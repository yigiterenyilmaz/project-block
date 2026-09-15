// PURPOSE: BossIdentityView's shared GEOMETRY ("rigs") and the persistent AMBIENCE that poses them.
//
// A rig is built once and posed every frame from plain numbers, so an intro and the ambience it
// hands over to draw exactly the same object - the intro only animates the parameters that the
// ambience then holds still (a bracket's fly-in distance, how much of a ring is drawn, how tall a
// letterbox bar is). The handover itself is a short crossfade (Style.AmbienceFade).
//
// A board-anchored rig is REBUILT when the arena changes size (a layout flip, the mirror world),
// because its proportions were laid out for the old one; posing only moves things.

using System;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BossIdentityView
    {
        private Transform ambWorld;
        private Transform ambScreen;

        /// <summary>What the controller asked for, and what is actually built right now. They
        /// differ during a switch: the old look fades out before the new one is built.</summary>
        private BossAmbience wantedAmbience = BossAmbience.None;

        private BossAmbience builtAmbience = BossAmbience.None;
        private float ambienceTime;
        private float ambienceWeight;
        private Vector2 builtBoardSize;

        private TapeRig tapeRig;
        private SealRig sealRig;
        private CageRig cageRig;
        private EclipseRig eclipseRig;
        private AuraRig auraRig;
        private LetterboxRig letterboxRig;

        public BossAmbience Ambience
        {
            get { return wantedAmbience; }
        }

        /// <summary>Asks for a look. Cheap to call every frame with the same value.</summary>
        public void SetAmbience(BossAmbience style)
        {
            wantedAmbience = style;
        }

        private void TickAmbience(float dt)
        {
            ambienceTime += dt;
            float step = dt / Mathf.Max(0.01f, Style.AmbienceFade);
            if (builtAmbience != wantedAmbience)
            {
                ambienceWeight -= step;
                if (ambienceWeight <= 0f || builtAmbience == BossAmbience.None)
                {
                    RebuildAmbience(wantedAmbience);
                    ambienceWeight = 0f;
                }
            }
            else
            {
                float target = IntroHoldsScreen ? 0f : 1f;
                ambienceWeight = Mathf.MoveTowards(ambienceWeight, target, step);
            }
            if (builtAmbience == BossAmbience.None)
            {
                return;
            }
            if ((rect.size - builtBoardSize).sqrMagnitude > 0.0004f)
            {
                RebuildAmbience(builtAmbience);
            }
            PoseAmbience(Mathf.Clamp01(ambienceWeight));
        }

        private void RebuildAmbience(BossAmbience style)
        {
            ClearChildren(ambWorld);
            ClearChildren(ambScreen);
            tapeRig = null;
            sealRig = null;
            cageRig = null;
            eclipseRig = null;
            auraRig = null;
            letterboxRig = null;
            orbitRig = null;
            lavaRig = null;
            builtAmbience = style;
            builtBoardSize = rect.size;
            switch (style)
            {
                case BossAmbience.HazardTape: tapeRig = BuildTape(ambWorld); break;
                case BossAmbience.BloodEclipse: eclipseRig = BuildEclipse(ambWorld, ambScreen); break;
                case BossAmbience.RuneCircle: sealRig = BuildSeal(ambWorld, rect); break;
                case BossAmbience.IronCage: cageRig = BuildCage(ambWorld, rect); break;
                case BossAmbience.Letterbox: letterboxRig = BuildLetterbox(ambScreen, AmbientBarOrder); break;
                case BossAmbience.Aura: auraRig = BuildAura(ambWorld); break;
                case BossAmbience.Orbital: orbitRig = BuildOrbit(ambWorld, ambScreen, rect); break;
                case BossAmbience.LavaLake: lavaRig = BuildLava(ambWorld, ambScreen); break;
            }
        }

        private void PoseAmbience(float w)
        {
            float t = ambienceTime;
            // Every look answers the board (BossIdentityView.Reactions): `e` is how hard the stage
            // was just hit, `drive` that energy run over time.
            float e = reactEnergy;
            float drive = reactDrive;
            switch (builtAmbience)
            {
                case BossAmbience.HazardTape:
                    // The tape races for a moment, each run is shoved outward as the front reaches
                    // it, and the glow under it flares.
                    PoseTape(tapeRig, rect, t + drive * 2.5f, w, 0.5f + 0.5f * Mathf.Sin(t * 2.2f), e);
                    break;
                case BossAmbience.BloodEclipse:
                    // The corona swells and flares; the embers are blown outward by the front.
                    PoseEclipse(eclipseRig, t + drive * 3f, Style.EclipseTint * w, 0f, Vector2.zero,
                        (0.62f + 0.18f * Mathf.Sin(t * 1.3f) + 0.45f * e) * w,
                        1f + 0.025f * Mathf.Sin(t * 0.9f) + 0.07f * e, w);
                    break;
                case BossAmbience.RuneCircle:
                {
                    // A rune flares and swells as the front passes it; the seal spins up.
                    float spinOuter = t * 2.0f + drive * 40f;
                    float spinInner = -t * 3.5f - drive * 60f;
                    float rMid = (sealRig.ROut + sealRig.RIn) * 0.5f;
                    for (int k = 0; k < sealRig.RuneAlpha.Length; k++)
                    {
                        float wave = Mathf.Cos(2f * Mathf.PI * (t * 0.22f - k / (float)sealRig.RuneAlpha.Length));
                        float a = 90f - 360f * (k + 0.5f) / sealRig.RuneAlpha.Length - spinOuter;
                        float hit = Wave(rect.center + Polar(rMid, a));
                        sealRig.RuneAlpha[k] = 0.45f + 0.55f * Mathf.Pow(Mathf.Max(0f, wave), 8f) + 1.2f * hit;
                        sealRig.RuneScale[k] = 1f + 0.35f * Mathf.Min(1f, hit);
                    }
                    PoseSeal(sealRig, rect.center, 1f + 0.02f * e, Mathf.Clamp01((0.8f + 0.3f * e) * w), 1f, 1f, 1f,
                        0.30f + 0.08f * Mathf.Sin(t * 1.1f) + 0.6f * e, spinOuter, spinInner);
                    break;
                }
                case BossAmbience.IronCage:
                    // The cage takes the blow: brackets rattle and pop, the lock jolts, lights flare.
                    PoseCage(cageRig, rect, w, null, 0.05f * Mathf.Min(1.5f, e), 1f, 0f, 0.14f * Mathf.Min(1.5f, e),
                        0.55f + 0.45f * Mathf.Sin(t * 3.9f) + e, t, e);
                    break;
                case BossAmbience.Letterbox:
                    // The bars are thrown back toward the screen's edges and the hairline burns;
                    // they settle back in as the energy falls away.
                    PoseLetterbox(letterboxRig, halfH * 0.075f * (1f - 0.6f * Mathf.Min(1f, e)), w,
                        0.55f + 0.25f * Mathf.Sin(t * 1.6f) + 0.9f * e);
                    break;
                case BossAmbience.Aura:
                    PoseAura(auraRig, rect, t + drive * 1.5f, w, e);
                    break;
                case BossAmbience.Orbital:
                    // The shared clock, so the intro's planets hand over in place (see Worlds.cs).
                    PoseOrbit(orbitRig, orbitClock, w, w, null, null, 0f);
                    break;
                case BossAmbience.LavaLake:
                    PoseLava(lavaRig, orbitClock, w, 1f);
                    break;
            }
        }

        // =================================================================== HAZARD TAPE
        //
        // Red/black barrier tape round the arena, built like a physical thing rather than four
        // stripy rectangles: each run of tape sits between two heavy CORNER POSTS (so the strips
        // never overlap in a muddle at the corners), drops a shadow, catches the upper-left light
        // on one edge and falls into shade on the other, carries a faint sheen, and marches
        // CLOCKWISE. Nothing on it lights up or blinks - that read as a distraction over the board.

        private sealed class TapeRig
        {
            public readonly SpriteRenderer[] Shadows = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Strips = new SpriteRenderer[4];
            public readonly SpriteRenderer[] LitEdges = new SpriteRenderer[4];
            public readonly SpriteRenderer[] ShadeEdges = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Sheens = new SpriteRenderer[4];
            public readonly SpriteRenderer[] PostShadows = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Posts = new SpriteRenderer[4];
            public readonly SpriteRenderer[] PostBevels = new SpriteRenderer[4];
            public readonly SpriteRenderer[] PostFaces = new SpriteRenderer[4];
            public SpriteRenderer Glow;
        }

        private const float TapeGap = 0.12f;
        private const float TapeThickness = 0.19f;

        private static readonly Color PostBody = new Color(0.11f, 0.09f, 0.10f);
        private static readonly Color PostFace = new Color(0.19f, 0.15f, 0.16f);

        private static TapeRig BuildTape(Transform parent)
        {
            var rig = new TapeRig();
            rig.Glow = Part(parent, "TapeGlow", GlowBox, AuraOrder);
            for (int i = 0; i < 4; i++)
            {
                rig.Shadows[i] = Part(parent, "TapeShadow" + i, ViewUtil.WhiteSprite, FrameOrder - 2);
                rig.Strips[i] = TiledPart(parent, "Tape" + i, StripeFrame(0f, 0f), FrameOrder);
                rig.Sheens[i] = Part(parent, "TapeSheen" + i, ViewUtil.WhiteSprite, FrameOrder + 1);
                rig.LitEdges[i] = Part(parent, "TapeLit" + i, ViewUtil.WhiteSprite, FrameOrder + 1);
                rig.ShadeEdges[i] = Part(parent, "TapeShade" + i, ViewUtil.WhiteSprite, FrameOrder + 1);
                rig.PostShadows[i] = SlicedPart(parent, "PostShadow" + i, FrameOrder - 1);
                rig.Posts[i] = SlicedPart(parent, "Post" + i, FrameOrder + 3);
                rig.PostBevels[i] = Part(parent, "PostBevel" + i, ViewUtil.WhiteSprite, FrameOrder + 4);
                rig.PostFaces[i] = SlicedPart(parent, "PostFace" + i, FrameOrder + 4);
            }
            return rig;
        }

        private void PoseTape(TapeRig rig, Rect r, float t, float alpha, float pulse, float energy = 0f)
        {
            float g = TapeGap;
            float th = TapeThickness;
            float line = g + th * 0.5f;
            // top, right, bottom, left - each turned so the stripes march CLOCKWISE
            var centres = new[]
            {
                new Vector2(r.center.x, r.yMax + line),
                new Vector2(r.xMax + line, r.center.y),
                new Vector2(r.center.x, r.yMin - line),
                new Vector2(r.xMin - line, r.center.y)
            };
            float[] degrees = { 0f, -90f, 180f, 90f };
            float[] lengths = { r.width + 2f * g, r.height + 2f * g, r.width + 2f * g, r.height + 2f * g };
            Vector2[] outward = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            var light = new Vector2(-0.6f, 0.8f);
            var drop = new Vector2(0.05f, -0.07f);
            Sprite frame = StripeFrame(t, 14f);

            for (int i = 0; i < 4; i++)
            {
                bool horizontal = i % 2 == 0;
                float len = lengths[i];
                // Shoved outward as a front reaches this run of tape.
                Vector2 c = centres[i] + outward[i] * Mathf.Min(0.18f, 0.12f * Wave(centres[i]));
                Vector2 along = horizontal ? new Vector2(len, th) : new Vector2(th, len);
                Put(rig.Shadows[i], c + drop, along, 0f, Color.black, 0.38f * alpha);
                rig.Strips[i].sprite = frame;
                PutTiled(rig.Strips[i], c, new Vector2(len, th), degrees[i], alpha);

                // The edge that faces the light catches it; the other one falls into shade.
                Vector2 n = outward[i];
                Vector2 litSide = Vector2.Dot(n, light) >= 0f ? n : -n;
                Vector2 edge = horizontal ? new Vector2(len, 0.024f) : new Vector2(0.024f, len);
                Vector2 shadeEdge = horizontal ? new Vector2(len, 0.04f) : new Vector2(0.04f, len);
                Put(rig.LitEdges[i], c + litSide * (th * 0.5f - 0.012f), edge, 0f, Style.Bone, 0.32f * alpha);
                Put(rig.ShadeEdges[i], c - litSide * (th * 0.5f - 0.02f), shadeEdge, 0f, Color.black, 0.5f * alpha);
                Vector2 sheen = horizontal ? new Vector2(len, th * 0.22f) : new Vector2(th * 0.22f, len);
                Put(rig.Sheens[i], c + litSide * th * 0.17f, sheen, 0f, Color.white, 0.07f * alpha);
            }

            // Corner posts: where the runs of tape end.
            float post = th * 1.9f;
            var corners = new[]
            {
                new Vector2(r.xMin - line, r.yMax + line), new Vector2(r.xMax + line, r.yMax + line),
                new Vector2(r.xMax + line, r.yMin - line), new Vector2(r.xMin - line, r.yMin - line)
            };
            for (int k = 0; k < 4; k++)
            {
                Vector2 p = corners[k];
                p += (p - r.center).normalized * Mathf.Min(0.15f, 0.1f * Wave(p));
                PlaceSliced(rig.PostShadows[k], p + drop * 1.3f, Vector2.one * post, Color.black, 0.45f * alpha);
                PlaceSliced(rig.Posts[k], p, Vector2.one * post, PostBody, alpha);
                Put(rig.PostBevels[k], p + new Vector2(0f, post * 0.5f - 0.025f), new Vector2(post * 0.8f, 0.02f), 0f,
                    Style.Bone, 0.25f * alpha);
                PlaceSliced(rig.PostFaces[k], p - new Vector2(0f, 0.01f), Vector2.one * post * 0.7f, PostFace, alpha);
            }

            Vector2 glowSize = (r.size + Vector2.one * 2f * (g + th)) / GlowBoxFill;
            Put(rig.Glow, r.center, glowSize * (1f + 0.04f * energy), 0f, Style.Threat,
                (0.14f + 0.4f * energy) * alpha);
        }

        // =================================================================== RUNE SEAL

        private sealed class SealRig
        {
            public Transform Root;
            public Transform Outer;
            public Transform Inner;
            public SpriteRenderer[] OuterSegs;
            public SpriteRenderer[] InnerSegs;
            public SpriteRenderer[] Star;
            public Vector2[] StarFrom;
            public Vector2[] StarTo;
            public Transform[] Runes;
            public SpriteRenderer[][] RuneBars;
            public float[] RuneAlpha;
            public float[] RuneScale;
            public SpriteRenderer GlowOuter;
            public SpriteRenderer GlowInner;
            public float ROut;
            public float RIn;
        }

        private const int SealOuterSegments = 128;
        private const int SealInnerSegments = 48;
        private const int SealRunes = 16;

        /// <summary>A two-ring summoning seal the board sits ON: most of it hides behind the
        /// arena, and what shows is an arc and a star point past each edge.</summary>
        private static SealRig BuildSeal(Transform parent, Rect r)
        {
            var s = new SealRig();
            float half = Mathf.Max(r.width, r.height) * 0.5f;
            s.ROut = half * 1.34f;
            s.RIn = half * 1.18f;
            s.Root = new GameObject("Seal").transform;
            s.Root.SetParent(parent, false);
            s.Outer = new GameObject("Outer").transform;
            s.Outer.SetParent(s.Root, false);
            s.Inner = new GameObject("Inner").transform;
            s.Inner.SetParent(s.Root, false);

            s.GlowOuter = Part(s.Root, "GlowOuter", SoftRing, SealGlowOrder);
            Put(s.GlowOuter, Vector2.zero, Vector2.one * s.ROut / 0.40f, 0f, Style.Arcane, 0f);
            s.GlowInner = Part(s.Root, "GlowInner", SoftRing, SealGlowOrder);
            Put(s.GlowInner, Vector2.zero, Vector2.one * s.RIn / 0.40f, 0f, Style.Arcane, 0f);

            s.OuterSegs = new SpriteRenderer[SealOuterSegments];
            float outerLen = 2f * Mathf.PI * s.ROut / SealOuterSegments * 1.08f;
            for (int i = 0; i < SealOuterSegments; i++)
            {
                float a = 90f - 360f * i / SealOuterSegments;
                SpriteRenderer seg = Part(s.Outer, "O" + i, ViewUtil.WhiteSprite, SealLineOrder);
                Put(seg, Polar(s.ROut, a), new Vector2(outerLen, 0.045f), a + 90f, Style.Arcane, 0f);
                s.OuterSegs[i] = seg;
            }
            s.InnerSegs = new SpriteRenderer[SealInnerSegments];
            float innerLen = 2f * Mathf.PI * s.RIn / (SealInnerSegments * 2f);
            for (int i = 0; i < SealInnerSegments; i++)
            {
                float a = -90f + 360f * i / SealInnerSegments;
                SpriteRenderer seg = Part(s.Inner, "I" + i, ViewUtil.WhiteSprite, SealLineOrder);
                Put(seg, Polar(s.RIn, a), new Vector2(innerLen, 0.03f), a + 90f, Style.Arcane, 0f);
                s.InnerSegs[i] = seg;
            }

            // Two squares inscribed in the inner ring: an eight-point star whose tips poke out
            // past the board's edges.
            s.Star = new SpriteRenderer[8];
            s.StarFrom = new Vector2[8];
            s.StarTo = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float offset = i < 4 ? 0f : 45f;
                int k = i % 4;
                s.StarFrom[i] = Polar(s.RIn, offset + 90f * k);
                s.StarTo[i] = Polar(s.RIn, offset + 90f * (k + 1));
                s.Star[i] = Part(s.Inner, "Star" + i, ViewUtil.WhiteSprite, SealLineOrder);
            }

            s.Runes = new Transform[SealRunes];
            s.RuneBars = new SpriteRenderer[SealRunes][];
            s.RuneAlpha = new float[SealRunes];
            s.RuneScale = new float[SealRunes];
            float rMid = (s.ROut + s.RIn) * 0.5f;
            for (int k = 0; k < SealRunes; k++)
            {
                float a = 90f - 360f * (k + 0.5f) / SealRunes;
                Transform glyph = new GameObject("Rune" + k).transform;
                glyph.SetParent(s.Outer, false);
                glyph.localPosition = Polar(rMid, a);
                glyph.localRotation = Quaternion.Euler(0f, 0f, a - 90f);
                s.Runes[k] = glyph;
                s.RuneBars[k] = BuildGlyph(glyph, k);
            }
            return s;
        }

        /// <summary>A made-up rune: a stem and two strokes picked by hash - never a letter.</summary>
        private static SpriteRenderer[] BuildGlyph(Transform glyph, int k)
        {
            var bars = new SpriteRenderer[3];
            const float h = 0.24f;
            const float w = 0.028f;
            bars[0] = Part(glyph, "Stem", ViewUtil.WhiteSprite, RuneOrder);
            Put(bars[0], Vector2.zero, new Vector2(w, h), 0f, Style.Arcane, 0f);
            for (int b = 1; b < 3; b++)
            {
                bars[b] = Part(glyph, "Stroke" + b, ViewUtil.WhiteSprite, RuneOrder);
                float y = (Hash(k, 10 + b) - 0.5f) * h * 0.8f;
                float side = Hash(k, 20 + b) < 0.5f ? -1f : 1f;
                float degrees = Hash(k, 30 + b) < 0.5f ? 90f : 90f + side * 40f;
                Put(bars[b], new Vector2(side * 0.05f, y), new Vector2(w, h * 0.45f), degrees,
                    Style.Arcane, 0f);
            }
            return bars;
        }

        private static Vector2 Polar(float radius, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }

        /// <summary>Poses the seal. The draw fractions say how much of each ring and of the star
        /// exists (0..1); runes read the rig's own RuneAlpha/RuneScale arrays.</summary>
        private static void PoseSeal(SealRig s, Vector2 centre, float scale, float alpha,
            float outerDraw, float innerDraw, float starDraw, float glow, float spinOuter,
            float spinInner)
        {
            s.Root.localPosition = new Vector3(centre.x, centre.y, 0f);
            s.Root.localScale = new Vector3(scale, scale, 1f);
            s.Outer.localRotation = Quaternion.Euler(0f, 0f, -spinOuter);
            s.Inner.localRotation = Quaternion.Euler(0f, 0f, -spinInner);

            float drawn = outerDraw * SealOuterSegments;
            for (int i = 0; i < SealOuterSegments; i++)
            {
                if (i >= drawn)
                {
                    s.OuterSegs[i].color = Color.clear;
                    continue;
                }
                float head = outerDraw < 1f ? Mathf.Clamp01(1f - (drawn - i) / 6f) : 0f;
                Color c = Color.Lerp(Style.Arcane, Style.Bone, head * 0.8f);
                c.a = alpha;
                s.OuterSegs[i].color = c;
            }
            float innerDrawn = innerDraw * SealInnerSegments;
            for (int i = 0; i < SealInnerSegments; i++)
            {
                Color c = Style.Arcane;
                c.a = i < innerDrawn ? alpha * 0.85f : 0f;
                s.InnerSegs[i].color = c;
            }
            for (int i = 0; i < 8; i++)
            {
                Vector2 d = s.StarTo[i] - s.StarFrom[i];
                float len = d.magnitude * Mathf.Clamp01(starDraw);
                Vector2 dir = d.normalized;
                Put(s.Star[i], s.StarFrom[i] + dir * len * 0.5f, new Vector2(len, 0.026f),
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Style.Arcane, alpha * 0.7f);
            }
            for (int k = 0; k < SealRunes; k++)
            {
                float sc = s.RuneScale[k];
                s.Runes[k].localScale = new Vector3(sc, sc, 1f);
                for (int b = 0; b < 3; b++)
                {
                    Color c = Style.Arcane;
                    c.a = alpha * s.RuneAlpha[k];
                    s.RuneBars[k][b].color = c;
                }
            }
            Color g = Style.Arcane;
            g.a = Mathf.Clamp01(glow) * alpha * outerDraw;
            s.GlowOuter.color = g;
            g.a = Mathf.Clamp01(glow) * 0.7f * alpha * innerDraw;
            s.GlowInner.color = g;
        }

        // =================================================================== IRON CAGE

        private sealed class CageRig
        {
            public readonly Transform[] Brackets = new Transform[4];
            public readonly SpriteRenderer[][] BracketParts = new SpriteRenderer[4][];
            public readonly Color[][] BracketColours = new Color[4][];
            public readonly SpriteRenderer[] Leds = new SpriteRenderer[4];
            public readonly SpriteRenderer[] Rails = new SpriteRenderer[8];
            public readonly SpriteRenderer[] RailLights = new SpriteRenderer[8];
            public Transform Lock;
            public SpriteRenderer[] LockParts;
            public Color[] LockColours;
            public SpriteRenderer KeyGlow;
            public float L;
            public float T;
        }

        private const float CageOutset = 0.07f;

        /// <summary>Corner sign for bracket k: top-left, top-right, bottom-right, bottom-left.</summary>
        private static readonly Vector2[] CageCorners =
        {
            new Vector2(-1f, 1f), new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f)
        };

        private static CageRig BuildCage(Transform parent, Rect r)
        {
            var c = new CageRig();
            float cell = Mathf.Max(r.width, r.height) / 7f;
            c.L = cell * 1.15f;
            c.T = cell * 0.21f;
            for (int k = 0; k < 4; k++)
            {
                float sx = CageCorners[k].x;
                float sy = CageCorners[k].y;
                Transform b = new GameObject("Bracket" + k).transform;
                b.SetParent(parent, false);
                c.Brackets[k] = b;
                float L = c.L;
                float T = c.T;
                var parts = new System.Collections.Generic.List<SpriteRenderer>();
                var colours = new System.Collections.Generic.List<Color>();
                System.Action<string, Sprite, int, Vector2, Vector2, Color> add =
                    delegate (string name, Sprite sprite, int order, Vector2 at, Vector2 size, Color colour)
                    {
                        SpriteRenderer sr = Part(b, name, sprite, order);
                        Put(sr, at, size, 0f, colour, 0f);
                        parts.Add(sr);
                        colours.Add(colour);
                    };
                add("ArmH", ViewUtil.WhiteSprite, FrameOrder, new Vector2(-sx * L * 0.5f, -sy * T * 0.5f), new Vector2(L, T), Style.Iron);
                add("ArmV", ViewUtil.WhiteSprite, FrameOrder, new Vector2(-sx * T * 0.5f, -sy * L * 0.5f), new Vector2(T, L), Style.Iron);
                add("LightH", ViewUtil.WhiteSprite, FrameOrder + 1, new Vector2(-sx * L * 0.5f, -sy * 0.02f), new Vector2(L, 0.04f), Style.IronLight);
                add("LightV", ViewUtil.WhiteSprite, FrameOrder + 1, new Vector2(-sx * 0.02f, -sy * L * 0.5f), new Vector2(0.04f, L), Style.IronLight);
                add("ShadeH", ViewUtil.WhiteSprite, FrameOrder + 1, new Vector2(-sx * (L + T) * 0.5f, -sy * (T - 0.02f)), new Vector2(L - T, 0.04f), Style.IronDark);
                add("ShadeV", ViewUtil.WhiteSprite, FrameOrder + 1, new Vector2(-sx * (T - 0.02f), -sy * (L + T) * 0.5f), new Vector2(0.04f, L - T), Style.IronDark);
                float[] along = { 0.50f, 0.84f };
                for (int i = 0; i < 2; i++)
                {
                    float a = L * along[i];
                    Vector2 h = new Vector2(-sx * a, -sy * T * 0.5f);
                    Vector2 v = new Vector2(-sx * T * 0.5f, -sy * a);
                    add("RivetH" + i, Disc, FrameOrder + 2, h, Vector2.one * T * 0.42f, Style.IronLight);
                    add("RivetV" + i, Disc, FrameOrder + 2, v, Vector2.one * T * 0.42f, Style.IronLight);
                    add("PinH" + i, Disc, FrameOrder + 3, h + new Vector2(0.008f, -0.008f), Vector2.one * T * 0.2f, Style.IronDark);
                    add("PinV" + i, Disc, FrameOrder + 3, v + new Vector2(0.008f, -0.008f), Vector2.one * T * 0.2f, Style.IronDark);
                }
                c.BracketParts[k] = parts.ToArray();
                c.BracketColours[k] = colours.ToArray();
                c.Leds[k] = Part(b, "Led", SoftDot, FrameOrder + 3);
                Put(c.Leds[k], new Vector2(-sx * T * 0.5f, -sy * T * 0.5f), Vector2.one * T * 1.3f, 0f, Style.Threat, 0f);
            }
            for (int i = 0; i < 8; i++)
            {
                c.Rails[i] = Part(parent, "Rail" + i, ViewUtil.WhiteSprite, FrameOrder);
                c.RailLights[i] = Part(parent, "RailLight" + i, ViewUtil.WhiteSprite, FrameOrder + 1);
            }

            c.Lock = new GameObject("Lock").transform;
            c.Lock.SetParent(parent, false);
            var lockParts = new System.Collections.Generic.List<SpriteRenderer>();
            var lockColours = new System.Collections.Generic.List<Color>();
            System.Action<string, Sprite, int, Vector2, Vector2, Color> addLock =
                delegate (string name, Sprite sprite, int order, Vector2 at, Vector2 size, Color colour)
                {
                    SpriteRenderer sr = Part(c.Lock, name, sprite, order);
                    Put(sr, at, size, 0f, colour, 0f);
                    lockParts.Add(sr);
                    lockColours.Add(colour);
                };
            float u = cell;
            addLock("Shackle", HardRing, FrameOrder + 4, new Vector2(0f, 0.22f * u), Vector2.one * 0.44f * u, Style.IronLight);
            addLock("Body", ViewUtil.RoundedSprite, FrameOrder + 5, Vector2.zero, new Vector2(0.58f, 0.46f) * u, Style.Iron);
            addLock("BodyLight", ViewUtil.WhiteSprite, FrameOrder + 6, new Vector2(0f, 0.18f * u), new Vector2(0.48f * u, 0.035f), Style.IronLight);
            addLock("Key", Disc, FrameOrder + 7, new Vector2(0f, 0.02f * u), Vector2.one * 0.11f * u, Style.Threat);
            addLock("Slot", ViewUtil.WhiteSprite, FrameOrder + 7, new Vector2(0f, -0.06f * u), new Vector2(0.045f, 0.12f) * u, Style.Threat);
            c.LockParts = lockParts.ToArray();
            c.LockColours = lockColours.ToArray();
            c.KeyGlow = Part(c.Lock, "KeyGlow", SoftDot, FrameOrder + 6);
            Put(c.KeyGlow, new Vector2(0f, -0.01f * u), Vector2.one * 0.42f * u, 0f, Style.Threat, 0f);
            return c;
        }

        /// <summary>Poses the cage. <paramref name="fly"/> is per bracket, 0 = seated, 1 = off in
        /// the corner of the screen (null = all seated); <paramref name="bounce"/> the squash of an
        /// impact; <paramref name="rails"/> how far the bars have closed; <paramref name="lockDrop"/>
        /// 0 = seated, 1 = well above.</summary>
        private static void PoseCage(CageRig c, Rect r, float alpha, float[] fly, float bounce,
            float rails, float lockDrop, float lockSquash, float key, float t, float rattle = 0f)
        {
            float L = c.L;
            float T = c.T;
            float ox = r.width * 0.5f + CageOutset + T;
            float oy = r.height * 0.5f + CageOutset + T;
            for (int k = 0; k < 4; k++)
            {
                float sx = CageCorners[k].x;
                float sy = CageCorners[k].y;
                float f = fly != null ? fly[k] : 0f;
                Vector2 corner = r.center + new Vector2(sx * ox, sy * oy);
                Vector2 at = corner + new Vector2(sx, sy) * 5f * f;
                // A hit on the board rattles each bracket on its own beat.
                float shake = Mathf.Min(1.5f, rattle);
                at += new Vector2(Mathf.Sin(t * 57f + k * 2.1f), Mathf.Sin(t * 63f + k * 1.3f)) * 0.035f * shake;
                Transform b = c.Brackets[k];
                b.localPosition = new Vector3(at.x, at.y, 0f);
                b.localRotation = Quaternion.Euler(0f, 0f, sx * sy * 30f * f + Mathf.Sin(t * 49f + k) * 3f * shake);
                float sc = 1f + bounce;
                b.localScale = new Vector3(sc, sc, 1f);
                SpriteRenderer[] parts = c.BracketParts[k];
                for (int i = 0; i < parts.Length; i++)
                {
                    Color col = c.BracketColours[k][i];
                    col.a = alpha;
                    parts[i].color = col;
                }
                float led = Mathf.Clamp01(0.35f + 0.65f * Mathf.Max(0f, Mathf.Sin(t * 2.4f - k * 1.2f)) + rattle);
                c.Leds[k].color = new Color(Style.Threat.r, Style.Threat.g, Style.Threat.b,
                    alpha * led * (1f - f));
            }
            // Rails: two halves per edge, each growing from its bracket toward the middle.
            float railT = T * 0.45f;
            for (int e = 0; e < 4; e++)
            {
                bool horizontal = e % 2 == 0;
                float sign = e < 2 ? 1f : -1f;
                float lineCoord = horizontal
                    ? r.center.y + sign * (oy - T * 0.5f)
                    : r.center.x + sign * (ox - T * 0.5f);
                float span = horizontal ? ox - L : oy - L;
                float fromCentre = horizontal ? r.center.x : r.center.y;
                for (int h = 0; h < 2; h++)
                {
                    float end = h == 0 ? -1f : 1f;
                    float start = fromCentre + end * span;
                    float len = span * Mathf.Clamp01(rails);
                    float mid = start - end * len * 0.5f;
                    int i = e * 2 + h;
                    Vector2 pos = horizontal ? new Vector2(mid, lineCoord) : new Vector2(lineCoord, mid);
                    Vector2 size = horizontal ? new Vector2(len, railT) : new Vector2(railT, len);
                    Vector2 lightPos = pos + (horizontal ? new Vector2(0f, sign * railT * 0.3f)
                        : new Vector2(sign * railT * 0.3f, 0f));
                    Vector2 lightSize = horizontal ? new Vector2(len, 0.022f) : new Vector2(0.022f, len);
                    Put(c.Rails[i], pos, size, 0f, Style.Iron, alpha);
                    Put(c.RailLights[i], lightPos, lightSize, 0f, Style.IronLight, alpha);
                }
            }
            Vector2 lockAt = new Vector2(r.center.x, r.center.y + oy - T * 0.5f)
                + Vector2.up * 3f * lockDrop
                + new Vector2(Mathf.Sin(t * 71f) * 0.03f, Mathf.Abs(Mathf.Sin(t * 23f)) * 0.06f) * Mathf.Min(1.5f, rattle);
            c.Lock.localPosition = new Vector3(lockAt.x, lockAt.y, 0f);
            c.Lock.localScale = new Vector3(1f + lockSquash, 1f - lockSquash * 0.6f, 1f);
            for (int i = 0; i < c.LockParts.Length; i++)
            {
                Color col = c.LockColours[i];
                if (c.LockParts[i].name == "Key" || c.LockParts[i].name == "Slot")
                {
                    col = Color.Lerp(Style.IronDark, Style.Threat, Mathf.Clamp01(key));
                }
                col.a = alpha;
                c.LockParts[i].color = col;
            }
            c.KeyGlow.color = new Color(Style.Threat.r, Style.Threat.g, Style.Threat.b,
                alpha * 0.6f * Mathf.Clamp01(key));
        }

        // =================================================================== BLOOD ECLIPSE
        //
        // Behind the arena a dark moon has covered a red sun: what is left is a CORONA of uneven
        // streamers (two layers turning slowly against each other, so it shimmers rather than
        // spins), a hot thin RING OF FIRE right on the moon's limb, the moon itself as a crisp disc
        // with a faint earthshine, a darkened world round it and embers drifting up past.
        //
        // RESOLUTION MATTERS HERE: every one of these is stretched across most of the screen, so
        // they are baked at 768 texels (EclipseTexels) with a one-texel antialiased edge and a
        // sub-quantisation dither in the corona's long falloff - the first pass used the shared
        // 128/256 sprites and came out soft-edged and banded.

        private sealed class EclipseRig
        {
            public SpriteRenderer Tint;
            public SpriteRenderer Sun;
            public SpriteRenderer CoronaGlow;
            public SpriteRenderer CoronaA;
            public SpriteRenderer CoronaB;
            public SpriteRenderer Moon;
            public SpriteRenderer Earthshine;
            public SpriteRenderer Rim;
            public SpriteRenderer[] Embers;
            public SpriteRenderer[] EmberCores;
        }

        private const int EmberCount = 26;
        private const int EclipseTexels = 768;

        /// <summary>Where the moon's limb sits in each baked texture, as a fraction of its width.</summary>
        private const float CoronaLimb = 0.20f;

        private const float RimLimb = 0.42f;

        private static Sprite eclipseCorona;
        private static Sprite eclipseMoon;
        private static Sprite eclipseRim;

        private static EclipseRig BuildEclipse(Transform world, Transform screen)
        {
            var e = new EclipseRig();
            e.Tint = Part(screen, "Tint", FadeUp, TintOrder);
            e.Sun = Part(world, "Sun", EclipseMoonSprite, HaloOrder);
            e.CoronaGlow = Part(world, "CoronaGlow", SoftDot, HaloOrder);
            e.CoronaA = Part(world, "CoronaA", EclipseCoronaSprite, HaloOrder + 1);
            e.CoronaB = Part(world, "CoronaB", EclipseCoronaSprite, HaloOrder + 1);
            e.Moon = Part(world, "Moon", EclipseMoonSprite, HaloOrder + 2);
            e.Earthshine = Part(world, "Earthshine", SoftDot, HaloOrder + 3);
            e.Rim = Part(world, "Rim", EclipseRimSprite, HaloOrder + 4);
            e.Embers = Many(world, "Ember", SoftDot, EmberOrder, EmberCount);
            e.EmberCores = Many(world, "EmberCore", SoftDot, EmberOrder + 1, EmberCount);
            return e;
        }

        /// <summary>The world goes maroon; behind the board a dark moon covers a red sun and only
        /// its corona is left round the arena; embers rise past it.</summary>
        private void PoseEclipse(EclipseRig e, float t, float tint, float sun, Vector2 moonOffset,
            float coronaAlpha, float coronaScale, float embers)
        {
            float rd = rect.size.magnitude * 0.5f * 1.02f;
            Vector2 c = rect.center;
            // Darker toward the bottom of the screen: FadeUp is opaque at its bottom edge.
            Put(e.Tint, Vector2.zero, new Vector2(halfW, halfH) * 2.6f, 0f, new Color(0.10f, 0.01f, 0.03f),
                Mathf.Clamp01(tint * 1.1f));
            Put(e.Sun, c, Vector2.one * rd * 2f, 0f, Style.Corona, sun);

            float a = Mathf.Clamp01(coronaAlpha);
            Put(e.CoronaGlow, c, Vector2.one * rd * 5.2f * coronaScale, 0f, new Color(0.85f, 0.12f, 0.10f),
                0.30f * a);
            float coronaSize = rd / CoronaLimb * coronaScale;
            Put(e.CoronaA, c, Vector2.one * coronaSize, t * 1.4f, new Color(1f, 0.40f, 0.24f), a);
            Put(e.CoronaB, c, Vector2.one * coronaSize * 1.07f, 37f - t * 0.9f, new Color(0.80f, 0.10f, 0.14f),
                0.65f * a);

            float moonA = Mathf.Max(sun, Mathf.Clamp01(coronaAlpha * 1.6f));
            Vector2 moonAt = c + moonOffset;
            Put(e.Moon, moonAt, Vector2.one * rd * 2f, 0f, new Color(0.03f, 0.008f, 0.016f), moonA);
            Put(e.Earthshine, moonAt + new Vector2(-rd * 0.25f, rd * 0.25f), Vector2.one * rd * 1.5f, 0f,
                new Color(0.28f, 0.05f, 0.08f), 0.22f * moonA);
            // The ring of fire flickers very slightly - never a pulse.
            float flicker = 0.92f + 0.08f * Mathf.Sin(t * 3.1f) * Mathf.Sin(t * 1.7f + 1f);
            Put(e.Rim, moonAt, Vector2.one * rd / RimLimb, 0f, new Color(1f, 0.62f, 0.40f),
                a * flicker * (1f - Mathf.Clamp01(moonOffset.magnitude)));

            for (int i = 0; i < EmberCount; i++)
            {
                float period = 7f + 5f * Hash(i, 3);
                float phase = Mathf.Repeat(t / period + Hash(i, 2), 1f);
                float x = c.x + (Hash(i, 1) - 0.5f) * halfW * 1.9f + Mathf.Sin(t * 0.8f + i) * 0.25f;
                float y = c.y + Mathf.Lerp(-halfH * 1.15f, halfH * 1.15f, phase);
                float size = 0.07f + 0.12f * Hash(i, 4);
                float flick = 0.75f + 0.25f * Mathf.Sin(t * (5f + 4f * Hash(i, 5)) + i);
                // A passing front blows the embers outward and fans them brighter.
                var ember = new Vector2(x, y);
                float gust = Wave(ember);
                ember += WavePush(ember) * 0.6f;
                float ea = Mathf.Clamp01(Mathf.Sin(phase * Mathf.PI) * embers * flick * (1f + 0.8f * gust));
                Put(e.Embers[i], ember, Vector2.one * size * 2.2f * (1f + 0.4f * Mathf.Min(1f, gust)), 0f,
                    new Color(1f, 0.30f, 0.12f), 0.45f * ea);
                Put(e.EmberCores[i], ember, Vector2.one * size * 0.7f, 0f, new Color(1f, 0.78f, 0.52f), ea);
            }
        }

        /// <summary>Solid inside the limb; outside it, streamers of uneven length falling off, with
        /// a dither in the alpha so the long falloff does not band.</summary>
        private static Sprite EclipseCoronaSprite
        {
            get
            {
                if (eclipseCorona == null)
                {
                    eclipseCorona = Baked(EclipseTexels, delegate (float r, float theta, int x, int y)
                    {
                        if (r <= CoronaLimb)
                        {
                            return 1f;
                        }
                        float k = Mathf.Clamp01((r - CoronaLimb) / (0.5f - CoronaLimb));
                        float rays = 0.5f + 0.5f * (0.45f * Mathf.Cos(7f * theta + 0.6f)
                            + 0.30f * Mathf.Cos(13f * theta + 2.1f) + 0.25f * Mathf.Cos(31f * theta + 4.4f));
                        // Streamers reach further than the gaps between them.
                        float reach = Mathf.Lerp(0.55f, 1.25f, rays * rays);
                        float fall = Mathf.Pow(Mathf.Clamp01(1f - k / reach), 2.4f);
                        float dither = (Hash(x * 7919 + y, 3) - 0.5f) / 255f * 2f;
                        return Mathf.Clamp01(fall + dither);
                    });
                }
                return eclipseCorona;
            }
        }

        /// <summary>A crisp disc filling the texture, antialiased across one texel.</summary>
        private static Sprite EclipseMoonSprite
        {
            get
            {
                if (eclipseMoon == null)
                {
                    eclipseMoon = Baked(EclipseTexels, delegate (float r, float theta, int x, int y)
                    {
                        return Mathf.Clamp01((0.5f - r) * EclipseTexels + 0.5f);
                    });
                }
                return eclipseMoon;
            }
        }

        /// <summary>A hot, thin ring on the limb with a tight glow round it.</summary>
        private static Sprite EclipseRimSprite
        {
            get
            {
                if (eclipseRim == null)
                {
                    eclipseRim = Baked(EclipseTexels, delegate (float r, float theta, int x, int y)
                    {
                        float d = r - RimLimb;
                        float core = Mathf.Exp(-Sq(d / 0.0035f));
                        float glow = 0.45f * Mathf.Exp(-Sq(d / 0.018f));
                        float bead = 0.8f + 0.2f * Mathf.Cos(11f * theta + 1.3f);
                        return Mathf.Clamp01((core + glow) * bead);
                    });
                }
                return eclipseRim;
            }
        }

        /// <summary>A white square texture whose alpha comes from (radius 0..0.5, angle, x, y).</summary>
        private static Sprite Baked(int n, Func<float, float, int, int, float> alpha)
        {
            Texture2D tex = NewTexture(n, n);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f;
                    float dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = alpha(r, Mathf.Atan2(dy, dx), x, y);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n);
        }

        // =================================================================== AURA

        private sealed class AuraRig
        {
            public SpriteRenderer Base;
            public SpriteRenderer[] Blobs;
        }

        private const int AuraBlobs = 44;

        private static AuraRig BuildAura(Transform parent)
        {
            var a = new AuraRig();
            a.Base = Part(parent, "AuraBase", GlowBox, AuraOrder);
            a.Blobs = new SpriteRenderer[AuraBlobs];
            for (int i = 0; i < AuraBlobs; i++)
            {
                a.Blobs[i] = Part(parent, "AuraBlob" + i, SoftDot, AuraOrder + 1);
            }
            return a;
        }

        /// <summary>Dark red flame-smoke licking off the arena's edges, behind the board, so the
        /// board itself reads as the thing giving off the menace.</summary>
        /// <param name="surge">Reaction energy: the flames leap further, bigger and hotter.</param>
        private static void PoseAura(AuraRig a, Rect r, float t, float w, float surge = 0f)
        {
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 1.4f);
            surge = Mathf.Min(1.5f, surge);
            Put(a.Base, r.center, r.size / GlowBoxFill * (1f + 0.05f * surge), 0f,
                Color.Lerp(Style.Maroon, Style.Threat, 0.4f * surge), Mathf.Clamp01((0.75f + 0.2f * breathe + 0.3f * surge) * w));
            float perimeter = 2f * (r.width + r.height);
            for (int i = 0; i < AuraBlobs; i++)
            {
                float s = (i + Hash(i, 5) * 0.8f) / AuraBlobs * perimeter;
                Vector2 p;
                Vector2 n;
                Vector2 tangent;
                if (s < r.width)
                {
                    p = new Vector2(r.xMin + s, r.yMin); n = Vector2.down; tangent = Vector2.right;
                }
                else if (s < r.width + r.height)
                {
                    p = new Vector2(r.xMax, r.yMin + s - r.width); n = Vector2.right; tangent = Vector2.up;
                }
                else if (s < 2f * r.width + r.height)
                {
                    p = new Vector2(r.xMax - (s - r.width - r.height), r.yMax); n = Vector2.up; tangent = Vector2.left;
                }
                else
                {
                    p = new Vector2(r.xMin, r.yMax - (s - 2f * r.width - r.height)); n = Vector2.left; tangent = Vector2.down;
                }
                float period = 1.8f + 1.4f * Hash(i, 7);
                float phase = Mathf.Repeat(t / period + Hash(i, 6), 1f);
                Vector2 at = p + n * (0.05f + phase * 0.85f * (1f + 0.7f * surge))
                    + tangent * Mathf.Sin(t * 0.9f + i * 1.7f) * 0.14f;
                float size = (0.5f + 0.85f * phase) * (0.8f + 0.5f * Hash(i, 8)) * (1f + 0.35f * surge);
                float alpha = Mathf.Pow(Mathf.Sin(phase * Mathf.PI), 1.5f) * 0.5f * (1f + 0.6f * surge) * w;
                Color c = Color.Lerp(new Color(Style.Threat.r * 0.8f, Style.Threat.g * 0.6f,
                    Style.Threat.b * 0.6f), Style.Maroon, phase);
                Put(a.Blobs[i], at, Vector2.one * size, 0f, c, alpha);
            }
        }

        // =================================================================== LETTERBOX

        private sealed class LetterboxRig
        {
            public SpriteRenderer Top;
            public SpriteRenderer Bottom;
            public SpriteRenderer HairTop;
            public SpriteRenderer HairBottom;
            public SpriteRenderer GlowTop;
            public SpriteRenderer GlowBottom;
        }

        private static LetterboxRig BuildLetterbox(Transform screen, int order)
        {
            return new LetterboxRig
            {
                GlowTop = Part(screen, "GlowTop", FadeUp, order),
                GlowBottom = Part(screen, "GlowBottom", FadeUp, order),
                Top = Part(screen, "BarTop", ViewUtil.WhiteSprite, order + 1),
                Bottom = Part(screen, "BarBottom", ViewUtil.WhiteSprite, order + 1),
                HairTop = Part(screen, "HairTop", ViewUtil.WhiteSprite, order + 2),
                HairBottom = Part(screen, "HairBottom", ViewUtil.WhiteSprite, order + 2)
            };
        }

        /// <summary>Black bars of <paramref name="barHeight"/> at the top and bottom of the screen,
        /// each with a red hairline on its inner edge and a faint red wash falling off it.</summary>
        private void PoseLetterbox(LetterboxRig l, float barHeight, float alpha, float hair)
        {
            float width = halfW * 2.6f;
            const float overscan = 1f;
            float inner = halfH - barHeight;
            Color black = new Color(0.01f, 0.005f, 0.008f);
            Put(l.Top, new Vector2(0f, inner + (barHeight + overscan) * 0.5f),
                new Vector2(width, barHeight + overscan), 0f, black, alpha);
            Put(l.Bottom, new Vector2(0f, -inner - (barHeight + overscan) * 0.5f),
                new Vector2(width, barHeight + overscan), 0f, black, alpha);
            float hairAlpha = alpha * Mathf.Clamp01(hair) * Mathf.Clamp01(barHeight / 0.08f);
            Put(l.HairTop, new Vector2(0f, inner), new Vector2(width, 0.022f), 0f, Style.Threat, hairAlpha);
            Put(l.HairBottom, new Vector2(0f, -inner), new Vector2(width, 0.022f), 0f, Style.Threat, hairAlpha);
            Put(l.GlowTop, new Vector2(0f, inner - 0.22f), new Vector2(width, 0.44f), 180f, Style.Threat, hairAlpha * 0.22f);
            Put(l.GlowBottom, new Vector2(0f, -inner + 0.22f), new Vector2(width, 0.44f), 0f, Style.Threat, hairAlpha * 0.22f);
        }
    }
}
