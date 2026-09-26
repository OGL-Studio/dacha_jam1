namespace NightShift.Core
{
    /// <summary>
    /// A single occupied cell of the network grid. Nodes are created exclusively by
    /// <see cref="NetworkGraph"/> (via placement or the fixed Gateway/Core seeding); identity
    /// (<see cref="Id"/>, <see cref="X"/>, <see cref="Y"/>, <see cref="Type"/>) never changes
    /// after creation.
    /// </summary>
    /// <example>
    /// <code>
    /// var graph = new NetworkGraph(new GameData());
    /// Node gateway = graph.GatewayNode;
    /// System.Console.WriteLine($"Gateway at ({gateway.X},{gateway.Y})");
    /// </code>
    /// </example>
    public sealed class Node
    {
        /// <summary>Stable identity, assigned sequentially at creation. Never reused.</summary>
        public int Id { get; }

        /// <summary>Grid column, 0-based from the left edge.</summary>
        public int X { get; }

        /// <summary>Grid row, 0-based from the top edge.</summary>
        public int Y { get; }

        /// <summary>The node's role in the network.</summary>
        public NodeType Type { get; }

        /// <summary>
        /// Security tool level: 1 (base) or 2 (upgraded). Meaningless for Gateway/Server/Core,
        /// which always report 1.
        /// </summary>
        public int Level { get; internal set; } = 1;

        /// <summary>
        /// Whether the node is functioning. Servers only produce income while online; security
        /// tools only apply their effects while online. Gateway and Core ignore this flag.
        /// </summary>
        public bool IsOnline { get; set; } = true;

        /// <summary>Remaining catch capacity for a Honeypot. Unused for other node types.</summary>
        public int HoneypotCapacityRemaining { get; internal set; }

        /// <summary>
        /// Simulation-clock time at which this node's isolation (see
        /// <see cref="NetworkSimulation.Isolate"/>) expires. A value less than or equal to the
        /// queried time means the node is not currently isolated.
        /// </summary>
        public float IsolatedUntilTime { get; internal set; } = float.NegativeInfinity;

        internal Node(int id, int x, int y, NodeType type)
        {
            Id = id;
            X = x;
            Y = y;
            Type = type;
        }

        /// <summary>True while <paramref name="atTime"/> falls inside this node's isolation window.</summary>
        public bool IsIsolatedAt(float atTime) => atTime < IsolatedUntilTime;
    }
}
