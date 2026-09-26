namespace NightShift.Core
{
    /// <summary>
    /// The machine-readable outcome of a typed terminal command. Story 004 of
    /// `production/epics/night-shift/story-004-terminal-commands.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a code and not just a sentence.</b> Every player-facing string in this game is
    /// Russian and lives in the Unity layer's <c>UiStrings</c>, but <c>NightShift.Core</c> owns the
    /// command rules. So the parser returns a code plus the data the sentence needs (node name,
    /// seconds remaining), and the view turns that into Russian. Core never holds display text, and
    /// the view never re-implements a rule in order to phrase the answer itself.</para>
    /// </remarks>
    public enum TerminalResultCode
    {
        /// <summary>The line was blank. Nothing ran and nothing should be logged beyond the echo.</summary>
        Empty = 0,

        /// <summary><c>help</c> succeeded; <see cref="TerminalCommandResult.Commands"/> carries the command list to render.</summary>
        Help = 1,

        /// <summary>The first token is not a known command.</summary>
        UnknownCommand = 2,

        /// <summary>The command needs a node argument and none was given.</summary>
        MissingArgument = 3,

        /// <summary>More tokens were typed than the command accepts.</summary>
        TooManyArguments = 4,

        /// <summary>The node argument does not name any node on the map.</summary>
        UnknownNode = 5,

        /// <summary>The command is still cooling down; <see cref="TerminalCommandResult.Seconds"/> is the time left.</summary>
        OnCooldown = 6,

        /// <summary><c>isolate</c> applied; <see cref="TerminalCommandResult.Seconds"/> is the isolation duration.</summary>
        IsolateApplied = 7,

        /// <summary><c>isolate</c> refused because the target cannot be cut off (the Gateway and the Core never can).</summary>
        IsolateRefused = 8,

        /// <summary><c>scan</c> applied; <see cref="TerminalCommandResult.Seconds"/> is the reveal duration.</summary>
        ScanApplied = 9,

        /// <summary><c>patch</c> applied: the node is back online and any isolation on it is cleared.</summary>
        PatchApplied = 10,

        /// <summary><c>patch</c> refused by the simulation.</summary>
        PatchRefused = 11,

        /// <summary>
        /// An unexpected exception was caught inside the processor. Surfaced as a normal log line so
        /// that acceptance criterion 6's "без исключений" holds even for a defect.
        /// </summary>
        InternalError = 12,
    }
}
