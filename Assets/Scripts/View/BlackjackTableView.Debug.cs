// PURPOSE: BlackjackTableView's DEBUG VIEWS - the brief's sixteen switches (BlackjackFx.Debug),
// drawn as thin outlines, crosses and readouts over the table so "is the group centred", "where
// does the house's card fly" and "what does the bet field hold" are a glance, not an argument.
// Off in the game; the lab turns them on. Pooled - a frame with nothing on draws nothing.

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private readonly List<SpriteRenderer> debugMarks = new List<SpriteRenderer>();
        private TextMesh debugText;
        private int debugUsed;

        /// <summary>Extra readouts the controller hands in (the audio duck, the hand's result).</summary>
        public string DebugExtra;

        private void TickDebug()
        {
            debugUsed = 0;
            if (BlackjackFx.Debug.Any)
            {
                DrawDebug();
            }
            for (int i = debugUsed; i < debugMarks.Count; i++)
            {
                debugMarks[i].enabled = false;
            }
            if (debugText != null)
            {
                debugText.gameObject.SetActive(BlackjackFx.Debug.Any);
            }
        }

        private void DrawDebug()
        {
            Color layout = new Color(0.3f, 0.9f, 1f, 0.8f);
            Color anchor = new Color(1f, 0.85f, 0.2f, 0.9f);
            if (BlackjackFx.Debug.ShowBlackjackLayoutBounds)
            {
                Outline(L.Region.center, L.Region.size, layout);
                Outline(L.Player, Vector2.one * L.BoardSize, layout);
                Outline(L.Dealer, Vector2.one * L.BoardSize, layout);
            }
            if (BlackjackFx.Debug.ShowArenaGroupCenter)
            {
                Cross(L.GroupCenter, 0.5f, new Color(1f, 0.3f, 0.9f, 0.9f));
                Mark(new Vector2(0f, L.GroupCenter.y), new Vector2(0.01f, 9f), new Color(1f, 0.3f, 0.9f, 0.35f));
            }
            if (BlackjackFx.Debug.ShowPlayerBoardCenter)
            {
                Cross(L.Player, 0.35f, BlackjackShapes.PlayerInk);
            }
            if (BlackjackFx.Debug.ShowDealerBoardCenter)
            {
                Cross(L.Dealer, 0.35f, BlackjackShapes.DealerInk);
            }
            if (BlackjackFx.Debug.ShowDealerHandSlots)
            {
                int n = Mathf.Max(1, DealerCardCount);
                for (int i = 0; i < n; i++)
                {
                    float rot;
                    Vector2 p = SlotPos(i, n, out rot);
                    Outline(p, new Vector2(CardW, CardH), layout);
                }
                Cross(L.DealerEdge, 0.25f, layout);
            }
            if (BlackjackFx.Debug.ShowDealerChosenCard && chosenSlot >= 0)
            {
                float rot;
                Vector2 p = SlotPos(chosenSlot, Mathf.Max(1, DealerCardCount + 1), out rot);
                Outline(p, new Vector2(CardW * 1.15f, CardH * 1.15f), new Color(1f, 0.4f, 0.2f, 1f));
            }
            if (BlackjackFx.Debug.ShowDealerCardTravelPath && debugFlightOn)
            {
                for (int i = 0; i <= 16; i++)
                {
                    Mark(BlackjackFx.Bezier(debugFlightFrom, debugFlightCtrl, debugFlightTo, i / 16f), Vector2.one * 0.05f, anchor);
                }
                Cross(debugFlightCtrl, 0.15f, new Color(1f, 0.5f, 0.2f, 0.7f));
            }
            if (BlackjackFx.Debug.ShowPlayerScoreAnchor)
            {
                Cross(ScoreAnchor(Side.Player), 0.2f, anchor);
            }
            if (BlackjackFx.Debug.ShowDealerScoreAnchor)
            {
                Cross(ScoreAnchor(Side.Dealer), 0.2f, anchor);
            }
            if (BlackjackFx.Debug.ShowBankAnchor)
            {
                Cross(BankAnchor, 0.25f, anchor);
            }
            if (BlackjackFx.Debug.ShowWagerAnchor)
            {
                Cross(WagerAnchor, 0.25f, anchor);
                Cross(L.DealerIntake, 0.18f, new Color(0.8f, 0.2f, 0.3f, 1f));
            }
            if (BlackjackFx.Debug.ShowTargetAnchor)
            {
                Cross(L.Ledger + new Vector2(L.LedgerHalfWidth, 0f), 0.22f, anchor);
            }

            var sb = new StringBuilder();
            if (BlackjackFx.Debug.ShowBetInputState)
            {
                sb.Append("BET  ").Append(BetInputReadout).Append('\n');
            }
            if (BlackjackFx.Debug.ShowBackgroundMood)
            {
                sb.Append("MOOD ").Append(MoodReadout).Append('\n');
            }
            if ((BlackjackFx.Debug.ShowHandResult || BlackjackFx.Debug.ShowAudioDuck) && !string.IsNullOrEmpty(DebugExtra))
            {
                sb.Append(DebugExtra).Append('\n');
            }
            if (BlackjackFx.Debug.ShowBlackjackLayoutBounds)
            {
                sb.Append("LAYOUT ").Append(L.Stacked ? "stacked" : "side by side").Append("  board ").Append(L.BoardSize.ToString("0.00"))
                    .Append("  gap ").Append(L.Gap.ToString("0.00")).Append("  group ").Append(L.GroupCenter.ToString("0.00")).Append('\n');
            }
            if (debugText == null)
            {
                debugText = ViewUtil.MakeText3D(transform, "BlackjackDebug", Vector2.zero, "0", 90, 0.15f / 9f,
                    new Color(0.7f, 1f, 0.9f), DebugOrder, TextAnchor.LowerLeft);
            }
            UiLayout lay = UiLayout.Active;
            debugText.transform.localPosition = new Vector2(-lay.HalfWidth + 0.2f, -lay.OrthoSize + 0.2f);
            debugText.text = sb.ToString();
        }

        private void Mark(Vector2 at, Vector2 size, Color c)
        {
            SpriteRenderer r;
            if (debugUsed < debugMarks.Count)
            {
                r = debugMarks[debugUsed];
            }
            else
            {
                r = Sprite("DebugMark", ViewUtil.WhiteSprite, c, DebugOrder);
                debugMarks.Add(r);
            }
            debugUsed++;
            r.enabled = true;
            r.color = c;
            r.transform.localPosition = at;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void Outline(Vector2 c, Vector2 size, Color col)
        {
            float w = 0.025f;
            Mark(c + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, w), col);
            Mark(c - new Vector2(0f, size.y * 0.5f), new Vector2(size.x, w), col);
            Mark(c + new Vector2(size.x * 0.5f, 0f), new Vector2(w, size.y), col);
            Mark(c - new Vector2(size.x * 0.5f, 0f), new Vector2(w, size.y), col);
        }

        private void Cross(Vector2 c, float size, Color col)
        {
            Mark(c, new Vector2(size, 0.03f), col);
            Mark(c, new Vector2(0.03f, size), col);
        }
    }
}
