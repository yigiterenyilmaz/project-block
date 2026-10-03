// PURPOSE: Snow and "Çığ", wired up - the one place SnowView is reached from, by the game and by
// the animation lab alike.
//
// THE MOMENTS. Every repaint hands the snow layer the board (SyncSnow): it hooks itself into the
// board's fall so snow slides and merges as snow, and re-reads the heaps. A turn that melted snow
// plays the melt on the cells the report names (PlaySnowMelt). While "Çığ" is being aimed, every
// frame shows Core's plan for the line under the pointer (ShowAvalancheAim) and a click on a line
// that cannot go is refused where it stands (RefuseAvalancheAt). And an avalanche is played in two
// halves either side of the rules: the faces of what it is about to crush are taken BEFORE the
// power runs (the board is repainted without them the moment it has), and the slide is started
// right after that repaint - the descent, each row's blocks breaking as the front reaches them,
// the settle, and only then the fall that followed through the board's own water animation, with
// the score's "+TOTAL" carried to the TOPLAM over it.
//
// Nothing here decides anything: the plan is GameBoard.PlanAvalanche, what happened is
// AvalancheVisuals, what fell afterwards RoundEngine.ExternalWaterFrames, what merged
// GameBoard.SnowMerges, what melted TurnReport.SnowMelted.

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

        /// <summary>What a block the avalanche crushes breaks in: a cold near-white, so the powder
        /// reads as the snow's rather than as a power's orange blast.</summary>
        private static readonly Color AvalancheColor = new Color(0.86f, 0.94f, 1f);

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
                    snow.ScoreAnchor = ScoreWorldAnchor;
                    snow.AimChanged = OnAvalancheAim;
                }
                if (boardView != null)
                {
                    snow.Attach(boardView);
                }
                return snow;
            }
        }

        /// <summary>Every repaint: the snow layer follows the board.</summary>
        private void SyncSnow(RoundEngine round)
        {
            if (boardView == null || boardView.Board == null)
            {
                return;
            }
            SnowFx.Sync(boardView);
        }

        /// <summary>The snow this turn's report says ran out of time.</summary>
        private void PlaySnowMelt(TurnReport report)
        {
            if (report != null && report.SnowMelted.Count > 0 && boardView != null)
            {
                SnowFx.PlayMelt(boardView, report.SnowMelted);
            }
        }

        /// <summary>The aim began or ended: a cold pulse on the power card, the background a breath
        /// darker while the player chooses.</summary>
        private void OnAvalancheAim(bool on)
        {
            if (on && pendingTargetPowerId.HasValue && powerBar != null)
            {
                powerBar.PulsePower(pendingTargetPowerId.Value);
            }
            if (background == null)
            {
                return;
            }
            if (!on)
            {
                background.SetMood("avalanche", null);
                return;
            }
            GameBackgroundPresentationController.Mood mood = GameBackgroundPresentationController.Mood.Neutral;
            mood.Brightness = 0.955f;
            mood.Saturation = 0.96f;
            mood.Warmth = 0.85f;
            background.SetMood("avalanche", mood);
        }

        /// <summary>Every frame "Çığ" is aimed at a cell: Core's plan for that line, drawn.</summary>
        private void ShowAvalancheAim(ActivationTarget at)
        {
            if (!at.Cell.HasValue || boardView == null || boardView.Board == null)
            {
                return;
            }
            SnowFx.ShowAim(boardView, boardView.Board.PlanAvalanche(at.Cell.Value));
        }

        /// <summary>A click on a line that cannot avalanche: refused where it stands, the aim kept.
        /// True when refused.</summary>
        private bool RefuseAvalancheAt(ActivationTarget target)
        {
            if (!target.Cell.HasValue || target.OnMirrorWorld || boardView == null || boardView.Board == null)
            {
                return false;
            }
            AvalanchePlan plan = boardView.Board.PlanAvalanche(target.Cell.Value);
            if (plan.Any)
            {
                return false;
            }
            SnowFx.PlayRefused(boardView, plan, target.Cell.Value);
            if (messageText != null)
            {
                messageText.text = plan.Blocked == AvalancheBlock.Spent
                    ? Loc.Pick("That snow was laid by an avalanche: feed it fresh snow first.",
                        "Bu karı bir çığ bıraktı: önce üstüne taze kar düşmeli.")
                    : plan.Blocked == AvalancheBlock.NoRoom
                        ? Loc.Pick("That snow has nowhere to slide.", "O karın kayacak yeri yok.")
                        : Loc.Pick("No snow on that row.", "O satırda kar yok.");
            }
            return true;
        }

        /// <summary>The cells an aimed power lights while the player is choosing (every power but
        /// "Çığ", which draws its own aim).</summary>
        private IReadOnlyList<GridPos> PowerPreviewCells(Power aiming, ActivationTarget at)
        {
            return aiming.PreviewCells(at);
        }

        /// <summary>BEFORE the power runs: the face of every cube the avalanche is about to crush,
        /// by cell. Null for any other power.</summary>
        private Dictionary<GridPos, ClusterBurstView.Look> CaptureAvalancheFaces(Power power,
            ActivationTarget target)
        {
            if (!(power is CigPower) || !target.Cell.HasValue || target.OnMirrorWorld
                || boardView == null || boardView.Board == null)
            {
                return null;
            }
            return FacesFor(boardView.Board.PlanAvalanche(target.Cell.Value));
        }

        private Dictionary<GridPos, ClusterBurstView.Look> FacesFor(AvalanchePlan plan)
        {
            var faces = new Dictionary<GridPos, ClusterBurstView.Look>();
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
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                hold.Add(plan.Columns[c].Source);
                for (int i = 0; i < plan.Columns[c].Covered.Count; i++)
                {
                    hold.Add(plan.Columns[c].Covered[i]);
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
                // a layer the avalanche itself laid is drawn by the avalanche
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
                    Colour = ViewUtil.CubeTileColor(landed.Value, tile),
                    Cube = landed.Value
                });
            }
            // ---- the lines it completed break after the fall; their faces are taken now
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
                        // never painted: snow the avalanche laid and a line took in the same breath
                        Tile = ViewUtil.SnowTile,
                        Colour = Color.white,
                        Paint = ViewUtil.ElementColor(BlockElement.Snow)
                    });
            }

            waterAnimating = true; // locks placement for the slide AND the fall
            SnowFx.PlayAvalanche(boardView, visuals, faces, new List<GridPos>(hold), bystanders,
                delegate(List<GridPos> cells, List<ClusterBurstView.Look> looks)
                {
                    FlashCells(cells, AvalancheColor, null, looks);
                },
                delegate
                {
                    // the board's own gravity: the soft slide after the heavy fall
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
                },
                null);
            return true;
        }

        // ================================================================ animation lab
        //
        // Boards of the lab's own, laid out from strings (top row first) and run through the REAL
        // rules: the snow settles with GameBoard.SettleWaterAndReact (the same call the turn makes),
        // the plan is GameBoard.PlanAvalanche, the snow is laid by GameBoard.ApplyAvalancheOnLabBoard.
        // Only the crushing - the engine's DestroyCubes in a round - is done here by hand.
        //
        // Legend: '.' empty   N plain   G gold   O obsidian   W water   F fire   X an eroded cell
        //         1..9 snow of that power (melt from the scene)   A..E snow of power 1, melt 1..5
        //         L an avalanche layer (stratum 7, power 1, three turns)   M the same, melt 2

        private Coroutine animSnow;

        private void StopAnimSnow()
        {
            if (animSnow != null)
            {
                StopCoroutine(animSnow);
                animSnow = null;
            }
            SnowView.HoldAt = SnowView.Beat.None;
            if (snow != null)
            {
                snow.Stop();
                snow.HideAim();
            }
            waterAnimating = false;
        }

        private GameBoard BuildSnowLabBoard(string[] rows, int melt)
        {
            AnimResync();
            int h = rows.Length;
            int w = rows[0].Length;
            var board = new GameBoard(w, h);
            List<int> cards = AnimBossCards();
            var dead = new List<GridPos>();
            for (int r = 0; r < h; r++)
            {
                for (int x = 0; x < w; x++)
                {
                    char c = rows[r][x];
                    int y = h - 1 - r;
                    var at = new GridPos(x, y);
                    int card = cards.Count > 0 ? cards[(x + y * 3) % cards.Count] : 1;
                    if (c >= '1' && c <= '9')
                    {
                        board.SetCubeAt(at, new Cube(CubeKind.Snow, card, false, c - '0', melt, 0));
                    }
                    else if (c >= 'A' && c <= 'E')
                    {
                        board.SetCubeAt(at, new Cube(CubeKind.Snow, card, false, 1, c - 'A' + 1, 0));
                    }
                    else if (c == 'L' || c == 'M')
                    {
                        board.SetCubeAt(at, new Cube(CubeKind.Snow, card, false, 1, c == 'L' ? 3 : 2, 7));
                    }
                    else if (c == 'X')
                    {
                        dead.Add(at);
                    }
                    else
                    {
                        CubeKind? kind = c == 'N' ? CubeKind.Normal : c == 'G' ? CubeKind.Gold
                            : c == 'O' ? CubeKind.Obsidian : c == 'W' ? CubeKind.Water
                            : c == 'F' ? CubeKind.Fire : (CubeKind?)null;
                        if (kind.HasValue)
                        {
                            board.SetCubeAt(at, new Cube(kind.Value, card));
                        }
                    }
                }
            }
            if (dead.Count > 0)
            {
                board.MarkDeadOnLabBoard(dead);
            }
            boardView.Rebuild(board, MainBoardWorldSize, MainBoardCenter);
            boardView.Refresh();
            SyncSnow(null);
            return board;
        }

        private void SnowLab(string label, IEnumerator body)
        {
            StopAnimSnow();
            animLastLabel = label;
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
            animSnow = StartCoroutine(SnowLabRun(body));
        }

        private IEnumerator SnowLabRun(IEnumerator body)
        {
            yield return body;
            animSnow = null;
        }

        /// <summary>A board standing still, for the material and the numbers.</summary>
        private void SnowLabStatic(string label, string[] rows, int melt)
        {
            SnowLab(label, SnowStaticRoutine(rows, melt));
        }

        private IEnumerator SnowStaticRoutine(string[] rows, int melt)
        {
            BuildSnowLabBoard(rows, melt);
            yield break;
        }

        /// <summary>Snow added at the top (or anywhere), then the board's own settle played.
        /// <paramref name="drops"/> are written into the board after the first look.</summary>
        private void SnowLabFall(string label, string[] rows, int melt, string[] drops, int dropMelt)
        {
            SnowLab(label, SnowFallRoutine(rows, melt, drops, dropMelt));
        }

        private IEnumerator SnowFallRoutine(string[] rows, int melt, string[] drops, int dropMelt)
        {
            GameBoard board = BuildSnowLabBoard(rows, melt);
            yield return new WaitForSeconds(0.6f);
            List<int> cards = AnimBossCards();
            for (int r = 0; r < drops.Length; r++)
            {
                for (int x = 0; x < drops[r].Length; x++)
                {
                    char c = drops[r][x];
                    var at = new GridPos(x, drops.Length - 1 - r + (board.Height - drops.Length));
                    if (c >= '1' && c <= '9')
                    {
                        board.SetCubeAt(at, new Cube(CubeKind.Snow, cards[0], false, c - '0', dropMelt, 0));
                    }
                }
            }
            boardView.Refresh();
            SyncSnow(null);
            yield return new WaitForSeconds(0.35f);
            board.ClearSnowReports();
            var frames = new List<IReadOnlyList<WaterMove>>();
            board.SettleWaterAndReact(frames);
            boardView.Refresh();
            SyncSnow(null);
            bool done = false;
            waterAnimating = true;
            boardView.PlayWaterAnimation(frames, delegate { done = true; waterAnimating = false; });
            while (!done)
            {
                yield return null;
            }
        }

        /// <summary>The snow on a board melting: a turn passes until the given cell has gone.</summary>
        private void SnowLabMelt(string label, string[] rows, int melt, int turns)
        {
            SnowLab(label, SnowMeltRoutine(rows, melt, turns));
        }

        private IEnumerator SnowMeltRoutine(string[] rows, int melt, int turns)
        {
            GameBoard board = BuildSnowLabBoard(rows, melt);
            yield return new WaitForSeconds(0.6f);
            for (int turn = 0; turn < turns; turn++)
            {
                IReadOnlyList<DestroyedCube> melted = board.TickSnowMelt();
                boardView.Refresh();
                SyncSnow(null);
                if (melted.Count > 0)
                {
                    SnowFx.PlayMelt(boardView, melted);
                }
                yield return new WaitForSeconds(0.8f);
            }
        }

        /// <summary>The aim held over one cell (or swept along a column of cells) for a while.</summary>
        private void SnowLabAim(string label, string[] rows, int melt, GridPos[] hover, float seconds)
        {
            SnowLab(label, SnowAimRoutine(rows, melt, hover, seconds));
        }

        private IEnumerator SnowAimRoutine(string[] rows, int melt, GridPos[] hover, float seconds)
        {
            GameBoard board = BuildSnowLabBoard(rows, melt);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                int at = Mathf.Min(hover.Length - 1, (int)(t / seconds * hover.Length));
                SnowFx.ShowAim(boardView, board.PlanAvalanche(hover[at]));
                yield return null;
            }
            AvalanchePlan last = board.PlanAvalanche(hover[hover.Length - 1]);
            if (!last.Any)
            {
                SnowFx.PlayRefused(boardView, last, hover[hover.Length - 1]);
            }
            SnowFx.HideAim();
        }

        /// <summary>An avalanche on a lab board, the aim shown first; <paramref name="beat"/> freezes
        /// it for the beat-isolation entries.</summary>
        private void SnowLabAvalanche(string label, string[] rows, int melt, GridPos aim,
            SnowView.Beat beat = SnowView.Beat.None, System.Action<GameBoard> prepare = null)
        {
            SnowLab(label, SnowAvalancheRoutine(rows, melt, aim, beat, prepare));
        }

        private IEnumerator SnowAvalancheRoutine(string[] rows, int melt, GridPos aim, SnowView.Beat beat,
            System.Action<GameBoard> prepare)
        {
            GameBoard board = BuildSnowLabBoard(rows, melt);
            if (prepare != null)
            {
                prepare(board);
                boardView.Refresh();
                SyncSnow(null);
            }
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime;
                SnowFx.ShowAim(boardView, board.PlanAvalanche(aim));
                yield return null;
            }
            SnowFx.HideAim();
            AvalanchePlan plan = board.PlanAvalanche(aim);
            if (!plan.Any)
            {
                SnowFx.PlayRefused(boardView, plan, aim);
                yield break;
            }
            Dictionary<GridPos, ClusterBurstView.Look> faces = FacesFor(plan);
            int paying = 0;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                foreach (DestroyedCube dead in plan.Columns[c].Crushed)
                {
                    board.DestroyCubeForced(dead.Pos);
                    if (dead.Cube.Kind != CubeKind.Snow)
                    {
                        paying++;
                    }
                }
            }
            var visuals = new AvalancheVisuals { Plan = plan, CubesPaid = paying };
            visuals.Laid.AddRange(board.ApplyAvalancheOnLabBoard(plan));
            visuals.PointsPerCube = new CigPower().PointsPerCrushedCube;
            visuals.Points = paying * visuals.PointsPerCube * session.Config.Scoring.ScoreScale;
            board.ClearSnowReports();
            var fall = new List<IReadOnlyList<WaterMove>>();
            board.SettleWaterAndReact(fall);
            boardView.Refresh();
            SyncSnow(null);
            SnowView.HoldAt = beat;
            PlayAvalancheScene(visuals, faces, fall, new List<GridPos>(), null);
            animLastLabel = Loc.Pick(
                "avalanche: " + plan.Columns.Count + " columns, " + plan.Depth + " rows, "
                    + paying + " crushed, " + visuals.Laid.Count + " cells of snow, +" + visuals.Points,
                "çığ: " + plan.Columns.Count + " sütun, " + plan.Depth + " satır, "
                    + paying + " ezildi, " + visuals.Laid.Count + " hücre kar, +" + visuals.Points);
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }

        // ---- the boards ------------------------------------------------------------------

        private static readonly string[] SnowEmpty =
        {
            ".......", ".......", ".......", ".......", ".......", ".......", ".......",
        };

        private static readonly string[] SnowAges =
        {
            ".......", ".......", ".......", ".......", ".......", ".......", "EDCBA..",
        };

        private static string[] SnowOne(string bottom)
        {
            return new[] { ".......", ".......", ".......", ".......", ".......", ".......", bottom };
        }

        /// <summary>A heap of the given power and width on row 4 over three rows of blocks.</summary>
        private static string[] SnowAvalancheBoard(char power, int width, bool blocks, bool floorGap)
        {
            var rows = new string[7];
            int first = (7 - width) / 2;
            for (int r = 0; r < 7; r++)
            {
                var row = new char[7];
                for (int x = 0; x < 7; x++)
                {
                    int y = 6 - r;
                    bool under = x >= first && x < first + width;
                    row[x] = '.';
                    if (y == 4 && under)
                    {
                        row[x] = power;
                    }
                    else if (blocks && y < 4 && (y > 0 || !floorGap))
                    {
                        row[x] = (x + y) % 3 == 0 && !under ? '.' : 'N';
                    }
                }
                rows[r] = new string(row);
            }
            return rows;
        }

        private void AddSnowLab()
        {
            AddAnimSub("powers", "snow", "SNOW BLOCK + ÇIĞ", "KAR BLOĞU + ÇIĞ");

            // ---- SNOW BLOCK ----
            AddAnim("kar 1: fresh snow, 5 turns", "kar 1: taze kar, 5 tur",
                () => SnowLabStatic("fresh, 5 turns", SnowOne("..111.."), 5));
            AddAnim("kar 2: 4 turns (settled)", "kar 2: 4 tur (oturmuş)",
                () => SnowLabStatic("settled, 4", SnowOne("..111.."), 4));
            AddAnim("kar 3: 3 turns (softening)", "kar 3: 3 tur (yumuşuyor)",
                () => SnowLabStatic("softening, 3", SnowOne("..111.."), 3));
            AddAnim("kar 4: 2 turns (wet)", "kar 4: 2 tur (ıslak)",
                () => SnowLabStatic("wet, 2", SnowOne("..111.."), 2));
            AddAnim("kar 5: 1 turn (collapsing)", "kar 5: 1 tur (çöküyor)",
                () => SnowLabStatic("collapsing, 1", SnowOne("..111.."), 1));
            AddAnim("kar 5b: all five ages side by side", "kar 5b: beş yaş yan yana",
                () => SnowLabStatic("5 4 3 2 1", SnowAges, 5));
            AddAnim("kar 6: full melt", "kar 6: tam erime",
                () => SnowLabMelt("full melt", SnowOne("..1.1.1"), 1, 1));
            AddAnim("kar 6b: ages to melt, turn by turn", "kar 6b: tur tur yaşlanıp erime",
                () => SnowLabMelt("five turns", SnowOne("..111.."), 5, 5));
            AddAnim("kar 7: single-cell gravity move", "kar 7: tek hücre düşme",
                () => SnowLabFall("one cell", new[] { ".......", ".......", ".......", ".......", ".......", "...1...", "......." }, 5,
                    new string[0], 5));
            AddAnim("kar 8: multi-cell gravity move", "kar 8: çok hücre düşme",
                () => SnowLabFall("six cells", new[] { "...1...", ".......", ".......", ".......", ".......", ".......", "......." }, 5,
                    new string[0], 5));
            AddAnim("kar 9: snow arrival on a block", "kar 9: bloğa varış",
                () => SnowLabFall("arrival", new[] { "..111..", ".......", ".......", ".......", ".......", ".......", "NNN.NNN" }, 5,
                    new string[0], 5));
            AddAnim("kar 10: normal merge", "kar 10: normal birleşme",
                () => SnowLabFall("merge", SnowOne("..111.."), 5, new[] { "..111.." }, 5));
            AddAnim("kar 11: power 1 -> 2", "kar 11: power 1 -> 2",
                () => SnowLabFall("power 1 -> 2", SnowOne("...1..."), 5, new[] { "...1..." }, 5));
            AddAnim("kar 12: power 2 -> 3", "kar 12: power 2 -> 3",
                () => SnowLabFall("power 2 -> 3", SnowOne("..222.."), 5, new[] { "..111.." }, 5));
            AddAnim("kar 13: timer 3 + incoming 5 -> 5", "kar 13: süre 3 + gelen 5 -> 5",
                () => SnowLabFall("3 + 5 = 5", SnowOne("..CCC.."), 3, new[] { "..111.." }, 5));
            AddAnim("kar 14: timer 3 + incoming 4 -> 4", "kar 14: süre 3 + gelen 4 -> 4",
                () => SnowLabFall("3 + 4 = 4", SnowOne("..CCC.."), 3, new[] { "..111.." }, 4));
            AddAnim("kar 15: timer 3 + incoming 3 -> 4", "kar 15: süre 3 + gelen 3 -> 4",
                () => SnowLabFall("3 + 3 = 4", SnowOne("..CCC.."), 3, new[] { "..111.." }, 3));
            AddAnim("kar 16: timer 3 + incoming 2 -> 3 (no refresh)", "kar 16: süre 3 + gelen 2 -> 3 (tazelenme yok)",
                () => SnowLabFall("3 + 2 = 3", SnowOne("..CCC.."), 3, new[] { "..111.." }, 2));
            AddAnim("kar 17: avalanche-layer merge barrier", "kar 17: çığ katmanı birleşme bariyeri",
                () => SnowLabFall("strata stay apart", new[] { ".......", ".......", "..LLL..", ".......", "..LLL..", "..LLL..", "..LLL.." }, 3,
                    new string[0], 5));
            AddAnim("kar 18: fresh snow merges into the top layer", "kar 18: taze kar üst katmana katılır",
                () => SnowLabFall("feeds the top stratum", new[] { ".......", ".......", ".......", ".......", "..LLL..", "..LLL..", "..LLL.." }, 3,
                    new[] { "..111.." }, 5));
            AddAnim("kar 19: the barrier remains afterwards", "kar 19: bariyer sonra da durur",
                () =>
                {
                    SnowView.Show.SnowMergeBarrier = true;
                    SnowLabFall("seam still there", new[] { ".......", ".......", ".......", ".......", "..LLL..", "..LLL..", "..LLL.." }, 3,
                        new[] { "..111.." }, 5);
                });

            // ---- AVALANCHE AIM ----
            AddAnim("çığ 20: power card activation", "çığ 20: güç kartı aktivasyonu",
                () => SnowLabAim("activation", SnowAvalancheBoard('3', 3, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 21: row selector sweeping", "çığ 21: satır seçici gezer",
                () => SnowLabAim("row selector", SnowAvalancheBoard('3', 3, true, false), 5,
                    new[] { new GridPos(3, 0), new GridPos(3, 1), new GridPos(3, 2), new GridPos(3, 3), new GridPos(3, 4), new GridPos(3, 5), new GridPos(3, 4) }, 4f));
            AddAnim("çığ 22: eligible row", "çığ 22: uygun satır",
                () => SnowLabAim("eligible", SnowAvalancheBoard('3', 3, true, false), 5, new[] { new GridPos(0, 4) }, 2.5f));
            AddAnim("çığ 23: invalid row", "çığ 23: geçersiz satır",
                () => SnowLabAim("invalid", SnowAvalancheBoard('3', 3, true, false), 5, new[] { new GridPos(0, 2) }, 2f));
            AddAnim("çığ 24: power 1 footprint", "çığ 24: power 1 izi",
                () => SnowLabAim("power 1", SnowAvalancheBoard('1', 3, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 25: power 2 footprint", "çığ 25: power 2 izi",
                () => SnowLabAim("power 2", SnowAvalancheBoard('2', 3, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 26: power 3 footprint", "çığ 26: power 3 izi",
                () => SnowLabAim("power 3", SnowAvalancheBoard('3', 3, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 27: 1x1 snow pile", "çığ 27: 1x1 kar öbeği",
                () => SnowLabAim("1x1", SnowAvalancheBoard('3', 1, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 28: 1x3 snow pile", "çığ 28: 1x3 kar öbeği",
                () => SnowLabAim("1x3", SnowAvalancheBoard('3', 3, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 29: long snow group", "çığ 29: uzun kar grubu",
                () => SnowLabAim("1x7", SnowAvalancheBoard('2', 7, true, false), 5, new[] { new GridPos(3, 4) }, 2.5f));
            AddAnim("çığ 30: spent row", "çığ 30: harcanmış satır",
                () => SnowLabAim("spent", new[] { ".......", ".......", "..LLL..", "..NNN..", "N.NNN.N", "NNNNNNN", "NNNNNNN" }, 3,
                    new[] { new GridPos(3, 4) }, 2.5f));

            // ---- AVALANCHE ----
            AddAnim("çığ 31: power 1, empty cells", "çığ 31: power 1, boş hücreler",
                () => SnowLabAvalanche("p1 empty", SnowAvalancheBoard('1', 3, false, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 32: power 1, occupied cells", "çığ 32: power 1, dolu hücreler",
                () => SnowLabAvalanche("p1 occupied", SnowAvalancheBoard('1', 3, true, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 33: power 3, empty cells", "çığ 33: power 3, boş hücreler",
                () => SnowLabAvalanche("p3 empty", SnowAvalancheBoard('3', 3, false, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 34: power 3, occupied cells", "çığ 34: power 3, dolu hücreler",
                () => SnowLabAvalanche("p3 occupied", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 35: the 1x3 power 3 example", "çığ 35: 1x3 power 3 örneği",
                () => SnowLabAvalanche("1x3 p3", SnowAvalancheBoard('3', 3, true, true), 5, new GridPos(3, 4)));
            AddAnim("çığ 36: anticipation only", "çığ 36: sadece basınç toplama",
                () => SnowLabAvalanche("anticipation", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.Anticipation));
            AddAnim("çığ 37: single row impact", "çığ 37: tek satır çarpması",
                () => SnowLabAvalanche("first impact", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.FirstFill));
            AddAnim("çığ 38: block pressure", "çığ 38: blok basıncı",
                () => SnowLabAvalanche("pressure", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.FirstPressure));
            AddAnim("çığ 39: block destruction", "çığ 39: blok kırılması",
                () => SnowLabAvalanche("destruction", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.FirstDestroy));
            AddAnim("çığ 40: snow fill", "çığ 40: kar dolması",
                () => SnowLabAvalanche("fill", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.FirstFill));
            AddAnim("çığ 41: full chain descent", "çığ 41: tam zincir iniş",
                () => SnowLabAvalanche("chain", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 42: final settle (held)", "çığ 42: son oturma (bekler)",
                () => SnowLabAvalanche("settle", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.Settle));
            AddAnim("çığ 43: score collection (held, RESET to finish)", "çığ 43: puan toplama (bekler, SIFIRLA)",
                () => SnowLabAvalanche("score", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4), SnowView.Beat.Score));
            AddAnim("çığ 44: post-avalanche gravity", "çığ 44: çığ sonrası yerçekimi",
                () => SnowLabAvalanche("then gravity", SnowAvalancheBoard('3', 3, true, true), 5, new GridPos(3, 4)));
            AddAnim("çığ 45: 3-turn avalanche snow", "çığ 45: 3 turluk çığ karı",
                () =>
                {
                    SnowView.Show.SnowMeltTimer = true;
                    SnowView.Show.SnowStrataId = true;
                    SnowLabAvalanche("3-turn strata", SnowAvalancheBoard('3', 3, true, false), 5, new GridPos(3, 4));
                });

            // ---- STRESS ----
            AddAnim("çığ 46: wide avalanche", "çığ 46: geniş çığ",
                () => SnowLabAvalanche("wide", SnowAvalancheBoard('2', 7, true, false), 5, new GridPos(3, 4)));
            AddAnim("çığ 47: high power", "çığ 47: yüksek power",
                () => SnowLabAvalanche("power 6", new[] { ".......", "..666..", "..NNN..", "..NNN..", "..NNN..", "..NNN..", "..NNN.." }, 5, new GridPos(3, 5)));
            AddAnim("çığ 48: many blocks destroyed", "çığ 48: çok blok ezilir",
                () => SnowLabAvalanche("many", new[] { ".......", ".......", "4444444", "NNNNNNN", "NNNNNNN", "NNNNNNN", "NNNNNNN" }, 5, new GridPos(3, 4)));
            AddAnim("çığ 49: mixed block materials", "çığ 49: karışık malzemeler",
                () => SnowLabAvalanche("mixed", new[] { ".......", ".......", ".33333.", ".GOWFN.", ".NFGON.", ".OWNGF.", "NNNNNNN" }, 5, new GridPos(3, 4)));
            AddAnim("çığ 50: several heaps on one row", "çığ 50: aynı satırda birkaç öbek",
                () => SnowLabAvalanche("groups", new[] { ".......", ".......", "22.3.11", "NNNNNNN", "NN.NNNN", "NNNNNNN", "NNNNNNN" }, 5, new GridPos(0, 4)));
            AddAnim("çığ 51: board with holes / erosion", "çığ 51: delikli / aşınmış alan",
                () => SnowLabAvalanche("eroded", new[] { ".......", ".......", "..333..", "..NXN..", "..N.N..", "..NNX..", "NNNNNNN" }, 5, new GridPos(3, 4)));
            AddAnim("çığ 52: gravity turned left", "çığ 52: yerçekimi sola",
                () => SnowLabAvalanche("left", new[] { ".......", "NNN...3", "NNN...3", "NNNN..3", ".......", ".......", "......." }, 5, new GridPos(6, 4),
                    SnowView.Beat.None, b => b.SetWaterFlowOnLabBoard(new GridPos(-1, 0))));
            AddAnim("çığ 53: mobile quality (Low)", "çığ 53: mobil kalite (Düşük)",
                () =>
                {
                    SnowView.Level = SnowView.Quality.Low;
                    SnowLabAvalanche("low quality", SnowAvalancheBoard('3', 5, true, false), 5, new GridPos(3, 4));
                });
            AddAnim("çığ: quality back to High", "çığ: kalite Yüksek'e dön",
                () => { SnowView.Level = SnowView.Quality.High; animLastLabel = "High"; });

            // ---- DEBUG ----
            AddAnim("debug: ShowSnowPower", "debug: ShowSnowPower", () => SnowToggle(ref SnowView.Show.SnowPower, "ShowSnowPower"));
            AddAnim("debug: ShowSnowMeltTimer", "debug: ShowSnowMeltTimer", () => SnowToggle(ref SnowView.Show.SnowMeltTimer, "ShowSnowMeltTimer"));
            AddAnim("debug: ShowSnowGravityPath", "debug: ShowSnowGravityPath", () => SnowToggle(ref SnowView.Show.SnowGravityPath, "ShowSnowGravityPath"));
            AddAnim("debug: ShowSnowMergeTarget", "debug: ShowSnowMergeTarget", () => SnowToggle(ref SnowView.Show.SnowMergeTarget, "ShowSnowMergeTarget"));
            AddAnim("debug: ShowSnowMergeBarrier", "debug: ShowSnowMergeBarrier", () => SnowToggle(ref SnowView.Show.SnowMergeBarrier, "ShowSnowMergeBarrier"));
            AddAnim("debug: ShowSnowStrataId", "debug: ShowSnowStrataId", () => SnowToggle(ref SnowView.Show.SnowStrataId, "ShowSnowStrataId"));
            AddAnim("debug: ShowAvalancheEligibleRows", "debug: ShowAvalancheEligibleRows", () => SnowToggle(ref SnowView.Show.AvalancheEligibleRows, "ShowAvalancheEligibleRows"));
            AddAnim("debug: ShowAvalancheSpentRows", "debug: ShowAvalancheSpentRows", () => SnowToggle(ref SnowView.Show.AvalancheSpentRows, "ShowAvalancheSpentRows"));
            AddAnim("debug: ShowAvalanchePreviewCells", "debug: ShowAvalanchePreviewCells", () => SnowToggle(ref SnowView.Show.AvalanchePreviewCells, "ShowAvalanchePreviewCells"));
            AddAnim("debug: ShowAvalancheDestructionCells", "debug: ShowAvalancheDestructionCells", () => SnowToggle(ref SnowView.Show.AvalancheDestructionCells, "ShowAvalancheDestructionCells"));
            AddAnim("debug: ShowAvalancheLayerIndex", "debug: ShowAvalancheLayerIndex", () => SnowToggle(ref SnowView.Show.AvalancheLayerIndex, "ShowAvalancheLayerIndex"));
            AddAnim("debug: ShowAvalancheFront", "debug: ShowAvalancheFront", () => SnowToggle(ref SnowView.Show.AvalancheFront, "ShowAvalancheFront"));
            AddAnim("debug: ShowAvalancheTiming", "debug: ShowAvalancheTiming", () => SnowToggle(ref SnowView.Show.AvalancheTiming, "ShowAvalancheTiming"));
            AddAnim("debug: ShowScoreEssence", "debug: ShowScoreEssence", () => SnowToggle(ref SnowView.Show.ScoreEssence, "ShowScoreEssence"));
            AddAnim("debug: ShowParticleBudget", "debug: ShowParticleBudget", () => SnowToggle(ref SnowView.Show.ParticleBudget, "ShowParticleBudget"));
            AddAnim("debug: all snow views off", "debug: tüm kar görünümleri kapalı",
                () => { SnowView.Show.AllOff(); animLastLabel = "snow debug off"; });
        }

        private void SnowToggle(ref bool flag, string name)
        {
            flag = !flag;
            animLastLabel = name + (flag ? " ON" : " OFF");
            if (AnimLabOpen)
            {
                RedrawAnimationLab();
            }
        }
    }
}
