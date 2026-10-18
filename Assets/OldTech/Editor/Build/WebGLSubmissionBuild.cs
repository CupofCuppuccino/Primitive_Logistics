using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    [InitializeOnLoad]
    public static class WebGLSubmissionBuild
    {
        private const string Pending = "OldTech.WebGL.BuildPending";
        private const string Output = "Builds/WebGL";
        private const string Report = "Logs/WebGL/Build.txt";

        static WebGLSubmissionBuild() { EditorApplication.update += ContinueBuild; }

        [MenuItem("Old Tech/Build itch.io WebGL submission")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                throw new InvalidOperationException("Install international Unity 2022.3.62f3 WebGL Build Support and restart the editor.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
            Directory.CreateDirectory("Logs/WebGL");
            File.WriteAllText(Report, "Building WebGL: " + DateTime.Now.ToString("s"));
            SessionState.SetBool(Pending, true);
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL &&
                !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                SessionState.SetBool(Pending, false);
                throw new InvalidOperationException("Could not switch to WebGL.");
            }
        }

        private static void ContinueBuild()
        {
            if (!SessionState.GetBool(Pending, false) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL) return;
            SessionState.SetBool(Pending, false);
            try { Build(); }
            catch (Exception e) { File.WriteAllText(Report, "FAIL: " + e); Debug.LogException(e); }
        }

        private static void Build()
        {
            ProjectSetup.ConfigureBuildScenes();
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.initialMemorySize = 256;
            PlayerSettings.WebGL.maximumMemorySize = 2048;
            PlayerSettings.WebGL.template = "PROJECT:PrimitiveLogistics";
            PlayerSettings.defaultWebScreenWidth = 1280;
            PlayerSettings.defaultWebScreenHeight = 720;
            Directory.CreateDirectory(Output);
            BuildReport result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameSceneBuilder.MenuScenePath, GameSceneBuilder.ScenePath },
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.CleanBuildCache
            });
            if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build: " + result.summary.result);
            if (!File.Exists(Output + "/index.html")) throw new InvalidOperationException("WebGL entry point missing.");
            File.WriteAllText(Report, "PASS: Unity " + Application.unityVersion + " release WebGL build. Gzip with decompression fallback; responsive canvas; 256 MB initial memory; " +
                result.summary.totalSize + " bytes. " + DateTime.Now.ToString("s"));
            AssetDatabase.SaveAssets();
        }
    }
}
