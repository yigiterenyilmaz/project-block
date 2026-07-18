// PURPOSE: One card in the player's collection. A card is the deck-side identity of a
// block; the board only stores cubes (with the source card id) once a card is played.
// EXTENSION POINT: this is where per-card state from the design plan will live later:
// elemental types (fire/water/obsidian/gold/...), an attached joker cube ("Parazit"),
// upgrade/trait data, and shop pricing. Keep the base card intentionally slim.

namespace ProjectBlock.Core
{
    /// <summary>A block card owned by the player.</summary>
    public sealed class BlockCard
    {
        /// <summary>Unique per session; also stamped onto every cube this card places.</summary>
        public int Id { get; }

        public BlockShape Shape { get; }

        public BlockCard(int id, BlockShape shape)
        {
            Id = id;
            Shape = shape;
        }

        public override string ToString()
        {
            return "Card#" + Id + "(" + Shape.Size + " cubes)";
        }
    }
}
