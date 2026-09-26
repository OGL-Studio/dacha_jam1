namespace NightShift.Core
{
    /// <summary>
    /// A single attack packet travelling through the network. Created only by
    /// <see cref="NetworkSimulation.SpawnPacket"/> / <see cref="NetworkSimulation.SpawnPacketAt"/>.
    /// </summary>
    public sealed class Packet
    {
        public int Id { get; }
        public PacketType Type { get; }
        public int MaxHp { get; }
        public int CoreDamage { get; }

        /// <summary>Current hit points. The packet is destroyed once this reaches 0 or below.</summary>
        public int CurrentHp { get; internal set; }

        /// <summary>True if this packet was spawned hidden (see <see cref="PacketDefinition.StartsHidden"/>).</summary>
        public bool RawStartsHidden { get; }

        /// <summary>Set permanently once an IDS reveals this packet (at the IDS's own node or a node directly linked to it).</summary>
        public bool IndividuallyRevealed { get; internal set; }

        /// <summary>Node the packet is currently departing from (or resting at, between ticks).</summary>
        public int CurrentNodeId { get; internal set; }

        /// <summary>Node the packet is travelling towards on its current link.</summary>
        public int TargetNodeId { get; internal set; }

        /// <summary>
        /// The node the packet is ultimately heading for — the Core for every ordinary packet, a
        /// Server for a «DDoS» wave. The simulation re-routes to the Core if the destination
        /// becomes unreachable, so this can change during the packet's life.
        /// </summary>
        public int DestinationNodeId { get; internal set; }

        /// <summary>Fraction of the current link travelled, in [0, 1), independent of the link's length.</summary>
        public float LinkProgress { get; internal set; }

        /// <summary>True once the packet has been removed from the simulation (blocked, leaked, or dissipated).</summary>
        public bool IsDestroyed { get; internal set; }

        /// <summary>«аномалия»: this packet may step between grid-adjacent cells with no link between them.</summary>
        public bool CanCrossMissingLinks { get; }

        /// <summary>«DDoS»: reaching the destination Server takes it offline instead of damaging the Core.</summary>
        public bool DisablesTargetServer { get; }

        /// <summary>«червь»: the first healthy Server this packet reaches becomes an infection source.</summary>
        public bool InfectsServer { get; }

        internal Packet(int id, PacketType type, PacketDefinition definition, int startNodeId, int destinationNodeId)
        {
            Id = id;
            Type = type;
            MaxHp = definition.MaxHp;
            CurrentHp = definition.MaxHp;
            CoreDamage = definition.CoreDamage;
            RawStartsHidden = definition.StartsHidden;
            CanCrossMissingLinks = definition.CanCrossMissingLinks;
            DisablesTargetServer = definition.DisablesTargetServer;
            InfectsServer = definition.InfectsServer;
            CurrentNodeId = startNodeId;
            TargetNodeId = startNodeId;
            DestinationNodeId = destinationNodeId;
            LinkProgress = 0f;
        }
    }
}
