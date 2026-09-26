using NightShift.Core;
using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// One pooled visual for one in-flight <see cref="Packet"/>. Implements the packet half of
    /// Story 002 acceptance criterion 3 ("packets visibly move along links; hidden packets are not
    /// visible until revealed").
    /// </summary>
    /// <remarks>
    /// <para><b>Three renderers, not one.</b> A bare coloured dot failed the criterion in practice
    /// for two reasons, and each renderer answers one of them:</para>
    /// <list type="bullet">
    ///   <item><b>Halo</b> - <see cref="ViewConfig.StandardPacketColor"/> and
    ///   <see cref="ViewConfig.FirewallColor"/> are both red, so a red dot crossing a red Firewall
    ///   ring was effectively invisible. The halo is drawn under the fill in a high-contrast colour
    ///   so the packet reads against every node type it passes over.</item>
    ///   <item><b>Trail</b> - a still frame cannot show motion. The streak points back along the
    ///   link the packet is crossing, so a single screenshot shows that the packet is travelling
    ///   and in which direction. Without it, "packets visibly move along links" is unevidenceable
    ///   from a screenshot and reads, frame by frame, as a dot parked somewhere.</item>
    ///   <item><b>Fill</b> - the packet's type colour, on top.</item>
    /// </list>
    ///
    /// <para><b>Scale lives on the children.</b> The pooled root keeps scale 1 so every child size
    /// in <see cref="ViewConfig"/> is a plain world-unit measurement and the rotated trail is not
    /// skewed by a non-uniform parent scale.</para>
    ///
    /// <para>Pooled and reused by <see cref="NetworkMapView"/>: a view is never created or destroyed
    /// while a night is running, so the per-frame path allocates nothing.</para>
    /// </remarks>
    public sealed class PacketView
    {
        private readonly Transform _transform;
        private readonly Transform _trailTransform;
        private readonly SpriteRenderer _trailRenderer;
        private readonly SpriteRenderer _outlineRenderer;
        private readonly SpriteRenderer _fillRenderer;
        private readonly ViewConfig _config;

        /// <summary>Id of the packet currently bound to this view, or -1 when pooled.</summary>
        public int PacketId { get; private set; } = -1;

        /// <summary>
        /// Frame stamp of the last <see cref="Apply"/>. <see cref="NetworkMapView"/> compares it
        /// against the current stamp to find views whose packet has left the simulation, without
        /// allocating a per-frame set.
        /// </summary>
        public int LastSeenStamp { get; set; } = -1;

        private PacketView(
            Transform transform,
            Transform trailTransform,
            SpriteRenderer trailRenderer,
            SpriteRenderer outlineRenderer,
            SpriteRenderer fillRenderer,
            ViewConfig config)
        {
            _transform = transform;
            _trailTransform = trailTransform;
            _trailRenderer = trailRenderer;
            _outlineRenderer = outlineRenderer;
            _fillRenderer = fillRenderer;
            _config = config;
        }

        /// <summary>Creates a pooled, initially hidden packet visual under <paramref name="parent"/>.</summary>
        /// <param name="parent">Pool root supplied by <see cref="NetworkMapView"/>.</param>
        /// <param name="config">Presentation constants; retained so <see cref="Apply"/> needs no per-frame argument for them.</param>
        /// <param name="sortingOrder">
        /// Base sorting order. The trail draws at this value, the halo one above it and the fill two
        /// above, so the three layers of a packet never fight each other.
        /// </param>
        /// <param name="index">Pool index, for a readable name in the hierarchy.</param>
        public static PacketView Create(Transform parent, ViewConfig config, int sortingOrder, int index)
        {
            var go = new GameObject("Packet" + index);
            go.transform.SetParent(parent, false);

            var trailGo = new GameObject("Trail");
            trailGo.transform.SetParent(go.transform, false);
            trailGo.transform.localScale = new Vector3(config.PacketTrailLength, config.PacketTrailThickness, 1f);
            var trailRenderer = trailGo.AddComponent<SpriteRenderer>();
            trailRenderer.sprite = PrimitiveSpriteFactory.GetWhitePixel();
            trailRenderer.sortingOrder = sortingOrder;
            trailRenderer.enabled = false;

            float outlineSize = config.PacketSize + 2f * config.PacketOutlineWidth;
            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(go.transform, false);
            outlineGo.transform.localScale = new Vector3(outlineSize, outlineSize, 1f);
            var outlineRenderer = outlineGo.AddComponent<SpriteRenderer>();
            outlineRenderer.sprite = PrimitiveSpriteFactory.GetShape(PrimitiveShape.Circle);
            outlineRenderer.color = config.PacketOutlineColor;
            outlineRenderer.sortingOrder = sortingOrder + 1;
            outlineRenderer.enabled = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            fillGo.transform.localScale = new Vector3(config.PacketSize, config.PacketSize, 1f);
            var fillRenderer = fillGo.AddComponent<SpriteRenderer>();
            fillRenderer.sprite = PrimitiveSpriteFactory.GetShape(PrimitiveShape.Circle);
            fillRenderer.sortingOrder = sortingOrder + 2;
            fillRenderer.enabled = false;

            return new PacketView(
                go.transform, trailGo.transform, trailRenderer, outlineRenderer, fillRenderer, config);
        }

        /// <summary>Binds this view to a packet. Call once when the view leaves the pool.</summary>
        public void Bind(int packetId)
        {
            PacketId = packetId;
        }

        /// <summary>
        /// Moves and tints the view for the current simulation state.
        /// </summary>
        /// <param name="worldPosition">
        /// Interpolated position along the packet's current link, from
        /// <see cref="NetworkSimulation"/>'s own <see cref="Packet.LinkProgress"/>.
        /// </param>
        /// <param name="travelDirection">
        /// Unit vector from the node the packet left towards the node it is heading for, or
        /// <see cref="Vector2.zero"/> when it is not crossing a link. Drives the trail only.
        /// </param>
        /// <param name="isVisible">
        /// Result of <see cref="NetworkSimulation.IsPacketVisible"/>. A hidden packet keeps moving
        /// but is not drawn, which is what makes an Ids reveal read as the packet popping into view.
        /// </param>
        /// <param name="color">Tint for the packet's type.</param>
        public void Apply(Vector2 worldPosition, Vector2 travelDirection, bool isVisible, Color color)
        {
            _transform.localPosition = new Vector3(worldPosition.x, worldPosition.y, 0f);

            _outlineRenderer.enabled = isVisible;
            _fillRenderer.enabled = isVisible;

            bool drawTrail = isVisible
                             && _config.PacketTrailLength > 0f
                             && travelDirection.sqrMagnitude > 0.000001f;
            _trailRenderer.enabled = drawTrail;

            if (!isVisible)
            {
                return;
            }

            _fillRenderer.color = color;
            _outlineRenderer.color = _config.PacketOutlineColor;

            if (!drawTrail)
            {
                return;
            }

            Color trailColor = color;
            trailColor.a *= Mathf.Clamp01(_config.PacketTrailAlpha);
            _trailRenderer.color = trailColor;

            // The streak sprite is one unit long with a centre pivot, so shifting it back by half
            // its length makes it start at the packet and extend backwards along the link.
            float halfLength = _config.PacketTrailLength * 0.5f;
            _trailTransform.localPosition =
                new Vector3(-travelDirection.x * halfLength, -travelDirection.y * halfLength, 0f);
            _trailTransform.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(travelDirection.y, travelDirection.x) * Mathf.Rad2Deg);
        }

        /// <summary>Hides the view and returns it to the pooled state.</summary>
        public void Release()
        {
            PacketId = -1;
            LastSeenStamp = -1;
            _trailRenderer.enabled = false;
            _outlineRenderer.enabled = false;
            _fillRenderer.enabled = false;
        }
    }
}
