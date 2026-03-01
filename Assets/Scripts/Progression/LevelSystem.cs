using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Progression
{
    /// <summary>
    /// Manages experience gain, level-up, talent trees, and mode affinity bonuses.
    /// 
    /// XP formula: xpToNextLevel = 1000 × level
    /// 
    /// Mode Affinity System (from design doc):
    ///   If the player hasn't used mode X in the last 3 encounters,
    ///   using mode X grants +5% XP bonus to encourage trying the under-used mode.
    /// </summary>
    public class LevelSystem : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [SerializeField] private PlayerStatsBehaviour playerStatsBehaviour;
        [SerializeField] private TalentTree talentTree;

        // ── STATE ──────────────────────────────────────────────────────────────
        private ModeSwitchSystem _modeSwitchSystem;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
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

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnEnemyKilled(EventBus.GameEventArgs args)
        {
            if (args is EventBus.DamageEventArgs damageArgs)
            {
                // XP scales with enemy level
                var enemyStats = damageArgs.Target?.GetComponent<PlayerStatsBehaviour>()?.Stats;
                float xpReward = 50f + (enemyStats?.Level ?? 1) * 25f;

                // Boss bonus
                var enemyAI = damageArgs.Target?.GetComponent<AI.Enemy.EnemyAI>();
                if (enemyAI?.EnemyType == EnemyType.Boss) xpReward *= 5f;
                else if (enemyAI?.EnemyType == EnemyType.Elite) xpReward *= 2f;

                AwardXP(xpReward, $"Killed {damageArgs.Target?.name}");
            }
        }

        private void OnEncounterCompleted(EventBus.GameEventArgs args)
        {
            // Flat XP for completing an encounter
            AwardXP(100f, "Encounter Completed");
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
