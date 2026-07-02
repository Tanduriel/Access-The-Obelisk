using HarmonyLib;
using TMPro;

namespace AccessTheObelisk
{
    /// <summary>
    /// Announces focus and open/close state for the game's on-screen keyboard
    /// overlay, used for text entry (chat, lobby names, search fields) when a
    /// gamepad drives the cursor instead of a physical keyboard.
    /// </summary>
    public sealed class VirtualKeyboardHandler
    {
        private bool _wasActive;
        private int _lastFocusIndex = -1;

        /// <summary>
        /// Announces the overlay opening/closing and focus changes between its
        /// keys. Returns true while the overlay is open so downstream handlers
        /// do not also react to the same input.
        /// </summary>
        public bool Update()
        {
            KeyboardManager keyboard = KeyboardManager.Instance;
            bool active = keyboard != null && keyboard.elements != null && keyboard.IsActive();
            if (!active)
            {
                if (_wasActive)
                {
                    ScreenReader.Say(Loc.Get("virtual_keyboard_closed"));
                }

                Reset();
                return false;
            }

            if (!_wasActive)
            {
                _wasActive = true;
                ScreenReader.Say(Loc.Get("virtual_keyboard_opened"));
                AnnounceFocus(keyboard, true);
                return true;
            }

            AnnounceFocus(keyboard, false);
            return true;
        }

        private void Reset()
        {
            _wasActive = false;
            _lastFocusIndex = -1;
        }

        private void AnnounceFocus(KeyboardManager keyboard, bool force)
        {
            int index = keyboard.controllerHorizontalIndex;
            if (!force && index == _lastFocusIndex)
            {
                return;
            }

            _lastFocusIndex = index;
            TMP_Text key = GetKey(keyboard, index);
            if (key == null)
            {
                return;
            }

            ScreenReader.Say(DescribeKey(key));
        }

        private static TMP_Text GetKey(KeyboardManager keyboard, int index)
        {
            if (keyboard.keyList == null || index < 0 || index >= keyboard.keyList.Count)
            {
                return null;
            }

            return keyboard.keyList[index];
        }

        /// <summary>Spoken label for a key: a friendly name for the special keys, the character itself otherwise.</summary>
        internal static string DescribeKey(TMP_Text key)
        {
            string internalName = key.transform.parent != null ? key.transform.parent.name.ToLowerInvariant() : "";
            switch (internalName)
            {
                case "keyspace":
                    return Loc.Get("virtual_keyboard_space");
                case "keyshift":
                    return Loc.Get("virtual_keyboard_shift");
                case "keyreturn":
                    return Loc.Get("virtual_keyboard_return");
                case "keydelete":
                    return Loc.Get("virtual_keyboard_delete");
                default:
                    return TextCleaner.ToSpeech(key.text);
            }
        }
    }

    /// <summary>
    /// Announces each on-screen keyboard key press as it is typed, mirroring how
    /// a screen reader echoes typed characters on a physical keyboard.
    /// </summary>
    [HarmonyPatch(typeof(KeyboardManager), nameof(KeyboardManager.DoKey))]
    internal static class VirtualKeyboardKeyPressPatch
    {
        private static void Postfix(string name, string value)
        {
            switch (name)
            {
                case "keyspace":
                    ScreenReader.Say(Loc.Get("virtual_keyboard_space"));
                    break;
                case "keydelete":
                    ScreenReader.Say(Loc.Get("virtual_keyboard_delete"));
                    break;
                case "keyshift":
                    ScreenReader.Say(Loc.Get(IsUppercase(KeyboardManager.Instance) ? "virtual_keyboard_shift_on" : "virtual_keyboard_shift_off"));
                    break;
                case "keyreturn":
                    // The overlay closes right after this; VirtualKeyboardHandler
                    // announces that transition on the next frame.
                    break;
                default:
                    if (!string.IsNullOrEmpty(value))
                    {
                        ScreenReader.Say(TextCleaner.ToSpeech(value));
                    }

                    break;
            }
        }

        /// <summary>Infers the shift state from any single-letter key's current case.</summary>
        private static bool IsUppercase(KeyboardManager keyboard)
        {
            if (keyboard == null || keyboard.keyList == null)
            {
                return false;
            }

            foreach (TMP_Text key in keyboard.keyList)
            {
                if (key != null && key.text.Length == 1 && char.IsLetter(key.text[0]))
                {
                    return char.IsUpper(key.text[0]);
                }
            }

            return false;
        }
    }
}
