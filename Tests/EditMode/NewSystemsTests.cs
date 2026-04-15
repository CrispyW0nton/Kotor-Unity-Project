using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.KotOR.Parsers;
using KotORUnity.Combat;
using KotORUnity.Inventory;
using KotORUnity.UI;
using KotORUnity.Audio;

namespace KotORUnity.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  RESOURCE TYPES TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class ResourceTypesTests
    {
        [Test]
        public void NoDuplicateConstantValues()
        {
            // Verify that the most-used pairs have distinct IDs
            Assert.AreNotEqual(ResourceType.TGA, ResourceType.DDS);
            Assert.AreNotEqual(ResourceType.MDL, ResourceType.MDX);
            Assert.AreNotEqual(ResourceType.UTC, ResourceType.UTP);
            Assert.AreNotEqual(ResourceType.GIT, ResourceType.IFO);
        }

        [Test]
        public void KnownConstantsMatchSpec()
        {
            Assert.AreEqual((ushort)3,    ResourceType.TGA);
            Assert.AreEqual((ushort)2002, ResourceType.MDL);
            Assert.AreEqual((ushort)3006, ResourceType.MDX);
            Assert.AreEqual((ushort)3007, ResourceType.TPC);
            Assert.AreEqual((ushort)2029, ResourceType.DLG);
            Assert.AreEqual((ushort)2010, ResourceType.NCS);
            Assert.AreEqual((ushort)3005, ResourceType.LIP);
            Assert.AreEqual((ushort)2023, ResourceType.UTC);
            Assert.AreEqual((ushort)2025, ResourceType.UTI);
            Assert.AreEqual((ushort)2026, ResourceType.UTD);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  TEXTURE LOADER TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class TextureLoaderTests
    {
        [Test]
        public void NullDataReturnsNull()
        {
            var tex = TextureLoader.Decode(null, "test");
            Assert.IsNull(tex);
        }

        [Test]
        public void TooShortDataReturnsNull()
        {
            var tex = TextureLoader.Decode(new byte[4], "test");
            Assert.IsNull(tex);
        }

        [Test]
        public void ValidTGA24_Decoded()
        {
            // Minimal valid 24-bit uncompressed TGA (type=2, 2×2 pixels)
            byte[] tga = new byte[18 + 2 * 2 * 3];
            tga[0] = 0;   // id length
            tga[1] = 0;   // color map type
            tga[2] = 2;   // image type (uncompressed RGB)
            // skip color map spec (5 bytes)
            // origin (4 bytes)
            tga[12] = 2; tga[13] = 0; // width  = 2
            tga[14] = 2; tga[15] = 0; // height = 2
            tga[16] = 24; // bits per pixel
            tga[17] = 0;  // image descriptor

            // Fill with blue pixels (BGR order)
            for (int i = 18; i < tga.Length; i += 3)
            { tga[i] = 255; tga[i+1] = 0; tga[i+2] = 0; } // B=255,G=0,R=0

            var tex = TextureLoader.Decode(tga, "test_tga");
            Assert.IsNotNull(tex);
            Assert.AreEqual(2, tex.width);
            Assert.AreEqual(2, tex.height);
        }

        [Test]
        public void ValidDDS_DXT1_Decoded()
        {
            // Minimal DDS DXT1 header (128 bytes + 8 bytes data for 4×4)
            byte[] dds = new byte[128 + 8];
            // Magic
            dds[0]='D'; dds[1]='D'; dds[2]='S'; dds[3]=' ';
            // Header size = 124
            BitConverter.GetBytes(124).CopyTo(dds, 4);
            // Flags (required: DDSD_CAPS|DDSD_HEIGHT|DDSD_WIDTH|DDSD_PIXELFORMAT|DDSD_LINEARSIZE)
            BitConverter.GetBytes(0x00021007).CopyTo(dds, 8);
            // Height, Width = 4
            BitConverter.GetBytes(4).CopyTo(dds, 12);
            BitConverter.GetBytes(4).CopyTo(dds, 16);
            // linearSize = 8
            BitConverter.GetBytes(8).CopyTo(dds, 20);
            // mipMapCount = 1
            BitConverter.GetBytes(1).CopyTo(dds, 28);
            // Pixel format size = 32
            BitConverter.GetBytes(32).CopyTo(dds, 76);
            // Pixel format flags = DDPF_FOURCC (4)
            BitConverter.GetBytes(4).CopyTo(dds, 80);
            // FourCC = "DXT1"
            dds[84]='D'; dds[85]='X'; dds[86]='T'; dds[87]='1';

            var tex = TextureLoader.Decode(dds, "test_dds");
            Assert.IsNotNull(tex);
            Assert.AreEqual(4, tex.width);
            Assert.AreEqual(4, tex.height);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  LIP READER TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class LipReaderTests
    {
        private byte[] BuildLip(float length, LipKeyframe[] frames)
        {
            int size = 12 + frames.Length * 5;
            byte[] data = new byte[size];
            // Signature + version
            data[0]='L'; data[1]='I'; data[2]='P'; data[3]=' ';
            data[4]='V'; data[5]='1'; data[6]='.'; data[7]='0';
            // Length
            BitConverter.GetBytes(length).CopyTo(data, 8);
            // Entry count
            BitConverter.GetBytes((uint)frames.Length).CopyTo(data, 12 - 4 + 4);
            // Wait — offset math: header is 12 bytes total
            // Re-layout: sig(4) + ver(4) + length(4) + count(4) = 16 bytes header
            // Let me rebuild
            data = new byte[16 + frames.Length * 5];
            data[0]='L'; data[1]='I'; data[2]='P'; data[3]=' ';
            data[4]='V'; data[5]='1'; data[6]='.'; data[7]='0';
            BitConverter.GetBytes(length).CopyTo(data, 8);
            BitConverter.GetBytes((uint)frames.Length).CopyTo(data, 12);
            for (int i = 0; i < frames.Length; i++)
            {
                int off = 16 + i * 5;
                BitConverter.GetBytes(frames[i].Time).CopyTo(data, off);
                data[off + 4] = frames[i].Shape;
            }
            return data;
        }

        [Test]
        public void NullDataReturnsNull()
        {
            Assert.IsNull(LipReader.Parse(null));
        }

        [Test]
        public void TooShortDataReturnsNull()
        {
            Assert.IsNull(LipReader.Parse(new byte[8]));
        }

        [Test]
        public void ValidLip_ParsesCorrectly()
        {
            var frames = new[]
            {
                new LipKeyframe { Time = 0.0f,  Shape = 17 },
                new LipKeyframe { Time = 0.25f, Shape = 3  },
                new LipKeyframe { Time = 0.75f, Shape = 0  },
            };
            byte[] data = BuildLip(1.5f, frames);

            var lip = LipReader.Parse(data);

            Assert.IsNotNull(lip);
            Assert.AreEqual(1.5f, lip.Length, 0.001f);
            Assert.AreEqual(3, lip.Keyframes.Length);
            Assert.AreEqual(17, lip.Keyframes[0].Shape);
            Assert.AreEqual(3,  lip.Keyframes[1].Shape);
            Assert.AreEqual(0.25f, lip.Keyframes[1].Time, 0.001f);
        }

        [Test]
        public void EmptyLip_ZeroFrames()
        {
            byte[] data = BuildLip(0f, Array.Empty<LipKeyframe>());
            var lip = LipReader.Parse(data);
            Assert.IsNotNull(lip);
            Assert.AreEqual(0, lip.Keyframes.Length);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class ForcePowerTests
    {
        [Test]
        public void ForcePowerDef_DefaultValues()
        {
            var def = new ForcePowerDef
            {
                SpellId  = 1,
                Label    = "Force Push",
                ForceCost = 10,
                CooldownSeconds = 3f,
                Alignment = ForcePowerAlignment.Universal
            };
            Assert.AreEqual("Force Push", def.Label);
            Assert.AreEqual(10, def.ForceCost);
            Assert.AreEqual(ForcePowerAlignment.Universal, def.Alignment);
        }

        [Test]
        public void ForcePowerInstance_CooldownTick()
        {
            var def = new ForcePowerDef { SpellId = 1, Label = "Test", CooldownSeconds = 5f };
            var inst = new ForcePowerInstance(def);

            Assert.IsTrue(inst.IsReady);
            inst.StartCooldown();
            Assert.IsFalse(inst.IsReady);

            inst.Tick(3f);
            Assert.IsFalse(inst.IsReady);
            Assert.AreEqual(2f, inst.CooldownRemaining, 0.01f);

            inst.Tick(2f);
            Assert.IsTrue(inst.IsReady);
        }

        [Test]
        public void ForcePowerInstance_CooldownDoesNotGoNegative()
        {
            var def  = new ForcePowerDef { SpellId = 1, Label = "T", CooldownSeconds = 2f };
            var inst = new ForcePowerInstance(def);
            inst.StartCooldown();
            inst.Tick(10f);
            Assert.AreEqual(0f, inst.CooldownRemaining);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MERCHANT SYSTEM TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class MerchantSystemTests
    {
        private MerchantData DefaultMerchant() => new MerchantData
        {
            MarkUp   = 150,
            MarkDown = 50
        };

        [Test]
        public void BuyPrice_DefaultMarkUp()
        {
            var m = DefaultMerchant();
            int price = MerchantSystem.BuyPrice(100, m, 0);
            Assert.AreEqual(150, price); // 100 × 150/100 = 150
        }

        [Test]
        public void SellPrice_DefaultMarkDown()
        {
            var m = DefaultMerchant();
            int price = MerchantSystem.SellPrice(100, m, 0);
            Assert.AreEqual(50, price); // 100 × 50/100 = 50
        }

        [Test]
        public void BuyPrice_PersuadeDiscount()
        {
            var m = DefaultMerchant();
            // Persuade 10 → discount 5% → markup 145%
            int price = MerchantSystem.BuyPrice(100, m, 10);
            Assert.AreEqual(145, price);
        }

        [Test]
        public void SellPrice_PersuadeBonus()
        {
            var m = DefaultMerchant();
            // Persuade 10 → bonus 5% → markdown 55%
            int price = MerchantSystem.SellPrice(100, m, 10);
            Assert.AreEqual(55, price);
        }

        [Test]
        public void BuyPrice_PersuadeCappedAt30()
        {
            var m = DefaultMerchant();
            // Max persuade discount: 30% → markup 120%
            int price = MerchantSystem.BuyPrice(100, m, 100);
            Assert.AreEqual(120, price);
        }

        [Test]
        public void SellPrice_NeverBelowOne()
        {
            var m = new MerchantData { MarkUp = 150, MarkDown = 1 };
            int price = MerchantSystem.SellPrice(1, m, 0);
            Assert.GreaterOrEqual(price, 1);
        }

        [Test]
        public void BuyPrice_NullMerchant_UsesDefaults()
        {
            // Should not throw
            int price = MerchantSystem.BuyPrice(100, null, 0);
            Assert.GreaterOrEqual(price, 1);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  JOURNAL SYSTEM TESTS  (data model only — no MonoBehaviour)
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class JournalDataTests
    {
        [Test]
        public void QuestEntry_DefaultStatus()
        {
            var q = new QuestEntry { QuestTag = "test_quest" };
            Assert.AreEqual(QuestState.Inactive, q.Status);
        }

        [Test]
        public void QuestObjective_Defaults()
        {
            var obj = new QuestObjective { StateId = 10, Description = "Desc" };
            Assert.AreEqual(10, obj.StateId);
            Assert.IsFalse(obj.IsComplete);
            Assert.IsFalse(obj.IsFailed);
        }

        [Test]
        public void JournalSaveData_RoundTrip()
        {
            var save = new JournalSaveData();
            save.Quests.Add(new JournalQuestSave { Tag = "q1", State = 5, Status = "Active" });
            Assert.AreEqual(1, save.Quests.Count);
            Assert.AreEqual("q1", save.Quests[0].Tag);
            Assert.AreEqual(5,    save.Quests[0].State);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CHARACTER CREATION DATA TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class CharacterCreationTests
    {
        [Test]
        public void AttributeSet_ModifierCalc()
        {
            var a = new AttributeSet { Strength = 16 };
            Assert.AreEqual(3, a.Modifier(16));  // (16-10)/2 = 3
            Assert.AreEqual(0, a.Modifier(10));  // (10-10)/2 = 0
            Assert.AreEqual(-1, a.Modifier(8));  // (8-10)/2 = -1
            Assert.AreEqual(5,  a.Modifier(20)); // (20-10)/2 = 5
        }

        [Test]
        public void NewGameConfig_DefaultValues()
        {
            var cfg = new NewGameConfig();
            Assert.AreEqual("Revan", cfg.PlayerName);
            Assert.AreEqual(0, cfg.ClassId);
            Assert.AreEqual(0, cfg.Gender);
            Assert.IsNotNull(cfg.Attributes);
        }

        [Test]
        public void AttributeSet_AllStatsDefault10()
        {
            var a = new AttributeSet();
            Assert.AreEqual(10, a.Strength);
            Assert.AreEqual(10, a.Dexterity);
            Assert.AreEqual(10, a.Constitution);
            Assert.AreEqual(10, a.Intelligence);
            Assert.AreEqual(10, a.Wisdom);
            Assert.AreEqual(10, a.Charisma);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  INVENTORY SYSTEM EXTENDED TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class InventoryExtendedTests
    {
        [Test]
        public void ItemData_AliasProperties()
        {
            var item = new ItemData
            {
                DisplayName = "Blaster Pistol",
                BaseItemName = "Pistol description",
                Icon = "w_bpistol_001"
            };
            Assert.AreEqual("Blaster Pistol", item.Name);
            Assert.AreEqual("Pistol description", item.Description);
            Assert.AreEqual("w_bpistol_001", item.IconResRef);
        }

        [Test]
        public void Inventory_Clear_RemovesAllItems()
        {
            var inv = new Inventory();
            inv.AddItem(new ItemData { ResRef = "item_a", DisplayName = "A", StackSize = 1 });
            inv.AddItem(new ItemData { ResRef = "item_b", DisplayName = "B", StackSize = 1 });
            Assert.AreEqual(2, inv.Items.Count);

            inv.Clear();
            Assert.AreEqual(0, inv.Items.Count);
        }

        [Test]
        public void Inventory_AllSlots_IteratesItems()
        {
            var inv = new Inventory();
            inv.AddItem(new ItemData { ResRef = "item_a", DisplayName = "A", StackSize = 1 });
            inv.AddItem(new ItemData { ResRef = "item_b", DisplayName = "B", StackSize = 1 });

            var slots = new List<InventorySlot>(inv.AllSlots);
            Assert.AreEqual(2, slots.Count);
        }

        [Test]
        public void Inventory_GetAllItemResRefs_ReturnsAllResrefs()
        {
            var inv = new Inventory();
            inv.AddItem(new ItemData { ResRef = "r1", DisplayName = "I1", StackSize = 1 });
            inv.AddItem(new ItemData { ResRef = "r2", DisplayName = "I2", StackSize = 1 });

            var refs = inv.GetAllItemResRefs();
            Assert.AreEqual(2, refs.Count);
            Assert.IsTrue(refs.Contains("r1"));
            Assert.IsTrue(refs.Contains("r2"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MDL READER SMOKE TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class MdlReaderTests
    {
        [Test]
        public void Parse_NullData_ReturnsNull()
        {
            Assert.IsNull(MdlReader.Parse(null, null));
        }

        [Test]
        public void Parse_TooShortData_ReturnsNull()
        {
            Assert.IsNull(MdlReader.Parse(new byte[10], null));
        }

        [Test]
        public void Parse_AllZeroHeader_ReturnsNullOrEmpty()
        {
            // 12 (file header) + 80 (geo header) = minimum size
            byte[] zeros = new byte[200];
            // Should not throw; may return null or a model with no nodes
            var model = MdlReader.Parse(zeros, null);
            // Either null (zero root offset) or empty root is acceptable
            if (model != null)
                Assert.IsNull(model.RootNode);
        }

        [Test]
        public void MdlMesh_FieldDefaults()
        {
            var mesh = new MdlReader.MdlMesh
            {
                Vertices  = new Vector3[3],
                Triangles = new int[] { 0, 1, 2 },
                Alpha     = 1f
            };
            Assert.AreEqual(3, mesh.Vertices.Length);
            Assert.IsFalse(mesh.IsTransparent);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SAVE MANAGER DATA TESTS
    // ═══════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class SaveManagerDataTests
    {
        [Test]
        public void GameState_Serializable()
        {
            var state = new SaveSystem.GameState
            {
                version    = "2.1",
                moduleName = "danm14aa",
                kotorDir   = "/games/kotor",
                activeMode = "RTS"
            };

            string json = UnityEngine.JsonUtility.ToJson(state);
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("danm14aa"));
            Assert.IsTrue(json.Contains("2.1"));

            var restored = UnityEngine.JsonUtility.FromJson<SaveSystem.GameState>(json);
            Assert.AreEqual("danm14aa", restored.moduleName);
            Assert.AreEqual("RTS",     restored.activeMode);
        }

        [Test]
        public void InventorySaveData_Serializable()
        {
            var data = new SaveSystem.InventorySaveData
            {
                itemResRefs = new[] { "w_bpistol_001", "i_mask_01" },
                credits     = 1500
            };
            string json = UnityEngine.JsonUtility.ToJson(data);
            Assert.IsTrue(json.Contains("w_bpistol_001"));
        }

        [Test]
        public void GlobalVarsSaveData_ArraysSerializable()
        {
            var gv = new SaveSystem.GlobalVarsSaveData
            {
                boolKeys    = new[] { "door_open", "met_carth" },
                boolValues  = new[] { true, false },
                intKeys     = new[] { "score" },
                intValues   = new[] { 42 }
            };
            string json = UnityEngine.JsonUtility.ToJson(gv);
            Assert.IsTrue(json.Contains("door_open"));
            Assert.IsTrue(json.Contains("42"));
        }
    }
}
