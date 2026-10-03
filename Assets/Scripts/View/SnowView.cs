// PURPOSE: SNOW on the board and the "Çığ" avalanche - the whole presentation of the system:
// "SNOW ACCUMULATION -> PRESSURE -> AVALANCHE".
//
// SNOW IS A MATERIAL THAT LIVES ON THE BOARD. Every snow cube is drawn by the board itself on the
// shared SnowAge material, and this writes each cube's state into its renderer's property block
// every frame: how near melting it is (FRESH 5 -> SETTLED -> SOFTENING -> WET -> COLLAPSING 1 - a
// material that ages, never a countdown number), whether an avalanche packed it, the seam that
// keeps two of one avalanche's layers apart, a refresh's cooling sweep, the aim's brightening. The
// heap's POWER is the one number it carries: small, deep blue-grey with an off-white edge, once per
// heap, near its edge - never a badge.
//
// MOVEMENT is the board's own fall (BoardView.PlayWaterAnimation - Core settles snow with the
// water), which tells this layer about snow through BoardView.ISnowMotion: a slide with powder
// trailing and a squash on arrival, and a fall that ends INSIDE a heap handed over as a MERGE -
// the incoming snow presses on the heap, the heap flattens along the flow, powder squirts from the
// seam, the incoming mass spreads into it, a cold ring closes on it and the number goes old ->
// pressed -> new. If the merge made the snow last longer, a cooling sweep runs out from the
// contact and the aged look freshens. A merge that stopped at an avalanche seam lights that seam.
//
// THE MELT is never a fade: it softens, the middle goes wet and sinks, the outline eats in
// irregularly (the shader's _Melt), a few drops, gone.
//
// THE AVALANCHE is replayed over a board Core has already settled, from AvalancheVisuals: the
// heap gathers pressure (compressing along the flow, powder lifting off its back, grooves in it,
// its number pulsing then dimming, a low rumble), lets go ("WHUFF"), and a dense front comes down
// ROW BY ROW - pressing each row's blocks (1 -> 0.94, a cold reflection on their snow side),
// breaking them (their own material, through the controller's cluster burst, plus white powder),
// and only THEN filling the row ("THUM-CRSH", a pixel of the arena knocked along the flow), the
// next row, the next. Then the whole mass settles (1.03 -> 0.97 -> 1, "thoom"), the score
// essences each broken block left gather into one "+TOTAL" that goes to the TOPLAM, and only then
// is the board's own gravity handed back - the soft slide after the heavy fall.
//
// THE VIEW DECIDES NOTHING: which heaps slide, how far, what breaks, what pays, the strata and
// their seams, the merges with their before and after, what melted - all Core
// (AvalanchePlan / AvalancheVisuals / SnowMerge / GameBoard.SnowSeamBelow / TurnReport.SnowMelted).
//
// IT LIVES OUTSIDE THE BOARD'S TRANSFORM (BoardView.Rebuild destroys the board's children) and
// copies it every frame.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The snow layer over the board. Owned by the controller.</summary>
    public sealed class SnowView : MonoBehaviour, BoardView.ISnowMotion
    {
        // =================================================================== TUNING
        /// <summary>Every length in seconds, distances in cells. The brief's starting numbers.</summary>
        public static class Style
        {
            // ---- snow
            public static float snowMoveDurationPerCell = 0.085f; // (the board's fall clock)
            public static float snowArrivalSquash = 0.96f;
            public static int snowPowderCount = 4;
            public static float snowMergeContactDuration = 0.08f;
            public static float snowMergeCompressDuration = 0.15f;
            public static float snowMergeSettleDuration = 0.09f;
            public static float snowPowerPulseDuration = 0.18f;
            public static float snowMeltDuration = 0.45f;
            public static float snowMeltWetnessStrength = 1f;
            public static float snowTurnAgeSettle = 0.14f;
            public static float snowAgeBlend = 0.45f;

            // ---- avalanche
            public static float avalancheAnticipationDuration = 0.22f;
            public static float avalancheFrontTravelDuration = 0.10f;
            public static float avalancheLayerInterval = 0.17f;
            public static float avalancheBlockPressureDuration = 0.065f;
            public static float avalancheBlockDestroyDuration = 0.09f;
            public static float avalancheSnowFillDuration = 0.09f;
            public static float avalancheFinalSettleDuration = 0.16f;
            public static float avalanchePostSettleBreath = 0.12f;
            public static int avalanchePowderCount = 8;
            public static float avalancheBoardImpulse = 0.016f;
            public static float avalancheScoreGatherDuration = 0.25f;
            public static float avalancheScorePopDuration = 0.35f;
            public static float avalancheScoreFlightDuration = 0.42f;
            public static float avalancheFreshHold = 0.3f;
            public static float avalancheFreshBlend = 0.9f;

            // ---- budgets
            public static int MaxParticles = 40;
            public static int HeroFlecksPerLayer = 12;

            // ---- colour
            public static Color PowerInk = new Color(0.27f, 0.35f, 0.46f, 1f);
            public static Color PowerRim = new Color(0.96f, 0.98f, 1f, 0.95f);
            public static Color Powder = new Color(0.95f, 0.97f, 1f, 0.9f);
            public static Color PowderBlue = new Color(0.78f, 0.86f, 0.96f, 0.85f);
            public static Color PressureRing = new Color(0.72f, 0.82f, 0.95f, 0.55f);
            public static Color Frost = new Color(0.86f, 0.93f, 1f, 0.5f);
            public static Color Haze = new Color(0.70f, 0.82f, 0.96f, 0.07f);
            public static Color Ghost = new Color(0.92f, 0.96f, 1f, 0.24f);
            public static Color PressShadow = new Color(0.30f, 0.38f, 0.50f, 0.32f);
            public static Color Muted = new Color(0.66f, 0.70f, 0.76f, 0.32f);
            public static Color MeltWater = new Color(0.62f, 0.78f, 0.95f, 0.8f);
            public static Color EssenceGold = new Color(1f, 0.80f, 0.36f, 0.95f);
            public static Color EssenceRim = new Color(0.94f, 0.97f, 1f, 0.6f);
            public static Color ScoreGold = new Color(1f, 0.84f, 0.40f, 1f);
        }

        /// <summary>Quality: Low keeps every beat and drops most particles.</summary>
        public enum Quality
        {
            High,
            Medium,
            Low
        }

        public static Quality Level = Application.isMobilePlatform ? Quality.Medium : Quality.High;

        /// <summary>The debug views (the lab switches them).</summary>
        public static class Show
        {
            public static bool SnowPower = true;
            public static bool SnowMeltTimer;
            public static bool SnowGravityPath;
            public static bool SnowMergeTarget;
            public static bool SnowMergeBarrier;
            public static bool SnowStrataId;
            public static bool AvalancheEligibleRows;
            public static bool AvalancheSpentRows;
            public static bool AvalanchePreviewCells;
            public static bool AvalancheDestructionCells;
            public static bool AvalancheLayerIndex;
            public static bool AvalancheFront;
            public static bool AvalancheTiming;
            public static bool ScoreEssence;
            public static bool ParticleBudget;

            public static void AllOff()
            {
                SnowMeltTimer = SnowGravityPath = SnowMergeTarget = SnowMergeBarrier = SnowStrataId = false;
                AvalancheEligibleRows = AvalancheSpentRows = AvalanchePreviewCells = false;
                AvalancheDestructionCells = AvalancheLayerIndex = AvalancheFront = AvalancheTiming = false;
                ScoreEssence = ParticleBudget = false;
                SnowPower = true;
            }
        }

        /// <summary>Where an avalanche is frozen for the lab's beat isolation; None plays through.</summary>
        public enum Beat
        {
            None,
            Anticipation,
            FirstPressure,
            FirstDestroy,
            FirstFill,
            Chain,
            Settle,
            Score
        }

        public static Beat HoldAt = Beat.None;

        private const int GhostOrder = 2;
        private const int MarkOrder = 2;
        private const int ProxyOrder = 3;
        private const int FrontOrder = 4;
        private const int ParticleOrder = 6;
        private const int LabelOrder = 7;
        private const int ScoreOrder = 60;

        /// <summary>Set by the controller.</summary>
        public SoundFx Sfx;

        /// <summary>The numbers are hidden while this says so (cubes in the air).</summary>
        public Func<bool> Busy;

        /// <summary>Where the TOPLAM is, in world space.</summary>
        public Func<Vector2> ScoreAnchor;

        /// <summary>The aim began (true) or ended (false): the power card's pulse and the
        /// background's dimming are the controller's.</summary>
        public Action<bool> AimChanged;

        /// <summary>The score line's answer while the "+TOTAL" lands (TickScoreResponse reads it).</summary>
        public float ScoreClaim { get; private set; }

        public float ScoreScale { get; private set; } = 1f;

        public Color ScoreInk { get; private set; } = Style.ScoreGold;

        public bool Playing
        {
            get { return avalanche != null; }
        }

        // =================================================================== state

        private BoardView board;
        private Transform root;
        private Transform labels;
        private Transform aimRoot;
        private Transform debugRoot;
        private MaterialPropertyBlock block;

        private sealed class CellLook
        {
            public float Age = -1f;
            public int LastMelt = -1;
            public float SettleAt = -10f;
            public float SweepAt = -10f;
            public Vector2 SweepFrom;
            public float BarrierAt = -10f;
            public float SquashUntil;
        }

        private readonly Dictionary<GridPos, CellLook> looks = new Dictionary<GridPos, CellLook>();
        private readonly HashSet<SnowMerge> played = new HashSet<SnowMerge>();
        private readonly Dictionary<SnowMerge, float> seenAt = new Dictionary<SnowMerge, float>();
        private int freshStratum;
        private float freshAt = -10f;
        private float lastMoveSound = -10f;
        private float nextIdle;
        private string labelKey = string.Empty;
        private readonly Dictionary<GridPos, TextMesh> heapLabels = new Dictionary<GridPos, TextMesh>();
        private readonly Dictionary<GridPos, float> labelPulse = new Dictionary<GridPos, float>();
        private readonly HashSet<GridPos> labelHidden = new HashSet<GridPos>();

        private static Sprite ringSprite;

        private void EnsureRoot(BoardView view)
        {
            if (view != null && board != view)
            {
                board = view;
                board.SnowMotion = this;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            if (root == null)
            {
                var go = new GameObject("SnowLayer");
                go.transform.SetParent(transform, false);
                root = go.transform;
                labels = Child(root, "SnowLabels");
                aimRoot = Child(root, "SnowAim");
                debugRoot = Child(root, "SnowDebug");
            }
            FollowBoard();
        }

        private static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Hooks this layer to the board, so its falls tell it about snow.</summary>
        public void Attach(BoardView view)
        {
            EnsureRoot(view);
        }

        private void FollowBoard()
        {
            if (board == null || root == null)
            {
                return;
            }
            root.position = board.transform.position;
            root.rotation = board.transform.rotation;
            root.localScale = board.transform.lossyScale;
        }

        private float Cell
        {
            get { return board != null ? board.CellWorldSize : 1f; }
        }

        private GameBoard Cells
        {
            get { return board != null ? board.Board : null; }
        }

        private GridPos Flow
        {
            get { return Cells != null ? Cells.WaterFlow : new GridPos(0, -1); }
        }

        private static Vector2 V(GridPos p)
        {
            return new Vector2(p.X, p.Y);
        }

        private static uint Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + 977);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        private static float Hash01(int x, int y, int salt)
        {
            return (Hash(x * 31 + salt, y * 17 - salt) & 0xFFFFu) / 65535f;
        }

        /// <summary>The material age a melt time reads as: 5 FRESH .. 1 COLLAPSING.</summary>
        public static float AgeOf(int melt)
        {
            if (melt >= SnowRules.MeltTurns) return 0f;
            if (melt == 4) return 0.2f;
            if (melt == 3) return 0.45f;
            if (melt == 2) return 0.7f;
            return 1f;
        }

        // =================================================================== per frame

        private void LateUpdate()
        {
            if (board == null || Cells == null || root == null)
            {
                return;
            }
            FollowBoard();
            float now = Time.time;
            ExpireMerges(now);
            PaintCells(now);
            RefreshLabels(now);
            Idle(now);
            StepParticles(Time.deltaTime);
            TickAim(now);
            TickScore(Time.deltaTime);
            DrawDebug();
        }

        /// <summary>A merge the board never animated (no fall was played) is simply shown done.</summary>
        private void ExpireMerges(float now)
        {
            List<SnowMerge> merges = Cells.SnowMerges;
            if (merges.Count == 0 && (played.Count > 0 || seenAt.Count > 0))
            {
                // the engine cleared its report: nothing remembered is still reachable
                played.Clear();
                seenAt.Clear();
            }
            for (int i = 0; i < merges.Count; i++)
            {
                if (played.Contains(merges[i]))
                {
                    continue;
                }
                float at;
                if (!seenAt.TryGetValue(merges[i], out at))
                {
                    seenAt[merges[i]] = now;
                }
                else if (now - at > 2.5f)
                {
                    played.Add(merges[i]);
                }
            }
        }

        /// <summary>The merge still waiting to be played that a cell's heap belongs to, or null.</summary>
        private SnowMerge PendingFor(GridPos cell)
        {
            List<SnowMerge> merges = Cells.SnowMerges;
            for (int i = merges.Count - 1; i >= 0; i--)
            {
                if (!played.Contains(merges[i]) && merges[i].Heap.Contains(cell))
                {
                    return merges[i];
                }
            }
            return null;
        }

        /// <summary>Writes every snow cube's state into its renderer.</summary>
        private void PaintCells(float now)
        {
            GameBoard cells = Cells;
            Material snow = ViewUtil.SnowMaterial;
            GridPos flow = Flow;
            for (int x = cells.MinX; x < cells.MinX + cells.Width; x++)
            {
                for (int y = cells.MinY; y < cells.MinY + cells.Height; y++)
                {
                    var at = new GridPos(x, y);
                    Cube? cube = cells.GetCube(at);
                    if (!cube.HasValue || cube.Value.Kind != CubeKind.Snow)
                    {
                        looks.Remove(at);
                        continue;
                    }
                    SpriteRenderer r = board.CellRendererAt(at);
                    if (r == null || snow == null || r.sharedMaterial != snow)
                    {
                        continue;
                    }
                    CellLook look;
                    if (!looks.TryGetValue(at, out look))
                    {
                        look = new CellLook();
                        looks[at] = look;
                    }
                    SnowMerge pending = PendingFor(at);
                    int melt = pending != null ? pending.MeltBefore : cube.Value.SnowMelt;
                    // a turn passing: a small settle, a glint at 3 -> 2
                    if (look.LastMelt > 0 && melt < look.LastMelt && pending == null)
                    {
                        look.SettleAt = now;
                        if (look.LastMelt == 3 && melt == 2)
                        {
                            Glint(board.CellToWorld(at), Cell);
                        }
                    }
                    look.LastMelt = melt;
                    float target = AgeOf(melt);
                    if (cube.Value.SnowStratum != 0 && cube.Value.SnowStratum == freshStratum)
                    {
                        // the avalanche's own snow: distinct and fresh-pressed for a breath, then
                        // into the 3-turn family it really is
                        float k = Mathf.Clamp01((now - freshAt - Style.avalancheFreshHold)
                            / Mathf.Max(0.01f, Style.avalancheFreshBlend));
                        target *= k;
                    }
                    look.Age = look.Age < 0f ? target
                        : Mathf.MoveTowards(look.Age, target, Time.deltaTime / Mathf.Max(0.05f, Style.snowAgeBlend));
                    float seam = cells.SnowSeamBelow(at) ? 1f : 0f;
                    float barrier = Mathf.Clamp01(1f - (now - look.BarrierAt) / 0.5f);
                    float sweepK = (now - look.SweepAt) / 0.45f;
                    float aimGlow = aimEligible.Contains(at) ? 1f : 0f;
                    float aimMute = aimSpent.Contains(at) ? 1f : 0f;
                    r.GetPropertyBlock(block);
                    block.SetFloat(AgeId, look.Age);
                    block.SetFloat(MeltId, 0f);
                    block.SetFloat(PackedId, cube.Value.SnowStratum != 0 ? 1f : 0f);
                    block.SetFloat(PressId, pressCells.Contains(at) ? pressAmount : 0f);
                    block.SetFloat(SeamId, Mathf.Clamp01(seam + barrier * 0.6f) * (seam > 0f ? 1f : barrier));
                    block.SetVector(SweepId, sweepK >= 0f && sweepK <= 1f
                        ? new Vector4(look.SweepFrom.x, look.SweepFrom.y, sweepK * 1.3f, (1f - sweepK) * 0.9f)
                        : Vector4.zero);
                    block.SetFloat(GlowId, aimGlow * aimLevel);
                    block.SetFloat(MuteId, aimMute * aimLevel);
                    block.SetFloat(SeedId, (Hash(x, y) & 1023u) / 37f);
                    r.SetPropertyBlock(block);
                    // a turn's settle: a breath of squash along the flow, from the cube's foot
                    float sk = (now - look.SettleAt) / Mathf.Max(0.01f, Style.snowTurnAgeSettle);
                    if (sk >= 0f && sk <= 1f)
                    {
                        float a = 1f - 0.03f * Mathf.Sin(sk * Mathf.PI);
                        board.SetCellSquash(at, flow.X != 0 ? new Vector2(a, 1f) : new Vector2(1f, a));
                        look.SquashUntil = now + 0.01f;
                    }
                    else if (look.SquashUntil > 0f && now > look.SquashUntil && !squashing.Contains(at))
                    {
                        board.SetCellSquash(at, Vector2.one);
                        look.SquashUntil = 0f;
                    }
                }
            }
        }

        /// <summary>Gives a cube drawn away from the board (a drop in flight, a proxy) the snow's
        /// own look.</summary>
        public void Dress(SpriteRenderer renderer, Cube cube)
        {
            DressProxy(renderer, AgeOf(cube.SnowMelt), cube.SnowStratum != 0 ? 1f : 0f, 0f,
                (Hash(cube.SourceCardId, cube.SnowMelt) & 1023u) / 37f);
        }

        private void DressProxy(SpriteRenderer renderer, float age, float packed, float press, float seed)
        {
            if (renderer == null)
            {
                return;
            }
            Material snow = ViewUtil.SnowMaterial;
            if (snow != null)
            {
                renderer.sharedMaterial = snow;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            renderer.GetPropertyBlock(block);
            block.SetFloat(AgeId, age);
            block.SetFloat(MeltId, 0f);
            block.SetFloat(PackedId, packed);
            block.SetFloat(PressId, press);
            block.SetFloat(SeamId, 0f);
            block.SetVector(SweepId, Vector4.zero);
            block.SetFloat(GlowId, 0f);
            block.SetFloat(MuteId, 0f);
            block.SetFloat(SeedId, seed);
            renderer.SetPropertyBlock(block);
        }

        private void SetProxyFloat(SpriteRenderer renderer, int id, float value)
        {
            if (renderer == null || ViewUtil.SnowMaterial == null)
            {
                return;
            }
            renderer.GetPropertyBlock(block);
            block.SetFloat(id, value);
            renderer.SetPropertyBlock(block);
        }

        private SpriteRenderer SnowProxy(Transform parent, Vector2 at, float size, int order)
        {
            SpriteRenderer snow = ViewUtil.MakeCell(parent, "SnowProxy", at, size, Color.white, order);
            ViewUtil.ApplyTile(snow, ViewUtil.SnowTile, size);
            snow.color = Color.white;
            return snow;
        }

        // =================================================================== the numbers

        /// <summary>Every repaint: the heaps' numbers follow the board, and a merge that came with
        /// this repaint is waiting to be played.</summary>
        public void Sync(BoardView view)
        {
            if (view == null || view.Board == null)
            {
                return;
            }
            EnsureRoot(view);
            labelKey = string.Empty; // rebuilt on the next frame
        }

        private void RefreshLabels(float now)
        {
            GameBoard cells = Cells;
            bool hide = board.IsDark || !Show.SnowPower;
            if (labels.gameObject.activeSelf == hide)
            {
                labels.gameObject.SetActive(!hide);
            }
            // one label per heap, keyed on what it shows
            var key = new System.Text.StringBuilder();
            var heaps = new List<KeyValuePair<GridPos, int>>();
            var done = new HashSet<GridPos>();
            for (int x = cells.MinX; x < cells.MinX + cells.Width; x++)
            {
                for (int y = cells.MinY; y < cells.MinY + cells.Height; y++)
                {
                    var at = new GridPos(x, y);
                    Cube? cube = cells.GetCube(at);
                    if (!cube.HasValue || cube.Value.Kind != CubeKind.Snow || done.Contains(at))
                    {
                        continue;
                    }
                    List<GridPos> heap = cells.SnowHeapAt(at);
                    for (int i = 0; i < heap.Count; i++)
                    {
                        done.Add(heap[i]);
                    }
                    SnowMerge pending = PendingFor(at);
                    int power = pending != null ? pending.PowerBefore : cube.Value.SnowPower;
                    GridPos anchor = heap[heap.Count - 1];
                    heaps.Add(new KeyValuePair<GridPos, int>(anchor, power));
                    key.Append(anchor.X).Append(',').Append(anchor.Y).Append('=').Append(power).Append(';');
                }
            }
            string k = key.ToString();
            if (k != labelKey)
            {
                labelKey = k;
                foreach (KeyValuePair<GridPos, TextMesh> old in heapLabels)
                {
                    if (old.Value != null)
                    {
                        Destroy(old.Value.gameObject);
                    }
                }
                heapLabels.Clear();
                for (int i = 0; i < heaps.Count; i++)
                {
                    heapLabels[heaps[i].Key] = MakeNumber(heaps[i].Key, heaps[i].Value);
                }
            }
            // the pulses a merge or an avalanche asked for
            foreach (KeyValuePair<GridPos, TextMesh> label in heapLabels)
            {
                if (label.Value == null)
                {
                    continue;
                }
                float scale = 1f;
                float pulse;
                if (labelPulse.TryGetValue(label.Key, out pulse))
                {
                    float t = (now - pulse) / Mathf.Max(0.01f, Style.snowPowerPulseDuration);
                    if (t >= 0f && t <= 1f)
                    {
                        scale = t < 0.35f ? Mathf.Lerp(1f, 0.82f, t / 0.35f) : Mathf.Lerp(1.18f, 1f, (t - 0.35f) / 0.65f);
                    }
                }
                label.Value.transform.localScale = new Vector3(scale, scale, 1f);
                bool off = labelHidden.Contains(label.Key) || board.IsCellCovered(label.Key);
                if (label.Value.gameObject.activeSelf == off)
                {
                    label.Value.gameObject.SetActive(!off);
                }
            }
        }

        /// <summary>The heap's power: a small number tucked into the corner of the heap's last cell,
        /// deep blue-grey with an off-white rim.</summary>
        private TextMesh MakeNumber(GridPos anchor, int power)
        {
            float cell = Cell;
            Vector2 at = board.CellToWorld(anchor) + new Vector2(cell * 0.27f, cell * 0.27f);
            // a glyph is characterSize * fontSize / 10 tall: about a fifth of a cell
            TextMesh text = ViewUtil.MakeText3D(labels, "SnowPower", at, power.ToString(), 90,
                cell * 0.022f, Style.PowerInk, LabelOrder, TextAnchor.MiddleCenter);
            text.fontStyle = FontStyle.Bold;
            foreach (Transform outline in text.transform)
            {
                TextMesh rim = outline.GetComponent<TextMesh>();
                if (rim != null)
                {
                    rim.color = Style.PowerRim;
                    rim.fontStyle = FontStyle.Bold;
                }
            }
            return text;
        }

        private void PulseLabel(GridPos cellOfHeap, float delay)
        {
            List<GridPos> heap = Cells.SnowHeapAt(cellOfHeap);
            GridPos anchor = heap.Count > 0 ? heap[heap.Count - 1] : cellOfHeap;
            labelPulse[anchor] = Time.time + delay;
        }

        // =================================================================== idle

        private void Idle(float now)
        {
            if (now < nextIdle || Playing || Level == Quality.Low || looks.Count == 0)
            {
                return;
            }
            nextIdle = now + UnityEngine.Random.Range(3f, 6f);
            // a grain or two of powder settling off one cube's edge - never an emitter
            int pick = UnityEngine.Random.Range(0, looks.Count);
            foreach (GridPos at in looks.Keys)
            {
                if (pick-- > 0)
                {
                    continue;
                }
                Vector2 c = board.CellToWorld(at);
                Vector2 down = V(Flow);
                Vector2 side = new Vector2(-down.y, down.x);
                for (int i = 0; i < UnityEngine.Random.Range(1, 3); i++)
                {
                    Spawn(Kind.Grain, c + side * (Cell * UnityEngine.Random.Range(-0.4f, 0.4f)) - down * (Cell * 0.42f),
                        down * (Cell * 0.12f) + side * (Cell * UnityEngine.Random.Range(-0.08f, 0.08f)),
                        Cell * 0.045f, Style.Powder, 0.6f, false);
                }
                break;
            }
        }

        // =================================================================== motion (ISnowMotion)

        public void Trail(Vector2 local, GridPos step, float speed)
        {
            EnsureRoot(board);
            if (Time.time - lastMoveSound > 0.25f && Sfx != null)
            {
                lastMoveSound = Time.time;
                Sfx.Snow(SnowCue.Move);
            }
            // 3-6 grains over a fall: one now and then, behind it
            float rate = Level == Quality.Low ? 0.04f : 0.11f;
            if (UnityEngine.Random.value > rate * (0.5f + speed))
            {
                return;
            }
            Vector2 down = V(step);
            Vector2 side = new Vector2(-down.y, down.x);
            Spawn(Kind.Grain, local - down * (Cell * 0.45f) + side * (Cell * UnityEngine.Random.Range(-0.35f, 0.35f)),
                -down * (Cell * 0.25f) + side * (Cell * UnityEngine.Random.Range(-0.2f, 0.2f)),
                Cell * UnityEngine.Random.Range(0.03f, 0.055f),
                UnityEngine.Random.value < 0.3f ? Style.PowderBlue : Style.Powder, 0.35f, false);
        }

        public void Landed(GridPos cell, GridPos step)
        {
            EnsureRoot(board);
            if (Sfx != null)
            {
                Sfx.Snow(SnowCue.Arrive);
            }
            Vector2 c = board.CellToWorld(cell);
            Vector2 down = V(step);
            Vector2 side = new Vector2(-down.y, down.x);
            int count = Level == Quality.Low ? 2 : 2 + (int)(Hash01(cell.X, cell.Y, 3) * 2.99f);
            for (int i = 0; i < count; i++)
            {
                float s = i % 2 == 0 ? -1f : 1f;
                Spawn(Kind.Grain, c + down * (Cell * 0.42f) + side * (s * Cell * 0.3f),
                    side * (s * Cell * UnityEngine.Random.Range(0.4f, 0.8f)) - down * (Cell * 0.15f),
                    Cell * 0.045f, i == 0 ? Style.PowderBlue : Style.Powder, 0.32f, false);
            }
        }

        public bool IsAbsorb(WaterMove move)
        {
            if (Cells == null)
            {
                return false;
            }
            List<SnowMerge> merges = Cells.SnowMerges;
            for (int i = 0; i < merges.Count; i++)
            {
                if (played.Contains(merges[i]))
                {
                    continue;
                }
                List<WaterMove> absorbed = merges[i].Absorbed;
                for (int a = 0; a < absorbed.Count; a++)
                {
                    if (absorbed[a].From.Equals(move.From) && absorbed[a].To.Equals(move.To))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Absorbed(GridPos from, GridPos into, GridPos step)
        {
            EnsureRoot(board);
            SnowMerge merge = null;
            List<SnowMerge> merges = Cells.SnowMerges;
            for (int i = 0; i < merges.Count && merge == null; i++)
            {
                if (played.Contains(merges[i]))
                {
                    continue;
                }
                foreach (WaterMove move in merges[i].Absorbed)
                {
                    if (move.From.Equals(from) && move.To.Equals(into))
                    {
                        merge = merges[i];
                        break;
                    }
                }
            }
            StartCoroutine(MergeOne(from, into, step, merge));
        }

        private readonly HashSet<GridPos> squashing = new HashSet<GridPos>();

        /// <summary>
        /// One incoming cube meeting the heap it merges into. The first cube of a merge plays the
        /// whole heap's answer (the squash, the ring, the number, the refresh); a second cube of the
        /// same arriving heap only presses and spreads.
        /// </summary>
        private IEnumerator MergeOne(GridPos from, GridPos into, GridPos step, SnowMerge merge)
        {
            bool lead = merge != null && !played.Contains(merge);
            if (merge != null)
            {
                played.Add(merge);
            }
            float cell = Cell;
            float size = cell * BoardView.CubeCellShare;
            Vector2 down = V(step);
            Vector2 side = new Vector2(-down.y, down.x);
            Vector2 heapAt = board.CellToWorld(into);
            Vector2 seam = heapAt - down * (cell * 0.5f);
            SpriteRenderer incoming = SnowProxy(root, heapAt - down * cell, size, FrontOrder);
            DressProxy(incoming, merge != null ? AgeOf(merge.ArrivingMelt) : 0f, 0f, 0f, 3.1f);
            if (Sfx != null && lead)
            {
                Sfx.Snow(SnowCue.Merge);
            }
            List<GridPos> heap = merge != null ? merge.Heap : new List<GridPos> { into };
            for (int i = 0; i < heap.Count; i++)
            {
                squashing.Add(heap[i]);
            }
            int flecks = Level == Quality.Low ? 3 : 4 + (int)(Hash01(into.X, into.Y, 7) * 4.99f);
            bool burst = false;
            bool pulsed = false;
            float contact = Style.snowMergeContactDuration;
            float compress = Style.snowMergeCompressDuration;
            float settle = Style.snowMergeSettleDuration;
            float total = contact + compress + settle + 0.05f;
            float t = 0f;
            while (t < total)
            {
                if (board == null)
                {
                    break;
                }
                t += Time.deltaTime;
                // ---- the incoming mass: presses on, then spreads into the heap (never a fade)
                float into01;
                if (t < contact)
                {
                    float k = t / contact;
                    into01 = 0.15f * k;
                }
                else
                {
                    float k = Mathf.Clamp01((t - contact) / compress);
                    into01 = 0.15f + 0.85f * (1f - (1f - k) * (1f - k));
                }
                float thin = Mathf.Lerp(1f, 0.08f, Mathf.Clamp01((into01 - 0.1f) / 0.9f));
                float wide = Mathf.Lerp(1f, 1.16f, into01);
                // its foot rides into the heap's top edge, the rest following
                Vector2 pos = heapAt - down * (cell * (1f - 0.55f * into01))
                    - down * (size * 0.5f * (1f - thin)) * 0f;
                pos = seam - down * (size * 0.5f * thin) + down * (cell * 0.12f * into01);
                if (incoming != null)
                {
                    incoming.transform.localPosition = pos;
                    incoming.transform.localScale = down.x != 0
                        ? new Vector3(size * thin, size * wide, 1f)
                        : new Vector3(size * wide, size * thin, 1f);
                    float a = 1f - Mathf.Clamp01((into01 - 0.85f) / 0.15f);
                    incoming.color = new Color(1f, 1f, 1f, a);
                }
                // ---- the heap: flattens along the flow at the contact, then recovers
                if (lead)
                {
                    float hk = Mathf.Clamp01(t / (contact + compress * 0.6f));
                    float rk = Mathf.Clamp01((t - contact - compress * 0.6f) / (compress * 0.4f + settle));
                    float along = t < contact + compress * 0.6f
                        ? Mathf.Lerp(1f, 0.94f, Mathf.Sin(hk * Mathf.PI * 0.5f))
                        : Mathf.Lerp(0.94f, 1f, 1f - (1f - rk) * (1f - rk));
                    float acrossS = 1f + (1f - along) * 0.5f;
                    for (int i = 0; i < heap.Count; i++)
                    {
                        board.SetCellSquash(heap[i], down.x != 0 ? new Vector2(along, acrossS) : new Vector2(acrossS, along));
                    }
                }
                // ---- powder out of the seam, sideways
                if (!burst && t >= contact * 0.8f)
                {
                    burst = true;
                    for (int i = 0; i < flecks; i++)
                    {
                        float s = i % 2 == 0 ? -1f : 1f;
                        Spawn(Kind.Grain, seam + side * (s * cell * UnityEngine.Random.Range(0.1f, 0.42f)),
                            side * (s * cell * UnityEngine.Random.Range(0.7f, 1.4f)) - down * (cell * UnityEngine.Random.Range(0.05f, 0.3f)),
                            cell * UnityEngine.Random.Range(0.035f, 0.06f), i % 3 == 0 ? Style.PowderBlue : Style.Powder,
                            UnityEngine.Random.Range(0.28f, 0.42f), false);
                    }
                    if (lead && merge != null)
                    {
                        // the cold ring closing in on the heap
                        Vector2 centre = Vector2.zero;
                        for (int i = 0; i < heap.Count; i++)
                        {
                            centre += board.CellToWorld(heap[i]);
                        }
                        centre /= heap.Count;
                        Spawn(Kind.Ring, centre, Vector2.zero, cell * (0.9f + 0.5f * heap.Count), Style.PressureRing, 0.32f, true);
                        // a refresh: the cooling sweep runs out from the contact
                        if (merge.Refreshed)
                        {
                            for (int i = 0; i < heap.Count; i++)
                            {
                                CellLook look;
                                if (!looks.TryGetValue(heap[i], out look))
                                {
                                    look = new CellLook();
                                    looks[heap[i]] = look;
                                }
                                look.SweepAt = Time.time;
                                Vector2 offset = (seam - board.CellToWorld(heap[i])) / cell;
                                look.SweepFrom = new Vector2(0.5f + offset.x, 0.5f + offset.y);
                            }
                            if (Sfx != null)
                            {
                                Sfx.Snow(SnowCue.Refresh);
                            }
                        }
                        // a merge that stopped at a seam lights it
                        for (int i = 0; i < merge.BarrierUnder.Count; i++)
                        {
                            CellLook look;
                            if (looks.TryGetValue(merge.BarrierUnder[i], out look))
                            {
                                look.BarrierAt = Time.time;
                            }
                        }
                    }
                }
                // ---- the number: old -> pressed -> new, with a tick
                if (lead && !pulsed && t >= contact + compress * 0.5f)
                {
                    pulsed = true;
                    PulseLabel(into, -Style.snowPowerPulseDuration * 0.35f);
                    labelKey = string.Empty;
                    if (Sfx != null)
                    {
                        Sfx.Snow(SnowCue.PowerTick);
                    }
                }
                yield return null;
            }
            if (incoming != null)
            {
                Destroy(incoming.gameObject);
            }
            for (int i = 0; i < heap.Count; i++)
            {
                squashing.Remove(heap[i]);
                if (lead && board != null)
                {
                    board.SetCellSquash(heap[i], Vector2.one);
                }
            }
            labelKey = string.Empty;
        }

        // =================================================================== the melt

        /// <summary>Snow whose time ran out: soft, collapsing, wet at the edge, eaten inward, a few
        /// drops, an empty cell. The cells are already empty on the board.</summary>
        public void PlayMelt(BoardView view, IReadOnlyList<DestroyedCube> melted)
        {
            if (view == null || view.Board == null || melted == null || melted.Count == 0 || view.IsDark)
            {
                return;
            }
            EnsureRoot(view);
            if (Sfx != null)
            {
                Sfx.Snow(SnowCue.Melt);
            }
            for (int i = 0; i < melted.Count; i++)
            {
                StartCoroutine(MeltOne(melted[i], i));
            }
        }

        private IEnumerator MeltOne(DestroyedCube melted, int index)
        {
            float cell = Cell;
            float size = cell * BoardView.CubeCellShare;
            Vector2 at = board.CellToWorld(melted.Pos);
            Vector2 down = V(Flow);
            SpriteRenderer snow = SnowProxy(root, at, size, ProxyOrder);
            DressProxy(snow, 1f, melted.Cube.SnowStratum != 0 ? 1f : 0f, 0f, (Hash(melted.Pos.X, melted.Pos.Y) & 1023u) / 37f);
            bool shader = ViewUtil.SnowMaterial != null;
            int drops = Level == Quality.Low ? 2 : 2 + (int)(Hash01(melted.Pos.X, melted.Pos.Y, 11) * 3.99f);
            bool dripped = false;
            float wait = 0.025f * index;
            float t = -wait;
            float dur = Mathf.Max(0.1f, Style.snowMeltDuration);
            while (t < dur)
            {
                if (board == null)
                {
                    break;
                }
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                // A: the surface gives a little, the edges draw in
                float s = k < 0.25f ? Mathf.Lerp(1f, 0.98f, k / 0.25f) : 0.98f;
                // the foot stays where it was: it sinks toward its own floor
                float sink = Mathf.Lerp(1f, 0.9f, Mathf.Clamp01((k - 0.2f) / 0.8f));
                if (shader)
                {
                    SetProxyFloat(snow, MeltId, Mathf.Clamp01(k * 1.05f) * Style.snowMeltWetnessStrength);
                }
                else
                {
                    // no shader: shrink unevenly toward the middle and fade
                    float wobble = 0.06f * Mathf.Sin(k * 9f + melted.Pos.X);
                    s *= Mathf.Lerp(1f, 0.25f, k) * (1f + wobble);
                    snow.color = new Color(Mathf.Lerp(1f, 0.78f, k), Mathf.Lerp(1f, 0.88f, k), 1f, 1f - k * k);
                }
                bool vertical = Mathf.Abs(down.y) > 0.5f;
                snow.transform.localScale = vertical
                    ? new Vector3(size * s, size * s * sink, 1f)
                    : new Vector3(size * s * sink, size * s, 1f);
                snow.transform.localPosition = at + down * (size * 0.5f * (1f - sink));
                if (!dripped && k > 0.55f)
                {
                    dripped = true;
                    for (int i = 0; i < drops; i++)
                    {
                        Vector2 side = new Vector2(-down.y, down.x);
                        Spawn(Kind.Drop, at + side * (cell * UnityEngine.Random.Range(-0.3f, 0.3f))
                                + down * (cell * UnityEngine.Random.Range(-0.1f, 0.3f)),
                            down * (cell * UnityEngine.Random.Range(0.15f, 0.35f)),
                            cell * UnityEngine.Random.Range(0.05f, 0.08f), Style.MeltWater,
                            UnityEngine.Random.Range(0.3f, 0.45f), true);
                    }
                }
                yield return null;
            }
            if (snow != null)
            {
                Destroy(snow.gameObject);
            }
        }

        private void Glint(Vector2 at, float cell)
        {
            if (Level == Quality.Low)
            {
                return;
            }
            Spawn(Kind.Glint, at + new Vector2(cell * 0.12f, cell * 0.1f), Vector2.zero, cell * 0.22f,
                new Color(0.86f, 0.94f, 1f, 0.85f), 0.3f, true);
        }

        // =================================================================== the aim

        private readonly HashSet<GridPos> aimEligible = new HashSet<GridPos>();
        private readonly HashSet<GridPos> aimSpent = new HashSet<GridPos>();
        private int aimFrame = -10;
        private bool aiming;
        private float aimLevel;
        private string aimKey = string.Empty;
        private AvalanchePlan aimPlan;
        private readonly List<SpriteRenderer> aimShimmer = new List<SpriteRenderer>();

        /// <summary>
        /// Called every frame the "Çığ" aim is over the board, with the PLAN Core made for the line
        /// under the pointer (it IS the preview report): the row read through two broken frost traces
        /// and a cold haze, the heaps that will go brightening, a soft ghost of compressed snow over
        /// every cell the avalanche will really reach, a pressure edge and a small inward shadow on
        /// every block it will crush, and the spent layers muted with a tiny mark. A line that cannot
        /// go is muted, nothing glows.
        /// </summary>
        public void ShowAim(BoardView view, AvalanchePlan plan)
        {
            if (view == null || view.Board == null || plan == null)
            {
                return;
            }
            EnsureRoot(view);
            aimFrame = Time.frameCount;
            if (!aiming)
            {
                aiming = true;
                if (Sfx != null)
                {
                    Sfx.Snow(SnowCue.AimPulse);
                }
                if (AimChanged != null)
                {
                    AimChanged(true);
                }
            }
            string key = plan.Line + ":" + plan.Blocked + ":" + plan.Columns.Count + ":" + plan.Depth
                + ":" + plan.Flow.X + "," + plan.Flow.Y + ":" + plan.SpentCells.Count;
            if (key == aimKey && aimPlan != null)
            {
                return;
            }
            aimKey = key;
            aimPlan = plan;
            BuildAim(plan);
        }

        /// <summary>Puts the aim away at once (the activation, a cancel).</summary>
        public void HideAim()
        {
            aimFrame = -10;
            aimLevel = 0f;
            ClearAim();
            if (aiming)
            {
                aiming = false;
                if (AimChanged != null)
                {
                    AimChanged(false);
                }
            }
        }

        private void ClearAim()
        {
            if (aimRoot != null)
            {
                for (int i = aimRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(aimRoot.GetChild(i).gameObject);
                }
            }
            aimShimmer.Clear();
            aimEligible.Clear();
            aimSpent.Clear();
            aimKey = string.Empty;
            aimPlan = null;
        }

        private void TickAim(float now)
        {
            if (aimFrame >= Time.frameCount - 1)
            {
                aimLevel = Mathf.MoveTowards(aimLevel, 1f, Time.deltaTime / 0.12f);
            }
            else if (aiming)
            {
                aimLevel = Mathf.MoveTowards(aimLevel, 0f, Time.deltaTime / 0.1f);
                if (aimLevel <= 0f)
                {
                    HideAim();
                }
            }
            for (int i = 0; i < aimShimmer.Count; i++)
            {
                SpriteRenderer r = aimShimmer[i];
                if (r == null)
                {
                    continue;
                }
                Color c = r.color;
                float baseA = r.transform.localScale.z; // the authored alpha rides in z
                c.a = baseA * aimLevel * (0.85f + 0.15f * Mathf.Sin(now * 3.1f + i * 1.7f));
                r.color = c;
            }
        }

        private SpriteRenderer AimSprite(string name, Sprite sprite, Vector2 at, Vector2 size, Color colour, int order)
        {
            SpriteRenderer r = ViewUtil.MakeRect(aimRoot, name, at, Vector2.one, colour, order);
            r.sprite = sprite;
            Vector2 bounds = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            // z carries the authored alpha for the shimmer (a scale of z is invisible on a sprite)
            r.transform.localScale = new Vector3(size.x / Mathf.Max(0.0001f, bounds.x),
                size.y / Mathf.Max(0.0001f, bounds.y), colour.a);
            aimShimmer.Add(r);
            return r;
        }

        private void BuildAim(AvalanchePlan plan)
        {
            ClearAim();
            aimPlan = plan;
            GameBoard cells = Cells;
            float cell = Cell;
            bool valid = plan.Blocked == AvalancheBlock.None;
            bool vertical = plan.Flow.X == 0;
            // ---- the row: two broken frost traces along its edges and a cold haze in it
            GridPos first = vertical ? new GridPos(cells.MinX, plan.Line) : new GridPos(plan.Line, cells.MinY);
            GridPos last = vertical ? new GridPos(cells.MinX + cells.Width - 1, plan.Line)
                : new GridPos(plan.Line, cells.MinY + cells.Height - 1);
            Vector2 a = board.CellToWorld(first);
            Vector2 b = board.CellToWorld(last);
            Vector2 mid = (a + b) * 0.5f;
            float length = Vector2.Distance(a, b) + cell;
            Vector2 across = vertical ? new Vector2(0f, 1f) : new Vector2(1f, 0f);
            Color frost = valid ? Style.Frost : Style.Muted;
            AimSprite("RowHaze", ViewUtil.GlowSprite, mid, vertical ? new Vector2(length, cell * 1.05f) : new Vector2(cell * 1.05f, length),
                valid ? Style.Haze : new Color(Style.Muted.r, Style.Muted.g, Style.Muted.b, 0.04f), GhostOrder);
            int pieces = Mathf.Max(3, Mathf.RoundToInt(length / cell) * 2);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < pieces; i++)
                {
                    float u = (i + 0.5f) / pieces - 0.5f;
                    if (Hash01(i, side, plan.Line) < 0.22f)
                    {
                        continue; // broken, so it reads as frost and not a frame
                    }
                    Vector2 along = vertical ? new Vector2(u * length, 0f) : new Vector2(0f, u * length);
                    float wob = (Hash01(i, side * 3, plan.Line) - 0.5f) * cell * 0.04f;
                    Vector2 at = mid + along + across * (side * (cell * 0.5f + wob));
                    float piece = length / pieces * Mathf.Lerp(0.6f, 0.95f, Hash01(i, side, 9));
                    AimSprite("Frost", ViewUtil.GlowSprite, at,
                        vertical ? new Vector2(piece, cell * 0.07f) : new Vector2(cell * 0.07f, piece), frost, MarkOrder);
                }
                // a wisp curling off each end
                Vector2 end = mid + (vertical ? new Vector2(side * length * 0.5f, 0f) : new Vector2(0f, side * length * 0.5f));
                AimSprite("Wisp", ViewUtil.GlowSprite, end, new Vector2(cell * 0.4f, cell * 0.4f),
                    new Color(frost.r, frost.g, frost.b, frost.a * 0.6f), MarkOrder);
            }
            if (!valid)
            {
                for (int i = 0; i < plan.SpentCells.Count; i++)
                {
                    SpentMark(plan.SpentCells[i]);
                }
                return;
            }
            // ---- what goes, where it goes, what it breaks
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                AvalancheColumn column = plan.Columns[c];
                aimEligible.Add(column.Source);
                for (int i = 0; i < column.Covered.Count; i++)
                {
                    Vector2 at = board.CellToWorld(column.Covered[i]);
                    // a soft translucent ghost of compressed snow, deeper rows a touch fainter
                    SpriteRenderer ghost = AimSprite("Footprint", ViewUtil.SnowTile, at,
                        new Vector2(cell * 0.94f, cell * 0.94f),
                        new Color(Style.Ghost.r, Style.Ghost.g, Style.Ghost.b, Style.Ghost.a * (1f - 0.12f * i)), GhostOrder);
                    if (ViewUtil.SnowMaterial != null)
                    {
                        DressProxy(ghost, 0f, 1f, 0f, i + c * 7f);
                    }
                }
                for (int i = 0; i < column.Crushed.Count; i++)
                {
                    Vector2 at = board.CellToWorld(column.Crushed[i].Pos);
                    Vector2 up = -V(plan.Flow);
                    // a pressure edge on the side the snow will come from, a small inward shadow
                    AimSprite("Pressure", ViewUtil.GlowSprite, at + up * (cell * 0.38f),
                        plan.Flow.X == 0 ? new Vector2(cell * 0.86f, cell * 0.16f) : new Vector2(cell * 0.16f, cell * 0.86f),
                        new Color(0.93f, 0.97f, 1f, 0.42f), MarkOrder + 1);
                    AimSprite("Inward", ViewUtil.GlowSprite, at, new Vector2(cell * 0.7f, cell * 0.7f),
                        Style.PressShadow, MarkOrder);
                }
            }
            for (int i = 0; i < plan.SpentCells.Count; i++)
            {
                SpentMark(plan.SpentCells[i]);
            }
        }

        /// <summary>A spent layer, while aiming only: muted and a tiny compacted-strata mark.</summary>
        private void SpentMark(GridPos cell)
        {
            aimSpent.Add(cell);
            float c = Cell;
            Vector2 at = board.CellToWorld(cell) + new Vector2(-c * 0.26f, c * 0.26f);
            for (int i = 0; i < 3; i++)
            {
                AimSprite("Spent", ViewUtil.WhiteSprite, at + new Vector2(0f, -i * c * 0.05f),
                    new Vector2(c * (0.16f - i * 0.03f), c * 0.018f), new Color(0.55f, 0.62f, 0.72f, 0.7f), LabelOrder);
            }
        }

        /// <summary>A click on a line that cannot avalanche: the snow on it presses once and lets go,
        /// a little puff collapses inward, a dry "ff".</summary>
        public void PlayRefused(BoardView view, AvalanchePlan plan, GridPos cell)
        {
            if (view == null || view.Board == null)
            {
                return;
            }
            EnsureRoot(view);
            if (Sfx != null)
            {
                Sfx.Snow(plan != null && plan.Blocked == AvalancheBlock.Spent ? SnowCue.Spent : SnowCue.AimBlocked);
            }
            var targets = new List<GridPos>();
            if (plan != null)
            {
                targets.AddRange(plan.SpentCells);
            }
            if (targets.Count == 0)
            {
                targets.Add(cell);
            }
            for (int i = 0; i < targets.Count; i++)
            {
                Spawn(Kind.Implode, board.CellToWorld(targets[i]), Vector2.zero, Cell * 0.9f,
                    new Color(0.92f, 0.96f, 1f, 0.45f), 0.26f, true);
                if (view.Board.GetCube(targets[i]).HasValue)
                {
                    StartCoroutine(Press(targets[i]));
                }
            }
        }

        private IEnumerator Press(GridPos cell)
        {
            squashing.Add(cell);
            GridPos flow = Flow;
            float t = 0f;
            while (t < 0.2f && board != null)
            {
                t += Time.deltaTime;
                float a = 1f - 0.05f * Mathf.Sin(Mathf.Clamp01(t / 0.2f) * Mathf.PI);
                board.SetCellSquash(cell, flow.X != 0 ? new Vector2(a, 1f) : new Vector2(1f, a));
                yield return null;
            }
            squashing.Remove(cell);
            if (board != null)
            {
                board.SetCellSquash(cell, Vector2.one);
            }
        }

        // =================================================================== the avalanche

        /// <summary>A cube that fell after the avalanche, drawn where it STARTED while the snow comes
        /// down (the board already shows it where it landed).</summary>
        public struct Bystander
        {
            public Vector2 At;
            public Sprite Tile;
            public Color Colour;
            public Cube Cube;
        }

        private Coroutine avalanche;
        private readonly List<GameObject> proxies = new List<GameObject>();
        private readonly List<GridPos> held = new List<GridPos>();
        private readonly HashSet<GridPos> pressCells = new HashSet<GridPos>();
        private float pressAmount;
        private string timing = string.Empty;
        private int layerShown;

        /// <summary>
        /// Plays an avalanche over a board that has ALREADY settled. <paramref name="faces"/> are the
        /// crushed cubes' faces taken before the rules ran; <paramref name="hold"/> every cell
        /// anything happened in (kept blank until the snow has come down); <paramref name="burst"/>
        /// breaks a row's blocks through the controller's cluster burst; <paramref name="landed"/> is
        /// called when the avalanche has settled and the board's own gravity may take over;
        /// <paramref name="scored"/> when the "+TOTAL" reaches the TOPLAM.
        /// </summary>
        public void PlayAvalanche(BoardView view, AvalancheVisuals visuals,
            Dictionary<GridPos, ClusterBurstView.Look> faces, IList<GridPos> hold, IList<Bystander> bystanders,
            Action<List<GridPos>, List<ClusterBurstView.Look>> burst, Action landed, Action scored)
        {
            Stop();
            if (view == null || view.Board == null || visuals == null || visuals.Plan == null || !visuals.Plan.Any)
            {
                if (landed != null) { landed(); }
                if (scored != null) { scored(); }
                return;
            }
            EnsureRoot(view);
            HideAim();
            avalanche = StartCoroutine(Avalanche(visuals, faces, hold, bystanders, burst, landed, scored));
        }

        /// <summary>Ends an avalanche in flight and gives the board its cells back.</summary>
        public void Stop()
        {
            if (avalanche != null)
            {
                StopCoroutine(avalanche);
                avalanche = null;
            }
            CleanupAvalanche();
        }

        private void CleanupAvalanche()
        {
            for (int i = 0; i < proxies.Count; i++)
            {
                if (proxies[i] != null)
                {
                    Destroy(proxies[i]);
                }
            }
            proxies.Clear();
            if (board != null && held.Count > 0)
            {
                board.ReleaseCells(held);
            }
            held.Clear();
            pressCells.Clear();
            pressAmount = 0f;
            labelHidden.Clear();
            if (board != null)
            {
                board.SetImpulse(Vector2.zero);
            }
            timing = string.Empty;
        }

        private SpriteRenderer Proxy(Vector2 at, float size, int order)
        {
            SpriteRenderer snow = SnowProxy(root, at, size, order);
            proxies.Add(snow.gameObject);
            return snow;
        }

        private sealed class FrontColumn
        {
            public AvalancheColumn Column;
            public SpriteRenderer Front;
            public Vector2 Origin;
            public readonly List<SpriteRenderer> Victims = new List<SpriteRenderer>();
            public readonly List<Vector3> VictimScale = new List<Vector3>();
            public readonly List<SpriteRenderer> Laid = new List<SpriteRenderer>();
            public readonly List<float> LaidAt = new List<float>();
            public int Broken;
        }

        /// <summary>When the front reaches layer <paramref name="k"/> (1-based) and presses on it.</summary>
        private static float ImpactTime(int k)
        {
            return Style.avalancheAnticipationDuration + Style.avalancheFrontTravelDuration
                + (k - 1) * Style.avalancheLayerInterval;
        }

        private bool Frozen(Beat beat)
        {
            return HoldAt == beat;
        }

        private IEnumerator Avalanche(AvalancheVisuals visuals, Dictionary<GridPos, ClusterBurstView.Look> faces,
            IList<GridPos> hold, IList<Bystander> bystanders, Action<List<GridPos>, List<ClusterBurstView.Look>> burst,
            Action landed, Action scored)
        {
            AvalanchePlan plan = visuals.Plan;
            float cell = Cell;
            float size = cell * BoardView.CubeCellShare;
            Vector2 down = V(plan.Flow);
            Vector2 side = new Vector2(-down.y, down.x);
            bool vertical = Mathf.Abs(down.y) > 0.5f;

            held.Clear();
            if (hold != null)
            {
                held.AddRange(hold);
            }
            board.HoldCells(held);
            if (bystanders != null)
            {
                for (int i = 0; i < bystanders.Count; i++)
                {
                    SpriteRenderer still = ViewUtil.MakeCell(root, "Bystander", bystanders[i].At, size, bystanders[i].Colour, ProxyOrder);
                    ViewUtil.ApplyTile(still, bystanders[i].Tile, size);
                    still.color = bystanders[i].Colour;
                    if (bystanders[i].Cube.Kind == CubeKind.Snow)
                    {
                        Dress(still, bystanders[i].Cube);
                    }
                    proxies.Add(still.gameObject);
                }
            }
            // the heaps' numbers dim for the avalanche
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                foreach (GridPos anchor in heapLabels.Keys)
                {
                    if (anchor.Equals(plan.Columns[c].Source))
                    {
                        labelHidden.Add(anchor);
                    }
                }
            }

            var fronts = new List<FrontColumn>();
            int depth = 0;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                AvalancheColumn column = plan.Columns[c];
                var fc = new FrontColumn { Column = column, Origin = board.CellToWorld(column.Source) };
                fc.Front = Proxy(fc.Origin, size, FrontOrder);
                DressProxy(fc.Front, AgeOf(column.SourceCube.SnowMelt), column.SourceCube.SnowStratum != 0 ? 1f : 0f, 0f, c * 3.3f);
                for (int i = 0; i < column.Covered.Count; i++)
                {
                    fc.Laid.Add(null);
                    fc.LaidAt.Add(-1f);
                    SpriteRenderer victim = null;
                    for (int v = 0; v < column.Crushed.Count; v++)
                    {
                        if (!column.Crushed[v].Pos.Equals(column.Covered[i]))
                        {
                            continue;
                        }
                        ClusterBurstView.Look look;
                        if (faces != null && faces.TryGetValue(column.Covered[i], out look) && look.Tile != null)
                        {
                            victim = ViewUtil.MakeCell(root, "Victim", board.CellToWorld(column.Covered[i]), size, look.Colour, ProxyOrder);
                            ViewUtil.ApplyTile(victim, look.Tile, size);
                            victim.color = look.Colour;
                            if (column.Crushed[v].Cube.Kind == CubeKind.Snow)
                            {
                                Dress(victim, column.Crushed[v].Cube);
                            }
                            proxies.Add(victim.gameObject);
                        }
                    }
                    fc.Victims.Add(victim);
                    fc.VictimScale.Add(victim != null ? victim.transform.localScale : Vector3.one);
                }
                depth = Mathf.Max(depth, column.Covered.Count);
                fronts.Add(fc);
            }

            // ---- the essences each paying block leaves
            var essences = new List<Particle>();
            Vector2 centre = Vector2.zero;
            int coveredCount = 0;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                for (int i = 0; i < plan.Columns[c].Covered.Count; i++)
                {
                    centre += board.CellToWorld(plan.Columns[c].Covered[i]);
                    coveredCount++;
                }
            }
            centre /= Mathf.Max(1, coveredCount);

            if (Sfx != null)
            {
                Sfx.Snow(SnowCue.PressureRumble);
            }
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                PulseLabel(plan.Columns[c].Source, 0f);
            }

            float anticipation = Style.avalancheAnticipationDuration;
            float release = Style.avalancheFrontTravelDuration;
            float pressure = Style.avalancheBlockPressureDuration;
            float destroyDelay = 0.035f;
            float fill = Style.avalancheSnowFillDuration;
            float lastImpact = ImpactTime(depth);
            float settleStart = lastImpact + pressure + destroyDelay + fill + 0.02f;
            float settleEnd = settleStart + Style.avalancheFinalSettleDuration;
            var layerDone = new bool[depth + 1];
            var layerPressed = new bool[depth + 1];
            bool released = false;
            bool settled = false;
            float t = 0f;
            float impulseUntil = -1f;
            int heroBudget = Level == Quality.Low ? 4 : Style.HeroFlecksPerLayer;
            while (t < settleEnd)
            {
                if (board == null)
                {
                    yield break;
                }
                // ---- the lab's beat isolation: freeze at the end of the named beat
                Beat at = t < anticipation ? Beat.Anticipation
                    : t < ImpactTime(1) + pressure ? Beat.FirstPressure
                    : t < ImpactTime(1) + pressure + destroyDelay + 0.02f ? Beat.FirstDestroy
                    : t < ImpactTime(1) + pressure + destroyDelay + fill ? Beat.FirstFill
                    : t < settleStart ? Beat.Chain : Beat.Settle;
                bool frozen = HoldAt != Beat.None && HoldAt != Beat.Score && BeatOrder(at) > BeatOrder(HoldAt);
                if (!frozen)
                {
                    t += Time.deltaTime;
                }
                timing = at + "  t=" + t.ToString("0.00") + "s";

                // ---- ANTICIPATION: the heap gathers pressure
                float ak = Mathf.Clamp01(t / anticipation);
                pressAmount = ak;
                for (int f = 0; f < fronts.Count; f++)
                {
                    FrontColumn fc = fronts[f];
                    SetProxyFloat(fc.Front, PressId, Mathf.Clamp01(ak * 1.2f));
                    float d = FrontDepth(t, fc.Column.Covered.Count);
                    float squash = t < anticipation ? Mathf.Lerp(1f, 0.93f, Mathf.Sin(ak * Mathf.PI * 0.5f)) : 1f;
                    // released: it stretches as it comes down, settling back when it lands
                    float moving = FrontSpeed(t, fc.Column.Covered.Count);
                    float along = squash * (1f + 0.12f * moving);
                    float acrossS = 1f + (1f - squash) * 0.6f - 0.04f * moving;
                    if (t >= settleStart)
                    {
                        float sk = Mathf.Clamp01((t - settleStart) / Style.avalancheFinalSettleDuration);
                        along = sk < 0.4f ? Mathf.Lerp(1.03f, 0.97f, sk / 0.4f) : Mathf.Lerp(0.97f, 1f, (sk - 0.4f) / 0.6f);
                        acrossS = 1f + (1f - along) * 0.5f;
                    }
                    Vector2 foot = down * (size * 0.5f * (1f - along));
                    fc.Front.transform.localPosition = fc.Origin + down * (cell * d) + foot;
                    fc.Front.transform.localScale = vertical
                        ? new Vector3(size * acrossS, size * along, 1f)
                        : new Vector3(size * along, size * acrossS, 1f);
                    // powder lifting off the heap's back while it loads
                    if (t < anticipation && Level != Quality.Low && UnityEngine.Random.value < 0.18f)
                    {
                        Spawn(Kind.Grain, fc.Origin - down * (cell * 0.45f) + side * (cell * UnityEngine.Random.Range(-0.4f, 0.4f)),
                            -down * (cell * UnityEngine.Random.Range(0.25f, 0.5f)) + side * (cell * UnityEngine.Random.Range(-0.15f, 0.15f)),
                            cell * 0.04f, Style.Powder, 0.35f, false);
                    }
                    // ---- the layers it reaches
                    for (int k = 1; k <= fc.Column.Covered.Count; k++)
                    {
                        float ik = ImpactTime(k);
                        SpriteRenderer victim = fc.Victims[k - 1];
                        if (victim != null)
                        {
                            float pk = Mathf.Clamp01((t - ik) / pressure);
                            if (t >= ik && t < ik + pressure + destroyDelay)
                            {
                                // pressed from the snow's side: 1 -> 0.94, its far side holding
                                float s = Mathf.Lerp(1f, 0.94f, pk);
                                Vector3 baseScale = fc.VictimScale[k - 1];
                                victim.transform.localScale = vertical
                                    ? new Vector3(baseScale.x * (1f + (1f - s) * 0.4f), baseScale.y * s, 1f)
                                    : new Vector3(baseScale.x * s, baseScale.y * (1f + (1f - s) * 0.4f), 1f);
                                victim.transform.localPosition = board.CellToWorld(fc.Column.Covered[k - 1])
                                    + down * (size * 0.5f * (1f - s));
                            }
                            if (t >= ik + pressure && victim.gameObject.activeSelf)
                            {
                                victim.gameObject.SetActive(false);
                            }
                        }
                        // the snow fills the cell it has passed: a beat after the block broke
                        float fillAt = ik + pressure + destroyDelay;
                        if (k < fc.Column.Covered.Count && t >= fillAt + fill * 0.6f && fc.Laid[k - 1] == null)
                        {
                            // the front moves on; this cell keeps a layer of its own
                            fc.Laid[k - 1] = Proxy(board.CellToWorld(fc.Column.Covered[k - 1]), size, ProxyOrder);
                            DressProxy(fc.Laid[k - 1], 0f, 1f, 0.6f, k * 1.7f + f);
                            fc.LaidAt[k - 1] = t;
                        }
                    }
                    for (int k = 0; k < fc.Laid.Count; k++)
                    {
                        SpriteRenderer laid = fc.Laid[k];
                        if (laid == null)
                        {
                            continue;
                        }
                        Vector2 home = board.CellToWorld(fc.Column.Covered[k]);
                        float along2 = 1f;
                        if (t >= settleStart)
                        {
                            float sk = Mathf.Clamp01((t - settleStart) / Style.avalancheFinalSettleDuration);
                            along2 = sk < 0.4f ? Mathf.Lerp(1.03f, 0.97f, sk / 0.4f) : Mathf.Lerp(0.97f, 1f, (sk - 0.4f) / 0.6f);
                        }
                        float press = Mathf.Lerp(0.6f, 0f, Mathf.Clamp01((t - fc.LaidAt[k]) / 0.4f));
                        SetProxyFloat(laid, PressId, press);
                        laid.transform.localScale = vertical
                            ? new Vector3(size * (1f + (1f - along2) * 0.5f), size * along2, 1f)
                            : new Vector3(size * along2, size * (1f + (1f - along2) * 0.5f), 1f);
                        laid.transform.localPosition = home + down * (size * 0.5f * (1f - along2));
                    }
                }

                // ---- the release
                if (!released && t >= anticipation)
                {
                    released = true;
                    if (Sfx != null)
                    {
                        Sfx.Snow(SnowCue.Release);
                    }
                    for (int f = 0; f < fronts.Count; f++)
                    {
                        Vector2 o = fronts[f].Origin;
                        Spawn(Kind.Puff, o + down * (cell * 0.4f), down * (cell * 0.6f), cell * 0.9f,
                            new Color(0.95f, 0.97f, 1f, 0.32f), 0.3f, true);
                    }
                }

                // ---- each layer's impact, all columns together: the rhythm
                for (int k = 1; k <= depth; k++)
                {
                    float ik = ImpactTime(k);
                    if (!layerPressed[k] && t >= ik)
                    {
                        layerPressed[k] = true;
                        layerShown = k;
                        // the cold pressure reflection on the snow side of every block in the row
                        for (int f = 0; f < fronts.Count; f++)
                        {
                            if (k <= fronts[f].Victims.Count && fronts[f].Victims[k - 1] != null)
                            {
                                Vector2 vc = board.CellToWorld(fronts[f].Column.Covered[k - 1]);
                                Spawn(Kind.Puff, vc - down * (cell * 0.36f), Vector2.zero,
                                    cell * 0.8f, new Color(0.96f, 0.98f, 1f, 0.5f), pressure + 0.05f, true, true);
                            }
                        }
                    }
                    if (!layerDone[k] && t >= ik + pressure)
                    {
                        layerDone[k] = true;
                        if (Sfx != null)
                        {
                            Sfx.Snow(SnowCue.RowImpact, 1f + 0.03f * (k - 1));
                        }
                        impulseUntil = t + 0.08f;
                        var rowCells = new List<GridPos>();
                        var rowLooks = new List<ClusterBurstView.Look>();
                        int flecks = 0;
                        for (int f = 0; f < fronts.Count; f++)
                        {
                            FrontColumn fc = fronts[f];
                            if (k > fc.Column.Covered.Count)
                            {
                                continue;
                            }
                            GridPos target = fc.Column.Covered[k - 1];
                            Vector2 tc = board.CellToWorld(target);
                            bool hadBlock = fc.Victims[k - 1] != null;
                            if (hadBlock)
                            {
                                ClusterBurstView.Look look = faces[target];
                                rowCells.Add(target);
                                rowLooks.Add(look);
                                // a score essence where it broke (snow buried under snow pays nothing)
                                bool pays = true;
                                for (int v = 0; v < fc.Column.Crushed.Count; v++)
                                {
                                    if (fc.Column.Crushed[v].Pos.Equals(target) && fc.Column.Crushed[v].Cube.Kind == CubeKind.Snow)
                                    {
                                        pays = false;
                                    }
                                }
                                if (pays && visuals.Points > 0)
                                {
                                    essences.Add(SpawnEssence(tc, cell));
                                }
                            }
                            // white powder off the impact, a budget per layer
                            int n = hadBlock ? 3 : 1;
                            for (int i = 0; i < n && flecks < heroBudget; i++, flecks++)
                            {
                                float s = (i % 2 == 0 ? -1f : 1f);
                                Spawn(Kind.Grain, tc - down * (cell * 0.3f) + side * (s * cell * UnityEngine.Random.Range(0.1f, 0.45f)),
                                    side * (s * cell * UnityEngine.Random.Range(0.6f, 1.5f)) - down * (cell * UnityEngine.Random.Range(0.2f, 0.6f)),
                                    cell * UnityEngine.Random.Range(0.04f, 0.075f), i % 3 == 2 ? Style.PowderBlue : Style.Powder,
                                    UnityEngine.Random.Range(0.3f, 0.5f), false);
                            }
                            if (Level != Quality.Low)
                            {
                                Spawn(Kind.Puff, tc - down * (cell * 0.15f), side * (cell * 0.1f), cell * 0.75f,
                                    new Color(0.95f, 0.97f, 1f, 0.26f), 0.34f, true);
                            }
                        }
                        if (rowCells.Count > 0 && burst != null)
                        {
                            burst(rowCells, rowLooks);
                        }
                    }
                }

                // ---- the arena takes one small knock per row, along the flow
                if (t < impulseUntil)
                {
                    float ik2 = Mathf.Clamp01(1f - (impulseUntil - t) / 0.08f);
                    board.SetImpulse(down * (Style.avalancheBoardImpulse * cell * Mathf.Sin(ik2 * Mathf.PI)));
                }
                else
                {
                    board.SetImpulse(Vector2.zero);
                }

                // ---- the settle
                if (!settled && t >= settleStart)
                {
                    settled = true;
                    if (Sfx != null)
                    {
                        Sfx.Snow(SnowCue.Settle);
                    }
                    for (int f = 0; f < fronts.Count; f++)
                    {
                        FrontColumn fc = fronts[f];
                        GridPos last = fc.Column.Covered[fc.Column.Covered.Count - 1];
                        if (Level != Quality.Low)
                        {
                            Spawn(Kind.Grain, board.CellToWorld(last) + down * (cell * 0.45f) + side * (cell * 0.4f),
                                side * (cell * 0.5f), cell * 0.04f, Style.Powder, 0.3f, false);
                            Spawn(Kind.Grain, board.CellToWorld(last) + down * (cell * 0.45f) - side * (cell * 0.4f),
                                -side * (cell * 0.5f), cell * 0.04f, Style.Powder, 0.3f, false);
                        }
                    }
                }
                StepEssences(essences, Time.deltaTime, frozen);
                yield return null;
            }
            board.SetImpulse(Vector2.zero);
            while (HoldAt == Beat.Settle)
            {
                yield return null;
            }

            // ---- the avalanche is over: the board's own gravity takes it from here, and its own
            // snow stays fresh-pressed for a breath before it reads as the 3-turn snow it is
            freshStratum = plan.Stratum;
            freshAt = Time.time;
            float breath = 0f;
            while (breath < Style.avalanchePostSettleBreath)
            {
                breath += Time.deltaTime;
                StepEssences(essences, Time.deltaTime, false);
                yield return null;
            }
            for (int i = 0; i < proxies.Count; i++)
            {
                if (proxies[i] != null)
                {
                    Destroy(proxies[i]);
                }
            }
            proxies.Clear();
            if (board != null && held.Count > 0)
            {
                board.ReleaseCells(held);
            }
            held.Clear();
            labelHidden.Clear();
            pressCells.Clear();
            pressAmount = 0f;
            labelKey = string.Empty;
            avalanche = null;
            if (landed != null)
            {
                landed();
            }
            // ---- the payout runs on its own clock, over the gravity
            if (visuals.Points > 0)
            {
                StartCoroutine(Payout(essences, centre, visuals.Points, scored));
            }
            else
            {
                ClearEssences(essences);
                if (scored != null)
                {
                    scored();
                }
            }
        }

        private static int BeatOrder(Beat beat)
        {
            return (int)beat;
        }

        /// <summary>Where a column's front is, in cells from its source: it loads in place, collapses
        /// into the first row on the release, and from then presses at each row (a hair into it),
        /// breaks through and fills it.</summary>
        private float FrontDepth(float t, int cells)
        {
            float a = Style.avalancheAnticipationDuration;
            float r = Style.avalancheFrontTravelDuration;
            float p = Style.avalancheBlockPressureDuration;
            float l = Style.avalancheLayerInterval;
            float fill = Style.avalancheSnowFillDuration;
            const float press = 0.12f; // how far into a row it leans while it pushes
            if (t <= a)
            {
                return 0f;
            }
            if (t <= a + r)
            {
                float k = (t - a) / r;
                return press * k * k;
            }
            for (int k = 1; k <= cells; k++)
            {
                float ik = ImpactTime(k);
                float breakAt = ik + p + 0.035f;
                if (t < breakAt)
                {
                    return (k - 1) + press;
                }
                float filled = breakAt + fill;
                if (t < filled)
                {
                    float e = (t - breakAt) / fill;
                    return (k - 1) + press + (1f - press) * (1f - (1f - e) * (1f - e));
                }
                if (k == cells)
                {
                    return cells;
                }
                float next = ImpactTime(k + 1);
                if (t < next)
                {
                    float e = Mathf.Clamp01((t - filled) / Mathf.Max(0.01f, next - filled));
                    return k + press * e * e;
                }
            }
            return cells;
        }

        private float FrontSpeed(float t, int cells)
        {
            float d0 = FrontDepth(t - 0.016f, cells);
            float d1 = FrontDepth(t, cells);
            return Mathf.Clamp01(Mathf.Abs(d1 - d0) / 0.016f * 0.08f);
        }

        // =================================================================== the payout

        private Particle SpawnEssence(Vector2 at, float cell)
        {
            Particle p = Spawn(Kind.Essence, at, -V(Flow) * (cell * 0.18f), cell * 0.2f, Style.EssenceGold, 99f, true);
            return p;
        }

        private void StepEssences(List<Particle> essences, float dt, bool frozen)
        {
            // they hang about the rows they came from, drifting a little against the flow
            for (int i = 0; i < essences.Count; i++)
            {
                Particle p = essences[i];
                if (p.Renderer == null)
                {
                    continue;
                }
                p.Velocity *= Mathf.Exp(-dt * 3f);
                if (!frozen)
                {
                    p.Position += p.Velocity * dt;
                }
                p.Renderer.transform.localPosition = p.Position;
                if (p.Rim != null)
                {
                    p.Rim.transform.localPosition = p.Position;
                }
            }
        }

        private void ClearEssences(List<Particle> essences)
        {
            for (int i = 0; i < essences.Count; i++)
            {
                Kill(essences[i]);
            }
            essences.Clear();
        }

        private IEnumerator Payout(List<Particle> essences, Vector2 centre, int points, Action scored)
        {
            while (HoldAt == Beat.Score)
            {
                StepEssences(essences, Time.deltaTime, true);
                yield return null;
            }
            float cell = Cell;
            // ---- GATHER: the essences go to the avalanche's middle
            var from = new List<Vector2>();
            for (int i = 0; i < essences.Count; i++)
            {
                from.Add(essences[i].Position);
            }
            float g = 0f;
            float gather = Mathf.Max(0.05f, Style.avalancheScoreGatherDuration);
            while (g < gather)
            {
                g += Time.deltaTime;
                float k = Mathf.Clamp01(g / gather);
                float e = k * k * (3f - 2f * k);
                for (int i = 0; i < essences.Count; i++)
                {
                    Particle p = essences[i];
                    if (p.Renderer == null)
                    {
                        continue;
                    }
                    Vector2 bow = new Vector2(-(centre - from[i]).y, (centre - from[i]).x) * 0.18f;
                    p.Position = Vector2.Lerp(from[i], centre, e) + bow * Mathf.Sin(e * Mathf.PI);
                    p.Renderer.transform.localPosition = p.Position;
                    if (p.Rim != null)
                    {
                        p.Rim.transform.localPosition = p.Position;
                    }
                    float s = p.Size * Mathf.Lerp(1f, 0.6f, e);
                    Vector2 b = p.Renderer.sprite.bounds.size;
                    p.Renderer.transform.localScale = new Vector3(s / b.x, s / b.y, 1f);
                }
                yield return null;
            }
            ClearEssences(essences);
            if (Sfx != null)
            {
                Sfx.Snow(SnowCue.Reward);
            }
            // ---- POP: "+TOTAL", warm gold with a snow-white rim
            TextMesh total = ViewUtil.MakeText3D(root, "AvalancheTotal", centre, "+" + points, 90,
                cell * 0.055f, Style.ScoreGold, ScoreOrder, TextAnchor.MiddleCenter);
            total.fontStyle = FontStyle.Bold;
            foreach (Transform outline in total.transform)
            {
                TextMesh rim = outline.GetComponent<TextMesh>();
                if (rim != null)
                {
                    rim.color = new Color(0.97f, 0.99f, 1f, 0.95f);
                    rim.fontStyle = FontStyle.Bold;
                }
            }
            float pop = Mathf.Max(0.05f, Style.avalancheScorePopDuration);
            float q = 0f;
            while (q < pop + 0.25f)
            {
                q += Time.deltaTime;
                float k = Mathf.Clamp01(q / pop);
                float s = k < 0.45f ? Mathf.Lerp(0.65f, 1.14f, k / 0.45f)
                    : k < 0.75f ? Mathf.Lerp(1.14f, 0.98f, (k - 0.45f) / 0.3f)
                    : Mathf.Lerp(0.98f, 1f, (k - 0.75f) / 0.25f);
                if (total != null)
                {
                    total.transform.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            // ---- TO THE TOPLAM: a short curved transfer, the TOTAL answers when it lands
            Vector2 target = ScoreAnchor != null
                ? (Vector2)root.InverseTransformPoint(ScoreAnchor())
                : centre + new Vector2(0f, cell * 4f);
            Vector2 start = centre;
            Vector2 lift = start + Vector2.up * (cell * 1.4f);
            Vector2 lift2 = target + Vector2.down * (cell * 0.6f);
            float fly = Mathf.Max(0.05f, Style.avalancheScoreFlightDuration);
            float w = 0f;
            while (w < fly)
            {
                w += Time.deltaTime;
                float k = Mathf.Clamp01(w / fly);
                float e = k * k * (3f - 2f * k);
                float u = 1f - e;
                Vector2 pos = u * u * u * start + 3f * u * u * e * lift + 3f * u * e * e * lift2 + e * e * e * target;
                if (total != null)
                {
                    total.transform.localPosition = pos;
                    float s = Mathf.Lerp(1f, 0.45f, e);
                    total.transform.localScale = new Vector3(s, s, 1f);
                }
                yield return null;
            }
            if (total != null)
            {
                Destroy(total.gameObject);
            }
            scoreLandedAt = Time.time;
            if (scored != null)
            {
                scored();
            }
        }

        private float scoreLandedAt = -10f;

        private void TickScore(float dt)
        {
            float k = (Time.time - scoreLandedAt) / 0.32f;
            if (k < 0f || k > 1f)
            {
                ScoreClaim = 0f;
                ScoreScale = 1f;
                return;
            }
            ScoreClaim = 1f - k;
            ScoreScale = 1f + 0.08f * Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI);
            ScoreInk = Style.ScoreGold;
        }

        // =================================================================== particles (pooled)

        private enum Kind
        {
            Grain,
            Puff,
            Drop,
            Ring,
            Implode,
            Glint,
            Essence
        }

        private sealed class Particle
        {
            public Kind Kind;
            public SpriteRenderer Renderer;
            public SpriteRenderer Rim;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Color Colour;
            public float Spin;
            public bool Soft;
            public bool Still;
        }

        private readonly List<Particle> particles = new List<Particle>();
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        private int Budget
        {
            get { return Level == Quality.High ? Style.MaxParticles : Level == Quality.Medium ? 28 : 14; }
        }

        private SpriteRenderer Rent(Sprite sprite, int order)
        {
            SpriteRenderer r = pool.Count > 0 ? pool.Pop() : null;
            if (r == null)
            {
                r = ViewUtil.MakeRect(root, "SnowFx", Vector2.zero, Vector2.one, Color.white, order);
            }
            r.gameObject.SetActive(true);
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r == null)
            {
                return;
            }
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        private Particle Spawn(Kind kind, Vector2 at, Vector2 velocity, float size, Color colour, float life,
            bool soft, bool still = false)
        {
            if (root == null)
            {
                return null;
            }
            if (kind != Kind.Essence && particles.Count >= Budget)
            {
                return null;
            }
            var p = new Particle
            {
                Kind = kind,
                Position = at,
                Velocity = velocity,
                Life = life,
                Size = size,
                Colour = colour,
                Soft = soft,
                Still = still,
                Spin = UnityEngine.Random.Range(-360f, 360f)
            };
            Sprite sprite = kind == Kind.Grain ? ViewUtil.WhiteSprite
                : kind == Kind.Ring || kind == Kind.Implode ? Ring
                : ViewUtil.GlowSprite;
            p.Renderer = Rent(sprite, kind == Kind.Essence ? ParticleOrder + 1 : ParticleOrder);
            if (kind == Kind.Essence)
            {
                p.Rim = Rent(ViewUtil.GlowSprite, ParticleOrder);
                Vector2 b = p.Rim.sprite.bounds.size;
                p.Rim.transform.localScale = new Vector3(size * 1.9f / b.x, size * 1.9f / b.y, 1f);
                p.Rim.color = Style.EssenceRim;
                p.Rim.transform.localPosition = at;
            }
            particles.Add(p);
            Place(p, 0f);
            return p;
        }

        private void Kill(Particle p)
        {
            if (p == null)
            {
                return;
            }
            Return(p.Renderer);
            Return(p.Rim);
            p.Renderer = null;
            p.Rim = null;
            particles.Remove(p);
        }

        private void StepParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                if (p.Kind == Kind.Essence)
                {
                    // the payout moves these itself; they only shimmer here
                    if (p.Renderer != null)
                    {
                        float tw = 0.85f + 0.15f * Mathf.Sin(Time.time * 9f + i);
                        p.Renderer.color = new Color(p.Colour.r, p.Colour.g, p.Colour.b, p.Colour.a * tw);
                    }
                    continue;
                }
                p.Age += dt;
                float k = p.Age / Mathf.Max(0.01f, p.Life);
                if (k >= 1f || p.Renderer == null)
                {
                    Kill(p);
                    continue;
                }
                if (!p.Still)
                {
                    p.Velocity *= Mathf.Exp(-dt * (p.Kind == Kind.Drop ? 1.5f : 4f));
                    p.Position += p.Velocity * dt;
                }
                Place(p, k);
            }
        }

        private void Place(Particle p, float k)
        {
            if (p.Renderer == null)
            {
                return;
            }
            Transform tr = p.Renderer.transform;
            tr.localPosition = p.Position;
            Vector2 b = p.Renderer.sprite != null ? (Vector2)p.Renderer.sprite.bounds.size : Vector2.one;
            Color c = p.Colour;
            float size = p.Size;
            switch (p.Kind)
            {
                case Kind.Grain:
                    size *= Mathf.Lerp(1f, 0.5f, k);
                    tr.localRotation = Quaternion.Euler(0f, 0f, p.Spin * p.Age);
                    c.a *= 1f - k * k;
                    break;
                case Kind.Puff:
                    size *= Mathf.Lerp(0.6f, 1.3f, 1f - (1f - k) * (1f - k));
                    c.a *= Mathf.Sin(Mathf.Clamp01(k * 1.2f) * Mathf.PI);
                    break;
                case Kind.Drop:
                    c.a *= 1f - k;
                    tr.localScale = new Vector3(size * 0.7f / b.x, size / b.y, 1f);
                    p.Renderer.color = c;
                    return;
                case Kind.Ring:
                    // closing IN on the heap: the cold pressure ring
                    size *= Mathf.Lerp(1.25f, 0.85f, k);
                    c.a *= Mathf.Sin(k * Mathf.PI);
                    break;
                case Kind.Implode:
                    size *= Mathf.Lerp(1f, 0.35f, k);
                    c.a *= 1f - k;
                    break;
                case Kind.Glint:
                    size *= Mathf.Sin(k * Mathf.PI);
                    c.a *= Mathf.Sin(k * Mathf.PI);
                    tr.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    break;
            }
            tr.localScale = new Vector3(size / Mathf.Max(0.0001f, b.x), size / Mathf.Max(0.0001f, b.y), 1f);
            p.Renderer.color = c;
        }

        /// <summary>A soft ring, made once.</summary>
        private static Sprite Ring
        {
            get
            {
                if (ringSprite == null)
                {
                    const int n = 64;
                    var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.hideFlags = HideFlags.HideAndDontSave;
                    var px = new Color32[n * n];
                    for (int y = 0; y < n; y++)
                    {
                        for (int x = 0; x < n; x++)
                        {
                            float dx = (x + 0.5f) / n - 0.5f;
                            float dy = (y + 0.5f) / n - 0.5f;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            float a = Mathf.Exp(-Mathf.Pow((r - 0.4f) / 0.045f, 2f));
                            px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.Clamp(a * 255f, 0f, 255f));
                        }
                    }
                    tex.SetPixels32(px);
                    tex.Apply();
                    ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                }
                return ringSprite;
            }
        }

        // =================================================================== debug

        private string debugKey = string.Empty;

        private void DrawDebug()
        {
            bool any = Show.SnowMeltTimer || Show.SnowStrataId || Show.SnowMergeBarrier || Show.SnowMergeTarget
                || Show.SnowGravityPath || Show.AvalancheTiming || Show.ParticleBudget || Show.AvalancheLayerIndex
                || Show.AvalancheEligibleRows || Show.AvalancheSpentRows || Show.AvalanchePreviewCells
                || Show.AvalancheDestructionCells || Show.ScoreEssence || Show.AvalancheFront;
            if (!any)
            {
                if (debugRoot.childCount > 0)
                {
                    ClearChildren(debugRoot);
                    debugKey = string.Empty;
                }
                return;
            }
            var key = new System.Text.StringBuilder();
            GameBoard cells = Cells;
            for (int x = cells.MinX; x < cells.MinX + cells.Width; x++)
            {
                for (int y = cells.MinY; y < cells.MinY + cells.Height; y++)
                {
                    Cube? c = cells.GetCube(new GridPos(x, y));
                    if (c.HasValue && c.Value.Kind == CubeKind.Snow)
                    {
                        key.Append(x).Append(y).Append(c.Value.SnowMelt).Append(c.Value.SnowStratum).Append(c.Value.SnowPower);
                    }
                }
            }
            key.Append(timing).Append(particles.Count).Append(layerShown).Append(aimKey);
            string k = key.ToString();
            if (k == debugKey)
            {
                return;
            }
            debugKey = k;
            ClearChildren(debugRoot);
            float cell = Cell;
            var heapsDone = new HashSet<GridPos>();
            for (int x = cells.MinX; x < cells.MinX + cells.Width; x++)
            {
                for (int y = cells.MinY; y < cells.MinY + cells.Height; y++)
                {
                    var at = new GridPos(x, y);
                    Cube? c = cells.GetCube(at);
                    if (!c.HasValue || c.Value.Kind != CubeKind.Snow)
                    {
                        continue;
                    }
                    Vector2 w = board.CellToWorld(at);
                    if (Show.SnowMeltTimer)
                    {
                        DebugText(w + new Vector2(-cell * 0.25f, -cell * 0.28f), "m" + c.Value.SnowMelt, new Color(0.3f, 0.5f, 0.9f));
                    }
                    if (Show.SnowStrataId && c.Value.SnowStratum != 0)
                    {
                        DebugText(w + new Vector2(-cell * 0.25f, cell * 0.28f), "s" + c.Value.SnowStratum, new Color(0.7f, 0.4f, 0.9f));
                    }
                    if (Show.SnowMergeBarrier && cells.SnowSeamBelow(at))
                    {
                        Vector2 d = V(Flow);
                        ViewUtil.MakeRect(debugRoot, "Barrier", w + d * (cell * 0.48f),
                            d.x == 0 ? new Vector2(cell * 0.9f, cell * 0.05f) : new Vector2(cell * 0.05f, cell * 0.9f),
                            new Color(1f, 0.3f, 0.3f, 0.9f), LabelOrder + 1);
                    }
                    if (Show.SnowGravityPath)
                    {
                        Vector2 d = V(Flow);
                        ViewUtil.MakeRect(debugRoot, "Flow", w + d * (cell * 0.2f),
                            d.x == 0 ? new Vector2(cell * 0.05f, cell * 0.4f) : new Vector2(cell * 0.4f, cell * 0.05f),
                            new Color(0.2f, 0.9f, 0.4f, 0.8f), LabelOrder + 1);
                    }
                    if (Show.SnowMergeTarget && PendingFor(at) != null)
                    {
                        ViewUtil.MakeRect(debugRoot, "MergeTarget", w, new Vector2(cell * 0.3f, cell * 0.3f),
                            new Color(1f, 0.85f, 0.2f, 0.7f), LabelOrder + 1);
                    }
                }
            }
            if (Show.AvalancheEligibleRows || Show.AvalancheSpentRows)
            {
                bool vertical = Flow.X == 0;
                int lines = vertical ? cells.Height : cells.Width;
                for (int i = 0; i < lines; i++)
                {
                    GridPos probe = vertical ? new GridPos(cells.MinX, cells.MinY + i) : new GridPos(cells.MinX + i, cells.MinY);
                    AvalanchePlan plan = cells.PlanAvalanche(probe);
                    Vector2 w = board.CellToWorld(probe) - (vertical ? new Vector2(cell * 0.8f, 0f) : new Vector2(0f, cell * 0.8f));
                    if (Show.AvalancheEligibleRows && plan.Any)
                    {
                        DebugText(w, "OK", new Color(0.3f, 0.9f, 0.5f));
                    }
                    else if (Show.AvalancheSpentRows && plan.Blocked == AvalancheBlock.Spent)
                    {
                        DebugText(w, "spent", new Color(0.7f, 0.7f, 0.8f));
                    }
                }
            }
            if (aimPlan != null && (Show.AvalanchePreviewCells || Show.AvalancheDestructionCells))
            {
                for (int c = 0; c < aimPlan.Columns.Count; c++)
                {
                    if (Show.AvalanchePreviewCells)
                    {
                        foreach (GridPos covered in aimPlan.Columns[c].Covered)
                        {
                            ViewUtil.MakeRect(debugRoot, "Preview", board.CellToWorld(covered),
                                new Vector2(cell * 0.18f, cell * 0.18f), new Color(0.3f, 0.7f, 1f, 0.9f), LabelOrder + 1);
                        }
                    }
                    if (Show.AvalancheDestructionCells)
                    {
                        foreach (DestroyedCube crushed in aimPlan.Columns[c].Crushed)
                        {
                            ViewUtil.MakeRect(debugRoot, "Destroy", board.CellToWorld(crushed.Pos) + new Vector2(cell * 0.2f, 0f),
                                new Vector2(cell * 0.18f, cell * 0.18f), new Color(1f, 0.35f, 0.25f, 0.9f), LabelOrder + 1);
                        }
                    }
                }
            }
            Vector2 corner = board.CellToWorld(new GridPos(cells.MinX, cells.MinY + cells.Height - 1))
                + new Vector2(-cell * 0.4f, cell * 0.85f);
            var line = new System.Text.StringBuilder();
            if (Show.AvalancheTiming && timing.Length > 0)
            {
                line.Append(timing).Append("   ");
            }
            if (Show.AvalancheLayerIndex && Playing)
            {
                line.Append("layer ").Append(layerShown).Append("   ");
            }
            if (Show.ParticleBudget)
            {
                line.Append("particles ").Append(particles.Count).Append('/').Append(Budget).Append("   ");
            }
            if (Show.ScoreEssence)
            {
                int ess = 0;
                foreach (Particle p in particles)
                {
                    if (p.Kind == Kind.Essence) ess++;
                }
                line.Append("essences ").Append(ess);
            }
            if (line.Length > 0)
            {
                DebugText(corner, line.ToString(), new Color(1f, 0.95f, 0.6f), TextAnchor.MiddleLeft);
            }
        }

        private void DebugText(Vector2 at, string text, Color colour, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            ViewUtil.MakeText3D(debugRoot, "SnowDebug", at, text, 90, Cell * 0.016f, colour, LabelOrder + 2, anchor);
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Destroy(t.GetChild(i).gameObject);
            }
        }

        private void OnDisable()
        {
            avalanche = null;
            CleanupAvalanche();
        }

        private static readonly int AgeId = Shader.PropertyToID("_Age");
        private static readonly int MeltId = Shader.PropertyToID("_Melt");
        private static readonly int PackedId = Shader.PropertyToID("_Packed");
        private static readonly int PressId = Shader.PropertyToID("_Press");
        private static readonly int SeamId = Shader.PropertyToID("_Seam");
        private static readonly int SweepId = Shader.PropertyToID("_Sweep");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int MuteId = Shader.PropertyToID("_Mute");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
    }
}
