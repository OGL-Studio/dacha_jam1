using System;
using System.Collections.Generic;
using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// All day-phase mouse interaction on the map: place the armed shop node on an empty cell, drag
    /// node-to-node to buy a link, right-click a link to remove it, click a security tool to select
    /// it for upgrade. Implements Story 003 acceptance criteria 2, 3, 4 and the map half of 6 of
    /// `production/epics/night-shift/story-003-day-build-phase.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>Owns no rule.</b> Every legality and affordability decision is
    /// <see cref="NetworkSimulation"/>'s: this class calls <c>TryPlaceNode</c> / <c>TryAddLink</c> /
    /// <c>TryUpgrade</c> and forwards the <c>out string error</c> it gets back. It deliberately does
    /// not pre-check "can I afford this" before calling - a second copy of that test here is a
    /// second thing to get out of step with Core. The only checks it makes for itself are "is the
    /// cursor even on the grid" and "which link is under the cursor", neither of which Core models.</para>
    ///
    /// <para><b>Legacy input, deliberately.</b> <c>com.unity.inputsystem</c> is not installed and
    /// `ProjectSettings/ProjectSettings.asset` has <c>activeInputHandler: 0</c>, so
    /// <see cref="Input"/> is the only API that compiles here - the same call
    /// <see cref="GameRunner"/>'s debug speed toggle already documents.</para>
    ///
    /// <para><b>UI must win over the map.</b> A click on the shop panel must not also place a node on
    /// the cell behind it. UI Toolkit tells us when the pointer is over the panel through
    /// <see cref="PointerOverUi"/>, which <see cref="DayShopView"/> drives from
    /// <c>PointerEnterEvent</c> / <c>PointerLeaveEvent</c>. That is preferred over converting mouse
    /// coordinates into panel space and hit-testing the panel: no coordinate-system or
    /// y-flip assumptions, and it cannot disagree with what UI Toolkit itself thinks it hit.</para>
    ///
    /// <para><b>Visual feedback is part of the story.</b> Criterion 6 asks that impossible actions
    /// read as impossible, so the node ghost and the link rubber band are tinted by
    /// <see cref="NetworkGraph.IsCellEmpty"/> and <see cref="NetworkGraph.CanAddLink"/> before the
    /// player commits. Those are queries, not rules - they cost nothing and change nothing.</para>
    /// </remarks>
    public sealed class DayBuildController : MonoBehaviour
    {
        private const int GhostLinkSortingOrder = 15;
        private const int GhostNodeSortingOrder = 25;
        private const int SelectionRingSortingOrder = 26;
        private const int NoNode = -1;

        private NetworkSimulation _simulation;
        private GameRunner _runner;
        private NetworkMapView _mapView;
        private ViewConfig _config;
        private MapLayout _layout;
        private Camera _camera;

        private bool _hasTypeSelection;
        private NodeType _selectedType;
        private int _selectedNodeId = NoNode;

        private int _dragFromNodeId = NoNode;
        private int _lastHoverNodeId = NoNode;

        private GameObject _ghostNode;
        private GameObject _ghostLink;
        private GameObject _selectionRing;

        private SpriteRenderer _ghostNodeRenderer;
        private SpriteRenderer _ghostLinkRenderer;

        /// <summary>True while the pointer is over a pickable day-phase UI element; map clicks are then ignored.</summary>
        public bool PointerOverUi { get; set; }

        /// <summary>True when a shop node type is armed for placement.</summary>
        public bool HasTypeSelection => _hasTypeSelection;

        /// <summary>The armed node type. Only meaningful while <see cref="HasTypeSelection"/>.</summary>
        public NodeType SelectedType => _selectedType;

        /// <summary>The node the player has selected on the map, or null. Drives the upgrade button.</summary>
        public Node SelectedNode => _selectedNodeId == NoNode ? null : _simulation.Graph.GetNode(_selectedNodeId);

        /// <summary>Raised when the armed shop type changes (including when it is cleared).</summary>
        public event Action OnTypeSelectionChanged;

        /// <summary>Raised when the selected map node changes, with the new selection or null.</summary>
        public event Action<Node> OnNodeSelectionChanged;

        /// <summary>Raised with a short Russian status line, or a rejection reason from Core, for the panel to show.</summary>
        public event Action<string> OnStatus;

        /// <summary>Raised after any successful build action, once the map visuals have been rebuilt.</summary>
        public event Action OnBuildChanged;

        /// <summary>
        /// Injects everything the controller reads. Call before the first day begins.
        /// </summary>
        /// <param name="simulation">Simulation that owns every build rule and the credit balance.</param>
        /// <param name="runner">Phase owner - build input is refused outside <see cref="GamePhase.Day"/>.</param>
        /// <param name="mapView">Map visuals, rebuilt after every successful build action.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="layout">Grid-to-world mapping, and its inverse for picking.</param>
        /// <param name="camera">The orthographic map camera, for <c>ScreenToWorldPoint</c>.</param>
        public void Initialize(
            NetworkSimulation simulation,
            GameRunner runner,
            NetworkMapView mapView,
            ViewConfig config,
            MapLayout layout,
            Camera camera)
        {
            _simulation = simulation;
            _runner = runner;
            _mapView = mapView;
            _config = config;
            _layout = layout;
            _camera = camera;

            BuildGhosts();

            if (_runner != null)
            {
                _runner.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        // ------------------------------------------------------------------
        // Commands the shop panel issues
        // ------------------------------------------------------------------

        /// <summary>Arms a node type for placement, or disarms it if it was already armed.</summary>
        public void ToggleNodeType(NodeType type)
        {
            if (_hasTypeSelection && _selectedType == type)
            {
                ClearTypeSelection();
                return;
            }

            _hasTypeSelection = true;
            _selectedType = type;
            SetSelectedNode(null);
            OnTypeSelectionChanged?.Invoke();
            OnStatus?.Invoke(UiStrings.DayHintPlacing);
        }

        /// <summary>Disarms the shop.</summary>
        public void ClearTypeSelection()
        {
            if (!_hasTypeSelection)
            {
                return;
            }

            _hasTypeSelection = false;
            OnTypeSelectionChanged?.Invoke();
            OnStatus?.Invoke(UiStrings.DayHintIdle);
        }

        /// <summary>
        /// Upgrades the selected node to level 2 through <see cref="NetworkSimulation.TryUpgrade"/>,
        /// surfacing its refusal as-is (acceptance criterion 4).
        /// </summary>
        public void UpgradeSelectedNode()
        {
            Node node = SelectedNode;
            if (node == null)
            {
                return;
            }

            NodeType type = node.Type;
            int cost = _simulation.Data.GetUpgradeCost(type);

            if (!_simulation.TryUpgrade(node.Id, out string error))
            {
                OnStatus?.Invoke(string.Format(UiStrings.RejectedFormat, error));
                return;
            }

            OnStatus?.Invoke(string.Format(UiStrings.UpgradedFormat, UiStrings.GetNodeName(type), cost));
            AfterBuildChange();
            OnNodeSelectionChanged?.Invoke(node);
        }

        // ------------------------------------------------------------------
        // Per-frame input
        // ------------------------------------------------------------------

        private void Update()
        {
            if (_simulation == null || _runner == null)
            {
                return;
            }

            if (_runner.Phase != GamePhase.Day)
            {
                return;
            }

            Vector2 world = PointerWorldPosition();
            bool onGrid = _layout.TryWorldToCell(world, out int cellX, out int cellY);
            Node hovered = onGrid ? _simulation.Graph.GetNodeAt(cellX, cellY) : null;

            if (!PointerOverUi)
            {
                if (Input.GetMouseButtonDown(0))
                {
                    HandleLeftDown(onGrid, cellX, cellY, hovered);
                }

                if (Input.GetMouseButtonDown(1))
                {
                    HandleRightDown(world);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                HandleLeftUp(hovered);
            }

            UpdateGhosts(world, onGrid, cellX, cellY, hovered);
        }

        private void HandleLeftDown(bool onGrid, int cellX, int cellY, Node hovered)
        {
            if (!onGrid)
            {
                OnStatus?.Invoke(UiStrings.CellOutOfGrid);
                return;
            }

            if (_hasTypeSelection)
            {
                PlaceSelectedType(cellX, cellY);
                return;
            }

            if (hovered == null)
            {
                SetSelectedNode(null);
                OnStatus?.Invoke(UiStrings.DayHintIdle);
                return;
            }

            // A press on a node is both "select this node" and "maybe the start of a link drag".
            // Which one it was is decided on release, in HandleLeftUp.
            _dragFromNodeId = hovered.Id;
            _lastHoverNodeId = NoNode;
            SetSelectedNode(hovered);
        }

        private void HandleLeftUp(Node hovered)
        {
            if (_dragFromNodeId == NoNode)
            {
                return;
            }

            int fromId = _dragFromNodeId;
            _dragFromNodeId = NoNode;
            _lastHoverNodeId = NoNode;

            if (hovered == null || hovered.Id == fromId)
            {
                return; // A click on one node: the selection made on press is the whole action.
            }

            CreateLink(fromId, hovered.Id);
        }

        private void HandleRightDown(Vector2 world)
        {
            if (_hasTypeSelection)
            {
                ClearTypeSelection();
                return;
            }

            if (TryPickLink(world, out Link link))
            {
                RemoveLink(link);
                return;
            }

            SetSelectedNode(null);
            OnStatus?.Invoke(UiStrings.NoLinkUnderCursor);
        }

        // ------------------------------------------------------------------
        // Build actions - every rule belongs to NetworkSimulation
        // ------------------------------------------------------------------

        private void PlaceSelectedType(int cellX, int cellY)
        {
            NodeType type = _selectedType;
            int cost = _simulation.Data.GetNodeCost(type);

            if (!_simulation.TryPlaceNode(cellX, cellY, type, out Node placed, out string error))
            {
                OnStatus?.Invoke(string.Format(UiStrings.RejectedFormat, error));
                return;
            }

            OnStatus?.Invoke(string.Format(UiStrings.PlacedFormat, UiStrings.GetNodeName(type), cost));
            AfterBuildChange();
            SetSelectedNode(placed);
        }

        private void CreateLink(int fromNodeId, int toNodeId)
        {
            // Priced by length, so the length has to be known before the link exists; CanAddLink is
            // the query Core exposes for exactly that, and TryAddLink re-runs it authoritatively.
            int cost = _simulation.Graph.CanAddLink(fromNodeId, toNodeId, out int length, out _)
                ? _simulation.Data.GetLinkCost(length)
                : 0;

            if (!_simulation.TryAddLink(fromNodeId, toNodeId, out _, out string error))
            {
                OnStatus?.Invoke(string.Format(UiStrings.RejectedFormat, error));
                return;
            }

            OnStatus?.Invoke(string.Format(UiStrings.LinkCreatedFormat, cost));
            AfterBuildChange();
        }

        private void RemoveLink(Link link)
        {
            if (!_simulation.TryRemoveLink(link.NodeAId, link.NodeBId, out int refund, out string error))
            {
                OnStatus?.Invoke(string.Format(UiStrings.RejectedFormat, error));
                return;
            }

            OnStatus?.Invoke(string.Format(UiStrings.LinkRemovedFormat, refund));
            AfterBuildChange();
        }

        /// <summary>Rebuilds the map visuals and tells the panel to re-read prices and affordability.</summary>
        private void AfterBuildChange()
        {
            if (_mapView != null)
            {
                _mapView.RebuildTopology();
            }

            OnBuildChanged?.Invoke();
        }

        // ------------------------------------------------------------------
        // Selection and picking
        // ------------------------------------------------------------------

        private void SetSelectedNode(Node node)
        {
            int newId = node != null ? node.Id : NoNode;
            if (newId == _selectedNodeId)
            {
                return;
            }

            _selectedNodeId = newId;
            OnNodeSelectionChanged?.Invoke(node);
        }

        /// <summary>
        /// Finds the link whose centre line passes closest to <paramref name="world"/>, within
        /// <see cref="ViewConfig.LinkPickRadius"/>.
        /// </summary>
        /// <remarks>
        /// Distance to the segment, not to its midpoint: a 6-cell link is long, and the player aims
        /// at the piece of line they can see. The nearest of several candidates wins, so overlapping
        /// links behave predictably.
        /// </remarks>
        private bool TryPickLink(Vector2 world, out Link picked)
        {
            picked = default;

            NetworkGraph graph = _simulation.Graph;
            IReadOnlyList<Link> links = graph.AllLinks;

            float bestDistance = _config.LinkPickRadius;
            bool found = false;

            for (int i = 0; i < links.Count; i++)
            {
                Link link = links[i];
                Node a = graph.GetNode(link.NodeAId);
                Node b = graph.GetNode(link.NodeBId);
                if (a == null || b == null)
                {
                    continue;
                }

                float distance = DistanceToSegment(world, _layout.NodeToWorld(a), _layout.NodeToWorld(b));
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    picked = link;
                    found = true;
                }
            }

            return found;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 segment = to - from;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.000001f)
            {
                return Vector2.Distance(point, from);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSquared);
            return Vector2.Distance(point, from + segment * t);
        }

        private Vector2 PointerWorldPosition()
        {
            if (_camera == null)
            {
                return Vector2.zero;
            }

            Vector3 world = _camera.ScreenToWorldPoint(Input.mousePosition);
            return new Vector2(world.x, world.y);
        }

        // ------------------------------------------------------------------
        // Ghost visuals
        // ------------------------------------------------------------------

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Day)
            {
                OnStatus?.Invoke(UiStrings.DayHintIdle);
                return;
            }

            // Leaving the day: drop every day-only piece of state so the night is clean.
            _hasTypeSelection = false;
            _dragFromNodeId = NoNode;
            SetSelectedNode(null);
            OnTypeSelectionChanged?.Invoke();
            HideGhosts();
        }

        private void BuildGhosts()
        {
            _ghostNode = CreateSprite("GhostNode", GhostNodeSortingOrder, out _ghostNodeRenderer);
            _ghostLink = CreateSprite("GhostLink", GhostLinkSortingOrder, out _ghostLinkRenderer);
            _selectionRing = CreateSprite("SelectionRing", SelectionRingSortingOrder, out SpriteRenderer ringRenderer);

            _ghostLinkRenderer.sprite = PrimitiveSpriteFactory.GetWhitePixel();
            ringRenderer.sprite = PrimitiveSpriteFactory.GetShape(PrimitiveShape.Ring);
            ringRenderer.color = _config.SelectionRingColor;

            HideGhosts();
        }

        private GameObject CreateSprite(string name, int sortingOrder, out SpriteRenderer renderer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            go.SetActive(false);
            return go;
        }

        private void HideGhosts()
        {
            SetActive(_ghostNode, false);
            SetActive(_ghostLink, false);
            SetActive(_selectionRing, false);
        }

        private void UpdateGhosts(Vector2 world, bool onGrid, int cellX, int cellY, Node hovered)
        {
            UpdateNodeGhost(onGrid, cellX, cellY);
            UpdateLinkGhost(world, hovered);
            UpdateSelectionRing();
        }

        private void UpdateNodeGhost(bool onGrid, int cellX, int cellY)
        {
            bool show = _hasTypeSelection && onGrid && !PointerOverUi;
            SetActive(_ghostNode, show);
            if (!show)
            {
                return;
            }

            Vector2 position = _layout.CellToWorld(cellX, cellY);
            _ghostNode.transform.localPosition = new Vector3(position.x, position.y, 0f);
            _ghostNode.transform.localScale = new Vector3(_config.NodeSize, _config.NodeSize, 1f);

            _ghostNodeRenderer.sprite = PrimitiveSpriteFactory.GetShape(_config.GetNodeShape(_selectedType));
            _ghostNodeRenderer.color = _simulation.Graph.IsCellEmpty(cellX, cellY)
                ? _config.GhostValidColor
                : _config.GhostInvalidColor;
        }

        private void UpdateLinkGhost(Vector2 world, Node hovered)
        {
            bool dragging = _dragFromNodeId != NoNode;
            SetActive(_ghostLink, dragging);
            if (!dragging)
            {
                return;
            }

            Node from = _simulation.Graph.GetNode(_dragFromNodeId);
            if (from == null)
            {
                SetActive(_ghostLink, false);
                return;
            }

            Vector2 start = _layout.NodeToWorld(from);
            Vector2 end = hovered != null ? _layout.NodeToWorld(hovered) : world;

            Color color = _config.LinkDragColor;
            int hoverId = hovered != null ? hovered.Id : NoNode;

            if (hovered != null && hovered.Id != from.Id)
            {
                bool legal = _simulation.Graph.CanAddLink(from.Id, hovered.Id, out int length, out string error);
                color = legal ? _config.GhostValidColor : _config.GhostInvalidColor;

                if (hoverId != _lastHoverNodeId)
                {
                    OnStatus?.Invoke(legal
                        ? string.Format(UiStrings.LinkPriceFormat, length, _simulation.Data.GetLinkCost(length))
                        : string.Format(UiStrings.RejectedFormat, error));
                }
            }

            _lastHoverNodeId = hoverId;

            _ghostLinkRenderer.color = color;
            SetLineEndpoints(_ghostLink.transform, start, end, _config.LinkThickness);
        }

        private void UpdateSelectionRing()
        {
            Node selected = _runner.Phase == GamePhase.Day ? SelectedNode : null;
            SetActive(_selectionRing, selected != null);
            if (selected == null)
            {
                return;
            }

            Vector2 position = _layout.NodeToWorld(selected);
            float size = _config.NodeSize * _config.SelectionRingScale;
            _selectionRing.transform.localPosition = new Vector3(position.x, position.y, 0f);
            _selectionRing.transform.localScale = new Vector3(size, size, 1f);
        }

        private static void SetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        /// <summary>
        /// Stretches and rotates the unit sprite into a segment between two world points - the same
        /// trick <see cref="NetworkMapView"/> uses for links and grid lines.
        /// </summary>
        private static void SetLineEndpoints(Transform lineTransform, Vector2 from, Vector2 to, float thickness)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 midpoint = (from + to) * 0.5f;

            lineTransform.localPosition = new Vector3(midpoint.x, midpoint.y, 0f);
            lineTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            lineTransform.localScale = new Vector3(length, thickness, 1f);
        }
    }
}
