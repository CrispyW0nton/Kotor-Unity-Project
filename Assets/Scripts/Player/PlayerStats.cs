using System;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Player
{
    /// <summary>
    /// Data container for a character's stats.
    /// Used for player characters AND companions AND enemies.
    /// 
    /// Scaling formulas (from design doc):
    ///   HP(level)      = 100 + 25 × level
    ///   Damage(level)  = BaseDamage × (1 + 0.1 × level) × (1 + TalentMultiplier)
    /// </summary>
    [Serializable]
    public class PlayerStats
    {
        // ── INSPECTOR / SERIALIZED FIELDS ──────────────────────────────────────
        [Header("Identity")]
        [SerializeField] private string characterName = "Revan";
        [SerializeField] private int level = 1;
        [SerializeField] private float experience = 0f;
        [SerializeField] private float xpToNextLevel = 1000f;

        [Header("Combat Stats")]
        [SerializeField] private float maxHealth = 125f;
        [SerializeField] private float currentHealth = 125f;
        [SerializeField] private float maxShield = 100f;
        [SerializeField] private float currentShield = 100f;
        [SerializeField] private float stamina = 100f;

        [Header("Derived Stats")]
        [SerializeField] private float attackBonus = 0f;
        [SerializeField] private float defenseRating = 0f;
        [SerializeField] private float talentDamageMultiplier = 0f; // 0..0.5 range

        [Header("Mode Affinity")]
        [SerializeField] private float rtsCombatTimeTotal = 0f;
        [SerializeField] private float actionCombatTimeTotal = 0f;

        // ── EVENTS ─────────────────────────────────────────────────────────────
        public event Action<float, float> OnHealthChanged;    // (current, max)
        public event Action<float, float> OnShieldChanged;    // (current, max)
        public event Action<int> OnLevelUp;                   // (new level)
        public event Action OnDied;

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public string CharacterName => characterName;
        public int Level => level;
        public float Experience => experience;
        public float XpToNextLevel => xpToNextLevel;
        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public float MaxShield => maxShield;
        public float CurrentShield => currentShield;
        public float Stamina => stamina;
        public float AttackBonus => attackBonus;
        public float DefenseRating => defenseRating;
        public float TalentDamageMultiplier => Mathf.Clamp(talentDamageMultiplier, 0f, GameConstants.MAX_TALENT_DAMAGE_MULTIPLIER);
        public float RTSCombatTimeTotal => rtsCombatTimeTotal;
        public float ActionCombatTimeTotal => actionCombatTimeTotal;
        public bool IsAlive => currentHealth > 0f;

        // ── INITIALIZATION ─────────────────────────────────────────────────────
        public PlayerStats() { }

        public PlayerStats(string name, int startLevel = 1)
        {
            characterName = name;
            SetLevel(startLevel);
        }

        // ── LEVELING ───────────────────────────────────────────────────────────
        /// <summary>
        /// Set level and recalculate all derived stats.
        /// HP(level) = 100 + 25 × level
        /// </summary>
        public void SetLevel(int newLevel)
        {
            newLevel = Mathf.Clamp(newLevel, 1, GameConstants.MAX_LEVEL);
            level = newLevel;

            // Recalculate max health from formula
            maxHealth = GameConstants.BASE_HEALTH + GameConstants.HEALTH_PER_LEVEL * level;
            currentHealth = maxHealth;

            // XP curve: each level requires 1000 × level XP
            xpToNextLevel = 1000f * level;

            Debug.Log($"[PlayerStats] {characterName} set to level {level}, HP: {maxHealth}");
        }

        /// <summary>Add experience and check for level up.</summary>
        public void AddExperience(float amount)
        {
            experience += amount;
            while (experience >= xpToNextLevel && level < GameConstants.MAX_LEVEL)
            {
                experience -= xpToNextLevel;
                LevelUp();
            }
        }

        private void LevelUp()
        {
            int newLevel = Mathf.Clamp(level + 1, 1, GameConstants.MAX_LEVEL);
            float oldMaxHealth = maxHealth;
            SetLevel(newLevel);

            // Heal the difference on level up (standard RPG behavior)
            float healthGained = maxHealth - oldMaxHealth;
            Heal(healthGained);

            OnLevelUp?.Invoke(level);
            EventBus.Publish(EventBus.EventType.PlayerLevelUp);
        }

        // ── HEALTH & SHIELD ────────────────────────────────────────────────────
        /// <summary>Apply damage to this character, shield absorbs first.</summary>
        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
        {
            if (!IsAlive) return;
            if (amount <= 0f) return;

            // Shield absorbs first
            if (currentShield > 0f)
            {
                float shieldAbsorb = Mathf.Min(currentShield, amount);
                currentShield -= shieldAbsorb;
                amount -= shieldAbsorb;
                OnShieldChanged?.Invoke(currentShield, maxShield);
            }

            // Remaining damage goes to health
            if (amount > 0f)
            {
                currentHealth = Mathf.Max(0f, currentHealth - amount);
                OnHealthChanged?.Invoke(currentHealth, maxHealth);

                if (currentHealth <= 0f)
                    Die();
            }
        }

        /// <summary>Heal this character (capped at max health).</summary>
        public void Heal(float amount)
        {
            if (!IsAlive) return;
            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>Restore shield (capped at max shield).</summary>
        public void RestoreShield(float amount)
        {
            currentShield = Mathf.Min(maxShield, currentShield + amount);
            OnShieldChanged?.Invoke(currentShield, maxShield);
        }

        private void Die()
        {
            currentHealth = 0f;
            OnDied?.Invoke();
            EventBus.Publish(EventBus.EventType.PlayerDied);
        }

        // ── DAMAGE CALCULATION ─────────────────────────────────────────────────
        /// <summary>
        /// Get the scaled ability damage for this character's level.
        /// Damage(level) = BaseDamage × (1 + 0.1 × level) × (1 + TalentMultiplier)
        /// </summary>
        public float GetScaledAbilityDamage(float baseDamage)
        {
            return baseDamage
                * (1f + GameConstants.ABILITY_DAMAGE_LEVEL_MULTIPLIER * level)
                * (1f + TalentDamageMultiplier);
        }

        // ── TALENT SYSTEM ──────────────────────────────────────────────────────
        public void AddTalentDamageBonus(float bonus)
        {
            talentDamageMultiplier = Mathf.Clamp(
                talentDamageMultiplier + bonus,
                0f, GameConstants.MAX_TALENT_DAMAGE_MULTIPLIER);
        }

        // ── MODE AFFINITY TRACKING ─────────────────────────────────────────────
        public void RecordCombatTime(GameMode mode, float seconds)
        {
            if (mode == GameMode.RTS) rtsCombatTimeTotal += seconds;
            else actionCombatTimeTotal += seconds;
        }

        /// <summary>
        /// Ratio of time spent in RTS mode vs total combat time.
        /// Used by balance telemetry to detect mode dominance.
        /// </summary>
        public float RTSUsageRatio
        {
            get
            {
                float total = rtsCombatTimeTotal + actionCombatTimeTotal;
                return total > 0f ? rtsCombatTimeTotal / total : 0f;
            }
        }

        // ── SERIALIZATION (for SaveSystem) ─────────────────────────────────────
        public PlayerStatsData ToData()
        {
            return new PlayerStatsData
            {
                characterName = this.characterName,
                level = this.level,
                experience = this.experience,
                currentHealth = this.currentHealth,
                currentShield = this.currentShield,
                stamina = this.stamina,
                attackBonus = this.attackBonus,
                defenseRating = this.defenseRating,
                talentDamageMultiplier = this.talentDamageMultiplier,
                rtsCombatTimeTotal = this.rtsCombatTimeTotal,
                actionCombatTimeTotal = this.actionCombatTimeTotal
            };
        }

        public void FromData(PlayerStatsData data)
        {
            characterName = data.characterName;
            SetLevel(data.level);
            experience = data.experience;
            currentHealth = data.currentHealth;
            currentShield = data.currentShield;
            stamina = data.stamina;
            attackBonus = data.attackBonus;
            defenseRating = data.defenseRating;
            talentDamageMultiplier = data.talentDamageMultiplier;
            rtsCombatTimeTotal = data.rtsCombatTimeTotal;
            actionCombatTimeTotal = data.actionCombatTimeTotal;
        }
    }

    /// <summary>Serializable data structure for saving/loading PlayerStats.</summary>
    [Serializable]
    public class PlayerStatsData
    {
        public string characterName;
        public int level;
        public float experience;
        public float currentHealth;
        public float currentShield;
        public float stamina;
        public float attackBonus;
        public float defenseRating;
        public float talentDamageMultiplier;
        public float rtsCombatTimeTotal;
        public float actionCombatTimeTotal;
    }
}
