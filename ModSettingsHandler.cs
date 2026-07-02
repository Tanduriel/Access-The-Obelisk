using System.Collections.Generic;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides the accessibility mod settings menu.
    /// </summary>
    public sealed class ModSettingsHandler
    {
        private enum SettingKind
        {
            MapDetails,
            EnemyPlayedCards,
            RepeatSingleItem,
            DeathEffectRemovals,
            Close
        }

        private readonly List<SettingKind> _items = new List<SettingKind>
        {
            SettingKind.MapDetails,
            SettingKind.EnemyPlayedCards,
            SettingKind.RepeatSingleItem,
            SettingKind.DeathEffectRemovals,
            SettingKind.Close
        };

        private bool _isOpen;
        private int _index;

        /// <summary>
        /// Updates the mod settings menu and its global open hotkey.
        /// </summary>
        public bool Update()
        {
            if (!_isOpen)
            {
                return TryOpen();
            }

            AccessStateManager.SetState(AccessState.ModSettings);
            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1);
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

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                Activate();
                return true;
            }

            return true;
        }

        private bool TryOpen()
        {
            if (TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (!ctrl || !ModInput.GetKeyDown(KeyCode.M))
            {
                return false;
            }

            _isOpen = true;
            _index = 0;
            ScreenReader.Say(Loc.Get("mod_settings_screen"));
            ScreenReader.SayQueued(Loc.Get("mod_settings_controls"));
            AnnounceFocused();
            return true;
        }

        private void Move(int delta)
        {
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            AnnounceFocused();
        }

        private void Jump(bool end)
        {
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            AnnounceFocused();
        }

        private void Activate()
        {
            SettingKind item = _items[_index];
            if (item == SettingKind.Close)
            {
                Close();
                return;
            }

            if (item == SettingKind.MapDetails)
            {
                ModSettings.MapDetailsEnabled = !ModSettings.MapDetailsEnabled;
            }
            else if (item == SettingKind.EnemyPlayedCards)
            {
                ModSettings.EnemyPlayedCardsEnabled = !ModSettings.EnemyPlayedCardsEnabled;
            }
            else if (item == SettingKind.RepeatSingleItem)
            {
                ModSettings.RepeatSingleItemEnabled = !ModSettings.RepeatSingleItemEnabled;
            }
            else if (item == SettingKind.DeathEffectRemovals)
            {
                ModSettings.DeathEffectRemovalsEnabled = !ModSettings.DeathEffectRemovalsEnabled;
            }

            AnnounceFocused();
        }

        private void Close()
        {
            _isOpen = false;
            ScreenReader.Say(Loc.Get("mod_settings_closed"));
        }

        private void AnnounceFocused()
        {
            ScreenReader.Say(FormatItem(_items[_index]));
        }

        private static string FormatItem(SettingKind item)
        {
            if (item == SettingKind.MapDetails)
            {
                return Loc.Get("mod_settings_checkbox", Loc.Get("mod_settings_map_details"), StateText(ModSettings.MapDetailsEnabled));
            }

            if (item == SettingKind.EnemyPlayedCards)
            {
                return Loc.Get("mod_settings_checkbox", Loc.Get("mod_settings_enemy_played_cards"), StateText(ModSettings.EnemyPlayedCardsEnabled));
            }

            if (item == SettingKind.RepeatSingleItem)
            {
                return Loc.Get("mod_settings_checkbox", Loc.Get("mod_settings_repeat_single_item"), StateText(ModSettings.RepeatSingleItemEnabled));
            }

            if (item == SettingKind.DeathEffectRemovals)
            {
                return Loc.Get("mod_settings_checkbox", Loc.Get("mod_settings_death_effect_removals"), StateText(ModSettings.DeathEffectRemovalsEnabled));
            }

            return Loc.Get("mod_settings_close");
        }

        private static string StateText(bool enabled)
        {
            return enabled ? Loc.Get("settings_on") : Loc.Get("settings_off");
        }
    }
}
