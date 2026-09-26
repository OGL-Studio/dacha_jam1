using UnityEngine;

namespace NightShift.Game
{
    /// <summary>
    /// The player's window-size choice: the short list of supported sizes, the windowed/fullscreen
    /// flag, the <see cref="PlayerPrefs"/> keys they persist under, and the one call that pushes them
    /// into the engine. Backs the settings screen the player asked for
    /// («настройки с изменением размеров окна»).
    /// </summary>
    /// <remarks>
    /// <para><b>Why a static utility and not a MonoBehaviour.</b> There is exactly one window, the
    /// choice has to be re-applied before anything is drawn - <see cref="GameBootstrap.Build"/> calls
    /// <see cref="ApplySaved"/> as its first act - and <see cref="GameBootstrap.Restart"/> destroys and
    /// rebuilds the whole object graph, which a component holding this state would not survive. Nothing
    /// here touches the scene, so it needs no node of its own.</para>
    ///
    /// <para><b>Only 16:9 sizes are offered</b>, because the UI panel's reference resolution is 16:9
    /// (<see cref="ViewConfig.UiReferenceResolution"/>) and at that aspect the whole interface scales
    /// uniformly - the three sizes are the same layout at three magnifications. Other aspects are still
    /// handled (a fullscreen window inherits the desktop's aspect, whatever it is), just not offered:
    /// see <see cref="MapCamera.Frame"/>, which re-derives the map framing and the left-hand gutter from
    /// the live aspect ratio.</para>
    ///
    /// <para><b>Fullscreen mode.</b> <see cref="FullScreenMode.FullScreenWindow"/> - borderless
    /// desktop-resolution fullscreen - rather than
    /// <see cref="FullScreenMode.ExclusiveFullScreen"/>: it switches instantly, never changes the
    /// desktop's own mode, and alt-tabs cleanly, which matters far more for a jam game than a
    /// mode-switched exclusive window.</para>
    ///
    /// <para><b>In the Unity editor this has no visible effect.</b>
    /// <see cref="Screen.SetResolution"/> is a no-op against the Game view, whose size is set by the
    /// editor. The setting has to be checked in a built player.</para>
    /// </remarks>
    public static class DisplaySettings
    {
        /// <summary>The window sizes offered on the settings screen, all 16:9, smallest first.</summary>
        public static readonly Vector2Int[] WindowSizes =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
        };

        private const string WidthKey = "nightshift.display.width";
        private const string HeightKey = "nightshift.display.height";
        private const string FullscreenKey = "nightshift.display.fullscreen";

        private static bool _loaded;
        private static int _width;
        private static int _height;
        private static bool _fullscreen;

        /// <summary>Chosen window width in pixels. Falls back to the current window's width.</summary>
        public static int Width
        {
            get
            {
                EnsureLoaded();
                return _width;
            }
        }

        /// <summary>Chosen window height in pixels. Falls back to the current window's height.</summary>
        public static int Height
        {
            get
            {
                EnsureLoaded();
                return _height;
            }
        }

        /// <summary>True when the player chose fullscreen.</summary>
        public static bool Fullscreen
        {
            get
            {
                EnsureLoaded();
                return _fullscreen;
            }
        }

        /// <summary>True once the player has made a choice that was written to <see cref="PlayerPrefs"/>.</summary>
        public static bool HasSavedChoice => PlayerPrefs.HasKey(WidthKey) && PlayerPrefs.HasKey(HeightKey);

        /// <summary>
        /// Re-applies the saved choice. Call once at launch, before any view is built.
        /// </summary>
        /// <remarks>
        /// Does nothing at all when the player has never opened the settings screen, so a first launch
        /// keeps whatever size the platform gave it rather than being snapped to a size nobody asked
        /// for. Also does nothing when the window already matches, which is what makes it safe to call
        /// again from <see cref="GameBootstrap.Restart"/>.
        /// </remarks>
        public static void ApplySaved()
        {
            EnsureLoaded();

            if (!HasSavedChoice)
            {
                return;
            }

            ApplyToScreen(_width, _height, _fullscreen);
        }

        /// <summary>
        /// Stores a choice and applies it immediately.
        /// </summary>
        /// <param name="width">Window width in pixels. Clamped to at least 640.</param>
        /// <param name="height">Window height in pixels. Clamped to at least 360.</param>
        /// <param name="fullscreen">True for borderless fullscreen, false for a window.</param>
        public static void Apply(int width, int height, bool fullscreen)
        {
            _width = Mathf.Max(640, width);
            _height = Mathf.Max(360, height);
            _fullscreen = fullscreen;
            _loaded = true;

            PlayerPrefs.SetInt(WidthKey, _width);
            PlayerPrefs.SetInt(HeightKey, _height);
            PlayerPrefs.SetInt(FullscreenKey, _fullscreen ? 1 : 0);
            PlayerPrefs.Save();

            ApplyToScreen(_width, _height, _fullscreen);
        }

        /// <summary>The engine mode a windowed/fullscreen flag maps to.</summary>
        public static FullScreenMode ModeFor(bool fullscreen)
        {
            return fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        }

        /// <summary>
        /// True when <paramref name="size"/> is the size currently chosen - what the settings screen
        /// highlights.
        /// </summary>
        /// <remarks>
        /// Compared against the stored choice and not against <see cref="Screen.width"/> on purpose:
        /// a resize requested this frame is not visible in <c>Screen</c> until the next one, so
        /// highlighting from <c>Screen</c> would leave the button the player just pressed unlit.
        /// </remarks>
        public static bool IsCurrentSize(Vector2Int size) => size.x == Width && size.y == Height;

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            if (HasSavedChoice)
            {
                _width = PlayerPrefs.GetInt(WidthKey, Screen.width);
                _height = PlayerPrefs.GetInt(HeightKey, Screen.height);
                _fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
                return;
            }

            _width = Screen.width;
            _height = Screen.height;
            _fullscreen = Screen.fullScreen;
        }

        private static void ApplyToScreen(int width, int height, bool fullscreen)
        {
            FullScreenMode mode = ModeFor(fullscreen);

            bool alreadyThere = Screen.width == width &&
                                Screen.height == height &&
                                Screen.fullScreenMode == mode;
            if (alreadyThere)
            {
                return;
            }

            Screen.SetResolution(width, height, mode);
        }
    }
}
