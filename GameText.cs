using System;
using System.Collections.Generic;

namespace AccessTheObelisk
{
    /// <summary>
    /// Reads localized game text with safe fallbacks for screen-reader output.
    /// </summary>
    public static class GameText
    {
        private const int MaxAuraCurseDescriptionCacheEntries = 512;
        private static readonly Dictionary<string, string> AuraCurseDescriptionCache = new Dictionary<string, string>();
        private static string _auraCurseDescriptionCacheLanguage;

        /// <summary>
        /// Gets localized text from the game's translation table.
        /// </summary>
        public static string Get(string id, string type = "")
        {
            if (Texts.Instance == null || string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            try
            {
                string text = Texts.Instance.GetText(id, type);
                return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("GameText failed to read text '" + id + "': " + ex.Message);
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a localized aura or curse name.
        /// </summary>
        public static string AuraCurseName(AuraCurseData effect)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            string name = Get(effect.Id);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Get(effect.ACName);
            }

            return string.IsNullOrWhiteSpace(name) ? effect.ACName : name;
        }

        /// <summary>
        /// Gets a localized card or item name.
        /// </summary>
        public static string CardName(CardData card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            string id = !string.IsNullOrWhiteSpace(card.UpgradedFrom) ? card.UpgradedFrom : card.Id;
            id = TruncateNecropolisCardId(id.Replace("v2", string.Empty));
            string name = Get("c_" + id + "_name", "cards");
            return string.IsNullOrWhiteSpace(name) ? card.CardName : name;
        }

        /// <summary>
        /// Gets a localized card rarity name.
        /// </summary>
        public static string CardRarityName(Enums.CardRarity rarity)
        {
            return LocalizedEnum("card_rarity_", rarity.ToString());
        }

        /// <summary>
        /// Gets a localized card type name.
        /// </summary>
        public static string CardTypeName(Enums.CardType type)
        {
            return LocalizedEnum("card_type_", type.ToString());
        }

        /// <summary>
        /// Gets a localized damage type name.
        /// </summary>
        public static string DamageTypeName(Enums.DamageType type)
        {
            if (type == Enums.DamageType.None)
            {
                return string.Empty;
            }

            return LocalizedEnum("damage_", type.ToString());
        }

        /// <summary>
        /// Gets a localized aura or curse description.
        /// </summary>
        public static string AuraCurseDescription(AuraCurseData effect)
        {
            return AuraCurseDescription(effect, 1, null);
        }

        /// <summary>
        /// Gets a localized aura or curse description with game placeholder values applied.
        /// </summary>
        public static string AuraCurseDescription(AuraCurseData effect, int charges, Character character)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            string language = CurrentLanguageKey();
            string cacheKey = AuraCurseDescriptionCacheKey(effect, charges, character);
            string cached;
            if (TryGetAuraCurseDescriptionCache(cacheKey, language, out cached))
            {
                return cached;
            }

            AuraCurseData modified = ApplyAuraCurseRuntimeModifiers(effect, character);
            string description = Get(modified.Id + "_description", "auracurse");
            if (string.IsNullOrWhiteSpace(description))
            {
                description = modified.Description;
            }

            string result = ApplyAuraCurseDescriptionReplacements(description, modified, charges, character);
            StoreAuraCurseDescriptionCache(cacheKey, result, language);
            return result;
        }

        private static bool TryGetAuraCurseDescriptionCache(string key, string language, out string value)
        {
            value = null;
            if (_auraCurseDescriptionCacheLanguage != language)
            {
                AuraCurseDescriptionCache.Clear();
                _auraCurseDescriptionCacheLanguage = language;
                return false;
            }

            return AuraCurseDescriptionCache.TryGetValue(key, out value);
        }

        private static void StoreAuraCurseDescriptionCache(string key, string value, string language)
        {
            if (_auraCurseDescriptionCacheLanguage != language)
            {
                AuraCurseDescriptionCache.Clear();
                _auraCurseDescriptionCacheLanguage = language;
            }

            if (AuraCurseDescriptionCache.Count >= MaxAuraCurseDescriptionCacheEntries)
            {
                AuraCurseDescriptionCache.Clear();
            }

            AuraCurseDescriptionCache[key] = value ?? string.Empty;
        }

        private static string AuraCurseDescriptionCacheKey(AuraCurseData effect, int charges, Character character)
        {
            string id = !string.IsNullOrWhiteSpace(effect.Id) ? effect.Id : effect.ACName;
            string characterId = character == null ? "" : character.Id;
            return id + "|" + charges + "|" + characterId;
        }

        private static string CurrentLanguageKey()
        {
            try
            {
                if (Globals.Instance != null && !string.IsNullOrWhiteSpace(Globals.Instance.CurrentLang))
                {
                    return Globals.Instance.CurrentLang;
                }
            }
            catch
            {
            }

            return "";
        }

        /// <summary>
        /// Gets a localized display value for a TextMeshPro sprite name.
        /// </summary>
        public static string SpriteName(string spriteName)
        {
            string modText = SpriteNameFromModLocalization(spriteName);
            if (!string.IsNullOrWhiteSpace(modText))
            {
                return modText;
            }

            string text = Get(spriteName);
            return string.IsNullOrWhiteSpace(text) ? spriteName : text;
        }

        private static string SpriteNameFromModLocalization(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return string.Empty;
            }

            string key = "speech_sprite_" + spriteName.Trim().ToLowerInvariant().Replace(" ", "");
            string text = Loc.Get(key);
            return string.Equals(text, key, StringComparison.Ordinal) ? string.Empty : text;
        }

        private static AuraCurseData ApplyAuraCurseRuntimeModifiers(AuraCurseData effect, Character character)
        {
            if (effect == null || character == null || AtOManager.Instance == null)
            {
                return effect;
            }

            try
            {
                AuraCurseData modified = AtOManager.Instance.GlobalAuraCurseModificationByTraitsAndItems("set", effect.Id, character, character, true);
                return modified ?? effect;
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("Could not apply aura/curse runtime modifiers for " + effect.Id + ": " + ex.Message);
                return effect;
            }
        }

        private static string ApplyAuraCurseDescriptionReplacements(string description, AuraCurseData effect, int charges, Character character)
        {
            if (string.IsNullOrWhiteSpace(description) || effect == null)
            {
                return description;
            }

            Dictionary<string, string> replacements = new Dictionary<string, string>();
            int safeCharges = charges == 0 ? 1 : charges;
            AddReplacement(replacements, "<ChargesValueBy14>", FloorToInt(Math.Abs(safeCharges / 14f)));
            AddReplacement(replacements, "<ChargesCurrent>", safeCharges);
            AddReplacement(replacements, "<ChargesCurrentHalf>", RoundToInt(safeCharges * 0.5f));
            AddReplacement(replacements, "<ChargesMultiplier>", RoundToInt(effect.ChargesMultiplierDescription * safeCharges));
            AddReplacement(replacements, "<ChargesMultiplier_sec>", RoundToInt(effect.ChargesMultiplierDescription * safeCharges));
            AddReplacement(replacements, "<ChargesMultiplierHalf>", RoundToInt(effect.ChargesMultiplierDescription * RoundToInt(safeCharges * 0.5f)));
            AddReplacement(replacements, "<ChargesAux1>", CalculateChargesAux(safeCharges, effect.ChargesAuxNeedForOne1));
            AddReplacement(replacements, "<ChargesAux1_sec>", CalculateChargesAux(safeCharges, effect.ChargesAuxNeedForOne1));
            AddReplacement(replacements, "<ChargesAux2>", CalculateChargesAux(safeCharges, effect.ChargesAuxNeedForOne2));
            AddReplacement(replacements, "<ChargesAux2_sec>", CalculateChargesAux(safeCharges, effect.ChargesAuxNeedForOne2));
            AddReplacement(replacements, "<CustomAuxValue>", effect.CustomAuxValue);
            AddReplacement(replacements, "<AuraDamageIncreasedPerStack>", CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPerStack, 0));
            AddReplacement(replacements, "<AuraDamageIncreasedPerStack2>", CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPerStack2, 0));
            AddReplacement(replacements, "<AuraDamageIncreasedPercentPerStack>", CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPercentPerStack, 0));
            AddReplacement(replacements, "<IncreasedDirectDamageReceivedPerStack>", CalculateAuraDamage(safeCharges, effect.IncreasedDirectDamageReceivedPerStack, effect.IncreasedDirectDamageChargesMultiplierNeededForOne));
            AddReplacement(replacements, "<IncreasedDirectDamageReceivedPerStack2>", CalculateAuraDamage(safeCharges, effect.IncreasedDirectDamageReceivedPerStack2, effect.IncreasedDirectDamageChargesMultiplierNeededForOne));
            AddReplacement(replacements, "<DamageAux1>", Math.Abs(CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPercentPerStack, 0)));
            AddReplacement(replacements, "<DamageAux1_sec>", Math.Abs(CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPercentPerStack, 0)));
            AddReplacement(replacements, "<DamageAux2>", Math.Abs(CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPercentPerStack2, 0)));
            AddReplacement(replacements, "<DamageAux2_sec>", Math.Abs(CalculateAuraDamage(safeCharges, effect.AuraDamageIncreasedPercentPerStack2, 0)));
            AddReplacement(replacements, "<ResistAux1>", Math.Abs(RoundToInt(safeCharges * effect.ResistModifiedPercentagePerStack)));
            AddReplacement(replacements, "<ResistAux2>", Math.Abs(RoundToInt(safeCharges * effect.ResistModifiedPercentagePerStack2)));
            AddReplacement(replacements, "<MaxCharges>", MaxChargesForDescription(effect));
            AddReplacement(replacements, "<HealReceivedPercent>", effect.HealReceivedPercent);
            AddReplacement(replacements, "<CharacterStatModifiedValue>", effect.CharacterStatModifiedValue);
            AddReplacement(replacements, "<CharacterStatModifiedPerStack>", CharacterStatPerStack(effect, safeCharges));
            AddReplacement(replacements, "<CharacterStatModifiedValuePerStackTotal>", CharacterStatPerStackTotal(effect, safeCharges));
            AddReplacement(replacements, "<DamageWhenConsumed>", effect.DamageWhenConsumed);
            AddReplacement(replacements, "<DamageSidesWhenConsumed>", effect.DamageSidesWhenConsumed);
            AddReplacement(replacements, "<HealAttackerConsumeCharges>", effect.HealAttackerConsumeCharges);
            AddReplacement(replacements, "<Resistance1>", RoundToInt(effect.ResistModifiedValue + RoundToInt(safeCharges * effect.ResistModifiedPercentagePerStack)));
            AddReplacement(replacements, "<DamageWhenConsumedPerCharge>", CalculateAuraDamage(safeCharges, effect.DamageWhenConsumedPerCharge, 0));
            AddReplacement(replacements, "<ExplodeAtStacks>", effect.ExplodeAtStacks);
            AddReplacement(replacements, "<ACOnExplode>", AuraCurseName(effect.ACOnExplode));
            AddReplacement(replacements, "<HealReceivedPercentPerStack>", RoundToInt(safeCharges * effect.HealReceivedPercentPerStack));
            AddReplacement(replacements, "<HealDonePercentPerStack>", RoundToInt(safeCharges * effect.HealDonePercentPerStack));
            AddReplacement(replacements, "<HealPerChargeOnExplode>", RoundToInt(safeCharges * effect.HealPerChargeOnExplode));
            AddReplacement(replacements, "<DamageZeal>", DamageZeal(effect, safeCharges, character));

            string result = description;
            foreach (KeyValuePair<string, string> replacement in replacements)
            {
                result = result.Replace(replacement.Key, replacement.Value);
            }

            return result;
        }

        private static void AddReplacement(Dictionary<string, string> replacements, string key, object value)
        {
            replacements[key] = value == null ? string.Empty : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int CalculateChargesAux(int charges, float chargesAuxNeedForOne)
        {
            if (chargesAuxNeedForOne == 0f)
            {
                return 0;
            }

            return FloorToInt(charges / chargesAuxNeedForOne);
        }

        private static int CalculateAuraDamage(int charges, float damagePerStack, float neededForOne)
        {
            if (neededForOne == 0f)
            {
                return RoundToInt(charges * damagePerStack);
            }

            float multiplier = 1f / neededForOne;
            if (damagePerStack < 0f)
            {
                return -1 * FloorToInt(Math.Abs(multiplier * charges * damagePerStack));
            }

            return FloorToInt(multiplier * charges * damagePerStack);
        }

        private static int MaxChargesForDescription(AuraCurseData effect)
        {
            int value = effect.MaxCharges;
            try
            {
                if ((MadnessManager.Instance != null && MadnessManager.Instance.IsMadnessTraitActive("restrictedpower")) ||
                    (AtOManager.Instance != null && AtOManager.Instance.IsChallengeTraitActive("restrictedpower")))
                {
                    value = effect.MaxMadnessCharges;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("Could not evaluate max aura/curse charges for " + effect.Id + ": " + ex.Message);
            }

            return value;
        }

        private static string CharacterStatPerStack(AuraCurseData effect, int charges)
        {
            int value = FloorToInt(charges * effect.CharacterStatModifiedValuePerStack);
            if (value > 0)
            {
                return "+" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string CharacterStatPerStackTotal(AuraCurseData effect, int charges)
        {
            float multiplier = effect.CharacterStatChargesMultiplierNeededForOne == 0 ? 0f : 1f / effect.CharacterStatChargesMultiplierNeededForOne;
            float raw = multiplier * charges * effect.CharacterStatModifiedValuePerStack;
            int value = effect.CharacterStatModifiedValuePerStack < 0f ? -FloorToInt(Math.Abs(raw)) : FloorToInt(raw);
            return value >= 0
                ? "+" + value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int DamageZeal(AuraCurseData effect, int charges, Character character)
        {
            if (effect.Id != "zeal" || character == null)
            {
                return 0;
            }

            int value = RoundToInt(character.GetAuraCharges("burn") * effect.AuraDamageIncreasedPercentPerStack);
            if (character.HaveTrait("righteousflame"))
            {
                value += RoundToInt(charges * effect.AuraDamageIncreasedPercentPerStack2);
            }

            return value;
        }

        private static int RoundToInt(float value)
        {
            return Functions.FuncRoundToInt(value);
        }

        private static int FloorToInt(float value)
        {
            return (int)Math.Floor(value);
        }
        private static string TruncateNecropolisCardId(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return string.Empty;
            }

            id = id.ToLowerInvariant();
            if (id.EndsWith("mnp", StringComparison.Ordinal) && !id.Contains("cataclysm"))
            {
                return id.Substring(0, id.Length - 3);
            }

            if (id.EndsWith("np", StringComparison.Ordinal))
            {
                return id.Substring(0, id.Length - 2);
            }

            return id;
        }

        private static string LocalizedEnum(string prefix, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            string key = prefix + raw.ToLowerInvariant();
            string localized = Loc.Get(key);
            if (!string.Equals(localized, key, StringComparison.Ordinal))
            {
                return localized;
            }

            return TextCleaner.ToSpeech(raw.Replace("_", " "));
        }
    }
}


