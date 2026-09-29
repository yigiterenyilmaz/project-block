// PURPOSE: "Yangın" / "Taşkın" BUILDING UP (2026-09-29, designer's call). The spread now goes off
// by itself every second turn, so the turn before it has to SAY so: every source cube swells with
// its own element - fire glows up hot and throws a few rising sparks, water wells and sheds a
// few droplets - and the beat quickens, so the player can see the spread coming and plan round
// it. It is the ANTICIPATION only; the spread itself is FireSpreadView / FloodView.
//
// THE VIEW DECIDES NOTHING: which cells are sources and when it goes off are the joker's
// (SpreadJoker.TurnsUntilSpread and the board's cubes of its kind).

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class SpreadBuildupView : MonoBehaviour
    {
        private static readonly Color FireGlow = new Color(1f, 0.55f, 0.18f);
        private static readonly Color WaterGlow = new Color(0.35f, 0.7f, 1f);

        private sealed class Mark
        {
            public SpriteRenderer Glow;
            public Vector2 At;
            public float Phase;
            public bool Fire;
        }

        private sealed class Mote
        {
            public SpriteRenderer R;
            public Vector2 At;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public bool Fire;
        }

        private readonly List<Mark> marks = new List<Mark>();
        private readonly List<Mote> motes = new List<Mote>();
        private BoardView board;
        private float clock;
        private uint seed = 0x2545F491u;

        public void Build(BoardView view)
        {
            board = view;
        }

        /// <summary>Restates the build-up: the cells about to spread, and which element.</summary>
        public void Show(IReadOnlyList<GridPos> fireCells, IReadOnlyList<GridPos> waterCells)
        {
            Clear();
            if (board == null)
            {
                return;
            }
            Add(fireCells, true);
            Add(waterCells, false);
        }

        private void Add(IReadOnlyList<GridPos> cells, bool fire)
        {
            if (cells == null)
            {
                return;
            }
            foreach (GridPos cell in cells)
            {
                var r = Make(ViewUtil.GlowSprite, 9);
                Vector2 at = board.CellToWorld(cell);
                marks.Add(new Mark { Glow = r, At = at, Fire = fire, Phase = (cell.X * 0.73f + cell.Y * 1.31f) });
            }
        }

        private void Update()
        {
            if (board == null || marks.Count == 0 && motes.Count == 0)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            float cell = board.CellWorldSize;
            foreach (Mark m in marks)
            {
                // A quick, eager beat: this is the turn before it goes.
                float k = 0.5f + 0.5f * Mathf.Sin(clock * 7.5f + m.Phase);
                Color c = m.Fire ? FireGlow : WaterGlow;
                c.a = 0.28f + 0.3f * k;
                m.Glow.color = c;
                m.Glow.transform.localPosition = m.At;
                Vector2 unit = m.Glow.sprite.bounds.size;
                float s = cell * (1.05f + 0.12f * k);
                m.Glow.transform.localScale = new Vector3(s / unit.x, s / unit.y, 1f);
                // Now and then the element spills a little: a spark up, a droplet down.
                if (Rand() < dt * 2.2f)
                {
                    SpawnMote(m, cell);
                }
            }
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                Mote m = motes[i];
                m.Age += dt;
                float t = m.Age / m.Life;
                if (t >= 1f)
                {
                    Destroy(m.R.gameObject);
                    motes.RemoveAt(i);
                    continue;
                }
                m.Velocity += (m.Fire ? Vector2.up * 0.4f : Vector2.down * 2.2f) * cell * dt;
                m.At += m.Velocity * dt;
                m.R.transform.localPosition = m.At;
                Color c = m.Fire ? Color.Lerp(new Color(1f, 0.85f, 0.45f), FireGlow, t) : WaterGlow;
                c.a = (1f - t) * 0.9f;
                m.R.color = c;
            }
        }

        private void SpawnMote(Mark mark, float cell)
        {
            var r = Make(ViewUtil.GlowSprite, 10);
            Vector2 unit = r.sprite.bounds.size;
            float s = cell * (mark.Fire ? 0.1f : 0.12f);
            r.transform.localScale = new Vector3(s / unit.x, s / unit.y, 1f);
            Vector2 at = mark.At + new Vector2(Rand() - 0.5f, Rand() - 0.5f) * cell * 0.6f;
            Vector2 v = mark.Fire
                ? new Vector2((Rand() - 0.5f) * 0.4f, 0.8f + Rand() * 0.6f) * cell
                : new Vector2((Rand() - 0.5f) * 0.3f, -0.2f) * cell;
            motes.Add(new Mote { R = r, At = at, Velocity = v, Life = 0.55f + Rand() * 0.35f, Fire = mark.Fire });
        }

        private SpriteRenderer Make(Sprite sprite, int order)
        {
            var go = new GameObject("SpreadBuildup");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = new Color(1f, 1f, 1f, 0f);
            return r;
        }

        private float Rand()
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFF) / (float)0x1000000;
        }

        public void Clear()
        {
            foreach (Mark m in marks)
            {
                if (m.Glow != null) Destroy(m.Glow.gameObject);
            }
            marks.Clear();
        }
    }
}
