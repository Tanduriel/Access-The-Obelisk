using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Announces focus and open/close state for the game's on-screen keyboard
    /// overlay, used for text entry (chat, lobby names, search fields) when a
    /// gamepad drives the cursor instead of a physical keyboard.
    /// </summary>
    public sealed class VirtualKeyboardHandler
    {
        /// <summary>
        /// Grace period after <see cref="KeyboardManager.Instance"/> first appears
        /// during which state changes are tracked silently, never announced. The
        /// overlay's GameObject starts active in the scene and its own
        /// <c>Start()</c> hides it shortly after, but not always within the same
        /// frame our polling sees it, so <see cref="_announcedOpen"/> alone isn't
        /// enough to suppress the very first "opened": this window keeps
        /// <see cref="_wasActive"/> from ever taking the "just opened" branch
        /// (the only place that sets <see cref="_announcedOpen"/>) for that
        /// startup blip, however long it happens to last.
        /// </summary>
        private const float StartupGraceSeconds = 1f;

        private bool _wasActive;
        private bool _announcedOpen;
        private int _lastFocusIndex = -1;
        private float _readyAt = -1f;

        /// <summary>
        /// Announces the overlay opening/closing and focus changes between its
        /// keys. Returns true while the overlay is open so downstream handlers
        /// do not also react to the same input.
        /// </summary>
        public bool Update()
        {
            KeyboardManager keyboard = KeyboardManager.Instance;
            if (keyboard == null)
            {
                Reset();
                _readyAt = -1f;
                return false;
            }

            if (_readyAt < 0f)
            {
                _readyAt = Time.unscaledTime + StartupGraceSeconds;
            }

            bool active = keyboard.elements != null && keyboard.IsActive();
            if (Time.unscaledTime < _readyAt)
            {
                _wasActive = active;
                return active;
            }

            if (!active)
            {
                // Only announce closing if we actually announced opening. This
                // covers the case where the startup blip outlasts the grace
                // window above: _wasActive got silently latched to true during
                // the window, so the "just opened" branch below never runs and
                // _announcedOpen is never set, once the overlay is really hidden.
                if (_announcedOpen)
                {
                    ScreenReader.Say(Loc.Get("virtual_keyboard_closed"));
                }

                Reset();
                return false;
            }

            if (!_wasActive)
            {
                _wasActive = true;
                _announcedOpen = true;
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
            _announcedOpen = false;
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
    [HarmonyPatch(typeof(KeyboardManager), "DoKey")]
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
