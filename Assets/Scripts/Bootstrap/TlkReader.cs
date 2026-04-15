using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.Bootstrap
{
    /// <summary>
    /// Parses BioWare TLK (Talk Table) files.
    ///
    /// The TLK file (dialog.tlk) contains all localised strings used in KotOR.
    /// Every NPC name, dialogue line, item description, etc. is indexed by a
    /// StrRef (uint) into this table.
    ///
    /// Binary layout (little-endian):
    ///   Header (20 bytes):
    ///     char[4]  FileType    "TLK "
    ///     char[4]  FileVersion "V3.0"
    ///     uint32   LanguageID
    ///     uint32   StringCount
    ///     uint32   StringEntriesOffset
    ///   Per-entry data (40 bytes each, from offset 20):
    ///     uint32   Flags
    ///     char[16] SoundResRef
    ///     uint32   VolumeVariance (unused)
    ///     uint32   PitchVariance  (unused)
    ///     uint32   StringOffset   (from StringEntriesOffset)
    ///     uint32   StringSize
    ///     float    SoundLength
    /// </summary>
    public class TlkReader
    {
        // ── DATA ──────────────────────────────────────────────────────────────
        private string[]  _strings;
        private string[]  _soundRefs;
        private float[]   _soundLengths;

        public int  StringCount => _strings?.Length ?? 0;
        public bool IsLoaded    => _strings != null;

        // Flags
        private const uint FLAG_TEXT_PRESENT  = 0x01;
        private const uint FLAG_SOUND_PRESENT = 0x02;
        private const uint FLAG_SOUND_LENGTH  = 0x04;

        // ── LOAD ──────────────────────────────────────────────────────────────
        public void Load(byte[] data)
        {
            if (data == null || data.Length < 20)
            {
                Debug.LogError("[TlkReader] Data too short.");
                return;
            }

            using var ms = new MemoryStream(data);
            using var br = new BinaryReader(ms, Encoding.ASCII); // header is ASCII

            // Header
            string fileType    = new string(br.ReadChars(4));
            string fileVersion = new string(br.ReadChars(4));

            if (fileType.TrimEnd('\0') != "TLK")
            {
                Debug.LogError($"[TlkReader] Not a TLK file (type='{fileType}').");
                return;
            }

            uint languageId          = br.ReadUInt32();
            uint stringCount         = br.ReadUInt32();
            uint stringEntriesOffset = br.ReadUInt32();

            _strings      = new string[stringCount];
            _soundRefs    = new string[stringCount];
            _soundLengths = new float[stringCount];

            // Read entry descriptors (40 bytes each, starting at byte 20)
            var entries = new (uint flags, string sound, uint offset, uint size, float length)[stringCount];
            for (uint i = 0; i < stringCount; i++)
            {
                uint  flags          = br.ReadUInt32();
                string soundResRef   = new string(br.ReadChars(16)).TrimEnd('\0');
                uint  volumeVariance = br.ReadUInt32();
                uint  pitchVariance  = br.ReadUInt32();
                uint  stringOffset   = br.ReadUInt32();
                uint  stringSize     = br.ReadUInt32();
                float soundLength    = br.ReadSingle();

                entries[i] = (flags, soundResRef, stringOffset, stringSize, soundLength);
            }

            // KotOR string data is Windows-1252 encoded, not UTF-8.
            System.Text.Encoding strEnc;
            try   { strEnc = System.Text.Encoding.GetEncoding(1252); }
            catch { strEnc = System.Text.Encoding.GetEncoding("iso-8859-1"); } // Mono/Unity fallback

            // Read actual string data
            for (uint i = 0; i < stringCount; i++)
            {
                var (flags, sound, offset, size, length) = entries[i];
                _soundRefs[i]    = sound;
                _soundLengths[i] = length;

                if ((flags & FLAG_TEXT_PRESENT) != 0 && size > 0)
                {
                    long pos = stringEntriesOffset + offset;
                    if (pos + size <= data.Length)
                    {
                        _strings[i] = strEnc.GetString(data, (int)pos, (int)size);
                    }
                    else
                    {
                        _strings[i] = "";
                    }
                }
                else
                {
                    _strings[i] = "";
                }
            }

            Debug.Log($"[TlkReader] Loaded {stringCount} strings, language {languageId}.");
        }

        // ── ACCESSORS ─────────────────────────────────────────────────────────
        public string GetString(uint strref)
        {
            if (_strings == null || strref >= _strings.Length)
                return $"<StrRef:{strref}>";
            return _strings[strref];
        }

        public string GetSoundRef(uint strref)
        {
            if (_soundRefs == null || strref >= _soundRefs.Length)
                return "";
            return _soundRefs[strref];
        }

        public float GetSoundLength(uint strref)
        {
            if (_soundLengths == null || strref >= _soundLengths.Length)
                return 0f;
            return _soundLengths[strref];
        }

        /// <summary>Returns true if the StrRef has an associated sound file.</summary>
        public bool HasSound(uint strref) => !string.IsNullOrEmpty(GetSoundRef(strref));
    }
}
