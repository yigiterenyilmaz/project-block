// PURPOSE: One VERTICAL card in the joker or power bar - the shared shape both bars draw, so the
// two can never drift apart. A tinted body (the live state colour), a frame ring (the rarity
// colour), an ICON WELL across the top and the name and one status line under it.
// NOTE FOR AGENTS: the well holds the joker/power icon (ViewUtil.JokerIcon/PowerIcon, by DefId);
// an item with no art yet shows the empty recess. Everything else is built from Art/Cards
// (card_base, card_frame), white 9-sliced sprites whose Image colour is the whole look - drop in
// real card art under the same names and nothing here changes. Missing art falls back to flat
// rectangles.
//
// PAINTED MODE. A bar may instead name a WHOLE PAINTED CARD (the joker bar asks for
// Art/Cards/card_joker), and then the art is the card: its own border, its own recess for the
// icon, its own plate for the name. Three things change and nothing else:
//
//   1. THE ART IS DRAWN AS IT WAS PAINTED - Simple rather than 9-sliced, and white rather than
//      tinted. Slicing a painted card stretches its gold divider and its corner radius into a
//      different card at every size; tinting it by the state colour (which is a DARK panel
//      colour, 0.13-0.42) blacks the painting out. So the state becomes a CAST over the art and
//      the rarity ring is switched off, because the art already has a border and two borders
//      fighting is worse than no rarity ring. Rarity still reads: it is on the name.
//   2. EVERYTHING IS PLACED BY THE ART'S OWN REGIONS, as fractions of the card - measured off the
//      file, not guessed - so the icon lands in the recess and the name lands on the plate at any
//      card size, on the phone layout as on the desktop one.
//   3. THE ICON KEEPS ITS 3:4 AND IS FITTED TO THE RECESS. The fifty joker icons are 451x604 with
//      a wide and VARIABLE transparent margin round each subject, so fitting the FRAME rather
//      than the subject is what keeps them the size their artist drew them relative to one
//      another. ArtIconFill is the one number to move if they want to sit larger.
//
// Dropping card_joker.png out of Resources takes every card straight back to the flat look.

using UnityEngine;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>The widgets of one bar card. Built once per slot, refilled every Refresh.</summary>
    public sealed class HeldItemCard
    {
        public GameObject Root;

        /// <summary>The card body, coloured by the item's live state (ready, silenced...) - or
        /// the painted card itself, and then the state is only a cast over it.</summary>
        public Image Body;

        /// <summary>The rim, coloured by rarity. Replaces the old edge strip. Off on a painted
        /// card, which has a border of its own.</summary>
        public Image Frame;

        /// <summary>The recess the icon sits in. Off on a painted card, which has one.</summary>
        public Image Well;

        /// <summary>The joker/power art, inside the well. Hidden when it has none yet.</summary>
        public Image Icon;

        /// <summary>This card's halo, or null on a card no bar gave a glow layer to. It does NOT
        /// live under the card: every glow is parented into one layer drawn beneath every card,
        /// because a uGUI child always draws OVER its parent and a halo on top of the painting is
        /// a wash over it. See CardGlowFx for what drives it.</summary>
        public CardGlowFx Glow;

        /// <summary>An icon on a spent or switched-off card is dimmed rather than hidden, so the
        /// card is still recognisable while it says it cannot be used.</summary>
        private static readonly Color DimIconColor = new Color(0.45f, 0.45f, 0.48f, 0.85f);

        public Text Hotkey;

        public Text Title;

        public Text Status;

        public int InstanceId = -1;

        private static readonly Color WellColor = new Color(0f, 0f, 0f, 0.38f);

        /// <summary>
        /// WHAT A PAINTED CARD IS MADE OF - its own regions, as fractions of itself.
        ///
        /// There are two of them now and they are not the same shape, which is the whole reason
        /// this is a type rather than eight constants: the JOKER card is upright with a name
        /// plate across the bottom, the POWER card is nearly square and has NO PLATE AT ALL - it
        /// is a frame round an icon and nothing else. Code that assumes every painted card has
        /// somewhere to write a name puts the name on the border.
        ///
        /// MEASURED OFF THE FILES, never estimated. The joker card's recess is centred but its
        /// plate is NOT - it sits about 2% right of centre - so every edge is carried separately
        /// rather than derived from a shared middle.
        /// </summary>
        public struct CardArt
        {
            public float IconTop;
            public float IconBottom;
            public float IconLeft;
            public float IconRight;

            /// <summary>False for a card that is only a frame: nothing may be written on it.
            /// </summary>
            public bool HasPlate;

            public float PlateTop;
            public float PlateBottom;
            public float PlateLeft;
            public float PlateRight;

            /// <summary>A card with a plate: an icon recess and somewhere to write.</summary>
            public CardArt(float iconTop, float iconBottom, float iconLeft, float iconRight,
                float plateTop, float plateBottom, float plateLeft, float plateRight)
            {
                IconTop = iconTop;
                IconBottom = iconBottom;
                IconLeft = iconLeft;
                IconRight = iconRight;
                HasPlate = true;
                PlateTop = plateTop;
                PlateBottom = plateBottom;
                PlateLeft = plateLeft;
                PlateRight = plateRight;
            }

            /// <summary>A card that is only a frame round its icon.</summary>
            public CardArt(float iconTop, float iconBottom, float iconLeft, float iconRight)
            {
                IconTop = iconTop;
                IconBottom = iconBottom;
                IconLeft = iconLeft;
                IconRight = iconRight;
                HasPlate = false;
                PlateTop = 0f;
                PlateBottom = 0f;
                PlateLeft = 0f;
                PlateRight = 0f;
            }
        }

        /// <summary>Art/Cards/card_joker.png, 908x1275.</summary>
        public static readonly CardArt JokerCard = new CardArt(0.042f, 0.679f, 0.064f, 0.937f,
            0.725f, 0.931f, 0.099f, 0.938f);

        /// <summary>Art/Cards/card_power.png, 946x1040 - a frame, and no plate.</summary>
        public static readonly CardArt PowerCard = new CardArt(0.053f, 0.935f, 0.056f, 0.943f);

        /// <summary>Which anatomy a card file has. One place, so the market cannot disagree with
        /// the bars about where a card's recess is.</summary>
        public static CardArt ArtFor(string artName)
        {
            return artName == "card_power" ? PowerCard : JokerCard;
        }

        /// <summary>How much of the recess the icon's own 3:4 frame fills. One, because the art
        /// already carries a generous margin - raise it and the taller subjects start climbing
        /// over the gold divider.</summary>
        private const float ArtIconFill = 1f;

        /// <summary>
        /// THE PLATE'S OWN COLOUR, measured off the art, and the CONTRAST anything written on it
        /// has to reach against it (WCAG's ratio, 4.5 - the normal-text bar).
        ///
        /// This is here because the first pass mixed each colour a FIXED amount toward the ink
        /// and shipped: measured afterwards, the pale rarity accents landed at 1.9 to 3.2 against
        /// this plate, which is where a rare joker's name went invisible on the shelf. A blend is
        /// a guess about a colour nobody has looked at yet; a contrast target is an answer for
        /// every colour, including the ones added next year.
        /// </summary>
        private static readonly Color ArtPlate = new Color(0.745f, 0.675f, 0.612f);

        private const float ArtInkContrast = 4.5f;

        /// <summary>
        /// AND THE HUE IS SATURATED ON THE WAY DOWN, which is the other half of it.
        ///
        /// A pale colour taken straight down goes GREY - at the depth this plate needs, common,
        /// rare and legendary all arrive as the same brown and the tier stops reading. Pushing
        /// the saturation as the value falls keeps a rare joker's name a deep BLUE and a
        /// legendary's a deep AMBER at the same darkness. Proportional, with no floor added, so a
        /// colour that was near-neutral to begin with stays near-neutral.
        /// </summary>
        private const float ArtInkSaturate = 1.55f;

        private const int ArtInkSteps = 20;

        /// <summary>Where a colour ends up if even black will not do it - it always does.</summary>
        private static readonly Color ArtInk = new Color(0.17f, 0.12f, 0.09f);

        /// <summary>How hard a live state casts over the painting, and how far a card that cannot
        /// be used is taken down. A cast, never a repaint.</summary>
        private const float ArtStateCast = 0.32f;

        private const float ArtDim = 0.82f;

        /// <summary>The painted card, or null for the flat look. Set once, at Create.</summary>
        private Sprite art;

        private CardArt anatomy = JokerCard;

        private Color resting = Color.white;

        private Color state = Color.white;

        private bool dimmed;

        /// <summary>True while this card is a painting rather than a tinted rectangle.</summary>
        public bool Painted
        {
            get { return art != null; }
        }

        /// <summary>Makes the card under <paramref name="parent"/>. <paramref name="artName"/> is
        /// the painted card to look for in Art/Cards - missing art simply falls back to the flat
        /// build, which is what every bar had before. Size and text placement come later from
        /// <see cref="Layout"/>, because the layout profile can change under it.</summary>
        public static HeldItemCard Create(Transform parent, string name, Color titleColor,
            Color statusColor, string artName)
        {
            var card = new HeldItemCard();
            card.art = string.IsNullOrEmpty(artName) ? null : ViewUtil.CardSprite(artName);
            card.anatomy = ArtFor(artName);
            card.Root = new GameObject(name);
            card.Root.transform.SetParent(parent, false);
            card.Root.AddComponent<RectTransform>();

            card.Body = card.Root.AddComponent<Image>();
            if (card.Painted)
            {
                card.Body.raycastTarget = false;
                card.Body.sprite = card.art;
                // SIMPLE, not Sliced: a painted card has a gold divider and a corner radius, and
                // nine-slicing stretches both into a different card at every size.
                card.Body.type = Image.Type.Simple;
            }
            else
            {
                Sliced(card.Body, ViewUtil.CardSprite("card_base"));
            }

            card.Well = MakeImage(card.Root.transform, "IconWell", ViewUtil.CardSprite("card_base"));
            card.Well.color = WellColor;
            // The painted card has its own recess, so ours would be a second one drawn on it.
            card.Well.enabled = !card.Painted;
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(card.Well.transform, false);
            card.Icon = iconGo.AddComponent<Image>();
            card.Icon.raycastTarget = false;
            // The art is drawn 3:4 on a transparent ground; keeping its aspect is what stops a
            // well of a slightly different shape from squashing it.
            card.Icon.preserveAspect = true;
            // A NEGATIVE inset: the art carries a wide transparent margin round its subject, and
            // letting the image run a little past the recess is what makes the subject fill it.
            // A PAINTED recess is a hole in a painting, so there the icon stays inside it.
            Stretch(card.Icon.rectTransform, card.Painted ? 0f : -12f);
            card.Icon.enabled = false;
            card.Frame = MakeImage(card.Root.transform, "Frame", ViewUtil.CardSprite("card_frame"));
            Stretch(card.Frame.rectTransform, 0f);
            // Two borders fighting reads worse than none: on a painted card the rarity lives on
            // the name instead.
            card.Frame.enabled = !card.Painted;

            // INKED AT BUILD TIME TOO, because not every bar re-colours every line each
            // refresh: the joker bar sets its status colour once, here, and a light blue-grey
            // status on a pale plate is exactly as unreadable as a pale name was.
            Color title = card.Painted ? InkOn(titleColor) : titleColor;
            Color status = card.Painted ? InkOn(statusColor) : statusColor;
            card.Hotkey = MakeText(card.Root.transform, "Hotkey", FontStyle.Bold, title,
                TextAnchor.UpperLeft);
            card.Title = MakeText(card.Root.transform, "Title", FontStyle.Bold, title,
                TextAnchor.MiddleCenter);
            card.Status = MakeText(card.Root.transform, "Status", FontStyle.Normal, status,
                TextAnchor.UpperCenter);
            return card;
        }

        /// <summary>Gives this card a halo in <paramref name="layer"/> - a container the bar keeps
        /// as its FIRST child, so every glow is drawn under every card and a halo can never sit on
        /// top of the card next door. Call once, at build time; SyncGlow then keeps it on the
        /// slot.</summary>
        public void AttachGlow(Transform layer)
        {
            if (layer == null || Glow != null)
            {
                return;
            }
            var go = new GameObject(Root.name + "_Glow");
            go.transform.SetParent(layer, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = ViewUtil.GlowSprite;
            // SLICED: the falloff is the sprite's border and has to keep its width at every card
            // size - stretched as a whole it is a haze on a phone slot and a hard rim on a
            // desktop card.
            image.type = Image.Type.Sliced;
            image.enabled = false;
            Glow = go.AddComponent<CardGlowFx>();
            SyncGlow();
        }

        /// <summary>Puts the halo back on the card's slot. The bar calls this whenever it has
        /// moved its slots, because the two are siblings rather than parent and child.</summary>
        public void SyncGlow()
        {
            if (Glow == null)
            {
                return;
            }
            var mine = Root.GetComponent<RectTransform>();
            var his = Glow.GetComponent<RectTransform>();
            his.anchorMin = mine.anchorMin;
            his.anchorMax = mine.anchorMax;
            his.pivot = mine.pivot;
            Glow.gameObject.SetActive(Root.activeSelf);
            Glow.Follow(mine.anchoredPosition, mine.sizeDelta);
        }

        /// <summary>Shows <paramref name="sprite"/> in the well, or nothing when it is null. The
        /// dim flag is the card's too: the one thing that says "you cannot use this" should not
        /// leave the card around it looking ready.</summary>
        public void SetIcon(Sprite sprite, bool dim)
        {
            Icon.sprite = sprite;
            Icon.enabled = sprite != null;
            Icon.color = dim ? DimIconColor : Color.white;
            dimmed = dim;
            ApplyBody();
        }

        /// <summary>The colour the bar paints a card that has nothing going on. A painted card is
        /// left ALONE in that state - it was painted, and a resting card is the painting.</summary>
        public void SetResting(Color colour)
        {
            resting = colour;
            ApplyBody();
        }

        /// <summary>This refresh's live state colour.</summary>
        public void SetBody(Color colour)
        {
            state = colour;
            ApplyBody();
        }

        /// <summary>Whatever colour a bar wants a piece of text to be, adapted to what the card is
        /// actually made of. On a painted card that is ink on a pale plate; on a flat one it is
        /// the colour the bar asked for, unchanged.</summary>
        public Color Ink(Color wanted)
        {
            return Painted ? InkOn(wanted) : wanted;
        }

        /// <summary>Whatever is written ON the painted plate goes through this - the bar card's
        /// name and status, and the market tile's name and price - so "what colour is that on
        /// this plate" has ONE answer, and it is a measured one.</summary>
        public static Color InkOn(Color wanted)
        {
            float h, s, v;
            Color.RGBToHSV(wanted, out h, out s, out v);
            s = Mathf.Clamp01(s * ArtInkSaturate);
            for (int i = 0; i <= ArtInkSteps; i++)
            {
                Color c = Color.HSVToRGB(h, s, v * (1f - i / (float)ArtInkSteps));
                c.a = wanted.a;
                if (Contrast(c, ArtPlate) >= ArtInkContrast)
                {
                    return c;
                }
            }
            return new Color(ArtInk.r, ArtInk.g, ArtInk.b, wanted.a);
        }

        /// <summary>WCAG's contrast ratio. The sRGB curve is in it on purpose: two colours a
        /// linear average calls equally far apart are not equally readable.</summary>
        private static float Contrast(Color a, Color b)
        {
            float la = Relative(a) + 0.05f;
            float lb = Relative(b) + 0.05f;
            return la > lb ? la / lb : lb / la;
        }

        private static float Relative(Color c)
        {
            return 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
        }

        private static float Channel(float v)
        {
            return v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);
        }

        private void ApplyBody()
        {
            if (!Painted)
            {
                Body.color = state;
                return;
            }
            // A STATE CANNOT REPAINT A PAINTING, it can only cast over it - so the state colour is
            // taken for its HUE at full brightness and laid on lightly. The resting state is left
            // exactly as painted, and a card that cannot be used is taken down as a whole rather
            // than being given a colour that means nothing on its own.
            Color tint = Color.white;
            if (!Same(state, resting))
            {
                float peak = Mathf.Max(state.r, Mathf.Max(state.g, state.b));
                Color hue = peak > 0.001f
                    ? new Color(state.r / peak, state.g / peak, state.b / peak)
                    : Color.white;
                tint = Color.Lerp(Color.white, hue, ArtStateCast);
            }
            float dim = dimmed ? ArtDim : 1f;
            Body.color = new Color(tint.r * dim, tint.g * dim, tint.b * dim, 1f);
        }

        private static bool Same(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.003f && Mathf.Abs(a.g - b.g) < 0.003f
                && Mathf.Abs(a.b - b.b) < 0.003f;
        }

        /// <summary>Places everything inside a card of <paramref name="size"/> canvas pixels. A
        /// <paramref name="compact"/> card (the phone row) has no room for the status line.</summary>
        public void Layout(Vector2 size, bool compact)
        {
            if (Painted)
            {
                LayoutPainted(size, compact);
                return;
            }
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

        /// <summary>
        /// THE SAME WIDGETS, PUT WHERE THE PAINTING SAYS.
        ///
        /// Every rect is a fraction of the card, so one measurement of the art serves the desktop
        /// card, the phone card and anything in between - and if the art is ever redrawn, the
        /// eight numbers at the top of this file are the whole of the change.
        /// </summary>
        private void LayoutPainted(Vector2 size, bool compact)
        {
            // THE RECESS. The icon is a child of it and stretched to it, so fitting the recess is
            // all it takes - preserveAspect then letterboxes the 3:4 art inside it.
            float wellWidth = size.x * (anatomy.IconRight - anatomy.IconLeft) * ArtIconFill;
            float wellHeight = size.y * (anatomy.IconBottom - anatomy.IconTop) * ArtIconFill;
            float wellMiddle = (anatomy.IconLeft + anatomy.IconRight) * 0.5f - 0.5f;
            float wellTop = (anatomy.IconTop + anatomy.IconBottom) * 0.5f * size.y
                - wellHeight * 0.5f;
            Place(Well.rectTransform, new Vector2(size.x * wellMiddle, -wellTop),
                new Vector2(wellWidth, wellHeight));

            // A CARD WITH NO PLATE IS A FRAME ROUND AN ICON, and nothing is written on it. The
            // power card is that card: the state it is in is said by the cast over the painting
            // and by the icon being dimmed, which is what those two were already doing on the
            // joker card underneath its text.
            if (!anatomy.HasPlate)
            {
                Title.gameObject.SetActive(false);
                Status.gameObject.SetActive(false);
                Hotkey.gameObject.SetActive(false);
                return;
            }
            Title.gameObject.SetActive(true);
            Hotkey.gameObject.SetActive(true);

            // THE PLATE. The name has it to itself on a compact card; on a full one the status
            // line shares it, because the painting has no third region and a status floating over
            // the recess would sit on top of the icon.
            float plateWidth = size.x * (anatomy.PlateRight - anatomy.PlateLeft);
            float plateHeight = size.y * (anatomy.PlateBottom - anatomy.PlateTop);
            float plateMiddle = (anatomy.PlateLeft + anatomy.PlateRight) * 0.5f - 0.5f;
            float plateTop = size.y * anatomy.PlateTop;
            float titleHeight = compact ? plateHeight : plateHeight * 0.62f;
            Place(Title.rectTransform, new Vector2(size.x * plateMiddle, -plateTop),
                new Vector2(plateWidth, titleHeight));
            Title.resizeTextMaxSize = Mathf.RoundToInt(size.x * 0.13f);
            Title.resizeTextMinSize = 8;

            Status.gameObject.SetActive(!compact);
            Place(Status.rectTransform, new Vector2(size.x * plateMiddle, -plateTop - titleHeight),
                new Vector2(plateWidth, plateHeight - titleHeight));
            Status.alignment = TextAnchor.MiddleCenter;
            Status.resizeTextMaxSize = Mathf.RoundToInt(size.x * 0.095f);
            Status.resizeTextMinSize = 7;

            // The hotkey goes in the recess's own top-left corner, where the painting is empty.
            Hotkey.fontSize = Mathf.RoundToInt(size.x * 0.11f);
            Place(Hotkey.rectTransform,
                new Vector2(size.x * wellMiddle, -size.y * anatomy.IconTop - 2f),
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
