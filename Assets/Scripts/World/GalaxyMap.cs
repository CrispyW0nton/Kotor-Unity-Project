using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.SaveSystem;

namespace KotORUnity.World
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  GALAXY MAP — KotOR planet navigation system
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  KotOR features seven planets the player can travel to after escaping Taris:
    //    Dantooine, Tatooine, Kashyyyk, Manaan, Korriban, Leviathan, Star Forge
    //  (Plus Taris as the prologue planet.)
    //
    //  This system manages:
    //    • Planet catalogue with lock/unlock state
    //    • "Warp to planet" triggering AreaLoader
    //    • Conversation-flag integration (e.g. Kashyyyk requires freeing Wookiees)
    //    • Star Map discovery tracking (required to find the Star Forge)

    // ───────────────────────────────────────────────────────────────────────────
    //  PLANET DATA
    // ───────────────────────────────────────────────────────────────────────────

    public enum PlanetId
    {
        Endar_Spire,    // Prologue crash
        Taris,          // Acts 1 prologue planet
        Dantooine,      // Jedi Enclave
        Tatooine,       // Anchorhead / Dune Sea
        Kashyyyk,       // Wookiee homeworld
        Manaan,         // Ahto City / Sith base
        Korriban,       // Sith Academy / Valley of Dark Lords
        Unknown_World,  // Rakata Prime (Star Map 4)
        Star_Forge      // Final area — unlocked after all star maps
    }

    [Serializable]
    public class GalaxyPlanet
    {
        public PlanetId   Id;
        public string     Name;
        public string     Description;
        public string     LandingModuleRef;   // first .mod to load on arrival
        public string     LandingWaypoint;    // waypoint tag for player spawn
        public Vector2    MapPosition;        // position on the galaxy map sprite
        public bool       IsUnlocked;
        public bool       IsVisited;
        public bool       HasStarMap;         // one of the 5 Star Maps found here
        public bool       StarMapFound;       // player retrieved it
        public Texture2D  PlanetSprite;       // assigned in inspector
        public string     UnlockConditionTag; // NWScript global that unlocks this planet

        // Crew assignments (updated by companion quests)
        public List<string> ActiveSideQuests = new List<string>();
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  GALAXY MAP MANAGER
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Manages the galaxy-map state: which planets are known, visited, and
    /// how many Star Maps have been found.
    ///
    /// Wire-up:
    ///   GalaxyMapManager.Instance.OpenMap() — show the map UI.
    ///   GalaxyMapManager.Instance.TravelTo(PlanetId) — initiate hyperspace travel.
    ///   Subscribe OnPlanetUnlocked, OnStarMapFound, OnAllStarMapsFound.
    /// </summary>
    public class GalaxyMapManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static GalaxyMapManager Instance { get; private set; }

        // ── EVENTS ─────────────────────────────────────────────────────────────
        public event Action<GalaxyPlanet>  OnPlanetUnlocked;
        public event Action<GalaxyPlanet>  OnStarMapFound;
        public event Action               OnAllStarMapsFound;  // triggers Star Forge unlock
        public event Action<GalaxyPlanet>  OnTravelStarted;
        public event Action<GalaxyPlanet>  OnTravelCompleted;

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Star Map Settings")]
        [Tooltip("How many Star Maps needed to unlock the Star Forge.")]
        [SerializeField] private int starMapsRequired = 4;

        // ── CATALOGUE ─────────────────────────────────────────────────────────
        private readonly List<GalaxyPlanet> _planets = new List<GalaxyPlanet>();

        // ── PROPERTIES ────────────────────────────────────────────────────────
        public IReadOnlyList<GalaxyPlanet> Planets         => _planets;
        public int                         StarMapsFound   => _planets.FindAll(p => p.StarMapFound).Count;
        public bool                        StarForgeUnlocked => StarMapsFound >= starMapsRequired;

        public GalaxyPlanet CurrentPlanet { get; private set; }
        public bool IsMapOpen { get; private set; }

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildDefaultCatalogue();
        }

        private void Start()
        {
            EventBus.Subscribe(EventBus.EventType.ModuleLoaded, OnModuleLoaded);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModuleLoaded, OnModuleLoaded);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Open the galaxy map (raises a GalaxyMapOpened event).</summary>
        public void OpenMap()
        {
            IsMapOpen = true;
            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());
            Debug.Log("[GalaxyMap] Map opened.");
        }

        /// <summary>Close the galaxy map.</summary>
        public void CloseMap()
        {
            IsMapOpen = false;
            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());
        }

        /// <summary>
        /// Initiate hyperspace travel to a planet.
        /// Raises OnTravelStarted, then loads the planet's landing module.
        /// </summary>
        public bool TravelTo(PlanetId id)
        {
            var planet = GetPlanet(id);
            if (planet == null)
            {
                Debug.LogWarning($"[GalaxyMap] Planet {id} not found in catalogue.");
                return false;
            }

            if (!planet.IsUnlocked)
            {
                Debug.LogWarning($"[GalaxyMap] Planet {planet.Name} is not yet unlocked.");
                return false;
            }

            Debug.Log($"[GalaxyMap] Initiating hyperspace travel to {planet.Name}...");
            CurrentPlanet = planet;
            CloseMap();
            OnTravelStarted?.Invoke(planet);

            // Trigger area loader
            EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                new EventBus.ModuleEventArgs(planet.LandingModuleRef, planet.LandingWaypoint));

            return true;
        }

        /// <summary>Unlock a planet (e.g. after completing a quest trigger).</summary>
        public void UnlockPlanet(PlanetId id)
        {
            var planet = GetPlanet(id);
            if (planet == null || planet.IsUnlocked) return;
            planet.IsUnlocked = true;
            OnPlanetUnlocked?.Invoke(planet);
            Debug.Log($"[GalaxyMap] Unlocked: {planet.Name}");
        }

        /// <summary>Record that the player found this planet's Star Map.</summary>
        public void RecordStarMapFound(PlanetId id)
        {
            var planet = GetPlanet(id);
            if (planet == null || planet.StarMapFound) return;
            planet.StarMapFound = true;
            Debug.Log($"[GalaxyMap] Star Map found on {planet.Name}! ({StarMapsFound}/{starMapsRequired})");
            OnStarMapFound?.Invoke(planet);

            if (StarMapsFound >= starMapsRequired)
            {
                UnlockPlanet(PlanetId.Star_Forge);
                OnAllStarMapsFound?.Invoke();
                Debug.Log("[GalaxyMap] All Star Maps collected! The Star Forge is unlocked.");
            }
        }

        /// <summary>Get a planet by id.</summary>
        public GalaxyPlanet GetPlanet(PlanetId id) => _planets.Find(p => p.Id == id);

        /// <summary>Returns all currently unlocked planets.</summary>
        public List<GalaxyPlanet> GetUnlockedPlanets() => _planets.FindAll(p => p.IsUnlocked);

        // ── SAVE / LOAD ───────────────────────────────────────────────────────

        /// <summary>Serialise map state for SaveManager.</summary>
        public GalaxyMapSaveData GetSaveData()
        {
            var data = new GalaxyMapSaveData();
            foreach (var p in _planets)
            {
                data.PlanetStates.Add(new PlanetSaveState
                {
                    PlanetId        = p.Id.ToString(),
                    IsUnlocked      = p.IsUnlocked,
                    IsVisited       = p.IsVisited,
                    StarMapFound    = p.StarMapFound,
                    ActiveSideQuests= new List<string>(p.ActiveSideQuests)
                });
            }
            data.CurrentPlanetId = CurrentPlanet?.Id.ToString() ?? "";
            return data;
        }

        /// <summary>Restore map state from SaveManager.</summary>
        public void LoadSaveData(GalaxyMapSaveData data)
        {
            if (data == null) return;
            foreach (var s in data.PlanetStates)
            {
                if (!Enum.TryParse<PlanetId>(s.PlanetId, out var pid)) continue;
                var planet = GetPlanet(pid);
                if (planet == null) continue;
                planet.IsUnlocked   = s.IsUnlocked;
                planet.IsVisited    = s.IsVisited;
                planet.StarMapFound = s.StarMapFound;
                planet.ActiveSideQuests = new List<string>(s.ActiveSideQuests ?? new List<string>());
            }
            if (!string.IsNullOrEmpty(data.CurrentPlanetId) &&
                Enum.TryParse<PlanetId>(data.CurrentPlanetId, out var cur))
                CurrentPlanet = GetPlanet(cur);
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        private void OnModuleLoaded(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModuleEventArgs ma && CurrentPlanet != null)
            {
                if (ma.ModuleName.StartsWith(CurrentPlanet.LandingModuleRef,
                        StringComparison.OrdinalIgnoreCase))
                {
                    CurrentPlanet.IsVisited = true;
                    OnTravelCompleted?.Invoke(CurrentPlanet);
                    Debug.Log($"[GalaxyMap] Arrived at {CurrentPlanet.Name}.");
                }
            }
        }

        // ── DEFAULT CATALOGUE (matches KotOR 1 planet list) ───────────────────

        private void BuildDefaultCatalogue()
        {
            _planets.Clear();
            _planets.AddRange(new[]
            {
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Endar_Spire,
                    Name                = "Endar Spire",
                    Description         = "A Republic cruiser ambushed above Taris. The prologue begins here.",
                    LandingModuleRef    = "end_m01aa",
                    LandingWaypoint     = "wp_start",
                    MapPosition         = new Vector2(0.10f, 0.80f),
                    IsUnlocked          = true,
                    IsVisited           = true,
                    HasStarMap          = false
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Taris,
                    Name                = "Taris",
                    Description         = "A city-world ravaged by Sith bombardment.",
                    LandingModuleRef    = "tar_m02aa",
                    LandingWaypoint     = "wp_south_apartments",
                    MapPosition         = new Vector2(0.22f, 0.60f),
                    IsUnlocked          = true,
                    HasStarMap          = false
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Dantooine,
                    Name                = "Dantooine",
                    Description         = "Peaceful homeworld of the Jedi Enclave. A Star Map lies in ancient ruins.",
                    LandingModuleRef    = "dan_m14aa",
                    LandingWaypoint     = "wp_enclave_entrance",
                    MapPosition         = new Vector2(0.35f, 0.45f),
                    IsUnlocked          = false,
                    HasStarMap          = true,
                    UnlockConditionTag  = "K_DANTOOINE_UNLOCKED"
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Tatooine,
                    Name                = "Tatooine",
                    Description         = "A desert planet of twin suns, swoop races, and ancient Rakatan ruins.",
                    LandingModuleRef    = "tat_m17aa",
                    LandingWaypoint     = "wp_anchorhead_landing",
                    MapPosition         = new Vector2(0.60f, 0.30f),
                    IsUnlocked          = false,
                    HasStarMap          = true,
                    UnlockConditionTag  = "K_TATOOINE_UNLOCKED"
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Kashyyyk,
                    Name                = "Kashyyyk",
                    Description         = "Wookiee homeworld, enslaved by Czerka Corporation. A Star Map lies in the Shadowlands.",
                    LandingModuleRef    = "kas_m22aa",
                    LandingWaypoint     = "wp_rwookrrorro_landing",
                    MapPosition         = new Vector2(0.50f, 0.55f),
                    IsUnlocked          = false,
                    HasStarMap          = true,
                    UnlockConditionTag  = "K_KASHYYYK_UNLOCKED"
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Manaan,
                    Name                = "Manaan",
                    Description         = "Neutral ocean world home to the Selkath and their kolto trade.",
                    LandingModuleRef    = "man_m26aa",
                    LandingWaypoint     = "wp_ahto_city_landing",
                    MapPosition         = new Vector2(0.70f, 0.65f),
                    IsUnlocked          = false,
                    HasStarMap          = true,
                    UnlockConditionTag  = "K_MANAAN_UNLOCKED"
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Korriban,
                    Name                = "Korriban",
                    Description         = "Sith homeworld. The Valley of Dark Lords and Sith Academy await.",
                    LandingModuleRef    = "kor_m35aa",
                    LandingWaypoint     = "wp_dreshdae_landing",
                    MapPosition         = new Vector2(0.80f, 0.20f),
                    IsUnlocked          = false,
                    HasStarMap          = true,
                    UnlockConditionTag  = "K_KORRIBAN_UNLOCKED"
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Unknown_World,
                    Name                = "Unknown World",
                    Description         = "Rakata Prime — the origin of the Star Forge. Accessible only with all Star Maps.",
                    LandingModuleRef    = "unk_m41aa",
                    LandingWaypoint     = "wp_beach_landing",
                    MapPosition         = new Vector2(0.90f, 0.50f),
                    IsUnlocked          = false,
                    HasStarMap          = false
                },
                new GalaxyPlanet
                {
                    Id                  = PlanetId.Star_Forge,
                    Name                = "Star Forge",
                    Description         = "The ancient Rakatan factory above the Unknown World. The final confrontation awaits.",
                    LandingModuleRef    = "sta_m45aa",
                    LandingWaypoint     = "wp_star_forge_entry",
                    MapPosition         = new Vector2(0.95f, 0.55f),
                    IsUnlocked          = false,
                    HasStarMap          = false
                }
            });

            Debug.Log($"[GalaxyMap] Catalogue built: {_planets.Count} planets.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SAVE DATA STRUCTS (used by SaveManager)
    // ═══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class GalaxyMapSaveData
    {
        public string CurrentPlanetId = "";
        public List<PlanetSaveState> PlanetStates = new List<PlanetSaveState>();
    }

    [Serializable]
    public class PlanetSaveState
    {
        public string       PlanetId;
        public bool         IsUnlocked;
        public bool         IsVisited;
        public bool         StarMapFound;
        public List<string> ActiveSideQuests = new List<string>();
    }
}
