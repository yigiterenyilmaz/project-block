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
            // Noticed BEFORE the refresh, because the refresh is what starts the replacement
            // mine's reveal and it has to know to wait for the blast. Drawn further down.
            TriggerMineDetonation(round);
            RefreshAll(report);
            // A moving board's turn end is drawn on THIS frame, the one the board was repainted in
            // - never behind the water below, or its cubes would be seen at their new cells before
            // they had set off.
            PlayBoardMotion(report);
            // "Kangren" for the same reason: the cells it takes are held back to what stood in them
            // on this frame, before anything is drawn.
            PlayGangrene(round, report);
            // "Yılan" too: it slid, it may have eaten, and the player's lines may have cut it - all
            // of it on the frame the board was repainted.
            PlaySnake(round);
            // "Hidrolik pres" letting go, on the same frame and for the same reason: the cubes it
            // shoved are already at their new cells on the board, so the copies have to set off now.
            PlayPressRelease(round);
            // "Parazit": the host cube either held against something this turn, or the player's own
            // line finally took it. Both on this frame, off the rules' own report.
            PlayParasiteSeverance(round, report);
            PlayParasiteRefusals(round);
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
            TriggerInfectionBlast(report);
            PlayMineDetonationFeedback(round);
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
            // "Matruşka": the dolls held since the refresh open NOW - as the cubes under them go, not
            // when the board was repainted before the water fell.
            boardView.ReleaseDolls();
            // "Hazine": a mark found by this turn's destruction is revealed as ITS cube breaks -
            // which is now, not when the board was repainted before the water fell.
            SyncHazine(round, report);
            // "Meydan Okuma": the dare paid, missed or moved - as this turn's lines go.
            PlayChallengeEvent();
            // "Elmas Kazma": the obsidian the sweep could not take, after the sweep's own wave.
            PlayQuarry(report);
            // "Tutuştur": every other fire burns out, climbing from the source's explosion peak.
            PlayIgnition();
            // EVERY JOKER THAT FIRED THIS TURN lights up, from the one channel Core reports them
            // on. Here rather than at the repaint because a proc is an EVENT and belongs with the
            // turn's other events - a flash on the repaint frame lands before the line it was
            // paid for has even broken.
            PlayJokerProcs(report);
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

        /// <summary>
        /// Lights every joker the turn says fired. ONE seam for all of them: a joker earns its
        /// flash by calling NoteProc in Core, and nothing here knows which jokers exist.
        ///
        /// The ids are matched against the bar as they come, so a joker sold mid-turn simply
        /// finds no panel and lights nothing - there is no bookkeeping to go stale.
        /// </summary>
        private void PlayJokerProcs(TurnReport report)
        {
            if (report == null || jokerBar == null)
            {
                return;
            }
            for (int i = 0; i < report.ProcedJokers.Count; i++)
            {
                jokerBar.ProcJoker(report.ProcedJokers[i]);
                PlayArenaProc(report.ProcedJokers[i]);
            }
            // WATER THAT FELL INTO PLACE paid its bonus: say so over the arena, once.
            if (report.WaterFallLines > 0)
            {
                FloatingTextFx.Spawn(transform, MainBoardCenter + new Vector2(0f, 1.6f),
                    Loc.Pick("WATER FALL +" + session.Config.Scoring.WaterFallBonusPercent + "%",
                        "SU DÜŞTÜ +%" + session.Config.Scoring.WaterFallBonusPercent),
                    new Color(0.53f, 0.87f, 0.87f), 60, 0.06f);
            }
        }

        /// <summary>
        /// A joker whose subject is THE WHOLE BOARD lights the board as well as its own card.
        ///
        /// "Simetri" pays for the shape the arena is left in, so a flash confined to a card in
        /// the corner says nothing about what was actually rewarded - the player needs to see the
        /// thing that scored, which is the arena itself. It goes through FlashBoard, the ripple
        /// the two other whole-arena events already use, so a board-wide joker and a board-wide
        /// clear speak the same way.
        ///
        /// Kept to jokers that are genuinely about the board: an ordinary payout lighting the
        /// arena would make every turn look like a clean sweep.
        /// </summary>
        private void PlayArenaProc(int instanceId)
        {
            if (session == null || session.Jokers == null)
            {
                return;
            }
            Joker fired = session.Jokers.Find(instanceId);
            if (fired is SimetriJoker)
            {
                FlashBoard(SymmetryProcColor);
            }
        }

        /// <summary>"Simetri"'s own colour - a cool mirror-blue, so its ripple is never confused
        /// with the sweep's cyan or a blast's orange.</summary>
        private static readonly Color SymmetryProcColor = new Color(0.62f, 0.74f, 1f);

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
            // A turn whose only explosion was a loose group ("Hedefli" payout, a late reshape
            // clear) does not shake: the group explosion keeps the screen still. A cleared line,
            // TNT and a clean sweep keep theirs.
            if (report.CubesExploded > 0 || report.DynamiteTriggered || report.CleanSweep)
            {
                ShakeForBlast(report.DynamiteTriggered, report.CleanSweep, comboStreak);
            }
            if (report.DynamiteTriggered)
            {
                FlashDynamite(DynamiteCenter(report));
                SpawnDynamitePopup();
            }
            // The popup shows the SCORING combo (consecutive line-clearing turns), which is
            // what actually pays out - not the destruction-only comboStreak that drives shake.
            if (report.ComboCount >= 2)
            {
                SpawnComboPopup(report.ComboCount, report.ComboBridged,
                    report.ComboMultiplier);
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
            if (bossIdentity != null)
            {
                bossIdentity.ReactDynamite(at);
            }
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
            SpawnComboPopup(comboCount, false, 1.0);
        }

        /// <summary>
        /// The combo popup, and whether "Mikrodalga" is the only reason there is one.
        ///
        /// A BRIDGED combo says so, because otherwise the joker is invisible at the exact moment
        /// it does its work: the player cleared no line last turn, so by every rule they know
        /// the streak should have died - and it did not. A second line under the count, in the
        /// joker's own warmer colour, is what turns "that is odd" into "that is my joker".
        ///
        /// It is also the one popup that reports a DISCOUNT, so it must not read as a
        /// celebration of the full bonus: a reheated combo pays part of what an unbroken one
        /// would (RoundRules.ComboBridgedScorePercent).
        /// </summary>
        private void SpawnComboPopup(int comboCount, bool bridged, double factor)
        {
            // THE LADDER HAS A CEILING, so the popup stops promising more once it is reached.
            // The streak itself keeps counting (jokers care, and so does the player), but a
            // "COMBO x7" over a bonus that stopped growing at three is the popup telling the
            // player they are being paid for something they are not - which is exactly the
            // "what you see and what you are paid line up" rule this file already holds the
            // first clearing turn to.
            double[] ladder = session != null ? session.Config.Scoring.ComboMultipliers : null;
            bool maxed = ladder != null && ladder.Length > 0 && comboCount >= ladder.Length;
            FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                Loc.Pick("COMBO x", "KOMBO x") + comboCount
                    + (maxed ? Loc.Pick("  MAX!", "  MAKS!") : "!"),
                bridged ? BridgedComboColor : new Color(1f, 0.6f, 0.2f), 64, 0.08f);
            // THE MULTIPLIER IS THE POINT NOW, so it is printed: a streak that says only "x3"
            // as a COUNT tells the player nothing about what it is worth, and the number that
            // matters is no longer a flat bonus they could read off the score.
            if (factor > 1.0001)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                    "x" + factor.ToString("0.##") + Loc.Pick(" SCORE", " PUAN"),
                    bridged ? BridgedComboColor : new Color(1f, 0.78f, 0.35f), 46, 0.09f);
            }
            if (bridged)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.6f),
                    Loc.Pick("MİKRODALGA  -  kept warm", "MİKRODALGA  -  sıcak tutuldu"),
                    BridgedComboColor, 40, 0.10f);
            }
        }

        /// <summary>A bridged combo's own colour: the ordinary combo orange pulled toward the
        /// microwave's warmer amber, so the two read as the same event at different temperatures
        /// rather than as two unrelated popups.</summary>
        private static readonly Color BridgedComboColor = new Color(1f, 0.78f, 0.32f);

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
            FlashCells(report.ExtraExplodedCells, BlastColor);
            // A "Hedefli" payout keeps its cells in a list of its own (so "Antimadde" cannot be
            // billed for them). It goes off in the lime that belongs to nothing else on the
            // board, so the cube the player was aiming at is what they see break.
            FlashCells(report.TargetedExplodedCells,
                ViewUtil.ElementColor(BlockElement.Targeted));
            // Cells a BOSS took off rather than destroyed - and here, only the ones that
            // VANISHED where they stood ("Alzheimer", "Hidrolik pres"): those go through a removal
            // variant. The other two kinds have ALREADY played, on the frame the board was
            // repainted: one a MOVING board carried off ("Yürüyen merdiven", "Merkezkaç kuvveti")
            // was torn away along its step (PlayBoardMotion), and one that CHANGED in place
            // ("Kangren") was drawn turning by the rot's own system (PlayGangrene). This pass can
            // run behind the water, which is exactly why they do not wait for it.
            var removed = new List<GridPos>();
            for (int i = 0; i < report.LiftedCells.Count; i++)
            {
                if (report.LiftKindAt(i) == LiftKind.Removed)
                {
                    removed.Add(report.LiftedCells[i]);
                }
            }
            PlayRemoval(removed);
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
            // A cleared LINE shares nothing with FlashCells. A whole row of cells going off one
            // by one is a rectangle, and the sheet already flies its own debris out of the
            // middle. What a line gets instead is LineSweepView - energy leaving the centre for
            // both ends, with each slot answering as it passes - under the drawn burst. A loose
            // group goes through ClusterBurstView, which has neither a direction nor a combo
            // tier, so the two can never be mistaken for each other.
            lineSweep.Play(cells, boardView.CellWorldSize, row, tone);
            lineBurst.Play(cells, boardView.CellWorldSize, row, activeLineTier);
            if (bossIdentity != null)
            {
                bossIdentity.ReactLine(cells[0], cells[cells.Count - 1], tier);
            }
        }

        /// <summary>
        /// EVERY explosion that is not a line: a late board-reshape clear, a "Hedefli" payout, a
        /// power blast, the sweeper. The group goes off from its own centre through
        /// ClusterBurstView - each cell charging, bursting and letting go - in the colour that
        /// destruction already owns. It leaves the camera alone unless its impulse setting is
        /// raised from zero.
        ///
        /// <paramref name="onFirstPeak"/> is for a caller that owns its SOUND: it fires on the
        /// frame the first cell bursts, so the bang lands with the flash instead of ahead of it.
        ///
        /// Cells outside the board are dropped: a turn that also eroded the arena can name a
        /// cell that is no longer there. A cell named twice goes off once. Returns whether
        /// anything was drawn.
        /// </summary>
        private bool FlashCells(IReadOnlyList<GridPos> cells, Color tone,
            System.Action onFirstPeak = null, IReadOnlyList<ClusterBurstView.Look> faces = null)
        {
            if (cells == null || cells.Count == 0 || boardView == null || boardView.Board == null)
            {
                return false;
            }
            GameBoard board = boardView.Board;
            var group = new HashSet<GridPos>();
            var world = new List<Vector2>(cells.Count);
            var looks = new List<ClusterBurstView.Look>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                if (board.IsInside(cells[i]) && group.Add(cells[i]))
                {
                    world.Add(boardView.CellToWorld(cells[i]));
                    Sprite tile;
                    Color colour;
                    if (faces != null && faces.Count > 0)
                    {
                        // The lab's blocks, handed in whole: the cells it blasts hold none.
                        looks.Add(faces[i % faces.Count]);
                    }
                    else
                    {
                        // The board repainted this cell empty a moment ago; the burst breaks the
                        // cube that WAS there, so it asks what that cube looked like.
                        looks.Add(boardView.TryCubeLook(cells[i], CubeLookMaxAge, out tile, out colour)
                            ? new ClusterBurstView.Look { Tile = tile, Colour = colour }
                            : new ClusterBurstView.Look());
                    }
                }
            }
            if (world.Count == 0)
            {
                return false;
            }
            // The screen stays still unless ClusterBurstView.Style.ScreenImpulseStrength is raised
            // from its default of zero (the designer's call).
            float impulse = ClusterBurstView.ImpulseFor(world.Count);
            System.Action onImpact = null;
            if (impulse > 0f)
            {
                onImpact = delegate
                {
                    ShakeCamera(impulse, ClusterBurstView.Style.ScreenImpulseDuration, 3f);
                };
            }
            clusterBurst.Play(world, looks, boardView.CellWorldSize, boardView.CubeWorldSize, tone,
                onFirstPeak, onImpact);
            if (bossIdentity != null)
            {
                var centre = Vector2.zero;
                for (int i = 0; i < world.Count; i++)
                {
                    centre += world[i];
                }
                bossIdentity.ReactBurst(centre / world.Count, world.Count);
            }
            return true;
        }

        /// <summary>How long after a repaint emptied a cell FlashCells still breaks the cube that
        /// stood there. Generous: the sweeper waits its own beat, and a turn's explosion can play
        /// after the water has finished falling.</summary>
        private const float CubeLookMaxAge = 2f;

        /// <summary>The ways a cube a boss REMOVED can leave the board. Every removal draws one
        /// at random - the same one twice is allowed - so the player keeps seeing it happen
        /// differently. Add a variant here and in PlayRemoval; nothing else needs to know.</summary>
        private enum RemovalVariant
        {
            ColdSink,
            PhaseFold,
            CryoSublimation
        }

        private static readonly RemovalVariant[] RemovalVariants =
        {
            RemovalVariant.ColdSink, RemovalVariant.PhaseFold, RemovalVariant.CryoSublimation
        };

        /// <summary>
        /// Cubes a boss REMOVED - vanished where they stood, cells empty now. One variant, picked
        /// at random, for the whole removal: half a group falling into pits and half doing
        /// something else would read as two events. Visual only - the random pick never reaches
        /// Core. <paramref name="variant"/> and <paramref name="faces"/> are the lab's: a variant
        /// to force and the blocks to use on cells that hold none.
        /// </summary>
        private bool PlayRemoval(IReadOnlyList<GridPos> cells, RemovalVariant? variant = null,
            IReadOnlyList<ClusterBurstView.Look> faces = null)
        {
            if (cells == null || cells.Count == 0 || boardView == null || boardView.Board == null)
            {
                return false;
            }
            GameBoard board = boardView.Board;
            var seen = new HashSet<GridPos>();
            var world = new List<Vector2>(cells.Count);
            var looks = new List<ClusterBurstView.Look>(cells.Count);
            for (int i = 0; i < cells.Count; i++)
            {
                if (!board.IsInside(cells[i]) || !seen.Add(cells[i]))
                {
                    continue;
                }
                world.Add(boardView.CellToWorld(cells[i]));
                Sprite tile;
                Color colour;
                if (faces != null && faces.Count > 0)
                {
                    looks.Add(faces[i % faces.Count]);
                }
                else
                {
                    looks.Add(boardView.TryCubeLook(cells[i], CubeLookMaxAge, out tile, out colour)
                        ? new ClusterBurstView.Look { Tile = tile, Colour = colour }
                        : new ClusterBurstView.Look());
                }
            }
            if (world.Count == 0)
            {
                return false;
            }
            RemovalVariant chosen = variant.HasValue ? variant.Value
                : RemovalVariants[Random.Range(0, RemovalVariants.Length)];
            switch (chosen)
            {
                case RemovalVariant.PhaseFold:
                    phaseFold.Play(world, looks, boardView.CellWorldSize, boardView.CubeWorldSize,
                        boardView.EmptySlotSize);
                    break;
                case RemovalVariant.CryoSublimation:
                    cryoSublimation.Play(world, looks, boardView.CellWorldSize, boardView.CubeWorldSize,
                        boardView.EmptySlotSize);
                    break;
                default:
                    coldSink.Play(world, looks, boardView.CellWorldSize, boardView.CubeWorldSize,
                        boardView.EmptySlotSize);
                    break;
            }
            return true;
        }

        /// <summary>
        /// A moving board's turn end ("Yürüyen merdiven", "Merkezkaç kuvveti"), drawn on the frame
        /// the board was repainted in its new state: every cube that stays rides to its new cell
        /// (PlayBoardMoves), every one that does not is torn off (PlayForcedExit) - both on the same
        /// board's launch, so a row's survivors and casualties set off together.
        /// </summary>
        private void PlayBoardMotion(TurnReport report)
        {
            if (report == null)
            {
                return;
            }
            PlayBoardMoves(report.BoardMoves);
            var carried = new List<GridPos>();
            var motions = new List<LiftMotion>();
            for (int i = 0; i < report.LiftedCells.Count; i++)
            {
                LiftMotion motion = report.LiftMotionAt(i);
                if (report.LiftKindAt(i) == LiftKind.Relocated && motion.Reason != LiftReason.None)
                {
                    carried.Add(report.LiftedCells[i]);
                    motions.Add(motion);
                }
            }
            PlayForcedExit(carried, motions);
        }

        /// <summary>
        /// Cubes a moving board carried to another cell and that SURVIVED: each rides from the cell
        /// Core says it left to the cell Core says it reached - never a destination worked out here -
        /// through BossMoveView, on the profile of the board that moved it. The board view keeps the
        /// destinations blank until they land. A mirror-world move plays over the mirror board.
        /// </summary>
        private bool PlayBoardMoves(IReadOnlyList<CellMove> moves)
        {
            if (moves == null || moves.Count == 0 || bossMove == null)
            {
                return false;
            }
            bool any = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool mirror = pass == 1;
                BoardView view = mirror ? mirrorBoardView : boardView;
                if (view == null || view.Board == null)
                {
                    continue;
                }
                var from = new List<Vector2>();
                var to = new List<Vector2>();
                var targets = new List<GridPos>();
                var sources = new List<BoardMotionSource>();
                var looks = new List<ClusterBurstView.Look>();
                for (int i = 0; i < moves.Count; i++)
                {
                    CellMove m = moves[i];
                    if (m.Mirror != mirror || !view.Board.IsInside(m.To))
                    {
                        continue;
                    }
                    from.Add(view.CellToWorld(m.From));
                    to.Add(view.CellToWorld(m.To));
                    targets.Add(m.To);
                    sources.Add(m.Source);
                    Sprite tile = ViewUtil.CubeTile(m.Cube.Kind, FindOwnedCard(m.Cube.SourceCardId));
                    looks.Add(new ClusterBurstView.Look { Tile = tile, Colour = ViewUtil.CubeTileColor(m.Cube, tile) });
                }
                if (from.Count == 0)
                {
                    continue;
                }
                bossMove.Play(view, from, to, targets, sources, looks, view.CellWorldSize, view.CubeWorldSize,
                    view.WorldRect);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// Cubes a MOVING board carried off ("Yürüyen merdiven", "Merkezkaç kuvveti"): each is torn
        /// off along the step Core reported it taking (LiftMotion) - never a direction worked out
        /// here - through MomentumPeelView ("Soğuk sökülme"). Drawn from the cube Core says WENT,
        /// since its cell may already hold the cube that slid in behind it, and from the cell it
        /// stood in when the step began. A mirror-world cube plays over the mirror board. The
        /// board's rect goes along, so a cube going over the edge is clipped by it.
        /// </summary>
        private bool PlayForcedExit(IReadOnlyList<GridPos> cells, IReadOnlyList<LiftMotion> motions)
        {
            if (cells == null || motions == null || cells.Count == 0 || momentumPeel == null)
            {
                return false;
            }
            bool any = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool mirror = pass == 1;
                BoardView view = mirror ? mirrorBoardView : boardView;
                if (view == null || view.Board == null)
                {
                    continue;
                }
                var from = new List<Vector2>();
                var steps = new List<Vector2>();
                var reasons = new List<LiftReason>();
                var sources = new List<BoardMotionSource>();
                var looks = new List<ClusterBurstView.Look>();
                for (int i = 0; i < cells.Count && i < motions.Count; i++)
                {
                    LiftMotion m = motions[i];
                    if (m.Mirror != mirror || m.Reason == LiftReason.None)
                    {
                        continue;
                    }
                    from.Add(view.CellToWorld(m.From));
                    steps.Add(new Vector2(m.Step.X, m.Step.Y));
                    reasons.Add(m.Reason);
                    sources.Add(m.Source);
                    Sprite tile = ViewUtil.CubeTile(m.Cube.Kind, FindOwnedCard(m.Cube.SourceCardId));
                    looks.Add(new ClusterBurstView.Look { Tile = tile, Colour = ViewUtil.CubeTileColor(m.Cube, tile) });
                }
                if (from.Count == 0)
                {
                    continue;
                }
                momentumPeel.Play(from, steps, reasons, sources, looks, view.CellWorldSize, view.CubeWorldSize,
                    view.WorldRect);
                any = true;
            }
            return any;
        }

        /// <summary>
        /// "Kangren" - the rot's whole turn, exactly as Core reported it: the cell it took and the
        /// side it crept in from, the cubes beside it it could not take, every line it took whole in
        /// the order they died, and the cubes each jump turned on the nearer edge. Nothing here is
        /// worked out from the board; the View is told all of it (TurnReport.GangreneSpread /
        /// GangreneLineDeaths) and only decides how it LOOKS.
        /// </summary>
        private bool PlayGangrene(RoundEngine round, TurnReport report)
        {
            if (report == null || (report.GangreneSpread == null && report.GangreneLineDeaths.Count == 0))
            {
                return false;
            }
            var scene = new GangreneView.TurnScene();
            GangreneSpread spread = report.GangreneSpread;
            if (spread != null)
            {
                scene.Cell = spread.Cell;
                scene.Source = spread.Source;
                scene.HadCube = spread.Before.HasValue;
                if (spread.Before.HasValue)
                {
                    scene.Before = LookOf(spread.Before.Value);
                }
                for (int i = 0; i < spread.Immune.Count; i++)
                {
                    scene.Immune.Add(spread.Immune[i]);
                }
            }
            for (int d = 0; d < report.GangreneLineDeaths.Count; d++)
            {
                GangreneLineDeath death = report.GangreneLineDeaths[d];
                var line = new GangreneView.LineDeath
                {
                    IsRow = death.IsRow,
                    Line = death.Line,
                    EdgeLine = death.EdgeLine
                };
                for (int i = 0; i < death.Converted.Count; i++)
                {
                    line.Converted.Add(new GangreneView.Converted
                    {
                        Cell = death.Converted[i],
                        Before = i < death.Before.Count
                            ? LookOf(death.Before[i])
                            : default(ClusterBurstView.Look)
                    });
                }
                scene.Deaths.Add(line);
            }
            // The turn's bill, felt once through every rotten cube - taken from the breakdown the
            // boss actually wrote, never assumed from the board.
            var boss = round != null ? round.Boss as KangrenBoss : null;
            if (boss != null)
            {
                foreach (ScoreContribution c in report.Score.Contributions)
                {
                    if (c.Source == boss.DefId && c.Flat < 0)
                    {
                        scene.Billed = true;
                        break;
                    }
                }
            }
            // A turn that cleared something gets its own blast read first; a quiet one does not.
            bool cleared = report.ExplodedRows.Count > 0 || report.ExplodedColumns.Count > 0
                || report.ExtraExplodedCells.Count > 0;
            scene.Delay = cleared ? GangreneView.Style.TurnStartDelay
                : GangreneView.Style.TurnStartDelayQuiet;
            return PlayGangreneScene(scene);
        }

        /// <summary>The snake's turn this View has already played, so a report is played once.</summary>
        private int snakeTurnPlayed;

        /// <summary>Whether the snake in this arena has had its wake-up. It comes AWAKE the first
        /// time the round is drawn; after that it is simply there.</summary>
        private bool snakeSpawned;

        /// <summary>
        /// "Yılan": where the snake is, straight from the rules. The board leaves its cells blank
        /// (see BoardView.Refresh) and SnakeView stands the six drawn pieces in them - so this is
        /// the only thing that tells the View a snake exists at all.
        /// </summary>
        private void SyncSnake(RoundEngine round)
        {
            var boss = round != null ? round.Boss as SnakeBoss : null;
            if (boss == null)
            {
                boardView.StopSnake();
                return;
            }
            boardView.Snake.Sync(boardView);
            if (!snakeSpawned)
            {
                // A NEW SNAKE HAS EATEN NOTHING. Its defeat is about the colours IT took, so the
                // last one's history cannot be allowed to leak into it.
                SnakeDefeatView.ForgetHistory();
            }
            boardView.Snake.SetBody(boss.Body, !snakeSpawned);
            snakeSpawned = true;
        }

        /// <summary>
        /// "Yılan"'s whole turn, exactly as Core reported it (SnakeBoss.LastTurn): the segments the
        /// player's lines cut, in order, with the tail cell each one took; then either the death or
        /// the slide - and the slide comes with THE BODY AFTER EVERY CELL of it, so the View follows
        /// the movement the rules made instead of interpolating between two states. What it ate
        /// comes with the block's own face, taken before the rules removed it.
        /// </summary>
        private bool PlaySnake(RoundEngine round)
        {
            var boss = round != null ? round.Boss as SnakeBoss : null;
            SnakeTurnVisuals turn = boss != null ? boss.LastTurn : null;
            if (turn == null || turn.Turn == snakeTurnPlayed)
            {
                return false;
            }
            snakeTurnPlayed = turn.Turn;
            var scene = new SnakeView.TurnScene
            {
                Defeated = turn.Defeated,
                Stuck = turn.WasStuck,
                Grew = turn.GrowthOccurred,
                EatenCell = turn.EatenCell
            };
            CopyCells(turn.BodyBeforeCuts, scene.BodyBefore);
            var standing = new List<GridPos>(turn.BodyBeforeCuts);
            for (int i = 0; i < turn.RemovedTailCells.Count; i++)
            {
                var after = new List<GridPos>();
                if (i < turn.BodyAfterEachCut.Count)
                {
                    CopyCells(turn.BodyAfterEachCut[i], after);
                }
                scene.Cuts.Add(new SnakeView.CutStep
                {
                    Trigger = SnakeCutTrigger(turn, i, standing),
                    RemovedTail = turn.RemovedTailCells[i],
                    BodyAfter = after
                });
                standing = after;
            }
            for (int i = 0; i < turn.StepSnapshots.Count; i++)
            {
                var step = new List<GridPos>();
                CopyCells(turn.StepSnapshots[i], step);
                scene.Steps.Add(step);
            }
            if (turn.EatenCube.HasValue)
            {
                scene.EatenLook = LookOf(turn.EatenCube.Value);
            }
            CopyCells(turn.BodyAfter, scene.BodyAfter);
            return PlaySnakeScene(scene);
        }

        // ---- "Parazit" -------------------------------------------------------------------
        //
        // The host cube's harness, and the three things that can happen to it. Everything below
        // takes what the RULES wrote (ParasiteVisuals: GameBoard.HostRefusals, the joker's own host
        // cell and passenger) and turns it into world geometry. Nothing here decides whether an
        // attempt was refused, which way it came from, or whether the bond is about to break.

        private readonly List<ParasiteHostView.Host> parasiteHosts =
            new List<ParasiteHostView.Host>();

        private readonly List<GridPos> parasiteSevered = new List<GridPos>();

        /// <summary>The parasite in play, or null. One per run by the rules.</summary>
        private ParazitJoker FindParasite()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var parasite = owned[i] as ParazitJoker;
                if (parasite != null)
                {
                    return parasite;
                }
            }
            return null;
        }

        /// <summary>
        /// "TILSIM"'s ground, asked every repaint, and its harvest the moment the power runs.
        ///
        /// Both come from the power's own reports. The harvest is keyed on a SERIAL rather than on
        /// the list's contents, so a repaint during the animation does not restart it; the ground
        /// is keyed the same way, and an empty report where there was a full one is the gift being
        /// recalled at the end of its round. The View works out none of it - not which ghosts could
        /// be reclaimed from, not which cells the next board got, not when the gift expires.
        /// </summary>
        private void SyncTalisman(RoundEngine round)
        {
            TilsimPower power = FindTalisman();
            if (power == null)
            {
                boardView.StopTalisman();
                return;
            }
            if (power.LastActivation != null)
            {
                boardView.Talisman.PlayHarvest(boardView, power.LastActivation);
            }
            boardView.Talisman.Sync(boardView, power.LastGround);
        }

        /// <summary>
        /// "Yangın"'s spread, asked every repaint and keyed on the joker's own SERIAL.
        ///
        /// The View works out none of it: which cubes were already fire, which neighbours were
        /// lit, what each of those used to be and which side it caught from are all the report's
        /// - and so, crucially, is the ONE-RING rule. A View that derived the targets from "what
        /// is fire now" could not tell a source from something it had just lit.
        /// </summary>
        private void SyncFireSpread(RoundEngine round)
        {
            if (session == null || session.Jokers == null || boardView == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var spread = owned[i] as SpreadJoker;
                if (spread != null && spread.LastSpread != null)
                {
                    boardView.FireSpread.Play(boardView, spread.LastSpread);
                }
            }
        }

        /// <summary>
        /// "Buzluk"'s freeze, asked every repaint and keyed on the joker's own SERIAL.
        ///
        /// The View works out none of it. Which cubes froze, what each one used to be, and WHICH
        /// SIDES ARE WALL are all the report's - and the last of those is the rule the picture
        /// exists to tell, so a View that derived it from "which cells are on the rim" would be
        /// a second copy of it. It would also be a WRONG copy: a wall here is any neighbour that
        /// is not play area, holes and eroded cells included.
        /// </summary>
        private void SyncBuzluk(RoundEngine round)
        {
            if (session == null || session.Jokers == null || boardView == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var buzluk = owned[i] as BuzlukJoker;
                if (buzluk != null && buzluk.LastFreeze != null)
                {
                    boardView.Ice.Play(boardView, buzluk.LastFreeze);
                }
            }
        }

        /// <summary>The talisman in the player's power inventory, or null.</summary>
        private TilsimPower FindTalisman()
        {
            if (session == null || session.Powers == null)
            {
                return null;
            }
            IReadOnlyList<Power> owned = session.Powers.Powers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as TilsimPower;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// WHERE "MAPUS" HAS ITS SEAL, asked every repaint - and everything about it comes from the
        /// boss's own report (MapusSealVisuals): which cell, how many turns it has held it, how
        /// close the row and the column through it are to completion, and whether the cap has just
        /// let a cell go. The View works out none of that; it plays it.
        ///
        /// Calling this on every repaint is safe by design: the same cell twice is the seal being
        /// HELD, and the seal view answers that by doing nothing rather than by rebuilding a
        /// prison the player is already looking at.
        /// </summary>
        private void SyncMapus(RoundEngine round)
        {
            var boss = round != null ? round.Boss as MapusBoss : null;
            MapusSealVisuals seal = boss != null ? boss.LastSeal : null;
            if (boss == null)
            {
                boardView.StopMapus();
                return;
            }
            if (seal == null || !seal.HasSeal)
            {
                // No seal this turn: too few free cells, or the cap released the only cell worth
                // taking. Either way the board breathes, and the release is a real beat.
                boardView.Mapus.Sync(boardView, null, seal != null && seal.Released);
                return;
            }
            boardView.Mapus.Sync(boardView, new MapusSealView.Seal
            {
                Cell = seal.Cell,
                TurnsHeld = seal.TurnsHeld,
                MaxTurns = seal.MaxTurns,
                RowGaps = seal.RowGaps,
                ColumnGaps = seal.ColumnGaps,
                RowHeldAlone = seal.RowHeldByTheSealAlone,
                ColumnHeldAlone = seal.ColumnHeldByTheSealAlone
            }, seal.Released);
        }

        /// <summary>
        /// Where the host cube is and who is riding it, asked every repaint. Both come from the
        /// JOKER - the View never works out which cube is a host, and never guesses the passenger
        /// from the cube's own material, which says nothing about who is on it.
        /// </summary>
        private void SyncParasite(RoundEngine round)
        {
            parasiteHosts.Clear();
            ParazitJoker parasite = FindParasite();
            if (parasite != null && parasite.HasBinding && parasite.HostPosition.HasValue
                && round != null && round.Board != null
                && round.Board.IsInside(parasite.HostPosition.Value))
            {
                Cube? cube = round.Board.GetCube(parasite.HostPosition.Value);
                if (cube.HasValue && cube.Value.Protected)
                {
                    parasiteHosts.Add(new ParasiteHostView.Host
                    {
                        Cell = parasite.HostPosition.Value,
                        Passenger = parasite.PassengerIdentity(session),
                        // The cube's OWN face, so the wrap can redraw it drained. LookOf takes it
                        // from the cube itself, magenta wash and all removed.
                        Look = LookOf(cube.Value)
                    });
                }
            }
            if (parasiteHosts.Count == 0 && !boardView.Parasite.Busy)
            {
                boardView.Parasite.Sync(boardView, parasiteHosts);
                return;
            }
            boardView.Parasite.Sync(boardView, parasiteHosts);
        }

        /// <summary>
        /// Every attempt the rules REFUSED on a host this turn, played in their order: the parasite
        /// grips harder, on the side Core says the force came from. A refusal with no direction
        /// gets an undirected clamp rather than a side this invented.
        /// </summary>
        private bool PlayParasiteRefusals(RoundEngine round)
        {
            if (round == null || round.MainBoard == null || boardView == null)
            {
                return false;
            }
            IReadOnlyList<HostRefusal> refusals = round.MainBoard.HostRefusals.Refusals;
            bool any = false;
            for (int i = 0; i < refusals.Count; i++)
            {
                HostRefusal r = refusals[i];
                boardView.Parasite.PlayRefusal(new ParasiteHostView.Refusal
                {
                    Cell = r.Cell,
                    Kind = r.Kind,
                    Step = new Vector2(r.Step.X, r.Step.Y),
                    HasDirection = r.HasDirection
                });
                any = true;
            }
            return any;
        }

        /// <summary>
        /// THE ONE THING THE PARASITE CANNOT HOLD: the player's own line took the host cube. The
        /// bonds break one at a time, the passenger is shown, and both go. Driven off the turn's
        /// destroyed cubes - a host that is in that list is a host the rules just killed.
        /// </summary>
        private bool PlayParasiteSeverance(RoundEngine round, TurnReport report)
        {
            if (report == null || boardView == null || parasiteHosts.Count == 0)
            {
                return false;
            }
            parasiteSevered.Clear();
            IReadOnlyList<DestroyedCube> destroyed = report.DestroyedCubes;
            for (int i = 0; i < destroyed.Count; i++)
            {
                for (int h = 0; h < parasiteHosts.Count; h++)
                {
                    if (destroyed[i].Pos.Equals(parasiteHosts[h].Cell))
                    {
                        parasiteSevered.Add(parasiteHosts[h].Cell);
                    }
                }
            }
            for (int i = 0; i < parasiteSevered.Count; i++)
            {
                // WHICH LINE TOOK IT, from the report: a row shears the wrap one way and a column
                // the other. A host on both is torn along the row, which is the one the eye follows.
                bool horizontal = true;
                for (int r = 0; r < report.ExplodedRows.Count; r++)
                {
                    if (report.ExplodedRows[r] == parasiteSevered[i].Y)
                    {
                        horizontal = true;
                        break;
                    }
                    horizontal = false;
                }
                boardView.Parasite.PlaySeverance(parasiteSevered[i], horizontal);
            }
            return parasiteSevered.Count > 0;
        }

        /// <summary>The lab's seam for a severance, and the game's: both go through here.</summary>
        private bool PlayParasiteSeveranceScene(GridPos cell, bool horizontal)
        {
            if (boardView == null)
            {
                return false;
            }
            boardView.Parasite.PlaySeverance(cell, horizontal);
            return true;
        }

        // ---- "Hidrolik pres" -------------------------------------------------------------
        //
        // THE SEAMS THE LAB AND THE GAME SHARE. Everything below takes a report the RULES wrote
        // (PressCompressionVisuals / PressReleaseVisuals) and turns it into world geometry -
        // nothing here decides which way the press opens, which cube moves, how far, or which
        // quadrant was empty. Where a cell is on the screen and what a cube's face looks like is
        // all this adds.

        /// <summary>The last reports played, by reference: the rules make a new one per event, so
        /// this is all it takes to know an event is new without Core carrying a counter for us.
        /// </summary>
        private PressCompressionVisuals pressSqueezePlayed;

        private PressReleaseVisuals pressReleasePlayed;

        /// <summary>The press in play, or null. One at a time by the rules (CanRun refuses while
        /// IsPressing), so the first one found is the one.</summary>
        private HidrolikPresPower FindPress()
        {
            if (session == null || session.Powers == null)
            {
                return null;
            }
            IReadOnlyList<Power> owned = session.Powers.Powers;
            for (int i = 0; i < owned.Count; i++)
            {
                var press = owned[i] as HidrolikPresPower;
                if (press != null)
                {
                    return press;
                }
            }
            return null;
        }

        /// <summary>
        /// What a shut press is told every repaint: how many turns it has left (the POWER's number,
        /// never one counted here) and the colours of the four quadrants inside it, so the release
        /// can bring them up under the seams. The plates themselves follow the board
        /// (BoardView.Refresh -> CompressedCubeView.Sync).
        /// </summary>
        private void SyncPress(RoundEngine round)
        {
            HidrolikPresPower press = FindPress();
            if (press == null || !press.IsPressing)
            {
                boardView.Press.SetCountdown(0, 4);
                return;
            }
            boardView.Press.SetCountdown(press.TurnsLeft, press.TurnsCompressed);
            PressCompressionVisuals squeeze = press.LastCompression;
            if (squeeze != null && squeeze.Swallowed.Count == 4)
            {
                boardView.Press.SetMemory(QuadMemory(squeeze.Swallowed[0]),
                    QuadMemory(squeeze.Swallowed[1]), QuadMemory(squeeze.Swallowed[2]),
                    QuadMemory(squeeze.Swallowed[3]));
            }
        }

        /// <summary>A stored quadrant's colour for the shell's memory - what the block is MADE OF,
        /// never its tint (a painted tile's tint is white). A transparent colour means the quadrant
        /// was EMPTY, and the shell shows nothing for it.</summary>
        private static Color QuadMemory(Cube? cube)
        {
            if (!cube.HasValue)
            {
                return new Color(0f, 0f, 0f, 0f);
            }
            Color paint = ViewUtil.CubeMaterialColor(cube.Value);
            return new Color(paint.r, paint.g, paint.b, 1f);
        }

        /// <summary>The slate a compressed cube is made of - the designer's hard industrial grey,
        /// taken from the one place that decides it.</summary>
        private static Color PressSlate()
        {
            return ViewUtil.CubeMaterialColor(
                new Cube(CubeKind.Compressed, GameBoard.PressCardId));
        }

        /// <summary>
        /// THE SQUEEZE, on the frame the board was repainted after the power ran. The four faces
        /// come from the report - taken before the rules emptied the cells, which is the only way
        /// the laminae can wear the cubes' own materials - and a quadrant the report stored as NULL
        /// is played as a hole, never as a cube this invented.
        /// </summary>
        private bool PlayPressCompression(RoundEngine round)
        {
            HidrolikPresPower press = FindPress();
            PressCompressionVisuals report = press != null ? press.LastCompression : null;
            if (report == null || ReferenceEquals(report, pressSqueezePlayed)
                || report.Cells.Count != 4 || boardView == null || boardView.Board == null)
            {
                return false;
            }
            pressSqueezePlayed = report;
            return PlayPressScene(PressSqueezeSceneOf(report));
        }

        /// <summary>
        /// A squeeze report turned into world geometry. The LAB goes through here too: it runs the
        /// real GameBoard.Compress on a board of its own and hands the report it gets back, so a
        /// retimed or reshaped compression shows up there for free.
        /// </summary>
        private HydraulicPressView.CompressionScene PressSqueezeSceneOf(
            PressCompressionVisuals report)
        {
            var scene = new HydraulicPressView.CompressionScene
            {
                CompressedCentre = boardView.CellToWorld(report.CompressedCell),
                CompressedCell = report.CompressedCell,
                Slate = PressSlate(),
                CellSize = boardView.CellWorldSize,
                CubeSize = boardView.CubeWorldSize
            };
            for (int i = 0; i < 4; i++)
            {
                Cube? cube = report.Swallowed[i];
                scene.Quads[i] = new HydraulicPressView.Quad
                {
                    Cell = report.Cells[i],
                    Centre = boardView.CellToWorld(report.Cells[i]),
                    Occupied = cube.HasValue,
                    Look = cube.HasValue ? LookOf(cube.Value) : new ClusterBurstView.Look()
                };
            }
            return scene;
        }

        /// <summary>
        /// THE RELEASE, on the frame the board was repainted after it opened. Every side the rules
        /// pressed is played in their order - the refusals included, with the cube that refused and
        /// what KIND it was - then every cube they moved, along its own reported step, with a
        /// destination that is outside the board when the cube went over the edge. When the rules
        /// detonated instead, the prelude plays and PressureVesselView takes the event over.
        /// </summary>
        private bool PlayPressRelease(RoundEngine round)
        {
            HidrolikPresPower press = FindPress();
            PressReleaseVisuals report = press != null ? press.LastRelease : null;
            if (report == null || ReferenceEquals(report, pressReleasePlayed)
                || report.Cells.Count != 4 || boardView == null || boardView.Board == null)
            {
                return false;
            }
            pressReleasePlayed = report;
            return PlayPressReleaseReport(report);
        }

        /// <summary>
        /// A release report turned into world geometry and played - the prelude's denials, the
        /// laminae, the push chain, and the failure when the rules detonated instead. The LAB goes
        /// through here too, off a real GameBoard.Expand on a board of its own.
        /// </summary>
        private bool PlayPressReleaseReport(PressReleaseVisuals report)
        {
            GameBoard board = boardView.Board;
            var scene = new HydraulicPressView.ReleaseScene
            {
                AnchorCentre = boardView.CellToWorld(report.PressCell),
                Slate = PressSlate(),
                CellSize = boardView.CellWorldSize,
                CubeSize = boardView.CubeWorldSize,
                DiagonalAxis = report.DiagonalAxis,
                Detonated = report.Detonated,
                BoardRect = boardView.WorldRect
            };
            for (int i = 0; i < 4; i++)
            {
                Cube? cube = report.Stored[i];
                scene.Quads[i] = new HydraulicPressView.Quad
                {
                    Cell = report.Cells[i],
                    Centre = boardView.CellToWorld(report.Cells[i]),
                    Occupied = cube.HasValue,
                    Look = cube.HasValue ? LookOf(cube.Value) : new ClusterBurstView.Look()
                };
            }
            for (int i = 0; i < report.Tests.Count; i++)
            {
                PressureTest test = report.Tests[i];
                scene.Denials.Add(new HydraulicPressView.Denial
                {
                    Step = new Vector2(test.Step.X, test.Step.Y),
                    Succeeded = test.Succeeded,
                    IsReroute = test.IsReroute,
                    BlockedAt = boardView.CellToWorld(test.BlockedAt),
                    BlockedKind = test.BlockedKind
                });
            }
            for (int i = 0; i < report.Pushes.Count; i++)
            {
                PressPush push = report.Pushes[i];
                // A cube that left the board has no cell to ask for a centre, so its destination is
                // stepped out from the one it came from - it really is outside the arena.
                Vector2 from = boardView.CellToWorld(push.From);
                Vector2 to = board.IsInside(push.To)
                    ? boardView.CellToWorld(push.To)
                    : from + new Vector2(push.Step.X, push.Step.Y) * boardView.CellWorldSize;
                scene.Shoves.Add(new HydraulicPressView.Shove
                {
                    From = from,
                    To = to,
                    Step = new Vector2(push.Step.X, push.Step.Y),
                    LeftBoard = push.LeftBoard,
                    Order = push.Order,
                    Target = push.To,
                    Look = LookOf(push.Cube)
                });
            }
            bool played = PlayPressReleaseScene(scene);
            if (report.Detonated)
            {
                var failure = new PressureVesselView.Scene
                {
                    // The shell collapses where it stood; the footprint is DetonatedCells.
                    Centre = boardView.CellToWorld(report.PressCell),
                    Slate = PressSlate(),
                    CellSize = boardView.CellWorldSize,
                    CubeSize = boardView.CubeWorldSize
                };
                for (int i = 0; i < 4; i++)
                {
                    failure.Memory[i] = QuadMemory(report.Stored[i]);
                }
                for (int i = 0; i < report.DetonatedCells.Count; i++)
                {
                    CubeKind kind = i < report.DetonatedKinds.Count
                        ? report.DetonatedKinds[i]
                        : CubeKind.Normal;
                    // The cube's own face, kept from the repaint that emptied the cell - the same
                    // bargain every blast in the game makes (BoardView.TryCubeLook).
                    Sprite tile;
                    Color colour;
                    ClusterBurstView.Look look = boardView.TryCubeLook(report.DetonatedCells[i],
                        CubeLookMaxAge, out tile, out colour)
                        ? new ClusterBurstView.Look { Tile = tile, Colour = colour }
                        : new ClusterBurstView.Look();
                    if (look.Paint.a <= 0f)
                    {
                        Color paint = ViewUtil.CubeMaterialColor(new Cube(kind, 0));
                        look.Paint = new Color(paint.r, paint.g, paint.b, 1f);
                    }
                    failure.Casualties.Add(new PressureVesselView.Casualty
                    {
                        Centre = boardView.CellToWorld(report.DetonatedCells[i]),
                        Kind = kind,
                        Look = look
                    });
                }
                played |= PlayPressFailureScene(failure);
            }
            return played;
        }

        /// <summary>The squeeze's seam. The lab and the game both go through here, so a retimed
        /// compression shows its new timing in both for free.</summary>
        private bool PlayPressScene(HydraulicPressView.CompressionScene scene)
        {
            if (hydraulicPress == null || scene == null || boardView == null)
            {
                return false;
            }
            // The shell the compression closes is ITS plate while it plays; the standing press's
            // own layer stands off that cell so two things never draw one shell.
            boardView.Press.Suppress(scene.Quads[0].Cell, true);
            hydraulicPress.PlayCompression(boardView, scene);
            StartCoroutine(ReleasePressSuppression(scene.Quads[0].Cell));
            return true;
        }

        /// <summary>The release's seam.</summary>
        private bool PlayPressReleaseScene(HydraulicPressView.ReleaseScene scene)
        {
            if (hydraulicPress == null || scene == null || boardView == null)
            {
                return false;
            }
            hydraulicPress.PlayRelease(boardView, scene);
            return true;
        }

        /// <summary>The failure's seam - its own event, never a removal variant and never the
        /// cluster burst.</summary>
        private bool PlayPressFailureScene(PressureVesselView.Scene scene)
        {
            if (pressureVessel == null || scene == null)
            {
                return false;
            }
            pressureVessel.Play(scene);
            return true;
        }

        /// <summary>Hands a cell back to the standing press's own layer once the compression has
        /// finished closing its shell on it.</summary>
        private IEnumerator ReleasePressSuppression(GridPos cell)
        {
            float guard = 0f;
            while (hydraulicPress != null && hydraulicPress.Busy && guard < 4f)
            {
                guard += Time.deltaTime;
                yield return null;
            }
            if (boardView != null)
            {
                boardView.Press.Suppress(cell, false);
            }
        }

        /// <summary>
        /// Which segment a cut's constriction starts from: a cell of the snake that is ON the line
        /// Core says exploded, the one nearest the head, so the signal is seen running the whole way
        /// down to the tail. The LINE is Core's - only which of its cells to start at is ours.
        /// </summary>
        private GridPos SnakeCutTrigger(SnakeTurnVisuals turn, int index, List<GridPos> standing)
        {
            if (index >= turn.CutRows.Count + turn.CutColumns.Count)
            {
                // A cut from a non-line explosion: no line to start from, so start at the head.
                return standing.Count > 0 ? standing[0] : new GridPos(0, 0);
            }
            bool isRow = index < turn.CutRows.Count;
            int line = isRow ? turn.CutRows[index] : turn.CutColumns[index - turn.CutRows.Count];
            for (int i = 0; i < standing.Count; i++)
            {
                if (isRow ? standing[i].Y == line : standing[i].X == line)
                {
                    return standing[i];
                }
            }
            return standing.Count > 0 ? standing[standing.Count - 1] : new GridPos(0, 0);
        }

        /// <summary>Puts a snake on the board - the same two calls SyncSnake makes, for the lab's
        /// own board.</summary>
        private void PlaySnakeBody(IReadOnlyList<GridPos> body, bool spawn)
        {
            if (boardView == null || boardView.Board == null)
            {
                return;
            }
            boardView.Snake.Sync(boardView);
            boardView.Snake.SetBody(body, spawn);
        }

        /// <summary>The seam the snake is played through - the game from its report above, the
        /// animation lab from a snake of its own.</summary>
        private bool PlaySnakeScene(SnakeView.TurnScene scene)
        {
            if (scene == null || boardView == null || boardView.Board == null)
            {
                return false;
            }
            boardView.Snake.Sync(boardView);
            boardView.Snake.PlayTurn(scene);
            return true;
        }

        private static void CopyCells(IReadOnlyList<GridPos> from, List<GridPos> into)
        {
            into.Clear();
            if (from == null)
            {
                return;
            }
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        /// <summary>The seam the rot is played through - the game from its report above, the
        /// animation lab from what the same board code wrote on a board of its own.</summary>
        private bool PlayGangreneScene(GangreneView.TurnScene scene)
        {
            if (scene == null || boardView == null || boardView.Board == null)
            {
                return false;
            }
            boardView.Gangrene.Sync(boardView);
            boardView.Gangrene.PlayTurn(scene);
            return true;
        }

        /// <summary>The face and colour a cube is drawn with - the board's own rule, so a cube that
        /// is about to die on screen looks exactly like the one that was standing there.</summary>
        private ClusterBurstView.Look LookOf(Cube cube)
        {
            Sprite tile = ViewUtil.CubeTile(cube.Kind, FindOwnedCard(cube.SourceCardId));
            return new ClusterBurstView.Look
            {
                Tile = tile,
                Colour = ViewUtil.CubeTileColor(cube, tile),
                Paint = ViewUtil.CubeMaterialColor(cube)
            };
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
            if (bossIdentity != null)
            {
                bossIdentity.ReactSweep();
            }
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

        /// <summary>
        /// Drops a defective block's cubes off the screen, each wearing the face it had in the hand.
        ///
        /// They used to be flat squares in the block's element colour, which threw away the one
        /// thing that tells block types apart - their art. Every cube now asks ViewUtil.CardCubeTile,
        /// the question the hand asks, so a fox falls as a fox, a gear as a gear, and a targeted
        /// block with its bullseye on the cube that carried it.
        ///
        /// The shape is the one the card was PLACED with (RoundEngine.EffectiveShape - rotated,
        /// reshaped), and the reported cells leave out any that were outside the arena, so a cube's
        /// place in the list is not its place in the shape: each is matched back through the shape.
        /// </summary>
        private void SpawnFallingCubes(BoardView view, IReadOnlyList<GridPos> cells, BlockCard card)
        {
            if (view == null || cells == null || cells.Count == 0)
            {
                return;
            }
            BlockShape shape = null;
            if (card != null)
            {
                RoundEngine round = session != null ? session.CurrentRound : null;
                shape = round != null ? round.EffectiveShape(card) : card.Shape;
            }
            GridPos origin = shape != null ? FallOrigin(cells, shape) : new GridPos(0, 0);
            for (int i = 0; i < cells.Count; i++)
            {
                Sprite tile = null;
                Color tint = ViewUtil.ColorForCard(card != null ? card.Id : 0);
                if (shape != null)
                {
                    int index = ShapeIndexOf(shape,
                        new GridPos(cells[i].X - origin.X, cells[i].Y - origin.Y));
                    if (index >= 0)
                    {
                        tile = ViewUtil.CardCubeTile(card, shape, index,
                            ReferenceEquals(shape, card.Shape), out tint);
                    }
                    else
                    {
                        // Not a cube of the shape (it always should be) - still the card's own face.
                        tile = ViewUtil.CubeTile(CubeKind.Normal, card);
                        tint = ViewUtil.CubeTileColor(tile, tint);
                    }
                }
                FallingCubeFx.Spawn(transform, view.CellToWorld(cells[i]), view.CubeWorldSize,
                    tile, tint, i * 0.045f);
            }
        }

        /// <summary>Where the card was placed, recovered from the cells it covered: the offset at
        /// which every reported cell is a cube of the shape. The first cell is tried against each
        /// cube in turn, so cells missing past the edge of the arena cannot throw it off.</summary>
        private static GridPos FallOrigin(IReadOnlyList<GridPos> cells, BlockShape shape)
        {
            GridPos first = cells[0];
            for (int k = 0; k < shape.Cells.Count; k++)
            {
                var origin = new GridPos(first.X - shape.Cells[k].X, first.Y - shape.Cells[k].Y);
                bool fits = true;
                for (int i = 1; i < cells.Count && fits; i++)
                {
                    fits = ShapeIndexOf(shape,
                        new GridPos(cells[i].X - origin.X, cells[i].Y - origin.Y)) >= 0;
                }
                if (fits)
                {
                    return origin;
                }
            }
            return new GridPos(first.X - shape.Cells[0].X, first.Y - shape.Cells[0].Y);
        }

        private static int ShapeIndexOf(BlockShape shape, GridPos offset)
        {
            for (int i = 0; i < shape.Cells.Count; i++)
            {
                if (shape.Cells[i].Equals(offset))
                {
                    return i;
                }
            }
            return -1;
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
            // Everything after the first is a RE-reveal: the same language, held a little
            // shorter, because by then the player knows the ritual and only needs the look.
            bool again = mine.ShuffleCount > 1;
            lastMineShuffle = mine.ShuffleCount;
            // A detonation arms a fresh mine on the spot, so the blast and this reveal are the
            // same frame unless the dance waits for it. See TriggerMineDetonation.
            mineShuffle.Play(boardView, round.MainBoard, mine.ShufflePath, again,
                mineDetonationPending ? MineBlastSeconds : 0f);
        }

        /// <summary>How long the detonation gets the screen to itself before the replacement
        /// mine's reveal begins.</summary>
        private const float MineBlastSeconds = 1.15f;

        /// <summary>
        /// "Mayın eşeği" going off. Nothing drew this at all before: the mine took half the round
        /// and said so nowhere, which is indistinguishable from the mine doing nothing.
        ///
        /// It is watched by COUNT rather than by a flag on the report, the same way the shuffle
        /// is, because a detonation is the boss's own bookkeeping and never appears in a
        /// TurnReport. The cell comes from LastDetonationCell, not MineCell - a fresh mine is
        /// already armed somewhere else by the time this runs, and the blast belongs over the old
        /// one.
        /// </summary>
        private void TriggerMineDetonation(RoundEngine round)
        {
            var mine = round != null ? round.Boss as MayinEsegiBoss : null;
            if (mine == null)
            {
                lastMineDetonations = 0;
                mineDetonationPending = false;
                return;
            }
            if (mine.Detonations == lastMineDetonations)
            {
                return;
            }
            lastMineDetonations = mine.Detonations;
            mineDetonationPending = true;
        }

        /// <summary>Draws the detonation the check above noticed: the cell goes off in the mine's
        /// own red, the camera takes it, and the number it cost is said out loud over the board.
        /// Split from the check because the CHECK has to run before the refresh (so the shuffle
        /// knows to wait) while the DRAWING belongs after the line explosion that caused it.
        /// </summary>
        private void PlayMineDetonationFeedback(RoundEngine round)
        {
            var mine = round != null ? round.Boss as MayinEsegiBoss : null;
            if (mine == null || !mineDetonationPending)
            {
                return;
            }
            mineDetonationPending = false;
            FlashCells(new List<GridPos> { mine.LastDetonationCell }, MineShuffleView.MineColor);
            ShakeCamera(0.34f, 0.16f, 2.4f);
            sfx.Explode();
            FloatingTextFx.Spawn(transform, new Vector2(0f, 3.0f),
                Loc.Pick("MINE!", "MAYIN!"), MineShuffleView.MineColor, 76, 0.09f);
            if (mine.LastDetonationLoss > 0)
            {
                FloatingTextFx.Spawn(transform, new Vector2(0f, 2.1f),
                    Loc.Pick("HALF THE ROUND GONE  -", "RAUNDUN YARISI GİTTİ  -")
                        + mine.LastDetonationLoss,
                    new Color(1f, 0.35f, 0.35f), 58, 0.08f);
            }
            messageText.text = Loc.Pick(
                "The mine went off - half of everything this round was banked is gone.",
                "Mayın patladı - bu raunt topladığın puanın yarısı gitti.");
            UpdateHud();
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

        /// <summary>A pile as a list with its TOP card first.</summary>
        private static List<BlockCard> TopFirst(IReadOnlyList<BlockCard> pile)
        {
            var list = new List<BlockCard>(pile.Count);
            for (int i = pile.Count - 1; i >= 0; i--)
            {
                list.Add(pile[i]);
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
                // A new arena: the snake in it has not woken up yet, and no turn of its own has
                // been played.
                snakeSpawned = false;
                snakeTurnPlayed = 0;
            }
            // "Alacakaranlık" - set BEFORE the refresh, so the very first paint is already dark.
            boardView.SetDarkness(round.BoardIsDark);
            boardView.Refresh();
            SyncSnake(round);
            SyncPress(round);
            SyncParasite(round);
            SyncMapus(round);
            SyncTalisman(round);
            SyncFireSpread(round);
            SyncBuzluk(round);
            SyncRebate(round);
            SyncQuake();
            SyncChallenge();
            SyncQuarry();
            SyncIgnition();
            SyncPowder();
            SyncMetamorphosis();
            boardView.SetDeadZone(session.Config.Rules.DeadZoneRows);
            boardView.ClearPreview();
            RefreshMirrorWorld();
            RefreshInfections(report);
            cardLayer.Sync(round, report);
            // AFTER the hand is laid out, never before: the payout is drawn on the held cards and
            // they are not where the player will see them until this call has run.
            SyncMidas(round);
            // "Tamagotchi" lays out what it is still owed, next to the hand it has to come from.
            var pet = round.Boss as TamagotchiBoss;
            cardLayer.ShowPetDemands(pet != null ? pet.Demands : null);
            RefreshFlames(round.ContinueCount);
            UpdateHud();
            jokerBar.Refresh(session, pendingTargetJokerId);
            powerBar.Refresh(session, pendingTargetPowerId);
            // AFTER the power bar, never before: "Yer altı kaynakları"'s fill UNCOVERS a card the
            // bar has already painted charged, so the charged card has to be there to uncover.
            SyncSeam();
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
                boardView.ShowDolls(null, null, 0);
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
            RefreshBossBoardMarks(report);
            // "Kütleçekim merkezi": which way the arena is pulling water. A no-op on every board
            // that pulls it downward, which is nearly all of them.
            boardView.ShowGravity(session.CurrentRound.Board.WaterFlow);
        }

        /// <summary>Hands the board view what the new bosses have put ON it: "Matruşka"'s dolls
        /// and "İstilacı"'s marked column. Both are marks on the arena rather than cubes in it,
        /// so neither belongs in the cube pass.</summary>
        private void RefreshBossBoardMarks(TurnReport report)
        {
            RoundEngine round = session.CurrentRound;
            BossRound boss = round != null ? round.Boss : null;

            var dolls = boss as MatruskaBoss;
            if (dolls != null)
            {
                IReadOnlyList<GridPos> cells = dolls.DollCells;
                var generations = new List<int>(cells.Count);
                for (int i = 0; i < cells.Count; i++)
                {
                    generations.Add(dolls.GenerationAt(cells[i]));
                }
                // A turn that DID something to the dolls is staged, not repainted: the board is told
                // where they end up and what happened on the way, and keeps its picture until the
                // cubes actually go (PlayExplosionFeedback releases it).
                if (report != null && report.DollEvents.Count > 0)
                {
                    boardView.HoldDolls(report.DollEvents, cells, generations, dolls.Generations);
                }
                else
                {
                    boardView.ShowDolls(cells, generations, dolls.Generations);
                }
            }
            else
            {
                boardView.ShowDolls(null, null, 0);
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
            // "Eforsuz galibiyet" pays walking into the shop, which is not a turn and has no
            // board - so it is celebrated here rather than through any of the turn seams. Keyed
            // on the PAYOUT, so a rebuilt shop does not rain twice.
            CheckEffortlessPayout();
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
                ? Loc.Pick(
                    PadOr("Click KAÇAKÇI, then an offer: take it FREE (may be defective)",
                        "A on KAÇAKÇI, then an offer: take it FREE (may be defective)"),
                    PadOr("KAÇAKÇI'ya tıkla, sonra bir ürüne: BEDAVA (defolu çıkabilir)",
                        "KAÇAKÇI'da A, sonra bir ürüne: BEDAVA (defolu çıkabilir)"))
                : string.Empty;
        }

        private void UpdateHud()
        {
            UpdateScoreHud();
            RefreshBossBadge();
            // The "SELL CARDS" plate over the draw pile is retired: the pile says it is clickable
            // with its hover outline, and the market's own DECK button names the action. Kept
            // explicitly off so a plate built by an older path never lingers.
            cardLayer.SetSellHint(false);
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
                if (round.Board.BlightedCellCount > 0)
                {
                    sb.Append(Loc.Pick("  dead zone ", "  ölü bölge ")).Append(round.Board.BlightedCellCount);
                }
                if (round.FreeDeckRecyclesLeft > 0)
                {
                    sb.Append(Loc.Pick("   free reshuffles ", "   bedava karma "))
                        .Append(round.FreeDeckRecyclesLeft);
                }
                else
                {
                    bool zone = round.Config.Erosion != ShuffleErosion.FromOutside;
                    sb.Append(zone
                        ? Loc.Pick("   ERODING - next reshuffle grows the dead zone",
                            "   ERİYOR - sıradaki karma ölü bölgeyi büyütür")
                        : Loc.Pick("   ERODING - next reshuffle eats the board",
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
                else if (shop != null && shop.Targeting == ActivationTargeting.BoardArea)
                {
                    step = workshopPressAnchor.HasValue
                        ? Loc.Pick("now pick WHICH of the four cells keeps the pressed cube",
                            "şimdi preslenen küpün dört kareden HANGİSİNDE kalacağını seç")
                        : Loc.Pick("pick the 2x2 patch to squeeze",
                            "sıkıştırılacak 2x2'lik alanı seç");
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
                case LossReason.DeadZoneOverran:
                    return Loc.Pick("the dead zone swallowed the arena (the deck ran dry once too often)",
                        "ölü bölge alanı yuttu (deste çok kez bitti)");
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
