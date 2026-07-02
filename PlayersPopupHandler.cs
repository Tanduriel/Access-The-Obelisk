using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for the native multiplayer players popup.
    /// </summary>
    public sealed class PlayersPopupHandler
    {
        private enum PlayerItemKind
        {
            Player,
            Mute,
            Unmute
        }

        private sealed class PlayerItem
        {
            public PlayerItemKind Kind;
            public AlertPlayer Player;
            public Transform Transform;
            public int Slot;
            public string Label;
            public string Key;
        }

        private readonly List<PlayerItem> _items = new List<PlayerItem>();
        private int _index;
        private bool _announced;
        private string _lastFocusKey;

        /// <summary>
        /// Opens and updates the multiplayer players popup.
        /// </summary>
        public bool Update()
        {
            AlertManager alert = AlertManager.Instance;
            if (!IsPlayersPopupOpen(alert))
            {
                Reset();
                if (ShouldOpenPlayersPopup())
                {
                    OpenPlayersPopup();
                    return true;
                }

                return false;
            }

            AccessStateManager.SetState(AccessState.PlayersPopup);
            Refresh(alert);
            AnnounceOpened();
            ProcessKeys(alert);
            AnnounceFocus();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _announced = false;
            _lastFocusKey = null;
        }

        private bool ShouldOpenPlayersPopup()
        {
            if (!ModInput.GetKeyDown(KeyCode.P) || TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return false;
            }

            return GameManager.Instance != null
                && GameManager.Instance.IsMultiplayer()
                && NetworkManager.Instance != null
                && NetworkManager.Instance.IsConnected();
        }

        private void OpenPlayersPopup()
        {
            if (NetworkManager.Instance == null || NetworkManager.Instance.GetNumPlayers() < 2)
            {
                ScreenReader.Say(Loc.Get("players_popup_unavailable"));
                return;
            }

            ChatManager chat = ChatManager.Instance;
            if (chat != null)
            {
                chat.ShowPlayers();
            }
            else if (AlertManager.Instance != null)
            {
                AlertManager.Instance.ShowPlayers();
            }
        }

        private void Refresh(AlertManager alert)
        {
            string currentKey = CurrentKey();
            _items.Clear();
            if (alert.playerList != null)
            {
                for (int i = 0; i < alert.playerList.Count; i++)
                {
                    AlertPlayer player = alert.playerList[i];
                    if (player == null || !player.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    AddPlayerItem(player, i);
                    AddActionItem(player, i, PlayerItemKind.Mute, player.muteButton, Loc.Get("players_popup_mute", PlayerName(i)));
                    AddActionItem(player, i, PlayerItemKind.Unmute, player.unmuteButton, Loc.Get("players_popup_unmute", PlayerName(i)));
                }
            }

            RestoreFocus(currentKey);
        }

        private void AddPlayerItem(AlertPlayer player, int slot)
        {
            PlayerItem item = new PlayerItem();
            item.Kind = PlayerItemKind.Player;
            item.Player = player;
            item.Transform = player.transform;
            item.Slot = slot;
            item.Label = PlayerLabel(player, slot);
            item.Key = "player:" + slot + ":" + item.Label;
            _items.Add(item);
        }

        private void AddActionItem(AlertPlayer player, int slot, PlayerItemKind kind, Transform transform, string label)
        {
            if (!IsVisible(transform))
            {
                return;
            }

            PlayerItem item = new PlayerItem();
            item.Kind = kind;
            item.Player = player;
            item.Transform = transform;
            item.Slot = slot;
            item.Label = label;
            item.Key = kind + ":" + slot;
            _items.Add(item);
        }

        private void AnnounceOpened()
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(Loc.Get("players_popup_screen"));
        }

        private void ProcessKeys(AlertManager alert)
        {
            if (ModInput.GetKeyDown(KeyCode.UpArrow) || ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow) || ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                Move(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrent();
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                Close(alert);
            }
        }

        private void Move(int delta)
        {
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            FocusCurrent();
            AnnounceFocus(force: true);
        }

        private void Jump(bool end)
        {
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            FocusCurrent();
            AnnounceFocus(force: true);
        }

        private void ActivateCurrent()
        {
            PlayerItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("players_popup_empty"));
                return;
            }

            if (item.Kind == PlayerItemKind.Player)
            {
                ScreenReader.Say(item.Label);
                return;
            }

            if (item.Kind == PlayerItemKind.Mute)
            {
                item.Player.DoMute();
                ScreenReader.Say(Loc.Get("players_popup_muted", PlayerName(item.Slot)));
            }
            else if (item.Kind == PlayerItemKind.Unmute)
            {
                item.Player.DoUnmute();
                ScreenReader.Say(Loc.Get("players_popup_unmuted", PlayerName(item.Slot)));
            }

            Refresh(AlertManager.Instance);
            FocusCurrent();
            AnnounceFocus(force: true);
        }

        private void AnnounceFocus(bool force = false)
        {
            PlayerItem item = CurrentItem();
            if (item == null)
            {
                if (force || _lastFocusKey != "empty")
                {
                    _lastFocusKey = "empty";
                    ScreenReader.Say(Loc.Get("players_popup_empty"));
                }

                return;
            }

            string key = item.Key + ":" + item.Label;
            if (!force && key == _lastFocusKey)
            {
                return;
            }

            _lastFocusKey = key;
            FocusCurrent();
            ScreenReader.Say(item.Label);
        }

        private void FocusCurrent()
        {
            PlayerItem item = CurrentItem();
            if (item == null || item.Transform == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(item.Transform.gameObject);
            }

            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(item.Transform.position);
            }
        }

        private PlayerItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index);
            return _items[_index];
        }

        private string CurrentKey()
        {
            PlayerItem item = CurrentItem();
            return item != null ? item.Key : null;
        }

        private void RestoreFocus(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Key == key)
                    {
                        _index = i;
                        return;
                    }
                }
            }

            _index = ClampIndex(_index);
        }

        private int ClampIndex(int index)
        {
            if (_items.Count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index >= _items.Count ? _items.Count - 1 : index;
        }

        private static void Close(AlertManager alert)
        {
            if (alert != null)
            {
                if (alert.playersT != null)
                {
                    alert.playersT.gameObject.SetActive(false);
                }

                alert.HideAlert();
            }
        }

        private static string PlayerLabel(AlertPlayer player, int slot)
        {
            string name = PlayerName(slot);
            List<string> parts = new List<string>();
            parts.Add(Loc.Get("players_popup_player", slot + 1, name));
            if (slot == 0)
            {
                parts.Add(Loc.Get("players_popup_host"));
            }

            string platform = PlatformText(slot);
            if (!string.IsNullOrWhiteSpace(platform))
            {
                parts.Add(Loc.Get("players_popup_platform", platform));
            }

            string ping = PingText(player, slot);
            if (!string.IsNullOrWhiteSpace(ping))
            {
                parts.Add(ping);
            }

            if (NetworkManager.Instance != null && NetworkManager.Instance.GetPlayerNickPosition(slot) != NetworkManager.Instance.GetPlayerNick())
            {
                string muteState = NetworkManager.Instance.IsPlayerMutedBySlot(slot) ? Loc.Get("players_popup_muted_state") : Loc.Get("players_popup_unmuted_state");
                parts.Add(muteState);
            }

            return string.Join(". ", parts.ToArray());
        }

        private static string PlayerName(int slot)
        {
            NetworkManager network = NetworkManager.Instance;
            if (network == null)
            {
                return Loc.Get("unknown_player");
            }

            string nick = network.GetPlayerNickPosition(slot);
            string real = network.GetPlayerNickReal(nick);
            string text = TextCleaner.ToSpeech(real);
            return string.IsNullOrWhiteSpace(text) ? Loc.Get("unknown_player") : text;
        }

        private static string PlatformText(int slot)
        {
            NetworkManager network = NetworkManager.Instance;
            if (network == null)
            {
                return string.Empty;
            }

            return TextCleaner.ToSpeech(network.GetPlatformString(network.GetPlayerNickPosition(slot)));
        }

        private static string PingText(AlertPlayer player, int slot)
        {
            if (player != null)
            {
                player.SetDescription();
                TMP_Text description = player.playerDescription;
                string text = description != null ? TextCleaner.ToSpeech(description.text) : string.Empty;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            NetworkManager network = NetworkManager.Instance;
            if (network != null && network.PlayerPing != null)
            {
                string nick = network.GetPlayerNickPosition(slot);
                if (!string.IsNullOrWhiteSpace(nick) && network.PlayerPing.ContainsKey(nick))
                {
                    return Loc.Get("players_popup_ping", network.PlayerPing[nick]);
                }
            }

            return string.Empty;
        }

        private static bool IsPlayersPopupOpen(AlertManager alert)
        {
            return alert != null
                && alert.IsActive()
                && alert.playersT != null
                && alert.playersT.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && Functions.TransformIsVisible(transform);
        }
    }
}
