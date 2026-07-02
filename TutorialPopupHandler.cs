namespace AccessTheObelisk
{
    /// <summary>
    /// Announces tutorial popups shown by the game.
    /// </summary>
    public static class TutorialPopupHandler
    {
        private static string _lastAnnouncement;
        private static float _lastAnnouncementTime;

        /// <summary>
        /// Handles keyboard activation for the active tutorial popup.
        /// </summary>
        public static bool Update()
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsTutorialActive())
            {
                return false;
            }

            AccessStateManager.SetState(AccessState.TutorialPopup);
            if (ModInput.GetKeyDown(UnityEngine.KeyCode.Return) || ModInput.GetKeyDown(UnityEngine.KeyCode.KeypadEnter) || ModInput.GetKeyDown(UnityEngine.KeyCode.Space))
            {
                GameManager.Instance.HideTutorialPopup();
                ScreenReader.Say(Loc.Get("tutorial_continue"));
                return true;
            }

            return true;
        }

        /// <summary>
        /// Announces the current tutorial popup text.
        /// </summary>
        public static void Announce(PopTutorialManager popup, string type)
        {
            if (popup == null || popup.popText == null)
            {
                Main.Log.LogWarning("Tutorial popup had no readable text field.");
                return;
            }

            string text = TextCleaner.ToSpeech(popup.popText.text);
            if (string.IsNullOrWhiteSpace(text))
            {
                Main.Log.LogWarning("Tutorial popup text was empty for type: " + type);
                return;
            }

            string announcement = Loc.Get("tutorial_popup", text);
            if (announcement == _lastAnnouncement && UnityEngine.Time.unscaledTime - _lastAnnouncementTime < 1f)
            {
                return;
            }

            _lastAnnouncement = announcement;
            _lastAnnouncementTime = UnityEngine.Time.unscaledTime;
            AccessStateManager.SetState(AccessState.TutorialPopup);
            ScreenReader.Say(announcement);
        }
    }
}
