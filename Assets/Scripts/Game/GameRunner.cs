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
    /// </remarks>
    public sealed class GameRunner : MonoBehaviour
    {
        /// <summary>Optional launch argument that sets the starting debug time scale, e.g. <c>--speed 4</c>.</summary>
        private const string SpeedArg = "--speed";

        private NetworkSimulation _simulation;
        private NightData _night;
        private ViewConfig _config;
        private float _speedMultiplier = 1f;

        /// <summary>The simulation being driven. Null until <see cref="Initialize"/> runs.</summary>
        public NetworkSimulation Simulation => _simulation;

        /// <summary>Data for the night currently loaded.</summary>
        public NightData CurrentNight => _night;

        /// <summary>Current debug time scale: 1 normally, <see cref="ViewConfig.FastForwardMultiplier"/> while fast-forwarding.</summary>
        public float SpeedMultiplier => _speedMultiplier;

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
        /// Injects the simulation and the night to play. Does not start the night - call
        /// <see cref="StartNight"/> after every view has subscribed, so no packet event is missed.
        /// </summary>
        public void Initialize(NetworkSimulation simulation, NightData night, ViewConfig config)
        {
            _simulation = simulation;
            _night = night;
            _config = config;
            _speedMultiplier = 1f;

            ApplyStartupSpeedOverride();
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

        /// <summary>Starts the loaded night. Safe to call once, after views are wired.</summary>
        public void StartNight()
        {
            if (_simulation == null || _night == null)
            {
                Debug.LogError("[NightShift] GameRunner.StartNight called before Initialize.");
                return;
            }

            _simulation.StartNight(_night);
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
            if (_config == null || !Input.GetKeyDown(_config.FastForwardKey))
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
