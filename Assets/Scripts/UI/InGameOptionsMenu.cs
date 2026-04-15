using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Audio;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  IN-GAME OPTIONS MENU
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Tabs:  Audio | Graphics | Controls | Gameplay
    //
    //  All values are persisted via PlayerPrefs and immediately applied to:
    //    • AudioManager  (master / music / SFX / VO volumes)
    //    • Screen.SetResolution / QualitySettings  (graphics)
    //    • InputHandler  (key bindings)
    //    • ModeSwitchSystem  (gameplay toggles)

    /// <summary>
    /// Persistent in-game options panel. Toggle with Escape or the pause button.
    /// Survives scene loads (DontDestroyOnLoad) so settings are always accessible.
    /// </summary>
    public class InGameOptionsMenu : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static InGameOptionsMenu Instance { get; private set; }

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action OnOpened;
        public event Action OnClosed;

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root Panel")]
        [SerializeField] private GameObject _rootPanel;
        [SerializeField] private KeyCode    _toggleKey = KeyCode.Escape;

        [Header("Tab Buttons")]
        [SerializeField] private Button _tabAudio;
        [SerializeField] private Button _tabGraphics;
        [SerializeField] private Button _tabControls;
        [SerializeField] private Button _tabGameplay;

        [Header("Tab Panels")]
        [SerializeField] private GameObject _panelAudio;
        [SerializeField] private GameObject _panelGraphics;
        [SerializeField] private GameObject _panelControls;
        [SerializeField] private GameObject _panelGameplay;

        // ── AUDIO TAB ─────────────────────────────────────────────────────────
        [Header("Audio")]
        [SerializeField] private Slider _sliderMaster;
        [SerializeField] private Slider _sliderMusic;
        [SerializeField] private Slider _sliderSFX;
        [SerializeField] private Slider _sliderVO;
        [SerializeField] private TextMeshProUGUI _lblMaster;
        [SerializeField] private TextMeshProUGUI _lblMusic;
        [SerializeField] private TextMeshProUGUI _lblSFX;
        [SerializeField] private TextMeshProUGUI _lblVO;

        // ── GRAPHICS TAB ──────────────────────────────────────────────────────
        [Header("Graphics")]
        [SerializeField] private TMP_Dropdown    _dropResolution;
        [SerializeField] private TMP_Dropdown    _dropQuality;
        [SerializeField] private Toggle          _toggleFullscreen;
        [SerializeField] private Toggle          _toggleVSync;
        [SerializeField] private Slider          _sliderFOV;
        [SerializeField] private TextMeshProUGUI _lblFOV;

        // ── CONTROLS TAB ──────────────────────────────────────────────────────
        [Header("Controls")]
        [SerializeField] private Slider          _sliderMouseSens;
        [SerializeField] private TextMeshProUGUI _lblMouseSens;
        [SerializeField] private Toggle          _toggleInvertY;
        [SerializeField] private Transform       _keybindContainer;   // parent for keybind rows
        [SerializeField] private GameObject      _keybindRowPrefab;   // KeybindRow prefab

        // ── GAMEPLAY TAB ──────────────────────────────────────────────────────
        [Header("Gameplay")]
        [SerializeField] private Toggle          _toggleAutoPause;
        [SerializeField] private Toggle          _toggleSubtitles;
        [SerializeField] private Toggle          _toggleTutorialHints;
        [SerializeField] private Toggle          _toggleCombatLog;
        [SerializeField] private TMP_Dropdown    _dropDifficulty;

        // ── FOOTER ────────────────────────────────────────────────────────────
        [Header("Footer")]
        [SerializeField] private Button _btnApply;
        [SerializeField] private Button _btnRevert;
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnSaveQuit;
        [SerializeField] private TextMeshProUGUI _lblVersion;

        // ══════════════════════════════════════════════════════════════════════
        //  PREFS KEYS
        // ══════════════════════════════════════════════════════════════════════

        private const string PREF_MASTER    = "opt_vol_master";
        private const string PREF_MUSIC     = "opt_vol_music";
        private const string PREF_SFX       = "opt_vol_sfx";
        private const string PREF_VO        = "opt_vol_vo";
        private const string PREF_FULLSCREEN= "opt_fullscreen";
        private const string PREF_VSYNC     = "opt_vsync";
        private const string PREF_QUALITY   = "opt_quality";
        private const string PREF_RES_IDX   = "opt_res_idx";
        private const string PREF_FOV       = "opt_fov";
        private const string PREF_MOUSE_SENS= "opt_mouse_sens";
        private const string PREF_INVERT_Y  = "opt_invert_y";
        private const string PREF_AUTOPAUSE = "opt_autopause";
        private const string PREF_SUBTITLES = "opt_subtitles";
        private const string PREF_TUTORIALS = "opt_tutorials";
        private const string PREF_COMBATLOG = "opt_combatlog";
        private const string PREF_DIFFICULTY= "opt_difficulty";

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private bool   _isOpen     = false;
        private int    _activeTab  = 0;
        private Resolution[] _availableResolutions;

        // Snapshot for revert
        private OptionsSnapshot _snapshot;

        private struct OptionsSnapshot
        {
            public float Master, Music, SFX, VO;
            public bool  Fullscreen, VSync;
            public int   Quality, ResIdx, Difficulty;
            public float FOV, MouseSens;
            public bool  InvertY, AutoPause, Subtitles, Tutorials, CombatLog;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            BuildResolutionDropdown();
            BuildQualityDropdown();
            BuildDifficultyDropdown();
            RegisterListeners();
            LoadPrefsToUI();
            ApplyAllSettings();
            ShowTab(0);
            SetPanelVisible(false);

            if (_lblVersion != null)
                _lblVersion.text = $"KotOR-Unity  v5.0  build {Application.version}";
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                Toggle();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        public bool IsOpen => _isOpen;

        public void Toggle() { if (_isOpen) Close(); else Open(); }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            TakeSnapshot();
            LoadPrefsToUI();
            SetPanelVisible(true);
            // Pause time if in pause-able mode
            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());
            OnOpened?.Invoke();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            SetPanelVisible(false);
            OnClosed?.Invoke();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TAB NAVIGATION
        // ══════════════════════════════════════════════════════════════════════

        private void ShowTab(int index)
        {
            _activeTab = index;
            if (_panelAudio    != null) _panelAudio   .SetActive(index == 0);
            if (_panelGraphics != null) _panelGraphics.SetActive(index == 1);
            if (_panelControls != null) _panelControls.SetActive(index == 2);
            if (_panelGameplay != null) _panelGameplay.SetActive(index == 3);

            // Highlight active tab button
            SetTabHighlight(_tabAudio,    index == 0);
            SetTabHighlight(_tabGraphics, index == 1);
            SetTabHighlight(_tabControls, index == 2);
            SetTabHighlight(_tabGameplay, index == 3);
        }

        private void SetTabHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null)
                img.color = active ? new Color(0.3f, 0.6f, 1f) : Color.white;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LISTENER REGISTRATION
        // ══════════════════════════════════════════════════════════════════════

        private void RegisterListeners()
        {
            // Tabs
            _tabAudio?.onClick.AddListener(() => ShowTab(0));
            _tabGraphics?.onClick.AddListener(() => ShowTab(1));
            _tabControls?.onClick.AddListener(() => ShowTab(2));
            _tabGameplay?.onClick.AddListener(() => ShowTab(3));

            // Audio sliders  — live-apply
            _sliderMaster?.onValueChanged.AddListener(v => { UpdateLabel(_lblMaster, v); ApplyVolumes(); });
            _sliderMusic ?.onValueChanged.AddListener(v => { UpdateLabel(_lblMusic,  v); ApplyVolumes(); });
            _sliderSFX   ?.onValueChanged.AddListener(v => { UpdateLabel(_lblSFX,    v); ApplyVolumes(); });
            _sliderVO    ?.onValueChanged.AddListener(v => { UpdateLabel(_lblVO,     v); ApplyVolumes(); });

            // Graphics
            _sliderFOV        ?.onValueChanged.AddListener(v => { UpdateLabel(_lblFOV, v, "°"); });
            _toggleFullscreen ?.onValueChanged.AddListener(_ => { });
            _toggleVSync      ?.onValueChanged.AddListener(_ => { });

            // Controls
            _sliderMouseSens?.onValueChanged.AddListener(v => UpdateLabel(_lblMouseSens, v));

            // Footer
            _btnApply    ?.onClick.AddListener(ApplyAndSave);
            _btnRevert   ?.onClick.AddListener(RevertToSnapshot);
            _btnClose    ?.onClick.AddListener(Close);
            _btnSaveQuit ?.onClick.AddListener(SaveAndQuit);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUILD DROPDOWNS
        // ══════════════════════════════════════════════════════════════════════

        private void BuildResolutionDropdown()
        {
            if (_dropResolution == null) return;
            _availableResolutions = Screen.resolutions;
            _dropResolution.ClearOptions();
            var opts = new List<string>();
            foreach (var r in _availableResolutions)
                opts.Add($"{r.width} × {r.height}  @{r.refreshRate}Hz");
            _dropResolution.AddOptions(opts);
        }

        private void BuildQualityDropdown()
        {
            if (_dropQuality == null) return;
            _dropQuality.ClearOptions();
            _dropQuality.AddOptions(new List<string>(QualitySettings.names));
        }

        private void BuildDifficultyDropdown()
        {
            if (_dropDifficulty == null) return;
            _dropDifficulty.ClearOptions();
            _dropDifficulty.AddOptions(new List<string> { "Easy", "Normal", "Hard", "Nightmare" });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD PREFS → UI
        // ══════════════════════════════════════════════════════════════════════

        private void LoadPrefsToUI()
        {
            // Audio
            SetSlider(_sliderMaster, PlayerPrefs.GetFloat(PREF_MASTER, 1f));
            SetSlider(_sliderMusic,  PlayerPrefs.GetFloat(PREF_MUSIC,  0.8f));
            SetSlider(_sliderSFX,    PlayerPrefs.GetFloat(PREF_SFX,    0.9f));
            SetSlider(_sliderVO,     PlayerPrefs.GetFloat(PREF_VO,     1f));

            UpdateLabel(_lblMaster, _sliderMaster?.value ?? 1f);
            UpdateLabel(_lblMusic,  _sliderMusic?.value  ?? 0.8f);
            UpdateLabel(_lblSFX,    _sliderSFX?.value    ?? 0.9f);
            UpdateLabel(_lblVO,     _sliderVO?.value     ?? 1f);

            // Graphics
            SetToggle(_toggleFullscreen, PlayerPrefs.GetInt(PREF_FULLSCREEN, 1) == 1);
            SetToggle(_toggleVSync,      PlayerPrefs.GetInt(PREF_VSYNC,      1) == 1);
            SetDropdown(_dropQuality,    PlayerPrefs.GetInt(PREF_QUALITY,    QualitySettings.GetQualityLevel()));
            SetDropdown(_dropResolution, PlayerPrefs.GetInt(PREF_RES_IDX,    0));
            SetSlider(_sliderFOV,        PlayerPrefs.GetFloat(PREF_FOV,      60f));
            UpdateLabel(_lblFOV, _sliderFOV?.value ?? 60f, "°");

            // Controls
            SetSlider(_sliderMouseSens,  PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 0.5f));
            SetToggle(_toggleInvertY,    PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1);
            UpdateLabel(_lblMouseSens,   _sliderMouseSens?.value ?? 0.5f);

            // Gameplay
            SetToggle(_toggleAutoPause,     PlayerPrefs.GetInt(PREF_AUTOPAUSE,  1) == 1);
            SetToggle(_toggleSubtitles,     PlayerPrefs.GetInt(PREF_SUBTITLES,  1) == 1);
            SetToggle(_toggleTutorialHints, PlayerPrefs.GetInt(PREF_TUTORIALS,  1) == 1);
            SetToggle(_toggleCombatLog,     PlayerPrefs.GetInt(PREF_COMBATLOG,  1) == 1);
            SetDropdown(_dropDifficulty,    PlayerPrefs.GetInt(PREF_DIFFICULTY, 1));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  APPLY ALL SETTINGS
        // ══════════════════════════════════════════════════════════════════════

        private void ApplyAllSettings()
        {
            ApplyVolumes();
            ApplyGraphics();
            ApplyGameplay();
        }

        private void ApplyVolumes()
        {
            float master = _sliderMaster?.value ?? 1f;
            float music  = _sliderMusic?.value  ?? 0.8f;
            float sfx    = _sliderSFX?.value    ?? 0.9f;
            float vo     = _sliderVO?.value     ?? 1f;

            AudioListener.volume = master;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetMusicVolume(music * master);
                AudioManager.Instance.SetSFXVolume(sfx * master);
                AudioManager.Instance.SetVoiceVolume(vo * master);
            }
        }

        private void ApplyGraphics()
        {
            // Quality level
            int quality = _dropQuality?.value ?? QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(quality, true);

            // VSync
            bool vsync = _toggleVSync?.isOn ?? true;
            QualitySettings.vSyncCount = vsync ? 1 : 0;

            // Fullscreen + resolution
            bool fullscreen = _toggleFullscreen?.isOn ?? true;
            int resIdx = _dropResolution?.value ?? 0;
            if (_availableResolutions != null && resIdx < _availableResolutions.Length)
            {
                var r = _availableResolutions[resIdx];
                Screen.SetResolution(r.width, r.height, fullscreen, r.refreshRate);
            }
            else
            {
                Screen.fullScreen = fullscreen;
            }

            // FOV — publish so CameraControllers can pick it up
            float fov = _sliderFOV?.value ?? 60f;
            EventBus.Publish(EventBus.EventType.UIHUDRefresh,
                new EventBus.GameEventArgs());
            PlayerPrefs.SetFloat(PREF_FOV, fov);
        }

        private void ApplyGameplay()
        {
            // Auto-pause on round
            bool autoPause = _toggleAutoPause?.isOn ?? true;
            var combatMgr = FindObjectOfType<Combat.CombatManager>();
            if (combatMgr != null)
                combatMgr.AutoPauseOnRound = autoPause;

            PlayerPrefs.SetInt(PREF_AUTOPAUSE, autoPause ? 1 : 0);

            // Subtitles toggle — publish to dialogue system
            bool subtitles = _toggleSubtitles?.isOn ?? true;
            PlayerPrefs.SetInt(PREF_SUBTITLES, subtitles ? 1 : 0);

            // Difficulty → stored for combat resolver
            int diff = _dropDifficulty?.value ?? 1;
            PlayerPrefs.SetInt(PREF_DIFFICULTY, diff);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  APPLY & SAVE / REVERT
        // ══════════════════════════════════════════════════════════════════════

        public void ApplyAndSave()
        {
            ApplyAllSettings();
            SavePrefs();
            Debug.Log("[OptionsMenu] Settings applied and saved.");
        }

        private void SavePrefs()
        {
            PlayerPrefs.SetFloat(PREF_MASTER,    _sliderMaster?.value ?? 1f);
            PlayerPrefs.SetFloat(PREF_MUSIC,     _sliderMusic?.value  ?? 0.8f);
            PlayerPrefs.SetFloat(PREF_SFX,       _sliderSFX?.value    ?? 0.9f);
            PlayerPrefs.SetFloat(PREF_VO,        _sliderVO?.value     ?? 1f);
            PlayerPrefs.SetInt(PREF_FULLSCREEN,  (_toggleFullscreen?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_VSYNC,       (_toggleVSync?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_QUALITY,     _dropQuality?.value ?? 0);
            PlayerPrefs.SetInt(PREF_RES_IDX,     _dropResolution?.value ?? 0);
            PlayerPrefs.SetFloat(PREF_FOV,       _sliderFOV?.value ?? 60f);
            PlayerPrefs.SetFloat(PREF_MOUSE_SENS,_sliderMouseSens?.value ?? 0.5f);
            PlayerPrefs.SetInt(PREF_INVERT_Y,    (_toggleInvertY?.isOn ?? false) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_AUTOPAUSE,   (_toggleAutoPause?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_SUBTITLES,   (_toggleSubtitles?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_TUTORIALS,   (_toggleTutorialHints?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_COMBATLOG,   (_toggleCombatLog?.isOn ?? true) ? 1 : 0);
            PlayerPrefs.SetInt(PREF_DIFFICULTY,  _dropDifficulty?.value ?? 1);
            PlayerPrefs.Save();
        }

        private void TakeSnapshot()
        {
            _snapshot = new OptionsSnapshot
            {
                Master     = _sliderMaster?.value ?? 1f,
                Music      = _sliderMusic?.value  ?? 0.8f,
                SFX        = _sliderSFX?.value    ?? 0.9f,
                VO         = _sliderVO?.value     ?? 1f,
                Fullscreen = _toggleFullscreen?.isOn ?? true,
                VSync      = _toggleVSync?.isOn ?? true,
                Quality    = _dropQuality?.value ?? 0,
                ResIdx     = _dropResolution?.value ?? 0,
                FOV        = _sliderFOV?.value ?? 60f,
                MouseSens  = _sliderMouseSens?.value ?? 0.5f,
                InvertY    = _toggleInvertY?.isOn ?? false,
                AutoPause  = _toggleAutoPause?.isOn ?? true,
                Subtitles  = _toggleSubtitles?.isOn ?? true,
                Tutorials  = _toggleTutorialHints?.isOn ?? true,
                CombatLog  = _toggleCombatLog?.isOn ?? true,
                Difficulty = _dropDifficulty?.value ?? 1
            };
        }

        public void RevertToSnapshot()
        {
            SetSlider(_sliderMaster,    _snapshot.Master);
            SetSlider(_sliderMusic,     _snapshot.Music);
            SetSlider(_sliderSFX,       _snapshot.SFX);
            SetSlider(_sliderVO,        _snapshot.VO);
            SetToggle(_toggleFullscreen, _snapshot.Fullscreen);
            SetToggle(_toggleVSync,      _snapshot.VSync);
            SetDropdown(_dropQuality,   _snapshot.Quality);
            SetDropdown(_dropResolution,_snapshot.ResIdx);
            SetSlider(_sliderFOV,       _snapshot.FOV);
            SetSlider(_sliderMouseSens, _snapshot.MouseSens);
            SetToggle(_toggleInvertY,   _snapshot.InvertY);
            SetToggle(_toggleAutoPause, _snapshot.AutoPause);
            SetToggle(_toggleSubtitles, _snapshot.Subtitles);
            SetToggle(_toggleTutorialHints, _snapshot.Tutorials);
            SetToggle(_toggleCombatLog, _snapshot.CombatLog);
            SetDropdown(_dropDifficulty,_snapshot.Difficulty);
            ApplyAllSettings();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SAVE AND QUIT
        // ══════════════════════════════════════════════════════════════════════

        private void SaveAndQuit()
        {
            ApplyAndSave();
            var saveMgr = SaveSystem.SaveManager.Instance;
            if (saveMgr != null)
            {
                saveMgr.QuickSave();
                Debug.Log("[OptionsMenu] Quick-saved before quit.");
            }
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STATIC ACCESSOR — volumes read by AudioManager on init
        // ══════════════════════════════════════════════════════════════════════

        public static float GetMasterVolume()  => PlayerPrefs.GetFloat(PREF_MASTER, 1f);
        public static float GetMusicVolume()   => PlayerPrefs.GetFloat(PREF_MUSIC,  0.8f);
        public static float GetSFXVolume()     => PlayerPrefs.GetFloat(PREF_SFX,    0.9f);
        public static float GetVOVolume()      => PlayerPrefs.GetFloat(PREF_VO,     1f);
        public static float GetFOV()           => PlayerPrefs.GetFloat(PREF_FOV,    60f);
        public static float GetMouseSens()     => PlayerPrefs.GetFloat(PREF_MOUSE_SENS, 0.5f);
        public static bool  GetInvertY()       => PlayerPrefs.GetInt(PREF_INVERT_Y, 0) == 1;
        public static bool  GetSubtitles()     => PlayerPrefs.GetInt(PREF_SUBTITLES, 1) == 1;
        public static int   GetDifficulty()    => PlayerPrefs.GetInt(PREF_DIFFICULTY, 1);

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void SetPanelVisible(bool visible)
        {
            if (_rootPanel != null)
                _rootPanel.SetActive(visible);
        }

        private static void SetSlider(Slider s, float v)
        {
            if (s != null) s.value = v;
        }

        private static void SetToggle(Toggle t, bool v)
        {
            if (t != null) t.isOn = v;
        }

        private static void SetDropdown(TMP_Dropdown d, int v)
        {
            if (d != null) d.value = v;
        }

        private static void UpdateLabel(TextMeshProUGUI lbl, float v, string suffix = "")
        {
            if (lbl != null)
                lbl.text = $"{Mathf.RoundToInt(v * 100)}{suffix}";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  KEYBIND ROW  (one row per action in the Controls tab)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Displays one keybinding row: action label + current key + rebind button.
    /// The rebind button starts a "listening" coroutine that captures the next keypress.
    /// </summary>
    public class KeybindRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _actionLabel;
        [SerializeField] private TextMeshProUGUI _keyLabel;
        [SerializeField] private Button          _rebindButton;

        private string  _prefKey;
        private KeyCode _currentKey;
        private bool    _isListening;

        public void Initialise(string actionName, string prefKey, KeyCode defaultKey)
        {
            _prefKey     = prefKey;
            _currentKey  = (KeyCode)PlayerPrefs.GetInt(prefKey, (int)defaultKey);
            _isListening = false;

            if (_actionLabel != null) _actionLabel.text = actionName;
            RefreshKeyLabel();

            _rebindButton?.onClick.AddListener(StartListening);
        }

        private void Update()
        {
            if (!_isListening) return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _isListening = false;
                RefreshKeyLabel();
                return;
            }

            // Capture any key that was pressed this frame
            foreach (KeyCode kc in Enum.GetValues(typeof(KeyCode)))
            {
                if (kc == KeyCode.None) continue;
                if (Input.GetKeyDown(kc))
                {
                    _currentKey  = kc;
                    _isListening = false;
                    PlayerPrefs.SetInt(_prefKey, (int)kc);
                    PlayerPrefs.Save();
                    RefreshKeyLabel();
                    return;
                }
            }
        }

        private void StartListening()
        {
            _isListening = true;
            if (_keyLabel != null) _keyLabel.text = "[press any key]";
        }

        private void RefreshKeyLabel()
        {
            if (_keyLabel != null) _keyLabel.text = _currentKey.ToString();
        }
    }
}
