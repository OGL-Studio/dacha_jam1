using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NightShift.Game.EditorTools
{
    /// <summary>
    /// Batch-mode Windows player build, for QA evidence capture from an automated session.
    /// </summary>
    /// <remarks>
    /// <para><b>Why a script rather than a <c>-buildTarget</c> command line.</b> The Windows target
    /// identifier for the CLI is still <c>[TO BE CONFIGURED]</c> in <c>project.yaml</c> and is not
    /// documented anywhere in <c>docs/engine-reference/unity/</c>, so writing one from memory is
    /// exactly the guess that reference directory exists to prevent. Naming
    /// <see cref="BuildTarget.StandaloneWindows64"/> in C# instead means the editor's own compiler
    /// validates the choice: a wrong identifier fails loudly at compile time rather than quietly
    /// building the wrong thing.</para>
    ///
    /// <para><b>The scene list is explicit</b> rather than read from
    /// <c>ProjectSettings/EditorBuildSettings.asset</c>, whose entry for <c>Main.unity</c> currently
    /// carries a placeholder GUID.</para>
    /// </remarks>
    public static class PlayerBuild
    {
        /// <summary>Scene the player boots into.</summary>
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        /// <summary>Output executable, relative to the project root.</summary>
        public const string OutputPath = "Builds/Windows/NightShift.exe";

        /// <summary>
        /// Builds a development Windows 64-bit player. Invoked via
        /// <c>-executeMethod NightShift.Game.EditorTools.PlayerBuild.BuildWindows</c>.
        /// </summary>
        /// <remarks>
        /// Exits with a non-zero code on failure so the calling shell can tell the difference;
        /// <c>-quit</c> alone would report success regardless.
        /// </remarks>
        public static void BuildWindows()
        {
            EnsureMainScene();

            string outputDirectory = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,

                // Development gives us a player log, which is where QaScreenshot's confirmation
                // lines land - without it a missing screenshot has no explanation.
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log(
                "[PlayerBuild] result=" + summary.result
                + " errors=" + summary.totalErrors
                + " size=" + summary.totalSize
                + " output=" + OutputPath);

            if (summary.result != BuildResult.Succeeded)
            {
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Guarantees <see cref="MainScenePath"/> exists and is a scene Unity itself serialised.
        /// </summary>
        /// <remarks>
        /// The hand-authored 126-byte stub committed with Story 002 was never imported, so its
        /// validity was unknown. Re-saving through <see cref="EditorSceneManager"/> replaces it with
        /// a file the engine wrote, which is the only way to be sure it opens. <see
        /// cref="NewSceneMode.Single"/> is deliberate: the additive path is what failed at editor
        /// load with "Cannot create a new scene additively with an untitled scene unsaved".
        /// </remarks>
        private static void EnsureMainScene()
        {
            string directory = Path.GetDirectoryName(MainScenePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // GameBootstrap raises camera, map and HUD from code, so an empty scene is correct -
            // no default camera or light is wanted here.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool saved = EditorSceneManager.SaveScene(scene, MainScenePath);

            Debug.Log("[PlayerBuild] scene " + (saved ? "saved " : "FAILED to save ") + MainScenePath);

            AssetDatabase.Refresh();
        }
    }
}
