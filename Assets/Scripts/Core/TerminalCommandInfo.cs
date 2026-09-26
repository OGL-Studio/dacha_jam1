namespace NightShift.Core
{
    /// <summary>
    /// One entry of the terminal's command table: its name, how many arguments it takes, and the
    /// usage line <c>help</c> prints. Story 004 acceptance criterion 1.
    /// </summary>
    /// <remarks>
    /// Descriptions are deliberately absent: they are Russian player-facing text and therefore
    /// belong to the Unity layer's <c>UiStrings</c>, keyed by <see cref="Name"/>. Core owns the
    /// table, the view owns the words.
    /// </remarks>
    public sealed class TerminalCommandInfo
    {
        /// <summary>Lower-case command word, as typed. Matching is case-insensitive.</summary>
        public string Name { get; }

        /// <summary>How many arguments the command requires - exactly, not at least.</summary>
        public int ArgumentCount { get; }

        /// <summary>Usage line for <c>help</c>, e.g. <c>isolate &lt;node&gt;</c>.</summary>
        public string Usage { get; }

        /// <summary>Seconds this command is unavailable for after a successful run.</summary>
        public float Cooldown { get; }

        public TerminalCommandInfo(string name, int argumentCount, string usage, float cooldown)
        {
            Name = name;
            ArgumentCount = argumentCount;
            Usage = usage;
            Cooldown = cooldown;
        }
    }
}
