using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides lightweight keyboard access to the native multiplayer chat field and history.
    /// </summary>
    public sealed class ChatHandler
    {
        private readonly List<string> _history = new List<string>();
        private int _historyIndex = -1;

        /// <summary>
        /// Updates multiplayer chat hotkeys.
        /// </summary>
        public bool Update()
        {
            ChatManager chat = ChatManager.Instance;
            if (!IsAvailable(chat))
            {
                Reset();
                return false;
            }

            if (IsChatInputFocused(chat))
            {
                return UpdateFocusedChatInput(chat);
            }

            if (TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.Slash))
            {
                FocusChatInput(chat);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftBracket))
            {
                ReadHistory(chat, -1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.RightBracket))
            {
                ReadHistory(chat, 1);
                return true;
            }

            return false;
        }

        private void Reset()
        {
            _history.Clear();
            _historyIndex = -1;
        }

        private void FocusChatInput(ChatManager chat)
        {
            if (chat.chatGO != null && !chat.chatGO.gameObject.activeInHierarchy)
            {
                chat.ShowChat();
            }

            TMP_InputField input = chat.chatInput;
            if (input == null || !input.gameObject.activeInHierarchy || !input.interactable)
            {
                ScreenReader.Say(Loc.Get("chat_input_unavailable"));
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }

            input.ActivateInputField();
            ScreenReader.Say(Loc.Get("chat_input_focused"));
        }

        private bool UpdateFocusedChatInput(ChatManager chat)
        {
            if (ModInput.GetKeyDown(KeyCode.UpArrow) ||
                ModInput.GetKeyDown(KeyCode.DownArrow) ||
                ModInput.GetKeyDown(KeyCode.LeftArrow) ||
                ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                DeactivateChatInput(chat);
                ScreenReader.Say(Loc.Get("chat_input_left"));
                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                return true;
            }

            return false;
        }

        private void DeactivateChatInput(ChatManager chat)
        {
            if (chat.chatInput != null)
            {
                chat.chatInput.DeactivateInputField();
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void ReadHistory(ChatManager chat, int delta)
        {
            RebuildHistory(chat);
            if (_history.Count == 0)
            {
                _historyIndex = -1;
                ScreenReader.Say(Loc.Get("chat_history_empty"));
                return;
            }

            if (_historyIndex < 0 || _historyIndex >= _history.Count)
            {
                _historyIndex = _history.Count - 1;
            }
            else if (!NavigationBounds.TryMove(ref _historyIndex, delta, _history.Count))
            {
                return;
            }

            AnnounceHistory();
        }

        private void AnnounceHistory()
        {
            if (_historyIndex < 0 || _historyIndex >= _history.Count)
            {
                ScreenReader.Say(Loc.Get("chat_history_empty"));
                return;
            }

            ScreenReader.Say(Loc.Get("chat_history_item", _historyIndex + 1, _history.Count, _history[_historyIndex]));
        }

        private void RebuildHistory(ChatManager chat)
        {
            _history.Clear();
            string text = chat.chatText != null ? chat.chatText.text : string.Empty;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = TextCleaner.ToSpeech(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    _history.Add(line);
                }
            }
        }

        private static bool IsAvailable(ChatManager chat)
        {
            return chat != null
                && chat.gameObject != null
                && chat.gameObject.activeInHierarchy
                && GameManager.Instance != null
                && GameManager.Instance.IsMultiplayer();
        }

        private static bool IsChatInputFocused(ChatManager chat)
        {
            return EventSystem.current != null
                && chat != null
                && chat.chatInput != null
                && EventSystem.current.currentSelectedGameObject == chat.chatInput.gameObject;
        }
    }
}
