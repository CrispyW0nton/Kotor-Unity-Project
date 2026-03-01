using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Abilities;
using KotORUnity.Weapons;
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

        [Header("Weapon")]
        [SerializeField] private WeaponBase equippedWeapon;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private NavMeshAgent _navAgent;
        private PlayerStats _stats;

        // ── STATE ──────────────────────────────────────────────────────────────
        private CompanionBehaviorTier _currentTier = CompanionBehaviorTier.AutonomousAction;
        private OrderQueue _orderQueue;
        private GameObject _currentTarget;
        private Formation _formation = Formation.Spread;
        private Vector3 _formationOffset;

        // Timers
        private float _autoAttackTimer = 0f;
        private float _coverUpdateTimer = 0f;
        private Transform _currentCoverPosition;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<PlayerStatsBehaviour>().Stats;
            _orderQueue = new OrderQueue(this);

            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
            EventBus.Subscribe(EventBus.EventType.GamePaused, OnGamePaused);
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

            _autoAttackTimer += Time.deltaTime;
            _coverUpdateTimer += Time.deltaTime;

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
