using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for the multiplayer lobby screens.
    /// </summary>
    public sealed class LobbyHandler
    {
        private enum LobbyItemKind
        {
            Text,
            Button,
            RegionDropdown,
            CrossplayToggle,
            CreateNameInput,
            CreatePlayersDropdown,
            CreatePasswordInput,
            Toggle,
            RoomList,
            RoomSlot,
            KickPlayer
        }

        private sealed class LobbyItem
        {
            public LobbyItemKind Kind;
            public Transform Transform;
            public string Key;
            public string Label;
            public TMP_InputField Input;
            public TMP_Dropdown Dropdown;
            public Toggle Toggle;
            public RoomList Room;
            public int SlotIndex = -1;
        }

        private readonly List<LobbyItem> _items = new List<LobbyItem>();
        private int _index;
        private string _screen;
        private string _lastFocusKey;
        private float _lastRefreshTime;
        private bool _announcedScreen;
        private bool _queueNextFocusAnnouncement;

        /// <summary>
        /// Updates lobby navigation, focus speech, and activation.
        /// </summary>
        public bool Update()
        {
            LobbyManager lobby = LobbyManager.Instance;
            if (lobby == null || SceneStatic.GetSceneName() != "Lobby")
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Lobby);
            RefreshIfNeeded(lobby);
            AnnounceScreenOnce();

            if (_items.Count == 0)
            {
                return true;
            }

            ProcessKeys(lobby);
            AnnounceFocus();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _screen = null;
            _lastFocusKey = null;
            _lastRefreshTime = 0f;
            _announcedScreen = false;
            _queueNextFocusAnnouncement = false;
        }

        private void RefreshIfNeeded(LobbyManager lobby)
        {
            string currentScreen = GetScreen(lobby);
            if (currentScreen != _screen)
            {
                _screen = currentScreen;
                _index = 0;
                _lastFocusKey = null;
                _announcedScreen = false;
                Refresh(lobby);
                WarpToCurrent();
                _lastRefreshTime = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - _lastRefreshTime < 0.15f)
            {
                return;
            }

            string currentKey = CurrentKey();
            Refresh(lobby);
            RestoreFocus(currentKey);
            _lastRefreshTime = Time.unscaledTime;
        }

        private void Refresh(LobbyManager lobby)
        {
            _items.Clear();
            AddStatus(lobby);

            if (IsVisible(lobby.regions))
            {
                AddRegionItems(lobby);
            }

            if (IsVisible(lobby.CreateRoomT))
            {
                AddCreateItems(lobby);
            }

            if (IsVisible(lobby.JoinRoomT))
            {
                AddJoinItems(lobby);
            }

            if (IsVisible(lobby.RoomT))
            {
                AddRoomItems(lobby);
            }

            AddVisibleControllerButtons(lobby);
            _index = ClampIndex(_index);
        }

        private void AddStatus(LobbyManager lobby)
        {
            string status = Clean(lobby.statusTM != null ? lobby.statusTM.text : null);
            if (!string.IsNullOrWhiteSpace(status))
            {
                AddText("status", lobby.statusTM.transform, Loc.Get("lobby_status", status));
            }
        }

        private void AddRegionItems(LobbyManager lobby)
        {
            if (lobby.dropRegion != null && IsVisible(lobby.dropRegion.transform))
            {
                AddDropdown("region", lobby.dropRegion.transform, LobbyItemKind.RegionDropdown, lobby.dropRegion, Loc.Get("lobby_region", DropdownValue(lobby.dropRegion)));
            }

            if (lobby.UICrossPlay != null && IsVisible(lobby.UICrossPlay.transform))
            {
                string value = lobby.UICrossPlay.isOn ? Loc.Get("settings_on") : Loc.Get("settings_off");
                string label = lobby.UICrossPlay.interactable ? Loc.Get("lobby_crossplay", value) : Loc.Get("lobby_crossplay_locked", value);
                AddToggle("crossplay", lobby.UICrossPlay.transform, LobbyItemKind.CrossplayToggle, lobby.UICrossPlay, label);
            }

            AddVisibleButtonsFrom(lobby.regions, "region-button");
        }

        private void AddCreateItems(LobbyManager lobby)
        {
            if (lobby.UICreateName != null && IsVisible(lobby.UICreateName.transform))
            {
                AddInput("create-name", lobby.UICreateName.transform, LobbyItemKind.CreateNameInput, lobby.UICreateName, Loc.Get("lobby_create_name", InputValue(lobby.UICreateName)));
            }

            if (lobby.UICreatePlayers != null && IsVisible(lobby.UICreatePlayers.transform))
            {
                AddDropdown("create-players", lobby.UICreatePlayers.transform, LobbyItemKind.CreatePlayersDropdown, lobby.UICreatePlayers, Loc.Get("lobby_create_players", DropdownValue(lobby.UICreatePlayers)));
            }

            if (lobby.UITogglePwd != null && IsVisible(lobby.UITogglePwd.transform))
            {
                AddToggle("password-toggle", lobby.UITogglePwd.transform, LobbyItemKind.Toggle, lobby.UITogglePwd, Loc.Get("lobby_password_toggle", lobby.UITogglePwd.isOn ? Loc.Get("settings_on") : Loc.Get("settings_off")));
            }

            if (lobby.UICreatePwd != null && IsVisible(lobby.UICreatePwd.transform))
            {
                AddInput("create-password", lobby.UICreatePwd.transform, LobbyItemKind.CreatePasswordInput, lobby.UICreatePwd, Loc.Get("lobby_create_password", string.IsNullOrEmpty(lobby.UICreatePwd.text) ? Loc.Get("pdx_empty") : Loc.Get("pdx_entered")));
            }

            if (lobby.UIToggleLfm != null && IsVisible(lobby.UIToggleLfm.transform))
            {
                AddToggle("lfm-toggle", lobby.UIToggleLfm.transform, LobbyItemKind.Toggle, lobby.UIToggleLfm, Loc.Get("lobby_lfm_toggle", lobby.UIToggleLfm.isOn ? Loc.Get("settings_on") : Loc.Get("settings_off")));
            }

            AddVisibleButtonsFrom(lobby.CreateRoomT, "create-button");
        }

        private void AddJoinItems(LobbyManager lobby)
        {
            if (lobby.GridTransform != null && IsVisible(lobby.GridTransform))
            {
                int visibleRooms = 0;
                foreach (Transform child in lobby.GridTransform)
                {
                    RoomList room = child != null ? child.GetComponent<RoomList>() : null;
                    if (room == null || !IsVisible(room.transform))
                    {
                        continue;
                    }

                    visibleRooms++;
                    LobbyItem item = new LobbyItem();
                    item.Kind = LobbyItemKind.RoomList;
                    item.Transform = room.transform;
                    item.Room = room;
                    item.Key = "room:" + room.RoomName;
                    item.Label = Loc.Get("lobby_room_item", visibleRooms, RoomText(room));
                    _items.Add(item);
                }
            }

            AddVisibleButtonsFrom(lobby.JoinRoomT, "join-button");
        }

        private void AddRoomItems(LobbyManager lobby)
        {
            string title = Clean(lobby.roomTitle != null ? lobby.roomTitle.text : null);
            if (!string.IsNullOrWhiteSpace(title))
            {
                AddText("room-title", lobby.roomTitle.transform, title);
            }

            if (lobby.roomSlots != null)
            {
                for (int i = 0; i < lobby.roomSlots.Length; i++)
                {
                    TMP_Text slot = lobby.roomSlots[i];
                    if (slot == null || !slot.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    LobbyItem item = new LobbyItem();
                    item.Kind = LobbyItemKind.RoomSlot;
                    item.Transform = slot.transform;
                    item.Key = "slot:" + i + ":" + Clean(slot.text);
                    item.Label = Loc.Get("lobby_player_slot", i + 1, Clean(slot.text));
                    item.SlotIndex = i;
                    _items.Add(item);
                }
            }

            string waiting = Clean(lobby.roomWaiting != null ? lobby.roomWaiting.text : null);
            if (!string.IsNullOrWhiteSpace(waiting))
            {
                AddText("room-waiting", lobby.roomWaiting.transform, waiting);
            }

            if (lobby.roomSlotsKick != null)
            {
                for (int i = 0; i < lobby.roomSlotsKick.Length; i++)
                {
                    Transform kick = lobby.roomSlotsKick[i];
                    if (!IsVisible(kick))
                    {
                        continue;
                    }

                    LobbyItem item = new LobbyItem();
                    item.Kind = LobbyItemKind.KickPlayer;
                    item.Transform = kick;
                    item.Key = "kick:" + i;
                    item.Label = Loc.Get("lobby_kick_player", i + 1);
                    item.SlotIndex = i;
                    _items.Add(item);
                }
            }

            AddSpecialButton("launch", lobby.buttonLaunch, Loc.Get("lobby_launch_game"));
            AddSpecialButton("steam", lobby.buttonSteam, Loc.Get("lobby_invite_steam"));
            AddVisibleButtonsFrom(lobby.RoomT, "room-button");
        }

        private void AddVisibleControllerButtons(LobbyManager lobby)
        {
            if (lobby.buttonsController == null)
            {
                return;
            }

            for (int i = 0; i < lobby.buttonsController.Length; i++)
            {
                Transform transform = lobby.buttonsController[i];
                if (!IsVisible(transform))
                {
                    continue;
                }

                AddButton("controller:" + i + ":" + transform.name, transform, ButtonText(transform));
            }
        }

        private void AddVisibleButtonsFrom(Transform root, string prefix)
        {
            if (!IsVisible(root))
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null && IsVisible(button.transform))
                {
                    AddButton(prefix + ":button:" + i + ":" + button.name, button.transform, ButtonText(button.transform));
                }
            }

            BotonGeneric[] genericButtons = root.GetComponentsInChildren<BotonGeneric>(false);
            for (int i = 0; i < genericButtons.Length; i++)
            {
                BotonGeneric button = genericButtons[i];
                if (button != null && IsVisible(button.transform))
                {
                    AddButton(prefix + ":generic:" + i + ":" + button.name, button.transform, ButtonText(button.transform));
                }
            }

            BotonEndTurn[] endButtons = root.GetComponentsInChildren<BotonEndTurn>(false);
            for (int i = 0; i < endButtons.Length; i++)
            {
                BotonEndTurn button = endButtons[i];
                if (button != null && IsVisible(button.transform))
                {
                    AddButton(prefix + ":end:" + i + ":" + button.name, button.transform, ButtonText(button.transform));
                }
            }
        }

        private void AddSpecialButton(string key, Transform transform, string label)
        {
            if (IsVisible(transform))
            {
                AddButton(key, transform, label);
            }
        }

        private void AddText(string key, Transform transform, string label)
        {
            if (transform == null || ContainsTransform(transform) || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            LobbyItem item = new LobbyItem();
            item.Kind = LobbyItemKind.Text;
            item.Transform = transform;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void AddInput(string key, Transform transform, LobbyItemKind kind, TMP_InputField input, string label)
        {
            if (transform == null || input == null || ContainsTransform(transform))
            {
                return;
            }

            LobbyItem item = new LobbyItem();
            item.Kind = kind;
            item.Transform = transform;
            item.Input = input;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void AddDropdown(string key, Transform transform, LobbyItemKind kind, TMP_Dropdown dropdown, string label)
        {
            if (transform == null || dropdown == null || ContainsTransform(transform))
            {
                return;
            }

            LobbyItem item = new LobbyItem();
            item.Kind = kind;
            item.Transform = transform;
            item.Dropdown = dropdown;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void AddToggle(string key, Transform transform, LobbyItemKind kind, Toggle toggle, string label)
        {
            if (transform == null || toggle == null || ContainsTransform(transform))
            {
                return;
            }

            LobbyItem item = new LobbyItem();
            item.Kind = kind;
            item.Transform = transform;
            item.Toggle = toggle;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void AddButton(string key, Transform transform, string label)
        {
            if (transform == null || ContainsTransform(transform))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = Clean(transform.name);
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            LobbyItem item = new LobbyItem();
            item.Kind = LobbyItemKind.Button;
            item.Transform = transform;
            item.Key = key;
            item.Label = label;
            _items.Add(item);
        }

        private void ProcessKeys(LobbyManager lobby)
        {
            bool textFocused = TextInputFocusHelper.IsTextInputFocused();
            if (textFocused && !Input.GetKeyDown(KeyCode.UpArrow) && !Input.GetKeyDown(KeyCode.DownArrow))
            {
                return;
            }

            if (textFocused)
            {
                DeactivateFocusedInput();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) && AdjustCurrent(-1))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) && AdjustCurrent(1))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                MoveToEdge(false);
            }
            else if (Input.GetKeyDown(KeyCode.End))
            {
                MoveToEdge(true);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(-1);
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                Move(1);
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateCurrent(lobby);
            }
        }

        private bool AdjustCurrent(int delta)
        {
            LobbyItem item = CurrentItem();
            if (item == null || item.Dropdown == null || item.Dropdown.options == null || item.Dropdown.options.Count == 0 || !item.Dropdown.interactable)
            {
                return false;
            }

            int next = item.Dropdown.value + delta;
            if (next < 0 || next >= item.Dropdown.options.Count)
            {
                return true;
            }

            item.Dropdown.value = next;
            item.Dropdown.RefreshShownValue();
            Refresh(LobbyManager.Instance);
            AnnounceFocus(force: true);
            return true;
        }

        private void Move(int delta)
        {
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            WarpToCurrent();
            AnnounceFocus(force: true);
        }

        private void MoveToEdge(bool end)
        {
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            WarpToCurrent();
            AnnounceFocus(force: true);
        }

        private void ActivateCurrent(LobbyManager lobby)
        {
            LobbyItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            if (item.Input != null && item.Input.interactable)
            {
                FocusInput(item.Input, item.Label);
                return;
            }

            if (item.Dropdown != null && item.Dropdown.interactable)
            {
                ScreenReader.Say(Loc.Get("pdx_dropdown_hint", item.Label));
                return;
            }

            if (item.Toggle != null)
            {
                if (!item.Toggle.interactable)
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Label));
                    return;
                }

                item.Toggle.isOn = !item.Toggle.isOn;
                if (item.Kind == LobbyItemKind.CrossplayToggle)
                {
                    lobby.SetCrossPlayEnabled(item.Toggle.isOn);
                }

                Refresh(lobby);
                AnnounceFocus(force: true);
                return;
            }

            if (item.Kind == LobbyItemKind.RoomList && item.Room != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                item.Room.JoinRoom();
                _lastFocusKey = null;
                return;
            }

            if (item.Kind == LobbyItemKind.KickPlayer && item.SlotIndex > -1)
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                lobby.KickPlayer(item.SlotIndex);
                _lastFocusKey = null;
                return;
            }

            if (item.Kind == LobbyItemKind.Text)
            {
                ScreenReader.Say(item.Label);
                return;
            }

            if (TryActivateKnownButton(lobby, item))
            {
                _lastFocusKey = null;
                return;
            }

            ScreenReader.Say(Loc.Get("no_menu_item"));
        }

        private bool TryActivateKnownButton(LobbyManager lobby, LobbyItem item)
        {
            Transform transform = item.Transform;
            if (transform == null)
            {
                return false;
            }

            Button button = transform.GetComponent<Button>() ?? transform.GetComponentInParent<Button>();
            if (button != null)
            {
                if (!button.interactable)
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Label));
                    return true;
                }

                ScreenReader.Say(Loc.Get("activated", item.Label));
                button.onClick.Invoke();
                return true;
            }

            string action = (transform.name + " " + item.Key + " " + item.Label).ToLowerInvariant();
            if (transform == lobby.buttonLaunch || action.Contains("launch"))
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                lobby.LaunchGame();
                return true;
            }

            if (transform == lobby.buttonSteam || action.Contains("steam"))
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                lobby.InviteSteam();
                return true;
            }

            if (transform == lobby.regionsDisconnect || transform.IsChildOf(lobby.regionsDisconnect))
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                lobby.DisconnectRegion(true);
                return true;
            }

            if (lobby.regions != null && transform.IsChildOf(lobby.regions) && action.Contains("connect"))
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                lobby.SelectRegion();
                return true;
            }

            BotonEndTurn endTurn = transform.GetComponent<BotonEndTurn>() ?? transform.GetComponentInParent<BotonEndTurn>();
            if (endTurn != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                endTurn.OnMouseUp();
                return true;
            }

            BotonGeneric generic = transform.GetComponent<BotonGeneric>() ?? transform.GetComponentInParent<BotonGeneric>();
            if (generic != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Label));
                generic.Clicked();
                return true;
            }

            return false;
        }

        private void AnnounceScreenOnce()
        {
            if (_announcedScreen)
            {
                return;
            }

            _announcedScreen = true;
            _queueNextFocusAnnouncement = true;
            ScreenReader.Say(Loc.Get("lobby_screen_" + (_screen ?? "unknown")));
        }

        private void AnnounceFocus(bool force = false)
        {
            LobbyItem item = CurrentItem();
            if (item == null)
            {
                return;
            }

            string key = item.Key + ":" + item.Label;
            if (!force && key == _lastFocusKey)
            {
                return;
            }

            _lastFocusKey = key;
            string text = Loc.Get("lobby_item", item.Label);
            if (_queueNextFocusAnnouncement)
            {
                _queueNextFocusAnnouncement = false;
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private LobbyItem CurrentItem()
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
            LobbyItem item = CurrentItem();
            return item != null ? item.Key : null;
        }

        private void RestoreFocus(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _index = ClampIndex(_index);
                return;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Key == key)
                {
                    _index = i;
                    return;
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

            if (index >= _items.Count)
            {
                return _items.Count - 1;
            }

            return index;
        }

        private void WarpToCurrent()
        {
            LobbyItem item = CurrentItem();
            if (item == null || item.Transform == null || Mouse.current == null || GameManager.Instance == null || GameManager.Instance.cameraMain == null)
            {
                return;
            }

            Vector3 screen = GameManager.Instance.cameraMain.WorldToScreenPoint(item.Transform.position);
            Mouse.current.WarpCursorPosition(screen);
        }

        private static void FocusInput(TMP_InputField input, string text)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(input.gameObject);
            }

            input.ActivateInputField();
            ScreenReader.Say(Loc.Get("pdx_input_focused", string.IsNullOrWhiteSpace(text) ? Loc.Get("pdx_text_field") : text));
        }

        private static void DeactivateFocusedInput()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return;
            }

            TMP_InputField input = EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>();
            if (input != null && input.isFocused)
            {
                input.DeactivateInputField();
            }
        }

        private static string GetScreen(LobbyManager lobby)
        {
            if (IsVisible(lobby.RoomT))
            {
                return "room";
            }

            if (IsVisible(lobby.CreateRoomT))
            {
                return "create";
            }

            if (IsVisible(lobby.JoinRoomT))
            {
                return "join";
            }

            if (IsVisible(lobby.regions))
            {
                return "region";
            }

            return "unknown";
        }

        private static string RoomText(RoomList room)
        {
            List<string> parts = new List<string>();
            AddPart(parts, Clean(room._Name != null ? room._Name.text : null));
            AddPart(parts, Clean(room._Creator != null ? room._Creator.text : null));
            AddPart(parts, Clean(room._Players != null ? room._Players.text : null));
            AddPart(parts, Clean(room._Version != null ? room._Version.text : null));
            if (room._Lock != null && room._Lock.gameObject.activeInHierarchy)
            {
                AddPart(parts, Loc.Get("lobby_password_required"));
            }

            return parts.Count == 0 ? room.RoomName : string.Join(". ", parts.ToArray());
        }

        private static string ButtonText(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            BotonGeneric generic = transform.GetComponent<BotonGeneric>() ?? transform.GetComponentInParent<BotonGeneric>();
            if (generic != null)
            {
                string text = Clean(generic.GetText());
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = Clean(GameText.Get(generic.idTranslate));
                }

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            TMP_Text textComponent = transform.GetComponentInChildren<TMP_Text>(false);
            if (textComponent != null)
            {
                string text = Clean(textComponent.text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return KnownButtonName(transform.name);
        }

        private static string KnownButtonName(string name)
        {
            switch (name)
            {
            case "ButtonMultiplayerCreate":
                return Loc.Get("lobby_create_button");
            case "ButtonMultiplayerJoin":
                return Loc.Get("lobby_join_button");
            case "ButtonMultiplayerBack":
                return Loc.Get("pre_run_close");
            case "SetReady":
                return Loc.Get("lobby_ready_button");
            case "AllUnready":
                return Loc.Get("lobby_all_unready_button");
            default:
                return Clean(name);
            }
        }

        private static string DropdownValue(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0 || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
            {
                return Loc.Get("pdx_empty");
            }

            return Clean(dropdown.options[dropdown.value].text);
        }

        private static string InputValue(TMP_InputField input)
        {
            string text = input != null ? Clean(input.text) : string.Empty;
            return string.IsNullOrWhiteSpace(text) ? Loc.Get("pdx_empty") : text;
        }

        private static void AddPart(List<string> parts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
            {
                parts.Add(text);
            }
        }

        private bool ContainsTransform(Transform transform)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                Transform existing = _items[i].Transform;
                if (existing == transform || (existing != null && transform != null && (existing.IsChildOf(transform) || transform.IsChildOf(existing))))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && Functions.TransformIsVisible(transform);
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }

    [HarmonyPatch(typeof(LobbyManager), "CreateRoom")]
    internal static class LobbyCreateRoomPatch
    {
        private static bool Prefix()
        {
            if (!TextInputFocusHelper.IsTextInputFocused())
            {
                return true;
            }

            ScreenReader.Say(Loc.Get("lobby_input_submit_blocked"));
            return false;
        }
    }
}
