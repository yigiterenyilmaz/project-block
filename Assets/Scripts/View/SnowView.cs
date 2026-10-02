// PURPOSE: SNOW on the board - what the cube alone cannot say, and the two things that happen to
// it. Three jobs:
//
//   THE NUMBERS. A heap's POWER and its MELT TIME are the whole of the mechanic and neither is in
//   the tile, so every heap carries them: the power as one bold number in the middle of the heap
//   (a heap is one thing, however many cubes wide - it gets ONE number, not one per cube), and
//   the turns it has left as a small chip on its corner that warms when the next turn melts it.
//   A packed layer - snow an avalanche laid - wears two faint compression lines, because it
//   behaves differently (it never merges with the layer under it) and looked the same.
//
//   THE AVALANCHE ("Çığ"). Core has FINISHED by the time this plays: the heaps have left their
//   line, the cubes under them are gone and what was left hanging has already fallen. So the
//   board is held blank where anything happened and the event is replayed over it from the
//   report - the heap shudders, then comes down its column a cell at a time; each cube in the
//   way breaks AS THE SNOW REACHES IT (the caller bursts it, from the face taken before the
//   rules ran) and a packed layer is left standing in every cell the front passes. Only then
//   are the cells released and the fall that followed handed to the board's own water animation.
//
//   THE MELT. Snow whose time ran out sinks into its own cell and is gone - never a burst: it
//   paid nothing and nothing broke.
//
// THE VIEW DECIDES NOTHING. Which heaps slid, how far each column got, what was crushed and what
// it paid are AvalancheVisuals; what melted is TurnReport.SnowMelted; a heap is GameBoard.SnowHeapAt.
//
// IT LIVES OUTSIDE THE BOARD'S TRANSFORM and copies it every frame (BoardView.Rebuild destroys
// the board's children, and an avalanche cut off mid-slide would leave its cells held).

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The snow layer over the board. Owned by the controller.</summary>
    public sealed class SnowView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            /// <summary>The heap's power: text height and chip size, in cells.</summary>
            public static float PowerChip = 0.46f;

            public static Color PowerChipColour = new Color(0.10f, 0.19f, 0.32f, 0.88f);

            public static Color PowerText = new Color(1f, 1f, 1f, 1f);

            /// <summary>The melt chip on the heap's corner.</summary>
            public static float MeltChip = 0.27f;

            public static Color MeltChipColour = new Color(0.29f, 0.42f, 0.56f, 0.92f);

            public static Color MeltLastTurn = new Color(0.80f, 0.27f, 0.20f, 0.96f);

            public static Color PackedLine = new Color(0.36f, 0.52f, 0.72f, 0.30f);

            /// <summary>The avalanche: the shudder before it lets go, how long the front takes to
            /// cross one cell, the stagger between a heap's columns, and the breath at the end.</summary>
            public static float Shudder = 0.16f;

            public static float CellSeconds = 0.075f;

            public static float ColumnStagger = 0.028f;

            public static float Settle = 0.12f;

            /// <summary>How long a laid layer takes to seat (1.12 -> 1), and the front's stretch
            /// along its travel.</summary>
            public static float SeatSeconds = 0.14f;

            public static float FrontStretch = 1.16f;

            public static float MeltSeconds = 0.42f;

            public static Color Powder = new Color(0.93f, 0.97f, 1f, 0.55f);

            public static Color MeltWater = new Color(0.62f, 0.80f, 0.96f, 0.75f);
        }

        private const int LineOrder = 3;
        private const int ChipOrder = 4;
        private const int TextOrder = 5;
        private const int ProxyOrder = 3;
        private const int FrontOrder = 4;
        private const int PowderOrder = 6;

        /// <summary>The cues, set by the controller.</summary>
        public SoundFx Sfx;

        /// <summary>True while an avalanche is on screen.</summary>
        public bool Playing
        {
            get { return avalanche != null; }
        }

        private BoardView board;
        private Transform root;
        private Transform labels;
        private Coroutine avalanche;
        private readonly List<GridPos> held = new List<GridPos>();
        private readonly List<GameObject> proxies = new List<GameObject>();

        /// <summary>The numbers are hidden while this says so (cubes are still in the air).</summary>
        public Func<bool> Busy;

        private void EnsureRoot(BoardView view)
        {
            board = view;
            if (root == null)
            {
                var go = new GameObject("SnowLayer");
                go.transform.SetParent(transform, false);
                root = go.transform;
                var labelGo = new GameObject("SnowLabels");
                labelGo.transform.SetParent(root, false);
                labels = labelGo.transform;
            }
            FollowBoard();
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

        private void LateUpdate()
        {
            FollowBoard();
            if (labels != null)
            {
                bool hide = Playing || (Busy != null && Busy());
                if (labels.gameObject.activeSelf == hide)
                {
                    labels.gameObject.SetActive(!hide);
                }
            }
        }

        // =================================================================== the numbers

        /// <summary>Rebuilds the heap numbers from the board as it stands. Called on every
        /// repaint; a board without snow costs one scan.</summary>
        public void Sync(BoardView view)
        {
            GameBoard cells = view != null ? view.Board : null;
            if (cells == null)
            {
                return;
            }
            if (!cells.HasSnow && (labels == null || labels.childCount == 0))
            {
                return;
            }
            EnsureRoot(view);
            for (int i = labels.childCount - 1; i >= 0; i--)
            {
                Destroy(labels.GetChild(i).gameObject);
            }
            if (view.IsDark)
            {
                return; // a blind round tells the player nothing about a cell it has not lit
            }
            float cell = view.CellWorldSize;
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
                    Vector2 centre = Vector2.zero;
                    Vector2 corner = view.CellToWorld(heap[heap.Count - 1]);
                    for (int i = 0; i < heap.Count; i++)
                    {
                        done.Add(heap[i]);
                        Vector2 at2 = view.CellToWorld(heap[i]);
                        centre += at2;
                        if (cube.Value.SnowPacked)
                        {
                            ViewUtil.MakeRect(labels, "Packed", at2 + new Vector2(0f, cell * 0.16f),
                                new Vector2(cell * 0.62f, cell * 0.035f), Style.PackedLine, LineOrder);
                            ViewUtil.MakeRect(labels, "Packed", at2 - new Vector2(0f, cell * 0.16f),
                                new Vector2(cell * 0.62f, cell * 0.035f), Style.PackedLine, LineOrder);
                        }
                    }
                    centre /= heap.Count;
                    if (cube.Value.SnowPower >= 2)
                    {
                        MakeChip(centre, cell * Style.PowerChip, Style.PowerChipColour,
                            cube.Value.SnowPower.ToString(), Style.PowerText);
                    }
                    MakeChip(corner + new Vector2(cell * 0.30f, -cell * 0.30f), cell * Style.MeltChip,
                        cube.Value.SnowMelt <= 1 ? Style.MeltLastTurn : Style.MeltChipColour,
                        cube.Value.SnowMelt.ToString(), Color.white);
                }
            }
        }

        private void MakeChip(Vector2 at, float size, Color chip, string text, Color ink)
        {
            ViewUtil.MakeRounded(labels, "Chip", at, new Vector2(size, size), chip, ChipOrder);
            // TextMesh wants a big font and a small character size to stay sharp.
            // (height is about a tenth of fontSize x characterSize: 0.6 of the chip here)
            ViewUtil.MakeText3D(labels, "Number", at, text, 90, size * 0.066f, ink,
                TextOrder, TextAnchor.MiddleCenter);
        }

        // =================================================================== the melt

        /// <summary>Snow whose time ran out, sinking into its own cell. The cells are already
        /// empty on the board.</summary>
        public void PlayMelt(BoardView view, IReadOnlyList<DestroyedCube> melted)
        {
            if (view == null || view.Board == null || melted == null || melted.Count == 0 || view.IsDark)
            {
                return;
            }
            EnsureRoot(view);
            GridPos flow = view.Board.WaterFlow;
            var down = new Vector2(flow.X, flow.Y);
            for (int i = 0; i < melted.Count; i++)
            {
                StartCoroutine(MeltOne(view.CellToWorld(melted[i].Pos), view.CellWorldSize, down, i));
            }
        }

        private IEnumerator MeltOne(Vector2 at, float cell, Vector2 down, int index)
        {
            float size = cell * BoardView.CubeCellShare;
            SpriteRenderer snow = ViewUtil.MakeCell(root, "MeltingSnow", at, size, Color.white, ProxyOrder);
            ViewUtil.ApplyTile(snow, ViewUtil.CubeTile(CubeKind.Snow), size);
            snow.color = Color.white;
            SpriteRenderer pool = ViewUtil.MakeRect(root, "MeltWater", at + down * (size * 0.42f),
                new Vector2(0f, 0f), Style.MeltWater, ProxyOrder);
            pool.sprite = ViewUtil.GlowSprite;
            Vector2 glow = pool.sprite.bounds.size;
            bool vertical = Mathf.Abs(down.y) > 0.5f;
            float wait = 0.03f * index;
            float t = -wait;
            while (t < Style.MeltSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.MeltSeconds);
                float e = k * k * (3f - 2f * k);
                // It sinks TOWARD the ground it stood on: thinner along the flow, a little wider
                // across it, and its foot stays where it was.
                float thin = Mathf.Lerp(1f, 0.10f, e);
                float wide = Mathf.Lerp(1f, 1.10f, e);
                snow.transform.localScale = vertical
                    ? new Vector3(size * wide, size * thin, 1f)
                    : new Vector3(size * thin, size * wide, 1f);
                snow.transform.localPosition = at + down * (size * 0.5f * (1f - thin));
                snow.color = new Color(Mathf.Lerp(1f, 0.74f, e), Mathf.Lerp(1f, 0.88f, e), 1f,
                    1f - Mathf.Clamp01((k - 0.55f) / 0.45f));
                float poolSize = size * Mathf.Lerp(0.3f, 1.0f, e);
                pool.transform.localScale = vertical
                    ? new Vector3(poolSize / glow.x, poolSize * 0.32f / glow.y, 1f)
                    : new Vector3(poolSize * 0.32f / glow.x, poolSize / glow.y, 1f);
                Color pc = Style.MeltWater;
                pc.a *= Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                pool.color = pc;
                yield return null;
            }
            Destroy(snow.gameObject);
            Destroy(pool.gameObject);
        }

        // =================================================================== the avalanche

        /// <summary>One cube that was standing somewhere else when the avalanche began and fell
        /// afterwards: where it stood and what it looks like. Drawn still while the snow comes
        /// down, because the board underneath already shows it where it landed.</summary>
        public struct Bystander
        {
            public Vector2 At;
            public Sprite Tile;
            public Color Colour;
        }

        /// <summary>
        /// Plays an avalanche over a board that has ALREADY settled. <paramref name="hold"/> is
        /// every cell anything happened in (kept blank until the end); <paramref name="crush"/>
        /// is called with the index of a column and of the covered cell in it at the moment the
        /// snow reaches a cube there; <paramref name="onDone"/> when the cells have been released
        /// - which is where the caller starts the fall that followed.
        /// </summary>
        public void PlayAvalanche(BoardView view, AvalancheVisuals visuals, IList<GridPos> hold,
            IList<Bystander> bystanders, Action<int, int> crush, Action onDone)
        {
            Stop();
            if (view == null || view.Board == null || visuals == null || visuals.Plan == null)
            {
                if (onDone != null)
                {
                    onDone();
                }
                return;
            }
            EnsureRoot(view);
            avalanche = StartCoroutine(Avalanche(view, visuals, hold, bystanders, crush, onDone));
        }

        /// <summary>Ends an avalanche in flight and gives the board its cells back.</summary>
        public void Stop()
        {
            if (avalanche != null)
            {
                StopCoroutine(avalanche);
                avalanche = null;
            }
            Cleanup();
        }

        private void Cleanup()
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
        }

        private SpriteRenderer SnowProxy(Vector2 at, float size, int order)
        {
            SpriteRenderer snow = ViewUtil.MakeCell(root, "AvalancheSnow", at, size, Color.white, order);
            ViewUtil.ApplyTile(snow, ViewUtil.CubeTile(CubeKind.Snow), size);
            snow.color = Color.white;
            proxies.Add(snow.gameObject);
            return snow;
        }

        private sealed class Slide
        {
            public AvalancheColumn Column;
            public SpriteRenderer Front;
            public Vector2 From;
            public readonly List<Vector2> Cells = new List<Vector2>();
            public readonly List<SpriteRenderer> Laid = new List<SpriteRenderer>();
            public readonly List<float> LaidAt = new List<float>();
            public float Delay;
            public int Reached;
        }

        private sealed class Puff
        {
            public SpriteRenderer Renderer;
            public Vector2 At;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
        }

        private IEnumerator Avalanche(BoardView view, AvalancheVisuals visuals, IList<GridPos> hold,
            IList<Bystander> bystanders, Action<int, int> crush, Action onDone)
        {
            AvalanchePlan plan = visuals.Plan;
            float cell = view.CellWorldSize;
            float size = cell * BoardView.CubeCellShare;
            var down = new Vector2(plan.Flow.X, plan.Flow.Y);
            bool vertical = Mathf.Abs(down.y) > 0.5f;

            held.Clear();
            if (hold != null)
            {
                held.AddRange(hold);
            }
            view.HoldCells(held);
            if (bystanders != null)
            {
                for (int i = 0; i < bystanders.Count; i++)
                {
                    SpriteRenderer still = ViewUtil.MakeCell(root, "Bystander", bystanders[i].At, size,
                        bystanders[i].Colour, ProxyOrder);
                    ViewUtil.ApplyTile(still, bystanders[i].Tile, size);
                    still.color = bystanders[i].Colour;
                    proxies.Add(still.gameObject);
                }
            }

            var slides = new List<Slide>();
            float longest = 0f;
            for (int c = 0; c < plan.Columns.Count; c++)
            {
                AvalancheColumn column = plan.Columns[c];
                var slide = new Slide { Column = column, From = view.CellToWorld(column.Source) };
                for (int i = 0; i < column.Covered.Count; i++)
                {
                    slide.Cells.Add(view.CellToWorld(column.Covered[i]));
                    slide.Laid.Add(null);
                    slide.LaidAt.Add(0f);
                }
                slide.Front = SnowProxy(slide.From, size, FrontOrder);
                slide.Delay = Style.Shudder + Style.ColumnStagger * c;
                longest = Mathf.Max(longest, slide.Delay + Style.CellSeconds * slide.Cells.Count);
                slides.Add(slide);
            }
            if (Sfx != null)
            {
                Sfx.Rumble();
            }
            var puffs = new List<Puff>();
            float end = longest + Style.Settle;
            float t = 0f;
            while (t < end)
            {
                if (view == null || view.Board == null)
                {
                    break;
                }
                float dt = Time.deltaTime;
                t += dt;
                for (int s = 0; s < slides.Count; s++)
                {
                    Slide slide = slides[s];
                    if (t < slide.Delay)
                    {
                        // THE SHUDDER: it loads against the way it is about to go, never a jitter
                        // in every direction - a mass leaning, then letting go.
                        float k = Mathf.Clamp01(t / Mathf.Max(0.01f, slide.Delay));
                        float lean = Mathf.Sin(k * Mathf.PI) * cell * 0.05f;
                        float buzz = Mathf.Sin(t * 70f + s * 1.9f) * cell * 0.012f * k;
                        Vector2 across = new Vector2(down.y, down.x);
                        slide.Front.transform.localPosition = slide.From - down * lean + across * buzz;
                        continue;
                    }
                    // THE FRONT: accelerating out of rest, then steady - snow gathers speed.
                    float along = (t - slide.Delay) / Style.CellSeconds;
                    float eased = along < 1f ? along * along : along;
                    float reach = Mathf.Min(eased, slide.Cells.Count);
                    int whole = Mathf.Min(slide.Cells.Count, Mathf.FloorToInt(reach + 0.5f));
                    Vector2 at = slide.From + down * (cell * reach);
                    float stretch = reach < slide.Cells.Count ? Style.FrontStretch : 1f;
                    slide.Front.transform.localPosition = at;
                    slide.Front.transform.localScale = vertical
                        ? new Vector3(size, size * stretch, 1f)
                        : new Vector3(size * stretch, size, 1f);
                    while (slide.Reached < whole)
                    {
                        int index = slide.Reached++;
                        if (crush != null && HasCrushed(slide.Column, slide.Column.Covered[index]))
                        {
                            crush(s, index);
                        }
                        // The last cell is the front itself coming to rest; every other one keeps
                        // a layer of its own.
                        if (index < slide.Cells.Count - 1)
                        {
                            slide.Laid[index] = SnowProxy(slide.Cells[index], size, ProxyOrder);
                            slide.LaidAt[index] = t;
                        }
                        SpawnPowder(puffs, slide.Cells[index], down, cell, index + s * 7);
                    }
                    for (int i = 0; i < slide.Laid.Count; i++)
                    {
                        if (slide.Laid[i] == null)
                        {
                            continue;
                        }
                        float sk = Mathf.Clamp01((t - slide.LaidAt[i]) / Style.SeatSeconds);
                        float seat = Mathf.Lerp(1.12f, 1f, 1f - (1f - sk) * (1f - sk));
                        slide.Laid[i].transform.localScale = new Vector3(size * seat, size * seat, 1f);
                    }
                }
                StepPowder(puffs, dt);
                yield return null;
            }
            for (int i = 0; i < puffs.Count; i++)
            {
                if (puffs[i].Renderer != null)
                {
                    Destroy(puffs[i].Renderer.gameObject);
                }
            }
            avalanche = null;
            Cleanup();
            if (onDone != null)
            {
                onDone();
            }
        }

        private static bool HasCrushed(AvalancheColumn column, GridPos cell)
        {
            for (int i = 0; i < column.Crushed.Count; i++)
            {
                if (column.Crushed[i].Pos.Equals(cell))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The powder a layer throws as it lands: thrown sideways and a little back up the
        /// slope, soft, gone in a third of a second. Deterministic per cell.</summary>
        private void SpawnPowder(List<Puff> puffs, Vector2 at, Vector2 down, float cell, int seed)
        {
            var across = new Vector2(down.y, down.x);
            for (int i = 0; i < 2; i++)
            {
                uint h = (uint)(seed * 374761393 + i * 668265263 + 97);
                h = (h ^ (h >> 13)) * 1274126177u;
                float a = ((h >> 8) & 1023u) / 1023f;
                var puff = new Puff();
                puff.At = at + across * ((i == 0 ? -0.42f : 0.42f) * cell);
                puff.Velocity = across * ((i == 0 ? -1f : 1f) * cell * Mathf.Lerp(0.9f, 1.7f, a))
                    - down * (cell * Mathf.Lerp(0.3f, 0.9f, a));
                puff.Life = Mathf.Lerp(0.26f, 0.38f, a);
                puff.Size = cell * Mathf.Lerp(0.34f, 0.5f, a);
                puff.Renderer = ViewUtil.MakeRect(root, "Powder", puff.At, new Vector2(0f, 0f),
                    new Color(0f, 0f, 0f, 0f), PowderOrder);
                puff.Renderer.sprite = ViewUtil.GlowSprite;
                puffs.Add(puff);
            }
        }

        private static void StepPowder(List<Puff> puffs, float dt)
        {
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff p = puffs[i];
                p.Age += dt;
                float k = Mathf.Clamp01(p.Age / p.Life);
                if (p.Renderer == null || k >= 1f)
                {
                    if (p.Renderer != null)
                    {
                        Destroy(p.Renderer.gameObject);
                    }
                    puffs.RemoveAt(i);
                    continue;
                }
                p.Velocity *= Mathf.Exp(-dt * 5f);
                p.At += p.Velocity * dt;
                Vector2 glow = p.Renderer.sprite.bounds.size;
                float size = p.Size * Mathf.Lerp(0.5f, 1.25f, 1f - (1f - k) * (1f - k));
                p.Renderer.transform.localPosition = p.At;
                p.Renderer.transform.localScale = new Vector3(size / glow.x, size / glow.y, 1f);
                Color c = Style.Powder;
                c.a *= Mathf.Sin(k * Mathf.PI);
                p.Renderer.color = c;
            }
        }

        private void OnDisable()
        {
            avalanche = null;
            Cleanup();
        }
    }
}
