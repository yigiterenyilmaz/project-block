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

            var sfxGo = new GameObject("SoundFx");
            sfxGo.transform.SetParent(transform, false);
            sfx = sfxGo.AddComponent<SoundFx>();

            var flamesGo = new GameObject("FlameStreak");
            flamesGo.transform.SetParent(transform, false);
            flameStreak = flamesGo.AddComponent<FlameStreakView>();

            var lineBurstGo = new GameObject("LineBurst");
            lineBurstGo.transform.SetParent(transform, false);
            lineBurst = lineBurstGo.AddComponent<LineBurstView>();

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

            tooltipRoot = new GameObject("Tooltip");
            tooltipRoot.transform.SetParent(transform, false);
            tooltipRoot.SetActive(false);

            var canvasGo = new GameObject("HudCanvas");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            infoText = MakeText(canvasGo.transform, "InfoText", new Vector2(0f, 1f),
                new Vector2(16f, -16f), TextAnchor.UpperLeft, 24, Color.white);
            // Score first, at the top centre; the message line sits under it.
            totalText = MakeText(canvasGo.transform, "TotalText", new Vector2(0.5f, 1f),
                new Vector2(0f, -14f), TextAnchor.UpperCenter, 34, new Color(1f, 0.86f, 0.42f));
            messageText = MakeText(canvasGo.transform, "MessageText", new Vector2(0.5f, 1f),
                new Vector2(0f, -66f), TextAnchor.UpperCenter, 28, new Color(1f, 0.92f, 0.45f));

            jokerBar.Build(canvasGo.transform);
            powerBar.Build(canvasGo.transform);

            // Built last so it starts on top of the bars and the HUD text; Show() also
            // re-asserts that, so this ordering is a convenience rather than a dependency.
            var menuGo = new GameObject("MenuScreenView");
            menuGo.transform.SetParent(transform, false);
            menu = menuGo.AddComponent<MenuScreenView>();
            menu.Build(canvasGo.transform);
        }

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
