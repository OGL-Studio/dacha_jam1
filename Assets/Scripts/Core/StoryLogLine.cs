namespace NightShift.Core
{
    /// <summary>
    /// One story line scheduled to appear in the terminal log during a night. Story 006 acceptance
    /// criterion 3 of `production/epics/night-shift/story-006-story-and-screens.md`: «во время ночи в
    /// лог приходят сюжетные реплики по таймингу из данных».
    /// </summary>
    /// <remarks>
    /// <b>The timing is data, not code.</b> <see cref="TimeSeconds"/> is measured from the start of
    /// the night, against <c>NetworkSimulation.NightElapsedTime</c> - i.e. simulation time, so the
    /// debug fast-forward replays the same script at x4 instead of desynchronising it from the waves.
    /// </remarks>
    public sealed class StoryLogLine
    {
        /// <summary>Seconds after the night started at which the line is printed.</summary>
        public float TimeSeconds { get; set; }

        /// <summary>The line, already in Russian. Printed verbatim.</summary>
        public string Text { get; set; }

        /// <summary>
        /// True for lines that come from whatever the network has become, rather than from a
        /// colleague. The view prints these in the warning colour.
        /// </summary>
        public bool Corrupted { get; set; }
    }
}
