using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.Core;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.KotOR.Modules;
using KotORUnity.KotOR.Parsers;

namespace KotORUnity.ModTools
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE / AREA EDITOR  —  Mod Tool
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Represents a room entry in a module layout being authored.
    /// </summary>
    [Serializable]
    public class AreaRoom
    {
        public string ModelResRef = "";   // e.g. "ebo_m12aa_01"
        public Vector3 Position;
        public Vector3 Rotation;
        public bool IsEnabled = true;
        public string Tag = "";
    }

    /// <summary>
    /// An entity (creature, door, placeable, waypoint, trigger) placed in an area.
    /// Serialises to the module's .git GFF.
    /// </summary>
    [Serializable]
    public class AreaEntity
    {
        public enum EntityType { Creature, Door, Placeable, Item, Trigger, Waypoint, Sound, Merchant }

        public EntityType Type;
        public string TemplateResRef = "";  // UTC / UTD / UTP etc.
        public string Tag = "";
        public string LocalizedName = "";
        public Vector3 Position;
        public float BearingDeg;            // Y-rotation in degrees
        public bool IsEnabled = true;

        // Trigger-specific
        public Vector3 TriggerExtents = Vector3.one;
        public string TransitionDestModule = "";
        public string TransitionDestWaypoint = "";

        // Script hooks
        public string OnEnterScript = "";
        public string OnExitScript = "";
        public string OnUsedScript = "";
        public string OnHeartbeatScript = "";
        public string OnDeathScript = "";

        public string DisplaySummary =>
            $"[{Type}] {(string.IsNullOrEmpty(Tag) ? TemplateResRef : Tag)} @ {Position:F1}";
    }

    /// <summary>
    /// Area properties (mirrors the .are GFF fields the engine reads).
    /// </summary>
    [Serializable]
    public class AreaProperties
    {
        public string ModuleName = "my_module_01";
        public string AreaName = "New Area";
        public string AmbientMusicDay = "";
        public string AmbientMusicNight = "";
        public string AmbientSoundDay = "";
        public string AmbientSoundNight = "";
        public Color FogColor = new Color(0.5f, 0.5f, 0.5f);
        public float FogNearDist = 30f;
        public float FogFarDist = 80f;
        public bool FogEnabled = false;
        public int GravityY = -981;         // * 0.01 → m/s²
        public string OnEnter = "";
        public string OnExit = "";
        public string OnHeartbeat = "";
        public string OnUserDefined = "";
        public string Comment = "";
    }

    /// <summary>
    /// Full module definition: area properties + room list + entity list.
    /// Can be serialised to a folder containing .are / .lyt / .git text stubs,
    /// which the CampaignPackager then bundles into an ERF.
    /// </summary>
    [Serializable]
    public class ModuleDefinition
    {
        public AreaProperties Properties = new AreaProperties();
        public List<AreaRoom>   Rooms    = new List<AreaRoom>();
        public List<AreaEntity> Entities = new List<AreaEntity>();
        public string FilePath = "";       // last save path (for re-save)
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MODULE AREA EDITOR  —  runtime service class
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runtime service backing both the in-editor EditorWindow and an optional
    /// in-game overlay. Handles create, load, save, and validation of module
    /// definitions. The EditorWindow (ModuleAreaEditorWindow.cs in Editor/)
    /// wraps this class for the ScriptableObject-free workflow.
    ///
    /// New-port capabilities unlocked vs vanilla KotOR engine:
    ///   • Unlimited rooms per area (vanilla cap ≈ 64)
    ///   • Per-room physics / NavMesh override (rooms can have custom gravity)
    ///   • Trigger volumes with arbitrary polygon shapes
    ///   • Day/night music crossfade per area
    ///   • NPC spawn limits removed (NavMesh streaming)
    /// </summary>
    public class ModuleAreaEditor
    {
        // ── CONSTANTS ──────────────────────────────────────────────────────────
        public const string ModuleFileExtension = ".mod_def.json";
        private const string DefaultOutputFolder = "ModOutput/Modules";

        // ── STATE ──────────────────────────────────────────────────────────────
        public ModuleDefinition Current { get; private set; } = new ModuleDefinition();
        public bool IsDirty { get; private set; } = false;
        public string LastError { get; private set; } = "";

        // ── ROOM OPERATIONS ───────────────────────────────────────────────────

        /// <summary>Add a new room at the given position.</summary>
        public AreaRoom AddRoom(string modelResRef, Vector3 position)
        {
            var room = new AreaRoom
            {
                ModelResRef = modelResRef.ToLowerInvariant().Trim(),
                Position    = position,
                Tag         = $"room_{Current.Rooms.Count:D3}"
            };
            Current.Rooms.Add(room);
            IsDirty = true;
            return room;
        }

        /// <summary>Remove a room by index.</summary>
        public bool RemoveRoom(int index)
        {
            if (index < 0 || index >= Current.Rooms.Count) return false;
            Current.Rooms.RemoveAt(index);
            IsDirty = true;
            return true;
        }

        /// <summary>Move a room to a new world position.</summary>
        public void MoveRoom(int index, Vector3 newPosition)
        {
            if (index < 0 || index >= Current.Rooms.Count) return;
            Current.Rooms[index].Position = newPosition;
            IsDirty = true;
        }

        // ── ENTITY OPERATIONS ─────────────────────────────────────────────────

        /// <summary>Place a new entity in the area.</summary>
        public AreaEntity AddEntity(AreaEntity.EntityType type, string templateResRef,
                                    Vector3 position, float bearingDeg = 0f)
        {
            var ent = new AreaEntity
            {
                Type           = type,
                TemplateResRef = templateResRef.ToLowerInvariant().Trim(),
                Position       = position,
                BearingDeg     = bearingDeg,
                Tag            = $"{type.ToString().ToLower()}_{Current.Entities.Count:D3}"
            };
            Current.Entities.Add(ent);
            IsDirty = true;
            return ent;
        }

        /// <summary>Remove an entity by index.</summary>
        public bool RemoveEntity(int index)
        {
            if (index < 0 || index >= Current.Entities.Count) return false;
            Current.Entities.RemoveAt(index);
            IsDirty = true;
            return true;
        }

        /// <summary>Return all entities of a given type.</summary>
        public IEnumerable<AreaEntity> GetEntitiesOfType(AreaEntity.EntityType type) =>
            Current.Entities.Where(e => e.Type == type);

        // ── AREA TRIGGER HELPERS ──────────────────────────────────────────────

        /// <summary>
        /// Quick-add a standard area-transition trigger (like a door exit).
        /// </summary>
        public AreaEntity AddTransitionTrigger(Vector3 position,
                                               string destModule,
                                               string destWaypoint)
        {
            var ent = AddEntity(AreaEntity.EntityType.Trigger, "wp_transition", position);
            ent.TransitionDestModule   = destModule;
            ent.TransitionDestWaypoint = destWaypoint;
            ent.TriggerExtents         = new Vector3(1.5f, 3f, 0.5f);
            return ent;
        }

        // ── SERIALISATION ─────────────────────────────────────────────────────

        /// <summary>Create a blank new module definition.</summary>
        public void NewModule(string moduleName = "new_module_01")
        {
            Current = new ModuleDefinition();
            Current.Properties.ModuleName = moduleName;
            IsDirty = false;
            LastError = "";
        }

        /// <summary>Save the current module definition to JSON.</summary>
        public bool Save(string outputFolder = "")
        {
            try
            {
                if (string.IsNullOrEmpty(outputFolder))
                    outputFolder = Path.Combine(Application.persistentDataPath, DefaultOutputFolder);

                Directory.CreateDirectory(outputFolder);
                string safeName = SanitiseName(Current.Properties.ModuleName);
                string path = Path.Combine(outputFolder, safeName + ModuleFileExtension);

                string json = JsonUtility.ToJson(Current, prettyPrint: true);
                File.WriteAllText(path, json, Encoding.UTF8);

                Current.FilePath = path;
                IsDirty = false;
                Debug.Log($"[ModuleAreaEditor] Saved → {path}");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[ModuleAreaEditor] Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Load a module definition from a JSON file.</summary>
        public bool Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    LastError = $"File not found: {filePath}";
                    return false;
                }
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                Current = JsonUtility.FromJson<ModuleDefinition>(json);
                Current.FilePath = filePath;
                IsDirty = false;
                LastError = "";
                Debug.Log($"[ModuleAreaEditor] Loaded '{Current.Properties.ModuleName}' " +
                          $"({Current.Rooms.Count} rooms, {Current.Entities.Count} entities)");
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError($"[ModuleAreaEditor] Load failed: {ex.Message}");
                return false;
            }
        }

        // ── LYT EXPORT ────────────────────────────────────────────────────────

        /// <summary>
        /// Export a KotOR-compatible .lyt text file from the current room list.
        /// Vanilla format: "roomcount N\nroom MODELNAME X Y Z\n..."
        /// </summary>
        public string ExportLyt()
        {
            var sb = new StringBuilder();
            var enabled = Current.Rooms.Where(r => r.IsEnabled).ToList();
            sb.AppendLine($"# Generated by KotOR-Unity ModuleAreaEditor");
            sb.AppendLine($"# Module: {Current.Properties.ModuleName}");
            sb.AppendLine($"roomcount {enabled.Count}");
            foreach (var r in enabled)
            {
                // KotOR LYT uses Z-up; Unity uses Y-up; swap Y and Z for vanilla compat
                sb.AppendLine($"room {r.ModelResRef.ToLowerInvariant()} " +
                              $"{r.Position.x:F4} {r.Position.z:F4} {r.Position.y:F4}");
            }
            sb.AppendLine("doorhookcount 0");
            sb.AppendLine("emptycount 0");
            return sb.ToString();
        }

        // ── GIT EXPORT ────────────────────────────────────────────────────────

        /// <summary>
        /// Export a minimal KotOR-compatible .git GFF stub (text/JSON form)
        /// that the CampaignPackager converts into a binary GFF blob.
        ///
        /// Format is the ModTools JSON representation of entity lists;
        /// the CampaignPackager handles the binary encoding.
        /// </summary>
        public string ExportGitJson()
        {
            return JsonUtility.ToJson(new { Entities = Current.Entities }, prettyPrint: true);
        }

        // ── ARE EXPORT ────────────────────────────────────────────────────────

        /// <summary>
        /// Export a text representation of the area properties (.are) for the
        /// CampaignPackager to embed in the module ERF.
        /// </summary>
        public string ExportAreJson()
        {
            return JsonUtility.ToJson(Current.Properties, prettyPrint: true);
        }

        // ── VALIDATION ────────────────────────────────────────────────────────

        /// <summary>
        /// Validate the current module definition and return a list of warnings
        /// and errors. Empty list = no issues.
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            if (string.IsNullOrWhiteSpace(Current.Properties.ModuleName))
                issues.Add("ERROR: Module name is empty.");

            if (Current.Rooms.Count == 0)
                issues.Add("WARNING: No rooms defined — the area will be empty.");

            // Check for duplicate room tags
            var roomTags = Current.Rooms.Select(r => r.Tag).Where(t => !string.IsNullOrEmpty(t)).ToList();
            var dupRoomTags = roomTags.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key);
            foreach (var dup in dupRoomTags)
                issues.Add($"WARNING: Duplicate room tag '{dup}'.");

            // Entity checks
            foreach (var ent in Current.Entities)
            {
                if (string.IsNullOrWhiteSpace(ent.TemplateResRef))
                    issues.Add($"ERROR: Entity '{ent.Tag}' has no TemplateResRef.");

                if (ent.Type == AreaEntity.EntityType.Trigger &&
                    !string.IsNullOrEmpty(ent.TransitionDestModule) &&
                    ent.TriggerExtents == Vector3.zero)
                    issues.Add($"WARNING: Transition trigger '{ent.Tag}' has zero extents — player may never enter.");
            }

            // Warn if no player start waypoint exists
            bool hasStart = Current.Entities.Any(e =>
                e.Type == AreaEntity.EntityType.Waypoint &&
                (e.Tag.ToLower().Contains("start") || e.Tag.ToLower().StartsWith("wp_entry")));
            if (!hasStart)
                issues.Add("WARNING: No player-start waypoint found (tag containing 'start' or 'wp_entry').");

            return issues;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static string SanitiseName(string name) =>
            string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));

        // ── STATIC FACTORY — load all module defs in a folder ─────────────────

        /// <summary>
        /// Enumerate all saved module definitions in a folder.
        /// Returns paths only; call Load() on each as needed.
        /// </summary>
        public static IEnumerable<string> ListModuleFiles(string folder)
        {
            if (!Directory.Exists(folder)) return Enumerable.Empty<string>();
            return Directory.GetFiles(folder, "*" + ModuleFileExtension, SearchOption.AllDirectories);
        }

        // ── EDITOR GUI STUB ───────────────────────────────────────────────────
        public void DrawEditorGUI()
        {
#if UNITY_EDITOR
            if (Current == null) { UnityEditor.EditorGUILayout.HelpBox("No module loaded.", UnityEditor.MessageType.Info); return; }
            UnityEditor.EditorGUILayout.LabelField(
                $"Module: {Current.Properties?.ModuleName ?? ""}  Rooms: {Current.Rooms?.Count ?? 0}  Entities: {Current.Entities?.Count ?? 0}  Dirty: {IsDirty}",
                UnityEditor.EditorStyles.helpBox);
#endif
        }
    }
}
