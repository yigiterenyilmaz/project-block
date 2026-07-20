// PURPOSE: A "Parazit" binding - the card id and cell index of the board cube a
// joker rides on instead of taking a slot.

namespace ProjectBlock.Core
{
    /// <summary>Where an attached joker lives (Parazit). Reserved: nothing sets it yet.</summary>
    public readonly struct CubeAttachment
    {
        /// <summary>Id of the BlockCard the joker rides on.</summary>
        public readonly int CardId;

        /// <summary>Index into that card's BlockShape.Cells - the host cube.</summary>
        public readonly int CellIndex;

        public CubeAttachment(int cardId, int cellIndex)
        {
            CardId = cardId;
            CellIndex = cellIndex;
        }
    }
}
