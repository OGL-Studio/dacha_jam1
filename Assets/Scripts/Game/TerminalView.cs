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

        private TerminalCommandProcessor _processor;
        private GameRunner _runner;
        private ViewConfig _config;

        private VisualElement _panel;
        private ScrollView _log;
        private TextField _input;

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

        /// <summary>Appends one line to the scrolling history, trims the oldest, and scrolls to the bottom.</summary>
        private void AppendLine(string text, Color color)
        {
            if (_log == null)
            {
                return;
            }

            var label = new Label(text);
            label.style.color = color;
            label.style.fontSize = _config.TerminalFontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 0f;
            _log.contentContainer.Add(label);

            int max = Mathf.Max(8, _config.TerminalMaxLogLines);
            while (_log.contentContainer.childCount > max)
            {
                _log.contentContainer.RemoveAt(0);
            }

            ScrollToBottomSoon();
        }

        /// <summary>
        /// Scrolls the log to its newest line one frame later, once layout has measured the line just
        /// added - scrolling in the same frame would clamp against the old content height.
        /// </summary>
        private void ScrollToBottomSoon()
        {
            _log.schedule.Execute(() =>
            {
                Scroller scroller = _log.verticalScroller;
                if (scroller != null)
                {
                    scroller.value = scroller.highValue;
                }
            });
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

            _log = new ScrollView(ScrollViewMode.Vertical) { name = "terminal-log" };
            _log.style.flexGrow = 1f;
            _log.style.flexShrink = 1f;
            _log.style.marginTop = 4f;
            _log.style.marginBottom = 4f;
            _panel.Add(_log);

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
