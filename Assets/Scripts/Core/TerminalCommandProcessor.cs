using System;
using System.Collections.Generic;

namespace NightShift.Core
{
    /// <summary>
    /// Parses and executes the player's terminal commands - <c>help</c>, <c>isolate &lt;node&gt;</c>,
    /// <c>scan</c>, <c>patch &lt;node&gt;</c> - and owns their cooldowns. Implements Story 004
    /// acceptance criteria 1-7 of
    /// `production/epics/night-shift/story-004-terminal-commands.md`; criterion 8 (the input field,
    /// the scrolling log, the up-arrow recall) is the Unity view's half.
    /// </summary>
    /// <remarks>
    /// <para><b>Engine-independent.</b> Like the rest of <c>NightShift.Core</c> this has zero
    /// <c>UnityEngine</c> references and no display text: it returns
    /// <see cref="TerminalCommandResult"/> values that the Unity terminal renders into Russian.</para>
    ///
    /// <para><b>Never throws.</b> Acceptance criterion 6 demands that an unknown command, a bad node
    /// name or a surplus argument produce a clear error "без исключений". Every rejection is a normal
    /// return value, and <see cref="Execute"/> additionally wraps the whole dispatch in a catch that
    /// converts any unexpected exception into
    /// <see cref="TerminalResultCode.InternalError"/> - a defect in a command must not take the
    /// player's night down with it.</para>
    ///
    /// <para><b>Cooldowns are per command, not per node.</b> Criterion 2 asks that repeating
    /// <c>isolate</c> on the same node before its cooldown ends be refused; a per-command cooldown
    /// satisfies that and also refuses the cheaper exploit of cycling <c>isolate</c> across different
    /// nodes every frame. The clock is <see cref="NetworkSimulation.SimulationTime"/>, so cooldowns
    /// run on simulation time: they respect the debug time scale and freeze between nights, exactly
    /// like every other timed effect in the game.</para>
    ///
    /// <para><b>A refused command costs nothing.</b> Cooldowns start only after an effect actually
    /// applied, so a typo, an unknown node or an <c>isolate gw</c> never burns the command.</para>
    ///
    /// <para><b>No balance numbers here.</b> Every duration and cooldown is read from
    /// <see cref="GameData"/> at construction.</para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var terminal = new TerminalCommandProcessor(simulation);
    /// TerminalCommandResult result = terminal.Execute("ISOLATE Srv-1"); // case-insensitive
    /// </code>
    /// </example>
    public sealed class TerminalCommandProcessor
    {
        /// <summary>The <c>help</c> command word.</summary>
        public const string HelpCommand = "help";

        /// <summary>The <c>isolate</c> command word.</summary>
        public const string IsolateCommand = "isolate";

        /// <summary>The <c>scan</c> command word.</summary>
        public const string ScanCommand = "scan";

        /// <summary>The <c>patch</c> command word.</summary>
        public const string PatchCommand = "patch";

        /// <summary>The <c>shutdown</c> command word (Story 005). Only ever legal as <c>shutdown --all</c>.</summary>
        public const string ShutdownCommand = "shutdown";

        /// <summary>The one argument <see cref="ShutdownCommand"/> accepts.</summary>
        public const string ShutdownAllArgument = "--all";

        private static readonly char[] TokenSeparators = { ' ', '\t' };

        private readonly NetworkSimulation _simulation;
        private readonly GameData _data;
        private readonly List<TerminalCommandInfo> _commands = new List<TerminalCommandInfo>();
        private readonly Dictionary<string, TerminalCommandInfo> _commandsByName =
            new Dictionary<string, TerminalCommandInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, float> _readyAtTime =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The full command table, including commands not currently available.</summary>
        public IReadOnlyList<TerminalCommandInfo> Commands => _commands;

        /// <summary>
        /// The commands the player may use right now, in <c>help</c>'s order. Story 005 acceptance
        /// criterion 5: <c>shutdown --all</c> is absent until the network goes out of control, and
        /// from that moment on it is the only thing left besides <c>help</c>.
        /// </summary>
        public IReadOnlyList<TerminalCommandInfo> AvailableCommands
        {
            get
            {
                var available = new List<TerminalCommandInfo>(_commands.Count);
                foreach (TerminalCommandInfo info in _commands)
                {
                    if (IsAvailable(info))
                    {
                        available.Add(info);
                    }
                }
                return available;
            }
        }

        /// <summary>
        /// Binds the processor to a simulation and builds the command table from
        /// <see cref="GameData"/>.
        /// </summary>
        /// <param name="simulation">Simulation the commands act on. Never null.</param>
        public TerminalCommandProcessor(NetworkSimulation simulation)
        {
            _simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            _data = simulation.Data;

            Register(new TerminalCommandInfo(HelpCommand, 0, HelpCommand, _data.HelpCooldown));
            Register(new TerminalCommandInfo(IsolateCommand, 1, IsolateCommand + " <node>", _data.IsolateCooldown));
            Register(new TerminalCommandInfo(ScanCommand, 0, ScanCommand, _data.ScanCooldown));
            Register(new TerminalCommandInfo(PatchCommand, 1, PatchCommand + " <node>", _data.PatchCooldown));
            Register(new TerminalCommandInfo(
                ShutdownCommand,
                1,
                ShutdownCommand + " " + ShutdownAllArgument,
                _data.ShutdownCooldown,
                availableOnlyOutOfControl: true));
        }

        /// <summary>
        /// Whether a command is usable in the simulation's current state. <c>help</c> stays available
        /// in the out-of-control end state on purpose: the one command still left has to be
        /// discoverable, and listing it changes nothing in the simulation.
        /// </summary>
        public bool IsAvailable(TerminalCommandInfo info)
        {
            if (info == null)
            {
                return false;
            }

            if (info.AvailableOnlyOutOfControl)
            {
                return _simulation.IsOutOfControl;
            }

            return !_simulation.IsOutOfControl || info.Name == HelpCommand;
        }

        /// <summary>Metadata for a command word, or null if no such command exists. Case-insensitive.</summary>
        public TerminalCommandInfo FindCommand(string name) =>
            name != null && _commandsByName.TryGetValue(name, out TerminalCommandInfo info) ? info : null;

        /// <summary>
        /// Seconds of cooldown still owed by a command, or 0 when it is ready (or unknown).
        /// </summary>
        public float GetCooldownRemaining(string commandName)
        {
            if (commandName == null || !_readyAtTime.TryGetValue(commandName, out float readyAt))
            {
                return 0f;
            }

            float remaining = readyAt - _simulation.SimulationTime;
            return remaining > 0f ? remaining : 0f;
        }

        /// <summary>
        /// Parses and runs one typed line. Returns a structured result for the view to render; never
        /// throws and never returns null.
        /// </summary>
        /// <param name="line">Raw input, exactly as the player typed it. Null and blank are accepted.</param>
        public TerminalCommandResult Execute(string line)
        {
            try
            {
                return ExecuteCore(line);
            }
            catch (Exception exception)
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.InternalError,
                    string.Empty,
                    line ?? string.Empty,
                    string.Empty,
                    0f,
                    exception.GetType().Name + ": " + exception.Message);
            }
        }

        private TerminalCommandResult ExecuteCore(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return TerminalCommandResult.Blank();
            }

            string[] tokens = line.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return TerminalCommandResult.Blank();
            }

            string commandName = tokens[0];
            TerminalCommandInfo info = FindCommand(commandName);
            if (info == null)
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.UnknownCommand, commandName, string.Empty, string.Empty, 0f,
                    "Unknown command '" + commandName + "'.");
            }

            // Story 005 criterion 5: availability is decided before anything else, so a command that
            // does not exist in this state cannot spend a cooldown or touch the simulation.
            if (!IsAvailable(info))
            {
                TerminalResultCode code = info.AvailableOnlyOutOfControl
                    ? TerminalResultCode.ShutdownUnavailable
                    : TerminalResultCode.CommandLockedOutOfControl;

                return TerminalCommandResult.Rejected(
                    code, info.Name, string.Empty, string.Empty, 0f,
                    "'" + info.Name + "' is not available in the current state.");
            }

            int suppliedArguments = tokens.Length - 1;
            if (suppliedArguments < info.ArgumentCount)
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.MissingArgument, info.Name, string.Empty, string.Empty, 0f,
                    "'" + info.Usage + "' needs " + info.ArgumentCount + " argument(s).");
            }

            if (suppliedArguments > info.ArgumentCount)
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.TooManyArguments, info.Name, tokens[1], string.Empty, 0f,
                    "'" + info.Usage + "' takes " + info.ArgumentCount + " argument(s), got " + suppliedArguments + ".");
            }

            string argument = info.ArgumentCount > 0 ? tokens[1] : string.Empty;

            // Criterion 5: a command invoked during its cooldown reports the time left and does
            // nothing at all - checked before the argument is resolved, so the answer is the same
            // whichever node was named.
            float remaining = GetCooldownRemaining(info.Name);
            if (remaining > 0f)
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.OnCooldown, info.Name, argument, string.Empty, remaining,
                    "'" + info.Name + "' is on cooldown for " + remaining + "s.");
            }

            switch (info.Name)
            {
                case HelpCommand:
                    return StartCooldown(info, TerminalCommandResult.HelpListing(info.Name, AvailableCommands));
                case ScanCommand:
                    return ExecuteScan(info);
                case IsolateCommand:
                    return ExecuteIsolate(info, argument);
                case PatchCommand:
                    return ExecutePatch(info, argument);
                case ShutdownCommand:
                    return ExecuteShutdown(info, argument);
                default:
                    // Unreachable while every registered command is handled above; kept so that
                    // adding a table entry without a case degrades into a clear error, not a crash.
                    return TerminalCommandResult.Rejected(
                        TerminalResultCode.UnknownCommand, info.Name, argument, string.Empty, 0f,
                        "Command '" + info.Name + "' has no implementation.");
            }
        }

        private TerminalCommandResult ExecuteScan(TerminalCommandInfo info)
        {
            float duration = _data.ScanDuration;
            _simulation.Reveal(duration);
            return StartCooldown(info, TerminalCommandResult.Applied(
                TerminalResultCode.ScanApplied, info.Name, string.Empty, string.Empty, duration));
        }

        private TerminalCommandResult ExecuteIsolate(TerminalCommandInfo info, string argument)
        {
            if (!NodeNaming.TryResolve(_simulation.Graph, argument, out Node intended))
            {
                return UnknownNode(info, argument);
            }

            // Story 005 criterion 4: on the late nights the command may land on another node. The
            // roll is the simulation's (seeded) business; the processor only reports what it hit.
            bool misfired = _simulation.TryMisfireTarget(intended, mustBeIsolatable: true, out Node node);

            string canonical = NodeNaming.GetName(_simulation.Graph, node);
            float duration = _data.IsolateDuration;

            if (!_simulation.Isolate(node.Id, duration))
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.IsolateRefused, info.Name, argument, canonical, 0f,
                    "'" + canonical + "' cannot be isolated.");
            }

            return StartCooldown(info, BuildApplied(
                TerminalResultCode.IsolateApplied, info, argument, canonical, duration, misfired, intended));
        }

        private TerminalCommandResult ExecutePatch(TerminalCommandInfo info, string argument)
        {
            if (!NodeNaming.TryResolve(_simulation.Graph, argument, out Node intended))
            {
                return UnknownNode(info, argument);
            }

            bool misfired = _simulation.TryMisfireTarget(intended, mustBeIsolatable: false, out Node node);

            string canonical = NodeNaming.GetName(_simulation.Graph, node);

            if (!_simulation.Patch(node.Id))
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.PatchRefused, info.Name, argument, canonical, 0f,
                    "'" + canonical + "' could not be patched.");
            }

            return StartCooldown(info, BuildApplied(
                TerminalResultCode.PatchApplied, info, argument, canonical, 0f, misfired, intended));
        }

        /// <summary>
        /// The <c>shutdown --all</c> ending (Story 005 criterion 5). Availability was already checked
        /// in <see cref="ExecuteCore"/>, so all that is left is the argument spelling and the call.
        /// </summary>
        private TerminalCommandResult ExecuteShutdown(TerminalCommandInfo info, string argument)
        {
            if (!string.Equals(argument, ShutdownAllArgument, StringComparison.OrdinalIgnoreCase))
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.InvalidArgument, info.Name, argument, string.Empty, 0f,
                    "'" + info.Usage + "' is the only accepted form.");
            }

            if (!_simulation.ShutdownAll())
            {
                return TerminalCommandResult.Rejected(
                    TerminalResultCode.ShutdownUnavailable, info.Name, argument, string.Empty, 0f,
                    "The network is not out of control.");
            }

            return StartCooldown(info, TerminalCommandResult.Applied(
                TerminalResultCode.ShutdownApplied, info.Name, argument, string.Empty, 0f));
        }

        /// <summary>Success result for a node-targeted command, misfire-aware so both callers phrase it identically.</summary>
        private TerminalCommandResult BuildApplied(
            TerminalResultCode code,
            TerminalCommandInfo info,
            string argument,
            string canonicalNodeName,
            float seconds,
            bool misfired,
            Node intended)
        {
            if (!misfired)
            {
                return TerminalCommandResult.Applied(code, info.Name, argument, canonicalNodeName, seconds);
            }

            return TerminalCommandResult.AppliedMisfire(
                code,
                info.Name,
                argument,
                canonicalNodeName,
                NodeNaming.GetName(_simulation.Graph, intended),
                seconds);
        }

        private static TerminalCommandResult UnknownNode(TerminalCommandInfo info, string argument) =>
            TerminalCommandResult.Rejected(
                TerminalResultCode.UnknownNode, info.Name, argument, string.Empty, 0f,
                "No node is named '" + argument + "'.");

        /// <summary>
        /// Arms a command's cooldown and passes its result straight through, so that every success
        /// path starts the cooldown in exactly one place and no rejection path can start one.
        /// </summary>
        private TerminalCommandResult StartCooldown(TerminalCommandInfo info, TerminalCommandResult result)
        {
            _readyAtTime[info.Name] = _simulation.SimulationTime + (info.Cooldown > 0f ? info.Cooldown : 0f);
            return result;
        }

        private void Register(TerminalCommandInfo info)
        {
            _commands.Add(info);
            _commandsByName[info.Name] = info;
        }
    }
}
