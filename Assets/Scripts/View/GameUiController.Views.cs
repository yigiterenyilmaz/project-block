// PURPOSE: GameUiController view construction - building the runtime view objects and
// the shared text-label helper.

using System.Collections;
using System.Collections.Generic;
using System.Text;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        /// <summary>
        /// Every card face the view has ever seen this session, by id.
        ///
        /// The owned deck is NOT enough to answer "what did this cube come from". A bonus card
        /// never joins it (a debug-dealt block, a "Kara delik" void block, a cloned card), and
        /// a card that HAS been played has already left the hand while its cubes stand there
        /// for the rest of the round. So faces are remembered as they go past instead of
        /// looked up after the fact.
        /// </summary>
        private readonly Dictionary<int, BlockCard> cardFaces = new Dictionary<int, BlockCard>();

        /// <summary>Files away every card currently visible - hand, bonus hand and owned deck -
        /// so a cube placed from any of them can still find its face later. Called on every
        /// refresh, which is the last moment a card is guaranteed to still be somewhere.</summary>
        private void RememberCardFaces(RoundEngine round)
        {
            if (session != null)
            {
                IReadOnlyList<BlockCard> owned = session.OwnedCards;
                for (int i = 0; i < owned.Count; i++)
                {
                    cardFaces[owned[i].Id] = owned[i];
                }
            }
            if (round != null)
            {
                for (int i = 0; i < round.Hand.Count; i++)
                {
                    cardFaces[round.Hand[i].Id] = round.Hand[i];
                }
                // The BONUS hand is a separate list, not part of Hand - and it is where every
                // card that never joins the deck lives: a debug-dealt block, a "Kara delik"
                // void block, a clone. Miss it and exactly those blocks lose their face the
                // moment they are placed.
                IReadOnlyList<BonusSlot> bonus = round.BonusHand;
                for (int i = 0; i < bonus.Count; i++)
                {
                    cardFaces[bonus[i].Card.Id] = bonus[i].Card;
                }
            }
        }

        /// <summary>The card with this id, or null. Cubes with no card behind them (rot,
        /// snakes, mines) answer null, which every caller treats as "no card face".</summary>
        private BlockCard FindOwnedCard(int cardId)
        {
            BlockCard card;
            return cardFaces.TryGetValue(cardId, out card) ? card : null;
        }

        private void BuildViews()
        {
            // First, so every other view is built on top of it: the backdrop is the surface the
            // game sits on, not an effect laid over it. It parents itself to the camera.
            var backdropGo = new GameObject("Backdrop");
            backdropGo.transform.SetParent(transform, false);
            backdrop = backdropGo.AddComponent<BackdropView>();
            backdrop.Build(cam);

            var boardGo = new GameObject("BoardView");
            boardGo.transform.SetParent(transform, false);
            boardView = boardGo.AddComponent<BoardView>();
            // Lets a placed cube keep the face of the card that placed it (see CardLookup).
            boardView.CardLookup = FindOwnedCard;

            var galleryGo = new GameObject("BlockGallery");
            galleryGo.transform.SetParent(transform, false);
            blockGallery = galleryGo.AddComponent<BlockGalleryView>();

            var cardsGo = new GameObject("CardLayer");
            cardsGo.transform.SetParent(transform, false);
            cardLayer = cardsGo.AddComponent<CardLayerView>();

            var overlayGo = new GameObject("DeckOverlay");
            overlayGo.transform.SetParent(transform, false);
            deckOverlay = overlayGo.AddComponent<DeckOverlayView>();

            var deckSelectGo = new GameObject("DeckSelect");
            deckSelectGo.transform.SetParent(transform, false);
            deckSelect = deckSelectGo.AddComponent<DeckSelectView>();

            var marketGo = new GameObject("MarketView");
            marketGo.transform.SetParent(transform, false);
            marketView = marketGo.AddComponent<MarketView>();
            // Holding a section refreshes it. The view reports the gesture and nothing else -
            // it never touches money or the market, exactly as clicking an offer does not.
            marketView.SectionHeld = delegate (MarketOfferKind kind) { RerollSection(kind); };
            // DEMO shelf: the PROCEED button is the mouse twin of [N]. Same three steps, so a
            // player who never finds the key is not stuck in the shop.
            marketView.ProceedPressed = delegate { LeaveMarketNow(); };
            // The shelf names the control that starts the next round; which one that IS depends
            // on what the player is holding, so it asks rather than being told.
            marketView.ProceedHint = delegate { return PadOr("[N]", "Y"); };

            var sfxGo = new GameObject("SoundFx");
            sfxGo.transform.SetParent(transform, false);
            sfx = sfxGo.AddComponent<SoundFx>();

            var flamesGo = new GameObject("FlameStreak");
            flamesGo.transform.SetParent(transform, false);
            flameStreak = flamesGo.AddComponent<FlameStreakView>();

            var vignetteGo = new GameObject("OvertimeVignette");
            vignetteGo.transform.SetParent(transform, false);
            overtimeVignette = vignetteGo.AddComponent<OvertimeVignetteView>();
            overtimeVignette.Build(cam, BoardCenter);

            var pressureGo = new GameObject("OvertimePressure");
            pressureGo.transform.SetParent(transform, false);
            overtimePressure = pressureGo.AddComponent<OvertimePressureView>();
            overtimePressure.Build(boardView, overtimeVignette);

            var lineBurstGo = new GameObject("LineBurst");
            lineBurstGo.transform.SetParent(transform, false);
            lineBurst = lineBurstGo.AddComponent<LineBurstView>();

            var lineSweepGo = new GameObject("LineSweep");
            lineSweepGo.transform.SetParent(transform, false);
            lineSweep = lineSweepGo.AddComponent<LineSweepView>();

            var sweepSparkGo = new GameObject("SweepSparks");
            sweepSparkGo.transform.SetParent(transform, false);
            sweepSparks = sweepSparkGo.AddComponent<SweepSparkView>();

            var cleanseGo = new GameObject("BoardCleanse");
            cleanseGo.transform.SetParent(transform, false);
            boardCleanse = cleanseGo.AddComponent<BoardCleanseView>();

            var tntGo = new GameObject("DynamiteBlast");
            tntGo.transform.SetParent(transform, false);
            dynamiteBlast = tntGo.AddComponent<DynamiteBlastView>();

            var blastGo = new GameObject("BlastFx");
            blastGo.transform.SetParent(transform, false);
            blastFx = blastGo.AddComponent<BlastFxView>();

            var lineSwapGo = new GameObject("LineSwapPicker");
            lineSwapGo.transform.SetParent(transform, false);
            lineSwapPicker = lineSwapGo.AddComponent<LineSwapPickerView>();

            var jokerGo = new GameObject("JokerBarView");
            jokerGo.transform.SetParent(transform, false);
            jokerBar = jokerGo.AddComponent<JokerBarView>();

            var powerGo = new GameObject("PowerBarView");
            powerGo.transform.SetParent(transform, false);
            powerBar = powerGo.AddComponent<PowerBarView>();

            var pickerGo = new GameObject("GrantPicker");
            pickerGo.transform.SetParent(transform, false);
            grantPicker = pickerGo.AddComponent<GrantPickerView>();

            var choiceGo = new GameObject("ChoicePicker");
            choiceGo.transform.SetParent(transform, false);
            choicePicker = choiceGo.AddComponent<ChoicePickerView>();

            var batakGo = new GameObject("BatakBet");
            batakGo.transform.SetParent(transform, false);
            batakBet = batakGo.AddComponent<BatakBetView>();

            var designerGo = new GameObject("BlockDesigner");
            designerGo.transform.SetParent(transform, false);
            blockDesigner = designerGo.AddComponent<BlockDesignerView>();

            var crtGo = new GameObject("CrtOverlay");
            crt = crtGo.AddComponent<CrtOverlayView>();
            crt.Build(cam); // parents its own overlay to the camera

            var cubeGo = new GameObject("CubePicker");
            cubeGo.transform.SetParent(transform, false);
            cubePicker = cubeGo.AddComponent<CubePickerView>();

            var mineGo = new GameObject("MineShuffle");
            mineGo.transform.SetParent(transform, false);
            mineShuffle = mineGo.AddComponent<MineShuffleView>();

            var weldGo = new GameObject("WeldPicker");
            weldGo.transform.SetParent(transform, false);
            weldPicker = weldGo.AddComponent<WeldPickerView>();

            var animLabGo = new GameObject("AnimationLab");
            animLabGo.transform.SetParent(transform, false);
            animLab = animLabGo.AddComponent<AnimationLabView>();

            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Stated rather than left at the default, because the tooltip canvas below is
            // defined RELATIVE to it: two overlay canvases sort by this number alone.
            canvas.sortingOrder = HudCanvasOrder;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            hudCanvas = canvas;

            // AFTER the HUD canvas, and on one of its own - see BuildTooltipCanvas.
            BuildTooltipCanvas();

            // EVERY piece of HUD hangs off this, not off the canvas, so a camera shake can take
            // the interface with it. The canvas is ScreenSpaceOverlay: it does not follow the
            // camera, so shaking the camera alone moved the WORLD under a interface that stayed
            // nailed down - which reads as the board rattling inside a still screen rather than
            // as the screen being hit. Stretched to fill, offset only while a shake is running.
            var shakeGo = new GameObject("HudShake", typeof(RectTransform));
            hudShake = (RectTransform)shakeGo.transform;
            hudShake.SetParent(canvasGo.transform, false);
            hudShake.anchorMin = Vector2.zero;
            hudShake.anchorMax = Vector2.one;
            hudShake.offsetMin = Vector2.zero;
            hudShake.offsetMax = Vector2.zero;

            infoText = MakeText(hudShake, "InfoText", new Vector2(0f, 1f),
                new Vector2(16f, -16f), TextAnchor.UpperLeft, InfoFontSize, Color.white);
            // A LEFT COLUMN, not a banner. At the shared 860 the debug lines ran most of the way
            // across the screen and broke wherever they happened to run out, which is how a line
            // ends up reading "... F3:" / "animations" over the board. Narrowing the rect is the
            // whole wrap: Unity Text wraps to its own width, so this is what decides the shape.
            // The font came down with it - the column has to stay clear of the POWER BAR, which
            // is anchored 256px below the top on this same side.
            infoText.rectTransform.sizeDelta = new Vector2(InfoWidth, 460f);
            // Score first, at the top centre; the message line sits under it.
            totalText = MakeText(hudShake, "TotalText", new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), TextAnchor.UpperCenter, 34, new Color(1f, 0.86f, 0.42f));
            messageText = MakeText(hudShake, "MessageText", new Vector2(0.5f, 1f),
                new Vector2(0f, -66f), TextAnchor.UpperCenter, 28, new Color(1f, 0.92f, 0.45f));

            jokerBar.Build(hudShake);
            // Before the bars are laid out: the badge owns the top of that same corner column.
            BuildBossBadge(hudShake);
            powerBar.Build(hudShake);

            // Built last so it starts on top of the bars and the HUD text; Show() also
            // re-asserts that, so this ordering is a convenience rather than a dependency.
            var menuGo = new GameObject("MenuScreenView");
            menuGo.transform.SetParent(transform, false);
            menu = menuGo.AddComponent<MenuScreenView>();
            menu.Build(hudShake);

            // Last, and on the CANVAS rather than on HudShake: the drawn pointer must not be
            // moved by a screen shake, and it must draw over the menus built above it.
            gamepad = new GamepadBridge(hudCanvas);

            // What the pad can do right now, along the bottom. On HudShake, not the canvas:
            // it is HUD and belongs to the screen, unlike the pointer above.
            BuildPadPrompts(hudShake);

            // One line that appears ONLY if a synthesized click is failing to register - the
            // one thing about gamepad input that cannot be diagnosed from inside the game.
            // Silent when it works, which is the point (see GamepadBridge.RecordDebug).
            padDebugText = MakeText(hudShake, "PadDebug", new Vector2(0f, 0f),
                new Vector2(16f, 16f), TextAnchor.LowerLeft, 18,
                new Color(1f, 0.55f, 0.45f));
            padDebugText.rectTransform.sizeDelta = new Vector2(900f, 40f);
            padDebugText.text = string.Empty;
        }

        /// <summary>The two overlay canvases, in order. The gap is there so a third layer can be
        /// slid between them later without renumbering either.</summary>
        private const int HudCanvasOrder = 0;

        private const int TooltipCanvasOrder = 100;

        /// <summary>
        /// The tooltip's own SCREEN-SPACE OVERLAY canvas, sorted above the HUD's.
        ///
        /// WHY IT IS NOT A WORLD-SPACE OBJECT ANY MORE, which is the whole point of this method:
        /// an overlay canvas is composited after the camera has finished, so it covers EVERY
        /// world-space renderer no matter what sorting order that renderer claims. The tooltip
        /// used to be world-space sprites at order 50, and the things it most often has to be
        /// read over - the joker bar, the power bar, the score and the debug column - are all UI
        /// on the HUD canvas. So the panel describing a power was drawn underneath the power bar
        /// it was describing, and no sorting order could ever have fixed it: the fix is to be on
        /// a canvas too, and to be the higher one.
        ///
        /// It also has to be its OWN canvas rather than a child of the HUD's, because the HUD's
        /// content is ordered by sibling index and a tooltip must outrank all of it without
        /// having to be re-parented to the end of the list every time something else is added.
        /// </summary>
        private void BuildTooltipCanvas()
        {
            var canvasGo = new GameObject("TooltipCanvas");
            canvasGo.transform.SetParent(transform, false);
            tooltipCanvas = canvasGo.AddComponent<Canvas>();
            tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            tooltipCanvas.sortingOrder = TooltipCanvasOrder;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // The panel itself: pivot at its TOP-LEFT corner, because that is the corner the
            // cursor anchors, and anchored to the canvas's bottom-left so anchoredPosition is
            // simply "where on the screen", in reference-resolution units.
            var panel = new GameObject("Tooltip");
            panel.transform.SetParent(canvasGo.transform, false);
            tooltipRoot = panel.AddComponent<RectTransform>();
            tooltipRoot.anchorMin = Vector2.zero;
            tooltipRoot.anchorMax = Vector2.zero;
            tooltipRoot.pivot = new Vector2(0f, 1f);

            // The edge is a slightly bigger rounded plate BEHIND the fill (first child = drawn
            // first), so the two corners share a radius and the hairline stays even all the way
            // round - an outline drawn as four rects cannot turn a corner.
            MakeTooltipPlate(tooltipRoot, "TipEdge", TooltipEdgeColor, TooltipEdge);
            MakeTooltipPlate(tooltipRoot, "TipBg", TooltipBgColor, 0f);

            tooltipTitle = MakeTooltipLabel(tooltipRoot, "TipTitle", TooltipTitleFontSize,
                FontStyle.Bold, TooltipTitleColor);
            tooltipBody = MakeTooltipLabel(tooltipRoot, "TipBody", TooltipBodyFontSize,
                FontStyle.Normal, TooltipBodyColor);

            panel.SetActive(false);
        }

        /// <summary>One rounded plate of the tooltip, stretched to fill the panel and grown by
        /// <paramref name="bleed"/> pixels on every side. SLICED, so the corner keeps the radius
        /// it was generated with however wide or tall the description makes the panel - the same
        /// sprite and the same reason as the cards (ViewUtil.RoundedSprite).</summary>
        private static Image MakeTooltipPlate(RectTransform parent, string name, Color color,
            float bleed)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = ViewUtil.RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-bleed, -bleed);
            rect.offsetMax = new Vector2(bleed, bleed);
            return image;
        }

        /// <summary>One line-block of tooltip text, hung from the panel's top-left. The body
        /// arrives already wrapped by ViewUtil.WrapText, so both of these overflow rather than
        /// wrap again - a second wrap at a different width is how a description ends up with one
        /// orphaned word per paragraph.</summary>
        private static Text MakeTooltipLabel(RectTransform parent, string name, int fontSize,
            FontStyle style, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = ViewUtil.UiFontFor(style);
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return text;
        }

        /// <summary>How wide the top-left run/debug readout is allowed to be, in the canvas
        /// 1920x1080 reference space - under a third of the width, so it never crosses the
        /// board or the market panel.</summary>
        private const float InfoWidth = 620f;

        private const int InfoFontSize = 20;

        private static Text MakeText(Transform parent, string name, Vector2 anchor,
            Vector2 offset, TextAnchor alignment, int fontSize, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text text = go.AddComponent<Text>();
            text.font = ViewUtil.UiFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(860f, 420f);
            return text;
        }
    }
}
