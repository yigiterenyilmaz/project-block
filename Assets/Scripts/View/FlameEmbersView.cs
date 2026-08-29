// PURPOSE: The embers thrown off the overtime fire - small sparks that lift from the corner
// flames, drift, cool and go out. Driven entirely by FlameStreakView, which says WHERE the fire
// is and how hot; nothing here knows about rounds or overtime.
//
// THEY ARE SOFT ROUND MOTES, and that was a deliberate reversal. They were flat squares first,
// on the reading that this board has no gradients or glows anywhere (see CLAUDE.md on
// CellFlashFx) - but that rule is about DESTRUCTION on the board's own cells, and the painted
// flame that these come off already carries gradients of its own. Squares read as debris rather
// than sparks next to it. The falloff texture is still GENERATED, so no asset arrives with it.
//
// NOT A ParticleSystem. Unity's would work, but this is a few dozen sprites following four lines
// of arithmetic, and the module set it would take to express the same thing (rate over a shaped
// emitter, size and colour over lifetime, drag, a noise field) is what made the old fire 600
// lines. A fixed pool with a Vector2 velocity is smaller than its own configuration would be.
//
// HOW ONE LIVES. It is born inside the base of a flame, not at a point: a spark that leaves from
// a single spot reads as a fountain. It rises, DECELERATING (see Drag) so it hangs at the top of
// its arc the way a real ember does, drifts sideways along a slow sine that is its own, shrinks,
// and fades. When it dies it is reborn - the pool never allocates after the first Rebuild.
//
// COLOUR RUNS HOT TO COOL over the life, never to soot: a colour walked down to grey is invisible
// against a dark backdrop long before it reaches the end, so these die on ALPHA while still
// orange. That rule was carried over from the particle fire, which looked like ashes without it.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Embers lifting off the overtime fire. Fed by FlameStreakView.</summary>
    public sealed class FlameEmbersView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the embers LOOK, in one place.</summary>
        public static class Style
        {
            /// <summary>Embers alive per emitter at the first overtime level, and at full heat.
            /// Per EMITTER, so the count follows the fire rather than a constant.</summary>
            public static float CountLow = 7f;

            public static float CountHigh = 20f;

            /// <summary>Ember size as a fraction of the flame's height. Larger than the flat
            /// squares these replaced, because a soft mote's visible core is a good deal smaller
            /// than the sprite it is drawn on.</summary>
            public static float SizeLow = 0.016f;

            public static float SizeHigh = 0.034f;

            /// <summary>How far up an ember gets, as a fraction of the flame's height, before it
            /// is spent. Over 1 so the best of them clear the tip - embers that all die inside
            /// the flame are never seen.</summary>
            public static float Rise = 1.15f;

            /// <summary>Seconds an ember lives, before its own variation. This is also the SPEED
            /// dial: the launch speed is derived from Rise/Life so an ember still just clears the
            /// flame, which means a longer life is a slower drift over the same distance rather
            /// than a spark that flies further. At 1.6 they crossed the screen in well under two
            /// seconds and read as sparks off a grinder.</summary>
            public static float Life = 3.0f;

            /// <summary>How much of its speed an ember loses per second. This is what makes it
            /// slow as it climbs and hang before going out, instead of shooting off the top of
            /// the screen at a constant rate. Eased off with the longer life - at 1.35 an ember
            /// that now lives twice as long stalled halfway and just sat there.</summary>
            public static float Drag = 0.8f;

            /// <summary>Sideways drift, as a fraction of the rise speed, and how fast that drift
            /// swings. Each ember gets its own phase, so they never sway together.</summary>
            public static float Drift = 0.30f;

            public static float DriftRate = 1.0f;

            /// <summary>Where in the flame embers are born: a fraction of its width across, and
            /// of its height up from the foot. Born in the BASE, which is where a fire actually
            /// sheds them.</summary>
            public static float SpawnWidth = 0.55f;

            public static float SpawnHeight = 0.35f;

            /// <summary>Colour at birth and just before death. Hot to cool, and never to soot -
            /// an ember that greys out has already disappeared against the backdrop, so these
            /// stay orange and die on alpha alone.</summary>
            public static Color Hot = new Color(1f, 0.93f, 0.62f);

            public static Color Cool = new Color(0.95f, 0.42f, 0.12f);

            /// <summary>Fraction of its life an ember spends fading out. The rest is full alpha:
            /// a spark that fades from the moment it appears never looks lit.</summary>
            public static float FadeTail = 0.55f;

            /// <summary>What an ember shrinks to by the end, as a fraction of its birth size.</summary>
            public static float ShrinkTo = 0.35f;
        }

        // =================================================================== internals

        /// <summary>Above the flames (4) so a spark is never hidden behind the fire it left, and
        /// still far below anything the player reads.</summary>
        private const int SortingOrder = 5;

        private struct Ember
        {
            public SpriteRenderer Renderer;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public float Phase;
            public int Emitter;
        }

        /// <summary>Where embers are born - the foot of each flame. Held rather than passed every
        /// frame: the fire only moves when the round does.</summary>
        private readonly List<Vector2> emitters = new List<Vector2>();
        private Ember[] embers = new Ember[0];
        private int live;
        private float flameWidth;
        private float flameHeight;
        private float heat;
        private System.Random rng = new System.Random(8675309);
        private static Sprite emberSprite;

        /// <summary>Called by FlameStreakView whenever the fire is rebuilt. `heat` is 0..1;
        /// passing an empty list or zero heat puts every ember out.</summary>
        public void SetState(List<Vector2> feet, float width, float height, float heatLevel)
        {
            emitters.Clear();
            if (feet != null)
            {
                emitters.AddRange(feet);
            }
            flameWidth = width;
            flameHeight = height;
            heat = Mathf.Clamp01(heatLevel);

            int want = emitters.Count == 0 || heat <= 0f || height <= 0f
                ? 0
                : emitters.Count * Mathf.RoundToInt(
                    Mathf.Lerp(Style.CountLow, Style.CountHigh, heat));

            EnsureCapacity(want);
            for (int i = live; i < want; i++)
            {
                // Staggered ages, so a fire that has just been lit is not a single volley.
                Spawn(i);
                embers[i].Age = Range(0f, embers[i].Life);
            }
            for (int i = want; i < embers.Length; i++)
            {
                if (embers[i].Renderer != null)
                {
                    embers[i].Renderer.enabled = false;
                }
            }
            live = want;
        }

        private void Update()
        {
            if (live == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < live; i++)
            {
                Ember e = embers[i];
                e.Age += dt;
                if (e.Age >= e.Life)
                {
                    embers[i] = e;
                    Spawn(i);
                    continue;
                }

                // Decelerate rather than coast: an ember that keeps its launch speed reads as a
                // projectile, and the hang at the top of the arc is most of what sells it.
                e.Velocity *= Mathf.Max(0f, 1f - Style.Drag * dt);
                float sway = Mathf.Sin(e.Age * Style.DriftRate + e.Phase)
                    * Style.Drift * (flameHeight * Style.Rise / Style.Life);
                e.Position += (e.Velocity + new Vector2(sway, 0f)) * dt;

                float t = e.Age / e.Life;
                float size = e.Size * Mathf.Lerp(1f, Style.ShrinkTo, t);
                float alpha = t < 1f - Style.FadeTail
                    ? 1f
                    : 1f - (t - (1f - Style.FadeTail)) / Style.FadeTail;

                Color c = Color.Lerp(Style.Hot, Style.Cool, Mathf.Clamp01(t * 1.4f));
                c.a = Mathf.Clamp01(alpha);
                e.Renderer.color = c;
                e.Renderer.transform.localPosition = e.Position;
                e.Renderer.transform.localScale = new Vector3(size, size, 1f);
                embers[i] = e;
            }
        }

        /// <summary>(Re)births ember `i` at the base of one of the flames.</summary>
        private void Spawn(int i)
        {
            Ember e = embers[i];
            e.Emitter = emitters.Count == 0 ? 0 : rng.Next(emitters.Count);
            Vector2 foot = emitters[e.Emitter];

            // Across the base and a little way up it, never from a single point.
            e.Position = foot + new Vector2(
                Range(-0.5f, 0.5f) * flameWidth * Style.SpawnWidth,
                Range(0f, 1f) * flameHeight * Style.SpawnHeight);

            e.Life = Style.Life * Range(0.7f, 1.35f);
            // Launched fast enough that, after Drag has eaten most of it, the ember has covered
            // about Rise x the flame's height.
            float speed = flameHeight * Style.Rise / Style.Life * Range(1.05f, 1.6f);
            e.Velocity = new Vector2(Range(-0.18f, 0.18f) * speed, speed);
            e.Size = flameHeight * Mathf.Lerp(Style.SizeLow, Style.SizeHigh, heat)
                * Range(0.65f, 1.35f);
            e.Phase = Range(0f, 6.283f);
            e.Age = 0f;
            e.Renderer.enabled = true;
            embers[i] = e;
        }

        private void EnsureCapacity(int want)
        {
            if (embers.Length >= want)
            {
                return;
            }
            var grown = new Ember[want];
            System.Array.Copy(embers, grown, embers.Length);
            for (int i = embers.Length; i < want; i++)
            {
                var go = new GameObject("Ember" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = EmberSprite;
                sr.sortingOrder = SortingOrder;
                grown[i] = new Ember { Renderer = sr };
            }
            embers = grown;
        }

        /// <summary>The mote: a round falloff, generated once and shared. Biweight, (1-d^2)^2,
        /// which reaches zero with a zero slope - so there is no rim anywhere on it and it reads
        /// as light rather than as a disc. Small, because it is only ever drawn a few pixels
        /// across and the falloff is all of the shape there is.</summary>
        private static Sprite EmberSprite
        {
            get
            {
                if (emberSprite != null)
                {
                    return emberSprite;
                }
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.hideFlags = HideFlags.HideAndDontSave;
                var px = new Color[n * n];
                for (int y = 0; y < n; y++)
                {
                    float dy = (y / (float)(n - 1) - 0.5f) * 2f;
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x / (float)(n - 1) - 0.5f) * 2f;
                        float k = Mathf.Clamp01(1f - (dx * dx + dy * dy));
                        px[y * n + x] = new Color(1f, 1f, 1f, k * k);
                    }
                }
                tex.SetPixels(px);
                tex.Apply();
                emberSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n),
                    new Vector2(0.5f, 0.5f), n);
                return emberSprite;
            }
        }

        private float Range(float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }
    }
}
