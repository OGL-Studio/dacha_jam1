using System;
using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Every presentation-layer constant the Unity view uses: colours, sizes, pool sizes, and the
    /// debug fast-forward knobs. Implements the view half of Story 002
    /// (`production/epics/night-shift/story-002-unity-night-view.md`).
    /// </summary>
    /// <remarks>
    /// Deliberately contains no gameplay balance: every balance number lives in
    /// <see cref="GameData"/> / <see cref="NightData"/> and is read from the simulation. The
    /// numbers here only change how the same simulation looks. Serializable so a later story can
    /// expose it in the Inspector or load it from JSON without touching the views.
    /// </remarks>
    [Serializable]
    public sealed class ViewConfig
    {
        // --- Layout (world units; one grid cell is CellSize units) ---

        /// <summary>World size of one grid cell.</summary>
        public float CellSize = 1f;

        /// <summary>Empty space kept around the grid on every side, in cells.</summary>
        public float MarginCells = 0.6f;

        /// <summary>Extra empty space above the grid, in cells, so the HUD bar never covers the top row.</summary>
        public float TopExtraCells = 1.2f;

        /// <summary>
        /// Minimum empty space to the left of the grid, in cells, so the left-hand panels never cover
        /// the first column. The day shop and the night terminal both sit there, and the Gateway is
        /// at column 0 — without this gutter it is hidden and cannot be clicked.
        /// </summary>
        /// <remarks>
        /// <b>A floor, not the whole answer.</b> Panels are sized in UI reference pixels and the map in
        /// world units, and how those two compare changes with the window's aspect ratio — so a gutter
        /// fixed in cells is only ever right at one aspect. <see cref="MapCamera.Frame"/> derives the
        /// gutter it actually uses from <see cref="DayPanelWidthPx"/> and the live aspect, and treats
        /// this value as the lower bound so the framing at the reference resolution cannot regress.
        /// </remarks>
        public float LeftExtraCells = 3.4f;

        // --- Colours ---

        public Color BackgroundColor = new Color(0.02f, 0.03f, 0.02f, 1f);
        public Color GridLineColor = new Color(0.09f, 0.22f, 0.11f, 1f);
        public Color LinkColor = new Color(0.22f, 0.62f, 0.28f, 1f);

        public Color GatewayColor = new Color(0.95f, 0.72f, 0.22f, 1f);
        public Color ServerColor = new Color(0.35f, 0.85f, 0.40f, 1f);
        public Color CoreColor = new Color(0.55f, 0.95f, 0.95f, 1f);
        public Color FirewallColor = new Color(0.95f, 0.42f, 0.25f, 1f);
        public Color IdsColor = new Color(0.45f, 0.65f, 0.98f, 1f);
        public Color HoneypotColor = new Color(0.92f, 0.85f, 0.35f, 1f);

        public Color StandardPacketColor = new Color(1f, 0.30f, 0.28f, 1f);
        public Color StealthPacketColor = new Color(0.78f, 0.38f, 0.95f, 1f);

        /// <summary>
        /// Halo drawn under every packet. Deliberately near-white and opaque: the Standard packet
        /// colour and <see cref="FirewallColor"/> are both red, so a bare red dot crossing a
        /// Firewall ring is nearly unreadable. The halo is the channel that keeps a packet legible
        /// against every node type it passes over.
        /// </summary>
        public Color PacketOutlineColor = new Color(1f, 1f, 1f, 1f);

        public Color BlockedFlashColor = new Color(0.45f, 1f, 0.55f, 1f);
        public Color LeakedFlashColor = new Color(1f, 0.30f, 0.25f, 1f);
        public Color DissipatedFlashColor = new Color(0.70f, 0.70f, 0.75f, 1f);

        // --- Sizes (world units) ---

        public float GridLineThickness = 0.02f;
        public float LinkThickness = 0.07f;
        public float NodeSize = 0.74f;
        public float PacketSize = 0.26f;

        /// <summary>Width of the packet halo on each side, in world units. Halo diameter is <see cref="PacketSize"/> + 2x this.</summary>
        public float PacketOutlineWidth = 0.07f;

        // --- Packet motion trail ---
        // A still screenshot cannot show movement. The trail is what makes "the packet is
        // travelling along this link, in this direction" readable in a single frame - which is both
        // what acceptance criterion 3 asks a reviewer to see and the only way the criterion can be
        // evidenced by a screenshot at all. Set PacketTrailLength to 0 to switch it off.

        /// <summary>Length of the streak drawn behind a moving packet, in world units.</summary>
        public float PacketTrailLength = 0.60f;

        /// <summary>Thickness of that streak, in world units.</summary>
        public float PacketTrailThickness = 0.10f;

        /// <summary>Alpha applied to the packet's own colour when drawing the streak.</summary>
        public float PacketTrailAlpha = 0.55f;

        // --- Packet destruction flash ---

        public float FlashDuration = 0.28f;
        public float FlashStartSize = 0.30f;
        public float FlashEndSize = 1.05f;

        // --- Pooling (pre-warmed so no allocation happens mid-night) ---

        public int PacketViewPoolSize = 24;
        public int FlashViewPoolSize = 12;

        // --- HUD ---

        public float HudFontSize = 20f;
        public Color HudTextColor = new Color(0.62f, 0.98f, 0.62f, 1f);
        public Color HudBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        /// <summary>Full-screen scrim behind the shift report. Opaque enough that the frozen map stops competing with the text.</summary>
        public Color ReportBackgroundColor = new Color(0f, 0.02f, 0f, 0.88f);

        /// <summary>Fill of the report panel itself, so the report reads as a panel and not as text floating over the map.</summary>
        public Color ReportBoxColor = new Color(0.03f, 0.07f, 0.03f, 1f);

        /// <summary>Border colour of the report panel.</summary>
        public Color ReportBoxBorderColor = new Color(0.30f, 0.72f, 0.36f, 1f);

        /// <summary>Border width of the report panel, in reference-resolution pixels.</summary>
        public float ReportBoxBorderWidthPx = 2f;

        public float ReportFontSize = 24f;

        /// <summary>Preferred UI fonts, tried in order. Monospace first, per the brief's terminal art direction.</summary>
        public string[] PreferredFontNames = { "Consolas", "Courier New", "Liberation Mono", "DejaVu Sans Mono", "Arial" };

        /// <summary>UI Toolkit reference resolution. Story 002 targets 60 FPS at 1280x720.</summary>
        public Vector2Int UiReferenceResolution = new Vector2Int(1280, 720);

        // --- Day phase (Story 003) ---

        /// <summary>Width of the day build panel, in reference-resolution pixels.</summary>
        public float DayPanelWidthPx = 268f;

        /// <summary>Distance the day panel is inset from the left and top screen edges, in reference-resolution pixels.</summary>
        public float DayPanelMarginPx = 12f;

        /// <summary>Fill of a shop button that is affordable and not selected.</summary>
        public Color ShopButtonColor = new Color(0.05f, 0.11f, 0.06f, 1f);

        /// <summary>Fill of the shop button whose node type is currently armed for placement.</summary>
        public Color ShopButtonSelectedColor = new Color(0.12f, 0.30f, 0.15f, 1f);

        /// <summary>Fill of a shop button the player cannot currently afford (acceptance criterion 6).</summary>
        public Color ShopButtonDisabledColor = new Color(0.06f, 0.06f, 0.06f, 1f);

        /// <summary>Text colour of a shop button the player cannot currently afford.</summary>
        public Color DisabledTextColor = new Color(0.42f, 0.45f, 0.42f, 1f);

        /// <summary>Border of the shop button whose node type is armed for placement.</summary>
        public Color ShopButtonSelectedBorderColor = new Color(0.72f, 0.98f, 0.72f, 1f);

        /// <summary>Colour of the short status / rejection line under the shop.</summary>
        public Color DayStatusColor = new Color(0.95f, 0.78f, 0.35f, 1f);

        public float DayFontSize = 18f;
        public float DayTitleFontSize = 22f;

        /// <summary>Tint of the translucent node ghost drawn on a legal target cell.</summary>
        public Color GhostValidColor = new Color(0.62f, 0.98f, 0.62f, 0.45f);

        /// <summary>Tint of the node ghost on an illegal cell, and of an over-long link drag (acceptance criterion 6).</summary>
        public Color GhostInvalidColor = new Color(1f, 0.32f, 0.28f, 0.45f);

        /// <summary>Colour of the rubber-band line drawn while dragging a link between two nodes.</summary>
        public Color LinkDragColor = new Color(0.72f, 0.98f, 0.72f, 0.75f);

        /// <summary>Colour of the ring marking the node selected for upgrade.</summary>
        public Color SelectionRingColor = new Color(1f, 1f, 1f, 0.85f);

        /// <summary>Diameter of that selection ring, as a multiple of <see cref="NodeSize"/>.</summary>
        public float SelectionRingScale = 1.35f;

        /// <summary>How close, in world units, a right-click must land to a link's centre line to remove it.</summary>
        public float LinkPickRadius = 0.22f;

        // --- Night phase: terminal (Story 004) ---

        /// <summary>Width of the terminal panel, in reference-resolution pixels.</summary>
        public float TerminalWidthPx = 430f;

        /// <summary>Height of the terminal panel, in reference-resolution pixels.</summary>
        public float TerminalHeightPx = 218f;

        /// <summary>Inset of the terminal panel from the left and bottom screen edges, in reference-resolution pixels.</summary>
        public float TerminalMarginPx = 12f;

        /// <summary>Fill of the terminal panel. Near-opaque so the log stays readable over the map.</summary>
        public Color TerminalBackgroundColor = new Color(0f, 0.04f, 0.01f, 0.92f);

        /// <summary>Border colour of the terminal panel.</summary>
        public Color TerminalBorderColor = new Color(0.26f, 0.68f, 0.32f, 1f);

        /// <summary>Border width of the terminal panel, in reference-resolution pixels.</summary>
        public float TerminalBorderWidthPx = 2f;

        /// <summary>Fill of the single-line input field.</summary>
        public Color TerminalInputBackgroundColor = new Color(0.02f, 0.10f, 0.03f, 1f);

        /// <summary>Colour of the echoed input lines.</summary>
        public Color TerminalEchoColor = new Color(0.55f, 0.80f, 0.55f, 1f);

        /// <summary>Colour of a successful command's output.</summary>
        public Color TerminalOkColor = new Color(0.68f, 0.99f, 0.68f, 1f);

        /// <summary>Colour of a refusal or an error.</summary>
        public Color TerminalErrorColor = new Color(1f, 0.52f, 0.36f, 1f);

        /// <summary>Colour of the controls hint under the input field.</summary>
        public Color TerminalHintColor = new Color(0.44f, 0.62f, 0.46f, 1f);

        public float TerminalFontSize = 15f;
        public float TerminalTitleFontSize = 17f;
        public float TerminalHintFontSize = 12f;

        /// <summary>Distance the terminal log scrolls per mouse-wheel notch, in reference-resolution pixels.</summary>
        public float TerminalScrollStepPx = 40f;

        /// <summary>Width of the terminal log's scroll indicator, in reference-resolution pixels.</summary>
        public float TerminalScrollBarWidthPx = 4f;

        /// <summary>Fill of that indicator. Only drawn while the log is taller than its viewport.</summary>
        public Color TerminalScrollBarColor = new Color(0.26f, 0.68f, 0.32f, 0.85f);

        /// <summary>
        /// Oldest lines are dropped once the log exceeds this many, so a long night cannot grow the
        /// panel's element count without bound.
        /// </summary>
        public int TerminalMaxLogLines = 160;

        /// <summary>Key that blurs the terminal input so the gameplay hotkeys work again.</summary>
        public KeyCode TerminalBlurKey = KeyCode.Escape;

        // --- Map node labels (Story 004 acceptance criterion 7) ---

        /// <summary>Colour of the <c>srv-1</c>-style name drawn under every node on the map.</summary>
        public Color NodeLabelColor = new Color(0.78f, 0.96f, 0.80f, 1f);

        /// <summary>
        /// Glyph resolution of the world-space node labels. Higher is sharper and costs font atlas
        /// space; the on-screen size is this times <see cref="NodeLabelCharacterSize"/>.
        /// </summary>
        public int NodeLabelFontSize = 48;

        /// <summary>World units per font unit for the node labels - the knob that sets their apparent size.</summary>
        public float NodeLabelCharacterSize = 0.042f;

        /// <summary>Vertical offset of a node label from its node's centre, in world units. Negative is below.</summary>
        public float NodeLabelOffsetY = -0.42f;

        // --- Story 006: title screen, letters, endings ---
        // All three are full-screen overlays inside a panel whose reference resolution is
        // UiReferenceResolution (1280x720), so every px value below is a 720p pixel: the widths are
        // chosen so the longest authored line in StoryLibrary fits without wrapping at that size.

        /// <summary>Scrim behind a full-screen story overlay. Opaque: the map must not compete with the text.</summary>
        public Color OverlayBackgroundColor = new Color(0f, 0.02f, 0.01f, 0.97f);

        /// <summary>Font size of the game's name on the title screen.</summary>
        public float TitleFontSize = 54f;

        /// <summary>Font size of the prologue lines under the title.</summary>
        public float TitleBodyFontSize = 20f;

        /// <summary>Colour of the dim controls hint at the bottom of the title screen.</summary>
        public Color TitleHintColor = new Color(0.40f, 0.60f, 0.42f, 1f);

        /// <summary>Width of the letter and ending panels, in reference-resolution pixels.</summary>
        public float StoryPanelWidthPx = 820f;

        /// <summary>Inner padding of the letter and ending panels, in reference-resolution pixels.</summary>
        public float StoryPanelPaddingPx = 26f;

        /// <summary>Font size of a letter's body and of the ending text.</summary>
        public float StoryBodyFontSize = 17f;

        /// <summary>Font size of a letter's header lines (from / subject) and of the screen title.</summary>
        public float StoryHeaderFontSize = 20f;

        /// <summary>Text colour of an intact letter: the same calm green as the rest of the terminal.</summary>
        public Color StoryTextColor = new Color(0.72f, 0.98f, 0.72f, 1f);

        /// <summary>
        /// Text colour of a damaged transmission - Story 006 criterion 2's «к ночи 5 письма
        /// искажены». The glyph damage is authored into the strings in <c>StoryLibrary</c>; this is the
        /// second channel, so the corruption reads even in a still screenshot.
        /// </summary>
        public Color StoryCorruptColor = new Color(1f, 0.47f, 0.33f, 1f);

        /// <summary>Colour of the letter counter and of the ending footer.</summary>
        public Color StoryMetaColor = new Color(0.46f, 0.66f, 0.48f, 1f);

        /// <summary>Colour of a story line printed into the night log by <see cref="StoryLogDirector"/>.</summary>
        public Color StoryLogColor = new Color(0.66f, 0.86f, 1f, 1f);

        // --- Window-size adaptation (player-requested polish pass) ---
        // UiRoot puts the panel in PanelScaleMode.ScaleWithScreenSize against UiReferenceResolution
        // with match 0.5, so at 16:9 every size in DisplaySettings.WindowSizes is the same layout at a
        // different magnification and nothing below is needed. What these knobs exist for is the other
        // aspects — a fullscreen window inherits the desktop's shape, and at match 0.5 a window wider
        // than 16:9 gives the panel MORE reference pixels across and FEWER down, a narrower one the
        // reverse. The percentages below are what stops a fixed-pixel panel from crowding the map in
        // the narrow case.

        /// <summary>
        /// Cap on the width of the two left-hand panels (day shop, night terminal), as a percentage of
        /// the screen, applied on top of their pixel widths.
        /// </summary>
        /// <remarks>
        /// <see cref="MapCamera.Frame"/> reserves its left gutter with the same cap, so the two can
        /// never disagree about how much of the screen the panels occupy.
        /// </remarks>
        public float LeftPanelMaxWidthPercent = 33f;

        /// <summary>Cap on the terminal panel's height, as a percentage of the screen.</summary>
        public float TerminalMaxHeightPercent = 42f;

        /// <summary>Clear space kept between a left-hand panel's right edge and the nearest node, in reference pixels.</summary>
        public float LeftGutterGapPx = 16f;

        /// <summary>
        /// Hard ceiling on the left gutter, as a fraction of the visible width, so a pathological aspect
        /// ratio cannot squeeze the map to nothing.
        /// </summary>
        public float MaxLeftGutterFraction = 0.45f;

        /// <summary>Cap on the letter and ending panels' width, as a percentage of the screen.</summary>
        public float StoryPanelMaxWidthPercent = 94f;

        // --- Main menu, settings and help screens ---

        /// <summary>Width of the main menu's entry buttons, in reference-resolution pixels.</summary>
        public float MenuButtonWidthPx = 320f;

        /// <summary>Cap on the menu's prologue, buttons and hint width, as a percentage of the screen.</summary>
        public float MenuTextMaxWidthPercent = 72f;

        /// <summary>Width of the settings content box, as a percentage of the screen.</summary>
        public float SettingsPanelWidthPercent = 58f;

        /// <summary>Width of the help content box, as a percentage of the screen. Two columns live in it.</summary>
        public float HelpPanelWidthPercent = 88f;

        /// <summary>
        /// Font size of the help screen's control lines. Smaller than
        /// <see cref="StoryBodyFontSize"/> on purpose: the help is the tallest screen in the game and
        /// has to fit the reference height with the title and the button counted.
        /// </summary>
        public float HelpBodyFontSize = 15f;

        // --- Debug (Story 002 acceptance criterion 6) ---

        /// <summary>Key that toggles the debug time scale between x1 and <see cref="FastForwardMultiplier"/>.</summary>
        public KeyCode FastForwardKey = KeyCode.F4;

        /// <summary>Debug time-scale multiplier. Not a balance value - it only changes how fast the same night is replayed.</summary>
        public float FastForwardMultiplier = 4f;

        /// <summary>Colour and icon shape are the two channels that distinguish node types on the map.</summary>
        public Color GetNodeColor(NodeType type)
        {
            switch (type)
            {
                case NodeType.Gateway: return GatewayColor;
                case NodeType.Server: return ServerColor;
                case NodeType.Core: return CoreColor;
                case NodeType.Firewall: return FirewallColor;
                case NodeType.Ids: return IdsColor;
                case NodeType.Honeypot: return HoneypotColor;
                default: return Color.white;
            }
        }

        /// <summary>Per-type icon shape, so node types stay distinguishable without colour vision.</summary>
        public PrimitiveShape GetNodeShape(NodeType type)
        {
            switch (type)
            {
                case NodeType.Gateway: return PrimitiveShape.Diamond;
                case NodeType.Server: return PrimitiveShape.Square;
                case NodeType.Core: return PrimitiveShape.Hexagon;
                case NodeType.Firewall: return PrimitiveShape.Ring;
                case NodeType.Ids: return PrimitiveShape.Triangle;
                case NodeType.Honeypot: return PrimitiveShape.Circle;
                default: return PrimitiveShape.Square;
            }
        }

        /// <summary>Packet colour by type. Hidden packets are not drawn at all until revealed, so this is only used for visible ones.</summary>
        public Color GetPacketColor(PacketType type)
        {
            switch (type)
            {
                case PacketType.Stealth: return StealthPacketColor;
                default: return StandardPacketColor;
            }
        }
    }
}
