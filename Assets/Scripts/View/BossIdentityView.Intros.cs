// PURPOSE: BossIdentityView's five INTRO timelines. Each Start* builds its pieces, then sets a
// pose function of absolute time and the cues (shake, sound) it fires on the way.
//
// Shared shape, so they can be compared fairly: about three seconds, the HUD dims while the card is
// up, the boss's NAME is the largest thing on screen at the peak, its RULE is readable under it, and
// the last half second hands over to the ambience (IntroRun.HandOver).
//
//   ALARM      - arcade: a siren flash at the screen edges, hazard bands slam in, the name hits.
//   ECLIPSE    - atmosphere: the world drains to maroon while a dark moon covers a red sun behind
//                the board, and the corona flares at totality.
//   SEAL       - occult: a two-ring seal draws itself round the arena, runes ignite, and it STAMPS.
//   LOCKDOWN   - mechanical: four iron brackets slam onto the corners, bars close, a lock drops.
//   CINEMATIC  - title card: letterbox bars close in, the name spreads wide and tightens.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BossIdentityView
    {
        private SpriteRenderer MakeVeil()
        {
            return Part(introScreen, "Veil", ViewUtil.WhiteSprite, VeilOrder);
        }

        private void PutVeil(SpriteRenderer veil, float darkness)
        {
            Put(veil, Vector2.zero, new Vector2(halfW, halfH) * 2.6f, 0f, Color.black, darkness);
        }

        /// <summary>The caption / name / rule stack most intros share, centred on the board.</summary>
        private sealed class TitleStack
        {
            public Title Caption;
            public Title Name;
            public Title Rule;
        }

        private TitleStack MakeStack(string name, string rule, string caption)
        {
            return new TitleStack
            {
                Caption = MakeTitle(introScreen, "Caption", caption ?? string.Empty, 0.20f, true),
                Name = MakeTitle(introScreen, "Name", Upper(name), 0.80f, true),
                Rule = MakeTitle(introScreen, "Rule", rule, 0.17f, false)
            };
        }

        private void PutStack(TitleStack s, Color captionColour, float t, float inA, float inB,
            float outA, float outB)
        {
            Vector2 c = boardOnScreen;
            float a = Env(t, inA, inB, outA, outB);
            float grow = Mathf.Lerp(1.08f, 1f, OutCubic(Seg(t, inA, inB + 0.4f)));
            s.Caption.Put(c + new Vector2(0f, 0.72f), 1f, captionColour, a);
            s.Name.Put(c + new Vector2(0f, 0.05f), grow, Style.Bone, a);
            s.Rule.Put(c + new Vector2(0f, -0.78f), 1f, Style.Bone, a * Seg(t, inA + 0.15f, inB + 0.25f) * 0.9f);
        }

        // =================================================================== ALARM

        private void StartAlarm(string name, string rule)
        {
            run.Duration = 2.75f;
            run.HandOver = 2.2f;
            run.HoldAt = 1.5f;
            SpriteRenderer veil = MakeVeil();
            SpriteRenderer siren = Part(introScreen, "Siren", EdgeGlow, VeilOrder + 1);
            SpriteRenderer plate = Part(introScreen, "Plate", ViewUtil.WhiteSprite, BandOrder - 1);
            SpriteRenderer bandTop = TiledPart(introScreen, "BandTop", StripeFrame(0f, 0f), BandOrder);
            SpriteRenderer bandBottom = TiledPart(introScreen, "BandBottom", StripeFrame(0f, 0f), BandOrder);
            var edges = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                edges[i] = Part(introScreen, "BandEdge" + i, ViewUtil.WhiteSprite, BandOrder + 1);
            }
            SpriteRenderer diamondL = Part(introScreen, "DiamondL", ViewUtil.WhiteSprite, BandOrder + 2);
            SpriteRenderer diamondR = Part(introScreen, "DiamondR", ViewUtil.WhiteSprite, BandOrder + 2);
            Title tag = MakeTitle(introScreen, "Tag", Loc.Pick("BOSS", "PATRON"), 0.30f, true);
            Title title = MakeTitle(introScreen, "Name", Upper(name), 0.74f, true);
            Title ruleText = MakeTitle(introScreen, "Rule", string.Empty, 0.17f, false);
            float tagWidth = TextWidth(tag.Text, tag.Text.text);
            int shownChars = -1;

            CueSting(0f, BossSting.Siren);
            CueSting(0.55f, BossSting.Boom);
            CueShake(0.58f, 0.16f, 0.32f);

            run.Pose = delegate (float t)
            {
                Vector2 c = new Vector2(0f, boardOnScreen.y);
                float width = halfW * 2.8f;
                float frame = Env(t, 0f, 0.25f, 2.25f, 2.7f);
                SetHud(1f - (1f - Style.HudDim) * frame);
                PutVeil(veil, Style.VeilDarkness * frame);

                float sirenA = 0.62f * Hit(t, 0f, 4.5f) + 0.5f * Hit(t, 0.5f, 4.5f)
                    + 0.35f * Hit(t, 1.0f, 4.5f);
                Put(siren, Vector2.zero, new Vector2(halfW, halfH) * 2.02f, 0f, Style.Threat,
                    sirenA * (1f - Seg(t, 2.2f, 2.6f)));

                const float gap = 1.15f;
                const float bandHeight = 0.44f;
                float inP = OutBack(Seg(t, 0.12f, 0.44f));
                float outP = InCubic(Seg(t, 2.15f, 2.55f));
                float xTop = Mathf.LerpUnclamped(-width, 0f, inP);
                float xBottom = Mathf.LerpUnclamped(width, 0f, inP);
                float yTop = c.y + gap + outP * (halfH + 1.5f);
                float yBottom = c.y - gap - outP * (halfH + 1.5f);
                Sprite stripe = StripeFrame(t, 26f);
                bandTop.sprite = stripe;
                bandBottom.sprite = stripe;
                PutTiled(bandTop, new Vector2(xTop, yTop), new Vector2(width, bandHeight), 0f, 1f);
                PutTiled(bandBottom, new Vector2(xBottom, yBottom), new Vector2(width, bandHeight), 0f, 1f);
                Vector2 edgeSize = new Vector2(width, 0.035f);
                Put(edges[0], new Vector2(xTop, yTop + bandHeight * 0.5f), edgeSize, 0f, Style.Ink, 1f);
                Put(edges[1], new Vector2(xTop, yTop - bandHeight * 0.5f), edgeSize, 0f, Style.Ink, 1f);
                Put(edges[2], new Vector2(xBottom, yBottom + bandHeight * 0.5f), edgeSize, 0f, Style.Ink, 1f);
                Put(edges[3], new Vector2(xBottom, yBottom - bandHeight * 0.5f), edgeSize, 0f, Style.Ink, 1f);

                float plateA = 0.88f * Smooth(Seg(t, 0.18f, 0.42f)) * (1f - Smooth(Seg(t, 2.1f, 2.5f)));
                float slam = Hit(t, 0.58f, 7f);
                Color hot = new Color(Style.Threat.r * 0.55f, Style.Threat.g * 0.4f, Style.Threat.b * 0.4f);
                Put(plate, c, new Vector2(width * Mathf.Clamp01(inP), gap * 2f), 0f,
                    Color.Lerp(Style.Ink, hot, slam), plateA);

                float textOut = 1f - Smooth(Seg(t, 2.1f, 2.4f));
                float tagA = Seg(t, 0.36f, 0.46f) * textOut;
                float blink = 0.6f + 0.4f * Mathf.Round(Mathf.Repeat(t * 3f, 1f));
                tag.Put(c + new Vector2(0f, 0.66f), 1f, Style.Threat, tagA);
                float dx = tagWidth * 0.5f + 0.35f;
                Put(diamondL, c + new Vector2(-dx, 0.66f), Vector2.one * 0.17f, 45f, Style.Threat, tagA * blink);
                Put(diamondR, c + new Vector2(dx, 0.66f), Vector2.one * 0.17f, 45f, Style.Threat, tagA * blink);

                float scale = Mathf.Lerp(2.3f, 1f, OutCubic(Seg(t, 0.46f, 0.6f)));
                title.Put(c + new Vector2(0f, 0.02f), scale, Style.Bone, Seg(t, 0.46f, 0.52f) * textOut);

                int chars = Mathf.FloorToInt(rule.Length * Seg(t, 0.75f, 1.35f));
                if (chars != shownChars)
                {
                    shownChars = chars;
                    ruleText.SetText(rule.Substring(0, chars));
                }
                ruleText.Put(c + new Vector2(0f, -0.58f), 1f, Style.Bone, 0.9f * textOut);
            };
        }

        // =================================================================== ECLIPSE

        private void StartEclipse(string name, string rule, string caption)
        {
            run.Duration = 3.35f;
            run.HandOver = 2.85f;
            run.HoldAt = 2.45f;
            SpriteRenderer veil = MakeVeil();
            EclipseRig rig = BuildEclipse(introWorld, introScreen);
            run.CardIn = new Vector2(1.7f, 2.0f);
            run.CardOut = new Vector2(2.7f, 3.05f);
            SpriteRenderer flare = Part(introWorld, "Flare", SoftDot, FxOrder);
            TitleStack stack = MakeStack(name, rule, caption);

            CueSting(0f, BossSting.Drone);
            CueSting(1.66f, BossSting.Boom);
            CueShake(1.7f, 0.12f, 0.4f);

            run.Pose = delegate (float t)
            {
                SetHud(1f - (1f - Style.HudDim) * Env(t, 0f, 0.6f, 2.7f, 3.2f));
                PutVeil(veil, 0.5f * Env(t, 0.2f, 1.0f, 2.7f, 3.2f));
                float handOver = 1f - Seg(t, 2.85f, 3.3f);
                float tint = Style.EclipseTint * Smooth(Seg(t, 0f, 1.2f)) * handOver;
                float moon = OutCubic(Seg(t, 0.35f, 1.7f));
                Vector2 moonOffset = new Vector2(5.5f, 2.8f) * (1f - moon);
                float sun = Smooth(Seg(t, 0.15f, 0.6f)) * (1f - Smooth(Seg(t, 1.6f, 1.9f)));
                float corona = (Smooth(Seg(t, 1.0f, 1.7f)) * 0.8f + 0.5f * Hit(t, 1.7f, 3f)) * handOver;
                float embers = Seg(t, 1.2f, 2.5f) * handOver;
                PoseEclipse(rig, t, tint, sun, moonOffset, corona, 1f + 0.08f * Hit(t, 1.7f, 4f), embers);

                // The "diamond ring": the last sliver of sun as the moon closes, on the rim.
                float rd = rect.size.magnitude * 0.5f;
                Vector2 rim = rect.center + Polar(rd, 35f);
                float flash = Hit(t, 1.7f, 4f);
                Put(flare, rim, Vector2.one * (0.8f + 2.8f * flash), 0f,
                    Color.Lerp(Style.Corona, Style.Bone, 0.45f), flash * 0.9f);

                PutStack(stack, Style.Threat, t, 1.8f, 2.1f, 2.65f, 3.0f);
            };
        }

        // =================================================================== SEAL

        private void StartSeal(string name, string rule, string caption)
        {
            run.Duration = 3.05f;
            run.HandOver = 2.55f;
            run.HoldAt = 2.3f;
            SpriteRenderer veil = MakeVeil();
            SealRig seal = BuildSeal(introWorld, rect);
            run.CardIn = new Vector2(1.65f, 1.95f);
            run.CardOut = new Vector2(2.5f, 2.9f);
            SpriteRenderer head = Part(introWorld, "DrawHead", SoftDot, SealLineOrder + 1);
            SpriteRenderer flash = Part(introWorld, "StampFlash", SoftDot, FlashBehindOrder);
            TitleStack stack = MakeStack(name, rule, caption);

            CueSting(1.62f, BossSting.Gong);
            CueShake(1.66f, 0.2f, 0.35f);

            run.Pose = delegate (float t)
            {
                SetHud(1f - (1f - Style.HudDim) * Env(t, 0f, 0.4f, 2.45f, 2.95f));
                PutVeil(veil, 0.5f * Env(t, 0f, 0.4f, 2.45f, 2.95f));
                float outer = Smooth(Seg(t, 0.15f, 1.05f));
                float inner = Seg(t, 0.35f, 1.2f);
                float star = Smooth(Seg(t, 0.85f, 1.45f));
                for (int k = 0; k < seal.RuneAlpha.Length; k++)
                {
                    float at = 1.05f + k * 0.03f;
                    seal.RuneAlpha[k] = Seg(t, at, at + 0.08f);
                    seal.RuneScale[k] = Mathf.Lerp(1.9f, 1f, OutCubic(Seg(t, at, at + 0.18f)));
                }
                float scale;
                if (t < 1.55f)
                {
                    scale = Mathf.Lerp(1.12f, 1.10f, Seg(t, 0f, 1.55f));
                }
                else if (t < 1.66f)
                {
                    scale = Mathf.Lerp(1.10f, 0.96f, InCubic(Seg(t, 1.55f, 1.66f)));
                }
                else
                {
                    scale = Mathf.Lerp(0.96f, 1f, OutCubic(Seg(t, 1.66f, 1.85f)));
                }
                float glow = 0.25f * Seg(t, 0.2f, 1.4f) + 0.9f * Hit(t, 1.66f, 3.5f);
                float alpha = 1f - Smooth(Seg(t, 2.55f, 3.0f));
                PoseSeal(seal, rect.center, scale, alpha, outer, inner, star, glow, 0f, 0f);

                bool drawing = outer > 0f && outer < 1f;
                Vector2 headAt = rect.center + Polar(seal.ROut * scale, 90f - 360f * outer);
                Put(head, headAt, Vector2.one * 0.55f, 0f, Style.Bone, drawing ? 0.9f : 0f);
                float stamp = Hit(t, 1.66f, 5f);
                Put(flash, rect.center, Vector2.one * seal.ROut * 2.6f, 0f, Style.Arcane, 0.5f * stamp);

                PutStack(stack, Style.Arcane, t, 1.75f, 2.05f, 2.45f, 2.85f);
            };
        }

        // =================================================================== LOCKDOWN

        private void StartLockdown(string name, string rule, string caption)
        {
            run.Duration = 2.85f;
            run.HandOver = 2.3f;
            run.HoldAt = 2.3f;
            SpriteRenderer veil = MakeVeil();
            CageRig cage = BuildCage(introWorld, rect);
            run.CardIn = new Vector2(1.65f, 1.95f);
            run.CardOut = new Vector2(2.45f, 2.8f);
            var impacts = new SpriteRenderer[4];
            var land = new float[4];
            for (int k = 0; k < 4; k++)
            {
                impacts[k] = Part(introWorld, "Impact" + k, SoftDot, FrameOrder + 8);
                land[k] = 0.25f + 0.16f * k;
                CueSting(land[k], BossSting.Clank);
                CueShake(land[k], 0.07f, 0.15f);
            }
            CueSting(1.62f, BossSting.Boom);
            CueShake(1.62f, 0.2f, 0.3f);
            TitleStack stack = MakeStack(name, rule, caption);
            var fly = new float[4];

            run.Pose = delegate (float t)
            {
                SetHud(1f - (1f - Style.HudDim) * Env(t, 0f, 0.3f, 2.3f, 2.8f));
                PutVeil(veil, 0.42f * Env(t, 0f, 0.3f, 2.3f, 2.8f));
                float bounce = 0f;
                for (int k = 0; k < 4; k++)
                {
                    fly[k] = 1f - InCubic(Seg(t, land[k] - 0.22f, land[k]));
                    bounce = Mathf.Max(bounce, 0.08f * Hit(t, land[k], 14f));
                }
                float rails = OutCubic(Seg(t, 0.95f, 1.35f));
                float drop = 1f - InCubic(Seg(t, 1.4f, 1.62f));
                float squash = 0.12f * Hit(t, 1.62f, 12f);
                float key = Smooth(Seg(t, 1.7f, 1.95f)) * (0.8f + 0.2f * Mathf.Sin(t * 9f));
                float alpha = 1f - Smooth(Seg(t, 2.3f, 2.8f));
                PoseCage(cage, rect, alpha, fly, bounce, rails, drop, squash, key, t);

                float ox = rect.width * 0.5f + CageOutset + cage.T;
                float oy = rect.height * 0.5f + CageOutset + cage.T;
                for (int k = 0; k < 4; k++)
                {
                    Vector2 corner = rect.center + new Vector2(CageCorners[k].x * ox, CageCorners[k].y * oy);
                    float h = Hit(t, land[k], 9f);
                    Put(impacts[k], corner, Vector2.one * (0.6f + 0.9f * (1f - h)), 0f, Style.Threat, 0.75f * h);
                }
                PutStack(stack, Style.Threat, t, 1.75f, 2.05f, 2.4f, 2.75f);
            };
        }

        // =================================================================== CINEMATIC

        private void StartCinematic(string name, string rule, string caption)
        {
            run.Duration = 3.45f;
            run.HandOver = 2.95f;
            run.HoldAt = 2.35f;
            SpriteRenderer veil = MakeVeil();
            LetterboxRig bars = BuildLetterbox(introScreen, CinemaBarOrder);
            run.CardIn = new Vector2(0.45f, 0.9f);
            run.CardOut = new Vector2(2.75f, 3.15f);
            string upper = Upper(name);
            const float letterHeight = 0.72f;
            var letters = new Title[upper.Length];
            var advances = new float[upper.Length];
            float sum = 0f;
            for (int i = 0; i < upper.Length; i++)
            {
                string ch = upper[i].ToString();
                letters[i] = MakeTitle(introScreen, "L" + i, ch, letterHeight, true);
                advances[i] = TextWidth(letters[i].Text, ch);
                sum += advances[i];
            }
            SpriteRenderer lineGlow = Part(introScreen, "LineGlow", SoftDot, TextShadowOrder - 1);
            SpriteRenderer line = Part(introScreen, "Line", ViewUtil.WhiteSprite, TextShadowOrder);
            Title captionText = MakeTitle(introScreen, "Caption",
                Spaced(caption ?? string.Empty), 0.19f, true);
            Title ruleText = MakeTitle(introScreen, "Rule", rule, 0.17f, false);

            CueSting(0f, BossSting.Drone);
            CueSting(0.55f, BossSting.Boom);
            CueShake(0.58f, 0.05f, 0.25f);

            run.Pose = delegate (float t)
            {
                float barFull = halfH * 0.30f;
                float barHeight = t < 2.8f
                    ? Mathf.Lerp(0f, barFull, OutCubic(Seg(t, 0f, 0.45f)))
                    : Mathf.Lerp(barFull, halfH * 0.075f, Smooth(Seg(t, 2.8f, 3.35f)));
                PoseLetterbox(bars, barHeight, 1f - Seg(t, 2.95f, 3.4f), 0.8f);
                SetHud(1f - Env(t, 0f, 0.4f, 2.8f, 3.3f));
                PutVeil(veil, 0.45f * Env(t, 0.1f, 0.6f, 2.8f, 3.3f));

                Vector2 c = boardOnScreen + new Vector2(0f, 0.1f);
                float textOut = 1f - Smooth(Seg(t, 2.7f, 3.1f));
                float extra = Mathf.Lerp(0.85f, 0.10f, OutCubic(Seg(t, 0.55f, 2.0f))) * letterHeight;
                float total = sum + extra * Mathf.Max(0, upper.Length - 1);
                float x = -total * 0.5f;
                float middle = (upper.Length - 1) * 0.5f;
                for (int i = 0; i < upper.Length; i++)
                {
                    float fromMiddle = middle > 0f ? Mathf.Abs(i - middle) / middle : 0f;
                    float start = 0.55f + fromMiddle * 0.45f;
                    float a = Smooth(Seg(t, start, start + 0.4f)) * textOut;
                    letters[i].Put(new Vector2(c.x + x + advances[i] * 0.5f, c.y), 1f, Style.Bone, a);
                    x += advances[i] + extra;
                }
                float finalWidth = sum + 0.10f * letterHeight * Mathf.Max(0, upper.Length - 1);
                float lineWidth = Mathf.Min(finalWidth * 1.15f, halfW * 1.6f) * OutCubic(Seg(t, 1.0f, 1.6f));
                Put(line, c + new Vector2(0f, -0.56f), new Vector2(lineWidth, 0.028f), 0f, Style.Threat, textOut);
                Put(lineGlow, c + new Vector2(0f, -0.56f), new Vector2(lineWidth * 1.1f, 0.35f), 0f,
                    Style.Threat, 0.3f * textOut);
                captionText.Put(c + new Vector2(0f, 0.68f), 1f, Style.Threat, Seg(t, 1.0f, 1.4f) * textOut);
                ruleText.Put(c + new Vector2(0f, -0.98f), 1f, Style.Bone, 0.85f * Seg(t, 1.5f, 1.9f) * textOut);
            };
        }

        /// <summary>"BOSS STAGE" -> "B O S S   S T A G E": a title card's letter-spaced caption.</summary>
        private static string Spaced(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}
