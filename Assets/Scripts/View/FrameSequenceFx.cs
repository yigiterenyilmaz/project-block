// PURPOSE: Plays a hand-drawn frame sequence (a sprite sheet) as ONE layer of an effect - pooled,
// on the owner's clock, with a duration PER FRAME.
//
// WHY PER FRAME. A drawn burst is not evenly spaced: the frames around the peak have to hold longer
// than the ones building up to it, or the peak - the one frame the whole drawing exists for - is
// gone before it registers. A single fps cannot say that; a table of milliseconds can, and the
// table is the animator's own timing, kept next to the art that it belongs to.
//
// WHY IT DOES NOT DRIVE ITSELF. The owner ticks it with its OWN delta, so the animation lab's
// time scale slows a sheet together with everything the code draws around it - a sheet on the
// real clock beside a light on the scaled one is two animations that drift apart at 0.25x. The
// owner also reads FrameAt back, which is how a light, a number or a burst of particles is laid
// on a particular DRAWN frame instead of on a guess about when that frame shows.
//
// THE SHEET IS A REGULAR GRID, read left to right and top row first, cut once at first use, and
// every frame is centred on the same point - the pack step (Tools/ArtPrep) puts each drawing's
// bright core on the cell centre, so a burst grows in place rather than sliding while it grows.
// A missing sheet degrades to nothing drawn and one warning, never an exception per frame.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class FrameSequenceFx
    {
        /// <summary>One sheet, cut once and shared.</summary>
        public sealed class Sheet
        {
            public readonly string Path;
            public readonly int Columns;
            public readonly int Rows;

            /// <summary>Grid index of this sequence's first frame, so a PART of a drawing can be
            /// its own sequence with its own timing (a cancel plays only a burst's tail).</summary>
            public readonly int First;

            /// <summary>How long each frame is SHOWN, in milliseconds.</summary>
            public readonly float[] FrameMs;

            private Sprite[] frames;
            private bool looked;

            public Sheet(string path, int columns, int rows, params float[] frameMs)
                : this(path, columns, rows, 0, frameMs)
            {
            }

            public Sheet(string path, int columns, int rows, int first, params float[] frameMs)
            {
                Path = path;
                Columns = columns;
                Rows = rows;
                First = first;
                FrameMs = frameMs;
            }

            public int Count
            {
                get { return FrameMs.Length; }
            }

            /// <summary>The whole sequence, in seconds.</summary>
            public float Duration
            {
                get
                {
                    float total = 0f;
                    for (int i = 0; i < FrameMs.Length; i++)
                    {
                        total += FrameMs[i];
                    }
                    return total / 1000f;
                }
            }

            /// <summary>When frame <paramref name="index"/> first shows, in seconds.</summary>
            public float StartOf(int index)
            {
                float total = 0f;
                for (int i = 0; i < index && i < FrameMs.Length; i++)
                {
                    total += FrameMs[i];
                }
                return total / 1000f;
            }

            /// <summary>The frame showing at <paramref name="t"/> seconds, and how far through it
            /// (0..1). -1 before the start and Count once it has run out.</summary>
            public int FrameAt(float t, out float through)
            {
                through = 0f;
                if (t < 0f)
                {
                    return -1;
                }
                float ms = t * 1000f;
                for (int i = 0; i < FrameMs.Length; i++)
                {
                    if (ms < FrameMs[i])
                    {
                        through = ms / Mathf.Max(FrameMs[i], 0.0001f);
                        return i;
                    }
                    ms -= FrameMs[i];
                }
                return FrameMs.Length;
            }

            /// <summary>Null when the art is missing - the caller draws the rest without it.
            /// </summary>
            public Sprite Frame(int index)
            {
                if (!looked)
                {
                    looked = true;
                    var tex = Resources.Load<Texture2D>(Path);
                    if (tex == null)
                    {
                        Debug.LogWarning("[block_bonk] Frame sheet missing: Resources/" + Path);
                    }
                    else
                    {
                        int w = tex.width / Columns;
                        int h = tex.height / Rows;
                        frames = new Sprite[Count];
                        for (int i = 0; i < Count; i++)
                        {
                            int col = (i + First) % Columns;
                            int row = (i + First) / Columns;
                            // Texture rows count from the BOTTOM, the sheet reads from the top.
                            // One unit per frame edge, so a scale in cells means what it says.
                            frames[i] = Sprite.Create(tex,
                                new Rect(col * w, (Rows - 1 - row) * h, w, h),
                                new Vector2(0.5f, 0.5f), w);
                        }
                    }
                }
                if (frames == null || index < 0 || index >= frames.Length)
                {
                    return null;
                }
                return frames[index];
            }

            public bool Available
            {
                get { return Frame(0) != null; }
            }
        }

        /// <summary>One sequence being shown.</summary>
        public sealed class Play
        {
            public Sheet Sheet;
            public float Clock;
            public Vector2 Position;
            public float Size;
            public float Degrees;
            public bool Mirror;
            public Color Tint = Color.white;

            /// <summary>Extra multiplier on the drawn size, for a pinch or a swell the owner
            /// wants on top of the drawing.</summary>
            public float Scale = 1f;

            /// <summary>Frames at or past this are not shown (a cancel cuts a sheet short).
            /// </summary>
            public int StopAt = int.MaxValue;

            /// <summary>Seconds the last shown frame takes to fade once the sheet has run out or
            /// been cut. A drawn burst whose last frame simply vanishes reads as a sprite swap.
            /// </summary>
            public float FadeOut = 0.06f;

            internal SpriteRenderer Renderer;
            internal float EndedAt = -1f;

            public int Frame
            {
                get
                {
                    float ignored;
                    return Sheet.FrameAt(Clock, out ignored);
                }
            }

            public bool Done
            {
                get { return EndedAt >= 0f && Clock - EndedAt >= FadeOut; }
            }
        }

        private readonly Transform parent;
        private readonly int order;
        private readonly List<Play> live = new List<Play>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        /// <summary>Switch for the lab: the sheet layer alone off.</summary>
        public bool Visible = true;

        public FrameSequenceFx(Transform parent, int sortingOrder)
        {
            this.parent = parent;
            order = sortingOrder;
        }

        public IReadOnlyList<Play> Live
        {
            get { return live; }
        }

        public Play Start(Sheet sheet, Vector2 position, float size)
        {
            var play = new Play { Sheet = sheet, Position = position, Size = size };
            play.Renderer = Rent();
            live.Add(play);
            Draw(play);
            return play;
        }

        public void Tick(float dt)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Play play = live[i];
                play.Clock += dt;
                Draw(play);
                if (play.Done)
                {
                    Return(play.Renderer);
                    live.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            for (int i = 0; i < live.Count; i++)
            {
                Return(live[i].Renderer);
            }
            live.Clear();
        }

        private void Draw(Play play)
        {
            float through;
            int index = play.Sheet.FrameAt(play.Clock, out through);
            int last = Mathf.Min(play.Sheet.Count, play.StopAt) - 1;
            float alpha = 1f;
            if (index > last)
            {
                if (play.EndedAt < 0f)
                {
                    play.EndedAt = play.Clock;
                }
                index = last;
                alpha = 1f - Mathf.Clamp01((play.Clock - play.EndedAt) / Mathf.Max(play.FadeOut, 0.0001f));
            }
            SpriteRenderer r = play.Renderer;
            Sprite sprite = index >= 0 ? play.Sheet.Frame(index) : null;
            if (!Visible || sprite == null || alpha <= 0f)
            {
                r.enabled = false;
                return;
            }
            r.enabled = true;
            r.sprite = sprite;
            r.flipX = play.Mirror;
            Color c = play.Tint;
            c.a *= alpha;
            r.color = c;
            Transform t = r.transform;
            t.position = new Vector3(play.Position.x, play.Position.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, play.Degrees);
            float s = play.Size * play.Scale;
            t.localScale = new Vector3(s, s, 1f);
        }

        private SpriteRenderer Rent()
        {
            if (pool.Count > 0)
            {
                SpriteRenderer r = pool.Pop();
                r.gameObject.SetActive(true);
                return r;
            }
            var go = new GameObject("FrameSequence");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.gameObject.SetActive(false);
            pool.Push(r);
        }
    }
}
