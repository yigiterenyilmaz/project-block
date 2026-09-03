// PURPOSE: Tiny helpers for the placeholder UI (runtime-generated sprites, card colors) and
// the PAINTED BLOCK TILES a cube is drawn on.
// NOTE FOR AGENTS: everything under Assets/Scripts/View is intentionally disposable
// debug presentation. Game rules NEVER live here - they belong to ProjectBlock.Core.
//
// THE TILE TABLE. A cube is no longer a flat white square: it is a painted tile out of
// Assets/Resources/Art/Blocks, looked up by CubeKind on the board and by BlockElement in the
// hand (the two disagree - "Çark" is an element with no cube kind of its own). Everything the
// art has no tile for falls back to the DEFAULT tile, which is near-white and therefore takes
// the colour that kind always had - so an unpainted kind still reads as a tile, and painting
// one later is a single line here. If a tile file is missing entirely the fallback is the old
// flat WhiteSprite, so a stripped build degrades instead of rendering nothing.
//
// A tile carries its own paint, so a cube that HAS one is drawn WHITE (HasOwnTile) and the
// colour channel is left free for washes and animation. A cube on the default tile is drawn
// in its own colour as before.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Shared sprite + color helpers for the debug UI.</summary>
    public static class ViewUtil
    {
        private static Sprite whiteSprite;

        /// <summary>1x1 white sprite (1 world unit) generated at runtime - no asset needed.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    tex.filterMode = FilterMode.Point;
                    whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return whiteSprite;
            }
        }

        private static Sprite roundedSprite;

        /// <summary>Pixels a side of the generated rounded sprite, and the corner radius in
        /// those pixels. Both only ever divide by RoundedPpu, so the shape is what matters and
        /// the numbers are just resolution.</summary>
        private const int RoundedPixels = 64;

        private const int RoundedRadius = 16;

        /// <summary>At 128 pixels per unit the 16-pixel corner is 0.125 world units - about a
        /// twelfth of a card's width, which is where a playing card's corner sits.</summary>
        private const float RoundedPpu = 128f;

        /// <summary>
        /// A white ROUNDED rectangle, generated once and 9-SLICED, for anything that wants a
        /// card's silhouette instead of a hard rectangle. Hand it to MakePlate and the corner
        /// keeps its radius at any size while only the middle stretches - which is the whole
        /// reason it is sliced rather than scaled.
        ///
        /// The corner is antialiased across one pixel. That is not a soft texture in the sense
        /// the board forbids: at 128 ppu the ramp is 0.008 of a world unit, far under a screen
        /// pixel at any sane zoom, and without it a "rounded" corner is just a staircase.
        /// </summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    var tex = new Texture2D(RoundedPixels, RoundedPixels, TextureFormat.RGBA32,
                        false);
                    for (int y = 0; y < RoundedPixels; y++)
                    {
                        for (int x = 0; x < RoundedPixels; x++)
                        {
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, RoundedAlpha(x, y)));
                        }
                    }
                    tex.Apply();
                    tex.filterMode = FilterMode.Bilinear;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    roundedSprite = Sprite.Create(tex,
                        new Rect(0, 0, RoundedPixels, RoundedPixels), new Vector2(0.5f, 0.5f),
                        RoundedPpu, 0, SpriteMeshType.FullRect,
                        new Vector4(RoundedRadius, RoundedRadius, RoundedRadius, RoundedRadius));
                }
                return roundedSprite;
            }
        }

        /// <summary>Coverage of one pixel by the rounded rectangle: 1 inside, 0 outside, and a
        /// single pixel of ramp across the curve.</summary>
        private static float RoundedAlpha(int x, int y)
        {
            // Distance from the pixel's centre to the nearest corner circle's centre, measured
            // only once the pixel is actually in a corner - along an edge the shape is straight.
            float px = x + 0.5f;
            float py = y + 0.5f;
            float cx = Mathf.Clamp(px, RoundedRadius, RoundedPixels - RoundedRadius);
            float cy = Mathf.Clamp(py, RoundedRadius, RoundedPixels - RoundedRadius);
            float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01(RoundedRadius + 0.5f - distance);
        }

        /// <summary>MakeRect's rectangle with a card's ROUNDED corners. Same arguments, same
        /// meaning - it is MakePlate over the generated rounded sprite, so the corner keeps its
        /// radius however the rest is stretched.</summary>
        public static SpriteRenderer MakeRounded(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int sortingOrder)
        {
            return MakePlate(parent, name, position, size, color, sortingOrder, RoundedSprite);
        }

        // ---- painted UI plates ---------------------------------------------------------

        private const string UiFolder = "Art/Ui/";
        private const string DeckFolder = "Art/Decks/";
        private static readonly Dictionary<string, Sprite> uiCache =
            new Dictionary<string, Sprite>();

        /// <summary>Loads a painted UI plate out of Resources once. Null (not an exception)
        /// when the art is not there, which every caller treats as "draw a flat rectangle" -
        /// the same bargain the block tiles make.</summary>
        public static Sprite UiSprite(string fileName)
        {
            return CachedSprite(UiFolder + fileName);
        }

        /// <summary>The emblem for a starting deck, on the same terms: null when the art is
        /// missing, and the deck picker then draws sample shapes as it always did.</summary>
        public static Sprite DeckIcon(string fileName)
        {
            return CachedSprite(DeckFolder + fileName);
        }

        /// <summary>Keyed on the FULL path, so two folders can never collide on a file name.
        /// A miss is cached too - a missing asset must not be re-looked-up every frame.</summary>
        private static Sprite CachedSprite(string path)
        {
            Sprite sprite;
            if (!uiCache.TryGetValue(path, out sprite))
            {
                sprite = Resources.Load<Sprite>(path);
                uiCache[path] = sprite;
            }
            return sprite;
        }

        // ---- fonts ---------------------------------------------------------------------

        // THE GAME IS SET IN FREDOKA, and the copies under Assets/Resources/Fonts are PATCHED.
        // Upstream Fredoka has no Turkish letters - it ships Latin-1 plus ten stray Extended-A
        // glyphs, so it can draw "kayitli" but not "kayItli": I, g, G, s, S with their Turkish
        // marks are all missing, and half the joker names came out with holes in them. The
        // marks themselves (uni0306 breve, uni0327 cedilla, uni0307 dot) were already drawn by
        // the type designer, so Tools/FontPatch/add_turkish_glyphs.py only assembles the five
        // composites and adds the cmap entries. Nothing in them is hand-drawn. Re-run that
        // script if the font is ever updated - a fresh download will be broken again.

        private const string FontFolder = "Fonts/";
        private static Font uiFont;
        private static Font uiFontBold;
        private static bool fontsLoaded;

        /// <summary>The face for everything that is not a button or a heading (Fredoka
        /// SemiBold). Never null: with the fonts stripped this falls back to Unity's built-in
        /// face, so a build without the art still reads.</summary>
        public static Font UiFont
        {
            get
            {
                LoadFonts();
                return uiFont;
            }
        }

        /// <summary>Fredoka Bold - buttons, headings, and the title line of a tooltip.</summary>
        public static Font UiFontBold
        {
            get
            {
                LoadFonts();
                return uiFontBold;
            }
        }

        /// <summary>The face for a legacy FontStyle. Callers that ask for Bold get the REAL
        /// bold cut and should then leave `fontStyle` alone: a dynamic font told to be bold
        /// on top of it gets Unity's synthetic smear, which is not the same shape.</summary>
        public static Font UiFontFor(FontStyle style)
        {
            return style == FontStyle.Bold ? UiFontBold : UiFont;
        }

        private static void LoadFonts()
        {
            if (fontsLoaded)
            {
                return;
            }
            fontsLoaded = true;
            uiFont = Resources.Load<Font>(FontFolder + "Fredoka-SemiBold");
            uiFontBold = Resources.Load<Font>(FontFolder + "Fredoka-Bold");
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            if (uiFontBold == null)
            {
                uiFontBold = uiFont;
            }
        }

        // ---- painted block tiles ------------------------------------------------------

        private const string TileFolder = "Art/Blocks/";
        private static readonly Dictionary<string, Sprite> tileCache =
            new Dictionary<string, Sprite>();

        /// <summary>Loads a tile out of Resources once. Null (not an exception) when the art
        /// is not there, which is what every caller treats as "fall back to a flat square".</summary>
        private static Sprite Tile(string fileName)
        {
            Sprite tile;
            if (!tileCache.TryGetValue(fileName, out tile))
            {
                tile = Resources.Load<Sprite>(TileFolder + fileName);
                tileCache[fileName] = tile;
            }
            return tile;
        }

        /// <summary>Whether a tile file loaded, by file name. For the diagnostics in the F4
        /// gallery: some tiles (the targeted block's BODY) belong to no cube kind and no
        /// element, so nothing else on that screen would reveal a failed import.</summary>
        public static bool HasTile(string fileName)
        {
            return Tile(fileName) != null;
        }

        /// <summary>The tile every cube without one of its own is drawn on.</summary>
        public static Sprite DefaultTile
        {
            get { return Tile("block_default"); }
        }

        /// <summary>False when the tile art is missing altogether and the whole game is back on
        /// flat squares. Callers that size a cube differently on a tile ask this.</summary>
        public static bool ArtLoaded
        {
            get { return DefaultTile != null; }
        }

        /// <summary>The tile file for a cube kind, or null when that kind has no art of its
        /// own. Kept separate from CubeTile so callers can ask whether the paint is the tile's
        /// (draw white) or the cube's (draw its colour).</summary>
        private static Sprite OwnTile(CubeKind kind)
        {
            switch (kind)
            {
                case CubeKind.Fire: return Tile("block_fire");
                case CubeKind.Water: return Tile("block_water");
                case CubeKind.Obsidian: return Tile("block_obsidian");
                case CubeKind.Gold: return Tile("block_gold");
                case CubeKind.Transparent: return Tile("block_glass");
                case CubeKind.Dynamite: return Tile("block_dynamite");
                case CubeKind.Target: return Tile("block_target");
                case CubeKind.Void: return Tile("block_void");
                default: return null;
            }
        }

        /// <summary>Same table for the HAND, where a card knows its element and not a cube
        /// kind. "Çark" lives only here: it rotates in the hand and lands as plain cubes.</summary>
        private static Sprite OwnTile(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire: return Tile("block_fire");
                case BlockElement.Water: return Tile("block_water");
                case BlockElement.Obsidian: return Tile("block_obsidian");
                case BlockElement.Gold: return Tile("block_gold");
                case BlockElement.Transparent: return Tile("block_glass");
                case BlockElement.Dynamite: return Tile("block_dynamite");
                case BlockElement.Mechanical: return Tile("block_mechanical");
                case BlockElement.Fox: return Tile("block_fox");
                case BlockElement.Negative: return Tile("block_negative");
                case BlockElement.Targeted: return Tile("block_target");
                case BlockElement.Ghost: return Tile("block_ghost");
                default: return null;
            }
        }

        /// <summary>True when this kind is drawn on its own painted tile - so it must be
        /// tinted WHITE and its colour left to washes and idle animation.</summary>
        public static bool HasOwnTile(CubeKind kind)
        {
            return OwnTile(kind) != null;
        }

        /// <summary>The sprite a board cube of this kind is drawn on: its own tile, the
        /// default tile, or the flat white square if no art loaded at all.</summary>
        public static Sprite CubeTile(CubeKind kind)
        {
            Sprite own = OwnTile(kind);
            if (own != null)
            {
                return own;
            }
            return DefaultTile != null ? DefaultTile : WhiteSprite;
        }

        /// <summary>
        /// The tile for a cube that knows which CARD put it there.
        ///
        /// Needed because a cube kind is not the whole story: "Çark" and "Tilki" have no kind
        /// of their own and land as plain cubes, and a "Hedefli" block's ordinary cubes are
        /// plain too - only its one marked cube is CubeKind.Target. Left on kind alone all of
        /// those would dissolve into anonymous default tiles the moment they were placed, which
        /// reads as the art having failed to apply. The card is looked up from the cube's
        /// SourceCardId by the view; Core knows nothing about it.
        ///
        /// Kind still wins: a gear block's cube that water turned to obsidian is obsidian.
        /// </summary>
        public static Sprite CubeTile(CubeKind kind, BlockCard sourceCard)
        {
            Sprite own = OwnTile(kind);
            if (own != null)
            {
                return own;
            }
            if (sourceCard != null)
            {
                // A targeted block's plain cubes get its body tile - the marked one came back
                // as CubeKind.Target above and is already wearing the bullseye.
                if (sourceCard.Has(BlockElement.Targeted))
                {
                    Sprite body = Tile("block_target_body");
                    if (body != null)
                    {
                        return body;
                    }
                }
                for (int i = 0; i < sourceCard.Elements.Count; i++)
                {
                    Sprite fromCard = OwnTile(sourceCard.Elements[i]);
                    if (fromCard != null)
                    {
                        return fromCard;
                    }
                }
            }
            return DefaultTile != null ? DefaultTile : WhiteSprite;
        }

        // ---- the tiles that MOVE ------------------------------------------------------
        //
        // Three blocks animate by distorting themselves rather than by changing colour, and all
        // three are the same shader (Resources/Shaders/BlockWarp) under different knobs. The
        // material is chosen by the TILE rather than by cube kind or element, which is what
        // lets the board and the hand agree without either of them knowing the rule: whatever
        // face a cube ended up wearing, it moves the way that face moves.

        private static Shader warpShader;
        private static Material waterMaterial;
        private static Material fireMaterial;
        private static Material ghostMaterial;
        private static Material voidMaterial;
        private static bool warpShaderMissing;

        private static Shader WarpShader
        {
            get
            {
                if (warpShader == null && !warpShaderMissing)
                {
                    // Shader.Find works in the editor; the Resources copy is what survives into
                    // a build without an Always Included Shaders entry.
                    warpShader = Shader.Find("ProjectBlock/BlockWarp");
                    if (warpShader == null)
                    {
                        warpShader = Resources.Load<Shader>("Shaders/BlockWarp");
                    }
                    warpShaderMissing = warpShader == null;
                }
                return warpShader;
            }
        }

        private static Material MakeWarpMaterial(float amplitude, float frequency, float speed,
            float edgeHold, float drift, float swirl)
        {
            if (WarpShader == null)
            {
                return null; // no shader, no distortion: the tile still draws, just still
            }
            var material = new Material(WarpShader);
            material.SetFloat("_WarpAmp", amplitude);
            material.SetFloat("_WarpFreq", frequency);
            material.SetFloat("_WarpSpeed", speed);
            material.SetFloat("_EdgeHold", edgeHold);
            material.SetFloat("_WarpDrift", drift);
            material.SetFloat("_Swirl", swirl);
            return material;
        }

        /// <summary>The material a tile animates itself with, or null for the ordinary sprite
        /// material. Null is a complete answer - most tiles do not move.</summary>
        public static Material TileMaterial(Sprite tile)
        {
            if (tile == null)
            {
                return null;
            }
            if (tile == Tile("block_water"))
            {
                // Only the inside swirls; the frame and the rounded corners are furniture.
                // Big and slow rather than fine and fast: a low frequency at a high amplitude
                // is what rolls in blobs, where the reverse only ripples.
                if (waterMaterial == null)
                {
                    waterMaterial = MakeWarpMaterial(0.036f, 3.8f, 0.55f, 1f, 0.35f, 0f);
                }
                return waterMaterial;
            }
            if (tile == Tile("block_fire"))
            {
                // The same rolling flow as the water, at roughly half the speed - lava is
                // heavy. Border held: the block's frame is not on fire, its middle is.
                if (fireMaterial == null)
                {
                    fireMaterial = MakeWarpMaterial(0.030f, 3.4f, 0.26f, 1f, 0f, 0f);
                }
                return fireMaterial;
            }
            if (tile == Tile("block_ghost"))
            {
                // No border to respect: a ghost that held a crisp edge would not be one. Slower
                // and wider than the water, so it billows rather than ripples.
                if (ghostMaterial == null)
                {
                    ghostMaterial = MakeWarpMaterial(0.045f, 3.2f, 0.42f, 0f, 0f, 0f);
                }
                return ghostMaterial;
            }
            if (tile == Tile("block_void"))
            {
                // A hole turns instead of flowing: a slow stir about the centre, dying off well
                // before the rim, with only a breath of warp on top of it.
                if (voidMaterial == null)
                {
                    voidMaterial = MakeWarpMaterial(0.008f, 4f, 0.3f, 1f, 0f, 0.55f);
                }
                return voidMaterial;
            }
            return null;
        }

        /// <summary>
        /// The material a still tile uses: the built-in sprite material every SpriteRenderer
        /// starts life with, CAPTURED from one rather than built.
        ///
        /// It must be that exact shared asset. A `new Material(Shader.Find("Sprites/Default"))`
        /// looks identical and is not: one instance handed to every cell means one _MainTex for
        /// every cell, so the whole board ends up wearing whichever tile was assigned last.
        /// Unity's own sprite material is the one the renderer feeds each sprite's texture into
        /// per instance. Kept here so a cube that STOPS moving (water turned to obsidian) can be
        /// put back on it.
        /// </summary>
        public static Material PlainSpriteMaterial
        {
            get { return plainSpriteMaterial; }
        }

        private static Material plainSpriteMaterial;

        /// <summary>True when a tile carries its OWN paint and must be drawn white. False for
        /// the near-white default tile and the flat square, which are there to be tinted the
        /// colour the cube always had. One rule, asked of the tile that was actually chosen -
        /// so no caller has to re-derive how the choice was made.</summary>
        public static bool CarriesOwnPaint(Sprite tile)
        {
            return tile != null && tile != WhiteSprite && tile != DefaultTile;
        }

        /// <summary>The sprite a hand-card cube of this element is drawn on.</summary>
        public static Sprite CubeTile(BlockElement element)
        {
            Sprite own = OwnTile(element);
            if (own != null)
            {
                return own;
            }
            return DefaultTile != null ? DefaultTile : WhiteSprite;
        }

        /// <summary>What COLOUR to draw a cube in, given the tile it ended up on: white when
        /// the tile carries its own paint (the art speaks for itself), its usual colour when
        /// the tile is there to be tinted. The Parazit host tint survives either way - which
        /// cube carries the passenger has to stay visible on a painted board too.</summary>
        public static Color CubeTileColor(Cube cube, Sprite tile)
        {
            if (!CarriesOwnPaint(tile))
            {
                return CubeDisplayColor(cube);
            }
            return cube.Protected
                ? Color.Lerp(Color.white, new Color(0.85f, 0.2f, 0.85f), 0.4f)
                : Color.white;
        }

        /// <summary>Hand-card equivalent: white on a tile that paints itself, the given flat
        /// colour on one that wants tinting.</summary>
        public static Color CubeTileColor(Sprite tile, Color flatColor)
        {
            return CarriesOwnPaint(tile) ? Color.white : flatColor;
        }

        /// <summary>
        /// Puts a tile on a renderer and sizes it so the BLOCK BODY covers worldSize.
        ///
        /// The body, not the image. Every tile's .meta sets pixelsPerUnit to the body's pixel
        /// size and the pivot to the body's centre, so one world unit is always one block. For
        /// most tiles the body IS the whole canvas and the two are the same number - but the
        /// fox is drawn with tufts of fur breaking the square, and art like that must hang OVER
        /// its cell rather than be shrunk to fit inside it. That is why the scale here is
        /// simply worldSize and the sprite's own import settings carry the difference: put the
        /// overhang in the .meta and every call site gets it right without knowing.
        /// </summary>
        public static void ApplyTile(SpriteRenderer renderer, Sprite tile, float worldSize)
        {
            if (renderer == null)
            {
                return;
            }
            renderer.sprite = tile != null ? tile : WhiteSprite;
            renderer.transform.localScale = new Vector3(worldSize, worldSize, 1f);
            // The material goes with the tile, both ways round: a cell that was water and is
            // now obsidian has to be put BACK on the plain material or it keeps swirling.
            Material material = TileMaterial(tile);
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            else if (PlainSpriteMaterial != null)
            {
                renderer.sharedMaterial = PlainSpriteMaterial;
            }
        }

        /// <summary>Stable, distinct-ish color per card id (golden-ratio hue walk).</summary>
        public static Color ColorForCard(int cardId)
        {
            float hue = (cardId * 0.618034f) % 1f;
            if (hue < 0f) hue += 1f;
            return Color.HSVToRGB(hue, 0.55f, 0.95f);
        }

        /// <summary>Signature color of a block element (cards, cubes, labels).</summary>
        public static Color ElementColor(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire: return new Color(1f, 0.45f, 0.15f);
                case BlockElement.Water: return new Color(0.35f, 0.6f, 1f);
                case BlockElement.Obsidian: return new Color(0.25f, 0.22f, 0.3f);
                case BlockElement.Gold: return new Color(1f, 0.8f, 0.25f);
                case BlockElement.Transparent: return new Color(0.75f, 0.85f, 0.9f);
                case BlockElement.Ghost: return new Color(0.78f, 0.78f, 0.95f);
                case BlockElement.Negative: return new Color(0.16f, 0.16f, 0.2f);
                case BlockElement.Dynamite: return new Color(0.88f, 0.2f, 0.15f);
                case BlockElement.Mechanical: return new Color(0.6f, 0.65f, 0.7f);
                case BlockElement.Fox: return new Color(0.85f, 0.5f, 0.2f);
                // "Hedefli": a bright lime that belongs to nothing else on the board, so the one
                // cube that matters is findable at a glance among cubes of every other colour.
                case BlockElement.Targeted: return new Color(0.55f, 0.95f, 0.20f);
                default: return Color.gray;
            }
        }

        /// <summary>Board color of a cube: element kinds get their signature color,
        /// plain cubes keep their card's color. A Parazit host cube is tinted toward magenta
        /// so the player can see which cube carries the passenger.</summary>
        public static Color CubeDisplayColor(Cube cube)
        {
            if (cube.Protected)
            {
                return Color.Lerp(CubeBaseColor(cube), new Color(0.85f, 0.2f, 0.85f), 0.55f);
            }
            return CubeBaseColor(cube);
        }

        private static Color CubeBaseColor(Cube cube)
        {
            switch (cube.Kind)
            {
                case CubeKind.Fire: return ElementColor(BlockElement.Fire);
                case CubeKind.Water: return ElementColor(BlockElement.Water);
                case CubeKind.Obsidian: return ElementColor(BlockElement.Obsidian);
                case CubeKind.Gold: return ElementColor(BlockElement.Gold);
                case CubeKind.Transparent: return ElementColor(BlockElement.Transparent);
                case CubeKind.Dynamite: return ElementColor(BlockElement.Dynamite);
                case CubeKind.Ice: return new Color(0.62f, 0.86f, 0.95f);
                case CubeKind.Void: return new Color(0.10f, 0.07f, 0.16f);
                case CubeKind.Mine: return new Color(0.42f, 0.12f, 0.12f);
                // "Kangren" rot: a sickly grey-green, so a rotten cube is never mistaken for a
                // healthy one however full the board is.
                case CubeKind.Gangrene: return new Color(0.38f, 0.44f, 0.28f);
                // "Hidrolik pres": a hard industrial slate, so four cubes squeezed into one never
                // reads as an ordinary block.
                case CubeKind.Compressed: return new Color(0.30f, 0.34f, 0.42f);
                // "Hedefli": the block's one marked cube. The rest of the block keeps its card
                // colour, so the target stands out from its own siblings as well as the board.
                case CubeKind.Target: return ElementColor(BlockElement.Targeted);
                // "Snake": a deep scaly green that reads as one continuous animal across
                // however many cells it is lying on.
                case CubeKind.Snake: return new Color(0.18f, 0.55f, 0.28f);
                default: return ColorForCard(cube.SourceCardId);
            }
        }

        /// <summary>Short name of a CUBE kind, for a label that has to name what it acts on
        /// ("Antimadde" saying which element it annihilates).</summary>
        public static string KindLabel(CubeKind kind)
        {
            switch (kind)
            {
                case CubeKind.Fire: return ElementLabel(BlockElement.Fire);
                case CubeKind.Water: return ElementLabel(BlockElement.Water);
                case CubeKind.Obsidian: return ElementLabel(BlockElement.Obsidian);
                case CubeKind.Gold: return ElementLabel(BlockElement.Gold);
                case CubeKind.Transparent: return ElementLabel(BlockElement.Transparent);
                case CubeKind.Dynamite: return ElementLabel(BlockElement.Dynamite);
                case CubeKind.Ice: return Loc.Pick("ice", "buz");
                case CubeKind.Gangrene: return Loc.Pick("rot", "kangren");
                case CubeKind.Compressed: return Loc.Pick("pressed", "pres");
                case CubeKind.Target: return Loc.Pick("target", "hedef");
                case CubeKind.Snake: return Loc.Pick("snake", "yılan");
                default: return Loc.Pick("plain", "sade");
            }
        }

        /// <summary>Short display name of an element for card labels.</summary>
        public static string ElementLabel(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire: return Loc.Pick("FIRE", "ATEŞ");
                case BlockElement.Water: return Loc.Pick("WATER", "SU");
                case BlockElement.Obsidian: return Loc.Pick("OBSIDIAN", "OBSİDYEN");
                case BlockElement.Gold: return Loc.Pick("GOLD", "ALTIN");
                case BlockElement.Transparent: return Loc.Pick("GLASS", "CAM");
                case BlockElement.Ghost: return Loc.Pick("GHOST", "HAYALET");
                case BlockElement.Negative: return Loc.Pick("NEGATIVE", "NEGATİF");
                case BlockElement.Dynamite: return "TNT";
                case BlockElement.Mechanical: return Loc.Pick("GEARS", "ÇARK");
                case BlockElement.Fox: return Loc.Pick("FOX", "TİLKİ");
                case BlockElement.Targeted: return Loc.Pick("TARGETED", "HEDEFLİ");
                default: return element.ToString().ToUpperInvariant();
            }
        }

        /// <summary>One-line rules text of a block type, for hover tooltips
        /// (mirrors the enum docs in BlockElement.cs).</summary>
        public static string ElementDescription(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire:
                    return Loc.Pick("When one cube explodes, the whole block goes with it.",
                        "Bir küpü patlayınca bloğun tamamı onunla birlikte patlar.");
                case BlockElement.Water:
                    return Loc.Pick("Falls and spreads each turn; turns touching fire to obsidian.",
                        "Her tur düşer ve yayılır; değdiği ateşi obsidyene çevirir.");
                case BlockElement.Obsidian:
                    return Loc.Pick("Indestructible, but ignored by the clean-sweep check.",
                        "Yok edilemez, ama temizlik kontrolü onu saymaz.");
                case BlockElement.Gold:
                    return Loc.Pick("Indestructible and sweep-exempt; pays a bonus every turn on the board.",
                        "Yok edilemez ve temizliği bozmaz; alanda durduğu her tur bonus öder.");
                case BlockElement.Transparent:
                    return Loc.Pick("A block can be placed on top of it; the new cube replaces it.",
                        "Üstüne blok konabilir; yeni küp onun yerini alır.");
                case BlockElement.Ghost:
                    return Loc.Pick("Can be placed partly off the board (at least one cube on).",
                        "Kısmen alan dışına konabilir (en az bir küp içeride).");
                case BlockElement.Negative:
                    return Loc.Pick(
                        "Place it ON existing blocks to erase them. It leaves nothing behind - "
                            + "the cells end up empty. Obsidian and gold refuse it.",
                        "Mevcut blokların ÜSTÜNE konur ve onları siler. Geriye hiçbir şey "
                            + "bırakmaz, kareler boşalır. Obsidyen ve altın kabul etmez.");
                case BlockElement.Dynamite:
                    return Loc.Pick("If the whole block explodes the turn it lands, the board is cleared.",
                        "Blok tek seferde tümüyle patlarsa tüm alan temizlenir.");
                case BlockElement.Mechanical:
                    return Loc.Pick("Right-click it in hand to rotate 90 degrees.",
                        "Eldeyken sağ tık ile 90 derece döner.");
                case BlockElement.Fox:
                    return Loc.Pick("Right-click it in hand to reshape into any shape in your deck.",
                        "Eldeyken sağ tık ile destendeki herhangi bir şekle bürünür.");
                case BlockElement.Targeted:
                    return Loc.Pick(
                        "One marked cube is its target. Break the TARGET first and the block pays "
                            + "a bonus and goes up whole; break any other cube first and the "
                            + "block is spent - it just stands there.",
                        "İşaretli tek küpü onun hedefidir. Önce HEDEFİ patlatırsan blok bonus "
                            + "verir ve tümüyle patlar; önce başka bir küpü giderse bloğun "
                            + "etkisi kalmaz - orada öylece durur.");
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// A procedural "refresh" glyph: a ring of small squares with a gap on the right, and a
        /// tapered arrowhead at the end of the sweep. Built from rects like every other sprite
        /// in this project, so it needs no texture asset and no font that happens to carry the
        /// arrow codepoint.
        /// </summary>
        public static void MakeRefreshIcon(Transform parent, string name, Vector2 center,
            float radius, float thickness, Color color, int sortingOrder)
        {
            // Dense enough that the squares OVERLAP into a smooth ring: at 12 segments the gaps
            // between them were wider than the segments, which is what made the old icon read as
            // a lumpy letter rather than a circle.
            const int Segments = 30;
            const float StartDegrees = 400f;  // 40 degrees, one full turn on so the sweep is
            const float EndDegrees = 90f;     // clockwise and FINISHES at the top of the ring
            for (int i = 0; i < Segments; i++)
            {
                float degrees = Mathf.Lerp(StartDegrees, EndDegrees, i / (float)(Segments - 1));
                float radians = degrees * Mathf.Deg2Rad;
                Vector2 at = center
                    + new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
                MakeRect(parent, name + "_arc" + i, at,
                    new Vector2(thickness, thickness), color, sortingOrder);
            }
            // The arrowhead closes the ring at the TOP, which is the whole reason the sweep ends
            // there: MakeRect cannot rotate, and at the top of a circle the tangent is exactly
            // horizontal, so a triangle of axis-aligned bars really does point along the ring
            // (clockwise) instead of off at an angle.
            Vector2 tip = center + new Vector2(thickness * 1.9f, radius);
            for (int i = 0; i < 3; i++)
            {
                // Vertical bars marching back from the tip, growing taller - a right-pointing
                // triangle. The tip bar is one pixel of thickness, the last is the full head.
                MakeRect(parent, name + "_head" + i,
                    tip - new Vector2(thickness * 0.62f * i, 0f),
                    new Vector2(thickness * 0.62f, thickness * (0.7f + i * 1.15f)),
                    color, sortingOrder);
            }
        }

        /// <summary>Word-wraps, then CLIPS to a line budget with a trailing ellipsis. Panels
        /// here are fixed-size and TextMesh happily runs straight out the bottom of one, so
        /// anything drawn inside a tile has to be clamped rather than trusted to fit.</summary>
        public static string WrapText(string text, int maxCharsPerLine, int maxLines)
        {
            string wrapped = WrapText(text, maxCharsPerLine);
            if (maxLines <= 0)
            {
                return wrapped;
            }
            string[] lines = wrapped.Split('\n');
            if (lines.Length <= maxLines)
            {
                return wrapped;
            }
            var kept = new System.Text.StringBuilder();
            for (int i = 0; i < maxLines; i++)
            {
                if (i > 0)
                {
                    kept.Append('\n');
                }
                kept.Append(lines[i]);
            }
            kept.Append('…');
            return kept.ToString();
        }

        /// <summary>Greedy word wrap for the placeholder TextMesh labels (no auto-wrapping).</summary>
        public static string WrapText(string text, int maxCharsPerLine)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            string[] words = text.Split(' ');
            var sb = new System.Text.StringBuilder();
            int lineLength = 0;
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                if (lineLength > 0 && lineLength + 1 + word.Length > maxCharsPerLine)
                {
                    sb.Append('\n');
                    lineLength = 0;
                }
                else if (lineLength > 0)
                {
                    sb.Append(' ');
                    lineLength++;
                }
                sb.Append(word);
                lineLength += word.Length;
            }
            return sb.ToString();
        }

        /// <summary>Creates a square sprite object. Scale is uniform (a cell/tile).</summary>
        public static SpriteRenderer MakeCell(Transform parent, string name, Vector2 position,
            float scale, Color color, int sortingOrder)
        {
            SpriteRenderer renderer = MakeRect(parent, name, position,
                new Vector2(scale, scale), color, sortingOrder);
            return renderer;
        }

        /// <summary>Creates a world-space text (TextMesh) for labels like market prices.
        /// Keep fontSize high (~90) and characterSize small or TextMesh renders blurry;
        /// for text over busy backgrounds put a dark rect behind it (outline copies ghost).</summary>
        public static TextMesh MakeText3D(Transform parent, string name, Vector2 position,
            string text, int fontSize, float characterSize, Color color, int sortingOrder,
            TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            var textMesh = go.AddComponent<TextMesh>();
            Font font = UiFont;
            textMesh.font = font;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = characterSize;
            textMesh.color = color;
            textMesh.anchor = anchor;
            textMesh.text = text;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            meshRenderer.material = font.material;
            meshRenderer.sortingOrder = sortingOrder;
            return textMesh;
        }

        /// <summary>
        /// Scales a GENERATED plate sprite (one the code drew into a texture itself, like the
        /// board surface or its line glow) so that it covers exactly <paramref name="size"/>
        /// world units, whatever pixel dimensions its texture happened to come out at.
        ///
        /// USE THIS RATHER THAN ASSIGNING localScale DIRECTLY, and here is the trap it exists to
        /// close: Sprite.Create takes ONE pixels-per-unit for both axes, so a sprite made from a
        /// non-square texture is never 1x1 world units - it is (w/ppu) by (h/ppu). Setting the
        /// scale to the target size therefore only works while the texture is SQUARE, and every
        /// board in the game was square until retro mode grew four dead rows on top of one. The
        /// plate then came out narrower than the arena by exactly width/height, leaving the outer
        /// column of cells sitting off the edge of the board they belong to.
        ///
        /// Dividing by the sprite's own world size is exact for both cases: a square sprite is
        /// 1x1 and this is arithmetically the old assignment, so no square board moves by a pixel.
        /// </summary>
        public static void FitGeneratedPlate(SpriteRenderer renderer, Vector2 size)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }
            Vector2 spriteSize = renderer.sprite.bounds.size;
            renderer.transform.localScale = new Vector3(
                size.x / Mathf.Max(0.0001f, spriteSize.x),
                size.y / Mathf.Max(0.0001f, spriteSize.y),
                1f);
        }

        /// <summary>Creates a rectangular sprite object (position and size in local space).</summary>
        public static SpriteRenderer MakeRect(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            if (plainSpriteMaterial == null)
            {
                // Every sprite in the game is born here, so this is where the genuine built-in
                // sprite material can be taken from - see PlainSpriteMaterial for why it cannot
                // just be constructed.
                plainSpriteMaterial = renderer.sharedMaterial;
            }
            renderer.sprite = WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>A PAINTED panel: the rectangle MakeRect draws, but on a 9-SLICED sprite, so
        /// the frame keeps the thickness it was drawn with while only the middle stretches out
        /// to `size`. Falls back to MakeRect's flat rectangle when the art is missing, and the
        /// two are interchangeable from the caller's side - only the colour means something
        /// different, since over a sprite it MULTIPLIES the paint instead of filling.
        ///
        /// The size goes on the RENDERER, never on the transform: MakeRect sizes a 1x1 sprite
        /// by localScale, and scaling a sliced sprite would take its border along and undo the
        /// whole point of slicing it.</summary>
        public static SpriteRenderer MakePlate(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int sortingOrder, Sprite plate)
        {
            if (plate == null)
            {
                return MakeRect(parent, name, position, size, color, sortingOrder);
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = plate;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        /// <summary>A sprite drawn at a world SIZE, square. The icons under Art/Decks are
        /// authored square and imported at a PPU equal to their own pixel side, so one sprite
        /// unit is one world unit and the scale here IS the size - redrawing an icon at another
        /// resolution changes nothing at the call site.</summary>
        public static SpriteRenderer MakeIcon(Transform parent, string name, Vector2 position,
            float size, Color color, int sortingOrder, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
