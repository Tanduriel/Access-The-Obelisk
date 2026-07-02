using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for the credits screen in the main menu.
    /// </summary>
    public sealed class CreditsHandler
    {
        private readonly MainMenuHandler _mainMenuHandler;
        private readonly List<string> _lines = new List<string>();
        private string _lastCreditText;
        private bool _announced;
        private int _index;

        /// <summary>
        /// Initializes CreditsHandler with a reference to the main menu handler for focus restoration.
        /// </summary>
        public CreditsHandler(MainMenuHandler mainMenuHandler)
        {
            _mainMenuHandler = mainMenuHandler;
        }

        /// <summary>
        /// Updates credits screen reading and navigation.
        /// </summary>
        public bool Update()
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (!IsOpen(menu))
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Credits);
            RebuildIfNeeded(menu);
            AnnounceOnce();
            ProcessKeys(menu);
            return true;
        }

        private static bool IsOpen(MainMenuManager menu)
        {
            return menu != null
                && menu.credits != null
                && menu.credits.gameObject.activeInHierarchy
                && menu.menuT != null
                && !menu.menuT.gameObject.activeSelf;
        }

        private void Reset()
        {
            _lines.Clear();
            _lastCreditText = null;
            _announced = false;
            _index = 0;
        }

        private void RebuildIfNeeded(MainMenuManager menu)
        {
            string rawText = menu.creditText != null ? menu.creditText.text : string.Empty;
            if (rawText == _lastCreditText && _lines.Count > 0)
            {
                return;
            }

            _lastCreditText = rawText;
            _lines.Clear();
            AddCreditLines(rawText);
            _lines.Add(Loc.Get("credits_close"));
            _index = 0;
            _announced = false;
        }

        private void AnnounceOnce()
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(Loc.Get("credits_screen"));
            ScreenReader.SayQueued(CurrentLine());
        }

        private void ProcessKeys(MainMenuManager menu)
        {
            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(menu, -1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(menu, 1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(menu, -5);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                Move(menu, 5);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                Jump(menu, false);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                Jump(menu, true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                Close(menu);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                if (IsCloseIndex())
                {
                    Close(menu);
                }
            }
        }

        private void Move(MainMenuManager menu, int delta)
        {
            if (_lines.Count == 0)
            {
                return;
            }

            int next = Clamp(_index + delta, 0, _lines.Count - 1);
            if (next == _index && (_lines.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            _index = next;
            UpdateScroll(menu);
            ScreenReader.Say(CurrentLine());
        }

        private void Jump(MainMenuManager menu, bool end)
        {
            if (_lines.Count == 0)
            {
                return;
            }

            int next = end ? _lines.Count - 1 : 0;
            if (next == _index && (_lines.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            _index = next;
            UpdateScroll(menu);
            ScreenReader.Say(CurrentLine());
        }

        private void Close(MainMenuManager menu)
        {
            string closeText = _lines.Count > 0 ? _lines[_lines.Count - 1] : Loc.Get("credits_close");
            ScreenReader.Say(Loc.Get("activated", closeText));
            menu.ShowSaveGame(false);
            Reset();
            _mainMenuHandler?.ForceReannounce();
        }

        private string CurrentLine()
        {
            if (_lines.Count == 0)
            {
                return string.Empty;
            }

            if (IsCloseIndex())
            {
                return Loc.Get("credits_close_focus", _lines[_index]);
            }

            return _lines[_index];
        }

        private bool IsCloseIndex()
        {
            return _lines.Count > 0 && _index == _lines.Count - 1;
        }

        private void UpdateScroll(MainMenuManager menu)
        {
            if (menu.creditScrollRect == null || _lines.Count <= 1 || IsCloseIndex())
            {
                return;
            }

            int readableCount = _lines.Count - 1;
            float normalized = 1f - ((float)_index / Mathf.Max(1, readableCount - 1));
            menu.creditScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        private void AddCreditLines(string rawText)
        {
            string normalized = Regex.Replace(rawText ?? string.Empty, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
            normalized = normalized.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] paragraphs = normalized.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < paragraphs.Length; i++)
            {
                AddReadableChunk(TextCleaner.ToSpeech(paragraphs[i]));
            }
        }

        private void AddReadableChunk(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            const int maxLength = 260;
            string remaining = text.Trim();
            while (remaining.Length > maxLength)
            {
                int split = FindSplitIndex(remaining, maxLength);
                _lines.Add(remaining.Substring(0, split).Trim());
                remaining = remaining.Substring(split).Trim();
            }

            if (!string.IsNullOrWhiteSpace(remaining))
            {
                _lines.Add(remaining);
            }
        }

        private static int FindSplitIndex(string text, int maxLength)
        {
            int split = text.LastIndexOf(". ", maxLength, StringComparison.Ordinal);
            if (split > 80)
            {
                return split + 1;
            }

            split = text.LastIndexOf("; ", maxLength, StringComparison.Ordinal);
            if (split > 80)
            {
                return split + 1;
            }

            split = text.LastIndexOf(' ', maxLength);
            return split > 80 ? split : maxLength;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
