namespace NightShift.Core
{
    /// <summary>
    /// The tunable, data-driven profile of one packet type. Populated in <see cref="GameData"/>;
    /// never hardcoded in simulation logic, so Story 005 can register new attack types purely as
    /// data.
    /// </summary>
    public sealed class PacketDefinition
    {
        public PacketType Type { get; set; }

        /// <summary>
        /// Movement speed in grid cells per second, before IDS slow effects. Crossing a link of
        /// Manhattan length L takes L / speed seconds.
        /// </summary>
        public float SpeedCellsPerSecond { get; set; }

        /// <summary>Starting hit points. Consumed by Firewall filtering; the packet is destroyed at 0.</summary>
        public int MaxHp { get; set; }

        /// <summary>Core integrity damage dealt if this packet reaches the Core.</summary>
        public int CoreDamage { get; set; }

        /// <summary>Whether the packet starts hidden from Firewalls until an IDS reveals it.</summary>
        public bool StartsHidden { get; set; }
    }
}
