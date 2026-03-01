using NUnit.Framework;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Unit tests for GameConstants — ensures all design document formulas
    /// produce expected values.
    /// </summary>
    public class GameConstantsTests
    {
        [Test]
        public void HealthFormula_Level1_Returns125()
        {
            float expected = GameConstants.BASE_HEALTH + GameConstants.HEALTH_PER_LEVEL * 1;
            Assert.AreEqual(125f, expected);
        }

        [Test]
        public void HealthFormula_Level30_Returns850()
        {
            float expected = GameConstants.BASE_HEALTH + GameConstants.HEALTH_PER_LEVEL * 30;
            Assert.AreEqual(850f, expected);
        }

        [Test]
        public void WeaponDamageTier1_RTSBase_Is10()
        {
            float tier1RTS = GameConstants.BASE_WEAPON_DPS_RTS
                * System.MathF.Pow(1, GameConstants.WEAPON_TIER_EXPONENT);
            Assert.AreEqual(GameConstants.BASE_WEAPON_DPS_RTS, tier1RTS, 0.01f);
        }

        [Test]
        public void WeaponDamageTier10_RTSScales_GreaterThanLinear()
        {
            float tier10RTS = GameConstants.BASE_WEAPON_DPS_RTS
                * System.MathF.Pow(10, GameConstants.WEAPON_TIER_EXPONENT);
            float linearProjection = GameConstants.BASE_WEAPON_DPS_RTS * 10f;
            Assert.Greater(tier10RTS, linearProjection);
        }

        [Test]
        public void ActionDPSRatio_IsPoint7OfRTSSquadDPS()
        {
            Assert.AreEqual(0.70f, GameConstants.ACTION_TO_RTS_DPS_RATIO, 0.001f);
        }

        [Test]
        public void CompanionActionEfficiency_Is60Percent()
        {
            Assert.AreEqual(0.60f, GameConstants.COMPANION_ACTION_MODE_EFFICIENCY, 0.001f);
        }

        [Test]
        public void RTSGuaranteedHit_Threshold_Is95Percent()
        {
            Assert.AreEqual(0.95f, GameConstants.RTS_GUARANTEED_HIT_THRESHOLD, 0.001f);
        }

        [Test]
        public void ModeSwitchCooldown_Is2Seconds()
        {
            Assert.AreEqual(2.0f, GameConstants.MODE_SWITCH_COOLDOWN, 0.001f);
        }

        [Test]
        public void RTSTimeScale_Is10Percent()
        {
            Assert.AreEqual(0.1f, GameConstants.RTS_TIME_SCALE, 0.001f);
        }

        [Test]
        public void HeadshotMultiplier_Is2Point5()
        {
            Assert.AreEqual(2.5f, GameConstants.HEADSHOT_MULTIPLIER, 0.001f);
        }

        [Test]
        public void WeakPointMultiplier_Is3()
        {
            Assert.AreEqual(3.0f, GameConstants.WEAK_POINT_MULTIPLIER, 0.001f);
        }

        [Test]
        public void RTSAbilityDamageRatio_Is80Percent()
        {
            Assert.AreEqual(0.80f, GameConstants.RTS_ABILITY_DAMAGE_RATIO, 0.001f);
        }
    }
}
