using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides structured keyboard access for the Obelisk Challenge draft screen.
    /// </summary>
    public sealed class ChallengeSelectionHandler
    {
        private enum Zone
        {
            Choices,
            Heroes,
            Actions
        }

        private sealed class ChallengeItem
        {
            public string Summary;
            public int PackIndex = -1;
            public PerkChallengeItem Perk;
            public int PerkIndex = -1;
            public bool Selected;
            public bool Available;
            public readonly List<string> Lines = new List<string>();
        }

        private sealed class ActionItem
        {
            public string Summary;
            public BotonGeneric Button;
            public bool Reroll;
            public bool Ready;
            public bool Available;
        }

        private static readonly FieldInfo PerkDataField = AccessTools.Field(typeof(PerkChallengeItem), "perkData");
        private static readonly FieldInfo PerkIndexField = AccessTools.Field(typeof(PerkChallengeItem), "index");
        private static readonly FieldInfo PerkActiveField = AccessTools.Field(typeof(PerkChallengeItem), "active");
        private static readonly FieldInfo PerkEnabledField = AccessTools.Field(typeof(PerkChallengeItem), "enabled");

        private readonly List<ChallengeItem> _choices = new List<ChallengeItem>();
        private readonly List<int> _heroes = new List<int>();
        private readonly List<ActionItem> _actions = new List<ActionItem>();
        private Zone _zone = Zone.Choices;
        private int _choiceIndex;
        private int _heroIndex;
        private int _actionIndex;
        private int _lineIndex;
        private bool _announced;
        private string _lastHeader;
        private string _lastFocusKey;
        private float _lastRefreshTime;
        private float _ignoreInputUntil;
        private bool _waitForSubmitRelease;

        /// <summary>
        /// Updates challenge draft navigation and activation.
        /// </summary>
        public bool Update()
        {
            ChallengeSelectionManager challenge = ChallengeSelectionManager.Instance;
            CardCraftManager craft = CardCraftManager.Instance;
            if (challenge == null || craft == null || !craft.gameObject.activeInHierarchy || craft.craftType != 5)
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.ChallengeSelection);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(challenge, craft);
                AnnounceScreen(challenge, craft);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(challenge);
            return true;
        }

        private void Reset()
        {
            _choices.Clear();
            _heroes.Clear();
            _actions.Clear();
            _zone = Zone.Choices;
            _choiceIndex = 0;
            _heroIndex = 0;
            _actionIndex = 0;
            _lineIndex = 0;
            _announced = false;
            _lastHeader = null;
            _lastFocusKey = null;
            _ignoreInputUntil = 0f;
            _waitForSubmitRelease = false;
        }

        private void Refresh(ChallengeSelectionManager challenge, CardCraftManager craft)
        {
            _choices.Clear();
            _heroes.Clear();
            _actions.Clear();

            if (craft.challengePerks != null && craft.challengePerks.gameObject.activeInHierarchy)
            {
                AddPerks(craft);
            }
            else if (craft.tmpContainer != null && craft.tmpContainer.childCount > 0)
            {
                AddSpecialCards(craft);
            }
            else
            {
                AddPacks(craft);
            }

            AddHeroes(challenge);
            AddActions(craft);
            _choiceIndex = ClampIndex(_choiceIndex, _choices.Count);
            _heroIndex = ClampIndex(_heroIndex, _heroes.Count);
            _actionIndex = ClampIndex(_actionIndex, _actions.Count);
            if (!CurrentZoneHasItems())
            {
                MoveToFirstAvailableZone();
            }
        }

        private void AddPacks(CardCraftManager craft)
        {
            for (int i = 0; i < craft.cardChallengeContainer.Length; i++)
            {
                Transform container = craft.cardChallengeContainer[i];
                if (container == null || !container.gameObject.activeInHierarchy)
                {
                    continue;
                }

                List<CardVertical> cards = new List<CardVertical>();
                foreach (Transform child in container)
                {
                    CardVertical card = child != null ? child.GetComponent<CardVertical>() : null;
                    if (card != null && card.cardData != null && child.gameObject.activeInHierarchy)
                    {
                        cards.Add(card);
                    }
                }

                if (cards.Count == 0)
                {
                    continue;
                }

                ChallengeItem item = new ChallengeItem();
                item.PackIndex = i;
                item.Selected = IsTransformVisible(craft.cardChallengeSelected, i);
                item.Available = IsButtonAvailable(craft.cardChallengeButton, i);
                string title = ReadChallengeTitle(craft, i, Loc.Get("challenge_pack_fallback", i + 1));
                item.Summary = Loc.Get("challenge_pack_summary", title, item.Selected ? Loc.Get("challenge_selected") : item.Available ? Loc.Get("available") : Loc.Get("unavailable"));
                item.Lines.Add(item.Summary);
                item.Lines.Add(Loc.Get("challenge_pack_cards"));
                for (int j = 0; j < cards.Count; j++)
                {
                    AddCardLine(item.Lines, cards[j].cardData);
                }

                _choices.Add(item);
            }
        }

        private void AddSpecialCards(CardCraftManager craft)
        {
            Transform container = craft.tmpContainer;
            for (int i = 0; i < container.childCount; i++)
            {
                Transform child = container.GetChild(i);
                CardItem card = child != null ? child.GetComponent<CardItem>() : null;
                if (card == null || card.CardData == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                ChallengeItem item = new ChallengeItem();
                item.PackIndex = i;
                item.Available = IsButtonAvailable(craft.cardChallengeButton, i);
                item.Summary = Loc.Get("challenge_special_summary", Clean(card.CardData.CardName), item.Available ? Loc.Get("available") : Loc.Get("unavailable"));
                AddCardDetails(item.Lines, card.CardData);
                _choices.Add(item);
            }
        }

        private void AddPerks(CardCraftManager craft)
        {
            for (int i = 0; i < craft.perkChallengeItems.Length; i++)
            {
                PerkChallengeItem perk = craft.perkChallengeItems[i];
                if (perk == null || !perk.gameObject.activeInHierarchy || !Functions.TransformIsVisible(perk.transform))
                {
                    continue;
                }

                PerkData data = ReadPerkData(perk);
                ChallengeItem item = new ChallengeItem();
                item.Perk = perk;
                item.PerkIndex = ReadPerkIndex(perk);
                item.Selected = ReadBool(PerkActiveField, perk);
                item.Available = ReadBool(PerkEnabledField, perk);
                string name = data != null ? BuildPerkName(data) : Loc.Get("perk_unknown");
                item.Summary = Loc.Get("challenge_perk_summary", name, item.Selected ? Loc.Get("challenge_selected") : item.Available ? Loc.Get("available") : Loc.Get("unavailable"));
                item.Lines.Add(item.Summary);
                if (data != null)
                {
                    AddPerkDetailLines(item.Lines, data);
                }

                _choices.Add(item);
            }
        }

        private void AddHeroes(ChallengeSelectionManager challenge)
        {
            for (int i = 0; i < 4; i++)
            {
                Hero hero = AtOManager.Instance != null ? AtOManager.Instance.GetHero(i) : null;
                if (hero != null && hero.HeroData != null)
                {
                    _heroes.Add(i);
                    if (challenge.currentHeroIndex == i)
                    {
                        _heroIndex = _heroes.Count - 1;
                    }
                }
            }
        }

        private void AddActions(CardCraftManager craft)
        {
            if (craft.rerollChallenge != null && craft.rerollChallenge.gameObject.activeInHierarchy)
            {
                ActionItem item = new ActionItem();
                item.Button = craft.rerollChallenge;
                item.Reroll = true;
                item.Available = craft.rerollChallenge.IsEnabled();
                item.Summary = Clean(craft.rerollChallenge.GetText());
                if (string.IsNullOrWhiteSpace(item.Summary))
                {
                    item.Summary = Loc.Get("challenge_reroll");
                }

                _actions.Add(item);
            }

            BotonGeneric ready = craft.readyChallenge != null ? craft.readyChallenge.GetComponent<BotonGeneric>() : null;
            if (ready != null && ready.gameObject.activeInHierarchy)
            {
                ActionItem item = new ActionItem();
                item.Button = ready;
                item.Ready = true;
                item.Available = ready.IsEnabled();
                item.Summary = Clean(ready.GetText());
                if (string.IsNullOrWhiteSpace(item.Summary))
                {
                    item.Summary = Loc.Get("challenge_ready");
                }

                _actions.Add(item);
            }
        }

        private void AnnounceScreen(ChallengeSelectionManager challenge, CardCraftManager craft)
        {
            string header = BuildHeader(challenge, craft);
            if (!_announced)
            {
                _announced = true;
                _ignoreInputUntil = Time.unscaledTime + 0.35f;
                _waitForSubmitRelease = IsSubmitHeld();
                ScreenReader.Say(header);
                ScreenReader.SayQueued(Loc.Get("challenge_controls"));
                AnnounceFocused(true);
                _lastHeader = header;
                return;
            }

            if (header != _lastHeader)
            {
                _lastHeader = header;
                ScreenReader.Say(header);
                AnnounceFocused(true);
            }
        }

        private static string BuildHeader(ChallengeSelectionManager challenge, CardCraftManager craft)
        {
            string title = Clean(craft.cardChallengeGlobalTitle != null ? craft.cardChallengeGlobalTitle.text : "");
            string round = Clean(craft.cardChallengeRound != null ? craft.cardChallengeRound.text : "");
            string hero = HeroName(challenge.currentHeroIndex);
            string bonus = Clean(craft.cardChallengeBonus != null ? craft.cardChallengeBonus.text : "");
            List<string> parts = new List<string>();
            parts.Add(string.IsNullOrWhiteSpace(title) ? Loc.Get("challenge_screen") : title);
            if (!string.IsNullOrWhiteSpace(hero))
            {
                parts.Add(Loc.Get("challenge_current_hero", hero));
            }

            if (!string.IsNullOrWhiteSpace(round))
            {
                parts.Add(round);
            }

            if (!string.IsNullOrWhiteSpace(bonus))
            {
                parts.Add(Loc.Get("challenge_bonus", bonus));
            }

            return string.Join(" ", parts.ToArray());
        }

        private void ProcessKeys(ChallengeSelectionManager challenge)
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
                JumpItem(false, challenge);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpItem(true, challenge);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveZone(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveZone(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1, challenge);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1, challenge);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(challenge);
            }
        }

        private void MoveZone(int delta)
        {
            int start = (int)_zone;
            for (int zone = start + delta; zone >= 0 && zone < 3; zone += delta)
            {
                Zone candidate = (Zone)zone;
                if (ZoneHasItems(candidate))
                {
                    _zone = candidate;
                    _lineIndex = 0;
                    AnnounceFocused();
                    return;
                }
            }

            ScreenReader.Say(Loc.Get("no_menu_item"));
        }

        private bool ZoneHasItems(Zone zone)
        {
            Zone previous = _zone;
            _zone = zone;
            bool result = CurrentZoneHasItems();
            _zone = previous;
            return result;
        }

        private void MoveItem(int delta, ChallengeSelectionManager challenge)
        {
            if (_zone == Zone.Choices)
            {
                if (!NavigationBounds.TryMove(ref _choiceIndex, delta, _choices.Count))
                {
                    return;
                }
            }
            else if (_zone == Zone.Heroes)
            {
                if (!NavigationBounds.TryMove(ref _heroIndex, delta, _heroes.Count))
                {
                    return;
                }

                int hero = CurrentHeroIndex();
                if (hero >= 0 && hero != challenge.currentHeroIndex)
                {
                    AtOManager.Instance.SideBarCharacterClicked(hero);
                }
            }
            else if (_zone == Zone.Actions)
            {
                if (!NavigationBounds.TryMove(ref _actionIndex, delta, _actions.Count))
                {
                    return;
                }
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void JumpItem(bool end, ChallengeSelectionManager challenge)
        {
            if (_zone == Zone.Choices)
            {
                if (!NavigationBounds.TryJump(ref _choiceIndex, end, _choices.Count))
                {
                    return;
                }
            }
            else if (_zone == Zone.Heroes)
            {
                if (!NavigationBounds.TryJump(ref _heroIndex, end, _heroes.Count))
                {
                    return;
                }

                int hero = CurrentHeroIndex();
                if (hero >= 0 && hero != challenge.currentHeroIndex)
                {
                    AtOManager.Instance.SideBarCharacterClicked(hero);
                }
            }
            else if (_zone == Zone.Actions)
            {
                if (!NavigationBounds.TryJump(ref _actionIndex, end, _actions.Count))
                {
                    return;
                }
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void MoveLine(int delta)
        {
            ChallengeItem item = CurrentChoice();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("challenge_no_details"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(Loc.Get("combat_buffer_position", item.Lines[_lineIndex], _lineIndex + 1, item.Lines.Count));
        }

        private void JumpLine(bool end)
        {
            ChallengeItem item = CurrentChoice();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("challenge_no_details"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(Loc.Get("combat_buffer_position", item.Lines[_lineIndex], _lineIndex + 1, item.Lines.Count));
        }

        private void Activate(ChallengeSelectionManager challenge)
        {
            if (_zone == Zone.Choices)
            {
                ActivateChoice(challenge);
            }
            else if (_zone == Zone.Heroes)
            {
                int hero = CurrentHeroIndex();
                if (hero >= 0)
                {
                    AtOManager.Instance.SideBarCharacterClicked(hero);
                    ScreenReader.Say(Loc.Get("challenge_current_hero", HeroName(hero)));
                }
            }
            else
            {
                ActivateAction(challenge);
            }
        }

        private void ActivateChoice(ChallengeSelectionManager challenge)
        {
            ChallengeItem item = CurrentChoice();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("challenge_no_choice"));
                return;
            }

            if (item.Selected)
            {
                ScreenReader.Say(Loc.Get("challenge_already_selected"));
                return;
            }

            if (!item.Available)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                return;
            }

            if (item.Perk != null)
            {
                challenge.AssignPerk(challenge.currentHeroIndex, item.PerkIndex);
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                _lastHeader = null;
                _lastFocusKey = null;
                return;
            }

            if (item.PackIndex >= 0)
            {
                challenge.SelectPack(challenge.currentHeroIndex, item.PackIndex);
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                _lastHeader = null;
                _lastFocusKey = null;
            }
        }

        private void ActivateAction(ChallengeSelectionManager challenge)
        {
            ActionItem item = CurrentAction();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            if (!item.Available)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                return;
            }

            if (item.Reroll)
            {
                challenge.RerollFromButton();
            }
            else if (item.Ready)
            {
                challenge.Ready();
            }
            else if (item.Button != null)
            {
                item.Button.Clicked();
            }

            ScreenReader.Say(Loc.Get(item.Ready ? "activated_loading" : "activated", item.Summary));
            _lastHeader = null;
            _lastFocusKey = null;
        }

        private void AnnounceFocused(bool queued = false)
        {
            string key = FocusKey();
            string text = FocusText();
            if (key == _lastFocusKey && !queued)
            {
                return;
            }

            _lastFocusKey = key;
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private string FocusKey()
        {
            if (_zone == Zone.Choices)
            {
                ChallengeItem item = CurrentChoice();
                return "choice:" + (item != null ? item.Summary : _choiceIndex.ToString());
            }

            if (_zone == Zone.Heroes)
            {
                return "hero:" + CurrentHeroIndex();
            }

            return "action:" + _actionIndex;
        }

        private string FocusText()
        {
            if (_zone == Zone.Choices)
            {
                ChallengeItem item = CurrentChoice();
                return item != null ? Loc.Get("challenge_choice_position", _choiceIndex + 1, _choices.Count, item.Summary) : Loc.Get("challenge_no_choice");
            }

            if (_zone == Zone.Heroes)
            {
                int hero = CurrentHeroIndex();
                return hero >= 0 ? Loc.Get("challenge_hero_position", _heroIndex + 1, _heroes.Count, HeroName(hero)) : Loc.Get("unknown_hero");
            }

            ActionItem action = CurrentAction();
            if (action == null)
            {
                return Loc.Get("no_menu_item");
            }

            string summary = action.Available ? action.Summary : Loc.Get("menu_item_unavailable", action.Summary);
            return Loc.Get("challenge_action_position", _actionIndex + 1, _actions.Count, summary);
        }

        private ChallengeItem CurrentChoice()
        {
            if (_choices.Count == 0)
            {
                return null;
            }

            _choiceIndex = ClampIndex(_choiceIndex, _choices.Count);
            return _choices[_choiceIndex];
        }

        private int CurrentHeroIndex()
        {
            if (_heroes.Count == 0)
            {
                return -1;
            }

            _heroIndex = ClampIndex(_heroIndex, _heroes.Count);
            return _heroes[_heroIndex];
        }

        private ActionItem CurrentAction()
        {
            if (_actions.Count == 0)
            {
                return null;
            }

            _actionIndex = ClampIndex(_actionIndex, _actions.Count);
            return _actions[_actionIndex];
        }

        private bool CurrentZoneHasItems()
        {
            return (_zone == Zone.Choices && _choices.Count > 0) ||
                (_zone == Zone.Heroes && _heroes.Count > 0) ||
                (_zone == Zone.Actions && _actions.Count > 0);
        }

        private void MoveToFirstAvailableZone()
        {
            if (_choices.Count > 0)
            {
                _zone = Zone.Choices;
            }
            else if (_heroes.Count > 0)
            {
                _zone = Zone.Heroes;
            }
            else
            {
                _zone = Zone.Actions;
            }
        }

        private static string ReadChallengeTitle(CardCraftManager craft, int index, string fallback)
        {
            if (craft.cardChallengeTitle == null || index < 0 || index >= craft.cardChallengeTitle.Length || craft.cardChallengeTitle[index] == null)
            {
                return fallback;
            }

            TMP_Text text = craft.cardChallengeTitle[index].GetComponentInChildren<TMP_Text>(true);
            string clean = text != null ? Clean(text.text) : "";
            return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
        }

        private static bool IsButtonAvailable(Transform[] buttons, int index)
        {
            if (buttons == null || index < 0 || index >= buttons.Length || buttons[index] == null)
            {
                return false;
            }

            BotonGeneric button = buttons[index].GetComponent<BotonGeneric>();
            return buttons[index].gameObject.activeInHierarchy && Functions.TransformIsVisible(buttons[index]) && button != null && button.IsEnabled();
        }

        private static bool IsTransformVisible(Transform[] transforms, int index)
        {
            return transforms != null &&
                index >= 0 &&
                index < transforms.Length &&
                transforms[index] != null &&
                transforms[index].gameObject.activeInHierarchy &&
                Functions.TransformIsVisible(transforms[index]);
        }

        private static void AddCardLine(List<string> lines, CardData data)
        {
            if (data == null)
            {
                return;
            }

            lines.Add(Loc.Get("challenge_card_line", Clean(GameText.CardName(data)), data.EnergyCost, GameText.CardTypeName(data.CardType)));
        }

        private static void AddCardDetails(List<string> lines, CardData data)
        {
            if (data == null)
            {
                return;
            }

            lines.Add(Clean(data.CardName));
            lines.Add(Loc.Get("combat_card_cost", data.EnergyCost));
            lines.Add(Loc.Get("combat_card_type", GameText.CardTypeName(data.CardType)));
            lines.Add(Loc.Get("combat_card_target", Clean(data.Target)));
            int damage = data.DamagePreCalculated != 0 ? data.DamagePreCalculated : data.Damage;
            if (damage != 0)
            {
                lines.Add(Loc.Get("combat_card_damage", damage));
            }

            if (data.Heal != 0)
            {
                lines.Add(Loc.Get("combat_card_heal", data.Heal));
            }

            string description = Clean(!string.IsNullOrWhiteSpace(data.DescriptionNormalized) ? data.DescriptionNormalized : data.Description);
            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(Loc.Get("combat_card_description", description));
            }
        }

        private static void AddPerkDetailLines(List<string> lines, PerkData data)
        {
            string description = Clean(Perk.PerkDescription(data, doPopup: true, data.Level, 0, enabled: true, active: false));
            if (!string.IsNullOrWhiteSpace(description))
            {
                lines.Add(description);
            }

            if (data.MaxHealth != 0)
            {
                lines.Add(Loc.Get("item_max_health", data.MaxHealth));
            }

            if (data.EnergyBegin != 0)
            {
                lines.Add(Loc.Get("item_energy", data.EnergyBegin));
            }

            if (data.SpeedQuantity != 0)
            {
                lines.Add(Loc.Get("challenge_speed", data.SpeedQuantity));
            }

            if (data.DamageFlatBonus != Enums.DamageType.None && data.DamageFlatBonusValue != 0)
            {
                lines.Add(Loc.Get("item_damage_bonus", GameText.DamageTypeName(data.DamageFlatBonus), data.DamageFlatBonusValue));
            }

            if (data.ResistModified != Enums.DamageType.None && data.ResistModifiedValue != 0)
            {
                lines.Add(Loc.Get("item_resist", GameText.DamageTypeName(data.ResistModified), data.ResistModifiedValue));
            }

            if (data.AuracurseBonus != null && data.AuracurseBonusValue != 0)
            {
                lines.Add(Loc.Get("item_aura_bonus", Clean(GameText.AuraCurseName(data.AuracurseBonus)), data.AuracurseBonusValue));
            }
        }

        private static string BuildPerkName(PerkData data)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(data.Id))
            {
                parts.Add(Clean(GameText.Get(data.Id)));
            }

            if (!string.IsNullOrWhiteSpace(data.IconTextValue))
            {
                parts.Add(Clean(data.IconTextValue));
            }

            string joined = string.Join(" ", parts.ToArray()).Trim();
            return string.IsNullOrWhiteSpace(joined) ? data.Id : joined;
        }

        private static PerkData ReadPerkData(PerkChallengeItem item)
        {
            return PerkDataField != null ? PerkDataField.GetValue(item) as PerkData : null;
        }

        private static int ReadPerkIndex(PerkChallengeItem item)
        {
            if (PerkIndexField == null)
            {
                return -1;
            }

            object value = PerkIndexField.GetValue(item);
            return value is int ? (int)value : -1;
        }

        private static bool ReadBool(FieldInfo field, object target)
        {
            if (field == null || target == null)
            {
                return false;
            }

            object value = field.GetValue(target);
            return value is bool && (bool)value;
        }

        private static string HeroName(int index)
        {
            Hero hero = AtOManager.Instance != null ? AtOManager.Instance.GetHero(index) : null;
            if (hero == null || hero.HeroData == null)
            {
                return Loc.Get("unknown_hero");
            }

            string source = Clean(hero.SourceName);
            string subclass = hero.HeroData.HeroSubClass != null ? Clean(hero.HeroData.HeroSubClass.SubClassName) : "";
            if (string.IsNullOrWhiteSpace(source))
            {
                source = Clean(hero.HeroData.HeroName);
            }

            return string.IsNullOrWhiteSpace(subclass) || source == subclass ? source : source + ", " + subclass;
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
    }
}
