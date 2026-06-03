namespace AccessTheObelisk
{
    /// <summary>
    /// Tracks the current accessibility context.
    /// </summary>
    public static class AccessStateManager
    {
        /// <summary>
        /// Initializes state tracking.
        /// </summary>
        public static void Initialize()
        {
            CurrentState = AccessState.Unknown;
        }

        /// <summary>
        /// Current accessibility context.
        /// </summary>
        public static AccessState CurrentState { get; private set; }

        /// <summary>
        /// Changes the accessibility context.
        /// </summary>
        public static void SetState(AccessState state)
        {
            if (CurrentState == state)
            {
                return;
            }

            CurrentState = state;
            DebugLogger.LogState("Accessibility state: " + state);
        }
    }

    /// <summary>
    /// Accessibility contexts used by handlers.
    /// </summary>
    public enum AccessState
    {
        /// <summary>State is not known yet.</summary>
        Unknown,

        /// <summary>Main menu or title flow.</summary>
        MainMenu,

        /// <summary>Hero selection screen is active.</summary>
        HeroSelection,

        /// <summary>Pre-run madness or sandbox options are active.</summary>
        PreRunOptions,

        /// <summary>Obelisk Challenge draft screen is active.</summary>
        ChallengeSelection,

        /// <summary>Settings menu is active.</summary>
        Settings,

        /// <summary>Accessibility mod settings menu is active.</summary>
        ModSettings,

        /// <summary>Confirmation or information alert is active.</summary>
        Alert,

        /// <summary>Tutorial popup is active.</summary>
        TutorialPopup,

        /// <summary>Combat encounter is active.</summary>
        Combat,

        /// <summary>Event screen is active.</summary>
        Event,

        /// <summary>Map screen is active.</summary>
        Map,

        /// <summary>Town screen is active.</summary>
        Town,

        /// <summary>Reward screen is active.</summary>
        Rewards,

        /// <summary>Loot screen is active.</summary>
        Loot,

        /// <summary>End-of-run results or unlock screen is active.</summary>
        FinishRun,

        /// <summary>Hero perk tree is active.</summary>
        PerkTree,

        /// <summary>Map corruption reward choice is active.</summary>
        Corruption,

        /// <summary>Character deck view is active.</summary>
        CharacterDeck,

        /// <summary>Full character information window is active.</summary>
        CharacterInfo,

        /// <summary>Tome of Knowledge screen is active.</summary>
        Tome,

        /// <summary>Paradox legal document screen is active.</summary>
        ParadoxDocument,

        /// <summary>Cinematic scene is active.</summary>
        Cinematic
    }
}
