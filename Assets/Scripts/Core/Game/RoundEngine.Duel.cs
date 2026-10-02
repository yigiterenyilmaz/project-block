// PURPOSE: RoundEngine (partial) - a round as ONE SIDE OF A DUEL ("Blackjack", designer's call
// 2026-10-02). The duel deals each side half of the deck and both play their half out once; the
// side with more points takes the bet. This file holds the engine's half of that:
//
//   - THE RULES OF A DUEL SIDE (PlaysOutItsCards): the draw pile is never refilled from the
//     discard, a hand that cannot be topped up is simply shorter, and running out of moves ends
//     nothing by itself (RoundEngine.Bookkeeping asks the boss instead). The player's round gets
//     them from its boss (BossRound.PlaysOutCards); the computer's round is a session-less engine
//     seated at the table (duelSeat), because it has no boss of its own.
//   - A HELD ROUND (PlayIsHeld): between hands nothing may be placed until the bet is in.
//   - A NEW HAND (BeginDuelHand): a fresh arena and a fresh pile, dealt by the duel.
//
// Everything that decides WHO plays WHAT, and what a hand is worth, is the boss's
// (BlackjackBoss) and the planner's (Core/Duel). The engine only plays by the table's rules.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>True for the computer's side of a duel: a session-less round seated at the
        /// table, with no boss to say so.</summary>
        private bool duelSeat;

        /// <summary>True while this round plays a dealt half out once (see the file header).</summary>
        public bool PlaysOutItsCards
        {
            get { return duelSeat || (Boss != null && Boss.PlaysOutCards); }
        }

        /// <summary>True while nothing may be placed - the boss is waiting for a decision.</summary>
        public bool PlayIsHeld
        {
            get { return Boss != null && Boss.HoldsPlay; }
        }

        /// <summary>True while anything held can still be placed somewhere. The duel asks it at
        /// the end of every turn to know whether the player's hand is over.</summary>
        internal bool HasMoveLeft
        {
            get { return HasAnyPlayableMove(); }
        }

        /// <summary>
        /// The computer's side of a duel: a round with no session, no jokers and no boss, playing
        /// <paramref name="cards"/> in the order given (the LAST is the top of its pile). It
        /// prices everything with <paramref name="scorer"/> - the player's own - so both sides
        /// are paid by the same list. Its board is the one <paramref name="config"/> builds.
        /// </summary>
        internal static RoundEngine CreateDuelSide(RoundConfig config, IReadOnlyList<BlockCard> cards,
            IScoreCalculator scorer)
        {
            var deck = new RoundDeck(NullRandom.Instance);
            deck.ResetTo(cards);
            var side = new RoundEngine(config, new RoundRules(), NullRandom.Instance, scorer, null,
                null, new GameBoard(config.BoardWidth, config.BoardHeight, config.ExtraPlayableCells,
                    config.OptionalPlayableCells), deck);
            side.duelSeat = true;
            side.RefillHand();
            side.ResyncSnapshot();
            side.CaptureTurnStartCardCounts();
            return side;
        }

        /// <summary>Marks a loaded round as the computer's side (the flag is not in a round's
        /// own save; the duel writes it beside it).</summary>
        internal void SeatAtDuelTable()
        {
            duelSeat = true;
        }

        /// <summary>
        /// Takes every card out of the round - the hand and all three piles - and leaves the
        /// board as it stands. The duel does this when a round starts (it deals the cards itself)
        /// and when a hand ends (what the player could not place is collected). Nothing is told
        /// a card was lost: they are not lost, they are going back into the duel's deck.
        /// </summary>
        internal void SetAsideForDuel()
        {
            while (Hand.Count > 0)
            {
                Hand.RemoveAt(Hand.Count - 1);
            }
            Deck.ResetTo(null);
        }

        /// <summary>
        /// A new hand of the duel: a FRESH arena (the round's own, as its config builds it) and
        /// <paramref name="cards"/> as the draw pile, in the order given, and a hand drawn from it.
        /// Everything that remembered the last hand's board is dropped with it - the rewind
        /// history, the dynamite and targeted blocks, the welds, rotations and fox forms - and the
        /// combo starts again. The jokers are told the arena changed shape.
        /// </summary>
        internal void BeginDuelHand(IReadOnlyList<BlockCard> cards)
        {
            SetAsideForDuel();
            Deck.ResetTo(cards);
            var fresh = new GameBoard(Config.BoardWidth, Config.BoardHeight,
                Config.ExtraPlayableCells, Config.OptionalPlayableCells);
            fresh.IgnoreElements = ElementsIgnored;
            mainBoard = fresh;
            boardHistory.Clear();
            dynamiteBlocks.Clear();
            armedTargetCards.Clear();
            weldedOnBoard.Clear();
            cardPlacedSize.Clear();
            rotations.Clear();
            foxShapes.Clear();
            frozenCards.Clear();
            comboCount = 0;
            comboBlankTurns = 0;
            drawPileReportedEmpty = false;
            RefillHand();
            ResyncSnapshot();
            CaptureTurnStartCardCounts();
            NoteBoardReshaped();
        }
    }
}
