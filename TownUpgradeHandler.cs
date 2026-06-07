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
        private enum UpgradeZone
        {
            Upgrades,
            Actions
        }

        private sealed class UpgradeItem
        {
            public string Summary;
            public BotonSupply Supply;
            public BotonGeneric Button;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<List<UpgradeItem>> _upgradeColumns = new List<List<UpgradeItem>>();
        private readonly List<UpgradeItem> _actions = new List<UpgradeItem>();
        private readonly List<UpgradeItem> _sellItems = new List<UpgradeItem>();
        private UpgradeZone _zone = UpgradeZone.Upgrades;
        private int _columnIndex;
        private int _rowIndex;
        private int _actionIndex;
        private int _sellIndex;
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
                _zone = UpgradeZone.Upgrades;
                _columnIndex = 0;
                _rowIndex = 0;
                _actionIndex = 0;
                _sellIndex = 0;
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
            _upgradeColumns.Clear();
            _actions.Clear();
            _sellItems.Clear();
            _zone = UpgradeZone.Upgrades;
            _columnIndex = 0;
            _rowIndex = 0;
            _actionIndex = 0;
            _sellIndex = 0;
            _lineIndex = 0;
            _announced = false;
            _lastSellMode = false;
        }

        private void Refresh(TownUpgradeWindow window)
        {
            _upgradeColumns.Clear();
            _actions.Clear();
            _sellItems.Clear();
            if (IsSellMode(window))
            {
                AddSellSupplyItems(window);
                _sellIndex = ClampIndex(_sellIndex, _sellItems.Count);
            }
            else
            {
                AddSupplyItems(window);
                AddAction(window.sellSupplyButton, Loc.Get("town_upgrade_sell_supply"));
                AddAction(window.exitButton, Loc.Get("town_upgrade_exit"));
                _columnIndex = ClampIndex(_columnIndex, _upgradeColumns.Count);
                NormalizeColumn(1);
                _rowIndex = ClampIndex(_rowIndex, CurrentColumn().Count);
                _actionIndex = ClampIndex(_actionIndex, _actions.Count);
                EnsureZoneHasItems();
            }

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
                while (_upgradeColumns.Count < supply.column)
                {
                    _upgradeColumns.Add(new List<UpgradeItem>());
                }

                _upgradeColumns[supply.column - 1].Add(item);
            }

            for (int i = 0; i < _upgradeColumns.Count; i++)
            {
                _upgradeColumns[i].Sort(CompareSupplyRows);
            }
        }

        private static int CompareSupplyRows(UpgradeItem a, UpgradeItem b)
        {
            int row = a.Supply.row.CompareTo(b.Supply.row);
            if (row != 0)
            {
                return row;
            }

            return string.CompareOrdinal(a.Supply.supplyId, b.Supply.supplyId);
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
            _sellItems.Add(item);
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
            _sellItems.Add(item);
        }

        private void AddAction(Transform transform, string fallback)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || !Functions.TransformIsVisible(transform))
            {
                return;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            if (button == null)
            {
                return;
            }

            UpgradeItem item = new UpgradeItem();
            item.Button = button;
            AddLine(item, ReadButtonText(button.transform, fallback));
            item.Summary = string.Join(" ", item.Lines.ToArray());
            _actions.Add(item);
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
                JumpItem(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                JumpItem(true);
                return;
            }

            if (_lastSellMode)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow))
                {
                    MoveSellItem(-1);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    MoveSellItem(1);
                    return;
                }
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveGroup(-1);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveGroup(1);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Activate();
            }
        }

        private void MoveGroup(int delta)
        {
            if (_zone == UpgradeZone.Actions)
            {
                if (delta < 0 && HasUpgradeItems())
                {
                    _zone = UpgradeZone.Upgrades;
                    NormalizeColumn(-1);
                    _rowIndex = ClampIndex(_rowIndex, CurrentColumn().Count);
                    _lineIndex = 0;
                    AnnounceFocusedItem();
                }

                return;
            }

            int next = _columnIndex + delta;
            while (next >= 0 && next < _upgradeColumns.Count && _upgradeColumns[next].Count == 0)
            {
                next += delta;
            }

            if (next >= 0 && next < _upgradeColumns.Count)
            {
                _columnIndex = next;
                _rowIndex = ClampIndex(_rowIndex, CurrentColumn().Count);
                _lineIndex = 0;
                AnnounceFocusedItem();
                return;
            }

            if (delta > 0 && _actions.Count > 0)
            {
                _zone = UpgradeZone.Actions;
                _lineIndex = 0;
                AnnounceFocusedItem();
            }
        }

        private void MoveItem(int delta)
        {
            List<UpgradeItem> items = CurrentGroup();
            int index = _zone == UpgradeZone.Upgrades ? _rowIndex : _actionIndex;
            if (!NavigationBounds.TryMove(ref index, delta, items.Count))
            {
                return;
            }

            if (_zone == UpgradeZone.Upgrades)
            {
                _rowIndex = index;
            }
            else
            {
                _actionIndex = index;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void MoveSellItem(int delta)
        {
            if (!NavigationBounds.TryMove(ref _sellIndex, delta, _sellItems.Count))
            {
                return;
            }

            _lineIndex = 0;
            AnnounceFocusedItem();
        }

        private void JumpItem(bool end)
        {
            int index;
            int count;
            if (_lastSellMode)
            {
                index = _sellIndex;
                count = _sellItems.Count;
            }
            else if (_zone == UpgradeZone.Upgrades)
            {
                index = _rowIndex;
                count = CurrentColumn().Count;
            }
            else
            {
                index = _actionIndex;
                count = _actions.Count;
            }

            if (!NavigationBounds.TryJump(ref index, end, count))
            {
                return;
            }

            if (_lastSellMode)
            {
                _sellIndex = index;
            }
            else if (_zone == UpgradeZone.Upgrades)
            {
                _rowIndex = index;
            }
            else
            {
                _actionIndex = index;
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
            if (_lastSellMode)
            {
                return CurrentFromList(_sellItems, ref _sellIndex);
            }

            if (_zone == UpgradeZone.Upgrades)
            {
                List<UpgradeItem> column = CurrentColumn();
                return CurrentFromList(column, ref _rowIndex);
            }

            return CurrentFromList(_actions, ref _actionIndex);
        }

        private List<string> CurrentLines()
        {
            UpgradeItem item = CurrentItem();
            return item != null ? item.Lines : new List<string>();
        }

        private List<UpgradeItem> CurrentColumn()
        {
            if (_upgradeColumns.Count == 0)
            {
                return new List<UpgradeItem>();
            }

            _columnIndex = ClampIndex(_columnIndex, _upgradeColumns.Count);
            return _upgradeColumns[_columnIndex];
        }

        private List<UpgradeItem> CurrentGroup()
        {
            return _zone == UpgradeZone.Upgrades ? CurrentColumn() : _actions;
        }

        private static UpgradeItem CurrentFromList(List<UpgradeItem> items, ref int index)
        {
            if (items.Count == 0)
            {
                return null;
            }

            index = ClampIndex(index, items.Count);
            return items[index];
        }

        private bool HasUpgradeItems()
        {
            for (int i = 0; i < _upgradeColumns.Count; i++)
            {
                if (_upgradeColumns[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void NormalizeColumn(int direction)
        {
            if (_upgradeColumns.Count == 0 || CurrentColumn().Count > 0)
            {
                return;
            }

            int next = _columnIndex;
            while (next >= 0 && next < _upgradeColumns.Count)
            {
                if (_upgradeColumns[next].Count > 0)
                {
                    _columnIndex = next;
                    return;
                }

                next += direction >= 0 ? 1 : -1;
            }

            next = _columnIndex;
            while (next >= 0 && next < _upgradeColumns.Count)
            {
                if (_upgradeColumns[next].Count > 0)
                {
                    _columnIndex = next;
                    return;
                }

                next += direction >= 0 ? -1 : 1;
            }
        }

        private void EnsureZoneHasItems()
        {
            if (_zone == UpgradeZone.Upgrades && HasUpgradeItems())
            {
                return;
            }

            if (_zone == UpgradeZone.Actions && _actions.Count > 0)
            {
                return;
            }

            _zone = HasUpgradeItems() ? UpgradeZone.Upgrades : UpgradeZone.Actions;
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
