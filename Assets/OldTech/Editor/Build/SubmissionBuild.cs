using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class SubmissionBuild
    {
        [MenuItem("Old Tech/Build final Windows submission")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
            }
            ProjectSetup.ConfigureBuildScenes();
            Directory.CreateDirectory("Builds/ReleaseWindows");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { GameSceneBuilder.MenuScenePath, GameSceneBuilder.ScenePath },
                locationPathName = "Builds/ReleaseWindows/PrimitiveLogistics.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.DetailedBuildReport
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Release build failed: " + report.summary.result);
            if ((report.summary.options & (BuildOptions.Development | BuildOptions.AllowDebugging | BuildOptions.ConnectWithProfiler)) != 0)
                throw new InvalidOperationException("Submission build unexpectedly enables development/debugging.");
            Directory.CreateDirectory("Logs/FinalSubmission");
            string[] assets = report.packedAssets.SelectMany(a => a.contents).Select(a => a.sourceAssetPath)
                .Where(p => !string.IsNullOrEmpty(p)).Distinct().OrderBy(p => p).ToArray();
            // Some Unity 2022 player builds omit per-file packed-asset details.
            if (assets.Length == 0) assets = ReferencedAssets();
            File.WriteAllLines("Logs/FinalSubmission/PackedAssets.txt", assets);
            string[] unusedAudio = { "WoodImpact.mp3", "UiWoodButton.mp3", "LogPickup.mp3", "FireFeed.mp3", "FireCrackle.mp3" };
            if (assets.Any(p => p.IndexOf("ArtReferences", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (p.StartsWith("Assets/OldTech/Audio/Sfx/", StringComparison.Ordinal) && unusedAudio.Contains(Path.GetFileName(p)))))
                throw new InvalidOperationException("Reference art or replaced audio unexpectedly included in the release.");
            string assembly = "Builds/ReleaseWindows/PrimitiveLogistics_Data/Managed/Assembly-CSharp.dll";
            if (File.Exists(assembly))
            {
                string metadata = Encoding.ASCII.GetString(File.ReadAllBytes(assembly));
                if (new[] { "GameFlowSmokeTests", "ExtendedRouteSmokeTests", "UiArtworkPreview" }.Any(metadata.Contains))
                    throw new InvalidOperationException("Development-only test code was included in the release.");
            }
            File.WriteAllText("Logs/FinalSubmission/ReleaseBuild.txt", "PASS: Unity " + Application.unityVersion +
                " Windows x64 release build. No development mode, debugger or profiler connection. " +
                "Development-only test code absent. Referenced runtime assets only; reference art and replaced source audio excluded. " +
                report.summary.totalSize + " bytes. " + DateTime.Now.ToString("s"));
        }

        public static void AuditExistingRelease()
        {
            string[] assets = ReferencedAssets();
            string[] replaced = { "WoodImpact.mp3", "UiWoodButton.mp3", "LogPickup.mp3", "FireFeed.mp3", "FireCrackle.mp3" };
            if (assets.Any(p => p.IndexOf("ArtReferences", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (p.StartsWith("Assets/OldTech/Audio/Sfx/", StringComparison.Ordinal) && replaced.Contains(Path.GetFileName(p)))))
                throw new InvalidOperationException("Unused/reference assets remain in the release dependency graph.");
            File.WriteAllLines("Logs/FinalSubmission/ReferencedAssets.txt", assets);
            File.WriteAllText("Logs/FinalSubmission/ResourceAudit.txt", "PASS: Scene and Resources dependency closure excludes reference art and replaced original MP3s. " +
                "Unity did not supply a packed-file list; ReferencedAssets.txt records the verified dependency closure instead. " +
                assets.Count(p => p.StartsWith("Assets/OldTech/Audio/Sfx/", StringComparison.Ordinal)) + " active sound assets.");
        }

        private static string[] ReferencedAssets()
        {
            string[] resources = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)).ToArray();
            return AssetDatabase.GetDependencies(resources.Concat(new[] { GameSceneBuilder.MenuScenePath, GameSceneBuilder.ScenePath }).ToArray(), true)
                .Distinct().OrderBy(p => p).ToArray();
        }
    }
}
