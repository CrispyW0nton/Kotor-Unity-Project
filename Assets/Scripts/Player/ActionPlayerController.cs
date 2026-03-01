using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Abilities;
using KotORUnity.Weapons;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Player
{
    /// <summary>
    /// Action Mode player controller.
    /// Handles third-person movement, aiming, firing, and direct ability use.
    /// Requires a CharacterController component.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ActionPlayerController : MonoBehaviour
    {
        // ── INSPECTOR ──────────────────────────────────────────────────────────
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float sprintMultiplier = 1.6f;
        [SerializeField] private float gravity = -15f;
        [SerializeField] private float jumpHeight = 1.5f;

        [Header("Look / Aim")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float aimFOV = 45f;
        [SerializeField] private float normalFOV = 70f;
        [SerializeField] private Transform cameraTarget;

        [Header("Combat")]
        [SerializeField] private WeaponBase equippedWeapon;
        [SerializeField] private List<AbilityBase> abilities = new List<AbilityBase>();

        [Header("Dodge")]
        [SerializeField] private float dodgeCooldown = 1.5f;
        [SerializeField] private float dodgeDistance = 3f;
        [SerializeField] private float dodgeDuration = 0.25f;

        // ── COMPONENTS ─────────────────────────────────────────────────────────
        private CharacterController _cc;
        private Camera _mainCamera;
        private PlayerStats _stats;

        // ── STATE ──────────────────────────────────────────────────────────────
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private Vector3 _velocity;
        private float _yaw = 0f;
        private float _pitch = 0f;
        private bool _isAiming = false;
        private bool _isGrounded;
        private float _dodgeCooldownRemaining = 0f;
        private bool _isDodging = false;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stats = GetComponent<PlayerStatsBehaviour>()?.Stats;
            _mainCamera = Camera.main;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (!IsActiveMode()) return;

            TickCooldowns();
            HandleGravity();
            HandleMovement();
            HandleLook();
        }

        // ── INPUT SETTERS (called by InputHandler) ─────────────────────────────
        public void SetMovementInput(float h, float v) => _moveInput = new Vector2(h, v);
        public void SetLookInput(float x, float y) => _lookInput = new Vector2(x, y);
        public void SetAiming(bool aiming) => _isAiming = aiming;

        // ── MOVEMENT ───────────────────────────────────────────────────────────
        private void HandleMovement()
        {
            if (_isDodging) return;

            _isGrounded = _cc.isGrounded;
            if (_isGrounded && _velocity.y < 0f)
                _velocity.y = -2f;

            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 move = (forward * _moveInput.y + right * _moveInput.x).normalized;

            bool isSprinting = Input.GetKey(KeyCode.LeftShift) && !_isAiming;
            float speed = moveSpeed * (isSprinting ? sprintMultiplier : 1f);

            _cc.Move(move * speed * Time.deltaTime);
        }

        private void HandleGravity()
        {
            _isGrounded = _cc.isGrounded;
            _velocity.y += gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }

        private void HandleLook()
        {
            _yaw += _lookInput.x * mouseSensitivity;
            _pitch -= _lookInput.y * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch, -60f, 60f);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraTarget != null)
                cameraTarget.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        // ── COMBAT ─────────────────────────────────────────────────────────────
        public void FireWeapon()
        {
            if (equippedWeapon == null) return;
            equippedWeapon.Fire(transform, _isAiming);
        }

        public void UseAbility(int slot)
        {
            if (slot < 0 || slot >= abilities.Count) return;
            var ability = abilities[slot];
            if (ability == null || !ability.IsReady) return;

            // Find target via raycast
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
            GameObject target = null;
            if (Physics.Raycast(ray, out RaycastHit hit, 50f))
                target = hit.collider.gameObject;

            ability.Execute(gameObject, target, GameMode.Action);
        }

        /// <summary>Dodge roll — consumes stamina, grants brief invincibility.</summary>
        public void TryDodge(Vector2 direction)
        {
            if (_dodgeCooldownRemaining > 0f || _isDodging) return;
            if (_stats != null && _stats.Stamina < 20f) return;

            StartCoroutine(DodgeRoutine(direction));
        }

        private System.Collections.IEnumerator DodgeRoutine(Vector2 dir)
        {
            _isDodging = true;
            _dodgeCooldownRemaining = dodgeCooldown;

            Vector3 dodgeDir = (transform.forward * dir.y + transform.right * dir.x).normalized;
            float elapsed = 0f;

            while (elapsed < dodgeDuration)
            {
                _cc.Move(dodgeDir * (dodgeDistance / dodgeDuration) * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            _isDodging = false;
        }

        // ── HELPERS ────────────────────────────────────────────────────────────
        private void TickCooldowns()
        {
            if (_dodgeCooldownRemaining > 0f)
                _dodgeCooldownRemaining -= Time.deltaTime;
        }

        private bool IsActiveMode()
        {
            var msSystem = GameManager.Instance?.GetComponent<ModeSwitchSystem>()
                ?? FindObjectOfType<ModeSwitchSystem>();
            return msSystem == null || msSystem.CurrentMode == GameMode.Action;
        }

        // ── PUBLIC ACCESSORS ───────────────────────────────────────────────────
        public WeaponBase EquippedWeapon => equippedWeapon;
        public bool IsAiming => _isAiming;
        public bool IsDodging => _isDodging;
    }
}
