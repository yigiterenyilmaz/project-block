// PURPOSE: "Midas"'s GOLDEN DIVIDEND, wired up - the one place the payout animation is reached
// from, by the game and by the animation lab alike.
//
// THREE THINGS LIVE HERE AND NOTHING ELSE. WHERE the gold is (the held cards' own visuals, asked
// of CardLayerView and placed by CardVisual's own cube layout, never a formula copied out of it);
// WHERE the money is going (the real score line in the HUD, converted to world space - never a
// screen coordinate written down); and HOW the score answers when it lands.
//
// The animation itself is MidasPayoutView, and what it draws is MidasPayoutVisuals - the joker's
// own report. Nothing here counts a cube or works out an amount.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private MidasPayoutView midasPayout;

        /// <summary>The score line's own scale and colour, kept so the payout can warm it and
        /// hand it back exactly as it was. Read once, the first time it is warmed.</summary>
        private Color midasScoreInk;

        private bool midasScoreRead;

        /// <summary>
        /// The joker's payout, asked every repaint and keyed on the report's SERIAL - so a hand
        /// redrawn behind the animation cannot restart it.
        ///
        /// It runs AFTER cardLayer.Sync on purpose: the payout is drawn on the held cards, and
        /// before that call they are not yet where the player is about to see them.
        /// </summary>
        private void SyncMidas(RoundEngine round)
        {
            if (midasPayout == null || round == null)
            {
                return;
            }
            MidasJoker midas = FindMidas();
            if (midas == null || midas.LastPayout == null || !midas.LastPayout.Any)
            {
                return;
            }
            List<MidasPayoutView.SourceSpot> spots = MidasSpots(midas.LastPayout);
            if (spots.Count == 0)
            {
                return;
            }
            Vector2 score = ScoreWorldAnchor();
            midasPayout.Play(midas.LastPayout, spots, score, MidasTotalAnchor(score));
        }

        private MidasJoker FindMidas()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as MidasJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>
        /// Turns the report's sources into places on the screen.
        ///
        /// A source whose card has no visual (it is off screen, or the hand has already moved on)
        /// is simply dropped rather than guessed at: a payout drawn at a made-up position tells
        /// the player the money came from somewhere it did not.
        /// </summary>
        private List<MidasPayoutView.SourceSpot> MidasSpots(MidasPayoutVisuals report)
        {
            var spots = new List<MidasPayoutView.SourceSpot>();
            for (int i = 0; i < report.Sources.Count; i++)
            {
                MidasGoldSource source = report.Sources[i];
                CardVisual visual = cardLayer != null ? cardLayer.Held(source.CardId) : null;
                if (visual == null || source.Shape == null)
                {
                    continue;
                }
                spots.Add(MidasSpotFor(visual, source.Shape, source.Subtotal));
            }
            return spots;
        }

        /// <summary>One card's gold cubes, in world space - through CardVisual's OWN layout, so
        /// the glint sits on the cube rather than near it however the card is scaled.</summary>
        private static MidasPayoutView.SourceSpot MidasSpotFor(CardVisual visual, BlockShape shape,
            int subtotal)
        {
            var cubes = new List<Vector2>();
            IReadOnlyList<GridPos> cells = shape.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3 local = CardVisual.MiniCubeLocal(shape, cells[i]);
                cubes.Add(visual.transform.TransformPoint(local));
            }
            // The cube's size on screen is the card's own scale applied to its layout size, and
            // the card's half-height the same way - the payout parks its number just clear of
            // the card's top edge, so it has to know where that edge actually is.
            float scale = visual.transform.lossyScale.x;
            return new MidasPayoutView.SourceSpot
            {
                Card = visual.transform.position,
                CardHalf = CardVisual.BodyHeight * 0.5f * scale,
                Cubes = cubes,
                CubeSize = CardVisual.MiniCubeSize(shape) * scale,
                Subtotal = subtotal
            };
        }

        /// <summary>
        /// WHERE THE SCORE IS, in world units.
        ///
        /// The HUD is a ScreenSpaceOverlay canvas, so the text's position is in screen pixels and
        /// has to be put through the camera. Asked of the real label every time rather than
        /// written down: the HUD moves with the layout profile, and a hard-coded corner is a
        /// payout that flies into empty space on a phone.
        /// </summary>
        private Vector2 ScoreWorldAnchor()
        {
            if (totalText == null || cam == null)
            {
                return new Vector2(0f, 3.6f);
            }
            Vector3 screen = totalText.rectTransform.position;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                Mathf.Abs(cam.transform.position.z)));
            return new Vector2(world.x, world.y);
        }

        /// <summary>
        /// WHERE THE TOTAL FITS, which is not "just above the score".
        ///
        /// The score line is anchored to the TOP of the screen, so the first version's popup
        /// went straight off it and the reward - the one number the whole payout is building
        /// toward - was the one thing nobody ever saw. It goes on the INSIDE of the score
        /// instead, and is then clamped into the camera's own rect with a margin, so no HUD
        /// layout can push it out again.
        /// </summary>
        private Vector2 MidasTotalAnchor(Vector2 score)
        {
            if (cam == null)
            {
                return score - new Vector2(0f, 0.7f);
            }
            const float margin = 0.8f;
            Vector3 eye = cam.transform.position;
            float halfY = cam.orthographicSize;
            float halfX = halfY * cam.aspect;
            // Toward the middle of the screen, whichever side of it the score is on.
            float y = score.y > eye.y ? score.y - 0.7f : score.y + 0.7f;
            return new Vector2(
                Mathf.Clamp(score.x, eye.x - halfX + margin, eye.x + halfX - margin),
                Mathf.Clamp(y, eye.y - halfY + margin, eye.y + halfY - margin));
        }

        /// <summary>
        /// THE LAB'S WAY IN, and it is the same animation the game plays - only the ARGUMENTS
        /// are made up, which is the rule every lab entry in this file's neighbourhood follows.
        ///
        /// The lab cannot deal itself a gold hand (that would be Core state), so it lays out
        /// cards of its own through CardLayerView.ShowLabHand and drives the payout against
        /// THOSE visuals. The report is built the way the joker builds it - cube count off the
        /// shape, subtotal off the live PointsPerGoldCubeHeld - so a rebalanced joker shows its
        /// new number here without this being touched.
        /// </summary>
        public void PlayMidasDebug(IReadOnlyList<CardVisual> hand, IReadOnlyList<BlockShape> shapes,
            int pointsPerCube)
        {
            if (midasPayout == null || hand == null || shapes == null)
            {
                return;
            }
            var report = new MidasPayoutVisuals
            {
                Serial = Time.frameCount,
                PointsPerGoldCube = pointsPerCube
            };
            var spots = new List<MidasPayoutView.SourceSpot>();
            for (int i = 0; i < hand.Count && i < shapes.Count; i++)
            {
                if (hand[i] == null || shapes[i] == null)
                {
                    continue;
                }
                int cubes = shapes[i].Size;
                int subtotal = cubes * pointsPerCube;
                report.Sources.Add(new MidasGoldSource
                {
                    CardId = -1,
                    BonusHand = false,
                    Slot = i,
                    Shape = shapes[i],
                    GoldCubes = cubes,
                    Subtotal = subtotal
                });
                report.TotalScore += subtotal;
                spots.Add(MidasSpotFor(hand[i], shapes[i], subtotal));
            }
            if (spots.Count == 0)
            {
                return;
            }
            Vector2 score = ScoreWorldAnchor();
            midasPayout.Play(report, spots, score, MidasTotalAnchor(score));
        }

        /// <summary>Stops a payout the lab started and takes its hand away.</summary>
        public void StopMidasDebug()
        {
            if (midasPayout != null)
            {
                midasPayout.Stop();
            }
            if (cardLayer != null)
            {
                cardLayer.ClearLabHand();
            }
        }

        /// <summary>
        /// The score line answering a payout: a little bigger and a shade warmer while the gold
        /// is landing, back to itself the moment it stops.
        ///
        /// The VIEW does not reach in here - it reports how warm it is and this applies it, so
        /// one transform has one owner. Ticked from Update.
        /// </summary>
        // THE SCORE LINE'S ANSWER MOVED to GameUiController.Rebate.TickScoreResponse, because
        // two effects can warm the same label and each one writing it directly meant whichever
        // ticked second won - and the one that had finished would reset the label to normal
        // while the other was still mid-punch. MidasPayoutView.ScoreWarm is what it reads.
    }
}
