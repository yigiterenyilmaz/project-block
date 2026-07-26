// PURPOSE: Tiny helpers for the placeholder UI (runtime-generated sprites, card colors).
// NOTE FOR AGENTS: everything under Assets/Scripts/View is intentionally disposable
// debug presentation. Game rules NEVER live here - they belong to ProjectBlock.Core.

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
                default: return ColorForCard(cube.SourceCardId);
            }
        }

        /// <summary>Short display name of an element for card labels.</summary>
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
                default: return Loc.Pick("plain", "sade");
            }
        }

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
                default:
                    return string.Empty;
            }
        }

        /// <summary>Greedy word wrap for the placeholder TextMesh labels (no auto-wrapping).</summary>
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
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        /// <summary>Creates a rectangular sprite object (position and size in local space).</summary>
        public static SpriteRenderer MakeRect(Transform parent, string name, Vector2 position,
            Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = WhiteSprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }
    }
}
