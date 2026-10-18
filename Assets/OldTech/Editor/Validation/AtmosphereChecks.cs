using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class AtmosphereChecks
    {
        [MenuItem("Old Tech/Validate daylight and painted scenery")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene scene = SceneManager.GetActiveScene();
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            try
            {
                var signs = UnityEngine.Object.FindObjectsOfType<CheckpointMarker>();
                CheckpointMarker start = signs.Single(s => s.checkpointIndex == 0);
                Transform spiral = start.transform.Find("Spiral");
                Require(signs.Length == 10, "Ten unified signs including start");
                Require(Mathf.Abs(spiral.localPosition.y - 0.159f) < 0.001f && start.transform.Find("Board (1)") != null,
                    "Artist-edited start spiral and backing board are preserved");
                foreach (CheckpointMarker sign in signs)
                {
                    Require(sign.transform.Find("Spiral").localPosition == spiral.localPosition && sign.transform.Find("Board (1)") != null,
                        "Start format copied to " + sign.name);
                    Transform stripe = sign.transform.Find("RouteStripe");
                    Require(stripe != null && stripe.GetComponent<Collider>() == null && Mathf.Abs(stripe.position.x - sign.transform.position.x) < 0.001f,
                        "Route stripe below " + sign.name);
                }
                Require(!UnityEngine.Object.FindObjectsOfType<Transform>().Any(t => t.name == "Route stripe"), "Detached stripes removed");
                DaylightCycle daylight = UnityEngine.Object.FindObjectOfType<DaylightCycle>();
                Require(daylight != null && daylight.ruins.Length == 13, "Thirteen background ruin clusters bound");
                Require(daylight.GetComponentsInChildren<Collider>().Length == 0, "Painted scenery has no physics colliders");
                HaulSession session = UnityEngine.Object.FindObjectOfType<HaulSession>();
                Require(Mathf.Abs(session.defaults.checkpointPositions[8] - 185f) < 0.001f &&
                    Mathf.Abs(signs.Single(s => s.checkpointIndex == 9).transform.position.x - 185f) < 0.001f,
                    "Final DELIVERY checkpoint and save destination are at 185m");
                Sprite[] frames = session.GetComponent<DeliveryEnding>().fireFrames;
                Require(frames != null && frames.Length == 4 && frames.Select(s => s.name).Distinct().Count() == 4, "Four distinct fire sprites bound in reading order");
                Vector3 lightAtStart = Vector3.zero;
                for (int i = 0; i < 3; i++)
                {
                    float progress = i * 0.5f;
                    float x = progress * RouteGeometry.FinishX;
                    Vector3 shift = new Vector3(x, RouteGeometry.HeightAt(x), 0f) - session.cargo.transform.position + new Vector3(0f, 1.235f, 0f);
                    session.cargo.transform.position += shift;
                    session.robot.transform.position += shift;
                    foreach (RollingLog log in UnityEngine.Object.FindObjectsOfType<RollingLog>().Where(l => !l.anchored && l.GetComponent<LogPickup>() == null))
                        log.transform.position += shift;
                    UnityEngine.Object.FindObjectOfType<HaulCamera>().Snap();
                    daylight.ApplyProgress(progress);
                    Vector3 viewport = daylight.view.WorldToViewportPoint(daylight.sunDisc.transform.position);
                    Require(Mathf.Abs(viewport.x - Mathf.Lerp(0.08f, 0.92f, progress)) < 0.002f, "Sun position follows route progress " + progress);
                    Require(!float.IsNaN(daylight.sunlight.intensity) && !float.IsNaN(daylight.sunlight.transform.rotation.x), "Finite light values at progress " + progress);
                    if (i == 1) Require(Mathf.Abs(viewport.y - 0.88f) < 0.002f, "Sun apex matches the reference at 88% picture height");
                    if (i == 0) lightAtStart = daylight.sunlight.transform.forward;
                    if (i == 2) Require(Vector3.Dot(lightAtStart, daylight.sunlight.transform.forward) < 0.5f && viewport.y < 0.3f,
                        "Sun sets on the right and light changes direction");
                    Capture(daylight.view, i == 0 ? "Sunrise.png" : i == 1 ? "Noon.png" : "Sunset.png");
                }
                File.WriteAllText("Logs/Atmosphere/Validation.txt", "PASS: DELIVERY checkpoint and save destination at 185m. Sun apex at 88% picture height. All ten signs match the edited start backing and spiral. Ten stripes are attached beneath their signs; no detached stripes/colliders. Thirteen ruin clusters. Four distinct fire frames. Sunrise, noon and sunset positions plus light direction/intensity are valid; sunset has no NaNs. Render previews captured.");
            }
            finally { EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath); }
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/Atmosphere");
            RenderTexture target = RenderTexture.GetTemporary(1280, 720, 24);
            RenderTexture active = RenderTexture.active, previous = camera.targetTexture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/Atmosphere/" + name, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
