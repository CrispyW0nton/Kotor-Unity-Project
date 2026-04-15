using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Inventory;
using KotORUnity.Party;
using KotORUnity.Scripting;
using KotORUnity.UI;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.SaveSystem
{
    /// <summary>
    /// Full game state serialized to disk as JSON.
    /// Stores everything needed to restore a session exactly.
    ///
    /// Version 3.0 adds: faction relation overrides, area visit history,
    /// visited modules list, door/container states, and LevelSystem class data.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public string version    = "3.0";
        public string timestamp;
        public string moduleName;
        public string kotorDir;
        public string targetGame;

        // ── Player ────────────────────────────────────────────────────────────
        public PlayerStatsData playerStats;
        public float[] playerPosition = new float[3];
        public float[] playerFacing   = new float[2];
        public string  equippedWeaponId;
        public string[] abilityIds;
        public string[] unlockedTalents;
        public float[]  abilityCooldowns;
        public float    currentFP;
        public float    maxFP;
        public string   playerClass;   // KotORClass enum name

        // ── Companions ────────────────────────────────────────────────────────
        public CompanionSaveData[] companions;

        // ── Inventory ─────────────────────────────────────────────────────────
        public InventorySnapshot inventory;
        public int               playerCredits;

        // ── Party ─────────────────────────────────────────────────────────────
        public PartySaveData party;

        // ── Journal ───────────────────────────────────────────────────────────
        public JournalSaveData journal;

        // ── Global variables (NWScript) ───────────────────────────────────────
        public GlobalVarsSaveData globalVars;

        // ── Mode state ────────────────────────────────────────────────────────
        public string activeMode;
        public float  switchCooldownRemaining;
        public float  pauseCooldownRemaining;

        // ── Mode preference history ───────────────────────────────────────────
        public int   encountersSinceRTS;
        public int   encountersSinceAction;
        public float rtsCombatTimeTotal;
        public float actionCombatTimeTotal;
        public bool  rtsAffinityBonusActive;
        public bool  actionAffinityBonusActive;

        // ── World / Area state  (v3.0) ────────────────────────────────────────
        /// <summary>Modules the player has visited this playthrough (for galaxy map etc.).</summary>
        public string[] visitedModules;

        /// <summary>
        /// Faction relation overrides set by scripts at runtime.
        /// Encoded as "factionA:factionB:relation" (e.g. "1:0:-1").
        /// </summary>
        public string[] factionOverrides;

        /// <summary>Tags of doors/containers that have been opened/looted.</summary>
        public string[] openedDoorTags;
        public string[] lootedContainerTags;

        /// <summary>Creature tags that have been permanently killed (won't respawn).</summary>
        public string[] permanentlyKilledTags;

        // ── Galaxy Map  (v3.1) ────────────────────────────────────────────────
        /// <summary>Full galaxy map state: planet unlock / visit / star-map flags.</summary>
        public KotORUnity.World.GalaxyMapSaveData galaxyMap;

        // ── Achievements + Codex  (v3.2) ──────────────────────────────────────
        public KotORUnity.Core.AchievementSaveData achievements;
        public KotORUnity.Core.CodexSaveData       codex;
    }

    [Serializable]
    public class CompanionSaveData
    {
        public string         companionId;
        public PlayerStatsData stats;
        public float[]         position = new float[3];
        public bool            isActive;
    }

    /// <summary>Lightweight inventory snapshot used by GameState JSON (resref list + credits).
    /// The full slot-aware version is KotORUnity.Inventory.InventorySaveData.</summary>
    [Serializable]
    public class InventorySnapshot
    {
        public string[] itemResRefs;   // all items in player inventory (resrefs)
        public int      credits;
    }

    [Serializable]
    public class PartySaveData
    {
        public string[] rosterMemberIds;
        public string[] activePartyIds;  // up to 3
        public int      credits;         // party table credits
        public bool     soloMode;
        public int[]    pazaakCards;
        public int[]    pazaakSideDeck;
    }

    [Serializable]
    public class GlobalVarsSaveData
    {
        public string[] boolKeys;
        public bool[]   boolValues;
        public string[] intKeys;
        public int[]    intValues;
        public string[] floatKeys;
        public float[]  floatValues;
        public string[] stringKeys;
        public string[] stringValues;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  WORLD STATE TRACKER  — runtime list of world object states
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Tracks runtime world state changes (opened doors, killed NPCs, visited areas).
    /// Feeds into SaveManager.CaptureGameState() for full serialization.
    /// </summary>
    public static class WorldStateTracker
    {
        private static readonly HashSet<string> _visitedModules       = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _openedDoors          = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _lootedContainers     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> _permanentlyKilled    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<string>    _factionOverrides     = new List<string>();

        public static void MarkModuleVisited(string module)
        {
            if (!string.IsNullOrEmpty(module))
                _visitedModules.Add(module);
        }
        public static bool HasVisited(string module) => _visitedModules.Contains(module);

        public static void MarkDoorOpened(string tag)     { if (!string.IsNullOrEmpty(tag)) _openedDoors.Add(tag); }
        public static bool IsDoorOpened(string tag)       => _openedDoors.Contains(tag);

        public static void MarkContainerLooted(string tag) { if (!string.IsNullOrEmpty(tag)) _lootedContainers.Add(tag); }
        public static bool IsContainerLooted(string tag)   => _lootedContainers.Contains(tag);

        public static void MarkPermanentlyKilled(string tag) { if (!string.IsNullOrEmpty(tag)) _permanentlyKilled.Add(tag); }
        public static bool IsPermanentlyKilled(string tag)   => _permanentlyKilled.Contains(tag);

        public static void RecordFactionOverride(int a, int b, FactionRelation rel)
        {
            string key = $"{a}:{b}:{(int)rel}";
            if (!_factionOverrides.Contains(key)) _factionOverrides.Add(key);
            // Apply immediately
            FactionManager.SetRelation(a, b, rel);
        }

        // Serialization helpers
        public static string[] GetVisitedModules()      => new List<string>(_visitedModules).ToArray();
        public static string[] GetOpenedDoors()         => new List<string>(_openedDoors).ToArray();
        public static string[] GetLootedContainers()    => new List<string>(_lootedContainers).ToArray();
        public static string[] GetPermanentlyKilled()   => new List<string>(_permanentlyKilled).ToArray();
        public static string[] GetFactionOverrides()    => _factionOverrides.ToArray();

        public static void RestoreFromSave(GameState s)
        {
            _visitedModules.Clear();
            _openedDoors.Clear();
            _lootedContainers.Clear();
            _permanentlyKilled.Clear();
            _factionOverrides.Clear();

            if (s.visitedModules != null)       foreach (var m in s.visitedModules)     _visitedModules.Add(m);
            if (s.openedDoorTags != null)       foreach (var t in s.openedDoorTags)     _openedDoors.Add(t);
            if (s.lootedContainerTags != null)  foreach (var t in s.lootedContainerTags) _lootedContainers.Add(t);
            if (s.permanentlyKilledTags != null) foreach (var t in s.permanentlyKilledTags) _permanentlyKilled.Add(t);

            if (s.factionOverrides != null)
                foreach (var ov in s.factionOverrides)
                {
                    var parts = ov.Split(':');
                    if (parts.Length == 3 &&
                        int.TryParse(parts[0], out int fa) &&
                        int.TryParse(parts[1], out int fb) &&
                        int.TryParse(parts[2], out int rel))
                    {
                        FactionManager.SetRelation(fa, fb, (FactionRelation)rel);
                    }
                }
        }

        public static void Clear()
        {
            _visitedModules.Clear(); _openedDoors.Clear();
            _lootedContainers.Clear(); _permanentlyKilled.Clear();
            _factionOverrides.Clear();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  SAVE MANAGER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Save/Load system.
    /// Supports quick save, auto save, and 5 manual slots.
    ///
    /// Integrates: PlayerStats, ForcePowerManager, Inventory, Party, Journal,
    ///             GlobalVars (NWScript), ModeSwitchSystem.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static SaveManager Instance { get; private set; }

        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Auto Save")]
        [SerializeField] private bool  autoSaveEnabled  = true;
        [SerializeField] private float autoSaveInterval = 300f;

        // ── STATE ──────────────────────────────────────────────────────────────
        private float _autoSaveTimer = 0f;

        // ── PATHS ──────────────────────────────────────────────────────────────
        private static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, "Saves");

        private static string GetSavePath(SaveSlot slot)
        {
            string file = slot switch
            {
                SaveSlot.QuickSave => "quicksave.json",
                SaveSlot.AutoSave  => "autosave.json",
                SaveSlot.Manual1   => "save_01.json",
                SaveSlot.Manual2   => "save_02.json",
                SaveSlot.Manual3   => "save_03.json",
                SaveSlot.Manual4   => "save_04.json",
                SaveSlot.Manual5   => "save_05.json",
                _                  => "save_00.json"
            };
            return Path.Combine(SaveDirectory, file);
        }

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(SaveDirectory);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!autoSaveEnabled) return;
            _autoSaveTimer += Time.unscaledDeltaTime;
            if (_autoSaveTimer >= autoSaveInterval)
            {
                _autoSaveTimer = 0f;
                Save(SaveSlot.AutoSave);
            }
        }

        // ── QUICK SAVE/LOAD ────────────────────────────────────────────────────
        public void QuickSave() => Save(SaveSlot.QuickSave);
        public void QuickLoad() => Load(SaveSlot.QuickSave);

        // ── SAVE ───────────────────────────────────────────────────────────────
        public void Save(SaveSlot slot)
        {
            try
            {
                GameState state = CaptureGameState();
                string json = JsonUtility.ToJson(state, prettyPrint: true);
                string path = GetSavePath(slot);
                File.WriteAllText(path, json);

                EventBus.Publish(EventBus.EventType.GameSaved);
                Debug.Log($"[SaveManager] Saved → {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        // ── LOAD ───────────────────────────────────────────────────────────────
        public void Load(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] No save at: {path}");
                return;
            }

            try
            {
                string json   = File.ReadAllText(path);
                GameState state = JsonUtility.FromJson<GameState>(json);
                RestoreGameState(state);
                EventBus.Publish(EventBus.EventType.GameLoaded);
                Debug.Log($"[SaveManager] Loaded ← {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            }
        }

        // ── CAPTURE ────────────────────────────────────────────────────────────
        private GameState CaptureGameState()
        {
            var state = new GameState
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                version   = "2.1"
            };

            // GameManager refs
            if (GameManager.Instance != null)
            {
                state.kotorDir   = GameManager.Instance.KotorDir;
                state.targetGame = GameManager.Instance.TargetGame.ToString();
            }

            // Module
            var modLoader = FindObjectOfType<KotOR.Modules.ModuleLoader>();
            if (modLoader != null) state.moduleName = modLoader.CurrentModuleName;

            // Player
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                var statsB = playerObj.GetComponent<PlayerStatsBehaviour>();
                if (statsB != null)
                    state.playerStats = statsB.Stats.ToData();

                state.playerPosition = new float[]
                {
                    playerObj.transform.position.x,
                    playerObj.transform.position.y,
                    playerObj.transform.position.z
                };
                state.playerFacing = new float[]
                {
                    playerObj.transform.eulerAngles.x,
                    playerObj.transform.eulerAngles.y
                };

                // Force points
                var fpm = playerObj.GetComponent<Combat.ForcePowerManager>();
                if (fpm != null) { state.currentFP = fpm.CurrentFP; state.maxFP = fpm.MaxFP; }
            }

            // Inventory
            var invMgr = InventoryManager.Instance;
            if (invMgr != null)
            {
                var allItems = invMgr.PlayerInventory?.GetAllItemResRefs();
                state.inventory = new InventorySnapshot
                {
                    itemResRefs = allItems != null ? allItems.ToArray() : Array.Empty<string>(),
                    credits     = invMgr.PlayerCredits
                };
                state.playerCredits = invMgr.PlayerCredits;
            }

            // Party
            var partyMgr = PartyManager.Instance;
            if (partyMgr != null)
            {
                state.party = new PartySaveData
                {
                    rosterMemberIds = partyMgr.GetRosterIds(),
                    activePartyIds  = partyMgr.GetActivePartyIds()
                };
            }

            // Journal
            var journal = JournalSystem.Instance;
            if (journal != null) state.journal = journal.GetSaveData();

            // Global vars
            state.globalVars = GlobalVars.GetSaveData();

            // Mode
            var ms = FindObjectOfType<ModeSwitchSystem>();
            if (ms != null)
            {
                state.activeMode               = ms.CurrentMode.ToString();
                state.switchCooldownRemaining  = ms.SwitchCooldownRemaining;
                state.pauseCooldownRemaining   = ms.PauseCooldownRemaining;
            }

            // Companions
            var companions = FindObjectsOfType<AI.Companion.CompanionAI>();
            state.companions = new CompanionSaveData[companions.Length];
            for (int i = 0; i < companions.Length; i++)
            {
                var kcd = companions[i].GetComponent<World.KotorCreatureData>();
                state.companions[i] = new CompanionSaveData
                {
                    companionId = companions[i].CompanionName,
                    stats       = companions[i].Stats?.ToData(),
                    isActive    = kcd != null,
                    position    = new float[]
                    {
                        companions[i].transform.position.x,
                        companions[i].transform.position.y,
                        companions[i].transform.position.z
                    }
                };
            }

            // Party table extras (pazaak, credits, solo mode)
            var partyMgr2 = PartyManager.Instance;
            if (partyMgr2 != null && state.party != null)
            {
                state.party.credits      = partyMgr2.Table.Credits;
                state.party.soloMode     = partyMgr2.Table.SoloMode;
                state.party.pazaakCards  = (int[])partyMgr2.Table.PazaakCards.Clone();
                state.party.pazaakSideDeck = (int[])partyMgr2.Table.PazaakSideDeck.Clone();
            }

            // World state (v3.0)
            state.visitedModules       = WorldStateTracker.GetVisitedModules();
            state.factionOverrides     = WorldStateTracker.GetFactionOverrides();
            state.openedDoorTags       = WorldStateTracker.GetOpenedDoors();
            state.lootedContainerTags  = WorldStateTracker.GetLootedContainers();
            state.permanentlyKilledTags = WorldStateTracker.GetPermanentlyKilled();

            // Galaxy Map
            if (World.GalaxyMapManager.Instance != null)
                state.galaxyMap = World.GalaxyMapManager.Instance.GetSaveData();

            // Achievements + Codex
            if (Core.AchievementSystem.Instance != null)
                state.achievements = Core.AchievementSystem.Instance.GetSaveData();
            if (Core.CodexSystem.Instance != null)
                state.codex = Core.CodexSystem.Instance.GetSaveData();

            // LevelSystem class
            var ls = Progression.LevelSystem.Instance;
            if (ls != null) state.playerClass = ls.PlayerClass.ToString();

            return state;
        }

        // ── RESTORE ────────────────────────────────────────────────────────────
        private void RestoreGameState(GameState state)
        {
            // Player position / stats
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && state.playerStats != null)
            {
                var statsB = playerObj.GetComponent<PlayerStatsBehaviour>();
                statsB?.Stats.FromData(state.playerStats);

                if (state.playerPosition?.Length == 3)
                    playerObj.transform.position = new Vector3(
                        state.playerPosition[0], state.playerPosition[1], state.playerPosition[2]);

                // Force points
                var fpm = playerObj.GetComponent<Combat.ForcePowerManager>();
                if (fpm != null && state.maxFP > 0f)
                {
                    fpm.SetMaxFP(state.maxFP);
                    fpm.RestoreFP(state.currentFP);
                }
            }

            // Inventory
            var invMgr = InventoryManager.Instance;
            if (invMgr != null && state.inventory != null)
            {
                invMgr.PlayerInventory?.Clear();
                invMgr.SetCredits(state.inventory.credits);
                if (state.inventory.itemResRefs != null)
                    foreach (var resref in state.inventory.itemResRefs)
                        invMgr.PickUp(resref);
            }

            // Party
            var partyMgr = PartyManager.Instance;
            if (partyMgr != null && state.party != null)
            {
                partyMgr.RestoreFromSave(state.party.rosterMemberIds, state.party.activePartyIds);
                // Restore party table extras
                if (state.party.credits > 0) partyMgr.Table.AddCredits(state.party.credits);
                partyMgr.Table.SoloMode = state.party.soloMode;
                if (state.party.pazaakCards != null)
                    Array.Copy(state.party.pazaakCards, partyMgr.Table.PazaakCards,
                               Mathf.Min(state.party.pazaakCards.Length, partyMgr.Table.PazaakCards.Length));
                if (state.party.pazaakSideDeck != null)
                    Array.Copy(state.party.pazaakSideDeck, partyMgr.Table.PazaakSideDeck,
                               Mathf.Min(state.party.pazaakSideDeck.Length, partyMgr.Table.PazaakSideDeck.Length));
            }

            // Journal
            var journal = JournalSystem.Instance;
            if (journal != null) journal.RestoreFromSave(state.journal);

            // Global vars
            if (state.globalVars != null) GlobalVars.RestoreFromSave(state.globalVars);

            // World state (v3.0)
            WorldStateTracker.RestoreFromSave(state);

            // Galaxy Map (v3.1)
            if (state.galaxyMap != null && World.GalaxyMapManager.Instance != null)
                World.GalaxyMapManager.Instance.LoadSaveData(state.galaxyMap);

            // Achievements + Codex (v3.2)
            if (state.achievements != null && Core.AchievementSystem.Instance != null)
                Core.AchievementSystem.Instance.RestoreFromSave(state.achievements);
            if (state.codex != null && Core.CodexSystem.Instance != null)
                Core.CodexSystem.Instance.RestoreFromSave(state.codex);

            // Mode
            if (!string.IsNullOrEmpty(state.activeMode))
            {
                var ms = FindObjectOfType<ModeSwitchSystem>();
                if (ms != null && Enum.TryParse<GameMode>(state.activeMode, out GameMode mode))
                    ms.ForceSwitch(mode);
            }

            // Load module — triggers AreaLoader which re-spawns the world
            if (!string.IsNullOrEmpty(state.moduleName) && GameManager.Instance != null)
                GameManager.Instance.LoadModule(state.moduleName);

            EventBus.Publish(EventBus.EventType.UIHUDRefresh);
        }

        // ── METADATA ──────────────────────────────────────────────────────────
        public bool     SaveExists    (SaveSlot slot) => File.Exists(GetSavePath(slot));

        public DateTime? GetSaveTimestamp(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                string json  = File.ReadAllText(path);
                var    state = JsonUtility.FromJson<GameState>(json);
                if (DateTime.TryParse(state.timestamp, out DateTime ts)) return ts;
            }
            catch { }
            return null;
        }

        public string GetSaveModuleName(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                var state = JsonUtility.FromJson<GameState>(File.ReadAllText(path));
                return state.moduleName;
            }
            catch { return null; }
        }

        /// <summary>
        /// Returns the raw JSON of a save file, or null if it doesn't exist.
        /// Used by the SaveLoadUI to read metadata without a full deserialise.
        /// </summary>
        public string GetSaveJson(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return null;
            try   { return File.ReadAllText(path); }
            catch { return null; }
        }
    }
}
