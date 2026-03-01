using NUnit.Framework;
using KotORUnity.KotOR.Parsers;
using System.Text;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Unit tests for GffReader — validates parsing of GFF binary format.
    /// Uses minimal synthetic GFF data for format verification.
    /// </summary>
    public class GffReaderTests
    {
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
        public void GetFloat_MissingKey_ReturnsDefault()
        {
            var gffStruct = new GffReader.GffStruct();
            float result = GffReader.GetFloat(gffStruct, "NonExistent", 3.14f);
            Assert.AreEqual(3.14f, result, 0.001f);
        }
    }
}
