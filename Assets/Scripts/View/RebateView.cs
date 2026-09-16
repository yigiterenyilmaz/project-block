// PURPOSE: "Harcama bonusu" - the draw pile running dry PAYING YOU BACK, drawn as one causal
// chain from the empty slot to the score.
//
// THE MECHANIC IS NOT "you scored some points". It is "you spent the resource, and the spending
// itself refunded you". So the payout may not simply appear: it has to be SEEN COMING OUT OF THE
// EMPTY PILE. Six beats, one clock, about a second:
//
//   1. THE PILE IS EMPTY      the slot settles a pixel, cools, and a thin void rim runs round it
//   2. THE REBATE WAKES       a short muted-gold line in the middle of the empty slot
//   3. THE RECEIPT UNFURLS    a warm voucher strip opens out of that line, height then width
//   4. THE VALUE IS STAMPED   the REAL payout lands on it with a small compression and a few
//                             gold flecks, and is then simply legible for a fifth of a second
//   5. IT FOLDS IN            the strip gathers to its middle and becomes one small rebate core
//   6. IT IS COLLECTED        the core arcs to the score and is absorbed
//
// WHAT IT IS NOT: no full-screen flash, no coin rain, no jackpot, no shake, no camera move, no
// legendary aura. This is a COMMON joker and the tone is deliberately dry - the same event that
// pays you here eats a piece of the arena (RoundEngine.NoteDeckRecycled) and, past the threshold,
// is the loss condition. The joker's own comment calls it a consolation and not a rescue, so this
// pays out without ever congratulating anybody.
//
// TWO THINGS COME FROM CORE AND MUST NEVER BE DERIVED HERE:
//   THE AMOUNT. PointsPerEmptyDrawPile is a balance placeholder; a hard-coded "+60" is a picture
//   that will one day lie about the score. It is RebateVisuals.Payout.
//   THE COUNT. The joker pays on a BOOL - "did the pile empty at all this turn" - so a turn where
//   it emptied twice pays ONCE. Hanging this off "the draw pile just emptied" would play it twice
//   and promise money that never arrives. It is keyed on the report's SERIAL.
//
// BOTH ANCHORS ARE ASKED FOR, never written down: the source is the real draw pile's own position
// and the target the real score label's, both of which move with the layout profile. A hard-coded
// corner is a payout that flies into empty space on a phone.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    public sealed class RebateView : MonoBehaviour
    {
        /// <summary>Every number, in one place.</summary>
        public static class Style
        {
            // ---- the timeline (seconds, on one clock; the beats overlap) ----
            public static float EmptyBeat = 0.10f;

            public static float LineAt = 0.07f;

            public static float LineFor = 0.075f;

            public static float StripAt = 0.10f;

            public static float StripFor = 0.12f;

            public static float StampAt = 0.20f;

            public static float StampFor = 0.115f;

            /// <summary>How long the value is simply LEGIBLE, doing nothing. The reward beat -
            /// without it the player sees a shape move, not a number arrive.</summary>
            public static float HoldFor = 0.18f;

            public static float FoldFor = 0.14f;

            /// <summary>The flight, at the far end of a range scaled by how far it has to go.
            /// </summary>
            public static float TravelNear = 0.26f;

            public static float TravelFar = 0.42f;

            public static float Absorb = 0.07f;

            /// <summary>How long the score stays warm after it lands.</summary>
            public static float ScoreWarmFor = 0.17f;

            /// <summary>A hard ceiling on the whole thing. It only happens when the deck runs
            /// out, so a second is affordable - but not a second and a half.</summary>
            public static float Ceiling = 1.15f;

            // ---- the empty pile ----
            /// <summary>How far the slot settles. A pixel or two of "that is gone", never a
            /// bounce and never a punishment.</summary>
            public static float PileSettle = 0.972f;

            public static Color VoidRim = new Color(0.62f, 0.60f, 0.55f, 0.5f);

            // ---- the receipt ----
            /// <summary>Strip WIDTH as a share of the draw pile's width. Its height is not a
            /// number here: the 3:1 ribbon proportion is baked into the sprite
            /// (RebateShapes.StripAspect), which is what keeps it a voucher and stops it reading
            /// as a card - the one silhouette it must never borrow.</summary>
            public static float StripWide = 0.66f;

            public static Color Cream = new Color(0.96f, 0.89f, 0.72f);

            public static Color OldGold = new Color(0.78f, 0.62f, 0.28f);

            public static Color Bronze = new Color(0.36f, 0.26f, 0.12f);

            public static Color Amber = new Color(0.93f, 0.72f, 0.34f);

            /// <summary>
            /// THE VALUE'S INK: STAMP RED.
            ///
            /// It was ivory, which measured 1.11 against the cream strip it is printed on -
            /// not a soft look, a NUMBER YOU CANNOT READ. The mistake was inherited from the
            /// pass where the strip was still white: ivory on white-ish is invisible either
            /// way, and it survived the strip being given its proper colour.
            ///
            /// Red is also the right answer rather than merely a readable one: the beat is a
            /// STAMP, and stamped ink on a voucher is red. Measured at 5.07 against the strip,
            /// over the 4.5 the card plates are held to. A brighter red reads better as a colour
            /// and worse as text - (0.90, 0.15, 0.13) only manages 3.56.
            /// </summary>
            public static Color Ink = new Color(0.72f, 0.13f, 0.12f);

            /// <summary>The shadow under it - a dark bronze, offset a hair down and right. The
            /// spec asked for it from the start and it was never built; on a warm strip it is
            /// what gives the digits an edge.</summary>
            public static Color InkShade = new Color(0.26f, 0.13f, 0.07f);

            public static float InkShadeDrop = 0.055f;

            /// <summary>
            /// The value's height as a share of the STRIP's height - not of the pile, and not an
            /// absolute.
            ///
            /// It shipped as a share of the pile WIDTH and came out 180% of the strip it was
            /// meant to be printed on: a giant number overflowing the tray and colliding with the
            /// pile's own count. A number stamped on a voucher is smaller than the voucher.
            /// </summary>
            public static float TextShareOfStrip = 0.62f;

            /// <summary>The font is rendered at this size and scaled by characterSize; a TextMesh
            /// ends up about fontSize * characterSize / 10 world units tall, which is the
            /// relation the stamp solves BACKWARDS from the height it wants.</summary>
            public static int TextFont = 64;

            /// <summary>The stamp's overshoot, and the strip's answer to it.</summary>
            public static float StampPunch = 1.13f;

            public static float StripPress = 0.965f;

            // ---- the flecks ----
            public static int FleckMin = 4;

            public static int FleckMax = 6;

            public static float FleckSize = 0.085f;

            public static float FleckReach = 0.34f;

            public static float FleckLife = 0.26f;

            /// <summary>Everything this effect may have on screen at once. It is a COMMON joker.
            /// </summary>
            public static int ParticleCap = 12;

            // ---- the core and the flight ----
            public static float CoreSize = 0.2f;

            /// <summary>
            /// How far the flight lifts above the straight line, as a share of the distance.
            ///
            /// It used to bow PERPENDICULAR to the travel at 0.22, and measured against the real
            /// anchors that put the control point at (1.8, 0.0) - which is the middle of the
            /// board. The board is not part of this mechanic at all: it is neither source, nor
            /// target, nor a waypoint. The lift is now straight UP, where there is nothing.
            /// </summary>
            public static float Lift = 0.17f;

            /// <summary>How much of the way toward the target the second control sits, so the
            /// flight arrives along the score's own direction instead of swinging past it.
            /// </summary>
            public static float Approach = 0.72f;

            /// <summary>
            /// The ribbon behind the token, in shares of the pile's width.
            ///
            /// It shipped at 0.42 x 0.27 against a 0.31 token - LONGER AND WIDER THAN THE THING
            /// IT TRAILED - drawn on a shape that tapers to a point and rotated along the
            /// flight. That is an arrowhead, and "a gold triangle flying across the board" is
            /// exactly what was reported. A trail is thinner than its token, always.
            /// </summary>
            public static float TrailLong = 0.26f;

            public static float TrailWide = 0.045f;

            /// <summary>The score's own answer. Small: the receipt already showed the number.
            /// </summary>
            public static float ScorePunch = 0.085f;
        }

        /// <summary>What the lab can take away, one layer at a time.</summary>
        public static class Layers
        {
            public static bool ShowEmptyBeat = true;

            public static bool ShowGoldLine = true;

            public static bool ShowReceiptStrip = true;

            public static bool ShowStamp = true;

            public static bool ShowFlecks = true;

            public static bool ShowRebateCore = true;

            public static bool ShowCollectionPath = true;

            public static bool ShowScoreResponse = true;

            /// <summary>DEV: ring the SOURCE the payout came from and the TARGET it is going to,
            /// so "is this anchored to the real draw pile and the real score" is a glance.
            /// </summary>
            public static bool ShowAnchors = false;

            /// <summary>DEV: print what the RULES said - the payout, the count this round and
            /// whether the threshold had already gone - beside what the picture is doing.
            /// </summary>
            public static bool ShowReport = false;

            /// <summary>DEV: dot the whole flight path. The board is not part of this mechanic -
            /// not source, not target, not a waypoint - and this is how that stops being a claim
            /// and becomes something you can look at.</summary>
            public static bool ShowBezier = false;

            public static void AllOn()
            {
                ShowEmptyBeat = true;
                ShowGoldLine = true;
                ShowReceiptStrip = true;
                ShowStamp = true;
                ShowFlecks = true;
                ShowRebateCore = true;
                ShowCollectionPath = true;
                ShowScoreResponse = true;
                ShowAnchors = false;
                ShowReport = false;
                ShowBezier = false;
            }
        }

        /// <summary>Audio hooks. Soft and dry: a thup, a stylised swipe, a muted tok, a small
        /// warm chime. Never a cash register, a slot machine or a casino.</summary>
        public static System.Action<string> Sounded;

        public const string SoundEmptyBeat = "harcama.empty";
        public const string SoundReceiptOpen = "harcama.receipt";
        public const string SoundStamp = "harcama.stamp";
        public const string SoundCollect = "harcama.collect";
        public const string SoundArrive = "harcama.arrive";

        private const int BackOrder = 86;
        private const int StripOrder = 87;
        private const int TextOrder = 89;
        private const int CoreOrder = 90;

        private sealed class Fleck
        {
            public SpriteRenderer Sprite;
            public Vector2 From;
            public Vector2 To;
            public float Born;
            public float Spin;
            public float Size;
        }

        private RebateVisuals report;
        private int playedSerial = -1;
        private Vector2 source;
        private Vector2 target;
        private float pileWidth = 1.5f;
        private float clock = -1f;

        private SpriteRenderer rim;
        private SpriteRenderer line;
        private SpriteRenderer stripEdge;
        private SpriteRenderer stripShade;
        private SpriteRenderer strip;
        private SpriteRenderer core;
        private SpriteRenderer trail;
        private SpriteRenderer sourceMark;
        private SpriteRenderer targetMark;
        private TextMesh value;
        private TextMesh valueShade;
        private TextMesh debug;
        private readonly List<Fleck> flecks = new List<Fleck>();
        private readonly List<SpriteRenderer> pathDots = new List<SpriteRenderer>();
        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private bool saidOpen;
        private bool saidStamp;
        private bool saidCollect;
        private bool saidArrive;

        /// <summary>0..1 while the score is answering, for the HUD to read. The label itself is
        /// the controller's - this view never reaches into the canvas.</summary>
        public float ScoreWarm { get; private set; }

        public bool Busy
        {
            get { return clock >= 0f; }
        }

        /// <summary>True once the receipt has folded away - the moment the draw pile can go back
        /// to showing what the rules actually hold, because nothing is standing on it any more.
        /// </summary>
        public bool ReceiptGone
        {
            get { return clock >= FoldEnd; }
        }

        /// <summary>Everything except the flight, on the view's own clock.</summary>
        private float FoldEnd
        {
            get
            {
                return Style.StampAt + Style.StampFor + Style.HoldFor + Style.FoldFor;
            }
        }

        private float Travel
        {
            get
            {
                // Longer for a longer way, so the flight has one speed rather than one duration.
                float far = Mathf.Clamp01(Vector2.Distance(source, target) / 9f);
                return Mathf.Lerp(Style.TravelNear, Style.TravelFar, far);
            }
        }

        private float Total
        {
            get { return Mathf.Min(FoldEnd + Travel + Style.Absorb, Style.Ceiling); }
        }

        /// <summary>
        /// One turn's cashback.
        ///
        /// Called on every repaint with whatever the joker last paid, so the SERIAL is what makes
        /// it happen once - and once is the whole point here, because the rules pay once for a
        /// turn however many times the pile ran dry in it.
        /// </summary>
        public void Play(RebateVisuals paid, Vector2 drawPile, Vector2 score, float pileWide)
        {
            if (paid == null || paid.Serial == playedSerial)
            {
                return;
            }
            playedSerial = paid.Serial;
            report = paid;
            source = drawPile;
            target = score;
            pileWidth = Mathf.Max(pileWide, 0.2f);
            clock = 0f;
            saidOpen = saidStamp = saidCollect = saidArrive = false;
            Announce(SoundEmptyBeat);
        }

        /// <summary>Takes everything down. The board calls this when it is torn down, and the
        /// lab's RESET calls it too.</summary>
        public void Stop()
        {
            clock = -1f;
            ScoreWarm = 0f;
            Hide(rim);
            Hide(line);
            Hide(strip);
            Hide(core);
            Hide(trail);
            Hide(sourceMark);
            Hide(targetMark);
            if (value != null) { value.gameObject.SetActive(false); }
            if (valueShade != null) { valueShade.gameObject.SetActive(false); }
            if (debug != null) { debug.gameObject.SetActive(false); }
            for (int i = 0; i < flecks.Count; i++)
            {
                Return(flecks[i].Sprite);
            }
            flecks.Clear();
            for (int i = 0; i < pathDots.Count; i++)
            {
                Hide(pathDots[i]);
            }
            Hide(stripEdge);
            Hide(stripShade);
        }

        private void LateUpdate()
        {
            if (clock < 0f)
            {
                return;
            }
            clock += Time.deltaTime;
            PaintEmptyBeat();
            PaintLine();
            PaintStrip();
            PaintFlecks();
            PaintCore();
            PaintPath();
            PaintAnchors();
            PaintDebug();
            if (clock > Total + Style.ScoreWarmFor)
            {
                Stop();
            }
        }

        // ---- 1. THE PILE IS EMPTY --------------------------------------------------------------

        private void PaintEmptyBeat()
        {
            // A THIN VOID RIM round the slot, and nothing else. The pile's own settle is the card
            // layer's business (CardLayerView.PlayDrawPileEmptyBeat) - it owns that transform and
            // an effect reaching into it would fight the next repaint.
            float k = Span(clock, 0f, Style.EmptyBeat * 2.2f);
            if (!Layers.ShowEmptyBeat || k >= 1f)
            {
                Hide(rim);
                return;
            }
            if (rim == null)
            {
                rim = Rent(RebateShapes.Ring, BackOrder);
            }
            rim.enabled = true;
            float on = Mathf.Sin(k * Mathf.PI);
            rim.transform.position = source;
            float w = pileWidth * 1.04f;
            rim.transform.localScale = new Vector3(w, w * 1.29f, 1f);
            Color c = Style.VoidRim;
            c.a *= on;
            rim.color = c;
        }

        // ---- 2. THE REBATE WAKES ---------------------------------------------------------------

        private void PaintLine()
        {
            float k = Span(clock, Style.LineAt, Style.LineAt + Style.LineFor);
            // It hands over to the strip: once that is opening, the line has become its middle.
            float gone = Span(clock, Style.StripAt, Style.StripAt + Style.StripFor * 0.5f);
            if (!Layers.ShowGoldLine || k <= 0f || gone >= 1f)
            {
                Hide(line);
                return;
            }
            if (line == null)
            {
                line = Rent(RebateShapes.Bar, StripOrder);
            }
            line.enabled = true;
            line.transform.position = source;
            float w = pileWidth * Style.StripWide * Mathf.Lerp(0.2f, 1f, Smooth(k));
            line.transform.localScale = new Vector3(w, pileWidth * 0.09f, 1f);
            Color c = Style.OldGold;
            c.a = Smooth(k) * (1f - gone);
            line.color = c;
        }

        // ---- 3-5. THE RECEIPT ------------------------------------------------------------------

        private void PaintStrip()
        {
            float open = Span(clock, Style.StripAt, Style.StripAt + Style.StripFor);
            float fold = Span(clock, FoldEnd - Style.FoldFor, FoldEnd);
            if (!Layers.ShowReceiptStrip || open <= 0f || fold >= 1f)
            {
                Hide(strip);
                Hide(stripEdge);
                Hide(stripShade);
                if (value != null) { value.gameObject.SetActive(false); }
                if (valueShade != null) { valueShade.gameObject.SetActive(false); }
                return;
            }
            if (strip == null)
            {
                stripShade = Rent(RebateShapes.Strip, StripOrder - 1);
                stripEdge = Rent(RebateShapes.Strip, StripOrder);
                strip = Rent(RebateShapes.Strip, StripOrder + 1);
            }
            strip.enabled = true;
            // LIFTED a few pixels off the slot, so it reads as having come OUT of the pile
            // rather than as having been printed on it - but only a few, because a voucher
            // hovering half a card above its own tray is a floating UI panel.
            strip.transform.position = source + new Vector2(0f, pileWidth * 0.045f);

            // HEIGHT FIRST, THEN WIDTH - it opens out of the line rather than scaling up as a
            // block, which is the difference between a voucher unfurling and a panel appearing.
            float h = Mathf.Lerp(0.1f, 1.05f, Smooth(Mathf.Clamp01(open / 0.6f)));
            if (open > 0.6f) { h = Mathf.Lerp(1.05f, 1f, Smooth((open - 0.6f) / 0.4f)); }
            float w = Mathf.Lerp(0.45f, 1f, Smooth(Mathf.Clamp01((open - 0.25f) / 0.75f)));

            // The stamp presses it down a hair, and the fold gathers it to its middle.
            float stamp = Span(clock, Style.StampAt, Style.StampAt + Style.StampFor);
            if (Layers.ShowStamp && stamp > 0f && stamp < 1f)
            {
                h *= Mathf.Lerp(Style.StripPress, 1f, Smooth(stamp));
            }
            if (fold > 0f)
            {
                w *= Mathf.Lerp(1f, 0.2f, Smooth(fold));
                h *= Mathf.Lerp(1f, 0.35f, Smooth(fold));
            }

            // ONE reference size: the sprite is a unit square with the 3:1 ribbon baked into
            // it (RebateShapes.StripAspect), so w and h here are pure animation factors and the
            // proportion cannot drift out from under them.
            float wide = pileWidth * Style.StripWide;
            strip.transform.localScale = new Vector3(wide * w, wide * h, 1f);
            // CREAM-GOLD, and the first pass shipped it as Color.white - which on this warm
            // pile art read as no voucher at all, which is exactly what was reported. Three
            // renderers, because a voucher needs a body, an edge and something under it:
            // an antique-gold plate a hair larger BEHIND the cream body, and a soft bronze
            // contact shadow under both. Cheaper and flatter than a border layer, and it cannot
            // come apart when the strip folds because all three take the same scale.
            float alpha = open >= 1f ? 1f : Smooth(open);
            strip.color = new Color(Style.Cream.r, Style.Cream.g, Style.Cream.b, alpha);

            Vector3 scale = strip.transform.localScale;
            stripEdge.enabled = true;
            stripEdge.transform.position = strip.transform.position;
            stripEdge.transform.localScale = scale * 1.055f;
            stripEdge.color = new Color(Style.OldGold.r, Style.OldGold.g, Style.OldGold.b, alpha);

            stripShade.enabled = true;
            stripShade.transform.position = strip.transform.position
                + new Vector3(0f, -pileWidth * 0.022f, 0f);
            stripShade.transform.localScale = scale * 1.02f;
            stripShade.color = new Color(Style.Bronze.r, Style.Bronze.g, Style.Bronze.b,
                alpha * 0.55f);

            if (open > 0.4f && !saidOpen)
            {
                saidOpen = true;
                Announce(SoundReceiptOpen);
            }
            PaintValue(stamp, fold);
        }

        private void PaintValue(float stamp, float fold)
        {
            if (!Layers.ShowStamp || report == null || stamp <= 0f)
            {
                if (value != null) { value.gameObject.SetActive(false); }
                if (valueShade != null) { valueShade.gameObject.SetActive(false); }
                return;
            }
            if (value == null)
            {
                valueShade = ViewUtil.MakeText3D(transform, "RebateValueShade", Vector2.zero,
                    string.Empty, Style.TextFont, 0.03f, Style.InkShade, TextOrder - 1,
                    TextAnchor.MiddleCenter);
                value = ViewUtil.MakeText3D(transform, "RebateValue", Vector2.zero, string.Empty,
                    Style.TextFont, 0.03f, Style.Ink, TextOrder, TextAnchor.MiddleCenter);
            }
            value.gameObject.SetActive(true);
            valueShade.gameObject.SetActive(true);
            // THE NUMBER IS CORE'S. Never a constant, never re-derived: see the file header.
            value.text = "+" + report.Payout;
            value.transform.position = strip != null && strip.enabled
                ? strip.transform.position
                : (Vector3)source;

            // 0.65 -> 1.13 -> 0.98 -> 1: a stamp, not a pop.
            float s;
            if (stamp < 0.45f) { s = Mathf.Lerp(0.65f, Style.StampPunch, Smooth(stamp / 0.45f)); }
            else if (stamp < 0.75f)
            {
                s = Mathf.Lerp(Style.StampPunch, 0.98f, Smooth((stamp - 0.45f) / 0.30f));
            }
            else { s = Mathf.Lerp(0.98f, 1f, Smooth((stamp - 0.75f) / 0.25f)); }

            float alpha = 1f;
            if (fold > 0f)
            {
                s *= Mathf.Lerp(1f, 0.45f, Smooth(fold));
                alpha = 1f - Smooth(Mathf.Clamp01(fold / 0.8f));
            }
            // SOLVED BACKWARDS from the strip's own height, so the stamp is always a number
            // ON the voucher rather than a number NEAR it.
            float stripHigh = pileWidth * Style.StripWide / RebateShapes.StripAspect;
            float wanted = stripHigh * Style.TextShareOfStrip * s;
            value.characterSize = Mathf.Max(wanted * 10f / Style.TextFont, 0.0001f);
            Color ink = Style.Ink;
            ink.a = alpha;
            value.color = ink;

            // The shadow rides with it: same text, same size, a hair down and right.
            valueShade.text = value.text;
            valueShade.characterSize = value.characterSize;
            float drop = stripHigh * Style.InkShadeDrop;
            valueShade.transform.position = value.transform.position
                + new Vector3(drop, -drop, 0f);
            Color shade = Style.InkShade;
            shade.a = alpha * 0.8f;
            valueShade.color = shade;

            if (!saidStamp && stamp > 0.15f)
            {
                saidStamp = true;
                Announce(SoundStamp);
                Throw();
            }
        }

        // ---- the flecks ------------------------------------------------------------------------

        /// <summary>A few small gold chips off the stamp, and a hard cap. This is a COMMON joker:
        /// the richness is in the chain reading, not in the particle count.</summary>
        private void Throw()
        {
            if (!Layers.ShowFlecks)
            {
                return;
            }
            int want = Random.Range(Style.FleckMin, Style.FleckMax + 1);
            for (int i = 0; i < want && flecks.Count < Style.ParticleCap; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f);
                var f = new Fleck
                {
                    Sprite = Rent(RebateShapes.Fleck(i), TextOrder + 1),
                    From = source,
                    To = source + dir * pileWidth * Style.FleckReach
                        * Random.Range(0.55f, 1f),
                    Born = clock,
                    Spin = Random.Range(-220f, 220f),
                    Size = pileWidth * Style.FleckSize * Random.Range(0.7f, 1.25f)
                };
                flecks.Add(f);
            }
        }

        private void PaintFlecks()
        {
            for (int i = flecks.Count - 1; i >= 0; i--)
            {
                Fleck f = flecks[i];
                float k = (clock - f.Born) / Style.FleckLife;
                if (k >= 1f)
                {
                    Return(f.Sprite);
                    flecks.RemoveAt(i);
                    continue;
                }
                float out01 = 1f - (1f - k) * (1f - k);
                f.Sprite.enabled = true;
                f.Sprite.transform.position = Vector2.Lerp(f.From, f.To, out01);
                f.Sprite.transform.localScale = new Vector3(f.Size, f.Size, 1f);
                f.Sprite.transform.rotation =
                    Quaternion.Euler(0f, 0f, f.Spin * (clock - f.Born));
                Color c = k < 0.5f ? Style.Cream : Style.OldGold;
                c.a = 1f - Smooth(k);
                f.Sprite.color = c;
            }
        }

        // ---- 5-6. THE CORE, AND THE FLIGHT -----------------------------------------------------

        private void PaintCore()
        {
            float born = Span(clock, FoldEnd - Style.FoldFor * 0.45f, FoldEnd);
            float fly = Span(clock, FoldEnd, FoldEnd + Travel);
            float eat = Span(clock, FoldEnd + Travel, FoldEnd + Travel + Style.Absorb);
            if (!Layers.ShowRebateCore || born <= 0f || eat >= 1f)
            {
                Hide(core);
                Hide(trail);
                if (eat >= 1f && !saidArrive)
                {
                    saidArrive = true;
                    Announce(SoundArrive);
                }
                TickWarm(eat >= 1f);
                return;
            }
            if (core == null)
            {
                core = Rent(RebateShapes.Core, CoreOrder);
            }
            core.enabled = true;

            // SLOW LAUNCH, then accelerate, then a fast snap in - and a slight arc, never an S
            // and never a waypoint out over the board.
            float eased = fly * fly * (3f - 2f * fly);
            eased = Mathf.Lerp(eased, fly * fly, 0.35f);
            Vector2 at = Layers.ShowCollectionPath
                ? Arc(source, target, eased)
                : Vector2.Lerp(source, target, eased);
            core.transform.position = at;

            float s = pileWidth * Style.CoreSize * Mathf.Lerp(0.45f, 1f, Smooth(born));
            if (eat > 0f) { s *= Mathf.Lerp(1f, 0f, Smooth(eat)); }
            core.transform.localScale = new Vector3(s, s, 1f);
            core.color = Color.Lerp(Style.Cream, Style.Amber, 0.45f);

            if (fly > 0f && !saidCollect)
            {
                saidCollect = true;
                Announce(SoundCollect);
            }

            // A SHORT WARM RIBBON behind it - one tapered strip along the way it came, never a
            // straight bright line across the screen.
            if (Layers.ShowCollectionPath && fly > 0.02f && fly < 1f)
            {
                if (trail == null)
                {
                    trail = Rent(RebateShapes.Trail, CoreOrder - 1);
                }
                Vector2 back = Arc(source, target, Mathf.Max(0f, eased - 0.09f));
                Vector2 span = at - back;
                float len = Mathf.Max(span.magnitude, 0.0001f);
                trail.enabled = true;
                trail.transform.position = (at + back) * 0.5f;
                trail.transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                // Clamped BELOW the token's own size on the cross axis, so the pair can never
                // read as one pointed object however the numbers are tuned later.
                float wide = Mathf.Min(pileWidth * Style.TrailWide,
                    pileWidth * Style.CoreSize * 0.45f);
                trail.transform.localScale = new Vector3(
                    Mathf.Min(len, pileWidth * Style.TrailLong), wide, 1f);
                Color t = Style.Amber;
                t.a = 0.55f * Mathf.Sin(Mathf.Clamp01(fly) * Mathf.PI);
                trail.color = t;
            }
            else
            {
                Hide(trail);
            }
            TickWarm(false);
        }

        /// <summary>The score's own answer, published for the HUD to read. This view never
        /// touches the canvas: the label belongs to the controller, and two effects warming it
        /// at once have to be combined in one place.</summary>
        private void TickWarm(bool arrived)
        {
            float from = FoldEnd + Travel;
            float k = Span(clock, from, from + Style.ScoreWarmFor);
            ScoreWarm = Layers.ShowScoreResponse && clock >= from ? Mathf.Sin(k * Mathf.PI) : 0f;
        }

        /// <summary>
        /// THE FLIGHT: a cubic bezier from the pile to the score, and the one thing it must not
        /// do is visit the board.
        ///
        /// Both controls are written in terms of the two ANCHORS and a straight UP lift - there
        /// is no board-centre term anywhere, which is the point. The path may of course happen to
        /// cross the board on its way; what it may not do is aim at it. That distinction is the
        /// whole of it, and the first version failed it by bowing perpendicular to the travel,
        /// which put its control point almost exactly on the board's middle.
        /// </summary>
        private static Vector2 Arc(Vector2 a, Vector2 b, float t)
        {
            Vector2 span = b - a;
            float lift = span.magnitude * Style.Lift;
            // Out of the pile: a little up, and a little the way it is going.
            Vector2 c1 = a + new Vector2(span.x * 0.18f, lift);
            // Into the score: already most of the way there, still above the line, so it arrives
            // along the score's own direction rather than swinging past it.
            Vector2 c2 = a + span * Style.Approach + new Vector2(0f, lift * 0.55f);
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
        }

        // ---- dev ---------------------------------------------------------------------------------

        private void PaintPath()
        {
            if (!Layers.ShowBezier)
            {
                for (int i = 0; i < pathDots.Count; i++)
                {
                    Hide(pathDots[i]);
                }
                return;
            }
            const int dots = 18;
            while (pathDots.Count < dots)
            {
                pathDots.Add(Rent(RebateShapes.Core, BackOrder));
            }
            for (int i = 0; i < dots; i++)
            {
                SpriteRenderer d = pathDots[i];
                d.enabled = true;
                d.sprite = RebateShapes.Core;
                d.transform.position = Arc(source, target, i / (float)(dots - 1));
                float sz = pileWidth * 0.045f;
                d.transform.localScale = new Vector3(sz, sz, 1f);
                d.color = new Color(0.35f, 1f, 0.55f, 0.75f);
            }
        }

        private void PaintAnchors()
        {
            if (!Layers.ShowAnchors)
            {
                Hide(sourceMark);
                Hide(targetMark);
                return;
            }
            if (sourceMark == null) { sourceMark = Rent(RebateShapes.Ring, CoreOrder + 1); }
            if (targetMark == null) { targetMark = Rent(RebateShapes.Ring, CoreOrder + 1); }
            sourceMark.enabled = true;
            targetMark.enabled = true;
            sourceMark.transform.position = source;
            targetMark.transform.position = target;
            float s = pileWidth * 0.5f;
            sourceMark.transform.localScale = new Vector3(s, s, 1f);
            targetMark.transform.localScale = new Vector3(s, s, 1f);
            sourceMark.color = new Color(0.3f, 1f, 0.4f, 0.85f);
            targetMark.color = new Color(1f, 0.4f, 0.35f, 0.85f);
        }

        private void PaintDebug()
        {
            if (!Layers.ShowReport || report == null)
            {
                if (debug != null) { debug.gameObject.SetActive(false); }
                return;
            }
            if (debug == null)
            {
                debug = ViewUtil.MakeText3D(transform, "RebateDebug", Vector2.zero, string.Empty,
                    48, 0.035f, new Color(0.7f, 1f, 0.8f), CoreOrder + 2, TextAnchor.UpperCenter);
            }
            debug.gameObject.SetActive(true);
            debug.transform.position = source + new Vector2(0f, -pileWidth * 0.75f);
            debug.text = "payout " + report.Payout + "\nround x" + report.TimesThisRound
                + (report.ThresholdPassed ? "\novertime" : string.Empty);
        }

        // ---- pool --------------------------------------------------------------------------------

        private SpriteRenderer Rent(Sprite art, int order)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("Rebate");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.enabled = true;
            r.sprite = art;
            r.sortingOrder = order;
            r.transform.rotation = Quaternion.identity;
            return r;
        }

        private void Return(SpriteRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
                spare.Push(r);
            }
        }

        private static void Hide(SpriteRenderer r)
        {
            if (r != null)
            {
                r.enabled = false;
            }
        }

        private static float Span(float t, float from, float to)
        {
            return Mathf.Clamp01((t - from) / Mathf.Max(to - from, 0.0001f));
        }

        private static float Smooth(float k)
        {
            return k * k * (3f - 2f * k);
        }

        private static void Announce(string what)
        {
            if (Sounded != null)
            {
                Sounded(what);
            }
        }
    }
}
