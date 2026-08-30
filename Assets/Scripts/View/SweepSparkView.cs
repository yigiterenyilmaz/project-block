// PURPOSE: The motes a CLEAN SWEEP leaves behind - small drawn fragments of light that appear
// in the wake of the cleansing wave, drift a little way and dissolve where they stand. Driven
// from BoardCleanseView as the front passes each cell, never scattered over the board at once.
//
// THEY ARE RESIDUE, NOT A BURST. The first version threw them across the whole arena the moment
// the sweep landed, big and fast, and they read as debris escaping a detonation - which is the
// wrong event entirely. A full-board clear is a cleanse: what is left over should look like the
// last of something dissolving, so a mote here is small, slow, stays near the cell it came from
// and goes out by shrinking and fading rather than by flying off. Nothing is thrown.
//
// THE SHEET IS THREE ANIMATIONS, NOT ONE. It arrived as a 3x3, and read as a single nine-frame
// burst its light goes 0.2 then 1.1 between neighbouring frames - a spark cannot come back to
// life. Read as COLUMNS it is three separate three-frame bursts, and all three then run clean:
// energy 3.4/3.3/1.1, 3.8/1.3/0.4, 3.3/0.2/0.1, with the radius growing in each. So a column is
// a variant and a row is a frame, which is what keeps thirty motes from looking stamped.
//
// THE FRAMES AND THE LIFE ARE SEPARATE CLOCKS. Three drawn frames is a fifth of a second, far
// too short for something that is supposed to dissolve, so the sheet plays out and HOLDS on its
// last frame while the mote goes on drifting, shrinking and fading. The drawing is the shape;
// the life is the behaviour.
//
// THE COLOUR IS THE ART'S. The sheet is packed as drawn and the sweep's flash was moved to match
// it, not the other way round: a tint can only take colour away from a drawing, and it takes the
// white-hot centres with it. The tint below still works and starts at white.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The clean-sweep motes. Emit them one or two at a time; they run themselves
    /// out.</summary>
    public sealed class SweepSparkView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>How big one mote is drawn, in CELLS. The art fills about three quarters
            /// of its frame, so the lit part comes out around a third of a cell - these have to
            /// read as fragments of light, and anything approaching cell-sized reads as a
            /// bubble.</summary>
            public static float Size = 0.88f;

            public static float SizeJitter = 0.30f;

            /// <summary>Frames per second through the three drawn frames. Slow: the mote is
            /// settling, not going off.</summary>
            public static float Fps = 11f;

            /// <summary>How long a mote exists, which is far longer than its three frames. The
            /// drawing holds on its last frame and the mote goes on dissolving.</summary>
            public static float Lifetime = 0.55f;

            public static float LifetimeJitter = 0.28f;

            /// <summary>How fast a mote sets off, in CELLS per second. This is the number that
            /// decides whether these look like residue or like debris: at a third of a cell per
            /// second a mote barely leaves the square it came from.</summary>
            public static float Speed = 0.52f;

            /// <summary>How much of its speed a mote keeps each second. Well under 1, so it is
            /// nearly stopped by the time it is halfway through its life - the deceleration is
            /// what makes it look like it is dissolving in place.</summary>
            public static float Damping = 0.06f;

            /// <summary>A gentle upward bias, in cells per second squared. Light rises; it is
            /// the one cue that says this is energy rather than dust settling.</summary>
            public static float Lift = 0.22f;

            /// <summary>How far a mote may stray from where it appeared, in cells. A hard stop,
            /// so nothing can ever cross the board however the numbers above are tuned.</summary>
            public static float MaxTravel = 0.80f;

            /// <summary>How small a mote gets before it goes out, as a fraction of its size.</summary>
            public static float ShrinkTo = 0.35f;

            /// <summary>How much of its life a mote spends at full strength before it starts
            /// letting go.</summary>
            public static float HoldFraction = 0.25f;

            /// <summary>How far a mote appears from the middle of the cell it belongs to, in
            /// cells, so a cell's motes are not stacked on its centre point.</summary>
            public static float Spread = 0.44f;

            public static float Spin = 180f;

            /// <summary>Ceiling on how many can be alive at once. A cleanse asks for one mote
            /// every several cells, so this is only a guard.</summary>
            public static int MaxLive = 96;
        }

        /// <summary>Over the board and the cleanse wave, under the score popups.</summary>
        private const int SortingOrder = 13;

        private const int VariantCount = 3;

        private const int FramesPerVariant = 3;

        private const string SheetPath = "Art/Fx/sweep_spark_sheet";

        // =================================================================== the sheet

        private static Sprite[][] variants;   // [variant][frame]

        private static bool loaded;

        /// <summary>The three variants, sliced once and shared. Null when the sheet is not in
        /// the project, in which case the cleanse simply runs without motes.</summary>
        private static Sprite[][] Variants()
        {
            if (loaded)
            {
                return variants;
            }
            loaded = true;
            var sheet = Resources.Load<Texture2D>(SheetPath);
            if (sheet == null)
            {
                Debug.LogWarning("[block_bonk] Sweep spark sheet missing: Resources/" + SheetPath);
                return null;
            }
            int cw = sheet.width / VariantCount;
            int ch = sheet.height / FramesPerVariant;
            var built = new Sprite[VariantCount][];
            for (int v = 0; v < VariantCount; v++)
            {
                built[v] = new Sprite[FramesPerVariant];
                for (int f = 0; f < FramesPerVariant; f++)
                {
                    // Texture y counts from the BOTTOM while the sheet reads top row first.
                    int rectY = (FramesPerVariant - 1 - f) * ch;
                    built[v][f] = Sprite.Create(sheet, new Rect(v * cw, rectY, cw, ch),
                        new Vector2(0.5f, 0.5f), cw);   // PPU = cell width: 1 unit at scale 1
                }
            }
            variants = built;
            return built;
        }

        // =================================================================== one live mote

        private sealed class Mote
        {
            public SpriteRenderer Renderer;
            public int Variant;
            public Vector2 Spawn;
            public Vector2 Origin;
            public Vector2 Velocity;
            public float Size;
            public float Cell;
            public float Life;
            public float Age;
            public bool Running;
        }

        private readonly System.Collections.Generic.List<Mote> motes =
            new System.Collections.Generic.List<Mote>();

        private Color tint = Color.white;

        /// <summary>
        /// Puts <paramref name="count"/> motes at a cell. Called by BoardCleanseView as the wave
        /// front passes, so where they appear is already the story of the wave - nothing here
        /// decides placement beyond a little spread inside the cell.
        /// </summary>
        public void EmitAt(Vector2 world, float cellSize, int count)
        {
            Sprite[][] art = Variants();
            if (art == null || cellSize <= 0f)
            {
                return;
            }
            for (int i = 0; i < count; i++)
            {
                Mote m = Take();
                if (m == null)
                {
                    return;
                }
                float spread = Style.Spread * cellSize;
                m.Origin = world + new Vector2(
                    Random.Range(-spread, spread), Random.Range(-spread, spread));
                m.Spawn = m.Origin;
                m.Cell = cellSize;
                // Set off in ANY direction rather than outward from anything: these are not
                // thrown by the wave, they are what the wave left behind.
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float speed = Style.Speed * cellSize * Random.Range(0.5f, 1f);
                m.Velocity = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;
                m.Variant = Random.Range(0, VariantCount);
                m.Size = cellSize * Style.Size
                    * (1f + Random.Range(-Style.SizeJitter, Style.SizeJitter));
                m.Life = Style.Lifetime
                    * (1f + Random.Range(-Style.LifetimeJitter, Style.LifetimeJitter));
                m.Age = 0f;
                m.Running = true;
                m.Renderer.transform.localPosition = new Vector3(m.Origin.x, m.Origin.y, 0f);
                m.Renderer.transform.localRotation =
                    Quaternion.Euler(0f, 0f, Random.Range(-Style.Spin, Style.Spin));
                m.Renderer.transform.localScale = new Vector3(m.Size, m.Size, 1f);
                m.Renderer.sprite = art[m.Variant][0];
                m.Renderer.color = tint;
                m.Renderer.enabled = true;
            }
        }

        /// <summary>What the motes are multiplied by. The art already carries its colour, so
        /// white leaves it alone.</summary>
        public void SetTint(Color colour)
        {
            tint = colour;
        }

        private void Update()
        {
            Sprite[][] art = Variants();
            if (art == null)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < motes.Count; i++)
            {
                Mote m = motes[i];
                if (!m.Running)
                {
                    continue;
                }
                m.Age += dt;
                if (m.Age >= m.Life)
                {
                    m.Running = false;
                    m.Renderer.enabled = false;
                    continue;
                }
                float k = m.Age / m.Life;

                // Drift, decelerating hard, with a little lift. Clamped to MaxTravel so a mote
                // can never wander off the cell it belongs to whatever the tuning says.
                m.Velocity *= Mathf.Pow(Style.Damping, dt);
                m.Velocity.y += Style.Lift * m.Cell * dt;
                m.Origin += m.Velocity * dt;
                // The hard stop the tuning is allowed to rely on: however the speed and damping
                // are set, a mote can never leave the neighbourhood of the cell it came from.
                Vector2 off = m.Origin - m.Spawn;
                float cap = Style.MaxTravel * m.Cell;
                if (off.sqrMagnitude > cap * cap)
                {
                    m.Origin = m.Spawn + off.normalized * cap;
                    m.Velocity = Vector2.zero;
                }
                m.Renderer.transform.localPosition =
                    new Vector3(m.Origin.x, m.Origin.y, 0f);

                int frame = Mathf.Min(FramesPerVariant - 1,
                    Mathf.FloorToInt(m.Age * Style.Fps));
                m.Renderer.sprite = art[m.Variant][frame];

                // Hold, then dissolve: shrink and fade together, because either one alone reads
                // as a trick - fading alone leaves a ghost the same size, shrinking alone leaves
                // a hard dot that vanishes.
                float fade = k < Style.HoldFraction
                    ? 1f
                    : 1f - (k - Style.HoldFraction) / (1f - Style.HoldFraction);
                fade = Mathf.Clamp01(fade);
                fade *= fade;
                Color c = tint;
                c.a = tint.a * fade;
                m.Renderer.color = c;
                float s = m.Size * Mathf.Lerp(Style.ShrinkTo, 1f, fade);
                m.Renderer.transform.localScale = new Vector3(s, s, 1f);
            }
        }

        private Mote Take()
        {
            for (int i = 0; i < motes.Count; i++)
            {
                if (!motes[i].Running)
                {
                    return motes[i];
                }
            }
            if (motes.Count >= Style.MaxLive)
            {
                return null;
            }
            var go = new GameObject("Mote" + motes.Count);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = SortingOrder;
            r.enabled = false;
            var made = new Mote { Renderer = r };
            motes.Add(made);
            return made;
        }

        /// <summary>Stops everything at once - the run ending, the board being torn down.</summary>
        public void Stop()
        {
            for (int i = 0; i < motes.Count; i++)
            {
                motes[i].Running = false;
                if (motes[i].Renderer != null)
                {
                    motes[i].Renderer.enabled = false;
                }
            }
        }
    }
}
