using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access and announcements for event screens.
    /// </summary>
    public sealed class EventHandler
    {
        private sealed class EventItem
        {
            public string Summary;
            public Reply Reply;
            public BotonGeneric Button;
            public bool Unavailable;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<EventItem> _items = new List<EventItem>();

        private int _index;
        private int _lineIndex;
        private bool _announced;
        private float _lastRefreshTime;
        private string _lastHeader;
        private string _lastResult;
        private readonly HashSet<string> _announcedRollOutcomes = new HashSet<string>();

        /// <summary>
        /// Updates event screen navigation and announcements.
        /// </summary>
        public bool Update()
        {
            EventManager manager = EventManager.Instance;
            if (manager == null || !manager.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Event);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(manager);
                AnnounceEventOnce(manager);
                AnnounceRollOutcomeIfChanged(manager);
                AnnounceResultIfChanged(manager);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _lineIndex = 0;
            _announced = false;
            _lastHeader = null;
            _lastResult = null;
            _announcedRollOutcomes.Clear();
        }

        private void Refresh(EventManager manager)
        {
            _items.Clear();
            AddContinue(manager);
            AddReplies(manager);
            _index = ClampIndex(_index, _items.Count);
        }

        private void AddContinue(EventManager manager)
        {
            if (manager.continueButton == null || !Functions.TransformIsVisible(manager.continueButton))
            {
                return;
            }

            BotonGeneric button = manager.continueButton.GetComponent<BotonGeneric>();
            EventItem item = new EventItem();
            item.Button = button;
            AddLine(item, Loc.Get("event_continue"));
            item.Summary = BuildSummary(item);
            _items.Add(item);
        }

        private void AddReplies(EventManager manager)
        {
            Reply[] replies = manager.GetComponentsInChildren<Reply>(true);
            List<Reply> visible = new List<Reply>();
            for (int i = 0; i < replies.Length; i++)
            {
                Reply reply = replies[i];
                if (reply != null && reply.gameObject.activeInHierarchy && Functions.TransformIsVisible(reply.transform))
                {
                    visible.Add(reply);
                }
            }

            visible.Sort((left, right) => right.transform.position.y.CompareTo(left.transform.position.y));
            for (int i = 0; i < visible.Count; i++)
            {
                _items.Add(BuildReplyItem(visible[i], i + 1, visible.Count));
            }
        }

        private static EventItem BuildReplyItem(Reply reply, int position, int total)
        {
            EventItem item = new EventItem();
            item.Reply = reply;
            item.Unavailable = reply.replyButtonBlocked != null && reply.replyButtonBlocked.gameObject.activeInHierarchy;
            AddReplyCharacterLine(item, reply);
            AddLine(item, Clean(reply.replyText != null ? reply.replyText.text : ""));
            if (reply.replyRoll != null && reply.replyRoll.gameObject.activeInHierarchy)
            {
                AddLine(item, Loc.Get("event_roll", Clean(reply.replyRollText != null ? reply.replyRollText.text : "")));
                if (reply.probPopup != null && !string.IsNullOrWhiteSpace(reply.probPopup.text))
                {
                    AddLine(item, Loc.Get("event_probability", Clean(reply.probPopup.text)));
                }
            }

            if (item.Unavailable)
            {
                AddLine(item, Loc.Get("unavailable"));
            }

            item.Summary = BuildSummary(item);
            return item;
        }

        private static void AddReplyCharacterLine(EventItem item, Reply reply)
        {
            EventReplyData data = reply != null ? reply.GetEventReplyData() : null;
            if (data == null || data.RequiredClass == null)
            {
                return;
            }

            string hero = FindHeroNameForSubclass(data.RequiredClass);
            if (string.IsNullOrWhiteSpace(hero))
            {
                hero = Clean(data.RequiredClass.CharacterName);
            }

            if (string.IsNullOrWhiteSpace(hero))
            {
                return;
            }

            string key = data.ReplyActionText == Enums.EventAction.CharacterName
                ? "event_character_speaks"
                : "event_character_option";
            AddLine(item, Loc.Get(key, hero));
        }

        private static string FindHeroNameForSubclass(SubClassData requiredClass)
        {
            if (requiredClass == null || EventManager.Instance == null || EventManager.Instance.Heroes == null)
            {
                return "";
            }

            string requiredId = CleanId(requiredClass.Id);
            Hero[] heroes = EventManager.Instance.Heroes;
            for (int i = 0; i < heroes.Length; i++)
            {
                Hero hero = heroes[i];
                if (hero == null || hero.HeroData == null || hero.HeroData.HeroSubClass == null)
                {
                    continue;
                }

                if (CleanId(hero.HeroData.HeroSubClass.Id) == requiredId ||
                    CleanId(hero.SubclassName) == CleanId(requiredClass.SubClassName))
                {
                    return Clean(hero.SourceName);
                }
            }

            return "";
        }

        private void AnnounceEventOnce(EventManager manager)
        {
            string header = BuildHeader(manager);
            if (_announced && header == _lastHeader)
            {
                return;
            }

            _announced = true;
            _lastHeader = header;
            GameEventBuffer.Add(header);
            ScreenReader.Say(header);
            AnnounceFocusedItem(true);
        }

        private static string BuildHeader(EventManager manager)
        {
            List<string> lines = new List<string>();
            AddLine(lines, Loc.Get("event_screen"));
            AddLine(lines, Clean(manager.title != null ? manager.title.text : ""));
            AddLine(lines, Clean(manager.description != null ? manager.description.text : ""));
            return string.Join(" ", lines.ToArray());
        }

        private void AnnounceResultIfChanged(EventManager manager)
        {
            if (manager.result == null || !manager.result.gameObject.activeInHierarchy)
            {
                return;
            }

            string result = Clean(manager.result.text);
            if (string.IsNullOrWhiteSpace(result) || result == _lastResult)
            {
                return;
            }

            _lastResult = result;
            string message = Loc.Get("event_result", result);
            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }

        private void AnnounceRollOutcomeIfChanged(EventManager manager)
        {
            AnnounceGlobalRollOutcome(manager.resultOK);
            AnnounceGlobalRollOutcome(manager.resultOKc);
            AnnounceGlobalRollOutcome(manager.resultKO);
            AnnounceGlobalRollOutcome(manager.resultKOc);
            AnnounceCharacterRollOutcomes(manager);
        }

        private void AnnounceGlobalRollOutcome(TMP_Text text)
        {
            string outcome = ReadVisibleText(text);
            if (string.IsNullOrWhiteSpace(outcome))
            {
                return;
            }

            AnnounceRollOutcome("global:" + outcome, Loc.Get("event_roll_result", outcome));
        }

        private void AnnounceCharacterRollOutcomes(EventManager manager)
        {
            if (manager.characterT == null)
            {
                return;
            }

            Hero[] heroes = manager.Heroes;
            for (int i = 0; i < manager.characterT.Length; i++)
            {
                Transform character = manager.characterT[i];
                if (character == null || character.childCount < 4)
                {
                    continue;
                }

                string hero = ReadHeroName(heroes, i);
                AnnounceCharacterRollOutcome(i, hero, character.GetChild(2).GetComponent<TMP_Text>());
                AnnounceCharacterRollOutcome(i, hero, character.GetChild(3).GetComponent<TMP_Text>());
            }
        }

        private void AnnounceCharacterRollOutcome(int index, string hero, TMP_Text text)
        {
            string outcome = ReadVisibleText(text);
            if (string.IsNullOrWhiteSpace(outcome))
            {
                return;
            }

            string key = "hero:" + index + ":" + outcome;
            string message = string.IsNullOrWhiteSpace(hero)
                ? Loc.Get("event_roll_result", outcome)
                : Loc.Get("event_roll_result_character", hero, outcome);
            AnnounceRollOutcome(key, message);
        }

        private void AnnounceRollOutcome(string key, string message)
        {
            if (_announcedRollOutcomes.Contains(key))
            {
                return;
            }

            _announcedRollOutcomes.Add(key);
            GameEventBuffer.Add(message);
            ScreenReader.SayQueued(message);
        }

        private static string ReadVisibleText(TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy || !Functions.TransformIsVisible(text.transform))
            {
                return "";
            }

            return Clean(text.text);
        }

        private static string ReadHeroName(Hero[] heroes, int index)
        {
            if (heroes == null || index < 0 || index >= heroes.Length || heroes[index] == null)
            {
                return "";
            }

            return Clean(heroes[index].SourceName);
        }

        private void ProcessKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveLine(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveLine(1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Home))
            {
                JumpLine(false);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.End))
            {
                JumpLine(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                JumpItem(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateFocusedItem();
            }
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_no_options"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
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
                ScreenReader.Say(Loc.Get("event_no_options"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void MoveLine(int delta)
        {
            EventItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_no_options"));
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
            EventItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("event_no_options"));
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
            EventItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("event_no_options"));
                return;
            }

            if (queued)
            {
                ScreenReader.SayQueued(item.Summary);
            }
            else
            {
                ScreenReader.Say(item.Summary);
            }
        }

        private void ActivateFocusedItem()
        {
            EventItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("event_no_options"));
                return;
            }

            if (item.Unavailable)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                return;
            }

            if (item.Button != null)
            {
                string message = Loc.Get("activated", Loc.Get("event_continue"));
                GameEventBuffer.Add(message);
                ScreenReader.Say(message);
                item.Button.Clicked();
                return;
            }

            if (item.Reply != null)
            {
                string message = Loc.Get("activated", item.Summary);
                GameEventBuffer.Add(message);
                ScreenReader.Say(message);
                item.Reply.SelectThisOption();
            }
        }

        private EventItem CurrentItem()
        {
            if (_index < 0 || _index >= _items.Count)
            {
                return null;
            }

            return _items[_index];
        }

        private static int ClampIndex(int index, int count)
        {
            if (count == 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static void AddLine(EventItem item, string line)
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

        private static string BuildSummary(EventItem item)
        {
            return string.Join(" ", item.Lines.ToArray());
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static string CleanId(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "" : text.Replace(" ", "").ToLowerInvariant();
        }
    }
}
