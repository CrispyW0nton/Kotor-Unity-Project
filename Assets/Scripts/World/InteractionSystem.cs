using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using KotORUnity.Core;
using KotORUnity.Dialogue;
using KotORUnity.Player;
using KotORUnity.World;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.World
{
    // ══════════════════════════════════════════════════════════════════════════
    //  INTERACTABLE  —  component placed on any NPC, door, container, placeable
    // ══════════════════════════════════════════════════════════════════════════

    public enum InteractableType
    {
        NPC,            // Trigger dialogue / conversation
        Door,           // Open/close or trigger area transition
        Container,      // Loot chest / footlocker
        Placeable,      // Terminals, cantina bar-tops, etc.
        AreaTransition, // Invisible trigger that loads a new module
        UseItem,        // Items placed in the world (medpacs, lightsaber crystals)
        QuestItem,      // Key quest pickups
        Workbench       // Crafting station
    }

    /// <summary>
    /// Attach this to any GameObject that the player can interact with.
    /// The InteractionController on the player will detect it via raycast / radius.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Identity")]
        [Tooltip("Unique ResRef used to look up DLG / UTD / UTC data.")]
        [SerializeField] public string ResRef = "";
        [SerializeField] public string DisplayName = "Unknown";
        [SerializeField] public InteractableType Type = InteractableType.NPC;

        [Header("Dialogue")]
        [Tooltip("The .dlg ResRef to start when this object is activated.")]
        [SerializeField] public string DlgResRef = "";
        [Tooltip("If true, an on-screen prompt is shown when the player is in range.")]
        [SerializeField] public bool ShowPrompt = true;
        [Tooltip("Range at which the prompt appears.")]
        [SerializeField] public float PromptRange = 4f;
        [Tooltip("Range at which the player must stand to activate.")]
        [SerializeField] public float InteractRange = 2.5f;

        [Header("Door")]
        [SerializeField] public bool IsLocked = false;
        [SerializeField] public string LockTag = "";
        [SerializeField] public string DestinationModule = "";
        [SerializeField] public string DestinationWaypoint = "WP_Entry01";
        [SerializeField] public bool IsOpen = false;

        [Header("Container")]
        [Tooltip("List of item ResRefs in this container.")]
        [SerializeField] public List<string> ContainerItems = new List<string>();
        [SerializeField] public bool IsEmpty = false;

        [Header("One-Shot")]
        [Tooltip("If true, this interactable is destroyed / deactivated after one use.")]
        [SerializeField] public bool OneShot = false;
        [SerializeField] private bool _used = false;

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<Interactable, GameObject> OnInteracted;
        public event Action<Interactable>             OnPlayerEnterRange;
        public event Action<Interactable>             OnPlayerExitRange;

        // ── STATE ─────────────────────────────────────────────────────────────
        public bool IsUsed => _used;
        private bool _playerInRange = false;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, InteractRange);
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, PromptRange);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Called by InteractionController when the player activates this object.</summary>
        public void Activate(GameObject activator)
        {
            if (OneShot && _used) return;

            OnInteracted?.Invoke(this, activator);

            switch (Type)
            {
                case InteractableType.NPC:
                    StartConversation(activator);
                    break;

                case InteractableType.Door:
                    ActivateDoor(activator);
                    break;

                case InteractableType.Container:
                    OpenContainer(activator);
                    break;

                case InteractableType.Placeable:
                    StartConversation(activator);
                    break;

                case InteractableType.AreaTransition:
                    TriggerAreaTransition();
                    break;

                case InteractableType.UseItem:
                case InteractableType.QuestItem:
                    PickupItem(activator);
                    break;

                case InteractableType.Workbench:
                    OpenWorkbench(activator);
                    break;
            }

            if (OneShot) _used = true;
        }

        public void NotifyPlayerInRange(bool inRange)
        {
            if (inRange == _playerInRange) return;
            _playerInRange = inRange;
            if (inRange) OnPlayerEnterRange?.Invoke(this);
            else         OnPlayerExitRange?.Invoke(this);
        }

        // ── INTERACTION HANDLERS ──────────────────────────────────────────────

        private void StartConversation(GameObject activator)
        {
            if (string.IsNullOrEmpty(DlgResRef))
            {
                Debug.LogWarning($"[Interactable] {DisplayName} has no DlgResRef assigned.");
                return;
            }
            DialogueManager.Instance?.StartDialogue(DlgResRef, gameObject);
        }

        private void ActivateDoor(GameObject activator)
        {
            if (IsLocked)
            {
                // Check if activator has the key
                var inv = activator?.GetComponent<Inventory.InventoryManager>();
                bool hasKey = inv != null && !string.IsNullOrEmpty(LockTag)
                              && inv.PlayerInventory.HasItem(LockTag);

                if (!hasKey)
                {
                    Debug.Log($"[Door] {DisplayName} is locked. Key required: {LockTag}");

                    if (!string.IsNullOrEmpty(DlgResRef))
                        DialogueManager.Instance?.StartDialogue(DlgResRef, gameObject);
                    return;
                }
                IsLocked = false;
            }

            if (!string.IsNullOrEmpty(DestinationModule))
            {
                // Area transition
                EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                    new EventBus.ModuleEventArgs(DestinationModule, DestinationWaypoint));
                return;
            }

            // Toggle open/close animation
            IsOpen = !IsOpen;
            PlayDoorAnim(IsOpen);
        }

        private void OpenContainer(GameObject activator)
        {
            if (IsEmpty)
            {
                Debug.Log($"[Container] {DisplayName} is empty.");
                return;
            }

            var inv = activator?.GetComponent<Inventory.InventoryManager>();
            if (inv == null) return;

            foreach (var itemRef in ContainerItems)
            {
                if (string.IsNullOrEmpty(itemRef)) continue;
                inv.AddItemByResRef(itemRef);
                EventBus.Publish(EventBus.EventType.ItemPickedUp,
                    new EventBus.ItemEventArgs(itemRef, itemRef, 0));
                Debug.Log($"[Container] Picked up: {itemRef}");
            }

            ContainerItems.Clear();
            IsEmpty = true;

            // Trigger loot event
            EventBus.Publish(EventBus.EventType.LootCollected,
                new EventBus.LootEventArgs(ResRef, 0, transform.position));
        }

        private void TriggerAreaTransition()
        {
            if (string.IsNullOrEmpty(DestinationModule)) return;
            EventBus.Publish(EventBus.EventType.AreaTransitionRequested,
                new EventBus.ModuleEventArgs(DestinationModule, DestinationWaypoint));
        }

        private void PickupItem(GameObject activator)
        {
            var inv = activator?.GetComponent<Inventory.InventoryManager>();
            if (inv == null) return;

            if (!string.IsNullOrEmpty(ResRef))
            {
                inv.AddItemByResRef(ResRef);
                EventBus.Publish(EventBus.EventType.ItemPickedUp,
                    new EventBus.ItemEventArgs(ResRef, DisplayName, 0));
            }

            gameObject.SetActive(false);
        }

        private void OpenWorkbench(GameObject activator)
        {
            // Publish an event so UIManager can open the crafting panel
            EventBus.Publish(EventBus.EventType.WorkbenchOpened,
                new EventBus.GameEventArgs());
            Debug.Log($"[Workbench] {DisplayName} opened.");
        }

        private void PlayDoorAnim(bool open)
        {
            var anim = GetComponent<Animation>() ?? GetComponentInChildren<Animation>();
            if (anim != null)
            {
                string clip = open ? "Open" : "Close";
                if (anim[clip] != null) anim.Play(clip);
                return;
            }
            var animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetBool("IsOpen", open);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  INTERACTION CONTROLLER  —  on the Player; handles detection & activation
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attach to the Player GameObject.
    /// Handles:
    ///   • Proximity detection of Interactables
    ///   • On-screen prompt display
    ///   • Click/key activation (E key or left-click on highlight)
    ///   • Auto-walk to target before activating
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class InteractionController : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static InteractionController Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Detection")]
        [SerializeField] private float scanRadius = 6f;
        [SerializeField] private LayerMask interactableLayer;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        [Header("Auto-Walk")]
        [Tooltip("If true, the player automatically walks toward the target before activating.")]
        [SerializeField] private bool autoWalkToTarget = true;
        [SerializeField] private float walkStopDistance = 1.8f;

        [Header("Cursor Highlight")]
        [SerializeField] private Texture2D cursorInteract;
        [SerializeField] private Texture2D cursorDefault;
        [Tooltip("Layer mask for mouse raycast (used in Action mode).")]
        [SerializeField] private LayerMask clickRaycastMask;

        // ── RUNTIME ───────────────────────────────────────────────────────────
        private Interactable _focusTarget;            // nearest / cursor-hovered
        private Interactable _pendingActivation;      // walking toward
        private CharacterController _cc;
        private UnityEngine.Camera _cam;
        private readonly Collider[] _scanBuffer = new Collider[32];

        // Prompt UI — resolved lazily
        private InteractionPromptUI _promptUI;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _cc  = GetComponent<CharacterController>();
            _cam = UnityEngine.Camera.main;
        }

        private void Update()
        {
            ScanNearbyInteractables();
            HandleMouseHover();
            HandleInput();
            HandleAutoWalk();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── SCAN ──────────────────────────────────────────────────────────────
        private void ScanNearbyInteractables()
        {
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, scanRadius, _scanBuffer, interactableLayer);

            Interactable nearest = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _scanBuffer[i];
                if (col == null) continue;
                var ia = col.GetComponent<Interactable>()
                         ?? col.GetComponentInParent<Interactable>();
                if (ia == null || !ia.gameObject.activeInHierarchy) continue;
                if (ia.IsUsed) continue;

                float dist = Vector3.Distance(transform.position, ia.transform.position);

                // Notify range enter / exit
                ia.NotifyPlayerInRange(dist <= ia.PromptRange);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = ia;
                }
            }

            // Update focus
            if (nearest != _focusTarget)
            {
                SetFocus(nearest);
            }
        }

        // ── MOUSE HOVER (Action Mode) ─────────────────────────────────────────
        private void HandleMouseHover()
        {
            // Skip if cursor is over a UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                SetCursor(false);
                return;
            }

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 40f, clickRaycastMask))
            {
                var ia = hit.collider.GetComponent<Interactable>()
                         ?? hit.collider.GetComponentInParent<Interactable>();
                if (ia != null && !ia.IsUsed)
                {
                    SetCursor(true);

                    // Left-click to activate
                    if (Input.GetMouseButtonDown(0))
                        TryActivate(ia);

                    return;
                }
            }
            SetCursor(false);
        }

        // ── INPUT ─────────────────────────────────────────────────────────────
        private void HandleInput()
        {
            if (Input.GetKeyDown(interactKey))
            {
                if (_focusTarget != null)
                    TryActivate(_focusTarget);
            }
        }

        // ── AUTO WALK ─────────────────────────────────────────────────────────
        private void HandleAutoWalk()
        {
            if (_pendingActivation == null) return;

            float dist = Vector3.Distance(transform.position, _pendingActivation.transform.position);
            if (dist <= walkStopDistance)
            {
                // Arrived — activate
                var target = _pendingActivation;
                _pendingActivation = null;
                target.Activate(gameObject);
                return;
            }

            // Move toward target
            if (autoWalkToTarget)
            {
                Vector3 dir = (_pendingActivation.transform.position - transform.position).normalized;
                dir.y = 0f;
                _cc.SimpleMove(dir * 4f);  // walk speed
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
            }
        }

        // ── ACTIVATION ────────────────────────────────────────────────────────
        public void TryActivate(Interactable target)
        {
            if (target == null || target.IsUsed) return;

            float dist = Vector3.Distance(transform.position, target.transform.position);

            if (dist <= target.InteractRange)
            {
                // In range — activate immediately
                target.Activate(gameObject);
            }
            else if (autoWalkToTarget)
            {
                // Walk there first
                _pendingActivation = target;
            }
            else
            {
                Debug.Log($"[InteractionController] {target.DisplayName} is too far away.");
            }
        }

        // ── FOCUS ─────────────────────────────────────────────────────────────
        private void SetFocus(Interactable newFocus)
        {
            _focusTarget = newFocus;

            // Update prompt UI
            if (_promptUI == null)
                _promptUI = FindObjectOfType<InteractionPromptUI>();

            if (_promptUI != null)
            {
                if (newFocus != null && newFocus.ShowPrompt)
                    _promptUI.Show(newFocus.DisplayName, GetInteractVerb(newFocus));
                else
                    _promptUI.Hide();
            }
        }

        private static string GetInteractVerb(Interactable ia)
        {
            return ia.Type switch
            {
                InteractableType.NPC       => "Talk",
                InteractableType.Door      => ia.IsOpen ? "Close" : "Open",
                InteractableType.Container => ia.IsEmpty ? "(Empty)" : "Loot",
                InteractableType.UseItem   => "Pick Up",
                InteractableType.QuestItem => "Examine",
                InteractableType.Workbench => "Use",
                _                          => "Examine"
            };
        }

        // ── CURSOR ────────────────────────────────────────────────────────────
        private bool _isInteractCursor = false;

        private void SetCursor(bool interact)
        {
            if (interact == _isInteractCursor) return;
            _isInteractCursor = interact;
            Texture2D tex = interact ? cursorInteract : cursorDefault;
            Cursor.SetCursor(tex, Vector2.zero, CursorMode.Auto);
        }

        // ── GIZMOS ────────────────────────────────────────────────────────────
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, scanRadius);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        public Interactable FocusTarget   => _focusTarget;
        public bool         IsAutoWalking => _pendingActivation != null;

        public void CancelAutoWalk()
        {
            _pendingActivation = null;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  INTERACTION PROMPT UI  —  the "E – Talk" label above interactables
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Minimal world-space / screen-space prompt shown when the player is near
    /// an interactable.  Requires a Canvas in World-Space or Screen-Space Overlay.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMPro.TextMeshProUGUI _verbText;
        [SerializeField] private TMPro.TextMeshProUGUI _nameText;

        [Header("Fade")]
        [SerializeField] private float _fadeSpeed = 8f;
        private CanvasGroup _cg;
        private bool _visible;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;
            if (_panel != null) _panel.SetActive(false);
        }

        private void Update()
        {
            float target = _visible ? 1f : 0f;
            _cg.alpha = Mathf.MoveTowards(_cg.alpha, target, _fadeSpeed * Time.deltaTime);
            if (_panel != null) _panel.SetActive(_cg.alpha > 0.01f);
        }

        public void Show(string objectName, string verb)
        {
            _visible = true;
            if (_nameText != null) _nameText.text = objectName;
            if (_verbText != null) _verbText.text  = $"[E] {verb}";
        }

        public void Hide()
        {
            _visible = false;
        }
    }

}
