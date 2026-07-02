using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides global hotkeys for reading current run currencies.
    /// </summary>
    public sealed class CurrencyHotkeyHandler
    {
        /// <summary>
        /// Updates global currency hotkeys.
        /// </summary>
        public bool Update()
        {
            if (TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (!ctrl)
            {
                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.G))
            {
                AnnounceGold();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.D))
            {
                AnnounceDust();
                return true;
            }

            if (ModInput.GetKeyDown(KeyCode.S))
            {
                AnnounceSupply();
                return true;
            }

            return false;
        }

        private static void AnnounceGold()
        {
            AtOManager ato = AtOManager.Instance;
            if (ato == null)
            {
                ScreenReader.Say(Loc.Get("currency_unavailable"));
                return;
            }

            ScreenReader.Say(Loc.Get("currency_gold", ato.CurrencyManager.GetPlayerGold()));
        }

        private static void AnnounceDust()
        {
            AtOManager ato = AtOManager.Instance;
            if (ato == null)
            {
                ScreenReader.Say(Loc.Get("currency_unavailable"));
                return;
            }

            ScreenReader.Say(Loc.Get("currency_dust", ato.CurrencyManager.GetPlayerDust()));
        }

        private static void AnnounceSupply()
        {
            PlayerManager player = PlayerManager.Instance;
            if (player == null)
            {
                ScreenReader.Say(Loc.Get("currency_unavailable"));
                return;
            }

            ScreenReader.Say(Loc.Get("currency_supply", player.GetPlayerSupplyActual()));
        }

        private static void AnnounceAll()
        {
            AtOManager ato = AtOManager.Instance;
            PlayerManager player = PlayerManager.Instance;
            if (ato == null && player == null)
            {
                ScreenReader.Say(Loc.Get("currency_unavailable"));
                return;
            }

            int gold = ato != null ? ato.CurrencyManager.GetPlayerGold() : 0;
            int dust = ato != null ? ato.CurrencyManager.GetPlayerDust() : 0;
            int supply = player != null ? player.GetPlayerSupplyActual() : 0;
            ScreenReader.Say(Loc.Get("currency_all", gold, dust, supply));
        }
    }
}
