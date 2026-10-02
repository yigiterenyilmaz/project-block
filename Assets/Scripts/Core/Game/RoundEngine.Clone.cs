// PURPOSE: RoundEngine (partial) - a COPY of a round to play moves out on. The duel's planner
// ("Blackjack", Core/Duel/DuelPlanner.cs) decides what the computer plays by really playing each
// candidate on a copy of its round and reading what the rules made of it, instead of keeping a
// second model of the rules that could disagree with the first.
//
// ONLY A ROUND WITH NO SESSION CAN BE COPIED. A session-less round is the base game and nothing
// else - no jokers, no powers, no boss, no purse - so everything it is fits in this class and
// playing a turn on the copy touches nothing outside it. A real round's jokers hold state of
// their own that no copy made here could carry.
//
// WHAT IS COPIED is exactly what a save writes (RoundEngine.Save): everything that outlives a
// turn. The per-turn scratch state is re-derived, as a load re-derives it. The test that pins this
// is a save of the copy compared with a save of the original, text for text - so a field added to
// the save and forgotten here fails a test rather than quietly bending a plan.
//
// The CARDS are shared with the original (as the piles share them with the owned deck). The copy
// draws its randomness from a null source: the base game draws none in a turn, and a plan must
// never move the run's real stream.

using System;
using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>A random source for a copy: always the low end. Never asked in a base-game
        /// turn; it exists so that a copy can never reach the run's stream.</summary>
        private sealed class NullRandom : IRandomSource
        {
            public static readonly NullRandom Instance = new NullRandom();

            public int NextInt(int minInclusive, int maxExclusive)
            {
                return minInclusive;
            }

            public double NextDouble()
            {
                return 0.0;
            }
        }

        /// <summary>The random source a copy and the house's round use: never asked in a base-game
        /// turn, and never the run's stream.</summary>
        internal static IRandomSource PlanningRandom
        {
            get { return NullRandom.Instance; }
        }

        /// <summary>True for a round that can be copied: one that is nothing but the base game.</summary>
        internal bool CanCloneForPlanning
        {
            get { return session == null && Boss == null && MirrorBoard == null; }
        }

        /// <summary>The session-less copy of a round for planning, made from ANY round: the
        /// duel's computer side is session-less already, but a test or a lab may want to plan
        /// on a copy of something else. Identical to CloneForPlanning for a session-less round.</summary>
        internal RoundEngine CloneDetached()
        {
            return CloneCore();
        }

        /// <summary>A copy of this round, as it stands between turns. See the file header.</summary>
        internal RoundEngine CloneForPlanning()
        {
            if (!CanCloneForPlanning)
            {
                throw new InvalidOperationException(
                    "Only a session-less round (no jokers, no boss, no mirror) can be copied.");
            }
            return CloneCore();
        }

        private RoundEngine CloneCore()
        {
            if (currentReport != null)
            {
                throw new InvalidOperationException("A round cannot be copied in the middle of a turn.");
            }
            var clone = new RoundEngine(Config, Rules, NullRandom.Instance, scorer, null, null,
                GameBoard.CreateClone(mainBoard), Deck.CloneWith(NullRandom.Instance));
            for (int i = 0; i < Hand.Count; i++)
            {
                clone.Hand.Add(Hand[i]);
            }
            clone.bonusHand.AddRange(bonusHand);

            clone.TurnNumber = TurnNumber;
            clone.RoundScore = RoundScore;
            clone.ThresholdPassed = ThresholdPassed;
            clone.CleanSweepCount = CleanSweepCount;
            clone.ContinueCount = ContinueCount;
            clone.Loss = Loss;
            clone.pendingAdvanceOffer = pendingAdvanceOffer;
            clone.bossWonTheRound = bossWonTheRound;
            clone.comboCount = comboCount;
            clone.comboBlankTurns = comboBlankTurns;
            clone.PowersUsedThisTurn = PowersUsedThisTurn;
            clone.DeckRecycleCount = DeckRecycleCount;
            clone.BoardErosionCount = BoardErosionCount;
            clone.erodedLeft = erodedLeft;
            clone.erodedRight = erodedRight;
            clone.erodedBottom = erodedBottom;
            clone.erodedTop = erodedTop;
            clone.erodedHoles.UnionWith(erodedHoles);
            clone.erodedOptional.UnionWith(erodedOptional);
            clone.erodedDead.UnionWith(erodedDead);
            clone.SuppressNaturalSweep = SuppressNaturalSweep;
            clone.drawPileReportedEmpty = drawPileReportedEmpty;
            clone.cleanSampleLocked = cleanSampleLocked;
            clone.CreditInstallment = CreditInstallment;
            clone.ExternalScoreCredited = ExternalScoreCredited;
            clone.waterFallTurn = waterFallTurn;
            // The boss is not copied, so a duel side keeps the table's rules through the seat.
            clone.duelSeat = PlaysOutItsCards;

            clone.armedTargetCards.UnionWith(armedTargetCards);
            foreach (KeyValuePair<int, DynamiteState> entry in dynamiteBlocks)
            {
                clone.dynamiteBlocks[entry.Key] = new DynamiteState
                {
                    FullSize = entry.Value.FullSize,
                    RemainingAtTurnStart = entry.Value.RemainingAtTurnStart,
                    PlacementTurn = entry.Value.PlacementTurn
                };
            }
            CopyMap(rotations, clone.rotations);
            CopyMap(cardPlacedSize, clone.cardPlacedSize);
            CopyMap(weldedOnBoard, clone.weldedOnBoard);
            CopyMap(frozenCards, clone.frozenCards);
            foreach (KeyValuePair<int, BlockShape> entry in foxShapes)
            {
                clone.foxShapes[entry.Key] = entry.Value; // shapes are immutable
            }
            foreach (KeyValuePair<int, BlockElement> entry in cardElements)
            {
                clone.cardElements[entry.Key] = entry.Value;
            }
            foreach (KeyValuePair<int, BorrowedGene> entry in borrowedGenes)
            {
                clone.borrowedGenes[entry.Key] = entry.Value;
            }
            // The rewind history's entries are never written to once taken, so the copy may hold
            // the same ones; the LIST is its own.
            clone.boardHistory.AddRange(boardHistory);

            // The per-turn destruction bookkeeping is re-derived, exactly as a load does it.
            clone.ResyncSnapshot();
            clone.CaptureTurnStartCardCounts();
            // Last, and written straight to the field: a copy has no listeners to tell.
            clone.Status = Status;
            return clone;
        }

        // ---- what the planner reads and sets on a round (the original or a copy) ----

        /// <summary>The price list this round scores with.</summary>
        internal IScoreCalculator Scorer
        {
            get { return scorer; }
        }

        /// <summary>Consecutive line-clearing turns so far (the combo streak).</summary>
        internal int ComboStreak
        {
            get { return comboCount; }
        }

        /// <summary>Every shape a fox block may take in this round - the one list the dead-end
        /// check uses, so a plan can never reshape a fox into something the rules refuse.</summary>
        internal IEnumerable<BlockShape> RoundShapes()
        {
            return AllRoundShapes();
        }

        /// <summary>The fox form a card is wearing, or null for its own shape.</summary>
        internal BlockShape FoxShapeOf(int cardId)
        {
            BlockShape shape;
            return foxShapes.TryGetValue(cardId, out shape) ? shape : null;
        }

        /// <summary>Quarter turns a card has been given (0..3).</summary>
        internal int RotationStepsOf(int cardId)
        {
            int steps;
            return rotations.TryGetValue(cardId, out steps) ? steps : 0;
        }

        /// <summary>
        /// Sets how a held card lies - its fox form (null for its own shape) and its quarter
        /// turns - in one write, without the checks RotateCard / SetFoxShape make. The planner
        /// uses it to try an orientation and to put the old one back; it only ever asks for what
        /// those two methods would have allowed (a rotation of a mechanical block, a form out of
        /// RoundShapes for a fox).
        /// </summary>
        internal void SetCardOrientation(int cardId, BlockShape foxShape, int rotationSteps)
        {
            if (foxShape == null)
            {
                foxShapes.Remove(cardId);
            }
            else
            {
                foxShapes[cardId] = foxShape;
            }
            int steps = ((rotationSteps % 4) + 4) % 4;
            if (steps == 0)
            {
                rotations.Remove(cardId);
            }
            else
            {
                rotations[cardId] = steps;
            }
        }

        private static void CopyMap(Dictionary<int, int> from, Dictionary<int, int> to)
        {
            foreach (KeyValuePair<int, int> entry in from)
            {
                to[entry.Key] = entry.Value;
            }
        }
    }
}
