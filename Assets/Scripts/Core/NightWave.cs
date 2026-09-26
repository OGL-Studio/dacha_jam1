namespace NightShift.Core
{
    /// <summary>
    /// One wave of an attack: «тип, количество, интервал, цель» — Story 005 acceptance criterion 1
    /// of `production/epics/night-shift/story-005-five-nights-escalation.md`. A night is a list of
    /// these; <see cref="NetworkSimulation.StartNight"/> expands every wave into the timed spawn
    /// schedule, so no wave logic ever needs a literal number.
    /// </summary>
    /// <example>
    /// <code>
    /// // Six weak DDoS packets, one every four seconds, starting 90s in, aimed at a Server.
    /// var wave = new NightWave
    /// {
    ///     Type = PacketType.Ddos,
    ///     Count = 6,
    ///     StartTime = 90f,
    ///     IntervalSeconds = 4f,
    ///     Target = WaveTargetKind.Server,
    /// };
    /// </code>
    /// </example>
    public sealed class NightWave
    {
        /// <summary>Which attack type this wave sends.</summary>
        public PacketType Type { get; set; } = PacketType.Standard;

        /// <summary>How many packets the wave sends. Zero or negative sends nothing.</summary>
        public int Count { get; set; }

        /// <summary>Seconds after night start at which the first packet of the wave spawns.</summary>
        public float StartTime { get; set; }

        /// <summary>Seconds between consecutive packets of the wave.</summary>
        public float IntervalSeconds { get; set; }

        /// <summary>What the wave's packets travel towards.</summary>
        public WaveTargetKind Target { get; set; } = WaveTargetKind.Core;

        /// <summary>
        /// Maximum absolute spawn-time jitter in seconds, applied per packet through the seeded
        /// <see cref="IRandomSource"/> so a wave does not read as a metronome. Zero means exact
        /// intervals. Jitter never moves a spawn before 0 or past the night's end.
        /// </summary>
        public float JitterSeconds { get; set; }
    }
}
