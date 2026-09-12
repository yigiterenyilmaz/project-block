// PURPOSE: "Kangren" - NECROTIC TAKEOVER / KURU ÇÜRÜME. The whole boss in one visual family: the
// rot taking a cell, a line dying for good, and the jump to the edge that follows. Owned by
// BoardView (like the nest and the containment field) because half of it is PRESENCE rather than
// an event: a rotten cube carries dead tissue and a dead line carries a scar for the rest of the
// round, both of which have to survive every repaint.
//
//   A SPREAD, EMPTY CELL     the floor of the cell is contaminated from the SOURCE side (the
//                            neighbour the rot crept out of), veins run in with it, then the dead
//                            mass rises a couple of pixels out of the floor, locks and settles.
//   B SPREAD, OCCUPIED CELL  the cube converts WHERE IT STANDS: a necrotic front crosses it from
//                            the source side and a little sooner at its edges than through its
//                            middle, so the last living colour is in the centre. Ahead of the
//                            front its life goes out (colour, then highlights, then a cast of the
//                            rot), veins reach in, hairline cracks open, and the surface contracts
//                            a pixel as it gives way. Its face never swaps for another.
//   C IMMUNE NEIGHBOURS      what the rot pressed against and could not take gets a dark stain on
//                            the contact edge for a breath - the only way to see it was refused.
//   D PRESENCE               every rotten cube breathes a slow, low, desynchronised dark pulse,
//                            and the turn the boss bills the player they all pulse once together.
//   E A LINE DIES            the life goes out along the line (a front from its middle, or end to
//                            end), the cells' wash comes in behind it, and a rounded band of dead
//                            stone - grained, cracked, with the light off its inner edge - is left
//                            under the line PERMANENTLY. That band is the promise the rules make:
//                            this line can never explode again.
//   F THE JUMP               necrotic pressure travels from the dead line to the nearer edge, one
//                            path per cube it is about to take, and those cubes convert in place
//                            with a small stagger. An empty edge cell stays empty - nothing is
//                            ever spawned. A cascade runs step by step with a pause between.
//
// NOTHING HERE DECIDES ANYTHING. Core reports the cell the rot took, the side it came from, what
// stood there, which cubes it could not take, every line that died in the order they died, which
// edge line each jump went to and which cubes it turned (TurnReport.GangreneSpread /
// GangreneLineDeaths); this draws exactly that. The board holds every cell an animation is on
// (BoardView.HoldCells) so nothing is ever drawn twice, and its dead-line wash is withheld
// (BoardView.SetRotWash) until the sweep brings it in.
//
// NOT A REMOVAL AND NOT AN EXPLOSION: no pit, no fold, no sublimation, no debris, flash, shake or
// particles, nothing flies, nothing glows, and the palette is ash, charcoal and dead olive -
// never toxic green, lime, neon or slime. Two shaders (Resources/Shaders/Gangrene,
// GangreneStreak); without them the conversion cross-fades to the rot colour and the band is a
// flat rounded plate.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>"Kangren": the rot's presence on the board and everything it does on a turn.</summary>
    public sealed class GangreneView : MonoBehaviour
    {
        // =================================================================== TUNING
        /// <summary>Everything that decides how the rot READS. Sizes in CELLS, times in seconds.</summary>
        public static class Style
        {
            // ---- the spread ----
            /// <summary>An empty cell taken: contamination, then the dead mass rising.</summary>
            public static float SpreadDurationEmpty = 0.32f;

            /// <summary>A cube converted where it stands.</summary>
            public static float SpreadDurationOccupied = 0.36f;

            /// <summary>How far the front follows the veins instead of running straight - the creep
            /// out of the source cell.</summary>
            public static float SourceCreepStrength = 0.30f;

            /// <summary>How dark an empty cell's floor goes as it is contaminated.</summary>
            public static float FloorContaminationAmount = 0.55f;

            // ---- the surface ----
            public static float VeinVisibility = 0.44f;

            /// <summary>Veins per face. They run in from the source edge and branch once.</summary>
            public static int VeinCount = 5;

            /// <summary>How far ahead of the front the veins have already reached, in face units.</summary>
            public static float VeinSpeed = 0.30f;

            public static float ColorDrainStrength = 0.90f;

            /// <summary>How far the highlights flatten: dead tissue is matte.</summary>
            public static float SurfaceMatteAmount = 0.62f;

            public static float CrackVisibility = 0.38f;

            /// <summary>How far the face draws in as it gives way, in cells (0.03: two pixels on a
            /// 64-pixel cell). It settles back to the board's own size, so the hand-off is invisible.</summary>
            public static float SurfaceCollapseAmount = 0.030f;

            // ---- dead mass growing in an empty cell ----
            /// <summary>How far the new mass rises out of the floor, in cells (0.035: about 2 px).</summary>
            public static float SpawnRiseAmount = 0.035f;

            public static float SpawnSettleDuration = 0.075f;

            // ---- presence ----
            /// <summary>How much darker a rotten cube's tissue goes at the bottom of its breath.</summary>
            public static float IdlePulseStrength = 0.12f;

            public static float IdlePulsePeriodMin = 1.6f;

            public static float IdlePulsePeriodMax = 2.9f;

            /// <summary>The one pulse they all share the turn the rot bills the player.</summary>
            public static float TurnDamagePulseStrength = 0.26f;

            public static float TurnDamagePulseDuration = 0.12f;

            // ---- a line dying ----
            public static float DeadLineSweepDuration = 0.38f;

            public static SweepMode DeadLineSweepMode = SweepMode.CentreOut;

            public static float DeadLineUnderlayOpacity = 0.72f;

            /// <summary>The band's corner radius, in cells.</summary>
            public static float DeadLineUnderlayRadius = 0.20f;

            public static float DeadLineTextureStrength = 0.55f;

            /// <summary>How deep the band sits: the light off its own inner edge.</summary>
            public static float DeadLineShadowStrength = 0.62f;

            // ---- the jump ----
            public static float EdgeTransferDuration = 0.22f;

            public static float EdgeTransferVisibility = 0.55f;

            public static float EdgeTransferVeinStrength = 0.50f;

            public static float EdgeConversionDuration = 0.30f;

            /// <summary>Seconds between the edge cubes - a wave, never a row flipping at once.</summary>
            public static float EdgeConversionStagger = 0.035f;

            /// <summary>The breath between one step of a cascade and the next.</summary>
            public static float ChainPauseDuration = 0.10f;

            // ---- variation and level of detail ----
            /// <summary>How much of the per-cell variation is used: the surface variant, the phase
            /// of the breath, the few milliseconds each conversion is off by.</summary>
            public static float VisualSeedVariation = 1f;

            /// <summary>How far the presence thins as the patch grows, so a board half taken by the
            /// rot is still quiet.</summary>
            public static float LargeClusterCompensation = 0.70f;

            // ---- when the turn's rot starts ----
            /// <summary>After a turn that cleared something: the player's own blast reads first.</summary>
            public static float TurnStartDelay = 0.28f;

            /// <summary>After a quiet turn there is nothing to wait for.</summary>
            public static float TurnStartDelayQuiet = 0.06f;
        }

        /// <summary>Which way the life goes out of a dying line.</summary>
        public enum SweepMode
        {
            /// <summary>From its middle, both ways at once.</summary>
            CentreOut,

            /// <summary>From one end to the other.</summary>
            EndToEnd
        }

        /// <summary>The lab's debug switches. The first and the last DRAW something extra and start
        /// off; the rest turn a layer of the effect off. The lab puts them all back on close.</summary>
        public static class Layers
        {
            /// <summary>A line from the rotten cell the spread came out of to the cell it took.</summary>
            public static bool ShowSourceDirection;

            public static bool ShowVeinLayer = true;

            public static bool ShowCrackLayer = true;

            /// <summary>Off: an empty cell's floor is not contaminated first - the mass just grows.</summary>
            public static bool ShowCellFloorContamination = true;

            /// <summary>Off: a dead line keeps only the cells' wash, with no band under it.</summary>
            public static bool ShowDeadLineUnderlay = true;

            public static bool ShowEdgeTransferPath = true;

            /// <summary>Off: the edge cubes are simply rotten when the pressure arrives.</summary>
            public static bool ShowTargetEdgeConversion = true;

            /// <summary>An outline round the line that died and the edge line it jumped to, drawn
            /// through the pause between the steps of a cascade.</summary>
            public static bool ShowChainStepBreaks;

            public static void Defaults()
            {
                ShowSourceDirection = false;
                ShowVeinLayer = true;
                ShowCrackLayer = true;
                ShowCellFloorContamination = true;
                ShowDeadLineUnderlay = true;
                ShowEdgeTransferPath = true;
                ShowTargetEdgeConversion = true;
                ShowChainStepBreaks = false;
            }
        }

        // =================================================================== palette
        //
        // Ash, charcoal, bruised grey-green and a muted dead olive. Nothing in here is saturated:
        // the rot is DEAD tissue, and a toxic green would read as a power-up.

        /// <summary>Dead tissue - the same muted olive the board tints a rotten cube.</summary>
        private static readonly Color RotColour = new Color(0.36f, 0.40f, 0.30f);

        /// <summary>The veins: a bruised charcoal green.</summary>
        private static readonly Color VeinColour = new Color(0.13f, 0.15f, 0.12f);

        /// <summary>Hairline cracks: near-black ash.</summary>
        private static readonly Color CrackColour = new Color(0.09f, 0.09f, 0.08f);

        /// <summary>What the matte mottling calms the surface toward.</summary>
        private static readonly Color MatteColour = new Color(0.29f, 0.31f, 0.25f);

        /// <summary>A contaminated floor, and an immune cube's contact stain. Lighter than the
        /// empty cell it is drawn on, or there would be nothing to see there.</summary>
        private static readonly Color StainColour = new Color(0.20f, 0.21f, 0.17f);

        /// <summary>The pressure travelling to the edge: ashen, so it reads ACROSS the dark board
        /// between the dead line and the cubes it is coming for.</summary>
        private static readonly Color PressureColour = new Color(0.32f, 0.34f, 0.28f);

        /// <summary>The dead line's band: stone / ash grey, no green in it at all. Lighter than
        /// the board's own plate on purpose - under the cubes it is only ever seen as the rim round
        /// the line and the gaps between the cells, and a band the plate's own value said nothing
        /// at all. Its inner shadow (below) is what keeps it a recess rather than a highlight.</summary>
        private static readonly Color BandColour = new Color(0.30f, 0.29f, 0.27f);

        /// <summary>The dark the breath and the turn's pulse take the tissue to.</summary>
        private static readonly Color PulseTint = new Color(0.42f, 0.44f, 0.40f);

        private static readonly Color ShadowColour = new Color(0.03f, 0.035f, 0.03f);

        private static readonly Color DebugColour = new Color(0.95f, 0.55f, 0.35f);

        private static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

        // =================================================================== layers
        //
        // The band is UNDER the cells (the board surface is -3, the cells 1): a dead line is part
        // of the board, not a mark laid over it. The idle tissue sits just over the cells, below
        // the ambient particles at 3. Everything an EVENT draws is above those.

        private const int BandOrder = 0;

        private const int SurfaceOrder = 2;

        private const int ShadowOrder = 5;

        private const int RotOrder = 6;

        private const int BodyOrder = 7;

        private const int PathOrder = 8;

        private const int SweepOrder = 9;

        private const int DebugOrder = 10;

        /// <summary>The contact shadow under dead mass while it is still off the floor.</summary>
        private const float ShadowAlpha = 0.26f;

        /// <summary>The sweep front's width and the band front's softness, along the line.</summary>
        private const float FrontSoftness = 0.13f;

        /// <summary>How soft the necrotic front is across a FACE, in face units.</summary>
        private const float FaceSoftness = 0.09f;

        /// <summary>How wide the pressure path is, in cells.</summary>
        private const float PathWidth = 0.5f;

        /// <summary>Faces of dead tissue built, and their size in pixels.</summary>
        private const int Variants = 6;

        private const int TileSize = 64;

        // =================================================================== state

        /// <summary>The board view this belongs to - where the cells are, and who holds them.</summary>
        private BoardView view;

        private GameBoard board;

        private float cellSize;

        private float cubeSize;

        private float slotSize;

        /// <summary>One rotten cube's standing tissue: the overlay, and the breath it is on.</summary>
        private sealed class Presence
        {
            public SpriteRenderer Surface;
            public int Variant;
            public int Turn;
            public float Period;
            public float Phase;
        }

        private readonly Dictionary<GridPos, Presence> presence = new Dictionary<GridPos, Presence>();

        /// <summary>Which side the rot came into a cell from, remembered so its tissue keeps
        /// pointing that way for the rest of the round. Cosmetic; a cell the View never saw
        /// convert (a reloaded round, the lab) falls back to its own hash.</summary>
        private readonly Dictionary<GridPos, int> sourceTurn = new Dictionary<GridPos, int>();

        /// <summary>A dead line's permanent band, keyed by the line.</summary>
        private sealed class Band
        {
            public SpriteRenderer Renderer;
            public bool IsRow;
            public int Line;
            /// <summary>How much of it has been swept in, 0..1. 1 for a line that was already
            /// dead when this first saw the board.</summary>
            public float Along;
            public float Seed;
        }

        private readonly Dictionary<int, Band> bands = new Dictionary<int, Band>();

        private readonly List<GridPos> cellBuffer = new List<GridPos>();

        private readonly List<int> bandBuffer = new List<int>();

        // =================================================================== the turn's events
        //
        // Core's report, turned into world positions and faces by the controller. Nothing in here
        // is worked out from the board.

        /// <summary>One cube the rot turned where it stood, with the face it wore.</summary>
        public struct Converted
        {
            public GridPos Cell;
            public ClusterBurstView.Look Before;
        }

        /// <summary>One line the rot took whole, and the jump that followed it.</summary>
        public sealed class LineDeath
        {
            public bool IsRow;

            /// <summary>Absolute row or column, as Core reported it.</summary>
            public int Line;

            /// <summary>The edge line the rot jumped to.</summary>
            public int EdgeLine;

            /// <summary>The cubes the jump turned there - never worked out here.</summary>
            public readonly List<Converted> Converted = new List<Converted>();
        }

        /// <summary>Everything the rot did on one turn.</summary>
        public sealed class TurnScene
        {
            /// <summary>The cell it took, or none on a turn it had nowhere to go.</summary>
            public GridPos? Cell;

            /// <summary>The rotten neighbour it crept out of; none for the seed.</summary>
            public GridPos? Source;

            /// <summary>What stood in the cell, if anything did.</summary>
            public bool HadCube;

            public ClusterBurstView.Look Before;

            /// <summary>Cubes beside the cell that nothing can infect.</summary>
            public readonly List<GridPos> Immune = new List<GridPos>();

            /// <summary>The lines that died, in the order they died.</summary>
            public readonly List<LineDeath> Deaths = new List<LineDeath>();

            /// <summary>The turn billed the player for the rot standing: they all pulse once.</summary>
            public bool Billed;

            /// <summary>How long to wait first, so a clear of the player's own reads before this.</summary>
            public float Delay = Style.TurnStartDelayQuiet;
        }

        // =================================================================== what is playing

        private sealed class Conversion
        {
            public GridPos Cell;
            public Vector2 At;
            public int Turn;
            public int Variant;
            public ClusterBurstView.Look Before;
            public float Start;
            public float End;
            public SpriteRenderer Rot;
            public SpriteRenderer Body;
            public bool Done;
        }

        private sealed class Growth
        {
            public GridPos Cell;
            public Vector2 At;
            public int Turn;
            public int Variant;
            public float Start;
            public float End;
            public SpriteRenderer Stain;
            public SpriteRenderer Shadow;
            public SpriteRenderer Body;
            public bool Done;
        }

        private sealed class Extinguish
        {
            public bool IsRow;
            public int Line;
            public int Key;
            public float Start;
            public float End;
            public Vector2 Centre;
            public float Length;
            public float Angle;
            public SpriteRenderer Front;
            public readonly List<GridPos> Cells = new List<GridPos>();
            /// <summary>Where each cell sits along the line, 0..1.</summary>
            public readonly List<float> Along = new List<float>();
            public bool Done;
        }

        private sealed class Pressure
        {
            public float Start;
            public float End;
            public Vector2 From;
            public Vector2 To;
            public float Seed;
            public SpriteRenderer Renderer;
        }

        private sealed class Contact
        {
            public Vector2 At;
            public float Angle;
            public float Start;
            public float End;
            public SpriteRenderer Renderer;
        }

        private sealed class StepBreak
        {
            public float Start;
            public float End;
            public Vector2 Centre;
            public Vector2 Size;
            public SpriteRenderer Renderer;
        }

        private sealed class Scene
        {
            public float Clock;
            public float End;
            public float PulseStart;
            public float PulseEnd;
            public readonly List<Conversion> Conversions = new List<Conversion>();
            public readonly List<Growth> Growths = new List<Growth>();
            public readonly List<Extinguish> Extinguishes = new List<Extinguish>();
            public readonly List<Pressure> Pressures = new List<Pressure>();
            public readonly List<Contact> Contacts = new List<Contact>();
            public readonly List<StepBreak> Breaks = new List<StepBreak>();
            public readonly List<GridPos> Held = new List<GridPos>();
            public SpriteRenderer SourceLine;
            public Vector2 SourceFrom;
            public Vector2 SourceTo;
        }

        private Scene scene;

        private readonly Stack<SpriteRenderer> spareRenderers = new Stack<SpriteRenderer>();

        private Material plainMaterial;

        private MaterialPropertyBlock block;

        // =================================================================== driving it

        /// <summary>
        /// Puts the standing rot in step with the board: a tissue overlay on every rotten cube the
        /// board is actually drawing, a band under every line the rules have killed, and nothing on
        /// a cell an animation is holding. Called from BoardView.Refresh, so this is always what
        /// Core says - never a memory of it.
        /// </summary>
        public void Sync(BoardView owner)
        {
            view = owner;
            GameBoard now = owner != null ? owner.Board : null;
            if (now != board)
            {
                // A different arena: nothing that was playing belongs to it.
                Stop();
                board = now;
            }
            if (board == null || view == null)
            {
                return;
            }
            cellSize = view.CellWorldSize;
            cubeSize = view.CubeWorldSize;
            slotSize = view.EmptySlotSize;
            SyncPresence();
            SyncBands();
        }

        /// <summary>
        /// Plays one turn's rot, exactly as Core reported it. Called on the frame the board was
        /// repainted in its new state: the cells the animations cover are taken back from the board
        /// (HoldCells) and the dead lines' wash is wound back to nothing, both before anything is
        /// drawn, so the end state is never seen first.
        /// </summary>
        public void PlayTurn(TurnScene turn)
        {
            if (view == null || board == null || turn == null)
            {
                return;
            }
            EnsureArt();
            Finish();
            scene = new Scene();
            float t = Mathf.Max(turn.Delay, 0f);

            // ---- A / B: the cell the rot took ----
            if (turn.Cell.HasValue && board.IsInside(turn.Cell.Value))
            {
                GridPos cell = turn.Cell.Value;
                int side = TurnFromSource(cell, turn.Source);
                float duration = turn.HadCube ? Style.SpreadDurationOccupied : Style.SpreadDurationEmpty;
                if (turn.HadCube)
                {
                    AddConversion(cell, side, turn.Before, t, t + duration);
                }
                else
                {
                    AddGrowth(cell, side, t, t + duration);
                }
                // C: what it pressed against and could not take.
                for (int i = 0; i < turn.Immune.Count; i++)
                {
                    AddContact(turn.Immune[i], cell, t + duration * 0.45f);
                }
                if (Layers.ShowSourceDirection && turn.Source.HasValue)
                {
                    scene.SourceFrom = view.CellToWorld(turn.Source.Value);
                    scene.SourceTo = view.CellToWorld(cell);
                    scene.SourceLine = Rent(LineSprite(), DebugOrder, null);
                }
                t += duration;
            }

            // ---- E / F: the lines that died, and the jumps, one step at a time ----
            for (int d = 0; d < turn.Deaths.Count; d++)
            {
                LineDeath death = turn.Deaths[d];
                Extinguish sweep = AddExtinguish(death, t, t + Style.DeadLineSweepDuration);
                t += Style.DeadLineSweepDuration;
                float jumpEnd = t;
                for (int i = 0; i < death.Converted.Count; i++)
                {
                    Converted target = death.Converted[i];
                    if (!board.IsInside(target.Cell))
                    {
                        continue;
                    }
                    // The pressure leaves the dead line from the cell in the target's own file, so
                    // the path says which cube is about to go.
                    GridPos from = death.IsRow
                        ? new GridPos(target.Cell.X, death.Line)
                        : new GridPos(death.Line, target.Cell.Y);
                    float lead = t + i * Mathf.Max(Style.EdgeConversionStagger, 0f);
                    if (Layers.ShowEdgeTransferPath)
                    {
                        AddPressure(from, target.Cell, lead, lead + Style.EdgeTransferDuration);
                    }
                    float convertAt = lead + Style.EdgeTransferDuration;
                    float convertEnd = convertAt + (Layers.ShowTargetEdgeConversion
                        ? Style.EdgeConversionDuration : 0f);
                    int side = TurnTowardLine(target.Cell, death.IsRow, death.Line);
                    AddConversion(target.Cell, side, target.Before, convertAt, convertEnd);
                    jumpEnd = Mathf.Max(jumpEnd, convertEnd);
                }
                t = jumpEnd;
                if (Layers.ShowChainStepBreaks && sweep != null)
                {
                    AddStepBreak(death, t, t + Style.ChainPauseDuration);
                }
                t += Style.ChainPauseDuration;
            }

            // ---- D: the turn's bill, felt through every rotten cube at once ----
            if (turn.Billed)
            {
                scene.PulseStart = t;
                scene.PulseEnd = t + Style.TurnDamagePulseDuration;
                t = scene.PulseEnd;
            }
            scene.End = t;
            if (scene.Held.Count > 0)
            {
                view.HoldCells(scene.Held);
            }
            // Nothing has been drawn yet this frame: paint every layer at its start, so no cell is
            // ever seen in its finished state first.
            PaintScene();
        }

        /// <summary>Everything playing lands at once and the board takes its cells back - the run
        /// ending, the lab closing, a new arena going up.</summary>
        public void Stop()
        {
            Finish();
            foreach (KeyValuePair<GridPos, Presence> entry in presence)
            {
                Return(entry.Value.Surface);
            }
            presence.Clear();
            foreach (KeyValuePair<int, Band> entry in bands)
            {
                Return(entry.Value.Renderer);
            }
            bands.Clear();
            sourceTurn.Clear();
            if (view != null)
            {
                view.ClearRotWash();
            }
        }

        // =================================================================== presence

        /// <summary>A tissue overlay on every rotten cube the BOARD is drawing - so a cell in the
        /// dark, or one an animation is holding, has none, and none is ever drawn under a cube that
        /// is not there.</summary>
        private void SyncPresence()
        {
            cellBuffer.Clear();
            foreach (KeyValuePair<GridPos, Presence> entry in presence)
            {
                if (view.RotLight(entry.Key) <= 0f)
                {
                    cellBuffer.Add(entry.Key);
                }
            }
            for (int i = 0; i < cellBuffer.Count; i++)
            {
                Return(presence[cellBuffer[i]].Surface);
                presence.Remove(cellBuffer[i]);
            }
            for (int x = 0; x < board.Width; x++)
            {
                for (int y = 0; y < board.Height; y++)
                {
                    var cell = new GridPos(board.MinX + x, board.MinY + y);
                    if (view.RotLight(cell) <= 0f || presence.ContainsKey(cell))
                    {
                        continue;
                    }
                    EnsureArt();
                    uint seed = Hash(cell.X, cell.Y);
                    var p = new Presence
                    {
                        Variant = VariantFor(seed),
                        Turn = TurnFor(cell),
                        Period = Mathf.Lerp(Style.IdlePulsePeriodMin, Style.IdlePulsePeriodMax,
                            (seed >> 9 & 1023u) / 1023f),
                        Phase = (seed >> 19 & 1023u) / 1023f
                    };
                    p.Surface = Rent(LookSprite(p.Variant), SurfaceOrder, null);
                    presence[cell] = p;
                }
            }
        }

        /// <summary>A band under every line the rules have killed, and none under a line they have
        /// not. A line that was already dead when this first saw the board gets its band whole -
        /// there was no death to watch.</summary>
        private void SyncBands()
        {
            bandBuffer.Clear();
            foreach (KeyValuePair<int, Band> entry in bands)
            {
                Band band = entry.Value;
                bool dead = band.IsRow
                    ? board.RowIsInfectionDead(band.Line)
                    : board.ColumnIsInfectionDead(band.Line);
                if (!dead || !Layers.ShowDeadLineUnderlay)
                {
                    bandBuffer.Add(entry.Key);
                }
            }
            for (int i = 0; i < bandBuffer.Count; i++)
            {
                Return(bands[bandBuffer[i]].Renderer);
                bands.Remove(bandBuffer[i]);
            }
            if (!Layers.ShowDeadLineUnderlay)
            {
                return;
            }
            for (int i = 0; i < board.InfectionDeadRows.Count; i++)
            {
                EnsureBand(true, board.InfectionDeadRows[i]);
            }
            for (int i = 0; i < board.InfectionDeadColumns.Count; i++)
            {
                EnsureBand(false, board.InfectionDeadColumns[i]);
            }
        }

        private Band EnsureBand(bool isRow, int line)
        {
            int key = BandKey(isRow, line);
            Band band;
            if (bands.TryGetValue(key, out band))
            {
                return band;
            }
            EnsureArt();
            band = new Band
            {
                IsRow = isRow,
                Line = line,
                Along = 1f,
                Seed = (Hash(isRow ? line : -line - 1, isRow ? 977 : 613) & 4095u) / 4095f * 6.283f
            };
            band.Renderer = Rent(ViewUtil.WhiteSprite, BandOrder,
                streakMaterial != null ? streakMaterial : null);
            bands[key] = band;
            return band;
        }

        private static int BandKey(bool isRow, int line)
        {
            return (line << 1) | (isRow ? 1 : 0);
        }

        // =================================================================== building the scene

        private void AddConversion(GridPos cell, int side, ClusterBurstView.Look before,
            float start, float end)
        {
            uint seed = Hash(cell.X * 7 + 3, cell.Y * 5 + 11);
            float jitter = Style.VisualSeedVariation * 0.012f * ((seed & 255u) / 255f);
            var c = new Conversion
            {
                Cell = cell,
                At = view.CellToWorld(cell),
                Turn = side,
                Variant = VariantFor(seed),
                Before = before,
                Start = start + jitter,
                End = end + jitter
            };
            c.Rot = Rent(RotTile(), RotOrder, gangreneMaterial);
            if (before.Tile != null)
            {
                c.Body = Rent(before.Tile, BodyOrder,
                    gangreneMaterial != null ? gangreneMaterial : ViewUtil.TileMaterial(before.Tile));
            }
            scene.Conversions.Add(c);
            scene.Held.Add(cell);
            sourceTurn[cell] = side;
        }

        private void AddGrowth(GridPos cell, int side, float start, float end)
        {
            uint seed = Hash(cell.X * 7 + 3, cell.Y * 5 + 11);
            var g = new Growth
            {
                Cell = cell,
                At = view.CellToWorld(cell),
                Turn = side,
                Variant = VariantFor(seed),
                Start = start,
                End = end
            };
            if (Layers.ShowCellFloorContamination)
            {
                g.Stain = Rent(SlotSprite(), SurfaceOrder, gangreneMaterial);
            }
            g.Shadow = Rent(ShadowSprite(), ShadowOrder, null);
            g.Body = Rent(RotTile(), RotOrder, gangreneMaterial);
            scene.Growths.Add(g);
            scene.Held.Add(cell);
            sourceTurn[cell] = side;
        }

        private void AddContact(GridPos immune, GridPos target, float at)
        {
            if (!board.IsInside(immune))
            {
                return;
            }
            uint seed = Hash(immune.X * 13 + 5, immune.Y * 17 + 7);
            // A breath, no more: 40 to 100 ms.
            float life = Mathf.Lerp(0.04f, 0.10f, (seed & 255u) / 255f * Style.VisualSeedVariation);
            Vector2 towards = view.CellToWorld(target) - view.CellToWorld(immune);
            var c = new Contact
            {
                At = view.CellToWorld(immune),
                Angle = Mathf.Atan2(towards.y, towards.x) * Mathf.Rad2Deg,
                Start = at,
                End = at + life
            };
            c.Renderer = Rent(ContactSprite(), SurfaceOrder, null);
            scene.Contacts.Add(c);
        }

        private Extinguish AddExtinguish(LineDeath death, float start, float end)
        {
            var e = new Extinguish
            {
                IsRow = death.IsRow,
                Line = death.Line,
                Key = BandKey(death.IsRow, death.Line),
                Start = start,
                End = end,
                Angle = death.IsRow ? 0f : 90f
            };
            int count = death.IsRow ? board.Width : board.Height;
            for (int i = 0; i < count; i++)
            {
                GridPos cell = death.IsRow
                    ? new GridPos(board.MinX + i, death.Line)
                    : new GridPos(death.Line, board.MinY + i);
                if (!board.IsInside(cell))
                {
                    continue;
                }
                e.Cells.Add(cell);
                e.Along.Add(count > 1 ? i / (float)(count - 1) : 0.5f);
                // The wash the board put on the moment the rules killed the line is wound back:
                // the sweep is what brings it in.
                view.SetRotWash(cell, 0f);
            }
            if (e.Cells.Count == 0)
            {
                return null;
            }
            Vector2 first = view.CellToWorld(e.Cells[0]);
            Vector2 last = view.CellToWorld(e.Cells[e.Cells.Count - 1]);
            e.Centre = (first + last) * 0.5f;
            e.Length = (last - first).magnitude + cellSize;
            if (streakMaterial != null)
            {
                e.Front = Rent(ViewUtil.WhiteSprite, SweepOrder, streakMaterial);
            }
            // Its band comes in with it, from nothing.
            Band band = EnsureBand(death.IsRow, death.Line);
            if (band != null)
            {
                band.Along = 0f;
            }
            scene.Extinguishes.Add(e);
            return e;
        }

        private void AddPressure(GridPos from, GridPos to, float start, float end)
        {
            if (streakMaterial == null || !board.IsInside(from))
            {
                return;
            }
            var p = new Pressure
            {
                From = view.CellToWorld(from),
                To = view.CellToWorld(to),
                Start = start,
                End = end,
                Seed = (Hash(to.X * 31 + 1, to.Y * 29 + 2) & 4095u) / 4095f * 6.283f
            };
            p.Renderer = Rent(ViewUtil.WhiteSprite, PathOrder, streakMaterial);
            scene.Pressures.Add(p);
        }

        /// <summary>The debug outline on the line that just died and the edge line its rot jumped
        /// to, held through the pause so the steps of a cascade can be counted.</summary>
        private void AddStepBreak(LineDeath death, float start, float end)
        {
            AddStepBreakLine(death.IsRow, death.Line, start, end);
            if (death.EdgeLine != death.Line)
            {
                AddStepBreakLine(death.IsRow, death.EdgeLine, start, end);
            }
        }

        private void AddStepBreakLine(bool isRow, int line, float start, float end)
        {
            int count = isRow ? board.Width : board.Height;
            GridPos a = isRow ? new GridPos(board.MinX, line) : new GridPos(line, board.MinY);
            GridPos b = isRow
                ? new GridPos(board.MinX + count - 1, line)
                : new GridPos(line, board.MinY + count - 1);
            var mark = new StepBreak
            {
                Start = start,
                End = end,
                Centre = (view.CellToWorld(a) + view.CellToWorld(b)) * 0.5f,
                Size = isRow
                    ? new Vector2(count * cellSize, cellSize)
                    : new Vector2(cellSize, count * cellSize)
            };
            mark.Renderer = Rent(MarkerSprite(), DebugOrder, null);
            scene.Breaks.Add(mark);
        }

        // =================================================================== the clock

        private void Update()
        {
            if (view == null || board == null)
            {
                return;
            }
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (scene != null)
            {
                scene.Clock += dt;
            }
            PaintScene();
            PaintPresence();
            PaintBands();
            if (scene != null && scene.Clock >= scene.End)
            {
                Finish();
            }
        }

        /// <summary>The standing tissue: its slow dark breath, and the one pulse they share the turn
        /// the rot is billed. Desynchronised per cell, and thinned as the patch grows.</summary>
        private void PaintPresence()
        {
            float thin = IdleScale(presence.Count);
            float shared = SharedPulse();
            foreach (KeyValuePair<GridPos, Presence> entry in presence)
            {
                Presence p = entry.Value;
                float breath = 0.5f - 0.5f * Mathf.Cos(
                    (Time.time / Mathf.Max(p.Period, 0.1f) + p.Phase) * 6.2831853f);
                float dark = Mathf.Clamp01(Style.IdlePulseStrength * thin * breath + shared);
                Vector2 at = view.CellToWorld(entry.Key);
                Color tint = Color.Lerp(Color.white, PulseTint, dark);
                PlaceTurned(p.Surface, at, cubeSize, tint, view.RotLight(entry.Key), p.Turn);
            }
        }

        private float SharedPulse()
        {
            if (scene == null || scene.PulseEnd <= scene.PulseStart || scene.Clock < scene.PulseStart)
            {
                return 0f;
            }
            float k = Mathf.InverseLerp(scene.PulseStart, scene.PulseEnd, scene.Clock);
            return k >= 1f ? 0f : Style.TurnDamagePulseStrength * Mathf.Sin(Mathf.PI * k);
        }

        /// <summary>How far the presence is turned down on a board the rot has taken a lot of: one
        /// rotten cube may breathe, thirty of them must not read as noise.</summary>
        private static float IdleScale(int count)
        {
            float over = Mathf.Max(0, count - 8) / 14f;
            return 1f / (1f + Mathf.Max(Style.LargeClusterCompensation, 0f) * over);
        }

        private void PaintBands()
        {
            foreach (KeyValuePair<int, Band> entry in bands)
            {
                Band band = entry.Value;
                int count = band.IsRow ? board.Width : board.Height;
                GridPos a = band.IsRow ? new GridPos(board.MinX, band.Line) : new GridPos(band.Line, board.MinY);
                GridPos b = band.IsRow
                    ? new GridPos(board.MinX + count - 1, band.Line)
                    : new GridPos(band.Line, board.MinY + count - 1);
                Vector2 centre = (view.CellToWorld(a) + view.CellToWorld(b)) * 0.5f;
                // Past the end cells and proud of the line on both sides: a dead line is a scar
                // the board carries, and a band hidden under the cubes would carry nothing.
                float length = (count - 1) * cellSize + cellSize * 1.2f;
                float width = cellSize * 1.2f;
                if (streakMaterial == null)
                {
                    // No shader: a flat rounded plate, grown along the line by the sweep.
                    float grown = band.Along >= 1f ? 1f : band.Along;
                    PlaceRect(band.Renderer, centre, length * grown, width, BandColour,
                        Style.DeadLineUnderlayOpacity * 0.8f, band.IsRow ? 0f : 90f);
                    continue;
                }
                PlaceRect(band.Renderer, centre, length, width, Color.white, 1f, band.IsRow ? 0f : 90f);
                Block();
                block.SetFloat(ModeId, 0f);
                block.SetVector(SpanId, new Vector4(length / cellSize * 0.5f, width / cellSize * 0.5f,
                    Style.DeadLineUnderlayRadius, 0.06f));
                block.SetFloat(AlongId, band.Along);
                block.SetFloat(SoftId, FrontSoftness);
                block.SetFloat(CentreOutId, Style.DeadLineSweepMode == SweepMode.CentreOut ? 1f : 0f);
                block.SetFloat(OpacityId, Style.DeadLineUnderlayOpacity);
                block.SetFloat(GrainId, Style.DeadLineTextureStrength);
                block.SetFloat(CrackVisId, Layers.ShowCrackLayer ? Style.CrackVisibility * 1.2f : 0f);
                block.SetFloat(ShadowStrengthId, Style.DeadLineShadowStrength);
                block.SetFloat(SeedId, band.Seed);
                block.SetColor(BandColourId, BandColour);
                block.SetColor(VeinColourId, VeinColour);
                band.Renderer.SetPropertyBlock(block);
            }
        }

        // =================================================================== the turn, frame by frame

        private void PaintScene()
        {
            if (scene == null)
            {
                return;
            }
            float now = scene.Clock;
            for (int i = 0; i < scene.Conversions.Count; i++)
            {
                PaintConversion(scene.Conversions[i], now);
            }
            for (int i = 0; i < scene.Growths.Count; i++)
            {
                PaintGrowth(scene.Growths[i], now);
            }
            for (int i = 0; i < scene.Extinguishes.Count; i++)
            {
                PaintExtinguish(scene.Extinguishes[i], now);
            }
            for (int i = 0; i < scene.Pressures.Count; i++)
            {
                PaintPressure(scene.Pressures[i], now);
            }
            for (int i = 0; i < scene.Contacts.Count; i++)
            {
                PaintContact(scene.Contacts[i], now);
            }
            for (int i = 0; i < scene.Breaks.Count; i++)
            {
                StepBreak mark = scene.Breaks[i];
                float k = now < mark.Start || now > mark.End ? 0f
                    : Mathf.Sin(Mathf.PI * Mathf.InverseLerp(mark.Start, mark.End, now));
                PlaceRect(mark.Renderer, mark.Centre, mark.Size.x, mark.Size.y, DebugColour, 0.8f * k, 0f);
            }
            if (scene.SourceLine != null)
            {
                Vector2 span = scene.SourceTo - scene.SourceFrom;
                PlaceRect(scene.SourceLine, (scene.SourceFrom + scene.SourceTo) * 0.5f,
                    span.magnitude, 0.05f * cellSize, DebugColour, 0.85f,
                    Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            }
        }

        /// <summary>B: a cube converting where it stands. The dead layer underneath is uncovered by
        /// the front; the dying layer on top keeps its own face and gives its material up.</summary>
        private void PaintConversion(Conversion c, float now)
        {
            if (c.Done)
            {
                return;
            }
            if (now < c.Start)
            {
                // Still standing, exactly as the board drew it: the board is holding the cell, so
                // its own cube has to be shown here until the rot reaches it.
                PaintStill(c);
                return;
            }
            float p = Mathf.Clamp01(Mathf.InverseLerp(c.Start, c.End, now));
            // It draws in as it gives way and settles back to the board's own size, so the
            // hand-off at the end shows nothing.
            float collapse = 1f - Style.SurfaceCollapseAmount * Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.7f));
            float size = cubeSize * collapse;
            if (gangreneMaterial == null)
            {
                // No shader: the face cross-fades to dead tissue. The front is what is missing.
                PlaceAxes(c.Rot, c.At, size, size, RotColour, Smooth(p / 0.8f));
                if (c.Body != null)
                {
                    PlaceAxes(c.Body, c.At, size, size,
                        Color.Lerp(c.Before.Colour, RotColour, Smooth(p)), 1f - Smooth((p - 0.35f) / 0.65f));
                }
            }
            else
            {
                PlaceAxes(c.Rot, c.At, size, size, RotColour, 1f);
                SetCubeBlock(c.Rot, c.At, size, 1f, FrontAt(p), c.Turn, c.Variant);
                if (c.Body != null)
                {
                    PlaceAxes(c.Body, c.At, size, size, c.Before.Colour, 1f);
                    SetCubeBlock(c.Body, c.At, size, 0f, FrontAt(p), c.Turn, c.Variant);
                }
            }
            if (p >= 1f)
            {
                Land(c.Cell);
                Return(c.Rot);
                Return(c.Body);
                c.Rot = null;
                c.Body = null;
                c.Done = true;
            }
        }

        /// <summary>The cube as it stood, drawn for the board while the board is holding its cell.</summary>
        private void PaintStill(Conversion c)
        {
            PlaceAxes(c.Rot, c.At, cubeSize, cubeSize, RotColour, 0f);
            if (c.Body == null)
            {
                return;
            }
            PlaceAxes(c.Body, c.At, cubeSize, cubeSize, c.Before.Colour, 1f);
            if (gangreneMaterial != null)
            {
                SetCubeBlock(c.Body, c.At, cubeSize, 0f, FrontAt(0f), c.Turn, c.Variant);
            }
        }

        /// <summary>A: an empty cell taken. Its floor is contaminated from the source side, then the
        /// dead mass rises out of it, locks and settles.</summary>
        private void PaintGrowth(Growth g, float now)
        {
            if (g.Done || now < g.Start)
            {
                if (g.Stain != null)
                {
                    g.Stain.color = Clear;
                }
                if (g.Body != null)
                {
                    g.Body.color = Clear;
                }
                if (g.Shadow != null)
                {
                    g.Shadow.color = Clear;
                }
                return;
            }
            float p = Mathf.Clamp01(Mathf.InverseLerp(g.Start, g.End, now));
            // The floor goes first, over a little more than half the phase.
            if (g.Stain != null)
            {
                float floor = Mathf.Clamp01(p / 0.55f);
                if (gangreneMaterial == null)
                {
                    PlaceAxes(g.Stain, g.At, slotSize, slotSize, StainColour,
                        Style.FloorContaminationAmount * Smooth(floor));
                }
                else
                {
                    PlaceAxes(g.Stain, g.At, slotSize, slotSize,
                        Color.white, Style.FloorContaminationAmount);
                    SetCubeBlock(g.Stain, g.At, slotSize, 2f, FrontAt(floor), g.Turn, g.Variant);
                }
            }
            // Then the mass itself: up out of the floor, then locked down and settled.
            float rise = Mathf.Clamp01((p - 0.42f) / 0.58f);
            float settleAt = 1f - Mathf.Clamp01(Style.SpawnSettleDuration
                / Mathf.Max(g.End - g.Start, 0.0001f));
            float lift = rise <= 0f ? 0f
                : Style.SpawnRiseAmount * cellSize * Mathf.Sin(Mathf.PI * Smooth(rise));
            float grow = Mathf.Lerp(0.74f, 1f, Smooth(rise / Mathf.Max(settleAt, 0.01f)));
            // A hair wider than tall as it lands - dead mass settling, not a bounce.
            float squash = p > settleAt
                ? Style.SurfaceCollapseAmount * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(settleAt, 1f, p))
                : 0f;
            var at = new Vector2(g.At.x, g.At.y + lift);
            float w = cubeSize * grow * (1f + squash);
            float h = cubeSize * grow * (1f - squash);
            if (g.Body != null)
            {
                PlaceAxes(g.Body, at, w, h, RotColour, rise > 0f ? 1f : 0f);
                if (gangreneMaterial != null && rise > 0f)
                {
                    SetCubeBlock(g.Body, at, Mathf.Max(w, h), 1f, FrontAt(1f), g.Turn, g.Variant);
                }
            }
            if (g.Shadow != null)
            {
                float shade = rise > 0f ? ShadowAlpha * Mathf.Clamp01(lift / (0.02f * cellSize)) : 0f;
                PlaceAxes(g.Shadow, new Vector2(g.At.x, g.At.y - 0.02f * cellSize),
                    w * 1.02f, h * 1.02f, ShadowColour, shade);
            }
            if (p >= 1f)
            {
                Land(g.Cell);
                Return(g.Stain);
                Return(g.Shadow);
                Return(g.Body);
                g.Stain = null;
                g.Shadow = null;
                g.Body = null;
                g.Done = true;
            }
        }

        /// <summary>E: the life going out along a line. The front travels, the cells' wash comes in
        /// behind it, and the band under the line is swept in with it.</summary>
        private void PaintExtinguish(Extinguish e, float now)
        {
            if (e.Done)
            {
                return;
            }
            float p = now <= e.Start ? 0f : Mathf.Clamp01(Mathf.InverseLerp(e.Start, e.End, now));
            float front = Smooth(p) * (1f + FrontSoftness);
            bool centreOut = Style.DeadLineSweepMode == SweepMode.CentreOut;
            for (int i = 0; i < e.Cells.Count; i++)
            {
                float d = centreOut ? Mathf.Abs(e.Along[i] - 0.5f) * 2f : e.Along[i];
                view.SetRotWash(e.Cells[i], SmoothBetween(d - FrontSoftness, d + FrontSoftness, front));
            }
            Band band;
            if (bands.TryGetValue(e.Key, out band))
            {
                band.Along = front;
            }
            if (e.Front != null)
            {
                PlaceRect(e.Front, e.Centre, e.Length, cellSize, Color.white, 1f, e.Angle);
                Block();
                block.SetFloat(ModeId, 2f);
                block.SetVector(SpanId, new Vector4(e.Length / cellSize * 0.5f, 0.5f, 0.2f, 0.06f));
                block.SetFloat(AlongId, front);
                block.SetFloat(SoftId, FrontSoftness);
                block.SetFloat(CentreOutId, centreOut ? 1f : 0f);
                block.SetFloat(OpacityId, p >= 1f ? 0f : 0.6f);
                block.SetFloat(SeedId, 0f);
                block.SetColor(VeinColourId, VeinColour);
                e.Front.SetPropertyBlock(block);
            }
            if (p >= 1f)
            {
                Return(e.Front);
                e.Front = null;
                e.Done = true;
            }
        }

        /// <summary>F: the pressure travelling from the dead line to the cube the jump is about to
        /// take. It says WHICH cube, before it goes.</summary>
        private void PaintPressure(Pressure p, float now)
        {
            if (p.Renderer == null)
            {
                return;
            }
            float k = now <= p.Start ? 0f : Mathf.Clamp01(Mathf.InverseLerp(p.Start, p.End, now));
            Vector2 span = p.To - p.From;
            float length = span.magnitude;
            if (k <= 0f || k >= 1f || length <= 0.0001f)
            {
                p.Renderer.color = Clear;
                return;
            }
            PlaceRect(p.Renderer, (p.From + p.To) * 0.5f, length, PathWidth * cellSize,
                Color.white, 1f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
            Block();
            block.SetFloat(ModeId, 1f);
            block.SetVector(SpanId, new Vector4(length / cellSize * 0.5f, PathWidth * 0.5f, 0.1f, 0.05f));
            block.SetFloat(AlongId, Smooth(k) * 1.05f);
            block.SetFloat(SoftId, FrontSoftness);
            block.SetFloat(OpacityId, Style.EdgeTransferVisibility);
            block.SetFloat(VeinStrengthId, Layers.ShowVeinLayer ? Style.EdgeTransferVeinStrength : 0f);
            block.SetFloat(TailId, 0.55f);
            block.SetFloat(SeedId, p.Seed);
            block.SetColor(BandColourId, PressureColour);
            block.SetColor(VeinColourId, VeinColour);
            p.Renderer.SetPropertyBlock(block);
        }

        /// <summary>C: a cube the rot could not take, stained for a breath where it pressed.</summary>
        private void PaintContact(Contact c, float now)
        {
            if (c.Renderer == null)
            {
                return;
            }
            float k = now < c.Start || now > c.End ? 0f
                : Mathf.Sin(Mathf.PI * Mathf.InverseLerp(c.Start, c.End, now));
            PlaceRect(c.Renderer, c.At, cubeSize, cubeSize, StainColour, 0.55f * k, c.Angle);
        }

        /// <summary>A cell is finished: the board takes it back and draws what really stands there,
        /// and its tissue overlay picks up from the same frame.</summary>
        private void Land(GridPos cell)
        {
            cellBuffer.Clear();
            cellBuffer.Add(cell);
            view.ReleaseCells(cellBuffer);
            for (int i = scene.Held.Count - 1; i >= 0; i--)
            {
                if (scene.Held[i].X == cell.X && scene.Held[i].Y == cell.Y)
                {
                    scene.Held.RemoveAt(i);
                }
            }
        }

        /// <summary>Everything playing ends where the rules already are: the cells go back, the
        /// wash goes fully on, the bands stand whole.</summary>
        private void Finish()
        {
            if (scene == null)
            {
                return;
            }
            Scene done = scene;
            scene = null;
            for (int i = 0; i < done.Conversions.Count; i++)
            {
                Return(done.Conversions[i].Rot);
                Return(done.Conversions[i].Body);
            }
            for (int i = 0; i < done.Growths.Count; i++)
            {
                Return(done.Growths[i].Stain);
                Return(done.Growths[i].Shadow);
                Return(done.Growths[i].Body);
            }
            for (int i = 0; i < done.Extinguishes.Count; i++)
            {
                Return(done.Extinguishes[i].Front);
            }
            for (int i = 0; i < done.Pressures.Count; i++)
            {
                Return(done.Pressures[i].Renderer);
            }
            for (int i = 0; i < done.Contacts.Count; i++)
            {
                Return(done.Contacts[i].Renderer);
            }
            for (int i = 0; i < done.Breaks.Count; i++)
            {
                Return(done.Breaks[i].Renderer);
            }
            Return(done.SourceLine);
            foreach (KeyValuePair<int, Band> entry in bands)
            {
                entry.Value.Along = 1f;
            }
            if (view != null)
            {
                if (done.Held.Count > 0)
                {
                    view.ReleaseCells(done.Held);
                }
                view.ClearRotWash();
            }
        }

        // =================================================================== the shader block

        /// <summary>Where the front stands at <paramref name="p"/> of the phase. It starts BEHIND
        /// the first pixel and ends PAST the last one: a front that stopped at 1 would leave the
        /// dying layer still half standing on the pixels that die last, and one that started at 0
        /// would have killed the first pixel before the phase began.</summary>
        private static float FrontAt(float p)
        {
            return Mathf.Lerp(-2f * FaceSoftness, 1f + 2f * FaceSoftness, Mathf.Clamp01(p));
        }

        private void SetCubeBlock(SpriteRenderer r, Vector2 at, float size, float mode, float front,
            int side, int variant)
        {
            if (gangreneMaterial == null || r == null)
            {
                return;
            }
            Block();
            Vector3 centre = transform.TransformPoint(new Vector3(at.x, at.y, 0f));
            float scale = transform.lossyScale.x;
            block.SetVector(CentreId, new Vector4(centre.x, centre.y, 0f, 0f));
            block.SetFloat(HalfId, Mathf.Max(size * 0.5f * (scale > 0f ? scale : 1f), 1e-4f));
            block.SetVector(TilesId, new Vector4(variant, 1f / Variants, 0f, 0f));
            block.SetVector(AxesId, AxesFor(side));
            block.SetFloat(ModeId, mode);
            block.SetFloat(FrontId, front);
            block.SetFloat(SoftId, FaceSoftness);
            block.SetFloat(CreepId, Style.SourceCreepStrength);
            block.SetFloat(EdgeBiasId, 0.35f);
            block.SetFloat(DrainId, Style.ColorDrainStrength);
            block.SetFloat(DrainReachId, 0.45f);
            block.SetFloat(MatteId, Style.SurfaceMatteAmount);
            block.SetFloat(VeinVisId, Layers.ShowVeinLayer ? Style.VeinVisibility : 0f);
            block.SetFloat(VeinAheadId, Style.VeinSpeed);
            block.SetFloat(LookVisId, 1f);
            block.SetColor(RotColourId, RotColour);
            block.SetColor(VeinColourId, VeinColour);
            block.SetColor(StainColourId, StainColour);
            block.SetTexture(LookTexId, LookAtlas());
            block.SetTexture(DataTexId, DataAtlas());
            r.SetPropertyBlock(block);
        }

        private void Block()
        {
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            block.Clear();
        }

        /// <summary>The face turned so the SOURCE edge is always u = 0, as the rows of a 2x2: the
        /// front runs along the first row, and the second is it turned a quarter left, so the
        /// pattern a rotated sprite draws and the pattern the shader draws are the same one.</summary>
        private static Vector4 AxesFor(int side)
        {
            Vector2 axis = SideAxis(side);
            return new Vector4(axis.x, axis.y, -axis.y, axis.x);
        }

        /// <summary>Which way the necrosis runs INTO the cell: away from the side it came from.</summary>
        private static Vector2 SideAxis(int side)
        {
            switch (side & 3)
            {
                case 0: return new Vector2(-1f, 0f);  // the rot came from the right
                case 1: return new Vector2(0f, -1f);  // from above
                case 2: return new Vector2(1f, 0f);   // from the left
                default: return new Vector2(0f, 1f);  // from below
            }
        }

        /// <summary>Which side Core says the rot came in from - never worked out here. A seed, which
        /// touches nothing, takes the cell's own hash so its tissue still has a grain.</summary>
        private static int TurnFromSource(GridPos cell, GridPos? source)
        {
            if (!source.HasValue)
            {
                return (int)(Hash(cell.X, cell.Y) & 3u);
            }
            int dx = source.Value.X - cell.X;
            int dy = source.Value.Y - cell.Y;
            if (dx > 0)
            {
                return 0;
            }
            if (dx < 0)
            {
                return 2;
            }
            return dy > 0 ? 1 : 3;
        }

        /// <summary>An edge cube the jump took: the pressure came from the dead line, so that is the
        /// side its tissue grows from.</summary>
        private static int TurnTowardLine(GridPos cell, bool isRow, int line)
        {
            if (isRow)
            {
                return line > cell.Y ? 1 : 3;
            }
            return line > cell.X ? 0 : 2;
        }

        private int TurnFor(GridPos cell)
        {
            int side;
            if (sourceTurn.TryGetValue(cell, out side))
            {
                return side;
            }
            return (int)(Hash(cell.X, cell.Y) & 3u);
        }

        private static int VariantFor(uint seed)
        {
            if (Style.VisualSeedVariation <= 0f)
            {
                return 0;
            }
            int used = 1 + (int)(Mathf.Clamp01(Style.VisualSeedVariation) * (Variants - 1));
            return (int)(seed % (uint)Mathf.Max(used, 1));
        }

        // =================================================================== renderers

        /// <summary>A square sprite whose one unit is its full size, never turned.</summary>
        private static void PlaceAxes(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha)
        {
            if (r == null)
            {
                return;
            }
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.identity;
            r.transform.localScale = new Vector3(width, height, 1f);
        }

        /// <summary>The same, turned so the sprite's own x runs the way the necrosis does.</summary>
        private static void PlaceTurned(SpriteRenderer r, Vector2 at, float size, Color colour,
            float alpha, int side)
        {
            if (r == null)
            {
                return;
            }
            Vector2 axis = SideAxis(side);
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg);
            r.transform.localScale = new Vector3(size, size, 1f);
        }

        private static void PlaceRect(SpriteRenderer r, Vector2 at, float width, float height,
            Color colour, float alpha, float angle)
        {
            if (r == null)
            {
                return;
            }
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
            Vector2 unit = r.sprite != null
                ? new Vector2(r.sprite.rect.width, r.sprite.rect.height) / r.sprite.pixelsPerUnit
                : Vector2.one;
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localScale = new Vector3(width / Mathf.Max(unit.x, 0.0001f),
                height / Mathf.Max(unit.y, 0.0001f), 1f);
        }

        private SpriteRenderer Rent(Sprite sprite, int order, Material material)
        {
            SpriteRenderer r;
            if (spareRenderers.Count > 0)
            {
                r = spareRenderers.Pop();
            }
            else
            {
                var go = new GameObject("Gangrene");
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
            spareRenderers.Push(r);
        }

        // =================================================================== maths

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float SmoothBetween(float a, float b, float x)
        {
            return Smooth(Mathf.Approximately(a, b) ? (x >= b ? 1f : 0f) : (x - a) / (b - a));
        }

        private static uint Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        private struct Dice
        {
            private uint state;

            public Dice(uint seed)
            {
                state = seed == 0u ? 0x9E3779B9u : seed;
            }

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) * (1f / 16777216f);
            }

            public float Range(float a, float b)
            {
                return a + (b - a) * Next();
            }
        }

        // =================================================================== shaders and art

        private static bool shadersLooked;

        private static Material gangreneMaterial;

        private static Material streakMaterial;

        private static Texture2D dataAtlas;

        private static Texture2D lookAtlas;

        private static Sprite[] lookSprites;

        private static Sprite slotSprite;

        private static Sprite shadowSprite;

        private static Sprite contactSprite;

        private static Sprite markerSprite;

        private static Sprite lineSprite;

        /// <summary>The look the surface was last baked at: the visibilities and the toggles. The
        /// lab turns those knobs at runtime, so the texture has to be able to follow.</summary>
        private static int bakedSignature;

        private static readonly int LookTexId = Shader.PropertyToID("_LookTex");
        private static readonly int DataTexId = Shader.PropertyToID("_DataTex");
        private static readonly int CentreId = Shader.PropertyToID("_Centre");
        private static readonly int HalfId = Shader.PropertyToID("_Half");
        private static readonly int TilesId = Shader.PropertyToID("_Tiles");
        private static readonly int AxesId = Shader.PropertyToID("_Axes");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int FrontId = Shader.PropertyToID("_Front");
        private static readonly int SoftId = Shader.PropertyToID("_Soft");
        private static readonly int CreepId = Shader.PropertyToID("_Creep");
        private static readonly int EdgeBiasId = Shader.PropertyToID("_EdgeBias");
        private static readonly int DrainId = Shader.PropertyToID("_Drain");
        private static readonly int DrainReachId = Shader.PropertyToID("_DrainReach");
        private static readonly int MatteId = Shader.PropertyToID("_Matte");
        private static readonly int VeinVisId = Shader.PropertyToID("_VeinVis");
        private static readonly int VeinAheadId = Shader.PropertyToID("_VeinAhead");
        private static readonly int LookVisId = Shader.PropertyToID("_LookVis");
        private static readonly int RotColourId = Shader.PropertyToID("_RotColour");
        private static readonly int VeinColourId = Shader.PropertyToID("_VeinColour");
        private static readonly int StainColourId = Shader.PropertyToID("_StainColour");
        private static readonly int SpanId = Shader.PropertyToID("_Span");
        private static readonly int AlongId = Shader.PropertyToID("_Along");
        private static readonly int CentreOutId = Shader.PropertyToID("_CentreOut");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int GrainId = Shader.PropertyToID("_Grain");
        private static readonly int CrackVisId = Shader.PropertyToID("_CrackVis");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int VeinStrengthId = Shader.PropertyToID("_VeinStrength");
        private static readonly int TailId = Shader.PropertyToID("_Tail");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int BandColourId = Shader.PropertyToID("_BandColour");

        /// <summary>Finds both shaders once. Shader.Find works in the editor; the Resources copy is
        /// what survives into a build. A shader that is missing or that this device cannot run
        /// leaves its material null - the fallbacks.</summary>
        private static void EnsureArt()
        {
            if (shadersLooked)
            {
                return;
            }
            shadersLooked = true;
            Shader cube = FindShader("ProjectBlock/Gangrene", "Shaders/Gangrene");
            if (cube != null)
            {
                gangreneMaterial = new Material(cube);
                gangreneMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            Shader streak = FindShader("ProjectBlock/GangreneStreak", "Shaders/GangreneStreak");
            if (streak != null)
            {
                streakMaterial = new Material(streak);
                streakMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private static Shader FindShader(string name, string resource)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                shader = Resources.Load<Shader>(resource);
            }
            return shader != null && shader.isSupported ? shader : null;
        }

        /// <summary>The face a rotten cube wears on the board: the default tile, tinted. Never the
        /// card's own element tile - a fox's face on dead tissue reads as the art having failed.</summary>
        private static Sprite RotTile()
        {
            return ViewUtil.CubeTile(CubeKind.Gangrene, null);
        }

        // ---- the two small textures, a tile per variant -------------------------------------

        /// <summary>The RAW channels of dead tissue: veins (R) running in from the u = 0 edge and
        /// branching once, hairline cracks (G), matte mottling (B), and the front's own
        /// irregularity (A). Linear, so the shader reads the numbers and not a gamma curve.</summary>
        private static Texture2D DataAtlas()
        {
            if (dataAtlas != null)
            {
                return dataAtlas;
            }
            int width = TileSize * Variants;
            var px = new Color32[width * TileSize];
            for (int v = 0; v < Variants; v++)
            {
                float[] veins = VeinField(v);
                float[] cracks = CrackField(v);
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        int i = y * TileSize + x;
                        var q = new Vector2((x + 0.5f) / TileSize * 2f - 1f,
                            (y + 0.5f) / TileSize * 2f - 1f);
                        px[y * width + v * TileSize + x] = new Color32(
                            Byte(veins[i]), Byte(cracks[i]), Byte(Mottle(v, q)), Byte(FrontNoise(v, q)));
                    }
                }
            }
            dataAtlas = new Texture2D(width, TileSize, TextureFormat.RGBA32, false, true);
            dataAtlas.hideFlags = HideFlags.HideAndDontSave;
            dataAtlas.filterMode = FilterMode.Bilinear;
            dataAtlas.wrapMode = TextureWrapMode.Clamp;
            dataAtlas.SetPixels32(px);
            dataAtlas.Apply(false, true);
            return dataAtlas;
        }

        /// <summary>
        /// The BAKED surface: the three layers - matte mottling, veins, cracks - collapsed into the
        /// single over-blend they add up to (rgb the colour to go to, a the coverage). The shader
        /// blends this with one lerp and the standing tissue's sprite blends it with plain alpha, so
        /// the two are pixel for pixel the same picture and the hand-off at the end of a conversion
        /// shows nothing at all.
        /// </summary>
        private static Texture2D LookAtlas()
        {
            int signature = Signature();
            if (lookAtlas != null && bakedSignature == signature)
            {
                return lookAtlas;
            }
            bakedSignature = signature;
            int width = TileSize * Variants;
            var px = new Color32[width * TileSize];
            float veinVis = Layers.ShowVeinLayer ? Style.VeinVisibility : 0f;
            float crackVis = Layers.ShowCrackLayer ? Style.CrackVisibility : 0f;
            for (int v = 0; v < Variants; v++)
            {
                float[] veins = VeinField(v);
                float[] cracks = CrackField(v);
                for (int y = 0; y < TileSize; y++)
                {
                    for (int x = 0; x < TileSize; x++)
                    {
                        int i = y * TileSize + x;
                        var q = new Vector2((x + 0.5f) / TileSize * 2f - 1f,
                            (y + 0.5f) / TileSize * 2f - 1f);
                        float a1 = Style.SurfaceMatteAmount * (0.35f + 0.65f * Mottle(v, q));
                        float a2 = veinVis * veins[i];
                        float a3 = crackVis * cracks[i];
                        float alpha = 1f - (1f - a1) * (1f - a2) * (1f - a3);
                        Color rgb = alpha <= 0.0001f ? MatteColour
                            : (MatteColour * a1 * (1f - a2) * (1f - a3)
                                + VeinColour * a2 * (1f - a3) + CrackColour * a3) / alpha;
                        px[y * width + v * TileSize + x] = new Color32(
                            Byte(rgb.r), Byte(rgb.g), Byte(rgb.b), Byte(alpha));
                    }
                }
            }
            if (lookAtlas == null)
            {
                lookAtlas = new Texture2D(width, TileSize, TextureFormat.RGBA32, false, false);
                lookAtlas.hideFlags = HideFlags.HideAndDontSave;
                lookAtlas.filterMode = FilterMode.Bilinear;
                lookAtlas.wrapMode = TextureWrapMode.Clamp;
            }
            lookAtlas.SetPixels32(px);
            lookAtlas.Apply(false, false);
            lookSprites = null;
            return lookAtlas;
        }

        /// <summary>Which knobs the baked surface depends on, so the lab turning one rebuilds it.</summary>
        private static int Signature()
        {
            unchecked
            {
                int h = Mathf.RoundToInt(Style.VeinVisibility * 1000f);
                h = h * 31 + Mathf.RoundToInt(Style.CrackVisibility * 1000f);
                h = h * 31 + Mathf.RoundToInt(Style.SurfaceMatteAmount * 1000f);
                h = h * 31 + Style.VeinCount;
                h = h * 31 + (Layers.ShowVeinLayer ? 1 : 0);
                h = h * 31 + (Layers.ShowCrackLayer ? 2 : 0);
                return h;
            }
        }

        /// <summary>One variant's standing tissue as a sprite, for the cells that simply carry it.</summary>
        private static Sprite LookSprite(int variant)
        {
            Texture2D atlas = LookAtlas();
            if (lookSprites == null)
            {
                lookSprites = new Sprite[Variants];
            }
            variant = Mathf.Clamp(variant, 0, Variants - 1);
            if (lookSprites[variant] == null)
            {
                lookSprites[variant] = Sprite.Create(atlas,
                    new Rect(variant * TileSize, 0, TileSize, TileSize),
                    new Vector2(0.5f, 0.5f), TileSize);
                lookSprites[variant].hideFlags = HideFlags.HideAndDontSave;
            }
            return lookSprites[variant];
        }

        private static byte Byte(float v)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f);
        }

        /// <summary>Veins: a few paths creeping IN from the u = 0 edge (the side the rot comes from),
        /// each wandering, thinning as it goes, and branching once. No Random - a variant always
        /// draws the same tissue.</summary>
        private static float[] VeinField(int variant)
        {
            var field = new float[TileSize * TileSize];
            int count = Mathf.Clamp(Style.VeinCount, 1, 12);
            var dice = new Dice(Hash(variant * 31 + 7, 1013));
            for (int k = 0; k < count; k++)
            {
                float y0 = -0.9f + 1.8f * (k + 0.5f) / count + dice.Range(-0.22f, 0.22f);
                float phase = dice.Range(0f, 6.283f);
                float curl = dice.Range(0.25f, 0.7f);
                // HOW FAR IN IT GETS. Veins that all crossed the whole face read as wood grain;
                // these run out at their own depths, which is what makes it tissue.
                float reach = dice.Range(0.8f, 1.9f);
                float amp = dice.Range(0.55f, 1f);
                float branchAt = dice.Range(0.25f, 0.6f);
                float branchSlope = dice.Range(-0.9f, 0.9f);
                // No drift of its own: a vein creeps IN from the source edge and wanders.
                WalkVein(field, -1.05f, y0, phase, curl, 0.05f, 0f, reach, amp);
                // One branch, from partway along, shorter and fainter: tissue does not fork evenly.
                float bx = -1.05f + reach * branchAt;
                float by = y0 + curl * 0.35f * Mathf.Sin(branchAt * 4.2f + phase);
                WalkVein(field, bx, by, phase + 1.7f, curl * 0.9f, 0.03f, branchSlope,
                    reach * dice.Range(0.3f, 0.6f), amp * 0.8f);
            }
            return field;
        }

        private static void WalkVein(float[] field, float x0, float y0, float phase, float curl,
            float width, float slope, float reach, float amp)
        {
            float x = x0;
            float y = y0;
            const float step = 0.04f;
            float gone = 0f;
            while (gone < reach && x < 1.1f)
            {
                float t = gone / Mathf.Max(reach, 1e-4f);
                float nx = x + step;
                float ny = y + step * (curl * Mathf.Cos(x * 4.2f + phase) + slope * 0.25f);
                // It thins AND fades toward its tip: a vein runs out, it does not stop dead.
                Stamp(field, x, y, nx, ny, width * (1f - 0.6f * t),
                    amp * (1f - Smooth((t - 0.72f) / 0.28f)));
                x = nx;
                y = ny;
                gone += step;
            }
        }

        /// <summary>Lays one segment of a vein into the field, softly - so it is a vein and not a
        /// drawn line.</summary>
        private static void Stamp(float[] field, float ax, float ay, float bx, float by, float width,
            float amp)
        {
            float minX = Mathf.Min(ax, bx) - width * 2f;
            float maxX = Mathf.Max(ax, bx) + width * 2f;
            float minY = Mathf.Min(ay, by) - width * 2f;
            float maxY = Mathf.Max(ay, by) + width * 2f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX + 1f) * 0.5f * TileSize));
            int x1 = Mathf.Min(TileSize - 1, Mathf.CeilToInt((maxX + 1f) * 0.5f * TileSize));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((minY + 1f) * 0.5f * TileSize));
            int y1 = Mathf.Min(TileSize - 1, Mathf.CeilToInt((maxY + 1f) * 0.5f * TileSize));
            var a = new Vector2(ax, ay);
            var ab = new Vector2(bx - ax, by - ay);
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2((x + 0.5f) / TileSize * 2f - 1f, (y + 0.5f) / TileSize * 2f - 1f);
                    float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / len2);
                    float d = (q - (a + ab * u)).magnitude;
                    float v = amp * (1f - Smooth((d - width * 0.35f) / Mathf.Max(width, 1e-4f)));
                    int i = y * TileSize + x;
                    if (v > field[i])
                    {
                        field[i] = v;
                    }
                }
            }
        }

        /// <summary>Hairline cracks: a handful of short splits across the face, in patches.</summary>
        private static float[] CrackField(int variant)
        {
            var field = new float[TileSize * TileSize];
            var dice = new Dice(Hash(variant * 17 + 3, 7717));
            int count = 5;
            for (int k = 0; k < count; k++)
            {
                float cx = dice.Range(-0.7f, 0.7f);
                float cy = dice.Range(-0.7f, 0.7f);
                float angle = dice.Range(0f, 3.1416f);
                float half = dice.Range(0.16f, 0.42f);
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Two slightly kinked halves, so a crack is never a straight ruler line.
                var mid = new Vector2(cx, cy);
                Vector2 a = mid - dir * half;
                Vector2 b = mid + dir * half;
                var kink = new Vector2(-dir.y, dir.x) * dice.Range(-0.06f, 0.06f);
                Stamp(field, a.x, a.y, mid.x + kink.x, mid.y + kink.y, 0.016f, 1f);
                Stamp(field, mid.x + kink.x, mid.y + kink.y, b.x, b.y, 0.014f, 1f);
            }
            return field;
        }

        /// <summary>The matte mottling: broad, low and uneven - dead tissue is not flat paint.</summary>
        private static float Mottle(int variant, Vector2 q)
        {
            float v = 0.5f
                + 0.30f * Mathf.Sin(2.3f * q.x + variant * 1.7f) * Mathf.Sin(1.9f * q.y + variant * 2.1f)
                + 0.16f * Mathf.Sin(4.7f * q.x + variant * 0.9f) * Mathf.Sin(4.1f * q.y + variant * 1.3f)
                + 0.10f * Mathf.Sin(8.3f * q.y + variant * 2.7f)
                + 0.07f * Mathf.Sin(11.7f * q.x + variant * 1.9f) * Mathf.Sin(9.3f * q.y + variant * 2.9f);
            return Mathf.Clamp01(v);
        }

        /// <summary>How irregular the front is at a point: what keeps the necrosis from crossing a
        /// face as a straight ruled line.</summary>
        private static float FrontNoise(int variant, Vector2 q)
        {
            float v = 0.5f
                + 0.3f * Mathf.Sin(1.7f * q.x + variant * 2.3f) * Mathf.Sin(2.1f * q.y + variant * 1.1f)
                + 0.2f * Mathf.Sin(3.3f * q.y + variant * 0.7f);
            return Mathf.Clamp01(v);
        }

        // ---- the small generated sprites ---------------------------------------------------

        /// <summary>The slot an empty cell is drawn as: what the contamination creeps across.</summary>
        private static Sprite SlotSprite()
        {
            if (slotSprite != null)
            {
                return slotSprite;
            }
            var px = new Color32[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / TileSize - 0.5f, (y + 0.5f) / TileSize - 0.5f, 0.14f);
                    px[y * TileSize + x] = new Color32(255, 255, 255,
                        Byte(Mathf.Clamp01(-sd / (1.5f / TileSize))));
                }
            }
            slotSprite = MakeSprite(TileSize, TileSize, px);
            return slotSprite;
        }

        /// <summary>The contact shadow under dead mass that has not settled yet.</summary>
        private static Sprite ShadowSprite()
        {
            if (shadowSprite != null)
            {
                return shadowSprite;
            }
            var px = new Color32[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / TileSize - 0.5f, (y + 0.5f) / TileSize - 0.5f, 0.2f);
                    px[y * TileSize + x] = new Color32(255, 255, 255, Byte(Mathf.Clamp01(-sd / 0.12f)));
                }
            }
            shadowSprite = MakeSprite(TileSize, TileSize, px);
            return shadowSprite;
        }

        /// <summary>An immune cube's stain: a wash on ONE edge of its face - the side the rot
        /// pressed against - and nothing anywhere else. Drawn turned toward the cell.</summary>
        private static Sprite ContactSprite()
        {
            if (contactSprite != null)
            {
                return contactSprite;
            }
            var px = new Color32[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float u = (x + 0.5f) / TileSize - 0.5f;
                    float v = (y + 0.5f) / TileSize - 0.5f;
                    float face = Mathf.Clamp01(-RoundedBox(u, v, 0.16f) / (1.5f / TileSize));
                    // Only the +x half, falling off inward, and fading toward the corners.
                    float wash = Mathf.Clamp01((u * 2f - 0.45f) / 0.55f);
                    float along = 1f - Mathf.Clamp01((Mathf.Abs(v) * 2f - 0.55f) / 0.45f);
                    px[y * TileSize + x] = new Color32(255, 255, 255,
                        Byte(face * wash * wash * along));
                }
            }
            contactSprite = MakeSprite(TileSize, TileSize, px);
            return contactSprite;
        }

        /// <summary>The debug outline round a line: a thin rounded ring.</summary>
        private static Sprite MarkerSprite()
        {
            if (markerSprite != null)
            {
                return markerSprite;
            }
            var px = new Color32[TileSize * TileSize];
            for (int y = 0; y < TileSize; y++)
            {
                for (int x = 0; x < TileSize; x++)
                {
                    float sd = RoundedBox((x + 0.5f) / TileSize - 0.5f, (y + 0.5f) / TileSize - 0.5f, 0.1f);
                    px[y * TileSize + x] = new Color32(255, 255, 255,
                        Byte(Mathf.Clamp01(1f - Mathf.Abs(sd + 0.02f) / 0.015f)));
                }
            }
            markerSprite = MakeSprite(TileSize, TileSize, px);
            return markerSprite;
        }

        /// <summary>The debug line from the source cell to the cell the rot took.</summary>
        private static Sprite LineSprite()
        {
            if (lineSprite != null)
            {
                return lineSprite;
            }
            const int w = 64;
            const int h = 8;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float along = (x + 0.5f) / w;
                    float off = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                    float a = Mathf.Clamp01(1f - off) * Mathf.Clamp01(along / 0.08f)
                        * Mathf.Clamp01((1f - along) / 0.08f);
                    px[y * w + x] = new Color32(255, 255, 255, Byte(a));
                }
            }
            lineSprite = MakeSprite(w, h, px);
            return lineSprite;
        }

        private static Sprite MakeSprite(int w, int h, Color32[] px)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Signed distance to a rounded square of half-size 0.5: negative inside.</summary>
        private static float RoundedBox(float u, float v, float radius)
        {
            float dx = Mathf.Abs(u) - (0.5f - radius);
            float dy = Mathf.Abs(v) - (0.5f - radius);
            float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f)
                + Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
            return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }
    }
}
