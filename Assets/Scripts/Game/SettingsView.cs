using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The settings screen: window size and windowed/fullscreen, applied immediately and remembered.
    /// Covers the player's request «нужно добавить настройки с изменением размеров окна».
    /// </summary>
    /// <remarks>
    /// <para><b>It owns no policy.</b> The list of sizes, the persistence and the engine call all live
    /// in <see cref="DisplaySettings"/>; this view reads that list, draws a button per entry, and calls
    /// <see cref="DisplaySettings.Apply"/>. The same separation as everywhere else in this layer - the
    /// view renders state it does not own.</para>
    ///
    /// <para><b>Sized in percentages, not pixels.</b> The content box is a percentage of the screen with
    /// a pixel cap, so a window whose aspect is narrower than 16:9 - which gives the UI panel fewer
    /// reference pixels across - shrinks the box instead of pushing it off the edges. The size buttons
    /// are in a wrapping row for the same reason.</para>
    ///
    /// <para><b>Layer picking.</b> Built into a layer that ignores picking, with a pickable panel that
    /// is <c>display: none</c> while the screen is down. See <see cref="GameBootstrap"/>.</para>
    /// </remarks>
    public sealed class SettingsView : MonoBehaviour
    {
        private const int SectionSpacingPx = 16;
        private const int RowSpacingPx = 8;
        private const int OptionPaddingXPx = 16;
        private const int OptionPaddingYPx = 8;
        private const int BackPaddingXPx = 28;
        private const int BackPaddingYPx = 10;

        private ViewConfig _config;
        private VisualElement _panel;
        private Label _currentLabel;

        private readonly List<Button> _sizeButtons = new List<Button>();
        private Button _windowedButton;
        private Button _fullscreenButton;

        /// <summary>True while the settings screen is on screen.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the settings screen, hidden.
        /// </summary>
        /// <param name="config">Presentation constants.</param>
        /// <param name="parent">UI Toolkit layer from <see cref="UiRoot.CreateLayer"/>.</param>
        /// <param name="onBack">
        /// Returns to the main menu. Supplied by <see cref="MenuController"/>, which also maps Escape
        /// onto it - this view polls no input of its own.
        /// </param>
        public void Initialize(ViewConfig config, VisualElement parent, Action onBack)
        {
            _config = config;

            if (parent == null)
            {
                Debug.LogError("[NightShift] SettingsView.Initialize got a null UI layer; settings will not appear.");
                return;
            }

            BuildPanel(parent, onBack);
        }

        /// <summary>Puts the settings screen up and re-reads the current choice.</summary>
        public void Show()
        {
            if (_panel == null)
            {
                return;
            }

            Refresh();
            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Takes the settings screen down.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
        }

        /// <summary>Re-reads <see cref="DisplaySettings"/> and re-styles every option.</summary>
        private void Refresh()
        {
            bool fullscreen = DisplaySettings.Fullscreen;

            for (int i = 0; i < _sizeButtons.Count && i < DisplaySettings.WindowSizes.Length; i++)
            {
                StyleOption(_sizeButtons[i], DisplaySettings.IsCurrentSize(DisplaySettings.WindowSizes[i]));
            }

            StyleOption(_windowedButton, !fullscreen);
            StyleOption(_fullscreenButton, fullscreen);

            _currentLabel.text = string.Format(
                UiStrings.SettingsCurrentFormat,
                DisplaySettings.Width,
                DisplaySettings.Height,
                fullscreen ? UiStrings.SettingsModeFullscreen : UiStrings.SettingsModeWindowed);
        }

        private void ApplySize(Vector2Int size)
        {
            DisplaySettings.Apply(size.x, size.y, DisplaySettings.Fullscreen);
            Refresh();
        }

        private void ApplyMode(bool fullscreen)
        {
            DisplaySettings.Apply(DisplaySettings.Width, DisplaySettings.Height, fullscreen);
            Refresh();
        }

        private void BuildPanel(VisualElement parent, Action onBack)
        {
            _panel = StoryUi.CreateOverlayPanel(parent, "settings-panel", _config);
            _panel.Add(StoryUi.CreateScreenTitle(UiStrings.SettingsTitle, _config));

            VisualElement box = StoryUi.CreateRelativeBox("settings-box", _config, _config.SettingsPanelWidthPercent);
            _panel.Add(box);

            box.Add(StoryUi.CreateSectionHeader(UiStrings.SettingsWindowSizeHeader, _config, 0f));

            VisualElement sizeRow = CreateOptionRow("settings-sizes");
            box.Add(sizeRow);

            foreach (Vector2Int size in DisplaySettings.WindowSizes)
            {
                Vector2Int captured = size;
                Button button = AddOption(
                    sizeRow,
                    "settings-size-" + size.x + "x" + size.y,
                    string.Format(UiStrings.SettingsSizeFormat, size.x, size.y),
                    () => ApplySize(captured));
                _sizeButtons.Add(button);
            }

            box.Add(StoryUi.CreateSectionHeader(UiStrings.SettingsModeHeader, _config, SectionSpacingPx));

            VisualElement modeRow = CreateOptionRow("settings-modes");
            box.Add(modeRow);

            _windowedButton = AddOption(modeRow, "settings-windowed", UiStrings.SettingsModeWindowed, () => ApplyMode(false));
            _fullscreenButton = AddOption(modeRow, "settings-fullscreen", UiStrings.SettingsModeFullscreen, () => ApplyMode(true));

            _currentLabel = StoryUi.CreateBodyLabel(string.Empty, _config, false);
            _currentLabel.name = "settings-current";
            _currentLabel.style.marginTop = SectionSpacingPx;
            box.Add(_currentLabel);

            Label note = StoryUi.CreateBodyLabel(UiStrings.SettingsNote, _config, false);
            note.name = "settings-note";
            note.style.color = _config.StoryMetaColor;
            note.style.fontSize = _config.TerminalFontSize;
            note.style.marginTop = RowSpacingPx;
            box.Add(note);

            Button back = StoryUi.CreateButton(UiStrings.BackButton, _config, BackPaddingXPx, BackPaddingYPx);
            back.name = "settings-back";
            back.style.marginTop = SectionSpacingPx + RowSpacingPx;
            if (onBack != null)
            {
                back.clicked += onBack;
            }

            _panel.Add(back);

            var hint = new Label { name = "settings-hint", text = UiStrings.MenuBackHint };
            hint.style.color = _config.TitleHintColor;
            hint.style.fontSize = _config.TerminalFontSize;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = RowSpacingPx;
            hint.style.flexShrink = 0f;
            _panel.Add(hint);
        }

        /// <summary>
        /// A wrapping row. Wrapping is the cheap insurance against a narrow window: the options drop
        /// onto a second line instead of overflowing the box.
        /// </summary>
        private VisualElement CreateOptionRow(string rowName)
        {
            var row = new VisualElement { name = rowName };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.flexShrink = 0f;
            return row;
        }

        private Button AddOption(VisualElement parent, string buttonName, string text, Action action)
        {
            Button button = StoryUi.CreateButton(text, _config, OptionPaddingXPx, OptionPaddingYPx);
            button.name = buttonName;
            button.style.marginRight = RowSpacingPx;
            button.style.marginBottom = RowSpacingPx;
            if (action != null)
            {
                button.clicked += action;
            }

            parent.Add(button);
            return button;
        }

        /// <summary>
        /// Lights the chosen option. Two channels, fill and border, for the same reason the shop's
        /// armed button has both: fill alone is hard to read at a glance on a dark panel.
        /// </summary>
        private void StyleOption(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            button.style.backgroundColor = selected ? _config.ShopButtonSelectedColor : _config.ShopButtonColor;
            button.style.color = selected ? _config.HudTextColor : _config.StoryTextColor;

            Color border = selected ? _config.ShopButtonSelectedBorderColor : _config.ReportBoxBorderColor;
            button.style.borderTopColor = border;
            button.style.borderBottomColor = border;
            button.style.borderLeftColor = border;
            button.style.borderRightColor = border;
        }
    }
}
