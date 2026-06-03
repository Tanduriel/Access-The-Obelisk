using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AccessTheObelisk
{
    /// <summary>
    /// Provides global read-only access to quest/event requirements and tracked quest items.
    /// </summary>
    public sealed class QuestRequirementHandler
    {
        private enum RequirementFilter
        {
            All,
            Items,
            Tracks
        }

        private sealed class RequirementEntry
        {
            public EventRequirementData Data;
            public bool ItemTrack;
            public bool RequirementTrack;
            public bool VisibleOnCurrentMap;
            public readonly List<string> Lines = new List<string>();
        }

        private readonly List<RequirementEntry> _entries = new List<RequirementEntry>();
        private RequirementFilter _filter;
        private int _index = -1;
        private string _lastSignature;

        /// <summary>
        /// Updates global quest item and requirement tracker hotkeys.
        /// </summary>
        public bool Update()
        {
            if (IsBlocked())
            {
                return false;
            }

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift)
            {
                return false;
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow))
            {
                ChangeFilter(-1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                ChangeFilter(1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                Move(-1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(1);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.Home))
            {
                Jump(false);
                return true;
            }

            if (Input.GetKeyDown(KeyCode.End))
            {
                Jump(true);
                return true;
            }

            return false;
        }

        private static bool IsBlocked()
        {
            if (AlertManager.Instance != null && AlertManager.Instance.IsActive())
            {
                return true;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsTutorialActive())
            {
                return true;
            }

            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }

            TMP_InputField input = EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>();
            return input != null && input.isFocused;
        }

        private void ChangeFilter(int delta)
        {
            int next = (int)_filter + delta;
            if (next < 0)
            {
                next = 0;
            }
            else if (next > (int)RequirementFilter.Tracks)
            {
                next = (int)RequirementFilter.Tracks;
            }

            _filter = (RequirementFilter)next;
            _index = -1;
            Rebuild();
            ScreenReader.Say(Loc.Get("requirements_filter", FilterName(_filter), _entries.Count));
        }

        private void Move(int delta)
        {
            Rebuild();
            if (_entries.Count == 0)
            {
                ScreenReader.Say(Loc.Get("requirements_empty"));
                return;
            }

            if (_index < 0)
            {
                _index = delta < 0 ? _entries.Count - 1 : 0;
            }
            else
            {
                if (!NavigationBounds.TryMove(ref _index, delta, _entries.Count))
                {
                    return;
                }
            }

            RequirementEntry entry = _entries[_index];
            if (entry.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("requirements_empty"));
                return;
            }

            ScreenReader.Say(Loc.Get("requirements_position", _index + 1, _entries.Count, string.Join(" ", entry.Lines.ToArray())));
        }

        private void Jump(bool end)
        {
            Rebuild();
            if (_entries.Count == 0)
            {
                ScreenReader.Say(Loc.Get("requirements_empty"));
                return;
            }

            if (_index < 0)
            {
                _index = end ? _entries.Count - 1 : 0;
            }
            else if (!NavigationBounds.TryJump(ref _index, end, _entries.Count))
            {
                return;
            }

            RequirementEntry entry = _entries[_index];
            if (entry.Lines.Count == 0)
            {
                ScreenReader.Say(Loc.Get("requirements_empty"));
                return;
            }

            ScreenReader.Say(Loc.Get("requirements_position", _index + 1, _entries.Count, string.Join(" ", entry.Lines.ToArray())));
        }

        private void Rebuild()
        {
            List<string> requirementIds = AtOManager.Instance != null ? AtOManager.Instance.GetPlayerRequeriments() : null;
            string signature = BuildSignature(requirementIds);
            if (signature == _lastSignature)
            {
                return;
            }

            _lastSignature = signature;
            _entries.Clear();
            if (requirementIds == null || Globals.Instance == null)
            {
                _index = -1;
                return;
            }

            for (int i = 0; i < requirementIds.Count; i++)
            {
                EventRequirementData data = Globals.Instance.GetRequirementData(requirementIds[i]);
                if (data == null)
                {
                    continue;
                }

                bool itemTrack = data.ItemTrack;
                bool requirementTrack = data.RequirementTrack;
                if (!itemTrack && !requirementTrack)
                {
                    continue;
                }

                if (_filter == RequirementFilter.Items && !itemTrack)
                {
                    continue;
                }

                if (_filter == RequirementFilter.Tracks && !requirementTrack)
                {
                    continue;
                }

                _entries.Add(BuildEntry(data, itemTrack, requirementTrack));
            }

            _index = ClampIndex(_index, _entries.Count);
        }

        private RequirementEntry BuildEntry(EventRequirementData data, bool itemTrack, bool requirementTrack)
        {
            RequirementEntry entry = new RequirementEntry();
            entry.Data = data;
            entry.ItemTrack = itemTrack;
            entry.RequirementTrack = requirementTrack;
            entry.VisibleOnCurrentMap = requirementTrack && CanShowOnCurrentMap(data);

            AddLine(entry, Loc.Get("requirements_entry", RequirementName(data), KindText(entry)));
            AddLine(entry, RequirementDescription(data));
            if (entry.VisibleOnCurrentMap)
            {
                AddLine(entry, Loc.Get("requirements_visible_on_map"));
            }
            else if (entry.RequirementTrack)
            {
                AddLine(entry, Loc.Get("requirements_not_visible_on_map"));
            }

            return entry;
        }

        private static bool CanShowOnCurrentMap(EventRequirementData data)
        {
            if (data == null || AtOManager.Instance == null)
            {
                return false;
            }

            string currentNode = AtOManager.Instance.currentMapNode;
            if (string.IsNullOrWhiteSpace(currentNode))
            {
                return false;
            }

            return data.CanShowRequeriment(AtOManager.Instance.GetMapZone(currentNode), Enums.Zone.None);
        }

        private string BuildSignature(List<string> requirementIds)
        {
            string currentNode = AtOManager.Instance != null ? AtOManager.Instance.currentMapNode : "";
            if (requirementIds == null)
            {
                return ((int)_filter).ToString() + ":" + currentNode;
            }

            return ((int)_filter).ToString() + ":" + currentNode + ":" + string.Join(",", requirementIds.ToArray());
        }

        private static string KindText(RequirementEntry entry)
        {
            if (entry.ItemTrack && entry.RequirementTrack)
            {
                return Loc.Get("requirements_kind_item_and_track");
            }

            if (entry.ItemTrack)
            {
                return Loc.Get("requirements_kind_item");
            }

            return Loc.Get("requirements_kind_track");
        }

        private static string FilterName(RequirementFilter filter)
        {
            switch (filter)
            {
                case RequirementFilter.Items:
                    return Loc.Get("requirements_filter_items");
                case RequirementFilter.Tracks:
                    return Loc.Get("requirements_filter_tracks");
                default:
                    return Loc.Get("requirements_filter_all");
            }
        }

        private static void AddLine(RequirementEntry entry, string line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                entry.Lines.Add(line);
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

        private static string RequirementName(EventRequirementData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.RequirementId + "_name", "requirements");
            return Clean(string.IsNullOrWhiteSpace(text) ? data.RequirementName : text);
        }

        private static string RequirementDescription(EventRequirementData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.RequirementId + "_description", "requirements");
            return Clean(string.IsNullOrWhiteSpace(text) ? data.Description : text);
        }
    }
}
