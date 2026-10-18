using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    [InitializeOnLoad]
    public static class LogArtSetup
    {
        private const string Art = "Assets/OldTech/Art";
        private const string Pending = "Library/OldTech.ApplyLogArt.request";
        private static Mesh visualMesh;
        private static Material barkMaterial, endMaterial;
        private static bool processing;
        private static double nextCheck;

        static LogArtSetup()
        {
            // A one-shot request lets the already-open editor apply an authorized art update.
            // It waits for edit mode and never interrupts play or recompilation.
            if (!Application.isBatchMode && File.Exists(Pending)) EditorApplication.update += ProcessPendingRequest;
        }

        private static void ProcessPendingRequest()
        {
            if (processing || EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (!File.Exists(Pending) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            processing = true;
            try
            {
                if (File.ReadAllText(Pending).Trim() == "SAVE_AND_BUILD") ApplyAndBuild();
                else ApplyPaintedTextures();
                if (File.Exists(Pending)) File.Delete(Pending);
                EditorApplication.update -= ProcessPendingRequest;
            }
            catch (Exception exception)
            {
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/LogArtUpdate.txt", "FAIL: " + exception);
                Debug.LogException(exception);
                // Do not retry a failed operation on every editor frame.
                EditorApplication.update -= ProcessPendingRequest;
            }
            finally { processing = false; }
        }

        [MenuItem("Old Tech/Apply painted log textures")]
        public static void ApplyPaintedTextures()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying log textures.");
            AssetDatabase.Refresh();
            if (!PrepareAssets()) throw new FileNotFoundException("LogEnd.png and LogBark.png are required under " + Art + "/Textures.");

            Scene previousActive = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            bool hadUnsavedChanges = scene.isDirty;
            int applied = 0;
            RollingLog previewLog = null;
            foreach (RollingLog log in UnityEngine.Object.FindObjectsOfType<RollingLog>(true))
            {
                if (log.gameObject.scene != scene) continue;
                MeshCollider collider = log.GetComponent<MeshCollider>();
                Mesh originalCollision = collider.sharedMesh;
                ApplyToLog(log);
                if (previewLog == null || log.transform.position.x > previewLog.transform.position.x) previewLog = log;
                if (collider.sharedMesh != originalCollision)
                    throw new InvalidOperationException("Art update unexpectedly changed a physics mesh.");
                applied++;
            }
            if (applied == 0) throw new InvalidOperationException("No rolling logs found in the delivery scene.");
            EditorSceneManager.MarkSceneDirty(scene);
            if (!hadUnsavedChanges) EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory("ArtReferences");
            Camera view = null;
            foreach (Camera candidate in UnityEngine.Object.FindObjectsOfType<Camera>())
                if (candidate.gameObject.scene == scene && candidate.CompareTag("MainCamera")) view = candidate;
            if (view != null)
            {
                Capture(view, "ArtReferences/LogTextures_InGame.png");
                GameObject preview = new GameObject("Temporary log art preview", typeof(Camera));
                try
                {
                    SceneManager.MoveGameObjectToScene(preview, scene);
                    Camera closeup = preview.GetComponent<Camera>();
                    closeup.CopyFrom(view);
                    closeup.enabled = false;
                    closeup.orthographicSize = 1.55f;
                    Vector3 focus = previewLog.transform.position + Vector3.up * 0.05f;
                    closeup.transform.position = focus + new Vector3(2.7f, 1.85f, -5f);
                    closeup.transform.LookAt(focus);
                    Capture(closeup, "ArtReferences/LogTextures_Closeup.png");
                }
                finally { UnityEngine.Object.DestroyImmediate(preview); }
            }
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
            }
            Directory.CreateDirectory("Logs");
            string result = "PASS: Applied painted bark and both circular end faces to " + applied +
                " logs. Physics meshes and worker model preserved. UV seam split; two material slots. " +
                (hadUnsavedChanges ? "Existing unsaved scene edits preserved; save the scene to persist the new renderer assignments." : "Scene and art assets saved.");
            File.WriteAllText("Logs/LogArtUpdate.txt", result);
            Debug.Log("OLDTECH: " + result);
            SceneView.RepaintAll();
        }

        public static bool PrepareAssets()
        {
            string barkPath = Art + "/Textures/LogBark.png";
            string endPath = Art + "/Textures/LogEnd.png";
            if (!File.Exists(barkPath) || !File.Exists(endPath)) return false;
            ConfigureTexture(barkPath, false);
            ConfigureTexture(endPath, true);
            Directory.CreateDirectory(Art + "/Materials");
            Directory.CreateDirectory(Art + "/Meshes");
            AssetDatabase.Refresh();
            barkMaterial = MakeMaterial(Art + "/Materials/LogBark.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(barkPath));
            endMaterial = MakeMaterial(Art + "/Materials/LogEnd.mat", AssetDatabase.LoadAssetAtPath<Texture2D>(endPath));
            string meshPath = Art + "/Meshes/LogVisual48.asset";
            visualMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (visualMesh == null)
            {
                visualMesh = MakeVisualCylinder();
                AssetDatabase.CreateAsset(visualMesh, meshPath);
            }
            return true;
        }

        private static void ConfigureTexture(string path, bool cutEnd)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = importer.textureType != TextureImporterType.Default ||
                importer.npotScale != TextureImporterNPOTScale.None || !importer.sRGBTexture ||
                !importer.mipmapEnabled || importer.alphaIsTransparency != cutEnd ||
                importer.wrapMode != (cutEnd ? TextureWrapMode.Clamp : TextureWrapMode.Repeat) ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!dirty) return;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = cutEnd;
            importer.wrapMode = cutEnd ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static Material MakeMaterial(string path, Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = Color.white;
            material.mainTexture = texture;
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Glossiness", 0.08f);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void ApplyToLog(RollingLog log)
        {
            if (visualMesh == null && !PrepareAssets()) return;
            MeshFilter filter = log.GetComponent<MeshFilter>();
            Renderer renderer = log.GetComponent<Renderer>();
            Undo.RecordObject(filter, "Apply painted log mesh");
            Undo.RecordObject(renderer, "Apply painted log materials");
            filter.sharedMesh = visualMesh;
            renderer.sharedMaterials = new[] { barkMaterial, endMaterial };
            foreach (string name in new[] { "End grain (visual)", "Rotation tick" })
            {
                Transform old = log.transform.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            }
            LineRenderer line = log.rim as LineRenderer;
            if (line != null)
            {
                Undo.RecordObject(line, "Move log highlight to outer edge");
                for (int i = 0; i < line.positionCount; i++)
                {
                    float angle = i * Mathf.PI * 2f / line.positionCount;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.485f, Mathf.Sin(angle) * 0.485f, -1.343f));
                }
                line.enabled = false;
            }
            EditorUtility.SetDirty(filter);
            EditorUtility.SetDirty(renderer);
        }

        private static Mesh MakeVisualCylinder()
        {
            const int segments = 48;
            const float radius = 0.5f, half = 1.325f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> sides = new List<int>(), ends = new List<int>();
            // Duplicate the seam's vertices so UVs wrap from 1 back to 0 without stretching a face.
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector3 normal = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices.Add(normal * radius + Vector3.back * half);
                vertices.Add(normal * radius + Vector3.forward * half);
                normals.Add(normal); normals.Add(normal);
                uvs.Add(new Vector2((float)i / segments, 0f));
                uvs.Add(new Vector2((float)i / segments, 1f));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2, b = (i + 1) * 2;
                sides.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            }
            for (int side = 0; side < 2; side++)
            {
                Vector3 normal = side == 0 ? Vector3.back : Vector3.forward;
                int center = vertices.Count;
                vertices.Add(normal * half); normals.Add(normal); uvs.Add(Vector2.one * 0.5f);
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(angle), y = Mathf.Sin(angle);
                    vertices.Add(new Vector3(x * radius, y * radius, normal.z * half));
                    normals.Add(normal);
                    uvs.Add(new Vector2(0.5f + x * 0.5f * (side == 0 ? 1f : -1f), 0.5f + y * 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = center + 1 + i, b = center + 1 + (i + 1) % segments;
                    ends.AddRange(side == 0 ? new[] { center, b, a } : new[] { center, a, b });
                }
            }
            Mesh mesh = new Mesh { name = "Painted log - side unwrap and circular ends", subMeshCount = 2 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs);
            mesh.SetTriangles(sides, 0); mesh.SetTriangles(ends, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void Capture(Camera camera, string path)
        {
            RenderTexture previous = RenderTexture.active, oldTarget = camera.targetTexture;
            RenderTexture rt = new RenderTexture(1280, 720, 24) { antiAliasing = 4 };
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }
        }

        public static void ApplyAndBuild()
        {
            Directory.CreateDirectory("Documentation/Backups");
            string backup = "Documentation/Backups/RollingDeliveryBeforeLogTextures.unity";
            if (!File.Exists(backup)) File.Copy(GameSceneBuilder.ScenePath, backup);
            ApplyPaintedTextures();
            // Save the live scene, including its existing edits, instead of replacing it from disk.
            Scene scene = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            if (File.Exists(Pending)) File.Delete(Pending);
            GameSceneBuilder.BuildWindows();
            string result = "PASS: Rolling logs use the user's painted bark and circular end faces. " +
                "Existing scene edits saved; original scene backed up under Documentation/Backups. " +
                "Physics meshes and worker model preserved. Windows build updated.";
            File.WriteAllText("Logs/LogArtUpdate.txt", result);
            Debug.Log("OLDTECH: " + result);
        }
    }
}
