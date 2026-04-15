using UnityEngine;
using KotORUnity.Weapons;
using KotORUnity.Combat;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Weapons
{
    /// <summary>
    /// Blaster Rifle — the standard assault weapon.
    /// Balanced DPS_RTS and Damage_Action. Versatile across both modes.
    /// </summary>
    public class BlasterRifle : WeaponBase
    {
        protected override void Awake()
        {
            base.Awake();
            weaponName = "Blaster Rifle";
            weaponType = WeaponType.BlasterRifle;
            baseDamageAction = 15f;
            baseDPSRTS = 10f;
            fireRate = 0.18f;
            range = 60f;
            spreadAngle = 2.0f;
            maxAmmo = 90;
            currentAmmo = 90;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            HitscanFire(fireOrigin, isAiming, out _, out _, out _);
        }
    }

    /// <summary>
    /// Blaster Pistol — compact sidearm.
    /// Good DPS_RTS for its size; moderate Action damage.
    /// Ideal for close-to-medium range in both modes.
    /// </summary>
    public class BlasterPistol : WeaponBase
    {
        protected override void Awake()
        {
            base.Awake();
            weaponName = "Blaster Pistol";
            weaponType = WeaponType.BlasterPistol;
            baseDamageAction = 12f;
            baseDPSRTS = 8.5f;
            fireRate = 0.22f;
            range = 40f;
            spreadAngle = 3.0f;
            maxAmmo = 50;
            currentAmmo = 50;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            HitscanFire(fireOrigin, isAiming, out _, out _, out _);
        }
    }

    /// <summary>
    /// Heavy Blaster Carbine — high-damage, slower fire.
    /// </summary>
    public class HeavyBlaster : WeaponBase
    {
        protected override void Awake()
        {
            base.Awake();
            weaponName = "Heavy Blaster";
            weaponType = WeaponType.HeavyBlaster;
            baseDamageAction = 25f;
            baseDPSRTS = 14f;
            fireRate = 0.45f;
            range = 55f;
            spreadAngle = 2.5f;
            maxAmmo = 30;
            currentAmmo = 30;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            HitscanFire(fireOrigin, isAiming, out _, out _, out _);
        }
    }

    /// <summary>
    /// Shotgun — devastating at close range in Action mode.
    /// Very low DPS_RTS (AI cannot use effectively at range).
    /// Design doc: "AI poor at range control, devastating up-close"
    /// </summary>
    public class Shotgun : WeaponBase
    {
        [SerializeField] private int pelletsPerShot = 8;
        [SerializeField] private float pelletSpread = 12f;

        protected override void Awake()
        {
            base.Awake();
            weaponName = "Scattergun";
            weaponType = WeaponType.Shotgun;
            baseDamageAction = 8f;  // per pellet — 8 pellets = 64 base
            baseDPSRTS = 6f;        // Low: AI poor at range
            fireRate = 0.85f;
            range = 20f;
            spreadAngle = pelletSpread;
            maxAmmo = 16;
            currentAmmo = 16;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            // Fire multiple pellets
            for (int i = 0; i < pelletsPerShot; i++)
                HitscanFire(fireOrigin, isAiming, out _, out _, out _);
        }
    }

    /// <summary>
    /// Sniper Rifle — high single-shot Action damage, very low DPS_RTS.
    /// Design doc: "Low DPS_RTS (slow fire rate), High Damage_Action (rewards headshots)"
    /// </summary>
    public class SniperRifle : WeaponBase
    {
        protected override void Awake()
        {
            base.Awake();
            weaponName = "Sniper Rifle";
            weaponType = WeaponType.SniperRifle;
            baseDamageAction = 60f;   // Very high — rewards headshots
            baseDPSRTS = 5f;          // Very low — slow fire rate in AI hands
            fireRate = 1.8f;
            range = 150f;
            spreadAngle = 0.3f;       // Very accurate
            maxAmmo = 10;
            currentAmmo = 10;
            accuracyAction = 0.99f;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            HitscanFire(fireOrigin, isAiming, out _, out _, out _);
        }
    }

    /// <summary>
    /// Vibroblade — melee weapon.
    /// High DPS_RTS in AI hands (continuous strikes).
    /// Action mode requires timing-based parry/strike combos.
    /// </summary>
    public class Vibroblade : WeaponBase
    {
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeArc = 90f;

        protected override void Awake()
        {
            base.Awake();
            weaponName = "Vibroblade";
            weaponType = WeaponType.Vibroblade;
            baseDamageAction = 20f;  // Per swing — timing bonus applies
            baseDPSRTS = 16f;        // High: AI continuous strikes
            fireRate = 0.5f;
            range = meleeRange;
            maxAmmo = int.MaxValue;  // No ammo
            currentAmmo = int.MaxValue;
        }

        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            // Melee arc sweep
            Collider[] hits = Physics.OverlapSphere(fireOrigin.position, meleeRange);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject) continue;

                Vector3 dirToTarget = (hit.transform.position - fireOrigin.position).normalized;
                float angle = Vector3.Angle(fireOrigin.forward, dirToTarget);

                if (angle <= meleeArc / 2f)
                {
                    HitscanFire(fireOrigin, isAiming, out _, out _, out _);
                }
            }
        }
    }

    // ── LIGHTSABER ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Lightsaber — the iconic Jedi/Sith melee weapon.
    ///
    /// Action Mode:
    ///   Swing: arc-sweep OverlapSphere dealing high melee damage.
    ///   Block:  When IsBlocking is true, incoming hitscan rays that hit the
    ///           lightsaber collider are reflected back at the shooter.
    ///
    /// RTS Mode:
    ///   Highest DPS_RTS of any melee weapon (AI chains strikes continuously).
    ///   Automatically enters block stance when no enemies are in swing range,
    ///   reducing incoming ranged damage by 50%.
    ///
    /// Design doc note:
    ///   "High DPS_RTS — AI continuous strikes; Action Mode: timing-based parry
    ///    combo grants full damage on perfect parry, 50% on late parry."
    /// </summary>
    public class Lightsaber : WeaponBase
    {
        [Header("Lightsaber Config")]
        [SerializeField] private float meleeRange = 2.5f;
        [SerializeField] private float meleeArc = 120f;           // wider arc than Vibroblade
        [SerializeField] private float blockDamageReduction = 0.50f; // 50% damage reduction while blocking
        [SerializeField] private float perfectParryWindow = 0.15f;   // seconds to get perfect timing
        [SerializeField] private GameObject bladeObject;             // The blade mesh (for enable/disable)
        [SerializeField] private AudioClip igniteSound;
        [SerializeField] private AudioClip humLoop;
        [SerializeField] private AudioClip clashSound;

        // ── STATE ──────────────────────────────────────────────────────────────
        private bool _isBlocking = false;
        private bool _isPerfectParryWindow = false;
        private float _parryWindowTimer = 0f;
        private AudioSource _humSource;

        // ── AWAKE ──────────────────────────────────────────────────────────────
        protected override void Awake()
        {
            base.Awake();
            weaponName = "Lightsaber";
            weaponType = WeaponType.Lightsaber;
            baseDamageAction = 30f;   // Per swing
            baseDPSRTS = 22f;         // Highest melee DPS_RTS
            fireRate = 0.4f;
            range = meleeRange;
            maxAmmo = int.MaxValue;
            currentAmmo = int.MaxValue;

            // Set up hum audio source
            _humSource = gameObject.AddComponent<AudioSource>();
            _humSource.clip = humLoop;
            _humSource.loop = true;
            _humSource.volume = 0.3f;
            _humSource.spatialBlend = 1f;
        }

        private void OnEnable()
        {
            if (igniteSound != null) _audioSource?.PlayOneShot(igniteSound);
            if (humLoop != null) _humSource?.Play();
        }

        private void OnDisable()
        {
            _humSource?.Stop();
        }

        // ── FIRE (SWING) ───────────────────────────────────────────────────────
        protected override void ExecuteFire(Transform fireOrigin, bool isAiming)
        {
            // Arc sweep — hit all enemies within meleeArc
            Collider[] hits = Physics.OverlapSphere(fireOrigin.position, meleeRange);
            foreach (var hit in hits)
            {
                if (hit.gameObject == gameObject || hit.gameObject == fireOrigin.gameObject)
                    continue;

                Vector3 dirToTarget = (hit.transform.position - fireOrigin.position).normalized;
                float angle = Vector3.Angle(fireOrigin.forward, dirToTarget);

                if (angle <= meleeArc / 2f)
                {
                    // Perfect parry reward — does not apply to attacks, only to timing
                    // Clash detection (lightsaber vs lightsaber) plays clash sound
                    var targetWeapon = hit.GetComponent<Lightsaber>();
                    if (targetWeapon != null && targetWeapon.IsBlocking)
                    {
                        if (clashSound != null) _audioSource.PlayOneShot(clashSound);
                        continue; // Blocked
                    }

                    HitscanFire(fireOrigin, isAiming, out _, out _, out _);
                }
            }
        }

        // ── BLOCK ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Enter block stance. While blocking, incoming damage is reduced by 50%.
        /// Perfect parry window opens for perfectParryWindow seconds after block starts.
        /// </summary>
        public void StartBlock()
        {
            _isBlocking = true;
            _isPerfectParryWindow = true;
            _parryWindowTimer = perfectParryWindow;
        }

        /// <summary>End block stance.</summary>
        public void EndBlock()
        {
            _isBlocking = false;
            _isPerfectParryWindow = false;
        }

        private void Update()
        {
            if (_isPerfectParryWindow)
            {
                _parryWindowTimer -= Time.deltaTime;
                if (_parryWindowTimer <= 0f)
                    _isPerfectParryWindow = false;
            }
        }

        // ── INCOMING DAMAGE MODIFIER ───────────────────────────────────────────
        /// <summary>
        /// Returns a damage multiplier to apply to incoming damage.
        /// Called by DamageSystem when this character is the target.
        ///   Perfect Parry: 0% damage (full block) + riposte bonus
        ///   Normal Block:  50% damage
        ///   Not Blocking:  100% damage
        /// </summary>
        public float GetIncomingDamageMultiplier(out bool isPerfectParry)
        {
            isPerfectParry = false;
            if (!_isBlocking) return 1f;
            if (_isPerfectParryWindow)
            {
                isPerfectParry = true;
                return 0f; // Full block — zero damage
            }
            return 1f - blockDamageReduction;
        }

        // ── RTS AUTO-ATTACK OVERRIDE ───────────────────────────────────────────
        public override void RTSAttackTick(GameObject target, float deltaTime)
        {
            if (!IsReady || target == null || _ownerStats == null) return;

            var targetStats = target.GetComponent<Player.PlayerStatsBehaviour>()?.Stats;
            if (targetStats == null) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);

            // Auto-block if no enemy in range
            if (dist > meleeRange)
            {
                _isBlocking = true;
                // Close the gap
                var agent = GetComponentInParent<UnityEngine.AI.NavMeshAgent>();
                agent?.SetDestination(target.transform.position);
                return;
            }

            _isBlocking = false;

            var (isHit, damage, hitType, _) = CombatResolver.ResolveRTSAttack(
                DPSRTS, deltaTime, _ownerStats, targetStats);

            if (isHit)
                DamageSystem.ApplyDamage(gameObject, target, damage, DamageType.Physical, hitType, GameMode.RTS);

            _nextFireTime = Time.time + fireRate;
        }

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public bool IsBlocking => _isBlocking;
        public bool IsPerfectParryWindow => _isPerfectParryWindow;
    }
}
