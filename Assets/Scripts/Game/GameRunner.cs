using System;
using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Drives one night: owns the <see cref="NetworkSimulation"/>, advances it with frame delta
    /// time, and toggles the debug time scale. Implements Story 002 acceptance criteria 5 and 6 of
    /// `production/epics/night-shift/story-002-unity-night-view.md`.
    /// </summary>
    /// <remarks>
    /// <para><b>The only place time enters the simulation.</b> <see cref="NetworkSimulation.Tick"/>
    /// is called exactly once per frame from <see cref="Update"/> with
    /// <c>Time.deltaTime * SpeedMultiplier</c>, so all gameplay is frame-rate independent and the
    /// debug fast-forward is just a larger delta - the simulation resolves packet hops inside the
    /// call, so a x4 night produces the same outcome as a x1 night.</para>
    ///
    /// <para><b>Ticking stops when the night ends.</b> <see cref="NetworkSimulation"/> clears
    /// <see cref="NetworkSimulation.IsNightActive"/> both when the clock runs out and when the Core
    /// is destroyed, so the report screen freezes the world instead of advancing an ended night.</para>
    ///
    /// <para><b>Owns no view.</b> Views observe this component and the simulation; nothing here
    /// references UI types, so the HUD and the report screen can be replaced by Story 006 without
    /// touching gameplay code.</para>
    ///
    /// <para><b>Also the phase state machine (Story 003).</b> The loop is
    /// <see cref="GamePhase.Day"/> -> <see cref="GamePhase.Night"/> -> <see cref="GamePhase.Morning"/>
    /// -> next Day. The runner is the single owner of <see cref="Phase"/>: the day panel, the night
    /// HUD and the shift report all switch themselves on and off from
    /// <see cref="OnPhaseChanged"/> and none of them knows about the others. Nights are pulled from
    /// an injected factory rather than a fixed instance, so the same runner plays night 2 after day
    /// 2 without the bootstrap being involved again.</para>
    /// </remarks>
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Optional launch argument that sets the starting debug time scale, e.g. <c>--speed 4</c>.</summary>
        private const string SpeedArg = "--speed";

        private NetworkSimulation _simulation;
        private NightData _night;
        private ViewConfig _config;
        private Func<int, NightData> _nightFactory;
        private float _speedMultiplier = 1f;
        private GamePhase _phase = GamePhase.Day;
        private int _dayNumber;

        /// <summary>The simulation being driven. Null until <see cref="Initialize"/> runs.</summary>
        public NetworkSimulation Simulation => _simulation;

        /// <summary>Data for the night currently loaded. Null until the first <see cref="StartNight"/>.</summary>
        public NightData CurrentNight => _night;

        /// <summary>Which half of the loop is running. See <see cref="GamePhase"/>.</summary>
        public GamePhase Phase => _phase;

        /// <summary>
        /// 1-based number of the day being built, which is also the number of the night it leads
        /// into. 0 before <see cref="BeginDay"/> has ever run.
        /// </summary>
        public int DayNumber => _dayNumber;

        /// <summary>Raised after <see cref="Phase"/> changes, with the new phase.</summary>
        public event Action<GamePhase> OnPhaseChanged;

        /// <summary>Current debug time scale: 1 normally, <see cref="ViewConfig.FastForwardMultiplier"/> while fast-forwarding.</summary>
        public float SpeedMultiplier => _speedMultiplier;

        /// <summary>
        /// Set while a text field owns the keyboard, which suppresses every gameplay hotkey read from
        /// the legacy <see cref="Input"/> class. Raised and lowered by <see cref="TerminalView"/> on
        /// focus in and focus out (Story 004).
        /// </summary>
        /// <remarks>
        /// <b>Why this is needed at all.</b> Legacy <see cref="Input"/> is polled globally and knows
        /// nothing about UI Toolkit's focus, so without this flag the F4 in a typed command would also
        /// toggle the debug time scale behind the player's back. The new Input System would route this
        /// for us, but that package is not installed here (see
        /// <see cref="HandleDebugSpeedToggle"/>). Escape blurs the terminal, which lowers the flag and
        /// hands the hotkeys back.
        /// </remarks>
        public bool TextInputActive { get; set; }

        /// <summary>
        /// Seconds left before the night ends, clamped to zero. Derived from
        /// <see cref="NightData.NightDuration"/> - the HUD never hardcodes a night length.
        /// </summary>
        public float NightTimeRemaining
        {
            get
            {
                if (_night == null || _simulation == null)
                {
                    return 0f;
                }

                return Mathf.Max(0f, _night.NightDuration - _simulation.NightElapsedTime);
            }
        }

        /// <summary>Raised when the debug time scale changes, with the new multiplier.</summary>
        public event Action<float> OnSpeedChanged;

        /// <summary>
        /// Injects the simulation and the source of night data. Starts neither a day nor a night -
        /// call <see cref="BeginDay"/> (or <see cref="StartNight"/>) after every view has
        /// subscribed, so no event is missed.
        /// </summary>
        /// <param name="simulation">The simulation to drive. Lives across every night, which is what preserves the built network.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="nightFactory">
        /// Produces the <see cref="NightData"/> for a 1-based night number. Injected rather than
        /// fixed so that Story 005's five-night set needs no change here.
        /// </param>
        public void Initialize(NetworkSimulation simulation, ViewConfig config, Func<int, NightData> nightFactory)
        {
            _simulation = simulation;
            _config = config;
            _nightFactory = nightFactory;
            _speedMultiplier = 1f;
            _phase = GamePhase.Day;
            _dayNumber = 0;

            if (_simulation != null)
            {
                _simulation.OnNightEnded += HandleNightEnded;
                _simulation.OnCoreDestroyed += HandleCoreDestroyed;
            }

            ApplyStartupSpeedOverride();
        }

        private void OnDestroy()
        {
            if (_simulation == null)
            {
                return;
            }

            _simulation.OnNightEnded -= HandleNightEnded;
            _simulation.OnCoreDestroyed -= HandleCoreDestroyed;
        }

        /// <summary>
        /// Applies the optional <c>--speed &lt;multiplier&gt;</c> launch argument.
        /// </summary>
        /// <remarks>
        /// <para><b>Why this exists.</b> Acceptance criterion 6 is the debug time scale, and a built
        /// player cannot be sent an F4 keystroke from an automated session - so without a
        /// non-interactive way in, the criterion can never be evidenced by a screenshot. The
        /// argument sets the multiplier through <see cref="SetSpeedMultiplier"/>, the same method
        /// <see cref="HandleDebugSpeedToggle"/> calls, so the HUD readout and the
        /// <see cref="OnSpeedChanged"/> event behave identically however the value was set. F4's own
        /// behaviour is untouched: it still toggles between x1 and
        /// <see cref="ViewConfig.FastForwardMultiplier"/> from wherever the multiplier currently is.</para>
        ///
        /// <para>Called from <see cref="Initialize"/>, which the bootstrap runs before
        /// <see cref="NightHudView.Initialize"/> reads <see cref="SpeedMultiplier"/>, so the HUD
        /// shows the overridden value from the very first frame.</para>
        /// </remarks>
        private void ApplyStartupSpeedOverride()
        {
            float requested = StartupArgs.ReadFloat(SpeedArg, 1f);
            if (requested <= 1f)
            {
                return;
            }

            SetSpeedMultiplier(requested);
        }

        /// <summary>
        /// Opens the next day: advances <see cref="DayNumber"/> and enters
        /// <see cref="GamePhase.Day"/>. Called once by the bootstrap for day 1 and again by the
        /// shift report for every day after it.
        /// </summary>
        /// <remarks>
        /// Nothing about the network is reset here - the <see cref="NetworkSimulation"/> instance and
        /// therefore its graph, credits and Core integrity live on from the previous night. That is
        /// the whole of Story 003 acceptance criterion 5's "следующий день с сохранённой сетью".
        /// </remarks>
        public void BeginDay()
        {
            if (_simulation == null)
            {
                Debug.LogError("[NightShift] GameRunner.BeginDay called before Initialize.");
                return;
            }

            _dayNumber++;
            SetPhase(GamePhase.Day);
        }

        /// <summary>
        /// Starts the night that follows the current day. Pulls its <see cref="NightData"/> from the
        /// factory passed to <see cref="Initialize"/>.
        /// </summary>
        /// <remarks>
        /// Tolerates being called before <see cref="BeginDay"/> (the <c>--skipday</c> launch path
        /// does exactly that) by treating that as night 1.
        /// </remarks>
        public void StartNight()
        {
            if (_simulation == null)
            {
                Debug.LogError("[NightShift] GameRunner.StartNight called before Initialize.");
                return;
            }

            if (_phase == GamePhase.Night)
            {
                return;
            }

            if (_dayNumber <= 0)
            {
                _dayNumber = 1;
            }

            NightData night = _nightFactory != null ? _nightFactory(_dayNumber) : _night;
            if (night == null)
            {
                Debug.LogError("[NightShift] GameRunner.StartNight has no NightData for night " + _dayNumber + ".");
                return;
            }

            _night = night;
            SetPhase(GamePhase.Night);
            _simulation.StartNight(_night);
        }

        private void HandleNightEnded(NightReport report) => SetPhase(GamePhase.Morning);

        private void HandleCoreDestroyed() => SetPhase(GamePhase.Morning);

        private void SetPhase(GamePhase phase)
        {
            _phase = phase;
            OnPhaseChanged?.Invoke(phase);
        }

        private void Update()
        {
            HandleDebugSpeedToggle();

            if (_simulation == null || !_simulation.IsNightActive)
            {
                return;
            }

            _simulation.Tick(Time.deltaTime * _speedMultiplier);
        }

        /// <summary>
        /// Toggles between x1 and the configured fast-forward multiplier (acceptance criterion 6).
        /// </summary>
        /// <remarks>
        /// Uses the legacy <see cref="Input"/> class deliberately.
        /// `docs/engine-reference/unity/modules/input.md` calls legacy input deprecated and directs
        /// you to <c>com.unity.inputsystem</c>, but that package is not installed in this project and
        /// `ProjectSettings/ProjectSettings.asset` has <c>activeInputHandler: 0</c> (legacy only), so
        /// the new Input System API would not compile here. Revisit if the package is ever added.
        /// </remarks>
        private void HandleDebugSpeedToggle()
        {
            if (_config == null || TextInputActive || !Input.GetKeyDown(_config.FastForwardKey))
            {
                return;
            }

            float fastForward = Mathf.Max(1f, _config.FastForwardMultiplier);
            SetSpeedMultiplier(_speedMultiplier > 1f ? 1f : fastForward);
        }

        /// <summary>
        /// The single place the debug time scale changes. Clamps to at least x1 - the debug knob may
        /// speed a night up, never slow the simulation below real time - and raises
        /// <see cref="OnSpeedChanged"/> only on an actual change.
        /// </summary>
        private void SetSpeedMultiplier(float multiplier)
        {
            float clamped = Mathf.Max(1f, multiplier);
            if (Mathf.Approximately(clamped, _speedMultiplier))
            {
                return;
            }

            _speedMultiplier = clamped;
            OnSpeedChanged?.Invoke(_speedMultiplier);
        }
    }
}
