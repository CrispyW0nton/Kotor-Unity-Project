using System;
using System.Collections.Generic;
using UnityEngine;
using KotORUnity.Core;

namespace KotORUnity.UI
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  PAZAAK — KotOR's in-universe card game
    // ═══════════════════════════════════════════════════════════════════════════
    //
    //  Rules recap (authentic KotOR Pazaak):
    //    • Goal: reach exactly 20 or as close as possible without going over.
    //    • Each round the dealer draws a "main deck" card (1–10) face up.
    //    • The player may optionally play one side-deck card per turn (+/−, ±, Flip).
    //    • After a card is played the player can Stand (lock their score) or Draw again.
    //    • If a player's total exceeds 20 they BUST and lose the set.
    //    • Best of 3 sets wins the match. Wager is in credits.
    //
    //  This file implements the pure game logic (no UnityUI coupling).
    //  Attach PazaakManager to a GameObject in the scene; call StartMatch() to begin.
    //  The UI layer (PazaakUI) subscribes to the events exposed here.

    // ───────────────────────────────────────────────────────────────────────────
    //  CARD TYPES
    // ───────────────────────────────────────────────────────────────────────────

    public enum PazaakCardType
    {
        Plus,       // +N  (standard positive)
        Minus,      // −N  (standard negative)
        PlusMinus,  // ±N  (player chooses sign when played)
        Flip,       // Flips the sign of your CURRENT score (e.g. 14 → −14; capped to 0)
        Tiebreaker, // Special: forces a tie → wins the set on tie
        Double      // Doubles the value of the next main-deck card drawn
    }

    [Serializable]
    public class PazaakCard
    {
        public PazaakCardType CardType;
        public int            Value;     // magnitude (1–6 for side-deck)

        public PazaakCard(PazaakCardType type, int value = 0)
        {
            CardType = type;
            Value    = value;
        }

        /// <summary>Human-readable label for UI.</summary>
        public string Label => CardType switch
        {
            PazaakCardType.Plus      => $"+{Value}",
            PazaakCardType.Minus     => $"-{Value}",
            PazaakCardType.PlusMinus => $"±{Value}",
            PazaakCardType.Flip      => "FLIP",
            PazaakCardType.Tiebreaker=> "TIE",
            PazaakCardType.Double    => "×2",
            _                        => $"{Value}"
        };
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  PLAYER STATE FOR ONE MATCH
    // ───────────────────────────────────────────────────────────────────────────

    public class PazaakPlayerState
    {
        public string Name        { get; }
        public bool   IsHuman     { get; }

        // Score tracking
        public int    Score       { get; private set; } = 0;
        public int    SetsWon     { get; private set; } = 0;
        public bool   IsStanding  { get; private set; } = false;
        public bool   IsBusted    => Score > 20;
        public bool   IsAt20      => Score == 20;

        // Side deck (10 cards chosen before the match; hand = 4 drawn at start of match)
        public List<PazaakCard> SideDeck { get; } = new List<PazaakCard>();
        public List<PazaakCard> Hand     { get; } = new List<PazaakCard>();
        // Cards already played this SET (can only play 1 per turn)
        public bool PlayedSideCardThisTurn { get; set; } = false;

        // Double-card pending flag
        public bool DoubleNextDraw { get; set; } = false;

        public PazaakPlayerState(string name, bool isHuman)
        {
            Name    = name;
            IsHuman = isHuman;
        }

        public void AddToScore(int amount) => Score = Math.Max(0, Score + amount);
        public void FlipScore()            => Score = Math.Max(0, 20 - Score);
        public void ResetSet()
        {
            Score                 = 0;
            IsStanding            = false;
            PlayedSideCardThisTurn= false;
            DoubleNextDraw        = false;
        }
        public void Stand()          => IsStanding = true;
        public void AwardSet()       => SetsWon++;
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  MATCH STATE MACHINE
    // ───────────────────────────────────────────────────────────────────────────

    public enum PazaakPhase
    {
        Idle,           // before match starts
        PlayerTurn,     // player draws from main deck; may play side card; then stand/draw
        OpponentTurn,   // AI opponent takes their turn
        SetResolution,  // determine who won the set
        MatchOver       // best-of-3 complete
    }

    // ───────────────────────────────────────────────────────────────────────────
    //  PAZAAK MANAGER  (MonoBehaviour — attach to a Pazaak scene object)
    // ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Core Pazaak game logic.
    ///
    /// Usage:
    ///   PazaakManager.Instance.StartMatch(opponentName, wager, opponentDeck);
    ///   Subscribe to events:
    ///     OnCardDrawn(player, card, newScore)
    ///     OnSideCardPlayed(player, card, newScore)
    ///     OnPlayerStands(player)
    ///     OnSetOver(winner)   — winner null = tie
    ///     OnMatchOver(winner, creditsWon)
    ///   For human turn call: DrawMainCard(), PlaySideCard(index), Stand(), EndTurn()
    /// </summary>
    public class PazaakManager : MonoBehaviour
    {
        // ── SINGLETON ─────────────────────────────────────────────────────────
        public static PazaakManager Instance { get; private set; }

        // ── EVENTS (UI subscribes to these) ───────────────────────────────────
        public event Action<PazaakPlayerState, PazaakCard, int> OnCardDrawn;
        public event Action<PazaakPlayerState, PazaakCard, int> OnSideCardPlayed;
        public event Action<PazaakPlayerState>                   OnPlayerStands;
        public event Action<PazaakPlayerState>                   OnBust;
        public event Action<PazaakPlayerState>                   OnSetOver;   // null = tie
        public event Action<PazaakPlayerState, int>              OnMatchOver; // winner, credits

        // ── INSPECTOR ─────────────────────────────────────────────────────────
        [Tooltip("Sets required to win the match (KotOR default: 3)")]
        [SerializeField] private int setsToWin = 3;

        // ── STATE ─────────────────────────────────────────────────────────────
        public PazaakPhase            Phase    { get; private set; } = PazaakPhase.Idle;
        public PazaakPlayerState      Player   { get; private set; }
        public PazaakPlayerState      Opponent { get; private set; }
        public int                    Wager    { get; private set; }

        private readonly System.Random _rng = new System.Random();

        // ── UNITY ─────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── PUBLIC API ────────────────────────────────────────────────────────

        /// <summary>
        /// Start a new Pazaak match.
        /// playerDeck: the 10-card side-deck the player has built (4 are drawn into hand).
        /// opponentDeck: same for the AI opponent.
        /// wager: credits at stake (0 for no wager).
        /// </summary>
        public void StartMatch(string opponentName, int wager,
                               List<PazaakCard> playerDeck,
                               List<PazaakCard> opponentDeck = null)
        {
            Wager    = wager;
            Player   = new PazaakPlayerState("Player", isHuman: true);
            Opponent = new PazaakPlayerState(opponentName, isHuman: false);

            // Build decks
            BuildSideDeck(Player,   playerDeck   ?? DefaultSideDeck());
            BuildSideDeck(Opponent, opponentDeck ?? DefaultSideDeck());

            // Draw opening hands (4 cards each)
            DrawHand(Player);
            DrawHand(Opponent);

            StartNewSet();
        }

        /// <summary>Player draws the next main-deck card (called by UI).</summary>
        public void DrawMainCard()
        {
            if (Phase != PazaakPhase.PlayerTurn) return;
            if (Player.IsStanding)               return;

            int drawn = DrawFromMainDeck();
            if (Player.DoubleNextDraw) { drawn *= 2; Player.DoubleNextDraw = false; }

            var card = new PazaakCard(PazaakCardType.Plus, drawn);
            Player.AddToScore(drawn);
            Player.PlayedSideCardThisTurn = false; // reset for this turn
            OnCardDrawn?.Invoke(Player, card, Player.Score);
            Debug.Log($"[Pazaak] {Player.Name} draws {drawn}. Score = {Player.Score}");

            if (Player.IsBusted)
            {
                OnBust?.Invoke(Player);
                Debug.Log($"[Pazaak] {Player.Name} busts!");
                ResolveSet();
            }
            else if (Player.IsAt20)
            {
                Player.Stand();
                OnPlayerStands?.Invoke(Player);
                AdvanceTurn();
            }
            // else: player can play a side card and then choose Stand or draw again
        }

        /// <summary>Play a side-deck card from the player's hand. Index = hand position (0-3).</summary>
        public bool PlaySideCard(int handIndex, bool positiveSign = true)
        {
            if (Phase != PazaakPhase.PlayerTurn)       return false;
            if (Player.PlayedSideCardThisTurn)         return false;
            if (handIndex < 0 || handIndex >= Player.Hand.Count) return false;

            var card = Player.Hand[handIndex];
            int delta = ApplySideCard(Player, card, positiveSign);
            Player.PlayedSideCardThisTurn = true;
            OnSideCardPlayed?.Invoke(Player, card, Player.Score);
            Debug.Log($"[Pazaak] {Player.Name} plays {card.Label} (Δ{delta:+#;-#;0}). Score = {Player.Score}");

            // Remove from hand
            Player.Hand.RemoveAt(handIndex);

            if (Player.IsBusted)
            {
                OnBust?.Invoke(Player);
                ResolveSet();
            }
            else if (Player.IsAt20)
            {
                Player.Stand();
                OnPlayerStands?.Invoke(Player);
                AdvanceTurn();
            }
            return true;
        }

        /// <summary>Player chooses to Stand (lock their current score).</summary>
        public void Stand()
        {
            if (Phase != PazaakPhase.PlayerTurn) return;
            Player.Stand();
            OnPlayerStands?.Invoke(Player);
            Debug.Log($"[Pazaak] {Player.Name} stands at {Player.Score}.");
            AdvanceTurn();
        }

        /// <summary>
        /// Called after the player finishes choosing (stand or no side-card) to
        /// move to the next phase (opponent turn or set resolution).
        /// </summary>
        public void EndTurn()
        {
            if (Phase != PazaakPhase.PlayerTurn) return;
            AdvanceTurn();
        }

        // ── INTERNAL LOGIC ────────────────────────────────────────────────────

        private void StartNewSet()
        {
            Player.ResetSet();
            Opponent.ResetSet();
            Phase = PazaakPhase.PlayerTurn;
            Debug.Log($"[Pazaak] New set. Player sets: {Player.SetsWon} | Opp sets: {Opponent.SetsWon}");
        }

        private void AdvanceTurn()
        {
            // If both are standing (or one busted) → resolve
            if ((Player.IsStanding || Player.IsBusted) &&
                (Opponent.IsStanding || Opponent.IsBusted))
            {
                ResolveSet();
                return;
            }

            if (Phase == PazaakPhase.PlayerTurn)
            {
                Phase = PazaakPhase.OpponentTurn;
                RunOpponentTurn();
            }
            else
            {
                Phase = PazaakPhase.PlayerTurn;
            }
        }

        // ── OPPONENT AI ───────────────────────────────────────────────────────

        private void RunOpponentTurn()
        {
            if (Opponent.IsStanding) { AdvanceTurn(); return; }

            // Draw main card
            int drawn = DrawFromMainDeck();
            if (Opponent.DoubleNextDraw) { drawn *= 2; Opponent.DoubleNextDraw = false; }
            var card = new PazaakCard(PazaakCardType.Plus, drawn);
            Opponent.AddToScore(drawn);
            OnCardDrawn?.Invoke(Opponent, card, Opponent.Score);
            Debug.Log($"[Pazaak] {Opponent.Name} draws {drawn}. Score = {Opponent.Score}");

            if (Opponent.IsBusted)
            {
                OnBust?.Invoke(Opponent);
                ResolveSet();
                return;
            }

            // Simple AI: play a side card if it would bring score closer to 20
            if (!Opponent.PlayedSideCardThisTurn && Opponent.Hand.Count > 0)
            {
                int bestDelta = 0;
                int bestIdx   = -1;
                for (int i = 0; i < Opponent.Hand.Count; i++)
                {
                    var sc    = Opponent.Hand[i];
                    int trial = SimulateSideCard(Opponent.Score, sc, true);
                    int delta = Math.Abs(20 - trial) - Math.Abs(20 - Opponent.Score);
                    if (delta < bestDelta) { bestDelta = delta; bestIdx = i; }
                }
                if (bestIdx >= 0)
                {
                    var sc2   = Opponent.Hand[bestIdx];
                    int delta2 = ApplySideCard(Opponent, sc2, true);
                    Opponent.PlayedSideCardThisTurn = true;
                    OnSideCardPlayed?.Invoke(Opponent, sc2, Opponent.Score);
                    Debug.Log($"[Pazaak] {Opponent.Name} plays {sc2.Label} (Δ{delta2:+#;-#;0}). Score = {Opponent.Score}");
                    Opponent.Hand.RemoveAt(bestIdx);

                    if (Opponent.IsBusted) { OnBust?.Invoke(Opponent); ResolveSet(); return; }
                }
            }

            // Decide to stand: stand if ≥17 or if score equals or beats player's standing score
            bool opponentStands =
                Opponent.Score >= 20 ||
                Opponent.Score >= 17 ||
                (Player.IsStanding && Opponent.Score >= Player.Score);

            if (opponentStands)
            {
                Opponent.Stand();
                OnPlayerStands?.Invoke(Opponent);
                Debug.Log($"[Pazaak] {Opponent.Name} stands at {Opponent.Score}.");
            }

            AdvanceTurn();
        }

        // ── SET RESOLUTION ────────────────────────────────────────────────────

        private void ResolveSet()
        {
            Phase = PazaakPhase.SetResolution;

            PazaakPlayerState winner = null;
            if (Player.IsBusted && !Opponent.IsBusted)      winner = Opponent;
            else if (Opponent.IsBusted && !Player.IsBusted) winner = Player;
            else if (Player.Score > Opponent.Score)          winner = Player;
            else if (Opponent.Score > Player.Score)          winner = Opponent;
            // tie → no winner, both keep sets

            winner?.AwardSet();
            OnSetOver?.Invoke(winner);
            Debug.Log($"[Pazaak] Set over. Winner: {(winner?.Name ?? "Tie")}. " +
                      $"Player:{Player.SetsWon}  Opp:{Opponent.SetsWon}");

            // Check match end
            if (Player.SetsWon >= setsToWin || Opponent.SetsWon >= setsToWin)
            {
                EndMatch();
            }
            else
            {
                StartNewSet();
            }
        }

        private void EndMatch()
        {
            Phase = PazaakPhase.MatchOver;

            PazaakPlayerState matchWinner =
                Player.SetsWon >= setsToWin ? Player : Opponent;

            int credits = matchWinner == Player ? Wager : -Wager;
            Debug.Log($"[Pazaak] Match over! Winner: {matchWinner.Name}. Credits: {credits:+#;-#;0}");

            // Apply credit change to party table
            if (Party.PartyManager.Instance != null)
                Party.PartyManager.Instance.AddCredits(credits);

            OnMatchOver?.Invoke(matchWinner, credits);
            EventBus.Publish(EventBus.EventType.UIHUDRefresh, new EventBus.GameEventArgs());
        }

        // ── HELPERS ───────────────────────────────────────────────────────────

        /// <summary>Draw a value 1–10 from the infinite main deck.</summary>
        private int DrawFromMainDeck() => _rng.Next(1, 11);

        /// <summary>Apply a side card to a player state. Returns the score delta.</summary>
        private static int ApplySideCard(PazaakPlayerState state, PazaakCard card, bool positive)
        {
            int before = state.Score;
            switch (card.CardType)
            {
                case PazaakCardType.Plus:
                    state.AddToScore(+card.Value);
                    break;
                case PazaakCardType.Minus:
                    state.AddToScore(-card.Value);
                    break;
                case PazaakCardType.PlusMinus:
                    state.AddToScore(positive ? +card.Value : -card.Value);
                    break;
                case PazaakCardType.Flip:
                    state.FlipScore();
                    break;
                case PazaakCardType.Double:
                    state.DoubleNextDraw = true;
                    break;
                case PazaakCardType.Tiebreaker:
                    // Handled at set resolution — no immediate score change
                    break;
            }
            return state.Score - before;
        }

        /// <summary>Simulate result of playing a side card (no mutation).</summary>
        private static int SimulateSideCard(int score, PazaakCard card, bool positive)
        {
            return card.CardType switch
            {
                PazaakCardType.Plus      => score + card.Value,
                PazaakCardType.Minus     => score - card.Value,
                PazaakCardType.PlusMinus => positive ? score + card.Value : score - card.Value,
                PazaakCardType.Flip      => Math.Max(0, 20 - score),
                _                        => score
            };
        }

        private static void BuildSideDeck(PazaakPlayerState state, List<PazaakCard> deck)
        {
            state.SideDeck.Clear();
            state.SideDeck.AddRange(deck);
        }

        private void DrawHand(PazaakPlayerState state)
        {
            state.Hand.Clear();
            var pool = new List<PazaakCard>(state.SideDeck);
            Shuffle(pool);
            int count = Math.Min(4, pool.Count);
            for (int i = 0; i < count; i++)
                state.Hand.Add(pool[i]);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>A default 10-card side deck (mix of ±1 through ±6).</summary>
        public static List<PazaakCard> DefaultSideDeck()
        {
            return new List<PazaakCard>
            {
                new PazaakCard(PazaakCardType.Plus,      3),
                new PazaakCard(PazaakCardType.Plus,      4),
                new PazaakCard(PazaakCardType.Plus,      5),
                new PazaakCard(PazaakCardType.Plus,      6),
                new PazaakCard(PazaakCardType.Minus,     3),
                new PazaakCard(PazaakCardType.Minus,     4),
                new PazaakCard(PazaakCardType.PlusMinus, 2),
                new PazaakCard(PazaakCardType.PlusMinus, 3),
                new PazaakCard(PazaakCardType.Flip,      0),
                new PazaakCard(PazaakCardType.Plus,      2),
            };
        }
    }
}
