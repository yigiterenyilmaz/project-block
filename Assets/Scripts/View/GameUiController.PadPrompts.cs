// PURPOSE: the strip along the bottom of the screen that says what the pad can do RIGHT NOW.
//
// It exists because a control scheme nobody can see is a control scheme nobody uses: the
// HOW TO PLAY pages are the reference, this is the reminder. It appears ONLY while a gamepad
// is actually driving (GamepadBridge.Active), so a mouse player never sees it, and it never
// mentions a debug key - the pad is how the game is PLAYED, and the debug keys stay on the
// keyboard where they belong.
//
// THE ONE RULE: it reads the same state the handlers do and names what they will actually do
// with the next press. A prompt that has drifted from its binding is worse than none, so
// every line here is written next to the branch it describes (.PadPlay for direct play, the
// GamepadBridge mapping table for the cursor scheme).
//
// EXTENSION POINT: a new binding is a token in the line for the state it belongs to. Rules
// NEVER live here.

using System.Collections.Generic;
using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        // The face buttons in their usual colours, so a glance finds the one it wants without
        // reading. Everything with no convention of its own - shoulders, triggers, the sticks,
        // Menu - shares one neutral tone.
        private const string PadColorA = "8CD98C";
        private const string PadColorB = "E8776A";
        private const string PadColorX = "79ACE8";
        private const string PadColorY = "EFC85A";
        private const string PadColorNeutral = "C3CAD8";

        /// <summary>The dot between a control and what it does - dim, so it separates without
        /// competing with either half.</summary>
        private const string PadColorSeparator = "6A7382";

        private const float PadPromptHeight = 40f;
        private const float PadPromptPadding = 26f;
        private const float PadPromptInset = 12f;
        private const int PadPromptFontSize = 21;

        /// <summary>In the canvas's own 1920-wide reference space, with a margin each side.</summary>
        private const float PadPromptMaxWidth = 1820f;

        private const int PadPromptMinFontSize = 15;

        private RectTransform padPromptRoot;
        private Image padPromptPlate;
        private Text padPromptText;

        /// <summary>What is on the strip, so it is only rebuilt when it actually changes -
        /// re-laying out a uGUI Text every frame is a canvas rebuild every frame.</summary>
        private string padPromptShown;

        /// <summary>Put away with L3, and stays away until L3 brings it back. A player who has
        /// learned the scheme should be able to have their screen back.</summary>
        private bool padPromptsHidden;

        /// <summary>Builds the strip, hidden. Bottom centre: the conventional home for button
        /// prompts, and the one edge of this screen that carries no readout.</summary>
        private void BuildPadPrompts(Transform parent)
        {
            var go = new GameObject("PadPrompts");
            go.transform.SetParent(parent, false);
            padPromptRoot = go.AddComponent<RectTransform>();
            padPromptRoot.anchorMin = new Vector2(0.5f, 0f);
            padPromptRoot.anchorMax = new Vector2(0.5f, 0f);
            padPromptRoot.pivot = new Vector2(0.5f, 0f);
            padPromptRoot.anchoredPosition = new Vector2(0f, PadPromptInset);
            padPromptRoot.sizeDelta = new Vector2(600f, PadPromptHeight);

            padPromptPlate = go.AddComponent<Image>();
            padPromptPlate.color = new Color(0.05f, 0.06f, 0.09f, 0.86f);
            padPromptPlate.raycastTarget = false;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(padPromptRoot, false);
            padPromptText = labelGo.AddComponent<Text>();
            padPromptText.font = ViewUtil.UiFont;
            padPromptText.fontSize = PadPromptFontSize;
            padPromptText.alignment = TextAnchor.MiddleCenter;
            padPromptText.color = new Color(0.80f, 0.85f, 0.92f);
            padPromptText.raycastTarget = false;
            padPromptText.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            go.SetActive(false);
        }

        /// <summary>True while a pad is the thing actually driving the game.</summary>
        private bool PadDriving
        {
            get { return gamepad != null && gamepad.Active; }
        }

        /// <summary>A prompt for something that has BOTH a key and a pad button: it names
        /// whichever the player is holding right now.
        ///
        /// The game is full of these - "[A] advance to market", "SHIFT+click an offer",
        /// "Drag to place" - and every one of them was a lie to a player on a pad. They are
        /// written once, here, rather than each caller learning about gamepads.</summary>
        private string PadOr(string keyboard, string pad)
        {
            return PadDriving ? pad : keyboard;
        }

        /// <summary>The same, for the two pad SCHEMES: what a control does is not always the
        /// same in both (a block is dragged under the cursor and stepped in direct mode).</summary>
        private string PadOr(string keyboard, string cursorPad, string directPad)
        {
            if (!PadDriving)
            {
                return keyboard;
            }
            return padScheme == PadScheme.Direct ? directPad : cursorPad;
        }

        /// <summary>One frame of the strip.        /// <summary>One frame of the strip. Called straight after the bridge is ticked, so it
        /// describes the state the pad is in NOW rather than the one it was in last frame.</summary>
        private void UpdatePadPrompts()
        {
            if (padPromptRoot == null)
            {
                return;
            }
            // L3 puts the strip away and brings it back. It is the one control that is about
            // the PROMPTS rather than about the game, so nothing else may claim it.
            Gamepad pad = Gamepad.current;
            if (pad != null && PadDriving && pad.leftStickButton.wasPressedThisFrame)
            {
                padPromptsHidden = !padPromptsHidden;
            }
            string line = PadDriving && !padPromptsHidden ? PadPromptLine() : string.Empty;
            if (!string.IsNullOrEmpty(line))
            {
                // Over the menu layer, which re-asserts ITSELF as the last sibling every time a
                // screen is drawn - and a full-screen backdrop would otherwise bury the strip
                // on exactly the screens whose controls need explaining. Only when it has
                // actually slipped: re-ordering the hierarchy dirties the canvas.
                Transform parent = padPromptRoot.parent;
                if (padPromptRoot.GetSiblingIndex() != parent.childCount - 1)
                {
                    padPromptRoot.SetAsLastSibling();
                }
            }
            if (line == padPromptShown)
            {
                return;
            }
            padPromptShown = line;
            bool show = !string.IsNullOrEmpty(line);
            if (padPromptRoot.gameObject.activeSelf != show)
            {
                padPromptRoot.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }
            padPromptText.text = line;
            // The plate is cut to the line rather than the line fitted to the plate: the strip
            // is a different length in every state and in both languages. The shop's line is
            // the longest by far - every verb the market has, plus the two conditional ones -
            // so a line that would run off the screen gives up point size rather than its tail.
            padPromptText.fontSize = PadPromptFontSize;
            float width = padPromptText.preferredWidth;
            if (width > PadPromptMaxWidth)
            {
                padPromptText.fontSize = Mathf.Max(PadPromptMinFontSize,
                    Mathf.FloorToInt(PadPromptFontSize * PadPromptMaxWidth / width));
                width = padPromptText.preferredWidth;
            }
            padPromptRoot.sizeDelta = new Vector2(
                Mathf.Min(width + PadPromptPadding * 2f, PadPromptMaxWidth + PadPromptPadding),
                PadPromptHeight);
        }

        /// <summary>What the pad does in the state the game is in. Empty means no strip.</summary>
        private string PadPromptLine()
        {
            if (screen != AppScreen.Playing)
            {
                return PadPanelDriven() ? PadPanelPromptLine() : PadMenuPromptLine();
            }
            if (session == null || AnimLabOpen || GalleryOpen)
            {
                return string.Empty; // the debug screens are not what a pad is for
            }
            if (PadPanelDriven())
            {
                return PadPanelPromptLine();
            }
            if (PadPanelOpen())
            {
                return PadJoin(PadTok("A", PadColorA, Loc.Pick("pick", "seç")),
                    PadTok("B", PadColorB, Loc.Pick("close", "kapat")));
            }
            if (session.Phase == GamePhase.Market)
            {
                return PadDirectPlayable()
                    ? PadDirectMarketPromptLine()
                    : PadMarketPromptLine();
            }
            if (session.Phase != GamePhase.Round)
            {
                return string.Empty;
            }
            RoundEngine round = session.CurrentRound;
            if (round == null)
            {
                return string.Empty;
            }
            if (round.Status == RoundStatus.AwaitingAdvanceDecision)
            {
                return PadJoin(PadTok("Y", PadColorY, Loc.Pick("take the market", "markete geç")),
                    PadTok("X", PadColorX, Loc.Pick("keep going", "devam et")));
            }
            if (round.Status != RoundStatus.InProgress)
            {
                return string.Empty;
            }
            if (padScheme == PadScheme.Direct && PadDirectPlayable())
            {
                return PadDirectPromptLine(round);
            }
            // The cursor scheme in a round: a block is DRAGGED, so the button is held.
            return PadJoin(PadTok("A", PadColorA, Loc.Pick("hold to drag", "basılı tutup sürükle")),
                PadTok("X", PadColorX, Loc.Pick("rotate", "döndür")),
                PadTok("B", PadColorB, Loc.Pick("pause", "duraklat")));
        }

        /// <summary>A panel the pad is stepping itself (see .PadPanels). What A means is what
        /// the panel was OPENED for, so the line has to say which of them this is.</summary>
        private string PadPanelPromptLine()
        {
            if (deckOverlay.IsOpen)
            {
                string act = hileliPickMode
                    ? Loc.Pick("pick / drop", "seç / bırak")
                    : foxPickSlot >= 0
                        ? Loc.Pick("take this shape", "bu şekli al")
                        : sellCardsMode ? Loc.Pick("sell it", "sat") : null;
                var parts = new List<string>();
                if (act != null)
                {
                    parts.Add(PadTok("A", PadColorA, act));
                }
                if (hileliPickMode)
                {
                    parts.Add(PadTok("Y", PadColorY, Loc.Pick("confirm", "onayla")));
                }
                parts.Add(PadHold("LB/RB", Loc.Pick("page", "sayfa")));
                parts.Add(PadTok("B", PadColorB, Loc.Pick("close", "kapat")));
                return PadJoin(parts.ToArray());
            }
            if (lineSwapPicker.IsOpen)
            {
                return PadJoin(PadTok("A", PadColorA, Loc.Pick("choose", "seç")));
            }
            if (choicePicker.IsOpen)
            {
                return PadJoin(PadTok("A", PadColorA, Loc.Pick("choose", "seç")),
                    PadTok("B", PadColorB, Loc.Pick("cancel", "iptal")));
            }
            return PadJoin(PadTok("A", PadColorA, Loc.Pick("start the run", "oyunu başlat")),
                PadTok("B", PadColorB, Loc.Pick("back", "geri")));
        }

        private string PadMenuPromptLine()
        {
            switch (screen)
            {
                case AppScreen.DeckSelect:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("choose", "seç")),
                        PadTok("B", PadColorB, Loc.Pick("back", "geri")));
                case AppScreen.HowToPlay:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("back", "geri")),
                        PadTok("B", PadColorB, Loc.Pick("back", "geri")));
                case AppScreen.Settings:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("change", "değiştir")),
                        PadTok("B", PadColorB, Loc.Pick("back", "geri")));
                case AppScreen.Paused:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("select", "seç")),
                        PadTok("B", PadColorB, Loc.Pick("resume", "devam et")));
                default:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("select", "seç")));
            }
        }

        private string PadMarketPromptLine()
        {
            var parts = new List<string>
            {
                PadTok("A", PadColorA, Loc.Pick("buy / sell", "al / sat")),
                PadTok("Y", PadColorY, Loc.Pick("next round", "sonraki raunt")),
                PadTok("LB/RB", PadColorNeutral, Loc.Pick("scroll", "kaydır"))
            };
            if (session.CanSmuggle)
            {
                // "Kaçakçı" is invisible unless something says so, exactly as the market HUD
                // says it for the mouse.
                parts.Add(PadTok("LT+A", PadColorNeutral, Loc.Pick("take it free", "bedava al")));
            }
            parts.Add(PadTok("B", PadColorB, Loc.Pick("pause", "duraklat")));
            return PadJoin(parts.ToArray());
        }

        /// <summary>The shop, STEPPED rather than pointed at. Every verb the market has gets a
        /// button here, which is the whole difference from the cursor line above.</summary>
        private string PadDirectMarketPromptLine()
        {
            if (padFocus == PadFocus.Jokers || padFocus == PadFocus.Powers)
            {
                return PadJoin(PadTok("A", PadColorA, Loc.Pick("sell it", "sat")));
            }
            var parts = new List<string>
            {
                PadTok("A", PadColorA, Loc.Pick("buy", "al")),
                PadTok("X", PadColorX, Loc.Pick("reroll", "yenile")),
                PadTok("Y", PadColorY, Loc.Pick("next round", "sonraki raunt")),
                PadHold("RB", Loc.Pick("sell a joker", "joker sat")),
                PadHold("LB", Loc.Pick("sell a power", "güç sat")),
                PadMenu(Loc.Pick("sell cards", "kart sat"))
            };
            if (session.CanSmuggle)
            {
                parts.Add(PadTok("LT+A", PadColorNeutral,
                    Loc.Pick("take it free", "bedava al")));
            }
            if (session.Debt > 0)
            {
                parts.Add(PadTok("R3", PadColorNeutral, Loc.Pick("pay the debt", "borcu öde")));
            }
            return PadJoin(parts.ToArray());
        }

        /// <summary>Direct play, which is the scheme with real states to describe. Each line is
        /// written beside the branch of HandlePadRound that answers those buttons.</summary>
        private string PadDirectPromptLine(RoundEngine round)
        {
            if (pendingTargetJokerId.HasValue || pendingTargetPowerId.HasValue)
            {
                // "Hidrolik pres" confirms twice, and A means a different thing each time - see
                // HandlePressClick, which is the branch answering this button.
                string confirm = workshopPressAnchor.HasValue
                    ? Loc.Pick("where the cube goes", "küp nereye gitsin")
                    : Loc.Pick("confirm", "onayla");
                return PadJoin(PadTok("A", PadColorA, confirm),
                    PadTok("B", PadColorB, Loc.Pick("cancel", "iptal")));
            }
            switch (padFocus)
            {
                case PadFocus.Jokers:
                case PadFocus.Powers:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("use it", "kullan")));
                case PadFocus.Board:
                    return PadJoin(PadTok("A", PadColorA, Loc.Pick("place", "yerleştir")),
                        PadTok("B", PadColorB, Loc.Pick("put it back", "geri koy")),
                        PadTok("X", PadColorX, Loc.Pick("rotate", "döndür")));
            }
            var parts = new List<string>
            {
                PadTok("A", PadColorA, Loc.Pick("take it", "al")),
                PadTok("X", PadColorX, Loc.Pick("rotate", "döndür")),
                PadHold("RB", Loc.Pick("jokers", "jokerler")),
                PadHold("LB", Loc.Pick("powers", "güçler")),
                PadMenu(Loc.Pick("your cards", "kartların")),
                PadTok("B", PadColorB, Loc.Pick("pause", "duraklat"))
            };
            if (round.Boss is TamagotchiBoss)
            {
                parts.Add(PadTok("R3", PadColorNeutral, Loc.Pick("feed the pet", "yaratığı besle")));
            }
            return PadJoin(parts.ToArray());
        }

        /// <summary>The panels that cover the board and want clicking. Split out of
        /// PadModalOpen so the prompt strip is not thrown by a MOUSE drag, which is not a
        /// panel and leaves the pad's own bindings exactly where they were.</summary>
        private bool PadPanelOpen()
        {
            return deckSelect.IsOpen || deckOverlay.IsOpen || batakBet.IsOpen
                || choicePicker.IsOpen || blockDesigner.IsOpen || grantPicker.IsOpen
                || lineSwapPicker.IsOpen || cubePicker.IsOpen || weldPicker.IsOpen
                || parazitStep != ParazitStep.None;
        }

        /// <summary>One token: the CONTROL in its own colour, a dim dot, then what it does.
        ///
        /// The bare coloured button is what reads best - a letter in the pad's own green or
        /// red is recognised without being read, and bracketing it took that away. But a
        /// control whose name is an ordinary word ("stick", "Back") then runs straight into
        /// its label and the whole token reads as a sentence: "stick pick a block". The dot is
        /// what separates them without boxing anything in.</summary>
        private static string PadTok(string button, string colorHex, string label)
        {
            return "<color=#" + colorHex + ">" + button + "</color>"
                + "<color=#" + PadColorSeparator + "> · </color>" + label;
        }

        /// <summary>The MENU button - the three lines. Drawn as the glyph the button carries
        /// when the font has one, and named in words when it does not: a missing glyph renders
        /// as an empty box, which is worse than the word it replaced.</summary>
        private static string PadMenu(string label)
        {
            return PadTok(PadMenuGlyph, PadColorNeutral, label);
        }

        /// <summary>Resolved once, against the font actually in use.</summary>
        private static string PadMenuGlyph
        {
            get
            {
                if (padMenuGlyph == null)
                {
                    Font font = ViewUtil.UiFont;
                    padMenuGlyph = font != null && font.HasCharacter('≡') ? "≡" : "Menu";
                }
                return padMenuGlyph;
            }
        }

        private static string padMenuGlyph;

        /// <summary>A control that has to be HELD, said so in the token rather than left for
        /// the player to discover by tapping it and getting nothing.</summary>
        private static string PadHold(string button, string label)
        {
            return PadTok(button + Loc.Pick(" (hold)", " (basılı)"), PadColorNeutral, label);
        }

        private static string PadJoin(params string[] parts)
        {
            return string.Join("     ", parts);
        }
    }
}
