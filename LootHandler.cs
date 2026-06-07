using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for item and gold loot screens.
    /// </summary>
    public sealed class LootHandler
    {
        private sealed class LootItem
        {
            public string Summary;
            public string LootId;
            public bool IsGold;
            public readonly List<LootBuffer> Buffers = new List<LootBuffer>();
        }

        private sealed class LootBuffer
        {
            public string Name;
            public string FocusSummary;
            public readonly List<string> Lines = new List<string>();

            public string Summary
            {
                get { return !string.IsNullOrWhiteSpace(FocusSummary) ? FocusSummary : string.Join(" ", Lines.ToArray()); }
            }
        }

        private static readonly FieldInfo ActiveCharacterField = AccessTools.Field(typeof(LootManager), "activeCharacter");
        private static readonly FieldInfo CharacterOrderField = AccessTools.Field(typeof(LootManager), "characterOrder");

        private readonly List<int> _characters = new List<int>();
        private readonly List<LootItem> _items = new List<LootItem>();
        private int _characterListIndex;
        private int _itemIndex;
        private int _bufferIndex;
        private int _lineIndex;
        private bool _announced;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates loot screen navigation.
        /// </summary>
        public bool Update()
        {
            LootManager loot = LootManager.Instance;
            if (loot == null || !loot.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Loot);
            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(loot);
                AnnounceOnce(loot);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(loot);
            return true;
        }

        private void Reset()
        {
            _characters.Clear();
            _items.Clear();
            _characterListIndex = 0;
            _itemIndex = 0;
            _bufferIndex = 0;
            _lineIndex = 0;
            _announced = false;
        }

        private void Refresh(LootManager loot)
        {
            int previousCharacter = CurrentCharacterIndex();
            _characters.Clear();
            if (loot.characterLootArray != null)
            {
                for (int i = 0; i < loot.characterLootArray.Length; i++)
                {
                    CharacterLoot character = loot.characterLootArray[i];
                    if (character != null && character.gameObject.activeInHierarchy && Functions.TransformIsVisible(character.transform))
                    {
                        _characters.Add(i);
                    }
                }
            }

            if (_characters.Count == 0)
            {
                _items.Clear();
                return;
            }

            int restored = _characters.IndexOf(previousCharacter);
            _characterListIndex = restored >= 0 ? restored : ClampIndex(_characterListIndex, _characters.Count);
            BuildItems(loot);
            _itemIndex = ClampIndex(_itemIndex, _items.Count);
            _bufferIndex = ClampIndex(_bufferIndex, CurrentBufferCount());
            _lineIndex = ClampIndex(_lineIndex, CurrentLinesCount());
        }

        private void BuildItems(LootManager loot)
        {
            _items.Clear();
            if (loot.cardContainer != null)
            {
                foreach (Transform child in loot.cardContainer)
                {
                    CardItem card = child != null ? child.GetComponent<CardItem>() : null;
                    if (card == null || card.CardData == null || !card.gameObject.activeInHierarchy || IsCardDisabled(card))
                    {
                        continue;
                    }

                    LootItem item = BuildCardItem(card);
                    if (item != null)
                    {
                        _items.Add(item);
                    }
                }
            }

            LootItem gold = BuildGoldItem(loot);
            if (gold != null)
            {
                _items.Add(gold);
            }
        }

        private static bool IsCardDisabled(CardItem card)
        {
            return card.disableT != null && card.disableT.gameObject.activeInHierarchy;
        }

        private static LootItem BuildCardItem(CardItem card)
        {
            CardData data = card.CardData;
            if (data == null)
            {
                return null;
            }

            LootItem item = new LootItem();
            item.LootId = card.lootId;
            AddItemOverviewBuffer(item, data);
            AddItemEffectBuffer(item, data);
            item.Summary = CardSpeech.BuildItemFocusSummary(data);
            return item;
        }

        private static LootItem BuildGoldItem(LootManager loot)
        {
            if (loot.botonGold == null || !loot.botonGold.gameObject.activeInHierarchy || !loot.botonGold.IsEnabled())
            {
                return null;
            }

            string text = Clean(loot.botonGold.GetText());
            LootItem item = new LootItem();
            item.IsGold = true;
            LootBuffer buffer = new LootBuffer();
            buffer.Name = Loc.Get("loot_gold");
            AddLine(buffer, string.IsNullOrWhiteSpace(text) ? Loc.Get("loot_gold") : Loc.Get("loot_gold_quantity", text));
            item.Buffers.Add(buffer);
            item.Summary = buffer.Summary;
            return item;
        }

        private static void AddItemOverviewBuffer(LootItem item, CardData data)
        {
            LootBuffer buffer = new LootBuffer();
            buffer.Name = Loc.Get("item_overview");
            buffer.FocusSummary = CardSpeech.BuildItemFocusSummary(data);
            buffer.Lines.AddRange(CardSpeech.BuildItemOverviewLines(data));
            item.Buffers.Add(buffer);
        }

        private static void AddItemEffectBuffer(LootItem item, CardData data)
        {
            LootBuffer buffer = new LootBuffer();
            buffer.Name = Loc.Get("item_effects");
            buffer.Lines.AddRange(CardSpeech.BuildItemEffectLines(data));
            if (buffer.Lines.Count > 1)
            {
                item.Buffers.Add(buffer);
            }
        }

        private static void AddItemDataLines(LootBuffer buffer, ItemData item)
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

        private void AnnounceOnce(LootManager loot)
        {
            if (_announced || _characters.Count == 0)
            {
                return;
            }

            _announced = true;
            string subtitle = Clean(loot.subtitle != null ? loot.subtitle.text : "");
            string message = string.IsNullOrWhiteSpace(subtitle) ? Loc.Get("loot_screen") : Loc.Get("loot_screen_with_subtitle", subtitle);
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            AnnounceCharacter();
            AnnounceFocused(true);
        }

        private void ProcessKeys(LootManager loot)
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
                MoveBuffer(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveBuffer(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveCharacter(-1, loot);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveCharacter(1, loot);
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

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate(loot);
            }
        }

        private void MoveCharacter(int delta, LootManager loot)
        {
            if (_characters.Count == 0)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _characterListIndex, delta, _characters.Count))
            {
                return;
            }

            _itemIndex = 0;
            _bufferIndex = 0;
            _lineIndex = 0;
            SelectCharacter(loot);
            Refresh(loot);
            AnnounceCharacter();
            AnnounceFocused(true);
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _itemIndex, delta, _items.Count))
            {
                return;
            }

            _bufferIndex = 0;
            _lineIndex = 0;
            AnnounceFocused();
        }

        private void JumpItem(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _itemIndex, end, _items.Count))
            {
                return;
            }

            _bufferIndex = 0;
            _lineIndex = 0;
            AnnounceFocused();
        }

        private void MoveBuffer(int delta)
        {
            LootItem item = CurrentItem();
            if (item == null || item.Buffers.Count <= 1)
            {
                ScreenReader.Say(Loc.Get("loot_no_other_buffer"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _bufferIndex, delta, item.Buffers.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void MoveLine(int delta)
        {
            LootBuffer buffer = CurrentBuffer();
            if (buffer == null || buffer.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, buffer.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(buffer.Lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            LootBuffer buffer = CurrentBuffer();
            if (buffer == null || buffer.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, buffer.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(buffer.Lines[_lineIndex]);
        }

        private void Activate(LootManager loot)
        {
            LootItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            int currentCharacter = CurrentCharacterIndex();
            if (GetActiveCharacter(loot) != currentCharacter)
            {
                SelectCharacter(loot);
                ScreenReader.Say(Loc.Get("loot_character_selected_press_enter"));
                return;
            }

            if (!CanChooseLoot(loot, currentCharacter))
            {
                ScreenReader.Say(Loc.Get("loot_not_owner", LootOwnerName(loot, currentCharacter)));
                return;
            }

            string message = Loc.Get("activated", FocusSummary(item));
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            if (item.IsGold)
            {
                loot.LootGold();
            }
            else if (!string.IsNullOrWhiteSpace(item.LootId))
            {
                loot.Looted(item.LootId);
            }

            Refresh(loot);
        }

        private void SelectCharacter(LootManager loot)
        {
            int index = CurrentCharacterIndex();
            if (index >= 0)
            {
                loot.ChangeCharacter(index);
            }
        }

        private static int GetActiveCharacter(LootManager loot)
        {
            if (loot == null || ActiveCharacterField == null)
            {
                return -1;
            }

            try
            {
                return (int)ActiveCharacterField.GetValue(loot);
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("LootHandler failed to read activeCharacter: " + ex.Message);
                return -1;
            }
        }

        private void AnnounceCharacter()
        {
            Hero hero = CurrentHero();
            string name = hero != null ? Clean(hero.SourceName) : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Loc.Get("unknown_hero");
            }

            LootManager loot = LootManager.Instance;
            if (GameManager.Instance != null && GameManager.Instance.IsMultiplayer() && loot != null)
            {
                string owner = LootOwnerName(loot, CurrentCharacterIndex());
                string key = CanChooseLoot(loot, CurrentCharacterIndex()) ? "loot_character_owner" : "loot_character_owner_read_only";
                ScreenReader.Say(Loc.Get(key, name, owner));
                return;
            }

            ScreenReader.Say(Loc.Get("loot_character", name));
        }

        private void AnnounceFocused(bool queued = false)
        {
            LootItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("loot_no_item"));
                return;
            }

            string text = FocusSummary(item);
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private string FocusSummary(LootItem item)
        {
            LootBuffer buffer = CurrentBuffer();
            return buffer != null ? buffer.Summary : item.Summary;
        }

        private LootItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _itemIndex = ClampIndex(_itemIndex, _items.Count);
            return _items[_itemIndex];
        }

        private LootBuffer CurrentBuffer()
        {
            LootItem item = CurrentItem();
            if (item == null || item.Buffers.Count == 0)
            {
                return null;
            }

            _bufferIndex = ClampIndex(_bufferIndex, item.Buffers.Count);
            return item.Buffers[_bufferIndex];
        }

        private int CurrentBufferCount()
        {
            LootItem item = CurrentItem();
            return item != null ? item.Buffers.Count : 0;
        }

        private int CurrentLinesCount()
        {
            LootBuffer buffer = CurrentBuffer();
            return buffer != null ? buffer.Lines.Count : 0;
        }

        private int CurrentCharacterIndex()
        {
            if (_characters.Count == 0)
            {
                return -1;
            }

            _characterListIndex = ClampIndex(_characterListIndex, _characters.Count);
            return _characters[_characterListIndex];
        }

        private Hero CurrentHero()
        {
            int index = CurrentCharacterIndex();
            if (index < 0 || AtOManager.Instance == null)
            {
                return null;
            }

            int heroIndex = LootHeroIndex(LootManager.Instance, index);
            return heroIndex >= 0 ? AtOManager.Instance.GetHero(heroIndex) : null;
        }

        private static bool CanChooseLoot(LootManager loot, int characterIndex)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsMultiplayer())
            {
                return true;
            }

            string owner = LootOwnerNick(loot, characterIndex);
            if (string.IsNullOrWhiteSpace(owner) || NetworkManager.Instance == null)
            {
                return false;
            }

            return owner == NetworkManager.Instance.GetPlayerNick();
        }

        private static string LootOwnerName(LootManager loot, int characterIndex)
        {
            string owner = LootOwnerNick(loot, characterIndex);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return Loc.Get("unknown_player");
            }

            if (NetworkManager.Instance != null)
            {
                string realName = NetworkManager.Instance.GetPlayerNickReal(owner);
                if (!string.IsNullOrWhiteSpace(realName))
                {
                    return Clean(realName);
                }
            }

            return Clean(owner);
        }

        private static string LootOwnerNick(LootManager loot, int characterIndex)
        {
            if (AtOManager.Instance == null)
            {
                return "";
            }

            int heroIndex = LootHeroIndex(loot, characterIndex);
            if (heroIndex < 0)
            {
                return "";
            }

            Hero hero = AtOManager.Instance.GetHero(heroIndex);
            return hero != null ? hero.Owner : "";
        }

        private static int LootHeroIndex(LootManager loot, int characterIndex)
        {
            if (characterIndex < 0)
            {
                return -1;
            }

            if (loot != null && CharacterOrderField != null)
            {
                try
                {
                    List<int> order = CharacterOrderField.GetValue(loot) as List<int>;
                    if (order != null && characterIndex < order.Count)
                    {
                        return order[characterIndex];
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.LogState("LootHandler failed to read characterOrder: " + ex.Message);
                }
            }

            return characterIndex <= 3 ? characterIndex : -1;
        }

        private static void AddItemNumberLine(LootBuffer buffer, string key, int value)
        {
            if (value != 0)
            {
                AddLine(buffer, Loc.Get(key, value));
            }
        }

        private static void AddItemPercentLine(LootBuffer buffer, string key, float value)
        {
            if (value != 0f)
            {
                AddLine(buffer, Loc.Get(key, value));
            }
        }

        private static void AddItemDamageLine(LootBuffer buffer, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(buffer, Loc.Get("item_damage_bonus", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemDamagePercentLine(LootBuffer buffer, Enums.DamageType damageType, float value)
        {
            if (damageType != Enums.DamageType.None && value != 0f)
            {
                AddLine(buffer, Loc.Get("item_damage_percent", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemResistLine(LootBuffer buffer, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(buffer, Loc.Get("item_resist", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddAuraLine(LootBuffer buffer, string key, AuraCurseData aura, int value)
        {
            if (aura != null && value != 0)
            {
                AddLine(buffer, Loc.Get(key, Clean(GameText.AuraCurseName(aura)), value));
            }
        }

        private static void AddAuraNameLine(LootBuffer buffer, string key, AuraCurseData aura)
        {
            if (aura != null)
            {
                AddLine(buffer, Loc.Get(key, Clean(GameText.AuraCurseName(aura))));
            }
        }

        private static void AddLine(LootBuffer buffer, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                buffer.Lines.Add(line);
            }
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private static string Clean(string value)
        {
            return TextCleaner.ToSpeech(value);
        }
    }
}
