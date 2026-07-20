// PURPOSE: What a player-activated joker/power was pointed at - an optional hand
// index and/or board cell. Built via None/Hand/Board factories.

namespace ProjectBlock.Core
{
    /// <summary>What a player-activated joker was pointed at. All fields optional.</summary>
    public readonly struct ActivationTarget
    {
        /// <summary>Index into the hand (Iade picks the card to swap).</summary>
        public readonly int? HandIndex;

        /// <summary>A board cell (future: Enfeksiyon picks the cube to infect).</summary>
        public readonly GridPos? Cell;

        public ActivationTarget(int? handIndex, GridPos? cell)
        {
            HandIndex = handIndex;
            Cell = cell;
        }

        public static readonly ActivationTarget None = new ActivationTarget(null, null);

        public static ActivationTarget Hand(int handIndex)
        {
            return new ActivationTarget(handIndex, null);
        }

        public static ActivationTarget Board(GridPos cell)
        {
            return new ActivationTarget(null, cell);
        }
    }
}
