using System.Collections.Generic;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access for post-combat and event card reward screens.
    /// </summary>
    public sealed class RewardsHandler
    {
        private sealed class RewardItem
        {
            public string Summary;
            public int CharacterIndex;
            public string InternalId;
            public bool IsDust;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<int> _characters = new List<int>();
        private readonly List<RewardItem> _items = new List<RewardItem>();
        private int _characterListIndex;
        private int _itemIndex;
        private int _lineIndex;
        private bool _announced;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates reward screen navigation.
        /// </summary>
        public bool Update()
        {
            RewardsManager rewards = RewardsManager.Instance;
            if (rewards == null || !rewards.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Rewards);
            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(rewards);
                AnnounceOnce(rewards);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(rewards);
            return true;
        }

        private void Reset()
        {
            _characters.Clear();
            _items.Clear();
            _characterListIndex = 0;
            _itemIndex = 0;
            _lineIndex = 0;
            _announced = false;
        }

        private void Refresh(RewardsManager rewards)
        {
            int previousCharacter = CurrentCharacterIndex();
            _characters.Clear();
            for (int i = 0; i < rewards.characterRewardArray.Length; i++)
            {
                CharacterReward characterReward = GetCharacterReward(rewards, i);
                if (characterReward == null || !Functions.TransformIsVisible(characterReward.transform))
                {
                    continue;
                }

                if (HasAvailableReward(characterReward))
                {
                    _characters.Add(i);
                }
            }

            if (_characters.Count == 0)
            {
                _items.Clear();
                _characterListIndex = 0;
                _itemIndex = 0;
                _lineIndex = 0;
                return;
            }

            int restoredIndex = _characters.IndexOf(previousCharacter);
            if (restoredIndex >= 0)
            {
                _characterListIndex = restoredIndex;
            }
            else
            {
                _characterListIndex = ClampIndex(_characterListIndex, _characters.Count);
            }

            BuildItems(rewards);
            _itemIndex = ClampIndex(_itemIndex, _items.Count);
            _lineIndex = ClampIndex(_lineIndex, CurrentLinesCount());
        }

        private static CharacterReward GetCharacterReward(RewardsManager rewards, int index)
        {
            if (rewards == null || rewards.characterRewardArray == null || index < 0 || index >= rewards.characterRewardArray.Length)
            {
                return null;
            }

            Transform transform = rewards.characterRewardArray[index];
            return transform != null ? transform.GetComponent<CharacterReward>() : null;
        }

        private static bool HasAvailableReward(CharacterReward characterReward)
        {
            if (characterReward == null)
            {
                return false;
            }

            if (characterReward.cardsByInternalId != null)
            {
                foreach (KeyValuePair<string, CardItem> pair in characterReward.cardsByInternalId)
                {
                    CardItem card = pair.Value;
                    if (card != null && card.CardData != null && card.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }

            return IsDustAvailable(characterReward);
        }

        private void BuildItems(RewardsManager rewards)
        {
            _items.Clear();
            CharacterReward characterReward = GetCharacterReward(rewards, CurrentCharacterIndex());
            if (characterReward == null)
            {
                return;
            }

            if (characterReward.cardsByInternalId != null)
            {
                foreach (KeyValuePair<string, CardItem> pair in characterReward.cardsByInternalId)
                {
                    CardItem card = pair.Value;
                    if (card == null || card.CardData == null || !card.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    RewardItem item = BuildCardReward(CurrentCharacterIndex(), pair.Key, card.CardData);
                    _items.Add(item);
                }
            }

            if (IsDustAvailable(characterReward))
            {
                _items.Add(BuildDustReward(CurrentCharacterIndex(), characterReward));
            }
        }

        private static bool IsDustAvailable(CharacterReward characterReward)
        {
            if (characterReward == null || characterReward.quantityDust == null || !characterReward.quantityDust.gameObject.activeInHierarchy)
            {
                return false;
            }

            Transform button = characterReward.quantityDust.childCount > 0 ? characterReward.quantityDust.GetChild(0) : null;
            BoxCollider2D collider = button != null ? button.GetComponent<BoxCollider2D>() : null;
            return collider == null || collider.enabled;
        }

        private static RewardItem BuildCardReward(int characterIndex, string internalId, CardData data)
        {
            RewardItem item = new RewardItem();
            item.CharacterIndex = characterIndex;
            item.InternalId = internalId;
            item.Lines.AddRange(CardSpeech.BuildCardLines(data, data.EnergyCost));
            if (data != null && data.CardType == Enums.CardType.Pet)
            {
                item.Lines.AddRange(CardSpeech.BuildItemEffectLines(data));
            }

            item.Summary = CardSpeech.BuildCardFocusSummary(data, data.EnergyCost);
            return item;
        }

        private static RewardItem BuildDustReward(int characterIndex, CharacterReward characterReward)
        {
            RewardItem item = new RewardItem();
            item.CharacterIndex = characterIndex;
            item.IsDust = true;
            string quantity = Clean(characterReward.quantityDustText != null ? characterReward.quantityDustText.text : "");
            AddLine(item, string.IsNullOrWhiteSpace(quantity) ? Loc.Get("reward_dust") : Loc.Get("reward_dust_quantity", quantity));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            return item;
        }

        private void AnnounceOnce(RewardsManager rewards)
        {
            if (_announced || _characters.Count == 0)
            {
                return;
            }

            _announced = true;
            string title = Clean(rewards.title != null ? rewards.title.text : "");
            string subtitle = Clean(rewards.subtitle != null ? rewards.subtitle.text : "");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = Loc.Get("reward_screen");
            }

            string message = string.IsNullOrWhiteSpace(subtitle) ? title : Loc.Get("reward_screen_with_subtitle", title, subtitle);
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            AnnounceFocused(true);
        }

        private void ProcessKeys(RewardsManager rewards)
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveLine(1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveLine(-1);
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

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveCharacter(-1, rewards);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveCharacter(1, rewards);
                return;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveItem(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveItem(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate(rewards);
            }
        }

        private void MoveCharacter(int delta, RewardsManager rewards)
        {
            if (_characters.Count == 0)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _characterListIndex, delta, _characters.Count))
            {
                return;
            }

            _itemIndex = 0;
            _lineIndex = 0;
            BuildItems(rewards);
            AnnounceCharacter();
            AnnounceFocused(true);
        }

        private void MoveItem(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _itemIndex, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void JumpItem(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _itemIndex, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void MoveLine(int delta)
        {
            RewardItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
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
            RewardItem item = CurrentItem();
            if (item == null || item.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, item.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(item.Lines[_lineIndex]);
        }

        private void Activate(RewardsManager rewards)
        {
            RewardItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            CharacterReward characterReward = GetCharacterReward(rewards, item.CharacterIndex);
            if (characterReward == null)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
                return;
            }

            if (!CanChooseReward(rewards, item.CharacterIndex))
            {
                ScreenReader.Say(Loc.Get("reward_not_owner", RewardOwnerName(rewards, item.CharacterIndex)));
                return;
            }

            string message = Loc.Get("activated", item.Lines.Count > 0 ? item.Lines[0] : item.Summary);
            GameEventBuffer.Add(message);
            ScreenReader.Say(message);
            if (item.IsDust)
            {
                characterReward.DustSelected("");
            }
            else
            {
                characterReward.CardSelected("", item.InternalId);
            }

            Refresh(rewards);
        }

        private void AnnounceCharacter()
        {
            Hero hero = CurrentHero();
            string name = hero != null ? Clean(hero.SourceName) : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = Loc.Get("unknown_hero");
            }

            RewardsManager rewards = RewardsManager.Instance;
            if (GameManager.Instance != null && GameManager.Instance.IsMultiplayer() && rewards != null)
            {
                string owner = RewardOwnerName(rewards, CurrentCharacterIndex());
                string key = CanChooseReward(rewards, CurrentCharacterIndex()) ? "reward_character_owner" : "reward_character_owner_read_only";
                ScreenReader.Say(Loc.Get(key, name, owner));
                return;
            }

            ScreenReader.Say(Loc.Get("reward_character", name));
        }

        private void AnnounceFocused(bool queued = false)
        {
            RewardItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("reward_no_item"));
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

        private RewardItem CurrentItem()
        {
            if (_items.Count == 0)
            {
                return null;
            }

            _itemIndex = ClampIndex(_itemIndex, _items.Count);
            return _items[_itemIndex];
        }

        private int CurrentCharacterIndex()
        {
            if (_characters.Count == 0)
            {
                return -1;
            }

            _characterListIndex = ClampIndex(_characterListIndex, _characters.Count);
            return _characters[_characterListIndex];
        }

        private Hero CurrentHero()
        {
            int characterIndex = CurrentCharacterIndex();
            if (characterIndex < 0 || AtOManager.Instance == null)
            {
                return null;
            }

            return AtOManager.Instance.GetHero(characterIndex);
        }

        private int CurrentLinesCount()
        {
            RewardItem item = CurrentItem();
            return item != null ? item.Lines.Count : 0;
        }

        private static bool CanChooseReward(RewardsManager rewards, int characterIndex)
        {
            if (GameManager.Instance == null || !GameManager.Instance.IsMultiplayer())
            {
                return true;
            }

            string owner = RewardOwnerNick(rewards, characterIndex);
            if (string.IsNullOrWhiteSpace(owner) || NetworkManager.Instance == null)
            {
                return false;
            }

            return owner == NetworkManager.Instance.GetPlayerNick();
        }

        private static string RewardOwnerName(RewardsManager rewards, int characterIndex)
        {
            string owner = RewardOwnerNick(rewards, characterIndex);
            if (string.IsNullOrWhiteSpace(owner))
            {
                return Loc.Get("unknown_player");
            }

            if (NetworkManager.Instance != null)
            {
                string realName = NetworkManager.Instance.GetPlayerNickReal(owner);
                if (!string.IsNullOrWhiteSpace(realName))
                {
                    return Clean(realName);
                }
            }

            return Clean(owner);
        }

        private static string RewardOwnerNick(RewardsManager rewards, int characterIndex)
        {
            if (rewards == null || rewards.theTeam == null || characterIndex < 0 || characterIndex >= rewards.theTeam.Length)
            {
                return "";
            }

            Hero hero = rewards.theTeam[characterIndex];
            return hero != null ? hero.Owner : "";
        }

        private static void AddLine(RewardItem item, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                item.Lines.Add(line);
            }
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

            if (index >= count)
            {
                return count - 1;
            }

            return index;
        }

        private static string Clean(string value)
        {
            return TextCleaner.ToSpeech(value);
        }
    }
}
