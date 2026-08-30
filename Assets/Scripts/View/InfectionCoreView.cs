// PURPOSE: What "Enfeksiyon" looks like on the board - a small living bioluminescent core
// latched inside an infected cell, feeding on the block that sits on it. Three turns of the SAME
// block and it takes the whole block; every turn it swallows one of the three spores around it,
// grows, and its pulse quickens.
//
// IT REPLACED A GREEN CELL AND THREE SQUARES. The old look tinted the entire cell renderer green
// every frame and hung three flat pips under it. That is a status effect with a progress bar
// bolted to it: the cell stopped being a board slot, the pips were HUD, and neither said anything
// about what the joker actually does. Nothing here paints a cell. The slot keeps its own dark
// surface and the infection is an OBJECT living inside it.
//
// THE VISUAL STATE IS DERIVED, NEVER STORED IN THE RULES. Everything below is read off what Core
// already exposes - the turn count, the threshold, and what cube is standing on the cell - and
// compared against what this view saw last time. A turn that advanced plays an absorption; a
// different SourceCardId plays a re-latch; an empty cell goes dormant. The joker does not know
// this file exists and none of its numbers are touched.
//
// THE BLOCK LINK IS THE MECHANIC MADE VISIBLE. What makes this joker odd is that it tracks one
// BLOCK, not one cell - so when a turn ticks, a short signal runs from the core out along the
// other cubes of that same block and is gone in a sixth of a second. It must never look like the
// infection SPREAD there: it is a confirmation, not a claim.
//
// EVERYTHING IS GENERATED AND POOLED. Three small sprites are built once and shared; a cell's
// twelve renderers are reused across refreshes, and Update allocates nothing. Several cells can
// be infected at once after the plus spreads, so this has to stay cheap.

using System.Collections.Generic;
using UnityEngine;
using ProjectBlock.Core;

namespace ProjectBlock.View
{
    /// <summary>The bioluminescent cores in infected cells. Fed by BoardView.ShowInfections.</summary>
    public sealed class InfectionCoreView : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ---- the core ----
            /// <summary>Core diameter at stage 0, in CELLS. Small: it lives inside the slot, it
            /// does not replace it.</summary>
            public static float CoreBaseScale = 0.155f;

            /// <summary>How much bigger the core is per stage swallowed. SMALL - a tenth of the
            /// base each time, so stage 2 is a fifth larger than stage 0 and no more. Size used
            /// to double across the three and it was carrying the whole read; the stages are
            /// told apart by the NODE COUNT, the pulse rate and the glow, and leaving size to do
            /// it as well made the core swell out of its slot.</summary>
            public static float CoreStageScale = 0.0155f;

            /// <summary>The soft light around the core, as a multiple of its own size.</summary>
            public static float AuraScale = 3.4f;

            public static float AuraAlpha = 0.30f;

            /// <summary>How much the core breathes, as a fraction of its size.</summary>
            public static float IdleBreath = 0.085f;

            /// <summary>Breathing speed per stage. Calm at rest, quickening as it feeds - this
            /// is the heartbeat the player reads the countdown off.</summary>
            public static float IdlePulseSpeed = 1.15f;

            public static float Stage1PulseSpeed = 2.5f;

            public static float Stage2PulseSpeed = 5.0f;

            public static float GlowStrength = 1f;

            /// <summary>Extra aura per stage. The stages have to separate on more than size, or
            /// stage 1 and stage 2 are the same picture at different scales.</summary>
            public static float AuraStageGain = 0.16f;

            /// <summary>The flicker that only stage 2 has - the last turn before it takes the
            /// block, and the one cue that says "this is about to go".</summary>
            public static float Stage2Flicker = 0.13f;

            public static float Stage2FlickerSpeed = 17f;

            /// <summary>How much the core breathes UNEVENLY - x and y out of phase, so it is not
            /// the same circle every frame. Small: this is life, not wobble.</summary>
            public static float BreathAsymmetry = 0.45f;

            /// <summary>How far apart successive cores start in their cycle. An irrational-ish
            /// fraction, so a plus of five never lines back up.</summary>
            public static float PhaseStagger = 0.37f;

            /// <summary>Degrees per second the core's lumpy rim turns.</summary>
            public static float CoreSpin = 14f;

            /// <summary>How far a filament flexes off its spoke, in degrees. A straight arm that
            /// never moves reads as a diagram; this is what makes it hold ON to something.</summary>
            public static float FilamentFlex = 11f;

            // ---- filaments ----
            /// <summary>How many arms hold the core to its cell.</summary>
            public static int FilamentCount = 5;

            public static float FilamentLength = 0.40f;

            public static float FilamentWidth = 0.055f;

            public static float FilamentOpacity = 0.42f;

            /// <summary>Extra filament opacity per stage. Barely there at rest, active by the
            /// last turn - the arms feed with the core rather than sitting at one brightness.</summary>
            public static float FilamentStageGain = 0.13f;

            /// <summary>How fast energy runs out and back along an arm.</summary>
            public static float FilamentPulseSpeed = 1.9f;

            // ---- the three spores ----
            public static float SporeOrbit = 0.325f;

            public static float SporeSize = 0.085f;

            public static float SporeAlpha = 0.82f;

            /// <summary>Where the three spores sit, in degrees, and how far out each one is as
            /// a multiple of the orbit. DELIBERATELY UNEVEN. Three equal shapes at 120 degrees
            /// around a middle is a trefoil - a clover - and no amount of shrinking them fixes
            /// that, because the read comes from the SYMMETRY and not from the size. Broken up
            /// they go back to being three satellites of something. The gaps have to be
            /// GENUINELY uneven - 118/126/116 still reads as 120, which is what a first attempt
            /// at this produced.</summary>
            public static float[] SporeAngles = { -102f, 6f, 163f };

            public static float[] SporeRadii = { 1.06f, 0.78f, 1.18f };

            /// <summary>The same problem, and the same fix, for the arms: five equal spokes is a
            /// flower. These are the base angles and the length of each.</summary>
            public static float[] FilamentAngles = { 9f, 63f, 149f, 206f, 301f };

            public static float[] FilamentLengths = { 1.05f, 0.64f, 1.18f, 0.76f, 0.92f };

            /// <summary>How long a spore takes to spiral in and be swallowed.</summary>
            public static float SporeAbsorbSeconds = 0.24f;

            public static float AbsorbFlashStrength = 0.9f;

            /// <summary>Fraction of the absorption the node spends brightening BEFORE it moves.</summary>
            public static float AbsorbHold = 0.30f;

            /// <summary>How much it swells while it charges.</summary>
            public static float AbsorbSwell = 0.55f;

            /// <summary>How far round the core the swallowed spore travels on its way in. A
            /// quarter turn, not most of one: it is being drawn in, not orbiting.</summary>
            public static float AbsorbSpiral = 95f;

            // ---- the block link ----
            public static float BlockLinkSeconds = 0.18f;

            public static float BlockLinkOpacity = 0.42f;

            public static float BlockLinkWidth = 0.045f;

            /// <summary>Most cubes of one block a signal is drawn to. A block is at most five
            /// cubes; the cap only guards against a reshape making one enormous.</summary>
            public static int BlockLinkMax = 6;

            // ---- reset ----
            public static float ResetSeconds = 0.22f;

            // ---- dormant ----
            /// <summary>What is left when the cell is empty: it is still infected, it is just not
            /// being fed, and that has to read as waiting rather than as gone.</summary>
            public static float DormantIntensity = 0.32f;

            public static float DormantScale = 0.72f;

            public static float DormantPulseSpeed = 0.7f;

            // ---- pre-detonation ----
            /// <summary>The silence before it takes the block. Short, and the whole reason the
            /// two heartbeats after it land.</summary>
            public static float PreDetonationHold = 0.085f;

            public static float PreDetonationBeat = 0.075f;

            public static float PreDetonationSwell = 0.65f;

            // ---- the plus spread ----
            public static float SpreadTendrilSeconds = 0.16f;

            public static float SpreadBloomSeconds = 0.26f;

            public static float SpreadTendrilWidth = 0.055f;

            // ---- motes ----
            public static int MoteCount = 3;

            public static float MoteSize = 0.045f;

            public static float MoteOrbit = 0.36f;

            public static float MoteSpeed = 0.55f;

            public static float MoteAlpha = 0.40f;

            // ---- palette ----
            /// <summary>CLEAN and cyan-leaning, so it never reads as the Kangren boss - that is a
            /// dirty, dead green and this is a joker working FOR the player. No olive, no brown,
            /// no rotting yellow anywhere in here.</summary>
            public static Color CoreColor = new Color(0.78f, 1f, 0.92f);

            public static Color GlowColor = new Color(0.20f, 1f, 0.62f);

            public static Color SporeColor = new Color(0.48f, 1f, 0.80f);
        }

        private const int AuraOrder = 6;

        private const int FilamentOrder = 7;

        private const int SporeOrder = 8;

        private const int CoreOrder = 9;

        // =================================================================== shared art

        private static Sprite coreSprite;

        private static Sprite blobSprite;

        private static Sprite dotSprite;

        private static Sprite streakSprite;

        /// <summary>The CORE body: opaque in the middle with a narrow soft rim, and a rim that
        /// wanders so it is not a drawn circle. Crisp on purpose - the body and the aura used to
        /// be the same soft blob, which is exactly why the whole thing read as a smudge of light
        /// instead of as a thing with a middle. The aura is what is allowed to be soft.</summary>
        private static Sprite CoreSprite()
        {
            if (coreSprite != null)
            {
                return coreSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    float edge = 1f + 0.085f * Mathf.Sin(ang * 3f + 0.4f)
                        + 0.05f * Mathf.Sin(ang * 5f + 2.2f);
                    float k = r / Mathf.Max(edge, 0.2f);
                    // Solid out to about two thirds, then a short fade - the fade is 22% of the
                    // radius, not the whole of it.
                    float a = Mathf.Sqrt(Mathf.Clamp01((0.88f - k) / 0.22f));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            coreSprite = Bake(px, n);
            return coreSprite;
        }

        /// <summary>The AURA: soft, wide and low, sitting under the core.</summary>
        private static Sprite BlobSprite()
        {
            if (blobSprite != null)
            {
                return blobSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    float edge = 1f + 0.11f * Mathf.Sin(ang * 3f) + 0.07f * Mathf.Sin(ang * 5f + 1.1f);
                    float k = r / Mathf.Max(edge, 0.2f);
                    float a = Mathf.Exp(-3.6f * k * k);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            blobSprite = Bake(px, n);
            return blobSprite;
        }

        /// <summary>A plain soft dot: spores, motes and the absorption flash.</summary>
        private static Sprite DotSprite()
        {
            if (dotSprite != null)
            {
                return dotSprite;
            }
            const int n = 32;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Exp(-4f * (u * u + v * v));
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            dotSprite = Bake(px, n);
            return dotSprite;
        }

        /// <summary>A tapered streak, pivoted at its LEFT end so it can be scaled out from the
        /// core: filaments, the block link and the spread tendrils are all this.</summary>
        private static Sprite StreakSprite()
        {
            if (streakSprite != null)
            {
                return streakSprite;
            }
            const int w = 64;
            const int h = 16;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;                     // 0 at the root, 1 at the tip
                    float v = ((y + 0.5f) / h - 0.5f) * 2f;
                    // Tapers to nothing at the tip and fades across, so no end is a hard edge.
                    float thick = 1f - u * 0.85f;
                    float a = Mathf.Exp(-3.2f * (v / Mathf.Max(thick, 0.05f))
                        * (v / Mathf.Max(thick, 0.05f)));
                    a *= Mathf.Clamp01(1f - u * u * 0.9f);
                    px[y * w + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            // Pivot at the left edge: scale.x is then the arm's LENGTH from the core outward.
            streakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), w);
            return streakSprite;
        }

        private static Sprite Bake(Color32[] px, int n)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        // =================================================================== one infected cell

        private sealed class Core
        {
            public GameObject Root;
            public SpriteRenderer Body;
            public SpriteRenderer Aura;
            public SpriteRenderer Flash;
            public SpriteRenderer[] Spores;
            public SpriteRenderer[] Filaments;
            public SpriteRenderer[] Motes;
            public SpriteRenderer[] Links;

            public GridPos Cell;
            public Vector2 At;
            public bool Live;

            // ---- what the rules say, as of the last refresh ----
            public int Turns;
            public int Threshold;
            public int CardId;
            public bool HasCube;

            // ---- what the visuals are doing about it ----
            public float Phase;
            public float AbsorbTimer;
            public int AbsorbIndex;
            public float ResetTimer;
            public float LinkTimer;
            public int LinkCount;
            public float SpreadTimer;
            public Vector2 SpreadFrom;
            public float ChargeTimer;
            public bool Charging;
        }

        private readonly List<Core> cores = new List<Core>();

        private float cellSize = 1f;

        /// <summary>How many cores have been started, for the phase stagger.</summary>
        private int spawnCount;

        private bool built;

        /// <summary>Reused every refresh so SetCells allocates nothing per turn.</summary>
        private readonly List<Vector2> linkBuffer = new List<Vector2>();

        // =================================================================== driving it

        public void Build(float cell)
        {
            cellSize = cell;
            built = true;
        }

        /// <summary>
        /// The infected cells as the rules currently see them. Everything visual is worked out by
        /// comparing this against the last call: a turn that went up plays an absorption, a
        /// different block plays a re-latch, an empty cell goes dormant, and a cell that was not
        /// here before and sits beside one that was is a plus-spread seed.
        /// </summary>
        public void SetCells(IReadOnlyList<InfectedCell> cells, GameBoard board,
            System.Func<GridPos, Vector2> toWorld, float cell)
        {
            cellSize = cell;
            built = true;
            for (int i = 0; i < cores.Count; i++)
            {
                cores[i].Live = false;
            }
            if (cells == null || board == null || toWorld == null)
            {
                Retire();
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                InfectedCell inf = cells[i];
                if (!board.IsInside(inf.Cell))
                {
                    continue;
                }
                Cube? cube = board.GetCube(inf.Cell);
                int cardId = cube.HasValue ? cube.Value.SourceCardId : -1;
                Core c = Find(inf.Cell);
                bool fresh = c == null;
                if (fresh)
                {
                    c = Take();
                    c.Cell = inf.Cell;
                    c.Turns = inf.Turns;
                    c.CardId = cardId;
                    // Spread rather than randomised: each new core starts a fixed fraction of a
                    // cycle behind the last, so five of them on the board never beat together
                    // and never look shuffled either.
                    c.Phase = spawnCount * Style.PhaseStagger;
                    spawnCount++;
                    // A cell that appears next to one already infected is the plus spread
                    // arriving; anything else is the player's own first infection.
                    Core parent = NeighbourCore(inf.Cell);
                    if (parent != null)
                    {
                        c.SpreadTimer = Style.SpreadTendrilSeconds + Style.SpreadBloomSeconds;
                        c.SpreadFrom = parent.At;
                    }
                }
                c.Live = true;
                c.At = toWorld(inf.Cell);
                c.Root.transform.localPosition = new Vector3(c.At.x, c.At.y, 0f);
                c.Threshold = Mathf.Max(inf.Threshold, 1);
                c.HasCube = cube.HasValue;

                if (!fresh)
                {
                    if (cardId != c.CardId)
                    {
                        // The block it was feeding on is gone. Not a punishment - a re-latch.
                        c.ResetTimer = Style.ResetSeconds;
                        c.AbsorbTimer = 0f;
                    }
                    else if (inf.Turns > c.Turns)
                    {
                        // Another turn on the same block: swallow the next spore, and signal
                        // down the block to say that is what it is tracking.
                        c.AbsorbTimer = Style.SporeAbsorbSeconds;
                        c.AbsorbIndex = Mathf.Clamp(inf.Turns - 1, 0, c.Spores.Length - 1);
                        c.LinkTimer = Style.BlockLinkSeconds;
                        BuildLinks(c, board, toWorld, cardId);
                    }
                }
                c.CardId = cardId;
                c.Turns = inf.Turns;
            }
            Retire();
        }

        /// <summary>The short charge before the block is taken: a beat of silence, then two fast
        /// heartbeats. The caller fires the blast when it is done.</summary>
        public float PlayCharge(GridPos cell)
        {
            Core c = Find(cell);
            float length = Style.PreDetonationHold + Style.PreDetonationBeat * 2f;
            if (c != null)
            {
                c.Charging = true;
                c.ChargeTimer = 0f;
            }
            return length;
        }

        public void Clear()
        {
            for (int i = 0; i < cores.Count; i++)
            {
                cores[i].Live = false;
            }
            Retire();
        }

        // =================================================================== internals

        private Core Find(GridPos cell)
        {
            for (int i = 0; i < cores.Count; i++)
            {
                if (cores[i].Root.activeSelf && cores[i].Cell.Equals(cell))
                {
                    return cores[i];
                }
            }
            return null;
        }

        /// <summary>An already-infected cell orthogonally beside this one - the plus spread's
        /// parent. Diagonals are deliberately not considered: the rule only grows in a plus.</summary>
        private Core NeighbourCore(GridPos cell)
        {
            for (int i = 0; i < cores.Count; i++)
            {
                Core c = cores[i];
                if (!c.Root.activeSelf)
                {
                    continue;
                }
                int dx = Mathf.Abs(c.Cell.X - cell.X);
                int dy = Mathf.Abs(c.Cell.Y - cell.Y);
                if (dx + dy == 1)
                {
                    return c;
                }
            }
            return null;
        }

        private void BuildLinks(Core c, GameBoard board, System.Func<GridPos, Vector2> toWorld,
            int cardId)
        {
            linkBuffer.Clear();
            if (cardId < 0)
            {
                c.LinkCount = 0;
                return;
            }
            for (int x = 0; x < board.Width && linkBuffer.Count < Style.BlockLinkMax; x++)
            {
                for (int y = 0; y < board.Height && linkBuffer.Count < Style.BlockLinkMax; y++)
                {
                    var pos = new GridPos(board.MinX + x, board.MinY + y);
                    if (pos.Equals(c.Cell) || !board.IsInside(pos))
                    {
                        continue;
                    }
                    Cube? cube = board.GetCube(pos);
                    if (cube.HasValue && cube.Value.SourceCardId == cardId)
                    {
                        linkBuffer.Add(toWorld(pos));
                    }
                }
            }
            // The LAST slot is reserved for the spread tendril, which draws itself there - two
            // things writing one renderer would have the tendril flicker on a five-cube block.
            c.LinkCount = Mathf.Min(linkBuffer.Count, c.Links.Length - 1);
            for (int i = 0; i < c.LinkCount; i++)
            {
                Vector2 d = linkBuffer[i] - c.At;
                float len = d.magnitude;
                Transform t = c.Links[i].transform;
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(len, Style.BlockLinkWidth * cellSize, 1f);
            }
        }

        private Core Take()
        {
            for (int i = 0; i < cores.Count; i++)
            {
                if (!cores[i].Root.activeSelf)
                {
                    cores[i].Root.SetActive(true);
                    Reset(cores[i]);
                    return cores[i];
                }
            }
            Core made = Create();
            cores.Add(made);
            return made;
        }

        private static void Reset(Core c)
        {
            c.AbsorbTimer = 0f;
            c.ResetTimer = 0f;
            c.LinkTimer = 0f;
            c.LinkCount = 0;
            c.SpreadTimer = 0f;
            c.ChargeTimer = 0f;
            c.Charging = false;
        }

        private Core Create()
        {
            var root = new GameObject("Infection");
            root.transform.SetParent(transform, false);
            var c = new Core
            {
                Root = root,
                Spores = new SpriteRenderer[3],
                Filaments = new SpriteRenderer[Style.FilamentCount],
                Motes = new SpriteRenderer[Style.MoteCount],
                Links = new SpriteRenderer[Style.BlockLinkMax]
            };
            c.Aura = Make(root.transform, "Aura", BlobSprite(), AuraOrder);
            for (int i = 0; i < c.Filaments.Length; i++)
            {
                c.Filaments[i] = Make(root.transform, "Filament" + i, StreakSprite(),
                    FilamentOrder);
            }
            for (int i = 0; i < c.Links.Length; i++)
            {
                c.Links[i] = Make(root.transform, "Link" + i, StreakSprite(), FilamentOrder);
            }
            for (int i = 0; i < c.Motes.Length; i++)
            {
                c.Motes[i] = Make(root.transform, "Mote" + i, DotSprite(), SporeOrder);
            }
            for (int i = 0; i < c.Spores.Length; i++)
            {
                c.Spores[i] = Make(root.transform, "Spore" + i, DotSprite(), SporeOrder);
            }
            c.Body = Make(root.transform, "Body", CoreSprite(), CoreOrder);
            c.Flash = Make(root.transform, "Flash", DotSprite(), CoreOrder);
            return c;
        }

        private static SpriteRenderer Make(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Retire()
        {
            for (int i = 0; i < cores.Count; i++)
            {
                if (!cores[i].Live && cores[i].Root.activeSelf)
                {
                    cores[i].Root.SetActive(false);
                }
            }
        }

        // =================================================================== the clock

        private void Update()
        {
            if (!built)
            {
                return;
            }
            float dt = Time.deltaTime;
            for (int i = 0; i < cores.Count; i++)
            {
                Core c = cores[i];
                if (!c.Root.activeSelf)
                {
                    continue;
                }
                Tick(c, dt);
            }
        }

        /// <summary>The pulse shape: a sine with a touch of its own second harmonic, which
        /// gives a quick rise, a peak and a longer settle. Normalised so it still runs -1..1.</summary>
        private static float Beat(float phase)
        {
            float x = phase * Mathf.PI * 2f;
            return (Mathf.Sin(x) + 0.30f * Mathf.Sin(x * 2f)) / 1.17f;
        }

        private void Tick(Core c, float dt)
        {
            int stage = Mathf.Clamp(c.Turns, 0, 3);
            bool dormant = !c.HasCube;

            // ---- the heartbeat. Its SPEED is the countdown: calm at rest, quicker each turn.
            float speed = dormant ? Style.DormantPulseSpeed
                : stage <= 0 ? Style.IdlePulseSpeed
                : stage == 1 ? Style.Stage1PulseSpeed : Style.Stage2PulseSpeed;
            c.Phase += dt * speed;
            // Not a pure sine: a second harmonic sharpens the peak and flattens the trough, so
            // it reads as a beat rather than as a wave. A clean sine is what made this look
            // mechanical however fast it ran.
            float wave = Beat(c.Phase);
            float breath = 1f + Style.IdleBreath * wave;
            // The second axis breathes out of phase with the first, so the core is never the
            // same circle twice - which is most of the difference between "organism" and
            // "glow dot". Stage 2 also gets a fast flicker nothing else has.
            float breathY = 1f + Style.IdleBreath * Style.BreathAsymmetry
                * Beat(c.Phase + 0.30f);
            float flicker = stage >= 2 && !dormant
                ? 1f + Style.Stage2Flicker
                    * Mathf.Sin(Time.time * Style.Stage2FlickerSpeed) * 0.5f
                : 1f;

            // ---- timers
            if (c.AbsorbTimer > 0f) c.AbsorbTimer -= dt;
            if (c.ResetTimer > 0f) c.ResetTimer -= dt;
            if (c.LinkTimer > 0f) c.LinkTimer -= dt;
            if (c.SpreadTimer > 0f) c.SpreadTimer -= dt;
            if (c.Charging) c.ChargeTimer += dt;

            float absorb = c.AbsorbTimer > 0f
                ? 1f - c.AbsorbTimer / Style.SporeAbsorbSeconds : 1f;
            float reset = c.ResetTimer > 0f ? c.ResetTimer / Style.ResetSeconds : 0f;

            // ---- the charge before it takes the block: silence, then two fast beats.
            float charge = 0f;
            if (c.Charging)
            {
                float t = c.ChargeTimer - Style.PreDetonationHold;
                if (t > 0f)
                {
                    float beat = t / Style.PreDetonationBeat;
                    charge = Mathf.Abs(Mathf.Sin(beat * Mathf.PI)) * Style.PreDetonationSwell;
                    // TWO beats: one full sine hump is one beat, so the cut is at 2 and not at
                    // 4 - at 4 it beat four times and held the blast back for 0.385s.
                    if (beat > 2f)
                    {
                        c.Charging = false;
                        c.ChargeTimer = 0f;
                    }
                }
                else
                {
                    // The silence: it stops breathing entirely, which is what makes the two
                    // beats after it land.
                    breath = 1f - Style.IdleBreath;
                }
            }

            // ---- intensity, which everything else is scaled by
            float intensity = dormant ? Style.DormantIntensity : 1f;
            intensity *= 1f - reset * 0.75f;
            float spreadIn = c.SpreadTimer > 0f
                ? Mathf.Clamp01(1f - c.SpreadTimer / Style.SpreadBloomSeconds) : 1f;
            intensity *= spreadIn;

            // ---- the core body
            float size = cellSize * (Style.CoreBaseScale + Style.CoreStageScale * stage)
                * (dormant ? Style.DormantScale : 1f) * (1f + charge) * flicker
                * (0.35f + 0.65f * spreadIn);
            c.Body.transform.localScale = new Vector3(size * breath, size * breathY, 1f);
            c.Body.transform.localRotation =
                Quaternion.Euler(0f, 0f, c.Phase * Style.CoreSpin);
            Color body = Style.CoreColor;
            body.a = Style.GlowStrength * intensity;
            c.Body.color = body;

            float auraSize = size * Style.AuraScale * breath;
            c.Aura.transform.localScale = new Vector3(auraSize, auraSize, 1f);
            Color aura = Style.GlowColor;
            // The aura gains with the stage as well as the core, so the three states differ in
            // brightness and not only in size.
            aura.a = (Style.AuraAlpha + Style.AuraStageGain * stage) * intensity
                * (1f + charge * 0.8f) * flicker;
            c.Aura.color = aura;

            // The swallow flash, on the frame the spore arrives.
            float flash = c.AbsorbTimer > 0f
                ? Mathf.Clamp01(1f - Mathf.Abs(absorb - 0.92f) / 0.10f) : 0f;
            Color fc = Style.CoreColor;
            fc.a = flash * Style.AbsorbFlashStrength;
            c.Flash.color = fc;
            float fs = size * (1.4f + 2.2f * flash);
            c.Flash.transform.localScale = new Vector3(fs, fs, 1f);

            TickFilaments(c, dormant ? 0 : stage, intensity, charge, reset);
            TickSpores(c, stage, absorb, intensity);
            TickMotes(c, intensity);
            TickLinks(c);
            TickSpread(c);
        }

        /// <summary>The arms holding the core into its cell, with energy running out and back.</summary>
        private void TickFilaments(Core c, int stage, float intensity, float charge, float reset)
        {
            for (int i = 0; i < c.Filaments.Length; i++)
            {
                float flow = 0.5f + 0.5f * Mathf.Sin(
                    c.Phase * Style.FilamentPulseSpeed * Mathf.PI * 2f + i * 1.7f);
                // Flexes off its spoke rather than sitting on it. A fixed spoke is a diagram,
                // and five EVEN spokes are a flower - hence the uneven table.
                float ang = Style.FilamentAngles[i % Style.FilamentAngles.Length]
                    + Style.FilamentFlex * Mathf.Sin(
                        c.Phase * 0.8f * Mathf.PI * 2f + i * 2.3f);
                // On a re-latch the arms let go first and take hold again.
                float len = cellSize * Style.FilamentLength
                    * Style.FilamentLengths[i % Style.FilamentLengths.Length]
                    * (0.7f + 0.3f * flow) * (1f - reset * 0.8f) * (1f + charge * 0.5f);
                Transform t = c.Filaments[i].transform;
                t.localRotation = Quaternion.Euler(0f, 0f, ang);
                t.localScale = new Vector3(len, Style.FilamentWidth * cellSize, 1f);
                Color col = Style.GlowColor;
                col.a = (Style.FilamentOpacity + Style.FilamentStageGain * stage) * intensity
                    * (0.55f + 0.45f * flow) * (1f + charge);
                c.Filaments[i].color = col;
            }
        }

        /// <summary>The three spores. One is swallowed per turn - it spirals in, and the ones
        /// already taken are simply not there.</summary>
        private void TickSpores(Core c, int stage, float absorb, float intensity)
        {
            for (int i = 0; i < c.Spores.Length; i++)
            {
                bool eaten = i < stage;
                bool eating = c.AbsorbTimer > 0f && i == c.AbsorbIndex;
                if (eaten && !eating)
                {
                    c.Spores[i].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                float ang = Style.SporeAngles[i % Style.SporeAngles.Length];
                float raw = eating ? absorb : 0f;
                // It BRIGHTENS WHERE IT STANDS first, and only then is drawn in. Moving from the
                // first frame reads as the node being deleted; the beat of charge before it
                // moves is what makes the tick feel earned.
                float hold = Style.AbsorbHold;
                float t = raw <= hold ? 0f : (raw - hold) / (1f - hold);
                float charging = raw <= hold ? raw / Mathf.Max(hold, 0.0001f) : 1f;
                ang += Style.AbsorbSpiral * t * t;
                float radius = cellSize * Style.SporeOrbit
                    * Style.SporeRadii[i % Style.SporeRadii.Length] * (1f - t * t);
                float rad = ang * Mathf.Deg2Rad;
                c.Spores[i].transform.localPosition = new Vector3(
                    Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius, 0f);
                float s = cellSize * Style.SporeSize * (1f - 0.45f * t)
                    * (1f + 0.22f * Beat(c.Phase + i * 0.33f))
                    * (1f + Style.AbsorbSwell * charging * (1f - t));
                c.Spores[i].transform.localScale = new Vector3(s, s, 1f);
                Color col = Style.SporeColor;
                col.a = Style.SporeAlpha * intensity
                    * (eating ? Mathf.Lerp(1f, 1.6f, charging) * (1f - t * 0.35f) : 1f);
                c.Spores[i].color = col;
            }
        }

        private void TickMotes(Core c, float intensity)
        {
            for (int i = 0; i < c.Motes.Length; i++)
            {
                float ang = (c.Phase * Style.MoteSpeed + i * 2.4f) * Mathf.PI * 2f;
                float radius = cellSize * Style.MoteOrbit
                    * (0.55f + 0.45f * Mathf.Sin(ang * 0.7f + i));
                c.Motes[i].transform.localPosition = new Vector3(
                    Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius * 0.8f, 0f);
                float s = cellSize * Style.MoteSize;
                c.Motes[i].transform.localScale = new Vector3(s, s, 1f);
                Color col = Style.SporeColor;
                col.a = Style.MoteAlpha * intensity
                    * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(ang * 0.5f)));
                c.Motes[i].color = col;
            }
        }

        /// <summary>The signal that runs down the block it is tracking. Gone in a sixth of a
        /// second, and it leaves nothing - it says "I know what I am holding", not "I am here".</summary>
        private void TickLinks(Core c)
        {
            float k = c.LinkTimer > 0f ? c.LinkTimer / Style.BlockLinkSeconds : 0f;
            for (int i = 0; i < c.Links.Length; i++)
            {
                if (i >= c.LinkCount || k <= 0f)
                {
                    c.Links[i].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                Color col = Style.GlowColor;
                col.a = Style.BlockLinkOpacity * Mathf.Sin(k * Mathf.PI);
                c.Links[i].color = col;
            }
        }

        /// <summary>The plus spread arriving: a tendril reaches over from the cell that seeded
        /// this one, then the new core blooms where it lands.</summary>
        private void TickSpread(Core c)
        {
            if (c.SpreadTimer <= 0f)
            {
                return;
            }
            float total = Style.SpreadTendrilSeconds + Style.SpreadBloomSeconds;
            float t = 1f - c.SpreadTimer / total;
            float reach = Mathf.Clamp01(t / (Style.SpreadTendrilSeconds / total));
            // Drawn on the LAST link slot so it needs no renderer of its own.
            SpriteRenderer tendril = c.Links[c.Links.Length - 1];
            Vector2 d = c.SpreadFrom - c.At;
            float len = d.magnitude * (1f - reach);
            Transform tr = tendril.transform;
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            tr.localScale = new Vector3(Mathf.Max(len, 0.0001f),
                Style.SpreadTendrilWidth * cellSize, 1f);
            Color col = Style.GlowColor;
            col.a = reach < 1f ? 0.7f : 0.7f * (1f - Mathf.Clamp01((t - reach) * 3f));
            tendril.color = col;
        }
    }
}
