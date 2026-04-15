using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KotORUnity.KotOR.FileReaders;

// ResourceType enum lives in KotORUnity.KotOR.FileReaders (ResourceTypes.cs)
// We re-export it here for convenience so callers can use just ResourceType.

namespace KotORUnity.Bootstrap
{
    /// <summary>
    /// Central resource manager. Mounts BIF, ERF, RIM and loose Override files
    /// from the KotOR installation and resolves resource requests by name + type.
    ///
    /// Resolution order (highest priority first):
    ///   1. Override folder  (loose files in /Override)
    ///   2. MOD archives     (module-specific ERF-format .mod files)
    ///   3. RIM archives     (module .rim files)
    ///   4. ERF archives     (general .erf files — texturepacks etc.)
    ///   5. BIF archives     (base game data via chitin.key)
    ///
    /// Uses the existing static BifReader / ErfReader / RimReader helpers.
    /// KEY file parsing is handled inline since BifReader only parses BIF data.
    /// </summary>
    public class ResourceManager
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static ResourceManager Instance { get; set; }

        // ── PRIVATE TYPES ─────────────────────────────────────────────────────
        private enum ResourceSource { Override, ERF, RIM, BIF }

        private class ResourceRecord
        {
            public ResourceSource Source;
            public int            Priority;
            // Override
            public string         FilePath = "";
            // ERF / RIM: we store the entry directly for direct lookup
            public ErfReader.ErfResourceEntry ErfEntry;
            public RimReader.RimResourceEntry RimEntry;
            // BIF: direct entry
            public BifReader.BifResourceEntry BifEntry;
        }

        // ── FLAT INDEX ────────────────────────────────────────────────────────
        // "resref|restype_ushort" → best record
        private readonly Dictionary<string, ResourceRecord> _index
            = new Dictionary<string, ResourceRecord>(StringComparer.OrdinalIgnoreCase);

        private string _gameRoot = "";
        public  bool   IsMounted   { get; private set; }
        /// <summary>Total number of indexed resource entries after mounting.</summary>
        public  int    EntryCount  => _index.Count;

        // ── MOUNT ─────────────────────────────────────────────────────────────
        /// <summary>Mount all archives from a KotOR root directory.</summary>
        public void Mount(string gameRoot)
        {
            _gameRoot = gameRoot;
            _index.Clear();
            IsMounted = false;

            if (!Directory.Exists(gameRoot))
            {
                Debug.LogWarning($"[ResourceManager] Directory not found: {gameRoot}");
                return;
            }

            Debug.Log($"[ResourceManager] Mounting: {gameRoot}");

            // 1. Override folder (priority 10)
            string overrideDir = Path.Combine(gameRoot, "Override");
            if (!Directory.Exists(overrideDir))
                overrideDir = Path.Combine(gameRoot, "override");
            if (Directory.Exists(overrideDir))
                IndexOverrideFolder(overrideDir);

            // 2. ERF archives in /data (priority 2)
            string dataDir = Path.Combine(gameRoot, "data");
            if (!Directory.Exists(dataDir)) dataDir = Path.Combine(gameRoot, "Data");
            if (Directory.Exists(dataDir))
            {
                foreach (var erf in Directory.GetFiles(dataDir, "*.erf", SearchOption.TopDirectoryOnly))
                    IndexErfFile(erf, 2);
            }

            // 3. BIF archives via chitin.key (priority 1)
            string keyPath = Path.Combine(gameRoot, "chitin.key");
            if (!File.Exists(keyPath)) keyPath = Path.Combine(gameRoot, "Chitin.key");
            if (File.Exists(keyPath))
                IndexBifFiles(keyPath, gameRoot);

            IsMounted = true;
            Debug.Log($"[ResourceManager] Mount complete. Index: {_index.Count} resources.");
        }

        /// <summary>Mount a specific module's RIM / MOD archives (priority 3–4).</summary>
        public void MountModule(string moduleName)
        {
            string modulesDir = Path.Combine(_gameRoot, "modules");
            if (!Directory.Exists(modulesDir))
                modulesDir = Path.Combine(_gameRoot, "Modules");
            if (!Directory.Exists(modulesDir)) return;

            // .rim
            TryIndexRim(Path.Combine(modulesDir, moduleName + ".rim"), 3);
            TryIndexRim(Path.Combine(modulesDir, moduleName + "_s.rim"), 3);

            // .mod (ERF format, highest module priority)
            TryIndexErf(Path.Combine(modulesDir, moduleName + ".mod"), 4);

            Debug.Log($"[ResourceManager] Module '{moduleName}' mounted.");
        }

        // ── RESOURCE LOOKUP ───────────────────────────────────────────────────
        public byte[] GetResource(string resref, ResourceType resType)
        {
            string key = MakeKey(resref, resType);
            if (!_index.TryGetValue(key, out var rec)) return null;

            try
            {
                switch (rec.Source)
                {
                    case ResourceSource.Override:
                        return File.ReadAllBytes(rec.FilePath);

                    case ResourceSource.ERF:
                        return rec.ErfEntry != null
                            ? ErfReader.ReadResource(rec.ErfEntry)
                            : null;

                    case ResourceSource.RIM:
                        return rec.RimEntry != null
                            ? RimReader.ReadResource(rec.RimEntry)
                            : null;

                    case ResourceSource.BIF:
                        return rec.BifEntry != null
                            ? BifReader.ReadResource(rec.BifEntry)
                            : null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceManager] Error reading {resref}.{resType}: {e.Message}");
            }

            return null;
        }

        public bool HasResource(string resref, ResourceType resType)
            => _index.ContainsKey(MakeKey(resref, resType));

        /// <summary>
        /// Mount an additional Override folder at runtime (used by ModLoader).
        /// Files in this folder take highest priority over all other sources.
        /// </summary>
        public void MountOverride(string overrideFolderPath)
        {
            if (!System.IO.Directory.Exists(overrideFolderPath))
            {
                UnityEngine.Debug.LogWarning($"[ResourceManager] MountOverride: path not found: {overrideFolderPath}");
                return;
            }
            IndexOverrideFolder(overrideFolderPath);
            UnityEngine.Debug.Log($"[ResourceManager] Mounted override folder: {overrideFolderPath}");
        }

        // ── ASSET BROWSER SUPPORT ─────────────────────────────────────────────

        /// <summary>
        /// Expose the full index for the AssetBrowser.
        /// Returns an enumerable of ((resref, resType), KotORResourceRef) pairs.
        /// </summary>
        public IEnumerable<KeyValuePair<(string resref, ushort resType), KotOR.FileReaders.KotORResourceRef>>
            GetAllEntries()
        {
            foreach (var kv in _index)
            {
                // Parse the composite key: "resref|restype"
                int sep = kv.Key.LastIndexOf('|');
                if (sep < 0) continue;
                string resref = kv.Key.Substring(0, sep);
                if (!ushort.TryParse(kv.Key.Substring(sep + 1), out ushort resType)) continue;

                var record = kv.Value;
                var refObj = new KotOR.FileReaders.KotORResourceRef
                {
                    ResRef       = resref,
                    ResType      = resType,
                    ArchiveType  = record.Source switch
                    {
                        ResourceSource.Override => KotOR.FileReaders.ArchiveType.Override,
                        ResourceSource.ERF      => KotOR.FileReaders.ArchiveType.ERF,
                        ResourceSource.RIM      => KotOR.FileReaders.ArchiveType.RIM,
                        _                       => KotOR.FileReaders.ArchiveType.BIF
                    }
                };

                // Populate source archive path + size
                if (record.ErfEntry  != null) { refObj.SourceArchive = record.ErfEntry.ErfFilePath;  refObj.Size = record.ErfEntry.FileSize; }
                else if (record.RimEntry != null) { refObj.SourceArchive = record.RimEntry.RimFilePath; refObj.Size = record.RimEntry.FileSize; }
                else if (record.BifEntry != null) { refObj.SourceArchive = record.BifEntry.FilePath;    refObj.Size = record.BifEntry.FileSize; }
                else if (!string.IsNullOrEmpty(record.FilePath))
                {
                    refObj.SourceArchive = record.FilePath;
                    try { refObj.Size = new System.IO.FileInfo(record.FilePath).Length; } catch { }
                }

                yield return new KeyValuePair<(string, ushort), KotOR.FileReaders.KotORResourceRef>(
                    (resref, resType), refObj);
            }
        }
        private void IndexOverrideFolder(string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
            {
                string resref = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                string ext    = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                var    rt     = ExtToResourceType(ext);
                if (rt == ResourceType.Invalid) continue;

                Upsert(MakeKey(resref, rt), new ResourceRecord
                {
                    Source   = ResourceSource.Override,
                    FilePath = file,
                    Priority = 10
                });
            }
        }

        private void IndexErfFile(string path, int priority)
        {
            var (_, entries) = ErfReader.ReadErfArchive(path);
            if (entries == null) return;

            foreach (var e in entries)
            {
                string key = MakeKey(e.ResRef, (ResourceType)e.ResType);
                Upsert(key, new ResourceRecord
                {
                    Source   = ResourceSource.ERF,
                    ErfEntry = e,
                    Priority = priority
                });
            }
        }

        private void TryIndexErf(string path, int priority)
        {
            if (File.Exists(path)) IndexErfFile(path, priority);
        }

        private void TryIndexRim(string path, int priority)
        {
            if (!File.Exists(path)) return;
            var entries = RimReader.ReadRimArchive(path);
            if (entries == null) return;

            foreach (var e in entries)
            {
                string key = MakeKey(e.ResRef, (ResourceType)e.ResType);
                Upsert(key, new ResourceRecord
                {
                    Source   = ResourceSource.RIM,
                    RimEntry = e,
                    Priority = priority
                });
            }
        }

        // ── KEY FILE + BIF INDEXING ───────────────────────────────────────────
        /// <summary>
        /// Parse chitin.key to discover all BIF archives and index their contents.
        ///
        /// KEY format (little-endian):
        ///   Header (64 bytes):
        ///     char[4]  Signature  "KEY "
        ///     char[4]  Version    "V1  "
        ///     uint32   BIFCount
        ///     uint32   KeyCount
        ///     uint32   OffsetToFileTable   (BIF list)
        ///     uint32   OffsetToKeyTable    (resource → BIF mapping)
        ///     ... build date, reserved
        ///
        ///   BIF table entry (12 bytes each):
        ///     uint32   FileSize
        ///     uint32   FilenameOffset  (from start of key file)
        ///     uint16   FilenameSize
        ///     uint16   Drives          (flags, usually 0x0001 = HD)
        ///
        ///   Key table entry (22 bytes each):
        ///     char[16] ResRef
        ///     uint16   ResType
        ///     uint32   ResID   (high 20 bits = BIF index, low 12 bits = resource index)
        /// </summary>
        private void IndexBifFiles(string keyPath, string gameRoot)
        {
            try
            {
                byte[] keyData = File.ReadAllBytes(keyPath);
                using var ms = new MemoryStream(keyData);
                using var br = new BinaryReader(ms, Encoding.ASCII);

                // Header
                string sig = new string(br.ReadChars(4));
                string ver = new string(br.ReadChars(4));
                if (!sig.StartsWith("KEY"))
                {
                    Debug.LogWarning($"[ResourceManager] Not a KEY file: {keyPath}");
                    return;
                }

                uint bifCount = br.ReadUInt32();
                uint keyCount = br.ReadUInt32();
                uint bifTableOffset = br.ReadUInt32();
                uint keyTableOffset = br.ReadUInt32();

                // Read BIF filename table
                ms.Seek(bifTableOffset, SeekOrigin.Begin);
                var bifEntries = new (uint fileSize, uint nameOffset, ushort nameSize, ushort drives)[bifCount];
                for (uint i = 0; i < bifCount; i++)
                {
                    uint   fs   = br.ReadUInt32();
                    uint   no   = br.ReadUInt32();
                    ushort ns   = br.ReadUInt16();
                    ushort drv  = br.ReadUInt16();
                    bifEntries[i] = (fs, no, ns, drv);
                }

                // Resolve BIF paths
                string[] bifPaths = new string[bifCount];
                for (uint i = 0; i < bifCount; i++)
                {
                    ms.Seek(bifEntries[i].nameOffset, SeekOrigin.Begin);
                    string bifName = Encoding.ASCII.GetString(
                        br.ReadBytes(bifEntries[i].nameSize)).TrimEnd('\0');
                    // BIF names use backslashes; normalise
                    bifName = bifName.Replace('\\', Path.DirectorySeparatorChar)
                                     .Replace('/', Path.DirectorySeparatorChar);
                    bifPaths[i] = Path.Combine(gameRoot, bifName);
                }

                // Read KEY table and index resources per BIF
                ms.Seek(keyTableOffset, SeekOrigin.Begin);
                // Pre-parse each BIF's resource table
                var bifResourceTables = new Dictionary<int, List<BifReader.BifResourceEntry>>();

                for (uint i = 0; i < keyCount; i++)
                {
                    string resref  = new string(br.ReadChars(16)).TrimEnd('\0');
                    ushort resType = br.ReadUInt16();
                    uint   resId   = br.ReadUInt32();

                    int bifIdx      = (int)(resId >> 20);
                    int resourceIdx = (int)(resId & 0xFFFFF);

                    if (bifIdx >= bifPaths.Length) continue;

                    // Lazy-load the BIF resource table
                    if (!bifResourceTables.TryGetValue(bifIdx, out var bifTable))
                    {
                        bifTable = BifReader.ReadResourceTable(bifPaths[bifIdx]);
                        bifResourceTables[bifIdx] = bifTable;
                    }

                    // Find matching entry
                    BifReader.BifResourceEntry matchEntry = null;
                    foreach (var e in bifTable)
                    {
                        if ((int)(e.ID & 0xFFFFF) == resourceIdx &&
                            e.ResourceType == resType)
                        {
                            matchEntry = e;
                            break;
                        }
                    }

                    if (matchEntry == null) continue;

                    string key = MakeKey(resref, (ResourceType)resType);
                    Upsert(key, new ResourceRecord
                    {
                        Source   = ResourceSource.BIF,
                        BifEntry = matchEntry,
                        Priority = 1
                    });
                }

                Debug.Log($"[ResourceManager] BIF index: {bifCount} archives, " +
                          $"{keyCount} key entries.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceManager] KEY parse error: {e.Message}\n{e.StackTrace}");
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private void Upsert(string key, ResourceRecord record)
        {
            if (!_index.TryGetValue(key, out var existing) ||
                existing.Priority < record.Priority)
            {
                _index[key] = record;
            }
        }

        private static string MakeKey(string resref, ResourceType rt)
            => $"{resref.ToLowerInvariant()}|{(ushort)rt}";

        private static ResourceType ExtToResourceType(string ext) => ext switch
        {
            "2da"  => ResourceType.TwoDA,
            "are"  => ResourceType.ARE,
            "git"  => ResourceType.GIT,
            "ifo"  => ResourceType.IFO,
            "utc"  => ResourceType.UTC,
            "uti"  => ResourceType.UTI,
            "utd"  => ResourceType.UTD,
            "utp"  => ResourceType.UTP,
            "dlg"  => ResourceType.DLG,
            "ncs"  => ResourceType.NCS,
            "wok"  => ResourceType.WOK,
            "tga"  => ResourceType.TGA,
            "dds"  => ResourceType.DDS,
            "mdl"  => ResourceType.MDL,
            "mdx"  => ResourceType.MDX,
            "wav"  => ResourceType.WAV,
            "ssf"  => ResourceType.SSF,
            "fac"  => ResourceType.FAC,
            "utm"  => ResourceType.UTM,
            "utw"  => ResourceType.UTW,
            "set"  => ResourceType.SET,
            "tlk"  => ResourceType.TLK,
            "bic"  => ResourceType.BIC,
            _      => ResourceType.Invalid
        };
    }
}
