using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.World;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.Combat
{
    // ══════════════════════════════════════════════════════════════════════════
    //  D20 ROLL HELPERS
    // ══════════════════════════════════════════════════════════════════════════
    public static class D20
    {
        private static readonly System.Random _rng = new System.Random();

        public static int Roll()                     => _rng.Next(1, 21);
        public static int Roll(int die)              => die > 0 ? _rng.Next(1, die + 1) : 0;
        public static int Roll(int numDice, int die) => RollSum(numDice, die);

        public static int RollSum(int n, int die)
        {
            int total = 0;
            for (int i = 0; i < n; i++) total += Roll(die);
            return total;
        }

        /// <summary>Modifier from a D&D ability score (score-10)/2 rounded down.</summary>
        public static int AbilityMod(int score) => (int)Math.Floor((score - 10) / 2.0);

        /// <summary>Attack roll: d20 + attack bonus vs AC.</summary>
        public static AttackResult Attack(int attackBonus, int targetAC, int critThreat = 20)
        {
            int roll = Roll();
            if (roll == 1)  return new AttackResult(roll, false, AttackOutcome.Miss, attackBonus);
            if (roll == 20) return new AttackResult(roll, true,  AttackOutcome.CriticalHit, attackBonus);

            bool hit = (roll + attackBonus) >= targetAC;
            bool crit = roll >= critThreat;
            return new AttackResult(roll, crit && hit, hit ? AttackOutcome.Hit : AttackOutcome.Miss, attackBonus);
        }

        /// <summary>Saving throw: d20 + save bonus vs DC.</summary>
        public static bool SavingThrow(int saveBonus, int dc)
        {
            int roll = Roll();
            return roll == 20 || (roll != 1 && (roll + saveBonus) >= dc);
        }
    }

    public enum AttackOutcome { Miss, Hit, CriticalHit }

    public class AttackResult
    {
        public int           DieRoll      { get; }
        public bool          IsCritical   { get; }
        public AttackOutcome Outcome      { get; }
        public int           AttackBonus  { get; }

        public AttackResult(int die, bool crit, AttackOutcome outcome, int bonus)
        {
            DieRoll = die; IsCritical = crit; Outcome = outcome; AttackBonus = bonus;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COMBATANT  —  stats wrapper for anything that fights
    // ══════════════════════════════════════════════════════════════════════════
    public class Combatant
    {
        public string   Name        { get; set; } = "Unknown";
        public int      MaxHP       { get; set; } = 10;
        public int      CurrentHP   { get; set; } = 10;
        public int      ArmorClass  { get; set; } = 10;
        public int      AttackBonus { get; set; } = 0;
        public int      DamageBonus { get; set; } = 0;
        public int      DamageDie   { get; set; } = 6;
        public int      DamageNumDice { get; set; } = 1;
        public int      CritThreat  { get; set; } = 20;
        public int      CritMult    { get; set; } = 2;
        public float    Initiative  { get; set; } = 0;

        // Saving throws
        public int      FortSave    { get; set; } = 0;
        public int      ReflexSave  { get; set; } = 0;
        public int      WillSave    { get; set; } = 0;

        public bool     IsAlive     => CurrentHP > 0;
        public bool     IsPlayer    { get; set; } = false;
        public GameObject GameObject { get; set; }

        public void TakeDamage(int amount)
        {
            CurrentHP = Math.Max(0, CurrentHP - amount);
        }

        public void Heal(int amount)
        {
            CurrentHP = Math.Min(MaxHP, CurrentHP + amount);
        }

        public static Combatant FromCreatureData(KotorCreatureData data)
        {
            return new Combatant
            {
                Name        = data.DisplayName,
                MaxHP       = data.MaxHP,
                CurrentHP   = data.CurrentHP,
                ArmorClass  = KotORUnity.KotOR.Parsers.GffReader.GetInt(data.UTC, "NaturalAC", 10),
                AttackBonus = KotORUnity.KotOR.Parsers.GffReader.GetInt(data.UTC, "BaseAttackBonus", 0),
                DamageBonus = 0,
                DamageDie   = 6,
                FortSave    = KotORUnity.KotOR.Parsers.GffReader.GetInt(data.UTC, "FortSavingThrow", 0),
                ReflexSave  = KotORUnity.KotOR.Parsers.GffReader.GetInt(data.UTC, "RefSavingThrow", 0),
                WillSave    = KotORUnity.KotOR.Parsers.GffReader.GetInt(data.UTC, "WillSavingThrow", 0),
                GameObject  = data.gameObject
            };
        }

        public static Combatant FromPlayerStats(PlayerStats stats, GameObject go)
        {
            return new Combatant
            {
                Name        = stats.CharacterName,
                MaxHP       = (int)stats.MaxHealth,
                CurrentHP   = (int)stats.CurrentHealth,
                ArmorClass  = 10 + stats.Level / 2,  // simplified
                AttackBonus = stats.Level,
                DamageBonus = 0,
                DamageDie   = 6,
                IsPlayer    = true,
                GameObject  = go
            };
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ACTION  —  a single queued combat action
    // ══════════════════════════════════════════════════════════════════════════
    public enum CombatActionType { AttackMelee, AttackRanged, UseAbility, UseItem, Defend, Move, Wait }

    public class CombatAction
    {
        public CombatActionType ActionType { get; set; } = CombatActionType.AttackMelee;
        public Combatant        Initiator  { get; set; }
        public Combatant        Target     { get; set; }
        public string           AbilityRef { get; set; } = "";
        public string           ItemRef    { get; set; } = "";
        public Vector3          MoveTarget { get; set; }
        public float            ExecutionTime { get; set; } = 1f;  // seconds
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COMBAT MANAGER  —  drives the RTWP combat loop
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Real-Time with Pause (RTWP) combat manager matching KotOR's original rules.
    ///
    /// Attack rounds execute every 3 seconds (one KotOR combat round).
    /// Each combatant's action queue is consumed in turn.
    /// Pausing (Space) freezes the round timer so the player can issue orders.
    ///
    /// Wire up:
    ///   Attach to "GameManager" or a dedicated "CombatManager" GameObject.
    ///   Call EnterCombat(player, enemies) when combat starts.
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static CombatManager Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("RTWP Settings")]
        [Tooltip("Duration of one combat round in seconds.")]
        [SerializeField] private float roundDuration = 3.0f;

        [Tooltip("Auto-pause when a round completes?")]
        [SerializeField] private bool autoPauseOnRound = false;

        [Tooltip("Auto-pause when an enemy is detected?")]
        [SerializeField] private bool autoPauseOnDetect = true;

        // ── RUNTIME STATE ─────────────────────────────────────────────────────
        private readonly List<Combatant> _combatants    = new List<Combatant>();
        private readonly List<CombatAction> _actionQueue = new List<CombatAction>();

        private bool  _inCombat    = false;
        private bool  _paused      = false;
        private float _roundTimer  = 0f;
        private int   _roundNumber = 0;

        // ── MODE-SWITCH FLUSH ──────────────────────────────────────────────────
        // When the player switches between Action Mode and RTWP mode mid-combat
        // we need to flush any pending RTWP action queue so stale actions from
        // the old mode don't execute on the next round tick.
        private GameEnums.GameMode _lastMode = GameEnums.GameMode.RTS;

        public bool  InCombat  => _inCombat;
        public bool  IsPaused  => _paused;
        public int   Round     => _roundNumber;

        /// <summary>Allows runtime toggle from OptionsMenu.</summary>
        public bool AutoPauseOnRound
        {
            get => autoPauseOnRound;
            set => autoPauseOnRound = value;
        }

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Subscribe to mode-switch events so we can flush the action queue
            // whenever the player toggles between Action and RTWP modes.
            EventBus.Subscribe(EventBus.EventType.ModeSwitch, OnModeSwitch);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeSwitch, OnModeSwitch);
        }

        // Called whenever the game mode changes (Action ↔ RTWP).
        private void OnModeSwitch(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs ms)
            {
                GameEnums.GameMode newMode = ms.NewMode;
                if (newMode != _lastMode)
                {
                    // Flush the action queue – stale orders from the old mode
                    // must not execute in the new mode's next round.
                    if (_inCombat && _actionQueue.Count > 0)
                    {
                        Debug.Log($"[CombatManager] Mode switched {_lastMode}→{newMode}: flushing {_actionQueue.Count} queued actions.");
                        _actionQueue.Clear();
                        _roundTimer = 0f; // restart the round timer on mode switch
                        EventBus.Publish(EventBus.EventType.ActionQueueChanged, new EventBus.GameEventArgs());
                    }
                    _lastMode = newMode;
                }
            }
        }

        private void Update()
        {
            if (!_inCombat || _paused) return;

            _roundTimer += Time.deltaTime;
            if (_roundTimer >= roundDuration)
            {
                _roundTimer = 0f;
                ExecuteRound();
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>Begin combat with the given combatants.</summary>
        public void EnterCombat(List<Combatant> combatants)
        {
            _combatants.Clear();
            _combatants.AddRange(combatants);
            _actionQueue.Clear();
            _roundNumber = 0;
            _roundTimer  = 0f;
            _inCombat    = true;

            // Roll initiatives
            foreach (var c in _combatants)
                c.Initiative = D20.Roll() + D20.AbilityMod(10); // simplified: no dex modifier yet

            // Sort by initiative (highest first)
            _combatants.Sort((a, b) => b.Initiative.CompareTo(a.Initiative));

            if (autoPauseOnDetect) SetPaused(true);

            EventBus.Publish(EventBus.EventType.CombatStarted, new EventBus.GameEventArgs());
            Debug.Log($"[CombatManager] Combat started. {_combatants.Count} combatants.");
        }

        /// <summary>Queue an action for a combatant.</summary>
        public void QueueAction(CombatAction action)
        {
            if (action == null || action.Initiator == null) return;
            // Remove any existing action for this combatant (one action per round)
            _actionQueue.RemoveAll(a => a.Initiator == action.Initiator);
            _actionQueue.Add(action);

            EventBus.Publish(EventBus.EventType.ActionQueueChanged, new EventBus.GameEventArgs());
        }

        /// <summary>Toggle combat pause.</summary>
        public void TogglePause() => SetPaused(!_paused);

        public void SetPaused(bool paused)
        {
            _paused = paused;
            EventBus.Publish(paused ? EventBus.EventType.GamePaused : EventBus.EventType.GameResumed,
                new EventBus.GameEventArgs());
        }

        /// <summary>End combat (all enemies dead or fled).</summary>
        public void ExitCombat()
        {
            _inCombat = false;
            _combatants.Clear();
            _actionQueue.Clear();
            SetPaused(false);
            EventBus.Publish(EventBus.EventType.CombatEnded, new EventBus.GameEventArgs());
        }

        /// <summary>Get all living enemies (non-player combatants).</summary>
        public List<Combatant> GetLivingEnemies()
            => _combatants.FindAll(c => !c.IsPlayer && c.IsAlive);

        /// <summary>Return the full initiative-sorted combatant list (for CombatHUD).</summary>
        public List<Combatant> GetCombatantsInOrder() => new List<Combatant>(_combatants);

        /// <summary>Index of the currently-acting combatant (used by CombatHUD).</summary>
        public int ActiveCombatantIndex => _activeCombatantIndex;
        private int _activeCombatantIndex = 0;

        // ── ROUND EXECUTION ───────────────────────────────────────────────────
        private void ExecuteRound()
        {
            _roundNumber++;
            EventBus.Publish(EventBus.EventType.CombatRoundStarted, new EventBus.GameEventArgs());
            Debug.Log($"[CombatManager] Round {_roundNumber} begins.");

            // Fill in default actions for anyone who didn't queue an action
            foreach (var c in _combatants)
            {
                if (!c.IsAlive) continue;
                if (_actionQueue.Find(a => a.Initiator == c) == null)
                    AutoQueueAction(c);
            }

            // Execute in initiative order
            foreach (var c in _combatants)
            {
                if (!c.IsAlive) continue;
                var action = _actionQueue.Find(a => a.Initiator == c);
                if (action != null)
                {
                    ExecuteAction(action);
                    _actionQueue.Remove(action);
                }
            }

            // Check for end of combat
            if (GetLivingEnemies().Count == 0)
            {
                Debug.Log("[CombatManager] All enemies defeated.");
                ExitCombat();
                return;
            }

            if (autoPauseOnRound) SetPaused(true);

            EventBus.Publish(EventBus.EventType.CombatRoundEnded, new EventBus.GameEventArgs());
        }

        // ── ACTION EXECUTION ──────────────────────────────────────────────────
        private void ExecuteAction(CombatAction action)
        {
            if (action.Target != null && !action.Target.IsAlive) return;

            switch (action.ActionType)
            {
                case CombatActionType.AttackMelee:
                case CombatActionType.AttackRanged:
                    ResolveAttack(action);
                    break;

                case CombatActionType.UseAbility:
                    ResolveAbility(action);
                    break;

                case CombatActionType.Defend:
                    // Grant AC bonus for this round
                    Debug.Log($"[Combat] {action.Initiator.Name} defends (+2 AC this round).");
                    action.Initiator.ArmorClass += 2;
                    break;

                case CombatActionType.Wait:
                    break;
            }
        }

        private void ResolveAttack(CombatAction action)
        {
            var attacker = action.Initiator;
            var defender = action.Target;
            if (defender == null) return;

            var result = D20.Attack(attacker.AttackBonus, defender.ArmorClass, attacker.CritThreat);

            if (result.Outcome == AttackOutcome.Miss)
            {
                Debug.Log($"[Combat] {attacker.Name} misses {defender.Name}. (roll:{result.DieRoll})");
                return;
            }

            // Base damage
            int dmg = D20.Roll(attacker.DamageNumDice, attacker.DamageDie) + attacker.DamageBonus;
            if (dmg < 1) dmg = 1;

            if (result.Outcome == AttackOutcome.CriticalHit)
            {
                dmg *= attacker.CritMult;
                Debug.Log($"[Combat] CRITICAL! {attacker.Name} hits {defender.Name} for {dmg}.");
            }
            else
            {
                Debug.Log($"[Combat] {attacker.Name} hits {defender.Name} for {dmg}. (roll:{result.DieRoll})");
            }

            defender.TakeDamage(dmg);

            // Publish damage event
            EventBus.Publish(EventBus.EventType.EntityDamaged,
                new EventBus.DamageEventArgs(
                    attacker.GameObject, defender.GameObject,
                    dmg, DamageType.Physical,
                    result.IsCritical ? HitType.Critical : HitType.Normal));

            if (!defender.IsAlive)
            {
                Debug.Log($"[Combat] {defender.Name} is killed!");
                EventBus.Publish(EventBus.EventType.EntityKilled,
                    new EventBus.DamageEventArgs(
                        attacker.GameObject, defender.GameObject,
                        0, DamageType.Physical, HitType.Normal));

                // Sync HP back to KotorCreatureData if present
                if (defender.GameObject != null)
                {
                    var data = defender.GameObject.GetComponent<KotorCreatureData>();
                    if (data != null) data.CurrentHP = 0;
                }
            }
        }

        private void ResolveAbility(CombatAction action)
        {
            // Delegate to the AbilityBase system if available
            Debug.Log($"[Combat] {action.Initiator.Name} uses ability '{action.AbilityRef}'.");
        }

        // ── AI AUTO-QUEUE ─────────────────────────────────────────────────────
        private void AutoQueueAction(Combatant c)
        {
            if (c.IsPlayer) return; // Player should always queue manually

            // Simple AI: attack nearest living enemy of opposite faction
            var target = c.IsPlayer
                ? _combatants.Find(x => !x.IsPlayer && x.IsAlive)
                : _combatants.Find(x => x.IsPlayer && x.IsAlive);

            if (target == null) return;

            QueueAction(new CombatAction
            {
                ActionType = CombatActionType.AttackMelee,
                Initiator  = c,
                Target     = target
            });
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COMBAT INITIATOR  —  MonoBehaviour that detects nearby enemies and
    //                        starts/ends combat via CombatManager
    // ══════════════════════════════════════════════════════════════════════════
    public class CombatInitiator : MonoBehaviour
    {
        [SerializeField] private float detectionRadius = 10f;
        [SerializeField] private LayerMask enemyLayer;

        private bool _wasInCombat = false;

        private void Update()
        {
            if (CombatManager.Instance == null) return;
            if (CombatManager.Instance.InCombat) return;

            // Check for nearby hostile creatures
            var cols = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
            if (cols.Length > 0)
            {
                StartCombat(cols);
            }
        }

        private void StartCombat(Collider[] enemyCols)
        {
            var combatants = new List<Combatant>();

            // Add player
            var psb = GetComponent<PlayerStatsBehaviour>();
            if (psb != null)
                combatants.Add(Combatant.FromPlayerStats(psb.Stats, gameObject));

            // Add companions
            var companions = FindObjectsOfType<KotorCreatureData>();
            foreach (var cd in enemyCols)
            {
                var data = cd.GetComponent<KotorCreatureData>();
                if (data != null && data.IsAlive)
                    combatants.Add(Combatant.FromCreatureData(data));
            }

            if (combatants.Count > 1)
                CombatManager.Instance.EnterCombat(combatants);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
