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
    //  PAZAAK UI
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Drives the entire Pazaak in-game HUD.
    //  Requires a PazaakManager in the same scene (auto-found on Awake).
    //
    //  Layout expected in the Unity Canvas hierarchy:
    //    [PazaakRoot]
    //      ├─ Header
    //      │    ├─ PlayerName (TMP)
    //      │    ├─ OpponentName (TMP)
    //      │    ├─ SetsPlayer (TMP)   "P: 0"
    //      │    └─ SetsOpponent (TMP) "O: 0"
    //      ├─ Scores
    //      │    ├─ PlayerScore (TMP)
    //      │    └─ OpponentScore (TMP)
    //      ├─ Board
    //      │    ├─ PlayerCardsContainer (layout group)
    //      │    └─ OpponentCardsContainer (layout group)
    //      ├─ PlayerHand (layout group — side-deck cards)
    //      │    └─ CardButtonPrefab × 4  (Button)
    //      ├─ Actions
    //      │    ├─ BtnDraw  (Button)
    //      │    ├─ BtnStand (Button)
    //      │    └─ BtnEndTurn (Button)
    //      ├─ StatusText (TMP) — "Your Turn", "Opponent's Turn", etc.
    //      ├─ WagerText  (TMP)
    //      ├─ PlusMinus Dialog (shown when player plays a ±N card)
    //      │    ├─ TxtPMCardLabel (TMP)
    //      │    ├─ BtnPlus   (Button)
    //      │    └─ BtnMinus  (Button)
    //      ├─ ResultOverlay
    //      │    ├─ ResultTitle   (TMP)
    //      │    ├─ ResultDetails (TMP)
    //      │    └─ BtnPlayAgain (Button)
    //      └─ BtnQuit (Button)

    /// <summary>
    /// Full Pazaak UI layer — subscribes to PazaakManager events and updates the display.
    /// </summary>
    public class PazaakUI : MonoBehaviour
    {
        // ── SINGLETON ──────────────────────────────────────────────────────────
        public static PazaakUI Instance { get; private set; }

        // ══════════════════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════════════════

        [Header("Root")]
        [SerializeField] private GameObject _rootPanel;

        [Header("Header Info")]
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private TextMeshProUGUI _opponentNameText;
        [SerializeField] private TextMeshProUGUI _setsPlayerText;
        [SerializeField] private TextMeshProUGUI _setsOpponentText;
        [SerializeField] private TextMeshProUGUI _wagerText;

        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI _playerScoreText;
        [SerializeField] private TextMeshProUGUI _opponentScoreText;
        [SerializeField] private Image           _playerScoreBg;     // tinted red on bust
        [SerializeField] private Image           _opponentScoreBg;

        [Header("Board (played cards)")]
        [SerializeField] private Transform  _playerCardsContainer;
        [SerializeField] private Transform  _opponentCardsContainer;
        [SerializeField] private GameObject _cardChipPrefab;   // small chip showing card value

        [Header("Player Hand (side-deck buttons)")]
        [SerializeField] private Transform  _handContainer;
        [SerializeField] private Button     _cardButtonPrefab;

        [Header("Action Buttons")]
        [SerializeField] private Button          _btnDraw;
        [SerializeField] private Button          _btnStand;
        [SerializeField] private Button          _btnEndTurn;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Plus/Minus Dialog")]
        [SerializeField] private GameObject      _pmDialog;
        [SerializeField] private TextMeshProUGUI _pmCardLabel;
        [SerializeField] private Button          _btnPMPlus;
        [SerializeField] private Button          _btnPMMinus;

        [Header("Result Overlay")]
        [SerializeField] private GameObject      _resultOverlay;
        [SerializeField] private TextMeshProUGUI _resultTitle;
        [SerializeField] private TextMeshProUGUI _resultDetails;
        [SerializeField] private Button          _btnPlayAgain;

        [Header("Misc")]
        [SerializeField] private Button          _btnQuit;
        [SerializeField] private float           _opponentTurnDelay = 1.2f;

        // ── COLORS ─────────────────────────────────────────────────────────────
        private static readonly Color _normalBg = new Color(0.15f, 0.15f, 0.20f, 1f);
        private static readonly Color _bustBg   = new Color(0.55f, 0.10f, 0.10f, 1f);
        private static readonly Color _standBg  = new Color(0.10f, 0.40f, 0.15f, 1f);
        private static readonly Color _at20Bg   = new Color(0.10f, 0.55f, 0.55f, 1f);

        // ── STATE ──────────────────────────────────────────────────────────────
        private PazaakManager        _mgr;
        private int                  _pendingPMIndex  = -1;   // side-card awaiting ±selection
        private List<PazaakCard>     _playerBoardCards   = new List<PazaakCard>();
        private List<PazaakCard>     _opponentBoardCards = new List<PazaakCard>();

        // ══════════════════════════════════════════════════════════════════════
        //  UNITY LIFECYCLE
        // ══════════════════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_rootPanel     != null) _rootPanel.SetActive(false);
            if (_resultOverlay != null) _resultOverlay.SetActive(false);
            if (_pmDialog      != null) _pmDialog.SetActive(false);
        }

        private void Start()
        {
            _mgr = PazaakManager.Instance ?? FindObjectOfType<PazaakManager>();
            if (_mgr == null)
            {
                Debug.LogWarning("[PazaakUI] No PazaakManager found in scene.");
                return;
            }
            SubscribeToManager();
            WireButtons();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnsubscribeFromManager();
        }

        // ══════════════════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Show the Pazaak UI and start a new match.
        /// playerDeck: optional custom side deck (defaults to PazaakManager default deck).
        /// wager: credits at stake.
        /// opponentName: NPC name shown in the header.
        /// </summary>
        public void StartMatch(string opponentName, int wager,
                               List<PazaakCard> playerDeck  = null,
                               List<PazaakCard> opponentDeck = null)
        {
            if (_mgr == null)
            {
                Debug.LogWarning("[PazaakUI] PazaakManager is null; cannot start match.");
                return;
            }

            _playerBoardCards.Clear();
            _opponentBoardCards.Clear();
            ClearBoardDisplay();

            if (_rootPanel     != null) _rootPanel.SetActive(true);
            if (_resultOverlay != null) _resultOverlay.SetActive(false);
            if (_pmDialog      != null) _pmDialog.SetActive(false);

            Time.timeScale = 0f;

            _mgr.StartMatch(opponentName, wager,
                playerDeck   ?? PazaakManager.DefaultSideDeck(),
                opponentDeck ?? PazaakManager.DefaultSideDeck());
        }

        /// <summary>Close the Pazaak UI and resume normal game time.</summary>
        public void Close()
        {
            if (_rootPanel != null) _rootPanel.SetActive(false);
            Time.timeScale = 1f;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  MANAGER EVENTS
        // ══════════════════════════════════════════════════════════════════════

        private void SubscribeToManager()
        {
            if (_mgr == null) return;
            _mgr.OnCardDrawn      += HandleCardDrawn;
            _mgr.OnSideCardPlayed += HandleSideCardPlayed;
            _mgr.OnPlayerStands  += HandleStand;
            _mgr.OnBust          += HandleBust;
            _mgr.OnSetOver       += HandleSetOver;
            _mgr.OnMatchOver     += HandleMatchOver;
        }

        private void UnsubscribeFromManager()
        {
            if (_mgr == null) return;
            _mgr.OnCardDrawn      -= HandleCardDrawn;
            _mgr.OnSideCardPlayed -= HandleSideCardPlayed;
            _mgr.OnPlayerStands  -= HandleStand;
            _mgr.OnBust          -= HandleBust;
            _mgr.OnSetOver       -= HandleSetOver;
            _mgr.OnMatchOver     -= HandleMatchOver;
        }

        private void HandleCardDrawn(PazaakPlayerState state, PazaakCard card, int newScore)
        {
            AddCardToBoard(state, card);
            RefreshScores();
            RefreshActionButtons();
            SetStatus(state.IsHuman ? "You drew a card." : $"{state.Name} draws.");
        }

        private void HandleSideCardPlayed(PazaakPlayerState state, PazaakCard card, int newScore)
        {
            RefreshScores();
            RefreshHand();
            SetStatus(state.IsHuman ? $"You played {card.Label}." : $"{state.Name} played {card.Label}.");
        }

        private void HandleStand(PazaakPlayerState state)
        {
            RefreshScores();
            RefreshActionButtons();
            SetStatus(state.IsHuman ? "You stand." : $"{state.Name} stands.");

            if (state.IsHuman && _playerScoreBg != null)
                _playerScoreBg.color = _standBg;
            else if (!state.IsHuman && _opponentScoreBg != null)
                _opponentScoreBg.color = _standBg;
        }

        private void HandleBust(PazaakPlayerState state)
        {
            RefreshScores();
            SetStatus(state.IsHuman ? "You bust!" : $"{state.Name} busts!");

            if (state.IsHuman && _playerScoreBg != null)
                _playerScoreBg.color = _bustBg;
            else if (!state.IsHuman && _opponentScoreBg != null)
                _opponentScoreBg.color = _bustBg;
        }

        private void HandleSetOver(PazaakPlayerState winner)
        {
            RefreshSets();
            string msg = winner == null
                ? "Set tied!"
                : (winner.IsHuman ? "You win the set!" : $"{winner.Name} wins the set.");
            SetStatus(msg);

            // Flash result briefly then reset board for new set
            StartCoroutine(NewSetDelay(1.8f));
        }

        private void HandleMatchOver(PazaakPlayerState winner, int creditsChange)
        {
            RefreshSets();
            ShowResult(winner, creditsChange);
            Time.timeScale = 1f;  // resume
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BUTTON WIRING
        // ══════════════════════════════════════════════════════════════════════

        private void WireButtons()
        {
            if (_btnDraw    != null) _btnDraw   .onClick.AddListener(OnDrawClicked);
            if (_btnStand   != null) _btnStand  .onClick.AddListener(OnStandClicked);
            if (_btnEndTurn != null) _btnEndTurn.onClick.AddListener(OnEndTurnClicked);

            if (_btnPMPlus  != null) _btnPMPlus .onClick.AddListener(() => ResolvePM(true));
            if (_btnPMMinus != null) _btnPMMinus.onClick.AddListener(() => ResolvePM(false));

            if (_btnPlayAgain != null) _btnPlayAgain.onClick.AddListener(OnPlayAgainClicked);
            if (_btnQuit      != null) _btnQuit     .onClick.AddListener(Close);
        }

        private void OnDrawClicked()
        {
            if (_mgr == null || _mgr.Phase != PazaakPhase.PlayerTurn) return;
            _mgr.DrawMainCard();
            RefreshActionButtons();
        }

        private void OnStandClicked()
        {
            if (_mgr == null || _mgr.Phase != PazaakPhase.PlayerTurn) return;
            _mgr.Stand();
            RefreshActionButtons();
        }

        private void OnEndTurnClicked()
        {
            if (_mgr == null || _mgr.Phase != PazaakPhase.PlayerTurn) return;
            _mgr.EndTurn();
            RefreshActionButtons();
        }

        private void OnPlayAgainClicked()
        {
            if (_resultOverlay != null) _resultOverlay.SetActive(false);
            // Re-start with the same wager and default decks
            int wager = _mgr?.Wager ?? 0;
            string oppName = _mgr?.Opponent?.Name ?? "Opponent";
            StartMatch(oppName, wager);
        }

        // ── HAND CARD BUTTONS ─────────────────────────────────────────────────

        private void BuildHandButtons()
        {
            if (_handContainer == null || _cardButtonPrefab == null) return;
            foreach (Transform child in _handContainer) Destroy(child.gameObject);

            if (_mgr?.Player?.Hand == null) return;

            for (int i = 0; i < _mgr.Player.Hand.Count; i++)
            {
                int idx  = i;
                var card = _mgr.Player.Hand[i];
                var btn  = Instantiate(_cardButtonPrefab, _handContainer);
                var lbl  = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null) lbl.text = card.Label;

                bool isPM = card.CardType == PazaakCardType.PlusMinus;
                btn.onClick.AddListener(() =>
                {
                    if (isPM)
                        OpenPMDialog(idx, card);
                    else
                        TryPlaySideCard(idx, true);
                });

                // Disable when not the player's turn
                btn.interactable = _mgr.Phase == PazaakPhase.PlayerTurn
                                   && !_mgr.Player.PlayedSideCardThisTurn;
            }
        }

        private void OpenPMDialog(int handIndex, PazaakCard card)
        {
            if (_pmDialog == null) { TryPlaySideCard(handIndex, true); return; }
            _pendingPMIndex = handIndex;
            if (_pmCardLabel != null) _pmCardLabel.text = $"Play {card.Label} as?";
            _pmDialog.SetActive(true);
        }

        private void ResolvePM(bool positive)
        {
            if (_pmDialog != null) _pmDialog.SetActive(false);
            if (_pendingPMIndex < 0) return;
            TryPlaySideCard(_pendingPMIndex, positive);
            _pendingPMIndex = -1;
        }

        private void TryPlaySideCard(int index, bool positive)
        {
            if (_mgr == null) return;
            bool played = _mgr.PlaySideCard(index, positive);
            if (played)
            {
                RefreshHand();
                RefreshActionButtons();
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  DISPLAY REFRESH
        // ══════════════════════════════════════════════════════════════════════

        private void RefreshAll()
        {
            if (_mgr == null) return;
            RefreshHeader();
            RefreshScores();
            RefreshSets();
            RefreshHand();
            RefreshActionButtons();
        }

        private void RefreshHeader()
        {
            if (_playerNameText   != null) _playerNameText.text   = _mgr?.Player?.Name   ?? "Player";
            if (_opponentNameText != null) _opponentNameText.text = _mgr?.Opponent?.Name ?? "Opponent";
            if (_wagerText        != null)
            {
                int w = _mgr?.Wager ?? 0;
                _wagerText.text = w > 0 ? $"Wager: {w:N0} cr" : "";
            }
        }

        private void RefreshScores()
        {
            if (_mgr == null) return;

            // Player score
            if (_playerScoreText != null)
            {
                _playerScoreText.text = _mgr.Player.Score.ToString();
                if (_playerScoreBg != null)
                {
                    if      (_mgr.Player.IsBusted) _playerScoreBg.color = _bustBg;
                    else if (_mgr.Player.IsAt20)   _playerScoreBg.color = _at20Bg;
                    else if (_mgr.Player.IsStanding) _playerScoreBg.color = _standBg;
                    else                           _playerScoreBg.color = _normalBg;
                }
            }

            // Opponent score
            if (_opponentScoreText != null)
            {
                // Only show if standing/busted (hide while opponent still drawing)
                bool reveal = _mgr.Opponent.IsStanding || _mgr.Opponent.IsBusted
                              || _mgr.Phase == PazaakPhase.SetResolution
                              || _mgr.Phase == PazaakPhase.MatchOver;
                _opponentScoreText.text = reveal ? _mgr.Opponent.Score.ToString() : "?";
                if (_opponentScoreBg != null)
                {
                    if      (!reveal)                    _opponentScoreBg.color = _normalBg;
                    else if (_mgr.Opponent.IsBusted)     _opponentScoreBg.color = _bustBg;
                    else if (_mgr.Opponent.IsAt20)       _opponentScoreBg.color = _at20Bg;
                    else if (_mgr.Opponent.IsStanding)   _opponentScoreBg.color = _standBg;
                    else                                 _opponentScoreBg.color = _normalBg;
                }
            }
        }

        private void RefreshSets()
        {
            if (_setsPlayerText   != null)
                _setsPlayerText.text   = $"Sets: {_mgr?.Player?.SetsWon ?? 0}";
            if (_setsOpponentText != null)
                _setsOpponentText.text = $"Sets: {_mgr?.Opponent?.SetsWon ?? 0}";
        }

        private void RefreshHand()
        {
            BuildHandButtons();
        }

        private void RefreshActionButtons()
        {
            if (_mgr == null) return;
            bool isPlayerTurn = _mgr.Phase == PazaakPhase.PlayerTurn;
            bool alreadyDrew  = !_mgr.Player.IsStanding && !_mgr.Player.IsBusted;

            // Draw is available if it's the player's turn and they haven't busted/stood
            if (_btnDraw != null)
                _btnDraw.interactable = isPlayerTurn && alreadyDrew && !_mgr.Player.IsStanding;

            // Stand always available on player turn if not busted
            if (_btnStand != null)
                _btnStand.interactable = isPlayerTurn && !_mgr.Player.IsBusted
                                         && !_mgr.Player.IsStanding;

            // End turn — always on player turn (even without playing a side card)
            if (_btnEndTurn != null)
                _btnEndTurn.interactable = isPlayerTurn;

            // Phase label
            string phase = _mgr.Phase switch
            {
                PazaakPhase.PlayerTurn   => "Your Turn",
                PazaakPhase.OpponentTurn => "Opponent's Turn",
                PazaakPhase.SetResolution => "Resolving Set...",
                PazaakPhase.MatchOver    => "Match Over",
                _                        => ""
            };
            SetStatus(phase);
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BOARD CARDS
        // ══════════════════════════════════════════════════════════════════════

        private void AddCardToBoard(PazaakPlayerState state, PazaakCard card)
        {
            bool isPlayer = state.IsHuman;
            var container = isPlayer ? _playerCardsContainer : _opponentCardsContainer;
            if (container == null || _cardChipPrefab == null) return;

            var chip = Instantiate(_cardChipPrefab, container);
            var lbl  = chip.GetComponentInChildren<TextMeshProUGUI>();
            if (lbl != null) lbl.text = card.CardType == PazaakCardType.Plus
                ? card.Value.ToString()
                : card.Label;

            if (isPlayer) _playerBoardCards.Add(card);
            else          _opponentBoardCards.Add(card);
        }

        private void ClearBoardDisplay()
        {
            if (_playerCardsContainer != null)
                foreach (Transform c in _playerCardsContainer) Destroy(c.gameObject);
            if (_opponentCardsContainer != null)
                foreach (Transform c in _opponentCardsContainer) Destroy(c.gameObject);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  RESULT OVERLAY
        // ══════════════════════════════════════════════════════════════════════

        private void ShowResult(PazaakPlayerState winner, int creditsChange)
        {
            if (_resultOverlay == null) return;
            _resultOverlay.SetActive(true);

            string title, details;
            if (winner != null && winner.IsHuman)
            {
                title   = "Victory!";
                details = creditsChange >= 0
                    ? $"You won {creditsChange:N0} credits!"
                    : "No wager was set.";
            }
            else if (winner != null)
            {
                title   = "Defeat";
                details = creditsChange < 0
                    ? $"You lost {-creditsChange:N0} credits."
                    : "No wager.";
            }
            else
            {
                title   = "Draw";
                details = "No credits exchanged.";
            }

            if (_resultTitle   != null) _resultTitle.text   = title;
            if (_resultDetails != null) _resultDetails.text = details;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  COROUTINES
        // ══════════════════════════════════════════════════════════════════════

        private IEnumerator NewSetDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            // Clear board for the new set
            _playerBoardCards.Clear();
            _opponentBoardCards.Clear();
            ClearBoardDisplay();

            RefreshAll();
        }
    }
}
