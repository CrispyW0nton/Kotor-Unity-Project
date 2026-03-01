using System;
using System.IO;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.SaveSystem
{
    /// <summary>
    /// Full game state serialized to disk.
    /// Stores all data needed to restore a session exactly.
    /// Includes mode preference history for the telemetry/affinity system.
    /// </summary>
    [Serializable]
    public class GameState
    {
        public string version = "2.0";
        public string timestamp;
        public string moduleName;
        public string kotorDir;
        public string targetGame;

        // Player
        public PlayerStatsData playerStats;
        public float[] playerPosition = new float[3];
        public float[] playerFacing = new float[2];
        public string equippedWeaponId;
        public string[] abilityIds;
        public string[] unlockedTalents;
        public float[] abilityCooldowns;

        // Companions
        public CompanionSaveData[] companions;

        // Mode state
        public string activeMode;
        public float switchCooldownRemaining;
        public float pauseCooldownRemaining;

        // Mode preference history
        public int encountersSinceRTS;
        public int encountersSinceAction;
        public float rtsCombatTimeTotal;
        public float actionCombatTimeTotal;
        public bool rtsAffinityBonusActive;
        public bool actionAffinityBonusActive;
    }

    [Serializable]
    public class CompanionSaveData
    {
        public string companionId;
        public PlayerStatsData stats;
        public float[] position = new float[3];
    }

    /// <summary>
    /// Save/Load system for the full game state.
    /// Supports quick save, auto save, and 5 manual slots.
    /// 
    /// Design doc note: "Save during RTS pause (exploitable but accepted — single-player game)"
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Save Settings")]
        [SerializeField] private bool autoSaveEnabled = true;
        [SerializeField] private float autoSaveInterval = 300f; // 5 minutes

        // ── STATE ──────────────────────────────────────────────────────────────
        private float _autoSaveTimer = 0f;

        // ── PATHS ──────────────────────────────────────────────────────────────
        private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");

        private static string GetSavePath(SaveSlot slot)
        {
            string fileName = slot switch
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
            return Path.Combine(SaveDirectory, fileName);
        }

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            Directory.CreateDirectory(SaveDirectory);
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

        // ── SAVE ───────────────────────────────────────────────────────────────
        public void QuickSave() => Save(SaveSlot.QuickSave);
        public void QuickLoad() => Load(SaveSlot.QuickSave);

        public void Save(SaveSlot slot)
        {
            try
            {
                GameState state = CaptureGameState();
                string json = JsonUtility.ToJson(state, prettyPrint: true);
                string path = GetSavePath(slot);
                File.WriteAllText(path, json);

                EventBus.Publish(EventBus.EventType.GameSaved);
                Debug.Log($"[SaveManager] Game saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        public void Load(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[SaveManager] No save file at: {path}");
                return;
            }

            try
            {
                string json = File.ReadAllText(path);
                GameState state = JsonUtility.FromJson<GameState>(json);
                RestoreGameState(state);

                EventBus.Publish(EventBus.EventType.GameLoaded);
                Debug.Log($"[SaveManager] Game loaded: {path}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
            }
        }

        // ── STATE CAPTURE ──────────────────────────────────────────────────────
        private GameState CaptureGameState()
        {
            var state = new GameState
            {
                timestamp = DateTime.UtcNow.ToString("O"),
                version = "2.0"
            };

            // Module
            if (GameManager.Instance != null)
            {
                state.kotorDir = GameManager.Instance.KotorDir;
                state.targetGame = GameManager.Instance.TargetGame.ToString();
            }

            // Player
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                var statsB = playerObj.GetComponent<PlayerStatsBehaviour>();
                if (statsB != null)
                    state.playerStats = statsB.Stats.ToData();

                state.playerPosition = new float[] {
                    playerObj.transform.position.x,
                    playerObj.transform.position.y,
                    playerObj.transform.position.z
                };
            }

            // Mode
            var ms = FindObjectOfType<ModeSwitchSystem>();
            if (ms != null)
            {
                state.activeMode = ms.CurrentMode.ToString();
                state.switchCooldownRemaining = ms.SwitchCooldownRemaining;
                state.pauseCooldownRemaining = ms.PauseCooldownRemaining;
                state.encountersSinceRTS = 0;   // tracked in ModeSwitchSystem
                state.encountersSinceAction = 0;
            }

            // Companions
            var companions = FindObjectsOfType<AI.Companion.CompanionAI>();
            state.companions = new CompanionSaveData[companions.Length];
            for (int i = 0; i < companions.Length; i++)
            {
                state.companions[i] = new CompanionSaveData
                {
                    companionId = companions[i].CompanionName,
                    stats = companions[i].Stats?.ToData(),
                    position = new float[] {
                        companions[i].transform.position.x,
                        companions[i].transform.position.y,
                        companions[i].transform.position.z
                    }
                };
            }

            return state;
        }

        // ── STATE RESTORE ──────────────────────────────────────────────────────
        private void RestoreGameState(GameState state)
        {
            // Restore player
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && state.playerStats != null)
            {
                var statsB = playerObj.GetComponent<PlayerStatsBehaviour>();
                statsB?.Stats.FromData(state.playerStats);

                if (state.playerPosition != null && state.playerPosition.Length == 3)
                {
                    playerObj.transform.position = new Vector3(
                        state.playerPosition[0],
                        state.playerPosition[1],
                        state.playerPosition[2]);
                }
            }

            // Restore mode
            if (!string.IsNullOrEmpty(state.activeMode))
            {
                var ms = FindObjectOfType<ModeSwitchSystem>();
                if (ms != null && Enum.TryParse<GameMode>(state.activeMode, out GameMode savedMode))
                    ms.ForceSwitch(savedMode);
            }

            // Load module
            if (!string.IsNullOrEmpty(state.moduleName) && GameManager.Instance != null)
                GameManager.Instance.LoadModule(state.moduleName);
        }

        // ── SAVE METADATA ──────────────────────────────────────────────────────
        public bool SaveExists(SaveSlot slot) => File.Exists(GetSavePath(slot));

        public DateTime? GetSaveTimestamp(SaveSlot slot)
        {
            string path = GetSavePath(slot);
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<GameState>(json);
                if (DateTime.TryParse(state.timestamp, out DateTime ts)) return ts;
            }
            catch { }
            return null;
        }
    }
}
