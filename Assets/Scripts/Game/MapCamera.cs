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
        /// Sizes the orthographic view so the grid plus its margins fits both axes, and shifts the
        /// view up by the configured top margin so the HUD bar never covers the top grid row.
        /// </summary>
        public void Frame()
        {
            float cell = _layout.CellSize;
            float margin = _config.MarginCells * cell;
            float topExtra = _config.TopExtraCells * cell;
            float leftExtra = _config.LeftExtraCells * cell;

            float requiredHeight = _layout.ContentHeight + 2f * margin + topExtra;
            float requiredWidth = _layout.ContentWidth + 2f * margin + leftExtra;

            float aspect = _camera.aspect;
            float sizeFromHeight = requiredHeight * 0.5f;
            float sizeFromWidth = aspect > 0.0001f ? requiredWidth / (2f * aspect) : sizeFromHeight;

            _camera.orthographicSize = Mathf.Max(sizeFromHeight, sizeFromWidth);

            // Shifting the camera left pushes the grid right on screen, opening the gutter the
            // left-hand panels occupy; the same trick as topExtra, mirrored onto x.
            transform.position = new Vector3(-leftExtra * 0.5f, topExtra * 0.5f, CameraDistance);

            _lastAspect = aspect;
        }
    }
}
