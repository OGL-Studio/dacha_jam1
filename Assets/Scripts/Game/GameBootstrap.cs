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
        /// Builds the full runtime object graph and starts night 1. Public so an editor tool or a
        /// later story can raise the game explicitly.
        /// </summary>
        /// <returns>The root GameObject of the created hierarchy.</returns>
        public static GameObject Build()
        {
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

            CreateUi(root.transform, simulation, runner, buildController, config);

            // Last: every view is now subscribed, so no phase or packet event can be missed.
            // Story 003 opens in the day phase - the night is the player's decision now. The
            // --skipday escape hatch preserves the Story 002 behaviour (straight into the night) that
            // the QaScreenshot `--atsim` capture path depends on.
            if (StartupArgs.HasFlag(SkipDayArg))
            {
                runner.StartNight();
            }
            else
            {
                runner.BeginDay();
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
        private static void CreateUi(
            Transform parent,
            NetworkSimulation simulation,
            GameRunner runner,
            DayBuildController buildController,
            ViewConfig config)
        {
            var uiGo = new GameObject("Ui");
            uiGo.transform.SetParent(parent, false);
            uiGo.SetActive(false);

            UiRoot uiRoot = uiGo.AddComponent<UiRoot>();
            uiRoot.Configure(config);
            uiGo.SetActive(true);

            // The HUD ignores picking; the day and report layers do not, because both own real
            // buttons. A pickable full-screen layer is safe here: map interaction is read from the
            // legacy Input class, which UI Toolkit's hit-testing does not feed, so nothing on the map
            // is "swallowed" by the layer. What keeps a click on the day panel from also placing a
            // node behind it is DayShopView reporting pointer-over to DayBuildController. The report
            // layer is created last, so it is picked above the day layer while both are up.
            VisualElement hudLayer = uiRoot.CreateLayer("hud-layer", true);
            VisualElement dayLayer = uiRoot.CreateLayer("day-layer", false);
            VisualElement reportLayer = uiRoot.CreateLayer("report-layer", false);

            uiGo.AddComponent<NightHudView>().Initialize(simulation, runner, config, hudLayer);
            uiGo.AddComponent<DayShopView>().Initialize(simulation, runner, buildController, config, dayLayer);

            // The report's only outgoing action is "open the next day", which is the runner's job.
            uiGo.AddComponent<NightReportView>().Initialize(simulation, config, reportLayer, runner.BeginDay);
        }
    }
}
