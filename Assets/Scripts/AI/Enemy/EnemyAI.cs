using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Combat;
using KotORUnity.Weapons;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.AI.Enemy
{
    /// <summary>
    /// Enemy AI state machine.
    /// 
    /// States: Patrol → Detect → Engage → Flank/Suppress → Execute → Retreat
    /// 
    /// Key design requirements from design doc:
    ///   - Must be competent in BOTH slow-motion RTS (10% speed) AND real-time Action
    ///   - Difficulty modifiers scale reaction time, NOT just stats
    ///   - Has Sprint ability (20s CD) that ignores RTS slow-motion (kiting counter)
    ///   - Gains Adaptation stacks vs pause-scum players (+10% damage per pause cycle)
    ///   - Scales based on player mode (see GameConstants.NORMAL_ACTION_ENEMY_HP_MULT)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerStatsBehaviour))]
    public class EnemyAI : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Enemy Identity")]
        [SerializeField] private string enemyName = "Enemy";
        [SerializeField] private EnemyType enemyType = EnemyType.Ranged;
        [SerializeField] private int level = 1;
        [SerializeField] private int encounterIndex = 0;
        [SerializeField] private string factionId = "enemy_default";

        [Header("Detection")]
        [SerializeField] private float detectionRadius = 15f;
        [SerializeField] private float detectionAngle = 120f; // field of view
        [SerializeField] private float reactionTime = 0.5f;   // scaled by difficulty

        [Header("Combat")]
        [SerializeField] private float attackRange = 15f;
        [SerializeField] private float meleeRange = 2f;
        [SerializeField] private float autoAttackInterval = 0.7f;

        [Header("Patrol")]
        [SerializeField] private Transform[] patrolWaypoints;
        [SerializeField] private float waypointWaitTime = 2f;

        [Header("Flank")]
        [SerializeField] private float flankRadius = 10f;

        [Header("Sprint Counter (kiting prevention)")]
        [SerializeField] private bool canSprint = true;

        [Header("Weapon")]
        [SerializeField] private WeaponBase equippedWeapon;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private NavMeshAgent _navAgent;
        private PlayerStats _stats;

        // ── STATE MACHINE ──────────────────────────────────────────────────────
        private EnemyAIState _currentState = EnemyAIState.Patrol;
        private GameObject _currentTarget;
        private int _currentWaypointIndex = 0;
        private float _stateTimer = 0f;

        // Timers
        private float _attackTimer = 0f;
        private float _sprintCooldown = 0f;
        private float _reactionTimer = 0f;
        private bool _hasDetectedPlayer = false;

        // Adaptation stacks (pause-scum counter)
        private int _adaptationStacks = 0;
        private float _adaptationTimer = 0f;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _navAgent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<PlayerStatsBehaviour>().Stats;

            InitializeStats();
            gameObject.tag = "Enemy";

            EventBus.Subscribe(EventBus.EventType.EnemyAdaptationStackAdded, OnAdaptationEvent);
            EventBus.Subscribe(EventBus.EventType.GamePaused, OnGamePaused);
            EventBus.Subscribe(EventBus.EventType.GameResumed, OnGameResumed);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.EnemyAdaptationStackAdded, OnAdaptationEvent);
            EventBus.Unsubscribe(EventBus.EventType.GamePaused, OnGamePaused);
            EventBus.Unsubscribe(EventBus.EventType.GameResumed, OnGameResumed);
        }

        private void InitializeStats()
        {
            // Get active mode for HP scaling
            GameMode activeMode = GameManager.Instance?.CurrentMode ?? GameMode.Action;
            float scaledHP = CombatResolver.GetEnemyMaxHP(80f, level, encounterIndex, activeMode);

            var statsComp = GetComponent<PlayerStatsBehaviour>();
            if (statsComp != null)
                statsComp.Stats.SetLevel(level);
        }

        private void Update()
        {
            if (!_stats.IsAlive)
            {
                TransitionTo(EnemyAIState.Dead);
                return;
            }

            _attackTimer += Time.deltaTime;
            _stateTimer += Time.deltaTime;
            _sprintCooldown = Mathf.Max(0f, _sprintCooldown - Time.deltaTime);

            // Adaptation stack reset
            if (_adaptationStacks > 0)
            {
                _adaptationTimer += Time.unscaledDeltaTime; // uses REAL time
                if (_adaptationTimer >= GameConstants.ENEMY_ADAPTATION_RESET_TIME)
                {
                    _adaptationStacks = 0;
                    _adaptationTimer = 0f;
                }
            }

            // Run state machine
            switch (_currentState)
            {
                case EnemyAIState.Patrol:   TickPatrol();   break;
                case EnemyAIState.Detect:   TickDetect();   break;
                case EnemyAIState.Engage:   TickEngage();   break;
                case EnemyAIState.Flank:    TickFlank();    break;
                case EnemyAIState.Suppress: TickSuppress(); break;
                case EnemyAIState.Execute:  TickExecute();  break;
                case EnemyAIState.Retreat:  TickRetreat();  break;
                case EnemyAIState.Dead:                     break;
            }
        }

        // ── STATE: PATROL ──────────────────────────────────────────────────────
        private void TickPatrol()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0) return;

            if (!_navAgent.pathPending && _navAgent.remainingDistance < 0.5f)
            {
                if (_stateTimer < waypointWaitTime) return;
                _currentWaypointIndex = (_currentWaypointIndex + 1) % patrolWaypoints.Length;
                _navAgent.SetDestination(patrolWaypoints[_currentWaypointIndex].position);
                _stateTimer = 0f;
            }

            // Scan for player
            if (CanDetectPlayer())
            {
                _hasDetectedPlayer = false;
                _reactionTimer = 0f;
                TransitionTo(EnemyAIState.Detect);
            }
        }

        // ── STATE: DETECT ──────────────────────────────────────────────────────
        private void TickDetect()
        {
            // Reaction time delay — scaled by difficulty
            _reactionTimer += Time.deltaTime;
            float adjustedReactionTime = GetDifficultyAdjustedReactionTime();

            if (_reactionTimer >= adjustedReactionTime)
            {
                _hasDetectedPlayer = true;
                EventBus.Publish(EventBus.EventType.EnemyDetectedPlayer);
                TransitionTo(EnemyAIState.Engage);
            }

            // If player leaves FOV during detection, return to patrol
            if (!CanDetectPlayer())
                TransitionTo(EnemyAIState.Patrol);
        }

        // ── STATE: ENGAGE ──────────────────────────────────────────────────────
        private void TickEngage()
        {
            if (_currentTarget == null)
            {
                FindPlayer();
                if (_currentTarget == null) { TransitionTo(EnemyAIState.Patrol); return; }
            }

            float dist = Vector3.Distance(transform.position, _currentTarget.transform.position);

            if (dist <= attackRange)
            {
                // Attack
                FaceTarget(_currentTarget);
                AutoAttack();

                // Occasionally flank
                if (_stateTimer > 5f && enemyType == EnemyType.Ranged)
                {
                    TransitionTo(EnemyAIState.Flank);
                    _stateTimer = 0f;
                }
            }
            else
            {
                // Advance toward player
                MoveToward(_currentTarget.transform.position);

                // Sprint to close gap if kiting is detected
                TrySprintToPlayer(dist);
            }
        }

        // ── STATE: FLANK ───────────────────────────────────────────────────────
        private void TickFlank()
        {
            if (_currentTarget == null) { TransitionTo(EnemyAIState.Engage); return; }

            // Calculate a flanking position
            if (_stateTimer < 0.1f)
            {
                Vector3 flankPos = CalculateFlankPosition();
                _navAgent.SetDestination(flankPos);
            }

            // Return to engage once we've repositioned
            if (!_navAgent.pathPending && _navAgent.remainingDistance < 0.5f)
                TransitionTo(EnemyAIState.Engage);
        }

        // ── STATE: SUPPRESS ────────────────────────────────────────────────────
        private void TickSuppress()
        {
            // Suppression fire — lower accuracy, higher fire rate
            // Used by Ranged enemies to pin down player while flankers move
            if (_currentTarget != null)
            {
                FaceTarget(_currentTarget);
                AutoAttack(suppressionFire: true);
            }

            if (_stateTimer > 4f)
                TransitionTo(EnemyAIState.Engage);
        }

        // ── STATE: EXECUTE (boss state) ────────────────────────────────────────
        private void TickExecute()
        {
            // Aggressive final-phase behavior — continuous attack, no retreat
            if (_currentTarget != null)
            {
                MoveToward(_currentTarget.transform.position);
                FaceTarget(_currentTarget);
                AutoAttack();
            }
        }

        // ── STATE: RETREAT ─────────────────────────────────────────────────────
        private void TickRetreat()
        {
            // Low-HP retreat — move away from player
            if (_currentTarget != null)
            {
                Vector3 retreatDir = (transform.position - _currentTarget.transform.position).normalized;
                _navAgent.SetDestination(transform.position + retreatDir * 10f);
            }

            // Re-engage after healing or if player closes distance
            if (_stats.CurrentHealth > _stats.MaxHealth * 0.4f)
                TransitionTo(EnemyAIState.Engage);
        }

        // ── COMBAT ─────────────────────────────────────────────────────────────
        private void AutoAttack(bool suppressionFire = false)
        {
            if (equippedWeapon == null || _currentTarget == null) return;
            if (_attackTimer < autoAttackInterval) return;
            _attackTimer = 0f;

            float suppressionMultiplier = suppressionFire ? 0.5f : 1.0f;

            // Apply adaptation bonus damage
            float adaptationMultiplier = 1f + GameConstants.ENEMY_ADAPTATION_PER_PAUSE * _adaptationStacks;

            float finalDamage = 15f * suppressionMultiplier * adaptationMultiplier;
            DamageSystem.ApplyDamage(gameObject, _currentTarget, finalDamage,
                DamageType.Energy, HitType.Normal, GameManager.Instance?.CurrentMode ?? GameMode.Action);
        }

        // ── SPRINT COUNTER ─────────────────────────────────────────────────────
        /// <summary>
        /// Sprint ability ignores RTS slow-motion. Prevents kiting exploits.
        /// Design doc: "Enemy AI has Sprint ability on 20s cooldown (ignores slow-motion)"
        /// </summary>
        private void TrySprintToPlayer(float distance)
        {
            if (!canSprint || _sprintCooldown > 0f || distance < attackRange * 1.5f) return;

            // Check if player has been running (kiting) — if distance is increasing
            if (_currentTarget != null && distance > attackRange * 2f)
            {
                _sprintCooldown = GameConstants.ENEMY_SPRINT_COOLDOWN;
                StartCoroutine(SprintRoutine());
            }
        }

        private IEnumerator SprintRoutine()
        {
            // Sprint runs in REAL time — ignores RTS time scale
            float sprintDuration = 2f;
            float sprintSpeed = _navAgent.speed * 2.5f;
            float originalSpeed = _navAgent.speed;

            _navAgent.speed = sprintSpeed;

            float elapsed = 0f;
            while (elapsed < sprintDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_currentTarget != null)
                    _navAgent.SetDestination(_currentTarget.transform.position);
                yield return null;
            }

            _navAgent.speed = originalSpeed;
        }

        // ── HELPERS ────────────────────────────────────────────────────────────
        private bool CanDetectPlayer()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return false;

            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > detectionRadius) return false;

            Vector3 dirToPlayer = (player.transform.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, dirToPlayer);
            if (angle > detectionAngle / 2f) return false;

            // Raycast LOS check
            if (Physics.Raycast(transform.position + Vector3.up, dirToPlayer, out RaycastHit hit, dist))
                return hit.collider.CompareTag("Player");

            return true;
        }

        private void FindPlayer()
        {
            _currentTarget = GameObject.FindWithTag("Player");
        }

        private void FaceTarget(GameObject target)
        {
            Vector3 dir = (target.transform.position - transform.position);
            dir.y = 0f;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(dir), 5f * Time.deltaTime);
        }

        private void MoveToward(Vector3 destination)
        {
            _navAgent.SetDestination(destination);
        }

        private Vector3 CalculateFlankPosition()
        {
            if (_currentTarget == null) return transform.position;
            Vector3 toTarget = (_currentTarget.transform.position - transform.position).normalized;
            Vector3 flankDir = Vector3.Cross(toTarget, Vector3.up).normalized;
            if (Random.value > 0.5f) flankDir = -flankDir;
            return _currentTarget.transform.position + flankDir * flankRadius;
        }

        private float GetDifficultyAdjustedReactionTime()
        {
            if (GameManager.Instance == null) return reactionTime;
            switch (GameManager.Instance.CurrentDifficulty)
            {
                case Difficulty.Easy:     return reactionTime * 2.0f;
                case Difficulty.Normal:   return reactionTime;
                case Difficulty.Hard:     return reactionTime * 0.6f;
                case Difficulty.Nightmare: return reactionTime * 0.3f;
                default: return reactionTime;
            }
        }

        private void TransitionTo(EnemyAIState newState)
        {
            _currentState = newState;
            _stateTimer = 0f;
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnAdaptationEvent(EventBus.GameEventArgs args)
        {
            // ModeSwitchSystem published an adaptation event
            var msSystem = FindObjectOfType<ModeSwitchSystem>();
            if (msSystem != null)
            {
                _adaptationStacks = Mathf.Min(msSystem.PauseCycleCount, GameConstants.ENEMY_ADAPTATION_MAX_STACKS);
                EventBus.Publish(EventBus.EventType.EnemyAdaptationStackAdded);
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
        public EnemyAIState CurrentState    => _currentState;
        public EnemyType    EnemyType       => enemyType;
        public int          AdaptationStacks=> _adaptationStacks;
        public PlayerStats  Stats           => _stats;

        /// <summary>Stun this enemy for <paramref name="duration"/> seconds (Force Stasis).</summary>
        public void Stun(float duration)
        {
            StopAllCoroutines();
            _currentState = EnemyAIState.Idle;
            StartCoroutine(StunRoutine(duration));
        }

        private System.Collections.IEnumerator StunRoutine(float duration)
        {
            var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (nav != null) nav.isStopped = true;
            yield return new WaitForSeconds(duration);
            if (nav != null) nav.isStopped = false;
            _currentState = EnemyAIState.Patrol;
        }

        // ── FACTION API ────────────────────────────────────────────────────────

        /// <summary>Inspector-assigned faction identifier (e.g. "sith", "republic").</summary>
        public string FactionId => factionId;

        private Encounter.FactionRelation _factionRelationOverride = Encounter.FactionRelation.Hostile;
        private bool _factionOverrideActive = false;

        /// <summary>
        /// Override this enemy's relation to the player for the duration of an encounter.
        /// Called by EncounterManager when faction overrides are in effect.
        /// </summary>
        public void SetFactionRelation(Encounter.FactionRelation relation)
        {
            _factionRelationOverride  = relation;
            _factionOverrideActive    = true;

            // If turned Friendly or Neutral, exit combat immediately
            if (relation != Encounter.FactionRelation.Hostile)
            {
                _currentState = EnemyAIState.Idle;
                var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null) nav.isStopped = true;
                Debug.Log($"[EnemyAI] {enemyName} faction set to {relation} — standing down.");
            }
        }

        /// <summary>Remove a previously applied faction override, restoring default Hostile behaviour.</summary>
        public void ResetFactionRelation()
        {
            _factionOverrideActive = false;
            _factionRelationOverride = Encounter.FactionRelation.Hostile;
        }

        /// <summary>Returns true if this enemy will attack the player in its current faction state.</summary>
        public bool IsHostileToPlayer =>
            !_factionOverrideActive || _factionRelationOverride == Encounter.FactionRelation.Hostile;
    }
}
