// PURPOSE: Embers drawn IN over the board from its two TOP CORNERS while overtime burns, each
// running diagonally down and inward. The other half of what FlameEmbersView does: those rise
// off the bottom corner fires and go out overhead, these come back in across the arena. Fed by
// FlameStreakView, like the embers, because only that knows whether the fire is lit and how hot.
//
// TWO CORNER MOUTHS, NOT A CEILING. They are born inside a REGION at each top corner - a box a
// fifth of the board across and down, not a point - and pushed on a diagonal towards the middle.
// This was a rain across the whole top edge first, and it read as weather falling past the arena
// rather than as the arena's own corners drawing in: the direction is what makes it belong to
// the fire, and a vertical fall has no direction to read.
//
// THE REGION IS WHAT KEEPS IT FROM BEING A JET. Every mote leaving one point on the same heading
// is a nozzle; scattered over a box, with the heading spread either side, the same motes read as
// a corner smouldering.
//
// THEY DIE INSIDE THE BOARD. Each carries its own travel, so they do not all fade along one line
// and the far end has no visible edge - motes that all reached the same distance would draw a
// second rim across the arena.
//
// IT IS STILL BACKGROUND. The fire and its embers are the event; this is the arena's corners
// smouldering. If a mote reads as a PARTICLE rather than as an ember, the number to lower is
// Alpha or Count, in that order.
//
// THEY PASS THROUGH THE BOARD, not onto it: no collision, no settling, no pile. An ember landing
// on the cubes would be a second thing the player has to read on a board that already carries
// the game, and would need clearing every time the board changed.
//
// The soft round sprite is generated here, so no asset arrives with it - soft for the same
// reason FlameEmbersView is: the painted flame this belongs to already carries gradients of its
// own, and a flat square beside it reads as debris rather than as an ember.

using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Embers drifting in from the arena's top corners. Fed by FlameStreakView.</summary>
    public sealed class FlameDriftView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the drift LOOKS, in one place.</summary>
        public static class Style
        {
            /// <summary>Motes alive across BOTH corners at the first overtime level, and at full
            /// heat.</summary>
            public static float CountLow = 12f;

            public static float CountHigh = 36f;

            /// <summary>Travel speed in world units per second. Fast enough to read as drawn in
            /// rather than as settling - at the top of this range a mote crosses about a third
            /// of the arena a second.</summary>
            public static float SpeedLow = 0.70f;

            public static float SpeedHigh = 1.38f;

            /// <summary>How far a mote gets before it is spent, as a fraction of the board's
            /// short side. The range is per mote, which is what keeps them from all going out
            /// along one line.</summary>
            public static float TravelLow = 0.30f;

            public static float TravelHigh = 0.90f;

            /// <summary>The corner mouth: how far in from the corner motes are born, across and
            /// down, as fractions of the board. A REGION rather than a point - see the header.</summary>
            public static float MouthWidth = 0.22f;

            public static float MouthHeight = 0.20f;

            /// <summary>Heading in degrees BELOW the horizontal, pointing inward: 45 sends a mote
            /// at the middle of the board from either corner.</summary>
            public static float Angle = 45f;

            /// <summary>Spread either side of that heading, in degrees. Without it a corner is a
            /// nozzle firing a rake of parallel lines.</summary>
            public static float Spread = 22f;

            /// <summary>A slow wander ACROSS the heading, and how fast it swings. Each mote has
            /// its own phase, so they never sway together. Small: it is there to stop the paths
            /// being straight lines, not to bend them off course.</summary>
            public static float Drift = 0.16f;

            public static float DriftRate = 0.8f;

            /// <summary>Mote size in world units. A cell is 0.59 to 0.93 across depending on the
            /// arena, so this is a small fraction of one whatever the board size.</summary>
            public static float SizeLow = 0.045f;

            public static float SizeHigh = 0.100f;

            /// <summary>Colour, warm and dim - the same fire, seen cold. It is not white: white
            /// ash over a turquoise arena reads as snow.</summary>
            public static Color Tint = new Color(1f, 0.66f, 0.42f);

            /// <summary>Peak opacity. The single most useful dial here, and it has been wrong in
            /// both directions: at 0.42 with smaller motes the drift was invisible against the
            /// arena, which is not restraint but absence. This is the level where it reads as
            /// heat in the air and still does not ask to be looked at.</summary>
            public static float Alpha = 0.72f;

            /// <summary>Fractions of a mote's own travel spent fading in at the mouth and out at
            /// the end. In is short - it is coming off a corner that is already burning, not
            /// arriving from somewhere - and out is long, because that is the half nobody should
            /// be able to point at.</summary>
            public static float FadeIn = 0.10f;

            public static float FadeOut = 0.45f;
        }

        // =================================================================== internals

        /// <summary>Over the board's own cells (1-3) so the motes pass in FRONT of the blocks,
        /// and under the destruction flash (10) and everything the player reads.</summary>
        private const int SortingOrder = 7;

        private struct Mote
        {
            public SpriteRenderer Renderer;

            /// <summary>Where it was born, and the unit heading it runs on. Position is derived
            /// from these and Gone rather than accumulated, so a mote cannot drift off its own
            /// line over a long overtime.</summary>
            public Vector2 Origin;

            public Vector2 Heading;

            /// <summary>How far along the heading it has come, and how far it gets in all.</summary>
            public float Gone;

            public float Travel;

            public float Speed;
            public float Size;
            public float Phase;
        }

        private Mote[] motes = new Mote[0];
        private int live;
        private Rect area;
        private float heat;
        private float clock;
        private System.Random rng = new System.Random(514229);
        private static Sprite moteSprite;

        /// <summary>Called by FlameStreakView whenever the fire is rebuilt. `heat` is 0..1; zero
        /// heat or an empty area stops the drift.</summary>
        public void SetState(Rect boardArea, float heatLevel)
        {
            area = boardArea;
            heat = Mathf.Clamp01(heatLevel);
            int want = heat <= 0f || area.width <= 0f || area.height <= 0f
                ? 0
                : Mathf.RoundToInt(Mathf.Lerp(Style.CountLow, Style.CountHigh, heat));

            EnsureCapacity(want);
            for (int i = live; i < want; i++)
            {
                // Scattered along their own travel on the first frame, not queued at the mouth:
                // corners that have just caught should already have embers out over the board.
                Respawn(i);
                motes[i].Gone = Range(0f, motes[i].Travel);
            }
            for (int i = want; i < motes.Length; i++)
            {
                if (motes[i].Renderer != null)
                {
                    motes[i].Renderer.enabled = false;
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
            clock += dt;
            for (int i = 0; i < live; i++)
            {
                Mote m = motes[i];
                m.Gone += m.Speed * dt;
                if (m.Gone >= m.Travel)
                {
                    motes[i] = m;
                    Respawn(i);
                    continue;
                }

                // How far through its OWN travel it is, 0 at the mouth and 1 where it goes out.
                float t = m.Gone / Mathf.Max(0.0001f, m.Travel);
                float fade = Mathf.Min(
                    t < Style.FadeIn ? t / Style.FadeIn : 1f,
                    t > 1f - Style.FadeOut ? (1f - t) / Style.FadeOut : 1f);

                Color c = Style.Tint;
                c.a = Mathf.Clamp01(fade) * Style.Alpha;
                m.Renderer.color = c;

                // Along the heading, plus a wander ACROSS it - the perpendicular is (-y, x).
                float sway = Mathf.Sin(clock * Style.DriftRate + m.Phase) * Style.Drift;
                Vector2 at = m.Origin + m.Heading * m.Gone
                    + new Vector2(-m.Heading.y, m.Heading.x) * sway;
                m.Renderer.transform.localPosition = at;
                m.Renderer.transform.localScale = new Vector3(m.Size, m.Size, 1f);
                motes[i] = m;
            }
        }

        /// <summary>Puts mote `i` back in one of the two corner mouths, on a new heading.</summary>
        private void Respawn(int i)
        {
            Mote m = motes[i];
            bool right = Range(0f, 1f) < 0.5f;

            // The VISIBLE corner, not the grid corner: WorldRect reports the cell area while the
            // plate the player sees is BorderOverhang wider, so the rim is half of that further
            // out - the same correction the corner flames make.
            float lip = BoardView.BorderOverhang * 0.5f;
            float top = area.yMax + lip;
            float side = right ? area.xMax + lip : area.xMin - lip;
            float inward = right ? -1f : 1f;

            m.Origin = new Vector2(
                side + inward * area.width * Style.MouthWidth * Range(0f, 1f),
                top - area.height * Style.MouthHeight * Range(0f, 1f));

            // Down and inward. Built as an angle so the spread is angular: scattering the
            // components instead bunches the motes along one axis.
            float angle = (Style.Angle + Range(-Style.Spread, Style.Spread)) * Mathf.Deg2Rad;
            m.Heading = new Vector2(Mathf.Cos(angle) * inward, -Mathf.Sin(angle));

            m.Gone = 0f;
            m.Travel = Mathf.Min(area.width, area.height)
                * Range(Style.TravelLow, Style.TravelHigh);
            m.Speed = Mathf.Lerp(Style.SpeedLow, Style.SpeedHigh, heat) * Range(0.7f, 1.35f);
            m.Size = Mathf.Lerp(Style.SizeLow, Style.SizeHigh, heat) * Range(0.7f, 1.4f);
            m.Phase = Range(0f, 6.283f);
            m.Renderer.enabled = true;
            motes[i] = m;
        }

        private void EnsureCapacity(int want)
        {
            if (motes.Length >= want)
            {
                return;
            }
            var grown = new Mote[want];
            System.Array.Copy(motes, grown, motes.Length);
            for (int i = motes.Length; i < want; i++)
            {
                var go = new GameObject("Drift" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = MoteSprite;
                sr.sortingOrder = SortingOrder;
                grown[i] = new Mote { Renderer = sr };
            }
            motes = grown;
        }

        /// <summary>The mote: a biweight falloff, (1-d^2)^2, which reaches zero with a zero slope
        /// so there is no rim on it. Shared, generated once.</summary>
        private static Sprite MoteSprite
        {
            get
            {
                if (moteSprite != null)
                {
                    return moteSprite;
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
                moteSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n),
                    new Vector2(0.5f, 0.5f), n);
                return moteSprite;
            }
        }

        private float Range(float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }
    }
}
