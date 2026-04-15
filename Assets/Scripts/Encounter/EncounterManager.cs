using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.AI.Enemy;
using KotORUnity.Inventory;
using KotORUnity.Party;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.Encounter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  LOOT TABLE
    // ═══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class LootEntry
    {
        public string  ItemResRef;          // e.g. "g_w_blstrpstl001"
        [Range(0f, 1f)]
        public float   DropChance = 0.5f;   // 0–1 probability
        public int     MinQuantity = 1;
        public int     MaxQuantity = 1;
        public bool    IsGuaranteed = false;// always drop regardless of chance
    }

    [Serializable]
    public class LootTable
    {
        public string        TableId;
        public List<LootEntry> Entries = new List<LootEntry>();
        public int           GuaranteedCredits   = 0;
        public int           BonusCreditsMax     = 0;// random 0..BonusCreditsMax added

        /// <summary>Roll the loot table; returns list of resrefs to give to the party.</summary>
        public List<string> Roll(out int credits)
        {
            var drops = new List<string>();
            foreach (var entry in Entries)
            {
                if (entry.IsGuaranteed || UnityEngine.Random.value <= entry.DropChance)
                {
                    int qty = UnityEngine.Random.Range(entry.MinQuantity, entry.MaxQuantity + 1);
                    for (int i = 0; i < qty; i++)
                        drops.Add(entry.ItemResRef);
                }
            }
            credits = GuaranteedCredits + UnityEngine.Random.Range(0, BonusCreditsMax + 1);
            return drops;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  WAVE DEFINITION
    // ═══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class EncounterWave
    {
        public string            WaveLabel = "Wave 1";
        public List<EnemyAI>     Enemies   = new List<EnemyAI>();
        public List<Transform>   SpawnPoints = new List<Transform>();
        [Tooltip("Seconds after the previous wave is cleared before this wave spawns.")]
        public float             DelayAfterPrevious = 2f;
        [Tooltip("Spawn enemies from the EnemyPrefabs list at the SpawnPoints if no pre-placed enemies exist.")]
        public bool              UseSpawnPrefabs = false;
        public List<GameObject>  EnemyPrefabs = new List<GameObject>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  FACTION RELATIONSHIP TABLE
    // ═══════════════════════════════════════════════════════════════════════════

    public enum FactionRelation { Neutral, Friendly, Hostile }

    [Serializable]
    public class FactionEntry
    {
        public string        FactionId;
        public FactionRelation RelationToPlayer = FactionRelation.Hostile;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ENCOUNTER MANAGER  (v2 — wave spawning, factions, loot)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Manages combat encounters — wave spawning, state tracking, faction awareness,
    /// loot distribution, and completion.
    ///
    /// Features (v2):
    ///   • Multi-wave support: each wave spawns when the previous is cleared.
    ///   • Faction table: each encounter can override global faction relations.
    ///   • Loot table: items and credits awarded on completion.
    ///   • EncounterType auto-classification (Swarm / SniperDuel / Assassination / Siege).
    ///   • XP award via LevelSystem on completion.
    ///   • Publishes EncounterStarted / EncounterCompleted on EventBus.
    ///
    /// Attach one EncounterManager per encounter zone. Wire enemies/waves in the Inspector.
    /// </summary>
    public class EncounterManager : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Encounter Identity")]
        [SerializeField] private string encounterName  = "Encounter";
        [SerializeField] private EncounterType encounterType = EncounterType.Standard;
        [SerializeField] private int   encounterIndex  = 0;  // HP scaling index
        [SerializeField] private bool  autoStartOnTrigger = true;

        [Header("Waves")]
        [Tooltip("Define one or more waves. Each wave's enemies are spawned after the previous wave is cleared.")]
        [SerializeField] private List<EncounterWave> waves = new List<EncounterWave>();
        [Tooltip("If true, the EncounterManager creates a single wave from the legacy 'enemies' list below.")]
        [SerializeField] private bool useWaveSystem = true;

        [Header("Legacy (single wave — used when useWaveSystem=false)")]
        [SerializeField] private List<EnemyAI> enemies = new List<EnemyAI>();

        [Header("Faction Overrides")]
        [Tooltip("Override the global faction relationships for this encounter.")]
        [SerializeField] private List<FactionEntry> factionOverrides = new List<FactionEntry>();

        [Header("Loot")]
        [SerializeField] private LootTable lootTable;
        [Tooltip("Drop loot on the ground at corpse positions when true; add directly to party inventory when false.")]
        [SerializeField] private bool       spawnLootPickups = false;
        [SerializeField] private GameObject lootPickupPrefab;

        [Header("XP Reward")]
        [SerializeField] private float completionXP = 200f;
        [SerializeField] private bool  scaleXPWithWaveCount = true;

        // ── STATE ──────────────────────────────────────────────────────────────
        private bool     _encounterActive    = false;
        private bool     _encounterCompleted = false;
        private int      _currentWaveIndex   = -1;
        private List<EnemyAI> _activeEnemies = new List<EnemyAI>();
        private GameMode _modeAtStart        = GameMode.Action;

        private Progression.LevelSystem  _levelSystem;
        private ModeSwitchSystem         _modeSwitchSystem;

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<int>  OnWaveStarted;      // wave index
        public event Action<int>  OnWaveCleared;      // wave index
        public event Action       OnEncounterComplete;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _modeSwitchSystem = FindObjectOfType<ModeSwitchSystem>();
            _levelSystem      = FindObjectOfType<Progression.LevelSystem>();
            EventBus.Subscribe(EventBus.EventType.EntityKilled, OnEntityKilled);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.EntityKilled, OnEntityKilled);
        }

        private void Update()
        {
            if (_encounterActive && !_encounterCompleted)
                CheckWaveCleared();
        }

        // ── TRIGGER ZONE ───────────────────────────────────────────────────────
        private void OnTriggerEnter(Collider other)
        {
            if (!autoStartOnTrigger || _encounterActive) return;
            if (other.CompareTag("Player"))
                StartEncounter();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Register an extra enemy into the current wave at runtime.</summary>
        public void RegisterEnemy(EnemyAI enemy)
        {
            if (enemy == null) return;
            _activeEnemies.Add(enemy);
            if (!useWaveSystem) enemies.Add(enemy);
        }

        /// <summary>Start the encounter (also callable from another script / trigger).</summary>
        public void StartEncounter()
        {
            if (_encounterActive || _encounterCompleted) return;

            _encounterActive = true;
            _modeAtStart     = _modeSwitchSystem?.CurrentMode ?? GameMode.Action;

            // Build wave list from legacy list if needed
            if (!useWaveSystem && waves.Count == 0)
            {
                var legacyWave = new EncounterWave { WaveLabel = "Wave 1" };
                legacyWave.Enemies.AddRange(enemies);
                waves.Add(legacyWave);
            }

            // Auto-classify
            if (encounterType == EncounterType.Standard)
                encounterType = ClassifyEncounter();

            Debug.Log($"[EncounterManager] '{encounterName}' started — Type: {encounterType}, Waves: {waves.Count}");

            // Apply faction overrides
            ApplyFactionOverrides();

            EventBus.Publish(EventBus.EventType.EncounterStarted,
                new EventBus.EncounterEventArgs(encounterType, waves.Count));

            _modeSwitchSystem?.RecordEncounterStart();

            // Kick off wave 0
            StartCoroutine(SpawnWave(0, 0f));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  WAVE SYSTEM
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator SpawnWave(int waveIdx, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (waveIdx >= waves.Count) yield break;

            _currentWaveIndex = waveIdx;
            _activeEnemies.Clear();

            var wave = waves[waveIdx];
            Debug.Log($"[EncounterManager] Spawning wave {waveIdx}: '{wave.WaveLabel}' — {wave.Enemies.Count} pre-placed enemies");

            // Use pre-placed enemies
            foreach (var e in wave.Enemies)
                if (e != null) _activeEnemies.Add(e);

            // Optionally spawn from prefabs at spawn points
            if (wave.UseSpawnPrefabs && wave.EnemyPrefabs.Count > 0 && wave.SpawnPoints.Count > 0)
            {
                int prefabIdx = 0;
                foreach (var sp in wave.SpawnPoints)
                {
                    var prefab = wave.EnemyPrefabs[prefabIdx % wave.EnemyPrefabs.Count];
                    prefabIdx++;
                    if (prefab == null || sp == null) continue;

                    var go = Instantiate(prefab, sp.position, sp.rotation);
                    var ai = go.GetComponent<EnemyAI>();
                    if (ai != null) _activeEnemies.Add(ai);
                }
            }

            // Scale enemy HP for subsequent waves
            if (waveIdx > 0)
                ScaleEnemiesForWave(waveIdx);

            OnWaveStarted?.Invoke(waveIdx);
            Debug.Log($"[EncounterManager] Wave {waveIdx} active with {_activeEnemies.Count} enemies.");
        }

        private void ScaleEnemiesForWave(int waveIdx)
        {
            float scaleMult = 1f + 0.15f * waveIdx;  // +15% HP per wave
            foreach (var e in _activeEnemies)
            {
                if (e == null) continue;
                var stats = e.GetComponent<Player.PlayerStatsBehaviour>()?.Stats;
                if (stats != null)
                    stats.ScaleHealth(scaleMult);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  WAVE COMPLETION CHECK
        // ══════════════════════════════════════════════════════════════════════

        private void CheckWaveCleared()
        {
            if (_activeEnemies.Count == 0) return;

            bool allDead = true;
            foreach (var enemy in _activeEnemies)
            {
                if (enemy == null) continue;
                var stats = enemy.GetComponent<Player.PlayerStatsBehaviour>()?.Stats;
                if (stats != null && stats.IsAlive) { allDead = false; break; }
            }

            if (!allDead) return;

            OnWaveCleared?.Invoke(_currentWaveIndex);
            Debug.Log($"[EncounterManager] Wave {_currentWaveIndex} cleared.");

            int nextWave = _currentWaveIndex + 1;
            if (nextWave < waves.Count)
            {
                float delay = waves[nextWave].DelayAfterPrevious;
                StartCoroutine(SpawnWave(nextWave, delay));
            }
            else
            {
                CompleteEncounter();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENCOUNTER COMPLETION
        // ══════════════════════════════════════════════════════════════════════

        private void CompleteEncounter()
        {
            _encounterCompleted = true;
            _encounterActive    = false;

            Debug.Log($"[EncounterManager] '{encounterName}' completed.");

            // Remove faction overrides
            RemoveFactionOverrides();

            EventBus.Publish(EventBus.EventType.EncounterCompleted,
                new EventBus.EncounterEventArgs(encounterType, waves.Count));

            // XP
            float xp = completionXP;
            if (scaleXPWithWaveCount && waves.Count > 1)
                xp *= (1f + 0.25f * (waves.Count - 1));
            _levelSystem?.AwardXP(xp, $"Encounter: {encounterName}");

            // Mode affinity
            GameMode finalMode = _modeSwitchSystem?.CurrentMode ?? GameMode.Action;
            _modeSwitchSystem?.RecordEncounterCompleted(finalMode);

            // Loot
            if (lootTable != null)
                DistributeLoot();

            OnEncounterComplete?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOOT DISTRIBUTION
        // ══════════════════════════════════════════════════════════════════════

        private void DistributeLoot()
        {
            int credits;
            List<string> drops = lootTable.Roll(out credits);

            var inv = InventoryManager.Instance;
            if (inv != null && credits > 0)
            {
                inv.AddCredits(credits);
                Debug.Log($"[EncounterManager] Loot: +{credits} credits.");
            }

            if (drops.Count == 0) return;

            if (spawnLootPickups && lootPickupPrefab != null)
            {
                // Scatter loot pickups near corpse positions
                Vector3 centre = GetCorpseCentre();
                foreach (var resref in drops)
                {
                    Vector2 offset2D = UnityEngine.Random.insideUnitCircle * 1.5f;
                    Vector3 pos = centre + new Vector3(offset2D.x, 0f, offset2D.y);
                    var pickup = Instantiate(lootPickupPrefab, pos, Quaternion.identity);
                    var pickupComp = pickup.GetComponent<World.LootPickup>();
                    pickupComp?.Initialise(resref, 1);
                }
                Debug.Log($"[EncounterManager] Spawned {drops.Count} loot pickups.");
            }
            else if (inv != null)
            {
                // Add directly to player inventory
                foreach (var resref in drops)
                    inv.AddItemByResRef(resref);
                Debug.Log($"[EncounterManager] Added {drops.Count} items to inventory.");
            }
        }

        private Vector3 GetCorpseCentre()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (var e in _activeEnemies)
            {
                if (e == null) continue;
                sum += e.transform.position;
                count++;
            }
            return count > 0 ? sum / count : transform.position;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FACTION HANDLING
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyFactionOverrides()
        {
            foreach (var fo in factionOverrides)
            {
                // Notify enemies belonging to this faction
                foreach (var e in _activeEnemies)
                {
                    if (e == null) continue;
                    if (e.FactionId == fo.FactionId)
                        e.SetFactionRelation(fo.RelationToPlayer);
                }
            }
        }

        private void RemoveFactionOverrides()
        {
            foreach (var e in _activeEnemies)
                e?.ResetFactionRelation();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENCOUNTER CLASSIFICATION
        // ══════════════════════════════════════════════════════════════════════

        private EncounterType ClassifyEncounter()
        {
            int totalEnemies = 0;
            foreach (var w in waves)
                totalEnemies += w.Enemies.Count + (w.UseSpawnPrefabs ? w.EnemyPrefabs.Count : 0);

            if (totalEnemies >= 8) return EncounterType.Swarm;
            if (waves.Count >= 3)  return EncounterType.Siege;

            bool hasBoss = false;
            int  rangedCount = 0;
            foreach (var w in waves)
                foreach (var e in w.Enemies)
                {
                    if (e == null) continue;
                    if (e.EnemyType == EnemyType.Boss)   hasBoss = true;
                    if (e.EnemyType == EnemyType.Ranged) rangedCount++;
                }

            if (hasBoss) return EncounterType.Assassination;
            if (rangedCount >= 2 && totalEnemies <= 3) return EncounterType.SniperDuel;
            return EncounterType.Standard;
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnEntityKilled(EventBus.GameEventArgs args) { /* CheckWaveCleared handled in Update */ }

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public bool IsActive        => _encounterActive;
        public bool IsCompleted     => _encounterCompleted;
        public int  CurrentWave     => _currentWaveIndex;
        public int  TotalWaves      => waves.Count;
        public EncounterType EncounterType => encounterType;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  LOOT PICKUP  (world object that auto-gives items when player steps on it)
    // ═══════════════════════════════════════════════════════════════════════════

    namespace World
    {
        /// <summary>
        /// Placed in the world by EncounterManager after combat completes.
        /// Collected automatically when the player enters the trigger collider.
        /// </summary>
        public class LootPickup : MonoBehaviour
        {
            [SerializeField] private string _itemResRef;
            [SerializeField] private int    _quantity = 1;
            [SerializeField] private TextMesh _label;

            private bool _collected = false;

            public void Initialise(string resRef, int qty)
            {
                _itemResRef = resRef;
                _quantity   = qty;
                if (_label != null) _label.text = resRef;
            }

            private void OnTriggerEnter(Collider other)
            {
                if (_collected || !other.CompareTag("Player")) return;
                Collect();
            }

            private void Collect()
            {
                _collected = true;
                var inv = InventoryManager.Instance;
                if (inv != null)
                {
                    for (int i = 0; i < _quantity; i++)
                        inv.AddItemByResRef(_itemResRef);
                    Debug.Log($"[LootPickup] Collected {_quantity}× {_itemResRef}");
                }
                Destroy(gameObject);
            }
        }
    }
}
