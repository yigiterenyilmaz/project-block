// PURPOSE: "Antimadde" - the key landing and ANNIHILATING every cube of one element. It used to be
// an ordinary group destruction with the plain explode sound, which is the smallest thing in the
// game drawing the hardest thing in the game to land (a perfect overlay, on a shape you did not
// choose, before it rots). This is its impact.
//
// MATTER MEETS ITS OPPOSITE, so it goes INWARD before it goes out:
//   CHARGE      a dark violet core pinches in on every doomed cube and a ring closes in on the
//               group's centre from outside; thin links run from that centre to each cube -
//               everything that is about to go is visibly tied together.
//   ANNIHILATE  one frame of pale lavender flash at every cube at once (never white), a sharp
//               shockwave per cube, one big ring across the arena from the centre, and a heavy
//               knock of the ARENA that rings out (never the camera, never Time.timeScale).
//   AFTERMATH   a NEGATIVE afterimage of each cell - the inverse of the colour that stood there -
//               fading out, and antimatter motes spiralling back INTO the centre and gone.
// It plays OVER the ordinary cluster burst, which still breaks the cubes themselves.
//
// THE VIEW DECIDES NOTHING: the cells are the report's destroyed cubes of AnnihilatedKind.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class AntimatterBlastView : MonoBehaviour
    {
        public static class Style
        {
            public static float Charge = 0.2f;
            public static float Total = 1.15f;
            public static float KnockDistance = 0.16f;
            public static float KnockSeconds = 0.45f;
            public static int MotesPerCube = 5;
        }

        private static readonly Color Void = new Color(0.16f, 0.05f, 0.28f);
        private static readonly Color Violet = new Color(0.62f, 0.38f, 1f);
        private static readonly Color Lavender = new Color(0.9f, 0.82f, 1f);

        private const int Order = 19;

        private sealed class Blast
        {
            public float Clock;
            public Vector2 Centre;
            public readonly List<Vector2> Cells = new List<Vector2>();
            public readonly List<Color> Faces = new List<Color>();
            public readonly List<SpriteRenderer> Cores = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Links = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Flashes = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Waves = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Ghosts = new List<SpriteRenderer>();
            public readonly List<SpriteRenderer> Motes = new List<SpriteRenderer>();
            public readonly List<Vector2> MoteFrom = new List<Vector2>();
            public SpriteRenderer Closing;
            public SpriteRenderer BigRing;
            public SpriteRenderer CentreCore;
            public bool Peaked;
            public float Reach;
        }

        private readonly List<Blast> blasts = new List<Blast>();
        private BoardView board;
        private float knockClock = -1f;
        private Vector2 knockDir = Vector2.down;

        /// <summary>Fires on the annihilation frame - the controller plays the boom there.</summary>
        public System.Action Peaked;

        private static Sprite dot;
        private static Sprite ring;

        public void Build(BoardView view)
        {
            board = view;
        }

        /// <summary>Plays the annihilation over <paramref name="cells"/>; <paramref name="faces"/>
        /// is the colour each cube wore (for the negative afterimage), same order.</summary>
        public void Play(IList<GridPos> cells, IList<Color> faces)
        {
            if (board == null || cells == null || cells.Count == 0)
            {
                return;
            }
            float cell = board.CellWorldSize;
            var b = new Blast();
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                Vector2 at = board.CellToWorld(cells[i]);
                b.Cells.Add(at);
                b.Faces.Add(faces != null && i < faces.Count ? faces[i] : Violet);
                sum += at;
            }
            b.Centre = sum / cells.Count;
            float far = 0f;
            foreach (Vector2 at in b.Cells)
            {
                far = Mathf.Max(far, (at - b.Centre).magnitude);
            }
            b.Reach = far + cell * 3f;
            for (int i = 0; i < b.Cells.Count; i++)
            {
                b.Cores.Add(Make(Dot, Order));
                b.Links.Add(Make(ViewUtil.WhiteSprite, Order - 1));
                b.Flashes.Add(Make(Dot, Order + 2));
                b.Waves.Add(Make(Ring, Order + 1));
                b.Ghosts.Add(Make(ViewUtil.RoundedSprite, Order - 2));
                for (int m = 0; m < Style.MotesPerCube; m++)
                {
                    float a = (i * 1.7f + m * 2.39996f);
                    b.Motes.Add(Make(Dot, Order + 1));
                    b.MoteFrom.Add(b.Cells[i] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * (0.8f + 0.25f * m));
                }
            }
            b.Closing = Make(Ring, Order);
            b.BigRing = Make(Ring, Order + 1);
            b.CentreCore = Make(Dot, Order + 1);
            blasts.Add(b);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = blasts.Count - 1; i >= 0; i--)
            {
                Blast b = blasts[i];
                b.Clock += dt;
                if (b.Clock >= Style.Total)
                {
                    Dispose(b);
                    blasts.RemoveAt(i);
                    continue;
                }
                Tick(b);
            }
            TickKnock(dt);
        }

        private void Tick(Blast b)
        {
            float cell = board.CellWorldSize;
            float t = b.Clock;
            float charge = Mathf.Clamp01(t / Style.Charge);
            float after = Mathf.Clamp01((t - Style.Charge) / (Style.Total - Style.Charge));
            bool pre = t < Style.Charge;
            if (!pre && !b.Peaked)
            {
                b.Peaked = true;
                knockClock = 0f;
                if (Peaked != null)
                {
                    Peaked();
                }
            }

            // The ring closing in on the centre, and the centre's own core gathering.
            Place(b.Closing, b.Centre, Mathf.Lerp(b.Reach * 2f, cell * 0.6f, charge * charge));
            b.Closing.color = Tint(Violet, pre ? 0.55f * charge : 0f);
            float coreSize = pre ? cell * Mathf.Lerp(0.3f, 0.9f, charge) : cell * Mathf.Lerp(1.6f, 0.2f, Mathf.Clamp01(after * 4f));
            Place(b.CentreCore, b.Centre, coreSize);
            b.CentreCore.color = pre ? Tint(Void, 0.85f * charge) : Tint(Lavender, 0.8f * (1f - Mathf.Clamp01(after * 4f)));

            // The big ring out across the arena.
            float out1 = 1f - (1f - after) * (1f - after);
            Place(b.BigRing, b.Centre, Mathf.Lerp(cell, b.Reach * 2.6f, Mathf.Clamp01(out1 * 1.6f)));
            b.BigRing.color = Tint(Violet, pre ? 0f : 0.7f * (1f - Mathf.Clamp01(after * 1.8f)));

            for (int i = 0; i < b.Cells.Count; i++)
            {
                Vector2 at = b.Cells[i];
                // CHARGE: a void pinching in on the cube; a link to the centre.
                Place(b.Cores[i], at, cell * Mathf.Lerp(1.1f, 0.35f, charge));
                b.Cores[i].color = Tint(Void, pre ? 0.75f * charge : 0f);
                Vector2 d = b.Centre - at;
                float len = d.magnitude;
                b.Links[i].transform.localPosition = (at + b.Centre) * 0.5f;
                b.Links[i].transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                SetScale(b.Links[i], len, cell * 0.035f * (1f + 0.5f * Mathf.Sin(t * 70f + i)));
                b.Links[i].color = Tint(Violet, len > 0.01f && pre ? 0.6f * charge : 0f);
                // ANNIHILATE: flash and a shockwave per cube.
                float fk = Mathf.Clamp01(after * 6f);
                Place(b.Flashes[i], at, cell * Mathf.Lerp(1.3f, 2.1f, fk));
                b.Flashes[i].color = Tint(Lavender, pre ? 0f : 0.95f * (1f - fk));
                float wk = Mathf.Clamp01(after * 3f);
                Place(b.Waves[i], at, cell * Mathf.Lerp(0.8f, 3.2f, 1f - (1f - wk) * (1f - wk)));
                b.Waves[i].color = Tint(Violet, pre ? 0f : 0.75f * (1f - wk));
                // AFTERMATH: the negative of what stood there.
                Color face = b.Faces[i];
                var neg = new Color(1f - face.r, 1f - face.g, 1f - face.b);
                neg = Color.Lerp(neg, Violet, 0.35f);
                float gk = Mathf.Clamp01((after - 0.05f) / 0.5f);
                Place(b.Ghosts[i], at, cell * Mathf.Lerp(0.95f, 0.7f, gk));
                b.Ghosts[i].color = Tint(neg, pre || gk <= 0f ? 0f : 0.5f * (1f - gk));
                for (int m = 0; m < Style.MotesPerCube; m++)
                {
                    int k = i * Style.MotesPerCube + m;
                    float mk = Mathf.Clamp01((after - 0.1f - m * 0.03f) / 0.45f);
                    float e = mk * mk;
                    Vector2 from = b.MoteFrom[k];
                    Vector2 rel = from - b.Centre;
                    float turn = e * 2.2f;
                    float cs = Mathf.Cos(turn), sn = Mathf.Sin(turn);
                    Vector2 p = b.Centre + new Vector2(rel.x * cs - rel.y * sn, rel.x * sn + rel.y * cs) * (1f - e);
                    Place(b.Motes[k], p, cell * 0.12f * (1f - e * 0.6f));
                    b.Motes[k].color = Tint(m % 2 == 0 ? Violet : Lavender, mk > 0f && mk < 1f ? 0.9f * Mathf.Sin(mk * Mathf.PI) : 0f);
                }
            }
        }

        private void TickKnock(float dt)
        {
            if (knockClock < 0f || board == null)
            {
                return;
            }
            knockClock += dt;
            float u = knockClock / Style.KnockSeconds;
            if (u >= 1f)
            {
                board.SetImpulse(Vector2.zero);
                knockClock = -1f;
                return;
            }
            // One hard drop, then it rings out.
            float shape = Mathf.Sin(u * Mathf.PI * 4f) * Mathf.Pow(1f - u, 2.2f);
            board.SetImpulse(knockDir * Style.KnockDistance * shape
                + new Vector2(Style.KnockDistance * 0.35f * Mathf.Sin(u * Mathf.PI * 7f) * (1f - u) * (1f - u), 0f));
        }

        private SpriteRenderer Make(Sprite sprite, int order)
        {
            var go = new GameObject("Antimatter");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, 0f);
            return r;
        }

        private static Color Tint(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static void Place(SpriteRenderer r, Vector2 at, float size)
        {
            r.transform.localPosition = at;
            SetScale(r, size, size);
        }

        private static void SetScale(SpriteRenderer r, float w, float h)
        {
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f), h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        private void Dispose(Blast b)
        {
            void Kill(List<SpriteRenderer> list)
            {
                foreach (SpriteRenderer r in list)
                {
                    if (r != null) Destroy(r.gameObject);
                }
            }
            Kill(b.Cores); Kill(b.Links); Kill(b.Flashes); Kill(b.Waves); Kill(b.Ghosts); Kill(b.Motes);
            if (b.Closing != null) Destroy(b.Closing.gameObject);
            if (b.BigRing != null) Destroy(b.BigRing.gameObject);
            if (b.CentreCore != null) Destroy(b.CentreCore.gameObject);
        }

        private static Sprite Dot
        {
            get { return dot != null ? dot : dot = Bake(64, d => { float k = 1f - Mathf.Clamp01(d); return k * k; }); }
        }

        private static Sprite Ring
        {
            get
            {
                return ring != null ? ring : ring = Bake(128, d =>
                {
                    float k = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.88f) / 0.1f);
                    return k * k;
                });
            }
        }

        private static Sprite Bake(int px, System.Func<float, float> alpha)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            float half = px * 0.5f;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y + 0.5f - half) / half;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(Mathf.Sqrt(dx * dx + dy * dy))));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
        }

        /// <summary>Takes everything down at once - a reset, a rebuilt board.</summary>
        public void Clear()
        {
            foreach (Blast b in blasts)
            {
                Dispose(b);
            }
            blasts.Clear();
            if (knockClock >= 0f && board != null)
            {
                board.SetImpulse(Vector2.zero);
            }
            knockClock = -1f;
        }
    }
}
