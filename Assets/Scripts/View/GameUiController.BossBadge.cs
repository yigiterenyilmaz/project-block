// PURPOSE: The BOSS BADGE - the top-right marker that says a boss is on this stage, and the
// handle you hover to find out which one.
//
// WHY A BADGE AND NOT A LINE OF TEXT. The boss already had a home: the run readout in the top
// left, which spelled out "[BOSS: Saatçi - 3 turns left]" and then its whole rules line under
// it. That readout is a narrow debug column - it is the last place a player looks, it is the
// first thing that gets long, and the boss is the single most important fact about the round it
// is on. So the boss moves to a mark of its own, in the corner the eye already goes to for
// jokers, and the RULES TEXT moves onto its hover, where there is room for it.
//
// IT IS DELIBERATELY SMALL. One square: a warning diamond and the word BOSS, no name, no
// numbers. Everything else - which boss, what it does, what it is up to right now (StatusText,
// live) - is one hover away. A badge that tried to fit the name would be a panel, and the strip
// under it already belongs to the jokers.
//
// The joker bar is anchored to the SAME corner, so the badge pushes it down by its own height
// while it is up (JokerBarView.SetTopOffset) rather than drawing over the first joker.

using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>Square, and as tall as a joker panel so the corner column keeps its rhythm.</summary>
        private const float BossBadgeSize = 92f;

        /// <summary>Matches the joker bar's own inset from the corner and its gap between
        /// panels - the badge is the first entry in that column, not a separate widget.</summary>
        private const float BossBadgeMargin = 16f;

        private const float BossBadgeGap = 8f;

        private static readonly Color BossBadgePlateColor = new Color(0.16f, 0.09f, 0.11f, 0.96f);
        private static readonly Color BossBadgeMarkColor = new Color(0.86f, 0.24f, 0.28f);
        private static readonly Color BossBadgeGlyphColor = new Color(1f, 0.94f, 0.90f);
        private static readonly Color BossBadgeLabelColor = new Color(1f, 0.62f, 0.58f);

        private RectTransform bossBadgeRoot;

        /// <summary>Creates the badge, hidden. Called once, with the HUD canvas.</summary>
        private void BuildBossBadge(Transform canvas)
        {
            var go = new GameObject("BossBadge");
            go.transform.SetParent(canvas, false);
            bossBadgeRoot = go.AddComponent<RectTransform>();
            bossBadgeRoot.anchorMin = new Vector2(1f, 1f);
            bossBadgeRoot.anchorMax = new Vector2(1f, 1f);
            bossBadgeRoot.pivot = new Vector2(1f, 1f);
            bossBadgeRoot.anchoredPosition = new Vector2(-BossBadgeMargin, -BossBadgeMargin);
            bossBadgeRoot.sizeDelta = new Vector2(BossBadgeSize, BossBadgeSize);

            var plate = go.AddComponent<Image>();
            plate.color = BossBadgePlateColor;
            plate.raycastTarget = false;

            // The warning diamond: a square turned 45 degrees. The glyph is NOT its child - a
            // child would turn with it and the "!" would lie on its side.
            var markGo = new GameObject("Mark");
            markGo.transform.SetParent(bossBadgeRoot, false);
            RectTransform mark = markGo.AddComponent<RectTransform>();
            mark.sizeDelta = new Vector2(46f, 46f);
            mark.anchoredPosition = new Vector2(0f, 14f);
            mark.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image markImage = markGo.AddComponent<Image>();
            markImage.color = BossBadgeMarkColor;
            markImage.raycastTarget = false;

            MakeBadgeLabel("Glyph", new Vector2(0f, 14f), new Vector2(BossBadgeSize, 40f),
                "!", 30, FontStyle.Bold, BossBadgeGlyphColor);
            MakeBadgeLabel("Label", new Vector2(0f, -30f), new Vector2(BossBadgeSize, 22f),
                Loc.Pick("BOSS", "PATRON"), 15, FontStyle.Bold, BossBadgeLabelColor);

            bossBadgeRoot.gameObject.SetActive(false);
        }

        /// <summary>A centred label on the badge. Its own helper rather than JokerBarView's,
        /// which anchors top-left and never wraps - everything here is centred on the square.</summary>
        private void MakeBadgeLabel(string name, Vector2 position, Vector2 size, string content,
            int fontSize, FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bossBadgeRoot, false);
            Text text = go.AddComponent<Text>();
            text.font = ViewUtil.UiFontFor(style);
            text.fontSize = fontSize;
            text.color = color;
            text.text = content;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>Shows the badge for a boss round and hides it everywhere else, pushing the
        /// joker bar down out of its way while it is up. Called from every HUD refresh, so the
        /// badge appears and goes with the stage without anything having to remember it.</summary>
        private void RefreshBossBadge()
        {
            if (bossBadgeRoot == null)
            {
                return;
            }
            bool show = ActiveBoss() != null;
            if (show)
            {
                // Re-picked rather than cached: [L] switches language in the middle of a run,
                // and the badge carries one of the two words it has to switch.
                bossBadgeRoot.Find("Label").GetComponent<Text>().text =
                    Loc.Pick("BOSS", "PATRON");
            }
            if (bossBadgeRoot.gameObject.activeSelf != show)
            {
                bossBadgeRoot.gameObject.SetActive(show);
            }
            jokerBar.SetTopOffset(show ? BossBadgeSize + BossBadgeGap : 0f);
        }

        /// <summary>The boss the player is fighting RIGHT NOW, or null. A boss belongs to the
        /// round it is on, so the badge goes out in the market that follows it - the stage is
        /// over, and a marker for a fight that is finished is a lie.</summary>
        private BossRound ActiveBoss()
        {
            if (session == null || session.Phase != GamePhase.Round)
            {
                return null;
            }
            RoundEngine round = session.CurrentRound;
            return round != null && round.Config.IsBossRound ? round.Boss : null;
        }

        /// <summary>Hides the badge with the rest of the run's presentation while a menu owns
        /// the frame, and puts the joker bar back where it belongs on the way out - a bar left
        /// pushed down by a badge that is no longer on screen is a gap nothing explains.</summary>
        private void SetBossBadgeVisible(bool visible)
        {
            if (bossBadgeRoot == null)
            {
                return;
            }
            bossBadgeRoot.gameObject.SetActive(visible && ActiveBoss() != null);
            jokerBar.SetTopOffset(
                bossBadgeRoot.gameObject.activeSelf ? BossBadgeSize + BossBadgeGap : 0f);
        }

        /// <summary>Is the pointer on the badge? Screen space, like the joker and power bars -
        /// this is Canvas UI, not a world sprite.</summary>
        private bool BossBadgeAt(Vector2 screenPos)
        {
            return bossBadgeRoot != null && bossBadgeRoot.gameObject.activeSelf
                && RectTransformUtility.RectangleContainsScreenPoint(
                    bossBadgeRoot, screenPos, null);
        }
    }
}
