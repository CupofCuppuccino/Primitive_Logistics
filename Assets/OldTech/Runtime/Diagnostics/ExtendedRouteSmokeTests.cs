#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OldTech
{
    public sealed class ExtendedRouteSmokeTests : MonoBehaviour
    {
        private string output, failure;
        private readonly List<string> results = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-oldTechExtended");
            if (index < 0 || index + 1 >= args.Length) return;
            var test = new GameObject("ExtendedRouteSmokeTests").AddComponent<ExtendedRouteSmokeTests>();
            test.output = args[index + 1];
            DontDestroyOnLoad(test.gameObject);
        }

        private void OnEnable() { Application.logMessageReceived += TrackErrors; }
        private void OnDisable() { Application.logMessageReceived -= TrackErrors; }
        private void TrackErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failure = message + "\n" + trace;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            Application.targetFrameRate = 90;
            yield return new WaitForSecondsRealtime(0.4f);
            MusicPlayer menuMusic = FindObjectOfType<MusicPlayer>();
            if (!Check(menuMusic != null && menuMusic.playlist.Length == 1 && menuMusic.CurrentClip.name == "MenuStrings",
                "Menu uses the strings track")) yield break;
            GameProgress.ClearSmokeSave();
            GameScenes.StartNewDelivery();
            float timeout = Time.unscaledTime + 10f;
            HaulSession session = null;
            while (Time.unscaledTime < timeout)
            {
                session = FindObjectOfType<HaulSession>();
                if (session != null && session.Ready) break;
                yield return null;
            }
            if (!Check(session != null && session.Ready, "Extended delivery initializes")) yield break;
            session.cargo.externalControl = session.grabber.externalControl = true;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-oldTechStoneOnly") >= 0)
            {
                yield return CheckStoneSound(session);
                GameProgress.ClearSmokeSave();
                if (failure == null) Finish();
                yield break;
            }
            yield return new WaitForSeconds(1.8f);
            if (!Check(session.Checkpoints.OwnedLogCount == 3 && session.Checkpoints.AllLogs.Length == 4 &&
                session.Checkpoints.AllLogs[3].GetComponent<Rigidbody>().isKinematic,
                "Start owns three logs with a fourth waiting on the route")) yield break;
            if (!Check(session.robot != null && session.robot.transform.parent != session.cargo.transform &&
                session.robot.Body != session.cargo.Body && !session.robot.Body.isKinematic &&
                (session.robot.Body.constraints & RigidbodyConstraints.FreezeRotationZ) == 0,
                "Robot has an independent dynamic body and can tip")) yield break;
            if (!Check(session.robot.IsOnPallet && session.Impacts == 0, "Robot stands stably on the pallet without spawn damage")) yield break;
            Capture("Start.png");
            MusicPlayer music = FindObjectOfType<MusicPlayer>();
            if (!Check(FindObjectsOfType<MusicPlayer>().Length == 1 && music.playlist.Length == 2 &&
                music.playlist[0].name == "GameplayPart1" && music.playlist[1].name == "GameplayPart2",
                "Game uses Part 1 then Part 2, with no menu music overlap")) yield break;
            AudioClip[] realPlaylist = music.playlist;
            AudioClip a = AudioClip.Create("ScheduleCheckA", 15435, 1, 44100, false);
            AudioClip b = AudioClip.Create("ScheduleCheckB", 15435, 1, 44100, false);
            music.playlist = new[] { a, b };
            music.RestartPlaylist();
            timeout = Time.unscaledTime + 3f;
            while (music.SwitchCount < 1 && Time.unscaledTime < timeout) yield return null;
            if (!Check(music.CurrentTrackIndex == 1 && music.SwitchCount == 1, "DSP playlist advances from first to second track")) yield break;
            while (music.SwitchCount < 2 && Time.unscaledTime < timeout) yield return null;
            if (!Check(music.CurrentTrackIndex == 0 && music.SwitchCount >= 2, "DSP playlist loops from second back to first")) yield break;
            music.playlist = realPlaylist;
            music.RestartPlaylist();
            Destroy(a); Destroy(b);

            float startX = session.cargo.Body.position.x;
            session.cargo.externalPush = true;
            yield return new WaitForSeconds(1.1f);
            session.cargo.externalPush = false;
            if (!Check(session.cargo.Body.position.x > startX + 0.5f && session.robot.IsOnPallet,
                "Normal pushing carries the loose robot")) yield break;

            // Isolate the bodies so a pallet-only impact cannot indirectly bump the robot.
            session.enabled = false;
            session.robot.Body.isKinematic = true;
            session.robot.transform.position = new Vector3(20f, 4f, 0f);
            int beforeHits = session.Impacts;
            CheckpointController.Teleport(session.cargo.Body, new Vector3(0f, 4f, 0f));
            session.cargo.Body.velocity = Vector3.down * 10f;
            foreach (RollingLog log in session.Checkpoints.Logs) CheckpointController.Teleport(log.Body, new Vector3(-10f, 0.51f, 0f));
            yield return new WaitForSeconds(0.9f);
            if (!Check(session.Impacts == beforeHits, "A hard pallet-only collision does not damage the robot")) yield break;
            session.cargo.Body.isKinematic = true;
            CheckpointController.Teleport(session.robot.Body, new Vector3(0f, 5f, 0f));
            session.robot.IgnoreImpactsFor(0f);
            session.robot.Body.velocity = Vector3.down * 10f;
            // Keep the parked pallet away from the falling robot.
            session.cargo.transform.position = new Vector3(10f, 1.235f, 0f);
            yield return new WaitForSeconds(1f);
            if (!Check(session.Impacts > beforeHits, "A hard robot collision reduces robot condition")) yield break;
            session.enabled = true;
            session.RestoreCheckpoint();
            yield return new WaitForSeconds(0.3f);
            session.enabled = false;
            session.cargo.Body.isKinematic = true;
            session.robot.Body.velocity = Vector3.right * 20f;
            yield return new WaitForSeconds(0.8f);
            Capture("LooseRobot.png");
            if (!Check(session.robot.IsOnPallet && !session.robot.Body.isKinematic, "Pallet edges catch the sliding loose robot: relative COM " +
                session.cargo.transform.InverseTransformPoint(session.robot.Body.worldCenterOfMass) + ", velocity " + session.robot.Body.velocity)) yield break;
            session.enabled = true;
            session.RestoreCheckpoint();
            if (!Check(session.robot.transform.parent == null && session.robot.Body.velocity.sqrMagnitude < 0.001f &&
                session.cargo.Body.velocity.sqrMagnitude < 0.001f, "Reset recovers the independent robot without residual momentum")) yield break;

            PlaceAt(session, 9.05f);
            yield return new WaitForSeconds(0.2f);
            PlaceAt(session, 23.55f);
            yield return new WaitForSeconds(0.2f);
            if (!Check(session.Checkpoints.CurrentIndex == 2 && session.Checkpoints.RecoveryLogCount == 3,
                "Early checkpoints restore three logs")) yield break;
            // Approach while the spare is still ahead of the pallet, rather than pinning it underneath.
            PlaceAt(session, 25.5f);
            RollingLog fourth = session.Checkpoints.AllLogs[3];
            session.grabber.BeginGrab(fourth, fourth.Body.position);
            session.grabber.SetTarget(fourth.Body.position + Vector3.up * 1.5f);
            yield return new WaitForSeconds(0.8f);
            if (!Check(session.Checkpoints.OwnedLogCount == 4 && session.grabber.Held == fourth &&
                !fourth.Body.isKinematic && fourth.Body.position.y > 1f, "Roadside fourth log can be collected and lifted by the actual mouse spring")) yield break;
            session.RestoreCheckpoint();
            if (!Check(session.Checkpoints.OwnedLogCount == 3 && fourth.Body.isKinematic &&
                Mathf.Abs(fourth.Body.position.x - session.Settings.fourthLogX) < 0.05f,
                "Reset before checkpoint three restores three and respawns the roadside spare")) yield break;
            PlaceAt(session, 36.05f);
            yield return new WaitForSeconds(0.2f);
            if (!Check(session.Checkpoints.CurrentIndex == 3 && session.Checkpoints.RecoveryLogCount == 4,
                "Checkpoint three explicitly grants a four-log recovery budget")) yield break;
            foreach (RollingLog log in session.Checkpoints.AllLogs)
            {
                CheckpointController.Teleport(log.Body, new Vector3(-30f, -12f, 0f));
                log.Body.velocity = Vector3.right * 8f;
            }
            session.RestoreCheckpoint();
            if (!Check(RecoveredLogs(session, 4), "Reset recovers all four logs from previous areas and offscreen positions")) yield break;
            Capture("FourLogCheckpoint.png");

            for (int i = 3; i < session.Settings.checkpointPositions.Length; i++)
            {
                PlaceAt(session, session.Settings.checkpointPositions[i] + 0.05f);
                yield return new WaitForSeconds(0.2f);
                session.RestoreCheckpoint();
                if (!Check(session.Checkpoints.CurrentIndex == i + 1 && RecoveredLogs(session, 4),
                    "Checkpoint " + (i + 1) + " restores its own position and four logs")) yield break;
            }

            session.enabled = false;
            PlaceAt(session, 36f);
            if (!Check(!RouteGeometry.TryWalkingSurface(43.1f, out _), "Empty section of pit has no walkable bridge")) yield break;
            for (int i = 0; i < 4; i++)
            {
                float x = new[] { 42.9f, 44.1f, 46.9f, 48.1f }[i];
                CheckpointController.Teleport(session.Checkpoints.Logs[i].Body, new Vector3(x, -0.392f, 0f));
            }
            Physics.SyncTransforms();
            if (!Check(RouteGeometry.TryWalkingSurface(45f, out float bridgeHeight) && bridgeHeight > -0.18f,
                "Logs placed on the real pit floor create worker footing")) yield break;
            CheckpointController.Teleport(session.cargo.Body, new Vector3(45f, 0.335f, 0f));
            CheckpointController.Teleport(session.robot.Body, session.cargo.Body.position);
            session.cargo.ResetAfterRecovery();
            session.cargo.externalPush = true;
            yield return new WaitForSeconds(0.9f);
            session.cargo.externalPush = false;
            if (!Check(session.cargo.Body.position.x > 45.2f && session.cargo.Body.position.y > -0.2f,
                "Physical pallet can advance on the log-filled pit")) yield break;
            FindObjectOfType<HaulCamera>().Snap();
            yield return new WaitForSeconds(0.2f);
            Capture("PitBridge.png");

            // Exercise the steep segment using its real colliders and gravity, with four initial supports.
            float angle = Mathf.Atan2(3.8f, 5f) * Mathf.Rad2Deg;
            float slopeX = 64.3f;
            Quaternion slopeRotation = Quaternion.Euler(0f, 0f, angle);
            for (int i = 0; i < 4; i++)
            {
                float x = slopeX + (i - 1.5f) * 1.05f;
                CheckpointController.Teleport(session.Checkpoints.Logs[i].Body, new Vector3(x, RouteGeometry.HeightAt(x) + 0.635f, 0f));
            }
            CheckpointController.Teleport(session.cargo.Body, new Vector3(slopeX, RouteGeometry.HeightAt(slopeX) + 1.55f, 0f));
            session.cargo.Body.rotation = slopeRotation;
            session.cargo.transform.rotation = slopeRotation;
            CheckpointController.Teleport(session.robot.Body, session.cargo.Body.position);
            session.robot.Body.rotation = slopeRotation;
            session.robot.transform.rotation = slopeRotation;
            Physics.SyncTransforms();
            session.cargo.ResetAfterRecovery();
            session.cargo.externalPush = true;
            yield return new WaitForSeconds(0.85f);
            session.cargo.externalPush = false;
            if (!Check(session.cargo.Body.position.x > slopeX + 0.15f,
                "Worker can push a supported pallet up the 37-degree slope: x=" + session.cargo.Body.position.x.ToString("F2") +
                ", support=" + session.cargo.HasSupport + ", footing=" + session.cargo.WorkerHasFooting)) yield break;
            FindObjectOfType<HaulCamera>().Snap();
            yield return new WaitForSeconds(0.15f);
            Capture("SteepSlope.png");
            session.enabled = true;
            session.RestoreCheckpoint();
            yield return PuzzleChecks(session);
            if (failure != null) yield break;
            PlaceAt(session, session.Settings.finishX + 0.1f);
            yield return new WaitForSeconds(0.25f);
            if (!Check(session.Finished, "The extended route still reaches the delivery ending with its robot")) yield break;
            session.GetComponent<DeliveryEnding>().Skip();
            if (!Check(session.EndingComplete, "Independent robot is handled by the ending")) yield break;
            GameProgress.ClearSmokeSave();
            Finish();
        }

        private IEnumerator CheckStoneSound(HaulSession session)
        {
            session.Checkpoints.Restore(5);
            session.enabled = false;
            PlaceAt(session, 86f);
            CheckpointController.Teleport(session.cargo.Body, new Vector3(86f, 10f, 0f));
            session.cargo.Body.isKinematic = true;
            CheckpointController.Teleport(session.robot.Body, new Vector3(80f, 10f, 0f));
            session.robot.Body.isKinematic = true;
            foreach (RollingLog log in session.Checkpoints.Logs)
                CheckpointController.Teleport(log.Body, new Vector3(80f, 0.51f, 0f));
            RollingLog ram = session.Checkpoints.Logs[0];
            CheckpointController.Teleport(ram.Body, new Vector3(87.8f, 0.55f, 0f));
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            int before = AudioFeedback.PlayCount(Cue.StoneImpact);
            session.grabber.BeginGrab(ram, ram.Body.position);
            session.grabber.SetTarget(new Vector3(95.9f, 0.55f, 0f));
            float timeout = Time.unscaledTime + 14f;
            while (!session.Puzzles.boulder.Filled && Time.unscaledTime < timeout) yield return null;
            session.grabber.Release();
            if (!Check(session.Puzzles.boulder.Unlocked && session.Puzzles.boulder.Filled,
                "The actual dragged log pushes the stone into its pit")) yield break;
            if (!Check(AudioFeedback.PlayCount(Cue.StoneImpact) > before && AudioFeedback.GetClip(Cue.StoneImpact).name == "StoneImpact" &&
                AudioFeedback.GetClip(Cue.Drop).name == "LogLanding",
                "Stone landing uses the old hard impact; wood landing uses the louder trimmed quiet-wood sound")) yield break;
            int after = AudioFeedback.PlayCount(Cue.StoneImpact);
            session.Checkpoints.Restore(6);
            yield return new WaitForSeconds(0.5f);
            if (!Check(AudioFeedback.PlayCount(Cue.StoneImpact) == after,
                "Restoring a solved stone checkpoint does not play a phantom stone impact")) yield break;
        }

        private IEnumerator PuzzleChecks(HaulSession session)
        {
            PuzzleDirector puzzles = session.Puzzles;
            if (!Check(puzzles != null && puzzles.fixedLog.anchored && puzzles.fixedLog.Body.isKinematic &&
                session.Checkpoints.AllLogs.Length == 4, "Fixed pit log is physical scenery outside the portable recovery budget")) yield break;
            session.grabber.BeginGrab(puzzles.fixedLog, puzzles.fixedLog.Body.position);
            if (!Check(session.grabber.Held == null, "Fixed pit log cannot be picked up")) yield break;
            int edges = 0;
            foreach (Collider collider in session.cargo.GetComponentsInChildren<Collider>())
                if (collider.name.StartsWith("PalletEdge") && collider.attachedRigidbody == session.cargo.Body && !collider.isTrigger) edges++;
            if (!Check(edges == 2 && session.robot.Body.mass == 16f && session.robot.Body.centerOfMass.y < 0.8f,
                "Both edited pallet edges collide and the heavier robot uses a lower mass centre")) yield break;
            CheckpointMarker[] markers = FindObjectsOfType<CheckpointMarker>();
            bool signs = markers.Length == 10;
            foreach (CheckpointMarker marker in markers)
                signs &= marker.transform.Find("Board") != null && marker.transform.Find("Label") != null && marker.transform.Find("Spiral") != null;
            if (!Check(signs, "All nine checkpoints and the start share one board-and-spiral unit")) yield break;
            GameObject fill = GameObject.Find("TerrainFill");
            if (!Check(fill != null && fill.GetComponent<Collider>() == null && fill.GetComponent<MeshFilter>().sharedMesh.bounds.max.x >= 206f,
                "Ground colour fills the full course without introducing a hidden collider")) yield break;

            session.enabled = false;
            puzzles.RestoreAt(84f);
            if (!Check(!puzzles.boulder.Unlocked && puzzles.boulder.Body.isKinematic && !puzzles.CanAdvanceTo(108f),
                "Unmoved boulder blocks the later checkpoint")) yield break;
            PlaceAt(session, 86f);
            session.cargo.Body.isKinematic = true;
            CheckpointController.Teleport(session.cargo.Body, new Vector3(86f, 10f, 0f));
            session.cargo.Body.isKinematic = true;
            session.robot.Body.isKinematic = true;
            CheckpointController.Teleport(session.robot.Body, new Vector3(80f, 10f, 0f));
            session.robot.Body.isKinematic = true;
            foreach (RollingLog log in session.Checkpoints.Logs)
                CheckpointController.Teleport(log.Body, new Vector3(80f, 0.51f, 0f));
            RollingLog ram = session.Checkpoints.Logs[0];
            CheckpointController.Teleport(ram.Body, new Vector3(87.8f, 0.55f, 0f));
            Physics.SyncTransforms();
            session.grabber.BeginGrab(ram, ram.Body.position);
            session.grabber.SetTarget(new Vector3(95.9f, 0.55f, 0f));
            float deadline = Time.unscaledTime + 14f;
            int stoneSounds = AudioFeedback.PlayCount(Cue.StoneImpact);
            while (!puzzles.boulder.Filled && Time.unscaledTime < deadline) yield return null;
            session.grabber.Release();
            if (!Check(puzzles.boulder.Unlocked && puzzles.boulder.Filled && RouteGeometry.TryWalkingSurface(97.7f, out _),
                "Actual mouse spring rams the boulder into the square pit: position=" + puzzles.boulder.Body.position)) yield break;
            if (!Check(AudioFeedback.PlayCount(Cue.StoneImpact) > stoneSounds && AudioFeedback.GetClip(Cue.StoneImpact).name == "StoneImpact",
                "The real boulder landing plays the former hard wooden impact as the stone sound")) yield break;
            FindObjectOfType<HaulCamera>().Snap();
            Capture("BoulderPit.png");
            puzzles.RestoreAt(84f);
            bool resetBefore = !puzzles.boulder.Filled && Vector3.Distance(puzzles.boulder.Body.position, puzzles.boulder.spawnPosition) < 0.01f;
            puzzles.RestoreAt(108f);
            if (!Check(resetBefore && puzzles.boulder.Filled && puzzles.CanAdvanceTo(108f),
                "Reset before the stone retries it; later checkpoints keep its bridge filled")) yield break;

            PlaceAt(session, 138f);
            session.cargo.Body.isKinematic = true;
            session.robot.IgnoreImpactsFor(20f);
            foreach (RollingLog log in session.Checkpoints.Logs)
                CheckpointController.Teleport(log.Body, new Vector3(130f, 0.51f, 0f));
            CheckpointController.Teleport(ram.Body, new Vector3(135.7f, 3.15f, 0f));
            session.grabber.BeginGrab(ram, ram.Body.position);
            session.grabber.SetTarget(new Vector3(140f, 3.15f, 0f));
            deadline = Time.unscaledTime + 4f;
            float tip = 0f;
            while (Time.unscaledTime < deadline)
            {
                tip = Mathf.Max(tip, Mathf.Abs(Mathf.DeltaAngle(0f, session.robot.Body.rotation.eulerAngles.z)));
                if (tip > 60f) break;
                yield return null;
            }
            session.grabber.Release();
            if (!Check(tip > 60f, "A dragged log can physically tip the heavier robot for the low arch: angle=" + tip)) yield break;
            session.robot.Body.isKinematic = true;
            CheckpointController.Teleport(session.robot.Body, new Vector3(146f, 1.6f, 0f));
            session.robot.Body.isKinematic = true;
            session.robot.Body.rotation = Quaternion.identity;
            session.robot.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            Collider roof = GameObject.Find("LowArchRoof").GetComponent<Collider>();
            bool uprightBlocked = RobotTouches(session.robot, roof);
            session.robot.Body.rotation = Quaternion.Euler(0f, 0f, 90f);
            session.robot.transform.rotation = session.robot.Body.rotation;
            Physics.SyncTransforms();
            Bounds bounds = new Bounds(session.robot.GetComponentsInChildren<Collider>()[0].bounds.center, Vector3.zero);
            foreach (Collider collider in session.robot.GetComponentsInChildren<Collider>()) bounds.Encapsulate(collider.bounds);
            Vector3 adjustment = new Vector3(146f - bounds.center.x, 1.6f - bounds.min.y, 0f);
            session.robot.Body.position += adjustment;
            session.robot.transform.position = session.robot.Body.position;
            Physics.SyncTransforms();
            if (!Check(uprightBlocked && !RobotTouches(session.robot, roof), "Real ceiling colliders block a standing robot and clear a horizontal robot")) yield break;
            FindObjectOfType<HaulCamera>().Snap();
            Capture("LowArch.png");

            PlaceAt(session, 174f);
            // The worker approaches the notch from its left side; the load can already span it.
            session.cargo.ResetAfterRecovery();
            CheckpointController.Teleport(session.cargo.Body, new Vector3(175f, 1.235f, 0f));
            session.cargo.Body.isKinematic = true;
            foreach (RollingLog log in session.Checkpoints.Logs)
                CheckpointController.Teleport(log.Body, new Vector3(160f, 0.51f, 0f));
            Physics.SyncTransforms();
            if (!Check(!RouteGeometry.TryWalkingSurface(171.55f, out _), "One-log notch cannot be crossed empty")) yield break;
            session.cargo.externalPush = true;
            yield return new WaitForSeconds(0.75f);
            session.cargo.externalPush = false;
            if (!Check(!session.cargo.WorkerHasFooting && session.cargo.WorkerX < 171.6f,
                "Worker stops at the open notch instead of skipping across it")) yield break;
            CheckpointController.Teleport(ram.Body, new Vector3(171.55f, -0.342f, 0f));
            yield return new WaitForSeconds(0.35f);
            if (!Check(RouteGeometry.TryWalkingSurface(171.55f, out float h) && h >= -0.18f,
                "One physical log fills the final notch and creates a usable step")) yield break;
            session.enabled = true;
            session.RestoreCheckpoint();
        }

        private static bool RobotTouches(RobotCargo robot, Collider obstacle)
        {
            foreach (Collider collider in robot.GetComponentsInChildren<Collider>())
                if (Physics.ComputePenetration(collider, collider.transform.position, collider.transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out _, out _)) return true;
            return false;
        }

        private static bool RecoveredLogs(HaulSession session, int count)
        {
            if (session.Checkpoints.OwnedLogCount != count) return false;
            foreach (RollingLog log in session.Checkpoints.Logs)
                if (!log.gameObject.activeInHierarchy || log.Body.isKinematic ||
                    Mathf.Abs(log.Body.position.x - session.cargo.Body.position.x) > 2f ||
                    log.Body.velocity.sqrMagnitude > 0.001f) return false;
            return true;
        }

        private static void PlaceAt(HaulSession session, float x)
        {
            session.grabber.Release();
            CheckpointController.Teleport(session.cargo.Body, new Vector3(x, RouteGeometry.HeightAt(x) + 1.235f, 0f));
            CheckpointController.Teleport(session.robot.Body, session.cargo.Body.position);
            session.robot.IgnoreImpactsFor(1.5f);
            for (int i = 0; i < session.Checkpoints.Logs.Length; i++)
            {
                float logX = x + (i - (session.Checkpoints.OwnedLogCount - 1) * 0.5f) * (session.Checkpoints.OwnedLogCount == 3 ? 1.5f : 1.25f);
                CheckpointController.Teleport(session.Checkpoints.Logs[i].Body, new Vector3(logX, RouteGeometry.HeightAt(logX) + 0.508f, 0f));
            }
            Physics.SyncTransforms();
            session.cargo.ResetAfterRecovery();
            if (session.Puzzles != null) session.Puzzles.RestoreAt(x);
            FindObjectOfType<HaulCamera>().Snap();
        }

        private bool Check(bool success, string message)
        {
            if (!success || failure != null)
            {
                Capture("Failure.png");
                results.Add("FAIL: " + message + (failure == null ? "" : "\n" + failure));
                failure = message;
                Finish();
                return false;
            }
            results.Add("PASS: " + message);
            return true;
        }

        private void Finish()
        {
            File.WriteAllLines(Path.Combine(output, "ExtendedRoute.txt"), results);
            Application.Quit(failure == null ? 0 : 1);
        }

        private void Capture(string file)
        {
            Camera camera = Camera.main;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(output, file), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                Destroy(image);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
