// PURPOSE: The "Hedefli" block type. One cube of such a block is its TARGET (stamped as
// CubeKind.Target by GameBoard.Place); this file decides what breaking it is worth.
//
// THE RULE, in one sentence: whichever of the block's cubes goes FIRST decides everything.
//  - the target is in the first explosion that touches the block -> it pays the aim bonus and
//    the rest of the block goes up with it, priced as an ordinary explosion;
//  - anything else goes first -> the block is spent. It keeps standing, keeps filling cells and
//    keeps breaking normally, but it will never pay: its effect is simply gone.
//
// "First" is judged per DESTRUCTION BATCH, not per cube. A line explosion takes several cubes of
// a block at once, and requiring the target to be alone would make the block nearly unclaimable -
// so the target counts as first whenever it is AMONG the cubes of that first batch.
//
// ARMING IS PER PLACEMENT, not per card: a block that missed its shot is armed again the next
// time it comes round from the discard and is played. Round-scoped like everything else here -
// the armed set dies with the engine, so nothing leaks into the next round.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>Card ids of targeted blocks standing on the board whose shot is still live.
        /// A card leaves the set the moment its first cube breaks, either way.</summary>
        private readonly HashSet<int> armedTargetCards = new HashSet<int>();

        /// <summary>True while a payout is destroying the rest of a block, so the destruction it
        /// causes cannot re-enter this resolution and settle the same card twice.</summary>
        private bool resolvingTargets;

        /// <summary>A card was just laid on the board: if it is a targeted block, its shot is
        /// live from now until the first of its cubes breaks.</summary>
        private void ArmTargetedBlock(BlockCard card)
        {
            // Exactly the condition GameBoard.Place stamps the target cube under, so the armed
            // set and the board can never disagree about which blocks have one.
            if (card != null && !ElementsIgnored && card.Has(BlockElement.Targeted)
                && card.TargetCellIndex >= 0)
            {
                armedTargetCards.Add(card.Id);
            }
        }

        /// <summary>
        /// Settles every targeted block caught in one batch of destroyed cubes. Called from the
        /// ONE place destruction is recorded (LogDestruction), so it sees every source there is -
        /// a line explosion, a fire chain, a joker, a power, a boss - without any of them knowing
        /// this rule exists.
        /// </summary>
        private void ResolveTargetedBlocks(List<DestroyedCube> batch)
        {
            if (resolvingTargets || armedTargetCards.Count == 0 || batch == null
                || batch.Count == 0)
            {
                return;
            }
            List<int> hits = null;
            List<int> misses = null;
            for (int i = 0; i < batch.Count; i++)
            {
                int cardId = batch[i].Cube.SourceCardId;
                if (!armedTargetCards.Contains(cardId))
                {
                    continue;
                }
                if (batch[i].Cube.Kind == CubeKind.Target)
                {
                    Add(ref hits, cardId);
                }
                else
                {
                    Add(ref misses, cardId);
                }
            }
            if (hits == null && misses == null)
            {
                return;
            }
            // A card whose target went in the same batch as one of its plain cubes is a HIT: the
            // whole batch is the one explosion that reached the block.
            if (misses != null && hits != null)
            {
                for (int i = misses.Count - 1; i >= 0; i--)
                {
                    if (hits.Contains(misses[i]))
                    {
                        misses.RemoveAt(i);
                    }
                }
            }
            // Disarmed BEFORE anything is destroyed below, so the payout's own destruction cannot
            // find these cards armed again.
            if (misses != null)
            {
                for (int i = 0; i < misses.Count; i++)
                {
                    armedTargetCards.Remove(misses[i]); // spent - the block is ordinary now
                }
            }
            if (hits == null)
            {
                return;
            }
            for (int i = 0; i < hits.Count; i++)
            {
                armedTargetCards.Remove(hits[i]);
            }
            resolvingTargets = true;
            try
            {
                for (int i = 0; i < hits.Count; i++)
                {
                    PayTargetedBlock(hits[i]);
                }
            }
            finally
            {
                resolvingTargets = false;
            }
        }

        private static void Add(ref List<int> list, int value)
        {
            if (list == null)
            {
                list = new List<int>();
            }
            if (!list.Contains(value))
            {
                list.Add(value);
            }
        }

        /// <summary>The aim bonus, plus the rest of the block going up with it. The extra cubes
        /// are destroyed THROUGH the engine and count for the sweep, exactly like a fire chain -
        /// so the destruction log, the ledger and the clean-sweep pre-condition all see them.</summary>
        private void PayTargetedBlock(int cardId)
        {
            var remaining = new List<GridPos>();
            foreach (GridPos cell in Board.CellsOfCard(cardId))
            {
                remaining.Add(cell);
            }
            int bonus = scorer.ScoreTargetedBlock();
            if (remaining.Count > 0)
            {
                IReadOnlyList<GridPos> blown = DestroyCubes(remaining, true);
                if (blown.Count > 0)
                {
                    // No lines were completed by this, so only the per-cube value applies.
                    bonus += scorer.ScoreLineExplosion(0, blown.Count);
                    if (currentReport != null)
                    {
                        // Its OWN channel, not ExtraExplodedCells: "Antimadde" bills per cube in
                        // that list, and it must not be charged for a blast it did not cause.
                        currentReport.AddTargetedExplodedCells(blown);
                    }
                }
            }
            if (currentReport != null)
            {
                currentReport.NoteTargetedBlockHit(cardId, bonus);
            }
            // Before finalization it is a base value of its own (a sweep must not swallow it);
            // after it - or between turns entirely - it goes in through the late/outside path,
            // which banks it and keeps the run currency in step.
            if (currentReport != null && !scoreFinalized)
            {
                breakdown.BaseTargeted += bonus;
            }
            else
            {
                AddScoreOutsideTurn(bonus);
            }
            // The payout may well have emptied the board - but DELIBERATELY not swept here while
            // a placement is resolving. Inside a turn the sweep belongs to step 3, which counts
            // it as the player's own placement clear (paying the bonus and recharging the
            // powers); firing it early would book it as an external one and, worse, zero a line
            // score that step 2 has not even assigned yet. Between turns there is no step 3, so
            // the clear is offered here under the usual "Genel temizlik" terms.
            if (currentReport == null)
            {
                TryResolveCleanSweep();
            }
        }
    }
}
