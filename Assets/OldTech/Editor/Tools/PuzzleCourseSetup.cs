using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class PuzzleCourseSetup
    {
        private const string SignPath = "Assets/OldTech/Art/Prefabs/CheckpointSign.prefab";
        private static readonly float[] Positions = { 9f, 23.5f, 36f, 56f, 84f, 108f, 134f, 164f, 185f };
        private static readonly string[] Labels = { "DEPARTURE", "RIDGE", "COAST", "STEEP HILL", "BOULDER PIT", "LAST CLIMB", "LOW ARCH", "ONE LOG PIT", "DELIVERY" };

        [MenuItem("Old Tech/Apply puzzle course")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            CaptureScene();
            LevelExpansionSetup.Apply();
            Scene scene = SceneManager.GetActiveScene();
            HaulSession session = UnityEngine.Object.FindObjectOfType<HaulSession>();
            session.defaults.robotMass = 16f;
            session.defaults.finishX = RouteGeometry.FinishX;
            session.defaults.checkpointPositions = (float[])Positions.Clone();
            session.defaults.checkpointLogCounts = new[] { 3, 3, 3, 4, 4, 4, 4, 4, 4, 4 };
            EditorUtility.SetDirty(session.defaults);
            ConfigurePayloadAndEdges(scene, session);
            CreateUnifiedSigns(scene);
            Transform environment = Root(scene, "Environment");
            foreach (Transform child in new List<Transform>(Children(environment)))
                if (child.name.EndsWith(" board") || child.name.EndsWith(" post") || child.GetComponent<TextMesh>() != null)
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            Transform stop = environment.Find("Camp stop");
            if (stop != null) stop.position = new Vector3(RouteGeometry.FinishX + 15f, 0.65f, 0f);
            Material ground = AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/GroundSand.mat");
            CreateTerrainFill(scene, ground);
            CreatePuzzles(scene, session);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Scene menu = EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            CreateTerrainFill(menu, ground);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            EditorSceneManager.CloseScene(menu, true);
            SceneManager.SetActiveScene(scene);
            AssetDatabase.SaveAssets();
            AtmosphereArtSetup.ApplyIfAvailable();
            File.WriteAllText("Logs/PuzzleCourseSetup.txt", "PASS: 190m course; nine unified checkpoint signs plus start sign; continuous terrain fill without colliders; 16kg loose robot with lower centre of mass; existing PalletEdge colliders; fixed pit log; log-pushed boulder and square pit; low arch; one-log notch. Background and foreground templates retained.");
        }

        public static void MoveDeliveryCheckpoint()
        {
            Scene original = SceneManager.GetActiveScene();
            if (original.isDirty) EditorSceneManager.SaveScene(original);
            Scene scene = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            HaulSettings settings = AssetDatabase.LoadAssetAtPath<HaulSettings>(GameSceneBuilder.SettingsPath);
            settings.checkpointPositions[8] = 185f;
            EditorUtility.SetDirty(settings);
            Transform marker = Root(scene, "Checkpoints").Find("Checkpoint09");
            if (marker == null) throw new InvalidOperationException("DELIVERY checkpoint is missing.");
            Vector3 position = marker.position;
            position.y += RouteGeometry.HeightAt(185f) - RouteGeometry.HeightAt(position.x);
            position.x = 185f;
            marker.position = position;
            PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            File.WriteAllText("Logs/DeliveryCheckpointMove.txt", "PASS: Checkpoint09 / DELIVERY sign and child stripe moved to 185m. HaulSettings and route generation defaults agree. Finish remains at 190m.");
        }

        private static void ConfigurePayloadAndEdges(Scene scene, HaulSession session)
        {
            Rigidbody robot = session.robot.GetComponent<Rigidbody>();
            robot.mass = session.defaults.robotMass;
            robot.centerOfMass = new Vector3(0f, 0.78f, 0f);
            robot.angularDrag = 1.5f;
            PhysicMaterial robotContact = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/OldTech/Physics/Materials/RobotContact.physicMaterial");
            robotContact.staticFriction = 0.9f;
            robotContact.dynamicFriction = 0.48f;
            EditorUtility.SetDirty(robotContact);
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!item.name.StartsWith("PalletEdge")) continue;
                    item.SetParent(session.cargo.transform, true);
                    item.gameObject.layer = 10;
                    Rigidbody extraBody = item.GetComponent<Rigidbody>();
                    if (extraBody != null) UnityEngine.Object.DestroyImmediate(extraBody);
                    Collider collider = item.GetComponent<Collider>();
                    if (collider == null)
                    {
                        BoxCollider box = item.gameObject.AddComponent<BoxCollider>();
                        MeshFilter mesh = item.GetComponent<MeshFilter>();
                        if (mesh != null && mesh.sharedMesh != null) { box.center = mesh.sharedMesh.bounds.center; box.size = mesh.sharedMesh.bounds.size; }
                        collider = box;
                    }
                    collider.isTrigger = false;
                    collider.sharedMaterial = session.contactMaterial;
                    count++;
                }
            if (count < 2) throw new InvalidOperationException("Both user-created PalletEdge objects must be present.");
            EditorUtility.SetDirty(robot);
        }

        private static void CreateUnifiedSigns(Scene scene)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SignPath);
            if (prefab == null)
            {
                Transform board = FindTransform(scene, "03 / COAST board");
                Transform post = FindTransform(scene, "03 / COAST post");
                Transform label = FindTransform(scene, "03 / COAST");
                if (board == null || post == null || label == null) throw new InvalidOperationException("The edited COAST board template was not found.");
                GameObject unit = new GameObject("CheckpointSign");
                Vector3 origin = new Vector3(26f, RouteGeometry.HeightAt(26f), 1.9f);
                Transform boardCopy = UnityEngine.Object.Instantiate(board, unit.transform);
                Transform postCopy = UnityEngine.Object.Instantiate(post, unit.transform);
                Transform labelCopy = UnityEngine.Object.Instantiate(label, unit.transform);
                boardCopy.name = "Board"; postCopy.name = "Post"; labelCopy.name = "Label";
                boardCopy.localPosition = board.position - origin;
                postCopy.localPosition = post.position - origin;
                labelCopy.localPosition = label.position - origin;
                labelCopy.GetComponent<TextMesh>().characterSize = 0.065f;
                GameObject symbol = new GameObject("Spiral");
                symbol.transform.SetParent(unit.transform, false);
                LineRenderer line = symbol.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 48;
                line.widthMultiplier = 0.04f;
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/LogOutline.mat");
                for (int i = 0; i < line.positionCount; i++)
                {
                    float t = (float)i / (line.positionCount - 1);
                    float a = t * Mathf.PI * 4f;
                    float r = Mathf.Lerp(0.02f, 0.23f, t);
                    line.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 1.15f + Mathf.Sin(a) * r, -0.11f));
                }
                CheckpointMarker marker = unit.AddComponent<CheckpointMarker>();
                marker.flag = boardCopy.GetComponent<Renderer>();
                marker.symbol = line;
                Directory.CreateDirectory(Path.GetDirectoryName(SignPath));
                prefab = PrefabUtility.SaveAsPrefabAsset(unit, SignPath);
                UnityEngine.Object.DestroyImmediate(unit);
            }
            Transform root = Root(scene, "Checkpoints");
            foreach (Transform child in new List<Transform>(Children(root))) UnityEngine.Object.DestroyImmediate(child.gameObject);
            for (int i = 0; i <= Positions.Length; i++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                instance.name = "Checkpoint" + i.ToString("00");
                float x = i == 0 ? -5.5f : Positions[i - 1];
                instance.transform.position = new Vector3(x, RouteGeometry.HeightAt(x), 1.9f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                CheckpointMarker marker = instance.GetComponent<CheckpointMarker>();
                marker.checkpointIndex = i;
                marker.recoveryLogCount = i < 3 ? 3 : 4;
                TextMesh text = instance.transform.Find("Label").GetComponent<TextMesh>();
                text.text = i == 0 ? "START / THREE LOGS" : i.ToString("00") + " / " + Labels[i - 1];
                float width = text.GetComponent<Renderer>().bounds.size.x;
                if (width > 2.25f) text.characterSize *= 2.25f / width;
                EditorUtility.SetDirty(marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform.Find("Label").GetComponent<TextMesh>());
            }
        }

        private static void CreateTerrainFill(Scene scene, Material ground)
        {
            const string path = "Assets/OldTech/Art/Meshes/TerrainFill.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = "ContinuousTerrainFill" };
                AssetDatabase.CreateAsset(mesh, path);
            }
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            foreach (Vector4 section in RouteGeometry.Sections)
            {
                int index = vertices.Count;
                vertices.Add(new Vector3(section.x, section.z, 0f));
                vertices.Add(new Vector3(section.x, -24f, 0f));
                vertices.Add(new Vector3(section.y, -24f, 0f));
                vertices.Add(new Vector3(section.y, section.w, 0f));
                triangles.AddRange(new[] { index, index + 2, index + 1, index, index + 3, index + 2 });
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            Transform fill = Root(scene, "TerrainFill");
            if (fill == null) fill = FindTransform(scene, "Plane");
            if (fill == null) fill = new GameObject("TerrainFill").transform;
            Transform oldPlane = FindTransform(scene, "Plane");
            if (oldPlane != null && oldPlane != fill) UnityEngine.Object.DestroyImmediate(oldPlane.gameObject);
            fill.SetParent(null, true);
            fill.name = "TerrainFill";
            fill.SetPositionAndRotation(new Vector3(0f, 0f, -3.015f), Quaternion.identity);
            fill.localScale = Vector3.one;
            foreach (Collider collider in fill.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            MeshFilter filter = fill.GetComponent<MeshFilter>();
            if (filter == null) filter = fill.gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = fill.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = fill.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ground;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void CreatePuzzles(Scene scene, HaulSession session)
        {
            Transform root = Root(scene, "Puzzles");
            if (root == null) root = new GameObject("Puzzles").transform;
            foreach (Transform child in new List<Transform>(Children(root))) UnityEngine.Object.DestroyImmediate(child.gameObject);
            PuzzleDirector director = root.GetComponent<PuzzleDirector>();
            if (director == null) director = root.gameObject.AddComponent<PuzzleDirector>();
            Material stone = StoneMaterial();
            PhysicMaterial contact = new PhysicMaterial("StoneContact")
            { staticFriction = 0.06f, dynamicFriction = 0.04f, frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0f };
            const string contactPath = "Assets/OldTech/Physics/Materials/StoneContact.physicMaterial";
            PhysicMaterial saved = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(contactPath);
            if (saved == null) { AssetDatabase.CreateAsset(contact, contactPath); saved = contact; }
            else { EditorUtility.CopySerialized(contact, saved); UnityEngine.Object.DestroyImmediate(contact); }
            GameObject boulder = new GameObject("Boulder");
            boulder.layer = 8;
            boulder.transform.SetParent(root, false);
            boulder.transform.position = new Vector3(90f, 1.45f, 0f);
            Mesh rockMesh = RockMesh();
            boulder.AddComponent<MeshFilter>().sharedMesh = rockMesh;
            boulder.AddComponent<MeshRenderer>().sharedMaterial = stone;
            MeshCollider shape = boulder.AddComponent<MeshCollider>(); shape.sharedMesh = rockMesh; shape.convex = true; shape.sharedMaterial = saved;
            Rigidbody body = boulder.AddComponent<Rigidbody>();
            body.mass = 55f; body.isKinematic = true; body.drag = 0.02f;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            director.boulder = boulder.AddComponent<BoulderPuzzle>();
            MakeBox(root, "SquarePitLeftWall", new Vector3(95.9f, -1.45f, 0f), new Vector3(0.2f, 2.9f, 6f), stone);
            MakeBox(root, "SquarePitRightWall", new Vector3(99.5f, -1.45f, 0f), new Vector3(0.2f, 2.9f, 6f), stone);
            MakeBox(root, "LowArchRoof", new Vector3(146f, 4.15f, 0f), new Vector3(6f, 0.6f, 6f), stone);
            MakeBox(root, "LowArchFrontPier", new Vector3(146f, 1.925f, -2.8f), new Vector3(0.4f, 3.85f, 0.35f), stone);
            MakeBox(root, "LowArchBackPier", new Vector3(146f, 1.925f, 2.8f), new Vector3(0.4f, 3.85f, 0.35f), stone);
            RollingLog source = Array.Find(UnityEngine.Object.FindObjectsOfType<RollingLog>(), log => !log.anchored && log.name == "RollingLog03");
            RollingLog fixedLog = UnityEngine.Object.Instantiate(source, root);
            fixedLog.name = "FixedPitLog";
            fixedLog.anchored = true;
            fixedLog.transform.SetPositionAndRotation(new Vector3(45.5f, -0.392f, 0f), Quaternion.identity);
            fixedLog.GetComponent<Rigidbody>().isKinematic = true;
            fixedLog.SetHovered(false);
            foreach (Transform child in fixedLog.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            director.fixedLog = fixedLog;
            EditorUtility.SetDirty(director);
        }

        private static Mesh RockMesh()
        {
            const string path = "Assets/OldTech/Art/Meshes/Boulder.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh != null) return mesh;
            var points = new[] { new Vector2(-1.2f, -1.45f), new Vector2(1.2f, -1.45f), new Vector2(1.45f, -1.2f), new Vector2(1.45f, 1.2f), new Vector2(1.2f, 1.45f), new Vector2(-1.2f, 1.45f), new Vector2(-1.45f, 1.2f), new Vector2(-1.45f, -1.2f) };
            var v = new List<Vector3>(); var t = new List<int>();
            for (int side = 0; side < 2; side++) foreach (Vector2 p in points) v.Add(new Vector3(p.x, p.y, side == 0 ? -1.45f : 1.45f));
            for (int i = 1; i < 7; i++) { t.AddRange(new[] { 0, i + 1, i }); t.AddRange(new[] { 8, i + 8, i + 9 }); }
            for (int i = 0; i < 8; i++) { int n = (i + 1) % 8; t.AddRange(new[] { i, n, i + 8, n, n + 8, i + 8 }); }
            mesh = new Mesh { name = "BeveledBoulder" }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path); return mesh;
        }
        private static Material StoneMaterial()
        {
            const string path = "Assets/OldTech/Art/Materials/Stone.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material, path); }
            material.color = new Color(0.44f, 0.42f, 0.37f); material.SetFloat("_Glossiness", 0.05f); EditorUtility.SetDirty(material); return material;
        }
        private static void MakeBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.layer = 8;
            box.transform.SetParent(parent, false); box.transform.position = position; box.transform.localScale = scale; box.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static Transform Root(Scene scene, string name)
        {
            foreach (GameObject item in scene.GetRootGameObjects()) if (item.name == name) return item.transform;
            return null;
        }
        private static Transform FindTransform(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) foreach (Transform item in root.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item;
            return null;
        }
        private static IEnumerable<Transform> Children(Transform root) { foreach (Transform child in root) yield return child; }

        public static void CaptureScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            Directory.CreateDirectory("Documentation/Backups/BeforePuzzleCourse");
            foreach (string path in new[] { GameSceneBuilder.ScenePath, GameSceneBuilder.MenuScenePath, GameSceneBuilder.SettingsPath })
            {
                string backup = "Documentation/Backups/BeforePuzzleCourse/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            var report = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!item.name.ToLowerInvariant().Contains("plane") && !item.name.Contains("PalletEdge") && item.parent != null) continue;
                    report.AppendLine(item.name + " position=" + item.position + " rotation=" + item.eulerAngles + " scale=" + item.lossyScale);
                    foreach (Renderer renderer in item.GetComponents<Renderer>())
                        report.AppendLine("  Renderer bounds=" + renderer.bounds + " material=" + AssetDatabase.GetAssetPath(renderer.sharedMaterial));
                    foreach (Collider collider in item.GetComponents<Collider>())
                        report.AppendLine("  Collider " + collider.GetType().Name + " bounds=" + collider.bounds);
                }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/PuzzleCourseSceneSnapshot.txt", report.ToString());
        }
    }
}
