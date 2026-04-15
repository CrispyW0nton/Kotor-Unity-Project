using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using KotORUnity.KotOR.Parsers;

namespace KotORUnity.World
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ANIMATOR BRIDGE
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Converts the AnimationClips produced by MdlBuilder.BuildAnimations() into
    //  a runtime Unity Animator so that KotOR character models can play back
    //  walk / run / idle / attack / die animations without needing pre-built
    //  AnimatorController assets.
    //
    //  Architecture
    //  ────────────
    //  • One AnimatorBridge per character GameObject (player, companion, enemy).
    //  • On Awake it reads any pre-loaded clips from ModelLoader (via
    //    PendingClipsTag), or accepts them via LoadClips().
    //  • Exposes a simple Play(stateName) / CrossFade(stateName, duration) API
    //    that mirrors the Unity Animator interface so callers don't need to know
    //    whether they're running the legacy Animation component or Animator.
    //  • Falls back to the legacy Animation component if no Animator is present,
    //    because KotOR models are very old and bone counts are small.
    //
    //  State Names (standard set matching KotOR animation names)
    //  ─────────────────────────────────────────────────────────
    //    "idle"          — standing idle loop
    //    "walk"          — walking loop
    //    "run"           — running loop
    //    "attack"        — melee attack (once)
    //    "attack2"       — second melee variant
    //    "fire"           — ranged fire (once)
    //    "die"           — death (once)
    //    "dead"          — lying dead loop (after die)
    //    "talk"          — dialogue lip animation loop
    //    "pause"         — pause gesture
    //    "getup"         — getting up after knockdown

    /// <summary>
    /// Attach this to any KotOR character GO loaded by <see cref="ModelLoader"/>.
    /// Call <see cref="Play"/> or <see cref="CrossFade"/> to drive animations.
    /// </summary>
    public class AnimatorBridge : MonoBehaviour
    {
        // ── CONSTANTS ──────────────────────────────────────────────────────────
        public const string IDLE    = "idle";
        public const string WALK    = "walk";
        public const string RUN     = "run";
        public const string ATTACK  = "attack";
        public const string FIRE    = "fire";
        public const string DIE     = "die";
        public const string DEAD    = "dead";
        public const string TALK    = "talk";
        public const string PAUSE2  = "pause";
        public const string GETUP   = "getup";

        // ── STATE ──────────────────────────────────────────────────────────────
        private Animation                        _legacyAnim;
        private Animator                         _animator;
        private NavMeshAgent                     _navAgent;     // optional, drives auto-locomotion
        private Dictionary<string, AnimationClip> _clips
            = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        private string  _currentState    = "";
        private bool    _ready           = false;

        // ── BLEND-TREE LOCOMOTION ──────────────────────────────────────────────
        // When autoLocomotion is true, AnimatorBridge polls the NavMeshAgent speed
        // each frame and calls UpdateLocomotion automatically.
        [Header("Auto Locomotion")]
        [Tooltip("If true, automatically drives idle/walk/run from the NavMeshAgent speed.")]
        [SerializeField] private bool autoLocomotion     = true;
        [Tooltip("Speed threshold above which 'run' plays instead of 'walk'.")]
        [SerializeField] private float runSpeedThreshold = 3.5f;
        [Tooltip("Speed threshold above which 'walk' plays (below = idle).")]
        [SerializeField] private float walkSpeedThreshold = 0.15f;
        [Tooltip("Cross-fade blend time between locomotion states (seconds).")]
        [SerializeField] private float locomotionBlend   = 0.2f;

        // Blend-tree smoothing — avoids constant clip switching on micro-movements
        private float  _smoothedSpeed    = 0f;
        private float  _speedSmoothing   = 8f;    // lerp factor (per-second)

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<string> OnStateChanged;
        public event Action         OnDeathAnimFinished;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            _legacyAnim = GetComponentInChildren<Animation>();
            _animator   = GetComponentInChildren<Animator>();
            _navAgent   = GetComponent<NavMeshAgent>();

            // Absorb any clips pre-loaded by ModelLoader
            var tag = GetComponent<PendingClipsTag>();
            if (tag != null)
            {
                LoadClips(tag.Clips);
                Destroy(tag);
            }
        }

        private void Update()
        {
            if (!_ready || !autoLocomotion) return;

            // Determine raw speed from NavMeshAgent or Rigidbody or transform delta
            float rawSpeed = 0f;
            if (_navAgent != null && _navAgent.isActiveAndEnabled)
                rawSpeed = _navAgent.velocity.magnitude;
            else
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rawSpeed = rb.velocity.magnitude;
            }

            // Smooth speed to prevent jitter
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeed,
                Time.deltaTime * _speedSmoothing);

            // Drive locomotion blend
            UpdateLocomotionBlended(_smoothedSpeed);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Supply animation clips after the model has been instantiated.
        /// Typically called by <see cref="ModelLoader"/> right after it builds the GO.
        /// </summary>
        public void LoadClips(Dictionary<string, AnimationClip> clips)
        {
            if (clips == null || clips.Count == 0) return;

            _clips = new Dictionary<string, AnimationClip>(clips,
                StringComparer.OrdinalIgnoreCase);

            // Register all clips onto the legacy Animation component (fastest path)
            if (_legacyAnim == null)
                _legacyAnim = gameObject.AddComponent<Animation>();

            foreach (var kv in _clips)
            {
                if (_legacyAnim.GetClip(kv.Key) == null)
                    _legacyAnim.AddClip(kv.Value, kv.Key);
            }

            // Set idle as default if it exists
            var idleClip = FindClip(IDLE);
            if (idleClip != null)
                _legacyAnim.clip = idleClip;

            _ready = true;
            Play(IDLE);
            Debug.Log($"[AnimatorBridge] {gameObject.name}: loaded {_clips.Count} clips.");
        }

        /// <summary>
        /// Play a named animation immediately (no blend).
        /// If the exact name is not found, the nearest match is used.
        /// </summary>
        public void Play(string stateName)
        {
            if (!_ready || string.IsNullOrEmpty(stateName)) return;

            string resolved = Resolve(stateName);
            if (string.IsNullOrEmpty(resolved)) return;
            if (resolved == _currentState && IsPlaying(resolved)) return;

            _currentState = resolved;

            if (_legacyAnim != null && _legacyAnim.GetClip(resolved) != null)
            {
                _legacyAnim.Play(resolved);
            }
            else if (_animator != null)
            {
                _animator.Play(resolved);
            }

            OnStateChanged?.Invoke(resolved);

            // Special: watch for death animation end
            if (resolved == DIE || resolved.StartsWith("die"))
                StartCoroutine(WaitForClipEnd(resolved, OnDeathAnimFinished));
        }

        /// <summary>
        /// Cross-fade to a named animation over <paramref name="fadeDuration"/> seconds.
        /// Falls back to immediate Play if legacy Animation is used.
        /// </summary>
        public void CrossFade(string stateName, float fadeDuration = 0.25f)
        {
            if (!_ready || string.IsNullOrEmpty(stateName)) return;

            string resolved = Resolve(stateName);
            if (string.IsNullOrEmpty(resolved)) return;
            if (resolved == _currentState && IsPlaying(resolved)) return;

            _currentState = resolved;

            if (_legacyAnim != null && _legacyAnim.GetClip(resolved) != null)
            {
                _legacyAnim.CrossFade(resolved, fadeDuration);
            }
            else if (_animator != null)
            {
                _animator.CrossFadeInFixedTime(resolved, fadeDuration);
            }

            OnStateChanged?.Invoke(resolved);
        }

        /// <summary>Stop all animations.</summary>
        public void Stop()
        {
            _legacyAnim?.Stop();
            _currentState = "";
        }

        /// <summary>True if the named (or resolved) animation is currently playing.</summary>
        public bool IsPlaying(string stateName)
        {
            if (string.IsNullOrEmpty(stateName)) return false;
            string resolved = Resolve(stateName);
            if (_legacyAnim != null) return _legacyAnim.IsPlaying(resolved);
            return false;
        }

        public string CurrentState => _currentState;
        public bool   IsReady      => _ready;
        public IReadOnlyDictionary<string, AnimationClip> Clips => _clips;

        // ── COMBAT / MOVEMENT HELPERS ─────────────────────────────────────────

        /// <summary>
        /// Convenience: play idle, walk or run based on agent speed (immediate snap).
        /// For smooth blending call <see cref="UpdateLocomotionBlended"/> instead.
        /// </summary>
        public void UpdateLocomotion(float speed)
        {
            if (speed < walkSpeedThreshold)  CrossFade(IDLE, locomotionBlend);
            else if (speed < runSpeedThreshold) CrossFade(WALK, locomotionBlend);
            else                             CrossFade(RUN,  locomotionBlend);
        }

        /// <summary>
        /// Locomotion blend-tree: chooses idle / walk / run with smoothed crossfades,
        /// and suppresses transitions when the delta is below the threshold so
        /// micro-movements don't cause constant clip switching.
        /// </summary>
        public void UpdateLocomotionBlended(float speed)
        {
            string target;
            if      (speed < walkSpeedThreshold)  target = IDLE;
            else if (speed < runSpeedThreshold)   target = WALK;
            else                                  target = RUN;

            if (target != _currentState)
                CrossFade(target, locomotionBlend);
        }

        /// <summary>
        /// Manually set the NavMeshAgent this bridge should poll for speed.
        /// Call this if the agent is added after Awake.
        /// </summary>
        public void SetNavMeshAgent(NavMeshAgent agent)
        {
            _navAgent = agent;
        }

        /// <summary>Enable or disable automatic locomotion from NavMeshAgent speed.</summary>
        public void SetAutoLocomotion(bool enabled) => autoLocomotion = enabled;

        /// <summary>Play the attack animation appropriate to the weapon type.</summary>
        public void PlayAttack(bool ranged = false)
        {
            CrossFade(ranged ? FIRE : ATTACK, 0.1f);
        }

        /// <summary>Trigger death sequence (die → dead loop).</summary>
        public void PlayDeath()
        {
            Play(DIE);
            StartCoroutine(ChainAfterClip(DIE, DEAD));
        }

        // ── PRIVATE HELPERS ───────────────────────────────────────────────────

        /// <summary>
        /// Resolve a semantic state name to an actual clip name via fuzzy matching.
        /// KotOR animation names often include suffixes: "walk_fwd", "attack01", etc.
        /// </summary>
        private string Resolve(string stateName)
        {
            // Exact match first
            if (_clips.ContainsKey(stateName)) return stateName;

            // Prefix match
            foreach (var key in _clips.Keys)
                if (key.StartsWith(stateName, StringComparison.OrdinalIgnoreCase))
                    return key;

            // Contains match
            foreach (var key in _clips.Keys)
                if (key.IndexOf(stateName, StringComparison.OrdinalIgnoreCase) >= 0)
                    return key;

            // Fallback: if no match at all, use idle or first clip
            if (_clips.ContainsKey(IDLE)) return IDLE;
            foreach (var key in _clips.Keys) return key;  // first available

            return "";
        }

        private AnimationClip FindClip(string name)
        {
            _clips.TryGetValue(Resolve(name), out var clip);
            return clip;
        }

        private IEnumerator WaitForClipEnd(string clipName, Action callback)
        {
            var clip = FindClip(clipName);
            if (clip == null) { callback?.Invoke(); yield break; }

            float length = clip.length;
            yield return new WaitForSeconds(length);
            callback?.Invoke();
        }

        private IEnumerator ChainAfterClip(string first, string next)
        {
            var clip = FindClip(first);
            float wait = clip != null ? clip.length : 1f;
            yield return new WaitForSeconds(wait);
            CrossFade(next);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  PENDING CLIPS TAG  (attached by ModelLoader, consumed by AnimatorBridge)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Temporary component placed on a freshly instantiated model GO by
    /// <see cref="ModelLoader"/> to carry the parsed animation clips until
    /// <see cref="AnimatorBridge.Awake"/> can pick them up.
    /// </summary>
    public class PendingClipsTag : MonoBehaviour
    {
        public Dictionary<string, AnimationClip> Clips =
            new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
    }
}
