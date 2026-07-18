// PURPOSE: Tiny helpers for the placeholder UI (runtime-generated sprites, card colors).
// NOTE FOR AGENTS: everything under Assets/Scripts/View is intentionally disposable
// debug presentation. Game rules NEVER live here - they belong to ProjectBlock.Core.

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

        /// <summary>Creates a square sprite object. Scale is uniform (a cell/tile).</summary>
        public static SpriteRenderer MakeCell(Transform parent, string name, Vector2 position,
            float scale, Color color, int sortingOrder)
        {
            SpriteRenderer renderer = MakeRect(parent, name, position,
                new Vector2(scale, scale), color, sortingOrder);
            return renderer;
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
