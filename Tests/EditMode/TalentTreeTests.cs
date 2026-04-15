using System.Collections.Generic;
using NUnit.Framework;
using KotORUnity.Core;
using KotORUnity.Progression;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for the TalentTree system — node existence, prerequisite gating,
    /// rank limits, and damage bonus application.
    /// 
    /// TalentTree is a MonoBehaviour, so we test it in isolation by constructing
    /// a minimal TalentNode graph directly.
    /// </summary>
    public class TalentTreeTests
    {
        // ── HELPERS ────────────────────────────────────────────────────────────
        private Dictionary<string, TalentNode> BuildSoldierTree()
        {
            var nodes = new Dictionary<string, TalentNode>();

            nodes["marksman_1"] = new TalentNode
            {
                id = "marksman_1",
                displayName = "Marksman I",
                maxRanks = 1,
                damageBonus = 0.05f,
                prerequisites = new List<string>()
            };

            nodes["tactical_eye_1"] = new TalentNode
            {
                id = "tactical_eye_1",
                displayName = "Tactical Eye I",
                maxRanks = 1,
                damageBonus = 0.03f,
                prerequisites = new List<string>()
            };

            nodes["marksman_2"] = new TalentNode
            {
                id = "marksman_2",
                displayName = "Marksman II",
                maxRanks = 1,
                damageBonus = 0.08f,
                prerequisites = new List<string> { "marksman_1" }
            };

            nodes["rts_coordination_1"] = new TalentNode
            {
                id = "rts_coordination_1",
                displayName = "RTS Coordination I",
                maxRanks = 1,
                damageBonus = 0.04f,
                prerequisites = new List<string> { "tactical_eye_1" }
            };

            nodes["overwatch_mastery"] = new TalentNode
            {
                id = "overwatch_mastery",
                displayName = "Overwatch Mastery",
                maxRanks = 1,
                damageBonus = 0.10f,
                prerequisites = new List<string> { "marksman_2", "rts_coordination_1" }
            };

            return nodes;
        }

        // ── PREREQUISITE GATING ────────────────────────────────────────────────
        [Test]
        public void TierOneNodes_CanBeUnlocked_WithNoPrerequisites()
        {
            var nodes = BuildSoldierTree();
            Assert.IsTrue(nodes["marksman_1"].CanUnlock(nodes));
            Assert.IsTrue(nodes["tactical_eye_1"].CanUnlock(nodes));
        }

        [Test]
        public void Tier2Node_CannotUnlock_IfPrerequisiteNotMet()
        {
            var nodes = BuildSoldierTree();
            // marksman_2 requires marksman_1 — which is not yet unlocked
            Assert.IsFalse(nodes["marksman_2"].CanUnlock(nodes));
        }

        [Test]
        public void Tier2Node_CanUnlock_AfterPrerequisiteUnlocked()
        {
            var nodes = BuildSoldierTree();
            nodes["marksman_1"].currentRanks = 1; // unlock the prerequisite
            Assert.IsTrue(nodes["marksman_2"].CanUnlock(nodes));
        }

        [Test]
        public void OverwatchMastery_RequiresBothTier2Nodes()
        {
            var nodes = BuildSoldierTree();

            // Only unlock marksman path — rts_coordination_1 not unlocked
            nodes["marksman_1"].currentRanks = 1;
            nodes["marksman_2"].currentRanks = 1;
            Assert.IsFalse(nodes["overwatch_mastery"].CanUnlock(nodes));

            // Now unlock the other path
            nodes["tactical_eye_1"].currentRanks = 1;
            nodes["rts_coordination_1"].currentRanks = 1;
            Assert.IsTrue(nodes["overwatch_mastery"].CanUnlock(nodes));
        }

        // ── RANK LIMITS ────────────────────────────────────────────────────────
        [Test]
        public void Node_CannotExceedMaxRanks()
        {
            var nodes = BuildSoldierTree();
            nodes["marksman_1"].currentRanks = 1; // At max (maxRanks=1)
            Assert.IsFalse(nodes["marksman_1"].CanUnlock(nodes)); // already maxed
        }

        [Test]
        public void MultiRankNode_CanBeUnlocked_UntilMax()
        {
            var node = new TalentNode
            {
                id = "multi",
                maxRanks = 3,
                currentRanks = 0,
                prerequisites = new List<string>()
            };
            var nodes = new Dictionary<string, TalentNode> { ["multi"] = node };

            Assert.IsTrue(node.CanUnlock(nodes));
            node.currentRanks++;
            Assert.IsTrue(node.CanUnlock(nodes));
            node.currentRanks++;
            Assert.IsTrue(node.CanUnlock(nodes));
            node.currentRanks++;
            Assert.IsFalse(node.CanUnlock(nodes)); // maxed at 3
        }

        // ── DAMAGE BONUS ACCUMULATION ──────────────────────────────────────────
        [Test]
        public void UnlockingMultipleNodes_AccumulatesDamageBonuses()
        {
            var stats = new PlayerStats("Test", 1);
            var nodes = BuildSoldierTree();

            // Simulate unlocking tier 1 nodes
            nodes["marksman_1"].currentRanks = 1;
            stats.AddTalentDamageBonus(nodes["marksman_1"].damageBonus);

            nodes["tactical_eye_1"].currentRanks = 1;
            stats.AddTalentDamageBonus(nodes["tactical_eye_1"].damageBonus);

            // Expected: 0.05 + 0.03 = 0.08
            Assert.AreEqual(0.08f, stats.TalentDamageMultiplier, 0.001f);
        }

        [Test]
        public void TalentDamageMultiplier_CappedAt50Percent()
        {
            var stats = new PlayerStats("Test", 1);
            // Try to add well over 50%
            stats.AddTalentDamageBonus(2.0f);
            Assert.AreEqual(GameConstants.MAX_TALENT_DAMAGE_MULTIPLIER,
                            stats.TalentDamageMultiplier, 0.001f);
        }

        // ── XP FORMULA ────────────────────────────────────────────────────────
        [Test]
        public void XP_ToNextLevel_Scales_With_Level()
        {
            var statsL1 = new PlayerStats("Test", 1);
            var statsL5 = new PlayerStats("Test", 5);
            Assert.Less(statsL1.XpToNextLevel, statsL5.XpToNextLevel);
        }
    }
}
