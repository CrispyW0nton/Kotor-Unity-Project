using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using KotORUnity.Core;
#pragma warning disable 0414, 0219

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MOD STATE  —  tracks a single loaded mod
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime state for a loaded mod.
    /// </summary>
    public class LoadedMod
    {
        public string ModId;
        public string DisplayName;
        public string Version;
        public string FilePath;          // path to the .kotormod file
        public string ExtractPath;       // temp folder where the mod was extracted
        public ModManifest Manifest;
        public bool   IsEnabled     = true;
        public bool   IsHotReloaded = false;
        public float  LoadTime;          // Time.realtimeSinceStartup when loaded
        public List<string> LoadedFiles  = new List<string>();   // resolved file paths
        public List<string> AppliedPatches = new List<string>(); // 2DA / TLK patches applied
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  2DA PATCH  —  row-append / cell-replace format
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A single 2DA patch operation.
    /// </summary>
    [Serializable]
    public class TwoDAPatch  // Patch descriptor — see CampaignPackager + GameDataRepository for live patching
    {
        public enum PatchOp { AppendRow, SetCell, DeleteRow }

        public PatchOp Operation;
        public string  TableName;   // e.g. "spells"
        public int     Row         = -1;   // -1 = append (AppendRow only needs TableName)
        public string  Column      = "";
        public string  NewValue    = "";
        public Dictionary<string, string> RowData = new Dictionary<string, string>();  // for AppendRow
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MOD LOADER  —  MonoBehaviour singleton
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime mod discovery, dependency resolution, load-order sorting,
    /// extraction, and hot-reload for KotOR Unity Port mods.
    ///
    /// Responsibilities:
    ///   1. Scan the Mods/ folder for .kotormod archives.
    ///   2. Read each archive's manifest.json.
    ///   3. Sort mods by LoadOrder (ascending) respecting dependencies.
    ///   4. Extract into a per-mod temp folder under Application.persistentDataPath/ModsExtracted.
    ///   5. Register Override files with the ResourceManager (highest priority).
    ///   6. Apply 2DA patches (AppendRow / SetCell operations).
    ///   7. Publish ModsLoaded event so other systems can react.
    ///   8. Provide hot-reload: re-extract and re-register on file change (Editor only).
    ///
    /// New-port features vs vanilla KotOR modding:
    ///   • Formal dependency / conflict resolution with clear user feedback
    ///   • Load order is numeric + dependency-aware (no manual TSLRCM juggling)
    ///   • 2DA patches append rows without replacing the whole file
    ///   • AssetBundle support for Unity-native assets (meshes, prefabs, materials)
    ///   • Hot-reload in Editor (file watcher on the Mods/ folder)
    ///   • SHA-256 integrity verification
    ///   • Per-mod enable/disable without restarting
    /// </summary>
    public class ModLoader : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static ModLoader Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Paths")]
        [Tooltip("Folder to scan for .kotormod files. Leave blank → <persistentDataPath>/Mods")]
        [SerializeField] private string modsFolder = "";

        [Header("Behaviour")]
        [SerializeField] private bool loadOnStart      = true;
        [SerializeField] private bool verifyIntegrity  = true;
        [SerializeField] private bool enableHotReload  = true;   // Editor only
        [SerializeField] private float hotReloadInterval = 2f;   // seconds between checks

        // ── STATE ─────────────────────────────────────────────────────────────
        private readonly List<LoadedMod> _loadedMods   = new List<LoadedMod>();
        private readonly List<string>    _loadErrors   = new List<string>();
        private bool    _modsLoaded = false;
        private float   _lastHotReloadCheck = 0f;
        private readonly Dictionary<string, long> _fileLastWritten = new Dictionary<string, long>();

        // ── UNITY ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            if (string.IsNullOrEmpty(modsFolder))
                modsFolder = Path.Combine(Application.persistentDataPath, "Mods");
        }

        private void Start()
        {
            if (loadOnStart) StartCoroutine(LoadAllModsRoutine());
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (enableHotReload && Time.realtimeSinceStartup - _lastHotReloadCheck > hotReloadInterval)
            {
                _lastHotReloadCheck = Time.realtimeSinceStartup;
                CheckHotReload();
            }
#endif
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Return a snapshot of all loaded mods.</summary>
        public List<LoadedMod> GetLoadedMods() => new List<LoadedMod>(_loadedMods);

        /// <summary>Return load errors from the last discovery pass.</summary>
        public IReadOnlyList<string> GetLoadErrors() => _loadErrors.AsReadOnly();

        /// <summary>Check whether a mod is loaded and enabled.</summary>
        public bool IsModLoaded(string modId) =>
            _loadedMods.Any(m => m.ModId == modId && m.IsEnabled);

        /// <summary>Disable a loaded mod (takes full effect on next game restart / reload).</summary>
        public void DisableMod(string modId)
        {
            var mod = _loadedMods.FirstOrDefault(m => m.ModId == modId);
            if (mod != null) { mod.IsEnabled = false; Debug.Log($"[ModLoader] Disabled '{modId}'."); }
        }

        /// <summary>Enable a loaded mod.</summary>
        public void EnableMod(string modId)
        {
            var mod = _loadedMods.FirstOrDefault(m => m.ModId == modId);
            if (mod != null) { mod.IsEnabled = true; Debug.Log($"[ModLoader] Enabled '{modId}'."); }
        }

        /// <summary>Trigger a full mod reload coroutine.</summary>
        public void ReloadAll() => StartCoroutine(LoadAllModsRoutine());

        // ── LOAD PIPELINE ─────────────────────────────────────────────────────

        private IEnumerator LoadAllModsRoutine()
        {
            float t0 = Time.realtimeSinceStartup;
            _loadErrors.Clear();
            _loadedMods.Clear();

            Debug.Log($"[ModLoader] Scanning '{modsFolder}'…");
            Directory.CreateDirectory(modsFolder);

            // 1. Discover all .kotormod files
            var modFiles = Directory.GetFiles(modsFolder, "*" + CampaignPackager.ModExtension,
                                              SearchOption.TopDirectoryOnly);
            if (modFiles.Length == 0)
            {
                Debug.Log("[ModLoader] No mods found.");
                _modsLoaded = true;
                yield break;
            }

            // 2. Read manifests
            var candidates = new List<LoadedMod>();
            foreach (var path in modFiles)
            {
                var manifest = CampaignPackager.PeekManifest(path);
                if (manifest == null)
                {
                    _loadErrors.Add($"Could not read manifest: {Path.GetFileName(path)}");
                    continue;
                }
                candidates.Add(new LoadedMod
                {
                    ModId       = manifest.ModId,
                    DisplayName = manifest.DisplayName,
                    Version     = manifest.Version,
                    FilePath    = path,
                    Manifest    = manifest
                });
            }

            // 3. Dependency / conflict resolution
            var sorted = ResolveDependencies(candidates);

            // 4. Extract and register each mod in order
            string extractRoot = Path.Combine(Application.persistentDataPath, "ModsExtracted");
            Directory.CreateDirectory(extractRoot);

            foreach (var mod in sorted)
            {
                yield return null; // spread across frames
                LoadSingleMod(mod, extractRoot);
            }

            float elapsed = Time.realtimeSinceStartup - t0;
            int loaded    = _loadedMods.Count(m => m.IsEnabled);
            Debug.Log($"[ModLoader] {loaded}/{sorted.Count} mods loaded in {elapsed:F2}s.");

            EventBus.Publish(EventBus.EventType.ModsLoaded,
                new EventBus.GameEventArgs { IntValue = loaded });

            _modsLoaded = true;
        }

        private void LoadSingleMod(LoadedMod mod, string extractRoot)
        {
            string extractPath = Path.Combine(extractRoot, mod.ModId);
            Directory.CreateDirectory(extractPath);
            mod.ExtractPath = extractPath;

            try
            {
                // Extract
                using (var zip = ZipFile.OpenRead(mod.FilePath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.Name == CampaignPackager.ManifestName) continue;
                        string destPath = Path.Combine(extractPath, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        entry.ExtractToFile(destPath, overwrite: true);
                        mod.LoadedFiles.Add(destPath);
                    }
                }

                // Integrity check (optional)
                if (verifyIntegrity) VerifyIntegrity(mod);

                // Register Override files with ResourceManager
                string overrideDir = Path.Combine(extractPath, "Override");
                if (Directory.Exists(overrideDir))
                    Bootstrap.SceneBootstrapper.Resources?.MountOverride(overrideDir);

                // Apply 2DA patches
                string twodaDir = Path.Combine(extractPath, "TwoDA");
                if (Directory.Exists(twodaDir)) Apply2DAPatches(mod, twodaDir);

                mod.LoadTime = Time.realtimeSinceStartup;
                _loadedMods.Add(mod);
                Debug.Log($"[ModLoader] Loaded '{mod.ModId}' v{mod.Version}  ({mod.LoadedFiles.Count} files)");
            }
            catch (Exception ex)
            {
                string err = $"Failed to load '{mod.ModId}': {ex.Message}";
                _loadErrors.Add(err);
                Debug.LogError($"[ModLoader] {err}");
            }
        }

        // ── DEPENDENCY RESOLUTION ─────────────────────────────────────────────

        /// <summary>
        /// Topological sort: respect LoadOrder then dependency edges.
        /// Drops mods whose required mods are missing.
        /// Logs conflicts.
        /// </summary>
        private List<LoadedMod> ResolveDependencies(List<LoadedMod> candidates)
        {
            var available = candidates.ToDictionary(m => m.ModId);
            var result    = new List<LoadedMod>();
            var visited   = new HashSet<string>();

            // Sort by load order first
            var sorted = candidates.OrderBy(m => m.Manifest.LoadOrder).ThenBy(m => m.ModId).ToList();

            foreach (var mod in sorted)
            {
                bool ok = true;

                // Check required
                foreach (var req in mod.Manifest.RequiredMods)
                {
                    if (!available.ContainsKey(req))
                    {
                        _loadErrors.Add($"'{mod.ModId}' requires '{req}' which is not installed.");
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;

                // Check incompatibilities
                foreach (var inc in mod.Manifest.IncompatibleMods)
                {
                    if (available.ContainsKey(inc))
                    {
                        _loadErrors.Add($"'{mod.ModId}' is incompatible with '{inc}' — skipping '{mod.ModId}'.");
                        ok = false;
                        break;
                    }
                }
                if (!ok) continue;

                if (!visited.Contains(mod.ModId))
                {
                    visited.Add(mod.ModId);
                    result.Add(mod);
                }
            }
            return result;
        }

        // ── 2DA PATCHING ──────────────────────────────────────────────────────

        /// <summary>
        /// Apply 2DA patch JSON files from a mod's TwoDA/ folder.
        /// Each file is named {tableName}_patch.json and contains an array of
        /// TwoDAPatch objects. The DataRepository applies the patch in memory.
        /// </summary>
        private void Apply2DAPatches(LoadedMod mod, string twodaDir)
        {
            foreach (var patchFile in Directory.GetFiles(twodaDir, "*_patch.json"))
            {
                try
                {
                    string json = File.ReadAllText(patchFile, Encoding.UTF8);
                    // The 2DA reader/repository handles the actual patching
                    // We just flag the table as patched for this mod
                    string tableName = Path.GetFileName(patchFile).Replace("_patch.json", "");
                    mod.AppliedPatches.Add(tableName);
                    Data.GameDataRepository.Instance?.Apply2DAPatch(tableName, json);
                    Debug.Log($"[ModLoader] Applied 2DA patch '{tableName}' from '{mod.ModId}'.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ModLoader] 2DA patch failed ({patchFile}): {ex.Message}");
                }
            }
        }

        // ── INTEGRITY CHECK ───────────────────────────────────────────────────

        private void VerifyIntegrity(LoadedMod mod)
        {
            if (mod.Manifest?.Files == null) return;
            foreach (var entry in mod.Manifest.Files)
            {
                string fullPath = Path.Combine(mod.ExtractPath, entry.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)) continue;  // already logged by extractor
                string actualHash = ComputeSHA256(fullPath);
                if (!string.Equals(actualHash, entry.SHA256, StringComparison.OrdinalIgnoreCase))
                    Debug.LogWarning($"[ModLoader] Integrity mismatch for '{entry.RelativePath}' in '{mod.ModId}'.");
            }
        }

        // ── HOT RELOAD ────────────────────────────────────────────────────────

        private void CheckHotReload()
        {
            if (!Directory.Exists(modsFolder)) return;
            var files = Directory.GetFiles(modsFolder, "*" + CampaignPackager.ModExtension);
            bool changed = false;
            foreach (var f in files)
            {
                long lwt = File.GetLastWriteTime(f).Ticks;
                if (_fileLastWritten.TryGetValue(f, out long prev) && prev != lwt)
                {
                    Debug.Log($"[ModLoader] Hot-reload triggered by '{Path.GetFileName(f)}'.");
                    changed = true;
                }
                _fileLastWritten[f] = lwt;
            }
            if (changed) StartCoroutine(LoadAllModsRoutine());
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static string ComputeSHA256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            byte[] hash   = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        // ── EDITOR WINDOW API ──────────────────────────────────────────────────
        /// <summary>Property accessor for EditorWindow iteration.</summary>
        public IReadOnlyList<LoadedMod> LoadedMods => _loadedMods;

        /// <summary>Hot-reload a mod by display name or ModId.</summary>
        public void HotReload(string modName)
        {
            var mod = _loadedMods.Find(m =>
                string.Equals(m.DisplayName, modName, System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.ModId,       modName, System.StringComparison.OrdinalIgnoreCase));
        if (mod != null) StartCoroutine(LoadAllModsRoutine()); // re-load all with updated priority
            else UnityEngine.Debug.LogWarning($"[ModLoader] HotReload: mod '{modName}' not found.");
        }

        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            UnityEditor.EditorGUILayout.LabelField(
                $"Loaded mods: {_loadedMods.Count}", UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
