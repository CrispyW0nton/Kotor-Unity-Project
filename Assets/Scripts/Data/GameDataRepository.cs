using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.Bootstrap;
using KotORUnity.Core;

namespace KotORUnity.Data
{
    /// <summary>
    /// Central repository for all KotOR static game data loaded from 2DA tables.
    ///
    /// Loaded once at startup by SceneBootstrapper.
    /// Access anywhere via GameDataRepository.Instance.
    ///
    /// Key tables:
    ///   classes       - character class definitions (hitdie, attackbonustable, etc.)
    ///   baseitems     - all item types and their stats
    ///   feat          - all feats (label, gainmultiple, effectsstack, etc.)
    ///   spells        - Force powers and spells
    ///   appearance    - creature visual appearance rows
    ///   portraits     - portrait textures
    ///   placeables    - placeable object appearance
    ///   doortypes     - door appearance
    ///   iprp_*        - item property tables
    ///   nwnx_*        - engine extension tables
    /// </summary>
    public class GameDataRepository : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static GameDataRepository Instance { get; private set; }

        // ── TABLE REGISTRY ─────────────────────────────────────────────────────
        private readonly Dictionary<string, TwoDATable> _tables
            = new Dictionary<string, TwoDATable>(System.StringComparer.OrdinalIgnoreCase);

        // Typed shortcuts for frequently accessed tables
        public TwoDATable Classes      { get; private set; }
        public TwoDATable BaseItems    { get; private set; }
        public TwoDATable Feats        { get; private set; }
        public TwoDATable Spells       { get; private set; }
        public TwoDATable Appearance   { get; private set; }
        public TwoDATable Portraits    { get; private set; }
        public TwoDATable Placeables   { get; private set; }
        public TwoDATable DoorTypes    { get; private set; }
        public TwoDATable Skills       { get; private set; }
        public TwoDATable SoundSets    { get; private set; }
        public TwoDATable AttackBonus  { get; private set; }
        public TwoDATable SavingThrows { get; private set; }
        public TwoDATable XPTable      { get; private set; }
        // ── Blueprint-derived tables (CSWRules / CTwoDimArrays) ────────────────
        public TwoDATable RacialTypes  { get; private set; }  // racialtypes.2da — CSWRace
        public TwoDATable Ranges       { get; private set; }  // ranges.2da      — weapon range data
        public TwoDATable SpellSchool  { get; private set; }  // spellschool.2da — Force power schools
        public TwoDATable Factions     { get; private set; }  // repute.2da      — CFactionManager seed

        public bool IsLoaded { get; private set; } = false;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── LOADING ───────────────────────────────────────────────────────────
        /// <summary>
        /// Load all required 2DA tables from the KotOR data directory.
        /// Called by SceneBootstrapper after archives are mounted.
        /// </summary>
        public void LoadAll(ResourceManager resources)
        {
            if (IsLoaded) return;

            Debug.Log("[GameDataRepository] Loading 2DA tables...");

            Classes      = Load2DA(resources, "classes");
            BaseItems    = Load2DA(resources, "baseitems");
            Feats        = Load2DA(resources, "feat");
            Spells       = Load2DA(resources, "spells");
            Appearance   = Load2DA(resources, "appearance");
            Portraits    = Load2DA(resources, "portraits");
            Placeables   = Load2DA(resources, "placeables");
            DoorTypes    = Load2DA(resources, "doortypes");
            Skills       = Load2DA(resources, "skills");
            SoundSets    = Load2DA(resources, "soundset");
            // KotOR1 table names (KotOR2 uses attackbonustable / savingthrowtable)
            AttackBonus  = Load2DA(resources, "ipaddbonustable");
            if (AttackBonus == null || AttackBonus.RowCount == 0)
                AttackBonus = Load2DA(resources, "attackbonustable"); // KotOR2 fallback
            SavingThrows = Load2DA(resources, "iprp_savethrow");
            if (SavingThrows == null || SavingThrows.RowCount == 0)
                SavingThrows = Load2DA(resources, "savingthrowtable"); // KotOR2 fallback
            XPTable      = Load2DA(resources, "exptable");

            // ── Blueprint-derived tables ───────────────────────────────────────
            // racialtypes.2da — mirrors CSWRace in CSWRules (only Human in KotOR1 but
            //                   still needed for GFF creature parsing)
            RacialTypes  = Load2DA(resources, "racialtypes");
            // ranges.2da — weapon range categories used by baseitems attack resolution
            Ranges       = Load2DA(resources, "ranges");
            // spellschool.2da — Force power school alignment data
            SpellSchool  = Load2DA(resources, "spellschool");
            // repute.2da — seeds CFactionManager with the 7 primary faction names
            // and default inter-faction relations
            Factions     = Load2DA(resources, "repute");
            if (Factions != null && Factions.RowCount > 0)
                SeedFactionManager();

            // Store all loaded tables in the dictionary for Get(name) lookups
            StoreInDictionary();

            IsLoaded = true;
            Debug.Log("[GameDataRepository] All 2DA tables loaded.");
        }

        /// <summary>
        /// Store all named tables into _tables so Get(string) can retrieve any
        /// table by name, including dynamically-loaded module tables.
        /// </summary>
        private void StoreInDictionary()
        {
            void Store(string name, TwoDATable t) { if (t != null) _tables[name] = t; }
            Store("classes",       Classes);      Store("baseitems",   BaseItems);
            Store("feat",          Feats);        Store("spells",      Spells);
            Store("appearance",    Appearance);   Store("portraits",   Portraits);
            Store("placeables",    Placeables);   Store("doortypes",   DoorTypes);
            Store("skills",        Skills);       Store("soundset",    SoundSets);
            Store("ipaddbonustable", AttackBonus);
            Store("iprp_savethrow", SavingThrows);
            Store("exptable",      XPTable);      Store("racialtypes", RacialTypes);
            Store("ranges",        Ranges);       Store("spellschool", SpellSchool);
            Store("repute",        Factions);
        }

        /// <summary>
        /// Seed FactionManager from repute.2da.
        /// Column layout: label, faction_friendly, faction_hostile (row = faction id).
        /// </summary>
        private void SeedFactionManager()
        {
            // The 7 primary faction names in repute.2da override the hardcoded defaults
            for (int row = 0; row < Mathf.Min(Factions.RowCount, FactionManager.PRIMARY_COUNT); row++)
            {
                string label = Factions.GetString(row, "label", "");
                if (!string.IsNullOrEmpty(label))
                    FactionManager.GetFaction(row)?.RenameFromTable(label);
            }
            Debug.Log($"[GameDataRepository] FactionManager seeded from repute.2da "
                    + $"({Factions.RowCount} rows).");
        }

        private TwoDATable Load2DA(ResourceManager resources, string name)
        {
            byte[] data = resources.GetResource(name, ResourceType.TwoDA);
            if (data == null)
            {
                Debug.LogWarning($"[GameDataRepository] 2DA not found: {name}.2da");
                return new TwoDATable { Name = name };
            }
            var table = TwoDAReader.Load(data, name);
            if (table != null)
                Debug.Log($"[GameDataRepository]   Loaded {name}.2da ({table.RowCount} rows)");
            return table ?? new TwoDATable { Name = name };
        }

        // ── PUBLIC ACCESSORS ──────────────────────────────────────────────────
        public TwoDATable Get(string name)
        {
            if (_tables.TryGetValue(name.ToLowerInvariant(), out var t)) return t;
            Debug.LogWarning($"[GameDataRepository] Table not found: {name}");
            return null;
        }

        // ── HELPER METHODS ────────────────────────────────────────────────────

        /// <summary>Get the hit die for a KotOR class row index.</summary>
        public int GetClassHitDie(int classRow) =>
            Classes?.GetInt(classRow, "hitdie", 6) ?? 6;

        /// <summary>Get the attack bonus for a class at a given level.</summary>
        public int GetAttackBonus(int classRow, int level)
        {
            string tableName = Classes?.GetString(classRow, "attackbonustable", "") ?? "";
            var atkTable = Get(tableName.ToLowerInvariant());
            return atkTable?.GetInt(level, "bab", 0) ?? 0;
        }

        /// <summary>Get XP required for a given level (1-based).</summary>
        public int GetXPForLevel(int level)
        {
            if (XPTable == null) return level * 1000;
            return XPTable.GetInt(level - 1, "xp", (level - 1) * 1000);
        }

        /// <summary>Get the label string for a feat row.</summary>
        public string GetFeatLabel(int featRow) =>
            Feats?.GetString(featRow, "label", $"Feat_{featRow}") ?? $"Feat_{featRow}";

        /// <summary>Get the resref for a creature's appearance model.</summary>
        public string GetAppearanceModel(int appearanceRow) =>
            Appearance?.GetString(appearanceRow, "modela", "") ?? "";

        /// <summary>Get the texture variation for a creature appearance.</summary>
        public string GetAppearanceTexture(int appearanceRow) =>
            Appearance?.GetString(appearanceRow, "texa", "") ?? "";

        // ── 2DA PATCH API (used by ModLoader) ─────────────────────────────────

        /// <summary>
        /// Apply a 2DA patch from a mod's TwoDA/tableName_patch.json file.
        /// Supports AppendRow and SetCell operations.
        /// </summary>
        public void Apply2DAPatch(string tableName, string patchJson)
        {
            var table = Get(tableName);
            if (table == null)
            {
                Debug.LogWarning($"[GameDataRepository] 2DA patch: table '{tableName}' not loaded — skipping.");
                return;
            }
            try
            {
                // Minimal JSON parsing for patch operations
                // Expected format: array of {operation, row, column, newValue, rowData}
                // We use a lightweight approach since Unity's JsonUtility doesn't support arrays directly
                patchJson = patchJson.Trim();
                if (patchJson.StartsWith("["))
                {
                    // Strip outer brackets and split by top-level { }
                    var ops = ExtractJsonObjects(patchJson);
                    foreach (var op in ops)
                        ApplySinglePatch(table, op);
                }
                else
                {
                    ApplySinglePatch(table, patchJson);
                }
                Debug.Log($"[GameDataRepository] Applied 2DA patch to '{tableName}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GameDataRepository] 2DA patch error ({tableName}): {ex.Message}");
            }
        }

        private void ApplySinglePatch(TwoDATable table, string opJson)
        {
            // Simple key-value extractor for the patch JSON
            string op  = ExtractJsonString(opJson, "operation");
            int    row = ExtractJsonInt(opJson, "row", -1);

            switch (op?.ToLower())
            {
                case "appendrow":
                    table.AppendRow(ExtractJsonStringDict(opJson, "rowData"));
                    break;
                case "setcell":
                    string col = ExtractJsonString(opJson, "column");
                    string val = ExtractJsonString(opJson, "newValue");
                    if (row >= 0 && !string.IsNullOrEmpty(col))
                        table.SetCell(row, col, val);
                    break;
                case "deleterow":
                    if (row >= 0) table.DeleteRow(row);
                    break;
                default:
                    Debug.LogWarning($"[GameDataRepository] Unknown 2DA patch operation: '{op}'");
                    break;
            }
        }

        // ── MINIMAL JSON HELPERS (no dependency on external libs) ────────────

        private static string ExtractJsonString(string json, string key)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json,
                $@"""{key}""\s*:\s*""([^""]*?)""");
            return m.Success ? m.Groups[1].Value : null;
        }

        private static int ExtractJsonInt(string json, string key, int defaultVal)
        {
            var m = System.Text.RegularExpressions.Regex.Match(json,
                $@"""{key}""\s*:\s*(-?\d+)");
            return m.Success && int.TryParse(m.Groups[1].Value, out int v) ? v : defaultVal;
        }

        private static Dictionary<string, string> ExtractJsonStringDict(string json, string key)
        {
            var dict = new Dictionary<string, string>();
            var m = System.Text.RegularExpressions.Regex.Match(json,
                $@"""{key}""\s*:\s*\{{([^}}]*)\}}");
            if (!m.Success) return dict;
            string block = m.Groups[1].Value;
            foreach (System.Text.RegularExpressions.Match pair in
                System.Text.RegularExpressions.Regex.Matches(block, @"""(\w+)""\s*:\s*""([^""]*)"""))
                dict[pair.Groups[1].Value] = pair.Groups[2].Value;
            return dict;
        }

        private static List<string> ExtractJsonObjects(string json)
        {
            var list  = new List<string>();
            int depth = 0;
            int start = -1;
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '{') { if (depth++ == 0) start = i; }
                else if (json[i] == '}') { if (--depth == 0 && start >= 0) { list.Add(json.Substring(start, i - start + 1)); start = -1; } }
            }
            return list;
        }
    }
}
