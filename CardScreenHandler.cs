using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Reads the game's native full card and item detail screen.
    /// </summary>
    public sealed class CardScreenHandler
    {
        private static readonly FieldInfo CardDataField = AccessTools.Field(typeof(CardScreenManager), "cardData");
        private static CardData _openedCardData;
        private readonly List<string> _lines = new List<string>();
        private CardData _activeCardData;
        private int _lineIndex;
        private int _focusIndex;
        private bool _announced;

        /// <summary>
        /// Opens the native card detail screen and prepares accessible speech for it.
        /// </summary>
        public static void Open(CardData cardData)
        {
            if (cardData == null || CardScreenManager.Instance == null)
            {
                ScreenReader.Say(Loc.Get("unknown_card"));
                return;
            }

            _openedCardData = cardData;
            CardScreenManager.Instance.ShowCardScreen(_state: true);
            CardScreenManager.Instance.SetCardData(cardData);
        }

        internal static void TrackNativeCardData(CardData cardData)
        {
            if (cardData != null)
            {
                _openedCardData = cardData;
            }
        }

        /// <summary>
        /// Updates active card detail navigation.
        /// </summary>
        public bool Update()
        {
            if (CardScreenManager.Instance == null || !CardScreenManager.Instance.IsActive())
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.CardScreen);
            CardData currentCardData = ReadCurrentCardData();
            if (_activeCardData != currentCardData)
            {
                Rebuild(currentCardData);
            }

            AnnounceOnce();
            ProcessKeys();
            return true;
        }

        private static CardData ReadCurrentCardData()
        {
            if (CardScreenManager.Instance == null)
            {
                return _openedCardData;
            }

            CardData managerData = CardDataField != null ? CardDataField.GetValue(CardScreenManager.Instance) as CardData : null;
            return managerData ?? _openedCardData;
        }

        private void Rebuild(CardData cardData)
        {
            _activeCardData = cardData;
            _lines.Clear();
            _lineIndex = 0;
            _announced = false;

            if (cardData == null)
            {
                return;
            }

            if (cardData.Item != null || cardData.ItemEnchantment != null)
            {
                _lines.AddRange(CardSpeech.BuildItemOverviewLines(cardData));
                _lines.AddRange(CardSpeech.BuildItemEffectLines(cardData));
            }
            else
            {
                _lines.AddRange(CardSpeech.BuildCardLines(cardData, cardData.EnergyCost));
            }
        }

        private void AnnounceOnce()
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            if (_activeCardData == null)
            {
                ScreenReader.Say(Loc.Get("unknown_card"));
                return;
            }

            ScreenReader.Say(Loc.Get("card_screen_opened", CardSpeech.CardNameWithRarity(_activeCardData)));
            ScreenReader.SayQueued(Loc.Get("card_screen_controls"));
            if (_lines.Count > 0)
            {
                ScreenReader.SayQueued(_lines[0]);
            }
        }

        private void ProcessKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveLine(1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveLine(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Home))
            {
                JumpLine(false);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.End))
            {
                JumpLine(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Home))
            {
                MoveFocus(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.End))
            {
                MoveFocus(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (_focusIndex == 1)
                {
                    CloseCardScreen();
                }
                else
                {
                    AnnounceCurrentDetailFocus();
                }

                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseCardScreen();
            }
        }

        private void MoveFocus(bool forward)
        {
            int delta = forward ? 1 : -1;
            if (!NavigationBounds.TryMove(ref _focusIndex, delta, 2))
            {
                return;
            }

            ScreenReader.Say(FocusText());
        }

        private void AnnounceCurrentDetailFocus()
        {
            ScreenReader.Say(FocusText());
        }

        private string FocusText()
        {
            return _focusIndex == 1 ? Loc.Get("card_screen_close_button") : CurrentDetailFocusText();
        }

        private string CurrentDetailFocusText()
        {
            if (_activeCardData == null)
            {
                return Loc.Get("unknown_card");
            }

            return Loc.Get("card_screen_detail_focus", CardSpeech.CardNameWithRarity(_activeCardData));
        }

        private void CloseCardScreen()
        {
            TomeHandler.BlockTomeCloseUntilEscapeRelease();
            CardScreenManager.Instance.ShowCardScreen(_state: false);
            ScreenReader.Say(Loc.Get("card_screen_closed"));
            Reset();
        }

        private void MoveLine(int delta)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("card_screen_no_details"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, _lines.Count))
            {
                return;
            }

            ScreenReader.Say(_lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            if (_lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("card_screen_no_details"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, _lines.Count))
            {
                return;
            }

            ScreenReader.Say(_lines[_lineIndex]);
        }

        private void Reset()
        {
            _activeCardData = null;
            _lineIndex = 0;
            _focusIndex = 0;
            _announced = false;
            _lines.Clear();
        }
    }

    [HarmonyPatch(typeof(CardScreenManager), "SetCardData")]
    internal static class CardScreenManagerSetCardDataPatch
    {
        private static void Prefix(CardData _cardData)
        {
            CardScreenHandler.TrackNativeCardData(_cardData);
        }
    }
}
