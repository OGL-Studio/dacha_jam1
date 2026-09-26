namespace NightShift.Game
{
    /// <summary>
    /// Which half of the game loop is running. Owned by <see cref="GameRunner"/>; every day-phase
    /// view and the night HUD switch themselves on and off from
    /// <see cref="GameRunner.OnPhaseChanged"/>.
    /// </summary>
    /// <remarks>
    /// The brief's loop is День -> Ночь -> Утро (отчёт) -> следующий День. <see cref="Morning"/> is
    /// a distinct phase rather than "day with the report open" because build input must be dead
    /// while the report is up: the night is over, the report already quotes the final numbers, and
    /// letting the player spend during it would make the report lie.
    /// </remarks>
    public enum GamePhase
    {
        /// <summary>Building: the shop, placement, linking and upgrades are live; the simulation is not ticking.</summary>
        Day = 0,

        /// <summary>The night is running: the simulation ticks and all build input is refused.</summary>
        Night = 1,

        /// <summary>The night has ended (or the Core fell) and the shift report is on screen.</summary>
        Morning = 2,
    }
}
