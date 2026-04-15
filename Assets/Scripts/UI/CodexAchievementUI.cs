using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  CODEX UI  —  In-game discovery log browser
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Layout:
    //    Left panel  : Category tabs (Planet / Creature / Lore / Person)
    //                + scrollable entry list
    //    Right panel : Selected entry detail (title, icon, body text)
    //    Toast popup : Bottom-screen slide-up when an entry is first discovered
    //
    //  Toggle with "C" or from the pause menu.

    /// <summary>
    /// Full-screen Codex browser. Tabs by category, scrollable entry list,
    /// detail panel, and a toast notification on new discoveries.
    /// </summary>
    public class CodexUI : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static CodexUI Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private KeyCode    _toggleKey = KeyCode.C;

        [Header("Category Tabs")]
        [SerializeField] private Button _tabAll;
        [SerializeField] private Button _tabPlanets;
        [SerializeField] private Button _tabCreatures;
        [SerializeField] private Button _tabLore;
        [SerializeField] private Button _tabPersons;
        [SerializeField] private TextMeshProUGUI _totalCountLabel;

        [Header("Entry List")]
        [SerializeField] private Transform  _entryListContainer;
        [SerializeField] private GameObject _entryRowPrefab;
        [SerializeField] private ScrollRect _listScrollRect;

        [Header("Detail Panel")]
        [SerializeField] private TextMeshProUGUI _detailTitle;
        [SerializeField] private TextMeshProUGUI _detailCategory;
        [SerializeField] private TextMeshProUGUI _detailBody;
        [SerializeField] private Image           _detailIcon;
        [SerializeField] private GameObject      _detailPanel;
        [SerializeField] private TextMeshProUGUI _discoveredDateLabel;

        [Header("Toast Notification")]
        [SerializeField] private GameObject      _toastRoot;
        [SerializeField] private TextMeshProUGUI _toastTitle;
        [SerializeField] private TextMeshProUGUI _toastCategory;
        [SerializeField] private float           _toastDuration = 4f;
        [SerializeField] private float           _toastSlideDistance = 80f;

        [Header("Close Button")]
        [SerializeField] private Button _btnClose;

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private bool   _isOpen      = false;
        private string _activeFilter = "";     // "" = all discovered

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RegisterTabListeners();
            _btnClose?.onClick.AddListener(Close);
            SetRootVisible(false);

            // Subscribe to discovery events for toast
            EventBus.Subscribe(EventBus.EventType.CodexEntryDiscovered, OnEntryDiscovered);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.CodexEntryDiscovered, OnEntryDiscovered);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                Toggle();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            SetRootVisible(true);
            RefreshList(_activeFilter);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetRootVisible(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TABS
        // ══════════════════════════════════════════════════════════════════════

        private void RegisterTabListeners()
        {
            _tabAll?.onClick.AddListener(()       => RefreshList(""));
            _tabPlanets?.onClick.AddListener(()   => RefreshList("Planet"));
            _tabCreatures?.onClick.AddListener(() => RefreshList("Creature"));
            _tabLore?.onClick.AddListener(()      => RefreshList("Lore"));
            _tabPersons?.onClick.AddListener(()   => RefreshList("Person"));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENTRY LIST
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshList(string category)
        {
            _activeFilter = category;

            var codex = CodexSystem.Instance;
            if (codex == null) return;

            // Destroy old rows
            foreach (Transform child in _entryListContainer)
                Destroy(child.gameObject);

            // Get discovered entries filtered by category
            var entries = string.IsNullOrEmpty(category)
                ? codex.GetDiscovered()
                : codex.GetByCategory(category).FindAll(e => e.Discovered);

            // Sort: alpha by title
            entries.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            foreach (var entry in entries)
                CreateEntryRow(entry);

            // Update count label
            if (_totalCountLabel != null)
                _totalCountLabel.text = $"{codex.DiscoveredCount} / {codex.TotalCount} discovered";

            // Hide detail until selection
            if (_detailPanel != null) _detailPanel.SetActive(false);
        }

        private void CreateEntryRow(CodexEntry entry)
        {
            if (_entryRowPrefab == null || _entryListContainer == null) return;

            var row = Instantiate(_entryRowPrefab, _entryListContainer);

            // Title label
            var titleLbl = row.GetComponentInChildren<TextMeshProUGUI>();
            if (titleLbl != null) titleLbl.text = entry.Title;

            // Category badge (secondary TMP if present)
            var labels = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (labels.Length > 1) labels[1].text = entry.Category;

            // Click → show detail
            var btn = row.GetComponent<Button>() ?? row.AddComponent<Button>();
            var capturedEntry = entry;
            btn.onClick.AddListener(() => ShowDetail(capturedEntry));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DETAIL PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void ShowDetail(CodexEntry entry)
        {
            if (_detailPanel != null) _detailPanel.SetActive(true);

            if (_detailTitle    != null) _detailTitle.text    = entry.Title;
            if (_detailCategory != null) _detailCategory.text = entry.Category;
            if (_detailBody     != null) _detailBody.text     = entry.Body;

            if (_discoveredDateLabel != null)
            {
                if (!string.IsNullOrEmpty(entry.DiscoveredTimestamp) &&
                    DateTime.TryParse(entry.DiscoveredTimestamp, out DateTime dt))
                    _discoveredDateLabel.text = $"Discovered: {dt:yyyy-MM-dd HH:mm}";
                else
                    _discoveredDateLabel.text = "";
            }

            // Load icon texture if resref set
            if (_detailIcon != null && !string.IsNullOrEmpty(entry.IconResRef))
            {
                var tex = KotOR.Parsers.TextureCache.Get(entry.IconResRef);
                if (tex != null)
                    _detailIcon.sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TOAST NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        private void OnEntryDiscovered(EventBus.GameEventArgs args)
        {
            if (args is EventBus.CodexEventArgs ce)
                ShowToast(ce.EntryTitle, ce.Category);
        }

        public void ShowToast(string title, string category)
        {
            StopCoroutine("ToastRoutine");
            StartCoroutine(ToastRoutine(title, category));
        }

        private IEnumerator ToastRoutine(string title, string category)
        {
            if (_toastRoot == null) yield break;

            if (_toastTitle    != null) _toastTitle.text    = $"Codex: {title}";
            if (_toastCategory != null) _toastCategory.text = category;

            // Slide in from bottom
            var rt = _toastRoot.GetComponent<RectTransform>();
            _toastRoot.SetActive(true);

            float t = 0f;
            Vector2 hiddenPos  = rt != null ? rt.anchoredPosition + Vector2.down * _toastSlideDistance : Vector2.zero;
            Vector2 visiblePos = rt != null ? rt.anchoredPosition : Vector2.zero;

            if (rt != null)
            {
                rt.anchoredPosition = hiddenPos;
                while (t < 0.3f)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = Vector2.Lerp(hiddenPos, visiblePos, t / 0.3f);
                    yield return null;
                }
                rt.anchoredPosition = visiblePos;
            }

            yield return new WaitForSecondsRealtime(_toastDuration);

            // Slide out
            t = 0f;
            if (rt != null)
            {
                while (t < 0.3f)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = Vector2.Lerp(visiblePos, hiddenPos, t / 0.3f);
                    yield return null;
                }
            }
            _toastRoot.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void SetRootVisible(bool v)
        {
            if (_rootPanel != null) _rootPanel.SetActive(v);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ACHIEVEMENT UI  —  In-game achievement browser + toast notifications
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Layout:
    //    Top bar      : Total points counter, progress bar
    //    Filter strip : All / Combat / Exploration / Story / Mastery / Secret
    //    Grid/List    : Achievement cards (locked = greyed out, hidden if secret)
    //    Toast popup  : Slide-up notification on unlock

    /// <summary>
    /// In-game achievement browser and unlock toast notifications.
    /// Toggle with "J" (default) or from the pause menu.
    /// </summary>
    public class AchievementUI : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static AchievementUI Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private KeyCode    _toggleKey = KeyCode.J;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _totalPointsLabel;
        [SerializeField] private Slider          _overallProgressBar;
        [SerializeField] private TextMeshProUGUI _unlockedCountLabel;

        [Header("Filter Buttons")]
        [SerializeField] private Button _filterAll;
        [SerializeField] private Button _filterCombat;
        [SerializeField] private Button _filterExploration;
        [SerializeField] private Button _filterStory;
        [SerializeField] private Button _filterMastery;
        [SerializeField] private Button _filterSecret;

        [Header("Achievement Card List")]
        [SerializeField] private Transform  _cardContainer;
        [SerializeField] private GameObject _cardPrefab;

        [Header("Toast")]
        [SerializeField] private GameObject      _toastRoot;
        [SerializeField] private TextMeshProUGUI _toastTitle;
        [SerializeField] private TextMeshProUGUI _toastPoints;
        [SerializeField] private float           _toastDuration     = 5f;
        [SerializeField] private float           _toastSlideDistance = 100f;

        [Header("Close")]
        [SerializeField] private Button _btnClose;

        // ── STATE ──────────────────────────────────────────────────────────────
        private bool   _isOpen         = false;
        private AchievementCategory? _activeFilter = null;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            RegisterFilterListeners();
            _btnClose?.onClick.AddListener(Close);
            SetRootVisible(false);
            if (_toastRoot != null) _toastRoot.SetActive(false);

            EventBus.Subscribe(EventBus.EventType.AchievementUnlocked, OnAchievementUnlocked);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.AchievementUnlocked, OnAchievementUnlocked);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                Toggle();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            SetRootVisible(true);
            RefreshCards(_activeFilter);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetRootVisible(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FILTER
        // ══════════════════════════════════════════════════════════════════════

        private void RegisterFilterListeners()
        {
            _filterAll?.onClick.AddListener(()         => RefreshCards(null));
            _filterCombat?.onClick.AddListener(()      => RefreshCards(AchievementCategory.Combat));
            _filterExploration?.onClick.AddListener(() => RefreshCards(AchievementCategory.Exploration));
            _filterStory?.onClick.AddListener(()       => RefreshCards(AchievementCategory.Story));
            _filterMastery?.onClick.AddListener(()     => RefreshCards(AchievementCategory.Mastery));
            _filterSecret?.onClick.AddListener(()      => RefreshCards(AchievementCategory.Secret));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CARD GRID
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshCards(AchievementCategory? filter)
        {
            _activeFilter = filter;

            var sys = AchievementSystem.Instance;
            if (sys == null) return;

            // Clear old cards
            foreach (Transform c in _cardContainer) Destroy(c.gameObject);

            var all = sys.GetAll();
            foreach (var def in all)
            {
                // Apply filter
                if (filter.HasValue && def.Category != filter.Value) continue;
                // Hide undiscovered secrets
                if (def.IsSecret && !sys.IsUnlocked(def.Id)) continue;

                CreateCard(def, sys);
            }

            // Update header stats
            RefreshHeader(sys);
        }

        private void CreateCard(AchievementDef def, AchievementSystem sys)
        {
            if (_cardPrefab == null || _cardContainer == null) return;

            bool unlocked = sys.IsUnlocked(def.Id);
            int  progress = sys.GetProgress(def.Id);

            var card = Instantiate(_cardPrefab, _cardContainer);

            var labels = card.GetComponentsInChildren<TextMeshProUGUI>();
            if (labels.Length > 0) labels[0].text = unlocked ? def.Title : "???";
            if (labels.Length > 1) labels[1].text = unlocked ? def.Description : "Locked";
            if (labels.Length > 2) labels[2].text = $"{def.PointValue} pts";

            // Progress bar
            var bar = card.GetComponentInChildren<Slider>();
            if (bar != null)
            {
                bar.gameObject.SetActive(def.HasProgress);
                if (def.HasProgress)
                {
                    bar.minValue = 0;
                    bar.maxValue = def.ProgressTarget;
                    bar.value    = progress;
                }
            }

            // Grey-out locked cards
            var canvasGroup = card.GetComponent<CanvasGroup>() ?? card.AddComponent<CanvasGroup>();
            canvasGroup.alpha = unlocked ? 1f : 0.45f;
        }

        private void RefreshHeader(AchievementSystem sys)
        {
            int total    = sys.GetAll().Count;
            int unlocked = sys.GetUnlocked().Count;
            int points   = sys.TotalPoints();

            if (_totalPointsLabel  != null) _totalPointsLabel.text  = $"{points} pts";
            if (_unlockedCountLabel != null) _unlockedCountLabel.text = $"{unlocked} / {total}";
            if (_overallProgressBar != null)
            {
                _overallProgressBar.minValue = 0;
                _overallProgressBar.maxValue = total;
                _overallProgressBar.value    = unlocked;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TOAST NOTIFICATION
        // ══════════════════════════════════════════════════════════════════════

        private void OnAchievementUnlocked(EventBus.GameEventArgs args)
        {
            if (args is EventBus.AchievementEventArgs ae)
                ShowToast(ae.AchievementTitle, ae.PointValue);

            // Refresh if open
            if (_isOpen) RefreshCards(_activeFilter);
        }

        public void ShowToast(string title, int points)
        {
            StopCoroutine("ToastRoutine");
            StartCoroutine(ToastRoutine(title, points));
        }

        private IEnumerator ToastRoutine(string title, int points)
        {
            if (_toastRoot == null) yield break;

            if (_toastTitle  != null) _toastTitle.text  = $"Achievement: {title}";
            if (_toastPoints != null) _toastPoints.text = $"+{points} pts";

            var rt = _toastRoot.GetComponent<RectTransform>();
            _toastRoot.SetActive(true);

            Vector2 visiblePos = rt != null ? rt.anchoredPosition : Vector2.zero;
            Vector2 hiddenPos  = visiblePos + Vector2.down * _toastSlideDistance;

            float t = 0f;
            if (rt != null)
            {
                rt.anchoredPosition = hiddenPos;
                while (t < 0.35f)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = Vector2.Lerp(hiddenPos, visiblePos, t / 0.35f);
                    yield return null;
                }
                rt.anchoredPosition = visiblePos;
            }

            yield return new WaitForSecondsRealtime(_toastDuration);

            t = 0f;
            if (rt != null)
            {
                while (t < 0.35f)
                {
                    t += Time.unscaledDeltaTime;
                    rt.anchoredPosition = Vector2.Lerp(visiblePos, hiddenPos, t / 0.35f);
                    yield return null;
                }
            }
            _toastRoot.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void SetRootVisible(bool v)
        {
            if (_rootPanel != null) _rootPanel.SetActive(v);
        }
    }
}
