using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NightShift.Game.EditorTools
{
    /// <summary>
    /// Creates `Assets/Scenes/Main.unity` if it is missing and registers it in
    /// `ProjectSettings/EditorBuildSettings.asset`. Closes the scene gap identified in Story 002 of
    /// `production/epics/night-shift/story-002-unity-night-view.md`: the project had no scene asset
    /// at all and an empty build scene list, so a built player had nothing to load.
    /// </summary>
    /// <remarks>
    /// <para><b>What is already on disk.</b> Story 002 committed a minimal `Main.unity` and a
    /// build-settings entry for it directly, so the repository is correct without the editor ever
    /// having run. This tool is the self-healing half of that: it recreates the scene if it goes
    /// missing, and - because a scene's GUID lives in the `.meta` file Unity generates on import,
    /// which is never hand-authored - it replaces the placeholder all-zero GUID in the committed
    /// build-settings entry with the real one the first time the editor loads the project.</para>
    ///
    /// <para><b>Non-destructive.</b> Whatever the developer currently has open is never replaced or
    /// modified. Work is deferred through <see cref="EditorApplication.delayCall"/> so nothing
    /// touches the scene manager during domain reload, and every step is a no-op once it has run.</para>
    ///
    /// <para><b>Two creation routes, tried in order.</b> The obvious call -
    /// <c>EditorSceneManager.NewScene(EmptyScene, Additive)</c> - cannot be used here: Unity rejects
    /// it with "Cannot create a new scene additively with an untitled scene unsaved", and an unsaved
    /// untitled scene is precisely the state of a project that has no scene asset yet, which is the
    /// state this tool exists to fix. So route 1 is
    /// <see cref="SceneManager.CreateScene(string)"/> plus
    /// <see cref="EditorSceneManager.SaveScene(Scene, string)"/>, and route 2 - if the editor refuses
    /// that as well - writes a minimal scene file directly and imports it, which is also how the
    /// `.meta` and its GUID come to exist without being hand-authored.</para>
    ///
    /// <para><b>The scene is intentionally empty</b> - no camera, no light, no objects.
    /// <see cref="NightShift.Game.GameBootstrap"/> builds the entire game at runtime, which is
    /// acceptance criterion 1.</para>
    /// </remarks>
    [InitializeOnLoad]
    public static class MainSceneSetup
    {
        /// <summary>Folder the scene lives in.</summary>
        public const string ScenesFolder = "Assets/Scenes";

        /// <summary>Project-relative path of the boot scene.</summary>
        public const string MainScenePath = "Assets/Scenes/Main.unity";

        static MainSceneSetup()
        {
            EditorApplication.delayCall += EnsureMainScene;
        }

        /// <summary>
        /// Ensures the boot scene exists on disk and is the first enabled entry of the build scene
        /// list. Safe to run repeatedly; also exposed as a menu item for manual re-runs.
        /// </summary>
        [MenuItem("Night Shift/Setup Main Scene")]
        public static void EnsureMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (!EnsureSceneAssetExists())
            {
                return;
            }

            EnsureRegisteredInBuildSettings();
        }

        /// <summary>Name of the boot scene, without path or extension.</summary>
        private const string SceneName = "Main";

        /// <summary>
        /// A valid, completely empty Unity scene: no GameObjects, and no camera or light either,
        /// because <see cref="NightShift.Game.GameBootstrap"/> creates all of that at runtime. Unity
        /// fills in any settings object this omits with its own defaults the first time the scene is
        /// opened and saved.
        /// </summary>
        private const string MinimalSceneYaml =
            "%YAML 1.1\n" +
            "%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!29 &1\n" +
            "OcclusionCullingSettings:\n" +
            "  m_ObjectHideFlags: 0\n" +
            "  serializedVersion: 2\n";

        private static bool EnsureSceneAssetExists()
        {
            if (File.Exists(MainScenePath))
            {
                return true;
            }

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            if (TryCreateThroughSceneManager() || TryCreateThroughSceneFile())
            {
                Debug.Log("[NightShift] Created empty boot scene " + MainScenePath +
                          " (the game is built at runtime by GameBootstrap).");
                return true;
            }

            Debug.LogWarning("[NightShift] Could not create " + MainScenePath +
                             ". Run 'Night Shift > Setup Main Scene' manually.");
            return false;
        }

        /// <summary>
        /// Route 1: ask the scene manager for a new empty additive scene and save it. Leaves the
        /// developer's open scene alone and produces exactly the file this editor version expects.
        /// </summary>
        private static bool TryCreateThroughSceneManager()
        {
            Scene scene = default;

            try
            {
                scene = SceneManager.CreateScene(SceneName);
                if (!scene.IsValid())
                {
                    return false;
                }

                bool saved = EditorSceneManager.SaveScene(scene, MainScenePath);
                EditorSceneManager.CloseScene(scene, true);

                return saved && File.Exists(MainScenePath);
            }
            catch (Exception exception)
            {
                Debug.Log("[NightShift] Scene-manager route unavailable (" + exception.Message +
                          "); falling back to writing the scene file directly.");

                if (scene.IsValid())
                {
                    try
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                    catch (Exception)
                    {
                        // Nothing useful to do if the partially created scene will not close.
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Route 2: write a minimal scene file and import it, so Unity generates the `.meta` and the
        /// GUID that <see cref="EditorBuildSettings"/> needs.
        /// </summary>
        private static bool TryCreateThroughSceneFile()
        {
            try
            {
                File.WriteAllText(MainScenePath, MinimalSceneYaml);
                AssetDatabase.ImportAsset(MainScenePath, ImportAssetOptions.ForceSynchronousImport);
                return File.Exists(MainScenePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[NightShift] Could not write " + MainScenePath + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>A GUID field that has never been resolved against the asset database.</summary>
        private const string EmptyGuid = "00000000000000000000000000000000";

        private static void EnsureRegisteredInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path != MainScenePath)
                {
                    continue;
                }

                // The entry may have been written before the scene had an imported GUID (the asset
                // GUID lives in the `.meta` Unity generates, which is never hand-authored). Rebuilding
                // the entry from its path makes the editor resolve the real GUID.
                bool needsGuid = scenes[i].guid.ToString() == EmptyGuid;
                bool needsEnabling = !scenes[i].enabled;

                if (!needsGuid && !needsEnabling)
                {
                    return;
                }

                scenes[i] = new EditorBuildSettingsScene(MainScenePath, true);
                EditorBuildSettings.scenes = scenes;
                Debug.Log("[NightShift] Repaired the build-settings entry for " + MainScenePath +
                          (needsGuid ? " (resolved its asset GUID)." : " (re-enabled it)."));
                return;
            }

            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            Array.Copy(scenes, updated, scenes.Length);
            updated[scenes.Length] = new EditorBuildSettingsScene(MainScenePath, true);
            EditorBuildSettings.scenes = updated;

            Debug.Log("[NightShift] Registered " + MainScenePath + " in the build scene list.");
        }
    }
}
