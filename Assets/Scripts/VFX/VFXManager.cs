using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.VFX
{
    /// <summary>
    /// Central VFX manager with GameObject object pooling.
    ///
    /// Each VFXType maintains a pool of pre-warmed instances. On Spawn()
    /// an inactive instance is retrieved and activated; on lifetime expiry
    /// it is returned to the pool rather than destroyed.
    ///
    /// Force Power VFX hooks subscribe to EventBus.AbilityUsed so effects
    /// fire automatically when abilities are triggered through the ability system.
    ///
    /// Usage:
    ///   VFXManager.Instance.Spawn(VFXType.ForcePushWave, position, normal);
    ///   VFXManager.Instance.SpawnHitVFX(hitType, position, normal);
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static VFXManager Instance { get; private set; }

        // ── VFX CATALOGUE ─────────────────────────────────────────────────────
        public enum VFXType
        {
            // Combat hits
            BlasterHit,
            BlasterMuzzleFlash,
            HeadshotSplatter,
            WeakPointBurst,
            BladeSlash,
            LightsaberBlock,
            LightsaberClash,
            ExplosionSmall,
            ExplosionLarge,
            ShieldBreak,
            ShieldHit,

            // Grenades & consumables
            GrenadeFragExplosion,
            GrenadeFragSmoke,
            GrenadeConcussionBlast,
            GrenadePlasmaArc,
            GrenadeAdhesiveGlob,
            GrenadeThermalDetonation,
            MedPacHealOrb,
            StimulantBurst,
            EnergyShieldActivate,
            EnergyShieldHit,
            EnergyShieldBreak,

            // Force Powers
            ForcePushWave,
            ForceLightning,
            ForceMindTrick,
            ForceStasisAura,
            ForceHealGlow,
            ForceStealth,
            ForceSpeed,
            ForceCrush,
            ForceDrain,
            ForceAura,
            ForceWhirlwind,
            ForceValorGlow,
            MassHealPulse,

            // Alignment shifts
            LightSideAura,
            DarkSideAura,

            // UI / Game events
            OverwatchMark,
            ModeSwitchPulse,
            LevelUpBurst,
            HealOrb,
            DialogueFocus,
            AchievementUnlock,
        }

        [System.Serializable]
        public class VFXEntry
        {
            public VFXType   type;
            public GameObject prefab;
            [Tooltip("Auto-return to pool after this many seconds. 0 = manual.")]
            public float lifetime = 2f;
            [Tooltip("Pre-warm pool with this many instances on startup.")]
            public int poolSize = 4;
        }

        [Header("VFX Prefab Catalogue")]
        [SerializeField] private List<VFXEntry> catalogue = new List<VFXEntry>();

        // ── OBJECT POOL ───────────────────────────────────────────────────────
        private readonly Dictionary<VFXType, VFXEntry>         _lookup = new();
        private readonly Dictionary<VFXType, Queue<GameObject>> _pool   = new();
        private Transform _poolRoot;

        // Force-Power / Ability name → VFXType mapping
        private static readonly Dictionary<string, VFXType> _fpVFX =
            new Dictionary<string, VFXType>(System.StringComparer.OrdinalIgnoreCase)
        {
            // Force Powers
            { "Force Push",          VFXType.ForcePushWave          },
            { "Force Wave",          VFXType.ForcePushWave          },
            { "Force Whirlwind",     VFXType.ForceWhirlwind         },
            { "Force Lightning",     VFXType.ForceLightning         },
            { "Sith Lightning",      VFXType.ForceLightning         },
            { "Shock",               VFXType.ForceLightning         },
            { "Force Stasis",        VFXType.ForceStasisAura        },
            { "Stasis Field",        VFXType.ForceStasisAura        },
            { "Dominate Mind",       VFXType.ForceMindTrick         },
            { "Force Persuade",      VFXType.ForceMindTrick         },
            { "Cure",                VFXType.ForceHealGlow          },
            { "Force Heal",          VFXType.ForceHealGlow          },
            { "Mass Heal",           VFXType.MassHealPulse          },
            { "Force Speed",         VFXType.ForceSpeed             },
            { "Force Stealth",       VFXType.ForceStealth           },
            { "Force Crush",         VFXType.ForceCrush             },
            { "Death Field",         VFXType.ForceDrain             },
            { "Drain Life",          VFXType.ForceDrain             },
            { "Force Aura",          VFXType.ForceAura              },
            { "Battle Meditation",   VFXType.ForceAura              },
            { "Force Valor",         VFXType.ForceValorGlow         },
            // Grenades
            { "Frag Grenade",        VFXType.GrenadeFragExplosion   },
            { "Concussion Grenade",  VFXType.GrenadeConcussionBlast },
            { "Plasma Grenade",      VFXType.GrenadePlasmaArc       },
            { "Adhesive Grenade",    VFXType.GrenadeAdhesiveGlob    },
            { "Thermal Detonator",   VFXType.GrenadeThermalDetonation },
            // Consumables
            { "MedPac",              VFXType.MedPacHealOrb          },
            { "Stimulant",           VFXType.StimulantBurst         },
            { "Energy Shield",       VFXType.EnergyShieldActivate   },
            // Overwatch
            { "Overwatch Protocol",  VFXType.OverwatchMark          },
        };

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Create a hidden root to keep pooled objects organised in the hierarchy
            _poolRoot = new GameObject("[VFX Pool]").transform;
            _poolRoot.SetParent(transform);

            // Build lookup and pre-warm pools
            foreach (var entry in catalogue)
            {
                _lookup[entry.type] = entry;
                _pool[entry.type]   = new Queue<GameObject>();

                if (entry.prefab == null || entry.poolSize <= 0) continue;
                for (int i = 0; i < entry.poolSize; i++)
                    _pool[entry.type].Enqueue(CreatePooledInstance(entry));
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.EventType.AbilityUsed,       OnAbilityUsed);
            EventBus.Subscribe(EventBus.EventType.EntityDamaged,     OnEntityDamaged);
            EventBus.Subscribe(EventBus.EventType.PlayerLevelUp,     OnLevelUp);
            EventBus.Subscribe(EventBus.EventType.AlignmentChanged,  OnAlignmentChanged);
            EventBus.Subscribe(EventBus.EventType.AchievementUnlocked, OnAchievementUnlocked);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.EventType.AbilityUsed,       OnAbilityUsed);
            EventBus.Unsubscribe(EventBus.EventType.EntityDamaged,     OnEntityDamaged);
            EventBus.Unsubscribe(EventBus.EventType.PlayerLevelUp,     OnLevelUp);
            EventBus.Unsubscribe(EventBus.EventType.AlignmentChanged,  OnAlignmentChanged);
            EventBus.Unsubscribe(EventBus.EventType.AchievementUnlocked, OnAchievementUnlocked);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── EVENT HANDLERS ────────────────────────────────────────────────────
        private void OnAbilityUsed(EventBus.GameEventArgs args)
        {
            if (args is not EventBus.AbilityEventArgs ev) return;

            Vector3 pos = ev.Target?.transform.position
                       ?? ev.Caster?.transform.position
                       ?? Vector3.zero;

            // Map ability name to VFX type
            if (_fpVFX.TryGetValue(ev.AbilityName, out VFXType vfxType))
            {
                Spawn(vfxType, pos, Vector3.up);

                // Additional trailing VFX for grenades (secondary effects)
                switch (vfxType)
                {
                    case VFXType.GrenadeFragExplosion:
                        Spawn(VFXType.GrenadeFragSmoke, pos, Vector3.up);
                        break;
                    case VFXType.GrenadeThermalDetonation:
                        Spawn(VFXType.ExplosionLarge, pos, Vector3.up);
                        break;
                }
            }
        }

        private void OnEntityDamaged(EventBus.GameEventArgs args)
        {
            if (args is not EventBus.DamageEventArgs ev) return;
            if (ev.Target == null) return;

            Vector3 pos = ev.Target.transform.position + Vector3.up * 0.5f;

            // Shield impact / break
            var statsB = ev.Target.GetComponent<Player.PlayerStatsBehaviour>();
            if (statsB != null)
            {
                if (statsB.Stats.CurrentShield > 0f)
                    Spawn(VFXType.ShieldHit, pos, Vector3.up);
                else if (ev.Type == DamageType.Energy)
                    Spawn(VFXType.ShieldBreak, pos, Vector3.up);
            }

            // Blaster hit on environment
            if (ev.Type == DamageType.Physical || ev.Type == DamageType.Energy)
                Spawn(VFXType.BlasterHit, pos, Vector3.up);
        }

        private void OnLevelUp(EventBus.GameEventArgs args)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                Spawn(VFXType.LevelUpBurst, playerGO.transform.position + Vector3.up, Vector3.up);
        }

        private void OnAlignmentChanged(EventBus.GameEventArgs args)
        {
            if (args is not EventBus.AlignmentEventArgs ev) return;
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return;

            Vector3 pos = playerGO.transform.position + Vector3.up;
            if (ev.NewAlignment > 30)
                Spawn(VFXType.LightSideAura, pos, Vector3.up);
            else if (ev.NewAlignment < -30)
                Spawn(VFXType.DarkSideAura, pos, Vector3.up);
        }

        private void OnAchievementUnlocked(EventBus.GameEventArgs args)
        {
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO != null)
                Spawn(VFXType.AchievementUnlock,
                    playerGO.transform.position + Vector3.up * 2f, Vector3.up);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        /// <summary>
        /// Spawn a VFX effect at a world position oriented by surface normal.
        /// Returns the activated GameObject (or null if no prefab is registered).
        /// </summary>
        public GameObject Spawn(VFXType type, Vector3 position, Vector3 normal)
        {
            if (!_lookup.TryGetValue(type, out var entry) || entry.prefab == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[VFXManager] No prefab registered for VFXType.{type}");
#endif
                return null;
            }

            GameObject vfx = GetFromPool(type, entry);
            vfx.transform.position = position;
            vfx.transform.rotation = normal != Vector3.zero
                ? Quaternion.LookRotation(normal) : Quaternion.identity;
            vfx.SetActive(true);

            if (entry.lifetime > 0f)
                StartCoroutine(ReturnToPoolAfter(vfx, type, entry.lifetime));

            return vfx;
        }

        /// <summary>Spawn at position with up-facing rotation.</summary>
        public GameObject Spawn(VFXType type, Vector3 position)
            => Spawn(type, position, Vector3.up);

        /// <summary>Spawn attached to a parent Transform (muzzle-flash, foot-step, etc.).</summary>
        public GameObject SpawnAttached(VFXType type, Transform parent, Vector3 localOffset)
        {
            if (!_lookup.TryGetValue(type, out var entry) || entry.prefab == null)
                return null;

            GameObject vfx = GetFromPool(type, entry);
            vfx.transform.SetParent(parent, worldPositionStays: false);
            vfx.transform.localPosition = localOffset;
            vfx.transform.localRotation = Quaternion.identity;
            vfx.SetActive(true);

            if (entry.lifetime > 0f)
                StartCoroutine(ReturnToPoolAfter(vfx, type, entry.lifetime));

            return vfx;
        }

        /// <summary>Spawn the appropriate hit VFX for a given HitType.</summary>
        public void SpawnHitVFX(HitType hitType, Vector3 position, Vector3 normal)
        {
            switch (hitType)
            {
                case HitType.Headshot:  Spawn(VFXType.HeadshotSplatter, position, normal); break;
                case HitType.WeakPoint: Spawn(VFXType.WeakPointBurst,   position, normal); break;
                default:                Spawn(VFXType.BlasterHit,        position, normal); break;
            }
        }

        /// <summary>Spawn Force Power VFX by ability name (used by ForcePowerManager).</summary>
        public void SpawnForcePowerVFX(string abilityName, Vector3 target)
        {
            if (_fpVFX.TryGetValue(abilityName, out VFXType vfxType))
                Spawn(vfxType, target, Vector3.up);
        }

        /// <summary>Spawn grenade explosion VFX at world position.</summary>
        public void SpawnGrenadeVFX(string grenadeType, Vector3 position)
        {
            var vfxType = grenadeType?.ToLowerInvariant() switch
            {
                "frag"       => VFXType.GrenadeFragExplosion,
                "concussion" => VFXType.GrenadeConcussionBlast,
                "plasma"     => VFXType.GrenadePlasmaArc,
                "adhesive"   => VFXType.GrenadeAdhesiveGlob,
                "thermal"    => VFXType.GrenadeThermalDetonation,
                _            => VFXType.ExplosionSmall
            };
            Spawn(vfxType, position, Vector3.up);
            if (vfxType == VFXType.GrenadeFragExplosion)
                Spawn(VFXType.GrenadeFragSmoke, position, Vector3.up);
            if (vfxType == VFXType.GrenadeThermalDetonation)
                Spawn(VFXType.ExplosionLarge, position, Vector3.up);
        }

        /// <summary>Spawn EnergyShield activation VFX on a character.</summary>
        public void SpawnShieldVFX(GameObject target, bool activate)
        {
            if (target == null) return;
            var pos = target.transform.position + Vector3.up;
            Spawn(activate ? VFXType.EnergyShieldActivate : VFXType.EnergyShieldBreak,
                pos, Vector3.up);
        }

        // ── POOL HELPERS ──────────────────────────────────────────────────────
        private GameObject CreatePooledInstance(VFXEntry entry)
        {
            var go = Instantiate(entry.prefab, _poolRoot);
            go.SetActive(false);
            go.name = $"[Pool] {entry.type}";
            return go;
        }

        private GameObject GetFromPool(VFXType type, VFXEntry entry)
        {
            if (_pool.TryGetValue(type, out var queue) && queue.Count > 0)
            {
                var existing = queue.Dequeue();
                existing.transform.SetParent(null);
                return existing;
            }
            // Pool empty — grow it
            return CreatePooledInstance(entry);
        }

        private System.Collections.IEnumerator ReturnToPoolAfter(
            GameObject vfx, VFXType type, float delay)
        {
            yield return new WaitForSeconds(delay);
            ReturnToPool(vfx, type);
        }

        private void ReturnToPool(GameObject vfx, VFXType type)
        {
            if (vfx == null) return;
            vfx.SetActive(false);
            vfx.transform.SetParent(_poolRoot);
            if (!_pool.ContainsKey(type))
                _pool[type] = new Queue<GameObject>();
            _pool[type].Enqueue(vfx);
        }
    }
}
