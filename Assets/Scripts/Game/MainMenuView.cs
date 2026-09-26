using System;
using System.Collections.Generic;
using NightShift.Core.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The main menu, which is also the title screen: the game's name, the prologue from
    /// <see cref="StoryLibrary.TitlePrologue"/>, and the four entries «Начать смену» / «Настройки» /
    /// «Справка» / «Выход». Implements Story 006 acceptance criterion 1 of
    /// `production/epics/night-shift/story-006-story-and-screens.md`, extended by the polish pass into
    /// a menu that is also reachable during play.
    /// </summary>
    /// <remarks>
    /// <para><b>One entry screen, not two.</b> This file was <c>TitleScreenView</c>; it grew the menu
    /// rather than gaining a sibling, so there is exactly one screen the game can be sitting on before
    /// day 1 and exactly one place the four entries live.</para>
    ///
    /// <para><b>It decides nothing.</b> Every entry raises an <see cref="Action"/> supplied by
    /// <see cref="MenuController"/>, which owns the screen stack, the pause latches and the keyboard.
    /// This view therefore polls no input at all - unlike the screen it replaced, which read Enter
    /// itself. Two components polling the same key is how the terminal's Escape and a menu's Escape
    /// start fighting.</para>
    ///
    /// <para><b>Layer picking.</b> The layer this builds into ignores picking and only the panel inside
    /// it is pickable, so the menu cannot swallow a click meant for a lower layer once it has hidden
    /// itself. See the comment in <see cref="GameBootstrap"/> on layer creation.</para>
    /// </remarks>
    public sealed class MainMenuView : MonoBehaviour
    {
        private const int PrologueSpacingPx = 6;
        private const int BlockSpacingPx = 22;
        private const int ButtonPaddingXPx = 34;
        private const int ButtonPaddingYPx = 10;
        private const int ButtonSpacingPx = 8;

        private readonly List<Button> _entries = new List<Button>();

        private ViewConfig _config;
        private VisualElement _panel;
        private Button _primaryButton;
        private Label _hint;

        /// <summary>True while the menu is on screen.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the menu, hidden. <see cref="MenuController"/> decides when it first appears, so a
        /// launch that skips the opening day (<see cref="GameBootstrap.SkipDayArg"/>) never shows it.
        /// </summary>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        /// <param name="onPrimary">
        /// «Начать смену» before day 1, «Продолжить» once a shift is under way. Which of the two it is
        /// is decided by <see cref="Show"/>'s argument; what it does is the controller's business.
        /// </param>
        /// <param name="onSettings">Opens the settings screen.</param>
        /// <param name="onHelp">Opens the help screen.</param>
        /// <param name="onQuit">Leaves the game.</param>
        public void Initialize(
            ViewConfig config,
            VisualElement parent,
            Action onPrimary,
            Action onSettings,
            Action onHelp,
            Action onQuit)
        {
            _config = config;

            if (parent == null)
            {
                Debug.LogError("[NightShift] MainMenuView.Initialize got a null UI layer; the menu will not appear.");
                return;
            }

            BuildPanel(parent, onPrimary, onSettings, onHelp, onQuit);
        }

        /// <summary>
        /// Puts the menu up.
        /// </summary>
        /// <param name="gameInProgress">
        /// False on the title screen, where the first entry starts day 1; true when the menu was opened
        /// from inside a running shift, where it resumes instead.
        /// </param>
        public void Show(bool gameInProgress)
        {
            if (_panel == null)
            {
                return;
            }

            _primaryButton.text = gameInProgress ? UiStrings.MenuResumeButton : UiStrings.StartShiftButton;
            _hint.text = gameInProgress ? UiStrings.MenuHintInGame : UiStrings.TitleHint;

            // Clearing focus is not cosmetic. A UI Toolkit Button keeps keyboard focus after it is
            // clicked and acts on Enter and Space itself, so the «Настройки» entry the player just used
            // to leave this screen would still be armed when they came back to it - and Space would then
            // both re-open the settings (UI Toolkit, via the focused button) and start the shift
            // (MenuController, which polls the same key). Blurring the entries leaves exactly one
            // handler for Enter and Space while the menu is up.
            foreach (Button entry in _entries)
            {
                entry.Blur();
            }

            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Takes the menu down. Starts nothing - the controller owns what happens next.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
        }

        private void BuildPanel(VisualElement parent, Action onPrimary, Action onSettings, Action onHelp, Action onQuit)
        {
            _panel = StoryUi.CreateOverlayPanel(parent, "menu-panel", _config);

            var title = new Label { name = "menu-title", text = UiStrings.TitleGameName };
            title.style.color = _config.HudTextColor;
            title.style.fontSize = _config.TitleFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.flexShrink = 0f;
            title.style.marginBottom = BlockSpacingPx;
            _panel.Add(title);

            foreach (string line in StoryLibrary.TitlePrologue)
            {
                var prologue = new Label { text = line };
                prologue.style.color = _config.StoryTextColor;
                prologue.style.fontSize = _config.TitleBodyFontSize;
                prologue.style.unityTextAlign = TextAnchor.MiddleCenter;
                prologue.style.marginBottom = PrologueSpacingPx;
                prologue.style.flexShrink = 0f;

                // The prologue lines were authored to fit 1280 reference pixels unwrapped. A window
                // whose aspect is narrower than 16:9 gives the panel fewer reference pixels across
                // (see MapCamera.LeftGutterScreenFraction for the arithmetic), so they have to be
                // allowed to wrap rather than be clipped.
                prologue.style.whiteSpace = WhiteSpace.Normal;
                prologue.style.maxWidth = Length.Percent(_config.MenuTextMaxWidthPercent);
                _panel.Add(prologue);
            }

            var buttons = new VisualElement { name = "menu-buttons" };
            buttons.style.flexDirection = FlexDirection.Column;
            buttons.style.alignItems = Align.Stretch;
            buttons.style.flexShrink = 0f;
            buttons.style.marginTop = BlockSpacingPx;
            buttons.style.width = _config.MenuButtonWidthPx;
            buttons.style.maxWidth = Length.Percent(_config.MenuTextMaxWidthPercent);
            _panel.Add(buttons);

            _primaryButton = AddMenuButton(buttons, "menu-start", UiStrings.StartShiftButton, onPrimary);
            AddMenuButton(buttons, "menu-settings", UiStrings.MenuSettingsButton, onSettings);
            AddMenuButton(buttons, "menu-help", UiStrings.MenuHelpButton, onHelp);
            AddMenuButton(buttons, "menu-quit", UiStrings.MenuQuitButton, onQuit);

            _hint = new Label { name = "menu-hint", text = UiStrings.TitleHint };
            _hint.style.color = _config.TitleHintColor;
            _hint.style.fontSize = _config.TerminalFontSize;
            _hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.maxWidth = Length.Percent(_config.MenuTextMaxWidthPercent);
            _hint.style.marginTop = BlockSpacingPx;
            _hint.style.flexShrink = 0f;
            _panel.Add(_hint);
        }

        private Button AddMenuButton(VisualElement parent, string buttonName, string text, Action action)
        {
            Button button = StoryUi.CreateButton(text, _config, ButtonPaddingXPx, ButtonPaddingYPx);
            button.name = buttonName;
            button.style.marginBottom = ButtonSpacingPx;
            button.style.unityTextAlign = TextAnchor.MiddleCenter;
            if (action != null)
            {
                button.clicked += action;
            }

            parent.Add(button);
            _entries.Add(button);
            return button;
        }
    }
}
