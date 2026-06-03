using BepInEx.Configuration;

namespace AccessTheObelisk
{
    /// <summary>
    /// Stores user-configurable accessibility mod settings.
    /// </summary>
    public static class ModSettings
    {
        private static ConfigEntry<bool> _mapDetailsEnabled;
        private static ConfigEntry<bool> _enemyPlayedCardsEnabled;

        /// <summary>
        /// Initializes persisted mod settings.
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            _mapDetailsEnabled = config.Bind(
                "Accessibility",
                "MapDetailsEnabled",
                true,
                "Speak detailed map location information and route hints.");

            _enemyPlayedCardsEnabled = config.Bind(
                "Accessibility",
                "EnemyPlayedCardsEnabled",
                false,
                "Speak enemy card names when enemies play visible cards in combat.");
        }

        /// <summary>
        /// True when map speech includes detailed status, type, and localized map phrases.
        /// </summary>
        public static bool MapDetailsEnabled
        {
            get { return _mapDetailsEnabled == null || _mapDetailsEnabled.Value; }
            set
            {
                if (_mapDetailsEnabled != null)
                {
                    _mapDetailsEnabled.Value = value;
                }
            }
        }

        /// <summary>
        /// True when enemy card names are spoken as enemies play them.
        /// </summary>
        public static bool EnemyPlayedCardsEnabled
        {
            get { return _enemyPlayedCardsEnabled != null && _enemyPlayedCardsEnabled.Value; }
            set
            {
                if (_enemyPlayedCardsEnabled != null)
                {
                    _enemyPlayedCardsEnabled.Value = value;
                }
            }
        }
    }
}
