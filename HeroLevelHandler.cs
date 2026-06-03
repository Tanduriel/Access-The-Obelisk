using HarmonyLib;
using System.Collections.Generic;

namespace AccessTheObelisk
{
    [HarmonyPatch(typeof(AtOManager), nameof(AtOManager.HeroLevelUp))]
    internal static class HeroLevelHandler
    {
        private sealed class LevelSnapshot
        {
            public int Level;
            public int MaxHp;
            public int Speed;
            public int Energy;
            public int EnergyTurn;
        }

        private static void Prefix(AtOManager __instance, int heroIndex, out LevelSnapshot __state)
        {
            __state = TakeSnapshot(__instance, heroIndex);
        }

        private static void Postfix(AtOManager __instance, int heroIndex, string traitId, LevelSnapshot __state)
        {
            Hero hero = GetHero(__instance, heroIndex);
            if (hero == null || __state == null || hero.Level <= __state.Level)
            {
                return;
            }

            TraitData trait = Globals.Instance != null ? Globals.Instance.GetTraitData(traitId) : null;
            string message = BuildMessage(hero, trait, __state);
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
        }

        private static LevelSnapshot TakeSnapshot(AtOManager manager, int heroIndex)
        {
            Hero hero = GetHero(manager, heroIndex);
            if (hero == null)
            {
                return null;
            }

            return new LevelSnapshot
            {
                Level = hero.Level,
                MaxHp = hero.GetMaxHP(),
                Speed = hero.Speed,
                Energy = hero.Energy,
                EnergyTurn = hero.EnergyTurn
            };
        }

        private static Hero GetHero(AtOManager manager, int heroIndex)
        {
            Hero[] team = manager != null ? manager.GetTeam() : null;
            if (team == null || heroIndex < 0 || heroIndex >= team.Length)
            {
                return null;
            }

            return team[heroIndex];
        }

        private static string BuildMessage(Hero hero, TraitData trait, LevelSnapshot previous)
        {
            string heroName = TextCleaner.ToSpeech(hero.SourceName);
            string traitName = trait != null ? TextCleaner.ToSpeech(trait.TraitName) : Loc.Get("perk_unknown");
            string traitDescription = trait != null ? TextCleaner.ToSpeech(trait.Description) : "";
            string changes = BuildChanges(hero, previous);
            if (!string.IsNullOrWhiteSpace(changes))
            {
                return Loc.Get("hero_level_up_with_changes", heroName, hero.Level, traitName, changes, traitDescription);
            }

            return Loc.Get("hero_level_up", heroName, hero.Level, traitName, traitDescription);
        }

        private static string BuildChanges(Hero hero, LevelSnapshot previous)
        {
            List<string> changes = new List<string>();
            AddChange(changes, "hero_level_hp", hero.GetMaxHP() - previous.MaxHp);
            AddChange(changes, "hero_level_speed", hero.Speed - previous.Speed);
            AddChange(changes, "hero_level_energy", hero.Energy - previous.Energy);
            AddChange(changes, "hero_level_energy_turn", hero.EnergyTurn - previous.EnergyTurn);
            return string.Join(" ", changes.ToArray());
        }

        private static void AddChange(List<string> changes, string key, int delta)
        {
            if (delta != 0)
            {
                changes.Add(Loc.Get(key, FormatSigned(delta)));
            }
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }
    }
}
