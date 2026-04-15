using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using KotORUnity.Combat;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER EDITOR TOOL  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full Force Power definition for authoring custom powers.
    /// Extends ForcePowerDef with new-port fields not available in vanilla spells.2da.
    ///
    /// New-port capabilities:
    ///   • Area-of-effect radius instead of single target only
    ///   • Channeled powers (continuous FP drain while key held)
    ///   • Charge-up time before release
    ///   • Custom VFX / SFX overrides per power
    ///   • Per-power alignment shift on use
    ///   • Power chains: casting this power unlocks a follow-up power window
    ///   • Upgraded tiers: Weak / Normal / Strong baked into one definition
    ///   • Custom C# handler class name (mod DLL can provide its own effect logic)
    /// </summary>
    [Serializable]
    public class ForcePowerDefinition
    {
        // ── IDENTITY ──────────────────────────────────────────────────────────
        public string ResRef          = "my_power_001";
        public string Label           = "Custom Power";
        public string Description     = "";
        public string IconResRef      = "isk_pow001";
        public string Comment         = "";

        // ── ALIGNMENT ─────────────────────────────────────────────────────────
        /// <summary>0=Universal 1=LightSide 2=DarkSide</summary>
        public int    Alignment       = 0;
        /// <summary>Alignment shift applied to caster on each use (-100 to +100; negative = Dark).</summary>
        public int    AlignmentShift  = 0;

        // ── COST ──────────────────────────────────────────────────────────────
        public int    ForceCost       = 20;
        /// <summary>FP drained per second while channeling (0 = not channeled).</summary>
        public float  ChannelFPPerSec = 0f;
        public float  CooldownSeconds = 3f;

        // ── DIFFICULTY CLASS ──────────────────────────────────────────────────
        public int    DC              = 14;
        /// <summary>Saving throw type: 0=None 1=Fort 2=Reflex 3=Will.</summary>
        public int    SaveType        = 3;
        /// <summary>Effect on successful save: 0=Negate 1=Half.</summary>
        public int    SaveEffect      = 0;

        // ── TARGETING ─────────────────────────────────────────────────────────
        public bool   IsRanged        = false;   // requires selected target
        public float  Range           = 20f;     // metres
        public float  AOERadius       = 0f;      // 0 = single-target
        public bool   AffectsFriendly = false;
        public bool   AffectsSelf     = false;

        // ── TIMING ────────────────────────────────────────────────────────────
        public float  ChargeUpTime    = 0f;      // seconds held before firing
        public float  Duration        = 0f;      // 0 = instant
        public bool   Interruptible   = true;    // can be cancelled by damage

        // ── DAMAGE ────────────────────────────────────────────────────────────
        public int    DamageNumDice   = 0;
        public int    DamageDie       = 6;
        /// <summary>Damage type: 0=None 1=Physical 2=Force 4=Stun 8=Shock 16=Cold 32=Fire.</summary>
        public int    DamageType      = 0;

        // ── STATUS EFFECT ─────────────────────────────────────────────────────
        /// <summary>0=None 1=Stun 2=Slow 3=Fear 4=Blind 5=Immobilise 6=Push 7=Pull 8=Lift.</summary>
        public int    StatusEffect    = 0;
        public float  StatusDuration  = 2f;

        // ── BUFF / DEBUFF ─────────────────────────────────────────────────────
        public int    AttackBonus     = 0;   // signed; applied to target during duration
        public int    ACBonus         = 0;
        public float  SpeedMultiplier = 1f;  // 1 = no change; 0.5 = half speed (Force Slow)
        public int    HPRestore       = 0;   // healing on hit (Force Heal)
        public int    FPRestore       = 0;   // FP restoration on use (Drain)

        // ── POWER CHAIN ───────────────────────────────────────────────────────
        /// <summary>ResRef of a follow-up power unlocked for N seconds after casting this one.</summary>
        public string ChainPowerResRef   = "";
        public float  ChainWindowSeconds = 0f;

        // ── UPGRADE TIERS ─────────────────────────────────────────────────────
        public bool HasUpgrades             = false;
        public string UpgradedFromResRef    = "";   // "" = base power
        public string Tier1ResRef           = "";   // Weak variant
        public string Tier2ResRef           = "";   // Normal variant
        public string Tier3ResRef           = "";   // Strong variant

        // ── VFX / SFX ─────────────────────────────────────────────────────────
        public string VFXCastResRef    = "";   // particle effect played at caster
        public string VFXImpactResRef  = "";   // particle effect at target / hit
        public string SFXCastResRef    = "";   // sound on cast
        public string SFXImpactResRef  = "";   // sound on impact

        // ── SCRIPTING ─────────────────────────────────────────────────────────
        public string ScriptName       = "";   // NWScript resref on impact
        public string CSharpHandler    = "";   // fully-qualified C# class name in mod DLL

        // ── PREREQUISITE ──────────────────────────────────────────────────────
        public int    MinForceLevel    = 1;
        public List<string> RequiredPowers = new List<string>();  // ResRef prerequisites

        // ── METADATA ──────────────────────────────────────────────────────────
        public List<string> MetaTags   = new List<string>();
        public string FilePath         = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FORCE POWER EDITOR SERVICE
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Service class backing the Force Power Editor window and the runtime
    /// custom-power registration system.
    /// </summary>
    public partial class ForcePowerEditorTool
    {
        // ── CONSTANTS ─────────────────────────────────────────────────────────
        public const string FileExtension  = ".fp_def.json";
        private const string DefaultFolder = "ModOutput/ForcePowers";

        // Alignment names for UI
        public static readonly string[] AlignmentNames =
            { "Universal", "Light Side", "Dark Side" };

        // Save type names for UI
        public static readonly string[] SaveTypeNames =
            { "None", "Fortitude", "Reflex", "Will" };

        // Status effect names for UI
        public static readonly string[] StatusEffectNames =
        {
            "None", "Stun", "Slow", "Fear", "Blind",
            "Immobilise", "Force Push", "Force Pull", "Force Lift"
        };

        // ── STATE ─────────────────────────────────────────────────────────────
        public ForcePowerDefinition Current { get; private set; } = new ForcePowerDefinition();
        public bool IsDirty { get; private set; } = false;
        public string LastError { get; private set; } = "";

        // ── CRUD ──────────────────────────────────────────────────────────────

        public void NewPower(string resref = "my_power_001")
        {
            Current = new ForcePowerDefinition { ResRef = resref };
            IsDirty = false; LastError = "";
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
                Debug.Log($"[ForcePowerEditorTool] Saved → {path}");
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; Debug.LogError($"[ForcePowerEditorTool] {ex.Message}"); return false; }
        }

        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) { LastError = $"Not found: {filePath}"; return false; }
                Current = JsonUtility.FromJson<ForcePowerDefinition>(File.ReadAllText(filePath, Encoding.UTF8));
                Current.FilePath = filePath;
                IsDirty = false; LastError = "";
                return true;
            }
            catch (Exception ex) { LastError = ex.Message; Debug.LogError($"[ForcePowerEditorTool] {ex.Message}"); return false; }
        }

        // ── RUNTIME REGISTRATION ──────────────────────────────────────────────

        /// <summary>
        /// Convert this definition to a ForcePowerDef and register it with the
        /// ForcePowerRegistry so the ForcePowerManager can use it in-game.
        /// </summary>
        public ForcePowerDef RegisterRuntime()
        {
            var def = new ForcePowerDef
            {
                SpellId         = Current.ResRef.GetHashCode() & 0x7FFF,
                Label           = Current.Label,
                ResRef          = Current.IconResRef,
                Alignment       = (ForcePowerAlignment)Current.Alignment,
                ForceCost       = Current.ForceCost,
                CooldownSeconds = Current.CooldownSeconds,
                DC              = Current.DC,
                IsRanged        = Current.IsRanged,
                ScriptName      = Current.ScriptName
            };
            ForcePowerRegistry.Register(def);
            Debug.Log($"[ForcePowerEditorTool] Registered runtime power '{def.Label}' (id={def.SpellId}).");
            return def;
        }

        // ── SPELLS.2DA EXPORT ─────────────────────────────────────────────────

        /// <summary>
        /// Generate a spells.2da append row for the CampaignPackager's 2DA patcher.
        /// Returns JSON suitable for TwoDA/spells_patch.json.
        /// </summary>
        public string ExportSpells2DAPatchRow()
        {
            string align = Current.Alignment switch
            {
                1 => "light",
                2 => "dark",
                _ => "neutral"
            };

            var row = new
            {
                operation   = "AppendRow",
                tableName   = "spells",
                rowData     = new Dictionary<string, string>
                {
                    { "label",        Current.Label         },
                    { "iconresref",   Current.IconResRef    },
                    { "forcealign",   align                  },
                    { "forcecost",    Current.ForceCost.ToString() },
                    { "cooldowntime", Current.CooldownSeconds.ToString("F2") },
                    { "dc",           Current.DC.ToString()  },
                    { "rangedflag",   Current.IsRanged ? "1" : "0" },
                    { "onimpact",     Current.ScriptName     },
                    { "range",        Current.Range.ToString("F1") },
                    { "goodevil",     Current.AlignmentShift.ToString() },
                    { "aoe_radius",   Current.AOERadius.ToString("F1") },  // new-port column
                    { "channel_fps",  Current.ChannelFPPerSec.ToString("F2") },
                    { "charge_time",  Current.ChargeUpTime.ToString("F2") },
                    { "cs_handler",   Current.CSharpHandler  },
                }
            };
            return JsonUtility.ToJson(row, prettyPrint: true);
        }

        // ── VALIDATION ────────────────────────────────────────────────────────

        public List<string> Validate()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Current.ResRef))
                issues.Add("ERROR: ResRef is empty.");

            if (string.IsNullOrWhiteSpace(Current.Label))
                issues.Add("ERROR: Label is empty — power will show blank name.");

            if (Current.ForceCost < 0)
                issues.Add("ERROR: ForceCost cannot be negative.");

            if (Current.CooldownSeconds < 0)
                issues.Add("ERROR: CooldownSeconds cannot be negative.");

            if (Current.AOERadius > 0 && Current.IsRanged)
                issues.Add("WARNING: AOE radius + IsRanged both set — AOE takes precedence in the engine.");

            if (Current.ChannelFPPerSec > 0 && Current.Duration <= 0)
                issues.Add("WARNING: ChannelFPPerSec set but Duration = 0 — channel will end instantly.");

            if (Current.ChargeUpTime > 0 && Current.Duration <= 0 && Current.DamageNumDice == 0)
                issues.Add("WARNING: Charge-up power has no damage dice and no duration — it will do nothing.");

            if (Current.AlignmentShift != 0 && Current.Alignment == 0)
                issues.Add("INFO: Universal power causes alignment shift — this is intentional (e.g. Force Drain).");

            if (Current.LightSideOnly() && Current.DarkSideOnly())
                issues.Add("ERROR: Power has contradicting alignment restrictions.");

            if (Current.HasUpgrades && string.IsNullOrEmpty(Current.Tier1ResRef))
                issues.Add("WARNING: HasUpgrades = true but Tier1ResRef is empty.");

            if (!string.IsNullOrEmpty(Current.ChainPowerResRef) && Current.ChainWindowSeconds <= 0)
                issues.Add("WARNING: ChainPowerResRef set but ChainWindowSeconds = 0 — window will close immediately.");

            return issues;
        }

        // ── LISTING ───────────────────────────────────────────────────────────

        public static IEnumerable<string> ListPowerFiles(string folder)
        {
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + FileExtension, SearchOption.AllDirectories);
        }

        // ── PRESETS ───────────────────────────────────────────────────────────

        /// <summary>Pre-fill as a damage burst power (Force Lightning variant).</summary>
        public void PresetDamageBurst(string resref, string label, int alignment = 2)
        {
            NewPower(resref);
            Current.Label          = label;
            Current.Alignment      = alignment;
            Current.AlignmentShift = alignment == 2 ? -5 : 0;
            Current.ForceCost      = 30;
            Current.CooldownSeconds = 4f;
            Current.DC             = 16;
            Current.SaveType       = 2;   // Reflex
            Current.SaveEffect     = 1;   // Half damage
            Current.IsRanged       = true;
            Current.Range          = 15f;
            Current.DamageNumDice  = 4;
            Current.DamageDie      = 6;
            Current.DamageType     = 8;   // Shock
            Current.VFXCastResRef  = "p_fxlightning01";
            Current.SFXCastResRef  = "force_lightning";
            IsDirty = true;
        }

        /// <summary>Pre-fill as a heal power (Force Heal variant).</summary>
        public void PresetHeal(string resref, string label)
        {
            NewPower(resref);
            Current.Label          = label;
            Current.Alignment      = 1;   // Light Side
            Current.AlignmentShift = 3;
            Current.ForceCost      = 25;
            Current.CooldownSeconds = 3f;
            Current.AffectsSelf    = true;
            Current.HPRestore      = 30;
            Current.VFXCastResRef  = "p_fxheal01";
            IsDirty = true;
        }

        /// <summary>Pre-fill as a channeled Force drain.</summary>
        public void PresetChannel(string resref, string label)
        {
            NewPower(resref);
            Current.Label           = label;
            Current.Alignment       = 2;   // Dark Side
            Current.AlignmentShift  = -3;
            Current.ForceCost       = 5;   // initial cost
            Current.ChannelFPPerSec = 5f;  // drain while held
            Current.Duration        = 10f;
            Current.CooldownSeconds = 1f;
            Current.IsRanged        = true;
            Current.Range           = 8f;
            Current.DamageNumDice   = 1;
            Current.DamageDie       = 4;
            Current.DamageType      = 2;   // Force
            Current.FPRestore       = 5;   // drains enemy FP and transfers
            IsDirty = true;
        }

        /// <summary>Pre-fill as an AOE crowd-control power.</summary>
        public void PresetAOEControl(string resref, string label, int status = 1 /* Stun */)
        {
            NewPower(resref);
            Current.Label           = label;
            Current.Alignment       = 0;   // Universal
            Current.ForceCost       = 35;
            Current.CooldownSeconds = 6f;
            Current.DC              = 15;
            Current.SaveType        = 3;   // Will
            Current.AOERadius       = 6f;
            Current.StatusEffect    = status;
            Current.StatusDuration  = 4f;
            Current.VFXImpactResRef = "p_fxstun01";
            IsDirty = true;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private static string SanitiseName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
    }

    // Extension methods for ForcePowerDefinition
    public static class ForcePowerDefinitionExt
    {
        public static bool LightSideOnly(this ForcePowerDefinition d) => d.Alignment == 1;
        public static bool DarkSideOnly(this ForcePowerDefinition d)  => d.Alignment == 2;
    }

    // ── EDITOR GUI STUB on ForcePowerEditorTool ───────────────────────────────
    public partial class ForcePowerEditorTool
    {
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (Current == null) { UnityEditor.EditorGUILayout.HelpBox("No power loaded.", UnityEditor.MessageType.Info); return; }
            UnityEditor.EditorGUILayout.LabelField($"ResRef: {Current.ResRef}  Label: {Current.Label}  Dirty: {IsDirty}", UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
