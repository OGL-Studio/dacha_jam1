using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>One scheduled packet spawn during a night: fires <see cref="SpawnTime"/> seconds after night start.</summary>
    public sealed class PacketSpawnEvent
    {
        public float SpawnTime { get; set; }
        public PacketType Type { get; set; }
    }

    /// <summary>
    /// Data describing a single playable night. Story 001 ships plain night 1 data; Story 005
    /// supplies nights 2-5 (escalating attack types, denser schedules) as more instances of this
    /// same class — no change to <see cref="NetworkSimulation"/> is required.
    /// </summary>
    /// <example>
    /// <code>
    /// var night1 = new NightData
    /// {
    ///     NightNumber = 1,
    ///     NightDuration = 120f,
    ///     SpawnSchedule = { new PacketSpawnEvent { SpawnTime = 5f, Type = PacketType.Standard } },
    /// };
    /// sim.StartNight(night1);
    /// </code>
    /// </example>
    public sealed class NightData
    {
        public int NightNumber { get; set; } = 1;

        /// <summary>Total night length in seconds. The night ends and reports when elapsed time reaches this.</summary>
        public float NightDuration { get; set; } = 120f;

        /// <summary>Packets to spawn at Gateway. Order does not matter — <see cref="NetworkSimulation.StartNight"/> sorts by <see cref="PacketSpawnEvent.SpawnTime"/> internally.</summary>
        public List<PacketSpawnEvent> SpawnSchedule { get; set; } = new List<PacketSpawnEvent>();
    }
}
