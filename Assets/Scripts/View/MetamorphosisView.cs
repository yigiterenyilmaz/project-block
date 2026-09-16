// PURPOSE: "Metamorfoz" - a plain cube slowly becoming GOLD, drawn while it happens.
//
// THE CLOCK WAS INVISIBLE AND THE CHANGE WAS PERMANENT, which is the worst pair of properties a
// mechanic can have: a cube that had stood six turns looked exactly like one that landed this
// turn, and on the seventh it silently became gold - a cube that never breaks and blocks a clean
// sweep. The player was asked to plan around something they could not see and then punished by
// it. Everything here exists to make the clock legible BEFORE it runs out.
//
// TWO THINGS, and they are different events:
//
//   THE RIPENING   a standing mark on every cube on the clock, growing warmer and tighter as it
//                  nears the change. It is a STATE and it is restated every turn from the
//                  report, so a cube that exploded simply stops being listed.
//   THE TURN       the moment a cube becomes gold: a bloom that settles into the new material.
//                  It is an EVENT and it fires once.
//
// IT IS A CREEPING GILDING, NOT A BADGE. The mark grows INWARD from the cube's own rim rather
// than sitting in the middle of it: gold is taking the cube over, so it starts at the edges and
// closes. A pip in the centre would be a counter, which is the thing the hydraulic press's four
// orange dots already taught us not to draw.
//
// THE VIEW DECIDES NOTHING: MetamorphosisVisuals carries which cells and how far along.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Draws Metamorfoz's ripening cubes and their moment of change.</summary>
    public sealed class MetamorphosisView : MonoBehaviour
    {
        /// <summary>The gilding's colour early and late. It starts as a barely-there warm tarnish
        /// and ends near the gold the cube is about to become, so the last turn before the change
        /// is unmistakably the last turn.</summary>
        private static readonly Color EarlyTone = new Color(0.62f, 0.52f, 0.30f);

        private static readonly Color LateTone = new Color(1f, 0.82f, 0.32f);

        /// <summary>Seconds for one breath, slow when fresh and quick when nearly gold.</summary>
        private const float SlowPulse = 2.4f;

        private const float FastPulse = 0.7f;

        /// <summary>The change itself.</summary>
        private const float BloomSeconds = 0.55f;

        /// <summary>Over the cube, under the destruction effects - a line clear taking a ripening
        /// cube has to read over the top of its mark.</summary>
        private const int MarkOrder = 8;

        private const int BloomOrder = 9;

        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();
        private readonly List<float> markProgress = new List<float>();
        private readonly List<Vector3> markBase = new List<Vector3>();

        private readonly List<SpriteRenderer> blooms = new List<SpriteRenderer>();
        private readonly List<float> bloomTime = new List<float>();
        private readonly List<Vector3> bloomBase = new List<Vector3>();

        private BoardView board;

        public void Build(BoardView boardView)
        {
            board = boardView;
        }

        /// <summary>Restates the ripening marks and blooms whatever turned this turn. Null clears
        /// everything, which a new round and a board with nothing on the clock both do.</summary>
        public void Show(MetamorphosisVisuals report)
        {
            ClearMarks();
            if (report == null || board == null)
            {
                return;
            }
            float cell = board.CellWorldSize;
            for (int i = 0; i < report.Count; i++)
            {
                Vector2 at = board.CellToWorld(report.Ripening[i]);
                float k = Mathf.Clamp01(report.Progress[i]);
                // The GILDING: a plate the size of the cube, whose STRENGTH and beat carry the
                // progress. One renderer per cube - a ring drawn as two plates would double that
                // for a mark this small, and the alpha ramp says the same thing.
                SpriteRenderer mark = ViewUtil.MakeRounded(transform, "Ripen_" + i, at,
                    new Vector2(cell, cell), Color.Lerp(EarlyTone, LateTone, k), MarkOrder);
                marks.Add(mark);
                markProgress.Add(k);
                markBase.Add(new Vector3(cell, cell, 1f));
            }
            for (int i = 0; i < report.Turned.Count; i++)
            {
                Vector2 at = board.CellToWorld(report.Turned[i]);
                SpriteRenderer bloom = ViewUtil.MakeRounded(transform, "Turned_" + i, at,
                    new Vector2(cell, cell), LateTone, BloomOrder);
                blooms.Add(bloom);
                bloomTime.Add(0f);
                bloomBase.Add(new Vector3(cell, cell, 1f));
            }
        }

        /// <summary>True when something turned to gold this turn - what the sound asks.</summary>
        public bool Turned
        {
            get { return blooms.Count > 0; }
        }

        private void Update()
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i] == null)
                {
                    continue;
                }
                float k = markProgress[i];
                float period = Mathf.Lerp(SlowPulse, FastPulse, k);
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
                Color c = Color.Lerp(EarlyTone, LateTone, k);
                // Never opaque: the cube under it is still a plain cube and has to look like one
                // right up until it is not.
                c.a = Mathf.Lerp(0.16f, 0.52f, k) * Mathf.Lerp(0.6f, 1f, beat);
                marks[i].color = c;
                // The mark TIGHTENS on the beat, written from the stored base rather than from
                // the transform's own scale - read back and multiplied it would compound.
                float squeeze = 1f - 0.03f * beat * k;
                marks[i].transform.localScale = markBase[i] * squeeze;
            }
            for (int i = blooms.Count - 1; i >= 0; i--)
            {
                bloomTime[i] += Time.unscaledDeltaTime;
                float t = bloomTime[i] / BloomSeconds;
                if (t >= 1f || blooms[i] == null)
                {
                    if (blooms[i] != null)
                    {
                        Destroy(blooms[i].gameObject);
                    }
                    blooms.RemoveAt(i);
                    bloomTime.RemoveAt(i);
                    bloomBase.RemoveAt(i);
                    continue;
                }
                // OUT AND SETTLE: the gold swells past the cell and comes back onto it, which is
                // the cube being taken over rather than a light going on over it.
                float swell = t < 0.35f
                    ? Mathf.Lerp(1f, 1.22f, t / 0.35f)
                    : Mathf.Lerp(1.22f, 1f, (t - 0.35f) / 0.65f);
                blooms[i].transform.localScale = bloomBase[i] * swell;
                Color c = LateTone;
                c.a = (1f - t) * 0.75f;
                blooms[i].color = c;
            }
        }

        private void ClearMarks()
        {
            for (int i = 0; i < marks.Count; i++)
            {
                if (marks[i] != null)
                {
                    Destroy(marks[i].gameObject);
                }
            }
            marks.Clear();
            markProgress.Clear();
            markBase.Clear();
        }

        /// <summary>Takes everything down - a new round, a rebuilt board.</summary>
        public void Clear()
        {
            ClearMarks();
            for (int i = 0; i < blooms.Count; i++)
            {
                if (blooms[i] != null)
                {
                    Destroy(blooms[i].gameObject);
                }
            }
            blooms.Clear();
            bloomTime.Clear();
            bloomBase.Clear();
        }
    }
}
