// PURPOSE: BossIdentityView's two WORLD looks - the arena is put somewhere else entirely for a
// boss stage, rather than being decorated:
//
//   ORBITAL   - the board is the SUN of a small system seen from above: space darkens the
//               backdrop, stars twinkle, two nebulae glow, and three orbits run all the way ROUND
//               the arena (never across it) with a planet on each and an asteroid belt between
//               the outer two. Intro "Gravity well": a hyperspace jump that drops out in a flash
//               and a shockwave off the board, then the orbits trace themselves round it one by
//               one while each planet spirals in and settles, silently.
//   LAVA LAKE - the board floats on a lake of lava: churning glowing blobs, drifting dark crust
//               plates with hot edges, bubbles popping, a hot rim where the lava meets the arena
//               and a dark contact shadow under it. Intro "Eruption": the lava SEEPS OUT from
//               under the arena and spreads across the screen along a ragged, sizzling front
//               (steam and crackles where it meets the ground), then throws molten droplets.
//
// ONE WORLD CLOCK. The intro and the ambience build SEPARATE rigs and crossfade between them, so
// both must put every planet, lava blob, crust plate and bubble in the same place on the same
// frame - otherwise the handover shows six planets, or the lake's pieces jump. Both pose from `orbitClock`, which runs regardless of the intro
// (a title card held for a press does not stop the planets), never from their own timelines.
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
        private const int LavaSteamOrder = -64;
        private const int LavaRimOrder = -43;
        private const int LavaShadowOrder = -41;
        private const int SpaceTintOrder = -207;
        private const int StarOrder = -206;

        private OrbitRig orbitRig;
        private LavaRig lavaRig;

        /// <summary>The one clock every orbit rig moves its planets by (see the header).</summary>
        private float orbitClock;

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
            public SpriteRenderer Glow;

            /// <summary>The gas giant's ring, split so the planet sits INSIDE it: the far half
            /// under the body, the near half over it. Null on the other planets.</summary>
            public SpriteRenderer RingBack;

            public SpriteRenderer RingFront;
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
                MakeRing(world, 0, h * 1.85f, h * 1.22f, 16f, 0.6f, h * 0.085f, new Color(1f, 0.48f, 0.28f)),
                MakeRing(world, 1, h * 2.35f, h * 1.48f, -10f, 2.6f, h * 0.070f, new Color(0.70f, 0.82f, 0.95f)),
                MakeRing(world, 2, h * 3.05f, h * 1.86f, 6f, 4.5f, h * 0.13f, new Color(0.72f, 0.48f, 0.90f))
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
            if (index == 2)
            {
                ring.RingBack = Part(parent, "PlanetRingBack" + index, PlanetRingSprite(false), OrbitOrder + 2);
                ring.RingFront = Part(parent, "PlanetRingFront" + index, PlanetRingSprite(true), OrbitOrder + 4);
            }
            ring.Body = Part(parent, "Planet" + index, PlanetSprite(index), OrbitOrder + 3);
            return ring;
        }

        private static Vector2 Ellipse(float a, float b, float theta)
        {
            return new Vector2(a * Mathf.Cos(theta), b * Mathf.Sin(theta));
        }

        /// <param name="t">The orbit clock: planets, belt, stars and the trails all move by it.</param>
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
                // Stars are on the screen root; the front is in world space.
                float glint = Mathf.Min(1f, Wave(at + rect.center - boardOnScreen));
                Put(o.Stars[i], at, Vector2.one * (0.03f + 0.07f * Mathf.Pow(Hash(i, 63), 2f)) * (1f + 1.2f * glint), 0f,
                    Color.Lerp(Style.Bone, new Color(0.75f, 0.8f, 1f), Hash(i, 64)),
                    Mathf.Clamp01(tw * 0.85f + glint) * starAlpha);
            }
            Put(o.Nebula, c + new Vector2(-h * 1.3f, h * 0.5f), new Vector2(h * 5f, h * 3.2f), 25f,
                new Color(0.55f, 0.12f, 0.45f), 0.20f * starAlpha);
            Put(o.NebulaB, c + new Vector2(h * 1.4f, -h * 0.6f), new Vector2(h * 4.4f, h * 2.6f), -20f,
                new Color(0.75f, 0.18f, 0.16f), 0.18f * starAlpha);
            Put(o.Core, c, Vector2.one * h * 4.4f, 0f, new Color(1f, 0.40f, 0.22f),
                Mathf.Clamp01(0.26f + 0.05f * Mathf.Sin(t * 1.2f) + 0.7f * coreBoost + 0.35f * reactEnergy) * alpha);

            // Asteroid belt between the outer two orbits: small, dim, irregular grit - never big or
            // bright enough to be read as more planets.
            OrbitRing inner = o.Rings[1];
            OrbitRing outer = o.Rings[2];
            float beltShow = draw != null ? draw[1] : 1f;
            for (int i = 0; i < BeltCount; i++)
            {
                float mix = 0.35f + 0.3f * Hash(i, 110);
                float theta = Hash(i, 111) * Mathf.PI * 2f + t * 0.05f * (0.7f + 0.6f * Hash(i, 112));
                Vector2 at = c + Ellipse(Mathf.Lerp(inner.A, outer.A, mix), Mathf.Lerp(inner.B, outer.B, mix), theta);
                at += WavePush(at) * 0.3f;
                float s = 0.02f + 0.035f * Hash(i, 113);
                Put(o.Belt[i], at, new Vector2(s, s * (0.6f + 0.4f * Hash(i, 114))), Hash(i, 115) * 180f,
                    new Color(0.52f, 0.45f, 0.44f), 0.5f * alpha * beltShow);
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
                    float theta = Mathf.PI * 0.5f - step * i;
                    // How far BEHIND the planet this segment is, along its direction of travel.
                    float behind = Mathf.Repeat((planet - theta) * Mathf.Sign(ring.Speed), 2f * Mathf.PI);
                    // The orbit unrolls out from BEHIND the planet, so a line is never drawn ahead of
                    // it - whichever way that planet goes round.
                    float reached = behind / (2f * Mathf.PI) * OrbitSegments;
                    if (reached >= drawn)
                    {
                        seg.color = Color.clear;
                        continue;
                    }
                    Vector2 at = c + Ellipse(ring.A, ring.B, theta);
                    Vector2 next = c + Ellipse(ring.A, ring.B, theta - step);
                    Vector2 d = next - at;
                    // One neutral colour all the way round: a planet never tints its orbit (a
                    // coloured trail read as the line being painted ahead of it). The only thing
                    // that lights the line is a pressure front passing along it.
                    float head = drawn < OrbitSegments ? Mathf.Clamp01(1f - (drawn - reached) / 8f) : 0f;
                    float pulse = Mathf.Min(1f, Wave(at));
                    Color col = Color.Lerp(new Color(0.95f, 0.66f, 0.66f), Style.Bone, Mathf.Max(head, pulse * 0.7f));
                    Put(seg, (at + next) * 0.5f, new Vector2(d.magnitude * 1.1f, 0.03f + 0.02f * Mathf.Max(head, pulse)),
                        Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, col,
                        Mathf.Clamp01(0.38f + head + 0.55f * pulse) * alpha);
                }
                // The planet spirals in from outside the screen and settles onto its orbit.
                float p = arrive != null ? arrive[k] : 1f;
                float spiralTheta = planet - (1f - p) * 2.5f * Mathf.PI * Mathf.Sign(ring.Speed);
                float spread = 1f + (1f - OutCubic(p)) * 2.4f;
                Vector2 pos = c + Ellipse(ring.A * spread, ring.B * spread, spiralTheta);
                // A front nudges the planet off its orbit for a moment, and it springs back.
                pos += WavePush(pos) * 0.22f;
                float pa = Smooth(p * 3f) * alpha;
                float size = ring.BodySize * 2f;
                // The baked planet is lit from its local +x, so it is turned to face the sun.
                Vector2 toSun = c - pos;
                float facing = Mathf.Atan2(toSun.y, toSun.x) * Mathf.Rad2Deg;
                Put(ring.Glow, pos, Vector2.one * size * 1.9f, 0f, ring.BodyColour, 0.28f * pa);
                Put(ring.Body, pos, Vector2.one * size, facing, Color.white, pa);
                if (ring.RingBack != null)
                {
                    // A fixed tilt: a ring keeps its orientation in space as the planet goes round.
                    var ringSize = new Vector2(size * 2.3f, size * 0.78f);
                    Put(ring.RingBack, pos, ringSize, -16f, Color.white, pa);
                    Put(ring.RingFront, pos, ringSize, -16f, Color.white, pa);
                }
            }
        }

        // ---- planet art (baked once, lit from local +x) ----

        private static readonly Sprite[] planetSprites = new Sprite[3];
        private static readonly Sprite[] planetRingSprites = new Sprite[2];

        /// <summary>Smooth value noise in 0..1 over a lattice of Hash values.</summary>
        private static float ValueNoise(float x, float y, int salt)
        {
            int xi = Mathf.FloorToInt(x);
            int yi = Mathf.FloorToInt(y);
            float fx = x - xi;
            float fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi * 7919 + yi * 104729, salt);
            float b = Hash((xi + 1) * 7919 + yi * 104729, salt);
            float cc = Hash(xi * 7919 + (yi + 1) * 104729, salt);
            float d = Hash((xi + 1) * 7919 + (yi + 1) * 104729, salt);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(cc, d, fx), fy);
        }

        private static float Fbm(float x, float y, int salt)
        {
            return 0.55f * ValueNoise(x, y, salt) + 0.3f * ValueNoise(x * 2.1f, y * 2.1f, salt + 1)
                + 0.15f * ValueNoise(x * 4.3f, y * 4.3f, salt + 2);
        }

        /// <summary>0: a molten world (dark basalt with glowing fissures), 1: an ice world (pale
        /// blue-white with soft craters and a cold haze), 2: a banded violet gas giant. Each is a
        /// shaded sphere lit from +x, with limb darkening and a thin atmosphere on the lit rim.</summary>
        private static Sprite PlanetSprite(int kind)
        {
            if (planetSprites[kind] != null)
            {
                return planetSprites[kind];
            }
            const int n = 128;
            Texture2D tex = NewTexture(n, n);
            var px = new Color[n * n];
            var light = new Vector3(0.78f, 0.22f, 0.58f).normalized;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = ((x + 0.5f) / n - 0.5f) * 2f;
                    float dy = ((y + 0.5f) / n - 0.5f) * 2f;
                    float r2 = dx * dx + dy * dy;
                    const float radius = 0.86f;
                    float rr = Mathf.Sqrt(r2) / radius;
                    // A thin atmospheric halo past the surface, strongest on the lit side.
                    if (rr > 1f)
                    {
                        float haze = Mathf.Clamp01(1f - (rr - 1f) / 0.14f);
                        float litSide = Mathf.Clamp01(0.25f + 0.75f * (dx / Mathf.Max(0.001f, Mathf.Sqrt(r2))));
                        Color hc = kind == 0 ? new Color(1f, 0.55f, 0.3f) : kind == 1 ? new Color(0.7f, 0.88f, 1f) : new Color(0.85f, 0.65f, 1f);
                        px[y * n + x] = new Color(hc.r, hc.g, hc.b, haze * haze * litSide * 0.55f);
                        continue;
                    }
                    float nx = dx / radius;
                    float ny = dy / radius;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - nx * nx - ny * ny));
                    float lambert = Mathf.Max(0f, nx * light.x + ny * light.y + nz * light.z);
                    float terminator = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lambert * 1.6f));
                    float shade = 0.06f + 0.94f * Mathf.Pow(lambert, 0.85f);
                    // Surface coordinates that curve with the sphere.
                    float u = nx / (0.6f + nz * 0.4f);
                    float v = ny / (0.6f + nz * 0.4f);
                    Color surface;
                    Color emissive = Color.black;
                    switch (kind)
                    {
                        case 0:
                        {
                            float f = Fbm(u * 2.4f + 3f, v * 2.4f + 7f, 300);
                            Color basalt = Color.Lerp(new Color(0.22f, 0.09f, 0.06f), new Color(0.62f, 0.27f, 0.14f), f);
                            float crack = Mathf.Abs(Fbm(u * 3.2f + 11f, v * 3.2f, 310) - 0.5f);
                            float fissure = Mathf.Clamp01(1f - crack / 0.045f);
                            surface = basalt;
                            // Fissures glow on the NIGHT side too - that is what makes it molten.
                            emissive = new Color(1f, 0.45f, 0.12f) * (fissure * fissure * 0.9f);
                            break;
                        }
                        case 1:
                        {
                            float f = Fbm(u * 2f + 5f, v * 2f + 1f, 320);
                            surface = Color.Lerp(new Color(0.48f, 0.62f, 0.78f), new Color(0.90f, 0.95f, 1f), f);
                            for (int k = 0; k < 5; k++)
                            {
                                float cx = (Hash(k, 330) - 0.5f) * 1.3f;
                                float cy = (Hash(k, 331) - 0.5f) * 1.3f;
                                float cr = 0.12f + 0.18f * Hash(k, 332);
                                float dist = Mathf.Sqrt(Sq(u - cx) + Sq(v - cy)) / cr;
                                if (dist < 1.15f)
                                {
                                    float rim = Mathf.Clamp01(1f - Mathf.Abs(dist - 1f) / 0.15f);
                                    float bowl = Mathf.Clamp01(1f - dist);
                                    surface = Color.Lerp(surface, surface * 0.72f, bowl * 0.6f);
                                    surface = Color.Lerp(surface, Color.white, rim * 0.25f);
                                }
                            }
                            break;
                        }
                        default:
                        {
                            float warp = Fbm(u * 1.5f, v * 1.5f, 340) * 0.35f;
                            float band = Mathf.Sin((v + warp) * 9f) * 0.5f + 0.5f;
                            float fine = Mathf.Sin((v + warp * 1.6f) * 23f) * 0.5f + 0.5f;
                            surface = Color.Lerp(new Color(0.34f, 0.18f, 0.48f), new Color(0.86f, 0.66f, 0.92f), band);
                            surface = Color.Lerp(surface, new Color(0.98f, 0.82f, 0.70f), fine * band * 0.25f);
                            // A storm eye.
                            float eye = Mathf.Sqrt(Sq((u - 0.25f) / 0.22f) + Sq((v + 0.32f) / 0.12f));
                            surface = Color.Lerp(surface, new Color(0.95f, 0.55f, 0.45f), Mathf.Clamp01(1f - eye) * 0.7f);
                            break;
                        }
                    }
                    // Limb darkening, and a thin bright atmosphere just inside the lit limb.
                    float limb = Mathf.Lerp(0.55f, 1f, Mathf.Pow(nz, 0.5f));
                    float atmos = Mathf.Pow(1f - nz, 3f) * terminator;
                    Color lit = surface * shade * limb;
                    Color atmosColour = kind == 0 ? new Color(1f, 0.6f, 0.35f) : kind == 1 ? new Color(0.75f, 0.9f, 1f) : new Color(0.9f, 0.72f, 1f);
                    lit += atmosColour * atmos * 0.55f + emissive * (1f - 0.5f * terminator);
                    float edge = Mathf.Clamp01((1f - rr) * n * 0.45f);
                    px[y * n + x] = new Color(Mathf.Clamp01(lit.r), Mathf.Clamp01(lit.g), Mathf.Clamp01(lit.b), edge);
                }
            }
            planetSprites[kind] = Finish(tex, px);
            return planetSprites[kind];
        }

        /// <summary>A gas giant's ring as a thin banded annulus; <paramref name="front"/> keeps the
        /// near (lower) half, otherwise the far half. Squashed to an ellipse where it is placed.</summary>
        private static Sprite PlanetRingSprite(bool front)
        {
            int slot = front ? 1 : 0;
            if (planetRingSprites[slot] != null)
            {
                return planetRingSprites[slot];
            }
            const int n = 256;
            Texture2D tex = NewTexture(n, n);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f;
                    float dy = (y + 0.5f) / n - 0.5f;
                    bool lower = dy < 0f;
                    if (lower != front)
                    {
                        px[y * n + x] = Color.clear;
                        continue;
                    }
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01((r - 0.52f) * 60f) * Mathf.Clamp01((0.98f - r) * 60f);
                    float bands = 0.55f + 0.45f * Mathf.Sin(r * 70f) * Mathf.Sin(r * 23f);
                    float gap = Mathf.Clamp01(Mathf.Abs(r - 0.80f) / 0.025f);
                    // The near half is lit, the far half falls a little into the planet's shadow.
                    float lightness = front ? 1f : 0.7f;
                    Color col = Color.Lerp(new Color(0.55f, 0.42f, 0.62f), new Color(0.96f, 0.86f, 0.80f), bands) * lightness;
                    px[y * n + x] = new Color(col.r, col.g, col.b, a * gap * (0.45f + 0.4f * bands));
                }
            }
            planetRingSprites[slot] = Finish(tex, px);
            return planetRingSprites[slot];
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
            // The planets settle in silence - a sound per planet did not fit.

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
                // The shared orbit clock, not t: the ambience's rig takes over on this same clock.
                PoseOrbit(rig, orbitClock, open, open, draw, arrive, Hit(t, drop, 2.5f));
                PutStack(stack, new Color(0.75f, 0.8f, 1f), t, 2.3f, 2.6f, 3.0f, 3.3f);
            };
        }

        // =================================================================== LAVA LAKE

        private sealed class LavaRig
        {
            public SpriteRenderer Base;
            public SpriteRenderer[] Pools;
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
        private const int LavaPools = 22;
        private const int LavaHots = 12;
        private const int LavaPlates = 18;
        private const int LavaBubbles = 9;

        private static readonly Color LavaDeep = new Color(0.50f, 0.05f, 0.02f);
        private static readonly Color LavaBright = new Color(1f, 0.38f, 0.05f);
        private static readonly Color LavaHot = new Color(1f, 0.72f, 0.22f);
        private static readonly Color LavaBaseColour = new Color(0.30f, 0.04f, 0.02f);

        private static LavaRig BuildLava(Transform world, Transform screen)
        {
            var l = new LavaRig();
            l.Base = Part(screen, "LavaBase", ViewUtil.WhiteSprite, LavaBaseOrder);
            l.Pools = Many(world, "LavaPool", Disc, LavaBaseOrder, LavaPools);
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

        /// <summary>How far past the arena's edge a point is (0 on or inside it).</summary>
        private float OutsideArena(Vector2 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(rect.xMin - p.x, p.x - rect.xMax));
            float dy = Mathf.Max(0f, Mathf.Max(rect.yMin - p.y, p.y - rect.yMax));
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>The furthest any lake piece can be from the arena's edge.</summary>
        private float LavaReachMax
        {
            get { return new Vector2(halfW, halfH).magnitude * 1.6f + 1f; }
        }

        /// <summary>Where the spreading front is past the arena's edge in the direction of
        /// <paramref name="p"/>: ragged (a few slow lobes round the board), so it never reads as a
        /// growing circle or a wipe.</summary>
        private float LavaFront(Vector2 p, float spread, float t)
        {
            Vector2 d = p - rect.center;
            float angle = Mathf.Atan2(d.y, d.x);
            float lobes = 0.55f * Mathf.Sin(angle * 3f + 1.3f) + 0.35f * Mathf.Sin(angle * 5f - 0.7f + t * 0.6f)
                + 0.25f * Mathf.Sin(angle * 8f + 2.1f);
            float reach = Mathf.Lerp(-1.2f, LavaReachMax + 1.5f, spread);
            return reach * (1f + 0.22f * lobes * (1f - spread));
        }

        /// <summary>0..1: how much of a lake piece at <paramref name="p"/> has flowed in yet.</summary>
        private float LavaGrowth(Vector2 p, float spread, float t, float lag)
        {
            if (spread >= 1f)
            {
                return 1f;
            }
            return Smooth(Seg(LavaFront(p, spread, t) - OutsideArena(p) - lag, 0f, 1.1f));
        }

        /// <param name="spread">0 = no lava yet, 1 = the lake covers the screen. The lake does not
        /// slide in: it seeps out from under the arena and spreads outward from there.</param>
        private void PoseLava(LavaRig l, float t, float alpha, float spread)
        {
            Vector2 c = rect.center;
            float w = halfW * 2.3f;
            float h = halfH * 2.3f;
            // The flat base only completes once the front is well out, filling the gaps behind it.
            Put(l.Base, Vector2.zero, new Vector2(halfW, halfH) * 2.6f, 0f, LavaBaseColour,
                alpha * Smooth(Seg(spread, 0.55f, 1f)));
            // Hard-edged pools lay the molten ground down along the front, ahead of the base.
            for (int i = 0; i < LavaPools; i++)
            {
                float angle = Hash(i, 120) * Mathf.PI * 2f;
                float away = Mathf.Pow(Hash(i, 121), 0.8f) * LavaReachMax * 0.8f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 edge = c + new Vector2(dir.x * rect.width, dir.y * rect.height) * 0.5f;
                Vector2 at = edge + dir * away;
                float g = LavaGrowth(at, spread, t, 0f);
                float size = (1.8f + 2.4f * Hash(i, 122)) * (0.25f + 0.75f * OutCubic(g));
                Put(l.Pools[i], at, new Vector2(size, size * (0.75f + 0.25f * Hash(i, 123))), Hash(i, 124) * 180f,
                    LavaBaseColour, alpha * g);
            }
            for (int i = 0; i < LavaBlobs; i++)
            {
                var at = new Vector2(c.x + (Hash(i, 80) - 0.5f) * w + Mathf.Sin(t * 0.13f + i) * 0.8f,
                    (Hash(i, 81) - 0.5f) * h + Mathf.Cos(t * 0.11f + i * 1.3f) * 0.6f);
                float g = LavaGrowth(at, spread, t, 0.2f);
                // A front heaves the molten mass: it swells, brightens and is pushed outward.
                float heave = Mathf.Min(1f, Wave(at));
                at += WavePush(at) * 0.35f;
                float size = (2.2f + 2.5f * Hash(i, 82)) * (0.35f + 0.65f * g) * (1f + 0.12f * heave);
                Color blob = Color.Lerp(Color.Lerp(LavaDeep, LavaBright, Hash(i, 83)), LavaHot, 0.5f * heave);
                Put(l.Blobs[i], at, Vector2.one * size, 0f, blob,
                    Mathf.Clamp01(0.55f * (0.8f + 0.2f * Mathf.Sin(t * 0.7f + i)) + 0.3f * heave) * alpha * g);
            }
            for (int i = 0; i < LavaHots; i++)
            {
                var at = new Vector2(c.x + (Hash(i, 84) - 0.5f) * w + Mathf.Sin(t * 0.2f + i * 2f) * 0.5f,
                    (Hash(i, 85) - 0.5f) * h + Mathf.Sin(t * 0.17f + i) * 0.4f);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * (0.9f + Hash(i, 86)) + i * 1.7f);
                float g = LavaGrowth(at, spread, t, 0.4f);
                float flare = Mathf.Min(1.2f, Wave(at));
                Put(l.Hots[i], at, Vector2.one * (0.8f + 0.8f * Hash(i, 87)) * (1f + 0.5f * flare), 0f, LavaHot,
                    Mathf.Clamp01(0.35f * pulse + 0.5f * flare) * alpha * g);
            }
            for (int i = 0; i < LavaPlates; i++)
            {
                float speed = 0.12f * (0.6f + 0.8f * Hash(i, 88));
                float x = Mathf.Repeat(Hash(i, 89) * w + t * speed, w) - w * 0.5f + c.x;
                float y = (Hash(i, 90) - 0.5f) * h + Mathf.Sin(t * 0.3f + i) * 0.15f;
                var size = new Vector2(0.7f + 0.9f * Hash(i, 91), 0.35f + 0.5f * Hash(i, 92));
                float rot = Hash(i, 93) * 180f + t * 4f * (Hash(i, 94) - 0.5f);
                // Plates that drift under the arena are simply not seen there; fade at the wrap.
                float edge = Mathf.Min(Seg(x - c.x + w * 0.5f, 0f, 0.8f), Seg(c.x + w * 0.5f - x, 0f, 0.8f));
                // Crust forms only once the lava has been there a while - well behind the front.
                float g = LavaGrowth(new Vector2(x, y), spread, t, 1.2f);
                // Plates ride the front outward and their seams glow hotter as it lifts them.
                var plate = new Vector2(x, y);
                float lift = Mathf.Min(1f, Wave(plate));
                plate += WavePush(plate) * 0.45f;
                rot += 12f * lift * (Hash(i, 125) - 0.5f);
                Put(l.CrustGlow[i], plate, size * 1.7f * (1f + 0.25f * lift), rot, LavaBright,
                    Mathf.Clamp01(0.35f + 0.5f * lift) * edge * alpha * g);
                Put(l.Crust[i], plate, size * (0.4f + 0.6f * g), rot, new Color(0.10f, 0.03f, 0.02f),
                    0.88f * edge * alpha * g);
            }
            Rect keepOut = new Rect(rect.xMin - 0.5f, rect.yMin - 0.5f, rect.width + 1f, rect.height + 1f);
            for (int i = 0; i < LavaBubbles; i++)
            {
                float period = 1.6f + 1.6f * Hash(i, 95);
                float cycle = t / period + Hash(i, 96);
                float phase = Mathf.Repeat(cycle, 1f);
                int lap = Mathf.FloorToInt(cycle);
                var at = new Vector2(c.x + (Hash(i * 17 + lap, 97) - 0.5f) * halfW * 1.8f,
                    c.y + (Hash(i * 17 + lap, 98) - 0.5f) * halfH * 1.8f);
                float a = keepOut.Contains(at) ? 0f : alpha * LavaGrowth(at, spread, t, 0.6f);
                Put(l.Bubbles[i], at, Vector2.one * Mathf.Lerp(0.05f, 0.6f, OutCubic(phase)), 0f, LavaHot,
                    (1f - phase) * 0.7f * a);
                Put(l.BubbleCores[i], at, Vector2.one * 0.2f * (1f - phase), 0f, LavaBright, (1f - phase) * a);
            }
            // The rim comes up first and hottest: the lava is squeezing out from under the arena.
            float seep = Smooth(Seg(spread, 0f, 0.12f));
            float heat = 0.55f + 0.2f * Mathf.Sin(t * 1.7f) + 0.35f * seep * (1f - Seg(spread, 0.3f, 0.9f))
                + 0.45f * reactEnergy;
            Put(l.Rim, c, (rect.size + Vector2.one * (0.4f + 0.3f * (1f - seep))) / GlowBoxFill, 0f, LavaHot,
                Mathf.Clamp01(heat) * alpha * seep);
            Put(l.Shadow, c + new Vector2(0.06f, -0.1f), rect.size * 1.02f / GlowBoxFill, 0f,
                new Color(0.04f, 0.01f, 0.0f), 0.7f * alpha * seep);
        }

        private void StartEruption(string name, string rule, string caption)
        {
            // Paced so the lake is SEEN filling up: ~2.15s of spreading before the eruption.
            run.Duration = 4.25f;
            run.HandOver = 3.8f;
            run.HoldAt = 3.25f;
            SpriteRenderer veil = MakeVeil();
            LavaRig rig = BuildLava(introWorld, introScreen);
            run.CardIn = new Vector2(2.6f, 2.9f);
            run.CardOut = new Vector2(3.5f, 3.85f);
            const int dropCount = 26;
            var drops = new SpriteRenderer[dropCount];
            for (int i = 0; i < dropCount; i++)
            {
                drops[i] = Part(introWorld, "Drop" + i, SoftDot, FxOrder);
            }
            // Sizzle along the spreading front: steam puffs rising off it and hot crackles.
            const int steamCount = 34;
            const int crackleCount = 40;
            var steam = Many(introWorld, "Steam", SoftDot, LavaSteamOrder, steamCount);
            var crackles = Many(introWorld, "Crackle", SoftDot, LavaSteamOrder + 1, crackleCount);
            TitleStack stack = MakeStack(name, rule, caption);
            CueSting(0f, BossSting.Drone);
            // One long molten roar under the whole spread, not a string of hisses.
            CueSting(0.1f, BossSting.Magma);
            CueShake(0.05f, 0.04f, 0.8f);
            CueShake(0.95f, 0.06f, 0.6f);
            CueShake(1.7f, 0.08f, 0.5f);
            CueSting(2.35f, BossSting.Boom);
            CueShake(2.38f, 0.2f, 0.4f);
            const float burst = 2.38f;
            const float spreadFrom = 0.15f;
            const float spreadTo = 2.3f;

            run.Pose = delegate (float t)
            {
                SetHud(1f - (1f - Style.HudDim) * Env(t, 0f, 0.3f, 3.55f, 4.05f));
                PutVeil(veil, 0.3f * Env(t, 0f, 0.3f, 3.55f, 4.05f));
                float handOver = 1f - Seg(t, 3.8f, 4.25f);
                // Slow seep first, then it floods outward.
                float spread = Mathf.Pow(Seg(t, spreadFrom, spreadTo), 1.35f);
                // The shared clock, not t, so the lake's blobs, plates and bubbles are exactly where
                // the background's are when it takes over. The choreography stays on t.
                PoseLava(rig, orbitClock, handOver, spread);

                // Steam and crackles live AT the front, so they travel out with it and die with it.
                float frontLife = Env(t, spreadFrom, spreadFrom + 0.15f, spreadTo - 0.25f, spreadTo + 0.3f);
                Vector2 c = rect.center;
                for (int i = 0; i < steamCount; i++)
                {
                    float angle = Hash(i, 130) * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    float period = 0.55f + 0.5f * Hash(i, 131);
                    float cycle = t / period + Hash(i, 132);
                    float life = Mathf.Repeat(cycle, 1f);
                    int lap = Mathf.FloorToInt(cycle);
                    float wobble = (Hash(i * 31 + lap, 133) - 0.5f) * 0.5f;
                    var sideways = new Vector2(-dir.y, dir.x);
                    Vector2 edge = c + new Vector2(dir.x * rect.width, dir.y * rect.height) * 0.5f;
                    Vector2 born = edge + dir * Mathf.Max(0f, LavaFront(edge + dir, spread, t) - 0.2f)
                        + sideways * wobble;
                    Vector2 at = born + Vector2.up * life * 0.9f + sideways * Mathf.Sin(life * 5f + i) * 0.08f;
                    float size = Mathf.Lerp(0.35f, 1.3f, life) * (0.7f + 0.6f * Hash(i, 134));
                    Put(steam[i], at, Vector2.one * size, 0f, new Color(0.78f, 0.66f, 0.60f),
                        0.28f * Mathf.Sin(life * Mathf.PI) * frontLife * handOver);
                }
                for (int i = 0; i < crackleCount; i++)
                {
                    float period = 0.18f + 0.25f * Hash(i, 140);
                    float cycle = t / period + Hash(i, 141);
                    float life = Mathf.Repeat(cycle, 1f);
                    int lap = Mathf.FloorToInt(cycle);
                    float angle = Hash(i * 13 + lap, 142) * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 edge = c + new Vector2(dir.x * rect.width, dir.y * rect.height) * 0.5f;
                    Vector2 at = edge + dir * Mathf.Max(0f, LavaFront(edge + dir, spread, t) + 0.1f)
                        + dir * life * 0.25f + Vector2.up * life * 0.2f;
                    float flicker = Hash(i * 7 + lap, 143) < 0.55f ? 1f : 0f;
                    Put(crackles[i], at, Vector2.one * Mathf.Lerp(0.16f, 0.04f, life), 0f,
                        Color.Lerp(LavaHot, LavaBright, life), (1f - life) * flicker * frontLife * handOver);
                }

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
                PutStack(stack, LavaBright, t, 2.7f, 3.0f, 3.45f, 3.8f);
            };
        }
    }
}
