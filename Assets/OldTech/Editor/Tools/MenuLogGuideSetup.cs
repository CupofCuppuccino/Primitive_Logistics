using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class MenuLogGuideSetup
    {
        private const string MeshRoot = "Assets/OldTech/Art/Meshes/";
        private const string MaterialPath = "Assets/OldTech/Art/Materials/MenuLogGuide.mat";

        [MenuItem("Old Tech/Apply menu log transfer guide")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            Scene menu = SceneManager.GetSceneByPath(GameSceneBuilder.MenuScenePath);
            if (!menu.IsValid() || !menu.isLoaded) menu = EditorSceneManager.OpenScene(GameSceneBuilder.MenuScenePath);
            Transform[] items = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform target = items.Single(t => t.name == "RollingLog03 (1)");
            Transform source = items.Single(t => t.name == "RollingLog04");
            Camera view = menu.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>()).First(c => c.enabled);
            Directory.CreateDirectory(MeshRoot);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Sprites/Default")) { name = "MenuLogGuide", color = Color.white };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            // Hide the solid visual only; keep its mesh and exact artist-authored transform.
            target.GetComponent<MeshRenderer>().enabled = false;
            Transform old = target.Find("DashedLogGuide");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject ghost = new GameObject("DashedLogGuide");
            ghost.transform.SetParent(target, false);
            var outline = new Strokes(ghost.transform.InverseTransformDirection(view.transform.forward));
            Bounds bounds = target.GetComponent<MeshFilter>().sharedMesh.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) + 0.015f;
            float front = bounds.min.z - 0.015f, back = bounds.max.z + 0.015f;
            outline.Ring(radius, front, 16, GameGui.Paper);
            outline.Ring(radius, back, 16, GameGui.Paper);
            outline.Ring(radius * 0.52f, front, 10, GameGui.Paper);
            foreach (Vector2 end in new[] { Vector2.left * radius, Vector2.right * radius, Vector2.up * radius })
                for (int i = 0; i < 8; i++)
                    outline.Line(new Vector3(end.x, end.y, Mathf.Lerp(front, back, i / 8f)),
                        new Vector3(end.x, end.y, Mathf.Lerp(front, back, (i + 0.58f) / 8f)), 0.036f, GameGui.Paper);
            Assign(ghost, outline, "MenuGhostLog.asset", material);

            Transform world = items.Single(t => t.name == "MenuBackdropCheckpoint03");
            old = world.Find("LogTransferArrow");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            GameObject arrow = new GameObject("LogTransferArrow");
            arrow.transform.SetParent(world, false);
            var curve = new Strokes(arrow.transform.InverseTransformDirection(view.transform.forward));
            Vector3 start = arrow.transform.InverseTransformPoint(source.TransformPoint(new Vector3(radius + 0.12f, 0.1f, front - 0.05f)));
            Vector3 endPoint = arrow.transform.InverseTransformPoint(target.TransformPoint(new Vector3(radius + 0.12f, 0.17f, front - 0.05f)));
            Vector3 first = start + new Vector3(2.1f, 0.45f, 0f);
            Vector3 second = endPoint + new Vector3(2.3f, 0.65f, 0f);
            var path = new List<Vector3>();
            for (int i = 0; i <= 48; i++)
            {
                float t = i / 48f, a = 1f - t;
                path.Add(a * a * a * start + 3f * a * a * t * first + 3f * a * t * t * second + t * t * t * endPoint);
            }
            Vector3 direction = (endPoint - second).normalized;
            Vector3 side = Vector3.Cross(view.transform.forward, direction).normalized;
            foreach (float width in new[] { 0.095f, 0.045f })
            {
                Color color = width > 0.05f ? GameGui.Paper : GameGui.Red;
                for (int i = 1; i < path.Count; i++) curve.Line(path[i - 1], path[i], width, color);
                curve.Line(endPoint - direction * 0.32f + side * 0.18f, endPoint, width, color);
                curve.Line(endPoint - direction * 0.32f - side * 0.18f, endPoint, width, color);
            }
            Assign(arrow, curve, "MenuLogTransferArrow.asset", material);
            EditorUtility.SetDirty(target.GetComponent<MeshRenderer>());
            EditorSceneManager.MarkSceneDirty(menu);
            EditorSceneManager.SaveScene(menu);
            AssetDatabase.SaveAssets();
        }

        private static void Assign(GameObject obj, Strokes strokes, string filename, Material material)
        {
            string path = MeshRoot + filename;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = Path.GetFileNameWithoutExtension(filename) }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.SetVertices(strokes.vertices);
            mesh.SetColors(strokes.colors);
            mesh.SetTriangles(strokes.triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private sealed class Strokes
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Color> colors = new List<Color>();
            public readonly List<int> triangles = new List<int>();
            private readonly Vector3 viewNormal;
            public Strokes(Vector3 normal) { viewNormal = normal; }
            public void Line(Vector3 from, Vector3 to, float width, Color color)
            {
                Vector3 side = Vector3.Cross(viewNormal, to - from).normalized * width * 0.5f;
                int index = vertices.Count;
                vertices.AddRange(new[] { from - side, from + side, to + side, to - side });
                for (int i = 0; i < 4; i++) colors.Add(color);
                triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
            }
            public void Ring(float radius, float z, int dashes, Color color)
            {
                for (int dash = 0; dash < dashes; dash++)
                    for (int piece = 0; piece < 3; piece++)
                    {
                        float a = (dash + piece * 0.2f) / dashes * Mathf.PI * 2f;
                        float b = (dash + (piece + 1) * 0.2f) / dashes * Mathf.PI * 2f;
                        Line(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, z),
                            new Vector3(Mathf.Cos(b) * radius, Mathf.Sin(b) * radius, z), 0.036f, color);
                    }
            }
        }
    }
}
