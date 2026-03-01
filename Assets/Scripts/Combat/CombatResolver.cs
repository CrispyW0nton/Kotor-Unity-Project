using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Combat
{
    /// <summary>
    /// Resolves combat outcomes for both Action Mode and RTS Mode.
    /// 
    /// Action Mode:
    ///   Hit detection via Unity Raycast/Physics. Damage is player skill-based.
    ///   Headshot and weak-point multipliers reward precision.
    /// 
    /// RTS Mode:
    ///   Dice-roll hit resolution. No miss if hitChance >= 95% (design doc spec).
    ///   Hit chance = BaseAccuracy + AttackBonus - DefenseRating.
    ///   Damage uses DPS_RTS × elapsed time.
    /// </summary>
    public static class CombatResolver
    {
        // ── ACTION MODE RESOLUTION ─────────────────────────────────────────────
        /// <summary>
        /// Resolve an Action mode attack (hitscan).
        /// Returns the damage to apply and the hit type.
        /// </summary>
        public static (float damage, HitType hitType) ResolveActionHit(
            float weaponDamage,
            RaycastHit hit,
            PlayerStats attackerStats)
        {
            if (attackerStats == null) return (0f, HitType.Miss);

            string tag = hit.collider.tag;
            HitType hitType = HitType.Normal;
            float multiplier = 1f;

            // Check for headshot
            if (tag == "Head" || tag == "Headshot")
            {
                hitType = HitType.Headshot;
                multiplier = GameConstants.HEADSHOT_MULTIPLIER;
            }
            // Check for weak point
            else if (tag == "WeakPoint")
            {
                hitType = HitType.WeakPoint;
                multiplier = GameConstants.WEAK_POINT_MULTIPLIER;
            }

            float rawDamage = attackerStats.GetScaledAbilityDamage(weaponDamage) * multiplier;
            return (rawDamage, hitType);
        }

        // ── RTS MODE RESOLUTION ────────────────────────────────────────────────
        /// <summary>
        /// Resolve an RTS mode attack (stat-based dice roll).
        /// Returns hit chance, hit type, and damage.
        /// </summary>
        public static (bool isHit, float damage, HitType hitType, float hitChance) ResolveRTSAttack(
            float weaponDPSRTS,
            float deltaTime,
            PlayerStats attackerStats,
            PlayerStats defenderStats)
        {
            if (attackerStats == null) return (false, 0f, HitType.Miss, 0f);

            // Hit chance = BaseAccuracy + AttackBonus - DefenseRating (normalized 0..1)
            float baseAccuracy = 0.70f;
            float defenderDefense = defenderStats?.DefenseRating ?? 0f;

            float hitChance = Mathf.Clamp01(
                baseAccuracy
                + (attackerStats.AttackBonus / 100f)
                - (defenderDefense / 100f));

            // RTS guaranteed hit threshold
            bool isHit;
            if (hitChance >= GameConstants.RTS_GUARANTEED_HIT_THRESHOLD)
                isHit = true;
            else
                isHit = UnityEngine.Random.value < hitChance;

            if (!isHit)
                return (false, 0f, HitType.Miss, hitChance);

            // Calculate damage (DPS × delta time × level scaling)
            float rawDamage = attackerStats.GetScaledAbilityDamage(weaponDPSRTS) * deltaTime;

            return (true, rawDamage, HitType.Normal, hitChance);
        }

        // ── ABILITY RESOLUTION ─────────────────────────────────────────────────
        /// <summary>
        /// Calculate ability damage based on mode.
        /// RTS mode deals 80% of Action mode ability damage (design doc spec).
        /// </summary>
        public static float ResolveAbilityDamage(
            float baseAbilityDamage,
            GameMode executionMode,
            PlayerStats casterStats)
        {
            if (casterStats == null) return 0f;

            float scaledDamage = casterStats.GetScaledAbilityDamage(baseAbilityDamage);

            if (executionMode == GameMode.RTS)
                scaledDamage *= GameConstants.RTS_ABILITY_DAMAGE_RATIO;

            return scaledDamage;
        }

        // ── DIFFICULTY ADJUSTMENT ──────────────────────────────────────────────
        /// <summary>Get enemy max HP adjusted for difficulty and current player mode.</summary>
        public static float GetEnemyMaxHP(float baseHP, int level, int encounterIndex, GameMode activeMode)
        {
            float scaledHP = baseHP + 30f * level * (1f + 0.05f * encounterIndex);

            if (GameManager.Instance != null)
                scaledHP *= GameManager.Instance.GetEnemyHPMultiplier(activeMode);

            return scaledHP;
        }

        // ── SOLO PENALTY ───────────────────────────────────────────────────────
        /// <summary>
        /// Check if the player is effectively solo (no living companions nearby).
        /// If so, they take the "Exposed Target" damage multiplier.
        /// </summary>
        public static float GetSoloExposedTargetMultiplier(bool hasLivingCompanionsNearby)
        {
            return hasLivingCompanionsNearby ? 1.0f : GameConstants.SOLO_EXPOSED_TARGET_MULTIPLIER;
        }

        // ── HIT CHANCE DISPLAY (for RTS UI) ───────────────────────────────────
        /// <summary>
        /// Get a player-readable hit chance string for the RTS UI.
        /// Shows "Guaranteed" if >= 95%, otherwise shows percentage.
        /// </summary>
        public static string GetHitChanceDisplayString(float hitChance)
        {
            if (hitChance >= GameConstants.RTS_GUARANTEED_HIT_THRESHOLD)
                return "GUARANTEED";
            return $"{Mathf.RoundToInt(hitChance * 100)}%";
        }
    }
}
