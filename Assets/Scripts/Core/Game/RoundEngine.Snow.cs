// PURPOSE: RoundEngine's half of SNOW (partial) - the turn passing for the snow on the board, and
// the avalanche the "Çığ" power asks for. The board knows how snow behaves (GameBoard.Snow); this
// is where it meets the things only the engine may do: the destruction log, the score, the lines
// a board change completes.

using System.Collections.Generic;

namespace ProjectBlock.Core
{
    partial class RoundEngine
    {
        /// <summary>
        /// Turn step 0: every snow heap is a turn nearer melting and what ran out is gone, in
        /// both worlds. Melting is not a destruction, so the destruction diff is re-baselined
        /// straight away - anything that destroys later this turn must not find the melted cells
        /// missing and bill the turn for them. Nothing at all happens on a board without snow,
        /// which is what keeps a snowless round byte-identical.
        /// </summary>
        private void MeltSnowForTurn(TurnReport report)
        {
            bool mainSnow = MainBoard.HasSnow;
            bool mirrorSnow = MirrorBoard != null && MirrorBoard.HasSnow;
            if (!mainSnow && !mirrorSnow)
            {
                return;
            }
            if (mainSnow)
            {
                report.AddSnowMelted(MainBoard.TickSnowMelt());
            }
            if (mirrorSnow)
            {
                MirrorBoard.TickSnowMelt();
            }
            ResyncSnapshot();
        }

        /// <summary>What an avalanche on the line through <paramref name="cell"/> would do, on the
        /// board the activation is pointed at. Changes nothing - the power's preview and its
        /// usability check both ask this.</summary>
        internal AvalanchePlan PlanAvalanche(GridPos cell)
        {
            return Board.PlanAvalanche(cell);
        }

        /// <summary>
        /// "Çığ": the heaps on the line through <paramref name="cell"/> come down. In order -
        /// what stands in the way is CRUSHED (forced: gold and obsidian go too) through
        /// DestroyCubes, so the destruction log, the sweep tally and "Hedefli" all see it; the
        /// snow takes the cells; every crushed cube that was not snow itself pays
        /// <paramref name="pointsPerCrushedCube"/>; then the snow settles under the arena's own
        /// gravity and any line the avalanche completed goes off under the ordinary between-turn
        /// rules. Returns what happened, or null when nothing on that line can slide.
        /// </summary>
        internal AvalancheVisuals TriggerAvalanche(GridPos cell, int pointsPerCrushedCube)
        {
            AvalanchePlan plan = Board.PlanAvalanche(cell);
            if (!plan.Any)
            {
                return null;
            }
            var visuals = new AvalancheVisuals { Plan = plan };
            List<GridPos> doomed = plan.CrushedCells();
            IReadOnlyList<GridPos> gone = doomed.Count > 0
                ? DestroyCubes(doomed, true, true)
                : new List<GridPos>();
            visuals.Laid.AddRange(Board.ApplyAvalanche(plan));
            // The heaps LEFT their line and snow APPEARED under it: neither is a death.
            NoteBoardRearranged();

            // Snow the avalanche ran over is only buried - it never pays for itself.
            int paying = 0;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                List<DestroyedCube> crushed = plan.Columns[c].Crushed;
                for (int i = 0; i < crushed.Count; i++)
                {
                    if (crushed[i].Cube.Kind != CubeKind.Snow && Contains(gone, crushed[i].Pos))
                    {
                        paying++;
                    }
                }
            }
            visuals.CubesPaid = paying;
            int before = RoundScore;
            AddScoreOutsideTurn(paying * pointsPerCrushedCube);
            visuals.Points = RoundScore - before;

            // What is left hanging falls, snow that lands on snow is absorbed, and a line the
            // avalanche filled goes off. Appended to the frames the activation already holds.
            Board.SettleWaterAndReact(externalWaterFrames);
            ResyncSnapshot();
            ResolveFullLinesOutsideTurn();
            return visuals;
        }
    }
}
