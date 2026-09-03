// PURPOSE: Gamepad support for the WHOLE game, in ONE file.
//
// THE CENTRAL TRICK, and the reason this is not spread across a dozen handlers: the game
// reads exactly two things, `Mouse.current` and `Keyboard.current`, and it reads them in one
// place (GameUiController.Update). So instead of threading a second input path through the
// menu layer, the eight in-game modals, the market, the bars, the drag and the retro
// controller, this bridge OWNS A VIRTUAL MOUSE AND A VIRTUAL KEYBOARD - real InputSystem
// devices it adds itself - and writes the pad's sticks and buttons into them. Every existing
// handler goes on reading Mouse.current / Keyboard.current and cannot tell the difference: a
// click is a click whether the cursor was pushed there by a hand or by a thumbstick.
//
// Four things follow from that, and they are the whole design:
//
//  1. ONE DEVICE IS CURRENT AT A TIME, so the bridge only writes while THE PAD IS THE ACTIVE
//     DEVICE. Touching the real mouse or the real keyboard hands control straight back that
//     same frame (and the OS cursor comes back with it). Nudging the stick takes it again.
//  2. A SYNTHESIZED PRESS MUST SURVIVE `wasPressedThisFrame`. InputState.Change made during
//     Update lands in the current update step, so the game sees the edge the same frame; the
//     pulse machinery below still holds every synthetic press down for a couple of frames and
//     forces a gap after it, so a one-frame tap or a frame-rate spike can never swallow one.
//  3. IT IS TICKED FROM GameUiController.Update, before that method reads Mouse.current -
//     explicitly, rather than leaving it to script execution order. (The one thing outside
//     that ordering is the F2 rarity grader's own Update, which is a debug tool with no
//     gamepad route into it anyway.)
//  4. THE CONTEXT COMES FROM THE GAME, never from a boss/phase check in here. The controller
//     passes what the frame IS (a list menu or a pointer screen; a market, a round, a round
//     waiting on the advance decision) and this file only maps buttons onto it.
//
// THE MAPPING (deliberately plain - the designer will re-cut it later):
//
//   left stick     pointer screens: move the cursor      list menus: move the selection
//   d-pad          arrow keys (menus, settings, how-to-play pages, the retro falling piece)
//   A / cross      left click, and SUBMIT on a list menu
//   X / square     right click (rotate a block, reshape a fox, skip Halüsinasyon)
//   B / circle     Escape - cancel targeting, close a modal, open the pause menu
//   Start          Escape
//   Y / triangle   the phase's one press: market -> [N] next round, advance decision ->
//                  [A] take the market. A round in progress has no such verb.
//   LT (held)      Shift - the "Kaçakçı" smuggle modifier in the market
//   R3             in the market, pay down the "Kredi kartı" debt
//   RT (held)      cursor speed boost
//   LB / RB        mouse wheel up / down (the deck overlay, the market shelf, how-to pages)
//   right stick Y  the same wheel
//
// Presentation and input routing only - rules NEVER live here (see the View folder note).
// EXTENSION POINT: a new binding is a line in BuildFrame; a new context is a member of
// ActionContext plus its case there. Nothing else in the game has to learn about pads.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ProjectBlock.View
{
    /// <summary>Drives a virtual mouse + keyboard from the gamepad, and paints the cursor.</summary>
    public sealed class GamepadBridge
    {
        // ---- what the frame is, told to us by the controller ---------------------------

        /// <summary>Whether the stick is a POINTER or a SELECTION this frame. A list menu
        /// parks the cursor off-screen instead of freezing it in place: MenuScreenView takes
        /// its selection from hover, so a cursor left sitting on an entry would fight the
        /// stick for it every frame.</summary>
        public enum PointerMode
        {
            /// <summary>The board, the market, the modals, the deck pick - the cursor moves.</summary>
            Pointer,

            /// <summary>Title / pause / settings / how-to-play / run summary - the stick is
            /// up and down the entry list.</summary>
            MenuList,

            /// <summary>DIRECT play (`GameUiController.PadPlay.cs`): the controller is steering
            /// the pointer itself, snapping it onto whatever the pad has SELECTED - a hand
            /// card, a board cell, a joker panel - so every hover visual and tooltip in the
            /// game keeps working off it. No cursor is drawn and NO buttons are synthesized:
            /// direct mode calls the game's own methods instead of faking clicks.</summary>
            Snapped
        }

        /// <summary>Which single verb the north face button stands for right now.</summary>
        public enum ActionContext
        {
            None,
            Round,
            RoundDecision,
            Market
        }

        // ---- tuning (all placeholders; the feel is meant to be re-cut) ------------------

        private const float StickDeadzone = 0.24f;

        /// <summary>Pixels a second at full tilt, on a 1080-tall screen - scaled to the real
        /// one, so the cursor crosses the board in the same TIME at any resolution.</summary>
        private const float CursorSpeed = 1450f;

        private const float CursorBoost = 2.1f;

        /// <summary>Held direction: the wait before it starts repeating, then the gap between
        /// repeats. Long enough that one press is one step.</summary>
        private const float RepeatDelay = 0.36f;

        private const float RepeatInterval = 0.11f;

        /// <summary>Frames a synthesized press is forced to stay down, and the frames of
        /// release forced after it. See rule 2 in the header - this is what makes an edge
        /// impossible to miss.</summary>
        private const int PulseFrames = 2;

        private const int GapFrames = 1;

        private const float TriggerPoint = 0.4f;

        /// <summary>What one wheel notch reports on Windows. Every wheel handler in the game
        /// reads the SIGN, so the number only has to be unmistakably non-zero.</summary>
        private const float ScrollNotch = 120f;

        /// <summary>Where the cursor is parked while a list menu owns the frame - far enough
        /// off-screen that no hover test can hit anything.</summary>
        private static readonly Vector2 ParkedPosition = new Vector2(-4096f, -4096f);

        // ---- devices --------------------------------------------------------------------

        private Mouse virtualMouse;
        private Keyboard virtualKeyboard;
        private Mouse realMouse;
        private Keyboard realKeyboard;

        /// <summary>True while the PAD owns the pointer. Everything this class writes is
        /// gated on it (see rule 1).</summary>
        private bool padActive;

        private Vector2 cursorPosition;

        // ---- synthesized controls --------------------------------------------------------

        private readonly SynthButton leftButton = new SynthButton();
        private readonly SynthButton rightButton = new SynthButton();
        private readonly Dictionary<Key, SynthButton> keys = new Dictionary<Key, SynthButton>();
        private readonly HashSet<Key> tapped = new HashSet<Key>();
        private readonly HashSet<Key> held = new HashSet<Key>();
        private readonly HashSet<Key> keysDown = new HashSet<Key>();
        private readonly HashSet<Key> lastKeysDown = new HashSet<Key>();

        private readonly Repeater repeatUp = new Repeater();
        private readonly Repeater repeatDown = new Repeater();
        private readonly Repeater repeatLeft = new Repeater();
        private readonly Repeater repeatRight = new Repeater();
        private readonly Repeater repeatScrollUp = new Repeater();
        private readonly Repeater repeatScrollDown = new Repeater();

        // ---- the painted cursor -----------------------------------------------------------

        private readonly Canvas canvas;
        private RectTransform cursorRect;
        private Image cursorFill;
        private static Sprite arrowSprite;

        private static readonly Color CursorIdle = Color.white;
        private static readonly Color CursorPressed = new Color(1f, 0.86f, 0.42f);

        /// <summary>How many screen pixels one texel of the arrow is drawn at.</summary>
        private const float CursorScale = 2.6f;

        public GamepadBridge(Canvas hudCanvas)
        {
            canvas = hudCanvas;
            RegisterKeys();
            BuildCursor();
        }

        /// <summary>True while the pad is driving - the HUD may say so.</summary>
        public bool Active
        {
            get { return padActive; }
        }

        /// <summary>Where DIRECT mode wants the pointer this frame, in screen pixels. Only read
        /// in <see cref="PointerMode.Snapped"/>; set it before Tick.</summary>
        public Vector2 SnapPosition { get; set; }

        /// <summary>What the bridge is doing and, crucially, WHAT THE GAME SEES - the device
        /// that is current and whether the click it was handed actually registered. Shown in
        /// the debug readout, because a synthesized press that quietly fails to become an edge
        /// is otherwise invisible from inside the game.</summary>
        public string DebugSummary { get; private set; }

        /// <summary>One frame. Called at the TOP of GameUiController.Update, before it reads
        /// Mouse.current, so everything written here is visible to the same frame.</summary>
        public void Tick(PointerMode mode, ActionContext context)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                // Unplugged mid-game: give the mouse back rather than leaving a dead arrow
                // painted over the board.
                Deactivate();
                return;
            }
            FindRealDevices();
            if (padActive && RealDeviceUsed())
            {
                Deactivate();
                return;
            }
            if (!padActive)
            {
                if (!PadTouched(pad))
                {
                    return;
                }
                Activate();
            }
            // EVERY FRAME, not once when the pad takes over. Cursor.visible is global state
            // that the editor and the platform both reset on their own (a focus change is
            // enough), and a single write at the transition loses to any of them - which is
            // how the OS arrow ends up sitting on screen beside the drawn one.
            Cursor.visible = false;
            BuildFrame(pad, mode, context, Time.unscaledDeltaTime);
        }

        /// <summary>Takes the virtual devices back out of the system. Without this an editor
        /// play/stop cycle leaves a new pair behind every time.</summary>
        public void Dispose()
        {
            Deactivate();
            if (virtualMouse != null && virtualMouse.added)
            {
                InputSystem.RemoveDevice(virtualMouse);
            }
            if (virtualKeyboard != null && virtualKeyboard.added)
            {
                InputSystem.RemoveDevice(virtualKeyboard);
            }
            virtualMouse = null;
            virtualKeyboard = null;
        }

        // ---- the frame ---------------------------------------------------------------------

        /// <summary>Reads the pad and writes the whole frame into both virtual devices. This
        /// is the mapping table in code; the header lists it in words.</summary>
        private void BuildFrame(Gamepad pad, PointerMode mode, ActionContext context, float dt)
        {
            if (mode == PointerMode.Snapped)
            {
                // DIRECT play owns the buttons; all the bridge does is put the pointer where
                // the selection is, so the hover visuals and tooltips follow it. The latches
                // are still stepped with nothing pressed, so anything left down lets go.
                tapped.Clear();
                held.Clear();
                foreach (KeyValuePair<Key, SynthButton> entry in keys)
                {
                    entry.Value.Step(false, false);
                }
                leftButton.Step(false, false);
                rightButton.Step(false, false);
                cursorPosition = SnapPosition;
                WriteMouse(cursorPosition, Vector2.zero, 0f);
                WriteKeyboard();
                DrawCursor(false);
                return;
            }
            Vector2 stick = Deadzoned(pad.leftStick.ReadValue());
            Vector2 rightStick = Deadzoned(pad.rightStick.ReadValue());
            bool menu = mode == PointerMode.MenuList;

            // ---- pointer motion. A list menu spends the stick on its selection instead, so
            // the cursor neither moves nor hovers there (see PointerMode).
            Vector2 delta = Vector2.zero;
            if (!menu && stick.sqrMagnitude > 0f)
            {
                float boost = pad.rightTrigger.ReadValue() > TriggerPoint ? CursorBoost : 1f;
                float speed = CursorSpeed * (Screen.height / 1080f) * boost;
                // Squared response: the small movements that aim at one cell stay small while
                // full tilt still crosses the screen.
                delta = stick * (stick.magnitude * speed * dt);
                cursorPosition += delta;
                cursorPosition.x = Mathf.Clamp(cursorPosition.x, 0f, Screen.width);
                cursorPosition.y = Mathf.Clamp(cursorPosition.y, 0f, Screen.height);
            }

            // ---- directions. The d-pad always types arrows (the retro falling piece, page
            // turns, settings rows); the stick joins it only where it is not the pointer, and
            // there it is 4-WAY - a diagonal push on the settings screen would otherwise move
            // the row AND change the setting it landed on.
            bool vertical = Mathf.Abs(stick.y) >= Mathf.Abs(stick.x);
            bool up = pad.dpad.up.isPressed || (menu && vertical && stick.y > 0.5f);
            bool down = pad.dpad.down.isPressed || (menu && vertical && stick.y < -0.5f);
            bool left = pad.dpad.left.isPressed || (menu && !vertical && stick.x < -0.5f);
            bool right = pad.dpad.right.isPressed || (menu && !vertical && stick.x > 0.5f);

            tapped.Clear();
            held.Clear();
            if (repeatUp.Step(up, dt)) { tapped.Add(Key.UpArrow); }
            if (repeatDown.Step(down, dt)) { tapped.Add(Key.DownArrow); }
            if (repeatLeft.Step(left, dt)) { tapped.Add(Key.LeftArrow); }
            if (repeatRight.Step(right, dt)) { tapped.Add(Key.RightArrow); }
            if (!menu && down)
            {
                // Down is the one direction something reads as a HOLD rather than as presses:
                // the retro falling piece soft-drops for as long as it is held. Safe to make
                // it a hold only off the menus, where the repeat is what navigates.
                held.Add(Key.DownArrow);
            }

            // ---- face buttons
            bool southTap = pad.buttonSouth.wasPressedThisFrame;
            if (menu && southTap)
            {
                // SUBMIT as well as a click. ReadMenuChoice takes Enter before the click, and
                // the parked cursor makes the click hit nothing, so this is one action.
                tapped.Add(Key.Enter);
            }
            if (pad.buttonEast.wasPressedThisFrame || pad.startButton.wasPressedThisFrame)
            {
                tapped.Add(Key.Escape);
            }
            if (pad.buttonNorth.wasPressedThisFrame)
            {
                // Only where the stage really has a one-press verb. A round IN PROGRESS has
                // none: everything it offers is a placement, a joker or a power. The debug
                // keys that live around those (redraw the hand, grant a joker, skip a stage)
                // are deliberately NOT on the pad - a pad is how the game is played, not how
                // it is poked at.
                switch (context)
                {
                    case ActionContext.Market:
                        tapped.Add(Key.N); // start the next round
                        break;
                    case ActionContext.RoundDecision:
                        tapped.Add(Key.A); // take the market
                        break;
                }
            }
            if (context == ActionContext.RoundDecision && pad.buttonWest.wasPressedThisFrame)
            {
                // The other half of the advance decision. Safe to double up with the right
                // click below: while the round waits on this choice nothing reads one.
                tapped.Add(Key.C);
            }
            if (pad.leftTrigger.ReadValue() > TriggerPoint)
            {
                held.Add(Key.LeftShift); // "Kaçakçı": hold to smuggle the offer you click
            }
            if (context == ActionContext.Market && pad.rightStickButton.wasPressedThisFrame)
            {
                // "Kredi kartı": settling up is a market action and never automatic. It is on
                // the same control in the DIRECT scheme, so the HUD can name one button.
                tapped.Add(Key.O);
            }

            foreach (KeyValuePair<Key, SynthButton> entry in keys)
            {
                entry.Value.Step(held.Contains(entry.Key), tapped.Contains(entry.Key));
            }
            leftButton.Step(pad.buttonSouth.isPressed, southTap);
            rightButton.Step(pad.buttonWest.isPressed, pad.buttonWest.wasPressedThisFrame);

            // ---- the wheel. One notch per repeat, so a held shoulder walks the deck overlay
            // at a readable pace instead of a page a frame.
            float scroll = 0f;
            if (repeatScrollUp.Step(pad.leftShoulder.isPressed || rightStick.y > 0.5f, dt))
            {
                scroll = ScrollNotch;
            }
            else if (repeatScrollDown.Step(pad.rightShoulder.isPressed || rightStick.y < -0.5f, dt))
            {
                scroll = -ScrollNotch;
            }

            WriteMouse(menu ? ParkedPosition : cursorPosition, menu ? Vector2.zero : delta, scroll);
            WriteKeyboard();
            DrawCursor(!menu);
        }

        private static Vector2 Deadzoned(Vector2 value)
        {
            float magnitude = value.magnitude;
            if (magnitude < StickDeadzone)
            {
                return Vector2.zero;
            }
            // Rescaled from the deadzone edge, so the first pixel of movement past it is slow
            // rather than a jump to a quarter speed.
            return value / magnitude * Mathf.InverseLerp(StickDeadzone, 1f, magnitude);
        }

        private void WriteMouse(Vector2 position, Vector2 delta, float scroll)
        {
            if (virtualMouse == null)
            {
                return;
            }
            var state = new MouseState
            {
                position = position,
                delta = delta,
                scroll = new Vector2(0f, scroll)
            };
            state = state.WithButton(MouseButton.Left, leftButton.Down);
            state = state.WithButton(MouseButton.Right, rightButton.Down);
            InputState.Change(virtualMouse, state);
            // EXPLICITLY current. A state change only makes a device current when the state
            // actually CHANGED, so a pointer that is holding still - which is exactly what a
            // player does in the moment before they click - stops re-asserting itself, and
            // anything that made the real mouse current in the meantime would be read instead.
            virtualMouse.MakeCurrent();
            RecordDebug();
        }

        /// <summary>What the game will see a few lines later, captured right after the write -
        /// and SILENT unless something is wrong.
        ///
        /// A synthesized click that quietly fails to become an EDGE is the one thing about this
        /// bridge that cannot be diagnosed from inside the game: the pointer moves, the button
        /// is plainly down, and nothing happens. So the moment a press has been handed over and
        /// no edge has EVER come back, this says which of the two links broke - the device the
        /// game is reading, or the edge on it. Once one press registers it never speaks again.</summary>
        private void RecordDebug()
        {
            Mouse current = Mouse.current;
            bool ours = current == virtualMouse;
            if (ours && current.leftButton.wasPressedThisFrame)
            {
                sawAnyEdge = true;
            }
            if (leftButton.Down)
            {
                sawAnyPress = true;
            }
            DebugSummary = sawAnyPress && !sawAnyEdge
                ? "gamepad click is not registering - reading "
                    + (current != null ? (ours ? "the pad's own mouse" : current.name) : "no mouse")
                    + ", button " + (leftButton.Down ? "down" : "up") + ", no press edge yet"
                : string.Empty;
        }

        private bool sawAnyEdge;
        private bool sawAnyPress;

        /// <summary>Writes the keyboard ONLY when the set of held keys changed. That leaves
        /// the REAL keyboard current the rest of the time, so the debug keys still work under
        /// a hand while the other one is on the pad.</summary>
        private void WriteKeyboard()
        {
            if (virtualKeyboard == null)
            {
                return;
            }
            keysDown.Clear();
            foreach (KeyValuePair<Key, SynthButton> entry in keys)
            {
                if (entry.Value.Down)
                {
                    keysDown.Add(entry.Key);
                }
            }
            if (keysDown.SetEquals(lastKeysDown))
            {
                return;
            }
            var state = new KeyboardState();
            foreach (Key key in keysDown)
            {
                state.Set(key, true);
            }
            InputState.Change(virtualKeyboard, state);
            lastKeysDown.Clear();
            lastKeysDown.UnionWith(keysDown);
        }

        // ---- taking and giving back the pointer ------------------------------------------

        private void Activate()
        {
            if (virtualMouse == null)
            {
                virtualMouse = InputSystem.AddDevice<Mouse>("Gamepad Cursor");
            }
            else if (!virtualMouse.added)
            {
                InputSystem.AddDevice(virtualMouse);
            }
            if (virtualKeyboard == null)
            {
                virtualKeyboard = InputSystem.AddDevice<Keyboard>("Gamepad Keys");
            }
            else if (!virtualKeyboard.added)
            {
                InputSystem.AddDevice(virtualKeyboard);
            }
            // Start where the hand left off, so picking up the pad does not teleport the
            // pointer away from whatever was being looked at.
            cursorPosition = realMouse != null
                ? realMouse.position.ReadValue()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            cursorPosition.x = Mathf.Clamp(cursorPosition.x, 0f, Screen.width);
            cursorPosition.y = Mathf.Clamp(cursorPosition.y, 0f, Screen.height);
            ResetSynth();
            padActive = true;
            Cursor.visible = false;
        }

        /// <summary>Hands the pointer back to the hand that just asked for it.
        ///
        /// It writes to the virtual devices ONLY if something synthesized is still down. A
        /// write makes them current again, which would steal the pointer straight back from
        /// the mouse movement that ended the pad's turn - but a button left pressed in a
        /// device nobody clears would be reported held FOREVER, and the drag, the designer's
        /// paint stroke and the deck scrollbar all watch isPressed. Releasing costs one frame
        /// of the pointer; not releasing costs the rest of the session.</summary>
        private void Deactivate()
        {
            if (!padActive)
            {
                return;
            }
            padActive = false;
            bool stillHolding = leftButton.Down || rightButton.Down || lastKeysDown.Count > 0;
            ResetSynth();
            if (stillHolding)
            {
                // At the REAL pointer, so even the frame the virtual mouse stays current
                // reports where the hand actually is. The buttons come from the latches,
                // which the reset above has just let go of.
                WriteMouse(realMouse != null ? realMouse.position.ReadValue() : cursorPosition,
                    Vector2.zero, 0f);
                if (virtualKeyboard != null)
                {
                    InputState.Change(virtualKeyboard, new KeyboardState());
                }
            }
            DrawCursor(false);
            Cursor.visible = true;
        }

        private void ResetSynth()
        {
            leftButton.Reset();
            rightButton.Reset();
            foreach (KeyValuePair<Key, SynthButton> entry in keys)
            {
                entry.Value.Reset();
            }
            lastKeysDown.Clear();
            repeatUp.Reset();
            repeatDown.Reset();
            repeatLeft.Reset();
            repeatRight.Reset();
            repeatScrollUp.Reset();
            repeatScrollDown.Reset();
        }

        /// <summary>Anything on the pad that the mapping uses. Deliberately not "any control":
        /// a resting stick and a warm trigger both drift, and either would steal the pointer
        /// off a player who is using the mouse.</summary>
        private static bool PadTouched(Gamepad pad)
        {
            return pad.leftStick.ReadValue().magnitude > StickDeadzone
                || pad.rightStick.ReadValue().magnitude > StickDeadzone
                || pad.dpad.up.isPressed || pad.dpad.down.isPressed
                || pad.dpad.left.isPressed || pad.dpad.right.isPressed
                || pad.buttonSouth.isPressed || pad.buttonEast.isPressed
                || pad.buttonWest.isPressed || pad.buttonNorth.isPressed
                || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                || pad.startButton.isPressed || pad.selectButton.isPressed
                || pad.leftTrigger.ReadValue() > TriggerPoint
                || pad.rightTrigger.ReadValue() > TriggerPoint;
        }

        /// <summary>Real hardware asking for the pointer back. Read off the devices we found
        /// ourselves, never off Mouse.current / Keyboard.current - those may well be OURS.</summary>
        private bool RealDeviceUsed()
        {
            if (realMouse != null)
            {
                // A pixel of jitter is not a request; a real move or a real click is.
                if (realMouse.delta.ReadValue().sqrMagnitude > 1f
                    || realMouse.leftButton.wasPressedThisFrame
                    || realMouse.rightButton.wasPressedThisFrame
                    || Mathf.Abs(realMouse.scroll.ReadValue().y) > 0.01f)
                {
                    return true;
                }
            }
            return realKeyboard != null && realKeyboard.anyKey.wasPressedThisFrame;
        }

        private void FindRealDevices()
        {
            if (realMouse == null || !realMouse.added)
            {
                realMouse = null;
                foreach (InputDevice device in InputSystem.devices)
                {
                    var mouse = device as Mouse;
                    if (mouse != null && mouse != virtualMouse)
                    {
                        realMouse = mouse;
                        break;
                    }
                }
            }
            if (realKeyboard == null || !realKeyboard.added)
            {
                realKeyboard = null;
                foreach (InputDevice device in InputSystem.devices)
                {
                    var keyboard = device as Keyboard;
                    if (keyboard != null && keyboard != virtualKeyboard)
                    {
                        realKeyboard = keyboard;
                        break;
                    }
                }
            }
        }

        // ---- the painted cursor ------------------------------------------------------------

        /// <summary>The OS cursor cannot follow a virtual mouse, so the pointer is DRAWN. It
        /// hangs off the HUD canvas itself rather than off HudShake: a screen shake must not
        /// move the thing the player is aiming with.</summary>
        private void BuildCursor()
        {
            if (canvas == null)
            {
                return;
            }
            var go = new GameObject("GamepadCursor", typeof(RectTransform));
            cursorRect = (RectTransform)go.transform;
            cursorRect.SetParent(canvas.transform, false);
            cursorRect.anchorMin = new Vector2(0.5f, 0.5f);
            cursorRect.anchorMax = new Vector2(0.5f, 0.5f);
            cursorRect.pivot = ArrowPivot;
            cursorRect.sizeDelta = new Vector2(ArrowWidth, ArrowHeight) * CursorScale;
            cursorFill = go.AddComponent<Image>();
            cursorFill.sprite = ArrowSprite;
            cursorFill.raycastTarget = false;
            cursorFill.color = CursorIdle;
            go.SetActive(false);
        }

        private void DrawCursor(bool visible)
        {
            if (cursorRect == null)
            {
                return;
            }
            if (!visible || !padActive)
            {
                if (cursorRect.gameObject.activeSelf)
                {
                    cursorRect.gameObject.SetActive(false);
                }
                return;
            }
            if (!cursorRect.gameObject.activeSelf)
            {
                cursorRect.gameObject.SetActive(true);
            }
            // Over the menus and the bars, which are all under HudShake. Only asserted when it
            // has actually slipped - re-parenting order every frame dirties the canvas.
            Transform parent = cursorRect.parent;
            if (cursorRect.GetSiblingIndex() != parent.childCount - 1)
            {
                cursorRect.SetAsLastSibling();
            }
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)canvas.transform, cursorPosition, null, out local))
            {
                cursorRect.anchoredPosition = local;
            }
            cursorFill.color = leftButton.Down ? CursorPressed : CursorIdle;
        }

        // The arrow, drawn the way everything else on this board is: flat, hard-edged, one
        // pixel of outline and no glow. '#' is the fill, '.' is nothing; the outline is grown
        // around it so the pointer reads over the board AND over a bright market panel.
        private static readonly string[] ArrowRows =
        {
            "#.........",
            "##........",
            "###.......",
            "####......",
            "#####.....",
            "######....",
            "#######...",
            "########..",
            "#########.",
            "##########",
            "######....",
            "###.###...",
            "#...###...",
            "....###...",
            ".....##..."
        };

        private const int ArrowWidth = 12;  // the rows above plus a pixel of outline each side
        private const int ArrowHeight = 17;

        /// <summary>The TIP, in this sprite's normalized coordinates - the point the cursor
        /// actually aims with, and therefore the RectTransform's pivot too.</summary>
        private static readonly Vector2 ArrowPivot =
            new Vector2(1.5f / ArrowWidth, (ArrowHeight - 1.5f) / ArrowHeight);

        private static Sprite ArrowSprite
        {
            get
            {
                if (arrowSprite == null)
                {
                    arrowSprite = BuildArrowSprite();
                }
                return arrowSprite;
            }
        }

        private static Sprite BuildArrowSprite()
        {
            var fill = new bool[ArrowWidth, ArrowHeight];
            for (int row = 0; row < ArrowRows.Length; row++)
            {
                string line = ArrowRows[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] == '#')
                    {
                        // +1 for the outline margin; rows are written top-down, texture rows
                        // run bottom-up.
                        fill[col + 1, ArrowHeight - 2 - row] = true;
                    }
                }
            }
            var tex = new Texture2D(ArrowWidth, ArrowHeight, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var outline = new Color(0.04f, 0.05f, 0.08f, 1f);
            for (int y = 0; y < ArrowHeight; y++)
            {
                for (int x = 0; x < ArrowWidth; x++)
                {
                    if (fill[x, y])
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Neighbours(fill, x, y) ? outline : Color.clear);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, ArrowWidth, ArrowHeight),
                ArrowPivot, ArrowWidth);
        }

        private static bool Neighbours(bool[,] fill, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx >= 0 && nx < ArrowWidth && ny >= 0 && ny < ArrowHeight && fill[nx, ny])
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // ---- the two little machines -------------------------------------------------------

        private SynthButton KeyOf(Key key)
        {
            SynthButton button;
            if (!keys.TryGetValue(key, out button))
            {
                button = new SynthButton();
                keys.Add(key, button);
            }
            return button;
        }

        /// <summary>Every key the mapping can produce, registered up front so the per-frame
        /// loop steps ALL of them - a latch that is not stepped never lets go.</summary>
        private void RegisterKeys()
        {
            KeyOf(Key.Escape);
            KeyOf(Key.Enter);
            KeyOf(Key.UpArrow);
            KeyOf(Key.DownArrow);
            KeyOf(Key.LeftArrow);
            KeyOf(Key.RightArrow);
            KeyOf(Key.A);
            KeyOf(Key.C);
            KeyOf(Key.N);
            KeyOf(Key.O);
            KeyOf(Key.S);
            KeyOf(Key.LeftShift);
        }

        /// <summary>One synthesized button. Down for as long as the player holds it (a drag
        /// has to survive), but NEVER for less than PulseFrames, and always with GapFrames of
        /// release behind it - that is what guarantees the game sees the press edge and the
        /// release edge whatever the frame rate does. See rule 2 in the header.</summary>
        private sealed class SynthButton
        {
            private int downFrames;
            private int gapFrames;
            private bool queued;

            public bool Down { get; private set; }

            public void Step(bool physicalHeld, bool wasTapped)
            {
                if (wasTapped)
                {
                    queued = true;
                }
                if (Down)
                {
                    if (downFrames > 0)
                    {
                        downFrames--;
                    }
                    if (!physicalHeld && downFrames == 0)
                    {
                        Down = false;
                        gapFrames = GapFrames;
                    }
                    return;
                }
                if (gapFrames > 0)
                {
                    gapFrames--;
                    return;
                }
                if (queued || physicalHeld)
                {
                    queued = false;
                    Down = true;
                    downFrames = PulseFrames;
                }
            }

            public void Reset()
            {
                Down = false;
                downFrames = 0;
                gapFrames = 0;
                queued = false;
            }
        }

        /// <summary>Held-direction auto-repeat: one immediately, then a wait, then a steady
        /// stream. Returns true on the frames a press should be synthesized.</summary>
        private sealed class Repeater
        {
            private float timer;
            private bool wasOn;

            public bool Step(bool on, float dt)
            {
                if (!on)
                {
                    wasOn = false;
                    return false;
                }
                if (!wasOn)
                {
                    wasOn = true;
                    timer = RepeatDelay;
                    return true;
                }
                timer -= dt;
                if (timer <= 0f)
                {
                    timer = RepeatInterval;
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                wasOn = false;
                timer = 0f;
            }
        }
    }
}
