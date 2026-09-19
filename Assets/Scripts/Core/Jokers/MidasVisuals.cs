// PURPOSE: What "Midas" paid this turn, written down for the VIEW. Reporting only: nothing here
// decides anything, and the joker's score is computed exactly as it always was.
//
// IT EXISTS BECAUSE THE ANIMATION'S SUBJECT IS THE CUBE, NOT THE CARD. "Midas paid you 8" is a
// number; "each of these four gold cubes paid you 2" is the rule. So the report carries the
// SOURCES - which held card, how many of its cubes are gold RIGHT NOW, and the shape those cubes
// actually sit in - and the View draws one payout per cube from that.
//
// THREE THINGS THE VIEW MUST NEVER WORK OUT FOR ITSELF, all of which this settles:
//
//   WHICH CUBES     the gold count comes from the ROUND (CardHasElement + EffectiveShape), never
//                   from the printed card. A boss can suppress every element ("Vanilya") and then
//                   there is no payout at all; and a card's shape is not fixed for the round
//                   ("Kıtlık" fattens what comes back from the discard, the fox reshape rewrites
//                   it), so the printed shape would pay for cubes the player is not holding.
//   HOW MUCH        PointsPerGoldCube is a BALANCE PLACEHOLDER. A View that draws "+2" is a View
//                   that lies the day somebody changes the number.
//   WHERE FROM      hand and BONUS hand only. Gold already on the board pays its own upkeep
//                   (RoundEngine.Turn step 4) and Midas never touches it - an animation that
//                   takes the board's gold as its source is telling the player the wrong rule.
//
// The SHAPE is the effective one and is carried by reference, so the View can place a payout on
// every gold cube of the card in the layout the player is actually looking at.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One held card that paid this turn.</summary>
    public sealed class MidasGoldSource
    {
        /// <summary>The card's id, so the View can find the visual that is drawing it.</summary>
        public int CardId;

        /// <summary>True for a bonus-hand slot. The two are drawn in different places and the
        /// View has to be told which list the slot index belongs to.</summary>
        public bool BonusHand;

        /// <summary>Index within that list.</summary>
        public int Slot;

        /// <summary>The shape the card is ACTUALLY in this round - what its cubes are laid out
        /// in on screen.</summary>
        public BlockShape Shape;

        /// <summary>How many of those cubes are gold. Today that is all of them (gold is a
        /// card-wide element), and it is carried separately anyway so a per-cube element would
        /// not need this file rewritten.</summary>
        public int GoldCubes;

        /// <summary>What this card paid, in screen points: GoldCubes * PointsPerGoldCube.</summary>
        public int Subtotal;
    }

    /// <summary>
    /// One turn's Midas payout. A new SERIAL every time it pays, so a repaint during the
    /// animation cannot restart it - the same key the talisman's reports use.
    /// </summary>
    public sealed class MidasPayoutVisuals
    {
        public int Serial;

        /// <summary>The live value of the joker's own field, never a number the View knows.
        /// In SCREEN points (x ScoreScale), like every number in this report.</summary>
        public int PointsPerGoldCube;

        /// <summary>What the joker actually added to this turn's score, in screen points.</summary>
        public int TotalScore;

        public readonly List<MidasGoldSource> Sources = new List<MidasGoldSource>();

        /// <summary>Every gold cube that paid, across hand and bonus hand.</summary>
        public int GoldCubes
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Sources.Count; i++)
                {
                    total += Sources[i].GoldCubes;
                }
                return total;
            }
        }

        public bool Any
        {
            get { return TotalScore > 0 && Sources.Count > 0; }
        }
    }
}
