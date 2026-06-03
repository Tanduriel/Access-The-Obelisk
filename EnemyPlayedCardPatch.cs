using HarmonyLib;

namespace AccessTheObelisk
{
    /// <summary>
    /// Announces visible enemy card names when enemies play cards.
    /// </summary>
    [HarmonyPatch(typeof(NPC), nameof(NPC.CastCardNPC))]
    public static class EnemyPlayedCardPatch
    {
        /// <summary>
        /// Reads the NPC card before the game removes it from the visible enemy card array.
        /// </summary>
        public static void Prefix(NPC __instance, int theCard)
        {
            if (!ModSettings.EnemyPlayedCardsEnabled || __instance == null || __instance.NPCItem == null || __instance.NPCItem.cardsCI == null)
            {
                return;
            }

            if (theCard < 0 || theCard >= __instance.NPCItem.cardsCI.Length)
            {
                return;
            }

            CardItem card = __instance.NPCItem.cardsCI[theCard];
            if (card == null || card.CardData == null)
            {
                return;
            }

            string enemyName = TextCleaner.ToSpeech(__instance.SourceName);
            string cardName = GameText.CardName(card.CardData);
            ScreenReader.SayQueued(Loc.Get("combat_enemy_played_card", enemyName, cardName));
        }
    }
}
