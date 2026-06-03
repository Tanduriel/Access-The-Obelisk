using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Announces subtitles shown by the game's cinematic scene.
    /// </summary>
    public sealed class CinematicHandler
    {
        private string _lastSpokenText;
        private bool _wasActive;

        /// <summary>
        /// Updates cinematic subtitle announcements.
        /// </summary>
        public bool Update()
        {
            CinematicManager cinematic = CinematicManager.Instance;
            if (cinematic == null || !IsActive(cinematic))
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Cinematic);
            _wasActive = true;

            string text = CurrentSubtitleText(cinematic);
            if (string.IsNullOrWhiteSpace(text) || text == _lastSpokenText)
            {
                return true;
            }

            _lastSpokenText = text;
            ScreenReader.Say(text);
            return true;
        }

        private void Reset()
        {
            if (!_wasActive)
            {
                return;
            }

            _wasActive = false;
            _lastSpokenText = null;
        }

        private static bool IsActive(CinematicManager cinematic)
        {
            return cinematic.gameObject != null && cinematic.gameObject.activeInHierarchy;
        }

        private static string CurrentSubtitleText(CinematicManager cinematic)
        {
            List<string> parts = new List<string>();
            AddText(parts, cinematic.textMiddle);
            AddText(parts, cinematic.textBottom);
            return string.Join(" ", parts.ToArray());
        }

        private static void AddText(List<string> parts, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            string clean = TextCleaner.ToSpeech(text.text);
            if (!string.IsNullOrWhiteSpace(clean) && !parts.Contains(clean))
            {
                parts.Add(clean);
            }
        }
    }
}
