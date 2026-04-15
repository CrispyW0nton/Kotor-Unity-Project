using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Combat;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Weapons
{
    /// <summary>
    /// Base class for all weapons in the hybrid combat system.
    /// 
    /// Each weapon has TWO damage stats per the design doc:
    ///   - Damage_Action: Per-shot damage requiring player aim (hitscan)
    ///   - DPS_RTS: Averaged damage output when AI-controlled
    /// 
    /// Weapon damage scales with tier: damage = base × tier^1.15
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Weapon Identity")]
        [SerializeField] protected string weaponName = "Blaster Rifle";
        [SerializeField] protected WeaponType weaponType = WeaponType.BlasterRifle;
        [SerializeField] protected int tier = 1;

        [Header("Dual-Stat Damage")]
        [SerializeField] protected float baseDamageAction = 15f;
        [SerializeField] protected float baseDPSRTS = 10f;

        [Header("Ammo")]
        [SerializeField] protected int maxAmmo = 100;
        [SerializeField] protected int currentAmmo = 100;
        [SerializeField] protected float reloadTime = 2.0f;

        [Header("Fire Rate")]
        [SerializeField] protected float fireRate = 0.2f;  // seconds between shots

        [Header("Range & Accuracy")]
        [SerializeField] protected float range = 50f;
        [SerializeField] protected float accuracyAction = 0.85f;  // 0..1, modifies ADS spread
        [SerializeField] protected float spreadAngle = 2f;        // degrees

        [Header("Audio / VFX")]
        [SerializeField] protected AudioClip fireSound;
        [SerializeField] protected AudioClip reloadSound;
        [SerializeField] protected GameObject muzzleFlashPrefab;
        [SerializeField] protected GameObject hitVFXPrefab;
        [SerializeField] protected Transform muzzlePoint;

        // ── STATE ──────────────────────────────────────────────────────────────
        protected float _nextFireTime = 0f;
        protected bool _isReloading = false;
        protected PlayerStats _ownerStats;
        protected AudioSource _audioSource;

        // ── COMPUTED STATS (Tier scaling) ──────────────────────────────────────
        /// <summary>Tier-scaled Action damage. damage = base × tier^1.15</summary>
        public float DamageAction => baseDamageAction
            * Mathf.Pow(tier, GameConstants.WEAPON_TIER_EXPONENT);

        /// <summary>Tier-scaled RTS DPS. dps = base × tier^1.15</summary>
        public float DPSRTS => baseDPSRTS
            * Mathf.Pow(tier, GameConstants.WEAPON_TIER_EXPONENT);

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public string WeaponName => weaponName;
        public WeaponType WeaponCategory => weaponType;   // renamed: avoids CS0102 clash with the WeaponType enum
        public int Tier => tier;
        public int CurrentAmmo => currentAmmo;
        public int MaxAmmo => maxAmmo;
        public bool HasAmmo => currentAmmo > 0;
        public bool IsReloading => _isReloading;
        public bool IsReady => Time.time >= _nextFireTime && !_isReloading && HasAmmo;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        protected virtual void Awake()
        {
            _audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            _ownerStats = GetComponentInParent<PlayerStatsBehaviour>()?.Stats;
        }

        // ── ABSTRACT / VIRTUAL INTERFACE ──────────────────────────────────────
        /// <summary>Fire this weapon from the given transform. isAiming affects accuracy.</summary>
        public virtual void Fire(Transform fireOrigin, bool isAiming)
        {
            if (!IsReady) return;

            _nextFireTime = Time.time + fireRate;
            currentAmmo--;

            // Muzzle flash
            if (muzzleFlashPrefab != null && muzzlePoint != null)
                Instantiate(muzzleFlashPrefab, muzzlePoint.position, muzzlePoint.rotation);

            // Audio
            if (fireSound != null)
                _audioSource.PlayOneShot(fireSound);

            // Execute mode-specific fire logic
            ExecuteFire(fireOrigin, isAiming);
        }

        protected abstract void ExecuteFire(Transform fireOrigin, bool isAiming);

        /// <summary>Perform RTS auto-attack tick (called per frame in RTS mode).</summary>
        public virtual void RTSAttackTick(GameObject target, float deltaTime)
        {
            if (!IsReady || target == null || _ownerStats == null) return;

            var targetStats = target.GetComponent<PlayerStatsBehaviour>()?.Stats;
            if (targetStats == null) return;

            var (isHit, damage, hitType, hitChance) = CombatResolver.ResolveRTSAttack(
                DPSRTS, deltaTime, _ownerStats, targetStats);

            if (isHit)
                DamageSystem.ApplyDamage(gameObject, target, damage, DamageType.Energy, hitType, GameMode.RTS);

            _nextFireTime = Time.time + fireRate;
        }

        // ── RELOAD ─────────────────────────────────────────────────────────────
        public void StartReload()
        {
            if (_isReloading || currentAmmo == maxAmmo) return;
            StartCoroutine(ReloadRoutine());
        }

        protected virtual System.Collections.IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            if (reloadSound != null) _audioSource.PlayOneShot(reloadSound);
            yield return new WaitForSeconds(reloadTime);
            currentAmmo = maxAmmo;
            _isReloading = false;
        }

        // ── HITSCAN UTILITY ────────────────────────────────────────────────────
        protected bool HitscanFire(Transform origin, bool isAiming, out RaycastHit hit, out float damage, out HitType hitType)
        {
            damage = 0f;
            hitType = HitType.Miss;

            // Apply spread
            float spread = isAiming ? spreadAngle * 0.25f : spreadAngle;
            Vector3 direction = origin.forward;
            direction += new Vector3(
                UnityEngine.Random.Range(-spread, spread),
                UnityEngine.Random.Range(-spread, spread),
                0f) * 0.01f;
            direction.Normalize();

            if (Physics.Raycast(origin.position, direction, out hit, range))
            {
                var result = CombatResolver.ResolveActionHit(DamageAction, hit, _ownerStats);
                damage = result.damage;
                hitType = result.hitType;

                // Spawn hit VFX
                if (hitVFXPrefab != null)
                    Instantiate(hitVFXPrefab, hit.point, Quaternion.LookRotation(hit.normal));

                DamageSystem.ApplyDamage(gameObject, hit.collider.gameObject,
                    damage, DamageType.Energy, hitType, GameMode.Action);

                return true;
            }
            return false;
        }
    }
}
