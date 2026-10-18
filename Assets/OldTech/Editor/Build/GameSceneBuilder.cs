using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace OldTech.Editor
{
    public static class GameSceneBuilder
    {
        public const string ScenePath = "Assets/OldTech/Scenes/RollingDelivery.unity";
        public const string SettingsPath = "Assets/OldTech/Settings/HaulSettings.asset";
        private const string ArtPath = "Assets/OldTech/Art/Materials";
        public const string MenuScenePath = "Assets/OldTech/Scenes/MainMenu.unity";
        private static PhysicMaterial contact;
        private static Material ground, wood, endGrain, rim, hover, held, deck, metal, dark, accent, worker;
        private static Mesh cylinder;

        [MenuItem("Old Tech/Rebuild delivery scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }

        // Command line entry point; uses the installed 2022.3.62f3 editor, no external packages.
        public static void PrepareProject()
        {
            BuildScene();
            Debug.Log("OLDTECH: Scene prepared successfully with Unity " + Application.unityVersion);
        }

        public static void BuildScene()
        {
            Directory.CreateDirectory("Assets/OldTech/Scenes");
            Directory.CreateDirectory("Assets/OldTech/Settings");
            Directory.CreateDirectory(ArtPath);
            Directory.CreateDirectory("Assets/OldTech/Art/Meshes");
            Directory.CreateDirectory("Assets/OldTech/Physics/Materials");
            Directory.CreateDirectory("Assets/OldTech/Physics/Meshes");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureProject();
            CreateMaterials();
            HaulSettings settings = AssetDatabase.LoadAssetAtPath<HaulSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<HaulSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }

            GameObject environment = new GameObject("Environment");
            MakeGround(environment.transform, -18f, 12f, 0f, 0f);
            MakeGround(environment.transform, 12f, 21f, 0f, RouteGeometry.SummitHeight);
            MakeGround(environment.transform, 21f, 26f, RouteGeometry.SummitHeight, RouteGeometry.SummitHeight);
            MakeGround(environment.transform, 26f, 36f, RouteGeometry.SummitHeight, 0f);
            MakeGround(environment.transform, 36f, 53f, 0f, 0f);
            Cube("Start boundary", environment.transform, new Vector3(-12f, 1f, 0f), new Vector3(0.5f, 3f, 6f), ground, true, 8);
            Cube("Camp stop", environment.transform, new Vector3(49f, 0.65f, 0f), new Vector3(0.7f, 1.3f, 6f), ground, true, 8);
            MakeMarker(environment.transform, -5.5f, "01 / DEPARTURE", false);
            MakeMarker(environment.transform, 12f, "02 / UPHILL", false);
            MakeMarker(environment.transform, 26f, "03 / COAST", false);
            MakeMarker(environment.transform, settings.finishX, "DELIVERY", true);

            GameObject logsRoot = new GameObject("RollingLogs");
            float[] initialX = { -1.5f, 0f, 1.5f };
            for (int i = 0; i < initialX.Length; i++) MakeLog(logsRoot.transform, i, initialX[i]);
            CargoMotor cargo = MakeCargo();
            MakeWorker(cargo);

            GameObject systems = new GameObject("Systems");
            Camera view = new GameObject("Main Camera").AddComponent<Camera>();
            view.transform.SetParent(systems.transform);
            view.tag = "MainCamera";
            view.orthographic = true;
            view.orthographicSize = 5.65f;
            view.nearClipPlane = 0.1f;
            view.farClipPlane = 120f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.65f, 0.72f, 0.72f);
            view.allowHDR = false;
            view.gameObject.AddComponent<AudioListener>();
            HaulCamera follow = view.gameObject.AddComponent<HaulCamera>();
            follow.cargo = cargo.transform;
            follow.Snap();

            LogGrabber grabber = systems.AddComponent<LogGrabber>();
            grabber.view = view;
            grabber.cargo = cargo;
            HaulSession session = systems.AddComponent<HaulSession>();
            session.defaults = settings;
            session.contactMaterial = contact;
            session.cargo = cargo;
            session.grabber = grabber;

            Light sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(environment.transform);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.intensity = 1.12f;
            sun.color = new Color(1f, 0.94f, 0.81f);
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.035f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.64f, 0.7f, 0.75f);
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            RenderSettings.skybox = null;

            ProjectSetup.ConfigureDeliveryScene(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            ProjectSetup.ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            SceneryArtSetup.ApplyIfAvailable();
            PuzzleCourseSetup.Apply();
            Selection.activeGameObject = cargo.gameObject;
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
                sceneView.LookAt(new Vector3(1f, 1.7f, 0f), Quaternion.Euler(13f, 0f, 0f), 12f, true);
        }

        private static void ConfigureProject()
        {
            Time.fixedDeltaTime = 1f / 90f;
            Time.maximumDeltaTime = 0.1f;
            SerializedObject timeSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0]);
            timeSettings.FindProperty("Fixed Timestep").floatValue = 1f / 90f;
            timeSettings.FindProperty("Maximum Allowed Timestep").floatValue = 0.1f;
            timeSettings.ApplyModifiedPropertiesWithoutUndo();
            Physics.gravity = new Vector3(0f, -15f, 0f);
            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics.defaultSolverIterations = 16;
            Physics.defaultSolverVelocityIterations = 8;
            Physics.defaultContactOffset = 0.008f;
            Physics.defaultMaxDepenetrationVelocity = 3f;
            Physics.sleepThreshold = 0.003f;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 65f;
            QualitySettings.vSyncCount = 1;
            PlayerSettings.companyName = "Lydia";
            PlayerSettings.productName = "Primitive Logistics";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            SerializedObject tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            layers.GetArrayElementAtIndex(8).stringValue = "Ground";
            layers.GetArrayElementAtIndex(9).stringValue = "Logs";
            layers.GetArrayElementAtIndex(10).stringValue = "Cargo";
            tags.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material MaterialAsset(string name, Color color, bool unlit = false)
        {
            string path = ArtPath + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(unlit ? "Unlit/Color" : "Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.16f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateMaterials()
        {
            ground = MaterialAsset("GroundSand", new Color(0.56f, 0.51f, 0.4f));
            wood = MaterialAsset("WoodBase", new Color(0.37f, 0.21f, 0.105f));
            endGrain = MaterialAsset("WoodEndBase", new Color(0.77f, 0.58f, 0.32f));
            rim = MaterialAsset("LogOutline", new Color(0.3f, 0.19f, 0.09f), true);
            hover = MaterialAsset("LogHover", new Color(1f, 0.93f, 0.64f), true);
            held = MaterialAsset("LogHeld", new Color(0.78f, 0.94f, 0.39f), true);
            deck = MaterialAsset("CargoDeck", new Color(0.18f, 0.25f, 0.27f));
            metal = MaterialAsset("RobotBody", new Color(0.68f, 0.79f, 0.73f));
            dark = MaterialAsset("Charcoal", new Color(0.07f, 0.1f, 0.11f));
            accent = MaterialAsset("CargoAccent", new Color(0.82f, 0.89f, 0.32f));
            worker = MaterialAsset("WorkerCloth", new Color(0.7f, 0.31f, 0.19f));
            contact = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/OldTech/Physics/Materials/RollingContact.physicMaterial");
            if (contact == null)
            {
                contact = new PhysicMaterial("Rolling contact");
                AssetDatabase.CreateAsset(contact, "Assets/OldTech/Physics/Materials/RollingContact.physicMaterial");
            }
            contact.dynamicFriction = contact.staticFriction = 0.85f;
            contact.bounciness = 0f;
            contact.frictionCombine = PhysicMaterialCombine.Average;
            contact.bounceCombine = PhysicMaterialCombine.Minimum;
            EditorUtility.SetDirty(contact);
            cylinder = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/OldTech/Physics/Meshes/LogCollision48.asset");
            if (cylinder == null)
            {
                cylinder = MakeCylinder(48, 0.5f, 2.65f);
                AssetDatabase.CreateAsset(cylinder, "Assets/OldTech/Physics/Meshes/LogCollision48.asset");
            }
        }

        private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool physical = false, int layer = 0)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.layer = layer;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!physical) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            else obj.GetComponent<Collider>().sharedMaterial = contact;
            return obj;
        }

        private static void MakeGround(Transform parent, float x0, float x1, float y0, float y1)
        {
            Vector3 tangent = new Vector3(x1 - x0, y1 - y0, 0f);
            Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
            Vector3 center = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f) - normal * 0.5f;
            GameObject segment = Cube("Track " + x0 + " to " + x1, parent, center,
                new Vector3(tangent.magnitude + 0.015f, 1f, 6f), ground, true, 8);
            segment.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
        }

        private static void MakeMarker(Transform parent, float x, string label, bool finish)
        {
            float y = RouteGeometry.HeightAt(x);
            Cube(label + " post", parent, new Vector3(x, y + 1.05f, 1.9f), new Vector3(0.1f, 2.1f, 0.1f), deck);
            Cube(label + " board", parent, new Vector3(x, y + 1.9f, 1.9f), new Vector3(2.45f, 0.55f, 0.1f), finish ? accent : deck);
            GameObject text = new GameObject(label);
            text.transform.SetParent(parent);
            text.transform.position = new Vector3(x, y + 1.9f, 1.83f);
            text.transform.rotation = Quaternion.identity;
            TextMesh mesh = text.AddComponent<TextMesh>();
            mesh.text = label;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 50;
            mesh.characterSize = 0.047f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = finish ? new Color(0.08f, 0.12f, 0.1f) : new Color(0.9f, 0.9f, 0.81f);
            text.GetComponent<Renderer>().sharedMaterial = mesh.font.material;
            Cube("Route stripe", parent, new Vector3(x, y + 0.008f, 0f), new Vector3(0.07f, 0.012f, 4.8f), accent);
        }

        private static void MakeLog(Transform parent, int index, float x)
        {
            GameObject obj = new GameObject("Log " + (index + 1));
            obj.transform.SetParent(parent);
            obj.transform.position = new Vector3(x, 0.508f, 0f);
            obj.layer = 9;
            obj.AddComponent<MeshFilter>().sharedMesh = cylinder;
            obj.AddComponent<MeshRenderer>().sharedMaterial = wood;
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = cylinder;
            collider.convex = true;
            collider.sharedMaterial = contact;
            Rigidbody body = obj.AddComponent<Rigidbody>();
            ConfigureBody(body, 2.4f);
            body.angularDrag = 0.045f;
            RollingLog log = obj.AddComponent<RollingLog>();
            log.normalRim = rim;
            log.hoverRim = hover;
            log.heldRim = held;

            GameObject face = new GameObject("End grain (visual)");
            face.transform.SetParent(obj.transform, false);
            face.transform.localPosition = new Vector3(0f, 0f, -1.327f);
            face.transform.localScale = new Vector3(0.94f, 0.94f, 0.005f);
            face.AddComponent<MeshFilter>().sharedMesh = cylinder;
            face.AddComponent<MeshRenderer>().sharedMaterial = endGrain;
            GameObject ringObj = new GameObject("Grab highlight");
            ringObj.transform.SetParent(obj.transform, false);
            LineRenderer line = ringObj.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 48;
            line.widthMultiplier = 0.022f;
            line.sharedMaterial = rim;
            for (int i = 0; i < 48; i++)
            {
                float a = i * Mathf.PI * 2f / 48f;
                line.SetPosition(i, new Vector3(Mathf.Cos(a) * 0.37f, Mathf.Sin(a) * 0.37f, -1.343f));
            }
            log.rim = line;
            Cube("Rotation tick", obj.transform, new Vector3(0.23f, 0f, -1.344f),
                new Vector3(0.2f, 0.045f, 0.012f), rim);
            LogArtSetup.ApplyToLog(log);
        }

        private static void ConfigureBody(Rigidbody body, float mass)
        {
            body.mass = mass;
            body.drag = 0.035f;
            body.angularDrag = 0.15f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY;
            body.solverIterations = 20;
            body.solverVelocityIterations = 10;
            body.maxAngularVelocity = 25f;
        }

        private static CargoMotor MakeCargo()
        {
            GameObject root = new GameObject("Cargo - rigid robot and pallet");
            root.layer = 10;
            root.transform.position = new Vector3(0f, 1.235f, 0f);
            MakeBeveledDeck(root.transform);
            for (int i = 0; i < 5; i++)
                Cube("Pallet top slat", root.transform, new Vector3(-1.9f + i * 0.95f, 0.24f, 0f),
                    new Vector3(0.8f, 0.065f, 1.7f), wood);
            Cube("Left foot", root.transform, new Vector3(-0.35f, 0.4f, 0f), new Vector3(0.44f, 0.24f, 0.7f), metal, true, 10);
            Cube("Right foot", root.transform, new Vector3(0.35f, 0.4f, 0f), new Vector3(0.44f, 0.24f, 0.7f), metal, true, 10);
            Cube("Left leg", root.transform, new Vector3(-0.35f, 0.94f, 0f), new Vector3(0.3f, 0.85f, 0.4f), metal, true, 10);
            Cube("Right leg", root.transform, new Vector3(0.35f, 0.94f, 0f), new Vector3(0.3f, 0.85f, 0.4f), metal, true, 10);
            Cube("Robot torso", root.transform, new Vector3(0f, 1.81f, 0f), new Vector3(1.15f, 1.12f, 0.65f), metal, true, 10);
            Cube("Left arm", root.transform, new Vector3(-0.77f, 1.77f, 0f), new Vector3(0.27f, 1.14f, 0.37f), metal, true, 10);
            Cube("Right arm", root.transform, new Vector3(0.77f, 1.77f, 0f), new Vector3(0.27f, 1.14f, 0.37f), metal, true, 10);
            Cube("Head", root.transform, new Vector3(0f, 2.76f, 0f), new Vector3(0.88f, 0.66f, 0.69f), metal, true, 10);
            Cube("Face", root.transform, new Vector3(0f, 2.78f, -0.355f), new Vector3(0.69f, 0.25f, 0.025f), dark);
            Cube("Eye L", root.transform, new Vector3(-0.2f, 2.78f, -0.377f), new Vector3(0.1f, 0.1f, 0.025f), accent);
            Cube("Eye R", root.transform, new Vector3(0.2f, 2.78f, -0.377f), new Vector3(0.1f, 0.1f, 0.025f), accent);
            Cube("Chest label", root.transform, new Vector3(0f, 1.9f, -0.34f), new Vector3(0.6f, 0.26f, 0.035f), accent);
            Rigidbody body = root.AddComponent<Rigidbody>();
            ConfigureBody(body, 24f);
            body.angularDrag = 0.8f;
            body.maxAngularVelocity = 4f;
            body.centerOfMass = new Vector3(0f, 0.28f, 0f);
            return root.AddComponent<CargoMotor>();
        }

        private static void MakeWorker(CargoMotor cargo)
        {
            GameObject root = new GameObject("Worker - cubes only, no model required");
            root.transform.position = new Vector3(-3.05f, 0f, -0.1f);
            WorkerVisual visual = root.AddComponent<WorkerVisual>();
            visual.cargo = cargo;
            GameObject torso = new GameObject("Torso pivot");
            torso.transform.SetParent(root.transform, false);
            torso.transform.localPosition = new Vector3(0f, 0.68f, 0f);
            visual.torso = torso.transform;
            Cube("Body", torso.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.65f, 0.45f), worker);
            Cube("Head", torso.transform, new Vector3(0.03f, 0.85f, 0f), new Vector3(0.47f, 0.46f, 0.46f), endGrain);
            Cube("Hair", torso.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.5f, 0.14f, 0.49f), dark);
            Cube("Nose", torso.transform, new Vector3(0.3f, 0.83f, 0f), new Vector3(0.15f, 0.16f, 0.22f), endGrain);
            Cube("Arms", torso.transform, new Vector3(0.39f, 0.38f, 0f), new Vector3(0.65f, 0.15f, 0.6f), worker);
            visual.leftLeg = MakeLeg(root.transform, "Near leg", -0.18f);
            visual.rightLeg = MakeLeg(root.transform, "Far leg", 0.18f);
        }

        private static void MakeBeveledDeck(Transform parent)
        {
            string path = "Assets/OldTech/Art/Meshes/PalletBeveled.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                Vector2[] outline =
                {
                    new Vector2(-2.4f, 0.22f), new Vector2(2.4f, 0.22f),
                    new Vector2(2.4f, 0.02f), new Vector2(1.85f, -0.22f),
                    new Vector2(-1.85f, -0.22f), new Vector2(-2.4f, 0.02f)
                };
                List<Vector3> vertices = new List<Vector3>();
                List<int> triangles = new List<int>();
                for (int side = 0; side < 2; side++)
                    foreach (Vector2 p in outline) vertices.Add(new Vector3(p.x, p.y, side == 0 ? -0.825f : 0.825f));
                for (int i = 1; i < 5; i++)
                {
                    triangles.AddRange(new[] { 0, i, i + 1 });
                    triangles.AddRange(new[] { 6, i + 7, i + 6 });
                }
                for (int i = 0; i < 6; i++)
                {
                    int next = (i + 1) % 6;
                    triangles.AddRange(new[] { i, i + 6, next, next, i + 6, next + 6 });
                }
                mesh = new Mesh { name = "Pallet with raised leading edges" };
                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            GameObject obj = new GameObject("Pallet - beveled runner edges");
            obj.layer = 10;
            obj.transform.SetParent(parent, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = deck;
            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;
            collider.sharedMaterial = contact;
        }

        private static Transform MakeLeg(Transform parent, string name, float z)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = new Vector3(0f, 0.67f, z);
            Cube("Leg", pivot.transform, new Vector3(0f, -0.3f, 0f), new Vector3(0.18f, 0.6f, 0.18f), dark);
            Cube("Foot", pivot.transform, new Vector3(0.08f, -0.59f, 0f), new Vector3(0.34f, 0.12f, 0.22f), dark);
            return pivot.transform;
        }

        private static Mesh MakeCylinder(int segments, float radius, float length)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();
            float half = length * 0.5f;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                Vector3 normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices.Add(normal * radius + Vector3.back * half);
                vertices.Add(normal * radius + Vector3.forward * half);
                normals.Add(normal);
                normals.Add(normal);
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = ((i + 1) % segments) * 2;
                triangles.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }
            for (int side = 0; side < 2; side++)
            {
                int center = vertices.Count;
                Vector3 normal = side == 0 ? Vector3.back : Vector3.forward;
                vertices.Add(normal * half);
                normals.Add(normal);
                for (int i = 0; i < segments; i++)
                {
                    float angle = 2f * Mathf.PI * i / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, normal.z * half));
                    normals.Add(normal);
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = center + 1 + i, b = center + 1 + (i + 1) % segments;
                    triangles.AddRange(side == 0 ? new[] { center, b, a } : new[] { center, a, b });
                }
            }
            Mesh mesh = new Mesh { name = "Cylinder48 - true convex roller" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        [MenuItem("Old Tech/Build Windows game")]
        public static void BuildWindows()
        {
            if (!File.Exists(ScenePath)) BuildScene();
            if (!File.Exists(MenuScenePath)) ProjectSetup.CreateMenuScene();
            ProjectSetup.ConfigureBuildScenes();
            Directory.CreateDirectory("Builds/Windows");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MenuScenePath, ScenePath },
                locationPathName = "Builds/Windows/PrimitiveLogistics.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + result.summary.result);
            Debug.Log("OLDTECH: Windows build succeeded: " + result.summary.totalSize + " bytes");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/BuildWindows.txt", "PASS: Unity " + Application.unityVersion + " Windows build. " + result.summary.totalSize + " bytes. " + DateTime.Now.ToString("s"));
        }

        public static void BuildAndCapture()
        {
            BuildScene();
            Camera camera = UnityEngine.Object.FindObjectOfType<HaulCamera>().GetComponent<Camera>();
            RenderTexture rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Logs");
            File.WriteAllBytes("Logs/ScenePreview.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(image);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            BuildWindows();
        }
    }
}
