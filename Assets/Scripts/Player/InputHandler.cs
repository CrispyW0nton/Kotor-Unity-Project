using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.Player
{
    /// <summary>
    /// Handles all player input and routes it to the appropriate
    /// mode-specific controller (Action or RTS).
    /// 
    /// Input bindings:
    ///   Tab           → Mode switch
    ///   Space         → RTS Pause/Unpause
    ///   WASD/Arrows   → Movement (Action) / Camera pan (RTS)
    ///   Mouse1        → Fire (Action) / Select unit (RTS)
    ///   Mouse2        → Aim (Action) / Issue move order (RTS)
    ///   1,2,3         → Ability slots
    ///   F5            → Quick Save
    ///   F9            → Quick Load
    ///   Escape        → Menu
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        // ── REFERENCES ─────────────────────────────────────────────────────────
        private ModeSwitchSystem _modeSwitchSystem;
        private ActionPlayerController _actionController;
        private RTSPlayerController _rtsController;

        // ── CONFIGURATION ──────────────────────────────────────────────────────
        [Header("Key Bindings")]
        [SerializeField] private KeyCode modeSwitchKey = KeyCode.Tab;
        [SerializeField] private KeyCode pauseKey = KeyCode.Space;
        [SerializeField] private KeyCode ability1Key = KeyCode.Alpha1;
        [SerializeField] private KeyCode ability2Key = KeyCode.Alpha2;
        [SerializeField] private KeyCode ability3Key = KeyCode.Alpha3;
        [SerializeField] private KeyCode quickSaveKey = KeyCode.F5;
        [SerializeField] private KeyCode quickLoadKey = KeyCode.F9;

        [Header("Mouse")]
        [SerializeField] private string fireButton = "Fire1";
        [SerializeField] private string aimButton = "Fire2";

        // ── CACHED STATE ───────────────────────────────────────────────────────
        private bool _inputEnabled = true;

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            _modeSwitchSystem = FindObjectOfType<ModeSwitchSystem>();
            _actionController = GetComponent<ActionPlayerController>();
            _rtsController = GetComponent<RTSPlayerController>();
        }

        private void OnEnable()
        {
            if (_modeSwitchSystem != null)
            {
                _modeSwitchSystem.OnModeTransitionStarted += OnTransitionStarted;
                _modeSwitchSystem.OnModeTransitionCompleted += OnTransitionCompleted;
            }
        }

        private void OnDisable()
        {
            if (_modeSwitchSystem != null)
            {
                _modeSwitchSystem.OnModeTransitionStarted -= OnTransitionStarted;
                _modeSwitchSystem.OnModeTransitionCompleted -= OnTransitionCompleted;
            }
        }

        private void Update()
        {
            if (!_inputEnabled) return;

            HandleModeSwitch();
            HandlePause();
            HandleAbilities();
            HandleSave();

            // Route movement / combat input to the active controller
            if (_modeSwitchSystem != null && !_modeSwitchSystem.IsTransitioning)
            {
                if (_modeSwitchSystem.CurrentMode == GameMode.Action)
                    HandleActionInput();
                else
                    HandleRTSInput();
            }
        }

        // ── MODE SWITCH INPUT ──────────────────────────────────────────────────
        private void HandleModeSwitch()
        {
            if (Input.GetKeyDown(modeSwitchKey))
            {
                _modeSwitchSystem?.TrySwitchMode();
            }
        }

        // ── PAUSE INPUT (RTS only) ─────────────────────────────────────────────
        private void HandlePause()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                _modeSwitchSystem?.TryTogglePause();
            }
        }

        // ── ABILITY INPUT ──────────────────────────────────────────────────────
        private void HandleAbilities()
        {
            if (Input.GetKeyDown(ability1Key)) TriggerAbility(0);
            if (Input.GetKeyDown(ability2Key)) TriggerAbility(1);
            if (Input.GetKeyDown(ability3Key)) TriggerAbility(2);
        }

        private void TriggerAbility(int slot)
        {
            GameMode mode = _modeSwitchSystem?.CurrentMode ?? GameMode.Action;
            if (mode == GameMode.Action)
                _actionController?.UseAbility(slot);
            else
                _rtsController?.QueueAbility(slot);
        }

        // ── ACTION INPUT ───────────────────────────────────────────────────────
        private void HandleActionInput()
        {
            if (_actionController == null) return;

            // Movement
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            _actionController.SetMovementInput(h, v);

            // Fire
            if (Input.GetButton(fireButton))
                _actionController.FireWeapon();

            // Aim down sights
            _actionController.SetAiming(Input.GetButton(aimButton));

            // Mouse look
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            _actionController.SetLookInput(mouseX, mouseY);
        }

        // ── RTS INPUT ──────────────────────────────────────────────────────────
        private void HandleRTSInput()
        {
            if (_rtsController == null) return;

            // Camera pan with keyboard
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            _rtsController.PanCamera(h, v);

            // Right-click to issue move order
            if (Input.GetMouseButtonDown(1))
                _rtsController.IssueMouseClickOrder();

            // Left-click to select unit
            if (Input.GetMouseButtonDown(0))
                _rtsController.SelectUnit();
        }

        // ── SAVE INPUT ─────────────────────────────────────────────────────────
        private void HandleSave()
        {
            if (Input.GetKeyDown(quickSaveKey))
            {
                var saveManager = FindObjectOfType<SaveSystem.SaveManager>();
                saveManager?.QuickSave();
            }
            if (Input.GetKeyDown(quickLoadKey))
            {
                var saveManager = FindObjectOfType<SaveSystem.SaveManager>();
                saveManager?.QuickLoad();
            }
        }

        // ── TRANSITION CALLBACKS ───────────────────────────────────────────────
        private void OnTransitionStarted(GameMode from, GameMode to)
        {
            // Lock input during transition
            _inputEnabled = false;
        }

        private void OnTransitionCompleted(GameMode newMode)
        {
            // Restore input after transition
            _inputEnabled = true;
        }

        // ── PUBLIC API ─────────────────────────────────────────────────────────
        public void SetInputEnabled(bool enabled) => _inputEnabled = enabled;
    }
}
