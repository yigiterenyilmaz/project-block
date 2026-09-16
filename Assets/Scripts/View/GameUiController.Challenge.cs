// PURPOSE: "Meydan Okuma"'s contract, wired up - the one place ChallengeContractView is reached
// from, by the game and by the animation lab alike.
//
// THREE THINGS LIVE HERE. WHERE a dared line is (its first and last PLAY cells along the line as
// Core indexes it, through the arena's own transform, so an irregular board spans only what is
// really there and the contract rides the dynamite's knock and the overtime squeeze); WHERE the
// wager token fits (just past one end of the line, on the first side that is on screen - a row
// prefers its right end, a column its bottom end, because the score is at the top); and WHEN each
// thing is shown.
//
// THE WHEN IS THE SUBTLE PART. The joker writes its event during the turn, and the repaint runs
// before the turn's explosions are drawn - behind any water that is still falling. So the repaint
// only ever hands over the joker's LIVE state, and only once the last event has been played; the
// event itself is played from PlayExplosionFeedback, which is the moment the line that paid the
// dare actually breaks. Without that the contract would jump to its new line, or vanish, before
// the player had seen why.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private ChallengeContractView challenge;

        private MeydanOkumaJoker FindMeydan()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as MeydanOkumaJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        /// <summary>The repaint's half: the joker's live contract, once its events are shown.
        /// </summary>
        private void SyncChallenge()
        {
            if (boardView == null)
            {
                return;
            }
            MeydanOkumaJoker joker = FindMeydan();
            if (joker == null)
            {
                if (challenge != null)
                {
                    challenge.Stop();
                }
                return;
            }
            EnsureChallenge();
            if (!challenge.HasPlayed(joker.LastEvent))
            {
                return; // the turn that changed it has not been drawn yet
            }
            challenge.SetSteady(ContractOf(joker), joker.PendingBonus);
        }

        /// <summary>The turn's half: the event, as the turn's lines break.</summary>
        private void PlayChallengeEvent()
        {
            MeydanOkumaJoker joker = FindMeydan();
            if (joker == null || joker.LastEvent == null || boardView == null)
            {
                return;
            }
            EnsureChallenge();
            challenge.Play(joker.LastEvent);
        }

        private static ChallengeContractView.Contract ContractOf(MeydanOkumaJoker joker)
        {
            return new ChallengeContractView.Contract
            {
                Has = joker.HasActiveMark,
                IsRow = joker.MarkIsRow,
                Line = joker.MarkedLine,
                Bonus = joker.CurrentBonus,
                Attempt = joker.AttemptsMade,
                TurnsLeft = joker.TurnsLeft,
                InitialTurns = joker.InitialTurns
            };
        }

        private void EnsureChallenge()
        {
            if (challenge == null)
            {
                var go = new GameObject("ChallengeContract");
                go.transform.SetParent(transform, false);
                challenge = go.AddComponent<ChallengeContractView>();
                challenge.Locate = LocateChallengeLine;
                challenge.ScoreAnchor = ScoreWorldAnchor;
            }
        }

        private void StopChallenge()
        {
            if (challenge != null)
            {
                challenge.Stop();
                // What the joker last reported is not "still to come" after a reset: the live state
                // below is its result.
                MeydanOkumaJoker joker = FindMeydan();
                challenge.MarkPlayed(joker != null ? joker.LastEvent : null);
            }
        }

        /// <summary>
        /// A dared line on screen. <paramref name="line"/> is 0-based in the board's own box, the
        /// space TurnReport.ExplodedRows / ExplodedColumns (and the joker) use.
        /// </summary>
        private ChallengeContractView.Line LocateChallengeLine(bool isRow, int line)
        {
            var result = new ChallengeContractView.Line();
            GameBoard board = boardView != null ? boardView.Board : null;
            if (board == null)
            {
                return result;
            }
            int count = isRow ? board.Width : board.Height;
            int first = -1;
            int last = -1;
            for (int i = 0; i < count; i++)
            {
                GridPos p = isRow ? new GridPos(board.MinX + i, board.MinY + line)
                    : new GridPos(board.MinX + line, board.MinY + i);
                if (board.IsInside(p))
                {
                    if (first < 0) { first = i; }
                    last = i;
                }
            }
            if (first < 0)
            {
                return result;
            }
            Transform arena = boardView.transform;
            GridPos pa = isRow ? new GridPos(board.MinX + first, board.MinY + line)
                : new GridPos(board.MinX + line, board.MinY + first);
            GridPos pb = isRow ? new GridPos(board.MinX + last, board.MinY + line)
                : new GridPos(board.MinX + line, board.MinY + last);
            result.A = arena.TransformPoint(boardView.CellToWorld(pa));
            result.B = arena.TransformPoint(boardView.CellToWorld(pb));
            result.Along = isRow ? (Vector2)arena.right : (Vector2)arena.up;
            result.Across = new Vector2(-result.Along.y, result.Along.x);
            result.Cell = boardView.CellWorldSize * arena.lossyScale.x;
            result.Cells = last - first + 1;
            result.Pixel = cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
            result.Valid = true;

            // The token: just past one end, on the first side that is on screen.
            Rect visible = HazineVisibleRect();
            float reach = (0.5f + ChallengeContractView.Style.ClampOut + 0.16f) * result.Cell;
            float halfW = ChallengeShapes.TokenHalfWidth * ChallengeContractView.Style.TokenWidth * result.Cell;
            float halfH = ChallengeShapes.TokenHalfHeight * ChallengeContractView.Style.TokenWidth * result.Cell;
            Vector2 atB = result.B + result.Along * (reach + (isRow ? halfW : halfH + 0.1f * result.Cell));
            Vector2 atA = result.A - result.Along * (reach + (isRow ? halfW : halfH + 0.1f * result.Cell));
            Vector2 first_ = isRow ? atB : atA;
            Vector2 second = isRow ? atA : atB;
            result.Token = Fits(first_, halfW, halfH, visible) ? first_
                : Fits(second, halfW, halfH, visible) ? second
                : ClampInto(first_, halfW, halfH, visible);
            return result;
        }

        private static bool Fits(Vector2 p, float hw, float hh, Rect r)
        {
            return r.width <= 0f || (p.x - hw >= r.xMin && p.x + hw <= r.xMax && p.y - hh >= r.yMin
                && p.y + hh <= r.yMax);
        }

        private static Vector2 ClampInto(Vector2 p, float hw, float hh, Rect r)
        {
            if (r.width <= 0f)
            {
                return p;
            }
            return new Vector2(Mathf.Clamp(p.x, r.xMin + hw, r.xMax - hw), Mathf.Clamp(p.y, r.yMin + hh, r.yMax - hh));
        }
    }
}
