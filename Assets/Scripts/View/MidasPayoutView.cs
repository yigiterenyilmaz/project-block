// PURPOSE: "Midas" - GOLDEN DIVIDEND. The payout the joker makes EVERY TURN for the gold you are
// holding, drawn so that the player can follow the money from the cube that earned it to the score
// that received it.
//
// THE SUBJECT IS THE CUBE, NOT THE CARD. "Midas paid 8" is a number; "each of these four gold
// cubes paid 2" is the rule, and the rule is what the animation has to teach. So every payout
// starts ON a gold cube in the hand, and the number that leaves it is that cube's own.
//
// THE CHAIN, and it is the whole design - break a link and the effect stops explaining anything:
//
//   WAKE       each held gold cube warms, in turn, a wave running across the hand. A hand that
//              flashes all at once is a screen effect; a wave has a direction and a source.
//   VALUE      the cube's own amount is born out of its middle and fans away from it - left cube
//              up-left, right cube up-right - so three payouts read as three, never as one number
//              printed three times.
//   FLECKS     two to four small gold shards, and a very rare four-point spark. Not confetti,
//              not a coin shower: this is a dividend, not a jackpot.
//   ESSENCE    the number shrinks and leaves a small warm seed. The TEXT is the reward being
//              read; the SEED is the reward being collected, and splitting the two is what keeps
//              a twelve-cube payout from being twelve numbers flying across the screen.
//   COLLECTION the seeds curve into the score, slow then fast, and the score answers once.
//   TOTAL      one +N beside it, at the end, for the reward rather than the mechanism.
//
// IT RUNS EVERY SINGLE TURN, which decides more about it than anything else: about eight tenths of
// a second, no screen shake, no flash, no camera move, nothing full-screen. Common rarity, and the
// spectacle has to match - see Style.
//
// THE VIEW DECIDES NOTHING. MidasPayoutVisuals (Core, reporting only, [NotSaved]) says which held
// cards paid, how many of their cubes were gold at the time, what a cube is worth and what the
// total was. The amount is never "+2" here: that number is a balance placeholder and the day it
// changes this must change with it, which it does by never having known it.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The payout animation. Pooled, self-driving, and finished inside a second.</summary>
    public sealed class MidasPayoutView : MonoBehaviour
    {
        /// <summary>Where one paying card actually is on screen. The View does not go looking for
        /// cards: whoever owns the hand (the controller, or the lab with a hand of its own) works
        /// out the positions and hands them over, so both drive the same animation.</summary>
        public struct SourceSpot
        {
            /// <summary>The card's own centre - where a subtotal goes when there are too many
            /// cubes for a number each.</summary>
            public Vector2 Card;

            /// <summary>Half the card's height on screen. The amount is parked just CLEAR of the
            /// card's top edge: printed over the card it lands on the element band ("ALTIN") and
            /// on the cubes themselves, and a number sitting on top of its own source is the one
            /// arrangement that makes the source unreadable.</summary>
            public float CardHalf;

            /// <summary>One point per GOLD cube, in world space.</summary>
            public List<Vector2> Cubes;

            /// <summary>How big one of those cubes is on screen, so the glint over it is the
            /// cube's own size rather than a number guessed here. A card in the hand and a card
            /// in the lab are not drawn at the same scale.</summary>
            public float CubeSize;

            public int Subtotal;
        }

        public static class Style
        {
            // ---- the palette: old gold, and nothing that reads as neon or as a casino ----
            public static readonly Color Gold = new Color(0.86f, 0.68f, 0.28f);

            public static readonly Color Amber = new Color(0.78f, 0.55f, 0.20f);

            /// <summary>The warm ivory a number's face is lit with - never white, which on this
            /// board reads as a UI colour rather than as metal.</summary>
            public static readonly Color Ivory = new Color(0.98f, 0.93f, 0.78f);

            public static readonly Color Bronze = new Color(0.30f, 0.20f, 0.08f);

            // ---- the wake ----
            /// <summary>How long one cube takes to warm, and how far apart two cubes are. The
            /// stagger is what makes it a wave across the hand instead of a flash.</summary>
            public static float Wake = 0.085f;

            public static float Stagger = 0.028f;

            /// <summary>What the stagger is squeezed to when the hand is full of gold - twenty
            /// cubes at the full stagger is over half a second before the last one even starts.
            /// </summary>
            public static float StaggerTight = 0.016f;

            /// <summary>
            /// EVERY SIZE IN THIS FILE IS A WORLD UNIT, AND A WORLD UNIT IS ABOUT 108 SCREEN
            /// PIXELS. That sentence is here because the first pass did not have it: the flecks
            /// were 0.05 (five pixels), the seeds 0.055 (six) and the numbers came out the
            /// smallest popup in a game whose sweep popup is 0.8 - which on screen is nothing at
            /// all. A card's mini cube is 0.28 units, about thirty pixels, and THAT is the ruler
            /// everything on a card is measured against.
            ///
            /// Scale is the one dial over the lot, so "is it big enough" can be answered by
            /// looking rather than by arguing - the lab cycles it (see the vine-size lesson).
            /// </summary>
            public static float Scale = 1f;

            /// <summary>The glint over a cube's middle, as a multiple of THE CUBE. Over one,
            /// because it has to read as the cube lighting up rather than as a dot placed on it -
            /// at 0.62 it was smaller than the thing it was supposed to be happening to.</summary>
            public static float GlintSize = 1.5f;

            /// <summary>And a floor, so a small card still lights up.</summary>
            public static float GlintFloor = 0.3f;

            /// <summary>
            /// THE FOIL SWEEP - a thin warm highlight running across the cube, low-left to
            /// high-right, once. It is the whole reason the cube reads as METAL DOING SOMETHING
            /// rather than as a square with a light put on it, and it is the cheapest honest
            /// answer to "the cube does not react": no recolour, no flash, no white.
            /// </summary>
            public static float FoilWidth = 0.22f;

            public static float FoilStrength = 0.5f;

            /// <summary>A short warm rim round the cube as the value leaves it, and the residue
            /// that is still there for a moment after the payout has gone. A cube that snaps back
            /// to normal the frame the number leaves says the event never touched it.</summary>
            public static float EdgeStrength = 0.45f;

            public static float Afterglow = 0.22f;

            public static float GlintStrength = 0.75f;

            // ---- the number ----
            /// <summary>Character size of a per-cube amount, and of the total beside the score.
            /// </summary>
            /// <summary>Character size at font 90, so the world height is 9x this. The game's
            /// own popups sit at 0.51 (combo), 0.65 (dynamite) and 0.80 (clean sweep) world units
            /// - a payout that happens every turn belongs in that family, not under it.</summary>
            /// <summary>
            /// COMPACT. At 0.08 this was 78 pixels of "+2" thrown a long way off the hand, which
            /// reads as an announcement rather than as a coin coming out of a cube - and it
            /// crossed the card's own header on the way. The amount is a LABEL on a small thing;
            /// the TOTAL beside the score is the announcement.
            /// </summary>
            public static float ValueSize = 0.054f;

            public static float TotalSize = 0.072f;

            /// <summary>Birth: 0.65 to 1.12 and back to 1. No bounce after it - one punch.
            /// </summary>
            public static float Birth = 0.055f;

            public static float Settle = 0.05f;

            /// <summary>How long the amount stays legible before it starts becoming a seed.
            /// </summary>
            public static float Hold = 0.15f;

            /// <summary>How far ABOVE THE CARD'S TOP EDGE the amount parks, and how far it is
            /// allowed to fan sideways. It stays in the hand's own region: a number that climbs
            /// over the board has left the thing it is talking about behind.</summary>
            public static float Rise = 0.16f;

            public static float Fan = 0.2f;

            /// <summary>Shrinking into the seed.</summary>
            public static float Shrink = 0.18f;

            // ---- the flecks ----
            public static int FlecksPerCube = 3;

            public static float FleckSize = 0.14f;

            public static float FleckReach = 0.55f;

            public static float FleckLife = 0.42f;

            /// <summary>The whole effect's ceiling, however much gold is held.</summary>
            public static int FleckCap = 26;

            /// <summary>A four-point spark, once or twice in a payout and never more.</summary>
            public static int SparkCap = 2;

            public static float SparkSize = 0.3f;

            public static float SparkLife = 0.12f;

            // ---- the collection ----
            /// <summary>
            /// THE SEED IS THE ONLY THING THE PLAYER FOLLOWS ACROSS THE SCREEN, so it is the last
            /// thing that may be small. At 0.055 it was six pixels and at 0.17 it was eighteen -
            /// a bead that size, moving fast, over a busy board, is not something an eye tracks.
            /// A third of a world unit is about thirty-five pixels: the size of the cube it came
            /// out of, which is the point.
            /// </summary>
            /// <summary>
            /// THE ESSENCE IS A FACETED SHARD, not a blurred dot - and that distinction is the
            /// whole of what was wrong with the first pass. Three soft yellow dots drifting over
            /// the middle of the board look like a bug: they have no shape, no direction and no
            /// obvious connection to anything. A small hard silhouette with a glow BEHIND it, on
            /// a ribbon, is a thing being carried somewhere.
            /// </summary>
            public static float EssenceSize = 0.22f;

            /// <summary>The soft halo behind that shard, as a multiple of it. Under two: at more
            /// the glow IS the object again.</summary>
            public static float EssenceGlow = 1.8f;

            /// <summary>How far it stretches toward the score as it launches, which is what makes
            /// it read as being pulled rather than teleporting.</summary>
            public static float EssenceStretch = 0.3f;

            /// <summary>The ribbon it leaves: how much of its own curve is drawn behind it, and
            /// how wide that is at the head.</summary>
            public static float RibbonLength = 0.16f;

            public static float RibbonWidth = 0.075f;

            /// <summary>The flight, and how far it bows off the straight line. Slow, then fast:
            /// the acceleration is what makes it read as being PULLED in.</summary>
            public static float FlightNear = 0.24f;

            public static float FlightFar = 0.42f;

            public static float Bow = 0.22f;

            /// <summary>How far behind the seed the trail motes sit, along its own curve.
            /// </summary>
            public static float Trail = 0.09f;

            // ---- the score's answer ----
            /// <summary>How long the score stays warm after a seed lands, and how hard it moves.
            /// One answer for the payout, not one per seed.</summary>
            public static float Warmth = 0.22f;

            public static float Punch = 0.08f;

            /// <summary>When the total appears: once this share of the seeds has landed.</summary>
            public static float TotalAt = 0.7f;

            public static float TotalHold = 0.12f;

            public static float TotalFade = 0.18f;

            // ---- how much is too much ----
            /// <summary>Above this many cubes the numbers stop being one each: the cubes all still
            /// wake and still pay, but the amounts are gathered into one per CARD. Twenty "+2"s on
            /// the hand is not a richer payout, it is a screen nobody can read.</summary>
            public static int SubtotalAbove = 14;

            /// <summary>And even below it, no more than this many numbers are in the air at
            /// once. Eight rather than ten now that a number is a real size.</summary>
            public static int VisibleValues = 8;
        }

        /// <summary>
        /// WHERE IT DRAWS, and it has to be ABOVE THE CARDS - which is not where it started.
        ///
        /// The payout happens ON the hand, so every part of it overlaps a card, and the first
        /// pass sat at 44-46: over a resting hand card (which tops out at 29) but UNDER a card
        /// shown over the hand, which is 49-59 (CardLayerView.FxOrder) - and that is exactly
        /// what the animation lab lays its own gold hand out with. So in the one place built to
        /// look at this effect, the effect was behind the cards.
        ///
        /// 80-84 clears everything drawn in world space: the hand and its flourishes (59), the
        /// block gallery (63) and the boss stage's own furniture (up to 77). It is a payout that
        /// lasts eight tenths of a second and it is always meant to be read.
        /// </summary>
        private const int GlintOrder = 80;

        private const int FleckOrder = 81;

        private const int SeedOrder = 82;

        private const int ValueOrder = 83;

        private const int MarkOrder = 84;

        /// <summary>What the lab can take away, one layer at a time.</summary>
        public static class Layers
        {
            public static bool ShowCubeWake = true;

            public static bool ShowValuePopups = true;

            public static bool ShowFlecks = true;

            /// <summary>The foil highlight running across a cube, and the ribbon behind the
            /// essence - the two layers this corrective pass added, switchable on their own
            /// because both were added to answer "it does not react" and "it is three dots".
            /// </summary>
            public static bool ShowFoilSweep = true;

            public static bool ShowRibbon = true;

            public static bool ShowEssence = true;

            public static bool ShowTotal = true;

            /// <summary>DEV: draw a mark on every source cube the report named, and on the score
            /// target - the fastest way to see that the payout is coming off the HAND and not off
            /// the board.</summary>
            public static bool ShowSourceAnchors = false;

            /// <summary>DEV: force the crowded layout at any count.</summary>
            public static bool ForceSubtotals = false;

            public static void AllOn()
            {
                ShowCubeWake = true;
                ShowValuePopups = true;
                ShowFlecks = true;
                ShowFoilSweep = true;
                ShowRibbon = true;
                ShowEssence = true;
                ShowTotal = true;
                ShowSourceAnchors = false;
                ForceSubtotals = false;
            }
        }

        // =================================================================== the pieces

        private sealed class Pay
        {
            public Vector2 At;
            public Vector2 Drift;
            public int Amount;
            public float Start;
            public TextMesh Text;
            public SpriteRenderer Seed;
            public Vector2 SeedFrom;
            public Vector2 Control;

            /// <summary>When its number stopped being read, or -1 while it still is.</summary>
            public float SeedStart = -1f;
            public float Flight;
            public bool Landed;
            public bool Big;

            /// <summary>The soft halo BEHIND the shard, and the ribbon it leaves along its own
            /// curve. Two separate motes trailing a third was the "three yellow dots" - a tapered
            /// strip is one moving thing.</summary>
            public SpriteRenderer Glow;

            public TalismanTendril Ribbon;
        }

        /// <summary>One gold cube answering: a warmth in its middle, a foil highlight running
        /// across it, and a rim that is still faintly lit after the value has gone.</summary>
        private sealed class Glint
        {
            public SpriteRenderer Body;
            public SpriteRenderer Foil;
            public SpriteRenderer Rim;
            public Vector2 At;
            public float Start;
            public float Size;
        }

        private sealed class Speck
        {
            public SpriteRenderer Body;
            public Vector2 At;
            public Vector2 Dir;
            public float Start;
            public float Life;
            public float Size;
            public float Spin;
        }

        private readonly List<Pay> pays = new List<Pay>();

        private readonly List<Glint> glints = new List<Glint>();

        private readonly List<Speck> specks = new List<Speck>();

        private readonly List<SpriteRenderer> marks = new List<SpriteRenderer>();

        private readonly Stack<SpriteRenderer> spare = new Stack<SpriteRenderer>();

        private readonly Stack<TextMesh> spareText = new Stack<TextMesh>();

        private Vector2 scoreAt;

        /// <summary>Where the TOTAL may be written. Not derived from the score: the score line
        /// lives at the top of the screen, so "just above it" is off the screen - which is
        /// exactly where the first version put it. Whoever knows the camera works it out.
        /// </summary>
        private Vector2 totalSpot;

        private float clock = -1f;

        private float span;

        private int lastSerial = -1;

        private TextMesh total;

        private float totalAt = -1f;

        private int landed;

        private int seeds;

        /// <summary>
        /// HOW WARM THE SCORE IS, 0 to 1 - what the HUD reads to answer a payout.
        ///
        /// The view does not touch the score text: that is UI on a canvas the effect knows
        /// nothing about, and reaching into it from here is how two systems end up fighting over
        /// one transform. It reports a number instead.
        /// </summary>
        public float ScoreWarm { get; private set; }

        /// <summary>True while anything is playing - what the lab waits on.</summary>
        public bool Busy
        {
            get { return clock >= 0f; }
        }

        /// <summary>
        /// Plays one turn's payout. <paramref name="spots"/> must line up with report.Sources.
        ///
        /// Keyed on the report's SERIAL: the game asks every repaint, and a payout must not
        /// restart because the hand was redrawn behind it.
        /// </summary>
        public void Play(MidasPayoutVisuals report, List<SourceSpot> spots, Vector2 score,
            Vector2 totalPlace)
        {
            if (report == null || !report.Any || spots == null || report.Serial == lastSerial)
            {
                return;
            }
            lastSerial = report.Serial;
            Stop();
            scoreAt = score;
            totalSpot = totalPlace;

            int cubes = report.GoldCubes;
            bool crowded = Layers.ForceSubtotals || cubes > Style.SubtotalAbove;
            float stagger = cubes > 8 ? Style.StaggerTight : Style.Stagger;
            int flecks = 0;
            int sparks = 0;
            int order = 0;
            float last = 0f;

            for (int i = 0; i < spots.Count && i < report.Sources.Count; i++)
            {
                SourceSpot spot = spots[i];
                MidasGoldSource source = report.Sources[i];
                if (spot.Cubes == null || spot.Cubes.Count == 0)
                {
                    continue;
                }
                for (int k = 0; k < spot.Cubes.Count; k++)
                {
                    float when = order * stagger;
                    order++;
                    last = Mathf.Max(last, when);
                    if (Layers.ShowCubeWake)
                    {
                        glints.Add(new Glint
                        {
                            At = spot.Cubes[k],
                            Start = when,
                            Size = spot.CubeSize > 0.001f ? spot.CubeSize : 0.2f
                        });
                    }
                    if (Layers.ShowFlecks && flecks < Style.FleckCap)
                    {
                        flecks += Scatter(spot.Cubes[k], when + Style.Wake, cubes, ref sparks);
                    }
                    if (crowded || !Layers.ShowValuePopups)
                    {
                        continue;
                    }
                    // ONE NUMBER PER CUBE, up to the point where numbers stop being readable.
                    pays.Add(Value(spot.Cubes[k], spot.Card.y + spot.CardHalf,
                        report.PointsPerGoldCube, when + Style.Wake * 0.7f, order, false));
                }
                if (!crowded || !Layers.ShowValuePopups)
                {
                    continue;
                }
                // CROWDED: the cubes still wake and still spark - what is gathered is only the
                // READING of it, one amount per card, so the hand is not a wall of "+2".
                pays.Add(Value(spot.Card, spot.Card.y + spot.CardHalf, source.Subtotal,
                    order * stagger + Style.Wake, order, true));
            }

            seeds = pays.Count;
            span = last + Style.Wake + Style.Hold + Style.Shrink + Style.FlightFar + 0.35f;
            clock = 0f;
            landed = 0;
            ScoreWarm = 0f;
            totalAt = -1f;
            if (Layers.ShowSourceAnchors)
            {
                Anchors(spots, score);
            }
            if (pays.Count == 0 && Layers.ShowTotal)
            {
                // Everything was switched off except the total - the lab does that, and it must
                // still see something arrive.
                totalAt = span * 0.4f;
            }
            totalTotal = report.TotalScore;
        }

        private int totalTotal;

        private Pay Value(Vector2 at, float cardTop, int amount, float start, int order,
            bool big)
        {
            // THE FAN IS DETERMINISTIC, not random: the same hand pays out the same way twice,
            // and a number that jitters between turns reads as a bug rather than as life.
            float side = ((order * 37) % 100) / 100f - 0.5f;
            // IT PARKS JUST CLEAR OF THE CARD, not a fixed distance above the cube. A fixed rise
            // puts a bottom-row cube's number over the card's own element band and a top-row
            // cube's number out over the board - the two failures in the same line of code.
            float rise = Mathf.Max(Style.Rise * Style.Scale,
                cardTop - at.y + Style.Rise * Style.Scale);
            return new Pay
            {
                At = at,
                Amount = amount,
                Start = start,
                Big = big,
                Drift = new Vector2(side * 2f * Style.Fan * Style.Scale, rise),
                Flight = Mathf.Lerp(Style.FlightNear, Style.FlightFar,
                    Mathf.Clamp01(Vector2.Distance(at, scoreAt) / 9f))
            };
        }

        private int Scatter(Vector2 at, float when, int cubes, ref int sparks)
        {
            int n = cubes <= 4 ? Style.FlecksPerCube
                : cubes <= 8 ? 2
                : cubes <= 16 ? 1
                : (at.x * 7f) % 1f > 0.5f ? 1 : 0;
            for (int i = 0; i < n; i++)
            {
                float a = (i * 2.39996f + at.x * 3.1f + at.y * 1.7f) % 6.2832f;
                specks.Add(new Speck
                {
                    At = at,
                    Dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f + 0.35f),
                    Start = when,
                    Life = Style.FleckLife,
                    Size = Style.FleckSize * Style.Scale * (0.7f + ((i * 13) % 7) / 10f),
                    Spin = 40f + ((i * 29) % 80)
                });
            }
            // A SPARK IS RARE. Two in a payout, never a shower - the moment it is common it is
            // glitter, and glitter is the casino this must not become.
            if (sparks < Style.SparkCap && ((int)(at.x * 31f + at.y * 17f) % 5) == 0)
            {
                sparks++;
                specks.Add(new Speck
                {
                    At = at,
                    Dir = Vector2.zero,
                    Start = when,
                    Life = Style.SparkLife,
                    Size = Style.SparkSize * Style.Scale,
                    Spin = 0f
                });
            }
            return n;
        }

        private void Anchors(List<SourceSpot> spots, Vector2 score)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                if (spots[i].Cubes == null)
                {
                    continue;
                }
                for (int k = 0; k < spots[i].Cubes.Count; k++)
                {
                    SpriteRenderer m = Rent(MidasShapes.Shard, MarkOrder);
                    m.transform.localPosition = spots[i].Cubes[k];
                    Fit(m, 0.12f, 0.12f);
                    m.color = new Color(0.2f, 1f, 0.3f, 0.9f);
                    marks.Add(m);
                }
            }
            SpriteRenderer target = Rent(MidasShapes.Star, MarkOrder);
            target.transform.localPosition = score;
            Fit(target, 0.3f, 0.3f);
            target.color = new Color(0.3f, 0.8f, 1f, 0.9f);
            marks.Add(target);
        }

        public void Stop()
        {
            for (int i = 0; i < pays.Count; i++)
            {
                Return(pays[i].Text);
                Return(pays[i].Seed);
                Return(pays[i].Glow);
                ReturnRibbon(pays[i].Ribbon);
                pays[i].Ribbon = null;
            }
            pays.Clear();
            for (int i = 0; i < glints.Count; i++)
            {
                Return(glints[i].Body);
                Return(glints[i].Foil);
                Return(glints[i].Rim);
            }
            glints.Clear();
            for (int i = 0; i < specks.Count; i++)
            {
                Return(specks[i].Body);
            }
            specks.Clear();
            for (int i = 0; i < marks.Count; i++)
            {
                Return(marks[i]);
            }
            marks.Clear();
            Return(total);
            total = null;
            clock = -1f;
            ScoreWarm = 0f;
        }

        // =================================================================== the clock

        private void Update()
        {
            if (clock < 0f)
            {
                return;
            }
            clock += Time.deltaTime;
            PaintGlints();
            PaintSpecks();
            PaintPays();
            PaintTotal();
            // The score's warmth decays on its own, so a cascade of arrivals tops it up rather
            // than each one starting a new animation.
            ScoreWarm = Mathf.Max(0f, ScoreWarm - Time.deltaTime / Style.Warmth);
            if (clock > span)
            {
                Stop();
            }
        }

        private void PaintGlints()
        {
            for (int i = 0; i < glints.Count; i++)
            {
                Glint g = glints[i];
                float life = Style.Wake + Style.Afterglow;
                float k = Span(clock, g.Start, g.Start + life);
                if (k <= 0f || k >= 1f)
                {
                    if (g.Body != null)
                    {
                        Return(g.Body);
                        Return(g.Foil);
                        Return(g.Rim);
                        g.Body = null;
                        g.Foil = null;
                        g.Rim = null;
                    }
                    continue;
                }
                if (g.Body == null)
                {
                    g.Body = Rent(MidasShapes.Mote, GlintOrder);
                    g.Foil = Rent(MidasShapes.Mote, GlintOrder);
                    g.Rim = Rent(MidasShapes.Rim, GlintOrder);
                }
                float cube = Mathf.Max(g.Size, Style.GlintFloor / Mathf.Max(Style.GlintSize, 1f));
                float wake = Span(clock, g.Start, g.Start + Style.Wake);

                // THE WARMTH in its middle - in and out over its own window. A warmth that stays
                // is a recolour, and a gold cube has to still look like the gold cube it was.
                float on = Mathf.Sin(wake * Mathf.PI);
                float size = Mathf.Max(Style.GlintSize * g.Size, Style.GlintFloor)
                    * Style.Scale * (0.7f + 0.5f * on);
                g.Body.transform.localPosition = g.At;
                Fit(g.Body, size, size);
                g.Body.color = new Color(Style.Ivory.r, Style.Ivory.g, Style.Ivory.b,
                    Style.GlintStrength * on);

                // THE FOIL SWEEP, low-left to high-right, once. Thin, warm, and gone - this is
                // the piece that says the metal is doing something rather than being lit.
                float travel = Mathf.Lerp(-0.62f, 0.62f, wake);
                var along = new Vector2(0.707f, 0.707f);
                g.Foil.transform.localPosition = g.At + along * (travel * cube * Style.Scale);
                g.Foil.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                Fit(g.Foil, cube * Style.FoilWidth * Style.Scale, cube * 1.5f * Style.Scale);
                g.Foil.color = new Color(Style.Ivory.r, Style.Ivory.g, Style.Ivory.b,
                    Layers.ShowFoilSweep
                        ? Style.FoilStrength * Mathf.Sin(wake * Mathf.PI)
                        : 0f);

                // THE RIM, and the residue after it: the cube is still faintly warm for a beat
                // once its value has gone, instead of snapping back the frame the number leaves.
                float after = Span(clock, g.Start + Style.Wake, g.Start + life);
                float rim = Mathf.Sin(wake * Mathf.PI) * Style.EdgeStrength;
                if (after > 0f)
                {
                    rim = Style.EdgeStrength * 0.5f * (1f - after);
                }
                g.Rim.transform.localPosition = g.At;
                float rimSize = cube * 1.12f * Style.Scale;
                Fit(g.Rim, rimSize, rimSize);
                g.Rim.color = new Color(Style.Gold.r, Style.Gold.g, Style.Gold.b, rim);
            }
        }

        private void PaintSpecks()
        {
            for (int i = 0; i < specks.Count; i++)
            {
                Speck s = specks[i];
                float k = Span(clock, s.Start, s.Start + s.Life);
                if (k <= 0f || k >= 1f)
                {
                    if (s.Body != null)
                    {
                        Return(s.Body);
                        s.Body = null;
                    }
                    continue;
                }
                if (s.Body == null)
                {
                    s.Body = Rent(s.Dir == Vector2.zero ? MidasShapes.Star : MidasShapes.Shard, FleckOrder);
                }
                // Out fast, then almost stopping - gold has weight.
                float ease = 1f - (1f - k) * (1f - k);
                Vector2 at = s.At + s.Dir * (Style.FleckReach * Style.Scale * ease);
                s.Body.transform.localPosition = at;
                s.Body.transform.localRotation = Quaternion.Euler(0f, 0f, s.Spin * ease);
                float size = s.Size * (1f - k * 0.5f);
                Fit(s.Body, size, size);
                Color tint = s.Dir == Vector2.zero ? Style.Ivory
                    : (i % 3 == 0 ? Style.Amber : Style.Gold);
                s.Body.color = new Color(tint.r, tint.g, tint.b, 1f - k * k);
            }
        }

        private void PaintPays()
        {
            int visible = 0;
            for (int i = 0; i < pays.Count; i++)
            {
                Pay p = pays[i];
                float birth = Span(clock, p.Start, p.Start + Style.Birth);
                float read = p.Start + Style.Birth + Style.Settle + Style.Hold;
                float gone = read + Style.Shrink;
                if (birth <= 0f)
                {
                    continue;
                }
                if (clock < gone)
                {
                    visible++;
                    // Over the cap the number is simply not drawn - its cube still woke, still
                    // sparked and still pays: what is dropped is the READING, never the money.
                    if (visible > Style.VisibleValues && !p.Big)
                    {
                        continue;
                    }
                    if (p.Text == null)
                    {
                        p.Text = RentText((p.Big ? Style.TotalSize * 0.86f : Style.ValueSize)
                            * Style.Scale);
                        p.Text.text = "+" + p.Amount;
                    }
                    float settle = Span(clock, p.Start + Style.Birth,
                        p.Start + Style.Birth + Style.Settle);
                    float scale = birth < 1f ? Mathf.Lerp(0.65f, 1.12f, birth)
                        : Mathf.Lerp(1.12f, 1f, settle);
                    float drift = Span(clock, p.Start, read);
                    float fade = Span(clock, read, gone);
                    scale *= Mathf.Lerp(1f, 0.7f, fade);
                    Vector2 at = p.At + p.Drift * Ease(drift);
                    p.SeedFrom = at;
                    p.Text.transform.localPosition = at;
                    p.Text.transform.localScale = new Vector3(scale, scale, 1f);
                    p.Text.transform.localRotation = Quaternion.Euler(0f, 0f,
                        ((i * 17) % 7 - 3) * 0.9f);
                    Color ink = Style.Ivory;
                    p.Text.color = new Color(ink.r, ink.g, ink.b, Mathf.Lerp(1f, 0.55f, fade));
                    continue;
                }
                if (p.SeedStart < 0f)
                {
                    // The moment its reading is over - whether it was ever drawn or not. A payout
                    // held back by the visible cap still EARNED its money, and if its seed is not
                    // started here it arrives at the score before it ever left the hand.
                    Return(p.Text);
                    p.Text = null;
                    p.SeedStart = clock;
                }
                if (!Layers.ShowEssence)
                {
                    Land(p);
                    continue;
                }
                // THE SEED. The number was the reward being READ; this is it being COLLECTED.
                float fly = Span(clock, p.SeedStart, p.SeedStart + p.Flight);
                if (p.Seed == null && fly < 1f)
                {
                    // A SHARD, not a soft dot - with the glow BEHIND it rather than instead of
                    // it. Three blurred yellow circles drifting over the board was the whole
                    // complaint, and the shape is most of the answer.
                    p.Glow = Rent(MidasShapes.Mote, SeedOrder - 1);
                    p.Seed = Rent(MidasShapes.Shard, SeedOrder);
                    p.Ribbon = RentRibbon();
                    // ONE CLEAN ARC toward the score. Perpendicular to its own run and scaled by
                    // it, so a short trip bends a little and a long one bends more - never an
                    // S-curve, and never a detour through the middle of the board.
                    Vector2 mid = (p.SeedFrom + scoreAt) * 0.5f;
                    Vector2 away = scoreAt - p.SeedFrom;
                    p.Control = mid + new Vector2(-away.y, away.x).normalized
                        * (Style.Bow * away.magnitude * 0.25f);
                }
                if (fly >= 1f)
                {
                    Land(p);
                    continue;
                }
                // Slow, then fast: it is being pulled in rather than thrown.
                float k = fly * fly * (3f - 2f * fly) * 0.35f + fly * fly * 0.65f;
                Vector2 pos = Bezier(p.SeedFrom, p.Control, scoreAt, k);
                Vector2 back1 = Bezier(p.SeedFrom, p.Control, scoreAt, Mathf.Max(0f, k - 0.04f));
                Vector2 heading = pos - back1;
                float size = Style.EssenceSize * Style.Scale * (1f - 0.2f * k);
                p.Seed.transform.localPosition = pos;
                // IT STRETCHES ALONG ITS OWN HEADING as it goes, hardest at the launch - which
                // is what makes it read as being pulled rather than as teleporting one step at
                // a time.
                float stretch = 1f + Style.EssenceStretch * (1f - k);
                if (heading.sqrMagnitude > 1e-6f)
                {
                    p.Seed.transform.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.Atan2(heading.y, heading.x) * Mathf.Rad2Deg);
                }
                Fit(p.Seed, size * stretch, size / stretch);
                p.Seed.color = new Color(Style.Ivory.r, Style.Ivory.g, Style.Ivory.b, 1f);
                p.Glow.transform.localPosition = pos;
                float glow = size * Style.EssenceGlow;
                Fit(p.Glow, glow, glow);
                p.Glow.color = new Color(Style.Gold.r, Style.Gold.g, Style.Gold.b, 0.5f);
                // AND THE RIBBON: the last stretch of its OWN curve, tapering to nothing. Not a
                // beam and not a line - what is left behind something moving.
                p.Ribbon.Ribbon(p.SeedFrom, p.Control, scoreAt,
                    Style.RibbonWidth * Style.Scale, 0.9f,
                    Mathf.Max(0f, k - Style.RibbonLength), k,
                    new Color(Style.Amber.r, Style.Amber.g, Style.Amber.b,
                        Layers.ShowRibbon ? 0.75f : 0f));
            }
        }

        private void Land(Pay p)
        {
            if (p.Landed)
            {
                return;
            }
            p.Landed = true;
            landed++;
            Return(p.Seed);
            Return(p.Glow);
            p.Seed = null;
            p.Glow = null;
            ReturnRibbon(p.Ribbon);
            p.Ribbon = null;
            // ONE ANSWER, TOPPED UP. Every seed adds warmth rather than starting its own bump, so
            // twelve arrivals are a cascade into one response instead of twelve bounces.
            ScoreWarm = Mathf.Min(1f, ScoreWarm + 0.55f);
            if (totalAt < 0f && seeds > 0 && landed >= Mathf.CeilToInt(seeds * Style.TotalAt))
            {
                totalAt = clock;
            }
        }

        private void PaintTotal()
        {
            if (!Layers.ShowTotal || totalAt < 0f || totalTotal <= 0)
            {
                return;
            }
            float k = clock - totalAt;
            float life = Style.Birth + Style.Settle + Style.TotalHold + Style.TotalFade;
            if (k > life)
            {
                Return(total);
                total = null;
                return;
            }
            if (total == null)
            {
                total = RentText(Style.TotalSize * Style.Scale);
                total.text = "+" + totalTotal;
            }
            float birth = Span(k, 0f, Style.Birth);
            float settle = Span(k, Style.Birth, Style.Birth + Style.Settle);
            float fade = Span(k, life - Style.TotalFade, life);
            float scale = birth < 1f ? Mathf.Lerp(0.75f, 1.12f, birth)
                : Mathf.Lerp(1.12f, 1f, settle);
            // It drifts AWAY FROM THE SCORE rather than upward: on a HUD whose score is at
            // the top, up is off the screen.
            Vector2 away = totalSpot - scoreAt;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : new Vector2(0f, -1f);
            total.transform.localPosition = totalSpot + away * (0.25f * fade * Style.Scale);
            total.transform.localScale = new Vector3(scale, scale, 1f);
            total.color = new Color(Style.Gold.r, Style.Gold.g, Style.Gold.b, 1f - fade);
        }

        // =================================================================== helpers

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
            k = Mathf.Clamp01(k);
            return k * k * (3f - 2f * k);
        }

        private static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
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

        private SpriteRenderer Rent(Sprite sprite, int order)
        {
            SpriteRenderer r;
            if (spare.Count > 0)
            {
                r = spare.Pop();
            }
            else
            {
                var go = new GameObject("MidasFx");
                go.transform.SetParent(transform, false);
                r = go.AddComponent<SpriteRenderer>();
            }
            r.sprite = sprite;
            r.sortingOrder = order;
            r.transform.localRotation = Quaternion.identity;
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
            spare.Push(r);
        }

        /// <summary>The ribbons are pooled like everything else - a payout can want a dozen
        /// and this runs every turn.</summary>
        private readonly Stack<TalismanTendril> spareRibbons = new Stack<TalismanTendril>();

        /// <summary>
        /// The soft tapered strip is TalismanTendril's, and it is used here rather than written
        /// again: it is a generic three-row mesh with its softness in the vertices, the name is
        /// only where it was first needed, and two copies of a mesh builder is two places for a
        /// seam to appear.
        /// </summary>
        private TalismanTendril RentRibbon()
        {
            TalismanTendril r = spareRibbons.Count > 0
                ? spareRibbons.Pop()
                : TalismanTendril.Make(transform, FleckOrder);
            r.Order(FleckOrder);
            r.Hide();
            return r;
        }

        private void ReturnRibbon(TalismanTendril r)
        {
            if (r == null)
            {
                return;
            }
            r.Hide();
            spareRibbons.Push(r);
        }

        private TextMesh RentText(float size)
        {
            TextMesh t;
            if (spareText.Count > 0)
            {
                t = spareText.Pop();
                t.gameObject.SetActive(true);
            }
            else
            {
                t = ViewUtil.MakeText3D(transform, "MidasValue", Vector2.zero, string.Empty,
                    90, size, Style.Ivory, ValueOrder, TextAnchor.MiddleCenter);
            }
            t.characterSize = size;
            t.fontStyle = FontStyle.Bold;
            return t;
        }

        private void Return(TextMesh t)
        {
            if (t == null)
            {
                return;
            }
            t.gameObject.SetActive(false);
            spareText.Push(t);
        }

    }

    /// <summary>
    /// The three small silhouettes the payout is made of, baked once: a soft MOTE (the glint in a
    /// cube and the seed that flies), a small SHARD (a fleck of gold, cut rather than round) and a
    /// four-point STAR (the rare spark). Kept out of the view so the shapes are one thing and the
    /// timing another.
    /// </summary>
    internal static class MidasShapes
    {
        private const int Pixels = 48;

        private static Sprite mote;

        private static Sprite shard;

        private static Sprite star;

        public static Sprite Mote
        {
            get
            {
                if (mote == null)
                {
                    mote = Bake("MidasMote", delegate (float x, float y)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        // A soft falloff with a dense middle - a hard disc is a dot, and a dot in
                        // this game is a status light.
                        return Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                    });
                }
                return mote;
            }
        }

        public static Sprite Shard
        {
            get
            {
                if (shard == null)
                {
                    shard = Bake("MidasShard", delegate (float x, float y)
                    {
                        // A squat diamond: metal breaks into facets, not into circles.
                        float d = Mathf.Abs(x) * 1.15f + Mathf.Abs(y) * 0.85f;
                        return d <= 0.82f ? 1f : Mathf.Clamp01((0.98f - d) / 0.16f);
                    });
                }
                return shard;
            }
        }

        /// <summary>A rounded-square RING - the warm rim a cube wears while its value is
        /// leaving, and the residue after. Drawn as a shape rather than as a scaled copy of the
        /// cube, so it can never be mistaken for a second cube.</summary>
        public static Sprite Rim
        {
            get
            {
                if (rim == null)
                {
                    rim = Bake("MidasRim", delegate (float x, float y)
                    {
                        float d = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
                        // Between 0.72 and 0.94 of the box, soft on both sides.
                        float inner = Mathf.Clamp01((d - 0.72f) / 0.12f);
                        float outer = Mathf.Clamp01((0.94f - d) / 0.1f);
                        return Mathf.Min(inner, outer);
                    });
                }
                return rim;
            }
        }

        private static Sprite rim;

        public static Sprite Star
        {
            get
            {
                if (star == null)
                {
                    star = Bake("MidasStar", delegate (float x, float y)
                    {
                        // Four points: two crossed tapers, soft at their tips.
                        float ax = Mathf.Abs(x);
                        float ay = Mathf.Abs(y);
                        float arm = Mathf.Max(1f - (ax + ay * 5.5f), 1f - (ay + ax * 5.5f));
                        return Mathf.Clamp01(arm) * Mathf.Clamp01(arm);
                    });
                }
                return star;
            }
        }

        private delegate float Field(float x, float y);

        private static Sprite Bake(string name, Field field)
        {
            var tex = new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[Pixels * Pixels];
            float step = 2f / Pixels;
            for (int j = 0; j < Pixels; j++)
            {
                float y = -1f + (j + 0.5f) * step;
                for (int i = 0; i < Pixels; i++)
                {
                    float x = -1f + (i + 0.5f) * step;
                    float a = Mathf.Clamp01(field(x, y));
                    pixels[j * Pixels + i] =
                        new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            Sprite made = Sprite.Create(tex, new Rect(0f, 0f, Pixels, Pixels),
                new Vector2(0.5f, 0.5f), Pixels);
            made.hideFlags = HideFlags.HideAndDontSave;
            return made;
        }
    }
}
