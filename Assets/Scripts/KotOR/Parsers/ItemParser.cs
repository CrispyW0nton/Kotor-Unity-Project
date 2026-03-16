using System;
using KotORUnity.Inventory;

namespace KotORUnity.KotOR.Parsers
{
    /// <summary>
    /// ItemParser — thin facade over UtiReader.
    ///
    /// InventorySystem.AddItemByResRef() calls KotOR.Parsers.ItemParser.Parse()
    /// rather than UtiReader.Parse() directly, keeping the KotOR file-parsing
    /// concern in the KotOR.Parsers namespace while sharing the same logic.
    /// </summary>
    public static class ItemParser
    {
        /// <summary>
        /// Parse a raw UTI byte array into an ItemData instance.
        /// Delegates to UtiReader.Parse which contains all GFF field extraction.
        /// </summary>
        /// <param name="data">Raw bytes from a .uti GFF file.</param>
        /// <param name="resref">Resource reference name (used for logging / ItemData.ResRef).</param>
        /// <returns>Populated ItemData, or null on parse failure.</returns>
        public static ItemData Parse(byte[] data, string resref = "")
        {
            return UtiReader.Parse(data, resref);
        }
    }
}
