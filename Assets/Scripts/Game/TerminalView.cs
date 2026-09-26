using System.Collections.Generic;
using NightShift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The night terminal: a focused input field, a scrolling output history, and up/down-arrow
    /// recall of previous commands. Implements Story 004 acceptance criterion 8 of
    /// `production/epics/night-shift/story-004-terminal-commands.md`, and renders the results of
    /// criteria 1-7, which <see cref="TerminalCommandProcessor"/> decides.
    /// </summary>
    /// <remarks>
    /// <para><b>A thin view.</b> Nothing here knows what a command does, what it costs, or how long a
    /// cooldown lasts: the whole line is handed to <see cref="TerminalCommandProcessor.Execute"/> and
    /// the returned <see cref="TerminalCommandResult"/> is turned into Russian by
    /// <see cref="UiStrings.FormatTerminalResult"/>. Adding a command in Story 005 needs no change in
    /// this file beyond a description string.</para>
    ///
    /// <para><b>Night only.</b> The panel follows <see cref="GameRunner.OnPhaseChanged"/> and is up
    /// only during <see cref="GamePhase.Night"/> - the terminal is how the player intervenes while
    /// packets are in flight, and the day has its own panel.</para>
    ///
    /// <para><b>Keyboard ownership.</b> While the field has focus it raises
    /// <see cref="GameRunner.TextInputActive"/>, which suppresses the legacy-Input hotkeys (F4's
    /// debug time scale) so that typing cannot trigger gameplay. Escape blurs the field and hands the
    /// hotkeys back; clicking the panel takes focus again. The key handler is registered in the
    /// trickle-down phase so Enter and the arrows are intercepted before the field's own text editing
    /// sees them.</para>
    ///
    /// <para><b>No balance numbers and no colours in code.</b> Durations and cooldowns come from
    /// <see cref="GameData"/> through the processor; every colour and size comes from
    /// <see cref="ViewConfig"/>.</para>
    /// </remarks>
    public sealed class TerminalView : MonoBehaviour
    {
        private const int PanelPaddingPx = 8;
        private const int InputPaddingPx = 4;
        private const long FocusDelayMs = 60;

        /// <summary>Slack, in panel pixels, within which the log still counts as "at the bottom".</summary>
        private const float TailEpsilonPx = 0.5f;

        /// <summary>Gap kept between the wrapped text and the scroll indicator, in panel pixels.</summary>
        private const float ScrollBarGapPx = 3f;

        private TerminalCommandProcessor _processor;
        private GameRunner _runner;
        private ViewConfig _config;

        private VisualElement _panel;
        private VisualElement _logViewport;
        private VisualElement _logContent;
        private VisualElement _scrollThumb;
        private TextField _input;

        /// <summary>How far the log is scrolled down from its oldest line, in panel pixels.</summary>
        private float _scrollOffset;

        /// <summary>
        /// True while the log is pinned to its newest line. Cleared when the player scrolls back, and
        /// set again as soon as they scroll to the bottom - so reading history is never yanked away by
        /// an incoming line, and following the live output needs no action.
        /// </summary>
        private bool _followTail = true;

        /// <summary>Submitted command lines, oldest first (acceptance criterion 8's history).</summary>
        private readonly List<string> _history = new List<string>();

        /// <summary>
        /// Index into <see cref="_history"/> while browsing it. Equal to the list's count when the
        /// player is not browsing, which is what makes the first Up press recall the newest entry.
        /// </summary>
        private int _historyCursor;

        private bool _visible;

        /// <summary>
        /// Builds the terminal into <paramref name="parent"/> and subscribes to the phase machine.
        /// </summary>
        /// <param name="processor">Command parser and dispatcher. Owns the rules; this view owns none.</param>
        /// <param name="runner">Phase machine, and the flag that mutes gameplay hotkeys while typing.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer supplied by <see cref="UiRoot.CreateLayer"/>.</param>
        public void Initialize(TerminalCommandProcessor processor, GameRunner runner, ViewConfig config, VisualElement parent)
        {
            _processor = processor;
            _runner = runner;
            _config = config;

            if (parent == null || processor == null)
            {
                Debug.LogError("[NightShift] TerminalView.Initialize needs a UI layer and a processor; terminal disabled.");
                return;
            }

            BuildPanel(parent);

            _runner.OnPhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(_runner.Phase);
        }

        private void OnDestroy()
        {
            if (_runner != null)
            {
                _runner.OnPhaseChanged -= HandlePhaseChanged;
                _runner.TextInputActive = false;
            }
        }

        // ------------------------------------------------------------------
        // Visibility
        // ------------------------------------------------------------------

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Night)
            {
                Show();
                return;
            }

            Hide();
        }

        /// <summary>Brings the terminal up, greets the shift, and takes keyboard focus.</summary>
        public void Show()
        {
            if (_panel == null || _visible)
            {
                return;
            }

            _visible = true;
            _panel.style.display = DisplayStyle.Flex;

            // A night always opens on its newest line, whatever the player was reading last night.
            _followTail = true;

            AppendLine(UiStrings.TerminalReady, _config.TerminalOkColor);
            FocusInputSoon();
        }

        /// <summary>
        /// Takes the terminal down and releases the keyboard, so the shift report's own controls and
        /// the gameplay hotkeys are not competing with an invisible focused field.
        /// </summary>
        public void Hide()
        {
            _visible = false;

            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            if (_input != null)
            {
                _input.Blur();
            }

            if (_runner != null)
            {
                _runner.TextInputActive = false;
            }
        }

        // ------------------------------------------------------------------
        // Input
        // ------------------------------------------------------------------

        /// <summary>
        /// Intercepts the terminal's own keys before the text field edits itself: Enter submits, the
        /// arrows browse history, Escape releases the keyboard.
        /// </summary>
        private void HandleKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
                    evt.StopPropagation();
                    return;
                case KeyCode.UpArrow:
                    StepHistory(-1);
                    evt.StopPropagation();
                    return;
                case KeyCode.DownArrow:
                    StepHistory(1);
                    evt.StopPropagation();
                    return;
            }

            if (evt.keyCode == _config.TerminalBlurKey)
            {
                _input.Blur();
                evt.StopPropagation();
                return;
            }

            // Some platforms report Enter only as a character, with keyCode None.
            if (evt.character == '\n' || evt.character == '\r')
            {
                Submit();
                evt.StopPropagation();
            }
        }

        /// <summary>
        /// Moves through the submitted-command history by <paramref name="delta"/> entries and puts
        /// the result in the field. Stepping past the newest entry clears the field, which is how the
        /// player gets back to an empty prompt without deleting characters.
        /// </summary>
        private void StepHistory(int delta)
        {
            if (_history.Count == 0)
            {
                return;
            }

            int target = Mathf.Clamp(_historyCursor + delta, 0, _history.Count);
            _historyCursor = target;

            _input.value = target >= _history.Count ? string.Empty : _history[target];
        }

        /// <summary>Runs the current input line, echoes it, records it in the history, and logs the outcome.</summary>
        private void Submit()
        {
            string raw = _input.value ?? string.Empty;
            _input.value = string.Empty;

            string line = raw.Trim();
            if (line.Length == 0)
            {
                _historyCursor = _history.Count;
                return;
            }

            AppendLine(string.Format(UiStrings.TerminalEchoFormat, line), _config.TerminalEchoColor);

            // Consecutive repeats are not stored twice: Up should step back through distinct
            // commands, not through however many times the player retried a cooling-down one.
            if (_history.Count == 0 || _history[_history.Count - 1] != line)
            {
                _history.Add(line);
            }
            _historyCursor = _history.Count;

            RenderResult(_processor.Execute(line));
        }

        /// <summary>Turns a command result into log lines. The only place command output reaches the screen.</summary>
        private void RenderResult(TerminalCommandResult result)
        {
            if (result == null || result.Code == TerminalResultCode.Empty)
            {
                return;
            }

            if (result.Code == TerminalResultCode.Help)
            {
                AppendLine(UiStrings.TerminalHelpHeader, _config.TerminalOkColor);
                IReadOnlyList<TerminalCommandInfo> commands = result.Commands;
                for (int i = 0; i < commands.Count; i++)
                {
                    TerminalCommandInfo info = commands[i];
                    AppendLine(
                        string.Format(UiStrings.TerminalHelpRowFormat, info.Usage, UiStrings.GetCommandDescription(info.Name)),
                        _config.TerminalOkColor);
                }
                return;
            }

            TerminalCommandInfo known = _processor.FindCommand(result.CommandName);
            string message = UiStrings.FormatTerminalResult(result, known != null ? known.Usage : null);
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            AppendLine(message, result.Success ? _config.TerminalOkColor : _config.TerminalErrorColor);
        }

        // ------------------------------------------------------------------
        // Log
        // ------------------------------------------------------------------

        /// <summary>
        /// Prints one authored story line into the night log - Story 006 acceptance criterion 3. Called
        /// by <see cref="StoryLogDirector"/> on the schedule held in
        /// <c>NightShift.Core.Data.StoryLibrary</c>.
        /// </summary>
        /// <remarks>
        /// <para><b>Why the terminal owns the rendering.</b> The story lines share the log with command
        /// output, so they have to share its trimming, its scroll-to-bottom and its element budget; a
        /// second overlay writing into the same panel would fight all three. This is the whole of the
        /// terminal's story surface - it decides nothing about <i>what</i> or <i>when</i>.</para>
        ///
        /// <para>A distinct colour keeps a story line from being mistaken for the result of something
        /// the player typed, and <paramref name="corrupted"/> switches to the warning colour for the
        /// lines that are not coming from a colleague any more.</para>
        /// </remarks>
        /// <param name="text">The line, already in Russian, from the data file.</param>
        /// <param name="corrupted">True for a damaged transmission (nights 4-5).</param>
        public void AppendStoryLine(string text, bool corrupted)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            AppendLine(text, corrupted ? _config.StoryCorruptColor : _config.StoryLogColor);
        }

        /// <summary>Appends one line to the log and trims the oldest beyond <see cref="ViewConfig.TerminalMaxLogLines"/>.</summary>
        /// <remarks>
        /// Nothing scrolls here. Appending changes the content's height, which raises
        /// <see cref="GeometryChangedEvent"/> once layout has measured the new line, and
        /// <see cref="HandleLogGeometryChanged"/> is what pins the view to the newest line. Scrolling
        /// in this method instead would clamp against the height the log had *before* the new line was
        /// measured, and land one line short every time.
        /// </remarks>
        private void AppendLine(string text, Color color)
        {
            if (_logContent == null)
            {
                return;
            }

            var label = new Label(text);
            label.style.color = color;
            label.style.fontSize = _config.TerminalFontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 0f;
            _logContent.Add(label);

            int max = Mathf.Max(8, _config.TerminalMaxLogLines);
            while (_logContent.childCount > max)
            {
                _logContent.RemoveAt(0);
            }
        }

        // ------------------------------------------------------------------
        // Scrolling
        // ------------------------------------------------------------------

        /// <summary>
        /// Distance the content can travel inside the viewport, in panel pixels. Zero while the whole
        /// log fits, which is also what hides the indicator.
        /// </summary>
        private float MaxScrollOffset
        {
            get
            {
                if (_logContent == null || _logViewport == null)
                {
                    return 0f;
                }

                float contentHeight = _logContent.layout.height;
                float viewportHeight = _logViewport.layout.height;
                if (float.IsNaN(contentHeight) || float.IsNaN(viewportHeight))
                {
                    return 0f;
                }

                return Mathf.Max(0f, contentHeight - viewportHeight);
            }
        }

        /// <summary>One wheel notch, converted into <see cref="ViewConfig.TerminalScrollStepPx"/> of travel.</summary>
        /// <remarks>
        /// A positive <c>delta.y</c> is a scroll towards the newest line, the same direction UI
        /// Toolkit's own scrolling controls use. <c>StopPropagation</c> keeps the notch from also
        /// reaching anything the terminal sits over.
        /// </remarks>
        private void HandleLogWheel(WheelEvent evt)
        {
            if (MaxScrollOffset <= 0f)
            {
                return;
            }

            SetScrollOffset(_scrollOffset + evt.delta.y * _config.TerminalScrollStepPx);
            evt.StopPropagation();
        }

        /// <summary>Clamps, stores and applies a new scroll position, and re-decides whether to follow the tail.</summary>
        private void SetScrollOffset(float offset)
        {
            float max = MaxScrollOffset;
            _scrollOffset = Mathf.Clamp(offset, 0f, max);
            _followTail = _scrollOffset >= max - TailEpsilonPx;
            ApplyScroll();
        }

        /// <summary>
        /// Re-pins the log after any layout change: a line appended or trimmed, a resized window, or a
        /// wrapped line changing height.
        /// </summary>
        private void HandleLogGeometryChanged(GeometryChangedEvent evt)
        {
            float max = MaxScrollOffset;
            _scrollOffset = _followTail ? max : Mathf.Clamp(_scrollOffset, 0f, max);
            ApplyScroll();
        }

        /// <summary>Moves the content and sizes the indicator. The only place either is written.</summary>
        private void ApplyScroll()
        {
            if (_logContent == null || _logViewport == null)
            {
                return;
            }

            _logContent.style.top = -_scrollOffset;

            float contentHeight = _logContent.layout.height;
            float viewportHeight = _logViewport.layout.height;
            float max = MaxScrollOffset;

            bool scrollable = max > 0f &&
                              !float.IsNaN(contentHeight) && contentHeight > 0f &&
                              !float.IsNaN(viewportHeight) && viewportHeight > 0f;

            _scrollThumb.style.display = scrollable ? DisplayStyle.Flex : DisplayStyle.None;
            if (!scrollable)
            {
                return;
            }

            float thumbHeight = Mathf.Max(
                _config.TerminalScrollBarWidthPx * 2f,
                viewportHeight * viewportHeight / contentHeight);

            float travel = Mathf.Max(0f, viewportHeight - thumbHeight);
            _scrollThumb.style.height = thumbHeight;
            _scrollThumb.style.top = travel * (_scrollOffset / max);
        }

        private void FocusInputSoon()
        {
            _input.schedule.Execute(() =>
            {
                if (_visible)
                {
                    _input.Focus();
                }
            }).ExecuteLater(FocusDelayMs);
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private void BuildPanel(VisualElement parent)
        {
            _panel = new VisualElement { name = "terminal-panel" };
            _panel.style.position = Position.Absolute;
            _panel.style.left = _config.TerminalMarginPx;
            _panel.style.bottom = _config.TerminalMarginPx;
            _panel.style.width = _config.TerminalWidthPx;
            _panel.style.height = _config.TerminalHeightPx;

            // Both dimensions above are 720p pixel counts, and the panel's share of the screen changes
            // with the window's aspect ratio: at match 0.5 a window narrower than 16:9 gives the panel
            // fewer reference pixels across, and a wider one gives it fewer down (see
            // MapCamera.LeftGutterScreenFraction for the arithmetic). Without these caps the terminal
            // eats two-fifths of a 4:3 screen's width, and on an ultra-wide it grows towards the HUD
            // bar. The log inside is already flexible - its viewport takes whatever height is left over
            // - so shrinking the panel costs lines of history, not layout.
            _panel.style.maxWidth = Length.Percent(_config.LeftPanelMaxWidthPercent);
            _panel.style.maxHeight = Length.Percent(_config.TerminalMaxHeightPercent);

            _panel.style.flexDirection = FlexDirection.Column;
            _panel.style.backgroundColor = _config.TerminalBackgroundColor;
            _panel.style.paddingLeft = PanelPaddingPx;
            _panel.style.paddingRight = PanelPaddingPx;
            _panel.style.paddingTop = PanelPaddingPx;
            _panel.style.paddingBottom = PanelPaddingPx;
            SetBorder(_panel, _config.TerminalBorderColor, _config.TerminalBorderWidthPx);

            // Clicking anywhere on the panel - including the log - returns focus to the field, so the
            // player never has to hunt for the caret after reading output.
            _panel.RegisterCallback<PointerDownEvent>(_ => FocusInputSoon());

            var title = new Label(UiStrings.TerminalTitle) { name = "terminal-title" };
            title.style.color = _config.TerminalOkColor;
            title.style.fontSize = _config.TerminalTitleFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexShrink = 0f;
            _panel.Add(title);

            BuildLog();

            _input = new TextField { name = "terminal-input" };
            _input.multiline = false;
            _input.isDelayed = false;
            _input.style.flexShrink = 0f;
            _input.style.fontSize = _config.TerminalFontSize;
            _input.style.color = _config.TerminalOkColor;
            _input.style.backgroundColor = _config.TerminalInputBackgroundColor;
            _input.style.paddingLeft = InputPaddingPx;
            _input.style.paddingRight = InputPaddingPx;
            _input.style.paddingTop = InputPaddingPx;
            _input.style.paddingBottom = InputPaddingPx;
            _input.style.marginLeft = 0f;
            _input.style.marginRight = 0f;
            SetBorder(_input, _config.TerminalBorderColor, 1f);

            // TrickleDown: the field's own text editing consumes Enter and the arrow keys at the
            // target, so the terminal has to see them on the way down.
            _input.RegisterCallback<KeyDownEvent>(HandleKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<FocusInEvent>(_ => SetKeyboardOwned(true));
            _input.RegisterCallback<FocusOutEvent>(_ => SetKeyboardOwned(false));
            _panel.Add(_input);

            var hint = new Label(UiStrings.TerminalHint) { name = "terminal-hint" };
            hint.style.color = _config.TerminalHintColor;
            hint.style.fontSize = _config.TerminalHintFontSize;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.flexShrink = 0f;
            hint.style.marginTop = 3f;
            _panel.Add(hint);

            _panel.style.display = DisplayStyle.None;
            parent.Add(_panel);
        }

        /// <summary>
        /// Builds the scrolling log: a clipped viewport, an absolutely positioned content column that
        /// slides inside it, and a thin position indicator.
        /// </summary>
        /// <remarks>
        /// <para><b>Why this is not a <c>ScrollView</c>.</b> It was one, and it could not work here.
        /// <see cref="ScrollView"/> takes its viewport clipping, its content-container flex rules and
        /// its scroller geometry from the default runtime theme's style sheet - and a
        /// <see cref="PanelSettings"/> created in code has no theme (see <see cref="UiRoot"/>; the
        /// player log records the engine's own <c>No Theme Style Sheet set to PanelSettings</c>
        /// warning). Unstyled, the viewport never bounded the content, so the log grew instead of
        /// scrolling. Worse, <c>ScrollView.OnScrollWheel</c> calls <c>ReadSingleLineHeight</c>, which
        /// resolves a font metric through that same missing theme and threw a
        /// <see cref="System.NullReferenceException"/> on every wheel notch - also in the player log.
        /// Nothing this view could set on the <c>ScrollView</c> from outside would have stopped that
        /// throw, so the control is not used.</para>
        ///
        /// <para><b>Absolute content, not a flex child.</b> The content column is
        /// <see cref="Position.Absolute"/>, so its height is its own and it contributes nothing to the
        /// viewport's: the viewport's height therefore comes purely from <c>flexGrow</c> inside the
        /// panel's fixed height, which is exactly the bounded box a scroller needs. Scrolling is then
        /// one assignment to <c>top</c>, and <c>overflow: hidden</c> on the viewport does the
        /// clipping. Every value below is either a <see cref="ViewConfig"/> knob or spacing, in
        /// keeping with the rest of this file.</para>
        /// </remarks>
        private void BuildLog()
        {
            _logViewport = new VisualElement { name = "terminal-log" };
            _logViewport.style.flexGrow = 1f;
            _logViewport.style.flexShrink = 1f;

            // Without this a flex child will not shrink below its own content, which would push the
            // input field out through the bottom of the panel as soon as the log grew past it.
            _logViewport.style.minHeight = 0f;

            _logViewport.style.overflow = Overflow.Hidden;
            _logViewport.style.marginTop = 4f;
            _logViewport.style.marginBottom = 4f;
            _panel.Add(_logViewport);

            _logContent = new VisualElement { name = "terminal-log-content" };
            _logContent.style.position = Position.Absolute;
            _logContent.style.left = 0f;
            _logContent.style.right = _config.TerminalScrollBarWidthPx + ScrollBarGapPx;
            _logContent.style.top = 0f;
            _logContent.style.flexDirection = FlexDirection.Column;
            _logViewport.Add(_logContent);

            _scrollThumb = new VisualElement { name = "terminal-log-thumb" };
            _scrollThumb.style.position = Position.Absolute;
            _scrollThumb.style.right = 0f;
            _scrollThumb.style.top = 0f;
            _scrollThumb.style.width = _config.TerminalScrollBarWidthPx;
            _scrollThumb.style.backgroundColor = _config.TerminalScrollBarColor;
            _scrollThumb.style.display = DisplayStyle.None;

            // The indicator reports the position; it must never become the thing under the cursor
            // when the player aims a wheel notch at the log.
            _scrollThumb.pickingMode = PickingMode.Ignore;
            _logViewport.Add(_scrollThumb);

            _logViewport.RegisterCallback<WheelEvent>(HandleLogWheel);

            // Both, and not only the content: the content's height changes when a line is added, and
            // the viewport's changes when the window is resized. Either invalidates the clamp.
            _logContent.RegisterCallback<GeometryChangedEvent>(HandleLogGeometryChanged);
            _logViewport.RegisterCallback<GeometryChangedEvent>(HandleLogGeometryChanged);
        }

        private void SetKeyboardOwned(bool owned)
        {
            if (_runner != null)
            {
                _runner.TextInputActive = owned;
            }
        }

        private static void SetBorder(VisualElement element, Color color, float widthPx)
        {
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftWidth = widthPx;
            element.style.borderRightWidth = widthPx;
            element.style.borderTopWidth = widthPx;
            element.style.borderBottomWidth = widthPx;
        }
    }
}
