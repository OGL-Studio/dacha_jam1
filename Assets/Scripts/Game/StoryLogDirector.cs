using System.Collections.Generic;
using NightShift.Core;
using NightShift.Core.Data;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Pushes the night's authored story lines into the terminal log at their authored times.
    /// Implements Story 006 acceptance criterion 3 of
    /// `production/epics/night-shift/story-006-story-and-screens.md` («во время ночи в лог приходят
    /// сюжетные реплики по таймингу из данных»).
    /// </summary>
    /// <remarks>
    /// <para><b>Simulation time, not wall-clock time.</b> The cursor is compared against
    /// <see cref="NetworkSimulation.NightElapsedTime"/>, which <see cref="GameRunner"/> advances by
    /// <c>Time.deltaTime * SpeedMultiplier</c>. So the script stays locked to the waves it comments
    /// on: at the debug x4 the lines arrive four times sooner in real seconds and at exactly the same
    /// point of the night. A <c>Time.time</c> schedule would drift apart from the attacks.</para>
    ///
    /// <para><b>One-way cursor.</b> Lines are pre-sorted by <see cref="StoryLibrary.GetNightLines"/>
    /// and consumed with a moving index, so a frame long enough to pass several entries prints all of
    /// them in order and nothing can print twice. The cursor resets on every
    /// <see cref="GamePhase.Night"/> entry, which is also what makes a restarted campaign replay the
    /// script from the top.</para>
    ///
    /// <para><b>Owns no text and no layout.</b> The sentences come from <c>NightShift.Core.Data</c> and
    /// the rendering from <see cref="TerminalView.AppendStoryLine"/>; this class only decides
    /// <i>when</i>.</para>
    /// </remarks>
    public sealed class StoryLogDirector : MonoBehaviour
    {
        private GameRunner _runner;
        private TerminalView _terminal;
        private NetworkSimulation _simulation;

        private IReadOnlyList<StoryLogLine> _lines;
        private int _next;

        /// <summary>How many of this night's lines have already been printed. Useful in a harness.</summary>
        public int PrintedLineCount => _next;

        /// <summary>
        /// Subscribes to the phase machine. Safe to call before the first night starts.
        /// </summary>
        /// <param name="runner">Phase source, and the owner of the night clock.</param>
        /// <param name="simulation">Read for <see cref="NetworkSimulation.NightElapsedTime"/>.</param>
        /// <param name="terminal">Where the lines are printed.</param>
        public void Initialize(GameRunner runner, NetworkSimulation simulation, TerminalView terminal)
        {
            _runner = runner;
            _simulation = simulation;
            _terminal = terminal;

            if (_runner != null)
            {
                _runner.OnPhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.OnPhaseChanged -= HandlePhaseChanged;
            }
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.Night)
            {
                _lines = null;
                return;
            }

            _lines = StoryLibrary.GetNightLines(_runner != null ? _runner.DayNumber : 1);
            _next = 0;
        }

        private void Update()
        {
            if (_lines == null || _simulation == null || _terminal == null)
            {
                return;
            }

            // Stop feeding the log the moment the night ends: the report or the ending screen is on
            // its way up, and a line arriving behind it would be read after its own night finished.
            if (!_simulation.IsNightActive)
            {
                return;
            }

            float elapsed = _simulation.NightElapsedTime;
            while (_next < _lines.Count && _lines[_next].TimeSeconds <= elapsed)
            {
                StoryLogLine line = _lines[_next];
                _terminal.AppendStoryLine(line.Text, line.Corrupted);
                _next++;
            }
        }
    }
}
