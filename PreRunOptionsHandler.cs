using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen-reader access for pre-run Madness and Sandbox option windows.
    /// </summary>
    public sealed class PreRunOptionsHandler
    {
        private enum ItemKind
        {
            MadnessLevel,
            MadnessCorruptor,
            SandboxCombo,
            SandboxChoice,
            Button,
            Text
        }

        private sealed class Item
        {
            public ItemKind Kind;
            public string Key;
            public string Label;
            public string Details;
            public BotonGeneric Button;
            public readonly List<BotonGeneric> Buttons = new List<BotonGeneric>();
            public int IntValue;
        }

        private readonly List<Item> _items = new List<Item>();
        private int _index;
        private bool _wasActive;
        private bool _waitForSubmitRelease;
        private float _ignoreInputUntil;
        private string _mode;
        private float _lastRefreshTime;
        private static bool _openedByShowMadness;
        private static bool _openedByShowSandbox;

        /// <summary>
        /// Updates modal pre-run option access.
        /// </summary>
        public bool Update()
        {
            bool madnessActive = IsMadnessWindowActive();
            bool sandboxActive = IsSandboxWindowActive();
            if (!madnessActive && !sandboxActive)
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.PreRunOptions);
            string mode = madnessActive ? "madness" : "sandbox";
            if (!_wasActive || _mode != mode || Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                _mode = mode;
                Refresh(madnessActive);
                _lastRefreshTime = Time.unscaledTime;
            }

            if (!_wasActive)
            {
                _wasActive = true;
                _waitForSubmitRelease = IsSubmitHeld();
                _ignoreInputUntil = Time.unscaledTime + 0.35f;
                ScreenReader.Say(madnessActive ? Loc.Get("pre_run_madness_screen") : Loc.Get("pre_run_sandbox_screen"));
                ScreenReader.SayQueued(madnessActive ? Loc.Get("pre_run_madness_controls") : Loc.Get("pre_run_sandbox_controls"));
                AnnounceFocused(true);
            }

            ProcessKeys(madnessActive);
            return true;
        }

        private static bool IsMadnessWindowActive()
        {
            MadnessManager manager = MadnessManager.Instance;
            return manager != null &&
                   _openedByShowMadness &&
                   (IsActiveWindow(manager.madnessWindow) ||
                    IsActiveWindow(manager.madnessChallengeWindow) ||
                    IsActiveWindow(manager.madnessWeeklyWindow) ||
                    IsActiveWindow(manager.madnessSingularityWindow));
        }

        private static bool IsSandboxWindowActive()
        {
            SandboxManager manager = SandboxManager.Instance;
            return manager != null && _openedByShowSandbox && IsActiveWindow(manager.sandboxWindow);
        }

        private static bool IsActiveWindow(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy && Functions.TransformIsVisible(transform);
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _wasActive = false;
            _waitForSubmitRelease = false;
            _ignoreInputUntil = 0f;
            _mode = "";
            _lastRefreshTime = 0f;
        }

        private void Refresh(bool madnessActive)
        {
            _items.Clear();
            if (madnessActive)
            {
                BuildMadnessItems();
            }
            else
            {
                BuildSandboxItems();
            }

            _index = ClampIndex(_index, _items.Count);
        }

        private void BuildMadnessItems()
        {
            MadnessManager manager = MadnessManager.Instance;
            AddMadnessButtons(manager.mButton);
            AddMadnessButtons(manager.mChallengeButton);
            AddMadnessButtons(manager.mSingularityButton);
            AddCorruptors(manager);
            AddVisibleText(manager.weeklyModificators, Loc.Get("pre_run_weekly_modifiers"));
            AddVisibleTextArray(manager.mChallengeWeeklyText, Loc.Get("pre_run_weekly_trait"));
            AddButton(manager.buttonSandbox, Loc.Get("hero_selection_sandbox"));
            AddButton(manager.madnessConfirmButton, Loc.Get("pre_run_confirm"));
            AddButton(manager.madnessChallengeConfirmButton, Loc.Get("pre_run_confirm"));
            AddButton(manager.madnessSingularityConfirmButton, Loc.Get("pre_run_confirm"));
            AddButton(manager.buttonExit, Loc.Get("pre_run_close"));
            AddButton(manager.buttonChallengeExit, Loc.Get("pre_run_close"));
            AddButton(manager.buttonWeeklyExit, Loc.Get("pre_run_close"));
            AddButton(manager.buttonSingularityExit, Loc.Get("pre_run_close"));
        }

        private void AddMadnessButtons(BotonGeneric[] buttons)
        {
            if (buttons == null)
            {
                return;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                BotonGeneric button = buttons[i];
                if (!IsVisible(button != null ? button.transform : null))
                {
                    continue;
                }

                Item item = new Item();
                item.Kind = ItemKind.MadnessLevel;
                item.Button = button;
                item.IntValue = button.auxInt >= 0 ? button.auxInt : i;
                item.Label = Loc.Get("pre_run_madness_level", item.IntValue);
                item.Details = Clean(Functions.GetMadnessBonusText(item.IntValue));
                _items.Add(item);
            }
        }

        private void AddCorruptors(MadnessManager manager)
        {
            if (manager.mCorruptor == null)
            {
                return;
            }

            for (int i = 0; i < manager.mCorruptor.Length; i++)
            {
                BotonGeneric button = manager.mCorruptor[i];
                if (!IsVisible(button != null ? button.transform : null))
                {
                    continue;
                }

                string label = "";
                if (manager.mCorruptorText != null && i < manager.mCorruptorText.Length && manager.mCorruptorText[i] != null)
                {
                    label = Clean(manager.mCorruptorText[i].text);
                }

                Item item = new Item();
                item.Kind = ItemKind.MadnessCorruptor;
                item.Button = button;
                item.IntValue = i;
                item.Label = string.IsNullOrWhiteSpace(label) ? Loc.Get("pre_run_corruptor", i + 1) : label;
                item.Details = CorruptorDetails(i);
                _items.Add(item);
            }
        }

        private static string CorruptorDetails(int index)
        {
            string id = null;
            switch (index)
            {
                case 0: id = "impedingdoom"; break;
                case 1: id = "decadence"; break;
                case 2: id = "restrictedpower"; break;
                case 3: id = "resistantmonsters"; break;
                case 4: id = "poverty"; break;
                case 5: id = "overchargedmonsters"; break;
                case 6: id = "randomcombats"; break;
                case 7: id = "despair"; break;
                case 8: id = "equalizer"; break;
            }

            return string.IsNullOrWhiteSpace(id) ? "" : Clean(GameText.Get(id + "desc"));
        }

        private void BuildSandboxItems()
        {
            SandboxManager manager = SandboxManager.Instance;
            List<BotonGeneric> buttons = manager.sandboxWindow
                .GetComponentsInChildren<BotonGeneric>(includeInactive: true)
                .Where(button => IsVisible(button.transform))
                .OrderByDescending(button => button.transform.position.y)
                .ThenBy(button => button.transform.position.x)
                .ToList();

            foreach (IGrouping<string, BotonGeneric> group in buttons
                .Where(button => (button.gameObject.name == "SandboxBox" || button.gameObject.name == "SandboxBoxCheck") && !string.IsNullOrWhiteSpace(button.auxString))
                .GroupBy(button => button.auxString))
            {
                Item item = new Item();
                item.Kind = IsComboKey(group.Key) ? ItemKind.SandboxCombo : ItemKind.SandboxChoice;
                item.Key = group.Key;
                item.Label = Loc.Get("sandbox_" + group.Key);
                item.Buttons.AddRange(group.OrderBy(button => button.auxInt));
                _items.Add(item);
            }

            AddButton(manager.buttonEnable, Loc.Get("pre_run_sandbox_enable"));
            AddButton(manager.buttonDisable, Loc.Get("pre_run_sandbox_disable"));
            AddButton(manager.buttonReset, Loc.Get("pre_run_sandbox_reset"));
            AddButton(manager.buttonMadness, Loc.Get("hero_selection_madness"));
            AddButton(manager.buttonExit, Loc.Get("pre_run_close"));
        }

        private void AddButton(Transform transform, string fallback)
        {
            if (!IsVisible(transform))
            {
                return;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            Item item = new Item();
            item.Kind = ItemKind.Button;
            item.Button = button;
            item.Label = ReadButtonText(transform, fallback);
            _items.Add(item);
        }

        private void AddVisibleTextArray(TMPro.TMP_Text[] texts, string fallback)
        {
            if (texts == null)
            {
                return;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                TMPro.TMP_Text text = texts[i];
                if (text == null || !IsVisible(text.transform))
                {
                    continue;
                }

                string name = Clean(text.text);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                PopupText popup = text.GetComponent<PopupText>();
                string description = popup != null ? Clean(popup.text) : "";

                Item item = new Item();
                item.Kind = ItemKind.Text;
                item.Label = fallback;
                item.Details = string.IsNullOrWhiteSpace(description)
                    ? name
                    : name + ". " + description;
                _items.Add(item);
            }
        }

        private void AddVisibleText(TMPro.TMP_Text text, string fallback)
        {
            if (text == null || !IsVisible(text.transform))
            {
                return;
            }

            string cleaned = Clean(text.text);
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return;
            }

            Item item = new Item();
            item.Kind = ItemKind.Text;
            item.Label = fallback;
            item.Details = cleaned;
            _items.Add(item);
        }

        private void ProcessKeys(bool madnessActive)
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

            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
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

            if (ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                AdjustFocused(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                AdjustFocused(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                ActivateFocused(madnessActive);
            }
        }

        private void Move(int delta)
        {
            if (NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                AnnounceFocused();
            }
        }

        private void Jump(bool end)
        {
            if (NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                AnnounceFocused();
            }
        }

        private void AdjustFocused(int delta)
        {
            Item item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            if (item.Kind == ItemKind.MadnessLevel)
            {
                int currentListIndex = _items.FindIndex(candidate => candidate == item);
                for (int i = currentListIndex + delta; i >= 0 && i < _items.Count; i += delta)
                {
                    if (_items[i].Kind == ItemKind.MadnessLevel)
                    {
                        _index = i;
                        ActivateFocused(madnessActive: true);
                        return;
                    }
                }

                ScreenReader.Say(Loc.Get("pre_run_madness_edge", item.IntValue));
                return;
            }

            if (item.Kind == ItemKind.SandboxCombo)
            {
                SandboxManager.Instance.BoxClick(item.Key, delta);
                AnnounceFocused();
                return;
            }

            if (item.Kind == ItemKind.SandboxChoice)
            {
                AdjustSandboxChoice(item, delta);
            }
        }

        private void ActivateFocused(bool madnessActive)
        {
            Item item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            if (item.Kind == ItemKind.MadnessLevel)
            {
                if (item.Button != null && !item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Label));
                    return;
                }

                MadnessManager.Instance.SelectMadness(item.IntValue);
                ScreenReader.Say(FocusText(item));
                return;
            }

            if (item.Kind == ItemKind.MadnessCorruptor)
            {
                if (item.Button != null && !item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Label));
                    return;
                }

                MadnessManager.Instance.SelectMadnessCorruptor(item.IntValue);
                ScreenReader.Say(FocusText(item));
                return;
            }

            if (item.Kind == ItemKind.SandboxChoice)
            {
                ToggleSandboxChoice(item);
                return;
            }

            if (item.Kind == ItemKind.Button && item.Button != null)
            {
                if (!item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Label));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", item.Label));
                item.Button.Clicked();
            }
        }

        private void AdjustSandboxChoice(Item item, int delta)
        {
            List<int> values = item.Buttons.Select(button => button.auxInt).Distinct().OrderBy(value => value).ToList();
            if (values.Count == 0)
            {
                return;
            }

            int current = SandboxManager.Instance.GetSandboxBoxValue(item.Key);
            int position = values.IndexOf(current);
            if (position < 0)
            {
                position = 0;
            }

            int nextPosition = Mathf.Clamp(position + delta, 0, values.Count - 1);
            if (nextPosition == position && (values.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            SandboxManager.Instance.BoxClick(item.Key, values[nextPosition]);
            AnnounceFocused();
        }

        private void ToggleSandboxChoice(Item item)
        {
            BotonGeneric button = item.Buttons.FirstOrDefault(candidate => candidate.auxInt == 1) ?? item.Buttons.FirstOrDefault();
            if (button == null)
            {
                return;
            }

            SandboxManager.Instance.BoxClick(item.Key, button.auxInt);
            AnnounceFocused();
        }

        private void AnnounceFocused(bool queued = false)
        {
            string text = FocusText(CurrentItem());
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private string FocusText(Item item)
        {
            if (item == null)
            {
                return Loc.Get("no_menu_item");
            }

            switch (item.Kind)
            {
                case ItemKind.MadnessLevel:
                    return string.IsNullOrWhiteSpace(item.Details)
                        ? Loc.Get("pre_run_madness_value", item.IntValue)
                        : Loc.Get("pre_run_madness_value_details", item.IntValue, item.Details);
                case ItemKind.MadnessCorruptor:
                    return Loc.Get("pre_run_toggle_value", item.Label, MadnessManager.Instance.IsMadnessCorruptorSelected(item.IntValue) ? Loc.Get("settings_on") : Loc.Get("settings_off"), item.Details);
                case ItemKind.SandboxCombo:
                    return Loc.Get("pre_run_sandbox_value", item.Label, SandboxComboValue(item.Key));
                case ItemKind.SandboxChoice:
                    return Loc.Get("pre_run_sandbox_value", item.Label, SandboxChoiceValue(item));
                case ItemKind.Text:
                    return Loc.Get("pre_run_text_item", item.Label, item.Details);
                default:
                    return Loc.Get("hero_selection_action_focus", item.Label);
            }
        }

        private static string SandboxComboValue(string key)
        {
            GameObject display = GameObject.Find(key);
            TMPro.TMP_Text text = display != null ? display.GetComponent<TMPro.TMP_Text>() : null;
            return text != null ? Clean(text.text) : "";
        }

        private static string SandboxChoiceValue(Item item)
        {
            int value = SandboxManager.Instance.GetSandboxBoxValue(item.Key);
            if (item.Key == "sbTotalHeroes")
            {
                return value == 0 ? Loc.Get("pre_run_sandbox_four_heroes") : Loc.Get("pre_run_sandbox_heroes", value);
            }

            if (item.Key == "sbLessMonsters")
            {
                return value == 0 ? Loc.Get("settings_off") : Loc.Get("pre_run_sandbox_less_monsters", value);
            }

            return value == 0 ? Loc.Get("settings_off") : Loc.Get("settings_on");
        }

        private Item CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private static bool IsComboKey(string key)
        {
            return key == "sbEnergy" ||
                   key == "sbSpeed" ||
                   key == "sbGold" ||
                   key == "sbShards" ||
                   key == "sbCraftCost" ||
                   key == "sbUpgradeCost" ||
                   key == "sbTransformCost" ||
                   key == "sbRemoveCost" ||
                   key == "sbEquipmentCost" ||
                   key == "sbPetsCost" ||
                   key == "sbDivinationCost" ||
                   key == "sbMonstersHP" ||
                   key == "sbMonstersDamage";
        }

        private static string ReadButtonText(Transform transform, string fallback)
        {
            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            if (button != null)
            {
                string text = Clean(button.GetText());
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return fallback;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy && Functions.TransformIsVisible(transform);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(index, 0, count - 1);
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static bool IsSubmitHeld()
        {
            return ModInput.GetKey(KeyCode.Return) || ModInput.GetKey(KeyCode.KeypadEnter) || ModInput.GetKey(KeyCode.Space);
        }

        internal static void NotifyShowMadness()
        {
            _openedByShowMadness = true;
        }

        internal static void NotifyCloseMadness()
        {
            _openedByShowMadness = false;
        }

        internal static void NotifyShowSandbox()
        {
            _openedByShowSandbox = true;
        }

        internal static void NotifyCloseSandbox()
        {
            _openedByShowSandbox = false;
        }
    }

    [HarmonyPatch(typeof(MadnessManager), nameof(MadnessManager.ShowMadness))]
    internal static class MadnessManagerShowMadnessPatch
    {
        private static void Postfix()
        {
            PreRunOptionsHandler.NotifyShowMadness();
        }
    }

    [HarmonyPatch(typeof(MadnessManager), nameof(MadnessManager.CloseMadness))]
    internal static class MadnessManagerCloseMadnessPatch
    {
        private static void Postfix()
        {
            PreRunOptionsHandler.NotifyCloseMadness();
        }
    }

    [HarmonyPatch(typeof(SandboxManager), nameof(SandboxManager.ShowSandbox))]
    internal static class SandboxManagerShowSandboxPatch
    {
        private static void Postfix()
        {
            PreRunOptionsHandler.NotifyShowSandbox();
        }
    }

    [HarmonyPatch(typeof(SandboxManager), nameof(SandboxManager.CloseSandbox))]
    internal static class SandboxManagerCloseSandboxPatch
    {
        private static void Postfix()
        {
            PreRunOptionsHandler.NotifyCloseSandbox();
        }
    }
}
