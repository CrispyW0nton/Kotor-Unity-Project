using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.KotOR.Parsers;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.KotOR.Modules
{
    /// <summary>
    /// Data model for a loaded KotOR module layout (.lyt file).
    /// Contains room definitions for visual placement.
    /// </summary>
    public class ModuleLayout
    {
        public string ModuleName;
        public List<RoomDefinition> Rooms = new List<RoomDefinition>();
        public List<string> DoorHooks = new List<string>();
        public List<string> EmptyRooms = new List<string>();
    }

    public class RoomDefinition
    {
        public string ModelName;
        public Vector3 Position;
        public bool IsEmpty;
    }

    /// <summary>
    /// Module entity instance (creature, placeable, door, waypoint, trigger, item).
    /// Loaded from the module's .git (GFF) file.
    /// </summary>
    public class ModuleEntityInstance
    {
        public enum EntityType { Creature, Door, Placeable, Item, Trigger, Waypoint, Sound }

        public EntityType Type;
        public string TemplateResRef;   // UTC/UTD/UTP file reference
        public Vector3 Position;
        public float Bearing;           // Y-rotation in radians
        public string Tag;
        public string LocalizedName;
        public float HP;
    }

    /// <summary>
    /// Loads a complete KotOR module into the Unity scene.
    /// 
    /// Loading sequence:
    ///   1. Find and open .rim and _s.rim files for the module
    ///   2. Load module info (.ifo GFF)
    ///   3. Parse layout (.lyt) — room models and positions
    ///   4. Load area (.are GFF) — properties, ambient sounds, lighting
    ///   5. Load instances (.git GFF) — all entities in the module
    ///   6. Spawn entities via EntitySpawner
    ///   7. Play ambient music
    /// </summary>
    public class ModuleLoader : MonoBehaviour
    {
        // ── CONFIG ─────────────────────────────────────────────────────────────
        private string _kotorDir;
        private TargetGame _targetGame;

        [Header("Spawn Settings")]
        [SerializeField] private Transform moduleRoot;
        [SerializeField] private bool spawnCreatures = true;
        [SerializeField] private bool spawnDoors = true;
        [SerializeField] private bool spawnPlaceables = true;
        [SerializeField] private bool spawnWaypoints = false;
        [SerializeField] private bool playAmbientMusic = true;

        // ── STATE ──────────────────────────────────────────────────────────────
        private string _currentModule;
        private ModuleLayout _currentLayout;
        private List<ModuleEntityInstance> _currentEntities;
        private AudioSource _ambientMusicSource;

        // ── INITIALIZATION ─────────────────────────────────────────────────────
        public void Initialize(string kotorDir, TargetGame targetGame)
        {
            _kotorDir = kotorDir;
            _targetGame = targetGame;

            if (moduleRoot == null)
                moduleRoot = new GameObject("ModuleRoot").transform;

            _ambientMusicSource = gameObject.AddComponent<AudioSource>();
            _ambientMusicSource.loop = true;
            _ambientMusicSource.spatialBlend = 0f; // 2D ambient
        }

        // ── MODULE LOADING ─────────────────────────────────────────────────────
        /// <summary>
        /// Load a module by name. Clears the current module first.
        /// </summary>
        public void LoadModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                Debug.LogError("[ModuleLoader] Empty module name.");
                return;
            }

            ClearCurrentModule();
            _currentModule = moduleName;

            try
            {
                Debug.Log($"[ModuleLoader] Loading module: {moduleName}");

                // 1. Open RIM archives
                string rimPath = GetModulePath(moduleName + ".rim");
                string rimSPath = GetModulePath(moduleName + "_s.rim");

                var rimEntries = RimReader.ReadRimArchive(rimPath);
                var rimSEntries = File.Exists(rimSPath)
                    ? RimReader.ReadRimArchive(rimSPath)
                    : new List<RimReader.RimResourceEntry>();

                // 2. Load module info (IFO)
                var ifoEntry = RimReader.FindResource(rimEntries, "module", ResourceType.IFO)
                    ?? RimReader.FindResource(rimSEntries, "module", ResourceType.IFO);

                if (ifoEntry != null)
                {
                    byte[] ifoData = RimReader.ReadResource(ifoEntry);
                    var ifoGff = GffReader.Parse(ifoData);
                    ProcessModuleInfo(ifoGff);
                }

                // 3. Load layout (LYT)
                string moduleLower = moduleName.ToLower();
                var lytEntry = RimReader.FindResource(rimEntries, moduleLower, ResourceType.LYT);
                if (lytEntry != null)
                {
                    byte[] lytData = RimReader.ReadResource(lytEntry);
                    _currentLayout = ParseLayout(lytData, moduleName);
                    SpawnRooms(_currentLayout);
                }

                // 4. Load area (ARE)
                var areEntry = RimReader.FindResource(rimEntries, moduleLower, ResourceType.ARE);
                if (areEntry != null)
                {
                    byte[] areData = RimReader.ReadResource(areEntry);
                    var areGff = GffReader.Parse(areData);
                    ProcessArea(areGff);
                }

                // 5. Load instances (GIT)
                var gitEntry = RimReader.FindResource(rimEntries, moduleLower, (ushort)2023)
                    ?? RimReader.FindResource(rimSEntries, moduleLower, (ushort)2023);
                if (gitEntry != null)
                {
                    byte[] gitData = RimReader.ReadResource(gitEntry);
                    var gitGff = GffReader.Parse(gitData);
                    _currentEntities = ParseEntityInstances(gitGff);
                    SpawnEntities(_currentEntities);
                }

                // Notify success
                EventBus.Publish(EventBus.EventType.ModuleLoaded,
                    new EventBus.ModuleEventArgs(moduleName));

                Debug.Log($"[ModuleLoader] Module '{moduleName}' loaded successfully.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ModuleLoader] Failed to load module '{moduleName}': {e.Message}\n{e.StackTrace}");
            }
        }

        // ── MODULE INFO PROCESSING ─────────────────────────────────────────────
        private void ProcessModuleInfo(GffReader.GffStruct ifo)
        {
            if (ifo == null) return;
            string modName = GffReader.GetString(ifo, "Mod_Name");
            Debug.Log($"[ModuleLoader] Module Name: {modName}");
        }

        // ── LAYOUT PARSING ─────────────────────────────────────────────────────
        private ModuleLayout ParseLayout(byte[] lytData, string moduleName)
        {
            var layout = new ModuleLayout { ModuleName = moduleName };
            string text = Encoding.ASCII.GetString(lytData);
            string[] lines = text.Split('\n');

            bool inRooms = false;
            int roomCount = 0;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("roomcount", StringComparison.OrdinalIgnoreCase))
                {
                    inRooms = true;
                    int.TryParse(line.Split(' ')[1], out roomCount);
                    continue;
                }
                if (inRooms && roomCount > 0)
                {
                    string[] parts = line.Split(new char[]{' ', '\t'}, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float x);
                        float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float y);
                        float.TryParse(parts[3], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float z);

                        layout.Rooms.Add(new RoomDefinition
                        {
                            ModelName = parts[0],
                            Position = new Vector3(x, z, y), // KotOR uses Y-up, Unity too but axes differ
                            IsEmpty = parts[0].StartsWith("empty")
                        });
                        roomCount--;
                    }
                }
            }

            Debug.Log($"[ModuleLoader] Parsed {layout.Rooms.Count} rooms from layout.");
            return layout;
        }

        // ── AREA PROCESSING ────────────────────────────────────────────────────
        private void ProcessArea(GffReader.GffStruct are)
        {
            if (are == null) return;

            // Ambient music
            if (playAmbientMusic)
            {
                int ambientMusicDay = GffReader.GetInt(are, "MusicDay", -1);
                int ambientMusicNight = GffReader.GetInt(are, "MusicNight", -1);

                if (ambientMusicDay >= 0)
                    Debug.Log($"[ModuleLoader] Ambient music track (day): {ambientMusicDay}");
            }

            // Area properties could include lighting, fog, etc.
            string ambientSound = GffReader.GetString(are, "AmbientSndDay");
            if (!string.IsNullOrEmpty(ambientSound))
                Debug.Log($"[ModuleLoader] Ambient sound: {ambientSound}");
        }

        // ── ENTITY INSTANCE PARSING ────────────────────────────────────────────
        private List<ModuleEntityInstance> ParseEntityInstances(GffReader.GffStruct git)
        {
            var instances = new List<ModuleEntityInstance>();
            if (git == null) return instances;

            // Creatures
            ParseEntityList(git, "Creature List", ModuleEntityInstance.EntityType.Creature, instances);
            // Doors
            ParseEntityList(git, "Door List", ModuleEntityInstance.EntityType.Door, instances);
            // Placeables
            ParseEntityList(git, "Placeable List", ModuleEntityInstance.EntityType.Placeable, instances);
            // Items
            ParseEntityList(git, "Item List", ModuleEntityInstance.EntityType.Item, instances);
            // Waypoints
            ParseEntityList(git, "WaypointList", ModuleEntityInstance.EntityType.Waypoint, instances);

            Debug.Log($"[ModuleLoader] Parsed {instances.Count} entity instances.");
            return instances;
        }

        private void ParseEntityList(
            GffReader.GffStruct git,
            string listKey,
            ModuleEntityInstance.EntityType type,
            List<ModuleEntityInstance> instances)
        {
            if (!git.Fields.ContainsKey(listKey)) return;
            var list = git.Fields[listKey].AsList();
            if (list == null) return;

            foreach (var item in list)
            {
                var instance = new ModuleEntityInstance
                {
                    Type = type,
                    TemplateResRef = GffReader.GetString(item, "TemplateResRef"),
                    Tag = GffReader.GetString(item, "Tag"),
                    Position = GffReader.GetVector(item, "XPosition") != Vector3.zero
                        ? GffReader.GetVector(item, "XPosition")
                        : new Vector3(
                            GffReader.GetFloat(item, "XPosition"),
                            GffReader.GetFloat(item, "ZPosition"),
                            GffReader.GetFloat(item, "YPosition")),
                    Bearing = GffReader.GetFloat(item, "Bearing")
                };
                instances.Add(instance);
            }
        }

        // ── ROOM SPAWNING ──────────────────────────────────────────────────────
        private void SpawnRooms(ModuleLayout layout)
        {
            foreach (var room in layout.Rooms)
            {
                if (room.IsEmpty) continue;

                // In full implementation: load MDL/MDX model via MdlReader
                // For now: create placeholder GameObjects at correct positions
                var roomObj = new GameObject($"Room_{room.ModelName}");
                roomObj.transform.SetParent(moduleRoot);
                roomObj.transform.position = room.Position;

                // Mark for model loading
                roomObj.AddComponent<RoomModelPlaceholder>().SetModelName(room.ModelName);
            }
        }

        // ── ENTITY SPAWNING ────────────────────────────────────────────────────
        private void SpawnEntities(List<ModuleEntityInstance> entities)
        {
            foreach (var entity in entities)
            {
                switch (entity.Type)
                {
                    case ModuleEntityInstance.EntityType.Creature when spawnCreatures:
                        SpawnCreature(entity);
                        break;
                    case ModuleEntityInstance.EntityType.Door when spawnDoors:
                        SpawnDoor(entity);
                        break;
                    case ModuleEntityInstance.EntityType.Placeable when spawnPlaceables:
                        SpawnPlaceable(entity);
                        break;
                }
            }
        }

        private void SpawnCreature(ModuleEntityInstance entity)
        {
            var go = new GameObject($"Creature_{entity.Tag}_{entity.TemplateResRef}");
            go.transform.SetParent(moduleRoot);
            go.transform.position = entity.Position;
            go.transform.rotation = Quaternion.Euler(0f, entity.Bearing * Mathf.Rad2Deg, 0f);
            go.tag = "Enemy"; // Default — UTC parsing would set friendly/hostile
            Debug.Log($"[ModuleLoader] Spawned creature: {entity.TemplateResRef} at {entity.Position}");
        }

        private void SpawnDoor(ModuleEntityInstance entity)
        {
            var go = new GameObject($"Door_{entity.Tag}");
            go.transform.SetParent(moduleRoot);
            go.transform.position = entity.Position;
            go.transform.rotation = Quaternion.Euler(0f, entity.Bearing * Mathf.Rad2Deg, 0f);
        }

        private void SpawnPlaceable(ModuleEntityInstance entity)
        {
            var go = new GameObject($"Placeable_{entity.Tag}");
            go.transform.SetParent(moduleRoot);
            go.transform.position = entity.Position;
            go.transform.rotation = Quaternion.Euler(0f, entity.Bearing * Mathf.Rad2Deg, 0f);
        }

        // ── CLEANUP ────────────────────────────────────────────────────────────
        private void ClearCurrentModule()
        {
            if (moduleRoot != null)
                foreach (Transform child in moduleRoot)
                    Destroy(child.gameObject);

            _currentLayout = null;
            _currentEntities = null;

            if (_ambientMusicSource != null)
                _ambientMusicSource.Stop();
        }

        // ── PATH HELPERS ───────────────────────────────────────────────────────
        private string GetModulePath(string fileName)
        {
            return Path.Combine(_kotorDir, "Modules", fileName);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public string CurrentModule => _currentModule;
        public ModuleLayout CurrentLayout => _currentLayout;
    }

    /// <summary>
    /// Placeholder component on room GameObjects.
    /// In full implementation, triggers async MDL/MDX model loading.
    /// </summary>
    public class RoomModelPlaceholder : MonoBehaviour
    {
        private string _modelName;
        public void SetModelName(string name) => _modelName = name;
        public string ModelName => _modelName;
    }
}
