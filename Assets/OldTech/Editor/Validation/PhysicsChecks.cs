using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldTech.Editor
{
    // Exercises the real scene, colliders, spring grip and motor against PhysX, without a test package.
    public static class PhysicsChecks
    {
        private const float Step = 1f / 90f;
        private static CargoMotor cargo;
        private static LogGrabber grabber;
        private static RollingLog[] logs;
        private static readonly List<string> results = new List<string>();

        [MenuItem("Old Tech/Run physics smoke checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode before checking physics.");
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty) EditorSceneManager.SaveScene(activeScene);
            SimulationMode previousMode = Physics.simulationMode;
            Exception failure = null;
            try
            {
                results.Clear();
                Physics.simulationMode = SimulationMode.Script;
                OpenFresh();
                Require(logs.Length == 3, "Delivery starts with exactly three logs");
                float startY = cargo.Body.position.y;
                Simulate(180, false);
                Require(Mathf.Abs(cargo.Body.position.x) < 0.2f, "Initial load stays in place");
                Require(Mathf.Abs(cargo.Body.position.y - startY) < 0.12f, "Pallet rests on the cylinders without sinking");
                Require(Mathf.Abs(Mathf.DeltaAngle(0f, cargo.Body.rotation.eulerAngles.z)) < 3f, "Initial stack stays upright");

                float startX = cargo.Body.position.x;
                float maxSpin = 0f;
                for (int i = 0; i < 100; i++)
                {
                    Simulate(1, true);
                    foreach (RollingLog log in logs) maxSpin = Mathf.Max(maxSpin, Mathf.Abs(log.Body.angularVelocity.z));
                }
                float pushedDistance = cargo.Body.position.x - startX;
                float pushedSpeed = cargo.Body.velocity.x;
                Require(pushedDistance > 0.55f, "Space motor advances the cargo: " + pushedDistance.ToString("0.00") + "m");
                Require(maxSpin > 0.4f, "Cylinders actually rotate: " + maxSpin.ToString("0.00") + "rad/s");
                Require(pushedSpeed < 3.1f, "Push speed stays controlled: " + pushedSpeed.ToString("0.00") + "m/s");
                float beforeCoast = cargo.Body.position.x;
                Simulate(45, false);
                Require(cargo.Body.position.x > beforeCoast + 0.05f, "Releasing Space preserves forward coast");
                Require(Mathf.Abs(cargo.Body.velocity.x) < pushedSpeed * 0.7f, "Worker braking gradually slows the load");

                OpenFresh();
                Simulate(90, false);
                RollingLog spare = logs[0];
                for (int i = 0; i < 500 && spare.Body.position.x >= cargo.Body.position.x - 2.1f; i++)
                    Simulate(1, true);
                Simulate(70, false);
                grabber.BeginGrab(spare, spare.Body.position);
                grabber.SetTarget(spare.Body.position + Vector3.left * 0.6f);
                Simulate(70, false);
                Vector3 lifted = spare.Body.position + Vector3.up * 2f;
                grabber.SetTarget(lifted);
                Simulate(150, false);
                Require(grabber.Held == spare, "Mouse spring holds an unloaded log without breaking");
                Require(Vector3.Distance(spare.Body.position, lifted) < 0.25f, "Mouse spring follows its target with bounded lag");
                grabber.Release();
                Simulate(180, false);
                Require(spare.Body.position.y > 0.45f && spare.Body.position.y < 0.59f,
                    "Released log lands on ground without tunneling: y=" + spare.Body.position.y.ToString("0.000"));

                OpenFresh();
                Simulate(90, false);
                RunFirstLeg();
                results.Add("PASS: Three-log recycling reaches the first checkpoint using the physical mouse spring.");
                RunSlopeCheck();
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/PhysicsChecks.txt", results);
                Debug.Log("OLDTECH: All physics checks passed.\n" + string.Join("\n", results));
            }
            catch (Exception ex)
            {
                failure = ex;
                results.Add("FAIL: " + ex.Message);
                Directory.CreateDirectory("Logs");
                File.WriteAllLines("Logs/PhysicsChecks.txt", results);
                Debug.LogError("OLDTECH physics check failed: " + ex);
            }
            finally
            {
                Physics.simulationMode = previousMode;
                EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
                AssetDatabase.SaveAssets();
            }
            if (failure != null)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw new InvalidOperationException("Physics smoke checks failed", failure);
            }
        }

        public static void RebuildAndRun()
        {
            GameSceneBuilder.BuildScene();
            Run();
        }

        public static void ValidateAndBuild()
        {
            RebuildAndRun();
            GameSceneBuilder.BuildWindows();
        }

        private static void OpenFresh()
        {
            EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            cargo = UnityEngine.Object.FindObjectOfType<CargoMotor>();
            grabber = UnityEngine.Object.FindObjectOfType<LogGrabber>();
            logs = Array.FindAll(UnityEngine.Object.FindObjectsOfType<RollingLog>(), log => !log.anchored && log.GetComponent<LogPickup>() == null);
            Array.Sort(logs, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            foreach (RollingLog log in logs) log.ApplyTuning();
            RobotCargo robot = UnityEngine.Object.FindObjectOfType<RobotCargo>();
            if (robot != null)
            {
                robot.SendMessage("Awake");
                robot.SendMessage("Start");
            }
            cargo.TickMotor(false, Step);
            Physics.SyncTransforms();
        }

        private static void Simulate(int ticks, bool push)
        {
            for (int i = 0; i < ticks; i++)
            {
                cargo.TickMotor(push, Step);
                grabber.TickGrip(Step);
                Physics.Simulate(Step);
                if (float.IsNaN(cargo.Body.position.x) || cargo.Body.position.y < -2f)
                    throw new Exception("Cargo left the physical track");
            }
        }

        private static void RunFirstLeg()
        {
            int recycles = 0;
            int ticksWithoutRecycle = 0;
            float lastX = cargo.Body.position.x;
            for (int tick = 0; tick < 40000; tick++)
            {
                if (cargo.Body.position.x >= 9f)
                {
                    results.Add("PASS: First checkpoint reached at x=" + cargo.Body.position.x.ToString("0.00") +
                        "m with " + recycles + " log recycles.");
                    return;
                }
                RollingLog rear = null;
                RollingLog front = null;
                foreach (RollingLog log in logs)
                {
                    if (rear == null || log.Body.position.x < rear.Body.position.x) rear = log;
                    if (front == null || log.Body.position.x > front.Body.position.x) front = log;
                }

                bool catchRunaway = front.Body.position.x > cargo.Body.position.x + 4.8f;
                if (rear.Body.position.x < cargo.Body.position.x - 2.1f || catchRunaway)
                {
                    Simulate(75, false);
                    RollingLog selected = catchRunaway ? front : rear;
                    grabber.BeginGrab(selected, selected.Body.position);
                    if (!catchRunaway)
                    {
                        grabber.SetTarget(selected.Body.position + Vector3.left * 0.65f);
                        Simulate(65, false);
                    }
                    float safeHeight = Mathf.Min(6.45f, cargo.Body.position.y + 4.0f);
                    grabber.SetTarget(new Vector3(selected.Body.position.x, safeHeight, 0f));
                    Simulate(105, false);
                    float dropX = cargo.Body.position.x + 2.95f;
                    grabber.SetTarget(new Vector3(dropX, safeHeight, 0f));
                    Simulate(135, false);
                    grabber.SetTarget(new Vector3(dropX, RouteGeometry.HeightAt(dropX) + 0.55f, 0f));
                    Simulate(120, false);
                    float insertX = cargo.Body.position.x + 1.95f;
                    grabber.SetTarget(new Vector3(insertX, RouteGeometry.HeightAt(insertX) + 0.55f, 0f));
                    Simulate(80, false);
                    RequireSilent(grabber.Held == selected, "Transport grip broke at x=" + cargo.Body.position.x);
                    grabber.Release();
                    Simulate(50, true);
                    recycles++;
                    ticksWithoutRecycle = 0;
                    lastX = cargo.Body.position.x;
                }
                else
                {
                    Simulate(1, true);
                    ticksWithoutRecycle++;
                    if (ticksWithoutRecycle > 2200 && cargo.Body.position.x < lastX + 0.3f)
                        throw new Exception("Haul stalled at x=" + cargo.Body.position.x.ToString("0.00") +
                            "; cargo=" + cargo.Body.position + "; angle=" + cargo.Body.rotation.eulerAngles.z +
                            "; support=" + cargo.HasSupport + "; speed=" + cargo.Body.velocity + "; log positions=" + string.Join(",", Array.ConvertAll(logs,
                                log => log.Body.position.ToString("F2"))));
                }
            }
            throw new Exception("First delivery leg did not finish within the simulation limit; x=" + cargo.Body.position.x);
        }

        private static void RunSlopeCheck()
        {
            OpenFresh();
            const float x = 16f;
            float slope = (RouteGeometry.HeightAt(x + 0.1f) - RouteGeometry.HeightAt(x - 0.1f)) / 0.2f;
            float angle = Mathf.Atan(slope) * Mathf.Rad2Deg;
            RobotCargo robot = UnityEngine.Object.FindObjectOfType<RobotCargo>();
            Vector3 robotOffset = robot != null ? robot.Body.position - cargo.Body.position : Vector3.zero;
            cargo.Body.position = new Vector3(x, RouteGeometry.HeightAt(x) + 1.235f, 0f);
            cargo.Body.rotation = Quaternion.Euler(0f, 0f, angle);
            if (robot != null)
            {
                robot.Body.position = cargo.Body.position + cargo.Body.rotation * robotOffset;
                robot.Body.rotation = cargo.Body.rotation;
                robot.Body.velocity = Vector3.zero;
                robot.Body.angularVelocity = Vector3.zero;
            }
            for (int i = 0; i < logs.Length; i++)
            {
                float logX = x + (i - 1) * 1.5f;
                logs[i].Body.position = new Vector3(logX, RouteGeometry.HeightAt(logX) + 0.508f, 0f);
            }
            Physics.SyncTransforms();
            Simulate(90, false);
            float before = cargo.Body.position.x;
            Simulate(90, true);
            Require(cargo.Body.position.x > before + 0.2f, "Three-log cargo can advance up the existing slope");
            Require(cargo.Body.position.y > RouteGeometry.HeightAt(cargo.Body.position.x) + 0.9f,
                "Slope traversal keeps the pallet supported above the ground");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            results.Add("PASS: " + message);
        }

        private static void RequireSilent(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
