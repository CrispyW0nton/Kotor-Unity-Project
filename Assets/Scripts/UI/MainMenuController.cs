using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Bootstrap;
using KotORUnity.SaveSystem;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  MAIN MENU CONTROLLER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drives the main menu scene.
    ///
    /// Panel flow:
    ///   Root Panel: New Game | Load Game | Options | Quit
    ///   New Game  → CharacterCreation scene (loaded additively)
    ///   Load Game → Save slot picker panel
    ///   Options   → Options panel (graphics / audio / keybinds)
    ///
    /// The main menu also shows the KotOR title logo and triggers the intro
    /// wipe/fade animation that plays on the main menu's first load.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static MainMenuController Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root")]
        [SerializeField] private CanvasGroup _rootGroup;
        [SerializeField] private Button      _btnNewGame;
        [SerializeField] private Button      _btnLoadGame;
        [SerializeField] private Button      _btnOptions;
        [SerializeField] private Button      _btnQuit;
        [SerializeField] private TextMeshProUGUI _versionLabel;

        [Header("Load Game Panel")]
        [SerializeField] private GameObject  _loadPanel;
        [SerializeField] private Button[]    _saveSlotButtons;  // 7 slots (Quick, Auto, 1-5)
        [SerializeField] private TextMeshProUGUI[] _saveSlotLabels;
        [SerializeField] private Button      _btnCloseLoad;

        [Header("Options Panel")]
        [SerializeField] private GameObject  _optionsPanel;
        [SerializeField] private Slider      _masterVolumeSlider;
        [SerializeField] private Slider      _musicVolumeSlider;
        [SerializeField] private Slider      _sfxVolumeSlider;
        [SerializeField] private Toggle      _fullscreenToggle;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private Button      _btnCloseOptions;
        [SerializeField] private Button      _btnApplyOptions;

        [Header("Fade")]
        [SerializeField] private CanvasGroup _fadeOverlay;
        [SerializeField] private float       _fadeTime = 1f;

        [Header("Scene Names")]
        [SerializeField] private string _gameSceneName   = "Game";
        [SerializeField] private string _charCreateScene = "CharacterCreation";

        // ══════════════════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════════════════

        private SaveManager _saveManager;

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // SaveManager is a DontDestroyOnLoad singleton — use Instance, not
            // FindObjectOfType which cannot see DontDestroyOnLoad objects.
            _saveManager = SaveManager.Instance;

            // Wire buttons
            if (_btnNewGame  != null) _btnNewGame .onClick.AddListener(OnNewGame);
            if (_btnLoadGame != null) _btnLoadGame.onClick.AddListener(OnLoadGame);
            if (_btnOptions  != null) _btnOptions .onClick.AddListener(OnOptions);
            if (_btnQuit     != null) _btnQuit    .onClick.AddListener(OnQuit);

            if (_btnCloseLoad    != null) _btnCloseLoad   .onClick.AddListener(CloseAllPanels);
            if (_btnCloseOptions != null) _btnCloseOptions.onClick.AddListener(CloseAllPanels);
            if (_btnApplyOptions != null) _btnApplyOptions.onClick.AddListener(ApplyOptions);

            // Wire save-slot buttons
            var slots = (GameEnums.SaveSlot[])Enum.GetValues(typeof(GameEnums.SaveSlot));
            for (int i = 0; i < _saveSlotButtons?.Length && i < slots.Length; i++)
            {
                int slotIdx = i;
                GameEnums.SaveSlot slot = slots[i];
                if (_saveSlotButtons[i] != null)
                    _saveSlotButtons[i].onClick.AddListener(() => LoadSlot(slot));
                RefreshSlotLabel(i, slot);
            }

            // Hide sub-panels
            CloseAllPanels();

            // Version label
            if (_versionLabel != null)
                _versionLabel.text = $"MRL GameForge v2  |  KotOR-Unity Port";

            // Fade in
            StartCoroutine(FadeIn());

            // Load game button only active if any save exists
            bool hasSave = false;
            if (_saveManager != null)
                foreach (GameEnums.SaveSlot s in Enum.GetValues(typeof(GameEnums.SaveSlot)))
                    if (_saveManager.SaveExists(s)) { hasSave = true; break; }
            if (_btnLoadGame != null) _btnLoadGame.interactable = hasSave;

            // Load options from PlayerPrefs
            LoadOptions();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUTTON HANDLERS
        // ══════════════════════════════════════════════════════════════════════

        private void OnNewGame()
        {
            Debug.Log("[MainMenu] New Game selected.");
            StartCoroutine(FadeAndLoad(_charCreateScene));
        }

        private void OnLoadGame()
        {
            CloseAllPanels();
            if (_loadPanel != null) _loadPanel.SetActive(true);
        }

        private void OnOptions()
        {
            CloseAllPanels();
            if (_optionsPanel != null) _optionsPanel.SetActive(true);
        }

        private void OnQuit()
        {
            Debug.Log("[MainMenu] Quit.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CloseAllPanels()
        {
            if (_loadPanel    != null) _loadPanel   .SetActive(false);
            if (_optionsPanel != null) _optionsPanel.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  LOAD GAME PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshSlotLabel(int btnIndex, GameEnums.SaveSlot slot)
        {
            if (_saveSlotLabels == null || btnIndex >= _saveSlotLabels.Length) return;
            var lbl = _saveSlotLabels[btnIndex];
            if (lbl == null) return;

            if (_saveManager == null || !_saveManager.SaveExists(slot))
            {
                lbl.text = $"[{slot}] — Empty";
                if (_saveSlotButtons != null && btnIndex < _saveSlotButtons.Length)
                    if (_saveSlotButtons[btnIndex] != null)
                        _saveSlotButtons[btnIndex].interactable = false;
            }
            else
            {
                var ts = _saveManager.GetSaveTimestamp(slot);
                lbl.text = ts.HasValue
                    ? $"[{slot}] {ts.Value:yyyy-MM-dd HH:mm}"
                    : $"[{slot}] Saved";
            }
        }

        private void LoadSlot(GameEnums.SaveSlot slot)
        {
            if (_saveManager == null) return;
            Debug.Log($"[MainMenu] Loading slot: {slot}");
            StartCoroutine(FadeAndLoadSlot(slot));
        }

        private IEnumerator FadeAndLoadSlot(GameEnums.SaveSlot slot)
        {
            yield return StartCoroutine(FadeOut());
            _saveManager.Load(slot);
            SceneManager.LoadScene(_gameSceneName);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  OPTIONS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void LoadOptions()
        {
            if (_masterVolumeSlider  != null)
                _masterVolumeSlider.value  = PlayerPrefs.GetFloat("vol_master", 1f);
            if (_musicVolumeSlider   != null)
                _musicVolumeSlider.value   = PlayerPrefs.GetFloat("vol_music",  0.8f);
            if (_sfxVolumeSlider     != null)
                _sfxVolumeSlider.value     = PlayerPrefs.GetFloat("vol_sfx",    1f);
            if (_fullscreenToggle    != null)
                _fullscreenToggle.isOn     = Screen.fullScreen;

            BuildResolutionDropdown();
        }

        private void BuildResolutionDropdown()
        {
            if (_resolutionDropdown == null) return;
            _resolutionDropdown.ClearOptions();
            var resolutions = Screen.resolutions;
            int current = 0;
            var opts = new System.Collections.Generic.List<TMP_Dropdown.OptionData>();
            for (int i = 0; i < resolutions.Length; i++)
            {
                var r = resolutions[i];
                opts.Add(new TMP_Dropdown.OptionData($"{r.width}×{r.height} @{(int)r.refreshRateRatio.value}Hz"));
                if (r.width == Screen.currentResolution.width &&
                    r.height == Screen.currentResolution.height)
                    current = i;
            }
            _resolutionDropdown.AddOptions(opts);
            _resolutionDropdown.value = current;
        }

        private void ApplyOptions()
        {
            float master = _masterVolumeSlider  != null ? _masterVolumeSlider.value  : 1f;
            float music  = _musicVolumeSlider   != null ? _musicVolumeSlider.value   : 0.8f;
            float sfx    = _sfxVolumeSlider     != null ? _sfxVolumeSlider.value     : 1f;
            bool  fs     = _fullscreenToggle    != null && _fullscreenToggle.isOn;

            PlayerPrefs.SetFloat("vol_master", master);
            PlayerPrefs.SetFloat("vol_music",  music);
            PlayerPrefs.SetFloat("vol_sfx",    sfx);
            PlayerPrefs.SetInt  ("fullscreen", fs ? 1 : 0);
            PlayerPrefs.Save();

            // Apply audio
            AudioListener.volume = master;
            Screen.fullScreen    = fs;

            if (_resolutionDropdown != null)
            {
                var res = Screen.resolutions[_resolutionDropdown.value];
                Screen.SetResolution(res.width, res.height, fs);
            }

            Debug.Log("[MainMenu] Options applied.");
            CloseAllPanels();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  FADE HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator FadeIn()
        {
            if (_fadeOverlay == null) yield break;
            _fadeOverlay.alpha = 1f;
            float t = 0f;
            while (t < _fadeTime)
            {
                t += Time.unscaledDeltaTime;
                _fadeOverlay.alpha = 1f - Mathf.Clamp01(t / _fadeTime);
                yield return null;
            }
            _fadeOverlay.alpha = 0f;
        }

        private IEnumerator FadeOut()
        {
            if (_fadeOverlay == null) yield break;
            _fadeOverlay.alpha = 0f;
            float t = 0f;
            while (t < _fadeTime)
            {
                t += Time.unscaledDeltaTime;
                _fadeOverlay.alpha = Mathf.Clamp01(t / _fadeTime);
                yield return null;
            }
            _fadeOverlay.alpha = 1f;
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            yield return StartCoroutine(FadeOut());
            SceneManager.LoadScene(sceneName);
        }
    }
}
