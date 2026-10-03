// PURPOSE: BlackjackTableView's INTRO - the panel that opens the stage, in the middle of the
// screen (never a popup at the side): BLACKJACK, "Paranı ikiye katla.", the bank turning into the
// target before the player's eyes, the house's 500 advance when the bank is empty, and the stage's
// four rules as four small pictures - stake, outscore the house, a win pays double, reach the target.
//
// It is a table opening, not a slot machine starting: burgundy over deep green, one muted gold trim
// with a single glint running round it, a 0.92 -> 1.02 -> 1.00 arrival, a dimmed table behind. It
// waits for the player, and when it closes it folds DOWN onto the table, which is where the betting
// then begins. The numbers are Core's (the bank, the target, whether the house staked 500).

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private struct IntroItem
        {
            public SpriteRenderer Sprite;
            public TextMesh Text;
            public Color Ink;
            public float At;
        }

        private Transform introRoot;
        private SpriteRenderer introDim;
        private SpriteRenderer introGlint;
        private readonly List<IntroItem> introItems = new List<IntroItem>();
        private TextMesh introBank;
        private TextMesh introTarget;
        private TextMesh introStake;
        private TextMesh introFooter;
        private bool introBuilt;
        private bool introOpen;
        private float introT0;
        private float introAlpha;
        private float introScale = 1f;
        private float introFold = 1f;
        private float introGlintT0 = -10f;
        private Vector2 introSize;

        public bool IntroOpen
        {
            get { return introOpen; }
        }

        private void BuildIntro()
        {
            if (introBuilt)
            {
                return;
            }
            introBuilt = true;
            introRoot = new GameObject("Intro").transform;
            introRoot.SetParent(transform, false);
            introDim = Sprite("IntroDim", ViewUtil.WhiteSprite, new Color(0.01f, 0.012f, 0.014f), IntroOrder - 1);
            var rim = ViewUtil.MakeRounded(introRoot, "Rim", Vector2.zero, Vector2.one, BlackjackShapes.GoldDim, IntroOrder);
            var body = ViewUtil.MakeRounded(introRoot, "Body", Vector2.zero, Vector2.one, new Color(0.13f, 0.035f, 0.055f), IntroOrder + 1);
            var inner = ViewUtil.MakeRounded(introRoot, "Inner", Vector2.zero, Vector2.one, new Color(0.035f, 0.085f, 0.08f), IntroOrder + 3);
            var innerLine = ViewUtil.MakeRounded(introRoot, "InnerLine", Vector2.zero, Vector2.one, BlackjackShapes.GoldDim, IntroOrder + 2);
            introGlint = ViewUtil.MakeIcon(introRoot, "Glint", Vector2.zero, 1f, BlackjackShapes.Cream, IntroOrder + 4, BlackjackShapes.RadialSprite);
            AddIntro(rim, null, BlackjackShapes.GoldDim, 0f);
            AddIntro(body, null, new Color(0.13f, 0.035f, 0.055f), 0f);
            AddIntro(innerLine, null, new Color(BlackjackShapes.GoldDim.r, BlackjackShapes.GoldDim.g, BlackjackShapes.GoldDim.b, 0.35f), 0f);
            AddIntro(inner, null, new Color(0.035f, 0.085f, 0.08f), 0f);
        }

        private void AddIntro(SpriteRenderer sprite, TextMesh text, Color ink, float at)
        {
            introItems.Add(new IntroItem { Sprite = sprite, Text = text, Ink = ink, At = at });
        }

        private TextMesh IntroText(string name, string value, float height, Color ink, Vector2 at, float appear, bool bold)
        {
            TextMesh tm = Text(name, height, ink, IntroOrder + 8, TextAnchor.MiddleCenter, bold);
            tm.transform.SetParent(introRoot, false);
            tm.transform.localPosition = at;
            tm.text = value;
            AddIntro(null, tm, ink, appear);
            return tm;
        }

        private void PlaceIntro()
        {
            if (!introBuilt)
            {
                return;
            }
            UiLayout layout = UiLayout.Active;
            Size(introDim, Vector2.zero, new Vector2(layout.HalfWidth * 2f + 1f, layout.OrthoSize * 2f + 1f));
            introRoot.localPosition = new Vector2(0f, L.GroupCenter.y + 0.1f * L.Type);
        }

        public void HideIntro()
        {
            introOpen = false;
            if (introBuilt)
            {
                introRoot.gameObject.SetActive(false);
                Alpha(introDim, 0f);
            }
        }

        /// <summary>
        /// Opens the intro and holds it until <paramref name="pressed"/> answers true, then folds
        /// it onto the table. <paramref name="houseStake"/>: the bank was empty and the house put
        /// its 500 in - the bank is shown arriving from nothing.
        /// </summary>
        public IEnumerator PlayIntro(long bank, long targetValue, bool houseStake, long stake, Func<bool> pressed)
        {
            BuildIntro();
            // the panel's content is rebuilt for these numbers
            for (int i = introItems.Count - 1; i >= 4; i--)
            {
                if (introItems[i].Text != null) Destroy(introItems[i].Text.gameObject);
                if (introItems[i].Sprite != null) Destroy(introItems[i].Sprite.gameObject);
                introItems.RemoveAt(i);
            }
            float t = Mathf.Max(0.7f, L.Type);
            introSize = new Vector2(Mathf.Min(7.6f * t, UiLayout.Active.HalfWidth * 2f - 0.3f), 5.0f * t);
            float sx = introSize.x / (7.6f * t);
            Vector2 size = introSize;
            for (int i = 0; i < 4; i++)
            {
                SpriteRenderer r = introItems[i].Sprite;
                Vector2 inset = i == 0 ? new Vector2(0.06f, 0.06f) * t : i == 1 ? Vector2.zero
                    : i == 2 ? new Vector2(-0.22f, -0.22f) * t : new Vector2(-0.26f, -0.26f) * t;
                r.size = size + inset;
            }
            float y = size.y * 0.5f;
            IntroText("Title", "BLACKJACK", 0.64f * t, BlackjackShapes.Gold, new Vector2(0f, y - 0.72f * t), 0.10f, true);
            IntroText("Sub", Loc.Pick("Double your money.", "Paranı ikiye katla."), 0.27f * t, BlackjackShapes.Cream, new Vector2(0f, y - 1.18f * t), 0.19f, false);

            // ---- bank -> target
            float rowY = y - 1.95f * t;
            float side = 1.55f * t * sx;
            IntroText("BankCap", Loc.Pick("BANK", "KASAN"), 0.17f * t, CaptionInk, new Vector2(-side, rowY + 0.34f * t), 0.28f, false);
            introBank = IntroText("Bank", houseStake ? "0" : bank.ToString(), 0.46f * t, BankInk, new Vector2(-side, rowY), 0.28f, true);
            IntroText("TargetCap", Loc.Pick("TARGET", "HEDEF"), 0.17f * t, CaptionInk, new Vector2(side, rowY + 0.34f * t), 0.46f, false);
            introTarget = IntroText("Target", "", 0.46f * t, BlackjackShapes.Gold, new Vector2(side, rowY), 0.46f, true);
            var line = Sprite("IntroArrowLine", ViewUtil.WhiteSprite, BlackjackShapes.GoldDim, IntroOrder + 5);
            line.transform.SetParent(introRoot, false);
            Size(line, new Vector2(0f, rowY), new Vector2(side * 0.9f, 0.018f * t));
            line.transform.localPosition = new Vector3(0f, rowY, 0f);
            AddIntro(line, null, BlackjackShapes.GoldDim, 0.4f);
            var head = Sprite("IntroArrowHead", BlackjackShapes.TriangleSprite, BlackjackShapes.Gold, IntroOrder + 5);
            head.transform.SetParent(introRoot, false);
            head.transform.localPosition = new Vector3(side * 0.45f + 0.04f * t, rowY, 0f);
            head.transform.localScale = new Vector3(0.16f * t, 0.16f * t, 1f);
            AddIntro(head, null, BlackjackShapes.Gold, 0.42f);
            IntroText("Double", "×2", 0.2f * t, BlackjackShapes.Gold, new Vector2(0f, rowY + 0.2f * t), 0.42f, true);
            introStake = IntroText("Stake", Loc.Pick("HOUSE ADVANCE  +", "KASA AVANSI  +") + stake, 0.19f * t, BlackjackShapes.Gold,
                new Vector2(-side, rowY - 0.4f * t), houseStake ? 0.34f : 99f, false);

            // ---- the four rules, as pictures
            string[] captions =
            {
                Loc.Pick("Place a bet", "Bahis koy"),
                Loc.Pick("Outscore\nthe house", "Kasadan yüksek\nskor yap"),
                Loc.Pick("Win and\nget 2×", "Kazanırsan\n2× al"),
                Loc.Pick("Reach\nthe target", "Paranı hedefe\nçıkar")
            };
            float stepY = -size.y * 0.5f + 1.25f * t;
            float span = size.x * 0.76f;
            for (int i = 0; i < 4; i++)
            {
                float x = -span * 0.5f + span * i / 3f;
                float at = 0.62f + i * BlackjackFx.Tuning.introStepStagger;
                var disc = ViewUtil.MakeRounded(introRoot, "StepRim" + i, new Vector2(x, stepY + 0.18f * t), new Vector2(0.62f, 0.62f) * t, BlackjackShapes.GoldDim, IntroOrder + 5);
                AddIntro(disc, null, new Color(BlackjackShapes.GoldDim.r, BlackjackShapes.GoldDim.g, BlackjackShapes.GoldDim.b, 0.8f), at);
                var discIn = ViewUtil.MakeRounded(introRoot, "Step" + i, new Vector2(x, stepY + 0.18f * t), new Vector2(0.56f, 0.56f) * t, new Color(0.06f, 0.05f, 0.05f), IntroOrder + 6);
                AddIntro(discIn, null, new Color(0.06f, 0.05f, 0.05f), at);
                Vector2 c = new Vector2(x, stepY + 0.18f * t);
                if (i == 0)
                {
                    var icon = ViewUtil.MakeIcon(introRoot, "StepIcon" + i, c, 0.4f * t, Color.white, IntroOrder + 7, BlackjackShapes.ChipSprite);
                    AddIntro(icon, null, Color.white, at);
                }
                else if (i == 3)
                {
                    var icon = ViewUtil.MakeIcon(introRoot, "StepIcon" + i, c, 0.4f * t, BlackjackShapes.Gold, IntroOrder + 7, BlackjackShapes.TargetSprite);
                    AddIntro(icon, null, BlackjackShapes.Gold, at);
                }
                else
                {
                    IntroText("StepIcon" + i, i == 1 ? "VS" : "2×", 0.24f * t, i == 1 ? BlackjackShapes.Cream : BlackjackShapes.Gold, c, at, true);
                }
                TextMesh cap = IntroText("StepCap" + i, captions[i], 0.15f * t, MessageInk, new Vector2(x, stepY - 0.42f * t), at + 0.04f, false);
                cap.lineSpacing = 0.9f;
            }
            introFooter = IntroText("Footer", Loc.Pick("click to continue", "devam etmek için tıkla"), 0.14f * t, MiniInk,
                new Vector2(0f, -size.y * 0.5f + 0.3f * t), 1.1f, false);

            // ---- open
            PlaceIntro();
            introRoot.gameObject.SetActive(true);
            introOpen = true;
            introT0 = Time.time;
            introFold = 1f;
            introGlintT0 = Time.time + T(0.2f);
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.IntroOpen);
            }
            float open = T(BlackjackFx.Tuning.introOpen);
            while (Time.time - introT0 < open)
            {
                float k = (Time.time - introT0) / open;
                introAlpha = BlackjackFx.EaseOut(k);
                introScale = k < 0.65f ? Mathf.Lerp(0.92f, 1.02f, BlackjackFx.EaseOut(k / 0.65f)) : Mathf.Lerp(1.02f, 1f, (k - 0.65f) / 0.35f);
                yield return null;
            }
            introAlpha = 1f;
            introScale = 1f;

            // ---- the house's advance: a token from the house's side into the bank, the bank counting up
            if (houseStake)
            {
                yield return Wait(T(0.12f));
                Vector2 bankWorld = (Vector2)introRoot.localPosition + new Vector2(-side, rowY);
                Vector2 from = (Vector2)introRoot.localPosition + new Vector2(size.x * 0.5f, rowY - 0.4f * t);
                if (Sfx != null)
                {
                    Sfx.Casino(BlackjackCue.HouseStake);
                }
                StartCoroutine(FlyTokens(from, bankWorld, 3, T(BlackjackFx.Tuning.houseStakeDuration), BlackjackCue.ChipTap));
                yield return CountText(introBank, 0, bank, T(BlackjackFx.Tuning.houseStakeDuration + 0.1f), BlackjackCue.BankTick);
            }
            // ---- the target, a breath after the bank
            yield return Wait(T(0.12f));
            yield return CountText(introTarget, houseStake ? bank : 0, targetValue, T(0.32f), BlackjackCue.BankTick);

            // ---- hold for the player (never less than a moment, so a stray click cannot skip it)
            float minimum = Time.time + T(0.45f);
            while (Time.time < minimum || pressed == null || !pressed())
            {
                yield return null;
            }
            // ---- fold onto the table
            if (Sfx != null)
            {
                Sfx.Casino(BlackjackCue.TableTap, 0.9f, 0.5f);
            }
            float close = T(BlackjackFx.Tuning.introClose);
            float c0 = Time.time;
            while (Time.time - c0 < close)
            {
                float k = (Time.time - c0) / close;
                introFold = 1f - BlackjackFx.EaseIn(k) * 0.94f;
                introAlpha = 1f - BlackjackFx.EaseIn(k);
                yield return null;
            }
            HideIntro();
        }

        private IEnumerator CountText(TextMesh tm, long from, long to, float seconds, BlackjackCue tick)
        {
            float t0 = Time.time;
            float next = 0f;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / Mathf.Max(0.01f, seconds));
                long v = from + (long)Math.Round((to - from) * BlackjackFx.EaseOut(k));
                tm.text = v.ToString();
                if (Time.time >= next && k < 1f && Sfx != null)
                {
                    next = Time.time + 0.07f;
                    Sfx.Casino(tick, 1f, 0.6f);
                }
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
        }

        private void TickIntro(float dt)
        {
            if (!introBuilt || !introOpen)
            {
                return;
            }
            float age = Time.time - introT0;
            introRoot.localScale = new Vector3(introScale, introScale * introFold, 1f);
            Alpha(introDim, BlackjackFx.DarkAlpha(0.5f) * introAlpha);
            for (int i = 0; i < introItems.Count; i++)
            {
                IntroItem it = introItems[i];
                float a = Mathf.Clamp01((age - T(it.At)) / T(0.2f)) * introAlpha;
                if (it.Sprite != null)
                {
                    Color c = it.Ink;
                    c.a *= a;
                    it.Sprite.color = c;
                }
                else if (it.Text != null)
                {
                    float extra = it.Text == introFooter ? 0.55f + 0.45f * Mathf.Sin(Time.time * 2.4f) : 1f;
                    TextAlpha(it.Text, it.Ink, a * extra);
                }
            }
            // one glint runs the trim, clockwise from the top left
            float g = (Time.time - introGlintT0) / T(1.1f);
            if (g >= 0f && g <= 1f)
            {
                Vector2 h = introSize * 0.5f;
                float per = 2f * (h.x + h.y);
                float d = g * per;
                Vector2 p;
                if (d < 2f * h.x) p = new Vector2(-h.x + d, h.y);
                else if (d < 2f * h.x + 2f * h.y) p = new Vector2(h.x, h.y - (d - 2f * h.x));
                else if (d < 4f * h.x + 2f * h.y) p = new Vector2(h.x - (d - 2f * h.x - 2f * h.y), -h.y);
                else p = new Vector2(-h.x, -h.y + (d - 4f * h.x - 2f * h.y));
                introGlint.transform.localPosition = p;
                introGlint.transform.localScale = new Vector3(0.5f * L.Type, 0.5f * L.Type, 1f);
                Alpha(introGlint, BlackjackFx.LightAlpha(0.9f) * Mathf.Sin(Mathf.PI * g) * introAlpha);
            }
            else
            {
                Alpha(introGlint, 0f);
            }
        }
    }
}
