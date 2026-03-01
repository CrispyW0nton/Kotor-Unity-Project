using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.KotOR.FileReaders
{
    /// <summary>
    /// KotOR Resource Type constants.
    /// Used when searching for specific resource types in ERF/RIM/BIF archives.
    /// </summary>
    public static class ResourceType
    {
        public const ushort BMP  = 1;
        public const ushort TGA  = 3;
        public const ushort WAV  = 4;
        public const ushort INI  = 7;
        public const ushort TXT  = 10;
        public const ushort MDL  = 2002;
        public const ushort NSS  = 2009;  // NWScript source
        public const ushort NCS  = 2010;  // NWScript compiled
        public const ushort ARE  = 2012;  // Area GFF
        public const ushort SET  = 2013;
        public const ushort IFO  = 2014;  // Module info GFF
        public const ushort BIC  = 2015;  // Character GFF
        public const ushort WOK  = 2016;  // Walk-mesh
        public const ushort MDX  = 3006;  // Model data (paired with MDL)
        public const ushort TPC  = 3007;  // Texture
        public const ushort TXB  = 3008;  // Texture
        public const ushort SSF  = 3011;  // Sound set
        public const ushort NDB  = 2064;  // NWScript debug
        public const ushort PTM  = 3010;
        public const ushort PTT  = 3011;
        public const ushort LYT  = 3000;  // Module layout
        public const ushort VIS  = 3001;  // Visibility data
        public const ushort RIM  = 3002;  // (not used as a resource type)
        public const ushort PTH  = 3003;  // Path data
        public const ushort LIP  = 3005;  // Lip sync
        public const ushort UTC  = 2023;  // Creature template GFF
        public const ushort UTD  = 2026;  // Door template GFF
        public const ushort UTE  = 2022;  // Encounter template GFF
        public const ushort UTI  = 2025;  // Item template GFF
        public const ushort UTP  = 2032;  // Placeable template GFF
        public const ushort UTM  = 2020;  // Merchant template GFF
        public const ushort UTW  = 2034;  // Waypoint template GFF
        public const ushort UTS  = 2035;  // Sound template GFF
        public const ushort UTT  = 2033;  // Trigger template GFF
        public const ushort DLG  = 2029;  // Dialogue GFF
        public const ushort GIT  = 2023;  // Dynamic area instances GFF (same as UTC — context-dependent)
        public const ushort GUI  = 3033;  // GUI file
        public const ushort MP3  = 1002; // MP3 audio (for ambient music)
    }

    /// <summary>
    /// KotOR resource reference — unified type pointing to a resource
    /// regardless of which archive (BIF, ERF, RIM) it lives in.
    /// </summary>
    public class KotORResourceRef
    {
        public string ResRef;
        public ushort ResType;
        public string SourceArchive;  // File path of the archive containing this resource
        public ArchiveType ArchiveType;
        public long Offset;
        public long Size;

        // Cached data
        private byte[] _cachedData;
        public byte[] Data
        {
            get
            {
                if (_cachedData != null) return _cachedData;
                _cachedData = ReadFromArchive();
                return _cachedData;
            }
        }

        private byte[] ReadFromArchive()
        {
            if (!File.Exists(SourceArchive)) return null;
            try
            {
                using var fs = new FileStream(SourceArchive, FileMode.Open, FileAccess.Read);
                fs.Seek(Offset, SeekOrigin.Begin);
                byte[] data = new byte[Size];
                fs.Read(data, 0, (int)Size);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[KotORResourceRef] Read error for {ResRef}: {e.Message}");
                return null;
            }
        }
    }

    public enum ArchiveType { BIF, ERF, RIM, MOD, Override }
}
