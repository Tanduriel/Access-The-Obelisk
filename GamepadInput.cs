using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace AccessTheObelisk
{
    /// <summary>
    /// Reads the active gamepad each frame and translates a fixed set of button
    /// chords and right-stick gestures into the same <see cref="KeyCode"/> presses
    /// the mod's keyboard hotkeys already consume. Keyboard handling is untouched;
    /// the gamepad simply acts as a second source of those key events.
    /// </summary>
    /// <remarks>
    /// Layout (fixed, not rebindable):
    /// D-pad or left stick navigate like the keyboard arrows, A activates like
    /// Enter, B goes back like Escape, Start ends the combat turn like Space,
    /// Select/View opens the focused hero/action like Ctrl+Enter, and (held)
    /// browses multiplayer chat history with Left/Right like the keyboard's [
    /// and ]. Hold LT for self/character info, hold RT for run/enemy info, then
    /// a face button or shoulder. The right stick drives the event buffers.
    /// </remarks>
    public static class GamepadInput
    {
        private const float TriggerThreshold = 0.5f;
        private const float StickPressThreshold = 0.6f;
        private const float StickReleaseThreshold = 0.35f;
        private const float StickRepeatDelay = 0.4f;
        private const float StickRepeatInterval = 0.18f;

        // KeyCodes that should report as "held" this frame (chord modifiers).
        private static readonly HashSet<KeyCode> HeldKeys = new HashSet<KeyCode>();

        // KeyCodes that should report a fresh "down" this frame.
        private static readonly HashSet<KeyCode> DownKeys = new HashSet<KeyCode>();

        // Latched right-stick direction (0 none, 1 up, 2 down, 3 left, 4 right).
        private static int _stickDirection;
        private static float _stickRepeatTimer;

        // Latched navigation direction from the D-pad or left stick.
        private static int _navDirection;
        private static float _navRepeatTimer;
        private static bool _modifierHeld;

        /// <summary>
        /// True while a mod chord modifier trigger (LT or RT) is held. Used by the
        /// suppression patch to stop the game reacting to the chord's buttons.
        /// </summary>
        public static bool ModifierHeld => _modifierHeld;

        /// <summary>True if a gamepad chord pressed this KeyCode this frame.</summary>
        public static bool IsKeyDown(KeyCode key) => DownKeys.Contains(key);

        /// <summary>True if a gamepad chord holds this KeyCode this frame.</summary>
        public static bool IsKeyHeld(KeyCode key) => HeldKeys.Contains(key);

        /// <summary>
        /// True while the game's on-screen keyboard is open. The mod then leaves
        /// the whole gamepad to the game so letters can be picked and typed.
        /// </summary>
        public static bool GameVirtualKeyboardActive =>
            KeyboardManager.Instance != null && KeyboardManager.Instance.IsActive();

        /// <summary>
        /// Recomputes the virtual key state from the current gamepad. Must run once
        /// per frame before any handler reads input.
        /// </summary>
        public static void Update()
        {
            DownKeys.Clear();
            HeldKeys.Clear();
            _modifierHeld = false;

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                _stickDirection = 0;
                _navDirection = 0;
                return;
            }

            if (GameVirtualKeyboardActive)
            {
                _stickDirection = 0;
                _navDirection = 0;
                return;
            }

            bool lt = pad.leftTrigger.ReadValue() > TriggerThreshold;
            bool rt = pad.rightTrigger.ReadValue() > TriggerThreshold;
            _modifierHeld = lt || rt;

            if (pad.startButton.wasPressedThisFrame)
            {
                // Not used by the game or any mod chord, so it is free for an
                // instant end-turn shortcut matching the keyboard's Space key.
                DownKeys.Add(KeyCode.Space);
            }

            // Not used by the game or any mod chord, so it is free for the
            // Ctrl+Enter shortcut (hero detail popup, action confirm, etc.).
            MapButton(pad.selectButton, KeyCode.LeftControl, KeyCode.Return);

            if (lt)
            {
                // Left trigger: information about your own party and resources.
                MapButton(pad.buttonSouth, KeyCode.LeftControl, KeyCode.H);   // character HP
                MapButton(pad.buttonEast, KeyCode.LeftControl, KeyCode.B);    // block / shield
                MapButton(pad.buttonWest, KeyCode.LeftControl, KeyCode.E);    // energy
                MapButton(pad.buttonNorth, KeyCode.LeftControl, KeyCode.F);   // effects / powers
                MapButton(pad.leftShoulder, KeyCode.LeftControl, KeyCode.D);  // dust
                MapButton(pad.rightShoulder, KeyCode.LeftControl, KeyCode.S); // supply
            }
            else if (rt)
            {
                // Right trigger: information about the run and the enemies.
                MapButton(pad.buttonSouth, KeyCode.LeftControl, KeyCode.G);   // gold
                MapButton(pad.buttonEast, KeyCode.LeftControl, KeyCode.R);    // round
                MapButton(pad.buttonWest, KeyCode.LeftShift, KeyCode.V);      // resistances
                MapButton(pad.buttonNorth, KeyCode.LeftControl, KeyCode.I);   // enemy intents
                MapButton(pad.leftShoulder, KeyCode.LeftControl, KeyCode.T);  // turn order
                MapButton(pad.rightShoulder, KeyCode.LeftShift, KeyCode.H);   // lowest-HP hero
            }
            else
            {
                MapNavigation(pad);
            }

            UpdateRightStickBuffers(pad);
        }

        /// <summary>
        /// Maps the D-pad or left stick to the arrow keys, A to Enter and B to
        /// Escape, so the mod's keyboard navigation works identically from the
        /// gamepad. Holding a direction repeats after a short delay.
        /// </summary>
        private static void MapNavigation(Gamepad pad)
        {
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                DownKeys.Add(KeyCode.Return);
            }

            if (pad.buttonSouth.isPressed)
            {
                // Lets the activation guard see the "submit key" as held until release.
                HeldKeys.Add(KeyCode.Return);
            }

            if (pad.buttonEast.wasPressedThisFrame)
            {
                DownKeys.Add(KeyCode.Escape);
            }

            Vector2 v = pad.dpad.ReadValue();
            if (v == Vector2.zero)
            {
                v = pad.leftStick.ReadValue();
            }

            int dir = LatchDirection(v, ref _navDirection, ref _navRepeatTimer);
            if (dir == 0)
            {
                return;
            }

            if (pad.selectButton.isPressed && (dir == 3 || dir == 4))
            {
                // Select + Left/Right browses chat history, matching the
                // keyboard's [ and ] hotkeys (ChatHandler reads those directly).
                DownKeys.Add(dir == 3 ? KeyCode.LeftBracket : KeyCode.RightBracket);
                return;
            }

            PressArrow(dir);
        }

        /// <summary>
        /// Records a chord (modifier + primary) as virtual key presses for the frame
        /// the button was pressed, so hotkeys that test the modifier and the primary
        /// together see both at once.
        /// </summary>
        private static void MapButton(ButtonControl button, KeyCode modifier, KeyCode primary)
        {
            if (button == null || !button.wasPressedThisFrame)
            {
                return;
            }

            HeldKeys.Add(modifier);
            DownKeys.Add(primary);
        }

        /// <summary>
        /// Maps right-stick gestures to the buffer hotkeys (Ctrl + arrows): up/down
        /// step through the current buffer, left/right switch buffers. Holding a
        /// direction repeats after a short delay.
        /// </summary>
        private static void UpdateRightStickBuffers(Gamepad pad)
        {
            int dir = LatchDirection(pad.rightStick.ReadValue(), ref _stickDirection, ref _stickRepeatTimer);
            if (dir == 0)
            {
                return;
            }

            HeldKeys.Add(KeyCode.LeftControl);
            PressArrow(dir);
        }

        /// <summary>
        /// Turns an analog direction vector into a latched digital direction
        /// (0 none, 1 up, 2 down, 3 left, 4 right) with hold-to-repeat. Returns
        /// the direction on the frames it should fire, otherwise 0.
        /// </summary>
        private static int LatchDirection(Vector2 v, ref int direction, ref float repeatTimer)
        {
            if (v.magnitude < StickReleaseThreshold)
            {
                direction = 0;
                repeatTimer = 0f;
                return 0;
            }

            int dir = 0;
            if (Mathf.Abs(v.y) >= Mathf.Abs(v.x))
            {
                if (v.y > StickPressThreshold)
                {
                    dir = 1;
                }
                else if (v.y < -StickPressThreshold)
                {
                    dir = 2;
                }
            }
            else if (v.x < -StickPressThreshold)
            {
                dir = 3;
            }
            else if (v.x > StickPressThreshold)
            {
                dir = 4;
            }

            if (dir == 0)
            {
                return 0;
            }

            bool fire;
            if (dir != direction)
            {
                direction = dir;
                repeatTimer = StickRepeatDelay;
                fire = true;
            }
            else
            {
                repeatTimer -= Time.unscaledDeltaTime;
                fire = repeatTimer <= 0f;
                if (fire)
                {
                    repeatTimer = StickRepeatInterval;
                }
            }

            return fire ? dir : 0;
        }

        /// <summary>Records the arrow KeyCode matching a latched direction as pressed.</summary>
        private static void PressArrow(int dir)
        {
            switch (dir)
            {
                case 1:
                    DownKeys.Add(KeyCode.UpArrow);
                    break;
                case 2:
                    DownKeys.Add(KeyCode.DownArrow);
                    break;
                case 3:
                    DownKeys.Add(KeyCode.LeftArrow);
                    break;
                case 4:
                    DownKeys.Add(KeyCode.RightArrow);
                    break;
            }
        }
    }
}
