using System.Collections;
using UnityEngine;
using KotORUnity.Core;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Camera
{
    /// <summary>
    /// Manages smooth transitions between Action Camera and RTS Camera.
    /// 
    /// When a mode switch occurs, this controller:
    ///   1. Receives the ModeTransitionStarted event
    ///   2. Runs a 1.5s spline interpolation between camera positions
    ///   3. Flashes the player orientation pulse at the midpoint (t=0.5)
    ///   4. Activates/deactivates the appropriate camera
    /// </summary>
    public class CameraTransitionController : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Camera References")]
        [SerializeField] private UnityEngine.Camera actionCamera;
        [SerializeField] private UnityEngine.Camera rtsCamera;
        [SerializeField] private Transform playerTransform;

        [Header("Orientation Pulse")]
        [SerializeField] private Renderer playerRenderer;
        [SerializeField] private Color pulseColor = new Color(0.2f, 0.8f, 1f, 0.8f);

        // ── STATE ──────────────────────────────────────────────────────────────
        private bool _isTransitioning = false;
        private Coroutine _transitionCoroutine;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            EventBus.Subscribe(EventBus.EventType.ModeTransitionStarted, OnTransitionStarted);
            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnTransitionCompleted);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionStarted, OnTransitionStarted);
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionCompleted, OnTransitionCompleted);
        }

        private void Start()
        {
            // Set initial camera state
            SetCamerasForMode(GameMode.Action);
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnTransitionStarted(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs switchArgs)
            {
                if (_transitionCoroutine != null)
                    StopCoroutine(_transitionCoroutine);
                _transitionCoroutine = StartCoroutine(
                    TransitionRoutine(switchArgs.PreviousMode, switchArgs.NewMode));
            }
        }

        private void OnTransitionCompleted(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs switchArgs)
            {
                SetCamerasForMode(switchArgs.NewMode);
                _isTransitioning = false;
            }
        }

        // ── TRANSITION COROUTINE ───────────────────────────────────────────────
        private IEnumerator TransitionRoutine(GameMode from, GameMode to)
        {
            _isTransitioning = true;

            // Get source and target transforms
            Vector3 startPos = from == GameMode.Action
                ? GetActionCameraPosition()
                : GetRTSCameraPosition();
            Vector3 endPos = to == GameMode.Action
                ? GetActionCameraPosition()
                : GetRTSCameraPosition();

            Quaternion startRot = from == GameMode.Action
                ? GetActionCameraRotation()
                : GetRTSCameraRotation();
            Quaternion endRot = to == GameMode.Action
                ? GetActionCameraRotation()
                : GetRTSCameraRotation();

            // Enable both cameras during transition
            if (actionCamera) actionCamera.enabled = true;
            if (rtsCamera) rtsCamera.enabled = true;

            float elapsed = 0f;
            float duration = GameConstants.MODE_SWITCH_TRANSITION_DURATION;
            bool pulseFired = false;

            // Use a single "transition camera" — the action camera moves to match
            UnityEngine.Camera transCamera = actionCamera ?? rtsCamera;
            if (transCamera == null) yield break;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                transCamera.transform.position = Vector3.Lerp(startPos, endPos, smooth);
                transCamera.transform.rotation = Quaternion.Slerp(startRot, endRot, smooth);

                // Fire orientation pulse at midpoint
                if (!pulseFired && t >= 0.5f)
                {
                    pulseFired = true;
                    StartCoroutine(OrientationPulseRoutine());
                }

                yield return null;
            }

            // Snap to final position
            transCamera.transform.position = endPos;
            transCamera.transform.rotation = endRot;
        }

        // ── CAMERA POSITION CALCULATORS ────────────────────────────────────────
        private Vector3 GetActionCameraPosition()
        {
            if (playerTransform == null) return GameConstants.ACTION_CAMERA_OFFSET;
            return playerTransform.position
                + playerTransform.TransformDirection(GameConstants.ACTION_CAMERA_OFFSET);
        }

        private Quaternion GetActionCameraRotation()
        {
            if (playerTransform == null) return Quaternion.identity;
            return Quaternion.LookRotation(
                (playerTransform.position + Vector3.up * 1.5f) - GetActionCameraPosition());
        }

        private Vector3 GetRTSCameraPosition()
        {
            Vector3 center = playerTransform != null ? playerTransform.position : Vector3.zero;
            return center + new Vector3(0f, GameConstants.RTS_CAMERA_HEIGHT, -GameConstants.RTS_CAMERA_BACK);
        }

        private Quaternion GetRTSCameraRotation()
        {
            Vector3 lookTarget = playerTransform != null ? playerTransform.position : Vector3.zero;
            return Quaternion.LookRotation(lookTarget - GetRTSCameraPosition());
        }

        // ── ORIENTATION PULSE ──────────────────────────────────────────────────
        private IEnumerator OrientationPulseRoutine()
        {
            if (playerRenderer == null) yield break;

            Color originalColor = playerRenderer.material.color;
            playerRenderer.material.color = pulseColor;
            yield return new WaitForSecondsRealtime(GameConstants.ORIENTATION_PULSE_DURATION);
            playerRenderer.material.color = originalColor;
        }

        // ── CAMERA ACTIVATION ─────────────────────────────────────────────────
        private void SetCamerasForMode(GameMode mode)
        {
            if (actionCamera != null) actionCamera.enabled = mode == GameMode.Action;
            if (rtsCamera != null) rtsCamera.enabled = mode == GameMode.RTS;
        }

        public bool IsTransitioning => _isTransitioning;
    }
}
