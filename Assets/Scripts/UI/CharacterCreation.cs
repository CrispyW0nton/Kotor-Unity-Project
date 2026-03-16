using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Core;
using KotORUnity.Data;
using KotORUnity.Player;
using KotORUnity.SaveSystem;
using static KotORUnity.Core.GameEnums;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  DATA MODELS
    // ═══════════════════════════════════════════════════════════════════════════

    [Serializable]
    public class CharacterClass
    {
        public int    ClassId;       // index in classes.2da
        public string Name;
        public string Description;
        public int    HitDie;        // e.g. 10 = d10
        public int    ForceDie;      // 0 = no Force (Scout, Soldier, Scoundrel)
        public int    BaseAttackBonusProgression; // 1=high,2=med,3=low
        public bool   IsForceUser;
        public List<int> PrimarySkills = new List<int>(); // skill indices
    }

    [Serializable]
    public class CharacterRace
    {
        public string Name;          // "Human" — only race in KotOR1
        public int    StrMod, DexMod, ConMod, IntMod, WisMod, ChaMod;
        public int    ExtraSkillPoints;
        public int    ExtraFeats;
    }

    [Serializable]
    public class AttributeSet
    {
        public int Strength     = 10;
        public int Dexterity    = 10;
        public int Constitution = 10;
        public int Intelligence = 10;
        public int Wisdom       = 10;
        public int Charisma     = 10;

        public int Modifier(int score) => (score - 10) / 2;
    }

    [Serializable]
    public class NewGameConfig
    {
        public string PlayerName = "Revan";
        public int    ClassId    = 0;   // Soldier by default
        public int    PortraitId = 0;
        public AttributeSet Attributes = new AttributeSet();
        public int    Gender = 0;       // 0=male, 1=female
        public int    AppearanceId = 0; // appearance.2da row
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  CHARACTER CREATION CONTROLLER
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drives the KotOR character creation flow:
    ///   1. Class selection
    ///   2. Attribute allocation  (28-point buy, matching KotOR1)
    ///   3. Portrait selection
    ///   4. Name input
    ///   5. Confirm → start game
    ///
    /// All panels are driven by the same controller; active panel is shown via
    /// Show/HidePanel helpers. Wire the SerializeField references in the Inspector.
    /// </summary>
    public class CharacterCreationController : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static CharacterCreationController Instance { get; private set; }

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<NewGameConfig> OnCreationComplete;

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR REFERENCES
        // ══════════════════════════════════════════════════════════════════════

        [Header("Panels")]
        [SerializeField] private GameObject _panelClass;
        [SerializeField] private GameObject _panelAttributes;
        [SerializeField] private GameObject _panelPortrait;
        [SerializeField] private GameObject _panelName;

        [Header("Class Panel")]
        [SerializeField] private Transform _classButtonContainer;
        [SerializeField] private Button    _classButtonPrefab;
        [SerializeField] private TextMeshProUGUI _classDescText;
        [SerializeField] private TextMeshProUGUI _classHitDieText;
        [SerializeField] private TextMeshProUGUI _classForceText;

        [Header("Attribute Panel")]
        [SerializeField] private TextMeshProUGUI _pointsRemainingText;
        [SerializeField] private AttributeRow[]  _attributeRows; // wired in Inspector
        [SerializeField] private TextMeshProUGUI _hpPreviewText;
        [SerializeField] private TextMeshProUGUI _fpPreviewText;

        [Header("Portrait Panel")]
        [SerializeField] private Transform  _portraitContainer;
        [SerializeField] private Button     _portraitButtonPrefab;
        [SerializeField] private Image      _portraitPreview;

        [Header("Name Panel")]
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TextMeshProUGUI _summaryText;

        [Header("Navigation")]
        [SerializeField] private Button _btnPrev;
        [SerializeField] private Button _btnNext;
        [SerializeField] private TextMeshProUGUI _stepLabel;

        [Header("Save Slot Selection (shown before final confirm)")]
        [SerializeField] private GameObject     _panelSlotSelect;
        [SerializeField] private Transform      _slotButtonContainer;
        [SerializeField] private Button         _slotButtonPrefab;
        [SerializeField] private TextMeshProUGUI _slotInfoText;

        // ══════════════════════════════════════════════════════════════════════
        //  STATE
        // ══════════════════════════════════════════════════════════════════════

        private int _currentStep = 0;   // 0=class, 1=attrs, 2=portrait, 3=name
        private const int TOTAL_STEPS = 4;

        private const int POINT_BUY_TOTAL  = 30; // KotOR uses 30-point buy
        private const int BASE_STAT        = 8;  // costs 0 to go from 8→8

        private NewGameConfig _config = new NewGameConfig();
        private int _pointsRemaining  = POINT_BUY_TOTAL;

        private List<CharacterClass> _classes  = new List<CharacterClass>();
        private List<string>         _portraits = new List<string>(); // resrefs

        // Attribute point costs: each index = cost to buy (stat-8) points
        private static readonly int[] _buyCosts = { 0, 1, 2, 3, 4, 5, 7, 9, 12, 15 };

        private SaveSlot _selectedSlot = SaveSlot.Manual1;

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
            LoadClassData();
            LoadPortraitData();
            BuildClassButtons();
            BuildPortraitButtons();
            BuildSlotButtons();
            ShowStep(0);

            if (_btnPrev != null) _btnPrev.onClick.AddListener(PrevStep);
            if (_btnNext != null) _btnNext.onClick.AddListener(NextStep);
            if (_nameInput != null) _nameInput.onValueChanged.AddListener(OnNameChanged);

            // Hide slot panel until confirmation
            if (_panelSlotSelect != null) _panelSlotSelect.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  STEP NAVIGATION
        // ══════════════════════════════════════════════════════════════════════

        private void ShowStep(int step)
        {
            _currentStep = Mathf.Clamp(step, 0, TOTAL_STEPS - 1);

            if (_panelClass       != null) _panelClass      .SetActive(_currentStep == 0);
            if (_panelAttributes  != null) _panelAttributes .SetActive(_currentStep == 1);
            if (_panelPortrait    != null) _panelPortrait   .SetActive(_currentStep == 2);
            if (_panelName        != null) _panelName       .SetActive(_currentStep == 3);

            if (_btnPrev != null) _btnPrev.interactable = _currentStep > 0;
            if (_btnNext != null)
            {
                bool isLast = _currentStep == TOTAL_STEPS - 1;
                var label = _btnNext.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = isLast ? "CONFIRM" : "NEXT";
            }

            if (_stepLabel != null)
            {
                string[] names = { "CLASS", "ATTRIBUTES", "PORTRAIT", "NAME" };
                _stepLabel.text = $"STEP {_currentStep + 1} / {TOTAL_STEPS}: {names[_currentStep]}";
            }

            // Per-step refresh
            if (_currentStep == 1) RefreshAttributePanel();
            if (_currentStep == 3) RefreshSummary();
        }

        private void PrevStep() => ShowStep(_currentStep - 1);

        private void NextStep()
        {
            if (_currentStep < TOTAL_STEPS - 1)
            {
                ShowStep(_currentStep + 1);
            }
            else
            {
                ConfirmCreation();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CLASS PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void LoadClassData()
        {
            _classes.Clear();
            var repo = GameDataRepository.Instance;

            // KotOR starting classes: Soldier (row 0), Scout (row 1), Scoundrel (row 2)
            string[] classNames = { "Soldier", "Scout", "Scoundrel" };
            int[]    hitDice    = { 10, 8, 6 };
            bool[]   hasForce   = { false, false, false };
            string[] descs      = {
                "Soldiers are combat specialists trained in all forms of warfare. High HP, all weapon proficiencies.",
                "Scouts are versatile survivalists. Balanced skills and combat ability, bonus to Repair and Computer Use.",
                "Scoundrels rely on cunning and stealth. Highest skills, Sneak Attack, but fewest HP."
            };

            for (int i = 0; i < classNames.Length; i++)
            {
                _classes.Add(new CharacterClass
                {
                    ClassId     = i,
                    Name        = classNames[i],
                    HitDie      = hitDice[i],
                    IsForceUser = hasForce[i],
                    Description = descs[i]
                });
            }
        }

        private void BuildClassButtons()
        {
            if (_classButtonContainer == null || _classButtonPrefab == null) return;

            foreach (Transform child in _classButtonContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < _classes.Count; i++)
            {
                var classRef = _classes[i];
                var btn = Instantiate(_classButtonPrefab, _classButtonContainer);
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = classRef.Name;
                btn.onClick.AddListener(() => SelectClass(classRef));
            }
        }

        private void SelectClass(CharacterClass cls)
        {
            _config.ClassId = cls.ClassId;

            if (_classDescText  != null) _classDescText .text = cls.Description;
            if (_classHitDieText!= null) _classHitDieText.text = $"Hit Die: d{cls.HitDie}";
            if (_classForceText != null)
                _classForceText.text = cls.IsForceUser ? "Force Sensitive" : "No Force Powers";

            Debug.Log($"[CharCreation] Class selected: {cls.Name}");
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ATTRIBUTE PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshAttributePanel()
        {
            if (_pointsRemainingText != null)
                _pointsRemainingText.text = $"Points Remaining: {_pointsRemaining}";

            // HP / FP preview
            var cls = _classes.Find(c => c.ClassId == _config.ClassId);
            int conMod = _config.Attributes.Modifier(_config.Attributes.Constitution);
            int hp     = (cls?.HitDie ?? 10) + conMod;
            int fp     = cls != null && cls.IsForceUser
                       ? 4 + _config.Attributes.Modifier(_config.Attributes.Wisdom) : 0;

            if (_hpPreviewText != null) _hpPreviewText.text = $"Starting HP: {hp}";
            if (_fpPreviewText != null) _fpPreviewText.text = cls?.IsForceUser == true
                ? $"Force Points: {fp}" : "Force Points: —";
        }

        /// <summary>Called by AttributeRow UI buttons.</summary>
        public void IncreaseStat(int statIndex)
        {
            var attrs = _config.Attributes;
            int current = GetStat(attrs, statIndex);
            if (current >= 18) return; // cap

            int cost = BuyCost(current + 1) - BuyCost(current);
            if (_pointsRemaining < cost) return;

            SetStat(attrs, statIndex, current + 1);
            _pointsRemaining -= cost;
            RefreshAttributePanel();
        }

        public void DecreaseStat(int statIndex)
        {
            var attrs = _config.Attributes;
            int current = GetStat(attrs, statIndex);
            if (current <= BASE_STAT) return;

            int refund = BuyCost(current) - BuyCost(current - 1);
            SetStat(attrs, statIndex, current - 1);
            _pointsRemaining += refund;
            RefreshAttributePanel();
        }

        private static int BuyCost(int value)
        {
            int offset = value - BASE_STAT;
            if (offset < 0) return 0;
            if (offset >= _buyCosts.Length) return _buyCosts[_buyCosts.Length - 1] + offset;
            return _buyCosts[offset];
        }

        private static int GetStat(AttributeSet a, int index) => index switch
        {
            0 => a.Strength,     1 => a.Dexterity, 2 => a.Constitution,
            3 => a.Intelligence, 4 => a.Wisdom,    5 => a.Charisma,
            _ => 10
        };

        private static void SetStat(AttributeSet a, int index, int value)
        {
            switch (index)
            {
                case 0: a.Strength     = value; break;
                case 1: a.Dexterity   = value; break;
                case 2: a.Constitution= value; break;
                case 3: a.Intelligence= value; break;
                case 4: a.Wisdom      = value; break;
                case 5: a.Charisma    = value; break;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PORTRAIT PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void LoadPortraitData()
        {
            _portraits.Clear();
            // KotOR portrait resrefs follow "po_pXXX" naming convention
            for (int i = 0; i < 8; i++)
                _portraits.Add($"po_pbmn{i:D2}");  // example: po_pbmn00..07
        }

        private void BuildPortraitButtons()
        {
            if (_portraitContainer == null || _portraitButtonPrefab == null) return;

            foreach (Transform child in _portraitContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < _portraits.Count; i++)
            {
                int idx = i;
                var btn = Instantiate(_portraitButtonPrefab, _portraitContainer);
                var img = btn.GetComponent<Image>();
                if (img != null)
                {
                    var tex = KotOR.Parsers.TextureCache.Get(_portraits[idx]);
                    if (tex != null) img.sprite = Sprite.Create(tex,
                        new Rect(0,0,tex.width,tex.height), Vector2.one * 0.5f);
                }
                btn.onClick.AddListener(() => SelectPortrait(idx));
            }
        }

        private void SelectPortrait(int idx)
        {
            _config.PortraitId = idx;
            if (_portraitPreview != null && idx < _portraits.Count)
            {
                var tex = KotOR.Parsers.TextureCache.Get(_portraits[idx]);
                if (tex != null)
                    _portraitPreview.sprite = Sprite.Create(tex,
                        new Rect(0,0,tex.width,tex.height), Vector2.one * 0.5f);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  NAME PANEL
        // ══════════════════════════════════════════════════════════════════════

        private void OnNameChanged(string name)
        {
            _config.PlayerName = string.IsNullOrWhiteSpace(name) ? "Revan" : name.Trim();
        }

        private void RefreshSummary()
        {
            if (_summaryText == null) return;
            var a  = _config.Attributes;
            var cls = _classes.Find(c => c.ClassId == _config.ClassId);
            _summaryText.text =
                $"Name:  {_config.PlayerName}\n" +
                $"Class: {cls?.Name ?? "Unknown"}\n\n" +
                $"STR {a.Strength}  DEX {a.Dexterity}  CON {a.Constitution}\n" +
                $"INT {a.Intelligence}  WIS {a.Wisdom}  CHA {a.Charisma}";
        }

        // ══════════════════════════════════════════════════════════════════════
        //  SAVE SLOT SELECTION
        // ══════════════════════════════════════════════════════════════════════

        private void BuildSlotButtons()
        {
            if (_slotButtonContainer == null || _slotButtonPrefab == null) return;

            foreach (Transform c in _slotButtonContainer) Destroy(c.gameObject);

            var slots = new[]
            {
                SaveSlot.Manual1, SaveSlot.Manual2, SaveSlot.Manual3,
                SaveSlot.Manual4, SaveSlot.Manual5
            };

            var saveMgr = SaveManager.Instance;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var btn  = Instantiate(_slotButtonPrefab, _slotButtonContainer);

                // Label
                var lbl  = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                {
                    bool exists   = saveMgr != null && saveMgr.SaveExists(slot);
                    string label  = $"Slot {i + 1}";
                    if (exists)
                    {
                        var ts     = saveMgr.GetSaveTimestamp(slot);
                        string mod = saveMgr.GetSaveModuleName(slot);
                        label = $"Slot {i + 1}  [{mod}  {ts?.ToString("yyyy-MM-dd HH:mm") ?? "?"}]";
                    }
                    else
                    {
                        label = $"Slot {i + 1}  [Empty]";
                    }
                    lbl.text = label;
                }

                btn.onClick.AddListener(() => SelectSlot(slot));
            }
        }

        private void SelectSlot(SaveSlot slot)
        {
            _selectedSlot = slot;
            if (_slotInfoText != null)
                _slotInfoText.text = $"Selected: {slot}";
            LaunchGame();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONFIRM
        // ══════════════════════════════════════════════════════════════════════

        private void ConfirmCreation()
        {
            if (string.IsNullOrWhiteSpace(_config.PlayerName))
                _config.PlayerName = "Revan";

            Debug.Log($"[CharCreation] Character created: {_config.PlayerName} / class {_config.ClassId}");

            OnCreationComplete?.Invoke(_config);

            // Show save-slot selection panel
            if (_panelSlotSelect != null)
            {
                BuildSlotButtons();          // refresh labels in case saves changed
                _panelSlotSelect.SetActive(true);
                // Hide step navigation while slot panel is active
                if (_btnNext != null) _btnNext.gameObject.SetActive(false);
                if (_btnPrev != null) _btnPrev.gameObject.SetActive(false);
            }
            else
            {
                // No slot panel wired — use Manual1 and go directly
                LaunchGame();
            }
        }

        private void LaunchGame()
        {
            if (_panelSlotSelect != null) _panelSlotSelect.SetActive(false);

            var gameMgr = GameManager.Instance;
            if (gameMgr != null)
                gameMgr.StartNewGame(_config, _selectedSlot);
            else
                Debug.LogError("[CharCreation] GameManager.Instance is null — cannot start new game.");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ATTRIBUTE ROW  (helper component for each stat row in the UI)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One row in the attribute panel (e.g. "Strength  10  [-][+]").
    /// Wire in the Inspector; the index corresponds to the attribute order in AttributeSet.
    /// </summary>
    public class AttributeRow : MonoBehaviour
    {
        [SerializeField] private int _statIndex;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private Button _decBtn;
        [SerializeField] private Button _incBtn;

        private void Start()
        {
            if (_decBtn != null)
                _decBtn.onClick.AddListener(() => CharacterCreationController.Instance?.DecreaseStat(_statIndex));
            if (_incBtn != null)
                _incBtn.onClick.AddListener(() => CharacterCreationController.Instance?.IncreaseStat(_statIndex));
        }

        public void Refresh(int value)
        {
            if (_valueText != null) _valueText.text = value.ToString();
        }
    }
}
