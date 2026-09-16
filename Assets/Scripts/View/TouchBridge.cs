// PURPOSE: TOUCH INPUT, and it adds no second way to play the game.
//
// THE SAME TRICK THE GAMEPAD USES. There is exactly one input path here: every handler in the game
// reads Mouse.current, and GamepadBridge already proved that the way to add a device is to write
// INTO that rather than to grow a parallel set of handlers. So this owns a virtual mouse and puts
// the finger's position and press into it. Drag-and-drop, hover, the market, the menus and every
// tooltip then work on a phone without one line of theirs changing - which is also what stops the
// touch build and the desktop build from drifting apart.
//
// A FINGER IS THE LEFT BUTTON, one to one, press for press. That is deliberate rather than lazy:
// the whole game is press-move-release (pick a card up, carry it over the arena, drop it), so a
// finger mapped straight onto the left button IS the drag, and anything cleverer - a tap gesture,
// a press-and-hold to pick up - would have to reimplement it.
//
// RIGHT-CLICK IS A SECOND FINGER, because the game genuinely uses it (marking, selling, the
// designer's erase) and a long press cannot be told from the beginning of a drag until it is too
// late - by then the card has already been picked up. The ONE hold in the game is built on top of
// that pick-up rather than in here: a "Simya" card held STILL opens its element picker
// (GameUiController.Alchemy.cs), undoing the pick-up once it is plainly not a drag.
//
// ONLY WHILE A FINGER IS DOWN. Like the pad, this writes only while it is being used, so a device
// with both a touchscreen and a mouse hands the pointer back the moment the mouse moves.
//
// EXTENSION POINT: pinch and two-finger scroll would go here, written into the virtual mouse's
// scroll axis, which is what the deck overlay and the market already read.

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace ProjectBlock.View
{
    /// <summary>Turns finger input into the mouse every existing handler already reads.</summary>
    public sealed class TouchBridge
    {
        /// <summary>Frames a synthesized press is held for. The same reasoning as the pad's: a
        /// handler asks wasPressedThisFrame, and a tap that begins and ends inside one frame would
        /// otherwise be delivered as nothing at all.</summary>
        private const int MinPressFrames = 2;

        private Mouse virtualMouse;

        private bool active;

        private int leftHeldFrames;

        private int rightHeldFrames;

        private bool rightLatched;

        private Vector2 position;

        private Vector2 lastPosition;

        /// <summary>True while a finger is driving the pointer.</summary>
        public bool Active { get { return active; } }

        /// <summary>
        /// Reads the touchscreen and writes it into the virtual mouse. Call once per frame, from
        /// the top of the controller's Update and BEFORE anything reads Mouse.current - the same
        /// contract the pad bridge has, and for the same reason.
        /// </summary>
        public void Tick()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
            {
                Release();
                return;
            }

            TouchControl primary = screen.primaryTouch;
            bool down = primary != null && primary.press.isPressed;
            bool wasDown = leftHeldFrames > 0;

            if (!down && !wasDown && leftHeldFrames <= 0 && rightHeldFrames <= 0)
            {
                // Nothing is happening. Give the pointer back so a mouse or a pad can have it.
                Release();
                return;
            }

            Activate();
            if (virtualMouse == null)
            {
                return;
            }

            if (down)
            {
                lastPosition = position;
                position = primary.position.ReadValue();
                leftHeldFrames = MinPressFrames;
            }
            else if (leftHeldFrames > 0)
            {
                // Held on for a moment past the lift, so the release is delivered as its own
                // frames rather than vanishing with the finger.
                leftHeldFrames--;
            }

            // A SECOND finger is the right button, pulsed once and not repeated while it stays
            // down - the game's right-click actions are all one-shot.
            bool second = SecondFingerDown(screen);
            if (second && !rightLatched)
            {
                rightHeldFrames = MinPressFrames;
                rightLatched = true;
            }
            else if (!second)
            {
                rightLatched = false;
            }
            if (rightHeldFrames > 0)
            {
                rightHeldFrames--;
            }

            var state = new MouseState
            {
                position = position,
                delta = position - lastPosition,
                scroll = Vector2.zero
            };
            state = state.WithButton(MouseButton.Left, leftHeldFrames > 0);
            state = state.WithButton(MouseButton.Right, rightHeldFrames > 0);
            InputState.Change(virtualMouse, state);
            // EXPLICITLY current, for the pad's reason: a state change only makes a device current
            // when the state actually changed, and a finger resting still is exactly the moment
            // before it is lifted - the frame a click is decided on.
            virtualMouse.MakeCurrent();
        }

        private static bool SecondFingerDown(Touchscreen screen)
        {
            var touches = screen.touches;
            int down = 0;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed)
                {
                    down++;
                }
            }
            return down >= 2;
        }

        private void Activate()
        {
            if (virtualMouse == null)
            {
                virtualMouse = InputSystem.AddDevice<Mouse>("Touch Pointer");
            }
            else if (!virtualMouse.added)
            {
                InputSystem.AddDevice(virtualMouse);
            }
            active = true;
        }

        /// <summary>Takes the virtual mouse back out, so the real pointer is current again.</summary>
        public void Release()
        {
            if (!active)
            {
                return;
            }
            active = false;
            leftHeldFrames = 0;
            rightHeldFrames = 0;
            rightLatched = false;
            if (virtualMouse != null && virtualMouse.added)
            {
                InputSystem.RemoveDevice(virtualMouse);
            }
        }

        /// <summary>Drops the device entirely (the controller is going away).</summary>
        public void Dispose()
        {
            Release();
            virtualMouse = null;
        }
    }
}
