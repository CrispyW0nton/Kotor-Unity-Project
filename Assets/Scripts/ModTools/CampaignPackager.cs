using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CAMPAIGN PACKAGER  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Metadata written into every mod package's manifest.
    /// </summary>
    [Serializable]
    public class ModManifest
    {
        // ── IDENTITY ──────────────────────────────────────────────────────────
        public string ModId        = "my_mod_001";       // lowercase_underscore
        public string DisplayName  = "My Mod";
        public string Author       = "";
        public string Version      = "1.0.0";            // semver
        public string Description  = "";
        public string Url          = "";                  // website / Nexus page
        public string License      = "CC BY-NC-SA 4.0";

        // ── COMPATIBILITY ─────────────────────────────────────────────────────
        public string EngineMinVersion = "0.9.0";
        public string EngineMaxVersion = "";              // empty = no upper limit
        public string Game             = "kotor1";        // kotor1 | kotor2 | both
        public List<string> RequiredMods  = new List<string>();  // ModId list
        public List<string> IncompatibleMods = new List<string>();

        // ── CONTENT ───────────────────────────────────────────────────────────
        public List<string> IncludedModules  = new List<string>();
        public List<string> IncludedItems    = new List<string>();
        public List<string> IncludedCreatures = new List<string>();
        public List<string> Override2DAs     = new List<string>();  // 2DA files that patch base data
        public List<string> OverrideTLK      = new List<string>();  // TLK patches
        public bool         ContainsScript   = false;
        public bool         ContainsTextures = false;
        public bool         ContainsModels   = false;
        public bool         ContainsAudio    = false;

        // ── LOAD ORDER ────────────────────────────────────────────────────────
        /// <summary>0–999; lower number loads first.</summary>
        public int LoadOrder = 100;
        public bool LoadEarly = false;   // load before game data (replaces base files)

        // ── FILE LIST ─────────────────────────────────────────────────────────
        public List<ManifestFileEntry> Files = new List<ManifestFileEntry>();

        // ── GENERATED ─────────────────────────────────────────────────────────
        public string BuildDate    = "";
        public string EngineBuiltWith = "0.9.0";
        public long   PackageSizeBytes = 0;
    }

    /// <summary>
    /// One file entry in the manifest.
    /// </summary>
    [Serializable]
    public class ManifestFileEntry
    {
        public string RelativePath;   // e.g. "Override/my_item.uti"
        public string SHA256;
        public long   SizeBytes;
        public string AssetType;      // "UTI", "UTC", "MOD_DEF", "2DA", "TLK", "LIP", "WAV", etc.
    }

    /// <summary>
    /// Build result returned from PackageMod.
    /// </summary>
    public class PackageResult
    {
        public bool   Success;
        public string OutputPath;
        public int    FileCount;
        public long   SizeBytes;
        public List<string> Warnings = new List<string>();
        public string ErrorMessage;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CAMPAIGN PACKAGER SERVICE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Packages a mod's authored assets into a distributable .kotormod archive.
    ///
    /// Format: ZIP archive renamed to .kotormod containing:
    ///   manifest.json          — ModManifest (human-readable)
    ///   Override/              — loose-file overrides (UTI, UTC, 2DA, TGA, etc.)
    ///   Modules/               — .mod_def.json files → will be loaded as modules
    ///   Items/                 — .item_def.json files
    ///   Creatures/             — .utc_def.json files
    ///   ForcePowers/           — .fp_def.json files
    ///   Scripts/               — compiled C# DLL or NWScript .nss patches
    ///   Textures/              — .tga / .dds / .png texture replacements
    ///   Audio/                 — .wav / .mp3 sound replacements
    ///   Dialogue/              — patched .dlg JSON files
    ///   TwoDA/                 — 2DA patch files (add rows / replace values)
    ///
    /// The ModLoader extracts this at runtime and hot-reloads assets.
    ///
    /// New-port features unlocked vs vanilla Override folder:
    ///   • Proper dependency / conflict detection
    ///   • SHA-256 integrity checking
    ///   • Semantic versioning
    ///   • Load-order manifest
    ///   • 2DA patch rows (append or modify specific cells) instead of file replacement
    ///   • TLK patch records (add/replace string entries by StrRef)
    /// </summary>
    public class CampaignPackager
    {
        // ── CONSTANTS ─────────────────────────────────────────────────────────
        public const string ModExtension    = ".kotormod";
        public const string ManifestName    = "manifest.json";
        public const string EngineVersion   = "0.9.0";

        // Recognised source folder names and their destinations in the package
        private static readonly Dictionary<string, string> FolderMap = new Dictionary<string, string>
        {
            { "Modules",     "Modules"     },
            { "Items",       "Items"       },
            { "Creatures",   "Creatures"   },
            { "ForcePowers", "ForcePowers" },
            { "Scripts",     "Scripts"     },
            { "Textures",    "Textures"    },
            { "Audio",       "Audio"       },
            { "Dialogue",    "Dialogue"    },
            { "TwoDA",       "TwoDA"       },
            { "Override",    "Override"    },
        };

        // ── STATE ─────────────────────────────────────────────────────────────
        public ModManifest Manifest { get; private set; } = new ModManifest();
        public string LastError     { get; private set; } = "";

        // Source directory supplied by the caller
        private string _sourceRoot = "";

        // ── CONFIGURATION ─────────────────────────────────────────────────────

        /// <summary>Set the source root folder containing the mod's unpackaged assets.</summary>
        public void SetSourceRoot(string folder)
        {
            _sourceRoot = folder;
            Manifest.Files.Clear();
        }

        /// <summary>Load an existing manifest from a packaged .kotormod for editing.</summary>
        public bool LoadManifest(string modFilePath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(modFilePath);
                var entry = zip.GetEntry(ManifestName);
                if (entry == null) { LastError = "manifest.json not found in archive."; return false; }
                using var sr = new StreamReader(entry.Open());
                Manifest = JsonUtility.FromJson<ModManifest>(sr.ReadToEnd());
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; return false; }
        }

        // ── PACKAGE ───────────────────────────────────────────────────────────

        /// <summary>
        /// Build the .kotormod archive from the source root.
        /// </summary>
        /// <param name="outputFolder">Where to write the .kotormod file.</param>
        public PackageResult PackageMod(string outputFolder = "")
        {
            var result = new PackageResult();

            // Validate
            var manifestIssues = ValidateManifest();
            foreach (var w in manifestIssues.Where(i => i.StartsWith("WARNING")))
                result.Warnings.Add(w);
            if (manifestIssues.Any(i => i.StartsWith("ERROR")))
            {
                result.Success = false;
                result.ErrorMessage = string.Join("\n", manifestIssues.Where(i => i.StartsWith("ERROR")));
                return result;
            }

            if (string.IsNullOrEmpty(outputFolder))
                outputFolder = Path.Combine(Application.persistentDataPath, "ModOutput/Packages");
            Directory.CreateDirectory(outputFolder);

            string safeName = SanitiseName(Manifest.ModId);
            string outputPath = Path.Combine(outputFolder, $"{safeName}_{Manifest.Version}{ModExtension}");
            string tempZip    = outputPath + ".tmp.zip";

            try
            {
                // Build manifest file list
                Manifest.Files.Clear();
                Manifest.BuildDate       = DateTime.UtcNow.ToString("o");
                Manifest.EngineBuiltWith = EngineVersion;

                if (!string.IsNullOrEmpty(_sourceRoot) && Directory.Exists(_sourceRoot))
                {
                    // Delete existing temp
                    if (File.Exists(tempZip)) File.Delete(tempZip);

                    using (var archive = ZipFile.Open(tempZip, ZipArchiveMode.Create))
                    {
                        foreach (var folderPair in FolderMap)
                        {
                            string srcFolder = Path.Combine(_sourceRoot, folderPair.Key);
                            if (!Directory.Exists(srcFolder)) continue;

                            foreach (var file in Directory.GetFiles(srcFolder, "*", SearchOption.AllDirectories))
                            {
                                string relPath = Path.Combine(folderPair.Value,
                                    file.Substring(srcFolder.Length).TrimStart('/', '\\'));
                                relPath = relPath.Replace('\\', '/');

                                archive.CreateEntryFromFile(file, relPath, System.IO.Compression.CompressionLevel.Optimal);

                                Manifest.Files.Add(new ManifestFileEntry
                                {
                                    RelativePath = relPath,
                                    SHA256       = ComputeSHA256(file),
                                    SizeBytes    = new FileInfo(file).Length,
                                    AssetType    = InferAssetType(file)
                                });

                                DetectContentFlags(file);
                                result.FileCount++;
                            }
                        }

                        // Write manifest
                        Manifest.PackageSizeBytes = new FileInfo(tempZip).Length;
                        var manifestEntry = archive.CreateEntry(ManifestName);
                        using (var sw = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                            sw.Write(JsonUtility.ToJson(Manifest, prettyPrint: true));
                    }

                    // Rename .tmp.zip → .kotormod
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    File.Move(tempZip, outputPath);
                }
                else
                {
                    // No source root — create manifest-only package for testing
                    using (var archive = ZipFile.Open(outputPath, ZipArchiveMode.Create))
                    {
                        var manifestEntry = archive.CreateEntry(ManifestName);
                        using (var sw = new StreamWriter(manifestEntry.Open(), Encoding.UTF8))
                            sw.Write(JsonUtility.ToJson(Manifest, prettyPrint: true));
                    }
                    result.Warnings.Add("No source root set — manifest-only package created.");
                }

                result.Success    = true;
                result.OutputPath = outputPath;
                result.SizeBytes  = new FileInfo(outputPath).Length;
                Debug.Log($"[CampaignPackager] Packaged → {outputPath} ({result.FileCount} files, {result.SizeBytes} bytes)");
                return result;
            }
            catch (Exception ex)
            {
                if (File.Exists(tempZip)) File.Delete(tempZip);
                result.Success      = false;
                result.ErrorMessage = ex.Message;
                LastError = ex.Message;
                Debug.LogError($"[CampaignPackager] Package failed: {ex.Message}");
                return result;
            }
        }

        // ── VALIDATION ────────────────────────────────────────────────────────

        public List<string> ValidateManifest()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Manifest.ModId))
                issues.Add("ERROR: ModId is empty.");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(Manifest.ModId, @"^[a-z0-9_]+$"))
                issues.Add($"WARNING: ModId '{Manifest.ModId}' should be lowercase alphanumeric + underscores only.");

            if (string.IsNullOrWhiteSpace(Manifest.DisplayName))
                issues.Add("WARNING: DisplayName is empty.");

            if (string.IsNullOrWhiteSpace(Manifest.Version))
                issues.Add("ERROR: Version is empty.");
            else if (!System.Text.RegularExpressions.Regex.IsMatch(Manifest.Version, @"^\d+\.\d+\.\d+"))
                issues.Add($"WARNING: Version '{Manifest.Version}' does not follow semver (e.g. '1.0.0').");

            if (string.IsNullOrWhiteSpace(Manifest.Author))
                issues.Add("WARNING: Author is empty.");

            // Check required mods don't overlap with incompatible
            foreach (var req in Manifest.RequiredMods)
                if (Manifest.IncompatibleMods.Contains(req))
                    issues.Add($"ERROR: '{req}' appears in both RequiredMods and IncompatibleMods.");

            return issues;
        }

        // ── LISTING ───────────────────────────────────────────────────────────

        /// <summary>List all .kotormod files in a folder.</summary>
        public static IEnumerable<string> ListPackages(string folder)
        {
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + ModExtension, SearchOption.TopDirectoryOnly);
        }

        /// <summary>Read only the manifest from a .kotormod without full extraction.</summary>
        public static ModManifest PeekManifest(string modPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(modPath);
                var entry = zip.GetEntry(ManifestName);
                if (entry == null) return null;
                using var sr = new StreamReader(entry.Open());
                return JsonUtility.FromJson<ModManifest>(sr.ReadToEnd());
            }
            catch { return null; }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private void DetectContentFlags(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".nss" or ".cs" or ".dll") Manifest.ContainsScript = true;
            if (ext is ".tga" or ".dds" or ".png" or ".tpc") Manifest.ContainsTextures = true;
            if (ext is ".mdl" or ".mdx") Manifest.ContainsModels = true;
            if (ext is ".wav" or ".mp3" or ".ogg") Manifest.ContainsAudio = true;
        }

        private static string InferAssetType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant().TrimStart('.');
            if (filePath.Contains("item_def"))     return "UTI";
            if (filePath.Contains("utc_def"))      return "UTC";
            if (filePath.Contains("mod_def"))      return "MOD_DEF";
            if (filePath.Contains("fp_def"))       return "FP_DEF";
            return ext.ToUpperInvariant();
        }

        private static string ComputeSHA256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs  = File.OpenRead(filePath);
            byte[] hash   = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static string SanitiseName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));

        // ── EDITOR GUI STUB ───────────────────────────────────────────────────
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            UnityEditor.EditorGUILayout.LabelField(
                $"Mod: {Manifest?.DisplayName ?? "(none)"}  ID: {Manifest?.ModId ?? ""}",
                UnityEditor.EditorStyles.helpBox);
#endif
        }

        // ── EDITOR WINDOW CONVENIENCE ALIASES ────────────────────────────────
        /// <summary>Alias for SetSourceRoot (used by EditorWindow).</summary>
        public void SetSourceFolder(string path) => SetSourceRoot(path);
        /// <summary>Alias for ValidateManifest (used by EditorWindow).</summary>
        public List<string> Validate() => ValidateManifest();
        /// <summary>Package the mod to a specific output file path.</summary>
        public PackageResult Package(string outputFilePath)
        {
            string folder = System.IO.Path.GetDirectoryName(outputFilePath);
            return PackageMod(folder);
        }
        /// <summary>Display name of the mod (for EditorWindow title bar).</summary>
        public string ModName => Manifest?.DisplayName;
    }
}
