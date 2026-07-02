using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides keyboard access and announcements for the hero perk tree.
    /// </summary>
    public sealed class PerkTreeHandler
    {
        private enum PerkZone
        {
            Categories,
            Perks,
            Actions
        }

        private sealed class PerkItem
        {
            public string Summary;
            public int CategoryIndex = -1;
            public PerkNode Node;
            public BotonGeneric Button;
            public PerkSlot Slot;
            public bool Exit;
            public readonly List<string> Lines = new List<string>();
        }

        private static readonly FieldInfo SubClassIdField = AccessTools.Field(typeof(PerkTree), "subClassId");
        private static float _suppressSlotSaveUntil;

        private readonly List<PerkItem> _categories = new List<PerkItem>();
        private readonly List<PerkItem> _perks = new List<PerkItem>();
        private readonly List<List<PerkItem>> _perkRows = new List<List<PerkItem>>();
        private readonly List<PerkItem> _actions = new List<PerkItem>();
        private PerkZone _zone = PerkZone.Categories;
        private int _categoryIndex;
        private int _perkRowIndex;
        private int _perkColumnIndex;
        private int _actionIndex;
        private int _lineIndex;
        private bool _announced;
        private float _lastRefreshTime;
        private int _lastCategory = -1;
        private string _lastHero = "";

        /// <summary>
        /// Updates perk tree navigation.
        /// </summary>
        public bool Update()
        {
            PerkTree tree = PerkTree.Instance;
            if (tree == null || !tree.IsActive())
            {
                Reset();
                return false;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            AccessStateManager.SetState(AccessState.PerkTree);
            int category = ActiveCategory(tree);
            string hero = ActiveHeroName(tree);
            if (category != _lastCategory || hero != _lastHero)
            {
                _lastCategory = category;
                _lastHero = hero;
                _categoryIndex = category;
                _perkRowIndex = 0;
                _perkColumnIndex = 0;
                _actionIndex = 0;
                _lineIndex = 0;
                _announced = false;
            }

            if (Time.unscaledTime - _lastRefreshTime > 0.25f)
            {
                Refresh(tree, category);
                AnnounceOnce(tree, hero);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(tree);
            return true;
        }

        private void Reset()
        {
            _categories.Clear();
            _perks.Clear();
            _perkRows.Clear();
            _actions.Clear();
            _zone = PerkZone.Categories;
            _categoryIndex = 0;
            _perkRowIndex = 0;
            _perkColumnIndex = 0;
            _actionIndex = 0;
            _lineIndex = 0;
            _announced = false;
            _lastCategory = -1;
            _lastHero = "";
        }

        private void Refresh(PerkTree tree, int category)
        {
            _categories.Clear();
            _perks.Clear();
            _perkRows.Clear();
            _actions.Clear();
            AddCategoryItems(tree);
            AddPerkNodeItems(tree, category);
            AddActionItems(tree);
            AddSlotItems(tree);
            _categoryIndex = ClampIndex(_categoryIndex, _categories.Count);
            _perkRowIndex = ClampIndex(_perkRowIndex, _perkRows.Count);
            NormalizePerkRow(1);
            _perkColumnIndex = ClampIndex(_perkColumnIndex, CurrentPerkRow().Count);
            _actionIndex = ClampIndex(_actionIndex, _actions.Count);
            _lineIndex = ClampIndex(_lineIndex, CurrentLines().Count);
            EnsureZoneHasItems();
        }

        private void EnsureZoneHasItems()
        {
            if (CurrentZoneHasItems())
            {
                return;
            }

            if (_perks.Count > 0)
            {
                _zone = PerkZone.Perks;
            }
            else if (_categories.Count > 0)
            {
                _zone = PerkZone.Categories;
            }
            else
            {
                _zone = PerkZone.Actions;
            }
        }

        private void AddCategoryItems(PerkTree tree)
        {
            if (tree.buttonType == null)
            {
                return;
            }

            int active = ActiveCategory(tree);
            for (int i = 0; i < tree.buttonType.Length; i++)
            {
                BotonGeneric button = tree.buttonType[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PerkItem item = new PerkItem();
                item.CategoryIndex = i;
                string text = ReadButtonText(button, CategoryFallback(i));
                AddLine(item, active == i ? Loc.Get("perk_category_current", text) : Loc.Get("perk_category", text));
                item.Summary = item.Lines[0];
                _categories.Add(item);
            }
        }

        private void AddPerkNodeItems(PerkTree tree, int category)
        {
            if (tree.categoryT == null || category < 0 || category >= tree.categoryT.Length || tree.categoryT[category] == null)
            {
                return;
            }

            PerkNode[] nodes = tree.categoryT[category].GetComponentsInChildren<PerkNode>(true);
            List<PerkNode> ordered = new List<PerkNode>(nodes);
            ordered.Sort(CompareNodes);
            for (int i = 0; i < ordered.Count; i++)
            {
                PerkNode node = ordered[i];
                if (node == null || node.PND == null || !node.gameObject.activeInHierarchy || !Functions.TransformIsVisible(node.transform))
                {
                    continue;
                }

                PerkItem item = BuildNodeItem(tree, node);
                _perks.Add(item);
                AddNodeToRow(item);
            }
        }

        private void AddNodeToRow(PerkItem item)
        {
            int row = item != null && item.Node != null && item.Node.PND != null ? item.Node.PND.Row : 0;
            while (_perkRows.Count <= row)
            {
                _perkRows.Add(new List<PerkItem>());
            }

            _perkRows[row].Add(item);
        }

        private static int CompareNodes(PerkNode a, PerkNode b)
        {
            int row = a.PND.Row.CompareTo(b.PND.Row);
            if (row != 0)
            {
                return row;
            }

            int column = a.PND.Column.CompareTo(b.PND.Column);
            if (column != 0)
            {
                return column;
            }

            return string.CompareOrdinal(a.PND.Id, b.PND.Id);
        }

        private PerkItem BuildNodeItem(PerkTree tree, PerkNode node)
        {
            PerkItem item = new PerkItem();
            item.Node = node;
            string name = PerkName(node);
            string status = NodeStatus(tree, node);
            AddLine(item, Loc.Get("perk_node_position", node.PND.Row + 1, node.PND.Column + 1));
            AddLine(item, Loc.Get("perk_node_summary", name, status));
            AddLine(item, PerkRequirementLine(tree, node));
            AddLine(item, Loc.Get("perk_node_cost", tree.GetPointsForNode(node.PND)));
            AddLine(item, PerkAuraBonusLine(node.PND.Perk));
            AddLine(item, Clean(node.NewPerkDescription(node.PND.Perk)));
            item.Summary = Loc.Get("perk_node_summary", name, status);
            return item;
        }

        private void AddActionItems(PerkTree tree)
        {
            AddButton(tree.buttonConfirm, Loc.Get("perk_confirm"));
            AddButton(tree.buttonReset, Loc.Get("perk_reset"));
            AddButton(tree.buttonExport, Loc.Get("perk_export"));
            AddButton(tree.buttonImport, Loc.Get("perk_import"));
            if (tree.buttonExit != null && tree.buttonExit.gameObject.activeInHierarchy && Functions.TransformIsVisible(tree.buttonExit))
            {
                BotonGeneric button = tree.buttonExit.GetComponent<BotonGeneric>();
                if (button != null)
                {
                    AddButton(button, Loc.Get("perk_exit"));
                    return;
                }

                PerkItem item = new PerkItem();
                item.Exit = true;
                AddLine(item, Loc.Get("perk_exit"));
                item.Summary = item.Lines[0];
                _actions.Add(item);
            }
        }

        private void AddButton(BotonGeneric button, string fallback)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !Functions.TransformIsVisible(button.transform))
            {
                return;
            }

            PerkItem item = new PerkItem();
            item.Button = button;
            string text = ReadButtonText(button, fallback);
            AddLine(item, button.IsEnabled() ? text : Loc.Get("menu_item_unavailable", text));
            item.Summary = item.Lines[0];
            _actions.Add(item);
        }

        private void AddSlotItems(PerkTree tree)
        {
            if (tree.perkSlot == null || tree.perkSlot.Length == 0 || tree.perkSlot[0] == null || !Functions.TransformIsVisible(tree.perkSlot[0].transform))
            {
                return;
            }

            for (int i = 0; i < tree.perkSlot.Length; i++)
            {
                PerkSlot slot = tree.perkSlot[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                BoxCollider2D collider = slot.GetComponent<BoxCollider2D>();
                bool loadable = collider != null && collider.enabled;
                string title = Clean(slot.title != null ? slot.title.text : "");
                string points = Clean(slot.cards != null && slot.cards.gameObject.activeInHierarchy ? slot.cards.text : "");
                PerkItem item = new PerkItem();
                item.Slot = loadable ? slot : null;
                AddLine(item, Loc.Get("perk_slot", i + 1, string.IsNullOrWhiteSpace(title) ? Loc.Get("empty_slot") : title));
                if (!string.IsNullOrWhiteSpace(points))
                {
                    AddLine(item, Loc.Get("perk_slot_points", points));
                }

                if (!loadable)
                {
                    AddLine(item, Loc.Get("unavailable"));
                }

                item.Summary = string.Join(" ", item.Lines.ToArray());
                _actions.Add(item);
                AddSlotButton(slot.saveButton, Loc.Get("perk_slot_save", i + 1));
                AddSlotButton(slot.deleteButton, Loc.Get("perk_slot_delete", i + 1));
            }
        }

        private void AddSlotButton(Transform buttonTransform, string fallback)
        {
            if (buttonTransform == null || !buttonTransform.gameObject.activeInHierarchy || !Functions.TransformIsVisible(buttonTransform))
            {
                return;
            }

            BotonGeneric button = buttonTransform.GetComponent<BotonGeneric>();
            AddButton(button, fallback);
        }

        private void AnnounceOnce(PerkTree tree, string hero)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
            string points = Clean(tree.availablePerksPoints != null ? tree.availablePerksPoints.text : "");
            string used = Clean(tree.usedPerksPoints != null ? tree.usedPerksPoints.text : "");
            ScreenReader.Say(Loc.Get("perk_screen", string.IsNullOrWhiteSpace(hero) ? Loc.Get("unknown_hero") : hero, points, used));
            ScreenReader.SayQueued(Loc.Get("perk_controls"));
            AnnounceFocused(true);
        }

        private void ProcessKeys(PerkTree tree)
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (ctrl && ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                ReadLine(1);
                return;
            }

            if (ctrl && ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                ReadLine(-1);
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

            if (ModInput.GetKeyDown(KeyCode.UpArrow))
            {
                if (_zone == PerkZone.Perks)
                {
                    MovePerkRow(-1);
                }
                else
                {
                    MoveZone(-1);
                }
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.DownArrow))
            {
                if (_zone == PerkZone.Perks)
                {
                    MovePerkRow(1);
                }
                else
                {
                    MoveZone(1);
                }
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter) || ModInput.GetKeyDown(KeyCode.Space))
            {
                Activate(tree);
            }
        }

        private bool MoveZone(int delta)
        {
            int start = (int)_zone;
            for (int zone = start + delta; zone >= 0 && zone < 3; zone += delta)
            {
                PerkZone candidate = (PerkZone)zone;
                if (ZoneHasItems(candidate))
                {
                    _zone = candidate;
                    _lineIndex = 0;
                    AnnounceFocused();
                    return true;
                }
            }

            return false;
        }

        private void MovePerkRow(int delta)
        {
            int next = _perkRowIndex + delta;
            while (next >= 0 && next < _perkRows.Count && _perkRows[next].Count == 0)
            {
                next += delta;
            }

            if (next >= 0 && next < _perkRows.Count)
            {
                _perkRowIndex = next;
                _perkColumnIndex = ClampIndex(_perkColumnIndex, CurrentPerkRow().Count);
                _lineIndex = 0;
                AnnounceFocused();
                return;
            }

            if (MoveZone(delta))
            {
                return;
            }

            if (_perkRows.Count == 1)
            {
                AnnounceFocused();
            }
        }

        private void NormalizePerkRow(int direction)
        {
            if (_perkRows.Count == 0 || CurrentPerkRow().Count > 0)
            {
                return;
            }

            int forward = _perkRowIndex;
            while (forward >= 0 && forward < _perkRows.Count)
            {
                if (_perkRows[forward].Count > 0)
                {
                    _perkRowIndex = forward;
                    return;
                }

                forward += direction >= 0 ? 1 : -1;
            }

            int backward = _perkRowIndex;
            while (backward >= 0 && backward < _perkRows.Count)
            {
                if (_perkRows[backward].Count > 0)
                {
                    _perkRowIndex = backward;
                    return;
                }

                backward += direction >= 0 ? -1 : 1;
            }
        }

        private bool ZoneHasItems(PerkZone zone)
        {
            PerkZone previous = _zone;
            _zone = zone;
            bool result = CurrentZoneHasItems();
            _zone = previous;
            return result;
        }

        private bool CurrentZoneHasItems()
        {
            switch (_zone)
            {
                case PerkZone.Categories:
                    return _categories.Count > 0;
                case PerkZone.Perks:
                    return _perks.Count > 0;
                case PerkZone.Actions:
                    return _actions.Count > 0;
                default:
                    return false;
            }
        }

        private void MoveItem(int delta)
        {
            switch (_zone)
            {
                case PerkZone.Categories:
                    if (!NavigationBounds.TryMove(ref _categoryIndex, delta, _categories.Count))
                    {
                        return;
                    }

                    break;
                case PerkZone.Perks:
                    if (!NavigationBounds.TryMove(ref _perkColumnIndex, delta, CurrentPerkRow().Count))
                    {
                        return;
                    }

                    break;
                case PerkZone.Actions:
                    if (!NavigationBounds.TryMove(ref _actionIndex, delta, _actions.Count))
                    {
                        return;
                    }

                    break;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void JumpItem(bool end)
        {
            switch (_zone)
            {
                case PerkZone.Categories:
                    if (!NavigationBounds.TryJump(ref _categoryIndex, end, _categories.Count))
                    {
                        return;
                    }

                    break;
                case PerkZone.Perks:
                    if (!NavigationBounds.TryJump(ref _perkColumnIndex, end, CurrentPerkRow().Count))
                    {
                        return;
                    }

                    break;
                case PerkZone.Actions:
                    if (!NavigationBounds.TryJump(ref _actionIndex, end, _actions.Count))
                    {
                        return;
                    }

                    break;
            }

            _lineIndex = 0;
            AnnounceFocused();
        }

        private void ReadLine(int delta)
        {
            List<string> lines = CurrentLines();
            if (lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("perk_no_item"));
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
                ScreenReader.Say(Loc.Get("perk_no_item"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _lineIndex, end, lines.Count))
            {
                return;
            }

            ScreenReader.Say(lines[_lineIndex]);
        }

        private void Activate(PerkTree tree)
        {
            PerkItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("perk_no_item"));
                return;
            }

            if (item.CategoryIndex >= 0)
            {
                tree.SetCategory(item.CategoryIndex);
                _categoryIndex = item.CategoryIndex;
                _perkRowIndex = 0;
                _perkColumnIndex = 0;
                _lineIndex = 0;
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                Refresh(tree, ActiveCategory(tree));
                _zone = PerkZone.Perks;
                AnnounceFocused(true);
                return;
            }

            if (item.Node != null)
            {
                ActivateNode(tree, item.Node);
                return;
            }

            if (item.Slot != null)
            {
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                tree.LoadPerkConfig(item.Slot.slot);
                return;
            }

            if (item.Button != null)
            {
                if (!item.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", item.Summary));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", item.Summary));
                item.Button.Clicked();
                return;
            }

            if (item.Exit)
            {
                ScreenReader.Say(Loc.Get("activated", item.Summary));
                tree.Hide();
            }
        }

        private void ActivateNode(PerkTree tree, PerkNode node)
        {
            if (node.PND == null || node.PND.Perk == null)
            {
                ScreenReader.Say(FocusSummary(CurrentItem()));
                return;
            }

            if (!tree.IsOwner || !tree.CanModify() || node.IsLocked() || (node.iconLock != null && node.iconLock.gameObject.activeInHierarchy))
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", PerkName(node)));
                return;
            }

            ScreenReader.Say(Loc.Get("activated", PerkName(node)));
            tree.SelectPerk(node.PND.Perk.Id, node);
            Refresh(tree, ActiveCategory(tree));
            AnnounceFocused(true);
        }

        private void AnnounceFocused(bool queued = false)
        {
            PerkItem item = CurrentItem();
            if (item == null)
            {
                ScreenReader.Say(Loc.Get("perk_no_item"));
                return;
            }

            string text = FocusSummary(item);
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private static string FocusSummary(PerkItem item)
        {
            return item != null ? item.Summary : "";
        }

        private PerkItem CurrentItem()
        {
            switch (_zone)
            {
                case PerkZone.Categories:
                    return CurrentFromList(_categories, ref _categoryIndex);
                case PerkZone.Perks:
                    return CurrentFromList(CurrentPerkRow(), ref _perkColumnIndex);
                case PerkZone.Actions:
                    return CurrentFromList(_actions, ref _actionIndex);
                default:
                    return null;
            }
        }

        private static PerkItem CurrentFromList(List<PerkItem> items, ref int index)
        {
            if (items.Count == 0)
            {
                return null;
            }

            index = ClampIndex(index, items.Count);
            return items[index];
        }

        private List<string> CurrentLines()
        {
            PerkItem item = CurrentItem();
            return item != null ? item.Lines : new List<string>();
        }

        private List<PerkItem> CurrentPerkRow()
        {
            if (_perkRows.Count == 0)
            {
                return new List<PerkItem>();
            }

            _perkRowIndex = ClampIndex(_perkRowIndex, _perkRows.Count);
            return _perkRows[_perkRowIndex];
        }

        private static int ActiveCategory(PerkTree tree)
        {
            if (tree.categoryT == null)
            {
                return 0;
            }

            for (int i = 0; i < tree.categoryT.Length; i++)
            {
                if (tree.categoryT[i] != null && tree.categoryT[i].gameObject.activeSelf)
                {
                    return i;
                }
            }

            return 0;
        }

        private static string ActiveHeroName(PerkTree tree)
        {
            string subClassId = SubClassIdField != null ? SubClassIdField.GetValue(tree) as string : "";
            if (string.IsNullOrWhiteSpace(subClassId) || Globals.Instance == null)
            {
                return "";
            }

            SubClassData data = Globals.Instance.GetSubClassData(subClassId);
            return data != null ? Clean(data.CharacterName) : Clean(subClassId);
        }

        private static string NodeStatus(PerkTree tree, PerkNode node)
        {
            if (node.IsSelected())
            {
                return Loc.Get("perk_selected");
            }

            if (node.IsLocked() || (node.iconLock != null && node.iconLock.gameObject.activeInHierarchy))
            {
                return Loc.Get("locked");
            }

            if (!tree.CanModify() || !tree.IsOwner)
            {
                return Loc.Get("perk_read_only");
            }

            return tree.GetPointsAvailable() >= tree.GetPointsForNode(node.PND) ? Loc.Get("available") : Loc.Get("perk_not_enough_points");
        }

        private static string PerkName(PerkNode node)
        {
            if (node == null || node.PND == null)
            {
                return Loc.Get("perk_unknown");
            }

            PerkData perk = node.PND.Perk;
            if (perk == null)
            {
                return Loc.Get("perk_choose_one");
            }

            string auraBonus = PerkAuraBonusLine(perk);
            if (!string.IsNullOrWhiteSpace(auraBonus))
            {
                return auraBonus;
            }

            string description = Clean(node.NewPerkDescription(perk));
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(perk.Id) ? node.PND.Id : perk.Id;
        }

        private static string PerkRequirementLine(PerkTree tree, PerkNode node)
        {
            if (tree == null || node == null || node.PND == null)
            {
                return "";
            }

            int required = tree.GetPointsNeeded(node.PND.Row);
            return Loc.Get("perk_group_requirement", required);
        }

        private static string PerkAuraBonusLine(PerkData perk)
        {
            if (perk == null || perk.AuracurseBonus == null || perk.AuracurseBonusValue == 0)
            {
                return "";
            }

            string name = Clean(AccessTheObelisk.GameText.AuraCurseName(perk.AuracurseBonus));
            if (string.IsNullOrWhiteSpace(name))
            {
                return "";
            }

            return Loc.Get("perk_aura_bonus", name, perk.AuracurseBonusValue);
        }

        private static string CategoryFallback(int index)
        {
            switch (index)
            {
                case 0:
                    return GameText("general");
                case 1:
                    return GameText("physical");
                case 2:
                    return GameText("elemental");
                case 3:
                    return GameText("mystical");
                default:
                    return Loc.Get("perk_category_fallback", index + 1);
            }
        }

        private static string ReadButtonText(BotonGeneric button, string fallback)
        {
            string text = button != null ? Clean(button.GetText()) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string GameText(string id)
        {
            return Texts.Instance != null ? Texts.Instance.GetText(id) : id;
        }

        private static void AddLine(PerkItem item, string line)
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

        internal static void SuppressSlotSaveBriefly()
        {
            _suppressSlotSaveUntil = Time.unscaledTime + 0.5f;
        }

        internal static bool ShouldSuppressSlotSave()
        {
            return _suppressSlotSaveUntil >= Time.unscaledTime;
        }
    }

    [HarmonyPatch(typeof(PerkTree), nameof(PerkTree.RemovePerkSlot))]
    internal static class PerkTreeRemovePerkSlotActionPatch
    {
        private static void Postfix()
        {
            PerkTreeHandler.SuppressSlotSaveBriefly();
        }
    }

    [HarmonyPatch(typeof(PerkTree), nameof(PerkTree.SavePerkSlot))]
    internal static class PerkTreeSavePerkSlotPatch
    {
        private static bool Prefix()
        {
            return !PerkTreeHandler.ShouldSuppressSlotSave();
        }
    }
}
