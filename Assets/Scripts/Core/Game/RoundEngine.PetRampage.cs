// PURPOSE: RoundEngine's half of "Tamagotchi" going furious - the board it bites when it was left
// hungry, chosen by ANALYSIS rather than at random ("smart cruelty").
//
// THE PET HUNTS THE PLAYER'S ROOM TO MOVE. It looks at the board as it stands, the cards the
// player holds (hand and bonus hand) and the cards they are about to draw (the top of the draw
// pile, `lookahead` of them), and measures how much ROOM that leaves the player (PetRoom):
//   - how many held cards still fit ANYWHERE, heavily weighted (a card cut off is a card lost);
//   - every legal placement left for them, and - at half weight - for the coming cards;
//   - the LINE POTENTIAL of the board: every row and column that can still go off, weighted by
//     how close it is to going off, so a bite through a nearly full line costs the player the
//     line as well as the cell (an eaten cell goes dead and kills its row and its column).
// The bite is ONE CONNECTED REGION: the first cell is the one whose loss shrinks that room the
// most, every next one is the crueller of its 4-neighbours, greedily, on a clone of the board, so
// each bite is chosen against the board the previous one left.
//
// IT NEVER EATS THE LAST WAY OUT. A bite that would leave no held card with a legal placement is
// never taken - the check is the same one the dead-end rule asks (frozen and boss-locked cards are
// no way out, a gear may turn, a fox may reshape) - so the player is always left an escape, and
// the escape is as narrow as the pet can make it. Cruel, but fair.
//
// An eaten cell goes DEAD (GameBoard.MarkDead, the erosion state): it cannot be built on and it
// kills its row and its column for the rest of the round. Only EMPTY cells are candidates - the
// pet eats the ground, not the blocks, so nothing is destroyed and nothing is scored.
// EXTENSION POINT: the weights below are the whole personality of the hunt.

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

        /// <summary>How much the board's line potential counts, in placements.</summary>
        private const float PetLineWeight = 6f;

        /// <summary>
        /// The connected bite of up to <paramref name="count"/> empty cells the pet should eat to
        /// push the player as close to a dead end as it can WITHOUT closing it. Fewer when the
        /// board has no bite left that keeps a way out. Chooses only - nothing on the real board
        /// changes. <paramref name="firstStepScores"/>, when given, receives every first-bite
        /// candidate with the room it would leave (for the debug view).
        /// </summary>
        internal List<GridPos> ChooseCellsToStarve(int count, int lookahead = -1,
            List<PetCellScore> firstStepScores = null)
        {
            if (lookahead < 0)
            {
                lookahead = Rules.HandSize;
            }
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
                        // After the first bite the region grows only into its own neighbours: a
                        // bite, not a scatter of holes.
                        if (step > 0 && !TouchesAny(pos, eaten))
                        {
                            continue;
                        }
                        GameBoard trial = GameBoard.CreateClone(work);
                        if (trial.MarkDead(new[] { pos }).Count == 0)
                        {
                            continue;
                        }
                        int fitting;
                        float score = PetRoom(trial, lookahead, out fitting);
                        if (step == 0 && firstStepScores != null)
                        {
                            firstStepScores.Add(new PetCellScore { Cell = pos, Room = score, Rejected = fitting == 0 });
                        }
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

        private static bool TouchesAny(GridPos pos, List<GridPos> cells)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                int d = System.Math.Abs(cells[i].X - pos.X) + System.Math.Abs(cells[i].Y - pos.Y);
                if (d == 1)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Eats the cells for the rest of the round (they go dead). Returns the cells
        /// that actually went.</summary>
        internal List<GridPos> EatCellsForGood(IEnumerable<GridPos> cells)
        {
            List<GridPos> eaten = MainBoard.MarkDead(cells);
            ResyncSnapshot();
            if (eaten.Count > 0)
            {
                NoteBoardReshaped();
            }
            return eaten;
        }

        /// <summary>The player's room on the board as it stands now (see PetRoom).</summary>
        internal float PetRoomNow(int lookahead, out int fitting)
        {
            return PetRoom(MainBoard, lookahead, out fitting);
        }

        /// <summary>How much room the player has on <paramref name="board"/>: the held cards that
        /// still fit anywhere, heavily weighted, plus every placement left for them and - at half
        /// weight - for the next <paramref name="lookahead"/> cards of the draw pile, plus the
        /// board's line potential. Lower is worse for the player.</summary>
        internal float PetRoom(GameBoard board, int lookahead, out int fitting)
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
            int coming = lookahead;
            for (int i = draw.Count - 1; i >= 0 && coming > 0; i--, coming--)
            {
                placements += PetComingCardWeight * PlacementsOn(board, draw[i]);
            }
            return fitting * PetFittingCardWeight + placements + PetLineWeight * LinePotential(board);
        }

        /// <summary>Every row and column that can still go off, weighted by how close it is: a
        /// line one cube short is worth far more than an empty one.</summary>
        private static float LinePotential(GameBoard board)
        {
            float potential = 0f;
            for (int y = 0; y < board.Height; y++)
            {
                int gaps = board.RowGapCount(board.MinY + y);
                if (gaps >= 0)
                {
                    potential += 1f / ((1f + gaps) * (1f + gaps));
                }
            }
            for (int x = 0; x < board.Width; x++)
            {
                int gaps = board.ColumnGapCount(board.MinX + x);
                if (gaps >= 0)
                {
                    potential += 1f / ((1f + gaps) * (1f + gaps));
                }
            }
            return potential;
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
