// PURPOSE: BossIdentityView's two WORLD looks - the arena is put somewhere else entirely for a
// boss stage, rather than being decorated:
//
//   ORBITAL   - the board is the SUN of a small system seen from above: space darkens the
//               backdrop, stars twinkle, two nebulae glow, and three orbits run all the way ROUND
//               the arena (never across it) with a planet on each and an asteroid belt between
//               the outer two. Intro "Gravity well": a hyperspace jump that drops out in a flash
//               and a shockwave off the board, then the orbits trace themselves round it one by
//               one while each planet spirals in and settles.
//   LAVA LAKE - the board floats on a lake of lava: churning glowing blobs, drifting dark crust
//               plates with hot edges, bubbles popping, a hot rim where the lava meets the arena
//               and a dark contact shadow under it. Intro "Eruption": the screen rumbles, the lava
//               rises from the bottom of the screen round the board and throws molten droplets.
//
// Both sit ABOVE the backdrop and the red atmosphere and BELOW the board, so a cell is never
// covered. Same rig pattern as BossIdentityView.Rigs: built once, posed from numbers.

using UnityEngine;

namespace ProjectBlock.View
{
    public sealed partial class BossIdentityView
    {
        private const int LavaBaseOrder = -70;
        private const int LavaBlobOrder = -69;
        private const int LavaHotOrder = -68;
        private const int LavaCrustGlowOrder = -67;
        private const int LavaCrustOrder = -66;
        private const int LavaBubbleOrder = -65;
        private const int LavaRimOrder = -43;
        private const int LavaShadowOrder = -41;
        private const int SpaceTintOrder = -207;
        private const int StarOrder = -206;

        private OrbitRig orbitRig;
        private LavaRig lavaRig;

        // =================================================================== ORBITAL
        //
        // The arena is the SUN of a small system seen from above: three orbits run all the way
        // ROUND it, never across it - each ellipse is sized so even its closest approach (the
        // diagonal, toward the board's corners) clears the arena - with an asteroid belt between
        // the outer two. Everything sits behind the board plate.

        private sealed class OrbitRing
        {
            public float A;
            public float B;
            public float Speed;
            public float Phase;
            public float BodySize;
            public Color BodyColour;
            public SpriteRenderer[] Segs;
            public SpriteRenderer Body;
            public SpriteRenderer Shade;
            public SpriteRenderer Glow;
        }

        private sealed class OrbitRig
        {
            public SpriteRenderer Tint;
            public SpriteRenderer Nebula;
            public SpriteRenderer NebulaB;
            public SpriteRenderer Core;
            public SpriteRenderer[] Stars;
            public SpriteRenderer[] Belt;
            public OrbitRing[] Rings;
        }

        private const int OrbitSegments = 110;
        private const int StarCount = 80;
        private const int BeltCount = 54;
        private const int OrbitOrder = -57;
        private const int NebulaOrder = -62;

        private static OrbitRig BuildOrbit(Transform world, Transform screen, Rect r)
        {
            var o = new OrbitRig();
            o.Tint = Part(screen, "SpaceTint", ViewUtil.WhiteSprite, SpaceTintOrder);
            o.Stars = Many(screen, "Star", SoftDot, StarOrder, StarCount);
            o.Nebula = Part(world, "Nebula", SoftDot, NebulaOrder);
            o.NebulaB = Part(world, "NebulaB", SoftDot, NebulaOrder);
            o.Core = Part(world, "StarCore", SoftDot, HaloOrder);
            o.Belt = Many(world, "Rock", Disc, OrbitOrder, BeltCount);
            float h = Mathf.Max(r.width, r.height) * 0.5f;
            o.Rings = new[]
            {
                MakeRing(world, 0, h * 1.85f, h * 1.22f, 16f, 0.6f, h * 0.10f, new Color(1f, 0.42f, 0.24f)),
                MakeRing(world, 1, h * 2.35f, h * 1.48f, -10f, 2.6f, h * 0.075f, new Color(0.92f, 0.86f, 0.80f)),
                MakeRing(world, 2, h * 3.05f, h * 1.86f, 6f, 4.5f, h * 0.14f, new Color(0.62f, 0.34f, 0.84f))
            };
            return o;
        }

        private static OrbitRing MakeRing(Transform parent, int index, float a, float b,
            float speed, float phase, float bodySize, Color colour)
        {
            var ring = new OrbitRing
            {
                A = a, B = b, Speed = speed, Phase = phase, BodySize = bodySize, BodyColour = colour
            };
            ring.Segs = Many(parent, "Orbit" + index + "_", ViewUtil.WhiteSprite, OrbitOrder, OrbitSegments);
            ring.Glow = Part(parent, "PlanetGlow" + index, SoftDot, OrbitOrder + 1);
            ring.Body = Part(parent, "Planet" + index, Disc, OrbitOrder + 2);
            ring.Shade = Part(parent, "PlanetShade" + index, Disc, OrbitOrder + 3);
            return ring;
        }

        private static Vector2 Ellipse(float a, float b, float theta)
        {
            return new Vector2(a * Mathf.Cos(theta), b * Mathf.Sin(theta));
        }

        /// <param name="draw">Per ring, how much of the orbit has been traced (null = all).</param>
        /// <param name="arrive">Per ring, how far its planet has spiralled in (null = in orbit).</param>
        private void PoseOrbit(OrbitRig o, float t, float alpha, float starAlpha, float[] draw,
            float[] arrive, float coreBoost)
        {
            Vector2 c = rect.center;
            float h = Mathf.Max(rect.width, rect.height) * 0.5f;
            Put(o.Tint, Vector2.zero, new Vector2(halfW, halfH) * 2.6f, 0f,
                new Color(0.015f, 0.008f, 0.04f), 0.72f * starAlpha);
            for (int i = 0; i < StarCount; i++)
            {
                var at = new Vector2((Hash(i, 60) - 0.5f) * halfW * 2f, (Hash(i, 61) - 0.5f) * halfH * 2f);
                float tw = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * (0.8f + 2f * Hash(i, 62)) + i), 3f);
                Put(o.Stars[i], at, Vector2.one * (0.03f + 0.07f * Mathf.Pow(Hash(i, 63), 2f)), 0f,
                    Color.Lerp(Style.Bone, new Color(0.75f, 0.8f, 1f), Hash(i, 64)), tw * 0.85f * starAlpha);
            }
            Put(o.Nebula, c + new Vector2(-h * 1.3f, h * 0.5f), new Vector2(h * 5f, h * 3.2f), 25f,
                new Color(0.55f, 0.12f, 0.45f), 0.20f * starAlpha);
            Put(o.NebulaB, c + new Vector2(h * 1.4f, -h * 0.6f), new Vector2(h * 4.4f, h * 2.6f), -20f,
                new Color(0.75f, 0.18f, 0.16f), 0.18f * starAlpha);
            Put(o.Core, c, Vector2.one * h * 4.4f, 0f, new Color(1f, 0.40f, 0.22f),
                Mathf.Clamp01(0.26f + 0.05f * Mathf.Sin(t * 1.2f) + 0.7f * coreBoost) * alpha);

            // Asteroid belt between the outer two orbits.
            OrbitRing inner = o.Rings[1];
            OrbitRing outer = o.Rings[2];
            float beltShow = draw != null ? draw[1] : 1f;
            for (int i = 0; i < BeltCount; i++)
            {
                float mix = 0.35f + 0.3f * Hash(i, 110);
                float theta = Hash(i, 111) * Mathf.PI * 2f + t * 0.05f * (0.7f + 0.6f * Hash(i, 112));
                Vector2 at = c + Ellipse(Mathf.Lerp(inner.A, outer.A, mix), Mathf.Lerp(inner.B, outer.B, mix), theta);
                Put(o.Belt[i], at, Vector2.one * (0.03f + 0.06f * Hash(i, 113)), 0f,
                    new Color(0.62f, 0.52f, 0.50f), 0.7f * alpha * beltShow);
            }

            for (int k = 0; k < o.Rings.Length; k++)
            {
                OrbitRing ring = o.Rings[k];
                float drawn = (draw != null ? draw[k] : 1f) * OrbitSegments;
                float planet = ring.Phase + ring.Speed * t * Mathf.Deg2Rad;
                float step = 2f * Mathf.PI / OrbitSegments;
                for (int i = 0; i < OrbitSegments; i++)
                {
                    SpriteRenderer seg = ring.Segs[i];
                    // Traced from the top, clockwise, like a pen going round.
                    float theta = Mathf.PI * 0.5f - step * i;
                    if (i >= drawn)
                    {
                        seg.color = Color.clear;
                        continue;
                    }
                    Vector2 at = c + Ellipse(ring.A, ring.B, theta);
                    Vector2 next = c + Ellipse(ring.A, ring.B, theta - step);
                    Vector2 d = next - at;
                    float behind = Mathf.Repeat((planet - theta) * Mathf.Sign(ring.Speed), 2f * Mathf.PI);
                    float trail = Mathf.Exp(-behind * 1.4f);
                    float head = drawn < OrbitSegments ? Mathf.Clamp01(1f - (drawn - i) / 8f) : 0f;
                    Color col = Color.Lerp(new Color(0.95f, 0.62f, 0.62f), ring.BodyColour, trail);
                    col = Color.Lerp(col, Style.Bone, head);
                    Put(seg, (at + next) * 0.5f, new Vector2(d.magnitude * 1.1f, 0.03f + 0.02f * head),
                        Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, col,
                        Mathf.Clamp01(0.22f + 0.65f * trail + head) * alpha);
                }
                // The planet spirals in from outside the screen and settles onto its orbit.
                float p = arrive != null ? arrive[k] : 1f;
                float spiralTheta = planet - (1f - p) * 2.5f * Mathf.PI * Mathf.Sign(ring.Speed);
                float spread = 1f + (1f - OutCubic(p)) * 2.4f;
                Vector2 pos = c + Ellipse(ring.A * spread, ring.B * spread, spiralTheta);
                float pa = Smooth(p * 3f) * alpha;
                float size = ring.BodySize;
                Put(ring.Glow, pos, Vector2.one * size * 5f, 0f, ring.BodyColour, 0.5f * pa);
                Put(ring.Body, pos, Vector2.one * size * 2f, 0f, ring.BodyColour, pa);
                // Night side away from the sun at the board's centre.
                Vector2 away = (pos - c).normalized * size * 0.6f;
                Put(ring.Shade, pos + away, Vector2.one * size * 1.75f, 0f, new Color(0.03f, 0.01f, 0.05f), 0.78f * pa);
            }
        }

        /// <summary>"Gravity well": a hyperspace jump that arrives at the arena. Streaks rush
        /// OUT past the camera, the jump drops out in a flash and a shockwave ringing off the
        /// board, space opens up, then the orbits trace themselves round the arena one by one
        /// while each planet spirals in and settles.</summary>
        private void StartOrbit(string name, string rule, string caption)
        {
            run.Duration = 3.8f;
            run.HandOver = 3.3f;
            run.HoldAt = 2.75f;
            SpriteRenderer veil = MakeVeil();
            OrbitRig rig = BuildOrbit(introWorld, introScreen, rect);
            run.CardIn = new Vector2(2.2f, 2.5f);
            run.CardOut = new Vector2(3.05f, 3.35f);
            const int streakCount = 84;
            var streaks = Many(introScreen, "Warp", ViewUtil.WhiteSprite, VeilOrder + 1, streakCount);
            SpriteRenderer tunnel = Part(introScreen, "Tunnel", EdgeGlow, VeilOrder + 2);
            SpriteRenderer shock = Part(introWorld, "Shockwave", SoftRing, FxOrder);
            SpriteRenderer flash = Part(introScreen, "Flash", ViewUtil.WhiteSprite, FxOrder + 1);
            TitleStack stack = MakeStack(name, rule, caption);
            const float drop = 1.15f;
            var draw = new float[3];
            var arrive = new float[3];

            CueSting(0f, BossSting.Drone);
            CueShake(0.1f, 0.035f, 1.0f);
            CueSting(drop, BossSting.Boom);
            CueSting(drop + 0.05f, BossSting.Gong);
            CueShake(drop + 0.02f, 0.22f, 0.45f);
            for (int k = 0; k < 3; k++)
            {
                CueSting(2.0f + 0.22f * k, BossSting.Clank);
            }

            run.Pose = delegate (float t)
            {
                SetHud(1f - Env(t, 0f, 0.35f, 3.1f, 3.6f));
                // Hyperspace: near-black, streaks accelerating OUTWARD from the arena.
                float jump = Env(t, 0f, 0.2f, drop - 0.05f, drop + 0.08f);
                PutVeil(veil, Mathf.Max(0.92f * jump, 0.25f * Env(t, drop, drop + 0.2f, 3.1f, 3.6f)));
                float accel = Seg(t, 0f, drop);
                for (int i = 0; i < streakCount; i++)
                {
                    float angle = Hash(i, 70) * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float lap = Mathf.Repeat(t * (0.5f + 1.8f * accel) * (0.7f + 0.6f * Hash(i, 71)) + Hash(i, 72), 1f);
                    float dist = Mathf.Lerp(0.6f, halfW * 1.3f, lap * lap);
                    float len = (0.05f + 2.4f * lap * lap) * (0.4f + accel);
                    Vector2 at = boardOnScreen + dir * (dist + len * 0.5f);
                    Color col = Color.Lerp(new Color(0.75f, 0.85f, 1f), new Color(1f, 0.55f, 0.5f), Hash(i, 73));
                    Put(streaks[i], at, new Vector2(len, 0.022f + 0.02f * lap), angle * Mathf.Rad2Deg, col,
                        jump * Mathf.Sin(lap * Mathf.PI));
                }
                Put(tunnel, Vector2.zero, new Vector2(halfW, halfH) * 2.02f, 0f, new Color(0.35f, 0.4f, 1f),
                    0.55f * jump * accel);

                // Drop out: a flash, and a shockwave ringing off the arena.
                Put(flash, Vector2.zero, new Vector2(halfW, halfH) * 2.6f, 0f, new Color(1f, 0.85f, 0.8f),
                    0.75f * Hit(t, drop, 6f));
                float wave = Seg(t, drop, drop + 0.8f);
                float half = Mathf.Max(rect.width, rect.height) * 0.5f;
                Put(shock, rect.center, Vector2.one * Mathf.Lerp(half * 1.5f, half * 5f, OutCubic(wave)) / 0.40f,
                    0f, new Color(1f, 0.6f, 0.45f), t >= drop ? (1f - wave) : 0f);

                float open = Smooth(Seg(t, drop, drop + 0.6f)) * (1f - Seg(t, 3.3f, 3.8f));
                for (int k = 0; k < 3; k++)
                {
                    draw[k] = Smooth(Seg(t, 1.3f + 0.22f * k, 1.9f + 0.22f * k));
                    arrive[k] = Seg(t, 1.35f + 0.22f * k, 2.0f + 0.22f * k);
                }
                PoseOrbit(rig, t, open, open, draw, arrive, Hit(t, drop, 2.5f));
                PutStack(stack, new Color(0.75f, 0.8f, 1f), t, 2.3f, 2.6f, 3.0f, 3.3f);
            };
        }

        // =================================================================== LAVA LAKE

        private sealed class LavaRig
        {
            public SpriteRenderer Base;
            public SpriteRenderer[] Blobs;
            public SpriteRenderer[] Hots;
            public SpriteRenderer[] Crust;
            public SpriteRenderer[] CrustGlow;
            public SpriteRenderer[] Bubbles;
            public SpriteRenderer[] BubbleCores;
            public SpriteRenderer Rim;
            public SpriteRenderer Shadow;
        }

        private const int LavaBlobs = 26;
        private const int LavaHots = 12;
        private const int LavaPlates = 18;
        private const int LavaBubbles = 9;

        private static readonly Color LavaDeep = new Color(0.50f, 0.05f, 0.02f);
        private static readonly Color LavaBright = new Color(1f, 0.38f, 0.05f);
        private static readonly Color LavaHot = new Color(1f, 0.72f, 0.22f);

        private static LavaRig BuildLava(Transform world, Transform screen)
        {
            var l = new LavaRig();
            l.Base = Part(screen, "LavaBase", ViewUtil.WhiteSprite, LavaBaseOrder);
            l.Blobs = Many(world, "LavaBlob", SoftDot, LavaBlobOrder, LavaBlobs);
            l.Hots = Many(world, "LavaHot", SoftDot, LavaHotOrder, LavaHots);
            l.CrustGlow = Many(world, "CrustGlow", SoftDot, LavaCrustGlowOrder, LavaPlates);
            l.Crust = Many(world, "Crust", ViewUtil.RoundedSprite, LavaCrustOrder, LavaPlates);
            l.Bubbles = Many(world, "Bubble", HardRing, LavaBubbleOrder, LavaBubbles);
            l.BubbleCores = Many(world, "BubbleCore", Disc, LavaBubbleOrder, LavaBubbles);
            l.Rim = Part(world, "LavaRim", GlowBox, LavaRimOrder);
            l.Shadow = Part(world, "LavaShadow", GlowBox, LavaShadowOrder);
            return l;
        }

        private static SpriteRenderer[] Many(Transform parent, string name, Sprite sprite, int order, int count)
        {
            var list = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = Part(parent, name + i, sprite, order);
            }
            return list;
        }

        /// <param name="rise">0 = the lake is a screen below where it belongs, 1 = in place.</param>
        private void PoseLava(LavaRig l, float t, float alpha, float rise)
        {
            float lift = -(1f - rise) * halfH * 2.3f;
            Vector2 c = rect.center;
            float w = halfW * 2.3f;
            float h = halfH * 2.3f;
            Put(l.Base, new Vector2(0f, lift), new Vector2(halfW, halfH) * 2.6f, 0f,
                new Color(0.30f, 0.04f, 0.02f), alpha);
            for (int i = 0; i < LavaBlobs; i++)
            {
                var at = new Vector2(c.x + (Hash(i, 80) - 0.5f) * w + Mathf.Sin(t * 0.13f + i) * 0.8f,
                    (Hash(i, 81) - 0.5f) * h + Mathf.Cos(t * 0.11f + i * 1.3f) * 0.6f + lift);
                float size = 2.2f + 2.5f * Hash(i, 82);
                Put(l.Blobs[i], at, Vector2.one * size, 0f, Color.Lerp(LavaDeep, LavaBright, Hash(i, 83)),
                    0.55f * (0.8f + 0.2f * Mathf.Sin(t * 0.7f + i)) * alpha);
            }
            for (int i = 0; i < LavaHots; i++)
            {
                var at = new Vector2(c.x + (Hash(i, 84) - 0.5f) * w + Mathf.Sin(t * 0.2f + i * 2f) * 0.5f,
                    (Hash(i, 85) - 0.5f) * h + Mathf.Sin(t * 0.17f + i) * 0.4f + lift);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * (0.9f + Hash(i, 86)) + i * 1.7f);
                Put(l.Hots[i], at, Vector2.one * (0.8f + 0.8f * Hash(i, 87)), 0f, LavaHot, 0.35f * pulse * alpha);
            }
            for (int i = 0; i < LavaPlates; i++)
            {
                float speed = 0.12f * (0.6f + 0.8f * Hash(i, 88));
                float x = Mathf.Repeat(Hash(i, 89) * w + t * speed, w) - w * 0.5f + c.x;
                float y = (Hash(i, 90) - 0.5f) * h + Mathf.Sin(t * 0.3f + i) * 0.15f + lift;
                var size = new Vector2(0.7f + 0.9f * Hash(i, 91), 0.35f + 0.5f * Hash(i, 92));
                float rot = Hash(i, 93) * 180f + t * 4f * (Hash(i, 94) - 0.5f);
                // Plates that drift under the arena are simply not seen there; fade at the wrap.
                float edge = Mathf.Min(Seg(x - c.x + w * 0.5f, 0f, 0.8f), Seg(c.x + w * 0.5f - x, 0f, 0.8f));
                Put(l.CrustGlow[i], new Vector2(x, y), size * 1.7f, rot, LavaBright, 0.35f * edge * alpha);
                Put(l.Crust[i], new Vector2(x, y), size, rot, new Color(0.10f, 0.03f, 0.02f), 0.88f * edge * alpha);
            }
            Rect keepOut = new Rect(rect.xMin - 0.5f, rect.yMin - 0.5f, rect.width + 1f, rect.height + 1f);
            for (int i = 0; i < LavaBubbles; i++)
            {
                float period = 1.6f + 1.6f * Hash(i, 95);
                float cycle = t / period + Hash(i, 96);
                float phase = Mathf.Repeat(cycle, 1f);
                int lap = Mathf.FloorToInt(cycle);
                var at = new Vector2(c.x + (Hash(i * 17 + lap, 97) - 0.5f) * halfW * 1.8f,
                    c.y + (Hash(i * 17 + lap, 98) - 0.5f) * halfH * 1.8f + lift);
                float a = keepOut.Contains(at) ? 0f : alpha;
                Put(l.Bubbles[i], at, Vector2.one * Mathf.Lerp(0.05f, 0.6f, OutCubic(phase)), 0f, LavaHot,
                    (1f - phase) * 0.7f * a);
                Put(l.BubbleCores[i], at, Vector2.one * 0.2f * (1f - phase), 0f, LavaBright, (1f - phase) * a);
            }
            float heat = 0.55f + 0.2f * Mathf.Sin(t * 1.7f);
            Vector2 anchor = rect.center + new Vector2(0f, lift);
            Put(l.Rim, anchor, (rect.size + Vector2.one * 0.4f) / GlowBoxFill, 0f, LavaHot, heat * alpha);
            Put(l.Shadow, anchor + new Vector2(0.06f, -0.1f), rect.size * 1.02f / GlowBoxFill, 0f,
                new Color(0.04f, 0.01f, 0.0f), 0.7f * alpha * rise);
        }

        private void StartEruption(string name, string rule, string caption)
        {
            run.Duration = 3.3f;
            run.HandOver = 2.85f;
            run.HoldAt = 2.3f;
            SpriteRenderer veil = MakeVeil();
            LavaRig rig = BuildLava(introWorld, introScreen);
            run.CardIn = new Vector2(1.65f, 1.95f);
            run.CardOut = new Vector2(2.55f, 2.9f);
            const int dropCount = 26;
            var drops = new SpriteRenderer[dropCount];
            for (int i = 0; i < dropCount; i++)
            {
                drops[i] = Part(introWorld, "Drop" + i, SoftDot, FxOrder);
            }
            TitleStack stack = MakeStack(name, rule, caption);
            CueSting(0f, BossSting.Drone);
            CueShake(0.05f, 0.05f, 0.5f);
            CueShake(0.65f, 0.08f, 0.45f);
            CueSting(1.35f, BossSting.Boom);
            CueShake(1.38f, 0.2f, 0.4f);
            const float burst = 1.38f;

            run.Pose = delegate (float t)
            {
                SetHud(1f - (1f - Style.HudDim) * Env(t, 0f, 0.3f, 2.6f, 3.1f));
                PutVeil(veil, 0.3f * Env(t, 0f, 0.3f, 2.6f, 3.1f));
                float handOver = 1f - Seg(t, 2.85f, 3.3f);
                PoseLava(rig, t, handOver, OutCubic(Seg(t, 0.2f, 1.4f)));
                // Molten droplets thrown off the arena's lower edges, then falling back.
                float tau = t - burst;
                for (int i = 0; i < dropCount; i++)
                {
                    if (tau < 0f || tau > 1.2f)
                    {
                        drops[i].color = Color.clear;
                        continue;
                    }
                    float along = Hash(i, 100);
                    bool side = Hash(i, 101) < 0.5f;
                    Vector2 start;
                    Vector2 normal;
                    if (side)
                    {
                        float sx = along < 0.5f ? -1f : 1f;
                        start = new Vector2(sx < 0f ? rect.xMin : rect.xMax, rect.yMin + rect.height * Hash(i, 102) * 0.6f);
                        normal = new Vector2(sx, 0f);
                    }
                    else
                    {
                        start = new Vector2(rect.xMin + rect.width * along, rect.yMin);
                        normal = new Vector2((along - 0.5f) * 0.8f, -0.2f);
                    }
                    Vector2 v = normal * (2.5f + 2f * Hash(i, 103)) + Vector2.up * (3f + 2.5f * Hash(i, 104));
                    Vector2 at = start + v * tau + new Vector2(0f, -7f) * tau * tau * 0.5f;
                    float life = tau / 1.2f;
                    Put(drops[i], at, Vector2.one * Mathf.Lerp(0.2f, 0.06f, life), 0f,
                        Color.Lerp(LavaHot, LavaBright, life), 1f - life);
                }
                PutStack(stack, LavaBright, t, 1.75f, 2.05f, 2.5f, 2.85f);
            };
        }
    }
}
