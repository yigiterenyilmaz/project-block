// PURPOSE: "Kara Delik"'s presentation, wired up - the one place BlackHoleView is reached from, by the
// game and by the animation lab alike.
//
// TWO HALVES, ON TWO MOMENTS, as for the pickaxe: the repaint after a turn has already emptied what
// the holes ate, moved what they pulled and wiped what the collapse took, so that repaint raises the
// proxies and holds the pull destinations (SyncBlackHole -> Prepare); the gravity itself plays from
// PlayExplosionFeedback, after the turn's own lines have gone (PlayBlackHole -> Begin), and the devour
// after the gravity. The mass the hole shows, the piles it has voided this round and the refusals it
// answered are all read from Core on every repaint; nothing here counts or decides anything.
//
// A collapse is THE joker's payoff and gets its own language: the ordinary sweep wave and its shake
// are not played for it (HoleCollapseThisTurn), the popup and the sound still are.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private static readonly Color DiscardProxyEdge = new Color(0.24f, 0.22f, 0.20f);
        private static readonly Color DiscardProxyFace = new Color(0.88f, 0.86f, 0.80f);
        private static readonly Color DrawProxyInner = new Color(0.15f, 0.19f, 0.31f);

        private bool blackHoleWired;
        private RoundEngine voidedRound;
        private readonly HashSet<bool> voidedPiles = new HashSet<bool>();
        private DeckSwallowVisuals lastDevourSeen;
        private BlackHoleVisuals collapseTurn;
        private int devourTopCardId = -1;

        private KaraDelikJoker FindBlackHoleJoker()
        {
            if (session == null || session.Jokers == null)
            {
                return null;
            }
            IReadOnlyList<Joker> owned = session.Jokers.Jokers;
            for (int i = 0; i < owned.Count; i++)
            {
                var found = owned[i] as KaraDelikJoker;
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private BlackHoleView EnsureBlackHole()
        {
            BlackHoleView view = boardView.BlackHoles;
            if (!blackHoleWired)
            {
                blackHoleWired = true;
                view.Pixel = delegate
                {
                    return cam != null ? cam.orthographicSize * 2f / Mathf.Max(1, Screen.height) : 0.01f;
                };
                view.Face = delegate(GridPos cell, Cube cube, out Sprite tile, out Color colour)
                {
                    if (!boardView.TryCubeLook(cell, 2f, out tile, out colour))
                    {
                        boardView.CubeFace(cube, out tile, out colour);
                    }
                    return tile != null;
                };
            }
            return view;
        }

        /// <summary>The repaint's half: the mass, the proxies, the voided piles.</summary>
        private void SyncBlackHole(RoundEngine round)
        {
            if (boardView == null || round == null || !BlackHoleView.Available)
            {
                return;
            }
            KaraDelikJoker joker = FindBlackHoleJoker();
            bool anyHole = round.MainBoard.CellsOfKind(CubeKind.Void).Count > 0;
            if (joker == null && !anyHole && !boardView.HasBlackHoles)
            {
                return;
            }
            BlackHoleView view = EnsureBlackHole();
            view.SetMass(joker != null ? joker.SwallowedThisRound : 0, KaraDelikJoker.GoalFor(round.MainBoard));

            if (!ReferenceEquals(voidedRound, round))
            {
                voidedRound = round;
                voidedPiles.Clear();
            }
            if (joker != null)
            {
                if (joker.LastTurn != null)
                {
                    if (joker.LastTurn.Collapsed && !ReferenceEquals(collapseTurn, joker.LastTurn))
                    {
                        collapseTurn = joker.LastTurn;
                        holeCollapsePending = true;
                    }
                    view.Prepare(joker.LastTurn);
                }
                if (joker.LastDeckSwallow != null && !ReferenceEquals(lastDevourSeen, joker.LastDeckSwallow))
                {
                    lastDevourSeen = joker.LastDeckSwallow;
                    voidedPiles.Add(joker.LastDeckSwallow.FromDrawPile);
                    devourPending = joker.LastDeckSwallow;
                    IReadOnlyList<int> ids = joker.LastDeckSwallow.CardIds;
                    devourTopCardId = ids.Count > 0 ? ids[0] : -1;
                }
            }
            float scale = UiLayout.Active.PileScale;
            view.SetPileResidue(true, voidedPiles.Contains(true) && round.Deck.DrawCount == 0,
                CardLayerView.DrawPilePos, scale);
            view.SetPileResidue(false, voidedPiles.Contains(false) && round.Deck.DiscardCount == 0,
                CardLayerView.DiscardPilePos, scale);
        }

        private DeckSwallowVisuals devourPending;

        /// <summary>True for the one feedback pass whose sweep is the hole's collapse.</summary>
        private bool holeCollapsePending;

        private bool TakeHoleCollapse(TurnReport report)
        {
            bool collapse = holeCollapsePending && report != null && report.CleanSweep;
            holeCollapsePending = false;
            return collapse;
        }

        /// <summary>The turn's half: after the lines, the gravity; after the gravity, the devour.</summary>
        private void PlayBlackHole(TurnReport report)
        {
            if (boardView == null || !BlackHoleView.Available)
            {
                return;
            }
            KaraDelikJoker joker = FindBlackHoleJoker();
            float delay = BlackHoleDelay(report);
            float gravity = 0f;
            if (joker != null && joker.LastTurn != null)
            {
                BlackHoleView view = EnsureBlackHole();
                view.Begin(joker.LastTurn, delay);
                gravity = GravityLength(joker.LastTurn);
            }
            PlayDevour(delay + gravity);
        }

        private void PlayDevour(float delay)
        {
            if (devourPending == null)
            {
                return;
            }
            DeckSwallowVisuals ev = devourPending;
            devourPending = null;
            Color edge = ev.FromDrawPile ? CardVisual.FrameColorFor(devourTopCardId) : DiscardProxyEdge;
            Color inner = ev.FromDrawPile ? DrawProxyInner : DiscardProxyFace;
            EnsureBlackHole().Devour(ev,
                ev.FromDrawPile ? CardLayerView.DrawPilePos : CardLayerView.DiscardPilePos,
                UiLayout.Active.PileScale, edge, inner, delay);
        }

        /// <summary>When the turn's own destruction has been seen: a line's beam, or a breath.</summary>
        private float BlackHoleDelay(TurnReport report)
        {
            if (report == null)
            {
                return 0.05f;
            }
            int lines = report.ExplodedRows.Count + report.ExplodedColumns.Count;
            if (lines == 0)
            {
                return 0.08f;
            }
            GameBoard board = boardView.Board;
            int half = board != null ? Mathf.Max(board.Width, board.Height) / 2 : 3;
            return LineSweepView.Style.PropagationSeconds * half + LineSweepView.Style.PeakHold
                + LineSweepView.Style.FadeSeconds * 0.5f;
        }

        private static float GravityLength(BlackHoleVisuals turn)
        {
            if (turn == null || turn.SwallowedThisTurn + turn.Pulls.Count == 0)
            {
                return 0.1f;
            }
            float length = BlackHoleView.Style.Grip + BlackHoleView.Style.SwallowSpan + BlackHoleView.Style.Swallow;
            if (turn.Collapsed)
            {
                length += BlackHoleView.Style.Saturation + BlackHoleView.Style.Silence
                    + BlackHoleView.Style.Wave + BlackHoleView.Style.Burst;
            }
            return length;
        }

        private void StopBlackHole()
        {
            if (boardView == null || !boardView.HasBlackHoles)
            {
                return;
            }
            boardView.BlackHoles.StopEvents();
            KaraDelikJoker joker = FindBlackHoleJoker();
            boardView.BlackHoles.MarkPlayed(joker != null ? joker.LastTurn : null,
                joker != null ? joker.LastDeckSwallow : null);
            devourPending = null;
            holeCollapsePending = false;
        }
    }
}
