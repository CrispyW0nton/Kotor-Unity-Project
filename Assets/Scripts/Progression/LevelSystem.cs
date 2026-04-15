using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Progression
{
    // ══════════════════════════════════════════════════════════════════════════
    //  KOTOR CLASS DEFINITIONS
    //  Source: KotOR 1 cls_*.2da tables and the system layout PDF
    // ══════════════════════════════════════════════════════════════════════════

    public enum KotORClass
    {
        Soldier    = 0,    // d10 HP, high BAB, bonus feats every 3 levels
        Scout      = 1,    // d8  HP, medium BAB, tech skills + saves
        Scoundrel  = 2,    // d6  HP, low BAB, high skills + sneak attack
        Jedi_Guardian  = 3,  // d10 HP, high BAB, Force Points = 15+CON×5
        Jedi_Consular  = 4,  // d6  HP, low BAB,  Force Points = 30+CON×7
        Jedi_Sentinel  = 5,  // d8  HP, med BAB,  Force Points = 22+CON×6
    }

    [Serializable]
    public class KotORClassData
    {
        public KotORClass Class;
        public string     Name;
        public int        HitDie;               // d6/d8/d10
        public int        BaseHP;               // starting HP before CON
        public float      BabProgression;        // 1.0 = full, 0.75 = 3/4, 0.5 = 1/2
        public int        SkillPointsPerLevel;
        public bool       IsJediClass;
        public int        FpBase;               // Force Points base (Jedi only)
        public int        FpConMultiplier;      // FP += CON × this
        public int[]      FeatLevelSchedule;    // levels at which bonus feats are granted
        public int[]      ForcePowerLevels;     // levels at which new Force Powers are gained

        // ── XP table (matches KotOR 1 experience.2da) ───────────────────────
        // Level → cumulative XP required.  Levels 1-20.
        public static readonly int[] XPTable = new int[]
        {
            0,       // level 1  (no XP needed)
            1000,    // level 2
            3000,    // level 3
            6000,    // level 4
            10000,   // level 5
            15000,   // level 6
            21000,   // level 7
            28000,   // level 8
            36000,   // level 9
            45000,   // level 10
            55000,   // level 11
            66000,   // level 12
            78000,   // level 13
            91000,   // level 14
            105000,  // level 15
            120000,  // level 16
            136000,  // level 17
            153000,  // level 18
            171000,  // level 19
            190000,  // level 20
        };

        // ── Saving throw tables (Fort/Reflex/Will per level) ─────────────────
        // Source: cls_savthr_*.2da
        public static int GetBaseSave(KotORClass cls, SaveType save, int level)
        {
            // Good progression: 2 + level/2.  Poor progression: level/3
            bool good = (cls, save) switch
            {
                (KotORClass.Soldier,       SaveType.Fortitude) => true,
                (KotORClass.Scout,         SaveType.Reflex)    => true,
                (KotORClass.Scoundrel,     SaveType.Reflex)    => true,
                (KotORClass.Jedi_Guardian, SaveType.Fortitude) => true,
                (KotORClass.Jedi_Consular, SaveType.Will)      => true,
                (KotORClass.Jedi_Sentinel, SaveType.Reflex)    => true,
                (KotORClass.Jedi_Sentinel, SaveType.Will)      => true,
                _ => false
            };
            return good ? 2 + level / 2 : level / 3;
        }

        public static KotORClassData Get(KotORClass cls) => _table[(int)cls];

        private static readonly KotORClassData[] _table = new KotORClassData[]
        {
            new KotORClassData { Class = KotORClass.Soldier, Name = "Soldier",
                HitDie = 10, BaseHP = 10, BabProgression = 1.0f, SkillPointsPerLevel = 2,
                IsJediClass = false, FpBase = 0, FpConMultiplier = 0,
                FeatLevelSchedule = new[]{ 1, 2, 3, 5, 7, 9, 11, 13, 15, 17, 19 },
                ForcePowerLevels  = new int[0] },
            new KotORClassData { Class = KotORClass.Scout, Name = "Scout",
                HitDie = 8, BaseHP = 8, BabProgression = 0.75f, SkillPointsPerLevel = 6,
                IsJediClass = false, FpBase = 0, FpConMultiplier = 0,
                FeatLevelSchedule = new[]{ 1, 3, 6, 9, 12, 15, 18 },
                ForcePowerLevels  = new int[0] },
            new KotORClassData { Class = KotORClass.Scoundrel, Name = "Scoundrel",
                HitDie = 6, BaseHP = 6, BabProgression = 0.5f, SkillPointsPerLevel = 8,
                IsJediClass = false, FpBase = 0, FpConMultiplier = 0,
                FeatLevelSchedule = new[]{ 1, 4, 8, 12, 16, 20 },
                ForcePowerLevels  = new int[0] },
            new KotORClassData { Class = KotORClass.Jedi_Guardian, Name = "Jedi Guardian",
                HitDie = 10, BaseHP = 10, BabProgression = 1.0f, SkillPointsPerLevel = 2,
                IsJediClass = true, FpBase = 15, FpConMultiplier = 5,
                FeatLevelSchedule = new[]{ 1, 2, 4, 6, 8, 10, 12, 14, 16, 18, 20 },
                ForcePowerLevels  = new[]{ 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 } },
            new KotORClassData { Class = KotORClass.Jedi_Consular, Name = "Jedi Consular",
                HitDie = 6, BaseHP = 6, BabProgression = 0.5f, SkillPointsPerLevel = 4,
                IsJediClass = true, FpBase = 30, FpConMultiplier = 7,
                FeatLevelSchedule = new[]{ 1, 5, 10, 15, 20 },
                ForcePowerLevels  = new[]{ 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 } },
            new KotORClassData { Class = KotORClass.Jedi_Sentinel, Name = "Jedi Sentinel",
                HitDie = 8, BaseHP = 8, BabProgression = 0.75f, SkillPointsPerLevel = 6,
                IsJediClass = true, FpBase = 22, FpConMultiplier = 6,
                FeatLevelSchedule = new[]{ 1, 3, 6, 9, 12, 15, 18 },
                ForcePowerLevels  = new[]{ 1, 3, 5, 7, 9, 11, 13, 15, 17, 19 } },
        };
    }

    public enum SaveType { Fortitude, Reflex, Will }

    /// <summary>
    /// Manages experience gain, level-up, class progression, feats, and mode affinity bonuses.
    ///
    /// XP table: matches KotOR 1 experience.2da exactly.
    ///
    /// Mode Affinity System (from design doc):
    ///   If the player hasn't used mode X in the last 3 encounters,
    ///   using mode X grants +5% XP bonus to encourage trying the under-used mode.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static LevelSystem Instance { get; private set; }

        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [SerializeField] private PlayerStatsBehaviour playerStatsBehaviour;
        [SerializeField] private TalentTree talentTree;
        [SerializeField] private KotORClass playerClass = KotORClass.Scoundrel;

        // ── STATE ──────────────────────────────────────────────────────────────
        private ModeSwitchSystem _modeSwitchSystem;
        public int CurrentXP => (int)(playerStatsBehaviour?.Stats?.Experience ?? 0f);
        public KotORClass PlayerClass => playerClass;

        /// <summary>XP required to reach a specific level (1-based, uses exact KotOR table).</summary>
        public static int GetXPForLevel(int level)
        {
            int idx = Mathf.Clamp(level - 1, 0, KotORClassData.XPTable.Length - 1);
            return KotORClassData.XPTable[idx];
        }

        /// <summary>Calculate the maximum Force Points for a class at the given level and CON score.</summary>
        public static int GetMaxFP(KotORClass cls, int level, int constitution)
        {
            var data = KotORClassData.Get(cls);
            if (!data.IsJediClass) return 0;
            int conMod = (constitution - 10) / 2;
            return data.FpBase + level * (data.FpConMultiplier + conMod);
        }

        /// <summary>Whether the player earns a feat/power choice at this level.</summary>
        public bool IsFeatureLevel(int level)
        {
            var data = KotORClassData.Get(playerClass);
            return System.Array.IndexOf(data.FeatLevelSchedule, level) >= 0;
        }

        /// <summary>Whether the player earns a Force Power choice at this level.</summary>
        public bool IsForcePowerLevel(int level)
        {
            var data = KotORClassData.Get(playerClass);
            return System.Array.IndexOf(data.ForcePowerLevels, level) >= 0;
        }

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
            _modeSwitchSystem = FindObjectOfType<ModeSwitchSystem>();
            EventBus.Subscribe(EventBus.EventType.EntityKilled, OnEnemyKilled);
            EventBus.Subscribe(EventBus.EventType.EncounterCompleted, OnEncounterCompleted);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.EntityKilled, OnEnemyKilled);
            EventBus.Unsubscribe(EventBus.EventType.EncounterCompleted, OnEncounterCompleted);
        }

        // ── XP GRANTING ───────────────────────────────────────────────────────
        /// <summary>
        /// Initialise the player character from a <see cref="UI.NewGameConfig"/> produced
        /// by the Character Creation screen.  Sets class, applies base stats, and resets XP.
        /// </summary>
        public void InitialiseFromConfig(UI.NewGameConfig config)
        {
            if (config == null) return;

            // Map ClassId → KotORClass enum
            playerClass = config.ClassId switch
            {
                0 => KotORClass.Soldier,
                1 => KotORClass.Scout,
                2 => KotORClass.Scoundrel,
                _ => KotORClass.Scoundrel
            };

            // Push attributes into PlayerStats
            if (playerStatsBehaviour != null)
            {
                var stats = playerStatsBehaviour.Stats;
                if (stats != null)
                {
                    stats.SetCharacterName(config.PlayerName);
                    var a = config.Attributes;
                    stats.SetAbilityScores(a.Strength, a.Dexterity, a.Constitution,
                                           a.Intelligence, a.Wisdom, a.Charisma);
                    stats.SetLevel(1);
                }
            }

            Debug.Log($"[LevelSystem] Initialised from config — class={playerClass} name={config.PlayerName}");
        }

        /// <summary>
        /// Award experience to the player.
        /// Applies mode affinity bonus if active (+5% for under-used mode).
        /// </summary>
        public void AwardXP(float baseAmount, string source = "Combat")
        {
            if (playerStatsBehaviour == null) return;

            bool affinityBonus = false;
            float amount = baseAmount;

            // Check mode affinity bonus
            GameMode currentMode = _modeSwitchSystem?.CurrentMode ?? GameMode.Action;
            if (_modeSwitchSystem != null && _modeSwitchSystem.IsAffinityBonusActive(currentMode))
            {
                amount *= (1f + GameConstants.MODE_AFFINITY_XP_BONUS);
                affinityBonus = true;

                EventBus.Publish(EventBus.EventType.ModeAffinityBonusTriggered);
                Debug.Log($"[LevelSystem] Mode Affinity Bonus active! +5% XP ({source})");
            }

            playerStatsBehaviour.Stats.AddExperience(amount);

            EventBus.Publish(EventBus.EventType.ExperienceGained,
                new EventBus.ExperienceEventArgs(amount, source, affinityBonus));
        }

        /// <summary>
        /// Directly set the player's XP (used by NWScript SetXP command).
        /// If the new value triggers a level-up threshold, processes it.
        /// </summary>
        public void SetXP(int xp)
        {
            if (playerStatsBehaviour?.Stats == null) return;
            float current = playerStatsBehaviour.Stats.Experience;
            float delta = xp - current;
            if (delta > 0)
                AwardXP(delta, "Script:SetXP");
            else if (delta < 0)
                playerStatsBehaviour.Stats.SetExperience(xp);
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnEnemyKilled(EventBus.GameEventArgs args)
        {
            if (args is EventBus.DamageEventArgs damageArgs)
            {
                // XP reward: (killer level + target level) / 2 × 100
                // Matches the KotOR 1 challenge rating formula (simplified)
                int playerLevel  = playerStatsBehaviour?.Stats?.Level ?? 1;
                var enemyStats   = damageArgs.Target?.GetComponent<PlayerStatsBehaviour>()?.Stats;
                int enemyLevel   = enemyStats?.Level ?? 1;

                float xpReward = ((playerLevel + enemyLevel) / 2f) * 100f;

                // Boss/Elite multipliers
                var enemyAI = damageArgs.Target?.GetComponent<AI.Enemy.EnemyAI>();
                if (enemyAI?.EnemyType == EnemyType.Boss)  xpReward *= 5f;
                else if (enemyAI?.EnemyType == EnemyType.Elite) xpReward *= 2f;

                // Level delta penalty: killing trivial enemies gives less XP
                int delta = playerLevel - enemyLevel;
                if (delta >= 5)  xpReward *= 0.25f;
                else if (delta >= 3) xpReward *= 0.5f;

                AwardXP(xpReward, $"Killed {damageArgs.Target?.name}");

                // Process level-up milestones using KotOR XP table
                CheckLevelUpMilestones();
            }
        }

        private void OnEncounterCompleted(EventBus.GameEventArgs args)
        {
            // Flat XP for completing an encounter (matches KotOR design)
            AwardXP(200f, "Encounter Completed");
            CheckLevelUpMilestones();
        }

        /// <summary>
        /// After XP has been added, check whether the player has crossed
        /// a level threshold and fire feat/Force Power notification events.
        /// </summary>
        private void CheckLevelUpMilestones()
        {
            if (playerStatsBehaviour?.Stats == null) return;
            int level = playerStatsBehaviour.Stats.Level;

            if (IsFeatureLevel(level))
                Debug.Log($"[LevelSystem] Level {level}: bonus feat available ({KotORClassData.Get(playerClass).Name})");

            if (IsForcePowerLevel(level))
                Debug.Log($"[LevelSystem] Level {level}: Force Power slot available");
        }
    }

    // ── TALENT NODE ───────────────────────────────────────────────────────────
    [Serializable]
    public class TalentNode
    {
        public string id;
        public string displayName;
        public string description;
        public int maxRanks = 1;
        public int currentRanks = 0;
        public float damageBonus = 0f;           // Added to TalentDamageMultiplier
        public float actionModeBonus = 0f;       // Bonus specifically in Action mode
        public float rtsModeBonus = 0f;          // Bonus specifically in RTS mode
        public List<string> prerequisites = new List<string>();
        public bool IsUnlocked => currentRanks > 0;
        public bool IsMaxed => currentRanks >= maxRanks;
        public bool CanUnlock(Dictionary<string, TalentNode> allNodes)
        {
            foreach (var prereqId in prerequisites)
            {
                if (!allNodes.ContainsKey(prereqId) || !allNodes[prereqId].IsUnlocked)
                    return false;
            }
            return !IsMaxed;
        }
    }

    // ── TALENT TREE ───────────────────────────────────────────────────────────
    /// <summary>
    /// Character talent tree. Each character has one tree with 8+ nodes.
    /// Nodes must synergize with BOTH combat modes (design doc requirement).
    /// 
    /// Example tree structure (Soldier):
    ///   Tier 1: Marksman I, Tactical Eye I
    ///   Tier 2: Marksman II (req: Marksman I), RTS Coordination I (req: Tactical Eye I)
    ///   Tier 3: Overwatch Mastery (req: Marksman II + RTS Coordination I) — hybrid ability unlock
    /// </summary>
    public class TalentTree : MonoBehaviour
    {
        [SerializeField] private string treeName = "Soldier";
        [SerializeField] private PlayerStatsBehaviour playerStatsBehaviour;

        private Dictionary<string, TalentNode> _nodes = new Dictionary<string, TalentNode>();

        private void Awake()
        {
            InitializeSoldierTree();
        }

        private void InitializeSoldierTree()
        {
            _nodes.Clear();

            // Tier 1
            AddNode(new TalentNode
            {
                id = "marksman_1",
                displayName = "Marksman I",
                description = "+10% ADS accuracy in Action mode, +5% RTS hit chance",
                maxRanks = 1,
                actionModeBonus = 0.10f,
                rtsModeBonus = 0.05f,
                damageBonus = 0.05f
            });

            AddNode(new TalentNode
            {
                id = "tactical_eye_1",
                displayName = "Tactical Eye I",
                description = "+8% squad DPS in RTS mode, unlock hit chance overlay",
                maxRanks = 1,
                rtsModeBonus = 0.08f,
                actionModeBonus = 0.03f,
                damageBonus = 0.03f
            });

            // Tier 2
            AddNode(new TalentNode
            {
                id = "marksman_2",
                displayName = "Marksman II",
                description = "+15% headshot damage in Action. Sniper Rifle DPS_RTS +10%",
                maxRanks = 1,
                actionModeBonus = 0.15f,
                rtsModeBonus = 0.10f,
                damageBonus = 0.08f,
                prerequisites = new List<string> { "marksman_1" }
            });

            AddNode(new TalentNode
            {
                id = "rts_coordination_1",
                displayName = "RTS Coordination I",
                description = "+12% companion AI efficiency in Action mode (from 60% to 67%)",
                maxRanks = 1,
                rtsModeBonus = 0.12f,
                actionModeBonus = 0.05f,
                damageBonus = 0.04f,
                prerequisites = new List<string> { "tactical_eye_1" }
            });

            // Tier 3
            AddNode(new TalentNode
            {
                id = "overwatch_mastery",
                displayName = "Overwatch Mastery",
                description = "Unlock Overwatch Protocol hybrid ability. +25% crit after RTS mark.",
                maxRanks = 1,
                actionModeBonus = 0.25f,
                rtsModeBonus = 0.15f,
                damageBonus = 0.10f,
                prerequisites = new List<string> { "marksman_2", "rts_coordination_1" }
            });

            // Additional nodes
            AddNode(new TalentNode
            {
                id = "tactical_scope_1",
                displayName = "Tactical Scope",
                description = "+20% damage in RTS, +zoom stability in Action",
                maxRanks = 2,
                rtsModeBonus = 0.20f,
                actionModeBonus = 0.05f,
                damageBonus = 0.06f,
                prerequisites = new List<string> { "marksman_1" }
            });

            AddNode(new TalentNode
            {
                id = "adrenaline_1",
                displayName = "Adrenaline Rush I",
                description = "Action mode builds Adrenaline resource for powerful finishers",
                maxRanks = 1,
                actionModeBonus = 0.12f,
                rtsModeBonus = 0f,
                damageBonus = 0.07f,
                prerequisites = new List<string> { "marksman_1" }
            });
        }

        private void AddNode(TalentNode node) => _nodes[node.id] = node;

        public bool TryUnlockNode(string nodeId)
        {
            if (!_nodes.ContainsKey(nodeId)) return false;
            var node = _nodes[nodeId];
            if (!node.CanUnlock(_nodes)) return false;

            node.currentRanks++;

            // Apply bonuses to player stats
            playerStatsBehaviour?.Stats.AddTalentDamageBonus(node.damageBonus);

            EventBus.Publish(EventBus.EventType.TalentUnlocked);
            Debug.Log($"[TalentTree] Unlocked: {node.displayName}");
            return true;
        }

        public Dictionary<string, TalentNode> GetAllNodes() => _nodes;
        public TalentNode GetNode(string id) => _nodes.ContainsKey(id) ? _nodes[id] : null;
        public string TreeName => treeName;
    }
}
