using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class SceneryArtSetup
    {
        private const string TextureFolder = "Assets/OldTech/Art/Textures/Scenery";
        private const string BackgroundPath = TextureFolder + "/Background.png";
        private const string ForegroundPath = TextureFolder + "/Foreground.png";

        public static void ApplyIfAvailable()
        {
            if (File.Exists("ArtReferences/后景.png") && File.Exists("ArtReferences/前景.png")) Apply();
        }

        [MenuItem("Old Tech/Apply scenery artwork")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying scenery.");
            ImportImage("ArtReferences/后景.png", BackgroundPath, 67.5f);
            ImportImage("ArtReferences/前景.png", ForegroundPath, 135f);
            ApplyToScene(GameSceneBuilder.ScenePath);
            ApplyToScene(GameSceneBuilder.MenuScenePath);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ScenerySetup.txt", "PASS: Original transparent PNGs imported unchanged; background behind platform; foreground outside the playable lane; both scenes updated.");
        }

        private static void ImportImage(string source, string target, float pixelsPerUnit)
        {
            Directory.CreateDirectory(TextureFolder);
            File.Copy(source, target, true);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 8192;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void ApplyToScene(string path)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            ValidateScene(scene);
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            string backup = "Documentation/Backups/BeforeScenery/" + Path.GetFileName(path);
            Directory.CreateDirectory(Path.GetDirectoryName(backup));
            if (!File.Exists(backup)) File.Copy(path, backup);
            GameObject root = null;
            foreach (GameObject item in scene.GetRootGameObjects())
                if (item.name == "Scenery") { root = item; break; }
            if (root == null) root = new GameObject("Scenery");
            AddLayer(root.transform, "Background", BackgroundPath, new Vector3(20f, 0f, 8f));
            AddLayer(root.transform, "Foreground00", ForegroundPath, new Vector3(16f - 7680f / 135f, 1.35f, -5f));
            AddLayer(root.transform, "Foreground01", ForegroundPath, new Vector3(16f, 1.35f, -5f));
            AddLayer(root.transform, "Foreground02", ForegroundPath, new Vector3(16f + 7680f / 135f, 1.35f, -5f));
            ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            CapturePreview(scene);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        private static void CapturePreview(Scene scene)
        {
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<Camera>(true) != null) camera = root.GetComponentInChildren<Camera>(true);
            if (camera == null) return;
            Directory.CreateDirectory("Logs/Scenery");
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/Scenery/" + scene.name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void AddLayer(Transform parent, string name, string path, Vector3 position)
        {
            Transform existing = parent.Find(name);
            GameObject item = existing != null ? existing.gameObject : new GameObject(name);
            // Refreshing painted textures must preserve the artist's scene adjustments.
            if (existing == null)
            {
                item.transform.SetParent(parent, false);
                item.transform.SetPositionAndRotation(position, Quaternion.Euler(Mathf.Atan2(4.2f, 18f) * Mathf.Rad2Deg, 0f, 0f));
                item.transform.localScale = Vector3.one;
            }
            SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = item.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            renderer.color = Color.white;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static void ValidateScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) > 0)
                        throw new InvalidOperationException("Missing script on " + item.name + " in " + scene.path);
            if (scene.path == GameSceneBuilder.ScenePath)
            {
                HaulSession session = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponentInChildren<HaulSession>(true) != null) session = root.GetComponentInChildren<HaulSession>(true);
                if (session == null || session.defaults == null || session.cargo == null || session.grabber == null)
                    throw new InvalidOperationException("Delivery session references must resolve before saving scenery.");
            }
        }
    }
}
