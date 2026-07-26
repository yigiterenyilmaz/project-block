// PURPOSE: "Şaşırtmaca" - the shell game. The hand is dealt FACE DOWN. Turning one card over
// commits you to it: the rest of the hand locks behind it, so you play the card you picked
// blind, wherever it happens to fit. When the turn is over the hand is shown to you, topped up,
// and mixed face down again - so you always know WHAT you are holding and never WHERE.
//
// TWO PIECES, and the split matters:
//  - the face-down deal is a fact about the SCREEN (HidesHandCards), exactly like
//    "Alacakaranlık" darkening the board. No rule in the engine depends on it.
//  - the LOCK is the real rule (LocksHandCard), asked live by the engine at the two places
//    that matter: playing a card, and counting a card as a way out of a dead end.
//
// THE COMMITMENT CANNOT KILL YOU. A boss must never lock a round with its own restriction, so
// the moment the player turns a card over the engine re-runs its dead-end check, and if the card
// they committed to fits nowhere, TryEscapeDeadEnd lifts the lock and they may pick again. They
// have still learned what that card was, which is the price of the mistake.
//
// Nothing here is randomness of its own: the mix uses the round's rng through the engine, so a
// replay of the same seed deals the same shells in the same order.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>"Şaşırtmaca" - a face-down hand, and one commitment per turn.</summary>
    public sealed class SasirtmacaBoss : BossRound
    {
        /// <summary>The card the player has turned over this turn, or 0 while the whole hand is
        /// still face down. Everything else in the hand is locked while this is set.</summary>
        private int revealedCardId;

        /// <summary>The hand as it stood BEFORE the last mix, newest turn only. The View shows
        /// these face up for a beat after a turn - "here is what you were holding" - while the
        /// real hand has already been mixed behind them.</summary>
        private readonly List<int> handBeforeMix = new List<int>();

        private int cardsPlayedBlind;

        public SasirtmacaBoss()
            : base("sasirtmaca", "Şaşırtmaca")
        {
            SetDescription(
                "Your hand is dealt face down. Turning one card over locks the rest - you play "
                    + "the card you picked blind. Afterwards the hand is shown to you, topped up, "
                    + "and mixed face down again.",
                "Elin üstü kapalı dağıtılır. Bir kartı çevirmek diğerlerini kilitler - körlemesine "
                    + "seçtiğin kartı oynarsın. Sonrasında el sana gösterilir, kart çekilir ve "
                    + "kartlar üstü kapalı yeniden karılır.");
        }

        /// <summary>The card turned over this turn, or 0. The View draws this one face up.</summary>
        public int RevealedCardId
        {
            get { return revealedCardId; }
        }

        /// <summary>The hand as it stood before the last mix, for the View's reveal beat.</summary>
        public IReadOnlyList<int> HandBeforeMix
        {
            get { return handBeforeMix; }
        }

        public override string StatusText
        {
            get
            {
                return revealedCardId != 0
                    ? Loc.Pick("committed", "karar verildi")
                    : Loc.Pick("pick one, blind", "körlemesine seç");
            }
        }

        public override bool HidesHandCards
        {
            get { return true; }
        }

        /// <summary>Every card BUT the one turned over is locked. While nothing is turned over
        /// nothing is locked - the player has to be free to pick.</summary>
        public override bool LocksHandCard(BlockCard card)
        {
            return revealedCardId != 0 && card != null && card.Id != revealedCardId;
        }

        public override bool RevealHandCard(BlockCard card)
        {
            if (card == null || revealedCardId != 0)
            {
                return false; // one commitment per turn, and it cannot be taken back
            }
            revealedCardId = card.Id;
            return true;
        }

        /// <summary>The lock is the only thing this boss can jam a round with, so lifting it is
        /// the only escape it owes. Answering false once it is lifted is what keeps the engine's
        /// retry loop from spinning.</summary>
        public override bool TryEscapeDeadEnd(RoundContext ctx)
        {
            if (revealedCardId == 0)
            {
                return false;
            }
            revealedCardId = 0;
            return true;
        }

        /// <summary>Cards played blind this round, for the UI.</summary>
        public int CardsPlayedBlind
        {
            get { return cardsPlayedBlind; }
        }

        public override void OnRoundStarted(RoundContext ctx)
        {
            revealedCardId = 0;
            cardsPlayedBlind = 0;
            handBeforeMix.Clear();
            Mix(ctx.Round, ctx.Rng);
        }

        /// <summary>The turn is over: the hand has already been topped up (the refill is step 7,
        /// this hook is step 8), so remember what it looks like, mix it, and open the next
        /// commitment.</summary>
        public override void AfterTurnScored(TurnContext turn)
        {
            if (revealedCardId != 0)
            {
                cardsPlayedBlind++;
            }
            revealedCardId = 0;
            Mix(turn.Round, turn.Rng);
        }

        private void Mix(RoundEngine round, IRandomSource mixRng)
        {
            if (round == null)
            {
                return;
            }
            handBeforeMix.Clear();
            for (int i = 0; i < round.Hand.Count; i++)
            {
                handBeforeMix.Add(round.Hand[i].Id);
            }
            round.ShuffleHandOrder(mixRng);
        }
    }
}
