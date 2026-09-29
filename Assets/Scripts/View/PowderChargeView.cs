// PURPOSE: "Barut tedarikçisi" - a dynamite block VISIBLY filling with powder, and paying it out.
// The joker's whole proposition is "leave this standing and it gets worth more", so the block on
// the board has to say so where the player is actually looking.
//
// THREE THINGS, and they are different on purpose:
//
//   THE POUR    the EVENT of a charge: grains of powder are drawn in from around the cell on a
//               spiral, land in the cube one after another, and the cube takes them with a small
//               GULP (a punch of the heat under its face). More grains the fuller it gets, so a
//               late charge visibly loads more than an early one. No flat flare square.
//   THE HEAT    the STATE: a soft glow under the cube's face and a live FUSE SPARK on its corner
//               that sheds tiny sparks upward. Both ride FULLNESS - a barely-charged block is a dim
//               slow coal with a lazy spark, a nearly-full one is hot, fast and spitting. At the
//               CAP it is its own state: a tight heat rim round the cell beats fast, saying "take
//               this now". Never a counter, never a bar.
//   THE PAYOUT  when a charged block goes up, a pop at each cube as it breaks - and for a FULL
//               block the BONUS BLAST: a hot core flash, a shockwave ring, sparks thrown out with
//               drag, glowing chunks, a second afterburn pop, one knock of the ARENA (never the
//               camera) and the points printed in the powder's own orange.
//
// Colours are a warm coal heating to hot orange - never white, never a warning red.
//
// THE VIEW DECIDES NOTHING: PowderVisuals says which cells charged and how full; PowderPayoutVisuals
// says which went up, which were full and what was paid. The heat is rebuilt from the live report
// each turn, so a block that explodes simply stops being listed.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Draws the powder on charged dynamite and its payout. Owned by GameUiController,
    /// which hands it the reports; it holds no state Core does not re-state.</summary>
    public sealed class PowderChargeView : MonoBehaviour
    {
        private static readonly Color CoolEmber = new Color(0.72f, 0.38f, 0.14f);

        private static readonly Color HotEmber = new Color(1f, 0.70f, 0.26f);

        /// <summary>The blast's hottest note - a pale amber, still never white.</summary>
        private static readonly Color BlastCore = new Color(1f, 0.86f, 0.52f);

        private static readonly Color Soot = new Color(0.22f, 0.12f, 0.08f);

        private const int HeatOrder = 8;
        private const int RimOrder = 8;
        private const int FuseOrder = 9;
        private const int GrainOrder = 10;
        private const int BlastOrder = 18;

        private const float SlowPulse = 1.9f;
        private const float FastPulse = 0.55f;
        private const float FullPulse = 0.30f;

        /// <summary>The pour: grains, their stagger and how long one takes to fall in.</summary>
        private const int GrainsMin = 4;
        private const int GrainsPerCharge = 2;
        private const float GrainStagger = 0.045f;
        private const float GrainFlight = 0.34f;

        /// <summary>The gulp when a grain lands: how far the heat punches, and how long it lasts.</summary>
        private const float GulpPunch = 0.22f;
        private const float GulpSeconds = 0.22f;

        /// <summary>The full block's bonus blast.</summary>
        private const float BlastSeconds = 0.75f;
        private const float RingReach = 2.6f;
        private const int BlastSparks = 14;
        private const int BlastChunks = 7;
        private const float AfterburnDelay = 0.13f;
        private const float KnockDistance = 0.06f;

        private sealed class Heat
        {
            public GridPos Cell;
            public SpriteRenderer Glow;
            public SpriteRenderer Fuse;
            public SpriteRenderer Rim;
            public float Fullness;
            public bool Full;
            public float Gulp = -1f;
            public float SparkWait;
            public float Seed;
        }

        /// <summary>One loose piece: a grain pouring in, a fuse spit, a blast spark, a chunk, a
        /// flash, a ring. Everything that moves and dies is one of these.</summary>
        private sealed class Bit
        {
            public SpriteRenderer R;
            public float Age;
            public float Delay;
            public float Life;
            public Vector2 From;
            public Vector2 To;
            public Vector2 Velocity;
            public float Drag;
            public float Gravity;
            public float Size;
            public float EndSize;
            public float Stretch;
            public float Spin;
            public Color Colour;
            public Color EndColour;
            public float Alpha;
            public int Kind;
            public GridPos? TargetCell;
        }

        private const int KindGrain = 0;
        private const int KindSpark = 1;
        private const int KindFlash = 2;
        private const int KindRing = 3;

        private readonly List<Heat> heats = new List<Heat>();
        private readonly List<Bit> bits = new List<Bit>();
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private BoardView board;
        private float knockAt = -1f;
        private Vector2 knockDir;

        private static Sprite dot;
        private static Sprite ring;

        /// <summary>Fired when a full block's bonus blast peaks - the controller plays the boom.</summary>
        public System.Action<bool> BlastPeaked;

        public void Build(BoardView boardView)
        {
            board = boardView;
        }

        // ------------------------------------------------------------------ charge + heat

        /// <summary>
        /// Restates the heat from the turn's report and pours powder into every cell that just
        /// charged. Called on the repaint; the pour only fires for a NEW report (the caller
        /// matches by identity), so pass <paramref name="pour"/> false for a plain restate.
        /// </summary>
        public void Show(PowderVisuals report, bool pour = true)
        {
            ClearHeat();
            if (report == null || board == null)
            {
                return;
            }
            float cell = board.CellWorldSize;
            for (int i = 0; i < report.Count; i++)
            {
                Vector2 at = board.CellToWorld(report.Cells[i]);
                var h = new Heat
                {
                    Cell = report.Cells[i],
                    Fullness = Mathf.Clamp01(report.Fullness[i]),
                    Full = report.Full[i],
                    Seed = Hash(report.Cells[i], 1),
                };
                h.Glow = Rent(Dot, HeatOrder, at, cell * 0.9f);
                h.Fuse = Rent(Dot, FuseOrder, at + FuseTip(cell), cell * 0.16f);
                if (h.Full)
                {
                    h.Rim = Rent(ViewUtil.GlowSprite, RimOrder, at, cell * 1.18f);
                }
                h.SparkWait = h.Seed * 0.4f;
                heats.Add(h);
                if (pour && report.Gained[i])
                {
                    Pour(h, at, cell, report.Charges[i]);
                }
            }
        }

        /// <summary>The fuse sits on the cube's upper-right corner, just inside its face.</summary>
        private static Vector2 FuseTip(float cell)
        {
            return new Vector2(cell * 0.3f, cell * 0.3f);
        }

        /// <summary>Grains drawn in from a ring round the cell on a curl, each landing with a gulp.</summary>
        private void Pour(Heat h, Vector2 at, float cell, int charges)
        {
            int count = GrainsMin + GrainsPerCharge * Mathf.Max(0, charges - 1);
            for (int g = 0; g < count; g++)
            {
                float a = (h.Seed + g / (float)count) * Mathf.PI * 2f + Hash(h.Cell, 10 + g) * 0.6f;
                float reach = cell * Mathf.Lerp(0.75f, 1.05f, Hash(h.Cell, 40 + g));
                var b = NewBit(KindGrain, Dot, GrainOrder);
                b.From = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
                b.To = at + new Vector2(Hash(h.Cell, 70 + g) - 0.5f, Hash(h.Cell, 90 + g) - 0.5f)
                    * cell * 0.2f;
                b.Delay = g * GrainStagger;
                b.Life = GrainFlight * Mathf.Lerp(0.85f, 1.15f, Hash(h.Cell, 20 + g));
                b.Size = cell * Mathf.Lerp(0.07f, 0.11f, Hash(h.Cell, 30 + g));
                b.EndSize = b.Size * 0.45f;
                b.Spin = (g % 2 == 0 ? 1f : -1f) * 1.1f;
                b.Colour = Color.Lerp(Soot, CoolEmber, 0.55f);
                b.EndColour = Color.Lerp(CoolEmber, HotEmber, h.Fullness);
                b.Alpha = 0.95f;
                b.TargetCell = h.Cell;
            }
        }

        // ------------------------------------------------------------------ payout

        /// <summary>
        /// The powder going up. <paramref name="delays"/> is when each cell's cube breaks on
        /// screen (same order as the report). Every charged cube pops; a FULL block gets the bonus
        /// blast, once per block (at its first cube), plus the points printed over it.
        /// </summary>
        public void PlayPayout(PowderPayoutVisuals payout, IReadOnlyList<float> delays)
        {
            if (payout == null || board == null)
            {
                return;
            }
            float cell = board.CellWorldSize;
            bool blasted = false;
            Vector2 sum = Vector2.zero;
            float firstDelay = float.MaxValue;
            for (int i = 0; i < payout.Count; i++)
            {
                Vector2 at = board.CellToWorld(payout.Cells[i]);
                float delay = delays != null && i < delays.Count ? delays[i] : 0f;
                sum += at;
                firstDelay = Mathf.Min(firstDelay, delay);
                float full = Mathf.Clamp01(payout.Fullness[i]);
                Pop(at, cell, delay, full, payout.Cells[i]);
                if (payout.Full[i] && !blasted)
                {
                    blasted = true;
                    Blast(at, cell, delay + 0.05f, payout.Cells[i]);
                }
            }
            if (payout.Count > 0 && payout.Points > 0)
            {
                Vector2 centre = sum / payout.Count;
                StartCoroutineSafe(centre + new Vector2(0f, cell * 0.6f), firstDelay + 0.18f,
                    "+" + payout.Points + (payout.AnyFull ? Loc.Pick("  POWDER!", "  BARUT!") : ""),
                    payout.AnyFull ? 58 : 46);
            }
        }

        private readonly List<(float at, Vector2 where, string text, int size)> pendingText =
            new List<(float, Vector2, string, int)>();

        private float clock;

        private void StartCoroutineSafe(Vector2 where, float delay, string text, int size)
        {
            pendingText.Add((clock + delay, where, text, size));
        }

        /// <summary>A charged cube breaking: a heat flash sized by how full it was and a spray.</summary>
        private void Pop(Vector2 at, float cell, float delay, float full, GridPos seedCell)
        {
            var f = NewBit(KindFlash, Dot, BlastOrder);
            f.From = f.To = at;
            f.Delay = delay;
            f.Life = 0.28f;
            f.Size = cell * Mathf.Lerp(0.9f, 1.4f, full);
            f.EndSize = f.Size * 1.25f;
            f.Colour = HotEmber;
            f.EndColour = CoolEmber;
            f.Alpha = Mathf.Lerp(0.45f, 0.75f, full);
            int n = 3 + Mathf.RoundToInt(full * 5f);
            for (int s = 0; s < n; s++)
            {
                float a = Hash(seedCell, 200 + s) * Mathf.PI * 2f;
                Spark(at, a, cell * Mathf.Lerp(2.2f, 4.2f, Hash(seedCell, 230 + s)) * (0.6f + full),
                    delay, 0.3f + 0.15f * full, cell * 0.06f, full);
            }
        }

        /// <summary>THE BONUS BLAST of a full block.</summary>
        private void Blast(Vector2 at, float cell, float delay, GridPos seedCell)
        {
            // Core flash.
            var core = NewBit(KindFlash, Dot, BlastOrder + 1);
            core.From = core.To = at;
            core.Delay = delay;
            core.Life = 0.34f;
            core.Size = cell * 1.2f;
            core.EndSize = cell * 2.4f;
            core.Colour = BlastCore;
            core.EndColour = HotEmber;
            core.Alpha = 0.9f;
            // Shockwave ring.
            var r = NewBit(KindRing, Ring, BlastOrder);
            r.From = r.To = at;
            r.Delay = delay;
            r.Life = 0.5f;
            r.Size = cell * 0.5f;
            r.EndSize = cell * RingReach * 2f;
            r.Colour = HotEmber;
            r.EndColour = CoolEmber;
            r.Alpha = 0.8f;
            // Sparks thrown out, fast, with drag.
            for (int s = 0; s < BlastSparks; s++)
            {
                float a = (s + Hash(seedCell, 300 + s) * 0.7f) / BlastSparks * Mathf.PI * 2f;
                Spark(at, a, cell * Mathf.Lerp(6f, 10f, Hash(seedCell, 330 + s)), delay,
                    Mathf.Lerp(0.45f, 0.7f, Hash(seedCell, 360 + s)), cell * 0.075f, 1f);
            }
            // Glowing chunks: heavier, slower, falling.
            for (int c = 0; c < BlastChunks; c++)
            {
                float a = Hash(seedCell, 400 + c) * Mathf.PI * 2f;
                var b = NewBit(KindSpark, Dot, BlastOrder);
                b.From = b.To = at;
                b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a) + 0.6f)
                    * cell * Mathf.Lerp(2.5f, 4.5f, Hash(seedCell, 430 + c));
                b.Drag = 2.2f;
                b.Gravity = cell * 7f;
                b.Delay = delay;
                b.Life = Mathf.Lerp(0.55f, 0.8f, Hash(seedCell, 460 + c));
                b.Size = cell * Mathf.Lerp(0.1f, 0.15f, Hash(seedCell, 490 + c));
                b.EndSize = b.Size * 0.3f;
                b.Colour = HotEmber;
                b.EndColour = Soot;
                b.Alpha = 1f;
            }
            // Afterburn: a second, smaller pop a beat later, slightly off centre.
            var after = NewBit(KindFlash, Dot, BlastOrder + 1);
            after.From = after.To = at + new Vector2(Hash(seedCell, 7) - 0.5f, Hash(seedCell, 8) - 0.5f)
                * cell * 0.5f;
            after.Delay = delay + AfterburnDelay;
            after.Life = 0.3f;
            after.Size = cell * 0.8f;
            after.EndSize = cell * 1.7f;
            after.Colour = HotEmber;
            after.EndColour = CoolEmber;
            after.Alpha = 0.7f;
            var ring2 = NewBit(KindRing, Ring, BlastOrder);
            ring2.From = ring2.To = after.From;
            ring2.Delay = after.Delay;
            ring2.Life = 0.38f;
            ring2.Size = cell * 0.4f;
            ring2.EndSize = cell * RingReach * 1.2f;
            ring2.Colour = HotEmber;
            ring2.EndColour = CoolEmber;
            ring2.Alpha = 0.5f;

            knockAt = clock + delay;
            knockDir = Vector2.down;
            pendingPeaks.Add(clock + delay);
        }

        private readonly List<float> pendingPeaks = new List<float>();

        private void Spark(Vector2 at, float angle, float speed, float delay, float life, float size,
            float full)
        {
            var b = NewBit(KindSpark, Dot, BlastOrder);
            b.From = b.To = at;
            b.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            b.Drag = 5.5f;
            b.Gravity = speed * 0.25f;
            b.Delay = delay;
            b.Life = life;
            b.Size = size;
            b.EndSize = size * 0.25f;
            b.Stretch = 2.8f;
            b.Colour = Color.Lerp(HotEmber, BlastCore, full * 0.6f);
            b.EndColour = CoolEmber;
            b.Alpha = 1f;
        }

        // ------------------------------------------------------------------ tick

        private void Update()
        {
            float dt = Time.deltaTime;
            clock += dt;
            float cell = board != null ? board.CellWorldSize : 1f;

            for (int i = 0; i < heats.Count; i++)
            {
                TickHeat(heats[i], dt, cell);
            }

            for (int i = bits.Count - 1; i >= 0; i--)
            {
                Bit b = bits[i];
                b.Age += dt;
                float t = (b.Age - b.Delay) / Mathf.Max(b.Life, 0.0001f);
                if (t < 0f)
                {
                    b.R.color = Invisible;
                    continue;
                }
                if (t >= 1f || b.R == null)
                {
                    if (b.Kind == KindGrain && b.TargetCell.HasValue)
                    {
                        Heat target = HeatAt(b.TargetCell.Value);
                        if (target != null)
                        {
                            target.Gulp = 0f;
                        }
                    }
                    Return(b.R);
                    bits.RemoveAt(i);
                    continue;
                }
                TickBit(b, t, dt);
            }

            for (int i = pendingText.Count - 1; i >= 0; i--)
            {
                if (clock >= pendingText[i].at)
                {
                    FloatingTextFx.Spawn(transform, pendingText[i].where, pendingText[i].text,
                        HotEmber, pendingText[i].size, 0.08f);
                    pendingText.RemoveAt(i);
                }
            }
            for (int i = pendingPeaks.Count - 1; i >= 0; i--)
            {
                if (clock >= pendingPeaks[i])
                {
                    pendingPeaks.RemoveAt(i);
                    if (BlastPeaked != null)
                    {
                        BlastPeaked(true);
                    }
                }
            }
            TickKnock();
        }

        private void TickHeat(Heat h, float dt, float cell)
        {
            float period = h.Full ? FullPulse : Mathf.Lerp(SlowPulse, FastPulse, h.Fullness);
            float k = 0.5f + 0.5f * Mathf.Sin((clock + h.Seed * 3f) * Mathf.PI * 2f / period);
            float gulp = 0f;
            if (h.Gulp >= 0f)
            {
                h.Gulp += dt;
                float g = h.Gulp / GulpSeconds;
                gulp = g < 1f ? Mathf.Sin(g * Mathf.PI) * (1f - g * 0.3f) : 0f;
                if (g >= 1f)
                {
                    h.Gulp = -1f;
                }
            }
            Vector2 at = board.CellToWorld(h.Cell);
            if (h.Glow != null)
            {
                Color c = Color.Lerp(CoolEmber, HotEmber, h.Fullness);
                c.a = Mathf.Clamp01(Mathf.Lerp(0.2f, 0.55f, h.Fullness)
                    * Mathf.Lerp(h.Full ? 0.55f : 0.7f, 1f, k) + gulp * 0.35f);
                h.Glow.color = c;
                float s = cell * Mathf.Lerp(0.55f, 0.85f, h.Fullness) * (1f + GulpPunch * gulp
                    + (h.Full ? 0.06f * k : 0f));
                SetSize(h.Glow, at, s, s);
            }
            if (h.Fuse != null)
            {
                // A live spark: flickers on its own fast noise, hotter and bigger when fuller.
                float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(clock * 14f, h.Seed * 50f);
                Color c = Color.Lerp(HotEmber, BlastCore, h.Fullness * 0.7f);
                c.a = Mathf.Clamp01(Mathf.Lerp(0.55f, 1f, h.Fullness) * flicker + gulp * 0.3f);
                h.Fuse.color = c;
                float s = cell * Mathf.Lerp(0.12f, 0.22f, h.Fullness) * flicker * (1f + gulp * 0.5f);
                SetSize(h.Fuse, at + FuseTip(cell), s, s);
                // It spits: tiny sparks up and off, faster the fuller it is.
                h.SparkWait -= dt;
                if (h.SparkWait <= 0f)
                {
                    h.SparkWait = Mathf.Lerp(0.55f, 0.09f, h.Fullness) * (0.6f + 0.8f * Random01());
                    Spit(at + FuseTip(cell), cell, h.Fullness);
                }
            }
            if (h.Rim != null)
            {
                Color c = HotEmber;
                c.a = 0.18f + 0.3f * k;
                h.Rim.color = c;
                float s = cell * (1.12f + 0.06f * k);
                SetSize(h.Rim, at, s, s);
            }
        }

        private void Spit(Vector2 at, float cell, float full)
        {
            var b = NewBit(KindSpark, Dot, FuseOrder);
            b.From = b.To = at;
            float a = Mathf.Lerp(0.2f, 1.4f, Random01()) * Mathf.PI * 0.5f + Mathf.PI * 0.1f;
            b.Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cell * Mathf.Lerp(0.8f, 1.8f, full);
            b.Drag = 3f;
            b.Gravity = cell * 2.5f;
            b.Life = Mathf.Lerp(0.25f, 0.4f, Random01());
            b.Size = cell * Mathf.Lerp(0.035f, 0.055f, full);
            b.EndSize = b.Size * 0.3f;
            b.Stretch = 1.8f;
            b.Colour = BlastCore;
            b.EndColour = CoolEmber;
            b.Alpha = Mathf.Lerp(0.6f, 1f, full);
        }

        private void TickBit(Bit b, float t, float dt)
        {
            Vector2 pos;
            float angle = 0f;
            if (b.Kind == KindGrain)
            {
                // Drawn in on a curl, accelerating into the cube.
                float e = t * t;
                Vector2 d = b.From - b.To;
                float curl = b.Spin * (1f - e) * 0.9f;
                float cs = Mathf.Cos(curl);
                float sn = Mathf.Sin(curl);
                pos = b.To + new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs) * (1f - e);
            }
            else if (b.Kind == KindSpark)
            {
                b.Velocity *= Mathf.Exp(-b.Drag * dt);
                b.Velocity += Vector2.down * b.Gravity * dt;
                b.To += b.Velocity * dt;
                pos = b.To;
                angle = Mathf.Atan2(b.Velocity.y, b.Velocity.x) * Mathf.Rad2Deg;
            }
            else
            {
                pos = b.From;
            }
            float size = Mathf.Lerp(b.Size, b.EndSize, b.Kind == KindRing ? 1f - (1f - t) * (1f - t) : t);
            Color c = Color.Lerp(b.Colour, b.EndColour, t);
            float fade = b.Kind == KindGrain ? Mathf.Clamp01(t * 6f) : (1f - t) * (1f - t);
            if (b.Kind == KindFlash)
            {
                fade = 1f - t;
                fade *= fade;
            }
            c.a = b.Alpha * fade;
            b.R.color = c;
            b.R.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            b.R.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            float speed = b.Kind == KindSpark ? Mathf.Clamp01(b.Velocity.magnitude / (size * 40f + 1e-4f)) : 0f;
            float stretch = 1f + (b.Stretch > 0f ? (b.Stretch - 1f) * speed : 0f);
            SetScale(b.R, size * stretch, size);
        }

        private void TickKnock()
        {
            if (knockAt < 0f || board == null)
            {
                return;
            }
            float age = clock - knockAt;
            if (age < 0f)
            {
                return;
            }
            const float dur = 0.22f;
            if (age >= dur)
            {
                board.SetImpulse(Vector2.zero);
                knockAt = -1f;
                return;
            }
            float u = age / dur;
            float shape = Mathf.Sin(u * Mathf.PI * 2.5f) * (1f - u) * (1f - u);
            board.SetImpulse(knockDir * KnockDistance * shape);
        }

        // ------------------------------------------------------------------ plumbing

        private static readonly Color Invisible = new Color(1f, 1f, 1f, 0f);

        private Bit NewBit(int kind, Sprite sprite, int order)
        {
            var b = new Bit { Kind = kind, R = Rent(sprite, order, Vector2.zero, 0.01f) };
            b.R.color = Invisible;
            bits.Add(b);
            return b;
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Vector2 at, float size)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
                r.enabled = true;
            }
            else
            {
                var go = new GameObject("Powder");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Invisible;
            r.transform.localRotation = Quaternion.identity;
            SetSize(r, at, size, size);
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            spare.Push(r);
        }

        private static void SetSize(SpriteRenderer r, Vector2 at, float w, float h)
        {
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            SetScale(r, w, h);
        }

        private static void SetScale(SpriteRenderer r, float w, float h)
        {
            Vector2 unit = r.sprite != null ? (Vector2)r.sprite.bounds.size : Vector2.one;
            r.transform.localScale = new Vector3(w / Mathf.Max(unit.x, 1e-4f),
                h / Mathf.Max(unit.y, 1e-4f), 1f);
        }

        private static float Hash(GridPos cell, int salt)
        {
            unchecked
            {
                uint h = (uint)(cell.X * 73856093) ^ (uint)(cell.Y * 19349663) ^ (uint)(salt * 83492791);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private uint spitState = 0x9E3779B9u;

        /// <summary>The fuse's spit timing only - pure decoration, so a local xorshift, never
        /// UnityEngine.Random's global stream.</summary>
        private float Random01()
        {
            spitState ^= spitState << 13;
            spitState ^= spitState >> 17;
            spitState ^= spitState << 5;
            return (spitState & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>A soft round dot, falling off to zero at its own edge.</summary>
        private static Sprite Dot
        {
            get
            {
                if (dot == null)
                {
                    dot = Bake(64, (d) =>
                    {
                        float k = 1f - Mathf.Clamp01(d);
                        return k * k;
                    });
                }
                return dot;
            }
        }

        /// <summary>A thin soft ring for the shockwave.</summary>
        private static Sprite Ring
        {
            get
            {
                if (ring == null)
                {
                    ring = Bake(128, (d) =>
                    {
                        float k = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.86f) / 0.12f);
                        return k * k;
                    });
                }
                return ring;
            }
        }

        private static Sprite Bake(int px, System.Func<float, float> alphaOfDistance)
        {
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false);
            float half = px * 0.5f;
            for (int y = 0; y < px; y++)
            {
                for (int x = 0; x < px; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alphaOfDistance(Mathf.Sqrt(dx * dx + dy * dy))));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);
        }

        private void ClearHeat()
        {
            for (int i = 0; i < heats.Count; i++)
            {
                Return(heats[i].Glow);
                Return(heats[i].Fuse);
                Return(heats[i].Rim);
            }
            heats.Clear();
        }

        /// <summary>Grains find their heat by CELL, so a repaint that restates the heat mid-pour
        /// does not rob the block of its gulp.</summary>
        private Heat HeatAt(GridPos cell)
        {
            for (int i = 0; i < heats.Count; i++)
            {
                if (heats[i].Cell.Equals(cell))
                {
                    return heats[i];
                }
            }
            return null;
        }

        /// <summary>Takes everything down - a new round, a rebuilt board, leaving the round.</summary>
        public void Clear()
        {
            ClearHeat();
            for (int i = 0; i < bits.Count; i++)
            {
                Return(bits[i].R);
            }
            bits.Clear();
            pendingText.Clear();
            pendingPeaks.Clear();
            if (knockAt >= 0f && board != null)
            {
                board.SetImpulse(Vector2.zero);
            }
            knockAt = -1f;
        }
    }
}
