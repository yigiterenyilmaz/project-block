// PURPOSE: The compressed cube's own PRESENCE - "Hidrolik pres" sitting on the board with four
// cells shut inside it. Owned by BoardView like the rot and the snake are, because it has to outlive
// every repaint: a press stands for four turns and the board is repainted many times in them.
//
// It is a LAYER OVER the board's own slate cube, not a replacement for it - the same bargain
// GangreneView makes with a rotten cube. The plate wears the cell's own colour
// (ViewUtil.CubeMaterialColor for CubeKind.Compressed, the hard industrial slate the designer
// chose), so if the shader is missing the board underneath is still a readable pressed block and
// nothing has vanished.
//
// WHAT IT SAYS, and nothing else:
//   IDLE          CONTAINED PRESSURE. Not breathing - the cube is not alive and must never wobble.
//                 Every couple of seconds the central dimple contracts, the quadrant seams tighten,
//                 the contact shadow draws in a percent or two and the surface highlight sharpens
//                 for a moment, and it all goes back. An object holding something in.
//   COUNTDOWN     PRESSURE AGEING, not a counter. The turn a press is on is said by the STATE OF
//                 ITS MATERIAL: the central dimple bites deeper, the quadrant seams tighten and
//                 darken, the outer bevel takes compression, the contact shadow draws in and gets
//                 heavier, and one more short PRESSURE SCAR - a tapered crease running out of the
//                 cross into a quadrant - locks each turn. Four orange dots is what this replaces:
//                 a cooldown LED strip, which said "count the lights" instead of "this thing is
//                 holding a lot of pressure now". The number still comes from the POWER
//                 (TurnsLeft / TurnsCompressed) and is never counted here.
//   TICK          a turn does not advance in one frame: a ~180ms PRESSURE TICK loads the shell
//                 (its four edges press inward a pixel), tightens the dimple, contracts the seams,
//                 locks the new scar with a brief burnt-amber strain running along it, tightens the
//                 shadow, and settles. No bounce, no scale-up, no particles.
//   LAST TURN     everything is at its tightest, the idle cycle runs faster, and a very subtle
//                 uneven surface strain says the shell is working. Still slate, still calm, still
//                 heavy - it never becomes an orange cube. No flash, no shake.
//   MEMORY        the stored quadrants' colours, available under the seams for the release to wake
//                 for a moment. In idle it is effectively off.
//
// It draws no release, no failure and no compression: those are HydraulicPressView's and
// PressureVesselView's, and while one of them owns a cell this view is told to stand off it
// (Suppress) so two things never draw the same shell.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The compressed cube standing on the board: its surface, its idle pressure and its
    /// turn countdown.</summary>
    public sealed class CompressedCubeView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how a shut press READS. Times in seconds.</summary>
        public static class Style
        {
            /// <summary>How often the contained pressure cycles. Slow, so it never reads as a pulse.
            /// </summary>
            public static float IdlePressurePeriod = 2.7f;

            /// <summary>How much of the period the contraction itself takes.</summary>
            public static float IdlePressureShare = 0.26f;

            /// <summary>How far the central dimple contracts on a cycle.</summary>
            public static float IdleDimpleStrength = 0.22f;

            /// <summary>How far the quadrant seams tighten with it.</summary>
            public static float IdleSeamStrength = 0.18f;

            /// <summary>How far the contact shadow draws in (0.02 = 2%).</summary>
            public static float IdleShadowTightening = 0.02f;

            /// <summary>How much the surface highlight sharpens at the peak.</summary>
            public static float IdleGlossLift = 0.1f;

            // ---- PRESSURE AGEING: what one more turn under load does to the shell ----
            /// <summary>How deep the central dimple sits on the FIRST turn, and how much deeper
            /// each turn after it drives the shell in.</summary>
            public static float WaitDimpleBase = 0.22f;

            public static float WaitDimplePerTurn = 0.055f;

            /// <summary>The quadrant seams: how they sit when it has just locked, and how much
            /// tighter and darker each further turn pulls them.</summary>
            public static float WaitSeamBase = 0.13f;

            public static float WaitSeamPerTurn = 0.035f;

            /// <summary>How much compression the outer bevel takes per turn.</summary>
            public static float WaitBevelPerTurn = 0.022f;

            /// <summary>The contact shadow: it draws IN and gets HEAVIER as the pressure builds,
            /// so the capsule sits harder on the board the longer it is shut.</summary>
            public static float WaitShadowTightBase = 0f;

            public static float WaitShadowTightPerTurn = 0.022f;

            public static float WaitShadowDarkPerTurn = 0.05f;

            /// <summary>A locked pressure scar: how deep the crease is cut, where along the
            /// quadrant diagonal it runs, and how wide it starts before it tapers away. FACE units
            /// (-1..1 across the plate).</summary>
            public static float ScarDepth = 0.5f;

            public static float ScarInner = 0.2f;

            public static float ScarOuter = 0.62f;

            /// <summary>Half-width in FACE units, so the crease is 2x this share of the cube:
            /// 0.05 is about 3px wide on a 64px cube. A crease, not a wedge - too wide and the four
            /// of them merge into a star across the middle of the face.</summary>
            public static float ScarHalfWidth = 0.05f;

            /// <summary>The burnt-amber strain that runs out along a scar as it locks - an ACCENT
            /// on the moment, never the countdown itself. It is gone almost at once.</summary>
            public static float ScarGlintDuration = 0.14f;

            /// <summary>The PRESSURE TICK: how long a turn takes to arrive, how far the shell's
            /// edges press inward while it does, and how much extra the dimple and seams give.
            /// </summary>
            public static float TickDuration = 0.18f;

            public static float TickEdgeInset = 0.016f;

            public static float TickDimpleStrength = 0.35f;

            public static float TickSeamStrength = 0.3f;

            public static float TickSettle = 0.008f;

            /// <summary>How often the contained pressure cycles on each turn. It quickens as the
            /// release comes on - the capsule is nearer its limit, not more alive.</summary>
            public static readonly float[] IdleCyclePerTurn = { 3.1f, 2.8f, 2.4f, 1.8f };

            /// <summary>And how hard that cycle pushes, per turn.</summary>
            public static readonly float[] IdleStrengthPerTurn = { 0.7f, 0.85f, 1f, 1.25f };

            /// <summary>What the final turn leaves standing: a very subtle warm residue in the
            /// creases, and an uneven surface strain that says the shell is working.</summary>
            public static float FinalStressResidue = 0.12f;

            public static float FinalSurfaceStrain = 0.5f;

            /// <summary>Seam width at rest, as the shader reads it. Its DEPTH comes from the
            /// wait model above, because that is one of the things a turn changes.</summary>
            public static float SeamDepth = 0.15f;

            /// <summary>Half-width of a seam in FACE units (-1..1 across the plate), so 0.022 is a
            /// line about 4% of the cube wide. Thin: a heavy cross turns the face into a window
            /// frame, and with the dimple and the four marks the whole thing reads as a die.
            /// </summary>
            public static float SeamWidth = 0.022f;

            /// <summary>Dimple depth and radius at rest.</summary>
            public static float DimpleDepth = 0.26f;

            public static float DimpleRadius = 0.17f;

            /// <summary>Corner compression at rest.</summary>
            public static float CornerCompression = 0.13f;

            /// <summary>The matte steel response. Low: this is not a gloss pass.</summary>
            public static float Gloss = 0.15f;

            /// <summary>The contact shadow: how much bigger than the cube, how far below it, how
            /// dark. TIGHTER and HEAVIER than an ordinary cube's - the weight is the point.</summary>
            public static float ShadowScale = 0.9f;

            public static float ShadowDrop = 0.055f;

            public static float ShadowAlpha = 0.38f;
        }

        /// <summary>What the lab can switch off one at a time - the only way to see what each layer
        /// is worth.</summary>
        public static class Layers
        {
            public static bool ShowShell = true;

            public static bool ShowSeams = true;

            public static bool ShowDimple = true;

            public static bool ShowScars = true;

            public static bool ShowShadow = true;

            public static void AllOn()
            {
                ShowShell = true;
                ShowSeams = true;
                ShowDimple = true;
                ShowScars = true;
                ShowShadow = true;
            }
        }

        /// <summary>The industrial pressure amber. Muted and warm - never a neon alarm red, and
        /// never the gold a reward would use.</summary>
        public static readonly Color PressureAmber = new Color(0.72f, 0.45f, 0.16f);

        /// <summary>Everything the shell shader needs for one plate. Shared by this view, the
        /// compression, the release and the failure, so a compressed cube looks the same whichever
        /// of them is driving it.</summary>
        public struct ShellLook
        {
            public Color Base;
            public float SeamDepth;
            public float SeamWidth;
            public float DimpleDepth;
            public float DimpleRadius;
            public float Corner;
            public float Gloss;
            /// <summary>How loaded each quadrant's pressure scar is, 0..1.</summary>
            public Vector4 Scars;
            /// <summary>Where the burnt-amber strain is along each scar, 0..1; below zero for a
            /// scar that is not locking this frame.</summary>
            public Vector4 Glints;
            /// <summary>How far through its life the press is, 0..1 - what ages the whole shell.
            /// </summary>
            public float Load;
            /// <summary>The uneven surface strain of a shell working at its limit.</summary>
            public float Strain;
            public Color Q0;
            public Color Q1;
            public Color Q2;
            public Color Q3;
            public float Memory;
            public float Stress;
            /// <summary>Per-side outward push in WORLD units: x = +x, y = +y, z = -x, w = -y.</summary>
            public Vector4 EdgeLead;
            public float Collapse;
            public float Alpha;

            /// <summary>A shut press at rest.</summary>
            public static ShellLook Rest(Color slate)
            {
                return new ShellLook
                {
                    Base = slate,
                    SeamDepth = Style.SeamDepth,
                    SeamWidth = Style.SeamWidth,
                    DimpleDepth = Style.DimpleDepth,
                    DimpleRadius = Style.DimpleRadius,
                    Corner = Style.CornerCompression,
                    Gloss = Style.Gloss,
                    Scars = Vector4.zero,
                    Glints = new Vector4(-1f, -1f, -1f, -1f),
                    Load = 0f,
                    Strain = 0f,
                    Memory = 0f,
                    Stress = 0f,
                    EdgeLead = Vector4.zero,
                    Collapse = 0f,
                    Alpha = 1f
                };
            }
        }

        private static readonly int SeamsId = Shader.PropertyToID("_Seams");
        private static readonly int DimpleId = Shader.PropertyToID("_Dimple");
        private static readonly int CornerId = Shader.PropertyToID("_Corner");
        private static readonly int GlossId = Shader.PropertyToID("_Gloss");
        private static readonly int ScarsId = Shader.PropertyToID("_Scars");
        private static readonly int GlintsId = Shader.PropertyToID("_Glints");
        private static readonly int ScarGeomId = Shader.PropertyToID("_ScarGeom");
        private static readonly int LoadId = Shader.PropertyToID("_Load");
        private static readonly int StrainId = Shader.PropertyToID("_Strain");
        private static readonly int AmberId = Shader.PropertyToID("_Amber");
        private static readonly int Q0Id = Shader.PropertyToID("_Q0");
        private static readonly int Q1Id = Shader.PropertyToID("_Q1");
        private static readonly int Q2Id = Shader.PropertyToID("_Q2");
        private static readonly int Q3Id = Shader.PropertyToID("_Q3");
        private static readonly int MemoryId = Shader.PropertyToID("_Memory");
        private static readonly int StressId = Shader.PropertyToID("_Stress");
        private static readonly int EdgeLeadId = Shader.PropertyToID("_EdgeLead");
        private static readonly int CollapseId = Shader.PropertyToID("_Collapse");
        private static readonly int FaceHalfId = Shader.PropertyToID("_FaceHalf");

        private static Material shellMaterial;

        private static bool shellLooked;

        /// <summary>The PressShell material, or null when the shader cannot be had - in which case
        /// every caller leaves the board's own cube alone. Shader.Find works in the editor; the
        /// Resources copy survives into a build.</summary>
        public static Material ShellMaterial()
        {
            if (!shellLooked)
            {
                shellLooked = true;
                Shader shader = Shader.Find("ProjectBlock/PressShell");
                if (shader == null)
                {
                    shader = Resources.Load<Shader>("Shaders/PressShell");
                }
                if (shader != null && shader.isSupported)
                {
                    shellMaterial = new Material(shader);
                    shellMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return shellMaterial;
        }

        /// <summary>Paints one shell plate. The single place the shader's properties are written,
        /// so the compression, the idle, the release and the failure cannot drift apart.</summary>
        public static void ApplyShell(SpriteRenderer renderer, MaterialPropertyBlock block,
            ShellLook look)
        {
            if (renderer == null || block == null)
            {
                return;
            }
            block.Clear();
            float seamDepth = Layers.ShowSeams ? look.SeamDepth : 0f;
            float dimple = Layers.ShowDimple ? look.DimpleDepth : 0f;
            Vector4 scars = Layers.ShowScars ? look.Scars : Vector4.zero;
            block.SetVector(SeamsId, new Vector4(seamDepth, look.SeamWidth, 0f, 0f));
            block.SetVector(DimpleId, new Vector4(dimple, look.DimpleRadius, 0f, 0f));
            block.SetFloat(CornerId, look.Corner);
            block.SetFloat(GlossId, look.Gloss);
            block.SetVector(ScarsId, scars);
            block.SetVector(GlintsId, Layers.ShowScars ? look.Glints
                : new Vector4(-1f, -1f, -1f, -1f));
            block.SetVector(ScarGeomId, new Vector4(Style.ScarInner, Style.ScarOuter,
                Style.ScarHalfWidth, Style.ScarDepth));
            block.SetFloat(LoadId, look.Load);
            block.SetFloat(StrainId, look.Strain);
            block.SetColor(AmberId, PressureAmber);
            block.SetColor(Q0Id, look.Q0);
            block.SetColor(Q1Id, look.Q1);
            block.SetColor(Q2Id, look.Q2);
            block.SetColor(Q3Id, look.Q3);
            block.SetFloat(MemoryId, Layers.ShowSeams ? look.Memory : 0f);
            block.SetFloat(StressId, look.Stress);
            block.SetVector(EdgeLeadId, look.EdgeLead);
            block.SetFloat(CollapseId, look.Collapse);
            // The sprite's OWN half-extent, so the shader's face coordinate is really -1..1. The
            // generated plate is half a unit across (64px at 128 PPU) and assuming otherwise put
            // every corner feature off the face - which is exactly how the countdown came to be
            // invisible.
            block.SetFloat(FaceHalfId, FaceHalfOf(renderer));
            Color tint = look.Base;
            tint.a = look.Alpha;
            renderer.color = tint;
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Half a renderer's sprite in OBJECT space - what turns positionOS into a
        /// -1..1 face coordinate whatever the sprite's pixels-per-unit happens to be.</summary>
        public static float FaceHalfOf(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return 0.5f;
            }
            Vector2 size = renderer.sprite.bounds.size;
            return Mathf.Max(size.x, size.y) * 0.5f;
        }

        // =================================================================== state

        private sealed class Shut
        {
            public GridPos Cell;
            public SpriteRenderer Plate;
            public SpriteRenderer Shadow;
            public float Clock;
            public bool Suppressed;
            /// <summary>How loaded each quadrant's crease is, 0..1, eased toward its target.
            /// </summary>
            public readonly float[] Scars = new float[4];
            public readonly float[] ScarTargets = new float[4];
            /// <summary>The amber strain travelling along a scar as it locks: seconds since it
            /// started, or below zero when nothing is locking.</summary>
            public readonly float[] GlintClock = { -1f, -1f, -1f, -1f };
            /// <summary>Seconds into the PRESSURE TICK a new turn brings, or below zero between
            /// turns. This is what stops a turn arriving in one frame.</summary>
            public float TickClock = -1f;
        }

        private readonly Dictionary<GridPos, Shut> shut = new Dictionary<GridPos, Shut>();

        private readonly List<GridPos> retired = new List<GridPos>();

        private MaterialPropertyBlock block;

        private float cellSize;

        private float cubeSize;

        private int turnsLeft;

        private int turnsTotal = 4;

        private readonly Color[] memory = new Color[4];

        private bool memorySet;

        /// <summary>Sorting: the plate sits just over the board's own cube, its shadow just under.
        /// </summary>
        private const int ShadowOrder = 2;

        private const int PlateOrder = 5;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
        }

        /// <summary>
        /// The compressed cubes the rules currently have on the board, asked on every repaint so
        /// what is drawn is what the rules say rather than a memory of it. A cell that has stopped
        /// being a press - broken while shut, opened, lifted - loses its plate here.
        /// </summary>
        public void Sync(BoardView view)
        {
            if (view == null || view.Board == null)
            {
                return;
            }
            cellSize = view.CellWorldSize;
            cubeSize = view.CubeWorldSize;
            GameBoard board = view.Board;
            retired.Clear();
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Cube? cube = board.IsInside(entry.Key) ? board.GetCube(entry.Key) : null;
                if (!cube.HasValue || cube.Value.Kind != CubeKind.Compressed)
                {
                    retired.Add(entry.Key);
                }
            }
            for (int i = 0; i < retired.Count; i++)
            {
                Drop(retired[i]);
            }
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = new GridPos(board.MinX + x, board.MinY + y);
                    if (!board.IsInside(cell))
                    {
                        continue;
                    }
                    Cube? cube = board.GetCube(cell);
                    if (!cube.HasValue || cube.Value.Kind != CubeKind.Compressed)
                    {
                        continue;
                    }
                    Shut s;
                    if (!shut.TryGetValue(cell, out s))
                    {
                        s = Make(cell, view);
                        shut[cell] = s;
                    }
                    Place(s, view);
                }
            }
            ApplyCountdown();
            PaintAll();
        }

        /// <summary>How many turns the press has left, straight from the power. The view never
        /// counts turns: a countdown that disagreed with the rules would be worse than none.
        /// </summary>
        public void SetCountdown(int left, int total)
        {
            int was = LoadedQuadrants;
            turnsLeft = left;
            turnsTotal = total > 0 ? total : 4;
            int now = LoadedQuadrants;
            ApplyCountdown();
            // A turn ARRIVING gets its pressure tick. Re-stating the same turn (every repaint
            // does) must not, or the shell would twitch on every board refresh.
            if (now > was)
            {
                BeginPressureTick(now - 1);
            }
        }

        /// <summary>The colours of the four quadrants inside, for the release to wake under the
        /// seams. Index follows the rules' patch order: anchor, right, up, up-right. A transparent
        /// colour means that quadrant was EMPTY.</summary>
        public void SetMemory(Color q0, Color q1, Color q2, Color q3)
        {
            memory[0] = q0;
            memory[1] = q1;
            memory[2] = q2;
            memory[3] = q3;
            memorySet = true;
        }

        /// <summary>The stored colours, for whoever is playing the shell this frame.</summary>
        public bool TryMemory(out Color q0, out Color q1, out Color q2, out Color q3)
        {
            q0 = memory[0];
            q1 = memory[1];
            q2 = memory[2];
            q3 = memory[3];
            return memorySet;
        }

        /// <summary>Stand off a cell while the compression, the release or the failure owns its
        /// shell - two things must never draw the same plate.</summary>
        public void Suppress(GridPos cell, bool on)
        {
            Shut s;
            if (shut.TryGetValue(cell, out s))
            {
                s.Suppressed = on;
                if (s.Plate != null)
                {
                    s.Plate.enabled = !on;
                }
                if (s.Shadow != null)
                {
                    s.Shadow.enabled = !on && Layers.ShowShadow;
                }
            }
        }

        /// <summary>True while a plate is standing on that cell.</summary>
        public bool Holds(GridPos cell)
        {
            return shut.ContainsKey(cell);
        }

        /// <summary>
        /// What this layer is ACTUALLY doing right now, in one line, for the lab to print under the
        /// board. Every part of the countdown is invisible by design - a mark is a few pixels of
        /// recess - so "it looks the same" is not enough to tell a broken count from a subtle one.
        /// This says which of the two it is: how many plates are standing, whether one is suppressed
        /// or disabled, the TurnsLeft it was given, how many marks that should light, and what the
        /// marks have actually eased to.
        /// </summary>
        public string DebugState()
        {
            int lit = LoadedQuadrants;
            string marks = "-";
            string state = "plaka yok";
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Shut s2 = entry.Value;
                marks = string.Format("{0:0.00}/{1:0.00}/{2:0.00}/{3:0.00}",
                    s2.Scars[0], s2.Scars[1], s2.Scars[2], s2.Scars[3]);
                bool on = s2.Plate != null && s2.Plate.enabled;
                state = (s2.Suppressed ? "BASTIRILMIS" : "acik") + (on ? ", ciziliyor" : ", KAPALI");
                break;
            }
            return string.Format(
                "plaka {0} ({1}) | shader {2} | TurnsLeft {3} -> {4} kilitli bolge"
                    + " | yuk {5:0.00} | cizikler {6}",
                shut.Count, state, ShellMaterial() != null ? "var" : "YOK", turnsLeft, lit,
                Load, marks);
        }

        /// <summary>Takes every plate down. Called when the lab resyncs or the round ends.</summary>
        public void Stop()
        {
            retired.Clear();
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                retired.Add(entry.Key);
            }
            for (int i = 0; i < retired.Count; i++)
            {
                Drop(retired[i]);
            }
            shut.Clear();
            memorySet = false;
            turnsLeft = 0;
        }

        private Shut Make(GridPos cell, BoardView view)
        {
            var s = new Shut { Cell = cell };
            Material shell = ShellMaterial();
            var plateGo = new GameObject("PressShell");
            plateGo.transform.SetParent(transform, false);
            s.Plate = plateGo.AddComponent<SpriteRenderer>();
            s.Plate.sprite = ViewUtil.RoundedSprite;
            s.Plate.sortingOrder = PlateOrder;
            if (shell != null)
            {
                s.Plate.sharedMaterial = shell;
            }
            else
            {
                // No shader: the board's own slate cube is the press, and this layer adds nothing
                // rather than covering it with a flat square.
                s.Plate.enabled = false;
            }
            var shadowGo = new GameObject("PressShadow");
            shadowGo.transform.SetParent(transform, false);
            s.Shadow = shadowGo.AddComponent<SpriteRenderer>();
            s.Shadow.sprite = ViewUtil.RoundedSprite;
            s.Shadow.sortingOrder = ShadowOrder;
            s.Shadow.color = new Color(0f, 0f, 0f, Style.ShadowAlpha);
            // Spread the four marks over the press's life so they do not all arrive together on
            // the first repaint after a load.
            s.Clock = (Mathf.Abs(cell.X * 7 + cell.Y * 13) % 100) * 0.01f * Style.IdlePressurePeriod;
            return s;
        }

        private void Place(Shut s, BoardView view)
        {
            Vector2 at = view.CellToWorld(s.Cell);
            float size = cubeSize > 0f ? cubeSize : cellSize;
            if (s.Plate != null)
            {
                ViewUtil.FitGeneratedPlate(s.Plate, new Vector2(size, size));
                s.Plate.transform.localPosition = new Vector3(at.x, at.y, 0f);
                s.Plate.enabled = !s.Suppressed && Layers.ShowShell && ShellMaterial() != null;
            }
            if (s.Shadow != null)
            {
                s.Shadow.transform.localPosition =
                    new Vector3(at.x, at.y - size * Style.ShadowDrop, 0f);
                s.Shadow.enabled = !s.Suppressed && Layers.ShowShadow;
            }
        }

        /// <summary>
        /// How many quadrants have locked: ONE PER TURN THE PRESS HAS BEEN SHUT.
        ///
        /// The power sets TurnsLeft to TurnsCompressed the moment it runs - mid-turn - and only
        /// decrements at the END of that same turn, so the states the player sees are
        /// TurnsLeft = 4, 3, 2, 1 and then it opens. That is FOUR standing states, so the count is
        /// total - TurnsLeft + 1, running 1..4.
        ///
        /// The count is not the point, though: it picks which creases are loaded, and everything
        /// else about the shell - dimple, seams, bevel, shadow - ages with <see cref="Load"/>. A
        /// player should be able to tell the turns apart without counting anything.
        /// </summary>
        private int LoadedQuadrants
        {
            get
            {
                return turnsLeft > 0 ? Mathf.Clamp(turnsTotal - turnsLeft + 1, 0, 4) : 0;
            }
        }

        /// <summary>How far through its life the press is, 0 on the first turn to 1 on the last.
        /// </summary>
        private float Load
        {
            get
            {
                int total = Mathf.Max(1, turnsTotal);
                return total > 1
                    ? Mathf.Clamp01((LoadedQuadrants - 1) / (float)(total - 1))
                    : (LoadedQuadrants > 0 ? 1f : 0f);
            }
        }

        private void ApplyCountdown()
        {
            int lit = LoadedQuadrants;
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Shut s = entry.Value;
                for (int i = 0; i < 4; i++)
                {
                    s.ScarTargets[i] = i < lit ? 1f : 0f;
                }
            }
        }

        /// <summary>
        /// A turn has arrived. It does not land in one frame: the shell loads, the dimple bites,
        /// the seams contract, the new crease locks with a short strain running along it, the
        /// shadow tightens and the whole thing settles. Called when TurnsLeft actually changes.
        /// </summary>
        private void BeginPressureTick(int newlyLocked)
        {
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Shut s = entry.Value;
                s.TickClock = 0f;
                if (newlyLocked >= 0 && newlyLocked < 4)
                {
                    s.GlintClock[newlyLocked] = 0f;
                }
            }
        }

        private void Drop(GridPos cell)
        {
            Shut s;
            if (!shut.TryGetValue(cell, out s))
            {
                return;
            }
            if (s.Plate != null)
            {
                Destroy(s.Plate.gameObject);
            }
            if (s.Shadow != null)
            {
                Destroy(s.Shadow.gameObject);
            }
            shut.Remove(cell);
        }

        private void Update()
        {
            if (shut.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            float period = IdlePeriod;
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Shut s = entry.Value;
                s.Clock += dt;
                if (s.Clock > period)
                {
                    s.Clock -= period;
                }
                // The pressure tick a new turn brings, and then it is over.
                if (s.TickClock >= 0f)
                {
                    s.TickClock += dt;
                    if (s.TickClock > Style.TickDuration)
                    {
                        s.TickClock = -1f;
                    }
                }
                // A crease locks over the tick rather than switching on.
                float step = Style.TickDuration > 0f ? dt / Style.TickDuration : 1f;
                for (int i = 0; i < 4; i++)
                {
                    s.Scars[i] = Mathf.MoveTowards(s.Scars[i], s.ScarTargets[i], step);
                    if (s.GlintClock[i] >= 0f)
                    {
                        s.GlintClock[i] += dt;
                        if (s.GlintClock[i] > Style.ScarGlintDuration)
                        {
                            s.GlintClock[i] = -1f;
                        }
                    }
                }
            }
            PaintAll();
        }

        /// <summary>How often the contained pressure cycles, for the turn the press is on. It
        /// quickens as the release comes on: the capsule is nearer its limit, not more alive.
        /// </summary>
        private float IdlePeriod
        {
            get
            {
                int i = Mathf.Clamp(LoadedQuadrants - 1, 0,
                    Style.IdleCyclePerTurn.Length - 1);
                return LoadedQuadrants > 0 ? Style.IdleCyclePerTurn[i]
                    : Style.IdlePressurePeriod;
            }
        }

        private float IdleStrength
        {
            get
            {
                int i = Mathf.Clamp(LoadedQuadrants - 1, 0,
                    Style.IdleStrengthPerTurn.Length - 1);
                return LoadedQuadrants > 0 ? Style.IdleStrengthPerTurn[i] : 1f;
            }
        }

        private void PaintAll()
        {
            Material shell = ShellMaterial();
            if (shell == null)
            {
                return;
            }
            float period = IdlePeriod;
            float strength = IdleStrength;
            float load = Load;
            int locked = LoadedQuadrants;
            bool finalTurn = locked >= turnsTotal && locked > 0;
            foreach (KeyValuePair<GridPos, Shut> entry in shut)
            {
                Shut s = entry.Value;
                if (s.Suppressed || s.Plate == null || !s.Plate.enabled)
                {
                    continue;
                }
                float pressure = Contained(s.Clock / Mathf.Max(period, 0.001f)) * strength;
                // THE PRESSURE TICK: a short load, then a settle. Everything answers together -
                // that is what makes it mechanical rather than a tween on one property.
                float tick = 0f;
                float settle = 0f;
                if (s.TickClock >= 0f && Style.TickDuration > 0f)
                {
                    float k = Mathf.Clamp01(s.TickClock / Style.TickDuration);
                    tick = k < 0.55f ? Ease(k / 0.55f) : 1f - Ease((k - 0.55f) / 0.45f);
                    settle = k > 0.55f ? Ease((k - 0.55f) / 0.45f) : 0f;
                }

                ShellLook look = ShellLook.Rest(
                    ViewUtil.CubeMaterialColor(new Cube(CubeKind.Compressed, GameBoard.PressCardId)));
                // ---- ageing: one more turn under load ----
                look.DimpleDepth = Style.WaitDimpleBase + Style.WaitDimplePerTurn * (locked - 1 > 0
                    ? locked - 1 : 0);
                look.SeamDepth = Style.WaitSeamBase + Style.WaitSeamPerTurn * (locked - 1 > 0
                    ? locked - 1 : 0);
                look.Corner += Style.WaitBevelPerTurn * Mathf.Max(0, locked - 1);
                look.Load = load;
                // ---- the idle cycle, on top of that state ----
                look.DimpleDepth *= 1f + Style.IdleDimpleStrength * pressure;
                look.DimpleRadius *= 1f - 0.12f * pressure;
                look.SeamDepth *= 1f + Style.IdleSeamStrength * pressure;
                look.Gloss += Style.IdleGlossLift * pressure;
                // ---- the tick, on top of both ----
                look.DimpleDepth *= 1f + Style.TickDimpleStrength * tick;
                look.SeamDepth *= 1f + Style.TickSeamStrength * tick;
                // The four edges press INWARD while it loads - negative lead, never a scale.
                float inset = -(Style.TickEdgeInset * tick + Style.TickSettle * settle)
                    * (cubeSize > 0f ? cubeSize : cellSize);
                look.EdgeLead = new Vector4(inset, inset, inset, inset);
                look.Scars = new Vector4(s.Scars[0], s.Scars[1], s.Scars[2], s.Scars[3]);
                look.Glints = new Vector4(GlintAt(s, 0), GlintAt(s, 1), GlintAt(s, 2),
                    GlintAt(s, 3));
                if (finalTurn)
                {
                    // The shell working at its limit: a very subtle warm residue in the creases
                    // and an uneven surface strain. It never becomes an orange cube.
                    look.Stress = Style.FinalStressResidue;
                    look.Strain = Style.FinalSurfaceStrain * (0.5f + 0.5f * pressure);
                }
                ApplyShell(s.Plate, block, look);

                if (s.Shadow != null && s.Shadow.enabled)
                {
                    // The shadow draws IN and gets HEAVIER with every turn: the capsule sits
                    // harder on the board the longer it holds.
                    float tightness = Style.WaitShadowTightBase
                        + Style.WaitShadowTightPerTurn * Mathf.Max(0, locked - 1)
                        + Style.IdleShadowTightening * pressure + 0.02f * tick;
                    float size = (cubeSize > 0f ? cubeSize : cellSize)
                        * Style.ShadowScale * (1f - tightness);
                    ViewUtil.FitGeneratedPlate(s.Shadow, new Vector2(size, size));
                    float dark = Style.ShadowAlpha
                        + Style.WaitShadowDarkPerTurn * Mathf.Max(0, locked - 1)
                        + 0.04f * tick;
                    s.Shadow.color = new Color(0f, 0f, 0f, Mathf.Clamp01(dark));
                }
            }
        }

        /// <summary>Where the amber strain has got to along a locking crease, 0..1 - or below zero
        /// when that crease is not locking this frame.</summary>
        private static float GlintAt(Shut s, int index)
        {
            if (s.GlintClock[index] < 0f || Style.ScarGlintDuration <= 0f)
            {
                return -1f;
            }
            return Mathf.Clamp01(s.GlintClock[index] / Style.ScarGlintDuration);
        }

        private static float Ease(float k)
        {
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        /// <summary>One cycle of contained pressure: a short contraction and a slow return, never a
        /// sine that makes the cube look like it is breathing.</summary>
        private static float Contained(float t)
        {
            t = Mathf.Repeat(t, 1f);
            float share = Mathf.Clamp(Style.IdlePressureShare, 0.05f, 0.9f);
            if (t < share)
            {
                float k = t / share;
                return Mathf.SmoothStep(0f, 1f, k);
            }
            float back = (t - share) / (1f - share);
            return Mathf.SmoothStep(1f, 0f, back);
        }
    }
}
