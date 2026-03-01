using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KotORUnity.KotOR.Parsers
{
    /// <summary>
    /// Parses BioWare GFF (Generic File Format) files into memory.
    /// 
    /// GFF is a hierarchical data format used for almost all KotOR data:
    ///   .utc → creature templates
    ///   .uti → item templates
    ///   .are → area properties
    ///   .ifo → module info
    ///   .dlg → dialogue
    ///   .git → area instances
    ///   ... and many more
    /// 
    /// Structure:
    ///   Header → Structs[] → Fields[] → Labels[] → FieldData → FieldIndices → ListIndices
    ///   All data is accessed through the root struct.
    /// </summary>
    public class GffReader
    {
        // ── GFF FIELD TYPES ────────────────────────────────────────────────────
        public enum GffFieldType : uint
        {
            Byte     = 0,
            Char     = 1,
            Word     = 2,
            Short    = 3,
            DWord    = 4,
            Int      = 5,
            DWord64  = 6,
            Int64    = 7,
            Float    = 8,
            Double   = 9,
            CExoString    = 10,
            ResRef        = 11,
            CExoLocString = 12,
            Void          = 13,
            Struct        = 14,
            List          = 15,
            Orientation   = 16,
            Vector        = 17,
            StrRef        = 18
        }

        // ── GFF HEADER ─────────────────────────────────────────────────────────
        public class GffHeader
        {
            public string FileType;         // e.g., "UTC ", "ARE "
            public string FileVersion;      // "V3.2"
            public uint StructOffset;
            public uint StructCount;
            public uint FieldOffset;
            public uint FieldCount;
            public uint LabelOffset;
            public uint LabelCount;
            public uint FieldDataOffset;
            public uint FieldDataCount;
            public uint FieldIndicesOffset;
            public uint FieldIndicesCount;
            public uint ListIndicesOffset;
            public uint ListIndicesCount;
        }

        // ── GFF STRUCT ─────────────────────────────────────────────────────────
        public class GffStruct
        {
            public uint Type;
            public uint DataOrDataOffset;
            public uint FieldCount;
            public Dictionary<string, GffField> Fields = new Dictionary<string, GffField>();
        }

        // ── GFF FIELD ──────────────────────────────────────────────────────────
        public class GffField
        {
            public GffFieldType Type;
            public string Label;
            public object Value;  // boxed value

            // Typed accessors
            public byte AsByte()   => Value is byte   b ? b   : default;
            public int AsInt()     => Value is int    i ? i   : Convert.ToInt32(Value);
            public uint AsDWord()  => Value is uint   u ? u   : Convert.ToUInt32(Value);
            public float AsFloat() => Value is float  f ? f   : Convert.ToSingle(Value);
            public string AsString() => Value?.ToString() ?? "";
            public GffStruct AsStruct() => Value as GffStruct;
            public List<GffStruct> AsList() => Value as List<GffStruct>;
            public Vector3 AsVector()
            {
                if (Value is float[] v && v.Length >= 3)
                    return new Vector3(v[0], v[1], v[2]);
                return Vector3.zero;
            }
            public Quaternion AsOrientation()
            {
                if (Value is float[] q && q.Length >= 4)
                    return new Quaternion(q[0], q[1], q[2], q[3]);
                return Quaternion.identity;
            }
        }

        // ── PARSE ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Parse a GFF file from raw bytes.
        /// Returns the root GffStruct (all data accessible from here).
        /// </summary>
        public static GffStruct Parse(byte[] data)
        {
            if (data == null || data.Length < 56)
            {
                Debug.LogError("[GffReader] Data too small to be a valid GFF.");
                return null;
            }

            try
            {
                using var ms = new MemoryStream(data);
                using var br = new BinaryReader(ms, Encoding.ASCII);

                // Header
                var header = new GffHeader
                {
                    FileType          = new string(br.ReadChars(4)),
                    FileVersion       = new string(br.ReadChars(4)),
                    StructOffset      = br.ReadUInt32(),
                    StructCount       = br.ReadUInt32(),
                    FieldOffset       = br.ReadUInt32(),
                    FieldCount        = br.ReadUInt32(),
                    LabelOffset       = br.ReadUInt32(),
                    LabelCount        = br.ReadUInt32(),
                    FieldDataOffset   = br.ReadUInt32(),
                    FieldDataCount    = br.ReadUInt32(),
                    FieldIndicesOffset = br.ReadUInt32(),
                    FieldIndicesCount  = br.ReadUInt32(),
                    ListIndicesOffset  = br.ReadUInt32(),
                    ListIndicesCount   = br.ReadUInt32()
                };

                // Read labels
                ms.Seek(header.LabelOffset, SeekOrigin.Begin);
                string[] labels = new string[header.LabelCount];
                for (int i = 0; i < header.LabelCount; i++)
                    labels[i] = new string(br.ReadChars(16)).TrimEnd('\0');

                // Read field data block
                ms.Seek(header.FieldDataOffset, SeekOrigin.Begin);
                byte[] fieldData = br.ReadBytes((int)header.FieldDataCount);

                // Read field indices
                ms.Seek(header.FieldIndicesOffset, SeekOrigin.Begin);
                uint[] fieldIndices = new uint[header.FieldIndicesCount / 4];
                for (int i = 0; i < fieldIndices.Length; i++)
                    fieldIndices[i] = br.ReadUInt32();

                // Read list indices
                ms.Seek(header.ListIndicesOffset, SeekOrigin.Begin);
                uint[] listIndices = new uint[header.ListIndicesCount / 4];
                for (int i = 0; i < listIndices.Length; i++)
                    listIndices[i] = br.ReadUInt32();

                // Read fields
                ms.Seek(header.FieldOffset, SeekOrigin.Begin);
                var fields = new GffField[header.FieldCount];
                for (int i = 0; i < header.FieldCount; i++)
                {
                    uint typeId = br.ReadUInt32();
                    uint labelIndex = br.ReadUInt32();
                    uint dataOrOffset = br.ReadUInt32();

                    fields[i] = new GffField
                    {
                        Type  = (GffFieldType)typeId,
                        Label = labelIndex < labels.Length ? labels[labelIndex] : $"Label_{labelIndex}"
                    };

                    // Resolve inline values
                    ParseFieldValue(fields[i], dataOrOffset, fieldData, data, header);
                }

                // Read structs
                ms.Seek(header.StructOffset, SeekOrigin.Begin);
                var structs = new GffStruct[header.StructCount];
                for (int i = 0; i < header.StructCount; i++)
                {
                    uint type = br.ReadUInt32();
                    uint dataOrOffset2 = br.ReadUInt32();
                    uint fieldCount = br.ReadUInt32();

                    structs[i] = new GffStruct
                    {
                        Type = type,
                        DataOrDataOffset = dataOrOffset2,
                        FieldCount = fieldCount
                    };
                }

                // Link fields to structs
                for (int i = 0; i < structs.Length; i++)
                {
                    var s = structs[i];
                    if (s.FieldCount == 1)
                    {
                        var field = fields[s.DataOrDataOffset];
                        s.Fields[field.Label] = field;
                    }
                    else if (s.FieldCount > 1)
                    {
                        uint startIdx = s.DataOrDataOffset / 4;
                        for (int j = 0; j < s.FieldCount && startIdx + j < fieldIndices.Length; j++)
                        {
                            uint fieldIdx = fieldIndices[startIdx + j];
                            if (fieldIdx < fields.Length)
                            {
                                var field = fields[fieldIdx];
                                s.Fields[field.Label] = field;
                            }
                        }
                    }
                }

                // Link struct/list fields to their actual struct objects
                LinkStructFields(fields, structs, listIndices);

                return structs.Length > 0 ? structs[0] : null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[GffReader] Parse error: {e.Message}\n{e.StackTrace}");
                return null;
            }
        }

        /// <summary>Parse a GFF field's value from the field data block.</summary>
        private static void ParseFieldValue(GffField field, uint dataOrOffset,
            byte[] fieldData, byte[] fullData, GffHeader header)
        {
            using var fd = new BinaryReader(new MemoryStream(fieldData), Encoding.ASCII);

            switch (field.Type)
            {
                case GffFieldType.Byte:
                case GffFieldType.Char:
                    field.Value = (byte)(dataOrOffset & 0xFF);
                    break;
                case GffFieldType.Word:
                    field.Value = (ushort)(dataOrOffset & 0xFFFF);
                    break;
                case GffFieldType.Short:
                    field.Value = (short)(dataOrOffset & 0xFFFF);
                    break;
                case GffFieldType.DWord:
                    field.Value = dataOrOffset;
                    break;
                case GffFieldType.Int:
                    field.Value = (int)dataOrOffset;
                    break;
                case GffFieldType.Float:
                    field.Value = BitConverter.ToSingle(BitConverter.GetBytes(dataOrOffset), 0);
                    break;
                case GffFieldType.DWord64:
                    if (dataOrOffset + 8 <= fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        field.Value = fd.ReadUInt64();
                    }
                    break;
                case GffFieldType.Int64:
                    if (dataOrOffset + 8 <= fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        field.Value = fd.ReadInt64();
                    }
                    break;
                case GffFieldType.Double:
                    if (dataOrOffset + 8 <= fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        field.Value = fd.ReadDouble();
                    }
                    break;
                case GffFieldType.CExoString:
                    if (dataOrOffset < fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        uint len = fd.ReadUInt32();
                        field.Value = len > 0 ? new string(fd.ReadChars((int)len)) : "";
                    }
                    break;
                case GffFieldType.ResRef:
                    if (dataOrOffset < fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        byte len = fd.ReadByte();
                        field.Value = len > 0 ? new string(fd.ReadChars(len)) : "";
                    }
                    break;
                case GffFieldType.Vector:
                    if (dataOrOffset + 12 <= fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        field.Value = new float[] {
                            fd.ReadSingle(), fd.ReadSingle(), fd.ReadSingle()
                        };
                    }
                    break;
                case GffFieldType.Orientation:
                    if (dataOrOffset + 16 <= fieldData.Length)
                    {
                        fd.BaseStream.Seek(dataOrOffset, SeekOrigin.Begin);
                        field.Value = new float[] {
                            fd.ReadSingle(), fd.ReadSingle(), fd.ReadSingle(), fd.ReadSingle()
                        };
                    }
                    break;
                case GffFieldType.Struct:
                    field.Value = (int)dataOrOffset; // index resolved later
                    break;
                case GffFieldType.List:
                    field.Value = (int)dataOrOffset; // offset into list indices, resolved later
                    break;
                default:
                    field.Value = dataOrOffset;
                    break;
            }
        }

        private static void LinkStructFields(GffField[] fields, GffStruct[] structs, uint[] listIndices)
        {
            foreach (var field in fields)
            {
                if (field.Type == GffFieldType.Struct && field.Value is int structIdx)
                {
                    if (structIdx >= 0 && structIdx < structs.Length)
                        field.Value = structs[structIdx];
                }
                else if (field.Type == GffFieldType.List && field.Value is int listOffset)
                {
                    var list = new List<GffStruct>();
                    int byteOffset = listOffset;
                    if (byteOffset < listIndices.Length * 4)
                    {
                        int idx = byteOffset / 4;
                        if (idx < listIndices.Length)
                        {
                            uint count = listIndices[idx];
                            for (int j = 1; j <= count && idx + j < listIndices.Length; j++)
                            {
                                uint structIndex = listIndices[idx + j];
                                if (structIndex < structs.Length)
                                    list.Add(structs[structIndex]);
                            }
                        }
                    }
                    field.Value = list;
                }
            }
        }

        // ── HELPER ACCESSORS ───────────────────────────────────────────────────
        public static string GetString(GffStruct s, string key, string defaultVal = "")
        {
            if (s == null || !s.Fields.ContainsKey(key)) return defaultVal;
            return s.Fields[key].AsString();
        }

        public static int GetInt(GffStruct s, string key, int defaultVal = 0)
        {
            if (s == null || !s.Fields.ContainsKey(key)) return defaultVal;
            return s.Fields[key].AsInt();
        }

        public static float GetFloat(GffStruct s, string key, float defaultVal = 0f)
        {
            if (s == null || !s.Fields.ContainsKey(key)) return defaultVal;
            return s.Fields[key].AsFloat();
        }

        public static Vector3 GetVector(GffStruct s, string key)
        {
            if (s == null || !s.Fields.ContainsKey(key)) return Vector3.zero;
            return s.Fields[key].AsVector();
        }
    }
}
