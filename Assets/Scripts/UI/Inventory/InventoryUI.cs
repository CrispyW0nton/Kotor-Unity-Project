using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Inventory;
using KotORUnity.Player;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.UI.Inventory
{
    // ══════════════════════════════════════════════════════════════════════════
    //  INVENTORY UI  —  Full KotOR-style equipment + backpack screen
    //
    //  Layout mirrors KotOR 1:
    //    Left panel  : equipment paperdoll (12 equipment slots)
    //    Centre panel: player portrait + stats summary (AC, attack, saves)
    //    Right panel : backpack grid (scrollable, 5×8 = 40 slots visible)
    //    Bottom bar  : credits display, close button, sort button
    //
    //  Drag-and-drop:
    //    • Drag item from backpack → equipment slot  → equip
    //    • Drag item from equipment → backpack slot  → unequip + place
    //    • Drag between backpack slots               → reorder
    //    • Right-click item                          → context menu (Use/Equip/Drop/Info)
    //
    //  Stat comparison:
    //    • Hover equipment slot while holding an item → show stat delta overlay
    //
    //  EventBus:
    //    Publishes ItemEquipped / ItemUnequipped / UIHUDRefresh
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Main Inventory UI controller.  Attach to the root Inventory panel GameObject.
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        // ── INSPECTOR REFS ────────────────────────────────────────────────────

        [Header("Panels")]
        [SerializeField] private GameObject _inventoryPanel;
        [SerializeField] private Transform  _backpackGrid;          // parent for backpack ItemSlotUI
        [SerializeField] private Transform  _equipmentSlotsParent;  // parent for EquipSlotUI

        [Header("Prefabs")]
        [SerializeField] private GameObject _itemSlotPrefab;        // BackpackSlotUI prefab
        [SerializeField] private GameObject _equipSlotPrefab;       // EquipSlotUI prefab
        [SerializeField] private GameObject _dragIconPrefab;        // floating icon while dragging

        [Header("Centre Panel")]
        [SerializeField] private Image              _portraitImage;
        [SerializeField] private TextMeshProUGUI    _characterNameText;
        [SerializeField] private TextMeshProUGUI    _classLevelText;
        [SerializeField] private TextMeshProUGUI    _acText;
        [SerializeField] private TextMeshProUGUI    _attackText;
        [SerializeField] private TextMeshProUGUI    _damageText;
        [SerializeField] private TextMeshProUGUI    _fortText;
        [SerializeField] private TextMeshProUGUI    _refText;
        [SerializeField] private TextMeshProUGUI    _willText;

        [Header("Bottom Bar")]
        [SerializeField] private TextMeshProUGUI    _creditsText;
        [SerializeField] private Button             _closeButton;
        [SerializeField] private Button             _sortButton;

        [Header("Tooltip")]
        [SerializeField] private GameObject         _tooltipPanel;
        [SerializeField] private TextMeshProUGUI    _tooltipNameText;
        [SerializeField] private TextMeshProUGUI    _tooltipStatsText;
        [SerializeField] private TextMeshProUGUI    _tooltipDescText;

        [Header("Stat Compare Overlay")]
        [SerializeField] private GameObject         _comparePanel;
        [SerializeField] private TextMeshProUGUI    _compareDeltaText;

        [Header("Context Menu")]
        [SerializeField] private GameObject         _contextMenuPanel;
        [SerializeField] private Button             _ctxUseButton;
        [SerializeField] private Button             _ctxEquipButton;
        [SerializeField] private Button             _ctxDropButton;
        [SerializeField] private Button             _ctxInfoButton;

        [Header("Config")]
        [SerializeField] private int  _backpackColumns = 5;
        [SerializeField] private int  _backpackRows    = 8;
        [SerializeField] private bool _closeOnESC      = true;

        // ── RUNTIME ───────────────────────────────────────────────────────────
        private InventoryManager    _invMgr;
        private PlayerStatsBehaviour _playerStatsBehaviour;
        private PlayerStats           _playerStats => _playerStatsBehaviour?.Stats;

        // Slot pools
        private readonly List<BackpackSlotUI> _backpackSlots  = new List<BackpackSlotUI>();
        private readonly Dictionary<EquipSlot, EquipSlotUI> _equipSlots
            = new Dictionary<EquipSlot, EquipSlotUI>();

        // Drag state
        private ItemData    _dragItem;
        private int         _dragSourceBackpackIndex = -1;
        private EquipSlot   _dragSourceEquipSlot     = EquipSlot.None;
        private GameObject  _dragIconGO;

        // Context menu state
        private ItemData    _ctxItem;
        private int         _ctxBackpackIndex = -1;

        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static InventoryUI Instance { get; private set; }

        // ── UNITY ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _closeButton?.onClick.AddListener(Hide);
            _sortButton?.onClick.AddListener(SortBackpack);
            _ctxUseButton?.onClick.AddListener(OnContextUse);
            _ctxEquipButton?.onClick.AddListener(OnContextEquip);
            _ctxDropButton?.onClick.AddListener(OnContextDrop);
            _ctxInfoButton?.onClick.AddListener(OnContextInfo);

            BuildEquipmentSlots();
            BuildBackpackSlots();

            Hide();
        }

        private void Start()
        {
            _invMgr      = InventoryManager.Instance;
            _playerStatsBehaviour = FindObjectOfType<PlayerStatsBehaviour>();

            // Subscribe to inventory changes
            EventBus.Subscribe(EventBus.EventType.ItemEquipped,   _ => RefreshAll());
            EventBus.Subscribe(EventBus.EventType.ItemUnequipped, _ => RefreshAll());
            EventBus.Subscribe(EventBus.EventType.ItemPickedUp,   _ => RefreshBackpack());
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.ItemEquipped,   _ => RefreshAll());
            EventBus.Unsubscribe(EventBus.EventType.ItemUnequipped, _ => RefreshAll());
            EventBus.Unsubscribe(EventBus.EventType.ItemPickedUp,   _ => RefreshBackpack());
        }

        private void Update()
        {
            if (!_inventoryPanel.activeSelf) return;

            // Close on Escape
            if (_closeOnESC && Input.GetKeyDown(KeyCode.Escape))
                Hide();

            // Toggle with I key
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
                Toggle();

            // Update drag icon position
            if (_dragItem != null && _dragIconGO != null)
                _dragIconGO.transform.position = Input.mousePosition;

            // Hide context menu on click elsewhere
            if (_contextMenuPanel != null && _contextMenuPanel.activeSelf
                && Input.GetMouseButtonDown(0))
            {
                if (!EventSystem.current.IsPointerOverGameObject())
                    HideContextMenu();
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        public void Show()
        {
            _inventoryPanel.SetActive(true);
            RefreshAll();
            EventBus.Publish(EventBus.EventType.GamePaused);
        }

        public void Hide()
        {
            _inventoryPanel.SetActive(false);
            HideDragIcon();
            HideContextMenu();
            HideTooltip();
            EventBus.Publish(EventBus.EventType.GameResumed);
        }

        public void Toggle()
        {
            if (_inventoryPanel.activeSelf) Hide(); else Show();
        }

        // ── BUILD SLOTS ───────────────────────────────────────────────────────

        private void BuildEquipmentSlots()
        {
            if (_equipSlotPrefab == null || _equipmentSlotsParent == null) return;

            // Map of all equip slots to their display names
            var slotDefs = new (EquipSlot slot, string label)[]
            {
                (EquipSlot.Head,    "Head"),
                (EquipSlot.Body,    "Body"),
                (EquipSlot.Hands,   "Hands"),
                (EquipSlot.ArmL,    "Arm L"),
                (EquipSlot.ArmR,    "Arm R"),
                (EquipSlot.Ring1,   "Ring 1"),
                (EquipSlot.Ring2,   "Ring 2"),
                (EquipSlot.Belt,    "Belt"),
                (EquipSlot.Boots,   "Boots"),
                (EquipSlot.WeaponR, "Weapon R"),
                (EquipSlot.WeaponL, "Weapon L / Off-hand"),
                (EquipSlot.Cloak,   "Cloak"),
                (EquipSlot.Implant, "Implant"),
            };

            foreach (var def in slotDefs)
            {
                var go   = Instantiate(_equipSlotPrefab, _equipmentSlotsParent);
                var esui = go.GetComponent<EquipSlotUI>() ?? go.AddComponent<EquipSlotUI>();
                esui.Init(def.slot, def.label, this);
                _equipSlots[def.slot] = esui;
            }
        }

        private void BuildBackpackSlots()
        {
            if (_itemSlotPrefab == null || _backpackGrid == null) return;

            int total = _backpackColumns * _backpackRows;
            for (int i = 0; i < total; i++)
            {
                var go   = Instantiate(_itemSlotPrefab, _backpackGrid);
                var bpui = go.GetComponent<BackpackSlotUI>() ?? go.AddComponent<BackpackSlotUI>();
                bpui.Init(i, this);
                _backpackSlots.Add(bpui);
            }
        }

        // ── REFRESH ───────────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshEquipmentSlots();
            RefreshBackpack();
            RefreshStats();
            RefreshCredits();
        }

        private void RefreshEquipmentSlots()
        {
            if (_invMgr == null) return;

            foreach (var kvp in _equipSlots)
            {
                var item = _invMgr.PlayerInventory?.GetEquipped(kvp.Key);
                kvp.Value.SetItem(item);
            }
        }

        private void RefreshBackpack()
        {
            if (_invMgr == null) return;

            var items = _invMgr.PlayerInventory?.Items;
            for (int i = 0; i < _backpackSlots.Count; i++)
            {
                var item = (items != null && i < items.Count) ? items[i] : null;
                _backpackSlots[i].SetItem(item);
            }
        }

        private void RefreshStats()
        {
            if (_playerStats == null) return;

            if (_acText     != null) _acText.text     = $"AC  {_playerStats.ArmorClass}";
            if (_attackText != null) _attackText.text = $"ATK +{_playerStats.AttackBonus}";
            if (_damageText != null) _damageText.text = $"DMG {_playerStats.DamageBonusText}";
            if (_fortText   != null) _fortText.text   = $"FORT {_playerStats.FortSave:+0;-0;0}";
            if (_refText    != null) _refText.text    = $"REFL {_playerStats.RefSave:+0;-0;0}";
            if (_willText   != null) _willText.text   = $"WILL {_playerStats.WillSave:+0;-0;0}";

            if (_characterNameText != null)
                _characterNameText.text = _playerStats.CharacterName;
            if (_classLevelText != null)
                _classLevelText.text = _playerStats.ClassLevelDisplay;
        }

        private void RefreshCredits()
        {
            if (_creditsText != null && _invMgr != null)
                _creditsText.text = $"Credits: {_invMgr.PlayerCredits:N0}";
        }

        // ── DRAG & DROP ───────────────────────────────────────────────────────

        /// <summary>Begin dragging from a backpack slot.</summary>
        internal void BeginDragFromBackpack(int index, ItemData item)
        {
            _dragItem                = item;
            _dragSourceBackpackIndex = index;
            _dragSourceEquipSlot     = EquipSlot.None;
            ShowDragIcon(item);
        }

        /// <summary>Begin dragging from an equipment slot.</summary>
        internal void BeginDragFromEquip(EquipSlot slot, ItemData item)
        {
            _dragItem                = item;
            _dragSourceBackpackIndex = -1;
            _dragSourceEquipSlot     = slot;
            ShowDragIcon(item);
        }

        /// <summary>Drop onto a backpack slot.</summary>
        internal void DropOntoBackpack(int targetIndex)
        {
            if (_dragItem == null) return;

            if (_dragSourceEquipSlot != EquipSlot.None)
            {
                // Unequip → place in target backpack slot
                _invMgr?.Unequip(_dragSourceEquipSlot);
                // Item is automatically added back to backpack by InventorySystem
            }
            else if (_dragSourceBackpackIndex >= 0 && _dragSourceBackpackIndex != targetIndex)
            {
                // Reorder within backpack
                _invMgr?.PlayerInventory?.SwapItems(_dragSourceBackpackIndex, targetIndex);
            }

            ClearDragState();
            RefreshAll();
        }

        /// <summary>Drop onto an equipment slot.</summary>
        internal void DropOntoEquipSlot(EquipSlot slot)
        {
            if (_dragItem == null) return;

            if (_dragSourceEquipSlot == slot)
            {
                ClearDragState();
                return;
            }

            // Validate: item must be equippable in this slot
            bool valid = (_dragItem.EquipableSlots & (1 << (int)slot)) != 0;
            if (!valid)
            {
                ShowTemporaryMessage("Can't equip that there!");
                ClearDragState();
                return;
            }

            if (_dragSourceBackpackIndex >= 0)
                _invMgr?.Equip(_dragItem.ResRef, slot);
            else if (_dragSourceEquipSlot != EquipSlot.None)
            {
                // Move from one equip slot to another (e.g. swap ring slots)
                _invMgr?.Unequip(_dragSourceEquipSlot);
                _invMgr?.Equip(_dragItem.ResRef, slot);
            }

            ClearDragState();
            RefreshAll();
        }

        /// <summary>Cancel drag (e.g. dropped on invalid area).</summary>
        internal void CancelDrag()
        {
            ClearDragState();
        }

        private void ClearDragState()
        {
            _dragItem                = null;
            _dragSourceBackpackIndex = -1;
            _dragSourceEquipSlot     = EquipSlot.None;
            HideDragIcon();
        }

        private void ShowDragIcon(ItemData item)
        {
            if (_dragIconPrefab == null) return;
            HideDragIcon();
            _dragIconGO = Instantiate(_dragIconPrefab, transform.root);
            var img = _dragIconGO.GetComponent<Image>();
            if (img != null) img.sprite = LoadItemSprite(item);
            var cg = _dragIconGO.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0.85f; cg.blocksRaycasts = false; }
        }

        private void HideDragIcon()
        {
            if (_dragIconGO != null) { Destroy(_dragIconGO); _dragIconGO = null; }
        }

        // ── TOOLTIP ───────────────────────────────────────────────────────────

        internal void ShowTooltip(ItemData item, Vector2 screenPos)
        {
            if (_tooltipPanel == null || item == null) return;

            _tooltipPanel.SetActive(true);
            if (_tooltipNameText  != null) _tooltipNameText.text  = item.DisplayName;
            if (_tooltipStatsText != null) _tooltipStatsText.text = BuildItemStatsString(item);
            if (_tooltipDescText  != null) _tooltipDescText.text  = item.DescriptionText;

            // Position tooltip near cursor, clamped to screen
            var rt = _tooltipPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                Vector2 pos = screenPos + new Vector2(20f, -10f);
                pos.x = Mathf.Clamp(pos.x, 0, Screen.width  - rt.rect.width);
                pos.y = Mathf.Clamp(pos.y, rt.rect.height, Screen.height);
                rt.position = pos;
            }
        }

        internal void HideTooltip()
        {
            _tooltipPanel?.SetActive(false);
        }

        // ── STAT COMPARE OVERLAY ─────────────────────────────────────────────

        internal void ShowStatCompare(ItemData dragged, ItemData current)
        {
            if (_comparePanel == null) return;
            _comparePanel.SetActive(true);
            if (_compareDeltaText != null)
                _compareDeltaText.text = BuildStatDeltaString(dragged, current);
        }

        internal void HideStatCompare()
        {
            _comparePanel?.SetActive(false);
        }

        // ── CONTEXT MENU ──────────────────────────────────────────────────────

        internal void ShowContextMenu(ItemData item, int backpackIndex, Vector2 screenPos)
        {
            if (_contextMenuPanel == null) return;
            _ctxItem          = item;
            _ctxBackpackIndex = backpackIndex;

            _contextMenuPanel.SetActive(true);
            var rt = _contextMenuPanel.GetComponent<RectTransform>();
            if (rt != null) rt.position = screenPos;

            // Configure buttons
            bool isEquipable = item != null && item.EquipableSlots != 0;
            bool isUsable    = item != null && item.Charges > 0;
            _ctxEquipButton?.gameObject.SetActive(isEquipable);
            _ctxUseButton?.gameObject.SetActive(isUsable);
        }

        private void HideContextMenu()
        {
            _ctxItem          = null;
            _ctxBackpackIndex = -1;
            _contextMenuPanel?.SetActive(false);
        }

        private void OnContextUse()
        {
            if (_ctxItem == null) return;
            // TODO: use item (medical packs, stims, etc.)
            Debug.Log($"[InventoryUI] Used: {_ctxItem.DisplayName}");
            HideContextMenu();
            RefreshBackpack();
        }

        private void OnContextEquip()
        {
            if (_ctxItem == null) return;
            // Find best slot
            var bestSlot = GetBestEquipSlot(_ctxItem);
            if (bestSlot != EquipSlot.None)
            {
                _invMgr?.Equip(_ctxItem.ResRef, bestSlot);
                RefreshAll();
            }
            HideContextMenu();
        }

        private void OnContextDrop()
        {
            if (_ctxItem == null) return;
            // Drop item in world (spawn loot bag near player)
            DropItemInWorld(_ctxItem);
            _invMgr?.PlayerInventory?.RemoveItem(_ctxItem);
            RefreshBackpack();
            HideContextMenu();
        }

        private void OnContextInfo()
        {
            if (_ctxItem == null) return;
            // Show full item info panel
            ShowTooltip(_ctxItem, Input.mousePosition);
            HideContextMenu();
        }

        // ── SORT ─────────────────────────────────────────────────────────────

        private void SortBackpack()
        {
            _invMgr?.PlayerInventory?.Sort();
            RefreshBackpack();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private Sprite LoadItemSprite(ItemData item)
        {
            if (item == null) return null;
            return Resources.Load<Sprite>($"Icons/Items/{item.ResRef}")
                ?? Resources.Load<Sprite>("Icons/Items/default_item");
        }

        private string BuildItemStatsString(ItemData item)
        {
            if (item == null) return "";
            var sb = new System.Text.StringBuilder();
            if (item.IsWeapon)
            {
                sb.AppendLine($"Damage:  {item.DamageNumDice}d{item.DamageDie}+{item.DamageBonus}");
                sb.AppendLine($"Attack:  +{item.AttackBonus}");
            }
            if (item.IsArmour)
            {
                sb.AppendLine($"Defense: +{item.ACBonus} AC");
                if (item.MaxDexBonus < 99)
                    sb.AppendLine($"Max Dex: +{item.MaxDexBonus}");
            }
            if (item.Cost > 0)
                sb.AppendLine($"Value:   {item.Cost:N0} credits");
            return sb.ToString().TrimEnd();
        }

        private string BuildStatDeltaString(ItemData incoming, ItemData current)
        {
            if (incoming == null) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Stat Change:");
            int acDelta  = incoming.ACBonus     - (current?.ACBonus     ?? 0);
            int atkDelta = incoming.AttackBonus  - (current?.AttackBonus  ?? 0);
            int dmgDelta = incoming.DamageBonus  - (current?.DamageBonus  ?? 0);
            if (acDelta  != 0) sb.AppendLine(ColorDelta("AC",  acDelta));
            if (atkDelta != 0) sb.AppendLine(ColorDelta("ATK", atkDelta));
            if (dmgDelta != 0) sb.AppendLine(ColorDelta("DMG", dmgDelta));
            return sb.ToString().TrimEnd();
        }

        private string ColorDelta(string label, int delta)
        {
            string col = delta > 0 ? "#00FF88" : "#FF4444";
            return $"<color={col}>{label}: {delta:+0;-0;0}</color>";
        }

        private EquipSlot GetBestEquipSlot(ItemData item)
        {
            if (item == null) return EquipSlot.None;

            // Priority order for auto-equip
            var priority = new EquipSlot[]
            {
                EquipSlot.WeaponR, EquipSlot.WeaponL, EquipSlot.Body,
                EquipSlot.Head, EquipSlot.Belt, EquipSlot.Boots,
                EquipSlot.Cloak, EquipSlot.Implant, EquipSlot.Ring1,
                EquipSlot.Ring2, EquipSlot.Hands, EquipSlot.ArmL, EquipSlot.ArmR,
            };

            foreach (var slot in priority)
            {
                if ((item.EquipableSlots & (1 << (int)slot)) != 0)
                    return slot;
            }
            return EquipSlot.None;
        }

        private void DropItemInWorld(ItemData item)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            Vector3 pos = player.transform.position + player.transform.forward * 1.5f;
            var lootGO  = new GameObject($"Dropped_{item.ResRef}");
            lootGO.transform.position = pos;
            // Tag it so InteractionSystem can pick it up
            lootGO.tag = "Interactable";
            // Attach a simple component to mark it as a loot item
            var loot = lootGO.AddComponent<DroppedItem>();
            loot.Item = item;
            Debug.Log($"[InventoryUI] Dropped '{item.DisplayName}' at {pos}");
        }

        private void ShowTemporaryMessage(string msg)
        {
            Debug.Log($"[InventoryUI] {msg}");
            // Could show toast notification via HUDManager
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BACKPACK SLOT UI
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One cell in the backpack grid.  Handles display, hover tooltip, drag-start,
    /// drop-receive, and right-click context menu.
    /// </summary>
    public class BackpackSlotUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image           _iconImage;
        [SerializeField] private TextMeshProUGUI _stackText;
        [SerializeField] private Image           _highlightImage;

        private int          _index;
        private ItemData     _item;
        private InventoryUI  _owner;

        internal void Init(int index, InventoryUI owner)
        {
            _index = index;
            _owner = owner;
            if (_iconImage      != null) _iconImage.enabled      = false;
            if (_stackText      != null) _stackText.enabled      = false;
            if (_highlightImage != null) _highlightImage.enabled = false;
        }

        internal void SetItem(ItemData item)
        {
            _item = item;
            bool hasItem = item != null;

            if (_iconImage != null)
            {
                _iconImage.enabled = hasItem;
                if (hasItem)
                {
                    var sprite = Resources.Load<Sprite>($"Icons/Items/{item.ResRef}");
                    if (sprite == null) sprite = Resources.Load<Sprite>("Icons/Items/default_item");
                    _iconImage.sprite = sprite;
                }
            }
            if (_stackText != null)
            {
                _stackText.enabled = hasItem && item.StackSize > 1;
                if (hasItem) _stackText.text = item.StackSize.ToString();
            }
        }

        // ── Hover ──────────────────────────────────────────────────────────
        public void OnPointerEnter(PointerEventData e)
        {
            if (_item != null)
                _owner.ShowTooltip(_item, e.position);
            if (_highlightImage != null) _highlightImage.enabled = true;
        }

        public void OnPointerExit(PointerEventData e)
        {
            _owner.HideTooltip();
            if (_highlightImage != null) _highlightImage.enabled = false;
        }

        // ── Click ──────────────────────────────────────────────────────────
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right && _item != null)
                _owner.ShowContextMenu(_item, _index, e.position);
        }

        // ── Drag ──────────────────────────────────────────────────────────
        public void OnBeginDrag(PointerEventData e)
        {
            if (_item == null) return;
            _owner.BeginDragFromBackpack(_index, _item);
        }

        public void OnDrag(PointerEventData e) { /* handled by InventoryUI.Update */ }

        public void OnEndDrag(PointerEventData e)
        {
            // If dropped on nothing valid, cancel
            if (e.pointerEnter == null)
                _owner.CancelDrag();
        }

        public void OnDrop(PointerEventData e)
        {
            _owner.DropOntoBackpack(_index);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  EQUIP SLOT UI
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One equipment slot in the paperdoll.  Accepts drag-and-drop from backpack,
    /// shows stat compare overlay on hover while dragging.
    /// </summary>
    public class EquipSlotUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        [SerializeField] private Image           _iconImage;
        [SerializeField] private TextMeshProUGUI _slotLabel;
        [SerializeField] private Image           _emptyFrame;
        [SerializeField] private Image           _highlightImage;

        private EquipSlot   _slot;
        private ItemData    _item;
        private InventoryUI _owner;

        internal void Init(EquipSlot slot, string label, InventoryUI owner)
        {
            _slot  = slot;
            _owner = owner;
            if (_slotLabel    != null) _slotLabel.text     = label;
            if (_emptyFrame   != null) _emptyFrame.enabled = true;
            if (_iconImage    != null) _iconImage.enabled  = false;
            if (_highlightImage != null) _highlightImage.enabled = false;
        }

        internal void SetItem(ItemData item)
        {
            _item = item;
            bool has = item != null;

            if (_iconImage != null)
            {
                _iconImage.enabled = has;
                if (has)
                {
                    var spr = Resources.Load<Sprite>($"Icons/Items/{item.ResRef}");
                    if (spr == null) spr = Resources.Load<Sprite>("Icons/Items/default_equip");
                    _iconImage.sprite = spr;
                }
            }
            if (_emptyFrame != null) _emptyFrame.enabled = !has;
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (_highlightImage != null) _highlightImage.enabled = true;
            if (_item != null)
                _owner.ShowTooltip(_item, e.position);
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (_highlightImage != null) _highlightImage.enabled = false;
            _owner.HideTooltip();
            _owner.HideStatCompare();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right && _item != null)
            {
                // Right-click equipped item → unequip
                InventoryManager.Instance?.Unequip(_slot);
            }
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (_item == null) return;
            _owner.BeginDragFromEquip(_slot, _item);
        }

        public void OnDrag(PointerEventData e) { }

        public void OnEndDrag(PointerEventData e)
        {
            if (e.pointerEnter == null) _owner.CancelDrag();
        }

        public void OnDrop(PointerEventData e)
        {
            _owner.DropOntoEquipSlot(_slot);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DROPPED ITEM — world-space loot marker
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks a dropped item in the world so the InteractionSystem can pick it up.
    /// </summary>
    public class DroppedItem : MonoBehaviour
    {
        public ItemData Item;

        private void Start()
        {
            // Add a collider so the Interaction system can detect it
            if (GetComponent<Collider>() == null)
            {
                var col = gameObject.AddComponent<SphereCollider>();
                col.radius    = 0.3f;
                col.isTrigger = true;
            }
        }
    }
}
