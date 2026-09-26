using System.Collections.Generic;
using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Draws the network the simulation owns: the cell grid, the links, the nodes (one colour and
    /// one icon shape per <see cref="NodeType"/>), the in-flight packets, and a short flash wherever
    /// a packet is destroyed. Implements Story 002 acceptance criteria 2 and 3 of
    /// `production/epics/night-shift/story-002-unity-night-view.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>Rendering choice.</b> Everything is a <see cref="SpriteRenderer"/> with a
    /// procedurally generated sprite (see <see cref="PrimitiveSpriteFactory"/>). Lines are a 1x1
    /// white sprite scaled and rotated. This project runs the Built-in Render Pipeline with no URP
    /// package installed, and a runtime <see cref="SpriteRenderer"/> resolves the engine's built-in
    /// sprite material on its own - so unlike a <c>LineRenderer</c> plus <c>Shader.Find</c> it
    /// cannot break in a player build through shader stripping.</para>
    ///
    /// <para><b>Frame budget.</b> Topology visuals are built once; packet and flash visuals are
    /// pooled and pre-warmed. The per-frame sync walks <see cref="NetworkSimulation.ActivePackets"/>
    /// with an indexed <c>for</c> loop, never <c>foreach</c>, because enumerating an
    /// <see cref="IReadOnlyList{T}"/> through the interface boxes its enumerator. Nothing on the
    /// per-frame path allocates.</para>
    ///
    /// <para><b>Update order.</b> Reads simulation state in <see cref="LateUpdate"/> while
    /// <see cref="GameRunner"/> ticks the simulation in <c>Update</c>, so the view always renders
    /// the state produced this frame regardless of Unity's component order.</para>
    /// </remarks>
    public sealed class NetworkMapView : MonoBehaviour
    {
        private const int GridSortingOrder = 0;
        private const int LinkSortingOrder = 10;
        private const int NodeSortingOrder = 20;
        private const int NodeLabelSortingOrder = 25;
        private const int PacketSortingOrder = 30;
        private const int FlashSortingOrder = 40;

        /// <summary>
        /// Shared across every label, resolved once. Static because a font is an engine-owned asset,
        /// not per-view state, and building one per node would waste a font atlas each time.
        /// </summary>
        private static Font _nodeLabelFont;

        private NetworkSimulation _simulation;
        private ViewConfig _config;
        private MapLayout _layout;

        private Transform _gridRoot;
        private Transform _linkRoot;
        private Transform _nodeRoot;
        private Transform _packetRoot;
        private Transform _flashRoot;

        private readonly List<GameObject> _topologyVisuals = new List<GameObject>();

        private readonly Dictionary<int, PacketView> _packetViewsByPacketId = new Dictionary<int, PacketView>();
        private readonly List<PacketView> _activePacketViews = new List<PacketView>();
        private readonly Stack<PacketView> _freePacketViews = new Stack<PacketView>();

        private readonly List<FlashView> _activeFlashes = new List<FlashView>();
        private readonly Stack<FlashView> _freeFlashes = new Stack<FlashView>();

        private int _frameStamp;
        private int _createdPacketViewCount;
        private int _createdFlashCount;
        private bool _initialized;

        /// <summary>
        /// Wires the view to a simulation. Call before the night starts so no packet event is missed.
        /// </summary>
        /// <param name="simulation">Simulation to render. Not modified by this view.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="layout">Grid-to-world mapping.</param>
        public void Initialize(NetworkSimulation simulation, ViewConfig config, MapLayout layout)
        {
            _simulation = simulation;
            _config = config;
            _layout = layout;

            _gridRoot = CreateChildRoot("Grid");
            _linkRoot = CreateChildRoot("Links");
            _nodeRoot = CreateChildRoot("Nodes");
            _packetRoot = CreateChildRoot("Packets");
            _flashRoot = CreateChildRoot("Flashes");

            BuildGrid();
            RebuildTopology();
            PrewarmPools();

            _simulation.OnPacketBlocked += HandlePacketBlocked;
            _simulation.OnPacketLeaked += HandlePacketLeaked;
            _simulation.OnPacketDissipated += HandlePacketDissipated;

            _initialized = true;
        }

        /// <summary>
        /// Rebuilds every node and link visual from the current graph. Story 002 has a fixed
        /// topology, so this runs once; Story 003 (day-phase building) can call it after a placement.
        /// </summary>
        public void RebuildTopology()
        {
            for (int i = 0; i < _topologyVisuals.Count; i++)
            {
                Destroy(_topologyVisuals[i]);
            }
            _topologyVisuals.Clear();

            NetworkGraph graph = _simulation.Graph;

            IReadOnlyList<Link> links = graph.AllLinks;
            for (int i = 0; i < links.Count; i++)
            {
                Link link = links[i];
                Node a = graph.GetNode(link.NodeAId);
                Node b = graph.GetNode(link.NodeBId);
                if (a == null || b == null)
                {
                    continue;
                }

                GameObject line = CreateLine(
                    _linkRoot,
                    "Link" + link.NodeAId + "-" + link.NodeBId,
                    LinkSortingOrder,
                    _config.LinkColor);
                SetLineEndpoints(line.transform, _layout.NodeToWorld(a), _layout.NodeToWorld(b), _config.LinkThickness);
                _topologyVisuals.Add(line);
            }

            IReadOnlyList<Node> nodes = graph.AllNodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                var go = new GameObject("Node" + node.Id + "_" + node.Type);
                go.transform.SetParent(_nodeRoot, false);

                Vector2 position = _layout.NodeToWorld(node);
                go.transform.localPosition = new Vector3(position.x, position.y, 0f);
                go.transform.localScale = new Vector3(_config.NodeSize, _config.NodeSize, 1f);

                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = PrimitiveSpriteFactory.GetShape(_config.GetNodeShape(node.Type));
                renderer.color = _config.GetNodeColor(node.Type);
                renderer.sortingOrder = NodeSortingOrder;

                _topologyVisuals.Add(go);
                _topologyVisuals.Add(CreateNodeLabel(node, position));
            }
        }

        /// <summary>
        /// Draws a node's terminal name (<c>gw</c>, <c>srv-1</c>, <c>fw-2</c>...) just under it.
        /// Implements the map half of Story 004 acceptance criterion 7.
        /// </summary>
        /// <remarks>
        /// <para><b>Same name source as the parser.</b> The text comes from
        /// <see cref="NodeNaming.GetName"/>, which is also what
        /// <c>TerminalCommandProcessor</c> resolves typed names through - so "имена нод в терминале
        /// совпадают с подписями на карте" is structural here, not a convention someone has to
        /// remember. There is no second list of names to keep in step.</para>
        ///
        /// <para><b>Why <see cref="TextMesh"/>.</b> A world-space label needs no per-frame
        /// screen-projection maths and moves with the map for free, whereas a UI Toolkit overlay would
        /// have to convert every node's world position into panel space every time the camera or the
        /// topology changed. TextMesh is also the only text path available: TextMeshPro is not
        /// installed, and the labels are pure ASCII so the legacy dynamic font renders them without a
        /// glyph-coverage worry.</para>
        /// </remarks>
        private GameObject CreateNodeLabel(Node node, Vector2 nodePosition)
        {
            var go = new GameObject("Label" + node.Id);
            go.transform.SetParent(_nodeRoot, false);
            go.transform.localPosition = new Vector3(
                nodePosition.x,
                nodePosition.y + _config.NodeLabelOffsetY,
                0f);

            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = go.AddComponent<MeshRenderer>();
            }

            var text = go.AddComponent<TextMesh>();
            text.text = NodeNaming.GetName(_simulation.Graph, node);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = _config.NodeLabelColor;
            text.fontSize = _config.NodeLabelFontSize;
            text.characterSize = _config.NodeLabelCharacterSize;

            Font font = ResolveNodeLabelFont();
            if (font != null)
            {
                text.font = font;
                renderer.sharedMaterial = font.material;
            }

            renderer.sortingOrder = NodeLabelSortingOrder;
            return go;
        }

        /// <summary>
        /// Resolves the built-in legacy dynamic font for the map labels, or null if even that is
        /// unavailable - in which case the labels are simply skipped rather than the map failing.
        /// </summary>
        private static Font ResolveNodeLabelFont()
        {
            if (_nodeLabelFont != null)
            {
                return _nodeLabelFont;
            }

            try
            {
                _nodeLabelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[NightShift] No font for map node labels: " + exception.Message);
                _nodeLabelFont = null;
            }

            return _nodeLabelFont;
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            SyncPackets();
            TickFlashes(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (_simulation == null)
            {
                return;
            }

            _simulation.OnPacketBlocked -= HandlePacketBlocked;
            _simulation.OnPacketLeaked -= HandlePacketLeaked;
            _simulation.OnPacketDissipated -= HandlePacketDissipated;
        }

        // ------------------------------------------------------------------
        // Packets
        // ------------------------------------------------------------------

        private void SyncPackets()
        {
            _frameStamp++;

            IReadOnlyList<Packet> packets = _simulation.ActivePackets;
            for (int i = 0; i < packets.Count; i++)
            {
                Packet packet = packets[i];

                if (!_packetViewsByPacketId.TryGetValue(packet.Id, out PacketView view))
                {
                    view = AcquirePacketView();
                    view.Bind(packet.Id);
                    _packetViewsByPacketId[packet.Id] = view;
                    _activePacketViews.Add(view);
                }

                view.LastSeenStamp = _frameStamp;

                ResolvePacketPlacement(packet, out Vector2 position, out Vector2 direction);
                view.Apply(
                    position,
                    direction,
                    _simulation.IsPacketVisible(packet),
                    _config.GetPacketColor(packet.Type));
            }

            // Anything not touched this frame has left the simulation (blocked, leaked, dissipated).
            for (int i = _activePacketViews.Count - 1; i >= 0; i--)
            {
                PacketView view = _activePacketViews[i];
                if (view.LastSeenStamp == _frameStamp)
                {
                    continue;
                }

                _packetViewsByPacketId.Remove(view.PacketId);
                _activePacketViews.RemoveAt(i);
                view.Release();
                _freePacketViews.Push(view);
            }
        }

        /// <summary>
        /// Resolves where a packet is drawn and which way it is travelling.
        /// </summary>
        /// <remarks>
        /// <para>This mirrors <see cref="NetworkSimulation"/>'s packet state exactly and invents
        /// nothing: Core advances <see cref="Packet.LinkProgress"/> as a 0..1 fraction of the link
        /// between <see cref="Packet.CurrentNodeId"/> and <see cref="Packet.TargetNodeId"/>, moving
        /// <c>CurrentNodeId</c> forward and resetting the fraction to 0 only at the instant of
        /// arrival. So a packet is off-node for all of a leg except the single tick it arrives on,
        /// and the straight lerp below traces precisely the segment drawn as the link.</para>
        ///
        /// <para>A packet that is not crossing a link - target equal to current, or a node the graph
        /// no longer has - is drawn on its node with no direction, which suppresses the trail.</para>
        /// </remarks>
        /// <param name="packet">Packet to place. Not modified.</param>
        /// <param name="position">World position to draw at.</param>
        /// <param name="direction">Unit travel direction, or <see cref="Vector2.zero"/> when parked.</param>
        private void ResolvePacketPlacement(Packet packet, out Vector2 position, out Vector2 direction)
        {
            direction = Vector2.zero;

            NetworkGraph graph = _simulation.Graph;

            Node current = graph.GetNode(packet.CurrentNodeId);
            if (current == null)
            {
                position = Vector2.zero;
                return;
            }

            Vector2 fromPosition = _layout.NodeToWorld(current);
            position = fromPosition;

            if (packet.TargetNodeId == packet.CurrentNodeId)
            {
                return;
            }

            Node target = graph.GetNode(packet.TargetNodeId);
            if (target == null)
            {
                return;
            }

            Vector2 toPosition = _layout.NodeToWorld(target);
            position = Vector2.Lerp(fromPosition, toPosition, Mathf.Clamp01(packet.LinkProgress));

            Vector2 delta = toPosition - fromPosition;
            if (delta.sqrMagnitude > 0.000001f)
            {
                direction = delta.normalized;
            }
        }

        /// <summary>Interpolated world position of a packet, for the destruction flash.</summary>
        private Vector2 PacketWorldPosition(Packet packet)
        {
            ResolvePacketPlacement(packet, out Vector2 position, out _);
            return position;
        }

        private PacketView AcquirePacketView()
        {
            if (_freePacketViews.Count > 0)
            {
                return _freePacketViews.Pop();
            }

            _createdPacketViewCount++;
            return PacketView.Create(_packetRoot, _config, PacketSortingOrder, _createdPacketViewCount);
        }

        // ------------------------------------------------------------------
        // Destruction flashes
        // ------------------------------------------------------------------

        private void HandlePacketBlocked(Packet packet, PacketBlockReason reason)
        {
            PlayFlash(PacketWorldPosition(packet), _config.BlockedFlashColor);
        }

        private void HandlePacketLeaked(Packet packet)
        {
            PlayFlash(PacketWorldPosition(packet), _config.LeakedFlashColor);
        }

        private void HandlePacketDissipated(Packet packet)
        {
            PlayFlash(PacketWorldPosition(packet), _config.DissipatedFlashColor);
        }

        private void PlayFlash(Vector2 worldPosition, Color color)
        {
            FlashView flash = AcquireFlash();
            flash.Play(worldPosition, color, _config);
            _activeFlashes.Add(flash);
        }

        private FlashView AcquireFlash()
        {
            if (_freeFlashes.Count > 0)
            {
                return _freeFlashes.Pop();
            }

            _createdFlashCount++;
            return FlashView.Create(_flashRoot, FlashSortingOrder, _createdFlashCount);
        }

        private void TickFlashes(float deltaTime)
        {
            for (int i = _activeFlashes.Count - 1; i >= 0; i--)
            {
                FlashView flash = _activeFlashes[i];
                if (flash.Tick(deltaTime))
                {
                    _activeFlashes.RemoveAt(i);
                    _freeFlashes.Push(flash);
                }
            }
        }

        private void PrewarmPools()
        {
            for (int i = 0; i < _config.PacketViewPoolSize; i++)
            {
                _createdPacketViewCount++;
                _freePacketViews.Push(PacketView.Create(_packetRoot, _config, PacketSortingOrder, _createdPacketViewCount));
            }

            for (int i = 0; i < _config.FlashViewPoolSize; i++)
            {
                _createdFlashCount++;
                _freeFlashes.Push(FlashView.Create(_flashRoot, FlashSortingOrder, _createdFlashCount));
            }
        }

        // ------------------------------------------------------------------
        // Static visuals
        // ------------------------------------------------------------------

        private void BuildGrid()
        {
            float halfHeight = _layout.ContentHeight * 0.5f;
            float halfWidth = _layout.ContentWidth * 0.5f;

            for (int i = 0; i <= _layout.Width; i++)
            {
                float x = _layout.VerticalGridLineX(i);
                GameObject line = CreateLine(_gridRoot, "GridV" + i, GridSortingOrder, _config.GridLineColor);
                SetLineEndpoints(
                    line.transform,
                    new Vector2(x, -halfHeight),
                    new Vector2(x, halfHeight),
                    _config.GridLineThickness);
            }

            for (int i = 0; i <= _layout.Height; i++)
            {
                float y = _layout.HorizontalGridLineY(i);
                GameObject line = CreateLine(_gridRoot, "GridH" + i, GridSortingOrder, _config.GridLineColor);
                SetLineEndpoints(
                    line.transform,
                    new Vector2(-halfWidth, y),
                    new Vector2(halfWidth, y),
                    _config.GridLineThickness);
            }
        }

        private Transform CreateChildRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private static GameObject CreateLine(Transform parent, string name, int sortingOrder, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PrimitiveSpriteFactory.GetWhitePixel();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;

            return go;
        }

        /// <summary>
        /// Stretches and rotates a unit sprite into a segment between two world points. The sprite is
        /// one world unit square with a centre pivot, so scale maps directly to (length, thickness).
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
