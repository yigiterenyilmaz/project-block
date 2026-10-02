// PURPOSE: Snow and "Çığ", wired up - the one place SnowView is reached from, by the game and by
// the animation lab alike.
//
// THREE MOMENTS. Every repaint re-reads the heaps' numbers (SyncSnow). A turn that melted snow
// plays the melt on the cells the report names (PlaySnowMelt). And an avalanche is played in two
// halves either side of the rules: the faces of what it is about to crush are taken BEFORE the
// power runs (the board is repainted without them the moment it has), and the slide is started
// right after that repaint - the snow's descent first, each cube breaking as the front reaches
// it, and only then the fall that followed, through the board's own water animation.
//
// Nothing here decides anything: which heaps slid and how far is AvalancheVisuals, what fell
// afterwards is RoundEngine.ExternalWaterFrames, what melted is TurnReport.SnowMelted.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private SnowView snow;

        /// <summary>The last avalanche played, by reference.</summary>
        private AvalancheVisuals avalanchePlayed;

        /// <summary>What a cube an avalanche crushes is burst in: a cold near-white, so the powder
        /// reads as the snow's rather than as a power's orange blast.</summary>
        private static readonly Color AvalancheColor = new Color(0.84f, 0.93f, 1f);

        private SnowView SnowFx
        {
            get
            {
                if (snow == null)
                {
                    var go = new GameObject("SnowView");
                    go.transform.SetParent(transform, false);
                    snow = go.AddComponent<SnowView>();
                    snow.Sfx = sfx;
                    snow.Busy = delegate { return waterAnimating; };
                }
                return snow;
            }
        }

        /// <summary>Every repaint: the numbers on the heaps follow the board.</summary>
        private void SyncSnow(RoundEngine round)
        {
            if (boardView == null || boardView.Board == null)
            {
                return;
            }
            if (snow != null || boardView.Board.HasSnow)
            {
                SnowFx.Sync(boardView);
            }
        }

        /// <summary>The snow this turn's report says ran out of time.</summary>
        private void PlaySnowMelt(TurnReport report)
        {
            if (report != null && report.SnowMelted.Count > 0 && boardView != null)
            {
                SnowFx.PlayMelt(boardView, report.SnowMelted);
            }
        }

        /// <summary>
        /// The cells an aimed power lights while the player is choosing. "Çığ" needs the BOARD to
        /// answer (which heaps are on that row, how far each comes down), which a bare target does
        /// not carry - so it is asked here; every other power answers for itself.
        /// </summary>
        private IReadOnlyList<GridPos> PowerPreviewCells(Power aiming, ActivationTarget at)
        {
            if (aiming is CigPower && at.Cell.HasValue && boardView != null && boardView.Board != null)
            {
                AvalanchePlan plan = boardView.Board.PlanAvalanche(at.Cell.Value);
                List<GridPos> cells = plan.CoveredCells();
                for (int i = 0; i < plan.Columns.Count; i++)
                {
                    cells.Add(plan.Columns[i].Source);
                }
                return cells;
            }
            return aiming.PreviewCells(at);
        }

        /// <summary>BEFORE the power runs: the face of every cube the avalanche is about to
        /// crush, by cell. Null for any other power.</summary>
        private Dictionary<GridPos, ClusterBurstView.Look> CaptureAvalancheFaces(Power power,
            ActivationTarget target)
        {
            if (!(power is CigPower) || !target.Cell.HasValue || target.OnMirrorWorld
                || boardView == null || boardView.Board == null)
            {
                return null;
            }
            var faces = new Dictionary<GridPos, ClusterBurstView.Look>();
            AvalanchePlan plan = boardView.Board.PlanAvalanche(target.Cell.Value);
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                List<DestroyedCube> crushed = plan.Columns[c].Crushed;
                for (int i = 0; i < crushed.Count; i++)
                {
                    Sprite tile;
                    Color colour;
                    if (boardView.TryCubeLook(crushed[i].Pos, 0f, out tile, out colour))
                    {
                        faces[crushed[i].Pos] = new ClusterBurstView.Look
                        {
                            Tile = tile,
                            Colour = colour,
                            Paint = ViewUtil.CubeMaterialColor(crushed[i].Cube)
                        };
                    }
                }
            }
            return faces;
        }

        /// <summary>
        /// AFTER the repaint that followed the power: plays the avalanche the power reports. True
        /// when it took the activation over - the caller's generic blast and water animation are
        /// then skipped, because both are played from here in the avalanche's own order.
        /// </summary>
        private bool PlayAvalanche(CigPower cig, Dictionary<GridPos, ClusterBurstView.Look> faces)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (cig == null || faces == null || round == null || cig.LastAvalanche == null
                || ReferenceEquals(cig.LastAvalanche, avalanchePlayed) || boardView == null)
            {
                return false;
            }
            AvalancheVisuals visuals = cig.LastAvalanche;
            avalanchePlayed = visuals;
            return PlayAvalancheScene(visuals, faces,
                new List<IReadOnlyList<WaterMove>>(round.ExternalWaterFrames),
                new List<GridPos>(round.ExternalDestructionLog), round);
        }

        /// <summary>The seam the game and the lab share: an avalanche over the board as it is
        /// shown, the fall after it, and the lines it completed.</summary>
        private bool PlayAvalancheScene(AvalancheVisuals visuals,
            Dictionary<GridPos, ClusterBurstView.Look> faces, List<IReadOnlyList<WaterMove>> fall,
            List<GridPos> destroyed, RoundEngine round)
        {
            GameBoard board = boardView.Board;
            AvalanchePlan plan = visuals.Plan;
            if (board == null || plan == null || !plan.Any)
            {
                return false;
            }
            // ---- every cell anything happens in is held blank until the snow has come down
            var hold = new HashSet<GridPos>();
            var laid = new HashSet<GridPos>(visuals.Laid);
            var crushedCells = new HashSet<GridPos>();
            Vector2 centre = Vector2.zero;
            int covered = 0;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                hold.Add(plan.Columns[c].Source);
                for (int i = 0; i < plan.Columns[c].Covered.Count; i++)
                {
                    hold.Add(plan.Columns[c].Covered[i]);
                    centre += boardView.CellToWorld(plan.Columns[c].Covered[i]);
                    covered++;
                }
                for (int i = 0; i < plan.Columns[c].Crushed.Count; i++)
                {
                    crushedCells.Add(plan.Columns[c].Crushed[i].Pos);
                }
            }
            // ---- what fell afterwards: where each cube STARTED, so it can stand there meanwhile
            var startOf = new Dictionary<GridPos, GridPos>(); // where it is now -> where it began
            for (int f = 0; f < fall.Count; f++)
            {
                var moved = new List<KeyValuePair<GridPos, GridPos>>();
                for (int i = 0; i < fall[f].Count; i++)
                {
                    GridPos from = fall[f][i].From;
                    GridPos began;
                    if (!startOf.TryGetValue(from, out began))
                    {
                        began = from;
                    }
                    startOf.Remove(from);
                    moved.Add(new KeyValuePair<GridPos, GridPos>(fall[f][i].To, began));
                    hold.Add(from);
                    hold.Add(fall[f][i].To);
                }
                for (int i = 0; i < moved.Count; i++)
                {
                    startOf[moved[i].Key] = moved[i].Value;
                }
            }
            var bystanders = new List<SnowView.Bystander>();
            foreach (KeyValuePair<GridPos, GridPos> entry in startOf)
            {
                // A layer the avalanche itself laid is drawn by the avalanche.
                if (laid.Contains(entry.Value))
                {
                    continue;
                }
                Cube? landed = board.GetCube(entry.Key);
                if (!landed.HasValue)
                {
                    continue;
                }
                Sprite tile = boardView.FaceOf(landed.Value);
                bystanders.Add(new SnowView.Bystander
                {
                    At = boardView.CellToWorld(entry.Value),
                    Tile = tile,
                    Colour = ViewUtil.CubeTileColor(landed.Value, tile)
                });
            }
            // ---- the lines it completed break after the fall; their faces are taken now, while
            // the repaint that emptied them is fresh
            var lineCells = new List<GridPos>();
            var lineLooks = new List<ClusterBurstView.Look>();
            for (int i = 0; i < destroyed.Count; i++)
            {
                if (crushedCells.Contains(destroyed[i]) || lineCells.Contains(destroyed[i]))
                {
                    continue;
                }
                Sprite tile;
                Color colour;
                lineCells.Add(destroyed[i]);
                lineLooks.Add(boardView.TryCubeLook(destroyed[i], CubeLookMaxAge, out tile, out colour)
                    ? new ClusterBurstView.Look { Tile = tile, Colour = colour }
                    : new ClusterBurstView.Look
                    {
                        // Never painted: snow the avalanche laid and a line took in the same breath.
                        Tile = ViewUtil.CubeTile(CubeKind.Snow),
                        Colour = Color.white,
                        Paint = ViewUtil.ElementColor(BlockElement.Snow)
                    });
            }

            bool boomed = false;
            waterAnimating = true; // locks placement for the slide AND the fall
            SnowFx.PlayAvalanche(boardView, visuals, new List<GridPos>(hold), bystanders,
                delegate(int column, int index)
                {
                    GridPos cell = plan.Columns[column].Covered[index];
                    ClusterBurstView.Look look;
                    if (!faces.TryGetValue(cell, out look))
                    {
                        return;
                    }
                    bool first = !boomed;
                    boomed = true;
                    FlashCells(new[] { cell }, AvalancheColor,
                        first ? (System.Action)delegate { sfx.Explode(); } : null,
                        new[] { look });
                },
                delegate
                {
                    boardView.PlayWaterAnimation(fall, delegate
                    {
                        waterAnimating = false;
                        if (lineCells.Count > 0)
                        {
                            FlashCells(lineCells, BlastColor, delegate { sfx.Explode(); }, lineLooks);
                        }
                        if (round != null && session != null && session.Phase == GamePhase.Round)
                        {
                            SyncHazine(round, null);
                        }
                    });
                });
            if (visuals.Points > 0 && covered > 0)
            {
                FloatingTextFx.Spawn(transform,
                    (Vector2)boardView.transform.TransformPoint(centre / covered) + new Vector2(0f, 0.5f),
                    "+" + visuals.Points, AvalancheColor, 60, 0.07f);
            }
            return true;
        }

        // ---- animation lab ----------------------------------------------------------------
        //
        // A board of the lab's own with a heap on it, and the REAL rules run on it: the plan is
        // GameBoard.PlanAvalanche, the snow is laid by GameBoard.ApplyAvalanche and the fall is
        // the board's own settle - only the crushing, which the engine does for the destruction
        // log's sake, is done here by hand.

        private Coroutine animSnow;

        private void StopAnimSnow()
        {
            if (animSnow != null)
            {
                StopCoroutine(animSnow);
                animSnow = null;
            }
            if (snow != null)
            {
                snow.Stop();
            }
            waterAnimating = false;
        }

        /// <summary>A heap of <paramref name="power"/> on a half-built board, then the avalanche.
        /// <paramref name="floorGap"/> leaves the bottom row empty, so the layers fall afterwards.
        /// </summary>
        private void AnimAvalanche(int power, int width, bool floorGap)
        {
            StopAnimSnow();
            animSnow = StartCoroutine(AnimAvalancheRoutine(power, width, floorGap));
        }

        private IEnumerator AnimAvalancheRoutine(int power, int width, bool floorGap)
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null)
            {
                animSnow = null;
                yield break;
            }
            int w = Mathf.Max(6, round.Board.Width);
            int h = Mathf.Max(6, round.Board.Height);
            List<int> cards = AnimBossCards();
            var board = new GameBoard(w, h);
            int row = Mathf.Min(h - 1, power + (floorGap ? 1 : 0));
            int first = Mathf.Max(0, (w - width) / 2);
            for (int x = 0; x < w; x++)
            {
                for (int y = floorGap ? 1 : 0; y < row; y++)
                {
                    bool under = x >= first && x < first + width;
                    if (under || AnimHash(x + 3, y + 7) % 100u < 55u)
                    {
                        CubeKind kind = under && AnimHash(x, y) % 7u == 0u ? CubeKind.Gold
                            : under && AnimHash(x, y) % 5u == 0u ? CubeKind.Obsidian : CubeKind.Normal;
                        board.SetCubeAt(new GridPos(x, y),
                            new Cube(kind, cards[(int)(AnimHash(x / 2, y / 2) % (uint)cards.Count)]));
                    }
                }
            }
            for (int x = first; x < first + width && x < w; x++)
            {
                board.SetCubeAt(new GridPos(x, row),
                    new Cube(CubeKind.Snow, cards[0], false, power, SnowRules.MeltTurns, false));
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            SyncSnow(round);
            yield return new WaitForSeconds(0.7f);

            var aim = new GridPos(first, row);
            var faces = new Dictionary<GridPos, ClusterBurstView.Look>();
            AvalanchePlan plan = board.PlanAvalanche(aim);
            var destroyed = new List<GridPos>();
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                for (int i = 0; i < plan.Columns[c].Crushed.Count; i++)
                {
                    DestroyedCube dead = plan.Columns[c].Crushed[i];
                    Sprite tile;
                    Color colour;
                    if (boardView.TryCubeLook(dead.Pos, 0f, out tile, out colour))
                    {
                        faces[dead.Pos] = new ClusterBurstView.Look
                        {
                            Tile = tile,
                            Colour = colour,
                            Paint = ViewUtil.CubeMaterialColor(dead.Cube)
                        };
                    }
                    board.DestroyCubeForced(dead.Pos);
                    destroyed.Add(dead.Pos);
                }
            }
            var visuals = new AvalancheVisuals { Plan = plan, CubesPaid = destroyed.Count };
            visuals.Laid.AddRange(board.ApplyAvalancheOnLabBoard(plan));
            visuals.Points = destroyed.Count * new CigPower().PointsPerCrushedCube
                * session.Config.Scoring.ScoreScale;
            var fall = new List<IReadOnlyList<WaterMove>>();
            board.SettleWaterAndReact(fall);
            boardView.Refresh();
            SyncSnow(round);
            PlayAvalancheScene(visuals, faces, fall, new List<GridPos>(), null);
            animLastLabel = Loc.Pick(
                "avalanche: power " + power + ", " + plan.Columns.Count + " columns, "
                    + destroyed.Count + " crushed, " + visuals.Laid.Count + " cells of snow",
                "çığ: power " + power + ", " + plan.Columns.Count + " sütun, "
                    + destroyed.Count + " ezildi, " + visuals.Laid.Count + " hücre kar");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            animSnow = null;
        }

        /// <summary>Snow bars dropped one on another on a lab board: the fall, the absorb and the
        /// numbers; then a turn's worth of melting on the last press.</summary>
        private void AnimSnowStack()
        {
            StopAnimSnow();
            animSnow = StartCoroutine(AnimSnowStackRoutine());
        }

        private IEnumerator AnimSnowStackRoutine()
        {
            RoundEngine round = session != null ? session.CurrentRound : null;
            if (round == null || boardView == null)
            {
                animSnow = null;
                yield break;
            }
            int w = Mathf.Max(6, round.Board.Width);
            int h = Mathf.Max(6, round.Board.Height);
            var board = new GameBoard(w, h);
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            int[] lengths = { 3, 3, 2, 1 };
            int[] starts = { 1, 1, 3, 5 };
            for (int drop = 0; drop < lengths.Length; drop++)
            {
                for (int i = 0; i < lengths[drop]; i++)
                {
                    board.SetCubeAt(new GridPos(starts[drop] + i, h - 1), Cube.FreshSnow(200 + drop));
                }
                var frames = new List<IReadOnlyList<WaterMove>>();
                board.SettleWaterAndReact(frames);
                boardView.Refresh();
                SyncSnow(round);
                bool landed = false;
                waterAnimating = true;
                boardView.PlayWaterAnimation(frames, delegate { landed = true; waterAnimating = false; });
                while (!landed)
                {
                    yield return null;
                }
                SyncSnow(round);
                yield return new WaitForSeconds(0.45f);
            }
            // A turn passes four times over: the numbers count down and the snow goes.
            for (int turn = 0; turn < SnowRules.MeltTurns; turn++)
            {
                IReadOnlyList<DestroyedCube> melted = board.TickSnowMelt();
                boardView.Refresh();
                SyncSnow(round);
                if (melted.Count > 0)
                {
                    SnowFx.PlayMelt(boardView, melted);
                }
                yield return new WaitForSeconds(0.55f);
            }
            animLastLabel = Loc.Pick("snow: four bars dropped, absorbed, then melted",
                "kar: dört blok düştü, birleşti, sonra eridi");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            animSnow = null;
        }
    }
}
