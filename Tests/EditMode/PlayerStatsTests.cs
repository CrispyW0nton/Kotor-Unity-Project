using NUnit.Framework;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Unit tests for PlayerStats — level scaling, health, damage formulas.
    /// </summary>
    public class PlayerStatsTests
    {
        [Test]
        public void PlayerStats_Level1_HasCorrectHP()
        {
            var stats = new PlayerStats("TestChar", 1);
            Assert.AreEqual(125f, stats.MaxHealth, 0.01f);
        }

        [Test]
        public void PlayerStats_Level10_HasCorrectHP()
        {
            var stats = new PlayerStats("TestChar", 10);
            float expected = 100f + 25f * 10;
            Assert.AreEqual(expected, stats.MaxHealth, 0.01f);
        }

        [Test]
        public void PlayerStats_TakeDamage_ReducesHealth()
        {
            var stats = new PlayerStats("TestChar", 1);
            stats.TakeDamage(30f);
            Assert.Less(stats.CurrentHealth, stats.MaxHealth);
        }

        [Test]
        public void PlayerStats_ShieldAbsorbsDamageFirst()
        {
            var stats = new PlayerStats("TestChar", 1);
            float initialShield = stats.MaxShield;
            stats.TakeDamage(50f);
            Assert.Less(stats.CurrentShield, initialShield);
            Assert.AreEqual(stats.MaxHealth, stats.CurrentHealth, 0.01f); // health unchanged if shield absorbed all
        }

        [Test]
        public void PlayerStats_Heal_CapsAtMaxHealth()
        {
            var stats = new PlayerStats("TestChar", 1);
            stats.TakeDamage(200f); // kill shield + reduce health
            stats.Heal(10000f);     // overheal
            Assert.AreEqual(stats.MaxHealth, stats.CurrentHealth, 0.01f);
        }

        [Test]
        public void PlayerStats_LevelUp_OnExperienceThreshold()
        {
            var stats = new PlayerStats("TestChar", 1);
            int levelUpFired = 0;
            stats.OnLevelUp += _ => levelUpFired++;

            stats.AddExperience(1000f); // level 1 threshold = 1000
            Assert.AreEqual(1, levelUpFired);
            Assert.AreEqual(2, stats.Level);
        }

        [Test]
        public void PlayerStats_ScaledAbilityDamage_IncreaseWithLevel()
        {
            var statsL1 = new PlayerStats("TestChar", 1);
            var statsL10 = new PlayerStats("TestChar", 10);

            float baseDamage = 30f;
            float damageL1 = statsL1.GetScaledAbilityDamage(baseDamage);
            float damageL10 = statsL10.GetScaledAbilityDamage(baseDamage);

            Assert.Greater(damageL10, damageL1);
        }

        [Test]
        public void PlayerStats_RTSUsageRatio_IsZeroWithNoData()
        {
            var stats = new PlayerStats("TestChar", 1);
            Assert.AreEqual(0f, stats.RTSUsageRatio, 0.001f);
        }

        [Test]
        public void PlayerStats_RTSUsageRatio_CalculatesCorrectly()
        {
            var stats = new PlayerStats("TestChar", 1);
            stats.RecordCombatTime(GameMode.RTS, 60f);
            stats.RecordCombatTime(GameMode.Action, 60f);
            Assert.AreEqual(0.5f, stats.RTSUsageRatio, 0.001f);
        }

        [Test]
        public void PlayerStats_Die_SetsHealthToZero()
        {
            var stats = new PlayerStats("TestChar", 1);
            bool diedFired = false;
            stats.OnDied += () => diedFired = true;

            stats.TakeDamage(9999f);
            Assert.AreEqual(0f, stats.CurrentHealth, 0.01f);
            Assert.IsFalse(stats.IsAlive);
            Assert.IsTrue(diedFired);
        }
    }
}
