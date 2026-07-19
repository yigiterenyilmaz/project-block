// PURPOSE: A two-digit "locker" input for the Batak bet - a tens dial and a ones dial (0-9 each,
// so 00-99), each nudged by a + / - button or the mouse wheel, with Confirm / Cancel. Replaces the
// old 1-8 list so a bet can run up to 99. Placeholder presentation like the other View/ modals; the
// controller reads Value and calls GameSession.PlaceBatakBet.

using ProjectBlock.Core;
using UnityEngine;

namespace ProjectBlock.View
{
    /// <summary>Modal two-digit bet dial. While open, the controller blocks other input.</summary>
    public sealed class BatakBetView : MonoBehaviour
    {
        private const float DigitX = 0.95f; // tens dial at -DigitX, ones dial at +DigitX
        private static readonly Vector2 PlusOffset = new Vector2(0f, 1.05f);
        private static readonly Vector2 MinusOffset = new Vector2(0f, -1.05f);
        private static readonly Vector2 DialButtonSize = new Vector2(1.2f, 0.6f);
        private static readonly Vector2 ColumnSize = new Vector2(1.4f, 3.0f);
        private static readonly Vector2 ButtonSize = new Vector2(2.3f, 0.72f);
        private static readonly Vector2 ConfirmCenter = new Vector2(-1.45f, -2.7f);
        private static readonly Vector2 CancelCenter = new Vector2(1.45f, -2.7f);

        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.82f);
        private static readonly Color DialColor = new Color(0.18f, 0.22f, 0.30f);
        private static readonly Color ConfirmOn = new Color(0.18f, 0.42f, 0.24f);
        private static readonly Color ConfirmOff = new Color(0.16f, 0.16f, 0.18f);
        private static readonly Color CancelColor = new Color(0.45f, 0.22f, 0.22f);
        private static readonly Color Ink = new Color(0.95f, 0.96f, 1f);
        private static readonly Color Faint = new Color(0.6f, 0.6f, 0.62f);

        private int tens;
        private int ones;
        private string title = "";

        public bool IsOpen { get; private set; }

        /// <summary>The current two-digit bet (0-99).</summary>
        public int Value
        {
            get { return tens * 10 + ones; }
        }

        public void Show(string heading)
        {
            title = heading;
            tens = 0;
            ones = 1; // default 01 - the minimum legal bet
            IsOpen = true;
            Rebuild();
        }

        public void Hide()
        {
            IsOpen = false;
            Clear();
        }

        /// <summary>Nudges a dial (0 = tens, 1 = ones) by delta, clamped to a single digit.</summary>
        public void Bump(int dial, int delta)
        {
            if (dial == 0)
            {
                tens = Mathf.Clamp(tens + delta, 0, 9);
            }
            else if (dial == 1)
            {
                ones = Mathf.Clamp(ones + delta, 0, 9);
            }
            Rebuild();
        }

        /// <summary>Dial (0/1) whose "+" button is under the point, or -1.</summary>
        public int PlusAt(Vector2 world)
        {
            return DialButtonHit(world, PlusOffset);
        }

        /// <summary>Dial (0/1) whose "-" button is under the point, or -1.</summary>
        public int MinusAt(Vector2 world)
        {
            return DialButtonHit(world, MinusOffset);
        }

        /// <summary>Dial column (0/1) under the point - used for mouse-wheel spinning, or -1.</summary>
        public int DialColumnAt(Vector2 world)
        {
            if (Within(world, new Vector2(-DigitX, 0f), ColumnSize)) return 0;
            if (Within(world, new Vector2(DigitX, 0f), ColumnSize)) return 1;
            return -1;
        }

        public bool ConfirmAt(Vector2 world)
        {
            return Value >= 1 && Within(world, ConfirmCenter, ButtonSize);
        }

        public bool CancelAt(Vector2 world)
        {
            return Within(world, CancelCenter, ButtonSize);
        }

        private int DialButtonHit(Vector2 world, Vector2 offset)
        {
            if (Within(world, new Vector2(-DigitX, 0f) + offset, DialButtonSize)) return 0;
            if (Within(world, new Vector2(DigitX, 0f) + offset, DialButtonSize)) return 1;
            return -1;
        }

        private void Rebuild()
        {
            Clear();
            ViewUtil.MakeRect(transform, "Dim", Vector2.zero, new Vector2(30f, 14f), DimColor, 40);
            ViewUtil.MakeText3D(transform, "Title", new Vector2(0f, 2.8f), title, 46, 0.045f,
                Color.white, 42, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, "Hint", new Vector2(0f, 2.3f),
                Loc.Pick("scroll or +/- to set, then Bet", "kaydır ya da +/- ile ayarla, sonra Bahis"),
                90, 0.02f, Faint, 42, TextAnchor.MiddleCenter);

            DrawDial(-DigitX, tens, "Tens");
            DrawDial(DigitX, ones, "Ones");

            bool ok = Value >= 1;
            ViewUtil.MakeRect(transform, "Confirm", ConfirmCenter, ButtonSize, ok ? ConfirmOn : ConfirmOff, 41);
            ViewUtil.MakeText3D(transform, "ConfirmT", ConfirmCenter,
                Loc.Pick("BET ", "BAHIS ") + Value, 52, 0.04f, ok ? Ink : Faint, 42,
                TextAnchor.MiddleCenter);
            ViewUtil.MakeRect(transform, "Cancel", CancelCenter, ButtonSize, CancelColor, 41);
            ViewUtil.MakeText3D(transform, "CancelT", CancelCenter,
                Loc.Pick("Cancel", "Vazgeç"), 52, 0.04f, Ink, 42, TextAnchor.MiddleCenter);
        }

        private void DrawDial(float x, int digit, string key)
        {
            ViewUtil.MakeRect(transform, key + "Plus", new Vector2(x, 0f) + PlusOffset, DialButtonSize, DialColor, 41);
            ViewUtil.MakeText3D(transform, key + "PlusT", new Vector2(x, 0f) + PlusOffset, "+", 60, 0.06f, Ink, 42, TextAnchor.MiddleCenter);
            ViewUtil.MakeText3D(transform, key + "Digit", new Vector2(x, 0f), digit.ToString(), 96, 0.12f, Ink, 42, TextAnchor.MiddleCenter);
            ViewUtil.MakeRect(transform, key + "Minus", new Vector2(x, 0f) + MinusOffset, DialButtonSize, DialColor, 41);
            ViewUtil.MakeText3D(transform, key + "MinusT", new Vector2(x, 0f) + MinusOffset, "-", 60, 0.06f, Ink, 42, TextAnchor.MiddleCenter);
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }

        private static bool Within(Vector2 p, Vector2 center, Vector2 size)
        {
            return Mathf.Abs(p.x - center.x) <= size.x * 0.5f
                && Mathf.Abs(p.y - center.y) <= size.y * 0.5f;
        }
    }
}
