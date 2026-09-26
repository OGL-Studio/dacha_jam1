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
    /// first; then the views subscribe to simulation events; then
    /// <see cref="GameRunner.StartNight"/> runs last. Starting the night before the views existed
    /// would drop the first packet events on the floor. <see cref="GameRunner.Initialize"/> likewise
    /// precedes <see cref="NightHudView.Initialize"/>, which reads the night number, the time
    /// remaining and the time scale off the runner.</para>
    ///
    /// <para><b>Dependency injection, no singletons.</b> Every component receives what it needs
    /// through an <c>Initialize</c> call, so each is testable in isolation and nothing reaches for a
    /// global.</para>
    /// </remarks>
    public static class GameBootstrap
    {
        /// <summary>Name of the root GameObject the bootstrap creates.</summary>
        public const string RootObjectName = "NightShift";

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
            NightData night = NightContent.CreateNight1(new SystemRandomSource(NightContent.ScheduleSeed));

            var layout = new MapLayout(data.GridWidth, data.GridHeight, config.CellSize);

            var root = new GameObject(RootObjectName);
            GameRunner runner = root.AddComponent<GameRunner>();

            CreateCamera(root.transform, config, layout);
            CreateMap(root.transform, simulation, config, layout);

            // The HUD reads night number, time remaining and time scale from the runner, so the
            // runner must know about the night before the HUD is built.
            runner.Initialize(simulation, night, config);

            CreateUi(root.transform, simulation, runner, config);

            // Last: every view is now subscribed, so no packet event can be missed.
            runner.StartNight();

            return root;
        }

        private static void CreateCamera(Transform parent, ViewConfig config, MapLayout layout)
        {
            var cameraGo = new GameObject("MainCamera");
            cameraGo.transform.SetParent(parent, false);
            cameraGo.tag = "MainCamera";

            cameraGo.AddComponent<Camera>();
            cameraGo.AddComponent<AudioListener>();
            cameraGo.AddComponent<MapCamera>().Initialize(config, layout);
        }

        private static void CreateMap(Transform parent, NetworkSimulation simulation, ViewConfig config, MapLayout layout)
        {
            var mapGo = new GameObject("NetworkMap");
            mapGo.transform.SetParent(parent, false);
            mapGo.AddComponent<NetworkMapView>().Initialize(simulation, config, layout);
        }

        /// <summary>
        /// Creates the UI Toolkit host. The GameObject is built inactive on purpose:
        /// <see cref="UIDocument"/> creates its panel in <c>OnEnable</c> and needs its
        /// <c>panelSettings</c> already assigned, so <see cref="UiRoot.Configure"/> runs first and the
        /// object is activated afterwards.
        /// </summary>
        private static void CreateUi(Transform parent, NetworkSimulation simulation, GameRunner runner, ViewConfig config)
        {
            var uiGo = new GameObject("Ui");
            uiGo.transform.SetParent(parent, false);
            uiGo.SetActive(false);

            UiRoot uiRoot = uiGo.AddComponent<UiRoot>();
            uiRoot.Configure(config);
            uiGo.SetActive(true);

            // The HUD ignores picking so it cannot swallow Story 003's map clicks.
            VisualElement hudLayer = uiRoot.CreateLayer("hud-layer", true);
            VisualElement reportLayer = uiRoot.CreateLayer("report-layer", false);

            uiGo.AddComponent<NightHudView>().Initialize(simulation, runner, config, hudLayer);
            uiGo.AddComponent<NightReportView>().Initialize(simulation, config, reportLayer);
        }
    }
}
