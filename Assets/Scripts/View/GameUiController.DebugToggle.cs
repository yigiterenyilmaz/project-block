// PURPOSE: A small button (and F8) that hides the DEBUG READOUT in the top-left corner - the run
// line, the rules dump and the key list. It is useful while developing and in the way when judging
// how a screen looks, which is exactly what the boss stage presentation needs.
//
// The readout's GameObject is switched on and off by the menu layer (SetRunPresentationVisible),
// so this never touches that: it only toggles the Text component, and pushes the readout down under
// the button so the two never overlap. Remembered across sessions (PlayerPrefs).

using ProjectBlock.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    partial class GameUiController
    {
        private const string DebugTextPref = "block_bonk.debug_text_shown";
        private const float DebugButtonWidth = 150f;
        private const float DebugButtonHeight = 30f;

        private RectTransform debugButton;
        private Text debugButtonLabel;
        private bool debugTextShown = true;

        private void BuildDebugButton()
        {
            debugTextShown = PlayerPrefs.GetInt(DebugTextPref, 1) == 1;
            var go = new GameObject("DebugTextToggle", typeof(RectTransform));
            debugButton = (RectTransform)go.transform;
            debugButton.SetParent(hudShake, false);
            debugButton.anchorMin = new Vector2(0f, 1f);
            debugButton.anchorMax = new Vector2(0f, 1f);
            debugButton.pivot = new Vector2(0f, 1f);
            debugButton.sizeDelta = new Vector2(DebugButtonWidth, DebugButtonHeight);
            var plate = go.AddComponent<Image>();
            plate.sprite = ViewUtil.RoundedSprite;
            plate.type = Image.Type.Sliced;
            plate.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);
            plate.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(debugButton, false);
            debugButtonLabel = labelGo.AddComponent<Text>();
            debugButtonLabel.font = ViewUtil.UiFont;
            debugButtonLabel.fontSize = 16;
            debugButtonLabel.alignment = TextAnchor.MiddleCenter;
            debugButtonLabel.color = new Color(0.78f, 0.82f, 0.90f);
            debugButtonLabel.raycastTarget = false;
            var lr = (RectTransform)labelGo.transform;
            lr.anchorMin = Vector2.zero;
            lr.anchorMax = Vector2.one;
            lr.offsetMin = Vector2.zero;
            lr.offsetMax = Vector2.zero;
        }

        /// <summary>Called from the top of Update. Keeps the button and the readout in step, and
        /// answers true on the frame the button eats a click.</summary>
        private bool TickDebugToggle(Keyboard kb, Mouse mouse)
        {
            if (hudShake == null || infoText == null)
            {
                return false;
            }
            if (debugButton == null)
            {
                BuildDebugButton();
            }
            bool playing = screen == AppScreen.Playing && session != null;
            debugButton.gameObject.SetActive(playing && infoText.gameObject.activeInHierarchy);
            if (!playing)
            {
                return false;
            }

            bool toggled = kb != null && kb.f8Key.wasPressedThisFrame;
            if (!toggled && mouse != null && mouse.leftButton.wasPressedThisFrame
                && debugButton.gameObject.activeInHierarchy
                && RectTransformUtility.RectangleContainsScreenPoint(debugButton,
                    mouse.position.ReadValue(), null))
            {
                toggled = true;
            }
            if (toggled)
            {
                debugTextShown = !debugTextShown;
                PlayerPrefs.SetInt(DebugTextPref, debugTextShown ? 1 : 0);
            }

            float inset = UiLayout.Active.CornerInset;
            debugButton.anchoredPosition = new Vector2(inset, -inset);
            debugButtonLabel.text = debugTextShown
                ? Loc.Pick("hide debug  [F8]", "debug gizle  [F8]")
                : Loc.Pick("show debug  [F8]", "debug göster  [F8]");
            infoText.enabled = debugTextShown;
            infoText.rectTransform.anchoredPosition =
                new Vector2(inset, -(inset + DebugButtonHeight + 6f));
            return toggled && mouse != null && mouse.leftButton.wasPressedThisFrame;
        }
    }
}
