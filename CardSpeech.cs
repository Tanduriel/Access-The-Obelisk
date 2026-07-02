using System.Collections.Generic;

using Cards;
using Cards.Data;
namespace AccessTheObelisk
{
    internal static class CardSpeech
    {
        internal static List<string> BuildCardLines(CardRealtimeData data, int energyCost)
        {
            List<string> lines = new List<string>();
            if (data == null)
            {
                return lines;
            }

            AddLine(lines, CardNameWithRarity(data));
            AddLine(lines, Loc.Get("combat_card_cost", energyCost));
            AddLine(lines, RequirementLine(data));
            AddLine(lines, Loc.Get("combat_card_type", GameText.CardTypeName(data.CardType)));
            AddLine(lines, Loc.Get("combat_card_target", Clean(data.Target)));
            AddCardNumberLine(lines, "combat_card_damage", DamageValue(data), 0);
            AddCardNumberLine(lines, "combat_card_heal", HealValue(data), 0);
            AddLine(lines, Loc.Get("combat_card_description", Description(data)));
            AddLine(lines, Fluff(data));
            AddCardEffects(lines, data);
            return lines;
        }

        internal static string BuildCardFocusSummary(CardRealtimeData data, int energyCost)
        {
            if (data == null)
            {
                return Loc.Get("unknown_card");
            }

            string description = Description(data);
            string requirement = Requirement(data);
            if (!string.IsNullOrWhiteSpace(requirement))
            {
                if (string.IsNullOrWhiteSpace(description))
                {
                    return Loc.Get("combat_card_focus_summary_requirement_no_description", CardNameWithRarity(data), energyCost, requirement, GameText.CardTypeName(data.CardType), Clean(data.Target));
                }

                return Loc.Get("combat_card_focus_summary_requirement", CardNameWithRarity(data), energyCost, requirement, GameText.CardTypeName(data.CardType), Clean(data.Target), description);
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                return Loc.Get("combat_card_focus_summary_no_description", CardNameWithRarity(data), energyCost, GameText.CardTypeName(data.CardType), Clean(data.Target));
            }

            return Loc.Get("combat_card_focus_summary", CardNameWithRarity(data), energyCost, GameText.CardTypeName(data.CardType), Clean(data.Target), description);
        }

        /// <summary>
        /// Returns true when the card represents an equippable item, pet, or enchantment
        /// whose stat effects should be read instead of plain combat card lines.
        /// </summary>
        internal static bool IsItem(CardRealtimeData data)
        {
            return data != null && (data.CardClass == Enums.CardClass.Item || data.Item != null || data.ItemEnchantment != null);
        }

        /// <summary>
        /// Builds the full detail line list for a card or item, choosing item overview plus
        /// effect lines for equipment and plain combat card lines otherwise.
        /// </summary>
        internal static List<string> BuildDetailLines(CardRealtimeData data, int energyCost)
        {
            if (!IsItem(data))
            {
                return BuildCardLines(data, energyCost);
            }

            List<string> lines = BuildItemOverviewLines(data);
            List<string> effectLines = BuildItemEffectLines(data);
            for (int i = 1; i < effectLines.Count; i++)
            {
                AddLine(lines, effectLines[i]);
            }

            return lines;
        }

        /// <summary>
        /// Builds the focus summary for a card or item, choosing the item summary for equipment.
        /// </summary>
        internal static string BuildDetailSummary(CardRealtimeData data, int energyCost)
        {
            return IsItem(data) ? BuildItemFocusSummary(data) : BuildCardFocusSummary(data, energyCost);
        }

        internal static List<string> BuildItemOverviewLines(CardRealtimeData data)
        {
            List<string> lines = new List<string>();
            if (data == null)
            {
                return lines;
            }

            AddLine(lines, Loc.Get("item_overview"));
            AddLine(lines, CardNameWithRarity(data));
            AddLine(lines, Loc.Get("combat_card_type", GameText.CardTypeName(data.CardType)));
            AddLine(lines, Loc.Get("combat_card_description", Description(data)));
            AddLine(lines, Fluff(data));
            return lines;
        }

        internal static List<string> BuildItemEffectLines(CardRealtimeData data)
        {
            List<string> lines = new List<string>();
            if (data == null)
            {
                return lines;
            }

            AddLine(lines, Loc.Get("item_effects"));
            AddLine(lines, Loc.Get("combat_card_description", Description(data)));
            AddLine(lines, Fluff(data));
            AddItemDataLines(lines, data.Item);
            AddItemDataLines(lines, data.ItemEnchantment);
            return lines;
        }

        internal static string BuildItemFocusSummary(CardRealtimeData data)
        {
            if (data == null)
            {
                return Loc.Get("unknown_card");
            }

            string description = Description(data);
            if (string.IsNullOrWhiteSpace(description))
            {
                return Loc.Get("item_focus_summary_no_description", CardNameWithRarity(data), GameText.CardTypeName(data.CardType));
            }

            return Loc.Get("item_focus_summary", CardNameWithRarity(data), GameText.CardTypeName(data.CardType), description);
        }

        internal static string CardNameWithRarity(CardRealtimeData data)
        {
            if (data == null)
            {
                return Loc.Get("unknown_card");
            }

            return Loc.Get("card_name_with_rarity", Clean(GameText.CardName(data)), GameText.CardRarityName(data.CardRarity));
        }

        private static string Description(CardRealtimeData data)
        {
            if (data == null)
            {
                return "";
            }

            return Clean(data.DescriptionNormalized);
        }

        /// <summary>
        /// Resolves the runtime card for a card definition, returning null when it cannot be built.
        /// Used where the game now exposes a <see cref="CardDataNew"/> definition but a runtime card is needed.
        /// </summary>
        internal static CardRealtimeData Resolve(CardDataNew card)
        {
            return card != null && Globals.Instance != null
                ? Globals.Instance.GetCardData(card.Id, instantiate: false)
                : null;
        }

        /// <summary>
        /// Returns the precalculated damage dealt to the primary target, falling back to the
        /// base damage value when precalculated effects are not available.
        /// </summary>
        internal static int DamageValue(CardRealtimeData data)
        {
            if (data == null || !data.HasDamage)
            {
                return 0;
            }

            List<DamageEffectData> effects = data.DamageMergedPrecalculated != null && data.DamageMergedPrecalculated.Count > 0
                ? data.DamageMergedPrecalculated
                : data.DamageOriginal;
            if (effects == null)
            {
                return 0;
            }

            int anyDamage = 0;
            foreach (DamageEffectData effect in effects)
            {
                if (effect == null)
                {
                    continue;
                }

                if (anyDamage == 0)
                {
                    anyDamage = effect.Damage;
                }

                if (effect.DamageTarget == DamageTarget.Target)
                {
                    return effect.Damage;
                }
            }

            return anyDamage;
        }

        /// <summary>
        /// Returns the total healing performed by the card across all heal effects.
        /// </summary>
        internal static int HealValue(CardRealtimeData data)
        {
            if (data == null || data.Heal == null || !data.Heal.HasHeal || data.Heal.HealEffects == null)
            {
                return 0;
            }

            int total = 0;
            foreach (HealEffectData effect in data.Heal.HealEffects)
            {
                if (effect != null)
                {
                    total += effect.Heal;
                }
            }

            return total;
        }

        private static string RequirementLine(CardRealtimeData data)
        {
            string requirement = Requirement(data);
            return string.IsNullOrWhiteSpace(requirement) ? string.Empty : Loc.Get("combat_card_requirement", requirement);
        }

        private static string Requirement(CardRealtimeData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.EffectRequired))
            {
                return string.Empty;
            }

            try
            {
                return Clean(data.GetRequireText());
            }
            catch (System.Exception ex)
            {
                DebugLogger.LogState("Could not build card requirement for " + data.Id + ": " + ex.Message);
                return string.Empty;
            }
        }

        private static string Fluff(CardRealtimeData data)
        {
            if (data == null)
            {
                return "";
            }

            string fluff = Clean(data.Fluff);
            return string.IsNullOrWhiteSpace(fluff) ? "" : Loc.Get("card_fluff", fluff);
        }

        private static void AddCardNumberLine(List<string> lines, string key, int primary, int fallback)
        {
            int value = primary != 0 ? primary : fallback;
            if (value != 0)
            {
                AddLine(lines, Loc.Get(key, value));
            }
        }

        private static void AddCardEffects(List<string> lines, CardRealtimeData data)
        {
            if (data == null)
            {
                return;
            }

            if (data.Auras != null && data.Auras.Auras != null)
            {
                foreach (AuraEffectData aura in data.Auras.Auras)
                {
                    if (aura != null)
                    {
                        AddCardEffect(lines, aura.Aura, aura.Charges);
                    }
                }
            }

            if (data.Curses != null)
            {
                foreach (CurseEffectData curse in data.Curses)
                {
                    if (curse != null)
                    {
                        AddCardEffect(lines, curse.Curse, curse.Charges);
                    }
                }
            }
        }

        private static void AddCardEffect(List<string> lines, AuraCurseData effect, int charges)
        {
            if (effect == null || charges == 0)
            {
                return;
            }

            string name = Clean(GameText.AuraCurseName(effect));
            AddLine(lines, Loc.Get("combat_card_effect", name, charges));
            string description = Clean(GameText.AuraCurseDescription(effect, charges, null));
            if (!string.IsNullOrWhiteSpace(description))
            {
                AddLine(lines, Loc.Get("combat_effect_description", name, description));
            }
        }

        private static void AddItemDataLines(List<string> lines, ItemData item)
        {
            if (item == null)
            {
                return;
            }

            AddItemNumberLine(lines, "item_max_health", item.MaxHealth);
            AddItemNumberLine(lines, "item_energy", item.EnergyQuantity);
            AddItemNumberLine(lines, "item_draw_cards", item.DrawCards);
            AddItemNumberLine(lines, "item_heal", item.HealQuantity);
            AddItemNumberLine(lines, "item_heal_bonus", item.HealFlatBonus);
            AddItemPercentLine(lines, "item_heal_percent", item.HealPercentBonus);
            AddItemDamageLine(lines, item.DamageFlatBonus, item.DamageFlatBonusValue);
            AddItemDamageLine(lines, item.DamageFlatBonus2, item.DamageFlatBonusValue2);
            AddItemDamageLine(lines, item.DamageFlatBonus3, item.DamageFlatBonusValue3);
            AddItemDamagePercentLine(lines, item.DamagePercentBonus, item.DamagePercentBonusValue);
            AddItemDamagePercentLine(lines, item.DamagePercentBonus2, item.DamagePercentBonusValue2);
            AddItemDamagePercentLine(lines, item.DamagePercentBonus3, item.DamagePercentBonusValue3);
            AddItemResistLine(lines, item.ResistModified1, item.ResistModifiedValue1);
            AddItemResistLine(lines, item.ResistModified2, item.ResistModifiedValue2);
            AddItemResistLine(lines, item.ResistModified3, item.ResistModifiedValue3);
            AddAuraLine(lines, "item_aura_bonus", item.AuracurseBonus1, item.AuracurseBonusValue1);
            AddAuraLine(lines, "item_aura_bonus", item.AuracurseBonus2, item.AuracurseBonusValue2);
            AddAuraLine(lines, "item_aura_gain", item.AuracurseGain1, item.AuracurseGainValue1);
            AddAuraLine(lines, "item_aura_gain", item.AuracurseGain2, item.AuracurseGainValue2);
            AddAuraLine(lines, "item_aura_gain", item.AuracurseGain3, item.AuracurseGainValue3);
            AddAuraLine(lines, "item_self_aura_gain", item.AuracurseGainSelf1, item.AuracurseGainSelfValue1);
            AddAuraLine(lines, "item_self_aura_gain", item.AuracurseGainSelf2, item.AuracurseGainSelfValue2);
            AddAuraLine(lines, "item_self_aura_gain", item.AuracurseGainSelf3, item.AuracurseGainSelfValue3);
            AddAuraNameLine(lines, "item_aura_immunity", item.AuracurseImmune1);
            AddAuraNameLine(lines, "item_aura_immunity", item.AuracurseImmune2);
            AddPetCardLines(lines, item);
        }

        private static void AddItemNumberLine(List<string> lines, string key, int value)
        {
            if (value != 0)
            {
                AddLine(lines, Loc.Get(key, value));
            }
        }

        private static void AddItemPercentLine(List<string> lines, string key, float value)
        {
            if (value != 0f)
            {
                AddLine(lines, Loc.Get(key, value));
            }
        }

        private static void AddItemDamageLine(List<string> lines, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(lines, Loc.Get("item_damage_bonus", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemDamagePercentLine(List<string> lines, Enums.DamageType damageType, float value)
        {
            if (damageType != Enums.DamageType.None && value != 0f)
            {
                AddLine(lines, Loc.Get("item_damage_percent", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddItemResistLine(List<string> lines, Enums.DamageType damageType, int value)
        {
            if (damageType != Enums.DamageType.None && value != 0)
            {
                AddLine(lines, Loc.Get("item_resist", GameText.DamageTypeName(damageType), value));
            }
        }

        private static void AddAuraLine(List<string> lines, string key, AuraCurseData aura, int value)
        {
            if (aura != null && value != 0)
            {
                string name = Clean(GameText.AuraCurseName(aura));
                AddLine(lines, Loc.Get(key, name, value));
                AddAuraDescription(lines, name, aura);
            }
        }

        private static void AddAuraNameLine(List<string> lines, string key, AuraCurseData aura)
        {
            if (aura != null)
            {
                string name = Clean(GameText.AuraCurseName(aura));
                AddLine(lines, Loc.Get(key, name));
                AddAuraDescription(lines, name, aura);
            }
        }

        private static void AddAuraDescription(List<string> lines, string name, AuraCurseData aura)
        {
            string description = Clean(GameText.AuraCurseDescription(aura, 1, null));
            if (!string.IsNullOrWhiteSpace(description))
            {
                AddLine(lines, Loc.Get("combat_effect_description", name, description));
            }
        }

        private static void AddPetCardLines(List<string> lines, CardDataNew card)
        {
            if (card == null || Globals.Instance == null)
            {
                return;
            }

            AddPetCardLines(lines, Globals.Instance.GetCardData(card.Id, instantiate: false));
        }

        private static void AddPetCardLines(List<string> lines, ItemData item)
        {
            if (item.PetActivation == Enums.ActivePets.Self || item.PetActivation == Enums.ActivePets.AllTeam)
            {
                AddLine(lines, Loc.Get("item_pet_activation", Clean(item.PetActivation.ToString())));
            }

            AddPetCardLines(lines, item.CardToGain);
            if (item.CardToGainList == null)
            {
                return;
            }

            for (int i = 0; i < item.CardToGainList.Count; i++)
            {
                AddPetCardLines(lines, item.CardToGainList[i]);
            }
        }

        private static void AddPetCardLines(List<string> lines, CardRealtimeData card)
        {
            if (card == null)
            {
                return;
            }

            AddLine(lines, Loc.Get("item_pet_card", CardNameWithRarity(card)));
            List<string> cardLines = BuildCardLines(card, card.EnergyCost);
            for (int i = 1; i < cardLines.Count; i++)
            {
                AddLine(lines, cardLines[i]);
            }
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
