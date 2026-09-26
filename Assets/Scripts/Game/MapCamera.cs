using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// Owns the orthographic camera that frames the whole network grid. Part of Story 002
    /// acceptance criterion 1 (opening any empty scene brings up camera, map and HUD from code).
    /// </summary>
    /// <remarks>
    /// This project runs the Built-in Render Pipeline - no URP package is installed - so the camera
    /// is configured through plain <see cref="Camera"/> properties and needs no render-pipeline
    /// component. The framing is recomputed whenever the viewport aspect changes, so the grid stays
    /// fully visible at any window size, not only the 1280x720 target.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    public sealed class MapCamera : MonoBehaviour
    {
        private const float CameraDistance = -10f;

        private Camera _camera;
        private ViewConfig _config;
        private MapLayout _layout;
        private float _lastAspect = -1f;

        /// <summary>The configured camera. Exposed so later stories can convert screen to world points.</summary>
        public Camera Camera => _camera;

        /// <summary>Configures and frames the camera. Call once from the bootstrap.</summary>
        public void Initialize(ViewConfig config, MapLayout layout)
        {
            _config = config;
            _layout = layout;

            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = config.BackgroundColor;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;

            Frame();
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                return;
            }

            // Cheap guard: only re-frame when the viewport shape actually changed.
            if (!Mathf.Approximately(_camera.aspect, _lastAspect))
            {
                Frame();
            }
        }

        /// <summary>
        /// Sizes the orthographic view so the grid plus its margins fits both axes, shifts the view up
        /// by the configured top margin so the HUD bar never covers the top grid row, and shifts it
        /// left by a gutter wide enough for the left-hand UI panels at the window's current aspect
        /// ratio.
        /// </summary>
        /// <remarks>
        /// <para><b>Why the gutter cannot be a constant.</b> The panels are laid out in UI reference
        /// pixels and the map in world units, and the exchange rate between them is the window's aspect
        /// ratio - so <see cref="ViewConfig.LeftExtraCells"/>, a constant in cells, is only ever exactly
        /// right at one aspect. It was tuned at 16:9, where it leaves the Gateway at column 0 about
        /// three percent of the screen width clear of the day panel. At 4:3 - which a fullscreen window
        /// on a 4:3 or 5:4 display produces, since <c>Screen.SetResolution</c> cannot change a
        /// borderless fullscreen window's shape - the same constant leaves the panel overlapping the
        /// Gateway. Deriving the gutter from the panel width instead makes the clearance an invariant
        /// rather than a coincidence.</para>
        ///
        /// <para><b>The gutter is solved for, not just computed.</b> It is a <i>fraction of the visible
        /// width</i>, and the visible width depends on the gutter - so the width-bound case is the
        /// solution of <c>visible = base + fraction * visible</c>, which is the division by
        /// <c>1 - fraction</c> below. When the height axis is the binding one the visible width is
        /// larger than that solution, and the fraction is simply applied to it; the grid still fits,
        /// because a larger visible width satisfies the same inequality.</para>
        /// </remarks>
        public void Frame()
        {
            float cell = _layout.CellSize;
            float margin = _config.MarginCells * cell;
            float topExtra = _config.TopExtraCells * cell;

            float aspect = _camera.aspect > 0.0001f ? _camera.aspect : 1f;

            // The width the grid needs with no gutter at all, and the height it needs with the HUD's.
            float baseWidth = _layout.ContentWidth + 2f * margin;
            float requiredHeight = _layout.ContentHeight + 2f * margin + topExtra;

            float gutterFraction = LeftGutterScreenFraction(aspect);
            float minGutter = _config.LeftExtraCells * cell;

            float widthBound = Mathf.Max(baseWidth / (1f - gutterFraction), baseWidth + minGutter);
            float sizeFromWidth = widthBound / (2f * aspect);
            float sizeFromHeight = requiredHeight * 0.5f;

            float size = Mathf.Max(sizeFromHeight, sizeFromWidth);
            _camera.orthographicSize = size;

            float visibleWidth = 2f * size * aspect;
            float gutter = Mathf.Max(minGutter, gutterFraction * visibleWidth);

            // Shifting the camera left pushes the grid right on screen, opening the gutter the
            // left-hand panels occupy; the same trick as topExtra, mirrored onto x.
            transform.position = new Vector3(-gutter * 0.5f, topExtra * 0.5f, CameraDistance);

            _lastAspect = _camera.aspect;
        }

        /// <summary>
        /// The share of the screen width the left-hand day panel occupies at a given aspect ratio,
        /// including its margin and the clear gap kept beyond it.
        /// </summary>
        /// <remarks>
        /// <para><b>Reproducing the panel's own scaling.</b> <see cref="UiRoot"/> configures
        /// <c>PanelScaleMode.ScaleWithScreenSize</c>, <c>PanelScreenMatchMode.MatchWidthOrHeight</c> and
        /// <c>match = 0.5</c>, for which the scale factor is the geometric mean of the two axis ratios:
        /// <c>scale = sqrt((w/refW) * (h/refH))</c>. The panel's width in reference pixels is therefore
        /// <c>w / scale = refW * sqrt(aspect / refAspect)</c> - a function of the aspect ratio alone,
        /// which is why this needs no <see cref="Screen"/> query and stays correct at every one of
        /// <see cref="DisplaySettings.WindowSizes"/> (all 16:9, so all of them give exactly
        /// <c>refW</c>).</para>
        ///
        /// <para><b>The day panel, not the terminal.</b> The terminal is the wider of the two left-hand
        /// panels, but it is a short box in the bottom-left corner, while the day panel runs most of the
        /// screen's height - so reserving the terminal's full width as gutter would shrink the map by
        /// about a fifth to clear a panel that is nowhere near the rows it would have to clear. The
        /// day panel is what decides the gutter; the terminal instead gets a height cap
        /// (<see cref="ViewConfig.TerminalMaxHeightPercent"/>) and a width cap
        /// (<see cref="ViewConfig.LeftPanelMaxWidthPercent"/>), the same cap this applies, so the two
        /// agree. The consequence is deliberate and worth knowing: the bottom one or two cells of
        /// column 0 sit behind the terminal at night, as they always have.</para>
        /// </remarks>
        /// <param name="aspect">Viewport width divided by viewport height.</param>
        private float LeftGutterScreenFraction(float aspect)
        {
            Vector2Int reference = _config.UiReferenceResolution;
            if (reference.x <= 0 || reference.y <= 0)
            {
                return 0f;
            }

            float referenceAspect = (float)reference.x / reference.y;
            float logicalWidth = reference.x * Mathf.Sqrt(aspect / referenceAspect);
            if (logicalWidth <= 1f)
            {
                return 0f;
            }

            // The same clamp DayShopView puts on the panel itself, so this cannot reserve room for a
            // width the panel will not actually take.
            float panelCapPx = logicalWidth * _config.LeftPanelMaxWidthPercent * 0.01f;
            float panelPx = Mathf.Min(_config.DayPanelWidthPx, panelCapPx);
            float occupiedPx = _config.DayPanelMarginPx + panelPx + _config.LeftGutterGapPx;

            return Mathf.Clamp(occupiedPx / logicalWidth, 0f, _config.MaxLeftGutterFraction);
        }
    }
}
