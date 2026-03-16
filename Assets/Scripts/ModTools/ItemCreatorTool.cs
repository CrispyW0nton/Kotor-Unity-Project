using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using KotORUnity.Inventory;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ITEM CREATOR TOOL  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Item property definition for the UTI author.
    /// Mirrors the PropertiesList GFF array in KotOR UTI files.
    /// </summary>
    [Serializable]
    public class ItemPropertyDef
    {
        // Row indices into iprp_* 2DA tables (same as vanilla KotOR)
        public int PropertyType;    // iprp_feats / iprp_paramtable row
        public int SubType;         // e.g. damage type, bonus type
        public int CostTable;       // iprp_costtable row
        public int CostValue;       // value in that table
        public int Param1;          // iprp_param1 row
        public int Param1Value;

        // Human-readable display used in the editor UI
        public string PropertyLabel = "";
        public string SubTypeLabel  = "";
        public string CostLabel     = "";

        public override string ToString() =>
            $"{PropertyLabel} / {SubTypeLabel} ({CostLabel})";
    }

    /// <summary>
    /// Full item definition as authored in the Item Creator Tool.
    /// Serialises to JSON and can be exported as a UTI GFF for packaging.
    /// </summary>
    [Serializable]
    public class ItemDefinition
    {
        // ── IDENTITY ──────────────────────────────────────────────────────────
        public string ResRef       = "my_item_001";
        public string Tag          = "MY_ITEM_001";
        public string DisplayName  = "Custom Item";
        public string Description  = "";
        public string IconResRef   = "iplsc001";    // texture resref for inventory icon
        public string ModelResRef  = "";            // 3-D model resref (optional override)
        public string Comment      = "";

        // ── CLASSIFICATION ────────────────────────────────────────────────────
        /// <summary>Base item row in baseitems.2da.</summary>
        public int BaseItemRow = 0;
        /// <summary>ItemType: 0 = Misc, 1 = Weapon, 2 = Armour, 3 = Quest, 4 = Consumable.</summary>
        public int ItemType = 0;
        /// <summary>Bitmask of EquipSlot values where this item can be equipped.</summary>
        public int EquipableSlots = 0;

        // ── WEAPON STATS ──────────────────────────────────────────────────────
        public int AttackBonus    = 0;
        public int DamageBonus    = 0;
        public int DamageDie      = 6;   // d4/d6/d8/d10/d12/d20
        public int DamageNumDice  = 1;
        /// <summary>Damage type bitmask: 1=Bludgeoning 2=Piercing 4=Slashing 8=Ion 16=Sonic 32=Blaster.</summary>
        public int DamageType     = 4;   // default: slashing
        public int CritThreat     = 20;  // rolls this or higher = threat
        public int CritMult       = 2;   // multiply dice on confirmed crit

        // ── RANGED EXTRAS ─────────────────────────────────────────────────────
        public bool IsRanged          = false;
        public int  ShotsFired        = 1;   // shots per attack action (blasters)
        public string AmmoResRef      = "";  // link to ammo belt UTI

        // ── ARMOUR / SHIELD STATS ─────────────────────────────────────────────
        public int ACBonus       = 0;
        public int MaxDexBonus   = 99;
        public int ArcaneFailure = 0;   // armour check penalty
        public int ArmorClass    = 0;   // base armour class type (light/medium/heavy = 0/1/2)

        // ── GENERAL PROPERTIES ────────────────────────────────────────────────
        public int  Cost       = 100;
        public int  StackSize  = 1;
        public int  MaxStacks  = 1;
        public int  Charges    = 0;     // > 0 → consumable
        public bool Identified = true;
        public bool Stolen     = false;
        public bool QuestItem  = false;
        public bool Droppable  = true;
        public bool Pickpocketable = false;

        // ── ITEM PROPERTIES (iprp_*) ──────────────────────────────────────────
        public List<ItemPropertyDef> Properties = new List<ItemPropertyDef>();

        // ── SCRIPT HOOKS ──────────────────────────────────────────────────────
        public string OnActivate   = "";  // NWScript when used
        public string OnAcquire    = "";
        public string OnUnacquire  = "";
        public string OnHitCastSpell = "";

        // ── ALIGNMENT RESTRICTIONS ────────────────────────────────────────────
        public bool RestrictByAlignment = false;
        public bool LightSideOnly = false;
        public bool DarkSideOnly  = false;

        // ── CLASS RESTRICTIONS ────────────────────────────────────────────────
        public bool RestrictByClass = false;
        /// <summary>Bitmask: 1=Scoundrel 2=Soldier 4=Scout 8=JediGuardian 16=JediConsular 32=JediSentinel.</summary>
        public int AllowedClasses = 0x3F;  // all by default

        // ── NEW-PORT EXTRAS (no vanilla equivalent) ───────────────────────────
        public float WorldDropChance   = 1f;    // 0–1; used by loot tables
        public string CustomVFXResRef  = "";    // particle effect to attach when equipped
        public string SoundOnHit       = "";    // override hit SFX resref
        public string SoundOnEquip     = "";    // override equip SFX resref
        public bool   AllowModding     = true;  // false = locked, can't add properties
        public List<string> Tags       = new List<string>();  // searchable metadata tags

        // ── FILE PATH ─────────────────────────────────────────────────────────
        public string FilePath = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ITEM CREATOR SERVICE  —  backend logic
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Service class that manages item definition authoring.
    ///
    /// Unlocked capabilities vs vanilla KotOR UTI editor (KotOR Tool):
    ///   • Shot count per attack (blasters)
    ///   • World drop-chance weight for procedural loot
    ///   • Custom VFX / SFX overrides
    ///   • Modding lock flag
    ///   • Freeform metadata tags for community searchability
    ///   • Ranged weapon burst-fire support
    ///   • Unrestricted property count (vanilla cap was ~16 properties)
    /// </summary>
    public class ItemCreatorTool
    {
        // ── CONSTANTS ─────────────────────────────────────────────────────────
        public const string FileExtension    = ".item_def.json";
        private const string DefaultFolder   = "ModOutput/Items";

        // Vanilla EquipSlot mapping (mirrors EquipSlot enum)
        public static readonly Dictionary<string, int> SlotBits = new Dictionary<string, int>
        {
            { "Head",     1 << 0 },
            { "Body",     1 << 1 },
            { "Hands",    1 << 2 },
            { "WeaponR",  1 << 3 },
            { "WeaponL",  1 << 4 },
            { "Belt",     1 << 5 },
            { "Implant",  1 << 6 },
            { "Visor",    1 << 7 },
            { "Cloak",    1 << 8 },  // new-port slot
        };

        // Damage die options
        public static readonly int[] DamageDieOptions = { 2, 4, 6, 8, 10, 12, 20 };

        // ── STATE ─────────────────────────────────────────────────────────────
        public ItemDefinition Current { get; private set; } = new ItemDefinition();
        public bool IsDirty { get; private set; } = false;
        public string LastError { get; private set; } = "";

        // ── CRUD ──────────────────────────────────────────────────────────────

        /// <summary>Start a fresh item.</summary>
        public void NewItem(string resref = "my_item_001")
        {
            Current = new ItemDefinition { ResRef = resref, Tag = resref.ToUpperInvariant() };
            IsDirty = false;
            LastError = "";
        }

        /// <summary>Save current item to JSON.</summary>
        public bool Save(string outputFolder = "")
        {
            try
            {
                if (string.IsNullOrEmpty(outputFolder))
                    outputFolder = Path.Combine(Application.persistentDataPath, DefaultFolder);
                Directory.CreateDirectory(outputFolder);

                string path = Path.Combine(outputFolder, SanitiseName(Current.ResRef) + FileExtension);
                File.WriteAllText(path, JsonUtility.ToJson(Current, prettyPrint: true), Encoding.UTF8);
                Current.FilePath = path;
                IsDirty = false;
                Debug.Log($"[ItemCreatorTool] Saved → {path}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[ItemCreatorTool] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Load an item definition from JSON.</summary>
        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) { LastError = $"Not found: {filePath}"; return false; }
                Current = JsonUtility.FromJson<ItemDefinition>(File.ReadAllText(filePath, Encoding.UTF8));
                Current.FilePath = filePath;
                IsDirty = false;
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[ItemCreatorTool] Load failed: {ex.Message}");
                return false;
            }
        }

        // ── PROPERTY MANAGEMENT ───────────────────────────────────────────────

        /// <summary>Add an item property.</summary>
        public void AddProperty(int propertyType, int subType, int costTable,
                                int costValue, int param1 = 0, int param1Value = 0,
                                string label = "", string subLabel = "", string costLabel = "")
        {
            Current.Properties.Add(new ItemPropertyDef
            {
                PropertyType  = propertyType,
                SubType       = subType,
                CostTable     = costTable,
                CostValue     = costValue,
                Param1        = param1,
                Param1Value   = param1Value,
                PropertyLabel = label,
                SubTypeLabel  = subLabel,
                CostLabel     = costLabel
            });
            IsDirty = true;
        }

        /// <summary>Remove a property by index.</summary>
        public bool RemoveProperty(int index)
        {
            if (index < 0 || index >= Current.Properties.Count) return false;
            Current.Properties.RemoveAt(index);
            IsDirty = true;
            return true;
        }

        // ── SLOT HELPERS ──────────────────────────────────────────────────────

        public void SetSlot(string slotName, bool enabled)
        {
            if (!SlotBits.TryGetValue(slotName, out int bit)) return;
            if (enabled) Current.EquipableSlots |= bit;
            else         Current.EquipableSlots &= ~bit;
            IsDirty = true;
        }

        public bool IsSlotEnabled(string slotName) =>
            SlotBits.TryGetValue(slotName, out int bit) && (Current.EquipableSlots & bit) != 0;

        // ── UTI TEXT EXPORT ───────────────────────────────────────────────────

        /// <summary>
        /// Export a UTI GFF text stub (JSON) that the CampaignPackager converts
        /// to binary GFF for packaging into the mod ERF.
        /// </summary>
        public string ExportUtiJson() =>
            JsonUtility.ToJson(Current, prettyPrint: true);

        // ── VALIDATION ────────────────────────────────────────────────────────

        public List<string> Validate()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Current.ResRef))
                issues.Add("ERROR: ResRef is empty.");
            else if (Current.ResRef.Length > 16)
                issues.Add($"WARNING: ResRef '{Current.ResRef}' is longer than 16 chars — vanilla engines will truncate it.");

            if (string.IsNullOrWhiteSpace(Current.DisplayName))
                issues.Add("ERROR: DisplayName is empty (player will see a blank name).");

            if (Current.EquipableSlots == 0 && Current.ItemType != 0)
                issues.Add("WARNING: No equip slots set — item cannot be equipped.");

            if (Current.ItemType == 1 && Current.DamageNumDice == 0)
                issues.Add("ERROR: Weapon has 0 damage dice.");

            if (Current.Cost < 0)
                issues.Add("ERROR: Cost cannot be negative.");

            if (Current.StackSize < 1)
                issues.Add("ERROR: StackSize must be ≥ 1.");

            if (Current.IsRanged && Current.ShotsFired < 1)
                issues.Add("ERROR: Ranged weapon must fire ≥ 1 shot.");

            if (Current.LightSideOnly && Current.DarkSideOnly)
                issues.Add("ERROR: Item cannot be both Light-Side-Only and Dark-Side-Only.");

            // Check for duplicate property types
            var propGroups = Current.Properties
                .GroupBy(p => p.PropertyType)
                .Where(g => g.Count() > 1);
            foreach (var g in propGroups)
                issues.Add($"WARNING: Duplicate item property type {g.Key} — stacking may not work as expected.");

            return issues;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static string SanitiseName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));

        // ── BATCH LISTING ─────────────────────────────────────────────────────

        public static IEnumerable<string> ListItemFiles(string folder)
        {
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + FileExtension, SearchOption.AllDirectories);
        }

        // ── QUICK FACTORIES ───────────────────────────────────────────────────

        /// <summary>Pre-fill with sensible defaults for a melee weapon.</summary>
        public void PresetMeleeWeapon(string resref, string displayName,
                                      int attackBonus = 2, int damageDie = 6,
                                      int numDice = 1, int critThreat = 20)
        {
            NewItem(resref);
            Current.DisplayName  = displayName;
            Current.ItemType     = 1;
            Current.BaseItemRow  = 18;  // short sword row in baseitems.2da
            Current.EquipableSlots = SlotBits["WeaponR"] | SlotBits["WeaponL"];
            Current.AttackBonus  = attackBonus;
            Current.DamageDie    = damageDie;
            Current.DamageNumDice = numDice;
            Current.CritThreat   = critThreat;
            Current.DamageType   = 4;   // slashing
            Current.Cost         = (attackBonus + numDice) * 50;
            IsDirty = true;
        }

        /// <summary>Pre-fill for a blaster pistol.</summary>
        public void PresetBlasterPistol(string resref, string displayName,
                                        int attackBonus = 1, int shots = 1)
        {
            NewItem(resref);
            Current.DisplayName  = displayName;
            Current.ItemType     = 1;
            Current.BaseItemRow  = 56;  // blaster pistol row
            Current.EquipableSlots = SlotBits["WeaponR"];
            Current.IsRanged     = true;
            Current.AttackBonus  = attackBonus;
            Current.DamageDie    = 6;
            Current.DamageNumDice = 1;
            Current.DamageType   = 32;  // blaster
            Current.ShotsFired   = shots;
            Current.CritThreat   = 20;
            Current.Cost         = 200 + attackBonus * 80 + (shots - 1) * 150;
            IsDirty = true;
        }

        /// <summary>Pre-fill for a medium armour.</summary>
        public void PresetMediumArmour(string resref, string displayName,
                                       int acBonus = 6, int maxDex = 3)
        {
            NewItem(resref);
            Current.DisplayName  = displayName;
            Current.ItemType     = 2;
            Current.BaseItemRow  = 30;  // medium battle armour row
            Current.EquipableSlots = SlotBits["Body"];
            Current.ACBonus      = acBonus;
            Current.MaxDexBonus  = maxDex;
            Current.ArmorClass   = 1;   // medium
            Current.Cost         = acBonus * 200;
            IsDirty = true;
        }

        /// <summary>Pre-fill for a medpac / consumable.</summary>
        public void PresetConsumable(string resref, string displayName,
                                     int charges = 1, string activateScript = "k_hos_medpac")
        {
            NewItem(resref);
            Current.DisplayName  = displayName;
            Current.ItemType     = 4;
            Current.BaseItemRow  = 70;  // medpac row
            Current.Charges      = charges;
            Current.StackSize    = charges;
            Current.MaxStacks    = 99;
            Current.OnActivate   = activateScript;
            Current.Cost         = charges * 40;
            IsDirty = true;
        }

        // ── EDITOR GUI STUB ───────────────────────────────────────────────────
        /// <summary>Minimal IMGUI draw for the EditorWindow.</summary>
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (Current == null) { UnityEditor.EditorGUILayout.HelpBox("No item loaded.", UnityEditor.MessageType.Info); return; }
            UnityEditor.EditorGUILayout.LabelField($"ResRef: {Current.ResRef}  Tag: {Current.Tag}  Dirty: {IsDirty}", UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
