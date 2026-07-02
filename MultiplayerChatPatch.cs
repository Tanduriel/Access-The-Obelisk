using HarmonyLib;
using Photon.Realtime;

namespace AccessTheObelisk
{
    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.ChatText))]
    internal static class MultiplayerChatPatch
    {
        private static void Postfix(string text, bool showAlertIfClosed)
        {
            string message = TextCleaner.ToSpeech(text);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string announcement = Loc.Get("multiplayer_chat_message", FormatNamePrefix(message));
            GameEventBuffer.Add(announcement);
            ScreenReader.SayQueued(announcement);
        }

        /// <summary>
        /// Converts the game's "[name] message" chat prefix into "name: message" for speech.
        /// </summary>
        private static string FormatNamePrefix(string message)
        {
            if (message.Length == 0 || message[0] != '[')
            {
                return message;
            }

            int end = message.IndexOf(']');
            if (end <= 1)
            {
                return message;
            }

            string name = message.Substring(1, end - 1).Trim();
            string rest = message.Substring(end + 1).TrimStart();
            if (name.Length == 0)
            {
                return message;
            }

            return rest.Length == 0 ? name + ":" : name + ": " + rest;
        }
    }

    [HarmonyPatch(typeof(ChatManager), nameof(ChatManager.WelcomeMsg))]
    internal static class MultiplayerChatWelcomePatch
    {
        private static void Postfix(string roomName)
        {
            string message = Loc.Get("multiplayer_room_joined", TextCleaner.ToSpeech(roomName));
            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnPlayerEnteredRoom))]
    internal static class MultiplayerPlayerEnteredPatch
    {
        private static void Postfix(Player other)
        {
            string playerName = PlayerName(other);
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            string message = Loc.Get("multiplayer_player_joined", playerName);
            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }

        private static string PlayerName(Player player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            NetworkManager network = NetworkManager.Instance;
            string nick = player.NickName;
            return TextCleaner.ToSpeech(network != null ? network.GetPlayerNickReal(nick) : nick);
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.OnPlayerLeftRoom))]
    internal static class MultiplayerPlayerLeftPatch
    {
        private static void Prefix(Player other)
        {
            string playerName = PlayerName(other);
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return;
            }

            string message = Loc.Get("multiplayer_player_left", playerName);
            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }

        private static string PlayerName(Player player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            NetworkManager network = NetworkManager.Instance;
            string nick = player.NickName;
            return TextCleaner.ToSpeech(network != null ? network.GetPlayerNickReal(nick) : nick);
        }
    }
}
