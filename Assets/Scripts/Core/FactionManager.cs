using System;
using System.Collections.Generic;
using UnityEngine;

namespace KotORUnity.Core
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  FACTION SYSTEM  —  mirrors CFactionManager / CSWSFaction from the PDF
    //
    //  The original engine has exactly 7 primary (hard-coded) factions plus a
    //  dynamic CExoArrayList<CSWSFaction*> for module-defined factions.
    //  Faction relations are a symmetric matrix: faction[a].GetRelation(b).
    // ═══════════════════════════════════════════════════════════════════════════

    public enum FactionRelation
    {
        Friendly  =  1,
        Neutral   =  0,
        Hostile   = -1
    }

    [Serializable]
    public class KotORFaction
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public int    Id;
        public string Name;

        // ── Relation table: maps other FactionId → relation ──────────────────
        private readonly Dictionary<int, FactionRelation> _relations
            = new Dictionary<int, FactionRelation>();

        public KotORFaction(int id, string name) { Id = id; Name = name; }

        public void SetRelation(int otherFactionId, FactionRelation rel)
            => _relations[otherFactionId] = rel;

        public FactionRelation GetRelation(int otherFactionId)
            => _relations.TryGetValue(otherFactionId, out var r) ? r : FactionRelation.Neutral;

        public bool IsHostileTo(int otherFactionId)
            => GetRelation(otherFactionId) == FactionRelation.Hostile;

        public bool IsFriendlyTo(int otherFactionId)
            => GetRelation(otherFactionId) == FactionRelation.Friendly;

        /// <summary>Update the faction name from repute.2da label column.</summary>
        public void RenameFromTable(string label) { Name = label; }

        public override string ToString() => $"Faction[{Id}:{Name}]";
    }

    /// <summary>
    /// Mirrors CFactionManager from the KotOR 1 system layout PDF.
    ///
    /// The original engine defines exactly 7 primary factions by ordinal, then
    /// stores additional module-defined factions in a dynamic list.
    ///
    /// Primary faction IDs (0-6) are hard-coded to match the original engine
    /// so that creature UTCs with faction_id=1 still resolve correctly.
    /// </summary>
    public static class FactionManager
    {
        // ── PRIMARY FACTION IDs — match original engine ordinals ──────────────
        public const int FACTION_HOSTILE_1  = 0;   // standard enemies
        public const int FACTION_FRIENDLY_1 = 1;   // player + companions
        public const int FACTION_HOSTILE_2  = 2;   // Sith/dark side NPCs
        public const int FACTION_NEUTRAL    = 3;   // merchants, civilians
        public const int FACTION_FRIENDLY_2 = 4;   // Republic forces
        public const int FACTION_PREDATOR   = 5;   // wildlife, rancors
        public const int FACTION_HOSTILE_3  = 6;   // Czerka, GenoHaradan
        public const int PRIMARY_COUNT      = 7;

        // ── STORAGE  (mirrors 7 primary + CExoArrayList<CSWSFaction*>) ────────
        private static readonly KotORFaction[] _primary = new KotORFaction[PRIMARY_COUNT];
        private static readonly List<KotORFaction> _extra = new List<KotORFaction>();
        private static int _nextDynamicId = PRIMARY_COUNT;

        public static bool IsInitialized { get; private set; }

        // ── INITIALISE ────────────────────────────────────────────────────────
        static FactionManager() => Initialize();

        public static void Initialize()
        {
            // Build the 7 primary factions
            _primary[FACTION_HOSTILE_1]  = new KotORFaction(FACTION_HOSTILE_1,  "Hostile1");
            _primary[FACTION_FRIENDLY_1] = new KotORFaction(FACTION_FRIENDLY_1, "Friendly1");
            _primary[FACTION_HOSTILE_2]  = new KotORFaction(FACTION_HOSTILE_2,  "Hostile2");
            _primary[FACTION_NEUTRAL]    = new KotORFaction(FACTION_NEUTRAL,    "Neutral");
            _primary[FACTION_FRIENDLY_2] = new KotORFaction(FACTION_FRIENDLY_2, "Friendly2");
            _primary[FACTION_PREDATOR]   = new KotORFaction(FACTION_PREDATOR,   "Predator");
            _primary[FACTION_HOSTILE_3]  = new KotORFaction(FACTION_HOSTILE_3,  "Hostile3");

            // Default relation table (mirrors KotOR 1 factions.2da defaults)
            // Friendly1 ↔ Friendly2 = friendly
            SetRelation(FACTION_FRIENDLY_1, FACTION_FRIENDLY_2, FactionRelation.Friendly);

            // Hostile factions are hostile to both friendly factions
            foreach (int hf in new[]{ FACTION_HOSTILE_1, FACTION_HOSTILE_2, FACTION_HOSTILE_3 })
            {
                SetRelation(hf, FACTION_FRIENDLY_1, FactionRelation.Hostile);
                SetRelation(hf, FACTION_FRIENDLY_2, FactionRelation.Hostile);
                // Hostile factions friendly to each other
                foreach (int hf2 in new[]{ FACTION_HOSTILE_1, FACTION_HOSTILE_2, FACTION_HOSTILE_3 })
                    if (hf != hf2)
                        SetRelation(hf, hf2, FactionRelation.Friendly);
            }

            // Predators are hostile to everyone
            for (int i = 0; i < PRIMARY_COUNT; i++)
                if (i != FACTION_PREDATOR)
                    SetRelation(FACTION_PREDATOR, i, FactionRelation.Hostile);

            _extra.Clear();
            _nextDynamicId = PRIMARY_COUNT;
            IsInitialized = true;

            Debug.Log($"[FactionManager] Initialized {PRIMARY_COUNT} primary factions.");
        }

        // ── RELATION API ──────────────────────────────────────────────────────
        /// <summary>Set relation bidirectionally (symmetric, like the original engine).</summary>
        public static void SetRelation(int a, int b, FactionRelation rel)
        {
            var fa = GetFaction(a);
            var fb = GetFaction(b);
            if (fa == null || fb == null) return;
            fa.SetRelation(b, rel);
            fb.SetRelation(a, rel);
        }

        public static FactionRelation GetRelation(int a, int b)
            => GetFaction(a)?.GetRelation(b) ?? FactionRelation.Neutral;

        public static bool AreHostile(int a, int b)
            => GetRelation(a, b) == FactionRelation.Hostile;

        public static bool AreFriendly(int a, int b)
            => GetRelation(a, b) == FactionRelation.Friendly;

        // ── LOOKUP ────────────────────────────────────────────────────────────
        public static KotORFaction GetFaction(int id)
        {
            if (id >= 0 && id < PRIMARY_COUNT) return _primary[id];
            foreach (var f in _extra) if (f.Id == id) return f;
            return null;
        }

        public static KotORFaction GetFaction(string name)
        {
            foreach (var f in _primary)
                if (f != null && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
            foreach (var f in _extra)
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        // ── DYNAMIC FACTION CREATION (module-defined) ─────────────────────────
        /// <summary>
        /// Create a new module-defined faction.
        /// Mirrors CExoArrayList addition in CFactionManager.
        /// </summary>
        public static KotORFaction CreateFaction(string name)
        {
            var f = new KotORFaction(_nextDynamicId++, name);
            _extra.Add(f);
            Debug.Log($"[FactionManager] Created dynamic faction: {f}");
            return f;
        }

        /// <summary>All factions: primary + dynamic.</summary>
        public static IEnumerable<KotORFaction> AllFactions()
        {
            foreach (var f in _primary) if (f != null) yield return f;
            foreach (var f in _extra)  yield return f;
        }

        /// <summary>Reset dynamic factions (call on module unload).</summary>
        public static void ClearDynamicFactions()
        {
            _extra.Clear();
            _nextDynamicId = PRIMARY_COUNT;
        }
    }
}
