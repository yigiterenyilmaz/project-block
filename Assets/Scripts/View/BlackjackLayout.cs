// PURPOSE: WHERE THE BLACKJACK TABLE STANDS - one solver for the two arenas, the HUD over them,
// the wager between them and the house's closed hand, worked out from the screen rather than from
// written-down coordinates.
//
// THE GROUP IS CENTRED ON THE SCREEN, not on the room the bars leave. The joker bar on the right
// is narrower than the power column on the left, so the old "Öteki dünya" region was off centre,
// and two boards in it read as a table pushed to one side. Here the free room is measured on both
// sides, the SMALLER side is mirrored onto the larger, and the pair stands symmetric about x = 0:
// same size, same height, one controlled gap between them for the wager and the seam.
//
// SIDE BY SIDE or STACKED, whichever gives the bigger boards - on every screen this game ships on
// that is side by side (a phone's width beats its height once the bars and the hand are taken
// out), but the solver asks rather than assumes, and the lab shows it for other shapes.
//
// THE HUD HANGS OFF THE BOARDS' TOP EDGE, capped by the screen's: bank, target ledger, the hand's
// line, then the score duel - so a taller screen gives the boards the room and the HUD follows.
//
// Pure arithmetic over a Frame; nothing here touches a renderer.

using UnityEngine;

namespace ProjectBlock.View
{
    public struct BlackjackLayout
    {
        /// <summary>What the solver needs to know about a screen.</summary>
        public struct Frame
        {
            public float HalfWidth;
            public float HalfHeight;
            public bool Portrait;
            /// <summary>World units taken from the left / right edges by the bars.</summary>
            public float LeftReserve;
            public float RightReserve;
            /// <summary>World units taken from the top by bars that run across it (portrait).</summary>
            public float TopReserve;
            /// <summary>The highest point the hand (and the piles beside it) reach.</summary>
            public float HandTop;
            public Vector2 HandCenter;

            /// <summary>The screen in front of the player right now.</summary>
            public static Frame Active()
            {
                UiLayout layout = UiLayout.Active;
                var f = new Frame();
                f.HalfWidth = layout.HalfWidth;
                f.HalfHeight = layout.OrthoSize;
                f.Portrait = layout.IsPortrait;
                float px = layout.WorldPerCanvasPixel;
                if (layout.IsPortrait)
                {
                    f.LeftReserve = 0.2f;
                    f.RightReserve = 0.2f;
                    f.TopReserve = 3.05f;
                    // the piles stand over the hand's ends; their tops are the line
                    float pileTop = layout.DrawPile.y + (CardVisual.BodyHeight + 0.18f) * layout.PileScale * 0.5f;
                    f.HandTop = pileTop + 0.12f;
                }
                else
                {
                    f.LeftReserve = (layout.CornerInset + layout.PowerColumns * layout.PowerPanel.x
                        + (layout.PowerColumns - 1) * layout.PowerGap + 14f) * px;
                    f.RightReserve = (layout.JokerColumns * layout.JokerPanel.x
                        + (layout.JokerColumns - 1) * layout.JokerGap + layout.CornerInset + 14f) * px;
                    f.TopReserve = 0f;
                    f.HandTop = layout.HandCenter.y + CardVisual.BodyHeight * layout.CardScale * 0.5f + 0.22f;
                }
                f.HandCenter = layout.HandCenter;
                return f;
            }

            /// <summary>A desktop screen of another aspect (the lab's previews).</summary>
            public static Frame Desktop(float aspect)
            {
                var f = new Frame();
                f.HalfHeight = 5f;
                f.HalfWidth = 5f * aspect;
                float px = f.HalfWidth * 2f / 1920f;
                f.LeftReserve = (16f + 160f + 14f) * px;
                f.RightReserve = (2f * 160f + 10f + 16f + 14f) * px;
                f.HandTop = -4.05f + 0.9f + 0.22f;
                f.HandCenter = new Vector2(0f, -4.05f);
                return f;
            }

            /// <summary>A phone held upright (the lab's preview), solved the way UiLayout solves it.</summary>
            public static Frame Phone(float aspect)
            {
                var f = new Frame();
                f.Portrait = true;
                f.HalfWidth = 6.5f * 0.5f / 0.88f;
                f.HalfHeight = f.HalfWidth / aspect;
                f.LeftReserve = f.RightReserve = 0.2f;
                f.TopReserve = 3.05f;
                float handCenter = -f.HalfHeight + 0.62f + CardVisual.BodyHeight * 0.92f * 0.5f;
                f.HandCenter = new Vector2(0f, handCenter);
                f.HandTop = handCenter + CardVisual.BodyHeight * 0.92f * 0.5f + 0.34f
                    + (CardVisual.BodyHeight + 0.18f) * 0.52f + 0.12f;
                return f;
            }
        }

        public bool Stacked;
        public float BoardSize;
        public float Gap;
        public Vector2 Player;
        public Vector2 Dealer;
        public Vector2 GroupCenter;
        /// <summary>The box the group was fitted into (debug).</summary>
        public Rect Region;

        public Vector2 Bank;
        public Vector2 Ledger;
        public float LedgerHalfWidth;
        public Vector2 MiniLine;
        public Vector2 PlayerScore;
        public Vector2 DealerScore;
        public Vector2 Vs;
        public Vector2 Diff;
        public Vector2 Wager;
        public bool WagerBetween;
        public Vector2 PlayerLabel;
        public Vector2 DealerLabel;
        public Vector2 DealerHand;
        public float DealerCardScale;
        public Vector2 DealerEdge;
        public Vector2 DealerIntake;
        public Vector2 Message;
        public Vector2 PlayerHand;
        /// <summary>One unit of HUD type: everything on the table is sized off it.</summary>
        public float Type;

        public float BoardTop
        {
            get { return Mathf.Max(Player.y, Dealer.y) + BoardSize * 0.5f; }
        }

        public float BoardBottom
        {
            get { return Mathf.Min(Player.y, Dealer.y) - BoardSize * 0.5f; }
        }

        public static BlackjackLayout Solve(Frame f)
        {
            var l = new BlackjackLayout();
            float reserve = Mathf.Max(f.LeftReserve, f.RightReserve);
            float halfFree = f.HalfWidth - reserve;
            float hud = BlackjackFx.Tuning.blackjackHudTopSpacing * (f.Portrait ? 0.86f : 1f);
            float labelBand = f.Portrait ? 0.38f : 0.44f;
            float top = f.HalfHeight - f.TopReserve - hud;
            float bottom = f.HandTop;
            float gap = f.Portrait ? BlackjackFx.Tuning.blackjackArenaGapCompact : BlackjackFx.Tuning.blackjackArenaGap;
            float height = top - bottom;

            float bySide = Mathf.Min((halfFree * 2f - gap) * 0.5f, height - labelBand);
            float byStack = Mathf.Min(halfFree * 2f * 0.92f, (height - gap - labelBand * 2f) * 0.5f);
            l.Stacked = byStack > bySide * 1.04f;
            l.BoardSize = Mathf.Max(1f, (l.Stacked ? byStack : bySide) * BlackjackFx.Tuning.blackjackArenaScale);
            l.Gap = gap;
            l.Region = new Rect(-halfFree, bottom, halfFree * 2f, height);

            float s = l.BoardSize;
            float y0 = (top - labelBand + bottom) * 0.5f + BlackjackFx.Tuning.blackjackGroupVerticalOffset;
            if (l.Stacked)
            {
                // the house above, the player nearer their hand
                l.Dealer = new Vector2(0f, y0 + (s + gap + labelBand) * 0.5f);
                l.Player = new Vector2(0f, y0 - (s + gap + labelBand) * 0.5f + labelBand);
            }
            else
            {
                float x = (s + gap) * 0.5f;
                l.Player = new Vector2(-x, y0);
                l.Dealer = new Vector2(x, y0);
            }
            l.GroupCenter = (l.Player + l.Dealer) * 0.5f;
            l.Type = Mathf.Clamp(s / 4.9f, 0.62f, 1.15f);

            // ---- the HUD over the group, capped by the screen's own top
            float boardTop = l.BoardTop;
            float capTop = f.HalfHeight - f.TopReserve - 0.08f;
            float t = l.Type;
            float bankY = Mathf.Min(boardTop + hud * 0.80f, capTop - 0.34f * t);
            l.Bank = new Vector2(0f, bankY);
            l.Ledger = new Vector2(0f, bankY - 0.50f * t);
            l.LedgerHalfWidth = Mathf.Min(2.3f * t, halfFree * 0.6f);
            l.MiniLine = new Vector2(0f, bankY - 0.82f * t);
            float scoreY = l.Stacked ? l.Dealer.y - s * 0.5f - gap * 0.5f : bankY - 1.22f * t;
            float scoreX = l.Stacked ? 0.45f * t : 0.42f * t;
            l.PlayerScore = new Vector2(-scoreX, scoreY);
            l.DealerScore = new Vector2(scoreX, scoreY);
            l.Vs = new Vector2(0f, scoreY);
            l.Diff = new Vector2(0f, scoreY - 0.42f * t);

            l.WagerBetween = !l.Stacked && gap >= 1.2f;
            l.Wager = l.WagerBetween
                ? new Vector2(0f, l.GroupCenter.y - 0.2f * t)
                : l.Stacked
                    ? new Vector2(-s * 0.5f - 0.25f, l.GroupCenter.y)
                    : new Vector2(0f, l.BoardBottom - 0.42f * t);

            l.PlayerLabel = l.Player + new Vector2(0f, s * 0.5f + 0.22f * t);
            l.DealerLabel = l.Dealer + new Vector2(0f, s * 0.5f + 0.22f * t);

            l.DealerCardScale = 0.36f * t;
            float cardW = CardVisual.BodyWidth * l.DealerCardScale;
            float cardH = CardVisual.BodyHeight * l.DealerCardScale;
            l.DealerHand = new Vector2(l.Dealer.x + s * 0.5f - cardW * 1.6f,
                l.Dealer.y + s * 0.5f + BlackjackFx.Tuning.blackjackDealerHandOffset * t + cardH * 0.08f);
            // the house's name never sits under its own cards
            float fanLeft = l.DealerHand.x - cardW * 1.7f;
            if (l.DealerLabel.x + 0.45f * t > fanLeft - 0.1f)
            {
                l.DealerLabel.x = fanLeft - 0.55f * t;
            }
            // where the house's refills come from, and where a lost stake is taken
            l.DealerEdge = l.Stacked
                ? new Vector2(f.HalfWidth + 0.8f, l.DealerHand.y)
                : new Vector2(l.Dealer.x + s * 0.5f + 1.2f, l.DealerHand.y + 0.4f);
            l.DealerIntake = new Vector2(l.Dealer.x + s * 0.18f, l.Dealer.y + s * 0.5f + 0.1f);

            l.Message = new Vector2(0f, Mathf.Max(l.BoardBottom - 0.38f * t - (l.WagerBetween ? 0f : 0.55f * t),
                f.HandTop + 0.12f));
            float handShift = f.Portrait ? 0f : l.Player.x * BlackjackFx.Tuning.blackjackPlayerHandOffset;
            l.PlayerHand = new Vector2(f.HandCenter.x + (l.Stacked ? 0f : handShift), f.HandCenter.y);
            return l;
        }
    }
}
