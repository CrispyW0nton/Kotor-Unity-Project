using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.KotOR.FileReaders
{
    /// <summary>
    /// Reads BioWare RIM (Resource Image) archive files.
    /// RIM files are simpler than ERF — each module has paired .rim files:
    ///   {module}.rim     → base module resources
    ///   {module}_s.rim   → script resources
    /// 
    /// RIM Format:
    ///   Header: "RIMV1.0 " + reserved (4 bytes) + entry count + offset to resource list
    ///   Resource List: resref (16 bytes) + restype (4 bytes) + id (4 bytes) + offset (4 bytes) + size (4 bytes)
    /// </summary>
    public class RimReader
    {
        private const string RIM_SIGNATURE = "RIM ";
        private const string RIM_VERSION = "V1.0";

        public class RimResourceEntry
        {
            public string ResRef;
            public uint ResType;
            public uint ResID;
            public uint Offset;
            public uint FileSize;
            public string RimFilePath;
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>Read all resource entries from a RIM file.</summary>
        public static List<RimResourceEntry> ReadRimArchive(string rimFilePath)
        {
            var entries = new List<RimResourceEntry>();

            if (!File.Exists(rimFilePath))
            {
                Debug.LogError($"[RimReader] RIM file not found: {rimFilePath}");
                return entries;
            }

            try
            {
                using var fs = new FileStream(rimFilePath, FileMode.Open, FileAccess.Read);
                using var br = new BinaryReader(fs, Encoding.ASCII);

                string sig = new string(br.ReadChars(4));
                string ver = new string(br.ReadChars(4));

                if (sig != RIM_SIGNATURE)
                {
                    Debug.LogError($"[RimReader] Invalid RIM signature: '{sig}'");
                    return entries;
                }

                br.ReadUInt32(); // Reserved
                uint entryCount = br.ReadUInt32();
                uint resourceListOffset = br.ReadUInt32();

                fs.Seek(resourceListOffset, SeekOrigin.Begin);

                for (int i = 0; i < entryCount; i++)
                {
                    string resref = new string(br.ReadChars(16)).TrimEnd('\0');
                    uint resType = br.ReadUInt32();
                    uint resId = br.ReadUInt32();
                    uint offset = br.ReadUInt32();
                    uint fileSize = br.ReadUInt32();

                    entries.Add(new RimResourceEntry
                    {
                        ResRef = resref,
                        ResType = resType,
                        ResID = resId,
                        Offset = offset,
                        FileSize = fileSize,
                        RimFilePath = rimFilePath
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[RimReader] Error reading {rimFilePath}: {e.Message}");
            }

            return entries;
        }

        /// <summary>Read the raw bytes of a resource from a RIM archive.</summary>
        public static byte[] ReadResource(RimResourceEntry entry)
        {
            if (entry == null || !File.Exists(entry.RimFilePath)) return null;
            try
            {
                using var fs = new FileStream(entry.RimFilePath, FileMode.Open, FileAccess.Read);
                fs.Seek(entry.Offset, SeekOrigin.Begin);
                byte[] data = new byte[entry.FileSize];
                fs.Read(data, 0, (int)entry.FileSize);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[RimReader] Error reading resource {entry.ResRef}: {e.Message}");
                return null;
            }
        }

        /// <summary>Find a resource by name in a RIM entry list.</summary>
        public static RimResourceEntry FindResource(
            List<RimResourceEntry> entries, string resRef, uint? resType = null)
        {
            foreach (var entry in entries)
            {
                bool nameMatch = string.Equals(entry.ResRef, resRef, StringComparison.OrdinalIgnoreCase);
                bool typeMatch = resType == null || entry.ResType == resType.Value;
                if (nameMatch && typeMatch) return entry;
            }
            return null;
        }
    }
}
