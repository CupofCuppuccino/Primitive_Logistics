using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace OldTech.Editor
{
    public static class GuiArtworkSetup
    {
        public const string TextureRoot = "Assets/OldTech/Art/Textures/UI/";
        private const string AssetPath = "Assets/OldTech/Resources/GuiArtwork.asset";
        private static bool queued;

        [MenuItem("Old Tech/Apply editable UI pictures")]
        public static void Apply()
        {
            Directory.CreateDirectory(TextureRoot);
            GUISkin skin = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Game);
            ExportIfMissing("SliderTrack", skin.horizontalSlider.normal.background);
            ExportIfMissing("SliderThumb", skin.horizontalSliderThumb.normal.background);
            ExportIfMissing("ModalShade", Texture2D.whiteTexture);
            foreach (GuiImageId id in Enum.GetValues(typeof(GuiImageId)))
            {
                string path = TextureRoot + id + ".png";
                if (!File.Exists(path)) throw new FileNotFoundException("UI image missing: " + path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 1024;
                // Painted transparent edges should not acquire block-compression fringes.
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Directory.CreateDirectory("Assets/OldTech/Resources");
            GuiArtwork asset = AssetDatabase.LoadAssetAtPath<GuiArtwork>(AssetPath);
            if (asset == null) { asset = ScriptableObject.CreateInstance<GuiArtwork>(); AssetDatabase.CreateAsset(asset, AssetPath); }
            RefreshRegions();
            if (!asset.Complete) throw new InvalidOperationException("UI artwork bindings are incomplete.");
            Directory.CreateDirectory("Logs/PaintedUi");
            File.WriteAllLines("Logs/PaintedUi/Artwork.txt", asset.images.Select(image => "PASS: " + image.name + " = " + AssetDatabase.GetAssetPath(image.texture) + "; visible region " + image.region).ToArray());
        }

        // Repainting/replacing any PNG refreshes its visible bounds without changing the file.
        public static void QueueRefresh()
        {
            if (queued) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; RefreshRegions(); };
        }

        private static void RefreshRegions()
        {
            GuiArtwork asset = AssetDatabase.LoadAssetAtPath<GuiArtwork>(AssetPath);
            if (asset == null) return;
            GuiImageId[] ids = (GuiImageId[])Enum.GetValues(typeof(GuiImageId));
            asset.images = new GuiImage[ids.Length];
            foreach (GuiImageId id in ids)
            {
                string path = TextureRoot + id + ".png";
                asset.images[(int)id] = new GuiImage { name = id.ToString(), texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path), region = OpaqueRegion(path) };
            }
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        private static Rect OpaqueRegion(string path)
        {
            if (!File.Exists(path)) return new Rect(0f, 0f, 1f, 1f);
            var image = new Texture2D(2, 2);
            try
            {
                image.LoadImage(File.ReadAllBytes(path));
                Color32[] pixels = image.GetPixels32();
                int minX = image.width, minY = image.height, maxX = -1, maxY = -1;
                for (int y = 0; y < image.height; y++)
                    for (int x = 0; x < image.width; x++)
                        if (pixels[y * image.width + x].a > 8)
                        { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                return maxX < 0 ? new Rect(0f, 0f, 1f, 1f) : new Rect((float)minX / image.width, (float)minY / image.height,
                    (float)(maxX - minX + 1) / image.width, (float)(maxY - minY + 1) / image.height);
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        private static void ExportIfMissing(string name, Texture2D source)
        {
            string path = TextureRoot + name + ".png";
            if (File.Exists(path)) return;
            if (source == null) source = Texture2D.whiteTexture;
            RenderTexture target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }

    public sealed class GuiArtworkImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] oldPaths)
        {
            if (imported.Concat(moved).Any(path => path.StartsWith(GuiArtworkSetup.TextureRoot, StringComparison.Ordinal) && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                GuiArtworkSetup.QueueRefresh();
        }
    }
}
