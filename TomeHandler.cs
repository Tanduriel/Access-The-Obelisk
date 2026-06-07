using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides accessible navigation for the game's native Tome of Knowledge screen.
    /// </summary>
    public sealed class TomeHandler
    {
        private sealed class TomeItem
        {
            public string Summary;
            public CardData Card;
            public readonly List<string> Lines = new List<string>();
            public Action Activate;
        }

        private readonly List<TomeItem> _items = new List<TomeItem>();
        private int _itemIndex;
        private int _lineIndex;
        private bool _announced;
        private static bool _openedByShowTome;
        private static bool _blockCloseUntilEscapeRelease;
        private string _lastSection = "";
        private int _lastCount;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates Tome hotkeys and active Tome navigation.
        /// </summary>
        public bool Update()
        {
            if (TryCloseCardDetail())
            {
                return true;
            }

            if (TryOpenHotkey())
            {
                return true;
            }

            TomeManager tome = TomeManager.Instance;
            if (tome == null || !IsAccessibleTomeOpen(tome))
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Tome);
            if (IsSearchFocused(tome))
            {
                HandleSearchFocus(tome);
                return true;
            }

            string section = ActiveSection(tome);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(tome, section);
                AnnounceTome(section);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(tome, section);
            return true;
        }

        private static bool TryCloseCardDetail()
        {
            if (TomeManager.Instance == null || !IsAccessibleTomeOpen(TomeManager.Instance) || CardScreenManager.Instance == null || !CardScreenManager.Instance.IsActive())
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CardScreenManager.Instance.ShowCardScreen(_state: false);
                ScreenReader.Say(Loc.Get("tome_card_detail_closed"));
            }

            return true;
        }

        private bool TryOpenHotkey()
        {
            if (!Input.GetKeyDown(KeyCode.B) || TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            TomeManager tome = TomeManager.Instance;
            if (tome == null || !CanOpenTome())
            {
                ScreenReader.Say(Loc.Get("tome_unavailable"));
                return true;
            }

            if (tome.IsActive())
            {
                if (CanReattachOpenTome(tome))
                {
                    _openedByShowTome = true;
                    ScreenReader.Say(Loc.Get("tome_opened"));
                    ResetForNewView();
                    return true;
                }

                ScreenReader.Say(Loc.Get("tome_already_open"));
                return true;
            }

            if (TeamManagement.Instance != null)
            {
                TeamManagement.Instance.EnableDisableTestingPanels(state: false);
            }

            tome.ShowTome(_status: true);
            _openedByShowTome = true;
            ScreenReader.Say(Loc.Get("tome_opened"));
            ResetForNewView();
            return true;
        }

        private static bool IsAccessibleTomeOpen(TomeManager tome)
        {
            return tome != null && _openedByShowTome && tome.IsActive() && tome.content != null && IsVisible(tome.content.transform);
        }

        private static bool CanReattachOpenTome(TomeManager tome)
        {
            return tome != null && !_openedByShowTome && tome.IsActive() && tome.content != null && IsVisible(tome.content.transform);
        }

        private static bool CanOpenTome()
        {
            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return false;
            }

            if (SettingsManager.Instance != null && SettingsManager.Instance.IsActive())
            {
                return false;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsTutorialActive())
            {
                return false;
            }

            if (CardScreenManager.Instance != null && CardScreenManager.Instance.IsActive())
            {
                return false;
            }

            if (DamageMeterManager.Instance != null && DamageMeterManager.Instance.IsActive())
            {
                return false;
            }

            if (MatchManager.Instance != null && (MatchManager.Instance.CombatLoading || MatchManager.Instance.CardDrag))
            {
                return false;
            }

            if (FinishRunManager.Instance != null)
            {
                return false;
            }

            return true;
        }

        private void Refresh(TomeManager tome, string section)
        {
            _items.Clear();
            AddSectionButtons(tome);
            if (section == "main")
            {
                AddVisibleTextItems(tome.mainSection, Loc.Get("tome_main_stat"));
            }
            else if (section == "cards" || section == "items")
            {
                AddTomeButtons(tome, section);
                AddVisibleCards(tome.cardContainer);
                AddPageActions(tome);
            }
            else if (section == "glossary")
            {
                AddGlossaryItems(tome);
                AddPageActions(tome);
            }
            else if (section == "runs")
            {
                AddRunsItems(tome);
                AddPageActions(tome);
            }
            else if (section == "scoreboard")
            {
                AddScoreboardItems(tome);
                AddPageActions(tome);
            }
            else
            {
                AddVisibleTextItems(tome.content != null ? tome.content.transform : null, Loc.Get("tome_information"));
            }

            AddExitItem(tome);
            if (_itemIndex >= _items.Count)
            {
                _itemIndex = _items.Count - 1;
            }

            if (_itemIndex < 0)
            {
                _itemIndex = 0;
            }

            if (_lastCount != _items.Count)
            {
                _lineIndex = 0;
                _lastCount = _items.Count;
            }
        }

        private void AddSectionButtons(TomeManager tome)
        {
            AddButton(tome.mainSectionButtonT, Loc.Get("tome_section_main"));
            AddButton(tome.cardsSectionButtonT, Loc.Get("tome_section_cards"));
            AddButton(tome.itemsSectionButtonT, Loc.Get("tome_section_items"));
            AddButton(tome.glossarySectionButtonT, Loc.Get("tome_section_glossary"));
            AddButton(tome.runsSectionButtonT, Loc.Get("tome_section_runs"));
            AddButton(tome.scoreboardSectionButtonT, Loc.Get("tome_section_scoreboard"));
        }

        private void AddButton(Transform transform, string fallback)
        {
            if (!IsVisible(transform))
            {
                return;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            string text = button != null ? Clean(button.GetText()) : "";
            AddActionItem(string.IsNullOrWhiteSpace(text) ? fallback : text, () => button?.Clicked());
        }

        private void AddTomeButtons(TomeManager tome, string section)
        {
            if (tome.TomeButtons == null)
            {
                return;
            }

            foreach (TomeButton button in tome.TomeButtons)
            {
                if (button == null || !IsVisible(button.transform))
                {
                    continue;
                }

                int tomeClass = button.tomeClass;
                if (section == "cards" && (tomeClass < -1 || tomeClass > 6 || tomeClass == 4))
                {
                    continue;
                }

                if (section == "items" && tomeClass != 7 && tomeClass != 8 && tomeClass != 9 && tomeClass != 10 && tomeClass != 11 && tomeClass != 22)
                {
                    continue;
                }

                string label = TomeButtonName(tomeClass);
                AddActionItem(label, () => ActivateTomeButton(button));
            }
        }

        private void AddVisibleCards(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            CardItem[] cards = parent.GetComponentsInChildren<CardItem>(true);
            foreach (CardItem card in cards)
            {
                if (card == null || card.CardData == null || !IsVisible(card.transform))
                {
                    continue;
                }

                if (card.transform.localScale.x < 0.2f || card.transform.localScale.y < 0.07f)
                {
                    continue;
                }

                _items.Add(BuildCardItem(card.CardData, card.GetEnergyCost()));
            }
        }

        private void AddGlossaryItems(TomeManager tome)
        {
            if (IsVisible(tome.glossaryIndex))
            {
                AddGlossaryIndex(tome.glossaryPageIndex);
                AddGlossaryIndex(tome.glossaryPageIndex2);
                return;
            }

            if (tome.glossaryTexts == null)
            {
                return;
            }

            foreach (TMP_Text text in tome.glossaryTexts)
            {
                AddTextItem(text, Loc.Get("tome_glossary_entry"));
            }
        }

        private void AddGlossaryIndex(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (!IsVisible(child) || child.childCount == 0)
                {
                    continue;
                }

                BotonGeneric button = child.GetChild(0).GetComponent<BotonGeneric>();
                if (button == null || button.auxInt < 0)
                {
                    continue;
                }

                string label = Clean(button.GetText());
                AddActionItem(string.IsNullOrWhiteSpace(label) ? Loc.Get("tome_glossary_entry") : label, () => button.Clicked());
            }
        }

        private void AddRunsItems(TomeManager tome)
        {
            if (IsVisible(tome.runDetails))
            {
                AddRunDetailItems(tome.runDetails.GetComponent<TomeRunDetails>());
                return;
            }

            if (tome.runsContainer == null)
            {
                return;
            }

            foreach (Transform child in tome.runsContainer)
            {
                if (!IsVisible(child))
                {
                    continue;
                }

                TomeRun run = child.GetComponent<TomeRun>();
                if (run == null)
                {
                    continue;
                }

                int index = ParseTrailingIndex(child.name);
                string text = ReadVisibleText(child);
                AddActionItem(string.IsNullOrWhiteSpace(text) ? Loc.Get("tome_run") : text, () => TomeManager.Instance.DoRun(index));
            }
        }

        private void AddRunDetailItems(TomeRunDetails details)
        {
            if (details == null)
            {
                return;
            }

            AddTomeRunDetailButton(details.tomeButtons, 0, Loc.Get("tome_run_path"));
            AddTomeRunDetailButton(details.tomeButtons, 1, Loc.Get("tome_run_character"));
            AddTomeRunDetailButton(details.tomeButtons, 2, Loc.Get("tome_run_character"));
            AddTomeRunDetailButton(details.tomeButtons, 3, Loc.Get("tome_run_character"));
            AddTomeRunDetailButton(details.tomeButtons, 4, Loc.Get("tome_run_character"));
            AddVisibleTextItems(details.transform, Loc.Get("tome_run_detail"));
            AddCardVerticalItems(details.cardListContainer);
            AddActionItem(Loc.Get("tome_close_run_detail"), () => TomeManager.Instance.RunDetailClose());
        }

        private void AddTomeRunDetailButton(TomeButton[] buttons, int index, string fallback)
        {
            if (buttons == null || index < 0 || index >= buttons.Length || buttons[index] == null || !IsVisible(buttons[index].transform))
            {
                return;
            }

            TomeButton button = buttons[index];
            string label = ReadVisibleText(button.transform);
            AddActionItem(string.IsNullOrWhiteSpace(label) ? fallback : label, () => ActivateTomeButton(button));
        }

        private void AddCardVerticalItems(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (!IsVisible(child))
                {
                    continue;
                }

                CardVertical vertical = child.GetComponent<CardVertical>();
                if (vertical != null && vertical.cardData != null)
                {
                    _items.Add(BuildCardItem(vertical.cardData, vertical.cardData.EnergyCost));
                }
            }
        }

        private void AddScoreboardItems(TomeManager tome)
        {
            AddTextItem(tome.scoreTitle, Loc.Get("tome_scoreboard_title"));
            AddTextItem(tome.scoreTitleSub, Loc.Get("tome_scoreboard_title"));
            AddTextItem(tome.scoreStatus, Loc.Get("tome_scoreboard_status"));
            if (tome.scoreButtons != null)
            {
                foreach (Transform button in tome.scoreButtons)
                {
                    AddButton(button, Loc.Get("tome_scoreboard_button"));
                }
            }

            AddButton(tome.buttonPrevWeekly != null ? tome.buttonPrevWeekly.transform : null, Loc.Get("tome_previous_week"));
            AddButton(tome.buttonNextWeekly != null ? tome.buttonNextWeekly.transform : null, Loc.Get("tome_next_week"));

            if (tome.scoresName == null)
            {
                return;
            }

            foreach (Score score in tome.scoresName)
            {
                if (score == null || !IsVisible(score.transform))
                {
                    continue;
                }

                string row = Clean(string.Join(" ", new[]
                {
                    score.index != null ? score.index.text : "",
                    score.name != null ? score.name.text : "",
                    score.score != null ? score.score.text : ""
                }));
                if (!string.IsNullOrWhiteSpace(row))
                {
                    AddSimpleItem(row);
                }
            }
        }

        private void AddPageActions(TomeManager tome)
        {
            if (tome.IsTherePrev())
            {
                AddActionItem(Loc.Get("tome_previous_page"), () => tome.DoPrevPage());
            }

            if (tome.IsThereNext())
            {
                AddActionItem(Loc.Get("tome_next_page"), () => tome.DoNextPage());
            }
        }

        private void AddExitItem(TomeManager tome)
        {
            AddActionItem(Loc.Get("tome_close"), () => CloseTome(tome));
        }

        private void AddVisibleTextItems(Transform root, string label)
        {
            if (root == null)
            {
                return;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            HashSet<string> seen = new HashSet<string>();
            foreach (TMP_Text text in texts)
            {
                if (text == null || !IsVisible(text.transform))
                {
                    continue;
                }

                string value = Clean(text.text);
                if (string.IsNullOrWhiteSpace(value) || seen.Contains(value))
                {
                    continue;
                }

                seen.Add(value);
                AddSimpleItem(Loc.Get("tome_labeled_value", label, value));
            }
        }

        private void AddTextItem(TMP_Text text, string label)
        {
            if (text == null || !IsVisible(text.transform))
            {
                return;
            }

            string value = Clean(text.text);
            if (!string.IsNullOrWhiteSpace(value))
            {
                AddSimpleItem(Loc.Get("tome_labeled_value", label, value));
            }
        }

        private static TomeItem BuildCardItem(CardData data, int energyCost)
        {
            TomeItem item = new TomeItem();
            item.Card = data;
            if (data != null && data.CardClass == Enums.CardClass.Item)
            {
                item.Lines.AddRange(CardSpeech.BuildItemOverviewLines(data));
                List<string> effectLines = CardSpeech.BuildItemEffectLines(data);
                for (int i = 1; i < effectLines.Count; i++)
                {
                    AddLine(item, effectLines[i]);
                }

                item.Summary = CardSpeech.BuildItemFocusSummary(data);
            }
            else
            {
                item.Lines.AddRange(CardSpeech.BuildCardLines(data, energyCost));
                item.Summary = CardSpeech.BuildCardFocusSummary(data, energyCost);
            }

            return item;
        }

        private void ProcessKeys(TomeManager tome, string section)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.F))
            {
                FocusSearch(tome);
                return;
            }

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

            if (ctrl && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSection(tome, -1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSection(tome, 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                if (tome.IsTherePrev())
                {
                    tome.DoPrevPage();
                    ScreenReader.Say(Loc.Get("tome_previous_page"));
                    ResetForNewView();
                }
                else
                {
                    MoveItem(-1);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                if (tome.IsThereNext())
                {
                    tome.DoNextPage();
                    ScreenReader.Say(Loc.Get("tome_next_page"));
                    ResetForNewView();
                }
                else
                {
                    MoveItem(1);
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpItem(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveItem(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveItem(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                ActivateFocused();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape(tome, section);
            }
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("tome_no_items"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _itemIndex, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void JumpItem(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("tome_no_items"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _itemIndex, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void MoveLine(int delta)
        {
            TomeItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("tome_no_items"));
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
            TomeItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("tome_no_items"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void MoveSection(TomeManager tome, int delta)
        {
            string[] sections = { "main", "cards", "items", "glossary", "runs", "scoreboard" };
            int index = Array.IndexOf(sections, ActiveSection(tome));
            if (index < 0)
            {
                index = 0;
            }

            int nextIndex = NavigationBounds.ClampIndex(index + delta, sections.Length);
            if (nextIndex == index)
            {
                return;
            }

            string next = sections[nextIndex];
            ActivateSection(tome, next);
        }

        private static void ActivateSection(TomeManager tome, string section)
        {
            switch (section)
            {
                case "main":
                    tome.DoTomeMain();
                    break;
                case "cards":
                    tome.DoTomeCards();
                    break;
                case "items":
                    tome.DoTomeItems();
                    break;
                case "glossary":
                    tome.DoTomeGlossary();
                    break;
                case "runs":
                    tome.DoTomeRuns();
                    break;
                case "scoreboard":
                    tome.DoTomeScoreboard();
                    break;
            }
        }

        private void ActivateFocused()
        {
            TomeItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("tome_no_items"));
                return;
            }

            if (item.Card != null && CardScreenManager.Instance != null)
            {
                CardScreenHandler.Open(item.Card);
                return;
            }

            if (item.Activate != null)
            {
                item.Activate();
                ResetForNewView();
                return;
            }

            AnnounceFocusedItem();
        }

        private void FocusSearch(TomeManager tome)
        {
            if (tome.searchInput == null || !IsVisible(tome.searchInput.transform))
            {
                ScreenReader.Say(Loc.Get("tome_search_unavailable"));
                return;
            }

            tome.searchInput.ActivateInputField();
            tome.ShowSearchFocus();
            ScreenReader.Say(Loc.Get("tome_search_focused"));
        }

        private static void HandleSearchFocus(TomeManager tome)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                tome.searchInput.DeactivateInputField();
                ScreenReader.Say(Loc.Get("tome_search_closed"));
            }
        }

        private void HandleEscape(TomeManager tome, string section)
        {
            if (IsVisible(tome.runDetails))
            {
                tome.RunDetailClose();
                ScreenReader.Say(Loc.Get("tome_close_run_detail"));
                ResetForNewView();
                return;
            }

            if (section == "glossary" && !IsVisible(tome.glossaryIndex))
            {
                tome.SetPage(0, absolute: true);
                ScreenReader.Say(Loc.Get("tome_glossary_index"));
                ResetForNewView();
                return;
            }

            CloseTome(tome);
        }

        private static void CloseTome(TomeManager tome)
        {
            if (TeamManagement.Instance != null)
            {
                TeamManagement.Instance.EnableDisableTestingPanels(state: true);
                TeamManagement.Instance.UpdateDeck();
            }

            tome.ShowTome(_status: false);
            _openedByShowTome = false;
            ScreenReader.Say(Loc.Get("tome_closed"));
        }

        private void AnnounceTome(string section)
        {
            if (!_announced || _lastSection != section)
            {
                _announced = true;
                _lastSection = section;
                ScreenReader.Say(Loc.Get("tome_screen", SectionName(section)));
                ScreenReader.SayQueued(Loc.Get("tome_controls"));
                AnnounceFocusedItem(queued: true);
            }
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            TomeItem item = CurrentItem();
            string message = item != null ? item.Summary : Loc.Get("tome_no_items");
            if (queued)
            {
                ScreenReader.SayQueued(message);
            }
            else
            {
                ScreenReader.Say(message);
            }
        }

        private TomeItem CurrentItem()
        {
            if (_items.Count == 0 || _itemIndex < 0 || _itemIndex >= _items.Count)
            {
                return null;
            }

            return _items[_itemIndex];
        }

        private void AddActionItem(string text, Action action)
        {
            TomeItem item = new TomeItem();
            AddLine(item, text);
            item.Summary = text;
            item.Activate = action;
            _items.Add(item);
        }

        private void AddSimpleItem(string text)
        {
            TomeItem item = new TomeItem();
            AddLine(item, text);
            item.Summary = text;
            _items.Add(item);
        }

        private static void AddLine(TomeItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static string ActiveSection(TomeManager tome)
        {
            if (IsVisible(tome.cardsSection))
            {
                return "cards";
            }

            if (IsVisible(tome.itemsSection))
            {
                return "items";
            }

            if (IsVisible(tome.glossarySection))
            {
                return "glossary";
            }

            if (IsVisible(tome.runsSection))
            {
                return "runs";
            }

            if (IsVisible(tome.scoreboardSection))
            {
                return "scoreboard";
            }

            return "main";
        }

        private static string SectionName(string section)
        {
            return Loc.Get("tome_section_" + section);
        }

        private static string TomeButtonName(int tomeClass)
        {
            switch (tomeClass)
            {
                case -1:
                    return GameText.Get("allcards");
                case 0:
                    return Loc.Get("tome_class_warrior");
                case 1:
                    return Loc.Get("tome_class_mage");
                case 2:
                    return Loc.Get("tome_class_healer");
                case 3:
                    return Loc.Get("tome_class_scout");
                case 5:
                    return Loc.Get("tome_class_boon");
                case 6:
                    return Loc.Get("tome_class_injury");
                case 7:
                    return Loc.Get("character_item_weapon");
                case 8:
                    return Loc.Get("character_item_armor");
                case 9:
                    return Loc.Get("character_item_jewelry");
                case 10:
                    return Loc.Get("character_item_accessory");
                case 11:
                    return Loc.Get("character_item_pet");
                case 14:
                    return GameText.Get("global");
                case 15:
                    return GameText.Get("friends");
                case 21:
                    return GameText.Get("combatStats");
                case 22:
                    return Loc.Get("tome_enchantments");
                case 23:
                    return GameText.Get("index");
                default:
                    return Loc.Get("tome_button", tomeClass);
            }
        }

        private static void ActivateTomeButton(TomeButton button)
        {
            if (button == null || TomeManager.Instance == null)
            {
                return;
            }

            int tomeClass = button.tomeClass;
            if (tomeClass == 14 || tomeClass == 15)
            {
                TomeManager.Instance.SelectTomeScores(tomeClass);
            }
            else if (tomeClass >= 16 && tomeClass <= 20)
            {
                TomeManager.Instance.RunDetailButton(tomeClass - 16);
            }
            else if (tomeClass == 21)
            {
                TomeManager.Instance.RunCombatStats();
            }
            else if (tomeClass == 23)
            {
                TomeManager.Instance.SetPage(0);
            }
            else
            {
                TomeManager.Instance.SelectTomeCards(tomeClass);
            }
        }

        private static bool IsSearchFocused(TomeManager tome)
        {
            return tome != null && tome.searchInput != null && tome.searchInput.isFocused;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy && Functions.TransformIsVisible(transform);
        }

        private static string ReadVisibleText(Transform root)
        {
            if (root == null)
            {
                return "";
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            List<string> lines = new List<string>();
            foreach (TMP_Text text in texts)
            {
                if (text == null || !IsVisible(text.transform))
                {
                    continue;
                }

                string value = Clean(text.text);
                if (!string.IsNullOrWhiteSpace(value) && !lines.Contains(value))
                {
                    lines.Add(value);
                }
            }

            return string.Join(" ", lines.ToArray());
        }

        private static int ParseTrailingIndex(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return -1;
            }

            int split = name.LastIndexOf('_');
            if (split < 0 || split + 1 >= name.Length)
            {
                return -1;
            }

            int index;
            return int.TryParse(name.Substring(split + 1), out index) ? index : -1;
        }

        private void ResetForNewView()
        {
            _announced = false;
            _lastSection = "";
            _lastCount = 0;
            _lastRefreshTime = 0f;
            _itemIndex = 0;
            _lineIndex = 0;
        }

        private void Reset()
        {
            ResetForNewView();
            _items.Clear();
        }

        internal static void NotifyShowTome(bool state)
        {
            _openedByShowTome = state;
        }

        internal static bool IsOpenForEscapeGuard()
        {
            TomeManager tome = TomeManager.Instance;
            return tome != null && tome.IsActive() && tome.content != null && IsVisible(tome.content.transform);
        }

        internal static void BlockTomeCloseUntilEscapeRelease()
        {
            if (IsOpenForEscapeGuard())
            {
                _blockCloseUntilEscapeRelease = true;
            }
        }

        internal static bool ShouldBlockTomeClose()
        {
            if (CardScreenManager.Instance != null && CardScreenManager.Instance.IsActive() && IsOpenForEscapeGuard())
            {
                _blockCloseUntilEscapeRelease = true;
                return true;
            }

            if (!_blockCloseUntilEscapeRelease)
            {
                return false;
            }

            if (Input.GetKey(KeyCode.Escape))
            {
                return true;
            }

            _blockCloseUntilEscapeRelease = false;
            return false;
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return count - 1;
            }

            if (index >= count)
            {
                return 0;
            }

            return index;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }

    [HarmonyPatch(typeof(TomeManager), nameof(TomeManager.ShowTome))]
    internal static class TomeManagerShowTomePatch
    {
        private static bool Prefix(bool _status, ref bool __state)
        {
            __state = true;
            if (!_status && TomeHandler.ShouldBlockTomeClose())
            {
                __state = false;
                return false;
            }

            return true;
        }

        private static void Postfix(bool _status, bool __state)
        {
            if (__state)
            {
                TomeHandler.NotifyShowTome(_status);
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), nameof(GameManager.EscapeFunction))]
    internal static class GameManagerEscapeFunctionTomeCardScreenPatch
    {
        private static void Prefix()
        {
            if (CardScreenManager.Instance != null && CardScreenManager.Instance.IsActive() && TomeHandler.IsOpenForEscapeGuard())
            {
                TomeHandler.BlockTomeCloseUntilEscapeRelease();
            }
        }
    }
}
