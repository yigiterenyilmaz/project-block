// PURPOSE: The SMOKE a dynamite detonation leaves behind - a cloud born AT the bomb that is
// thrown outward, loses its momentum almost at once, then drifts, swells and thins out.
// Spawn-and-forget, like FloatingTextFx.
//
// IT USED TO BE A SCREENFUL OF SQUARES. The old cloud filled the whole camera rect from a
// jittered grid, and every puff was ViewUtil.WhiteSprite - a one-pixel WHITE SQUARE stretched
// to nearly seven world units. That is why it read as "someone put a picture of smoke on the
// screen": the puffs were literally squares, they were the size of the arena, and where they
// were had nothing to do with where the bomb was. The concept survives - puffs that billow,
// drift, grow and thin - but all three of those are fixed here.
//
// THE PUFF IS ROUND AND SOFT, and generated rather than drawn: a radial falloff with a little
// lumpiness around its rim so a single puff is not a disc either. Nothing is imported, which is
// the same bargain the rest of this layer makes.
//
// THERE ARE THREE BEHAVIOURS, NOT ONE. Hot smoke goes first - small, fast, short. The main
// cloud follows it out and does the work. A few residuals hang on at low alpha after the rest
// has gone. All of them share one motion law: a short outward impulse, heavy drag that kills it
// inside about a tenth of a second, then slow drift and swelling while the alpha falls. Smoke
// that keeps its speed reads as debris; this has to look like it is settling.
//
// IT FILLS THE SCREEN, BUT IT GETS THERE FROM THE BOMB. The old cloud covered the camera by
// being BORN there - a jittered grid of squares laid over the viewport, which is why it read as
// a picture rather than as smoke. This one starts as a point at the blast and expands out to the
// edges, so the coverage is the same and the story is not. Two things carry it: the impulse,
// which is spent almost at once, and an EXPANSION term that keeps pushing outward while fading
// with the puff's life. The expansion speed is heavily jittered per puff - some barely leave the
// bomb and some run to the corner - which is what fills the middle as well as the rim instead of
// blowing a hole in the cloud.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>A cloud of drifting smoke, thrown from a point. See SmokeFx.Burst.</summary>
    public sealed class SmokeFx : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>How many of each layer. High: the cloud is meant to CHOKE the screen,
            /// and with soft overlapping puffs that is a question of total covered area rather
            /// than of any one puff being big. Counts and sizes together put about four screens
            /// of puff over one screen, which is what makes the middle read as solid while the
            /// rim still has holes in it.</summary>
            public static int HotCount = 18;

            public static int MainCount = 38;

            public static int ResidualCount = 20;

            /// <summary>Sizes in CELLS, from birth to death. Nothing here approaches the size of
            /// the board, which is the whole reason the old cloud read as a pasted-on picture.</summary>
            public static float HotFrom = 0.6f;

            public static float HotTo = 3.0f;

            public static float MainFrom = 1.0f;

            public static float MainTo = 5.4f;

            public static float ResidualFrom = 0.8f;

            public static float ResidualTo = 5.0f;

            /// <summary>How varied one layer's puffs are, as a fraction of their size.</summary>
            public static float SizeJitter = 0.34f;

            /// <summary>The outward throw at birth, in cells per second.</summary>
            public static float HotSpeed = 12f;

            public static float MainSpeed = 7f;

            public static float ResidualSpeed = 4f;

            /// <summary>Fraction of speed kept per second. Tiny, so the throw is spent almost at
            /// once and what is left is drift - this is the number that decides whether the
            /// smoke settles or flies.</summary>
            public static float Drag = 0.0006f;

            /// <summary>The OUTWARD push that carries the cloud to the screen edges, in cells
            /// per second, fading to nothing over a puff's life. The impulse alone cannot do it -
            /// drag eats it in a tenth of a second - and raising the impulse instead would make
            /// the smoke read as shrapnel. This is the term that expands a cloud, and it is
            /// Per LAYER, because they do not live equally long and one speed then throws the
            /// long-lived residuals clean off the screen while the hot smoke is still near the
            /// bomb: distance is speed x life, so the life has to be paid for here.
            public static float HotExpand = 34f;

            public static float MainExpand = 42f;

            public static float ResidualExpand = 26f;

            /// <summary>How wildly the expansion varies per puff. WIDE on purpose: at a single
            /// speed every puff leaves together and the middle of the cloud empties out. Spread
            /// from a quarter to full and the cloud is dense from the bomb to the rim.</summary>
            public static float ExpandJitter = 0.75f;

            /// <summary>The slow lift once the throw is gone, in cells per second.</summary>
            public static float Drift = 0.55f;

            public static float DriftSpread = 0.45f;

            public static float HotLife = 0.55f;

            public static float MainLife = 1.15f;

            public static float ResidualLife = 1.55f;

            public static float LifeJitter = 0.28f;

            /// <summary>Peak opacity per layer. The main cloud carries the read; the hot smoke
            /// is a flash of it and the residual is nearly gone from the start.</summary>
            public static float HotAlpha = 0.72f;

            public static float MainAlpha = 0.82f;

            public static float ResidualAlpha = 0.46f;

            /// <summary>Fraction of a puff's life spent billowing IN. Short: it arrives fast and
            /// leaves slowly, which is what smoke does.</summary>
            public static float RiseShare = 0.14f;

            /// <summary>How far from the bomb a puff may be born, in cells.</summary>
            public static float SpawnSpread = 0.55f;

            /// <summary>How late the last puff of a layer may start.</summary>
            public static float HotDelay = 0.04f;

            public static float MainDelay = 0.13f;

            public static float ResidualDelay = 0.26f;
        }

        /// <summary>Three flat greys and nothing in between, so the cloud stays a limited
        /// palette like everything else on screen.</summary>
        private static readonly Color[] Greys =
        {
            new Color(0.74f, 0.72f, 0.70f),
            new Color(0.54f, 0.52f, 0.51f),
            new Color(0.35f, 0.34f, 0.34f)
        };

        // =================================================================== the puff art

        private static Sprite[] puffSprites;

        private const int PuffVariants = 4;

        /// <summary>A soft round puff with a lumpy rim. Four of them, so neighbouring puffs are
        /// not the same shape - a cloud built from one disc repeated reads as a pattern the same
        /// way a cloud built from one square reads as a box.</summary>
        private static Sprite[] PuffSprites()
        {
            if (puffSprites != null)
            {
                return puffSprites;
            }
            puffSprites = new Sprite[PuffVariants];
            for (int v = 0; v < PuffVariants; v++)
            {
                const int n = 96;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                tex.hideFlags = HideFlags.HideAndDontSave;
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color32[n * n];
                float p1 = v * 1.7f;
                float p2 = v * 2.9f + 0.6f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = ((x + 0.5f) / n - 0.5f) * 2f;
                        float w = ((y + 0.5f) / n - 0.5f) * 2f;
                        float r = Mathf.Sqrt(u * u + w * w);
                        float ang = Mathf.Atan2(w, u);
                        // The rim wanders, so the silhouette is a lump rather than a circle.
                        float edge = 1f + 0.16f * Mathf.Sin(ang * 3f + p1)
                            + 0.10f * Mathf.Sin(ang * 5f + p2);
                        float k = Mathf.Clamp01(r / Mathf.Max(edge, 0.2f));
                        float a = Mathf.Exp(-2.9f * k * k) - 0.055f;
                        px[y * n + x] = new Color32(255, 255, 255,
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false, false);
                puffSprites[v] = Sprite.Create(tex, new Rect(0, 0, n, n),
                    new Vector2(0.5f, 0.5f), n);
            }
            return puffSprites;
        }

        // =================================================================== one puff

        private struct Puff
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public Vector2 Velocity;
            public Vector2 Out;
            public float Expand;
            public Vector2 Drift;
            public float Delay;
            public float Life;
            public float FromSize;
            public float ToSize;
            public float Alpha;
            public float Spin;
        }

        private Puff[] puffs;

        private float age;

        private float lifetime;

        /// <summary>
        /// Throws a cloud from <paramref name="centre"/>. Sizes and speeds are in CELLS, so the
        /// same call reads the same on a 7x7 and an 11x11.
        /// </summary>
        public static void Burst(Transform parent, Vector2 centre, float cell)
        {
            if (cell <= 0f)
            {
                return;
            }
            var go = new GameObject("Smoke");
            go.transform.SetParent(parent, false);
            SmokeFx fx = go.AddComponent<SmokeFx>();

            var list = new System.Collections.Generic.List<Puff>(
                Style.HotCount + Style.MainCount + Style.ResidualCount);
            fx.AddLayer(list, go.transform, centre, cell, Style.HotCount, Style.HotSpeed,
                Style.HotExpand, Style.HotFrom, Style.HotTo, Style.HotLife, Style.HotAlpha,
                Style.HotDelay);
            fx.AddLayer(list, go.transform, centre, cell, Style.MainCount, Style.MainSpeed,
                Style.MainExpand, Style.MainFrom, Style.MainTo, Style.MainLife, Style.MainAlpha,
                Style.MainDelay);
            fx.AddLayer(list, go.transform, centre, cell, Style.ResidualCount,
                Style.ResidualSpeed, Style.ResidualExpand, Style.ResidualFrom, Style.ResidualTo,
                Style.ResidualLife, Style.ResidualAlpha, Style.ResidualDelay);
            fx.puffs = list.ToArray();
            for (int i = 0; i < fx.puffs.Length; i++)
            {
                fx.lifetime = Mathf.Max(fx.lifetime, fx.puffs[i].Delay + fx.puffs[i].Life);
            }
        }

        private void AddLayer(System.Collections.Generic.List<Puff> into, Transform parent,
            Vector2 centre, float cell, int count, float speed, float expand, float from,
            float to, float life, float alpha, float maxDelay)
        {
            for (int i = 0; i < count; i++)
            {
                // Spread around the circle rather than at random, so a layer cannot leave a
                // quadrant bare - with seven draws pure random regularly does.
                float ang = (i + Random.Range(0.15f, 0.85f)) / count * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float jitter = 1f + Random.Range(-Style.SizeJitter, Style.SizeJitter);
                var go = new GameObject("Puff_" + into.Count);
                go.transform.SetParent(parent, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PuffSprites()[Random.Range(0, PuffVariants)];
                renderer.color = Greys[Random.Range(0, Greys.Length)];
                // Over the cards (they are under the smoke too) but under the popups at 45.
                renderer.sortingOrder = 40 + Random.Range(0, 2);
                renderer.enabled = false;
                go.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                into.Add(new Puff
                {
                    Renderer = renderer,
                    At = centre + dir * cell * Random.Range(0f, Style.SpawnSpread),
                    Velocity = dir * speed * cell * Random.Range(0.6f, 1f),
                    Out = dir,
                    Expand = expand * cell
                        * Random.Range(1f - Style.ExpandJitter, 1f),
                    Drift = new Vector2(
                        Random.Range(-Style.DriftSpread, Style.DriftSpread), Style.Drift) * cell,
                    Delay = Random.Range(0f, maxDelay),
                    Life = life * (1f + Random.Range(-Style.LifeJitter, Style.LifeJitter)),
                    FromSize = cell * from * jitter,
                    ToSize = cell * to * jitter,
                    Alpha = alpha * Random.Range(0.75f, 1f),
                    Spin = Random.Range(-22f, 22f)
                });
            }
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < puffs.Length; i++)
            {
                puffs[i] = Step(puffs[i], age - puffs[i].Delay, dt);
            }
        }

        private static Puff Step(Puff puff, float puffAge, float dt)
        {
            if (puffAge < 0f || puffAge >= puff.Life)
            {
                puff.Renderer.enabled = false;
                return puff;
            }
            puff.Renderer.enabled = true;
            float t = puffAge / puff.Life;

            // The motion law: the throw is killed by drag almost at once, and what carries the
            // puff after that is the slow drift. By halfway through its life it is barely
            // moving, which is what makes it settle rather than sail.
            puff.Velocity *= Mathf.Pow(Style.Drag, dt);
            // Impulse (already dying), plus the expansion that carries the cloud outward and
            // fades with the puff, plus the slow lift. By the last third the expansion is nearly
            // gone, so the cloud stops spreading and simply thins - which is the settling the
            // old version never did.
            Vector2 expand = puff.Out * (puff.Expand * (1f - t) * (1f - t));
            puff.At += (puff.Velocity + expand + puff.Drift) * dt;

            // Grows fast and then keeps swelling slowly - a puff that stopped growing would read
            // as a blob sitting there.
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            float size = Mathf.Lerp(puff.FromSize, puff.ToSize, grow);
            puff.Renderer.transform.localPosition = new Vector3(puff.At.x, puff.At.y, 0f);
            puff.Renderer.transform.localScale = new Vector3(size, size, 1f);
            puff.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f,
                puff.Renderer.transform.localRotation.eulerAngles.z + puff.Spin * dt);

            float alpha = t < Style.RiseShare
                ? t / Style.RiseShare
                : 1f - Mathf.Pow((t - Style.RiseShare) / (1f - Style.RiseShare), 1.6f);
            Color color = puff.Renderer.color;
            color.a = Mathf.Clamp01(alpha) * puff.Alpha;
            puff.Renderer.color = color;
            return puff;
        }
    }
}
