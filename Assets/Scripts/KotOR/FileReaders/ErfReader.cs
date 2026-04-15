using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.KotOR.FileReaders
{
    /// <summary>
    /// Reads BioWare ERF (Encapsulated Resource Format) archive files.
    /// Used for module-specific files (.erf, .mod, .sav extensions).
    /// 
    /// ERF Format:
    ///   Header: signature (8 bytes) + language count + description size + entry count + offsets
    ///   Key Table: resource name + ID + type
    ///   Resource Table: offset + file size
    ///   Language strings (localised descriptions)
    ///   Data blocks
    /// </summary>
    public class ErfReader
    {
        public class ErfHeader
        {
            public string FileType;       // "ERF ", "MOD ", "SAV ", "HAK "
            public string FileVersion;    // "V1.0"
            public uint LanguageCount;
            public uint LocalizedStringSize;
            public uint EntryCount;
            public uint OffsetToLocalizedString;
            public uint OffsetToKeyList;
            public uint OffsetToResourceList;
            public uint BuildYear;
            public uint BuildDay;
            public string Description;
        }

        public class ErfResourceEntry
        {
            public string ResRef;    // Resource name (up to 16 chars)
            public uint ResID;
            public ushort ResType;
            public uint Offset;
            public uint FileSize;
            public string ErfFilePath;
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Open an ERF/MOD/SAV archive and return all resource entries.
        /// </summary>
        public static (ErfHeader header, List<ErfResourceEntry> entries) ReadErfArchive(string erfFilePath)
        {
            var entries = new List<ErfResourceEntry>();
            ErfHeader header = null;

            if (!File.Exists(erfFilePath))
            {
                Debug.LogError($"[ErfReader] ERF file not found: {erfFilePath}");
                return (null, entries);
            }

            try
            {
                using var fs = new FileStream(erfFilePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs, Encoding.ASCII);

                // Read header
                header = new ErfHeader
                {
                    FileType    = new string(br.ReadChars(4)),
                    FileVersion = new string(br.ReadChars(4))
                };

                if (header.FileVersion != "V1.0")
                    Debug.LogWarning($"[ErfReader] Unexpected ERF version: {header.FileVersion}");

                header.LanguageCount              = br.ReadUInt32();
                header.LocalizedStringSize         = br.ReadUInt32();
                header.EntryCount                  = br.ReadUInt32();
                header.OffsetToLocalizedString     = br.ReadUInt32();
                header.OffsetToKeyList             = br.ReadUInt32();
                header.OffsetToResourceList        = br.ReadUInt32();
                header.BuildYear                   = br.ReadUInt32();
                header.BuildDay                    = br.ReadUInt32();

                // Skip description hash (116 bytes)
                fs.Seek(116, SeekOrigin.Current);

                // Read key list
                fs.Seek(header.OffsetToKeyList, SeekOrigin.Begin);
                var keys = new (string resref, uint resId, ushort resType)[header.EntryCount];

                for (int i = 0; i < header.EntryCount; i++)
                {
                    char[] resrefChars = br.ReadChars(16);
                    string resref = new string(resrefChars).TrimEnd('\0');
                    uint resId = br.ReadUInt32();
                    ushort resType = br.ReadUInt16();
                    br.ReadUInt16(); // unused
                    keys[i] = (resref, resId, resType);
                }

                // Read resource list
                fs.Seek(header.OffsetToResourceList, SeekOrigin.Begin);
                for (int i = 0; i < header.EntryCount; i++)
                {
                    uint offset = br.ReadUInt32();
                    uint fileSize = br.ReadUInt32();
                    entries.Add(new ErfResourceEntry
                    {
                        ResRef = keys[i].resref,
                        ResID = keys[i].resId,
                        ResType = keys[i].resType,
                        Offset = offset,
                        FileSize = fileSize,
                        ErfFilePath = erfFilePath
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ErfReader] Error reading {erfFilePath}: {e.Message}");
            }

            return (header, entries);
        }

        /// <summary>Read the raw bytes of a resource from an ERF archive.</summary>
        public static byte[] ReadResource(ErfResourceEntry entry)
        {
            if (entry == null || !File.Exists(entry.ErfFilePath)) return null;
            try
            {
                using var fs = new FileStream(entry.ErfFilePath, FileMode.Open, FileAccess.Read);
                fs.Seek(entry.Offset, SeekOrigin.Begin);
                byte[] data = new byte[entry.FileSize];
                fs.Read(data, 0, (int)entry.FileSize);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[ErfReader] Error reading resource {entry.ResRef}: {e.Message}");
                return null;
            }
        }

        /// <summary>Find a resource by name in an ERF archive (case-insensitive).</summary>
        public static ErfResourceEntry FindResource(
            List<ErfResourceEntry> entries, string resRef, ushort? resType = null)
        {
            foreach (var entry in entries)
            {
                if (string.Equals(entry.ResRef, resRef, StringComparison.OrdinalIgnoreCase))
                {
                    if (resType == null || entry.ResType == resType.Value)
                        return entry;
                }
            }
            return null;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ERF WRITER  —  creates KotOR-compatible ERF/MOD/SAV binary archives
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Writes a BioWare ERF binary archive from a list of in-memory resources.
    ///
    /// ERF Binary Layout (little-endian):
    ///   Header   (160 bytes)
    ///     char[4]  FileType        e.g. "MOD " or "ERF "
    ///     char[4]  Version         "V1.0"
    ///     uint32   LanguageCount   (0 for mod archives)
    ///     uint32   LocalisedStringSize
    ///     uint32   EntryCount
    ///     uint32   OffsetToLocalizedStrings
    ///     uint32   OffsetToKeyList
    ///     uint32   OffsetToResourceList
    ///     uint32   BuildYear       (years since 1900)
    ///     uint32   BuildDay        (0-based day of year)
    ///     bytes[116] DescriptionStrRef + reserved
    ///   Key Table  (entry × 24 bytes)
    ///     char[16] ResRef
    ///     uint32   ResourceID
    ///     uint16   ResourceType
    ///     uint16   Unused
    ///   Resource Table  (entry × 8 bytes)
    ///     uint32   OffsetToResource
    ///     uint32   ResourceSize
    ///   Resource Data blocks (concatenated)
    /// </summary>
    public static class ErfWriter
    {
        // Common ERF resource type codes (matching those in ErfReader / ResourceType enum)
        public static readonly Dictionary<string, ushort> ExtToType =
            new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase)
        {
            { ".res",   0x0000 }, { ".bmp",   0x0001 }, { ".tga",   0x000F },
            { ".wav",   0x0011 }, { ".ini",   0x0018 }, { ".txt",   0x000A },
            { ".mdl",   0x07D0 }, { ".nss",   0x07D2 }, { ".ncs",   0x07D3 },
            { ".mod",   0x07D4 }, { ".are",   0x07D5 }, { ".set",   0x07D6 },
            { ".ifo",   0x07D7 }, { ".bic",   0x07D8 }, { ".wok",   0x07D9 },
            { ".2da",   0x07DA }, { ".tlk",   0x07DB }, { ".txi",   0x07DC },
            { ".git",   0x07DD }, { ".bti",   0x07DF }, { ".uti",   0x07E6 },
            { ".btc",   0x07E7 }, { ".utc",   0x07E8 }, { ".dlg",   0x07ED },
            { ".itp",   0x07EE }, { ".btt",   0x07EF }, { ".utt",   0x07F0 },
            { ".dds",   0x07F1 }, { ".uts",   0x07F3 }, { ".ltr",   0x07F4 },
            { ".gff",   0x07F5 }, { ".fac",   0x07F6 }, { ".bte",   0x07F7 },
            { ".ute",   0x07F8 }, { ".btd",   0x07F9 }, { ".utd",   0x07FA },
            { ".btp",   0x07FB }, { ".utp",   0x07FC }, { ".dft",   0x07FD },
            { ".gic",   0x07FE }, { ".gui",   0x07FF }, { ".css",   0x0800 },
            { ".ccs",   0x0801 }, { ".btm",   0x0802 }, { ".utm",   0x0803 },
            { ".dwk",   0x0804 }, { ".pwk",   0x0805 }, { ".btg",   0x0806 },
            { ".utg",   0x0807 }, { ".jrl",   0x0808 }, { ".sav",   0x0809 },
            { ".utw",   0x080A }, { ".4pc",   0x080B }, { ".ssf",   0x080C },
            { ".hak",   0x080D }, { ".nwm",   0x080E }, { ".bik",   0x080F },
            { ".lip",   0x0816 }, { ".mdx",   0x07D1 },
        };

        /// <summary>A single resource to pack into the archive.</summary>
        public class ErfResource
        {
            public string ResRef;   // max 16 chars, no extension
            public ushort ResType;  // from ExtToType
            public byte[] Data;

            public ErfResource(string resRef, ushort resType, byte[] data)
            {
                ResRef  = resRef?.Length > 16 ? resRef.Substring(0, 16) : resRef ?? "";
                ResType = resType;
                Data    = data ?? Array.Empty<byte>();
            }

            public ErfResource(string resRef, string extension, byte[] data)
                : this(resRef,
                       ExtToType.TryGetValue(extension, out ushort t) ? t : (ushort)0x0000,
                       data) { }
        }

        /// <summary>
        /// Write an ERF/MOD archive to a byte array.
        /// fileType should be "MOD " (4 chars) for module files, "ERF " for generic.
        /// </summary>
        public static byte[] Write(IList<ErfResource> resources,
                                   string fileType = "MOD ")
        {
            if (resources == null) resources = new List<ErfResource>();

            // Pad/truncate fileType to exactly 4 bytes
            string ft  = (fileType + "    ").Substring(0, 4);

            int entryCount = resources.Count;

            // ── Compute offsets ───────────────────────────────────────────────
            const int HEADER_SIZE   = 160;
            const int KEY_ENTRY_SZ  = 24;
            const int RES_ENTRY_SZ  = 8;

            int offsetToLocalStrings = HEADER_SIZE;
            int offsetToKeyList      = offsetToLocalStrings; // no language strings
            int offsetToResList      = offsetToKeyList + entryCount * KEY_ENTRY_SZ;
            int offsetToData         = offsetToResList + entryCount * RES_ENTRY_SZ;

            // Compute individual data offsets
            var dataOffsets = new int[entryCount];
            int cursor = offsetToData;
            for (int i = 0; i < entryCount; i++)
            {
                dataOffsets[i] = cursor;
                cursor += resources[i].Data.Length;
            }

            // ── Build binary ──────────────────────────────────────────────────
            using var ms = new MemoryStream(cursor + 64);
            using var bw = new BinaryWriter(ms, Encoding.ASCII);

            // Header (160 bytes)
            WriteFixedString(bw, ft,     4);       // FileType
            WriteFixedString(bw, "V1.0", 4);       // Version
            bw.Write((uint)0);                     // LanguageCount
            bw.Write((uint)0);                     // LocalisedStringSize
            bw.Write((uint)entryCount);             // EntryCount
            bw.Write((uint)offsetToLocalStrings);   // OffsetToLocalizedStrings
            bw.Write((uint)offsetToKeyList);        // OffsetToKeyList
            bw.Write((uint)offsetToResList);        // OffsetToResourceList
            bw.Write((uint)(DateTime.UtcNow.Year - 1900)); // BuildYear
            bw.Write((uint)DateTime.UtcNow.DayOfYear);     // BuildDay
            bw.Write((uint)0xFFFFFFFF);             // DescriptionStrRef (-1 = none)
            bw.Write(new byte[116]);                // Reserved

            // Key table (entryCount × 24 bytes)
            for (int i = 0; i < entryCount; i++)
            {
                var r = resources[i];
                WriteFixedString(bw, r.ResRef, 16); // ResRef (16 bytes)
                bw.Write((uint)i);                  // ResourceID
                bw.Write(r.ResType);                // ResourceType
                bw.Write((ushort)0);                // Unused
            }

            // Resource list (entryCount × 8 bytes)
            for (int i = 0; i < entryCount; i++)
            {
                bw.Write((uint)dataOffsets[i]);            // OffsetToResource
                bw.Write((uint)resources[i].Data.Length);  // ResourceSize
            }

            // Resource data blocks
            foreach (var r in resources)
                bw.Write(r.Data);

            return ms.ToArray();
        }

        /// <summary>
        /// Build an ERF/MOD archive from a folder of files.
        /// All files in the folder (non-recursive) are included.
        /// </summary>
        public static byte[] WriteFromFolder(string folderPath, string fileType = "MOD ")
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"[ErfWriter] Folder not found: {folderPath}");

            var resources = new List<ErfResource>();
            foreach (string file in Directory.GetFiles(folderPath))
            {
                string ext    = Path.GetExtension(file).ToLowerInvariant();
                string resRef = Path.GetFileNameWithoutExtension(file);
                if (resRef.Length > 16) resRef = resRef.Substring(0, 16);
                byte[] data   = File.ReadAllBytes(file);
                resources.Add(new ErfResource(resRef, ext, data));
            }

            Debug.Log($"[ErfWriter] Packing {resources.Count} files from '{folderPath}'.");
            return Write(resources, fileType);
        }

        private static void WriteFixedString(BinaryWriter bw, string s, int length)
        {
            var bytes = new byte[length];
            if (!string.IsNullOrEmpty(s))
            {
                var src = Encoding.ASCII.GetBytes(s);
                Array.Copy(src, bytes, Math.Min(src.Length, length));
            }
            bw.Write(bytes);
        }
    }
}
