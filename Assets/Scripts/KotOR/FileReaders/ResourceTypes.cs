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
    ///
    /// Values match the KotOR/NWN engine type IDs exactly.
    /// References: nwmax docs, xoreos source, kotor-save-editor.
    ///
    /// Declared as a ushort-backed enum so it can be used as a method
    /// parameter and return type (C# forbids static classes in those roles).
    /// All callers use ResourceType.TwoDA, ResourceType.UTC etc. unchanged.
    /// Cast to ushort with  (ushort)myResourceType  wherever needed.
    /// </summary>
    public enum ResourceType : ushort
    {
        Invalid = 0,
        BMP    = 1,
        TGA    = 3,     // Targa uncompressed texture
        WAV    = 4,
        INI    = 7,
        TXT    = 10,
        MDL    = 2002,  // Model geometry header
        NSS    = 2009,  // NWScript source
        NCS    = 2010,  // NWScript compiled bytecode
        ARE    = 2012,  // Area GFF
        SET    = 2013,
        IFO    = 2014,  // Module info GFF
        BIC    = 2015,  // Character GFF
        WOK    = 2016,  // Walk-mesh
        TXB    = 2021,  // Texture (alternate BioWare format)
        UTC    = 2023,  // Creature template GFF
        UTE    = 2022,  // Encounter template GFF
        UTD    = 2026,  // Door template GFF
        UTP    = 2027,  // Placeable template GFF
        DLG    = 2029,  // Dialogue GFF
        UTI    = 2025,  // Item template GFF
        UTM    = 2020,  // Merchant template GFF
        UTT    = 2033,  // Trigger template GFF
        UTW    = 2034,  // Waypoint template GFF
        UTS    = 2035,  // Sound template GFF
        DDS    = 2032,  // DDS compressed texture (some KotOR builds)
        LYT    = 3000,  // Module layout
        VIS    = 3001,  // Visibility data
        PTH    = 3003,  // Path data
        LIP    = 3005,  // Lip-sync data
        MDX    = 3006,  // Model geometry data (paired with MDL)
        TPC    = 3007,  // BioWare TPC texture wrapper
        SSF    = 3011,  // Sound set GFF
        GIT    = 3012,  // Dynamic area instances GFF
        PTM    = 3010,
        PTT    = 3013,
        NDB    = 2064,  // NWScript debug
        GUI    = 3033,  // GUI definition file
        MP3    = 1002,  // MP3 audio
        FAC    = 3028,  // Faction GFF
        TLK    = 2018,  // Talk table
        RIM    = 3002,  // (archive type marker, not normally a resource)
        TwoDA  = 2017,  // 2DA table
        GFF    = 2038,  // Generic GFF
    }

    /// <summary>
    /// KotOR resource reference — unified pointer to a resource
    /// regardless of which archive (BIF, ERF, RIM, Override) it lives in.
    /// Supports lazy-loading: data is only read from disk when first accessed.
    /// </summary>
    public class KotORResourceRef
    {
        public string      ResRef;
        public ushort      ResType;
        public string      SourceArchive;   // File path of the archive
        public ArchiveType ArchiveType;
        public long        Offset;
        public long        Size;

        // Cached data — null until first access
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
                using var fs = new FileStream(SourceArchive, FileMode.Open, FileAccess.Read,
                                              FileShare.Read);
                fs.Seek(Offset, SeekOrigin.Begin);
                byte[] data = new byte[Size];
                int read = 0;
                while (read < (int)Size)
                {
                    int chunk = fs.Read(data, read, (int)Size - read);
                    if (chunk == 0) break;
                    read += chunk;
                }
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[KotORResourceRef] Read error for '{ResRef}': {e.Message}");
                return null;
            }
        }
    }

    public enum ArchiveType { BIF, ERF, RIM, MOD, Override }
}
