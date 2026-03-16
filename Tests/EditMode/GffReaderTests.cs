using NUnit.Framework;
using KotORUnity.KotOR.Parsers;
using System.Collections.Generic;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Unit tests for GffReader — validates parsing helpers, field access, and
    /// error-handling paths. Uses in-memory GffStruct/GffField construction.
    /// </summary>
    public class GffReaderTests
    {
        // ── HELPERS ────────────────────────────────────────────────────────────
        private static GffReader.GffField MakeField(GffReader.GffFieldType type, string label, object value)
        {
            return new GffReader.GffField { Type = type, Label = label, Value = value };
        }

        // ── NULL / SHORT DATA GUARDS ───────────────────────────────────────────
        [Test]
        public void GffReader_NullData_ReturnsNull()
        {
            var result = GffReader.Parse(null);
            Assert.IsNull(result);
        }

        [Test]
        public void GffReader_TooShortData_ReturnsNull()
        {
            byte[] tooShort = new byte[10];
            var result = GffReader.Parse(tooShort);
            Assert.IsNull(result);
        }

        [Test]
        public void GffReader_EmptyArray_ReturnsNull()
        {
            var result = GffReader.Parse(new byte[0]);
            Assert.IsNull(result);
        }

        // ── NULL STRUCT HELPERS ────────────────────────────────────────────────
        [Test]
        public void GetString_NullStruct_ReturnsDefault()
        {
            string result = GffReader.GetString(null, "TestKey", "default");
            Assert.AreEqual("default", result);
        }

        [Test]
        public void GetInt_NullStruct_ReturnsDefault()
        {
            int result = GffReader.GetInt(null, "TestKey", 42);
            Assert.AreEqual(42, result);
        }

        [Test]
        public void GetFloat_NullStruct_ReturnsDefault()
        {
            float result = GffReader.GetFloat(null, "TestKey", 3.14f);
            Assert.AreEqual(3.14f, result, 0.001f);
        }

        // ── EMPTY STRUCT HELPERS ───────────────────────────────────────────────
        [Test]
        public void GetFloat_MissingKey_ReturnsDefault()
        {
            var gffStruct = new GffReader.GffStruct();
            float result = GffReader.GetFloat(gffStruct, "NonExistent", 3.14f);
            Assert.AreEqual(3.14f, result, 0.001f);
        }

        [Test]
        public void GetString_MissingKey_ReturnsDefault()
        {
            var gffStruct = new GffReader.GffStruct();
            string result = GffReader.GetString(gffStruct, "NonExistent", "fallback");
            Assert.AreEqual("fallback", result);
        }

        [Test]
        public void GetInt_MissingKey_ReturnsDefault()
        {
            var gffStruct = new GffReader.GffStruct();
            int result = GffReader.GetInt(gffStruct, "HP", -1);
            Assert.AreEqual(-1, result);
        }

        // ── POPULATED STRUCT HELPERS ───────────────────────────────────────────
        [Test]
        public void GetString_ExistingKey_ReturnsValue()
        {
            var gffStruct = new GffReader.GffStruct();
            gffStruct.Fields["Tag"] = MakeField(GffReader.GffFieldType.CExoString, "Tag", "test_creature");

            string result = GffReader.GetString(gffStruct, "Tag", "");
            Assert.AreEqual("test_creature", result);
        }

        [Test]
        public void GetInt_ExistingKey_ReturnsValue()
        {
            var gffStruct = new GffReader.GffStruct();
            gffStruct.Fields["HP"] = MakeField(GffReader.GffFieldType.Int, "HP", (int)150);

            int result = GffReader.GetInt(gffStruct, "HP", 0);
            Assert.AreEqual(150, result);
        }

        [Test]
        public void GetFloat_ExistingKey_ReturnsValue()
        {
            var gffStruct = new GffReader.GffStruct();
            gffStruct.Fields["XPosition"] = MakeField(GffReader.GffFieldType.Float, "XPosition", 12.5f);

            float result = GffReader.GetFloat(gffStruct, "XPosition", 0f);
            Assert.AreEqual(12.5f, result, 0.001f);
        }

        // ── NESTED STRUCT ─────────────────────────────────────────────────────
        [Test]
        public void GetStruct_ExistingKey_ReturnsNestedStruct()
        {
            var parent = new GffReader.GffStruct();
            var child  = new GffReader.GffStruct();
            child.Fields["Name"] = MakeField(GffReader.GffFieldType.CExoString, "Name", "nested_value");
            parent.Fields["LocalizedName"] = MakeField(GffReader.GffFieldType.Struct, "LocalizedName", child);

            var retrieved = GffReader.GetStruct(parent, "LocalizedName");
            Assert.IsNotNull(retrieved);
            Assert.AreEqual("nested_value", GffReader.GetString(retrieved, "Name", ""));
        }

        [Test]
        public void GetStruct_MissingKey_ReturnsNull()
        {
            var gffStruct = new GffReader.GffStruct();
            var result = GffReader.GetStruct(gffStruct, "NotHere");
            Assert.IsNull(result);
        }

        // ── GFF FIELD TYPE COVERAGE ────────────────────────────────────────────
        [Test]
        public void GffFieldType_HasAllExpectedTypes()
        {
            var values = System.Enum.GetValues(typeof(GffReader.GffFieldType));
            Assert.GreaterOrEqual(values.Length, 15);
        }

        [Test]
        public void GffFieldType_StructIs14()
        {
            Assert.AreEqual(14u, (uint)GffReader.GffFieldType.Struct);
        }

        [Test]
        public void GffFieldType_ListIs15()
        {
            Assert.AreEqual(15u, (uint)GffReader.GffFieldType.List);
        }

        // ── GFFSTRUCT TYPED ACCESSORS ─────────────────────────────────────────
        [Test]
        public void GffField_AsString_ConvertsValue()
        {
            var field = MakeField(GffReader.GffFieldType.CExoString, "Label", "hello");
            Assert.AreEqual("hello", field.AsString());
        }

        [Test]
        public void GffField_AsInt_ConvertsValue()
        {
            var field = MakeField(GffReader.GffFieldType.Int, "HP", (int)99);
            Assert.AreEqual(99, field.AsInt());
        }

        [Test]
        public void GffField_AsFloat_ConvertsValue()
        {
            var field = MakeField(GffReader.GffFieldType.Float, "Speed", 3.5f);
            Assert.AreEqual(3.5f, field.AsFloat(), 0.001f);
        }

        [Test]
        public void GffField_AsStruct_ReturnsNestedStruct()
        {
            var child  = new GffReader.GffStruct();
            var field  = MakeField(GffReader.GffFieldType.Struct, "Body", child);
            Assert.AreEqual(child, field.AsStruct());
        }

        [Test]
        public void GffField_AsList_ReturnsList()
        {
            var list  = new System.Collections.Generic.List<GffReader.GffStruct>();
            var field = MakeField(GffReader.GffFieldType.List, "Items", list);
            Assert.AreEqual(list, field.AsList());
        }
    }
}
