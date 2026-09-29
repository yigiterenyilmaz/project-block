// PURPOSE: Activation animations for powers that had none of their own - each its own sentence:
//
//   TOTEM ("Totem")         - a carved, lit pole of three faces bursts up out of a rune circle on
//                             the arena floor, throwing dust, settles with a squash, wakes its eyes
//                             one face at a time, spreads its carved wings, and blesses the arena
//                             with gold rays, rings and rising motes before sinking back into the
//                             ground. The market it sends you to opens only once it is gone.
//   CLEAN SLATE ("İkinci Şans") - a FOUR-LEAF CLOVER grows over the board and its leaves are pulled
//                             off one by one; each leaf that flutters away takes its QUADRANT of
//                             the board with it, the cubes there lifting and dissolving into green
//                             and gold. The luck is spent, the round is given back. No score, so
//                             it must never read as an explosion.
//   COLD FUSION ("Soğuk Füzyon") - the two copies leave their piles, meet in the middle in a cold
//                             blue burst, and part again to their bonus slots.
//   COPY OUT ("Aşırma", "Yedekleme") - the same family: ONE card is lifted off its pile (the draw
//                             pile for Aşırma, the discard for Yedekleme), it splits in two with a
//                             flash, and both halves fly to their bonus slots.
//   CHARM ("Tılsım")        - a gold-rimmed charm with a spirit gem swings down on its cord over the
//                             arena, the ghosts' spirits are drawn into it along curved threads,
//                             and it pulses the payout before it is pulled back up. The harvest and
//                             the vines themselves are TalismanView's; this is the power's summons.
//   ARENA RESIZE (the inflations) - GROWING: the arena keeps its cell size for a moment while the new
//                             bands unfold out of the old rim, then the whole arena eases back to
//                             fit. SHRINKING: the doomed bands are crushed inward against the new
//                             rim - their floor and cubes squashed flat along the push - the rim's
//                             cubes take the blow with a squash and a bounce, and then the tighter
//                             arena eases up to its new cell size.
//
// Presentation only, and each is handed what it needs by the controller (the cube faces taken
// before the wipe, the new cards' visuals, the board before the resize). Playing is true while one
// runs, which the controller treats like the water animation: input waits.
//
// Shapes are BAKED once (leaf, ring, ray, disc, diamond, feather) at one world unit a sprite, so a
// scale IS a size; every light is a gradient that dies at its own edge, never a flat plate.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Activation effects for Totem, İkinci Şans, Soğuk Füzyon, Aşırma, Yedekleme, Tılsım
    /// and the inflation powers.</summary>
    public sealed class PowerFxView : MonoBehaviour
    {
        private const int BoardOrder = 30;

        private static readonly Color Gold = new Color(1f, 0.82f, 0.38f, 1f);
        private static readonly Color GoldDeep = new Color(0.78f, 0.52f, 0.16f, 1f);
        private static readonly Color Warm = new Color(1f, 0.9f, 0.62f, 1f);
        private static readonly Color Cold = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color WoodDark = new Color(0.27f, 0.16f, 0.09f, 1f);
        private static readonly Color WoodMid = new Color(0.47f, 0.29f, 0.15f, 1f);
        private static readonly Color WoodLight = new Color(0.62f, 0.42f, 0.22f, 1f);
        private static readonly Color WoodHigh = new Color(0.78f, 0.58f, 0.34f, 1f);
        private static readonly Color Carving = new Color(0.11f, 0.06f, 0.035f, 1f);
        private static readonly Color Paint = new Color(0.72f, 0.2f, 0.14f, 1f);
        private static readonly Color Teal = new Color(0.22f, 0.55f, 0.52f, 1f);
        private static readonly Color Dust = new Color(0.62f, 0.52f, 0.4f, 1f);
        private static readonly Color Leaf = new Color(0.3f, 0.7f, 0.33f, 1f);
        private static readonly Color LeafDeep = new Color(0.16f, 0.45f, 0.2f, 1f);
        private static readonly Color Luck = new Color(0.78f, 1f, 0.6f, 1f);
        private static readonly Color Spirit = new Color(0.72f, 0.95f, 0.85f, 1f);
        private static readonly Color Floor = new Color(0.112f, 0.121f, 0.147f, 1f);

        /// <summary>Aşırma steals from the draw pile: a sly violet. Yedekleme backs up from the
        /// discard: a calm mint.</summary>
        public static readonly Color StealColour = new Color(0.78f, 0.58f, 1f, 1f);

        public static readonly Color BackupColour = new Color(0.5f, 1f, 0.76f, 1f);

        private int running;

        /// <summary>The game's sound effects, handed over by the controller; every animation here
        /// plays its own beats through it. Null-safe (the lab may run without it).</summary>
        public SoundFx Sfx;

        private void Play(Action<SoundFx> cue)
        {
            if (Sfx != null)
            {
                cue(Sfx);
            }
        }

        public bool Playing
        {
            get { return running > 0; }
        }

        /// <summary>One cube's face, taken before the board was changed.</summary>
        public struct CubeFace
        {
            public Vector2 World;
            public GridPos Cell;
            public Sprite Tile;
            public Color Colour;
        }

        // =================================================================== TOTEM

        public void PlayTotem(Vector2 centre, float boardSize, Action done)
        {
            StartCoroutine(Totem(centre, boardSize, done));
        }

        private IEnumerator Totem(Vector2 centre, float boardSize, Action done)
        {
            running++;
            float u = boardSize / 9f;               // one face is about a board cell and a half
            float poleHeight = u * 5.6f;
            Vector2 foot = centre + new Vector2(0f, -poleHeight * 0.5f);
            var root = new GameObject("Totem").transform;
            root.SetParent(transform, false);
            root.position = foot;
            var body = new GameObject("TotemBody").transform; // the part that squashes
            body.SetParent(root, false);
            var wood = new List<Part>();
            var eyes = new List<SpriteRenderer>();
            var eyeFlares = new List<SpriteRenderer>();

            // ---- the ground: a rune circle and a glowing crack the pole comes out of ----
            var ground = new List<Part>();
            SpriteRenderer runeRing = MakeShape(transform, "RuneRing", Ring, foot, u * 3.6f, Gold, BoardOrder + 1);
            runeRing.transform.localScale = new Vector3(u * 3.6f, u * 1.2f, 1f);
            ground.Add(new Part(runeRing, Gold));
            SpriteRenderer runeInner = MakeShape(transform, "RuneInner", Ring, foot, u * 2.6f, GoldDeep, BoardOrder + 1);
            runeInner.transform.localScale = new Vector3(u * 2.6f, u * 0.86f, 1f);
            ground.Add(new Part(runeInner, GoldDeep));
            var ticks = new List<SpriteRenderer>();
            for (int i = 0; i < 10; i++)
            {
                SpriteRenderer tick = MakeShape(transform, "RuneTick", Diamond, foot, u * 0.2f, Gold, BoardOrder + 1);
                ticks.Add(tick);
                ground.Add(new Part(tick, Gold));
            }
            SpriteRenderer crack = MakeShape(transform, "Crack", Disc, foot, u * 2.2f, Warm, BoardOrder + 1);
            crack.transform.localScale = new Vector3(u * 2.2f, u * 0.5f, 1f);
            ground.Add(new Part(crack, Warm));
            SetGroupAlpha(ground, 0f);

            // ---- the pole: a plinth, three faces, wings and a sun ----
            Carve(body, wood, "Plinth", new Vector2(0f, u * 0.2f), new Vector2(u * 2.2f, u * 0.5f), WoodDark, 0);
            string[] kinds = { "bear", "owl", "eagle" };
            for (int i = 0; i < 3; i++)
            {
                float y = u * (1.1f + i * 1.45f);
                Color tone = i == 1 ? WoodLight : WoodMid;
                Carve(body, wood, "Face" + i, new Vector2(0f, y), new Vector2(u * 1.7f, u * 1.38f), tone, 1);
                // A painted band across each face, the teal and red a totem is really coloured in.
                wood.Add(new Part(MakeRound(body, "Band", new Vector2(0f, y - u * 0.02f),
                    new Vector2(u * 1.7f, u * 0.08f), i == 1 ? Teal : Paint, 3), i == 1 ? Teal : Paint));
                wood.Add(new Part(MakeRound(body, "Brow", new Vector2(0f, y + u * 0.4f),
                    new Vector2(u * 1.36f, u * 0.16f), Carving, 3), Carving));
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 eyeAt = new Vector2(side * u * 0.38f, y + u * 0.15f);
                    wood.Add(new Part(MakeRound(body, "Socket", eyeAt, new Vector2(u * 0.42f, u * 0.28f), Carving, 3), Carving));
                    SpriteRenderer eye = MakeShape(body, "Eye", Disc, Vector2.zero, u * 0.22f, Gold, BoardOrder + 6);
                    eye.transform.localPosition = eyeAt;
                    eye.transform.localScale = new Vector3(u * 0.24f, u * 0.13f, 1f);
                    eye.color = Clear(Gold);
                    eyes.Add(eye);
                    SpriteRenderer flare = MakeShape(body, "EyeFlare", ViewUtil.GlowSprite, Vector2.zero, u * 0.7f, Gold, BoardOrder + 7);
                    flare.transform.localPosition = eyeAt;
                    flare.color = Clear(Gold);
                    eyeFlares.Add(flare);
                }
                // The nose: an eagle's beak points down, the others are carved knobs.
                SpriteRenderer nose = MakeShape(body, "Nose", Diamond, Vector2.zero, u * 0.3f,
                    kinds[i] == "eagle" ? GoldDeep : WoodDark, BoardOrder + 4);
                nose.transform.localPosition = new Vector2(0f, y - u * 0.08f);
                nose.transform.localScale = kinds[i] == "eagle"
                    ? new Vector3(u * 0.3f, u * 0.5f, 1f) : new Vector3(u * 0.26f, u * 0.3f, 1f);
                wood.Add(new Part(nose, nose.color));
                wood.Add(new Part(MakeRound(body, "Mouth", new Vector2(0f, y - u * 0.4f),
                    new Vector2(u * 0.86f, u * 0.2f), Carving, 3), Carving));
                for (int tooth = -1; tooth <= 1; tooth++)
                {
                    wood.Add(new Part(MakeRound(body, "Tooth", new Vector2(tooth * u * 0.2f, y - u * 0.36f),
                        new Vector2(u * 0.1f, u * 0.1f), WoodHigh, 4), WoodHigh));
                }
            }
            float crestY = u * (1.1f + 2 * 1.45f) + u * 0.95f;
            // The wings: feathers fanned from the top face, folded at first and spread later.
            var wings = new List<Transform>();
            for (int side = -1; side <= 1; side += 2)
            {
                var hinge = new GameObject("Wing").transform;
                hinge.SetParent(body, false);
                hinge.localPosition = new Vector2(side * u * 0.7f, crestY - u * 0.55f);
                hinge.localScale = new Vector3(side, 1f, 1f);
                for (int f = 0; f < 4; f++)
                {
                    SpriteRenderer feather = MakeShape(hinge, "Feather", Feather, Vector2.zero, u, f % 2 == 0 ? WoodLight : WoodMid, BoardOrder + 2);
                    feather.transform.localScale = new Vector3(u * 0.55f, u * (1.7f - f * 0.22f), 1f);
                    feather.transform.localRotation = Quaternion.Euler(0f, 0f, -(35f + f * 17f));
                    wood.Add(new Part(feather, feather.color));
                    SpriteRenderer tip = MakeShape(feather.transform, "Tip", Disc, Vector2.zero, 1f, Paint, BoardOrder + 3);
                    tip.transform.localPosition = new Vector2(0f, 0.86f);
                    tip.transform.localScale = new Vector3(0.5f, 0.12f, 1f);
                    wood.Add(new Part(tip, Paint));
                }
                hinge.localRotation = Quaternion.Euler(0f, 0f, side * 50f); // folded up
                wings.Add(hinge);
            }
            SpriteRenderer sun = MakeShape(body, "Sun", Disc, Vector2.zero, u * 1.0f, GoldDeep, BoardOrder + 3);
            sun.transform.localPosition = new Vector2(0f, crestY + u * 0.1f);
            wood.Add(new Part(sun, GoldDeep));
            SpriteRenderer sunRing = MakeShape(body, "SunRing", Ring, Vector2.zero, u * 1.4f, Gold, BoardOrder + 4);
            sunRing.transform.localPosition = sun.transform.localPosition;
            sunRing.color = Clear(Gold);
            SpriteRenderer sunGlow = MakeShape(body, "SunGlow", ViewUtil.GlowSprite, Vector2.zero, u * 2.6f, Gold, BoardOrder + 1);
            sunGlow.transform.localPosition = sun.transform.localPosition;
            sunGlow.color = Clear(Gold);

            // ---- the blessing's light: rays behind the pole ----
            var rays = new List<SpriteRenderer>();
            Vector2 heart = foot + new Vector2(0f, poleHeight * 0.55f);
            for (int i = 0; i < 12; i++)
            {
                SpriteRenderer ray = MakeShape(transform, "Ray", Ray, heart, 1f, Gold, BoardOrder - 1);
                ray.transform.localScale = new Vector3(u * (i % 2 == 0 ? 0.9f : 0.55f), boardSize * (i % 2 == 0 ? 0.75f : 0.55f), 1f);
                ray.color = Clear(Gold);
                rays.Add(ray);
            }
            SpriteRenderer halo = MakeShape(transform, "Halo", ViewUtil.GlowSprite, heart, poleHeight * 1.5f, Gold, BoardOrder - 1);
            halo.color = Clear(Gold);

            var motes = new List<Mote>();
            var rings = new List<SpriteRenderer>();
            SetGroupAlpha(wood, 0f);
            bool[] woke = new bool[3];
            bool titled = false;
            bool dusted = false;
            bool rumbled = false;
            bool spreadHeard = false;
            bool sinkHeard = false;
            float spin = 0f;
            float t = 0f;
            const float Rise0 = 0.18f, Rise1 = 0.78f, Wake0 = 0.9f, Spread = 1.4f, Bless = 1.5f,
                Sink0 = 2.35f, End = 2.85f;
            float nextPuff = Rise0;
            while (t < End)
            {
                float dt = Time.deltaTime;
                t += dt;
                spin += dt * 25f;

                // THE GROUND answers first and last.
                float groundIn = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.Clamp01((t - Sink0 - 0.2f) / 0.3f));
                SetGroupAlpha(ground, groundIn * 0.8f);
                crack.color = Fade(Warm, groundIn * (0.35f + 0.35f * Bump(t, Rise0, Rise1 + 0.2f)));
                float ringSpread = Mathf.Lerp(0.6f, 1f, EaseOut(Mathf.Clamp01(t / 0.35f)));
                runeRing.transform.localScale = new Vector3(u * 3.6f * ringSpread, u * 1.2f * ringSpread, 1f);
                for (int i = 0; i < ticks.Count; i++)
                {
                    float a = (spin + i * 36f) * Mathf.Deg2Rad;
                    ticks[i].transform.position = foot + new Vector2(Mathf.Cos(a) * u * 1.55f * ringSpread,
                        Mathf.Sin(a) * u * 0.52f * ringSpread);
                }

                if (!rumbled && t >= Rise0)
                {
                    rumbled = true;
                    Play(x => x.Rumble());
                }
                if (!sinkHeard && t >= Sink0)
                {
                    sinkHeard = true;
                    Play(x => x.Rumble());
                }
                // THE RISE: out of the ground with an overshoot, dust thrown sideways from the foot.
                float rise = EaseOutBack(Mathf.Clamp01((t - Rise0) / (Rise1 - Rise0)), 1.2f);
                float sink = Mathf.Clamp01((t - Sink0) / (End - Sink0));
                float lift = -poleHeight * (1f - rise) + -poleHeight * sink * sink;
                body.localPosition = new Vector2(0f, lift);
                SetGroupAlpha(wood, Mathf.Clamp01((t - Rise0) * 5f) * (1f - sink));
                // Only what stands above the floor line is drawn - the pole comes OUT of the ground.
                ClipBelow(wood, foot.y - u * 0.05f);
                if (t > Rise0 && t < Rise1 + 0.1f && t >= nextPuff)
                {
                    nextPuff += 0.05f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        motes.Add(Mote.Make(this, ViewUtil.GlowSprite, foot + new Vector2(side * u * 0.8f, 0f),
                            new Vector2(side * u * UnityEngine.Random.Range(1.4f, 2.6f), u * UnityEngine.Random.Range(0.2f, 0.9f)),
                            u * 0.9f, Dust, 0.55f, BoardOrder + 8, 2.5f, 0f, 1.8f));
                    }
                }
                // LANDING: a squash on the settle, a second burst of dust.
                float land = Bump(t, Rise1 - 0.08f, Rise1 + 0.22f);
                body.localScale = new Vector3(1f + 0.06f * land, 1f - 0.07f * land, 1f);
                if (!dusted && t >= Rise1 - 0.05f)
                {
                    dusted = true;
                    Play(x => x.Drum(0.8f));
                    for (int i = 0; i < 10; i++)
                    {
                        float a = Mathf.Lerp(-0.3f, Mathf.PI + 0.3f, i / 9f);
                        motes.Add(Mote.Make(this, ViewUtil.GlowSprite, foot,
                            new Vector2(Mathf.Cos(a) * u * 3f, Mathf.Abs(Mathf.Sin(a)) * u * 1.2f),
                            u * 0.7f, Dust, 0.5f, BoardOrder + 8, 3f, 0f, 1.5f));
                    }
                }

                // THE EYES WAKE, bottom face first, each with a flare and a shudder of the pole.
                for (int face = 0; face < 3; face++)
                {
                    float at = Wake0 + face * 0.16f;
                    float k = Mathf.Clamp01((t - at) / 0.1f) * (1f - sink);
                    float flare = Bump(t, at, at + 0.32f) * (1f - sink);
                    for (int s = 0; s < 2; s++)
                    {
                        eyes[face * 2 + s].color = Fade(Gold, k);
                        eyeFlares[face * 2 + s].color = Fade(Gold, 0.9f * flare + 0.25f * k);
                        eyeFlares[face * 2 + s].transform.localScale = Vector3.one * u * (0.5f + 0.7f * flare);
                    }
                    if (!woke[face] && t >= at)
                    {
                        woke[face] = true;
                        float pitch = 1f + face * 0.14f;
                        Play(x => x.Drum(pitch));
                    }
                }
                float shudder = 0f;
                for (int face = 0; face < 3; face++)
                {
                    shudder += Bump(t, Wake0 + face * 0.16f, Wake0 + face * 0.16f + 0.12f);
                }
                root.position = foot + new Vector2(Mathf.Sin(t * 90f) * u * 0.03f * shudder, 0f);

                // THE WINGS SPREAD, and the sun on top lights.
                if (!spreadHeard && t >= Spread)
                {
                    spreadHeard = true;
                    Play(x => x.Whoosh());
                }
                float spread = EaseOutBack(Mathf.Clamp01((t - Spread) / 0.3f), 1.6f);
                for (int w = 0; w < wings.Count; w++)
                {
                    float side = w == 0 ? -1f : 1f;
                    wings[w].localRotation = Quaternion.Euler(0f, 0f, side * Mathf.Lerp(50f, -8f, spread));
                }
                float sunK = Mathf.Clamp01((t - Spread) / 0.2f) * (1f - sink);
                sunGlow.color = Fade(Gold, 0.55f * sunK * (0.8f + 0.2f * Mathf.Sin(t * 9f)));
                sunRing.color = Fade(Gold, sunK * 0.9f);
                sunRing.transform.localScale = Vector3.one * u * (1.3f + 0.15f * Mathf.Sin(t * 6f));

                // THE BLESSING: rays turning slowly behind, rings over the board, motes rising.
                float bless = Mathf.Clamp01((t - Bless) / 0.25f) * (1f - Mathf.Clamp01((t - Sink0) / 0.3f));
                for (int i = 0; i < rays.Count; i++)
                {
                    rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, i * 30f + spin * 0.6f);
                    rays[i].color = Fade(Gold, bless * (i % 2 == 0 ? 0.22f : 0.14f)
                        * (0.8f + 0.2f * Mathf.Sin(t * 5f + i)));
                }
                halo.color = Fade(Gold, 0.3f * bless);
                if (!titled && t >= Bless)
                {
                    titled = true;
                    Play(x => x.Chime(1.15f));
                    for (int r = 0; r < 2; r++)
                    {
                        rings.Add(MakeShape(transform, "TotemRing", Ring, heart, 0.1f, Gold, BoardOrder + 1));
                    }
                    for (int i = 0; i < 18; i++)
                    {
                        float a = i * 2.39996f;
                        Vector2 at = foot + new Vector2(Mathf.Cos(a) * u * UnityEngine.Random.Range(0.6f, 1.6f), UnityEngine.Random.Range(0f, poleHeight * 0.7f));
                        motes.Add(Mote.Make(this, Diamond, at, new Vector2(0f, u * UnityEngine.Random.Range(1.2f, 2.4f)),
                            u * UnityEngine.Random.Range(0.1f, 0.2f), i % 3 == 0 ? Warm : Gold, 1f, BoardOrder + 9, 0f, 0f,
                            UnityEngine.Random.Range(0.7f, 1.2f)));
                    }
                    FloatingTextFx.Spawn(transform, foot + new Vector2(0f, poleHeight * 1.12f),
                        Loc.Pick("TOTEM - TO THE MARKET", "TOTEM - MARKETE"), Gold, 60, 0.07f);
                }
                for (int r = 0; r < rings.Count; r++)
                {
                    float k = Mathf.Clamp01((t - Bless - r * 0.18f) / 0.6f);
                    rings[r].transform.localScale = Vector3.one * Mathf.Lerp(u, boardSize * 1.6f, EaseOut(k));
                    rings[r].color = Fade(Gold, k <= 0f ? 0f : 0.6f * (1f - k));
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer r in rings)
            {
                Destroy(r.gameObject);
            }
            foreach (SpriteRenderer r in rays)
            {
                Destroy(r.gameObject);
            }
            foreach (SpriteRenderer r in ticks)
            {
                Destroy(r.gameObject);
            }
            Destroy(halo.gameObject);
            Destroy(runeRing.gameObject);
            Destroy(runeInner.gameObject);
            Destroy(crack.gameObject);
            Destroy(root.gameObject);
            running--;
            if (done != null)
            {
                done();
            }
        }

        /// <summary>A carved block: a dark outline, the wood, a lit left edge and a shaded right.
        /// </summary>
        private static void Carve(Transform parent, List<Part> parts, string name, Vector2 at, Vector2 size,
            Color tone, int layer)
        {
            float edge = size.x * 0.16f;
            parts.Add(new Part(MakeRound(parent, name + "Outline", at, size + new Vector2(size.y * 0.1f, size.y * 0.1f), Carving, layer), Carving));
            parts.Add(new Part(MakeRound(parent, name, at, size, tone, layer + 1), tone));
            Color lit = Color.Lerp(tone, WoodHigh, 0.55f);
            Color shade = Color.Lerp(tone, Carving, 0.45f);
            parts.Add(new Part(MakeRound(parent, name + "Lit", at + new Vector2(-size.x * 0.5f + edge * 0.6f, 0f),
                new Vector2(edge, size.y * 0.88f), lit, layer + 2), lit));
            parts.Add(new Part(MakeRound(parent, name + "Shade", at + new Vector2(size.x * 0.5f - edge * 0.6f, 0f),
                new Vector2(edge, size.y * 0.88f), shade, layer + 2), shade));
        }

        // ============================================================= CLEAN SLATE

        public void PlayCleanSlate(IList<CubeFace> cubes, Vector2 centre, float cube, float boardSize)
        {
            StartCoroutine(CleanSlate(cubes, centre, cube, boardSize));
        }

        /// <summary>
        /// The clover. Leaf q covers the quadrant it points into (0 up-right, 1 up-left, 2 down-left,
        /// 3 down-right) and is plucked in that order; the cubes of that quadrant go with it.
        /// </summary>
        private IEnumerator CleanSlate(IList<CubeFace> cubes, Vector2 centre, float cube, float boardSize)
        {
            running++;
            float leafLen = boardSize * 0.27f;
            var clover = new GameObject("Clover").transform;
            clover.SetParent(transform, false);
            clover.position = centre;
            SpriteRenderer aura = MakeShape(clover, "Aura", ViewUtil.GlowSprite, Vector2.zero, leafLen * 3.4f, Luck, BoardOrder + 1);
            aura.transform.localPosition = Vector3.zero;
            SpriteRenderer stem = MakeRound(clover, "Stem", new Vector2(leafLen * 0.14f, -leafLen * 0.55f),
                new Vector2(leafLen * 0.09f, leafLen * 1.1f), LeafDeep, 3);
            stem.transform.localRotation = Quaternion.Euler(0f, 0f, 16f);
            var leaves = new SpriteRenderer[4];
            var leafShadow = new SpriteRenderer[4];
            for (int q = 0; q < 4; q++)
            {
                float angle = 45f + q * 90f;
                Quaternion turn = Quaternion.Euler(0f, 0f, angle - 90f);
                leafShadow[q] = MakeShape(clover, "LeafShadow", Leaf_, Vector2.zero, leafLen, new Color(0f, 0.08f, 0f, 0.35f), BoardOrder + 3);
                leafShadow[q].transform.localPosition = new Vector2(leafLen * 0.04f, -leafLen * 0.05f);
                leafShadow[q].transform.localRotation = turn;
                leaves[q] = MakeShape(clover, "Leaf", Leaf_, Vector2.zero, leafLen, q % 2 == 0 ? Leaf : Color.Lerp(Leaf, LeafDeep, 0.25f), BoardOrder + 4);
                leaves[q].transform.localPosition = Vector3.zero;
                leaves[q].transform.localRotation = turn;
            }
            SpriteRenderer knot = MakeShape(clover, "Knot", Disc, Vector2.zero, leafLen * 0.22f, LeafDeep, BoardOrder + 5);
            knot.transform.localPosition = Vector3.zero;

            // Copies of the cubes, each tied to the leaf over its quadrant.
            var copies = new List<SpriteRenderer>();
            var quadrant = new List<int>();
            var lag = new List<float>();
            float farthest = 0.01f;
            for (int i = 0; i < cubes.Count; i++)
            {
                farthest = Mathf.Max(farthest, Vector2.Distance(cubes[i].World, centre));
            }
            for (int i = 0; i < cubes.Count; i++)
            {
                SpriteRenderer copy = ViewUtil.MakeCell(transform, "SlateCube", cubes[i].World, cube,
                    cubes[i].Colour, BoardOrder);
                ViewUtil.ApplyTile(copy, cubes[i].Tile, cube);
                copy.color = cubes[i].Colour;
                copies.Add(copy);
                Vector2 d = cubes[i].World - centre;
                float deg = (Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + 360f) % 360f;
                quadrant.Add(Mathf.Clamp((int)(deg / 90f), 0, 3));
                // From the clover's heart outward: the luck leaves the middle of the quadrant last.
                lag.Add(0.28f * d.magnitude / farthest);
            }

            const float Grow = 0.38f, FirstPluck = 0.55f, PluckGap = 0.3f, Wiggle = 0.14f, Fly = 0.85f;
            float[] pluckAt = new float[4];
            for (int q = 0; q < 4; q++)
            {
                pluckAt[q] = FirstPluck + q * PluckGap;
            }
            float end = pluckAt[3] + Mathf.Max(Fly, 0.28f + 0.5f) + 0.1f;
            var velocity = new Vector2[4];
            var spinRate = new float[4];
            var plucked = new bool[4];
            var motes = new List<Mote>();
            var cubeGone = new bool[copies.Count];
            bool said = false;
            Play(x => x.Chime(1.35f)); // the clover appears
            float t = 0f;
            while (t < end)
            {
                float dt = Time.deltaTime;
                t += dt;
                // THE CLOVER GROWS in with a quarter turn and an overshoot.
                float grow = EaseOutBack(Mathf.Clamp01(t / Grow), 1.8f);
                clover.localScale = Vector3.one * grow;
                clover.localRotation = Quaternion.Euler(0f, 0f, (1f - Mathf.Clamp01(t / Grow)) * 70f
                    + Mathf.Sin(t * 3f) * 2f);
                float stemGone = Mathf.Clamp01((t - pluckAt[3] - 0.15f) / 0.35f);
                stem.color = Fade(LeafDeep, 1f - stemGone);
                knot.color = Fade(LeafDeep, 1f - stemGone);
                aura.color = Fade(Luck, 0.22f * Mathf.Clamp01(t / Grow) * (1f - stemGone)
                    * (0.85f + 0.15f * Mathf.Sin(t * 7f)));

                for (int q = 0; q < 4; q++)
                {
                    SpriteRenderer leaf = leaves[q];
                    if (leaf == null)
                    {
                        continue;
                    }
                    float baseAngle = 45f + q * 90f - 90f;
                    float before = t - (pluckAt[q] - Wiggle);
                    if (!plucked[q])
                    {
                        // A tug: the leaf trembles and leans out before it lets go.
                        float tug = Mathf.Clamp01(before / Wiggle);
                        float wobble = before > 0f ? Mathf.Sin(before * 70f) * 9f * tug : 0f;
                        leaf.transform.localRotation = Quaternion.Euler(0f, 0f, baseAngle + wobble);
                        leafShadow[q].transform.localRotation = leaf.transform.localRotation;
                        leaf.transform.localScale = new Vector3(1f, 1f + 0.08f * tug, 1f) * leafLen;
                        if (t >= pluckAt[q])
                        {
                            plucked[q] = true;
                            float pitch = 0.95f + q * 0.12f;
                            Play(x => x.Pluck(pitch));
                            // Off it goes: out along its own direction and up, spinning.
                            float a = (45f + q * 90f) * Mathf.Deg2Rad;
                            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            velocity[q] = dir * leafLen * 2.6f + Vector2.up * leafLen * 1.4f;
                            spinRate[q] = (q % 2 == 0 ? 1f : -1f) * 320f;
                            leaf.transform.SetParent(transform, true);
                            Destroy(leafShadow[q].gameObject);
                            // A snap where it was attached.
                            for (int m = 0; m < 6; m++)
                            {
                                float ma = a + (m - 2.5f) * 0.35f;
                                motes.Add(Mote.Make(this, Diamond, centre + dir * leafLen * 0.15f,
                                    new Vector2(Mathf.Cos(ma), Mathf.Sin(ma)) * leafLen * UnityEngine.Random.Range(1.2f, 2.2f),
                                    leafLen * 0.09f, m % 2 == 0 ? Luck : Gold, 1f, BoardOrder + 9, 3f, 0f, 0.45f));
                            }
                            motes.Add(Mote.Make(this, ViewUtil.GlowSprite, centre + dir * leafLen * 0.3f, Vector2.zero,
                                leafLen * 1.1f, Luck, 0.55f, BoardOrder + 2, 0f, 0f, 0.3f));
                        }
                        continue;
                    }
                    // Fluttering away: drag, a little gravity, a sideways sway, a spin, shrinking.
                    float k = Mathf.Clamp01((t - pluckAt[q]) / Fly);
                    velocity[q] *= 1f - 2.4f * dt;
                    velocity[q] += new Vector2(Mathf.Sin((t - pluckAt[q]) * 11f + q) * leafLen * 5f, -leafLen * 1.2f) * dt;
                    leaf.transform.position += (Vector3)(velocity[q] * dt);
                    leaf.transform.Rotate(0f, 0f, spinRate[q] * dt * (1f - k * 0.6f));
                    float flip = Mathf.Cos((t - pluckAt[q]) * 14f + q);
                    leaf.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.55f, k) * (0.35f + 0.65f * Mathf.Abs(flip)),
                        Mathf.Lerp(1f, 0.55f, k), 1f) * leafLen;
                    leaf.color = Fade(Color.Lerp(leaf.color, Luck, dt * 1.5f), 1f - k * k);
                    if (k >= 1f)
                    {
                        Destroy(leaf.gameObject);
                        leaves[q] = null;
                    }
                }

                // THE QUADRANT GOES WITH ITS LEAF: lift, shrink, pale to luck-green, dissolve.
                for (int i = 0; i < copies.Count; i++)
                {
                    if (copies[i] == null)
                    {
                        continue;
                    }
                    float k = Mathf.Clamp01((t - pluckAt[quadrant[i]] - lag[i]) / 0.5f);
                    if (k <= 0f)
                    {
                        continue;
                    }
                    if (!cubeGone[i])
                    {
                        cubeGone[i] = true;
                        for (int m = 0; m < 2; m++)
                        {
                            float side = (i * 37 + m * 91) % 7 / 7f - 0.5f; // deterministic spread
                            motes.Add(Mote.Make(this, m == 0 ? ViewUtil.GlowSprite : Diamond, cubes[i].World,
                                new Vector2(side * cube * 1.3f, cube * (1.5f + m * 0.6f)),
                                cube * (m == 0 ? 0.45f : 0.14f), m == 0 ? Luck : Gold, 0.9f, BoardOrder + 1, 0.5f, 0f, 0.7f));
                        }
                    }
                    copies[i].transform.position = cubes[i].World + new Vector2(0f, cube * 0.35f * k);
                    copies[i].transform.localScale = Vector3.one * cube * Mathf.Lerp(1f, 0.5f, k);
                    Color c = Color.Lerp(cubes[i].Colour, Luck, k * 0.75f);
                    c.a = 1f - k * k;
                    copies[i].color = c;
                    if (k >= 1f)
                    {
                        Destroy(copies[i].gameObject);
                        copies[i] = null;
                    }
                }
                if (!said && t >= pluckAt[3])
                {
                    said = true;
                    Play(x => x.Chime(1.25f));
                    FloatingTextFx.Spawn(transform, centre + new Vector2(0f, boardSize * 0.08f),
                        Loc.Pick("SECOND CHANCE", "İKİNCİ ŞANS"), Luck, 60, 0.07f);
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer c in copies)
            {
                if (c != null)
                {
                    Destroy(c.gameObject);
                }
            }
            foreach (SpriteRenderer l in leaves)
            {
                if (l != null)
                {
                    Destroy(l.gameObject);
                }
            }
            Destroy(clover.gameObject);
            running--;
        }

        // ============================================================= COLD FUSION

        /// <summary>The new bonus cards, each starting at the pile it was copied from (local
        /// positions in the card layer), meeting in the middle, then parting to their slots.
        /// </summary>
        public void PlayColdFusion(Transform cardLayer, IList<CardVisual> cards, IList<Vector2> fromPiles,
            int order, Action onMeet)
        {
            StartCoroutine(ColdFusion(cardLayer, cards, fromPiles, order, onMeet));
        }

        private IEnumerator ColdFusion(Transform cardLayer, IList<CardVisual> cards, IList<Vector2> fromPiles,
            int order, Action onMeet)
        {
            running++;
            var homes = new List<Vector2>();
            Vector2 meet = Vector2.zero;
            for (int i = 0; i < cards.Count; i++)
            {
                homes.Add(cards[i].HomePosition);
                cards[i].transform.localPosition = fromPiles[i];
                meet += fromPiles[i];
            }
            meet = meet / Mathf.Max(1, cards.Count) + new Vector2(0f, 1.6f);
            // Frost where each card leaves its pile.
            for (int i = 0; i < cards.Count; i++)
            {
                StartCoroutine(Burst(cardLayer.TransformPoint(fromPiles[i]), 1.2f, Cold, order, 0.35f));
            }
            Play(x => x.Whoosh());
            // 1. Both rise to the meeting point.
            yield return Animate(0.28f, k =>
            {
                float e = 1f - (1f - k) * (1f - k);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null)
                    {
                        Vector2 offset = new Vector2((i - (cards.Count - 1) * 0.5f) * 0.5f, 0f);
                        cards[i].transform.localPosition = Vector2.Lerp(fromPiles[i], meet + offset, e);
                    }
                }
            });
            // 2. THE FUSION: a cold burst where they meet.
            if (onMeet != null)
            {
                onMeet();
            }
            StartCoroutine(Burst(cardLayer.TransformPoint(meet), 3.2f, Cold, order, 0.45f));
            yield return new WaitForSeconds(0.08f);
            // 3. They part to their own slots.
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].MoveTo(homes[i], 0.32f, null);
                }
            }
            yield return new WaitForSeconds(0.32f);
            running--;
        }

        // ================================================================ COPY OUT

        /// <summary>"Aşırma" / "Yedekleme": one card lifted off <paramref name="fromPile"/> (a local
        /// position in the card layer) splits into the new copies, which fly to their slots.
        /// <paramref name="onSplit"/> fires on the frame it splits (the sound).</summary>
        public void PlayCopyOut(Transform cardLayer, IList<CardVisual> cards, Vector2 fromPile, Color colour,
            int order, Action onSplit)
        {
            StartCoroutine(CopyOut(cardLayer, cards, fromPile, colour, order, onSplit));
        }

        private IEnumerator CopyOut(Transform cardLayer, IList<CardVisual> cards, Vector2 fromPile, Color colour,
            int order, Action onSplit)
        {
            running++;
            var homes = new List<Vector2>();
            for (int i = 0; i < cards.Count; i++)
            {
                homes.Add(cards[i].HomePosition);
                cards[i].transform.localPosition = fromPile;
            }
            Vector2 lift = fromPile + new Vector2(0f, 1.9f);
            // 1. The pile gives the card up: a flash on the pile, and ONE card (they are stacked
            // exactly on one another) rises out of it with a little sway.
            StartCoroutine(Burst(cardLayer.TransformPoint(fromPile), 1.4f, colour, order, 0.35f));
            Play(x => x.Whoosh());
            SpriteRenderer aura = MakeShape(transform, "CopyAura", ViewUtil.GlowSprite,
                cardLayer.TransformPoint(fromPile), 1.6f, colour, order - 1);
            aura.color = Clear(colour);
            yield return Animate(0.3f, k =>
            {
                float e = EaseOutBack(k, 1.3f);
                Vector2 at = Vector2.LerpUnclamped(fromPile, lift, e) + new Vector2(Mathf.Sin(k * Mathf.PI) * 0.15f, 0f);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null)
                    {
                        cards[i].transform.localPosition = at;
                    }
                }
                aura.transform.position = cardLayer.TransformPoint(at);
                aura.color = Fade(colour, 0.5f * k);
            });
            // 2. It charges - the aura tightens and brightens - and SPLITS.
            yield return Animate(0.12f, k =>
            {
                aura.transform.localScale = Vector3.one * Mathf.Lerp(1.6f, 1.1f, k);
                aura.color = Fade(colour, Mathf.Lerp(0.5f, 0.95f, k));
            });
            if (onSplit != null)
            {
                onSplit();
            }
            Vector2 world = cardLayer.TransformPoint(lift);
            StartCoroutine(Burst(world, 3f, colour, order, 0.4f));
            var motes = new List<Mote>();
            for (int m = 0; m < 10; m++)
            {
                float a = m * 0.628f;
                motes.Add(Mote.Make(this, Diamond, world, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(2.5f, 4f),
                    0.14f, m % 2 == 0 ? colour : Color.white, 1f, order + 1, 4f, 0f, 0.4f));
            }
            // A thread between the halves stretches and snaps.
            SpriteRenderer thread = ViewUtil.MakeRounded(transform, "CopyThread", Vector2.zero, new Vector2(0.1f, 0.06f), colour, order);
            thread.transform.position = world;
            float spread = 0.75f;
            float s = 0f;
            while (s < 0.16f)
            {
                s += Time.deltaTime;
                float k = Mathf.Clamp01(s / 0.16f);
                float e = EaseOutBack(k, 2f);
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i] != null)
                    {
                        float side = i - (cards.Count - 1) * 0.5f;
                        cards[i].transform.localPosition = lift + new Vector2(side * spread * e, 0f);
                    }
                }
                thread.size = new Vector2(Mathf.Lerp(0.1f, spread * 1.2f, k), 0.06f * (1f - k * 0.7f));
                thread.color = Fade(colour, 1f - k);
                aura.color = Fade(colour, 0.95f * (1f - k));
                StepMotes(motes, Time.deltaTime);
                yield return null;
            }
            Destroy(thread.gameObject);
            Destroy(aura.gameObject);
            // 3. Each half flies to its own slot.
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].MoveTo(homes[i], 0.3f, null);
                }
            }
            s = 0f;
            while (s < 0.32f)
            {
                s += Time.deltaTime;
                StepMotes(motes, Time.deltaTime);
                yield return null;
            }
            ClearMotes(motes);
            running--;
        }

        // ==================================================================== REEL

        /// <summary>"Olta": a jointed rod leans in over the card's bonus slot, the line is CAST in
        /// a slack arc onto the pile the card was in, the bobber bobs and then DIPS - a "!" and a
        /// splash - and the card is REELED up in tugs: every tug bends the rod, snaps the line
        /// taut and swings the card on the hook like something really caught. It lands in its
        /// slot, the hook lets go and the rod lifts away.</summary>
        public void PlayReel(Transform cardLayer, CardVisual card, Vector2 fromPile, bool fromDiscard, int order)
        {
            StartCoroutine(Reel(cardLayer, card, fromPile, fromDiscard, order));
        }

        private const int LineSegments = 14;

        private IEnumerator Reel(Transform cardLayer, CardVisual card, Vector2 fromPile, bool fromDiscard, int order)
        {
            running++;
            Vector2 home = card.HomePosition;
            Vector2 hangLocal = home + new Vector2(0f, 2.3f);             // where the catch is lifted to
            Vector2 pile = cardLayer.TransformPoint(fromPile);
            // The rod stands on the side AWAY from the pile and leans toward it, so the line never
            // crosses the rod: the draw pile is on the right, the discard on the left.
            Vector2 slot = cardLayer.TransformPoint(home);
            float mir = pile.x > slot.x ? -1f : 1f;
            Vector2 rodBase = cardLayer.TransformPoint(home + new Vector2(2.3f * mir, 1.2f));
            Color lineColour = new Color(0.93f, 0.96f, 1f, 1f);
            Color accent = fromDiscard ? new Color(1f, 0.58f, 0.36f) : new Color(0.55f, 0.86f, 1f);
            Color steel = new Color(0.86f, 0.89f, 0.94f);
            Color cork = new Color(0.95f, 0.93f, 0.86f);
            card.transform.localPosition = fromPile;
            card.gameObject.SetActive(false); // still in the pile until it is hooked

            // ---- the rod: three tapering joints that bend, a grip, a reel ----
            var rodParts = new List<SpriteRenderer>();
            float[] jointLen = { 0.95f, 0.85f, 0.75f };
            float[] jointWide = { 0.13f, 0.095f, 0.065f };
            for (int i = 0; i < 3; i++)
            {
                rodParts.Add(ViewUtil.MakeRounded(transform, "RodJoint", Vector2.zero,
                    new Vector2(jointLen[i] + 0.04f, jointWide[i]), i == 0 ? WoodDark : WoodMid, order + 3));
            }
            var rings = new List<SpriteRenderer>();
            for (int i = 0; i < 3; i++)
            {
                rings.Add(MakeShape(transform, "RodRing", Disc, rodBase, 0.07f, GoldDeep, order + 4));
            }
            SpriteRenderer grip = ViewUtil.MakeRounded(transform, "Grip", Vector2.zero, new Vector2(0.5f, 0.17f), Carving, order + 3);
            SpriteRenderer reelBody = MakeShape(transform, "Reel", Disc, rodBase, 0.36f, WoodHigh, order + 4);
            SpriteRenderer reelSpoke = ViewUtil.MakeRounded(transform, "ReelSpoke", Vector2.zero, new Vector2(0.3f, 0.05f), WoodDark, order + 5);
            var line = new List<SpriteRenderer>();
            for (int i = 0; i < LineSegments; i++)
            {
                line.Add(ViewUtil.MakeRounded(transform, "Line", Vector2.zero, new Vector2(0.1f, 0.03f), lineColour, order + 2));
            }
            SpriteRenderer hook = MakeShape(transform, "Hook", Diamond, pile, 0.18f, steel, order + 6);
            SpriteRenderer bobberTop = MakeShape(transform, "Bobber", Disc, pile, 0.24f, Paint, order + 5);
            SpriteRenderer bobberBottom = MakeShape(transform, "BobberBase", Disc, pile, 0.2f, cork, order + 4);
            SpriteRenderer glint = MakeShape(transform, "Glint", ViewUtil.GlowSprite, pile, 0.6f, accent, order + 1);
            glint.color = Clear(accent);
            var motes = new List<Mote>();

            float rodAngle = 145f;   // written for the rod on the right leaning left; Mirror() flips it
            float bend = 0f;
            float lift = 1f;         // 1 = off screen above, 0 = in place
            float fade = 1f;
            Vector2 tipNow = rodBase;

            // Poses the rod for this frame and hands back where its tip is.
            Func<Vector2> poseRod = () =>
            {
                Vector2 at = rodBase + new Vector2(0.4f * mir, 1.4f) * lift;
                float angle = Mirror(rodAngle + 25f * lift, mir);
                grip.transform.position = at - (Vector2)(Quaternion.Euler(0f, 0f, angle) * Vector3.right) * 0.28f;
                grip.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                reelBody.transform.position = at + new Vector2(0f, -0.14f);
                for (int i = 0; i < 3; i++)
                {
                    // Each joint turns a little further toward the line as the rod loads.
                    angle -= mir * bend * (6f + i * 9f);
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector3.right;
                    Vector2 end = at + dir * jointLen[i];
                    rodParts[i].transform.position = (at + end) * 0.5f;
                    rodParts[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                    rings[i].transform.position = end;
                    at = end;
                }
                Color wood = Fade(WoodMid, fade);
                rodParts[0].color = Fade(WoodDark, fade);
                rodParts[1].color = wood;
                rodParts[2].color = wood;
                grip.color = Fade(Carving, fade);
                reelBody.color = Fade(WoodHigh, fade);
                reelSpoke.color = Fade(WoodDark, fade);
                reelSpoke.transform.position = reelBody.transform.position;
                foreach (SpriteRenderer r in rings)
                {
                    r.color = Fade(GoldDeep, fade);
                }
                return at;
            };

            // 0. THE ROD LEANS IN from above.
            yield return Animate(0.12f, k =>
            {
                lift = 1f - EaseOutBack(k, 1.2f);
                fade = Mathf.Clamp01(k * 3f);
                tipNow = poseRod();
                PoseLine(line, tipNow, tipNow, 0f, 0f);
                hook.transform.position = tipNow;
                bobberTop.transform.position = tipNow;
                bobberBottom.transform.position = tipNow;
            });
            lift = 0f;

            // 1. THE CAST: the rod whips back and forward, the hook flies out in a slack arc.
            Play(x => x.Whoosh());
            yield return Animate(0.08f, k =>
            {
                rodAngle = 145f + 22f * Mathf.Sin(k * Mathf.PI * 0.5f);
                bend = -0.5f * k;
                tipNow = poseRod();
                PoseLine(line, tipNow, tipNow, 0f, fade);
            });
            yield return Animate(0.2f, k =>
            {
                rodAngle = Mathf.Lerp(167f, 138f, EaseOut(k));
                bend = Mathf.Lerp(-0.5f, 0.35f, k);
                tipNow = poseRod();
                float e = EaseOut(k);
                Vector2 at = Vector2.Lerp(tipNow, pile, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * 1.6f);
                hook.transform.position = at;
                Vector2 bob = Vector2.Lerp(tipNow, at, 0.8f);
                bobberTop.transform.position = bob + new Vector2(0f, 0.06f);
                bobberBottom.transform.position = bob - new Vector2(0f, 0.06f);
                PoseLine(line, tipNow, at, 0.9f, fade);
            });
            // The hook lands: a ring, a few drops.
            Play(x => x.Pluck(0.9f));
            Splash(motes, pile, accent, 5, order);

            // 2. THE WAIT AND THE BITE: two idle bobs, then a hard dip, a "!" and a splash.
            Vector2 bobRest = (Vector2)bobberTop.transform.position - new Vector2(0f, 0.06f);
            yield return Animate(0.2f, k =>
            {
                float bobY = Mathf.Sin(k * Mathf.PI * 4f) * 0.05f;
                Vector2 bob = bobRest + new Vector2(0f, bobY);
                bobberTop.transform.position = bob + new Vector2(0f, 0.06f);
                bobberBottom.transform.position = bob - new Vector2(0f, 0.06f);
                tipNow = poseRod();
                PoseLine(line, tipNow, hook.transform.position, 0.55f, fade);
                StepMotes(motes, Time.deltaTime);
            });
            Play(x => x.Pluck(1.45f));
            FloatingTextFx.Spawn(transform, bobRest + new Vector2(0f, 0.55f), "!", accent, 80, 0.09f);
            Splash(motes, bobRest, accent, 8, order);
            SpriteRenderer ripple = MakeShape(transform, "Ripple", Ring, pile, 0.3f, accent, order + 1);
            yield return Animate(0.14f, k =>
            {
                float dip = Mathf.Sin(Mathf.Clamp01(k * 1.6f) * Mathf.PI) * 0.26f;
                bobberTop.transform.position = bobRest + new Vector2(0f, 0.06f - dip);
                bobberBottom.transform.position = bobRest - new Vector2(0f, 0.06f + dip);
                ripple.transform.localScale = new Vector3(Mathf.Lerp(0.3f, 2f, k), Mathf.Lerp(0.15f, 0.9f, k), 1f);
                ripple.color = Fade(accent, 0.85f * (1f - k));
                glint.color = Fade(accent, 0.6f * Mathf.Sin(k * Mathf.PI));
                bend = Mathf.Lerp(0.35f, 0.8f, Bump(k, 0f, 1f));
                tipNow = poseRod();
                hook.transform.position = pile + new Vector2(Mathf.Sin(k * 50f) * 0.05f, -dip * 0.5f);
                PoseLine(line, tipNow, hook.transform.position, 0.2f, fade);
                StepMotes(motes, Time.deltaTime);
            });
            Destroy(ripple.gameObject);
            card.gameObject.SetActive(true);

            // 3. THE REEL: four tugs. Each one loads the rod, snaps the line taut, jerks the card
            // up and leaves it swinging on the hook; the ratchet climbs in pitch.
            const int Tugs = 4;
            Vector2 from = fromPile;
            float swing = 0f;        // the card's pendulum angle, degrees
            float swingVel = 0f;
            float spin = 0f;
            for (int tug = 0; tug < Tugs; tug++)
            {
                Vector2 to = Vector2.Lerp(fromPile, hangLocal, (tug + 1f) / Tugs);
                float pitch = 1.8f + tug * 0.14f;
                Play(x => x.Pluck(pitch));
                Vector2 start = from;
                // The jerk sets the swing going, alternating sides.
                swingVel += (tug % 2 == 0 ? 1f : -1f) * 260f;
                float tension = 1f;
                float t = 0f;
                while (t < 0.13f)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    float k = Mathf.Clamp01(t / 0.085f);
                    Vector2 at = Vector2.LerpUnclamped(start, to, EaseOutBack(k, 1.6f));
                    // The pendulum: pulled back to hanging, damped.
                    swingVel += (-swing * 90f - swingVel * 5f) * dt;
                    swing += swingVel * dt;
                    card.transform.localPosition = at;
                    card.transform.localRotation = Quaternion.Euler(0f, 0f, swing * 0.35f);
                    Vector2 hookAt = cardLayer.TransformPoint(at + (Vector2)(Quaternion.Euler(0f, 0f, swing * 0.35f) * new Vector3(0f, 0.92f, 0f)));
                    hook.transform.position = hookAt;
                    Vector2 bob = hookAt + new Vector2(0f, 0.55f);
                    bobberTop.transform.position = bob + new Vector2(0f, 0.06f);
                    bobberBottom.transform.position = bob - new Vector2(0f, 0.06f);
                    tension = Mathf.Max(0f, tension - dt * 9f);
                    bend = 0.35f + 0.9f * tension;
                    tipNow = poseRod();
                    PoseLine(line, tipNow, hookAt, 0.25f * (1f - tension), fade);
                    spin += dt * 1100f * tension;
                    reelSpoke.transform.rotation = Quaternion.Euler(0f, 0f, -spin);
                    StepMotes(motes, dt);
                    yield return null;
                }
                // Drops shaken off the card as it comes up.
                if (tug < 2)
                {
                    Splash(motes, cardLayer.TransformPoint(card.transform.localPosition), accent, 3, order);
                }
                from = to;
            }

            // 4. LANDED: the card swings in to its slot, the hook lets go, the rod lifts away.
            Play(x => x.Chime(1.45f));
            StartCoroutine(Burst(cardLayer.TransformPoint(home), 1.6f, accent, order + 1, 0.3f));
            card.MoveTo(home, 0.15f, null);
            Vector2 hookFrom = hook.transform.position;
            float end = 0f;
            while (end < 0.22f)
            {
                float dt = Time.deltaTime;
                end += dt;
                float k = Mathf.Clamp01(end / 0.22f);
                swing *= 1f - Mathf.Clamp01(dt * 12f);
                card.transform.localRotation = Quaternion.Euler(0f, 0f, swing * 0.35f);
                bend = Mathf.Lerp(bend, 0f, dt * 8f);
                lift = k * k;
                fade = 1f - k;
                tipNow = poseRod();
                Vector2 at = Vector2.Lerp(hookFrom, tipNow, EaseOut(Mathf.Clamp01(k * 1.6f)));
                hook.transform.position = at;
                hook.color = Fade(steel, fade);
                bobberTop.transform.position = Vector2.Lerp(tipNow, at, 0.5f) + new Vector2(0f, 0.06f);
                bobberBottom.transform.position = Vector2.Lerp(tipNow, at, 0.5f) - new Vector2(0f, 0.06f);
                bobberTop.color = Fade(Paint, fade);
                bobberBottom.color = Fade(cork, fade);
                PoseLine(line, tipNow, at, 0.4f * k, fade);
                StepMotes(motes, dt);
                yield return null;
            }
            card.transform.localRotation = Quaternion.identity;
            ClearMotes(motes);
            foreach (SpriteRenderer r in line) Destroy(r.gameObject);
            foreach (SpriteRenderer r in rodParts) Destroy(r.gameObject);
            foreach (SpriteRenderer r in rings) Destroy(r.gameObject);
            Destroy(grip.gameObject);
            Destroy(reelBody.gameObject);
            Destroy(reelSpoke.gameObject);
            Destroy(hook.gameObject);
            Destroy(bobberTop.gameObject);
            Destroy(bobberBottom.gameObject);
            Destroy(glint.gameObject);
            running--;
        }

        /// <summary>An angle authored for the rod on the right (leaning left), mirrored about the
        /// vertical when the rod stands on the left.</summary>
        private static float Mirror(float angle, float mir)
        {
            return mir > 0f ? angle : 180f - angle;
        }

        /// <summary>The fishing line from the rod tip to the hook, as a sagging curve: slack 0 is
        /// taut and straight, 1 hangs well below the chord.</summary>
        private static void PoseLine(List<SpriteRenderer> line, Vector2 from, Vector2 to, float slack, float alpha)
        {
            float span = Vector2.Distance(from, to);
            Vector2 mid = (from + to) * 0.5f + Vector2.down * span * 0.35f * slack;
            Vector2 prev = from;
            for (int i = 0; i < line.Count; i++)
            {
                float k = (i + 1f) / line.Count;
                Vector2 p = (1f - k) * (1f - k) * from + 2f * (1f - k) * k * mid + k * k * to;
                Vector2 d = p - prev;
                line[i].transform.position = (prev + p) * 0.5f;
                line[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                line[i].size = new Vector2(Mathf.Max(0.02f, d.magnitude + 0.02f), 0.03f);
                line[i].color = Fade(line[i].color, 0.9f * alpha);
                prev = p;
            }
        }

        /// <summary>A few water drops thrown up and falling back.</summary>
        private void Splash(List<Mote> motes, Vector2 at, Color colour, int count, int order)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(40f, 140f, (i + 0.5f) / count) * Mathf.Deg2Rad;
                motes.Add(Mote.Make(this, ViewUtil.GlowSprite, at,
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(1.6f, 2.8f),
                    UnityEngine.Random.Range(0.12f, 0.2f), Color.Lerp(colour, Color.white, 0.4f), 0.9f,
                    order + 4, 0.5f, 9f, 0.45f));
            }
        }

        // ============================================================ BOARD STRIKES

        /// <summary>Which lead-in a board power plays before its cubes go.</summary>
        public enum Strike
        {
            Cross,      // "Çaprazlama": a crosshair locks on, beams run out along the arms
            Frame,      // "Çerçeve": a light traces the rim of the board once
            Bulldozer,  // "Buldozer": a blade drives through the band and flattens what is there
            EchoArm,    // "Eko", first use: listening waves
            EchoReplay, // "Eko", replay: the remembered explosion rings again on each cell
            MineBlast,  // "Mayın" on a cube: a mine drops, blinks, blows
            MineArm     // "Mayın" on an empty cell: a mine drops, blinks and settles in
        }

        /// <summary>
        /// A board power's own lead-in. <paramref name="doomed"/> are the cubes it destroyed, with
        /// the faces they wore (they stand as copies until the strike lands); <paramref name="impact"/>
        /// fires at that moment - the controller bursts them there. The bulldozer flattens its own
        /// and never calls impact. <paramref name="focus"/> is the targeted cell, when there is one;
        /// <paramref name="rows"/> says which way a bulldozer runs.
        /// </summary>
        public void PlayStrike(BoardView view, Strike kind, IList<CubeFace> doomed, GridPos? focus,
            bool rows, IList<GridPos> echoCells, Vector2 centre, float boardSize, Action impact)
        {
            StartCoroutine(StrikeRoutine(view, kind, doomed, focus, rows, echoCells, centre, boardSize, impact));
        }

        private IEnumerator StrikeRoutine(BoardView view, Strike kind, IList<CubeFace> doomed, GridPos? focus,
            bool rows, IList<GridPos> echoCells, Vector2 centre, float boardSize, Action impact)
        {
            running++;
            float cell = view.CellWorldSize;
            var copies = new List<SpriteRenderer>();
            foreach (CubeFace f in doomed)
            {
                SpriteRenderer c = ViewUtil.MakeCell(view.transform, "Doomed", view.CellToWorld(f.Cell), cell, f.Colour, 5);
                ViewUtil.ApplyTile(c, f.Tile, cell);
                c.color = f.Colour;
                copies.Add(c);
            }
            Func<GridPos, Vector2> world = p => view.transform.TransformPoint(view.CellToWorld(p));
            var motes = new List<Mote>();
            var temp = new List<SpriteRenderer>();
            bool struck = false;
            Action strike = () =>
            {
                if (struck)
                {
                    return;
                }
                struck = true;
                foreach (SpriteRenderer c in copies)
                {
                    if (c != null) Destroy(c.gameObject);
                }
                copies.Clear();
                if (impact != null)
                {
                    impact();
                }
            };

            if (kind == Strike.Cross && focus.HasValue)
            {
                // (Polished 2026-09-29.) A proper targeting LOCK and a proper BEAM: two counter-
                // turning rings close onto the cell with four brackets, the core pulses, and the
                // cross fires as a hot white core inside a wide warm glow on each arm - tapered,
                // running to the board's own edge - then breaks with a flash at the middle, a
                // burst where each arm ends and sparks thrown along the arms.
                Color red = new Color(1f, 0.42f, 0.3f);
                Color hot = new Color(1f, 0.96f, 0.86f);
                Vector2 at = world(focus.Value);
                SpriteRenderer halo = MakeShape(transform, "ReticleHalo", ViewUtil.GlowSprite, at, cell * 2f, red, BoardOrder + 5);
                SpriteRenderer ring = MakeShape(transform, "Reticle", Ring, at, cell * 2.4f, red, BoardOrder + 6);
                SpriteRenderer ring2 = MakeShape(transform, "Reticle2", Ring, at, cell * 3f, red, BoardOrder + 6);
                SpriteRenderer dot = MakeShape(transform, "ReticleDot", Diamond, at, cell * 0.3f, red, BoardOrder + 7);
                var ticks = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    ticks[i] = ViewUtil.MakeRounded(transform, "Tick", Vector2.zero, new Vector2(cell * 0.42f, cell * 0.09f), red, BoardOrder + 7);
                }
                temp.Add(halo); temp.Add(ring); temp.Add(ring2); temp.Add(dot); temp.AddRange(ticks);
                Play(x => x.Stretch());
                yield return Animate(0.3f, k =>
                {
                    float e = EaseOutBack(k, 1.5f);
                    ring.transform.localScale = Vector3.one * cell * Mathf.Lerp(3.4f, 1.3f, e);
                    ring.transform.rotation = Quaternion.Euler(0f, 0f, (1f - e) * 120f);
                    ring2.transform.localScale = Vector3.one * cell * Mathf.Lerp(4.2f, 1.7f, e);
                    ring2.transform.rotation = Quaternion.Euler(0f, 0f, -(1f - e) * 160f);
                    ring.color = Fade(red, k);
                    ring2.color = Fade(red, 0.45f * k);
                    halo.color = Fade(red, 0.25f * k);
                    float pulse = 1f + 0.25f * Mathf.Sin(k * Mathf.PI * 4f);
                    dot.transform.localScale = Vector3.one * cell * 0.3f * pulse;
                    dot.color = Fade(Color.Lerp(red, hot, k), k);
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 dir = Quaternion.Euler(0f, 0f, i * 90f + (1f - e) * 90f) * Vector3.right;
                        ticks[i].transform.position = at + dir * cell * Mathf.Lerp(1.7f, 0.72f, e);
                        ticks[i].transform.rotation = Quaternion.Euler(0f, 0f, i * 90f + 90f + (1f - e) * 90f);
                        ticks[i].color = Fade(red, k);
                    }
                });
                // The lock CLICKS: a snap of light on the core.
                Play(x => x.Pluck(2f));
                StartCoroutine(Burst(at, cell * 1.6f, hot, BoardOrder + 8, 0.16f));
                GameBoard crossBoard = view.Board;
                GridPos fc = focus.Value;
                float[] reach =
                {
                    crossBoard.MinX + crossBoard.Width - 1 - fc.X + 0.5f,   // right
                    crossBoard.MinY + crossBoard.Height - 1 - fc.Y + 0.5f,  // up
                    fc.X - crossBoard.MinX + 0.5f,                          // left
                    fc.Y - crossBoard.MinY + 0.5f                           // down
                };
                var glows = new SpriteRenderer[4];
                var cores = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    glows[i] = MakeShape(transform, "BeamGlow", Ray, at, 1f, Warm, BoardOrder + 6);
                    cores[i] = MakeShape(transform, "BeamCore", Ray, at, 1f, hot, BoardOrder + 7);
                    temp.Add(glows[i]);
                    temp.Add(cores[i]);
                }
                Play(x => x.Whoosh());
                Action<float, float> placeBeams = (float run, float width) =>
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float len = cell * Mathf.Max(0.3f, reach[i] * run) * 1.08f;
                        float angle = i * 90f - 90f; // the ray points up its own +y
                        float shimmer = 1f + 0.12f * Mathf.Sin(Time.time * 40f + i);
                        glows[i].transform.position = at;
                        glows[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        glows[i].transform.localScale = new Vector3(cell * 0.95f * width * shimmer, len, 1f);
                        cores[i].transform.position = at;
                        cores[i].transform.rotation = Quaternion.Euler(0f, 0f, angle);
                        cores[i].transform.localScale = new Vector3(cell * 0.28f * width, len, 1f);
                        glows[i].color = Fade(Warm, 0.75f);
                        cores[i].color = Fade(hot, 1f);
                    }
                };
                yield return Animate(0.22f, k => placeBeams(EaseOut(k), 1f));
                strike();
                // The break: flash in the middle, a burst at the end of every arm, sparks.
                Play(x => x.Drum(1.5f));
                StartCoroutine(Burst(at, cell * 3.2f, hot, BoardOrder + 8, 0.28f));
                for (int i = 0; i < 4; i++)
                {
                    Vector2 dir = Quaternion.Euler(0f, 0f, i * 90f) * Vector3.right;
                    StartCoroutine(Burst(at + dir * cell * reach[i], cell * 1.8f, Warm, BoardOrder + 7, 0.3f));
                }
                var sparks = new List<SpriteRenderer>();
                var sparkV = new List<Vector2>();
                for (int i = 0; i < 16; i++)
                {
                    Vector2 dir = Quaternion.Euler(0f, 0f, (i % 4) * 90f + ((i * 37) % 30 - 15)) * Vector3.right;
                    SpriteRenderer sp = MakeShape(transform, "CrossSpark", ViewUtil.GlowSprite, at, cell * 0.16f, hot, BoardOrder + 8);
                    sparks.Add(sp);
                    sparkV.Add(dir * cell * (5f + (i % 3) * 2.5f));
                    temp.Add(sp);
                }
                yield return Animate(0.3f, k =>
                {
                    placeBeams(1f, 1f + 0.8f * k);
                    for (int i = 0; i < 4; i++)
                    {
                        glows[i].color = Fade(Warm, 0.75f * (1f - k));
                        cores[i].color = Fade(hot, 1f - k * k);
                    }
                    for (int i = 0; i < sparks.Count; i++)
                    {
                        sparkV[i] *= 0.9f;
                        sparks[i].transform.position += (Vector3)(sparkV[i] * Time.deltaTime);
                        sparks[i].color = Fade(Color.Lerp(hot, red, k), 1f - k);
                    }
                    ring.color = Fade(red, 1f - k);
                    ring2.color = Fade(red, 0.45f * (1f - k));
                    halo.color = Fade(red, 0.25f * (1f - k));
                    dot.color = Fade(hot, 1f - k);
                    foreach (SpriteRenderer t in ticks) t.color = Fade(red, 1f - k);
                });
            }
            else if (kind == Strike.Frame)
            {
                // A light runs once round the rim, and the rim goes as it closes the loop.
                GameBoard board = view.Board;
                Vector2 a = world(new GridPos(board.MinX, board.MinY));
                Vector2 b = world(new GridPos(board.MinX + board.Width - 1, board.MinY + board.Height - 1));
                Rect r = Rect.MinMaxRect(a.x - cell * 0.5f, a.y - cell * 0.5f, b.x + cell * 0.5f, b.y + cell * 0.5f);
                Vector2[] corners = { new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin) };
                float perim = 2f * (r.width + r.height);
                var sides = new SpriteRenderer[4];
                for (int i = 0; i < 4; i++)
                {
                    sides[i] = ViewUtil.MakeRounded(transform, "FrameSide", Vector2.zero, new Vector2(0.05f, cell * 0.14f), Gold, BoardOrder + 6);
                    temp.Add(sides[i]);
                }
                SpriteRenderer head = MakeShape(transform, "FrameHead", ViewUtil.GlowSprite, corners[0], cell * 1.4f, Warm, BoardOrder + 7);
                temp.Add(head);
                Play(x => x.Whoosh());
                yield return Animate(0.55f, k =>
                {
                    float d = EaseOut(k) * perim;
                    float walked = 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 p0 = corners[i];
                        Vector2 p1 = corners[(i + 1) % 4];
                        float len = Vector2.Distance(p0, p1);
                        float part = Mathf.Clamp(d - walked, 0f, len);
                        Vector2 end = Vector2.Lerp(p0, p1, len > 0f ? part / len : 0f);
                        Vector2 dd = end - p0;
                        sides[i].transform.position = (p0 + end) * 0.5f;
                        sides[i].transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p1.y - p0.y, p1.x - p0.x) * Mathf.Rad2Deg);
                        sides[i].size = new Vector2(Mathf.Max(0.02f, dd.magnitude), cell * 0.14f);
                        sides[i].enabled = part > 0f;
                        if (d >= walked && d <= walked + len)
                        {
                            head.transform.position = end;
                        }
                        walked += len;
                    }
                });
                Play(x => x.Drum(1.2f));
                // The frame clamps in and the rim goes.
                yield return Animate(0.12f, k =>
                {
                    foreach (SpriteRenderer s in sides) s.color = Fade(Warm, 1f);
                    head.color = Fade(Warm, 1f - k);
                });
                strike();
                yield return Animate(0.25f, k =>
                {
                    foreach (SpriteRenderer s in sides) s.color = Fade(Gold, 1f - k);
                });
            }
            else if (kind == Strike.Bulldozer)
            {
                // The blade: a yellow plate with hazard stripes, driven across the band.
                Color yellow = new Color(0.98f, 0.76f, 0.2f);
                float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue;
                foreach (CubeFace f in doomed)
                {
                    Vector2 p = world(f.Cell);
                    float along = rows ? p.x : p.y;
                    float across = rows ? p.y : p.x;
                    minA = Mathf.Min(minA, along); maxA = Mathf.Max(maxA, along);
                    minB = Mathf.Min(minB, across); maxB = Mathf.Max(maxB, across);
                }
                GameBoard board = view.Board;
                Vector2 lo = world(new GridPos(board.MinX, board.MinY));
                Vector2 hi = world(new GridPos(board.MinX + board.Width - 1, board.MinY + board.Height - 1));
                float startA = (rows ? lo.x : lo.y) - cell * 1.5f;
                float endA = (rows ? hi.x : hi.y) + cell * 1.5f;
                float mid = (minB + maxB) * 0.5f;
                float bladeLen = (maxB - minB) + cell * 1.4f;
                var blade = new GameObject("Blade").transform;
                blade.SetParent(transform, false);
                blade.rotation = Quaternion.Euler(0f, 0f, rows ? 0f : 90f);
                SpriteRenderer plate = MakeRound(blade, "Plate", Vector2.zero, new Vector2(cell * 0.45f, bladeLen), yellow, 6);
                SpriteRenderer edge = MakeRound(blade, "Edge", new Vector2(cell * 0.25f, 0f), new Vector2(cell * 0.1f, bladeLen), Carving, 7);
                for (int i = 0; i < 5; i++)
                {
                    SpriteRenderer stripe = MakeRound(blade, "Stripe", new Vector2(0f, -bladeLen * 0.4f + i * bladeLen * 0.2f),
                        new Vector2(cell * 0.4f, cell * 0.12f), Carving, 7);
                    stripe.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                }
                Play(x => x.Rumble());
                var squashed = new bool[copies.Count];
                float travel = 0.75f;
                float t = 0f;
                while (t < travel + 0.25f)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    float k = Mathf.Clamp01(t / travel);
                    float a = Mathf.Lerp(startA, endA, k);
                    Vector2 bladeAt = rows ? new Vector2(a, mid) : new Vector2(mid, a);
                    blade.position = bladeAt + new Vector2(Mathf.Sin(t * 60f), Mathf.Cos(t * 53f)) * cell * 0.02f;
                    float fade = 1f - Mathf.Clamp01((t - travel) / 0.25f);
                    foreach (SpriteRenderer r in blade.GetComponentsInChildren<SpriteRenderer>())
                    {
                        r.color = Fade(r.color, fade);
                    }
                    for (int i = 0; i < copies.Count; i++)
                    {
                        if (copies[i] == null)
                        {
                            continue;
                        }
                        Vector2 p = world(doomed[i].Cell);
                        float along = rows ? p.x : p.y;
                        float behind = a - along; // > 0 once the blade has reached it
                        if (behind < -cell * 0.5f)
                        {
                            continue;
                        }
                        if (!squashed[i])
                        {
                            squashed[i] = true;
                            Play(x => x.Squish());
                            for (int m = 0; m < 3; m++)
                            {
                                motes.Add(Mote.Make(this, ViewUtil.GlowSprite, p,
                                    (rows ? new Vector2(1f, UnityEngine.Random.Range(-0.8f, 0.8f)) : new Vector2(UnityEngine.Random.Range(-0.8f, 0.8f), 1f)) * cell * 2.5f,
                                    cell * 0.6f, Dust, 0.5f, BoardOrder + 5, 3f, 0f, 0.5f));
                            }
                        }
                        // Flattened against the blade: pushed with it, squashed along its travel.
                        float s = Mathf.Clamp01((behind + cell * 0.5f) / (cell * 0.8f));
                        Vector2 home = view.CellToWorld(doomed[i].Cell);
                        Vector2 push = (rows ? Vector2.right : Vector2.up) * cell * 0.4f * s;
                        copies[i].transform.localPosition = home + push;
                        float flat = Mathf.Lerp(1f, 0.08f, s);
                        float wide = Mathf.Lerp(1f, 1.25f, s);
                        copies[i].transform.localScale = rows ? new Vector3(cell * flat, cell * wide, 1f) : new Vector3(cell * wide, cell * flat, 1f);
                        copies[i].color = Fade(Color.Lerp(doomed[i].Colour, Dust, s * 0.6f), 1f - Mathf.Clamp01((s - 0.7f) / 0.3f));
                    }
                    StepMotes(motes, dt);
                    yield return null;
                }
                Destroy(blade.gameObject);
                struck = true; // flattened, never burst
                foreach (SpriteRenderer c in copies) if (c != null) Destroy(c.gameObject);
                copies.Clear();
            }
            else if (kind == Strike.EchoArm)
            {
                // Listening: an ear-shaped set of arcs pulsing inward over the board.
                Color teal = new Color(0.55f, 1f, 0.85f);
                FloatingTextFx.Spawn(transform, centre + new Vector2(0f, boardSize * 0.1f),
                    Loc.Pick("LISTENING...", "DİNLİYOR..."), teal, 56, 0.06f);
                Play(x => x.Chime(0.8f));
                for (int wave = 0; wave < 3; wave++)
                {
                    SpriteRenderer ring = MakeShape(transform, "Listen", Ring, centre, boardSize, teal, BoardOrder + 4);
                    temp.Add(ring);
                }
                yield return Animate(0.9f, k =>
                {
                    for (int w = 0; w < temp.Count; w++)
                    {
                        float kk = Mathf.Clamp01(k * 1.6f - w * 0.3f);
                        temp[w].transform.localScale = Vector3.one * boardSize * Mathf.Lerp(1.3f, 0.1f, kk);
                        temp[w].color = Fade(teal, kk <= 0f ? 0f : 0.5f * Mathf.Sin(kk * Mathf.PI));
                    }
                });
            }
            else if (kind == Strike.EchoReplay)
            {
                // The echo: each remembered cell rings three times, fainter each time, then goes.
                Color teal = new Color(0.55f, 1f, 0.85f);
                Play(x => x.Chime(0.8f));
                // Every REMEMBERED cell rings - also the ones that stand empty now.
                var ringAt = new List<GridPos>();
                if (echoCells != null)
                {
                    ringAt.AddRange(echoCells);
                }
                foreach (CubeFace f in doomed)
                {
                    if (!ringAt.Contains(f.Cell)) ringAt.Add(f.Cell);
                }
                for (int i = 0; i < ringAt.Count; i++)
                {
                    for (int w = 0; w < 3; w++)
                    {
                        temp.Add(MakeShape(transform, "Echo", Ring, world(ringAt[i]), cell, teal, BoardOrder + 6));
                    }
                }
                float t = 0f;
                int rung = 0;
                while (t < 0.6f)
                {
                    float dt = Time.deltaTime;
                    t += dt;
                    for (int w = 0; w < 3; w++)
                    {
                        float kk = Mathf.Clamp01((t - w * 0.15f) / 0.3f);
                        for (int i = 0; i < ringAt.Count; i++)
                        {
                            SpriteRenderer ring = temp[i * 3 + w];
                            ring.transform.localScale = Vector3.one * cell * Mathf.Lerp(0.4f, 1.8f, kk);
                            ring.color = Fade(teal, kk <= 0f || kk >= 1f ? 0f : (0.8f - w * 0.25f) * (1f - kk));
                        }
                        if (rung == w && t >= w * 0.15f)
                        {
                            rung++;
                            float pitch = 1.4f - w * 0.15f;
                            Play(x => x.Pluck(pitch));
                        }
                    }
                    foreach (SpriteRenderer c in copies)
                    {
                        if (c != null) c.color = Fade(Color.Lerp(c.color, teal, dt * 2f), 1f);
                    }
                    yield return null;
                }
                strike();
            }
            else if ((kind == Strike.MineBlast || kind == Strike.MineArm) && focus.HasValue)
            {
                Vector2 at = world(focus.Value);
                Color red = new Color(1f, 0.25f, 0.2f);
                var mine = new GameObject("Mine").transform;
                mine.SetParent(transform, false);
                SpriteRenderer shell = MakeShape(mine, "Shell", Disc, Vector2.zero, cell * 0.62f, new Color(0.22f, 0.24f, 0.28f), BoardOrder + 7);
                shell.transform.localPosition = Vector3.zero;
                for (int i = 0; i < 8; i++)
                {
                    SpriteRenderer spike = MakeShape(mine, "Spike", Diamond, Vector2.zero, cell * 0.18f, new Color(0.35f, 0.37f, 0.42f), BoardOrder + 6);
                    float a = i * 45f * Mathf.Deg2Rad;
                    spike.transform.localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * 0.33f;
                }
                SpriteRenderer lamp = MakeShape(mine, "Lamp", ViewUtil.GlowSprite, Vector2.zero, cell * 0.5f, red, BoardOrder + 8);
                lamp.transform.localPosition = new Vector2(0f, cell * 0.05f);
                SpriteRenderer shadow = MakeShape(transform, "MineShadow", Disc, at, cell * 0.5f, new Color(0f, 0f, 0f, 0.35f), BoardOrder + 5);
                temp.Add(shadow);
                Play(x => x.Whoosh());
                // It drops in, bounces once.
                yield return Animate(0.28f, k =>
                {
                    float h = (1f - k) * (1f - k) * cell * 3f + Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2f)) * (1f - k) * cell * 0.2f;
                    mine.position = at + new Vector2(0f, h);
                    shadow.transform.localScale = Vector3.one * cell * Mathf.Lerp(0.2f, 0.55f, k);
                    lamp.color = Clear(red);
                });
                Play(x => x.Drum(1.5f));
                // Two blinks and beeps, the second faster.
                for (int blink = 0; blink < 3; blink++)
                {
                    float gap = blink == 0 ? 0.16f : 0.1f;
                    Play(x => x.Pluck(2.6f));
                    yield return Animate(gap, k =>
                    {
                        lamp.color = Fade(red, Mathf.Sin(k * Mathf.PI));
                        mine.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(k * Mathf.PI));
                    });
                }
                if (kind == Strike.MineBlast)
                {
                    Destroy(mine.gameObject);
                    strike();
                }
                else
                {
                    // Armed: it settles into the cell and hides (the board draws the mine now).
                    yield return Animate(0.2f, k =>
                    {
                        mine.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
                        foreach (SpriteRenderer r in mine.GetComponentsInChildren<SpriteRenderer>())
                        {
                            r.color = Fade(r.color, 1f - k);
                        }
                        shadow.color = Fade(shadow.color, 0.35f * (1f - k));
                    });
                    Destroy(mine.gameObject);
                }
            }
            strike();
            ClearMotes(motes);
            foreach (SpriteRenderer r in temp)
            {
                if (r != null) Destroy(r.gameObject);
            }
            running--;
        }

        /// <summary>"Eko" has just RECORDED an explosion: on each remembered cell a ring rings out
        /// twice, fainter the second time, and a line of teal motes runs from every cell to the
        /// board's middle, where "ECHO RECORDED" is said. Short, and it does not hold the input -
        /// it plays alongside the explosion it heard.</summary>
        public void PlayEchoRecord(IList<Vector2> cells, float cell, Vector2 centre)
        {
            StartCoroutine(EchoRecord(cells, cell, centre));
        }

        private IEnumerator EchoRecord(IList<Vector2> cells, float cell, Vector2 centre)
        {
            Color teal = new Color(0.55f, 1f, 0.85f);
            yield return new WaitForSeconds(0.3f); // after the explosion it heard
            var rings = new List<SpriteRenderer>();
            var motes = new List<Mote>();
            foreach (Vector2 c in cells)
            {
                rings.Add(MakeShape(transform, "EchoHeard", Ring, c, cell, teal, BoardOrder + 6));
                rings.Add(MakeShape(transform, "EchoHeard", Ring, c, cell, teal, BoardOrder + 6));
                Vector2 toward = (centre - c) * 1.6f;
                motes.Add(Mote.Make(this, Diamond, c, toward, cell * 0.14f, teal, 1f, BoardOrder + 7, 1.5f, 0f, 0.55f));
            }
            Play(x => x.Chime(0.85f));
            FloatingTextFx.Spawn(transform, centre + new Vector2(0f, cell * 1.2f),
                Loc.Pick("ECHO RECORDED", "YANKI KAYDEDİLDİ"), teal, 54, 0.06f);
            bool second = false;
            float t = 0f;
            while (t < 0.6f)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < rings.Count; i++)
                {
                    float kk = Mathf.Clamp01((t - (i % 2) * 0.2f) / 0.35f);
                    rings[i].transform.localScale = Vector3.one * cell * Mathf.Lerp(0.5f, 1.7f, kk);
                    rings[i].color = Fade(teal, kk <= 0f || kk >= 1f ? 0f : (i % 2 == 0 ? 0.8f : 0.4f) * (1f - kk));
                }
                if (!second && t >= 0.2f)
                {
                    second = true;
                    Play(x => x.Pluck(1.2f));
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer r in rings) Destroy(r.gameObject);
        }

        // ============================================================ HAND POWERS

        /// <summary>"Cımbız": tweezers close on the card and TWIST it - the card's new shape turns
        /// in from where it was, with a snap.</summary>
        public void PlayTweezers(CardVisual card, int order, int steps)
        {
            StartCoroutine(Tweezers(card, order, steps));
        }

        private IEnumerator Tweezers(CardVisual card, int order, int steps)
        {
            running++;
            float scale = card.transform.lossyScale.x;
            Vector2 at = card.transform.position;
            float h = CardVisual.BodyHeight * scale;
            Color steel = new Color(0.8f, 0.84f, 0.9f);
            var prongs = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                prongs[i] = ViewUtil.MakeRounded(transform, "Prong", Vector2.zero, new Vector2(0.11f, h * 0.7f), steel, order + 3);
            }
            SpriteRenderer hinge = MakeShape(transform, "Hinge", Disc, at, 0.22f, steel, order + 3);
            Play(x => x.Pluck(1.9f));
            // Down onto the card, prongs open, then they pinch.
            yield return Animate(0.16f, k =>
            {
                float e = EaseOut(k);
                Vector2 top = at + new Vector2(0f, h * Mathf.Lerp(1.3f, 0.75f, e));
                hinge.transform.position = top;
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    float open = Mathf.Lerp(0.28f, 0.2f, e);
                    prongs[i].transform.position = top + new Vector2(side * open * 0.5f, -h * 0.32f);
                    prongs[i].transform.rotation = Quaternion.Euler(0f, 0f, side * 8f);
                    prongs[i].color = Fade(steel, k);
                }
                hinge.color = Fade(steel, k);
            });
            Play(x => x.Pluck(2.4f));
            // The twist: the rotated card turns in from a quarter turn back, and the tweezers turn
            // with it.
            // The new shape turns in from where the old one stood: clockwise steps come in from a
            // positive (anticlockwise) angle, three steps is one quarter the other way.
            // Steps 0 is the pick having been PREVIEWED already: the tweezers only set it, with a
            // small counter-twist that snaps home.
            steps = ((steps % 4) + 4) % 4;
            float from = steps == 0 ? 14f : steps == 3 ? -90f : 90f * steps;
            yield return Animate(0.18f + 0.08f * Mathf.Abs(from) / 90f, k =>
            {
                float angle = from * (1f - EaseOutBack(k, 1.8f));
                if (card != null)
                {
                    card.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
                Quaternion turn = Quaternion.Euler(0f, 0f, angle - from);
                Vector2 top = at + (Vector2)(turn * new Vector3(0f, h * 0.75f, 0f));
                hinge.transform.position = top;
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    prongs[i].transform.position = top + (Vector2)(turn * new Vector3(side * 0.1f, -h * 0.32f, 0f));
                    prongs[i].transform.rotation = turn;
                }
            });
            if (card != null)
            {
                card.transform.localRotation = Quaternion.identity;
            }
            Play(x => x.Drum(1.8f));
            yield return Animate(0.14f, k =>
            {
                foreach (SpriteRenderer p in prongs)
                {
                    p.transform.position += Vector3.up * Time.deltaTime * 6f;
                    p.color = Fade(steel, 1f - k);
                }
                hinge.transform.position += Vector3.up * Time.deltaTime * 6f;
                hinge.color = Fade(steel, 1f - k);
            });
            foreach (SpriteRenderer p in prongs) Destroy(p.gameObject);
            Destroy(hinge.gameObject);
            running--;
        }

        /// <summary>"Cımbız" locking in: the tweezers come down onto the card's block and HOLD it
        /// for <paramref name="hold"/> seconds while the card is turned upright under it, then
        /// lift away.</summary>
        public void PlayTweezerGrip(CardVisual card, int order, float hold)
        {
            StartCoroutine(TweezerGrip(card, order, hold));
        }

        private IEnumerator TweezerGrip(CardVisual card, int order, float hold)
        {
            float scale = card.transform.lossyScale.x;
            Vector2 at = card.transform.position;
            float h = CardVisual.BodyHeight * scale;
            Color steel = new Color(0.8f, 0.84f, 0.9f);
            var prongs = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                prongs[i] = ViewUtil.MakeRounded(transform, "Prong", Vector2.zero, new Vector2(0.11f, h * 0.55f), steel, order + 3);
            }
            SpriteRenderer hinge = MakeShape(transform, "Hinge", Disc, at, 0.22f, steel, order + 3);
            Action<float, float> pose = (drop, open) =>
            {
                Vector2 top = at + new Vector2(0f, h * Mathf.Lerp(1.2f, 0.55f, drop));
                hinge.transform.position = top;
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    prongs[i].transform.position = top + new Vector2(side * open, -h * 0.25f);
                    prongs[i].transform.rotation = Quaternion.Euler(0f, 0f, side * 6f);
                }
            };
            yield return Animate(0.1f, k =>
            {
                pose(EaseOut(k), Mathf.Lerp(0.3f, 0.12f, k));
                foreach (SpriteRenderer p in prongs) p.color = Fade(steel, k);
                hinge.color = Fade(steel, k);
            });
            yield return new WaitForSeconds(Mathf.Max(0f, hold - 0.1f));
            yield return Animate(0.14f, k =>
            {
                pose(1f - k, Mathf.Lerp(0.12f, 0.3f, k));
                foreach (SpriteRenderer p in prongs) p.color = Fade(steel, 1f - k);
                hinge.color = Fade(steel, 1f - k);
            });
            foreach (SpriteRenderer p in prongs) Destroy(p.gameObject);
            Destroy(hinge.gameObject);
        }

        /// <summary>"Büyüteç": a magnifying glass sweeps across the draw pile, catches the light and
        /// holds over it for a beat.</summary>
        public void PlayMagnifier(Vector2 pile, float size, int order)
        {
            StartCoroutine(Magnifier(pile, size, order));
        }

        private IEnumerator Magnifier(Vector2 pile, float size, int order)
        {
            running++;
            var glass = new GameObject("Magnifier").transform;
            glass.SetParent(transform, false);
            SpriteRenderer lens = MakeShape(glass, "Lens", ViewUtil.GlowSprite, Vector2.zero, size, new Color(0.75f, 0.92f, 1f), order + 2);
            lens.transform.localPosition = Vector3.zero;
            SpriteRenderer rim = MakeShape(glass, "Rim", Ring, Vector2.zero, size * 1.1f, Gold, order + 3);
            rim.transform.localPosition = Vector3.zero;
            SpriteRenderer handle = ViewUtil.MakeRounded(glass, "Handle", new Vector2(size * 0.5f, -size * 0.5f), new Vector2(size * 0.18f, size * 0.7f), WoodDark, order + 2);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            SpriteRenderer glint = MakeShape(glass, "Glint", Diamond, Vector2.zero, size * 0.16f, Color.white, order + 4);
            glint.transform.localPosition = new Vector2(-size * 0.2f, size * 0.2f);
            Play(x => x.Whoosh());
            Vector2 from = pile + new Vector2(-size * 1.6f, size * 0.6f);
            yield return Animate(0.5f, k =>
            {
                float e = EaseOutBack(k, 1.2f);
                glass.position = Vector2.LerpUnclamped(from, pile, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI * 2f) * size * 0.08f);
                glass.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.15f, e);
                lens.color = Fade(new Color(0.75f, 0.92f, 1f), 0.35f * k);
                rim.color = Fade(Gold, k);
                handle.color = Fade(WoodDark, k);
                glint.color = Fade(Color.white, 0.9f * Mathf.Sin(k * Mathf.PI));
            });
            Play(x => x.Chime(1.7f));
            FloatingTextFx.Spawn(transform, pile + new Vector2(0f, size * 0.9f), "+2", Gold, 60, 0.07f);
            yield return Animate(0.35f, k =>
            {
                float fade = 1f - k;
                lens.color = Fade(lens.color, 0.35f * fade);
                rim.color = Fade(Gold, fade);
                handle.color = Fade(WoodDark, fade);
                glass.localScale = Vector3.one * (1.15f + 0.1f * k);
            });
            Destroy(glass.gameObject);
            running--;
        }

        /// <summary>"Transfer": the discard's top card and the draw pile's top card arc past each
        /// other and trade places. World positions and a card-back size.</summary>
        public void PlaySwapPiles(Vector2 drawPile, Vector2 discardPile, Vector2 cardSize, int order)
        {
            StartCoroutine(SwapPiles(drawPile, discardPile, cardSize, order));
        }

        private IEnumerator SwapPiles(Vector2 drawPile, Vector2 discardPile, Vector2 cardSize, int order)
        {
            running++;
            Color back = new Color(0.16f, 0.2f, 0.38f);
            Color trim = new Color(0.86f, 0.68f, 0.3f);
            SpriteRenderer[] a = MakeBack(cardSize, back, trim, drawPile, order + 3);
            SpriteRenderer[] b = MakeBack(cardSize, new Color(0.9f, 0.88f, 0.82f), new Color(0.55f, 0.5f, 0.42f), discardPile, order + 3);
            Play(x => x.Whoosh());
            yield return Animate(0.42f, k =>
            {
                float e = k * k * (3f - 2f * k);
                float arc = Mathf.Sin(k * Mathf.PI) * cardSize.y * 0.9f;
                PlaceBack(a, Vector2.Lerp(drawPile, discardPile, e) + new Vector2(0f, arc), Mathf.Sin(k * Mathf.PI) * 18f);
                PlaceBack(b, Vector2.Lerp(discardPile, drawPile, e) - new Vector2(0f, arc * 0.6f), -Mathf.Sin(k * Mathf.PI) * 18f);
            });
            Play(x => x.Pluck(1.6f));
            StartCoroutine(Burst(drawPile, cardSize.x * 1.8f, Cold, order + 1, 0.25f));
            StartCoroutine(Burst(discardPile, cardSize.x * 1.8f, Warm, order + 1, 0.25f));
            foreach (SpriteRenderer r in a) Destroy(r.gameObject);
            foreach (SpriteRenderer r in b) Destroy(r.gameObject);
            running--;
        }

        /// <summary>"Öteki Dünya": a silver seam opens across the board and sweeps down it, a
        /// reflection of the arena shimmering after it - the world beneath has been opened.</summary>
        public void PlayMirror(Vector2 centre, float boardSize)
        {
            StartCoroutine(Mirror(centre, boardSize));
        }

        private IEnumerator Mirror(Vector2 centre, float boardSize)
        {
            running++;
            Color silver = new Color(0.85f, 0.9f, 1f);
            Color violet = new Color(0.7f, 0.55f, 1f);
            SpriteRenderer seam = ViewUtil.MakeRounded(transform, "MirrorSeam", Vector2.zero, new Vector2(boardSize * 1.05f, boardSize * 0.02f), silver, BoardOrder + 7);
            SpriteRenderer sheen = MakeShape(transform, "MirrorSheen", ViewUtil.GlowSprite, centre, 1f, violet, BoardOrder + 5);
            var ripples = new List<SpriteRenderer>();
            for (int i = 0; i < 3; i++)
            {
                ripples.Add(MakeShape(transform, "MirrorRipple", Ring, centre, 0.1f, silver, BoardOrder + 6));
            }
            var motes = new List<Mote>();
            Play(x => x.Stretch());
            float top = centre.y + boardSize * 0.5f;
            float bottom = centre.y - boardSize * 0.5f;
            yield return Animate(0.55f, k =>
            {
                float e = EaseOut(k);
                float y = Mathf.Lerp(top, bottom, e);
                seam.transform.position = new Vector2(centre.x, y);
                seam.color = Fade(silver, Mathf.Sin(k * Mathf.PI) * 0.9f + 0.1f);
                sheen.transform.position = new Vector2(centre.x, (top + y) * 0.5f);
                sheen.transform.localScale = new Vector3(boardSize * 1.2f, Mathf.Max(0.1f, top - y) * 1.1f, 1f);
                sheen.color = Fade(violet, 0.22f * Mathf.Sin(k * Mathf.PI));
                if (UnityEngine.Random.value < Time.deltaTime * 40f)
                {
                    motes.Add(Mote.Make(this, Diamond, new Vector2(centre.x + UnityEngine.Random.Range(-0.5f, 0.5f) * boardSize, y),
                        new Vector2(0f, -1.5f), boardSize * 0.015f, silver, 1f, BoardOrder + 8, 1f, 0f, 0.4f));
                }
                StepMotes(motes, Time.deltaTime);
            });
            Play(x => x.Chime(0.7f));
            FloatingTextFx.Spawn(transform, centre, Loc.Pick("THE OTHER WORLD", "ÖTEKİ DÜNYA"), violet, 60, 0.07f);
            yield return Animate(0.5f, k =>
            {
                for (int i = 0; i < ripples.Count; i++)
                {
                    float kk = Mathf.Clamp01(k * 1.5f - i * 0.25f);
                    ripples[i].transform.localScale = Vector3.one * boardSize * Mathf.Lerp(0.1f, 1.4f, kk);
                    ripples[i].color = Fade(silver, kk <= 0f ? 0f : 0.5f * (1f - kk));
                }
                seam.color = Fade(silver, 1f - k);
                StepMotes(motes, Time.deltaTime);
            });
            ClearMotes(motes);
            Destroy(seam.gameObject);
            Destroy(sheen.gameObject);
            foreach (SpriteRenderer r in ripples) Destroy(r.gameObject);
            running--;
        }

        // ================================================================ REWIND

        /// <summary>"Kum Saati": an hourglass turns over above the board and time runs back. The
        /// cubes the last turns PLACED are pulled back up out of their cells (last in, first
        /// out), and the cubes those turns DESTROYED fly back together from their pieces.
        /// <paramref name="leaving"/> are faces taken before the rewind; <paramref name="returning"/>
        /// faces of the rewound board, whose cells are held blank until they land.</summary>
        public void PlayRewind(BoardView view, IList<CubeFace> leaving, IList<CubeFace> returning,
            Vector2 centre, float boardSize)
        {
            StartCoroutine(Rewind(view, leaving, returning, centre, boardSize));
        }

        private IEnumerator Rewind(BoardView view, IList<CubeFace> leaving, IList<CubeFace> returning,
            Vector2 centre, float boardSize)
        {
            running++;
            float cell = view.CellWorldSize;
            float u = boardSize / 9f;
            var held = new List<GridPos>();
            foreach (CubeFace f in returning)
            {
                held.Add(f.Cell);
            }
            view.HoldCells(held);
            Color blue = new Color(0.62f, 0.8f, 1f);
            Color sand = new Color(0.96f, 0.8f, 0.46f);
            Color sandDeep = new Color(0.82f, 0.6f, 0.3f);

            // ---- THE HOURGLASS ----
            // The FRAME and the GLASS turn over; the SAND never does. Sand is drawn in world space
            // against gravity, which is why it used to break: parented to the glass, the "bottom"
            // heap went upside down with the flip and drained UPWARD at the top, and the stream sat
            // at the waist doing nothing. Now: before the flip the sand turns WITH the glass (it is
            // held in its bulb), and from the moment it lands, the top bulb empties at the neck, a
            // real stream of falling grains runs down, and a mound builds in the bottom bulb.
            Vector2 glassAt = centre + new Vector2(0f, boardSize * 0.08f);
            var glass = new GameObject("Hourglass").transform;
            glass.SetParent(transform, false);
            glass.position = glassAt;
            var frame = new List<Part>();
            float capW = u * 2.0f, capH = u * 0.26f, bulbH = u * 1.15f, bulbW = u * 1.45f;
            float capY = bulbH + capH * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                frame.Add(new Part(MakeRound(glass, "Cap", new Vector2(0f, side * capY), new Vector2(capW, capH), WoodDark, 3), WoodDark));
                frame.Add(new Part(MakeRound(glass, "CapLip", new Vector2(0f, side * (capY - capH * 0.42f)), new Vector2(capW * 0.92f, capH * 0.22f), WoodHigh, 4), WoodHigh));
                frame.Add(new Part(MakeRound(glass, "Post", new Vector2(side * u * 0.88f, 0f), new Vector2(u * 0.13f, bulbH * 2f), WoodMid, 2), WoodMid));
                frame.Add(new Part(MakeRound(glass, "PostShine", new Vector2(side * u * 0.88f - u * 0.03f, 0f), new Vector2(u * 0.035f, bulbH * 1.9f), WoodLight, 3), WoodLight));
            }
            Color glassTint = new Color(0.8f, 0.92f, 1f, 0.22f);
            Color glassEdge = new Color(0.85f, 0.95f, 1f, 0.55f);
            for (int side = -1; side <= 1; side += 2)
            {
                // A slightly larger pale triangle behind the body is the glass's EDGE.
                SpriteRenderer edge = MakeShape(glass, "BulbEdge", Tri, Vector2.zero, 1f, glassEdge, BoardOrder + 2);
                edge.transform.localPosition = new Vector2(0f, side * bulbH * 0.5f);
                edge.transform.localScale = new Vector3(bulbW * 1.08f, -side * bulbH * 1.04f, 1f);
                SpriteRenderer body = MakeShape(glass, "Bulb", Tri, Vector2.zero, 1f, glassTint, BoardOrder + 3);
                body.transform.localPosition = new Vector2(0f, side * bulbH * 0.5f);
                body.transform.localScale = new Vector3(bulbW, -side * bulbH, 1f);
                frame.Add(new Part(edge, glassEdge));
                frame.Add(new Part(body, glassTint));
                // A streak of reflection down one slope.
                Color shine = new Color(1f, 1f, 1f, 0.5f);
                SpriteRenderer streak = MakeRound(glass, "Shine", new Vector2(-bulbW * 0.2f, side * bulbH * 0.55f), new Vector2(u * 0.05f, bulbH * 0.55f), shine, 6);
                streak.transform.localRotation = Quaternion.Euler(0f, 0f, side * -28f);
                frame.Add(new Part(streak, shine));
            }
            // Sand held in the bottom bulb, turning WITH the glass until the flip lands.
            float fullH = bulbH * 0.62f;
            SpriteRenderer carried = MakeShape(glass, "SandHeld", Tri, Vector2.zero, 1f, sand, BoardOrder + 4);
            carried.transform.localPosition = new Vector2(0f, -bulbH + fullH * 0.5f);
            carried.transform.localScale = new Vector3(bulbW * 0.78f, fullH, 1f);
            // World-space sand for after the flip: the top pile (apex at the neck, draining down
            // into it), the bottom mound (building), the stream and its grains.
            var sandRoot = new GameObject("Sand").transform;
            sandRoot.SetParent(transform, false);
            SpriteRenderer top = MakeShape(sandRoot, "SandTop", Tri, glassAt, 1f, sand, BoardOrder + 4);
            SpriteRenderer mound = MakeShape(sandRoot, "SandMound", Tri, glassAt, 1f, sand, BoardOrder + 4);
            SpriteRenderer stream = MakeShape(sandRoot, "Stream", ViewUtil.WhiteSprite, glassAt, 1f, sand, BoardOrder + 5);
            top.color = Clear(sand);
            mound.color = Clear(sand);
            stream.color = Clear(sand);
            var grains = new List<SpriteRenderer>();
            var grainY = new List<float>();
            var grainV = new List<float>();
            var grainX = new List<float>();
            float grainSpawn = 0f;
            int grainSeed = 1;

            SpriteRenderer halo = MakeShape(transform, "RewindHalo", ViewUtil.GlowSprite, glassAt, u * 5f, blue, BoardOrder + 1);
            halo.color = Clear(blue);
            SpriteRenderer sweep = MakeShape(transform, "RewindRing", Ring, centre, boardSize * 1.1f, blue, BoardOrder + 1);
            sweep.color = Clear(blue);
            // A clock hand over the board, running BACK.
            var handPivot = new GameObject("RewindHand").transform;
            handPivot.SetParent(transform, false);
            handPivot.position = centre;
            SpriteRenderer hand = MakeRound(handPivot, "Hand", new Vector2(0f, boardSize * 0.2f), new Vector2(u * 0.09f, boardSize * 0.4f), blue, -1);
            hand.color = Clear(blue);
            SpriteRenderer wash = MakeShape(transform, "RewindWash", ViewUtil.GlowSprite, centre, boardSize * 1.15f, blue, BoardOrder - 1);
            wash.color = Clear(blue);

            // ---- the cubes ----
            var leave = new List<SpriteRenderer>();
            var ghosts = new List<SpriteRenderer>();
            foreach (CubeFace f in leaving)
            {
                SpriteRenderer g = ViewUtil.MakeCell(view.transform, "RewoundGhost", view.CellToWorld(f.Cell), cell, f.Colour, 4);
                ViewUtil.ApplyTile(g, f.Tile, cell);
                g.color = Clear(f.Colour);
                ghosts.Add(g);
                SpriteRenderer c = ViewUtil.MakeCell(view.transform, "Rewound", view.CellToWorld(f.Cell), cell, f.Colour, 5);
                ViewUtil.ApplyTile(c, f.Tile, cell);
                c.color = f.Colour;
                leave.Add(c);
            }
            var shards = new List<SpriteRenderer[]>();
            var shardFrom = new List<Vector2[]>();
            var snaps = new List<SpriteRenderer>();
            foreach (CubeFace f in returning)
            {
                var set = new SpriteRenderer[4];
                var from = new Vector2[4];
                Vector2 at = view.CellToWorld(f.Cell);
                for (int q = 0; q < 4; q++)
                {
                    set[q] = ViewUtil.MakeCell(view.transform, "Returning", at, cell, f.Colour, 5);
                    ViewUtil.ApplyTile(set[q], f.Tile, cell);
                    set[q].color = Clear(f.Colour);
                    float a = (q * 90f + 45f + (f.Cell.X * 31 + f.Cell.Y * 17) % 40) * Mathf.Deg2Rad;
                    from[q] = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * 1.6f;
                }
                shards.Add(set);
                shardFrom.Add(from);
                SpriteRenderer snap = MakeShape(view.transform, "Snap", ViewUtil.GlowSprite, at, cell * 1.3f, blue, BoardOrder);
                snap.color = Clear(blue);
                snaps.Add(snap);
            }

            const float Appear = 0.22f, Flip0 = 0.2f, Flip1 = 0.55f, Run0 = 0.5f, DrainEnd = 1.35f, End = 1.6f;
            Play(x => x.Whoosh());
            bool flipped = false;
            float nextTick = Run0;
            int tick = 0;
            float t = 0f;
            float gravity = u * 22f;
            while (t < End)
            {
                float dt = Time.deltaTime;
                t += dt;
                float show = Mathf.Clamp01(t / Appear) * (1f - Mathf.Clamp01((t - End + 0.25f) / 0.25f));
                SetGroupAlpha(frame, show);
                float pop = EaseOutBack(Mathf.Clamp01(t / Appear), 1.6f);
                glass.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, pop);
                // The turn: a small wind-up the wrong way, then over with a settle.
                float fk = Mathf.Clamp01((t - Flip0) / (Flip1 - Flip0));
                float windup = t < Flip0 ? -8f * Mathf.Clamp01((t - Flip0 + 0.1f) / 0.1f) : 0f;
                float angle = 180f * EaseOutBack(fk, 1.2f) + windup;
                glass.localRotation = Quaternion.Euler(0f, 0f, angle);
                // A little lift while it turns, so it reads as picked up and set down.
                glass.position = glassAt + new Vector2(0f, u * 0.25f * Mathf.Sin(fk * Mathf.PI));
                if (!flipped && fk >= 0.92f)
                {
                    flipped = true;
                    Play(x => x.Drum(1.3f));
                }
                carried.color = Fade(sand, flipped ? 0f : show);

                // ---- SAND, in world space ----
                float scaleNow = glass.localScale.x;
                Vector2 neck = (Vector2)glass.position;
                float drain = flipped ? Mathf.Clamp01((t - Flip1) / (DrainEnd - Flip1)) : 0f;
                float sandShow = flipped ? show : 0f;
                // A cone's height goes as the cube root of its volume - the top pile falls
                // quickly at first and lingers at the neck; the mound climbs the same way.
                float topH = fullH * scaleNow * Mathf.Pow(1f - drain, 1f / 3f) * (drain >= 1f ? 0f : 1f);
                float moundH = fullH * scaleNow * 0.85f * Mathf.Pow(drain, 1f / 3f);
                top.transform.position = neck + new Vector2(0f, topH * 0.5f);
                top.transform.localScale = new Vector3(bulbW * scaleNow * (topH / Mathf.Max(bulbH * scaleNow, 1e-4f)) * 0.98f, -topH, 1f);
                top.color = Fade(Color.Lerp(sand, sandDeep, 0.2f), sandShow);
                float floorY = neck.y - bulbH * scaleNow;
                mound.transform.position = new Vector2(neck.x, floorY + moundH * 0.5f);
                mound.transform.localScale = new Vector3(bulbW * scaleNow * 0.85f * Mathf.Lerp(0.45f, 1f, drain), moundH, 1f);
                mound.color = Fade(sand, sandShow);
                bool pouring = flipped && drain > 0f && drain < 1f;
                float streamTop = neck.y;
                float streamBottom = floorY + moundH;
                float jitter = 0.7f + 0.3f * Mathf.PerlinNoise(t * 30f, 0.5f);
                stream.transform.position = new Vector2(neck.x, (streamTop + streamBottom) * 0.5f);
                SetWorldSize(stream, u * 0.045f * jitter, Mathf.Max(0f, streamTop - streamBottom));
                stream.color = Fade(sand, pouring ? sandShow * 0.85f : 0f);
                // Grains: spat out of the neck, falling under gravity onto the mound.
                if (pouring)
                {
                    grainSpawn -= dt;
                    while (grainSpawn <= 0f)
                    {
                        grainSpawn += 0.018f;
                        grainSeed = grainSeed * 1103515245 + 12345;
                        float r01 = ((grainSeed >> 8) & 0xFFFF) / 65535f;
                        SpriteRenderer g = MakeShape(sandRoot, "Grain", ViewUtil.WhiteSprite, neck, u * 0.05f, sand, BoardOrder + 5);
                        grains.Add(g);
                        grainY.Add(neck.y);
                        grainV.Add(u * (0.5f + r01));
                        grainX.Add((r01 - 0.5f) * u * 0.07f);
                    }
                }
                for (int i = grains.Count - 1; i >= 0; i--)
                {
                    grainV[i] += gravity * dt;
                    grainY[i] -= grainV[i] * dt;
                    if (grainY[i] <= streamBottom)
                    {
                        Destroy(grains[i].gameObject);
                        grains.RemoveAt(i);
                        grainY.RemoveAt(i);
                        grainV.RemoveAt(i);
                        grainX.RemoveAt(i);
                        continue;
                    }
                    grains[i].transform.position = new Vector2(neck.x + grainX[i], grainY[i]);
                    grains[i].color = Fade(i % 3 == 0 ? sandDeep : sand, sandShow);
                }

                halo.color = Fade(blue, 0.3f * show);
                // Time running BACK over the board: the ring closes, the hand turns anticlockwise
                // and the whole arena is washed cold for the length of it.
                float rk = Mathf.Clamp01((t - Run0) / (End - Run0));
                sweep.transform.localScale = Vector3.one * boardSize * Mathf.Lerp(1.2f, 0.3f, rk);
                sweep.transform.rotation = Quaternion.Euler(0f, 0f, rk * 360f);
                sweep.color = Fade(blue, 0.35f * Mathf.Sin(rk * Mathf.PI));
                handPivot.rotation = Quaternion.Euler(0f, 0f, 720f * rk * rk);
                hand.color = Fade(blue, 0.28f * Mathf.Sin(rk * Mathf.PI));
                wash.color = Fade(blue, 0.12f * Mathf.Sin(rk * Mathf.PI));
                if (t >= nextTick && t < End - 0.2f)
                {
                    float pitch = tick % 2 == 0 ? 2.1f : 1.8f;
                    Play(x => x.Pluck(pitch));
                    tick++;
                    nextTick += Mathf.Max(0.06f, 0.14f - tick * 0.012f);
                }
                // PLACED CUBES go back up: last in first out, lifting, shrinking, turning blue,
                // with an afterimage lagging behind them.
                for (int i = 0; i < leave.Count; i++)
                {
                    if (leave[i] == null)
                    {
                        continue;
                    }
                    float start = Run0 + 0.45f * (leave.Count - 1 - i) / Mathf.Max(1, leave.Count);
                    float k = Mathf.Clamp01((t - start) / 0.38f);
                    Vector2 home = view.CellToWorld(leaving[i].Cell);
                    float e = k * k;
                    leave[i].transform.localPosition = home + new Vector2(0f, cell * 1.4f * e);
                    leave[i].transform.localScale = Vector3.one * cell * Mathf.Lerp(1f, 0.6f, k);
                    leave[i].color = Fade(Color.Lerp(leaving[i].Colour, blue, k), 1f - e);
                    float gk = Mathf.Clamp01(k - 0.15f);
                    ghosts[i].transform.localPosition = home + new Vector2(0f, cell * 1.4f * gk * gk);
                    ghosts[i].transform.localScale = Vector3.one * cell * Mathf.Lerp(1f, 0.6f, gk);
                    ghosts[i].color = Fade(blue, k > 0f ? 0.35f * (1f - gk) : 0f);
                    if (k >= 1f)
                    {
                        Destroy(leave[i].gameObject);
                        Destroy(ghosts[i].gameObject);
                        leave[i] = null;
                    }
                }
                // DESTROYED CUBES come back together: four pieces converge and snap, and the cell
                // gives one cold pulse as it closes.
                for (int i = 0; i < shards.Count; i++)
                {
                    float start = Run0 + 0.1f + 0.45f * i / Mathf.Max(1, shards.Count);
                    float k = Mathf.Clamp01((t - start) / 0.4f);
                    if (shards[i] != null)
                    {
                        Vector2 home = view.CellToWorld(returning[i].Cell);
                        float e = k * k * (3f - 2f * k);
                        for (int q = 0; q < 4; q++)
                        {
                            shards[i][q].transform.localPosition = Vector2.Lerp(shardFrom[i][q], home, e);
                            shards[i][q].transform.localScale = Vector3.one * cell * Mathf.Lerp(0.3f, 1f, e);
                            shards[i][q].transform.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * (q % 2 == 0 ? 160f : -160f));
                            shards[i][q].color = Fade(Color.Lerp(blue, returning[i].Colour, e), k <= 0f ? 0f : Mathf.Min(1f, k * 3f) * (q == 0 ? 1f : 1f - e));
                        }
                        if (k >= 1f)
                        {
                            for (int q = 0; q < 4; q++)
                            {
                                Destroy(shards[i][q].gameObject);
                            }
                            shards[i] = null;
                            view.ReleaseCells(new[] { returning[i].Cell });
                        }
                    }
                    float sk = Mathf.Clamp01((t - start - 0.4f) / 0.22f);
                    snaps[i].color = Fade(blue, sk > 0f && sk < 1f ? 0.55f * (1f - sk) : 0f);
                    snaps[i].transform.localScale = Vector3.one * cell * Mathf.Lerp(1f, 1.5f, sk)
                        / Mathf.Max(snaps[i].sprite.bounds.size.x, 1e-4f);
                }
                yield return null;
            }
            foreach (SpriteRenderer r in leave)
            {
                if (r != null) Destroy(r.gameObject);
            }
            foreach (SpriteRenderer r in ghosts)
            {
                if (r != null) Destroy(r.gameObject);
            }
            foreach (SpriteRenderer[] set in shards)
            {
                if (set != null) foreach (SpriteRenderer r in set) Destroy(r.gameObject);
            }
            foreach (SpriteRenderer r in snaps)
            {
                if (r != null) Destroy(r.gameObject);
            }
            if (view != null)
            {
                view.ReleaseCells(held);
            }
            Play(x => x.Chime(0.9f));
            Destroy(glass.gameObject);
            Destroy(sandRoot.gameObject);
            Destroy(halo.gameObject);
            Destroy(sweep.gameObject);
            Destroy(handPivot.gameObject);
            Destroy(wash.gameObject);
            running--;
        }

        /// <summary>Scales a renderer so it covers <paramref name="w"/> x <paramref name="h"/>
        /// world units, whatever its sprite's own size.</summary>
        private static void SetWorldSize(SpriteRenderer r, float w, float h)
        {
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f), h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        // ================================================================ INVERT

        /// <summary>"Bardağın Boş Tarafı": a glass tips over above the board and every cell turns
        /// over like a tile in a diagonal wave - what was full comes up empty, what was empty
        /// comes up full. <paramref name="before"/> maps every play cell to its old face (Tile
        /// null = it was empty), <paramref name="after"/> to its new one. <paramref name="done"/>
        /// fires when the last cell has turned (the lines the new cubes made go off then).</summary>
        public void PlayInvert(BoardView view, IList<CubeFace> before, IList<CubeFace> after,
            Vector2 centre, float boardSize, Action done)
        {
            StartCoroutine(Invert(view, before, after, centre, boardSize, done));
        }

        private IEnumerator Invert(BoardView view, IList<CubeFace> before, IList<CubeFace> after,
            Vector2 centre, float boardSize, Action done)
        {
            running++;
            float cell = view.CellWorldSize;
            float u = boardSize / 9f;
            Color water = new Color(0.55f, 0.82f, 1f);
            var held = new List<GridPos>();
            foreach (CubeFace f in before)
            {
                held.Add(f.Cell);
            }
            view.HoldCells(held);

            // ---- the glass: a tumbler that tips over and pours ----
            var cup = new GameObject("Glass").transform;
            cup.SetParent(transform, false);
            cup.position = centre + new Vector2(-boardSize * 0.28f, boardSize * 0.3f);
            var parts = new List<Part>();
            Color rim = new Color(0.85f, 0.95f, 1f, 0.8f);
            Color body = new Color(0.75f, 0.9f, 1f, 0.3f);
            parts.Add(new Part(MakeRound(cup, "Body", Vector2.zero, new Vector2(u * 1.3f, u * 1.8f), body, 3), body));
            parts.Add(new Part(MakeRound(cup, "Water", new Vector2(0f, -u * 0.4f), new Vector2(u * 1.1f, u * 0.85f), Fade(water, 0.75f), 4), Fade(water, 0.75f)));
            parts.Add(new Part(MakeRound(cup, "Rim", new Vector2(0f, u * 0.9f), new Vector2(u * 1.4f, u * 0.1f), rim, 5), rim));
            parts.Add(new Part(MakeRound(cup, "Base", new Vector2(0f, -u * 0.9f), new Vector2(u * 1.3f, u * 0.14f), rim, 5), rim));
            parts.Add(new Part(MakeRound(cup, "Shine", new Vector2(-u * 0.4f, u * 0.1f), new Vector2(u * 0.1f, u * 1.2f), new Color(1f, 1f, 1f, 0.6f), 5), new Color(1f, 1f, 1f, 0.6f)));
            SetGroupAlpha(parts, 0f);
            var motes = new List<Mote>();

            // ---- the tiles: an old face and a new face per cell ----
            var oldFace = new List<SpriteRenderer>();
            var newFace = new List<SpriteRenderer>();
            var delay = new List<float>();
            int minSum = int.MaxValue, maxSum = int.MinValue;
            foreach (CubeFace f in before)
            {
                int d = f.Cell.X - f.Cell.Y;
                minSum = Math.Min(minSum, d);
                maxSum = Math.Max(maxSum, d);
            }
            for (int i = 0; i < before.Count; i++)
            {
                Vector2 at = view.CellToWorld(before[i].Cell);
                oldFace.Add(MakeFace(view, before[i], at, cell));
                newFace.Add(MakeFace(view, after[i], at, cell));
                newFace[i].enabled = false;
                // A diagonal wave from the corner the glass tips toward.
                delay.Add(0.45f + 0.5f * (before[i].Cell.X - before[i].Cell.Y - minSum) / Mathf.Max(1f, maxSum - minSum));
            }

            const float Show = 0.18f, Tip = 0.42f, Turn = 0.2f;
            Play(x => x.Whoosh());
            float end = 0.45f + 0.5f + Turn + 0.15f;
            bool poured = false;
            float lastTick = -1f;
            bool[] turned = new bool[before.Count];
            float t = 0f;
            while (t < end)
            {
                float dt = Time.deltaTime;
                t += dt;
                float show = Mathf.Clamp01(t / Show) * (1f - Mathf.Clamp01((t - end + 0.25f) / 0.25f));
                SetGroupAlpha(parts, show);
                float tip = EaseOutBack(Mathf.Clamp01((t - Show) / (Tip - Show)), 1.4f);
                cup.localRotation = Quaternion.Euler(0f, 0f, -125f * tip);
                if (!poured && t >= Tip)
                {
                    poured = true;
                    Play(x => x.Pluck(0.8f));
                    Vector2 lip = cup.TransformPoint(new Vector2(0f, u * 0.9f));
                    for (int m = 0; m < 12; m++)
                    {
                        motes.Add(Mote.Make(this, ViewUtil.GlowSprite, lip,
                            new Vector2(UnityEngine.Random.Range(1.5f, 4f), UnityEngine.Random.Range(-1f, 1.5f)),
                            u * UnityEngine.Random.Range(0.18f, 0.3f), water, 0.8f, BoardOrder + 6, 0.6f, 9f, 0.6f));
                    }
                }
                for (int i = 0; i < before.Count; i++)
                {
                    float k = Mathf.Clamp01((t - delay[i]) / Turn);
                    if (k <= 0f)
                    {
                        continue;
                    }
                    // A tile turning over: the old face narrows to an edge, the new one opens.
                    bool second = k >= 0.5f;
                    float w = second ? (k - 0.5f) * 2f : 1f - k * 2f;
                    float lift = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
                    if (oldFace[i] != null)
                    {
                        oldFace[i].enabled = !second;
                        oldFace[i].transform.localScale = new Vector3(cell * w * lift, cell * lift, 1f) * (oldFace[i].name == "Floor" ? 0.82f : 1f);
                    }
                    newFace[i].enabled = second;
                    newFace[i].transform.localScale = new Vector3(cell * w * lift, cell * lift, 1f) * (newFace[i].name == "Floor" ? 0.82f : 1f);
                    if (!turned[i] && second)
                    {
                        turned[i] = true;
                        if (t - lastTick > 0.035f)
                        {
                            lastTick = t;
                            float pitch = 1.5f + 0.5f * (i % 5) / 5f;
                            Play(x => x.Pluck(pitch));
                        }
                    }
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer r in oldFace) if (r != null) Destroy(r.gameObject);
            foreach (SpriteRenderer r in newFace) if (r != null) Destroy(r.gameObject);
            if (view != null)
            {
                view.ReleaseCells(held);
            }
            Destroy(cup.gameObject);
            running--;
            if (done != null)
            {
                done();
            }
        }

        /// <summary>A cube's face, or the empty floor when it has none.</summary>
        private static SpriteRenderer MakeFace(BoardView view, CubeFace face, Vector2 at, float cell)
        {
            if (face.Tile == null)
            {
                SpriteRenderer floor = ViewUtil.MakeCell(view.transform, "Floor", at, cell * 0.82f, Floor, 5);
                return floor;
            }
            SpriteRenderer cube = ViewUtil.MakeCell(view.transform, "Cube", at, cell, face.Colour, 6);
            ViewUtil.ApplyTile(cube, face.Tile, cell);
            cube.color = face.Colour;
            return cube;
        }

        // =============================================================== MAGAZINE

        /// <summary>"Hızlı Çekim Şarjörü": the draw pile is FIRED into the discard one card-back
        /// after another like a magazine being emptied - a muzzle flash, a kick, the cards
        /// spinning across - then the discard riffles back into the draw pile in one arc.
        /// Positions are the card layer's local pile positions.</summary>
        public void PlayMagazine(Transform cardLayer, Vector2 drawPile, Vector2 discardPile, int fired,
            int returned, float pileScale, int order)
        {
            StartCoroutine(Magazine(cardLayer, drawPile, discardPile, fired, returned, pileScale, order));
        }

        private IEnumerator Magazine(Transform cardLayer, Vector2 drawPile, Vector2 discardPile, int fired,
            int returned, float pileScale, int order)
        {
            running++;
            float scale = cardLayer.lossyScale.x * pileScale;
            Vector2 size = new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * scale;
            Vector2 from = cardLayer.TransformPoint(drawPile);
            Vector2 to = cardLayer.TransformPoint(discardPile);
            Color back = new Color(0.16f, 0.2f, 0.38f);
            Color trim = new Color(0.86f, 0.68f, 0.3f);
            Color flash = new Color(1f, 0.85f, 0.5f);
            int shots = Mathf.Clamp(fired, 0, 10);
            int backs = Mathf.Clamp(returned, 0, 12);
            var flying = new List<SpriteRenderer[]>();
            var motes = new List<Mote>();

            // 1. FIRE: one card-back after another, fast, each with a flash and a kick.
            float gap = shots > 1 ? Mathf.Min(0.07f, 0.55f / shots) : 0f;
            float flight = 0.28f;
            float t = 0f;
            int launched = 0;
            var launchedAt = new List<float>();
            float fireEnd = gap * Mathf.Max(0, shots - 1) + flight;
            while (t < fireEnd + 0.02f && shots > 0)
            {
                float dt = Time.deltaTime;
                t += dt;
                while (launched < shots && t >= launched * gap)
                {
                    flying.Add(MakeBack(size, back, trim, from, order + 2 + launched));
                    launchedAt.Add(launched * gap);
                    StartCoroutine(Burst(from + new Vector2(0f, size.y * 0.1f), size.x * 1.6f, flash, order + 20, 0.12f));
                    int n = launched;
                    Play(x => x.Pluck(2.3f + 0.04f * (n % 3)));
                    for (int m = 0; m < 2; m++)
                    {
                        motes.Add(Mote.Make(this, Diamond, from, new Vector2(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(1f, 3f)),
                            size.x * 0.08f, flash, 1f, order + 21, 3f, 6f, 0.3f));
                    }
                    launched++;
                }
                for (int i = 0; i < flying.Count; i++)
                {
                    if (flying[i] == null)
                    {
                        continue;
                    }
                    float k = Mathf.Clamp01((t - launchedAt[i]) / flight);
                    float e = 1f - (1f - k) * (1f - k);
                    Vector2 at = Vector2.Lerp(from, to, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y * 0.8f);
                    float spin = (1f - e) * (i % 2 == 0 ? 540f : -540f) + (i - shots * 0.5f) * 3f;
                    PlaceBack(flying[i], at, spin);
                }
                StepMotes(motes, dt);
                yield return null;
            }
            // The fired cards land and settle into the discard.
            foreach (SpriteRenderer[] c in flying)
            {
                foreach (SpriteRenderer r in c) Destroy(r.gameObject);
            }
            flying.Clear();
            StartCoroutine(Burst(to, size.x * 2f, flash, order + 1, 0.2f));

            // 2. THE RELOAD: the discard riffles back into the draw pile in one arc.
            Play(x => x.Shuffle());
            float riffle = 0.36f;
            float stagger = backs > 1 ? 0.2f / (backs - 1) : 0f;
            for (int i = 0; i < backs; i++)
            {
                flying.Add(MakeBack(size, back, trim, to, order + 2 + i));
            }
            t = 0f;
            float total = riffle + stagger * Mathf.Max(0, backs - 1);
            while (t < total)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < backs; i++)
                {
                    float k = Mathf.Clamp01((t - i * stagger) / riffle);
                    float e = k * k * (3f - 2f * k);
                    Vector2 at = Vector2.Lerp(to, from, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y * 1.1f);
                    PlaceBack(flying[i], at, Mathf.Sin(k * Mathf.PI) * (i % 2 == 0 ? 14f : -14f));
                }
                StepMotes(motes, dt);
                yield return null;
            }
            foreach (SpriteRenderer[] c in flying)
            {
                foreach (SpriteRenderer r in c) Destroy(r.gameObject);
            }
            ClearMotes(motes);
            Play(x => x.Drum(1.6f));
            StartCoroutine(Burst(from, size.x * 1.8f, trim, order + 1, 0.25f));
            running--;
        }

        private SpriteRenderer[] MakeBack(Vector2 size, Color back, Color trim, Vector2 at, int order)
        {
            var root = new SpriteRenderer[3];
            root[0] = ViewUtil.MakeRounded(transform, "BackTrim", Vector2.zero, size, trim, order);
            root[1] = ViewUtil.MakeRounded(transform, "Back", Vector2.zero, size * 0.9f, back, order);
            root[2] = MakeShape(transform, "BackMark", Diamond, at, size.x * 0.35f, trim, order);
            root[0].sortingOrder = order;
            root[1].sortingOrder = order;
            root[2].sortingOrder = order;
            PlaceBack(root, at, 0f);
            return root;
        }

        private static void PlaceBack(SpriteRenderer[] card, Vector2 at, float degrees)
        {
            Quaternion turn = Quaternion.Euler(0f, 0f, degrees);
            foreach (SpriteRenderer r in card)
            {
                r.transform.position = at;
                r.transform.rotation = turn;
            }
        }

        // ================================================================ HOLOGRAM

        /// <summary>"Hologram": the bonus card turns into light - a cyan projection of itself that
        /// flickers and glitches sideways - then comes apart into scanline slices that stream,
        /// one after another, into the discard pile, where they rebuild for a moment and are
        /// gone. <paramref name="at"/> is the card's world centre, <paramref name="discard"/> the
        /// discard pile's.</summary>
        public void PlayHologram(Vector2 at, Vector2 discard, float cardScale, int order)
        {
            StartCoroutine(Hologram(at, discard, cardScale, order));
        }

        private IEnumerator Hologram(Vector2 at, Vector2 discard, float cardScale, int order)
        {
            running++;
            Vector2 size = new Vector2(CardVisual.BodyWidth, CardVisual.BodyHeight) * cardScale;
            Color cyan = new Color(0.45f, 0.95f, 1f);
            Color magenta = new Color(1f, 0.45f, 0.9f);
            const int Slices = 9;
            var slices = new SpriteRenderer[Slices];
            var ghost = new SpriteRenderer[Slices];
            float sliceH = size.y / Slices;
            for (int i = 0; i < Slices; i++)
            {
                slices[i] = ViewUtil.MakeRounded(transform, "Scan", Vector2.zero, new Vector2(size.x, sliceH * 0.78f), cyan, order + 2);
                ghost[i] = ViewUtil.MakeRounded(transform, "ScanGhost", Vector2.zero, new Vector2(size.x, sliceH * 0.78f), magenta, order + 1);
            }
            SpriteRenderer glow = MakeShape(transform, "HoloGlow", ViewUtil.GlowSprite, at, size.y * 1.6f, cyan, order);
            SpriteRenderer beam = MakeShape(transform, "HoloBeam", Ray, at - new Vector2(0f, size.y * 0.75f), 1f, cyan, order);
            beam.transform.localScale = new Vector3(size.x * 1.4f, size.y * 1.5f, 1f);
            var motes = new List<Mote>();
            Play(x => x.Stretch());

            // 1. PROJECTION: the slices flicker on over the card, jittering sideways.
            float t = 0f;
            const float Project = 0.38f;
            while (t < Project)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = t / Project;
                for (int i = 0; i < Slices; i++)
                {
                    float y = at.y + size.y * 0.5f - sliceH * (i + 0.5f);
                    // A glitch: every few frames a slice jumps sideways.
                    float jitter = (Mathf.PerlinNoise(i * 3.1f, t * 18f) - 0.5f) * size.x * 0.25f * (1f - k * 0.5f);
                    bool on = Mathf.PerlinNoise(i * 7.3f, t * 30f) > 0.25f - k * 0.3f;
                    slices[i].transform.position = new Vector2(at.x + jitter, y);
                    slices[i].color = Fade(cyan, on ? 0.55f + 0.3f * k : 0.1f);
                    ghost[i].transform.position = new Vector2(at.x + jitter + size.x * 0.04f, y);
                    ghost[i].color = Fade(magenta, on ? 0.3f : 0f);
                }
                glow.color = Fade(cyan, 0.35f * k);
                beam.color = Fade(cyan, 0.25f * k);
                yield return null;
            }
            // 2. STREAM: the slices leave top first, each a streak to the discard.
            Play(x => x.Whoosh());
            const float Travel = 0.34f, Gap = 0.035f;
            var starts = new Vector2[Slices];
            for (int i = 0; i < Slices; i++)
            {
                starts[i] = slices[i].transform.position;
                Destroy(ghost[i].gameObject);
            }
            t = 0f;
            float total = Gap * (Slices - 1) + Travel;
            bool landed = false;
            while (t < total + 0.25f)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < Slices; i++)
                {
                    float k = Mathf.Clamp01((t - i * Gap) / Travel);
                    float e = k * k * (3f - 2f * k);
                    Vector2 target = discard + new Vector2(0f, size.y * 0.5f - sliceH * (i + 0.5f)) * 0.8f;
                    Vector2 p = Vector2.Lerp(starts[i], target, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * size.y * 0.35f);
                    slices[i].transform.position = p;
                    // Stretched along its flight in the middle, then back to a line of the card.
                    float stretch = 1f + 1.6f * Mathf.Sin(k * Mathf.PI);
                    slices[i].size = new Vector2(size.x * 0.8f / Mathf.Lerp(1f, 1.2f, e), sliceH * 0.78f / stretch * 0.9f + 0.01f);
                    float out1 = Mathf.Clamp01((t - total) / 0.25f);
                    slices[i].color = Fade(cyan, (k >= 1f ? 0.8f : 0.7f) * (1f - out1));
                    if (k > 0f && k < 1f && UnityEngine.Random.value < dt * 20f)
                    {
                        motes.Add(Mote.Make(this, Diamond, p, Vector2.zero, sliceH * 0.5f, cyan, 0.7f, order + 3, 0f, 0f, 0.25f));
                    }
                }
                float fade = 1f - Mathf.Clamp01(t / 0.3f);
                glow.color = Fade(cyan, 0.35f * fade);
                beam.color = Fade(cyan, 0.25f * fade);
                if (!landed && t >= total)
                {
                    landed = true;
                    Play(x => x.Chime(2f));
                    StartCoroutine(Burst(discard, size.x * 2f, cyan, order + 1, 0.3f));
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer r in slices) Destroy(r.gameObject);
            Destroy(glow.gameObject);
            Destroy(beam.gameObject);
            running--;
        }

        // =================================================================== CHARM

        /// <summary>"Tılsım": the charm is summoned over the arena and the ghosts' spirits are
        /// drawn into it. <paramref name="ghosts"/> are world positions, <paramref name="colours"/>
        /// each ghost's own colour.</summary>
        public void PlayTalismanCharm(Vector2 centre, float boardSize, IList<Vector2> ghosts, IList<Color> colours,
            int total)
        {
            StartCoroutine(TalismanCharm(centre, boardSize, ghosts, colours, total));
        }

        private IEnumerator TalismanCharm(Vector2 centre, float boardSize, IList<Vector2> ghosts, IList<Color> colours,
            int total)
        {
            running++;
            float u = boardSize / 9f;
            Vector2 anchor = centre + new Vector2(0f, boardSize * 0.72f);  // where the cord hangs from
            float cordLen = boardSize * 0.42f;
            var pivot = new GameObject("Charm").transform;
            pivot.SetParent(transform, false);
            pivot.position = anchor;
            var parts = new List<Part>();
            SpriteRenderer cord = MakeRound(pivot, "Cord", new Vector2(0f, -cordLen * 0.5f),
                new Vector2(u * 0.07f, cordLen), GoldDeep, 1);
            parts.Add(new Part(cord, GoldDeep));
            var charm = new GameObject("Amulet").transform;
            charm.SetParent(pivot, false);
            charm.localPosition = new Vector2(0f, -cordLen - u * 0.8f);
            SpriteRenderer glow = MakeShape(charm, "Glow", ViewUtil.GlowSprite, Vector2.zero, u * 4.5f, Spirit, BoardOrder + 1);
            glow.transform.localPosition = Vector3.zero;
            glow.color = Clear(Spirit);
            SpriteRenderer back = MakeShape(charm, "Back", Disc, Vector2.zero, u * 1.7f, new Color(0.16f, 0.24f, 0.22f), BoardOrder + 3);
            back.transform.localPosition = Vector3.zero;
            parts.Add(new Part(back, back.color));
            SpriteRenderer rim = MakeShape(charm, "Rim", Ring, Vector2.zero, u * 1.9f, Gold, BoardOrder + 4);
            rim.transform.localPosition = Vector3.zero;
            parts.Add(new Part(rim, Gold));
            SpriteRenderer inner = MakeShape(charm, "Inner", Ring, Vector2.zero, u * 1.3f, GoldDeep, BoardOrder + 4);
            inner.transform.localPosition = Vector3.zero;
            parts.Add(new Part(inner, GoldDeep));
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                SpriteRenderer stud = MakeShape(charm, "Stud", Diamond, Vector2.zero, u * 0.16f, Gold, BoardOrder + 5);
                stud.transform.localPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * u * 0.8f;
                parts.Add(new Part(stud, Gold));
            }
            SpriteRenderer gem = MakeShape(charm, "Gem", Diamond, Vector2.zero, u * 0.62f, Teal, BoardOrder + 6);
            gem.transform.localPosition = Vector3.zero;
            gem.transform.localScale = new Vector3(u * 0.5f, u * 0.72f, 1f);
            parts.Add(new Part(gem, Teal));
            SpriteRenderer gemLight = MakeShape(charm, "GemLight", ViewUtil.GlowSprite, Vector2.zero, u * 1.2f, Spirit, BoardOrder + 7);
            gemLight.transform.localPosition = Vector3.zero;
            gemLight.color = Clear(Spirit);
            for (int i = -1; i <= 1; i++)
            {
                SpriteRenderer tassel = MakeShape(charm, "Tassel", Feather, Vector2.zero, 1f, i == 0 ? Paint : Teal, BoardOrder + 2);
                tassel.transform.localPosition = new Vector2(i * u * 0.35f, -u * 0.6f);
                tassel.transform.localScale = new Vector3(u * 0.3f, u * (i == 0 ? 1.2f : 0.9f), 1f);
                tassel.transform.localRotation = Quaternion.Euler(0f, 0f, 180f + i * 14f);
                parts.Add(new Part(tassel, tassel.color));
            }
            SpriteRenderer rune = MakeShape(transform, "CharmRune", Ring, centre, 0.1f, Spirit, BoardOrder + 1);
            rune.color = Clear(Spirit);
            SetGroupAlpha(parts, 0f);

            // Spirits, one per ghost, each leaving on its own beat along its own bow.
            int count = ghosts.Count;
            float stagger = count > 1 ? Mathf.Min(0.06f, 0.5f / (count - 1)) : 0f;
            var spirits = new SpriteRenderer[count];
            var arrived = new bool[count];
            var motes = new List<Mote>();
            const float Drop = 0.45f, Ignite = 0.5f, Draw0 = 0.7f, Travel = 0.5f;
            float drawEnd = Draw0 + stagger * Mathf.Max(0, count - 1) + Travel;
            float payAt = drawEnd + 0.05f;
            float leave0 = payAt + 0.55f;
            float end = leave0 + 0.4f;
            bool paid = false;
            bool ignited = false;
            bool leaving = false;
            float lastPing = -1f;
            Play(x => x.Whoosh());
            float pulse = 0f;
            float t = 0f;
            while (t < end)
            {
                float dt = Time.deltaTime;
                t += dt;
                // THE DESCENT on its cord, then a pendulum swing that dies away.
                float drop = EaseOutBack(Mathf.Clamp01(t / Drop), 1.1f);
                float leave = Mathf.Clamp01((t - leave0) / (end - leave0));
                pivot.position = anchor + new Vector2(0f, boardSize * 0.5f * (1f - drop) + boardSize * 0.55f * leave * leave);
                float swing = 16f * Mathf.Exp(-t * 2.2f) * Mathf.Sin(t * 7.5f);
                pivot.localRotation = Quaternion.Euler(0f, 0f, swing);
                charm.localRotation = Quaternion.Euler(0f, 0f, -swing * 0.4f + Mathf.Sin(t * 2f) * 2f);
                SetGroupAlpha(parts, Mathf.Clamp01(t * 5f) * (1f - leave));

                // THE GEM IGNITES, and a rune ring opens under the arena's middle.
                if (!ignited && t >= Ignite)
                {
                    ignited = true;
                    Play(x => x.Chime(1.8f));
                }
                if (!leaving && t >= leave0)
                {
                    leaving = true;
                    Play(x => x.Whoosh());
                }
                float ignite = Mathf.Clamp01((t - Ignite) / 0.2f) * (1f - leave);
                pulse = Mathf.Max(0f, pulse - dt * 4f);
                gem.color = Color.Lerp(Fade(Teal, gem.color.a), Fade(Spirit, gem.color.a), ignite * 0.6f + pulse * 0.4f);
                gemLight.color = Fade(Spirit, ignite * (0.55f + 0.45f * pulse));
                gemLight.transform.localScale = Vector3.one * u * (1.1f + 0.8f * pulse);
                glow.color = Fade(Spirit, ignite * (0.2f + 0.3f * pulse));
                charm.localScale = Vector3.one * (1f + 0.08f * pulse);
                float runeK = Mathf.Clamp01((t - Ignite) / 0.5f);
                rune.transform.localScale = Vector3.one * Mathf.Lerp(u, boardSize * 0.9f, EaseOut(runeK));
                rune.color = Fade(Spirit, runeK <= 0f ? 0f : 0.35f * (1f - runeK));

                // THE SPIRITS rise from where each ghost hung and are drawn into the charm.
                Vector2 into = charm.position;
                for (int i = 0; i < count; i++)
                {
                    float k = (t - Draw0 - i * stagger) / Travel;
                    if (k <= 0f || arrived[i])
                    {
                        continue;
                    }
                    if (spirits[i] == null)
                    {
                        spirits[i] = MakeShape(transform, "Spirit", ViewUtil.GlowSprite, ghosts[i], u * 0.8f, Color.Lerp(colours[i], Spirit, 0.4f), BoardOrder + 8);
                    }
                    k = Mathf.Clamp01(k);
                    float e = k * k * (3f - 2f * k);
                    Vector2 from = ghosts[i];
                    Vector2 bow = (from + into) * 0.5f + new Vector2((i % 2 == 0 ? 1f : -1f) * u * 1.5f, u * 1.2f);
                    Vector2 at = Vector2.Lerp(Vector2.Lerp(from, bow, e), Vector2.Lerp(bow, into, e), e);
                    spirits[i].transform.position = at;
                    spirits[i].transform.localScale = Vector3.one * u * Mathf.Lerp(0.8f, 0.35f, e);
                    spirits[i].color = Fade(Color.Lerp(colours[i], Spirit, 0.4f + 0.6f * e), 0.9f);
                    if (UnityEngine.Random.value < dt * 30f)
                    {
                        motes.Add(Mote.Make(this, Diamond, at, UnityEngine.Random.insideUnitCircle * u * 0.4f,
                            u * 0.1f, Spirit, 0.8f, BoardOrder + 7, 1f, 0f, 0.35f));
                    }
                    if (k >= 1f)
                    {
                        arrived[i] = true;
                        if (t - lastPing > 0.07f)
                        {
                            lastPing = t;
                            float pitch = 1.6f + 0.05f * (i % 6);
                            Play(x => x.Pluck(pitch));
                        }
                        Destroy(spirits[i].gameObject);
                        spirits[i] = null;
                        pulse = Mathf.Min(1f, pulse + 0.5f);
                    }
                }
                // THE PAYOUT: one strong pulse, a ring, the number.
                if (!paid && t >= payAt)
                {
                    paid = true;
                    pulse = 1f;
                    Play(x => x.Chime(1.5f));
                    StartCoroutine(Burst(charm.position, u * 5f, Spirit, BoardOrder + 2, 0.45f));
                    for (int m = 0; m < 12; m++)
                    {
                        float a = m * 0.5236f;
                        motes.Add(Mote.Make(this, Diamond, charm.position, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * u * 3f,
                            u * 0.14f, m % 2 == 0 ? Gold : Spirit, 1f, BoardOrder + 9, 3f, 0f, 0.5f));
                    }
                    string text = total > 0 ? "+" + total : Loc.Pick("TALISMAN", "TILSIM");
                    FloatingTextFx.Spawn(transform, (Vector2)charm.position + new Vector2(0f, u * 1.6f),
                        Loc.Pick("TALISMAN ", "TILSIM ") + text, Gold, 56, 0.065f);
                }
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer s in spirits)
            {
                if (s != null)
                {
                    Destroy(s.gameObject);
                }
            }
            Destroy(rune.gameObject);
            Destroy(pivot.gameObject);
            running--;
        }

        // ============================================================ ARENA RESIZE

        /// <summary>
        /// The inflations. <paramref name="before"/> is the board the view showed before the
        /// resize and <paramref name="cellBefore"/> its cell size; the view has already been
        /// rebuilt for the new board. <paramref name="doomed"/> are the cubes that stood in the
        /// bands a shrink removed (faces taken before the repaint), or null when growing.
        /// </summary>
        public void PlayBoardResize(BoardView view, GameBoard before, float cellBefore, IList<CubeFace> doomed)
        {
            if (view == null || before == null || view.Board == null || view.Board == before)
            {
                return;
            }
            GameBoard after = view.Board;
            bool grows = after.Width * after.Height > before.Width * before.Height;
            StartCoroutine(grows ? Grow(view, before, cellBefore) : Shrink(view, before, cellBefore, doomed));
        }

        private IEnumerator Grow(BoardView view, GameBoard before, float cellBefore)
        {
            running++;
            GameBoard after = view.Board;
            float cell = view.CellWorldSize;
            float s0 = cellBefore / Mathf.Max(0.0001f, cell);
            // The new bands: play area outside the old board's rectangle.
            var band = new List<GridPos>();
            var delay = new List<float>();
            var axis = new List<Vector2>();
            float midX = before.MinX + (before.Width - 1) * 0.5f;
            float midY = before.MinY + (before.Height - 1) * 0.5f;
            for (int x = after.MinX; x < after.MinX + after.Width; x++)
            {
                for (int y = after.MinY; y < after.MinY + after.Height; y++)
                {
                    var p = new GridPos(x, y);
                    if (!after.IsInside(p) || InRect(before, p))
                    {
                        continue;
                    }
                    band.Add(p);
                    int ox = x < before.MinX ? -1 : x >= before.MinX + before.Width ? 1 : 0;
                    int oy = y < before.MinY ? -1 : y >= before.MinY + before.Height ? 1 : 0;
                    axis.Add(new Vector2(ox, oy));
                    // A wave that leaves the middle of each edge for its corners.
                    float along = ox != 0 && oy == 0 ? Mathf.Abs(y - midY) : oy != 0 && ox == 0 ? Mathf.Abs(x - midX)
                        : Mathf.Max(before.Width, before.Height) * 0.5f + 1f;
                    delay.Add(0.05f + along * 0.035f);
                }
            }
            view.SetInflate(s0);
            Play(x => x.Stretch());
            for (int i = 0; i < band.Count; i++)
            {
                view.SetCellSquash(band[i], Vector2.zero);
            }
            // The seam: the old rim lit, pushed out to the new one.
            var seams = new List<SpriteRenderer>();
            Rect oldRect = CellRect(view, before);
            Rect newRect = CellRect(view, after);
            for (int i = 0; i < 4; i++)
            {
                seams.Add(ViewUtil.MakeRounded(view.transform, "Seam", Vector2.zero, Vector2.one, Warm, 6));
            }
            var motes = new List<Mote>();
            float maxDelay = 0f;
            foreach (float d in delay)
            {
                maxDelay = Mathf.Max(maxDelay, d);
            }
            float popEnd = maxDelay + 0.26f;
            float fit0 = Mathf.Max(0.2f, popEnd - 0.15f);
            float end = fit0 + 0.38f;
            bool[] popped = new bool[band.Count];
            float t = 0f;
            while (t < end)
            {
                float dt = Time.deltaTime;
                t += dt;
                for (int i = 0; i < band.Count; i++)
                {
                    float k = Mathf.Clamp01((t - delay[i]) / 0.24f);
                    float e = EaseOutBack(k, 2.2f);
                    // Unfolding OUT of the old rim: it opens along its axis first.
                    float along = e;
                    float across = Mathf.Clamp01(k * 1.6f);
                    Vector2 f = axis[i].x != 0 && axis[i].y == 0 ? new Vector2(along, across)
                        : axis[i].y != 0 && axis[i].x == 0 ? new Vector2(across, along) : new Vector2(e, e);
                    view.SetCellSquash(band[i], f);
                    if (!popped[i] && k > 0f)
                    {
                        popped[i] = true;
                        Vector2 at = view.transform.TransformPoint(view.CellToWorld(band[i]));
                        motes.Add(Mote.Make(this, Diamond, at, axis[i] * cell * 2.2f + UnityEngine.Random.insideUnitCircle * cell * 0.5f,
                            cell * 0.12f, Warm, 0.9f, BoardOrder + 4, 3f, 0f, 0.35f));
                    }
                }
                float sk = EaseOut(Mathf.Clamp01(t / Mathf.Max(0.2f, popEnd)));
                Rect r = LerpRect(oldRect, newRect, sk);
                float thick = cell * 0.08f;
                float alpha = 0.85f * (1f - sk * sk);
                PlaceSeam(seams[0], new Vector2(r.center.x, r.yMax), new Vector2(r.width, thick), alpha);
                PlaceSeam(seams[1], new Vector2(r.center.x, r.yMin), new Vector2(r.width, thick), alpha);
                PlaceSeam(seams[2], new Vector2(r.xMin, r.center.y), new Vector2(thick, r.height), alpha);
                PlaceSeam(seams[3], new Vector2(r.xMax, r.center.y), new Vector2(thick, r.height), alpha);
                // The arena eases back to fit, with the lightest overshoot.
                float fk = Mathf.Clamp01((t - fit0) / (end - fit0));
                view.SetInflate(Mathf.LerpUnclamped(s0, 1f, EaseOutBack(fk, 0.9f)));
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer s in seams)
            {
                if (s != null)
                {
                    Destroy(s.gameObject);
                }
            }
            if (view != null)
            {
                view.SetInflate(1f);
                view.ClearCellSquash();
            }
            running--;
        }

        private IEnumerator Shrink(BoardView view, GameBoard before, float cellBefore, IList<CubeFace> doomed)
        {
            running++;
            GameBoard after = view.Board;
            float cell = view.CellWorldSize;
            float s0 = cellBefore / Mathf.Max(0.0001f, cell);
            view.SetInflate(s0);
            Play(x => x.Squish());
            // What the removed bands looked like, drawn in the arena's own space so they scale with
            // it: the empty floor of every doomed cell, and the cubes that stood there.
            var pieces = new List<SpriteRenderer>();
            var from = new List<Vector2>();
            var push = new List<Vector2>();
            var baseSize = new List<float>();
            var cubeCells = new HashSet<GridPos>();
            if (doomed != null)
            {
                foreach (CubeFace f in doomed)
                {
                    cubeCells.Add(f.Cell);
                }
            }
            for (int x = before.MinX; x < before.MinX + before.Width; x++)
            {
                for (int y = before.MinY; y < before.MinY + before.Height; y++)
                {
                    var p = new GridPos(x, y);
                    if (!before.IsInside(p) || after.IsInside(p) || InRect(after, p))
                    {
                        continue;
                    }
                    Vector2 local = view.CellToWorld(p);
                    SpriteRenderer floor = ViewUtil.MakeCell(view.transform, "DoomedFloor", local, cell * 0.82f, Floor, 3);
                    pieces.Add(floor);
                    from.Add(local);
                    push.Add(PushOf(after, p));
                    baseSize.Add(cell * 0.82f);
                }
            }
            if (doomed != null)
            {
                foreach (CubeFace f in doomed)
                {
                    Vector2 local = view.CellToWorld(f.Cell);
                    SpriteRenderer copy = ViewUtil.MakeCell(view.transform, "DoomedCube", local, cell, f.Colour, 4);
                    ViewUtil.ApplyTile(copy, f.Tile, cell);
                    copy.color = f.Colour;
                    pieces.Add(copy);
                    from.Add(local);
                    push.Add(PushOf(after, f.Cell));
                    baseSize.Add(cell);
                }
            }
            // The new rim takes the blow.
            var rim = new List<GridPos>();
            var rimAxis = new List<Vector2>();
            for (int x = after.MinX; x < after.MinX + after.Width; x++)
            {
                for (int y = after.MinY; y < after.MinY + after.Height; y++)
                {
                    var p = new GridPos(x, y);
                    if (!after.IsInside(p))
                    {
                        continue;
                    }
                    int ox = x == after.MinX && !InRect(after, new GridPos(x - 1, y)) && InRect(before, new GridPos(x - 1, y)) ? 1
                        : x == after.MinX + after.Width - 1 && InRect(before, new GridPos(x + 1, y)) ? -1 : 0;
                    int oy = y == after.MinY && InRect(before, new GridPos(x, y - 1)) ? 1
                        : y == after.MinY + after.Height - 1 && InRect(before, new GridPos(x, y + 1)) ? -1 : 0;
                    if (ox != 0 || oy != 0)
                    {
                        rim.Add(p);
                        rimAxis.Add(new Vector2(ox, oy));
                    }
                }
            }
            var motes = new List<Mote>();
            const float Crush = 0.3f, Hit = 0.22f, Fit0 = 0.34f, End = 0.8f;
            bool dusted = false;
            float t = 0f;
            while (t < End)
            {
                float dt = Time.deltaTime;
                t += dt;
                // THE CRUSH: the bands are driven into the rim and flattened along the push.
                float ck = Mathf.Clamp01(t / Crush);
                float ce = ck * ck;
                for (int i = 0; i < pieces.Count; i++)
                {
                    if (pieces[i] == null)
                    {
                        continue;
                    }
                    Vector2 dir = push[i];
                    pieces[i].transform.localPosition = from[i] + dir * cell * 0.5f * ce;
                    float flat = Mathf.Lerp(1f, 0.08f, ce);
                    float wide = Mathf.Lerp(1f, 1.18f, ce);
                    float sx = Mathf.Abs(dir.x) > 0.5f ? flat : wide;
                    float sy = Mathf.Abs(dir.y) > 0.5f ? flat : wide;
                    if (Mathf.Abs(dir.x) > 0.5f && Mathf.Abs(dir.y) > 0.5f)
                    {
                        sx = flat;
                        sy = flat;
                    }
                    pieces[i].transform.localScale = new Vector3(baseSize[i] * sx, baseSize[i] * sy, 1f);
                    Color c = pieces[i].color;
                    c.a = 1f - Mathf.Clamp01((ck - 0.75f) / 0.25f);
                    pieces[i].color = c;
                }
                if (!dusted && t >= Hit)
                {
                    dusted = true;
                    for (int i = 0; i < rim.Count; i += 1)
                    {
                        Vector2 at = view.transform.TransformPoint(view.CellToWorld(rim[i]) - rimAxis[i] * cell * 0.5f);
                        Vector2 side = new Vector2(rimAxis[i].y, rimAxis[i].x);
                        motes.Add(Mote.Make(this, ViewUtil.GlowSprite, at,
                            side * cell * UnityEngine.Random.Range(-1.5f, 1.5f) - rimAxis[i] * cell * 0.6f,
                            cell * 0.5f, Dust, 0.45f, BoardOrder + 3, 3f, 0f, 0.45f));
                    }
                }
                // THE RIM'S CUBES take the blow: squashed along the push, then a bounce.
                float hk = Mathf.Clamp01((t - Hit) / 0.34f);
                float squash = hk <= 0f ? 0f : Mathf.Sin(hk * Mathf.PI * 1.5f) * Mathf.Exp(-hk * 3f);
                for (int i = 0; i < rim.Count; i++)
                {
                    float a = 1f - 0.24f * squash;
                    float b = 1f + 0.12f * squash;
                    Vector2 ax = rimAxis[i];
                    Vector2 f = ax.x != 0 && ax.y != 0 ? new Vector2(a, a)
                        : ax.x != 0 ? new Vector2(a, b) : new Vector2(b, a);
                    view.SetCellSquash(rim[i], f);
                }
                // THE REFIT: the tighter arena grows to its new cell size.
                float fk = Mathf.Clamp01((t - Fit0) / (End - Fit0));
                view.SetInflate(Mathf.LerpUnclamped(s0, 1f, EaseOutBack(fk, 1.1f)));
                StepMotes(motes, dt);
                yield return null;
            }
            ClearMotes(motes);
            foreach (SpriteRenderer p in pieces)
            {
                if (p != null)
                {
                    Destroy(p.gameObject);
                }
            }
            if (view != null)
            {
                view.SetInflate(1f);
                view.ClearCellSquash();
            }
            running--;
        }

        private static bool InRect(GameBoard board, GridPos p)
        {
            return p.X >= board.MinX && p.X < board.MinX + board.Width
                && p.Y >= board.MinY && p.Y < board.MinY + board.Height;
        }

        /// <summary>The way a doomed cell is pushed: toward the surviving board, one axis or both.
        /// </summary>
        private static Vector2 PushOf(GameBoard after, GridPos p)
        {
            float x = p.X < after.MinX ? 1f : p.X >= after.MinX + after.Width ? -1f : 0f;
            float y = p.Y < after.MinY ? 1f : p.Y >= after.MinY + after.Height ? -1f : 0f;
            return new Vector2(x, y);
        }

        /// <summary>A board's cell area in the view's local space.</summary>
        private static Rect CellRect(BoardView view, GameBoard board)
        {
            float c = view.CellWorldSize;
            Vector2 a = view.CellToWorld(new GridPos(board.MinX, board.MinY)) - new Vector2(c, c) * 0.5f;
            Vector2 b = view.CellToWorld(new GridPos(board.MinX + board.Width - 1, board.MinY + board.Height - 1))
                + new Vector2(c, c) * 0.5f;
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        private static Rect LerpRect(Rect a, Rect b, float k)
        {
            return Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, k), Mathf.Lerp(a.yMin, b.yMin, k),
                Mathf.Lerp(a.xMax, b.xMax, k), Mathf.Lerp(a.yMax, b.yMax, k));
        }

        private static void PlaceSeam(SpriteRenderer seam, Vector2 at, Vector2 size, float alpha)
        {
            if (seam == null)
            {
                return;
            }
            seam.transform.localPosition = at;
            seam.size = size; // a sliced plate is sized on the renderer, never the transform
            seam.color = Fade(Warm, alpha);
        }

        // ================================================================= helpers

        /// <summary>A renderer and the colour it was built in, so a group can fade as one.</summary>
        private struct Part
        {
            public SpriteRenderer R;
            public Color C;

            public Part(SpriteRenderer r, Color c)
            {
                R = r;
                C = c;
            }
        }

        private static void SetGroupAlpha(List<Part> parts, float alpha)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].R != null)
                {
                    parts[i].R.color = Fade(parts[i].C, parts[i].C.a * alpha);
                }
            }
        }

        /// <summary>Hides any part whose centre is still under the floor line, so the pole reads as
        /// coming up OUT of the ground rather than fading in over it.</summary>
        private static void ClipBelow(List<Part> parts, float floorY)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].R != null)
                {
                    parts[i].R.enabled = parts[i].R.transform.position.y >= floorY;
                }
            }
        }

        /// <summary>One loose particle: a velocity, drag, gravity, a life and a fade.</summary>
        private sealed class Mote
        {
            public SpriteRenderer R;
            public Vector2 V;
            public float Drag;
            public float Gravity;
            public float Life;
            public float Age;
            public float Size;
            public float Alpha;
            public Color C;

            public static Mote Make(PowerFxView owner, Sprite sprite, Vector2 at, Vector2 velocity, float size,
                Color colour, float alpha, int order, float drag, float gravity, float life)
            {
                SpriteRenderer r = MakeShape(owner.transform, "Mote", sprite, at, size, colour, order);
                r.color = Fade(colour, alpha);
                r.transform.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
                return new Mote
                {
                    R = r, V = velocity, Drag = drag, Gravity = gravity, Life = life, Size = size,
                    Alpha = alpha, C = colour
                };
            }
        }

        private static void StepMotes(List<Mote> motes, float dt)
        {
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                m.Age += dt;
                if (m.R == null || m.Age >= m.Life)
                {
                    if (m.R != null)
                    {
                        Destroy(m.R.gameObject);
                    }
                    motes.RemoveAt(i);
                    continue;
                }
                m.V *= Mathf.Max(0f, 1f - m.Drag * dt);
                m.V += Vector2.down * m.Gravity * dt;
                m.R.transform.position += (Vector3)(m.V * dt);
                float k = m.Age / m.Life;
                m.R.transform.localScale = Vector3.one * m.Size * (1f + 0.4f * k);
                m.R.color = Fade(m.C, m.Alpha * (1f - k) * (1f - k));
            }
        }

        private static void ClearMotes(List<Mote> motes)
        {
            foreach (Mote m in motes)
            {
                if (m.R != null)
                {
                    Destroy(m.R.gameObject);
                }
            }
            motes.Clear();
        }

        private IEnumerator Burst(Vector2 world, float size, Color colour, int order, float seconds)
        {
            SpriteRenderer glow = MakeShape(transform, "Burst", ViewUtil.GlowSprite, world, size * 0.2f, colour, order);
            yield return Animate(seconds, k =>
            {
                glow.transform.localScale = Vector3.one * Mathf.Lerp(size * 0.2f, size, 1f - (1f - k) * (1f - k));
                glow.color = new Color(colour.r, colour.g, colour.b, 0.8f * (1f - k));
            });
            Destroy(glow.gameObject);
        }

        private static IEnumerator Animate(float seconds, Action<float> step)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                step(Mathf.Clamp01(t / seconds));
                yield return null;
            }
            step(1f);
        }

        private static SpriteRenderer MakeRound(Transform parent, string name, Vector2 at, Vector2 size,
            Color colour, int layer)
        {
            return ViewUtil.MakeRounded(parent, name, at, size, colour, BoardOrder + 2 + layer);
        }

        /// <summary>A one-unit baked shape at a WORLD position, scaled to <paramref name="size"/>.
        /// </summary>
        private static SpriteRenderer MakeShape(Transform parent, string name, Sprite sprite, Vector2 world,
            float size, Color colour, int order)
        {
            SpriteRenderer r = ViewUtil.MakeIcon(parent, name, Vector2.zero, size, colour, order, sprite);
            r.transform.position = world;
            return r;
        }

        private static Color Fade(Color c, float alpha)
        {
            return new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
        }

        private static Color Clear(Color c)
        {
            return new Color(c.r, c.g, c.b, 0f);
        }

        private static float EaseOut(float k)
        {
            return 1f - (1f - k) * (1f - k) * (1f - k);
        }

        private static float EaseOutBack(float k, float overshoot)
        {
            float c3 = overshoot + 1f;
            float m = k - 1f;
            return 1f + c3 * m * m * m + overshoot * m * m;
        }

        /// <summary>0 outside [a, b], rising to 1 in the middle and back.</summary>
        private static float Bump(float t, float a, float b)
        {
            if (t <= a || t >= b)
            {
                return 0f;
            }
            return Mathf.Sin((t - a) / (b - a) * Mathf.PI);
        }

        // ------------------------------------------------------------ baked shapes

        private static Sprite leaf;
        private static Sprite ring;
        private static Sprite ray;
        private static Sprite disc;
        private static Sprite diamond;
        private static Sprite feather;

        /// <summary>A clover leaf: a heart with its point at the pivot (bottom centre), a vein down
        /// its middle and a lit rim.</summary>
        private static Sprite Leaf_
        {
            get
            {
                return leaf != null ? leaf : leaf = Bake("FxLeaf", 128, new Vector2(0.5f, 0f), (x, y) =>
                {
                    float r = 0.27f;
                    float c1 = new Vector2(x + 0.2f, y - 0.64f).magnitude - r;
                    float c2 = new Vector2(x - 0.2f, y - 0.64f).magnitude - r;
                    float w = 0.46f * Mathf.Clamp01((y - 0.03f) / 0.6f);
                    float tri = Mathf.Max(Mathf.Abs(x) - w, 0.03f - y);
                    tri = Mathf.Max(tri, y - 0.7f);
                    float d = Mathf.Min(Mathf.Min(c1, c2), tri);
                    float a = Mathf.Clamp01(0.5f - d / 0.018f);
                    float vein = Mathf.Abs(x) < 0.014f && y < 0.82f ? 0.72f : 1f;
                    float rimLit = Mathf.Clamp01(1f + d / 0.05f);
                    float lum = (0.78f + 0.14f * y + 0.1f * rimLit) * vein;
                    return new Vector2(a, lum);
                });
            }
        }

        /// <summary>A soft-edged triangle, point up, centred.</summary>
        private static Sprite Tri
        {
            get
            {
                return tri != null ? tri : tri = Bake("FxTri", 96, new Vector2(0.5f, 0.5f), (x, y) =>
                {
                    float half = 0.48f * (1f - y);
                    float d = Mathf.Max(Mathf.Abs(x) - half, Mathf.Max(0.02f - y, y - 0.98f));
                    float a = Mathf.Clamp01(0.5f - d / 0.02f);
                    return new Vector2(a, 0.85f + 0.15f * (1f - y));
                });
            }
        }

        private static Sprite tri;

        private static Sprite Ring
        {
            get
            {
                return ring != null ? ring : ring = Bake("FxRing", 128, new Vector2(0.5f, 0.5f), (x, y) =>
                {
                    float r = new Vector2(x, y - 0.5f).magnitude;
                    float a = Mathf.Exp(-Mathf.Pow((r - 0.43f) / 0.035f, 2f));
                    return new Vector2(a, 1f);
                });
            }
        }

        /// <summary>A beam from its pivot (bottom centre) that tapers and fades out along its length.
        /// </summary>
        private static Sprite Ray
        {
            get
            {
                return ray != null ? ray : ray = Bake("FxRay", 64, new Vector2(0.5f, 0f), (x, y) =>
                {
                    float w = 0.5f * (1f - y * 0.75f);
                    float a = Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(0.001f, w));
                    a = a * a * Mathf.Pow(1f - y, 1.3f) * Mathf.Clamp01(y / 0.12f);
                    return new Vector2(a, 1f);
                });
            }
        }

        /// <summary>A solid disc lit from the upper left.</summary>
        private static Sprite Disc
        {
            get
            {
                return disc != null ? disc : disc = Bake("FxDisc", 96, new Vector2(0.5f, 0.5f), (x, y) =>
                {
                    Vector2 p = new Vector2(x, y - 0.5f);
                    float d = p.magnitude - 0.47f;
                    float a = Mathf.Clamp01(0.5f - d / 0.02f);
                    float lum = 0.78f + 0.3f * Mathf.Clamp01(-p.x - p.y + 0.2f) - 0.12f * Mathf.Clamp01(p.x + p.y);
                    return new Vector2(a, Mathf.Clamp01(lum));
                });
            }
        }

        private static Sprite Diamond
        {
            get
            {
                return diamond != null ? diamond : diamond = Bake("FxDiamond", 64, new Vector2(0.5f, 0.5f), (x, y) =>
                {
                    float d = Mathf.Abs(x) + Mathf.Abs(y - 0.5f) - 0.46f;
                    float a = Mathf.Clamp01(0.5f - d / 0.03f);
                    float lum = 0.8f + 0.2f * Mathf.Clamp01(-x - (y - 0.5f) + 0.1f);
                    return new Vector2(a, lum);
                });
            }
        }

        /// <summary>A feather: a long leaf-shaped blade from its pivot (bottom centre).</summary>
        private static Sprite Feather
        {
            get
            {
                return feather != null ? feather : feather = Bake("FxFeather", 96, new Vector2(0.5f, 0f), (x, y) =>
                {
                    float half = 0.46f * Mathf.Sin(Mathf.Clamp01(y) * Mathf.PI) * (0.55f + 0.45f * y);
                    float d = Mathf.Abs(x) - half;
                    float a = Mathf.Clamp01(0.5f - d / 0.03f) * Mathf.Clamp01(y / 0.04f);
                    float lum = Mathf.Abs(x) < 0.02f ? 0.7f : 0.85f + 0.15f * Mathf.Clamp01(-x * 3f);
                    return new Vector2(a, lum);
                });
            }
        }

        /// <summary>Bakes a white shape (value in rgb, coverage in alpha) at one world unit a side.
        /// The function gets x in [-0.5, 0.5] and y in [0, 1] and returns (alpha, luminance).
        /// </summary>
        private static Sprite Bake(string name, int px, Vector2 pivot, Func<float, float, Vector2> shade)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[px * px];
            for (int j = 0; j < px; j++)
            {
                for (int i = 0; i < px; i++)
                {
                    float x = (i + 0.5f) / px - 0.5f;
                    float y = (j + 0.5f) / px;
                    Vector2 v = shade(x, y);
                    byte a = (byte)(Mathf.Clamp01(v.x) * 255f);
                    byte l = (byte)(Mathf.Clamp01(v.y) * 255f);
                    pixels[j * px + i] = new Color32(l, l, l, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, px, px), pivot, px);
            sprite.name = name;
            return sprite;
        }
    }
}
