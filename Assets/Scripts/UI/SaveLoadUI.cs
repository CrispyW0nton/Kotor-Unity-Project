using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.SaveSystem;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  SAVE / LOAD UI  —  KotOR-style save/load panel
    //
    //  Panels:
    //    • SavePanel  : grid of 7 save slots (Quick, Auto, Manual 1-5)
    //    • LoadPanel  : same grid, Load button replaces Save button per slot
    //    • Confirmation dialog: "Are you sure? This will overwrite…"
    //
    //  Slot card shows:
    //    • Thumbnail screenshot (captured on save)
    //    • Area / module name
    //    • Timestamp  (YYYY-MM-DD HH:MM)
    //    • Play time  (HH:MM:SS)
    //    • Player name + level
    //    • "[EMPTY]" if unused
    //
    //  Screenshot capture:
    //    Saves a 256×144 PNG via ScreenCapture to persistentDataPath.
    //    Loaded back as a Texture2D and displayed in the thumbnail Image.
    //
    //  EventBus:
    //    Publishes  GamePaused / GameResumed around panel open/close.
    //    Subscribes GameSaved / GameLoaded to refresh slot displays.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Save / Load menu panel controller.
    /// Call Show(isSave: true) to open the Save panel, or Show(isSave: false) to open Load.
    /// </summary>
    public class SaveLoadUI : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────

        [Header("Root Panels")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private GameObject _savePanel;
        [SerializeField] private GameObject _loadPanel;

        [Header("Slot Prefab & Container")]
        [SerializeField] private GameObject _slotCardPrefab;
        [SerializeField] private Transform  _saveSlotContainer;
        [SerializeField] private Transform  _loadSlotContainer;

        [Header("Header Labels")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Confirmation Dialog")]
        [SerializeField] private GameObject      _confirmDialog;
        [SerializeField] private TextMeshProUGUI _confirmBodyText;
        [SerializeField] private Button          _confirmYesButton;
        [SerializeField] private Button          _confirmNoButton;

        [Header("Notification")]
        [SerializeField] private GameObject      _notificationPanel;
        [SerializeField] private TextMeshProUGUI _notificationText;
        [SerializeField] private float           _notificationDuration = 2.5f;

        [Header("Close")]
        [SerializeField] private Button          _closeButton;

        // ── RUNTIME ───────────────────────────────────────────────────────────

        private bool _isSaveMode;
        private SaveSlot _pendingSlot;
        private bool     _pendingIsOverwrite;

        private readonly List<SaveSlotCardUI> _saveCards = new List<SaveSlotCardUI>();
        private readonly List<SaveSlotCardUI> _loadCards = new List<SaveSlotCardUI>();

        // Screenshot thumbnail size
        private const int THUMB_W = 256;
        private const int THUMB_H = 144;

        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static SaveLoadUI Instance { get; private set; }

        // ── UNITY ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _closeButton?.onClick.AddListener(Hide);
            _confirmYesButton?.onClick.AddListener(OnConfirmYes);
            _confirmNoButton?.onClick.AddListener(OnConfirmNo);

            BuildSlotCards();
            Hide();
        }

        private void Start()
        {
            EventBus.Subscribe(EventBus.EventType.GameSaved,  _ => RefreshAllSlots());
            EventBus.Subscribe(EventBus.EventType.GameLoaded, _ => Hide());
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.GameSaved,  _ => RefreshAllSlots());
            EventBus.Unsubscribe(EventBus.EventType.GameLoaded, _ => Hide());
        }

        private void Update()
        {
            if (_rootPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                Hide();
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Open the Save panel (isSave=true) or Load panel (isSave=false).</summary>
        public void Show(bool isSave)
        {
            _isSaveMode = isSave;
            _rootPanel.SetActive(true);
            _savePanel?.SetActive(isSave);
            _loadPanel?.SetActive(!isSave);
            HideConfirmDialog();

            if (_titleText != null)
                _titleText.text = isSave ? "SAVE GAME" : "LOAD GAME";

            RefreshAllSlots();
            EventBus.Publish(EventBus.EventType.GamePaused);
        }

        public void ShowSave() => Show(true);
        public void ShowLoad() => Show(false);

        public void Hide()
        {
            _rootPanel.SetActive(false);
            HideConfirmDialog();
            EventBus.Publish(EventBus.EventType.GameResumed);
        }

        // ── BUILD CARDS ───────────────────────────────────────────────────────

        private static readonly (SaveSlot slot, string label)[] SlotDefs =
        {
            (SaveSlot.QuickSave, "Quick Save"),
            (SaveSlot.AutoSave,  "Auto Save"),
            (SaveSlot.Manual1,   "Save Slot 1"),
            (SaveSlot.Manual2,   "Save Slot 2"),
            (SaveSlot.Manual3,   "Save Slot 3"),
            (SaveSlot.Manual4,   "Save Slot 4"),
            (SaveSlot.Manual5,   "Save Slot 5"),
        };

        private void BuildSlotCards()
        {
            if (_slotCardPrefab == null) return;

            foreach (var def in SlotDefs)
            {
                // Save panel cards
                if (_saveSlotContainer != null)
                {
                    var go   = Instantiate(_slotCardPrefab, _saveSlotContainer);
                    var card = go.GetComponent<SaveSlotCardUI>() ?? go.AddComponent<SaveSlotCardUI>();
                    card.Init(def.slot, def.label, true, this);
                    _saveCards.Add(card);
                }

                // Load panel cards
                if (_loadSlotContainer != null)
                {
                    var go   = Instantiate(_slotCardPrefab, _loadSlotContainer);
                    var card = go.GetComponent<SaveSlotCardUI>() ?? go.AddComponent<SaveSlotCardUI>();
                    card.Init(def.slot, def.label, false, this);
                    _loadCards.Add(card);
                }
            }
        }

        // ── REFRESH ───────────────────────────────────────────────────────────

        private void RefreshAllSlots()
        {
            var mgr = SaveManager.Instance;
            if (mgr == null) return;

            foreach (var card in _saveCards) card.Refresh(mgr);
            foreach (var card in _loadCards) card.Refresh(mgr);
        }

        // ── SAVE / LOAD ACTIONS ───────────────────────────────────────────────

        /// <summary>Called by SaveSlotCardUI when user clicks.</summary>
        internal void OnSlotClicked(SaveSlot slot, bool isSaveCard)
        {
            var mgr    = SaveManager.Instance;
            bool exists = mgr != null && mgr.SaveExists(slot);

            if (isSaveCard)
            {
                _pendingSlot        = slot;
                _pendingIsOverwrite = exists;

                if (exists && slot != SaveSlot.QuickSave && slot != SaveSlot.AutoSave)
                {
                    // Confirm overwrite for manual slots
                    ShowConfirmDialog($"Overwrite {SlotLabelFor(slot)}?\nThis cannot be undone.");
                }
                else
                {
                    ExecuteSave(slot);
                }
            }
            else
            {
                if (!exists)
                {
                    ShowNotification("No save data in this slot.");
                    return;
                }
                _pendingSlot = slot;
                ShowConfirmDialog($"Load {SlotLabelFor(slot)}?\nUnsaved progress will be lost.");
            }
        }

        private void ExecuteSave(SaveSlot slot)
        {
            var mgr = SaveManager.Instance;
            if (mgr == null) return;

            // Capture screenshot before saving
            StartCoroutine(CaptureScreenshotThenSave(slot));
        }

        private IEnumerator CaptureScreenshotThenSave(SaveSlot slot)
        {
            yield return new WaitForEndOfFrame();

            // Capture thumbnail
            string thumbPath = GetThumbnailPath(slot);
            try
            {
                var rt  = new RenderTexture(THUMB_W, THUMB_H, 24);
                var tex = new Texture2D(THUMB_W, THUMB_H, TextureFormat.RGB24, false);
                UnityEngine.Camera.main.targetTexture = rt;
                UnityEngine.Camera.main.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, THUMB_W, THUMB_H), 0, 0);
                tex.Apply();
                UnityEngine.Camera.main.targetTexture = null;
                RenderTexture.active = null;
                Destroy(rt);
                File.WriteAllBytes(thumbPath, tex.EncodeToPNG());
                Destroy(tex);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveLoadUI] Screenshot failed: {ex.Message}");
            }

            // Perform the actual save
            SaveManager.Instance.Save(slot);
            ShowNotification($"Game saved to {SlotLabelFor(slot)}.");
            RefreshAllSlots();
        }

        private void ExecuteLoad(SaveSlot slot)
        {
            var mgr = SaveManager.Instance;
            if (mgr == null) return;
            mgr.Load(slot);
            // Panel will close via GameLoaded event subscription
        }

        // ── CONFIRMATION DIALOG ───────────────────────────────────────────────

        private void ShowConfirmDialog(string body)
        {
            if (_confirmDialog == null) return;
            _confirmDialog.SetActive(true);
            if (_confirmBodyText != null) _confirmBodyText.text = body;
        }

        private void HideConfirmDialog()
        {
            _confirmDialog?.SetActive(false);
        }

        private void OnConfirmYes()
        {
            HideConfirmDialog();
            if (_isSaveMode) ExecuteSave(_pendingSlot);
            else             ExecuteLoad(_pendingSlot);
        }

        private void OnConfirmNo()
        {
            HideConfirmDialog();
        }

        // ── NOTIFICATION ─────────────────────────────────────────────────────

        private void ShowNotification(string msg)
        {
            if (_notificationPanel == null) return;
            if (_notificationText != null) _notificationText.text = msg;
            _notificationPanel.SetActive(true);
            StopAllCoroutines();
            StartCoroutine(HideNotificationAfter(_notificationDuration));
        }

        private IEnumerator HideNotificationAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            _notificationPanel?.SetActive(false);
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        internal static string GetThumbnailPath(SaveSlot slot)
        {
            string fileName = slot switch
            {
                SaveSlot.QuickSave => "thumb_quick.png",
                SaveSlot.AutoSave  => "thumb_auto.png",
                SaveSlot.Manual1   => "thumb_01.png",
                SaveSlot.Manual2   => "thumb_02.png",
                SaveSlot.Manual3   => "thumb_03.png",
                SaveSlot.Manual4   => "thumb_04.png",
                SaveSlot.Manual5   => "thumb_05.png",
                _                  => "thumb_unknown.png",
            };
            return Path.Combine(Application.persistentDataPath, fileName);
        }

        private string SlotLabelFor(SaveSlot slot) =>
            System.Array.Find(SlotDefs, d => d.slot == slot).label ?? slot.ToString();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SAVE SLOT CARD UI
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A single save-slot card in the Save/Load panel.
    /// </summary>
    public class SaveSlotCardUI : MonoBehaviour
    {
        [SerializeField] private Image           _thumbnailImage;
        [SerializeField] private TextMeshProUGUI _slotNameText;
        [SerializeField] private TextMeshProUGUI _areaText;
        [SerializeField] private TextMeshProUGUI _timestampText;
        [SerializeField] private TextMeshProUGUI _playtimeText;
        [SerializeField] private TextMeshProUGUI _charInfoText;
        [SerializeField] private GameObject      _emptyOverlay;
        [SerializeField] private Button          _actionButton;
        [SerializeField] private TextMeshProUGUI _actionButtonText;
        [SerializeField] private Image           _selectedHighlight;

        private SaveSlot   _slot;
        private string     _slotLabel;
        private bool       _isSaveCard;
        private SaveLoadUI _owner;
        private bool       _selected;

        // Default empty thumbnail colour
        private static readonly Color EmptyThumbColor = new Color(0.15f, 0.15f, 0.2f, 1f);

        internal void Init(SaveSlot slot, string label, bool isSaveCard, SaveLoadUI owner)
        {
            _slot       = slot;
            _slotLabel  = label;
            _isSaveCard = isSaveCard;
            _owner      = owner;

            if (_slotNameText   != null) _slotNameText.text   = label;
            if (_actionButtonText != null)
                _actionButtonText.text = isSaveCard ? "Save" : "Load";
            if (_selectedHighlight != null) _selectedHighlight.enabled = false;

            _actionButton?.onClick.AddListener(OnActionClicked);
        }

        internal void Refresh(SaveManager mgr)
        {
            if (mgr == null) return;

            bool exists = mgr.SaveExists(_slot);

            // Empty overlay
            _emptyOverlay?.SetActive(!exists);

            // Disable Load button if slot is empty
            if (!_isSaveCard && _actionButton != null)
                _actionButton.interactable = exists;

            if (!exists)
            {
                SetEmptyDisplay();
                return;
            }

            // Load metadata from the save file header
            RefreshFromDisk(mgr);
        }

        private void SetEmptyDisplay()
        {
            if (_thumbnailImage != null)
            {
                _thumbnailImage.sprite = null;
                _thumbnailImage.color  = EmptyThumbColor;
            }
            if (_areaText      != null) _areaText.text      = "— Empty —";
            if (_timestampText != null) _timestampText.text = "";
            if (_playtimeText  != null) _playtimeText.text  = "";
            if (_charInfoText  != null) _charInfoText.text  = "";
        }

        private void RefreshFromDisk(SaveManager mgr)
        {
            // Try to load just the header fields from the JSON save file
            try
            {
                string json = mgr.GetSaveJson(_slot);
                if (string.IsNullOrEmpty(json)) { SetEmptyDisplay(); return; }

                var state = JsonUtility.FromJson<SaveLoadUI_MetaOnly>(json);

                // Thumbnail
                string thumbPath = SaveLoadUI.GetThumbnailPath(_slot);
                if (File.Exists(thumbPath))
                {
                    byte[] bytes = File.ReadAllBytes(thumbPath);
                    var tex      = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        _thumbnailImage.sprite = Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            Vector2.one * 0.5f);
                        _thumbnailImage.color = Color.white;
                    }
                }
                else if (_thumbnailImage != null)
                {
                    _thumbnailImage.sprite = null;
                    _thumbnailImage.color  = new Color(0.25f, 0.25f, 0.35f, 1f);
                }

                // Text fields
                if (_areaText      != null)
                    _areaText.text = string.IsNullOrEmpty(state.moduleName) ? "Unknown Area" : state.moduleName;

                if (_timestampText != null && !string.IsNullOrEmpty(state.timestamp))
                {
                    if (DateTime.TryParse(state.timestamp, out var dt))
                        _timestampText.text = dt.ToString("yyyy-MM-dd  HH:mm");
                    else
                        _timestampText.text = state.timestamp;
                }

                if (_playtimeText  != null) _playtimeText.text  = "";   // requires play-time tracking
                if (_charInfoText  != null)
                {
                    string charInfo = "";
                    if (state.playerStats != null)
                        charInfo = $"{state.playerStats.characterName}   Lv {state.playerStats.level}";
                    _charInfoText.text = charInfo;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSlotCardUI] Failed to read slot {_slot}: {ex.Message}");
                SetEmptyDisplay();
            }
        }

        private void OnActionClicked()
        {
            _owner?.OnSlotClicked(_slot, _isSaveCard);
        }

        // ── Select highlight ──────────────────────────────────────────────────
        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_selectedHighlight != null) _selectedHighlight.enabled = selected;
        }

        // ── Minimal deserialization class for reading save header ─────────────
        [Serializable]
        private class SaveLoadUI_MetaOnly
        {
            public string            timestamp;
            public string            moduleName;
            public PlayerStatsHeader playerStats;

            [Serializable]
            public class PlayerStatsHeader
            {
                public string characterName;
                public int    level;
            }
        }
    }
}
