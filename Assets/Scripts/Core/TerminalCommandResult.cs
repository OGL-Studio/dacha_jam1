using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// The structured outcome of one line typed into the terminal: a success flag, a
    /// <see cref="TerminalResultCode"/>, and whatever data the view needs to phrase the answer.
    /// Story 004 of `production/epics/night-shift/story-004-terminal-commands.md`.
    /// </summary>
    /// <remarks>
    /// Immutable, and produced only through the static factory methods so that no code path can
    /// build a result whose code and payload disagree. <see cref="Diagnostic"/> is an English
    /// fallback for logs and tests; the player never sees it - the Unity layer renders
    /// <see cref="Code"/> through <c>UiStrings</c> instead.
    /// </remarks>
    /// <example>
    /// <code>
    /// TerminalCommandResult result = processor.Execute("isolate srv-1");
    /// if (!result.Success) { /* result.Code says why, result.Seconds says for how long */ }
    /// </code>
    /// </example>
    public sealed class TerminalCommandResult
    {
        private static readonly IReadOnlyList<TerminalCommandInfo> NoCommands = Array.Empty<TerminalCommandInfo>();

        /// <summary>True only when the command actually took effect.</summary>
        public bool Success { get; }

        /// <summary>What happened, in machine-readable form.</summary>
        public TerminalResultCode Code { get; }

        /// <summary>The command word as typed (trimmed, original casing), or an empty string for a blank line.</summary>
        public string CommandName { get; }

        /// <summary>The argument as typed, or an empty string when the command takes none.</summary>
        public string Argument { get; }

        /// <summary>
        /// Canonical name of the node the command acted on (see <see cref="NodeNaming.GetName"/>),
        /// or an empty string. Always the canonical lower-case form, never the player's casing.
        /// </summary>
        public string NodeName { get; }

        /// <summary>
        /// A duration in seconds whose meaning depends on <see cref="Code"/>: time remaining for
        /// <see cref="TerminalResultCode.OnCooldown"/>, effect duration for
        /// <see cref="TerminalResultCode.IsolateApplied"/> and
        /// <see cref="TerminalResultCode.ScanApplied"/>, otherwise 0.
        /// </summary>
        public float Seconds { get; }

        /// <summary>The command table, populated only for <see cref="TerminalResultCode.Help"/>.</summary>
        public IReadOnlyList<TerminalCommandInfo> Commands { get; }

        /// <summary>Non-localised description for logs and tests. Not player-facing.</summary>
        public string Diagnostic { get; }

        private TerminalCommandResult(
            bool success,
            TerminalResultCode code,
            string commandName,
            string argument,
            string nodeName,
            float seconds,
            IReadOnlyList<TerminalCommandInfo> commands,
            string diagnostic)
        {
            Success = success;
            Code = code;
            CommandName = commandName ?? string.Empty;
            Argument = argument ?? string.Empty;
            NodeName = nodeName ?? string.Empty;
            Seconds = seconds;
            Commands = commands ?? NoCommands;
            Diagnostic = diagnostic ?? string.Empty;
        }

        /// <summary>A blank input line: no command ran, and nothing failed either.</summary>
        public static TerminalCommandResult Blank() => new TerminalCommandResult(
            false, TerminalResultCode.Empty, string.Empty, string.Empty, string.Empty, 0f, null, "Empty input.");

        /// <summary>The <c>help</c> listing.</summary>
        public static TerminalCommandResult HelpListing(string commandName, IReadOnlyList<TerminalCommandInfo> commands) =>
            new TerminalCommandResult(
                true, TerminalResultCode.Help, commandName, string.Empty, string.Empty, 0f, commands, "Help listing.");

        /// <summary>A successful effect, optionally carrying the affected node and the effect duration.</summary>
        public static TerminalCommandResult Applied(
            TerminalResultCode code, string commandName, string argument, string nodeName, float seconds) =>
            new TerminalCommandResult(
                true, code, commandName, argument, nodeName, seconds, null, code + " on '" + nodeName + "'.");

        /// <summary>A rejection. Nothing in the simulation changed and no cooldown was started.</summary>
        public static TerminalCommandResult Rejected(
            TerminalResultCode code, string commandName, string argument, string nodeName, float seconds, string diagnostic) =>
            new TerminalCommandResult(false, code, commandName, argument, nodeName, seconds, null, diagnostic);
    }
}
