using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Inventory;
using KotORUnity.KotOR.Parsers;
using KotORUnity.Data;
using KotORUnity.Bootstrap;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MERCHANT DATA
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Merchant template (.utm GFF) parsed data.</summary>
    [Serializable]
    public class MerchantData
    {
        public string ResRef;
        public string Name;
        public int    MarkUp;        // buy multiplier %  (default 150)
        public int    MarkDown;      // sell multiplier % (default 50)
        public bool   BlackMarket;   // sells restricted items
        public List<MerchantItem> Inventory = new List<MerchantItem>();
    }

    [Serializable]
    public class MerchantItem
    {
        public string ResRef;
        public int    Infinite;   // 0 = limited, 1 = infinite stock
        public int    Stock;      // if Infinite==0, how many in stock
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MERCHANT SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Parses .utm GFF files and provides buy/sell pricing logic.
    ///
    /// KotOR pricing:
    ///   Buy price  = base_cost × MarkUp  / 100
    ///   Sell price = base_cost × MarkDown/ 100
    ///   Modified by Persuade skill: every 2 skill points = 1% better deal (capped ±30%)
    /// </summary>
    public static class MerchantSystem
    {
        public static MerchantData ParseUtm(byte[] data, string resref)
        {
            if (data == null || data.Length < 4) return null;

            var root = GffReader.Parse(data);
            if (root == null) return null;

            var merchant = new MerchantData
            {
                ResRef      = resref,
                Name        = GffReader.GetString(root, "LocalizedName"),
                MarkUp      = GffReader.GetInt   (root, "MarkUp",   150),
                MarkDown    = GffReader.GetInt   (root, "MarkDown", 50),
                BlackMarket = GffReader.GetInt   (root, "BlackMarket", 0) != 0
            };

            var itemList = GffReader.GetList(root, "ItemList");
            foreach (var itemStruct in itemList)
            {
                merchant.Inventory.Add(new MerchantItem
                {
                    ResRef   = GffReader.GetString(itemStruct, "ResRef"),
                    Infinite = GffReader.GetInt(itemStruct, "Infinite", 0),
                    Stock    = GffReader.GetInt(itemStruct, "StackSize", 1)
                });
            }
            return merchant;
        }

        public static int BuyPrice(int baseCost, MerchantData merchant, int persuadeSkill = 0)
        {
            float mark     = merchant?.MarkUp ?? 150;
            float discount = Mathf.Clamp(persuadeSkill * 0.5f, 0f, 30f);
            float price    = baseCost * (mark - discount) / 100f;
            return Mathf.Max(1, Mathf.RoundToInt(price));
        }

        public static int SellPrice(int baseCost, MerchantData merchant, int persuadeSkill = 0)
        {
            float mark  = merchant?.MarkDown ?? 50;
            float bonus = Mathf.Clamp(persuadeSkill * 0.5f, 0f, 30f);
            float price = baseCost * (mark + bonus) / 100f;
            return Mathf.Max(1, Mathf.RoundToInt(price));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  MERCHANT UI
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Full-screen merchant interface with confirmation dialog and item comparison.
    ///
    /// Layout:
    ///   • Left list   — merchant's wares (buy mode).
    ///   • Right list  — player's inventory (sell mode).
    ///   • Detail panel— item icon, name, description, stat comparison, price.
    ///   • Confirmation dialog — "Buy/Sell X for Y credits? [Confirm] [Cancel]"
    ///   • Toast notification  — brief feedback after transaction.
    ///
    /// Open via MerchantUI.Instance.Open(utmResRef).
    /// </summary>
    public class MerchantUI : MonoBehaviour
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────
        public static MerchantUI Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR FIELDS
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root Panel")]
        [SerializeField] private GameObject      _panel;
        [SerializeField] private TextMeshProUGUI _merchantNameText;
        [SerializeField] private TextMeshProUGUI _creditsText;
        [SerializeField] private Button          _btnClose;

        [Header("Merchant Inventory (Buy)")]
        [SerializeField] private Transform  _merchantListContainer;
        [SerializeField] private Button     _itemRowPrefab;
        [SerializeField] private ScrollRect _merchantScrollRect;

        [Header("Player Inventory (Sell)")]
        [SerializeField] private Transform  _playerListContainer;
        [SerializeField] private ScrollRect _playerScrollRect;

        [Header("Item Detail Panel")]
        [SerializeField] private GameObject      _detailPanel;
        [SerializeField] private Image           _detailIcon;
        [SerializeField] private TextMeshProUGUI _detailName;
        [SerializeField] private TextMeshProUGUI _detailDesc;
        [SerializeField] private TextMeshProUGUI _detailPrice;
        [SerializeField] private TextMeshProUGUI _detailType;
        [SerializeField] private TextMeshProUGUI _detailCharges;

        [Header("Stat Comparison (Detail Panel)")]
        [SerializeField] private GameObject      _comparePanel;
        [SerializeField] private TextMeshProUGUI _compareDamage;
        [SerializeField] private TextMeshProUGUI _compareAC;
        [SerializeField] private TextMeshProUGUI _compareAttack;
        [SerializeField] private TextMeshProUGUI _compareEquipped;   // currently equipped item name

        [Header("Action Buttons")]
        [SerializeField] private Button          _btnBuy;
        [SerializeField] private Button          _btnSell;
        [SerializeField] private TextMeshProUGUI _btnBuyLabel;
        [SerializeField] private TextMeshProUGUI _btnSellLabel;

        // ── CONFIRMATION DIALOG ──────────────────────────────────────────────
        [Header("Confirmation Dialog")]
        [SerializeField] private GameObject      _confirmDialog;
        [SerializeField] private TextMeshProUGUI _confirmTitle;
        [SerializeField] private TextMeshProUGUI _confirmItemName;
        [SerializeField] private TextMeshProUGUI _confirmPrice;
        [SerializeField] private TextMeshProUGUI _confirmCreditsAfter;
        [SerializeField] private Image           _confirmIcon;
        [SerializeField] private Button          _btnConfirmYes;
        [SerializeField] private Button          _btnConfirmNo;

        // ── TOAST ─────────────────────────────────────────────────────────────
        [Header("Toast Notification")]
        [SerializeField] private GameObject      _toastRoot;
        [SerializeField] private TextMeshProUGUI _toastText;
        [SerializeField] private float           _toastDuration = 2.5f;

        // ── FILTER TABS ───────────────────────────────────────────────────────
        [Header("Category Filter (optional)")]
        [SerializeField] private Button _filterAll;
        [SerializeField] private Button _filterWeapons;
        [SerializeField] private Button _filterArmor;
        [SerializeField] private Button _filterMedical;
        [SerializeField] private Button _filterMisc;

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private MerchantData  _merchant;
        private ItemData      _selectedMerchantItem;
        private InventorySlot _selectedPlayerSlot;
        private bool          _isBuyMode      = true;
        private string        _currentFilter  = "All";
        private Action        _pendingAction;          // queued after confirm
        private Coroutine     _toastCoroutine;

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_panel         != null) _panel.SetActive(false);
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            if (_toastRoot     != null) _toastRoot.SetActive(false);
            if (_comparePanel  != null) _comparePanel.SetActive(false);
        }

        private void Start()
        {
            if (_btnBuy   != null) _btnBuy  .onClick.AddListener(RequestBuy);
            if (_btnSell  != null) _btnSell .onClick.AddListener(RequestSell);
            if (_btnClose != null) _btnClose.onClick.AddListener(Close);

            if (_btnConfirmYes != null) _btnConfirmYes.onClick.AddListener(ConfirmAction);
            if (_btnConfirmNo  != null) _btnConfirmNo .onClick.AddListener(CancelConfirm);

            // Filter buttons
            if (_filterAll     != null) _filterAll    .onClick.AddListener(() => SetFilter("All"));
            if (_filterWeapons != null) _filterWeapons.onClick.AddListener(() => SetFilter("Weapon"));
            if (_filterArmor   != null) _filterArmor  .onClick.AddListener(() => SetFilter("Armor"));
            if (_filterMedical != null) _filterMedical.onClick.AddListener(() => SetFilter("Medical"));
            if (_filterMisc    != null) _filterMisc   .onClick.AddListener(() => SetFilter("Misc"));
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_panel != null && _panel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                if (_confirmDialog != null && _confirmDialog.activeSelf)
                    CancelConfirm();
                else
                    Close();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Open merchant UI for a given .utm resref.</summary>
        public void Open(string utmResRef)
        {
            var rm = SceneBootstrapper.Resources;
            if (rm == null) { Debug.LogWarning("[MerchantUI] ResourceManager not ready."); return; }

            byte[] utmData = rm.GetResource(utmResRef, KotOR.FileReaders.ResourceType.UTM);
            if (utmData == null)
            {
                Debug.LogWarning($"[MerchantUI] UTM not found: '{utmResRef}'");
                return;
            }

            _merchant = MerchantSystem.ParseUtm(utmData, utmResRef);
            if (_merchant == null) { Debug.LogWarning("[MerchantUI] UTM parse failed."); return; }

            _currentFilter = "All";
            if (_panel != null) _panel.SetActive(true);
            Time.timeScale = 0f;
            RefreshUI();

            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());
        }

        /// <summary>Close the merchant window and resume time.</summary>
        public void Close()
        {
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            if (_panel         != null) _panel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FILTER
        // ══════════════════════════════════════════════════════════════════════

        private void SetFilter(string filter)
        {
            _currentFilter = filter;
            PopulateMerchantList();
        }

        private bool ItemMatchesFilter(ItemData item)
        {
            if (_currentFilter == "All") return true;
            if (item == null) return false;
            return _currentFilter switch
            {
                "Weapon"  => item.IsWeapon,
                "Armor"   => item.IsArmour,
                "Medical" => item.IsUsable,
                "Misc"    => !item.IsWeapon && !item.IsArmour,
                _         => true
            };
        }

        // ══════════════════════════════════════════════════════════════════════
        //  REFRESH
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshUI()
        {
            if (_merchantNameText != null)
                _merchantNameText.text = _merchant?.Name ?? "Merchant";

            RefreshCredits();
            PopulateMerchantList();
            PopulatePlayerList();
            ClearDetail();
        }

        private void RefreshCredits()
        {
            var inv = InventoryManager.Instance;
            if (_creditsText != null)
                _creditsText.text = $"Credits: {inv?.PlayerCredits ?? 0:N0}";
        }

        private void PopulateMerchantList()
        {
            if (_merchantListContainer == null) return;
            foreach (Transform child in _merchantListContainer) Destroy(child.gameObject);
            if (_merchant?.Inventory == null) return;

            foreach (var mi in _merchant.Inventory)
            {
                ItemData item = LoadItemData(mi.ResRef);
                if (item == null) continue;
                if (!ItemMatchesFilter(item)) continue;

                // Show stock count if limited
                string stockLabel = mi.Infinite == 1 ? "" : $" ({mi.Stock})";
                int price = MerchantSystem.BuyPrice(item.Cost, _merchant, GetPersuadeSkill());
                bool canAfford = InventoryManager.Instance?.PlayerCredits >= price;

                var captured = item;
                var row = AddRow(_merchantListContainer,
                    item.Name + stockLabel,
                    $"{price:N0} cr",
                    () => SelectMerchantItem(captured),
                    canAfford ? Color.white : new Color(1f, 0.4f, 0.4f));
            }
        }

        private void PopulatePlayerList()
        {
            if (_playerListContainer == null) return;
            foreach (Transform child in _playerListContainer) Destroy(child.gameObject);

            var inv = InventoryManager.Instance?.PlayerInventory;
            if (inv == null) return;

            foreach (var slot in inv.AllSlots)
            {
                if (slot.Item == null) continue;
                int price = MerchantSystem.SellPrice(slot.Item.Cost, _merchant, GetPersuadeSkill());
                var capturedSlot = slot;
                AddRow(_playerListContainer,
                    slot.Item.Name,
                    $"{price:N0} cr",
                    () => SelectPlayerSlot(capturedSlot),
                    Color.white);
            }
        }

        private Button AddRow(Transform container, string itemName, string priceText,
                              Action onClick, Color nameColor)
        {
            if (_itemRowPrefab == null) return null;
            var row = Instantiate(_itemRowPrefab, container);
            var labels = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (labels.Length > 0) { labels[0].text = itemName; labels[0].color = nameColor; }
            if (labels.Length > 1) labels[1].text = priceText;
            row.onClick.AddListener(() => onClick?.Invoke());
            return row;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SELECTION
        // ══════════════════════════════════════════════════════════════════════

        private void SelectMerchantItem(ItemData item)
        {
            _selectedMerchantItem = item;
            _selectedPlayerSlot   = null;
            _isBuyMode            = true;
            int price = MerchantSystem.BuyPrice(item.Cost, _merchant, GetPersuadeSkill());
            ShowDetail(item, price, "Buy");
        }

        private void SelectPlayerSlot(InventorySlot slot)
        {
            _selectedPlayerSlot   = slot;
            _selectedMerchantItem = null;
            _isBuyMode            = false;
            int price = MerchantSystem.SellPrice(slot.Item.Cost, _merchant, GetPersuadeSkill());
            ShowDetail(slot.Item, price, "Sell");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DETAIL PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void ClearDetail()
        {
            if (_detailPanel  != null) _detailPanel.SetActive(false);
            if (_comparePanel != null) _comparePanel.SetActive(false);
            if (_btnBuy       != null) _btnBuy .gameObject.SetActive(false);
            if (_btnSell      != null) _btnSell.gameObject.SetActive(false);
        }

        private void ShowDetail(ItemData item, int price, string action)
        {
            if (item == null) { ClearDetail(); return; }

            if (_detailPanel != null) _detailPanel.SetActive(true);

            // Icon
            if (_detailIcon != null)
            {
                var tex = TextureCache.Get(item.IconResRef);
                if (tex != null)
                    _detailIcon.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }

            // Text
            if (_detailName   != null) _detailName.text   = item.Name;
            if (_detailDesc   != null) _detailDesc.text   = item.Description;
            if (_detailType    != null) _detailType.text    = item.IsWeapon ? "Weapon" : item.IsArmour ? "Armor" : "Item";
            if (_detailCharges != null) _detailCharges.text = item.Charges > 0 ? $"Charges: {item.Charges}" : "";

            // Price with color hint
            if (_detailPrice != null)
            {
                int credits = InventoryManager.Instance?.PlayerCredits ?? 0;
                bool ok     = action == "Sell" || credits >= price;
                _detailPrice.text  = $"{action} price: {price:N0} cr";
                _detailPrice.color = ok ? Color.white : new Color(1f, 0.35f, 0.35f);
            }

            // Stat comparison
            ShowComparison(item);

            // Action buttons
            if (_btnBuy  != null)
            {
                _btnBuy.gameObject.SetActive(_isBuyMode);
                if (_btnBuyLabel != null) _btnBuyLabel.text = $"Buy  {price:N0} cr";
            }
            if (_btnSell != null)
            {
                _btnSell.gameObject.SetActive(!_isBuyMode);
                if (_btnSellLabel != null) _btnSellLabel.text = $"Sell  {price:N0} cr";
            }
        }

        private void ShowComparison(ItemData item)
        {
            if (_comparePanel == null) return;

            // Try to find currently equipped item of the same slot
            ItemData equipped = GetEquippedItemForSlot(item);

            _comparePanel.SetActive(true);

            if (_compareEquipped != null)
                _compareEquipped.text = equipped != null
                    ? $"Equipped: {equipped.Name}"
                    : "Equipped: — (nothing)";

            // Damage comparison (use DamageNumDice / DamageDie)
            if (_compareDamage != null)
            {
                float eMean = equipped != null ? equipped.DamageNumDice * (equipped.DamageDie + 1) / 2f : 0f;
                float nMean = item.DamageNumDice * (item.DamageDie + 1) / 2f;
                string eStr = equipped != null ? $"{equipped.DamageNumDice}d{equipped.DamageDie}" : "--";
                string nStr = $"{item.DamageNumDice}d{item.DamageDie}";
                _compareDamage.text  = $"DMG  {eStr} → {nStr}";
                _compareDamage.color = CompareColor(nMean, eMean);
            }

            // AC comparison
            if (_compareAC != null)
            {
                int eAC = equipped?.ACBonus ?? 0;
                int nAC = item.ACBonus;
                _compareAC.text  = $"AC  {eAC:+0;-0;0} → {nAC:+0;-0;0}";
                _compareAC.color = CompareColor(nAC, eAC);
            }

            // Attack bonus comparison
            if (_compareAttack != null)
            {
                int eAtk = equipped?.AttackBonus ?? 0;
                int nAtk = item.AttackBonus;
                _compareAttack.text  = $"ATK  {eAtk:+0;-0;0} → {nAtk:+0;-0;0}";
                _compareAttack.color = CompareColor(nAtk, eAtk);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONFIRMATION DIALOG
        // ══════════════════════════════════════════════════════════════════════

        private void RequestBuy()
        {
            var inv = InventoryManager.Instance;
            if (inv == null || _selectedMerchantItem == null) return;

            int price = MerchantSystem.BuyPrice(_selectedMerchantItem.Cost, _merchant, GetPersuadeSkill());
            if (inv.PlayerCredits < price)
            {
                ShowToast($"Not enough credits! Need {price:N0} cr.");
                return;
            }

            int after = inv.PlayerCredits - price;
            OpenConfirmDialog(
                "Confirm Purchase",
                _selectedMerchantItem,
                price,
                after,
                () => ExecuteBuy(price));
        }

        private void RequestSell()
        {
            var inv = InventoryManager.Instance;
            if (inv == null || _selectedPlayerSlot?.Item == null) return;

            int price = MerchantSystem.SellPrice(_selectedPlayerSlot.Item.Cost, _merchant, GetPersuadeSkill());
            int after = (inv.PlayerCredits) + price;
            OpenConfirmDialog(
                "Confirm Sale",
                _selectedPlayerSlot.Item,
                price,
                after,
                () => ExecuteSell(price));
        }

        private void OpenConfirmDialog(string title, ItemData item, int price, int creditsAfter,
                                       Action onConfirm)
        {
            if (_confirmDialog == null) { onConfirm?.Invoke(); return; }

            _pendingAction = onConfirm;

            if (_confirmTitle        != null) _confirmTitle.text        = title;
            if (_confirmItemName     != null) _confirmItemName.text     = item?.Name ?? "Item";
            if (_confirmPrice        != null) _confirmPrice.text        = $"{price:N0} Credits";
            if (_confirmCreditsAfter != null) _confirmCreditsAfter.text = $"Balance after: {creditsAfter:N0} cr";

            if (_confirmIcon != null && item != null)
            {
                var tex = TextureCache.Get(item.IconResRef);
                if (tex != null)
                    _confirmIcon.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }

            _confirmDialog.SetActive(true);
        }

        private void ConfirmAction()
        {
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            _pendingAction?.Invoke();
            _pendingAction = null;
        }

        private void CancelConfirm()
        {
            if (_confirmDialog != null) _confirmDialog.SetActive(false);
            _pendingAction = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUY / SELL EXECUTION
        // ══════════════════════════════════════════════════════════════════════

        private void ExecuteBuy(int price)
        {
            var inv = InventoryManager.Instance;
            if (inv == null || _selectedMerchantItem == null) return;

            inv.SpendCredits(price);
            inv.PickUp(_selectedMerchantItem.ResRef);

            // Publish event for achievements etc.
            EventBus.Publish(EventBus.EventType.ItemPickedUp,
                new EventBus.ItemEventArgs(_selectedMerchantItem.ResRef,
                    _selectedMerchantItem.Name, price));

            ShowToast($"Bought '{_selectedMerchantItem.Name}' for {price:N0} cr.");
            Debug.Log($"[MerchantUI] Bought '{_selectedMerchantItem.Name}' for {price} cr.");
            _selectedMerchantItem = null;
            RefreshUI();
        }

        private void ExecuteSell(int price)
        {
            var inv = InventoryManager.Instance;
            if (inv == null || _selectedPlayerSlot?.Item == null) return;

            string soldName = _selectedPlayerSlot.Item.Name;
            inv.RemoveItem(_selectedPlayerSlot.Item.ResRef);
            inv.AddCredits(price);

            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());

            ShowToast($"Sold '{soldName}' for {price:N0} cr.");
            Debug.Log($"[MerchantUI] Sold '{soldName}' for {price} cr.");
            _selectedPlayerSlot = null;
            RefreshUI();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TOAST NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        private void ShowToast(string message)
        {
            if (_toastRoot == null) return;
            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            _toastCoroutine = StartCoroutine(ToastRoutine(message));
        }

        private IEnumerator ToastRoutine(string message)
        {
            if (_toastText != null) _toastText.text = message;
            _toastRoot.SetActive(true);

            // Simple fade-in/out using unscaled time (game is paused)
            float elapsed = 0f;
            var cg = _toastRoot.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                while (elapsed < 0.25f)
                {
                    cg.alpha = Mathf.Lerp(0f, 1f, elapsed / 0.25f);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                cg.alpha = 1f;
            }

            yield return new WaitForSecondsRealtime(_toastDuration - 0.5f);

            if (cg != null)
            {
                elapsed = 0f;
                while (elapsed < 0.5f)
                {
                    cg.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.5f);
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            _toastRoot.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private static ItemData LoadItemData(string resref)
        {
            var rm = SceneBootstrapper.Resources;
            if (rm == null) return null;
            byte[] data = rm.GetResource(resref, KotOR.FileReaders.ResourceType.UTI);
            if (data == null) return null;
            return KotORUnity.Inventory.UtiReader.ParseUti(data, resref);
        }

        private static int GetPersuadeSkill()
        {
            // PlayerStats doesn't yet expose skill ranks; default to 0
            return 0;
        }

        /// <summary>
        /// Find the item currently equipped in the slot that best matches the
        /// given item (weapon → weapon slot; armor → armor slot; etc.).
        /// </summary>
        private static ItemData GetEquippedItemForSlot(ItemData item)
        {
            if (item == null) return null;
            var inv = InventoryManager.Instance?.PlayerInventory;
            if (inv == null) return null;

            if (item.IsWeapon)  return inv.GetEquipped(EquipSlot.WeaponR);
            if (item.IsArmour)  return inv.GetEquipped(EquipSlot.Body);
            return null;
        }

        private static float ParseDamageMean_unused(string dice)
        {
            if (string.IsNullOrEmpty(dice) || dice == "--") return 0f;
            try
            {
                dice = dice.ToLowerInvariant().Trim();
                float bonus = 0f;
                int plusIdx = dice.IndexOf('+');
                if (plusIdx >= 0)
                {
                    if (float.TryParse(dice.Substring(plusIdx + 1), out float b)) bonus = b;
                    dice = dice.Substring(0, plusIdx);
                }
                int dIdx = dice.IndexOf('d');
                if (dIdx < 0) return float.TryParse(dice, out float flat) ? flat + bonus : bonus;
                int numDice = dIdx > 0 && int.TryParse(dice.Substring(0, dIdx), out int nd) ? nd : 1;
                int sides   = int.TryParse(dice.Substring(dIdx + 1), out int s) ? s : 1;
                return numDice * (sides + 1) / 2f + bonus;
            }
            catch { return 0f; }
        }

        private static Color CompareColor(float newVal, float oldVal)
        {
            if (newVal > oldVal) return new Color(0.4f, 1f, 0.4f);   // green = better
            if (newVal < oldVal) return new Color(1f, 0.4f, 0.4f);   // red   = worse
            return Color.white;
        }
    }
}
