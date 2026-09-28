// PURPOSE: A small modal picker for one-off in-round choices. Two layouts behind one API:
//   - a LIST: a title and a vertical stack of labelled rows (the debug boss list);
//   - a COMPASS: a panel with four ARROW buttons, one on each side of a centre hub
//     ("Kütleçekim merkezi" choosing which way water falls). A direction is picked by pressing
//     the arrow that POINTS that way, which a list of four words never managed to say.
// Either way the controller reads the chosen option index and acts on it, and a gamepad steps
// the options spatially off OptionWorldCenter - so the compass's up arrow is up on the stick
// too, with nothing written for it.
// Placeholder presentation like everything else under View/.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectBlock.View
{
    /// <summary>Modal "pick one of these". While open, the controller blocks other input.</summary>
    public sealed class ChoicePickerView : MonoBehaviour
    {
        private const float RowWidth = 6.5f;
        private const float RowHeight = 0.82f;
        private const float RowPitch = 0.96f;

        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.23f);
        private static readonly Color LabelColor = new Color(0.92f, 0.94f, 0.98f);

        /// <summary>One option: where it is, how big its hit area is, and - for a compass - the
        /// way it points and the pieces that answer the hover.</summary>
        private sealed class Option
        {
            public Vector2 Center;
            public Vector2 Size;
            public Vector2Int Direction;
            public bool Enabled = true;
            public Transform Button;
            public SpriteRenderer Plate;
            public SpriteRenderer Arrow;
            public float Hover; // 0..1, eased toward the pointer
        }

        private readonly List<Option> options = new List<Option>();

        public bool IsOpen { get; private set; }

        /// <summary>True while the four-arrow layout is up (see ShowCompass).</summary>
        public bool IsCompass { get; private set; }

        public void Show(string title, IReadOnlyList<string> labels)
        {
            Hide();
            IsOpen = true;
            int n = labels.Count;
            float startY = (n - 1) * RowPitch * 0.5f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, startY + 1.1f),
                title, 48, 0.06f, Color.white, 41, TextAnchor.MiddleCenter);

            // Scaled to whatever the screen can show. On a phone held upright the rows are
            // wider than the view, and a picker you cannot read both ends of is a picker you
            // cannot use. See ViewUtil.FitOverlay - it never magnifies, so the desktop is
            // untouched.
            float top = startY + 1.1f + 0.4f;
            float bottom = startY - (n - 1) * RowPitch - RowHeight * 0.5f;
            ViewUtil.FitOverlay(transform, new Vector2(RowWidth + 0.6f, top - bottom),
                new Vector2(0f, (top + bottom) * 0.5f));

            for (int i = 0; i < n; i++)
            {
                var center = new Vector2(0f, startY - i * RowPitch);
                options.Add(new Option { Center = center, Size = new Vector2(RowWidth, RowHeight) });
                ViewUtil.MakeRect(transform, "Row_" + i, center,
                    new Vector2(RowWidth, RowHeight), RowColor, 41);
                ViewUtil.MakeText3D(transform, "Label_" + i, center, labels[i],
                    60, 0.05f, LabelColor, 42, TextAnchor.MiddleCenter);
            }
        }

        // ---------------------------------------------------------------- the compass

        private static class Compass
        {
            public const float PanelSide = 5.7f;
            public const float Reach = 1.8f;          // hub centre to a button's centre
            public const float ButtonSide = 1.5f;
            public const float ArrowSize = 0.78f;
            public const float HubSide = 1.2f;
            public const float HoverScale = 1.08f;
            public const float HoverRate = 14f;       // per second, toward the pointer
            public const float OpenSeconds = 0.14f;

            public static readonly Color Panel = new Color(0.10f, 0.12f, 0.16f, 0.97f);
            public static readonly Color Rim = new Color(0.34f, 0.56f, 0.74f, 0.55f);
            public static readonly Color Button = new Color(0.17f, 0.21f, 0.28f, 1f);
            public static readonly Color ButtonHover = new Color(0.20f, 0.40f, 0.56f, 1f);
            public static readonly Color ButtonOff = new Color(0.12f, 0.13f, 0.15f, 1f);
            public static readonly Color Arrow = new Color(0.78f, 0.93f, 1f, 1f);
            public static readonly Color ArrowHover = new Color(1f, 1f, 1f, 1f);
            public static readonly Color ArrowOff = new Color(0.40f, 0.44f, 0.50f, 1f);
            public static readonly Color Hub = new Color(0.16f, 0.42f, 0.70f, 1f);
            public static readonly Color HubArrow = new Color(0.86f, 0.95f, 1f, 0.95f);
            public static readonly Color Muted = new Color(0.66f, 0.72f, 0.80f, 1f);
        }

        /// <summary>The compass body - everything but the dim - so the open can ease it in
        /// without touching the root, whose scale belongs to FitOverlay.</summary>
        private Transform compassBody;

        private float openTime;

        /// <summary>
        /// Opens the four-arrow layout. Each option is a DIRECTION (one step along an axis) and a
        /// short label printed under its arrow; the option indices are the order given, exactly
        /// as a list's rows are. <paramref name="current"/> is the way things point NOW: the hub
        /// shows it, and its own arrow is switched off, because choosing it again would spend the
        /// charge on nothing.
        /// </summary>
        public void ShowCompass(string title, string subtitle, IReadOnlyList<Vector2Int> directions,
            IReadOnlyList<string> labels, Vector2Int current, string currentLabel, string footer)
        {
            Hide();
            IsOpen = true;
            IsCompass = true;
            openTime = 0f;

            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f),
                new Color(0f, 0f, 0f, 0.82f), ViewUtil.MenuDimOrder);

            var bodyGo = new GameObject("Compass");
            bodyGo.transform.SetParent(transform, false);
            compassBody = bodyGo.transform;

            float half = Compass.PanelSide * 0.5f;
            // One menu plate under the title, the compass, the current-flow line and the footer.
            ViewUtil.MakeMenuPlate(compassBody, new Vector2(0f, 0.125f),
                new Vector2(Compass.PanelSide + 2.4f, Compass.PanelSide + 3.15f));
            ViewUtil.MakeText3D(compassBody, "Title", new Vector2(0f, half + 1.05f),
                title, 60, 0.06f, Color.white, 43, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(compassBody, "Subtitle", new Vector2(0f, half + 0.45f),
                subtitle, 60, 0.042f, Compass.Muted, 43, TextAnchor.MiddleCenter);

            // A thin lit rim is a second rounded plate a hair bigger, behind the panel.
            ViewUtil.MakeRounded(compassBody, "Rim", Vector2.zero,
                new Vector2(Compass.PanelSide + 0.08f, Compass.PanelSide + 0.08f), Compass.Rim, 41);
            ViewUtil.MakeRounded(compassBody, "Panel", Vector2.zero,
                new Vector2(Compass.PanelSide, Compass.PanelSide), Compass.Panel, 42);

            // THE HUB says what is true now: a water-blue tile with a small arrow the way the
            // flow already goes, and the word under the panel.
            ViewUtil.MakeRounded(compassBody, "Hub", Vector2.zero,
                new Vector2(Compass.HubSide, Compass.HubSide), Compass.Hub, 43);
            SpriteRenderer hubArrow = ViewUtil.MakeIcon(compassBody, "HubArrow", Vector2.zero,
                Compass.HubSide * 0.62f, Compass.HubArrow, 44, ArrowSprite);
            hubArrow.transform.localRotation = RotationFor(current);

            for (int i = 0; i < directions.Count; i++)
            {
                options.Add(BuildArrowButton(i, directions[i], i < labels.Count ? labels[i] : "",
                    directions[i] != current));
            }

            ViewUtil.MakeText3D(compassBody, "Current", new Vector2(0f, -half - 0.42f),
                currentLabel, 60, 0.042f, Compass.Muted, 43, TextAnchor.MiddleCenter);
            if (!string.IsNullOrEmpty(footer))
            {
                ViewUtil.MakeText3D(compassBody, "Footer", new Vector2(0f, -half - 0.95f),
                    footer, 60, 0.036f, Compass.Muted, 43, TextAnchor.MiddleCenter);
            }

            float top = half + 1.75f;     // the menu plate's own top and bottom
            float bottom = -half - 1.5f;
            ViewUtil.FitOverlay(transform, new Vector2(Compass.PanelSide + 2.6f, top - bottom),
                new Vector2(0f, (top + bottom) * 0.5f));
            compassBody.localScale = Vector3.one * 0.92f;
        }

        private Option BuildArrowButton(int index, Vector2Int direction, string label, bool enabled)
        {
            var center = new Vector2(direction.x, direction.y) * Compass.Reach;
            var go = new GameObject("Arrow_" + index);
            go.transform.SetParent(compassBody, false);
            go.transform.localPosition = center;

            var option = new Option
            {
                Center = center,
                Size = new Vector2(Compass.ButtonSide, Compass.ButtonSide),
                Direction = direction,
                Enabled = enabled,
                Button = go.transform
            };
            option.Plate = ViewUtil.MakeRounded(go.transform, "Plate", Vector2.zero, option.Size,
                enabled ? Compass.Button : Compass.ButtonOff, 43);
            option.Arrow = ViewUtil.MakeIcon(go.transform, "Arrow", new Vector2(0f, 0.14f),
                Compass.ArrowSize, enabled ? Compass.Arrow : Compass.ArrowOff, 44, ArrowSprite);
            option.Arrow.transform.localRotation = RotationFor(direction);
            ViewUtil.MakeText3D(go.transform, "Label", new Vector2(0f, -0.5f), label, 60,
                0.034f, enabled ? LabelColor : Compass.ArrowOff, 45, TextAnchor.MiddleCenter);
            return option;
        }

        private static Quaternion RotationFor(Vector2Int direction)
        {
            // The baked arrow points RIGHT (+x).
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 0f, angle);
        }

        private void Update()
        {
            if (!IsOpen || !IsCompass || compassBody == null)
            {
                return;
            }
            // THE OPEN: the panel settles in from a touch smaller - a menu arriving rather than
            // a sprite switching on. Eased out, a few frames, no overshoot.
            if (openTime < Compass.OpenSeconds)
            {
                openTime += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(openTime / Compass.OpenSeconds);
                float eased = 1f - (1f - k) * (1f - k);
                compassBody.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, eased);
            }

            // THE HOVER follows the pointer - which is the pad's snapped pointer too, so a stick
            // push lights the arrow it moved to with nothing written for it.
            int under = -1;
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                under = OptionAt(cam.ScreenToWorldPoint(mouse.position.ReadValue()));
            }
            float step = 1f - Mathf.Exp(-Compass.HoverRate * Time.unscaledDeltaTime);
            for (int i = 0; i < options.Count; i++)
            {
                Option option = options[i];
                if (option.Button == null || !option.Enabled)
                {
                    continue;
                }
                option.Hover = Mathf.Lerp(option.Hover, i == under ? 1f : 0f, step);
                float s = Mathf.Lerp(1f, Compass.HoverScale, option.Hover);
                option.Button.localScale = new Vector3(s, s, 1f);
                option.Plate.color = Color.Lerp(Compass.Button, Compass.ButtonHover, option.Hover);
                option.Arrow.color = Color.Lerp(Compass.Arrow, Compass.ArrowHover, option.Hover);
                // The arrow leans a little the way it points - the flow it is offering.
                Vector2 lean = new Vector2(option.Direction.x, option.Direction.y) * 0.06f * option.Hover;
                option.Arrow.transform.localPosition = new Vector3(lean.x, 0.14f + lean.y, 0f);
            }
        }

        // ------------------------------------------------------------ the arrow sprite

        private const int ArrowPixels = 128;
        private static Sprite arrowSprite;

        /// <summary>
        /// A chunky white arrow pointing RIGHT, baked once: a round-ended shaft and a broad head.
        /// Covered by 4x4 supersampling, so the edge is antialiased without any blur. PPU equal
        /// to its side, so one sprite unit is one world unit and MakeIcon's size IS the size.
        /// </summary>
        internal static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite != null)
                {
                    return arrowSprite;
                }
                var tex = new Texture2D(ArrowPixels, ArrowPixels, TextureFormat.RGBA32, false);
                const int samples = 4;
                for (int y = 0; y < ArrowPixels; y++)
                {
                    for (int x = 0; x < ArrowPixels; x++)
                    {
                        int hit = 0;
                        for (int sy = 0; sy < samples; sy++)
                        {
                            for (int sx = 0; sx < samples; sx++)
                            {
                                float u = (x + (sx + 0.5f) / samples) / ArrowPixels;
                                float v = (y + (sy + 0.5f) / samples) / ArrowPixels;
                                if (InsideArrow(u, v))
                                {
                                    hit++;
                                }
                            }
                        }
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, hit / (float)(samples * samples)));
                    }
                }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                arrowSprite = Sprite.Create(tex, new Rect(0, 0, ArrowPixels, ArrowPixels),
                    new Vector2(0.5f, 0.5f), ArrowPixels);
                return arrowSprite;
            }
        }

        /// <summary>The arrow in unit space, pointing +u, centred on v = 0.5.</summary>
        private static bool InsideArrow(float u, float v)
        {
            float dy = Mathf.Abs(v - 0.5f);
            // Shaft: a capsule from u 0.12 to the head's base, half-thickness 0.11.
            const float shaftHalf = 0.11f;
            const float shaftStart = 0.12f + shaftHalf;
            const float headBase = 0.50f;
            if (u >= shaftStart && u <= headBase + 0.02f && dy <= shaftHalf)
            {
                return true;
            }
            float cu = u - shaftStart;
            if (cu < 0f && cu * cu + dy * dy <= shaftHalf * shaftHalf)
            {
                return true; // the round tail
            }
            // Head: a triangle from the base (half-height 0.36) to the tip at u 0.9.
            const float tip = 0.90f;
            const float headHalf = 0.36f;
            if (u < headBase || u > tip)
            {
                return false;
            }
            return dy <= headHalf * (tip - u) / (tip - headBase);
        }

        // ------------------------------------------------------------------ queries

        /// <summary>The options, for a gamepad to step (see GameUiController.PadPanels.cs).</summary>
        public int OptionCount
        {
            get { return options.Count; }
        }

        public Vector2? OptionWorldCenter(int index)
        {
            if (index < 0 || index >= options.Count)
            {
                return null;
            }
            // Through the transform: the centres are local, and the overlay is scaled and moved
            // to fit the screen, so local and world are no longer the same point.
            return transform.TransformPoint(options[index].Center);
        }

        /// <summary>False for an option shown but not offered (the compass's current direction).
        /// </summary>
        public bool IsOptionEnabled(int index)
        {
            return index >= 0 && index < options.Count && options[index].Enabled;
        }

        /// <summary>The compass option pointing <paramref name="direction"/>, or -1 - what an
        /// arrow key asks for.</summary>
        public int OptionInDirection(Vector2Int direction)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (IsCompass && options[i].Direction == direction)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>True when a world point is on the compass's panel. A click there that hits
        /// no enabled arrow is a miss inside the menu, not a way out of it.</summary>
        public bool IsOnPanel(Vector2 world)
        {
            if (!IsCompass)
            {
                return false;
            }
            Vector2 local = transform.InverseTransformPoint(world);
            float half = Compass.PanelSide * 0.5f;
            return Mathf.Abs(local.x) <= half && Mathf.Abs(local.y) <= half;
        }

        /// <summary>Enabled option under a world point, or -1.</summary>
        public int OptionAt(Vector2 world)
        {
            Vector2 local = transform.InverseTransformPoint(world);
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Enabled
                    && Mathf.Abs(local.x - options[i].Center.x) <= options[i].Size.x * 0.5f
                    && Mathf.Abs(local.y - options[i].Center.y) <= options[i].Size.y * 0.5f)
                {
                    return i;
                }
            }
            return -1;
        }

        public void Hide()
        {
            IsOpen = false;
            IsCompass = false;
            compassBody = null;
            options.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
