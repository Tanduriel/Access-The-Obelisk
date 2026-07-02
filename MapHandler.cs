using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

using Cards;
using Cards.Data;
namespace AccessTheObelisk
{
    /// <summary>
    /// Announces and activates the existing map navigation.
    /// </summary>
    public sealed class MapHandler
    {
        private static readonly FieldInfo RoadsField = AccessTools.Field(typeof(MapManager), "roads");
        private readonly List<Node> _availableNodes = new List<Node>();
        private readonly List<Node> _mapExplorerPath = new List<Node>();
        private readonly List<Node> _mapExplorerChildren = new List<Node>();
        private readonly Dictionary<Node, LogicalCoordinate> _logicalCoordinates = new Dictionary<Node, LogicalCoordinate>();
        private string _lastMapNode;
        private string _lastAnnouncement;
        private bool _mapAnnounced;
        private int _focusedDestinationIndex;
        private Node _mapExplorerNode;
        private Node _mapExplorerParent;
        private int _mapExplorerChildIndex;
        private float _lastPollTime;
        private float _suppressFocusUntil;

        /// <summary>
        /// Updates map focus announcements and keyboard activation.
        /// </summary>
        public void Update()
        {
            MapManager map = MapManager.Instance;
            if (map == null)
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

            if (EventManager.Instance != null || map.IsMaskActive() || map.selectedNode)
            {
                return;
            }

            AccessStateManager.SetState(AccessState.Map);
            AnnounceMapOnce(map);
            TrackCurrentNode(map);
            ProcessKeys(map);
            PollFocusAnnouncement(map);
        }

        private void Reset()
        {
            _mapAnnounced = false;
            _lastMapNode = null;
            _lastAnnouncement = null;
            _focusedDestinationIndex = -1;
            _mapExplorerNode = null;
            _mapExplorerParent = null;
            _mapExplorerChildIndex = 0;
            _availableNodes.Clear();
            _mapExplorerPath.Clear();
            _mapExplorerChildren.Clear();
            _logicalCoordinates.Clear();
        }

        private void AnnounceMapOnce(MapManager map)
        {
            if (_mapAnnounced)
            {
                return;
            }

            _mapAnnounced = true;
            _lastMapNode = AtOManager.Instance != null ? AtOManager.Instance.currentMapNode : null;
            RebuildFocusList(map);
            ScreenReader.Say(Loc.Get("map_screen"));
            AnnounceCurrentAndDestinations(map);
            AnnounceFocusedNode(map);
            _suppressFocusUntil = Time.unscaledTime + 0.2f;
        }

        private void TrackCurrentNode(MapManager map)
        {
            string currentNode = AtOManager.Instance != null ? AtOManager.Instance.currentMapNode : null;
            if (string.IsNullOrEmpty(currentNode) || currentNode == _lastMapNode)
            {
                return;
            }

            _lastMapNode = currentNode;
            _lastAnnouncement = null;
            _focusedDestinationIndex = -1;
            ResetMapExplorer();
            RebuildFocusList(map);
            ScreenReader.Say(Loc.Get("map_current_node"));
            AnnounceCurrentAndDestinations(map);
            AnnounceFocusedNode(map);
        }

        private void ProcessKeys(MapManager map)
        {
            bool ctrl = ModInput.GetKey(KeyCode.LeftControl) || ModInput.GetKey(KeyCode.RightControl);
            if (ctrl)
            {
                if (ModInput.GetKeyDown(KeyCode.UpArrow))
                {
                    MovePreviewForward(map);
                }
                else if (ModInput.GetKeyDown(KeyCode.DownArrow))
                {
                    MovePreviewBackward(map);
                }
                else if (ModInput.GetKeyDown(KeyCode.LeftArrow))
                {
                    CyclePreviewBranch(map, -1);
                }
                else if (ModInput.GetKeyDown(KeyCode.RightArrow))
                {
                    CyclePreviewBranch(map, 1);
                }

                return;
            }

            if (ModInput.GetKeyDown(KeyCode.LeftArrow))
            {
                MoveFocus(map, -1);
            }
            else if (ModInput.GetKeyDown(KeyCode.RightArrow))
            {
                MoveFocus(map, 1);
            }
            else if (ModInput.GetKeyDown(KeyCode.Home))
            {
                JumpFocus(map, false);
            }
            else if (ModInput.GetKeyDown(KeyCode.End))
            {
                JumpFocus(map, true);
            }

            if (ModInput.GetKeyDown(KeyCode.Return) || ModInput.GetKeyDown(KeyCode.KeypadEnter))
            {
                ActivateCurrent(map);
            }
        }

        private void MoveFocus(MapManager map, int delta)
        {
            RebuildFocusList(map);
            if (_availableNodes.Count == 0)
            {
                ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                return;
            }

            int nextIndex = _focusedDestinationIndex + delta;
            if (nextIndex < 0)
            {
                nextIndex = 0;
            }
            else if (nextIndex >= _availableNodes.Count)
            {
                nextIndex = _availableNodes.Count - 1;
            }

            if (nextIndex == _focusedDestinationIndex && (_availableNodes.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            _focusedDestinationIndex = nextIndex;
            AnnounceFocusedNode(map);
            ResetMapExplorer();
            _suppressFocusUntil = Time.unscaledTime + 0.25f;
        }

        private void JumpFocus(MapManager map, bool end)
        {
            RebuildFocusList(map);
            if (_availableNodes.Count == 0)
            {
                ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                return;
            }

            if (!NavigationBounds.TryJump(ref _focusedDestinationIndex, end, _availableNodes.Count))
            {
                return;
            }

            AnnounceFocusedNode(map);
            ResetMapExplorer();
            _suppressFocusUntil = Time.unscaledTime + 0.25f;
        }

        private void ResetMapExplorer()
        {
            _mapExplorerNode = null;
            _mapExplorerParent = null;
            _mapExplorerChildIndex = 0;
            _mapExplorerPath.Clear();
            _mapExplorerChildren.Clear();
        }

        private void MovePreviewForward(MapManager map)
        {
            RebuildFocusList(map);
            if (_mapExplorerNode == null)
            {
                Node start = GetFocusedNode();
                if (start == null)
                {
                    ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                    return;
                }

                StartMapExplorer(map, start, GetCurrentNode(map));
                return;
            }

            RebuildMapExplorerChildren(map);
            if (_mapExplorerChildren.Count == 0)
            {
                if (!ModSettings.MapDetailsEnabled)
                {
                    ScreenReader.Say(GetNodeText(map, _mapExplorerNode, true));
                    return;
                }

                ScreenReader.Say(Loc.Get("map_path_preview_end", GetNodeText(map, _mapExplorerNode, true)));
                return;
            }

            if (_mapExplorerChildIndex < 0 || _mapExplorerChildIndex >= _mapExplorerChildren.Count)
            {
                _mapExplorerChildIndex = 0;
            }

            Node parent = _mapExplorerNode;
            Node child = _mapExplorerChildren[_mapExplorerChildIndex];
            _mapExplorerNode = child;
            _mapExplorerParent = parent;
            _mapExplorerPath.Add(child);
            _mapExplorerChildIndex = 0;
            RebuildMapExplorerChildren(map);
            AnnounceMapExplorer(map);
        }

        private void MovePreviewBackward(MapManager map)
        {
            if (_mapExplorerNode == null)
            {
                Node current = GetCurrentNode(map);
                string currentText = current != null ? GetNodeText(map, current, false) : Loc.Get("unknown_node");
                ScreenReader.Say(ModSettings.MapDetailsEnabled ? Loc.Get("map_preview_current", currentText) : currentText);
                return;
            }

            if (_mapExplorerPath.Count <= 1)
            {
                ResetMapExplorer();
                Node current = GetCurrentNode(map);
                string currentText = current != null ? GetNodeText(map, current, false) : Loc.Get("unknown_node");
                ScreenReader.Say(ModSettings.MapDetailsEnabled ? Loc.Get("map_preview_current", currentText) : currentText);
                return;
            }

            _mapExplorerPath.RemoveAt(_mapExplorerPath.Count - 1);
            _mapExplorerNode = _mapExplorerPath[_mapExplorerPath.Count - 1];
            _mapExplorerParent = _mapExplorerPath.Count > 1 ? _mapExplorerPath[_mapExplorerPath.Count - 2] : GetCurrentNode(map);
            _mapExplorerChildIndex = 0;
            RebuildMapExplorerChildren(map);
            AnnounceMapExplorer(map);
        }

        private void CyclePreviewBranch(MapManager map, int delta)
        {
            RebuildFocusList(map);
            List<Node> choices = GetMapExplorerSiblings(map);

            if (choices.Count == 0)
            {
                ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                return;
            }

            int currentIndex = _mapExplorerNode == null ? _focusedDestinationIndex : choices.IndexOf(_mapExplorerNode);
            if (currentIndex < 0 || currentIndex >= choices.Count)
            {
                currentIndex = 0;
            }

            int nextIndex = currentIndex + delta;
            if (nextIndex < 0)
            {
                nextIndex = 0;
            }
            else if (nextIndex >= choices.Count)
            {
                nextIndex = choices.Count - 1;
            }

            if (nextIndex == currentIndex && (choices.Count > 1 || !ModSettings.RepeatSingleItemEnabled))
            {
                return;
            }

            if (_mapExplorerNode == null)
            {
                _focusedDestinationIndex = nextIndex;
                StartMapExplorer(map, choices[nextIndex], GetCurrentNode(map));
                return;
            }

            _mapExplorerNode = choices[nextIndex];
            if (_mapExplorerPath.Count == 0)
            {
                _mapExplorerPath.Add(_mapExplorerNode);
            }
            else
            {
                _mapExplorerPath[_mapExplorerPath.Count - 1] = _mapExplorerNode;
            }

            _mapExplorerChildIndex = 0;
            RebuildMapExplorerChildren(map);
            AnnounceMapExplorer(map);
        }

        private void StartMapExplorer(MapManager map, Node node, Node parent)
        {
            ResetMapExplorer();
            if (node == null)
            {
                ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                return;
            }

            _mapExplorerNode = node;
            _mapExplorerParent = parent;
            _mapExplorerPath.Add(node);
            _mapExplorerChildIndex = 0;
            RebuildMapExplorerChildren(map);

            AnnounceMapExplorer(map);
        }

        private void RebuildMapExplorerChildren(MapManager map)
        {
            _mapExplorerChildren.Clear();
            if (_mapExplorerNode == null)
            {
                return;
            }

            _mapExplorerChildren.AddRange(GetOnwardNodes(map, _mapExplorerNode));
            if (_mapExplorerChildIndex < 0 || _mapExplorerChildIndex >= _mapExplorerChildren.Count)
            {
                _mapExplorerChildIndex = 0;
            }
        }

        private List<Node> GetMapExplorerSiblings(MapManager map)
        {
            if (_mapExplorerNode == null)
            {
                return new List<Node>(_availableNodes);
            }

            Node current = GetCurrentNode(map);
            if (_mapExplorerParent == null || _mapExplorerParent == current || IsCurrentNode(_mapExplorerParent))
            {
                return new List<Node>(_availableNodes);
            }

            return GetOnwardNodes(map, _mapExplorerParent, _mapExplorerNode);
        }

        private void AnnounceMapExplorer(MapManager map)
        {
            if (_mapExplorerNode == null)
            {
                return;
            }

            List<string> parts = new List<string>();
            string nodeText = GetNodeText(map, _mapExplorerNode, true);
            if (!ModSettings.MapDetailsEnabled)
            {
                if (GetMapExplorerSiblings(map).Count > 1)
                {
                    parts.Add(Loc.Get("map_branch_simple"));
                }

                parts.Add(nodeText);
                ScreenReader.Say(string.Join(". ", parts.ToArray()));
                return;
            }

            if (_mapExplorerParent != null && !IsCurrentNode(_mapExplorerParent))
            {
                parts.Add(Loc.Get("map_explorer_after", GetNodeName(_mapExplorerParent), nodeText));
            }
            else
            {
                parts.Add(Loc.Get("map_explorer_current", nodeText));
            }

            string siblingHint = GetMapExplorerSiblingHint(map);
            if (!string.IsNullOrWhiteSpace(siblingHint))
            {
                parts.Add(siblingHint);
            }

            if (_mapExplorerChildren.Count > 0)
            {
                Node selectedChild = _mapExplorerChildren[_mapExplorerChildIndex];
                parts.Add(Loc.Get("map_explorer_choices", _mapExplorerChildren.Count, GetNodeName(selectedChild)));
            }
            else
            {
                parts.Add(Loc.Get("map_explorer_no_onward"));
            }

            ScreenReader.Say(string.Join(". ", parts.ToArray()));
        }

        private string GetMapExplorerSiblingHint(MapManager map)
        {
            List<Node> siblings = GetMapExplorerSiblings(map);
            if (siblings.Count <= 1 || _mapExplorerNode == null)
            {
                return null;
            }

            int index = siblings.IndexOf(_mapExplorerNode);
            bool left = index > 0;
            bool right = index >= 0 && index < siblings.Count - 1;
            if (left && right)
            {
                return Loc.Get("map_explorer_siblings_both");
            }

            if (left)
            {
                return Loc.Get("map_explorer_sibling_left");
            }

            if (right)
            {
                return Loc.Get("map_explorer_sibling_right");
            }

            return null;
        }

        private void PollFocusAnnouncement(MapManager map)
        {
            if (Time.unscaledTime - _lastPollTime < 0.25f)
            {
                return;
            }

            if (Time.unscaledTime < _suppressFocusUntil)
            {
                return;
            }

            _lastPollTime = Time.unscaledTime;
            if (_availableNodes.Count == 0)
            {
                RebuildFocusList(map);
                AnnounceCurrentAndDestinations(map);
            }
        }

        private void AnnounceCurrentAndDestinations(MapManager map)
        {
            Node current = GetCurrentNode(map);
            string currentText = current != null ? GetNodeText(map, current, false) : Loc.Get("unknown_node");
            ScreenReader.Say(Loc.Get("map_current_and_destinations", currentText, _availableNodes.Count));
        }

        private void AnnounceFocusedNode(MapManager map)
        {
            Node node = GetFocusedNode();
            if (node == null)
            {
                if (_availableNodes.Count == 0)
                {
                    ScreenReader.Say(Loc.Get("map_no_available_destinations"));
                }

                return;
            }

            string announcement = GetNodeText(map, node, false);
            if (announcement == _lastAnnouncement)
            {
                return;
            }

            _lastAnnouncement = announcement;
            ScreenReader.Say(announcement);
        }

        private void ActivateCurrent(MapManager map)
        {
            Node node = GetFocusedNode();
            if (node == null)
            {
                ScreenReader.Say(Loc.Get("no_map_node"));
                return;
            }

            string text = GetNodeText(map, node, false);
            if (!map.CanTravelToThisNode(node))
            {
                ScreenReader.Say(Loc.Get("map_node_unavailable", text));
                return;
            }

            if (IsFollowingLeaderSelectionBlocked())
            {
                ScreenReader.Say(Loc.Get("map_following_leader_selection_blocked", text));
                return;
            }

            ScreenReader.Say(Loc.Get("activated_loading", text));
            if (map != null && !map.selectedNode)
            {
                GameManager.Instance.SetCursorPlain();
                map.HidePopup();
                map.PlayerSelectedNode(node);
            }
        }

        private static bool IsFollowingLeaderSelectionBlocked()
        {
            return GameManager.Instance != null
                && GameManager.Instance.IsMultiplayer()
                && NetworkManager.Instance != null
                && !NetworkManager.Instance.IsMaster()
                && AtOManager.Instance != null
                && AtOManager.Instance.followingTheLeader;
        }

        private Node GetFocusedNode()
        {
            if (_availableNodes.Count == 0)
            {
                return null;
            }

            if (_focusedDestinationIndex < 0 || _focusedDestinationIndex >= _availableNodes.Count)
            {
                _focusedDestinationIndex = 0;
            }

            return _availableNodes[_focusedDestinationIndex];
        }

        private Node GetCurrentNode(MapManager map)
        {
            if (AtOManager.Instance == null)
            {
                return null;
            }

            Dictionary<string, Node> nodes = map.GetMapNodeDict();
            Node current;
            if (nodes != null && nodes.TryGetValue(AtOManager.Instance.currentMapNode, out current))
            {
                return current;
            }

            return null;
        }

        private void RebuildFocusList(MapManager map)
        {
            Node previous = GetFocusedNode();
            _availableNodes.Clear();
            _logicalCoordinates.Clear();

            Node current = GetCurrentNode(map);

            if (current != null && current.nodeData != null && current.nodeData.NodesConnected != null)
            {
                for (int i = 0; i < current.nodeData.NodesConnected.Length; i++)
                {
                    NodeData connectedData = current.nodeData.NodesConnected[i];
                    if (connectedData == null)
                    {
                        continue;
                    }

                    Node connected = map.GetNodeFromId(connectedData.NodeId);
                    if (connected != null && connected != current && map.CanTravelToThisNode(connected, current))
                    {
                        AddAvailableNode(connected);
                    }
                }
            }

            RebuildLogicalCoordinates(map);
            SortNodesByRouteOrder(_availableNodes);

            if (previous != null)
            {
                int previousIndex = _availableNodes.IndexOf(previous);
                if (previousIndex >= 0)
                {
                    _focusedDestinationIndex = previousIndex;
                }
            }

            if (_availableNodes.Count == 0)
            {
                _focusedDestinationIndex = -1;
            }
            else if (_focusedDestinationIndex < 0 || _focusedDestinationIndex >= _availableNodes.Count)
            {
                _focusedDestinationIndex = 0;
            }
        }

        private void AddAvailableNode(Node node)
        {
            if (node != null && node.gameObject.activeInHierarchy && Functions.TransformIsVisible(node.transform) && !_availableNodes.Contains(node) && !string.IsNullOrWhiteSpace(node.GetNodeAssignedId()))
            {
                _availableNodes.Add(node);
            }
        }

        private List<Node> GetOnwardNodes(MapManager map, Node source)
        {
            return GetOnwardNodes(map, source, null);
        }

        private List<Node> GetOnwardNodes(MapManager map, Node source, Node allowedExistingNode)
        {
            List<Node> result = new List<Node>();
            if (source == null || source.nodeData == null || source.nodeData.NodesConnected == null)
            {
                return result;
            }

            for (int i = 0; i < source.nodeData.NodesConnected.Length; i++)
            {
                NodeData connectedData = source.nodeData.NodesConnected[i];
                if (connectedData == null)
                {
                    continue;
                }

                Node connected = map.GetNodeFromId(connectedData.NodeId);
                if (connected == null || connected == source || (_mapExplorerPath.Contains(connected) && connected != allowedExistingNode))
                {
                    continue;
                }

                if (connected.gameObject.activeInHierarchy && Functions.TransformIsVisible(connected.transform) && !string.IsNullOrWhiteSpace(connected.GetNodeAssignedId()) && HasRoadBetween(map, source, connected) && map.CanTravelToThisNode(connected, source))
                {
                    result.Add(connected);
                }
            }

            SortNodesByRouteOrder(result);
            KeepNearestRouteDepth(map, source, result);
            return result;
        }

        private static bool HasRoadBetween(MapManager map, Node source, Node destination)
        {
            if (map == null || source == null || destination == null || source.nodeData == null || destination.nodeData == null || RoadsField == null)
            {
                return true;
            }

            Dictionary<string, Transform> roads = RoadsField.GetValue(map) as Dictionary<string, Transform>;
            if (roads == null || roads.Count == 0)
            {
                return true;
            }

            string key = (source.nodeData.NodeId + "-" + destination.nodeData.NodeId).ToLower().Trim();
            return roads.ContainsKey(key);
        }

        private void KeepNearestRouteDepth(MapManager map, Node source, List<Node> nodes)
        {
            if (nodes.Count <= 1 || source == null)
            {
                return;
            }

            EnsureLogicalCoordinates(map);
            LogicalCoordinate sourceCoordinate;
            if (!_logicalCoordinates.TryGetValue(source, out sourceCoordinate))
            {
                return;
            }

            int nearestDepth = int.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                LogicalCoordinate coordinate;
                if (_logicalCoordinates.TryGetValue(nodes[i], out coordinate) && coordinate.Depth > sourceCoordinate.Depth && coordinate.Depth < nearestDepth)
                {
                    nearestDepth = coordinate.Depth;
                }
            }

            if (nearestDepth == int.MaxValue)
            {
                return;
            }

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                LogicalCoordinate coordinate;
                if (_logicalCoordinates.TryGetValue(nodes[i], out coordinate) && coordinate.Depth > nearestDepth)
                {
                    nodes.RemoveAt(i);
                }
            }
        }

        private int GetDefaultPreviewBranchIndex(MapManager map, Node source)
        {
            List<Node> choices = GetOnwardNodes(map, source);
            if (choices.Count == 0)
            {
                return 0;
            }

            int bestIndex = 0;
            float bestDistance = Mathf.Abs(choices[0].transform.localPosition.x - source.transform.localPosition.x);
            for (int i = 1; i < choices.Count; i++)
            {
                float distance = Mathf.Abs(choices[i].transform.localPosition.x - source.transform.localPosition.x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private void SortNodesByRouteOrder(List<Node> nodes)
        {
            nodes.Sort(CompareNodesByRouteOrder);
        }

        private int CompareNodesByRouteOrder(Node left, Node right)
        {
            LogicalCoordinate leftCoordinate;
            LogicalCoordinate rightCoordinate;
            bool hasLeft = _logicalCoordinates.TryGetValue(left, out leftCoordinate);
            bool hasRight = _logicalCoordinates.TryGetValue(right, out rightCoordinate);
            if (hasLeft && hasRight)
            {
                int depthCompare = leftCoordinate.Depth.CompareTo(rightCoordinate.Depth);
                if (depthCompare != 0)
                {
                    return depthCompare;
                }

                int laneCompare = leftCoordinate.Lane.CompareTo(rightCoordinate.Lane);
                if (laneCompare != 0)
                {
                    return laneCompare;
                }
            }
            else if (hasLeft)
            {
                return -1;
            }
            else if (hasRight)
            {
                return 1;
            }

            return CompareNodesLeftToRight(left, right);
        }

        private string GetNodeText(MapManager map, Node node, bool previewMode)
        {
            List<string> parts = new List<string>();
            string branchText = GetBranchText(map, node, previewMode);
            if (ModSettings.MapDetailsEnabled && !string.IsNullOrWhiteSpace(branchText))
            {
                parts.Add(branchText);
            }

            string name = node.nodeData != null ? NodeName(node.nodeData) : node.gameObject.name;
            if (string.IsNullOrWhiteSpace(name) && node.nodeData != null)
            {
                name = node.nodeData.NodeId;
            }

            parts.Add(Clean(name));
            parts.Add(GetCoordinateText(map, node));
            if (!ModSettings.MapDetailsEnabled)
            {
                return string.Join(". ", parts.ToArray());
            }

            if (previewMode)
            {
                parts.Add(Loc.Get("map_future_path"));
            }
            else if (IsCurrentNode(node))
            {
                parts.Add(Loc.Get("current"));
            }
            else if (map.CanTravelToThisNode(node))
            {
                parts.Add(Loc.Get("available"));
            }

            string action = GetActionText(node);
            if (!string.IsNullOrWhiteSpace(action))
            {
                parts.Add(action);
            }

            if (IsBossNode(node))
            {
                parts.Add(Loc.Get("boss"));
            }

            if (node.nodeData != null && node.nodeData.GoToTown)
            {
                parts.Add(Loc.Get("town"));
            }

            AddKnownLocationDetails(node, parts);

            return string.Join(". ", parts.ToArray());
        }

        private static string GetNodeName(Node node)
        {
            if (node == null)
            {
                return Loc.Get("unknown_node");
            }

            string name = node.nodeData != null ? NodeName(node.nodeData) : node.gameObject.name;
            if (string.IsNullOrWhiteSpace(name) && node.nodeData != null)
            {
                name = node.nodeData.NodeId;
            }

            return Clean(string.IsNullOrWhiteSpace(name) ? Loc.Get("unknown_node") : name);
        }

        private static bool IsCurrentNode(Node node)
        {
            return AtOManager.Instance != null && node.nodeData != null && node.nodeData.NodeId == AtOManager.Instance.currentMapNode;
        }

        private static string GetActionText(Node node)
        {
            string action = node.GetNodeAction();
            if (action == "combat")
            {
                return Loc.Get("combat");
            }

            if (action == "event")
            {
                return Loc.Get("event");
            }

            return Clean(action);
        }

        private void AddKnownLocationDetails(Node node, List<string> parts)
        {
            if (node == null || node.nodeData == null)
            {
                return;
            }

            string action = node.GetNodeAction();
            if (action == "event")
            {
                AddVisibleEventDetails(node, parts);
            }
            else if (action == "combat")
            {
                AddVisibleCombatDetails(node, parts);
            }

            if (node.nodeData.NodeGround != Enums.NodeGround.None)
            {
                AddLine(parts, Loc.Get("map_ground", Clean(GameText.Get(System.Enum.GetName(typeof(Enums.NodeGround), node.nodeData.NodeGround)))));
            }
        }

        private static void AddVisibleEventDetails(Node node, List<string> parts)
        {
            if (!IsNodeUnlockedForPopup(node))
            {
                AddLine(parts, Loc.Get("map_unknown_event"));
                return;
            }

            EventData data = Globals.Instance != null ? Globals.Instance.GetEventData(node.GetNodeAssignedId()) : null;
            if (data == null)
            {
                return;
            }

            AddLine(parts, EventName(data));
        }

        private void AddVisibleCombatDetails(Node node, List<string> parts)
        {
            CombatData data = Globals.Instance != null ? Globals.Instance.GetCombatData(node.GetNodeAssignedId()) : null;
            if (data == null)
            {
                return;
            }

            if (!IsCombatVisibleForPopup(node))
            {
                AddLine(parts, Loc.Get("map_unknown_event"));
                return;
            }

            AddCombatEnemies(node, data, parts);
        }

        private static void AddCombatEnemies(Node node, CombatData data, List<string> parts)
        {
            NPCData[] npcs = GetCombatNpcsForMap(node, data);
            if (npcs == null)
            {
                return;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < npcs.Length; i++)
            {
                if (npcs[i] != null)
                {
                    names.Add(Clean(NpcName(npcs[i])));
                }
            }

            if (names.Count > 0)
            {
                AddLine(parts, Loc.Get("map_combat_enemies", string.Join(", ", names.ToArray())));
            }
        }

        private static NPCData[] GetCombatNpcsForMap(Node node, CombatData data)
        {
            if (node != null
                && node.nodeData != null
                && node.nodeData.NodeCombatTier != Enums.CombatTier.T0
                && data != null
                && !data.NeverRandomizeEnemies
                && IsRandomCombatPreviewVisible(node)
                && !node.nodeData.DisableRandom)
            {
                string seedSource = node.nodeData.NodeId + AtOManager.Instance.GetGameId() + data.CombatId;
                return Functions.GetRandomCombat(node.nodeData.NodeCombatTier, seedSource.GetDeterministicHashCode(), node.nodeData.NodeId);
            }

            return data != null ? data.NPCList : null;
        }

        private static bool IsCombatVisibleForPopup(Node node)
        {
            return IsRandomCombatPreviewVisible(node) || IsNodeUnlockedForPopup(node);
        }

        private static bool IsRandomCombatPreviewVisible(Node node)
        {
            if (node == null || node.nodeData == null || node.GetNodeAction() != "combat" || node.nodeData.NodeCombatTier == Enums.CombatTier.T0 || node.nodeData.DisableRandom)
            {
                return false;
            }

            return (MadnessManager.Instance != null && MadnessManager.Instance.IsMadnessTraitActive("randomcombats"))
                || (GameManager.Instance != null && GameManager.Instance.IsObeliskChallenge())
                || (AtOManager.Instance != null && AtOManager.Instance.IsChallengeTraitActive("randomcombats"));
        }

        private static bool IsNodeUnlockedForPopup(Node node)
        {
            if (node == null || node.nodeData == null || AtOManager.Instance == null)
            {
                return false;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsObeliskChallenge())
            {
                return AtOManager.Instance.mapVisitedNodes != null && AtOManager.Instance.mapVisitedNodes.Contains(node.nodeData.NodeId);
            }

            if (PlayerManager.Instance == null)
            {
                return false;
            }

            return PlayerManager.Instance.IsNodeUnlocked(node.GetNodeAssignedId()) || PlayerManager.Instance.IsNodeUnlocked(node.nodeData.NodeId);
        }

        private static bool CanOfferObeliskCorruption(Node node, CombatData data)
        {
            if (node == null || node.nodeData == null || data == null || node.nodeData.DisableCorruption || node.GetNodeAction() != "combat")
            {
                return false;
            }

            string nodeId = node.nodeData.NodeId;
            if (AtOManager.Instance == null || AtOManager.Instance.GetGameId() == "tuto".ToUpper() || nodeId == "tutorial_1" || nodeId == "sen_1" || nodeId == "sen_2" || nodeId == "sen_3" || nodeId == "aqua_27")
            {
                return false;
            }

            return data.EventData == null || (data.EventRequirementData != null && !AtOManager.Instance.PlayerHasRequirement(data.EventRequirementData));
        }

        private string GetCoordinateText(MapManager map, Node node)
        {
            EnsureLogicalCoordinates(map);
            LogicalCoordinate coordinate;
            if (_logicalCoordinates.TryGetValue(node, out coordinate))
            {
                return Loc.Get("map_coordinates", coordinate.Lane, coordinate.Depth);
            }

            return Loc.Get("map_coordinates_unknown");
        }

        private string GetBranchText(MapManager map, Node node, bool previewMode)
        {
            return null;
        }

        private void EnsureLogicalCoordinates(MapManager map)
        {
            if (_logicalCoordinates.Count == 0)
            {
                RebuildLogicalCoordinates(map);
            }
        }

        private void RebuildLogicalCoordinates(MapManager map)
        {
            _logicalCoordinates.Clear();
            List<Node> roots = GetCoordinateRoots(map);
            if (roots.Count == 0)
            {
                return;
            }

            Dictionary<Node, int> depthByNode = new Dictionary<Node, int>();
            List<Node> queue = new List<Node>();
            roots.Sort(CompareNodesLeftToRight);
            for (int i = 0; i < roots.Count; i++)
            {
                depthByNode[roots[i]] = 0;
                queue.Add(roots[i]);
            }

            for (int i = 0; i < queue.Count && i < 80; i++)
            {
                Node source = queue[i];
                int nextDepth = depthByNode[source] + 1;
                List<Node> onward = GetOnwardNodesForCoordinate(map, source);
                for (int j = 0; j < onward.Count; j++)
                {
                    Node next = onward[j];
                    if (depthByNode.ContainsKey(next))
                    {
                        continue;
                    }

                    depthByNode[next] = nextDepth;
                    queue.Add(next);
                }
            }

            Dictionary<int, List<Node>> nodesByDepth = new Dictionary<int, List<Node>>();
            foreach (KeyValuePair<Node, int> item in depthByNode)
            {
                List<Node> list;
                if (!nodesByDepth.TryGetValue(item.Value, out list))
                {
                    list = new List<Node>();
                    nodesByDepth[item.Value] = list;
                }

                list.Add(item.Key);
            }

            foreach (KeyValuePair<int, List<Node>> item in nodesByDepth)
            {
                item.Value.Sort(CompareNodesLeftToRight);
                for (int i = 0; i < item.Value.Count; i++)
                {
                    _logicalCoordinates[item.Value[i]] = new LogicalCoordinate(item.Key, i + 1);
                }
            }
        }

        private List<Node> GetCoordinateRoots(MapManager map)
        {
            List<Node> visibleNodes = GetVisibleAssignedNodes(map);
            HashSet<Node> hasIncoming = new HashSet<Node>();
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                Node source = visibleNodes[i];
                if (source.nodeData == null || source.nodeData.NodesConnected == null)
                {
                    continue;
                }

                for (int j = 0; j < source.nodeData.NodesConnected.Length; j++)
                {
                    NodeData connectedData = source.nodeData.NodesConnected[j];
                    Node connected = connectedData != null ? map.GetNodeFromId(connectedData.NodeId) : null;
                    if (connected != null && visibleNodes.Contains(connected))
                    {
                        hasIncoming.Add(connected);
                    }
                }
            }

            List<Node> roots = new List<Node>();
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                Node node = visibleNodes[i];
                if (!hasIncoming.Contains(node))
                {
                    roots.Add(node);
                }
            }

            if (roots.Count == 0)
            {
                Node current = GetCurrentNode(map);
                if (current != null)
                {
                    roots.Add(current);
                }
            }

            return roots;
        }

        private static List<Node> GetVisibleAssignedNodes(MapManager map)
        {
            List<Node> result = new List<Node>();
            Dictionary<string, Node> nodes = map.GetMapNodeDict();
            if (nodes == null)
            {
                return result;
            }

            foreach (KeyValuePair<string, Node> item in nodes)
            {
                Node node = item.Value;
                if (node != null && node.gameObject.activeInHierarchy && Functions.TransformIsVisible(node.transform) && !string.IsNullOrWhiteSpace(node.GetNodeAssignedId()))
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private List<Node> GetOnwardNodesForCoordinate(MapManager map, Node source)
        {
            List<Node> result = new List<Node>();
            if (source == null || source.nodeData == null || source.nodeData.NodesConnected == null)
            {
                return result;
            }

            for (int i = 0; i < source.nodeData.NodesConnected.Length; i++)
            {
                NodeData connectedData = source.nodeData.NodesConnected[i];
                if (connectedData == null)
                {
                    continue;
                }

                Node connected = map.GetNodeFromId(connectedData.NodeId);
                if (connected != null && connected != source && connected.gameObject.activeInHierarchy && Functions.TransformIsVisible(connected.transform) && !string.IsNullOrWhiteSpace(connected.GetNodeAssignedId()) && map.CanTravelToThisNode(connected, source))
                {
                    result.Add(connected);
                }
            }

            return result;
        }

        private static int CompareNodesLeftToRight(Node left, Node right)
        {
            int xCompare = left.transform.localPosition.x.CompareTo(right.transform.localPosition.x);
            if (xCompare != 0)
            {
                return xCompare;
            }

            string leftId = left.nodeData != null ? left.nodeData.NodeId : left.gameObject.name;
            string rightId = right.nodeData != null ? right.nodeData.NodeId : right.gameObject.name;
            return string.CompareOrdinal(leftId, rightId);
        }

        private static bool IsBossNode(Node node)
        {
            if (node == null || node.nodeData == null || node.GetNodeAction() != "combat")
            {
                return false;
            }

            string nodeId = node.nodeData.NodeId;
            if (nodeId == "of1_10" || nodeId == "of2_10")
            {
                return true;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsObeliskChallenge() && AtOManager.Instance != null && AtOManager.Instance.NodeHaveBossRare(nodeId))
            {
                return true;
            }

            CombatData combat = Globals.Instance != null ? Globals.Instance.GetCombatData(node.GetNodeAssignedId()) : null;
            if (combat == null || combat.NPCList == null)
            {
                return false;
            }

            for (int i = 0; i < combat.NPCList.Length; i++)
            {
                if (combat.NPCList[i] != null && combat.NPCList[i].IsBoss)
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string text)
        {
            return TextCleaner.ToSpeech(text);
        }

        private static void AddLine(List<string> parts, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }

        private static void AddRequirement(HashSet<string> set, EventRequirementData requirement)
        {
            if (set != null && requirement != null)
            {
                string name = RequirementName(requirement);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    set.Add(name);
                }
            }
        }

        private static void AddCardReward(HashSet<string> set, CardRealtimeData card)
        {
            if (set == null || card == null)
            {
                return;
            }

            string name = CardName(card);
            if (!string.IsNullOrWhiteSpace(name))
            {
                set.Add(name);
            }
        }

        private static void AddClassReward(HashSet<string> set, SubClassData data)
        {
            if (set == null || data == null)
            {
                return;
            }

            string name = ClassName(data);
            if (!string.IsNullOrWhiteSpace(name))
            {
                set.Add(name);
            }
        }

        private static void AddJoined(List<string> parts, string key, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            List<string> list = new List<string>(values);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (string.IsNullOrWhiteSpace(list[i]))
                {
                    list.RemoveAt(i);
                }
            }

            if (list.Count == 0)
            {
                return;
            }

            list.Sort();
            AddLine(parts, Loc.Get(key, string.Join(", ", list.ToArray())));
        }

        private static string RequirementName(EventRequirementData requirement)
        {
            if (requirement == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(requirement.RequirementId + "_name", "requirements");
            return Clean(string.IsNullOrWhiteSpace(text) ? requirement.RequirementName : text);
        }

        private static string NodeName(NodeData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.NodeId + "_name", "nodes");
            if (string.IsNullOrWhiteSpace(text))
            {
                text = data.NodeName;
            }

            return Clean(string.IsNullOrWhiteSpace(text) ? data.NodeId : text);
        }

        private static string EventName(EventData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.EventId + "_nm", "events");
            return Clean(string.IsNullOrWhiteSpace(text) ? data.EventName : text);
        }

        private static string EventDescription(EventData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();
            string description = GameText.Get(data.EventId + "_dsc", "events");
            AddLine(lines, Clean(string.IsNullOrWhiteSpace(description) ? data.Description : description));
            string action = GameText.Get(data.EventId + "_dsca", "events");
            AddLine(lines, Clean(string.IsNullOrWhiteSpace(action) ? data.DescriptionAction : action));
            return string.Join(" ", lines.ToArray());
        }

        private static string EventRarityText(EventData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string spriteName = data.EventSpriteMap != null ? data.EventSpriteMap.name.ToLower() : "";
            switch (spriteName)
            {
                case "nodeiconeventgreen":
                    return Clean(GameText.Get("eventUncommon"));
                case "nodeiconeventblue":
                    return Clean(GameText.Get("eventRare"));
                case "nodeiconeventpurple":
                    return Clean(GameText.Get("eventEpic"));
                case "nodeiconmap":
                    return Clean(GameText.Get("mapLegendMapTransition"));
                case "quest-yogger":
                    return Clean(GameText.Get("wolfwars"));
                case "nodeiconeventteal":
                    return Clean(GameText.Get("eventCommon"));
            }

            if (data.EventIconShader == Enums.MapIconShader.Orange)
            {
                return Clean(GameText.Get("eventCharacter"));
            }

            return string.Empty;
        }

        private static string TierText(Enums.CombatTier tier)
        {
            if (tier == Enums.CombatTier.T0)
            {
                return string.Empty;
            }

            string text = tier.ToString();
            return text.StartsWith("T") && text.Length > 1 ? text.Substring(1) : Clean(text);
        }

        private static string ClassName(SubClassData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.Id + "_name", "class");
            return Clean(string.IsNullOrWhiteSpace(text) ? data.Id : text);
        }

        private static string NpcName(NPCData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string text = GameText.Get(data.Id + "_name", "monsters");
            return string.IsNullOrWhiteSpace(text) ? data.NPCName : text;
        }

        private static string CardName(CardRealtimeData data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            string id = data.Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                string text = GameText.Get("c_" + id.Replace("v2", "") + "_name", "cards");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return Clean(text);
                }
            }

            return Clean(data.CardName);
        }

        private struct LogicalCoordinate
        {
            public readonly int Depth;
            public readonly int Lane;

            public LogicalCoordinate(int depth, int lane)
            {
                Depth = depth;
                Lane = lane;
            }
        }
    }
}
