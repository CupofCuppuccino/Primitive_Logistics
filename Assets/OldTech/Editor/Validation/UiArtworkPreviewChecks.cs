using System;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace OldTech.Editor
{
    public static class UiArtworkPreviewChecks
    {
        public static void RunMenuOnly()
        {
            SessionState.SetBool(UiArtworkPreview.MenuOnlyKey, true);
            Run();
        }

        [MenuItem("Old Tech/Preview painted UI in game")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            var current = EditorSceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            SessionState.SetBool(UiArtworkPreview.ActiveKey, true);
            EditorApplication.isPlaying = true;
        }
    }
}
