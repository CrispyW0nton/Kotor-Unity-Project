using System;
using System.Collections;
using System.IO;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.Modules;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.Core
{
    /// <summary>
    /// Central entry point for the KotOR-Unity MRL_GameForge v2 game.
    /// 
    /// Responsibilities:
    ///   - Initialize all core systems in correct order
    ///   - Load KotOR modules from disk
    ///   - Manage game state (mode, difficulty, pause)
    ///   - Expose global accessors for key systems
    /// 
    /// This is a singleton MonoBehaviour — one instance per scene.
    /// Configure it in the Inspector by setting KotorDir, TargetGame, and EntryModule.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── INSPECTOR FIELDS ───────────────────────────────────────────────────
        [Header("KotOR Configuration")]
        [Tooltip("Root directory of your KotOR installation (e.g., C:/KotOR)")]
        [SerializeField] private string kotorDir = "";

        [Tooltip("Which game to target: KotOR (1) or TSL (2)")]
        [SerializeField] private TargetGame targetGame = TargetGame.KotOR;

        [Tooltip("Name of the module to load on startup (e.g., danm14aa)")]
        [SerializeField] private string entryModule = "danm14aa";

        [Header("Game Settings")]
        [Tooltip("Starting difficulty")]
        [SerializeField] private Difficulty difficulty = Difficulty.Normal;

        [Tooltip("Starting mode when the game initializes")]
        [SerializeField] private GameMode defaultMode = GameMode.Action;

        [Header("Debug")]
        [SerializeField] private bool debugMode = false;
        [SerializeField] private bool showModeOverlay = true;

        // ── SYSTEM REFERENCES (set by this manager) ───────────────────────────
        private ModeSwitchSystem _modeSwitchSystem;
        private ModuleLoader _moduleLoader;

        // ── STATE ──────────────────────────────────────────────────────────────
        private bool _isInitialized = false;
        private bool _isModuleLoaded = false;

        // ── PUBLIC ACCESSORS ───────────────────────────────────────────────────
        public string KotorDir => kotorDir;
        public TargetGame TargetGame => targetGame;
        public Difficulty CurrentDifficulty => difficulty;
        public GameMode CurrentMode => _modeSwitchSystem != null ? _modeSwitchSystem.CurrentMode : defaultMode;
        public bool IsInitialized => _isInitialized;
        public bool IsModuleLoaded => _isModuleLoaded;
        public bool DebugMode => debugMode;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            // Enforce singleton
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] Duplicate instance destroyed.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            // Initialization is deferred to Start so SceneBootstrapper.Awake()
            // has had a chance to run and write the KotorPath PlayerPrefs key.
        }

        private void Start()
        {
            StartCoroutine(InitializeWhenReady());
        }

        /// <summary>
        /// Wait for SceneBootstrapper to finish mounting archives, then initialize.
        /// On the very first run PlayerPrefs won't have the key yet — we fall back to
        /// polling SceneBootstrapper.IsReady or using the inspector kotorDir directly.
        /// </summary>
        private IEnumerator InitializeWhenReady()
        {
            // If SceneBootstrapper exists, wait until it has finished its BootRoutine.
            if (SceneBootstrapper.Instance != null)
            {
                float timeout = 30f;
                float elapsed = 0f;
                while (!SceneBootstrapper.IsReady && elapsed < timeout)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!SceneBootstrapper.IsReady)
                    Debug.LogWarning("[GameManager] SceneBootstrapper did not become ready within 30 s — proceeding anyway.");
            }

            InitializeSystems();

            if (_isInitialized)
                LoadEntryModule();
        }

        private void OnDestroy()
        {
            EventBus.ClearAll();
            if (Instance == this) Instance = null;
        }

        // ── INITIALIZATION ─────────────────────────────────────────────────────
        /// <summary>
        /// Initialize all game systems in dependency order.
        /// </summary>
        private void InitializeSystems()
        {
            try
            {
                LogDebug("Initializing KotOR-Unity MRL_GameForge v2...");

                // 1. Validate KotOR directory
                if (!ValidateKotorDirectory())
                {
                    Debug.LogError($"[GameManager] KotOR directory not found or invalid: '{kotorDir}'");
                    return;
                }

                // 2. Core systems (no dependencies)
                // EventBus is static — already available

                // 3. Mode switch system
                _modeSwitchSystem = GetComponentInChildren<ModeSwitchSystem>()
                    ?? gameObject.AddComponent<ModeSwitchSystem>();
                _modeSwitchSystem.Initialize(defaultMode);

                // 4. Module loader (KotOR file access)
                _moduleLoader = GetComponentInChildren<ModuleLoader>()
                    ?? gameObject.AddComponent<ModuleLoader>();
                _moduleLoader.Initialize(kotorDir, targetGame);

                // 5. Subscribe to key events
                EventBus.Subscribe(EventBus.EventType.ModuleLoaded, OnModuleLoaded);
                EventBus.Subscribe(EventBus.EventType.ModeSwitch, OnModeSwitch);

                _isInitialized = true;
                LogDebug("Systems initialized successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameManager] Initialization failed: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>Validate that the KotOR installation directory exists and contains expected files.</summary>
        private bool ValidateKotorDirectory()
        {
            // If the inspector field is empty, try to inherit the path that
            // SceneBootstrapper already resolved (it reads PlayerPrefs / auto-detect).
            if (string.IsNullOrEmpty(kotorDir))
            {
                // SceneBootstrapper stores the resolved path in PlayerPrefs key "KotorPath"
                string bootstrapPath = PlayerPrefs.GetString("KotorPath", "");
                if (!string.IsNullOrEmpty(bootstrapPath))
                {
                    kotorDir = bootstrapPath;
                    Debug.Log($"[GameManager] kotorDir inherited from SceneBootstrapper: '{kotorDir}'");
                }
            }

            if (string.IsNullOrEmpty(kotorDir)) return false;
            if (!Directory.Exists(kotorDir)) return false;

            // Check for expected KotOR directories
            bool hasModules = Directory.Exists(Path.Combine(kotorDir, "Modules"));
            bool hasData = Directory.Exists(Path.Combine(kotorDir, "data"))
                        || Directory.Exists(Path.Combine(kotorDir, "Data"));

            if (!hasModules)
                Debug.LogWarning($"[GameManager] Modules directory not found in '{kotorDir}'");
            if (!hasData)
                Debug.LogWarning($"[GameManager] Data directory not found in '{kotorDir}'");

            return hasModules;
        }

        // ── MODULE LOADING ─────────────────────────────────────────────────────
        /// <summary>Load the entry module specified in the Inspector.</summary>
        private void LoadEntryModule()
        {
            if (string.IsNullOrEmpty(entryModule))
            {
                Debug.LogWarning("[GameManager] No entry module specified.");
                return;
            }

            LogDebug($"Loading module: {entryModule}");
            _moduleLoader.LoadModule(entryModule);
        }

        /// <summary>Load a specific module by name at runtime.</summary>
        public void LoadModule(string moduleName)
        {
            if (!_isInitialized)
            {
                Debug.LogError("[GameManager] Cannot load module — not initialized.");
                return;
            }

            entryModule = moduleName;
            _moduleLoader.LoadModule(moduleName);
        }

        /// <summary>
        /// Begin a brand-new game from a <see cref="UI.NewGameConfig"/> produced by
        /// <see cref="UI.CharacterCreationController"/>.
        ///
        /// Flow:
        ///   1. Apply character config to PlayerStats / LevelSystem.
        ///   2. Save to the chosen slot (slot 0 = auto-save for new game).
        ///   3. Load the entry module.
        /// </summary>
        public void StartNewGame(UI.NewGameConfig config,
                                 SaveSlot slot = SaveSlot.Manual1)
        {
            Debug.Log($"[GameManager] StartNewGame — name={config.PlayerName} class={config.ClassId} slot={slot}");

            // Apply stats
            var levelSys = FindObjectOfType<Progression.LevelSystem>();
            if (levelSys != null)
                levelSys.InitialiseFromConfig(config);

            // Initial save to chosen slot
            var saveMgr = SaveSystem.SaveManager.Instance;
            if (saveMgr != null)
                saveMgr.Save(slot);

            // Load entry module
            LoadModule(string.IsNullOrEmpty(entryModule) ? "001EBO" : entryModule);
        }

        // ── GAME STATE CONTROL ─────────────────────────────────────────────────
        /// <summary>
        /// Switch game difficulty. Can be called at any time.
        /// Affects enemy HP multipliers and AI reaction times.
        /// </summary>
        public void SetDifficulty(Difficulty newDifficulty)
        {
            difficulty = newDifficulty;
            LogDebug($"Difficulty set to: {newDifficulty}");
        }

        /// <summary>Get the enemy HP multiplier for the current difficulty and mode.</summary>
        public float GetEnemyHPMultiplier(GameMode mode)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return 1.0f;
                case Difficulty.Normal:
                    return mode == GameMode.Action
                        ? GameConstants.NORMAL_ACTION_ENEMY_HP_MULT
                        : 1.0f;
                case Difficulty.Hard:
                    return mode == GameMode.Action
                        ? GameConstants.HARD_ACTION_ENEMY_HP_MULT
                        : GameConstants.HARD_RTS_ENEMY_HP_MULT;
                case Difficulty.Nightmare:
                    return GameConstants.NIGHTMARE_ENEMY_HP_MULT;
                default:
                    return 1.0f;
            }
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnModuleLoaded(EventBus.GameEventArgs args)
        {
            _isModuleLoaded = true;
            var moduleArgs = args as EventBus.ModuleEventArgs;
            LogDebug($"Module loaded: {moduleArgs?.ModuleName}");
        }

        private void OnModeSwitch(EventBus.GameEventArgs args)
        {
            var switchArgs = args as EventBus.ModeSwitchEventArgs;
            if (switchArgs != null)
                LogDebug($"Mode switched: {switchArgs.PreviousMode} → {switchArgs.NewMode}");
        }

        // ── UTILITY ────────────────────────────────────────────────────────────
        private void LogDebug(string message)
        {
            if (debugMode)
                Debug.Log($"[GameManager] {message}");
        }

        // ── EDITOR HELPERS ─────────────────────────────────────────────────────
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(kotorDir) && !Directory.Exists(kotorDir))
                Debug.LogWarning($"[GameManager] KotOR directory does not exist: '{kotorDir}'");
        }
#endif
    }
}
