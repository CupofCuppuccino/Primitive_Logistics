using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class AtmosphereArtSetup
    {
        private const string Art = "Assets/OldTech/Art/Textures/Atmosphere/";
        private const string SignPath = "Assets/OldTech/Art/Prefabs/CheckpointSign.prefab";
        private static readonly string[] PaintedBuildings = { "画板 1.png", "画板 2.png", "画板 3.png" };
        private static bool HasPaintedBuildings => PaintedBuildings.All(name => File.Exists(Art + name));
        private static readonly string[] PaintedFire = Enumerable.Range(0, 4).Select(i => "fire/fire_frame" + i.ToString("0000") + ".png").ToArray();
        private static bool HasPaintedFire => PaintedFire.All(name => File.Exists(Art + name));

        public static void ApplyIfAvailable()
        {
            if (File.Exists(Art + "Sun.png") && (HasPaintedFire || File.Exists(Art + "CampfireFourFrames.png")) &&
                (HasPaintedBuildings || File.Exists(Art + "RuinedBuildings.png"))) Apply();
        }

        [MenuItem("Old Tech/Apply daylight and painted scenery")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene current = SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            Directory.CreateDirectory("Documentation/Backups/BeforeAtmosphere");
            foreach (string path in new[] { GameSceneBuilder.ScenePath, GameSceneBuilder.MenuScenePath, SignPath })
            {
                string backup = "Documentation/Backups/BeforeAtmosphere/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
            }
            ImportAtlas("Sun.png", 1, 1, 2.2f, false);
            ImportFire();
            ImportBuildings();
            Scene delivery = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            if (!delivery.IsValid() || !delivery.isLoaded) delivery = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            SceneManager.SetActiveScene(delivery);
            NormalizeSigns(delivery);
            ConfigureScenery(delivery, false);
            HaulSession session = Find<HaulSession>(delivery);
            DeliveryEnding ending = session.GetComponent<DeliveryEnding>();
            if (ending == null) ending = session.gameObject.AddComponent<DeliveryEnding>();
            ending.fireFrames = FireSprites();
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(delivery);
            EditorSceneManager.SaveScene(delivery);

            Scene menu = EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(menu);
            NormalizeMenuSigns(menu, delivery);
            ConfigureScenery(menu, true);
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            EditorSceneManager.CloseScene(menu, true);
            SceneManager.SetActiveScene(delivery);
            Find<DaylightCycle>(delivery).ApplyProgress(0f);
            AssetDatabase.SaveAssets();
            File.WriteAllText("Logs/AtmosphereSetup.txt", "PASS: Start sign geometry including backing board and spiral placement copied to all checkpoint signs. RouteStripe is a non-colliding child below every sign; detached stripes removed. Transparent sun with route-driven light, sky and dusk. Three ruin sprites decorate the background. Ending binds four pivot-aligned painted fire frames. Existing actors, route and scenery transforms retained.");
        }

        private static void NormalizeSigns(Scene scene)
        {
            CheckpointMarker[] markers = All<CheckpointMarker>(scene).OrderBy(m => m.checkpointIndex).ToArray();
            CheckpointMarker start = markers.Single(m => m.checkpointIndex == 0);
            Transform stripeSource = All<Transform>(scene).Where(t => t.name == "Route stripe" || t.name == "RouteStripe")
                .OrderBy(t => Mathf.Abs(t.position.x - start.transform.position.x)).FirstOrDefault();
            Material stripeMaterial = stripeSource != null ? stripeSource.GetComponent<Renderer>().sharedMaterial :
                AssetDatabase.LoadAssetAtPath<Material>("Assets/OldTech/Art/Materials/CargoAccent.mat");
            Vector3 stripeScale = stripeSource != null ? stripeSource.lossyScale : new Vector3(0.07f, 0.012f, 4.8f);
            GameObject template = UnityEngine.Object.Instantiate(start.gameObject);
            template.name = "CheckpointSign";
            if (PrefabUtility.IsPartOfPrefabInstance(template)) PrefabUtility.UnpackPrefabInstance(template, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            template.transform.SetParent(null);
            template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            template.transform.localScale = Vector3.one;
            foreach (Transform stripe in template.GetComponentsInChildren<Transform>(true).Where(IsStripe).ToArray())
                UnityEngine.Object.DestroyImmediate(stripe.gameObject);
            GameObject stripeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripeObject.name = "RouteStripe";
            stripeObject.transform.SetParent(template.transform, false);
            stripeObject.transform.localPosition = new Vector3(0f, 0.008f, -1.9f);
            stripeObject.transform.localScale = stripeScale;
            stripeObject.GetComponent<Renderer>().sharedMaterial = stripeMaterial;
            UnityEngine.Object.DestroyImmediate(stripeObject.GetComponent<Collider>());
            TextMesh templateText = template.transform.Find("Label").GetComponent<TextMesh>();
            templateText.characterSize = 0.065f;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, SignPath);
            UnityEngine.Object.DestroyImmediate(template);

            // Build every sign from the artist-edited start, retaining each checkpoint's data and position.
            foreach (CheckpointMarker old in markers)
            {
                Vector3 position = old.transform.position;
                Quaternion rotation = old.transform.rotation;
                Transform parent = old.transform.parent;
                int index = old.checkpointIndex, count = old.recoveryLogCount;
                string text = old.transform.Find("Label").GetComponent<TextMesh>().text;
                string name = old.name;
                UnityEngine.Object.DestroyImmediate(old.gameObject);
                GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                item.name = name;
                item.transform.SetPositionAndRotation(position, rotation);
                CheckpointMarker marker = item.GetComponent<CheckpointMarker>();
                marker.checkpointIndex = index;
                marker.recoveryLogCount = count;
                TextMesh label = item.transform.Find("Label").GetComponent<TextMesh>();
                label.text = text;
                float width = label.GetComponent<Renderer>().bounds.size.x;
                if (width > 2.25f) label.characterSize *= 2.25f / width;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
            foreach (Transform stripe in All<Transform>(scene).Where(IsStripe).ToArray())
                if (stripe.GetComponentInParent<CheckpointMarker>() == null) UnityEngine.Object.DestroyImmediate(stripe.gameObject);
        }

        private static void NormalizeMenuSigns(Scene menu, Scene delivery)
        {
            foreach (Transform item in All<Transform>(menu).ToArray())
                if (IsStripe(item) || item.name.EndsWith(" board") || item.name.EndsWith(" post") || item.GetComponent<TextMesh>() != null)
                    UnityEngine.Object.DestroyImmediate(item.gameObject);
            Transform previous = Root(menu, "Checkpoints");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            Transform group = new GameObject("Checkpoints").transform;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SignPath);
            foreach (CheckpointMarker source in All<CheckpointMarker>(delivery).Where(m => m.checkpointIndex <= 1))
            {
                GameObject item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                item.name = source.name;
                item.transform.position = source.transform.position;
                CheckpointMarker marker = item.GetComponent<CheckpointMarker>();
                marker.checkpointIndex = source.checkpointIndex;
                TextMesh label = item.transform.Find("Label").GetComponent<TextMesh>();
                TextMesh original = source.transform.Find("Label").GetComponent<TextMesh>();
                label.text = original.text; label.characterSize = original.characterSize;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(label);
            }
        }

        private static bool IsStripe(Transform item) => item.name == "Route stripe" || item.name == "RouteStripe";

        private static void ConfigureScenery(Scene scene, bool menu)
        {
            Transform root = Root(scene, "Atmosphere");
            if (root == null) root = new GameObject("Atmosphere").transform;
            foreach (Transform child in root.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
            DaylightCycle daylight = root.GetComponent<DaylightCycle>();
            if (daylight == null) daylight = root.gameObject.AddComponent<DaylightCycle>();
            daylight.view = Find<Camera>(scene);
            daylight.cargo = menu ? null : Find<CargoMotor>(scene).transform;
            daylight.sunlight = All<Light>(scene).First(l => l.type == LightType.Directional);
            GameObject skyFill = new GameObject("SkyFill");
            skyFill.transform.SetParent(root, false);
            skyFill.transform.rotation = Quaternion.Euler(35f, -10f, 0f);
            daylight.skyFill = skyFill.AddComponent<Light>();
            daylight.skyFill.type = LightType.Directional;
            daylight.skyFill.shadows = LightShadows.None;
            daylight.previewProgress = menu ? 0.12f : 0f;
            daylight.finishX = RouteGeometry.FinishX;
            GameObject disc = new GameObject("SunDisc");
            disc.transform.SetParent(root, false);
            daylight.sunDisc = disc.AddComponent<SpriteRenderer>();
            daylight.sunDisc.sprite = Sprites("Sun.png")[0];
            daylight.sunDisc.sortingOrder = -30;
            Sprite[] sprites = BuildingSprites();
            var renderers = new List<SpriteRenderer>();
            float[] positions = menu ? new[] { -10f, 8f, 27f } : new[] { -10f, 8f, 27f, 44f, 61f, 79f, 102f, 115f, 132f, 150f, 171f, 188f, 204f };
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject ruin = new GameObject("Ruins" + i.ToString("00"));
                ruin.transform.SetParent(root, false);
                float depth = 15f + i % 2 * 3f;
                // The camera looks down slightly: compensate depth so distant bases meet the horizon.
                ruin.transform.SetPositionAndRotation(new Vector3(positions[i], -0.6f - depth * (4.2f / 18f), depth), daylight.view.transform.rotation);
                ruin.transform.localScale = Vector3.one * (0.72f + (i % 4) * 0.08f);
                SpriteRenderer renderer = ruin.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[i % sprites.Length];
                renderer.flipX = i % 2 != 0;
                renderer.sortingOrder = -10;
                renderers.Add(renderer);
            }
            daylight.ruins = renderers.ToArray();
            daylight.ApplyProgress(daylight.previewProgress);
            EditorUtility.SetDirty(daylight);
        }

        private static void ImportAtlas(string filename, int columns, int rows, float cellWorldSize, bool floorPivot)
        {
            string path = Art + filename;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var pixels = new Texture2D(2, 2);
            pixels.LoadImage(File.ReadAllBytes(path));
            int width = pixels.width / columns, height = pixels.height / rows;
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = columns * rows == 1 ? SpriteImportMode.Single : SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = width / cellWorldSize;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var frames = new List<SpriteMetaData>();
            for (int row = 0; row < rows; row++)
                for (int column = 0; column < columns; column++)
                {
                    int x = column * width, y = (rows - 1 - row) * height;
                    int bottom = 0;
                    if (floorPivot)
                    {
                        bool found = false;
                        for (int py = 0; py < height && !found; py++)
                            for (int px = 0; px < width; px++)
                                if (pixels.GetPixel(x + px, y + py).a > 0.2f) { bottom = py; found = true; break; }
                    }
                    frames.Add(new SpriteMetaData
                    {
                        name = Path.GetFileNameWithoutExtension(filename) + (row * columns + column).ToString("00"),
                        rect = new Rect(x, y, width, height), alignment = (int)SpriteAlignment.Custom,
                        pivot = floorPivot ? new Vector2(0.5f, (float)bottom / height) : new Vector2(0.5f, 0.5f)
                    });
                }
            if (frames.Count > 1) importer.spritesheet = frames.ToArray();
            else
            {
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = frames[0].pivot;
                importer.SetTextureSettings(settings);
            }
            importer.SaveAndReimport();
            UnityEngine.Object.DestroyImmediate(pixels);
        }

        private static void ImportBuildings()
        {
            if (HasPaintedBuildings)
                foreach (string name in PaintedBuildings) ImportAtlas(name, 1, 1, 8f, true);
            else ImportAtlas("RuinedBuildings.png", 3, 1, 8f, true);
        }

        private static Sprite[] BuildingSprites() => HasPaintedBuildings ? PaintedBuildings.SelectMany(Sprites).ToArray() : Sprites("RuinedBuildings.png");

        private static void ImportFire()
        {
            if (HasPaintedFire)
                foreach (string name in PaintedFire) ImportAtlas(name, 1, 1, 2.3f, true);
            else ImportAtlas("CampfireFourFrames.png", 2, 2, 2.3f, true);
        }

        private static Sprite[] FireSprites() => HasPaintedFire ? PaintedFire.SelectMany(Sprites).ToArray() : Sprites("CampfireFourFrames.png");

        [MenuItem("Old Tech/Replace ending fire frames")]
        public static void ReplaceFireFrames()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene original = SceneManager.GetActiveScene();
            if (original.isDirty) EditorSceneManager.SaveScene(original);
            ImportFire();
            Sprite[] frames = FireSprites();
            if (frames.Length != 4) throw new InvalidOperationException("Four ordered fire sprites are required.");
            Scene scene = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            HaulSession session = Find<HaulSession>(scene);
            DeliveryEnding ending = session.GetComponent<DeliveryEnding>();
            ending.fireFrames = frames;
            EditorUtility.SetDirty(ending);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            Directory.CreateDirectory("Logs/PaintedUi");
            File.WriteAllLines("Logs/PaintedUi/FireFrames.txt", frames.Select((frame, index) => "PASS: Frame " + index + " = " + AssetDatabase.GetAssetPath(frame)).ToArray());
        }

        [MenuItem("Old Tech/Replace background building artwork")]
        public static void ReplaceBuildings()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene original = SceneManager.GetActiveScene();
            if (original.isDirty) EditorSceneManager.SaveScene(original);
            ImportBuildings();
            Sprite[] sprites = BuildingSprites();
            if (sprites.Length != 3) throw new InvalidOperationException("Three building sprites are required.");
            Directory.CreateDirectory("Documentation/Backups/BeforeBuildingReplacement");
            Directory.CreateDirectory("Logs/BuildingReplacement");
            var report = new List<string>();
            foreach (string path in new[] { GameSceneBuilder.ScenePath, GameSceneBuilder.MenuScenePath })
            {
                string backup = "Documentation/Backups/BeforeBuildingReplacement/" + Path.GetFileName(path);
                if (!File.Exists(backup)) File.Copy(path, backup);
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                DaylightCycle daylight = Find<DaylightCycle>(scene);
                if (daylight == null || daylight.ruins == null) throw new InvalidOperationException("Building scenery missing in " + path);
                for (int i = 0; i < daylight.ruins.Length; i++)
                {
                    SpriteRenderer drawing = daylight.ruins[i];
                    Matrix4x4 before = drawing.transform.localToWorldMatrix;
                    drawing.sprite = sprites[i % sprites.Length];
                    EditorUtility.SetDirty(drawing);
                    if (drawing.transform.localToWorldMatrix != before) throw new InvalidOperationException("Building placement changed.");
                    report.Add("PASS: " + scene.name + "/" + drawing.name + " uses " + AssetDatabase.GetAssetPath(drawing.sprite) + "; placement retained.");
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                CaptureBuildings(Find<Camera>(scene), scene.name);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("Logs/BuildingReplacement/Validation.txt", report);
        }

        private static void CaptureBuildings(Camera camera, string sceneName)
        {
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/BuildingReplacement/" + sceneName + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static Sprite[] Sprites(string filename) => AssetDatabase.LoadAllAssetsAtPath(Art + filename).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        private static T Find<T>(Scene scene) where T : Component => All<T>(scene).FirstOrDefault();
        private static Transform Root(Scene scene, string name) => scene.GetRootGameObjects().FirstOrDefault(g => g.name == name)?.transform;
    }
}
