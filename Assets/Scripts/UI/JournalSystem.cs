using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;
using KotORUnity.Scripting;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  JOURNAL DATA MODEL
    // ═══════════════════════════════════════════════════════════════════════════

    public enum QuestState
    {
        Inactive,
        Active,
        Success,
        Failure
    }

    /// <summary>
    /// A single quest entry in the journal.
    /// Mirrors the KotOR quest system (journal.2da / quest GFF data).
    /// </summary>
    [Serializable]
    public class QuestEntry
    {
        public string  QuestTag;
        public string  Title;
        public int     CurrentState;       // matches journal.2da StateID column
        public QuestState Status = QuestState.Inactive;
        public List<QuestObjective> Objectives = new List<QuestObjective>();
        public string  LastDescription;    // most recent journal text
        public DateTime LastUpdated;
    }

    [Serializable]
    public class QuestObjective
    {
        public int    StateId;
        public string Description;
        public bool   IsComplete;
        public bool   IsFailed;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  JOURNAL SYSTEM
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// JournalSystem manages the player's quest journal.
    ///
    /// KotOR scripts call:
    ///   AddJournalQuestEntry(tag, stateId)  — advances the quest to a new state.
    ///   SetQuestState(tag, QuestState)      — marks success/failure directly.
    ///
    /// The journal reads quest text from the TLK file (via TlkReader).
    ///
    /// Data: journal.2da contains the mapping  questTag → (stateId → strRef).
    /// We expose JournalSystem.Instance.AddEntry / AdvanceQuest for NWScript VM.
    /// </summary>
    public class JournalSystem : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static JournalSystem Instance { get; private set; }

        // ── EVENTS ────────────────────────────────────────────────────────────
        public event Action<QuestEntry> OnQuestUpdated;
        public event Action<QuestEntry> OnQuestCompleted;
        public event Action<QuestEntry> OnQuestFailed;

        // ── STATE ──────────────────────────────────────────────────────────────
        private readonly Dictionary<string, QuestEntry> _quests
            = new Dictionary<string, QuestEntry>(StringComparer.OrdinalIgnoreCase);

        // ── UNITY LIFECYCLE ────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            // Register NWScript handlers
            NWScriptVM.RegisterHandler("AddJournalQuestEntry", ctx =>
            {
                // The script passes tag and stateId as context globals
                string tag  = GlobalVars.GetString("_journal_tag");
                int stateId = GlobalVars.GetInt   ("_journal_state");
                AddEntry(tag, stateId);
            });
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Add or advance a quest to <paramref name="stateId"/>.
        /// Creates the entry if it doesn't exist yet.
        /// </summary>
        public void AddEntry(string questTag, int stateId, string overrideText = null)
        {
            if (string.IsNullOrEmpty(questTag)) return;

            if (!_quests.TryGetValue(questTag, out var entry))
            {
                entry = new QuestEntry
                {
                    QuestTag = questTag,
                    Title    = questTag // will be replaced by TLK lookup below
                };
                _quests[questTag] = entry;
            }

            entry.CurrentState = stateId;
            entry.LastUpdated  = DateTime.UtcNow;

            // Determine description text
            string text = overrideText ?? LookupQuestText(questTag, stateId);
            entry.LastDescription = text;

            // Infer status from conventional KotOR state ranges:
            //   0       = inactive / hidden
            //   1–29    = active
            //   30–59   = success (different branches)
            //   60–89   = failure
            //   ≥100    = complete (end state)
            entry.Status = stateId switch
            {
                0          => QuestState.Inactive,
                >= 60      => stateId >= 100 ? QuestState.Success : QuestState.Failure,
                _          => QuestState.Active
            };

            // Add/update objective
            var obj = entry.Objectives.Find(o => o.StateId == stateId);
            if (obj == null)
            {
                entry.Objectives.Add(new QuestObjective
                {
                    StateId     = stateId,
                    Description = text,
                    IsComplete  = entry.Status == QuestState.Success,
                    IsFailed    = entry.Status == QuestState.Failure
                });
            }
            else
            {
                obj.Description = text;
                obj.IsComplete  = entry.Status == QuestState.Success;
                obj.IsFailed    = entry.Status == QuestState.Failure;
            }

            Debug.Log($"[Journal] Quest '{questTag}' → state {stateId}: {entry.Status}");

            OnQuestUpdated?.Invoke(entry);
            if (entry.Status == QuestState.Success)
                OnQuestCompleted?.Invoke(entry);
            else if (entry.Status == QuestState.Failure)
                OnQuestFailed?.Invoke(entry);

            EventBus.Publish(EventBus.EventType.UIHUDRefresh);
        }

        /// <summary>Mark a quest as complete (success).</summary>
        public void CompleteQuest(string questTag)
        {
            if (!_quests.TryGetValue(questTag, out var entry)) return;
            entry.Status = QuestState.Success;
            OnQuestCompleted?.Invoke(entry);
        }

        /// <summary>Mark a quest as failed.</summary>
        public void FailQuest(string questTag)
        {
            if (!_quests.TryGetValue(questTag, out var entry)) return;
            entry.Status = QuestState.Failure;
            OnQuestFailed?.Invoke(entry);
        }

        public QuestEntry GetQuest(string tag)
        {
            _quests.TryGetValue(tag, out var e);
            return e;
        }

        public IEnumerable<QuestEntry> GetAllQuests() => _quests.Values;

        public IEnumerable<QuestEntry> GetActiveQuests()
        {
            foreach (var q in _quests.Values)
                if (q.Status == QuestState.Active)
                    yield return q;
        }

        // ── SAVE / LOAD ────────────────────────────────────────────────────────

        public JournalSaveData GetSaveData()
        {
            var data = new JournalSaveData();
            foreach (var kv in _quests)
                data.Quests.Add(new JournalQuestSave
                {
                    Tag    = kv.Value.QuestTag,
                    State  = kv.Value.CurrentState,
                    Status = kv.Value.Status.ToString()
                });
            return data;
        }

        public void RestoreFromSave(JournalSaveData data)
        {
            _quests.Clear();
            if (data?.Quests == null) return;
            foreach (var qs in data.Quests)
                AddEntry(qs.Tag, qs.State);
        }

        // ── TLK LOOKUP ────────────────────────────────────────────────────────
        //
        // KotOR journal.2da maps quest tags to per-state TLK StrRefs.
        // Format: questTag → row index; columns: "State_<stateId>" → strRef uint
        //
        // We cache the journal 2DA on first use. If it isn't loaded we fall back
        // to a human-readable placeholder so the UI still shows something useful.

        private static Data.TwoDATable _journalTable = null;
        private static bool _journalTableLoaded = false;

        private static string LookupQuestText(string questTag, int stateId)
        {
            // Try to load journal.2da once
            if (!_journalTableLoaded)
            {
                _journalTableLoaded = true;
                var rm = Bootstrap.SceneBootstrapper.Resources;
                if (rm != null)
                {
                    byte[] data = rm.GetResource("journal",
                        KotOR.FileReaders.ResourceType.TwoDA);
                    if (data != null)
                        _journalTable = Data.TwoDAReader.Load(data, "journal");
                }
                if (_journalTable == null)
                    Debug.LogWarning("[JournalSystem] journal.2da not found — using placeholder text.");
            }

            if (_journalTable != null)
            {
                // Find the row whose Tag column matches questTag (case-insensitive)
                int rowCount = _journalTable.RowCount;
                for (int row = 0; row < rowCount; row++)
                {
                    string tag = _journalTable.GetString(row, "Tag");
                    if (string.Equals(tag, questTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // Column name: "State_<stateId>" or just the state number as a string
                        string colName = $"State_{stateId}";
                        int strRef     = _journalTable.GetInt(row, colName, -1);

                        // Some tables use plain integer column names (0, 1, 2 …)
                        if (strRef < 0)
                            strRef = _journalTable.GetInt(row, stateId.ToString(), -1);

                        if (strRef >= 0)
                        {
                            string text = Bootstrap.SceneBootstrapper.GetString((uint)strRef);
                            if (!string.IsNullOrEmpty(text) && !text.StartsWith("<StrRef:"))
                                return text;
                        }
                        break;
                    }
                }
            }

            // Readable fallback — still better than a raw StrRef code
            return $"[{questTag}] Quest update (state {stateId}).";
        }
    }

    // ── SAVE DATA CLASSES ─────────────────────────────────────────────────────

    [Serializable]
    public class JournalSaveData
    {
        public List<JournalQuestSave> Quests = new List<JournalQuestSave>();
    }

    [Serializable]
    public class JournalQuestSave
    {
        public string Tag;
        public int    State;
        public string Status;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  JOURNAL UI
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Journal panel UI — toggled with J key (configurable).
    /// Lists active quests on the left, details on the right.
    /// </summary>
    public class JournalUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private KeyCode    _toggleKey = KeyCode.J;

        [Header("Quest List")]
        [SerializeField] private Transform  _questListContainer;
        [SerializeField] private UnityEngine.UI.Button _questEntryPrefab;

        [Header("Detail View")]
        [SerializeField] private TMPro.TextMeshProUGUI _titleText;
        [SerializeField] private TMPro.TextMeshProUGUI _statusText;
        [SerializeField] private TMPro.TextMeshProUGUI _descriptionText;

        private bool _isOpen = false;

        private void Start()
        {
            if (_panel != null) _panel.SetActive(false);

            if (JournalSystem.Instance != null)
            {
                JournalSystem.Instance.OnQuestUpdated   += _ => RefreshList();
                JournalSystem.Instance.OnQuestCompleted += _ => RefreshList();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey)) Toggle();
        }

        public void Toggle()
        {
            _isOpen = !_isOpen;
            if (_panel != null) _panel.SetActive(_isOpen);
            if (_isOpen) RefreshList();
        }

        private void RefreshList()
        {
            if (_questListContainer == null) return;
            foreach (Transform child in _questListContainer)
                Destroy(child.gameObject);

            if (JournalSystem.Instance == null) return;

            foreach (var quest in JournalSystem.Instance.GetActiveQuests())
            {
                var q = quest; // capture
                if (_questEntryPrefab == null) continue;
                var btn = Instantiate(_questEntryPrefab, _questListContainer);
                var lbl = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (lbl != null) lbl.text = quest.Title;
                btn.onClick.AddListener(() => ShowDetail(q));
            }
        }

        private void ShowDetail(QuestEntry quest)
        {
            if (_titleText       != null) _titleText      .text = quest.Title;
            if (_statusText      != null) _statusText     .text = quest.Status.ToString();
            if (_descriptionText != null) _descriptionText.text = quest.LastDescription;
        }
    }
}
