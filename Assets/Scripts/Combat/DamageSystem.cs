using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Combat
{
    /// <summary>
    /// Applies damage to GameObjects with a PlayerStatsBehaviour component.
    /// Handles:
    ///   - Mode-specific multipliers
    ///   - Vulnerability window (mode switch penalty)
    ///   - Enemy adaptation stacks (pause-scum counter)
    ///   - VFX and audio spawning
    ///   - EventBus publishing
    /// </summary>
    public static class DamageSystem
    {
        // ── CORE APPLY DAMAGE ──────────────────────────────────────────────────
        /// <summary>
        /// Apply damage from a source to a target.
        /// All damage flows through here to ensure proper multipliers and events.
        /// </summary>
        public static void ApplyDamage(
            GameObject source,
            GameObject target,
            float baseDamage,
            DamageType damageType,
            HitType hitType,
            GameMode currentMode)
        {
            if (target == null) return;

            var targetStats = target.GetComponent<PlayerStatsBehaviour>();
            if (targetStats == null || !targetStats.Stats.IsAlive) return;

            // Apply mode switch vulnerability if target is the player
            float finalDamage = baseDamage;
            var modeSwitchSystem = Object.FindObjectOfType<ModeSwitchSystem>();
            if (modeSwitchSystem != null && modeSwitchSystem.IsVulnerable
                && IsPlayerCharacter(target))
            {
                finalDamage *= modeSwitchSystem.CurrentDamageTakenMultiplier;
            }

            // Apply enemy adaptation stacks (if source is an enemy attacking player)
            if (IsPlayerCharacter(target) && modeSwitchSystem != null)
            {
                int adaptationStacks = Mathf.Min(
                    modeSwitchSystem.PauseCycleCount,
                    GameConstants.ENEMY_ADAPTATION_MAX_STACKS);
                finalDamage *= (1f + GameConstants.ENEMY_ADAPTATION_PER_PAUSE * adaptationStacks);
            }

            // Apply solo exposed-target multiplier
            if (IsPlayerCharacter(target))
            {
                bool hasCompanions = HasLivingNearbyCompanions(target);
                finalDamage *= CombatResolver.GetSoloExposedTargetMultiplier(hasCompanions);
            }

            // Apply damage
            targetStats.Stats.TakeDamage(finalDamage, damageType);

            // Publish event
            EventBus.Publish(EventBus.EventType.EntityDamaged,
                new EventBus.DamageEventArgs(source, target, finalDamage, damageType, hitType));

            if (!targetStats.Stats.IsAlive)
            {
                EventBus.Publish(EventBus.EventType.EntityKilled,
                    new EventBus.DamageEventArgs(source, target, finalDamage, damageType, hitType));
            }

            // Spawn hit VFX
            SpawnHitVFX(target, hitType, finalDamage);

            if (GameManager.Instance?.DebugMode == true)
                Debug.Log($"[DamageSystem] {source?.name} → {target?.name}: {finalDamage:F1} {damageType} ({hitType}) [{currentMode}]");
        }

        // ── AOE DAMAGE ─────────────────────────────────────────────────────────
        /// <summary>Apply damage to all targets within a radius.</summary>
        public static void ApplyAOE(
            GameObject source,
            Vector3 origin,
            float radius,
            float baseDamage,
            DamageType damageType,
            GameMode currentMode)
        {
            Collider[] hits = Physics.OverlapSphere(origin, radius);
            foreach (var hit in hits)
            {
                if (hit.gameObject == source) continue;
                float distanceFalloff = 1f - (Vector3.Distance(origin, hit.transform.position) / radius);
                float falloffDamage = baseDamage * distanceFalloff;
                ApplyDamage(source, hit.gameObject, falloffDamage, damageType, HitType.Normal, currentMode);
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────────
        private static bool IsPlayerCharacter(GameObject go)
        {
            return go.CompareTag("Player");
        }

        private static bool HasLivingNearbyCompanions(GameObject player)
        {
            float squadSearchRadius = 30f;
            var companions = Object.FindObjectsOfType<AI.Companion.CompanionAI>();
            foreach (var companion in companions)
            {
                if (companion == null) continue;
                if (Vector3.Distance(player.transform.position, companion.transform.position) <= squadSearchRadius)
                {
                    var stats = companion.GetComponent<PlayerStatsBehaviour>();
                    if (stats != null && stats.Stats.IsAlive)
                        return true;
                }
            }
            return false;
        }

        private static void SpawnHitVFX(GameObject target, HitType hitType, float damage)
        {
            // VFX prefab spawning handled by the VFX manager (future system)
            // For now, spawn a log message in debug builds
#if UNITY_EDITOR
            if (hitType == HitType.Headshot || hitType == HitType.WeakPoint)
                Debug.Log($"[DamageSystem] CRITICAL HIT: {damage:F1} ({hitType})");
#endif
        }
    }
}
