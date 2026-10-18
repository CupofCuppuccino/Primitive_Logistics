using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        private const string Pending = "Library/OldTech.UpgradeProject.request";
        private static bool running;

        static ProjectSetup()
        {
            if (!Application.isBatchMode) EditorApplication.update += RunPending;
        }

        private static void RunPending()
        {
            if (running || !File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            running = true;
            try
            {
                string request = File.ReadAllText(Pending).Trim();
                if (request == "REIMPORT_SCRIPTS")
                {
                    foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/OldTech/Runtime" }))
                        AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                    EditorApplication.delayCall += DiagnoseSceneScripts;
                }
                else if (request == "SCENERY") SceneryArtSetup.Apply();
                else if (request == "EXPAND_ROUTE" || request == "APPLY_PUZZLES") PuzzleCourseSetup.Apply();
                else if (request == "CAPTURE_SCENE") PuzzleCourseSetup.CaptureScene();
                else if (request == "APPLY_ATMOSPHERE") AtmosphereArtSetup.Apply();
                else if (request == "REPLACE_BUILDINGS") AtmosphereArtSetup.ReplaceBuildings();
                else if (request == "REPLACE_FIRE") AtmosphereArtSetup.ReplaceFireFrames();
                else if (request == "APPLY_UI") GuiArtworkSetup.Apply();
                else if (request == "APPLY_CHECKPOINT_MENU") MenuBackdropSetup.Apply();
                else if (request == "APPLY_WORKER_COLLISION") WorkerCollisionSetup.Apply();
                else if (request == "APPLY_SFX") SoundEffectsSetup.Apply();
                else if (request == "BUILD_SUBMISSION") SubmissionBuild.Build();
                else if (request == "BUILD_WEBGL") WebGLSubmissionBuild.Start();
                else if (request == "RESTART_EDITOR")
                {
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                    {
                        Scene scene = SceneManager.GetSceneAt(i);
                        if (scene.isDirty && !string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
                    }
                    EditorApplication.delayCall += () => EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
                else if (request == "AUDIT_SUBMISSION") SubmissionBuild.AuditExistingRelease();
                else if (request == "EXPORT_SOURCE_MANIFEST") SourceSubmissionChecks.ExportManifest();
                else if (request == "PREVIEW_UI") UiArtworkPreviewChecks.Run();
                else if (request == "MOVE_DELIVERY") PuzzleCourseSetup.MoveDeliveryCheckpoint();
                else if (request == "CHECK_ATMOSPHERE") AtmosphereChecks.Run();
                else if (request == "CHECK_STARTUP") SceneStartupChecks.Run();
                else if (request == "CHECK_PHYSICS") PhysicsChecks.Run();
                else if (request == "CHECK_ROBOT_DAMAGE") ImpactDamageChecks.RunFromEditor();
                else if (request == "OPEN_GAME") EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
                else if (request == "DIAGNOSE") DiagnoseSceneScripts();
                else if (request == "OPEN_MENU") OpenMainMenu();
                else if (request == "BUILD_ONLY")
                {
                    Scene delivery = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
                    if (delivery.IsValid() && delivery.isLoaded && delivery.isDirty) EditorSceneManager.SaveScene(delivery);
                    GameSceneBuilder.BuildWindows();
                }
                else if (request == "UPGRADE") UpgradeAndBuild();
                else throw new InvalidOperationException("Unknown project request: " + request);
                File.Delete(Pending);
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ProjectUpgrade.txt", "PASS: Editor request handled: " + request + " at " + DateTime.Now.ToString("s"));
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ProjectUpgrade.txt", "FAIL: " + exception);
                Debug.LogException(exception);
            }
            finally { running = false; }
        }

        [MenuItem("Old Tech/Upgrade game flow and build")]
        public static void UpgradeAndBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before upgrading scenes.");
            Directory.CreateDirectory("Documentation/Backups");
            string backup = "Documentation/Backups/RollingDeliveryBeforeGameFlow.unity";
            if (!File.Exists(backup)) File.Copy(GameSceneBuilder.ScenePath, backup);
            MigrateAssets();
            Scene scene = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            if (FindInScene<RobotCargo>(scene) == null) ConfigureDeliveryScene(scene);
            EditorSceneManager.SaveScene(scene);
            if (!File.Exists(GameSceneBuilder.MenuScenePath)) CreateMenuScene();
            PuzzleCourseSetup.Apply();
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            PhysicsChecks.Run();
            GameSceneBuilder.BuildWindows();
            File.WriteAllText("Logs/ProjectUpgrade.txt", "PASS: Organized assets with GUIDs preserved; three initial logs; checkpoint recovery; main menu; pause UI; ending; physics checks; Windows build.");
            Debug.Log("OLDTECH: Game flow upgrade and build completed.");
        }

        [MenuItem("Old Tech/Open main menu")]
        public static void OpenMainMenu()
        {
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty) EditorSceneManager.SaveScene(current);
            EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath);
        }

        private static void DiagnoseSceneScripts()
        {
            var report = new System.Text.StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/OldTech/Runtime" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                report.AppendLine(path + " | " + guid + " | " + script.name + " | " + (script.GetClass()?.FullName ?? "NO CLASS"));
            }
            Scene scene = SceneManager.GetActiveScene();
            report.AppendLine("SCENE " + scene.path);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        report.AppendLine("MISSING ON " + item.name);
            report.AppendLine("Session " + UnityEngine.Object.FindObjectOfType<HaulSession>());
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/SceneScriptDiagnosis.txt", report.ToString());
        }

        private static void MigrateAssets()
        {
            string[,] moves =
            {
                { "Sand.mat", "Art/Materials/GroundSand.mat" },
                { "Wood.mat", "Art/Materials/WoodBase.mat" },
                { "End grain.mat", "Art/Materials/WoodEndBase.mat" },
                { "Rim.mat", "Art/Materials/LogOutline.mat" },
                { "Hover.mat", "Art/Materials/LogHover.mat" },
                { "Held.mat", "Art/Materials/LogHeld.mat" },
                { "Cargo deck.mat", "Art/Materials/CargoDeck.mat" },
                { "Robot.mat", "Art/Materials/RobotBody.mat" },
                { "Dark.mat", "Art/Materials/Charcoal.mat" },
                { "Safety yellow.mat", "Art/Materials/CargoAccent.mat" },
                { "Worker clay.mat", "Art/Materials/WorkerCloth.mat" },
                { "Rolling contact.physicMaterial", "Physics/Materials/RollingContact.physicMaterial" },
                { "Cylinder48.asset", "Physics/Meshes/LogCollision48.asset" },
                { "BeveledPallet.asset", "Art/Meshes/PalletBeveled.asset" }
            };
            Directory.CreateDirectory("Assets/OldTech/Physics/Materials");
            Directory.CreateDirectory("Assets/OldTech/Physics/Meshes");
            AssetDatabase.Refresh();
            for (int i = 0; i < moves.GetLength(0); i++)
            {
                string source = "Assets/OldTech/Greybox/" + moves[i, 0];
                string target = "Assets/OldTech/" + moves[i, 1];
                if (!File.Exists(source)) continue;
                if (File.Exists(target)) throw new InvalidOperationException("Asset destination already exists: " + target);
                string error = AssetDatabase.MoveAsset(source, target);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            foreach (string folder in new[] { "Assets/OldTech/Greybox", "Assets/OldTech/Scripts" })
                if (Directory.Exists(folder) && Directory.GetFileSystemEntries(folder).Length == 0)
                    AssetDatabase.DeleteAsset(folder);
        }

        public static void ConfigureDeliveryScene(Scene scene)
        {
            HaulSession session = FindInScene<HaulSession>(scene);
            if (session == null) throw new InvalidOperationException("Delivery scene has no HaulSession.");
            RollingLog[] logs = FindAllInScene<RollingLog>(scene);
            Array.Sort(logs, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (logs.Length < 3) throw new InvalidOperationException("Three original rolling logs are required.");
            if (logs[0].transform.parent != null) logs[0].transform.parent.name = "RollingLogs";
            for (int i = 3; i < logs.Length; i++) UnityEngine.Object.DestroyImmediate(logs[i].gameObject);
            for (int i = 0; i < 3; i++)
            {
                logs[i].name = "RollingLog" + (i + 1).ToString("00");
                logs[i].transform.SetPositionAndRotation(new Vector3((i - 1) * 1.5f, 0.508f, 0f), Quaternion.identity);
                LogArtSetup.ApplyToLog(logs[i]);
            }
            session.cargo.transform.SetPositionAndRotation(new Vector3(0f, 1.235f, 0f), Quaternion.identity);
            session.cargo.name = "RobotCargo";
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != "Environment") continue;
                Transform stop = root.transform.Find("Camp stop");
                if (stop != null) stop.position = new Vector3(49f, 0.65f, 0f);
            }
            if (session.GetComponent<CheckpointController>() == null) session.gameObject.AddComponent<CheckpointController>();
            if (session.GetComponent<HaulHud>() == null) session.gameObject.AddComponent<HaulHud>();
            DeliveryEnding ending = session.GetComponent<DeliveryEnding>();
            if (ending == null) ending = session.gameObject.AddComponent<DeliveryEnding>();
            Transform group = session.cargo.transform.Find("Robot");
            if (group == null)
            {
                group = new GameObject("Robot").transform;
                group.SetParent(session.cargo.transform, false);
                HashSet<string> robotParts = new HashSet<string>
                {
                    "Left foot", "Right foot", "Left leg", "Right leg", "Robot torso", "Left arm", "Right arm",
                    "Head", "Face", "Eye L", "Eye R", "Chest label"
                };
                List<Transform> parts = new List<Transform>();
                foreach (Transform child in session.cargo.transform) if (robotParts.Contains(child.name)) parts.Add(child);
                foreach (Transform child in parts) child.SetParent(group, true);
            }
            ending.robot = group;
            ending.worker = FindInScene<WorkerVisual>(scene);
            if (ending.worker != null) ending.worker.name = "Worker";
            if (session.defaults.checkpointPositions == null || session.defaults.checkpointPositions.Length == 0)
                session.defaults.checkpointPositions = new[] { 9f, 23.5f };
            EditorUtility.SetDirty(session.defaults);
            Transform markers = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Checkpoints") markers = root.transform;
            if (markers == null)
            {
                markers = new GameObject("Checkpoints").transform;
                SceneManager.MoveGameObjectToScene(markers.gameObject, scene);
            }
            for (int i = 0; i < session.defaults.checkpointPositions.Length; i++)
            {
                string name = "Checkpoint" + (i + 1).ToString("00");
                Transform existing = markers.Find(name);
                if (existing != null)
                {
                    AddCheckpointSymbol(existing);
                    continue;
                }
                float x = session.defaults.checkpointPositions[i];
                GameObject marker = new GameObject(name);
                marker.transform.SetParent(markers, false);
                marker.transform.position = new Vector3(x, RouteGeometry.HeightAt(x), 1.9f);
                MakeBox("Post", marker.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.1f, 2.4f, 0.1f), "WoodBase");
                Renderer flag = MakeBox("Flag", marker.transform, new Vector3(0f, 2.2f, 0f), new Vector3(1.5f, 0.65f, 0.1f), "WoodEndBase");
                GameObject label = new GameObject("Label");
                label.transform.SetParent(marker.transform, false);
                label.transform.localPosition = new Vector3(0f, 2.2f, -0.07f);
                TextMesh text = label.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 55;
                text.characterSize = 0.075f;
                text.anchor = TextAnchor.MiddleCenter;
                text.color = new Color(0.25f, 0.15f, 0.1f);
                text.text = "CP " + (i + 1);
                label.GetComponent<Renderer>().sharedMaterial = text.font.material;
                CheckpointMarker view = marker.AddComponent<CheckpointMarker>();
                view.checkpointIndex = i + 1;
                view.flag = flag;
                AddCheckpointSymbol(marker.transform);
            }
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void AddCheckpointSymbol(Transform marker)
        {
            Transform label = marker.Find("Label");
            if (label != null) UnityEngine.Object.DestroyImmediate(label.gameObject);
            if (marker.Find("CheckpointSymbol") != null) return;
            GameObject symbol = new GameObject("CheckpointSymbol");
            symbol.transform.SetParent(marker, false);
            LineRenderer line = symbol.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 40;
            line.widthMultiplier = 0.035f;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/LogOutline.mat");
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = (float)i / (line.positionCount - 1);
                float angle = t * Mathf.PI * 4f;
                float radius = Mathf.Lerp(0.025f, 0.23f, t);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 2.2f + Mathf.Sin(angle) * radius, -0.08f));
            }
        }

        private static Renderer MakeBox(string name, Transform parent, Vector3 position, Vector3 scale, string material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
            Renderer renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/" + material + ".mat");
            return renderer;
        }

        public static void CreateMenuScene()
        {
            Scene original = SceneManager.GetActiveScene();
            Scene delivery = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            bool openedDelivery = !delivery.IsValid() || !delivery.isLoaded;
            if (openedDelivery) delivery = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            GameObject world = new GameObject("MenuBackdrop");
            foreach (GameObject root in delivery.GetRootGameObjects())
            {
                if (root.GetComponent<CargoMotor>() == null && root.GetComponent<WorkerVisual>() == null &&
                    root.name != "Environment" && root.name != "RollingLogs" && !root.name.StartsWith("Logs")) continue;
                GameObject copy = UnityEngine.Object.Instantiate(root, world.transform);
                copy.name = root.name;
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
                foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            }
            Camera camera = new GameObject("MenuCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5.65f;
            camera.transform.position = new Vector3(-4.5f, 6f, -18f);
            camera.transform.LookAt(new Vector3(-4.5f, 1.8f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.7f, 0.71f, 0.63f);
            camera.allowHDR = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.7f, 0.75f);
            new GameObject("MainMenu").AddComponent<MainMenuController>();
            EditorSceneManager.SaveScene(menu, GameSceneBuilder.MenuScenePath);
            EditorSceneManager.CloseScene(menu, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (openedDelivery) EditorSceneManager.CloseScene(delivery, true);
        }

        public static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(GameSceneBuilder.MenuScenePath, true),
                new EditorBuildSettingsScene(GameSceneBuilder.ScenePath, true)
            };
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static T[] FindAllInScene<T>(Scene scene) where T : Component
        {
            List<T> results = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) results.AddRange(root.GetComponentsInChildren<T>(true));
            return results.ToArray();
        }
    }
}
