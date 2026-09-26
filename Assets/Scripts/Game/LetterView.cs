using System.Collections.Generic;
using NightShift.Core;
using NightShift.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The morning mail: the one or two letters from the security officers that open every day.
    /// Implements Story 006 acceptance criterion 2 of
    /// `production/epics/night-shift/story-006-story-and-screens.md` («перед каждым днём — 1-2 письма
    /// от офицеров ИБ; тон мрачнеет от ночи к ночи; к ночи 5 письма искажены»).
    /// </summary>
    /// <remarks>
    /// <para><b>Reads the day off the runner, the text off the data.</b> The view subscribes to
    /// <see cref="GameRunner.OnPhaseChanged"/> and, on entering <see cref="GamePhase.Day"/>, asks
    /// <see cref="StoryLibrary.GetLetters"/> for that day's set. It therefore needs no wiring from the
    /// report screen or the title screen, and adding a sixth day's letters is a change to one data
    /// file.</para>
    ///
    /// <para><b>Why it blocks building.</b> The panel is pickable, which stops clicks reaching the shop
    /// underneath - but map placement is polled from the legacy <see cref="Input"/> class, which UI
    /// Toolkit's hit-testing never sees. So while the mail is up this view also raises
    /// <see cref="DayBuildController.ModalOpen"/>; otherwise the player would place nodes blindly
    /// behind a full-screen letter.</para>
    ///
    /// <para><b>Corruption is a data flag.</b> Night 4-5 letters arrive already damaged from
    /// <c>StoryLibrary</c> (the glyph damage is authored into the strings, so it is identical every
    /// run) and carry <see cref="StoryLetter.Corrupted"/>; this view only chooses the colour. Nothing
    /// here generates a glitch, so no screenshot of this screen can come out unreadable by
    /// chance.</para>
    /// </remarks>
    public sealed class LetterView : MonoBehaviour
    {
        private const int HeaderSpacingPx = 14;
        private const int MetaSpacingPx = 10;
        private const int ButtonPaddingXPx = 24;
        private const int ButtonPaddingYPx = 8;

        private GameRunner _runner;
        private DayBuildController _buildController;
        private ViewConfig _config;

        private VisualElement _panel;
        private Label _screenTitle;
        private Label _counter;
        private Label _from;
        private Label _subject;
        private VisualElement _bodyBox;
        private Button _advanceButton;

        private IReadOnlyList<StoryLetter> _letters;
        private int _index;
        private int _shownFrame = -1;

        /// <summary>True while a letter is on screen.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the (hidden) mail screen and starts following the phase machine.
        /// </summary>
        /// <param name="runner">Phase and day-number source.</param>
        /// <param name="buildController">
        /// Raised and lowered through <see cref="DayBuildController.ModalOpen"/> so the map cannot be
        /// edited behind the mail. May be null in a harness that has no build controller.
        /// </param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        public void Initialize(GameRunner runner, DayBuildController buildController, ViewConfig config, VisualElement parent)
        {
            _runner = runner;
            _buildController = buildController;
            _config = config;

            if (parent == null)
            {
                Debug.LogError("[NightShift] LetterView.Initialize got a null UI layer; the letters will not appear.");
                return;
            }

            BuildPanel(parent);

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

            SetModal(false);
        }

        private void HandlePhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Day)
            {
                ShowForDay(_runner != null ? _runner.DayNumber : 1);
                return;
            }

            Hide();
        }

        /// <summary>
        /// Opens the mail for a 1-based day number. Does nothing when that day has no letters, so the
        /// day begins immediately instead of on an empty screen.
        /// </summary>
        public void ShowForDay(int dayNumber)
        {
            if (_panel == null)
            {
                return;
            }

            _letters = StoryLibrary.GetLetters(dayNumber);
            if (_letters == null || _letters.Count == 0)
            {
                Hide();
                return;
            }

            _screenTitle.text = string.Format(UiStrings.LetterScreenTitleFormat, dayNumber);
            _index = 0;
            RenderCurrent();

            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
            _shownFrame = Time.frameCount;
            SetModal(true);
        }

        /// <summary>Closes the mail and hands the day back to the player.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
            SetModal(false);
        }

        private void Update()
        {
            if (!IsShowing || _runner == null || _runner.TextInputActive)
            {
                return;
            }

            // The Enter that started the shift on the title screen is still down this frame, and
            // legacy Input is global: without this the first letter would flick straight to the second.
            // MonoBehaviour execution order among components on one GameObject is not contractual, so
            // this does not rely on the order the bootstrap added them in.
            if (Time.frameCount == _shownFrame)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                HandleAdvanceClicked();
            }
        }

        /// <summary>Moves to the next letter, or closes the mail after the last one.</summary>
        private void HandleAdvanceClicked()
        {
            if (!IsShowing || _letters == null)
            {
                return;
            }

            _index++;
            if (_index >= _letters.Count)
            {
                Hide();
                return;
            }

            RenderCurrent();
        }

        private void RenderCurrent()
        {
            StoryLetter letter = _letters[_index];
            bool corrupted = letter.Corrupted;
            Color headerColor = corrupted ? _config.StoryCorruptColor : _config.HudTextColor;

            _counter.text = string.Format(UiStrings.LetterCounterFormat, _index + 1, _letters.Count);
            _from.text = string.Format(UiStrings.LetterFromFormat, letter.Sender);
            _subject.text = string.Format(UiStrings.LetterSubjectFormat, letter.Subject);
            _from.style.color = headerColor;
            _subject.style.color = headerColor;

            // Rebuilt per letter rather than re-texting one label: each authored line is its own
            // label, which keeps the blank lines in the body as real spacer rows and lets a single
            // over-long line wrap on its own without pushing the others around.
            _bodyBox.Clear();
            string[] bodyLines = (letter.Body ?? string.Empty).Split('\n');
            foreach (string line in bodyLines)
            {
                _bodyBox.Add(StoryUi.CreateBodyLabel(line, _config, corrupted));
            }

            bool last = _index == _letters.Count - 1;
            _advanceButton.text = last ? UiStrings.LetterCloseButton : UiStrings.LetterNextButton;
        }

        private void SetModal(bool modal)
        {
            if (_buildController != null)
            {
                _buildController.ModalOpen = modal;
            }
        }

        private void BuildPanel(VisualElement parent)
        {
            _panel = StoryUi.CreateOverlayPanel(parent, "letter-panel", _config);

            _screenTitle = new Label { name = "letter-screen-title" };
            _screenTitle.style.color = _config.HudTextColor;
            _screenTitle.style.fontSize = _config.StoryHeaderFontSize;
            _screenTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _screenTitle.style.marginBottom = MetaSpacingPx;
            _screenTitle.style.flexShrink = 0f;
            _panel.Add(_screenTitle);

            VisualElement box = StoryUi.CreateBox("letter-box", _config);
            _panel.Add(box);

            _counter = new Label { name = "letter-counter" };
            _counter.style.color = _config.StoryMetaColor;
            _counter.style.fontSize = _config.TerminalFontSize;
            _counter.style.marginBottom = MetaSpacingPx;
            _counter.style.flexShrink = 0f;
            box.Add(_counter);

            _from = new Label { name = "letter-from" };
            _from.style.fontSize = _config.StoryHeaderFontSize;
            _from.style.whiteSpace = WhiteSpace.Normal;
            _from.style.flexShrink = 0f;
            box.Add(_from);

            _subject = new Label { name = "letter-subject" };
            _subject.style.fontSize = _config.StoryHeaderFontSize;
            _subject.style.whiteSpace = WhiteSpace.Normal;
            _subject.style.marginBottom = HeaderSpacingPx;
            _subject.style.flexShrink = 0f;
            box.Add(_subject);

            _bodyBox = new VisualElement { name = "letter-body" };
            _bodyBox.style.flexDirection = FlexDirection.Column;
            _bodyBox.style.alignItems = Align.FlexStart;
            _bodyBox.style.flexShrink = 0f;
            box.Add(_bodyBox);

            _advanceButton = StoryUi.CreateButton(UiStrings.LetterCloseButton, _config, ButtonPaddingXPx, ButtonPaddingYPx);
            _advanceButton.name = "letter-advance";
            _advanceButton.style.marginTop = HeaderSpacingPx + MetaSpacingPx;
            _advanceButton.clicked += HandleAdvanceClicked;
            _panel.Add(_advanceButton);
        }
    }
}
