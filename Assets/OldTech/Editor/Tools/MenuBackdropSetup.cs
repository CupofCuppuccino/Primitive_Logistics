using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace OldTech.Editor
{
    public static class MenuBackdropSetup
    {
        public const string BellyPath = "Assets/OldTech/Art/Textures/RobotBelly.png";
        private const string SignPath = "Assets/OldTech/Art/Prefabs/CheckpointSign.prefab";
        private const string MaterialRoot = "Assets/OldTech/Art/Materials/";

        [MenuItem("Old Tech/Copy checkpoint 3 to menu and apply visual fixes")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene current = SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            const string backupRoot = "Documentation/Backups/BeforeCheckpointMenu";
            Directory.CreateDirectory(backupRoot);
            foreach (string path in new[] { GameSceneBuilder.ScenePath, GameSceneBuilder.MenuScenePath, SignPath })
            {
                string backup = backupRoot + "/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }

            Material fontMaterial = GetMaterial("CheckpointText", Shader.Find("OldTech/DepthTestedText"));
            GameObject prefab = PrefabUtility.LoadPrefabContents(SignPath);
            try
            {
                FixLabels(prefab, fontMaterial);
                PrefabUtility.SaveAsPrefabAsset(prefab, SignPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }

            Scene delivery = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            if (!delivery.IsValid() || !delivery.isLoaded) delivery = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            SceneManager.SetActiveScene(delivery);
            foreach (GameObject root in delivery.GetRootGameObjects()) FixLabels(root, fontMaterial);
            RobotCargo robot = All<RobotCargo>(delivery).Single();
            ApplyBelly(robot.transform);
            EditorSceneManager.MarkSceneDirty(delivery);
            EditorSceneManager.SaveScene(delivery);

            Scene menu = SceneManager.GetSceneByPath(GameSceneBuilder.MenuScenePath);
            if (!menu.IsValid() || !menu.isLoaded) menu = EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            // Keep the existing menu buttons, camera, and menu music. Replace its early greybox world.
            Camera camera = All<Camera>(menu).Single();
            foreach (GameObject root in menu.GetRootGameObjects())
                if (root != camera.gameObject && root.GetComponent<MainMenuController>() == null && root.name != "Audio")
                    Object.DestroyImmediate(root);
            GameObject world = new GameObject("MenuBackdropCheckpoint03");
            foreach (GameObject root in delivery.GetRootGameObjects())
            {
                if (root.name == "Systems" || root.name == "Audio") continue;
                GameObject copy = Object.Instantiate(root, world.transform);
                copy.name = root.name;
                if (PrefabUtility.IsPartOfPrefabInstance(copy))
                    PrefabUtility.UnpackPrefabInstance(copy, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                // Remove pickup first: its RequireComponent would prevent removing RollingLog.
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true).OrderBy(b => b is LogPickup ? 0 : 1))
                    if (!(behaviour is DaylightCycle) && !(behaviour is DepthTestedLabel)) Object.DestroyImmediate(behaviour);
                foreach (Collider collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (Rigidbody body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            }

            HaulSession source = All<HaulSession>(delivery).Single();
            float x = source.defaults.checkpointPositions[2];
            Transform pallet = world.transform.Find(source.cargo.name);
            Transform menuRobot = world.transform.Find(robot.name);
            Transform worker = world.transform.Find(All<WorkerVisual>(delivery).Single().name);
            float lift = RouteGeometry.HeightAt(x) - RouteGeometry.HeightAt(source.cargo.transform.position.x);
            Vector3 offset = new Vector3(x - source.cargo.transform.position.x, lift, 0f);
            pallet.position += offset;
            menuRobot.position += offset;
            worker.position = new Vector3(x - 3.05f, RouteGeometry.HeightAt(x - 3.05f), -0.1f);
            Transform rollingLogs = world.transform.Find("RollingLogs");
            Transform[] logs = rollingLogs.Cast<Transform>().OrderBy(t => t.name).ToArray();
            for (int i = 0; i < logs.Length; i++)
            {
                float logX = x + (i - (logs.Length - 1) * 0.5f) * 1.25f;
                logs[i].SetPositionAndRotation(new Vector3(logX, RouteGeometry.HeightAt(logX) + 0.508f, 0f), Quaternion.identity);
                Transform marker = logs[i].Find("SpareLogMarker");
                if (marker != null) Object.DestroyImmediate(marker.gameObject);
            }
            foreach (Transform item in world.GetComponentsInChildren<Transform>(true).ToArray())
                if (item.name == "Grab highlight") Object.DestroyImmediate(item.gameObject);
            // Keep only nearby props; shared wide terrain and backdrop meshes remain intact.
            foreach (string groupName in new[] { "Environment", "Checkpoints", "Puzzles", "Scenery", "Atmosphere" })
            {
                Transform group = world.transform.Find(groupName);
                if (group == null) continue;
                foreach (Transform child in group.Cast<Transform>().ToArray())
                {
                    if (child.GetComponent<Light>() != null || child.name == "SunDisc") continue;
                    Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) continue;
                    Bounds bounds = renderers[0].bounds;
                    foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    if (bounds.max.x < x - 14f || bounds.min.x > x + 18f) Object.DestroyImmediate(child.gameObject);
                }
            }

            camera.orthographic = true;
            camera.orthographicSize = 5.65f;
            Vector3 focus = new Vector3(x - 3f, RouteGeometry.HeightAt(x) + 1.7f, 0f);
            camera.transform.position = focus + new Vector3(0f, 4.2f, -18f);
            camera.transform.LookAt(focus);
            DaylightCycle daylight = All<DaylightCycle>(menu).Single();
            daylight.view = camera;
            daylight.cargo = null;
            daylight.sunlight = All<Light>(menu).First(l => l.type == LightType.Directional && l.name != "SkyFill");
            daylight.skyFill = All<Light>(menu).FirstOrDefault(l => l.name == "SkyFill");
            daylight.sunDisc = All<SpriteRenderer>(menu).Single(s => s.name == "SunDisc");
            daylight.ruins = All<SpriteRenderer>(menu).Where(s => s.name.StartsWith("Ruins")).ToArray();
            daylight.previewProgress = x / source.defaults.finishX;
            daylight.ApplyProgress(daylight.previewProgress);
            RenderSettings.skybox = null;
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            Validate(delivery, menu, x);
            EditorSceneManager.CloseScene(menu, true);
            SceneManager.SetActiveScene(delivery);
            All<DaylightCycle>(delivery).Single().ApplyProgress(0f);
            AssetDatabase.SaveAssets();
        }

        private static void FixLabels(GameObject root, Material material)
        {
            foreach (CheckpointMarker marker in root.GetComponentsInChildren<CheckpointMarker>(true))
            {
                foreach (TextMesh text in marker.GetComponentsInChildren<TextMesh>(true))
                {
                    DepthTestedLabel label = text.GetComponent<DepthTestedLabel>();
                    if (label == null) label = text.gameObject.AddComponent<DepthTestedLabel>();
                    label.textMaterial = material;
                    text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
                    label.RefreshAtlas();
                    EditorUtility.SetDirty(label);
                    EditorUtility.SetDirty(text.GetComponent<MeshRenderer>());
                }
            }
        }

        private static void ApplyBelly(Transform robot)
        {
            if (!File.Exists(BellyPath))
            {
                Texture2D image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                image.SetPixels(Enumerable.Repeat(new Color(1f, 0.83f, 0.56f, 1f), 512 * 512).ToArray());
                image.Apply();
                File.WriteAllBytes(BellyPath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            AssetDatabase.ImportAsset(BellyPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(BellyPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            Material material = GetMaterial("RobotBelly", Shader.Find("Unlit/Texture"));
            material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(BellyPath);
            EditorUtility.SetDirty(material);
            Transform plate = robot.Find("Chest label");
            if (plate == null) throw new InvalidOperationException("Robot chest plate not found.");
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.GetComponent<MeshFilter>().sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(quad);
            foreach (Collider collider in plate.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            plate.localPosition = new Vector3(0.019f, 1.81f, -0.351f);
            plate.localRotation = Quaternion.identity;
            plate.localScale = new Vector3(0.5f, 0.5f, 1f);
            plate.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Material GetMaterial(string name, Shader shader)
        {
            if (shader == null) throw new InvalidOperationException("Required shader missing: " + name);
            string path = MaterialRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Validate(Scene delivery, Scene menu, float x)
        {
            if (All<HaulSession>(menu).Length != 0 || All<Rigidbody>(menu).Length != 0 || All<Collider>(menu).Length != 0)
                throw new InvalidOperationException("Menu copy contains gameplay physics or a session.");
            if (All<MainMenuController>(menu).Length != 1 || All<Camera>(menu).Length != 1)
                throw new InvalidOperationException("Menu controller or camera is missing/duplicated.");
            if (All<CheckpointMarker>(delivery).Any(m => m.GetComponentInChildren<DepthTestedLabel>() == null))
                throw new InvalidOperationException("A checkpoint still uses overlay text.");
            File.WriteAllText("Logs/CheckpointMenuSetup.txt", "PASS: Copied current checkpoint 3 scenery at " + x +
                "m, worker, pallet, robot and four logs. Menu keeps buttons and music, has no gameplay physics. " +
                "All checkpoint labels and their prefab use depth-tested font material. Robot uses editable 512px square " + BellyPath + ".");
        }

        private static T[] All<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }
    }
}
