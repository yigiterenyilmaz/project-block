// PURPOSE: PRESSURE VESSEL FAILURE - "Hidrolik pres" with nowhere to open. Its own event, with its
// own identity, because this is the ONE thing in the game that removes gold and obsidian, and it
// pays nothing. A generic TNT blast would say "the press exploded and took some blocks with it",
// which is the wrong sentence: what happened is that a sealed vessel could not vent, and the
// pressure it had been holding for four turns came out of it sideways.
//
// THE STORY, in order, and every beat of it is the opposite of an explosion:
//   OVERPRESSURE  the quadrant seams darken, the central dimple is crushed in, the contact shadow
//                 goes unnaturally tight, and a dull burnt amber stress comes up on the shell. The
//                 danger colour is MUTED INDUSTRIAL AMBER - never a neon red alarm.
//   STRAIN        the four edges are pulled a pixel or two out while the middle stays tight. Not a
//                 balloon: this is metal, so there is no 20% scale-up anywhere in it.
//   INWARD CRUSH  and then the shell does NOT burst outward - it collapses to the middle, hard,
//                 in under a tenth of a second. That inversion is the whole identity of the
//                 effect; get it the TNT way round and nothing else here matters.
//   KNOT          a very short dense slate-and-amber pressure knot at the centre. Mechanical
//                 pressure wound up, never a magic orb.
//   BURST         a short overpressure front across the board plane, shaped to the GRID: a rounded
//                 square with a cross influence, not a circle, and reaching only as far as the
//                 cells CORE actually emptied - the footprint is the report's, never a radius of
//                 this view's choosing.
//   SHEAR         the gold and obsidian around it do not fracture and do not spray shards. The
//                 pressure front reaches them and CRUSHES each one flat: a thin lamina in its own
//                 material (gold an amber-gold, obsidian a deep violet), carried a fifth of a cell
//                 along the front, drained, and gone. "The system failed so hard it flattened
//                 material that normally cannot be moved" - which is why no reward reads here.
//   RESIDUE       slate pressure haze, a dark stress imprint on the cells, and a dozen tiny
//                 metal-like dust flecks. Gone in a third of a second.
//
// No white flash, no full-screen overlay, no smoke cloud, no gold coins, no score pop, no confetti,
// no screen shake, no square debris. The press's own shell is not sheared - it is what collapsed.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The press failing: it could not open, so it came in on itself and took the stone
    /// around it.</summary>
    public sealed class PressureVesselView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ---- Y) overpressure build ----
            public static float BuildDuration = 0.14f;

            /// <summary>How far the seams darken and the dimple is driven in.</summary>
            public static float StressStrength = 1f;

            /// <summary>How far the shell goes toward burnt amber at the peak of the build.</summary>
            public static float AmberStrength = 0.9f;

            // ---- Z) shell strain ----
            /// <summary>How far each edge is pulled out, in cells. One or two pixels - this is
            /// metal.</summary>
            public static float StrainAmount = 0.028f;

            // ---- AA) the inward crush ----
            public static float InwardCollapseDuration = 0.1f;

            /// <summary>How far the edges travel toward the middle. The middle itself holds.
            /// </summary>
            public static float CollapseStrength = 0.8f;

            // ---- AB) the knot ----
            public static float KnotHold = 0.035f;

            public static float KnotSize = 0.34f;

            // ---- AC/AD/AE) the burst ----
            public static float BurstDuration = 0.17f;

            /// <summary>How square the front is: 0 a circle, 1 a square. The grid's own language.
            /// </summary>
            public static float BurstSquareness = 0.68f;

            /// <summary>How far the cross arms reach past the rounded square.</summary>
            public static float BurstCross = 0.22f;

            public static float BurstStrength = 0.85f;

            /// <summary>How far past the affected cells the front may show at all. Local: the
            /// failure never washes the board.</summary>
            public static float BurstOvershoot = 0.35f;

            // ---- AG/AH) the indestructible shear ----
            public static float ShearDuration = 0.22f;

            /// <summary>How thin a sheared cube is crushed, as a share of a cube.</summary>
            public static float ShearThickness = 0.16f;

            /// <summary>How far it is carried along the front, in cells. A fifth, not a flight.
            /// </summary>
            public static float ShearTravel = 0.3f;

            public static float ShearDesaturation = 0.6f;

            // ---- AK/AL) residue ----
            public static int FleckCount = 12;

            public static float FleckLifetime = 0.26f;

            public static float ResidueStrength = 0.3f;

            public static float ResidueDuration = 0.24f;
        }

        public static class Layers
        {
            public static bool ShowBuild = true;

            public static bool ShowCollapse = true;

            public static bool ShowBurst = true;

            public static bool ShowShear = true;

            public static bool ShowResidue = true;

            public static bool ShowFlecks = true;

            public static void AllOn()
            {
                ShowBuild = true;
                ShowCollapse = true;
                ShowBurst = true;
                ShowShear = true;
                ShowResidue = true;
                ShowFlecks = true;
            }
        }

        /// <summary>GOLD crushed flat is an amber-gold lamina; OBSIDIAN a deep violet one. They do
        /// not look alike and this is the one event that removes either, so the difference has to
        /// survive the shear.</summary>
        private static readonly Color GoldLamina = new Color(0.78f, 0.6f, 0.26f);

        private static readonly Color ObsidianLamina = new Color(0.29f, 0.21f, 0.38f);

        /// <summary>The burst's own palette: dark slate out, muted steel in the middle band, burnt
        /// amber as the accent, a short pale WARM STEEL at the centre. Never white.</summary>
        private static readonly Color BurstOuter = new Color(0.2f, 0.22f, 0.27f);

        private static readonly Color BurstMid = new Color(0.44f, 0.48f, 0.55f);

        private static readonly Color BurstCore = new Color(0.78f, 0.72f, 0.62f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        /// <summary>
        /// The overpressure FRONT: a rounded-square ring with a cross influence, generated once.
        ///
        /// It has to be a ring rather than a filled plate. A filled one grows into a pale wash over
        /// the cells, which reads as a screen effect - the thing this whole event is not. A front is
        /// an EDGE arriving somewhere, and the cells it has already passed are behind it.
        /// </summary>
        private static Sprite frontSprite;

        private const int FrontPixels = 128;

        private static Sprite FrontSprite()
        {
            if (frontSprite != null)
            {
                return frontSprite;
            }
            var tex = new Texture2D(FrontPixels, FrontPixels, TextureFormat.RGBA32, false);
            var pixels = new Color32[FrontPixels * FrontPixels];
            float half = FrontPixels * 0.5f;
            for (int y = 0; y < FrontPixels; y++)
            {
                for (int x = 0; x < FrontPixels; x++)
                {
                    // -1..1 across the texture.
                    float fx = (x + 0.5f - half) / half;
                    float fy = (y + 0.5f - half) / half;
                    // A rounded square: the Chebyshev distance softened toward the Euclidean one,
                    // which is the grid's shape without the hard corners; the cross influence pulls
                    // the arms a little further along the axes.
                    float square = Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fy));
                    float round = Mathf.Sqrt(fx * fx + fy * fy);
                    float d = Mathf.Lerp(round, square, Style.BurstSquareness);
                    float cross = 1f - Style.BurstCross
                        * (1f - Mathf.Min(Mathf.Abs(fx), Mathf.Abs(fy))
                            / Mathf.Max(Mathf.Max(Mathf.Abs(fx), Mathf.Abs(fy)), 1e-4f));
                    d *= cross;
                    // The ring itself: a band just inside the edge, soft on both sides.
                    float band = 1f - Mathf.Clamp01(Mathf.Abs(d - 0.82f) / 0.2f);
                    band *= band;
                    // And the faintest fill behind it, so the front has a wake rather than a hole.
                    float wake = Mathf.Clamp01(1f - d / 0.82f) * 0.22f;
                    float a = Mathf.Clamp01(band + wake) * (d <= 1f ? 1f : 0f);
                    pixels[y * FrontPixels + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            frontSprite = Sprite.Create(tex, new Rect(0, 0, FrontPixels, FrontPixels),
                new Vector2(0.5f, 0.5f), FrontPixels, 0, SpriteMeshType.FullRect);
            return frontSprite;
        }

        private const int ResidueOrder = 3;

        private const int BurstOrder = 7;

        private const int ShellOrder = 8;

        private const int ShearOrder = 9;

        private const int KnotOrder = 10;

        private const int FleckOrder = 11;

        /// <summary>One cube the failure took: where it stood, what it was, and its own face.
        /// Straight from PressReleaseVisuals.DetonatedCells / DetonatedKinds.</summary>
        public struct Casualty
        {
            public Vector2 Centre;
            public CubeKind Kind;
            public ClusterBurstView.Look Look;
        }

        /// <summary>The failure to play.</summary>
        public sealed class Scene
        {
            public Vector2 Centre;
            public Color Slate;
            public float CellSize;
            public float CubeSize;
            public readonly List<Casualty> Casualties = new List<Casualty>();
            public Color[] Memory = new Color[4];
        }

        private sealed class Shear
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Imprint;
            public Vector2 At;
            public Vector2 Out;
            public Color Lamina;
            public ClusterBurstView.Look Look;
            /// <summary>When the front reaches it - its own distance from the vessel, so the
            /// nearest stone goes first.</summary>
            public float Reached;
        }

        private sealed class Run
        {
            public float Clock;
            public float End;
            public float Cell;
            public float Cube;
            public Color Slate;
            public Vector2 Centre;
            public float Reach;
            public SpriteRenderer Shell;
            public SpriteRenderer Knot;
            public SpriteRenderer Burst;
            public readonly List<Shear> Shears = new List<Shear>();
            public readonly List<SpriteRenderer> Flecks = new List<SpriteRenderer>();
            public readonly List<Vector2> FleckDirs = new List<Vector2>();
            public Color[] Memory = new Color[4];
        }

        private readonly List<Run> runs = new List<Run>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private MaterialPropertyBlock block;

        private Material plainMaterial;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        public bool Busy
        {
            get { return runs.Count > 0; }
        }

        // =================================================================== driving it

        /// <summary>
        /// Plays the failure. <paramref name="scene"/>.Casualties is exactly what the rules
        /// emptied - the press's own cell included, which is the one that collapses rather than
        /// shearing. The burst's footprint is taken from how far those cells actually reach.
        /// </summary>
        public void Play(Scene scene)
        {
            if (scene == null || scene.CellSize <= 0f)
            {
                return;
            }
            var run = new Run
            {
                Cell = scene.CellSize,
                Cube = scene.CubeSize > 0f ? scene.CubeSize : scene.CellSize,
                Slate = scene.Slate,
                Centre = scene.Centre,
                Memory = scene.Memory
            };
            Material shell = CompressedCubeView.ShellMaterial();
            if (shell != null && Layers.ShowBuild)
            {
                run.Shell = Rent(ViewUtil.RoundedSprite, ShellOrder, shell);
            }
            // THE FOOTPRINT IS THE REPORT'S. The farthest cell the rules emptied is how far the
            // front goes, plus a little overshoot - and not one cell more.
            float reach = run.Cube * 0.5f;
            for (int i = 0; i < scene.Casualties.Count; i++)
            {
                float d = (scene.Casualties[i].Centre - scene.Centre).magnitude;
                if (d > reach)
                {
                    reach = d;
                }
            }
            run.Reach = reach + run.Cell * Style.BurstOvershoot;
            if (Layers.ShowBurst)
            {
                run.Burst = Rent(FrontSprite(), BurstOrder, null);
            }
            run.Knot = Rent(ViewUtil.RoundedSprite, KnotOrder, null);
            for (int i = 0; i < scene.Casualties.Count; i++)
            {
                Casualty c = scene.Casualties[i];
                if (c.Kind == CubeKind.Compressed)
                {
                    continue; // the vessel itself: it collapses, it is not sheared
                }
                if (!Layers.ShowShear)
                {
                    continue;
                }
                Vector2 away = c.Centre - scene.Centre;
                var s = new Shear
                {
                    At = c.Centre,
                    Out = away.sqrMagnitude > 1e-6f ? away.normalized : Vector2.up,
                    Look = c.Look,
                    Lamina = c.Kind == CubeKind.Gold ? GoldLamina : ObsidianLamina
                };
                // It is crushed when the front gets to it, not on a schedule of its own.
                s.Reached = run.Reach > 0f
                    ? Mathf.Clamp01(away.magnitude / run.Reach) * Style.BurstDuration
                    : 0f;
                s.Renderer = Rent(c.Look.Tile != null ? c.Look.Tile : ViewUtil.RoundedSprite,
                    ShearOrder, LaminaFor(c.Look));
                if (Layers.ShowResidue)
                {
                    s.Imprint = Rent(ViewUtil.RoundedSprite, ResidueOrder, null);
                }
                run.Shears.Add(s);
            }
            if (Layers.ShowFlecks)
            {
                int n = Mathf.Clamp(Style.FleckCount, 0, 16);
                for (int i = 0; i < n; i++)
                {
                    run.Flecks.Add(Rent(ViewUtil.RoundedSprite, FleckOrder, null));
                    // Irregular slivers in a spread, never square confetti and never a full ring.
                    float a = (i * 2.39996f) % (Mathf.PI * 2f);
                    run.FleckDirs.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
                }
            }
            run.End = Style.BuildDuration + Style.InwardCollapseDuration + Style.KnotHold
                + Style.BurstDuration + Style.ShearDuration + Style.ResidueDuration;
            runs.Add(run);
            Paint(run);
        }

        public void Stop()
        {
            for (int i = 0; i < runs.Count; i++)
            {
                Release(runs[i]);
            }
            runs.Clear();
        }

        private void Update()
        {
            if (runs.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                Run run = runs[i];
                run.Clock += dt;
                Paint(run);
                if (run.Clock >= run.End)
                {
                    Release(run);
                    runs.RemoveAt(i);
                }
            }
        }

        private static float Span(float t, float a, float b)
        {
            if (b <= a)
            {
                return t >= b ? 1f : 0f;
            }
            return Mathf.Clamp01((t - a) / (b - a));
        }

        private static float Ease(float k)
        {
            return k * k * (3f - 2f * k);
        }

        private void Paint(Run run)
        {
            float t = run.Clock;
            float buildEnd = Style.BuildDuration;
            float crushEnd = buildEnd + Style.InwardCollapseDuration;
            float knotEnd = crushEnd + Style.KnotHold;
            float burstEnd = knotEnd + Style.BurstDuration;

            float build = Span(t, 0f, buildEnd);
            float crush = Span(t, buildEnd, crushEnd);
            float knot = Span(t, crushEnd, knotEnd);
            float burst = Span(t, knotEnd, burstEnd);
            float residue = Span(t, burstEnd, run.End);

            // ---- the vessel: stressed, strained, and then in on itself ----
            if (run.Shell != null)
            {
                float strain = Ease(build) * Style.StrainAmount * run.Cell;
                CompressedCubeView.ShellLook look =
                    CompressedCubeView.ShellLook.Rest(run.Slate);
                look.Q0 = run.Memory[0];
                look.Q1 = run.Memory[1];
                look.Q2 = run.Memory[2];
                look.Q3 = run.Memory[3];
                look.Stress = Style.StressStrength * Ease(build) * Style.AmberStrength;
                // The dimple is driven in and the seams darken: pressure that cannot get out.
                look.DimpleDepth *= 1f + 0.7f * Ease(build);
                look.DimpleRadius *= 1f - 0.3f * Ease(build);
                look.SeamDepth *= 1f + 0.8f * Ease(build);
                // EVERY EDGE OUT while the middle holds - and then the inversion.
                look.EdgeLead = new Vector4(strain, strain, strain, strain) * (1f - crush);
                look.Collapse = Layers.ShowCollapse
                    ? Ease(crush) * Style.CollapseStrength * run.Cell * 0.5f
                    : 0f;
                look.Alpha = 1f - Ease(Span(t, crushEnd - 0.02f, knotEnd));
                run.Shell.transform.localPosition =
                    new Vector3(run.Centre.x, run.Centre.y, 0f);
                float size = run.Cube * (1f - 0.04f * Ease(build));
                Fit(run.Shell, size, size);
                CompressedCubeView.ApplyShell(run.Shell, block, look);
            }

            // ---- the knot: wound-up mechanical pressure, held for a breath ----
            if (run.Knot != null)
            {
                float on = knot > 0f && burst < 1f
                    ? Mathf.Min(Ease(knot), 1f - Ease(burst * 1.6f))
                    : 0f;
                float s = run.Cube * Style.KnotSize * (0.7f + 0.3f * Ease(knot));
                run.Knot.transform.localPosition = new Vector3(run.Centre.x, run.Centre.y, 0f);
                Fit(run.Knot, s, s);
                Color c = Color.Lerp(run.Slate, CompressedCubeView.PressureAmber, 0.55f);
                c.a = Mathf.Clamp01(on) * 0.95f;
                run.Knot.color = c;
            }

            // ---- the burst: a grid-shaped pressure front, only as far as the report ----
            if (run.Burst != null)
            {
                float k = Ease(burst);
                float size = run.Reach * 2f * k;
                run.Burst.transform.localPosition = new Vector3(run.Centre.x, run.Centre.y, 0f);
                // The front's own shape is baked into its sprite (a rounded square with a cross
                // influence); all this does is grow it to the reach the report earned.
                Fit(run.Burst, size, size);
                // Out dark, a steel band through the middle of the travel, a short warm steel
                // centre at the start. Nothing is ever taken to white.
                Color c = k < 0.35f
                    ? Color.Lerp(BurstCore, BurstMid, k / 0.35f)
                    : Color.Lerp(BurstMid, BurstOuter, (k - 0.35f) / 0.65f);
                c = Color.Lerp(c, CompressedCubeView.PressureAmber, 0.25f * (1f - k));
                c.a = Style.BurstStrength * Mathf.Sin(Mathf.Clamp01(burst) * Mathf.PI) * 0.6f;
                run.Burst.color = c;
            }

            // ---- the stone: crushed flat where it stands, carried a little, drained, gone ----
            for (int i = 0; i < run.Shears.Count; i++)
            {
                Shear s = run.Shears[i];
                float mine = Mathf.Max(0f, t - (knotEnd + s.Reached));
                float flat = Span(mine, 0f, Style.ShearDuration * 0.35f);
                float go = Span(mine, Style.ShearDuration * 0.3f, Style.ShearDuration);
                Vector2 at = s.At + s.Out * (Ease(go) * Style.ShearTravel * run.Cell);
                if (s.Renderer != null)
                {
                    s.Renderer.transform.localPosition = new Vector3(at.x, at.y, 0f);
                    float thin = Mathf.Lerp(1f, Style.ShearThickness, Ease(flat));
                    Fit(s.Renderer, run.Cube * (1f + 0.08f * Ease(flat)), run.Cube * thin);
                    Color tint = s.Look.Colour;
                    tint.a = 1f - Ease(go);
                    s.Renderer.color = tint;
                    if (HydraulicPressView.LaminaMaterial() != null)
                    {
                        block.Clear();
                        block.SetColor(Shader.PropertyToID("_Average"), s.Lamina);
                        block.SetFloat(Shader.PropertyToID("_Flatten"), Ease(flat));
                        block.SetFloat(Shader.PropertyToID("_Relief"), 0f);
                        block.SetFloat(Shader.PropertyToID("_Seam"), 0.4f * Ease(flat));
                        block.SetColor(Shader.PropertyToID("_SeamColour"),
                            CompressedCubeView.PressureAmber);
                        block.SetVector(Shader.PropertyToID("_Tone"),
                            new Vector2(Style.ShearDesaturation * go, 0.25f * go));
                        block.SetFloat(Shader.PropertyToID("_Negative"), 0f);
                        block.SetColor(Shader.PropertyToID("_NegativeColour"), BurstOuter);
                        block.SetFloat(Shader.PropertyToID("_Press"), Ease(flat));
                        block.SetFloat(Shader.PropertyToID("_FaceHalf"),
                            CompressedCubeView.FaceHalfOf(s.Renderer));
                        s.Renderer.SetPropertyBlock(block);
                    }
                }
                if (s.Imprint != null)
                {
                    // A dark stress imprint where it stood, and then the cell is simply empty.
                    s.Imprint.transform.localPosition = new Vector3(s.At.x, s.At.y, 0f);
                    Fit(s.Imprint, run.Cube * 0.8f, run.Cube * 0.8f);
                    float fade = 1f - Ease(residue);
                    s.Imprint.color = new Color(0f, 0f, 0f,
                        Style.ResidueStrength * Ease(go) * fade);
                }
            }

            // ---- a dozen tiny metal-like flecks, and the haze going out ----
            float fleck = Span(t, knotEnd, knotEnd + Style.FleckLifetime);
            for (int i = 0; i < run.Flecks.Count; i++)
            {
                SpriteRenderer f = run.Flecks[i];
                if (f == null)
                {
                    continue;
                }
                Vector2 dir = run.FleckDirs[i];
                // Irregular: each one on its own reach, so they never read as a ring.
                float spread = 0.4f + 0.6f * ((i * 37 % 13) / 13f);
                Vector2 at = run.Centre + dir * (Ease(fleck) * run.Reach * spread);
                f.transform.localPosition = new Vector3(at.x, at.y, 0f);
                float len = run.Cell * 0.075f * (1f - fleck);
                Fit(f, len * 2.6f, len * 0.7f);
                f.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                Color c = Color.Lerp(BurstMid, CompressedCubeView.PressureAmber, 0.35f);
                c.a = 0.7f * (1f - fleck) * (fleck > 0f ? 1f : 0f);
                f.color = c;
            }
        }

        private Material LaminaFor(ClusterBurstView.Look look)
        {
            Material lamina = HydraulicPressView.LaminaMaterial();
            return lamina != null ? lamina : ViewUtil.TileMaterial(look.Tile);
        }

        private static void Fit(SpriteRenderer r, float width, float height)
        {
            if (r == null || r.sprite == null)
            {
                return;
            }
            Vector2 unit = r.sprite.bounds.size;
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("Vessel");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
                if (plainMaterial == null)
                {
                    plainMaterial = r.sharedMaterial;
                }
            }
            Material wanted = material != null ? material : plainMaterial;
            if (wanted != null && r.sharedMaterial != wanted)
            {
                r.sharedMaterial = wanted;
            }
            r.SetPropertyBlock(null);
            r.maskInteraction = SpriteMaskInteraction.None;
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Clear;
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = Vector3.one;
            r.enabled = true;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.enabled = false;
            r.SetPropertyBlock(null);
            spare.Push(r);
        }

        private void Release(Run run)
        {
            Return(run.Shell);
            Return(run.Knot);
            Return(run.Burst);
            for (int i = 0; i < run.Shears.Count; i++)
            {
                Return(run.Shears[i].Renderer);
                Return(run.Shears[i].Imprint);
            }
            for (int i = 0; i < run.Flecks.Count; i++)
            {
                Return(run.Flecks[i]);
            }
            run.Shears.Clear();
            run.Flecks.Clear();
            run.FleckDirs.Clear();
        }
    }
}
