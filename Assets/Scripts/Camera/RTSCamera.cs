using UnityEngine;
using KotORUnity.Core;

namespace KotORUnity.Camera
{
    /// <summary>
    /// RTS Mode tactical camera.
    /// Elevated isometric/high-angle view with pan, zoom, and rotation.
    /// Automatically frames the squad when entering RTS mode.
    /// </summary>
    public class RTSCamera : MonoBehaviour
    {
        [Header("Boundaries")]
        [SerializeField] private float minHeight = 10f;
        [SerializeField] private float maxHeight = 50f;
        [SerializeField] private float defaultHeight = GameConstants.RTS_CAMERA_HEIGHT;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float zoomSmoothTime = 0.2f;
        [SerializeField] private float minFOV = 25f;
        [SerializeField] private float maxFOV = 60f;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 20f;
        [SerializeField] private float panSmoothTime = 0.15f;

        [Header("Rotation")]
        [SerializeField] private float rotateSpeed = 80f;
        [SerializeField] private float pitchAngle = 55f;  // degrees down from horizontal

        [Header("Auto-frame")]
        [SerializeField] private bool autoFrameSquadOnEnter = true;
        [SerializeField] private Transform[] squadTargets;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private UnityEngine.Camera _cam;

        // ── STATE ──────────────────────────────────────────────────────────────
        private float _targetHeight;
        private float _heightVelocity;
        private Vector3 _targetPosition;
        private Vector3 _positionVelocity;
        private float _fovVelocity;
        private float _yRotation = 0f;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            _targetHeight = defaultHeight;
            _targetPosition = transform.position;
            gameObject.tag = "RTSCamera";
        }

        private void OnEnable()
        {
            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);

            if (autoFrameSquadOnEnter)
                FrameSquad();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
        }

        private void LateUpdate()
        {
            HandleZoomInput();
            HandleRotationInput();
            ApplyPosition();
            ApplyFOV();
        }

        // ── INPUT HANDLERS ─────────────────────────────────────────────────────
        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetHeight = Mathf.Clamp(_targetHeight - scroll * zoomSpeed * 10f, minHeight, maxHeight);
            }
        }

        private void HandleRotationInput()
        {
            if (Input.GetKey(KeyCode.Q)) _yRotation -= rotateSpeed * Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.E)) _yRotation += rotateSpeed * Time.unscaledDeltaTime;
        }

        private void ApplyPosition()
        {
            float targetY = Mathf.SmoothDamp(transform.position.y, _targetHeight, ref _heightVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            Vector3 targetPos = new Vector3(
                _targetPosition.x,
                targetY,
                _targetPosition.z - GameConstants.RTS_CAMERA_BACK);

            transform.position = Vector3.SmoothDamp(
                transform.position, targetPos, ref _positionVelocity, panSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

            // Pitch-down rotation
            transform.rotation = Quaternion.Euler(pitchAngle, _yRotation, 0f);
        }

        private void ApplyFOV()
        {
            if (_cam == null) return;
            // FOV decreases as camera zooms in (height decreases)
            float heightRatio = (_targetHeight - minHeight) / (maxHeight - minHeight);
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, heightRatio);
            _cam.fieldOfView = Mathf.SmoothDamp(
                _cam.fieldOfView, targetFOV, ref _fovVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        // ── AUTO-FRAME ─────────────────────────────────────────────────────────
        /// <summary>Automatically frame the camera to center on the squad.</summary>
        public void FrameSquad()
        {
            if (squadTargets == null || squadTargets.Length == 0) return;

            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var target in squadTargets)
            {
                if (target != null) { center += target.position; count++; }
            }
            if (count > 0) center /= count;

            _targetPosition = center;
        }

        // ── EVENT HANDLERS ─────────────────────────────────────────────────────
        private void OnModeChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs switchArgs &&
                switchArgs.NewMode == Core.GameEnums.GameMode.RTS)
            {
                if (autoFrameSquadOnEnter) FrameSquad();
            }
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public void PanTo(Vector3 position) => _targetPosition = position;
        public void SetSquadTargets(Transform[] targets) => squadTargets = targets;
    }
}
