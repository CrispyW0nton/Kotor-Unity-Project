using System;
using System.Collections;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Core
{
    /// <summary>
    /// The heart of the MRL_GameForge v2 hybrid gameplay system.
    /// 
    /// Manages transitions between Action Mode and RTS Mode, including:
    ///   - Mode switch cooldown (2s)
    ///   - Vulnerability window during transition (+30% damage taken for 1s)
    ///   - Time scale management (RTS = 10%, Action = 100%)
    ///   - Camera transition coordination
    ///   - Pause system with cooldown (RTS mode only)
    ///   - Enemy adaptation stack management (pause-scum counter)
    ///   - Mode affinity tracking (XP bonuses for under-used modes)
    /// 
    /// Subscribe to EventBus.EventType.ModeSwitch to react to mode changes.
    /// </summary>
    public class ModeSwitchSystem : MonoBehaviour
    {
        // ── STATE ──────────────────────────────────────────────────────────────
        private GameMode _currentMode;
        private ModeTransitionState _transitionState = ModeTransitionState.Idle;
        private bool _isPaused = false;

        // Timers (unscaled time to resist time dilation)
        private float _switchCooldownRemaining = 0f;
        private float _pauseCooldownRemaining = 0f;
        private float _vulnerabilityTimeRemaining = 0f;

        // Vulnerability (damage multiplier applied during switch)
        private bool _isVulnerable = false;

        // Mode affinity tracking
        private int _encountersSinceRTS = 0;
        private int _encountersSinceAction = 0;

        // Pause adaptation tracking (enemy stacks reset)
        private float _adaptationResetTimer = 0f;
        private int _pauseCycleCount = 0;

        // ── PROPERTIES ─────────────────────────────────────────────────────────
        public GameMode CurrentMode => _currentMode;
        public ModeTransitionState TransitionState => _transitionState;
        public bool IsPaused => _isPaused;
        public bool IsTransitioning => _transitionState != ModeTransitionState.Idle;
        public bool IsVulnerable => _isVulnerable;
        public float SwitchCooldownRemaining => _switchCooldownRemaining;
        public float PauseCooldownRemaining => _pauseCooldownRemaining;
        public float CurrentDamageTakenMultiplier => _isVulnerable
            ? GameConstants.MODE_SWITCH_VULNERABILITY_MULTIPLIER
            : 1.0f;

        // ── EVENTS (direct C# — complement the EventBus) ──────────────────────
        public event Action<GameMode, GameMode> OnModeTransitionStarted;
        public event Action<GameMode> OnModeTransitionCompleted;
        public event Action OnPaused;
        public event Action OnResumed;

        // ── INITIALIZATION ─────────────────────────────────────────────────────
        public void Initialize(GameMode startingMode)
        {
            _currentMode = startingMode;
            _transitionState = ModeTransitionState.Idle;
            _isPaused = false;
            ApplyTimeScale();
        }

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Update()
        {
            // All timers use REAL time (unscaled) so they work inside time dilation
            float dt = Time.unscaledDeltaTime;

            // Tick down cooldowns
            if (_switchCooldownRemaining > 0f)
                _switchCooldownRemaining = Mathf.Max(0f, _switchCooldownRemaining - dt);

            if (_pauseCooldownRemaining > 0f)
                _pauseCooldownRemaining = Mathf.Max(0f, _pauseCooldownRemaining - dt);

            if (_vulnerabilityTimeRemaining > 0f)
            {
                _vulnerabilityTimeRemaining = Mathf.Max(0f, _vulnerabilityTimeRemaining - dt);
                if (_vulnerabilityTimeRemaining <= 0f)
                    _isVulnerable = false;
            }

            // Enemy adaptation reset (tracks real time since last pause)
            if (_pauseCycleCount > 0)
            {
                _adaptationResetTimer += dt;
                if (_adaptationResetTimer >= GameConstants.ENEMY_ADAPTATION_RESET_TIME)
                {
                    _pauseCycleCount = 0;
                    _adaptationResetTimer = 0f;
                    EventBus.Publish(EventBus.EventType.EnemyAdaptationStackAdded,
                        new EventBus.GameEventArgs()); // signal reset
                }
            }
        }

        // ── MODE SWITCH ────────────────────────────────────────────────────────
        /// <summary>
        /// Request a switch to the opposite mode.
        /// Returns false if on cooldown or already transitioning.
        /// </summary>
        public bool TrySwitchMode()
        {
            if (IsTransitioning)
            {
                Debug.Log("[ModeSwitchSystem] Cannot switch — already transitioning.");
                return false;
            }
            if (_switchCooldownRemaining > 0f)
            {
                Debug.Log($"[ModeSwitchSystem] Cannot switch — cooldown: {_switchCooldownRemaining:F1}s remaining.");
                return false;
            }

            GameMode targetMode = _currentMode == GameMode.Action ? GameMode.RTS : GameMode.Action;
            StartCoroutine(TransitionRoutine(targetMode));
            return true;
        }

        /// <summary>Force-switch to a specific mode (bypasses cooldown — use carefully).</summary>
        public void ForceSwitch(GameMode targetMode)
        {
            StopAllCoroutines();
            _transitionState = ModeTransitionState.Idle;
            CompleteTransition(targetMode);
        }

        // ── TRANSITION COROUTINE ───────────────────────────────────────────────
        private IEnumerator TransitionRoutine(GameMode targetMode)
        {
            // Begin transition
            GameMode previousMode = _currentMode;
            _transitionState = targetMode == GameMode.RTS
                ? ModeTransitionState.TransitioningToRTS
                : ModeTransitionState.TransitioningToAction;

            // Apply vulnerability window
            _isVulnerable = true;
            _vulnerabilityTimeRemaining = GameConstants.MODE_SWITCH_VULNERABILITY_DURATION;

            // Notify systems (camera transition starts here)
            OnModeTransitionStarted?.Invoke(previousMode, targetMode);
            EventBus.Publish(EventBus.EventType.ModeTransitionStarted,
                new EventBus.ModeSwitchEventArgs(previousMode, targetMode));

            // Begin time scale shift
            StartCoroutine(LerpTimeScale(
                targetMode == GameMode.RTS ? GameConstants.RTS_TIME_SCALE : GameConstants.ACTION_TIME_SCALE,
                GameConstants.TIME_SCALE_TRANSITION_DURATION));

            // Wait for camera + transition animation (unscaled)
            yield return new WaitForSecondsRealtime(GameConstants.MODE_SWITCH_TRANSITION_DURATION);

            CompleteTransition(targetMode);
        }

        private void CompleteTransition(GameMode newMode)
        {
            _currentMode = newMode;
            _transitionState = ModeTransitionState.Idle;
            _switchCooldownRemaining = GameConstants.MODE_SWITCH_COOLDOWN;

            // If entering RTS, also end any existing pause (fresh start)
            if (newMode == GameMode.RTS && _isPaused)
                _isPaused = false;

            ApplyTimeScale();

            OnModeTransitionCompleted?.Invoke(newMode);
            EventBus.Publish(EventBus.EventType.ModeTransitionCompleted,
                new EventBus.ModeSwitchEventArgs(
                    newMode == GameMode.RTS ? GameMode.Action : GameMode.RTS,
                    newMode));

            EventBus.Publish(EventBus.EventType.ModeSwitch,
                new EventBus.ModeSwitchEventArgs(
                    newMode == GameMode.RTS ? GameMode.Action : GameMode.RTS,
                    newMode));

            Debug.Log($"[ModeSwitchSystem] Mode switched to: {newMode}");
        }

        // ── PAUSE (RTS ONLY) ───────────────────────────────────────────────────
        /// <summary>
        /// Toggle pause in RTS mode. Subject to 5s cooldown after unpause.
        /// Does nothing in Action mode.
        /// </summary>
        public bool TryTogglePause()
        {
            if (_currentMode != GameMode.RTS)
            {
                Debug.Log("[ModeSwitchSystem] Pause only available in RTS mode.");
                return false;
            }
            if (_pauseCooldownRemaining > 0f && !_isPaused)
            {
                Debug.Log($"[ModeSwitchSystem] Pause on cooldown: {_pauseCooldownRemaining:F1}s remaining.");
                return false;
            }

            if (_isPaused)
                Unpause();
            else
                Pause();

            return true;
        }

        private void Pause()
        {
            _isPaused = true;
            Time.timeScale = 0f;
            OnPaused?.Invoke();
            EventBus.Publish(EventBus.EventType.GamePaused);

            // Increment adaptation counter
            _pauseCycleCount++;
            _adaptationResetTimer = 0f;

            Debug.Log($"[ModeSwitchSystem] RTS paused. Adaptation stacks: {_pauseCycleCount}");
        }

        private void Unpause()
        {
            _isPaused = false;
            ApplyTimeScale(); // Restore 10% time scale for RTS
            _pauseCooldownRemaining = GameConstants.RTS_PAUSE_COOLDOWN;

            OnResumed?.Invoke();
            EventBus.Publish(EventBus.EventType.GameResumed);
            Debug.Log("[ModeSwitchSystem] RTS unpaused.");
        }

        // ── TIME SCALE MANAGEMENT ──────────────────────────────────────────────
        private void ApplyTimeScale()
        {
            if (_isPaused) return;
            Time.timeScale = _currentMode == GameMode.RTS
                ? GameConstants.RTS_TIME_SCALE
                : GameConstants.ACTION_TIME_SCALE;
        }

        private IEnumerator LerpTimeScale(float targetScale, float duration)
        {
            float startScale = Time.timeScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Time.timeScale = Mathf.Lerp(startScale, targetScale, Mathf.SmoothStep(0, 1, t));
                yield return null;
            }
            Time.timeScale = targetScale;
        }

        // ── MODE AFFINITY TRACKING ─────────────────────────────────────────────
        /// <summary>
        /// Call at the start of each encounter to update mode affinity counters.
        /// The system tracks which mode is underused and grants XP bonuses.
        /// </summary>
        public void RecordEncounterMode(GameMode usedMode)
        {
            if (usedMode == GameMode.RTS)
            {
                _encountersSinceRTS = 0;
                _encountersSinceAction++;
            }
            else
            {
                _encountersSinceAction = 0;
                _encountersSinceRTS++;
            }
        }

        /// <summary>
        /// Returns true if the mode affinity XP bonus should apply for the given mode.
        /// Bonus triggers when the OTHER mode has been used for N+ consecutive encounters.
        /// </summary>
        public bool IsAffinityBonusActive(GameMode mode)
        {
            int encountersSinceThisMode = mode == GameMode.RTS
                ? _encountersSinceRTS
                : _encountersSinceAction;
            return encountersSinceThisMode >= GameConstants.MODE_AFFINITY_ENCOUNTER_THRESHOLD;
        }

        // ── PAUSE COUNT (for enemy adaptation) ────────────────────────────────
        /// <summary>Number of pause cycles since last adaptation reset.</summary>
        public int PauseCycleCount => _pauseCycleCount;
    }
}
