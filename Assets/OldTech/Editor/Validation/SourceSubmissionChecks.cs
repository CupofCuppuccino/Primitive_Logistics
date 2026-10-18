using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class SourceSubmissionChecks
    {
        [MenuItem("Old Tech/Export source asset manifest")]
        public static void ExportManifest()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            string[] roots = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                !AssetDatabase.IsValidFolder(p) && (p.Contains("/Resources/") || p.EndsWith(".cs", StringComparison.Ordinal) ||
                p.EndsWith(".prefab", StringComparison.Ordinal) || p.StartsWith("Assets/WebGLTemplates/", StringComparison.Ordinal)))
                .Concat(new[] { GameSceneBuilder.MenuScenePath, GameSceneBuilder.ScenePath, GameSceneBuilder.SettingsPath }).ToArray();
            string[] assets = AssetDatabase.GetDependencies(roots, true).Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                !AssetDatabase.IsValidFolder(p)).Distinct().OrderBy(p => p).ToArray();
            Directory.CreateDirectory("Logs/SourceSubmission");
            File.WriteAllLines("Logs/SourceSubmission/Assets.txt", assets);
        }

        // Command-line check for a clean checkout, with no dependency on local reference art or caches.
        public static void Validate()
        {
            if (Application.unityVersion != "2022.3.62f3") throw new InvalidOperationException("Use international Unity 2022.3.62f3.");
            if (EditorBuildSettings.scenes.Count(s => s.enabled) != 2) throw new InvalidOperationException("Expected menu and delivery build scenes.");
            foreach (string path in new[] { GameSceneBuilder.MenuScenePath, GameSceneBuilder.ScenePath })
            {
                Scene scene = EditorSceneManager.OpenScene(path);
                foreach (GameObject root in scene.GetRootGameObjects())
                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                            throw new InvalidOperationException("Missing script: " + path + "/" + child.name);
                if (path == GameSceneBuilder.MenuScenePath && UnityEngine.Object.FindObjectOfType<MainMenuController>() == null)
                    throw new InvalidOperationException("Main menu controller missing.");
                if (path == GameSceneBuilder.ScenePath)
                {
                    HaulSession session = UnityEngine.Object.FindObjectOfType<HaulSession>();
                    if (session == null || session.defaults == null || session.contactMaterial == null || session.cargo == null || session.grabber == null)
                        throw new InvalidOperationException("Delivery references are incomplete.");
                    if (UnityEngine.Object.FindObjectOfType<RobotCargo>() == null || UnityEngine.Object.FindObjectsOfType<CheckpointMarker>().Count(marker => marker.checkpointIndex > 0) != 9)
                        throw new InvalidOperationException("Robot or checkpoints missing.");
                }
            }
            GuiArtwork gui = Resources.Load<GuiArtwork>("GuiArtwork");
            SoundLibrary sounds = Resources.Load<SoundLibrary>("SoundLibrary");
            if (gui == null || !gui.Complete || sounds == null) throw new InvalidOperationException("UI or sound resource library missing.");
            foreach (Cue cue in Enum.GetValues(typeof(Cue)))
                if (sounds.Get(cue)?.clip == null) throw new InvalidOperationException("Missing sound: " + cue);
            EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath);
            Debug.Log("OLDTECH_SOURCE_CHECK_PASS: Clean source checkout compiles; both scenes load; gameplay, nine checkpoints, UI and all sound bindings are intact.");
        }
    }
}
