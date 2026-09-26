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
            box.style.backgroundColor = config.ReportBoxColor;
            box.style.paddingLeft = config.StoryPanelPaddingPx;
            box.style.paddingRight = config.StoryPanelPaddingPx;
            box.style.paddingTop = config.StoryPanelPaddingPx;
            box.style.paddingBottom = config.StoryPanelPaddingPx;
            ApplyBorder(box, config);
            return box;
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
