// PURPOSE: "Tamagotchi" ON SCREEN - the boss as a small pink creature that lives at the edge of the
// screen, watches the player, asks for two cards with two plates, gets excited as a card comes
// closer, really bites, chews and swallows what it is fed, grows impatient as the deadline runs
// down, and - left hungry - goes furious and eats the player's board, jokers, powers and cards.
//
// THE VIEW DECIDES NOTHING. Every fact is Core's (TamagotchiBoss): which cards it wants and how
// much they are worth, whether a card is food, whether the meal went through, the hunger stage and
// its progress, whether the next draw is the deadline, when it goes furious, what the punish
// planner chose and exactly what was eaten. They reach this class as TamagotchiRoundVisualState
// (read every frame) and as the boss's per-event reports (TamagotchiFeedVisuals,
// TamagotchiHungerChange, TamagotchiFuryVisuals, PetRampageVisuals), matched by IDENTITY by the
// controller like every other report. Core has always FINISHED by the time a beat here starts - the
// fed card is out of the run, the bitten cells are dead, the eaten joker is gone - so everything the
// pet eats is a proxy (TamagotchiFoodProxy) and what the real UI shows behind it is the new state.
//
// HOW A FRAME IS MADE. Three layers are composed into one PetPose and handed to TamagotchiRig:
//   1. LIFE - always on, unless a beat freezes it: breathing on an asymmetric cycle whose length
//      and depth change every breath (1.8-3 s, ~1%), a 0.8 px bob and a +-0.7 degree sway on value
//      noise rather than sines, blinks at irregular intervals, ears that loosen and stiffen with
//      the mood. That is what the brief means by "not a sine loop".
//   2. MOOD - the face the hunger stage calls for (Calm -> Hungry -> Impatient -> Angry, then
//      Satisfied or Furious), blended rather than swapped.
//   3. ACT - the one presentation playing: an idle, the entrance, a feed, the fury, a punish. It
//      writes into an Act each frame and lets go when it ends; springs carry the body back.
// On top, while a card is being DRAGGED, the tracking layer (.Plates) leans, opens the mouth and
// readies the paws by distance - acting, not a highlight.
//
// THE PRESENTATION QUEUE (TamagotchiPresentationQueue): FuriousTransition > Punish > Feed >
// Request > Idle. Any event cancels an idle cleanly; a feed is never cut mid-bite (only a round
// teardown stops it); nothing random ever interrupts the fury; the entrance may be skipped.
//
// The rest is split by subject: .Idle (entrance, idle library, exits), .Plates (request plates,
// patience ring, aura, feed zone, drag tracking), .Feed (the meal and the satisfaction),
// .Fury (hunger acting, the prewarning, the fury transition), .Punish (board, joker, power and pile
// eating, post-attack), .Debug (the lab's toggles and readouts).
// EXTENSION POINT: a new beat is a routine in the partial it belongs to, queued at its priority.

using System;
using System.Collections;
using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectBlock.View
{
    /// <summary>The view states the brief lists. Driven by Core events, never guessed.</summary>
    public enum PetViewState
    {
        Hidden,
        Entering,
        Requesting,
        IdleHungry,
        TrackingFood,
        Eating,
        Satisfied,
        Impatient,
        Angry,
        FuriousTransition,
        FuriousIdle,
        PunishTelegraph,
        PunishExecute,
        PunishRecover,
        Exiting
    }

    public enum PetPresentationPriority
    {
        Idle = 0,
        Request = 1,
        Feed = 2,
        Punish = 3,
        FuriousTransition = 4
    }

    /// <summary>HIGH: everything. MEDIUM: fewer particles. LOW: no ambient particles and the
    /// stepped bite - but the acting (eyes, mouth, body) is never removed at any level.</summary>
    public enum PetLod
    {
        High,
        Medium,
        Low
    }

    /// <summary>The haptic beats the brief names. There is no haptics layer in the game yet, so
    /// they are announced (Haptics) and nothing more.</summary>
    public enum PetHaptic
    {
        FeedTap, FeedTapHigh, FuryPulse, BoardBitePulse, JokerLossTap, PileAggregate
    }

    /// <summary>The round as the pet presents it - the brief's TamagotchiRoundVisualState, filled
    /// from TamagotchiBoss every frame by the controller (or by the lab).</summary>
    public sealed class TamagotchiRoundVisualState
    {
        public bool BossActive;
        public readonly List<PetRequest> Requests = new List<PetRequest>();
        public PetHungerStage Stage;
        public float Progress;
        public bool DeadlineNext;
        public uint Seed;
        public int HomeSide;

        public int Pending
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Requests.Count; i++)
                {
                    if (!Requests[i].Fed)
                    {
                        n++;
                    }
                }
                return n;
            }
        }

        public bool Furious
        {
            get { return Stage == PetHungerStage.Furious; }
        }

        public bool Satisfied
        {
            get { return Stage == PetHungerStage.Satisfied; }
        }

        /// <summary>True while it still takes food (the feed zone exists only then).</summary>
        public bool Wants
        {
            get { return BossActive && !Furious && !Satisfied && Pending > 0; }
        }
    }

    /// <summary>Where the pet lives this round - solved by the controller from the free space
    /// around the board (GameUiController.Tamagotchi: SolvePetHome).</summary>
    public sealed class TamagotchiHome
    {
        public string Name = "none";
        public Vector2 Base;            // world point under its feet when it is fully out
        public int Facing = -1;         // +1 = the board is to its right
        public Rect Clip = new Rect(-100f, -100f, 200f, 200f);
        public float UnitScale = 1f;    // world units per body unit
        public float PxToWorld = 0.0093f;
        public float HideDepth = 1.6f;  // world distance from fully out to fully hidden
        public Vector2 PlatesCentre;
        public bool PlatesVertical;
        public float PlateScale = 1f;
        public Vector2 EdgeTap;         // a point on the screen edge beside it (edge finger tap)
        public float Tilt;              // degrees the whole pet leans (round a side, over the top)
        public Vector2 HideDir = Vector2.down; // the way it goes when it hides behind its edge
        public float Rest = 1f;         // how far out it sits at rest here (1 all of it, ~0.6 a head)
        public float Sit;               // 0..1 sitting
        public bool Shadow = true;      // only a home with a floor under it casts one
        public int Edge;                // 0 bottom, 1 right, 2 top, 3 left (for the roaming)
        public Vector2? EdgePoint;      // a point on the edge it hides behind (null = under its base, on the clip's floor)
        public bool Inverted;           // hanging over the top edge, upside down (normal mood only)
        public string Kind = "corner";  // corner / pile / side / top (the roaming's families)
        public int Side;                // +1 the right half of the screen, -1 the left
        public Rect PetRect;
        public bool Squeezed;           // no free space: it is sitting over something
    }

    /// <summary>World anchors of the screen the pet looks at and reaches into, refreshed by the
    /// controller every frame (panels are REMEMBERED, because Core has taken the eaten one by the
    /// time the eating plays).</summary>
    public sealed class TamagotchiAnchors
    {
        public Vector2 BoardCentre;
        public Rect Board;
        public Vector2 HandCentre;
        public Vector2? HandFocus;      // the hand card the player is over, when any
        public Vector2 DrawPile;
        public Vector2 DiscardPile;
        public Vector2 JokerBar;
        public Vector2 PowerBar;
        public float CellSize = 0.93f;
        public float EmptySlot = 0.8f;
        public Color GroundColour = new Color(0.17f, 0.19f, 0.25f);
        public readonly Dictionary<int, Rect> JokerPanels = new Dictionary<int, Rect>();
        public readonly Dictionary<int, Rect> PowerPanels = new Dictionary<int, Rect>();

        /// <summary>Where every panel LAST stood, and at which place in its bar - kept through the
        /// frame Core takes one away, which is the frame the pet needs to know where it was.</summary>
        public readonly Dictionary<int, Rect> PanelMemory = new Dictionary<int, Rect>();
        public readonly Dictionary<int, int> PanelIndexMemory = new Dictionary<int, int>();
        public Func<GridPos, Vector2> CellToWorld;
        public Func<int, Vector2?> HandSlotWorld;
        public Rect Screen = new Rect(-9f, -5f, 18f, 10f);
        public float DebtVignette;      // perceptual, what "Kredi kartı" already puts on the edges
    }

    /// <summary>The presentation queue: FuriousTransition > Punish > Feed > Request > Idle.</summary>
    public sealed class TamagotchiPresentationQueue
    {
        public sealed class Item
        {
            public PetPresentationPriority Priority;
            public string Name;
            public Func<IEnumerator> Routine;
            public bool Interruptible;
            public float NotBefore;      // a beat that waits for the turn's own animations
            public Action OnInterrupted;
            public Action OnFinished;
        }

        private readonly List<Item> pending = new List<Item>();
        private readonly MonoBehaviour host;
        private Coroutine running;

        public Item Current { get; private set; }

        public TamagotchiPresentationQueue(MonoBehaviour host)
        {
            this.host = host;
        }

        public IReadOnlyList<Item> Pending
        {
            get { return pending; }
        }

        public void Enqueue(Item item)
        {
            int at = pending.Count;
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].Priority < item.Priority)
                {
                    at = i;
                    break;
                }
            }
            pending.Insert(at, item);
        }

        public bool Busy
        {
            get { return Current != null || pending.Count > 0; }
        }

        /// <summary>Starts what should be playing. An idle (or an interruptible beat) gives way to
        /// anything of a higher priority; nothing else is ever cut.</summary>
        public void Tick(float now)
        {
            if (Current != null && Current.Interruptible && pending.Count > 0
                && pending[0].Priority > Current.Priority && pending[0].NotBefore <= now)
            {
                Stop(true);
            }
            if (Current == null && pending.Count > 0 && pending[0].NotBefore <= now)
            {
                Item next = pending[0];
                pending.RemoveAt(0);
                Current = next;
                running = host.StartCoroutine(Run(next));
            }
        }

        /// <summary>Steps a beat and every routine it yields, one frame at a time, on a stack of
        /// its own - so stopping the beat stops all of it, whatever Unity does with nested
        /// coroutines.</summary>
        private IEnumerator Run(Item item)
        {
            var stack = new Stack<IEnumerator>();
            IEnumerator first = item.Routine();
            if (first != null)
            {
                stack.Push(first);
            }
            while (stack.Count > 0)
            {
                IEnumerator top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                IEnumerator nested = top.Current as IEnumerator;
                if (nested != null)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return null;
            }
            if (Current == item)
            {
                Current = null;
                running = null;
                if (item.OnFinished != null)
                {
                    item.OnFinished();
                }
            }
        }

        /// <summary>Stops the current beat (its OnInterrupted puts things where they belong).</summary>
        public void Stop(bool interrupted)
        {
            if (running != null)
            {
                host.StopCoroutine(running);
            }
            Item was = Current;
            Current = null;
            running = null;
            if (interrupted && was != null && was.OnInterrupted != null)
            {
                was.OnInterrupted();
            }
        }

        /// <summary>Round teardown: everything goes.</summary>
        public void Clear()
        {
            pending.Clear();
            Stop(true);
        }

        public bool Has(string name)
        {
            if (Current != null && Current.Name == name)
            {
                return true;
            }
            for (int i = 0; i < pending.Count; i++)
            {
                if (pending[i].Name == name)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public sealed partial class TamagotchiView : MonoBehaviour
    {
        // ================================================================== tuning (brief 441-451)

        /// <summary>Every number the brief asks to be tunable, at its start value. Pixels are
        /// canvas pixels (UiLayout.WorldPerCanvasPixel), seconds are at 1x.</summary>
        public static class Tuning
        {
            // CHARACTER
            public static float BodyScale = 1.6f;            // total width, in board cells (1.4-2.0)
            public static float IdleBob = 0.8f;              // px
            public static float IdleSway = 0.7f;             // degrees
            public static float BreathAmount = 0.01f;
            public static float IdleIntervalMin = 4.0f;
            public static float IdleIntervalMax = 6.5f;
            public static float EyeTrackMaxOffset = 1f;
            public static float HeadLeanMax = 3f;            // px, the medium-distance head lean
            public static float NearFoodLean = 10f;          // px, inside the feed zone (8-12)
            public static float MouthNearScale = 1f;
            public static float CheekBulge = 2.5f;           // px
            public static float BellyGulpScale = 0.04f;

            // FEED
            public static float FoodSnapDuration = 0.11f;
            public static float GrabDuration = 0.08f;
            public static float BiteDuration = 0.11f;
            public static int ChewCount = 2;
            public static float ChewDuration = 0.10f;
            public static float GulpDuration = 0.12f;
            public static float SatisfiedDuration = 0.30f;
            public static float HighValueFeedMultiplier = 1.06f;
            public static float LowValueFeedMultiplier = 0.82f;

            /// <summary>The brief's own start values (0.11 + 0.08 + 0.11 + 2 x 0.10 + 0.12 + 0.30,
            /// and the later chomps on top) add up to a little over its own range for a meal
            /// (0.65-0.90 s). They are kept as written and the whole meal is paced by this:
            /// a normal card lands at ~0.90 s, a cheap one ~0.74, a valuable one ~1.05.</summary>
            public static float FeedPace = 0.88f;

            // HUNGER
            public static float PatienceRingAlpha = 0.32f;
            public static float ImpatientIdleRate = 0.68f;
            public static float AngryAuraStrength = 0.30f;
            public static float FuriousAuraStrength = 0.42f;
            public static float FuryTransitionDuration = 0.92f;   // the LEGACY fury only (the lab's comparison)

            // FURY (the corrective pass; seconds at 1x - the whole thing runs ~1.55 s)
            public static float FurySilence = 0.15f;           // A: dead silence, the stare
            public static float FuryDisappointment = 0.23f;    // B: "are you serious"
            public static float FuryGrowlLead = 0.08f;         // the growl starts this long before B ends
            public static float FuryFaceMorph = 0.27f;         // D: the face breaks
            public static float FuryBodyCompression = 0.95f;   // C: the body pulled in
            public static float FuryBurstDuration = 0.26f;     // F: 0.95 -> 1.10 -> 1.03
            public static float HatredWaveDuration = 0.28f;    // E: the pressure front crossing the screen
            public static float HatredVignette = 0.24f;        // perceptual, at the surge's peak
            public static float HatredAmbientVignette = 0.08f; // perceptual, for as long as it is furious
            public static float HatredDesaturation = 0.11f;    // the background at the peak
            public static float HatredChromaticOffset = 1.5f;  // px
            public static float FuryTargetHold = 0.24f;        // the predatory hold before the punish
            public static float FuriousIdleMin = 1.8f;
            public static float FuriousIdleMax = 3.5f;
            public static float GrowlIdleMin = 4f;
            public static float GrowlIdleMax = 8f;

            // ROAMING
            public static int MoveTurnIntervalMin = 2;
            public static int MoveTurnIntervalMax = 4;
            public static float IdleRelocateSeconds = 15f;     // 12-20, once
            public static float HideDuration = 0.42f;
            public static float PeekDuration = 0.34f;
            public static float ScamperSpeed = 5.5f;           // world units a second
            public static float HopDuration = 0.36f;
            public static int PositionCooldown = 2;            // the last N homes are not taken again

            // SPEECH
            public static float BubbleMinGap = 6.5f;           // seconds between unscripted bubbles
            public static float BubbleHold = 1.7f;

            // ASSET EAT (the corrective pass)
            // (the brief's own per-beat ranges add up to more than its total for a joker, 0.9-1.3 s;
            // these sit at the short end of each range and land a normal joker at ~1.35 s, a
            // valuable one at ~1.5)
            public static float AssetLockDuration = 0.15f;
            public static float AssetThreatDuration = 0.12f;
            public static float AssetGrabDuration = 0.12f;
            public static float AssetStruggleDuration = 0.19f;
            public static float AssetPullDuration = 0.23f;
            public static float AssetFirstBite = 0.3f;         // the share of it the first bite takes
            public static float AssetChewDuration = 0.075f;    // one chew
            public static float AssetSecondBite = 0.35f;
            public static float AssetGulpDuration = 0.14f;

            // BOARD EAT (the corrective pass)
            public static float BoardTelegraph = 0.27f;
            public static float BoardPreSuction = 0.15f;
            public static float BoardLunge = 0.19f;
            public static float BoardBite = 0.12f;
            public static float BoardChunkPull = 0.22f;
            public static float BoardChew = 0.15f;             // one heavy chew
            public static float BoardGulp = 0.18f;
            public static float BoardScar = 0.55f;             // the bite stress on the new edge

            // PILE (the corrective pass)
            public static float PileLift = 0.1f;
            public static float PileTravel = 0.13f;
            public static float PileGap = 0.1f;

            /// <summary>A second punish in the same fury runs a little quicker - never instant.</summary>
            public static float RepeatPunishPace = 0.86f;

            // BOARD EAT
            public static float BoardTelegraphDuration = 0.24f;
            public static float BoardLungeDuration = 0.15f;
            public static float BoardBiteDuration = 0.11f;
            public static float BoardChunkPullDuration = 0.18f;
            public static float BoardChewDuration = 0.22f;
            public static float BoardAftermathDuration = 0.35f;

            // ASSET EAT
            public static float AssetTargetScale = 1.05f;
            public static float TongueDuration = 0.14f;
            public static float PawSnatchDuration = 0.20f;
            public static float AssetTravelDuration = 0.30f;
            public static int AssetBiteCount = 3;
            public static float AssetLossResidueDuration = 0.28f;

            // PILE
            public static float PileCardInterval = 0.09f;
            public static float PileCardTravelDuration = 0.22f;
            public static int PileHeroCardCount = 5;
            public static float PileFinalGulpDuration = 0.16f;
        }

        // ================================================================== wiring

        private TamagotchiRig rig;
        private SoundFx sfx;
        private Transform flight;           // proxies in transit, above the hand
        private SortingGroup flightGroup;
        private Transform decor;            // plates, ring, aura, feed zone
        private TamagotchiPresentationQueue queue;
        private readonly PetPose pose = new PetPose();
        private readonly Act act = new Act();

        /// <summary>Powers share the panel memory with the jokers, under their id plus this.</summary>
        public const int PowerMemoryKey = 1 << 20;

        public const int PetOrder = 11;
        public const int PlatesOrder = 10;
        public const int FlightOrder = 45;
        public const int LungeOrder = 46;

        public TamagotchiRoundVisualState State = new TamagotchiRoundVisualState();
        public TamagotchiHome Home = new TamagotchiHome();
        public TamagotchiAnchors Anchors = new TamagotchiAnchors();
        public PetLod Lod = PetLod.High;

        /// <summary>The lab's slow motion. The game always runs at 1.</summary>
        public float PlaybackRate = 1f;

        /// <summary>Every audio hook as it fires (the lab prints them; SoundFx plays them).</summary>
        public event Action<PetSound> Sounded;

        /// <summary>Every haptic beat as it fires. Nothing listens yet: the game has no haptics.</summary>
        public event Action<PetHaptic> Haptics;

        /// <summary>A line for the message bar (the controller owns it).</summary>
        public event Action<string> Says;

        /// <summary>Asked of the controller when the eaten joker/power strip should step back
        /// (true) and come back (false).</summary>
        public event Action<bool, bool> DimBar;

        /// <summary>Asked of the controller: hold the eaten thing's slot OPEN in its bar
        /// (jokers?, the place it stood at), or -1 to let the bar close up - only after the last
        /// bite has been swallowed.</summary>
        public event Action<bool, int> HoldBarSlot;

        /// <summary>The arena's own transform term, for the board's 1-2 px settle after a bite.</summary>
        public event Action<Vector2> BoardImpulse;

        public PetViewState ViewState { get; private set; }

        /// <summary>The routine playing now (debug).</summary>
        public string Playing
        {
            get { return queue != null && queue.Current != null ? queue.Current.Name : "-"; }
        }

        public static TamagotchiView Create(Transform parent, SoundFx sound)
        {
            var go = new GameObject("TamagotchiView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<TamagotchiView>();
            view.sfx = sound;
            view.Build();
            return view;
        }

        private void Build()
        {
            queue = new TamagotchiPresentationQueue(this);
            decor = new GameObject("PetDecor").transform;
            decor.SetParent(transform, false);
            var decorGroup = decor.gameObject.AddComponent<SortingGroup>();
            decorGroup.sortingOrder = PlatesOrder;
            rig = TamagotchiRig.Create(transform, PetOrder);
            flight = new GameObject("PetFlight").transform;
            flight.SetParent(transform, false);
            flightGroup = flight.gameObject.AddComponent<SortingGroup>();
            flightGroup.sortingOrder = FlightOrder;
            BuildDecor();
            BuildParticles();
            BuildHatred();
            BuildSpeech();
            rig.SetVisible(false);
            ViewState = PetViewState.Hidden;
        }

        // ================================================================== the clock

        private float Dt
        {
            get { return Time.deltaTime * Mathf.Max(0f, PlaybackRate) * (labFast ? 40f : 1f); }
        }

        /// <summary>The pet's own time - the lab's slow motion slows all of it together.</summary>
        private float clock;

        public float Now
        {
            get { return clock; }
        }

        private IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                yield return null;
            }
        }

        /// <summary>Runs <paramref name="step"/> with 0..1 over <paramref name="seconds"/>, ending
        /// exactly on 1.</summary>
        private IEnumerator Tween(float seconds, Action<float> step)
        {
            float t = 0f;
            while (t < seconds)
            {
                step(Mathf.Clamp01(t / Mathf.Max(0.0001f, seconds)));
                yield return null;
                t += Dt;
            }
            step(1f);
        }

        // easing
        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static float EaseIn(float t) { t = Mathf.Clamp01(t); return t * t; }
        private static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
        private static float Bell(float t) { return Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI); }
        private static float Back(float t, float s = 1.7f)
        {
            t = Mathf.Clamp01(t) - 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        /// <summary>Frame-rate independent smoothing toward a target with a half-life in seconds.</summary>
        private float Damp(float current, float target, float halfLife)
        {
            if (halfLife <= 0f)
            {
                return target;
            }
            return Mathf.Lerp(current, target, 1f - Mathf.Pow(0.5f, Dt / halfLife));
        }

        private Vector2 Damp(Vector2 current, Vector2 target, float halfLife)
        {
            return new Vector2(Damp(current.x, target.x, halfLife), Damp(current.y, target.y, halfLife));
        }

        private float Px(float pixels)
        {
            return pixels * Home.PxToWorld;
        }

        /// <summary>World units per body unit.</summary>
        private float S
        {
            get { return Home.UnitScale; }
        }

        // ================================================================== sound / haptics / text

        private float lastSoundAt;
        private PetSound lastSound;

        private void Sound(PetSound kind)
        {
            // spam control: the same light cue twice within 40 ms is one cue
            if (kind == lastSound && clock - lastSoundAt < 0.04f)
            {
                return;
            }
            kind = FuriousCue(kind);
            lastSound = kind;
            lastSoundAt = clock;
            if (Sounded != null)
            {
                Sounded(kind);
            }
            LastSound = kind;
            LastSoundAt = clock;
            if (sfx != null && PlaybackRate > 0.95f && !labFast)
            {
                sfx.Tamagotchi(kind);
            }
        }

        /// <summary>The routines ask for the cue they always did; a furious pet answers with its
        /// own - a deeper crunch, a heavier chew, a throat that does not swallow cleanly, a grunt
        /// where the cute one said "heh".</summary>
        private PetSound FuriousCue(PetSound kind)
        {
            if (furyNow < 0.5f || LabLegacy)
            {
                return kind;
            }
            switch (kind)
            {
                case PetSound.Chomp: return PetSound.ChompFurious;
                case PetSound.Chew: return PetSound.ChewFurious;
                case PetSound.Gulp:
                case PetSound.GulpBig: return PetSound.GulpDeep;
                case PetSound.Smug: return PetSound.Grunt;
                case PetSound.Hide:
                case PetSound.Move: return PetSound.MoveFurious;
                default: return kind;
            }
        }

        /// <summary>The last audio hook that fired, and when (the lab's readout).</summary>
        public PetSound LastSound { get; private set; }

        public float LastSoundAt { get; private set; }

        /// <summary>A major event takes the foreground: the rest of the game's audio steps back
        /// (<paramref name="amount"/> of the tuned duck) for a while and comes back on its own.</summary>
        private void Duck(float amount, float seconds)
        {
            if (sfx != null && PlaybackRate > 0.95f && !labFast)
            {
                sfx.DuckForPet(amount, seconds);
            }
        }

        private void Haptic(PetHaptic kind)
        {
            if (Haptics != null)
            {
                Haptics(kind);
            }
        }

        /// <summary>A FACT for the message bar - what was eaten, what the rule is. What the pet
        /// itself says is never put here: it comes out of its mouth as a bubble (Speak, .Speech).</summary>
        private void Say(string tr, string en)
        {
            if (Says != null)
            {
                Says(Loc.Pick(en, tr));
            }
        }

        // ================================================================== the act layer

        /// <summary>What the playing beat wants the body to do this frame. Cleared when the beat
        /// ends; the springs carry the body back.</summary>
        private sealed class Act
        {
            public float? Presence;
            public Vector2 Offset;
            public float Rot;
            public Vector2 Squash = Vector2.one;
            public float Scale = 1f;
            public Vector2 Head;
            public float HeadTilt;
            public float HeadSquash = 1f;
            public Vector3? LookAt;
            public Vector2? Look;
            public PetEmotion? Emotion;
            public float EmotionWeight = 1f;
            public string Mouth;
            public float MouthOpen = 1f;
            public float MouthScale = 1f;
            public float PuffL;
            public float PuffR;
            public Color? PuffTint;
            public float Belly;
            public PetPaw? PawL;
            public PetPaw? PawR;
            public float Lick;
            public float Blink = -1f;
            public float SparkleL;
            public float SparkleR;
            public float Glow;
            public float? EarWobble;
            public bool Still;
            public float Lid = -1f;
            public float IrisMul = 1f;
            public float CatchMul = 1f;
            public Color? Cheek;
            public float CheekA = -1f;
            public float Alpha = 1f;
            public bool Reach;
            public bool ReachTongue;
            public bool ReachRight = true;
            public Vector3 ReachTarget;
            public float Reach01;
            public float Ear = float.NaN;
            public float? Fury;             // the skin: 0 cute, 1 furious (null = what Core says)
            public float SquintL;
            public float SquintR;
            public Vector2 MouthOffset;
            public float? Sit;
            public float Tilt;              // degrees added to the home's own tilt
            public float Lift;              // 0..1 off the floor (a hop: the shadow lets go)
            public Vector2 Travel;          // world: where a move between homes has got to
            public float PresenceHalfLife;  // 0 = the usual spring

            public void Clear()
            {
                Fury = null; SquintL = 0f; SquintR = 0f; MouthOffset = Vector2.zero; Sit = null; Tilt = 0f;
                Lift = 0f; PresenceHalfLife = 0f; Travel = Vector2.zero;
                Presence = null; Offset = Vector2.zero; Rot = 0f; Squash = Vector2.one; Scale = 1f;
                Head = Vector2.zero; HeadTilt = 0f; HeadSquash = 1f; LookAt = null; Look = null;
                Emotion = null; EmotionWeight = 1f; Mouth = null; MouthOpen = 1f; MouthScale = 1f;
                PuffL = 0f; PuffR = 0f; PuffTint = null; Belly = 0f; PawL = null; PawR = null;
                Lick = 0f; Blink = -1f; SparkleL = 0f; SparkleR = 0f; Glow = 0f; EarWobble = null;
                Still = false; Lid = -1f; IrisMul = 1f; CatchMul = 1f; Cheek = null; CheekA = -1f;
                Alpha = 1f; Reach = false; ReachTongue = false; ReachRight = true;
                ReachTarget = Vector3.zero; Reach01 = 0f; Ear = float.NaN;
            }
        }

        private static PetPaw Paw(float x, float y, float angle, float scale = 1f)
        {
            return new PetPaw { Offset = new Vector2(x, y), Angle = angle, Scale = scale };
        }

        // ================================================================== state in, every frame

        private bool shown;
        private bool exiting;

        /// <summary>
        /// The round as Core has it now. The first frame of an active boss starts the entrance;
        /// the first frame without one after it was shown starts the exit.
        /// </summary>
        public void Sync(TamagotchiRoundVisualState state)
        {
            State = state ?? new TamagotchiRoundVisualState();
            if (State.BossActive && !shown)
            {
                Appear();
            }
        }

        /// <summary>Starts the round's pet: the rig comes up hidden and the entrance plays.</summary>
        private void Appear()
        {
            shown = true;
            exiting = false;
            queue.Clear();
            act.Clear();
            ResetLife(State.Seed);
            rig.SetVisible(true);
            presence = 0f;
            furyNow = State.Furious ? 1f : 0f;
            ResetRoaming();
            RebuildPlates();
            if (sfx != null)
            {
                sfx.WarmTamagotchi();
            }
            Enqueue(PetPresentationPriority.Request, "entrance", Entrance, true, 0f, SkipEntrance);
        }

        /// <summary>The round is over: the pet leaves the way the round left it.</summary>
        public void Leave()
        {
            if (!shown || exiting)
            {
                return;
            }
            exiting = true;
            Enqueue(PetPresentationPriority.Punish, "exit", Exit, false, 0f, null);
        }

        /// <summary>Teardown: everything stops and the pet is gone (a new run, a lab reset).</summary>
        public void HideNow()
        {
            queue.Clear();
            act.Clear();
            ClearFoodAndFlights();
            shown = false;
            exiting = false;
            rig.SetVisible(false);
            HideDecor();
            HideHatred();
            HideSpeech();
            mealsOnTheWay.Clear();
            HideDebug();
            ViewState = PetViewState.Hidden;
        }

        public bool Shown
        {
            get { return shown; }
        }

        private void Enqueue(PetPresentationPriority priority, string name, Func<IEnumerator> routine,
            bool interruptible, float delay, Action onInterrupted)
        {
            // Whichever way a beat ends, the body is handed back to LIFE + MOOD (the springs carry
            // it there) and the state goes back to following Core.
            queue.Enqueue(new TamagotchiPresentationQueue.Item
            {
                Priority = priority,
                Name = name,
                Routine = routine,
                Interruptible = interruptible,
                NotBefore = clock + delay,
                OnInterrupted = delegate
                {
                    act.Clear();
                    ReleaseState();
                    if (onInterrupted != null)
                    {
                        onInterrupted();
                    }
                },
                OnFinished = delegate
                {
                    act.Clear();
                    ReleaseState();
                    if (priority > PetPresentationPriority.Idle)
                    {
                        ScheduleNextIdle(0.6f);
                    }
                }
            });
        }

        /// <summary>True while a beat the rules have already resolved is still being shown and
        /// the relevant UI should wait for it (the brief's input lock: a short one for a feed,
        /// the whole picture for a punish).</summary>
        public bool LocksInput
        {
            get
            {
                if (!shown)
                {
                    return false;
                }
                if (clock < lockUntil)
                {
                    return true;
                }
                // The game must not wait on a long cinematic: Core resolved the punish at once, and
                // the lock only lasts until the thing has been SEEN to go (the fury, the telegraph,
                // the bite) - it lifts while the pet is still chewing and gloating.
                TamagotchiPresentationQueue.Item current = queue.Current;
                if (current != null && current.Priority >= PetPresentationPriority.Punish && current.Name != "exit"
                    && !(stateOverridden && stateOverride == PetViewState.PunishRecover))
                {
                    return true;
                }
                foreach (TamagotchiPresentationQueue.Item item in queue.Pending)
                {
                    if (item.Priority >= PetPresentationPriority.Punish && item.Name != "exit")
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private float lockUntil;

        private void LockFor(float seconds)
        {
            lockUntil = Mathf.Max(lockUntil, clock + seconds / Mathf.Max(0.05f, PlaybackRate));
        }

        // ================================================================== the frame

        private void Update()
        {
            clock += Dt;
            if (labFast && Time.unscaledTime - labFastSince > 4f)
            {
                labFast = false; // a beat that never came: never leave the pet fast-forwarded
            }
            if (!shown)
            {
                return;
            }
            queue.Tick(clock);
            TickIdleScheduler();
            ComposePose();
            rig.UnitScale = Home.UnitScale;
            rig.Home = Home.Base;
            rig.SetClip(ClipNow);
            rig.Apply(pose);
            TickDecor();
            TickParticles();
            TickHatred();
            TickSpeech();
            TickDebug();
            ViewState = DeriveState();
        }

        private PetViewState stateOverride = PetViewState.Hidden;
        private bool stateOverridden;

        /// <summary>A beat names the state it is in; otherwise the state follows Core's stage.</summary>
        private void SetState(PetViewState state)
        {
            stateOverride = state;
            stateOverridden = true;
        }

        private void ReleaseState()
        {
            stateOverridden = false;
        }

        private PetViewState DeriveState()
        {
            if (!shown)
            {
                return PetViewState.Hidden;
            }
            if (stateOverridden && queue.Current != null)
            {
                return stateOverride;
            }
            if (drag.Active && State.Wants)
            {
                return PetViewState.TrackingFood;
            }
            switch (State.Stage)
            {
                case PetHungerStage.Satisfied: return PetViewState.Satisfied;
                case PetHungerStage.Impatient: return PetViewState.Impatient;
                case PetHungerStage.Angry: return PetViewState.Angry;
                case PetHungerStage.Furious: return PetViewState.FuriousIdle;
                default: return PetViewState.IdleHungry;
            }
        }

        // ================================================================== life

        private float presence;
        private System.Random life = new System.Random(1);
        private uint lifeSeed = 1;
        private float breathPhase;
        private float breathLength = 2.4f;
        private float breathDepth = 1f;
        private float nextBlinkAt;
        private float blinkStart = -10f;
        private bool doubleBlink;
        private PetFace face = TamagotchiExpression.For(PetEmotion.Neutral);
        private Vector2 lookNow;
        private Vector2 offsetNow;
        private float rotNow;
        private Vector2 squashNow = Vector2.one;
        private Vector2 headNow;
        private float tiltNow;
        private PetPaw pawLNow = new PetPaw { Scale = 1f };
        private PetPaw pawRNow = new PetPaw { Scale = 1f };
        private float mouthOpenNow = 1f;
        private float puffLNow;
        private float puffRNow;
        private float bellyNow;
        private float lickNow;
        private float earWobbleNow;
        private PetEmotion moodShown = PetEmotion.Neutral;
        private float furyNow;
        private float sitNow;
        private float tiltNow2;
        private float furyBeatAt;
        private float furyBeatNext;

        /// <summary>How furious it LOOKS right now (0 cute skin, 1 furious skin).</summary>
        public float FuryShown
        {
            get { return furyNow; }
        }

        /// <summary>
        /// The furious idle's body: short irregular beats (1.015 wide, 0.985 tall), a second or so
        /// apart and never the same gap twice - tension let out in jolts, never a constant jitter.
        /// </summary>
        private float FuryBeat()
        {
            if (clock >= furyBeatNext)
            {
                furyBeatAt = clock;
                furyBeatNext = clock + Rand(0.75f, 1.7f);
            }
            float since = clock - furyBeatAt;
            return since < 0.2f ? Bell(since / 0.2f) : 0f;
        }

        /// <summary>The drawing a furious mouth uses in place of a cute one: the routines name the
        /// mouths they always did, and a furious pet makes them with its own face.</summary>
        private static string FuryMouth(string mouth)
        {
            switch (mouth)
            {
                case "closed":
                case "frown":
                case "chew_a":
                case "nom":
                case "small":
                    return "snarl";
                case "smug":
                    return "grin";
                case "chew_b":
                    return "gnash";
                case "wide":
                case "furious":
                case "medium":
                    return "rage";
                default:
                    return mouth;
            }
        }

        private void ResetLife(uint seed)
        {
            lifeSeed = seed == 0 ? 1u : seed;
            life = new System.Random((int)(lifeSeed & 0x7fffffff));
            breathPhase = 0f;
            breathLength = 2.4f;
            nextBlinkAt = clock + 1.4f;
            lookNow = Vector2.zero;
            offsetNow = Vector2.zero;
            rotNow = 0f;
            squashNow = Vector2.one;
            headNow = Vector2.zero;
            tiltNow = 0f;
            pawLNow = new PetPaw { Scale = 1f };
            pawRNow = new PetPaw { Scale = 1f };
            ResetIdleHistory();
        }

        private float Rand(float a, float b)
        {
            return a + (float)life.NextDouble() * (b - a);
        }

        /// <summary>Value noise in one dimension: random knots, smoothstepped between - drift that
        /// never repeats, which is what keeps the idle from reading as a sine loop.</summary>
        private float Noise(float t, int channel)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            float a = Hash(i, channel);
            float b = Hash(i + 1, channel);
            return Mathf.Lerp(a, b, f * f * (3f - 2f * f)) * 2f - 1f;
        }

        private float Hash(int i, int channel)
        {
            unchecked
            {
                uint h = (uint)i * 374761393u + (uint)channel * 668265263u + lifeSeed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }

        /// <summary>One breath: in for 40% of it with an ease, out for the rest more slowly; the
        /// next one is a different length and depth.</summary>
        private float Breath()
        {
            breathPhase += Dt / Mathf.Max(0.5f, breathLength);
            if (breathPhase >= 1f)
            {
                breathPhase -= 1f;
                breathLength = Rand(1.8f, 3.0f);
                breathDepth = Rand(0.82f, 1.15f);
            }
            float p = breathPhase;
            float v = p < 0.4f ? Smooth(p / 0.4f) : 1f - EaseOut((p - 0.4f) / 0.6f);
            return v * breathDepth;
        }

        private PetEmotion Mood()
        {
            switch (State.Stage)
            {
                case PetHungerStage.Hungry: return PetEmotion.Hungry;
                case PetHungerStage.Impatient: return PetEmotion.Impatient;
                case PetHungerStage.Angry: return PetEmotion.Angry;
                case PetHungerStage.Furious: return PetEmotion.Furious;
                case PetHungerStage.Satisfied: return PetEmotion.Happy;
                default: return State.Pending == 1 ? PetEmotion.Hungry : PetEmotion.Neutral;
            }
        }

        /// <summary>The calm mood's face is softer than the emotion's full setting: a hungry pet
        /// at rest has its "o" small and its brows only a little lifted.</summary>
        private PetFace MoodFace(PetEmotion mood)
        {
            PetFace f = TamagotchiExpression.For(mood);
            switch (mood)
            {
                case PetEmotion.Hungry:
                    f = PetFace.Lerp(TamagotchiExpression.For(PetEmotion.Neutral), f, 0.6f);
                    f.Mouth = State.Pending == 1 ? "nom" : "closed";
                    break;
                case PetEmotion.Happy:
                    // satisfied at rest is content, not beaming: open eyes, warm cheeks
                    f = TamagotchiExpression.For(PetEmotion.Neutral);
                    f.Cheek = TamagotchiArt.CheekBright;
                    f.CheekA = 0.7f;
                    f.Lid = 0.18f;
                    break;
                case PetEmotion.Furious:
                    // furious at rest keeps its mouth shut and its teeth for the bites
                    f.Mouth = "frown";
                    f.Glow = 0.18f;
                    break;
            }
            if (State.DeadlineNext)
            {
                f.Glow = Mathf.Max(f.Glow, 0.22f);
            }
            return f;
        }

        /// <summary>Composes LIFE + MOOD + ACT (+ the drag) into the pose the rig draws.</summary>
        private void ComposePose()
        {
            float px = Home.PxToWorld;
            bool still = act.Still || LabStill;
            PetHungerStage stage = State.Stage;
            bool stiff = stage == PetHungerStage.Angry || stage == PetHungerStage.Furious;

            // ---- presence: how far out of its nest it is
            // a card being carried to a peeking pet brings it out: its mouth has to be there to feed
            float restWant = drag.Active && State.Wants ? 1f : RestPresence;
            float wantPresence = act.Presence ?? (exiting ? presence : restWant);
            presence = Damp(presence, wantPresence, act.PresenceHalfLife > 0f ? act.PresenceHalfLife : 0.045f);
            Vector2 offset = HideDirection * ((1f - presence) * Home.HideDepth);

            // ---- the skin: Core says furious, but the cute one is kept until the fury has been SHOWN
            float furyTarget = LabLegacy ? 0f : act.Fury ?? (State.Furious && !queue.Has("fury") ? 1f : 0f);
            furyNow = Damp(furyNow, furyTarget, 0.05f);
            bool furious = furyNow > 0.5f;

            // ---- life
            float breath = still || !LabBreath ? 0f : Breath();
            float amount = Tuning.BreathAmount * (stiff ? 0.6f : 1f);
            Vector2 squash = new Vector2(1f - amount * 0.5f * breath, 1f + amount * breath);
            if (furious && !still && LabBreath)
            {
                // hunched, sharp, in jolts
                float beat = FuryBeat();
                squash = new Vector2(1f + 0.015f * beat, 1f - 0.015f * beat);
            }
            float t = clock;
            float bob = still || !LabBob ? 0f : Noise(t * 0.9f, 1) * Tuning.IdleBob * px;
            float sway = still || !LabSway ? 0f : Noise(t * 0.55f, 2) * Tuning.IdleSway * (stiff ? 0.55f : 1f);
            offset.y += bob;

            // a furious pet hangs forward over whatever it is looking at
            if (furious)
            {
                offset.x += F * Px(2f) * furyNow;
                sway = sway * 0.4f + F * -1.6f * furyNow;
            }

            // sitting: it lets itself down a little and spreads
            if (sitNow > 0.01f)
            {
                offset -= HomeUp * (0.035f * S * sitNow);
                squash = Vector2.Scale(squash, new Vector2(1f + 0.025f * sitNow, 1f - 0.03f * sitNow));
            }

            // ---- act
            offset += Home.Inverted ? new Vector2(act.Offset.x, -act.Offset.y) : act.Offset;
            offset += act.Travel;
            float rot = sway + act.Rot;
            squash = Vector2.Scale(squash, act.Squash);

            // ---- the drag layer (.Plates)
            Vector2 dragLean;
            float dragRot;
            ApplyDragLayer(out dragLean, out dragRot);
            offset += dragLean;
            rot += dragRot;

            // ---- springs carry the body between sources
            offsetNow = Damp(offsetNow, offset, 0.03f);
            rotNow = Damp(rotNow, rot, 0.05f);
            squashNow = Damp(squashNow, squash, 0.025f);
            headNow = Damp(headNow, act.Head + dragHead, 0.04f);
            tiltNow = Damp(tiltNow, act.HeadTilt + dragTilt, 0.05f);

            pose.Fury = furyNow;
            pose.SquintL = act.SquintL;
            pose.SquintR = act.SquintR;
            pose.MouthOffset = act.MouthOffset;
            sitNow = Damp(sitNow, act.Sit ?? Home.Sit, 0.06f);
            pose.Sit = sitNow;
            tiltNow2 = Damp(tiltNow2, Home.Tilt + act.Tilt, 0.05f);
            pose.RootTilt = tiltNow2;
            pose.Offset = offsetNow;
            pose.Rot = rotNow;
            pose.Squash = squashNow;
            // the furious body is 3% the bigger: it came out of its burst that way and stays so
            pose.Scale = act.Scale * (1f + 0.03f * furyNow);
            pose.Head = headNow;
            pose.HeadTilt = tiltNow;
            pose.HeadSquash = act.HeadSquash;

            // ---- face: the mood, the act's emotion over it, the drag's over that
            PetEmotion mood = Mood();
            moodShown = mood;
            PetFace target = MoodFace(mood);
            if (act.Emotion.HasValue)
            {
                target = PetFace.Lerp(target, TamagotchiExpression.For(act.Emotion.Value), act.EmotionWeight);
            }
            if (dragEmotionWeight > 0.01f)
            {
                target = PetFace.Lerp(target, TamagotchiExpression.For(dragEmotion), dragEmotionWeight);
            }
            if (act.Mouth != null)
            {
                target.Mouth = act.Mouth;
            }
            else if (dragMouth != null)
            {
                target.Mouth = dragMouth;
            }
            if (act.Lid >= 0f)
            {
                target.Lid = act.Lid;
            }
            if (act.Cheek.HasValue)
            {
                target.Cheek = act.Cheek.Value;
            }
            if (act.CheekA >= 0f)
            {
                target.CheekA = act.CheekA;
            }
            if (!float.IsNaN(act.Ear))
            {
                target.Ear = act.Ear;
            }
            target.Iris *= act.IrisMul * dragIris;
            target.Catch *= act.CatchMul;
            target.MouthScale *= act.MouthScale;
            target.Glow = Mathf.Max(target.Glow, act.Glow);
            face = PetFace.Lerp(face, target, 1f - Mathf.Pow(0.5f, Dt / 0.05f));
            // THE MOUTH'S DRAWING IS NOT BLENDED. Every other layer eases toward the target a
            // little each frame; a drawing cannot, and a per-frame blend never reaches the half-way
            // point at which Lerp would switch it - so it is taken straight from the target, and
            // the rig's pop covers the swap.
            face.Mouth = target.Mouth;
            if (furious)
            {
                // the same act, made with the furious face. Its mouths are already half as wide
                // again, so a beat's own widening is taken at less than face value.
                face.Mouth = FuryMouth(target.Mouth);
                face.MouthScale = Mathf.Lerp(1f, face.MouthScale, 0.45f);
            }
            pose.Face = face;

            // ---- eyes
            Vector2 look = Vector2.zero;
            if (act.Look.HasValue)
            {
                look = act.Look.Value;
            }
            else if (act.LookAt.HasValue)
            {
                look = LookTo(act.LookAt.Value);
            }
            else if (dragLook.HasValue)
            {
                look = dragLook.Value;
            }
            else
            {
                look = RestLook();
            }
            lookNow = Damp(lookNow, look * Tuning.EyeTrackMaxOffset, act.Look.HasValue || act.LookAt.HasValue ? 0.025f : 0.06f);
            pose.Look = lookNow;
            pose.Blink = act.Blink >= 0f ? act.Blink : (still || !LabBlinks ? 0f : NaturalBlink());

            // ---- mouth, cheeks, belly, paws, tongue
            mouthOpenNow = Damp(mouthOpenNow, act.MouthOpen * dragMouthOpen, 0.02f);
            pose.MouthOpen = mouthOpenNow;
            puffLNow = Damp(puffLNow, act.PuffL, 0.03f);
            puffRNow = Damp(puffRNow, act.PuffR, 0.03f);
            pose.PuffL = puffLNow;
            pose.PuffR = puffRNow;
            pose.PuffTint = act.PuffTint;
            bellyNow = Damp(bellyNow, act.Belly, 0.03f);
            pose.Belly = bellyNow;
            PetPaw wantL = act.PawL ?? dragPawL ?? IdlePaw(true);
            PetPaw wantR = act.PawR ?? dragPawR ?? IdlePaw(false);
            pawLNow = DampPaw(pawLNow, wantL, 0.035f);
            pawRNow = DampPaw(pawRNow, wantR, 0.035f);
            pose.PawL = pawLNow;
            pose.PawR = pawRNow;
            lickNow = Damp(lickNow, act.Lick, 0.025f);
            pose.Lick = lickNow;
            earWobbleNow = Damp(earWobbleNow, act.EarWobble ?? (stiff ? 0f : Noise(t * 1.3f, 3) * 3.5f), 0.06f);
            pose.EarWobble = earWobbleNow;
            pose.SparkleL = act.SparkleL;
            pose.SparkleR = act.SparkleR;
            pose.GlowExtra = act.Glow;
            pose.Alpha = act.Alpha;
            pose.ShadowA = Home.Shadow ? Mathf.Clamp01(presence * 1.2f - 0.1f) * act.Alpha * (1f - act.Lift) : 0f;
            pose.Tint = Color.white;
            pose.Reach = act.Reach;
            pose.ReachTongue = act.ReachTongue;
            pose.ReachRightPaw = act.ReachRight;
            pose.ReachTarget = act.ReachTarget;
            pose.Reach01 = act.Reach01;
        }

        private PetPaw DampPaw(PetPaw now, PetPaw want, float halfLife)
        {
            return new PetPaw
            {
                Offset = Damp(now.Offset, want.Offset, halfLife),
                Angle = Damp(now.Angle, want.Angle, halfLife),
                Scale = Damp(now.Scale <= 0f ? 1f : now.Scale, want.Scale <= 0f ? 1f : want.Scale, halfLife)
            };
        }

        /// <summary>The paws at rest: a little lower and in when it is calm, tucked when it is
        /// angry, clenched when it is furious.</summary>
        private PetPaw IdlePaw(bool left)
        {
            switch (State.Stage)
            {
                case PetHungerStage.Angry:
                    return Paw(left ? 0.02f : -0.02f, -0.02f, -12f);
                case PetHungerStage.Furious:
                    // out from the body and down: shoulders up, claws ready
                    return Paw(left ? -0.03f : 0.03f, -0.02f, -30f, 1.04f);
                default:
                    // sitting, its paws rest on its belly
                    if (sitNow > 0.5f)
                    {
                        return BellyPaw(!left);
                    }
                    // a little below level: arms held straight out are a scarecrow's
                    return Paw(0f, 0f, -18f);
            }
        }

        private float NaturalBlink()
        {
            if (clock >= nextBlinkAt)
            {
                blinkStart = clock;
                doubleBlink = life.NextDouble() < 0.18;
                nextBlinkAt = clock + Rand(2.2f, 5.5f);
            }
            float since = clock - blinkStart;
            float b = BlinkCurve(since);
            if (doubleBlink)
            {
                b = Mathf.Max(b, BlinkCurve(since - 0.2f));
            }
            return b;
        }

        /// <summary>A blink: shut in 50 ms, open in 70.</summary>
        private static float BlinkCurve(float since)
        {
            if (since < 0f || since > 0.12f)
            {
                return 0f;
            }
            return since < 0.05f ? since / 0.05f : 1f - (since - 0.05f) / 0.07f;
        }

        /// <summary>Where the eyes rest when nothing is asking them to look: at the hand the
        /// player is over (the "card track" idle, always on), else a little toward the board.</summary>
        private Vector2 RestLook()
        {
            if (Anchors.HandFocus.HasValue && State.Stage != PetHungerStage.Satisfied)
            {
                return LookTo(Anchors.HandFocus.Value) * 0.75f;
            }
            return LookTo(Anchors.BoardCentre) * 0.4f;
        }

        /// <summary>The look (-1..1 per axis) that points both eyes at a world point.</summary>
        private Vector2 LookTo(Vector3 world)
        {
            Vector3 eyes = (rig.EyeWorld(true) + rig.EyeWorld(false)) * 0.5f;
            Vector2 d = (Vector2)(world - eyes);
            float m = d.magnitude;
            if (m < 0.0001f)
            {
                return Vector2.zero;
            }
            float reach = Mathf.Clamp01(m / Mathf.Max(0.2f, 1.3f * S));
            return ToLocal(d / m) * Mathf.Lerp(0.35f, 1f, reach);
        }

        /// <summary>The head turning a little toward a point (used with LookAt).</summary>
        private void HeadToward(Vector3 world, float amount)
        {
            Vector2 d = (Vector2)(world - rig.MouthWorld);
            if (d.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector2 n = ToLocal(d.normalized);
            act.Head = n * 0.022f * amount;
            act.HeadTilt = -n.x * 4f * amount;
        }

        /// <summary>A world direction in the pet's own frame: it may be leaning in round a side of
        /// the screen or hanging over the top of it, and its eyes, head and paws are told where to
        /// go in ITS terms.</summary>
        private Vector2 ToLocal(Vector2 world)
        {
            float a = -tiltNow2 * Mathf.Deg2Rad;
            float c = Mathf.Cos(a);
            float s = Mathf.Sin(a);
            return new Vector2(world.x * c - world.y * s, world.x * s + world.y * c);
        }

        /// <summary>The pet's own "up" where it lives (its home's tilt).</summary>
        private Vector2 HomeUp
        {
            get
            {
                float a = Home.Tilt * Mathf.Deg2Rad;
                return new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
            }
        }

        /// <summary>The way it goes to hide behind its edge.</summary>
        private Vector2 HideDirection
        {
            get { return Home.HideDir.sqrMagnitude > 0.0001f ? Home.HideDir.normalized : Vector2.down; }
        }

        /// <summary>How far out it sits when nothing asks for more (a peek home shows a head).</summary>
        private float RestPresence
        {
            get { return Mathf.Clamp(Home.Rest, 0.05f, 1f); }
        }

        /// <summary>The direction toward the board, in x.</summary>
        private float F
        {
            get { return Home.Facing >= 0 ? 1f : -1f; }
        }
    }
}
