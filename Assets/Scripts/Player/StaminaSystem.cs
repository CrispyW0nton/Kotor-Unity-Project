using System;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Player
{
    /// <summary>
    /// Manages the Stamina resource for a character.
    ///
    /// Rules (design doc):
    ///   • Stamina regenerates over time when NOT sprinting or dodging.
    ///   • Dodge costs 20 stamina.
    ///   • Sprinting drains stamina at a configurable rate.
    ///   • Stamina regeneration pauses for 1 second after any stamina expenditure.
    ///   • In RTS mode, stamina regenerates at double rate (player is not directly in combat).
    ///
    /// Attach alongside PlayerStatsBehaviour on the player or companion prefab.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Stamina Pool")]
        [SerializeField] private float maxStamina = 100f;
        [SerializeField] [Range(0f, 100f)] private float startingStamina = 100f;

        [Header("Regeneration")]
        [SerializeField] private float regenRateAction = 12f;   // per second in Action mode
        [SerializeField] private float regenRateRTS = 24f;      // per second in RTS mode (double)
        [SerializeField] private float regenDelay = 1.0f;       // seconds before regen starts after use

        [Header("Costs")]
        [SerializeField] private float dodgeCost = 20f;
        [SerializeField] private float sprintDrainRate = 15f;   // per second while sprinting

        // ── STATE ──────────────────────────────────────────────────────────────
        private float _currentStamina;
        private float _regenDelayRemaining = 0f;
        private bool _isSprinting = false;

        // ── EVENTS ─────────────────────────────────────────────────────────────
        /// <summary>Raised whenever stamina changes. Args: (current, max).</summary>
        public event Action<float, float> OnStaminaChanged;

        /// <summary>Raised when stamina is fully depleted.</summary>
        public event Action OnStaminaDepleted;

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public float CurrentStamina => _currentStamina;
        public float MaxStamina => maxStamina;
        public float StaminaRatio => maxStamina > 0f ? _currentStamina / maxStamina : 0f;
        public bool HasStaminaForDodge => _currentStamina >= dodgeCost;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _currentStamina = Mathf.Clamp(startingStamina, 0f, maxStamina);
        }

        private void Update()
        {
            float dt = Time.deltaTime; // scaled — stamina regen slows in RTS (by design)

            // Sprint drain
            if (_isSprinting && _currentStamina > 0f)
            {
                ConsumeStamina(sprintDrainRate * dt);
                return; // Skip regen while actively sprinting
            }

            // Regen delay countdown
            if (_regenDelayRemaining > 0f)
            {
                _regenDelayRemaining = Mathf.Max(0f, _regenDelayRemaining - Time.unscaledDeltaTime);
                return;
            }

            // Regen
            if (_currentStamina < maxStamina)
            {
                float rate = GetRegenRate();
                float newVal = Mathf.Min(maxStamina, _currentStamina + rate * dt);
                if (!Mathf.Approximately(newVal, _currentStamina))
                {
                    _currentStamina = newVal;
                    OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
                    EventBus.Publish(EventBus.EventType.UIHUDRefresh);
                }
            }
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        /// <summary>
        /// Try to spend stamina for a dodge. Returns true if successful.
        /// </summary>
        public bool TryConsumeDodge()
        {
            if (_currentStamina < dodgeCost) return false;
            ConsumeStamina(dodgeCost);
            return true;
        }

        /// <summary>
        /// Consume a given amount of stamina.
        /// Resets the regen delay timer on every consumption.
        /// </summary>
        public void ConsumeStamina(float amount)
        {
            if (amount <= 0f) return;

            float prev = _currentStamina;
            _currentStamina = Mathf.Max(0f, _currentStamina - amount);
            _regenDelayRemaining = regenDelay;

            if (!Mathf.Approximately(prev, _currentStamina))
            {
                OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
                EventBus.Publish(EventBus.EventType.UIHUDRefresh);
            }

            if (_currentStamina <= 0f)
                OnStaminaDepleted?.Invoke();
        }

        /// <summary>Set whether the character is currently sprinting.</summary>
        public void SetSprinting(bool sprinting) => _isSprinting = sprinting;

        /// <summary>Restore stamina (e.g., from a stim pack).</summary>
        public void RestoreStamina(float amount)
        {
            if (amount <= 0f) return;
            float prev = _currentStamina;
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + amount);
            if (!Mathf.Approximately(prev, _currentStamina))
            {
                OnStaminaChanged?.Invoke(_currentStamina, maxStamina);
                EventBus.Publish(EventBus.EventType.UIHUDRefresh);
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────────
        private float GetRegenRate()
        {
            // Check current mode via GameManager singleton
            var ms = GameManager.Instance?.GetComponent<ModeSwitchSystem>()
                     ?? FindObjectOfType<ModeSwitchSystem>();
            if (ms != null && ms.CurrentMode == GameMode.RTS)
                return regenRateRTS;
            return regenRateAction;
        }
    }
}
