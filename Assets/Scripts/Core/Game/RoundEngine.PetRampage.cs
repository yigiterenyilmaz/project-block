// PURPOSE: RoundEngine's half of "Tamagotchi" going BERSERK - the board the pet eats when it was
// left hungry, chosen by ANALYSIS rather than at random.
//
// THE PET HUNTS THE PLAYER'S ROOM TO MOVE. It looks at the board as it stands, the cards the
// player holds (hand and bonus hand) and the cards they are about to draw (the top of the draw
// pile, a hand's worth), and eats the empty cells whose loss squeezes those cards hardest: first
// the number of held cards that still fit ANYWHERE, then how many placements are left for them
// and, at half weight, for the cards coming next. Greedy, one cell at a time, on a clone of the
// board, so each bite is chosen against the board the previous bite left.
//
// IT NEVER EATS THE LAST WAY OUT. A bite that would leave no held card with a legal placement is
// never taken - the check is the same one the dead-end rule asks (frozen and boss-locked cards are
// no way out, a gear may turn, a fox may reshape) - so the player is always left an escape, and
// the escape is as narrow as the pet can make it. It is a very bad position, never a certain loss.
//
// An eaten cell goes DEAD (GameBoard.MarkDead, the erosion state): it cannot be built on and it
// kills its row and its column for the rest of the round. Only EMPTY cells are candidates - the
// pet eats the room, not the blocks, so nothing is destroyed and nothing is scored.
// EXTENSION POINT: the pressure weights below are the whole personality of the hunt.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>What a held card that can still go SOMEWHERE is worth to the player, in
        /// placements. Large, so the pet first cuts off whole cards and only then trims the room
        /// the survivors have.</summary>
        private const float PetFittingCardWeight = 25f;

        /// <summary>How much a coming card's room counts next to a held card's.</summary>
        private const float PetComingCardWeight = 0.5f;

        /// <summary>
        /// The <paramref name="count"/> empty cells the pet should eat to push the player as
        /// close to a dead end as it can WITHOUT closing it. Fewer when the board has no bite
        /// left that keeps a way out. Chooses only - nothing on the real board changes.
        /// </summary>
        internal List<GridPos> ChooseCellsToStarve(int count)
        {
            var eaten = new List<GridPos>();
            GameBoard work = GameBoard.CreateClone(MainBoard);
            float cx = work.MinX + (work.Width - 1) * 0.5f;
            float cy = work.MinY + (work.Height - 1) * 0.5f;
            for (int step = 0; step < count; step++)
            {
                GridPos best = default(GridPos);
                bool found = false;
                float bestScore = float.MaxValue;
                for (int x = 0; x < work.Width; x++)
                {
                    for (int y = 0; y < work.Height; y++)
                    {
                        var pos = new GridPos(work.MinX + x, work.MinY + y);
                        if (!work.IsInside(pos) || work.GetCube(pos).HasValue)
                        {
                            continue;
                        }
                        GameBoard trial = GameBoard.CreateClone(work);
                        if (trial.MarkDead(new[] { pos }).Count == 0)
                        {
                            continue;
                        }
                        int fitting;
                        float score = PetPressure(trial, out fitting);
                        if (fitting == 0)
                        {
                            continue; // the last way out is never eaten
                        }
                        // Ties go to the cell nearer the middle, where a dead row AND column
                        // cost the player the most.
                        float dx = pos.X - cx;
                        float dy = pos.Y - cy;
                        score += (dx * dx + dy * dy) * 0.001f;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = pos;
                            found = true;
                        }
                    }
                }
                if (!found)
                {
                    break;
                }
                work.MarkDead(new[] { best });
                eaten.Add(best);
            }
            return eaten;
        }

        /// <summary>Eats the cells for the rest of the round (they go dead). Returns the cells
        /// that actually went.</summary>
        internal List<GridPos> EatCellsForGood(IEnumerable<GridPos> cells)
        {
            List<GridPos> eaten = MainBoard.MarkDead(cells);
            ResyncSnapshot();
            return eaten;
        }

        /// <summary>How much room the player has on <paramref name="board"/>: the held cards that
        /// still fit anywhere, heavily weighted, plus every placement left for them and - at half
        /// weight - for the next hand's worth of the draw pile. Lower is worse for the player.
        /// </summary>
        private float PetPressure(GameBoard board, out int fitting)
        {
            fitting = 0;
            float placements = 0f;
            for (int i = 0; i < Hand.Count; i++)
            {
                int n = PlacementsOn(board, Hand[i]);
                if (n > 0)
                {
                    fitting++;
                }
                placements += n;
            }
            foreach (BonusSlot slot in bonusHand)
            {
                int n = PlacementsOn(board, slot.Card);
                if (n > 0)
                {
                    fitting++;
                }
                placements += n;
            }
            IReadOnlyList<BlockCard> draw = Deck.DrawPile;
            int coming = Rules.HandSize;
            for (int i = draw.Count - 1; i >= 0 && coming > 0; i--, coming--)
            {
                placements += PetComingCardWeight * PlacementsOn(board, draw[i]);
            }
            return fitting * PetFittingCardWeight + placements;
        }

        /// <summary>Every legal origin this card has on <paramref name="board"/>, by the same
        /// rules the dead-end check uses (CanPlayCardAnywhere): a frozen or boss-locked card has
        /// none, a gear counts all four turns, a fox whatever it could reshape into.</summary>
        private int PlacementsOn(GameBoard board, BlockCard card)
        {
            if (card == null || IsFrozen(card.Id) || IsLockedByBoss(card))
            {
                return 0;
            }
            bool ghost = Has(card, BlockElement.Ghost);
            bool negative = Has(card, BlockElement.Negative);
            BlockShape shape = EffectiveShape(card);
            int count = CountOrigins(board, shape, ghost, negative);
            if (Has(card, BlockElement.Mechanical))
            {
                BlockShape rotated = shape;
                for (int i = 0; i < 3; i++)
                {
                    rotated = rotated.RotatedClockwise();
                    count += CountOrigins(board, rotated, ghost, negative);
                }
            }
            if (Has(card, BlockElement.Fox))
            {
                var seen = new HashSet<string>();
                foreach (BlockShape deckShape in AllRoundShapes())
                {
                    if (seen.Add(deckShape.CanonicalKey))
                    {
                        int n = CountOrigins(board, deckShape, ghost, negative);
                        if (n > count)
                        {
                            count = n;
                        }
                    }
                }
            }
            return count;
        }

        private static int CountOrigins(GameBoard board, BlockShape shape, bool ghost, bool negative)
        {
            int fromX = ghost ? 1 - shape.Width : 0;
            int fromY = ghost ? 1 - shape.Height : 0;
            int count = 0;
            for (int x = fromX; x < board.Width; x++)
            {
                for (int y = fromY; y < board.Height; y++)
                {
                    if (board.CanPlace(shape, new GridPos(x + board.MinX, y + board.MinY), ghost, negative))
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
