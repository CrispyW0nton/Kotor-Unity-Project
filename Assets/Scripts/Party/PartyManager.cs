using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.Bootstrap;
using KotORUnity.Core;
using KotORUnity.World;

namespace KotORUnity.Party
{
    // ══════════════════════════════════════════════════════════════════════════
    //  PARTY MEMBER DATA
    // ══════════════════════════════════════════════════════════════════════════
    [Serializable]
    public class PartyMemberData
    {
        public string   ResRef;          // UTC resref
        public string   Tag;
        public string   DisplayName;
        public bool     IsPlayer;
        public bool     IsActive;        // in current 3-member party
        public int      CurrentHP;
        public int      MaxHP;
        public bool     IsAlive => CurrentHP > 0;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  PARTY TABLE  —  mirrors CSWPartyTable from the KotOR 1 system layout PDF
    //
    //  Original fields (verbatim from PDF):
    //    Pazaak Cards   : int[18]
    //    Pazaak Sidedeck: int[10]
    //    Galaxy Map Status: planets available / selectable / selected
    //    AI state, Controlled PC, XP, Credits, Time played, Cheats used, Solo Mode
    // ══════════════════════════════════════════════════════════════════════════
    [Serializable]
    public class PartyTable
    {
        // ── Pazaak ────────────────────────────────────────────────────────────
        /// <summary>Full card collection — 18 slots matching original int[18].</summary>
        public int[] PazaakCards    = new int[18];
        /// <summary>Active side-deck — 10 slots matching original int[10].</summary>
        public int[] PazaakSideDeck = new int[10];

        // ── Galaxy Map ────────────────────────────────────────────────────────
        /// <summary>Planet IDs that have been unlocked on the galaxy map.</summary>
        public List<int> GalaxyMapAvailable   = new List<int>();
        /// <summary>Planet IDs currently selectable (e.g. not locked by story).</summary>
        public List<int> GalaxyMapSelectable  = new List<int>();
        /// <summary>Currently selected planet ID (-1 = none).</summary>
        public int       GalaxyMapSelected    = -1;

        // ── State flags ───────────────────────────────────────────────────────
        public bool  SoloMode      = false;
        public bool  CheatsUsed    = false;
        public int   ControlledPC  = 0;    // index into roster

        // ── Currency & progression ────────────────────────────────────────────
        public int   Credits       = 0;
        public float XP            = 0f;
        public float TimePlayed    = 0f;   // seconds

        // ── AI state (bitmask; 0 = normal AI, mirrors nAIState field) ─────────
        public int   AIState       = 0;

        // ── Helpers ───────────────────────────────────────────────────────────
        public void AddCredits(int amount) => Credits = Mathf.Max(0, Credits + amount);
        public bool SpendCredits(int amount)
        {
            if (Credits < amount) return false;
            Credits -= amount;
            return true;
        }

        public void UnlockPlanet(int planetId)
        {
            if (!GalaxyMapAvailable.Contains(planetId))
                GalaxyMapAvailable.Add(planetId);
        }

        public void MakePlanetSelectable(int planetId)
        {
            UnlockPlanet(planetId);
            if (!GalaxyMapSelectable.Contains(planetId))
                GalaxyMapSelectable.Add(planetId);
        }
    }


    /// <summary>
    /// Manages the player's party roster and active formation.
    ///
    /// KotOR supports up to 9 recruited companions stored in the global party,
    /// but only 3 can be active in an area at a time.
    /// </summary>
    public class PartyManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static PartyManager Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Party Setup")]
        [Tooltip("Maximum number of active party members (including player).")]
        [SerializeField] private int maxActive = 3;

        [Tooltip("Companion prefab with NavMeshAgent, KotorCreatureData, CompanionAI.")]
        [SerializeField] private GameObject companionPrefab;

        [Header("Follow Settings")]
        [SerializeField] private float followDistance = 2.0f;
        [SerializeField] private float formationSpread = 1.8f;

        // ── RUNTIME DATA ──────────────────────────────────────────────────────
        private readonly List<PartyMemberData> _roster = new List<PartyMemberData>();
        private Transform                       _playerTransform;

        /// <summary>
        /// Full party table — mirrors CSWPartyTable (pazaak, galaxy map,
        /// credits, solo mode, AI state, etc.).
        /// </summary>
        public PartyTable Table { get; } = new PartyTable();

        public IReadOnlyList<PartyMemberData>   Roster         => _roster;
        public IEnumerable<PartyMemberData>     ActiveMembers  =>
            _roster.FindAll(m => m.IsActive && m.IsAlive);

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>Add credits to the party table.</summary>
        public void AddCredits(int amount) => Table.AddCredits(amount);
        /// <summary>Spend credits from the party table.</summary>
        public bool SpendCredits(int amount) => Table.SpendCredits(amount);
        /// <summary>Get current credits.</summary>
        public int  GetCredits() => Table.Credits;

        /// <summary>Add a companion to the global roster.</summary>
        public void Recruit(string utcResRef)
        {
            if (_roster.Exists(m => m.ResRef == utcResRef))
            {
                Debug.LogWarning($"[PartyManager] '{utcResRef}' already recruited.");
                return;
            }

            byte[] utcData = SceneBootstrapper.Resources?.GetResource(utcResRef,
                KotORUnity.KotOR.FileReaders.ResourceType.UTC);
            var utc = utcData != null ? KotORUnity.KotOR.Parsers.GffReader.Parse(utcData) : null;

            uint nameRef  = (uint)KotORUnity.KotOR.Parsers.GffReader.GetInt(utc, "FirstName", 0);
            string name   = SceneBootstrapper.GetString(nameRef);
            if (string.IsNullOrEmpty(name)) name = utcResRef;

            int maxHP  = KotORUnity.KotOR.Parsers.GffReader.GetInt(utc, "MaxHitPoints",     10);
            int currHP = KotORUnity.KotOR.Parsers.GffReader.GetInt(utc, "CurrentHitPoints", maxHP);

            var member = new PartyMemberData
            {
                ResRef      = utcResRef,
                Tag         = KotORUnity.KotOR.Parsers.GffReader.GetString(utc, "Tag", utcResRef),
                DisplayName = name,
                IsPlayer    = false,
                IsActive    = ActiveCount() < maxActive,
                MaxHP       = maxHP,
                CurrentHP   = currHP
            };

            _roster.Add(member);
            Debug.Log($"[PartyManager] Recruited: {name} (active={member.IsActive})");

            if (member.IsActive) SpawnCompanion(member);
        }

        /// <summary>Activate a companion into the scene (swap in).</summary>
        public void Activate(string resref)
        {
            if (ActiveCount() >= maxActive)
            {
                Debug.LogWarning("[PartyManager] Party full — deactivate someone first.");
                return;
            }
            var m = _roster.Find(x => x.ResRef == resref);
            if (m == null) return;
            m.IsActive = true;
            SpawnCompanion(m);
            SyncSquadWithRTSController();
        }

        /// <summary>Deactivate a companion from the scene (swap out).</summary>
        public void Deactivate(string resref)
        {
            var m = _roster.Find(x => x.ResRef == resref);
            if (m == null || m.IsPlayer) return;
            m.IsActive = false;
            DespawnCompanion(resref);
            SyncSquadWithRTSController();
        }

        public void SetPlayerTransform(Transform t) => _playerTransform = t;

        /// <summary>
        /// Notify RTSPlayerController of a change in active companions
        /// so the squad list stays in sync.
        /// </summary>
        private void SyncSquadWithRTSController()
        {
            var rtsCon = UnityEngine.Object.FindObjectOfType<Player.RTSPlayerController>();
            if (rtsCon == null) return;

            // Deregister all companions and re-register active ones
            var companions = UnityEngine.Object.FindObjectsOfType<AI.Companion.CompanionAI>();
            foreach (var c in companions)
                rtsCon.RemoveCompanion(c);

            var activeRefs = new HashSet<string>(GetActivePartyIds());
            foreach (var c in companions)
            {
                var kcd = c.GetComponent<KotorCreatureData>();
                if (kcd != null && activeRefs.Contains(kcd.TemplateRef))
                    rtsCon.RegisterCompanion(c);
            }

            EventBus.Publish(EventBus.EventType.FormationChanged);
        }

        // ── FORMATION POSITIONS ───────────────────────────────────────────────
        /// <summary>Get the world position a companion should move to in formation.</summary>
        public Vector3 GetFormationPosition(int companionIndex)
        {
            if (_playerTransform == null) return Vector3.zero;

            // Simple diamond / wedge formation behind player
            float angle = 150f + (companionIndex % 2 == 0 ? -1 : 1) * 25f * ((companionIndex / 2) + 1);
            var dir = Quaternion.Euler(0, angle, 0) * _playerTransform.forward;
            return _playerTransform.position + dir * followDistance
                   + _playerTransform.right * (companionIndex % 2 == 0 ? -1 : 1) * formationSpread * 0.5f;
        }

        // ── SERIALIZATION ─────────────────────────────────────────────────────
        public List<PartyMemberData> GetSaveData() => new List<PartyMemberData>(_roster);

        /// <summary>Return all roster member IDs (resrefs) for save system.</summary>
        public string[] GetRosterIds() => _roster.ConvertAll(m => m.ResRef).ToArray();

        /// <summary>Return active-party member IDs for save system.</summary>
        public string[] GetActivePartyIds() =>
            _roster.FindAll(m => m.IsActive && !m.IsPlayer).ConvertAll(m => m.ResRef).ToArray();

        /// <summary>Restore party from saved resref arrays.</summary>
        public void RestoreFromSave(string[] rosterIds, string[] activeIds)
        {
            _roster.Clear();
            if (rosterIds == null) return;
            foreach (var id in rosterIds)
                Recruit(id);
            // Activate specified companions
            if (activeIds != null)
                foreach (var id in activeIds)
                {
                    var m = _roster.Find(x => x.ResRef == id);
                    if (m != null) m.IsActive = true;
                }
        }

        public void LoadSaveData(List<PartyMemberData> data)
        {
            _roster.Clear();
            if (data == null) return;
            foreach (var m in data)
            {
                _roster.Add(m);
                if (m.IsActive && !m.IsPlayer) SpawnCompanion(m);
            }
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────
        private int ActiveCount() => _roster.FindAll(m => m.IsActive).Count;

        private void SpawnCompanion(PartyMemberData member)
        {
            if (companionPrefab == null) return;

            Vector3 spawnPos = _playerTransform != null
                ? _playerTransform.position + _playerTransform.right * 2f
                : Vector3.zero;

            var go = Instantiate(companionPrefab, spawnPos, Quaternion.identity);
            go.name = member.DisplayName;

            var data = go.GetComponent<KotorCreatureData>() ?? go.AddComponent<KotorCreatureData>();
            // data is initialised externally by CreatureSpawner; here we just tag the GO
            go.tag = "Companion";

            var nav = go.GetComponent<NavMeshAgent>();
            if (nav != null) nav.enabled = true;
        }

        private void DespawnCompanion(string resref)
        {
            var companions = FindObjectsOfType<KotorCreatureData>();
            foreach (var c in companions)
            {
                if (c.TemplateRef == resref)
                { Destroy(c.gameObject); return; }
            }
        }
    }
}
