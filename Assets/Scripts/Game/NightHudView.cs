using NightShift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The night HUD bar: credits, Core integrity, time left before morning, and the night number.
    /// Implements Story 002 acceptance criterion 4 of
    /// `production/epics/night-shift/story-002-unity-night-view.md`, plus a readout of the debug
    /// time scale for criterion 6.
    /// </summary>
    /// <remarks>
    /// <para><b>Event-driven, not polled.</b> Credits come from
    /// <see cref="NetworkSimulation.OnCreditsChanged"/>, integrity from
    /// <see cref="NetworkSimulation.OnCoreDamaged"/>, and the time scale from
    /// <see cref="GameRunner.OnSpeedChanged"/>. Only the clock is polled, because the simulation
    /// exposes <see cref="NetworkSimulation.NightElapsedTime"/> as state with no matching event.</para>
    ///
    /// <para><b>Allocation.</b> Label text is only rebuilt when the displayed value actually changes -
    /// the clock at most once a second, credits only when the whole-credit figure moves - so the
    /// per-frame path normally allocates nothing at all.</para>
    ///
    /// <para><b>No balance numbers.</b> Night length comes from <see cref="NightData"/> and maximum
    /// integrity from <see cref="GameData.CoreStartingIntegrity"/>; this view hardcodes neither.</para>
    /// </remarks>
    public sealed class NightHudView : MonoBehaviour
    {
        private const int HudBarPaddingPx = 10;
        private const int HudLabelSpacingPx = 18;

        private NetworkSimulation _simulation;
        private GameRunner _runner;
        private ViewConfig _config;

        private VisualElement _bar;
        private Label _nightLabel;
        private Label _creditsLabel;
        private Label _integrityLabel;
        private Label _timeLabel;
        private Label _speedLabel;

        private int _maxIntegrity;
        private int _shownCredits = int.MinValue;
        private int _shownIntegrity = int.MinValue;
        private int _shownSecondsRemaining = int.MinValue;
        private float _shownSpeed = -1f;
        private bool _hidden;

        /// <summary>
        /// Builds the HUD into <paramref name="parent"/> and subscribes to the simulation.
        /// </summary>
        /// <param name="simulation">Simulation to observe.</param>
        /// <param name="runner">Night driver, for time remaining and the debug time scale.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer supplied by <see cref="UiRoot.CreateLayer"/>.</param>
        public void Initialize(NetworkSimulation simulation, GameRunner runner, ViewConfig config, VisualElement parent)
        {
            _simulation = simulation;
            _runner = runner;
            _config = config;
            _maxIntegrity = simulation.Data.CoreStartingIntegrity;

            if (parent == null)
            {
                Debug.LogError("[NightShift] NightHudView.Initialize got a null UI layer; HUD will not appear.");
                return;
            }

            BuildHud(parent);

            int nightNumber = runner.CurrentNight != null ? runner.CurrentNight.NightNumber : 0;
            _nightLabel.text = string.Format(UiStrings.NightFormat, nightNumber);

            SetCredits(simulation.Credits);
            SetIntegrity(simulation.CoreIntegrity);
            SetSpeed(runner.SpeedMultiplier);
            UpdateClock();

            _simulation.OnCreditsChanged += SetCredits;
            _simulation.OnCoreDamaged += HandleCoreDamaged;
            _simulation.OnNightEnded += HandleNightEnded;
            _simulation.OnCoreDestroyed += Hide;
            _runner.OnSpeedChanged += SetSpeed;
        }

        private void LateUpdate()
        {
            if (_timeLabel == null || _hidden)
            {
                return;
            }

            UpdateClock();
        }

        /// <summary>
        /// Takes the HUD bar down. Called when the night ends or the Core falls, because the shift
        /// report occupies the same screen and the two used to overprint each other at the top.
        /// </summary>
        /// <remarks>
        /// The HUD hides itself off the simulation's own end-of-night events rather than being told
        /// to by <see cref="NightReportView"/>: the two views stay unaware of each other, exactly as
        /// they are for every other piece of state they share.
        /// </remarks>
        public void Hide()
        {
            _hidden = true;
            if (_bar != null)
            {
                _bar.style.display = DisplayStyle.None;
            }
        }

        private void HandleNightEnded(NightReport report) => Hide();

        private void OnDestroy()
        {
            if (_simulation != null)
            {
                _simulation.OnCreditsChanged -= SetCredits;
                _simulation.OnCoreDamaged -= HandleCoreDamaged;
                _simulation.OnNightEnded -= HandleNightEnded;
                _simulation.OnCoreDestroyed -= Hide;
            }

            if (_runner != null)
            {
                _runner.OnSpeedChanged -= SetSpeed;
            }
        }

        private void BuildHud(VisualElement parent)
        {
            var bar = new VisualElement { name = "hud-bar" };
            _bar = bar;
            bar.style.position = Position.Absolute;
            bar.style.left = 0f;
            bar.style.top = 0f;
            bar.style.right = 0f;
            bar.style.flexShrink = 0f;
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.FlexStart;
            bar.style.backgroundColor = _config.HudBackgroundColor;
            bar.style.paddingLeft = HudBarPaddingPx;
            bar.style.paddingRight = HudBarPaddingPx;
            bar.style.paddingTop = HudBarPaddingPx;
            bar.style.paddingBottom = HudBarPaddingPx;
            parent.Add(bar);

            _nightLabel = AddHudLabel(bar, "hud-night");
            _creditsLabel = AddHudLabel(bar, "hud-credits");
            _integrityLabel = AddHudLabel(bar, "hud-integrity");
            _timeLabel = AddHudLabel(bar, "hud-time");
            _speedLabel = AddHudLabel(bar, "hud-speed");
        }

        private Label AddHudLabel(VisualElement bar, string labelName)
        {
            var label = new Label { name = labelName };
            label.style.color = _config.HudTextColor;
            label.style.fontSize = _config.HudFontSize;
            label.style.marginRight = HudLabelSpacingPx;
            bar.Add(label);
            return label;
        }

        private void SetCredits(float credits)
        {
            int whole = Mathf.FloorToInt(credits);
            if (whole == _shownCredits || _creditsLabel == null)
            {
                return;
            }

            _shownCredits = whole;
            _creditsLabel.text = string.Format(UiStrings.CreditsFormat, whole);
        }

        private void HandleCoreDamaged(int damage, int remainingIntegrity)
        {
            SetIntegrity(remainingIntegrity);
        }

        private void SetIntegrity(int integrity)
        {
            if (integrity == _shownIntegrity || _integrityLabel == null)
            {
                return;
            }

            _shownIntegrity = integrity;
            _integrityLabel.text = string.Format(UiStrings.CoreIntegrityFormat, integrity, _maxIntegrity);
        }

        private void SetSpeed(float multiplier)
        {
            if (Mathf.Approximately(multiplier, _shownSpeed) || _speedLabel == null)
            {
                return;
            }

            _shownSpeed = multiplier;
            _speedLabel.text = string.Format(UiStrings.SpeedFormat, Mathf.RoundToInt(multiplier));
        }

        private void UpdateClock()
        {
            int secondsRemaining = Mathf.CeilToInt(_runner.NightTimeRemaining);
            if (secondsRemaining == _shownSecondsRemaining)
            {
                return;
            }

            _shownSecondsRemaining = secondsRemaining;
            int minutes = secondsRemaining / 60;
            int seconds = secondsRemaining % 60;
            _timeLabel.text = string.Format(UiStrings.TimeRemainingFormat, minutes, seconds);
        }
    }
}
