using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;

using Cards;
using Cards.Data;
namespace AccessTheObelisk
{
    /// <summary>
    /// Provides accessible navigation for the hero customization popup (stats, rank,
    /// skins and card backs) shown on the hero selection screen. Modelled on the
    /// Tome of Knowledge handler: a single flat item list with section buttons,
    /// per-item detail lines on Control plus arrows, and section switching on
    /// Control plus Left and Right.
    /// </summary>
    public sealed class CharPopupHandler
    {
        private sealed class CharItem
        {
            public string Summary;
            public readonly List<string> Lines = new List<string>();
            public BotonSkin Skin;
            public BotonCardback Cardback;
            public Action Activate;
        }

        private static readonly FieldInfo SkinDataField = AccessTools.Field(typeof(BotonSkin), "skinData");
        private static readonly FieldInfo CardbackDataField = AccessTools.Field(typeof(BotonCardback), "cardbackData");

        private readonly List<CharItem> _items = new List<CharItem>();
        private int _itemIndex;
        private int _lineIndex;
        private bool _announced;
        private bool _returningFromPerkTree;
        private string _lastSection = "";
        private int _lastCount;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates customization popup navigation and activation.
        /// </summary>
        public bool Update()
        {
            CharPopup popup = HeroSelectionManager.Instance != null ? HeroSelectionManager.Instance.charPopup : null;
            if (popup == null || !popup.IsOpened())
            {
                Reset();
                return false;
            }

            // The perk tree opens on top of this popup; let PerkTreeHandler own it.
            // The game keeps this panel open behind the tree, so closing the tree
            // (its Exit button or Escape) returns here; re-announce the panel then.
            if (PerkTree.Instance != null && PerkTree.Instance.IsActive())
            {
                _returningFromPerkTree = true;
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.CharPopup);

            if (_returningFromPerkTree)
            {
                _returningFromPerkTree = false;
                _announced = false;
                _lastSection = "";
                _lastRefreshTime = 0f;
            }

            string section = ActiveSection(popup);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(popup, section);
                AnnounceScreen(popup, section);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(popup, section);
            return true;
        }

        private void Refresh(CharPopup popup, string section)
        {
            _items.Clear();
            AddSectionButtons(popup, section);

            switch (section)
            {
                case "skins":
                    AddSkins(popup);
                    break;
                case "cardbacks":
                    AddCardbacks();
                    break;
                case "rank":
                    AddRank(popup);
                    AddSuppliesButton(popup);
                    break;
                case "singularity":
                    AddSingularityCards(popup);
                    break;
                default:
                    AddStats(popup);
                    break;
            }

            AddCloseItem(popup);

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

        private void AddSectionButtons(CharPopup popup, string section)
        {
            AddSectionButton(popup.buttonStats, Loc.Get("char_popup_tab_stats"), "stats", section, () => { CloseCardBacks(); popup.ShowStats(); });
            AddSectionButton(popup.buttonRank, Loc.Get("char_popup_tab_rank"), "rank", section, () => { CloseCardBacks(); popup.ShowRank(); });
            AddSectionButton(popup.buttonSkins, Loc.Get("char_popup_tab_skins"), "skins", section, () => { CloseCardBacks(); popup.ShowSkins(); });
            AddSectionButton(popup.buttonCardback, Loc.Get("char_popup_tab_cardbacks"), "cardbacks", section, () => popup.ShowCardbacks());
            AddSectionButton(popup.buttonPerks, Loc.Get("char_popup_tab_perks"), "perks", section, () => popup.ShowPerks());
            AddSectionButton(popup.buttonSingularityCards, Loc.Get("char_popup_tab_singularity"), "singularity", section, () => { CloseCardBacks(); popup.ShowSingularityCards(); });
        }

        private void AddSectionButton(BotonGeneric button, string label, string key, string section, Action action)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            string text = key == section ? Loc.Get("char_popup_tab_current", label) : Loc.Get("char_popup_tab", label);
            AddActionItem(text, action);
        }

        private void AddSkins(CharPopup popup)
        {
            if (popup.botonSkinBase == null)
            {
                return;
            }

            for (int i = 0; i < popup.botonSkinBase.Length; i++)
            {
                BotonSkin skin = popup.botonSkinBase[i];
                if (skin == null || !skin.gameObject.activeInHierarchy || !Functions.TransformIsVisible(skin.transform))
                {
                    continue;
                }

                _items.Add(BuildSkinItem(skin));
            }
        }

        private void AddCardbacks()
        {
            GameObject popUp = HeroSelectionManager.Instance != null ? HeroSelectionManager.Instance.CardBacksPopUp : null;
            if (popUp == null || !popUp.activeInHierarchy)
            {
                return;
            }

            // Section tab and page buttons that organise the card backs.
            BotonGeneric[] buttons = popUp.GetComponentsInChildren<BotonGeneric>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                BotonGeneric button = buttons[i];
                if (button == null || !Functions.TransformIsVisible(button.transform))
                {
                    continue;
                }

                string label = CardbackButtonLabel(button);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                BotonGeneric captured = button;
                string text = button.IsEnabled() ? Loc.Get("char_popup_tab", label) : Loc.Get("char_popup_tab_current", label);
                AddActionItem(text, () => captured.Clicked());
            }

            BotonCardback[] cardbacks = popUp.GetComponentsInChildren<BotonCardback>(false);
            for (int i = 0; i < cardbacks.Length; i++)
            {
                BotonCardback cardback = cardbacks[i];
                if (cardback == null || !cardback.gameObject.activeInHierarchy || !Functions.TransformIsVisible(cardback.transform))
                {
                    continue;
                }

                _items.Add(BuildCardbackItem(cardback));
            }
        }

        private static string CardbackButtonLabel(BotonGeneric button)
        {
            switch (button.gameObject.name)
            {
                case "CardBackSelectionPage":
                    return Loc.Get("char_popup_cardback_page", button.auxInt + 1);
                case "CardBackSectionTab":
                    return Clean(button.GetText());
                case "CloseCardBackPanel":
                    return Loc.Get("char_popup_cardback_close");
                default:
                    return "";
            }
        }

        private void AddSuppliesButton(CharPopup popup)
        {
            BotonGeneric button = popup.useSuppliesButton;
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            string label = Clean(popup.useSuppliesAvailable != null ? popup.useSuppliesAvailable.text : "");
            if (string.IsNullOrWhiteSpace(label))
            {
                label = Loc.Get("char_popup_supplies");
            }

            string text = button.IsEnabled() ? label : Loc.Get("menu_item_unavailable", label);
            AddActionItem(text, () => { if (button.IsEnabled()) button.Clicked(); });
        }

        /// <summary>
        /// Builds the Stats tab from the hero data instead of scraping the visual
        /// layout, so resistances and other numbers carry their text labels. Mirrors
        /// the overview that <see cref="HeroSelectionHandler"/> exposes elsewhere.
        /// </summary>
        private void AddStats(CharPopup popup)
        {
            SubClassData data = SubClass(popup);
            if (data == null)
            {
                return;
            }

            AddClassicVariantToggle(popup, data.Id);
            AddSimpleItem(Loc.Get("hero_selection_detail_name", Clean(data.CharacterName)));
            AddSimpleItem(Loc.Get("hero_selection_detail_class", data.HeroClass.ToString()));
            if (data.HeroClassSecondary != Enums.HeroClass.None)
            {
                AddSimpleItem(Loc.Get("hero_selection_detail_secondary_class", data.HeroClassSecondary.ToString()));
            }
            if (data.HeroClassThird != Enums.HeroClass.None)
            {
                AddSimpleItem(Loc.Get("hero_selection_detail_secondary_class", data.HeroClassThird.ToString()));
            }

            AddSimpleItem(Loc.Get("hero_selection_detail_health", AdjustedHealth(data)));
            AddSimpleItem(Loc.Get("hero_selection_detail_energy", AdjustedEnergy(data), data.EnergyTurn));
            AddSimpleItem(Loc.Get("hero_selection_detail_speed", AdjustedSpeed(data)));

            AddResist(data, "damage_slashing", Enums.DamageType.Slashing);
            AddResist(data, "damage_blunt", Enums.DamageType.Blunt);
            AddResist(data, "damage_piercing", Enums.DamageType.Piercing);
            AddResist(data, "damage_fire", Enums.DamageType.Fire);
            AddResist(data, "damage_cold", Enums.DamageType.Cold);
            AddResist(data, "damage_lightning", Enums.DamageType.Lightning);
            AddResist(data, "damage_mind", Enums.DamageType.Mind);
            AddResist(data, "damage_holy", Enums.DamageType.Holy);
            AddResist(data, "damage_shadow", Enums.DamageType.Shadow);

            AddDescription(data);
            AddTraits(data);
            AddCards(data);
        }

        /// <summary>
        /// Exposes the game's classic/reworked hero variant toggle, currently only
        /// available for Magnus ("mercenary") and Andren ("ranger"). The base game
        /// only offers this as a mouse-only icon on the Stats tab, with no keyboard
        /// equivalent. Toggling goes through the same public
        /// <see cref="CharPopup.ToggleClassicMode"/> the game's own click handler
        /// uses, so multiplayer ownership rules (you can only change your own
        /// hero) are enforced by the game itself, not duplicated here.
        /// </summary>
        private void AddClassicVariantToggle(CharPopup popup, string subClassId)
        {
            if (!HeroClassicModeManager.IsSupported(subClassId))
            {
                return;
            }

            bool classic = HeroSelectionManager.Instance != null
                ? HeroSelectionManager.Instance.IsClassicHeroVariant(subClassId)
                : HeroClassicModeManager.IsClassicEnabled(subClassId);
            string text = Loc.Get("char_popup_classic_variant", ClassicVariantStateText(classic));
            AddActionItem(text, () => popup.ToggleClassicMode(subClassId));
        }

        private static string ClassicVariantStateText(bool classic)
        {
            string text = GameText(classic ? "classic" : "reworked");
            return string.IsNullOrWhiteSpace(text)
                ? Loc.Get(classic ? "char_popup_classic_state_classic" : "char_popup_classic_state_reworked")
                : text;
        }

        private void AddResist(SubClassData data, string nameKey, Enums.DamageType type)
        {
            AddSimpleItem(Loc.Get("hero_selection_detail_resist", Loc.Get(nameKey), AdjustedResist(data, type)));
        }

        private void AddDescription(SubClassData data)
        {
            string strength = Clean(data.CharacterDescriptionStrength);
            string description = Clean(data.CharacterDescription);
            if (!string.IsNullOrWhiteSpace(strength))
            {
                AddSimpleItem(Loc.Get("hero_selection_strength", strength));
            }
            if (!string.IsNullOrWhiteSpace(description))
            {
                AddSimpleItem(Loc.Get("hero_selection_description", description));
            }
        }

        private void AddTraits(SubClassData data)
        {
            AddTrait(data.Trait0);
            AddTrait(data.Trait1A);
            AddTrait(data.Trait1B);
            AddTrait(data.Trait2A);
            AddTrait(data.Trait2B);
            AddTrait(data.Trait3A);
            AddTrait(data.Trait3B);
            AddTrait(data.Trait4A);
            AddTrait(data.Trait4B);
        }

        private void AddTrait(TraitData trait)
        {
            if (trait == null)
            {
                return;
            }

            string name = Clean(AccessTheObelisk.GameText.TraitName(trait));
            string description = Clean(AccessTheObelisk.GameText.TraitDescription(trait));
            if (string.IsNullOrWhiteSpace(description) && trait.TraitCard != null && Texts.Instance != null)
            {
                CardRealtimeData card = Globals.Instance != null ? Globals.Instance.GetCardData(trait.TraitCard.Id, instantiate: false) : null;
                string cardName = card != null ? Clean(AccessTheObelisk.GameText.CardName(card)) : "";
                if (!string.IsNullOrWhiteSpace(cardName))
                {
                    description = Clean(string.Format(Texts.Instance.GetText("traitAddCard"), cardName));
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            string summary = string.IsNullOrWhiteSpace(description) ? name : Loc.Get("hero_selection_trait_line", name, description);
            AddSimpleItem(summary);
        }

        private void AddCards(SubClassData data)
        {
            CardRealtimeData item = StartingItem(data);
            if (item != null)
            {
                CharItem itemEntry = new CharItem();
                itemEntry.Summary = Loc.Get("hero_selection_starting_item", CardSpeech.BuildItemFocusSummary(item));
                AddLine(itemEntry, itemEntry.Summary);
                foreach (string line in CardSpeech.BuildItemEffectLines(item))
                {
                    AddLine(itemEntry, Clean(line));
                }
                _items.Add(itemEntry);
            }

            HeroCards[] cards = data.Cards;
            if (cards == null)
            {
                return;
            }

            int tier = PlayerManager.Instance != null ? PlayerManager.Instance.GetCharacterTier(data.Id, "card") : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                HeroCards heroCard = cards[i];
                if (heroCard == null || heroCard.Card == null || heroCard.UnitsInDeck <= 0)
                {
                    continue;
                }

                CardRealtimeData card = StartingCard(CardSpeech.Resolve(heroCard.Card), tier);
                int energy = card != null ? card.EnergyCost : 0;
                CharItem cardEntry = new CharItem();
                cardEntry.Summary = Loc.Get("hero_selection_starting_card", heroCard.UnitsInDeck, CardSpeech.BuildCardFocusSummary(card, energy));
                AddLine(cardEntry, cardEntry.Summary);
                foreach (string line in CardSpeech.BuildCardLines(card, energy))
                {
                    AddLine(cardEntry, Clean(line));
                }
                _items.Add(cardEntry);
            }
        }

        /// <summary>
        /// Builds the Singularity cards tab from the hero data, reading each card
        /// with its full text rather than leaving the card art unlabelled.
        /// </summary>
        private void AddSingularityCards(CharPopup popup)
        {
            SubClassData data = SubClass(popup);
            if (data == null || data.CardsSingularity == null)
            {
                return;
            }

            List<string> ids = new List<string>();
            for (int i = 0; i < data.CardsSingularity.Length; i++)
            {
                if (data.CardsSingularity[i] != null)
                {
                    ids.Add(data.CardsSingularity[i].Id);
                }
            }
            ids.Sort();

            foreach (string id in ids)
            {
                CardRealtimeData card = Globals.Instance != null ? Globals.Instance.GetCardData(id, instantiate: false) : null;
                if (card == null)
                {
                    continue;
                }

                int energy = card.EnergyCost;
                CharItem entry = new CharItem();
                entry.Summary = CardSpeech.BuildCardFocusSummary(card, energy);
                AddLine(entry, entry.Summary);
                foreach (string line in CardSpeech.BuildCardLines(card, energy))
                {
                    AddLine(entry, Clean(line));
                }
                _items.Add(entry);
            }
        }

        /// <summary>
        /// Builds the Rank tab from player progression data so the progress numbers
        /// carry their labels.
        /// </summary>
        private void AddRank(CharPopup popup)
        {
            SubClassData data = SubClass(popup);
            if (data == null || PlayerManager.Instance == null)
            {
                return;
            }

            string id = data.Id;
            int rank = PlayerManager.Instance.GetPerkRank(id);
            int progress = PlayerManager.Instance.GetProgress(id);
            int next = PlayerManager.Instance.GetPerkNextLevelPoints(id);
            int points = PlayerManager.Instance.GetPerkPointsAvailable(id);

            AddSimpleItem(Loc.Get("char_popup_rank", rank));
            AddSimpleItem(Loc.Get("char_popup_progress", progress, next));
            if (points > 0)
            {
                AddSimpleItem(Loc.Get("char_popup_perk_points", points));
            }
        }

        private void AddCloseItem(CharPopup popup)
        {
            AddActionItem(Loc.Get("char_popup_tab_close"), () =>
            {
                CloseCardBacks();
                popup.Close();
                ScreenReader.Say(Loc.Get("char_popup_closed"));
                Reset();
            });
        }

        private CharItem BuildSkinItem(BotonSkin skin)
        {
            string name = skin.skinName != null ? Clean(skin.skinName.text) : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Loc.Get("char_popup_skin");
            }

            bool locked = IsLocked(skin.lockT);
            bool selected = IsSelected(skin.overT);
            string requirement = locked ? SkinRequirement(skin) : "";

            CharItem item = new CharItem();
            item.Skin = skin;
            item.Summary = StateSummary(name, locked, selected, requirement);
            AddLine(item, name);
            AddLine(item, StateLabel(locked, selected));
            AddLine(item, requirement);
            return item;
        }

        private CharItem BuildCardbackItem(BotonCardback cardback)
        {
            string name = cardback.cardbackName != null ? Clean(cardback.cardbackName.text) : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Loc.Get("char_popup_cardback");
            }

            bool locked = IsLocked(cardback.lockT);
            bool selected = IsSelected(cardback.overT);
            string requirement = locked ? CardbackRequirement(cardback) : "";

            CharItem item = new CharItem();
            item.Cardback = cardback;
            item.Summary = StateSummary(name, locked, selected, requirement);
            AddLine(item, name);
            AddLine(item, StateLabel(locked, selected));
            AddLine(item, requirement);
            return item;
        }

        private static string StateSummary(string name, bool locked, bool selected, string requirement)
        {
            if (locked)
            {
                return Loc.Get("char_popup_locked", name, requirement);
            }

            if (selected)
            {
                return Loc.Get("char_popup_selected", name);
            }

            return Loc.Get("char_popup_available", name);
        }

        private static string StateLabel(bool locked, bool selected)
        {
            if (locked)
            {
                return Loc.Get("locked");
            }

            return selected ? Loc.Get("char_popup_selected_state") : Loc.Get("available");
        }

        private void ProcessKeys(CharPopup popup, string section)
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);

            if (ctrl && ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                MoveLine(1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                MoveLine(-1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.Home))
            {
                JumpLine(false);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.End))
            {
                JumpLine(true);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveSection(popup, -1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveSection(popup, 1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                JumpItem(false);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.UpArrow) || ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow) || ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                ActivateFocused();
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                HandleEscape(popup, section);
            }
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("char_popup_no_items"));
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
                ScreenReader.Say(Loc.Get("char_popup_no_items"));
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
            CharItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("char_popup_no_items"));
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
            CharItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("char_popup_no_items"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void MoveSection(CharPopup popup, int delta)
        {
            List<KeyValuePair<string, Action>> sections = BuildSections(popup);
            if (sections.Count == 0)
            {
                return;
            }

            string current = ActiveSection(popup);
            int index = -1;
            for (int i = 0; i < sections.Count; i++)
            {
                if (sections[i].Key == current)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                index = 0;
            }

            int nextIndex = NavigationBounds.ClampIndex(index + delta, sections.Count);
            if (nextIndex == index)
            {
                return;
            }

            sections[nextIndex].Value();
            ResetForNewView();
        }

        private List<KeyValuePair<string, Action>> BuildSections(CharPopup popup)
        {
            List<KeyValuePair<string, Action>> sections = new List<KeyValuePair<string, Action>>();
            AddSection(sections, popup.buttonStats, "stats", () => { CloseCardBacks(); popup.ShowStats(); });
            AddSection(sections, popup.buttonRank, "rank", () => { CloseCardBacks(); popup.ShowRank(); });
            AddSection(sections, popup.buttonSkins, "skins", () => { CloseCardBacks(); popup.ShowSkins(); });
            AddSection(sections, popup.buttonCardback, "cardbacks", () => popup.ShowCardbacks());
            AddSection(sections, popup.buttonSingularityCards, "singularity", () => { CloseCardBacks(); popup.ShowSingularityCards(); });
            return sections;
        }

        private static void AddSection(List<KeyValuePair<string, Action>> sections, BotonGeneric button, string key, Action action)
        {
            if (button != null && button.gameObject.activeInHierarchy && Functions.TransformIsVisible(button.transform))
            {
                sections.Add(new KeyValuePair<string, Action>(key, action));
            }
        }

        private void ActivateFocused()
        {
            CharItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("char_popup_no_items"));
                return;
            }

            if (item.Skin != null)
            {
                if (IsLocked(item.Skin.lockT))
                {
                    ScreenReader.Say(item.Summary);
                    return;
                }

                item.Skin.OnMouseUp();
                ScreenReader.Say(Loc.Get("char_popup_skin_selected", SkinName(item.Skin)));
                _lastRefreshTime = 0f;
                return;
            }

            if (item.Cardback != null)
            {
                if (IsLocked(item.Cardback.lockT))
                {
                    ScreenReader.Say(item.Summary);
                    return;
                }

                item.Cardback.OnMouseUp();
                ScreenReader.Say(Loc.Get("char_popup_cardback_selected", CardbackName(item.Cardback)));
                _lastRefreshTime = 0f;
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

        private void HandleEscape(CharPopup popup, string section)
        {
            if (section == "cardbacks")
            {
                CloseCardBacks();
                popup.ShowStats();
                ScreenReader.Say(Loc.Get("char_popup_tab_stats"));
                ResetForNewView();
                return;
            }

            CloseCardBacks();
            popup.Close();
            ScreenReader.Say(Loc.Get("char_popup_closed"));
            Reset();
        }

        private void AnnounceScreen(CharPopup popup, string section)
        {
            if (_announced && _lastSection == section)
            {
                return;
            }

            _announced = true;
            _lastSection = section;
            ScreenReader.Say(Loc.Get("char_popup_screen", HeroName(popup), SectionName(section)));
            ScreenReader.SayQueued(Loc.Get("char_popup_controls"));
            AnnounceFocusedItem(queued: true);
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            CharItem item = CurrentItem();
            string message = item != null ? item.Summary : Loc.Get("char_popup_no_items");
            if (queued)
            {
                ScreenReader.SayQueued(message);
            }
            else
            {
                ScreenReader.Say(message);
            }
        }

        private CharItem CurrentItem()
        {
            if (_items.Count == 0 || _itemIndex < 0 || _itemIndex >= _items.Count)
            {
                return null;
            }

            return _items[_itemIndex];
        }

        private void AddActionItem(string text, Action action)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            CharItem item = new CharItem();
            AddLine(item, text);
            item.Summary = text;
            item.Activate = action;
            _items.Add(item);
        }

        private void AddSimpleItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            CharItem item = new CharItem();
            AddLine(item, text);
            item.Summary = text;
            _items.Add(item);
        }

        private static void CloseCardBacks()
        {
            GameObject popUp = HeroSelectionManager.Instance != null ? HeroSelectionManager.Instance.CardBacksPopUp : null;
            if (popUp != null && popUp.activeSelf)
            {
                popUp.SetActive(false);
            }
        }

        private static string ActiveSection(CharPopup popup)
        {
            GameObject cardBacks = HeroSelectionManager.Instance != null ? HeroSelectionManager.Instance.CardBacksPopUp : null;
            if (cardBacks != null && cardBacks.activeInHierarchy)
            {
                return "cardbacks";
            }

            if (IsActive(popup.groupSkins))
            {
                return "skins";
            }

            if (IsActive(popup.groupRank))
            {
                return "rank";
            }

            if (IsActive(popup.groupSingularityCards))
            {
                return "singularity";
            }

            return "stats";
        }

        private static string SectionName(string section)
        {
            return Loc.Get("char_popup_tab_" + section);
        }

        private static bool IsActive(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy;
        }

        private static bool IsLocked(Transform lockT)
        {
            return lockT != null && lockT.gameObject.activeSelf;
        }

        private static bool IsSelected(Transform overT)
        {
            return overT != null && overT.gameObject.activeSelf;
        }

        private static string SkinName(BotonSkin skin)
        {
            string name = skin != null && skin.skinName != null ? Clean(skin.skinName.text) : "";
            return string.IsNullOrWhiteSpace(name) ? Loc.Get("char_popup_skin") : name;
        }

        private static string CardbackName(BotonCardback cardback)
        {
            string name = cardback != null && cardback.cardbackName != null ? Clean(cardback.cardbackName.text) : "";
            return string.IsNullOrWhiteSpace(name) ? Loc.Get("char_popup_cardback") : name;
        }

        private static string HeroName(CharPopup popup)
        {
            string id = popup != null ? popup.GetActive() : "";
            if (string.IsNullOrWhiteSpace(id) || Globals.Instance == null)
            {
                return Loc.Get("unknown_hero");
            }

            SubClassData data = Globals.Instance.GetSubClassData(id);
            return data != null ? Clean(data.CharacterName) : Clean(id);
        }

        private static SubClassData SubClass(CharPopup popup)
        {
            string id = popup != null ? popup.GetActive() : "";
            if (string.IsNullOrWhiteSpace(id) || Globals.Instance == null)
            {
                return null;
            }

            SubClassData data = Globals.Instance.GetSubClassData(id);
            return data != null ? HeroClassicModeManager.GetDisplaySubClassData(data) : null;
        }

        private static bool IsObeliskChallenge()
        {
            return GameManager.Instance != null && GameManager.Instance.IsObeliskChallenge();
        }

        private static int AdjustedHealth(SubClassData data)
        {
            int value = data.Hp;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkMaxHealth(data.Id);
            }

            return value;
        }

        private static int AdjustedEnergy(SubClassData data)
        {
            int value = data.Energy;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkEnergyBegin(data.Id);
            }

            return value;
        }

        private static int AdjustedSpeed(SubClassData data)
        {
            int value = data.Speed;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkSpeed(data.Id);
            }

            return value;
        }

        private static int AdjustedResist(SubClassData data, Enums.DamageType type)
        {
            int value = BaseResist(data, type);
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkResistBonus(data.Id, type);
            }

            return value;
        }

        private static int BaseResist(SubClassData data, Enums.DamageType type)
        {
            switch (type)
            {
                case Enums.DamageType.Slashing:
                    return data.ResistSlashing;
                case Enums.DamageType.Blunt:
                    return data.ResistBlunt;
                case Enums.DamageType.Piercing:
                    return data.ResistPiercing;
                case Enums.DamageType.Fire:
                    return data.ResistFire;
                case Enums.DamageType.Cold:
                    return data.ResistCold;
                case Enums.DamageType.Lightning:
                    return data.ResistLightning;
                case Enums.DamageType.Mind:
                    return data.ResistMind;
                case Enums.DamageType.Holy:
                    return data.ResistHoly;
                case Enums.DamageType.Shadow:
                    return data.ResistShadow;
                default:
                    return 0;
            }
        }

        private static CardRealtimeData StartingItem(SubClassData data)
        {
            if (data == null || data.Item == null || Globals.Instance == null)
            {
                return null;
            }

            string id = data.Item.Id;
            int tier = PlayerManager.Instance != null ? PlayerManager.Instance.GetCharacterTier(data.Id, "item") : 0;
            if (tier == 1 && !string.IsNullOrWhiteSpace(data.Item.Upgrade.UpgradesTo1))
            {
                id = data.Item.Upgrade.UpgradesTo1;
            }
            else if (tier == 2 && !string.IsNullOrWhiteSpace(data.Item.Upgrade.UpgradesTo2))
            {
                id = data.Item.Upgrade.UpgradesTo2;
            }

            return Globals.Instance.GetCardData(id, instantiate: false);
        }

        private static CardRealtimeData StartingCard(CardRealtimeData card, int tier)
        {
            if (card == null || !card.HasFlag(CustomFlags.Starter) || Globals.Instance == null)
            {
                return card;
            }

            if (tier == 1 && !string.IsNullOrWhiteSpace(card.UpgradesTo1))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo1.ToLower(), instantiate: false) ?? card;
            }

            if (tier == 2 && !string.IsNullOrWhiteSpace(card.UpgradesTo2))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo2.ToLower(), instantiate: false) ?? card;
            }

            return card;
        }

        private static string SkinRequirement(BotonSkin skin)
        {
            SkinData data = SkinDataField != null ? SkinDataField.GetValue(skin) as SkinData : null;
            if (data == null)
            {
                return Loc.Get("locked");
            }

            if (!string.IsNullOrEmpty(data.Sku) && SteamManager.Instance != null && !SteamManager.Instance.PlayerHaveDLC(data.Sku))
            {
                return FormatText("requiredDLC", DlcName(data.Sku));
            }

            if (!string.IsNullOrEmpty(data.Sku) && !string.IsNullOrEmpty(data.SteamStat) && SteamManager.Instance != null && SteamManager.Instance.GetStatInt(data.SteamStat) != 1)
            {
                return FormatText("requiredDLCandQuest", DlcName(data.Sku));
            }

            if (data.PerkLevel > 0)
            {
                return FormatText("skinRequiredRankLevel", data.PerkLevel.ToString());
            }

            return Loc.Get("locked");
        }

        private static string CardbackRequirement(BotonCardback cardback)
        {
            CardbackData data = CardbackDataField != null ? CardbackDataField.GetValue(cardback) as CardbackData : null;
            if (data == null)
            {
                return Loc.Get("locked");
            }

            if (data.PdxAccountRequired && !Paradox.Startup.isLoggedIn)
            {
                return GameText("loggedPDXitem");
            }

            if (!string.IsNullOrEmpty(data.Sku) && SteamManager.Instance != null && !SteamManager.Instance.PlayerHaveDLC(data.Sku))
            {
                return FormatText("requiredDLC", DlcName(data.Sku));
            }

            if (!string.IsNullOrEmpty(data.SteamStat))
            {
                return FormatText("requiredWeekly", GameText(data.SteamStat));
            }

            if (data.AdventureLevel > 0)
            {
                return data.AdventureLevel != 1 ? FormatText("requiredAdventureLevel", data.AdventureLevel.ToString()) : GameText("requiredAdventureComplete");
            }

            if (data.ObeliskLevel > 0)
            {
                return data.ObeliskLevel != 1 ? FormatText("requiredObeliskLevel", data.ObeliskLevel.ToString()) : GameText("requiredObeliskComplete");
            }

            if (data.SingularityLevel > 0)
            {
                return data.SingularityLevel != 1 ? FormatText("requiredSingularityLevel", data.SingularityLevel.ToString()) : GameText("requiredSingularityComplete");
            }

            if (data.RankLevel > 0)
            {
                return FormatText("skinRequiredRankLevel", data.RankLevel.ToString());
            }

            return Loc.Get("locked");
        }

        private static string DlcName(string sku)
        {
            if (SteamManager.Instance == null || string.IsNullOrEmpty(sku))
            {
                return sku;
            }

            return Clean(SteamManager.Instance.GetDLCName(sku));
        }

        private static string FormatText(string id, string argument)
        {
            string template = GameText(id);
            if (string.IsNullOrWhiteSpace(template))
            {
                return argument;
            }

            try
            {
                return Clean(string.Format(template, argument));
            }
            catch
            {
                return Clean(template);
            }
        }

        private static string GameText(string id)
        {
            return Texts.Instance != null ? Clean(Texts.Instance.GetText(id)) : id;
        }

        private static void AddLine(CharItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
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
            _returningFromPerkTree = false;
            _items.Clear();
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
