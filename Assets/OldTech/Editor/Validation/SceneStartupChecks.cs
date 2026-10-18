using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldTech.Editor
{
    // Test the editor's imported MonoScripts as well as the separately compiled player.
    [InitializeOnLoad]
    public static class SceneStartupChecks
    {
        private const string ActiveKey = "OldTech.SceneStartupChecks.Active";
        private static string failure;
        private static double deadline;
        private static float startX;
        private static int stage;

        static SceneStartupChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            Application.logMessageReceived += TrackErrors;
        }

        [MenuItem("Old Tech/Validate editor scene startup")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/OldTech/Runtime" }))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script.GetClass() == null) throw new InvalidOperationException("Unresolved MonoScript: " + script.name);
            }
            var current = EditorSceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty) EditorSceneManager.SaveScene(current);
            EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath);
            failure = null;
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                deadline = EditorApplication.timeSinceStartup + 20;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                GameProgress.UseEditorSmokeSave = false;
                SessionState.SetBool(ActiveKey, false);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/EditorSceneStartup.txt", failure ?? "PASS: All runtime MonoScript classes resolve. Menu opens. New Delivery initializes session, settings, camera and grip. Three rolling logs start. Cargo advances without runtime exceptions.");
                EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            }
        }

        private static void TrackErrors(string message, string trace, LogType type)
        {
            if (SessionState.GetBool(ActiveKey, false) &&
                (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                failure = "FAIL: " + message + "\n" + trace;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (failure != null || (deadline > 0 && EditorApplication.timeSinceStartup > deadline))
            {
                if (failure == null) failure = "FAIL: Editor gameplay initialization timed out.";
                EditorApplication.isPlaying = false;
                return;
            }
            if (stage == 0)
            {
                if (UnityEngine.Object.FindObjectOfType<MainMenuController>() == null) return;
                // Use a test-only save so validation never replaces the player's progress.
                GameProgress.UseEditorSmokeSave = true;
                GameScenes.StartNewDelivery();
                stage = 1;
            }
            HaulSession session = UnityEngine.Object.FindObjectOfType<HaulSession>();
            if (session == null || !session.Ready) return;
            if (stage == 1)
            {
                if (session.Settings == null || session.grabber == null || Camera.main == null || session.Checkpoints.Logs.Length != 3)
                { failure = "FAIL: Delivery initialization references are incomplete."; return; }
                startX = session.cargo.Body.position.x;
                session.cargo.externalControl = true;
                session.cargo.externalPush = true;
                stage = 2;
            }
            else if (stage == 2 && session.cargo.Body.position.x > startX + 0.6f)
            {
                session.cargo.externalPush = false;
                GameProgress.ClearSmokeSave();
                EditorApplication.isPlaying = false;
            }
        }
    }
}
