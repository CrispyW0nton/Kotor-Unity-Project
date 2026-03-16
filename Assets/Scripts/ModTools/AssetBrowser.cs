using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.KotOR.Parsers;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ASSET ENTRY  —  one row in the browser
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents one resource in the browser's search results.
    /// </summary>
    public class AssetEntry
    {
        public string   ResRef;         // e.g. "n_mandalorian01"
        public ushort   ResType;        // ResourceType constant
        public string   TypeLabel;      // e.g. "MDL", "TGA", "UTC"
        public string   SourceArchive;  // e.g. "data/models.bif"
        public ArchiveType ArchiveType;
        public long     Size;           // bytes

        public string DisplayName => $"{ResRef}.{TypeLabel.ToLowerInvariant()}";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ASSET BROWSER  —  runtime component (also used by Editor window)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// KotOR Asset Browser — query the ResourceManager's index and retrieve
    /// information / raw bytes for any resource in the mounted archives.
    ///
    /// Used by:
    ///   1. The Unity Editor window (AssetBrowserEditorWindow.cs)
    ///   2. The in-game dev console for quick previews
    ///   3. Mod tools at runtime
    ///
    /// Thread-safety: all public methods are safe to call from the main thread.
    /// Index building is synchronous; for large installs consider running on a
    /// background thread and posting results back via a queue.
    /// </summary>
    public class AssetBrowser
    {
        // ── TYPE LABEL TABLE ─────────────────────────────────────────────────
        private static readonly Dictionary<ResourceType, string> TypeLabels =
            new Dictionary<ResourceType, string>
            {
                { ResourceType.MDL,  "MDL"  }, { ResourceType.MDX,  "MDX"  },
                { ResourceType.TGA,  "TGA"  }, { ResourceType.DDS,  "DDS"  },
                { ResourceType.TPC,  "TPC"  }, { ResourceType.UTC,  "UTC"  },
                { ResourceType.UTI,  "UTI"  }, { ResourceType.UTD,  "UTD"  },
                { ResourceType.UTP,  "UTP"  }, { ResourceType.UTM,  "UTM"  },
                { ResourceType.UTW,  "UTW"  }, { ResourceType.UTE,  "UTE"  },
                { ResourceType.UTS,  "UTS"  }, { ResourceType.UTT,  "UTT"  },
                { ResourceType.DLG,  "DLG"  }, { ResourceType.NCS,  "NCS"  },
                { ResourceType.NSS,  "NSS"  }, { ResourceType.ARE,  "ARE"  },
                { ResourceType.IFO,  "IFO"  }, { ResourceType.GIT,  "GIT"  },
                { ResourceType.LYT,  "LYT"  }, { ResourceType.VIS,  "VIS"  },
                { ResourceType.WOK,  "WOK"  }, { ResourceType.LIP,  "LIP"  },
                { ResourceType.WAV,  "WAV"  }, { ResourceType.MP3,  "MP3"  },
                { ResourceType.TwoDA,"2DA"  }, { ResourceType.SSF,  "SSF"  },
                { ResourceType.FAC,  "FAC"  }, { ResourceType.BIC,  "BIC"  },
                { ResourceType.TLK,  "TLK"  }, { ResourceType.GUI,  "GUI"  },
            };

        // ── INDEX ─────────────────────────────────────────────────────────────
        private List<AssetEntry>    _index  = new List<AssetEntry>();
        private bool                _indexed = false;

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>
        /// Build the flat search index from ResourceManager.
        /// Call once after archives are mounted (i.e., after SceneBootstrapper.Mount).
        /// </summary>
        public void BuildIndex()
        {
            _index.Clear();
            var rm = SceneBootstrapper.Resources;
            if (rm == null)
            {
                Debug.LogWarning("[AssetBrowser] ResourceManager not available yet.");
                return;
            }

            foreach (var kv in rm.GetAllEntries())
            {
                var entry = new AssetEntry
                {
                    ResRef      = kv.Key.resref,
                    ResType     = kv.Key.resType,
                    TypeLabel   = TypeLabels.TryGetValue((ResourceType)kv.Key.resType, out var lbl) ? lbl
                                    : kv.Key.resType.ToString(),
                    SourceArchive = kv.Value.SourceArchive,
                    ArchiveType   = kv.Value.ArchiveType,
                    Size          = kv.Value.Size
                };
                _index.Add(entry);
            }

            _index.Sort((a, b) => string.Compare(a.ResRef, b.ResRef,
                                                   StringComparison.OrdinalIgnoreCase));
            _indexed = true;
            Debug.Log($"[AssetBrowser] Index built: {_index.Count:N0} resources.");
        }

        /// <summary>Total number of indexed resources.</summary>
        public int TotalCount => _index.Count;
        public bool IsIndexed  => _indexed;

        /// <summary>
        /// Search by partial resref name, optionally filtered to a specific type.
        /// Returns up to <paramref name="maxResults"/> entries.
        /// </summary>
        public List<AssetEntry> Search(string query, ushort typeFilter = 0,
                                        int maxResults = 500)
        {
            if (!_indexed) BuildIndex();

            bool hasQuery  = !string.IsNullOrWhiteSpace(query);
            bool hasFilter = typeFilter != 0;

            var results = new List<AssetEntry>(maxResults);
            foreach (var entry in _index)
            {
                if (hasQuery && entry.ResRef.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (hasFilter && entry.ResType != typeFilter)
                    continue;
                results.Add(entry);
                if (results.Count >= maxResults) break;
            }
            return results;
        }

        /// <summary>Get all entries for a given type.</summary>
        public List<AssetEntry> GetByType(ushort resType) => Search("", resType);

        /// <summary>Fetch the raw bytes for an asset.</summary>
        public byte[] GetBytes(AssetEntry entry)
        {
            var rm = SceneBootstrapper.Resources;
            return rm?.GetResource(entry.ResRef, (ResourceType)entry.ResType);
        }

        /// <summary>Fetch raw bytes by resref + type.</summary>
        public byte[] GetBytes(string resref, ushort resType)
        {
            var rm = SceneBootstrapper.Resources;
            return rm?.GetResource(resref, (ResourceType)resType);
        }

        /// <summary>Decode a texture asset into a Unity Texture2D.</summary>
        public Texture2D GetTexture(AssetEntry entry)
        {
            byte[] data = GetBytes(entry);
            return data == null ? null : TextureLoader.Decode(data, entry.ResRef);
        }

        /// <summary>Parse a GFF asset into a GffStruct.</summary>
        public KotOR.Parsers.GffReader.GffStruct GetGff(AssetEntry entry)
        {
            byte[] data = GetBytes(entry);
            return data == null ? null : KotOR.Parsers.GffReader.Parse(data);
        }

        /// <summary>Export an asset to a file on disk.</summary>
        public bool Export(AssetEntry entry, string outputPath)
        {
            byte[] data = GetBytes(entry);
            if (data == null) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                File.WriteAllBytes(outputPath, data);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AssetBrowser] Export failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Export ALL resources matching a type filter to a folder.
        /// Returns count of successfully exported files.
        /// </summary>
        public int ExportAll(ushort typeFilter, string outputFolder)
        {
            var entries = GetByType(typeFilter);
            int count   = 0;
            foreach (var entry in entries)
            {
                string ext  = TypeLabels.TryGetValue((ResourceType)entry.ResType, out var lbl)
                            ? lbl.ToLowerInvariant() : "bin";
                string path = Path.Combine(outputFolder, $"{entry.ResRef}.{ext}");
                if (Export(entry, path)) count++;
            }
            return count;
        }

        // ── TYPE HELPERS ──────────────────────────────────────────────────────

        public static string GetTypeLabel(ushort resType) =>
            TypeLabels.TryGetValue((ResourceType)resType, out var lbl) ? lbl : resType.ToString();

        public static IEnumerable<(ushort id, string label)> AllKnownTypes() =>
            TypeLabels.Select(kv => ((ushort)kv.Key, kv.Value));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SINGLETON ACCESSOR  (MonoBehaviour wrapper for runtime use)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// MonoBehaviour wrapper so the AssetBrowser is accessible from the scene.
    /// Attach to any persistent GameObject (e.g., the GameBootstrap object).
    /// </summary>
    public class AssetBrowserService : MonoBehaviour
    {
        public static AssetBrowserService Instance { get; private set; }
        public AssetBrowser Browser { get; private set; } = new AssetBrowser();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Build index once archives are mounted
            // SceneBootstrapper publishes UIHUDRefresh after mounting; use that as signal
            Core.EventBus.Subscribe(Core.EventBus.EventType.UIHUDRefresh,
                _ => { if (!Browser.IsIndexed) Browser.BuildIndex(); });
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
