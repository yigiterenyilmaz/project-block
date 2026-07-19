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
                case BlockElement.Dynamite: return new Color(0.88f, 0.2f, 0.15f);
                case BlockElement.Mechanical: return new Color(0.6f, 0.65f, 0.7f);
                case BlockElement.Fox: return new Color(0.85f, 0.5f, 0.2f);
                default: return Color.gray;
            }
        }

        /// <summary>Board color of a cube: element kinds get their signature color,
        /// plain cubes keep their card's color.</summary>
        public static Color CubeDisplayColor(Cube cube)
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
                default: return ColorForCard(cube.SourceCardId);
            }
        }

        /// <summary>Short display name of an element for card labels.</summary>
        public static string ElementLabel(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Dynamite: return "TNT";
                case BlockElement.Mechanical: return "GEARS";
                case BlockElement.Transparent: return "GLASS";
                default: return element.ToString().ToUpperInvariant();
            }
        }

        /// <summary>One-line rules text of a block type, for hover tooltips (English for now;
        /// mirrors the enum docs in BlockElement.cs).</summary>
        public static string ElementDescription(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Fire:
                    return "When one cube explodes, the whole block goes with it.";
                case BlockElement.Water:
                    return "Falls and spreads each turn; turns touching fire to obsidian.";
                case BlockElement.Obsidian:
                    return "Indestructible, but ignored by the clean-sweep check.";
                case BlockElement.Gold:
                    return "Indestructible and sweep-exempt; pays a bonus every turn on the board.";
                case BlockElement.Transparent:
                    return "A block can be placed on top of it; the new cube replaces it.";
                case BlockElement.Ghost:
                    return "Can be placed partly off the board (at least one cube on).";
                case BlockElement.Dynamite:
                    return "If the whole block explodes the turn it lands, the board is cleared.";
                case BlockElement.Mechanical:
                    return "Right-click it in hand to rotate 90 degrees.";
                case BlockElement.Fox:
                    return "Right-click it in hand to reshape into any shape in your deck.";
                default:
                    return string.Empty;
            }
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
