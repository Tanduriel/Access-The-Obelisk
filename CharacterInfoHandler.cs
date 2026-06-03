using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides accessible navigation for the game's native character information window.
    /// </summary>
    public sealed class CharacterInfoHandler
    {
        private sealed class InfoItem
        {
            public string Summary;
            public CardItem Card;
            public TraitLevel Trait;
            public TraitData TraitData;
            public bool TraitActive;
            public readonly List<string> Lines = new List<string>();
        }

        private static readonly FieldInfo TraitDataField = AccessTools.Field(typeof(TraitLevel), "traitData");
        private static readonly FieldInfo TraitActiveField = AccessTools.Field(typeof(TraitLevel), "active");
        private static readonly FieldInfo TraitEnabledField = AccessTools.Field(typeof(TraitLevel), "enabled");
        private static readonly FieldInfo TraitHeroIndexField = AccessTools.Field(typeof(TraitLevel), "heroIndex");
        private static readonly FieldInfo TraitLevelField = AccessTools.Field(typeof(TraitLevel), "traitLevel");

        private readonly List<InfoItem> _items = new List<InfoItem>();
        private int _itemIndex;
        private int _lineIndex;
        private bool _announced;
        private string _lastTab = "";
        private int _lastHeroIndex = -99;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates character-window hotkeys and active character-window navigation.
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

            CharacterWindowUI window = ActiveCharacterWindow();
            if (window == null || !window.IsActive())
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.CharacterInfo);
            string tab = GetActiveTab(window);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(window, tab);
                AnnounceWindow(window, tab);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(window, tab);
            return true;
        }

        private static bool TryCloseCardDetail()
        {
            if (ActiveCharacterWindow() == null || CardScreenManager.Instance == null || !CardScreenManager.Instance.IsActive())
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CardScreenManager.Instance.ShowCardScreen(_state: false);
                ScreenReader.Say(Loc.Get("character_card_detail_closed"));
            }

            return true;
        }

        private bool TryOpenHotkey()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl || !Input.GetKeyDown(KeyCode.I) || TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            if (!CanOpenCharacterWindow())
            {
                ScreenReader.Say(Loc.Get("character_window_unavailable"));
                return true;
            }

            if (ActiveCharacterWindow() != null)
            {
                ScreenReader.Say(Loc.Get("character_window_already_open"));
                return true;
            }

            int heroIndex = CurrentHeroIndex();
            if (heroIndex < 0)
            {
                ScreenReader.Say(Loc.Get("character_window_unavailable"));
                return true;
            }

            if (MatchManager.Instance != null && MatchManager.Instance.characterWindow != null)
            {
                MatchManager.Instance.ShowCharacterWindow("stats", isHero: true, heroIndex);
                ScreenReader.Say(Loc.Get("character_window_opened"));
                ResetForNewWindow();
                return true;
            }

            if (RewardsManager.Instance != null && RewardsManager.Instance.characterWindowUI != null)
            {
                RewardsManager.Instance.ShowCharacterWindow("stats", isHero: true, heroIndex);
                ScreenReader.Say(Loc.Get("character_window_opened"));
                ResetForNewWindow();
                return true;
            }

            if (LootManager.Instance != null && LootManager.Instance.characterWindowUI != null)
            {
                LootManager.Instance.ShowCharacterWindow("stats", isHero: true, heroIndex);
                ScreenReader.Say(Loc.Get("character_window_opened"));
                ResetForNewWindow();
                return true;
            }

            if (TownManager.Instance != null && TownManager.Instance.characterWindow != null)
            {
                TownManager.Instance.ShowCharacterWindow("stats", heroIndex);
                ScreenReader.Say(Loc.Get("character_window_opened"));
                ResetForNewWindow();
                return true;
            }

            if (MapManager.Instance != null && MapManager.Instance.characterWindow != null)
            {
                MapManager.Instance.ShowCharacterWindow("stats", heroIndex);
                ScreenReader.Say(Loc.Get("character_window_opened"));
                ResetForNewWindow();
                return true;
            }

            ScreenReader.Say(Loc.Get("character_window_unavailable"));
            return true;
        }

        private static bool CanOpenCharacterWindow()
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

            if (PerkTree.Instance != null && PerkTree.Instance.IsActive())
            {
                return false;
            }

            if (MatchManager.Instance != null)
            {
                MatchManager match = MatchManager.Instance;
                if (match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow || match.WaitingForAddcardAssignment || match.WaitingForCardEnergyAssignment || match.CardDrag)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CurrentHeroIndex()
        {
            if (MatchManager.Instance != null)
            {
                int active = MatchManager.Instance.GetHeroActive();
                if (active >= 0 && IsHeroAvailable(active))
                {
                    return active;
                }
            }

            CharacterWindowUI window = ActiveCharacterWindow();
            if (window != null && IsHeroAvailable(window.heroIndex))
            {
                return window.heroIndex;
            }

            if (CardCraftManager.Instance != null && IsHeroAvailable(CardCraftManager.Instance.heroIndex))
            {
                return CardCraftManager.Instance.heroIndex;
            }

            return FirstAvailableHeroIndex();
        }

        private static int FirstAvailableHeroIndex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (IsHeroAvailable(i))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsHeroAvailable(int index)
        {
            if (AtOManager.Instance == null || index < 0 || index > 3)
            {
                return false;
            }

            Hero hero = AtOManager.Instance.GetHero(index);
            return hero != null && hero.HeroData != null;
        }

        private static CharacterWindowUI ActiveCharacterWindow()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.characterWindow != null && MatchManager.Instance.characterWindow.IsActive())
            {
                return MatchManager.Instance.characterWindow;
            }

            if (RewardsManager.Instance != null && RewardsManager.Instance.characterWindowUI != null && RewardsManager.Instance.characterWindowUI.IsActive())
            {
                return RewardsManager.Instance.characterWindowUI;
            }

            if (LootManager.Instance != null && LootManager.Instance.characterWindowUI != null && LootManager.Instance.characterWindowUI.IsActive())
            {
                return LootManager.Instance.characterWindowUI;
            }

            if (TownManager.Instance != null && TownManager.Instance.characterWindow != null && TownManager.Instance.characterWindow.IsActive())
            {
                return TownManager.Instance.characterWindow;
            }

            if (MapManager.Instance != null && MapManager.Instance.characterWindow != null && MapManager.Instance.characterWindow.IsActive())
            {
                return MapManager.Instance.characterWindow;
            }

            return null;
        }

        private static string GetActiveTab(CharacterWindowUI window)
        {
            if (window == null)
            {
                return "";
            }

            if (window.deckWindow != null && window.deckWindow.gameObject.activeSelf)
            {
                if (MatchManager.Instance != null)
                {
                    if (window.botCombatDiscard != null && !window.botCombatDiscard.IsEnabled())
                    {
                        return "combatdiscard";
                    }

                    if (window.botCombatVanish != null && !window.botCombatVanish.IsEnabled())
                    {
                        return "combatvanish";
                    }

                    if (window.botCombatDeck != null && !window.botCombatDeck.IsEnabled())
                    {
                        return "combatdeck";
                    }
                }

                return "deck";
            }

            if (window.levelWindow != null && window.levelWindow.gameObject.activeSelf)
            {
                return "level";
            }

            if (window.itemsWindow != null && window.itemsWindow.gameObject.activeSelf)
            {
                return "items";
            }

            if (window.statsWindow != null && window.statsWindow.gameObject.activeSelf)
            {
                return "stats";
            }

            return "stats";
        }

        private void Refresh(CharacterWindowUI window, string tab)
        {
            _items.Clear();
            AddHeader(window);

            switch (tab)
            {
                case "deck":
                case "combatdeck":
                case "combatdiscard":
                case "combatvanish":
                    AddDeckItems(window);
                    break;
                case "items":
                    AddEquippedItems(window);
                    break;
                case "level":
                    AddLevelItems(window);
                    break;
                case "stats":
                    AddStatsItems(window);
                    break;
            }

            AddExitItem();
            if (_itemIndex >= _items.Count)
            {
                _itemIndex = _items.Count - 1;
            }

            if (_itemIndex < 0)
            {
                _itemIndex = 0;
            }
        }

        private void AddHeader(CharacterWindowUI window)
        {
            Character character = CurrentCharacter(window);
            if (character == null)
            {
                AddSimpleItem(Loc.Get("character_unknown"));
                return;
            }

            InfoItem item = new InfoItem();
            AddLine(item, Loc.Get("character_name", Clean(character.SourceName)));
            AddLine(item, Loc.Get("character_level", character.Level));
            AddLine(item, Loc.Get("combat_character_hp", character.HpCurrent, character.Hp));
            if (character.IsHero)
            {
                AddLine(item, Loc.Get("combat_character_energy", MatchManager.Instance != null ? character.EnergyCurrent : character.Energy));
                AddLine(item, Loc.Get("character_draw", character.GetDrawCardsTurnForDisplayInDeck()));
                AddLine(item, Loc.Get("character_perk_rank", character.PerkRank));
            }

            int[] speed = character.GetSpeed();
            AddLine(item, Loc.Get("character_speed", speed[0], speed[1]));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddDeckItems(CharacterWindowUI window)
        {
            if (window.deckWindow == null)
            {
                return;
            }

            AddCards(window.deckWindow.deckContent);
            if (window.deckWindow.injuryContent != null && window.deckWindow.injuryContent.gameObject.activeInHierarchy)
            {
                AddCards(window.deckWindow.injuryContent);
            }
        }

        private void AddCards(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CardItem card = child.GetComponent<CardItem>();
                if (card != null && card.CardData != null)
                {
                    _items.Add(BuildCardItem(card));
                }
            }
        }

        private void AddEquippedItems(CharacterWindowUI window)
        {
            string[] slotKeys = { "character_item_weapon", "character_item_armor", "character_item_jewelry", "character_item_accessory", "character_item_pet" };
            for (int i = 0; i < window.itemCardsCI.Length && i < slotKeys.Length; i++)
            {
                CardItem card = window.itemCardsCI[i];
                if (card != null && card.gameObject.activeInHierarchy && card.CardData != null)
                {
                    InfoItem item = BuildCardItem(card);
                    item.Lines.Insert(0, Loc.Get(slotKeys[i]));
                    item.Summary = Loc.Get("character_item_summary", Loc.Get(slotKeys[i]), CardSpeech.BuildItemFocusSummary(card.CardData));
                    _items.Add(item);
                    continue;
                }

                AddSimpleItem(Loc.Get("character_item_empty", Loc.Get(slotKeys[i])));
            }
        }

        private void AddLevelItems(CharacterWindowUI window)
        {
            AddTextItem(window.characterLevelText, Loc.Get("character_level_info"));
            for (int i = 0; i < window.traitLevel.Length; i++)
            {
                TraitLevel trait = window.traitLevel[i];
                if (trait == null || !trait.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AddTraitItem(trait);
            }

            for (int i = 0; i < window.traitLevelText.Length; i++)
            {
                AddTextItem(window.traitLevelText[i], Loc.Get("character_level_info"));
            }
        }

        private void AddStatsItems(CharacterWindowUI window)
        {
            StatsWindowUI stats = window.statsWindow;
            if (stats == null)
            {
                return;
            }

            AddTextItem(stats.statsName, Loc.Get("character_name_label"));
            AddTextItem(stats.statsHealth, Loc.Get("character_health_label"));
            AddTextItem(stats.statsEnergy, Loc.Get("character_energy_label"));
            AddTextItem(stats.statsSpeed, Loc.Get("character_speed_label"));
            AddTextItem(stats.statsCards, Loc.Get("character_cards_label"));
            AddTextItem(stats.globalDamageDonePercent, Loc.Get("character_damage_done"));
            AddTextItem(stats.globalHealingDonePercent, Loc.Get("character_healing_done_percent"));
            AddTextItem(stats.globalHealingDoneFlat, Loc.Get("character_healing_done_flat"));
            AddTextItem(stats.globalHealingTakenPercent, Loc.Get("character_healing_taken_percent"));
            AddTextItem(stats.globalHealingTakenFlat, Loc.Get("character_healing_taken_flat"));

            if (stats.dmageTypeT != null)
            {
                foreach (Transform damageType in stats.dmageTypeT)
                {
                    AddVisibleTextItem(damageType, Loc.Get("character_damage_type"));
                }
            }

            AddBuffItems(stats.GO_Buffs, Loc.Get("character_effect"));
            AddBuffItems(stats.GO_Immunities, Loc.Get("character_immunity"));
            AddBuffItems(stats.GO_AuraCurse, Loc.Get("character_aura_bonus"));
        }

        private void AddBuffItems(GameObject parent, string label)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent.transform)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                AddVisibleTextItem(child, label);
            }
        }

        private void AddVisibleTextItem(Transform root, string label)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            List<string> lines = new List<string>();
            foreach (TMP_Text text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string line = Clean(text.text);
                if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
                {
                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                return;
            }

            InfoItem item = new InfoItem();
            AddLine(item, label);
            foreach (string line in lines)
            {
                AddLine(item, line);
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddTraitItem(TraitLevel trait)
        {
            TraitData data = TraitDataField != null ? TraitDataField.GetValue(trait) as TraitData : null;
            if (data == null)
            {
                AddVisibleTextItem(trait.transform, Loc.Get("character_trait"));
                return;
            }

            bool active = TraitActiveField != null && (bool)TraitActiveField.GetValue(trait);
            bool enabled = TraitEnabledField != null && (bool)TraitEnabledField.GetValue(trait);
            int level = TraitLevelField != null ? (int)TraitLevelField.GetValue(trait) : 0;
            string name = Clean(data.TraitName);
            string status = active ? Loc.Get("available") : enabled ? Loc.Get("current") : Loc.Get("unavailable");

            InfoItem item = new InfoItem();
            item.Trait = trait;
            item.TraitData = data;
            item.TraitActive = active;
            AddLine(item, Loc.Get("character_trait_status", name, status));

            string description = Clean(data.Description);
            if (!string.IsNullOrWhiteSpace(description))
            {
                AddLine(item, description);
            }

            AddTraitCardLines(item, data.TraitCard, level, Loc.Get("character_trait_adds_card"));
            AddTraitCardLines(item, data.TraitCardForAllHeroes, 0, Loc.Get("character_trait_adds_card_all"));
            if (string.IsNullOrWhiteSpace(description) && data.TraitCard == null && data.TraitCardForAllHeroes == null)
            {
                AddLine(item, Loc.Get("character_trait_no_description"));
            }

            if (active)
            {
                AddLine(item, Loc.Get("character_trait_press_enter"));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddTextItem(TMP_Text text, string label)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            string value = Clean(text.text);
            if (!string.IsNullOrWhiteSpace(value))
            {
                AddSimpleItem(Loc.Get("character_labeled_value", label, value));
            }
        }

        private void AddExitItem()
        {
            AddSimpleItem(Loc.Get("character_close"));
        }

        private void AddSimpleItem(string text)
        {
            InfoItem item = new InfoItem();
            AddLine(item, text);
            item.Summary = text;
            _items.Add(item);
        }

        private static InfoItem BuildCardItem(CardItem card)
        {
            CardData data = card.CardData;
            InfoItem item = new InfoItem();
            item.Card = card;
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
                item.Lines.AddRange(CardSpeech.BuildCardLines(data, card.GetEnergyCost()));
                item.Summary = CardSpeech.BuildCardFocusSummary(data, card.GetEnergyCost());
            }

            return item;
        }

        private void ProcessKeys(CharacterWindowUI window, string tab)
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

            if (ctrl && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveHero(window, -1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveHero(window, 1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveTab(window, tab, -1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveTab(window, tab, 1);
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
                ActivateFocused(window);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                window.Hide();
                ScreenReader.Say(Loc.Get("character_window_closed"));
                Reset();
            }
        }

        private void AnnounceWindow(CharacterWindowUI window, string tab)
        {
            int heroIndex = window != null ? window.heroIndex : -1;
            if (!_announced || _lastTab != tab || _lastHeroIndex != heroIndex)
            {
                _announced = true;
                _lastTab = tab;
                _lastHeroIndex = heroIndex;
                ScreenReader.Say(Loc.Get("character_window_screen", TabName(tab)));
                ScreenReader.SayQueued(Loc.Get("character_window_controls"));
                AnnounceFocusedItem(queued: true);
            }
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("character_no_items"));
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
                ScreenReader.Say(Loc.Get("character_no_items"));
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
            InfoItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("character_no_items"));
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
            InfoItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("character_no_items"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void MoveTab(CharacterWindowUI window, string currentTab, int delta)
        {
            List<string> tabs = AvailableTabs(window);
            int currentIndex = tabs.IndexOf(currentTab);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = NavigationBounds.ClampIndex(currentIndex + delta, tabs.Count);
            if (nextIndex == currentIndex)
            {
                return;
            }

            string nextTab = tabs[nextIndex];
            ShowTab(window, nextTab);
        }

        private void MoveHero(CharacterWindowUI window, int delta)
        {
            if (window == null || IsNpcWindow(window))
            {
                ScreenReader.Say(Loc.Get("character_no_other_hero"));
                return;
            }

            int current = window.heroIndex;
            int availableCount = 0;
            for (int i = 0; i < 4; i++)
            {
                if (IsHeroAvailable(i))
                {
                    availableCount++;
                }
            }

            if (availableCount <= 1)
            {
                AnnounceFocusedItem();
                return;
            }

            for (int next = current + delta; next >= 0 && next < 4; next += delta)
            {
                if (IsHeroAvailable(next))
                {
                    ShowTab(window, GetActiveTab(window), next);
                    return;
                }
            }
        }

        private void ActivateFocused(CharacterWindowUI window)
        {
            InfoItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("character_no_items"));
                return;
            }

            if (item.Summary == Loc.Get("character_close"))
            {
                window.Hide();
                ScreenReader.Say(Loc.Get("character_window_closed"));
                Reset();
                return;
            }

            if (item.Card != null && item.Card.CardData != null && CardScreenManager.Instance != null)
            {
                CardScreenManager.Instance.ShowCardScreen(_state: true);
                CardScreenManager.Instance.SetCardData(item.Card.CardData);
                ScreenReader.Say(Loc.Get("character_card_detail", Clean(item.Card.CardData.CardName)));
                return;
            }

            if (item.Trait != null && item.TraitData != null)
            {
                ActivateTrait(item);
                return;
            }

            AnnounceFocusedItem();
        }

        private void ActivateTrait(InfoItem item)
        {
            if (!item.TraitActive)
            {
                AnnounceFocusedItem();
                return;
            }

            if (TownManager.Instance == null && MapManager.Instance == null)
            {
                ScreenReader.Say(Loc.Get("character_cant_level_up", Clean(GameText.Get("cantLevelUp"))));
                return;
            }

            int heroIndex = TraitHeroIndexField != null ? (int)TraitHeroIndexField.GetValue(item.Trait) : -1;
            if (heroIndex < 0 || AtOManager.Instance == null)
            {
                ScreenReader.Say(Loc.Get("character_window_unavailable"));
                return;
            }

            if (!GameManager.Instance.IsMultiplayer())
            {
                AtOManager.Instance.HeroLevelUp(heroIndex, item.TraitData.Id);
            }
            else
            {
                AtOManager.Instance.HeroLevelUpMP(heroIndex, item.TraitData.Id);
            }

            _lastRefreshTime = 0f;
        }

        private static List<string> AvailableTabs(CharacterWindowUI window)
        {
            List<string> tabs = new List<string>();
            if (MatchManager.Instance != null)
            {
                tabs.Add("combatdeck");
                tabs.Add("combatdiscard");
                tabs.Add("combatvanish");
            }
            else
            {
                tabs.Add("deck");
            }

            if (!IsNpcWindow(window))
            {
                tabs.Add("level");
                tabs.Add("items");
                tabs.Add("stats");
                if (GameManager.Instance == null || !GameManager.Instance.IsObeliskChallenge())
                {
                    tabs.Add("perks");
                }
            }
            else
            {
                tabs.Add("stats");
            }

            return tabs;
        }

        private static bool IsNpcWindow(CharacterWindowUI window)
        {
            return window != null && window.npcButtons != null && window.npcButtons.gameObject.activeSelf;
        }

        private static void AddTraitCardLines(InfoItem item, CardData card, int traitLevel, string label)
        {
            CardData data = TraitCardAtLevel(card, traitLevel);
            if (data == null)
            {
                return;
            }

            AddLine(item, Loc.Get("character_trait_card", label, CardSpeech.CardNameWithRarity(data)));
            List<string> cardLines = CardSpeech.BuildCardLines(data, data.EnergyCost);
            for (int i = 1; i < cardLines.Count; i++)
            {
                AddLine(item, cardLines[i]);
            }
        }

        private static CardData TraitCardAtLevel(CardData card, int traitLevel)
        {
            if (card == null || Globals.Instance == null)
            {
                return null;
            }

            if (traitLevel == 1 && !string.IsNullOrWhiteSpace(card.UpgradesTo1))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo1, instantiate: false);
            }

            if (traitLevel == 2 && !string.IsNullOrWhiteSpace(card.UpgradesTo2))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo2, instantiate: false);
            }

            return Globals.Instance.GetCardData(card.Id, instantiate: false);
        }

        private void ShowTab(CharacterWindowUI window, string tab, int heroIndex = -1)
        {
            int index = heroIndex >= 0 ? heroIndex : window.heroIndex;
            if (tab == "perks")
            {
                Character character = CurrentCharacter(window);
                if (character != null && character.IsHero && character.HeroData != null)
                {
                    window.Show("perks", index);
                    ScreenReader.Say(Loc.Get("character_opening_perks", Clean(character.SourceName)));
                    ResetForNewWindow();
                    return;
                }
            }

            if (MatchManager.Instance != null && MatchManager.Instance.characterWindow == window)
            {
                MatchManager.Instance.ShowCharacterWindow(tab, isHero: !IsNpcWindow(window), index);
            }
            else if (RewardsManager.Instance != null && RewardsManager.Instance.characterWindowUI == window)
            {
                RewardsManager.Instance.ShowCharacterWindow(tab, isHero: true, characterIndex: index);
            }
            else if (LootManager.Instance != null && LootManager.Instance.characterWindowUI == window)
            {
                LootManager.Instance.ShowCharacterWindow(tab, isHero: true, characterIndex: index);
            }
            else if (TownManager.Instance != null && TownManager.Instance.characterWindow == window)
            {
                TownManager.Instance.ShowCharacterWindow(tab, index);
            }
            else if (MapManager.Instance != null && MapManager.Instance.characterWindow == window)
            {
                MapManager.Instance.ShowCharacterWindow(tab, index);
            }

            _itemIndex = 0;
            _lineIndex = 0;
            _lastRefreshTime = 0f;
            _lastTab = "";
        }

        private static Character CurrentCharacter(CharacterWindowUI window)
        {
            if (window == null)
            {
                return null;
            }

            if (IsNpcWindow(window) && MatchManager.Instance != null)
            {
                return MatchManager.Instance.GetNPCCharacter(window.heroIndex);
            }

            if (AtOManager.Instance == null)
            {
                return null;
            }

            return AtOManager.Instance.GetHero(window.heroIndex);
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            InfoItem item = CurrentItem();
            string message = item != null ? item.Summary : Loc.Get("character_no_items");
            if (queued)
            {
                ScreenReader.SayQueued(message);
            }
            else
            {
                ScreenReader.Say(message);
            }
        }

        private InfoItem CurrentItem()
        {
            if (_items.Count == 0 || _itemIndex < 0 || _itemIndex >= _items.Count)
            {
                return null;
            }

            return _items[_itemIndex];
        }

        private void ResetForNewWindow()
        {
            _announced = false;
            _lastTab = "";
            _lastHeroIndex = -99;
            _lastRefreshTime = 0f;
        }

        private void Reset()
        {
            ResetForNewWindow();
            _itemIndex = 0;
            _lineIndex = 0;
            _items.Clear();
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

        private static string TabName(string tab)
        {
            return Loc.Get("character_tab_" + tab);
        }

        private static void AddLine(InfoItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
