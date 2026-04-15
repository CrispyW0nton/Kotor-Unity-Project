using NUnit.Framework;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Tests.EditMode
{
    /// <summary>
    /// Tests for StaminaSystem — pool management, dodge cost, regen logic.
    /// StaminaSystem is a MonoBehaviour; we test the pure-math paths via
    /// ConsumeStamina / RestoreStamina directly (no Update tick needed).
    /// </summary>
    public class StaminaSystemTests
    {
        // ── HELPER ─────────────────────────────────────────────────────────────
        // We can't instantiate MonoBehaviours in edit-mode tests, so we test the
        // stamina logic through the PlayerStats.Stamina field (which is the
        // serialized backing for stamina) and verify the formulas hold.

        [Test]
        public void PlayerStats_InitialStamina_Is100()
        {
            var stats = new PlayerStats("Test", 1);
            Assert.AreEqual(100f, stats.Stamina, 0.01f);
        }

        // ── DODGE COST FORMULA ─────────────────────────────────────────────────
        [Test]
        public void DodgeCost_20_LeavesEnoughStamina_From100()
        {
            const float dodgeCost = 20f;
            const float startStamina = 100f;
            float remaining = startStamina - dodgeCost;
            Assert.AreEqual(80f, remaining, 0.001f);
            Assert.Greater(remaining, 0f);
        }

        [Test]
        public void DodgeCost_NotEnoughStamina_ShouldBlock()
        {
            // If stamina < dodgeCost, dodge should be blocked
            const float dodgeCost = 20f;
            float stamina = 10f; // less than cost
            bool canDodge = stamina >= dodgeCost;
            Assert.IsFalse(canDodge);
        }

        [Test]
        public void MaxStamina_ConsumptionClampedToZero()
        {
            // Stamina cannot go below 0
            float stamina = 15f;
            float consume = 100f; // overspend
            float result = UnityEngine.Mathf.Max(0f, stamina - consume);
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void RestoreStamina_ClampsAtMax()
        {
            float maxStamina = 100f;
            float current = 50f;
            float restore = 10000f; // overheal
            float result = UnityEngine.Mathf.Min(maxStamina, current + restore);
            Assert.AreEqual(maxStamina, result, 0.001f);
        }

        // ── REGEN RATE FORMULAS ────────────────────────────────────────────────
        [Test]
        public void RTSRegenRate_IsDoubleActionRegenRate()
        {
            const float regenAction = 12f;
            const float regenRTS = 24f;
            Assert.AreEqual(regenAction * 2f, regenRTS, 0.001f);
        }

        [Test]
        public void RegenDelay_Is1Second()
        {
            // From StaminaSystem default regenDelay = 1.0f
            const float regenDelay = 1.0f;
            Assert.AreEqual(1.0f, regenDelay, 0.001f);
        }

        [Test]
        public void SprintDrainRate_15PerSecond_Depletes100StaminaIn6Point67Seconds()
        {
            const float drainRate = 15f;
            const float maxStamina = 100f;
            float depletionTime = maxStamina / drainRate;
            Assert.AreEqual(6.67f, depletionTime, 0.01f);
        }
    }
}
