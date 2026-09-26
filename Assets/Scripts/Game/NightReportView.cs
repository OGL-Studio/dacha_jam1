using NightShift.Core;
using NightShift.Core.Data;
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
    /// <para><b>Three screens, one panel (extended by Story 006).</b> The same box renders the morning
    /// report (criterion 4: заработано / заблокировано / пропущено / целостность), the «ВЫ УВОЛЕНЫ»
    /// defeat screen with its «Заново» button (criterion 5) and the <c>shutdown --all</c> ending
    /// (criterion 6). They share every style, the narrative block and the button row, and differ only in
    /// which of those are filled - so they were extended here rather than split into three views that
    /// would each have to re-derive the same layout without a UI Toolkit theme.</para>
    ///
    /// <para><b>Deliberately plain.</b> Labels on a dark panel, every colour and size set explicitly:
    /// a runtime <c>PanelSettings</c> has no theme style sheet to inherit from.</para>
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
        private System.Action _onContinue;
        private System.Action _onRestart;
        private bool _endingShown;

        private VisualElement _panel;
        private VisualElement _storyBox;
        private Button _continueButton;
        private Button _restartButton;
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
        /// <param name="simulation">Simulation to observe.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        /// <param name="onContinue">
        /// Invoked by «Следующий день» after the panel hides itself - Story 003 acceptance criterion
        /// 5. An <see cref="System.Action"/> rather than a <see cref="GameRunner"/> reference on
        /// purpose: the report stays a pure view and cannot start or stop anything else by accident.
        /// Null leaves the button off, which is what a defeat wants.
        /// </param>
        /// <param name="onRestart">
        /// Invoked by «Заново» on the defeat and ending screens - Story 006 acceptance criterion 5.
        /// Restarting means rebuilding the whole game, which is <see cref="GameBootstrap"/>'s job and
        /// emphatically not a view's, so this too arrives as an <see cref="System.Action"/>. Null
        /// leaves the button off.
        /// </param>
        public void Initialize(
            NetworkSimulation simulation,
            ViewConfig config,
            VisualElement parent,
            System.Action onContinue = null,
            System.Action onRestart = null)
        {
            _simulation = simulation;
            _config = config;
            _onContinue = onContinue;
            _onRestart = onRestart;

            if (parent == null)
            {
                Debug.LogError("[NightShift] NightReportView.Initialize got a null UI layer; the report will not appear.");
                return;
            }

            BuildPanel(parent);

            _simulation.OnNightEnded += HandleNightEnded;
            _simulation.OnCoreDestroyed += HandleCoreDestroyed;

            // Story 006 acceptance criterion 6. Both halves of the victory are consumed: the event
            // fires first (from NetworkSimulation.ShutdownAll), the report follows with Victory set.
            // Whichever arrives is enough, and _endingShown keeps the second one from re-rendering.
            _simulation.OnVictory += HandleVictory;
        }

        private void OnDestroy()
        {
            if (_simulation == null)
            {
                return;
            }

            _simulation.OnNightEnded -= HandleNightEnded;
            _simulation.OnCoreDestroyed -= HandleCoreDestroyed;
            _simulation.OnVictory -= HandleVictory;
        }

        /// <summary>
        /// Fills and shows the shift report: earned / blocked / leaked / integrity (Story 006
        /// acceptance criterion 4). Routes to the ending screen instead when the report is the victory
        /// one, and to the defeat presentation when the Core fell.
        /// </summary>
        public void Show(NightReport report)
        {
            if (_panel == null || report == null)
            {
                return;
            }

            if (report.Victory)
            {
                ShowEnding();
                return;
            }

            SetStoryLines(report.CoreDestroyed ? StoryLibrary.DefeatEnding : null);
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
            SetContinueVisible(_onContinue != null && !report.CoreDestroyed);

            // A lost campaign is the one case where the report is a dead end without «Заново»: the
            // Core is gone, so there is no next day to walk into.
            SetRestartVisible(report.CoreDestroyed);
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Takes the report down. Used when the player moves on to the next day.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
        }

        /// <summary>
        /// Shows the «ВЫ УВОЛЕНЫ» screen used when Core integrity reaches zero - Story 006 acceptance
        /// criterion 5. Carries the «Заново» button, which is the only way out of it.
        /// </summary>
        public void ShowDefeat()
        {
            if (_panel == null || _endingShown)
            {
                return;
            }

            SetStoryLines(StoryLibrary.DefeatEnding);
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

            // Defeat is terminal: there is no next day to walk into, only «Заново».
            SetContinueVisible(false);
            SetRestartVisible(true);
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>
        /// Shows the ending: <c>shutdown --all</c> pulled on the night the network went out of control.
        /// Story 006 acceptance criterion 6.
        /// </summary>
        /// <remarks>
        /// <para><b>Deliberately not a shift report.</b> The numbers of the last night are beside the
        /// point once the network is off, so every statistics line is blanked and the screen is the
        /// ending text from <c>StoryLibrary.VictoryEnding</c> plus «Заново». That is also why this is a
        /// separate method rather than a flag inside <see cref="Show"/>.</para>
        ///
        /// <para><b>Idempotent.</b> The victory arrives twice - once as
        /// <see cref="NetworkSimulation.OnVictory"/> and once as a <see cref="NightReport"/> with
        /// <see cref="NightReport.Victory"/> set - and <see cref="_endingShown"/> makes the second
        /// delivery a no-op. It also outranks a late <see cref="ShowDefeat"/>: the player who pulled the
        /// rubber-band on the last packet did not get fired.</para>
        /// </remarks>
        public void ShowEnding()
        {
            if (_panel == null || _endingShown)
            {
                return;
            }

            _endingShown = true;

            _titleLabel.text = UiStrings.EndingTitle;
            SetStoryLines(StoryLibrary.VictoryEnding);

            _nightLine.text = UiStrings.EndingFooter;
            _earnedLine.text = string.Empty;
            _blockedLine.text = string.Empty;
            _leakedLine.text = string.Empty;
            _dissipatedLine.text = string.Empty;
            _damageLine.text = string.Empty;
            _integrityLine.text = string.Empty;

            SetLinesVisible(true);
            SetContinueVisible(false);
            SetRestartVisible(true);
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>
        /// Replaces the narrative block above the statistics with <paramref name="lines"/>, or clears it
        /// when they are null. Each authored line becomes its own label, so an empty string in the data
        /// renders as a blank spacer row exactly as written.
        /// </summary>
        private void SetStoryLines(string[] lines)
        {
            if (_storyBox == null)
            {
                return;
            }

            _storyBox.Clear();
            if (lines == null || lines.Length == 0)
            {
                _storyBox.style.display = DisplayStyle.None;
                return;
            }

            foreach (string line in lines)
            {
                _storyBox.Add(StoryUi.CreateBodyLabel(line, _config, false));
            }

            _storyBox.style.display = DisplayStyle.Flex;
        }

        private void HandleNightEnded(NightReport report) => Show(report);

        private void HandleCoreDestroyed() => ShowDefeat();

        private void HandleVictory() => ShowEnding();

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

            // Narrative first, numbers second: on the defeat and ending screens the story text is the
            // screen, and on an ordinary morning report this block is empty and collapsed.
            _storyBox = new VisualElement { name = "report-story" };
            _storyBox.style.flexDirection = FlexDirection.Column;
            _storyBox.style.alignItems = Align.FlexStart;
            _storyBox.style.flexShrink = 0f;
            _storyBox.style.marginBottom = TitleSpacingPx;
            _storyBox.style.display = DisplayStyle.None;
            box.Add(_storyBox);

            _nightLine = AddReportLine(box, "report-night");
            _earnedLine = AddReportLine(box, "report-earned");
            _blockedLine = AddReportLine(box, "report-blocked");
            _leakedLine = AddReportLine(box, "report-leaked");
            _dissipatedLine = AddReportLine(box, "report-dissipated");
            _damageLine = AddReportLine(box, "report-damage");
            _integrityLine = AddReportLine(box, "report-integrity");

            _continueButton = new Button { name = "report-continue", text = UiStrings.ContinueButton };
            _continueButton.style.fontSize = _config.ReportFontSize;
            _continueButton.style.color = _config.HudTextColor;
            _continueButton.style.backgroundColor = _config.ShopButtonSelectedColor;
            _continueButton.style.marginTop = TitleSpacingPx;
            _continueButton.style.marginLeft = 0f;
            _continueButton.style.marginRight = 0f;
            _continueButton.style.flexShrink = 0f;
            _continueButton.style.paddingTop = LineSpacingPx;
            _continueButton.style.paddingBottom = LineSpacingPx;
            _continueButton.style.paddingLeft = PanelPaddingPx;
            _continueButton.style.paddingRight = PanelPaddingPx;
            _continueButton.style.borderTopWidth = _config.ReportBoxBorderWidthPx;
            _continueButton.style.borderBottomWidth = _config.ReportBoxBorderWidthPx;
            _continueButton.style.borderLeftWidth = _config.ReportBoxBorderWidthPx;
            _continueButton.style.borderRightWidth = _config.ReportBoxBorderWidthPx;
            _continueButton.style.borderTopColor = _config.ReportBoxBorderColor;
            _continueButton.style.borderBottomColor = _config.ReportBoxBorderColor;
            _continueButton.style.borderLeftColor = _config.ReportBoxBorderColor;
            _continueButton.style.borderRightColor = _config.ReportBoxBorderColor;
            _continueButton.style.display = DisplayStyle.None;
            _continueButton.clicked += HandleContinueClicked;
            box.Add(_continueButton);

            // Story 006 acceptance criterion 5: the way out of a lost campaign, and out of the ending.
            _restartButton = StoryUi.CreateButton(UiStrings.RestartButton, _config, PanelPaddingPx, LineSpacingPx);
            _restartButton.name = "report-restart";
            _restartButton.style.marginTop = TitleSpacingPx;
            _restartButton.style.display = DisplayStyle.None;
            _restartButton.clicked += HandleRestartClicked;
            box.Add(_restartButton);
        }

        /// <summary>
        /// Hands the campaign restart to the action the bootstrap injected. The panel is <i>not</i>
        /// hidden first: a restart destroys this whole view along with the rest of the object graph, and
        /// leaving the old screen up until that happens means no frame shows a bare map.
        /// </summary>
        private void HandleRestartClicked() => _onRestart?.Invoke();

        private void SetRestartVisible(bool visible)
        {
            if (_restartButton != null)
            {
                _restartButton.style.display = visible && _onRestart != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>Hides the report and hands control back to the caller's next-day action.</summary>
        private void HandleContinueClicked()
        {
            Hide();
            _onContinue?.Invoke();
        }

        private void SetContinueVisible(bool visible)
        {
            if (_continueButton != null)
            {
                _continueButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
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
