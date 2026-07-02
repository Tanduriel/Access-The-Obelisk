using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for Paradox legal document popups.
    /// </summary>
    public sealed class ParadoxDocumentHandler
    {
        private readonly List<string> _lines = new List<string>();
        private string _lastDocumentText;
        private bool _announced;
        private int _index;

        /// <summary>
        /// Updates legal document focus, line reading, and close activation.
        /// </summary>
        public bool Update()
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (!IsOpen(menu))
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.ParadoxDocument);
            RebuildIfNeeded(menu);
            AnnounceOnce(menu);
            ProcessKeys(menu);
            EnsureFocus(menu);
            return true;
        }

        private static bool IsOpen(MainMenuManager menu)
        {
            return menu != null &&
                menu.paradoxDocumentPopup != null &&
                menu.paradoxDocumentPopup.gameObject.activeInHierarchy;
        }

        private void Reset()
        {
            _lines.Clear();
            _lastDocumentText = null;
            _announced = false;
            _index = 0;
        }

        private void RebuildIfNeeded(MainMenuManager menu)
        {
            string rawText = menu.paradoxDocumentText != null ? menu.paradoxDocumentText.text : string.Empty;
            if (rawText == _lastDocumentText && _lines.Count > 0)
            {
                return;
            }

            _lastDocumentText = rawText;
            _lines.Clear();
            AddDocumentLines(rawText);
            AddCloseLine(menu);
            _index = 0;
            _announced = false;
        }

        private void AnnounceOnce(MainMenuManager menu)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(Loc.Get("pdx_document_screen"));
            ScreenReader.SayQueued(CurrentLine(menu));
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

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                Activate(menu);
            }
        }

        private void Move(MainMenuManager menu, int delta)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("pdx_document_empty"));
                return;
            }

            int nextIndex = Clamp(_index + delta, 0, _lines.Count - 1);
            if (nextIndex == _index && (_lines.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            _index = nextIndex;
            UpdateScroll(menu);
            FocusCloseButtonIfNeeded(menu);
            ScreenReader.Say(CurrentLine(menu));
        }

        private void Jump(MainMenuManager menu, bool end)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("pdx_document_empty"));
                return;
            }

            int nextIndex = end ? _lines.Count - 1 : 0;
            if (nextIndex == _index && (_lines.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            _index = nextIndex;
            UpdateScroll(menu);
            FocusCloseButtonIfNeeded(menu);
            ScreenReader.Say(CurrentLine(menu));
        }

        private string CurrentLine(MainMenuManager menu)
        {
            if (_lines.Count == 0)
            {
                return Loc.Get("pdx_document_empty");
            }

            string text = _lines[_index];
            if (IsCloseIndex())
            {
                return Loc.Get("pdx_document_close_focus", text);
            }

            return text;
        }

        private void Activate(MainMenuManager menu)
        {
            if (!IsCloseIndex())
            {
                ScreenReader.Say(Loc.Get("pdx_document_not_button"));
                return;
            }

            string closeText = _lines.Count > 0 ? _lines[_index] : CloseText(menu);
            ScreenReader.Say(Loc.Get("activated", closeText));
            menu.ClosePDXDocument();
            Reset();
        }

        private void EnsureFocus(MainMenuManager menu)
        {
            if (!IsCloseIndex())
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == CloseGameObject(menu))
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                return;
            }

            FocusCloseButtonIfNeeded(menu);
        }

        private void FocusCloseButtonIfNeeded(MainMenuManager menu)
        {
            GameObject close = CloseGameObject(menu);
            if (close == null)
            {
                return;
            }

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != close)
            {
                EventSystem.current.SetSelectedGameObject(close);
            }

            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(close.transform.position);
            }
        }

        private void UpdateScroll(MainMenuManager menu)
        {
            if (menu.paradoxDocumentScrollRect == null || _lines.Count <= 1 || IsCloseIndex())
            {
                return;
            }

            int readableCount = _lines.Count - 1;
            float normalized = 1f - ((float)_index / Mathf.Max(1, readableCount - 1));
            menu.paradoxDocumentScrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        private bool IsCloseIndex()
        {
            return _lines.Count > 0 && _index == _lines.Count - 1;
        }

        private static GameObject CloseGameObject(MainMenuManager menu)
        {
            return menu != null && menu.paradoxDocumentCloseButton != null ? menu.paradoxDocumentCloseButton.gameObject : null;
        }

        private void AddDocumentLines(string rawText)
        {
            string normalized = Regex.Replace(rawText ?? string.Empty, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
            normalized = normalized.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] paragraphs = normalized.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
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
            int split = text.LastIndexOf(". ", maxLength, System.StringComparison.Ordinal);
            if (split > 80)
            {
                return split + 1;
            }

            split = text.LastIndexOf("; ", maxLength, System.StringComparison.Ordinal);
            if (split > 80)
            {
                return split + 1;
            }

            split = text.LastIndexOf(' ', maxLength);
            return split > 80 ? split : maxLength;
        }

        private void AddCloseLine(MainMenuManager menu)
        {
            string close = CloseText(menu);
            _lines.Add(string.IsNullOrWhiteSpace(close) ? "Close" : close);
        }

        private static string CloseText(MainMenuManager menu)
        {
            if (menu != null && menu.paradoxDocumentCloseButton != null)
            {
                TMP_Text text = menu.paradoxDocumentCloseButton.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    string clean = TextCleaner.ToSpeech(text.text);
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        return clean;
                    }
                }

                BotonGeneric boton = menu.paradoxDocumentCloseButton.GetComponentInChildren<BotonGeneric>(true);
                if (boton != null)
                {
                    string clean = TextCleaner.ToSpeech(boton.GetText());
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        return clean;
                    }
                }

                Button button = menu.paradoxDocumentCloseButton.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>(true);
                    if (buttonText != null)
                    {
                        string clean = TextCleaner.ToSpeech(buttonText.text);
                        if (!string.IsNullOrWhiteSpace(clean))
                        {
                            return clean;
                        }
                    }
                }
            }

            string gameClose = GameText.Get("close");
            return string.IsNullOrWhiteSpace(gameClose) ? "Close" : TextCleaner.ToSpeech(gameClose);
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
