// PURPOSE: One VERTICAL card in the joker or power bar - the shared shape both bars draw, so the
// two can never drift apart. A tinted body (the live state colour), a frame ring (the rarity
// colour), an ICON WELL across the top and the name and one status line under it.
// NOTE FOR AGENTS: the well holds the joker/power icon (ViewUtil.JokerIcon/PowerIcon, by DefId);
// an item with no art yet shows the empty recess. Everything else is built from Art/Cards
// (card_base, card_frame), white 9-sliced sprites whose Image colour is the whole look - drop in
// real card art under the same names and nothing here changes. Missing art falls back to flat
// rectangles.

using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>The widgets of one bar card. Built once per slot, refilled every Refresh.</summary>
    public sealed class HeldItemCard
    {
        public GameObject Root;

        /// <summary>The card body, coloured by the item's live state (ready, silenced...).</summary>
        public Image Body;

        /// <summary>The rim, coloured by rarity. Replaces the old edge strip.</summary>
        public Image Frame;

        /// <summary>The recess the icon sits in.</summary>
        public Image Well;

        /// <summary>The joker/power art, inside the well. Hidden when it has none yet.</summary>
        public Image Icon;

        /// <summary>An icon on a spent or switched-off card is dimmed rather than hidden, so the
        /// card is still recognisable while it says it cannot be used.</summary>
        private static readonly Color DimIconColor = new Color(0.45f, 0.45f, 0.48f, 0.85f);

        public Text Hotkey;

        public Text Title;

        public Text Status;

        public int InstanceId = -1;

        private static readonly Color WellColor = new Color(0f, 0f, 0f, 0.38f);

        /// <summary>Makes the card under <paramref name="parent"/>. Size and text placement come
        /// later from <see cref="Layout"/>, because the layout profile can change under it.</summary>
        public static HeldItemCard Create(Transform parent, string name, Color titleColor,
            Color statusColor)
        {
            var card = new HeldItemCard();
            card.Root = new GameObject(name);
            card.Root.transform.SetParent(parent, false);
            card.Root.AddComponent<RectTransform>();

            card.Body = card.Root.AddComponent<Image>();
            Sliced(card.Body, ViewUtil.CardSprite("card_base"));

            card.Well = MakeImage(card.Root.transform, "IconWell", ViewUtil.CardSprite("card_base"));
            card.Well.color = WellColor;
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(card.Well.transform, false);
            card.Icon = iconGo.AddComponent<Image>();
            card.Icon.raycastTarget = false;
            // The art is drawn 3:4 on a transparent ground; keeping its aspect is what stops a
            // well of a slightly different shape from squashing it.
            card.Icon.preserveAspect = true;
            // A NEGATIVE inset: the art carries a wide transparent margin round its subject, and
            // letting the image run a little past the recess is what makes the subject fill it.
            Stretch(card.Icon.rectTransform, -12f);
            card.Icon.enabled = false;
            card.Frame = MakeImage(card.Root.transform, "Frame", ViewUtil.CardSprite("card_frame"));
            Stretch(card.Frame.rectTransform, 0f);

            card.Hotkey = MakeText(card.Root.transform, "Hotkey", FontStyle.Bold, titleColor,
                TextAnchor.UpperLeft);
            card.Title = MakeText(card.Root.transform, "Title", FontStyle.Bold, titleColor,
                TextAnchor.MiddleCenter);
            card.Status = MakeText(card.Root.transform, "Status", FontStyle.Normal, statusColor,
                TextAnchor.UpperCenter);
            return card;
        }

        /// <summary>Shows <paramref name="sprite"/> in the well, or nothing when it is null.</summary>
        public void SetIcon(Sprite sprite, bool dim)
        {
            Icon.sprite = sprite;
            Icon.enabled = sprite != null;
            Icon.color = dim ? DimIconColor : Color.white;
        }

        /// <summary>Places everything inside a card of <paramref name="size"/> canvas pixels. A
        /// <paramref name="compact"/> card (the phone row) has no room for the status line.</summary>
        public void Layout(Vector2 size, bool compact)
        {
            float pad = compact ? 5f : 7f;
            float wellWidth = size.x - pad * 2f;
            // The icons are drawn 3:4, so the well is a little taller than a square would be -
            // capped so the name always keeps its share of the card.
            float wellHeight = Mathf.Min(wellWidth * 0.86f, size.y * (compact ? 0.52f : 0.55f));
            Place(Well.rectTransform, new Vector2(0f, -pad), new Vector2(wellWidth, wellHeight));

            float below = size.y - pad - wellHeight;
            float titleHeight = compact ? below - pad : below * 0.52f;
            Place(Title.rectTransform, new Vector2(0f, -pad - wellHeight - 2f),
                new Vector2(size.x - pad * 2f, titleHeight));
            // Type follows the card's width, so resizing the bars in UiLayout is the only change a
            // bigger or smaller card needs.
            Title.resizeTextMaxSize = Mathf.RoundToInt(size.x * 0.14f);
            Title.resizeTextMinSize = 9;

            Status.gameObject.SetActive(!compact);
            Place(Status.rectTransform, new Vector2(0f, -pad - wellHeight - 2f - titleHeight),
                new Vector2(size.x - pad * 2f, below - titleHeight - pad));
            Status.resizeTextMaxSize = Mathf.RoundToInt(size.x * 0.105f);
            Status.resizeTextMinSize = 8;

            Hotkey.fontSize = Mathf.RoundToInt(size.x * 0.12f);
            Place(Hotkey.rectTransform, new Vector2(0f, -pad - 2f),
                new Vector2(wellWidth - 8f, Hotkey.fontSize + 6f));
        }

        // ---- building blocks ----------------------------------------------------------

        private static Image MakeImage(Transform parent, string name, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            Sliced(image, sprite);
            return image;
        }

        private static void Sliced(Image image, Sprite sprite)
        {
            image.raycastTarget = false;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }
        }

        private static Text MakeText(Transform parent, string name, FontStyle style, Color color,
            TextAnchor alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            // The real bold cut, not fontStyle - see ViewUtil.UiFontFor.
            text.font = ViewUtil.UiFontFor(style);
            text.color = color;
            text.alignment = alignment;
            // Names run from "Klon" to "Uzun Vadeli Yatırımcı" in a card 112 wide, so the text
            // wraps and shrinks to fit rather than overflowing into the next card.
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.lineSpacing = 0.9f;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Anchors a child to the card's top centre, offset down from it.</summary>
        private static void Place(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
