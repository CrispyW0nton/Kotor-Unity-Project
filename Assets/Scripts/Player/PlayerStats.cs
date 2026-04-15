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

        [Header("Ability Scores (D&D / KotOR)")]
        [SerializeField] private int strength     = 10;
        [SerializeField] private int dexterity    = 10;
        [SerializeField] private int constitution = 10;
        [SerializeField] private int intelligence = 10;
        [SerializeField] private int wisdom       = 10;
        [SerializeField] private int charisma     = 10;

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

        // ── ABILITY SCORE PROPERTIES ───────────────────────────────────────────
        public int Strength
        {
            get => strength;
            set => strength = Mathf.Clamp(value, 3, 30);
        }
        public int Dexterity
        {
            get => dexterity;
            set => dexterity = Mathf.Clamp(value, 3, 30);
        }
        public int Constitution
        {
            get => constitution;
            set => constitution = Mathf.Clamp(value, 3, 30);
        }
        public int Intelligence
        {
            get => intelligence;
            set => intelligence = Mathf.Clamp(value, 3, 30);
        }
        public int Wisdom
        {
            get => wisdom;
            set => wisdom = Mathf.Clamp(value, 3, 30);
        }
        public int Charisma
        {
            get => charisma;
            set => charisma = Mathf.Clamp(value, 3, 30);
        }

        /// <summary>Standard D&D ability modifier: (score - 10) / 2 (floor).</summary>
        public int AbilityModifier(int score) => Mathf.FloorToInt((score - 10) / 2f);

        public int MaxHP => (int)maxHealth;

        // ── DERIVED STATS FOR UI ───────────────────────────────────────────────
        /// <summary>Armour class = 10 + DEX modifier + equipped armour AC bonus.</summary>
        public int ArmorClass
        {
            get
            {
                int ac = 10 + AbilityModifier(dexterity);
                // Add body armour AC if equipped
                var inv = KotORUnity.Inventory.InventoryManager.Instance?.PlayerInventory;
                if (inv != null)
                {
                    var body = inv.GetEquipped(KotORUnity.Inventory.EquipSlot.Body);
                    if (body != null)
                    {
                        int dexCap = Mathf.Min(AbilityModifier(dexterity), body.MaxDexBonus);
                        ac = 10 + dexCap + body.ACBonus;
                    }
                }
                return ac;
            }
        }

        /// <summary>Fortitude save = CON modifier + level / 3.</summary>
        public int FortSave  => AbilityModifier(constitution) + Mathf.FloorToInt(level / 3f);
        /// <summary>Reflex save = DEX modifier + level / 4.</summary>
        public int RefSave   => AbilityModifier(dexterity)    + Mathf.FloorToInt(level / 4f);
        /// <summary>Will save = WIS modifier + level / 4.</summary>
        public int WillSave  => AbilityModifier(wisdom)       + Mathf.FloorToInt(level / 4f);

        /// <summary>Human-readable damage bonus for the main-hand weapon.</summary>
        public string DamageBonusText
        {
            get
            {
                var inv = KotORUnity.Inventory.InventoryManager.Instance?.PlayerInventory;
                if (inv != null)
                {
                    var wp = inv.GetEquipped(KotORUnity.Inventory.EquipSlot.WeaponR);
                    if (wp != null)
                        return $"{wp.DamageNumDice}d{wp.DamageDie}+{wp.DamageBonus + AbilityModifier(strength)}";
                }
                // Unarmed
                return $"1d4+{AbilityModifier(strength)}";
            }
        }

        /// <summary>Class + level display string, e.g. "Jedi Guardian 5".</summary>
        public string ClassLevelDisplay => $"Level {level}";

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

        /// <summary>
        /// Recalculate maxHealth and attackBonus after ability scores change.
        /// Call this after setting Strength/Constitution from a NewGameConfig.
        /// KotOR formula: HP = BASE_HEALTH + HEALTH_PER_LEVEL × level + CON_modifier × level
        /// </summary>
        public void RecalculateDerivedStats()
        {
            int conMod = AbilityModifier(constitution);
            maxHealth  = GameConstants.BASE_HEALTH
                       + GameConstants.HEALTH_PER_LEVEL * level
                       + conMod * level;
            maxHealth  = Mathf.Max(maxHealth, 1f);
            currentHealth = maxHealth;

            // STR modifier feeds melee attack bonus
            attackBonus = AbilityModifier(strength);

            // DEX modifier feeds defense rating
            defenseRating = AbilityModifier(dexterity);

            Debug.Log($"[PlayerStats] Recalculated: {characterName} " +
                      $"HP={maxHealth}  ATK={attackBonus}  DEF={defenseRating}");
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

        /// <summary>Set the maximum health value (used by NWScriptVM and dev console).</summary>
        public void SetMaxHealth(int newMax)
        {
            maxHealth = Mathf.Max(1f, newMax);
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Scale max and current health by <paramref name="multiplier"/>.
        /// Used by EncounterManager to buff later-wave enemies.
        /// </summary>
        public void ScaleHealth(float multiplier)
        {
            multiplier  = Mathf.Max(0.01f, multiplier);
            float ratio = (maxHealth > 0f) ? currentHealth / maxHealth : 1f;
            maxHealth     = maxHealth * multiplier;
            currentHealth = maxHealth * ratio;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Directly set the raw experience value (NWScript SetXP command).
        /// Does NOT trigger level-ups — use AddExperience for that.
        /// </summary>
        public void SetExperience(float value)
        {
            experience = Mathf.Max(0f, value);
        }

        /// <summary>Set the character's display name (from Character Creation).</summary>
        public void SetCharacterName(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                characterName = name.Trim();
        }

        /// <summary>
        /// Batch-set all six D&amp;D ability scores (from Character Creation point-buy).
        /// Triggers a full stat recalculation.
        /// </summary>
        public void SetAbilityScores(int str, int dex, int con, int intel, int wis, int cha)
        {
            strength     = Mathf.Clamp(str,   3, 20);
            dexterity    = Mathf.Clamp(dex,   3, 20);
            constitution = Mathf.Clamp(con,   3, 20);
            intelligence = Mathf.Clamp(intel, 3, 20);
            wisdom       = Mathf.Clamp(wis,   3, 20);
            charisma     = Mathf.Clamp(cha,   3, 20);
            RecalculateDerivedStats();
        }

        /// <summary>Restore shield (capped at max shield).</summary>
        public void RestoreShield(float amount)
        {
            currentShield = Mathf.Min(maxShield, currentShield + amount);
            OnShieldChanged?.Invoke(currentShield, maxShield);
        }

        // ── TEMPORARY BUFFS ───────────────────────────────────────────────────

        private int   _tempAttackBonus;
        private float _tempAttackBonusExpiry;

        /// <summary>Add a timed attack bonus (Force Valor, etc.).</summary>
        public void AddTemporaryAttackBonus(int bonus, float duration)
        {
            _tempAttackBonus       += bonus;
            _tempAttackBonusExpiry = UnityEngine.Time.time + duration;
        }

        /// <summary>Returns current effective attack bonus (including temporary).</summary>
        public int EffectiveAttackBonus(int baseBonus)
        {
            if (_tempAttackBonusExpiry > 0f && UnityEngine.Time.time < _tempAttackBonusExpiry)
                return baseBonus + _tempAttackBonus;
            _tempAttackBonus = 0;
            _tempAttackBonusExpiry = 0f;
            return baseBonus;
        }

        private void Die()
        {
            currentHealth = 0f;
            OnDied?.Invoke();
            // NOTE: EventBus.PlayerDied is intentionally NOT published here.
            // PlayerStatsBehaviour.OnCharacterDied() handles it so only actual
            // player-tagged GameObjects raise the PlayerDied bus event.
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
