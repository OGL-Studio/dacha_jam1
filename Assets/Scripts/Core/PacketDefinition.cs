namespace NightShift.Core
{
    /// <summary>
    /// The tunable, data-driven profile of one packet type. Populated in <see cref="GameData"/>;
    /// never hardcoded in simulation logic, so Story 005's attack types are registered purely as
    /// data.
    /// </summary>
    /// <remarks>
    /// The three behaviour flags below are what let the simulation implement Story 005 acceptance
    /// criterion 2 without naming a single <see cref="PacketType"/> member in
    /// <see cref="NetworkSimulation"/>: the pipeline asks the definition what the packet does.
    /// </remarks>
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

        /// <summary>
        /// «DDoS»: when the packet reaches its destination Server, that Server goes offline for
        /// <see cref="ServerDownSeconds"/> instead of the packet damaging the Core.
        /// </summary>
        public bool DisablesTargetServer { get; set; }

        /// <summary>Seconds a Server stays offline after a <see cref="DisablesTargetServer"/> hit. Ignored when that flag is false.</summary>
        public float ServerDownSeconds { get; set; }

        /// <summary>
        /// «червь»: the first healthy Server the packet arrives at becomes infected and starts
        /// spawning further packets of this same type towards its own neighbours until the player
        /// runs <c>patch</c> on it.
        /// </summary>
        public bool InfectsServer { get; set; }

        /// <summary>Seconds between spawns from an infected Server. Ignored when <see cref="InfectsServer"/> is false.</summary>
        public float InfectionSpawnIntervalSeconds { get; set; }

        /// <summary>
        /// «аномалия»: the packet may route between nodes that occupy grid-adjacent cells even
        /// when no <see cref="Link"/> joins them. Isolation still stops it — <c>isolate</c> stays a
        /// universal answer.
        /// </summary>
        public bool CanCrossMissingLinks { get; set; }
    }
}
