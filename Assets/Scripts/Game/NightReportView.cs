using NightShift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The end-of-shift report screen, and the defeat screen when the Core falls mid-night.
    /// Implements the closing clause of Story 002 acceptance criterion 5 of
    /// `production/epics/night-shift/story-002-unity-night-view.md` ("at the end - the shift report
    /// screen").
    /// </summary>
    /// <remarks>
    /// <para><b>Deliberately unstyled.</b> Plain labels on a dark panel: readable, and nothing more.
    /// Styled screens are Story 006 and are explicitly out of scope here, so this view sets only the
    /// colours and sizes it needs to be legible without a UI Toolkit theme.</para>
    ///
    /// <para><b>Two entry points.</b> <see cref="NetworkSimulation"/> raises
    /// <see cref="NetworkSimulation.OnNightEnded"/> when the clock runs out, but raises
    /// <see cref="NetworkSimulation.OnCoreDestroyed"/> - and no report - when integrity hits zero
    /// mid-night. Both are handled, otherwise a defeat would leave the player staring at a frozen
    /// map.</para>
    /// </remarks>
    public sealed class NightReportView : MonoBehaviour
    {
        private const int PanelPaddingPx = 28;
        private const int TitleSpacingPx = 16;
        private const int LineSpacingPx = 4;

        private NetworkSimulation _simulation;
        private ViewConfig _config;

        private VisualElement _panel;
        private Label _titleLabel;
        private Label _nightLine;
        private Label _earnedLine;
        private Label _blockedLine;
        private Label _leakedLine;
        private Label _dissipatedLine;
        private Label _damageLine;
        private Label _integrityLine;

        /// <summary>True once a report or defeat screen has been shown.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the (hidden) report panel into <paramref name="parent"/> and subscribes to the
        /// night-end events. Call before the night starts.
        /// </summary>
        public void Initialize(NetworkSimulation simulation, ViewConfig config, VisualElement parent)
        {
            _simulation = simulation;
            _config = config;

            if (parent == null)
            {
                Debug.LogError("[NightShift] NightReportView.Initialize got a null UI layer; the report will not appear.");
                return;
            }

            BuildPanel(parent);

            _simulation.OnNightEnded += HandleNightEnded;
            _simulation.OnCoreDestroyed += HandleCoreDestroyed;
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

        /// <summary>Fills and shows the shift report.</summary>
        public void Show(NightReport report)
        {
            if (_panel == null || report == null)
            {
                return;
            }

            _titleLabel.text = report.CoreDestroyed ? UiStrings.GameOverTitle : UiStrings.ReportTitle;
            _nightLine.text = string.Format(UiStrings.ReportNightFormat, report.NightNumber);
            _earnedLine.text = string.Format(UiStrings.ReportEarnedFormat, Mathf.FloorToInt(report.CreditsEarned));
            _blockedLine.text = string.Format(UiStrings.ReportBlockedFormat, report.PacketsBlocked);
            _leakedLine.text = string.Format(UiStrings.ReportLeakedFormat, report.PacketsLeaked);
            _dissipatedLine.text = string.Format(UiStrings.ReportDissipatedFormat, report.PacketsDissipated);
            _damageLine.text = string.Format(UiStrings.ReportDamageFormat, report.CoreDamageTaken);
            _integrityLine.text = string.Format(
                UiStrings.ReportIntegrityFormat,
                report.RemainingCoreIntegrity,
                _simulation.Data.CoreStartingIntegrity);

            SetLinesVisible(true);
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Shows the defeat screen used when the Core is destroyed before the night ends.</summary>
        public void ShowDefeat()
        {
            if (_panel == null)
            {
                return;
            }

            _titleLabel.text = UiStrings.GameOverTitle;
            _nightLine.text = UiStrings.GameOverBody;
            _earnedLine.text = string.Format(
                UiStrings.ReportIntegrityFormat,
                _simulation.CoreIntegrity,
                _simulation.Data.CoreStartingIntegrity);

            _blockedLine.text = string.Empty;
            _leakedLine.text = string.Empty;
            _dissipatedLine.text = string.Empty;
            _damageLine.text = string.Empty;
            _integrityLine.text = string.Empty;

            SetLinesVisible(true);
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        private void HandleNightEnded(NightReport report) => Show(report);

        private void HandleCoreDestroyed() => ShowDefeat();

        private void BuildPanel(VisualElement parent)
        {
            _panel = new VisualElement { name = "report-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = 0f;
            _panel.style.top = 0f;
            _panel.style.right = 0f;
            _panel.style.bottom = 0f;
            _panel.style.alignItems = Align.Center;
            _panel.style.justifyContent = Justify.Center;
            _panel.style.backgroundColor = _config.ReportBackgroundColor;
            _panel.style.display = DisplayStyle.None;
            parent.Add(_panel);

            var box = new VisualElement { name = "report-box" };
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.FlexStart;

            // flexShrink 0 on the box and on every line below is the second half of the fix in
            // UiRoot.ApplyRootStyle. UI Toolkit defaults flex-shrink to 1, so in a container whose
            // height resolved to zero every line is squeezed to zero height and they all overprint
            // one another in a band a few pixels tall. Refusing to shrink keeps the report readable
            // even if some future ancestor loses its height again.
            box.style.flexShrink = 0f;

            box.style.backgroundColor = _config.ReportBoxColor;
            box.style.borderTopWidth = _config.ReportBoxBorderWidthPx;
            box.style.borderBottomWidth = _config.ReportBoxBorderWidthPx;
            box.style.borderLeftWidth = _config.ReportBoxBorderWidthPx;
            box.style.borderRightWidth = _config.ReportBoxBorderWidthPx;
            box.style.borderTopColor = _config.ReportBoxBorderColor;
            box.style.borderBottomColor = _config.ReportBoxBorderColor;
            box.style.borderLeftColor = _config.ReportBoxBorderColor;
            box.style.borderRightColor = _config.ReportBoxBorderColor;

            box.style.paddingLeft = PanelPaddingPx;
            box.style.paddingRight = PanelPaddingPx;
            box.style.paddingTop = PanelPaddingPx;
            box.style.paddingBottom = PanelPaddingPx;
            _panel.Add(box);

            _titleLabel = new Label { name = "report-title" };
            _titleLabel.style.color = _config.HudTextColor;
            _titleLabel.style.fontSize = _config.ReportFontSize * 1.4f;
            _titleLabel.style.marginBottom = TitleSpacingPx;
            _titleLabel.style.flexShrink = 0f;
            box.Add(_titleLabel);

            _nightLine = AddReportLine(box, "report-night");
            _earnedLine = AddReportLine(box, "report-earned");
            _blockedLine = AddReportLine(box, "report-blocked");
            _leakedLine = AddReportLine(box, "report-leaked");
            _dissipatedLine = AddReportLine(box, "report-dissipated");
            _damageLine = AddReportLine(box, "report-damage");
            _integrityLine = AddReportLine(box, "report-integrity");
        }

        private Label AddReportLine(VisualElement box, string labelName)
        {
            var label = new Label { name = labelName };
            label.style.color = _config.HudTextColor;
            label.style.fontSize = _config.ReportFontSize;
            label.style.marginBottom = LineSpacingPx;
            label.style.flexShrink = 0f;
            box.Add(label);
            return label;
        }

        /// <summary>Hides empty lines so the defeat screen does not show blank rows.</summary>
        private void SetLinesVisible(bool visible)
        {
            SetLineVisible(_nightLine, visible);
            SetLineVisible(_earnedLine, visible);
            SetLineVisible(_blockedLine, visible);
            SetLineVisible(_leakedLine, visible);
            SetLineVisible(_dissipatedLine, visible);
            SetLineVisible(_damageLine, visible);
            SetLineVisible(_integrityLine, visible);
        }

        private static void SetLineVisible(Label label, bool visible)
        {
            if (label == null)
            {
                return;
            }

            bool hasText = !string.IsNullOrEmpty(label.text);
            label.style.display = visible && hasText ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
