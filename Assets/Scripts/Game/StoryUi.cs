using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// The three or four styling moves that Story 006's full-screen screens (title, letters, defeat,
    /// ending) all repeat, in one place.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a helper and not a base class.</b> The screens are <see cref="MonoBehaviour"/>s
    /// created by <see cref="GameBootstrap"/> with different dependencies; sharing a base class would
    /// buy nothing and make each one harder to read. What they genuinely share is pixel styling, which
    /// with no UI Toolkit theme in this project has to be assigned property by property - so that is
    /// what is factored out.</para>
    ///
    /// <para><b>Every value comes from <see cref="ViewConfig"/>.</b> Nothing here invents a colour or
    /// a font size; the pixel constants that do appear are spacing, passed in by the caller.</para>
    /// </remarks>
    public static class StoryUi
    {
        /// <summary>
        /// Adds a hidden, full-screen, vertically centred overlay panel to <paramref name="parent"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>Pickable on purpose, hidden on purpose.</b> The panel is left at the default
        /// picking mode so that while it is up it absorbs clicks aimed at the UI beneath it, and it is
        /// created with <c>display: none</c> so that while it is down it is out of the picking tree
        /// altogether and cannot swallow anything. This is the same split the terminal layer uses: the
        /// <i>layer</i> ignores picking, the panel inside it does not.</para>
        ///
        /// <para><b>Map clicks are a separate problem.</b> Map interaction is polled from the legacy
        /// <see cref="Input"/> class, which UI Toolkit hit-testing does not feed, so an overlay panel
        /// cannot block node placement by being pickable - see
        /// <see cref="DayBuildController.ModalOpen"/>, which is how the letter screen actually stops
        /// the player building underneath it.</para>
        /// </remarks>
        /// <param name="parent">Layer from <see cref="UiRoot.CreateLayer"/>.</param>
        /// <param name="panelName">Element name, for the UI Toolkit debugger.</param>
        /// <param name="config">Presentation constants.</param>
        /// <returns>The panel, a centred flex column. Callers add their content to it.</returns>
        public static VisualElement CreateOverlayPanel(VisualElement parent, string panelName, ViewConfig config)
        {
            var panel = new VisualElement { name = panelName };
            panel.style.position = Position.Absolute;
            panel.style.left = 0f;
            panel.style.top = 0f;
            panel.style.right = 0f;
            panel.style.bottom = 0f;
            panel.style.flexDirection = FlexDirection.Column;
            panel.style.alignItems = Align.Center;
            panel.style.justifyContent = Justify.Center;
            panel.style.backgroundColor = config.OverlayBackgroundColor;
            panel.style.display = DisplayStyle.None;
            parent.Add(panel);
            return panel;
        }

        /// <summary>
        /// Creates a bordered, padded content box of <see cref="ViewConfig.StoryPanelWidthPx"/> width.
        /// </summary>
        /// <remarks>
        /// <c>flexShrink = 0</c> is not cosmetic here - see the comment in
        /// <see cref="NightReportView"/>: UI Toolkit shrinks flex children by default, and in a
        /// container that ever resolves to zero height every line collapses and overprints the others.
        /// </remarks>
        public static VisualElement CreateBox(string boxName, ViewConfig config)
        {
            var box = new VisualElement { name = boxName };
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.FlexStart;
            box.style.flexShrink = 0f;
            box.style.width = config.StoryPanelWidthPx;

            // The width above is a 720p pixel count chosen so the longest authored line fits. A window
            // narrower than 16:9 gives the panel fewer reference pixels across, at which point that
            // count would hang off both edges - so it is also capped as a share of the screen.
            box.style.maxWidth = Length.Percent(config.StoryPanelMaxWidthPercent);
            box.style.backgroundColor = config.ReportBoxColor;
            box.style.paddingLeft = config.StoryPanelPaddingPx;
            box.style.paddingRight = config.StoryPanelPaddingPx;
            box.style.paddingTop = config.StoryPanelPaddingPx;
            box.style.paddingBottom = config.StoryPanelPaddingPx;
            ApplyBorder(box, config);
            return box;
        }

        /// <summary>
        /// A content box whose width is a percentage of the screen rather than a pixel count, capped at
        /// <see cref="ViewConfig.StoryPanelWidthPx"/>.
        /// </summary>
        /// <remarks>
        /// <para><b>Why the settings and help screens use this and the letters do not.</b> A letter's
        /// width is dictated by its authored lines, which were written to fit a known pixel measure. The
        /// two new screens have no authored line length - their text wraps - so they are better off
        /// scaling with the window, which is what makes them survive an aspect ratio the pixel measure
        /// was never chosen for.</para>
        ///
        /// <para><c>alignItems: Stretch</c>, unlike <see cref="CreateBox"/>: children of these screens
        /// are wrapping labels and flexible columns, and both need the box's full width to lay out
        /// against.</para>
        /// </remarks>
        /// <param name="boxName">Element name, for the UI Toolkit debugger.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="widthPercent">Width as a percentage of the parent, i.e. of the screen.</param>
        public static VisualElement CreateRelativeBox(string boxName, ViewConfig config, float widthPercent)
        {
            var box = new VisualElement { name = boxName };
            box.style.flexDirection = FlexDirection.Column;
            box.style.alignItems = Align.Stretch;
            box.style.flexShrink = 0f;
            box.style.width = Length.Percent(widthPercent);
            box.style.maxWidth = config.StoryPanelWidthPx * 1.5f;
            box.style.backgroundColor = config.ReportBoxColor;
            box.style.paddingLeft = config.StoryPanelPaddingPx;
            box.style.paddingRight = config.StoryPanelPaddingPx;
            box.style.paddingTop = config.StoryPanelPaddingPx;
            box.style.paddingBottom = config.StoryPanelPaddingPx;
            ApplyBorder(box, config);
            return box;
        }

        /// <summary>
        /// The bold heading above a full-screen screen's content box, styled like the letter screen's.
        /// </summary>
        public static Label CreateScreenTitle(string text, ViewConfig config)
        {
            var title = new Label { name = "screen-title", text = text };
            title.style.color = config.HudTextColor;
            title.style.fontSize = config.StoryHeaderFontSize;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 10f;
            title.style.flexShrink = 0f;
            return title;
        }

        /// <summary>
        /// A section heading inside a content box - «РАЗМЕР ОКНА», «НОЧЬ — ТЕРМИНАЛ».
        /// </summary>
        /// <param name="text">The heading, from <see cref="UiStrings"/>.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="marginTopPx">Space above the heading, in reference-resolution pixels.</param>
        public static Label CreateSectionHeader(string text, ViewConfig config, float marginTopPx)
        {
            var header = new Label { text = text };
            header.style.color = config.HudTextColor;
            header.style.fontSize = config.TerminalTitleFontSize;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.whiteSpace = WhiteSpace.Normal;
            header.style.marginTop = marginTopPx;
            header.style.marginBottom = 8f;
            header.style.flexShrink = 0f;
            return header;
        }

        /// <summary>Applies <see cref="ViewConfig.ReportBoxBorderColor"/> on all four sides.</summary>
        public static void ApplyBorder(VisualElement element, ViewConfig config)
        {
            element.style.borderTopWidth = config.ReportBoxBorderWidthPx;
            element.style.borderBottomWidth = config.ReportBoxBorderWidthPx;
            element.style.borderLeftWidth = config.ReportBoxBorderWidthPx;
            element.style.borderRightWidth = config.ReportBoxBorderWidthPx;
            element.style.borderTopColor = config.ReportBoxBorderColor;
            element.style.borderBottomColor = config.ReportBoxBorderColor;
            element.style.borderLeftColor = config.ReportBoxBorderColor;
            element.style.borderRightColor = config.ReportBoxBorderColor;
        }

        /// <summary>
        /// Creates a button styled like the shift report's own, so every screen's primary action looks
        /// the same.
        /// </summary>
        /// <param name="text">Caption, from <see cref="UiStrings"/>.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="paddingXPx">Horizontal padding, in reference-resolution pixels.</param>
        /// <param name="paddingYPx">Vertical padding, in reference-resolution pixels.</param>
        public static Button CreateButton(string text, ViewConfig config, float paddingXPx, float paddingYPx)
        {
            var button = new Button { text = text };
            button.style.fontSize = config.ReportFontSize;
            button.style.color = config.HudTextColor;
            button.style.backgroundColor = config.ShopButtonSelectedColor;
            button.style.flexShrink = 0f;
            button.style.marginLeft = 0f;
            button.style.marginRight = 0f;
            button.style.paddingLeft = paddingXPx;
            button.style.paddingRight = paddingXPx;
            button.style.paddingTop = paddingYPx;
            button.style.paddingBottom = paddingYPx;
            ApplyBorder(button, config);
            return button;
        }

        /// <summary>
        /// Creates a body-text label: wrapping, non-shrinking, in the intact or the damaged colour.
        /// </summary>
        /// <param name="text">The line. May be empty, which renders as a blank spacer row.</param>
        /// <param name="config">Presentation constants.</param>
        /// <param name="corrupted">True to use <see cref="ViewConfig.StoryCorruptColor"/>.</param>
        public static Label CreateBodyLabel(string text, ViewConfig config, bool corrupted)
        {
            var label = new Label { text = text };
            label.style.color = corrupted ? config.StoryCorruptColor : config.StoryTextColor;
            label.style.fontSize = config.StoryBodyFontSize;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.flexShrink = 0f;
            return label;
        }
    }
}
