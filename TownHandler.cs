using System.Collections.Generic;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access and announcements for the town hub.
    /// </summary>
    public sealed class TownHandler
    {
        private sealed class TownItem
        {
            public string Summary;
            public TownBuilding Building;
            public BotonGeneric Button;
            public TreasureRun Treasure;
            public bool TownUpgrades;
        }

        private readonly List<TownItem> _items = new List<TownItem>();
        private int _index;
        private bool _announced;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates town hub navigation.
        /// </summary>
        public bool Update()
        {
            TownManager town = TownManager.Instance;
            if (town == null || !town.gameObject.activeInHierarchy)
            {
                Reset();
                return false;
            }

            if (CardCraftManager.Instance != null && CardCraftManager.Instance.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Town);
            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(town);
                AnnounceTownOnce(town);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys();
            return true;
        }

        private void Reset()
        {
            _items.Clear();
            _index = 0;
            _announced = false;
        }

        private void Refresh(TownManager town)
        {
            _items.Clear();
            AddBuilding(town.buildingForge);
            AddBuilding(town.buildingAltar);
            AddBuilding(town.buildingChurch);
            AddBuilding(town.buildingCart);
            AddBuilding(town.buildingArmory);
            AddTownUpgrades(town);
            AddButton(town.townReady, Loc.Get("town_ready"));
            AddTreasures(town.treasureItems, Loc.Get("town_reward"));
            AddTreasures(town.treasureItemsCommunity, Loc.Get("town_community_reward"));
            _index = ClampIndex(_index, _items.Count);
        }

        private void AddBuilding(TownBuilding building)
        {
            if (building == null || !building.gameObject.activeInHierarchy || !Functions.TransformIsVisible(building.transform))
            {
                return;
            }

            TownItem item = new TownItem();
            item.Building = building;
            item.Summary = BuildBuildingText(building);
            _items.Add(item);
        }

        private void AddTownUpgrades(TownManager town)
        {
            if (town.townUpgrades == null || !town.townUpgrades.gameObject.activeInHierarchy || !Functions.TransformIsVisible(town.townUpgrades))
            {
                return;
            }

            TownItem item = new TownItem();
            item.TownUpgrades = true;
            item.Summary = Loc.Get("town_upgrades");
            _items.Add(item);
        }

        private void AddButton(BotonGeneric button, string fallback)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            TownItem item = new TownItem();
            item.Button = button;
            string text = Clean(button.GetText());
            item.Summary = string.IsNullOrWhiteSpace(text) ? fallback : text;
            _items.Add(item);
        }

        private void AddTreasures(TreasureRun[] treasures, string prefix)
        {
            if (treasures == null)
            {
                return;
            }

            for (int i = 0; i < treasures.Length; i++)
            {
                TreasureRun treasure = treasures[i];
                if (treasure == null || treasure.claimed || !treasure.gameObject.activeInHierarchy || !Functions.TransformIsVisible(treasure.transform))
                {
                    continue;
                }

                TownItem item = new TownItem();
                item.Treasure = treasure;
                item.Summary = Loc.Get("town_reward_text", prefix, Clean(treasure.qText != null ? treasure.qText.text : ""));
                _items.Add(item);
            }
        }

        private static string BuildBuildingText(TownBuilding building)
        {
            string title = "";
            string description = "";
            if (Texts.Instance != null)
            {
                title = Texts.Instance.GetText(building.idTitle);
                description = Texts.Instance.GetText(building.idDescription);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = building.idTitle;
            }

            return string.IsNullOrWhiteSpace(description) ? Clean(title) : Loc.Get("town_building", Clean(title), Clean(description));
        }

        private void AnnounceTownOnce(TownManager town)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            ScreenReader.Say(Loc.Get("town_screen"));
            if (town.tutorialBanner != null && town.tutorialBanner.gameObject.activeInHierarchy && town.tutorialBannerText != null)
            {
                ScreenReader.SayQueued(Clean(town.tutorialBannerText.text));
            }

            AnnounceFocusedItem(true);
        }

        private void ProcessKeys()
        {
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

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate();
            }
        }

        private void Move(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("town_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            AnnounceFocusedItem();
        }

        private void Jump(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("town_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            AnnounceFocusedItem();
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            TownItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("town_no_item"));
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

        private void Activate()
        {
            TownItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("town_no_item"));
                return;
            }

            ScreenReader.Say(Loc.Get("activated", item.Summary));
            if (item.Building != null)
            {
                ActivateBuilding(item.Building);
            }
            else if (item.Button != null)
            {
                item.Button.Clicked();
            }
            else if (item.Treasure != null && item.Treasure.bGeneric != null)
            {
                item.Treasure.bGeneric.Clicked();
            }
            else if (item.TownUpgrades && TownManager.Instance != null)
            {
                TownManager.Instance.ShowTownUpgrades(true);
            }
        }

        private static void ActivateBuilding(TownBuilding building)
        {
            if (AtOManager.Instance.TownTutorialStep > -1 && AtOManager.Instance.TownTutorialStep < 3)
            {
                if ((building.idTitle == "craftCards" && AtOManager.Instance.TownTutorialStep != 0) ||
                    (building.idTitle == "upgradeCards" && AtOManager.Instance.TownTutorialStep != 1) ||
                    (building.idTitle == "buyItems" && AtOManager.Instance.TownTutorialStep != 2) ||
                    (building.idTitle != "craftCards" && building.idTitle != "upgradeCards" && building.idTitle != "buyItems"))
                {
                    AlertManager.Instance.AlertConfirm(Texts.Instance.GetText("tutorialTownNeedComplete"));
                    return;
                }
            }

            if (building.idTitle == "craftCards")
            {
                AtOManager.Instance.DoCardCraft();
            }
            else if (building.idTitle == "upgradeCards")
            {
                AtOManager.Instance.DoCardUpgrade();
            }
            else if (building.idTitle == "removeCards")
            {
                AtOManager.Instance.DoCardHealer();
            }
            else if (building.idTitle == "divinationCards")
            {
                AtOManager.Instance.DoCardDivination();
            }
            else if (building.idTitle == "buyItems")
            {
                AtOManager.Instance.DoItemShop("");
            }
        }

        private TownItem CurrentItem()
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

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
