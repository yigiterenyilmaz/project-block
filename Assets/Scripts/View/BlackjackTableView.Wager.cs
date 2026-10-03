// PURPOSE: BlackjackTableView's WAGER - the stake lying on the table between the two arenas, and
// the money moving to and from it as small gold tokens.
//
// The plate is a small dark tray with a brass rim, a short chip stack and the amount: "BAHİS 200".
// It is the only place on the table where the stake is drawn, so every movement of money passes
// through it - the bet arrives on it from the bank, a win doubles it IN PLACE before it goes back,
// a push returns it, and a loss is slid off it to the house's side. Its glow follows how much of
// the bank the stake is, so a heavy stake is felt before anything is played.

using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BlackjackTableView
    {
        private Transform wagerRoot;
        private SpriteRenderer wagerGlow;
        private SpriteRenderer wagerRim;
        private SpriteRenderer wagerBody;
        private readonly SpriteRenderer[] wagerChips = new SpriteRenderer[3];
        private TextMesh wagerCaption;
        private TextMesh wagerAmount;

        private double wagerShown;
        private long wagerFrom, wagerTo;
        private float wagerT0 = -1f, wagerDur;
        private bool wagerOn;
        private float wagerA;
        private float wagerPulseT0 = -10f;
        private float wagerPulsePeak = 1.08f;
        private Vector2 wagerOffset;
        private float wagerDark;

        private readonly List<SpriteRenderer> tokenPool = new List<SpriteRenderer>();

        private void BuildWager()
        {
            wagerRoot = new GameObject("Wager").transform;
            wagerRoot.SetParent(transform, false);
            wagerGlow = ViewUtil.MakePlate(wagerRoot, "Glow", Vector2.zero, Vector2.one, BlackjackShapes.Amber, WagerOrder - 1, ViewUtil.GlowSprite);
            wagerRim = ViewUtil.MakeRounded(wagerRoot, "Rim", Vector2.zero, Vector2.one, BlackjackShapes.GoldDim, WagerOrder);
            wagerBody = ViewUtil.MakeRounded(wagerRoot, "Body", Vector2.zero, Vector2.one, new Color(0.07f, 0.055f, 0.06f), WagerOrder + 1);
            for (int i = 0; i < wagerChips.Length; i++)
            {
                wagerChips[i] = ViewUtil.MakeIcon(wagerRoot, "Chip" + i, Vector2.zero, 0.3f, Color.white, WagerOrder + 2 + i, BlackjackShapes.ChipSprite);
            }
            wagerCaption = ViewUtil.MakeText3D(wagerRoot, "Caption", Vector2.zero, "0", 90, 0.15f / 9f, CaptionInk, WagerOrder + 6, TextAnchor.MiddleCenter);
            wagerCaption.text = Loc.Pick("BET", "BAHİS");
            wagerAmount = ViewUtil.MakeText3D(wagerRoot, "Amount", Vector2.zero, "0", 90, 0.3f / 9f, BankInk, WagerOrder + 6, TextAnchor.MiddleCenter);
            if (ViewUtil.UiFontBold != null)
            {
                foreach (TextMesh each in wagerAmount.GetComponentsInChildren<TextMesh>(true))
                {
                    each.font = ViewUtil.UiFontBold;
                    each.GetComponent<MeshRenderer>().material = ViewUtil.UiFontBold.material;
                }
            }
        }

        private void PlaceWager()
        {
            float t = L.Type;
            wagerRoot.localPosition = L.Wager;
            Vector2 size = new Vector2(1.2f, 0.95f) * t;
            wagerGlow.size = size + new Vector2(0.7f, 0.7f) * t;
            wagerRim.size = size + new Vector2(0.05f, 0.05f) * t;
            wagerBody.size = size;
            for (int i = 0; i < wagerChips.Length; i++)
            {
                wagerChips[i].transform.localPosition = new Vector2(-0.02f * t + i * 0.012f * t, 0.2f * t + i * 0.045f * t);
                wagerChips[i].transform.localScale = new Vector3(0.3f * t, 0.22f * t, 1f);
            }
            wagerCaption.transform.localPosition = new Vector2(0f, -0.05f * t);
            wagerCaption.transform.localScale = Vector3.one;
            wagerAmount.transform.localPosition = new Vector2(0f, -0.27f * t);
            wagerAmount.characterSize = 0.3f * t / 9f;
            wagerCaption.characterSize = 0.15f * t / 9f;
            foreach (TextMesh each in wagerAmount.GetComponentsInChildren<TextMesh>(true))
            {
                each.characterSize = wagerAmount.characterSize;
            }
            foreach (TextMesh each in wagerCaption.GetComponentsInChildren<TextMesh>(true))
            {
                each.characterSize = wagerCaption.characterSize;
            }
        }

        private void ResetWager()
        {
            wagerOn = false;
            wagerA = 0f;
            wagerShown = 0;
            wagerT0 = -1f;
            wagerOffset = Vector2.zero;
            wagerDark = 0f;
            for (int i = 0; i < tokenPool.Count; i++)
            {
                tokenPool[i].enabled = false;
            }
        }

        public Vector2 WagerAnchor
        {
            get { return L.Wager + wagerOffset; }
        }

        public long ShownWager
        {
            get { return (long)System.Math.Round(wagerShown); }
        }

        /// <summary>Puts a stake on the plate (or takes it off) at once.</summary>
        public void SetWager(long amount, bool shown)
        {
            wagerShown = wagerFrom = wagerTo = amount;
            wagerT0 = -1f;
            wagerOn = shown;
            wagerOffset = Vector2.zero;
            wagerDark = 0f;
            if (!shown)
            {
                wagerA = 0f;
            }
        }

        /// <summary>Counts the plate's amount to a new value (a win doubling it in place).</summary>
        public void CountWager(long to, float seconds)
        {
            wagerFrom = ShownWager;
            wagerTo = to;
            wagerT0 = Time.time;
            wagerDur = seconds;
            PulseWager(1.12f);
        }

        public void PulseWager(float peak)
        {
            wagerPulseT0 = Time.time;
            wagerPulsePeak = peak;
        }

        private void TickWager(float dt)
        {
            float now = Time.time;
            if (wagerT0 >= 0f)
            {
                float k = Mathf.Clamp01((now - wagerT0) / Mathf.Max(0.01f, wagerDur));
                wagerShown = wagerFrom + (wagerTo - wagerFrom) * BlackjackFx.EaseOut(k);
                if (k >= 1f)
                {
                    wagerT0 = -1f;
                }
            }
            wagerA = Mathf.MoveTowards(wagerA, wagerOn ? 1f : 0f, dt * 6f);
            float p = BlackjackFx.Punch((now - wagerPulseT0) / T(0.3f), 1f, wagerPulsePeak, 0.98f);
            wagerRoot.localPosition = L.Wager + wagerOffset;
            wagerRoot.localScale = new Vector3(p, p, 1f) * (0.92f + 0.08f * wagerA) * (1f - 0.25f * wagerDark);
            Color body = Color.Lerp(new Color(0.07f, 0.055f, 0.06f), BlackjackShapes.Burgundy * 0.6f, wagerDark);
            body.a = 0.92f * wagerA;
            wagerBody.color = body;
            Color rim = Color.Lerp(BlackjackShapes.GoldDim, BlackjackShapes.Burgundy, wagerDark);
            rim.a = wagerA;
            wagerRim.color = rim;
            float glow = (0.12f + 0.45f * tension + 0.25f * allIn) * (1f - wagerDark);
            Color gc = Color.Lerp(BlackjackShapes.Amber, BlackjackShapes.BurgundyLight, allIn * 0.5f);
            wagerGlow.color = new Color(gc.r, gc.g, gc.b, BlackjackFx.LightAlpha(glow) * wagerA * (1f + 0.08f * Mathf.Sin(clock * 2.4f)));
            for (int i = 0; i < wagerChips.Length; i++)
            {
                Color c = Color.Lerp(Color.white, new Color(0.5f, 0.35f, 0.38f), wagerDark);
                c.a = wagerA * (i == 2 && allIn > 0.5f ? 1f : i < 2 ? 1f : 0.85f);
                wagerChips[i].color = c;
            }
            wagerAmount.text = ShownWager.ToString();
            TextAlpha(wagerAmount, Color.Lerp(BankInk, LossInk, wagerDark), wagerA);
            TextAlpha(wagerCaption, CaptionInk, wagerA);
        }

        // ================================================================== money in motion

        private SpriteRenderer RentToken()
        {
            for (int i = 0; i < tokenPool.Count; i++)
            {
                if (!tokenPool[i].enabled)
                {
                    tokenPool[i].enabled = true;
                    tokenPool[i].sprite = BlackjackShapes.TokenSprite;
                    tokenPool[i].color = Color.white;
                    return tokenPool[i];
                }
            }
            SpriteRenderer r = Sprite("Token" + tokenPool.Count, BlackjackShapes.TokenSprite, BlackjackShapes.Gold, FlightOrder + 4);
            tokenPool.Add(r);
            return r;
        }

        /// <summary>
        /// Gold tokens from one place to another along a lifted arc, one after another, each with
        /// a tap where it lands. It is MONEY moving (chips, not points). Waits until the last lands.
        /// </summary>
        public IEnumerator FlyTokens(Vector2 from, Vector2 to, int count, float seconds, BlackjackCue landCue)
        {
            count = Mathf.Clamp(count, 1, 8);
            float t = L.Type;
            float stagger = seconds * 0.12f;
            float each = seconds - stagger * (count - 1) * 0.5f;
            each = Mathf.Max(seconds * 0.55f, each);
            var tokens = new SpriteRenderer[count];
            var starts = new float[count];
            float now = Time.time;
            for (int i = 0; i < count; i++)
            {
                tokens[i] = RentToken();
                tokens[i].transform.localPosition = from;
                tokens[i].transform.localScale = Vector3.zero;
                starts[i] = now + i * stagger * 0.5f;
            }
            Vector2 mid = (from + to) * 0.5f;
            float lift = BlackjackFx.Tuning.betChipTravelArc * t * Mathf.Clamp(Vector2.Distance(from, to) / 3f, 0.5f, 1.4f);
            bool[] landed = new bool[count];
            int left = count;
            while (left > 0)
            {
                now = Time.time;
                for (int i = 0; i < count; i++)
                {
                    if (landed[i])
                    {
                        continue;
                    }
                    float k = Mathf.Clamp01((now - starts[i]) / each);
                    if (now < starts[i])
                    {
                        continue;
                    }
                    float e = BlackjackFx.EaseInOut(k);
                    Vector2 ctrl = mid + new Vector2((i - count * 0.5f) * 0.06f * t, lift);
                    tokens[i].transform.localPosition = BlackjackFx.Bezier(from, ctrl, to, e);
                    float s = 0.17f * t * Mathf.Min(1f, k * 6f) * (1f - 0.35f * Mathf.Max(0f, k - 0.8f) * 5f);
                    tokens[i].transform.localScale = new Vector3(s, s, 1f);
                    tokens[i].color = new Color(1f, 1f, 1f, 1f);
                    if (k >= 1f)
                    {
                        landed[i] = true;
                        left--;
                        tokens[i].enabled = false;
                        if (Sfx != null && (i == 0 || i == count - 1 || i % 2 == 0))
                        {
                            Sfx.Casino(landCue, 1f + 0.03f * i, 0.8f);
                        }
                    }
                }
                yield return null;
            }
        }

        /// <summary>A lost stake: the plate darkens and slides to the house's side, where it is taken.</summary>
        public IEnumerator SlideWagerToHouse(float seconds)
        {
            Vector2 to = L.DealerIntake - L.Wager;
            float t0 = Time.time;
            while (true)
            {
                float k = Mathf.Clamp01((Time.time - t0) / seconds);
                float e = BlackjackFx.EaseIn(k);
                wagerOffset = Vector2.Lerp(Vector2.zero, to, e) + new Vector2(0f, Mathf.Sin(Mathf.PI * k) * 0.25f * L.Type);
                wagerDark = Mathf.Clamp01(k * 1.6f);
                if (k >= 0.85f)
                {
                    wagerOn = false;
                }
                if (k >= 1f)
                {
                    break;
                }
                yield return null;
            }
            wagerOn = false;
            yield return Wait(T(0.12f));
            wagerOffset = Vector2.zero;
            wagerDark = 0f;
            wagerA = 0f;
        }
    }
}
