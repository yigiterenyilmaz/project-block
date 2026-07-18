// PURPOSE: One entry of the bonus hand ("bonus el") - extra playable blocks that do
// NOT occupy a normal hand slot. Confirmed rules: playing a bonus card counts as the
// turn's placement, burns the top card of the draw pile face-up into the discard, and
// the bonus card itself expires (leaves play) when played or when the round ends.
// The base game never fills the bonus hand; powers from the plan will (Klon, Dolly,
// Olta, Kara delik, Kazı çalışması). OutcomeOnPlay exists for exactly those powers:
// e.g. Olta cards go to the discard instead of expiring, Dolly clones return to the
// bonus hand. Add new outcomes as new enum members + a switch case in RoundEngine.

namespace ProjectBlock.Core
{
    /// <summary>What happens to a bonus card after it is placed.</summary>
    public enum BonusPlayOutcome
    {
        /// <summary>Default confirmed rule: the card leaves play for the rest of the round.</summary>
        ExpireFromRound = 0,

        /// <summary>Future (Olta and friends): the card goes to the discard pile like a hand card.</summary>
        ToDiscard = 1
    }

    /// <summary>A card sitting in the bonus hand.</summary>
    public sealed class BonusSlot
    {
        public BlockCard Card { get; }
        public BonusPlayOutcome OutcomeOnPlay { get; }

        public BonusSlot(BlockCard card, BonusPlayOutcome outcomeOnPlay)
        {
            Card = card;
            OutcomeOnPlay = outcomeOnPlay;
        }
    }
}
