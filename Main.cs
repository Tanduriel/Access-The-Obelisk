using BepInEx;
using HarmonyLib;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AccessTheObelisk
{
    /// <summary>
    /// Main BepInEx entry point for the Across the Obelisk accessibility mod.
    /// </summary>
    [BepInPlugin(PluginGuid, "AccessTheObelisk", PluginVersion)]
    public sealed class Main : BaseUnityPlugin
    {
        /// <summary>
        /// Stable plugin identifier used for Harmony ownership.
        /// </summary>
        public const string PluginGuid = "com.incognitus.accesstheobelisk";

        /// <summary>
        /// Current mod version used by BepInEx and the update checker.
        /// </summary>
        public const string PluginVersion = "0.4";

        private float _startupTimer;
        private float _lastMemoryLogTime;
        private bool _startupAnnounced;
        private Harmony _harmony;
        private MainMenuHandler _mainMenuHandler;
        private SettingsHandler _settingsHandler;
        private ModSettingsHandler _modSettingsHandler;
        private AlertHandler _alertHandler;
        private HeroSelectionHandler _heroSelectionHandler;
        private PreRunOptionsHandler _preRunOptionsHandler;
        private ChallengeSelectionHandler _challengeSelectionHandler;
        private CardPlayerHandler _cardPlayerHandler;
        private MapHandler _mapHandler;
        private EventHandler _eventHandler;
        private TownHandler _townHandler;
        private TownUpgradeHandler _townUpgradeHandler;
        private CardCraftHandler _cardCraftHandler;
        private RewardsHandler _rewardsHandler;
        private LootHandler _lootHandler;
        private FinishRunHandler _finishRunHandler;
        private PerkTreeHandler _perkTreeHandler;
        private CorruptionHandler _corruptionHandler;
        private ConflictHandler _conflictHandler;
        private CharacterInfoHandler _characterInfoHandler;
        private CharacterDeckHandler _characterDeckHandler;
        private CardScreenHandler _cardScreenHandler;
        private TomeHandler _tomeHandler;
        private ParadoxDocumentHandler _paradoxDocumentHandler;
        private LobbyHandler _lobbyHandler;
        private GiveHandler _giveHandler;
        private StoryIntroHandler _storyIntroHandler;
        private CinematicHandler _cinematicHandler;
        private CurrencyHotkeyHandler _currencyHotkeyHandler;
        private QuestRequirementHandler _questRequirementHandler;
        private CombatModalHandler _combatModalHandler;
        private CombatHandler _combatHandler;
        private UpdateCheckHandler _updateCheckHandler;

        /// <summary>
        /// Enables verbose accessibility logging.
        /// </summary>
        public static bool DebugMode { get; private set; }

        /// <summary>
        /// Shared BepInEx logger for helper classes.
        /// </summary>
        public static BepInEx.Logging.ManualLogSource Log { get; private set; }

        /// <summary>
        /// Active plugin instance used by non-MonoBehaviour handlers for coroutines.
        /// </summary>
        public static Main Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Loc.Initialize();
            ModSettings.Initialize(Config);
            ScreenReader.Initialize();
            AccessStateManager.Initialize();
            _mainMenuHandler = new MainMenuHandler();
            _settingsHandler = new SettingsHandler();
            _modSettingsHandler = new ModSettingsHandler();
            _alertHandler = new AlertHandler();
            _heroSelectionHandler = new HeroSelectionHandler();
            _preRunOptionsHandler = new PreRunOptionsHandler();
            _challengeSelectionHandler = new ChallengeSelectionHandler();
            _cardPlayerHandler = new CardPlayerHandler();
            _mapHandler = new MapHandler();
            _eventHandler = new EventHandler();
            _townHandler = new TownHandler();
            _townUpgradeHandler = new TownUpgradeHandler();
            _cardCraftHandler = new CardCraftHandler();
            _rewardsHandler = new RewardsHandler();
            _lootHandler = new LootHandler();
            _finishRunHandler = new FinishRunHandler();
            _perkTreeHandler = new PerkTreeHandler();
            _corruptionHandler = new CorruptionHandler();
            _conflictHandler = new ConflictHandler();
            _characterInfoHandler = new CharacterInfoHandler();
            _characterDeckHandler = new CharacterDeckHandler();
            _cardScreenHandler = new CardScreenHandler();
            _tomeHandler = new TomeHandler();
            _paradoxDocumentHandler = new ParadoxDocumentHandler();
            _lobbyHandler = new LobbyHandler();
            _giveHandler = new GiveHandler();
            _storyIntroHandler = new StoryIntroHandler();
            _cinematicHandler = new CinematicHandler();
            _currencyHotkeyHandler = new CurrencyHotkeyHandler();
            _questRequirementHandler = new QuestRequirementHandler();
            _combatModalHandler = new CombatModalHandler();
            _combatHandler = new CombatHandler();
            _updateCheckHandler = new UpdateCheckHandler();
            ApplyPatches();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("AccessTheObelisk initialized.");
        }

        private void Update()
        {
            ScreenReader.Update();
            TrackActivationKeyForSpeechCleanup();
            ToggleDebugModeHotkey();
            LogMemoryDiagnosticsIfDue();
            AnnounceStartupOnce();
            if (_modSettingsHandler.Update())
            {
                return;
            }

            if (_currencyHotkeyHandler.Update())
            {
                return;
            }

            if (_questRequirementHandler.Update())
            {
                return;
            }

            if (GameEventBuffer.Update())
            {
                return;
            }

            bool tutorialActive = TutorialPopupHandler.Update();
            if (tutorialActive)
            {
                return;
            }

            if (_alertHandler.Update())
            {
                return;
            }

            if (InputActivationGuard.ShouldBlockSubmit())
            {
                return;
            }

            if (_combatModalHandler.Update())
            {
                return;
            }

            if (_cardScreenHandler.Update())
            {
                return;
            }

            if (_characterInfoHandler.Update())
            {
                return;
            }

            if (_characterDeckHandler.Update())
            {
                return;
            }

            if (_tomeHandler.Update())
            {
                return;
            }

            if (_paradoxDocumentHandler.Update())
            {
                return;
            }

            if (_storyIntroHandler.Update())
            {
                return;
            }

            if (_cinematicHandler.Update())
            {
                return;
            }

            if (_settingsHandler.Update())
            {
                return;
            }

            if (_giveHandler.Update())
            {
                return;
            }

            if (_cardPlayerHandler.Update())
            {
                return;
            }

            if (_finishRunHandler.Update())
            {
                return;
            }

            if (_corruptionHandler.Update())
            {
                return;
            }

            if (_perkTreeHandler.Update())
            {
                return;
            }

            if (_conflictHandler.Update())
            {
                return;
            }

            _mainMenuHandler.Update();
            if (_lobbyHandler.Update())
            {
                return;
            }

            if (_preRunOptionsHandler.Update())
            {
                return;
            }

            _heroSelectionHandler.Update();
            bool challengeSelectionActive = _challengeSelectionHandler.Update();
            if (challengeSelectionActive)
            {
                return;
            }

            bool eventActive = _eventHandler.Update();
            if (eventActive)
            {
                return;
            }

            bool cardCraftActive = _cardCraftHandler.Update();
            if (cardCraftActive)
            {
                return;
            }

            bool townUpgradeActive = _townUpgradeHandler.Update();
            if (townUpgradeActive)
            {
                return;
            }

            bool townActive = _townHandler.Update();
            if (townActive)
            {
                return;
            }

            bool rewardsActive = _rewardsHandler.Update();
            if (rewardsActive)
            {
                return;
            }

            bool lootActive = _lootHandler.Update();
            if (lootActive)
            {
                return;
            }

            _mapHandler.Update();
            _combatHandler.Update();
        }

        private static void TrackActivationKeyForSpeechCleanup()
        {
            if (Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) ||
                Input.GetKeyDown(KeyCode.Space))
            {
                ScreenReader.SuppressDuplicateActivationSpeech();
            }
        }

        private static void ToggleDebugModeHotkey()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift || !Input.GetKeyDown(KeyCode.F9))
            {
                return;
            }

            DebugMode = !DebugMode;
            ScreenReader.Say(Loc.Get(DebugMode ? "debug_enabled" : "debug_disabled"));
            if (DebugMode)
            {
                DebugLogger.LogMemory("Debug enabled");
            }
        }

        private void LogMemoryDiagnosticsIfDue()
        {
            if (!DebugMode || Time.unscaledTime - _lastMemoryLogTime < 30f)
            {
                return;
            }

            _lastMemoryLogTime = Time.unscaledTime;
            DebugLogger.LogMemory("Periodic");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }

            ScreenReader.Shutdown();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            DebugLogger.LogState("Scene loaded: " + scene.name);
        }

        private void AnnounceStartupOnce()
        {
            if (_startupAnnounced)
            {
                return;
            }

            _startupTimer += Time.unscaledDeltaTime;
            if (_startupTimer < 1.5f)
            {
                return;
            }

            _startupAnnounced = true;
            ScreenReader.Say(Loc.Get("mod_loaded"));
            _updateCheckHandler.Begin();
        }

        private void ApplyPatches()
        {
            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
                Logger.LogInfo("Harmony patches applied.");
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to apply Harmony patches: " + ex.Message);
                ScreenReader.Say(Loc.Get("patches_failed"));
            }
        }

        /// <summary>
        /// Runs a coroutine from handler classes that are not Unity components.
        /// </summary>
        public Coroutine RunHandlerCoroutine(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }
    }
}
