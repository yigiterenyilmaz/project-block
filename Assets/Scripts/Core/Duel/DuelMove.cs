// PURPOSE: One move of the duel's computer side - which held card, worn how, and where - and the
// one place a move is APPLIED, so the planner's simulations and the real play cannot lay a card
// down two different ways.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>A held card, how it is worn (element, fox form, quarter turns) and where it goes.</summary>
    public sealed class DuelMove
    {
        /// <summary>The card's id - its hand slot is looked up when the move is applied, because
        /// a simulated hand and the real one hold the card at the same slot only by accident.</summary>
        public int CardId;

        /// <summary>The element an alchemical card is played as, or null to leave it as it is.</summary>
        public BlockElement? Element;

        /// <summary>The fox form, or null for the card's own shape.</summary>
        public BlockShape FoxShape;

        /// <summary>Quarter turns clockwise (a mechanical block).</summary>
        public int Rotation;

        public GridPos Origin;

        /// <summary>The shape that lands, as worn - for the View and for the tests.</summary>
        public BlockShape Shape;

        /// <summary>What the planner made of it, in the round's (scaled) points: what the move
        /// banks now, and its whole value with the position it leaves. Reporting only.</summary>
        public double Gain;
        public double Value;

        public override string ToString()
        {
            return "card " + CardId + (Element.HasValue ? " as " + Element.Value : "")
                + (FoxShape != null ? " fox" : "") + (Rotation != 0 ? " rot" + Rotation : "")
                + " at " + Origin;
        }

        /// <summary>
        /// Plays the move on <paramref name="round"/>: the card is worn as the move says, placed,
        /// and an alchemical card's choice is put BACK straight after - the cubes it laid already
        /// carry their kind, and the card is the player's own (the duel shares the run's cards),
        /// so the computer's choice must not outlive the placement. Returns the turn's report, or
        /// null when the card is not held or the placement is refused.
        /// </summary>
        internal TurnReport ApplyTo(RoundEngine round)
        {
            int slot = SlotOf(round, CardId);
            if (slot < 0)
            {
                return null;
            }
            BlockCard card = round.Hand[slot];
            int previousChoice = card.ActiveChoice;
            if (Element.HasValue)
            {
                card.Choose(Element.Value);
            }
            try
            {
                round.SetCardOrientation(card.Id, FoxShape, Rotation);
                if (!round.CanPlaceCard(card, Origin))
                {
                    return null;
                }
                return round.PlayFromHand(slot, Origin);
            }
            finally
            {
                card.ActiveChoice = previousChoice;
            }
        }

        internal static int SlotOf(RoundEngine round, int cardId)
        {
            for (int i = 0; i < round.Hand.Count; i++)
            {
                if (round.Hand[i].Id == cardId)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
