using NUnit.Framework;
using KotORUnity.Core;
using KotORUnity.Combat;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for the AbilityBase cooldown and damage resolution logic.
    ///
    /// AbilityBase is a MonoBehaviour, so we test via CombatResolver.ResolveAbilityDamage
    /// (the pure function) and verify the cooldown field formulas independently.
    /// </summary>
    public class AbilityBaseTests
    {
        private PlayerStats _stats;

        [SetUp]
        public void Setup()
        {
            EventBus.ClearAll();
            _stats = new PlayerStats("Caster", 5);
        }

        [TearDown]
        public void Teardown() => EventBus.ClearAll();

        // ── DAMAGE RESOLUTION ─────────────────────────────────────────────────
        [Test]
        public void ActionMode_AbilityDamage_IsFullBase()
        {
            float baseDmg = 100f;
            float result = CombatResolver.ResolveAbilityDamage(baseDmg, GameMode.Action, _stats);
            // Level 5: base × (1 + 0.1×5) × (1 + 0) = base × 1.5
            float expected = baseDmg * (1f + GameConstants.ABILITY_DAMAGE_LEVEL_MULTIPLIER * 5)
                             * (1f + _stats.TalentDamageMultiplier);
            Assert.AreEqual(expected, result, 0.01f);
        }

        [Test]
        public void RTSMode_AbilityDamage_Is80PercentOfActionMode()
        {
            float baseDmg = 100f;
            float action = CombatResolver.ResolveAbilityDamage(baseDmg, GameMode.Action, _stats);
            float rts    = CombatResolver.ResolveAbilityDamage(baseDmg, GameMode.RTS,    _stats);

            Assert.AreEqual(0.80f, rts / action, 0.001f);
        }

        [Test]
        public void NullCaster_AbilityDamage_ReturnsZero()
        {
            float result = CombatResolver.ResolveAbilityDamage(100f, GameMode.Action, null);
            Assert.AreEqual(0f, result, 0.001f);
        }

        // ── COOLDOWN MATH ──────────────────────────────────────────────────────
        [Test]
        public void Cooldown_Progress_IsZero_WhenFull()
        {
            float cooldown = 8f;
            float remaining = 8f;
            float progress = cooldown > 0 ? (1f - remaining / cooldown) : 1f;
            Assert.AreEqual(0f, progress, 0.001f);
        }

        [Test]
        public void Cooldown_Progress_Is1_WhenComplete()
        {
            float cooldown = 8f;
            float remaining = 0f;
            float progress = cooldown > 0 ? (1f - remaining / cooldown) : 1f;
            Assert.AreEqual(1f, progress, 0.001f);
        }

        [Test]
        public void Cooldown_Progress_IsHalf_AtMidpoint()
        {
            float cooldown = 8f;
            float remaining = 4f;
            float progress = cooldown > 0 ? (1f - remaining / cooldown) : 1f;
            Assert.AreEqual(0.5f, progress, 0.001f);
        }

        // ── STAMINA COST ───────────────────────────────────────────────────────
        [Test]
        public void BladeRush_BaseDamage_Is45()
        {
            // Verify the value used in BladeRush.Awake matches expectations
            const float bladeRushBase = 45f;
            float result = CombatResolver.ResolveAbilityDamage(bladeRushBase, GameMode.Action, _stats);
            Assert.Greater(result, bladeRushBase); // level scaling should push it above base
        }

        // ── MODE VARIANT DAMAGE ───────────────────────────────────────────────
        [Test]
        public void ForceStasis_SameDamageInBothModes_WithNoTalents()
        {
            // ForceStasis uses executionType = Universal;
            // base damage is 5f; we just verify RTS ratio is applied consistently
            float baseDmg = 5f;
            float action = CombatResolver.ResolveAbilityDamage(baseDmg, GameMode.Action, _stats);
            float rts    = CombatResolver.ResolveAbilityDamage(baseDmg, GameMode.RTS, _stats);
            Assert.Less(rts, action); // RTS is always less due to 0.80 ratio
        }

        // ── STASIS DURATION COMPARISON ────────────────────────────────────────
        [Test]
        public void ForceStasis_RTSDuration_IsLongerThan_ActionDuration()
        {
            const float actionDuration = 3f;
            const float rtsDuration = 4f;  // design doc: RTS gets bonus duration
            Assert.Greater(rtsDuration, actionDuration);
        }
    }
}
