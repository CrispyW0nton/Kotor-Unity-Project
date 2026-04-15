using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Data;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Combat
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER DEFINITIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Alignment of a Force power — determines which characters can use it and
    /// how it affects the player's Light/Dark Side Meter.
    /// </summary>
    public enum ForcePowerAlignment
    {
        Universal,   // Any Force-user can learn it
        LightSide,   // Only Light Side characters
        DarkSide     // Only Dark Side characters
    }

    /// <summary>
    /// KotOR Force power definition — mirrors the 'spells.2da' table row.
    /// </summary>
    [Serializable]
    public class ForcePowerDef
    {
        public int    SpellId;           // 2DA row index
        public string Label;             // e.g. "Force Push", "Lightning"
        public string ResRef;            // resref for icon texture
        public ForcePowerAlignment Alignment;
        public int    ForceCost;         // Force Points consumed on cast
        public float  CooldownSeconds;   // Local cooldown for this power
        public int    DC;                // Difficulty class for saves (0 = no save)
        public bool   IsRanged;          // Target-required or area around caster
        public string ScriptName;        // NWScript to fire on use (optional)

        // Convenience factory from the 2DA table
        public static ForcePowerDef FromTwoDA(int rowIndex, Data.TwoDATable spells2da)
        {
            if (spells2da == null) return null;
            string label  = spells2da.GetString(rowIndex, "label");
            if (string.IsNullOrEmpty(label)) return null;

            string align  = spells2da.GetString(rowIndex, "forcealign").ToLowerInvariant();
            ForcePowerAlignment alignment = align switch
            {
                "light" => ForcePowerAlignment.LightSide,
                "dark"  => ForcePowerAlignment.DarkSide,
                _       => ForcePowerAlignment.Universal
            };

            return new ForcePowerDef
            {
                SpellId        = rowIndex,
                Label          = label,
                ResRef         = spells2da.GetString(rowIndex, "iconresref"),
                Alignment      = alignment,
                ForceCost      = spells2da.GetInt(rowIndex, "forcecost"),
                CooldownSeconds= spells2da.GetFloat(rowIndex, "cooldowntime"),
                DC             = spells2da.GetInt(rowIndex, "dc"),
                IsRanged       = spells2da.GetBool(rowIndex, "rangedflag"),
                ScriptName     = spells2da.GetString(rowIndex, "onimpact")
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER INSTANCE (on a specific character)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tracks the cooldown and known-status of a Force power on a character.
    /// </summary>
    public class ForcePowerInstance
    {
        public ForcePowerDef Def      { get; }
        public float CooldownRemaining { get; private set; }
        public bool  IsReady           => CooldownRemaining <= 0f;

        public ForcePowerInstance(ForcePowerDef def)
        {
            Def = def;
        }

        public void StartCooldown()  => CooldownRemaining = Def.CooldownSeconds;
        public void Tick(float dt)   => CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER MANAGER  (MonoBehaviour — attach to player / companion)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages a character's Force Point pool and known Force powers.
    ///
    /// Force Points = Wisdom modifier × class multiplier + base from level.
    /// Recovering FP: 1 FP/second when out of combat; 0.2 FP/second in combat.
    ///
    /// Alignment meter:  –100 (full dark) … +100 (full light).
    /// Each Light-Side power shifts +alignment_cost toward +100.
    /// Each Dark-Side power shifts –alignment_cost toward –100.
    /// </summary>
    public class ForcePowerManager : MonoBehaviour
    {
        // ── EVENTS ─────────────────────────────────────────────────────────────
        public event Action<float, float> OnForcePointsChanged;  // (current, max)
        public event Action<int>          OnAlignmentChanged;    // new alignment value
        public event Action<ForcePowerDef, bool> OnPowerUsed;   // (power, success)

        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Force Points")]
        [SerializeField] private float _maxFP          = 60f;
        [SerializeField] private float _fpRegenRate     = 1f;    // per second out of combat
        [SerializeField] private float _fpRegenCombat   = 0.2f;  // per second in combat

        [Header("Alignment")]
        [SerializeField, Range(-100, 100)]
        private int _alignment = 50; // starts Light-neutral

        // ── STATE ──────────────────────────────────────────────────────────────
        private float _currentFP;
        private bool  _inCombat;
        private readonly List<ForcePowerInstance> _knownPowers = new List<ForcePowerInstance>();

        // ── PROPERTIES ────────────────────────────────────────────────────────
        public float CurrentFP    => _currentFP;
        public float MaxFP        => _maxFP;
        public int   Alignment    => _alignment;
        public bool  IsLightSide  => _alignment > 0;
        public bool  IsDarkSide   => _alignment < 0;
        public IReadOnlyList<ForcePowerInstance> KnownPowers => _knownPowers;

        /// <summary>True when the character has FP and at least one power is off cooldown.</summary>
        public bool IsReady => _currentFP > 0f &&
                               _knownPowers.Exists(p => p.IsReady && p.Def.ForceCost <= _currentFP);

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _currentFP = _maxFP;
        }

        private void Start()
        {
            EventBus.Subscribe(EventBus.EventType.CombatStarted,  _ => _inCombat = true);
            EventBus.Subscribe(EventBus.EventType.CombatEnded,    _ => _inCombat = false);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.CombatStarted, _ => _inCombat = true);
            EventBus.Unsubscribe(EventBus.EventType.CombatEnded,   _ => _inCombat = false);
        }

        private void Update()
        {
            // Regenerate FP
            float regen = (_inCombat ? _fpRegenCombat : _fpRegenRate) * Time.deltaTime;
            SetFP(_currentFP + regen);

            // Tick all power cooldowns
            foreach (var p in _knownPowers)
                p.Tick(Time.deltaTime);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>Attempt to use a Force power. Returns true if successful.</summary>
        public bool UsePower(int spellId, GameObject target = null)
        {
            var inst = _knownPowers.Find(p => p.Def.SpellId == spellId);
            if (inst == null)
            {
                Debug.LogWarning($"[ForcePowerManager] Power {spellId} not known.");
                OnPowerUsed?.Invoke(null, false);
                return false;
            }

            if (!inst.IsReady)
            {
                Debug.Log($"[ForcePowerManager] '{inst.Def.Label}' still on cooldown ({inst.CooldownRemaining:F1}s).");
                OnPowerUsed?.Invoke(inst.Def, false);
                return false;
            }

            if (_currentFP < inst.Def.ForceCost)
            {
                Debug.Log($"[ForcePowerManager] Not enough Force Points ({_currentFP:F0}/{inst.Def.ForceCost}).");
                OnPowerUsed?.Invoke(inst.Def, false);
                return false;
            }

            // Check alignment restriction
            if (inst.Def.Alignment == ForcePowerAlignment.LightSide && IsDarkSide)
            {
                Debug.Log($"[ForcePowerManager] '{inst.Def.Label}' requires Light Side alignment.");
                OnPowerUsed?.Invoke(inst.Def, false);
                return false;
            }
            if (inst.Def.Alignment == ForcePowerAlignment.DarkSide && IsLightSide)
            {
                Debug.Log($"[ForcePowerManager] '{inst.Def.Label}' requires Dark Side alignment.");
                OnPowerUsed?.Invoke(inst.Def, false);
                return false;
            }

            // Consume FP and start cooldown
            SetFP(_currentFP - inst.Def.ForceCost);
            inst.StartCooldown();

            // Shift alignment
            int shift = inst.Def.Alignment switch
            {
                ForcePowerAlignment.LightSide => +5,
                ForcePowerAlignment.DarkSide  => -5,
                _                             =>  0
            };
            SetAlignment(_alignment + shift);

            // Execute effect
            ApplyPowerEffect(inst.Def, target);

            OnPowerUsed?.Invoke(inst.Def, true);
            EventBus.Publish(EventBus.EventType.AbilityUsed,
                new EventBus.AbilityEventArgs(gameObject, target, inst.Def.Label,
                    GameManager.Instance?.CurrentMode ?? GameEnums.GameMode.Action));

            return true;
        }

        /// <summary>Learn a new Force power (add to known list).</summary>
        public bool LearnPower(ForcePowerDef def)
        {
            if (def == null) return false;
            if (_knownPowers.Exists(p => p.Def.SpellId == def.SpellId)) return false; // already known

            _knownPowers.Add(new ForcePowerInstance(def));
            Debug.Log($"[ForcePowerManager] Learned '{def.Label}'.");
            return true;
        }

        /// <summary>Set maximum FP (recalculate on level-up).</summary>
        public void SetMaxFP(float max)
        {
            _maxFP     = Mathf.Max(1f, max);
            _currentFP = Mathf.Min(_currentFP, _maxFP);
            OnForcePointsChanged?.Invoke(_currentFP, _maxFP);
        }

        /// <summary>Restore FP by amount (rest / consumable).</summary>
        public void RestoreFP(float amount)
        {
            SetFP(_currentFP + amount);
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        private void SetFP(float value)
        {
            float prev = _currentFP;
            _currentFP = Mathf.Clamp(value, 0f, _maxFP);
            if (!Mathf.Approximately(prev, _currentFP))
                OnForcePointsChanged?.Invoke(_currentFP, _maxFP);
        }

        private void SetAlignment(int value)
        {
            int prev   = _alignment;
            _alignment = Mathf.Clamp(value, -100, 100);
            if (_alignment != prev)
            {
                OnAlignmentChanged?.Invoke(_alignment);
                EventBus.Publish(EventBus.EventType.AlignmentChanged,
                    new EventBus.AlignmentEventArgs(_alignment, _alignment - prev));
            }
        }

        private void ApplyPowerEffect(ForcePowerDef def, GameObject target)
        {
            // Fire NWScript handler if registered
            if (!string.IsNullOrEmpty(def.ScriptName))
            {
                Scripting.NWScriptVM.Run(def.ScriptName, gameObject, target);
                return;
            }

            // Built-in implementations for the most common powers
            switch (def.Label.ToLowerInvariant())
            {
                case "force push":
                    ApplyForcePush(target, def.DC);
                    break;
                case "force wave":
                    ApplyForceWave(gameObject, 6f, def.DC);
                    break;
                case "force lightning":
                case "sith lightning":
                    // Ranged chain: hit primary then arc to nearby targets
                    ApplyForceDamage(target, 2, 8, DamageType.Force);
                    foreach (var nearby in GetTargetsInRadius(
                                 target != null ? target.transform.position : transform.position,
                                 3f))
                    {
                        if (nearby != target && nearby != gameObject)
                            ApplyForceDamage(nearby, 1, 4, DamageType.Force); // arc
                    }
                    break;
                case "force heal":
                case "cure":
                    ApplyForceHeal(gameObject, 2, 8);
                    break;
                case "mass heal":
                    // Heal all allies in radius 8
                    foreach (var ally in GetTargetsInRadius(transform.position, 8f))
                    {
                        if (ally.CompareTag("Player") || ally.CompareTag("Companion"))
                            ApplyForceHeal(ally, 1, 8);
                    }
                    break;
                case "stasis":
                    ApplyStasis(target, 6f);
                    break;
                case "stasis field":
                    // AoE stasis in radius 5
                    foreach (var t in GetTargetsInRadius(
                                 target != null ? target.transform.position : transform.position,
                                 5f))
                    {
                        if (!t.CompareTag("Player") && !t.CompareTag("Companion"))
                            ApplyStasis(t, 4f);
                    }
                    break;
                case "force speed":
                    ApplyForceSpeed(gameObject, 2f, 12f);
                    break;
                case "force valor":
                    ApplyForceBuff(gameObject, +2, 12f);
                    // Also buff companions in range
                    foreach (var ally in GetTargetsInRadius(transform.position, 6f))
                        if (ally.CompareTag("Companion")) ApplyForceBuff(ally, +1, 12f);
                    break;
                case "drain life":
                case "death field":
                    ApplyDrainLife(target, gameObject, def.DC);
                    break;
                case "force whirlwind":
                    ApplyForceWave(gameObject, 4f, def.DC);
                    foreach (var t in GetTargetsInRadius(transform.position, 4f))
                        if (t != gameObject) ApplyForceDamage(t, 1, 6, DamageType.Force);
                    break;
                default:
                    Debug.Log($"[ForcePowerManager] '{def.Label}' — no built-in effect (script-driven).");
                    break;
            }
        }

        // ── FORCE EFFECTS ─────────────────────────────────────────────────────

        // ── AOE HELPER ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns all living GameObjects within radius of origin that are
        /// on the specified layer mask (defaults to everything).
        /// </summary>
        private static List<GameObject> GetTargetsInRadius(Vector3 origin, float radius,
                                                            int layerMask = ~0)
        {
            var result  = new List<GameObject>();
            var cols    = Physics.OverlapSphere(origin, radius, layerMask);
            foreach (var c in cols)
            {
                if (c.gameObject == null) continue;
                // Skip dead creatures
                var psb = c.GetComponent<PlayerStatsBehaviour>();
                if (psb != null && !psb.Stats.IsAlive) continue;
                var kcd = c.GetComponent<KotORUnity.World.KotorCreatureData>();
                if (kcd != null && !kcd.IsAlive) continue;
                result.Add(c.gameObject);
            }
            return result;
        }

        private static void ApplyForcePush(GameObject target, int dc)
        {
            // If no target specified → AoE push around caster's last known position
            if (target == null) return;

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Push away from caster (best approximation without caster ref here)
                Vector3 dir = (target.transform.position + Vector3.up * 0.5f).normalized;
                rb.AddForce(dir * 6f + Vector3.up * 3f, ForceMode.Impulse);
            }
            Debug.Log($"[Force] Force Push applied to {target.name}.");
        }

        /// <summary>Force Wave — AoE knockback around caster.</summary>
        private static void ApplyForceWave(GameObject caster, float radius, int dc)
        {
            if (caster == null) return;
            var targets = GetTargetsInRadius(caster.transform.position, radius);
            foreach (var t in targets)
            {
                if (t == caster) continue;
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 dir = (t.transform.position - caster.transform.position).normalized;
                    rb.AddForce(dir * 5f + Vector3.up * 3f, ForceMode.Impulse);
                }
                Debug.Log($"[Force] Force Wave: knockback on {t.name}.");
            }
        }

        private static void ApplyForceDamage(GameObject target, int diceCount, int diceSides,
                                              DamageType damageType)
        {
            if (target == null) return;
            int dmg = 0;
            for (int i = 0; i < diceCount; i++)
                dmg += UnityEngine.Random.Range(1, diceSides + 1);

            var stats = target.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats != null) stats.TakeDamage(dmg);
            Debug.Log($"[Force] Dealt {dmg} {damageType} damage to {target.name}.");
        }

        private static void ApplyForceHeal(GameObject target, int diceCount, int diceSides)
        {
            if (target == null) return;
            int heal = 0;
            for (int i = 0; i < diceCount; i++)
                heal += UnityEngine.Random.Range(1, diceSides + 1);

            var stats = target.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats != null) stats.Heal(heal);
            Debug.Log($"[Force] Healed {heal} HP on {target.name}.");
        }

        private static void ApplyStasis(GameObject target, float duration)
        {
            if (target == null) return;
            var enemy = target.GetComponent<AI.Enemy.EnemyAI>();
            if (enemy != null) enemy.Stun(duration);
            Debug.Log($"[Force] Stasis applied to {target.name} for {duration}s.");
        }

        private static void ApplyForceSpeed(GameObject target, float speedMultiplier, float duration)
        {
            var ctrl = target.GetComponent<Player.ActionPlayerController>();
            if (ctrl != null) ctrl.ApplySpeedBuff(speedMultiplier, duration);
            Debug.Log($"[Force] Force Speed x{speedMultiplier} for {duration}s.");
        }

        private static void ApplyForceBuff(GameObject target, int attackBonus, float duration)
        {
            // Mark on stats — combat system checks this
            var stats = target.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats != null) stats.AddTemporaryAttackBonus(attackBonus, duration);
            Debug.Log($"[Force] Force Valor +{attackBonus} ATK for {duration}s.");
        }

        private static void ApplyDrainLife(GameObject target, GameObject caster, int dc)
        {
            if (target == null || caster == null) return;
            var targetStats = target.GetComponent<PlayerStatsBehaviour>()?.Stats;
            var casterStats = caster.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (targetStats == null || casterStats == null) return;

            int dmg = UnityEngine.Random.Range(1, 8) + UnityEngine.Random.Range(1, 8);
            targetStats.TakeDamage(dmg);
            casterStats.Heal(dmg / 2);
            Debug.Log($"[Force] Drain Life: {dmg} dmg from {target.name}, healed {dmg/2} on {caster.name}.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER REGISTRY  (static — shared catalogue from spells.2da)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Singleton catalogue of all Force power definitions loaded from spells.2da.
    /// Populated once at startup by GameDataRepository.
    /// </summary>
    public static class ForcePowerRegistry
    {
        private static readonly Dictionary<int, ForcePowerDef> _byId
            = new Dictionary<int, ForcePowerDef>();
        private static readonly Dictionary<string, ForcePowerDef> _byLabel
            = new Dictionary<string, ForcePowerDef>(StringComparer.OrdinalIgnoreCase);

        public static bool IsLoaded { get; private set; }

        public static void Load(Data.TwoDATable spells2da)
        {
            _byId.Clear(); _byLabel.Clear();
            if (spells2da == null) { Debug.LogWarning("[ForcePowerRegistry] spells.2da is null."); return; }

            for (int i = 0; i < spells2da.RowCount; i++)
            {
                var def = ForcePowerDef.FromTwoDA(i, spells2da);
                if (def == null) continue;
                _byId[def.SpellId]       = def;
                _byLabel[def.Label]      = def;
            }
            IsLoaded = true;
            Debug.Log($"[ForcePowerRegistry] Loaded {_byId.Count} Force powers.");
        }

        public static ForcePowerDef GetById(int id)
        {
            _byId.TryGetValue(id, out var def);
            return def;
        }

        public static ForcePowerDef GetByLabel(string label)
        {
            _byLabel.TryGetValue(label, out var def);
            return def;
        }

        public static IEnumerable<ForcePowerDef> All() => _byId.Values;

        /// <summary>Register a runtime-created force power definition (e.g. from mod tools).</summary>
        public static void Register(ForcePowerDef def)
        {
            if (def == null) return;
            _byId[def.SpellId]     = def;
            _byLabel[def.Label]    = def;
        }
    }
}
