using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access and announcements for the town upgrades window.
    /// </summary>
    public sealed class TownUpgradeHandler
    {
        private sealed class UpgradeItem
        {
            public string Summary;
            public BotonSupply Supply;
            public BotonGeneric Button;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<UpgradeItem> _items = new List<UpgradeItem>();
        private int _index;
        private int _lineIndex;
        private bool _announced;
        private bool _lastSellMode;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates town upgrades navigation.
        /// </summary>
        public bool Update()
        {
            TownManager town = TownManager.Instance;
            TownUpgradeWindow window = town != null ? town.townUpgradeWindow : null;
            if (town == null || window == null || !town.gameObject.activeInHierarchy || !window.IsActive())
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.Town);
            bool sellMode = IsSellMode(window);
            if (sellMode != _lastSellMode)
            {
                _lastSellMode = sellMode;
                _index = 0;
                _lineIndex = 0;
                _announced = false;
            }

            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(window);
                AnnounceOnce(window);
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
            _lastSellMode = false;
        }

        private void Refresh(TownUpgradeWindow window)
        {
            _items.Clear();
            if (IsSellMode(window))
            {
                AddSellSupplyItems(window);
            }
            else
            {
                AddSupplyItems(window);
                AddButton(window.sellSupplyButton, Loc.Get("town_upgrade_sell_supply"));
                AddButton(window.exitButton, Loc.Get("town_upgrade_exit"));
            }

            _index = ClampIndex(_index, _items.Count);
            _lineIndex = ClampIndex(_lineIndex, CurrentLines().Count);
        }

        private void AddSupplyItems(TownUpgradeWindow window)
        {
            if (window.botonSupply == null)
            {
                return;
            }

            for (int i = 0; i < window.botonSupply.Count; i++)
            {
                BotonSupply supply = window.botonSupply[i];
                if (supply == null || !supply.gameObject.activeInHierarchy || !Functions.TransformIsVisible(supply.transform))
                {
                    continue;
                }

                UpgradeItem item = BuildSupplyItem(supply);
                _items.Add(item);
            }
        }

        private UpgradeItem BuildSupplyItem(BotonSupply supply)
        {
            UpgradeItem item = new UpgradeItem();
            item.Supply = supply;
            string name = SupplyText(supply);
            string status = SupplyStatus(supply);
            AddLine(item, Loc.Get("town_upgrade_position", supply.column, supply.row));
            AddLine(item, name);
            AddLine(item, status);
            AddLine(item, Loc.Get("town_upgrade_cost", PlayerManager.Instance.PointsRequiredForSupply(supply.supplyId)));
            AddRequirementLines(item, supply);
            item.Summary = Loc.Get("town_upgrade_summary", name, status);
            return item;
        }

        private static void AddRequirementLines(UpgradeItem item, BotonSupply supply)
        {
            string required = PlayerManager.Instance.SupplyRequiredForSupply(supply.supplyId);
            if (!string.IsNullOrWhiteSpace(required) && !PlayerManager.Instance.PlayerHaveSupply(required))
            {
                AddLine(item, Loc.Get("town_upgrade_requires", Clean(GameText(required))));
            }

            int spent = PlayerManager.Instance.TotalPointsSpentInSupplys();
            if (supply.row > 3 && spent < 30)
            {
                AddLine(item, Loc.Get("town_upgrade_requires_spent", 30 - spent));
            }

            int available = PlayerManager.Instance.GetPlayerSupplyActual();
            int cost = PlayerManager.Instance.PointsRequiredForSupply(supply.supplyId);
            if (!PlayerManager.Instance.PlayerHaveSupply(supply.supplyId) && available < cost)
            {
                AddLine(item, Loc.Get("town_upgrade_needs_supply", cost - available));
            }
        }

        private void AddSellSupplyItems(TownUpgradeWindow window)
        {
            AddLineHeader(window);
            if (window.sellSupplyTransforms == null)
            {
                return;
            }

            for (int i = 0; i < window.sellSupplyTransforms.Count; i++)
            {
                Transform transform = window.sellSupplyTransforms[i];
                AddButton(transform, ReadButtonText(transform, Loc.Get("menu_item", transform != null ? transform.name : "")));
            }
        }

        private void AddLineHeader(TownUpgradeWindow window)
        {
            UpgradeItem item = new UpgradeItem();
            AddLine(item, Loc.Get("town_upgrade_sell_supply"));
            AddLine(item, Loc.Get("town_upgrade_sell_quantity", Clean(window.supplySellQuantity != null ? window.supplySellQuantity.text : "0")));
            AddLine(item, Loc.Get("town_upgrade_sell_result", Clean(window.supplySellResult != null ? window.supplySellResult.text : "")));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AddButton(Transform transform, string fallback)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || !Functions.TransformIsVisible(transform))
            {
                return;
            }

            AddButton(transform.GetComponent<BotonGeneric>(), fallback);
        }

        private void AddButton(BotonGeneric button, string fallback)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            UpgradeItem item = new UpgradeItem();
            item.Button = button;
            AddLine(item, ReadButtonText(button.transform, fallback));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _items.Add(item);
        }

        private void AnnounceOnce(TownUpgradeWindow window)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            if (IsSellMode(window))
            {
                ScreenReader.Say(Loc.Get("town_upgrade_sell_screen"));
            }
            else
            {
                int supply = PlayerManager.Instance.GetPlayerSupplyActual();
                int spent = PlayerManager.Instance.TotalPointsSpentInSupplys();
                ScreenReader.Say(Loc.Get("town_upgrade_screen", supply, spent));
                if (window.requiredTM != null && window.requiredTM.gameObject.activeInHierarchy)
                {
                    ScreenReader.SayQueued(Clean(window.requiredTM.text));
                }
            }

            AnnounceFocusedItem(true);
        }

        private void ProcessKeys()
        {
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                ReadLine(-1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                ReadLine(1);
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

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate();
            }
        }

        private void Move(int delta)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
                return;
            }

            if (!NavigationBounds.TryMove(ref _index, delta, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void Jump(bool end)
        {
            if (_items.Count == 0)
            {
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _index, end, _items.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void ReadLine(int delta)
        {
            List<string> lines = CurrentLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
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
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void AnnounceFocusedItem(bool queued = false)
        {
            UpgradeItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
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

        private void Activate()
        {
            UpgradeItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("town_upgrade_no_item"));
                return;
            }

            if (item.Supply != null)
            {
                ActivateSupply(item.Supply, item.Summary);
                return;
            }

            if (item.Button != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                item.Button.Clicked();
            }
        }

        private static void ActivateSupply(BotonSupply supply, string summary)
        {
            if (PlayerManager.Instance.PlayerHaveSupply(supply.supplyId))
            {
                ScreenReader.Say(Loc.Get("town_upgrade_already_bought"));
                return;
            }

            if (!supply.available)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", summary));
                return;
            }

            ScreenReader.Say(Loc.Get("activated", summary));
            AlertManager.buttonClickDelegate = supply.BuySupply;
            AlertManager.Instance.AlertConfirmDouble(Texts.Instance.GetText("townAssignWarning"));
        }

        private static bool IsSellMode(TownUpgradeWindow window)
        {
            return window.sellSupplyT != null && Functions.TransformIsVisible(window.sellSupplyT);
        }

        private UpgradeItem CurrentItem()
        {
            if (_index < 0 || _index >= _items.Count)
            {
                return null;
            }

            return _items[_index];
        }

        private List<string> CurrentLines()
        {
            UpgradeItem item = CurrentItem();
            return item != null ? item.Lines : new List<string>();
        }

        private static string SupplyStatus(BotonSupply supply)
        {
            if (PlayerManager.Instance.PlayerHaveSupply(supply.supplyId))
            {
                return Loc.Get("town_upgrade_bought");
            }

            return supply.available ? Loc.Get("available") : Loc.Get("locked");
        }

        private static string SupplyText(BotonSupply supply)
        {
            string text = GameText(supply.supplyId);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return Clean(text);
            }

            TMP_Text childText = supply.GetComponentInChildren<TMP_Text>();
            return childText != null ? Clean(childText.text) : supply.supplyId;
        }

        private static string GameText(string id)
        {
            if (Texts.Instance == null || string.IsNullOrWhiteSpace(id))
            {
                return "";
            }

            return Texts.Instance.GetText(id);
        }

        private static string ReadButtonText(Transform transform, string fallback)
        {
            if (transform == null)
            {
                return fallback;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            string text = button != null ? Clean(button.GetText()) : "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            TMP_Text childText = transform.GetComponentInChildren<TMP_Text>();
            text = childText != null ? Clean(childText.text) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static void AddLine(UpgradeItem item, string line)
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

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }
    }
}
