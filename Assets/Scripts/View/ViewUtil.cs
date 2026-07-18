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
                case BlockElement.Mirror: return new Color(0.75f, 0.82f, 0.85f);
                case BlockElement.Fox: return new Color(0.85f, 0.5f, 0.2f);
                case BlockElement.PiggyBank: return new Color(1f, 0.55f, 0.7f);
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
                case CubeKind.PiggyBank: return ElementColor(BlockElement.PiggyBank);
                case CubeKind.Dynamite: return ElementColor(BlockElement.Dynamite);
                default: return ColorForCard(cube.SourceCardId);
            }
        }

        /// <summary>Short display name of an element for card labels.</summary>
        public static string ElementLabel(BlockElement element)
        {
            switch (element)
            {
                case BlockElement.Dynamite: return "TNT";
                case BlockElement.PiggyBank: return "PIGGY";
                case BlockElement.Mechanical: return "GEARS";
                case BlockElement.Transparent: return "GLASS";
                default: return element.ToString().ToUpperInvariant();
            }
        }

        /// <summary>Creates a square sprite object. Scale is uniform (a cell/tile).</summary>
        public static SpriteRenderer MakeCell(Transform parent, string name, Vector2 position,
            float scale, Color color, int sortingOrder)
        {
            SpriteRenderer renderer = MakeRect(parent, name, position,
                new Vector2(scale, scale), color, sortingOrder);
            return renderer;
        }

        /// <summary>Creates a world-space text (TextMesh) for labels like market prices.</summary>
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
