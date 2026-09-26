using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// The grid-graph topology: a fixed-size 2D cell grid, the nodes placed on it, and the links
    /// between them. A link may join any two distinct nodes up to
    /// <see cref="GameData.MaxLinkLength"/> cells apart (Manhattan distance). "Adjacent"
    /// elsewhere in <c>NightShift.Core</c> means graph-adjacent (directly linked), not
    /// neighbouring grid cells. Owns cell/link validity rules; knows nothing about credits,
    /// packets, or the night clock (see <see cref="NetworkSimulation"/> for those).
    /// </summary>
    /// <example>
    /// <code>
    /// var graph = new NetworkGraph(new GameData());
    /// graph.TryPlaceNode(3, 3, NodeType.Server, out Node server, out string error);
    /// graph.TryAddLink(graph.GatewayNode.Id, server.Id, out Link link, out error);
    /// </code>
    /// </example>
    public sealed class NetworkGraph
    {
        /// <summary>The four orthogonal grid steps, paired with <see cref="GridStepY"/>. Used only for «аномалия» phantom routing.</summary>
        private static readonly int[] GridStepX = { 1, -1, 0, 0 };
        private static readonly int[] GridStepY = { 0, 0, 1, -1 };

        private readonly GameData _data;
        private readonly Node[,] _cells;
        private readonly Dictionary<int, Node> _nodesById = new Dictionary<int, Node>();
        private readonly List<Node> _nodesInOrder = new List<Node>();
        private readonly Dictionary<int, List<int>> _adjacency = new Dictionary<int, List<int>>();
        private readonly Dictionary<Link, Link> _linksByKey = new Dictionary<Link, Link>();
        private readonly List<Link> _links = new List<Link>();
        private int _nextNodeId;

        public int Width { get; }
        public int Height { get; }

        /// <summary>The single fixed Gateway node, seeded at construction on the left edge. Never sellable.</summary>
        public Node GatewayNode { get; }

        /// <summary>The single fixed Core node, seeded at construction on the right edge. Never sellable.</summary>
        public Node CoreNode { get; }

        public NetworkGraph(GameData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            Width = data.GridWidth;
            Height = data.GridHeight;
            _cells = new Node[Width, Height];

            int midRow = Height / 2;
            GatewayNode = CreateNodeInternal(0, midRow, NodeType.Gateway);
            CoreNode = CreateNodeInternal(Width - 1, midRow, NodeType.Core);
        }

        public bool IsInBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        public bool IsCellEmpty(int x, int y) => IsInBounds(x, y) && _cells[x, y] == null;

        public Node GetNode(int nodeId) => _nodesById.TryGetValue(nodeId, out Node node) ? node : null;

        public Node GetNodeAt(int x, int y) => IsInBounds(x, y) ? _cells[x, y] : null;

        /// <summary>All nodes, oldest-created first (equivalently, ascending by Id) — a stable, allocation-free order for deterministic iteration.</summary>
        public IReadOnlyList<Node> AllNodes => _nodesInOrder;

        public IReadOnlyList<Link> AllLinks => _links;

        /// <summary>
        /// Places a new security tool or server in an empty in-bounds cell. Rejects Gateway/Core
        /// (fixed, not player-placeable) and occupied/out-of-bounds cells. Does not check or
        /// spend credits — see <see cref="NetworkSimulation.TryPlaceNode"/> for the credit-gated
        /// version callers should normally use.
        /// </summary>
        public bool TryPlaceNode(int x, int y, NodeType type, out Node node, out string error)
        {
            node = null;

            if (type == NodeType.Gateway || type == NodeType.Core)
            {
                error = $"{type} is fixed and cannot be placed by the player.";
                return false;
            }

            if (!IsInBounds(x, y))
            {
                error = $"Cell ({x},{y}) is outside the {Width}x{Height} grid.";
                return false;
            }

            if (!IsCellEmpty(x, y))
            {
                error = $"Cell ({x},{y}) is already occupied.";
                return false;
            }

            node = CreateNodeInternal(x, y, type);
            error = null;
            return true;
        }

        /// <summary>
        /// Validates a prospective link without changing anything: both nodes must exist, be
        /// distinct, be at most <see cref="GameData.MaxLinkLength"/> cells apart (Manhattan), and
        /// not already be linked (in either order). On success <paramref name="length"/> is the
        /// link's Manhattan length, which callers use to price it.
        /// </summary>
        /// <example>
        /// <code>
        /// if (graph.CanAddLink(a.Id, b.Id, out int length, out string error))
        /// {
        ///     int cost = gameData.GetLinkCost(length);
        /// }
        /// </code>
        /// </example>
        public bool CanAddLink(int nodeAId, int nodeBId, out int length, out string error)
        {
            length = 0;

            if (nodeAId == nodeBId)
            {
                error = "Cannot link a node to itself.";
                return false;
            }

            Node a = GetNode(nodeAId);
            Node b = GetNode(nodeBId);
            if (a == null || b == null)
            {
                error = "Both nodes must exist.";
                return false;
            }

            int distance = Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
            if (distance > _data.MaxLinkLength)
            {
                error = $"Link length {distance} exceeds the maximum of {_data.MaxLinkLength} cells.";
                return false;
            }

            if (_linksByKey.ContainsKey(new Link(nodeAId, nodeBId, distance)))
            {
                error = "A link between these nodes already exists.";
                return false;
            }

            length = distance;
            error = null;
            return true;
        }

        /// <summary>
        /// Links two nodes if <see cref="CanAddLink"/> allows it. Does not check or spend
        /// credits — see <see cref="NetworkSimulation.TryAddLink"/> for the credit-gated version
        /// callers should normally use.
        /// </summary>
        public bool TryAddLink(int nodeAId, int nodeBId, out Link link, out string error)
        {
            link = default;

            if (!CanAddLink(nodeAId, nodeBId, out int length, out error))
            {
                return false;
            }

            var created = new Link(nodeAId, nodeBId, length);
            _linksByKey[created] = created;
            _links.Add(created);
            AddAdjacency(nodeAId, nodeBId);
            AddAdjacency(nodeBId, nodeAId);

            link = created;
            return true;
        }

        /// <summary>
        /// Removes the link between two nodes, in either order. The counterpart to
        /// <see cref="TryAddLink"/>; charging or refunding credits is
        /// <see cref="NetworkSimulation.TryRemoveLink"/>'s job, not this method's.
        /// </summary>
        /// <param name="removed">The link as it was stored, carrying its real <see cref="Link.Length"/>.</param>
        /// <param name="error">Why removal was refused; empty on success.</param>
        /// <returns>True when the link existed and was removed.</returns>
        /// <remarks>
        /// Nodes are never removed from the graph, so the adjacency lists themselves always survive;
        /// only the two endpoint entries are dropped. A packet currently crossing this link keeps
        /// the endpoints it was already travelling between — it recomputes its route at the next
        /// node, where the link is simply no longer a neighbour.
        /// </remarks>
        public bool TryRemoveLink(int nodeAId, int nodeBId, out Link removed, out string error)
        {
            error = string.Empty;

            if (!_linksByKey.TryGetValue(new Link(nodeAId, nodeBId, 0), out removed))
            {
                error = $"No link between node {nodeAId} and node {nodeBId}.";
                return false;
            }

            _linksByKey.Remove(removed);
            _links.Remove(removed);
            RemoveAdjacency(removed.NodeAId, removed.NodeBId);
            RemoveAdjacency(removed.NodeBId, removed.NodeAId);
            return true;
        }

        /// <summary>Looks up the existing link between two nodes (either order). Ignores isolation — an isolated link still exists and keeps its length.</summary>
        public bool TryGetLink(int nodeAId, int nodeBId, out Link link) =>
            _linksByKey.TryGetValue(new Link(nodeAId, nodeBId, 0), out link);

        /// <summary>
        /// The length in cells a packet must cross to get from one node to the other: the
        /// <see cref="Link.Length"/> of the link between them, or — when
        /// <paramref name="allowGridAdjacency"/> is true and the two nodes sit in orthogonally
        /// neighbouring cells — 1, even though no link exists. Story 005's «аномалия» is the only
        /// packet that passes true (see <see cref="PacketDefinition.CanCrossMissingLinks"/>).
        /// </summary>
        /// <returns>False when the step is not traversable at all.</returns>
        public bool TryGetTraversalLength(int fromNodeId, int toNodeId, bool allowGridAdjacency, out int length)
        {
            if (TryGetLink(fromNodeId, toNodeId, out Link link))
            {
                length = link.Length;
                return true;
            }

            length = 0;
            if (!allowGridAdjacency)
            {
                return false;
            }

            Node from = GetNode(fromNodeId);
            Node to = GetNode(toNodeId);
            if (from == null || to == null)
            {
                return false;
            }

            int distance = Math.Abs(from.X - to.X) + Math.Abs(from.Y - to.Y);
            if (distance != 1)
            {
                return false;
            }

            length = distance;
            return true;
        }

        /// <summary>
        /// Adds a node the network grew by itself — Story 005 acceptance criterion 3's «чужая
        /// нода». Identical to <see cref="TryPlaceNode"/> except that it costs nothing, it flags the
        /// node <see cref="Node.IsAlien"/>, and an occupied preferred cell is not an error: it
        /// settles into the nearest free cell instead (nearest by Manhattan distance, ties broken by
        /// ascending column then row, so the outcome is deterministic).
        /// </summary>
        /// <param name="preferredX">Authored column (see <see cref="AlienNodeSpawn.X"/>).</param>
        /// <param name="preferredY">Authored row.</param>
        /// <param name="type">What the intruder presents itself as. Gateway/Core are refused.</param>
        /// <param name="node">The created node, or null when the grid has no free cell left.</param>
        public bool TryCreateAlienNode(int preferredX, int preferredY, NodeType type, out Node node)
        {
            node = null;

            if (type == NodeType.Gateway || type == NodeType.Core)
            {
                return false;
            }

            if (!TryFindNearestFreeCell(preferredX, preferredY, out int x, out int y))
            {
                return false;
            }

            node = CreateNodeInternal(x, y, type);
            node.IsAlien = true;
            return true;
        }

        /// <summary>The free cell closest to (<paramref name="preferredX"/>, <paramref name="preferredY"/>), or false when the grid is full.</summary>
        private bool TryFindNearestFreeCell(int preferredX, int preferredY, out int x, out int y)
        {
            x = 0;
            y = 0;
            int bestDistance = int.MaxValue;
            bool found = false;

            for (int cx = 0; cx < Width; cx++)
            {
                for (int cy = 0; cy < Height; cy++)
                {
                    if (_cells[cx, cy] != null)
                    {
                        continue;
                    }

                    int distance = Math.Abs(cx - preferredX) + Math.Abs(cy - preferredY);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    x = cx;
                    y = cy;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Upgrades a security tool from level 1 to level 2, growing Honeypot capacity by the
        /// level 2 - level 1 delta (already-consumed capacity is preserved). Does not check or
        /// spend credits — see <see cref="NetworkSimulation.TryUpgrade"/>.
        /// </summary>
        public bool TryUpgrade(int nodeId, out string error)
        {
            Node node = GetNode(nodeId);
            if (node == null)
            {
                error = "Node does not exist.";
                return false;
            }

            if (node.Type != NodeType.Firewall && node.Type != NodeType.Ids && node.Type != NodeType.Honeypot)
            {
                error = $"{node.Type} cannot be upgraded.";
                return false;
            }

            if (node.Level >= 2)
            {
                error = "Node is already at max level.";
                return false;
            }

            node.Level = 2;
            if (node.Type == NodeType.Honeypot)
            {
                int delta = _data.GetHoneypotCapacity(2) - _data.GetHoneypotCapacity(1);
                node.HoneypotCapacityRemaining += delta;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Neighbours of <paramref name="nodeId"/> whose connecting link is currently usable
        /// (neither endpoint isolated at <paramref name="atTime"/>), sorted ascending by node Id
        /// for deterministic traversal order.
        /// </summary>
        /// <remarks>
        /// Allocates a small list per call. Acceptable at this project's scale (grid up to 12x7 =
        /// 84 cells, at most a few dozen concurrent packets) — revisit with pooling if profiling
        /// ever shows otherwise; not yet profiled since there is no Unity editor in this
        /// environment to profile against.
        /// </remarks>
        public IReadOnlyList<int> GetActiveNeighbors(int nodeId, float atTime) =>
            GetActiveNeighbors(nodeId, atTime, false);

        /// <summary>
        /// As <see cref="GetActiveNeighbors(int, float)"/>, but when
        /// <paramref name="includeGridAdjacent"/> is true the result also contains nodes occupying
        /// orthogonally neighbouring grid cells with no link to this one — Story 005's «аномалия»
        /// route. Isolation still applies to those phantom steps, so <c>isolate</c> remains an
        /// answer to an anomaly.
        /// </summary>
        public IReadOnlyList<int> GetActiveNeighbors(int nodeId, float atTime, bool includeGridAdjacent)
        {
            if (!_adjacency.TryGetValue(nodeId, out List<int> neighborIds))
            {
                return Array.Empty<int>();
            }

            Node self = _nodesById[nodeId];
            if (self.IsIsolatedAt(atTime))
            {
                return Array.Empty<int>();
            }

            var result = new List<int>(neighborIds.Count);
            foreach (int neighborId in neighborIds)
            {
                if (!_nodesById[neighborId].IsIsolatedAt(atTime))
                {
                    result.Add(neighborId);
                }
            }

            if (includeGridAdjacent)
            {
                AddGridAdjacentNeighbors(self, atTime, result);
                result.Sort();
            }

            return result;
        }

        private void AddGridAdjacentNeighbors(Node self, float atTime, List<int> result)
        {
            for (int i = 0; i < GridStepX.Length; i++)
            {
                Node neighbor = GetNodeAt(self.X + GridStepX[i], self.Y + GridStepY[i]);
                if (neighbor == null || neighbor.IsIsolatedAt(atTime) || result.Contains(neighbor.Id))
                {
                    continue;
                }

                result.Add(neighbor.Id);
            }
        }

        /// <summary>Shortest path by hop count from <paramref name="fromNodeId"/> to <paramref name="toNodeId"/> at <paramref name="atTime"/>, or null if none exists.</summary>
        /// <remarks>
        /// Deliberately hop count, not total link length, per Story 001's acceptance criteria:
        /// with variable-length links, a 1-hop 6-cell route beats a 2-hop 2-cell route.
        /// </remarks>
        public List<int> FindShortestHopPath(int fromNodeId, int toNodeId, float atTime) =>
            FindShortestHopPath(fromNodeId, toNodeId, atTime, false);

        /// <summary>
        /// As <see cref="FindShortestHopPath(int, int, float)"/>, but when
        /// <paramref name="allowGridAdjacency"/> is true the search may also step between
        /// grid-adjacent nodes that are not linked (Story 005's «аномалия»).
        /// </summary>
        public List<int> FindShortestHopPath(int fromNodeId, int toNodeId, float atTime, bool allowGridAdjacency)
        {
            if (fromNodeId == toNodeId)
            {
                return new List<int> { fromNodeId };
            }

            var visited = new HashSet<int> { fromNodeId };
            var parent = new Dictionary<int, int>();
            var queue = new Queue<int>();
            queue.Enqueue(fromNodeId);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (int neighborId in GetActiveNeighbors(current, atTime, allowGridAdjacency))
                {
                    if (visited.Contains(neighborId))
                    {
                        continue;
                    }

                    visited.Add(neighborId);
                    parent[neighborId] = current;

                    if (neighborId == toNodeId)
                    {
                        return ReconstructPath(parent, fromNodeId, toNodeId);
                    }

                    queue.Enqueue(neighborId);
                }
            }

            return null;
        }

        public bool HasPath(int fromNodeId, int toNodeId, float atTime) => FindShortestHopPath(fromNodeId, toNodeId, atTime) != null;

        /// <summary>As <see cref="HasPath(int, int, float)"/>, optionally allowing «аномалия» steps between unlinked grid-adjacent nodes.</summary>
        public bool HasPath(int fromNodeId, int toNodeId, float atTime, bool allowGridAdjacency) =>
            FindShortestHopPath(fromNodeId, toNodeId, atTime, allowGridAdjacency) != null;

        private static List<int> ReconstructPath(Dictionary<int, int> parent, int fromNodeId, int toNodeId)
        {
            var path = new List<int> { toNodeId };
            int current = toNodeId;
            while (current != fromNodeId)
            {
                current = parent[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }

        private Node CreateNodeInternal(int x, int y, NodeType type)
        {
            var node = new Node(_nextNodeId, x, y, type);
            _nextNodeId++;

            if (type == NodeType.Honeypot)
            {
                node.HoneypotCapacityRemaining = _data.GetHoneypotCapacity(1);
            }

            _nodesById[node.Id] = node;
            _nodesInOrder.Add(node);
            _cells[x, y] = node;
            _adjacency[node.Id] = new List<int>();
            return node;
        }

        private void AddAdjacency(int fromId, int toId)
        {
            List<int> list = _adjacency[fromId];
            list.Add(toId);
            list.Sort();
        }

        private void RemoveAdjacency(int fromId, int toId)
        {
            if (_adjacency.TryGetValue(fromId, out List<int> list))
            {
                list.Remove(toId);
            }
        }
    }
}
