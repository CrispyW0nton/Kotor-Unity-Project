using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Abilities;
using KotORUnity.Weapons;
using KotORUnity.Combat;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.AI.Companion
{
    /// <summary>
    /// Companion AI — operates in two distinct tiers based on active game mode.
    /// 
    /// Tier 1 / RTS Mode (CommandedRTS):
    ///   - Executes player-issued orders from the OrderQueue
    ///   - 100% efficiency — full pathfinding, predictive ability use
    ///   - Waits for player command rather than acting autonomously
    /// 
    /// Tier 2 / Action Mode (AutonomousAction):
    ///   - 60% efficiency (GameConstants.COMPANION_ACTION_MODE_EFFICIENCY)
    ///   - Autonomous: finds nearest threats, seeks cover, reacts to damage
    ///   - Uses abilities reactively rather than predictively
    /// 
    /// Tier 3 / Hybrid Override (HybridOverride):
    ///   - Player can issue a single priority command even in Action mode
    ///   - Companion executes it, then returns to Tier 2 autonomous behavior
    /// 
    /// This is the design doc specification:
    ///   "Action Mode AI: 60% as effective as RTS-commanded"
    ///   "RTS Commanded AI: 100% efficiency (player earns this via skill)"
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerStatsBehaviour))]
    public class CompanionAI : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private string companionName = "Companion";
        [SerializeField] private int slotIndex = 0; // 0, 1, 2

        [Header("Combat Settings")]
        [SerializeField] private float attackRange = 15f;
        [SerializeField] private float autoAttackInterval = 0.5f;
        [SerializeField] private float aggroDetectionRadius = 20f;

        [Header("Cover Seeking")]
        [SerializeField] private float coverSeekRadius = 15f;
        [SerializeField] private LayerMask coverLayerMask;
        [SerializeField] private float coverUpdateInterval = 2f;

        [Header("Abilities")]
        [SerializeField] private List<AbilityBase> abilities = new List<AbilityBase>();

        [Header("Force Powers")]
        [Tooltip("HP % threshold below which companion uses a heal power on themselves")]
        [SerializeField, Range(0f, 1f)] private float healSelfThreshold  = 0.35f;
        [Tooltip("HP % threshold below which companion uses a heal power on the player")]
        [SerializeField, Range(0f, 1f)] private float healAllyThreshold  = 0.40f;
        [Tooltip("HP % threshold below which companion uses offensive Force powers")]
        [SerializeField, Range(0f, 1f)] private float offensiveFPThreshold = 0.50f;
        [Tooltip("Interval in seconds between Force power evaluations")]
        [SerializeField] private float forcePowerEvalInterval = 2.5f;

        [Header("Weapon")]
        [SerializeField] private WeaponBase equippedWeapon;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private NavMeshAgent _navAgent;
        private PlayerStats _stats;
        private ForcePowerManager _forcePowers;  // may be null if not a Force-user

        // ── STATE ──────────────────────────────────────────────────────────────
        private CompanionBehaviorTier _currentTier = CompanionBehaviorTier.AutonomousAction;
        private OrderQueue _orderQueue;
        private GameObject _currentTarget;
        private Formation _formation = Formation.Spread;
        private Vector3 _formationOffset;

        // Timers
        private float _autoAttackTimer    = 0f;
        private float _coverUpdateTimer   = 0f;
        private float _forcePowerEvalTimer= 0f;
        private Transform _currentCoverPosition;

        // Stun support
        private bool  _isStunned     = false;
        private float _stunRemaining = 0f;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _navAgent   = GetComponent<NavMeshAgent>();
            _stats      = GetComponent<PlayerStatsBehaviour>().Stats;
            _forcePowers= GetComponent<ForcePowerManager>(); // may be null
            _orderQueue = new OrderQueue(this);

            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
            EventBus.Subscribe(EventBus.EventType.GamePaused,  OnGamePaused);
            EventBus.Subscribe(EventBus.EventType.GameResumed, OnGameResumed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
            EventBus.Unsubscribe(EventBus.EventType.GamePaused, OnGamePaused);
            EventBus.Unsubscribe(EventBus.EventType.GameResumed, OnGameResumed);
        }

        private void Update()
        {
            if (!_stats.IsAlive) return;

            // Handle stun countdown
            if (_isStunned)
            {
                _stunRemaining -= Time.deltaTime;
                if (_stunRemaining <= 0f) _isStunned = false;
                else return; // stunned: no actions
            }

            _autoAttackTimer     += Time.deltaTime;
            _coverUpdateTimer    += Time.deltaTime;
            _forcePowerEvalTimer += Time.deltaTime;

            // Evaluate Force powers on a slower tick
            if (_forcePowers != null && _forcePowerEvalTimer >= forcePowerEvalInterval)
            {
                _forcePowerEvalTimer = 0f;
                EvaluateForcePowerUse();
            }

            switch (_currentTier)
            {
                case CompanionBehaviorTier.CommandedRTS:
                    TickRTSBehavior();
                    break;
                case CompanionBehaviorTier.AutonomousAction:
                    TickAutonomousBehavior();
                    break;
                case CompanionBehaviorTier.HybridOverride:
                    TickHybridBehavior();
                    break;
            }
        }

        // ── RTS TIER: Execute Queued Orders ────────────────────────────────────
        private void TickRTSBehavior()
        {
            // Process orders from the queue
            if (!_orderQueue.IsEmpty)
            {
                _orderQueue.ExecuteNext();
                return;
            }

            // No orders — hold position, auto-attack if enemy in range
            AutoAttack(1.0f); // Full 100% efficiency in RTS mode
        }

        // ── AUTONOMOUS TIER: Cover + Reactive Attack ───────────────────────────
        private void TickAutonomousBehavior()
        {
            // Update cover periodically
            if (_coverUpdateTimer >= coverUpdateInterval)
            {
                _coverUpdateTimer = 0f;
                TrySeekCover();
            }

            // Find and engage nearest enemy at 60% efficiency
            FindNearestEnemy();
            AutoAttack(GameConstants.COMPANION_ACTION_MODE_EFFICIENCY);

            // Maintain squad formation loosely
            MaintainLooseFormation();
        }

        // ── HYBRID TIER: Execute single override command, then return to Autonomous ─
        private void TickHybridBehavior()
        {
            if (!_orderQueue.IsEmpty)
            {
                _orderQueue.ExecuteNext();
            }
            else
            {
                // Override complete — return to autonomous
                _currentTier = CompanionBehaviorTier.AutonomousAction;
            }
        }

        // ── COMBAT LOGIC ───────────────────────────────────────────────────────
        private void FindNearestEnemy()
        {
            if (_currentTarget != null)
            {
                var targetStats = _currentTarget.GetComponent<PlayerStatsBehaviour>()?.Stats;
                if (targetStats != null && targetStats.IsAlive &&
                    Vector3.Distance(transform.position, _currentTarget.transform.position) <= aggroDetectionRadius)
                    return; // Keep current valid target
            }

            // Search for new target
            Collider[] hits = Physics.OverlapSphere(transform.position, aggroDetectionRadius);
            float nearest = float.MaxValue;
            _currentTarget = null;

            foreach (var hit in hits)
            {
                if (!hit.CompareTag("Enemy")) continue;
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearest)
                {
                    nearest = dist;
                    _currentTarget = hit.gameObject;
                }
            }
        }

        private void AutoAttack(float efficiencyMultiplier)
        {
            if (_currentTarget == null || equippedWeapon == null) return;
            if (_autoAttackTimer < autoAttackInterval / efficiencyMultiplier) return;

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);
            if (dist <= attackRange)
            {
                // Face target
                transform.LookAt(_currentTarget.transform);

                // Fire weapon at RTS DPS rate
                equippedWeapon.RTSAttackTick(_currentTarget, autoAttackInterval);
                _autoAttackTimer = 0f;
            }
            else
            {
                // Move toward target
                _navAgent.SetDestination(_currentTarget.transform.position);
            }
        }

        // ── COVER SEEKING ──────────────────────────────────────────────────────
        private void TrySeekCover()
        {
            if (_currentTarget == null) return;

            // Find cover objects in radius
            Collider[] coverObjects = Physics.OverlapSphere(transform.position, coverSeekRadius, coverLayerMask);
            Transform bestCover = null;
            float bestScore = float.MinValue;

            foreach (var cover in coverObjects)
            {
                // Score: closer to us, further from enemy
                float distFromUs = Vector3.Distance(transform.position, cover.transform.position);
                float distFromEnemy = Vector3.Distance(_currentTarget.transform.position, cover.transform.position);
                float score = distFromEnemy - distFromUs;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCover = cover.transform;
                }
            }

            if (bestCover != null && bestCover != _currentCoverPosition)
            {
                _currentCoverPosition = bestCover;
                _navAgent.SetDestination(bestCover.position);
            }
        }

        // ── FORMATION ──────────────────────────────────────────────────────────
        private void MaintainLooseFormation()
        {
            if (_currentTarget != null) return; // Don't reposition while in combat

            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            Vector3 desiredPos = player.transform.position + _formationOffset;
            float dist = Vector3.Distance(transform.position, desiredPos);

            if (dist > 3f) // Only reposition if more than 3m away
                _navAgent.SetDestination(desiredPos);
        }

        // ── FORCE POWER AI ─────────────────────────────────────────────────────
        private void EvaluateForcePowerUse()
        {
            if (_forcePowers == null || !_forcePowers.IsReady) return;

            float selfHpPct  = _stats.CurrentHealth / _stats.MaxHealth;

            // ── Priority 1: Heal self if critically wounded ────────────────────
            if (selfHpPct < healSelfThreshold)
            {
                if (TryUseForcePower("force heal") || TryUseForcePower("cure")) return;
            }

            // ── Priority 2: Heal player if near death ─────────────────────────
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                var pStats = player.GetComponent<PlayerStatsBehaviour>()?.Stats;
                if (pStats != null)
                {
                    float playerHpPct = pStats.CurrentHealth / pStats.MaxHealth;
                    if (playerHpPct < healAllyThreshold)
                    {
                        if (TryUseForcePower("force heal", player) ||
                            TryUseForcePower("cure",        player)) return;
                    }
                }
            }

            // ── Priority 3: Offensive powers if enemy is high-threat ──────────
            if (_currentTarget != null)
            {
                var tStats = _currentTarget.GetComponent<PlayerStatsBehaviour>()?.Stats;
                if (tStats != null)
                {
                    float targetHpPct = tStats.CurrentHealth / tStats.MaxHealth;
                    // Use CC power on healthy enemies, damage power on weakened ones
                    if (targetHpPct > offensiveFPThreshold)
                    {
                        if (TryUseForcePower("stasis",          _currentTarget)) return;
                        if (TryUseForcePower("stasis field",    _currentTarget)) return;
                    }
                    else
                    {
                        if (TryUseForcePower("force lightning", _currentTarget)) return;
                        if (TryUseForcePower("death field",     _currentTarget)) return;
                        if (TryUseForcePower("drain life",      _currentTarget)) return;
                    }
                }

                // ── Force push on any melee attacker that is very close ────────
                if (Vector3.Distance(transform.position, _currentTarget.transform.position) < 3f)
                {
                    TryUseForcePower("force push", _currentTarget);
                }
            }

            // ── Priority 4: Buff self if force valor or speed not active ──────
            if (selfHpPct > 0.6f && _currentTarget != null)
            {
                TryUseForcePower("force speed");
                TryUseForcePower("force valor");
            }
        }

        /// <summary>
        /// Try to use a Force power by label. Returns true if cast was successful.
        /// </summary>
        private bool TryUseForcePower(string powerLabel, GameObject target = null)
        {
            if (_forcePowers == null) return false;
            var def = ForcePowerRegistry.GetByLabel(powerLabel);
            if (def == null) return false;
            return _forcePowers.UsePower(def.SpellId, target ?? gameObject);
        }

        /// <summary>
        /// External call: this companion is ForcePowerManager.IsReady shorthand.
        /// Also exposes stun support so Force Stasis can affect enemies.
        /// </summary>
        public void Stun(float duration)
        {
            _isStunned     = true;
            _stunRemaining = Mathf.Max(_stunRemaining, duration);
            if (_navAgent != null) _navAgent.isStopped = true;
            Debug.Log($"[CompanionAI] {companionName} stunned for {duration:F1}s.");
        }

        // ── ORDER API (called by RTSPlayerController) ──────────────────────────
        public void OrderMoveTo(Vector3 destination)
        {
            _orderQueue.EnqueueMove(destination);
        }

        public void OrderAttack(GameObject target)
        {
            _currentTarget = target;
            _orderQueue.EnqueueAttack(target);
        }

        public void QueueAbilityBySlot(int slot)
        {
            if (slot < 0 || slot >= abilities.Count || abilities[slot] == null) return;
            _orderQueue.EnqueueAbility(abilities[slot], _currentTarget);
        }

        public void SetFormation(Formation formation)
        {
            _formation = formation;
            // Formation offset calculated by RTSPlayerController
        }

        public void SetFormationOffset(Vector3 offset)
        {
            _formationOffset = offset;
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnModeChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs switchArgs)
            {
                _currentTier = switchArgs.NewMode == GameMode.RTS
                    ? CompanionBehaviorTier.CommandedRTS
                    : CompanionBehaviorTier.AutonomousAction;

                // Clear order queue when switching to Autonomous
                if (switchArgs.NewMode == GameMode.Action)
                    _orderQueue.Clear();
            }
        }

        private void OnGamePaused(EventBus.GameEventArgs args)
        {
            _navAgent.isStopped = true;
        }

        private void OnGameResumed(EventBus.GameEventArgs args)
        {
            _navAgent.isStopped = false;
        }

        // ── PUBLIC ACCESSORS ───────────────────────────────────────────────────
        public string CompanionName => companionName;
        public int SlotIndex => slotIndex;
        public CompanionBehaviorTier CurrentTier => _currentTier;
        public PlayerStats Stats => _stats;
        public bool IsAlive => _stats?.IsAlive ?? false;
        public GameObject CurrentTarget => _currentTarget;
        public OrderQueue OrderQueue => _orderQueue;
    }
}
