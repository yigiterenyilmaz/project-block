// PURPOSE: "Barut tedarikçisi" - a dynamite block VISIBLY filling with powder. The joker's whole
// proposition is "leave this standing and it gets worth more", and until now the only evidence of
// that was a number on a card in the bar: the block on the board looked exactly as it had the
// turn it landed, so the decision the joker exists to create was invisible where the player was
// actually looking.
//
// TWO THINGS, and they are different on purpose:
//
//   THE SPARK   one short fuse flare per charge, on the frame it is banked. It is the EVENT -
//               this block just gained something - and it goes with the sizzle.
//   THE EMBER   a standing mark on every charged cube for as long as it stays charged. It is the
//               STATE - this block is holding powder - and it is what the player reads when they
//               are deciding whether to blow the block now or leave it one more turn.
//
// Both ride FULLNESS (0 to 1 against the cap, which Core carries in the report so nothing here
// needs to know what the cap is): a barely-charged block is a dim slow ember, a nearly-full one
// is bright, fast and hot. So "how ripe is this" is legible at a glance without a counter - the
// same reasoning that took the hydraulic press off its four orange dots.
//
// IT IS NOT A COUNTER AND IT IS NOT A BAR. A row of pips on a cube asks the player to count
// lights; a fill bar is a UI element sitting on the board. What is drawn is a fuse getting hotter,
// which is what is actually happening.
//
// THE VIEW DECIDES NOTHING: PowderVisuals says which cells and how full. The ember is rebuilt from
// the live report each turn, so a block that explodes simply stops being listed and its mark goes
// with it - there is no bookkeeping here to leak.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Draws the powder on charged dynamite. Owned by GameUiController, which hands it
    /// the turn's report; it holds no state Core does not re-state.</summary>
    public sealed class PowderChargeView : MonoBehaviour
    {
        /// <summary>The ember's colour at empty and at full - a dull warm coal heating to a hot
        /// orange. Never white and never a saturated red: red on a dark board reads as a warning
        /// light, and this is a promise rather than a threat.</summary>
        private static readonly Color CoolEmber = new Color(0.72f, 0.38f, 0.14f);

        private static readonly Color HotEmber = new Color(1f, 0.70f, 0.26f);

        /// <summary>How much of a cell the standing ember covers. Small - it sits ON the cube's
        /// face and must never look like a second cube in the cell.</summary>
        private const float EmberSize = 0.30f;

        /// <summary>The spark's reach at its peak, as a share of a cell.</summary>
        private const float SparkSize = 0.78f;

        private const float SparkSeconds = 0.42f;

        /// <summary>Over the cube, under the destruction effects - the powder is ON the block,
        /// and a line clear taking that block has to read over the top of it. The board's own
        /// cubes sit below these (see GangreneView's ladder for the same reasoning).</summary>
        private const int EmberOrder = 8;

        private const int SparkOrder = 7;

        /// <summary>Seconds for one ember breath at empty, and at full. A fuse burning down gets
        /// FASTER, which is most of what says "this is nearly ready".</summary>
        private const float SlowPulse = 1.9f;

        private const float FastPulse = 0.55f;

        /// <summary>
        /// A FULL BLOCK IS ITS OWN STATE, not just the top of the ramp. At the cap the block is
        /// worth everything it will ever be worth and every further turn of holding it is pure
        /// risk, so it stops being "nearly ready" and starts being "take this now": the ember
        /// grows past the ramp, beats faster than the ramp ever reaches, and its brightness
        /// swings wider instead of sitting at a steady high.
        ///
        /// Read off PowderVisuals.Full rather than from fullness >= 1, because what counts as
        /// full is the rules' business and the cap is a balance number that moves.
        /// </summary>
        private const float FullPulse = 0.30f;

        private const float FullGrow = 1.45f;

        private readonly List<SpriteRenderer> embers = new List<SpriteRenderer>();
        private readonly List<float> emberFullness = new List<float>();
        private readonly List<bool> emberFull = new List<bool>();
        private readonly List<Vector3> emberBase = new List<Vector3>();
        private readonly List<SpriteRenderer> sparks = new List<SpriteRenderer>();
        private readonly List<float> sparkTime = new List<float>();
        private readonly List<float> sparkFullness = new List<float>();

        private BoardView board;

        public void Build(BoardView boardView)
        {
            board = boardView;
        }

        /// <summary>
        /// Restates the standing embers from the turn's report and fires one spark per cell that
        /// just charged. Called once per turn; passing null clears everything, which is what a new
        /// round and a board with no dynamite on it both do.
        /// </summary>
        public void Show(PowderVisuals report)
        {
            ClearEmbers();
            if (report == null || board == null)
            {
                return;
            }
            float cell = board.CellWorldSize;
            for (int i = 0; i < report.Count; i++)
            {
                Vector2 at = board.CellToWorld(report.Cells[i]);
                float full = Mathf.Clamp01(report.Fullness[i]);
                SpriteRenderer ember = ViewUtil.MakeRounded(transform, "Ember_" + i, at,
                    new Vector2(cell * EmberSize, cell * EmberSize),
                    Color.Lerp(CoolEmber, HotEmber, full), EmberOrder);
                bool atCap = report.Full[i];
                if (atCap)
                {
                    ember.transform.localScale = new Vector3(FullGrow, FullGrow, 1f);
                }
                embers.Add(ember);
                emberFullness.Add(full);
                emberFull.Add(atCap);
                emberBase.Add(ember.transform.localScale);
                // Only a cell that GAINED this turn flares. A capped block keeps its ember - it
                // is the one holding the most powder - but it has nothing new to announce.
                if (report.Gained[i])
                {
                    SpawnSpark(at, cell, full);
                }
            }
        }

        /// <summary>The one-shot flare for a charge just banked.</summary>
        private void SpawnSpark(Vector2 at, float cell, float full)
        {
            SpriteRenderer spark = ViewUtil.MakeRounded(transform, "Spark", at,
                new Vector2(cell * SparkSize, cell * SparkSize),
                Color.Lerp(CoolEmber, HotEmber, full), SparkOrder);
            sparks.Add(spark);
            sparkTime.Add(0f);
            sparkFullness.Add(full);
        }

        private void Update()
        {
            // THE EMBER: a breath that quickens and brightens as the block fills. Written from a
            // stored base every frame, never from the renderer's own current value - a scale read
            // back and multiplied again compounds, which is the bug the ice seating had.
            for (int i = 0; i < embers.Count; i++)
            {
                if (embers[i] == null)
                {
                    continue;
                }
                float full = emberFullness[i];
                bool atCap = emberFull[i];
                float period = atCap ? FullPulse : Mathf.Lerp(SlowPulse, FastPulse, full);
                float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
                Color c = Color.Lerp(CoolEmber, HotEmber, full);
                // Even at full the ember never reaches opaque: the cube under it has to stay the
                // thing the player is looking at.
                c.a = Mathf.Lerp(0.26f, 0.80f, full) * Mathf.Lerp(atCap ? 0.35f : 0.55f, 1f, k);
                embers[i].color = c;
                if (atCap)
                {
                    // The beat is in the SIZE as well at the cap - written from the stored base,
                    // never from the transform's own current scale.
                    float swell = 1f + 0.16f * k;
                    embers[i].transform.localScale = emberBase[i] * swell;
                }
            }
            // THE SPARK: out fast, gone. Reverse iteration so a finished one can be dropped.
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                sparkTime[i] += Time.unscaledDeltaTime;
                float t = sparkTime[i] / SparkSeconds;
                if (t >= 1f || sparks[i] == null)
                {
                    if (sparks[i] != null)
                    {
                        Destroy(sparks[i].gameObject);
                    }
                    sparks.RemoveAt(i);
                    sparkTime.RemoveAt(i);
                    sparkFullness.RemoveAt(i);
                    continue;
                }
                // A flare has no rise worth drawing - it is already there and it is leaving.
                Color c = Color.Lerp(CoolEmber, HotEmber, sparkFullness[i]);
                c.a = (1f - t) * (1f - t) * 0.55f;
                sparks[i].color = c;
                float grow = Mathf.Lerp(0.55f, 1f, t);
                sparks[i].transform.localScale = new Vector3(grow, grow, 1f);
            }
        }

        private void ClearEmbers()
        {
            for (int i = 0; i < embers.Count; i++)
            {
                if (embers[i] != null)
                {
                    Destroy(embers[i].gameObject);
                }
            }
            embers.Clear();
            emberFullness.Clear();
            emberFull.Clear();
            emberBase.Clear();
        }

        /// <summary>Takes everything down - a new round, a rebuilt board, leaving the round.</summary>
        public void Clear()
        {
            ClearEmbers();
            for (int i = 0; i < sparks.Count; i++)
            {
                if (sparks[i] != null)
                {
                    Destroy(sparks[i].gameObject);
                }
            }
            sparks.Clear();
            sparkTime.Clear();
            sparkFullness.Clear();
        }
    }
}
