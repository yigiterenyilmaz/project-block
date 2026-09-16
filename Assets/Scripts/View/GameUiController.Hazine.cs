// PURPOSE: "Hazine"'s finds, wired up - the one place the reveal is reached from, by the game and
// by the animation lab alike.
//
// FOUR THINGS LIVE HERE AND NOTHING ELSE. WHEN each found cube breaks on screen (asked of the
// destruction that took it: a line breaks as its sweep reaches the cell, anything else as the
// cluster burst lets go - so the reveal follows the break instead of racing it); WHERE the found
// value goes (the real score label, the real power panel, the real card, the discard pile - asked
// every time, never written down, and nowhere at all when there is no such thing on screen); HOW
// that target answers when it lands; and the board-local knock, which goes to BoardView as a term
// of its own.
//
// It is asked from PlayExplosionFeedback rather than the repaint, because that is when the cubes
// actually go - behind any water that fell first - and a treasure revealed before the line that
// found it has broken is a treasure that knew it was coming.
//
// The animation itself is HazineRevealView and what it draws is HazineVisuals - the joker's own
// report. Nothing here decides what was found or what it did.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private HazineRevealView hazine;

        private void SyncHazine(RoundEngine round, TurnReport report)
        {
            if (session == null || session.Jokers == null || boardView == null || round == null)
            {
                return;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var joker = owned[i] as HazineJoker;
                if (joker != null && joker.LastFind != null)
                {
                    PlayHazine(joker.LastFind, round.Board, report, joker.InstanceId);
                }
            }
        }

        /// <summary>The seam the game and the lab share. The lab fabricates only the REPORT (and
        /// the turn's exploded lines, which decide when each cube breaks).</summary>
        private void PlayHazine(HazineVisuals find, GameBoard board, TurnReport report, int jokerId,
            IReadOnlyList<int> labRows = null, IReadOnlyList<int> labColumns = null)
        {
            if (find == null || boardView == null)
            {
                return;
            }
            EnsureHazine();
            var delays = new List<float>();
            for (int i = 0; i < find.Discoveries.Count; i++)
            {
                GridPos cell = find.Discoveries[i].Cell;
                IReadOnlyList<int> rows = report != null ? report.ExplodedRows : labRows;
                IReadOnlyList<int> columns = report != null ? report.ExplodedColumns : labColumns;
                delays.Add(HazineBreakDelay(board, rows, columns, cell));
            }
            Transform arena = boardView.transform;
            BoardView view = boardView;
            hazine.Play(find,
                delegate(GridPos cell) { return (Vector2)arena.TransformPoint(view.CellToWorld(cell)); },
                boardView.CellWorldSize * arena.lossyScale.x, delays,
                HazineBoardCentre(board),
                HazineDestination(find, jokerId), HazineVisibleRect());
        }

        /// <summary>The arena's middle in world space, off its own corner cells.</summary>
        private Vector2 HazineBoardCentre(GameBoard board)
        {
            if (board == null)
            {
                return boardView.transform.position;
            }
            Vector2 low = boardView.CellToWorld(new GridPos(board.MinX, board.MinY));
            Vector2 high = boardView.CellToWorld(new GridPos(board.MinX + board.Width - 1,
                board.MinY + board.Height - 1));
            return boardView.transform.TransformPoint((low + high) * 0.5f);
        }

        private void EnsureHazine()
        {
            if (hazine == null)
            {
                var go = new GameObject("HazineReveal");
                go.transform.SetParent(transform, false);
                hazine = go.AddComponent<HazineRevealView>();
                hazine.Impulse = delegate(Vector2 offset)
                {
                    if (boardView != null)
                    {
                        boardView.SetImpulse(offset);
                    }
                };
            }
        }

        private void StopHazine()
        {
            if (hazine != null)
            {
                hazine.Stop();
            }
            if (boardView != null)
            {
                boardView.SetImpulse(Vector2.zero);
            }
        }

        /// <summary>
        /// When the cube on <paramref name="cell"/> breaks on screen, in seconds from now.
        ///
        /// A LINE breaks from its middle outward (LineSweepView: each cell answers as the front
        /// reaches it, distance times PropagationSeconds, measured over the same cells FlashLine
        /// hands the sweep). Anything else - a loose group, a TNT block, a quake - is the cluster
        /// burst's pressure, stress and fracture.
        /// </summary>
        private static float HazineBreakDelay(GameBoard board, IReadOnlyList<int> rows,
            IReadOnlyList<int> columns, GridPos cell)
        {
            if (board != null)
            {
                if (rows != null && Contains(rows, cell.Y - board.MinY))
                {
                    return LineArrival(board, cell, true);
                }
                if (columns != null && Contains(columns, cell.X - board.MinX))
                {
                    return LineArrival(board, cell, false);
                }
            }
            return ClusterBurstView.Style.PressureDuration + ClusterBurstView.Style.EdgeStressDuration
                + ClusterBurstView.Style.FractureDuration;
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

        private static float LineArrival(GameBoard board, GridPos cell, bool row)
        {
            int count = row ? board.Width : board.Height;
            float first = float.MaxValue;
            float last = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                GridPos pos = row ? new GridPos(board.MinX + i, cell.Y) : new GridPos(cell.X, board.MinY + i);
                if (board.IsInside(pos))
                {
                    first = Mathf.Min(first, i);
                    last = Mathf.Max(last, i);
                }
            }
            float along = row ? cell.X - board.MinX : cell.Y - board.MinY;
            float half = (last - first) * 0.5f;
            float distance = half > 0f ? Mathf.Clamp01(Mathf.Abs(along - (first + half)) / half) : 0f;
            return distance * LineSweepView.Style.PropagationSeconds
                + LineSweepView.Style.CellReactionSeconds * 0.35f;
        }

        /// <summary>
        /// WHERE THE FIND WENT - only ever a thing that is really on screen. The score for the one
        /// reward that is score; the panel of the power the rules named; the card they froze or
        /// minted; the discard pile a discarded hand went to; the joker's own panel for a discount
        /// that will only be spent in the next market. A find with nowhere real to go keeps its
        /// verdict where it was found and sends nothing.
        /// </summary>
        private HazineRevealView.Destination HazineDestination(HazineVisuals find, int jokerId)
        {
            var none = new HazineRevealView.Destination();
            switch (find.Effect)
            {
                case HazineEffect.ExplosionBonus:
                    return new HazineRevealView.Destination
                    {
                        Has = true,
                        Point = ScoreWorldAnchor(),
                        IsScore = true
                    };
                case HazineEffect.MarketDiscount:
                    return PanelDestination(jokerBar != null ? JokerPanelWorld(jokerId) : null,
                        delegate { jokerBar.PulseJoker(jokerId); });
                case HazineEffect.PowerRefilled:
                case HazineEffect.PowerDrained:
                    return PanelDestination(powerBar != null ? PowerPanelWorld(find.PowerId) : null,
                        delegate { powerBar.PulsePower(find.PowerId); });
                case HazineEffect.CardFrozen:
                case HazineEffect.BonusCard:
                {
                    CardVisual card = cardLayer != null ? cardLayer.Held(find.CardId) : null;
                    if (card == null)
                    {
                        return none;
                    }
                    return new HazineRevealView.Destination
                    {
                        Has = true,
                        Point = card.transform.position
                    };
                }
                case HazineEffect.HandDiscarded:
                    return new HazineRevealView.Destination
                    {
                        Has = true,
                        Point = CardLayerView.DiscardPilePos
                    };
            }
            return none;
        }

        private static HazineRevealView.Destination PanelDestination(Vector2? at, System.Action pulse)
        {
            if (!at.HasValue)
            {
                return new HazineRevealView.Destination();
            }
            return new HazineRevealView.Destination { Has = true, Point = at.Value, OnArrive = pulse };
        }

        private Vector2? JokerPanelWorld(int jokerId)
        {
            IReadOnlyList<Joker> owned = session != null ? session.Jokers.Jokers : null;
            if (owned == null || cam == null)
            {
                return null;
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].InstanceId == jokerId)
                {
                    Vector2? screen = jokerBar.PanelScreenCenter(i);
                    return screen.HasValue ? (Vector2?)ScreenToWorld(screen.Value) : null;
                }
            }
            return null;
        }

        private Vector2? PowerPanelWorld(int powerId)
        {
            IReadOnlyList<Power> owned = session != null ? session.Powers.Powers : null;
            if (owned == null || cam == null || powerId < 0)
            {
                return null;
            }
            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i].InstanceId == powerId)
                {
                    Vector2? screen = powerBar.PanelScreenCenter(i);
                    return screen.HasValue ? (Vector2?)ScreenToWorld(screen.Value) : null;
                }
            }
            return null;
        }

        private Vector2 ScreenToWorld(Vector2 screen)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
                Mathf.Abs(cam.transform.position.z)));
            return new Vector2(world.x, world.y);
        }

        /// <summary>The camera's rect less a margin, so a verdict over the top row stays on
        /// screen.</summary>
        private Rect HazineVisibleRect()
        {
            if (cam == null)
            {
                return new Rect();
            }
            const float margin = 0.5f;
            Vector3 eye = cam.transform.position;
            float halfY = cam.orthographicSize - margin;
            float halfX = cam.orthographicSize * cam.aspect - margin;
            return new Rect(eye.x - halfX, eye.y - halfY, halfX * 2f, halfY * 2f);
        }
    }
}
