using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard and screen reader access for multiplayer path conflict resolution.
    /// </summary>
    public sealed class ConflictHandler
    {
        private sealed class ConflictItem
        {
            public string Key;
            public string Label;
            public int Option = -1;
        }

        private readonly List<ConflictItem> _items = new List<ConflictItem>();
        private int _index;
        private bool _announced;
        private string _lastStatus;
        private string _lastWinner;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates conflict navigation and announcements.
        /// </summary>
        public bool Update()
        {
            ConflictManager conflict = ActiveConflict();
            if (conflict == null)
            {
                Reset();
                return false;
            }

            AccessStateManager.SetState(AccessState.Conflict);
            if (Time.unscaledTime - _lastRefreshTime > 0.15f)
            {
                Refresh(conflict);
                AnnounceStateChanges(conflict);
                _lastRefreshTime = Time.unscaledTime;
            }

            if (!_announced)
            {
                _announced = true;
                ScreenReader.Say(Loc.Get("conflict_screen"));
                AnnounceStatus(conflict);
                AnnounceFocused(true);
            }

            ProcessKeys(conflict);
            return true;
        }

        private static ConflictManager ActiveConflict()
        {
            MapManager map = MapManager.Instance;
            if (map == null || map.Conflict == null || !map.Conflict.IsActive())
            {
                return null;
            }

            return map.Conflict;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _announced = false;
            _lastStatus = null;
            _lastWinner = null;
            _lastRefreshTime = 0f;
        }

        private void Refresh(ConflictManager conflict)
        {
            string currentKey = CurrentItem() != null ? CurrentItem().Key : null;
            _items.Clear();
            AddOptions(conflict);
            AddHeroes();

            _index = ClampIndex(_index, _items.Count);
            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Key == currentKey)
                    {
                        _index = i;
                        break;
                    }
                }
            }
        }

        private void AddOptions(ConflictManager conflict)
        {
            bool anyEnabled = false;
            if (conflict.botonConflict != null)
            {
                for (int i = 0; i < conflict.botonConflict.Length; i++)
                {
                    BotonGeneric button = conflict.botonConflict[i];
                    if (button == null || !button.gameObject.activeInHierarchy || !button.IsEnabled())
                    {
                        continue;
                    }

                    anyEnabled = true;
                    ConflictItem item = new ConflictItem();
                    item.Key = "option:" + i;
                    item.Option = i;
                    item.Label = Loc.Get("conflict_option", ReadButton(button, OptionFallback(i)));
                    _items.Add(item);
                }
            }

            if (!anyEnabled)
            {
                ConflictItem item = new ConflictItem();
                item.Key = "waiting";
                item.Label = StatusText(conflict);
                _items.Add(item);
            }
        }

        private void AddHeroes()
        {
            if (AtOManager.Instance == null)
            {
                return;
            }

            Hero[] heroes = AtOManager.Instance.GetTeam();
            if (heroes == null)
            {
                return;
            }

            for (int i = 0; i < heroes.Length; i++)
            {
                Hero hero = heroes[i];
                if (hero == null || hero.HeroData == null)
                {
                    continue;
                }

                string owner = PlayerName(hero.Owner);
                ConflictItem item = new ConflictItem();
                item.Key = "hero:" + i;
                item.Label = Loc.Get("conflict_hero", Clean(hero.SourceName), owner);
                _items.Add(item);
            }
        }

        private void AnnounceStateChanges(ConflictManager conflict)
        {
            string status = StatusText(conflict);
            if (_announced && !string.IsNullOrWhiteSpace(status) && status != _lastStatus)
            {
                _lastStatus = status;
                ScreenReader.Say(status);
            }

            string winner = Clean(ReadText(conflict.charWins));
            if (_announced && !string.IsNullOrWhiteSpace(winner) && winner != _lastWinner)
            {
                _lastWinner = winner;
                ScreenReader.Say(Loc.Get("conflict_winner", winner));
            }
        }

        private void AnnounceStatus(ConflictManager conflict)
        {
            string status = StatusText(conflict);
            if (!string.IsNullOrWhiteSpace(status))
            {
                _lastStatus = status;
                ScreenReader.Say(status);
            }
        }

        private void ProcessKeys(ConflictManager conflict)
        {
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Move(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                Move(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(conflict);
            }
        }

        private void Move(int delta)
        {
            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            AnnounceFocused(true);
        }

        private void Jump(bool end)
        {
            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            AnnounceFocused(true);
        }

        private void Activate(ConflictManager conflict)
        {
            ConflictItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("conflict_no_item"));
                return;
            }

            if (item.Option < 0)
            {
                ScreenReader.Say(StatusText(conflict));
                return;
            }

            ScreenReader.Say(Loc.Get("conflict_selected_option", item.Label));
            MapManager.Instance.ConflictSelection(item.Option);
        }

        private void AnnounceFocused(bool interrupt)
        {
            ConflictItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("conflict_no_item"), interrupt);
                return;
            }

            ScreenReader.Say(Loc.Get("conflict_item", _index + 1, _items.Count, item.Label), interrupt);
        }

        private ConflictItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private static int ClampIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            if (index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static string StatusText(ConflictManager conflict)
        {
            string choosing = Clean(ReadText(conflict.nickChoosing));
            if (!string.IsNullOrWhiteSpace(choosing))
            {
                return choosing;
            }

            string winner = Clean(ReadText(conflict.charWins));
            if (!string.IsNullOrWhiteSpace(winner))
            {
                return Loc.Get("conflict_winner", winner);
            }

            return Loc.Get("conflict_waiting");
        }

        private static string ReadButton(BotonGeneric button, string fallback)
        {
            string text = button != null ? button.GetText() : "";
            text = Clean(text);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string OptionFallback(int option)
        {
            switch (option)
            {
                case 0:
                    return Loc.Get("conflict_option_lowest");
                case 1:
                    return Loc.Get("conflict_option_middle");
                case 2:
                    return Loc.Get("conflict_option_highest");
                default:
                    return Loc.Get("unknown_value");
            }
        }

        private static string PlayerName(string nick)
        {
            if (string.IsNullOrWhiteSpace(nick))
            {
                return Loc.Get("unknown_value");
            }

            return NetworkManager.Instance != null ? Clean(NetworkManager.Instance.GetPlayerNickReal(nick)) : Clean(nick);
        }

        private static string ReadText(TMP_Text text)
        {
            return text != null ? text.text : "";
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
