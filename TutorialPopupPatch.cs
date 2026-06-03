using HarmonyLib;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Hooks game tutorial popup creation after the game has filled its text.
    /// </summary>
    [HarmonyPatch(typeof(PopTutorialManager), nameof(PopTutorialManager.Show))]
    public static class TutorialPopupPatch
    {
        /// <summary>
        /// Announces tutorial text after the game shows it.
        /// </summary>
        public static void Postfix(PopTutorialManager __instance, string type, Vector3 position, Vector3 position2)
        {
            TutorialPopupHandler.Announce(__instance, type);
        }
    }
}
