using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class LevelExpansionSetup
    {
        public const string MusicFolder = "Assets/OldTech/Audio/Music";
        private static readonly float[] CheckpointPositions = { 9f, 23.5f, 36f, 56f, 84f, 108f };
        private static readonly int[] LogCounts = { 3, 3, 3, 4, 4, 4, 4 };

        [MenuItem("Old Tech/Apply extended delivery route")]
        public static void ApplyFromMenu() { PuzzleCourseSetup.Apply(); }

        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene current = SceneManager.GetActiveScene();
            if (current.IsValid() && current.isDirty) EditorSceneManager.SaveScene(current);
            Directory.CreateDirectory("Documentation/Backups/BeforeExtendedRoute");
            foreach (string path in new[] { GameSceneBuilder.ScenePath, GameSceneBuilder.MenuScenePath, GameSceneBuilder.SettingsPath })
            {
                string backup = "Documentation/Backups/BeforeExtendedRoute/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            MoveMusic("InGameBGM.wav", "GameplayPart1.wav");
            MoveMusic("InGameBGM_Part2.wav", "GameplayPart2.wav");
            MoveMusic("InGameBGM_String.wav", "MenuStrings.wav");
            Scene delivery = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            HaulSession session = Find<HaulSession>(delivery);
            if (session == null) throw new InvalidOperationException("Delivery scene requires HaulSession.");
            Transform scenery = FindRoot(delivery, "Scenery");
            Transform foreground = scenery.Find("Foreground01");
            Transform background = scenery.Find("Background");
            Matrix4x4 backgroundBefore = background.localToWorldMatrix;
            Matrix4x4 foregroundBefore = foreground.localToWorldMatrix;
            Transform checkpointRoot = FindRoot(delivery, "Checkpoints");
            Transform checkpointTemplate = checkpointRoot.Find("Checkpoint01");
            if (checkpointTemplate == null) throw new InvalidOperationException("Checkpoint01 is required as the edited sign template.");
            string signBefore = DescribeHierarchy(checkpointTemplate);

            session.defaults.checkpointPositions = (float[])CheckpointPositions.Clone();
            session.defaults.checkpointLogCounts = (int[])LogCounts.Clone();
            session.defaults.finishX = RouteGeometry.FinishX;
            EditorUtility.SetDirty(session.defaults);
            ExtendTerrain(delivery, session);
            ConfigureRobot(delivery, session);
            ConfigureFourthLog(delivery, session);
            MatchCheckpointSigns(checkpointRoot, checkpointTemplate);
            MatchForegrounds(scenery, foreground, session.defaults.finishX);
            ExtendBackground(scenery, background, session.defaults.finishX);
            ConfigureMusic(delivery, false);
            if (background.localToWorldMatrix != backgroundBefore || foreground.localToWorldMatrix != foregroundBefore ||
                DescribeHierarchy(checkpointTemplate) != signBefore)
                throw new InvalidOperationException("The edited Background, Foreground01 and Checkpoint01 must remain unchanged.");
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(delivery);
            EditorSceneManager.SaveScene(delivery);

            // Update the menu in place so its camera and backdrop are preserved.
            Scene menu = EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            Transform menuScenery = FindRoot(menu, "Scenery");
            if (menuScenery != null)
            {
                Transform menuAnchor = menuScenery.Find("Foreground01");
                if (menuAnchor != null)
                {
                    menuAnchor.localPosition = foreground.localPosition;
                    menuAnchor.localRotation = foreground.localRotation;
                    menuAnchor.localScale = foreground.localScale;
                    MatchForegrounds(menuScenery, menuAnchor, 35f);
                }
            }
            ConfigureMusic(menu, true);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            EditorSceneManager.CloseScene(menu, true);
            SceneManager.SetActiveScene(delivery);
            ProjectSetup.ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ExtendedRouteSetup.txt", "PASS: Music moved with GUIDs preserved. Gameplay two-part playlist and menu strings configured. Route extended to 134m with two pits and two steep hill groups. Six checkpoints with 3/4 log budgets. Fourth roadside log added. Robot detached as an independent Rigidbody; only robot collisions count. Foreground tiles and checkpoint signs matched to edited templates. Original Background, Foreground01 and Checkpoint01 hierarchy preserved.");
        }

        private static void MoveMusic(string originalName, string finalName)
        {
            Directory.CreateDirectory(MusicFolder);
            AssetDatabase.Refresh();
            string oldPath = "Assets/OldTech/" + originalName;
            string newPath = MusicFolder + "/" + finalName;
            if (File.Exists(oldPath))
            {
                string error = AssetDatabase.MoveAsset(oldPath, newPath);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            var importer = AssetImporter.GetAtPath(newPath) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("Missing music: " + newPath);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.85f;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureMusic(Scene scene, bool menu)
        {
            Transform root = FindRoot(scene, "Audio");
            if (root == null) root = new GameObject("Audio").transform;
            Transform child = root.Find("Music");
            if (child == null)
            {
                child = new GameObject("Music").transform;
                child.SetParent(root, false);
            }
            MusicPlayer player = child.GetComponent<MusicPlayer>();
            if (player == null) player = child.gameObject.AddComponent<MusicPlayer>();
            player.playlist = menu ? new[] { Clip("MenuStrings.wav") } : new[] { Clip("GameplayPart1.wav"), Clip("GameplayPart2.wav") };
            EditorUtility.SetDirty(player);
        }

        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(MusicFolder + "/" + name);

        private static void ConfigureRobot(Scene scene, HaulSession session)
        {
            Transform robot = session.robot != null ? session.robot.transform : session.cargo.transform.Find("Robot");
            if (robot == null) throw new InvalidOperationException("Existing Robot group not found.");
            robot.SetParent(null, true);
            robot.name = "Robot";
            Rigidbody body = robot.GetComponent<Rigidbody>();
            if (body == null) body = robot.gameObject.AddComponent<Rigidbody>();
            body.mass = session.defaults.robotMass;
            body.drag = 0.025f;
            body.angularDrag = 0.8f;
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.solverIterations = 16;
            body.solverVelocityIterations = 8;
            body.centerOfMass = new Vector3(0f, 0.9f, 0f);
            const string robotContactPath = "Assets/OldTech/Physics/Materials/RobotContact.physicMaterial";
            PhysicMaterial robotContact = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(robotContactPath);
            if (robotContact == null)
            {
                robotContact = new PhysicMaterial("RobotContact");
                AssetDatabase.CreateAsset(robotContact, robotContactPath);
            }
            // Good static footing, but noticeably less grip once a hard stop makes the payload slide.
            robotContact.staticFriction = 0.85f;
            robotContact.dynamicFriction = 0.35f;
            robotContact.frictionCombine = PhysicMaterialCombine.Minimum;
            robotContact.bounciness = 0f;
            EditorUtility.SetDirty(robotContact);
            foreach (Collider item in robot.GetComponentsInChildren<Collider>()) item.sharedMaterial = robotContact;
            RobotCargo payload = robot.GetComponent<RobotCargo>();
            if (payload == null) payload = robot.gameObject.AddComponent<RobotCargo>();
            session.robot = payload;
            session.cargo.name = "TransportPallet";
            session.cargo.GetComponent<Rigidbody>().centerOfMass = Vector3.zero;
            DeliveryEnding ending = session.GetComponent<DeliveryEnding>();
            if (ending != null) { ending.robot = robot; EditorUtility.SetDirty(ending); }
        }

        private static void ConfigureFourthLog(Scene scene, HaulSession session)
        {
            RollingLog[] logs = Array.FindAll(FindAll<RollingLog>(scene), log => !log.anchored);
            Array.Sort(logs, (a, b) => string.CompareOrdinal(a.name, b.name));
            RollingLog spare = Array.Find(logs, item => item.name == "RollingLog04");
            // Reuse an already duplicated fourth log instead of creating a fifth physical log.
            RollingLog existingExtra = Array.Find(logs, item => item.name != "RollingLog01" && item.name != "RollingLog02" &&
                item.name != "RollingLog03" && item.name != "RollingLog04" && item.GetComponent<LogPickup>() == null);
            if (existingExtra != null)
            {
                if (spare != null) UnityEngine.Object.DestroyImmediate(spare.gameObject);
                spare = existingExtra;
                spare.name = "RollingLog04";
            }
            if (spare == null)
            {
                spare = UnityEngine.Object.Instantiate(logs[2], logs[2].transform.parent);
                spare.name = "RollingLog04";
            }
            float x = session.defaults.fourthLogX;
            spare.transform.SetPositionAndRotation(new Vector3(x, RouteGeometry.HeightAt(x) + 0.508f, 0f), Quaternion.identity);
            spare.GetComponent<Rigidbody>().isKinematic = true;
            LogPickup pickup = spare.GetComponent<LogPickup>();
            if (pickup == null) pickup = spare.gameObject.AddComponent<LogPickup>();
            if (pickup.marker == null)
            {
                Transform marker = new GameObject("SpareLogMarker").transform;
                marker.SetParent(spare.transform, false);
                marker.localPosition = new Vector3(0f, 1.3f, 0f);
                TextMesh text = marker.gameObject.AddComponent<TextMesh>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 48;
                text.characterSize = 0.065f;
                text.anchor = TextAnchor.MiddleCenter;
                text.text = "+1 LOG";
                text.color = new Color(0.35f, 0.2f, 0.08f);
                marker.GetComponent<Renderer>().sharedMaterial = text.font.material;
                pickup.marker = marker;
            }
            EditorUtility.SetDirty(pickup);
            if (Array.FindAll(FindAll<RollingLog>(scene), log => !log.anchored).Length != 4) throw new InvalidOperationException("The route must contain exactly three starting logs and one roadside spare.");
        }

        private static void MatchCheckpointSigns(Transform root, Transform template)
        {
            for (int i = 0; i < CheckpointPositions.Length; i++)
            {
                Transform marker = template;
                if (i > 0)
                {
                    string name = "Checkpoint" + (i + 1).ToString("00");
                    Transform old = root.Find(name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    marker = UnityEngine.Object.Instantiate(template, root);
                    marker.name = name;
                    marker.position = new Vector3(CheckpointPositions[i], RouteGeometry.HeightAt(CheckpointPositions[i]) + template.position.y - RouteGeometry.HeightAt(template.position.x), template.position.z);
                }
                CheckpointMarker view = marker.GetComponent<CheckpointMarker>();
                view.checkpointIndex = i + 1;
                view.recoveryLogCount = LogCounts[i + 1];
                EditorUtility.SetDirty(view);
            }
        }

        private static void MatchForegrounds(Transform root, Transform template, float finish)
        {
            SpriteRenderer renderer = template.GetComponent<SpriteRenderer>();
            float spacing = renderer.sprite.bounds.size.x * template.localScale.x;
            int last = Mathf.CeilToInt((finish + 14f - template.position.x) / spacing) + 1;
            for (int i = 0; i <= last; i++)
            {
                if (i == 1) continue;
                string name = "Foreground" + i.ToString("00");
                Transform item = root.Find(name);
                if (item == null) { item = UnityEngine.Object.Instantiate(template, root); item.name = name; }
                item.localPosition = template.localPosition + Vector3.right * ((i - 1) * spacing);
                item.localRotation = template.localRotation;
                item.localScale = template.localScale;
                SpriteRenderer visual = item.GetComponent<SpriteRenderer>();
                visual.sprite = renderer.sprite;
                visual.sharedMaterial = renderer.sharedMaterial;
                visual.color = renderer.color;
                visual.sortingOrder = renderer.sortingOrder;
            }
        }

        private static void ExtendBackground(Transform root, Transform template, float finish)
        {
            SpriteRenderer renderer = template.GetComponent<SpriteRenderer>();
            float spacing = renderer.sprite.bounds.size.x * template.localScale.x;
            for (int i = 1; template.position.x + (i - 0.5f) * spacing < finish + 12f; i++)
            {
                string name = "Background" + (i + 1).ToString("00");
                Transform item = root.Find(name);
                if (item == null) { item = UnityEngine.Object.Instantiate(template, root); item.name = name; }
                item.localPosition = template.localPosition + Vector3.right * (i * spacing);
                item.localRotation = template.localRotation;
                item.localScale = template.localScale;
            }
        }

        private static void ExtendTerrain(Scene scene, HaulSession session)
        {
            Transform environment = FindRoot(scene, "Environment");
            Material ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/GroundSand.mat");
            var remove = new List<GameObject>();
            foreach (Transform child in environment)
                if (child.name.StartsWith("Track ") && float.TryParse(child.name.Split(' ')[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) && x >= 36f)
                { ground = child.GetComponent<Renderer>().sharedMaterial; remove.Add(child.gameObject); }
                else if (child.name.StartsWith("DELIVERY")) remove.Add(child.gameObject);
            foreach (GameObject item in remove) UnityEngine.Object.DestroyImmediate(item);
            foreach (Vector4 section in RouteGeometry.Sections)
            {
                if (section.x < 36f) continue;
                Vector3 tangent = new Vector3(section.y - section.x, section.w - section.z, 0f);
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.name = "Track " + section.x + " to " + section.y;
                segment.layer = 8;
                segment.transform.SetParent(environment, false);
                segment.transform.position = new Vector3((section.x + section.y) * 0.5f, (section.z + section.w) * 0.5f, 0f) - normal * 0.5f;
                segment.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                segment.transform.localScale = new Vector3(tangent.magnitude + 0.005f, 1f, 6f);
                segment.GetComponent<Renderer>().sharedMaterial = ground;
                segment.GetComponent<Collider>().sharedMaterial = session.contactMaterial;
            }
            Transform stop = environment.Find("Camp stop");
            if (stop != null) stop.position = new Vector3(149f, 0.65f, 0f);
            MakeRouteLabel(environment, "Pit01Hint", 39f, "FILL THE PIT WITH LOGS");
            MakeRouteLabel(environment, "Steep01Hint", 59f, "STEEP HILL / EASY DOES IT");
            MakeRouteLabel(environment, "Pit02Hint", 92f, "ONE MORE PIT");
            MakeRouteLabel(environment, "Steep02Hint", 110f, "LAST CLIMB / MIND THE ROBOT");
            MakeRouteLabel(environment, "DeliveryGoal", 134f, "DELIVERY");
        }

        private static void MakeRouteLabel(Transform parent, string name, float x, string label)
        {
            Transform item = parent.Find(name);
            if (item == null) { item = new GameObject(name).transform; item.SetParent(parent, false); }
            item.position = new Vector3(x, RouteGeometry.HeightAt(x) + 3.4f, 2.8f);
            TextMesh text = item.GetComponent<TextMesh>();
            if (text == null) text = item.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 48;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.text = label;
            text.color = new Color(0.3f, 0.2f, 0.12f);
            item.GetComponent<Renderer>().sharedMaterial = text.font.material;
        }

        private static string DescribeHierarchy(Transform root)
        {
            var text = new System.Text.StringBuilder();
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                text.Append(child.name).Append(child.localPosition).Append(child.localRotation).Append(child.localScale);
            return text.ToString();
        }

        private static Transform FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == name) return root.transform;
            return null;
        }
        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.GetComponentInChildren<T>(true) != null) return root.GetComponentInChildren<T>(true);
            return null;
        }
        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects()) found.AddRange(root.GetComponentsInChildren<T>(true));
            return found.ToArray();
        }
    }
}
