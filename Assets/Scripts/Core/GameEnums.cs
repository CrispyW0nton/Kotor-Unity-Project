namespace KotORUnity.Core
{
    /// <summary>
    /// All enumerations used across the MRL_GameForge v2 system.
    /// </summary>
    public static class GameEnums
    {
        // ── GAME MODE ──────────────────────────────────────────────────────────
        /// <summary>The two primary gameplay modes the player can switch between.</summary>
        public enum GameMode
        {
            /// <summary>Third-person direct control — real-time aim, shoot, dodge.</summary>
            Action,
            /// <summary>Isometric tactical command — time-dilated, ability queuing, formations.</summary>
            RTS
        }

        /// <summary>State of the mode transition.</summary>
        public enum ModeTransitionState
        {
            Idle,
            TransitioningToRTS,
            TransitioningToAction
        }

        // ── DIFFICULTY ─────────────────────────────────────────────────────────
        public enum Difficulty
        {
            Easy,
            Normal,
            Hard,
            Nightmare
        }

        // ── TARGET GAME ────────────────────────────────────────────────────────
        public enum TargetGame
        {
            KotOR,
            TSL
        }

        // ── WEAPON TYPE ────────────────────────────────────────────────────────
        public enum WeaponType
        {
            BlasterPistol,
            BlasterRifle,
            HeavyBlaster,
            Shotgun,
            SniperRifle,
            Vibroblade,
            Lightsaber,
            Unarmed
        }

        // ── ABILITY TYPE ───────────────────────────────────────────────────────
        public enum AbilityType
        {
            Melee,
            Ranged,
            Force,
            Utility,
            Passive
        }

        /// <summary>How an ability behaves when queued in RTS vs executed in Action.</summary>
        public enum AbilityExecutionType
        {
            /// <summary>Same behavior regardless of mode.</summary>
            Universal,
            /// <summary>Requires player aim/timing input in Action mode.</summary>
            SkillBased,
            /// <summary>Auto-executes in RTS; requires input in Action.</summary>
            ModeVariant
        }

        // ── ENEMY TYPE ─────────────────────────────────────────────────────────
        public enum EnemyType
        {
            Melee,
            Ranged,
            Shielded,
            Elite,
            Boss
        }

        // ── ENEMY AI STATE ─────────────────────────────────────────────────────
        public enum EnemyAIState
        {
            Patrol,
            Detect,
            Engage,
            Flank,
            Suppress,
            Execute,
            Retreat,
            Dead
        }

        // ── COMPANION AI TIER ──────────────────────────────────────────────────
        public enum CompanionBehaviorTier
        {
            /// <summary>Tier 1: Execute queued orders with pathfinding (RTS mode).</summary>
            CommandedRTS,
            /// <summary>Tier 2: Autonomous cover-seeking, target prioritization (Action mode).</summary>
            AutonomousAction,
            /// <summary>Tier 3: Hybrid — interrupt system for player manual override commands.</summary>
            HybridOverride
        }

        // ── FORMATION ─────────────────────────────────────────────────────────
        public enum Formation
        {
            Spread,
            Line,
            Wedge,
            Column
        }

        // ── ENCOUNTER TYPE ─────────────────────────────────────────────────────
        /// <summary>Encounter archetypes — each has an optimal mode.</summary>
        public enum EncounterType
        {
            /// <summary>8+ weak enemies — RTS optimal.</summary>
            Swarm,
            /// <summary>Few high-HP ranged enemies — Action optimal.</summary>
            SniperDuel,
            /// <summary>Wave defense objective — RTS optimal.</summary>
            Siege,
            /// <summary>Single boss with weak points + adds — Hybrid optimal.</summary>
            Assassination,
            /// <summary>Generic mixed encounter.</summary>
            Standard
        }

        // ── OBJECTIVE TYPE ─────────────────────────────────────────────────────
        public enum ObjectiveType
        {
            KillAll,
            Defend,
            Extract,
            Stealth,
            Escort
        }

        // ── DAMAGE TYPE ────────────────────────────────────────────────────────
        public enum DamageType
        {
            Physical,
            Energy,
            Fire,
            Ion,
            Force,
            Sonic
        }

        // ── HIT TYPE ──────────────────────────────────────────────────────────
        public enum HitType
        {
            Miss,
            Normal,
            Critical,
            WeakPoint,
            Headshot
        }

        // ── SAVE SLOT ─────────────────────────────────────────────────────────
        public enum SaveSlot
        {
            QuickSave,
            AutoSave,
            Manual1,
            Manual2,
            Manual3,
            Manual4,
            Manual5
        }
    }
}
