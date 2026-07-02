using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace AccessTheObelisk
{
    /// <summary>
    /// Stops the game from acting on gamepad input the mod owns. The mod drives
    /// navigation itself (D-pad/left stick as arrows, A as Enter, B as Escape),
    /// so the game's own focus movement and gamepad "click" are suppressed; while
    /// a chord modifier trigger (LT/RT) is held, the face buttons and shoulders
    /// are claimed too. The game keeps the gamepad while its on-screen keyboard
    /// is open.
    /// </summary>
    [HarmonyPatch(typeof(InputController))]
    internal static class GamepadSuppressionPatch
    {
        /// <summary>
        /// Skips the game's gamepad "click" (Fire, bound to the south button). The
        /// mod translates that button to Enter and activates its own selection, so
        /// the game must not also click whatever the cursor happens to be over.
        /// Mouse and keyboard triggers of Fire are left alone.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("DoFire")]
        private static bool SuppressGamepadFire(InputAction.CallbackContext _context)
        {
            if (GamepadInput.ModifierHeld)
            {
                return false;
            }

            bool fromGamepad = _context.control != null && _context.control.device is Gamepad;
            return !fromGamepad || GamepadInput.GameVirtualKeyboardActive;
        }

        /// <summary>
        /// Skips the game's own gamepad focus movement (D-pad and left stick). The
        /// mod maps those to arrow keys and moves its spoken selection instead, so
        /// two competing cursors would otherwise drift apart. Keyboard-sourced
        /// movement and the on-screen keyboard keep the game's behavior.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("DoMovementVector")]
        private static bool SuppressGamepadFocusMovement(bool fromKeyboard)
        {
            if (fromKeyboard || Gamepad.current == null)
            {
                return true;
            }

            return GamepadInput.GameVirtualKeyboardActive;
        }

        /// <summary>
        /// Skips the game's gamepad button dispatch for face buttons and shoulders
        /// while a chord modifier is held. The triggers themselves are left alone so
        /// the game's options-menu navigation keeps working.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch("DoKeyBinding")]
        private static bool SuppressButtonsDuringChord(InputAction.CallbackContext _context)
        {
            if (!GamepadInput.ModifierHeld)
            {
                return true;
            }

            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                return true;
            }

            object control = _context.control;
            bool isChordButton = control == pad.buttonSouth
                || control == pad.buttonNorth
                || control == pad.buttonEast
                || control == pad.buttonWest
                || control == pad.leftShoulder
                || control == pad.rightShoulder;

            // false = skip the original (mod owns this button); true = let it run.
            return !isChordButton;
        }
    }

    /// <summary>
    /// Hands the gamepad to the mod by removing its bindings from Unity's UI input
    /// module. Without this the module would still move the engine-level UI focus
    /// and submit the focused element in parallel with the mod's own navigation.
    /// Keyboard and mouse bindings are left untouched.
    /// </summary>
    internal static class GamepadNavigation
    {
        /// <summary>
        /// Strips gamepad bindings from the active UI input module's move and
        /// submit actions. Safe to call on every scene load; once stripped, later
        /// calls find nothing to remove.
        /// </summary>
        public static void ReleaseGamepadFromUi()
        {
            try
            {
                InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
                if (module == null)
                {
                    return;
                }

                StripGamepadBindings(module.move != null ? module.move.action : null);
                StripGamepadBindings(module.submit != null ? module.submit.action : null);
            }
            catch (System.Exception ex)
            {
                Main.Log.LogWarning("Could not release gamepad from UI navigation: " + ex.Message);
            }
        }

        /// <summary>Erases every gamepad binding on one UI action, if it has any.</summary>
        private static void StripGamepadBindings(InputAction action)
        {
            if (action == null)
            {
                return;
            }

            bool wasEnabled = action.actionMap != null && action.actionMap.enabled;
            if (wasEnabled)
            {
                action.actionMap.Disable();
            }

            for (int i = action.bindings.Count - 1; i >= 0; i--)
            {
                string path = action.bindings[i].effectivePath;
                if (!string.IsNullOrEmpty(path) && (path.Contains("Gamepad") || path.Contains("rightStick")))
                {
                    action.ChangeBinding(i).Erase();
                }
            }

            if (wasEnabled)
            {
                action.actionMap.Enable();
            }
        }
    }
}
