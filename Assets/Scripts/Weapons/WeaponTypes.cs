using UnityEngine;
using KotORUnity.Weapons;
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
}
