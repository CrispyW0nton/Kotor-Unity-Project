using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Combat;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Abilities
{
    // ── BLADE RUSH ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Blade Rush — high-damage melee charge.
    /// 
    /// Action Mode: Requires directional input + timing → 100% damage on perfect execution
    /// RTS Mode:    Auto-executes on target → 80% damage, guaranteed hit
    /// 
    /// Design doc: "Blade Rush: Requires directional input + timing (Action) vs
    /// Auto-executes on target with 80% of Action damage (RTS)"
    /// </summary>
    public class BladeRush : AbilityBase
    {
        [Header("Blade Rush Config")]
        [SerializeField] private float rushDistance = 8f;
        [SerializeField] private float rushDuration = 0.3f;

        // Timing window for perfect execution in Action mode
        [SerializeField] private float perfectTimingWindow = 0.2f;

        private bool _isPerfectTimed = false;

        protected override void Awake()
        {
            base.Awake();
            abilityName = "Blade Rush";
            description = "Charge at an enemy with a powerful strike. Perfect timing deals full damage.";
            abilityType = AbilityType.Melee;
            executionType = AbilityExecutionType.ModeVariant;
            baseDamage = 45f;
            cooldown = 10f;
            range = 10f;
        }

        /// <summary>
        /// Call this with perfect timing for full damage in Action mode.
        /// Must be called within perfectTimingWindow before Execute().
        /// </summary>
        public void RegisterPerfectTiming() => _isPerfectTimed = true;

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            // Perfect timing grants full damage; otherwise 70% 
            float finalDamage = _isPerfectTimed ? damage : damage * 0.7f;
            _isPerfectTimed = false;

            if (target == null) return;

            // Rush toward target
            StartCoroutine(RushRoutine(caster, target, finalDamage));
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            if (target == null) return;
            // RTS: guaranteed auto-execute at 80% (already handled by CombatResolver)
            DamageSystem.ApplyDamage(caster, target, damage, DamageType.Physical, HitType.Normal, GameMode.RTS);
            SpawnImpactVFX(target.transform.position);
        }

        private System.Collections.IEnumerator RushRoutine(
            GameObject caster, GameObject target, float damage)
        {
            Vector3 start = caster.transform.position;
            Vector3 end = target.transform.position
                - (target.transform.position - start).normalized * 1.0f; // stop 1m away

            float elapsed = 0f;
            var cc = caster.GetComponent<CharacterController>();

            while (elapsed < rushDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / rushDuration;
                if (cc != null)
                    cc.Move((end - caster.transform.position).normalized
                        * (rushDistance / rushDuration) * Time.deltaTime);
                yield return null;
            }

            DamageSystem.ApplyDamage(caster, target, damage, DamageType.Physical, HitType.Normal, GameMode.Action);
            SpawnImpactVFX(target.transform.position);
        }
    }

    // ── FORCE PUSH ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Force Push — telekinetic knockback.
    /// 
    /// Action Mode: Aim + timing-based interrupt. Targeted knockback.
    /// RTS Mode:    Auto-executed AOE pushback, lower knockback distance.
    /// </summary>
    public class ForcePush : AbilityBase
    {
        [Header("Force Push Config")]
        [SerializeField] private float actionKnockbackDistance = 8f;
        [SerializeField] private float rtsKnockbackDistance = 4f;
        [SerializeField] private float knockbackDuration = 0.4f;

        protected override void Awake()
        {
            base.Awake();
            abilityName = "Force Push";
            description = "Unleash the Force to push enemies back. AOE in RTS mode.";
            abilityType = AbilityType.Force;
            executionType = AbilityExecutionType.ModeVariant;
            baseDamage = 20f;
            cooldown = 6f;
            range = 15f;
            isAOE = false;  // Action: single target; RTS: AOE (handled in override)
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            if (target == null) return;

            // Apply damage
            DamageSystem.ApplyDamage(caster, target, damage, DamageType.Force, HitType.Normal, GameMode.Action);
            SpawnImpactVFX(target.transform.position);

            // Knockback
            ApplyKnockback(target, caster.transform.forward, actionKnockbackDistance);
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            // RTS: AOE around caster's facing
            Collider[] hits = Physics.OverlapSphere(caster.transform.position, range / 2f);
            foreach (var hit in hits)
            {
                if (hit.gameObject == caster) continue;
                if (IsEnemy(hit.gameObject))
                {
                    DamageSystem.ApplyDamage(caster, hit.gameObject, damage,
                        DamageType.Force, HitType.Normal, GameMode.RTS);
                    ApplyKnockback(hit.gameObject, caster.transform.forward, rtsKnockbackDistance);
                }
            }
            SpawnCastVFX(caster.transform.position);
        }

        private void ApplyKnockback(GameObject target, Vector3 direction, float distance)
        {
            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(direction * distance * 10f, ForceMode.Impulse);
        }

        private bool IsEnemy(GameObject go) =>
            go.CompareTag("Enemy") || go.GetComponent<AI.Enemy.EnemyAI>() != null;
    }

    // ── STASIS ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Force Stasis — root and slow target.
    /// Universal: behaves the same in both modes. Duration differs slightly.
    /// </summary>
    public class ForceStasis : AbilityBase
    {
        [Header("Stasis Config")]
        [SerializeField] private float actionStasisDuration = 3f;
        [SerializeField] private float rtsStasisDuration = 4f; // RTS bonus: longer duration

        protected override void Awake()
        {
            base.Awake();
            abilityName = "Force Stasis";
            description = "Freeze an enemy in place. Lasts longer in RTS mode.";
            abilityType = AbilityType.Force;
            executionType = AbilityExecutionType.Universal;
            baseDamage = 5f;
            cooldown = 12f;
            range = 20f;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            ApplyStasis(target, actionStasisDuration);
            DamageSystem.ApplyDamage(caster, target, damage, DamageType.Force, HitType.Normal, GameMode.Action);
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            ApplyStasis(target, rtsStasisDuration);
            DamageSystem.ApplyDamage(caster, target, damage, DamageType.Force, HitType.Normal, GameMode.RTS);
        }

        private void ApplyStasis(GameObject target, float duration)
        {
            if (target == null) return;
            var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) StartCoroutine(StasisRoutine(agent, duration));
        }

        private System.Collections.IEnumerator StasisRoutine(
            UnityEngine.AI.NavMeshAgent agent, float duration)
        {
            agent.isStopped = true;
            yield return new WaitForSeconds(duration);
            if (agent != null) agent.isStopped = false;
        }
    }

    // ── OVERWATCH PROTOCOL ────────────────────────────────────────────────────
    /// <summary>
    /// Overwatch Protocol — the signature hybrid ability.
    /// 
    /// Unlocked through the Combo Mastery system.
    /// Step 1 (RTS): Mark a target with an overwatch beacon.
    /// Step 2 (Action): First shot on the marked target gets a crit bonus.
    /// 
    /// Design doc: "Overwatch Protocol: Mark target in RTS, switch to Action
    ///              for crit bonus on first shot."
    /// </summary>
    public class OverwatchProtocol : AbilityBase
    {
        [Header("Overwatch Config")]
        [SerializeField] private float critDamageBonus = 0.5f; // +50% on first Action shot
        [SerializeField] private float markDuration = 30f;
        [SerializeField] private GameObject markVFXPrefab;

        private GameObject _markedTarget;
        private GameObject _activeMarkVFX;
        private bool _waitingForActionShot = false;

        protected override void Awake()
        {
            base.Awake();
            abilityName = "Overwatch Protocol";
            description = "Mark a target in RTS mode. Switch to Action for a guaranteed critical hit.";
            abilityType = AbilityType.Utility;
            executionType = AbilityExecutionType.ModeVariant;
            baseDamage = 0f;  // No direct damage — crit bonus applied to next shot
            cooldown = 20f;
            range = 50f;
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            if (target == null) return;

            _markedTarget = target;
            _waitingForActionShot = true;

            // Spawn mark VFX on target
            if (markVFXPrefab != null)
                _activeMarkVFX = Instantiate(markVFXPrefab, target.transform);

            StartCoroutine(MarkExpireRoutine());
            Debug.Log($"[OverwatchProtocol] Target marked: {target.name}. Switch to Action for crit!");
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            // Action mode execution not applicable — mark is set in RTS, consumed by weapon fire
            Debug.Log("[OverwatchProtocol] Must be used in RTS mode first to mark target.");
        }

        /// <summary>
        /// Check if the given target is currently marked with Overwatch.
        /// Called by WeaponBase before applying damage.
        /// </summary>
        public bool IsMarkedTarget(GameObject target) =>
            _waitingForActionShot && _markedTarget == target;

        /// <summary>Consume the mark and return the crit bonus multiplier.</summary>
        public float ConsumeMarkBonus()
        {
            if (!_waitingForActionShot) return 1f;
            _waitingForActionShot = false;
            if (_activeMarkVFX != null) Destroy(_activeMarkVFX);
            return 1f + critDamageBonus;
        }

        private System.Collections.IEnumerator MarkExpireRoutine()
        {
            yield return new WaitForSeconds(markDuration);
            if (_waitingForActionShot)
            {
                _waitingForActionShot = false;
                if (_activeMarkVFX != null) Destroy(_activeMarkVFX);
                Debug.Log("[OverwatchProtocol] Mark expired.");
            }
        }
    }
}
