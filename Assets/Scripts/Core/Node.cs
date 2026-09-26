namespace NightShift.Core
{
    /// <summary>
    /// A single occupied cell of the network grid. Nodes are created exclusively by
    /// <see cref="NetworkGraph"/> (via placement, the fixed Gateway/Core seeding, or a Story 005
    /// alien intrusion); identity (<see cref="Id"/>, <see cref="X"/>, <see cref="Y"/>,
    /// <see cref="Type"/>, <see cref="IsAlien"/>) never changes after creation.
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

        /// <summary>
        /// True for a node the network grew by itself (Story 005 acceptance criterion 3). An alien
        /// node routes packets like any other and can be isolated, but it earns the player nothing
        /// and <see cref="CanBeRemovedByPlayer"/> is false for it — «их нельзя продать, только
        /// изолировать».
        /// </summary>
        public bool IsAlien { get; internal set; }

        /// <summary>
        /// Simulation-clock time at which a «DDoS» knock-out expires and the node comes back online
        /// by itself. <see cref="float.NegativeInfinity"/> when the node was never downed.
        /// </summary>
        /// <remarks>
        /// Distinct from <see cref="IsOnline"/> on purpose: <see cref="IsOnline"/> is the current
        /// fact, this is the scheduled recovery. <see cref="NetworkSimulation.Patch"/> clears the
        /// timer and brings the node up immediately, which is what makes <c>patch</c> worth typing
        /// rather than waiting.
        /// </remarks>
        public float DownedUntilTime { get; internal set; } = float.NegativeInfinity;

        /// <summary>
        /// True while a «червь» has this Server: it spawns further worms towards its neighbours
        /// every <see cref="PacketDefinition.InfectionSpawnIntervalSeconds"/> until
        /// <see cref="NetworkSimulation.Patch"/> clears it.
        /// </summary>
        public bool IsInfected { get; internal set; }

        /// <summary>Simulation-clock time of this infected node's next worm spawn. Meaningless while <see cref="IsInfected"/> is false.</summary>
        public float InfectionNextSpawnTime { get; internal set; } = float.NegativeInfinity;

        /// <summary>
        /// The packet type this infected node re-emits — the type that infected it, so the spread
        /// keeps the profile of whatever arrived rather than a type hardcoded in the simulation.
        /// Meaningless while <see cref="IsInfected"/> is false.
        /// </summary>
        public PacketType InfectionPacketType { get; internal set; }

        /// <summary>
        /// Whether the player is allowed to take this node out of the network. False for the fixed
        /// Gateway and Core and for every <see cref="IsAlien"/> node. The single place any future
        /// sell/demolish action must consult — there is no such action in the game yet, so today
        /// this is the standing guard rather than an active check.
        /// </summary>
        public bool CanBeRemovedByPlayer =>
            !IsAlien && Type != NodeType.Gateway && Type != NodeType.Core;

        internal Node(int id, int x, int y, NodeType type)
        {
            Id = id;
            X = x;
            Y = y;
            Type = type;
        }

        /// <summary>True while <paramref name="atTime"/> falls inside this node's isolation window.</summary>
        public bool IsIsolatedAt(float atTime) => atTime < IsolatedUntilTime;

        /// <summary>True while <paramref name="atTime"/> falls inside this node's «DDoS» downtime window.</summary>
        public bool IsDownedAt(float atTime) => atTime < DownedUntilTime;
    }
}
