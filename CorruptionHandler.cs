using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for the map corruption reward choice.
    /// </summary>
    public sealed class CorruptionHandler
    {
        private sealed class CorruptionItem
        {
            public string Summary;
            public string RewardChoice;
            public bool ToggleAccepted;
            public bool Continue;
            public readonly List<string> Lines = new List<string>();
        }

        private static readonly FieldInfo CorruptionRewardIdField = AccessTools.Field(typeof(CorruptionManager), "corruptionRewardId");
        private static readonly FieldInfo CorruptionRewardIdBField = AccessTools.Field(typeof(CorruptionManager), "corruptionRewardIdB");

        private readonly List<CorruptionItem> _items = new List<CorruptionItem>();
        private int _index;
        private int _lineIndex;
        private bool _announced;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates corruption choice navigation.
        /// </summary>
        public bool Update()
        {
            MapManager map = MapManager.Instance;
            CorruptionManager corruption = map != null ? map.corruption : null;
            if (corruption == null || !corruption.IsActive())
            {
                Reset();
                return false;
            }

            if (IsCardCraftActive())
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Corruption);
            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(corruption);
                AnnounceOnce();
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(corruption);
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _lineIndex = 0;
            _announced = false;
        }

        private void Refresh(CorruptionManager corruption)
        {
            _items.Clear();
            AddCorruptionCard(corruption);
            AddReward(corruption, "A", corruption.rewardBotA);
            AddReward(corruption, "B", corruption.rewardBotB);
            AddToggle(corruption);
            AddContinue(corruption);
            _index = ClampIndex(_index, _items.Count);
            _lineIndex = ClampIndex(_lineIndex, CurrentLines().Count);
        }

        private void AddCorruptionCard(CorruptionManager corruption)
        {
            CorruptionItem item = new CorruptionItem();
            AddLine(item, Loc.Get("corruption_screen"));
            AddLine(item, Clean(corruption.textDifficulty != null ? corruption.textDifficulty.text : ""));
            AddLine(item, Clean(corruption.textAcceptScore != null ? corruption.textAcceptScore.text : ""));

            CardData data = AtOManager.Instance != null && !string.IsNullOrWhiteSpace(AtOManager.Instance.corruptionIdCard)
                ? Globals.Instance.GetCardData(AtOManager.Instance.corruptionIdCard, instantiate: false)
                : null;
            if (data != null)
            {
                AddLine(item, Clean(data.CardName));
                AddLine(item, Loc.Get("combat_card_description", Clean(!string.IsNullOrWhiteSpace(data.DescriptionNormalized) ? data.DescriptionNormalized : data.Description)));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddReward(CorruptionManager corruption, string choice, BotonGeneric button)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return;
            }

            CorruptionItem item = new CorruptionItem();
            item.RewardChoice = choice;
            string text = ReadButtonText(button, Loc.Get("corruption_reward_choice", choice));
            AddLine(item, Loc.Get("corruption_reward_choice", choice));
            AddLine(item, text);
            AddRewardCardLines(item, RewardId(corruption, choice));
            if (!button.IsEnabled())
            {
                AddLine(item, Loc.Get("unavailable"));
            }

            bool selected = IsSelectedReward(corruption, choice);
            if (selected)
            {
                AddLine(item, Loc.Get("corruption_selected"));
            }

            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private static bool IsSelectedReward(CorruptionManager corruption, string choice)
        {
            if (AtOManager.Instance == null || string.IsNullOrWhiteSpace(AtOManager.Instance.corruptionId))
            {
                return false;
            }

            string rewardId = "";
            if (choice == "A" && CorruptionRewardIdField != null)
            {
                rewardId = CorruptionRewardIdField.GetValue(corruption) as string;
            }
            else if (choice == "B" && CorruptionRewardIdBField != null)
            {
                rewardId = CorruptionRewardIdBField.GetValue(corruption) as string;
            }

            return !string.IsNullOrWhiteSpace(rewardId) && AtOManager.Instance.corruptionId == rewardId;
        }

        private static string RewardId(CorruptionManager corruption, string choice)
        {
            if (choice == "A" && CorruptionRewardIdField != null)
            {
                return CorruptionRewardIdField.GetValue(corruption) as string;
            }

            if (choice == "B" && CorruptionRewardIdBField != null)
            {
                return CorruptionRewardIdBField.GetValue(corruption) as string;
            }

            return "";
        }

        private static void AddRewardCardLines(CorruptionItem item, string rewardId)
        {
            if (rewardId != "herocard" || AtOManager.Instance == null || string.IsNullOrWhiteSpace(AtOManager.Instance.corruptionRewardCard))
            {
                return;
            }

            CardData rewardCard = Globals.Instance.GetCardData(AtOManager.Instance.corruptionRewardCard, instantiate: false);
            if (rewardCard == null)
            {
                return;
            }

            string heroName = "";
            Hero[] team = AtOManager.Instance.GetTeam();
            int heroIndex = AtOManager.Instance.corruptionRewardChar;
            if (team != null && heroIndex >= 0 && heroIndex < team.Length && team[heroIndex] != null)
            {
                heroName = team[heroIndex].SourceName;
            }

            AddLine(item, string.IsNullOrWhiteSpace(heroName)
                ? Loc.Get("corruption_reward_card", Clean(rewardCard.CardName))
                : Loc.Get("corruption_reward_card_for_hero", Clean(rewardCard.CardName), Clean(heroName)));
            List<string> cardLines = CardSpeech.BuildCardLines(rewardCard, rewardCard.EnergyCost);
            for (int i = 0; i < cardLines.Count; i++)
            {
                AddLine(item, cardLines[i]);
            }
        }

        private void AddToggle(CorruptionManager corruption)
        {
            if (corruption.botonGenericX == null || !corruption.botonGenericX.gameObject.activeInHierarchy)
            {
                return;
            }

            CorruptionItem item = new CorruptionItem();
            item.ToggleAccepted = true;
            bool accepted = AtOManager.Instance != null && AtOManager.Instance.corruptionAccepted;
            AddLine(item, accepted ? Loc.Get("corruption_accepted") : Loc.Get("corruption_not_accepted"));
            item.Summary = item.Lines[0];
            _items.Add(item);
        }

        private void AddContinue(CorruptionManager corruption)
        {
            if (corruption.corruptionContinue == null || !corruption.corruptionContinue.gameObject.activeInHierarchy)
            {
                return;
            }

            CorruptionItem item = new CorruptionItem();
            item.Continue = true;
            AddLine(item, ReadTransformText(corruption.corruptionContinue, Loc.Get("corruption_continue")));
            item.Summary = item.Lines[0];
            _items.Add(item);
        }

        private void AnnounceOnce()
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(Loc.Get("corruption_screen"));
            AnnounceFocused(true);
        }

        private void ProcessKeys(CorruptionManager corruption)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                ReadLine(1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                ReadLine(-1);
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
                Jump(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(corruption);
            }
        }

        private void Move(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void Jump(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void ReadLine(int delta)
        {
            List<string> lines = CurrentLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _lineIndex, delta, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void JumpLine(bool end)
        {
            List<string> lines = CurrentLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void Activate(CorruptionManager corruption)
        {
            CorruptionItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(item.RewardChoice))
            {
                BotonGeneric button = item.RewardChoice == "A" ? corruption.rewardBotA : corruption.rewardBotB;
                if (button == null || !button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                    return;
                }

                corruption.ChooseReward(item.RewardChoice);
                ScreenReader.Say(Loc.Get("corruption_reward_selected", item.RewardChoice));
                Refresh(corruption);
                AnnounceFocused(true);
                return;
            }

            if (item.ToggleAccepted)
            {
                if (corruption.botonGenericX == null || !corruption.botonGenericX.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                    return;
                }

                corruption.BoxClicked();
                ScreenReader.Say(AtOManager.Instance != null && AtOManager.Instance.corruptionAccepted ? Loc.Get("corruption_accepted") : Loc.Get("corruption_not_accepted"));
                Refresh(corruption);
                return;
            }

            if (item.Continue)
            {
                ScreenReader.Say(Loc.Get("activated_loading", item.Summary));
                MapManager.Instance.CorruptionContinue();
            }
            else
            {
                ScreenReader.Say(item.Summary);
            }
        }

        private void AnnounceFocused(bool queued = false)
        {
            CorruptionItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("corruption_no_item"));
                return;
            }

            string text = item.Summary;
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private CorruptionItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _index = ClampIndex(_index, _items.Count);
            return _items[_index];
        }

        private List<string> CurrentLines()
        {
            CorruptionItem item = CurrentItem();
            return item != null ? item.Lines : new List<string>();
        }

        private static string ReadButtonText(BotonGeneric button, string fallback)
        {
            string text = button != null ? Clean(button.GetText()) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string ReadTransformText(Transform transform, string fallback)
        {
            TMP_Text text = transform != null ? transform.GetComponentInChildren<TMP_Text>(true) : null;
            string value = text != null ? Clean(text.text) : "";
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        private static void AddLine(CorruptionItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
        }

        private static int ClampIndex(int index, int count)
        {
            if (count == 0 || index < 0)
            {
                return 0;
            }

            return index >= count ? count - 1 : index;
        }

        private static bool IsCardCraftActive()
        {
            CardCraftManager craft = CardCraftManager.Instance;
            return craft != null && craft.gameObject.activeInHierarchy;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
