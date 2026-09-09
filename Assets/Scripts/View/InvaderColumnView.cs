// PURPOSE: "İstilacı"'s marked column. This class owns the LANE and the EVENTS; the corridor
// itself lives in ProjectBlock/InvaderColumn.shader.
//
// A THREAT WITH A DEADLINE, NOT A COLOUR. The boss marks one column and takes everything in it
// three turns later, so what this has to communicate is TIME. The countdown drives one number the
// shader leans on everywhere - rails tighten, the scan grows tense, the field breathes harder - so
// a player who never reads a counter still feels the last turn arrive.
//
// IT IS UNDER THE BLOCKS. The column stays playable while it is marked, so occupancy is uploaded as
// a one-pixel-wide texture and the field collapses wherever a cube stands. What identifies the lane
// then is its rails and the space between slots, which survive a block being dropped on top.
//
// THE END IS AN EXTRACTION. When the column comes due a single narrow band travels it once and what
// it passes is taken - stretched toward the exit and pulled out, not blown apart. The dynamite is
// the game's explosion; this is its collection.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The corridor "İstilacı" has marked, and the sweep that empties it.</summary>
    public sealed class InvaderColumnView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>How much field shows over an EMPTY cell of the lane. Low: a marked column
            /// is still a column the player may build in, and it has to keep looking like one.</summary>
            public static float FieldOpacity = 0.16f;

            /// <summary>How hard the field's energy carries its colour up out of the deep umber.
            /// This is what stops the lane being one flat orange: quiet parts stay nearly black-
            /// brown and only the working parts reach amber.</summary>
            public static float DepthContrast = 1.5f;

            /// <summary>Pulled slightly off full: a hair of grey in it reads as burnt rather than
            /// as a saturated UI colour.</summary>
            public static float Saturation = 0.92f;

            /// <summary>What survives in the middle of a cell that HAS a block, against what
            /// survives at its rim. The rim is what carries the threat once a cube covers it.</summary>
            public static float OccupiedOpacity = 0.11f;

            public static float OccupiedEdge = 0.58f;

            /// <summary>The scan is the loudest inner layer, so it steps back furthest under a
            /// block - the threat still reads there, from underneath.</summary>
            public static float OccupiedScan = 0.28f;

            // ------------------------------------------------------------------ palette
            /// <summary>Dark burnt umber - where the corridor sits when nothing is happening in it.
            /// Almost the board's own darkness, with rust in it.</summary>
            public static Color DeepColor = new Color(0.17f, 0.075f, 0.035f);

            /// <summary>Rust. The field at rest above the deep.</summary>
            public static Color FieldColor = new Color(0.44f, 0.18f, 0.065f);

            /// <summary>Burnt amber - the working parts.</summary>
            public static Color HeatColor = new Color(0.86f, 0.42f, 0.12f);

            /// <summary>Warm gold. The rails are machined, so they are the cleanest colour here.</summary>
            public static Color RailColor = new Color(1f, 0.72f, 0.30f);

            /// <summary>Reserved for the sweep and the snaps - the only near-white in the lane.</summary>
            public static Color CriticalColor = new Color(1f, 0.93f, 0.74f);

            public static Color MoteColor = new Color(1f, 0.66f, 0.28f);

            public static float HeatStrength = 0.26f;

            public static float HeatScale = 1.15f;

            /// <summary>How far the heat bends what is inside the corridor, in cells. Tiny, and it
            /// touches nothing but the field's own layers - the blocks draw on top of this.</summary>
            public static float HazeStrength = 0.035f;

            // ------------------------------------------------------------------ rails
            /// <summary>In fractions of the lane's width. Thin, and set just inside the walls - in
            /// the gutter BETWEEN blocks, which is why a built-in column still shows its rails.</summary>
            public static float RailWidth = 0.030f;

            public static float RailInset = 0.058f;

            public static float RailGlow = 0.62f;

            public static float RailPulse = 0.35f;

            /// <summary>Segments per cell along the rail - what makes it read as built rather than
            /// drawn. They tighten as the deadline closes.</summary>
            public static float RailSegment = 1.6f;

            /// <summary>0 leaves a plain glow line, 1 a fully segmented rail.</summary>
            public static float RailSegmentActivity = 0.85f;

            /// <summary>Brief concentrations that crawl UP the rail. What stops it being one even
            /// brightness end to end.</summary>
            public static float RailHotspot = 0.40f;

            /// <summary>The node above and the intake below - the corridor is plugged into
            /// something. A thickening of the rails, never a drawn device.</summary>
            public static float CapStrength = 0.38f;

            /// <summary>The scale printed along the rails. Nearly invisible until the survey band
            /// passes over it, which is what turns the scan from a gradient into a process.</summary>
            public static float TickStrength = 0.22f;

            /// <summary>Marks per cell.</summary>
            public static float TickPitch = 2f;

            // ------------------------------------------------------------------ scan
            public static float ScanStrength = 0.42f;

            public static float ScanSpeed = 0.18f;

            public static float ScanWidth = 0.13f;

            /// <summary>How lopsided the band is: a tight leading edge with a long tail below, so
            /// the direction is legible from one frozen frame.</summary>
            public static float ScanUpwardBias = 0.55f;

            // ------------------------------------------------------------------ drift
            // Idle motes, drawn in the shader. The Mote* fields further down are the EXTRACTION
            // particles - different layer, different moment.
            /// <summary>Fraction of the scrolling ranks that carry a mote at all. Sparse: this is
            /// meant to be felt, not counted.</summary>
            public static float UpwardMoteDensity = 0.32f;

            public static float UpwardMoteSpeed = 0.42f;

            public static float UpwardMoteSize = 0.10f;

            public static float UpwardMoteOpacity = 0.50f;

            // ------------------------------------------------------------------ critical
            /// <summary>Snaps at the rails, scaled by the SQUARE of the threat - so they are all
            /// but absent until the last turn and then genuinely arrive.</summary>
            public static float SparkDensity = 0.16f;

            public static float SparkGlow = 0.85f;

            // ------------------------------------------------------------------ state
            /// <summary>Threat by state. Three separated values rather than a ramp, so the three
            /// states are genuinely distinguishable rather than a slow slide. State 1 is three
            /// turns out (a lock, not an alarm); state 3 is the last turn.</summary>
            public static float State1Intensity = 0.15f;

            public static float State2Intensity = 0.52f;

            public static float State3Intensity = 1f;

            /// <summary>How long a state change takes to settle in - a short lean, then steady.</summary>
            public static float StateBlend = 0.35f;

            /// <summary>How far the corridor THINS over a cell seam. It never LIGHTS the grid -
            /// it steps out of the way, so the board's own dark seam shows through and the column
            /// stays something a placement can be planned in.</summary>
            public static float GridStrength = 0.45f;

            public static float EdgeAA = 1.15f;

            // ------------------------------------------------------------------ extraction
            /// <summary>How long the band takes to travel the whole lane. Fast, but readable.</summary>
            public static float SweepSeconds = 0.80f;

            public static float SweepWidth = 0.075f;

            public static float SweepGlow = 0.95f;

            /// <summary>Bottom to top: the lane is vertical and the cubes are being taken OUT of
            /// the arena, so the direction has to be an exit.</summary>
            public static bool SweepUpward = true;

            // ------------------------------------------------------------------ removal
            /// <summary>Motes per cube taken. Few: this is material being drawn out, not debris
            /// being thrown - the dynamite already owns thrown debris.</summary>
            public static int MotesPerCube = 5;

            public static float MoteSizeMin = 0.045f;

            public static float MoteSizeMax = 0.115f;

            public static float MotePull = 3.4f;

            public static float MoteSpread = 0.30f;

            public static float MoteLifeMin = 0.22f;

            public static float MoteLifeMax = 0.44f;

            public static Color MoteHot = new Color(1f, 0.72f, 0.34f);

            public static Color MoteAsh = new Color(0.24f, 0.19f, 0.16f);

            // ---------------------------------------------------------------- the collector
            /// <summary>The beat before the band moves: rails lock, the foot of the lane gathers.
            /// Short - this is a machine starting, not a fuse burning.</summary>
            public static float PreLockSeconds = 0.20f;

            /// <summary>How thin the band's hot core is against its halo.</summary>
            public static float BandCore = 1.30f;

            public static float BandHalo = 0.55f;

            /// <summary>The node where the band meets each rail. What makes the collector look
            /// like it is running ON the corridor rather than over it.</summary>
            public static float RailContact = 1.5f;

            public static float RailContactWidth = 0.045f;

            /// <summary>Energy at the foot of the lane during the lock.</summary>
            public static float ChargeStrength = 0.55f;

            /// <summary>The band leaving the top: pinched to a thread rather than switched off.</summary>
            public static float ExitSeconds = 0.16f;

            /// <summary>What a swept cell keeps for a moment. LOW - the cell has to read as
            /// playable again straight away, so this is an afterimage and not a mark.</summary>
            public static float Residue = 0.30f;

            /// <summary>The corridor standing down once the column has been taken.</summary>
            public static float ShutdownSeconds = 0.32f;

            // ---------------------------------------------------------------- one cube's exit
            /// <summary>How long a cube takes to be drawn out once the band reaches it.</summary>
            public static float PullSeconds = 0.34f;

            /// <summary>The small lift OUT of its slot, before it travels - the beat that says it
            /// is no longer part of the board.</summary>
            public static float LiftAmount = 0.16f;

            /// <summary>How far it travels on its way out, in cells.</summary>
            public static float PullTravel = 1.35f;

            /// <summary>How far it is drawn out along the pull, and how much it narrows across it.
            /// Kept well short of a line: this is a cube being taken, not a streak being drawn.</summary>
            public static float PullStretch = 0.85f;

            public static float CrossCompression = 0.42f;

            /// <summary>The trail behind it, in the CUBE'S OWN colour - so a green block leaves a
            /// green thread and a gold one a gold thread. It is what stops the sweep looking like
            /// one amber effect applied to everything.</summary>
            public static float TrailLength = 1.6f;

            public static float TrailOpacity = 0.55f;

            /// <summary>The shadow it leaves in its slot for a moment after it lifts - the clearest
            /// way to say a thing is no longer attached to the board.</summary>
            public static float ShadowOpacity = 0.55f;

            public static float CaptureStrength = 0.85f;

            public static float AftermathSeconds = 0.30f;
        }

        /// <summary>Over the board, under the blocks' own reading - see the occupancy collapse.</summary>
        private const int LaneOrder = 6;

        private const int MoteOrder = 14;

        /// <summary>Exactly where the real cube sat: under the corridor, over the board.</summary>
        private const int TakenOrder = 1;

        /// <summary>Matches BoardView's own cube fill, so the copy is the same size as the cube
        /// it stands in for and the swap is invisible.</summary>
        private const float TakenFill = 0.98f;

        /// <summary>The shadow sits UNDER the cube it belongs to; the trail behind it; the catch
        /// light on top of it. All below the corridor, which draws over the whole lane.</summary>
        private const int ShadowOrder = 0;

        private const int TrailOrder = 1;

        private const int CaptureOrder = 2;

        private const int OccupancyTexels = 64;

        // =================================================================== state

        private SpriteRenderer lane;

        private Material material;

        private Texture2D occupancy;

        private Color32[] occPixels;

        private float clock;

        private bool built;

        private int column;

        private int turnsLeft = 3;

        private float threat;

        private float threatTarget;

        private int minX;

        private int minY;

        private int height;

        private float cellSize = 1f;

        private Vector2 laneOrigin;

        private float sweepStartedAt = -1f;

        private static readonly int TimeId = Shader.PropertyToID("_NestTime");

        private static readonly int ThreatId = Shader.PropertyToID("_Threat");

        private static readonly int SweepAtId = Shader.PropertyToID("_SweepAt");

        private static readonly int SweepActiveId = Shader.PropertyToID("_SweepActive");

        private static readonly int ChargeId = Shader.PropertyToID("_Charge");

        private static readonly int NarrowId = Shader.PropertyToID("_BandNarrow");

        private static readonly int ShutdownId = Shader.PropertyToID("_Shutdown");

        // =================================================================== driving it

        /// <summary>
        /// Marks a column, or clears the lane when <paramref name="markedColumn"/> is null.
        /// <paramref name="turns"/> is what the boss has left, which is the only thing the whole
        /// escalation is driven from.
        /// </summary>
        public void Show(int? markedColumn, int turns, GameBoard board,
            System.Func<GridPos, Vector2> toWorld, float cell,
            System.Func<GridPos, bool> occupied)
        {
            if (!markedColumn.HasValue || board == null || cell <= 0f)
            {
                Clear();
                return;
            }
            bool fresh = !built || column != markedColumn.Value
                || minY != board.MinY || height != board.Height
                || !Mathf.Approximately(cell, cellSize);
            column = markedColumn.Value;
            minX = board.MinX;
            minY = board.MinY;
            height = board.Height;
            cellSize = cell;
            turnsLeft = turns;
            threatTarget = ThreatFor(turns);
            if (fresh)
            {
                threat = threatTarget;
                sweepStartedAt = -1f;
            }
            EnsureLane(toWorld, fresh);
            PushStyle();
            RefreshOccupancy(occupied);
            built = true;
        }

        private static float ThreatFor(int turns)
        {
            if (turns <= 1)
            {
                return Style.State3Intensity;
            }
            return turns == 2 ? Style.State2Intensity : Style.State1Intensity;
        }

        public void Clear()
        {
            built = false;
            if (lane != null)
            {
                lane.enabled = false;
            }
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].Live = false;
                if (motes[i].R != null)
                {
                    motes[i].R.enabled = false;
                }
            }
        }

        /// <summary>The column has come due. One band travels it and what it passes is taken.</summary>
        public void PlayExtraction(IReadOnlyList<DestroyedCube> taken,
            System.Func<GridPos, Vector2> toWorld,
            System.Func<Cube, Sprite> tileOf, System.Func<Cube, Sprite, Color> colourOf)
        {
            if (!built)
            {
                return;
            }
            sweepStartedAt = clock;
            ClearPulled();
            if (taken == null)
            {
                return;
            }
            for (int i = 0; i < taken.Count; i++)
            {
                Sprite tile = tileOf != null ? tileOf(taken[i].Cube) : null;
                Vector2 at = toWorld(taken[i].Pos);
                Color colour = colourOf != null ? colourOf(taken[i].Cube, tile) : Color.white;
                pulled.Add(new Pulled
                {
                    Where = at,
                    Along = Mathf.InverseLerp(minY, minY + height - 1, taken[i].Pos.Y),
                    Tile = tile,
                    Colour = colour,
                    Taken = false,
                    Body = MakeBody(at, tile, colour),
                    Trail = MakeFlat(at, colour, TrailOrder),
                    Shadow = MakeFlat(at, new Color(0f, 0f, 0f, 1f), ShadowOrder),
                    Capture = MakeFlat(at, Style.CriticalColor, CaptureOrder),
                    TakenAt = 0f
                });
            }
        }

        private struct Pulled
        {
            public Vector2 Where;
            public float Along;
            public Sprite Tile;
            public Color Colour;
            public bool Taken;

            /// <summary>The cube itself, drawn by THIS view.
            ///
            /// It has to be drawn here because by the time any of this runs the rules have
            /// already destroyed it and the board has already stopped drawing it - so without a
            /// copy of its own the extraction plays over an empty column and the cubes simply
            /// blink out. That is what it did: the band swept nothing.</summary>
            public SpriteRenderer Body;

            /// <summary>The cube's own colour trailing behind it, and the shadow it leaves in
            /// the slot. Both are what make the exit read as a REMOVAL rather than a fade.</summary>
            public SpriteRenderer Trail;

            public SpriteRenderer Shadow;

            /// <summary>The light that grabs it the instant the band arrives.</summary>
            public SpriteRenderer Capture;

            /// <summary>When the band reached it, so its own pull can be timed from there.</summary>
            public float TakenAt;
        }

        private readonly List<Pulled> pulled = new List<Pulled>();

        private void Kill(SpriteRenderer r)
        {
            if (r != null)
            {
                Destroy(r.gameObject);
            }
        }

        private void ClearPulled()
        {
            for (int i = 0; i < pulled.Count; i++)
            {
                Kill(pulled[i].Body);
                Kill(pulled[i].Trail);
                Kill(pulled[i].Shadow);
                Kill(pulled[i].Capture);
            }
            pulled.Clear();
        }

        // =================================================================== the lane

        private void EnsureLane(System.Func<GridPos, Vector2> toWorld, bool fresh)
        {
            if (occupancy == null)
            {
                occupancy = new Texture2D(1, OccupancyTexels, TextureFormat.RGBA32, false, true);
                occupancy.hideFlags = HideFlags.HideAndDontSave;
                occupancy.filterMode = FilterMode.Bilinear;
                occupancy.wrapMode = TextureWrapMode.Clamp;
                occPixels = new Color32[OccupancyTexels];
            }
            if (lane == null)
            {
                var go = new GameObject("Lane");
                go.transform.SetParent(transform, false);
                lane = go.AddComponent<SpriteRenderer>();
                lane.sortingOrder = LaneOrder;
                lane.sprite = ViewUtil.WhiteSprite;
                lane.color = Color.white;
            }
            if (material == null)
            {
                Shader shader = Shader.Find("ProjectBlock/InvaderColumn");
                material = new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
                material.hideFlags = HideFlags.HideAndDontSave;
                lane.sharedMaterial = material;
            }
            // One quad over the whole column. A white unit sprite scaled to it, so the shader gets
            // clean 0..1 UVs across and along the lane with no atlas or padding to reason about.
            Vector2 low = toWorld(new GridPos(column, minY));
            Vector2 high = toWorld(new GridPos(column, minY + height - 1));
            Vector2 centre = (low + high) * 0.5f;
            float lengthCells = height;
            lane.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            lane.transform.localScale = new Vector3(cellSize, cellSize * lengthCells, 1f);
            laneOrigin = new Vector2(low.x - cellSize * 0.5f, low.y - cellSize * 0.5f);
            lane.enabled = true;
        }

        private void PushStyle()
        {
            if (material == null)
            {
                return;
            }
            material.SetTexture("_MainTex", occupancy);
            material.SetVector("_SizeCells", new Vector4(1f, height, 0f, 0f));
            material.SetColor("_DeepColor", Style.DeepColor);
            material.SetColor("_FieldColor", Style.FieldColor);
            material.SetColor("_HeatColor", Style.HeatColor);
            material.SetColor("_RailColor", Style.RailColor);
            material.SetColor("_CriticalColor", Style.CriticalColor);
            material.SetColor("_MoteColor", Style.MoteColor);
            material.SetFloat("_FieldOpacity", Style.FieldOpacity);
            material.SetFloat("_DepthContrast", Style.DepthContrast);
            material.SetFloat("_Saturation", Style.Saturation);
            material.SetFloat("_OccupiedOpacity", Style.OccupiedOpacity);
            material.SetFloat("_OccupiedEdge", Style.OccupiedEdge);
            material.SetFloat("_OccupiedScan", Style.OccupiedScan);
            material.SetFloat("_HeatStrength", Style.HeatStrength);
            material.SetFloat("_HeatScale", Style.HeatScale);
            material.SetFloat("_HazeStrength", Style.HazeStrength);
            material.SetFloat("_ScanStrength", Style.ScanStrength);
            material.SetFloat("_ScanSpeed", Style.ScanSpeed);
            material.SetFloat("_ScanWidth", Style.ScanWidth);
            material.SetFloat("_ScanUpwardBias", Style.ScanUpwardBias);
            material.SetFloat("_RailWidth", Style.RailWidth);
            material.SetFloat("_RailGlow", Style.RailGlow);
            material.SetFloat("_RailInset", Style.RailInset);
            material.SetFloat("_RailPulse", Style.RailPulse);
            material.SetFloat("_RailSegment", Style.RailSegment);
            material.SetFloat("_RailSegmentActivity", Style.RailSegmentActivity);
            material.SetFloat("_RailHotspot", Style.RailHotspot);
            material.SetFloat("_MoteDensity", Style.UpwardMoteDensity);
            material.SetFloat("_MoteSpeed", Style.UpwardMoteSpeed);
            material.SetFloat("_MoteSize", Style.UpwardMoteSize);
            material.SetFloat("_MoteOpacity", Style.UpwardMoteOpacity);
            material.SetFloat("_CapStrength", Style.CapStrength);
            material.SetFloat("_TickStrength", Style.TickStrength);
            material.SetFloat("_TickPitch", Style.TickPitch);
            material.SetFloat("_SparkDensity", Style.SparkDensity);
            material.SetFloat("_SparkGlow", Style.SparkGlow);
            material.SetFloat("_EdgeAA", Style.EdgeAA);
            material.SetFloat("_GridStrength", Style.GridStrength);
            material.SetFloat("_CellPhase", 0f);
            material.SetFloat("_SweepWidth", Style.SweepWidth);
            material.SetFloat("_SweepGlow", Style.SweepGlow);
            material.SetFloat("_SweepCore", Style.BandCore);
            material.SetFloat("_SweepHalo", Style.BandHalo);
            material.SetFloat("_RailContact", Style.RailContact);
            material.SetFloat("_RailContactWidth", Style.RailContactWidth);
            material.SetFloat("_Residue", Style.Residue);
        }

        /// <summary>Which cells of the lane hold a cube, as a one-pixel-wide strip the shader
        /// samples bilinearly - so a cube's influence fades across its own cell.</summary>
        public void RefreshOccupancy(System.Func<GridPos, bool> occupied)
        {
            if (occupancy == null || occPixels == null || height <= 0)
            {
                return;
            }
            for (int t = 0; t < OccupancyTexels; t++)
            {
                float alongCells = (t + 0.5f) / OccupancyTexels * height;
                int cy = Mathf.Clamp(Mathf.FloorToInt(alongCells), 0, height - 1);
                bool full = occupied != null
                    && occupied(new GridPos(column, minY + cy));
                var v = (byte)(full ? 255 : 0);
                occPixels[t] = new Color32(v, v, v, 255);
            }
            occupancy.SetPixels32(occPixels);
            occupancy.Apply(false, false);
        }

        // =================================================================== motes

        /// <summary>A standing copy of a cube the column is about to take. The SAME call the
        /// board uses to dress a cube, so a gold block leaves as gold and an obsidian one as
        /// obsidian rather than as a coloured square.</summary>
        private SpriteRenderer MakeBody(Vector2 at, Sprite tile, Color colour)
        {
            var go = new GameObject("Taken");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = TakenOrder;
            ViewUtil.ApplyTile(r, tile, cellSize * TakenFill);
            r.color = colour;
            return r;
        }

        /// <summary>A plain coloured quad that belongs to one taken cube - its trail, its
        /// shadow, or the light that grabs it.</summary>
        private SpriteRenderer MakeFlat(Vector2 at, Color colour, int order)
        {
            var go = new GameObject("TakenPart");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(at.x, at.y, 0f);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = ViewUtil.WhiteSprite;
            r.sortingOrder = order;
            var c = colour;
            c.a = 0f;
            r.color = c;
            r.enabled = false;
            return r;
        }

        /// <summary>
        /// ONE CUBE BEING TAKEN, start to finish. The whole identity of this boss is in here:
        ///
        ///   0-20%   the band arrives and CATCHES it - it is still the cube you built with
        ///   20-45%  it comes out of its slot, and its shadow stays behind for a moment
        ///   45-75%  it is drawn out along the pull and narrows across it
        ///   75-90%  what is left is a thread in its own colour
        ///   90-100% gone
        ///
        /// It never cracks, never sheds a fragment, and never flashes. The dynamite breaks a
        /// block; this takes it, and every beat above exists to keep those two apart.
        /// </summary>
        private void StepPulled()
        {
            float dir = Style.SweepUpward ? 1f : -1f;
            float size = cellSize * TakenFill;
            for (int i = 0; i < pulled.Count; i++)
            {
                Pulled p = pulled[i];
                if (p.Body == null || !p.Taken)
                {
                    continue;                       // still standing; the band has not reached it
                }
                float k = Mathf.Clamp01((clock - p.TakenAt) / Mathf.Max(Style.PullSeconds, 0.01f));
                if (k >= 1f)
                {
                    Hide(p.Body);
                    Hide(p.Trail);
                    Hide(p.Shadow);
                    Hide(p.Capture);
                    continue;
                }

                // ---- CAUGHT. A short bar of light across the cube the instant the band is on
                // it, gone before it has travelled - what says the collector took hold of THIS
                // cube rather than the band merely passing over it.
                float grab = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k / 0.30f));
                if (p.Capture != null)
                {
                    p.Capture.enabled = grab > 0.01f;
                    p.Capture.transform.localPosition =
                        new Vector3(p.Where.x, p.Where.y - dir * size * 0.42f, 0f);
                    p.Capture.transform.localScale =
                        new Vector3(size * 1.02f, size * 0.16f, 1f);
                    Color cc = Style.CriticalColor;
                    cc.a = grab * Style.CaptureStrength;
                    p.Capture.color = cc;
                }

                // ---- OUT OF THE SLOT, then away. The lift is small and early; the travel is
                // squared, so it leaves slowly and is gone quickly.
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.45f, k));
                float go = Mathf.Clamp01(Mathf.InverseLerp(0.30f, 1f, k));
                float travel = Style.LiftAmount * cellSize * lift
                    + Style.PullTravel * cellSize * go * go;

                // ---- DRAWN OUT along the pull, narrowed across it. The growth is pushed toward
                // the LEADING edge, so the far side moves first and the near side follows - a
                // pull, rather than something inflating in place.
                float shape = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, k));
                float stretch = 1f + Style.PullStretch * shape;
                float narrow = 1f - Style.CrossCompression * shape;
                float lead = (stretch - 1f) * size * 0.5f * 0.65f;

                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, k));
                Transform tr = p.Body.transform;
                tr.localPosition = new Vector3(p.Where.x, p.Where.y + dir * (travel + lead), 0f);
                tr.localScale = new Vector3(narrow, stretch, 1f);
                Color c = p.Colour;
                c.a = fade;
                p.Body.color = c;
                p.Body.enabled = true;

                // ---- ITS OWN COLOUR behind it. A gold block leaves a gold thread; the sweep is
                // not one amber effect painted over everything it takes.
                if (p.Trail != null)
                {
                    float trailK = Mathf.Clamp01(Mathf.InverseLerp(0.25f, 1f, k));
                    p.Trail.enabled = trailK > 0.01f;
                    float len = size * Style.TrailLength * trailK;
                    p.Trail.transform.localPosition = new Vector3(p.Where.x,
                        p.Where.y + dir * (travel - len * 0.5f + size * 0.1f), 0f);
                    p.Trail.transform.localScale =
                        new Vector3(size * narrow * 0.55f, len, 1f);
                    Color tc = p.Colour;
                    tc.a = Style.TrailOpacity * trailK * (1f - trailK * trailK);
                    p.Trail.color = tc;
                }

                // ---- THE SHADOW STAYS a moment. Nothing says "this is no longer attached to the
                // board" as plainly as its shadow being left behind in the slot.
                if (p.Shadow != null)
                {
                    float shadowFade = 1f - Mathf.Clamp01(Mathf.InverseLerp(0.15f, 0.55f, k));
                    p.Shadow.enabled = shadowFade > 0.01f;
                    p.Shadow.transform.localPosition =
                        new Vector3(p.Where.x, p.Where.y, 0f);
                    p.Shadow.transform.localScale =
                        new Vector3(size * 0.92f, size * 0.92f, 1f);
                    p.Shadow.color = new Color(0f, 0f, 0f, Style.ShadowOpacity * shadowFade);
                }
            }
        }

        private static void Hide(SpriteRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
            }
        }

        private struct Mote
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Size;
            public float Age;
            public float Life;
            public Color Tint;
            public bool Live;
        }

        private Mote[] motes = new Mote[0];

        private static Sprite moteSprite;

        private static Sprite MoteSprite()
        {
            if (moteSprite != null)
            {
                return moteSprite;
            }
            const int n = 16;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    // Stretched along the pull, so a mote reads as being drawn rather than falling.
                    float a = Mathf.Exp(-(u * u * 7f + v * v * 2.2f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            moteSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return moteSprite;
        }

        private void EmitMotes(Vector2 at, Color blockColour)
        {
            for (int i = 0; i < Style.MotesPerCube; i++)
            {
                int slot = -1;
                for (int m = 0; m < motes.Length; m++)
                {
                    if (!motes[m].Live)
                    {
                        slot = m;
                        break;
                    }
                }
                if (slot < 0)
                {
                    if (motes.Length >= 48)
                    {
                        return;
                    }
                    var next = new Mote[motes.Length + 1];
                    System.Array.Copy(motes, next, motes.Length);
                    var go = new GameObject("Mote" + motes.Length);
                    go.transform.SetParent(transform, false);
                    var r = go.AddComponent<SpriteRenderer>();
                    r.sprite = MoteSprite();
                    r.sortingOrder = MoteOrder;
                    r.enabled = false;
                    next[motes.Length].R = r;
                    slot = motes.Length;
                    motes = next;
                }
                float dir = Style.SweepUpward ? 1f : -1f;
                motes[slot].Pos = at + Random.insideUnitCircle * (cellSize * 0.3f);
                motes[slot].Vel = new Vector2(
                    Random.Range(-Style.MoteSpread, Style.MoteSpread),
                    dir * Style.MotePull * Random.Range(0.7f, 1.3f)) * cellSize;
                motes[slot].Size = Random.Range(Style.MoteSizeMin, Style.MoteSizeMax) * cellSize;
                motes[slot].Life = Random.Range(Style.MoteLifeMin, Style.MoteLifeMax);
                motes[slot].Age = 0f;
                // Half carry the block's own colour out with them, half are already ash.
                motes[slot].Tint = Random.value < 0.5f
                    ? Color.Lerp(blockColour, Style.MoteHot, 0.5f) : Style.MoteAsh;
                motes[slot].Live = true;
                motes[slot].R.enabled = true;
            }
        }

        private void StepMotes(float dt)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                if (!motes[i].Live)
                {
                    continue;
                }
                motes[i].Age += dt;
                float k = motes[i].Age / motes[i].Life;
                if (k >= 1f)
                {
                    motes[i].Live = false;
                    motes[i].R.enabled = false;
                    continue;
                }
                // Accelerating out rather than slowing down: it is being pulled, not thrown.
                motes[i].Vel *= 1f + 1.6f * dt;
                motes[i].Pos += motes[i].Vel * dt;
                Transform t = motes[i].R.transform;
                t.localPosition = new Vector3(motes[i].Pos.x, motes[i].Pos.y, 0f);
                float s = motes[i].Size * (1f - k * 0.45f);
                t.localScale = new Vector3(s * 0.5f, s * 1.6f, 1f);
                Color c = motes[i].Tint;
                c.a = (1f - k) * (1f - k);
                motes[i].R.color = c;
            }
        }

        // =================================================================== running

        /// <summary>Whether the BAND is still travelling.</summary>
        public bool Sweeping
        {
            get { return sweepStartedAt >= 0f && clock - sweepStartedAt <= Style.SweepSeconds; }
        }

        /// <summary>
        /// Whether this view still OWNS the cubes it is taking - true until the last one has
        /// finished being pulled out, which is later than the band finishing.
        ///
        /// The board asks this before taking its own cubes back. It matters in the animation lab
        /// above all: there the rules have destroyed nothing, so the real cubes are still standing
        /// under the copies this view draws, and handing them back too early leaves the column
        /// looking as though the sweep took nothing.
        /// </summary>
        public bool Extracting
        {
            get { return sweepStartedAt >= 0f && clock - sweepStartedAt <= TotalSeconds; }
        }

        /// <summary>How long a column takes to travel, scaled to how tall it is - a band that
        /// crossed a short arena and a tall one in the same time would read as two speeds.</summary>
        private float SweepTravelSeconds
        {
            get { return Style.SweepSeconds * Mathf.Clamp(height / 7f, 0.7f, 1.6f); }
        }

        private float TotalSeconds
        {
            get
            {
                return Style.PreLockSeconds + SweepTravelSeconds + Style.ExitSeconds
                    + Style.ShutdownSeconds;
            }
        }

        public static float ExtractionDuration
        {
            get { return Style.SweepSeconds + Style.AftermathSeconds; }
        }

        // NO SCREEN SHAKE, and the fields for one are gone rather than left unread. A shake is
        // the dynamite's language: it says something was blown apart. This boss COLLECTS, and the
        // whole point of the sweep is that it does not feel like an explosion.

        private void Update()
        {
            if (!built)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            StepMotes(dt);
            StepPulled();

            // The state leans in rather than snapping, so a countdown step reads as pressure
            // arriving instead of a value being assigned.
            threat = Mathf.MoveTowards(threat, threatTarget,
                dt / Mathf.Max(Style.StateBlend, 0.001f));

            // ================================================== THE COLLECTION, IN FOUR BEATS
            //
            //   LOCK      the rails tighten and the foot of the lane gathers. Nothing has moved
            //             yet, and that pause is what makes the sweep read as a system being
            //             STARTED rather than as something going off.
            //   SWEEP     one pass, bottom to top. Every cube it reaches begins its own exit.
            //   EXIT      the band is pinched into a thread at the top rather than switched off.
            //   SHUTDOWN  the corridor stands down: there is nothing left here to threaten.
            //
            // No flash and no shake anywhere in it. This boss does not break the column, it
            // empties it, and every beat above is chosen to keep those two things apart.
            float sweepAt = -1f;
            bool sweeping = false;
            float charge = 0f;
            float narrow = 0f;
            float shutdown = 0f;
            if (sweepStartedAt >= 0f)
            {
                float t = clock - sweepStartedAt;
                float travel = SweepTravelSeconds;
                if (t < Style.PreLockSeconds)
                {
                    charge = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(Style.PreLockSeconds, 0.01f))
                        * Style.ChargeStrength;
                }
                else if (t < Style.PreLockSeconds + travel)
                {
                    float k = (t - Style.PreLockSeconds) / Mathf.Max(travel, 0.01f);
                    // Leaves with a push, holds its pace, and gains a little on the way out.
                    // Written as a curve rather than an easing name so the three parts of the
                    // brief - start, middle, finish - are each visible in it.
                    float e = 0.25f * k * k + 0.60f * k + 0.15f * k * k * k;
                    sweeping = true;
                    charge = Style.ChargeStrength * (1f - k) * 0.4f;
                    sweepAt = Style.SweepUpward ? e : 1f - e;
                    // Anything the band has reached begins its own exit, on its own clock.
                    for (int i = 0; i < pulled.Count; i++)
                    {
                        if (pulled[i].Taken)
                        {
                            continue;
                        }
                        bool reached = Style.SweepUpward
                            ? sweepAt >= pulled[i].Along : sweepAt <= pulled[i].Along;
                        if (!reached)
                        {
                            continue;
                        }
                        Pulled p = pulled[i];
                        p.Taken = true;
                        p.TakenAt = clock;
                        pulled[i] = p;
                        EmitMotes(p.Where, p.Colour);
                    }
                }
                else if (t < Style.PreLockSeconds + travel + Style.ExitSeconds)
                {
                    float k = (t - Style.PreLockSeconds - travel)
                        / Mathf.Max(Style.ExitSeconds, 0.01f);
                    sweeping = true;
                    sweepAt = Style.SweepUpward ? 1f : 0f;
                    narrow = Mathf.SmoothStep(0f, 1f, k);
                }
                else if (t < TotalSeconds)
                {
                    float k = (t - Style.PreLockSeconds - travel - Style.ExitSeconds)
                        / Mathf.Max(Style.ShutdownSeconds, 0.01f);
                    shutdown = Mathf.SmoothStep(0f, 1f, k);
                }
                else
                {
                    sweepStartedAt = -1f;
                    ClearPulled();
                }
            }

            if (material != null)
            {
                material.SetFloat(TimeId, clock);
                material.SetFloat(ThreatId, threat);
                material.SetFloat(SweepAtId, sweepAt);
                material.SetFloat(SweepActiveId, sweeping ? 1f : 0f);
                material.SetFloat(ChargeId, charge);
                material.SetFloat(NarrowId, narrow);
                material.SetFloat(ShutdownId, shutdown);
            }
        }

        private void OnDestroy()
        {
            if (occupancy != null)
            {
                Destroy(occupancy);
            }
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
