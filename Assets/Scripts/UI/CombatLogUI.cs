using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Combat;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  COMBAT LOG UI  —  KotOR-style scrollable combat feedback panel
    //
    //  Features:
    //    • Scrollable list of recent combat events (attack rolls, damage, saves,
    //      ability activations, status effects, XP awards)
    //    • Color-coded lines: hits=white, crits=gold, misses=grey, damage=red,
    //      heals=green, XP=cyan, force=purple, system=blue
    //    • Timestamp per entry (round number or real time)
    //    • Filter buttons: All / Combat / Force / XP / System
    //    • Auto-scroll to newest entry; manual scroll pauses auto-scroll
    //    • Maximum 200 entries kept (older entries purged)
    //    • Fade-in new entries
    //    • "Copy to Clipboard" button
    //    • EventBus subscriptions: EntityDamaged, AbilityUsed, ExperienceGained,
    //      CombatStarted, CombatEnded, ForcePowerActivated, PlayerDied
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Combat log panel controller.  Place on a child panel of the HUD Canvas.
    /// </summary>
    public class CombatLogUI : MonoBehaviour
    {
        // ── INSPECTOR ─────────────────────────────────────────────────────────

        [Header("Layout")]
        [SerializeField] private GameObject      _panel;
        [SerializeField] private ScrollRect      _scrollRect;
        [SerializeField] private Transform       _entryContainer;     // parent for log entries
        [SerializeField] private GameObject      _entryPrefab;        // LogEntryUI prefab

        [Header("Controls")]
        [SerializeField] private Button          _clearButton;
        [SerializeField] private Button          _copyButton;
        [SerializeField] private Button          _toggleButton;
        [SerializeField] private TextMeshProUGUI _toggleButtonText;

        [Header("Filter Buttons")]
        [SerializeField] private Button          _filterAll;
        [SerializeField] private Button          _filterCombat;
        [SerializeField] private Button          _filterForce;
        [SerializeField] private Button          _filterXP;
        [SerializeField] private Button          _filterSystem;

        [Header("Config")]
        [SerializeField] private int             _maxEntries      = 200;
        [SerializeField] private bool            _autoScroll      = true;
        [SerializeField] private float           _fadeInDuration  = 0.3f;
        [SerializeField] private bool            _showTimestamps  = false;
        [SerializeField] private bool            _startCollapsed  = false;

        // ── COLORS ────────────────────────────────────────────────────────────
        // (Inspector-overridable via public color fields)
        [Header("Entry Colors")]
        [SerializeField] private Color _colorHit      = Color.white;
        [SerializeField] private Color _colorCrit     = new Color(1f, 0.85f, 0f);   // gold
        [SerializeField] private Color _colorMiss     = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color _colorDamage   = new Color(1f, 0.3f, 0.3f);  // red
        [SerializeField] private Color _colorHeal     = new Color(0.3f, 1f, 0.4f);  // green
        [SerializeField] private Color _colorXP       = new Color(0.3f, 0.9f, 1f);  // cyan
        [SerializeField] private Color _colorForce    = new Color(0.7f, 0.4f, 1f);  // purple
        [SerializeField] private Color _colorSystem   = new Color(0.6f, 0.8f, 1f);  // blue

        // ── RUNTIME ───────────────────────────────────────────────────────────

        public enum LogCategory { All, Combat, Force, XP, System }
        public enum LogEntryType
        {
            Hit, CriticalHit, Miss, Damage, Heal,
            Save, SaveFailed, Force, XPGain,
            CombatStart, CombatEnd, Death, System
        }

        private readonly List<LogEntry>    _allEntries      = new List<LogEntry>();
        private readonly List<LogEntryUI>  _visibleEntryUIs = new List<LogEntryUI>();
        private LogCategory                _activeFilter    = LogCategory.All;
        private bool                       _isCollapsed     = false;
        private bool                       _userScrolled    = false;
        private int                        _roundNumber     = 0;

        // singleton
        public static CombatLogUI Instance { get; private set; }

        // ── DATA STRUCTURE ────────────────────────────────────────────────────

        private class LogEntry
        {
            public string      Text;
            public LogCategory Category;
            public LogEntryType Type;
            public float       Timestamp;
            public int         Round;
        }

        // ── UNITY ────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _clearButton?.onClick.AddListener(ClearLog);
            _copyButton?.onClick.AddListener(CopyToClipboard);
            _toggleButton?.onClick.AddListener(ToggleCollapse);

            _filterAll?.onClick.AddListener(() => SetFilter(LogCategory.All));
            _filterCombat?.onClick.AddListener(() => SetFilter(LogCategory.Combat));
            _filterForce?.onClick.AddListener(() => SetFilter(LogCategory.Force));
            _filterXP?.onClick.AddListener(() => SetFilter(LogCategory.XP));
            _filterSystem?.onClick.AddListener(() => SetFilter(LogCategory.System));

            if (_scrollRect != null)
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);

            if (_startCollapsed) Collapse();
        }

        private void Start()
        {
            // EventBus subscriptions
            EventBus.Subscribe(EventBus.EventType.EntityDamaged,      OnEntityDamaged);
            EventBus.Subscribe(EventBus.EventType.AbilityUsed,        OnAbilityUsed);
            EventBus.Subscribe(EventBus.EventType.ExperienceGained,   OnXPGained);
            EventBus.Subscribe(EventBus.EventType.CombatStarted,      OnCombatStarted);
            EventBus.Subscribe(EventBus.EventType.CombatEnded,        OnCombatEnded);
            EventBus.Subscribe(EventBus.EventType.CombatRoundStarted, OnRoundStarted);
            EventBus.Subscribe(EventBus.EventType.ForcePowerActivated, OnForcePower);
            EventBus.Subscribe(EventBus.EventType.PlayerDied,         OnPlayerDied);
            EventBus.Subscribe(EventBus.EventType.EntityKilled,       OnEntityKilled);

            AddSystemEntry("Combat log initialized.");
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe(EventBus.EventType.EntityDamaged,      OnEntityDamaged);
            EventBus.Unsubscribe(EventBus.EventType.AbilityUsed,        OnAbilityUsed);
            EventBus.Unsubscribe(EventBus.EventType.ExperienceGained,   OnXPGained);
            EventBus.Unsubscribe(EventBus.EventType.CombatStarted,      OnCombatStarted);
            EventBus.Unsubscribe(EventBus.EventType.CombatEnded,        OnCombatEnded);
            EventBus.Unsubscribe(EventBus.EventType.CombatRoundStarted, OnRoundStarted);
            EventBus.Unsubscribe(EventBus.EventType.ForcePowerActivated, OnForcePower);
            EventBus.Unsubscribe(EventBus.EventType.PlayerDied,         OnPlayerDied);
            EventBus.Unsubscribe(EventBus.EventType.EntityKilled,       OnEntityKilled);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Add a combat hit/miss entry.</summary>
        public void LogAttack(string attacker, string target, int roll, int ac,
                              bool hit, bool crit, int damage)
        {
            string text;
            LogEntryType type;
            Color color;

            if (crit)
            {
                text  = $"<b>CRITICAL HIT!</b>  {attacker} → {target}  Roll:{roll}  DMG:{damage}";
                type  = LogEntryType.CriticalHit;
                color = _colorCrit;
            }
            else if (hit)
            {
                text  = $"{attacker} hits {target}  [Roll:{roll} vs AC:{ac}]  DMG:{damage}";
                type  = LogEntryType.Hit;
                color = _colorHit;
            }
            else
            {
                text  = $"{attacker} misses {target}  [Roll:{roll} vs AC:{ac}]";
                type  = LogEntryType.Miss;
                color = _colorMiss;
            }

            AddEntry(text, LogCategory.Combat, type, color);
        }

        /// <summary>Add a saving throw entry.</summary>
        public void LogSave(string entity, string saveType, int roll, int dc, bool passed)
        {
            string text = passed
                ? $"{entity} passes {saveType} save  [Roll:{roll} vs DC:{dc}]"
                : $"{entity} FAILS {saveType} save  [Roll:{roll} vs DC:{dc}]";
            var type  = passed ? LogEntryType.Save : LogEntryType.SaveFailed;
            var color = passed ? _colorHit : _colorDamage;
            AddEntry(text, LogCategory.Combat, type, color);
        }

        /// <summary>Add a force power usage entry.</summary>
        public void LogForcePower(string caster, string powerName, string target = null)
        {
            string text = string.IsNullOrEmpty(target)
                ? $"{caster} uses Force Power: {powerName}"
                : $"{caster} uses {powerName} on {target}";
            AddEntry(text, LogCategory.Force, LogEntryType.Force, _colorForce);
        }

        /// <summary>Add an XP award entry.</summary>
        public void LogXP(float amount, string source)
        {
            string text = $"+{amount:N0} XP  ({source})";
            AddEntry(text, LogCategory.XP, LogEntryType.XPGain, _colorXP);
        }

        // ── EVENT HANDLERS ────────────────────────────────────────────────────

        private void OnEntityDamaged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.DamageEventArgs dmg)
            {
                string src = dmg.Source?.name ?? "Unknown";
                string tgt = dmg.Target?.name ?? "Unknown";
                bool crit  = dmg.HitType == HitType.Critical;
                bool hit   = dmg.HitType != HitType.Miss;

                if (hit)
                {
                    string damageText = crit
                        ? $"<b>CRITICAL!</b>  {src} → {tgt}  DMG:{dmg.Amount:N0}  [{dmg.Type}]"
                        : $"{src} → {tgt}  DMG:{dmg.Amount:N0}  [{dmg.Type}]";
                    AddEntry(damageText, LogCategory.Combat, crit ? LogEntryType.CriticalHit : LogEntryType.Hit,
                             crit ? _colorCrit : _colorDamage);
                }
                else
                {
                    AddEntry($"{src} misses {tgt}", LogCategory.Combat, LogEntryType.Miss, _colorMiss);
                }
            }
            else if (args != null)
            {
                AddEntry($"Damage: {args.FloatValue:N0}", LogCategory.Combat, LogEntryType.Damage, _colorDamage);
            }
        }

        private void OnAbilityUsed(EventBus.GameEventArgs args)
        {
            if (args is EventBus.AbilityEventArgs ab)
            {
                string txt = $"{ab.Caster?.name ?? "Player"} used {ab.AbilityName}";
                if (ab.Target != null) txt += $" on {ab.Target.name}";
                AddEntry(txt, LogCategory.Combat, LogEntryType.Hit, _colorHit);
            }
        }

        private void OnXPGained(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ExperienceEventArgs xp)
                LogXP(xp.Amount, xp.Source);
        }

        private void OnCombatStarted(EventBus.GameEventArgs args)
        {
            _roundNumber = 0;
            AddEntry("— Combat Started —", LogCategory.System, LogEntryType.CombatStart, _colorSystem);
        }

        private void OnCombatEnded(EventBus.GameEventArgs args)
        {
            AddEntry("— Combat Ended —", LogCategory.System, LogEntryType.CombatEnd, _colorSystem);
        }

        private void OnRoundStarted(EventBus.GameEventArgs args)
        {
            _roundNumber++;
            AddEntry($"Round {_roundNumber}", LogCategory.System, LogEntryType.System, _colorSystem);
        }

        private void OnForcePower(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ForcePowerEventArgs fp)
                LogForcePower("Player", fp.PowerName);
        }

        private void OnPlayerDied(EventBus.GameEventArgs args)
        {
            AddEntry("<b>YOU HAVE FALLEN.</b>", LogCategory.System, LogEntryType.Death, _colorDamage);
        }

        private void OnEntityKilled(EventBus.GameEventArgs args)
        {
            if (args != null && !string.IsNullOrEmpty(args.StringValue))
                AddEntry($"{args.StringValue} defeated.", LogCategory.Combat, LogEntryType.Death, _colorMiss);
        }

        // ── ENTRY MANAGEMENT ─────────────────────────────────────────────────

        private void AddEntry(string text, LogCategory category, LogEntryType type, Color color)
        {
            var entry = new LogEntry
            {
                Text      = text,
                Category  = category,
                Type      = type,
                Timestamp = Time.time,
                Round     = _roundNumber,
            };

            _allEntries.Add(entry);

            // Prune old entries
            while (_allEntries.Count > _maxEntries)
            {
                _allEntries.RemoveAt(0);
                // Also remove the corresponding UI entry if visible
                if (_visibleEntryUIs.Count > 0)
                {
                    Destroy(_visibleEntryUIs[0].gameObject);
                    _visibleEntryUIs.RemoveAt(0);
                }
            }

            // Only show if passes filter
            if (PassesFilter(entry))
                SpawnEntryUI(entry, color);
        }

        private void AddSystemEntry(string text)
            => AddEntry(text, LogCategory.System, LogEntryType.System, _colorSystem);

        private void SpawnEntryUI(LogEntry entry, Color color)
        {
            if (_entryContainer == null) return;

            GameObject go;
            if (_entryPrefab != null)
            {
                go = Instantiate(_entryPrefab, _entryContainer);
            }
            else
            {
                go = new GameObject("LogEntry");
                go.transform.SetParent(_entryContainer, false);
                var cg = go.AddComponent<CanvasGroup>();
                var layout = go.AddComponent<LayoutElement>();
                layout.preferredHeight = 20f;
                var txt = go.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 12;
                txt.color    = color;
            }

            var ui = go.GetComponent<LogEntryUI>() ?? go.AddComponent<LogEntryUI>();
            string displayText = _showTimestamps
                ? $"[R{entry.Round}] {entry.Text}"
                : entry.Text;
            ui.Init(displayText, color);
            _visibleEntryUIs.Add(ui);

            // Fade in
            if (_fadeInDuration > 0f) StartCoroutine(FadeIn(ui, _fadeInDuration));

            // Auto-scroll
            if (_autoScroll && !_userScrolled)
            {
                Canvas.ForceUpdateCanvases();
                if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private IEnumerator FadeIn(LogEntryUI ui, float duration)
        {
            if (ui == null) yield break;
            var cg = ui.GetComponent<CanvasGroup>();
            if (cg == null) yield break;

            cg.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            cg.alpha = 1f;
        }

        // ── FILTER ────────────────────────────────────────────────────────────

        private bool PassesFilter(LogEntry entry)
        {
            if (_activeFilter == LogCategory.All) return true;
            return entry.Category == _activeFilter;
        }

        private void SetFilter(LogCategory category)
        {
            _activeFilter = category;
            RebuildVisibleEntries();
        }

        private void RebuildVisibleEntries()
        {
            // Destroy all current UI entries
            foreach (var ui in _visibleEntryUIs)
                if (ui != null) Destroy(ui.gameObject);
            _visibleEntryUIs.Clear();

            // Re-spawn filtered entries
            foreach (var entry in _allEntries)
            {
                if (!PassesFilter(entry)) continue;
                Color color = GetColorForType(entry.Type);
                SpawnEntryUI(entry, color);
            }

            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
        }

        private Color GetColorForType(LogEntryType type) => type switch
        {
            LogEntryType.CriticalHit => _colorCrit,
            LogEntryType.Hit         => _colorHit,
            LogEntryType.Miss        => _colorMiss,
            LogEntryType.Damage      => _colorDamage,
            LogEntryType.Heal        => _colorHeal,
            LogEntryType.Save        => _colorHit,
            LogEntryType.SaveFailed  => _colorDamage,
            LogEntryType.Force       => _colorForce,
            LogEntryType.XPGain      => _colorXP,
            _                        => _colorSystem,
        };

        // ── COLLAPSE / EXPAND ─────────────────────────────────────────────────

        private void ToggleCollapse()
        {
            if (_isCollapsed) Expand(); else Collapse();
        }

        private void Collapse()
        {
            _isCollapsed = true;
            if (_scrollRect != null) _scrollRect.gameObject.SetActive(false);
            if (_toggleButtonText != null) _toggleButtonText.text = "▲";
        }

        private void Expand()
        {
            _isCollapsed = false;
            if (_scrollRect != null) _scrollRect.gameObject.SetActive(true);
            if (_toggleButtonText != null) _toggleButtonText.text = "▼";
        }

        // ── CLEAR / COPY ──────────────────────────────────────────────────────

        private void ClearLog()
        {
            _allEntries.Clear();
            foreach (var ui in _visibleEntryUIs)
                if (ui != null) Destroy(ui.gameObject);
            _visibleEntryUIs.Clear();
        }

        private void CopyToClipboard()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in _allEntries)
                sb.AppendLine($"[R{e.Round}] {e.Text}");
            GUIUtility.systemCopyBuffer = sb.ToString();
            AddSystemEntry("Log copied to clipboard.");
        }

        // ── SCROLL HANDLER ────────────────────────────────────────────────────

        private void OnScrollChanged(Vector2 v)
        {
            // If user scrolled up (not at bottom), pause auto-scroll
            _userScrolled = v.y > 0.02f;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  LOG ENTRY UI  —  A single line in the combat log
    // ══════════════════════════════════════════════════════════════════════════

    public class LogEntryUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        public void Init(string text, Color color)
        {
            if (_text == null) _text = GetComponentInChildren<TextMeshProUGUI>();
            if (_text == null)
            {
                // Auto-create text component
                _text = gameObject.AddComponent<TextMeshProUGUI>();
                _text.fontSize = 12;
            }
            _text.text  = text;
            _text.color = color;
        }
    }
}
