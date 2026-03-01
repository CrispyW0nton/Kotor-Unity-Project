using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace KotORUnity.KotOR.FileReaders
{
    /// <summary>
    /// Reads BioWare BIF (Binary Image Format) archive files.
    /// BIF files store the bulk of KotOR's game assets.
    /// Accessed via a KEY file that maps resource names to BIF locations.
    /// 
    /// BIF Format:
    ///   Header: "BIFFV1  " (8 bytes) + resource count + file count + file offset
    ///   Variable Table: file entries
    ///   Resource Table: resource entries (ID, offset, size, type)
    ///   Data: raw resource data
    /// </summary>
    public class BifReader
    {
        private const string BIF_SIGNATURE = "BIFFV1  ";
        private const int HEADER_SIZE = 20;

        // ── BIF HEADER ─────────────────────────────────────────────────────────
        public class BifHeader
        {
            public string Signature;
            public string Version;
            public uint VariableResourceCount;
            public uint FixedResourceCount;
            public uint VariableTableOffset;
        }

        // ── RESOURCE ENTRY ─────────────────────────────────────────────────────
        public class BifResourceEntry
        {
            public uint ID;
            public uint Offset;
            public uint FileSize;
            public ushort ResourceType;
            public string FilePath;  // Path to BIF file
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Parse the resource table from a BIF file.
        /// Returns a list of all resources in this BIF.
        /// </summary>
        public static List<BifResourceEntry> ReadResourceTable(string bifFilePath)
        {
            var entries = new List<BifResourceEntry>();

            if (!File.Exists(bifFilePath))
            {
                Debug.LogError($"[BifReader] BIF file not found: {bifFilePath}");
                return entries;
            }

            try
            {
                using var fs = new FileStream(bifFilePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs);

                // Read header
                string sig = new string(br.ReadChars(4));
                string ver = new string(br.ReadChars(4));
                uint variableCount = br.ReadUInt32();
                uint fixedCount = br.ReadUInt32();
                uint variableOffset = br.ReadUInt32();

                if (sig != "BIFF")
                {
                    Debug.LogError($"[BifReader] Invalid BIF signature: {sig}");
                    return entries;
                }

                // Seek to variable resource table
                fs.Seek(variableOffset, SeekOrigin.Begin);

                for (int i = 0; i < variableCount; i++)
                {
                    var entry = new BifResourceEntry
                    {
                        ID = br.ReadUInt32(),
                        Offset = br.ReadUInt32(),
                        FileSize = br.ReadUInt32(),
                        ResourceType = br.ReadUInt16(),
                        FilePath = bifFilePath
                    };
                    br.ReadUInt16(); // unused
                    entries.Add(entry);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BifReader] Error reading {bifFilePath}: {e.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Read the raw bytes of a specific resource from a BIF file.
        /// </summary>
        public static byte[] ReadResource(BifResourceEntry entry)
        {
            if (entry == null || !File.Exists(entry.FilePath)) return null;

            try
            {
                using var fs = new FileStream(entry.FilePath, FileMode.Open, FileAccess.Read);
                fs.Seek(entry.Offset, SeekOrigin.Begin);
                byte[] data = new byte[entry.FileSize];
                fs.Read(data, 0, (int)entry.FileSize);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[BifReader] Error reading resource: {e.Message}");
                return null;
            }
        }
    }
}
