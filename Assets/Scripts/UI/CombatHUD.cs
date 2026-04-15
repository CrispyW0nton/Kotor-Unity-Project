using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Combat;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  COMBATANT HUD ROW  — one row in the initiative display
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>A single row in the combat initiative list.</summary>
    public class CombatantRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Slider          _hpBar;
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private Image           _highlight;   // background tint
        [SerializeField] private Color           _activeColor  = new Color(1f, 0.85f, 0.2f, 0.4f);
        [SerializeField] private Color           _normalColor  = new Color(0f, 0f, 0f, 0.2f);

        public void Bind(Combatant combatant, bool isActive)
        {
            if (combatant == null) return;

            if (_nameText != null) _nameText.text = combatant.Name;

            float hp    = combatant.CurrentHP;
            float maxHp = combatant.MaxHP;
            if (_hpBar  != null) _hpBar.value = maxHp > 0f ? hp / maxHp : 0f;
            if (_hpText != null) _hpText.text = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";

            if (_highlight != null)
                _highlight.color = isActive ? _activeColor : _normalColor;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  COMBAT HUD MANAGER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CombatHUD — overlay panel shown during RTWP combat.
    ///
    /// Features:
    ///   • Initiative order list (left sidebar).
    ///   • Active combatant highlight.
    ///   • Round timer bar (3-second rounds per design doc).
    ///   • RTWP Pause button and status indicator.
    ///   • Pause-to-select mode: click an enemy to queue an action.
    ///   • Hit-chance display for the hovered target.
    ///   • Player Force Points bar (shown when player is Force-sensitive).
    ///   • Action queue list (up to 3 pending actions).
    ///
    /// This panel is toggled ON when combat starts and OFF when combat ends.
    /// </summary>
    public class CombatHUD : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static CombatHUD Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root")]
        [SerializeField] private GameObject  _combatPanel;

        [Header("Round Timer")]
        [SerializeField] private Slider          _roundTimerBar;
        [SerializeField] private TextMeshProUGUI _roundTimerText;
        [SerializeField] private TextMeshProUGUI _roundNumberText;
        private const float ROUND_DURATION = 3f; // seconds per design doc

        [Header("Pause")]
        [SerializeField] private Button          _btnPause;
        [SerializeField] private GameObject      _pauseIndicator;
        [SerializeField] private TextMeshProUGUI _pauseStatusText;

        [Header("Initiative List")]
        [SerializeField] private Transform  _initiativeContainer;
        [SerializeField] private CombatantRow _rowPrefab;

        [Header("Hit Chance")]
        [SerializeField] private GameObject      _hitChancePanel;
        [SerializeField] private TextMeshProUGUI _hitChanceText;
        [SerializeField] private TextMeshProUGUI _targetNameText;

        [Header("Force Points")]
        [SerializeField] private GameObject      _fpPanel;
        [SerializeField] private Slider          _fpBar;
        [SerializeField] private TextMeshProUGUI _fpText;

        [Header("Action Queue")]
        [SerializeField] private Transform       _actionQueueContainer;
        [SerializeField] private TextMeshProUGUI _actionRowPrefabText;

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private CombatManager        _combatManager;
        private ForcePowerManager    _forcePowerManager;
        private bool                 _isPaused;
        private float                _roundElapsed;
        private int                  _roundNumber;
        private readonly List<CombatantRow> _rows = new List<CombatantRow>();

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (_combatPanel != null) _combatPanel.SetActive(false);
        }

        private void Start()
        {
            if (_btnPause != null)
                _btnPause.onClick.AddListener(TogglePause);

            // Subscribe to combat events
            EventBus.Subscribe(EventBus.EventType.CombatStarted,  OnCombatStarted);
            EventBus.Subscribe(EventBus.EventType.CombatEnded,    OnCombatEnded);
            EventBus.Subscribe(EventBus.EventType.GamePaused,     _ => SetPaused(true));
            EventBus.Subscribe(EventBus.EventType.GameResumed,    _ => SetPaused(false));

            // Find Force Power Manager on player
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                _forcePowerManager = player.GetComponent<ForcePowerManager>();

            RefreshFPPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            EventBus.Unsubscribe(EventBus.EventType.CombatStarted, OnCombatStarted);
            EventBus.Unsubscribe(EventBus.EventType.CombatEnded,   OnCombatEnded);
            EventBus.Unsubscribe(EventBus.EventType.GamePaused,    _ => SetPaused(true));
            EventBus.Unsubscribe(EventBus.EventType.GameResumed,   _ => SetPaused(false));
        }

        private void Update()
        {
            if (_combatPanel == null || !_combatPanel.activeSelf) return;

            // Round timer
            if (!_isPaused)
            {
                _roundElapsed += Time.deltaTime;
                if (_roundElapsed >= ROUND_DURATION)
                {
                    _roundElapsed -= ROUND_DURATION;
                    _roundNumber++;
                    if (_roundNumberText != null)
                        _roundNumberText.text = $"Round {_roundNumber}";
                    RefreshInitiativeList();
                }
            }

            if (_roundTimerBar  != null)
                _roundTimerBar.value = _roundElapsed / ROUND_DURATION;
            if (_roundTimerText != null)
                _roundTimerText.text = $"{(ROUND_DURATION - _roundElapsed):F1}s";

            // FP bar per-frame (regenerating)
            RefreshFPBar();

            // Keyboard shortcuts
            if (Input.GetKeyDown(KeyCode.Space))
                TogglePause();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  EVENT HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private void OnCombatStarted(EventBus.GameEventArgs args)
        {
            _combatManager = FindObjectOfType<CombatManager>();
            _roundNumber   = 1;
            _roundElapsed  = 0f;
            _isPaused      = false;

            if (_combatPanel    != null) _combatPanel.SetActive(true);
            if (_roundNumberText!= null) _roundNumberText.text = "Round 1";

            RefreshInitiativeList();
            RefreshFPPanel();
        }

        private void OnCombatEnded(EventBus.GameEventArgs args)
        {
            if (_combatPanel != null) _combatPanel.SetActive(false);
            _combatManager   = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PAUSE
        // ══════════════════════════════════════════════════════════════════════

        public void TogglePause() => SetPaused(!_isPaused);

        private void SetPaused(bool paused)
        {
            _isPaused = paused;
            Time.timeScale = paused ? 0f : 1f;

            if (_pauseIndicator != null) _pauseIndicator.SetActive(paused);
            if (_pauseStatusText != null)
                _pauseStatusText.text = paused ? "PAUSED" : "";

            EventBus.Publish(paused ? EventBus.EventType.GamePaused : EventBus.EventType.GameResumed);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  INITIATIVE LIST
        // ══════════════════════════════════════════════════════════════════════

        public void RefreshInitiativeList()
        {
            if (_initiativeContainer == null || _rowPrefab == null) return;

            // Destroy old rows
            foreach (var r in _rows) if (r != null) Destroy(r.gameObject);
            _rows.Clear();

            if (_combatManager == null) return;

            var combatants = _combatManager.GetCombatantsInOrder();
            int activeIdx  = _combatManager.ActiveCombatantIndex;

            for (int i = 0; i < combatants.Count; i++)
            {
                var row = Instantiate(_rowPrefab, _initiativeContainer);
                row.Bind(combatants[i], i == activeIdx);
                _rows.Add(row);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  HIT CHANCE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the hit chance panel for a hovered/selected target.
        /// Called by mouse-over logic in the action player controller.
        /// </summary>
        public void ShowHitChance(string targetName, int attackBonus, int targetAC)
        {
            if (_hitChancePanel  != null) _hitChancePanel.SetActive(true);
            if (_targetNameText  != null) _targetNameText.text = targetName;

            // KotOR d20: need d20 + attackBonus ≥ targetAC → P(hit) = (20 + attackBonus - targetAC + 1) / 20
            int minRoll = Mathf.Clamp(targetAC - attackBonus, 1, 20);
            float hitPct = (21 - minRoll) / 20f * 100f;

            if (_hitChanceText != null)
                _hitChanceText.text = $"Hit: {hitPct:F0}%  (need {minRoll}+)";
        }

        public void HideHitChance()
        {
            if (_hitChancePanel != null) _hitChancePanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FORCE POINTS
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshFPPanel()
        {
            bool hasFP = _forcePowerManager != null;
            if (_fpPanel != null) _fpPanel.SetActive(hasFP);
        }

        private void RefreshFPBar()
        {
            if (_forcePowerManager == null || _fpBar == null) return;
            float cur = _forcePowerManager.CurrentFP;
            float max = _forcePowerManager.MaxFP;
            _fpBar.value = max > 0f ? cur / max : 0f;
            if (_fpText != null) _fpText.text = $"FP: {Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ACTION QUEUE
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Update the visible action queue list (called by CombatManager).</summary>
        public void RefreshActionQueue(IList<string> actions)
        {
            if (_actionQueueContainer == null) return;
            foreach (Transform child in _actionQueueContainer) Destroy(child.gameObject);

            foreach (var action in actions)
            {
                if (_actionRowPrefabText == null) break;
                var row = Instantiate(_actionRowPrefabText, _actionQueueContainer);
                row.text = action;
            }
        }
    }
}
