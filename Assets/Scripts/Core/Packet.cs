namespace NightShift.Core
{
    /// <summary>
    /// A single attack packet travelling through the network. Created only by
    /// <see cref="NetworkSimulation.SpawnPacket"/>.
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

        /// <summary>Fraction of the current link travelled, in [0, 1), independent of the link's length.</summary>
        public float LinkProgress { get; internal set; }

        /// <summary>True once the packet has been removed from the simulation (blocked, leaked, or dissipated).</summary>
        public bool IsDestroyed { get; internal set; }

        internal Packet(int id, PacketType type, PacketDefinition definition, int startNodeId)
        {
            Id = id;
            Type = type;
            MaxHp = definition.MaxHp;
            CurrentHp = definition.MaxHp;
            CoreDamage = definition.CoreDamage;
            RawStartsHidden = definition.StartsHidden;
            CurrentNodeId = startNodeId;
            TargetNodeId = startNodeId;
            LinkProgress = 0f;
        }
    }
}
