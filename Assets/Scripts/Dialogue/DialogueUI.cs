using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using KotORUnity.Dialogue;
using KotORUnity.Core;
using KotORUnity.Audio;
#pragma warning disable 0414, 0219

namespace KotORUnity.Dialogue
{
    // ══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE CAMERA CONTROLLER  —  positions camera for conversations
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Moves the dialogue camera to "over-the-shoulder" angles for speaker
    /// and listener during conversations, mimicking KotOR's cutscene style.
    /// Attach to the same GameObject as DialogueUI, or reference from it.
    /// </summary>
    public class DialogueCameraController : MonoBehaviour
    {
        [Header("Camera Reference")]
        [SerializeField] private UnityEngine.Camera _dialogueCam;

        [Header("Shot Settings")]
        [SerializeField] private float _shoulderOffsetX  = 0.4f;
        [SerializeField] private float _shoulderOffsetY  = 1.6f;   // head height
        [SerializeField] private float _cameraDistance   = 1.8f;
        [SerializeField] private float _transitionSpeed  = 4f;
        [SerializeField] private float _dialogueFOV      = 55f;

        // ── STATE ─────────────────────────────────────────────────────────────
        private Transform _speakerTransform;
        private Transform _listenerTransform;
        private bool _focusOnSpeaker = true;
        private Vector3 _targetPosition;
        private Quaternion _targetRotation;
        private Coroutine _shotRoutine;

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void LateUpdate()
        {
            if (_dialogueCam == null || _speakerTransform == null) return;

            _dialogueCam.transform.position = Vector3.Lerp(
                _dialogueCam.transform.position, _targetPosition,
                _transitionSpeed * Time.deltaTime);
            _dialogueCam.transform.rotation = Quaternion.Slerp(
                _dialogueCam.transform.rotation, _targetRotation,
                _transitionSpeed * Time.deltaTime);
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Set up the dialogue camera for this conversation.</summary>
        public void BeginConversation(Transform speaker, Transform listener)
        {
            _speakerTransform  = speaker;
            _listenerTransform = listener;
            _focusOnSpeaker    = false;

            if (_dialogueCam != null)
            {
                _dialogueCam.enabled = true;
                _dialogueCam.fieldOfView = _dialogueFOV;

                // Disable game cameras so this one takes over
                var mainCam = UnityEngine.Camera.main;
                if (mainCam != null && mainCam != _dialogueCam)
                    mainCam.enabled = false;
            }

            CutToSpeaker();
        }

        public void EndConversation()
        {
            if (_dialogueCam != null) _dialogueCam.enabled = false;

            // Re-enable main game camera
            var mainCam = UnityEngine.Camera.main;
            if (mainCam != null) mainCam.enabled = true;

            _speakerTransform  = null;
            _listenerTransform = null;
        }

        /// <summary>Cut to an over-the-shoulder shot looking at the speaker.</summary>
        public void CutToSpeaker()
        {
            if (_speakerTransform == null) return;

            // Camera behind listener, looking at speaker
            Transform camFrom = _listenerTransform ?? _speakerTransform;
            Vector3 lookAt    = _speakerTransform.position + Vector3.up * _shoulderOffsetY;
            Vector3 camPos    = camFrom.position
                + camFrom.right * _shoulderOffsetX
                + Vector3.up * _shoulderOffsetY
                - camFrom.forward * _cameraDistance;

            _targetPosition = camPos;
            _targetRotation = Quaternion.LookRotation(lookAt - camPos);
        }

        /// <summary>Cut to an over-the-shoulder shot looking at the listener (player's reply).</summary>
        public void CutToListener()
        {
            if (_listenerTransform == null) return;

            Transform camFrom = _speakerTransform;
            Vector3 lookAt    = _listenerTransform.position + Vector3.up * _shoulderOffsetY;
            Vector3 camPos    = camFrom.position
                - camFrom.right * _shoulderOffsetX
                + Vector3.up * _shoulderOffsetY
                - camFrom.forward * _cameraDistance;

            _targetPosition = camPos;
            _targetRotation = Quaternion.LookRotation(lookAt - camPos);
        }

        /// <summary>Wide establishing shot framing both characters.</summary>
        public void CutToWideShot()
        {
            if (_speakerTransform == null) return;

            Vector3 center = _speakerTransform.position;
            if (_listenerTransform != null)
                center = (_speakerTransform.position + _listenerTransform.position) * 0.5f;

            Vector3 camPos = center + new Vector3(0f, 2.5f, -4f);

            _targetPosition = camPos;
            _targetRotation = Quaternion.LookRotation(
                (center + Vector3.up * 1f) - camPos);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BARK SYSTEM  —  one-line VO barks without full dialogue UI
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Shows a floating bark (speech bubble) above a character.
    /// Used for combat taunts, ambient NPC chatter, and feedback barks.
    /// </summary>
    public class BarkSystem : MonoBehaviour
    {
        public static BarkSystem Instance { get; private set; }

        [Header("Bark Prefab")]
        [Tooltip("A World-Space Canvas with TextMeshProUGUI. If null a fallback is created.")]
        [SerializeField] private GameObject _barkBubblePrefab;
        [SerializeField] private float _defaultDuration = 3.5f;
        [SerializeField] private float _riseSpeed       = 0.3f;

        // Pool
        private readonly Queue<BarkInstance> _pool = new Queue<BarkInstance>();
        private readonly List<BarkInstance>  _active = new List<BarkInstance>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void Update()
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var b = _active[i];
                b.Timer -= Time.deltaTime;
                if (b.Root != null)
                    b.Root.transform.position += Vector3.up * _riseSpeed * Time.deltaTime;

                float alpha = Mathf.Clamp01(b.Timer / 0.5f); // fade out last 0.5s
                if (b.Cg != null) b.Cg.alpha = alpha;

                if (b.Timer <= 0f || b.Root == null)
                {
                    if (b.Root != null) b.Root.SetActive(false);
                    _pool.Enqueue(b);
                    _active.RemoveAt(i);
                }
            }
        }

        /// <summary>Show a bark above the given world-space anchor.</summary>
        public void Bark(string text, Transform anchor, float duration = -1f)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            BarkInstance inst = _pool.Count > 0 ? _pool.Dequeue() : CreateBarkInstance();

            float dur = duration > 0f ? duration : _defaultDuration;
            inst.Timer = dur;

            Vector3 worldPos = anchor != null
                ? anchor.position + Vector3.up * 2.2f
                : Vector3.zero;

            if (inst.Root != null)
            {
                inst.Root.SetActive(true);
                inst.Root.transform.position = worldPos;
            }
            if (inst.Text != null)  inst.Text.text = text;
            if (inst.Cg   != null)  inst.Cg.alpha  = 1f;

            _active.Add(inst);
        }

        private BarkInstance CreateBarkInstance()
        {
            GameObject root;
            if (_barkBubblePrefab != null)
            {
                root = Instantiate(_barkBubblePrefab);
            }
            else
            {
                // Minimal fallback
                root = new GameObject("Bark");
                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.transform.localScale = Vector3.one * 0.01f;
                var rt = root.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = new Vector2(200, 40);

                var textGO = new GameObject("T");
                textGO.transform.SetParent(root.transform, false);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 16;
                tmp.color    = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                var trt = textGO.GetComponent<RectTransform>();
                trt.anchorMin  = Vector2.zero;
                trt.anchorMax  = Vector2.one;
                trt.offsetMin  = trt.offsetMax = Vector2.zero;
            }

            var cg = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            // Billboard — always face camera
            root.AddComponent<BarkBillboard>();

            return new BarkInstance
            {
                Root = root,
                Text = root.GetComponentInChildren<TextMeshProUGUI>(),
                Cg   = cg,
                Timer = 0f
            };
        }

        private class BarkInstance
        {
            public GameObject           Root;
            public TextMeshProUGUI      Text;
            public CanvasGroup          Cg;
            public float                Timer;
        }
    }

    /// <summary>Simple billboard that makes a world-space canvas face the camera.</summary>
    public class BarkBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = UnityEngine.Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DIALOGUE UI  —  full conversation panel (rewritten with camera/typewriter)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Canvas-based dialogue UI for the KotOR-Unity port.
    ///
    /// Features:
    ///   • Typewriter text reveal with configurable speed
    ///   • Camera cuts via DialogueCameraController
    ///   • Speaker portrait + name
    ///   • Reply buttons with numbered keyboard shortcuts
    ///   • Skip (Space / click) to instant-reveal text
    ///   • Bark sub-system integration
    ///   • Fade in / fade out panel transitions
    ///
    /// Hierarchy expected:
    ///   DialoguePanel (GameObject)
    ///     PortraitImage       (Image – speaker portrait)
    ///     SpeakerName         (TextMeshProUGUI)
    ///     DialogueText        (TextMeshProUGUI)
    ///     ReplyContainer      (Transform – parent for reply buttons)
    ///     ContinueHint        (GameObject – "Press SPACE to continue")
    ///   ReplyButtonPrefab     (Button + TextMeshProUGUI)
    /// </summary>
    public class DialogueUI : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static DialogueUI Instance { get; private set; }

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Header("Panel")]
        [SerializeField] private GameObject  dialoguePanel;
        [SerializeField] private CanvasGroup panelCanvasGroup;
        [SerializeField] private float       panelFadeDuration = 0.25f;

        [Header("Speaker")]
        [SerializeField] private Image            portraitImage;
        [SerializeField] private TextMeshProUGUI  speakerNameText;
        [SerializeField] private Color            playerNameColor  = new Color(0.4f, 0.8f, 1f);
        [SerializeField] private Color            npcNameColor     = new Color(1f, 0.85f, 0.5f);

        [Header("Dialogue Text")]
        [SerializeField] private TextMeshProUGUI  dialogueBodyText;
        [SerializeField] private float            typewriterSpeed  = 40f; // chars/sec
        [SerializeField] private bool             skipTypewriter   = false;

        [Header("Reply Buttons")]
        [SerializeField] private Transform        replyContainer;
        [SerializeField] private GameObject       replyButtonPrefab;
        [SerializeField] private Color            replyNormalColor  = new Color(0.1f, 0.1f, 0.15f, 0.9f);
        [SerializeField] private Color            replyHoverColor   = new Color(0.2f, 0.4f, 0.7f, 0.95f);

        [Header("Continue")]
        [SerializeField] private Button           continueButton;
        [SerializeField] private GameObject       continueHint;    // "SPACE to continue"

        [Header("Camera")]
        [SerializeField] private DialogueCameraController dialogueCamera;

        [Header("Bark")]
        [SerializeField] private BarkSystem       barkSystem;

        // ── RUNTIME ───────────────────────────────────────────────────────────
        private readonly List<GameObject> _replyButtons = new List<GameObject>();
        private Coroutine _typewriterCoroutine;
        private Coroutine _fadeCoroutine;
        private bool _textFullyRevealed = false;
        private string _fullText = "";

        // ── UNITY LIFECYCLE ───────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;

            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            // Ensure panel starts hidden
            if (panelCanvasGroup == null && dialoguePanel != null)
                panelCanvasGroup = dialoguePanel.GetComponent<CanvasGroup>()
                                   ?? dialoguePanel.AddComponent<CanvasGroup>();

            HideDialogue();
        }

        private void Update()
        {
            // Space or Enter to skip/advance
            if (IsDialogueOpen() && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                if (!_textFullyRevealed)
                    SkipTypewriter();
                else
                    OnContinueClicked();
            }

            // Number keys 1–9 to select reply
            if (IsDialogueOpen() && _replyButtons.Count > 0)
            {
                for (int i = 0; i < _replyButtons.Count && i < 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        DialogueManager.Instance?.SelectReply(i);
                        return;
                    }
                }
            }
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>Display a dialogue entry with optional replies.</summary>
        public void ShowEntry(DialogueTree.DialogueEntry entry,
                              IReadOnlyList<DialogueTree.DialogueReply> replies)
        {
            // Fade in panel if not already visible
            if (dialoguePanel != null && !dialoguePanel.activeSelf)
            {
                dialoguePanel.SetActive(true);
                FadePanel(0f, 1f);
            }

            // Speaker name and color
            bool isPlayer = IsPlayerSpeaker(entry.Speaker);
            if (speakerNameText != null)
            {
                speakerNameText.text  = string.IsNullOrEmpty(entry.Speaker) ? "???" : entry.Speaker;
                speakerNameText.color = isPlayer ? playerNameColor : npcNameColor;
            }

            // Portrait — try to load sprite by speaker tag
            if (portraitImage != null)
            {
                var sprite = LoadPortrait(entry.Speaker);
                if (sprite != null)
                {
                    portraitImage.sprite  = sprite;
                    portraitImage.enabled = true;
                }
                else
                {
                    portraitImage.enabled = false;
                }
            }

            // Body text via typewriter
            _fullText = entry.Text ?? "";
            StartTypewriter(_fullText);

            // Camera cut
            if (dialogueCamera != null)
            {
                if (isPlayer) dialogueCamera.CutToListener();
                else          dialogueCamera.CutToSpeaker();
            }

            // Clear old replies — only reveal after typewriter completes
            ClearReplyButtons();
            if (continueHint != null)
                continueHint.SetActive(false);

            // Schedule reply display after text reveal
            StopCoroutine_Safe(ref _typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(
                TypewriterThenShowReplies(entry.Text ?? "", replies));
        }

        /// <summary>Immediately hide the dialogue panel.</summary>
        public void HideDialogue()
        {
            StopTypewriter();
            ClearReplyButtons();
            if (dialoguePanel != null)
            {
                FadePanel(panelCanvasGroup != null ? panelCanvasGroup.alpha : 1f, 0f,
                    onComplete: () => dialoguePanel.SetActive(false));
            }
            _textFullyRevealed = false;
        }

        /// <summary>Show a bark above the given anchor without opening the full panel.</summary>
        public void ShowBark(string text, Transform anchor, float duration = 3.5f)
        {
            if (barkSystem != null)
                barkSystem.Bark(text, anchor, duration);
            else
                BarkSystem.Instance?.Bark(text, anchor, duration);
        }

        public bool IsDialogueOpen() =>
            dialoguePanel != null && dialoguePanel.activeSelf;

        // ── TYPEWRITER ────────────────────────────────────────────────────────

        private void StartTypewriter(string text)
        {
            StopTypewriter();

            if (dialogueBodyText == null) return;

            if (skipTypewriter || string.IsNullOrEmpty(text))
            {
                dialogueBodyText.text = text;
                _textFullyRevealed    = true;
                return;
            }

            _textFullyRevealed = false;
            // Hide text initially using TMP visible characters
            dialogueBodyText.text = text;
            dialogueBodyText.maxVisibleCharacters = 0;
        }

        private IEnumerator TypewriterThenShowReplies(
            string text, IReadOnlyList<DialogueTree.DialogueReply> replies)
        {
            // Run typewriter
            if (!skipTypewriter && dialogueBodyText != null && !string.IsNullOrEmpty(text))
            {
                int totalChars = text.Length;
                float charsRevealed = 0f;

                while (Mathf.FloorToInt(charsRevealed) < totalChars)
                {
                    charsRevealed += typewriterSpeed * Time.deltaTime;
                    int visible = Mathf.Min(Mathf.FloorToInt(charsRevealed), totalChars);
                    dialogueBodyText.maxVisibleCharacters = visible;
                    yield return null;
                }

                dialogueBodyText.maxVisibleCharacters = totalChars;
            }

            _textFullyRevealed = true;
            BuildReplyButtons(replies);
        }

        private void SkipTypewriter()
        {
            StopTypewriter();
            if (dialogueBodyText != null)
            {
                dialogueBodyText.maxVisibleCharacters = int.MaxValue;
                dialogueBodyText.text = _fullText;
            }
            _textFullyRevealed = true;
        }

        private void StopTypewriter()
        {
            StopCoroutine_Safe(ref _typewriterCoroutine);
            _textFullyRevealed = true;
        }

        // ── REPLY BUTTONS ─────────────────────────────────────────────────────

        private void BuildReplyButtons(IReadOnlyList<DialogueTree.DialogueReply> replies)
        {
            ClearReplyButtons();

            if (replies == null || replies.Count == 0)
            {
                // No player replies — show continue hint
                if (continueButton  != null) continueButton.gameObject.SetActive(true);
                if (continueHint    != null) continueHint.SetActive(true);
                return;
            }

            if (continueButton != null) continueButton.gameObject.SetActive(false);
            if (continueHint   != null) continueHint.SetActive(false);

            for (int i = 0; i < replies.Count; i++)
            {
                var reply = replies[i];
                int capturedIndex = i;

                string label = (i < 9 ? $"{i + 1}. " : "   ") + reply.Text;

                GameObject btn = replyButtonPrefab != null
                    ? Instantiate(replyButtonPrefab, replyContainer)
                    : CreateFallbackButton(label, replyContainer);

                // Set label text
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = label;
                else
                {
                    var legacy = btn.GetComponentInChildren<Text>();
                    if (legacy != null) legacy.text = label;
                }

                // Bind click
                var button = btn.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() =>
                        DialogueManager.Instance?.SelectReply(capturedIndex));

                // Hover color via EventTrigger
                AddHoverColors(btn, replyNormalColor, replyHoverColor);

                _replyButtons.Add(btn);
            }
        }

        private void ClearReplyButtons()
        {
            foreach (var btn in _replyButtons)
                if (btn != null) Destroy(btn);
            _replyButtons.Clear();
        }

        // ── EVENT HANDLERS ────────────────────────────────────────────────────

        private void OnContinueClicked()
        {
            DialogueManager.Instance?.SelectReply(0);
        }

        // ── PANEL FADE ────────────────────────────────────────────────────────

        private void FadePanel(float from, float to, Action onComplete = null)
        {
            StopCoroutine_Safe(ref _fadeCoroutine);
            if (panelCanvasGroup == null) { onComplete?.Invoke(); return; }
            _fadeCoroutine = StartCoroutine(FadeRoutine(from, to, panelFadeDuration, onComplete));
        }

        private IEnumerator FadeRoutine(float from, float to, float dur, Action onComplete)
        {
            float elapsed = 0f;
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = from;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                if (panelCanvasGroup != null)
                    panelCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / dur);
                yield return null;
            }
            if (panelCanvasGroup != null) panelCanvasGroup.alpha = to;
            onComplete?.Invoke();
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        private static bool IsPlayerSpeaker(string speakerTag)
        {
            if (string.IsNullOrEmpty(speakerTag)) return false;
            string t = speakerTag.ToLowerInvariant();
            return t == "player" || t == "revan" || t == "pc";
        }

        private static Sprite LoadPortrait(string speakerTag)
        {
            if (string.IsNullOrEmpty(speakerTag)) return null;
            // Portraits expected at: Resources/Portraits/<speakerTag>
            return Resources.Load<Sprite>($"Portraits/{speakerTag}");
        }

        private static void AddHoverColors(GameObject btn, Color normal, Color hover)
        {
            var image = btn.GetComponent<Image>();
            if (image == null) return;
            image.color = normal;

            var trigger = btn.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener(_ => image.color = hover);
            trigger.triggers.Add(enterEntry);

            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener(_ => image.color = normal);
            trigger.triggers.Add(exitEntry);
        }

        private static GameObject CreateFallbackButton(string label, Transform parent)
        {
            var go = new GameObject("ReplyBtn");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(500, 44);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);

            go.AddComponent<Button>();

            var tgo = new GameObject("Label");
            tgo.transform.SetParent(go.transform, false);
            var txt = tgo.AddComponent<TextMeshProUGUI>();
            txt.text      = label;
            txt.fontSize  = 15;
            txt.color     = Color.white;
            txt.alignment = TextAlignmentOptions.MidlineLeft;

            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10, 2);
            trt.offsetMax = new Vector2(-10, -2);

            return go;
        }

        private void StopCoroutine_Safe(ref Coroutine c)
        {
            if (c != null) { StopCoroutine(c); c = null; }
        }
    }
}
