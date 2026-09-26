using NightShift.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightShift.Game
{
    /// <summary>
    /// Brings the whole game up from code. Implements Story 002 acceptance criterion 1 of
    /// `production/epics/night-shift/story-002-unity-night-view.md`: opening the `Main` scene - or
    /// any empty scene - raises camera, map and HUD with no manual scene assembly in the editor.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a bootstrap instead of a prefab or an authored scene.</b> Nothing in the project
    /// then depends on editor-side wiring that a merge can silently break, the whole object graph is
    /// reviewable as code, and every scene - including an empty one - is playable. This is the
    /// architectural spine of the story.</para>
    ///
    /// <para><b>Wiring order is load-bearing.</b> The simulation and the fixed network are built
    /// first; then the views subscribe to simulation events; then the first phase is entered last -
    /// <see cref="GameRunner.BeginDay"/> normally, <see cref="GameRunner.StartNight"/> under
    /// <see cref="SkipDayArg"/>. Entering a phase before the views existed would drop the first phase
    /// and packet events on the floor. <see cref="GameRunner.Initialize"/> likewise precedes
    /// <see cref="NightHudView.Initialize"/> and <see cref="DayShopView.Initialize"/>, which read the
    /// phase, the day number and the time scale off the runner.</para>
    ///
    /// <para><b>Dependency injection, no singletons.</b> Every component receives what it needs
    /// through an <c>Initialize</c> call, so each is testable in isolation and nothing reaches for a
    /// global.</para>
    /// </remarks>
    public static class GameBootstrap
    {
        /// <summary>Name of the root GameObject the bootstrap creates.</summary>
        public const string RootObjectName = "NightShift";

        /// <summary>
        /// Launch flag that skips the opening day and starts night 1 immediately, i.e. the Story 002
        /// launch behaviour.
        /// </summary>
        /// <remarks>
        /// <see cref="QaScreenshot"/>'s <c>--atsim</c> capture schedules on the night clock and waits
        /// for the night to become active. Now that a night only starts when the player presses
        /// «Начать смену», an automated capture run would wait forever - so it passes this flag.
        /// </remarks>
        public const string SkipDayArg = "--skipday";

        private static GameObject _root;

        /// <summary>
        /// Entry point Unity calls automatically once the first scene has loaded.
        /// </summary>
        /// <remarks>
        /// The null check on <see cref="_root"/> is not redundant with the
        /// <see cref="GameObject.Find"/> guard: with Enter Play Mode domain reload disabled the
        /// static survives a play-session restart, and Unity's null comparison correctly reports a
        /// destroyed object as null.
        /// </remarks>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapOnLoad()
        {
            if (_root != null)
            {
                return;
            }

            // A scene that already contains a hand-assembled game (a later story may add one) wins.
            if (GameObject.Find(RootObjectName) != null)
            {
                return;
            }

            _root = Build();
        }

        /// <summary>
        /// Restarts the campaign from scratch - Story 006 acceptance criterion 5's «Заново». Tears the
        /// whole object graph down and builds a new one.
        /// </summary>
        /// <remarks>
        /// <para><b>Why a rebuild and not a reset.</b> Everything a restart has to undo is owned by
        /// objects this class created: credits and Core integrity live in the
        /// <see cref="NetworkSimulation"/>, the topology in its <see cref="NetworkGraph"/>, the night
        /// number in <see cref="GameRunner"/>, the story cursor in <see cref="StoryLogDirector"/>, the
        /// log lines in <see cref="TerminalView"/>. Dropping the graph and re-running
        /// <see cref="Build"/> resets all of them by construction, and - unlike a hand-written
        /// <c>Reset()</c> on each - it cannot fall behind when a later story adds state. It is also why
        /// this is here and not in a view: no view may rebuild the game.</para>
        ///
        /// <para><b>The old root dies at the end of the frame</b>, because
        /// <see cref="Object.Destroy(Object)"/> is deferred and this is called from inside a UI Toolkit
        /// click callback - destroying the panel mid-dispatch would be re-entrant. Its camera and audio
        /// listener are switched off at once so they do not compete with the new ones (Unity warns about
        /// two active <see cref="AudioListener"/>s), which leaves only the old, now-inert UI panel
        /// overlapping the new title screen for a single frame.</para>
        /// </remarks>
        /// <returns>The root GameObject of the fresh hierarchy.</returns>
        public static GameObject Restart()
        {
            if (_root != null)
            {
                Transform oldCamera = _root.transform.Find("MainCamera");
                if (oldCamera != null)
                {
                    oldCamera.gameObject.SetActive(false);
                }

                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _root = Build();
            return _root;
        }

        /// <summary>
        /// Builds the full runtime object graph and opens the title screen. Public so an editor tool or
        /// a later story can raise the game explicitly.
        /// </summary>
        /// <returns>The root GameObject of the created hierarchy.</returns>
        public static GameObject Build()
        {
            // First, and before any view exists: the player's window-size choice from a previous
            // session. Doing it here rather than in a view means the very first frame is drawn at the
            // right size, so no layout is ever measured against a resolution the player did not pick.
            // It is a no-op when they have never opened the settings screen, and when the window
            // already matches - which is what makes it safe on the Restart() path too.
            DisplaySettings.ApplySaved();

            var config = new ViewConfig();

            GameData data = NightContent.CreateGameData();
            var simulation = new NetworkSimulation(
                data,
                new SystemRandomSource(NightContent.SimulationSeed),
                NightContent.StartingCredits);

            NightContent.BuildStartingNetwork(simulation);

            var layout = new MapLayout(data.GridWidth, data.GridHeight, config.CellSize);

            var root = new GameObject(RootObjectName);
            GameRunner runner = root.AddComponent<GameRunner>();

            MapCamera mapCamera = CreateCamera(root.transform, config, layout);
            NetworkMapView mapView = CreateMap(root.transform, simulation, config, layout);

            // The HUD and the day panel read the phase, the day number and the time scale off the
            // runner, so the runner is initialised before any view is built. Nights come from a
            // factory, not a fixed instance, so day 2 can lead into night 2 with no help from here.
            runner.Initialize(simulation, config, NightContent.CreateNight);

            DayBuildController buildController = root.AddComponent<DayBuildController>();
            buildController.Initialize(simulation, runner, mapView, config, layout, mapCamera.Camera);

            // Story 004: the terminal's rules live in Core, so the processor is built here next to the
            // simulation it acts on and injected into the view - the view owns no command logic.
            var terminal = new TerminalCommandProcessor(simulation);

            // Story 006: on the normal path the main menu is created and owns the first BeginDay call,
            // so nothing is entered here at all. Under --skipday the menu is still built - the player
            // may want the settings screen - but it is not shown.
            bool skipDay = StartupArgs.HasFlag(SkipDayArg);
            CreateUi(root.transform, simulation, runner, buildController, terminal, config, !skipDay);

            // Last: every view is now subscribed, so no phase or packet event can be missed.
            // Story 003 opens in the day phase - the night is the player's decision now. The
            // --skipday escape hatch preserves the Story 002 behaviour (straight into the night) that
            // the QaScreenshot `--atsim` capture path depends on, and skips the menu with it:
            // an automated capture cannot press a button.
            if (skipDay)
            {
                runner.StartNight();
            }

            return root;
        }

        private static MapCamera CreateCamera(Transform parent, ViewConfig config, MapLayout layout)
        {
            var cameraGo = new GameObject("MainCamera");
            cameraGo.transform.SetParent(parent, false);
            cameraGo.tag = "MainCamera";

            cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();

            MapCamera mapCamera = cameraGo.AddComponent<MapCamera>();
            mapCamera.Initialize(config, layout);
            return mapCamera;
        }

        private static NetworkMapView CreateMap(Transform parent, NetworkSimulation simulation, ViewConfig config, MapLayout layout)
        {
            var mapGo = new GameObject("NetworkMap");
            mapGo.transform.SetParent(parent, false);

            NetworkMapView mapView = mapGo.AddComponent<NetworkMapView>();
            mapView.Initialize(simulation, config, layout);
            return mapView;
        }

        /// <summary>
        /// Creates the UI Toolkit host. The GameObject is built inactive on purpose:
        /// <see cref="UIDocument"/> creates its panel in <c>OnEnable</c> and needs its
        /// <c>panelSettings</c> already assigned, so <see cref="UiRoot.Configure"/> runs first and the
        /// object is activated afterwards.
        /// </summary>
        /// <param name="showMenu">
        /// False under <c>--skipday</c>: the menu is built but not shown, and the caller starts the
        /// night itself. True on the normal path, where the menu is the game's first frame and its
        /// first entry owns the first <see cref="GameRunner.BeginDay"/>.
        /// </param>
        private static void CreateUi(
            Transform parent,
            NetworkSimulation simulation,
            GameRunner runner,
            DayBuildController buildController,
            TerminalCommandProcessor terminal,
            ViewConfig config,
            bool showMenu)
        {
            var uiGo = new GameObject("Ui");
            uiGo.transform.SetParent(parent, false);
            uiGo.SetActive(false);

            UiRoot uiRoot = uiGo.AddComponent<UiRoot>();
            uiRoot.Configure(config);
            uiGo.SetActive(true);

            // The pointer floor: one full-screen *pickable* element, underneath every other layer and
            // owning nothing. It exists because a runtime UI Toolkit panel dispatches a pointer event
            // only when its hit-test finds something; over the bare map - where every layer below
            // ignores picking and every panel is display: none - the hit-test finds nothing, no event
            // is dispatched, and the panel's "element under the pointer" is never updated. The
            // PointerLeaveEvent that DayShopView needs to clear DayBuildController.PointerOverUi
            // therefore never arrives, the flag sticks true after the player's first visit to the shop
            // panel, and MapInputBlocked silently discards every map click from then on. That is
            // Story 003's build phase dead in a built player. The floor guarantees the hit-test always
            // succeeds, so enter and leave are always paired; DayShopView also clears the flag on the
            // floor's own PointerEnterEvent, which makes the invariant self-healing rather than
            // event-order dependent. It must be created first, so every real layer sits above it.
            VisualElement pointerFloorLayer = uiRoot.CreateLayer("pointer-floor-layer", false);

            // Every layer below ignores picking, and none of them may stop doing so. A layer is a
            // full-screen container, so a *pickable* one is picked in preference to everything in
            // every layer beneath it, for the whole screen, for the whole game - the day panel's
            // buttons included. A PickingMode.Ignore element is excluded from hit-testing itself but
            // its children are not, so the panel inside each layer still takes its own clicks, and
            // each panel is display: none while its screen is down, which takes it out of hit-testing
            // entirely. Layer order still decides what draws and picks above what: the report covers
            // the day panel, the terminal covers the report, Story 006's letters cover the shop they
            // interrupt, and the menu stack covers everything - it can be opened from inside the day,
            // over a letter, so it has to be the top of the pile.
            VisualElement hudLayer = uiRoot.CreateLayer("hud-layer", true);
            VisualElement dayLayer = uiRoot.CreateLayer("day-layer", true);
            VisualElement reportLayer = uiRoot.CreateLayer("report-layer", true);
            VisualElement terminalLayer = uiRoot.CreateLayer("terminal-layer", true);
            VisualElement letterLayer = uiRoot.CreateLayer("letter-layer", true);

            // Three layers and not one for the menu stack, so the UI Toolkit debugger shows which
            // screen is up at a glance. They are mutually exclusive in practice - MenuController shows
            // exactly one of the three - so their relative order never matters; that they are all above
            // the game's own layers does.
            VisualElement menuLayer = uiRoot.CreateLayer("menu-layer", true);
            VisualElement settingsLayer = uiRoot.CreateLayer("settings-layer", true);
            VisualElement helpLayer = uiRoot.CreateLayer("help-layer", true);

            uiGo.AddComponent<NightHudView>().Initialize(simulation, runner, config, hudLayer);
            uiGo.AddComponent<DayShopView>().Initialize(simulation, runner, buildController, config, dayLayer, pointerFloorLayer);

            // The report's outgoing actions are "open the next day" and "restart the campaign" - the
            // runner's job and this class's job respectively. Neither is the view's.
            uiGo.AddComponent<NightReportView>().Initialize(simulation, config, reportLayer, runner.BeginDay, RestartCampaign);

            // The terminal shows itself only during GamePhase.Night, off the runner's phase event -
            // like every other view here, it is told nothing by its siblings.
            TerminalView terminalView = uiGo.AddComponent<TerminalView>();
            terminalView.Initialize(terminal, runner, config, terminalLayer);

            // Story 006 criterion 3: the night's authored lines, on the simulation clock, into the log
            // the terminal already owns. Initialised before the first night starts, like every view.
            uiGo.AddComponent<StoryLogDirector>().Initialize(runner, simulation, terminalView);

            // Criterion 2: the letters open every day, including day 1. The view finds its own day
            // number on GameRunner.OnPhaseChanged, so nothing has to remember to show it.
            uiGo.AddComponent<LetterView>().Initialize(runner, buildController, config, letterLayer);

            // Criterion 1, and the last thing built: the main menu is up from the first frame and its
            // first entry is what raises day 1. Nothing else calls BeginDay on the normal path.
            //
            // The controller is created before the three views because the views are handed its
            // navigation actions at construction time, and it is initialised after them because it
            // needs the views to show them. Taking a method-group delegate off a component that has not
            // been Initialize()d yet is safe - the delegate only captures the instance, and nothing is
            // invoked until the player clicks.
            MenuController menuController = uiGo.AddComponent<MenuController>();

            MainMenuView menuView = uiGo.AddComponent<MainMenuView>();
            menuView.Initialize(
                config,
                menuLayer,
                menuController.PrimaryAction,
                menuController.SettingsAction,
                menuController.HelpAction,
                menuController.QuitAction);

            SettingsView settingsView = uiGo.AddComponent<SettingsView>();
            settingsView.Initialize(config, settingsLayer, menuController.BackAction);

            HelpView helpView = uiGo.AddComponent<HelpView>();
            helpView.Initialize(config, helpLayer, menuController.BackAction);

            // The build controller goes in so the menu can raise DayBuildController.ModalOpen: the map
            // is picked from legacy Input, which UI Toolkit hit-testing never sees, so a full-screen
            // pickable panel on its own would not stop the player building underneath the menu.
            menuController.Initialize(
                runner,
                buildController,
                menuView,
                settingsView,
                helpView,
                runner.BeginDay,
                showMenu);
        }

        /// <summary>
        /// Adapter for <see cref="NightReportView"/>'s «Заново» action: a method group, so the view is
        /// handed a plain <see cref="System.Action"/> and never sees this class.
        /// </summary>
        private static void RestartCampaign() => Restart();
    }
}
