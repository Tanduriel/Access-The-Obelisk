using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for the game settings menu.
    /// </summary>
    public sealed class SettingsHandler
    {
        private enum ItemKind
        {
            Tab,
            Toggle,
            Slider,
            Dropdown,
            Button
        }

        private sealed class SettingItem
        {
            public ItemKind Kind;
            public string Key;
            public string Label;
            public int TabIndex;
            public Toggle Toggle;
            public Slider Slider;
            public TMP_Dropdown Dropdown;
            public string Description;
            public bool Available = true;
        }

        private static readonly FieldInfo TelemetryToggleField = AccessTools.Field(typeof(SettingsManager), "telemetryToggle");
        private static readonly FieldInfo TelemetryContainerField = AccessTools.Field(typeof(SettingsManager), "telemetryContainerGO");
        private static bool _openedByShowSettings;

        private readonly List<SettingItem> _items = new List<SettingItem>();
        private int _index;
        private int _currentTab;
        private bool _announced;
        private bool _alertAnnounced;
        private bool _alertAcceptSelected;
        private string _lastFocusKey;
        private float _lastRefreshTime;
        private float _ignoreInputUntil;
        private bool _waitForSubmitRelease;

        /// <summary>
        /// Updates settings menu navigation and activation.
        /// </summary>
        public bool Update()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings == null || !settings.IsActive() || !_openedByShowSettings)
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Settings);
            ClearGameSelection();
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(settings);
                AnnounceScreenOnce();
                _lastRefreshTime = Time.unscaledTime;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                HandleAlert(AlertManager.Instance);
                return true;
            }

            _alertAnnounced = false;
            ProcessKeys(settings);
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _currentTab = 0;
            _announced = false;
            _alertAnnounced = false;
            _alertAcceptSelected = false;
            _lastFocusKey = null;
            _ignoreInputUntil = 0f;
            _waitForSubmitRelease = false;
        }

        private void Refresh(SettingsManager settings)
        {
            int detectedTab = DetectTab(settings);
            if (!_announced)
            {
                _currentTab = detectedTab;
            }
            else if (detectedTab != _currentTab)
            {
                settings.SelectTab(_currentTab);
            }

            _items.Clear();
            AddTab(0, LocalizedSettingLabel("tab_graphics"));
            AddTab(1, LocalizedSettingLabel("tab_audio"));
            AddTab(2, LocalizedSettingLabel("tab_gameplay"));

            if (_currentTab == 0)
            {
                AddDropdown("resolution", LocalizedSettingLabel("resolution"), settings.resolutionDropdown);
                AddToggle("fullscreen", LocalizedSettingLabel("fullscreen"), settings.fullscreenToggle);
                AddToggle("vsync", LocalizedSettingLabel("vsync"), settings.vsyncToggle);
                AddToggle("screen_shake", LocalizedSettingLabel("screen_shake"), settings.screenShakeOptionToggle);
                AddToggle("ac_backgrounds", LocalizedSettingLabel("ac_backgrounds"), settings.acbackgroundEffectsToggle);
                AddDropdown("language", LocalizedSettingLabel("language"), settings.languageDropdown);
            }
            else if (_currentTab == 1)
            {
                AddSlider("master_volume", LocalizedSettingLabel("master_volume"), settings.masterVolumeSlider);
                AddSlider("effects_volume", LocalizedSettingLabel("effects_volume"), settings.effectsVolumeSlider);
                AddSlider("music_volume", LocalizedSettingLabel("music_volume"), settings.bsoVolumeSlider);
                AddSlider("ambience_volume", LocalizedSettingLabel("ambience_volume"), settings.ambienceVolumeSlider);
                AddToggle("background_mute", LocalizedSettingLabel("background_mute"), settings.backgroundMuteToggle);
                AddToggle("legacy_sounds", LocalizedSettingLabel("legacy_sounds"), settings.legacySoundsToggle);
                AddToggle("legacy_sounds_extra", LocalizedSettingLabel("legacy_sounds_extra"), settings.legacySoundsSheepOwlToggle);
            }
            else
            {
                AddToggle("fast_mode", LocalizedSettingLabel("fast_mode"), settings.fastModeToggle);
                AddToggle("auto_end", LocalizedSettingLabel("auto_end"), settings.autoEndToggle);
                AddToggle("show_effects", LocalizedSettingLabel("show_effects"), settings.showEffectsToggle);
                AddToggle("restart_combat", LocalizedSettingLabel("restart_combat"), settings.restartCombatOptionToggle);
                AddToggle("keyboard_shortcuts", LocalizedSettingLabel("keyboard_shortcuts"), settings.keyboardShortcutsToggle);
                AddToggle("extended_descriptions", LocalizedSettingLabel("extended_descriptions"), settings.extendedDescriptionsToggle);
                AddToggle("follow_leader", LocalizedSettingLabel("follow_leader"), settings.followingTheLeaderToggle);
                AddTelemetryToggle(settings);
                AddButton("reset_tutorial", LocalizedSettingLabel("reset_tutorial"), settings.resetTutorialToggle);
                if (settings.resetSavedT != null && settings.resetSavedT.gameObject.activeInHierarchy)
                {
                    AddButton("reset_saved", LocalizedSettingLabel("reset_saved"), settings.resetSavedToggle);
                }
            }

            AddVisibleControlsFromActiveTab(settings);
            _index = ClampIndex(_index, _items.Count);
        }

        private void AddTab(int tabIndex, string label)
        {
            SettingItem item = new SettingItem();
            item.Kind = ItemKind.Tab;
            item.Key = "tab:" + tabIndex;
            item.Label = label;
            item.TabIndex = tabIndex;
            _items.Add(item);
        }

        private void AddToggle(string key, string label, Toggle toggle)
        {
            if (!IsVisible(toggle != null ? toggle.transform : null))
            {
                return;
            }

            SettingItem item = new SettingItem();
            item.Kind = ItemKind.Toggle;
            item.Key = key;
            item.Label = label;
            item.Toggle = toggle;
            item.Description = ReadDescription(toggle.transform, key);
            _items.Add(item);
        }

        private void AddButton(string key, string label, Toggle toggle)
        {
            if (!IsVisible(toggle != null ? toggle.transform : null))
            {
                return;
            }

            SettingItem item = new SettingItem();
            item.Kind = ItemKind.Button;
            item.Key = key;
            item.Label = label;
            item.Toggle = toggle;
            item.Description = ReadDescription(toggle.transform, key);
            _items.Add(item);
        }

        private void AddSlider(string key, string label, Slider slider)
        {
            if (!IsVisible(slider != null ? slider.transform : null))
            {
                return;
            }

            SettingItem item = new SettingItem();
            item.Kind = ItemKind.Slider;
            item.Key = key;
            item.Label = label;
            item.Slider = slider;
            item.Description = ReadDescription(slider.transform, key);
            _items.Add(item);
        }

        private void AddDropdown(string key, string label, TMP_Dropdown dropdown)
        {
            if (!IsVisible(dropdown != null ? dropdown.transform : null))
            {
                return;
            }

            SettingItem item = new SettingItem();
            item.Kind = ItemKind.Dropdown;
            item.Key = key;
            item.Label = label;
            item.Dropdown = dropdown;
            item.Description = ReadDescription(dropdown.transform, key);
            _items.Add(item);
        }

        private void AddTelemetryToggle(SettingsManager settings)
        {
            GameObject container = TelemetryContainerField != null ? TelemetryContainerField.GetValue(settings) as GameObject : null;
            if (container != null && !container.activeInHierarchy)
            {
                return;
            }

            Toggle toggle = TelemetryToggleField != null ? TelemetryToggleField.GetValue(settings) as Toggle : null;
            AddToggle("telemetry", LocalizedSettingLabel("telemetry"), toggle);
        }

        private void AddVisibleControlsFromActiveTab(SettingsManager settings)
        {
            Transform tab = ActiveTab(settings);
            if (tab == null)
            {
                return;
            }

            Toggle[] toggles = tab.GetComponentsInChildren<Toggle>(true);
            for (int i = 0; i < toggles.Length; i++)
            {
                Toggle toggle = toggles[i];
                if (!IsVisible(toggle != null ? toggle.transform : null) || ContainsToggle(toggle))
                {
                    continue;
                }

                string key = KnownToggleKey(settings, toggle);
                string label = KnownLabel(key);
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = ReadControlLabel(toggle.transform, Loc.Get("settings_unknown_toggle"));
                }

                AddToggle(key, label, toggle);
            }

            Slider[] sliders = tab.GetComponentsInChildren<Slider>(true);
            for (int i = 0; i < sliders.Length; i++)
            {
                Slider slider = sliders[i];
                if (!IsVisible(slider != null ? slider.transform : null) || ContainsSlider(slider))
                {
                    continue;
                }

                string label = ReadControlLabel(slider.transform, Loc.Get("settings_unknown_slider"));
                AddSlider("dynamic_slider:" + slider.gameObject.name, label, slider);
            }

            TMP_Dropdown[] dropdowns = tab.GetComponentsInChildren<TMP_Dropdown>(true);
            for (int i = 0; i < dropdowns.Length; i++)
            {
                TMP_Dropdown dropdown = dropdowns[i];
                if (!IsVisible(dropdown != null ? dropdown.transform : null) || ContainsDropdown(dropdown))
                {
                    continue;
                }

                string label = ReadControlLabel(dropdown.transform, Loc.Get("settings_unknown_dropdown"));
                AddDropdown("dynamic_dropdown:" + dropdown.gameObject.name, label, dropdown);
            }
        }

        private void AnnounceScreenOnce()
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            _ignoreInputUntil = Time.unscaledTime + 0.35f;
            _waitForSubmitRelease = IsSubmitHeld();
            ScreenReader.Say(LocalizedSettingLabel("screen"));
            ScreenReader.SayQueued(Loc.Get("settings_controls"));
            AnnounceFocused(true);
        }

        private void HandleAlert(AlertManager alert)
        {
            bool hasLeft = alert.alertTextLeftButton != null && alert.alertTextLeftButton.transform.parent.gameObject.activeInHierarchy;
            bool hasRight = alert.alertTextRightButton != null && alert.alertTextRightButton.transform.parent.gameObject.activeInHierarchy;
            bool hasSingle = alert.alertTextSingleButton != null && alert.alertTextSingleButton.transform.parent.gameObject.activeInHierarchy;

            if (!_alertAnnounced)
            {
                _alertAnnounced = true;
                _alertAcceptSelected = false;
                string text = Clean(alert.alertText != null ? alert.alertText.text : "");
                if (string.IsNullOrWhiteSpace(text) && alert.alertTextCP != null && alert.alertTextCP.gameObject.activeInHierarchy)
                {
                    text = Clean(alert.alertTextCP.text);
                }

                ScreenReader.Say(Loc.Get("settings_alert", text));
                ScreenReader.SayQueued(AlertChoiceText(alert, hasLeft, hasRight, hasSingle));
            }

            if (hasLeft && hasRight && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                _alertAcceptSelected = Input.GetKeyDown(KeyCode.RightArrow);
                ScreenReader.Say(AlertChoiceText(alert, hasLeft, hasRight, hasSingle));
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (hasLeft && hasRight)
                {
                    alert.SetConfirmAnswer(_alertAcceptSelected);
                }
                else
                {
                    alert.CloseAlert(force: true);
                }

                _alertAnnounced = false;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                alert.CloseAlert(force: true);
                _alertAnnounced = false;
            }
        }

        private string AlertChoiceText(AlertManager alert, bool hasLeft, bool hasRight, bool hasSingle)
        {
            if (hasLeft && hasRight)
            {
                string left = Clean(alert.alertTextLeftButton.text);
                string right = Clean(alert.alertTextRightButton.text);
                string selected = _alertAcceptSelected ? right : left;
                return Loc.Get("settings_alert_choice", selected);
            }

            if (hasSingle)
            {
                return Loc.Get("settings_alert_single", Clean(alert.alertTextSingleButton.text));
            }

            return Loc.Get("settings_alert_no_button");
        }

        private void ProcessKeys(SettingsManager settings)
        {
            if (Time.unscaledTime < _ignoreInputUntil)
            {
                return;
            }

            if (_waitForSubmitRelease)
            {
                if (IsSubmitHeld())
                {
                    return;
                }

                _waitForSubmitRelease = false;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Adjust(settings, -1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                Adjust(settings, 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(settings);
            }
        }

        private void Move(int delta)
        {
            ClearGameSelection();
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            AnnounceFocused();
        }

        private void Jump(bool end)
        {
            ClearGameSelection();
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            AnnounceFocused();
        }

        private void Adjust(SettingsManager settings, int delta)
        {
            SettingItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            ClearGameSelection();
            if (item.Kind == ItemKind.Tab)
            {
                ScreenReader.Say(FocusText(item));
                return;
            }

            if (item.Kind == ItemKind.Slider)
            {
                AdjustSlider(settings, item, delta);
                return;
            }

            if (item.Kind == ItemKind.Dropdown)
            {
                AdjustDropdown(settings, item, delta);
                return;
            }

            ScreenReader.Say(FocusText(item));
        }

        private void Activate(SettingsManager settings)
        {
            SettingItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            ClearGameSelection();
            if (item.Kind == ItemKind.Tab)
            {
                SelectTab(settings, item.TabIndex);
                return;
            }

            if (item.Kind == ItemKind.Toggle)
            {
                bool next = !item.Toggle.isOn;
                SetToggle(settings, item, next);
                ScreenReader.Say(FocusText(item));
                return;
            }

            if (item.Kind == ItemKind.Button)
            {
                ActivateButton(settings, item);
                return;
            }

            if (item.Kind == ItemKind.Dropdown || item.Kind == ItemKind.Slider)
            {
                if (item.Kind == ItemKind.Dropdown && item.Key == "language")
                {
                    ApplyDropdown(settings, item);
                    ScreenReader.Say(Loc.Get("settings_language_applied", ValueText(item)));
                    return;
                }

                ScreenReader.Say(FocusText(item));
            }
        }

        private void SelectTab(SettingsManager settings, int tabIndex)
        {
            settings.SelectTab(tabIndex);
            _currentTab = tabIndex;
            _index = tabIndex;
            _lastFocusKey = null;
            Refresh(settings);
            ClearGameSelection();
            ScreenReader.Say(Loc.Get("settings_tab_selected", TabName(tabIndex)));
            AnnounceFocused(true);
        }

        private void AdjustSlider(SettingsManager settings, SettingItem item, int delta)
        {
            Slider slider = item.Slider;
            float range = slider.maxValue - slider.minValue;
            float step = range > 0f ? range / 20f : 0.05f;
            slider.SetValueWithoutNotify(Mathf.Clamp(slider.value + step * delta, slider.minValue, slider.maxValue));
            ApplySlider(settings, item);
            ScreenReader.Say(FocusText(item));
        }

        private void AdjustDropdown(SettingsManager settings, SettingItem item, int delta)
        {
            TMP_Dropdown dropdown = item.Dropdown;
            int count = dropdown.options != null ? dropdown.options.Count : 0;
            if (count == 0)
            {
                ScreenReader.Say(Loc.Get("settings_no_options"));
                return;
            }

            dropdown.SetValueWithoutNotify(ClampIndex(dropdown.value + delta, count));
            dropdown.RefreshShownValue();
            if (item.Key != "language")
            {
                ApplyDropdown(settings, item);
            }

            ScreenReader.Say(FocusText(item));
        }

        private void SetToggle(SettingsManager settings, SettingItem item, bool value)
        {
            item.Toggle.SetIsOnWithoutNotify(value);
            if (item.Key == "fullscreen")
            {
                settings.SetFullscreen(value);
            }
            else if (item.Key == "vsync")
            {
                settings.SetVsync(value);
            }
            else if (item.Key == "screen_shake")
            {
                settings.SetScreenShake(value);
            }
            else if (item.Key == "ac_backgrounds")
            {
                settings.SetACBackgrounds(value);
            }
            else if (item.Key == "background_mute")
            {
                settings.SetBackgroundMute(value);
            }
            else if (item.Key == "legacy_sounds")
            {
                settings.SetUseLegacySounds(value);
            }
            else if (item.Key == "legacy_sounds_extra")
            {
                settings.SetUseLegacySoundsSheepOwl(value);
            }
            else if (item.Key == "fast_mode")
            {
                settings.SetFastMode(value);
            }
            else if (item.Key == "auto_end")
            {
                settings.SetAutoEnd(value);
            }
            else if (item.Key == "show_effects")
            {
                settings.SetShowEffects(value);
            }
            else if (item.Key == "restart_combat")
            {
                settings.SetRestartCombat(value);
            }
            else if (item.Key == "keyboard_shortcuts")
            {
                settings.SetKeyboardShortcuts(value);
            }
            else if (item.Key == "extended_descriptions")
            {
                settings.SetExtendedDescriptions(value);
            }
            else if (item.Key == "follow_leader")
            {
                settings.SetFollowingTheLeader(value);
            }
            else if (item.Key == "telemetry")
            {
                settings.OnTelemetryToggleChanged(value);
            }
            else
            {
                item.Toggle.isOn = value;
            }
        }

        private static void ApplySlider(SettingsManager settings, SettingItem item)
        {
            if (item.Key == "master_volume")
            {
                settings.SetMasterVolume(item.Slider.value);
            }
            else if (item.Key == "effects_volume")
            {
                settings.SetEffectsVolume(item.Slider.value);
            }
            else if (item.Key == "music_volume")
            {
                settings.SetBSOVolume(item.Slider.value);
            }
            else if (item.Key == "ambience_volume")
            {
                settings.SetAmbienceVolume(item.Slider.value);
            }
        }

        private static void ApplyDropdown(SettingsManager settings, SettingItem item)
        {
            if (item.Key == "resolution")
            {
                settings.SetResolution(item.Dropdown.value);
            }
            else if (item.Key == "language")
            {
                AlertHandler.DismissNextSingleButtonAlertWithoutDelegate();
                settings.SetLanguage(item.Dropdown.value);
            }
        }

        private void ActivateButton(SettingsManager settings, SettingItem item)
        {
            if (item.Key == "reset_saved")
            {
                settings.ResetSavedData();
                ScreenReader.Say(Loc.Get("activated", item.Label));
            }
            else if (item.Key == "reset_tutorial")
            {
                settings.ResetTutorial();
                ScreenReader.Say(Loc.Get("activated", item.Label));
            }
        }

        private void AnnounceFocused(bool queued = false)
        {
            SettingItem item = CurrentItem();
            string key = item != null ? item.Key + ":" + ValueText(item) : "none";
            if (key == _lastFocusKey && !queued)
            {
                return;
            }

            _lastFocusKey = key;
            string text = item != null ? FocusText(item) : Loc.Get("no_menu_item");
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private string FocusText(SettingItem item)
        {
            string text;
            if (item.Kind == ItemKind.Tab)
            {
                string state = item.TabIndex == _currentTab ? Loc.Get("current") : Loc.Get("available");
                text = Loc.Get("settings_tab_item", item.Label, state);
            }
            else
            {
                text = item.Label + ". " + ValueText(item);
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                text += " " + Loc.Get("settings_description", item.Description);
            }

            return text;
        }

        private static string ValueText(SettingItem item)
        {
            if (item.Kind == ItemKind.Toggle)
            {
                return item.Toggle.isOn ? LocalizedText("enabled", Loc.Get("settings_on")) : LocalizedText("disabled", Loc.Get("settings_off"));
            }

            if (item.Kind == ItemKind.Button)
            {
                return Loc.Get("settings_press_enter");
            }

            if (item.Kind == ItemKind.Slider)
            {
                float range = item.Slider.maxValue - item.Slider.minValue;
                float normalized = range > 0f ? (item.Slider.value - item.Slider.minValue) / range : item.Slider.value;
                return Loc.Get("settings_percent", Mathf.RoundToInt(normalized * 100f));
            }

            if (item.Kind == ItemKind.Dropdown)
            {
                TMP_Dropdown dropdown = item.Dropdown;
                if (dropdown.options == null || dropdown.options.Count == 0)
                {
                    return Loc.Get("settings_no_options");
                }

                int index = ClampIndex(dropdown.value, dropdown.options.Count);
                return Clean(dropdown.options[index].text);
            }

            return string.Empty;
        }

        private SettingItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private static int DetectTab(SettingsManager settings)
        {
            if (settings.audioTab != null && settings.audioTab.gameObject.activeInHierarchy)
            {
                return 1;
            }

            if (settings.gameplayTab != null && settings.gameplayTab.gameObject.activeInHierarchy)
            {
                return 2;
            }

            return 0;
        }

        private static string TabName(int tabIndex)
        {
            if (tabIndex == 1)
            {
                return LocalizedSettingLabel("tab_audio");
            }

            if (tabIndex == 2)
            {
                return LocalizedSettingLabel("tab_gameplay");
            }

            return LocalizedSettingLabel("tab_graphics");
        }

        private static bool IsSubmitHeld()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count == 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static Transform ActiveTab(SettingsManager settings)
        {
            if (settings.audioTab != null && settings.audioTab.gameObject.activeInHierarchy)
            {
                return settings.audioTab;
            }

            if (settings.gameplayTab != null && settings.gameplayTab.gameObject.activeInHierarchy)
            {
                return settings.gameplayTab;
            }

            return settings.graphicsTab;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && Functions.TransformIsVisible(transform);
        }

        private bool ContainsToggle(Toggle toggle)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Toggle == toggle)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsSlider(Slider slider)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Slider == slider)
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsDropdown(TMP_Dropdown dropdown)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Dropdown == dropdown)
                {
                    return true;
                }
            }

            return false;
        }

        private static string KnownToggleKey(SettingsManager settings, Toggle toggle)
        {
            if (toggle == settings.fullscreenToggle) return "fullscreen";
            if (toggle == settings.vsyncToggle) return "vsync";
            if (toggle == settings.screenShakeOptionToggle) return "screen_shake";
            if (toggle == settings.acbackgroundEffectsToggle) return "ac_backgrounds";
            if (toggle == settings.backgroundMuteToggle) return "background_mute";
            if (toggle == settings.legacySoundsToggle) return "legacy_sounds";
            if (toggle == settings.legacySoundsSheepOwlToggle) return "legacy_sounds_extra";
            if (toggle == settings.fastModeToggle) return "fast_mode";
            if (toggle == settings.autoEndToggle) return "auto_end";
            if (toggle == settings.showEffectsToggle) return "show_effects";
            if (toggle == settings.restartCombatOptionToggle) return "restart_combat";
            if (toggle == settings.keyboardShortcutsToggle) return "keyboard_shortcuts";
            if (toggle == settings.extendedDescriptionsToggle) return "extended_descriptions";
            if (toggle == settings.followingTheLeaderToggle) return "follow_leader";
            if (toggle == settings.resetTutorialToggle) return "reset_tutorial";
            if (toggle == settings.resetSavedToggle) return "reset_saved";

            Toggle telemetry = TelemetryToggleField != null ? TelemetryToggleField.GetValue(settings) as Toggle : null;
            if (toggle == telemetry) return "telemetry";
            return "dynamic_toggle:" + toggle.gameObject.name;
        }

        private static string KnownLabel(string key)
        {
            if (key == "fullscreen") return LocalizedSettingLabel("fullscreen");
            if (key == "vsync") return LocalizedSettingLabel("vsync");
            if (key == "screen_shake") return LocalizedSettingLabel("screen_shake");
            if (key == "ac_backgrounds") return LocalizedSettingLabel("ac_backgrounds");
            if (key == "background_mute") return LocalizedSettingLabel("background_mute");
            if (key == "legacy_sounds") return LocalizedSettingLabel("legacy_sounds");
            if (key == "legacy_sounds_extra") return LocalizedSettingLabel("legacy_sounds_extra");
            if (key == "fast_mode") return LocalizedSettingLabel("fast_mode");
            if (key == "auto_end") return LocalizedSettingLabel("auto_end");
            if (key == "show_effects") return LocalizedSettingLabel("show_effects");
            if (key == "restart_combat") return LocalizedSettingLabel("restart_combat");
            if (key == "keyboard_shortcuts") return LocalizedSettingLabel("keyboard_shortcuts");
            if (key == "extended_descriptions") return LocalizedSettingLabel("extended_descriptions");
            if (key == "follow_leader") return LocalizedSettingLabel("follow_leader");
            if (key == "reset_tutorial") return LocalizedSettingLabel("reset_tutorial");
            if (key == "reset_saved") return LocalizedSettingLabel("reset_saved");
            if (key == "telemetry") return LocalizedSettingLabel("telemetry");
            return "";
        }

        private static string ReadControlLabel(Transform transform, string fallback)
        {
            Transform root = ControlRoot(transform);
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                string text = Clean(texts[i].text);
                if (!string.IsNullOrWhiteSpace(text) && !LooksLikeValueText(text))
                {
                    return text;
                }
            }

            string objectName = Clean(transform.gameObject.name);
            return string.IsNullOrWhiteSpace(objectName) ? fallback : objectName;
        }

        private static Transform ControlRoot(Transform transform)
        {
            Transform root = transform;
            while (root.parent != null && IsVisible(root.parent) && !HasSiblingControl(root))
            {
                root = root.parent;
            }

            return root;
        }

        private static bool HasSiblingControl(Transform transform)
        {
            if (transform.parent == null)
            {
                return true;
            }

            int controls = 0;
            foreach (Transform child in transform.parent)
            {
                if (child.GetComponentInChildren<Toggle>(true) != null ||
                    child.GetComponentInChildren<Slider>(true) != null ||
                    child.GetComponentInChildren<TMP_Dropdown>(true) != null)
                {
                    controls++;
                }
            }

            return controls > 1;
        }

        private static string ReadDescription(Transform transform, string key)
        {
            Transform root = ControlRoot(transform);
            PopupText popup = root.GetComponentInChildren<PopupText>(true);
            string text = DescriptionFromPopup(popup);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            BotonGeneric boton = root.GetComponentInChildren<BotonGeneric>(true);
            if (boton != null && !string.IsNullOrWhiteSpace(boton.idPopup))
            {
                return Clean(GameText.Get(boton.idPopup));
            }

            return LocalizedSettingDescription(key);
        }

        private static string DescriptionFromPopup(PopupText popup)
        {
            if (popup == null)
            {
                return "";
            }

            if (!string.IsNullOrWhiteSpace(popup.id))
            {
                return Clean(GameText.Get(popup.id));
            }

            return Clean(popup.text);
        }

        private static string LocalizedSettingLabel(string key)
        {
            return LocalizedText(SettingTextId(key), Loc.Get("settings_" + key));
        }

        private static string LocalizedSettingDescription(string key)
        {
            return Clean(GameText.Get(SettingDescriptionId(key)));
        }

        private static string LocalizedText(string id, string fallback)
        {
            string text = Clean(GameText.Get(id));
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string SettingTextId(string key)
        {
            if (key == "screen") return "settings";
            if (key == "tab_graphics") return "general";
            if (key == "tab_audio") return "audio";
            if (key == "tab_gameplay") return "gameplay";
            if (key == "resolution") return "screenResolution";
            if (key == "fullscreen") return "fullScreen";
            if (key == "screen_shake") return "enableShakeOption";
            if (key == "ac_backgrounds") return "acBackground";
            if (key == "language") return "selectLanguage";
            if (key == "master_volume") return "masterVolume";
            if (key == "effects_volume") return "effectsVolume";
            if (key == "music_volume") return "musicVolume";
            if (key == "ambience_volume") return "ambienceVolume";
            if (key == "background_mute") return "backgroundMute";
            if (key == "legacy_sounds") return "legacySounds";
            if (key == "legacy_sounds_extra") return "legacySoundsSheepOwl";
            if (key == "fast_mode") return "fastMode";
            if (key == "auto_end") return "autoEnd";
            if (key == "show_effects") return "showEffects";
            if (key == "restart_combat") return "restartCombatOption";
            if (key == "extended_descriptions") return "extendedDescriptions";
            if (key == "follow_leader") return "followTheLeader";
            if (key == "reset_tutorial") return "resetTutorial";
            if (key == "reset_saved") return "resetSavedData";
            if (key == "telemetry") return "optionalTelemetry";
            return key;
        }

        private static string SettingDescriptionId(string key)
        {
            if (key == "screen_shake") return "enableShakeOptionDes";
            if (key == "ac_backgrounds") return "acBackgroundDes";
            if (key == "background_mute") return "backgroundMuteDes";
            if (key == "legacy_sounds") return "legacySoundsDes";
            if (key == "legacy_sounds_extra") return "legacySoundsSheepOwlDes";
            if (key == "fast_mode") return "fastModeDes";
            if (key == "auto_end") return "autoEndDes";
            if (key == "show_effects") return "showEffectsDes";
            if (key == "restart_combat") return "restartCombatOptionDes";
            if (key == "extended_descriptions") return "extendedDescriptionsDes";
            if (key == "follow_leader") return "followTheLeaderDesc";
            if (key == "reset_tutorial") return "resetTutorialDes";
            if (key == "reset_saved") return "resetSavedDataDes";
            if (key == "telemetry") return "optionalTelemetryDescription";
            return "";
        }

        private static bool LooksLikeValueText(string text)
        {
            return text == Loc.Get("settings_on") ||
                text == Loc.Get("settings_off") ||
                text.EndsWith("%") ||
                text.Contains(" x ");
        }

        private static void ClearGameSelection()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        internal static void NotifyShowSettings(bool state)
        {
            _openedByShowSettings = state;
        }
    }

    [HarmonyPatch(typeof(SettingsManager), nameof(SettingsManager.ShowSettings))]
    internal static class SettingsManagerShowSettingsPatch
    {
        private static void Postfix(bool _state)
        {
            SettingsHandler.NotifyShowSettings(_state);
        }
    }
}
