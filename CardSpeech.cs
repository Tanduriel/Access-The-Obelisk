using System.Collections.Generic;

namespace AccessTheObelisk
{
    internal static class CardSpeech
    {
        internal static List<string> BuildCardLines(CardData data, int energyCost)
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
            AddCardNumberLine(lines, "combat_card_damage", data.DamagePreCalculated, data.Damage);
            AddCardNumberLine(lines, "combat_card_heal", data.Heal, 0);
            AddLine(lines, Loc.Get("combat_card_description", Description(data)));
            AddLine(lines, Fluff(data));
            AddCardEffects(lines, data);
            return lines;
        }

        internal static string BuildCardFocusSummary(CardData data, int energyCost)
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

        internal static List<string> BuildItemOverviewLines(CardData data)
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

        internal static List<string> BuildItemEffectLines(CardData data)
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

        internal static string BuildItemFocusSummary(CardData data)
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

        internal static string CardNameWithRarity(CardData data)
        {
            if (data == null)
            {
                return Loc.Get("unknown_card");
            }

            return Loc.Get("card_name_with_rarity", Clean(GameText.CardName(data)), GameText.CardRarityName(data.CardRarity));
        }

        private static string Description(CardData data)
        {
            if (data == null)
            {
                return "";
            }

            if (string.IsNullOrWhiteSpace(data.DescriptionNormalized))
            {
                try
                {
                    data.SetDescriptionNew(false, null, false);
                }
                catch (System.Exception ex)
                {
                    DebugLogger.LogState("Could not build card description for " + data.Id + ": " + ex.Message);
                }
            }

            return Clean(!string.IsNullOrWhiteSpace(data.DescriptionNormalized) ? data.DescriptionNormalized : data.Description);
        }

        private static string RequirementLine(CardData data)
        {
            string requirement = Requirement(data);
            return string.IsNullOrWhiteSpace(requirement) ? string.Empty : Loc.Get("combat_card_requirement", requirement);
        }

        private static string Requirement(CardData data)
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

        private static string Fluff(CardData data)
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

        private static void AddCardEffects(List<string> lines, CardData data)
        {
            AddCardEffect(lines, data.Aura, data.AuraCharges);
            AddCardEffect(lines, data.AuraSelf, data.AuraCharges);
            AddCardEffect(lines, data.Aura2, data.AuraCharges2);
            AddCardEffect(lines, data.AuraSelf2, data.AuraCharges2);
            AddCardEffect(lines, data.Aura3, data.AuraCharges3);
            AddCardEffect(lines, data.AuraSelf3, data.AuraCharges3);
            AddCardEffect(lines, data.Curse, data.CurseCharges);
            AddCardEffect(lines, data.CurseSelf, data.CurseCharges);
            AddCardEffect(lines, data.Curse2, data.CurseCharges2);
            AddCardEffect(lines, data.CurseSelf2, data.CurseCharges2);
            AddCardEffect(lines, data.Curse3, data.CurseCharges3);
            AddCardEffect(lines, data.CurseSelf3, data.CurseCharges3);
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

        private static void AddPetCardLines(List<string> lines, CardData card)
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
