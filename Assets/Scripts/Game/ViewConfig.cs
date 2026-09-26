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

        /// <summary>
        /// Share of a link's purchase price returned when the player removes it (acceptance
        /// criterion 3).
        /// </summary>
        /// <remarks>
        /// <b>This is a balance number and does not belong here.</b> It should sit in
        /// <see cref="GameData"/> next to <see cref="GameData.LinkCostPerCell"/>, but
        /// <c>NightShift.Core</c> has no link-removal concept at all (see
        /// <see cref="LinkRemovalShim"/>) and Story 003 does not own that assembly. Move it the
        /// moment Core grows a real <c>RemoveLink</c> API.
        /// </remarks>
        public float LinkRefundFraction = 0.5f;

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
