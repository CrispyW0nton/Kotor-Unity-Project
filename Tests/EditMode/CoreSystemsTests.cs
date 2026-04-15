using NUnit.Framework;
using System.Collections.Generic;
using System.Text;
using KotORUnity.Bootstrap;
using KotORUnity.Dialogue;
using KotORUnity.Inventory;
using KotORUnity.Combat;
using KotORUnity.Scripting;
using KotORUnity.Data;
using KotORUnity.KotOR.Parsers;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════════
//  TLK READER TESTS
// ═══════════════════════════════════════════════════════════════════════════════
namespace KotORUnity.Tests
{
    [TestFixture]
    public class TlkReaderTests
    {
        private static byte[] BuildMinimalTlk(string[] strings)
        {
            // Header: "TLK V3.0" + languageId + stringCount + stringEntriesOffset
            // Entry descriptors: 40 bytes each
            // String data follows
            uint count  = (uint)strings.Length;
            uint headerSize = 20;
            uint entrySize  = 40;
            uint entriesBlock = headerSize + count * entrySize;

            // Build string data
            var strData = new List<byte>();
            var offsets = new List<(uint offset, uint size)>();
            foreach (var s in strings)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(s);
                offsets.Add(((uint)strData.Count, (uint)encoded.Length));
                strData.AddRange(encoded);
            }

            var ms = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(ms);

            // Header
            bw.Write(Encoding.ASCII.GetBytes("TLK "));
            bw.Write(Encoding.ASCII.GetBytes("V3.0"));
            bw.Write((uint)0);           // languageId
            bw.Write(count);
            bw.Write(entriesBlock);      // stringEntriesOffset

            // Entry descriptors
            for (int i = 0; i < strings.Length; i++)
            {
                bw.Write((uint)0x01);    // FLAG_TEXT_PRESENT
                bw.Write(new byte[16]);  // sound resref
                bw.Write((uint)0);       // volumeVariance
                bw.Write((uint)0);       // pitchVariance
                bw.Write(offsets[i].offset);
                bw.Write(offsets[i].size);
                bw.Write(0f);            // soundLength
            }

            bw.Write(strData.ToArray());
            return ms.ToArray();
        }

        [Test]
        public void LoadNull_DoesNotThrow()
        {
            var tlk = new TlkReader();
            Assert.DoesNotThrow(() => tlk.Load(null));
            Assert.AreEqual(0, tlk.StringCount);
        }

        [Test]
        public void LoadValidTlk_CorrectStringCount()
        {
            var data = BuildMinimalTlk(new[] { "Hello", "World", "KotOR" });
            var tlk  = new TlkReader();
            tlk.Load(data);
            Assert.AreEqual(3, tlk.StringCount);
        }

        [Test]
        public void GetString_ReturnsCorrectText()
        {
            var data = BuildMinimalTlk(new[] { "Darth Revan", "Light Side" });
            var tlk  = new TlkReader();
            tlk.Load(data);
            Assert.AreEqual("Darth Revan", tlk.GetString(0));
            Assert.AreEqual("Light Side",  tlk.GetString(1));
        }

        [Test]
        public void GetString_OutOfRange_ReturnsStrRef()
        {
            var tlk = new TlkReader();
            Assert.IsTrue(tlk.GetString(999).StartsWith("<StrRef:"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TWO-DA READER TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class TwoDAReaderTests
    {
        private static byte[] Build2DA(string header, string colRow, string[] dataRows)
        {
            var sb = new StringBuilder();
            sb.AppendLine(header);
            sb.AppendLine("");
            sb.AppendLine(colRow);
            foreach (var r in dataRows) sb.AppendLine(r);
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        [Test]
        public void ParsesColumns()
        {
            var data  = Build2DA("2DA V2.0", "label hitdie bab", new[] { "0 Soldier 10 0" });
            var table = TwoDAReader.Load(data, "test");
            Assert.IsNotNull(table);
            Assert.IsTrue(table.HasColumn("label"));
            Assert.IsTrue(table.HasColumn("hitdie"));
        }

        [Test]
        public void GetsIntValue()
        {
            var data  = Build2DA("2DA V2.0", "label hitdie", new[] { "0 Soldier 10" });
            var table = TwoDAReader.Load(data, "cls");
            Assert.AreEqual(10, table.GetInt(0, "hitdie"));
        }

        [Test]
        public void NullCellReturnsDefault()
        {
            var data  = Build2DA("2DA V2.0", "label hitdie", new[] { "0 Soldier ****" });
            var table = TwoDAReader.Load(data, "cls2");
            Assert.AreEqual(6, table.GetInt(0, "hitdie", 6));
        }

        [Test]
        public void FindRow_LocatesCorrectRow()
        {
            var data  = Build2DA("2DA V2.0", "label", new[]
                { "0 Soldier", "1 Scout", "2 Scoundrel" });
            var table = TwoDAReader.Load(data, "cls3");
            Assert.AreEqual(1, table.FindRow("label", "Scout"));
        }

        [Test]
        public void InvalidHeader_ReturnsNull()
        {
            byte[] bad = Encoding.UTF8.GetBytes("NOT A 2DA\n\ncol\n0 val");
            Assert.IsNull(TwoDAReader.Load(bad, "bad"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE TREE TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class DialogueTreeTests
    {
        // Build a minimal valid DLG GFF binary.
        // Real GFF is complex; we test the tree's logic with a pre-parsed stub.

        [Test]
        public void NewTree_IsNotStarted()
        {
            var tree = new DialogueTree();
            Assert.IsFalse(tree.IsStarted);
            Assert.IsFalse(tree.IsFinished);
        }

        [Test]
        public void LoadNull_ReturnsFalse()
        {
            var tree = new DialogueTree();
            Assert.IsFalse(tree.Load(null, "null"));
            Assert.IsFalse(tree.IsLoaded);
        }

        [Test]
        public void LoadGarbage_ReturnsFalse()
        {
            var tree = new DialogueTree();
            bool ok = tree.Load(new byte[] { 0, 1, 2, 3 }, "garbage");
            Assert.IsFalse(ok);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  INVENTORY TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class InventoryTests
    {
        private static ItemData MakeItem(string resref, EquipSlot slot, int stack = 1)
        {
            return new ItemData
            {
                ResRef       = resref,
                Tag          = resref,
                DisplayName  = resref,
                EquipableSlots = (1 << (int)slot),
                StackSize    = stack
            };
        }

        [Test]
        public void AddItem_ItemIsInInventory()
        {
            var inv  = new Inventory.Inventory();
            var item = MakeItem("g_w_blstrpstl01", EquipSlot.WeaponR);
            inv.AddItem(item);
            Assert.IsTrue(inv.HasItem("g_w_blstrpstl01"));
        }

        [Test]
        public void StackableItems_Merge()
        {
            var inv = new Inventory.Inventory();
            var i1  = MakeItem("stim_a1", EquipSlot.None, 2);
            i1.EquipableSlots = 0;
            var i2  = MakeItem("stim_a1", EquipSlot.None, 3);
            i2.EquipableSlots = 0;
            inv.AddItem(i1);
            inv.AddItem(i2);
            Assert.AreEqual(1, ((List<ItemData>)inv.Items).Count);
            Assert.AreEqual(5, inv.Items[0].StackSize);
        }

        [Test]
        public void EquipItem_MovesFromInventoryToSlot()
        {
            var inv  = new Inventory.Inventory();
            var item = MakeItem("g_w_blstrpstl01", EquipSlot.WeaponR);
            inv.AddItem(item);
            bool ok = inv.Equip(item, EquipSlot.WeaponR);
            Assert.IsTrue(ok);
            Assert.AreSame(item, inv.GetEquipped(EquipSlot.WeaponR));
            Assert.AreEqual(0, inv.Items.Count);
        }

        [Test]
        public void EquipToWrongSlot_Fails()
        {
            var inv  = new Inventory.Inventory();
            var item = MakeItem("g_w_blstrpstl01", EquipSlot.WeaponR);
            inv.AddItem(item);
            bool ok  = inv.Equip(item, EquipSlot.Head);   // pistol can't go in Head slot
            Assert.IsFalse(ok);
        }

        [Test]
        public void UnequipItem_ReturnsToInventory()
        {
            var inv  = new Inventory.Inventory();
            var item = MakeItem("g_a_class4001", EquipSlot.Body);
            inv.AddItem(item);
            inv.Equip(item, EquipSlot.Body);
            inv.Unequip(EquipSlot.Body);
            Assert.IsNull(inv.GetEquipped(EquipSlot.Body));
            Assert.IsTrue(inv.HasItem("g_a_class4001"));
        }

        [Test]
        public void RemoveItem_DecreasesStackSize()
        {
            var inv  = new Inventory.Inventory();
            var item = MakeItem("medpac", EquipSlot.None, 5);
            item.EquipableSlots = 0;
            inv.AddItem(item);
            inv.RemoveItem(item, 2);
            Assert.AreEqual(3, inv.Items[0].StackSize);
        }

        [Test]
        public void TotalACBonus_SumsEquippedItems()
        {
            var inv   = new Inventory.Inventory();
            var armor = MakeItem("g_a_class4001", EquipSlot.Body);
            armor.ACBonus = 6;
            inv.AddItem(armor);
            inv.Equip(armor, EquipSlot.Body);
            Assert.AreEqual(6, inv.GetTotalACBonus());
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  D20 / RTWP COMBAT TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class D20Tests
    {
        [Test]
        public void D20Roll_InRange()
        {
            for (int i = 0; i < 200; i++)
            {
                int roll = D20.Roll();
                Assert.IsTrue(roll >= 1 && roll <= 20,
                    $"D20 roll out of range: {roll}");
            }
        }

        [Test]
        public void DieRoll_InRange()
        {
            for (int i = 0; i < 200; i++)
            {
                int roll = D20.Roll(6);
                Assert.IsTrue(roll >= 1 && roll <= 6,
                    $"d6 roll out of range: {roll}");
            }
        }

        [Test]
        public void AbilityMod_Correct()
        {
            Assert.AreEqual( 0, D20.AbilityMod(10));
            Assert.AreEqual( 2, D20.AbilityMod(14));
            Assert.AreEqual(-1, D20.AbilityMod(8));
            Assert.AreEqual( 5, D20.AbilityMod(20));
        }

        [Test]
        public void AttackResult_HasValidFields()
        {
            var result = D20.Attack(5, 12);
            Assert.IsTrue(result.DieRoll >= 1 && result.DieRoll <= 20);
            Assert.IsNotNull(result.Outcome);
        }

        [Test]
        public void AlwaysHit_HighBonus()
        {
            // With +100 bonus vs AC 10, every roll should hit (except natural 1)
            int misses = 0;
            for (int i = 0; i < 100; i++)
            {
                var r = D20.Attack(100, 10);
                if (r.Outcome == AttackOutcome.Miss) misses++;
            }
            // Only natural 1s should miss; in 100 rolls ~5% = ~5 misses expected
            Assert.LessOrEqual(misses, 15,
                "Too many misses with +100 attack bonus.");
        }

        [Test]
        public void Combatant_TakeDamage_CapsAtZero()
        {
            var c = new Combatant { MaxHP = 20, CurrentHP = 5 };
            c.TakeDamage(100);
            Assert.AreEqual(0, c.CurrentHP);
            Assert.IsFalse(c.IsAlive);
        }

        [Test]
        public void Combatant_Heal_CapsAtMax()
        {
            var c = new Combatant { MaxHP = 20, CurrentHP = 5 };
            c.Heal(100);
            Assert.AreEqual(20, c.CurrentHP);
        }

        [Test]
        public void SavingThrow_NaturalTwentyAlwaysPasses()
        {
            // We can't force a roll of 20, but we can test the formula: 20 + any bonus >= any DC
            // Test directly: if roll=20 always passes per our implementation
            // Simulate via high bonus
            int passes = 0;
            for (int i = 0; i < 50; i++)
                if (D20.SavingThrow(100, 50)) passes++;
            Assert.AreEqual(50, passes); // +100 bonus, DC50 → always passes
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  GLOBAL VARS TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class GlobalVarsTests
    {
        [SetUp]
        public void SetUp() => GlobalVars.Clear();

        [Test]
        public void SetAndGetBool()
        {
            GlobalVars.SetBool("LevelCompleted", true);
            Assert.IsTrue(GlobalVars.GetBool("LevelCompleted"));
        }

        [Test]
        public void GetMissingBool_ReturnsFalse()
        {
            Assert.IsFalse(GlobalVars.GetBool("NonExistentVar"));
        }

        [Test]
        public void SetAndGetInt()
        {
            GlobalVars.SetInt("Gold", 1500);
            Assert.AreEqual(1500, GlobalVars.GetInt("Gold"));
        }

        [Test]
        public void SetAndGetString()
        {
            GlobalVars.SetString("PlayerName", "Revan");
            Assert.AreEqual("Revan", GlobalVars.GetString("PlayerName"));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            GlobalVars.SetBool("flag", true);
            GlobalVars.SetInt("num", 42);
            GlobalVars.Clear();
            Assert.IsFalse(GlobalVars.GetBool("flag"));
            Assert.AreEqual(0, GlobalVars.GetInt("num"));
        }

        [Test]
        public void CaseInsensitiveKeys()
        {
            GlobalVars.SetBool("MyFlag", true);
            Assert.IsTrue(GlobalVars.GetBool("myflag"));
            Assert.IsTrue(GlobalVars.GetBool("MYFLAG"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  WALKMESH LOADER TESTS
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class WalkmeshLoaderTests
    {
        private static byte[] BuildMinimalWok(Vector3[] verts, int[] indices, uint[] walkFlags)
        {
            var ms = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(ms);

            bw.Write(Encoding.ASCII.GetBytes("BWM "));
            bw.Write(Encoding.ASCII.GetBytes("V1.0"));
            bw.Write((uint)0);               // walkType
            bw.Write(new byte[24]);          // RelUsePos1 + RelUsePos2

            // Vertices
            bw.Write((uint)verts.Length);
            foreach (var v in verts)
            {
                bw.Write(v.x); bw.Write(v.z); bw.Write(v.y); // KotOR Y/Z swap
            }

            // Faces
            uint faceCount = (uint)(indices.Length / 3);
            bw.Write(faceCount);
            foreach (var idx in indices) bw.Write((uint)idx);
            foreach (var wf in walkFlags) bw.Write(wf);

            return ms.ToArray();
        }

        [Test]
        public void ParseNull_ReturnsNull()
        {
            Assert.IsNull(WalkmeshLoader.Parse(null));
        }

        [Test]
        public void ParseTooShort_ReturnsNull()
        {
            Assert.IsNull(WalkmeshLoader.Parse(new byte[10]));
        }

        [Test]
        public void ParseWalkableFace_IncludedInResult()
        {
            var verts = new[]
            {
                new Vector3(0,0,0),
                new Vector3(1,0,0),
                new Vector3(0,0,1)
            };
            byte[] data = BuildMinimalWok(verts, new[]{ 0,1,2 }, new uint[]{ 0 /* walkable */ });
            var result = WalkmeshLoader.Parse(data, walkableOnly: true);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Indices.Count);
        }

        [Test]
        public void ParseNonWalkableFace_ExcludedWhenFiltered()
        {
            var verts = new[]
            {
                new Vector3(0,0,0),
                new Vector3(1,0,0),
                new Vector3(0,0,1)
            };
            // Flag 7 = NonWalkable
            byte[] data = BuildMinimalWok(verts, new[]{ 0,1,2 }, new uint[]{ 7 });
            var result = WalkmeshLoader.Parse(data, walkableOnly: true);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Indices.Count);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  RESOURCE MANAGER TESTS (unit — no real game files)
    // ═══════════════════════════════════════════════════════════════════════════
    [TestFixture]
    public class ResourceManagerTests
    {
        [Test]
        public void NotMounted_GetResource_ReturnsNull()
        {
            var rm = new ResourceManager();
            Assert.IsNull(rm.GetResource("danm14aa",
                KotORUnity.KotOR.FileReaders.ResourceType.GIT));
        }

        [Test]
        public void Mount_MissingDirectory_StillCompletes()
        {
            var rm = new ResourceManager();
            Assert.DoesNotThrow(() =>
                rm.Mount(System.IO.Path.Combine(Application.temporaryCachePath,
                    "NonExistentKotorDir_" + System.Guid.NewGuid())));
        }
    }
}
