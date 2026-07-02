using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

using Cards;
using Cards.Data;
namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for the event card shuffle mini-games.
    /// </summary>
    public sealed class CardPlayerHandler
    {
        private readonly List<CardPlayerItem> _items = new List<CardPlayerItem>();
        private int _index;
        private string _lastScene = "";
        private bool _announced;
        private bool _announcedShuffleWait;

        /// <summary>
        /// Updates card shuffle mini-game navigation.
        /// </summary>
        public bool Update()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            bool singleActive = sceneName == "CardPlayer" && CardPlayerManager.Instance != null;
            bool pairsActive = sceneName == "CardPlayerPairs" && CardPlayerPairsManager.Instance != null;
            if (!singleActive && !pairsActive)
            {
                Reset();
                return false;
            }

            if (_lastScene != sceneName)
            {
                Reset();
                _lastScene = sceneName;
            }

            Refresh(singleActive, pairsActive);
            if (!_announced)
            {
                _announced = true;
                ScreenReader.Say(singleActive ? Loc.Get("card_player_screen") : Loc.Get("card_player_pairs_screen"));
                ScreenReader.SayQueued(singleActive ? Loc.Get("card_player_controls") : Loc.Get("card_player_pairs_controls"));
                SpeakFocus();
            }

            if (_items.Count == 0)
            {
                if (!_announcedShuffleWait)
                {
                    _announcedShuffleWait = true;
                    ScreenReader.Say(Loc.Get("card_player_shuffling"));
                }

                return true;
            }

            _announcedShuffleWait = false;
            ProcessKeys();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _lastScene = "";
            _announced = false;
            _announcedShuffleWait = false;
        }

        private void Refresh(bool singleActive, bool pairsActive)
        {
            Transform previous = CurrentTransform();
            _items.Clear();
            if (singleActive)
            {
                AddSingleItems(CardPlayerManager.Instance);
            }
            else if (pairsActive)
            {
                AddPairsItems(CardPlayerPairsManager.Instance);
            }

            _index = IndexOf(previous);
            if (_index < 0)
            {
                _index = NavigationBounds.ClampIndex(_index, _items.Count);
            }
        }

        private void AddSingleItems(CardPlayerManager manager)
        {
            if (manager == null)
            {
                return;
            }

            AddButton(manager.botShuffle, Loc.Get("card_player_shuffle"), () => manager.Shuffle());
            bool beforeShuffle = IsVisible(manager.botShuffle);
            bool canChoose = IsVisible(manager.choose);
            AddCards(manager.cardContainer, canChoose, beforeShuffle, Loc.Get("card_player_face_down"));
        }

        private void AddPairsItems(CardPlayerPairsManager manager)
        {
            if (manager == null)
            {
                return;
            }

            AddText(manager.playerTurnText);
            AddButton(manager.botShuffle, Loc.Get("card_player_shuffle"), () => manager.Shuffle());
            if (manager.finishButton != null)
            {
                AddButton(manager.finishButton.transform, ReadButtonText(manager.finishButton, Loc.Get("card_player_finish_pairs")), () => manager.FinishPairGame());
            }

            bool beforeShuffle = IsVisible(manager.botShuffle);
            AddCards(manager.cardContainer, manager.CanClick(), beforeShuffle, Loc.Get("card_player_pair_face_down"));
        }

        private void AddText(TMP_Text text)
        {
            if (text == null || string.IsNullOrWhiteSpace(text.text) || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            string clean = Clean(text.text);
            if (!string.IsNullOrWhiteSpace(clean))
            {
                _items.Add(new CardPlayerItem(text.transform, clean, null));
            }
        }

        private void AddButton(Transform transform, string fallback, System.Action action)
        {
            if (!IsVisible(transform))
            {
                return;
            }

            _items.Add(new CardPlayerItem(transform, ReadTransformText(transform, fallback), action));
        }

        private void AddCards(Transform container, bool selectable, bool beforeShuffle, string hiddenKey)
        {
            if (container == null)
            {
                return;
            }

            List<CardItem> cards = new List<CardItem>();
            foreach (Transform child in container)
            {
                CardItem card = child != null ? child.GetComponent<CardItem>() : null;
                if (card == null || card.CardData == null || !card.gameObject.activeInHierarchy || !Functions.TransformIsVisible(card.transform))
                {
                    continue;
                }

                cards.Add(card);
            }

            cards.Sort(CompareCardsByPosition);
            for (int i = 0; i < cards.Count; i++)
            {
                CardItem card = cards[i];
                bool reveal = beforeShuffle || card.cardrevealed;
                string summary = reveal
                    ? Loc.Get("card_player_visible_card", i + 1, CardSpeech.BuildCardFocusSummary(card.CardData, card.CardData.EnergyCost))
                    : Loc.Get(hiddenKey, i + 1);
                System.Action action = selectable && card.CardPlayerIndex >= 0 ? () => SelectCard(card) : (System.Action)null;
                _items.Add(new CardPlayerItem(card.transform, summary, action));
            }
        }

        private static int CompareCardsByPosition(CardItem left, CardItem right)
        {
            Vector3 a = left.transform.position;
            Vector3 b = right.transform.position;
            int y = -a.y.CompareTo(b.y);
            return y != 0 ? y : a.x.CompareTo(b.x);
        }

        private static void SelectCard(CardItem card)
        {
            if (card == null)
            {
                return;
            }

            int index = card.CardPlayerIndex;
            if (CardPlayerManager.Instance != null)
            {
                CardPlayerManager.Instance.SelectCard(index);
            }
            else if (CardPlayerPairsManager.Instance != null)
            {
                CardPlayerPairsManager.Instance.SelectCard(index);
            }

            ScreenReader.Say(Loc.Get("card_player_selected", CardSpeech.BuildCardFocusSummary(card.CardData, card.CardData.EnergyCost)));
        }

        private void ProcessKeys()
        {
            if (ModInput.GetKeyDown(KeyCode.DownArrow) || ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                if (NavigationBounds.TryMove(ref _index, 1, _items.Count))
                {
                    SpeakFocus();
                }
            }
            else if (ModInput.GetKeyDown(KeyCode.UpArrow) || ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                if (NavigationBounds.TryMove(ref _index, -1, _items.Count))
                {
                    SpeakFocus();
                }
            }
            else if (ModInput.GetKeyDown(KeyCode.Home))
            {
                if (NavigationBounds.TryJump(ref _index, false, _items.Count))
                {
                    SpeakFocus();
                }
            }
            else if (ModInput.GetKeyDown(KeyCode.End))
            {
                if (NavigationBounds.TryJump(ref _index, true, _items.Count))
                {
                    SpeakFocus();
                }
            }
            else if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                Activate();
            }
        }

        private void Activate()
        {
            CardPlayerItem item = CurrentItem();
            if (item == null || item.Action == null)
            {
                ScreenReader.Say(Loc.Get("card_player_no_action"));
                return;
            }

            ScreenReader.Say(Loc.Get("activated", item.Summary));
            item.Action();
        }

        private void SpeakFocus()
        {
            CardPlayerItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("card_player_no_item"));
                return;
            }

            ScreenReader.Say(item.Summary);
        }

        private CardPlayerItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = NavigationBounds.ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private Transform CurrentTransform()
        {
            CardPlayerItem item = CurrentItem();
            return item != null ? item.Transform : null;
        }

        private int IndexOf(Transform transform)
        {
            if (transform == null)
            {
                return -1;
            }

            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].Transform == transform)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && transform.gameObject.activeInHierarchy && Functions.TransformIsVisible(transform);
        }

        private static string ReadButtonText(BotonGeneric button, string fallback)
        {
            if (button == null)
            {
                return fallback;
            }

            string text = Clean(button.GetText());
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string ReadTransformText(Transform transform, string fallback)
        {
            if (transform == null)
            {
                return fallback;
            }

            TMP_Text text = transform.GetComponentInChildren<TMP_Text>(true);
            string clean = Clean(text != null ? text.text : "");
            return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
        }

        private static string Clean(string value)
        {
            return TextCleaner.ToSpeech(value);
        }

        private sealed class CardPlayerItem
        {
            internal CardPlayerItem(Transform transform, string summary, System.Action action)
            {
                Transform = transform;
                Summary = summary;
                Action = action;
            }

            internal Transform Transform { get; private set; }

            internal string Summary { get; private set; }

            internal System.Action Action { get; private set; }
        }
    }
}
