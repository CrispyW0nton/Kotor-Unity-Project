using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KotORUnity.Bootstrap;
using KotORUnity.Data;
using KotORUnity.Core;
using KotORUnity.World;
using KotORUnity.Dialogue;

namespace KotORUnity.Bootstrap
{
    /// <summary>
    /// Entry point for the KotOR-Unity port.
    ///
    /// Attach this MonoBehaviour to a "GameBootstrap" GameObject that lives in
    /// a lightweight "Boot" scene (scene index 0).  On Awake it:
    ///   1. Mounts all KotOR archives via ResourceManager
    ///   2. Loads all 2DA tables via GameDataRepository
    ///   3. Loads the TLK string table
    ///   4. Transitions to the Main Menu scene
    ///
    /// Every other scene can then call SceneBootstrapper.Resources / .DataRepo
    /// from any MonoBehaviour without needing to re-mount the archives.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SceneBootstrapper : MonoBehaviour
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────
        public static SceneBootstrapper Instance { get; private set; }

        // ── INSPECTOR FIELDS ──────────────────────────────────────────────────
        [Header("KotOR Installation")]
        [Tooltip("Absolute path to the KotOR installation root (set once; stored in PlayerPrefs).")]
        [SerializeField] private string kotorInstallPath = "";

        [Header("Scene Flow")]
        [Tooltip("Build index of the Main Menu scene (loaded after bootstrapping).")]
        [SerializeField] private int mainMenuSceneIndex = 1;

        [Header("Debug")]
        [SerializeField] private bool verbose = false;

        // ── PUBLIC ACCESSORS ──────────────────────────────────────────────────
        public static ResourceManager   Resources  { get; private set; }
        public static TlkReader         Strings    { get; private set; }
        public static bool              IsReady    { get; private set; }

        // Convenience passthrough
        public static string GameRoot => Instance != null ? Instance._resolvedPath : "";

        // ── PRIVATE ───────────────────────────────────────────────────────────
        private string _resolvedPath;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            // Destroy duplicate — stop immediately, do not run BootRoutine
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // DontDestroyOnLoad requires a ROOT GameObject.
            // If we are nested under a parent, detach first.
            if (transform.parent != null)
            {
                Debug.LogWarning("[SceneBootstrapper] Detaching from parent so DontDestroyOnLoad works.");
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);

            // Resolve install path: inspector → PlayerPrefs → common defaults
            _resolvedPath = ResolveInstallPath();

            if (string.IsNullOrEmpty(_resolvedPath))
            {
                Debug.LogError("[SceneBootstrapper] KotOR install path not set. " +
                               "Set it via Inspector or PlayerPrefs key 'KotorPath'.");
                return;
            }

            PlayerPrefs.SetString("KotorPath", _resolvedPath);
            PlayerPrefs.Save();

            StartCoroutine(BootRoutine());
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── BOOT ROUTINE ──────────────────────────────────────────────────────
        private IEnumerator BootRoutine()
        {
            Log("=== KotOR-Unity Boot ===");
            Log($"Install path: {_resolvedPath}");

            // ── Step 1: Mount archives ────────────────────────────────────────
            Log("Step 1/4 — Mounting KotOR archives...");
            Resources = new ResourceManager();
            yield return null;   // spread across frames
            Resources.Mount(_resolvedPath);
            Log($"Step 1/4 — Archives mounted. Index: {Resources.EntryCount} resources.");
            yield return null;

            // ── Step 2: Load 2DA tables ───────────────────────────────────────
            // Use the singleton directly — FindObjectOfType cannot see objects
            // that have already been moved to the DontDestroyOnLoad scene.
            Log("Step 2/4 — Loading 2DA tables...");
            var dataRepo = GameDataRepository.Instance;
            if (dataRepo == null)
            {
                // Last-chance search across all loaded scenes
                dataRepo = GameObject.FindObjectOfType<GameDataRepository>();
            }
            if (dataRepo != null)
            {
                dataRepo.LoadAll(Resources);
                Log($"Step 2/4 — 2DA tables loaded. IsLoaded={dataRepo.IsLoaded}");
            }
            else
            {
                Debug.LogWarning("[SceneBootstrapper] GameDataRepository not found. " +
                    "Add a GameObject with the GameDataRepository component to the Boot scene.");
            }
            yield return null;

            // ── Step 3: Load TLK string table ─────────────────────────────────
            // dialog.tlk lives at the game root, NOT inside any BIF/ERF archive.
            // Always load it directly from disk first; fall back to ResourceManager
            // only if a modded version is indexed there.
            Log("Step 3/4 — Loading dialog.tlk...");
            Strings = new TlkReader();
            string tlkPath = System.IO.Path.Combine(_resolvedPath, "dialog.tlk");
            if (System.IO.File.Exists(tlkPath))
            {
                Strings.Load(System.IO.File.ReadAllBytes(tlkPath));
                Log($"Step 3/4 — TLK loaded from disk: {Strings.StringCount} strings.");
            }
            else
            {
                // Fallback: ResourceManager (modded TLK packed in an ERF)
                byte[] tlkData = Resources.GetResource("dialog",
                    KotORUnity.KotOR.FileReaders.ResourceType.TLK);
                if (tlkData != null)
                {
                    Strings.Load(tlkData);
                    Log($"Step 3/4 — TLK loaded from ResourceManager: {Strings.StringCount} strings.");
                }
                else
                {
                    Debug.LogWarning($"[SceneBootstrapper] dialog.tlk not found at '{tlkPath}' " +
                        "and not indexed in ResourceManager. String lookups will return placeholders.");
                }
            }
            yield return null;

            // ── Step 4: Done — transition to main menu ────────────────────────
            IsReady = true;
            Log("Step 4/4 — Boot complete. Transitioning to main menu...");

            if (mainMenuSceneIndex >= 0 &&
                mainMenuSceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(mainMenuSceneIndex);
            }
            else
            {
                Debug.LogWarning($"[SceneBootstrapper] mainMenuSceneIndex ({mainMenuSceneIndex}) " +
                    "is out of range. Add your Main Menu scene to File → Build Settings.");
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private string ResolveInstallPath()
        {
            if (!string.IsNullOrEmpty(kotorInstallPath) &&
                System.IO.Directory.Exists(kotorInstallPath))
                return kotorInstallPath;

            string saved = PlayerPrefs.GetString("KotorPath", "");
            if (!string.IsNullOrEmpty(saved) && System.IO.Directory.Exists(saved))
                return saved;

            // Common default install paths
            string[] defaults = {
                @"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
                @"C:\Program Files\LucasArts\SWKotOR",
                "/Applications/Knights of the Old Republic.app/Contents/Assets",
                "~/Library/Application Support/Steam/steamapps/common/swkotor"
            };
            foreach (var p in defaults)
            {
                string expanded = p.Replace("~",
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
                if (System.IO.Directory.Exists(expanded)) return expanded;
            }

            return "";
        }

        private void Log(string msg)
        {
            if (verbose) Debug.Log($"[SceneBootstrapper] {msg}");
            else Debug.Log($"[Boot] {msg}");
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>Set the KotOR install path at runtime (from a settings screen).</summary>
        public static void SetInstallPath(string path)
        {
            if (Instance == null) return;
            Instance.kotorInstallPath = path;
            Instance._resolvedPath    = path;
            PlayerPrefs.SetString("KotorPath", path);
            PlayerPrefs.Save();
        }

        /// <summary>Get a localised string by StrRef index.</summary>
        public static string GetString(uint strref)
            => Strings?.GetString(strref) ?? $"<StrRef:{strref}>";

        /// <summary>
        /// Get the VO wav resref stored in dialog.tlk for a given StrRef.
        /// KotOR TLK entries embed a 16-char SoundResRef field pointing to
        /// the matching .wav file in the override/StreamVoice folders.
        /// </summary>
        public static string GetVoiceWavResRef(uint strref)
            => Strings?.GetSoundRef(strref) ?? "";
    }
}
