// PURPOSE: What a player-activated joker needs to be pointed at (none, a board
// cell, a hand card...). The UI reads it to know what click to wait for.

namespace ProjectBlock.Core
{
    /// <summary>What an activated joker needs to be pointed at before it can run. The UI
    /// reads this to decide whether to ask for a target first.</summary>
    public enum ActivationTargeting
    {
        /// <summary>Fires immediately (Renovasyon).</summary>
        None = 0,

        /// <summary>Needs a card in hand (İade).</summary>
        HandCard = 1,

        /// <summary>Needs a board cell (Enfeksiyon).</summary>
        BoardCell = 2,

        /// <summary>Needs TWO whole lines - two rows or two columns - to exchange
        /// ("Kentsel Dönüşüm"). The UI puts arrows beside every row and above every column
        /// and waits for two picks on the same axis.</summary>
        LineSwap = 3,

        /// <summary>Needs a card in hand AND a subset of ITS OWN cubes ("Neşter": the player
        /// clicks the cubes that go into the first half). Target carries HandIndex + CellSet, the
        /// set holding SHAPE OFFSETS rather than board cells.</summary>
        CardCubes = 4,

        /// <summary>Needs TWO cards in hand and where the second sits against the first
        /// ("Lehimleme"). Target carries HandIndex + SecondHandIndex + Offset.</summary>
        TwoHandCards = 5,

        /// <summary>Needs a board cell AND a card in hand ("Gen nakli" moving an element from one
        /// to the other). Target carries both Cell and HandIndex, which it already could.</summary>
        CellAndHandCard = 6,

        /// <summary>Needs a 2x2 patch of board, named by its bottom-left cell ("Hidrolik pres").
        /// Distinct from BoardCell so the UI knows to preview four cells, not one.</summary>
        BoardArea = 7
    }
}
