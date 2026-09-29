// PURPOSE: "Eforsuz galibiyet" being paid - confetti falling across the whole screen as the
// player walks into the shop. The one celebration in the game that is not about the board, and it
// could not be a board effect even if we wanted it to be: this lands in the MARKET, where the
// arena is not on screen at all.
//
// (2026-09-29, designer's call) IT IS NOW FIRED, NOT RAINED: cannons along the bottom and up both
// sides throw it flying in, the air brakes it and it flutters down. The older note follows.
// IT WAS A RAIN, NOT A BURST. A burst from a point is an explosion, and this joker's feat is not an
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

        private const int BasePieces = 90;

        /// <summary>How many more pieces a power-free OVERTIME is worth. Not a different effect -
        /// the same one, with more in it.</summary>
        private const int OvertimePieces = 150;

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
            public float Gravity;
        }

        private readonly List<Piece> live = new List<Piece>();
        private readonly List<Piece> pool = new List<Piece>();

        private Camera cam;

        public void Build(Camera camera)
        {
            cam = camera;
        }

        /// <summary>
        /// FIRES confetti over the whole camera from CANNONS along the bottom and up both sides -
        /// the pieces go flying up and inward, lose their speed to the air, then flutter back
        /// down. <paramref name="heavy"/> is the overtime payout: well over twice the pieces, the
        /// cannons fire a second volley, and they throw harder.
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
            int volleys = heavy ? 2 : 1;
            for (int v = 0; v < volleys; v++)
            {
                for (int i = 0; i < count / volleys; i++)
                {
                    Piece piece = Take();
                    float size = Mathf.Lerp(SizeMin, SizeMax, R(rng)) * halfHeight;
                    // A STRIP, not a square: a square tumbling reads as a brick.
                    piece.Width = size;
                    piece.Height = size * Mathf.Lerp(1.6f, 2.8f, R(rng));
                    Color colour = Palette[rng.Next(Palette.Length)];
                    colour.a = 0f;
                    piece.Renderer.color = colour;
                    piece.Renderer.transform.localScale = new Vector3(piece.Width, piece.Height, 1f);
                    // WHERE it is fired from: the two sides, or the bottom - the widest edge, so
                    // it gets the most.
                    float roll = R(rng);
                    int source = roll < 0.18f ? 0 : roll < 0.36f ? 1 : 2;
                    float power = (heavy ? 1.15f : 1f) * Mathf.Lerp(0.75f, 1.2f, R(rng));
                    Vector2 from;
                    Vector2 dir;
                    if (source == 2)
                    {
                        float x = (R(rng) * 2f - 1f) * halfWidth * 0.95f;
                        from = new Vector2(x, -halfHeight * 1.05f);
                        // Up, leaning in toward the middle.
                        float lean = -x / halfWidth * 0.35f + (R(rng) * 2f - 1f) * 0.25f;
                        dir = new Vector2(lean, 1f).normalized;
                    }
                    else
                    {
                        float side = source == 0 ? -1f : 1f;
                        float y = Mathf.Lerp(-0.9f, 0.3f, R(rng)) * halfHeight;
                        from = new Vector2(side * halfWidth * 1.05f, y);
                        dir = new Vector2(-side, Mathf.Lerp(0.5f, 1.3f, R(rng))).normalized;
                    }
                    piece.Renderer.transform.position = new Vector3(middle.x + from.x, middle.y + from.y, 0f);
                    piece.Renderer.transform.localRotation = Quaternion.Euler(0f, 0f, R(rng) * 360f);
                    float speed = halfHeight * Mathf.Lerp(2.6f, 4.2f, R(rng)) * power;
                    if (source != 2)
                    {
                        speed *= 0.85f;
                    }
                    piece.Velocity = dir * speed;
                    piece.Spin = (R(rng) * 2f - 1f) * 540f;
                    piece.Life = Mathf.Lerp(FallSecondsMin, FallSecondsMax, R(rng)) * (heavy ? 1.2f : 1f) + 0.6f;
                    // A negative age is a wait: the volleys and the cannons stagger.
                    piece.Age = -(v * 0.45f + R(rng) * 0.18f);
                    piece.SwayPhase = R(rng) * 6.283f;
                    piece.SwayAmount = halfWidth * Mathf.Lerp(0.02f, 0.07f, R(rng));
                    piece.Gravity = halfHeight * 3.2f;
                    piece.Renderer.enabled = true;
                    live.Add(piece);
                }
            }
        }

        private static float R(System.Random rng)
        {
            return (float)rng.NextDouble();
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
                if (piece.Age < 0f)
                {
                    continue;
                }
                float t = piece.Age / piece.Life;
                Transform tr = piece.Renderer.transform;
                // FLIGHT: thrown hard, braked by the air, pulled down, and once it has lost its
                // throw it falls at paper's terminal speed with a sway.
                piece.Velocity *= Mathf.Exp(-1.6f * dt);
                piece.Velocity.y -= piece.Gravity * dt;
                float terminal = -piece.Gravity * 0.32f;
                if (piece.Velocity.y < terminal)
                {
                    piece.Velocity.y = Mathf.Lerp(piece.Velocity.y, terminal, 1f - Mathf.Exp(-6f * dt));
                }
                float slow = Mathf.Clamp01(1f - piece.Velocity.magnitude / (piece.Gravity * 0.8f));
                float sway = Mathf.Sin(piece.SwayPhase + piece.Age * 3.1f) * piece.SwayAmount * slow * 3f;
                tr.position = new Vector3(
                    tr.position.x + piece.Velocity.x * dt + sway * dt,
                    tr.position.y + piece.Velocity.y * dt,
                    0f);
                tr.localRotation = Quaternion.Euler(0f, 0f,
                    tr.localRotation.eulerAngles.z + piece.Spin * dt);
                float edge = Mathf.Abs(Mathf.Cos((piece.SwayPhase + piece.Age * 5.2f)));
                tr.localScale = new Vector3(piece.Width * Mathf.Lerp(0.25f, 1f, edge),
                    piece.Height, 1f);
                Color c = piece.Renderer.color;
                c.a = t > 0.85f ? 1f - (t - 0.85f) / 0.15f : 1f;
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
