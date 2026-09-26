namespace NightShift.Core
{
    /// <summary>
    /// What a <see cref="NightWave"/>'s packets are aiming at — the «цель» column of Story 005
    /// acceptance criterion 1. Resolved to a concrete node id at spawn time, because the set of
    /// Servers changes while a night runs.
    /// </summary>
    public enum WaveTargetKind
    {
        /// <summary>The Core, as in every night before Story 005.</summary>
        Core = 0,

        /// <summary>
        /// One Server, chosen at spawn time through the seeded <see cref="IRandomSource"/>. Falls
        /// back to the Core when the network currently has no Server to aim at.
        /// </summary>
        Server = 1,
    }
}
