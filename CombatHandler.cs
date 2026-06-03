using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides screen-reader buffers for the combat screen.
    /// </summary>
    public sealed class CombatHandler
    {
        private enum CombatZone
        {
            Cards,
            Enemies,
            Party
        }

        private enum EnemyBufferFocus
        {
            Details,
            RevealedCards,
            Events
        }

        private sealed class BufferItem
        {
            public string Summary;
            public CardItem Card;
            public Character Character;
            public Transform Target;
            public bool CardDetailsPending;
            public string ActionSelectionLine;
            public readonly List<string> Lines = new List<string>();
        }

        private sealed class CharacterSnapshot
        {
            public string Name;
            public int Hp;
            public int MaxHp;
            public bool Alive;
            public bool IsHero;
            public readonly Dictionary<string, int> Effects = new Dictionary<string, int>();
        }

        private static readonly FieldInfo CardItemTableField = AccessTools.Field(typeof(MatchManager), "cardItemTable");
        private static readonly FieldInfo AddcardSelectorField = AccessTools.Field(typeof(MatchManager), "addcardSelector");
        private static readonly FieldInfo CharOrderField = AccessTools.Field(typeof(MatchManager), "CharOrder");
        private static readonly FieldInfo EnergySelectorMaxEnergyField = AccessTools.Field(typeof(UIEnergySelector), "maxEnergy");
        private static readonly FieldInfo EnergySelectorMaxAssignedField = AccessTools.Field(typeof(UIEnergySelector), "maxEnergyToBeAssigned");
        private static readonly FieldInfo DiscardSelectorNonLimitedField = AccessTools.Field(typeof(UIDiscardSelector), "nonLimitedNumCards");

        private sealed class TurnOrderItem
        {
            public string Name;
            public int Speed;
            public float Order;
            public bool Current;
            public int TieIndex;
        }

        private readonly List<BufferItem> _cards = new List<BufferItem>();
        private readonly List<BufferItem> _enemies = new List<BufferItem>();
        private readonly List<BufferItem> _party = new List<BufferItem>();
        private readonly List<BufferItem> _targets = new List<BufferItem>();
        private readonly List<BufferItem> _actionCards = new List<BufferItem>();
        private readonly Dictionary<string, CharacterSnapshot> _snapshots = new Dictionary<string, CharacterSnapshot>();

        private CombatZone _zone;
        private int _cardIndex;
        private int _enemyIndex;
        private int _partyIndex;
        private int _targetIndex;
        private int _actionCardIndex;
        private int _lineIndex;
        private EnemyBufferFocus _enemyBufferFocus;
        private bool _combatAnnounced;
        private bool _targetModeAnnounced;
        private bool _actionModeAnnounced;
        private bool _energyModeAnnounced;
        private bool _initialFocusPending;
        private float _lastRefreshTime;
        private int _lastRound;
        private string _lastTurnKey;
        private string _lastEnergySelectionText;
        private string _actionCardsSignature;

        /// <summary>
        /// Updates combat buffer navigation and state-change announcements.
        /// </summary>
        public void Update()
        {
            MatchManager match = MatchManager.Instance;
            if (match == null)
            {
                Reset();
                return;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return;
            }

            AccessStateManager.SetState(AccessState.Combat);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                RefreshBuffers(match);
                RefreshActionCards(match);
                RefreshTargets(match);
                AnnounceCombatOnce(match);
                AnnounceRoundIfChanged(match);
                AnnounceTurnIfChanged(match);
                AnnounceInitialFocusIfReady();
                AnnounceStateChanges(match);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys();
        }

        private void Reset()
        {
            _combatAnnounced = false;
            _lastRound = -1;
            _lastTurnKey = null;
            _lineIndex = 0;
            _enemyBufferFocus = EnemyBufferFocus.Details;
            _cardIndex = 0;
            _enemyIndex = 0;
            _partyIndex = 0;
            _targetIndex = 0;
            _actionCardIndex = 0;
            _targetModeAnnounced = false;
            _actionModeAnnounced = false;
            _energyModeAnnounced = false;
            _initialFocusPending = false;
            _lastEnergySelectionText = null;
            _actionCardsSignature = null;
            _cards.Clear();
            _enemies.Clear();
            _party.Clear();
            _targets.Clear();
            _actionCards.Clear();
            _snapshots.Clear();
        }

        private void ProcessKeys(MatchManager match = null)
        {
            match = match ?? MatchManager.Instance;
            if (TryAnnounceCharacterHpHotkey(match))
            {
                return;
            }

            if (TryAnnounceFocusedEffectsHotkey(match))
            {
                return;
            }

            if (TryAnnounceEnemyIntentsHotkey(match))
            {
                return;
            }

            if (TryAnnounceRoundHotkey(match))
            {
                return;
            }

            if (TryAnnounceEnergyHotkey(match))
            {
                return;
            }

            if (TryAnnounceTurnOrderHotkey(match))
            {
                return;
            }

            if (IsEnergySelectionMode(match))
            {
                ProcessEnergySelectionKeys(match);
                return;
            }

            if (IsActionSelectionMode(match))
            {
                ProcessActionSelectionKeys(match);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                EndTurnFromKeyboard(match);
                return;
            }

            if (IsTargetMode(match))
            {
                ProcessTargetKeys(match);
                return;
            }

            bool ctrl = IsControlPressed();
            if (ctrl && (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow)))
            {
                MoveControlBuffer(UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) ? 1 : -1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveControlLine(1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveControlLine(-1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpControlLine(false);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpControlLine(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpItem(false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveZone(1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveZone(-1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateFocusedCard();
            }
        }

        private void ProcessEnergySelectionKeys(MatchManager match)
        {
            if (!_energyModeAnnounced)
            {
                _energyModeAnnounced = true;
                _targetModeAnnounced = false;
                _actionModeAnnounced = false;
                AnnounceEnergySelection(match, includeInstructions: true);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                match.EnergySelector.AssignEnergyLess();
                AnnounceEnergySelection(match, includeInstructions: false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                match.EnergySelector.AssignEnergyMore();
                AnnounceEnergySelection(match, includeInstructions: false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                string energy = ReadAssignedEnergy(match);
                ScreenReader.Say(Loc.Get("combat_energy_assigned", energy));
                match.EnergySelector.AssignEnergyAction();
            }
        }

        private void ProcessTargetKeys(MatchManager match)
        {
            if (!_targetModeAnnounced)
            {
                RefreshTargets(match);
                _targetModeAnnounced = true;
                _energyModeAnnounced = false;
                ScreenReader.Say(Loc.Get("combat_target_mode"));
                AnnounceFocusedTarget();
            }

            if (TryAnnounceEnergyHotkey(match))
            {
                return;
            }

            bool ctrl = IsControlPressed();
            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveTargetLine(1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveTargetLine(-1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpTargetLine(false);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpTargetLine(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpTarget(false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpTarget(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveTarget(-1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveTarget(1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ConfirmTarget(match);
            }
        }

        private void ProcessActionSelectionKeys(MatchManager match)
        {
            if (!_actionModeAnnounced)
            {
                RefreshActionCards(match);
                _actionModeAnnounced = true;
                _targetModeAnnounced = false;
                _energyModeAnnounced = false;
                ScreenReader.Say(GetActionSelectionTitle(match));
                AnnounceFocusedActionCard();
            }

            if (TryAnnounceEnergyHotkey(match))
            {
                return;
            }

            bool ctrl = IsControlPressed();
            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveActionLine(1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveActionLine(-1);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpActionLine(false);
                return;
            }

            if (ctrl && UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpActionLine(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Home))
            {
                JumpActionCard(false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.End))
            {
                JumpActionCard(true);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveActionCard(-1);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveActionCard(1);
                return;
            }

            if (ctrl && (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                ConfirmActionSelection(match);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ToggleActionCard(match);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                ConfirmActionSelection(match);
            }
        }

        private void MoveZone(int delta)
        {
            int zone = (int)_zone + delta;
            if (zone < 0)
            {
                zone = 0;
            }
            else if (zone > 2)
            {
                zone = 2;
            }

            if (zone == (int)_zone)
            {
                return;
            }

            _zone = (CombatZone)zone;
            _lineIndex = 0;
            SetEnemyBufferFocus(EnemyBufferFocus.Details, false);
            AnnounceFocusedItem();
        }

        private void MoveItem(int delta)
        {
            List<BufferItem> items = CurrentItems();
            if (items.Count == 0)
            {
                AnnounceEmptyZone();
                return;
            }

            int index = CurrentIndex() + delta;
            if (index < 0)
            {
                index = 0;
            }
            else if (index >= items.Count)
            {
                index = items.Count - 1;
            }

            if (index == CurrentIndex() && items.Count > 1)
            {
                return;
            }

            SetCurrentIndex(index);
            _lineIndex = 0;
            SetEnemyBufferFocus(EnemyBufferFocus.Details, false);
            AnnounceFocusedItem();
        }

        private void JumpItem(bool end)
        {
            List<BufferItem> items = CurrentItems();
            if (items.Count == 0)
            {
                AnnounceEmptyZone();
                return;
            }

            int index = CurrentIndex();
            if (!NavigationBounds.TryJump(ref index, end, items.Count))
            {
                return;
            }

            SetCurrentIndex(index);
            _lineIndex = 0;
            SetEnemyBufferFocus(EnemyBufferFocus.Details, false);
            AnnounceFocusedItem();
        }

        private void MoveControlBuffer(int delta)
        {
            if (_zone != CombatZone.Enemies)
            {
                if (GameEventBuffer.IsFocused)
                {
                    GameEventBuffer.LeaveFocus(true);
                }
                else
                {
                    GameEventBuffer.FocusLatest();
                }

                return;
            }

            int next = ((int)_enemyBufferFocus + delta + 3) % 3;
            SetEnemyBufferFocus((EnemyBufferFocus)next, true);
        }

        private void SetEnemyBufferFocus(EnemyBufferFocus focus, bool announce)
        {
            if (focus != EnemyBufferFocus.Events)
            {
                GameEventBuffer.LeaveFocus(false);
            }

            _enemyBufferFocus = focus;
            _lineIndex = 0;
            if (!announce)
            {
                return;
            }

            if (_enemyBufferFocus == EnemyBufferFocus.Details)
            {
                ScreenReader.Say(Loc.Get("combat_enemy_info_buffer"));
                AnnounceFocusedItem();
            }
            else if (_enemyBufferFocus == EnemyBufferFocus.RevealedCards)
            {
                AnnounceRevealedCardLine();
            }
            else
            {
                GameEventBuffer.FocusLatest();
            }
        }

        private void MoveControlLine(int delta)
        {
            if (_zone == CombatZone.Enemies && _enemyBufferFocus == EnemyBufferFocus.RevealedCards)
            {
                MoveRevealedCardLine(delta);
                return;
            }

            if (_zone == CombatZone.Enemies && _enemyBufferFocus == EnemyBufferFocus.Events)
            {
                GameEventBuffer.MoveFocused(delta > 0 ? -1 : 1);
                return;
            }

            MoveLine(delta);
        }

        private void JumpControlLine(bool end)
        {
            if (_zone == CombatZone.Enemies && _enemyBufferFocus == EnemyBufferFocus.RevealedCards)
            {
                JumpRevealedCardLine(end);
                return;
            }

            if (_zone == CombatZone.Enemies && _enemyBufferFocus == EnemyBufferFocus.Events)
            {
                GameEventBuffer.JumpFocused(end);
                return;
            }

            JumpLine(end);
        }

        private void MoveLine(int delta)
        {
            BufferItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                AnnounceEmptyZone();
                return;
            }

            int previousLineIndex = _lineIndex;
            _lineIndex += delta;
            if (_lineIndex < 0)
            {
                _lineIndex = 0;
            }
            else if (_lineIndex >= item.Lines.Count)
            {
                _lineIndex = item.Lines.Count - 1;
            }

            if (_lineIndex == previousLineIndex && item.Lines.Count > 1)
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            BufferItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                AnnounceEmptyZone();
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void AnnounceCombatOnce(MatchManager match)
        {
            if (_combatAnnounced)
            {
                return;
            }

            _combatAnnounced = true;
            string message = Loc.Get("combat_loaded");
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            AnnounceRoundIfChanged(match, true);
            AnnounceTurnIfChanged(match, true);
            if (CurrentItem() != null)
            {
                AnnounceFocusedItem(true);
            }
            else
            {
                _initialFocusPending = true;
            }
        }

        private void AnnounceRoundIfChanged(MatchManager match, bool force = false)
        {
            int round = match.GameRound();
            if (round <= 0)
            {
                return;
            }

            if (!force && round == _lastRound)
            {
                return;
            }

            _lastRound = round;
            string message = ReadRoundText(match, round);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }

        private static string ReadRoundText(MatchManager match, int round)
        {
            string visibleText = match.roundTM != null ? Clean(match.roundTM.text) : string.Empty;
            if (!string.IsNullOrWhiteSpace(visibleText))
            {
                return visibleText;
            }

            string gameText = GameText.Get("roundNumber");
            if (!string.IsNullOrWhiteSpace(gameText))
            {
                try
                {
                    return Clean(string.Format(gameText, round));
                }
                catch (FormatException ex)
                {
                    DebugLogger.LogState("Could not format round text: " + ex.Message);
                }
            }

            return Loc.Get("combat_round", round);
        }

        private void AnnounceTurnIfChanged(MatchManager match, bool force = false)
        {
            int heroActive = match.GetHeroActive();
            int npcActive = match.GetNPCActive();
            string turnKey = GetTurnAnnouncementKey(match, heroActive, npcActive);
            if (string.IsNullOrWhiteSpace(turnKey))
            {
                return;
            }

            string key = turnKey;
            if (!force && key == _lastTurnKey)
            {
                return;
            }

            _lastTurnKey = key;
            Hero[] heroes = match.GetTeamHero();
            NPC[] npcs = match.GetTeamNPC();
            if (heroActive >= 0 && heroes != null && heroActive < heroes.Length && heroes[heroActive] != null)
            {
                string message = Loc.Get("combat_turn_hero", Clean(heroes[heroActive].SourceName));
                GameEventBuffer.Add(message);
                ScreenReader.SayQueued(message);
            }
            else if (npcActive >= 0 && npcs != null && npcActive < npcs.Length && npcs[npcActive] != null)
            {
                string message = Loc.Get("combat_turn_enemy", Clean(npcs[npcActive].SourceName));
                GameEventBuffer.Add(message);
                ScreenReader.SayQueued(message);
            }
            else if (force)
            {
                string message = Loc.Get("combat_turn_status", Clean(match.GameStatus));
                GameEventBuffer.Add(message);
                ScreenReader.SayQueued(message);
            }
        }

        private void AnnounceInitialFocusIfReady()
        {
            if (!_initialFocusPending || CurrentItem() == null)
            {
                return;
            }

            _initialFocusPending = false;
            AnnounceFocusedItem(true);
        }

        private static string GetTurnAnnouncementKey(MatchManager match, int heroActive, int npcActive)
        {
            Hero[] heroes = match.GetTeamHero();
            if (heroActive >= 0 && heroes != null && heroActive < heroes.Length && heroes[heroActive] != null)
            {
                return "hero:" + heroActive;
            }

            NPC[] npcs = match.GetTeamNPC();
            if (npcActive >= 0 && npcs != null && npcActive < npcs.Length && npcs[npcActive] != null)
            {
                return "npc:" + npcActive;
            }

            return null;
        }

        private void RefreshBuffers(MatchManager match)
        {
            _cards.Clear();
            _enemies.Clear();
            _party.Clear();

            AddCards(match);
            AddCharacters(match.GetTeamNPC(), _enemies);
            AddCharacters(match.GetTeamHero(), _party);
            ClampIndexes();
        }

        private void RefreshTargets(MatchManager match)
        {
            _targets.Clear();
            if (!IsTargetMode(match) || match.CardActive == null)
            {
                _targetModeAnnounced = false;
                _targetIndex = 0;
                return;
            }

            AddTargetCharacters(match, match.GetTeamHero());
            AddTargetCharacters(match, match.GetTeamNPC());
            _targetIndex = ClampIndex(_targetIndex, _targets.Count);
        }

        private void RefreshActionCards(MatchManager match)
        {
            if (!IsActionSelectionMode(match))
            {
                _actionCards.Clear();
                _actionModeAnnounced = false;
                _actionCardIndex = 0;
                _actionCardsSignature = null;
                return;
            }

            List<CardItem> cards = GetActionCardItems(match);
            if (cards.Count == 0 && _actionCards.Count > 0)
            {
                for (int i = 0; i < _actionCards.Count; i++)
                {
                    BufferItem cached = _actionCards[i];
                    if (cached != null)
                    {
                        AddActionCardIfSelectable(cards, cached.Card, match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow, false);
                    }
                }
            }

            cards.Sort((left, right) => left.transform.position.x.CompareTo(right.transform.position.x));
            List<CardItem> selectableCards = new List<CardItem>();
            System.Text.StringBuilder signature = new System.Text.StringBuilder(cards.Count * 12);
            for (int i = 0; i < cards.Count; i++)
            {
                CardItem card = cards[i];
                if (card == null || card.CardData == null)
                {
                    continue;
                }

                bool selectable = match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow ? card.cardfordiscard : card.cardforaddcard;
                if (!selectable)
                {
                    continue;
                }

                bool selected = match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow ? card.cardselectedfordiscard : card.cardselectedforaddcard;
                selectableCards.Add(card);
                signature.Append(card.GetInstanceID()).Append(selected ? ":1;" : ":0;");
            }

            string newSignature = signature.ToString();
            if (_actionCardsSignature == newSignature)
            {
                _actionCardIndex = ClampIndex(_actionCardIndex, _actionCards.Count);
                return;
            }

            _actionCardsSignature = newSignature;
            _actionCards.Clear();
            for (int i = 0; i < selectableCards.Count; i++)
            {
                CardItem card = selectableCards[i];
                bool selected = match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow ? card.cardselectedfordiscard : card.cardselectedforaddcard;
                _actionCards.Add(BuildActionCardItem(card, selected));
            }

            _actionCardIndex = ClampIndex(_actionCardIndex, _actionCards.Count);
        }

        private void AddTargetCharacters(MatchManager match, Character[] characters)
        {
            if (characters == null)
            {
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || !character.Alive)
                {
                    continue;
                }

                Transform target = GetCharacterTargetTransform(character);
                if (target != null && match.CheckTarget(target, match.CardActive))
                {
                    BufferItem item = BuildCharacterItem(character, 0, 0, includeCardPreview: true);
                    item.Target = target;
                    _targets.Add(item);
                }
            }
        }

        private void AddCards(MatchManager match)
        {
            List<CardItem> cards = GetCardItems(match);
            cards.Sort((left, right) => left.transform.position.x.CompareTo(right.transform.position.x));
            for (int i = 0; i < cards.Count; i++)
            {
                CardItem card = cards[i];
                if (card == null || card.CardData == null)
                {
                    continue;
                }

                _cards.Add(BuildCardItem(card, i + 1, cards.Count));
            }
        }

        private static List<CardItem> GetCardItems(MatchManager match)
        {
            List<CardItem> result = new List<CardItem>();
            List<CardItem> table = CardItemTableField != null ? CardItemTableField.GetValue(match) as List<CardItem> : null;
            if (table != null)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    AddCardIfInHand(result, table[i]);
                }
            }

            if (result.Count == 0)
            {
                CardItem[] allCards = UnityEngine.Object.FindObjectsOfType<CardItem>();
                for (int i = 0; i < allCards.Length; i++)
                {
                    AddCardIfInHand(result, allCards[i]);
                }
            }

            return result;
        }

        private static List<CardItem> GetActionCardItems(MatchManager match)
        {
            List<CardItem> result = new List<CardItem>();
            bool discardMode = match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow;

            AddActionCardsFromContainer(result, match.DiscardSelector != null ? match.DiscardSelector.cardContainer : null, discardMode);
            AddActionCardsFromContainer(result, match.DeckCardsWindow != null ? match.DeckCardsWindow.cardContainer : null, discardMode);

            List<CardItem> table = CardItemTableField != null ? CardItemTableField.GetValue(match) as List<CardItem> : null;
            if (table != null)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    AddActionCardIfSelectable(result, table[i], discardMode, true);
                }
            }

            CardItem[] allCards = UnityEngine.Resources.FindObjectsOfTypeAll<CardItem>();
            for (int i = 0; i < allCards.Length; i++)
            {
                AddActionCardIfSelectable(result, allCards[i], discardMode, true);
            }

            return result;
        }

        private static void AddCardIfInHand(List<CardItem> result, CardItem card)
        {
            if (card == null || card.CardData == null || !card.gameObject.activeInHierarchy || card.transform.parent == null)
            {
                return;
            }

            if (card.transform.parent.gameObject.name == "Hand" && !result.Contains(card))
            {
                result.Add(card);
            }
        }

        private static void AddActionCardsFromContainer(List<CardItem> result, Transform container, bool discardMode)
        {
            if (container == null)
            {
                return;
            }

            foreach (Transform child in container)
            {
                if (child == null)
                {
                    continue;
                }

                AddActionCardIfSelectable(result, child.GetComponent<CardItem>(), discardMode, false);
            }
        }

        private static void AddActionCardIfSelectable(List<CardItem> result, CardItem card, bool discardMode, bool requireActive)
        {
            if (card == null || card.CardData == null || (requireActive && !card.gameObject.activeInHierarchy))
            {
                return;
            }

            bool selectable = discardMode ? card.cardfordiscard : card.cardforaddcard;
            if (selectable && !result.Contains(card))
            {
                result.Add(card);
            }
        }

        private static BufferItem BuildCardItem(CardItem card, int position, int total)
        {
            CardData data = card.CardData;
            BufferItem item = new BufferItem();
            item.Card = card;
            int cost = card.GetEnergyCost();
            item.Lines.AddRange(CardSpeech.BuildCardLines(data, cost));
            item.Summary = CardSpeech.BuildCardFocusSummary(data, cost);
            return item;
        }

        private static BufferItem BuildActionCardItem(CardItem card, bool selected)
        {
            CardData data = card.CardData;
            BufferItem item = new BufferItem();
            item.Card = card;
            item.ActionSelectionLine = selected ? Loc.Get("combat_action_card_selected") : Loc.Get("combat_action_card_not_selected");
            item.Lines.Add(CardSpeech.CardNameWithRarity(data));
            item.Lines.Add(item.ActionSelectionLine);
            item.Summary = CardSpeech.BuildCardFocusSummary(data, card.GetEnergyCost());
            item.CardDetailsPending = true;
            return item;
        }

        private static void EnsureCardDetails(BufferItem item)
        {
            if (item == null || !item.CardDetailsPending || item.Card == null || item.Card.CardData == null)
            {
                return;
            }

            item.Lines.Clear();
            item.Lines.AddRange(CardSpeech.BuildCardLines(item.Card.CardData, item.Card.GetEnergyCost()));
            if (!string.IsNullOrWhiteSpace(item.ActionSelectionLine))
            {
                int index = item.Lines.Count > 0 ? 1 : 0;
                item.Lines.Insert(index, item.ActionSelectionLine);
            }

            item.CardDetailsPending = false;
        }

        private static void AddCharacters(Character[] characters, List<BufferItem> destination)
        {
            if (characters == null)
            {
                return;
            }

            int total = CountAlive(characters);
            int position = 0;
            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || !character.Alive)
                {
                    continue;
                }

                position++;
                destination.Add(BuildCharacterItem(character, position, total));
            }
        }

        private static BufferItem BuildCharacterItem(Character character, int position, int total, bool includeCardPreview = false)
        {
            BufferItem item = new BufferItem();
            item.Character = character;
            string name = Clean(character.SourceName);
            AddLine(item, name);
            AddLine(item, FormatSpeed(character));
            if (includeCardPreview)
            {
                AddTargetPreview(item, character);
            }
            AddLine(item, Loc.Get("combat_character_hp", character.GetHp(), character.GetMaxHP()));
            AddLine(item, GetStartOfTurnDeathHint(character));
            AddCharacterEffects(item, character);
            item.Summary = BuildSummary(item, character);
            return item;
        }

        private static void AddTargetPreview(BufferItem item, Character character)
        {
            CharacterItem characterItem = GetCharacterItem(character);
            if (characterItem == null)
            {
                return;
            }

            string damagePreview = ReadVisibleText(characterItem.dmgPreviewText);
            if (!string.IsNullOrWhiteSpace(damagePreview))
            {
                AddLine(item, Loc.Get("combat_target_preview", damagePreview));
            }

            string effectPreview = ReadPurgeDispelPreview(characterItem);
            if (!string.IsNullOrWhiteSpace(effectPreview))
            {
                AddLine(item, Loc.Get("combat_target_preview", effectPreview));
            }
        }

        private static string ReadPurgeDispelPreview(CharacterItem characterItem)
        {
            if (characterItem.purgedispel == null || !characterItem.purgedispel.gameObject.activeInHierarchy)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddCleanPart(parts, ReadVisibleText(characterItem.purgedispelTitle));
            AddCleanPart(parts, ReadVisibleText(characterItem.purgedispel));
            AddCleanPart(parts, ReadVisibleText(characterItem.purgedispelQuantity));
            return string.Join(" ", parts.ToArray());
        }

        private static string ReadVisibleText(TMPro.TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return string.Empty;
            }

            return Clean(text.text);
        }

        private static void AddCleanPart(List<string> parts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }

        private static string GetStartOfTurnDeathHint(Character character)
        {
            CharacterItem characterItem = GetCharacterItem(character);
            if (characterItem == null || MatchManager.Instance == null || MatchManager.Instance.prePostDamageDictionary == null)
            {
                return null;
            }

            List<string> prePostDamage = characterItem.CalculateDamagePrePostForThisCharacter();
            if (prePostDamage == null || prePostDamage.Count < 3 || prePostDamage[2] != "1")
            {
                return null;
            }

            int startTurnDelta;
            if (!int.TryParse(prePostDamage[0], out startTurnDelta) || startTurnDelta >= 0)
            {
                return null;
            }

            if (character.GetHp() + startTurnDelta > 0)
            {
                return null;
            }

            return Loc.Get("combat_will_die_start_turn");
        }

        private static CharacterItem GetCharacterItem(Character character)
        {
            if (character == null)
            {
                return null;
            }

            if (character.HeroItem != null)
            {
                return character.HeroItem;
            }

            return character.NPCItem;
        }

        private void MoveRevealedCardLine(int delta)
        {
            List<string> lines = CurrentRevealedCardLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_revealed_cards"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void JumpRevealedCardLine(bool end)
        {
            List<string> lines = CurrentRevealedCardLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_revealed_cards"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void AnnounceRevealedCardLine()
        {
            List<string> lines = CurrentRevealedCardLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_revealed_cards"));
                return;
            }

            ScreenReader.Say(Loc.Get("combat_revealed_cards"));
            ScreenReader.SayQueued(lines[_lineIndex]);
        }

        private List<string> CurrentRevealedCardLines()
        {
            BufferItem item = CurrentItem();
            NPC npc = item != null ? item.Character as NPC : null;
            return BuildRevealedNpcCardLines(npc);
        }

        private static List<string> BuildRevealedNpcCardLines(NPC npc)
        {
            List<string> lines = new List<string>();
            if (npc == null || npc.NPCItem == null || npc.NPCItem.cardsCI == null)
            {
                return lines;
            }

            int revealed = 0;
            for (int i = 0; i < npc.NPCItem.cardsCI.Length; i++)
            {
                CardItem card = npc.NPCItem.cardsCI[i];
                if (card == null || card.CardData == null || !card.cardrevealed)
                {
                    continue;
                }

                revealed++;
                BufferItem cardItem = BuildCardItem(card, revealed, npc.NPCItem.cardsCI.Length);
                AddLine(lines, Loc.Get("combat_revealed_card", revealed, cardItem.Summary));
                for (int lineIndex = 0; lineIndex < cardItem.Lines.Count; lineIndex++)
                {
                    AddLine(lines, cardItem.Lines[lineIndex]);
                }
            }

            return lines;
        }

        private static Transform GetCharacterTargetTransform(Character character)
        {
            if (character is NPC)
            {
                return character.NPCItem != null ? character.NPCItem.transform : null;
            }

            return character.HeroItem != null ? character.HeroItem.transform : null;
        }

        private static void AddCharacterEffects(BufferItem item, Character character)
        {
            if (character.AuraList == null || character.AuraList.Count == 0)
            {
                AddLine(item, Loc.Get("combat_no_effects"));
                return;
            }

            for (int i = 0; i < character.AuraList.Count; i++)
            {
                Aura aura = character.AuraList[i];
                if (aura == null || aura.ACData == null || aura.AuraCharges == 0)
                {
                    continue;
                }

                string name = Clean(GameText.AuraCurseName(aura.ACData));
                AddLine(item, Loc.Get("combat_character_effect", name, aura.AuraCharges));
                string description = Clean(GameText.AuraCurseDescription(aura.ACData, aura.AuraCharges, character));
                if (!string.IsNullOrWhiteSpace(description))
                {
                    AddLine(item, Loc.Get("combat_effect_description", name, description));
                }
            }
        }

        private void AnnounceStateChanges(MatchManager match)
        {
            Dictionary<string, CharacterSnapshot> current = new Dictionary<string, CharacterSnapshot>();
            SnapshotCharacters(match.GetTeamHero(), current);
            SnapshotCharacters(match.GetTeamNPC(), current);

            foreach (KeyValuePair<string, CharacterSnapshot> pair in current)
            {
                CharacterSnapshot previous;
                if (!_snapshots.TryGetValue(pair.Key, out previous))
                {
                    continue;
                }

                AnnounceSnapshotDelta(previous, pair.Value);
            }

            _snapshots.Clear();
            foreach (KeyValuePair<string, CharacterSnapshot> pair in current)
            {
                _snapshots[pair.Key] = pair.Value;
            }
        }

        private static void SnapshotCharacters(Character[] characters, Dictionary<string, CharacterSnapshot> snapshots)
        {
            if (characters == null)
            {
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null)
                {
                    continue;
                }

                CharacterSnapshot snapshot = new CharacterSnapshot
                {
                    Name = Clean(character.SourceName),
                    Hp = character.GetHp(),
                    MaxHp = character.GetMaxHP(),
                    Alive = character.Alive,
                    IsHero = character.HeroItem != null
                };
                if (character.AuraList != null)
                {
                    for (int j = 0; j < character.AuraList.Count; j++)
                    {
                        Aura aura = character.AuraList[j];
                        if (aura != null && aura.ACData != null && aura.AuraCharges != 0)
                        {
                            snapshot.Effects[Clean(GameText.AuraCurseName(aura.ACData))] = aura.AuraCharges;
                        }
                    }
                }

                snapshots[character.Id + ":" + i] = snapshot;
            }
        }

        private static void AnnounceSnapshotDelta(CharacterSnapshot previous, CharacterSnapshot current)
        {
            if (current.Hp != previous.Hp)
            {
                int amount = Math.Abs(current.Hp - previous.Hp);
                string message = current.Hp < previous.Hp
                    ? Loc.Get("combat_hp_damage", current.Name, amount)
                    : Loc.Get("combat_hp_heal", current.Name, amount);
                GameEventBuffer.Add(message);
                ScreenReader.SayQueued(message);
            }

            if (previous.Alive && !current.Alive)
            {
                string message = Loc.Get(current.IsHero ? "combat_hero_died" : "combat_enemy_died", current.Name);
                GameEventBuffer.Add(message);
                ScreenReader.SayQueued(message);
            }

            foreach (KeyValuePair<string, int> effect in current.Effects)
            {
                int previousCharges;
                if (!previous.Effects.TryGetValue(effect.Key, out previousCharges))
                {
                    string message = Loc.Get("combat_effect_delta", current.Name, effect.Key, FormatSigned(effect.Value));
                    GameEventBuffer.Add(message);
                    ScreenReader.SayQueued(message);
                }
                else if (effect.Value != previousCharges)
                {
                    string message = Loc.Get("combat_effect_delta", current.Name, effect.Key, FormatSigned(effect.Value - previousCharges));
                    GameEventBuffer.Add(message);
                    ScreenReader.SayQueued(message);
                }
            }

            foreach (KeyValuePair<string, int> effect in previous.Effects)
            {
                if (!current.Effects.ContainsKey(effect.Key))
                {
                    string message = Loc.Get("combat_effect_delta", current.Name, effect.Key, FormatSigned(-effect.Value));
                    GameEventBuffer.Add(message);
                    ScreenReader.SayQueued(message);
                }
            }
        }

        private static string FormatSigned(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            BufferItem item = CurrentItem();
            if (item == null)
            {
                AnnounceEmptyZone();
                return;
            }

            string summary = BuildFocusedItemSummary(item);
            if (queued)
            {
                ScreenReader.SayQueued(summary);
            }
            else
            {
                ScreenReader.Say(summary);
            }
        }

        private string BuildFocusedItemSummary(BufferItem item)
        {
            if (_zone != CombatZone.Cards || item.Card == null || item.Card.CardData == null)
            {
                return item.Summary;
            }

            string preview = BuildImmediateCardPreview(item.Card);
            if (string.IsNullOrWhiteSpace(preview))
            {
                return item.Summary;
            }

            return item.Summary + " " + preview;
        }

        private static string BuildImmediateCardPreview(CardItem card)
        {
            MatchManager match = MatchManager.Instance;
            if (match == null || card == null || card.CardData == null || !match.CanInstaCast(card.CardData))
            {
                return string.Empty;
            }

            match.SetCardActive(card.CardData);
            match.SetDamagePreview(theCasterIsHero: true, card.CardData, card.tablePosition);
            List<string> previews = new List<string>();
            AddImmediateCardPreviewForCharacters(previews, match.GetTeamHero(), card.CardData);
            AddImmediateCardPreviewForCharacters(previews, match.GetTeamNPC(), card.CardData);
            if (previews.Count == 0)
            {
                return string.Empty;
            }

            return Loc.Get("combat_immediate_card_preview", string.Join(" ", previews.ToArray()));
        }

        private static void AddImmediateCardPreviewForCharacters(List<string> previews, Character[] characters, CardData cardData)
        {
            if (characters == null)
            {
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || !character.Alive)
                {
                    continue;
                }

                Transform target = GetCharacterTargetTransform(character);
                if (target == null || MatchManager.Instance == null || !MatchManager.Instance.CheckTarget(target, cardData))
                {
                    continue;
                }

                string preview = BuildTargetPreviewText(character);
                if (!string.IsNullOrWhiteSpace(preview))
                {
                    previews.Add(Loc.Get("combat_immediate_card_preview_target", Clean(character.SourceName), preview));
                }
            }
        }

        private static string BuildTargetPreviewText(Character character)
        {
            CharacterItem characterItem = GetCharacterItem(character);
            if (characterItem == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddCleanPart(parts, ReadVisibleText(characterItem.dmgPreviewText));
            AddCleanPart(parts, ReadPurgeDispelPreview(characterItem));
            return string.Join(" ", parts.ToArray());
        }

        private void AnnounceEmptyZone()
        {
            if (_zone == CombatZone.Cards)
            {
                ScreenReader.Say(Loc.Get("combat_cards_empty"));
            }
            else if (_zone == CombatZone.Enemies)
            {
                ScreenReader.Say(Loc.Get("combat_enemies_empty"));
            }
            else
            {
                ScreenReader.Say(Loc.Get("combat_party_empty"));
            }
        }

        private void ActivateFocusedCard()
        {
            if (_zone != CombatZone.Cards)
            {
                return;
            }

            BufferItem item = CurrentItem();
            if (item == null || item.Card == null)
            {
                AnnounceEmptyZone();
                return;
            }

            string message = Loc.Get("activated", Clean(item.Card.CardData != null ? item.Card.CardData.CardName : "card"));
            GameEventBuffer.Add(message);
            item.Card.OnMouseUpController();
        }

        private static void EndTurnFromKeyboard(MatchManager match)
        {
            if (match == null || IsTargetMode(match) || IsActionSelectionMode(match) || GameManager.Instance == null || GameManager.Instance.IsTutorialActive())
            {
                return;
            }

            string message = Loc.Get("combat_end_turn");
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            match.KeyboardSpace();
        }

        private void MoveActionCard(int delta)
        {
            if (_actionCards.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _actionCardIndex, delta, _actionCards.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedActionCard();
        }

        private void JumpActionCard(bool end)
        {
            if (_actionCards.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _actionCardIndex, end, _actionCards.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedActionCard();
        }

        private void MoveActionLine(int delta)
        {
            BufferItem item = CurrentActionCard();
            EnsureCardDetails(item);
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void JumpActionLine(bool end)
        {
            BufferItem item = CurrentActionCard();
            EnsureCardDetails(item);
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void ToggleActionCard(MatchManager match)
        {
            BufferItem item = CurrentActionCard();
            if (item == null || item.Card == null)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            if (match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow)
            {
                match.SelectCardToDiscard(item.Card);
            }
            else if (match.WaitingForAddcardAssignment)
            {
                match.SelectCardToAddcard(item.Card);
            }

            bool selected = match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow ? item.Card.cardselectedfordiscard : item.Card.cardselectedforaddcard;
            string message = Loc.Get(selected ? "combat_action_card_marked" : "combat_action_card_unmarked", Clean(item.Card.CardData != null ? item.Card.CardData.CardName : ""));
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            RefreshActionCards(match);
        }

        private void ConfirmActionSelection(MatchManager match)
        {
            if (match.WaitingForLookDiscardWindow)
            {
                if (match.DeckCardsWindow != null)
                {
                    match.DeckCardsWindow.Action();
                    string message = Loc.Get("combat_action_confirmed");
                    GameEventBuffer.Add(message);
                    ScreenReader.Say(message);
                    _actionModeAnnounced = false;
                }

                return;
            }

            if (match.WaitingForDiscardAssignment)
            {
                UIDiscardSelector selector = match.DiscardSelector;
                if (selector == null)
                {
                    return;
                }

                if (!IsDiscardSelectorConfirmable(match, selector))
                {
                    int left = match.CardsLeftForDiscard();
                    ScreenReader.Say(Loc.Get("combat_action_cards_left", left));
                    return;
                }

                selector.Action();
                string message = Loc.Get("combat_action_confirmed");
                GameEventBuffer.Add(message);
                ScreenReader.Say(message);
                _actionModeAnnounced = false;

                return;
            }

            if (match.WaitingForAddcardAssignment)
            {
                int left = match.CardsLeftForAddcard();
                if (left > 0)
                {
                    ScreenReader.Say(Loc.Get("combat_action_cards_left", left));
                    return;
                }

                UIAddcardSelector selector = GetAddcardSelector(match);
                if (selector != null)
                {
                    selector.Action();
                    string message = Loc.Get("combat_action_confirmed");
                    GameEventBuffer.Add(message);
                    ScreenReader.Say(message);
                    _actionModeAnnounced = false;
                }
            }
        }

        private void AnnounceFocusedActionCard()
        {
            BufferItem item = CurrentActionCard();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("combat_action_no_cards"));
                return;
            }

            ScreenReader.Say(item.Summary);
        }

        private BufferItem CurrentActionCard()
        {
            if (_actionCards.Count == 0)
            {
                return null;
            }

            _actionCardIndex = ClampIndex(_actionCardIndex, _actionCards.Count);
            return _actionCards[_actionCardIndex];
        }

        private void MoveTarget(int delta)
        {
            if (_targets.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            int nextIndex = _targetIndex + delta;
            if (nextIndex < 0)
            {
                nextIndex = 0;
            }
            else if (nextIndex >= _targets.Count)
            {
                nextIndex = _targets.Count - 1;
            }

            if (nextIndex == _targetIndex && _targets.Count > 1)
            {
                return;
            }

            _targetIndex = nextIndex;
            _lineIndex = 0;
            AnnounceFocusedTarget();
        }

        private void JumpTarget(bool end)
        {
            if (_targets.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _targetIndex, end, _targets.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedTarget();
        }

        private void MoveTargetLine(int delta)
        {
            BufferItem item = CurrentTarget();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            int previousLineIndex = _lineIndex;
            _lineIndex += delta;
            if (_lineIndex < 0)
            {
                _lineIndex = 0;
            }
            else if (_lineIndex >= item.Lines.Count)
            {
                _lineIndex = item.Lines.Count - 1;
            }

            if (_lineIndex == previousLineIndex && item.Lines.Count > 1)
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void JumpTargetLine(bool end)
        {
            BufferItem item = CurrentTarget();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void AnnounceFocusedTarget()
        {
            BufferItem item = CurrentTarget();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            ScreenReader.Say(item.Summary);
        }

        private void ConfirmTarget(MatchManager match)
        {
            BufferItem item = CurrentTarget();
            if (item == null || item.Target == null || match.CardItemActive == null)
            {
                ScreenReader.Say(Loc.Get("combat_no_targets"));
                return;
            }

            match.SetTarget(item.Target);
            string message = Loc.Get("combat_target_selected", item.Lines.Count > 0 ? item.Lines[0] : item.Summary);
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            match.ControllerExecute();
            _targetModeAnnounced = false;
        }

        private BufferItem CurrentTarget()
        {
            if (_targetIndex < 0 || _targetIndex >= _targets.Count)
            {
                return null;
            }

            return _targets[_targetIndex];
        }

        private static bool IsTargetMode(MatchManager match)
        {
            return match != null && match.CardDrag && match.controllerClickedCard && match.CardItemActive != null;
        }

        private static bool IsActionSelectionMode(MatchManager match)
        {
            return match != null && (match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow || match.WaitingForAddcardAssignment);
        }

        private static bool IsEnergySelectionMode(MatchManager match)
        {
            return match != null && match.EnergySelector != null && match.EnergySelector.IsActive();
        }

        private void AnnounceEnergySelection(MatchManager match, bool includeInstructions)
        {
            if (match == null || match.EnergySelector == null)
            {
                ScreenReader.Say(Loc.Get("combat_energy_unavailable"));
                return;
            }

            string instructions = Clean(match.EnergySelector.textInstructions != null ? match.EnergySelector.textInstructions.text : "");
            string energy = ReadAssignedEnergy(match);
            string limit = ReadEnergySelectionLimit(match);
            string value = string.IsNullOrWhiteSpace(limit)
                ? Loc.Get("combat_energy_selector_value", energy)
                : Loc.Get("combat_energy_selector_value_range", energy, limit);
            string message = includeInstructions && !string.IsNullOrWhiteSpace(instructions)
                ? Loc.Get("combat_energy_selector", instructions, value)
                : value;
            if (message == _lastEnergySelectionText)
            {
                return;
            }

            _lastEnergySelectionText = message;
            ScreenReader.Say(message);
        }

        private static string ReadAssignedEnergy(MatchManager match)
        {
            if (match == null || match.EnergySelector == null || match.EnergySelector.textAssignEnergy == null)
            {
                return "0";
            }

            string energy = Clean(match.EnergySelector.textAssignEnergy.text);
            return string.IsNullOrWhiteSpace(energy) ? "0" : energy;
        }

        private static string ReadEnergySelectionLimit(MatchManager match)
        {
            if (match == null || match.EnergySelector == null || EnergySelectorMaxEnergyField == null || EnergySelectorMaxAssignedField == null)
            {
                return "";
            }

            int maxEnergy = (int)EnergySelectorMaxEnergyField.GetValue(match.EnergySelector);
            int maxAssigned = (int)EnergySelectorMaxAssignedField.GetValue(match.EnergySelector);
            int limit = Math.Min(maxEnergy, maxAssigned);
            return limit >= 0 ? limit.ToString() : "";
        }

        private static bool TryAnnounceEnergyHotkey(MatchManager match)
        {
            if (!IsControlPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.E))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_energy_unavailable"));
                return true;
            }

            ScreenReader.Say(Loc.Get("combat_energy", match.GetHeroEnergy()));
            return true;
        }

        private bool TryAnnounceCharacterHpHotkey(MatchManager match)
        {
            if (!IsControlPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.H))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_character_unavailable"));
                return true;
            }

            if (IsShiftPressed())
            {
                AnnounceAllHeroHp(match);
                return true;
            }

            Character character = GetFocusedOrActiveCharacter(match);
            if (character == null)
            {
                ScreenReader.Say(Loc.Get("combat_character_unavailable"));
                return true;
            }

            ScreenReader.Say(FormatCharacterHp(character));
            return true;
        }

        private bool TryAnnounceFocusedEffectsHotkey(MatchManager match)
        {
            if (!IsControlPressed() || IsShiftPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.F))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_character_unavailable"));
                return true;
            }

            Character character = GetFocusedOrActiveCharacter(match);
            if (character == null)
            {
                ScreenReader.Say(Loc.Get("combat_character_unavailable"));
                return true;
            }

            ScreenReader.Say(FormatCharacterEffects(character));
            return true;
        }

        private bool TryAnnounceEnemyIntentsHotkey(MatchManager match)
        {
            if (!IsControlPressed() || IsShiftPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.I))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_enemy_intents_none"));
                return true;
            }

            NPC focused = GetFocusedEnemyForIntents(match);
            if (focused != null)
            {
                string focusedEntry = FormatEnemyIntentEntry(focused);
                ScreenReader.Say(string.IsNullOrWhiteSpace(focusedEntry)
                    ? Loc.Get("combat_enemy_intents_none_for", Clean(focused.SourceName))
                    : focusedEntry);
                return true;
            }

            NPC[] npcs = match.GetTeamNPC();
            List<string> entries = new List<string>();
            if (npcs != null)
            {
                for (int i = 0; i < npcs.Length; i++)
                {
                    NPC npc = npcs[i];
                    if (npc == null || !npc.Alive)
                    {
                        continue;
                    }

                    string entry = FormatEnemyIntentEntry(npc);
                    if (!string.IsNullOrWhiteSpace(entry))
                    {
                        entries.Add(entry);
                    }
                }
            }

            ScreenReader.Say(entries.Count == 0
                ? Loc.Get("combat_enemy_intents_none")
                : Loc.Get("combat_enemy_intents", string.Join(" ", entries.ToArray())));
            return true;
        }

        private static bool TryAnnounceRoundHotkey(MatchManager match)
        {
            if (!IsControlPressed() || IsShiftPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_round_unavailable"));
                return true;
            }

            int round = match.GameRound();
            if (round <= 0)
            {
                ScreenReader.Say(Loc.Get("combat_round_unavailable"));
                return true;
            }

            ScreenReader.Say(Loc.Get("combat_round", round));
            return true;
        }

        private static void AnnounceAllHeroHp(MatchManager match)
        {
            Hero[] heroes = match.GetTeamHero();
            if (heroes == null || heroes.Length == 0)
            {
                ScreenReader.Say(Loc.Get("combat_party_hp_unavailable"));
                return;
            }

            List<string> entries = new List<string>();
            for (int i = 0; i < heroes.Length; i++)
            {
                Hero hero = heroes[i];
                if (hero == null || hero.HeroData == null)
                {
                    continue;
                }

                entries.Add(FormatCharacterHp(hero));
            }

            if (entries.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_party_hp_unavailable"));
                return;
            }

            ScreenReader.Say(Loc.Get("combat_party_hp", string.Join(" ", entries.ToArray())));
        }

        private Character GetFocusedOrActiveCharacter(MatchManager match)
        {
            BufferItem item = IsTargetMode(match) ? CurrentTarget() : CurrentItem();
            if (item != null && item.Character != null)
            {
                return item.Character;
            }

            int heroActive = match.GetHeroActive();
            Hero[] heroes = match.GetTeamHero();
            if (heroActive >= 0 && heroes != null && heroActive < heroes.Length && heroes[heroActive] != null)
            {
                return heroes[heroActive];
            }

            int npcActive = match.GetNPCActive();
            NPC[] npcs = match.GetTeamNPC();
            if (npcActive >= 0 && npcs != null && npcActive < npcs.Length && npcs[npcActive] != null)
            {
                return npcs[npcActive];
            }

            return null;
        }

        private static string FormatCharacterHp(Character character)
        {
            return Loc.Get("combat_character_hp_named", Clean(character.SourceName), character.GetHp(), character.GetMaxHP());
        }

        private static string FormatCharacterEffects(Character character)
        {
            string name = Clean(character.SourceName);
            if (character.AuraList == null || character.AuraList.Count == 0)
            {
                return Loc.Get("combat_character_effects_none", name);
            }

            List<string> effects = new List<string>();
            for (int i = 0; i < character.AuraList.Count; i++)
            {
                Aura aura = character.AuraList[i];
                if (aura == null || aura.ACData == null || aura.AuraCharges == 0)
                {
                    continue;
                }

                effects.Add(Loc.Get("combat_character_effect_named", Clean(GameText.AuraCurseName(aura.ACData)), aura.AuraCharges));
            }

            if (effects.Count == 0)
            {
                return Loc.Get("combat_character_effects_none", name);
            }

            return Loc.Get("combat_character_effects", name, string.Join(" ", effects.ToArray()));
        }

        private NPC GetFocusedEnemyForIntents(MatchManager match)
        {
            BufferItem item = IsTargetMode(match) ? CurrentTarget() : CurrentItem();
            return item != null ? item.Character as NPC : null;
        }

        private static string FormatEnemyIntentEntry(NPC npc)
        {
            List<string> names = GetRevealedNpcCardNames(npc);
            if (names.Count == 0)
            {
                return string.Empty;
            }

            return Loc.Get("combat_enemy_intent_entry", Clean(npc.SourceName), string.Join(", ", names.ToArray()));
        }

        private static List<string> GetRevealedNpcCardNames(NPC npc)
        {
            List<string> names = new List<string>();
            if (npc == null || npc.NPCItem == null || npc.NPCItem.cardsCI == null)
            {
                return names;
            }

            for (int i = 0; i < npc.NPCItem.cardsCI.Length; i++)
            {
                CardItem card = npc.NPCItem.cardsCI[i];
                if (card == null || card.CardData == null || !card.cardrevealed)
                {
                    continue;
                }

                string name = Clean(GameText.CardName(card.CardData));
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static bool TryAnnounceTurnOrderHotkey(MatchManager match)
        {
            if (!IsControlPressed() || !UnityEngine.Input.GetKeyDown(KeyCode.T))
            {
                return false;
            }

            if (match == null)
            {
                ScreenReader.Say(Loc.Get("combat_turn_order_unavailable"));
                return true;
            }

            List<TurnOrderItem> order = BuildTurnOrder(match);
            if (order.Count == 0)
            {
                ScreenReader.Say(Loc.Get("combat_turn_order_unavailable"));
                return true;
            }

            List<string> entries = new List<string>();
            for (int i = 0; i < order.Count; i++)
            {
                TurnOrderItem item = order[i];
                string entry = Loc.Get("combat_turn_order_entry", i + 1, item.Name);
                if (item.Current)
                {
                    entry = Loc.Get("combat_turn_order_current", entry);
                }

                entries.Add(entry);
            }

            ScreenReader.Say(Loc.Get("combat_turn_order", string.Join(" ", entries.ToArray())));
            return true;
        }

        private static List<TurnOrderItem> BuildTurnOrder(MatchManager match)
        {
            List<TurnOrderItem> order = BuildTurnOrderFromGame(match);
            if (order.Count > 0)
            {
                return order;
            }

            AddFallbackTurnOrder(order, match.GetTeamHero(), true, match.GetHeroActive());
            AddFallbackTurnOrder(order, match.GetTeamNPC(), false, match.GetNPCActive());
            order.Sort((left, right) =>
            {
                int speed = right.Speed.CompareTo(left.Speed);
                if (speed != 0)
                {
                    return speed;
                }

                return left.TieIndex.CompareTo(right.TieIndex);
            });
            return order;
        }

        private static List<TurnOrderItem> BuildTurnOrderFromGame(MatchManager match)
        {
            List<TurnOrderItem> result = new List<TurnOrderItem>();
            if (CharOrderField == null)
            {
                return result;
            }

            try
            {
                object raw = CharOrderField.GetValue(match);
                IEnumerable<MatchManager.CharacterForOrder> charOrder = raw as IEnumerable<MatchManager.CharacterForOrder>;
                if (charOrder == null)
                {
                    return result;
                }

                foreach (MatchManager.CharacterForOrder entry in charOrder)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    Character character = entry.hero != null ? (Character)entry.hero : entry.npc;
                    if (character == null || !character.Alive)
                    {
                        continue;
                    }

                    TurnOrderItem item = new TurnOrderItem();
                    item.Name = Clean(character.SourceName);
                    item.Speed = entry.speed != null && entry.speed.Length > 0 ? entry.speed[0] : CurrentSpeed(character);
                    item.Order = entry.speedForOrder;
                    item.Current = entry.hero != null
                        ? entry.index == match.GetHeroActive()
                        : entry.index == match.GetNPCActive();
                    item.TieIndex = entry.index;
                    result.Add(item);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CombatHandler failed to read MatchManager.CharOrder: " + ex.Message);
            }

            return result;
        }

        private static void AddFallbackTurnOrder(List<TurnOrderItem> order, Character[] characters, bool heroes, int activeIndex)
        {
            if (characters == null)
            {
                return;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                Character character = characters[i];
                if (character == null || !character.Alive)
                {
                    continue;
                }

                TurnOrderItem item = new TurnOrderItem();
                item.Name = Clean(character.SourceName);
                item.Speed = CurrentSpeed(character);
                item.Current = i == activeIndex;
                item.TieIndex = heroes ? i : i + 10;
                order.Add(item);
            }
        }

        private static string FormatSpeed(Character character)
        {
            if (character == null)
            {
                return Loc.Get("combat_character_speed", 0);
            }

            int[] speed = character.GetSpeed();
            int current = speed != null && speed.Length > 0 ? speed[0] : 0;
            int baseValue = speed != null && speed.Length > 1 ? speed[1] : current;
            int modifier = speed != null && speed.Length > 2 ? speed[2] : current - baseValue;
            if (modifier == 0)
            {
                return Loc.Get("combat_character_speed", current);
            }

            return Loc.Get("combat_character_speed_modified", current, baseValue, modifier);
        }

        private static int CurrentSpeed(Character character)
        {
            if (character == null)
            {
                return 0;
            }

            int[] speed = character.GetSpeed();
            return speed != null && speed.Length > 0 ? speed[0] : 0;
        }

        private static bool IsControlPressed()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
        }

        private static bool IsShiftPressed()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
        }

        private static string GetActionSelectionTitle(MatchManager match)
        {
            if (match == null)
            {
                return Loc.Get("combat_action_select_cards");
            }

            if (match.WaitingForAddcardAssignment)
            {
                return Loc.Get("combat_action_add_cards", match.CardsLeftForAddcard());
            }

            if (match.WaitingForLookDiscardWindow || IsDiscardSelectionUpTo(match))
            {
                int left = match.CardsLeftForDiscard();
                if (left < 0)
                {
                    left = 0;
                }

                return Loc.Get("combat_action_discard_up_to_cards", left);
            }

            return Loc.Get("combat_action_discard_cards", match.CardsLeftForDiscard());
        }

        private static bool IsDiscardSelectorConfirmable(MatchManager match, UIDiscardSelector selector)
        {
            if (selector == null)
            {
                return false;
            }

            if (selector.button != null)
            {
                UnityEngine.UI.Button button = selector.button.GetComponent<UnityEngine.UI.Button>();
                if (button != null)
                {
                    return button.interactable;
                }
            }

            return match != null && (match.CardsLeftForDiscard() <= 0 || IsDiscardSelectionUpTo(match));
        }

        private static bool IsDiscardSelectionUpTo(MatchManager match)
        {
            UIDiscardSelector selector = match != null ? match.DiscardSelector : null;
            if (selector == null || DiscardSelectorNonLimitedField == null)
            {
                return false;
            }

            try
            {
                return (bool)DiscardSelectorNonLimitedField.GetValue(selector);
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CombatHandler failed to read discard selector limit mode: " + ex.Message);
                return false;
            }
        }

        private static UIAddcardSelector GetAddcardSelector(MatchManager match)
        {
            if (match == null || AddcardSelectorField == null)
            {
                return null;
            }

            try
            {
                return AddcardSelectorField.GetValue(match) as UIAddcardSelector;
            }
            catch (Exception ex)
            {
                DebugLogger.LogState("CombatHandler failed to read addcardSelector: " + ex.Message);
                return null;
            }
        }

        private List<BufferItem> CurrentItems()
        {
            if (_zone == CombatZone.Cards)
            {
                return _cards;
            }

            return _zone == CombatZone.Enemies ? _enemies : _party;
        }

        private BufferItem CurrentItem()
        {
            List<BufferItem> items = CurrentItems();
            int index = CurrentIndex();
            if (index < 0 || index >= items.Count)
            {
                return null;
            }

            return items[index];
        }

        private int CurrentIndex()
        {
            if (_zone == CombatZone.Cards)
            {
                return _cardIndex;
            }

            return _zone == CombatZone.Enemies ? _enemyIndex : _partyIndex;
        }

        private void SetCurrentIndex(int index)
        {
            if (_zone == CombatZone.Cards)
            {
                _cardIndex = index;
            }
            else if (_zone == CombatZone.Enemies)
            {
                _enemyIndex = index;
            }
            else
            {
                _partyIndex = index;
            }
        }

        private void ClampIndexes()
        {
            _cardIndex = ClampIndex(_cardIndex, _cards.Count);
            _enemyIndex = ClampIndex(_enemyIndex, _enemies.Count);
            _partyIndex = ClampIndex(_partyIndex, _party.Count);
        }

        private static int ClampIndex(int index, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static int CountAlive(Character[] characters)
        {
            int count = 0;
            if (characters == null)
            {
                return count;
            }

            for (int i = 0; i < characters.Length; i++)
            {
                if (characters[i] != null && characters[i].Alive)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AddLine(BufferItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static void AddLine(List<string> lines, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        private static string BuildSummary(BufferItem item, Character character = null)
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < item.Lines.Count; i++)
            {
                string line = item.Lines[i];
                if (!string.IsNullOrWhiteSpace(line) && line != Loc.Get("combat_no_effects") && !IsCharacterEffectDescriptionLine(line, character))
                {
                    lines.Add(line);
                }
            }

            return string.Join(" ", lines.ToArray());
        }

        private static bool IsCharacterEffectDescriptionLine(string line, Character character)
        {
            if (string.IsNullOrWhiteSpace(line) || character == null || character.AuraList == null)
            {
                return false;
            }

            for (int i = 0; i < character.AuraList.Count; i++)
            {
                Aura aura = character.AuraList[i];
                if (aura == null || aura.ACData == null || aura.AuraCharges == 0)
                {
                    continue;
                }

                string name = Clean(GameText.AuraCurseName(aura.ACData));
                string description = Clean(GameText.AuraCurseDescription(aura.ACData, aura.AuraCharges, character));
                if (!string.IsNullOrWhiteSpace(description) && line == Loc.Get("combat_effect_description", name, description))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
