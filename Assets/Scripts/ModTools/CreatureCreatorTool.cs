using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CREATURE CREATOR TOOL  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attribute set for a creature / NPC definition.
    /// Mirrors the six core KotOR attributes + derived saves.
    /// </summary>
    [Serializable]
    public class CreatureAttributes
    {
        [Range(3, 25)] public int Strength     = 10;
        [Range(3, 25)] public int Dexterity    = 10;
        [Range(3, 25)] public int Constitution = 10;
        [Range(3, 25)] public int Intelligence = 10;
        [Range(3, 25)] public int Wisdom       = 10;
        [Range(3, 25)] public int Charisma     = 10;

        // Derived modifiers
        public int StrMod => AbilityMod(Strength);
        public int DexMod => AbilityMod(Dexterity);
        public int ConMod => AbilityMod(Constitution);
        public int IntMod => AbilityMod(Intelligence);
        public int WisMod => AbilityMod(Wisdom);
        public int ChaMod => AbilityMod(Charisma);

        private static int AbilityMod(int score) => (score - 10) / 2;
    }

    /// <summary>
    /// Single skill rank for a creature.
    /// </summary>
    [Serializable]
    public class CreatureSkillEntry
    {
        public string SkillName = "";
        public int    Ranks     = 0;
        public bool   ClassSkill = false;
    }

    /// <summary>
    /// Feat granted to the creature at creation.
    /// </summary>
    [Serializable]
    public class CreatureFeatEntry
    {
        public int    FeatRow  = 0;    // index into feats.2da
        public string FeatName = "";
    }

    /// <summary>
    /// Default equipment slot for a creature.
    /// </summary>
    [Serializable]
    public class CreatureEquipSlot
    {
        public string SlotName  = "WeaponR";
        public string ItemResRef = "";        // UTI resref to auto-equip
    }

    /// <summary>
    /// AI behaviour preset for a creature.
    /// </summary>
    public enum CreatureAIPreset
    {
        Passive,        // never attacks first
        Coward,         // flees when HP < 25%
        Aggressive,     // attacks nearest enemy on sight
        Defensive,      // stays close to home position, counter-attacks
        Patrol,         // walks a waypoint path
        GuardPoint,     // holds a fixed location
        Companion,      // follows and assists player
        Custom          // full NWScript control
    }

    /// <summary>
    /// Full NPC / creature definition as authored in the Creature Creator Tool.
    /// Serialises to JSON; CampaignPackager converts it to binary UTC GFF.
    ///
    /// New-port extras (no vanilla equivalent):
    ///   • Faction system with configurable disposition matrix
    ///   • Per-creature walk / run / combat animation override
    ///   • Loot table reference (JSON list instead of hard-coded GFF)
    ///   • AI behaviour preset + custom patrol waypoints
    ///   • Force Points pool (vanilla companions had hardcoded values)
    ///   • Dialogue tree override per conversation trigger range
    /// </summary>
    [Serializable]
    public class CreatureDefinition
    {
        // ── IDENTITY ──────────────────────────────────────────────────────────
        public string ResRef        = "my_creature_001";
        public string Tag           = "MY_CREATURE_001";
        public string FirstName     = "Custom NPC";
        public string LastName      = "";
        public string Description   = "";
        public string Comment       = "";

        // ── APPEARANCE ────────────────────────────────────────────────────────
        public int    AppearanceRow = 0;    // row in appearance.2da
        public string BodyBag       = "";   // resref of corpse placeable
        public string Portrait      = "po_pmhc1";  // portrait resref
        public float  ScaleX        = 1f;
        public float  ScaleY        = 1f;
        public float  ScaleZ        = 1f;
        public string SoundsetRow   = "";   // row in soundset.2da

        // ── CLASSIFICATION ────────────────────────────────────────────────────
        public int    RacialType    = 6;    // 6 = Human (races.2da row)
        public int    SubRace       = 0;
        /// <summary>0=Scoundrel 1=Soldier 2=Scout 3=JediGuardian 4=JediConsular 5=JediSentinel.</summary>
        public int    ClassType     = 1;
        public int    Level         = 1;
        public int    CR            = 1;    // challenge rating for XP calculation
        public string Faction       = "Hostile";  // faction tag
        public bool   IsImmortal    = false;
        public bool   Plot          = false;       // plot flag = no kill
        public bool   Interruptable = true;

        // ── STATS ─────────────────────────────────────────────────────────────
        public CreatureAttributes Attributes = new CreatureAttributes();
        public int  MaxHP          = 10;
        public int  CurrentHP      = 10;
        public int  MaxFP          = 0;     // Force Points (0 = no Force)
        public int  CurrentFP      = 0;
        public int  NaturalAC      = 0;     // e.g. beast hide
        public int  FortSave       = 0;
        public int  ReflexSave     = 0;
        public int  WillSave       = 0;
        public int  ArmorClass     = 10;
        public int  AttackBonus    = 0;

        // ── SKILLS ────────────────────────────────────────────────────────────
        public List<CreatureSkillEntry> Skills = new List<CreatureSkillEntry>
        {
            new CreatureSkillEntry { SkillName = "Computer",      ClassSkill = false },
            new CreatureSkillEntry { SkillName = "Demolitions",   ClassSkill = false },
            new CreatureSkillEntry { SkillName = "Stealth",       ClassSkill = false },
            new CreatureSkillEntry { SkillName = "Awareness",     ClassSkill = true  },
            new CreatureSkillEntry { SkillName = "Persuade",      ClassSkill = false },
            new CreatureSkillEntry { SkillName = "Repair",        ClassSkill = false },
            new CreatureSkillEntry { SkillName = "Security",      ClassSkill = false },
            new CreatureSkillEntry { SkillName = "TreatInjury",   ClassSkill = true  },
        };

        // ── FEATS ─────────────────────────────────────────────────────────────
        public List<CreatureFeatEntry> Feats = new List<CreatureFeatEntry>();

        // ── FORCE POWERS ──────────────────────────────────────────────────────
        public List<int> KnownPowers = new List<int>();   // rows in spells.2da

        // ── DEFAULT EQUIPMENT ─────────────────────────────────────────────────
        public List<CreatureEquipSlot> DefaultEquipment = new List<CreatureEquipSlot>();

        // ── LOOT ──────────────────────────────────────────────────────────────
        public bool             DropInventory = true;
        public List<string>     StartingInventory = new List<string>();  // UTI resrefs
        public string           LootTableResRef   = "";   // custom JSON loot table
        public int              CreditsMin = 0;
        public int              CreditsMax = 0;

        // ── DIALOGUE ──────────────────────────────────────────────────────────
        public string DialogueResRef  = "";   // DLG file resref
        public float  ConversationRange = 5f; // metres; trigger auto-dialogue
        public bool   AutoDialogue    = false;

        // ── AI ────────────────────────────────────────────────────────────────
        public CreatureAIPreset AIPreset       = CreatureAIPreset.Aggressive;
        public float            SightRange     = 15f;
        public float            HearingRange   = 8f;
        public float            WalkSpeed      = 2.5f;
        public float            RunSpeed       = 5f;
        public List<string>     PatrolWaypoints = new List<string>();  // WP_ tags
        public bool             IgnoreParty    = false;
        public float            FleeHPPercent  = 0f;    // flee threshold (0 = never)

        // ── SCRIPT HOOKS ──────────────────────────────────────────────────────
        public string OnSpawn       = "";
        public string OnDeath       = "";
        public string OnHeartbeat   = "";
        public string OnPerception  = "";
        public string OnBlocked     = "";
        public string OnAttacked    = "";
        public string OnDamaged     = "";
        public string OnEndCombat   = "";
        public string OnConversation = "";
        public string OnUserDefined = "";

        // ── NEW-PORT EXTRAS ───────────────────────────────────────────────────
        public string AnimWalk      = "";   // override walk AnimationClip name
        public string AnimRun       = "";   // override run  AnimationClip name
        public string AnimIdle      = "";   // override idle AnimationClip name
        public string AnimCombatIdle = ""; // override combat-idle clip name
        public bool   CastsShadow   = true;
        public bool   UseRagdoll    = true;
        public float  AggroRadius   = 0f;   // 0 = use SightRange
        public List<string> MetaTags = new List<string>();

        // ── FILE ──────────────────────────────────────────────────────────────
        public string FilePath = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CREATURE CREATOR SERVICE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Service class that manages creature / NPC definition authoring.
    /// Handles CRUD, validation, stat derivation, and export.
    /// </summary>
    public class CreatureCreatorTool
    {
        public const string FileExtension  = ".utc_def.json";
        private const string DefaultFolder = "ModOutput/Creatures";

        // ── BASE XP TABLE (simplified level→XP awards) ───────────────────────
        private static readonly int[] CRXPTable =
        {
            0, 50, 125, 250, 500, 800, 1150, 1550, 2000, 2500,
            3000, 3750, 4500, 5500, 6600, 7800, 9200, 10800, 12600, 14400,
            16600, 18800, 21600, 24400, 27200, 30200
        };

        // ── STATE ─────────────────────────────────────────────────────────────
        public CreatureDefinition Current { get; private set; } = new CreatureDefinition();
        public bool IsDirty { get; private set; } = false;
        public string LastError { get; private set; } = "";

        // ── CRUD ──────────────────────────────────────────────────────────────

        public void NewCreature(string resref = "my_creature_001")
        {
            Current = new CreatureDefinition { ResRef = resref, Tag = resref.ToUpperInvariant() };
            IsDirty = false;
            LastError = "";
        }

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
                Debug.Log($"[CreatureCreatorTool] Saved → {path}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[CreatureCreatorTool] Save failed: {ex.Message}");
                return false;
            }
        }

        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) { LastError = $"Not found: {filePath}"; return false; }
                Current = JsonUtility.FromJson<CreatureDefinition>(File.ReadAllText(filePath, Encoding.UTF8));
                Current.FilePath = filePath;
                IsDirty = false;
                LastError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[CreatureCreatorTool] Load failed: {ex.Message}");
                return false;
            }
        }

        // ── STAT DERIVATION ───────────────────────────────────────────────────

        /// <summary>
        /// Auto-calculate HP, AC, saves and attack bonus based on class, level and
        /// attribute scores. Call after changing class / level / attributes.
        /// </summary>
        public void RecalcDerivedStats()
        {
            var a = Current.Attributes;
            int level = Mathf.Max(1, Current.Level);

            // Hit dice per class
            int hd = Current.ClassType switch
            {
                1 => 10,   // Soldier
                0 => 6,    // Scoundrel
                2 => 8,    // Scout
                3 => 10,   // Jedi Guardian
                4 => 6,    // Jedi Consular
                5 => 8,    // Jedi Sentinel
                _ => 8
            };

            Current.MaxHP      = hd * level + a.ConMod * level;
            Current.CurrentHP  = Current.MaxHP;

            // Base attack bonus (simplified fighter-table)
            Current.AttackBonus = Current.ClassType switch
            {
                1 or 3 => level,             // full BAB (Soldier, Guardian)
                0 or 4 => (level * 3) / 4,   // 3/4 BAB
                _      => (level * 3) / 4
            } + a.StrMod;

            // Saving throws
            Current.FortSave   = level / 2 + a.ConMod;
            Current.ReflexSave = level / 3 + a.DexMod;
            Current.WillSave   = level / 3 + a.WisMod;

            // AC
            Current.ArmorClass = 10 + a.DexMod + Current.NaturalAC;

            // Challenge rating ≈ level * 0.75 for humanoids
            Current.CR = Mathf.Max(1, (level * 3) / 4);

            // Force Points (Jedi classes only)
            bool isJedi = Current.ClassType >= 3 && Current.ClassType <= 5;
            if (isJedi)
            {
                Current.MaxFP    = level * (4 + a.WisMod);
                Current.CurrentFP = Current.MaxFP;
            }
            else
            {
                Current.MaxFP    = 0;
                Current.CurrentFP = 0;
            }

            IsDirty = true;
        }

        /// <summary>Estimated XP award when this creature is killed.</summary>
        public int EstimatedXPAward()
        {
            int cr = Mathf.Clamp(Current.CR, 0, CRXPTable.Length - 1);
            return CRXPTable[cr];
        }

        // ── EQUIPMENT HELPERS ─────────────────────────────────────────────────

        public void SetEquipment(string slotName, string itemResRef)
        {
            var existing = Current.DefaultEquipment.FirstOrDefault(e => e.SlotName == slotName);
            if (existing != null)
                existing.ItemResRef = itemResRef;
            else
                Current.DefaultEquipment.Add(new CreatureEquipSlot { SlotName = slotName, ItemResRef = itemResRef });
            IsDirty = true;
        }

        public void ClearEquipment(string slotName)
        {
            Current.DefaultEquipment.RemoveAll(e => e.SlotName == slotName);
            IsDirty = true;
        }

        // ── FEAT / POWER HELPERS ──────────────────────────────────────────────

        public void AddFeat(int featRow, string featName = "")
        {
            if (Current.Feats.Any(f => f.FeatRow == featRow)) return;
            Current.Feats.Add(new CreatureFeatEntry { FeatRow = featRow, FeatName = featName });
            IsDirty = true;
        }

        public bool RemoveFeat(int featRow)
        {
            int removed = Current.Feats.RemoveAll(f => f.FeatRow == featRow);
            if (removed > 0) IsDirty = true;
            return removed > 0;
        }

        public void AddForcePower(int spellRow)
        {
            if (!Current.KnownPowers.Contains(spellRow))
            {
                Current.KnownPowers.Add(spellRow);
                IsDirty = true;
            }
        }

        public bool RemoveForcePower(int spellRow)
        {
            if (Current.KnownPowers.Remove(spellRow)) { IsDirty = true; return true; }
            return false;
        }

        // ── VALIDATION ────────────────────────────────────────────────────────

        public List<string> Validate()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Current.ResRef))
                issues.Add("ERROR: ResRef is empty.");
            else if (Current.ResRef.Length > 16)
                issues.Add($"WARNING: ResRef '{Current.ResRef}' exceeds 16-char vanilla limit.");

            if (string.IsNullOrWhiteSpace(Current.FirstName))
                issues.Add("WARNING: FirstName is empty — creature will show blank name.");

            if (Current.Level < 1)
                issues.Add("ERROR: Level must be ≥ 1.");

            if (Current.MaxHP < 1)
                issues.Add("ERROR: MaxHP must be ≥ 1. Run RecalcDerivedStats() or set manually.");

            // Check attribute range
            var attrs = Current.Attributes;
            int[] scores = { attrs.Strength, attrs.Dexterity, attrs.Constitution,
                             attrs.Intelligence, attrs.Wisdom, attrs.Charisma };
            string[] names = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
            for (int i = 0; i < scores.Length; i++)
            {
                if (scores[i] < 1 || scores[i] > 30)
                    issues.Add($"WARNING: {names[i]} score {scores[i]} is outside 1–30 range.");
            }

            // Patrol AI checks
            if (Current.AIPreset == CreatureAIPreset.Patrol && Current.PatrolWaypoints.Count == 0)
                issues.Add("WARNING: AI preset is Patrol but no patrol waypoints are defined.");

            // Force-using creatures must be Jedi class
            bool isJedi = Current.ClassType >= 3 && Current.ClassType <= 5;
            if (Current.KnownPowers.Count > 0 && !isJedi)
                issues.Add("WARNING: Non-Jedi creature has Force powers — engine allows it but may be confusing.");

            if (Current.MaxFP > 0 && !isJedi)
                issues.Add("WARNING: MaxFP > 0 on a non-Jedi class creature.");

            return issues;
        }

        // ── UTC JSON EXPORT ───────────────────────────────────────────────────

        /// <summary>Export UTC GFF-stub JSON for CampaignPackager.</summary>
        public string ExportUtcJson() =>
            JsonUtility.ToJson(Current, prettyPrint: true);

        // ── LISTING ───────────────────────────────────────────────────────────

        public static IEnumerable<string> ListCreatureFiles(string folder)
        {
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + FileExtension, SearchOption.AllDirectories);
        }

        // ── PRESETS ───────────────────────────────────────────────────────────

        /// <summary>Pre-fill as a simple melee combatant.</summary>
        public void PresetMeleeCombatant(string resref, string name, int level = 3)
        {
            NewCreature(resref);
            Current.FirstName    = name;
            Current.ClassType    = 1;   // Soldier
            Current.Level        = level;
            Current.AIPreset     = CreatureAIPreset.Aggressive;
            Current.SightRange   = 12f;
            Current.Faction      = "Hostile";
            Current.Attributes.Strength  = 14;
            Current.Attributes.Dexterity = 10;
            Current.Attributes.Constitution = 12;
            RecalcDerivedStats();
        }

        /// <summary>Pre-fill as a Jedi companion.</summary>
        public void PresetJediCompanion(string resref, string name, int level = 5)
        {
            NewCreature(resref);
            Current.FirstName    = name;
            Current.ClassType    = 3;   // Jedi Guardian
            Current.Level        = level;
            Current.AIPreset     = CreatureAIPreset.Companion;
            Current.Faction      = "Player";
            Current.IgnoreParty  = false;
            Current.Attributes.Strength  = 14;
            Current.Attributes.Dexterity = 12;
            Current.Attributes.Wisdom    = 14;
            RecalcDerivedStats();
            AddFeat(0, "Power Attack");
            AddFeat(1, "Two-Weapon Fighting");
            AddForcePower(0);  // Force Push
            AddForcePower(3);  // Force Heal
        }

        /// <summary>Pre-fill as a ranged droids enemy.</summary>
        public void PresetDroidGunner(string resref, string name, int level = 4)
        {
            NewCreature(resref);
            Current.FirstName    = name;
            Current.ClassType    = 2;   // Scout
            Current.Level        = level;
            Current.AIPreset     = CreatureAIPreset.Aggressive;
            Current.RacialType   = 7;   // Droid
            Current.SightRange   = 20f;
            Current.Faction      = "Hostile";
            Current.Attributes.Dexterity    = 16;
            Current.Attributes.Constitution = 8;  // droids don't have CON
            RecalcDerivedStats();
            SetEquipment("WeaponR", "g_w_blstrrfl001");  // default blaster rifle
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private static string SanitiseName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));

        // ── EDITOR GUI STUB ───────────────────────────────────────────────────
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (Current == null) { UnityEditor.EditorGUILayout.HelpBox("No creature loaded.", UnityEditor.MessageType.Info); return; }
            UnityEditor.EditorGUILayout.LabelField($"ResRef: {Current.ResRef}  Tag: {Current.Tag}  Dirty: {IsDirty}", UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
