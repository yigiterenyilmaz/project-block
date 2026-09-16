// PURPOSE: "Meydan Okuma"'s SEA OF CHANCES - for every row and column on the board, how likely the
// player is to clear it before the dare's deadline, given what they actually hold, what is still
// in the draw pile and what is standing on the board.
//
// IT IS MEASURED, NOT ESTIMATED. The obvious way to write this is a formula - count the gaps,
// count the cards that fit, multiply some factors - and every such formula is a second definition
// of "can this line be filled" that quietly disagrees with the real one: water that falls out of
// the gap it was dropped into, a neighbouring line clearing and re-opening a cell, a pocket no
// shape can reach until something else explodes. So instead the future is SAMPLED. Each sample
// takes a copy of the board and plays the deadline out with the board's OWN code -
// GameBoard.Place, ResolveFullLines and SettleWaterAndReact, in the order RoundEngine.Turn runs
// them - and the chance is simply the share of samples in which the line went off in time.
//
// WHAT A SAMPLE PLAYS:
//   THE HAND is known, so it is used as it is. Frozen cards thaw on their own schedule and cards a
//   boss has locked are not there. The bonus hand is there too, spent once.
//   THE DRAW PILE is known in CONTENT but not in ORDER - the player cannot see it either - so each
//   sample shuffles it. That is the difference between "a card that fits is in your hand" and
//   "a card that fits is somewhere in a pile of thirty", and it is most of what makes a line hard.
//   When it runs dry the discard comes back, exactly as DrawWithRules recycles it - except past
//   the threshold, where running dry is the loss and the sample fails, and under "Imitasyon",
//   where nothing comes back at all.
//   THE PLAYER is a focused one: every turn, of every legal move the hand allows (a mechanical
//   block in all four turns), it takes the one that puts the most cubes into the line's own gaps,
//   and when nothing reaches the line it parks the block as far away from it as it can. That is
//   deliberately a good player chasing the dare - "how likely is the dare to be met if you go for
//   it" is the question, not "how likely is it to happen by accident".
//
// WHAT IT DOES NOT PLAY, and says so: element side effects beyond water (a fire chain, a dynamite
// block going off), a fox's random reshape, negative and antimatter blocks (neither ever fills a
// gap), powers, and the mirror world. Each of those only ever makes a real line EASIER or HARDER
// in ways the player chooses, and none of them is something a dare should be set around.
//
// IT TOUCHES NOTHING. Every sample runs on a clone, the draw order comes from a generator of its
// own seeded from the round's state, and the round's IRandomSource is never drawn from - a joker
// computing odds must not shift every later shuffle in the round, and a replayed save has to
// reach the same odds from the same board.
//
// A LINE THAT CAN NEVER GO OFF IS NOT A HARD LINE. A dead row (erosion, "Kangren"), a line with no
// required cell, a line with a SEALED cell in it ("Mapus"), or any line at all while a boss is
// suppressing explosions ("Bilinmezlik") is marked Possible = false. Daring the player to clear
// one of those would be a rigged bet, which is a different thing from a hard one.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    /// <summary>One line's odds.</summary>
    public sealed class LineChance
    {
        public bool IsRow;

        /// <summary>0-based, the same space as TurnReport.ExplodedRows / ExplodedColumns.
        /// </summary>
        public int Index;

        /// <summary>Required cells still empty - GameBoard.RowGapCount's answer, never a second
        /// count of our own.</summary>
        public int Gaps;

        /// <summary>The turns the dare would allow for THIS line.</summary>
        public int Deadline;

        /// <summary>Share of the sampled futures in which the line exploded in time, 0..1.
        /// </summary>
        public double Chance;

        /// <summary>False for a line that cannot go off at all - see the file header. Never a
        /// candidate for a dare, however low its chance.</summary>
        public bool Possible;

        public override string ToString()
        {
            return (IsRow ? "row " : "col ") + Index + " gaps " + Gaps + " in " + Deadline
                + "t: " + (Possible ? Chance.ToString("0.00") : "never");
        }
    }

    public static class LineChanceSea
    {
        /// <summary>Futures sampled per line. Enough that a line's odds settle to within a few
        /// per cent; the whole sea is measured at most three times a round.</summary>
        public const int DefaultSamples = 40;

        /// <summary>
        /// After this many futures, a line that has gone off in EVERY one of them, or in NONE, stops
        /// being sampled.
        ///
        /// The ladder never needs precision at the ends: a line that went off twelve times out of
        /// twelve is a gimme whatever its exact odds (a true 0.70 line does that 1.4% of the time),
        /// and one that went off never is as hard as it gets. Most lines on a real board sit at one
        /// end or the other - on the real decks the median line is 0.88 - so this is where most
        /// of the cost goes, and it matters on a phone.
        /// </summary>
        public const int SettledAfter = 12;

        /// <summary>
        /// The odds of every row and column on the round's board.
        ///
        /// <paramref name="deadlineForGaps"/> is the DARE's own deadline rule, passed in rather
        /// than copied, so the odds are always of the deadline the player will actually get.
        /// <paramref name="seed"/> must come from the round's STATE (see the file header).
        /// </summary>
        public static List<LineChance> Measure(RoundEngine round, Func<int, int> deadlineForGaps,
            int samples, uint seed)
        {
            var sea = new List<LineChance>();
            if (round == null || round.Board == null)
            {
                return sea;
            }
            GameBoard board = round.Board;
            var start = Snapshot.Of(round);
            for (int y = 0; y < board.Height; y++)
            {
                sea.Add(MeasureLine(round, start, true, y, deadlineForGaps, samples,
                    seed ^ (uint)(0x9E3779B9u * (uint)(y + 1))));
            }
            if (!round.Rules.RetroMode)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    sea.Add(MeasureLine(round, start, false, x, deadlineForGaps, samples,
                        seed ^ (uint)(0x85EBCA6Bu * (uint)(x + 1))));
                }
            }
            return sea;
        }

        private static LineChance MeasureLine(RoundEngine round, Snapshot start, bool isRow,
            int index, Func<int, int> deadlineForGaps, int samples, uint seed)
        {
            GameBoard board = round.Board;
            int gaps = isRow
                ? board.RowGapCount(index + board.MinY)
                : board.ColumnGapCount(index + board.MinX);
            var line = new LineChance { IsRow = isRow, Index = index, Gaps = gaps };
            if (gaps < 1 || round.LineExplosionsSuppressed || HasSealedGap(board, isRow, index))
            {
                // Dead, not required, already full at rest (it cannot be), sealed shut, or no line
                // may go off at all this round. Not a hard line - not a line.
                line.Possible = false;
                return line;
            }
            line.Possible = true;
            line.Deadline = Math.Max(1, deadlineForGaps(gaps));
            int wins = 0;
            int played = 0;
            var rng = new Xorshift(seed);
            for (int s = 0; s < samples; s++)
            {
                if (PlaySample(round, start, isRow, index, line.Deadline, rng))
                {
                    wins++;
                }
                played++;
                if (played == SettledAfter && (wins == 0 || wins == played))
                {
                    break; // settled at one end - see SettledAfter
                }
            }
            line.Chance = played > 0 ? wins / (double)played : 0.0;
            return line;
        }

        // ---- one future ------------------------------------------------------------------------

        private sealed class Held
        {
            public BlockCard Card;
            public bool Bonus;

            /// <summary>The sample turn this card can first be played on (a frozen card thaws).
            /// </summary>
            public int PlayableFrom;
        }

        private static bool PlaySample(RoundEngine round, Snapshot start, bool isRow, int index,
            int deadline, Xorshift rng)
        {
            GameBoard sim = GameBoard.CreateClone(round.Board);
            var held = new List<Held>(start.Held.Count);
            foreach (Held h in start.Held)
            {
                held.Add(new Held { Card = h.Card, Bonus = h.Bonus, PlayableFrom = h.PlayableFrom });
            }
            // The order nobody can see: a fresh shuffle per future.
            var draw = new List<BlockCard>(start.Draw);
            Shuffle(draw, rng);
            int drawAt = 0;
            var discard = new List<BlockCard>(start.Discard);
            bool retro = round.Rules.RetroMode;

            for (int turn = 0; turn < deadline; turn++)
            {
                Move move = BestMove(round, sim, held, turn, isRow, index);
                if (move.Card == null)
                {
                    return false; // nothing fits anywhere: a dead end, and the dare dies with it
                }
                sim.Place(move.Card.Card, move.Shape, move.Origin,
                    round.CardHasElement(move.Card.Card, BlockElement.Ghost));

                // RoundEngine.Turn's own order: explode in place; if nothing went, let the water
                // fall and look again; after an explosion the floor is pulled out from the water.
                LineExplosionResult blast = sim.ResolveFullLines(retro);
                if (blast.LineCount == 0)
                {
                    sim.SettleWaterAndReact();
                    blast = sim.ResolveFullLines(retro);
                }
                if (blast.LineCount > 0)
                {
                    if (Contains(isRow ? blast.Rows : blast.Columns, index))
                    {
                        return true;
                    }
                    sim.SettleWaterAndReact();
                }

                held.Remove(move.Card);
                discard.Add(move.Card.Card);
                if (move.Card.Bonus)
                {
                    continue; // a bonus card is spent, and the hand is not refilled for it
                }
                // Refill, as DrawWithRules would.
                if (drawAt >= draw.Count)
                {
                    if (round.ThresholdPassed)
                    {
                        return false; // past the bar a dry pile is the loss
                    }
                    if (round.Rules.DrawOnlyAvailableNoReshuffle || discard.Count == 0)
                    {
                        continue; // "Imitasyon": the hand simply plays out smaller
                    }
                    draw = discard;
                    discard = new List<BlockCard>();
                    Shuffle(draw, rng);
                    drawAt = 0;
                }
                held.Add(new Held { Card = draw[drawAt++], PlayableFrom = turn + 1 });
            }
            return false;
        }

        private struct Move
        {
            public Held Card;
            public BlockShape Shape;
            public GridPos Origin;
            public int Filled;
            public int Spill;
            public int Distance;
        }

        /// <summary>
        /// The focused player's move: most cubes into the line's gaps; then fewest cubes left
        /// lying outside it (clutter is what blocks the next turn); and when nothing reaches the
        /// line at all, as far from it as the block will go. Deterministic - the first of equals
        /// in board order - so the only randomness in a future is the draw.
        /// </summary>
        private static Move BestMove(RoundEngine round, GameBoard sim, List<Held> held, int turn,
            bool isRow, int index)
        {
            var best = new Move();
            bool found = false;
            GridPos flow = sim.WaterFlow;
            for (int h = 0; h < held.Count; h++)
            {
                Held card = held[h];
                if (card.PlayableFrom > turn || !Fills(round, card.Card))
                {
                    continue;
                }
                bool ghost = round.CardHasElement(card.Card, BlockElement.Ghost);
                bool water = CubeRules.KindForCard(card.Card) == CubeKind.Water
                    && !sim.IgnoreElements;
                BlockShape shape = round.EffectiveShape(card.Card);
                int turns = round.CardHasElement(card.Card, BlockElement.Mechanical) ? 4 : 1;
                for (int r = 0; r < turns; r++)
                {
                    int fromX = ghost ? 1 - shape.Width : 0;
                    int fromY = ghost ? 1 - shape.Height : 0;
                    for (int ox = fromX; ox < sim.Width; ox++)
                    {
                        for (int oy = fromY; oy < sim.Height; oy++)
                        {
                            var origin = new GridPos(ox + sim.MinX, oy + sim.MinY);
                            if (!sim.CanPlace(shape, origin, ghost))
                            {
                                continue;
                            }
                            Move m = Score(sim, shape, origin, isRow, index, water, flow);
                            m.Card = card;
                            m.Shape = shape;
                            m.Origin = origin;
                            if (!found || Better(m, best))
                            {
                                best = m;
                                found = true;
                            }
                        }
                    }
                    shape = shape.RotatedClockwise();
                }
            }
            return best;
        }

        private static bool Better(Move a, Move b)
        {
            if (a.Filled != b.Filled)
            {
                return a.Filled > b.Filled;
            }
            if (a.Filled > 0 && a.Spill != b.Spill)
            {
                return a.Spill < b.Spill;
            }
            return a.Filled == 0 && a.Distance > b.Distance;
        }

        private static Move Score(GameBoard sim, BlockShape shape, GridPos origin, bool isRow,
            int index, bool water, GridPos flow)
        {
            var m = new Move();
            int lineAbs = index + (isRow ? sim.MinY : sim.MinX);
            IReadOnlyList<GridPos> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                GridPos p = origin + cells[i];
                bool onLine = isRow ? p.Y == lineAbs : p.X == lineAbs;
                if (onLine && sim.IsInside(p) && !sim.IsOptional(p) && !sim.GetCube(p).HasValue
                    && (!water || WaterRests(sim, shape, origin, p, flow)))
                {
                    m.Filled++;
                }
                else
                {
                    m.Spill++;
                }
                int d = Math.Abs((isRow ? p.Y : p.X) - lineAbs);
                m.Distance += d;
            }
            return m;
        }

        /// <summary>A water cube only fills the gap it is dropped into if it stays there: the next
        /// cell along the arena's pull is wall, standing cube, or another cube of the same block.
        /// Otherwise it falls through and fills nothing - which is the single most common way a
        /// formula over-counts a line.</summary>
        private static bool WaterRests(GameBoard sim, BlockShape shape, GridPos origin, GridPos at,
            GridPos flow)
        {
            GridPos next = at + flow;
            if (!sim.IsInside(next) || sim.GetCube(next).HasValue)
            {
                return true;
            }
            IReadOnlyList<GridPos> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                if ((origin + cells[i]).Equals(next))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Negative blocks erase and antimatter annihilates; neither ever puts a cube in
        /// a gap, so neither is a way to meet a dare.</summary>
        private static bool Fills(RoundEngine round, BlockCard card)
        {
            return !card.AntimatterOf.HasValue
                && !round.CardHasElement(card, BlockElement.Negative);
        }

        private static bool HasSealedGap(GameBoard board, bool isRow, int index)
        {
            int length = isRow ? board.Width : board.Height;
            for (int i = 0; i < length; i++)
            {
                GridPos p = isRow
                    ? new GridPos(i + board.MinX, index + board.MinY)
                    : new GridPos(index + board.MinX, i + board.MinY);
                if (board.IsSealed(p))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Contains(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }
            return false;
        }

        private static void Shuffle(List<BlockCard> cards, Xorshift rng)
        {
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                BlockCard t = cards[i];
                cards[i] = cards[j];
                cards[j] = t;
            }
        }

        /// <summary>What the round holds at the moment the sea is measured. Read once and shared
        /// by every sample, so the round is only ever READ.</summary>
        private sealed class Snapshot
        {
            public readonly List<Held> Held = new List<Held>();
            public readonly List<BlockCard> Draw = new List<BlockCard>();
            public readonly List<BlockCard> Discard = new List<BlockCard>();

            public static Snapshot Of(RoundEngine round)
            {
                var s = new Snapshot();
                for (int i = 0; i < round.Hand.Count; i++)
                {
                    BlockCard card = round.Hand[i];
                    if (round.IsLockedByBoss(card))
                    {
                        continue;
                    }
                    s.Held.Add(new Held { Card = card, PlayableFrom = round.FreezeTurnsLeft(card.Id) });
                }
                foreach (BonusSlot slot in round.BonusHand)
                {
                    s.Held.Add(new Held { Card = slot.Card, Bonus = true });
                }
                s.Draw.AddRange(round.Deck.DrawPile);
                s.Discard.AddRange(round.Deck.DiscardPile);
                return s;
            }
        }

        /// <summary>The sea's own generator. Not IRandomSource, on purpose: see the file header.
        /// </summary>
        private sealed class Xorshift
        {
            private uint state;

            public Xorshift(uint seed)
            {
                state = seed == 0 ? 0x6D2B79F5u : seed;
            }

            public int Next(int bound)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (int)(state % (uint)bound);
            }
        }
    }
}
