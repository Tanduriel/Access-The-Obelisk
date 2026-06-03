using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for town card crafting screens.
    /// </summary>
    public sealed class CardCraftHandler
    {
        private sealed class CraftItem
        {
            public string Summary;
            public CardCraftItem CraftCard;
            public BotonGeneric Button;
            public bool Exit;
            public bool GiveGold;
            public bool ServiceAction;
            public bool LoadSavedDeck;
            public bool CraftSavedDeck;
            public int SaveDeckSlot = -1;
            public CardData CardData;
            public ItemCombatIcon ItemIcon;
            public TMPro.TMP_InputField SearchInput;
            public readonly List<string> Lines = new List<string>();
            public readonly List<CardBuffer> Buffers = new List<CardBuffer>();
        }

        private sealed class CardBuffer
        {
            public string Name;
            public string UpgradeType;
            public string FocusSummary;
            public readonly List<string> Lines = new List<string>();

            public string Summary
            {
                get { return !string.IsNullOrWhiteSpace(FocusSummary) ? FocusSummary : string.Join(" ", Lines.ToArray()); }
            }
        }

        private static readonly FieldInfo CraftCardItemDictField = AccessTools.Field(typeof(CardCraftManager), "craftCardItemDict");
        private static readonly FieldInfo HeroIndexField = AccessTools.Field(typeof(CardCraftManager), "heroIndex");
        private static readonly FieldInfo CostAField = AccessTools.Field(typeof(CardCraftManager), "costA");
        private static readonly FieldInfo CostBField = AccessTools.Field(typeof(CardCraftManager), "costB");
        private static readonly FieldInfo CostRemoveField = AccessTools.Field(typeof(CardCraftManager), "costRemove");
        private static readonly FieldInfo ItemIconCardDataField = AccessTools.Field(typeof(ItemCombatIcon), "cardData");
        private static readonly FieldInfo UpgradeAButtonField = AccessTools.Field(typeof(CardCraftManager), "BG_Left");
        private static readonly FieldInfo UpgradeBButtonField = AccessTools.Field(typeof(CardCraftManager), "BG_Right");
        private static readonly FieldInfo RemoveButtonField = AccessTools.Field(typeof(CardCraftManager), "BG_Remove");
        private static readonly FieldInfo CraftTierZoneField = AccessTools.Field(typeof(CardCraftManager), "craftTierZone");
        private static float _suppressDeckSaveUntil;
        private static readonly MethodInfo SetPriceMethod = AccessTools.Method(
            typeof(CardCraftManager),
            "SetPrice",
            new[] { typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool) });

        private readonly List<CraftItem> _items = new List<CraftItem>();
        private int _index;
        private int _lineIndex;
        private int _bufferIndex;
        private bool _announced;
        private float _lastRefreshTime;
        private int _lastCraftType = -1;
        private int _selectedUpgradeCardId;

        /// <summary>
        /// Updates card craft navigation.
        /// </summary>
        public bool Update()
        {
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft == null || !craft.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            if (craft.craftType != 0 && craft.craftType != 1 && craft.craftType != 2 && craft.craftType != 3 && craft.craftType != 4)
            {
                Reset();
                return false;
            }

            if (_lastCraftType != craft.craftType)
            {
                Reset();
                _lastCraftType = craft.craftType;
            }

            AccessStateManager.SetState(AccessState.Town);
            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(craft);
                AnnounceOnce(craft);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _lineIndex = 0;
            _bufferIndex = 0;
            _announced = false;
            _selectedUpgradeCardId = 0;
        }

        private void Refresh(CardCraftManager craft)
        {
            _items.Clear();
            if (craft.craftType == 2 && IsSaveLoadOpen(craft))
            {
                AddSaveLoadDeckItems(craft);
            }
            else if (craft.craftType == 0 || craft.craftType == 1)
            {
                AddDeckCards(craft);
            }
            else if (craft.craftType == 4)
            {
                AddShopItems(craft);
                AddArmoryControls(craft);
            }
            else if (craft.craftType == 3)
            {
                AddDivinationButtons(craft);
            }
            else
            {
                AddCraftCards(craft);
            }

            AddExit(craft);
            if (!IsSaveLoadOpen(craft))
            {
                AddCraftControls(craft);
                AddGiveGold(craft);
            }
            _index = ClampIndex(_index, _items.Count);
            _bufferIndex = ClampIndex(_bufferIndex, CurrentBufferCount());
            SelectFocusedUpgradeCard(craft);
        }

        private static bool IsSaveLoadOpen(CardCraftManager craft)
        {
            return craft != null
                && craft.cardCraftSave != null
                && craft.cardCraftSave.gameObject.activeInHierarchy
                && Functions.TransformIsVisible(craft.cardCraftSave);
        }

        private void AddSaveLoadDeckItems(CardCraftManager craft)
        {
            if (craft == null)
            {
                return;
            }

            AddSaveLoadHeader(craft);
            AddSaveLoadSlots(craft);
            AddSaveLoadPreview(craft);
            AddButtonAction(craft.botSaveLoad, Loc.Get("craft_save_load_return"));
        }

        private void AddSaveLoadHeader(CardCraftManager craft)
        {
            string heroText = Clean(craft.loadDeckHeroName != null ? craft.loadDeckHeroName.text : "");
            if (string.IsNullOrWhiteSpace(heroText))
            {
                return;
            }

            CraftItem item = new CraftItem();
            AddLine(item, heroText);
            item.Summary = heroText;
            _items.Add(item);
        }

        private void AddSaveLoadSlots(CardCraftManager craft)
        {
            if (craft.deckSlot == null)
            {
                return;
            }

            for (int i = 0; i < craft.deckSlot.Length; i++)
            {
                DeckSlot slot = craft.deckSlot[i];
                if (slot == null || !slot.gameObject.activeInHierarchy || !Functions.TransformIsVisible(slot.transform))
                {
                    continue;
                }

                bool loadable = IsDeckSlotLoadable(slot);
                if (loadable)
                {
                    _items.Add(BuildSavedDeckLoadItem(slot));
                    CraftItem deleteItem = BuildSavedDeckButtonItem(slot.deleteButton, Loc.Get("craft_saved_deck_delete", i + 1, SlotTitle(slot)));
                    if (deleteItem != null)
                    {
                        _items.Add(deleteItem);
                    }
                }
                else
                {
                    CraftItem saveItem = BuildSavedDeckButtonItem(slot.saveButton, Loc.Get("craft_saved_deck_save", i + 1));
                    if (saveItem != null)
                    {
                        _items.Add(saveItem);
                    }
                }
            }
        }

        private static bool IsDeckSlotLoadable(DeckSlot slot)
        {
            BoxCollider2D collider = slot != null ? slot.GetComponent<BoxCollider2D>() : null;
            return collider != null && collider.enabled;
        }

        private static CraftItem BuildSavedDeckLoadItem(DeckSlot slot)
        {
            CraftItem item = new CraftItem();
            item.LoadSavedDeck = true;
            item.SaveDeckSlot = slot.slot;
            string title = SlotTitle(slot);
            string count = Clean(slot.cards != null ? slot.cards.text : "");
            AddLine(item, Loc.Get("craft_saved_deck_slot", slot.slot + 1, title, string.IsNullOrWhiteSpace(count) ? "0" : count));
            AddLine(item, Loc.Get("craft_saved_deck_load_hint"));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private static CraftItem BuildSavedDeckButtonItem(Transform buttonTransform, string fallback)
        {
            BotonGeneric button = buttonTransform != null ? buttonTransform.GetComponent<BotonGeneric>() : null;
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return null;
            }

            CraftItem item = new CraftItem();
            item.Button = button;
            item.ServiceAction = true;
            string text = Clean(button.GetText());
            AddLine(item, string.IsNullOrWhiteSpace(text) ? fallback : fallback + ". " + text);
            if (!button.IsEnabled())
            {
                AddLine(item, Loc.Get("unavailable"));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private void AddSaveLoadPreview(CardCraftManager craft)
        {
            if (craft.loadDeckContainer == null || !craft.loadDeckContainer.gameObject.activeInHierarchy || !Functions.TransformIsVisible(craft.loadDeckContainer))
            {
                return;
            }

            CraftItem preview = new CraftItem();
            string deckName = Clean(craft.containerDeckName != null ? craft.containerDeckName.text : "");
            AddLine(preview, string.IsNullOrWhiteSpace(deckName) ? Loc.Get("craft_saved_deck_preview") : Loc.Get("craft_saved_deck_preview_named", deckName));
            AddSavedDeckPriceLines(preview, craft);
            AddSavedDeckPreviewCards(preview, craft);
            preview.Summary = string.Join(" ", preview.Lines.ToArray());
            _items.Add(preview);

            BotonGeneric craftButton = craft.botCraftingDeck;
            if (craftButton != null && craftButton.gameObject.activeInHierarchy && Functions.TransformIsVisible(craftButton.transform))
            {
                CraftItem action = new CraftItem();
                action.Button = craftButton;
                action.CraftSavedDeck = true;
                string text = Clean(craftButton.GetText());
                AddLine(action, string.IsNullOrWhiteSpace(text) ? Loc.Get("craft_saved_deck_apply") : text);
                AddSavedDeckPriceLines(action, craft);
                if (!craftButton.IsEnabled())
                {
                    AddLine(action, Loc.Get("unavailable"));
                }

                action.Summary = string.Join(" ", action.Lines.ToArray());
                _items.Add(action);
            }
        }

        private static void AddSavedDeckPriceLines(CraftItem item, CardCraftManager craft)
        {
            string price = Clean(craft.deckCraftPrice != null ? craft.deckCraftPrice.text : "");
            if (!string.IsNullOrWhiteSpace(price))
            {
                AddLine(item, price);
            }
        }

        private static void AddSavedDeckPreviewCards(CraftItem item, CardCraftManager craft)
        {
            if (craft.loadDeckCardContainer == null)
            {
                return;
            }

            int count = 0;
            foreach (Transform child in craft.loadDeckCardContainer)
            {
                CardVertical card = child != null ? child.GetComponent<CardVertical>() : null;
                if (card == null || card.cardData == null || !card.gameObject.activeInHierarchy || !Functions.TransformIsVisible(card.transform))
                {
                    continue;
                }

                count++;
                AddLine(item, Loc.Get("craft_saved_deck_card", count, CardSpeech.BuildCardFocusSummary(card.cardData, card.cardData.EnergyCost)));
            }

            if (count == 0)
            {
                AddLine(item, Loc.Get("deck_empty"));
            }
        }

        private static string SlotTitle(DeckSlot slot)
        {
            string title = Clean(slot != null && slot.title != null ? slot.title.text : "");
            return string.IsNullOrWhiteSpace(title) ? Loc.Get("empty_slot") : title;
        }

        private void AddExit(CardCraftManager craft)
        {
            BotonGeneric button = craft.exitCraftButton != null ? craft.exitCraftButton.GetComponent<BotonGeneric>() : null;
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.Button = button;
            item.Exit = true;
            string text = Clean(button.GetText());
            AddLine(item, string.IsNullOrWhiteSpace(text) ? Loc.Get("craft_exit") : text);
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddGiveGold(CardCraftManager craft)
        {
            if (craft == null || (craft.craftType != 0 && craft.craftType != 1 && craft.craftType != 2 && craft.craftType != 4) || PlayerUIManager.Instance == null || PlayerUIManager.Instance.giveGold == null)
            {
                return;
            }

            Transform giveGold = PlayerUIManager.Instance.giveGold;
            if (!giveGold.gameObject.activeInHierarchy || !Functions.TransformIsVisible(giveGold))
            {
                return;
            }

            BotonGeneric button = giveGold.GetComponent<BotonGeneric>();
            CraftItem item = new CraftItem();
            item.Button = button;
            item.GiveGold = true;
            string text = button != null ? Clean(button.GetText()) : "";
            AddLine(item, string.IsNullOrWhiteSpace(text) ? Loc.Get("craft_give_gold") : text);
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddCraftControls(CardCraftManager craft)
        {
            if (craft == null || craft.craftType != 2)
            {
                return;
            }

            AddButtonAction(craft.botSaveLoad, Loc.Get("craft_save_load"));
            AddAdvancedCraftButton(craft.buttonAffordableCraft, Loc.Get("craft_affordable_only"), AtOManager.Instance != null && AtOManager.Instance.affordableCraft);
            AddAdvancedCraftButton(craft.buttonAdvancedCraft, Loc.Get("craft_advanced_mode"), AtOManager.Instance != null && AtOManager.Instance.advancedCraft);
            AddPageButtons(craft.cardCraftPageContainer);
            AddSearchInput(craft);
        }

        private void AddArmoryControls(CardCraftManager craft)
        {
            if (craft == null || craft.craftType != 4)
            {
                return;
            }

            AddButtonAction(craft.itemShopButton != null ? craft.itemShopButton.GetComponent<BotonGeneric>() : null, Loc.Get("armory_items_tab"));
            AddButtonAction(craft.petShopButton != null ? craft.petShopButton.GetComponent<BotonGeneric>() : null, Loc.Get("armory_pets_tab"));
            AddArmoryReroll(craft);
            AddArmoryEquippedItem(craft.iconWeapon, Loc.Get("character_item_weapon"));
            AddArmoryEquippedItem(craft.iconArmor, Loc.Get("character_item_armor"));
            AddArmoryEquippedItem(craft.iconJewelry, Loc.Get("character_item_jewelry"));
            AddArmoryEquippedItem(craft.iconAccesory, Loc.Get("character_item_accessory"));
            AddArmoryEquippedItem(craft.iconPet, Loc.Get("character_item_pet"));
            AddArmoryShadyDeal(craft);
            AddGiveGold(craft);
            AddPageButtons(craft.itemsCraftPageContainer);
        }

        private void AddArmoryReroll(CardCraftManager craft)
        {
            BotonGeneric button = craft != null && craft.rerollButton != null ? craft.rerollButton.GetComponent<BotonGeneric>() : null;
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.Button = button;
            item.ServiceAction = true;
            string text = Clean(button.GetText());
            AddLine(item, string.IsNullOrWhiteSpace(text) ? Loc.Get("armory_reroll") : text);
            int cost = Globals.Instance != null ? Globals.Instance.GetCostReroll() : -1;
            int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
            if (cost >= 0)
            {
                AddLine(item, gold >= cost ? Loc.Get("armory_reroll_cost", cost, gold) : Loc.Get("armory_reroll_not_enough_gold", cost, gold));
            }

            if (craft.rerollButtonWarning != null && craft.rerollButtonWarning.gameObject.activeInHierarchy)
            {
                AddLine(item, Loc.Get("armory_reroll_limited"));
            }

            if (craft.rerollButtonLock != null && craft.rerollButtonLock.gameObject.activeInHierarchy)
            {
                AddLine(item, Loc.Get("armory_reroll_unavailable"));
            }

            if (!button.IsEnabled())
            {
                AddLine(item, Loc.Get("unavailable"));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddArmoryEquippedItem(ItemCombatIcon icon, string slotName)
        {
            if (icon == null || !icon.gameObject.activeInHierarchy || !Functions.TransformIsVisible(icon.transform))
            {
                return;
            }

            CardData data = ItemIconCardDataField != null ? ItemIconCardDataField.GetValue(icon) as CardData : null;
            if (data == null)
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.ItemIcon = icon;
            item.CardData = data;
            AddItemOverviewBuffer(item, data);
            AddItemEffectBuffer(item, data);
            AddLine(item, Loc.Get("armory_equipped_slot", slotName, CardSpeech.BuildItemFocusSummary(data)));
            item.Summary = item.Lines.Count > 0 ? item.Lines[0] : CardSpeech.BuildItemFocusSummary(data);
            _items.Add(item);
        }

        private void AddArmoryShadyDeal(CardCraftManager craft)
        {
            BotonGeneric button = craft != null && craft.shadyDealButton != null ? craft.shadyDealButton.GetComponent<BotonGeneric>() : null;
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.Button = button;
            item.ServiceAction = true;
            AddLine(item, Loc.Get("armory_shady_deal"));
            string cost = Clean(button.GetText());
            string result = Clean(craft.shadyDealResult != null ? craft.shadyDealResult.text : "");
            string left = Clean(craft.shadyDealLeft != null ? craft.shadyDealLeft.text : "");
            if (!string.IsNullOrWhiteSpace(cost))
            {
                AddLine(item, Loc.Get("armory_shady_cost", cost));
            }

            if (!string.IsNullOrWhiteSpace(result))
            {
                AddLine(item, Loc.Get("armory_shady_result", result));
            }

            if (!string.IsNullOrWhiteSpace(left))
            {
                AddLine(item, left);
            }

            if (!button.IsEnabled())
            {
                AddLine(item, Loc.Get("unavailable"));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddAdvancedCraftButton(BotonAdvancedCraft advancedButton, string fallback, bool active)
        {
            if (advancedButton == null)
            {
                return;
            }

            BotonGeneric button = advancedButton.GetComponent<BotonGeneric>();
            AddButtonAction(button, Loc.Get("craft_toggle_state", fallback, active ? Loc.Get("settings_on") : Loc.Get("settings_off")));
        }

        private void AddPageButtons(Transform pageContainer)
        {
            if (pageContainer == null)
            {
                return;
            }

            foreach (Transform child in pageContainer)
            {
                BotonGeneric button = child != null ? child.GetComponent<BotonGeneric>() : null;
                if (button == null)
                {
                    continue;
                }

                string text = Clean(button.GetText());
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = Loc.Get("craft_page", button.auxInt);
                }

                AddButtonAction(button, text);
            }
        }

        private void AddSearchInput(CardCraftManager craft)
        {
            if (craft.searchInput == null || craft.canvasSearchT == null || !craft.canvasSearchT.gameObject.activeInHierarchy || !Functions.TransformIsVisible(craft.searchInput.transform))
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.SearchInput = craft.searchInput;
            string value = Clean(craft.searchInput.text);
            AddLine(item, string.IsNullOrWhiteSpace(value) ? Loc.Get("craft_search") : Loc.Get("craft_search_value", value));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddButtonAction(BotonGeneric button, string fallback)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            CraftItem item = new CraftItem();
            item.Button = button;
            item.ServiceAction = true;
            string text = Clean(button.GetText());
            AddLine(item, string.IsNullOrWhiteSpace(text) ? fallback : text);
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddDeckCards(CardCraftManager craft)
        {
            if (craft.cardListContainer == null)
            {
                return;
            }

            foreach (Transform child in craft.cardListContainer)
            {
                CardVertical card = child != null ? child.GetComponent<CardVertical>() : null;
                if (card == null || card.cardData == null || !card.gameObject.activeInHierarchy || !Functions.TransformIsVisible(card.transform))
                {
                    continue;
                }

                CraftItem item = craft.craftType == 1 ? BuildRemoveDeckCard(card) : BuildUpgradeDeckCard(card);
                if (item != null)
                {
                    _items.Add(item);
                }
            }
        }

        private static CraftItem BuildUpgradeDeckCard(CardVertical card)
        {
            CraftItem item = new CraftItem();
            item.CraftCard = null;
            AddCardBuffer(item, Loc.Get("craft_current_card"), card.cardData, null);
            AddUpgradeBuffers(item, card.cardData);
            if (card.IsLocked())
            {
                AddLine(item, Loc.Get("locked"));
            }

            item.Summary = item.Buffers.Count > 0 ? item.Buffers[0].Summary : string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private static CraftItem BuildRemoveDeckCard(CardVertical card)
        {
            CraftItem item = new CraftItem();
            item.CraftCard = null;
            item.CardData = card.cardData;
            AddCardBuffer(item, Loc.Get("craft_current_card"), card.cardData, null);
            if (card.IsLocked())
            {
                AddLine(item, Loc.Get("locked"));
            }

            item.Summary = item.Buffers.Count > 0 ? item.Buffers[0].Summary : string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private static void AddUpgradeBuffers(CraftItem item, CardData data)
        {
            if (data == null)
            {
                return;
            }

            if (data.CardUpgraded == Enums.CardUpgraded.No)
            {
                AddCardBuffer(item, Loc.Get("craft_upgrade_a"), Globals.Instance.GetCardData(data.UpgradesTo1, instantiate: false), "A", data);
                AddCardBuffer(item, Loc.Get("craft_upgrade_b"), Globals.Instance.GetCardData(data.UpgradesTo2, instantiate: false), "B", data);
                return;
            }

            if (data.CardUpgraded == Enums.CardUpgraded.A && !string.IsNullOrWhiteSpace(data.UpgradedFrom))
            {
                string sibling = data.Id.Remove(data.Id.Length - 1, 1) + "B";
                AddCardBuffer(item, Loc.Get("craft_transform_b"), Globals.Instance.GetCardData(sibling, instantiate: false), "B", data);
                AddCardBuffer(item, Loc.Get("craft_base_card"), Globals.Instance.GetCardData(data.UpgradedFrom, instantiate: false), null);
                return;
            }

            if (data.CardUpgraded == Enums.CardUpgraded.B && !string.IsNullOrWhiteSpace(data.UpgradedFrom))
            {
                string sibling = data.Id.Remove(data.Id.Length - 1, 1) + "A";
                AddCardBuffer(item, Loc.Get("craft_transform_a"), Globals.Instance.GetCardData(sibling, instantiate: false), "A", data);
                AddCardBuffer(item, Loc.Get("craft_base_card"), Globals.Instance.GetCardData(data.UpgradedFrom, instantiate: false), null);
            }
        }

        private static void AddCardBuffer(CraftItem item, string name, CardData data, string upgradeType, CardData compareTo = null)
        {
            if (data == null)
            {
                return;
            }

            CardBuffer buffer = new CardBuffer();
            buffer.Name = name;
            buffer.UpgradeType = upgradeType;
            buffer.FocusSummary = Loc.Get("buffer_named_summary", name, CardSpeech.BuildCardFocusSummary(data, data.EnergyCost));
            AddLine(buffer, name);
            AddUpgradeDifferenceLines(buffer, compareTo, data);
            AddCardDataLines(buffer, data);
            item.Buffers.Add(buffer);
        }

        private static void AddUpgradeDifferenceLines(CardBuffer buffer, CardData baseData, CardData upgradeData)
        {
            if (baseData == null || upgradeData == null)
            {
                return;
            }

            List<string> changes = new List<string>();
            AddChange(changes, Loc.Get("craft_change_cost"), baseData.EnergyCost.ToString(), upgradeData.EnergyCost.ToString());
            AddChange(changes, Loc.Get("craft_change_damage"), CardNumber(baseData.DamagePreCalculated, baseData.Damage), CardNumber(upgradeData.DamagePreCalculated, upgradeData.Damage));
            AddChange(changes, Loc.Get("craft_change_heal"), baseData.Heal.ToString(), upgradeData.Heal.ToString());
            AddChange(changes, Loc.Get("craft_change_type"), GameText.CardTypeName(baseData.CardType), GameText.CardTypeName(upgradeData.CardType));
            AddChange(changes, Loc.Get("craft_change_target"), Clean(baseData.Target), Clean(upgradeData.Target));
            AddChange(changes, Loc.Get("card_rarity_label"), GameText.CardRarityName(baseData.CardRarity), GameText.CardRarityName(upgradeData.CardRarity));
            AddChange(changes, Loc.Get("craft_change_text"), UpgradeDescription(baseData), UpgradeDescription(upgradeData));

            if (changes.Count == 0)
            {
                AddLine(buffer, Loc.Get("craft_no_obvious_upgrade_changes"));
                return;
            }

            AddLine(buffer, Loc.Get("craft_upgrade_changes", string.Join(" ", changes.ToArray())));
        }

        private static void AddChange(List<string> changes, string label, string before, string after)
        {
            before = CleanChangeValue(before);
            after = CleanChangeValue(after);
            if (string.Equals(before, after, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            changes.Add(Loc.Get("craft_upgrade_change", CleanLabel(label), before, after));
        }

        private static string CardNumber(int primary, int fallback)
        {
            int value = primary != 0 ? primary : fallback;
            return value.ToString();
        }

        private static string UpgradeDescription(CardData data)
        {
            List<string> lines = CardSpeech.BuildCardLines(data, data.EnergyCost);
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].StartsWith(Loc.Get("combat_card_description", ""), System.StringComparison.OrdinalIgnoreCase))
                {
                    return lines[i];
                }
            }

            return string.Join(" ", lines.ToArray());
        }

        private static string CleanChangeValue(string value)
        {
            value = Clean(value);
            return string.IsNullOrWhiteSpace(value) ? Loc.Get("none_value") : value.Trim().TrimEnd(':', '.').Trim();
        }

        private static string CleanLabel(string value)
        {
            value = Clean(value);
            return string.IsNullOrWhiteSpace(value) ? Loc.Get("unknown_value") : value.Trim().TrimEnd(':', '.').Trim();
        }

        private void AddCraftCards(CardCraftManager craft)
        {
            Dictionary<int, CardCraftItem> cards = CraftCardItemDictField != null ? CraftCardItemDictField.GetValue(craft) as Dictionary<int, CardCraftItem> : null;
            if (cards == null)
            {
                return;
            }

            List<int> keys = new List<int>(cards.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                CardCraftItem item = cards[keys[i]];
                if (item == null || !item.gameObject.activeInHierarchy || !Functions.TransformIsVisible(item.transform))
                {
                    continue;
                }

                CraftItem craftItem = BuildCraftCard(item);
                if (craftItem != null)
                {
                    _items.Add(craftItem);
                }
            }
        }

        private void AddShopItems(CardCraftManager craft)
        {
            Dictionary<int, CardCraftItem> cards = CraftCardItemDictField != null ? CraftCardItemDictField.GetValue(craft) as Dictionary<int, CardCraftItem> : null;
            if (cards == null)
            {
                return;
            }

            List<int> keys = new List<int>(cards.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                CardCraftItem item = cards[keys[i]];
                if (item == null || !item.gameObject.activeInHierarchy || !Functions.TransformIsVisible(item.transform))
                {
                    continue;
                }

                CraftItem craftItem = BuildShopItem(item);
                if (craftItem != null)
                {
                    _items.Add(craftItem);
                }
            }
        }

        private void AddDivinationButtons(CardCraftManager craft)
        {
            if (craft.divinationWaitingContainer != null && craft.divinationWaitingContainer.gameObject.activeInHierarchy)
            {
                CraftItem waiting = new CraftItem();
                string text = Clean(craft.divinationWaitingMsg != null ? craft.divinationWaitingMsg.text : "");
                AddLine(waiting, string.IsNullOrWhiteSpace(text) ? Loc.Get("divination_waiting") : text);
                waiting.Summary = string.Join(" ", waiting.Lines.ToArray());
                _items.Add(waiting);
                return;
            }

            if (craft.divinationButtonContainer == null)
            {
                return;
            }

            foreach (Transform child in craft.divinationButtonContainer)
            {
                BotonGeneric button = child != null ? child.GetComponent<BotonGeneric>() : null;
                if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
                {
                    continue;
                }

                CraftItem item = BuildDivinationItem(button);
                if (item != null)
                {
                    _items.Add(item);
                }
            }
        }

        private static CraftItem BuildCraftCard(CardCraftItem card)
        {
            CardData data = Globals.Instance.GetCardData(card.cardId, instantiate: false);
            if (data == null)
            {
                return null;
            }

            CraftItem item = new CraftItem();
            item.CraftCard = card;
            AddCardDataLines(item, data);
            AddCraftCostLine(item, card);
            AddCraftAvailabilityLine(item, card);
            BotonGeneric button = card.button != null ? card.button.GetComponent<BotonGeneric>() : null;
            if (button != null)
            {
                string buttonText = Clean(button.GetText());
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    AddLine(item, buttonText);
                }
            }

            string costText = CraftCostText(CardCraftManager.Instance, card, includeDust: false);
            item.Summary = CardSpeech.BuildCardFocusSummary(data, data.EnergyCost);
            if (!string.IsNullOrWhiteSpace(costText))
            {
                item.Summary = item.Summary + " " + costText;
            }

            return item;
        }

        private static void AddCraftCostLine(CraftItem item, CardCraftItem card)
        {
            string text = CraftCostText(CardCraftManager.Instance, card, includeDust: true);
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddLine(item, text);
            }
        }

        private static void AddCraftAvailabilityLine(CraftItem item, CardCraftItem card)
        {
            if (card == null)
            {
                return;
            }

            string availability = "";
            if (card.availabilityYes != null && card.availabilityYes.gameObject.activeInHierarchy && card.availabilityYesText != null)
            {
                availability = Clean(card.availabilityYesText.text);
            }
            else if (card.availabilityNo != null && card.availabilityNo.gameObject.activeInHierarchy && card.availabilityNoText != null)
            {
                availability = Clean(card.availabilityNoText.text);
            }

            if (!string.IsNullOrWhiteSpace(availability))
            {
                AddLine(item, availability);
            }

            AddLine(item, card.Available && card.Enabled ? Loc.Get("available") : Loc.Get("unavailable"));
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft != null && card.Available && !card.Enabled)
            {
                int cost = CraftCost(craft, card.cardId);
                int dust = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerDust() : 0;
                if (cost > dust)
                {
                    AddLine(item, Loc.Get("craft_not_enough_dust", cost, dust));
                }
            }
        }

        private static string CraftUnavailableText(CardCraftManager craft, CardCraftItem card, string fallback)
        {
            if (craft == null || card == null)
            {
                return Loc.Get("menu_item_unavailable", fallback);
            }

            int cost = CraftCost(craft, card.cardId);
            int dust = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerDust() : 0;
            if (cost > dust)
            {
                return Loc.Get("craft_not_enough_dust", cost, dust);
            }

            return Loc.Get("menu_item_unavailable", fallback);
        }

        private static string CraftCostText(CardCraftManager craft, CardCraftItem card, bool includeDust)
        {
            if (craft == null || card == null)
            {
                return "";
            }

            int cost = CraftCost(craft, card.cardId);
            if (cost < 0)
            {
                return "";
            }

            if (cost == 0)
            {
                return Loc.Get("craft_card_cost_free");
            }

            int dust = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerDust() : 0;
            return includeDust ? Loc.Get("craft_card_cost_with_dust", cost, dust) : Loc.Get("craft_card_cost", cost);
        }

        private static int CraftCost(CardCraftManager craft, string cardId)
        {
            if (craft == null || SetPriceMethod == null)
            {
                return -1;
            }

            try
            {
                int zoneTier = CraftTierZoneField != null ? (int)CraftTierZoneField.GetValue(craft) : 0;
                object[] args = new object[] { "Craft", "", cardId ?? "", zoneTier, true };
                return (int)SetPriceMethod.Invoke(craft, args);
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CardCraftHandler failed to read craft cost: " + ex.Message);
                return -1;
            }
        }

        private static CraftItem BuildShopItem(CardCraftItem card)
        {
            CardData data = Globals.Instance.GetCardData(card.cardId, instantiate: false);
            if (data == null)
            {
                return null;
            }

            CraftItem item = new CraftItem();
            item.CraftCard = card;
            item.CardData = data;
            AddItemOverviewBuffer(item, data);
            AddItemEffectBuffer(item, data);
            AddLine(item, card.Available && card.Enabled ? Loc.Get("available") : Loc.Get("unavailable"));
            AddArmoryItemCostLines(item, card, data);
            BotonGeneric button = card.buttonItem != null ? card.buttonItem.GetComponent<BotonGeneric>() : null;
            if (button != null)
            {
                string buttonText = Clean(button.GetText());
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    AddLine(item, buttonText);
                    if (item.Buffers.Count > 0)
                    {
                        AddLine(item.Buffers[0], buttonText);
                    }
                }
            }

            item.Summary = CardSpeech.BuildItemFocusSummary(data);
            string costText = ArmoryItemCostText(CardCraftManager.Instance, data, includeGold: false);
            if (!string.IsNullOrWhiteSpace(costText))
            {
                item.Summary = item.Summary + " " + costText;
            }

            return item;
        }

        private static void AddArmoryItemCostLines(CraftItem item, CardCraftItem card, CardData data)
        {
            string text = ArmoryItemCostText(CardCraftManager.Instance, data, includeGold: true);
            if (!string.IsNullOrWhiteSpace(text))
            {
                AddLine(item, text);
            }

            if (card != null && card.Available && !card.Enabled)
            {
                int cost = ArmoryItemCost(CardCraftManager.Instance, data);
                int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
                if (cost > gold)
                {
                    AddLine(item, Loc.Get("armory_not_enough_gold", cost, gold));
                }
            }
        }

        private static string ArmoryItemCostText(CardCraftManager craft, CardData data, bool includeGold)
        {
            int cost = ArmoryItemCost(craft, data);
            if (cost < 0 || cost >= 1000000)
            {
                return "";
            }

            if (cost == 0)
            {
                return Loc.Get("armory_item_cost_free");
            }

            int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
            return includeGold ? Loc.Get("armory_item_cost_with_gold", cost, gold) : Loc.Get("armory_item_cost", cost);
        }

        private static int ArmoryItemCost(CardCraftManager craft, CardData data)
        {
            if (craft == null || data == null || SetPriceMethod == null)
            {
                return -1;
            }

            try
            {
                int zoneTier = CraftTierZoneField != null ? (int)CraftTierZoneField.GetValue(craft) : 0;
                string rarity = GameText.CardRarityName(data.CardRarity);
                object[] args = new object[] { "Item", rarity ?? "", data.Id ?? "", zoneTier, true };
                return (int)SetPriceMethod.Invoke(craft, args);
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CardCraftHandler failed to read armory item cost: " + ex.Message);
                return -1;
            }
        }

        private static string ArmoryUnavailableText(CardCraftManager craft, CraftItem item)
        {
            if (item == null)
            {
                return Loc.Get("craft_no_item");
            }

            int cost = ArmoryItemCost(craft, item.CardData);
            int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
            if (cost > gold && cost < 1000000)
            {
                return Loc.Get("armory_not_enough_gold", cost, gold);
            }

            return Loc.Get("menu_item_unavailable", item.Summary);
        }

        private static CraftItem BuildDivinationItem(BotonGeneric button)
        {
            CraftItem item = new CraftItem();
            item.Button = button;
            string text = Clean(button.GetText());
            AddLine(item, string.IsNullOrWhiteSpace(text) ? Loc.Get("divination_option", button.auxInt) : text);
            int tier = Globals.Instance.GetDivinationTier(button.auxInt);
            AddLine(item, Loc.Get("divination_reward_tier", tier));
            AddRewardTierLines(item, Globals.Instance.GetTierRewardData(tier));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private static void AddRewardTierLines(CraftItem item, TierRewardData tier)
        {
            if (tier == null)
            {
                return;
            }

            AddRewardCountLine(item, "Common", tier.Common);
            AddRewardCountLine(item, "Uncommon", tier.Uncommon);
            AddRewardCountLine(item, "Rare", tier.Rare);
            AddRewardCountLine(item, "Epic", tier.Epic);
            AddRewardCountLine(item, "Mythic", tier.Mythic);
            if (tier.Dust != 0)
            {
                AddLine(item, Loc.Get("divination_reward_dust", tier.Dust));
            }
        }

        private static void AddRewardCountLine(CraftItem item, string rarity, int count)
        {
            if (count != 0)
            {
                AddLine(item, Loc.Get("divination_reward_cards", count, rarity));
            }
        }

        private static void AddItemEffectBuffer(CraftItem item, CardData data)
        {
            CardBuffer buffer = new CardBuffer();
            buffer.Name = Loc.Get("item_effects");
            buffer.Lines.AddRange(CardSpeech.BuildItemEffectLines(data));
            if (buffer.Lines.Count > 1)
            {
                item.Buffers.Add(buffer);
            }
        }

        private static void AddItemOverviewBuffer(CraftItem item, CardData data)
        {
            CardBuffer buffer = new CardBuffer();
            buffer.Name = Loc.Get("item_overview");
            buffer.FocusSummary = CardSpeech.BuildItemFocusSummary(data);
            buffer.Lines.AddRange(CardSpeech.BuildItemOverviewLines(data));
            item.Buffers.Add(buffer);
        }

        private static void AddItemDataLines(CardBuffer buffer, ItemData item)
        {
            if (item == null)
            {
                return;
            }

            AddItemNumberLine(buffer, "item_max_health", item.MaxHealth);
            AddItemNumberLine(buffer, "item_energy", item.EnergyQuantity);
            AddItemNumberLine(buffer, "item_draw_cards", item.DrawCards);
            AddItemNumberLine(buffer, "item_heal", item.HealQuantity);
            AddItemNumberLine(buffer, "item_heal_bonus", item.HealFlatBonus);
            AddItemPercentLine(buffer, "item_heal_percent", item.HealPercentBonus);
            AddItemDamageLine(buffer, item.DamageFlatBonus, item.DamageFlatBonusValue);
            AddItemDamageLine(buffer, item.DamageFlatBonus2, item.DamageFlatBonusValue2);
            AddItemDamageLine(buffer, item.DamageFlatBonus3, item.DamageFlatBonusValue3);
            AddItemDamagePercentLine(buffer, item.DamagePercentBonus, item.DamagePercentBonusValue);
            AddItemDamagePercentLine(buffer, item.DamagePercentBonus2, item.DamagePercentBonusValue2);
            AddItemDamagePercentLine(buffer, item.DamagePercentBonus3, item.DamagePercentBonusValue3);
            AddItemResistLine(buffer, item.ResistModified1, item.ResistModifiedValue1);
            AddItemResistLine(buffer, item.ResistModified2, item.ResistModifiedValue2);
            AddItemResistLine(buffer, item.ResistModified3, item.ResistModifiedValue3);
            AddAuraLine(buffer, "item_aura_bonus", item.AuracurseBonus1, item.AuracurseBonusValue1);
            AddAuraLine(buffer, "item_aura_bonus", item.AuracurseBonus2, item.AuracurseBonusValue2);
            AddAuraLine(buffer, "item_aura_gain", item.AuracurseGain1, item.AuracurseGainValue1);
            AddAuraLine(buffer, "item_aura_gain", item.AuracurseGain2, item.AuracurseGainValue2);
            AddAuraLine(buffer, "item_aura_gain", item.AuracurseGain3, item.AuracurseGainValue3);
            AddAuraLine(buffer, "item_self_aura_gain", item.AuracurseGainSelf1, item.AuracurseGainSelfValue1);
            AddAuraLine(buffer, "item_self_aura_gain", item.AuracurseGainSelf2, item.AuracurseGainSelfValue2);
            AddAuraLine(buffer, "item_self_aura_gain", item.AuracurseGainSelf3, item.AuracurseGainSelfValue3);
            AddAuraNameLine(buffer, "item_aura_immunity", item.AuracurseImmune1);
            AddAuraNameLine(buffer, "item_aura_immunity", item.AuracurseImmune2);
        }

        private static void AddItemNumberLine(CardBuffer buffer, string key, int value)
        {
            if (value != 0)
            {
                AddLine(buffer, Loc.Get(key, value));
            }
        }

        private static void AddItemPercentLine(CardBuffer buffer, string key, float value)
        {
            if (value != 0f)
            {
                AddLine(buffer, Loc.Get(key, value));
            }
        }

        private static void AddItemDamageLine(CardBuffer buffer, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(buffer, Loc.Get("item_damage_bonus", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemDamagePercentLine(CardBuffer buffer, Enums.DamageType damageType, float value)
        {
            if (damageType != Enums.DamageType.None && value != 0f)
            {
                AddLine(buffer, Loc.Get("item_damage_percent", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemResistLine(CardBuffer buffer, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(buffer, Loc.Get("item_resist", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddAuraLine(CardBuffer buffer, string key, AuraCurseData aura, int value)
        {
            if (aura != null && value != 0)
            {
                AddLine(buffer, Loc.Get(key, Clean(GameText.AuraCurseName(aura)), value));
            }
        }

        private static void AddAuraNameLine(CardBuffer buffer, string key, AuraCurseData aura)
        {
            if (aura != null)
            {
                AddLine(buffer, Loc.Get(key, Clean(GameText.AuraCurseName(aura))));
            }
        }

        private static void AddCardDataLines(CraftItem item, CardData data)
        {
            item.Lines.AddRange(CardSpeech.BuildCardLines(data, data.EnergyCost));
        }

        private static void AddCardDataLines(CardBuffer buffer, CardData data)
        {
            buffer.Lines.AddRange(CardSpeech.BuildCardLines(data, data.EnergyCost));
        }

        private static void AddCardNumberLine(CraftItem item, string key, int primary, int fallback)
        {
            int value = primary != 0 ? primary : fallback;
            if (value != 0)
            {
                AddLine(item, Loc.Get(key, value));
            }
        }

        private static void AddCardNumberLine(CardBuffer buffer, string key, int primary, int fallback)
        {
            int value = primary != 0 ? primary : fallback;
            if (value != 0)
            {
                AddLine(buffer, Loc.Get(key, value));
            }
        }

        private void AnnounceOnce(CardCraftManager craft)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            string owner = Clean(craft.cardsOwner != null ? craft.cardsOwner.text : "");
            string screen = ScreenName(craft.craftType);
            string screenOwnerKey = ScreenOwnerKey(craft.craftType);
            ScreenReader.Say(string.IsNullOrWhiteSpace(owner) ? screen : Loc.Get(screenOwnerKey, owner));
            AnnounceFocused(true);
        }

        private void ProcessKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool saveLoadOpen = IsSaveLoadOpen(CardCraftManager.Instance);
            bool hasBuffers = CardCraftManager.Instance != null && (CardCraftManager.Instance.craftType == 0 || CardCraftManager.Instance.craftType == 4);
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

            if (ctrl && Input.GetKeyDown(KeyCode.LeftArrow) && hasBuffers)
            {
                MoveBuffer(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.RightArrow) && hasBuffers)
            {
                MoveBuffer(1);
                return;
            }

            if (saveLoadOpen && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(-1);
                return;
            }

            if (saveLoadOpen && Input.GetKeyDown(KeyCode.RightArrow))
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
                MoveHero(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveHero(1);
                return;
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

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate();
            }
        }

        private void MoveHero(int delta)
        {
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft == null || HeroIndexField == null)
            {
                return;
            }

            int current = (int)HeroIndexField.GetValue(craft);
            int next = FindNextHero(current, delta);
            if (next == current)
            {
                ScreenReader.Say(Loc.Get("craft_no_other_hero"));
                return;
            }

            AtOManager.Instance.SideBarCharacterClicked(next);
            _index = 0;
            _lineIndex = 0;
            _bufferIndex = 0;
            _selectedUpgradeCardId = 0;
            Refresh(craft);
            string owner = Clean(craft.cardsOwner != null ? craft.cardsOwner.text : "");
            Hero hero = AtOManager.Instance.GetHero(next);
            if (string.IsNullOrWhiteSpace(owner) && hero != null)
            {
                owner = hero.SourceName;
            }

            ScreenReader.Say(string.IsNullOrWhiteSpace(owner) ? ScreenName(craft.craftType) : Loc.Get(ScreenOwnerKey(craft.craftType), owner));
            AnnounceFocused(true);
        }

        private static int FindNextHero(int current, int delta)
        {
            for (int step = 1; step <= 4; step++)
            {
                int index = current + delta * step;
                if (index < 0 || index > 3)
                {
                    continue;
                }

                Hero hero = AtOManager.Instance.GetHero(index);
                if (hero == null || hero.HeroData == null)
                {
                    continue;
                }

                if (IsHeroUsableForCurrentPlayer(hero))
                {
                    return index;
                }
            }

            return current;
        }

        private static bool IsHeroUsableForCurrentPlayer(Hero hero)
        {
            if (!GameManager.Instance.IsMultiplayer() || hero.Owner == null || hero.Owner == "")
            {
                return true;
            }

            string playerNick = GetPlayerNick();
            return !string.IsNullOrWhiteSpace(playerNick) && hero.Owner == playerNick;
        }

        private static string GetPlayerNick()
        {
            try
            {
                Type networkManagerType = AccessTools.TypeByName("NetworkManager");
                PropertyInfo instanceProperty = networkManagerType != null ? AccessTools.Property(networkManagerType, "Instance") : null;
                object instance = instanceProperty != null ? instanceProperty.GetValue(null, null) : null;
                MethodInfo getPlayerNick = networkManagerType != null ? AccessTools.Method(networkManagerType, "GetPlayerNick") : null;
                return instance != null && getPlayerNick != null ? getPlayerNick.Invoke(instance, null) as string : "";
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CardCraftHandler failed to read NetworkManager.GetPlayerNick: " + ex.Message);
                return "";
            }
        }

        private void Move(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            _bufferIndex = 0;
            SelectFocusedUpgradeCard(CardCraftManager.Instance);
            AnnounceFocused();
        }

        private void Jump(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            _bufferIndex = 0;
            SelectFocusedUpgradeCard(CardCraftManager.Instance);
            AnnounceFocused();
        }

        private void MoveLine(int delta)
        {
            List<string> lines = CurrentLines();
            if (lines == null || lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            List<string> lines = CurrentLines();
            if (lines == null || lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void MoveBuffer(int delta)
        {
            CraftItem item = CurrentItem();
            if (item == null || item.Buffers.Count <= 1)
            {
                ScreenReader.Say(Loc.Get("craft_no_upgrade_preview"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _bufferIndex, delta, item.Buffers.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void AnnounceFocused(bool queued = false)
        {
            CraftItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            string summary = FocusSummary(item);
            if (queued)
            {
                ScreenReader.SayQueued(summary);
            }
            else
            {
                ScreenReader.Say(summary);
            }
        }

        private void Activate()
        {
            CraftItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("craft_no_item"));
                return;
            }

            CardCraftManager craft = CardCraftManager.Instance;
            if (item.Exit)
            {
                if (item.Button == null)
                {
                    ScreenReader.Say(FocusSummary(item));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated_loading", FocusSummary(item)));
                item.Button.Clicked();
                return;
            }

            if (item.GiveGold)
            {
                if (item.Button == null)
                {
                    ScreenReader.Say(FocusSummary(item));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", FocusSummary(item)));
                item.Button.Clicked();
                return;
            }

            if (item.SearchInput != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(item.SearchInput.gameObject);
                item.SearchInput.ActivateInputField();
                ScreenReader.Say(Loc.Get("craft_search_focused"));
                return;
            }

            if (item.ServiceAction)
            {
                if (item.Button == null)
                {
                    ScreenReader.Say(FocusSummary(item));
                    return;
                }

                if (!item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", FocusSummary(item)));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", FocusSummary(item)));
                item.Button.Clicked();
                _index = 0;
                _lineIndex = 0;
                _bufferIndex = 0;
                Refresh(craft);
                if (craft != null)
                {
                    string owner = Clean(craft.cardsOwner != null ? craft.cardsOwner.text : "");
                    ScreenReader.Say(IsSaveLoadOpen(craft)
                        ? Loc.Get("craft_save_load_screen")
                        : (string.IsNullOrWhiteSpace(owner) ? ScreenName(craft.craftType) : Loc.Get(ScreenOwnerKey(craft.craftType), owner)));
                    AnnounceFocused(true);
                }

                return;
            }

            if (item.LoadSavedDeck)
            {
                if (craft == null)
                {
                    ScreenReader.Say(Loc.Get("craft_no_item"));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", FocusSummary(item)));
                craft.LoadDeck(item.SaveDeckSlot);
                Refresh(craft);
                _index = FirstCraftSavedDeckIndex(_index);
                _lineIndex = 0;
                _bufferIndex = 0;
                AnnounceFocused(true);
                return;
            }

            if (item.CraftSavedDeck)
            {
                if (item.Button == null || !item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", FocusSummary(item)));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", FocusSummary(item)));
                item.Button.Clicked();
                _index = 0;
                _lineIndex = 0;
                _bufferIndex = 0;
                return;
            }

            if (craft != null && craft.craftType == 3)
            {
                if (item.Button == null)
                {
                    ScreenReader.Say(FocusSummary(item));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated_loading", FocusSummary(item)));
                item.Button.Clicked();
                return;
            }

            if (craft != null && craft.craftType == 1)
            {
                if (!craft.CanBuy("Remove") || !IsRemoveButtonEnabled(craft))
                {
                    ScreenReader.Say(RemoveUnavailableText(craft, item));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", FocusSummary(item)));
                craft.RemoveCard();
                _selectedUpgradeCardId = 0;
                return;
            }

            if (craft != null && craft.craftType == 0)
            {
                CardBuffer buffer = CurrentBuffer();
                if (buffer == null)
                {
                    ScreenReader.Say(Loc.Get("craft_no_item"));
                    return;
                }

                if (string.IsNullOrWhiteSpace(buffer.UpgradeType))
                {
                    ScreenReader.Say(Loc.Get("craft_choose_upgrade_buffer"));
                    return;
                }

                if (!craft.CanBuy(buffer.UpgradeType) || !IsUpgradeButtonEnabled(craft, buffer.UpgradeType))
                {
                    ScreenReader.Say(UpgradeUnavailableText(craft, buffer));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", buffer.Name));
                craft.BuyUpgrade(buffer.UpgradeType);
                return;
            }

            if (item.ItemIcon != null && item.CardData != null)
            {
                CardScreenManager.Instance.ShowCardScreen(_state: true);
                CardScreenManager.Instance.SetCardData(item.CardData);
                ScreenReader.Say(Loc.Get("deck_card_detail", CardSpeech.BuildItemFocusSummary(item.CardData)));
                return;
            }

            if (craft != null && craft.craftType == 4)
            {
                if (item.CraftCard == null)
                {
                    ScreenReader.Say(Loc.Get("craft_no_item"));
                    return;
                }

                if (!item.CraftCard.Available || !item.CraftCard.Enabled)
                {
                    ScreenReader.Say(ArmoryUnavailableText(craft, item));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", item.Buffers.Count > 0 && item.Buffers[0].Lines.Count > 1 ? item.Buffers[0].Lines[1] : item.Summary));
                craft.BuyItem(item.CraftCard.cardId);
                return;
            }

            if (item.CraftCard != null)
            {
                if (!item.CraftCard.Available || !item.CraftCard.Enabled)
                {
                    if (craft != null && craft.craftType == 2)
                    {
                        ScreenReader.Say(CraftUnavailableText(craft, item.CraftCard, item.Summary));
                    }
                    else
                    {
                        ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                    }

                    return;
                }

                BotonGeneric button = item.CraftCard.button != null ? item.CraftCard.button.GetComponent<BotonGeneric>() : null;
                if (button != null)
                {
                    ScreenReader.Say(Loc.Get("activated", item.Lines.Count > 0 ? item.Lines[0] : item.Summary));
                    button.Clicked();
                }

                return;
            }

        }

        private void SelectFocusedUpgradeCard(CardCraftManager craft)
        {
            if (craft == null || (craft.craftType != 0 && craft.craftType != 1))
            {
                return;
            }

            CardVertical card = CurrentVerticalCard();
            if (card != null && card.GetInstanceID() != _selectedUpgradeCardId)
            {
                _selectedUpgradeCardId = card.GetInstanceID();
                card.fMouseUp();
            }
        }

        private CardVertical CurrentVerticalCard()
        {
            CraftItem item = CurrentItem();
            if (item == null || item.Buffers.Count == 0)
            {
                return null;
            }

            CardCraftManager craft = CardCraftManager.Instance;
            if (craft == null || craft.cardListContainer == null)
            {
                return null;
            }

            int visible = -1;
            foreach (Transform child in craft.cardListContainer)
            {
                CardVertical card = child != null ? child.GetComponent<CardVertical>() : null;
                if (card == null || card.cardData == null || !card.gameObject.activeInHierarchy || !Functions.TransformIsVisible(card.transform))
                {
                    continue;
                }

                visible++;
                if (visible == _index)
                {
                    return card;
                }
            }

            return null;
        }

        private List<string> CurrentLines()
        {
            CardBuffer buffer = CurrentBuffer();
            if (buffer != null)
            {
                List<string> lines = new List<string>(buffer.Lines);
                AddUpgradeCostLine(lines, buffer);
                AddUpgradeOldCostLine(lines, buffer);
                AddRemoveCostLine(lines);
                AddRemoveOldCostLine(lines);
                AddRemoveMinimumDeckLine(lines, CurrentItem());
                AddRemoveUnavailableLine(lines, CurrentItem());
                AddRemainingUsesLine(lines);
                return lines;
            }

            CraftItem item = CurrentItem();
            return item != null ? item.Lines : null;
        }

        private CardBuffer CurrentBuffer()
        {
            CraftItem item = CurrentItem();
            if (item == null || item.Buffers.Count == 0)
            {
                return null;
            }

            _bufferIndex = ClampIndex(_bufferIndex, item.Buffers.Count);
            return item.Buffers[_bufferIndex];
        }

        private int CurrentBufferCount()
        {
            CraftItem item = CurrentItem();
            return item != null ? item.Buffers.Count : 0;
        }

        private string FocusSummary(CraftItem item)
        {
            CardBuffer buffer = CurrentBuffer();
            string summary = buffer != null ? buffer.Summary : item.Summary;
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft != null && craft.craftType == 0 && buffer != null)
            {
                string costText = UpgradeCostText(craft, buffer, includeEnough: false);
                if (!string.IsNullOrWhiteSpace(costText))
                {
                    summary = summary + " " + costText;
                }

                string oldCostText = UpgradeOldCostText(craft, buffer);
                if (!string.IsNullOrWhiteSpace(oldCostText))
                {
                    summary = summary + " " + oldCostText;
                }
            }

            if (craft != null && craft.craftType == 1 && craft.buttonRemove != null && craft.buttonRemove.gameObject.activeInHierarchy)
            {
                BotonGeneric button = craft.buttonRemove.GetComponent<BotonGeneric>();
                string buttonText = button != null ? Clean(button.GetText()) : "";
                if (!string.IsNullOrWhiteSpace(buttonText))
                {
                    summary = summary + " " + buttonText;
                }

                string costText = RemoveCostText(craft, includeEnough: true);
                if (!string.IsNullOrWhiteSpace(costText))
                {
                    summary = summary + " " + costText;
                }

                string oldCostText = RemoveOldCostText(craft);
                if (!string.IsNullOrWhiteSpace(oldCostText))
                {
                    summary = summary + " " + oldCostText;
                }

                string unavailable = RemoveUnavailableReason(craft, item);
                if (!string.IsNullOrWhiteSpace(unavailable))
                {
                    summary = summary + " " + unavailable;
                }
            }

            return summary;
        }

        private static void AddUpgradeCostLine(List<string> lines, CardBuffer buffer)
        {
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft == null || craft.craftType != 0 || buffer == null)
            {
                return;
            }

            string costText = UpgradeCostText(craft, buffer, includeEnough: true);
            if (!string.IsNullOrWhiteSpace(costText))
            {
                lines.Add(costText);
            }
        }

        private static string UpgradeCostText(CardCraftManager craft, CardBuffer buffer, bool includeEnough)
        {
            if (craft == null || buffer == null || string.IsNullOrWhiteSpace(buffer.UpgradeType))
            {
                return "";
            }

            int cost = GetUpgradeCost(craft, buffer.UpgradeType);
            if (cost < 0 || cost >= 1000000)
            {
                return "";
            }

            int dust = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerDust() : 0;
            if (cost == 0)
            {
                return Loc.Get("craft_upgrade_cost_free");
            }

            if (includeEnough)
            {
                return dust >= cost
                    ? Loc.Get("craft_upgrade_cost_with_dust", cost, dust)
                    : Loc.Get("craft_upgrade_not_enough_dust", cost, dust);
            }

            return Loc.Get("craft_upgrade_cost", cost);
        }

        private static void AddUpgradeOldCostLine(List<string> lines, CardBuffer buffer)
        {
            string oldCost = UpgradeOldCostText(CardCraftManager.Instance, buffer);
            if (!string.IsNullOrWhiteSpace(oldCost))
            {
                lines.Add(oldCost);
            }
        }

        private static string UpgradeOldCostText(CardCraftManager craft, CardBuffer buffer)
        {
            if (craft == null || buffer == null || string.IsNullOrWhiteSpace(buffer.UpgradeType))
            {
                return "";
            }

            TMPro.TMP_Text oldCostText = buffer.UpgradeType == "A" ? craft.oldcostAText : buffer.UpgradeType == "B" ? craft.oldcostBText : null;
            if (oldCostText == null || !oldCostText.gameObject.activeInHierarchy)
            {
                return "";
            }

            return Clean(oldCostText.text);
        }

        private static void AddRemainingUsesLine(List<string> lines)
        {
            CardCraftManager craft = CardCraftManager.Instance;
            if (craft == null || (craft.craftType != 0 && craft.craftType != 1) || craft.usesLeftT == null || craft.usesLeftText == null)
            {
                return;
            }

            if (!craft.usesLeftT.gameObject.activeInHierarchy || !Functions.TransformIsVisible(craft.usesLeftT))
            {
                return;
            }

            string text = Clean(craft.usesLeftText.text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        private static int GetUpgradeCost(CardCraftManager craft, string upgradeType)
        {
            FieldInfo field = upgradeType == "A" ? CostAField : upgradeType == "B" ? CostBField : null;
            if (craft == null || field == null)
            {
                return -1;
            }

            return (int)field.GetValue(craft);
        }

        private static bool IsUpgradeButtonEnabled(CardCraftManager craft, string upgradeType)
        {
            FieldInfo field = upgradeType == "A" ? UpgradeAButtonField : upgradeType == "B" ? UpgradeBButtonField : null;
            BotonGeneric button = craft != null && field != null ? field.GetValue(craft) as BotonGeneric : null;
            return button != null && button.gameObject.activeInHierarchy && button.IsEnabled();
        }

        private static void AddRemoveCostLine(List<string> lines)
        {
            string costText = RemoveCostText(CardCraftManager.Instance, includeEnough: true);
            if (!string.IsNullOrWhiteSpace(costText))
            {
                lines.Add(costText);
            }
        }

        private static string RemoveCostText(CardCraftManager craft, bool includeEnough)
        {
            if (craft == null || craft.craftType != 1)
            {
                return "";
            }

            int cost = GetRemoveCost(craft);
            if (cost < 0 || cost >= 1000000)
            {
                return "";
            }

            int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
            if (cost == 0)
            {
                return Loc.Get("craft_remove_cost_free");
            }

            if (includeEnough)
            {
                return gold >= cost
                    ? Loc.Get("craft_remove_cost_with_gold", cost, gold)
                    : Loc.Get("craft_remove_not_enough_gold", cost, gold);
            }

            return Loc.Get("craft_remove_cost", cost);
        }

        private static int GetRemoveCost(CardCraftManager craft)
        {
            if (craft == null || CostRemoveField == null)
            {
                return -1;
            }

            return (int)CostRemoveField.GetValue(craft);
        }

        private static void AddRemoveOldCostLine(List<string> lines)
        {
            string oldCost = RemoveOldCostText(CardCraftManager.Instance);
            if (!string.IsNullOrWhiteSpace(oldCost))
            {
                lines.Add(oldCost);
            }
        }

        private static string RemoveOldCostText(CardCraftManager craft)
        {
            if (craft == null || craft.craftType != 1 || craft.oldcostRemoveText == null || !craft.oldcostRemoveText.gameObject.activeInHierarchy)
            {
                return "";
            }

            return Clean(craft.oldcostRemoveText.text);
        }

        private static void AddRemoveMinimumDeckLine(List<string> lines, CraftItem item)
        {
            string text = RemoveMinimumDeckText(CardCraftManager.Instance, item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        private static string RemoveMinimumDeckText(CardCraftManager craft, CraftItem item)
        {
            if (craft == null || craft.craftType != 1 || AtOManager.Instance == null || AtOManager.Instance.Sandbox_noMinimumDecksize)
            {
                return "";
            }

            if (craft.minCardsT != null && craft.minCardsT.gameObject.activeInHierarchy && craft.minCardsText != null)
            {
                string text = Clean(craft.minCardsText.text);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return Loc.Get("craft_remove_minimum_deck", 15);
        }

        private static void AddRemoveUnavailableLine(List<string> lines, CraftItem item)
        {
            string text = RemoveUnavailableReason(CardCraftManager.Instance, item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lines.Add(text);
            }
        }

        private static string RemoveUnavailableText(CardCraftManager craft, CraftItem item)
        {
            string reason = RemoveUnavailableReason(craft, item);
            return string.IsNullOrWhiteSpace(reason) ? Loc.Get("craft_remove_unavailable") : reason;
        }

        private static string RemoveUnavailableReason(CardCraftManager craft, CraftItem item)
        {
            if (craft == null || craft.craftType != 1)
            {
                return "";
            }

            int cost = GetRemoveCost(craft);
            int gold = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerGold() : 0;
            if (cost > gold && cost < 1000000)
            {
                return Loc.Get("craft_remove_not_enough_gold", cost, gold);
            }

            if (IsMinimumDeckBlockingRemove(craft, item))
            {
                return Loc.Get("craft_remove_minimum_blocked");
            }

            if (!IsRemoveButtonEnabled(craft))
            {
                return Loc.Get("craft_remove_unavailable");
            }

            return "";
        }

        private static bool IsRemoveButtonEnabled(CardCraftManager craft)
        {
            BotonGeneric button = craft != null && RemoveButtonField != null ? RemoveButtonField.GetValue(craft) as BotonGeneric : null;
            return button != null && button.gameObject.activeInHierarchy && button.IsEnabled();
        }

        private static bool IsMinimumDeckBlockingRemove(CardCraftManager craft, CraftItem item)
        {
            if (craft == null || item == null || item.CardData == null || AtOManager.Instance == null || AtOManager.Instance.Sandbox_noMinimumDecksize)
            {
                return false;
            }

            if (item.CardData.CardClass == Enums.CardClass.Injury && AtOManager.Instance.GetNgPlus() >= 9 && AtOManager.Instance.CharInTown())
            {
                return false;
            }

            Hero hero = CurrentCraftHero(craft);
            if (hero == null)
            {
                return false;
            }

            if (item.CardData.CardClass == Enums.CardClass.Injury || item.CardData.CardClass == Enums.CardClass.Boon)
            {
                return hero.GetTotalCardsInDeck() <= 15;
            }

            return hero.GetTotalCardsInDeck(excludeInjuriesAndBoons: true) <= 15;
        }

        private static Hero CurrentCraftHero(CardCraftManager craft)
        {
            if (craft == null || HeroIndexField == null || AtOManager.Instance == null)
            {
                return null;
            }

            int heroIndex = (int)HeroIndexField.GetValue(craft);
            return AtOManager.Instance.GetHero(heroIndex);
        }

        private static string UpgradeUnavailableText(CardCraftManager craft, CardBuffer buffer)
        {
            if (craft == null || buffer == null)
            {
                return Loc.Get("craft_no_item");
            }

            int cost = GetUpgradeCost(craft, buffer.UpgradeType);
            int dust = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerDust() : 0;
            if (cost > dust && cost < 1000000)
            {
                return Loc.Get("craft_upgrade_not_enough_dust", cost, dust);
            }

            return Loc.Get("menu_item_unavailable", buffer.Name);
        }

        private static string ScreenName(int craftType)
        {
            if (craftType == 0)
            {
                return Loc.Get("altar_screen");
            }

            if (craftType == 1)
            {
                return Loc.Get("church_screen");
            }

            if (craftType == 3)
            {
                return Loc.Get("divination_screen");
            }

            if (craftType == 4)
            {
                return Loc.Get("armory_screen");
            }

            return Loc.Get("craft_screen");
        }

        private static string ScreenOwnerKey(int craftType)
        {
            if (craftType == 0)
            {
                return "altar_screen_owner";
            }

            if (craftType == 1)
            {
                return "church_screen_owner";
            }

            if (craftType == 3)
            {
                return "divination_screen_owner";
            }

            if (craftType == 4)
            {
                return "armory_screen_owner";
            }

            return "craft_screen_owner";
        }

        private CraftItem CurrentItem()
        {
            if (_index < 0 || _index >= _items.Count)
            {
                return null;
            }

            return _items[_index];
        }

        private int FirstCraftSavedDeckIndex(int fallback)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].CraftSavedDeck)
                {
                    return i;
                }
            }

            return ClampIndex(fallback, _items.Count);
        }

        private static void AddLine(CraftItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static void AddLine(CardBuffer buffer, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                buffer.Lines.Add(line);
            }
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

        internal static void SuppressDeckSaveBriefly()
        {
            _suppressDeckSaveUntil = Time.unscaledTime + 0.5f;
        }

        internal static bool ShouldSuppressDeckSave()
        {
            return _suppressDeckSaveUntil >= Time.unscaledTime;
        }
    }

    [HarmonyPatch(typeof(CardCraftManager), nameof(CardCraftManager.RemoveDeckAction))]
    internal static class CardCraftManagerRemoveDeckActionPatch
    {
        private static void Postfix()
        {
            CardCraftHandler.SuppressDeckSaveBriefly();
        }
    }

    [HarmonyPatch(typeof(CardCraftManager), nameof(CardCraftManager.SaveDeck))]
    internal static class CardCraftManagerSaveDeckPatch
    {
        private static bool Prefix()
        {
            return !CardCraftHandler.ShouldSuppressDeckSave();
        }
    }
}
