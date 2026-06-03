using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AccessTheObelisk
{
    /// <summary>
    /// Announces and activates the existing main menu navigation.
    /// </summary>
    public sealed class MainMenuHandler
    {
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private PointerEventData _pointerEventData;
        private string _lastAnnouncement;
        private GameObject _lastObject;
        private string _lastMenuScreen;
        private string _pendingInitialFocusScreen;
        private string _manualFocusScreen;
        private int _manualFocusIndex = -1;
        private bool _manualFocusAnnouncedThisFrame;
        private bool _initializedMenuFocus;
        private float _lastPollTime;
        private float _suppressFocusUntil;

        /// <summary>
        /// Updates main menu keyboard support and focus announcements.
        /// </summary>
        public void Update()
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (menu == null || EventSystem.current == null)
            {
                _initializedMenuFocus = false;
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsTutorialActive())
            {
                return;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return;
            }

            if (!IsMenuReady(menu))
            {
                _initializedMenuFocus = false;
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            EnsurePointerData();
            AccessStateManager.SetState(AccessState.MainMenu);
            EnsureInitialFocus(menu);
            TrackMenuScreen(menu);
            EnsureSubmenuFocus(menu);
            ProcessKeys(menu);
            PollFocusAnnouncement(menu);
        }

        private void EnsurePointerData()
        {
            if (_pointerEventData == null)
            {
                _pointerEventData = new PointerEventData(EventSystem.current);
            }
        }

        private void EnsureInitialFocus(MainMenuManager menu)
        {
            if (_initializedMenuFocus)
            {
                return;
            }

            Transform first = FindFirstVisibleMenuTransform(menu);
            if (first == null)
            {
                return;
            }

            _initializedMenuFocus = true;
            ScreenReader.Say(Loc.Get("main_menu_loaded"));
            WarpMouseTo(first.position);
        }

        private void TrackMenuScreen(MainMenuManager menu)
        {
            string currentScreen = GetMenuScreen(menu);
            if (currentScreen == _lastMenuScreen)
            {
                return;
            }

            _lastMenuScreen = currentScreen;
            _lastObject = null;
            _lastAnnouncement = null;
            _manualFocusScreen = null;
            _manualFocusIndex = -1;

            if (currentScreen == "GameMode")
            {
                _pendingInitialFocusScreen = currentScreen;
                _manualFocusScreen = currentScreen;
                _manualFocusIndex = 0;
                ScreenReader.Say(Loc.Get("game_mode_screen"));
            }
            else if (currentScreen == "Save")
            {
                _pendingInitialFocusScreen = currentScreen;
                _manualFocusScreen = currentScreen;
                _manualFocusIndex = 0;
                ScreenReader.Say(Loc.Get("save_slot_screen"));
            }
            else if (currentScreen == "Profiles")
            {
                _pendingInitialFocusScreen = currentScreen;
                _manualFocusScreen = currentScreen;
                _manualFocusIndex = 0;
                ScreenReader.Say(Loc.Get("profile_screen"));
            }
            else if (currentScreen == "Main")
            {
                _manualFocusScreen = currentScreen;
                _manualFocusIndex = 0;
            }
        }

        private static string GetMenuScreen(MainMenuManager menu)
        {
            if (menu.IsGameModesActive())
            {
                return "GameMode";
            }

            if (menu.IsSaveMenuActive())
            {
                return "Save";
            }

            if (IsProfilesOpen(menu))
            {
                return "Profiles";
            }

            return "Main";
        }

        private bool IsMenuReady(MainMenuManager menu)
        {
            string screen = GetMenuScreen(menu);
            if (screen == "GameMode" || screen == "Save" || screen == "Profiles" || screen == "Main")
            {
                return BuildPrimaryControllerList(menu).Count > 0;
            }

            return FindFirstVisibleMenuTransform(menu) != null;
        }

        private void EnsureSubmenuFocus(MainMenuManager menu)
        {
            if (string.IsNullOrEmpty(_pendingInitialFocusScreen) || _pendingInitialFocusScreen != GetMenuScreen(menu))
            {
                return;
            }

            Transform first = FindFirstVisibleControllerTransform(menu);
            if (first == null)
            {
                return;
            }

            WarpMouseTo(first.position);
            _pendingInitialFocusScreen = null;
            _suppressFocusUntil = Time.unscaledTime + 0.08f;
        }

        private Transform FindFirstVisibleMenuTransform(MainMenuManager menu)
        {
            if (menu.menuOps != null)
            {
                for (int i = 0; i < menu.menuOps.Length; i++)
                {
                    Transform candidate = menu.menuOps[i];
                    if (IsReadableTransform(candidate))
                    {
                        return candidate;
                    }
                }
            }

            Button[] buttons = menu.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].gameObject.activeInHierarchy)
                {
                    return buttons[i].transform;
                }
            }

            return null;
        }

        private static bool IsReadableTransform(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy && Functions.TransformIsVisible(transform);
        }

        private void ProcessKeys(MainMenuManager menu)
        {
            bool handledMovement = false;
            _manualFocusAnnouncedThisFrame = false;
            if (IsFocusedTextInput() && (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)))
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) && TryAdjustPdxDropdown(menu, -1))
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) && TryAdjustPdxDropdown(menu, 1))
            {
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                handledMovement = JumpManualFocus(menu, false);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                handledMovement = JumpManualFocus(menu, true);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                handledMovement = Move(menu, goingUp: true);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                handledMovement = Move(menu, goingDown: true);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                handledMovement = Move(menu, goingLeft: true);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                handledMovement = Move(menu, goingRight: true);
            }

            if (handledMovement)
            {
                if (_manualFocusAnnouncedThisFrame)
                {
                    _suppressFocusUntil = Time.unscaledTime + 0.35f;
                }
                else
                {
                    _suppressFocusUntil = Time.unscaledTime + 0.2f;
                }
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateCurrent();
            }
        }

        private bool Move(MainMenuManager menu, bool goingUp = false, bool goingRight = false, bool goingDown = false, bool goingLeft = false)
        {
            if (MoveManualFocus(menu, goingUp, goingRight, goingDown, goingLeft))
            {
                return true;
            }

            if (GameManager.Instance != null && GameManager.Instance.ConfigKeyboardShortcuts)
            {
                return false;
            }

            menu.ControllerMovement(goingUp, goingRight, goingDown, goingLeft);
            return true;
        }

        private bool MoveManualFocus(MainMenuManager menu, bool goingUp, bool goingRight, bool goingDown, bool goingLeft)
        {
            if (_manualFocusScreen != "GameMode" && _manualFocusScreen != "Save" && _manualFocusScreen != "Profiles" && _manualFocusScreen != "Main")
            {
                return false;
            }

            if (_manualFocusScreen != GetMenuScreen(menu))
            {
                return false;
            }

            List<Transform> candidates = BuildPrimaryControllerList(menu);
            if (candidates.Count == 0)
            {
                return false;
            }

            if (_manualFocusIndex < 0 || _manualFocusIndex >= candidates.Count)
            {
                _manualFocusIndex = 0;
            }

            if (candidates.Count == 1 && (goingLeft || goingUp || goingRight || goingDown))
            {
                WarpMouseTo(candidates[_manualFocusIndex].position);
                _lastObject = null;
                _lastAnnouncement = null;
                AnnounceObject(candidates[_manualFocusIndex].gameObject);
                _manualFocusAnnouncedThisFrame = true;
                return true;
            }

            int nextIndex = _manualFocusIndex;
            if (goingLeft || goingUp)
            {
                nextIndex--;
            }
            else if (goingRight || goingDown)
            {
                nextIndex++;
            }
            else
            {
                return false;
            }

            if (nextIndex < 0 || nextIndex >= candidates.Count)
            {
                return true;
            }

            _manualFocusIndex = nextIndex;
            DeactivateFocusedInput();
            WarpMouseTo(candidates[_manualFocusIndex].position);
            AnnounceObject(candidates[_manualFocusIndex].gameObject);
            _manualFocusAnnouncedThisFrame = true;
            return true;
        }

        private bool JumpManualFocus(MainMenuManager menu, bool end)
        {
            if (_manualFocusScreen != "GameMode" && _manualFocusScreen != "Save" && _manualFocusScreen != "Profiles" && _manualFocusScreen != "Main")
            {
                return false;
            }

            if (_manualFocusScreen != GetMenuScreen(menu))
            {
                return false;
            }

            List<Transform> candidates = BuildPrimaryControllerList(menu);
            if (candidates.Count == 0)
            {
                return false;
            }

            if (!NavigationBounds.TryJump(ref _manualFocusIndex, end, candidates.Count))
            {
                return false;
            }

            DeactivateFocusedInput();
            WarpMouseTo(candidates[_manualFocusIndex].position);
            AnnounceObject(candidates[_manualFocusIndex].gameObject);
            _manualFocusAnnouncedThisFrame = true;
            return true;
        }

        private bool TryAdjustPdxDropdown(MainMenuManager menu, int delta)
        {
            GameObject current = GetManualFocusObject(menu);
            if (current == null)
            {
                return false;
            }

            TMP_Dropdown dropdown = current.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                dropdown = current.GetComponentInParent<TMP_Dropdown>();
            }

            if (dropdown == null || !dropdown.gameObject.activeInHierarchy || dropdown.options == null || dropdown.options.Count == 0)
            {
                return false;
            }

            int next = dropdown.value + delta;
            if (next < 0 || next >= dropdown.options.Count)
            {
                return true;
            }

            dropdown.value = next;
            dropdown.RefreshShownValue();
            AnnounceObject(dropdown.gameObject);
            _suppressFocusUntil = Time.unscaledTime + 0.35f;
            return true;
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

        private static bool IsFocusedTextInput()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }

            TMP_InputField input = EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return input != null && input.isFocused;
        }

        private void PollFocusAnnouncement(MainMenuManager menu)
        {
            if (Time.unscaledTime - _lastPollTime < 0.08f)
            {
                return;
            }

            if (Time.unscaledTime < _suppressFocusUntil)
            {
                return;
            }

            _lastPollTime = Time.unscaledTime;
            AnnounceCurrentIfChanged(menu);
        }

        private void AnnounceCurrentIfChanged(MainMenuManager menu)
        {
            GameObject current = GetCurrentFocusObject(menu);
            if (current == null)
            {
                return;
            }

            AnnounceObject(current);
        }

        private void AnnounceObject(GameObject current)
        {
            string text = GetReadableText(current);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            Button button = current.GetComponentInParent<Button>();
            bool unavailable = button != null && !button.interactable;
            string announcement = unavailable ? Loc.Get("menu_item_unavailable", text) : Loc.Get("menu_item", text);
            if (current == _lastObject && announcement == _lastAnnouncement)
            {
                return;
            }

            _lastObject = current;
            _lastAnnouncement = announcement;
            ScreenReader.Say(announcement);
        }

        private void ActivateCurrent()
        {
            MainMenuManager menu = MainMenuManager.Instance;
            GameObject current = menu != null ? GetCurrentFocusObject(menu) : GetCurrentRaycastObject();
            if (current == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            string text = GetReadableText(current);
            TMP_InputField input = current.GetComponent<TMP_InputField>();
            if (input == null)
            {
                input = current.GetComponentInParent<TMP_InputField>();
            }

            if (input != null && input.interactable)
            {
                FocusInput(input, text);
                return;
            }

            TMP_Dropdown dropdown = current.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                dropdown = current.GetComponentInParent<TMP_Dropdown>();
            }

            if (dropdown != null && dropdown.interactable)
            {
                ScreenReader.Say(Loc.Get("pdx_dropdown_hint", string.IsNullOrWhiteSpace(text) ? DropdownValue(dropdown) : text));
                return;
            }

            if (TryActivatePdxButton(menu, current, text))
            {
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            int profileSlot = GetProfileSlot(current);
            if (profileSlot >= 0 && menu != null)
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? Loc.Get("profile_slot", profileSlot + 1) : text));
                menu.UseProfile(profileSlot);
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            if (IsProfileDeleteAction(current) && menu != null)
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? Loc.Get("profile_delete") : text));
                menu.DeleteProfile();
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            if (IsSaveDeleteAction(current))
            {
                MenuSaveButton saveDeleteButton = current.GetComponentInParent<MenuSaveButton>();
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? SaveDeleteLabel() : text));
                saveDeleteButton.DeleteThis();
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            Button button = current.GetComponentInParent<Button>();
            if (button != null && button.interactable)
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "button" : text));
                button.onClick.Invoke();
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            Toggle toggle = current.GetComponentInParent<Toggle>();
            if (toggle != null && toggle.interactable)
            {
                toggle.isOn = !toggle.isOn;
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "toggle" : text));
                return;
            }

            BotonMenuGameMode gameMode = current.GetComponentInParent<BotonMenuGameMode>();
            if (gameMode == null)
            {
                gameMode = current.GetComponentInChildren<BotonMenuGameMode>();
            }

            if (gameMode != null)
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "game mode" : text));
                gameMode.OnMouseUp();
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            MenuSaveButton saveButton = current.GetComponentInParent<MenuSaveButton>();
            if (saveButton == null)
            {
                saveButton = current.GetComponentInChildren<MenuSaveButton>();
            }

            if (saveButton != null)
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "save slot" : text));
                saveButton.SelectThis();
                _lastObject = null;
                _lastAnnouncement = null;
                return;
            }

            ScreenReader.Say(Loc.Get("no_menu_item"));
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

        private GameObject GetCurrentFocusObject(MainMenuManager menu)
        {
            GameObject manual = GetManualFocusObject(menu);
            if (manual != null)
            {
                return manual;
            }

            if (menu.IsGameModesActive() || menu.IsSaveMenuActive() || IsProfilesOpen(menu))
            {
                Transform controllerTransform = GetCurrentControllerTransform(menu);
                if (controllerTransform != null)
                {
                    return controllerTransform.gameObject;
                }
            }

            return GetCurrentRaycastObject();
        }

        private GameObject GetManualFocusObject(MainMenuManager menu)
        {
            if (_manualFocusScreen != "GameMode" && _manualFocusScreen != "Save" && _manualFocusScreen != "Profiles" && _manualFocusScreen != "Main")
            {
                return null;
            }

            if (_manualFocusScreen != GetMenuScreen(menu))
            {
                return null;
            }

            List<Transform> candidates = BuildPrimaryControllerList(menu);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (_manualFocusIndex < 0 || _manualFocusIndex >= candidates.Count)
            {
                _manualFocusIndex = 0;
            }

            return candidates[_manualFocusIndex].gameObject;
        }

        private Transform GetCurrentControllerTransform(MainMenuManager menu)
        {
            List<Transform> candidates = BuildCurrentControllerList(menu);
            if (candidates.Count == 0)
            {
                return null;
            }

            int index = Functions.GetListClosestIndexToMousePosition(candidates, checkUiItems: true);
            if (index < 0 || index >= candidates.Count)
            {
                return null;
            }

            return candidates[index];
        }

        private Transform FindFirstVisibleControllerTransform(MainMenuManager menu)
        {
            List<Transform> candidates = BuildCurrentControllerList(menu);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        private static List<Transform> BuildCurrentControllerList(MainMenuManager menu)
        {
            List<Transform> candidates = BuildPrimaryControllerList(menu);

            AddVisible(candidates, menu.menuController1);
            return candidates;
        }

        private static List<Transform> BuildPrimaryControllerList(MainMenuManager menu)
        {
            List<Transform> candidates = new List<Transform>();

            if (menu.IsSaveMenuActive())
            {
                AddSaveButtons(candidates, menu);
            }
            else if (menu.IsGameModesActive())
            {
                AddGameModeSelections(candidates, menu);
            }
            else if (IsProfilesOpen(menu))
            {
                AddProfileSelections(candidates, menu);
            }
            else
            {
                AddMainMenuSelections(candidates, menu);
            }

            return candidates;
        }

        private static bool IsProfilesOpen(MainMenuManager menu)
        {
            if (menu == null || menu.profilesT == null || !Functions.TransformIsVisible(menu.profilesT))
            {
                return false;
            }

            if (menu.menuT != null && Functions.TransformIsVisible(menu.menuT))
            {
                return false;
            }

            if (menu.profileOps != null)
            {
                for (int i = 0; i < menu.profileOps.Length; i++)
                {
                    if (Functions.TransformIsVisible(menu.profileOps[i]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddGameModeSelections(List<Transform> target, MainMenuManager menu)
        {
            AddVisibleTransform(target, GetReadableChild(menu.gameModeSelection0));
            AddVisibleTransform(target, GetReadableChild(menu.gameModeSelection1));
            AddVisibleTransform(target, GetReadableChild(menu.gameModeSelection2));
            AddVisibleTransform(target, GetReadableChild(menu.gameModeSelection3));
        }

        private static Transform GetReadableChild(Transform parent)
        {
            if (parent == null)
            {
                return null;
            }

            BotonMenuGameMode gameMode = parent.GetComponentInChildren<BotonMenuGameMode>(false);
            return gameMode != null ? gameMode.transform : parent;
        }

        private static void AddMainMenuSelections(List<Transform> target, MainMenuManager menu)
        {
            if (menu.menuOps != null)
            {
                for (int i = 0; i < menu.menuOps.Length; i++)
                {
                    AddVisibleTransform(target, menu.menuOps[i]);
                }
            }

            AddPdxSelections(target, menu);
        }

        private static void AddPdxSelections(List<Transform> target, MainMenuManager menu)
        {
            AddVisible(target, menu.menuControllerPDX);
            AddVisibleTransform(target, menu.paradoxLoginUser != null ? menu.paradoxLoginUser.transform : null);
            AddVisibleTransform(target, menu.paradoxLoginPassword != null ? menu.paradoxLoginPassword.transform : null);
            AddVisibleTransform(target, menu.paradoxCreateEmail != null ? menu.paradoxCreateEmail.transform : null);
            AddVisibleTransform(target, menu.paradoxCreatePassword != null ? menu.paradoxCreatePassword.transform : null);
            AddVisibleTransform(target, menu.paradoxDropdownRegion != null ? menu.paradoxDropdownRegion.transform : null);
            AddVisibleTransform(target, menu.paradoxDropdownDay != null ? menu.paradoxDropdownDay.transform : null);
            AddVisibleTransform(target, menu.paradoxDropdownMonth != null ? menu.paradoxDropdownMonth.transform : null);
            AddVisibleTransform(target, menu.paradoxDropdownYear != null ? menu.paradoxDropdownYear.transform : null);
            AddVisibleTransform(target, menu.paradoxCreateOffers != null ? menu.paradoxCreateOffers.transform : null);
            AddVisibleButtonsFrom(target, menu.paradoxT);
        }

        private static void AddVisibleButtonsFrom(List<Transform> target, Transform root)
        {
            if (root == null || !Functions.TransformIsVisible(root))
            {
                return;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button != null)
                {
                    AddVisibleTransform(target, button.transform);
                }
            }
        }

        private static void AddSaveButtons(List<Transform> target, MainMenuManager menu)
        {
            if (menu.menuSaveButtons == null)
            {
                return;
            }

            for (int i = 0; i < menu.menuSaveButtons.Length; i++)
            {
                MenuSaveButton saveButton = menu.menuSaveButtons[i];
                if (saveButton != null)
                {
                    AddVisibleTransform(target, saveButton.transform);
                    AddSaveDeleteAction(target, saveButton);
                }
            }

            AddVisibleTransform(target, menu.exitSaveGameButton);
        }

        private static void AddProfileSelections(List<Transform> target, MainMenuManager menu)
        {
            if (menu.profileOps != null)
            {
                for (int i = 0; i < menu.profileOps.Length; i++)
                {
                    AddVisibleTransform(target, menu.profileOps[i]);
                }
            }

            AddVisibleTransform(target, menu.profileDelete);
            AddVisibleTransform(target, menu.exitT);
        }

        private static void AddSaveDeleteAction(List<Transform> target, MenuSaveButton saveButton)
        {
            if (!HasOccupiedSave(saveButton) || saveButton.deleteButton == null || target.Contains(saveButton.deleteButton))
            {
                return;
            }

            target.Add(saveButton.deleteButton);
        }

        private static void AddVisibleTransform(List<Transform> target, Transform candidate)
        {
            if (candidate != null && Functions.TransformIsVisible(candidate) && !target.Contains(candidate) && HasReadableText(candidate.gameObject))
            {
                target.Add(candidate);
            }
        }

        private static bool HasReadableText(GameObject current)
        {
            return !string.IsNullOrWhiteSpace(GetReadableText(current));
        }

        private static void AddVisible(List<Transform> target, List<Transform> source)
        {
            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Transform candidate = source[i];
                AddVisibleTransform(target, candidate);
            }
        }

        private GameObject GetCurrentRaycastObject()
        {
            _pointerEventData.position = UnityEngine.Input.mousePosition;
            _raycastResults.Clear();
            EventSystem.current.RaycastAll(_pointerEventData, _raycastResults);

            for (int i = 0; i < _raycastResults.Count; i++)
            {
                GameObject item = _raycastResults[i].gameObject;
                if (item == null)
                {
                    continue;
                }

                if (item.GetComponentInParent<Button>() != null ||
                    item.GetComponentInParent<Toggle>() != null ||
                    item.GetComponentInParent<MenuButton>() != null ||
                    item.GetComponentInParent<BotonGeneric>() != null ||
                    item.GetComponentInParent<TMP_Text>() != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static string GetReadableText(GameObject current)
        {
            if (IsSaveDeleteAction(current))
            {
                MenuSaveButton saveButtonDelete = current.GetComponentInParent<MenuSaveButton>();
                string saveText = GetSaveButtonText(saveButtonDelete, GetSaveButtonPosition(saveButtonDelete));
                return Loc.Get("save_slot_delete", SaveDeleteLabel(), saveText);
            }

            BotonMenuGameMode gameMode = current.GetComponentInParent<BotonMenuGameMode>();
            if (gameMode == null)
            {
                gameMode = current.GetComponentInChildren<BotonMenuGameMode>();
            }

            if (gameMode != null && gameMode.optionText != null)
            {
                string title = Clean(gameMode.optionText.text);
                return AppendDescription(title, GetGameModeDescription(gameMode));
            }

            MenuSaveButton saveButton = current.GetComponentInParent<MenuSaveButton>();
            if (saveButton == null)
            {
                saveButton = current.GetComponentInChildren<MenuSaveButton>();
            }

            if (saveButton != null)
            {
                return GetSaveButtonText(saveButton, GetSaveButtonPosition(saveButton));
            }

            string pdxText = GetPdxText(current);
            if (!string.IsNullOrWhiteSpace(pdxText))
            {
                return pdxText;
            }

            int profileSlot = GetProfileSlot(current);
            if (profileSlot >= 0)
            {
                return GetProfileText(profileSlot);
            }

            if (IsProfileDeleteAction(current))
            {
                MainMenuManager menu = MainMenuManager.Instance;
                if (menu != null && menu.profileDeleteText != null)
                {
                    string deleteText = Clean(menu.profileDeleteText.text);
                    return string.IsNullOrWhiteSpace(deleteText) ? Loc.Get("profile_delete") : deleteText;
                }

                return Loc.Get("profile_delete");
            }

            MenuButton menuButton = current.GetComponentInParent<MenuButton>();
            if (menuButton != null && menuButton.buttonText != null)
            {
                return Clean(menuButton.buttonText.text);
            }

            BotonGeneric botonGeneric = current.GetComponentInParent<BotonGeneric>();
            if (botonGeneric != null)
            {
                string title = Clean(botonGeneric.GetText());
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Clean(GameText.Get(botonGeneric.idTranslate));
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Clean(FindRelatedTitleText(botonGeneric.idTranslate));
                }

                return AppendDescription(title, GetBotonGenericDescription(botonGeneric));
            }

            TMP_Text text = current.GetComponentInChildren<TMP_Text>();
            if (text == null)
            {
                text = current.GetComponentInParent<TMP_Text>();
            }

            return text != null ? Clean(text.text) : string.Empty;
        }

        private static string GetPdxText(GameObject current)
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (current == null || menu == null)
            {
                return string.Empty;
            }

            TMP_InputField input = current.GetComponent<TMP_InputField>();
            if (input == null)
            {
                input = current.GetComponentInParent<TMP_InputField>();
            }

            if (input != null)
            {
                return GetPdxInputText(menu, input);
            }

            TMP_Dropdown dropdown = current.GetComponent<TMP_Dropdown>();
            if (dropdown == null)
            {
                dropdown = current.GetComponentInParent<TMP_Dropdown>();
            }

            if (dropdown != null)
            {
                return GetPdxDropdownText(menu, dropdown);
            }

            Toggle toggle = current.GetComponent<Toggle>();
            if (toggle == null)
            {
                toggle = current.GetComponentInParent<Toggle>();
            }

            if (toggle != null && menu.paradoxCreateOffers != null && toggle == menu.paradoxCreateOffers)
            {
                return Loc.Get("pdx_marketing", toggle.isOn ? Loc.Get("settings_on") : Loc.Get("settings_off"));
            }

            if (menu.paradoxLoggedUser != null && current.transform.IsChildOf(menu.paradoxLoggedUser.transform))
            {
                return Loc.Get("pdx_logged_in", Clean(menu.paradoxLoggedUser.text));
            }

            return string.Empty;
        }

        private static string GetPdxInputText(MainMenuManager menu, TMP_InputField input)
        {
            if (menu.paradoxLoginUser != null && input == menu.paradoxLoginUser)
            {
                return Loc.Get("pdx_email", InputValue(input));
            }

            if (menu.paradoxLoginPassword != null && input == menu.paradoxLoginPassword)
            {
                return Loc.Get("pdx_password", PasswordState(input));
            }

            if (menu.paradoxCreateEmail != null && input == menu.paradoxCreateEmail)
            {
                return Loc.Get("pdx_create_email", InputValue(input));
            }

            if (menu.paradoxCreatePassword != null && input == menu.paradoxCreatePassword)
            {
                return Loc.Get("pdx_create_password", PasswordState(input));
            }

            return Loc.Get("pdx_text_field");
        }

        private static string GetPdxDropdownText(MainMenuManager menu, TMP_Dropdown dropdown)
        {
            if (menu.paradoxDropdownRegion != null && dropdown == menu.paradoxDropdownRegion)
            {
                return Loc.Get("pdx_region", DropdownValue(dropdown));
            }

            if (menu.paradoxDropdownDay != null && dropdown == menu.paradoxDropdownDay)
            {
                return Loc.Get("pdx_birth_day", DropdownValue(dropdown));
            }

            if (menu.paradoxDropdownMonth != null && dropdown == menu.paradoxDropdownMonth)
            {
                return Loc.Get("pdx_birth_month", DropdownValue(dropdown));
            }

            if (menu.paradoxDropdownYear != null && dropdown == menu.paradoxDropdownYear)
            {
                return Loc.Get("pdx_birth_year", DropdownValue(dropdown));
            }

            return DropdownValue(dropdown);
        }

        private static string InputValue(TMP_InputField input)
        {
            string text = input != null ? Clean(input.text) : string.Empty;
            return string.IsNullOrWhiteSpace(text) ? Loc.Get("pdx_empty") : text;
        }

        private static string PasswordState(TMP_InputField input)
        {
            return input != null && !string.IsNullOrEmpty(input.text) ? Loc.Get("pdx_entered") : Loc.Get("pdx_empty");
        }

        private static string DropdownValue(TMP_Dropdown dropdown)
        {
            if (dropdown == null || dropdown.options == null || dropdown.options.Count == 0 || dropdown.value < 0 || dropdown.value >= dropdown.options.Count)
            {
                return Loc.Get("pdx_empty");
            }

            return Clean(dropdown.options[dropdown.value].text);
        }

        private static bool TryActivatePdxButton(MainMenuManager menu, GameObject current, string text)
        {
            if (menu == null || current == null || menu.paradoxT == null || !current.transform.IsChildOf(menu.paradoxT))
            {
                return false;
            }

            Button button = current.GetComponentInParent<Button>();
            if (button == null || !button.interactable)
            {
                return false;
            }

            string actionText = ((text ?? string.Empty) + " " + current.name + " " + button.name).ToLowerInvariant();
            if (IsChildOf(current, menu.paradoxCreatePopup) && ContainsCreateAccountAction(actionText))
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "Create account" : text));
                menu.CreatePDXAccount();
                return true;
            }

            if (IsChildOf(current, menu.paradoxLoginFieldsPopup) && ContainsLoginAction(actionText))
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "Login" : text));
                menu.PDXLogin();
                return true;
            }

            if (IsChildOf(current, menu.paradoxLoginPrePopup) && ContainsCreateAccountAction(actionText))
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "Create account" : text));
                menu.ShowPDXCreate();
                return true;
            }

            if (IsChildOf(current, menu.paradoxLoginPrePopup) && ContainsLoginAction(actionText))
            {
                ScreenReader.Say(Loc.Get("activated", string.IsNullOrWhiteSpace(text) ? "Login" : text));
                menu.ShowPDXLogin();
                return true;
            }

            return false;
        }

        private static bool IsChildOf(GameObject current, Transform parent)
        {
            return current != null && parent != null && current.transform.IsChildOf(parent);
        }

        private static bool ContainsCreateAccountAction(string text)
        {
            return text.Contains("create") ||
                text.Contains("созда");
        }

        private static bool ContainsLoginAction(string text)
        {
            return text.Contains("login") ||
                text.Contains("log in") ||
                text.Contains("sign in") ||
                text.Contains("войти") ||
                text.Contains("вход");
        }

        private static bool IsSaveDeleteAction(GameObject current)
        {
            if (current == null)
            {
                return false;
            }

            MenuSaveButton saveButton = current.GetComponentInParent<MenuSaveButton>();
            return saveButton != null && saveButton.deleteButton != null && current.transform == saveButton.deleteButton && HasOccupiedSave(saveButton);
        }

        private static bool HasOccupiedSave(MenuSaveButton saveButton)
        {
            return saveButton != null &&
                saveButton.descriptionText != null &&
                saveButton.descriptionText.gameObject.activeInHierarchy;
        }

        private static int GetProfileSlot(GameObject current)
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (current == null || menu == null || menu.profileOps == null)
            {
                return -1;
            }

            Transform transform = current.transform;
            for (int i = 0; i < menu.profileOps.Length; i++)
            {
                Transform profile = menu.profileOps[i];
                if (profile != null && (transform == profile || transform.IsChildOf(profile) || profile.IsChildOf(transform)))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsProfileDeleteAction(GameObject current)
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (current == null || menu == null || menu.profileDelete == null)
            {
                return false;
            }

            Transform transform = current.transform;
            return transform == menu.profileDelete || transform.IsChildOf(menu.profileDelete) || menu.profileDelete.IsChildOf(transform);
        }

        private static string GetProfileText(int slot)
        {
            MainMenuManager menu = MainMenuManager.Instance;
            string text = string.Empty;
            if (menu != null && menu.profileOpsText != null && slot >= 0 && slot < menu.profileOpsText.Length && menu.profileOpsText[slot] != null)
            {
                text = Clean(menu.profileOpsText[slot].text);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = slot == 0 ? GameText.Get("default") : GameText.Get("profileCreate");
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                text = slot == 0 ? "Default" : Loc.Get("profile_create");
            }

            string profileText = Loc.Get("profile_slot_named", slot + 1, text);
            if (GameManager.Instance != null && slot == GameManager.Instance.ProfileId)
            {
                return Loc.Get("profile_current_slot", profileText);
            }

            return profileText;
        }

        private static string SaveDeleteLabel()
        {
            string text = GameText.Get("mainMenuDelete");
            if (string.IsNullOrWhiteSpace(text))
            {
                text = GameText.Get("keyDelete");
            }

            return string.IsNullOrWhiteSpace(text) ? "Delete save" : Clean(text);
        }

        private static string GetGameModeDescription(BotonMenuGameMode gameMode)
        {
            string key = GetGameModeDescriptionKey(gameMode.gameMode);
            string description = GameText.Get(key);
            if (!string.IsNullOrWhiteSpace(description))
            {
                return Clean(description);
            }

            return ReadDescriptionText(gameMode.description);
        }

        private static string GetGameModeDescriptionKey(int gameMode)
        {
            switch (gameMode)
            {
                case 0:
                case 1:
                    return "mainMenuAdventureDescription";
                case 2:
                case 3:
                    return "mainMenuObeliskDescription";
                case 4:
                case 5:
                    return "mainMenuWeeklyDescription";
                case 6:
                case 7:
                    return "mainMenuSingularityDescription";
                default:
                    return string.Empty;
            }
        }

        private static string GetBotonGenericDescription(BotonGeneric boton)
        {
            if (boton == null)
            {
                return string.Empty;
            }

            string popup = GameText.Get(boton.idPopup);
            if (!string.IsNullOrWhiteSpace(popup))
            {
                return Clean(popup);
            }

            return Clean(FindRelatedDescriptionText(boton.idTranslate));
        }

        private static string FindRelatedDescriptionText(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            string[] bases = BuildDescriptionBases(id);
            string[] suffixes = { "Description", "Des", "Desk", "Intro", "Introduction" };
            for (int i = 0; i < bases.Length; i++)
            {
                for (int j = 0; j < suffixes.Length; j++)
                {
                    string candidate = bases[i] + suffixes[j];
                    if (candidate == id)
                    {
                        continue;
                    }

                    string text = GameText.Get(candidate);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        private static string FindRelatedTitleText(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            string[] bases = BuildDescriptionBases(id);
            for (int i = 0; i < bases.Length; i++)
            {
                string text = GameText.Get(bases[i] + "Title");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private static string[] BuildDescriptionBases(string id)
        {
            string[] suffixes = { "Description", "Des", "Desk", "Intro", "Introduction", "Title" };
            List<string> bases = new List<string>();
            AddUnique(bases, id);
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (id.EndsWith(suffixes[i], System.StringComparison.OrdinalIgnoreCase) && id.Length > suffixes[i].Length)
                {
                    AddUnique(bases, id.Substring(0, id.Length - suffixes[i].Length));
                }
            }

            return bases.ToArray();
        }

        private static string ReadDescriptionText(Transform root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            TMP_Text text = root.GetComponentInChildren<TMP_Text>(true);
            return text != null ? Clean(text.text) : string.Empty;
        }

        private static string AppendDescription(string title, string description)
        {
            if (string.IsNullOrWhiteSpace(description) || description == title)
            {
                return title;
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                return Loc.Get("menu_description", description);
            }

            return title + ". " + Loc.Get("menu_description", description);
        }

        private static int GetSaveButtonPosition(MenuSaveButton saveButton)
        {
            MainMenuManager menu = MainMenuManager.Instance;
            if (menu == null || menu.menuSaveButtons == null)
            {
                return -1;
            }

            for (int i = 0; i < menu.menuSaveButtons.Length; i++)
            {
                if (menu.menuSaveButtons[i] == saveButton)
                {
                    return i + 1;
                }
            }

            return -1;
        }

        private static string GetSaveButtonText(MenuSaveButton saveButton, int position)
        {
            List<string> parts = new List<string>();
            if (position > 0)
            {
                parts.Add(Loc.Get("save_slot_position", position));
            }

            AddPart(parts, saveButton.slotText);
            AddPart(parts, saveButton.typeText);
            AddPart(parts, saveButton.descriptionText);
            AddPart(parts, saveButton.playersText);
            AddPart(parts, saveButton.madnessText);
            AddPart(parts, saveButton.versionText);

            return string.Join(". ", parts.ToArray());
        }

        private static void AddPart(List<string> parts, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            string clean = Clean(text.text);
            if (!string.IsNullOrWhiteSpace(clean) && !ContainsPart(parts, clean))
            {
                parts.Add(clean);
            }
        }

        private static bool ContainsPart(List<string> parts, string candidate)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddUnique(List<string> items, string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] == candidate)
                {
                    return;
                }
            }

            items.Add(candidate);
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static void WarpMouseTo(Vector3 worldPosition)
        {
            if (Mouse.current != null)
            {
                Mouse.current.WarpCursorPosition(worldPosition);
            }
        }
    }
}
