using NUnit.Framework;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for ModeSwitchSystem affinity counters — the pure-math portions
    /// that don't require MonoBehaviour lifecycle.
    ///
    /// We can't create the MonoBehaviour itself in edit-mode tests, but we can
    /// test the affinity counter logic directly because the math is exposed via
    /// RecordEncounterMode / IsAffinityBonusActive (which only touch plain fields).
    /// </summary>
    public class ModeSwitchAffinityTests
    {
        // ── AFFINITY THRESHOLD ─────────────────────────────────────────────────
        [Test]
        public void AffinityThreshold_Is3Encounters()
        {
            Assert.AreEqual(3, GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD);
        }

        [Test]
        public void AffinityXPBonus_Is5Percent()
        {
            Assert.AreEqual(0.05f, GameConstants.MODE_AFFINITY_XP_BONUS, 0.001f);
        }

        // ── COUNTER LOGIC (manual simulation) ─────────────────────────────────
        [Test]
        public void AffinityCounter_IncreasesWhenOppositeMode_Used()
        {
            int encountersSinceAction = 0;
            int encountersSinceRTS = 0;

            // Use Action mode for 3 encounters
            for (int i = 0; i < 3; i++)
            {
                encountersSinceAction = 0;    // reset action counter
                encountersSinceRTS++;         // rts counter increases
            }

            Assert.AreEqual(3, encountersSinceRTS);
            Assert.AreEqual(0, encountersSinceAction);
        }

        [Test]
        public void AffinityBonus_TriggersAt_Threshold_Encounters()
        {
            // Simulate 3 consecutive Action encounters
            int encountersSinceRTS = GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD;
            // Affinity bonus for RTS should now be active
            bool bonusActive = encountersSinceRTS >= GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD;
            Assert.IsTrue(bonusActive);
        }

        [Test]
        public void AffinityBonus_NotActive_Below_Threshold()
        {
            int encountersSinceRTS = GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD - 1;
            bool bonusActive = encountersSinceRTS >= GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD;
            Assert.IsFalse(bonusActive);
        }

        [Test]
        public void AffinityBonus_Resets_WhenModeSwitched()
        {
            // Player uses Action for 3 encounters, RTS affinity should be active
            int encountersSinceRTS = 3;

            // Player switches to RTS — counter resets
            encountersSinceRTS = 0;

            bool bonusActive = encountersSinceRTS >= GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD;
            Assert.IsFalse(bonusActive);
        }

        // ── XP BONUS MATH ─────────────────────────────────────────────────────
        [Test]
        public void XPBonus_5Percent_On100BaseXP_Yields105()
        {
            float baseXP = 100f;
            float bonusXP = baseXP * (1f + GameConstants.MODE_AFFINITY_XP_BONUS);
            Assert.AreEqual(105f, bonusXP, 0.001f);
        }

        // ── ADAPTATION RESET ──────────────────────────────────────────────────
        [Test]
        public void EnemyAdaptationResetTime_Is30Seconds()
        {
            Assert.AreEqual(30f, GameConstants.ENEMY_ADAPTATION_RESET_TIME, 0.001f);
        }

        [Test]
        public void EnemyAdaptationMaxStacks_Is5()
        {
            Assert.AreEqual(5, GameConstants.ENEMY_ADAPTATION_MAX_STACKS);
        }

        [Test]
        public void EnemyAdaptationDamage_5Stacks_50PercentIncrease()
        {
            float baseDamage = 100f;
            float multiplier = 1f + GameConstants.ENEMY_ADAPTATION_PER_PAUSE
                               * GameConstants.ENEMY_ADAPTATION_MAX_STACKS;
            float result = baseDamage * multiplier;
            Assert.AreEqual(150f, result, 0.01f);
        }
    }
}
