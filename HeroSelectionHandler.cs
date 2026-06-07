using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides structured keyboard access for the hero selection screen.
    /// </summary>
    public sealed class HeroSelectionHandler
    {
        private enum SelectionZone
        {
            Party,
            Heroes,
            Actions
        }

        private sealed class HeroAction
        {
            public string Summary;
            public BotonGeneric Button;
            public Transform Transform;
            public bool Begin;
            public bool Ready;
        }

        private sealed class DetailBuffer
        {
            public string Name;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<BoxSelection> _partySlots = new List<BoxSelection>();
        private readonly List<HeroSelection> _heroes = new List<HeroSelection>();
        private readonly List<HeroAction> _actions = new List<HeroAction>();
        private readonly List<DetailBuffer> _detailBuffers = new List<DetailBuffer>();
        private SelectionZone _zone = SelectionZone.Party;
        private int _partyIndex;
        private int _heroIndex;
        private int _actionIndex;
        private int _targetSlot;
        private int _detailBufferIndex;
        private int _detailLineIndex;
        private string _lastFocusKey;
        private bool _screenAnnounced;
        private bool _waitForSubmitRelease;
        private float _ignoreInputUntil;
        private float _lastRefreshTime;

        /// <summary>
        /// Updates hero selection navigation and activation.
        /// </summary>
        public void Update()
        {
            HeroSelectionManager manager = HeroSelectionManager.Instance;
            if (manager == null || manager.allGO == null || !manager.allGO.activeInHierarchy)
            {
                Reset();
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsTutorialActive())
            {
                return;
            }

            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return;
            }

            AccessStateManager.SetState(AccessState.HeroSelection);
            AnnounceScreenOnce();
            if (IsFirstAdventureAutoStart())
            {
                return;
            }

            if (Time.unscaledTime - _lastRefreshTime > 0.2f)
            {
                Refresh(manager);
                _lastRefreshTime = Time.unscaledTime;
            }

            ProcessKeys(manager);
        }

        private void Reset()
        {
            _partySlots.Clear();
            _heroes.Clear();
            _actions.Clear();
            _zone = SelectionZone.Party;
            _partyIndex = 0;
            _heroIndex = 0;
            _actionIndex = 0;
            _targetSlot = 0;
            ResetDetailBuffer();
            _lastFocusKey = null;
            _screenAnnounced = false;
            _waitForSubmitRelease = false;
            _ignoreInputUntil = 0f;
        }

        private void AnnounceScreenOnce()
        {
            if (_screenAnnounced)
            {
                return;
            }

            _screenAnnounced = true;
            _ignoreInputUntil = Time.unscaledTime + 0.45f;
            _waitForSubmitRelease = IsSubmitHeld();

            if (IsFirstAdventureAutoStart())
            {
                ScreenReader.Say(Loc.Get("first_adventure_auto_start"));
                return;
            }

            ScreenReader.Say(Loc.Get("hero_selection_screen_structured"));
            ScreenReader.SayQueued(Loc.Get("hero_selection_controls"));
            Refresh(HeroSelectionManager.Instance);
            AnnounceFocused(true);
        }

        private void Refresh(HeroSelectionManager manager)
        {
            BuildPartySlots(manager);
            BuildHeroList(manager);
            BuildActions(manager);
            _partyIndex = ClampIndex(_partyIndex, _partySlots.Count);
            _heroIndex = ClampIndex(_heroIndex, _heroes.Count);
            _actionIndex = ClampIndex(_actionIndex, _actions.Count);
            _targetSlot = ClampIndex(_targetSlot, _partySlots.Count);
            if (_zone == SelectionZone.Party && _partySlots.Count == 0)
            {
                _zone = _heroes.Count > 0 ? SelectionZone.Heroes : SelectionZone.Actions;
            }
            else if (_zone == SelectionZone.Heroes && _heroes.Count == 0)
            {
                _zone = _partySlots.Count > 0 ? SelectionZone.Party : SelectionZone.Actions;
            }
            else if (_zone == SelectionZone.Actions && _actions.Count == 0)
            {
                _zone = _heroes.Count > 0 ? SelectionZone.Heroes : SelectionZone.Party;
            }
        }

        private void BuildPartySlots(HeroSelectionManager manager)
        {
            _partySlots.Clear();
            if (manager.boxGO == null)
            {
                return;
            }

            for (int i = 0; i < manager.boxGO.Length; i++)
            {
                GameObject box = manager.boxGO[i];
                BoxSelection slot = box != null ? box.GetComponent<BoxSelection>() : null;
                if (slot != null && slot.gameObject.activeInHierarchy && Functions.TransformIsVisible(slot.transform))
                {
                    _partySlots.Add(slot);
                }
            }
        }

        private void BuildHeroList(HeroSelectionManager manager)
        {
            _heroes.Clear();
            foreach (KeyValuePair<string, HeroSelection> pair in manager.heroSelectionDictionary)
            {
                HeroSelection hero = pair.Value;
                if (hero == null || hero.HeroPicked || !hero.gameObject.activeInHierarchy || !Functions.TransformIsVisible(hero.transform))
                {
                    continue;
                }

                _heroes.Add(hero);
            }

            _heroes.Sort(CompareHeroes);
        }

        private static int CompareHeroes(HeroSelection left, HeroSelection right)
        {
            Vector3 a = left.transform.position;
            Vector3 b = right.transform.position;
            int row = b.y.CompareTo(a.y);
            if (row != 0)
            {
                return row;
            }

            int column = a.x.CompareTo(b.x);
            if (column != 0)
            {
                return column;
            }

            return string.CompareOrdinal(left.Id, right.Id);
        }

        private void BuildActions(HeroSelectionManager manager)
        {
            _actions.Clear();
            AddActionButton(manager.beginAdventureButton, Loc.Get("hero_selection_begin"), begin: true);
            AddActionButton(manager.readyButton, Loc.Get("hero_selection_ready"), ready: true);
            AddActionButton(manager.madnessButton, Loc.Get("hero_selection_madness"));
            AddActionButton(manager.sandboxButton, Loc.Get("hero_selection_sandbox"));
            AddActionButton(manager.gameSeed, CurrentSeedText(manager));
            AddActionButton(manager.gameSeedModify, Loc.Get("hero_selection_seed_modify"));
            AddActionButton(manager.weeklyModifiersButton, Loc.Get("hero_selection_weekly"));
            AddActionButton(manager.botonFollow != null ? manager.botonFollow.transform : null, Loc.Get("hero_selection_follow"));
            if (manager.menuController != null)
            {
                for (int i = 0; i < manager.menuController.Count; i++)
                {
                    AddActionButton(manager.menuController[i], Loc.Get("menu_item", manager.menuController[i] != null ? manager.menuController[i].name : ""));
                }
            }

            AddBackLikeButtons(manager);
        }

        private void AddActionButton(Transform transform, string fallback, bool begin = false, bool ready = false)
        {
            if (transform == null || !transform.gameObject.activeInHierarchy || !Functions.TransformIsVisible(transform))
            {
                return;
            }

            if (transform.GetComponentInParent<HeroSelection>() != null ||
                transform.GetComponentInChildren<HeroSelection>() != null ||
                transform.GetComponentInParent<BoxSelection>() != null ||
                transform.GetComponentInChildren<BoxSelection>() != null)
            {
                return;
            }

            BotonGeneric button = transform.GetComponent<BotonGeneric>();
            string text = ReadButtonText(transform, fallback);
            if (ContainsAction(transform))
            {
                return;
            }

            HeroAction action = new HeroAction();
            action.Transform = transform;
            action.Button = button;
            action.Begin = begin;
            action.Ready = ready;
            action.Summary = text;
            _actions.Add(action);
        }

        private void AddBackLikeButtons(HeroSelectionManager manager)
        {
            if (manager == null || manager.allGO == null)
            {
                return;
            }

            BotonGeneric[] buttons = manager.allGO.GetComponentsInChildren<BotonGeneric>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                BotonGeneric button = buttons[i];
                if (button == null || !IsBackLikeButton(button))
                {
                    continue;
                }

                AddActionButton(button.transform, ReadButtonText(button.transform, button.gameObject.name));
            }
        }

        private bool ContainsAction(Transform transform)
        {
            for (int i = 0; i < _actions.Count; i++)
            {
                if (_actions[i].Transform == transform)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBackLikeButton(BotonGeneric button)
        {
            string probe = ((button.gameObject.name ?? "") + " " + (button.idTranslate ?? "") + " " + (button.auxString ?? "")).ToLowerInvariant();
            return probe.Contains("mainmenu") ||
                probe.Contains("back") ||
                probe.Contains("return") ||
                probe.Contains("close") ||
                probe.Contains("exit") ||
                probe.Contains("cancel");
        }

        private void ProcessKeys(HeroSelectionManager manager)
        {
            if (Time.unscaledTime < _ignoreInputUntil)
            {
                return;
            }

            if (_waitForSubmitRelease)
            {
                if (IsSubmitHeld())
                {
                    return;
                }

                _waitForSubmitRelease = false;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (shift && _zone == SelectionZone.Actions && IsCurrentMadnessAction(manager) &&
                (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                AdjustMadnessFromHeroSelection(manager, Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1);
                return;
            }

            if (shift && _zone == SelectionZone.Party && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                AdjustPartySlotOwner(manager, Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1);
                return;
            }

            if (ctrl && GameEventBuffer.IsFocused && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            {
                LeaveEventBufferToHeroDetails(manager, Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveDetailBuffer(manager, -1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveDetailBuffer(manager, 1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.UpArrow))
            {
                ReadDetailLine(manager, 1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.DownArrow))
            {
                ReadDetailLine(manager, -1);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.Home))
            {
                JumpDetailLine(manager, false);
                return;
            }

            if (ctrl && Input.GetKeyDown(KeyCode.End))
            {
                JumpDetailLine(manager, true);
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

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                MoveZone(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                MoveZone(1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveItem(-1);
                return;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                MoveItem(1);
                return;
            }

            if (ctrl && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) && (_zone == SelectionZone.Heroes || _zone == SelectionZone.Party))
            {
                OpenFocusedHeroPerks(manager);
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                Activate(manager);
            }
        }

        private void MoveZone(int delta)
        {
            int start = (int)_zone;
            for (int zone = start + delta; zone >= 0 && zone < 3; zone += delta)
            {
                SelectionZone candidate = (SelectionZone)zone;
                if (ZoneHasItems(candidate))
                {
                    _zone = candidate;
                    AnnounceFocused();
                    return;
                }
            }

            ScreenReader.Say(Loc.Get("no_menu_item"));
        }

        private bool ZoneHasItems(SelectionZone zone)
        {
            SelectionZone previous = _zone;
            _zone = zone;
            bool result = CurrentZoneHasItems();
            _zone = previous;
            return result;
        }

        private bool CurrentZoneHasItems()
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    return _partySlots.Count > 0;
                case SelectionZone.Heroes:
                    return _heroes.Count > 0;
                case SelectionZone.Actions:
                    return _actions.Count > 0;
                default:
                    return false;
            }
        }

        private void MoveItem(int delta)
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    if (!NavigationBounds.TryMove(ref _partyIndex, delta, _partySlots.Count))
                    {
                        return;
                    }

                    _targetSlot = _partyIndex;
                    break;
                case SelectionZone.Heroes:
                    if (!NavigationBounds.TryMove(ref _heroIndex, delta, _heroes.Count))
                    {
                        return;
                    }

                    break;
                case SelectionZone.Actions:
                    if (!NavigationBounds.TryMove(ref _actionIndex, delta, _actions.Count))
                    {
                        return;
                    }

                    break;
            }

            AnnounceFocused();
            ResetDetailBuffer();
        }

        private void JumpItem(bool end)
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    if (!NavigationBounds.TryJump(ref _partyIndex, end, _partySlots.Count))
                    {
                        return;
                    }

                    _targetSlot = _partyIndex;
                    break;
                case SelectionZone.Heroes:
                    if (!NavigationBounds.TryJump(ref _heroIndex, end, _heroes.Count))
                    {
                        return;
                    }

                    break;
                case SelectionZone.Actions:
                    if (!NavigationBounds.TryJump(ref _actionIndex, end, _actions.Count))
                    {
                        return;
                    }

                    break;
            }

            AnnounceFocused();
            ResetDetailBuffer();
        }

        private void Activate(HeroSelectionManager manager)
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    ActivatePartySlot(manager);
                    break;
                case SelectionZone.Heroes:
                    ActivateHero(manager);
                    break;
                case SelectionZone.Actions:
                    ActivateAction(manager);
                    break;
            }
        }

        private void ActivatePartySlot(HeroSelectionManager manager)
        {
            BoxSelection slot = CurrentPartySlot();
            if (slot == null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_slot"));
                return;
            }

            _targetSlot = _partyIndex;
            HeroSelection assigned = manager.GetBoxHeroFromIndex(slot.GetId());
            if (assigned != null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_slot_target_with_hero", slot.GetId() + 1, GetHeroText(assigned)));
            }
            else
            {
                ScreenReader.Say(Loc.Get("hero_selection_slot_target", slot.GetId() + 1));
            }
        }

        private void AdjustPartySlotOwner(HeroSelectionManager manager, int delta)
        {
            if (manager == null || !GameManager.Instance.IsMultiplayer())
            {
                ScreenReader.Say(Loc.Get("hero_selection_owner_single_player"));
                return;
            }

            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsMaster())
            {
                ScreenReader.Say(Loc.Get("hero_selection_owner_master_only"));
                return;
            }

            BoxSelection slot = CurrentPartySlot();
            if (slot == null || NetworkManager.Instance.PlayerList == null || NetworkManager.Instance.PlayerList.Length == 0)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_slot"));
                return;
            }

            int currentIndex = 0;
            string currentOwner = slot.GetOwner();
            for (int i = 0; i < NetworkManager.Instance.PlayerList.Length; i++)
            {
                if (NetworkManager.Instance.PlayerList[i] != null && NetworkManager.Instance.PlayerList[i].NickName == currentOwner)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = currentIndex + delta;
            if (nextIndex < 0)
            {
                nextIndex = NetworkManager.Instance.PlayerList.Length - 1;
            }
            else if (nextIndex >= NetworkManager.Instance.PlayerList.Length)
            {
                nextIndex = 0;
            }

            string nextOwner = NetworkManager.Instance.PlayerList[nextIndex].NickName;
            manager.AssignPlayerToBox(nextOwner, slot.GetId());
            Refresh(manager);
            ScreenReader.Say(Loc.Get("hero_selection_owner_assigned", slot.GetId() + 1, NetworkManager.Instance.GetPlayerNickReal(nextOwner)));
            AnnounceFocused(true);
        }

        private void ActivateHero(HeroSelectionManager manager)
        {
            HeroSelection hero = CurrentHero();
            if (hero == null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_hero"));
                return;
            }

            if (hero.blocked || hero.DlcBlocked)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", GetHeroText(hero)));
                return;
            }

            int slot = FindTargetSlot(manager);
            if (slot < 0)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_slot"));
                return;
            }

            ScreenReader.Say(Loc.Get("hero_selection_assigning", GetHeroText(hero), slot + 1));
            hero.PickHero(_comingFromRandom: true);
            hero.PickStop(slot);
            _targetSlot = ClampIndex(slot + 1, _partySlots.Count);
            _zone = SelectionZone.Party;
            Refresh(manager);
            AnnounceFocused(true);
        }

        private void OpenFocusedHeroPerks(HeroSelectionManager manager)
        {
            HeroSelection hero = _zone == SelectionZone.Party ? CurrentPartySlotHero(manager) : CurrentHero();
            if (hero == null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_hero"));
                return;
            }

            if (hero.blocked || hero.DlcBlocked)
            {
                ScreenReader.Say(Loc.Get("menu_item_unavailable", GetHeroText(hero)));
                return;
            }

            ScreenReader.Say(Loc.Get("hero_selection_open_perks", GetHeroText(hero)));
            PerkTree.Instance.Show(hero.Id);
        }

        private BoxSelection CurrentPartySlot()
        {
            if (_partySlots.Count == 0)
            {
                return null;
            }

            _partyIndex = ClampIndex(_partyIndex, _partySlots.Count);
            return _partySlots[_partyIndex];
        }

        private HeroSelection CurrentPartySlotHero(HeroSelectionManager manager)
        {
            BoxSelection slot = CurrentPartySlot();
            if (manager == null || slot == null)
            {
                return null;
            }

            return manager.GetBoxHeroFromIndex(slot.GetId());
        }

        private HeroSelection FocusedHero(HeroSelectionManager manager)
        {
            return _zone == SelectionZone.Party ? CurrentPartySlotHero(manager) : CurrentHero();
        }

        private void MoveDetailBuffer(HeroSelectionManager manager, int delta)
        {
            if (!BuildDetailBuffers(manager))
            {
                return;
            }

            if (delta > 0 && _detailBufferIndex >= _detailBuffers.Count - 1)
            {
                GameEventBuffer.FocusLatest();
                return;
            }

            if (!NavigationBounds.TryMove(ref _detailBufferIndex, delta, _detailBuffers.Count))
            {
                return;
            }

            _detailLineIndex = -1;
            DetailBuffer buffer = CurrentDetailBuffer();
            ScreenReader.Say(buffer != null ? Loc.Get("hero_selection_detail_buffer", buffer.Name) : Loc.Get("hero_selection_no_details"));
        }

        private void LeaveEventBufferToHeroDetails(HeroSelectionManager manager, int delta)
        {
            GameEventBuffer.LeaveFocus(false);
            if (!BuildDetailBuffers(manager))
            {
                return;
            }

            _detailBufferIndex = delta < 0 ? _detailBuffers.Count - 1 : 0;
            _detailLineIndex = -1;
            DetailBuffer buffer = CurrentDetailBuffer();
            ScreenReader.Say(buffer != null ? Loc.Get("hero_selection_detail_buffer", buffer.Name) : Loc.Get("hero_selection_no_details"));
        }

        private void ReadDetailLine(HeroSelectionManager manager, int delta)
        {
            if (!BuildDetailBuffers(manager))
            {
                return;
            }

            DetailBuffer buffer = CurrentDetailBuffer();
            if (buffer == null || buffer.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_details"));
                return;
            }

            int previous = _detailLineIndex;
            if (_detailLineIndex < 0)
            {
                _detailLineIndex = 0;
            }
            else if (!NavigationBounds.TryMove(ref _detailLineIndex, delta, buffer.Lines.Count))
            {
                return;
            }

            if (_detailLineIndex == previous && buffer.Lines.Count > 1)
            {
                return;
            }

            ScreenReader.Say(buffer.Lines[_detailLineIndex]);
        }

        private void JumpDetailLine(HeroSelectionManager manager, bool end)
        {
            if (!BuildDetailBuffers(manager))
            {
                return;
            }

            DetailBuffer buffer = CurrentDetailBuffer();
            if (buffer == null || buffer.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_details"));
                return;
            }

            if (_detailLineIndex < 0)
            {
                _detailLineIndex = end ? buffer.Lines.Count - 1 : 0;
            }
            else if (!NavigationBounds.TryJump(ref _detailLineIndex, end, buffer.Lines.Count))
            {
                return;
            }

            ScreenReader.Say(buffer.Lines[_detailLineIndex]);
        }

        private DetailBuffer CurrentDetailBuffer()
        {
            if (_detailBuffers.Count == 0)
            {
                return null;
            }

            _detailBufferIndex = ClampIndex(_detailBufferIndex, _detailBuffers.Count);
            return _detailBuffers[_detailBufferIndex];
        }

        private bool BuildDetailBuffers(HeroSelectionManager manager)
        {
            HeroSelection hero = FocusedHero(manager);
            if (hero == null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_hero"));
                return false;
            }

            SubClassData data = GetHeroSubClassData(hero);
            if (data == null)
            {
                ScreenReader.Say(Loc.Get("hero_selection_no_details"));
                return false;
            }

            _detailBuffers.Clear();
            AddOverviewBuffer(hero, data);
            AddTraitsBuffer(data);
            AddCardsBuffer(data);
            _detailBufferIndex = ClampIndex(_detailBufferIndex, _detailBuffers.Count);
            DetailBuffer buffer = CurrentDetailBuffer();
            if (buffer == null || buffer.Lines.Count == 0)
            {
                _detailLineIndex = -1;
            }
            else if (_detailLineIndex >= buffer.Lines.Count)
            {
                _detailLineIndex = buffer.Lines.Count - 1;
            }
            return _detailBuffers.Count > 0;
        }

        private void AddOverviewBuffer(HeroSelection hero, SubClassData data)
        {
            DetailBuffer buffer = NewBuffer(Loc.Get("hero_selection_buffer_overview"));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_name", data.CharacterName));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_class", hero.GetHeroClass()));
            string secondary = hero.GetHeroClassSecondary();
            if (!string.IsNullOrWhiteSpace(secondary) && secondary != "None")
            {
                AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_secondary_class", secondary));
            }

            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_health", AdjustedHealth(data)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_energy", AdjustedEnergy(data), data.EnergyTurn));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_speed", AdjustedSpeed(data)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_strength", data.CharacterDescriptionStrength));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_description", data.CharacterDescription));
            AddBuffer(buffer);
        }

        private void AddTraitsBuffer(SubClassData data)
        {
            DetailBuffer buffer = NewBuffer(Loc.Get("hero_selection_buffer_traits"));
            AddTraitLine(buffer.Lines, data.Trait0);
            AddTraitLine(buffer.Lines, data.Trait1A);
            AddTraitLine(buffer.Lines, data.Trait1B);
            AddTraitLine(buffer.Lines, data.Trait2A);
            AddTraitLine(buffer.Lines, data.Trait2B);
            AddTraitLine(buffer.Lines, data.Trait3A);
            AddTraitLine(buffer.Lines, data.Trait3B);
            AddTraitLine(buffer.Lines, data.Trait4A);
            AddTraitLine(buffer.Lines, data.Trait4B);
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_slashing"), AdjustedResist(data, Enums.DamageType.Slashing)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_blunt"), AdjustedResist(data, Enums.DamageType.Blunt)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_piercing"), AdjustedResist(data, Enums.DamageType.Piercing)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_fire"), AdjustedResist(data, Enums.DamageType.Fire)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_cold"), AdjustedResist(data, Enums.DamageType.Cold)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_lightning"), AdjustedResist(data, Enums.DamageType.Lightning)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_mind"), AdjustedResist(data, Enums.DamageType.Mind)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_holy"), AdjustedResist(data, Enums.DamageType.Holy)));
            AddCleanPart(buffer.Lines, Loc.Get("hero_selection_detail_resist", Loc.Get("damage_shadow"), AdjustedResist(data, Enums.DamageType.Shadow)));
            AddBuffer(buffer);
        }

        private void AddCardsBuffer(SubClassData data)
        {
            DetailBuffer buffer = NewBuffer(Loc.Get("hero_selection_buffer_cards"));
            CardData item = StartingItem(data);
            if (item != null)
            {
                AddCleanPart(buffer.Lines, Loc.Get("hero_selection_starting_item", CardSpeech.BuildItemFocusSummary(item)));
                AddLines(buffer.Lines, CardSpeech.BuildItemEffectLines(item));
            }

            HeroCards[] cards = data.Cards;
            if (cards != null)
            {
                int tier = PlayerManager.Instance != null ? PlayerManager.Instance.GetCharacterTier(data.Id, "card") : 0;
                for (int i = 0; i < cards.Length; i++)
                {
                    HeroCards heroCard = cards[i];
                    if (heroCard == null || heroCard.Card == null || heroCard.UnitsInDeck <= 0)
                    {
                        continue;
                    }

                    CardData card = StartingCard(heroCard.Card, tier);
                    int energy = card != null ? card.EnergyCost : 0;
                    AddCleanPart(buffer.Lines, Loc.Get("hero_selection_starting_card", heroCard.UnitsInDeck, CardSpeech.BuildCardFocusSummary(card, energy)));
                    AddLines(buffer.Lines, CardSpeech.BuildCardLines(card, energy));
                }
            }

            AddBuffer(buffer);
        }

        private int FindTargetSlot(HeroSelectionManager manager)
        {
            if (_partySlots.Count == 0)
            {
                return -1;
            }

            int preferred = _partySlots[ClampIndex(_targetSlot, _partySlots.Count)].GetId();
            if (manager.IsYourBox("Box_" + preferred))
            {
                return preferred;
            }

            for (int i = 0; i < _partySlots.Count; i++)
            {
                int id = _partySlots[i].GetId();
                if (manager.IsYourBox("Box_" + id))
                {
                    return id;
                }
            }

            return -1;
        }

        private void ActivateAction(HeroSelectionManager manager)
        {
            HeroAction action = CurrentAction();
            if (action == null)
            {
                ScreenReader.Say(Loc.Get("no_menu_item"));
                return;
            }

            if (action.Begin)
            {
                BotonGeneric begin = manager.botonBegin;
                if (begin == null || !begin.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", action.Summary));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated_loading", action.Summary));
                manager.BeginAdventure();
                return;
            }

            if (action.Ready)
            {
                ScreenReader.Say(Loc.Get("activated", action.Summary));
                manager.Ready();
                return;
            }

            if (action.Transform == manager.gameSeedModify)
            {
                ScreenReader.Say(Loc.Get("activated", action.Summary));
                manager.ChangeSeed();
                return;
            }

            if (action.Button != null)
            {
                if (!action.Button.IsEnabled())
                {
                    ScreenReader.Say(Loc.Get("menu_item_unavailable", action.Summary));
                    return;
                }

                ScreenReader.Say(Loc.Get("activated", action.Summary));
                action.Button.Clicked();
                return;
            }

            action.Transform.SendMessage("OnMouseUp", SendMessageOptions.DontRequireReceiver);
            ScreenReader.Say(Loc.Get("activated", action.Summary));
        }

        private void AnnounceFocused(bool queued = false)
        {
            string key = FocusKey();
            string text = FocusText();
            if (string.IsNullOrWhiteSpace(text))
            {
                text = Loc.Get("no_menu_item");
            }

            if (key == _lastFocusKey && !queued)
            {
                return;
            }

            _lastFocusKey = key;
            ResetDetailBuffer();
            if (queued)
            {
                ScreenReader.SayQueued(text);
            }
            else
            {
                ScreenReader.Say(text);
            }
        }

        private string FocusKey()
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    return "party:" + _partyIndex;
                case SelectionZone.Heroes:
                    HeroSelection hero = CurrentHero();
                    return "hero:" + (hero != null ? hero.Id : _heroIndex.ToString());
                case SelectionZone.Actions:
                    return "action:" + _actionIndex;
                default:
                    return "";
            }
        }

        private string FocusText()
        {
            switch (_zone)
            {
                case SelectionZone.Party:
                    return PartyFocusText();
                case SelectionZone.Heroes:
                    return HeroFocusText();
                case SelectionZone.Actions:
                    return ActionFocusText();
                default:
                    return "";
            }
        }

        private string PartyFocusText()
        {
            BoxSelection slot = CurrentPartySlot();
            if (slot == null)
            {
                return Loc.Get("hero_selection_no_slot");
            }

            HeroSelection assigned = HeroSelectionManager.Instance.GetBoxHeroFromIndex(slot.GetId());
            string hero = assigned != null ? GetHeroText(assigned) : Loc.Get("empty_slot");
            string owner = Clean(slot.playerOwner != null ? slot.playerOwner.text : "");
            List<string> states = new List<string>();
            if (GameManager.Instance.IsMultiplayer() && !string.IsNullOrWhiteSpace(slot.GetOwner()) && NetworkManager.Instance != null)
            {
                states.Add(Loc.Get(NetworkManager.Instance.IsPlayerReady(slot.GetOwner()) ? "hero_selection_owner_ready" : "hero_selection_owner_not_ready"));
            }

            if (slot.GetId() == _targetSlot)
            {
                states.Add(Loc.Get("hero_selection_current_target"));
            }

            return Loc.Get("hero_selection_party_focus", slot.GetId() + 1, hero, owner, string.Join(" ", states.ToArray()));
        }

        private string HeroFocusText()
        {
            HeroSelection hero = CurrentHero();
            if (hero == null)
            {
                return Loc.Get("hero_selection_no_hero");
            }

            return Loc.Get("hero_selection_hero_focus", GetHeroText(hero, true), _targetSlot + 1);
        }

        private string ActionFocusText()
        {
            HeroAction action = CurrentAction();
            if (action == null)
            {
                return Loc.Get("no_menu_item");
            }

            if (action.Button != null && !action.Button.IsEnabled())
            {
                return Loc.Get("hero_selection_action_focus", Loc.Get("menu_item_unavailable", action.Summary));
            }

            if (action.Begin && (HeroSelectionManager.Instance.botonBegin == null || !HeroSelectionManager.Instance.botonBegin.IsEnabled()))
            {
                return Loc.Get("hero_selection_action_focus", Loc.Get("menu_item_unavailable", action.Summary));
            }

            return Loc.Get("hero_selection_action_focus", action.Summary);
        }

        private static string CurrentSeedText(HeroSelectionManager manager)
        {
            string seed = manager != null && manager.gameSeedTxt != null ? Clean(manager.gameSeedTxt.text) : "";
            return string.IsNullOrWhiteSpace(seed) ? Loc.Get("hero_selection_seed") : Loc.Get("hero_selection_seed_value", seed);
        }

        private bool IsCurrentMadnessAction(HeroSelectionManager manager)
        {
            HeroAction action = CurrentAction();
            return manager != null && action != null && action.Transform == manager.madnessButton;
        }

        private void AdjustMadnessFromHeroSelection(HeroSelectionManager manager, int delta)
        {
            if (manager == null || MadnessManager.Instance == null || PlayerManager.Instance == null || GameManager.Instance == null)
            {
                ScreenReader.Say(Loc.Get("pre_run_options_unavailable"));
                return;
            }

            int current = CurrentMadnessValue(manager);
            int max = MaxMadnessValue();
            int next = Mathf.Clamp(current + delta, 0, max);
            if (next == current)
            {
                ScreenReader.Say(Loc.Get("pre_run_madness_edge", current));
                return;
            }

            ApplyMadnessValue(manager, next);
            ScreenReader.Say(BuildMadnessSummary(next));
            _lastFocusKey = null;
        }

        private static int CurrentMadnessValue(HeroSelectionManager manager)
        {
            if (GameManager.Instance.IsGameAdventure())
            {
                return manager.NgValue;
            }

            if (GameManager.Instance.IsSingularity())
            {
                return manager.SingularityMadnessValue;
            }

            return manager.ObeliskMadnessValue;
        }

        private static int MaxMadnessValue()
        {
            if (GameManager.Instance.IsGameAdventure())
            {
                return PlayerManager.Instance.NgLevel;
            }

            if (GameManager.Instance.IsSingularity())
            {
                return PlayerManager.Instance.SingularityMadnessLevel;
            }

            return PlayerManager.Instance.ObeliskMadnessLevel;
        }

        private static void ApplyMadnessValue(HeroSelectionManager manager, int value)
        {
            if (GameManager.Instance.IsGameAdventure())
            {
                manager.NgValue = value;
                manager.NgValueMaster = value;
                SaveManager.SaveIntoPrefsInt("madnessLevel", value);
                SaveManager.SaveIntoPrefsString("madnessCorruptors", manager.NgCorruptors);
                manager.SetMadnessLevel();
            }
            else if (GameManager.Instance.IsSingularity())
            {
                manager.SingularityMadnessValue = value;
                manager.SingularityMadnessValueMaster = value;
                SaveManager.SaveIntoPrefsInt("singularityMadness", value);
                manager.SetSingularityMadnessLevel();
            }
            else
            {
                manager.ObeliskMadnessValue = value;
                manager.ObeliskMadnessValueMaster = value;
                SaveManager.SaveIntoPrefsInt("obeliskMadness", value);
                manager.SetObeliskMadnessLevel();
            }

            SaveManager.SavePrefs();
        }

        private static string BuildMadnessSummary(int value)
        {
            string details = MadnessManager.Instance != null ? Clean(Functions.GetMadnessBonusText(value)) : "";
            return string.IsNullOrWhiteSpace(details)
                ? Loc.Get("pre_run_madness_value", value)
                : Loc.Get("pre_run_madness_value_details", value, details);
        }

        private HeroSelection CurrentHero()
        {
            if (_heroes.Count == 0)
            {
                return null;
            }

            _heroIndex = ClampIndex(_heroIndex, _heroes.Count);
            return _heroes[_heroIndex];
        }

        private HeroAction CurrentAction()
        {
            if (_actions.Count == 0)
            {
                return null;
            }

            _actionIndex = ClampIndex(_actionIndex, _actions.Count);
            return _actions[_actionIndex];
        }

        private static DetailBuffer NewBuffer(string name)
        {
            DetailBuffer buffer = new DetailBuffer();
            buffer.Name = name;
            return buffer;
        }

        private void AddBuffer(DetailBuffer buffer)
        {
            if (buffer != null && buffer.Lines.Count > 0)
            {
                _detailBuffers.Add(buffer);
            }
        }

        private static void AddLines(List<string> target, List<string> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                AddCleanPart(target, source[i]);
            }
        }

        private static void AddTraitLine(List<string> lines, TraitData trait)
        {
            if (trait == null)
            {
                return;
            }

            string name = Clean(trait.TraitName);
            string description = Clean(trait.Description);
            if (string.IsNullOrWhiteSpace(description))
            {
                AddCleanPart(lines, name);
                return;
            }

            AddCleanPart(lines, Loc.Get("hero_selection_trait_line", name, description));
        }

        private static CardData StartingCard(CardData card, int tier)
        {
            if (card == null || !card.Starter || Globals.Instance == null)
            {
                return card;
            }

            if (tier == 1 && !string.IsNullOrWhiteSpace(card.UpgradesTo1))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo1.ToLower(), instantiate: false) ?? card;
            }

            if (tier == 2 && !string.IsNullOrWhiteSpace(card.UpgradesTo2))
            {
                return Globals.Instance.GetCardData(card.UpgradesTo2.ToLower(), instantiate: false) ?? card;
            }

            return card;
        }

        private static CardData StartingItem(SubClassData data)
        {
            if (data == null || data.Item == null || Globals.Instance == null)
            {
                return data != null ? data.Item : null;
            }

            string id = data.Item.Id;
            int tier = PlayerManager.Instance != null ? PlayerManager.Instance.GetCharacterTier(data.Id, "item") : 0;
            if (tier == 1 && !string.IsNullOrWhiteSpace(data.Item.UpgradesTo1))
            {
                id = data.Item.UpgradesTo1;
            }
            else if (tier == 2 && !string.IsNullOrWhiteSpace(data.Item.UpgradesTo2))
            {
                id = data.Item.UpgradesTo2;
            }

            return Globals.Instance.GetCardData(id, instantiate: false) ?? data.Item;
        }

        private static int AdjustedHealth(SubClassData data)
        {
            int value = data.Hp;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkMaxHealth(data.Id);
            }

            return value;
        }

        private static int AdjustedEnergy(SubClassData data)
        {
            int value = data.Energy;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkEnergyBegin(data.Id);
            }

            return value;
        }

        private static int AdjustedSpeed(SubClassData data)
        {
            int value = data.Speed;
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkSpeed(data.Id);
            }

            return value;
        }

        private static int AdjustedResist(SubClassData data, Enums.DamageType type)
        {
            int value = BaseResist(data, type);
            if (!IsObeliskChallenge() && PlayerManager.Instance != null)
            {
                value += PlayerManager.Instance.GetPerkResistBonus(data.Id, type);
            }

            return value;
        }

        private static int BaseResist(SubClassData data, Enums.DamageType type)
        {
            switch (type)
            {
                case Enums.DamageType.Slashing:
                    return data.ResistSlashing;
                case Enums.DamageType.Blunt:
                    return data.ResistBlunt;
                case Enums.DamageType.Piercing:
                    return data.ResistPiercing;
                case Enums.DamageType.Fire:
                    return data.ResistFire;
                case Enums.DamageType.Cold:
                    return data.ResistCold;
                case Enums.DamageType.Lightning:
                    return data.ResistLightning;
                case Enums.DamageType.Mind:
                    return data.ResistMind;
                case Enums.DamageType.Holy:
                    return data.ResistHoly;
                case Enums.DamageType.Shadow:
                    return data.ResistShadow;
                default:
                    return 0;
            }
        }

        private static bool IsObeliskChallenge()
        {
            return GameManager.Instance != null && GameManager.Instance.IsObeliskChallenge();
        }

        private void ResetDetailBuffer()
        {
            _detailBuffers.Clear();
            _detailBufferIndex = 0;
            _detailLineIndex = -1;
        }

        private static bool IsSubmitHeld()
        {
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter) || Input.GetKey(KeyCode.Space);
        }

        private static bool IsFirstAdventureAutoStart()
        {
            return GameManager.Instance != null &&
                AtOManager.Instance != null &&
                GameManager.Instance.IsGameAdventure() &&
                AtOManager.Instance.IsFirstGame() &&
                !GameManager.Instance.IsMultiplayer();
        }

        private static string GetHeroText(HeroSelection hero, bool includeDetails = false)
        {
            List<string> parts = new List<string>();
            AddPart(parts, hero.nameTM);

            string heroClass = hero.GetHeroClass();
            if (!string.IsNullOrWhiteSpace(heroClass) && heroClass != "None")
            {
                parts.Add(heroClass);
            }

            string secondary = hero.GetHeroClassSecondary();
            if (!string.IsNullOrWhiteSpace(secondary) && secondary != "None" && secondary != heroClass)
            {
                parts.Add(secondary);
            }

            AddPart(parts, hero.rankTM);
            if (hero.perkPointsT != null && hero.perkPointsT.gameObject.activeInHierarchy && hero.perkPoints != null)
            {
                parts.Add(Loc.Get("hero_selection_perk_points", Clean(hero.perkPoints.text)));
            }

            if (includeDetails)
            {
                AddHeroDescriptionParts(parts, hero);
            }

            if (hero.blocked || hero.DlcBlocked)
            {
                parts.Add(Loc.Get("locked"));
            }

            return string.Join(". ", parts.ToArray());
        }

        private static void AddHeroDescriptionParts(List<string> parts, HeroSelection hero)
        {
            SubClassData data = GetHeroSubClassData(hero);
            if (data == null)
            {
                return;
            }

            AddCleanPart(parts, Loc.Get("hero_selection_strength", data.CharacterDescriptionStrength));
            AddCleanPart(parts, Loc.Get("hero_selection_description", data.CharacterDescription));
        }

        private static SubClassData GetHeroSubClassData(HeroSelection hero)
        {
            if (hero == null || Globals.Instance == null || string.IsNullOrWhiteSpace(hero.Id))
            {
                return null;
            }

            return Globals.Instance.GetSubClassData(hero.Id);
        }

        private static string ReadButtonText(Transform transform, string fallback)
        {
            BotonGeneric button = transform != null ? transform.GetComponent<BotonGeneric>() : null;
            string text = button != null ? Clean(button.GetText()) : "";
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            if (button != null && Texts.Instance != null && !string.IsNullOrWhiteSpace(button.idTranslate))
            {
                text = Clean(Texts.Instance.GetText(button.idTranslate));
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            TMP_Text childText = transform != null ? transform.GetComponentInChildren<TMP_Text>(true) : null;
            text = childText != null ? Clean(childText.text) : "";
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static void AddPart(List<string> parts, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            AddCleanPart(parts, text.text);
        }

        private static void AddCleanPart(List<string> parts, string text)
        {
            string clean = Clean(text);
            if (!string.IsNullOrWhiteSpace(clean) && !ContainsPart(parts, clean))
            {
                parts.Add(clean);
            }
        }

        private static bool ContainsPart(List<string> parts, string candidate)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == candidate)
                {
                    return true;
                }
            }

            return false;
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
