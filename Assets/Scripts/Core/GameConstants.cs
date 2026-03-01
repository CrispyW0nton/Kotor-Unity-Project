using System;
using UnityEngine;

namespace KotORUnity.Core
{
    /// <summary>
    /// All numeric constants for the MRL_GameForge v2 system.
    /// Never use magic numbers in game logic — reference these instead.
    /// </summary>
    public static class GameConstants
    {
        // ── MODE SWITCH ────────────────────────────────────────────────────────
        /// <summary>Duration of the mode switch camera transition in seconds.</summary>
        public const float MODE_SWITCH_TRANSITION_DURATION = 1.5f;

        /// <summary>Cooldown after switching modes before switching again.</summary>
        public const float MODE_SWITCH_COOLDOWN = 2.0f;

        /// <summary>Duration of the vulnerability window when switching modes mid-combat.</summary>
        public const float MODE_SWITCH_VULNERABILITY_DURATION = 1.0f;

        /// <summary>Damage multiplier applied during mode switch vulnerability window.</summary>
        public const float MODE_SWITCH_VULNERABILITY_MULTIPLIER = 1.30f;

        // ── RTS TIME DILATION ──────────────────────────────────────────────────
        /// <summary>Time scale when RTS mode is active (10% of real time).</summary>
        public const float RTS_TIME_SCALE = 0.1f;

        /// <summary>Time scale when Action mode is active.</summary>
        public const float ACTION_TIME_SCALE = 1.0f;

        /// <summary>Duration to interpolate to RTS time scale from Action.</summary>
        public const float TIME_SCALE_TRANSITION_DURATION = 0.5f;

        /// <summary>Pause cooldown after unpausing in RTS mode (seconds).</summary>
        public const float RTS_PAUSE_COOLDOWN = 5.0f;

        // ── COMPANION AI ───────────────────────────────────────────────────────
        /// <summary>
        /// Efficiency multiplier for companion AI when player is in Action mode.
        /// Represents autonomous behavior vs RTS-commanded behavior.
        /// </summary>
        public const float COMPANION_ACTION_MODE_EFFICIENCY = 0.60f;

        /// <summary>Efficiency of companions under direct RTS command.</summary>
        public const float COMPANION_RTS_MODE_EFFICIENCY = 1.00f;

        /// <summary>Maximum number of companions in the squad.</summary>
        public const int MAX_COMPANIONS = 3;

        // ── COMBAT ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Target DPS ratio: Action solo DPS vs RTS squad DPS.
        /// Action solo should be ~70% of full squad RTS output.
        /// </summary>
        public const float ACTION_TO_RTS_DPS_RATIO = 0.70f;

        /// <summary>Guaranteed hit threshold in RTS mode (no misses at or above this).</summary>
        public const float RTS_GUARANTEED_HIT_THRESHOLD = 0.95f;

        /// <summary>Damage multiplier for headshots in Action mode.</summary>
        public const float HEADSHOT_MULTIPLIER = 2.5f;

        /// <summary>Damage multiplier for weak point crits in Action mode.</summary>
        public const float WEAK_POINT_MULTIPLIER = 3.0f;

        /// <summary>
        /// Extra incoming damage to a solo player who ignores squad in Action mode.
        /// Represents "Exposed Target" debuff for deathball exploit prevention.
        /// </summary>
        public const float SOLO_EXPOSED_TARGET_MULTIPLIER = 1.50f;

        /// <summary>RTS mode ability variant damage multiplier vs Action-executed version.</summary>
        public const float RTS_ABILITY_DAMAGE_RATIO = 0.80f;

        // ── ENEMY ADAPTATION (pause-scum counter) ─────────────────────────────
        /// <summary>Damage increase per pause cycle when enemies adapt.</summary>
        public const float ENEMY_ADAPTATION_PER_PAUSE = 0.10f;

        /// <summary>Time (real seconds) before enemy adaptation stacks reset.</summary>
        public const float ENEMY_ADAPTATION_RESET_TIME = 30.0f;

        /// <summary>Maximum number of adaptation stacks an enemy can accumulate.</summary>
        public const int ENEMY_ADAPTATION_MAX_STACKS = 5;

        /// <summary>Cooldown on enemy Sprint ability (ignores RTS slow-motion).</summary>
        public const float ENEMY_SPRINT_COOLDOWN = 20.0f;

        // ── PROGRESSION ────────────────────────────────────────────────────────
        /// <summary>Base HP at level 1.</summary>
        public const float BASE_HEALTH = 100f;

        /// <summary>HP gained per level.</summary>
        public const float HEALTH_PER_LEVEL = 25f;

        /// <summary>Maximum character level.</summary>
        public const int MAX_LEVEL = 30;

        /// <summary>Maximum weapon tier.</summary>
        public const int MAX_WEAPON_TIER = 10;

        /// <summary>Weapon damage tier scaling exponent.</summary>
        public const float WEAPON_TIER_EXPONENT = 1.15f;

        /// <summary>Base RTS weapon DPS at tier 1.</summary>
        public const float BASE_WEAPON_DPS_RTS = 10f;

        /// <summary>Base Action weapon damage at tier 1.</summary>
        public const float BASE_WEAPON_DAMAGE_ACTION = 15f;

        /// <summary>Ability damage scaling per level (10% per level).</summary>
        public const float ABILITY_DAMAGE_LEVEL_MULTIPLIER = 0.10f;

        /// <summary>Maximum talent tree damage bonus.</summary>
        public const float MAX_TALENT_DAMAGE_MULTIPLIER = 0.50f;

        /// <summary>XP bonus for using the under-utilized mode (affinity system).</summary>
        public const float MODE_AFFINITY_XP_BONUS = 0.05f;

        /// <summary>Encounters since last mode use before affinity bonus triggers.</summary>
        public const int MODE_AFFINITY_ENCOUNTER_THRESHOLD = 3;

        // ── CAMERA ─────────────────────────────────────────────────────────────
        /// <summary>Action camera offset from player (right, up, back).</summary>
        public static readonly Vector3 ACTION_CAMERA_OFFSET = new Vector3(1.5f, 2.0f, -3.0f);

        /// <summary>RTS camera height above the encounter center.</summary>
        public const float RTS_CAMERA_HEIGHT = 25f;

        /// <summary>RTS camera back offset from encounter center.</summary>
        public const float RTS_CAMERA_BACK = 15f;

        /// <summary>Orientation pulse highlight duration after mode switch.</summary>
        public const float ORIENTATION_PULSE_DURATION = 0.3f;

        // ── DIFFICULTY MODIFIERS ───────────────────────────────────────────────
        /// <summary>Enemy HP multiplier in Action mode on Normal difficulty.</summary>
        public const float NORMAL_ACTION_ENEMY_HP_MULT = 1.20f;

        /// <summary>Enemy HP multiplier in Action mode on Hard difficulty.</summary>
        public const float HARD_ACTION_ENEMY_HP_MULT = 1.50f;

        /// <summary>Enemy HP multiplier in RTS mode on Hard difficulty.</summary>
        public const float HARD_RTS_ENEMY_HP_MULT = 1.20f;

        /// <summary>Enemy HP multiplier on Nightmare difficulty (both modes).</summary>
        public const float NIGHTMARE_ENEMY_HP_MULT = 1.50f;

        // ── BALANCE TELEMETRY THRESHOLDS ───────────────────────────────────────
        /// <summary>If RTS combat time % drops below this, buff RTS mode.</summary>
        public const float RTS_UNDERUSE_THRESHOLD = 0.15f;

        /// <summary>If RTS combat time % rises above this, buff Action mode.</summary>
        public const float RTS_OVERUSE_THRESHOLD = 0.70f;
    }
}
