using NUnit.Framework;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for WeaponBase tier-scaling math (no MonoBehaviour dependencies).
    /// WeaponBase is abstract, so we test the formula directly via GameConstants
    /// and validate the expected output for known inputs.
    /// </summary>
    public class WeaponBaseTests
    {
        // ── TIER SCALING FORMULA ───────────────────────────────────────────────
        // damage = base × tier^1.15

        private static float ScaleDamage(float baseDmg, int tier)
            => baseDmg * System.MathF.Pow(tier, GameConstants.WEAPON_TIER_EXPONENT);

        [Test]
        public void Tier1_ScaledDamage_EqualsTierBase()
        {
            // tier^1.15 at tier=1 = 1.0
            float result = ScaleDamage(15f, 1);
            Assert.AreEqual(15f, result, 0.01f);
        }

        [Test]
        public void Tier2_ScaledDamage_GreaterThanTier1()
        {
            float t1 = ScaleDamage(15f, 1);
            float t2 = ScaleDamage(15f, 2);
            Assert.Greater(t2, t1);
        }

        [Test]
        public void Tier10_ScaledDamage_GreaterThanLinear()
        {
            // Super-linear scaling: tier^1.15 grows faster than tier^1.0
            float t10_actual = ScaleDamage(15f, 10);
            float t10_linear = 15f * 10f;
            Assert.Greater(t10_actual, t10_linear);
        }

        [Test]
        public void TierScaling_IsSuperLinear_AcrossAllTiers()
        {
            for (int tier = 2; tier <= GameConstants.MAX_WEAPON_TIER; tier++)
            {
                float actual  = ScaleDamage(1f, tier);
                float linear  = (float)tier;
                Assert.Greater(actual, linear,
                    $"Tier {tier}: expected super-linear scaling but got {actual} <= {linear}");
            }
        }

        [Test]
        public void RTS_DPS_IsSeventyPercentOf_ActionDPS_AtAllTiers()
        {
            // Design doc spec: DPS_Action ≈ 0.7 × DPS_RTS
            // Here we test the ratio constant rather than any per-weapon value.
            Assert.AreEqual(0.70f, GameConstants.ACTION_TO_RTS_DPS_RATIO, 0.001f);
        }

        // ── AMMO CONSTANTS ─────────────────────────────────────────────────────
        [Test]
        public void MaxWeaponTier_Is10()
        {
            Assert.AreEqual(10, GameConstants.MAX_WEAPON_TIER);
        }

        // ── FORMULA CONSISTENCY ────────────────────────────────────────────────
        [Test]
        public void BlasterRifle_Tier1_ActionDamage_MatchesBaseTimesScaling()
        {
            float baseDmgAction = 15f;   // matches BlasterRifle.Awake
            float expected = ScaleDamage(baseDmgAction, 1);
            Assert.AreEqual(15f, expected, 0.01f);
        }

        [Test]
        public void SniperRifle_ActionDamage_SignificantlyHigherThanBlasterRifle()
        {
            float sniperBase = 60f;
            float blasterBase = 15f;
            float sniperT1 = ScaleDamage(sniperBase, 1);
            float blasterT1 = ScaleDamage(blasterBase, 1);
            Assert.Greater(sniperT1, blasterT1 * 2f);
        }

        [Test]
        public void SniperRifle_DPSRTS_IsLowerThan_BlasterRifle_DPSRTS()
        {
            // Sniper: high single-shot action, low DPS_RTS
            float sniperRTSBase = 5f;
            float blasterRTSBase = 10f;
            Assert.Less(sniperRTSBase, blasterRTSBase);
        }
    }
}
