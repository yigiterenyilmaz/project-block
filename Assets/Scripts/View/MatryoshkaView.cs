// PURPOSE: "Matruşka"'s dolls on the board - standing on their cubes, and everything that happens to
// them: the first one arriving, a doll opening along its seam to let two smaller dolls out, the
// smallest opening on nothing, water carrying one to another cube, and the quiet end of the boss.
//
// CORE DECIDES, THIS STAGES. Every fact comes from MatruskaBoss through the turn's DollEvents: which
// doll broke, its generation, the cells its children were put on, whether it was the last
// generation, where water moved one to, whether the boss is beaten. Nothing is chosen here - no
// destination, no count, no generation - and nothing is inferred from what is on screen.
//
// THE ART IS THE PREPARED SHEET (Resources/Art/Matryoshka), cut by Tools/ArtPrep/matryoshka_sprites.py
// into a whole doll and its two shells per generation, all on one canvas so they line up by simply
// standing where the doll stands. Nothing here draws a doll. What IS generated is only what the art
// cannot carry: a contact shadow, the warm light inside an opened doll, the seam catching the light,
// a few motes of wood dust.
//
// THE ONE SENTENCE IT HAS TO SAY: "the doll opened and two smaller dolls came out of it". So the
// parent is never shrunk into its children and the children never pop in at their targets: they
// are seen rising out of the open doll and carried to their cubes, where the markers that will
// stand there have been waiting, hidden, to be handed over on the frame they land.
//
// AT REST. A resting doll is a few dozen pixels tall, so it is not made alive by moving - a pixel of
// motion is not seen. Its MATERIAL does the work (Resources/Shaders/MatryoshkaIdle: warmth under the
// lacquer, a quieter core, the seam catching light, a breathing sheen, a rare gold glint, a rim on
// the lit side) together with a small warm light on the cube under it. Motion stays, demoted to a
// supporting layer. The face takes part in none of it.
//
// TIMING. A turn's events are HELD from the refresh that follows the turn until the moment its
// cubes actually go (GameUiController releases them from PlayExplosionFeedback), so a doll opens as
// its cube breaks rather than half a second before or after. The rules are never waited on: by the
// time any of this plays, Core has long since moved on.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>The Matruşka dolls: resting markers, and the staging of what the boss did to them.</summary>
    public sealed class MatryoshkaView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ size
            /// <summary>The largest doll's height, in cells. Tall enough to read at a glance and narrow
            /// enough - the art is about three fifths as wide as it is tall - to leave the cube's own
            /// design showing on both sides.</summary>
            public static float DollHeight = 0.80f;

            /// <summary>Each generation's height against the largest, generation 1 first. Applied to
            /// the height alone, so every doll keeps its art's proportions. Smaller steps than the sheet
            /// itself takes, because at board size the sheet's smallest doll would be a speck.</summary>
            public static float[] GenerationScale = { 1.00f, 0.88f, 0.77f, 0.66f };

            /// <summary>How far below the cube's centre a doll's feet are, in cells.</summary>
            public static float BaseOffset = 0.38f;

            /// <summary>Where each generation's painted seam crosses its centre, as a share of its
            /// height from the bottom. Measured off the art by Tools/ArtPrep/matryoshka_sprites.py -
            /// rerun it and copy its numbers here if the sheet ever changes.</summary>
            public static float[] SeamHeight = { 0.351f, 0.344f, 0.331f, 0.332f };

            // ------------------------------------------------------------------ idle
            /// <summary>One breath, in seconds. Slow: eight dolls breathing fast would be a board that
            /// never sits still.</summary>
            public static float IdleBreathSeconds = 3.2f;

            /// <summary>How much taller a doll gets at the top of a breath. It narrows by half this.</summary>
            public static float IdleBreathStrength = 0.015f;

            /// <summary>How far it rises with the breath, in cells - about a pixel.</summary>
            public static float IdleFloatAmount = 0.008f;

            /// <summary>How far apart in their breathing the dolls are, as a share of a breath. Each
            /// doll's place is fixed by its cell, so they never fall into step and never shuffle either.</summary>
            public static float IdlePhaseSpread = 1f;

            /// <summary>How much of the movement above actually plays. Low on purpose: at board size a
            /// pixel of motion is not what makes a doll look alive - its material is - so motion is only
            /// the supporting layer. At this strength: under a percent of breath, half a pixel of rise.</summary>
            public static float MotionSecondaryStrength = 0.6f;

            /// <summary>How far a resting doll leans with its breath, in degrees, at full motion strength.</summary>
            public static float IdleTilt = 0.4f;

            // ------------------------------------------------------------------ idle material
            // Handed to Resources/Shaders/MatryoshkaIdle every frame. Every layer is small on its own and
            // runs on its own period, so a doll is at rest most of the time and something small is
            // always happening somewhere on the board - never everywhere at once.

            /// <summary>The warm pocket that swells and settles under the lacquer of the lower body.</summary>
            public static float BodyWarmthStrength = 0.30f;

            /// <summary>Breaths of that warmth per second - about one every four and a half seconds.</summary>
            public static float BodyWarmthSpeed = 0.22f;

            /// <summary>The pocket's size, as a share of the doll.</summary>
            public static float BodyWarmthScale = 0.42f;

            /// <summary>The quieter warmth deeper inside - the sense that something is still in there.</summary>
            public static float InnerCoreStrength = 0.22f;

            public static float InnerCoreSpeed = 0.28f;

            /// <summary>How much warmer the painted seam reads at the top of a warmth breath.</summary>
            public static float SeamIdleResponse = 0.12f;

            /// <summary>How much of the seam's surroundings that reaches, 0 to 1.</summary>
            public static float SeamIdleWidth = 0.45f;

            /// <summary>The broad soft highlight on the lacquer's upper left. It breathes where it is; it
            /// never sweeps across.</summary>
            public static float LacquerSheenStrength = 0.20f;

            public static float LacquerSheenSpeed = 0.16f;

            public static float LacquerSheenSoftness = 0.55f;

            /// <summary>One spot of the gold ornament catching the light.</summary>
            public static float GoldGlintStrength = 0.60f;

            public static float GoldGlintDuration = 0.18f;

            /// <summary>The shortest and longest quiet between one doll's glints, in seconds.</summary>
            public static float GoldGlintIntervalMin = 4f;

            public static float GoldGlintIntervalMax = 10f;

            /// <summary>The warm light on the cube round a doll's foot - what says an object is sitting
            /// here. Faint, and no wider than the doll.</summary>
            public static float PresenceLightStrength = 0.18f;

            /// <summary>That light's width against the doll's.</summary>
            public static float PresenceLightRadius = 1.0f;

            /// <summary>Its breaths per second - about one every five seconds.</summary>
            public static float PresenceLightSpeed = 0.2f;

            /// <summary>A warm edge on the side the light comes from, where the dark board would otherwise
            /// swallow the silhouette. One side only: an even edge all round is an outline.</summary>
            public static float RimStrength = 0.35f;

            /// <summary>How far in from the lit edge the rim reaches, as a share of the doll's width.</summary>
            public static float RimWidth = 0.035f;

            /// <summary>How much warmer the top and deeper the foot of the lacquer are drawn.</summary>
            public static float ColorDepthStrength = 0.08f;

            /// <summary>How far apart the dolls' material phases are. 0 puts every doll in step.</summary>
            public static float IdlePhaseVariation = 1f;

            /// <summary>Each generation's share of the warmth, generation 1 first.</summary>
            public static float[] GenerationWarmthMultiplier = { 1.00f, 0.90f, 0.80f, 0.65f };

            /// <summary>Each generation's share of the gold response.</summary>
            public static float[] GenerationGoldMultiplier = { 1.00f, 0.95f, 0.85f, 0.70f };

            /// <summary>How much inner core each generation carries: most in the largest, which still holds
            /// the most; least in the last - quieter, never dead.</summary>
            public static float[] GenerationCoreMultiplier = { 1.00f, 0.85f, 0.70f, 0.40f };

            /// <summary>Each generation's cycle speed: the largest breathes slowest.</summary>
            public static float[] GenerationCycleScale = { 0.85f, 1.00f, 1.12f, 1.20f };

            /// <summary>Three points ON each doll's gold ornament a glint may land on, in the sprite's own
            /// 0-1 space, three per generation, generation 1 first. Picked off the art by
            /// Tools/ArtPrep/matryoshka_sprites.py - copy its numbers here if the sheet changes.</summary>
            public static Vector2[] GlintAnchors =
            {
                new Vector2(0.506f, 0.441f), new Vector2(0.476f, 0.934f), new Vector2(0.101f, 0.540f),
                new Vector2(0.500f, 0.440f), new Vector2(0.474f, 0.937f), new Vector2(0.101f, 0.545f),
                new Vector2(0.497f, 0.382f), new Vector2(0.536f, 0.927f), new Vector2(0.192f, 0.539f),
                new Vector2(0.504f, 0.380f), new Vector2(0.577f, 0.921f), new Vector2(0.198f, 0.548f)
            };

            // ------------------------------------------------------------------ shadow
            public static float ShadowOpacity = 0.30f;

            /// <summary>The shadow's width against the doll's.</summary>
            public static float ShadowSize = 0.85f;

            // ------------------------------------------------------------------ arrival
            public static float SpawnDuration = 0.18f;

            /// <summary>How far above its cube the first doll starts, in cells.</summary>
            public static float SpawnLift = 0.08f;

            public static float SpawnStartScale = 0.78f;

            /// <summary>The little overshoot it settles out of - 1.03, then 1.00.</summary>
            public static float SpawnOvershoot = 0.03f;

            /// <summary>The soft warm light under it as it lands.</summary>
            public static float SpawnPulse = 0.35f;

            // ------------------------------------------------------------------ reaction
            /// <summary>A beat before a doll reacts, so the cube breaking under it is seen first. Small:
            /// the two have to read as one event.</summary>
            public static float ReactionDelay = 0.05f;

            public static float ReactionDuration = 0.10f;

            /// <summary>How far it lifts off its cube as it flinches, in cells.</summary>
            public static float ReactionLift = 0.035f;

            // ------------------------------------------------------------------ seam and opening
            /// <summary>How brightly the seam catches the light before it gives. Warm, and short - a
            /// wooden joint coming loose, not a laser.</summary>
            public static float SeamHighlightStrength = 0.55f;

            public static float SeamUnlockDuration = 0.10f;

            public static float OpenDuration = 0.12f;

            /// <summary>How far the top shell lifts, as a share of the doll's height.</summary>
            public static float OpenTopDistance = 0.16f;

            /// <summary>How far the bottom shell settles, as a share of the doll's height.</summary>
            public static float OpenBottomDistance = 0.04f;

            /// <summary>How far the top shell tips as it lifts, in degrees. A nesting doll parts; it does
            /// not swing open on a hinge.</summary>
            public static float OpenTilt = 2.5f;

            /// <summary>The warm light seen inside once it is open - there were other dolls in there.</summary>
            public static float InnerWarmGlow = 0.55f;

            // ------------------------------------------------------------------ children
            /// <summary>How long the children take to rise out of the opened doll.</summary>
            public static float ChildRevealDuration = 0.12f;

            /// <summary>Their size as they come out, against the size they land at.</summary>
            public static float ChildStartScale = 0.82f;

            /// <summary>The most a child may measure while it is still coming out, against its PARENT's
            /// height. The generations are close in size on the board, so at its own start scale a child
            /// was nearly as tall as the doll it came out of - and it has to look as if it fitted inside.</summary>
            public static float ChildInsideFit = 0.62f;

            /// <summary>The shortest and longest trip to a target, by distance: near targets are not
            /// dawdled to and far ones are not flung at.</summary>
            public static float ChildTravelDurationMin = 0.30f;

            public static float ChildTravelDurationMax = 0.60f;

            /// <summary>How far a child's path bows out, as a share of the distance it travels.</summary>
            public static float ChildArcHeight = 0.18f;

            /// <summary>How much later the second child leaves than the first.</summary>
            public static float ChildStagger = 0.045f;

            /// <summary>How far a child tips in flight, in degrees. Never a spin: it stays facing out.</summary>
            public static float ChildTilt = 4f;

            /// <summary>The faint smear behind a travelling child.</summary>
            public static float TrailOpacity = 0.22f;

            // ------------------------------------------------------------------ landing
            public static float LandingDuration = 0.16f;

            /// <summary>The settle into the cube - scale 1.03 to 1.00, and a matching dip.</summary>
            public static float LandingOvershoot = 0.03f;

            // ------------------------------------------------------------------ the parent's shell
            public static float ParentFadeDuration = 0.24f;

            /// <summary>Motes of wood dust as a shell fades. A handful: this is material, not a show.</summary>
            public static int WoodDustCount = 5;

            // ------------------------------------------------------------------ the last generation
            /// <summary>How long an opened smallest doll is seen to be empty before it goes.</summary>
            public static float TerminalEmptyHold = 0.12f;

            public static float TerminalFadeDuration = 0.24f;

            // ------------------------------------------------------------------ water
            public static float RelocationDuration = 0.28f;

            /// <summary>The height of the hop to its new cube, in cells. Low and plain on purpose: it has
            /// to look nothing like a split.</summary>
            public static float RelocationArcHeight = 0.30f;

            // ------------------------------------------------------------------ the boss beaten
            public static float CompletionHold = 0.10f;

            /// <summary>The warm wash over the arena when the last doll goes. Faint - the round's own
            /// completion is what celebrates; this is only the boss signing off.</summary>
            public static float CompletionPulseStrength = 0.08f;

            public static float CompletionDuration = 0.55f;

            // ------------------------------------------------------------------ crowd control
            /// <summary>How far apart dolls opening on the same turn start, so three at once read as
            /// three.</summary>
            public static float MultiSplitStagger = 0.05f;

            /// <summary>The most motes one turn may throw, however many dolls it breaks.</summary>
            public static int MaxMotesPerTurn = 18;

            /// <summary>How long a turn's events wait for the cubes to go before they play anyway.</summary>
            public static float HoldTimeout = 1.5f;

            // ------------------------------------------------------------------ palette
            public static Color WarmCream = new Color(1f, 0.90f, 0.72f);

            public static Color MutedGold = new Color(0.86f, 0.66f, 0.36f);

            public static Color Burgundy = new Color(0.45f, 0.10f, 0.14f);

            public static Color WoodBrown = new Color(0.42f, 0.24f, 0.16f);

            /// <summary>What an emptied shell greys toward as it fades.</summary>
            public static Color SpentTint = new Color(0.78f, 0.70f, 0.68f);

            /// <summary>The light a doll sits in: warm, leaning to gold, never a spotlight. Light has to be
            /// LIGHTER than what it falls on - a dark burgundy here only dimmed the cube under the doll.</summary>
            public static Color PresenceColor = new Color(1f, 0.64f, 0.44f);

            public static Color SheenColor = new Color(1f, 0.93f, 0.84f);

            public static Color GlintColor = new Color(1f, 0.86f, 0.56f);

            public static Color RimColor = new Color(1f, 0.64f, 0.48f);
        }

        /// <summary>The idle's layers, one switch each, for the animation lab to take them apart. All on in
        /// play; the lab puts them back when it closes.</summary>
        public static class Layers
        {
            public static bool BodyWarmth = true;
            public static bool GoldResponse = true;
            public static bool LacquerSheen = true;
            public static bool PresenceLight = true;
            public static bool RimLight = true;
            public static bool Motion = true;

            public static void AllOn()
            {
                BodyWarmth = true;
                GoldResponse = true;
                LacquerSheen = true;
                PresenceLight = true;
                RimLight = true;
                Motion = true;
            }
        }

        // =================================================================== orders
        // Above the cubes and every mark on them (infection cores run to 9), below the card layer's
        // overlays. Opening dolls climb above resting ones and travelling children above both, so a
        // split is never lost under another board effect or another doll.

        private const int PulseOrder = 21;

        private const int ShadowOrder = 22;

        private const int RestOrder = 23;

        private const int GlowOrder = 23;

        /// <summary>The lifted top shell sits BEHIND the children coming out, and the bottom shell in
        /// FRONT of them: seen from the front, a doll rising out of the cup passes in front of the raised
        /// lid and behind the cup's rim. The other way round, the lid half hid the children and an
        /// opening read as three dolls in a heap.</summary>
        private const int TopShellOrder = 24;

        private const int ChildInsideOrder = 25;

        private const int BottomShellOrder = 26;

        private const int SeamOrder = 27;

        private const int TrailOrder = 28;

        private const int TravelOrder = 29;

        private const int MoteOrder = 38;

        private const string ArtFolder = "Art/Matryoshka/";

        private const int ArtCount = 4;

        // =================================================================== state

        /// <summary>A doll as Core has it: where it is and which generation.</summary>
        public struct Mark
        {
            public GridPos Cell;
            public int Generation;

            public Mark(GridPos cell, int generation)
            {
                Cell = cell;
                Generation = generation;
            }
        }

        private sealed class Doll
        {
            public GridPos Cell;
            public int Generation;
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Shadow;

            /// <summary>The small warm light on the cube under it.</summary>
            public SpriteRenderer Presence;

            public float Phase;

            /// <summary>When it last came to rest on a cube, so its light can come up rather than appear.</summary>
            public float RestSince = -10f;

            /// <summary>A sequence owns it: it does not breathe, and a reserved one stays hidden
            /// until it is handed over.</summary>
            public bool Held;
        }

        private readonly Dictionary<GridPos, Doll> resting = new Dictionary<GridPos, Doll>();

        /// <summary>Where Core says the dolls are. What every sequence settles onto when it ends.</summary>
        private readonly List<Mark> truth = new List<Mark>();

        private List<DollEvent> pending;

        private readonly List<Mark> pendingAfter = new List<Mark>();

        private float pendingSince;

        /// <summary>Sequences still in flight. Nothing is reconciled while any is.</summary>
        private int running;

        private int lastGeneration = ArtCount;

        private GameBoard board;

        private Func<GridPos, Vector2> toWorld;

        private float cellSize = 1f;

        private int motesThisTurn;

        private int travelSlot;

        private readonly List<SpriteRenderer> live = new List<SpriteRenderer>();

        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        /// <summary>True while a turn is waiting to be staged or is being staged.</summary>
        public bool Busy
        {
            get { return running > 0 || pending != null; }
        }

        // =================================================================== driving it

        /// <summary>
        /// The dolls as they stand, drawn without ceremony. While a turn is being staged this only
        /// records the answer - the sequence settles onto it when it ends, so a repaint in the middle
        /// of a split cannot put the children on their cubes before they have got there.
        /// </summary>
        public void Show(IReadOnlyList<Mark> dolls, int generations, GameBoard onBoard,
            Func<GridPos, Vector2> cellToWorld, float cell)
        {
            Bind(generations, onBoard, cellToWorld, cell);
            Copy(dolls, truth);
            if (!Busy)
            {
                Reconcile();
            }
        }

        /// <summary>
        /// A turn's events and where the dolls end up, held until Release - the moment the cubes
        /// under them go. The board keeps the picture it had until then.
        ///
        /// A turn that arrives while the last one is still playing first snaps that one to its end:
        /// its dolls are exactly where this turn found them, and a child still in the air cannot be
        /// asked to open.
        /// </summary>
        public void Hold(IReadOnlyList<DollEvent> events, IReadOnlyList<Mark> after, int generations,
            GameBoard onBoard, Func<GridPos, Vector2> cellToWorld, float cell)
        {
            Bind(generations, onBoard, cellToWorld, cell);
            if (running > 0)
            {
                FinishAll();
            }
            if (pending != null)
            {
                Copy(pendingAfter, truth);
                pending = null;
                Reconcile();
            }
            pending = new List<DollEvent>(events);
            Copy(after, pendingAfter);
            pendingSince = Time.unscaledTime;
        }

        /// <summary>Plays what Hold is holding. Harmless when nothing is.</summary>
        public void Release()
        {
            if (pending == null)
            {
                return;
            }
            List<DollEvent> events = pending;
            pending = null;
            Copy(pendingAfter, truth);
            Stage(events);
        }

        /// <summary>Every doll and every effect gone at once - a dark board, a round with no Matruşka.</summary>
        public void Clear()
        {
            pending = null;
            truth.Clear();
            FinishAll();
        }

        // =================================================================== staging

        private void Stage(List<DollEvent> events)
        {
            motesThisTurn = 0;
            int openings = 0;
            float lastOpeningEnds = 0f;
            GridPos lastOpened = default(GridPos);
            bool beaten = false;
            for (int i = 0; i < events.Count; i++)
            {
                DollEvent e = events[i];
                switch (e.Kind)
                {
                    case DollEventKind.Arrived:
                        StartCoroutine(Arrive(Reserve(e.Cell, e.Generation)));
                        break;

                    case DollEventKind.Split:
                    case DollEventKind.Emptied:
                    {
                        float delay = Style.ReactionDelay + openings * Style.MultiSplitStagger;
                        openings++;
                        Retire(e.Cell);
                        // The children's markers go down now, hidden, on exactly the cells the boss put
                        // them on - so nothing else can take those cells while they are in the air.
                        List<Doll> children = null;
                        if (e.Kind == DollEventKind.Split && e.Children.Count > 0)
                        {
                            children = new List<Doll>(e.Children.Count);
                            for (int c = 0; c < e.Children.Count; c++)
                            {
                                children.Add(Reserve(e.Children[c], e.ChildGeneration));
                            }
                        }
                        StartCoroutine(Open(e.Cell, e.Generation, delay, children));
                        float ends = delay + OpenLength(children == null);
                        if (ends >= lastOpeningEnds)
                        {
                            lastOpeningEnds = ends;
                            lastOpened = e.Cell;
                        }
                        break;
                    }

                    case DollEventKind.Moved:
                        StartCoroutine(Carry(e.Cell, e.To, e.Generation));
                        break;

                    case DollEventKind.AllCracked:
                        beaten = true;
                        break;
                }
            }
            if (beaten)
            {
                StartCoroutine(SignOff(lastOpeningEnds + Style.CompletionHold, lastOpened));
            }
            if (running == 0)
            {
                Reconcile();
            }
        }

        /// <summary>The marker that will stand on a cell, put down hidden until it is handed over.</summary>
        private Doll Reserve(GridPos cell, int generation)
        {
            Doll d;
            if (!resting.TryGetValue(cell, out d))
            {
                d = MakeDoll(cell, generation);
                resting[cell] = d;
            }
            else if (d.Generation != generation)
            {
                d.Generation = generation;
                d.Body.sprite = Whole(generation);
                Wear(d);
            }
            d.Held = true;
            d.Body.enabled = false;
            d.Shadow.enabled = false;
            d.Presence.enabled = false;
            return d;
        }

        /// <summary>Takes a doll's marker off its cell. Whatever plays its part next stands in its exact
        /// place on the same frame, so it is never seen twice and never seen missing.</summary>
        private void Retire(GridPos cell)
        {
            Doll d;
            if (resting.TryGetValue(cell, out d))
            {
                // Its light goes out over a moment rather than with the marker.
                if (d.Presence.enabled && d.Presence.color.a > 0.001f)
                {
                    StartCoroutine(DimPresence(Take("PresenceOut", GlowSprite(), PulseOrder), d.Presence));
                }
                Destroy(d.Root);
                resting.Remove(cell);
            }
        }

        private IEnumerator DimPresence(SpriteRenderer r, SpriteRenderer from)
        {
            r.transform.position = from.transform.position;
            r.transform.localScale = from.transform.lossyScale;
            Color colour = from.color;
            float alpha = colour.a;
            const float Seconds = 0.2f;
            float t = 0f;
            while (t < Seconds)
            {
                t += Time.deltaTime;
                colour.a = alpha * (1f - Mathf.Clamp01(t / Seconds));
                r.color = colour;
                yield return null;
            }
            Give(r);
        }

        private float OpenLength(bool empty)
        {
            float open = Style.ReactionDuration + Style.OpenDuration;
            return empty
                ? open + Style.TerminalEmptyHold + Style.TerminalFadeDuration
                : open + Style.ChildRevealDuration + Style.ParentFadeDuration;
        }

        // =================================================================== the first doll

        private IEnumerator Arrive(Doll d)
        {
            running++;
            float h = HeightOf(d.Generation);
            SpriteRenderer pulse = Take("ArrivalLight", GlowSprite(), PulseOrder);
            Vector2 foot = FootOf(d.Cell);
            d.Body.enabled = true;
            d.Shadow.enabled = true;
            float t = 0f;
            while (t < Style.SpawnDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.SpawnDuration);
                // 0.78 up to 1.03 over most of it, then settled back to 1.00: it sits down, it does
                // not bounce.
                float size = k < 0.7f
                    ? Mathf.Lerp(Style.SpawnStartScale, 1f + Style.SpawnOvershoot, EaseOut(k / 0.7f))
                    : Mathf.Lerp(1f + Style.SpawnOvershoot, 1f, Mathf.SmoothStep(0f, 1f, (k - 0.7f) / 0.3f));
                Stand(d.Body, Vector2.zero, h, size, size, Style.SpawnLift * cellSize * (1f - EaseOut(k)), 0f, 0f);
                d.Body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(k * 3f));
                Shade(d.Shadow, Vector2.zero, d.Generation, h, 0.7f + 0.3f * k, Style.ShadowOpacity * k);
                Glow(pulse, foot + new Vector2(0f, h * 0.15f), h * Aspect(d.Generation) * 1.3f, h * 0.45f,
                    Style.WarmCream, Style.SpawnPulse * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            d.Body.color = Color.white;
            Give(pulse);
            d.Held = false;
            d.Presence.enabled = true;
            d.RestSince = Time.time;
            Done();
        }

        // =================================================================== a doll opening

        /// <summary>
        /// A doll opening along its seam: it flinches, the seam catches the light, the shells part and
        /// - when it holds anything - its children rise out and leave. The empty shells then fade.
        ///
        /// A last-generation doll plays the same opening and stops: its warm light goes out in an empty
        /// shell, nothing comes out, and it fades. That missing beat is the whole difference, and it is
        /// what tells the player there was nothing left inside.
        /// </summary>
        private IEnumerator Open(GridPos cell, int generation, float delay, List<Doll> children)
        {
            running++;
            bool empty = children == null;
            float h = HeightOf(generation);
            float width = h * Aspect(generation);
            float seamUp = Style.SeamHeight[ArtIndex(generation)] * h;
            Vector2 foot = FootOf(cell);

            // Standing exactly as its marker stood, through any stagger.
            SpriteRenderer whole = Take("OpeningDoll", Whole(generation), RestOrder);
            SpriteRenderer shadow = Take("OpeningShadow", ShadowSprite(), ShadowOrder);
            Stand(whole, foot, h, 1f, 1f, 0f, 0f, 0f);
            Shade(shadow, foot, generation, h, 1f, Style.ShadowOpacity);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            SpriteRenderer top = Take("TopShell", Top(generation), TopShellOrder);
            SpriteRenderer bottom = Take("BottomShell", Bottom(generation), BottomShellOrder);
            SpriteRenderer inside = Take("InsideLight", GlowSprite(), GlowOrder);
            SpriteRenderer seam = Take("SeamLight", SeamSprite(), SeamOrder);
            Give(whole);

            float openAt = Style.ReactionDuration;
            float openEnd = openAt + Style.OpenDuration;
            float revealAt = openAt + Style.OpenDuration * 0.7f;
            float leaveAt = revealAt + Style.ChildRevealDuration;
            float fadeAt = empty ? openEnd + Style.TerminalEmptyHold : leaveAt + 0.04f;
            float fadeLength = empty ? Style.TerminalFadeDuration : Style.ParentFadeDuration;
            float end = fadeAt + fadeLength;
            float tipSide = ((cell.X + cell.Y) & 1) == 0 ? 1f : -1f;

            if (!empty)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    StartCoroutine(Emerge(children[i], foot, h, seamUp, revealAt,
                        leaveAt + i * Style.ChildStagger, i, children.Count));
                }
            }

            bool dusted = false;
            float t = 0f;
            while (t < end)
            {
                t += Time.deltaTime;

                // 1. It flinches and comes up off its cube.
                float reacted = Mathf.Clamp01(t / Style.ReactionDuration);
                float flinch = 0.02f * Mathf.Sin(reacted * Mathf.PI);
                float lift = Style.ReactionLift * cellSize * EaseOut(reacted);

                // 2. The seam catches the light in the last of the flinch, and is gone as the shells part.
                float seamK = Mathf.Clamp01((t - Style.ReactionDuration * 0.4f) / Style.SeamUnlockDuration);
                float seamLight = Style.SeamHighlightStrength * Mathf.Sin(seamK * Mathf.PI);

                // 3. The shells part.
                float opened = OpenCurve((t - openAt) / Style.OpenDuration);
                float topUp = Style.OpenTopDistance * h * opened;
                float bottomDown = Style.OpenBottomDistance * h * opened;

                // 4. Its job done, the shell fades.
                float faded = Mathf.Clamp01((t - fadeAt) / fadeLength);
                float shrink = 1f - 0.05f * faded;
                Color shell = Color.Lerp(Color.white, Style.SpentTint, faded * 0.6f);
                shell.a = 1f - Mathf.SmoothStep(0f, 1f, faded);

                Stand(bottom, foot, h, (1f + flinch) * shrink, (1f - flinch) * shrink, lift - bottomDown, 0f, 0f);
                Stand(top, foot, h, (1f + flinch) * shrink, (1f - flinch) * shrink, lift + topUp,
                    Style.OpenTilt * opened * tipSide, seamUp);
                top.color = shell;
                bottom.color = shell;

                Shade(shadow, foot, generation, h, 1f + 0.1f * reacted,
                    Style.ShadowOpacity * (1f - 0.55f * reacted) * (1f - faded));

                Glow(seam, foot + new Vector2(0f, seamUp + lift - h * 0.012f), width * 0.95f, width * 0.95f,
                    Style.WarmCream, seamLight * (1f - opened));

                // The light inside: while children are coming out it lasts until the shell goes; in an
                // empty doll it goes out during the empty beat, before the shell does.
                float insideLight = empty
                    ? Style.InnerWarmGlow * opened * (1f - Mathf.Clamp01((t - openEnd) / Style.TerminalEmptyHold))
                    : Style.InnerWarmGlow * opened * (1f - faded);
                Glow(inside, foot + new Vector2(0f, seamUp + lift + topUp * 0.5f), width * 0.9f,
                    h * 0.30f + topUp, Color.Lerp(Style.WarmCream, Style.MutedGold, 0.35f), insideLight);

                if (!dusted && t >= fadeAt)
                {
                    dusted = true;
                    Motes(foot + new Vector2(0f, seamUp), Style.WoodDustCount, 0.35f, false);
                }
                yield return null;
            }
            Give(top);
            Give(bottom);
            Give(inside);
            Give(seam);
            Give(shadow);
            Done();
        }

        /// <summary>
        /// One child: it rises out of the opened parent, leaves along a gentle arc for the cell the boss
        /// gave it, lands, and is handed over to the marker waiting there.
        ///
        /// It starts INSIDE - behind the parent's bottom shell - so the first thing seen of it is a
        /// smaller doll coming up out of a bigger one. Its path bows to its own side and its shadow
        /// stays on the board below it, which is what keeps the destination readable when two children
        /// cross.
        /// </summary>
        private IEnumerator Emerge(Doll child, Vector2 parentFoot, float parentHeight, float seamUp,
            float revealAt, float leaveAt, int index, int count)
        {
            running++;
            float h = HeightOf(child.Generation);
            float side = count > 1 ? Mathf.Lerp(-1f, 1f, index / (float)(count - 1)) : 0f;
            float arcSide = side != 0f ? Mathf.Sign(side) : 1f;
            int order = TravelOrder + (travelSlot++ % 8);

            SpriteRenderer body = Take("Child", Whole(child.Generation), ChildInsideOrder);
            SpriteRenderer shadow = Take("ChildShadow", ShadowSprite(), ShadowOrder);
            SpriteRenderer trailNear = Take("ChildTrail", Whole(child.Generation), TrailOrder);
            SpriteRenderer trailFar = Take("ChildTrail", Whole(child.Generation), TrailOrder);
            SpriteRenderer contact = Take("LandingLight", GlowSprite(), PulseOrder);
            body.enabled = false;
            shadow.enabled = false;
            trailNear.enabled = false;
            trailFar.enabled = false;
            contact.enabled = false;

            // It comes out no bigger than would have fitted inside its parent, and grows into its own
            // size on the way to its cube.
            float startScale = Mathf.Min(Style.ChildStartScale,
                Style.ChildInsideFit * parentHeight / Mathf.Max(h, 0.0001f));
            Vector2 inside = parentFoot + new Vector2(side * parentHeight * 0.10f,
                seamUp - h * startScale * 0.55f);
            Vector2 outside = parentFoot + new Vector2(side * parentHeight * 0.22f,
                seamUp + parentHeight * Style.OpenTopDistance * 0.35f);
            Vector2 target = FootOf(child.Cell);
            float distance = Vector2.Distance(outside, target);
            float travel = Mathf.Lerp(Style.ChildTravelDurationMin, Style.ChildTravelDurationMax,
                Mathf.Clamp01(distance / (cellSize * 5f)));
            Vector2 across = target - outside;
            Vector2 normal = across.sqrMagnitude > 0.0001f
                ? new Vector2(-across.y, across.x).normalized : Vector2.up;
            Vector2 control = (outside + target) * 0.5f + normal * (arcSide * Style.ChildArcHeight * distance)
                + Vector2.up * (Style.ChildArcHeight * distance * 0.5f);
            float landAt = leaveAt + travel;
            float end = landAt + Style.LandingDuration;
            float emergedScale = Mathf.Lerp(startScale, 1f, 0.4f);
            bool landed = false;

            float t = 0f;
            while (t < end)
            {
                t += Time.deltaTime;
                if (t < revealAt)
                {
                    yield return null;
                    continue;
                }
                body.enabled = true;
                if (t < leaveAt)
                {
                    // Rising out of the parent, still behind the front of its bottom shell.
                    float k = EaseOut((t - revealAt) / Mathf.Max(leaveAt - revealAt, 0.0001f));
                    body.sortingOrder = ChildInsideOrder;
                    Stand(body, Vector2.Lerp(inside, outside, k), h,
                        Mathf.Lerp(startScale, emergedScale, k),
                        Mathf.Lerp(startScale, emergedScale, k), 0f, 0f, 0f);
                }
                else if (t < landAt)
                {
                    float k = Mathf.Clamp01((t - leaveAt) / travel);
                    float eased = EaseInOut(k);
                    float air = Mathf.Sin(k * Mathf.PI);
                    float size = Mathf.Lerp(emergedScale, 1f, eased);
                    body.sortingOrder = order;
                    Stand(body, Bezier(outside, control, target, eased), h, size, size, 0f,
                        Style.ChildTilt * air * arcSide, h * 0.5f);

                    // A short warm smear behind it, from the same path a moment earlier.
                    Trail(trailNear, outside, control, target, leaveAt, travel, t - 0.035f, h,
                        emergedScale, Style.TrailOpacity * air);
                    Trail(trailFar, outside, control, target, leaveAt, travel, t - 0.07f, h,
                        emergedScale, Style.TrailOpacity * 0.5f * air);

                    // Its shadow keeps to the board, closing in on the target as the child comes down.
                    shadow.enabled = true;
                    Shade(shadow, Vector2.Lerp(parentFoot, target, eased), child.Generation, h,
                        0.6f + 0.4f * eased, Style.ShadowOpacity * 0.5f * (0.3f + 0.7f * eased));
                }
                else
                {
                    if (!landed)
                    {
                        landed = true;
                        trailNear.enabled = false;
                        trailFar.enabled = false;
                        contact.enabled = true;
                        body.sortingOrder = RestOrder;
                        Motes(target + new Vector2(0f, h * 0.05f), 2, 0.18f, true);
                    }
                    float k = Mathf.Clamp01((t - landAt) / Style.LandingDuration);
                    float settle = Mathf.Sin(k * Mathf.PI);
                    float size = 1f + Style.LandingOvershoot * settle;
                    Stand(body, target, h, size, size, -Style.LandingOvershoot * h * 0.5f * settle, 0f, 0f);
                    Shade(shadow, target, child.Generation, h, 0.85f + 0.15f * k,
                        Mathf.Lerp(Style.ShadowOpacity * 0.5f, Style.ShadowOpacity, k));
                    Glow(contact, target + new Vector2(0f, h * 0.08f), h * Aspect(child.Generation) * 1.1f,
                        h * 0.30f, Style.WarmCream, 0.30f * (1f - k));
                }
                yield return null;
            }

            // Handed over on this frame: the proxy goes as the waiting marker appears.
            Give(body);
            Give(shadow);
            Give(trailNear);
            Give(trailFar);
            Give(contact);
            child.Held = false;
            child.Body.enabled = true;
            child.Shadow.enabled = true;
            child.Presence.enabled = true;
            child.RestSince = Time.time;
            child.Body.color = Color.white;
            PoseResting(child, Time.time);
            Done();
        }

        private void Trail(SpriteRenderer r, Vector2 from, Vector2 control, Vector2 to, float leaveAt,
            float travel, float at, float height, float fromScale, float alpha)
        {
            float k = Mathf.Clamp01((at - leaveAt) / travel);
            if (at < leaveAt || alpha <= 0.001f)
            {
                r.enabled = false;
                return;
            }
            r.enabled = true;
            float size = Mathf.Lerp(fromScale, 1f, EaseInOut(k));
            Stand(r, Bezier(from, control, to, EaseInOut(k)), height, size, size, 0f, 0f, 0f);
            Color c = Color.Lerp(Style.Burgundy, Style.MutedGold, 0.4f);
            c.a = alpha;
            r.color = c;
        }

        // =================================================================== water

        /// <summary>
        /// A doll carried to another cube because water moved its own out from under it. The doll itself
        /// makes the trip, whole and at its own generation: no seam, no light, nothing comes out. A
        /// short plain hop, so it can never be mistaken for a split.
        /// </summary>
        private IEnumerator Carry(GridPos from, GridPos to, int generation)
        {
            running++;
            Doll d;
            if (resting.TryGetValue(from, out d))
            {
                resting.Remove(from);
            }
            else
            {
                d = MakeDoll(from, generation);
            }
            Doll stale;
            if (resting.TryGetValue(to, out stale) && stale != d)
            {
                Destroy(stale.Root);
            }
            d.Cell = to;
            resting[to] = d;
            d.Held = true;
            d.Body.enabled = true;
            d.Shadow.enabled = true;
            d.Presence.enabled = false;     // the light stays with the cube; it comes up on the new one
            d.Body.sortingOrder = TravelOrder;

            Vector2 a = FootOf(from);
            Vector2 b = FootOf(to);
            float h = HeightOf(generation);
            float t = 0f;
            while (t < Style.RelocationDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.RelocationDuration);
                float air = Mathf.Sin(k * Mathf.PI);
                d.Root.transform.localPosition = Flat(Vector2.Lerp(a, b, Mathf.SmoothStep(0f, 1f, k)));
                Stand(d.Body, Vector2.zero, h, 1f, 1f, Style.RelocationArcHeight * cellSize * air, 0f, 0f);
                Shade(d.Shadow, Vector2.zero, generation, h, 1f - 0.25f * air, Style.ShadowOpacity * (1f - 0.5f * air));
                yield return null;
            }
            d.Root.transform.localPosition = Flat(b);
            d.Body.sortingOrder = RestOrder;
            d.Presence.enabled = true;
            d.RestSince = Time.time;
            d.Held = false;
            Done();
        }

        // =================================================================== the boss beaten

        /// <summary>The last shell gone, a breath of quiet, then a faint warm wash over the arena and a few
        /// motes rising from where the last doll stood. Nothing that competes with the round's own
        /// completion - and nothing that could trigger it: the boss already declared the round won.</summary>
        private IEnumerator SignOff(float delay, GridPos lastCell)
        {
            running++;
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }
            SpriteRenderer wash = Take("BossBeaten", PulseSprite(), PulseOrder);
            Vector2 low = toWorld != null && board != null ? toWorld(new GridPos(board.MinX, board.MinY)) : Vector2.zero;
            Vector2 high = toWorld != null && board != null
                ? toWorld(new GridPos(board.MinX + board.Width - 1, board.MinY + board.Height - 1)) : Vector2.zero;
            Vector2 centre = (low + high) * 0.5f;
            Vector2 size = new Vector2(Mathf.Abs(high.x - low.x), Mathf.Abs(high.y - low.y))
                + new Vector2(cellSize, cellSize);
            Motes(FootOf(lastCell) + new Vector2(0f, cellSize * 0.3f), 4, 0.25f, true);
            float t = 0f;
            while (t < Style.CompletionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Style.CompletionDuration);
                Glow(wash, centre - new Vector2(0f, size.y * 0.5f), size.x * (1f + 0.02f * k), size.y * (1f + 0.02f * k),
                    Style.MutedGold, Style.CompletionPulseStrength * Mathf.Sin(k * Mathf.PI));
                yield return null;
            }
            Give(wash);
            Done();
        }

        // =================================================================== motes

        private void Motes(Vector2 at, int count, float spread, bool rising)
        {
            for (int i = 0; i < count && motesThisTurn < Style.MaxMotesPerTurn; i++, motesThisTurn++)
            {
                Color colour = i % 3 == 0 ? Style.MutedGold : i % 3 == 1 ? Style.Burgundy : Style.WoodBrown;
                Vector2 heading = new Vector2(UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(rising ? 0.6f : -0.2f, 1f));
                if (heading.sqrMagnitude < 0.0001f)
                {
                    heading = Vector2.up;
                }
                StartCoroutine(Drift(Take("Mote", MoteSprite(), MoteOrder), at,
                    heading.normalized * (spread * cellSize), UnityEngine.Random.Range(0.35f, 0.6f), colour));
            }
        }

        private IEnumerator Drift(SpriteRenderer r, Vector2 from, Vector2 travel, float life, Color colour)
        {
            float size = cellSize * UnityEngine.Random.Range(0.035f, 0.06f);
            float t = 0f;
            while (t < life)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / life);
                Vector2 p = from + travel * EaseOut(k) + new Vector2(0f, cellSize * 0.08f * k);
                r.transform.localPosition = Flat(p);
                float s = size * (1f - 0.4f * k);
                r.transform.localScale = new Vector3(s, s, 1f);
                colour.a = 0.8f * (1f - k) * (1f - k);
                r.color = colour;
                yield return null;
            }
            Give(r);
        }

        // =================================================================== resting dolls

        private void Update()
        {
            if (pending != null && Time.unscaledTime - pendingSince > Style.HoldTimeout)
            {
                Release();
            }
            PushIdleMaterials();
            float now = Time.time;
            foreach (Doll d in resting.Values)
            {
                if (!d.Held)
                {
                    PoseResting(d, now);
                }
            }
        }

        /// <summary>
        /// A resting doll. The movement here is the SUPPORTING layer - a breath, a fraction of a pixel of
        /// rise, a lean too small to name - because at board size motion is hardly seen: what makes it
        /// look alive is its material (the idle shader, worn by its body) and the warm light it sits in.
        /// Each doll's place in every cycle is its cell's, so eight of them never do anything together.
        /// </summary>
        private void PoseResting(Doll d, float now)
        {
            float h = HeightOf(d.Generation);
            float motion = Layers.Motion ? Style.MotionSecondaryStrength : 0f;
            float cycle = (now / Mathf.Max(Style.IdleBreathSeconds, 0.01f) + d.Phase * Style.IdlePhaseSpread)
                * Mathf.PI * 2f;
            float breath = Style.IdleBreathStrength * motion * Mathf.Sin(cycle);
            float riseRange = Style.IdleFloatAmount * motion * cellSize;
            float lift = riseRange * Mathf.Sin(cycle + 0.9f);
            float lean = Style.IdleTilt * motion * Mathf.Sin(cycle * 0.61f + d.Phase * 5f);
            Stand(d.Body, Vector2.zero, h, 1f - breath * 0.5f, 1f + breath, lift, lean, 0f);
            Shade(d.Shadow, Vector2.zero, d.Generation, h, 1f - lift / Mathf.Max(h, 0.0001f),
                Style.ShadowOpacity);

            // The light it sits in: no wider than the doll, breathing on its own slow period, a few per
            // cent dimmer as the doll rises off it, and brought up gently when it has just arrived.
            float settled = Mathf.Clamp01((now - d.RestSince) / 0.35f);
            float glowCycle = (now * Style.PresenceLightSpeed + d.Phase * 0.73f * Style.IdlePhaseVariation)
                * Mathf.PI * 2f;
            float riseShare = riseRange > 0.0001f ? Mathf.Clamp01(lift / riseRange) : 0f;
            float presence = Layers.PresenceLight
                ? Style.PresenceLightStrength * (0.8f + 0.2f * Mathf.Sin(glowCycle)) * (1f - 0.03f * riseShare)
                    * settled
                : 0f;
            float width = h * Aspect(d.Generation) * Style.PresenceLightRadius;
            Glow(d.Presence, new Vector2(0f, h * 0.03f), width, width * 0.42f, Style.PresenceColor, presence);
        }

        // =================================================================== the idle material

        private static Material[] idleMaterials;

        private static bool idleMaterialMissing;

        private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        private static readonly int WarmthStrengthId = Shader.PropertyToID("_WarmthStrength");
        private static readonly int WarmthSpeedId = Shader.PropertyToID("_WarmthSpeed");
        private static readonly int WarmthScaleId = Shader.PropertyToID("_WarmthScale");
        private static readonly int CoreStrengthId = Shader.PropertyToID("_CoreStrength");
        private static readonly int CoreSpeedId = Shader.PropertyToID("_CoreSpeed");
        private static readonly int SeamResponseId = Shader.PropertyToID("_SeamResponse");
        private static readonly int SeamWidthId = Shader.PropertyToID("_SeamWidth");
        private static readonly int SheenStrengthId = Shader.PropertyToID("_SheenStrength");
        private static readonly int SheenSpeedId = Shader.PropertyToID("_SheenSpeed");
        private static readonly int SheenSoftnessId = Shader.PropertyToID("_SheenSoftness");
        private static readonly int GlintStrengthId = Shader.PropertyToID("_GlintStrength");
        private static readonly int GlintDurationId = Shader.PropertyToID("_GlintDuration");
        private static readonly int GlintIntervalMinId = Shader.PropertyToID("_GlintIntervalMin");
        private static readonly int GlintIntervalMaxId = Shader.PropertyToID("_GlintIntervalMax");
        private static readonly int GlintAId = Shader.PropertyToID("_GlintA");
        private static readonly int GlintBId = Shader.PropertyToID("_GlintB");
        private static readonly int GlintCId = Shader.PropertyToID("_GlintC");
        private static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int DepthStrengthId = Shader.PropertyToID("_DepthStrength");
        private static readonly int PhaseVariationId = Shader.PropertyToID("_PhaseVariation");
        private static readonly int SheenColorId = Shader.PropertyToID("_SheenColor");
        private static readonly int GlintColorId = Shader.PropertyToID("_GlintColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");

        /// <summary>Puts a resting doll's body in its generation's idle material. Without the shader or the
        /// mask it keeps the plain sprite material - still the doll, only unlit - and says so once.</summary>
        private void Wear(Doll d)
        {
            Material material = IdleMaterial(ArtIndex(d.Generation));
            if (material != null)
            {
                d.Body.sharedMaterial = material;
            }
        }

        /// <summary>
        /// One material per prepared doll, SHARED by every doll of that generation - never one per doll,
        /// and no property blocks, which would take the dolls out of batching. What differs by
        /// generation (the mask, the multipliers, the glint points) lives on its material; what differs
        /// by doll is worked out in the shader from where the doll stands.
        /// </summary>
        private static Material IdleMaterial(int art)
        {
            if (idleMaterialMissing)
            {
                return null;
            }
            if (idleMaterials == null)
            {
                idleMaterials = new Material[ArtCount];
            }
            if (idleMaterials[art] != null)
            {
                return idleMaterials[art];
            }
            // Shader.Find works in the editor; the Resources copy is what survives into a build.
            Shader shader = Shader.Find("ProjectBlock/MatryoshkaIdle");
            if (shader == null)
            {
                shader = Resources.Load<Shader>("Shaders/MatryoshkaIdle");
            }
            Texture2D mask = Resources.Load<Texture2D>(ArtFolder + "matryoshka_g" + (art + 1) + "_mask");
            if (shader == null || mask == null)
            {
                idleMaterialMissing = true;
                Debug.LogError("[block_bonk] Matryoshka idle material unavailable ("
                    + (shader == null ? "shader ProjectBlock/MatryoshkaIdle" : "mask matryoshka_g" + (art + 1) + "_mask")
                    + " missing) - resting dolls draw unlit. Run Tools/ArtPrep/matryoshka_sprites.py.");
                return null;
            }
            idleMaterials[art] = new Material(shader);
            idleMaterials[art].hideFlags = HideFlags.HideAndDontSave;
            idleMaterials[art].SetTexture(MaskTexId, mask);
            PushIdleMaterial(art);
            return idleMaterials[art];
        }

        /// <summary>Hands the Style and the lab's switches to the materials. Every frame, so tuning shows at
        /// once: four materials and a couple of dozen numbers.</summary>
        private static void PushIdleMaterials()
        {
            if (idleMaterials == null)
            {
                return;
            }
            for (int i = 0; i < idleMaterials.Length; i++)
            {
                if (idleMaterials[i] != null)
                {
                    PushIdleMaterial(i);
                }
            }
        }

        private static void PushIdleMaterial(int art)
        {
            Material m = idleMaterials[art];
            float cycle = Style.GenerationCycleScale[art];
            float warmth = Layers.BodyWarmth ? Style.GenerationWarmthMultiplier[art] : 0f;
            m.SetFloat(WarmthStrengthId, Style.BodyWarmthStrength * warmth);
            m.SetFloat(WarmthSpeedId, Style.BodyWarmthSpeed * cycle);
            m.SetFloat(WarmthScaleId, Style.BodyWarmthScale);
            m.SetFloat(CoreStrengthId, Layers.BodyWarmth
                ? Style.InnerCoreStrength * Style.GenerationCoreMultiplier[art] : 0f);
            m.SetFloat(CoreSpeedId, Style.InnerCoreSpeed * cycle);
            m.SetFloat(SeamResponseId, Style.SeamIdleResponse * warmth);
            m.SetFloat(SeamWidthId, Style.SeamIdleWidth);
            m.SetFloat(SheenStrengthId, Layers.LacquerSheen ? Style.LacquerSheenStrength : 0f);
            m.SetFloat(SheenSpeedId, Style.LacquerSheenSpeed * cycle);
            m.SetFloat(SheenSoftnessId, Style.LacquerSheenSoftness);
            m.SetFloat(GlintStrengthId, Layers.GoldResponse
                ? Style.GoldGlintStrength * Style.GenerationGoldMultiplier[art] : 0f);
            m.SetFloat(GlintDurationId, Style.GoldGlintDuration);
            m.SetFloat(GlintIntervalMinId, Style.GoldGlintIntervalMin);
            m.SetFloat(GlintIntervalMaxId, Style.GoldGlintIntervalMax);
            m.SetVector(GlintAId, Style.GlintAnchors[art * 3]);
            m.SetVector(GlintBId, Style.GlintAnchors[art * 3 + 1]);
            m.SetVector(GlintCId, Style.GlintAnchors[art * 3 + 2]);
            m.SetFloat(RimStrengthId, Layers.RimLight ? Style.RimStrength : 0f);
            m.SetFloat(RimWidthId, Style.RimWidth);
            m.SetFloat(DepthStrengthId, Style.ColorDepthStrength);
            m.SetFloat(PhaseVariationId, Style.IdlePhaseVariation);
            m.SetColor(SheenColorId, Style.SheenColor);
            m.SetColor(GlintColorId, Style.GlintColor);
            m.SetColor(RimColorId, Style.RimColor);
        }

        private Doll MakeDoll(GridPos cell, int generation)
        {
            var d = new Doll { Cell = cell, Generation = generation, Phase = Hash01(cell) };
            d.Root = new GameObject("Doll");
            d.Root.transform.SetParent(transform, false);
            d.Presence = NewRenderer(d.Root.transform, "Presence", GlowSprite(), PulseOrder);
            d.Shadow = NewRenderer(d.Root.transform, "Shadow", ShadowSprite(), ShadowOrder);
            d.Body = NewRenderer(d.Root.transform, "Doll", Whole(generation), RestOrder);
            Wear(d);
            d.Root.transform.localPosition = Flat(FootOf(cell));
            PoseResting(d, Time.time);
            return d;
        }

        /// <summary>Makes the board's dolls exactly what Core says they are, without animating anything.</summary>
        private void Reconcile()
        {
            var keep = new HashSet<GridPos>();
            for (int i = 0; i < truth.Count; i++)
            {
                Mark m = truth[i];
                keep.Add(m.Cell);
                Doll d;
                if (!resting.TryGetValue(m.Cell, out d))
                {
                    d = MakeDoll(m.Cell, m.Generation);
                    resting[m.Cell] = d;
                }
                if (d.Generation != m.Generation)
                {
                    d.Generation = m.Generation;
                    d.Body.sprite = Whole(m.Generation);
                    Wear(d);
                }
                d.Held = false;
                d.Body.enabled = true;
                d.Shadow.enabled = true;
                d.Presence.enabled = true;
                d.Body.color = Color.white;
                d.Body.sortingOrder = RestOrder;
                d.Root.transform.localPosition = Flat(FootOf(m.Cell));
                PoseResting(d, Time.time);
            }
            var gone = new List<GridPos>();
            foreach (KeyValuePair<GridPos, Doll> entry in resting)
            {
                if (!keep.Contains(entry.Key))
                {
                    gone.Add(entry.Key);
                }
            }
            for (int i = 0; i < gone.Count; i++)
            {
                Destroy(resting[gone[i]].Root);
                resting.Remove(gone[i]);
            }
        }

        /// <summary>Stops everything in flight and settles the board onto Core's answer at once.</summary>
        private void FinishAll()
        {
            StopAllCoroutines();
            running = 0;
            var inUse = new List<SpriteRenderer>(live);
            for (int i = 0; i < inUse.Count; i++)
            {
                Give(inUse[i]);
            }
            Reconcile();
        }

        private void Done()
        {
            running = Mathf.Max(0, running - 1);
            if (running == 0 && pending == null)
            {
                Reconcile();
            }
        }

        // =================================================================== geometry

        private void Bind(int generations, GameBoard onBoard, Func<GridPos, Vector2> cellToWorld, float cell)
        {
            lastGeneration = Mathf.Max(1, generations);
            board = onBoard;
            toWorld = cellToWorld;
            cellSize = cell > 0f ? cell : cellSize;
        }

        private Vector2 FootOf(GridPos cell)
        {
            Vector2 centre = toWorld != null ? toWorld(cell) : Vector2.zero;
            return centre - new Vector2(0f, Style.BaseOffset * cellSize);
        }

        private float HeightOf(int generation)
        {
            return cellSize * Style.DollHeight * Style.GenerationScale[ArtIndex(generation)];
        }

        /// <summary>
        /// Which of the four prepared dolls a generation wears. With the boss's own four generations it is
        /// one each, generation 1 the largest. If its Generations is ever retuned, the first generation is
        /// still the largest doll and the last is still the smallest, and the rest are spread between.
        /// </summary>
        private int ArtIndex(int generation)
        {
            if (lastGeneration <= 1)
            {
                return 0;
            }
            float k = (Mathf.Clamp(generation, 1, lastGeneration) - 1) / (float)(lastGeneration - 1);
            return Mathf.Clamp(Mathf.RoundToInt(k * (ArtCount - 1)), 0, ArtCount - 1);
        }

        private float Aspect(int generation)
        {
            Sprite s = Whole(generation);
            return s != null && s.rect.height > 0f ? s.rect.width / s.rect.height : 0.62f;
        }

        /// <summary>Stands a doll-shaped renderer on <paramref name="foot"/>: its bottom on that point,
        /// <paramref name="height"/> tall, stretched by sx/sy, raised by lift, and tipped by tilt degrees
        /// about a point <paramref name="pivotUp"/> above its foot.</summary>
        private static void Stand(SpriteRenderer r, Vector2 foot, float height, float sx, float sy, float lift,
            float tilt, float pivotUp)
        {
            float h = height * sy;
            Vector2 centre = foot + new Vector2(0f, h * 0.5f + lift);
            if (tilt != 0f)
            {
                Vector2 pivot = foot + new Vector2(0f, pivotUp + lift);
                float rad = tilt * Mathf.Deg2Rad;
                Vector2 arm = centre - pivot;
                centre = pivot + new Vector2(arm.x * Mathf.Cos(rad) - arm.y * Mathf.Sin(rad),
                    arm.x * Mathf.Sin(rad) + arm.y * Mathf.Cos(rad));
            }
            r.transform.localPosition = new Vector3(centre.x, centre.y, 0f);
            r.transform.localScale = new Vector3(height * sx, h, 1f);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void Shade(SpriteRenderer r, Vector2 foot, int generation, float height, float spread, float alpha)
        {
            float w = height * Aspect(generation) * Style.ShadowSize * spread;
            r.transform.localPosition = new Vector3(foot.x, foot.y + height * 0.02f, 0f);
            r.transform.localScale = new Vector3(w, w * 0.30f, 1f);
            r.transform.localRotation = Quaternion.identity;
            r.color = new Color(0f, 0f, 0f, alpha);
        }

        private static void Glow(SpriteRenderer r, Vector2 at, float width, float height, Color colour, float alpha)
        {
            r.transform.localPosition = new Vector3(at.x, at.y, 0f);
            r.transform.localScale = new Vector3(width, height, 1f);
            r.transform.localRotation = Quaternion.identity;
            colour.a = Mathf.Clamp01(alpha);
            r.color = colour;
        }

        private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        private static float EaseOut(float k)
        {
            k = Mathf.Clamp01(k);
            return 1f - (1f - k) * (1f - k) * (1f - k);
        }

        private static float EaseInOut(float k)
        {
            k = Mathf.Clamp01(k);
            return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) * 0.5f;
        }

        /// <summary>Locked, a short catch, then released and parted, easing into the hold - never linear,
        /// never past where it stops.</summary>
        private static float OpenCurve(float k)
        {
            k = Mathf.Clamp01(k);
            const float Catch = 0.2f;
            return k < Catch ? 0.06f * (k / Catch) : 0.06f + 0.94f * EaseOut((k - Catch) / (1f - Catch));
        }

        /// <summary>GLSL's smoothstep - an EDGE. Mathf.SmoothStep eases between two values instead.</summary>
        private static float Edge(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float Hash01(GridPos cell)
        {
            float h = Mathf.Sin(cell.X * 12.9898f + cell.Y * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        private static Vector3 Flat(Vector2 v)
        {
            return new Vector3(v.x, v.y, 0f);
        }

        private static void Copy(IReadOnlyList<Mark> from, List<Mark> to)
        {
            to.Clear();
            if (from == null)
            {
                return;
            }
            for (int i = 0; i < from.Count; i++)
            {
                to.Add(from[i]);
            }
        }

        // =================================================================== renderers

        private static SpriteRenderer NewRenderer(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        /// <summary>A pooled renderer for a moment's work. Every split takes a dozen and gives them back,
        /// so eight dolls breaking over a round do not churn the heap.</summary>
        private SpriteRenderer Take(string name, Sprite sprite, int order)
        {
            SpriteRenderer r = pool.Count > 0 ? pool.Pop() : NewRenderer(transform, name, null, order);
            r.gameObject.name = name;
            r.gameObject.SetActive(true);
            r.sprite = sprite;
            r.sortingOrder = order;
            r.color = Color.white;
            r.enabled = true;
            r.transform.localRotation = Quaternion.identity;
            live.Add(r);
            return r;
        }

        private void Give(SpriteRenderer r)
        {
            if (r == null || !live.Remove(r))
            {
                return;
            }
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        // =================================================================== the art

        private static Sprite[] wholeArt;

        private static Sprite[] topArt;

        private static Sprite[] bottomArt;

        private static bool artLoaded;

        /// <summary>
        /// The prepared dolls. Missing art is an ERROR, logged once, and no doll is drawn - a coloured
        /// square in its place would be exactly the placeholder this system exists to get rid of, and it
        /// would hide that the art failed to ship.
        /// </summary>
        private static bool LoadArt()
        {
            if (artLoaded)
            {
                return wholeArt != null;
            }
            artLoaded = true;
            var whole = new Sprite[ArtCount];
            var top = new Sprite[ArtCount];
            var bottom = new Sprite[ArtCount];
            for (int i = 0; i < ArtCount; i++)
            {
                string name = ArtFolder + "matryoshka_g" + (i + 1);
                whole[i] = Resources.Load<Sprite>(name);
                top[i] = Resources.Load<Sprite>(name + "_top");
                bottom[i] = Resources.Load<Sprite>(name + "_bottom");
                if (whole[i] == null || top[i] == null || bottom[i] == null)
                {
                    Debug.LogError("[block_bonk] Matryoshka art missing at Resources/" + name
                        + " - run Tools/ArtPrep/matryoshka_sprites.py");
                    return false;
                }
            }
            wholeArt = whole;
            topArt = top;
            bottomArt = bottom;
            return true;
        }

        private Sprite Whole(int generation)
        {
            return LoadArt() ? wholeArt[ArtIndex(generation)] : null;
        }

        private Sprite Top(int generation)
        {
            return LoadArt() ? topArt[ArtIndex(generation)] : null;
        }

        private Sprite Bottom(int generation)
        {
            return LoadArt() ? bottomArt[ArtIndex(generation)] : null;
        }

        // ---- generated: only what the art cannot carry

        private static Sprite shadowSprite;

        private static Sprite glowSprite;

        private static Sprite seamSprite;

        private static Sprite moteSprite;

        private static Sprite pulseSprite;

        /// <summary>A soft round falloff, squashed flat by its scale.</summary>
        private static Sprite ShadowSprite()
        {
            return shadowSprite != null ? shadowSprite : (shadowSprite = Soft(64, delegate (float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Exp(-r * r * 3f) * Mathf.Clamp01(1f - r);
            }));
        }

        private static Sprite GlowSprite()
        {
            return glowSprite != null ? glowSprite : (glowSprite = Soft(64, delegate (float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Exp(-r * r * 4f) * Mathf.Clamp01(1f - r);
            }));
        }

        /// <summary>The seam catching the light: a thin soft line that dips in the middle the way the
        /// painted seam does, fading out before either end.</summary>
        private static Sprite SeamSprite()
        {
            return seamSprite != null ? seamSprite : (seamSprite = Soft(128, delegate (float u, float v)
            {
                float line = -0.06f * (1f - u * u);
                float across = (v - line) / 0.035f;
                return Mathf.Exp(-across * across) * (1f - Edge(0.75f, 1f, Mathf.Abs(u)));
            }));
        }

        private static Sprite MoteSprite()
        {
            return moteSprite != null ? moteSprite : (moteSprite = Soft(16, delegate (float u, float v)
            {
                float r = Mathf.Sqrt(u * u + v * v);
                return Mathf.Exp(-r * r * 4f) * Mathf.Clamp01(1f - r);
            }));
        }

        /// <summary>A soft-edged panel for the arena's wash, anchored at its bottom edge.</summary>
        private static Sprite PulseSprite()
        {
            if (pulseSprite != null)
            {
                return pulseSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    float a = 1f - Edge(0.55f, 1f, Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            pulseSprite = Bake(px, n, new Vector2(0.5f, 0f));
            return pulseSprite;
        }

        private static Sprite Soft(int n, Func<float, float, float> alpha)
        {
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float v = (y + 0.5f) / n * 2f - 1f;
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(u, v)) * 255f));
                }
            }
            return Bake(px, n, new Vector2(0.5f, 0.5f));
        }

        private static Sprite Bake(Color32[] px, int n, Vector2 pivot)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), pivot, n, 0, SpriteMeshType.FullRect);
        }
    }
}
