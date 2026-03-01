using UnityEngine;
using KotORUnity.Core;

namespace KotORUnity.Camera
{
    /// <summary>
    /// Action Mode third-person camera.
    /// Follows the player character over the shoulder with optional ADS zoom.
    /// Implements smooth lag and collision avoidance.
    /// </summary>
    public class ActionCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = GameConstants.ACTION_CAMERA_OFFSET;
        [SerializeField] private float shoulderOffset = 0.5f;

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothTime = 0.1f;
        [SerializeField] private float rotationSmoothTime = 0.08f;

        [Header("ADS")]
        [SerializeField] private float normalFOV = 70f;
        [SerializeField] private float aimFOV = 45f;
        [SerializeField] private float fovSmoothTime = 0.1f;

        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask;
        [SerializeField] private float collisionRadius = 0.2f;
        [SerializeField] private float minDistance = 0.5f;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private UnityEngine.Camera _cam;

        // ── STATE ──────────────────────────────────────────────────────────────
        private Vector3 _positionVelocity;
        private float _fovVelocity;
        private bool _isAiming = false;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _cam = GetComponent<UnityEngine.Camera>();
            gameObject.tag = "ActionCamera";
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdatePosition();
            UpdateFOV();
        }

        // ── POSITION ───────────────────────────────────────────────────────────
        private void UpdatePosition()
        {
            Vector3 desiredOffset = _isAiming
                ? new Vector3(shoulderOffset, offset.y, offset.z * 0.7f)
                : offset;

            Vector3 desiredPos = target.position + target.TransformDirection(desiredOffset);

            // Collision detection — push camera forward if something is in the way
            Vector3 dirToCamera = (desiredPos - target.position).normalized;
            float distance = Vector3.Distance(target.position, desiredPos);

            if (Physics.SphereCast(target.position, collisionRadius, dirToCamera,
                out RaycastHit hit, distance, collisionMask))
            {
                float adjustedDistance = Mathf.Max(hit.distance, minDistance);
                desiredPos = target.position + dirToCamera * adjustedDistance;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPos, ref _positionVelocity, positionSmoothTime);

            // Always look toward player's head height
            Vector3 lookTarget = target.position + Vector3.up * 1.5f;
            Quaternion desiredRot = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationSmoothTime / Time.deltaTime);
        }

        // ── FOV ────────────────────────────────────────────────────────────────
        private void UpdateFOV()
        {
            if (_cam == null) return;
            float targetFOV = _isAiming ? aimFOV : normalFOV;
            _cam.fieldOfView = Mathf.SmoothDamp(
                _cam.fieldOfView, targetFOV, ref _fovVelocity, fovSmoothTime);
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public void SetAiming(bool aiming) => _isAiming = aiming;
        public void SetTarget(Transform t) => target = t;
    }
}
