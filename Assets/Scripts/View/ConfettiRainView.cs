// PURPOSE: "Eforsuz galibiyet" being paid - confetti falling across the whole screen as the
// player walks into the shop. The one celebration in the game that is not about the board, and it
// could not be a board effect even if we wanted it to be: this lands in the MARKET, where the
// arena is not on screen at all.
//
// IT IS A RAIN, NOT A BURST. A burst from a point is an explosion, and this joker's feat is not an
// impact - it is a round finished a particular way, and the reward arrives as you leave. So the
// pieces fall from above the camera, drift, tumble and pass out of the bottom, and there is no
// centre to it anywhere.
//
// THE OVERTIME VERSION IS HEAVIER, not faster and not a different colour: the same celebration for
// more of the same feat, because that is exactly what the doubled bonus is. Roughly twice the
// pieces, falling a little longer, with a wider spread - "more of it" reads as "worth more" without
// the player having to be told a multiplier happened.
//
// Pooled, because the alternative is a few hundred GameObjects created and destroyed inside a
// second. Everything is deterministic off a seed, so two runs of the same payout look the same.
//
// EXTENSION POINT: anything else that wants a screen-wide celebration (a run won, a boss beaten on
// its own terms) can call Play with its own palette - nothing here is about this joker except the
// default colours.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Screen-wide falling confetti. One instance, reused; Play tops it up rather than
    /// clearing, so two payouts in quick succession overlap instead of cutting each other off.</summary>
    public sealed class ConfettiRainView : MonoBehaviour
    {
        /// <summary>
        /// THE PALETTE - warm golds and a few cool accents, never a full rainbow. A rainbow reads
        /// as a generic "you win" sticker; a controlled set reads as this game's own celebration,
        /// and these are the HUD's own gold plus the two accents the bar cards already use.
        /// </summary>
        private static readonly Color[] Palette =
        {
            new Color(1f, 0.84f, 0.42f),
            new Color(1f, 0.72f, 0.28f),
            new Color(0.98f, 0.93f, 0.78f),
            new Color(0.72f, 0.95f, 1f),
            new Color(0.85f, 0.70f, 1f)
        };

        private const int BasePieces = 55;

        /// <summary>How many more pieces a power-free OVERTIME is worth. Not a different effect -
        /// the same one, with more in it.</summary>
        private const int OvertimePieces = 65;

        private const float FallSecondsMin = 1.5f;

        private const float FallSecondsMax = 2.6f;

        /// <summary>A piece's size as a share of the camera's half-height, so the confetti is the
        /// same size on every screen rather than the same number of world units.</summary>
        private const float SizeMin = 0.022f;

        private const float SizeMax = 0.046f;

        private sealed class Piece
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Spin;
            public float Life;
            public float Age;
            public float SwayPhase;
            public float SwayAmount;
            public float Width;
            public float Height;
        }

        private readonly List<Piece> live = new List<Piece>();
        private readonly List<Piece> pool = new List<Piece>();

        private Camera cam;

        public void Build(Camera camera)
        {
            cam = camera;
        }

        /// <summary>
        /// Rains confetti over the whole camera. <paramref name="heavy"/> is the overtime payout:
        /// more pieces, falling longer, spread wider.
        /// </summary>
        public void Play(bool heavy, int seed)
        {
            if (cam == null)
            {
                cam = Camera.main;
            }
            if (cam == null || !cam.orthographic)
            {
                return;
            }
            var rng = new System.Random(seed);
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            Vector3 middle = cam.transform.position;
            int count = heavy ? BasePieces + OvertimePieces : BasePieces;
            for (int i = 0; i < count; i++)
            {
                Piece piece = Take();
                float size = Mathf.Lerp(SizeMin, SizeMax, (float)rng.NextDouble()) * halfHeight;
                // A confetto is a STRIP, not a square: a square tumbling reads as a brick, and the
                // width changing as it spins is most of what makes paper look like paper.
                piece.Width = size;
                piece.Height = size * Mathf.Lerp(1.6f, 2.8f, (float)rng.NextDouble());
                Color colour = Palette[rng.Next(Palette.Length)];
                piece.Renderer.color = colour;
                piece.Renderer.transform.localScale = new Vector3(piece.Width, piece.Height, 1f);
                // Spread a little PAST both edges: pieces that all start inside the frame leave a
                // visible clean margin down each side.
                float spread = heavy ? 1.25f : 1.1f;
                float x = middle.x + ((float)rng.NextDouble() * 2f - 1f) * halfWidth * spread;
                // Staggered above the top edge, so they arrive over about half a second rather
                // than as one curtain.
                float y = middle.y + halfHeight * (1.05f + (float)rng.NextDouble() * 0.9f);
                piece.Renderer.transform.position = new Vector3(x, y, 0f);
                piece.Renderer.transform.localRotation =
                    Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f);
                float fall = Mathf.Lerp(FallSecondsMin, FallSecondsMax, (float)rng.NextDouble());
                piece.Velocity = new Vector2(
                    ((float)rng.NextDouble() * 2f - 1f) * halfWidth * 0.10f,
                    -halfHeight * 2.4f / fall);
                piece.Spin = ((float)rng.NextDouble() * 2f - 1f) * 320f;
                piece.Life = fall * (heavy ? 1.15f : 1f);
                piece.Age = 0f;
                piece.SwayPhase = (float)rng.NextDouble() * 6.283f;
                piece.SwayAmount = halfWidth * Mathf.Lerp(0.02f, 0.07f, (float)rng.NextDouble());
                piece.Renderer.enabled = true;
                live.Add(piece);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Piece piece = live[i];
                piece.Age += dt;
                if (piece.Age >= piece.Life || piece.Renderer == null)
                {
                    Retire(i);
                    continue;
                }
                float t = piece.Age / piece.Life;
                Transform tr = piece.Renderer.transform;
                // SWAY is written into the POSITION each frame from the piece's own phase, not
                // accumulated onto it - an offset added to itself every frame walks away.
                float sway = Mathf.Sin(piece.SwayPhase + piece.Age * 3.1f) * piece.SwayAmount;
                tr.position = new Vector3(
                    tr.position.x + (piece.Velocity.x * dt) + sway * dt,
                    tr.position.y + piece.Velocity.y * dt,
                    0f);
                tr.localRotation = Quaternion.Euler(0f, 0f,
                    tr.localRotation.eulerAngles.z + piece.Spin * dt);
                // A confetto TURNS EDGE-ON as it tumbles: the x scale follows its own spin, which
                // is what stops a falling rectangle reading as a falling sticker.
                float edge = Mathf.Abs(Mathf.Cos((piece.SwayPhase + piece.Age * 5.2f)));
                tr.localScale = new Vector3(piece.Width * Mathf.Lerp(0.25f, 1f, edge),
                    piece.Height, 1f);
                // Fades only at the very end, so the rain does not look like it is evaporating
                // halfway down the screen.
                Color c = piece.Renderer.color;
                c.a = t > 0.82f ? 1f - (t - 0.82f) / 0.18f : 1f;
                piece.Renderer.color = c;
            }
        }

        private Piece Take()
        {
            if (pool.Count > 0)
            {
                Piece reused = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
                return reused;
            }
            var go = new GameObject("Confetto");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = ViewUtil.WhiteSprite;
            // Well above the market's own chrome: this falls in FRONT of the shop.
            renderer.sortingOrder = 120;
            return new Piece { Renderer = renderer };
        }

        private void Retire(int index)
        {
            Piece piece = live[index];
            live.RemoveAt(index);
            if (piece.Renderer != null)
            {
                piece.Renderer.enabled = false;
                pool.Add(piece);
            }
        }

        /// <summary>Takes every piece off screen at once - leaving the market, a reset.</summary>
        public void Clear()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Retire(i);
            }
        }
    }
}
