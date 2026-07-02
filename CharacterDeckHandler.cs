using System.Collections.Generic;
using TMPro;
using UnityEngine;

using Cards;
using Cards.Data;
namespace AccessTheObelisk
{
    /// <summary>
    /// Provides accessible navigation for the game's native deck viewing windows.
    /// </summary>
    public sealed class CharacterDeckHandler
    {
        private sealed class DeckItem
        {
            public string Summary;
            public CardItem Card;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<DeckItem> _items = new List<DeckItem>();
        private int _itemIndex;
        private int _lineIndex;
        private bool _announced;
        private int _lastCount;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates deck-view hotkeys and active deck navigation.
        /// </summary>
        public bool Update()
        {
            if (TryCloseCardDetail())
            {
                return true;
            }

            if (IsCombatActionSelectionMode())
            {
                Reset();
                return false;
            }

            if (TryOpenDeckHotkey())
            {
                return true;
            }

            if (!TryGetActiveDeckWindow(out Transform deckContent, out Transform injuryContent, out string title, out CharacterWindowUI characterWindow, out UIDeckCards combatDeckWindow))
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.CharacterDeck);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(deckContent, injuryContent);
                AnnounceOnce(title);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(characterWindow, combatDeckWindow);
            return true;
        }

        private static bool IsCombatActionSelectionMode()
        {
            MatchManager match = MatchManager.Instance;
            return match != null && (match.WaitingForDiscardAssignment || match.WaitingForLookDiscardWindow || match.WaitingForAddcardAssignment);
        }

        private static bool TryCloseCardDetail()
        {
            if (CardScreenManager.Instance == null || !CardScreenManager.Instance.IsActive() || !TryGetActiveDeckWindow(out _, out _, out _, out _, out _))
            {
                return false;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                CardScreenManager.Instance.ShowCardScreen(_state: false);
                ScreenReader.Say(Loc.Get("deck_card_detail_closed"));
            }

            return true;
        }

        private bool TryOpenDeckHotkey()
        {
            bool shift = ModInput.GetKey(KeyCode.LeftShift) || ModInput.GetKey(KeyCode.RightShift);
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            bool vanishPile = ctrl && shift;
            if ((!vanishPile && ctrl) || !ModInput.GetKeyDown(KeyCode.F) || TextInputFocusHelper.IsTextInputFocused())
            {
                return false;
            }

            if (CardScreenManager.Instance != null && CardScreenManager.Instance.IsActive())
            {
                return true;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                ScreenReader.Say(Loc.Get("deck_unavailable"));
                return true;
            }

            if (MatchManager.Instance != null)
            {
                if (vanishPile)
                {
                    if (TryOpenCombatVanishPile())
                    {
                        return true;
                    }
                }
                else if (shift)
                {
                    if (TryOpenCombatDiscardPile())
                    {
                        return true;
                    }
                }
                else if (TryOpenCombatDrawPile())
                {
                    return true;
                }

                ScreenReader.Say(Loc.Get("deck_unavailable"));
                return true;
            }

            if (shift)
            {
                ScreenReader.Say(Loc.Get("deck_discard_unavailable"));
                return true;
            }

            if (TryGetActiveDeckWindow(out _, out _, out _, out _, out _))
            {
                ScreenReader.Say(Loc.Get("deck_already_open"));
                return true;
            }

            if (TryOpenNonCombatDeck())
            {
                return true;
            }

            ScreenReader.Say(Loc.Get("deck_unavailable"));
            return true;
        }

        private static bool TryOpenCombatDrawPile()
        {
            return TryOpenCombatPile("combatdeck", "deck_draw_opened");
        }

        private static bool TryOpenCombatDiscardPile()
        {
            return TryOpenCombatPile("combatdiscard", "deck_discard_opened");
        }

        private static bool TryOpenCombatVanishPile()
        {
            return TryOpenCombatPile("combatvanish", "deck_vanish_opened");
        }

        private static bool TryOpenCombatPile(string type, string announcementKey)
        {
            MatchManager match = MatchManager.Instance;
            if (match == null || match.characterWindow == null)
            {
                return false;
            }

            int heroIndex = match.GetHeroActive();
            if (heroIndex < 0)
            {
                heroIndex = FirstAvailableHeroIndex();
            }

            if (heroIndex < 0)
            {
                return false;
            }

            match.ShowCharacterWindow(type, isHero: true, heroIndex);
            ScreenReader.Say(Loc.Get(announcementKey));
            return true;
        }

        private static bool TryOpenNonCombatDeck()
        {
            int heroIndex = FirstAvailableHeroIndex();
            if (heroIndex < 0)
            {
                return false;
            }

            if (RewardsManager.Instance != null && RewardsManager.Instance.characterWindowUI != null)
            {
                RewardsManager.Instance.ShowDeck(heroIndex);
                ScreenReader.Say(Loc.Get("deck_opened"));
                return true;
            }

            if (LootManager.Instance != null && LootManager.Instance.characterWindowUI != null)
            {
                LootManager.Instance.ShowDeck(heroIndex);
                ScreenReader.Say(Loc.Get("deck_opened"));
                return true;
            }

            if (TownManager.Instance != null && TownManager.Instance.characterWindow != null)
            {
                TownManager.Instance.ShowDeck(heroIndex);
                ScreenReader.Say(Loc.Get("deck_opened"));
                return true;
            }

            if (MapManager.Instance != null && MapManager.Instance.characterWindow != null)
            {
                MapManager.Instance.ShowDeck(heroIndex);
                ScreenReader.Say(Loc.Get("deck_opened"));
                return true;
            }

            return false;
        }

        private static int FirstAvailableHeroIndex()
        {
            if (AtOManager.Instance == null)
            {
                return -1;
            }

            for (int i = 0; i < 4; i++)
            {
                Hero hero = AtOManager.Instance.team.GetHero(i);
                if (hero != null && hero.HeroData != null)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryGetActiveDeckWindow(out Transform deckContent, out Transform injuryContent, out string title, out CharacterWindowUI characterWindow, out UIDeckCards combatDeckWindow)
        {
            deckContent = null;
            injuryContent = null;
            title = string.Empty;
            characterWindow = null;
            combatDeckWindow = null;

            characterWindow = ActiveCharacterWindow();
            if (characterWindow != null && characterWindow.deckWindow != null && characterWindow.IsActive() && characterWindow.deckWindow.IsActive())
            {
                deckContent = characterWindow.deckWindow.deckContent;
                injuryContent = characterWindow.deckWindow.injuryContent != null && characterWindow.deckWindow.injuryContent.gameObject.activeInHierarchy
                    ? characterWindow.deckWindow.injuryContent
                    : null;
                title = ReadDeckWindowTitle(characterWindow.deckWindow);
                return deckContent != null;
            }

            if (MatchManager.Instance != null && MatchManager.Instance.DeckCardsWindow != null && MatchManager.Instance.DeckCardsWindow.IsActive() && !IsCombatActionSelectionMode())
            {
                combatDeckWindow = MatchManager.Instance.DeckCardsWindow;
                deckContent = combatDeckWindow.cardContainer;
                title = Clean(combatDeckWindow.textInstructions != null ? combatDeckWindow.textInstructions.text : "");
                return deckContent != null;
            }

            return false;
        }

        private static CharacterWindowUI ActiveCharacterWindow()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.characterWindow != null && MatchManager.Instance.characterWindow.IsActive())
            {
                return MatchManager.Instance.characterWindow;
            }

            if (RewardsManager.Instance != null && RewardsManager.Instance.characterWindowUI != null && RewardsManager.Instance.characterWindowUI.IsActive())
            {
                return RewardsManager.Instance.characterWindowUI;
            }

            if (LootManager.Instance != null && LootManager.Instance.characterWindowUI != null && LootManager.Instance.characterWindowUI.IsActive())
            {
                return LootManager.Instance.characterWindowUI;
            }

            if (TownManager.Instance != null && TownManager.Instance.characterWindow != null && TownManager.Instance.characterWindow.IsActive())
            {
                return TownManager.Instance.characterWindow;
            }

            if (MapManager.Instance != null && MapManager.Instance.characterWindow != null && MapManager.Instance.characterWindow.IsActive())
            {
                return MapManager.Instance.characterWindow;
            }

            return null;
        }

        private static string ReadDeckWindowTitle(DeckWindowUI deckWindow)
        {
            if (deckWindow == null)
            {
                return string.Empty;
            }

            string title = Clean(deckWindow.deckText != null ? deckWindow.deckText.text : "");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = Loc.Get("deck_screen");
            }

            return title;
        }

        private void Refresh(Transform deckContent, Transform injuryContent)
        {
            _items.Clear();
            AddCards(deckContent);
            AddCards(injuryContent);
            if (_itemIndex >= _items.Count)
            {
                _itemIndex = _items.Count - 1;
            }

            if (_itemIndex < 0)
            {
                _itemIndex = 0;
            }

            if (_lastCount != _items.Count)
            {
                _lineIndex = 0;
                _lastCount = _items.Count;
            }
        }

        private void AddCards(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                CardItem card = child.GetComponent<CardItem>();
                if (card == null || card.CardData == null)
                {
                    continue;
                }

                _items.Add(BuildCardItem(card));
            }
        }

        private static DeckItem BuildCardItem(CardItem card)
        {
            CardRealtimeData data = card.CardData;
            DeckItem item = new DeckItem();
            item.Card = card;
            item.Lines.AddRange(CardSpeech.BuildDetailLines(data, card.GetEnergyCost()));
            item.Summary = CardSpeech.BuildDetailSummary(data, card.GetEnergyCost());
            return item;
        }

        private void ProcessKeys(CharacterWindowUI characterWindow, UIDeckCards combatDeckWindow)
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (ctrl && ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                MoveLine(1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                MoveLine(-1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.Home))
            {
                JumpLine(false);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.End))
            {
                JumpLine(true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Home))
            {
                JumpItem(false);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.UpArrow) || ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow) || ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                OpenFocusedCardDetail();
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Escape))
            {
                CloseDeck(characterWindow, combatDeckWindow);
            }
        }

        private void AnnounceOnce(string title)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            string header = string.IsNullOrWhiteSpace(title) ? Loc.Get("deck_screen") : title;
            ScreenReader.Say(Loc.Get("deck_screen_with_title", header));
            ScreenReader.SayQueued(Loc.Get("deck_controls"));
            AnnounceFocusedItem(queued: true);
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("deck_empty"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _itemIndex, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void JumpItem(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("deck_empty"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _itemIndex, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void MoveLine(int delta)
        {
            DeckItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("deck_empty"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            DeckItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("deck_empty"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            DeckItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.SayQueued(Loc.Get("deck_empty"));
                return;
            }

            string message = item.Summary;
            if (queued)
            {
                ScreenReader.SayQueued(message);
            }
            else
            {
                ScreenReader.Say(message);
            }
        }

        private void OpenFocusedCardDetail()
        {
            DeckItem item = CurrentItem();
            if (item == null || item.Card == null || item.Card.CardData == null)
            {
                ScreenReader.Say(Loc.Get("deck_empty"));
                return;
            }

            if (CardScreenManager.Instance != null)
            {
                CardScreenHandler.Open(item.Card.CardData);
            }
        }

        private void CloseDeck(CharacterWindowUI characterWindow, UIDeckCards combatDeckWindow)
        {
            if (combatDeckWindow != null && MatchManager.Instance != null)
            {
                MatchManager.Instance.DrawDeckScreenDestroy();
            }
            else if (characterWindow != null)
            {
                characterWindow.Hide();
            }

            ScreenReader.Say(Loc.Get("deck_closed"));
            Reset();
        }

        private DeckItem CurrentItem()
        {
            if (_items.Count == 0 || _itemIndex < 0 || _itemIndex >= _items.Count)
            {
                return null;
            }

            return _items[_itemIndex];
        }

        private void Reset()
        {
            _announced = false;
            _itemIndex = 0;
            _lineIndex = 0;
            _lastCount = 0;
            _items.Clear();
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return count - 1;
            }

            if (index >= count)
            {
                return 0;
            }

            return index;
        }

        private static void AddLine(DeckItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
