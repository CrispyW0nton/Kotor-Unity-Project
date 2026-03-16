using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Player;
using KotORUnity.Combat;
using KotORUnity.Party;
using static KotORUnity.Core.GameEnums;
#pragma warning disable 0414, 0219

namespace KotORUnity.UI
{
    // ══════════════════════════════════════════════════════════════════════════
    //  ACTION SLOT  —  one quick-bar slot (ability / force power / item)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Data for one HUD action bar slot.</summary>
    [System.Serializable]
    public class ActionSlotData
    {
        public string       Label;
        public Sprite       Icon;
        public float        CooldownMax;
        public float        CooldownRemaining;
        public bool         IsAvailable  => CooldownRemaining <= 0f;
    }

    /// <summary>
    /// UI component for a single action-bar slot.
    /// Attach to the slot's root GameObject.
    /// </summary>
    public class ActionSlotUI : MonoBehaviour
    {
        [SerializeField] private Image            _iconImage;
        [SerializeField] private Image            _cooldownOverlay;   // radial fill
        [SerializeField] private TextMeshProUGUI  _hotkeyLabel;
        [SerializeField] private TextMeshProUGUI  _cooldownText;
        [SerializeField] private GameObject       _unavailableShade;
        [SerializeField] private Button           _button;
        [SerializeField] private Color            _readyTint    = Color.white;
        [SerializeField] private Color            _cooldownTint = new Color(0.5f, 0.5f, 0.5f, 0.8f);

        public int SlotIndex { get; set; }

        public void Bind(ActionSlotData data, int index, System.Action<int> onClick)
        {
            SlotIndex = index;

            if (_hotkeyLabel  != null) _hotkeyLabel.text   = index < 9 ? $"{index + 1}" : "";
            if (_iconImage    != null)
            {
                _iconImage.sprite  = data?.Icon;
                _iconImage.enabled = data?.Icon != null;
                _iconImage.color   = data != null && data.IsAvailable ? _readyTint : _cooldownTint;
            }

            float pct = data != null && data.CooldownMax > 0f
                ? data.CooldownRemaining / data.CooldownMax : 0f;

            if (_cooldownOverlay != null)
            {
                _cooldownOverlay.fillAmount = pct;
                _cooldownOverlay.enabled    = pct > 0f;
            }

            if (_cooldownText != null)
                _cooldownText.text = pct > 0f
                    ? $"{data.CooldownRemaining:F1}"
                    : "";

            if (_unavailableShade != null)
                _unavailableShade.SetActive(data != null && !data.IsAvailable);

            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(() => onClick?.Invoke(SlotIndex));
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  COMPANION BAR  —  small HP bar for each party member
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Small companion health bar shown in the top-right party panel.</summary>
    public class CompanionBarUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private Slider          _hpBar;
        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private Image           _portraitImage;
        [SerializeField] private GameObject      _downedIndicator;

        public void Bind(string name, float hp, float maxHp, Sprite portrait = null)
        {
            if (_nameText     != null) _nameText.text    = name;
            if (_hpText       != null) _hpText.text      = $"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}";
            if (_hpBar        != null) _hpBar.value      = maxHp > 0f ? hp / maxHp : 0f;
            if (_portraitImage!= null && portrait != null) _portraitImage.sprite = portrait;
            if (_downedIndicator != null) _downedIndicator.SetActive(hp <= 0f);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  HUD MANAGER
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Central HUD manager for KotOR-Unity.
    ///
    /// Wires to EventBus and PlayerStats for:
    ///   • Health / Shield / Stamina bars
    ///   • Force Points bar + alignment indicator
    ///   • XP / Level display
    ///   • Ammo counter
    ///   • Mode switch cooldown
    ///   • 9-slot action bar (abilities, force powers, items)
    ///   • Companion HP bars (party panel)
    ///   • Pause indicator
    ///   • Damage/XP floating numbers
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static HUDManager Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────

        [Header("─── Player Stats ───")]
        [SerializeField] private Slider           healthBar;
        [SerializeField] private Slider           shieldBar;
        [SerializeField] private Slider           staminaBar;
        [SerializeField] private TextMeshProUGUI  healthText;
        [SerializeField] private TextMeshProUGUI  shieldText;

        [Header("Force Points")]
        [SerializeField] private Slider           forceBar;
        [SerializeField] private TextMeshProUGUI  forceText;
        [SerializeField] private GameObject       forceBarRoot;   // hide if no Force user

        [Header("Alignment")]
        [SerializeField] private Slider           alignmentBar;   // 0=darkside, 1=lightside
        [SerializeField] private Image            alignmentIcon;
        [SerializeField] private Sprite           lightsideIcon;
        [SerializeField] private Sprite           darksideIcon;

        [Header("XP / Level")]
        [SerializeField] private Slider           xpBar;
        [SerializeField] private TextMeshProUGUI  levelText;
        [SerializeField] private TextMeshProUGUI  xpText;

        [Header("─── Mode ───")]
        [SerializeField] private TextMeshProUGUI  modeLabel;
        [SerializeField] private Image            modeIndicatorBg;
        [SerializeField] private Color            actionModeColor  = new Color(0.9f, 0.4f, 0.1f);
        [SerializeField] private Color            rtsModeColor     = new Color(0.1f, 0.6f, 1.0f);
        [SerializeField] private Slider           modeSwitchCooldownBar;
        [SerializeField] private TextMeshProUGUI  modeSwitchCooldownText;

        [Header("─── Action Bar ───")]
        [SerializeField] private ActionSlotUI[]   actionSlots;        // 9 slots
        [SerializeField] private GameObject       actionBarRoot;

        [Header("─── Ammo ───")]
        [SerializeField] private TextMeshProUGUI  ammoText;
        [SerializeField] private Image            weaponIcon;

        [Header("─── Party Panel ───")]
        [SerializeField] private Transform        partyPanelRoot;
        [SerializeField] private CompanionBarUI   companionBarPrefab;

        [Header("─── Crosshair ───")]
        [SerializeField] private GameObject       crosshair;
        [SerializeField] private Image            crosshairDot;

        [Header("─── Pause ───")]
        [SerializeField] private GameObject       pausePanel;
        [SerializeField] private TextMeshProUGUI  pauseLabel;

        [Header("─── Floating Text ───")]
        [SerializeField] private GameObject       floatingTextPrefab;
        [SerializeField] private Canvas           hudCanvas;

        [Header("─── Minimap ───")]
        [SerializeField] private RawImage         minimapImage;
        [SerializeField] private UnityEngine.Camera minimapCamera;

        [Header("─── Interact Prompt ───")]
        [SerializeField] private World.InteractionPromptUI interactPrompt;

        // ── RUNTIME STATE ─────────────────────────────────────────────────────
        private PlayerStats           _playerStats;
        private StaminaSystem         _staminaSystem;
        private ModeSwitchSystem      _modeSwitchSystem;
        private ForcePowerManager     _forcePowerManager;

        private readonly List<ActionSlotData>  _actionSlotData  = new List<ActionSlotData>();
        private readonly List<CompanionBarUI>  _companionBars   = new List<CompanionBarUI>();

        // Floating text pool
        private readonly Queue<FloatingTextInstance> _floatPool = new Queue<FloatingTextInstance>();

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            CachePlayerComponents();
            SubscribeEvents();
            BuildDefaultActionSlots();
            RefreshAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeEvents();

            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged -= OnHealthChanged;
                _playerStats.OnShieldChanged -= OnShieldChanged;
                _playerStats.OnLevelUp       -= OnLevelUp;
            }
            if (_staminaSystem != null)
                _staminaSystem.OnStaminaChanged -= OnStaminaChanged;
        }

        private void Update()
        {
            RefreshModeSwitchCooldown();
            RefreshForceBar();
            TickActionSlotCooldowns();
        }

        // ── SETUP ─────────────────────────────────────────────────────────────

        private void CachePlayerComponents()
        {
            var player = GameObject.FindWithTag("Player");
            if (player == null) return;

            _playerStats       = player.GetComponent<PlayerStatsBehaviour>()?.Stats;
            _staminaSystem     = player.GetComponent<StaminaSystem>();
            _forcePowerManager = player.GetComponent<ForcePowerManager>();
            _modeSwitchSystem  = FindObjectOfType<ModeSwitchSystem>();

            if (_playerStats != null)
            {
                _playerStats.OnHealthChanged += OnHealthChanged;
                _playerStats.OnShieldChanged += OnShieldChanged;
                _playerStats.OnLevelUp       += OnLevelUp;
            }
            if (_staminaSystem != null)
                _staminaSystem.OnStaminaChanged += OnStaminaChanged;

            // Show or hide force bar
            bool isForceSensitive = _forcePowerManager != null;
            if (forceBarRoot != null) forceBarRoot.SetActive(isForceSensitive);

            // Alignment bar
            if (alignmentBar != null && _forcePowerManager != null)
            {
                float align = (_forcePowerManager.Alignment + 100f) / 200f; // -100..100 → 0..1
                alignmentBar.value = align;
            }
        }

        private void BuildDefaultActionSlots()
        {
            _actionSlotData.Clear();
            for (int i = 0; i < 9; i++)
                _actionSlotData.Add(new ActionSlotData { Label = $"Slot {i + 1}" });

            RefreshActionBar();
        }

        // ── EVENT SUBSCRIPTION ────────────────────────────────────────────────

        private void SubscribeEvents()
        {
            EventBus.Subscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
            EventBus.Subscribe(EventBus.EventType.GamePaused,              OnGamePaused);
            EventBus.Subscribe(EventBus.EventType.GameResumed,             OnGameResumed);
            EventBus.Subscribe(EventBus.EventType.UIHUDRefresh,            OnHUDRefresh);
            EventBus.Subscribe(EventBus.EventType.ExperienceGained,        OnXPGained);
            EventBus.Subscribe(EventBus.EventType.EntityDamaged,           OnEntityDamaged);
            EventBus.Subscribe(EventBus.EventType.AlignmentChanged,        OnAlignmentChanged);
            EventBus.Subscribe(EventBus.EventType.CombatStarted,           OnCombatStarted);
            EventBus.Subscribe(EventBus.EventType.CombatEnded,             OnCombatEnded);
        }

        private void UnsubscribeEvents()
        {
            EventBus.Unsubscribe(EventBus.EventType.ModeTransitionCompleted, OnModeChanged);
            EventBus.Unsubscribe(EventBus.EventType.GamePaused,              OnGamePaused);
            EventBus.Unsubscribe(EventBus.EventType.GameResumed,             OnGameResumed);
            EventBus.Unsubscribe(EventBus.EventType.UIHUDRefresh,            OnHUDRefresh);
            EventBus.Unsubscribe(EventBus.EventType.ExperienceGained,        OnXPGained);
            EventBus.Unsubscribe(EventBus.EventType.EntityDamaged,           OnEntityDamaged);
            EventBus.Unsubscribe(EventBus.EventType.AlignmentChanged,        OnAlignmentChanged);
            EventBus.Unsubscribe(EventBus.EventType.CombatStarted,           OnCombatStarted);
            EventBus.Unsubscribe(EventBus.EventType.CombatEnded,             OnCombatEnded);
        }

        // ── FULL REFRESH ──────────────────────────────────────────────────────

        public void RefreshAll()
        {
            if (_playerStats != null)
            {
                RefreshHealthBar(_playerStats.CurrentHealth, _playerStats.MaxHealth);
                RefreshShieldBar(_playerStats.CurrentShield, _playerStats.MaxShield);
                RefreshLevel(_playerStats.Level);
                RefreshXP(_playerStats.Experience, _playerStats.XpToNextLevel);
            }
            if (_staminaSystem != null)
                RefreshStaminaBar(_staminaSystem.CurrentStamina, _staminaSystem.MaxStamina);

            RefreshMode(_modeSwitchSystem?.CurrentMode ?? GameMode.Action);
            RefreshForceBar();
            RefreshPartyPanel();
            RefreshActionBar();
        }

        // ── HEALTH / SHIELD / STAMINA ─────────────────────────────────────────

        private void OnHealthChanged(float cur, float max) => RefreshHealthBar(cur, max);
        private void OnShieldChanged(float cur, float max) => RefreshShieldBar(cur, max);
        private void OnStaminaChanged(float cur, float max) => RefreshStaminaBar(cur, max);

        private void RefreshHealthBar(float current, float max)
        {
            if (healthBar  != null) healthBar.value  = max > 0f ? current / max : 0f;
            if (healthText != null) healthText.text  = $"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
        }

        private void RefreshShieldBar(float current, float max)
        {
            if (shieldBar  != null) shieldBar.value  = max > 0f ? current / max : 0f;
            if (shieldText != null) shieldText.text  = $"{Mathf.CeilToInt(current)}";
        }

        private void RefreshStaminaBar(float current, float max)
        {
            if (staminaBar != null) staminaBar.value = max > 0f ? current / max : 0f;
        }

        // ── FORCE / ALIGNMENT ─────────────────────────────────────────────────

        private void RefreshForceBar()
        {
            if (_forcePowerManager == null) return;
            float cur = _forcePowerManager.CurrentFP;
            float max = _forcePowerManager.MaxFP;
            if (forceBar  != null) forceBar.value  = max > 0f ? cur / max : 0f;
            if (forceText != null) forceText.text  = $"FP {Mathf.CeilToInt(cur)}/{Mathf.CeilToInt(max)}";
        }

        private void OnAlignmentChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.AlignmentEventArgs ae)
            {
                // alignmentBar: 0 = dark side (-100), 1 = light side (+100)
                float normalised = (ae.NewAlignment + 100f) / 200f;
                if (alignmentBar  != null) alignmentBar.value = normalised;
                if (alignmentIcon != null)
                    alignmentIcon.sprite = normalised >= 0.5f ? lightsideIcon : darksideIcon;
            }
        }

        // ── MODE SWITCH ───────────────────────────────────────────────────────

        private void OnModeChanged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.ModeSwitchEventArgs e)
                RefreshMode(e.NewMode);
        }

        private void RefreshMode(GameMode mode)
        {
            if (modeLabel != null)
                modeLabel.text = mode == GameMode.Action ? "ACTION" : "TACTICAL";

            if (modeIndicatorBg != null)
                modeIndicatorBg.color = mode == GameMode.Action ? actionModeColor : rtsModeColor;

            if (crosshair != null)
                crosshair.SetActive(mode == GameMode.Action);
        }

        private void RefreshModeSwitchCooldown()
        {
            if (_modeSwitchSystem == null) return;
            float cooldown   = GameConstants.MODE_SWITCH_COOLDOWN;
            float remaining  = _modeSwitchSystem.SwitchCooldownRemaining;
            float ratio      = 1f - Mathf.Clamp01(remaining / Mathf.Max(cooldown, 0.001f));

            if (modeSwitchCooldownBar  != null) modeSwitchCooldownBar.value  = ratio;
            if (modeSwitchCooldownText != null)
                modeSwitchCooldownText.text = remaining > 0.05f
                    ? $"{remaining:F1}s" : "READY";
        }

        // ── XP / LEVEL ────────────────────────────────────────────────────────

        private void OnLevelUp(int level) => RefreshLevel(level);

        private void RefreshLevel(int level)
        {
            if (levelText != null) levelText.text = $"Lv.{level}";
        }

        private void RefreshXP(float xp, float xpToNext)
        {
            if (xpBar  != null) xpBar.value  = xpToNext > 0f ? xp / xpToNext : 0f;
            if (xpText != null) xpText.text  = $"{Mathf.FloorToInt(xp)}/{Mathf.FloorToInt(xpToNext)} XP";
        }

        // ── ACTION BAR ────────────────────────────────────────────────────────

        public void RefreshActionBar()
        {
            if (actionSlots == null) return;
            for (int i = 0; i < actionSlots.Length; i++)
            {
                if (actionSlots[i] == null) continue;
                var data = i < _actionSlotData.Count ? _actionSlotData[i] : null;
                actionSlots[i].Bind(data, i, OnActionSlotClicked);
            }
        }

        public void SetActionSlot(int index, ActionSlotData data)
        {
            while (_actionSlotData.Count <= index)
                _actionSlotData.Add(new ActionSlotData());
            _actionSlotData[index] = data;
            if (actionSlots != null && index < actionSlots.Length && actionSlots[index] != null)
                actionSlots[index].Bind(data, index, OnActionSlotClicked);
        }

        private void OnActionSlotClicked(int index)
        {
            // Delegate to player controller
            var player = GameObject.FindWithTag("Player");
            if (player != null)
                player.GetComponent<ActionPlayerController>()?.UseAbility(index);
        }

        private void TickActionSlotCooldowns()
        {
            bool changed = false;
            foreach (var slot in _actionSlotData)
            {
                if (slot == null) continue;
                if (slot.CooldownRemaining > 0f)
                {
                    slot.CooldownRemaining -= Time.deltaTime;
                    if (slot.CooldownRemaining < 0f) slot.CooldownRemaining = 0f;
                    changed = true;
                }
            }
            if (changed) RefreshActionBar();
        }

        public void TriggerAbilityCooldown(int slotIndex, float cooldownDuration)
        {
            if (slotIndex < 0 || slotIndex >= _actionSlotData.Count) return;
            _actionSlotData[slotIndex].CooldownMax       = cooldownDuration;
            _actionSlotData[slotIndex].CooldownRemaining = cooldownDuration;
            RefreshActionBar();
        }

        // ── AMMO ──────────────────────────────────────────────────────────────

        public void SetAmmoDisplay(int current, int max, Sprite weaponSprite = null)
        {
            if (ammoText != null)
                ammoText.text = max < 0 ? "∞" : $"{current}/{max}";
            if (weaponIcon != null && weaponSprite != null)
            {
                weaponIcon.sprite  = weaponSprite;
                weaponIcon.enabled = true;
            }
        }

        // ── PARTY PANEL ───────────────────────────────────────────────────────

        public void RefreshPartyPanel()
        {
            if (partyPanelRoot == null) return;

            // Destroy old bars
            foreach (var bar in _companionBars)
                if (bar != null) Destroy(bar.gameObject);
            _companionBars.Clear();

            var pm = PartyManager.Instance;
            if (pm == null) return;

            foreach (var member in pm.ActiveMembers)
            {
                if (member == null) continue;
                var bar = companionBarPrefab != null
                    ? Instantiate(companionBarPrefab, partyPanelRoot)
                    : CreateFallbackCompanionBar();

                bar.Bind(member.DisplayName, member.CurrentHP, member.MaxHP);
                _companionBars.Add(bar);
            }
        }

        private CompanionBarUI CreateFallbackCompanionBar()
        {
            var go  = new GameObject("CompanionBar");
            go.transform.SetParent(partyPanelRoot, false);
            return go.AddComponent<CompanionBarUI>();
        }

        // ── PAUSE ─────────────────────────────────────────────────────────────

        private void OnGamePaused(EventBus.GameEventArgs _)
        {
            if (pausePanel != null) pausePanel.SetActive(true);
            if (pauseLabel != null) pauseLabel.text = "PAUSED";
        }

        private void OnGameResumed(EventBus.GameEventArgs _)
        {
            if (pausePanel != null) pausePanel.SetActive(false);
        }

        // ── COMBAT START/END ──────────────────────────────────────────────────

        private void OnCombatStarted(EventBus.GameEventArgs _)
        {
            // Show action bar in RTS/tactical mode
            if (actionBarRoot != null) actionBarRoot.SetActive(true);
        }

        private void OnCombatEnded(EventBus.GameEventArgs _)
        {
            // nothing special needed here
        }

        // ── DAMAGE / XP FLOATERS ──────────────────────────────────────────────

        private void OnEntityDamaged(EventBus.GameEventArgs args)
        {
            if (args is EventBus.DamageEventArgs de && de.Target != null)
            {
                bool isCrit = de.HitType == HitType.Critical;
                Color col = isCrit
                    ? new Color(1f, 0.4f, 0f)
                    : de.Type == DamageType.Force
                        ? new Color(0.4f, 0.8f, 1f)
                        : Color.white;

                string text = isCrit ? $"CRIT!\n-{(int)de.Amount}" : $"-{(int)de.Amount}";
                SpawnFloatingText(text, de.Target.transform.position, col);
            }
        }

        private void OnXPGained(EventBus.GameEventArgs args)
        {
            if (_playerStats != null)
                RefreshXP(_playerStats.Experience, _playerStats.XpToNextLevel);

            if (args is EventBus.ExperienceEventArgs xpE)
            {
                var player = GameObject.FindWithTag("Player");
                if (player != null)
                    SpawnFloatingText($"+{xpE.Amount} XP", player.transform.position,
                        new Color(0.8f, 1f, 0.4f));
            }
        }

        private void OnHUDRefresh(EventBus.GameEventArgs _) => RefreshAll();

        // ── FLOATING TEXT ─────────────────────────────────────────────────────

        public void SpawnFloatingText(string text, Vector3 worldPos, Color color)
        {
            if (hudCanvas == null) return;

            FloatingTextInstance inst;
            if (_floatPool.Count > 0 && !_floatPool.Peek().Active)
                inst = _floatPool.Dequeue();
            else
                inst = CreateFloatingText();

            if (inst == null) return;

            inst.Show(text, worldPos, color, hudCanvas);
            StartCoroutine(RecycleAfter(inst, 1.8f));
        }

        private IEnumerator RecycleAfter(FloatingTextInstance inst, float delay)
        {
            yield return new WaitForSeconds(delay);
            inst.Hide();
            _floatPool.Enqueue(inst);
        }

        private FloatingTextInstance CreateFloatingText()
        {
            GameObject go;
            if (floatingTextPrefab != null)
                go = Instantiate(floatingTextPrefab);
            else
            {
                go = new GameObject("FloatTxt");
                go.AddComponent<RectTransform>().sizeDelta = new Vector2(120, 40);
                var tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.fontSize  = 18;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color     = Color.white;
            }

            go.transform.SetParent(hudCanvas.transform, false);
            go.SetActive(false);
            return new FloatingTextInstance(go);
        }

        // ── MISC PUBLIC API ───────────────────────────────────────────────────

        public void SetHitChanceDisplay(string text)
        {
            // Pass through to CombatHUD if open
            CombatHUD.Instance?.ShowHitChance("Target", 0, 10);
        }

        public void SetAbilityCooldown(int slot, float progress)
        {
            if (actionSlots != null && slot < actionSlots.Length && actionSlots[slot] != null)
            {
                if (slot < _actionSlotData.Count)
                    _actionSlotData[slot].CooldownRemaining =
                        (1f - Mathf.Clamp01(progress)) * _actionSlotData[slot].CooldownMax;
            }
        }

        public void ShowInteractPrompt(string name, string verb)
            => interactPrompt?.Show(name, verb);

        public void HideInteractPrompt()
            => interactPrompt?.Hide();

        // ── FLOATING TEXT INSTANCE ────────────────────────────────────────────

        private class FloatingTextInstance
        {
            private readonly GameObject      _go;
            private readonly TextMeshProUGUI _tmp;
            private readonly RectTransform   _rt;

            public bool Active => _go != null && _go.activeSelf;

            public FloatingTextInstance(GameObject go)
            {
                _go  = go;
                _tmp = go.GetComponentInChildren<TextMeshProUGUI>();
                _rt  = go.GetComponent<RectTransform>();
            }

            public void Show(string text, Vector3 worldPos, Color color, Canvas canvas)
            {
                if (_go == null) return;

                if (_tmp != null) { _tmp.text = text; _tmp.color = color; }
                _go.SetActive(true);

                // Convert world position to canvas local position
                if (_rt != null && canvas != null)
                {
                    Vector2 screenPos = UnityEngine.Camera.main != null
                        ? (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(worldPos)
                        : (Vector2)worldPos;

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvas.GetComponent<RectTransform>(),
                        screenPos, canvas.worldCamera, out Vector2 localPos);

                    _rt.anchoredPosition = localPos;
                }
            }

            public void Hide()
            {
                if (_go != null) _go.SetActive(false);
            }
        }
    }
}
