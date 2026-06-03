using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for end-of-run unlock and result screens.
    /// </summary>
    public sealed class FinishRunHandler
    {
        private sealed class FinishItem
        {
            public string Summary;
            public readonly List<string> Lines = new List<string>();
            public CardItem Card;
            public bool CloseUnlocks;
            public bool MainMenu;
        }

        private readonly List<FinishItem> _items = new List<FinishItem>();
        private int _index;
        private int _lineIndex;
        private bool _announced;
        private bool _wasActive;
        private bool _wasUnlockWindow;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates finish-run navigation.
        /// </summary>
        public bool Update()
        {
            FinishRunManager finish = FinishRunManager.Instance;
            bool active = finish != null && finish.gameObject.activeInHierarchy;
            if (!active)
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.FinishRun);
            bool unlockWindow = IsUnlockWindowActive(finish);
            if (!_wasActive || _wasUnlockWindow != unlockWindow)
            {
                _announced = false;
                _index = 0;
                _lineIndex = 0;
                _wasActive = true;
                _wasUnlockWindow = unlockWindow;
            }

            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(finish, unlockWindow);
                AnnounceOnce(unlockWindow);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(finish, unlockWindow);
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _lineIndex = 0;
            _announced = false;
            _wasActive = false;
            _wasUnlockWindow = false;
        }

        private static bool IsUnlockWindowActive(FinishRunManager finish)
        {
            return finish.characterWindow != null && finish.characterWindow.IsActive();
        }

        private void Refresh(FinishRunManager finish, bool unlockWindow)
        {
            _items.Clear();
            if (unlockWindow)
            {
                AddUnlockWindowItems(finish);
            }
            else
            {
                AddFinishSummaryItems(finish);
            }

            _index = ClampIndex(_index, _items.Count);
            _lineIndex = ClampIndex(_lineIndex, CurrentLinesCount());
        }

        private void AddUnlockWindowItems(FinishRunManager finish)
        {
            DeckWindowUI deck = finish.characterWindow != null ? finish.characterWindow.deckWindow : null;
            FinishItem header = new FinishItem();
            AddLine(header, Loc.Get("finish_unlocks_screen"));
            if (deck != null && deck.unlockedTitle != null && deck.unlockedTitle.gameObject.activeInHierarchy)
            {
                AddLine(header, ReadTransformText(deck.unlockedTitle, ""));
            }

            header.Summary = string.Join(" ", header.Lines.ToArray());
            _items.Add(header);

            if (deck != null && deck.unlockedContent != null)
            {
                foreach (Transform child in deck.unlockedContent)
                {
                    CardItem card = child != null ? child.GetComponent<CardItem>() : null;
                    if (card == null || card.CardData == null || !card.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    FinishItem item = BuildCardItem(card);
                    _items.Add(item);
                }
            }

            FinishItem close = new FinishItem();
            close.CloseUnlocks = true;
            AddLine(close, Loc.Get("finish_close_unlocks"));
            close.Summary = close.Lines[0];
            _items.Add(close);
        }

        private static FinishItem BuildCardItem(CardItem card)
        {
            FinishItem item = new FinishItem();
            item.Card = card;
            CardData data = card.CardData;
            AddLine(item, Clean(data.CardName));
            AddLine(item, Loc.Get("combat_card_type", GameText.CardTypeName(data.CardType)));
            AddLine(item, Loc.Get("combat_card_target", Clean(data.Target)));
            AddNumberLine(item, "combat_card_damage", data.DamagePreCalculated, data.Damage);
            AddNumberLine(item, "combat_card_heal", data.Heal, 0);
            AddLine(item, Loc.Get("combat_card_description", Clean(!string.IsNullOrWhiteSpace(data.DescriptionNormalized) ? data.DescriptionNormalized : data.Description)));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private void AddFinishSummaryItems(FinishRunManager finish)
        {
            FinishItem summary = new FinishItem();
            AddLine(summary, Loc.Get("finish_result_screen"));
            AddLine(summary, Clean(finish.subHeader != null ? finish.subHeader.text : ""));
            AddLabelValue(summary, "finish_places", finish.placesText, finish.placesTextGold);
            AddLabelValue(summary, "finish_expertise", finish.deathText, finish.deathTextGold);
            AddLabelValue(summary, "finish_deaths", finish.deathsText, finish.deathsTextGold);
            AddLabelValue(summary, "finish_experience", finish.experienceText, finish.experienceTextGold);
            AddLabelValue(summary, "finish_bosses", finish.bossesText, finish.bossesTextGold);
            AddLabelValue(summary, "finish_corruptions", finish.corruptionsText, finish.corruptionsTextGold);
            if (finish.completedBlock != null && finish.completedBlock.gameObject.activeInHierarchy)
            {
                AddLine(summary, Loc.Get("finish_completed", ReadText(finish.completedText), ReadText(finish.completedTextGold)));
            }

            AddLine(summary, Loc.Get("finish_final_score", ReadText(finish.finalScore)));
            AddLine(summary, Clean(finish.finalScoreMadness != null ? finish.finalScoreMadness.text : ""));
            AddLine(summary, Clean(finish.playedTimeText != null ? finish.playedTimeText.text : ""));
            AddLine(summary, Loc.Get("finish_reward", ReadText(finish.totalTextGold)));
            AddLine(summary, Clean(finish.gameReward != null ? finish.gameReward.text : ""));
            AddLine(summary, Clean(finish.mpBonus != null ? finish.mpBonus.text : ""));
            summary.Summary = string.Join(" ", summary.Lines.ToArray());
            _items.Add(summary);

            AddProgressItem(finish.fp0, 1);
            AddProgressItem(finish.fp1, 2);
            AddProgressItem(finish.fp2, 3);
            AddProgressItem(finish.fp3, 4);

            FinishItem mainMenu = new FinishItem();
            mainMenu.MainMenu = true;
            AddLine(mainMenu, ReadButtonText(finish.mainMenuButton, Loc.Get("finish_main_menu")));
            mainMenu.Summary = mainMenu.Lines[0];
            _items.Add(mainMenu);
        }

        private void AddProgressItem(FinishProgression progress, int position)
        {
            if (progress == null || !progress.IsActive())
            {
                return;
            }

            FinishItem item = new FinishItem();
            AddLine(item, Loc.Get("finish_character_progress", position, ReadText(progress.charName)));
            AddLine(item, Clean(progress.charRank != null ? progress.charRank.text : ""));
            AddLine(item, Loc.Get("finish_progress_points", ReadText(progress.charProgress), ReadText(progress.charMin), ReadText(progress.charMax)));
            AddLine(item, Clean(progress.charPoints != null && progress.charPoints.gameObject.activeInHierarchy ? progress.charPoints.text : ""));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private static void AddLabelValue(FinishItem item, string key, TMP_Text valueText, TMP_Text scoreText)
        {
            string value = ReadText(valueText);
            string score = ReadText(scoreText);
            if (!string.IsNullOrWhiteSpace(value) || !string.IsNullOrWhiteSpace(score))
            {
                AddLine(item, Loc.Get(key, value, score));
            }
        }

        private void ProcessKeys(FinishRunManager finish, bool unlockWindow)
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

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                Move(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(finish, unlockWindow);
            }
        }

        private void Move(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void Jump(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void MoveLine(int delta)
        {
            FinishItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            FinishItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void Activate(FinishRunManager finish, bool unlockWindow)
        {
            FinishItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            if (item.CloseUnlocks && finish.characterWindow != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                finish.characterWindow.Hide();
                _announced = false;
                return;
            }

            if (item.MainMenu)
            {
                if (finish.mainMenuButton == null || !finish.mainMenuButton.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated_loading", item.Summary));
                finish.mainMenuButton.Clicked();
                return;
            }

            ScreenReader.Say(item.Summary);
        }

        private void AnnounceOnce(bool unlockWindow)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(unlockWindow ? Loc.Get("finish_unlocks_screen") : Loc.Get("finish_result_screen"));
            AnnounceFocused(true);
        }

        private void AnnounceFocused(bool queued = false)
        {
            FinishItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("finish_no_item"));
                return;
            }

            string text = item.Summary;
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private FinishItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private int CurrentLinesCount()
        {
            FinishItem item = CurrentItem();
            return item != null ? item.Lines.Count : 0;
        }

        private static string ReadButtonText(BotonGeneric button, string fallback)
        {
            string text = button != null ? Clean(button.GetText()) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string ReadTransformText(Transform transform, string fallback)
        {
            TMP_Text text = transform != null ? transform.GetComponentInChildren<TMP_Text>(true) : null;
            string value = text != null ? Clean(text.text) : "";
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static string ReadText(TMP_Text text)
        {
            return text != null ? Clean(text.text) : "";
        }

        private static void AddNumberLine(FinishItem item, string key, int primary, int fallback)
        {
            int value = primary != 0 ? primary : fallback;
            if (value != 0)
            {
                AddLine(item, Loc.Get(key, value));
            }
        }

        private static void AddLine(FinishItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
