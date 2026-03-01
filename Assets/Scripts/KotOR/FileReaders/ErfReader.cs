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
}
