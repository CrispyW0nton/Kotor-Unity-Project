using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity;                        // WavDecoder (root namespace)
using KotORUnity.Bootstrap;
using KotORUnity.KotOR.Parsers;
using KotORUnity.KotOR.FileReaders;
using KotORUnity.Audio;
using GffStruct = KotORUnity.KotOR.Parsers.GffReader.GffStruct;

namespace KotORUnity.Dialogue
{
    /// <summary>
    /// Parses a KotOR DLG (dialogue) GFF and provides the runtime state-machine
    /// that drives NPC conversations.
    ///
    /// DLG GFF structure (simplified):
    ///   Root struct:
    ///     "StartingList"    — list of entry indices (with conditional scripts)
    ///     "EntryList"       — NPC lines (has RepliesList with CondScript + index)
    ///     "ReplyList"       — PC options (has EntriesList)
    ///     "OnEndConversation" — script to run when finished
    ///
    /// Usage:
    ///   var dlg = new DialogueTree();
    ///   dlg.Load(dlgBytes);
    ///   dlg.Start();                   // sets CurrentEntry to first valid entry
    ///   dlg.ChooseReply(replyIndex);   // advance to next NPC line
    ///   dlg.IsFinished                 // true when no more entries
    /// </summary>
    public class DialogueTree
    {
        // ── DATA MODEL ────────────────────────────────────────────────────────
        public class DialogueEntry
        {
            public int      Index;
            public string   Speaker;        // tag of speaking creature
            public uint     TextStrRef;     // index into dialog.tlk
            public string   Text;           // resolved from TLK
            public string   VoiceOver;      // sound resref
            public string   AnimId;         // animation override
            public string   Script;         // OnCutsceneSpeech script
            public List<DialogueLink> Replies = new List<DialogueLink>();
        }

        public class DialogueReply
        {
            public int      Index;
            public uint     TextStrRef;
            public string   Text;
            public string   Script;         // fired when this reply is chosen
            public bool     IsEnd;          // special end-of-convo node
            public List<DialogueLink> Entries = new List<DialogueLink>();
        }

        public class DialogueLink
        {
            public int    TargetIndex;
            public string CondScript;      // empty = always active
            public bool   IsActive = true;
        }

        // ── FIELDS ────────────────────────────────────────────────────────────
        private readonly List<DialogueEntry> _entries = new List<DialogueEntry>();
        private readonly List<DialogueReply> _replies = new List<DialogueReply>();
        private readonly List<DialogueLink>  _startList = new List<DialogueLink>();

        private string _endScript = "";
        private string _resRef    = "";
        private GameObject _speaker;     // NPC driving this conversation (OBJECT_SELF for NPC scripts)

        public bool IsLoaded  { get; private set; }
        public bool IsStarted { get; private set; }
        public bool IsFinished { get; private set; }

        /// <summary>The NPC line currently being displayed.</summary>
        public DialogueEntry CurrentEntry { get; private set; }

        /// <summary>PC reply options for the current entry.</summary>
        public IReadOnlyList<DialogueReply> CurrentReplies { get; private set; }
            = new List<DialogueReply>();

        // ── LOAD ──────────────────────────────────────────────────────────────
        public bool Load(byte[] dlgData, string resref = "", GameObject speaker = null)
        {
            _resRef  = resref;
            _speaker = speaker;
            _entries.Clear();
            _replies.Clear();
            _startList.Clear();

            var root = GffReader.Parse(dlgData);
            if (root == null)
            {
                Debug.LogError($"[DialogueTree] Failed to parse DLG '{resref}'.");
                return false;
            }

            _endScript = GffReader.GetString(root, "EndConversation");

            // ── Parse entry list ──────────────────────────────────────────────
            var entryList = root.GetField("EntryList")?.AsList();
            if (entryList != null)
            {
                for (int i = 0; i < entryList.Count; i++)
                {
                    var e = ParseEntry(entryList[i], i);
                    _entries.Add(e);
                }
            }

            // ── Parse reply list ──────────────────────────────────────────────
            var replyList = root.GetField("ReplyList")?.AsList();
            if (replyList != null)
            {
                for (int i = 0; i < replyList.Count; i++)
                {
                    var r = ParseReply(replyList[i], i);
                    _replies.Add(r);
                }
            }

            // ── Parse starting list ───────────────────────────────────────────
            var startList = root.GetField("StartingList")?.AsList();
            if (startList != null)
            {
                foreach (var s in startList)
                {
                    int   idx   = GffReader.GetInt(s, "Index", 0);
                    string cond = GffReader.GetString(s, "Active");
                    _startList.Add(new DialogueLink { TargetIndex = idx, CondScript = cond });
                }
            }

            IsLoaded = true;
            Debug.Log($"[DialogueTree] Loaded '{resref}': {_entries.Count} entries, " +
                      $"{_replies.Count} replies, {_startList.Count} starts.");
            return true;
        }

        // ── CONVERSATION STATE MACHINE ─────────────────────────────────────────
        /// <summary>Begin the conversation. Sets CurrentEntry to the first valid NPC line.</summary>
        public void Start()
        {
            if (!IsLoaded) return;

            IsStarted  = true;
            IsFinished = false;
            CurrentEntry = null;

            // Find first active entry from StartingList
            foreach (var link in _startList)
            {
                if (!EvaluateCond(link.CondScript)) continue;
                if (link.TargetIndex >= 0 && link.TargetIndex < _entries.Count)
                {
                    SetEntry(_entries[link.TargetIndex]);
                    return;
                }
            }

            // Nothing valid
            Finish();
        }

        /// <summary>Player chooses a reply (by index in CurrentReplies).</summary>
        public void ChooseReply(int replyListIndex)
        {
            if (IsFinished || replyListIndex < 0 ||
                replyListIndex >= CurrentReplies.Count) return;

            var reply = CurrentReplies[replyListIndex];

            // Fire the reply script with Player as OBJECT_SELF
            if (!string.IsNullOrEmpty(reply.Script))
            {
                var playerGO = UnityEngine.GameObject.FindGameObjectWithTag("Player");
                FireScript(reply.Script, playerGO);
            }

            if (reply.IsEnd || reply.Entries.Count == 0)
            {
                Finish();
                return;
            }

            // Advance to next NPC entry — evaluate conditions with speaker as OBJECT_SELF
            foreach (var link in reply.Entries)
            {
                bool active = string.IsNullOrEmpty(link.CondScript)
                    || Scripting.NWScriptVM.RunCondition(link.CondScript, _speaker);
                if (!active) continue;
                if (link.TargetIndex >= 0 && link.TargetIndex < _entries.Count)
                {
                    SetEntry(_entries[link.TargetIndex]);
                    return;
                }
            }

            Finish();
        }

        // ── INTERNAL HELPERS ──────────────────────────────────────────────────
        private void SetEntry(DialogueEntry entry)
        {
            CurrentEntry = entry;

            // Fire entry script with speaker as OBJECT_SELF
            if (!string.IsNullOrEmpty(entry.Script))
                FireScript(entry.Script, _speaker);

            // Gather valid replies — evaluate conditions with Player as OBJECT_SELF
            var playerGO = UnityEngine.GameObject.FindGameObjectWithTag("Player");
            var validReplies = new List<DialogueReply>();
            foreach (var link in entry.Replies)
            {
                // Condition scripts run with OBJECT_SELF = Player (PC)
                bool active = string.IsNullOrEmpty(link.CondScript)
                    || Scripting.NWScriptVM.RunCondition(link.CondScript, playerGO);
                if (!active) continue;
                if (link.TargetIndex >= 0 && link.TargetIndex < _replies.Count)
                    validReplies.Add(_replies[link.TargetIndex]);
            }

            CurrentReplies = validReplies;

            // If only one "continue" reply or none, auto-select or finish
            if (validReplies.Count == 0) Finish();
        }

        private void Finish()
        {
            IsFinished = true;
            if (!string.IsNullOrEmpty(_endScript))
                FireScript(_endScript, _speaker);

            Core.EventBus.Publish(Core.EventBus.EventType.DialogueEnded,
                new Core.EventBus.GameEventArgs());
        }

        private bool EvaluateCond(string script)
        {
            if (string.IsNullOrEmpty(script)) return true;
            return Scripting.NWScriptVM.RunCondition(script, _speaker);
        }

        private void FireScript(string script, GameObject target)
        {
            if (string.IsNullOrEmpty(script)) return;
            Scripting.NWScriptVM.Run(script, target ?? _speaker);
        }

        // ── GFF PARSERS ───────────────────────────────────────────────────────
        private DialogueEntry ParseEntry(GffStruct s, int index)
        {
            uint strref = (uint)GffReader.GetInt(s, "Text", -1);
            var entry = new DialogueEntry
            {
                Index      = index,
                Speaker    = GffReader.GetString(s, "Speaker"),
                TextStrRef = strref,
                Text       = SceneBootstrapper.GetString(strref),
                VoiceOver  = GffReader.GetString(s, "VO_ResRef"),
                AnimId     = GffReader.GetString(s, "AnimationID"),
                Script     = GffReader.GetString(s, "Script")
            };

            var replyLinks = s.GetField("RepliesList")?.AsList();
            if (replyLinks != null)
                foreach (var r in replyLinks)
                    entry.Replies.Add(new DialogueLink
                    {
                        TargetIndex = GffReader.GetInt(r, "Index", 0),
                        CondScript  = GffReader.GetString(r, "Active")
                    });

            return entry;
        }

        private DialogueReply ParseReply(GffStruct s, int index)
        {
            uint strref = (uint)GffReader.GetInt(s, "Text", -1);
            var reply = new DialogueReply
            {
                Index      = index,
                TextStrRef = strref,
                Text       = SceneBootstrapper.GetString(strref),
                Script     = GffReader.GetString(s, "Script"),
                IsEnd      = GffReader.GetInt(s, "IsChild", 0) == 0 &&
                             (strref == 0xFFFFFFFF || strref == 0)
            };

            var entryLinks = s.GetField("EntriesList")?.AsList();
            if (entryLinks != null)
                foreach (var e in entryLinks)
                    reply.Entries.Add(new DialogueLink
                    {
                        TargetIndex = GffReader.GetInt(e, "Index", 0),
                        CondScript  = GffReader.GetString(e, "Active")
                    });

            return reply;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE MANAGER  —  MonoBehaviour that coordinates the UI and audio
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Manages starting / running / closing conversations.
    /// Attach to a persistent "DialogueManager" GameObject.
    ///
    /// Wire up:
    ///   dialogueUI  → your Canvas-based DialogueUI component
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static DialogueManager Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [SerializeField] private DialogueUI dialogueUI;

        // ── RUNTIME STATE ─────────────────────────────────────────────────────
        private DialogueTree     _currentTree;
        private GameObject       _speaker;
        private AudioSource      _voiceSource;

        public bool IsInDialogue => _currentTree != null && !_currentTree.IsFinished;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            // Detach from parent so DontDestroyOnLoad works on nested GOs
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _voiceSource = gameObject.AddComponent<AudioSource>();
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────
        /// <summary>Start a conversation from a DLG resref.</summary>
        public void StartDialogue(string dlgResRef, GameObject speaker = null)
        {
            if (IsInDialogue)
            {
                Debug.LogWarning("[DialogueManager] Already in a dialogue.");
                return;
            }

            byte[] dlgData = SceneBootstrapper.Resources?.GetResource(dlgResRef, ResourceType.DLG);
            if (dlgData == null)
            {
                Debug.LogWarning($"[DialogueManager] DLG not found: '{dlgResRef}'.");
                return;
            }

            _speaker = speaker;
            _currentTree = new DialogueTree();
            if (!_currentTree.Load(dlgData, dlgResRef, speaker))
            {
                _currentTree = null;
                return;
            }

            _currentTree.Start();

            // Pause world time
            Core.EventBus.Publish(Core.EventBus.EventType.DialogueStarted,
                new Core.EventBus.GameEventArgs());

            ShowCurrentEntry();
        }

        /// <summary>Player selected a reply option.</summary>
        public void SelectReply(int replyIndex)
        {
            if (!IsInDialogue) return;

            _currentTree.ChooseReply(replyIndex);

            if (_currentTree.IsFinished)
            {
                EndDialogue();
            }
            else
            {
                ShowCurrentEntry();
            }
        }

        public void EndDialogue()
        {
            _currentTree  = null;
            _speaker      = null;

            if (_voiceSource.isPlaying) _voiceSource.Stop();

            dialogueUI?.HideDialogue();

            // Resume world
            Core.EventBus.Publish(Core.EventBus.EventType.DialogueEnded,
                new Core.EventBus.GameEventArgs());
        }

        // ── INTERNAL ─────────────────────────────────────────────────────────
        private void ShowCurrentEntry()
        {
            var entry = _currentTree.CurrentEntry;
            if (entry == null) { EndDialogue(); return; }

            // Play VO if available
            PlayVO(entry.VoiceOver, entry.Text);

            dialogueUI?.ShowEntry(entry, _currentTree.CurrentReplies);
        }

        private void PlayVO(string voResRef, string fallbackText)
        {
            if (string.IsNullOrEmpty(voResRef)) return;

            // Prefer AudioManager if available (handles caching, lip-sync, volume)
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayVOByResRef(voResRef, _speaker);
                return;
            }

            // Fallback: direct decode + play on local AudioSource
            byte[] wavData = SceneBootstrapper.Resources?.GetResource(voResRef, ResourceType.WAV);
            if (wavData == null) return;
            var clip = WavDecoder.Decode(wavData, voResRef);
            if (clip != null)
            {
                _voiceSource.clip = clip;
                _voiceSource.Play();

                // Also trigger lip-sync if LipSyncSystem is present
                LipSyncSystem.Instance?.PlayLip(voResRef, _voiceSource, _speaker);
            }
        }
    }
}
