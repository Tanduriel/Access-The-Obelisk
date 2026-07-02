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
        private static ConfigEntry<bool> _repeatSingleItemEnabled;
        private static ConfigEntry<bool> _deathEffectRemovalsEnabled;

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

            _repeatSingleItemEnabled = config.Bind(
                "Accessibility",
                "RepeatSingleItemEnabled",
                true,
                "Re-announce the only item in a list when navigating, even though it is the sole element.");

            _deathEffectRemovalsEnabled = config.Bind(
                "Accessibility",
                "DeathEffectRemovalsEnabled",
                false,
                "Speak effects removed from a dead target (hero or enemy).");
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

        /// <summary>
        /// True when navigating re-announces a list's only item; false leaves it silent.
        /// </summary>
        public static bool RepeatSingleItemEnabled
        {
            get { return _repeatSingleItemEnabled == null || _repeatSingleItemEnabled.Value; }
            set
            {
                if (_repeatSingleItemEnabled != null)
                {
                    _repeatSingleItemEnabled.Value = value;
                }
            }
        }

        /// <summary>
        /// True when a dead target's own effect removals are announced;
        /// false silences them and keeps only effect changes on other, still-living targets.
        /// </summary>
        public static bool DeathEffectRemovalsEnabled
        {
            get { return _deathEffectRemovalsEnabled != null && _deathEffectRemovalsEnabled.Value; }
            set
            {
                if (_deathEffectRemovalsEnabled != null)
                {
                    _deathEffectRemovalsEnabled.Value = value;
                }
            }
        }
    }
}
