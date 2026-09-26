using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The help screen: the goal of the campaign and every control the game actually reads, in two
    /// columns - the day's mouse controls and the night's terminal commands. Covers the player's
    /// request «там же где настройки добавь справку по игре с управлением».
    /// </summary>
    /// <remarks>
    /// <para><b>The text is documentation of the code, not of the design doc.</b> Every line in
    /// <see cref="UiStrings.HelpDayLines"/> and <see cref="UiStrings.HelpNightLines"/> was read back off
    /// <see cref="DayBuildController"/>, <see cref="TerminalView"/> and
    /// <see cref="NightShift.Core.TerminalCommandProcessor"/>. In particular Escape does <i>not</i> open
    /// the menu during the night - there it blurs the terminal field, which
    /// <see cref="TerminalView"/> owns - and the help says so rather than promising a key that is
    /// already taken.</para>
    ///
    /// <para><b>Two columns, sized in percentages.</b> A single column of twenty lines does not fit 720
    /// reference pixels of height once the title and the button are counted, and a window wider than
    /// 16:9 gives the panel <i>less</i> reference height, not more. Two flexible columns halve the
    /// height and spend the width the wide case does have.</para>
    ///
    /// <para><b>Layer picking.</b> Built into a layer that ignores picking, with a pickable panel that
    /// is <c>display: none</c> while the screen is down. See <see cref="GameBootstrap"/>.</para>
    /// </remarks>
    public sealed class HelpView : MonoBehaviour
    {
        private const int SectionSpacingPx = 14;
        private const int RowSpacingPx = 6;
        private const int ColumnGapPx = 22;
        private const int BackPaddingXPx = 28;
        private const int BackPaddingYPx = 10;

        private ViewConfig _config;
        private VisualElement _panel;

        /// <summary>True while the help screen is on screen.</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Builds the help screen, hidden. The content is static, so it is built once and never
        /// refreshed.
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
                Debug.LogError("[NightShift] HelpView.Initialize got a null UI layer; help will not appear.");
                return;
            }

            BuildPanel(parent, onBack);
        }

        /// <summary>Puts the help screen up.</summary>
        public void Show()
        {
            if (_panel == null)
            {
                return;
            }

            _panel.style.display = DisplayStyle.Flex;
            IsShowing = true;
        }

        /// <summary>Takes the help screen down.</summary>
        public void Hide()
        {
            if (_panel != null)
            {
                _panel.style.display = DisplayStyle.None;
            }

            IsShowing = false;
        }

        private void BuildPanel(VisualElement parent, Action onBack)
        {
            _panel = StoryUi.CreateOverlayPanel(parent, "help-panel", _config);
            _panel.Add(StoryUi.CreateScreenTitle(UiStrings.HelpTitle, _config));

            VisualElement box = StoryUi.CreateRelativeBox("help-box", _config, _config.HelpPanelWidthPercent);
            _panel.Add(box);

            // The goal spans both columns: it is the one thing a player who reads nothing else needs.
            box.Add(StoryUi.CreateSectionHeader(UiStrings.HelpGoalHeader, _config, 0f));
            AddLines(box, UiStrings.HelpGoalLines);

            var columns = new VisualElement { name = "help-columns" };
            columns.style.flexDirection = FlexDirection.Row;
            columns.style.alignItems = Align.FlexStart;
            columns.style.flexShrink = 0f;
            columns.style.marginTop = SectionSpacingPx;
            box.Add(columns);

            VisualElement dayColumn = AddColumn(columns, "help-day", true);
            dayColumn.Add(StoryUi.CreateSectionHeader(UiStrings.HelpDayHeader, _config, 0f));
            AddLines(dayColumn, UiStrings.HelpDayLines);

            VisualElement nightColumn = AddColumn(columns, "help-night", false);
            nightColumn.Add(StoryUi.CreateSectionHeader(UiStrings.HelpNightHeader, _config, 0f));
            AddLines(nightColumn, UiStrings.HelpNightLines);

            Label footer = StoryUi.CreateBodyLabel(UiStrings.HelpFooterLine, _config, false);
            footer.name = "help-footer";
            footer.style.color = _config.StoryMetaColor;
            footer.style.fontSize = _config.TerminalFontSize;
            footer.style.marginTop = SectionSpacingPx;
            box.Add(footer);

            Button back = StoryUi.CreateButton(UiStrings.BackButton, _config, BackPaddingXPx, BackPaddingYPx);
            back.name = "help-back";
            back.style.marginTop = SectionSpacingPx + RowSpacingPx;
            if (onBack != null)
            {
                back.clicked += onBack;
            }

            _panel.Add(back);

            var hint = new Label { name = "help-hint", text = UiStrings.MenuBackHint };
            hint.style.color = _config.TitleHintColor;
            hint.style.fontSize = _config.TerminalFontSize;
            hint.style.unityTextAlign = TextAnchor.MiddleCenter;
            hint.style.marginTop = RowSpacingPx;
            hint.style.flexShrink = 0f;
            _panel.Add(hint);
        }

        /// <summary>
        /// One half of the two-column block.
        /// </summary>
        /// <remarks>
        /// <c>flexBasis: 0</c> with <c>flexGrow: 1</c> and <c>minWidth: 0</c> is what makes the two
        /// columns share the box evenly <i>and</i> lets their text wrap: a flex child will not shrink
        /// below its own content width unless <c>minWidth</c> says it may, and without that the long
        /// lines would push the columns wider than the box instead of wrapping inside it.
        /// <c>alignItems: Stretch</c> then gives each label the column's width to wrap against.
        /// </remarks>
        private VisualElement AddColumn(VisualElement parent, string columnName, bool gapAfter)
        {
            var column = new VisualElement { name = columnName };
            column.style.flexDirection = FlexDirection.Column;
            column.style.alignItems = Align.Stretch;
            column.style.flexBasis = 0f;
            column.style.flexGrow = 1f;
            column.style.flexShrink = 1f;
            column.style.minWidth = 0f;
            if (gapAfter)
            {
                column.style.marginRight = ColumnGapPx;
            }

            parent.Add(column);
            return column;
        }

        private void AddLines(VisualElement parent, string[] lines)
        {
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                Label label = StoryUi.CreateBodyLabel(line, _config, false);
                label.style.fontSize = _config.HelpBodyFontSize;
                label.style.marginBottom = RowSpacingPx;
                parent.Add(label);
            }
        }
    }
}
