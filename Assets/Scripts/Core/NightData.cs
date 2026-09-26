using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>One scheduled packet spawn during a night: fires <see cref="SpawnTime"/> seconds after night start.</summary>
    /// <remarks>
    /// Story 005 added <see cref="Target"/>: a spawn may aim at something other than the Core (a
    /// DDoS wave aims at a Server). The target is resolved to a concrete node only at the moment of
    /// the spawn, since the network changes while the night runs.
    /// </remarks>
    public sealed class PacketSpawnEvent
    {
        public float SpawnTime { get; set; }
        public PacketType Type { get; set; }

        /// <summary>What this packet travels towards. Defaults to <see cref="WaveTargetKind.Core"/>, the pre-Story-005 behaviour.</summary>
        public WaveTargetKind Target { get; set; } = WaveTargetKind.Core;
    }

    /// <summary>
    /// Data describing a single playable night: its length, its waves, the intruder nodes that
    /// appear during it, how unreliable the terminal is, and whether it is the night the network
    /// finally goes out of control. Story 005 of
    /// `production/epics/night-shift/story-005-five-nights-escalation.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>Waves, not a flat list.</b> Acceptance criterion 1 asks for nights described as
    /// «волны: тип, количество, интервал, цель». <see cref="Waves"/> is that description;
    /// <see cref="NetworkSimulation.StartNight"/> expands it into timed spawns. The older flat
    /// <see cref="SpawnSchedule"/> is still honoured and is merged with the expanded waves, so a
    /// hand-authored spawn (and every Story 001 test that used one) keeps working.</para>
    ///
    /// <para><b>No balance numbers in night logic.</b> Everything the escalation needs is a field
    /// here or in <see cref="GameData"/>; the simulation reads them and the five authored nights
    /// live in <c>NightShift.Core.Data.NightLibrary</c>.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var night = new NightData
    /// {
    ///     NightNumber = 2,
    ///     NightDuration = 320f,
    ///     Waves = { new NightWave { Type = PacketType.Scan, Count = 6, StartTime = 10f, IntervalSeconds = 6f } },
    /// };
    /// sim.StartNight(night);
    /// </code>
    /// </example>
    public sealed class NightData
    {
        public int NightNumber { get; set; } = 1;

        /// <summary>Total night length in seconds. The night ends and reports when elapsed time reaches this.</summary>
        public float NightDuration { get; set; } = 120f;

        /// <summary>
        /// Individually authored spawns at the Gateway. Order does not matter —
        /// <see cref="NetworkSimulation.StartNight"/> sorts everything by
        /// <see cref="PacketSpawnEvent.SpawnTime"/> internally.
        /// </summary>
        public List<PacketSpawnEvent> SpawnSchedule { get; set; } = new List<PacketSpawnEvent>();

        /// <summary>The night's attack waves. Expanded into spawns at <see cref="NetworkSimulation.StartNight"/>.</summary>
        public List<NightWave> Waves { get; set; } = new List<NightWave>();

        /// <summary>Intruder nodes that appear by themselves during this night (acceptance criterion 3). Empty on nights 1-2.</summary>
        public List<AlienNodeSpawn> AlienNodes { get; set; } = new List<AlienNodeSpawn>();

        /// <summary>
        /// Probability in [0, 1] that a node-targeted terminal command hits the wrong node
        /// (acceptance criterion 4). 0 on nights 1-3; the roll goes through the simulation's seeded
        /// <see cref="IRandomSource"/>, so a misfire is reproducible for a given seed and a given
        /// sequence of typed commands.
        /// </summary>
        public float CommandMisfireChance { get; set; }

        /// <summary>
        /// True only for the last night: when the timer runs out the night does not report, it
        /// enters <see cref="NetworkSimulation.IsOutOfControl"/> — «сеть вне контроля» — where the
        /// only command left is <c>shutdown --all</c> and running it is the victory ending
        /// (acceptance criterion 5).
        /// </summary>
        public bool EndsOutOfControl { get; set; }
    }
}
