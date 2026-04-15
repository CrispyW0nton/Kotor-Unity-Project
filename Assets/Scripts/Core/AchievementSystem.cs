using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Core
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ACHIEVEMENT SYSTEM + CODEX
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Two cooperating systems in one file:
    //
    //  AchievementSystem
    //    • Tracks unlock conditions for each achievement.
    //    • Persists via PlayerPrefs (JSON blob keyed "achievements_v1").
    //    • Fires OnAchievementUnlocked event and publishes UIHUDRefresh to HUD.
    //
    //  CodexSystem
    //    • Discovery log — world lore, creature entries, planet logs.
    //    • Persists via PlayerPrefs (JSON blob keyed "codex_v1").
    //    • Automatically records first-time entity/location encounters.

    // ─────────────────────────────────────────────────────────────────────────
    //  DATA  MODELS
    // ─────────────────────────────────────────────────────────────────────────

    public enum AchievementCategory
    {
        Combat, Exploration, Story, Social, Mastery, Secret
    }

    [Serializable]
    public class AchievementDef
    {
        public string              Id;
        public string              Title;
        public string              Description;
        public AchievementCategory Category;
        public int                 PointValue  = 10;
        public bool                IsSecret    = false;
        public string              IconResRef  = "";

        // Progress-style achievements
        public bool                HasProgress = false;
        public int                 ProgressTarget = 1;
    }

    [Serializable]
    public class AchievementState
    {
        public string Id;
        public bool   Unlocked;
        public int    Progress;
        public string UnlockTimestamp;
    }

    [Serializable]
    public class CodexEntry
    {
        public string   Id;
        public string   Title;
        public string   Body;
        public string   Category;   // "Creature", "Planet", "Lore", "Person"
        public string   IconResRef;
        public bool     Discovered;
        public string   DiscoveredTimestamp;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SAVE DATA
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    public class AchievementSaveData
    {
        public List<AchievementState> States = new List<AchievementState>();
    }

    [Serializable]
    public class CodexSaveData
    {
        public List<CodexEntry> Entries = new List<CodexEntry>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ACHIEVEMENT SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public class AchievementSystem : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static AchievementSystem Instance { get; private set; }

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<AchievementDef> OnAchievementUnlocked;

        // ── STATE ──────────────────────────────────────────────────────────────
        private Dictionary<string, AchievementDef>   _defs    = new Dictionary<string, AchievementDef>();
        private Dictionary<string, AchievementState> _states  = new Dictionary<string, AchievementState>();

        private const string PREFS_KEY = "achievements_v1";

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterBuiltInAchievements();
            LoadFromPrefs();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BUILT-IN ACHIEVEMENT DEFINITIONS  (KotOR-themed)
        // ─────────────────────────────────────────────────────────────────────

        private void RegisterBuiltInAchievements()
        {
            Register(new AchievementDef {
                Id = "first_blood",         Title = "First Blood",
                Description = "Win your first combat encounter.",
                Category = AchievementCategory.Combat, PointValue = 10
            });
            Register(new AchievementDef {
                Id = "jedi_padawan",        Title = "Jedi Padawan",
                Description = "Use a Force power for the first time.",
                Category = AchievementCategory.Combat, PointValue = 10
            });
            Register(new AchievementDef {
                Id = "force_master",        Title = "Force Master",
                Description = "Use 50 Force powers in combat.",
                Category = AchievementCategory.Mastery,
                HasProgress = true, ProgressTarget = 50, PointValue = 50
            });
            Register(new AchievementDef {
                Id = "galaxy_traveller",    Title = "Galaxy Traveller",
                Description = "Visit 5 different planets.",
                Category = AchievementCategory.Exploration,
                HasProgress = true, ProgressTarget = 5, PointValue = 30
            });
            Register(new AchievementDef {
                Id = "bounty_hunter",       Title = "Bounty Hunter",
                Description = "Defeat 100 enemies.",
                Category = AchievementCategory.Combat,
                HasProgress = true, ProgressTarget = 100, PointValue = 40
            });
            Register(new AchievementDef {
                Id = "light_side_hero",     Title = "Servant of Light",
                Description = "Reach maximum Light Side alignment.",
                Category = AchievementCategory.Story, PointValue = 50
            });
            Register(new AchievementDef {
                Id = "dark_side_lord",      Title = "Lord of the Sith",
                Description = "Reach maximum Dark Side alignment.",
                Category = AchievementCategory.Story, PointValue = 50
            });
            Register(new AchievementDef {
                Id = "pazaak_champion",     Title = "Pazaak Champion",
                Description = "Win 10 games of Pazaak.",
                Category = AchievementCategory.Social,
                HasProgress = true, ProgressTarget = 10, PointValue = 20
            });
            Register(new AchievementDef {
                Id = "master_of_war",       Title = "Master of War",
                Description = "Win encounters in both Action and RTS mode.",
                Category = AchievementCategory.Mastery, PointValue = 25
            });
            Register(new AchievementDef {
                Id = "no_medpacs",          Title = "Warrior's Creed",
                Description = "Complete an encounter without using a Medpac.",
                Category = AchievementCategory.Mastery, PointValue = 30
            });
            Register(new AchievementDef {
                Id = "grenade_master",      Title = "Grenade Master",
                Description = "Throw 25 grenades.",
                Category = AchievementCategory.Combat,
                HasProgress = true, ProgressTarget = 25, PointValue = 15
            });
            Register(new AchievementDef {
                Id = "codex_scholar",       Title = "Codex Scholar",
                Description = "Discover 20 Codex entries.",
                Category = AchievementCategory.Exploration,
                HasProgress = true, ProgressTarget = 20, PointValue = 25
            });
            Register(new AchievementDef {
                Id = "speedrun_encounter",  Title = "Lightning Reflexes",
                Description = "Win an encounter in under 30 seconds.",
                Category = AchievementCategory.Secret, IsSecret = true, PointValue = 30
            });
            Register(new AchievementDef {
                Id = "full_party",          Title = "Friends in High Places",
                Description = "Have all available companions in your party at once.",
                Category = AchievementCategory.Social, PointValue = 20
            });
            Register(new AchievementDef {
                Id = "thermal_overkill",    Title = "Overkill",
                Description = "Kill 3 enemies with a single Thermal Detonator.",
                Category = AchievementCategory.Secret, IsSecret = true, PointValue = 40
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        public void Register(AchievementDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _defs[def.Id] = def;
            if (!_states.ContainsKey(def.Id))
                _states[def.Id] = new AchievementState { Id = def.Id, Progress = 0, Unlocked = false };
        }

        /// <summary>Immediately unlock an achievement by id.</summary>
        public void Unlock(string id)
        {
            if (!_defs.TryGetValue(id, out var def)) return;
            var state = GetOrCreateState(id);
            if (state.Unlocked) return;

            state.Unlocked = true;
            state.Progress = def.HasProgress ? def.ProgressTarget : 1;
            state.UnlockTimestamp = DateTime.UtcNow.ToString("o");

            SaveToPrefs();
            OnAchievementUnlocked?.Invoke(def);
            EventBus.Publish(EventBus.EventType.UIHUDRefresh,      new EventBus.GameEventArgs());
            EventBus.Publish(EventBus.EventType.AchievementUnlocked,
                new EventBus.AchievementEventArgs(def.Id, def.Title, def.PointValue));
            Debug.Log($"[Achievement] Unlocked: {def.Title}  (+{def.PointValue} pts)");
        }

        /// <summary>Increment a progress achievement. Unlocks automatically when target is reached.</summary>
        public void IncrementProgress(string id, int amount = 1)
        {
            if (!_defs.TryGetValue(id, out var def)) return;
            var state = GetOrCreateState(id);
            if (state.Unlocked) return;

            state.Progress = Mathf.Min(state.Progress + amount, def.ProgressTarget);
            if (state.Progress >= def.ProgressTarget)
                Unlock(id);
            else
                SaveToPrefs();
        }

        public bool IsUnlocked(string id) =>
            _states.TryGetValue(id, out var s) && s.Unlocked;

        public int GetProgress(string id) =>
            _states.TryGetValue(id, out var s) ? s.Progress : 0;

        public int TotalPoints()
        {
            int pts = 0;
            foreach (var kvp in _states)
                if (kvp.Value.Unlocked && _defs.TryGetValue(kvp.Key, out var def))
                    pts += def.PointValue;
            return pts;
        }

        public List<AchievementDef> GetAll() => new List<AchievementDef>(_defs.Values);

        public List<AchievementDef> GetUnlocked()
        {
            var list = new List<AchievementDef>();
            foreach (var kvp in _defs)
                if (_states.TryGetValue(kvp.Key, out var s) && s.Unlocked)
                    list.Add(kvp.Value);
            return list;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  EVENT WIRING  (auto-unlock on game events)
        // ─────────────────────────────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            EventBus.Subscribe(EventBus.EventType.EncounterCompleted, OnEncounterCompleted);
            EventBus.Subscribe(EventBus.EventType.AbilityUsed,        OnAbilityUsed);
            EventBus.Subscribe(EventBus.EventType.EntityKilled,       OnEntityKilled);
            EventBus.Subscribe(EventBus.EventType.ModuleLoaded,       OnModuleLoaded);
            EventBus.Subscribe(EventBus.EventType.AlignmentChanged,   OnAlignmentChanged);
        }

        private void UnsubscribeFromEvents()
        {
            EventBus.Unsubscribe(EventBus.EventType.EncounterCompleted, OnEncounterCompleted);
            EventBus.Unsubscribe(EventBus.EventType.AbilityUsed,        OnAbilityUsed);
            EventBus.Unsubscribe(EventBus.EventType.EntityKilled,       OnEntityKilled);
            EventBus.Unsubscribe(EventBus.EventType.ModuleLoaded,       OnModuleLoaded);
            EventBus.Unsubscribe(EventBus.EventType.AlignmentChanged,   OnAlignmentChanged);
        }

        private void OnEncounterCompleted(EventBus.GameEventArgs args)
        {
            Unlock("first_blood");
            Unlock("master_of_war");
        }

        private void OnAbilityUsed(EventBus.GameEventArgs args)
        {
            if (args is EventBus.AbilityEventArgs ab)
            {
                if (ab.AbilityName.Contains("Force") || ab.AbilityName.Contains("Stasis")
                    || ab.AbilityName.Contains("Lightning") || ab.AbilityName.Contains("Push"))
                {
                    Unlock("jedi_padawan");
                    IncrementProgress("force_master");
                }
                if (ab.AbilityName.Contains("Grenade") || ab.AbilityName.Contains("Detonator"))
                    IncrementProgress("grenade_master");
            }
        }

        private void OnEntityKilled(EventBus.GameEventArgs args)
        {
            IncrementProgress("bounty_hunter");
        }

        private void OnModuleLoaded(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModuleEventArgs mod)
            {
                // Check galaxy travel: count unique planet prefixes
                string planet = ExtractPlanetPrefix(mod.ModuleName);
                if (!string.IsNullOrEmpty(planet))
                {
                    string key = $"visited_{planet}";
                    if (!PlayerPrefs.HasKey(key))
                    {
                        PlayerPrefs.SetInt(key, 1);
                        IncrementProgress("galaxy_traveller");
                    }
                }
            }
        }

        private void OnAlignmentChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ForcePowerEventArgs fp)
            {
                if (fp.Alignment >= 100)  Unlock("light_side_hero");
                if (fp.Alignment <= -100) Unlock("dark_side_lord");
            }
        }

        private static string ExtractPlanetPrefix(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName) || moduleName.Length < 3) return "";
            // KotOR module names: "001EBO" = Endar Spire, "101PER" = Peragus, etc.
            // First 3 chars = area index; chars 3+ = planet code
            return moduleName.Length >= 6 ? moduleName.Substring(3, 3).ToUpper() : "";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PERSISTENCE
        // ─────────────────────────────────────────────────────────────────────

        private void SaveToPrefs()
        {
            var data = new AchievementSaveData();
            foreach (var s in _states.Values)
                data.States.Add(s);
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadFromPrefs()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var data = JsonUtility.FromJson<AchievementSaveData>(json);
                foreach (var s in data.States)
                    _states[s.Id] = s;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AchievementSystem] Could not load save data: {e.Message}");
            }
        }

        private AchievementState GetOrCreateState(string id)
        {
            if (!_states.TryGetValue(id, out var s))
            {
                s = new AchievementState { Id = id };
                _states[id] = s;
            }
            return s;
        }

        /// <summary>Called by SaveManager to embed achievements in the game save.</summary>
        public AchievementSaveData GetSaveData()
        {
            var data = new AchievementSaveData();
            foreach (var s in _states.Values) data.States.Add(s);
            return data;
        }

        public void RestoreFromSave(AchievementSaveData data)
        {
            if (data == null) return;
            foreach (var s in data.States)
                _states[s.Id] = s;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CODEX SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    public class CodexSystem : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static CodexSystem Instance { get; private set; }

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<CodexEntry> OnEntryDiscovered;

        // ── STATE ──────────────────────────────────────────────────────────────
        private Dictionary<string, CodexEntry> _entries = new Dictionary<string, CodexEntry>();

        private const string PREFS_KEY = "codex_v1";

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterBuiltInEntries();
            LoadFromPrefs();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  BUILT-IN CODEX ENTRIES  (KotOR lore)
        // ─────────────────────────────────────────────────────────────────────

        private void RegisterBuiltInEntries()
        {
            // ── PLANETS ───────────────────────────────────────────────────────
            AddEntry(new CodexEntry {
                Id = "planet_taris",  Title = "Taris",
                Body = "An ecumenopolis — a city-planet — that was largely destroyed by the Sith " +
                       "bombardment ordered by Darth Malak. Its lower levels are home to gangs, " +
                       "rakghouls, and the remnants of its former inhabitants.",
                Category = "Planet", IconResRef = "lbl_map_tar"
            });
            AddEntry(new CodexEntry {
                Id = "planet_dantooine", Title = "Dantooine",
                Body = "A peaceful Outer Rim world home to the Jedi Enclave. Fertile plains and " +
                       "kath hound herds make it an unlikely headquarters for the Jedi Order.",
                Category = "Planet", IconResRef = "lbl_map_dan"
            });
            AddEntry(new CodexEntry {
                Id = "planet_tatooine", Title = "Tatooine",
                Body = "A harsh desert world with twin suns, dominated by the Hutt clans and their " +
                       "criminal enterprises. Home to sand people, moisture farmers, and the Star Map.",
                Category = "Planet", IconResRef = "lbl_map_tat"
            });
            AddEntry(new CodexEntry {
                Id = "planet_kashyyyk", Title = "Kashyyyk",
                Body = "The Wookiee homeworld — a densely forested planet with towering wroshyr trees. " +
                       "The Czerka Corporation exploits its people as cheap labor.",
                Category = "Planet", IconResRef = "lbl_map_kas"
            });
            AddEntry(new CodexEntry {
                Id = "planet_manaan",   Title = "Manaan",
                Body = "The sole source of kolto in the galaxy, Manaan remains neutral between the " +
                       "Republic and Sith. Ruled by the Selkath and governed by strict laws of neutrality.",
                Category = "Planet", IconResRef = "lbl_map_man"
            });
            AddEntry(new CodexEntry {
                Id = "planet_korriban", Title = "Korriban",
                Body = "The ancestral home of the Sith species and their dark-side traditions. " +
                       "The Sith Academy trains new acolytes in the ways of the dark side. " +
                       "Its tombs hide the power of ancient Sith lords.",
                Category = "Planet", IconResRef = "lbl_map_kor"
            });
            AddEntry(new CodexEntry {
                Id = "planet_unknown",  Title = "Unknown World",
                Body = "A mysterious world cloaked from all known star charts. Its surface is inhabited " +
                       "by the Rakata, once masters of the ancient Infinite Empire.",
                Category = "Planet", IconResRef = "lbl_map_unk"
            });

            // ── CREATURES ─────────────────────────────────────────────────────
            AddEntry(new CodexEntry {
                Id = "creature_rakghoul", Title = "Rakghoul",
                Body = "Twisted mutants found in the Taris undercity, created by the rakghoul plague. " +
                       "Those infected transform within hours. The only cure is rakghoul serum.",
                Category = "Creature"
            });
            AddEntry(new CodexEntry {
                Id = "creature_kath_hound", Title = "Kath Hound",
                Body = "Native predators of Dantooine. Horned, canine beasts that hunt in packs. " +
                       "Most are hostile, but domesticated variants are kept as pets by colonists.",
                Category = "Creature"
            });
            AddEntry(new CodexEntry {
                Id = "creature_rancor", Title = "Rancor",
                Body = "One of the most feared creatures in the galaxy. Nearly impervious to conventional " +
                       "blaster fire. Encountered in the lower sewers of Taris.",
                Category = "Creature"
            });

            // ── LORE ──────────────────────────────────────────────────────────
            AddEntry(new CodexEntry {
                Id = "lore_star_forge",  Title = "The Star Forge",
                Body = "An ancient space station of Rakata construction that draws on the dark side of " +
                       "the Force to produce near-limitless war materiel. Darth Revan discovered it and " +
                       "used it to build the Sith fleet.",
                Category = "Lore"
            });
            AddEntry(new CodexEntry {
                Id = "lore_star_map",    Title = "Star Maps",
                Body = "Ancient Rakata artifacts scattered across the galaxy. Each contains coordinates " +
                       "pointing towards the Star Forge. Collecting all five reveals its location.",
                Category = "Lore"
            });
            AddEntry(new CodexEntry {
                Id = "lore_the_force",   Title = "The Force",
                Body = "An energy field created by all living things that binds the galaxy together. " +
                       "Force-sensitives can draw on its light or dark side for extraordinary abilities.",
                Category = "Lore"
            });
            AddEntry(new CodexEntry {
                Id = "lore_pazaak",      Title = "Pazaak",
                Body = "A popular card game throughout the galaxy. Players build a hand as close to 20 " +
                       "as possible without exceeding it. Special side-deck cards add strategic depth.",
                Category = "Lore"
            });

            // ── PERSONS ───────────────────────────────────────────────────────
            AddEntry(new CodexEntry {
                Id = "person_bastila",   Title = "Bastila Shan",
                Body = "A young Jedi Knight gifted with the rare ability of Battle Meditation, which " +
                       "can shift the tide of entire battles. She captured the broken Revan on the " +
                       "Leviathan.",
                Category = "Person"
            });
            AddEntry(new CodexEntry {
                Id = "person_carth",     Title = "Carth Onasi",
                Body = "A seasoned Republic pilot and soldier. Haunted by the betrayal of his mentor " +
                       "Saul Karath during the Mandalorian Wars. Fiercely loyal once trust is earned.",
                Category = "Person"
            });
            AddEntry(new CodexEntry {
                Id = "person_malak",     Title = "Darth Malak",
                Body = "Former Jedi Knight and apprentice of Darth Revan. After Revan's capture, Malak " +
                       "took command of the Sith Empire and launched a devastating campaign against the " +
                       "Republic.",
                Category = "Person"
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PUBLIC API
        // ─────────────────────────────────────────────────────────────────────

        public void AddEntry(CodexEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id)) return;
            if (!_entries.ContainsKey(entry.Id))
                _entries[entry.Id] = entry;
        }

        /// <summary>Mark a codex entry as discovered (first-time encounter triggers the event).</summary>
        public void Discover(string id)
        {
            if (!_entries.TryGetValue(id, out var entry)) return;
            if (entry.Discovered) return;

            entry.Discovered = true;
            entry.DiscoveredTimestamp = DateTime.UtcNow.ToString("o");

            SaveToPrefs();
            OnEntryDiscovered?.Invoke(entry);
            EventBus.Publish(EventBus.EventType.CodexEntryDiscovered,
                new EventBus.CodexEventArgs(entry.Id, entry.Title, entry.Category));

            // Award codex achievement progress
            AchievementSystem.Instance?.IncrementProgress("codex_scholar");

            Debug.Log($"[Codex] Discovered: {entry.Title}  [{entry.Category}]");
        }

        public CodexEntry GetEntry(string id) =>
            _entries.TryGetValue(id, out var e) ? e : null;

        public List<CodexEntry> GetAll() =>
            new List<CodexEntry>(_entries.Values);

        public List<CodexEntry> GetDiscovered()
        {
            var list = new List<CodexEntry>();
            foreach (var e in _entries.Values)
                if (e.Discovered) list.Add(e);
            return list;
        }

        public List<CodexEntry> GetByCategory(string category)
        {
            var list = new List<CodexEntry>();
            foreach (var e in _entries.Values)
                if (e.Category == category) list.Add(e);
            return list;
        }

        public int DiscoveredCount => GetDiscovered().Count;
        public int TotalCount      => _entries.Count;

        // ─────────────────────────────────────────────────────────────────────
        //  PERSISTENCE
        // ─────────────────────────────────────────────────────────────────────

        private void SaveToPrefs()
        {
            var data = new CodexSaveData();
            foreach (var e in _entries.Values) data.Entries.Add(e);
            PlayerPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        private void LoadFromPrefs()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                var data = JsonUtility.FromJson<CodexSaveData>(json);
                foreach (var e in data.Entries)
                    if (_entries.TryGetValue(e.Id, out var existing))
                    {
                        existing.Discovered          = e.Discovered;
                        existing.DiscoveredTimestamp = e.DiscoveredTimestamp;
                    }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Codex] Could not load save data: {ex.Message}");
            }
        }

        public CodexSaveData GetSaveData()
        {
            var data = new CodexSaveData();
            foreach (var e in _entries.Values) data.Entries.Add(e);
            return data;
        }

        public void RestoreFromSave(CodexSaveData data)
        {
            if (data == null) return;
            foreach (var e in data.Entries)
                if (_entries.TryGetValue(e.Id, out var existing))
                {
                    existing.Discovered          = e.Discovered;
                    existing.DiscoveredTimestamp = e.DiscoveredTimestamp;
                }
        }
    }
}
