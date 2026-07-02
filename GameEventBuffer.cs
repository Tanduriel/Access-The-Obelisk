using System.Collections.Generic;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Keeps a global read-back buffer of important gameplay events.
    /// </summary>
    public static class GameEventBuffer
    {
        private const int MaxEvents = 200;
        private static readonly List<string> Events = new List<string>();
        private static int _index = -1;
        private static bool _focused;

        /// <summary>
        /// Gets whether the global event buffer currently owns detail-reading input.
        /// </summary>
        public static bool IsFocused
        {
            get { return _focused; }
        }

        /// <summary>
        /// Adds an important gameplay event to the buffer.
        /// </summary>
        public static void Add(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string cleaned = TextCleaner.ToSpeech(text);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return;
            }

            Events.Add(cleaned);
            if (Events.Count > MaxEvents)
            {
                Events.RemoveAt(0);
                if (_index > 0)
                {
                    _index--;
                }
            }

            if (!_focused)
            {
                _index = Events.Count - 1;
            }

            DebugLogger.LogState("Event buffer: " + cleaned);
        }

        /// <summary>
        /// Updates global event-buffer hotkeys.
        /// </summary>
        public static bool Update()
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (IsHeroSelectionActive())
            {
                if (!ctrl || !_focused)
                {
                    return false;
                }

                if (ModInput.GetKeyDown(KeyCode.UpArrow))
                {
                    Move(1);
                    return true;
                }

                if (ModInput.GetKeyDown(KeyCode.DownArrow))
                {
                    Move(-1);
                    return true;
                }

                if (ModInput.GetKeyDown(KeyCode.Home))
                {
                    Jump(false);
                    return true;
                }

                if (ModInput.GetKeyDown(KeyCode.End))
                {
                    Jump(true);
                    return true;
                }

                return false;
            }

            if (!ctrl || IsMapActive() || IsLocalControlBufferActive())
            {
                // Local-control handlers (combat, tome, ...) own buffer focus themselves,
                // so only drop focus on the map, where nothing manages the buffer.
                if (IsMapActive())
                {
                    LeaveFocus(false);
                }

                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow) || ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                ToggleFocus();
                return true;
            }

            if (!_focused)
            {
                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(-1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return true;
            }

            return false;
        }

        private static bool IsLocalControlBufferActive()
        {
            CardCraftManager craft = CardCraftManager.Instance;
            GiveManager give = GiveManager.Instance;
            return AccessStateManager.CurrentState == AccessState.Combat
                || AccessStateManager.CurrentState == AccessState.CharacterInfo
                || AccessStateManager.CurrentState == AccessState.Give
                || AccessStateManager.CurrentState == AccessState.HeroSelection
                || AccessStateManager.CurrentState == AccessState.Tome
                || (give != null && give.IsActive())
                || (craft != null
                && craft.gameObject.activeInHierarchy
                && (craft.craftType == 0 || craft.craftType == 4 || craft.craftType == 5));
        }

        /// <summary>
        /// Moves focus into the event buffer at the newest event.
        /// </summary>
        public static void FocusLatest()
        {
            _focused = true;
            if (Events.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_buffer_empty"));
                return;
            }

            _index = Events.Count - 1;
            ScreenReader.Say(Loc.Get("event_buffer_focused"));
            AnnounceCurrent();
        }

        /// <summary>
        /// Leaves event-buffer focus.
        /// </summary>
        public static void LeaveFocus(bool announce)
        {
            if (!_focused)
            {
                return;
            }

            _focused = false;
            if (announce)
            {
                ScreenReader.Say(Loc.Get("event_buffer_left"));
            }
        }

        /// <summary>
        /// Reads another event while the event buffer is focused.
        /// </summary>
        public static void MoveFocused(int delta)
        {
            if (!_focused)
            {
                FocusLatest();
                return;
            }

            Move(delta);
        }

        /// <summary>
        /// Moves to the oldest or newest focused event.
        /// </summary>
        public static void JumpFocused(bool end)
        {
            if (!_focused)
            {
                FocusLatest();
                return;
            }

            Jump(end);
        }

        private static void ToggleFocus()
        {
            _focused = !_focused;
            if (!_focused)
            {
                ScreenReader.Say(Loc.Get("event_buffer_left"));
                return;
            }

            if (Events.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_buffer_empty"));
                return;
            }

            FocusLatest();
        }

        private static void Move(int delta)
        {
            if (Events.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_buffer_empty"));
                return;
            }

            if (_index < 0)
            {
                _index = Events.Count - 1;
            }
            else
            {
                if (!NavigationBounds.TryMove(ref _index, delta, Events.Count))
                {
                    return;
                }
            }

            AnnounceCurrent();
        }

        private static void Jump(bool end)
        {
            if (Events.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_buffer_empty"));
                return;
            }

            if (_index < 0)
            {
                _index = Events.Count - 1;
            }

            if (!NavigationBounds.TryJump(ref _index, end, Events.Count))
            {
                return;
            }

            AnnounceCurrent();
        }

        private static void AnnounceCurrent()
        {
            ScreenReader.Say(Events[_index]);
        }

        private static bool IsMapActive()
        {
            return AccessStateManager.CurrentState == AccessState.Map;
        }

        private static bool IsHeroSelectionActive()
        {
            HeroSelectionManager manager = HeroSelectionManager.Instance;
            return AccessStateManager.CurrentState == AccessState.HeroSelection ||
                (manager != null && manager.allGO != null && manager.allGO.activeInHierarchy);
        }
    }
}
