using HarmonyLib;

namespace AccessTheObelisk
{
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.ChatText))]
    internal static class MultiplayerChatPatch
    {
        private static void Postfix(string text, bool showAlertIfClosed)
        {
            if (!showAlertIfClosed)
            {
                return;
            }

            string message = TextCleaner.ToSpeech(text);
            if (!string.IsNullOrWhiteSpace(message))
            {
                ScreenReader.SayQueued(Loc.Get("multiplayer_chat_message", message));
            }
        }
    }
}
