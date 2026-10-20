using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldTech.Editor
{
    // Exercise the real spring and terrain contacts without touching player saves.
    public static class LogGrabberChecks
    {
        private const float Step = 1f / 90f;

        [MenuItem("Old Tech/Validate log dragging on edited terrain")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before checking log dragging.");
            var original = EditorSceneManager.GetActiveScene();
            if (original.isDirty) EditorSceneManager.SaveScene(original);
            string originalPath = original.path;
            SimulationMode previousMode = Physics.simulationMode;
            var results = new List<string>();
            LogGrabber grabber = null;
            try
            {
                EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
                Physics.simulationMode = SimulationMode.Script;
                HaulSession session = UnityEngine.Object.FindObjectOfType<HaulSession>();
                grabber = session.grabber;
                foreach (Rigidbody body in UnityEngine.Object.FindObjectsOfType<Rigidbody>())
                    body.isKinematic = true;
                session.cargo.SendMessage("Awake");
                RollingLog log = Array.Find(UnityEngine.Object.FindObjectsOfType<RollingLog>(),
                    item => !item.anchored && item.name == "RollingLog01");
                if (log == null) throw new InvalidOperationException("The delivery log is missing.");
                log.ApplyTuning();
                log.Body.isKinematic = false;
                Physics.SyncTransforms();

                float hillY = GroundHeight(119.5f);
                Require(hillY > 1f && hillY < 2f,
                    "Fixture uses checkpoint 06's actual lowered hill.", results);
                Place(session, log, 113.5f, 119.5f, hillY + 2.5f);
                grabber.BeginGrab(log, log.Body.position);
                grabber.SetTarget(new Vector3(119.5f, hillY + 0.65f, 0f));
                Simulate(grabber, 270);
                Require(grabber.Held == log && log.IsGrabbed &&
                    log.Body.position.y > hillY + 0.45f && log.Body.position.y < hillY + 0.9f,
                    "Held log descends to the lowered hill without releasing: y=" + log.Body.position.y, results);

                grabber.SetTarget(new Vector3(120.2f, hillY + 0.54f, 0f));
                Simulate(grabber, 180);
                Require(grabber.Held == log && Mathf.Abs(log.Body.position.x - 120.2f) < 0.15f &&
                    log.Body.position.y < hillY + 0.75f,
                    "Held log moves across the new hill at its actual height.", results);

                grabber.SetTarget(new Vector3(120.2f, hillY - 2f, 0f));
                Simulate(grabber, 180);
                Require(grabber.Held == log && log.Body.position.y >= hillY + 0.44f,
                    "Actual terrain stops a held log when the cursor points below ground.", results);

                grabber.SetTarget(new Vector3(119.5f, hillY + 2.5f, 0f));
                Simulate(grabber, 180);
                Require(log.Body.position.y > hillY + 1.8f,
                    "The spring still lifts the log from the lowered hill.", results);
                grabber.Release();
                Simulate(grabber, 270);
                Require(Mathf.Abs(log.Body.position.y - hillY - 0.5f) < 0.1f,
                    "Released log settles on the real hill without tunneling.", results);

                CheckLowSurface(session, log, 140f, 145f, "under the low arch", results);
                CheckLowSurface(session, log, 102f, 97.7f, "at the boulder pit floor", results);
                CheckLowSurface(session, log, 55f, 50f, "on flat ground", results);
                Debug.Log("OLDTECH_LOG_GRAB_CHECK_PASS: " + string.Join("\n", results));
            }
            catch (Exception exception)
            {
                results.Add("FAIL: " + exception.Message);
                throw;
            }
            finally
            {
                if (grabber != null) grabber.Release();
                Physics.simulationMode = previousMode;
                Directory.CreateDirectory("Logs/LogDragging");
                File.WriteAllLines("Logs/LogDragging/Validation.txt", results);
                if (!string.IsNullOrEmpty(originalPath)) EditorSceneManager.OpenScene(originalPath);
            }
        }

        private static void CheckLowSurface(HaulSession session, RollingLog log, float cargoX,
            float x, string description, List<string> results)
        {
            session.grabber.Release();
            float groundY = GroundHeight(x);
            Place(session, log, cargoX, x, groundY + 2f);
            session.grabber.BeginGrab(log, log.Body.position);
            session.grabber.SetTarget(new Vector3(x, groundY + 0.65f, 0f));
            Simulate(session.grabber, 270);
            Require(session.grabber.Held == log &&
                log.Body.position.y > groundY + 0.44f && log.Body.position.y < groundY + 0.9f,
                "Dragging remains usable " + description + ": y=" + log.Body.position.y, results);
        }

        private static void Place(HaulSession session, RollingLog log, float cargoX, float x, float y)
        {
            session.cargo.Body.position = new Vector3(cargoX, GroundHeight(cargoX) + 1.235f, 0f);
            log.Body.position = new Vector3(x, y, 0f);
            log.Body.rotation = Quaternion.identity;
            log.Body.velocity = Vector3.zero;
            log.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static float GroundHeight(float x)
        {
            if (!Physics.Raycast(new Vector3(x, 4f, 0f), Vector3.down, out RaycastHit hit,
                12f, 1 << 8, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("No ground at x=" + x);
            return hit.point.y;
        }

        private static void Simulate(LogGrabber grabber, int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                grabber.TickGrip(Step);
                Physics.Simulate(Step);
            }
        }

        private static void Require(bool condition, string message, List<string> results)
        {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS: " + message);
        }
    }
}
