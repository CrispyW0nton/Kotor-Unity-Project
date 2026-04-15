using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.Parsers;
using KotORUnity.KotOR.FileReaders;
using GffStruct    = KotORUnity.KotOR.Parsers.GffReader.GffStruct;
using GffFieldType = KotORUnity.KotOR.Parsers.GffReader.GffFieldType;

namespace KotORUnity.World
{
    /// <summary>
    /// Reads a module's .git (Game Instance File) and spawns all creatures,
    /// placeables, doors, waypoints and triggers as Unity GameObjects.
    ///
    /// The .git is a GFF whose top-level struct contains lists:
    ///   Creature List  — UTC template refs + position/orientation overrides
    ///   Door List      — UTD template refs
    ///   Placeable List — UTP template refs
    ///   Waypoint List  — UTW template refs
    ///   Trigger List   — UTT template refs
    ///   Sound List     — UTS template refs
    ///
    /// Call <see cref="SpawnArea"/> once a module is loaded.
    /// </summary>
    public class CreatureSpawner : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Prefabs")]
        [Tooltip("Generic creature prefab — needs CreatureController, NavMeshAgent, Animator.")]
        [SerializeField] private GameObject creaturePrefab;

        [Tooltip("Generic door prefab — needs DoorController.")]
        [SerializeField] private GameObject doorPrefab;

        [Tooltip("Generic placeable prefab.")]
        [SerializeField] private GameObject placeablePrefab;

        [Tooltip("Waypoint marker prefab (invisible sphere is fine).")]
        [SerializeField] private GameObject waypointPrefab;

        [Header("Container transforms (leave null to auto-create)")]
        [SerializeField] private Transform creatureContainer;
        [SerializeField] private Transform doorContainer;
        [SerializeField] private Transform placeableContainer;
        [SerializeField] private Transform waypointContainer;

        // ── RUNTIME DATA ──────────────────────────────────────────────────────
        private readonly List<CreatureInstance>   _creatures   = new List<CreatureInstance>();
        private readonly List<DoorInstance>       _doors       = new List<DoorInstance>();
        private readonly List<PlaceableInstance>  _placeables  = new List<PlaceableInstance>();
        private readonly List<WaypointInstance>   _waypoints   = new List<WaypointInstance>();

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            creatureContainer  = EnsureContainer(creatureContainer,  "Creatures");
            doorContainer      = EnsureContainer(doorContainer,      "Doors");
            placeableContainer = EnsureContainer(placeableContainer, "Placeables");
            waypointContainer  = EnsureContainer(waypointContainer,  "Waypoints");
        }

        // ── PUBLIC STATIC API (for DevConsole / NWScriptVM) ───────────────────

        /// <summary>
        /// Spawn a creature at a world position from a UTC resref.
        /// Used by the dev console 'spawn' command and NWScriptVM.SpawnCreatureAtLocation.
        /// </summary>
        public static void SpawnCreature(string utcResRef, Vector3 position, Quaternion rotation)
        {
            var spawner = FindObjectOfType<CreatureSpawner>();
            if (spawner == null)
            {
                // Fallback: create a simple sphere placeholder
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.name = utcResRef;
                go.tag  = "Creature";
                Debug.LogWarning($"[CreatureSpawner] No spawner in scene — placed placeholder for '{utcResRef}'.");
                return;
            }
            spawner.SpawnCreatureInstance(utcResRef, position, rotation);
        }

        /// <summary>Instance method for spawning a single creature by UTC resref.</summary>
        public void SpawnCreatureInstance(string utcResRef, Vector3 position, Quaternion rotation)
        {
            if (creaturePrefab == null)
            {
                Debug.LogWarning($"[CreatureSpawner] creaturePrefab not set — cannot spawn '{utcResRef}'.");
                return;
            }
            var go = Instantiate(creaturePrefab, position, rotation, creatureContainer);
            go.name = utcResRef;
            var data = go.GetComponent<KotorCreatureData>();
            if (data != null) data.Initialize(utcResRef, utcResRef.ToUpperInvariant(), utcResRef, 0, null);
            Debug.Log($"[CreatureSpawner] Spawned '{utcResRef}' at {position:F1}.");
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>
        /// Parse the .git for <paramref name="moduleName"/> and spawn everything.
        /// Must be called after SceneBootstrapper is ready.
        /// </summary>
        public void SpawnArea(string moduleName)
        {
            if (!SceneBootstrapper.IsReady)
            {
                Debug.LogError("[CreatureSpawner] SceneBootstrapper not ready.");
                return;
            }

            // Mount the module archives so the GIT resource is available
            SceneBootstrapper.Resources.MountModule(moduleName);

            byte[] gitData = SceneBootstrapper.Resources.GetResource(moduleName, ResourceType.GIT);
            if (gitData == null)
            {
                Debug.LogWarning($"[CreatureSpawner] .git not found for module '{moduleName}'.");
                return;
            }

            var git = GffReader.Parse(gitData);
            if (git == null)
            {
                Debug.LogError("[CreatureSpawner] Failed to parse .git GFF.");
                return;
            }

            DespawnAll();

            SpawnCreatures(git);
            SpawnDoors(git);
            SpawnPlaceables(git);
            SpawnWaypoints(git);

            Debug.Log($"[CreatureSpawner] Spawned area '{moduleName}': " +
                      $"{_creatures.Count} creatures, {_doors.Count} doors, " +
                      $"{_placeables.Count} placeables, {_waypoints.Count} waypoints.");
        }

        /// <summary>Destroy all spawned objects.</summary>
        public void DespawnAll()
        {
            foreach (var c in _creatures)   if (c.GameObject) Destroy(c.GameObject);
            foreach (var d in _doors)       if (d.GameObject) Destroy(d.GameObject);
            foreach (var p in _placeables)  if (p.GameObject) Destroy(p.GameObject);
            foreach (var w in _waypoints)   if (w.GameObject) Destroy(w.GameObject);

            _creatures.Clear();
            _doors.Clear();
            _placeables.Clear();
            _waypoints.Clear();
        }

        // ── SPAWN CREATURES ───────────────────────────────────────────────────
        private void SpawnCreatures(GffStruct git)
        {
            var list = git.GetField("Creature List")?.AsList();
            if (list == null) return;

            foreach (var entry in list)
            {
                string templateRef = GffReader.GetString(entry, "TemplateResRef");
                float  x = GffReader.GetFloat(entry, "XPosition");
                float  y = GffReader.GetFloat(entry, "YPosition");
                float  z = GffReader.GetFloat(entry, "ZPosition");
                float  bearing = GffReader.GetFloat(entry, "XOrientation");
                float  bearingY = GffReader.GetFloat(entry, "YOrientation");

                // Load UTC template for display name, tag, etc.
                var utcData = SceneBootstrapper.Resources.GetResource(templateRef, ResourceType.UTC);
                var utc     = utcData != null ? GffReader.Parse(utcData) : null;

                string displayName = utc != null ? ResolveLocString(utc, "FirstName") : templateRef;
                string tag         = GffReader.GetString(utc, "Tag", templateRef);
                int    appearance  = GffReader.GetInt(utc, "Appearance_Type", 0);
                bool   isHostile   = GffReader.GetInt(utc, "FactionID", 0) != 2; // 2=friendly

                // Position: KotOR→Unity coord system
                var pos = new Vector3(x, z, y);
                var rot = Quaternion.Euler(0, Mathf.Atan2(bearing, bearingY) * Mathf.Rad2Deg, 0);

                GameObject go = creaturePrefab != null
                    ? Instantiate(creaturePrefab, pos, rot, creatureContainer)
                    : new GameObject(displayName);

                go.name = $"{displayName} [{tag}]";
                go.transform.SetParent(creatureContainer, false);
                go.transform.position = pos;
                go.transform.rotation = rot;

                // Attach runtime data component
                var ci = go.AddComponent<KotorCreatureData>();
                ci.Initialize(templateRef, tag, displayName, appearance, utc);

                _creatures.Add(new CreatureInstance
                {
                    TemplateRef = templateRef,
                    Tag         = tag,
                    GameObject  = go
                });
            }
        }

        // ── SPAWN DOORS ───────────────────────────────────────────────────────
        private void SpawnDoors(GffStruct git)
        {
            var list = git.GetField("Door List")?.AsList();
            if (list == null) return;

            foreach (var entry in list)
            {
                string templateRef = GffReader.GetString(entry, "TemplateResRef");
                float  x = GffReader.GetFloat(entry, "X");
                float  y = GffReader.GetFloat(entry, "Y");
                float  z = GffReader.GetFloat(entry, "Z");
                float  bearing = GffReader.GetFloat(entry, "Bearing");

                // Load UTD for linked area target
                var utdData    = SceneBootstrapper.Resources.GetResource(templateRef, ResourceType.UTD);
                var utd        = utdData != null ? GffReader.Parse(utdData) : null;
                string tag     = GffReader.GetString(utd, "Tag", templateRef);
                string linkedTo = GffReader.GetString(entry, "LinkedTo");
                bool   locked  = GffReader.GetInt(utd, "Locked", 0) != 0;

                var pos = new Vector3(x, z, y);
                var rot = Quaternion.Euler(0, bearing * Mathf.Rad2Deg, 0);

                GameObject go = doorPrefab != null
                    ? Instantiate(doorPrefab, pos, rot, doorContainer)
                    : new GameObject(tag);

                go.name = $"Door [{tag}]";
                go.transform.position = pos;
                go.transform.rotation = rot;

                var dc = go.GetComponent<DoorController>() ?? go.AddComponent<DoorController>();
                dc.Initialize(tag, linkedTo, locked, utd);

                _doors.Add(new DoorInstance { Tag = tag, LinkedTo = linkedTo, GameObject = go });
            }
        }

        // ── SPAWN PLACEABLES ──────────────────────────────────────────────────
        private void SpawnPlaceables(GffStruct git)
        {
            var list = git.GetField("Placeable List")?.AsList();
            if (list == null) return;

            foreach (var entry in list)
            {
                string templateRef = GffReader.GetString(entry, "TemplateResRef");
                float  x = GffReader.GetFloat(entry, "X");
                float  y = GffReader.GetFloat(entry, "Y");
                float  z = GffReader.GetFloat(entry, "Z");
                float  bearing = GffReader.GetFloat(entry, "Bearing");

                var utpData = SceneBootstrapper.Resources.GetResource(templateRef, ResourceType.UTP);
                var utp     = utpData != null ? GffReader.Parse(utpData) : null;
                string tag  = GffReader.GetString(utp, "Tag", templateRef);

                var pos = new Vector3(x, z, y);
                var rot = Quaternion.Euler(0, bearing * Mathf.Rad2Deg, 0);

                GameObject go = placeablePrefab != null
                    ? Instantiate(placeablePrefab, pos, rot, placeableContainer)
                    : new GameObject(tag);

                go.name = $"Placeable [{tag}]";
                go.transform.position = pos;
                go.transform.rotation = rot;

                var pc = go.GetComponent<PlaceableController>() ?? go.AddComponent<PlaceableController>();
                pc.Initialize(tag, utp);

                _placeables.Add(new PlaceableInstance { Tag = tag, GameObject = go });
            }
        }

        // ── SPAWN WAYPOINTS ───────────────────────────────────────────────────
        private void SpawnWaypoints(GffStruct git)
        {
            var list = git.GetField("WaypointList")?.AsList();
            if (list == null) return;

            foreach (var entry in list)
            {
                float  x  = GffReader.GetFloat(entry, "XPosition");
                float  y  = GffReader.GetFloat(entry, "YPosition");
                float  z  = GffReader.GetFloat(entry, "ZPosition");
                string tag = GffReader.GetString(entry, "Tag");
                uint nameRef = (uint)GffReader.GetInt(entry, "LocalizedName", 0);
                string name  = SceneBootstrapper.GetString(nameRef);

                var pos = new Vector3(x, z, y);

                GameObject go = waypointPrefab != null
                    ? Instantiate(waypointPrefab, pos, Quaternion.identity, waypointContainer)
                    : new GameObject($"WP [{tag}]");

                go.name = $"Waypoint [{tag}]";
                go.transform.position = pos;

                var wc = go.AddComponent<WaypointMarker>();
                wc.Tag          = tag;
                wc.DisplayName  = name;

                _waypoints.Add(new WaypointInstance { Tag = tag, GameObject = go });
            }
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private static string ResolveLocString(GffStruct s, string field)
        {
            if (s == null) return "";
            var f = s.GetField(field);
            if (f == null) return "";
            // LocString field — try direct string, else try as struct with StrRef
            if (f.FieldType == GffFieldType.CExoLocString)
            {
                var ls = f.AsStruct();
                if (ls != null)
                {
                    uint strref = (uint)GffReader.GetInt(ls, "StringRef", -1);
                    if (strref != uint.MaxValue && SceneBootstrapper.Strings != null)
                        return SceneBootstrapper.Strings.GetString(strref);
                    // Inline text (lang 0 = English)
                    return GffReader.GetString(ls, "0", "");
                }
            }
            return f.AsString() ?? "";
        }

        private Transform EnsureContainer(Transform existing, string containerName)
        {
            if (existing != null) return existing;
            var go = new GameObject(containerName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        // ── INSTANCE DATA CLASSES ─────────────────────────────────────────────
        public class CreatureInstance
        {
            public string     TemplateRef;
            public string     Tag;
            public GameObject GameObject;
        }

        public class DoorInstance
        {
            public string     Tag;
            public string     LinkedTo;
            public GameObject GameObject;
        }

        public class PlaceableInstance
        {
            public string     Tag;
            public GameObject GameObject;
        }

        public class WaypointInstance
        {
            public string     Tag;
            public GameObject GameObject;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HELPER MONOBEHAVIOURS  (lightweight data holders on spawned objects)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Runtime data attached to spawned creatures.</summary>
    public class KotorCreatureData : MonoBehaviour
    {
        public string     TemplateRef    { get; private set; }
        public string     Tag            { get; private set; }
        public string     DisplayName    { get; private set; }
        public int        AppearanceRow  { get; private set; }
        public GffStruct  UTC            { get; private set; }

        // Derived stats (populated from UTC)
        public int MaxHP   { get; private set; }
        public int CurrentHP { get; set; }
        public int FactionId { get; private set; }   // matches FactionManager primary IDs (0-6)
        public bool IsAlive  => CurrentHP > 0;

        public void Initialize(string tRef, string tag, string name, int appearance, GffStruct utc)
        {
            TemplateRef   = tRef;
            Tag           = tag;
            DisplayName   = name;
            AppearanceRow = appearance;
            UTC           = utc;

            // HP from UTC
            MaxHP     = GffReader.GetInt(utc, "MaxHitPoints", 10);
            CurrentHP = GffReader.GetInt(utc, "CurrentHitPoints", MaxHP);
            FactionId = GffReader.GetInt(utc, "FactionID", 1);  // 1 = Hostile by default
        }
    }

    /// <summary>Runtime data and logic for doors.</summary>
    public class DoorController : MonoBehaviour
    {
        public string     Tag        { get; private set; }
        public string     LinkedTo   { get; private set; }  // area tag of the destination
        public bool       IsLocked   { get; private set; }
        public bool       IsOpen     { get; private set; }
        public GffStruct  UTD        { get; private set; }

        private string _onOpenScript;
        private string _onFailedScript;

        public void Initialize(string tag, string linkedTo, bool locked, GffStruct utd)
        {
            Tag      = tag;
            LinkedTo = linkedTo;
            IsLocked = locked;
            UTD      = utd;
            _onOpenScript   = GffReader.GetString(utd, "OnOpen");
            _onFailedScript = GffReader.GetString(utd, "OnFailToOpen");
        }

        /// <summary>Attempt to open the door (called by player interaction).</summary>
        public bool TryOpen()
        {
            if (IsLocked)
            {
                Debug.Log($"[Door:{Tag}] Locked.");
                if (!string.IsNullOrEmpty(_onFailedScript))
                    KotORUnity.Scripting.NWScriptVM.Run(_onFailedScript, gameObject);
                return false;
            }
            IsOpen = true;
            // Track door open in WorldStateTracker for save system
            if (!string.IsNullOrEmpty(Tag))
                KotORUnity.SaveSystem.WorldStateTracker.MarkDoorOpened(Tag);
            if (!string.IsNullOrEmpty(_onOpenScript))
                KotORUnity.Scripting.NWScriptVM.Run(_onOpenScript, gameObject);
            // Trigger area transition if this door leads somewhere
            if (!string.IsNullOrEmpty(LinkedTo))
                Core.EventBus.Publish(Core.EventBus.EventType.AreaTransitionRequested,
                    new AreaTransitionEventArgs(LinkedTo, Tag));
            return true;
        }

        public void SetLocked(bool locked) => IsLocked = locked;
    }

    /// <summary>Runtime data for placeables (containers, computers, switches).</summary>
    public class PlaceableController : MonoBehaviour
    {
        public string    Tag  { get; private set; }
        public GffStruct UTP  { get; private set; }

        private bool _hasInventory;
        private string _onUsedScript;

        public void Initialize(string tag, GffStruct utp)
        {
            Tag         = tag;
            UTP         = utp;
            _hasInventory = GffReader.GetInt(utp, "HasInventory", 0) != 0;
            _onUsedScript = GffReader.GetString(utp, "OnUsed");
        }

        public void OnUsed(GameObject user)
        {
            if (!string.IsNullOrEmpty(_onUsedScript))
                KotORUnity.Scripting.NWScriptVM.Run(_onUsedScript, gameObject, user);
            if (_hasInventory)
                Debug.Log($"[Placeable:{Tag}] Open inventory container UI");
        }
    }

    /// <summary>Waypoint marker — used by AI for patrol routes etc.</summary>
    public class WaypointMarker : MonoBehaviour
    {
        public string Tag         { get; set; }
        public string DisplayName { get; set; }
    }

    /// <summary>EventArgs for area transition requests.</summary>
    public class AreaTransitionEventArgs : Core.EventBus.GameEventArgs
    {
        public string TargetArea  { get; }
        public string DoorTag     { get; }
        public AreaTransitionEventArgs(string area, string door) { TargetArea = area; DoorTag = door; }
    }
}
