using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Combat;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

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

    // ══════════════════════════════════════════════════════════════════════════
    //  GRENADES
    // ══════════════════════════════════════════════════════════════════════════

    // ── FRAG GRENADE ───────────────────────────────────────────────────────────
    /// <summary>
    /// Frag Grenade — thrown AOE explosive.
    /// Action: player aims arc trajectory; RTS: auto-lobbed at target position.
    /// Matches KotOR1 g_i_grenade001 behaviour.
    /// </summary>
    public class FragGrenade : AbilityBase
    {
        [Header("Frag Grenade Config")]
        [SerializeField] private float explosionRadius = 4f;
        [SerializeField] private float fuseTime = 1.5f;
        [SerializeField] private GameObject explosionVFXPrefab;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Frag Grenade";
            description   = "Throw a fragmentation grenade that explodes on impact.";
            abilityType   = AbilityType.Grenade;
            executionType = AbilityExecutionType.ModeVariant;
            baseDamage    = 55f;
            cooldown      = 4f;
            range         = 25f;
            isAOE         = true;
            aoeRadius     = explosionRadius;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            Vector3 landPos = target != null
                ? target.transform.position
                : caster.transform.position + caster.transform.forward * range * 0.6f;
            StartCoroutine(LobGrenade(caster, landPos, damage));
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            Vector3 landPos = target != null
                ? target.transform.position
                : caster.transform.position + caster.transform.forward * range * 0.5f;
            StartCoroutine(LobGrenade(caster, landPos, damage * 0.8f));
        }

        private System.Collections.IEnumerator LobGrenade(
            GameObject caster, Vector3 landPos, float damage)
        {
            yield return new WaitForSeconds(fuseTime);

            if (explosionVFXPrefab != null)
                Instantiate(explosionVFXPrefab, landPos, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(landPos, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == caster) continue;
                // Falloff: full damage at centre, 50% at edge
                float dist    = Vector3.Distance(hit.transform.position, landPos);
                float falloff = Mathf.Lerp(1f, 0.5f, dist / explosionRadius);
                DamageSystem.ApplyDamage(caster, hit.gameObject,
                    damage * falloff, DamageType.Physical, HitType.Normal,
                    GameMode.Action);
            }
        }
    }

    // ── CONCUSSION GRENADE ────────────────────────────────────────────────────
    /// <summary>
    /// Concussion Grenade — stuns enemies in AOE; no lethal damage.
    /// Corresponds to KotOR1 g_i_grenade004.
    /// </summary>
    public class ConcussionGrenade : AbilityBase
    {
        [Header("Concussion Config")]
        [SerializeField] private float stunDuration = 4f;
        [SerializeField] private float explosionRadius = 5f;
        [SerializeField] private GameObject stunVFXPrefab;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Concussion Grenade";
            description   = "Stun enemies in an area. No lethal damage.";
            abilityType   = AbilityType.Grenade;
            executionType = AbilityExecutionType.Universal;
            baseDamage    = 5f;
            cooldown      = 6f;
            range         = 22f;
            isAOE         = true;
            aoeRadius     = explosionRadius;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
            => ApplyConcussion(caster, target);

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
            => ApplyConcussion(caster, target);

        private void ApplyConcussion(GameObject caster, GameObject target)
        {
            Vector3 centre = target != null ? target.transform.position : caster.transform.position;

            if (stunVFXPrefab != null)
                Instantiate(stunVFXPrefab, centre, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(centre, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == caster) continue;
                var enemy = hit.GetComponent<AI.Enemy.EnemyAI>();
                enemy?.Stun(stunDuration);

                // Minor impact damage
                DamageSystem.ApplyDamage(caster, hit.gameObject,
                    baseDamage, DamageType.Physical, HitType.Normal, GameMode.Action);
            }
        }
    }

    // ── PLASMA GRENADE ────────────────────────────────────────────────────────
    /// <summary>
    /// Plasma Grenade — fire-type DoT grenade (KotOR1 g_i_grenade003).
    /// Applies burn damage over 3 ticks.
    /// </summary>
    public class PlasmaGrenade : AbilityBase
    {
        [Header("Plasma Config")]
        [SerializeField] private float burnDuration  = 3f;
        [SerializeField] private float burnInterval  = 1f;
        [SerializeField] private float explosionRadius = 3.5f;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Plasma Grenade";
            description   = "Burns enemies in an area for fire damage over time.";
            abilityType   = AbilityType.Grenade;
            executionType = AbilityExecutionType.Universal;
            baseDamage    = 30f;
            cooldown      = 5f;
            range         = 20f;
            isAOE         = true;
            aoeRadius     = explosionRadius;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
            => StartCoroutine(BurnArea(caster, target?.transform.position
                ?? caster.transform.position + caster.transform.forward * 5f, damage));

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
            => StartCoroutine(BurnArea(caster, target?.transform.position
                ?? caster.transform.position + caster.transform.forward * 5f, damage * 0.8f));

        private System.Collections.IEnumerator BurnArea(
            GameObject caster, Vector3 centre, float damage)
        {
            SpawnCastVFX(centre);
            float elapsed = 0f;
            while (elapsed < burnDuration)
            {
                yield return new WaitForSeconds(burnInterval);
                elapsed += burnInterval;
                Collider[] hits = Physics.OverlapSphere(centre, explosionRadius);
                foreach (var hit in hits)
                {
                    if (hit.gameObject == caster) continue;
                    DamageSystem.ApplyDamage(caster, hit.gameObject,
                        damage / (burnDuration / burnInterval),
                        DamageType.Energy, HitType.Normal, GameMode.Action);
                }
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CONSUMABLES
    // ══════════════════════════════════════════════════════════════════════════

    // ── MEDPAC ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Medpac — instant heal consumable (KotOR1 g_i_medeqpmnt01–03).
    /// Action: full heal; RTS: heals self automatically; both consume one charge.
    /// </summary>
    public class MedPac : AbilityBase
    {
        [Header("MedPac Config")]
        [SerializeField] private float healAmount = 50f;
        [SerializeField] private MedPacGrade grade = MedPacGrade.Standard;

        public enum MedPacGrade { Basic = 1, Standard = 2, Advanced = 3 }

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Medpac";
            abilityType   = AbilityType.Consumable;
            executionType = AbilityExecutionType.Universal;
            cooldown      = 0f;  // limited by inventory charges
            range         = 0f;  // self-only in this implementation

            float gradeMultiplier = (float)grade;
            healAmount  = 25f * gradeMultiplier;
            baseDamage  = 0f;
            description = $"{grade} Medpac — restores {healAmount} HP.";
        }

        public override void Execute(GameObject caster, GameObject target, GameMode mode)
        {
            if (!IsReady) return;

            // Determine target: if null or self-use, heal caster
            GameObject healTarget = (target != null && target != caster) ? target : caster;

            var stats = healTarget.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats == null) return;

            float actual = Mathf.Min(healAmount, stats.MaxHealth - stats.CurrentHealth);
            stats.Heal(healAmount);

            SpawnCastVFX(healTarget.transform.position);
            if (castSound != null) _audioSource.PlayOneShot(castSound);

            Debug.Log($"[MedPac] Healed {healTarget.name} for {actual} HP ({grade}).");

            EventBus.Publish(EventBus.EventType.AbilityUsed,
                new EventBus.AbilityEventArgs(caster, healTarget, abilityName, mode));

            _cooldownRemaining = cooldown;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
    }

    // ── STIMULANT ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Stimulant — temporary combat stat boost (KotOR1 g_i_cmbtshot01–03).
    /// Grants +attack / +defence / +damage for a short duration.
    /// </summary>
    public class Stimulant : AbilityBase
    {
        [Header("Stimulant Config")]
        [SerializeField] private float duration       = 30f;
        [SerializeField] private float attackBonus    = 3f;
        [SerializeField] private float defenceBonus   = 2f;
        [SerializeField] private float damageBonus    = 5f;
        [SerializeField] private StimulantGrade grade = StimulantGrade.Standard;

        public enum StimulantGrade { Basic = 1, Standard = 2, Advanced = 3 }

        private bool _active = false;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Stimulant";
            abilityType   = AbilityType.Consumable;
            executionType = AbilityExecutionType.Universal;
            cooldown      = 0f;
            baseDamage    = 0f;

            float g       = (float)grade;
            attackBonus   = 2f * g;
            defenceBonus  = 1f * g;
            damageBonus   = 3f * g;
            duration      = 20f + 10f * g;
            description   = $"{grade} Stimulant — +{attackBonus} ATK, +{defenceBonus} DEF, +{damageBonus} DMG for {duration}s.";
        }

        public override void Execute(GameObject caster, GameObject target, GameMode mode)
        {
            if (!IsReady || _active) return;

            var stats = caster.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats == null) return;

            StartCoroutine(StimRoutine(stats));
            SpawnCastVFX(caster.transform.position);
            if (castSound != null) _audioSource.PlayOneShot(castSound);

            Debug.Log($"[Stimulant] {grade} activated on {caster.name} for {duration}s.");
            _cooldownRemaining = cooldown;
        }

        private System.Collections.IEnumerator StimRoutine(PlayerStats stats)
        {
            _active = true;
            stats.AddTemporaryAttackBonus((int)attackBonus, duration);
            yield return new WaitForSeconds(duration);
            _active = false;
            Debug.Log("[Stimulant] Effect expired.");
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
    }

    // ── SHIELD CHARGE ─────────────────────────────────────────────────────────
    /// <summary>
    /// Energy Shield — deploys a personal energy shield (KotOR1 g_i_frarmbnds01–03).
    /// Absorbs incoming damage up to a cap before breaking.
    /// </summary>
    public class EnergyShield : AbilityBase
    {
        [Header("Shield Config")]
        [SerializeField] private float shieldAmount  = 80f;
        [SerializeField] private float shieldDuration = 60f;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Energy Shield";
            description   = $"Deploy a personal energy shield absorbing {shieldAmount} damage.";
            abilityType   = AbilityType.Consumable;
            executionType = AbilityExecutionType.Universal;
            baseDamage    = 0f;
            cooldown      = 1f;
        }

        public override void Execute(GameObject caster, GameObject target, GameMode mode)
        {
            if (!IsReady) return;

            var stats = caster.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (stats == null) return;

            stats.RestoreShield(shieldAmount);
            SpawnCastVFX(caster.transform.position);
            if (castSound != null) _audioSource.PlayOneShot(castSound);

            Debug.Log($"[EnergyShield] Shield +{shieldAmount} applied to {caster.name}.");
            _cooldownRemaining = cooldown;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage) { }
    }

    // ── ADHESIVE GRENADE ──────────────────────────────────────────────────────
    /// <summary>
    /// Adhesive Grenade — slows enemy movement speed significantly (KotOR1 g_i_grenade005).
    /// Reduces NavMesh agent speed to 20% for duration.
    /// </summary>
    public class AdhesiveGrenade : AbilityBase
    {
        [Header("Adhesive Config")]
        [SerializeField] private float slowDuration   = 5f;
        [SerializeField] private float speedReduction = 0.8f;  // 80% speed reduction
        [SerializeField] private float explosionRadius = 4f;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Adhesive Grenade";
            description   = "Slows enemies in area — reduces movement to 20% for 5 seconds.";
            abilityType   = AbilityType.Grenade;
            executionType = AbilityExecutionType.Universal;
            baseDamage    = 10f;
            cooldown      = 8f;
            range         = 18f;
            isAOE         = true;
            aoeRadius     = explosionRadius;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
            => ApplySlow(caster, target?.transform.position ?? caster.transform.position, damage);

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
            => ApplySlow(caster, target?.transform.position ?? caster.transform.position, damage);

        private void ApplySlow(GameObject caster, Vector3 centre, float damage)
        {
            SpawnCastVFX(centre);
            Collider[] hits = Physics.OverlapSphere(centre, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == caster) continue;
                DamageSystem.ApplyDamage(caster, hit.gameObject,
                    damage, DamageType.Physical, HitType.Normal, GameMode.Action);
                var nav = hit.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (nav != null) StartCoroutine(SlowRoutine(nav));
            }
        }

        private System.Collections.IEnumerator SlowRoutine(UnityEngine.AI.NavMeshAgent nav)
        {
            float original = nav.speed;
            nav.speed = original * (1f - speedReduction);
            yield return new WaitForSeconds(slowDuration);
            if (nav != null) nav.speed = original;
        }
    }

    // ── THERMAL DETONATOR ─────────────────────────────────────────────────────
    /// <summary>
    /// Thermal Detonator — high-damage delayed explosive (KotOR1 g_i_grenade002).
    /// Largest blast radius, highest damage, longer fuse.
    /// </summary>
    public class ThermalDetonator : AbilityBase
    {
        [Header("Thermal Detonator Config")]
        [SerializeField] private float fuseTime       = 2.5f;
        [SerializeField] private float explosionRadius = 7f;
        [SerializeField] private GameObject explosionVFXPrefab;

        protected override void Awake()
        {
            base.Awake();
            abilityName   = "Thermal Detonator";
            description   = "High-yield explosive with large blast radius. Destroys almost everything nearby.";
            abilityType   = AbilityType.Grenade;
            executionType = AbilityExecutionType.ModeVariant;
            baseDamage    = 100f;
            cooldown      = 10f;
            range         = 20f;
            isAOE         = true;
            aoeRadius     = explosionRadius;
        }

        protected override void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
        {
            Vector3 pos = target?.transform.position
                ?? caster.transform.position + caster.transform.forward * range * 0.7f;
            StartCoroutine(Detonate(caster, pos, damage));
        }

        protected override void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats cs, float damage)
        {
            Vector3 pos = target?.transform.position
                ?? caster.transform.position + caster.transform.forward * range * 0.5f;
            StartCoroutine(Detonate(caster, pos, damage * 0.8f));
        }

        private System.Collections.IEnumerator Detonate(
            GameObject caster, Vector3 centre, float damage)
        {
            Debug.Log($"[ThermalDetonator] Armed — detonating in {fuseTime}s...");
            yield return new WaitForSeconds(fuseTime);

            if (explosionVFXPrefab != null)
                Instantiate(explosionVFXPrefab, centre, Quaternion.identity);

            Collider[] hits = Physics.OverlapSphere(centre, explosionRadius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == caster) continue;
                float dist    = Vector3.Distance(hit.transform.position, centre);
                float falloff = Mathf.Lerp(1f, 0.3f, dist / explosionRadius);
                DamageSystem.ApplyDamage(caster, hit.gameObject,
                    damage * falloff, DamageType.Physical, HitType.Normal, GameMode.Action);
            }

            Debug.Log($"[ThermalDetonator] Detonated — {hits.Length} targets in blast.");
        }
    }
}
