using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using KotORUnity.Bootstrap;
using KotORUnity.Core;
using KotORUnity.World;
#pragma warning disable 0414, 0219

namespace KotORUnity.World
{
    /// <summary>
    /// Trigger volume placed at module exits (doors, invisible transition zones).
    ///
    /// When the player walks through, it:
    ///   1. Saves the current position (return point).
    ///   2. Fires EventBus.AreaTransitionRequested so the AreaLoader can load
    ///      the destination module and place the player at the named waypoint.
    ///   3. Optionally fades the screen.
    ///
    /// The trigger GameObject needs a Collider with IsTrigger = true.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class AreaTransitionTrigger : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Destination")]
        [Tooltip("Module name of the destination area (e.g. 'ebo_m12aa').")]
        [SerializeField] private string destinationModule = "";

        [Tooltip("Tag of the waypoint (WP_) in the destination where the player spawns.")]
        [SerializeField] private string destinationWaypoint = "";

        [Header("Display")]
        [Tooltip("Shown briefly on screen during the transition.")]
        [SerializeField] private string loadingText = "";

        [Header("Behaviour")]
        [SerializeField] private bool requirePlayerTag = true;
        [SerializeField] private bool oneShot = false;

        // ── RUNTIME ───────────────────────────────────────────────────────────
        private bool _triggered = false;

        // ── UNITY ─────────────────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (requirePlayerTag && !other.CompareTag("Player")) return;
            if (string.IsNullOrEmpty(destinationModule)) return;

            _triggered = true;

            Debug.Log($"[AreaTransition] Player entered trigger → '{destinationModule}' " +
                      $"wp='{destinationWaypoint}'");

            EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                new AreaTransitionEventArgs(destinationModule, destinationWaypoint));

            if (oneShot) Destroy(gameObject);
            else         _triggered = false;   // allow re-trigger after load
        }

        // ── GIZMOS ────────────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.8f, 0.1f, 0.3f);
            var c = GetComponent<Collider>();
            if (c is BoxCollider box)
                Gizmos.DrawCube(transform.position + box.center,
                                Vector3.Scale(box.size, transform.lossyScale));
            else
                Gizmos.DrawSphere(transform.position, 1f);

            // Label
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"→ {destinationModule}\n  wp:{destinationWaypoint}");
        }
#endif
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  AREA LOADER  —  handles the actual module-load sequence
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Listens for AreaTransitionRequested events and performs the full load:
    ///   1. Fade out
    ///   2. Mount new module archives
    ///   3. Load .are (area properties) and build area lighting/sky
    ///   4. Load .git (instance file) via CreatureSpawner
    ///   5. Load + build walkmesh
    ///   6. Place player at the destination waypoint
    ///   7. Fade in
    /// </summary>
    public class AreaLoader : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static AreaLoader Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Scene References")]
        [SerializeField] private Transform      areaRoot;
        [SerializeField] private CreatureSpawner creatureSpawner;
        [SerializeField] private Transform      playerTransform;

        [Header("Startup Module")]
        [Tooltip("Module to load automatically when the Game scene starts (e.g. 'danm14aa').")]
        [SerializeField] private string startupModuleName = "danm14aa";
        [Tooltip("If true, loads startupModuleName on Start(). Disable for save-game loads.")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Header("Fade")]
        [SerializeField] private UnityEngine.UI.Image fadeImage;
        [SerializeField] private float fadeDuration = 0.4f;

        [Header("NavMesh Baking")]
        [Tooltip("If true, bakes a runtime NavMesh after loading each walkmesh.")]
        [SerializeField] private bool bakeNavMeshOnLoad = true;
        [SerializeField] private float navAgentRadius = 0.3f;
        [SerializeField] private float navAgentHeight = 1.8f;
        [SerializeField] private float navMaxSlope    = 45f;
        [SerializeField] private float navStepHeight  = 0.4f;

        [Header("Stress-Test / Diagnostics")]
        [Tooltip("Log detailed timing for each load phase (editor/debug use).")]
        [SerializeField] private bool enableLoadProfiling = false;

        // ── RUNTIME ───────────────────────────────────────────────────────────
        public string CurrentModule { get; private set; }
        private bool  _isLoading    = false;

        /// <summary>Last load's phase timing results (ms). Updated after every load.</summary>
        public LoadTimingReport LastLoadTiming { get; private set; }

        [Serializable]
        public class LoadTimingReport
        {
            public string ModuleName;
            public long   FadeOutMs;
            public long   ClearAreaMs;
            public long   MountModuleMs;
            public long   ParseAreMs;
            public long   SpawnCreaturesMs;
            public long   BuildWalkmeshMs;
            public long   BakeNavMeshMs;
            public long   PlacePlayerMs;
            public long   FadeInMs;
            public long   TotalMs;
        }

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private System.Collections.IEnumerator Start()
        {
            if (!autoLoadOnStart || string.IsNullOrEmpty(startupModuleName))
                yield break;

            // Wait until SceneBootstrapper has finished mounting archives.
            // (It sets IsReady=true after Step 4 completes.)
            float waited = 0f;
            while (!Bootstrap.SceneBootstrapper.IsReady && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!Bootstrap.SceneBootstrapper.IsReady)
            {
                Debug.LogWarning("[AreaLoader] SceneBootstrapper not ready after 30 s — " +
                                 "loading module without archive data.");
            }

            // Auto-find player transform if not set in Inspector
            if (playerTransform == null)
            {
                var playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null) playerTransform = playerGO.transform;
            }

            StartCoroutine(LoadAreaRoutine(startupModuleName, ""));
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.EventType.AreaTransitionRequested, OnTransitionRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.EventType.AreaTransitionRequested, OnTransitionRequested);
        }

        // ── EVENT HANDLER ─────────────────────────────────────────────────────
        private void OnTransitionRequested(EventBus.GameEventArgs args)
        {
            if (_isLoading) return;
            var ev = args as AreaTransitionEventArgs;
            if (ev == null) return;

            StartCoroutine(LoadAreaRoutine(ev.TargetArea, ev.DoorTag));
        }

        // ── LOAD ROUTINE ──────────────────────────────────────────────────────
        private System.Collections.IEnumerator LoadAreaRoutine(string moduleName, string waypointTag)
        {
            _isLoading = true;
            var report = new LoadTimingReport { ModuleName = moduleName };
            var totalSw = enableLoadProfiling ? Stopwatch.StartNew() : null;

            // 1. Fade out
            var sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            yield return StartCoroutine(Fade(0f, 1f));
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.FadeOutMs = sw.ElapsedMilliseconds; }

            // 2. Clear old area
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            if (areaRoot != null)
            {
                for (int i = areaRoot.childCount - 1; i >= 0; i--)
                    Destroy(areaRoot.GetChild(i).gameObject);
            }
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.ClearAreaMs = sw.ElapsedMilliseconds; }

            // 3. Mount new module
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            SceneBootstrapper.Resources?.MountModule(moduleName);
            CurrentModule = moduleName;
            KotORUnity.SaveSystem.WorldStateTracker.MarkModuleVisited(moduleName);
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.MountModuleMs = sw.ElapsedMilliseconds; }

            // 4. Parse .are properties (lighting, fog)
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            LoadAreProperties(moduleName);
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.ParseAreMs = sw.ElapsedMilliseconds; }

            // 5. Spawn creatures / doors / placeables / waypoints via .git
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            creatureSpawner?.SpawnArea(moduleName);
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.SpawnCreaturesMs = sw.ElapsedMilliseconds; }

            // 6. Build walkmesh collider
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            var walkmeshGO = BuildWalkmesh(moduleName);
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.BuildWalkmeshMs = sw.ElapsedMilliseconds; }

            // 7. Bake NavMesh (runtime, if enabled)
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            if (bakeNavMeshOnLoad && walkmeshGO != null)
            {
                WalkmeshLoader.BakeNavMeshOnGameObject(walkmeshGO,
                    navAgentRadius, navAgentHeight, navMaxSlope, navStepHeight);
            }
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.BakeNavMeshMs = sw.ElapsedMilliseconds; }

            // 8. Place player at destination waypoint
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            PlacePlayer(waypointTag);
            yield return null;
            if (enableLoadProfiling) { sw.Stop(); report.PlacePlayerMs = sw.ElapsedMilliseconds; }

            EventBus.Publish(EventBus.EventType.ModuleLoaded,
                new EventBus.ModuleEventArgs(moduleName));

            // 9. Fade in
            sw = enableLoadProfiling ? Stopwatch.StartNew() : null;
            yield return StartCoroutine(Fade(1f, 0f));
            if (enableLoadProfiling) { sw.Stop(); report.FadeInMs = sw.ElapsedMilliseconds; }

            if (enableLoadProfiling)
            {
                totalSw.Stop();
                report.TotalMs = totalSw.ElapsedMilliseconds;
                LastLoadTiming = report;
                LogTimingReport(report);
            }

            _isLoading = false;
        }

        private void LogTimingReport(LoadTimingReport r)
        {
            Debug.Log(
                $"[AreaLoader] Load timing for '{r.ModuleName}':\n" +
                $"  FadeOut:         {r.FadeOutMs,6} ms\n" +
                $"  ClearArea:       {r.ClearAreaMs,6} ms\n" +
                $"  MountModule:     {r.MountModuleMs,6} ms\n" +
                $"  ParseARE:        {r.ParseAreMs,6} ms\n" +
                $"  SpawnCreatures:  {r.SpawnCreaturesMs,6} ms\n" +
                $"  BuildWalkmesh:   {r.BuildWalkmeshMs,6} ms\n" +
                $"  BakeNavMesh:     {r.BakeNavMeshMs,6} ms\n" +
                $"  PlacePlayer:     {r.PlacePlayerMs,6} ms\n" +
                $"  FadeIn:          {r.FadeInMs,6} ms\n" +
                $"  ─────────────────────────\n" +
                $"  TOTAL:           {r.TotalMs,6} ms");
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private void LoadAreProperties(string moduleName)
        {
            byte[] areData = SceneBootstrapper.Resources?.GetResource(moduleName,
                KotORUnity.KotOR.FileReaders.ResourceType.ARE);
            if (areData == null) return;

            var are = KotORUnity.KotOR.Parsers.GffReader.Parse(areData);
            if (are == null) return;

            // Ambient light colour
            int ambR = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "AmbientRed",   128);
            int ambG = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "AmbientGreen", 128);
            int ambB = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "AmbientBlue",  128);

            Color ambColor = new Color(ambR / 255f, ambG / 255f, ambB / 255f);
            RenderSettings.ambientLight = ambColor;

            // Fog
            bool fogOn   = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "FogClipFar", 0) > 0;
            float fogNear = KotORUnity.KotOR.Parsers.GffReader.GetFloat(are, "FogClipNear", 50f);
            float fogFar  = KotORUnity.KotOR.Parsers.GffReader.GetFloat(are, "FogClipFar",  200f);
            int fogR = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "FogColor1", 128);
            int fogG = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "FogColor2", 128);
            int fogB = KotORUnity.KotOR.Parsers.GffReader.GetInt(are, "FogColor3", 128);

            RenderSettings.fog          = fogOn;
            RenderSettings.fogStartDistance = fogNear;
            RenderSettings.fogEndDistance   = fogFar;
            RenderSettings.fogColor     = new Color(fogR/255f, fogG/255f, fogB/255f);
        }

        private GameObject BuildWalkmesh(string moduleName)
        {
            byte[] wokData = SceneBootstrapper.Resources?.GetResource(moduleName,
                KotORUnity.KotOR.FileReaders.ResourceType.WOK);
            if (wokData == null) return null;

            var parent = areaRoot ?? transform;
            return WalkmeshLoader.BuildCollider(wokData, parent, moduleName + "_wok");
        }

        private void PlacePlayer(string waypointTag)
        {
            if (playerTransform == null) return;

            // Find the waypoint with a matching tag
            var markers = FindObjectsOfType<WaypointMarker>();
            foreach (var wp in markers)
            {
                string wpTag = string.IsNullOrEmpty(waypointTag) ? "WP_ENTRANCE" : waypointTag;
                if (string.Equals(wp.Tag, wpTag, StringComparison.OrdinalIgnoreCase))
                {
                    playerTransform.position = wp.transform.position + Vector3.up * 0.1f;
                    playerTransform.rotation = wp.transform.rotation;
                    return;
                }
            }

            // Fallback: no matching waypoint, put player at origin + small offset
            if (WalkmeshLoader.TryGetGroundHeight(Vector3.zero, out float h))
                playerTransform.position = new Vector3(0, h + 0.1f, 0);
        }

        private System.Collections.IEnumerator Fade(float from, float to)
        {
            if (fadeImage == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, elapsed / fadeDuration);
                var c = fadeImage.color;
                c.a = a;
                fadeImage.color = c;
                yield return null;
            }
            var fc = fadeImage.color;
            fc.a = to;
            fadeImage.color = fc;
        }
    }
}
