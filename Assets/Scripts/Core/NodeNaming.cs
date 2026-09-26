using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// The single source of truth for the short terminal names of nodes (<c>gw</c>, <c>core</c>,
    /// <c>srv-1</c>, <c>fw-2</c>...). Implements Story 004 acceptance criterion 7 of
    /// `production/epics/night-shift/story-004-terminal-commands.md`: the names the terminal parses
    /// and the labels drawn on the map must be the same names, so both the parser and
    /// <c>NetworkMapView</c>'s labels call into here and cannot drift apart.
    /// </summary>
    /// <remarks>
    /// <para><b>Derived, never stored.</b> <see cref="Node"/> deliberately gains no name field: a
    /// name is a pure function of the node's <see cref="NodeType"/> and its ordinal among the nodes
    /// of that type, in <see cref="NetworkGraph.AllNodes"/> order. That order is ascending by
    /// <see cref="Node.Id"/> and ids are never reused, and <see cref="NetworkGraph"/> has no node
    /// removal at all, so a node's name is stable for the whole session and the mapping is
    /// one-to-one in both directions.</para>
    ///
    /// <para><b>Gateway and Core carry no ordinal</b> - there is exactly one of each, seeded by
    /// <see cref="NetworkGraph"/>'s constructor, so <c>gw</c> and <c>core</c> read better than
    /// <c>gw-1</c> and <c>core-1</c>.</para>
    ///
    /// <para><b>Cost.</b> <see cref="GetName"/> is O(nodes) and <see cref="TryResolve"/> is
    /// therefore O(nodes^2). At this project's scale (a 12x7 grid, so at most 84 nodes, and one
    /// resolve per typed command) that is irrelevant, and it buys having no cache that could go
    /// stale after a placement.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// string label = NodeNaming.GetName(sim.Graph, node);          // "srv-1"
    /// NodeNaming.TryResolve(sim.Graph, "SRV-1", out Node found);   // case-insensitive
    /// </code>
    /// </example>
    public static class NodeNaming
    {
        /// <summary>Terminal name of the single Gateway node.</summary>
        public const string GatewayName = "gw";

        /// <summary>Terminal name of the single Core node.</summary>
        public const string CoreName = "core";

        /// <summary>Name prefix of Server nodes, before the <c>-N</c> ordinal.</summary>
        public const string ServerPrefix = "srv";

        /// <summary>Name prefix of Firewall nodes, before the <c>-N</c> ordinal.</summary>
        public const string FirewallPrefix = "fw";

        /// <summary>Name prefix of IDS nodes, before the <c>-N</c> ordinal.</summary>
        public const string IdsPrefix = "ids";

        /// <summary>Name prefix of Honeypot nodes, before the <c>-N</c> ordinal.</summary>
        public const string HoneypotPrefix = "hp";

        /// <summary>Separator between a name prefix and its ordinal.</summary>
        public const string OrdinalSeparator = "-";

        /// <summary>
        /// Name prefix of the nodes the network grew by itself (Story 005 acceptance criterion 3),
        /// before the <c>-N</c> ordinal. Deliberately unlike every other prefix: a name the player
        /// does not recognise is the point.
        /// </summary>
        public const string AlienPrefix = "x";

        /// <summary>
        /// The name prefix used for a specific node: <see cref="AlienPrefix"/> for an intruder,
        /// otherwise its type's prefix. Ordinals are counted per prefix, so an alien Server and the
        /// player's own Servers never collide.
        /// </summary>
        public static string GetPrefix(Node node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return node.IsAlien ? AlienPrefix : GetPrefix(node.Type);
        }

        /// <summary>The name prefix used for a node type. Unique per type, so names never collide across types.</summary>
        public static string GetPrefix(NodeType type)
        {
            switch (type)
            {
                case NodeType.Gateway: return GatewayName;
                case NodeType.Core: return CoreName;
                case NodeType.Server: return ServerPrefix;
                case NodeType.Firewall: return FirewallPrefix;
                case NodeType.Ids: return IdsPrefix;
                case NodeType.Honeypot: return HoneypotPrefix;
                default: return type.ToString().ToLowerInvariant();
            }
        }

        /// <summary>True for the two fixed, one-per-game node types, whose names carry no ordinal.</summary>
        public static bool IsSingleton(NodeType type) => type == NodeType.Gateway || type == NodeType.Core;

        /// <summary>
        /// The terminal name of <paramref name="node"/>, e.g. <c>gw</c>, <c>core</c>, <c>srv-1</c>.
        /// Always lower-case; <see cref="TryResolve"/> accepts any casing.
        /// </summary>
        /// <param name="graph">Graph that owns the node - supplies the per-type ordering.</param>
        /// <param name="node">Node to name.</param>
        public static string GetName(NetworkGraph graph, Node node)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            string prefix = GetPrefix(node);
            if (IsSingleton(node.Type) && !node.IsAlien)
            {
                return prefix;
            }

            int ordinal = 0;
            IReadOnlyList<Node> all = graph.AllNodes;
            for (int i = 0; i < all.Count; i++)
            {
                if (GetPrefix(all[i]) != prefix)
                {
                    continue;
                }

                ordinal++;
                if (all[i].Id == node.Id)
                {
                    return prefix + OrdinalSeparator + ordinal;
                }
            }

            // The node does not belong to this graph. Fall back to its id rather than throwing:
            // naming is used on the render path, which must never fail on odd input.
            return prefix + OrdinalSeparator + "id" + node.Id;
        }

        /// <summary>
        /// Finds the node whose <see cref="GetName"/> equals <paramref name="name"/>, ignoring case
        /// and surrounding whitespace (acceptance criterion 7's "ввод регистронезависимый").
        /// </summary>
        /// <param name="graph">Graph to search.</param>
        /// <param name="name">Name as typed by the player. Null, empty and unknown all return false.</param>
        /// <param name="node">The matched node, or null when this returns false.</param>
        public static bool TryResolve(NetworkGraph graph, string name, out Node node)
        {
            node = null;

            if (graph == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            string trimmed = name.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            IReadOnlyList<Node> all = graph.AllNodes;
            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(GetName(graph, all[i]), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    node = all[i];
                    return true;
                }
            }

            return false;
        }
    }
}
