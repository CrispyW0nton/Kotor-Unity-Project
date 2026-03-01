using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Combat;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Abilities
{
    /// <summary>
    /// Base class for all abilities in the hybrid system.
    /// 
    /// Key design principle: Mode-Variant Execution (from design doc)
    ///   - Action mode: requires directional input + timing → 100% damage if perfect
    ///   - RTS mode: auto-executes on target → 80% damage, guaranteed hit
    /// 
    /// Abilities support three execution types:
    ///   Universal    → Same behavior in both modes
    ///   SkillBased   → Action requires aim/timing; RTS uses auto-execute
    ///   ModeVariant  → Distinct effects in each mode
    /// </summary>
    public abstract class AbilityBase : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Ability Identity")]
        [SerializeField] protected string abilityName = "Ability";
        [SerializeField] protected string description = "";
        [SerializeField] protected AbilityType abilityType = AbilityType.Ranged;
        [SerializeField] protected AbilityExecutionType executionType = AbilityExecutionType.ModeVariant;
        [SerializeField] protected Sprite icon;

        [Header("Stats")]
        [SerializeField] protected float baseDamage = 30f;
        [SerializeField] protected float cooldown = 8f;
        [SerializeField] protected float range = 20f;
        [SerializeField] protected float staminaCost = 25f;

        [Header("Area of Effect")]
        [SerializeField] protected bool isAOE = false;
        [SerializeField] protected float aoeRadius = 5f;

        [Header("VFX")]
        [SerializeField] protected GameObject castVFXPrefab;
        [SerializeField] protected GameObject impactVFXPrefab;
        [SerializeField] protected AudioClip castSound;

        // ── STATE ──────────────────────────────────────────────────────────────
        protected float _cooldownRemaining = 0f;
        protected AudioSource _audioSource;

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public string AbilityName => abilityName;
        public string Description => description;
        public AbilityType AbilityType => abilityType;
        public float Cooldown => cooldown;
        public float CooldownRemaining => _cooldownRemaining;
        public float CooldownProgress => cooldown > 0 ? (1f - _cooldownRemaining / cooldown) : 1f;
        public bool IsReady => _cooldownRemaining <= 0f;
        public Sprite Icon => icon;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        protected virtual void Awake()
        {
            _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        }

        protected virtual void Update()
        {
            if (_cooldownRemaining > 0f)
                _cooldownRemaining -= Time.deltaTime;
        }

        // ── EXECUTION ─────────────────────────────────────────────────────────
        /// <summary>
        /// Execute this ability.
        /// Action mode may require additional player input (subclass handles this).
        /// RTS mode auto-executes with reduced damage.
        /// </summary>
        public virtual void Execute(GameObject caster, GameObject target, GameMode mode)
        {
            if (!IsReady) return;

            var casterStats = caster?.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (casterStats == null) return;

            // Calculate damage based on mode
            float damage = CombatResolver.ResolveAbilityDamage(baseDamage, mode, casterStats);

            // Apply ability effects
            if (mode == GameMode.Action)
                ExecuteActionMode(caster, target, casterStats, damage);
            else
                ExecuteRTSMode(caster, target, casterStats, damage);

            // Start cooldown
            _cooldownRemaining = cooldown;

            // VFX and audio
            SpawnCastVFX(caster.transform.position);
            if (castSound != null) _audioSource.PlayOneShot(castSound);

            EventBus.Publish(EventBus.EventType.AbilityUsed);
        }

        // ── MODE-SPECIFIC EXECUTION ────────────────────────────────────────────
        /// <summary>
        /// Action mode: full damage, may require aim/timing in subclass.
        /// Override to add skill-based mechanics.
        /// </summary>
        protected virtual void ExecuteActionMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            if (isAOE)
                DamageSystem.ApplyAOE(caster, caster.transform.position, aoeRadius, damage,
                    DamageType.Energy, GameMode.Action);
            else if (target != null)
                DamageSystem.ApplyDamage(caster, target, damage,
                    DamageType.Energy, HitType.Normal, GameMode.Action);
        }

        /// <summary>
        /// RTS mode: auto-executed, 80% damage, guaranteed hit.
        /// Override to add RTS-specific behavior (e.g., AOE patterns).
        /// </summary>
        protected virtual void ExecuteRTSMode(
            GameObject caster, GameObject target, PlayerStats casterStats, float damage)
        {
            // RTS always hits (within range check)
            if (target == null) return;

            float distance = Vector3.Distance(caster.transform.position, target.transform.position);
            if (distance > range) return;

            if (isAOE)
                DamageSystem.ApplyAOE(caster, target.transform.position, aoeRadius, damage,
                    DamageType.Energy, GameMode.RTS);
            else
                DamageSystem.ApplyDamage(caster, target, damage,
                    DamageType.Energy, HitType.Normal, GameMode.RTS);
        }

        // ── VFX HELPERS ────────────────────────────────────────────────────────
        protected void SpawnCastVFX(Vector3 position)
        {
            if (castVFXPrefab != null)
                Instantiate(castVFXPrefab, position, Quaternion.identity);
        }

        protected void SpawnImpactVFX(Vector3 position)
        {
            if (impactVFXPrefab != null)
                Instantiate(impactVFXPrefab, position, Quaternion.identity);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public void ResetCooldown() => _cooldownRemaining = 0f;
        public void SetCooldownRemaining(float remaining) => _cooldownRemaining = Mathf.Max(0f, remaining);
    }
}
