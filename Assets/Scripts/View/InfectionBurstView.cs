// PURPOSE: "Enfeksiyon"'s detonation - a block EATEN FROM INSIDE, not blown up.
//
// WHAT IT REPLACES. This used to be FlashCells(cells, green) plus a camera shake: the shared
// destruction language with a green tint on it, the same squares the dynamite and the sweeper
// strike. It read as "a green effect happened", and nothing about it said an infection had spent
// three turns ripening inside that block.
//
// SO IT IS BUILT AROUND ONE SENTENCE: the core ripened, the sickness ran through the block, and the
// block was eaten away from the inside. In order -
//   STAND      the block, wearing its own tiles, stands through the core's charge
//   VEINS      vessels grow out through it, cube by cube from the cell that ripened
//   PULL       a beat of inward pressure, so the rupture has something to release
//   DISSOLVE   each cube is eaten from its centre outward, with a glowing front of rot at the edge
//   RUPTURE    a small lumpy puff of bio-plasma and spores - never a fireball, never a star
//   CARRY      on the FIRST detonation only, spores carry the contagion to the cells it took
//   AFTERMATH  a faint stain that clears almost at once
//
// ONE ORGANISM, NOT SEPARATE BLASTS. Every beat runs outward from the ripe cell THROUGH the block
// (Ghost.Steps), so a four-cube block reads as one thing coming apart from where it was infected,
// not as four identical explosions going off together.
//
// THE BLOCK IS NEVER TURNED INTO A GREEN SQUARE. It is eaten: a SpriteMask whose sprite rises from
// the corners to the centre has its cutoff lowered, so the cube disappears from the middle outward
// behind a ragged front. The rot is only a THIN BAND just behind that front, cut out by two more
// masks on the same field - one hiding what is not eaten yet, one hiding what was eaten long ago -
// so the hollow it leaves is empty board, not a green fill. (The first version filled the whole
// eaten area with glow, and at half-eaten the cell measured 98% green: the square was back.) No
// shader - the same component the market already clips its shelf with.
//
// IT NEVER GUESSES WHAT SPREAD. The carriers fly to the rules' own EnfeksiyonJoker.LastSpreadCells,
// and the core waiting there is held (InfectionCoreView.HoldBirth) until they land, then blooms.
// They REPLACE the core view's straight tendril for a detonation: four straight lines out of one
// cell is a plus sign, and this is supposed to be contagion.
//
// IT IS NOT THE OTHERS. Blue and arcs belong to the circuit; fire, smoke and a shockwave to the
// dynamite; a clean wipe to a sweep. This is the infection cores' own mint and nothing else, with no
// shake and no screen flash - nothing hits the board, something inside it gives way.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The infection detonation: the block stands, is veined, eaten and dispersed.</summary>
    public sealed class InfectionBurstView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ travel
            /// <summary>How much later every beat reaches a cube one step further THROUGH the
            /// block from the cell that ripened. This is what makes the block one organism coming
            /// apart; zero puts every cube back in lockstep.</summary>
            public static float StepSeconds = 0.06f;

            // ------------------------------------------------------------------ veins
            /// <summary>How long the vessels take to grow through ONE cube. The cube is still
            /// entirely itself while they do - that is the point.</summary>
            public static float VeinSeconds = 0.22f;

            public static float VeinOpacity = 0.9f;

            /// <summary>How big the vessel network starts, as a share of the cube. It grows OUT
            /// from the middle, which is what reads as spreading rather than appearing.</summary>
            public static float VeinStartScale = 0.3f;

            // ------------------------------------------------------------------ pressure
            /// <summary>The inward pull just before it gives. A few per cent, and it is what
            /// gives the rupture something to release.</summary>
            public static float PullAmount = 0.05f;

            public static float PullSeconds = 0.08f;

            // ------------------------------------------------------------------ dissolve
            /// <summary>How long one cube takes to be eaten, centre to corners.</summary>
            public static float DissolveSeconds = 0.34f;

            /// <summary>How bright the front of the rot is as it eats outward.</summary>
            public static float RotOpacity = 0.95f;

            /// <summary>How thick that front is, as a share of the cube's area. The glow lives only
            /// in this band; everything behind it is hollow. A hot spot the moment it opens, a thin
            /// crack of light by the time it reaches the corners.</summary>
            public static float RotFrontWidth = 0.10f;

            /// <summary>How far what is left of the cube darkens as it goes - sick tissue, not a
            /// colour change. Well short of 1, so the block's own tile reads to the end.</summary>
            public static float TissueDarken = 0.35f;

            // ------------------------------------------------------------------ rupture puff
            public static float PuffSeconds = 0.30f;

            /// <summary>The puff's size as a share of a cell. Compact on purpose: block-scale,
            /// never board-scale.</summary>
            public static float PuffSize = 0.95f;

            /// <summary>How much bigger the ripe cell's puff is - it is where this started.</summary>
            public static float SourcePuff = 1.35f;

            public static float PuffOpacity = 0.6f;

            /// <summary>How lumpy the puff's outline is. 0 is a clean disc. The lumps are laid out
            /// by POSITION rather than by angle, so it can never grow points. Read when the sprite
            /// is first baked.</summary>
            public static float PuffRagged = 0.34f;

            // ------------------------------------------------------------------ spores
            public static int SporesPerCell = 6;

            public static float SporeSpeedMin = 0.6f;

            public static float SporeSpeedMax = 1.7f;

            public static float SporeSize = 0.2f;

            public static float SporeLifeMin = 0.35f;

            public static float SporeLifeMax = 0.7f;

            /// <summary>How hard a spore is blown AWAY from the ripe cell on top of its own
            /// direction, so the whole cloud visibly came out of one place.</summary>
            public static float SporeAway = 0.6f;

            /// <summary>The slow rise a spore takes over once its speed is spent - what makes it
            /// airborne rather than thrown.</summary>
            public static float SporeLift = 0.5f;

            public static float SporeDrag = 3.4f;

            // ------------------------------------------------------------------ carriers
            /// <summary>Spores sent to each cell the spread took. A handful travelling together,
            /// so the contagion arrives as a drift and not as a drawn line.</summary>
            public static int CarriersPerTarget = 4;

            /// <summary>How long a carrier takes to reach its cell. The core there blooms as the
            /// first one lands, so this is also how long after the rupture the spread arrives.</summary>
            public static float CarrierSeconds = 0.20f;

            public static float CarrierSize = 0.17f;

            /// <summary>How far a carrier may bow off the straight path, in cells.</summary>
            public static float CarrierBow = 0.16f;

            // ------------------------------------------------------------------ aftermath
            /// <summary>The stain a consumed cube leaves. LOW and short: the cell has to read as
            /// empty and playable again almost at once, so this is a trace, not a mark.</summary>
            public static float AftermathOpacity = 0.30f;

            public static float AftermathSeconds = 0.40f;

            // ------------------------------------------------------------------ palette
            /// <summary>
            /// The one colour of its own: deep viridian, for sick tissue and the stain.
            ///
            /// Everything bright is read from InfectionCoreView.Style instead, because the burst
            /// must be the thing those cores were, grown up - and that palette is deliberately clean
            /// and cyan-leaning so it never reads as the Kangren boss's dead green. No olive and no
            /// rotting yellow here either.
            /// </summary>
            public static Color Deep = new Color(0.03f, 0.15f, 0.12f);
        }

        // =================================================================== orders
        // The ghost cube sits where a real cube would: under the element embers (3), the line glow
        // (4) and the infection cores (6-9), so the charging core stays visible on top of it. Order 1
        // is the board's own cells and a tie there is a coin toss, so the ghost takes 2.

        private const int BodyOrder = 2;

        /// <summary>Shares the body's order, and only ever appears once that cube's body is gone.</summary>
        private const int StainOrder = 2;

        /// <summary>Two orders above the body, not one: the rot has masks of its own, and a gap on
        /// both sides keeps each set of masks off the other's renderer whether a range's back end
        /// counts as inside it or not - Unity's documentation does not say.</summary>
        private const int RotOrder = 4;

        private const int VeinOrder = 5;

        private const int PuffOrder = 10;

        private const int SporeOrder = 11;

        // Each set of masks reaches only its own renderer. Anything else between these orders
        // carries no mask interaction and is untouched, and the ranges keep the market's own masked
        // renderers out of both.
        private const int BodyMaskBack = 1;

        private const int BodyMaskFront = 2;

        private const int RotMaskBack = 3;

        private const int RotMaskFront = 4;

        /// <summary>The mask is a touch bigger than a cell, so a tile's rounded corners are eaten
        /// too instead of being switched off at the end.</summary>
        private const float MaskSize = 1.04f;

        /// <summary>One cube of the block that is going, with the art it was wearing.</summary>
        public struct Ghost
        {
            public Vector2 Where;
            public Sprite Tile;
            public Color Colour;

            /// <summary>The cube's size on the board, so the ghost replaces it without a jump.</summary>
            public float Size;

            /// <summary>Steps through the block from the cell that ripened; 0 IS that cell.</summary>
            public int Steps;
        }

        /// <summary>True while any detonation is still on screen.</summary>
        public bool Active
        {
            get { return live > 0; }
        }

        /// <summary>How many bursts are running. Two ripe blocks can go on the same turn, so each
        /// burst owns its own container and cleans up only that.</summary>
        private int live;

        private float cellSize = 1f;

        private sealed class Piece
        {
            public Ghost Ghost;
            public int Index;
            public float Size;
            public SpriteRenderer Body;
            public SpriteMask Mask;
            public SpriteMask Uneaten;
            public SpriteMask Hollow;
            public SpriteRenderer Rot;
            public SpriteRenderer Veins;
            public bool Ruptured;
            public bool Done;
        }

        // =================================================================== driving it

        /// <summary>
        /// Plays one block's detonation and returns the seconds from now until the ripe cell
        /// ruptures - which is when the sound lands and the carriers leave.
        ///
        /// The block goes up THIS frame and stands for <paramref name="hold"/> (the core's
        /// charge) before anything happens to it. <paramref name="carryTo"/> is the rules' own
        /// list of cells the spread took, empty or null on every detonation after the first.
        /// </summary>
        public float Play(IReadOnlyList<Ghost> cells, float cell, float hold, Vector2 from,
            IReadOnlyList<Vector2> carryTo)
        {
            hold = Mathf.Max(hold, 0f);
            float rupture = hold + Style.VeinSeconds + Style.PullSeconds;
            if (cells == null || cells.Count == 0 || cell <= 0f)
            {
                return rupture;
            }
            cellSize = cell;
            var host = new GameObject("Burst");
            host.transform.SetParent(transform, false);
            // A coroutine runs up to its first yield inside StartCoroutine, so the ghost block is
            // built on this very frame - the frame the board painted the real cubes away.
            StartCoroutine(Burst(host.transform, new List<Ghost>(cells), hold, from,
                carryTo == null ? new List<Vector2>() : new List<Vector2>(carryTo)));
            return rupture;
        }

        /// <summary>Clears everything at once - the lab's RESET.</summary>
        public void Stop()
        {
            StopAllCoroutines();
            live = 0;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        // =================================================================== the sequence

        private IEnumerator Burst(Transform host, List<Ghost> cells, float hold, Vector2 from,
            List<Vector2> carryTo)
        {
            live++;
            var pieces = new List<Piece>(cells.Count);
            int deepest = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                pieces.Add(Build(host, cells[i], i));
                deepest = Mathf.Max(deepest, cells[i].Steps);
            }

            // ---- STAND. Nothing happens to the block through the charge: it is simply still
            // there, which is the whole difference between a detonation and a vanish.
            if (hold > 0f)
            {
                yield return new WaitForSeconds(hold);
            }

            float ruptureAt = Style.VeinSeconds + Style.PullSeconds;
            float finish = deepest * Style.StepSeconds + ruptureAt + Style.DissolveSeconds;
            bool carried = false;
            float t = 0f;
            while (t < finish)
            {
                t += Time.deltaTime;
                for (int i = 0; i < pieces.Count; i++)
                {
                    Piece p = pieces[i];
                    if (!p.Done)
                    {
                        Advance(host, p, t - p.Ghost.Steps * Style.StepSeconds, from);
                    }
                }
                // The spread leaves with the ripe cell's rupture and never before; the cores it is
                // flying to are held until it lands.
                if (!carried && t >= ruptureAt)
                {
                    carried = true;
                    for (int i = 0; i < carryTo.Count; i++)
                    {
                        StartCoroutine(Carry(host, from, carryTo[i]));
                    }
                }
                yield return null;
            }
            for (int i = 0; i < pieces.Count; i++)
            {
                Finish(host, pieces[i]);
            }

            // Long enough for the last cube's stain to clear and its spores to settle.
            yield return new WaitForSeconds(
                Mathf.Max(Style.AftermathSeconds, Style.SporeLifeMax) + 0.05f);
            live--;
            if (host != null)
            {
                Destroy(host.gameObject);
            }
        }

        /// <summary>One cube at <paramref name="local"/> seconds into its own part of the sequence.
        /// Negative means the rot has not reached it yet.</summary>
        private void Advance(Transform host, Piece p, float local, Vector2 from)
        {
            if (local <= 0f)
            {
                return;
            }

            // ---- veins grow out through it
            float grown = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(local / Style.VeinSeconds));

            // ---- the pull
            float pulled = Mathf.Clamp01((local - Style.VeinSeconds) / Style.PullSeconds);
            float squeeze = 1f - Style.PullAmount * Mathf.Sin(pulled * Mathf.PI);

            // ---- eaten
            float eatFrom = Style.VeinSeconds + Style.PullSeconds;
            bool eating = local >= eatFrom;
            float eaten = Mathf.Clamp01((local - eatFrom) / Style.DissolveSeconds);
            if (eating && !p.Ruptured)
            {
                p.Ruptured = true;
                Rupture(host, p, from);
            }

            float swell = 1f + 0.04f * eaten;
            p.Body.transform.localScale = Square(p.Size * squeeze * swell);
            Color tissue = Color.Lerp(p.Ghost.Colour, Style.Deep, Style.TissueDarken * eaten);
            // The corners are the last thing the rot reaches, and a few pixels of them would
            // otherwise still be standing when the cube is switched off - so they thin away over
            // the last stretch instead of blinking out.
            tissue.a *= 1f - Edge(0.72f, 0.95f, eaten);
            p.Body.color = tissue;
            // The field is EQUALISED - every step of it covers the same share of the cube - so a
            // cutoff falling in a straight line eats area in a straight line, and DissolveSeconds
            // means what it says. (Squared, on a raw field, the cube was 95% gone at half time.)
            float front = eating ? 1f - eaten : 1f;
            p.Mask.alphaCutoff = front;
            // The rot band is what lies between these two: at or past the front, and no further past
            // it than RotFrontWidth. Until eating starts, Uneaten covers the whole cell.
            p.Uneaten.alphaCutoff = eating ? 1f - front : 0f;
            p.Hollow.alphaCutoff = Mathf.Min(1f, front + Style.RotFrontWidth);

            p.Veins.transform.localScale =
                Square(p.Size * Mathf.Lerp(Style.VeinStartScale, 1f, grown) * squeeze);
            p.Veins.color = Alpha(InfectionCoreView.Style.GlowColor,
                Style.VeinOpacity * grown * (1f - Edge(0f, 0.6f, eaten)));

            // Gone by the time the front has left the cube itself for the gutter round it.
            p.Rot.color = Alpha(InfectionCoreView.Style.GlowColor,
                eating ? Style.RotOpacity * Edge(0f, 0.04f, eaten) * (1f - Edge(0.6f, 0.85f, eaten)) : 0f);

            if (eaten >= 1f)
            {
                Finish(host, p);
            }
        }

        private Piece Build(Transform host, Ghost g, int index)
        {
            var p = new Piece
            {
                Ghost = g,
                Index = index,
                Size = g.Size > 0f ? g.Size : cellSize * 0.98f
            };

            p.Body = Make(host, "Body", null, BodyOrder);
            ViewUtil.ApplyTile(p.Body, g.Tile, p.Size);
            // Always the PLAIN material. A water or lava tile animates itself with a warp material,
            // and there is no telling that shader honours a mask - on it the cube might never be
            // eaten, only switched off at the end. It gives up its swirl for the half second it
            // takes to go and keeps its art.
            if (ViewUtil.PlainSpriteMaterial != null)
            {
                p.Body.sharedMaterial = ViewUtil.PlainSpriteMaterial;
            }
            p.Body.transform.localPosition = Flat(g.Where);
            p.Body.color = g.Colour;
            p.Body.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            p.Mask = MakeMask(host, "Eaten", g.Where, RotMaskSprite(), 1f,
                BodyMaskBack, BodyMaskFront);
            p.Uneaten = MakeMask(host, "Uneaten", g.Where, RotInvertedSprite(), 0f,
                RotMaskBack, RotMaskFront);
            p.Hollow = MakeMask(host, "Hollow", g.Where, RotMaskSprite(), 1f,
                RotMaskBack, RotMaskFront);

            p.Rot = Make(host, "Rot", RotBandSprite(), RotOrder);
            p.Rot.transform.localPosition = Flat(g.Where);
            p.Rot.transform.localScale = Square(cellSize * MaskSize);
            p.Rot.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            p.Rot.color = Alpha(InfectionCoreView.Style.GlowColor, 0f);

            // Three vessel patterns, each turned a quarter at a time, so neighbouring cubes never
            // wear the same stamp - and never turned off-axis, which would swing the pattern's
            // square edge out of its cell.
            p.Veins = Make(host, "Veins", VeinSprite(index + g.Steps), VeinOrder);
            p.Veins.transform.localPosition = Flat(g.Where);
            p.Veins.transform.localRotation =
                Quaternion.Euler(0f, 0f, 90f * ((index * 3 + g.Steps) % 4));
            p.Veins.transform.localScale = Square(p.Size * Style.VeinStartScale);
            p.Veins.color = Alpha(InfectionCoreView.Style.GlowColor, 0f);
            return p;
        }

        private SpriteMask MakeMask(Transform host, string name, Vector2 at, Sprite sprite,
            float cutoff, int back, int front)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host, false);
            go.transform.localPosition = Flat(at);
            go.transform.localScale = Square(cellSize * MaskSize);
            var mask = go.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = cutoff;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = back;
            mask.frontSortingOrder = front;
            return mask;
        }

        /// <summary>The cube is gone: its pieces are switched off and it leaves a stain.</summary>
        private void Finish(Transform host, Piece p)
        {
            if (p.Done)
            {
                return;
            }
            p.Done = true;
            p.Body.enabled = false;
            p.Rot.enabled = false;
            p.Veins.enabled = false;
            p.Mask.enabled = false;
            p.Uneaten.enabled = false;
            p.Hollow.enabled = false;
            MakeStain(host, p.Ghost.Where);
        }

        // =================================================================== the rupture

        private void Rupture(Transform host, Piece p, Vector2 from)
        {
            StartCoroutine(Puff(host, p.Ghost.Where, p.Ghost.Steps == 0, p.Index));
            Vector2 away = p.Ghost.Where - from;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.zero;
            SpawnSpores(host, p.Ghost.Where, away, Style.SporesPerCell);
        }

        /// <summary>A small lumpy cloud of bio-plasma where the cube splits. Pale at the moment it
        /// opens and settling to the cores' own glow as it thins, so it reads as the same thing the
        /// cores were rather than as a separate effect.</summary>
        private IEnumerator Puff(Transform host, Vector2 at, bool source, int index)
        {
            SpriteRenderer r = Make(host, "Puff", PuffSprite(), PuffOrder);
            r.transform.localPosition = Flat(at);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, index * 97f);
            float full = cellSize * Style.PuffSize * (source ? Style.SourcePuff : 1f);
            float t = 0f;
            while (t < Style.PuffSeconds && r != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.PuffSeconds);
                float open = 1f - (1f - k) * (1f - k);
                r.transform.localScale = Square(full * Mathf.Lerp(0.45f, 1f, open));
                r.color = Alpha(Color.Lerp(InfectionCoreView.Style.CoreColor,
                    InfectionCoreView.Style.GlowColor, k),
                    Style.PuffOpacity * Mathf.Pow(1f - k, 1.5f));
                yield return null;
            }
            if (r != null)
            {
                Destroy(r.gameObject);
            }
        }

        // =================================================================== the spread

        /// <summary>
        /// The spread, carried. A few spores leave the ripe cell together and drift along a shallow
        /// bow to one cell the spread took; the core waiting there is held until the first of them
        /// lands (BoardView hands CarrierSeconds on to HoldBirth) and blooms as they arrive.
        /// </summary>
        private IEnumerator Carry(Transform host, Vector2 from, Vector2 to)
        {
            float travel = Mathf.Max(Style.CarrierSeconds, 0.01f);
            int n = Mathf.Max(1, Style.CarriersPerTarget);
            var spores = new SpriteRenderer[n];
            var bow = new float[n];
            var delay = new float[n];
            var size = new float[n];
            for (int i = 0; i < n; i++)
            {
                spores[i] = Make(host, "Carrier", SporeSprite(), SporeOrder);
                spores[i].transform.localPosition = Flat(from);
                spores[i].color = Alpha(InfectionCoreView.Style.SporeColor, 0f);
                bow[i] = Random.Range(-1f, 1f) * Style.CarrierBow * cellSize;
                delay[i] = travel * 0.25f * i / n;
                size[i] = Style.CarrierSize * cellSize * Random.Range(0.75f, 1.2f);
            }
            Vector2 d = to - from;
            Vector2 side = d.sqrMagnitude > 0.0001f
                ? new Vector2(-d.y, d.x).normalized : Vector2.zero;
            float t = 0f;
            float total = travel * 1.25f;
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = 0; i < n; i++)
                {
                    if (spores[i] == null)
                    {
                        continue;
                    }
                    float k = Mathf.Clamp01((t - delay[i]) / travel);
                    float e = Mathf.SmoothStep(0f, 1f, k);
                    Vector2 pos = from + d * e + side * (bow[i] * Mathf.Sin(e * Mathf.PI));
                    spores[i].transform.localPosition = Flat(pos);
                    spores[i].transform.localScale = Square(size[i] * (1f - 0.35f * e));
                    float alpha = t < delay[i] ? 0f
                        : 0.9f * Edge(0f, 0.12f, k) * (1f - Edge(0.8f, 1f, k));
                    spores[i].color = Alpha(InfectionCoreView.Style.SporeColor, alpha);
                }
                yield return null;
            }
            for (int i = 0; i < n; i++)
            {
                if (spores[i] != null)
                {
                    Destroy(spores[i].gameObject);
                }
            }
        }

        // =================================================================== spores

        /// <summary>
        /// What the cube comes apart INTO: spores, never shards. Hard pieces are the dynamite's
        /// language, and this block is being eaten.
        ///
        /// They are thrown out - pushed away from the ripe cell on top of their own direction, so
        /// the cloud visibly came from one place - then lose their speed quickly and hang, drifting
        /// up as they fade. That is what makes them airborne rather than thrown.
        /// </summary>
        private void SpawnSpores(Transform host, Vector2 at, Vector2 away, int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpriteRenderer r = Make(host, "Spore", SporeSprite(), SporeOrder);
                float size = Style.SporeSize * cellSize * Random.Range(0.6f, 1.4f);
                r.transform.localPosition = Flat(at + Random.insideUnitCircle * (cellSize * 0.3f));
                r.transform.localScale = Square(size);
                Vector2 dir = Random.insideUnitCircle.normalized + away * Style.SporeAway;
                dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
                Vector2 vel = dir * (Random.Range(Style.SporeSpeedMin, Style.SporeSpeedMax) * cellSize);
                Color tint = Random.value < 0.35f
                    ? InfectionCoreView.Style.CoreColor : InfectionCoreView.Style.SporeColor;
                StartCoroutine(DriftSpore(r, vel,
                    Random.Range(Style.SporeLifeMin, Style.SporeLifeMax), size, tint));
            }
        }

        private IEnumerator DriftSpore(SpriteRenderer r, Vector2 vel, float life, float size,
            Color tint)
        {
            float t = 0f;
            Vector2 pos = r.transform.localPosition;
            while (t < life && r != null)
            {
                float dt = Time.deltaTime;
                t += dt;
                float k = t / life;
                vel = Vector2.Lerp(vel, Vector2.up * (Style.SporeLift * cellSize),
                    1f - Mathf.Exp(-Style.SporeDrag * dt));
                pos += vel * dt;
                r.transform.localPosition = Flat(pos);
                r.transform.localScale = Square(size * (1f - 0.35f * k));
                r.color = Alpha(tint, (1f - k) * (1f - k));
                yield return null;
            }
            if (r != null)
            {
                Destroy(r.gameObject);
            }
        }

        /// <summary>The faint trace a consumed cube leaves, gone again almost at once.</summary>
        private void MakeStain(Transform host, Vector2 at)
        {
            SpriteRenderer r = Make(host, "Stain", StainSprite(), StainOrder);
            r.transform.localPosition = Flat(at);
            r.transform.localScale = Square(cellSize * 0.94f);
            r.color = Alpha(Style.Deep, Style.AftermathOpacity);
            StartCoroutine(FadeStain(r));
        }

        private IEnumerator FadeStain(SpriteRenderer r)
        {
            float t = 0f;
            while (t < Style.AftermathSeconds && r != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.AftermathSeconds);
                r.color = Alpha(Style.Deep, Style.AftermathOpacity * (1f - k) * (1f - k));
                yield return null;
            }
            if (r != null)
            {
                Destroy(r.gameObject);
            }
        }

        // =================================================================== helpers

        private SpriteRenderer Make(Transform host, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host != null ? host : transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                r.sprite = sprite;
            }
            r.sortingOrder = order;
            return r;
        }

        private static Vector3 Flat(Vector2 v)
        {
            return new Vector3(v.x, v.y, 0f);
        }

        private static Vector3 Square(float s)
        {
            return new Vector3(s, s, 1f);
        }

        private static Color Alpha(Color c, float a)
        {
            c.a = a;
            return c;
        }

        /// <summary>
        /// GLSL's smoothstep: 0 below edge0, 1 above edge1, eased between.
        ///
        /// NOT Mathf.SmoothStep, which eases between two VALUES: Mathf.SmoothStep(0.55f, 0.97f, r)
        /// returns a number between 0.55 and 0.97, not an edge at them - and that silent misuse
        /// is exactly what broke this file's first version.
        /// </summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // =================================================================== the art

        private const int RotResolution = 128;

        private static Sprite rotMaskSprite;

        private static Sprite rotInvertedSprite;

        private static Sprite rotBandSprite;

        private static readonly Sprite[] veinSprites = new Sprite[3];

        private static Sprite puffSprite;

        private static Sprite sporeSprite;

        private static Sprite stainSprite;

        private static Sprite RotMaskSprite()
        {
            if (rotMaskSprite == null)
            {
                rotMaskSprite = BakeAlpha(RotField(false), RotResolution);
            }
            return rotMaskSprite;
        }

        private static Sprite RotInvertedSprite()
        {
            if (rotInvertedSprite == null)
            {
                rotInvertedSprite = BakeAlpha(RotField(true), RotResolution);
            }
            return rotInvertedSprite;
        }

        /// <summary>
        /// The order a cube is eaten in, written as alpha: highest in the middle, lowest in the
        /// corners, broken up by noise so the front is ragged rather than a growing circle.
        ///
        /// It is EQUALISED: each raw value is replaced by its rank, so every step of alpha covers
        /// the same share of the cell. That is what lets a straight-line cutoff eat area in a
        /// straight line, and what makes RotFrontWidth an honest share of the cube.
        ///
        /// The inverted copy feeds the mask that hides what is NOT eaten yet. It has to be this
        /// same field turned over, or the rot band would open a gap against the front it lights.
        /// Kept inside 0.02-0.98, so a cutoff of 1 masks nothing and 0 masks everything.
        /// </summary>
        private static float[] RotField(bool inverted)
        {
            int n = RotResolution;
            var order = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(u * u + v * v) / 1.4142f;
                    float grain = Noise(u * 2.6f + 1.7f, v * 2.6f) * 0.65f
                        + Noise(u * 6.3f, v * 6.3f + 4.1f) * 0.35f;
                    order[y * n + x] = (1f - d) * 0.78f + (grain * 0.5f + 0.5f) * 0.22f;
                }
            }
            var rank = new int[order.Length];
            for (int i = 0; i < rank.Length; i++)
            {
                rank[i] = i;
            }
            var keys = (float[])order.Clone();
            System.Array.Sort(keys, rank);
            var a = new float[order.Length];
            for (int r = 0; r < rank.Length; r++)
            {
                float m = 0.02f + 0.96f * r / (rank.Length - 1);
                a[rank[r]] = inverted ? 1f - m : m;
            }
            return a;
        }

        /// <summary>The rot itself - only ever seen through the band the masks leave. Mottled, so the
        /// crack of light is alive rather than a drawn line, and faded before the sprite's own edge
        /// so the band can never trace the square it sits in.</summary>
        private static Sprite RotBandSprite()
        {
            if (rotBandSprite != null)
            {
                return rotBandSprite;
            }
            int n = RotResolution;
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float mottle = 0.7f + 0.3f * (Noise(u * 7.3f + 3.3f, v * 7.3f) * 0.5f + 0.5f);
                    float border = 1f - Edge(0.78f, 0.98f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    a[y * n + x] = mottle * border;
                }
            }
            rotBandSprite = BakeAlpha(a, n);
            return rotBandSprite;
        }

        /// <summary>
        /// The vessels: branching, tapering lines growing out of the cube's middle.
        ///
        /// Grown, not noise. Ridged noise draws contour lines, and contour lines read as a marble
        /// texture printed on the tile; a few wandering branches that fork and thin toward their
        /// tips read as something alive running THROUGH it. Each line has a dark halo baked round
        /// it, so it stays legible on a pale tile and a dark one alike.
        /// </summary>
        private static Sprite VeinSprite(int variant)
        {
            int which = ((variant % veinSprites.Length) + veinSprites.Length) % veinSprites.Length;
            if (veinSprites[which] != null)
            {
                return veinSprites[which];
            }
            const int n = 128;
            var rng = new System.Random(7919 + which * 104729);
            var segments = new List<Vector4>();
            var widths = new List<Vector2>();
            const int Branches = 5;
            float turn = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int b = 0; b < Branches; b++)
            {
                float heading = turn + b * Mathf.PI * 2f / Branches
                    + ((float)rng.NextDouble() - 0.5f) * 0.7f;
                Grow(rng, segments, widths, Vector2.zero, heading, 7, 0.13f, 0.055f, 0.012f, true);
            }

            var core = new float[n * n];
            var halo = new float[n * n];
            float aa = 2f / n;
            for (int s = 0; s < segments.Count; s++)
            {
                var a = new Vector2(segments[s].x, segments[s].y);
                var b = new Vector2(segments[s].z, segments[s].w);
                float reach = Mathf.Max(widths[s].x, widths[s].y) * 2.2f + aa * 2f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(((Mathf.Min(a.x, b.x) - reach) + 1f) * 0.5f * n));
                int x1 = Mathf.Min(n - 1, Mathf.CeilToInt(((Mathf.Max(a.x, b.x) + reach) + 1f) * 0.5f * n));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(((Mathf.Min(a.y, b.y) - reach) + 1f) * 0.5f * n));
                int y1 = Mathf.Min(n - 1, Mathf.CeilToInt(((Mathf.Max(a.y, b.y) + reach) + 1f) * 0.5f * n));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2((x + 0.5f) / n * 2f - 1f, (y + 0.5f) / n * 2f - 1f);
                        float along;
                        float dist = SegmentDistance(p, a, b, out along);
                        float w = Mathf.Lerp(widths[s].x, widths[s].y, along);
                        int i = y * n + x;
                        core[i] = Mathf.Max(core[i], 1f - Edge(w - aa, w + aa, dist));
                        halo[i] = Mathf.Max(halo[i], 1f - Edge(w * 2.2f - aa, w * 2.2f + aa * 2f, dist));
                    }
                }
            }

            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    int i = y * n + x;
                    // A seed of light where the vessels meet - the core they grew out of.
                    float c = Mathf.Max(core[i], Mathf.Exp(-(u * u + v * v) * 30f));
                    float h = halo[i] * 0.55f;
                    float frame = 1f - Edge(0.82f, 0.98f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    float alpha = (c + (1f - c) * h) * frame;
                    float rgb = alpha > 0.0001f ? (c + (1f - c) * h * 0.12f) / (c + (1f - c) * h) : 1f;
                    byte level = (byte)Mathf.RoundToInt(Mathf.Clamp01(rgb) * 255f);
                    px[i] = new Color32(level, level, level,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            veinSprites[which] = Bake(px, n);
            return veinSprites[which];
        }

        /// <summary>One wandering branch, forking once near its root when it is a main one.</summary>
        private static void Grow(System.Random rng, List<Vector4> segments, List<Vector2> widths,
            Vector2 at, float heading, int count, float length, float rootWidth, float tipWidth,
            bool fork)
        {
            for (int i = 0; i < count; i++)
            {
                heading += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                Vector2 next = at + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * length;
                float w0 = Mathf.Lerp(rootWidth, tipWidth, (float)i / count);
                float w1 = Mathf.Lerp(rootWidth, tipWidth, (float)(i + 1) / count);
                segments.Add(new Vector4(at.x, at.y, next.x, next.y));
                widths.Add(new Vector2(w0, w1));
                if (fork && i == 2)
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    Grow(rng, segments, widths, next,
                        heading + side * (0.6f + (float)rng.NextDouble() * 0.4f),
                        4, length * 0.8f, w1 * 0.8f, tipWidth, false);
                }
                at = next;
            }
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float along)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float len2 = Vector2.Dot(ba, ba);
            along = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(pa, ba) / len2) : 0f;
            return (pa - ba * along).magnitude;
        }

        /// <summary>
        /// The rupture: a lumpy cloud, and deliberately not a star.
        ///
        /// Its outline is pushed in and out by noise sampled at each point's POSITION. An outline
        /// driven by the ANGLE around the centre is a shape with some number of points however it
        /// is tuned - which is a starburst, and a starburst is an explosion.
        /// </summary>
        private static Sprite PuffSprite()
        {
            if (puffSprite != null)
            {
                return puffSprite;
            }
            const int n = 128;
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float lump = Noise(u * 1.6f + 2.3f, v * 1.6f + 7.1f) * 0.7f
                        + Noise(u * 3.4f, v * 3.4f + 1.9f) * 0.3f;
                    float edge = 0.62f + lump * Style.PuffRagged;
                    float body = 1f - Edge(edge * 0.30f, edge, r);
                    float mottle = 0.78f + 0.22f * Noise(u * 5.2f + 9f, v * 5.2f);
                    a[y * n + x] = Mathf.Clamp01(body * mottle) * (1f - Edge(0.92f, 1f, r));
                }
            }
            puffSprite = BakeAlpha(a, n);
            return puffSprite;
        }

        private static Sprite SporeSprite()
        {
            if (sporeSprite != null)
            {
                return sporeSprite;
            }
            const int n = 32;
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    a[y * n + x] = Mathf.Exp(-r * r * 5.5f) * Mathf.Clamp01(1f - r);
                }
            }
            sporeSprite = BakeAlpha(a, n);
            return sporeSprite;
        }

        private static Sprite StainSprite()
        {
            if (stainSprite != null)
            {
                return stainSprite;
            }
            const int n = 96;
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float blotch = Noise(u * 2.4f + 5f, v * 2.4f) * 0.5f + 0.5f;
                    float r = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    a[y * n + x] = (1f - Edge(0.4f, 0.95f, r)) * (0.35f + 0.65f * blotch);
                }
            }
            stainSprite = BakeAlpha(a, n);
            return stainSprite;
        }

        private static float Hash(float x, float y)
        {
            float h = Mathf.Sin(x * 127.31f + y * 311.7f) * 43758.545f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Value noise in -1..1.</summary>
        private static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x);
            float iy = Mathf.Floor(y);
            float fx = x - ix;
            float fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy);
            float b = Hash(ix + 1f, iy);
            float c = Hash(ix, iy + 1f);
            float d = Hash(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy) * 2f - 1f;
        }

        private static Sprite BakeAlpha(float[] alpha, int n)
        {
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                px[i] = new Color32(255, 255, 255,
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha[i]) * 255f));
            }
            return Bake(px, n);
        }

        /// <summary>FullRect, not Tight: a tight mesh trims the transparent border, and for the
        /// mask that border is data - the corners are where the rot arrives last.</summary>
        private static Sprite Bake(Color32[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0,
                SpriteMeshType.FullRect);
        }
    }
}
