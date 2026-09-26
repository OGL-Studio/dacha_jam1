using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// Creates the runtime UI Toolkit panel the HUD and the shift report draw into, entirely from
    /// code. Supports Story 002 acceptance criteria 1, 4 and 5.
    /// </summary>
    /// <remarks>
    /// <para><b>Why UI Toolkit.</b> `docs/engine-reference/unity/modules/ui.md` marks UI Toolkit as
    /// the recommended, production-ready runtime UI for Unity 6 and documents the no-UXML dynamic
    /// creation path used here. The UGUI path in that same file routes text through TextMeshPro,
    /// which is not installed in this project.</para>
    ///
    /// <para><b>No theme dependency.</b> A <see cref="PanelSettings"/> created at runtime has no
    /// theme style sheet, so this class never relies on theme-provided values: it assigns the font
    /// explicitly through <c>unityFontDefinition</c>, and the views set every colour and size
    /// explicitly. Expect one editor warning from Unity about the missing theme style sheet; it is a
    /// warning, not an error, and nothing on screen depends on the theme.</para>
    ///
    /// <para><b>Activation order matters.</b> <see cref="UIDocument"/> creates its panel in
    /// <c>OnEnable</c> and needs <see cref="UIDocument.panelSettings"/> already set. The bootstrap
    /// therefore creates the host GameObject <i>inactive</i>, calls <see cref="Configure"/>, activates
    /// it, and only then calls <see cref="CreateLayer"/>.</para>
    /// </remarks>
    public sealed class UiRoot : MonoBehaviour
    {
        private const float PanelSortingOrder = 100f;

        private ViewConfig _config;
        private PanelSettings _panelSettings;
        private UIDocument _document;
        private Font _font;
        private bool _rootStyled;

        /// <summary>The font resolved for all UI text, or null if no font could be resolved.</summary>
        public Font UiFont => _font;

        /// <summary>
        /// Builds the panel settings and the <see cref="UIDocument"/>. Must be called while this
        /// GameObject is still inactive.
        /// </summary>
        public void Configure(ViewConfig config)
        {
            _config = config;
            _font = ResolveFont(config);

            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "NightShiftPanelSettings";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = config.UiReferenceResolution;
            _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            _panelSettings.match = 0.5f;
            _panelSettings.sortingOrder = PanelSortingOrder;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
        }

        /// <summary>
        /// Adds a full-screen, absolutely positioned container to the panel root and returns it.
        /// </summary>
        /// <param name="layerName">Element name, for the UI Toolkit debugger.</param>
        /// <param name="ignorePicking">
        /// True for overlays that must not swallow mouse input - the HUD passes true so Story 003's
        /// map interaction keeps working underneath it.
        /// </param>
        public VisualElement CreateLayer(string layerName, bool ignorePicking)
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                Debug.LogError("[NightShift] UiRoot.CreateLayer called before the UIDocument panel existed. " +
                               "Activate the host GameObject after Configure().");
                return null;
            }

            if (!_rootStyled)
            {
                ApplyRootStyle(root);
                _rootStyled = true;
            }

            var layer = new VisualElement { name = layerName };
            layer.style.position = Position.Absolute;
            layer.style.left = 0f;
            layer.style.top = 0f;
            layer.style.right = 0f;
            layer.style.bottom = 0f;
            if (ignorePicking)
            {
                layer.pickingMode = PickingMode.Ignore;
            }

            root.Add(layer);
            return layer;
        }

        private void OnDestroy()
        {
            if (_panelSettings != null)
            {
                Destroy(_panelSettings);
                _panelSettings = null;
            }
        }

        /// <summary>
        /// Applies the panel-wide font and text style, and - load-bearing - makes the document root
        /// fill the panel.
        /// </summary>
        /// <remarks>
        /// <para><b>Why <c>flexGrow</c> is not cosmetic.</b> <see cref="UIDocument.rootVisualElement"/>
        /// is an ordinary flex child of the panel, so its height comes from its content. Every layer
        /// <see cref="CreateLayer"/> adds is absolutely positioned and therefore contributes nothing
        /// to that content height, leaving the root at full width and <i>zero height</i>. Anything
        /// that sizes itself from the root - a full-screen overlay pinned <c>top:0; bottom:0</c> -
        /// then collapses to zero height too, its background never renders, and its children shrink
        /// to nothing and overprint each other. <c>flexGrow = 1</c> gives the root the panel's own
        /// height, which is what every descendant's percentage and inset resolves against.</para>
        /// </remarks>
        private void ApplyRootStyle(VisualElement root)
        {
            if (_font != null)
            {
                root.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(_font));
            }

            root.style.flexGrow = 1f;
            root.style.color = _config.HudTextColor;
            root.style.fontSize = _config.HudFontSize;
        }

        /// <summary>
        /// Resolves a UI font without importing any asset: an OS font first (monospace preferred, per
        /// the brief's terminal art direction), then the engine's legacy built-in font as a fallback.
        /// </summary>
        private static Font ResolveFont(ViewConfig config)
        {
            Font font = Font.CreateDynamicFontFromOSFont(config.PreferredFontNames, Mathf.RoundToInt(config.HudFontSize));
            if (font != null)
            {
                return font;
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[NightShift] No UI font could be resolved; HUD text may not render. " + exception.Message);
                return null;
            }
        }
    }
}
