// PURPOSE: What the circuit does to the blocks standing on it. The cable's own blue overload is a
// separate thing and is not touched here; this is the THERMAL failure of the cubes it runs through -
// heat coming off the cable, thermal cracking, rupture at the cable's own peak, and then burnt
// material falling as ash.
//
// THE BLOCK IS NEVER REPAINTED. Its tile, its colour and its shading are the player's, and they stay
// readable the whole way through: the heat is a separate overlay laid OVER the block, never a lerp
// of the block's own colour toward orange. A cube that turns into an orange square and then a white
// one is a state change with a palette, not a material failing - and the white square in particular
// is the single cheapest thing this effect could do, so there is none at any point.
//
// THE HEAT IS FOUR LAYERS, NOT A TINT. One overlay is paint; a material getting hot is not. So the
// warmth arrives as a hierarchy, each layer on its own curve so the stages are actually
// distinguishable: a whole-block warmth that comes up first and stays modest, uneven PATCHES that
// break it up, the CABLE BAND that is always the hottest thing, and last a CORE glow set inside the
// silhouette so the heat reads as coming from within the body while the bevel stays cooler.
//
// THREE OF THOSE FOUR ARE MASKED BY THE BLOCK'S OWN TILE. They use the same sprite the block is
// drawn with, so the warmth follows its exact silhouette, its rounding and its bevel, and cannot
// spill a square of colour over a shaped cube. That is also what keeps this from being a fill: the
// alphas are low, so what the player sees is the block's own colour running hot, not amber laid on
// top of it.
//
// THE HEAT COMES FROM THE CABLE. The overlay is a band, oriented along the direction the circuit
// actually runs through that cell and hottest on that axis, falling away across it. Cracks are born
// out of that band rather than scattered over the face. That is what ties the destruction to the
// thing causing it instead of leaving two effects that merely happen at the same time.
//
// IT HOLDS ITS OWN COPIES. Core destroyed these cubes during turn resolution and the board was
// redrawn without them before the view heard anything, so this draws its own copy of each from the
// destruction log, which carries the whole Cube. The board's drawing is never touched.
//
// THE TEMPO IS THE POINT: slow heat, tight cracking, a held breath, a very fast rupture, then ash
// that takes its time. Every stage after the rupture is slower than the one before it.

using System.Collections.Generic;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Cubes on a circuit cooking, cracking, rupturing and falling as ash.</summary>
    public sealed class CircuitHeatFx : MonoBehaviour
    {
        // =================================================================== TUNING
        public static class Style
        {
            // ------------------------------------------------------------------ heat
            /// <summary>THE CABLE BAND - the primary hot zone, and the hottest thing on the
            /// block. Laid OVER it, so this is how much warmth is added, never how much of the
            /// block is replaced.</summary>
            public static float HeatIntensity = 0.46f;

            public static float BandStart = 0.16f;

            /// <summary>WHOLE-BLOCK WARMTH. Low on purpose and masked by the block's own tile: this
            /// is the layer that has to say "all of this is getting hot" without ever becoming a
            /// coat of paint. It comes up first and rises almost linearly, so the block is already
            /// visibly running warm long before anything dramatic happens to it.</summary>
            public static float WholeBlockHeatStrength = 0.30f;

            public static float WholeBlockCurve = 1.15f;

            /// <summary>UNEVEN PATCHES over the face, so the warmth is not the same everywhere at
            /// once - which is the single thing that most makes a heat overlay look cheap.</summary>
            public static float HeatVariationStrength = 0.28f;

            public static float HeatVariationStart = 0.30f;

            public static float HeatVariationCurve = 1.8f;

            public static float HeatVariationScale = 0.92f;

            /// <summary>THE CORE. Sits inside the silhouette, comes up late and hard, and leaves
            /// the bevel and the outer edge cooler than the middle - which is what reads as heat
            /// arriving from inside the material rather than being printed on its face.</summary>
            public static float InternalGlowStrength = 0.30f;

            public static float InternalGlowStart = 0.50f;

            public static float InternalGlowCurve = 2.6f;

            public static float InternalGlowSize = 0.70f;

            /// <summary>The core is deeper and more saturated than the surface warmth: heat has a
            /// gradient through a solid, and this is the far end of it.</summary>
            public static Color InternalGlowTint = new Color(1f, 0.34f, 0.06f);

            /// <summary>How the heat builds. Above one it hangs back and then rushes, which keeps
            /// the block looking like itself for most of the wait.</summary>
            public static float HeatCurve = 2.1f;

            /// <summary>How far the heat spreads from the cable band, as a fraction of the cell.
            /// It never reaches the corners: those are the coolest part of the block.</summary>
            public static float HeatSpread = 0.86f;

            /// <summary>How much narrower the band is ACROSS the cable than along it. The cable is
            /// a line, so the heat it leaves is a stripe, not a disc.</summary>
            public static float HeatBandNarrow = 0.52f;

            /// <summary>The warm the overlay carries on a DARK block and on a LIGHT one. A single
            /// colour disappears on one or the other; picking by the block's own luminance is what
            /// keeps the heat readable whatever the player's palette is.</summary>
            public static Color HeatOnDark = new Color(1f, 0.52f, 0.16f);

            public static Color HeatOnLight = new Color(0.86f, 0.20f, 0.04f);

            /// <summary>Very slight local shimmer over the hottest part, late on.</summary>
            public static float ShimmerAmount = 0.020f;

            public static float ShimmerHz = 21f;

            public static float ShimmerStart = 0.52f;

            // ------------------------------------------------------------------ cracks
            /// <summary>When crazing starts, as a fraction of the run up to the rupture.</summary>
            public static float CrackStart = 0.42f;

            /// <summary>Crack families per block, each one a main branch plus a smaller one. Few:
            /// a block webbed with cracks is glass, not a cube under thermal stress.</summary>
            public static int CrackFamiliesMin = 2;

            public static int CrackFamiliesMax = 3;

            public static float CrackLengthMin = 0.20f;

            public static float CrackLengthMax = 0.44f;

            public static float CrackWidth = 0.020f;

            /// <summary>A crack starts as a dark split and only lights up as the heat behind it
            /// rises. Opening already glowing would make it a drawn line rather than a break.</summary>
            public static Color CrackDark = new Color(0.06f, 0.05f, 0.05f);

            public static Color CrackEmber = new Color(1f, 0.56f, 0.18f);

            public static float CrackGlowStart = 0.62f;

            /// <summary>The one moment the cable's own blue is allowed onto the block: a flicker of
            /// electrical white-blue in the cracks as the circuit bursts, and gone immediately.</summary>
            public static Color CrackArc = new Color(0.72f, 0.92f, 1f);

            public static float CrackArcWindow = 0.06f;

            // ------------------------------------------------------------------ tension
            public static float TensionStart = 0.86f;

            public static float PreRuptureSwell = 0.035f;

            public static float PreRuptureSqueeze = 0.016f;

            public static float PreRuptureVibration = 0.012f;

            /// <summary>The held breath just before it goes, as a fraction of the run up.</summary>
            public static float HoldStart = 0.945f;

            // ------------------------------------------------------------------ rupture
            /// <summary>Pieces the block breaks into. They carry the block's OWN colour at first
            /// and char from the edges in.</summary>
            public static int ShardCount = 4;

            public static float ShardImpulse = 1.7f;

            public static float ShardUpwardBias = 0.35f;

            public static float ShardDrag = 7.5f;

            public static float ShardSpin = 190f;

            public static float ShardLife = 0.42f;

            /// <summary>How far into its life a shard has finished charring.</summary>
            public static float ShardCharBy = 0.55f;

            public static Color ShardChar = new Color(0.115f, 0.105f, 0.098f);

            /// <summary>A brief hot rim on a shard's edge, because it was just torn out of
            /// something far too hot. Not molten, not dripping - just briefly incandescent.</summary>
            public static float ShardMoltenRim = 0.5f;

            /// <summary>The flash at the break. LOCAL - between the pieces and inside the cracks,
            /// never over the block's own silhouette.</summary>
            public static float RuptureFlash = 0.85f;

            public static float RuptureFlashSize = 0.42f;

            public static float RuptureFlashLife = 0.11f;

            // ------------------------------------------------------------------ ash
            public static int AshCount = 4;

            /// <summary>Ash goes UP first, loses it, and only then starts to fall. That reversal is
            /// the whole character - straight down from the first frame is gravel, not ash.</summary>
            public static float AshLift = 1.15f;

            public static float AshLiftDamp = 3.4f;

            public static float AshGravity = 0.30f;

            public static float AshTerminal = 0.42f;

            /// <summary>The sway on the way down, in cells and hertz, randomised per flake so four
            /// of them never move as one.</summary>
            public static float AshSwayAmplitude = 0.22f;

            public static float AshSwayHzMin = 0.5f;

            public static float AshSwayHzMax = 1.15f;

            public static float AshSpin = 70f;

            public static float AshLifeMin = 1.15f;

            public static float AshLifeMax = 1.85f;

            public static float AshSizeMin = 0.11f;

            public static float AshSizeMax = 0.21f;

            public static Color AshColor = new Color(0.215f, 0.200f, 0.188f);

            public static float AshOpacity = 0.85f;

            // ------------------------------------------------------------------ embers
            public static int EmberCount = 3;

            public static float EmberSize = 0.055f;

            public static float EmberLife = 0.45f;

            public static Color EmberColor = new Color(1f, 0.62f, 0.24f);

            // ------------------------------------------------------------------ leftovers
            /// <summary>A shadow of a burn on the slot, gone in well under a second. Kept weak so
            /// it never argues with the circuit's own scorch mark lying along the same cells.</summary>
            public static float ScorchOpacity = 0.30f;

            public static float ScorchLife = 0.65f;

            /// <summary>How far apart, at most, two blocks on the same circuit go off. Small: the
            /// rupture has to stay ONE event, this only stops it looking stamped.</summary>
            public static float MicroStagger = 0.035f;
        }

        /// <summary>EXACTLY where the real cube sat (ViewUtil.MakeCell uses 1). That matters:
        /// the cable is drawn at 4-8, ABOVE the blocks, and a ghost copy sitting higher would
        /// cover the very cable that is burning it - inverting what the player was looking at a
        /// moment earlier.</summary>
        private const int BodyOrder = 1;

        /// <summary>The heat layers, each with its OWN number. They used to be HeatOrder+1..+3,
        /// which collided with the crack and debris orders below - two renderers at the same
        /// sorting order have no defined winner, so the cracks could end up under the patch
        /// overlay depending on nothing in particular.</summary>
        private const int WarmOrder = 13;

        private const int PatchOrder = 14;

        private const int BandOrder = 15;

        private const int CoreOrder = 16;

        private const int CrackOrder = 17;

        private const int DebrisOrder = 18;

        private const int ScorchOrder = 12;

        private const int CrackSegments = 6;   // up to 3 families x main + branch

        /// <summary>A cube that was standing on the circuit, as it looked before it went.</summary>
        public struct Ghost
        {
            public Vector2 World;
            public Sprite Tile;
            public Color Colour;
            public float Size;

            /// <summary>Which way the cable runs through this cell. The heat is a band on it.</summary>
            public Vector2 CableDir;
        }

        // =================================================================== pooled pieces

        private sealed class Mote
        {
            public SpriteRenderer R;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Angle;
            public float Spin;
            public float Size;
            public float Life;
            public float Age;
            public float Seed;
            public float SwayHz;
            public Color Tint;
            public bool Live;
        }

        private sealed class Block
        {
            public SpriteRenderer Body;
            public SpriteRenderer Warm;      // whole-block, masked by the tile
            public SpriteRenderer Patches;   // uneven, masked by the tile
            public SpriteRenderer Heat;      // the cable band
            public SpriteRenderer Core;      // inside the silhouette, late
            public SpriteRenderer Flash;
            public SpriteRenderer Scorch;
            public SpriteRenderer[] Cracks = new SpriteRenderer[CrackSegments];
            public float[] CrackLen = new float[CrackSegments];
            public float[] CrackOpen = new float[CrackSegments];
            public Vector2[] CrackFrom = new Vector2[CrackSegments];
            public Vector2[] CrackDir = new Vector2[CrackSegments];
            public int CrackCount;
            public Mote[] Shards;
            public Mote[] Ash;
            public Mote[] Embers;

            public Vector2 Pos;
            public Color Base;
            public Color HeatTint;
            public float PatchAngle;
            public float Size;
            public float Seed;
            public float Stagger;
            public bool Ruptured;
        }

        private Block[] blocks = new Block[0];

        private int count;

        private float clock;

        private float rupture = 1f;

        private bool running;

        private bool holding;

        /// <summary>Whether this owns those cells at all - cooking them OR just holding them
        /// parked. The board asks, so it knows whether to keep its own cubes out of the way.</summary>
        public bool Active
        {
            get { return running || holding; }
        }

        private float cell = 1f;

        // =================================================================== sprites

        private static Sprite heatSprite;

        private static Sprite crackSprite;

        private static Sprite flashSprite;

        private static Sprite emberSprite;

        private static Sprite scorchSprite;

        private static Sprite[] shardSprites;

        private static Sprite[] ashSprites;

        /// <summary>The heat off the cable: hot along the middle, falling away across it, with two
        /// smaller patches so the face is not an even wash. Drawn wide and rotated onto the cable's
        /// own direction by the block that owns it.</summary>
        private static Sprite HeatSprite()
        {
            if (heatSprite != null)
            {
                return heatSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    // A stripe: barely falls off along the cable, hard across it.
                    float band = Mathf.Exp(-0.9f * u * u - 4.6f * v * v);
                    // Two off-centre patches, so the heat is not a clean ellipse.
                    float p1 = Mathf.Exp(-7f * ((u - 0.42f) * (u - 0.42f)
                        + (v - 0.26f) * (v - 0.26f)));
                    float p2 = Mathf.Exp(-9f * ((u + 0.34f) * (u + 0.34f)
                        + (v + 0.30f) * (v + 0.30f)));
                    float a = Mathf.Clamp01(band + p1 * 0.42f + p2 * 0.34f);
                    a = Mathf.Max(0f, a - 0.035f) * 1.036f;
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            heatSprite = Make(px, n, n);
            return heatSprite;
        }

        /// <summary>Scattered hot patches - several soft blobs at odd sizes, so the warmth over
        /// the face is never even. Rotated per block, which is enough to stop twenty of them
        /// looking stamped from the same die.</summary>
        private static Sprite patchSprite;

        private static Sprite PatchSprite()
        {
            if (patchSprite != null)
            {
                return patchSprite;
            }
            const int n = 64;
            var px = new Color32[n * n];
            float[] cx = { 0.28f, -0.34f, 0.06f, -0.12f, 0.44f };
            float[] cy = { -0.22f, 0.18f, 0.40f, -0.42f, 0.34f };
            float[] rr = { 5.5f, 7.5f, 11f, 9f, 15f };
            float[] amp = { 1f, 0.82f, 0.62f, 0.70f, 0.45f };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = 0f;
                    for (int p = 0; p < cx.Length; p++)
                    {
                        float du = u - cx[p];
                        float dv = v - cy[p];
                        a += Mathf.Exp(-rr[p] * (du * du + dv * dv)) * amp[p];
                    }
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            patchSprite = Make(px, n, n);
            return patchSprite;
        }

        private static Sprite CrackSprite()
        {
            if (crackSprite != null)
            {
                return crackSprite;
            }
            const int w = 48;
            const int h = 6;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                float across = Mathf.Exp(-8f * v * v);
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    // Wide where it started, closing to nothing at the tip: a split, not a stroke.
                    float along = Mathf.Pow(1f - u, 0.75f) * Mathf.Min(1f, u * 9f);
                    px[y * w + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(across * along) * 255f));
                }
            }
            crackSprite = Make(px, w, h);
            return crackSprite;
        }

        /// <summary>Irregular chips - deliberately NOT little cubes.</summary>
        private static Sprite[] ShardSprites()
        {
            if (shardSprites != null)
            {
                return shardSprites;
            }
            shardSprites = new Sprite[4];
            for (int s = 0; s < 4; s++)
            {
                const int n = 32;
                var px = new Color32[n * n];
                float p1 = s * 2.3f;
                float p2 = s * 1.7f + 0.9f;
                float sx = 1f + 0.28f * s;
                float sy = 1f - 0.19f * s;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = ((x + 0.5f) / n - 0.5f) / sx;
                        float v = ((y + 0.5f) / n - 0.5f) / sy;
                        float d = Mathf.Sqrt(u * u + v * v) * 2f;
                        float a = Mathf.Atan2(v, u);
                        // Straight-ish facets rather than lobes: a chip has edges.
                        float edge = 0.70f + 0.13f * Mathf.Sin(2f * a + p1)
                            + 0.10f * Mathf.Sin(3f * a + p2) + 0.05f * Mathf.Sin(5f * a);
                        float k = Mathf.Clamp01((edge - d) / 0.09f);
                        px[y * n + x] = new Color32(255, 255, 255,
                            (byte)Mathf.RoundToInt(k * 255f));
                    }
                }
                shardSprites[s] = Make(px, n, n);
            }
            return shardSprites;
        }

        /// <summary>Ash: thin, tapered, nothing like a square.</summary>
        private static Sprite[] AshSprites()
        {
            if (ashSprites != null)
            {
                return ashSprites;
            }
            ashSprites = new Sprite[4];
            for (int s = 0; s < 4; s++)
            {
                const int n = 24;
                var px = new Color32[n * n];
                float ph = s * 1.9f;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float u = ((x + 0.5f) / n - 0.5f) * 2f;
                        float v = ((y + 0.5f) / n - 0.5f) * 2f / (0.42f + 0.14f * s);
                        float d = Mathf.Sqrt(u * u + v * v);
                        float a = Mathf.Atan2(v, u);
                        float edge = 0.78f + 0.16f * Mathf.Sin(3f * a + ph)
                            + 0.09f * Mathf.Sin(5f * a + ph * 2f);
                        float k = Mathf.Clamp01((edge - d) / 0.16f);
                        px[y * n + x] = new Color32(255, 255, 255,
                            (byte)Mathf.RoundToInt(k * 255f));
                    }
                }
                ashSprites[s] = Make(px, n, n);
            }
            return ashSprites;
        }

        private static Sprite Dot(ref Sprite slot, int n, float falloff, float cut)
        {
            if (slot != null)
            {
                return slot;
            }
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = ((x + 0.5f) / n - 0.5f) * 2f;
                    float v = ((y + 0.5f) / n - 0.5f) * 2f;
                    float a = Mathf.Max(0f, Mathf.Exp(-falloff * (u * u + v * v)) - cut);
                    px[y * n + x] = new Color32(255, 255, 255,
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }
            slot = Make(px, n, n);
            return slot;
        }

        private static Sprite Make(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }

        // =================================================================== driving it

        /// <summary>
        /// Runs the whole failure: heat, cracking, rupture at <paramref name="ruptureAt"/>, and the
        /// burnt material afterwards. The rupture is timed to the circuit's own burst, which is why
        /// it is passed in rather than chosen here.
        /// </summary>
        public void Play(IReadOnlyList<Ghost> ghosts, float cellSize, float ruptureAt)
        {
            if (!Setup(ghosts, cellSize, ruptureAt))
            {
                return;
            }
            clock = 0f;
            running = true;
            holding = false;
        }

        /// <summary>
        /// Draws the blocks and nothing else - parked, stone cold, no clock. It is the state the
        /// break BEGINS from, so the animation lab can put the cable and the cubes on screen
        /// together and let the designer look at how they layer before setting anything off.
        /// </summary>
        public void Hold(IReadOnlyList<Ghost> ghosts, float cellSize)
        {
            if (!Setup(ghosts, cellSize, 1f))
            {
                return;
            }
            running = false;   // bodies stay drawn; Update does nothing
            holding = true;
        }

        private bool Setup(IReadOnlyList<Ghost> ghosts, float cellSize, float ruptureAt)
        {
            Stop();
            if (ghosts == null || ghosts.Count == 0 || ruptureAt <= 0f || cellSize <= 0f)
            {
                return false;
            }
            cell = cellSize;
            rupture = ruptureAt;
            Grow(ghosts.Count);
            count = ghosts.Count;

            for (int i = 0; i < count; i++)
            {
                Block b = blocks[i];
                Ghost g = ghosts[i];
                b.Pos = g.World;
                b.Base = g.Colour;
                b.Size = g.Size > 0f ? g.Size : cellSize;
                b.Seed = Random.value * 10f;
                b.Stagger = Random.Range(0f, Style.MicroStagger);
                b.Ruptured = false;

                // THE SAME CALL THE BOARD DRAWS A CUBE WITH. Setting the sprite by hand skips
                // the MATERIAL that goes with a tile, and a cube drawn without it is not the cube
                // the player has been looking at - which is the whole promise of a copy.
                ViewUtil.ApplyTile(b.Body, g.Tile, b.Size);
                b.Body.color = g.Colour;
                b.Body.transform.localPosition = new Vector3(g.World.x, g.World.y, 0f);
                b.Body.transform.localRotation = Quaternion.identity;
                b.Body.enabled = true;

                // Warm chosen against the block's own value, so the heat shows on a pale cube and
                // on a dark one alike without either being turned orange.
                float lum = g.Colour.r * 0.2126f + g.Colour.g * 0.7152f + g.Colour.b * 0.0722f;
                b.HeatTint = Color.Lerp(Style.HeatOnDark, Style.HeatOnLight,
                    Mathf.Clamp01((lum - 0.25f) / 0.5f));

                // Whole-block and core both wear the BLOCK'S OWN SPRITE, so the warmth is cut to
                // its silhouette and can never be a square of colour over a shaped tile.
                Sprite mask = g.Tile != null ? g.Tile : ViewUtil.WhiteSprite;
                b.Warm.sprite = mask;
                b.Warm.transform.localPosition = b.Body.transform.localPosition;
                b.Warm.transform.localScale = b.Body.transform.localScale;
                b.Warm.color = new Color(b.HeatTint.r, b.HeatTint.g, b.HeatTint.b, 0f);
                b.Warm.enabled = true;

                b.Core.sprite = mask;
                b.Core.transform.localPosition = b.Body.transform.localPosition;
                b.Core.transform.localScale = b.Body.transform.localScale
                    * Style.InternalGlowSize;
                b.Core.color = new Color(Style.InternalGlowTint.r, Style.InternalGlowTint.g,
                    Style.InternalGlowTint.b, 0f);
                b.Core.enabled = true;

                b.PatchAngle = Random.Range(0f, 360f);
                b.Patches.sprite = PatchSprite();
                b.Patches.transform.localPosition = b.Body.transform.localPosition;
                b.Patches.transform.localRotation = Quaternion.Euler(0f, 0f, b.PatchAngle);
                b.Patches.color = new Color(b.HeatTint.r, b.HeatTint.g, b.HeatTint.b, 0f);
                b.Patches.enabled = true;

                Vector2 dir = g.CableDir.sqrMagnitude > 0.0001f
                    ? g.CableDir.normalized : Vector2.right;
                b.Heat.sprite = HeatSprite();
                b.Heat.transform.localPosition = b.Body.transform.localPosition;
                b.Heat.transform.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                b.Heat.color = new Color(b.HeatTint.r, b.HeatTint.g, b.HeatTint.b, 0f);
                b.Heat.enabled = true;

                LayCracks(b, dir);

                b.Flash.sprite = Dot(ref flashSprite, 32, 3.0f, 0.04f);
                b.Flash.enabled = false;
                b.Scorch.sprite = Dot(ref scorchSprite, 32, 2.1f, 0.06f);
                b.Scorch.enabled = false;

                for (int s = 0; s < b.Shards.Length; s++) { b.Shards[s].Live = false; }
                for (int s = 0; s < b.Ash.Length; s++) { b.Ash[s].Live = false; }
                for (int s = 0; s < b.Embers.Length; s++) { b.Embers[s].Live = false; }
            }
            return true;
        }

        /// <summary>
        /// Crack families, born ON the cable band rather than scattered over the face. Each is a
        /// main branch running away from the cable plus a shorter one off it - which is what a
        /// thermal split does, and what a handful of random scratches does not.
        /// </summary>
        private void LayCracks(Block b, Vector2 dir)
        {
            int families = Random.Range(Style.CrackFamiliesMin, Style.CrackFamiliesMax + 1);
            b.CrackCount = 0;
            float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int f = 0; f < families && b.CrackCount < CrackSegments - 1; f++)
            {
                // Somewhere along the hot stripe, not in a corner.
                float along = Random.Range(-0.34f, 0.34f);
                Vector2 origin = b.Pos + dir * (along * b.Size)
                    + new Vector2(-dir.y, dir.x) * Random.Range(-0.07f, 0.07f) * b.Size;
                // Running away from the cable, give or take.
                float away = baseAng + 90f * (Random.value < 0.5f ? 1f : -1f)
                    + Random.Range(-38f, 38f);
                float len = Random.Range(Style.CrackLengthMin, Style.CrackLengthMax) * b.Size;
                AddCrack(b, origin, away, len);
                if (b.CrackCount < CrackSegments)
                {
                    // The branch leaves part way along the main one.
                    Vector2 mid = origin + Rot(away) * (len * Random.Range(0.35f, 0.6f));
                    AddCrack(b, mid, away + Random.Range(28f, 62f) * (Random.value < 0.5f ? 1f : -1f),
                        len * Random.Range(0.35f, 0.6f));
                }
            }
            for (int c = b.CrackCount; c < CrackSegments; c++)
            {
                b.Cracks[c].enabled = false;
            }
        }

        private static Vector2 Rot(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
        }

        private void AddCrack(Block b, Vector2 from, float angle, float length)
        {
            int c = b.CrackCount++;
            SpriteRenderer r = b.Cracks[c];
            r.sprite = CrackSprite();
            // Pivot is the middle of the sprite, so the crack grows from `from` outward by
            // moving as it lengthens - a crack that scaled about its centre would open at both
            // ends at once, which is not how a split travels.
            b.CrackLen[c] = length;
            b.CrackFrom[c] = from;
            b.CrackDir[c] = Rot(angle);
            b.CrackOpen[c] = Random.Range(0f, 0.35f);   // staggered, so they do not all go at once
            r.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            r.transform.localPosition = new Vector3(from.x, from.y, 0f);
            r.color = new Color(Style.CrackDark.r, Style.CrackDark.g, Style.CrackDark.b, 0f);
            r.transform.localScale = new Vector3(0f, Style.CrackWidth * cell * 8f, 1f);
            r.enabled = true;
        }

        public void Stop()
        {
            running = false;
            holding = false;
            for (int i = 0; i < blocks.Length; i++)
            {
                Block b = blocks[i];
                if (b == null)
                {
                    continue;
                }
                b.Body.enabled = false;
                b.Warm.enabled = false;
                b.Patches.enabled = false;
                b.Heat.enabled = false;
                b.Core.enabled = false;
                b.Flash.enabled = false;
                b.Scorch.enabled = false;
                for (int c = 0; c < CrackSegments; c++) { b.Cracks[c].enabled = false; }
                Hide(b.Shards);
                Hide(b.Ash);
                Hide(b.Embers);
            }
            count = 0;
        }

        private static void Hide(Mote[] motes)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                motes[i].Live = false;
                if (motes[i].R != null)
                {
                    motes[i].R.enabled = false;
                }
            }
        }

        private void Grow(int want)
        {
            if (blocks.Length >= want)
            {
                return;
            }
            var next = new Block[want];
            System.Array.Copy(blocks, next, blocks.Length);
            for (int i = blocks.Length; i < want; i++)
            {
                var b = new Block();
                b.Body = NewRenderer("Body" + i, BodyOrder);
                b.Warm = NewRenderer("Warm" + i, WarmOrder);
                b.Patches = NewRenderer("Patch" + i, PatchOrder);
                b.Heat = NewRenderer("Heat" + i, BandOrder);
                b.Core = NewRenderer("Core" + i, CoreOrder);
                b.Flash = NewRenderer("Flash" + i, DebrisOrder);
                b.Scorch = NewRenderer("Scorch" + i, ScorchOrder);
                for (int c = 0; c < CrackSegments; c++)
                {
                    b.Cracks[c] = NewRenderer("Crack" + i + "_" + c, CrackOrder);
                }
                b.Shards = NewMotes("Shard" + i, Style.ShardCount);
                b.Ash = NewMotes("Ash" + i, Style.AshCount);
                b.Embers = NewMotes("Ember" + i, Style.EmberCount);
                next[i] = b;
            }
            blocks = next;
        }

        private SpriteRenderer NewRenderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            r.enabled = false;
            return r;
        }

        private Mote[] NewMotes(string name, int n)
        {
            var motes = new Mote[n];
            for (int i = 0; i < n; i++)
            {
                motes[i] = new Mote { R = NewRenderer(name + "_" + i, DebrisOrder) };
            }
            return motes;
        }

        // =================================================================== running

        private void Update()
        {
            if (!running)
            {
                return;
            }
            float dt = Time.deltaTime;
            clock += dt;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                any |= StepBlock(blocks[i], dt);
            }
            running = any;
        }

        private bool StepBlock(Block b, float dt)
        {
            float t = clock - b.Stagger;
            float k = Mathf.Clamp01(t / rupture);

            if (!b.Ruptured && t >= rupture)
            {
                Rupture(b);
            }

            bool alive = false;
            if (!b.Ruptured)
            {
                alive = true;
                StepCooking(b, k);
            }
            else
            {
                b.Body.enabled = false;
                b.Warm.enabled = false;
                b.Patches.enabled = false;
                b.Heat.enabled = false;
                b.Core.enabled = false;
                for (int c = 0; c < b.CrackCount; c++) { b.Cracks[c].enabled = false; }
                alive |= StepAftermath(b, t - rupture, dt);
            }
            return alive;
        }

        private void StepCooking(Block b, float k)
        {
            // Four layers, four curves. The whole block warms first and gently, patches break it
            // up, the cable band is always the hottest, and the core arrives last from inside.
            float whole = Mathf.Pow(k, Style.WholeBlockCurve);
            float band = Ramp(k, Style.BandStart, Style.HeatCurve);
            float patch = Ramp(k, Style.HeatVariationStart, Style.HeatVariationCurve);
            float core = Ramp(k, Style.InternalGlowStart, Style.InternalGlowCurve);
            float heat = band;

            Color wc = b.HeatTint;
            wc.a = whole * Style.WholeBlockHeatStrength;
            b.Warm.color = wc;

            Color pc = b.HeatTint;
            pc.a = patch * Style.HeatVariationStrength;
            b.Patches.color = pc;
            float ps = b.Size * Style.HeatVariationScale;
            b.Patches.transform.localScale = new Vector3(ps, ps, 1f);

            Color cc2 = Style.InternalGlowTint;
            cc2.a = core * Style.InternalGlowStrength;
            b.Core.color = cc2;

            // The overlay, never the block itself.
            Color hc = b.HeatTint;
            hc.a = band * Style.HeatIntensity;
            b.Heat.color = hc;
            float spread = b.Size * Style.HeatSpread * (0.55f + 0.45f * band);
            b.Heat.transform.localScale = new Vector3(spread,
                spread * Style.HeatBandNarrow, 1f);

            // Tension, then a held breath: it swells, is squeezed back, and goes very still.
            float scale = 1f;
            Vector2 offset = Vector2.zero;
            if (k > Style.TensionStart)
            {
                float ten = (k - Style.TensionStart) / (1f - Style.TensionStart);
                scale = 1f + Style.PreRuptureSwell * Mathf.Sin(ten * Mathf.PI * 0.5f);
                if (k > Style.HoldStart)
                {
                    // Squeezed and almost motionless - the payoff is in the silence.
                    float hold = (k - Style.HoldStart) / (1f - Style.HoldStart);
                    scale -= Style.PreRuptureSqueeze * hold;
                }
                else
                {
                    float v = Style.PreRuptureVibration * ten * b.Size;
                    offset = new Vector2(
                        Mathf.Sin((clock * 34f + b.Seed) * Mathf.PI * 2f) * v,
                        Mathf.Sin((clock * 41f + b.Seed * 2f) * Mathf.PI * 2f) * v);
                }
            }
            else if (k > Style.ShimmerStart)
            {
                float sh = Style.ShimmerAmount * b.Size
                    * (k - Style.ShimmerStart) / (1f - Style.ShimmerStart);
                offset = new Vector2(
                    Mathf.Sin((clock * Style.ShimmerHz + b.Seed) * Mathf.PI * 2f) * sh, 0f);
            }
            float size = b.Size * scale;
            b.Body.transform.localScale = new Vector3(size, size, 1f);
            b.Body.transform.localPosition = new Vector3(b.Pos.x + offset.x,
                b.Pos.y + offset.y, 0f);
            // Every heat layer rides the block, so the tension shiver moves the warmth with it.
            Vector3 at = b.Body.transform.localPosition;
            b.Heat.transform.localPosition = at;
            b.Warm.transform.localPosition = at;
            b.Warm.transform.localScale = b.Body.transform.localScale;
            b.Patches.transform.localPosition = at;
            b.Core.transform.localPosition = at;
            b.Core.transform.localScale = b.Body.transform.localScale
                * Style.InternalGlowSize;

            // Cracks: dark splits first, embers behind them as the heat climbs.
            float crack = k > Style.CrackStart
                ? (k - Style.CrackStart) / (1f - Style.CrackStart) : 0f;
            for (int c = 0; c < b.CrackCount; c++)
            {
                SpriteRenderer r = b.Cracks[c];
                float own = Mathf.Clamp01((crack - b.CrackOpen[c]) / (1f - b.CrackOpen[c]));
                if (own <= 0f)
                {
                    r.enabled = false;
                    continue;
                }
                r.enabled = true;
                float len = b.CrackLen[c] * own;
                Vector3 s = r.transform.localScale;
                r.transform.localScale = new Vector3(len, s.y, 1f);
                // The sprite is pivoted in its middle, so the transform has to walk forward as
                // the crack lengthens - otherwise the split opens at BOTH ends at once, which is
                // not how a crack travels through anything.
                Vector2 mid = b.CrackFrom[c] + b.CrackDir[c] * (len * 0.5f);
                r.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
                float glow = k > Style.CrackGlowStart
                    ? (k - Style.CrackGlowStart) / (1f - Style.CrackGlowStart) : 0f;
                Color cc = Color.Lerp(Style.CrackDark, Style.CrackEmber, glow * glow);
                // The ONE moment the cable is allowed onto the block: as the circuit bursts, its
                // blue jumps into the splits. Two frames, then gone - any longer and the cracks
                // read as neon rather than as electricity finding a way in.
                if (k > 1f - Style.CrackArcWindow)
                {
                    float arc = (k - (1f - Style.CrackArcWindow)) / Style.CrackArcWindow;
                    cc = Color.Lerp(cc, Style.CrackArc, arc);
                }
                cc.a = Mathf.Min(1f, own * 1.4f);
                r.color = cc;
            }
        }

        /// <summary>A layer's own progress: nothing until it starts, then its own curve over
        /// what is left. Separate starts are what make the stages tell each other apart.</summary>
        private static float Ramp(float k, float start, float curve)
        {
            if (k <= start)
            {
                return 0f;
            }
            return Mathf.Pow((k - start) / (1f - start), curve);
        }

        private void Rupture(Block b)
        {
            b.Ruptured = true;

            // A LOCAL flash between the pieces - never the block's silhouette.
            b.Flash.transform.localPosition = new Vector3(b.Pos.x, b.Pos.y, 0f);
            float fs = b.Size * Style.RuptureFlashSize;
            b.Flash.transform.localScale = new Vector3(fs, fs, 1f);
            b.Flash.color = new Color(1f, 0.86f, 0.62f, Style.RuptureFlash);
            b.Flash.enabled = true;

            b.Scorch.transform.localPosition = new Vector3(b.Pos.x, b.Pos.y, 0f);
            float ss = b.Size * 0.9f;
            b.Scorch.transform.localScale = new Vector3(ss, ss, 1f);
            b.Scorch.color = new Color(0.04f, 0.035f, 0.03f, Style.ScorchOpacity);
            b.Scorch.enabled = true;

            Sprite[] shardArt = ShardSprites();
            for (int s = 0; s < b.Shards.Length; s++)
            {
                Mote m = b.Shards[s];
                float ang = (s + Random.Range(0.1f, 0.9f)) / b.Shards.Length * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) + Style.ShardUpwardBias);
                m.R.sprite = shardArt[Random.Range(0, shardArt.Length)];
                m.Pos = b.Pos + dir.normalized * (b.Size * 0.12f);
                m.Vel = dir.normalized * Style.ShardImpulse * cell * Random.Range(0.7f, 1.2f);
                m.Angle = Random.Range(0f, 360f);
                m.Spin = Random.Range(-Style.ShardSpin, Style.ShardSpin);
                m.Size = b.Size * Random.Range(0.30f, 0.46f);
                m.Life = Style.ShardLife * Random.Range(0.85f, 1.15f);
                m.Age = 0f;
                m.Tint = b.Base;
                m.Live = true;
                m.R.enabled = true;
            }

            Sprite[] ashArt = AshSprites();
            for (int s = 0; s < b.Ash.Length; s++)
            {
                Mote m = b.Ash[s];
                m.R.sprite = ashArt[Random.Range(0, ashArt.Length)];
                m.Pos = b.Pos + Random.insideUnitCircle * (b.Size * 0.3f);
                float ang = Random.Range(0f, Mathf.PI * 2f);
                m.Vel = new Vector2(Mathf.Cos(ang) * 0.35f, Style.AshLift
                    * Random.Range(0.7f, 1.2f)) * cell;
                m.Angle = Random.Range(0f, 360f);
                m.Spin = Random.Range(-Style.AshSpin, Style.AshSpin);
                m.Size = Random.Range(Style.AshSizeMin, Style.AshSizeMax) * cell;
                m.Life = Random.Range(Style.AshLifeMin, Style.AshLifeMax);
                m.Age = 0f;
                m.Seed = Random.value * 10f;
                m.SwayHz = Random.Range(Style.AshSwayHzMin, Style.AshSwayHzMax);
                m.Tint = Style.AshColor;
                m.Live = true;
                m.R.enabled = true;
            }

            for (int s = 0; s < b.Embers.Length; s++)
            {
                Mote m = b.Embers[s];
                m.R.sprite = Dot(ref emberSprite, 16, 4.5f, 0.03f);
                m.Pos = b.Pos + Random.insideUnitCircle * (b.Size * 0.34f);
                m.Vel = Random.insideUnitCircle * 0.35f * cell;
                m.Size = Style.EmberSize * cell * Random.Range(0.7f, 1.3f);
                m.Life = Style.EmberLife * Random.Range(0.7f, 1.3f);
                m.Age = 0f;
                m.Seed = Random.value * 10f;
                m.Tint = Style.EmberColor;
                m.Live = true;
                m.R.enabled = true;
            }
        }

        private bool StepAftermath(Block b, float t, float dt)
        {
            bool alive = false;

            if (b.Flash.enabled)
            {
                float k = t / Style.RuptureFlashLife;
                if (k >= 1f)
                {
                    b.Flash.enabled = false;
                }
                else
                {
                    alive = true;
                    Color c = b.Flash.color;
                    c.a = Style.RuptureFlash * (1f - k) * (1f - k);
                    b.Flash.color = c;
                }
            }
            if (b.Scorch.enabled)
            {
                float k = t / Style.ScorchLife;
                if (k >= 1f)
                {
                    b.Scorch.enabled = false;
                }
                else
                {
                    alive = true;
                    Color c = b.Scorch.color;
                    // Holds, then goes - a mark cooling rather than being switched off.
                    c.a = Style.ScorchOpacity * (k < 0.35f ? 1f : 1f - (k - 0.35f) / 0.65f);
                    b.Scorch.color = c;
                }
            }

            // Shards: thrown hard, stopped hard, charring from the moment they are free.
            for (int s = 0; s < b.Shards.Length; s++)
            {
                Mote m = b.Shards[s];
                if (!m.Live)
                {
                    continue;
                }
                m.Age += dt;
                float k = m.Age / m.Life;
                if (k >= 1f)
                {
                    m.Live = false;
                    m.R.enabled = false;
                    continue;
                }
                alive = true;
                m.Vel *= Mathf.Max(0f, 1f - Style.ShardDrag * dt);
                m.Pos += m.Vel * dt;
                m.Angle += m.Spin * dt;
                // The block's own colour first, charcoal by the time it stops - and a hot rim for
                // the first instant, because it has just come out of something far too hot.
                Color c = Color.Lerp(m.Tint, Style.ShardChar,
                    Mathf.Clamp01(k / Style.ShardCharBy));
                c = Color.Lerp(c, Style.CrackEmber,
                    Mathf.Max(0f, 1f - k / 0.22f) * Style.ShardMoltenRim);
                c.a = k > 0.75f ? 1f - (k - 0.75f) / 0.25f : 1f;
                Place(m, c);
            }

            // Ash: up, then a long feathered fall.
            for (int s = 0; s < b.Ash.Length; s++)
            {
                Mote m = b.Ash[s];
                if (!m.Live)
                {
                    continue;
                }
                m.Age += dt;
                float k = m.Age / m.Life;
                if (k >= 1f)
                {
                    m.Live = false;
                    m.R.enabled = false;
                    continue;
                }
                alive = true;
                // The lift dies away and gravity takes over, capped so it never picks up speed
                // like a stone. That reversal is the whole character of the fall.
                m.Vel.y -= Style.AshGravity * cell * dt;
                m.Vel.y = Mathf.Max(m.Vel.y - 0f, -Style.AshTerminal * cell);
                m.Vel *= Mathf.Max(0f, 1f - Style.AshLiftDamp * dt * (k < 0.25f ? 1f : 0.25f));
                m.Pos += m.Vel * dt;
                // Sideways sway, its own rate per flake.
                float sway = Mathf.Sin((m.Age * m.SwayHz + m.Seed) * Mathf.PI * 2f)
                    * Style.AshSwayAmplitude * cell * dt;
                m.Pos.x += sway;
                m.Angle += m.Spin * dt;
                Color c = m.Tint;
                c.a = Style.AshOpacity * (k < 0.12f ? k / 0.12f : Mathf.Pow(1f - k, 0.8f));
                Place(m, c);
            }

            for (int s = 0; s < b.Embers.Length; s++)
            {
                Mote m = b.Embers[s];
                if (!m.Live)
                {
                    continue;
                }
                m.Age += dt;
                float k = m.Age / m.Life;
                if (k >= 1f)
                {
                    m.Live = false;
                    m.R.enabled = false;
                    continue;
                }
                alive = true;
                m.Vel *= Mathf.Max(0f, 1f - 4f * dt);
                m.Pos += m.Vel * dt;
                float flicker = 0.7f + 0.3f * Mathf.Sin((m.Age * 19f + m.Seed) * Mathf.PI * 2f);
                Color c = Color.Lerp(m.Tint, Style.ShardChar, k * k);
                c.a = (1f - k) * (1f - k) * flicker;
                Place(m, c);
            }
            return alive;
        }

        private static void Place(Mote m, Color c)
        {
            Transform t = m.R.transform;
            t.localPosition = new Vector3(m.Pos.x, m.Pos.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, m.Angle);
            t.localScale = new Vector3(m.Size, m.Size, 1f);
            m.R.color = c;
        }
    }
}
