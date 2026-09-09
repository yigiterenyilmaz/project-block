// PURPOSE: GameUiController post-placement feedback - finalizing a placement, explosion
// and blast FX, camera shake, full refresh, infection marks, the HUD, and turn logs.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Everything the view does AFTER a card is played, shared by the drag path and
        /// the retro falling-piece path: logging, sounds, a full refresh, then the water/explosion
        /// feedback (water falls first, boom next, post-explosion falls last) and the sweeper.</summary>
        private void FinalizePlacement(RoundEngine round, TurnReport report)
        {
            if (verboseTurnLogs)
            {
                LogTurn(report);
            }
            sfx.Place();
            if (report.PlayedCardExpired)
            {
                sfx.Vanish();
            }
            if (report.DiscardWasReshuffled)
            {
                sfx.Shuffle();
            }
            RefreshAll(report);
            IReadOnlyList<IReadOnlyList<WaterMove>> frames = report.WaterFallFrames;
            if (frames.Count == 0)
            {
                PlayExplosionFeedback(round, report);
            }
            else
            {
                // Water falls first, THEN the boom it caused - and any post-explosion
                // falls play after the boom (WaterFramesBeforeExplosion splits them).
                int boomAt = Mathf.Clamp(report.WaterFramesBeforeExplosion, 0, frames.Count);
                var preFall = new List<IReadOnlyList<WaterMove>>();
                var postFall = new List<IReadOnlyList<WaterMove>>();
                for (int f = 0; f < frames.Count; f++)
                {
                    (f < boomAt ? preFall : postFall).Add(frames[f]);
                }
                waterAnimating = true;
                boardView.PlayWaterAnimation(preFall, delegate
                {
                    PlayExplosionFeedback(round, report);
                    boardView.PlayWaterAnimation(postFall,
                        delegate { waterAnimating = false; });
                });
            }
            TriggerSupurgeBlast();
            TriggerInfectionBlast();
            ShowShellGameReveal(round);
            // A resolved turn is the natural save point: the per-turn scratch state is at rest,
            // which is exactly what the save format assumes (see RoundEngine.Save).
            AutoSave();
        }

        /// <summary>
        /// "Şaşırtmaca": after the turn, the hand the player was holding is laid face up in
        /// front of them for a beat. The engine has already mixed it and dealt it face down, so
        /// this is the one moment they get to see what they have - never where it is.
        /// </summary>
        private void ShowShellGameReveal(RoundEngine round)
        {
            var boss = round.Boss as SasirtmacaBoss;
            if (boss == null || boss.HandBeforeMix.Count == 0)
            {
                return;
            }
            // The ids are of cards that are still held (the mix reorders, it never removes), so
            // they are looked up in the hand itself.
            var shown = new List<BlockCard>();
            foreach (int id in boss.HandBeforeMix)
            {
                for (int i = 0; i < round.Hand.Count; i++)
                {
                    if (round.Hand[i].Id == id)
                    {
                        shown.Add(round.Hand[i]);
                        break;
                    }
                }
            }
            cardLayer.ShowRevealBeat(shown, ShellGameRevealSeconds);
        }

        private const float ShellGameRevealSeconds = 1.1f;

        /// <summary>
        /// "Tamagotchi": hands the pet whatever hand card the cursor is over. Costs no turn, so
        /// it is a key rather than a mode - and it says why when the pet turns a card down, since
        /// "nothing happened" is indistinguishable from a bug.
        /// </summary>
        private void TryFeedPetUnderCursor(RoundEngine round, Mouse mouse)
        {
            var pet = round.Boss as TamagotchiBoss;
            if (pet == null || mouse == null)
            {
                return;
            }
            Vector2 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            CardVisual hit = cardLayer.CardAt(world);
            if (hit == null || hit.SlotIndex < 0 || hit.SlotIndex >= round.Hand.Count)
            {
                messageText.text = Loc.Pick("Point at a card in your hand to feed it.",
                    "Beslemek için elindeki bir kartı işaret et.");
                return;
            }
            if (!round.FeedPet(hit.SlotIndex))
            {
                messageText.text = Loc.Pick("It does not want that shape.",
                    "O şekli istemiyor.");
                return;
            }
            sfx.Vanish();
            FloatingTextFx.Spawn(transform, world, Loc.Pick("FED", "YEDİ"),
                new Color(0.6f, 0.9f, 0.5f), 54, 0.05f);
            RefreshAll(null);
        }

        /// <summary>Explosion sound + blast feedback for one turn. Deferred until after the
        /// pre-explosion water falls when the flow is what completed the line.</summary>
        private void PlayExplosionFeedback(RoundEngine round, TurnReport report)
        {
            if (report.CleanSweep)
            {
                // the sweep bling rises in pitch with every sweep this round
                sfx.CleanSweep(1f + 0.12f * Mathf.Min(round.CleanSweepCount - 1, 8));
                sfx.Flame();
            }
            else if (report.CubesExploded > 0 || LateExplodedCount(report) > 0)
            {
                // The late lists cover a board-reshape clear (inflation deflate) and a "Hedefli"
                // payout - neither of which the placement's own CubesExploded count ever saw.
                sfx.Explode();
            }
            // "Alacakaranlık": the two lights of a blind round. Setting a block DOWN lights what
            // it touches, faintly and one cell out - enough to confirm what your hand just did,
            // not enough to survey the board with. A BLAST lights far more and far brighter, so
            // making something happen stays the only way to actually see. The placement goes in
            // first, so a blast on the same turn overwrites it with its own brighter light
            // rather than the other way round.
            if (boardView.IsDark)
            {
                // PlacedCells only: MirrorPlacedCells are coordinates in the OTHER world and
                // would light the wrong squares of this one.
                boardView.LightUpPlacement(report.PlacedCells);
                var lit = new List<GridPos>();
                foreach (DestroyedCube dead in report.DestroyedCubes)
                {
                    lit.Add(dead.Pos);
                }
                lit.AddRange(report.ExtraExplodedCells);
                lit.AddRange(report.TargetedExplodedCells);
                boardView.LightUpAround(lit);
            }
            // "İstilacı": the marked column came due. Its cubes were not exploded - they were
            // taken - so they go to the corridor's own extraction rather than through FlashCells.
            if (report.ColumnSweptCells.Count > 0)
            {
                boardView.PlayColumnExtraction(SweptCubes(report));
            }
            // "Karantina": a cube that broke inside a sealed zone cost the player exactly what
            // it would have paid. The membrane over it answers - the view decides for itself
            // whether the cell was in a zone, so this asks about every cube and nothing here
            // needs to know the boss exists.
            foreach (DestroyedCube dead in report.DestroyedCubes)
            {
                boardView.PlayQuarantineReaction(dead.Pos);
                // "Besleme": a cube broken inside the nest is FOOD, and the pool reacts to being
                // fed. Same shape as the quarantine reaction - ask about every cube and let the
                // view decide whether that cell belongs to anything.
                boardView.PlayCreatureFeed(dead.Pos);
            }
            HandleBlastFeedback(round, report);
        }

        /// <summary>Cells this turn lost OUTSIDE the placement's own line explosion - a late
        /// board-reshape clear and a "Hedefli" payout. They live in separate lists because a
        /// joker bills against one of them, but every FX decision here treats them alike.</summary>
        private static int LateExplodedCount(TurnReport report)
        {
            return report.ExtraExplodedCells.Count + report.TargetedExplodedCells.Count
                + report.CircuitExplodedCells.Count;
        }

        /// <summary>Particles, shake, combo popups and the sweep celebration for one turn.</summary>
        private void HandleBlastFeedback(RoundEngine round, TurnReport report)
        {
            // Settled BEFORE anything is drawn: EmitBlastParticles reads the tier below, and on
            // a turn that cleared nothing it still runs (a boss lift has a puff to draw).
            // Turns played PAST the bar, which is what the heartbeat's rate follows. Counted from
            // the turn overtime began on rather than incremented, so it cannot drift if a turn
            // ever resolves twice.
            if (round.ThresholdPassed)
            {
                if (overtimeStartTurn < 0)
                {
                    overtimeStartTurn = report.TurnNumber;
                }
                overtimeTurns = Mathf.Max(0, report.TurnNumber - overtimeStartTurn);
            }

            bool clearedALine = report.ExplodedRows.Count > 0 || report.ExplodedColumns.Count > 0;
            activeLineTier = lineBurstTier;
            lineBurstTier = clearedALine
                ? Mathf.Min(lineBurstTier + 1, LineBurstView.MaxTier)
                : Mathf.Max(lineBurstTier - 1, 1);

            if (report.CubesExploded == 0 && LateExplodedCount(report) == 0)
            {
                comboStreak = 0;
                // A turn where a boss only LIFTED cells still needs its puff drawn - it just
                // breaks no combo and shakes no camera, because nothing exploded.
                EmitBlastParticles(round, report);
                return;
            }
            comboStreak++;
            EmitBlastParticles(round, report);
            ShakeForBlast(report.DynamiteTriggered, report.CleanSweep, comboStreak);
            if (report.DynamiteTriggered)
            {
                FlashDynamite(DynamiteCenter(report));
                SpawnDynamitePopup();
            }
            // The popup shows the SCORING combo (consecutive line-clearing turns), which is
            // what actually pays out - not the destruction-only comboStreak that drives shake.
            if (report.ComboCount >= 2)
            {
                SpawnComboPopup(report.ComboCount);
            }
            if (report.CleanSweep)
            {
                SpawnSweepPopup();
            }
            if (report.TargetedBlocksHit.Count > 0)
            {
                SpawnTargetPopup();
            }
        }

        // ---- the blast's condition-driven decisions, each in one place ----
        //
        // Split out of HandleBlastFeedback so the ANIMATION LAB (F3) can fire them with a
        // chosen combo streak instead of the one the round happens to be on. The lab drives
        // these very methods, so retuning an amplitude or a popup colour here is visible there
        // immediately - a copy in the lab would have drifted the first time one changed.

        /// <summary>Camera shake for a blast. A dynamite board-clear shakes hardest, a clean
        /// sweep next, an ordinary line clear least - and all three grow with the destruction
        /// streak, up to five turns deep.
        ///
        /// A blast is an IMPACT, so this one is deliberately front-loaded: it peaks higher than
        /// an even shake of the same energy and is gone in well under a fifth of a second, which
        /// is what makes it land with the ray instead of wobbling on behind it.</summary>
        private void ShakeForBlast(bool dynamite, bool sweep, int streak)
        {
            float amplitude = dynamite ? 0.3f : sweep ? 0.22f : 0.13f;
            amplitude *= 1f + 0.25f * Mathf.Min(Mathf.Max(streak, 1) - 1, 5);
            ShakeCamera(amplitude, 0.14f, 2.4f);
        }

        /// <summary>A dynamite board clear takes every destructible cube there is, so the whole
        /// arena strikes - in dynamite's own red, which is also the popup's - and then the smoke
        /// rolls in over the screen. Nothing drew this before: its cubes go through DestroyCubes
        /// rather than a line explosion, so they appear in no exploded row or column and simply
        /// blinked out of existence.</summary>
        /// <summary>The TNT detonation, at the block that blew. NOT FlashBoard: that strikes
        /// every cell on a schedule measured from the middle of the BOARD, so the whole arena
        /// lit the same red at once around a point the bomb had nothing to do with - a damage
        /// flash rather than an explosion. DynamiteBlastView runs the light out from HERE with
        /// a falloff, and the camera kick lands on its detonation beat rather than 45ms early.</summary>
        private void FlashDynamite(Vector2 at)
        {
            if (boardView == null || boardView.Board == null)
            {
                return;
            }
            dynamiteBlast.Play(boardView, boardView.Board, at,
                ViewUtil.ElementColor(BlockElement.Dynamite),
                delegate
                {
                    ShakeCamera(DynamiteBlastView.Style.CameraShakeStrength,
                        DynamiteBlastView.Style.CameraShakeDuration,
                        DynamiteBlastView.Style.CameraShakeDamping);
                });
        }

        /// <summary>Where the bomb was. The rule is that the block which detonates is the one
        /// PLACED THIS TURN, so the placed cells ARE the blast centre - no Core change was
        /// needed to find it, and the board middle is only a fallback for the lab.</summary>
        private Vector2 DynamiteCenter(TurnReport report)
        {
            IReadOnlyList<GridPos> placed = report != null ? report.PlacedCells : null;
            if (boardView == null)
            {
                return Vector2.zero;
            }
            if (placed == null || placed.Count == 0)
            {
                return boardView.WorldRect.center;
            }
            var sum = Vector2.zero;
            for (int i = 0; i < placed.Count; i++)
            {
                sum += boardView.CellToWorld(placed[i]);
            }
            return sum / placed.Count;
        }

        /// <summary>The smoke a detonation leaves. Its own method so the animation lab can fire
        /// it alone; thrown from the board middle there, since the lab has no bomb.</summary>
        private void PlayDynamiteSmoke()
        {
            if (boardView == null)
            {
                return;
            }
            SmokeFx.Burst(transform, boardView.WorldRect.center, boardView.CellWorldSize);
        }

        /// <summary>The world rectangle the camera can currently see - what a full-screen effect
        /// has to cover. Read live rather than cached: the shake moves the camera.</summary>
        private Rect CameraWorldRect()
        {
            if (cam == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 at = camBasePosition;
            return new Rect(at.x - halfWidth, at.y - halfHeight,
                halfWidth * 2f, halfHeight * 2f);
        }

        /// <summary>
        /// A block the player genuinely aimed at the arena was REFUSED by the board. Normally
        /// that is free and silent - the card just goes home - but a boss may bill for it
        /// ("Alacakaranlık", where what turned the block away is exactly what the dark hid), so
        /// the ONE question is put to the engine and the answer is what decides whether anything
        /// is said. Every driver that can refuse a placement calls this, so the price is the same
        /// whether the block was dragged, walked there on a pad, or dropped by the retro piece.
        ///
        /// It is called only for a drop AT the board: letting go of a card away from the arena,
        /// or putting it back, is a cancel and must never cost anything.
        /// </summary>
        private void RejectPlacement(RoundEngine round, Vector2 world)
        {
            // Already in the scaled economy, so it is printed as it comes: every score on the
            // HUD is scaled, and a popup that showed the logical number would read as a tenth of
            // what the meter just lost.
            int charged = round.ChargeIllegalPlacement();
            if (charged <= 0)
            {
                return;
            }
            FloatingTextFx.Spawn(transform, world, "-" + charged,
                new Color(1f, 0.35f, 0.35f), 56, 0.07f);
            UpdateHud();
        }

        private void SpawnDynamitePopup()
        {
            FloatingTextFx.Spawn(transform, new Vector2(0f, 3.4f),
                Loc.Pick("DYNAMITE!", "DİNAMİT!"), new Color(0.95f, 0.3f, 0.2f), 72, 0.09f);
        }

        private void SpawnComboPopup(int comboCount)
        {
            FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                Loc.Pick("COMBO x", "KOMBO x") + comboCount + "!",
                new Color(1f, 0.6f, 0.2f), 64, 0.08f);
        }

        private void SpawnSweepPopup()
        {
            FloatingTextFx.Spawn(transform, new Vector2(0f, 1.4f),
                Loc.Pick("CLEAN SWEEP!", "TEMİZLİK!"), new Color(1f, 0.85f, 0.3f), 80, 0.1f);
        }

        /// <summary>"Hedefli": the aim paid off. Shown in the block's own lime, so the popup and
        /// the cube the player was aiming at are obviously the same thing.</summary>
        private void SpawnTargetPopup()
        {
            FloatingTextFx.Spawn(transform, new Vector2(0f, 2.0f),
                Loc.Pick("TARGET HIT!", "HEDEF VURULDU!"),
                ViewUtil.ElementColor(BlockElement.Targeted), 68, 0.09f);
        }

        private void EmitBlastParticles(RoundEngine round, TurnReport report)
        {
            // A cleared LINE gets the ray (see FlashLine); everything else below is a loose
            // handful of cells, which has no direction to fire along and so just puffs.
            // MIND THE COORDINATES: ExplodedRows/Columns are 0-BASED ARRAY INDICES, while a
            // GridPos is absolute - the board's origin can sit anywhere once something has
            // inflated it, so MinX/MinY go back on here.
            foreach (int y in report.ExplodedRows)
            {
                FlashLine(round.Board, round.Board.MinY + y, true);
            }
            foreach (int x in report.ExplodedColumns)
            {
                FlashLine(round.Board, round.Board.MinX + x, false);
            }
            // Late board-reshape clears (inflation deflate, board powers) blast their exact
            // absolute cells - ExplodedRows/Columns never covered them. The board has already
            // been rebuilt to its new size by RefreshAll, so CellToWorld maps these correctly.
            FlashCells(report.ExtraExplodedCells, BlastColor, 4);
            // A "Hedefli" payout keeps its cells in a list of its own (so "Antimadde" cannot be
            // billed for them). It goes off in the lime that belongs to nothing else on the
            // board, so the cube the player was aiming at is what they see break.
            FlashCells(report.TargetedExplodedCells,
                ViewUtil.ElementColor(BlockElement.Targeted), 4);
            // Cells a BOSS lifted off rather than destroyed ("Alzheimer" forgetting a card,
            // "Yürüyen merdiven" carrying a row away). Pale and COLD - it never strikes bright,
            // because nothing blew up and nothing was earned.
            FlashCells(report.LiftedCells, LiftedColor, 3, cold: true);
            // "Kaçakçı" defective goods: the cubes showed up and then let go. They fall through
            // the arena and off the bottom of the screen - nothing landed, so there is nothing to
            // blast, only something to drop.
            DropFellThroughCubes(report);
            if (report.CleanSweep)
            {
                EmitSweepConfetti();
            }
        }

        /// <summary>The warm orange every blast is drawn in. Still the colour of everything
        /// that is not a cleared LINE - loose cells, a targeted payout, a late reshape - which
        /// have no streak of their own to be at a tier of.</summary>
        private static readonly Color BlastColor = new Color(1f, 0.72f, 0.35f);

        /// <summary>A cleared line's colour, by streak tier, indexed from 1. The SQUARES and the
        /// burst over them are two halves of one thing, so this table is read through
        /// LineBurstView.EffectiveTier and a tier can never flash one colour under another
        /// tier's debris. Fill in a colour here at the same time as the sheet, not before.
        ///
        /// TIER 3 IS NOT THE GREEN ITS SHEET IS MOSTLY PAINTED IN, deliberately. That green sits
        /// at hue 133, which is the infection blast below to within 0.14 in RGB - the two would
        /// have been the same colour, and on this board colour is what says WHICH destruction
        /// you are looking at. The sheet is two-toned, though: a green body under pale
        /// yellow-green spikes, and about a quarter of its lit pixels are that second colour.
        /// Taking the flash from the spikes instead lands 0.39 from the infection and 0.39 from
        /// tier 1's orange - the best separation available without repainting the art, and still
        /// a colour that is honestly in it. Anything nearer hue 130 collapses back onto the
        /// infection; anything nearer 70 collapses onto the orange.</summary>
        private static readonly Color[] LineBlastColors =
        {
            default(Color),
            new Color(1f, 0.72f, 0.35f),      // 1 - the ordinary clear, the warm orange
            new Color(0.72f, 0.38f, 1f),      // 2 - a clear straight after another: purple
            new Color(0.73f, 1f, 0.36f)       // 3 - the deepest streak: the sheet's spike lime
        };

        /// <summary>The streak tier the NEXT cleared line plays at, 1-based.
        ///
        /// The rule, which is not a plain reset: a turn that clears plays at this tier and then
        /// raises it; a turn that clears NOTHING lowers it by one. So three clears in a row run
        /// 1, 2, 3 and stay at 3 however long the run goes on, while a single missed turn after
        /// that drops you to 2 rather than all the way back - you have to miss twice to be at 1
        /// again. Losing a streak gradually is the point: one bad placement should cost a step,
        /// not the whole ladder.
        ///
        /// It lives here rather than in Core because nothing about it is a rule - no score, no
        /// legality, only which of three drawings to play. `report.ComboCount` is the SCORING
        /// combo and does reset outright; the two are deliberately not the same number.</summary>
        private int lineBurstTier = 1;

        /// <summary>The tier THIS turn's lines are drawn at - lineBurstTier as it stood when the
        /// turn was classified, held because EmitBlastParticles walks several lines and the
        /// counter has already moved on by then. The animation lab writes it directly.</summary>
        private int activeLineTier = 1;

        /// <summary>Cells a boss carried away rather than broke: cold and pale, so a lift can
        /// never be mistaken for something the player earned.</summary>
        private static readonly Color LiftedColor = new Color(0.62f, 0.68f, 0.82f, 0.9f);

        /// <summary>
        /// ONE cleared line: the ray out of its middle. <paramref name="line"/> is an ABSOLUTE
        /// row or column coordinate, like a GridPos - the two callers that hold a 0-based report
        /// index add the board's origin back before calling.
        ///
        /// The line's extent is taken from its outermost cells that are really play area, so a
        /// ray fired along an irregular board (Kentsel Dönüşüm's bolted-on cells, a "Dört kutup"
        /// quarter) stops where the board does instead of shooting off into a hole.
        /// </summary>
        private void FlashLine(GameBoard board, int line, bool row)
        {
            if (board == null || boardView == null || boardView.Board == null)
            {
                return;
            }
            var cells = new List<Vector2>();
            int count = row ? board.Width : board.Height;
            for (int i = 0; i < count; i++)
            {
                GridPos pos = row
                    ? new GridPos(board.MinX + i, line)
                    : new GridPos(line, board.MinY + i);
                if (board.IsInside(pos))
                {
                    cells.Add(boardView.CellToWorld(pos));
                }
            }
            if (cells.Count == 0)
            {
                return;
            }
            // The sweep and the burst take the SAME tier, through the same fallback, so the
            // two can never disagree about which streak the player is on.
            int tier = LineBurstView.EffectiveTier(activeLineTier);
            Color tone = LineBlastColors[tier];
            // A cleared LINE does not go through CellFlashFx and throws no code particles,
            // unlike every other destruction. Both were built for a loose handful of cells: a
            // whole row of struck squares is a rectangle, and the sheet already flies its own
            // debris out of the middle. What a line gets instead is LineSweepView - energy
            // leaving the centre for both ends, with each slot answering as it passes - under
            // the drawn burst. FlashCells keeps the squares and the sparks, because a loose
            // group has neither a direction nor a drawing of its own.
            lineSweep.Play(cells, boardView.CellWorldSize, row, tone);
            lineBurst.Play(cells, boardView.CellWorldSize, row, activeLineTier);
        }

        /// <summary>
        /// EVERY destruction that is not a line: a late board-reshape clear, a "Hedefli" payout,
        /// a power blast, the sweeper, an infection going off, cubes a boss lifted away. They
        /// strike in the same language a cleared line does - the squares themselves going off -
        /// only rippling out from the middle of the group instead of along an axis, and in the
        /// colour that destruction already owns.
        ///
        /// Cells outside the board are dropped: a turn that also eroded the arena can name a
        /// cell that is no longer there. Returns whether anything was drawn, which is what the
        /// callers that also make a sound or shake the camera decide on.
        /// </summary>
        private bool FlashCells(IReadOnlyList<GridPos> cells, Color tone, int particlesPerCell,
            bool cold = false)
        {
            if (cells == null || cells.Count == 0 || boardView == null || boardView.Board == null)
            {
                return false;
            }
            var world = new List<Vector2>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                if (boardView.Board.IsInside(cells[i]))
                {
                    world.Add(boardView.CellToWorld(cells[i]));
                }
            }
            if (world.Count == 0)
            {
                return false;
            }
            float[] times = CellFlashFx.BurstTimes(world);
            CellFlashFx.Play(transform, world, times, boardView.CellWorldSize,
                CellFlashFx.Pinch.Uniform,
                cold ? CellFlashFx.Palette.Cold(tone) : CellFlashFx.Palette.Hot(tone));
            StartCoroutine(BurstParticles(world, times, tone, particlesPerCell));
            return true;
        }

        /// <summary>
        /// The cubes' own particles going off UNDER the travelling wave instead of all on one
        /// frame, so the destruction visibly runs in a direction. This is what gives a blast its
        /// tactility, and the ray in particular its two visible tips.
        ///
        /// The schedule is the flash's own, handed in rather than recomputed, so the sparks can
        /// never drift out of step with the squares. World positions are taken up front: the
        /// wave outlives the frame it started on, and a board that rebuilds under it (an
        /// inflation power resolving late) must not drag the sparks somewhere else.
        /// </summary>
        private IEnumerator BurstParticles(List<Vector2> cells, float[] times, Color tone,
            int perCell)
        {
            var due = new List<KeyValuePair<float, Vector2>>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                due.Add(new KeyValuePair<float, Vector2>(times[i], cells[i]));
            }
            due.Sort(delegate (KeyValuePair<float, Vector2> a, KeyValuePair<float, Vector2> b)
            {
                return a.Key.CompareTo(b.Key);
            });
            // The last cells to go are the ones the wave is standing on as it hits the far end -
            // the loudest moment of the flash - so they throw a hotter, heavier spark.
            Color tipTone = Color.Lerp(tone, Color.white, 0.55f);
            float elapsed = 0f;
            int next = 0;
            while (next < due.Count)
            {
                while (next < due.Count && due[next].Key <= elapsed)
                {
                    bool tip = next >= due.Count - 2;
                    blastFx.EmitAt(due[next].Value, tip ? tipTone : tone,
                        tip ? perCell + 3 : perCell);
                    next++;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        /// <summary>
        /// The clean-sweep celebration: the WHOLE arena strikes, rippling out from its centre,
        /// and then the spark shower rains over it. The board flash is the same one a blast uses
        /// - it is the emptied board itself going off, one square at a time - and its colour has
        /// to layer legibly over the ray that just cleared the last line rather than doubling it.
        /// The cyan is taken from the shower's own art (hue 188) and is 0.97 from tier 1's
        /// orange in RGB; the gold that used to be here was 0.14 from it, which is to say the
        /// same colour. A sweep is now the one blast on this board that is COLD, which is the
        /// clearest thing it could be against a game whose destruction is all warm.
        ///
        /// Its own method so the animation lab can fire it alone (see ShakeForBlast).
        /// </summary>
        private void EmitSweepConfetti()
        {
            var cyan = new Color(0.30f, 0.91f, 1f);
            // NOT FlashBoard. A sweep is the one event about the WHOLE arena, and FlashBoard
            // fills each square in turn - correct for a dynamite clear, a paint bucket here.
            // BoardCleanseView runs a shockwave out of the middle instead: cells REACT as the
            // front reaches their own distance from the centre, the frame breaks the wave, and
            // it kicks the camera through the callback below so this view need not know what a
            // camera is. The motes come from the same clock, dropped in the wake.
            boardCleanse.Play(boardView, boardView.Board, cyan, sweepSparks,
                delegate
                {
                    ShakeCamera(BoardCleanseView.Style.ScreenShakeStrength,
                        BoardCleanseView.Style.ScreenShakeDuration,
                        BoardCleanseView.Style.ScreenShakeFrequency);
                });
        }

        /// <summary>
        /// THE WHOLE ARENA going off at once, rippling out from its centre - for the two events
        /// that are about the board rather than about some cells on it: a clean sweep and a
        /// dynamite board clear. No particles of their own; both already bring their own shower.
        /// </summary>
        private void FlashBoard(Color tone)
        {
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return;
            }
            var cells = new List<Vector2>(board.Width * board.Height);
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var pos = new GridPos(board.MinX + x, board.MinY + y);
                    if (board.IsInside(pos))
                    {
                        cells.Add(boardView.CellToWorld(pos));
                    }
                }
            }
            CellFlashFx.Play(transform, cells, CellFlashFx.BurstTimes(cells),
                boardView.CellWorldSize, CellFlashFx.Pinch.Uniform,
                CellFlashFx.Palette.Hot(tone));
        }

        /// <summary>Drops the cubes of a card that would not stay on the board, in both worlds.
        /// Staggered a little per cube so the block crumbles instead of sliding off as one slab.
        /// </summary>
        private void DropFellThroughCubes(TurnReport report)
        {
            SpawnFallingCubes(boardView, report.FellThroughCells, report.Card);
            SpawnFallingCubes(mirrorBoardView, report.MirrorFellThroughCells, report.MirrorCard);
        }

        private void SpawnFallingCubes(BoardView view, IReadOnlyList<GridPos> cells, BlockCard card)
        {
            if (view == null || cells == null || cells.Count == 0)
            {
                return;
            }
            Color color = card != null && card.Elements.Count > 0
                ? ViewUtil.ElementColor(card.Elements[0])
                : ViewUtil.ColorForCard(card != null ? card.Id : 0);
            for (int i = 0; i < cells.Count; i++)
            {
                FallingCubeFx.Spawn(transform, view.CellToWorld(cells[i]),
                    view.CellWorldSize * 0.86f, color, i * 0.045f);
            }
        }

        /// <summary>
        /// Runs the "Mayın eşeği" dance when the boss says a new one has happened. The path is the
        /// BOSS's, computed off the round rng, so what the player follows really is where the mine
        /// went - the View never invents a shuffle of its own.
        /// </summary>
        private void PlayMineShuffleIfDue(RoundEngine round)
        {
            var mine = round != null ? round.Boss as MayinEsegiBoss : null;
            if (mine == null)
            {
                lastMineShuffle = 0;
                return;
            }
            if (mine.ShuffleCount == lastMineShuffle || !mine.Armed)
            {
                return;
            }
            lastMineShuffle = mine.ShuffleCount;
            mineShuffle.Play(boardView, round.MainBoard, mine.ShufflePath);
        }

        /// <summary>Very small camera shake for explosions (slightly bigger on clean sweeps).
        /// Fades out evenly, which is what a rumble wants.</summary>
        private void ShakeCamera(float amplitude, float duration)
        {
            ShakeCamera(amplitude, duration, 1f);
        }

        /// <summary>As above, with the shape of the decay: 1 is the even fade, and anything
        /// higher front-loads the energy so the shake HITS and is gone - an impact rather than a
        /// rumble. Only a blast asks for that (see ShakeForBlast).</summary>
        private void ShakeCamera(float amplitude, float duration, float sharpness)
        {
            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                cam.transform.position = camBasePosition;
            }
            shakeRoutine = StartCoroutine(ShakeRoutine(amplitude, duration, sharpness));
        }

        private IEnumerator ShakeRoutine(float amplitude, float duration, float sharpness)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float falloff = Mathf.Pow(1f - Mathf.Clamp01(time / duration), sharpness);
                Vector2 offset = Random.insideUnitCircle * (amplitude * falloff);
                cam.transform.position = camBasePosition + new Vector3(offset.x, offset.y, 0f);
                ShakeHud(offset);
                yield return null;
            }
            cam.transform.position = camBasePosition;
            ShakeHud(Vector2.zero);
            shakeRoutine = null;
        }

        /// <summary>Takes the HUD along with the camera. The canvas is ScreenSpaceOverlay and so
        /// does not follow the camera at all: moving the camera alone slides the WORLD under an
        /// interface that stays nailed to the screen, which reads as the board rattling rather
        /// than as the screen being hit.
        ///
        /// The sign is inverted because moving the camera one way pushes what you see the OTHER
        /// way, and the HUD has to travel with what you see. The conversion is the camera's own:
        /// a world unit is Screen.height / (2 * orthographicSize) pixels, and the scaler's factor
        /// turns those into the canvas units anchoredPosition wants - so this stays correct at
        /// any resolution without a number written down anywhere.</summary>
        private void ShakeHud(Vector2 worldOffset)
        {
            if (hudShake == null || cam == null)
            {
                return;
            }
            if (worldOffset == Vector2.zero)
            {
                hudShake.anchoredPosition = Vector2.zero;
                return;
            }
            float pxPerUnit = Screen.height / Mathf.Max(2f * cam.orthographicSize, 0.0001f);
            float scale = hudCanvas != null ? Mathf.Max(hudCanvas.scaleFactor, 0.0001f) : 1f;
            hudShake.anchoredPosition = -worldOffset * (pxPerUnit / scale);
        }

        /// <summary>The top revealed cards of the discard pile ("Fraksiyon" inspect),
        /// newest first.</summary>
        private static List<BlockCard> RevealedDiscardCards(RoundEngine round)
        {
            var list = new List<BlockCard>();
            IReadOnlyList<BlockCard> pile = round.Deck.DiscardPile;
            int n = Mathf.Min(round.Rules.RevealedDiscardCount, pile.Count);
            for (int i = 0; i < n; i++)
            {
                list.Add(pile[pile.Count - 1 - i]);
            }
            return list;
        }

        private static BlockCard CardOfSlot(RoundEngine round, int slot)
        {
            if (slot < 0)
            {
                return null;
            }
            if (slot < round.Hand.Count)
            {
                return round.Hand[slot];
            }
            int bonusIndex = slot - round.Hand.Count;
            return bonusIndex < round.BonusHand.Count ? round.BonusHand[bonusIndex].Card : null;
        }

        private void RefreshAll(TurnReport report)
        {
            RoundEngine round = session.CurrentRound;
            RememberCardFaces(round);
            PlayMineShuffleIfDue(round);
            // "Öteki dünya" shrinks the main board and lifts it, to make room for the mirror
            // below. With one world these are the values the board always had.
            float mainSize = MainBoardWorldSize;
            Vector2 mainCenter = MainBoardCenter;
            // The CENTRE is part of this test, not just the size: the two layouts fit the
            // board into the same 6.5-unit box but put it in different places, so a profile flip
            // changes where the board goes without changing how big it is.
            if (boardView.Board != round.Board
                || !Mathf.Approximately(lastMainBoardSize, mainSize)
                || (lastMainBoardCenter - mainCenter).sqrMagnitude > 0.000001f)
            {
                boardView.Rebuild(round.Board, mainSize, mainCenter);
                lastMainBoardSize = mainSize;
                lastMainBoardCenter = mainCenter;
            }
            // "Alacakaranlık" - set BEFORE the refresh, so the very first paint is already dark.
            boardView.SetDarkness(round.BoardIsDark);
            boardView.Refresh();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
            boardView.ClearPreview();
            RefreshMirrorWorld();
            RefreshInfections(report);
            cardLayer.Sync(round, report);
            // "Tamagotchi" lays out what it is still owed, next to the hand it has to come from.
            var pet = round.Boss as TamagotchiBoss;
            cardLayer.ShowPetDemands(pet != null ? pet.Demands : null);
            RefreshFlames(round.ContinueCount);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
            SyncRetroPresentation();
            SyncRescueState();
        }

        /// <summary>Keeps every retro-mode presentation layer in sync with RoundRules.RetroMode:
        /// the CRT overlay, the CRT hum + bit-crush audio, and the fullscreen edge-bend shader
        /// global. Called at round start and on every full refresh.</summary>
        private void SyncRetroPresentation()
        {
            bool on = session != null && session.Config.Rules.RetroMode;
            if (!on)
            {
                retroFallHand = -1; // no piece falls once retro is off (the board shrank back)
            }
            if (crt != null)
            {
                crt.SetVisible(on);
            }
            if (sfx != null)
            {
                sfx.SetRetro(on); // the CRT hum loop
            }
            if (bitCrush != null)
            {
                bitCrush.Active = on; // grit the whole mix
            }
            Shader.SetGlobalFloat(CrtBendId, on ? 1f : 0f);
        }

        /// <summary>Gathers "Enfeksiyon" infection markers from the inventory and hands them
        /// to the board view to draw (buildup pips + tint).</summary>
        private void RefreshInfections(TurnReport report)
        {
            infectionBuffer.Clear();
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var enf = jokers[i] as EnfeksiyonJoker;
                if (enf != null)
                {
                    infectionBuffer.AddRange(enf.InfectedCells);
                }
            }
            // Every board marker is a map of the board, so none of them may show while blind.
            if (session.CurrentRound != null && session.CurrentRound.BoardIsDark)
            {
                boardView.ShowInfections(null);
                boardView.ShowCircuit(null);
                boardView.ShowQuarantine(null);
                boardView.ShowCreature(null);
                boardView.ShowDolls(null, null);
                boardView.ShowDoomedColumn(null, 0);
                // The pull markers sit OUTSIDE the arena and say nothing about what is on it, so
                // they are the one marker the dark does not have to swallow.
                boardView.ShowGravity(session.CurrentRound.Board.WaterFlow);
                return;
            }
            boardView.ShowInfections(infectionBuffer);
            RefreshCircuit(report);
            RefreshQuarantine();
            RefreshCreature();
            RefreshBossBoardMarks();
            // "Kütleçekim merkezi": which way the arena is pulling water. A no-op on every board
            // that pulls it downward, which is nearly all of them.
            boardView.ShowGravity(session.CurrentRound.Board.WaterFlow);
        }

        /// <summary>Hands the board view what the new bosses have put ON it: "Matruşka"'s dolls
        /// and "İstilacı"'s marked column. Both are marks on the arena rather than cubes in it,
        /// so neither belongs in the cube pass.</summary>
        private void RefreshBossBoardMarks()
        {
            RoundEngine round = session.CurrentRound;
            BossRound boss = round != null ? round.Boss : null;

            var dolls = boss as MatruskaBoss;
            if (dolls != null)
            {
                IReadOnlyList<GridPos> cells = dolls.DollCells;
                var sizes = new List<int>(cells.Count);
                for (int i = 0; i < cells.Count; i++)
                {
                    sizes.Add(dolls.GenerationsLeftAt(cells[i]));
                }
                boardView.ShowDolls(cells, sizes);
            }
            else
            {
                boardView.ShowDolls(null, null);
            }

            var invader = boss as IstilaciBoss;
            boardView.ShowDoomedColumn(
                invader != null && invader.HasMark ? invader.MarkedColumn : (int?)null,
                invader != null ? invader.TurnsLeft : 0);
        }

        /// <summary>Hands "Besleme"'s creature patch to the board view. A pet you cannot see is
        /// a pet you cannot feed.</summary>
        private void RefreshCreature()
        {
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var pet = jokers[i] as BeslemeJoker;
                if (pet != null && pet.IsAlive)
                {
                    boardView.ShowCreature(pet.Region);
                    return;
                }
            }
            boardView.ShowCreature(null);
        }

        /// <summary>Hands "Karantina"'s sealed lines to the board view, which washes them.</summary>
        /// <summary>The cubes the invader's column took, with the art they were wearing.</summary>
        private static List<DestroyedCube> SweptCubes(TurnReport report)
        {
            var found = new List<DestroyedCube>();
            for (int i = 0; i < report.ColumnSweptCells.Count; i++)
            {
                GridPos cell = report.ColumnSweptCells[i];
                foreach (DestroyedCube dead in report.DestroyedCubes)
                {
                    if (dead.Pos.Equals(cell))
                    {
                        found.Add(dead);
                        break;
                    }
                }
            }
            return found;
        }

        /// <summary>The destroyed cubes that stood on the circuit, in the order the path runs.</summary>
        private static List<DestroyedCube> CircuitCubes(TurnReport report)
        {
            var found = new List<DestroyedCube>();
            for (int i = 0; i < report.CircuitExplodedCells.Count; i++)
            {
                GridPos cell = report.CircuitExplodedCells[i];
                foreach (DestroyedCube dead in report.DestroyedCubes)
                {
                    if (dead.Pos.Equals(cell))
                    {
                        found.Add(dead);
                        break;
                    }
                }
            }
            return found;
        }

        private void RefreshQuarantine()
        {
            RoundEngine round = session.CurrentRound;
            var boss = round != null ? round.Boss as KarantinaBoss : null;
            boardView.ShowQuarantine(boss != null ? boss.QuarantinedCells : null);
        }

        /// <summary>Hands "Devre"'s traced circuit to the board view. Same shape as the infection
        /// pass: the joker holds the route, the board just draws it.</summary>
        private void RefreshCircuit(TurnReport report)
        {
            // The circuit broke THIS TURN. It has to be set off HERE, before the loop below
            // finds no circuit any more and clears the cable: the overload reads its route off
            // the trace that is still loaded, and a cleared trace has no route to read. Nor is
            // the cable cleared afterwards - BurnAway takes it out behind the failure, which is
            // the point of the two being one event.
            if (report != null && report.CircuitExplodedCells.Count > 0)
            {
                boardView.DetonateCircuit();
                // The cubes hold their ground and COOK until the sheet's blue core bursts, then
                // break with it. Breaking them first reads backwards - as if the blocks went on
                // their own and the circuit answered - so the two are pinned to the same moment.
                // The blocks own their whole failure, rupture included - deliberately NOT the
                // shared FlashCells, whose square-on-the-slot is exactly the full-cell white
                // flash this effect must not have. The one destruction that breaks the house
                // rule, and only because the rule's own drawing is the thing being avoided.
                boardView.PlayCircuitHeat(CircuitCubes(report),
                    CircuitOverloadView.RuptureTime);
                return;
            }
            IReadOnlyList<Joker> jokers = session.Jokers.Jokers;
            for (int i = 0; i < jokers.Count; i++)
            {
                var devre = jokers[i] as DevreJoker;
                if (devre != null && devre.HasCircuit)
                {
                    boardView.ShowCircuit(devre.Path);
                    return;
                }
            }
            boardView.ShowCircuit(null);
        }

        /// <summary>The prominent score line: the run total (which is also the market's money)
        /// and, while a round is being played, how that round stands against its threshold.
        /// Both numbers are in the scaled economy, so the threshold is lifted to match.</summary>
        private void UpdateScoreHud()
        {
            if (totalText == null)
            {
                return;
            }
            if (session == null)
            {
                totalText.text = string.Empty;
                return;
            }
            if (session.Phase != GamePhase.Round)
            {
                // The market panel is opaque and reaches the top of the screen, and it prints
                // the balance itself - a HUD line here would just overprint its title.
                totalText.text = string.Empty;
                return;
            }
            var sb = new StringBuilder();
            sb.Append(Loc.Pick("TOTAL ", "TOPLAM ")).Append(session.TotalScore);
            RoundEngine round = session.CurrentRound;
            if (round != null)
            {
                // RoundEngine.ScoreThreshold, never Config's: a boss may ask for less
                // ("Alacakaranlık" cutting the bar to 60%, "Taş ve sopa" by a quarter) and the
                // number the player is chasing must be the one the rules will check. The round
                // dump below already had this right; this line did not.
                sb.Append(Loc.Pick("        round ", "        raunt "))
                    .Append(round.RoundScore).Append(" / ")
                    .Append(round.ScoreThreshold * session.Config.Scoring.ScoreScale);
            }
            totalText.text = sb.ToString();
        }

        /// <summary>The HUD while the market is open. The round dump does NOT belong here: it
        /// describes a round that has already finished (turn counter, board size, the erosion
        /// clock, "right-click to rotate") and, because the canvas draws over world space, it
        /// printed straight across the market panel. Only the run-level facts and the debug
        /// keys survive, and the panel itself carries the prompts.</summary>
        /// <summary>The debug keys BOTH huds carry, in one place - they had drifted apart and
        /// the market's copy was missing half of them. Written as short tokens rather than
        /// "J: pick joker" sentences: the readout is a narrow left COLUMN now (InfoWidth), and a
        /// sentence per key wrapped three lines deep and ran down into the power bar.</summary>
        private static string DebugKeyLine()
        {
            return Loc.Pick(
                "Debug  J joker  P power  D deck  R new run  L türkçe\n"
                    + "F3 anim  F4 blocks  G boss  F5 skip to market",
                "Debug  J joker  P güç  D deste  R yeni oyun  L english\n"
                    + "F3 animasyon  F4 bloklar  G patron  F5 markete atla");
        }

        private void BuildMarketHud()
        {
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(" / ").Append(session.Config.TotalRounds)
                .Append(Loc.Pick("  done", "  bitti")).Append('\n');
            sb.Append(Loc.Pick("Cards ", "Kart ")).Append(session.OwnedCards.Count)
                .Append(Loc.Pick("   Jokers ", "   Joker ")).Append(session.Jokers.Count)
                .Append(Loc.Pick("   Powers ", "   Güç ")).Append(session.Powers.Count).Append('\n');
            // "Kredi kartı": paying the debt down is a MARKET action, so its prompt belongs
            // here and nowhere else - the round HUD only names the debt and its deadline.
            if (session.Debt > 0)
            {
                sb.Append(Loc.Pick("DEBT ", "BORÇ ")).Append(session.Debt)
                    .Append(Loc.Pick(PadOr("   [O] pay", "   R3 pay"), PadOr("   [O] öde", "   R3 öde"))).Append('\n');
            }
            sb.Append(DebugKeyLine());
            infoText.text = sb.ToString();
            // "Kaçakçı": the free item is invisible unless the market says so.
            messageText.text = session.CanSmuggle
                ? Loc.Pick(PadOr("SHIFT+click an offer: take it FREE (may be defective)", "LT + A on an offer: take it FREE (may be defective)"),
                    PadOr("Bir ürüne SHIFT+tık: BEDAVA al (defolu çıkabilir)", "Bir üründe LT + A: BEDAVA al (defolu çıkabilir)"))
                : string.Empty;
        }

        private void UpdateHud()
        {
            UpdateScoreHud();
            RefreshBossBadge();
            // The draw pile doubles as the SELL screen while shopping, which nothing on screen
            // said. Set from the phase on every refresh, so it can never be left on in a round.
            cardLayer.SetSellHint(session.Phase == GamePhase.Market);
            if (session.Phase == GamePhase.Market)
            {
                BuildMarketHud();
                return;
            }
            RoundEngine round = session.CurrentRound;
            var sb = new StringBuilder();
            sb.Append("Seed ").Append(lastSeedUsed)
                .Append(Loc.Pick("   Deck: ", "   Deste: ")).Append(currentDeck.Name).Append('\n');
            sb.Append(Loc.Pick("Round ", "Raunt ")).Append(session.RoundNumber)
                .Append(" / ").Append(session.Config.TotalRounds);
            // A boss STAGE carries the number of the round it follows, so say so plainly - the
            // player is not replaying round 3, they are fighting round 3's boss.
            if (session.InBossStage)
            {
                sb.Append(Loc.Pick("  (boss stage)", "  (patron aşaması)"));
            }
            else if (session.BossStageFollowsThisRound)
            {
                sb.Append(Loc.Pick("  - boss next", "  - sonraki: patron"));
            }
            // WHICH boss, what it does and what it is up to are on the BOSS BADGE and its
            // hover now (see GameUiController.BossBadge) - three facts that made this line the
            // longest in a readout that is a narrow column. What stays is the one bit the badge
            // cannot say from the corner: that this stage is a boss stage at all.
            if (round.Config.IsBossRound)
            {
                sb.Append(Loc.Pick("  [BOSS]", "  [PATRON]"));
            }
            // "Uzun vadeli yatırımcı" restarts the final round silently, so say why the board
            // just went blank - otherwise the do-over looks like a bug.
            if (session.FinalRoundReplays > 0)
            {
                sb.Append(Loc.Pick("  [REPLAY]", "  [TEKRAR]"));
            }
            sb.Append(Loc.Pick("   Turn ", "   Tur ")).Append(round.TurnNumber).Append('\n');

            // RoundScore lives in the scaled economy; lift the threshold to match for display.
            sb.Append(Loc.Pick("Score ", "Puan ")).Append(round.RoundScore)
                // RoundEngine.ScoreThreshold, not the config's: a boss may ask for less
                // ("Taş ve sopa") and the bar on screen has to be the bar the rules use.
                .Append(" / ").Append(round.ScoreThreshold * session.Config.Scoring.ScoreScale);
            if (round.ThresholdPassed)
            {
                sb.Append(Loc.Pick("  [threshold passed]", "  [eşik geçildi]"));
            }
            sb.Append('\n');
            // The run total is NOT repeated here - it has its own line at the top of the screen
            // (UpdateScoreHud), because it is real UI rather than debug furniture. The DEBT is,
            // though: "Kredi kartı" is the one number the player must not lose track of, and its
            // deadline has to be spelled out. Paying is a MARKET action, so the [O] prompt lives
            // in BuildMarketHud instead - this method returns early in the market.
            if (session.Debt > 0)
            {
                sb.Append(Loc.Pick("DEBT ", "BORÇ ")).Append(session.Debt);
                int next = NextBossRound(session);
                if (next > 0)
                {
                    sb.Append(Loc.Pick("  (pay before round ", "  (raunt "))
                        .Append(next)
                        .Append(Loc.Pick(" or lose)", " bitmeden öde yoksa kaybedersin)"));
                }
                sb.Append('\n');
            }
            else if (session.CreditAvailable)
            {
                sb.Append(Loc.Pick("(credit open)", "(kredi açık)")).Append('\n');
            }
            sb.Append(Loc.Pick("Draw ", "Çekme ")).Append(round.Deck.DrawCount)
                .Append(Loc.Pick("   Discard ", "   Iskarta ")).Append(round.Deck.DiscardCount)
                .Append(Loc.Pick("   Removed ", "   Çıkan ")).Append(round.Deck.RemovedCount).Append('\n');
            // The anti-stalling clock: how many more times the deck may run dry for free, and
            // how much of the arena the stalling has already cost.
            if (round.Config.Erosion != ShuffleErosion.None)
            {
                sb.Append(Loc.Pick("Board ", "Alan ")).Append(round.Board.Width)
                    .Append('x').Append(round.Board.Height);
                if (round.Board.DeadCellCount > 0)
                {
                    sb.Append(Loc.Pick("  eaten ", "  yenen ")).Append(round.Board.DeadCellCount);
                }
                if (round.FreeDeckRecyclesLeft > 0)
                {
                    sb.Append(Loc.Pick("   free reshuffles ", "   bedava karma "))
                        .Append(round.FreeDeckRecyclesLeft);
                }
                else
                {
                    sb.Append(Loc.Pick("   ERODING - next reshuffle eats the board",
                        "   ERİYOR - sıradaki karma alanı yiyor"));
                }
                sb.Append('\n');
            }
            AppendMirrorHud(sb, round);
            sb.Append(Loc.Pick("Jokers ", "Joker ")).Append(session.Jokers.Count);
            if (session.Jokers.Count > 0)
            {
                sb.Append(Loc.Pick(PadOr("   (1-9 activate)", "   (hold RB, then A)"), PadOr("   (1-9 kullan)", "   (RB basılı, sonra A)")));
            }
            sb.Append(Loc.Pick("   Powers ", "   Güç ")).Append(session.Powers.Count);
            if (session.Powers.Count > 0)
            {
                sb.Append(Loc.Pick(PadOr("   (click to use, one per turn)", "   (hold LB, then A - one per turn)"), PadOr("   (tıkla, tur başına bir)", "   (LB basılı, sonra A - tur başına bir)")));
            }
            sb.Append('\n');
            sb.Append(Loc.Pick(
                PadOr("Drag to place.  Click draw pile: deck.  Right-click: rotate GEARS / reshape FOX\n", "Hold A to drag.  A on the draw pile: your cards.  X: rotate GEARS / reshape FOX\n", "A takes a block, then places it.  Menu: your cards.  X: rotate GEARS / reshape FOX\n"),
                PadOr("Sürükleyip yerleştir.  Çekme destesi: kartların.  Sağ tık: ÇARK döndür / TİLKİ şekillendir\n", "Sürüklemek için A basılı.  Çekme destesinde A: kartların.  X: ÇARK döndür / TİLKİ şekillendir\n", "A bloğu alır, sonra koyar.  Menu: kartların.  X: ÇARK döndür / TİLKİ şekillendir\n")));
            // The keys that only mean something DURING a round; everything else is shared.
            sb.Append(Loc.Pick(
                "Debug  S hand  B bonus  K sell joker  O debt  W/M worlds\n",
                "Debug  S el  B bonus  K joker sat  O borç  W/M dünyalar\n"));
            sb.Append(DebugKeyLine());
            infoText.text = sb.ToString();

            if (pendingTargetJokerId.HasValue)
            {
                Joker targeting = session.Jokers.Find(pendingTargetJokerId.Value);
                string what = targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                    ? Loc.Pick("pick a cube on the board", "oyun alanından bir küp seç")
                    : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : "Joker")
                    + ": " + what + Loc.Pick(PadOr("\n[Esc] cancel", "\nB cancel"), PadOr("\n[Esc] vazgeç", "\nB vazgeç"));
                return;
            }
            // A workshop power's pick has steps, and a player who does not know which step they
            // are on is just clicking. Say it.
            if (workshopPowerId.HasValue)
            {
                Power shop = session.Powers.Find(workshopPowerId.Value);
                string step;
                if (shop != null && shop.Targeting == ActivationTargeting.CellAndHandCard)
                {
                    step = workshopDonorCell.HasValue
                        ? Loc.Pick("now pick the card that takes the element",
                            "şimdi elementi alacak kartı seç")
                        : Loc.Pick("pick the cube whose element you want",
                            "elementini alacağın küpü seç");
                }
                else if (shop != null && shop.Targeting == ActivationTargeting.TwoHandCards)
                {
                    step = workshopFirstCard < 0
                        ? Loc.Pick("pick the FIRST block to weld",
                            "lehimlenecek BİRİNCİ bloğu seç")
                        : Loc.Pick("now pick the SECOND block",
                            "şimdi İKİNCİ bloğu seç");
                }
                else
                {
                    step = Loc.Pick("pick the block to cut",
                        "kesilecek bloğu seç");
                }
                messageText.text = (shop != null ? shop.DisplayName : "Power") + ": " + step
                    + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }
            if (pendingTargetPowerId.HasValue)
            {
                Power targeting = session.Powers.Find(pendingTargetPowerId.Value);
                string what = pendingOltaMark
                    ? Loc.Pick("pick the hand card to mark (free, once per round)",
                        "işaretlenecek kartı elinden seç (bedava, raunt başına bir)")
                    : targeting != null && targeting.Targeting == ActivationTargeting.BoardCell
                        ? Loc.Pick("pick a cell on the board", "oyun alanından bir hücre seç")
                        : Loc.Pick("pick a block from your hand", "elinden bir blok seç");
                messageText.text = (targeting != null ? targeting.DisplayName : Loc.Pick("Power", "Güç"))
                    + ": " + what + Loc.Pick("\n[Esc] cancel", "\n[Esc] vazgeç");
                return;
            }

            // The dead-end pause: the board is full and only the rescue power can save it.
            if (round.Status == RoundStatus.AwaitingRescue)
            {
                messageText.text = Loc.Pick(
                    "No room left! Pick two rows or two columns to swap.\n[Esc] give up",
                    "Yer kalmadı! Yerini değiştirmek için iki satır ya da iki sütun seç."
                        + "\n[Esc] pes et");
                return;
            }

            switch (session.Phase)
            {
                case GamePhase.GameOver:
                    messageText.text = Loc.Pick("GAME OVER - ", "OYUN BİTTİ - ") + DescribeLoss(round.Loss)
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                case GamePhase.RunWon:
                    messageText.text = Loc.Pick(
                            "RUN COMPLETE! All ", "OYUN TAMAMLANDI! ")
                        + session.Config.TotalRounds
                        + Loc.Pick(" rounds survived.\nFinal score ", " rauntun hepsi geçildi.\nSon puan ")
                        + session.TotalScore
                        + Loc.Pick("\n[R] new run", "\n[R] yeni oyun");
                    break;
                // GamePhase.Market never reaches here - BuildMarketHud handles it and returns,
                // and the buy / next-round prompt is drawn inside the market panel instead.
                default:
                    if (round.Status == RoundStatus.AwaitingAdvanceDecision)
                    {
                        int continueCost = round.NextContinueCost;
                        int drawAfter = round.PredictDrawCountAfterContinue();
                        string warning = drawAfter < 0
                            ? Loc.Pick("  DECK OUT!", "  DESTE BİTER!")
                            : string.Empty;
                        messageText.text = Loc.Pick(
                                PadOr("Threshold reached!\n[A] advance to market    [C] continue: removes ", "Threshold reached!\nY advance to market    X continue: removes "),
                                PadOr("Eşik geçildi!\n[A] markete ilerle    [C] devam et: ", "Eşik geçildi!\nY markete ilerle    X devam et: "))
                            + continueCost
                            + Loc.Pick(" cards, draw pile ", " kart gider, çekme destesi ")
                            + round.Deck.DrawCount
                            + " -> " + Mathf.Max(drawAfter, 0) + warning;
                    }
                    else
                    {
                        messageText.text = string.Empty;
                    }
                    break;
            }
        }

        /// <summary>The number of the round whose BOSS STAGE is the next one coming - the
        /// deadline the market debt has to be settled by, because a boss stage that ends with the
        /// debt open ends the run. 0 when the progression has no boss stages at all.</summary>
        private static int NextBossRound(GameSession session)
        {
            // Already in one: this is the deadline.
            if (session.InBossStage)
            {
                return session.RoundNumber;
            }
            for (int round = session.RoundNumber; round <= session.Config.TotalRounds; round++)
            {
                if (session.Config.Progression.HasBossStageAfter(round))
                {
                    return round;
                }
            }
            return 0;
        }

        private static string DescribeLoss(LossReason? loss)
        {
            switch (loss)
            {
                case LossReason.NoPlayableMove:
                    return Loc.Pick("no held block fits the board",
                        "eldeki hiçbir blok alana sığmıyor");
                case LossReason.HandCannotBeRefilled:
                    return Loc.Pick("the deck ran out (hand could not be refilled)",
                        "deste tükendi (el doldurulamadı)");
                case LossReason.DrawPileEmptyAfterThreshold:
                    return Loc.Pick("draw pile emptied before a clean sweep",
                        "temizlik gelmeden çekme destesi bitti");
                case LossReason.BetFailed:
                    return Loc.Pick("the bet was lost (Batak)", "bahis tutmadı (Batak)");
                case LossReason.RetroTopOut:
                    return Loc.Pick("topped out - no room to drop from above",
                        "tepeye ulaştın - yukarıdan blok düşecek yer yok");
                case LossReason.ForbiddenCleanSweep:
                    return Loc.Pick("you cleared the board - Çıkmaz forbids it",
                        "tahtayı temizledin - Çıkmaz buna izin vermiyor");
                case LossReason.ForbiddenThreshold:
                    return Loc.Pick("you reached the threshold - Çıkmaz forbids it",
                        "puan eşiğine ulaştın - Çıkmaz buna izin vermiyor");
                case LossReason.DebtNotRepaid:
                    return Loc.Pick("a boss round ended with your market debt still open",
                        "patron raundu bitti, market borcun hâlâ açıktı");
                case LossReason.OutOfTurns:
                    return Loc.Pick("the turn limit ran out (Saatçi)",
                        "tur sınırı doldu (Saatçi)");
                case LossReason.NoRoomForDoll:
                    return Loc.Pick("a doll split with no cube left to sit on (Matruşka)",
                        "bebek bölündü ama oturacak küp kalmadı (Matruşka)");
                case LossReason.LineWithoutDoll:
                    return Loc.Pick("you exploded a line with no doll in it (Matruşka)",
                        "içinde bebek olmayan bir sıra patlattın (Matruşka)");
                case LossReason.PetWentHungry:
                    return Loc.Pick("the deck ran dry with the pet still unfed (Tamagotchi)",
                        "deste bitti, Tamagotchi hâlâ açtı");
                default:
                    return Loc.Pick("unknown", "bilinmiyor");
            }
        }

        private static void LogTurn(TurnReport report)
        {
            string sweep = report.CleanSweep ? ", CLEAN SWEEP" : string.Empty;
            Debug.Log("[block_bonk] Turn " + report.TurnNumber + ": " + report.Card
                + " at " + report.Origin + " -> +" + report.ScoreGained + " ("
                + (report.ExplodedRows.Count + report.ExplodedColumns.Count) + " lines, "
                + report.CubesExploded + " cubes" + sweep + "), status " + report.StatusAfter);
        }
    }
}
