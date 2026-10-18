#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech
{
    public sealed class GameFlowSmokeTests : MonoBehaviour
    {
        private static bool launched;
        private static string output;
        private bool resumeOnly;
        private string failure;
        private readonly List<string> results = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LaunchIfRequested()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-oldTechSmoke");
            if (launched || index < 0 || index + 1 >= args.Length) return;
            launched = true;
            output = args[index + 1];
            new GameObject("GameFlowSmokeTests").AddComponent<GameFlowSmokeTests>();
        }

        private void OnEnable() { Application.logMessageReceived += TrackErrors; }
        private void OnDisable() { Application.logMessageReceived -= TrackErrors; }

        private void TrackErrors(string condition, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                failure = condition + "\n" + trace;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(output);
            resumeOnly = Array.IndexOf(Environment.GetCommandLineArgs(), "-oldTechResumeOnly") >= 0;
            Application.targetFrameRate = 90;
            yield return new WaitForSecondsRealtime(0.5f);
            if (!Check(SceneManager.GetActiveScene().name == GameScenes.MainMenu &&
                FindObjectOfType<MainMenuController>() != null, "Player opens on the main menu")) yield break;
            CaptureScene("MenuBackdrop.png");
            if (!Check(GameGui.Artwork != null && GameGui.Artwork.Complete, "All fifteen editable UI PNGs load in the player")) yield break;
            if (!Check(AudioFeedback.Library != null && Enum.GetValues(typeof(Cue)).Cast<Cue>().All(c => AudioFeedback.GetClip(c) != null && AudioFeedback.GetClip(c).samples > 0),
                "All supplied sound effects load in the player, including both loops")) yield break;
            AudioFeedback.Play(Cue.Select);
            if (!Check(AudioFeedback.PlayCount(Cue.Select) == 1 && AudioFeedback.GetClip(Cue.Select).name == "UiWoodButton",
                "Menu button audio plays the supplied wooden button clip")) yield break;
            if (!Check(!DeveloperMode.IsUnlocked &&
                !FindObjectOfType<MainMenuController>().TryUnlockDeveloperMode("ludumdare60") && !DeveloperMode.IsUnlocked,
                "Developer mode starts locked and rejects incorrect password/case")) yield break;
            yield return CaptureInterface("MenuUi.png");

            if (resumeOnly)
            {
                if (!Check(GameProgress.TryLoad(out DeliverySave persisted) && persisted.checkpointIndex == 2 &&
                    !persisted.completed, "Checkpoint survives a full player-process restart")) yield break;
                GameScenes.ContinueDelivery();
                yield return WaitForSession();
                HaulSession resumed = FindObjectOfType<HaulSession>();
                if (!Check(resumed != null && resumed.Ready && resumed.Checkpoints.CurrentIndex == 2 &&
                    Mathf.Abs(resumed.cargo.Body.position.x - 23.5f) < 0.12f && LogsUnderCargo(resumed),
                    "Continue restores checkpoint two and all three logs beneath the cargo")) yield break;
                resumed.cargo.externalControl = true;
                PlaceAt(resumed, resumed.Settings.finishX + 0.1f);
                yield return new WaitForSeconds(0.25f);
                if (!Check(resumed.Finished, "Delivery reaches its ending")) yield break;
                if (!Check(AudioFeedback.PlayCount(Cue.Complete) == 1 && AudioFeedback.GetClip(Cue.Complete).name == "AchievementSatisfied",
                    "The delivery result popup plays the supplied achievement sound once")) yield break;
                yield return new WaitForSeconds(1.1f);
                CaptureScene("EndingDelivery.png");
                yield return new WaitForSeconds(3.25f);
                CaptureScene("EndingCarry.png");
                float deadline = Time.unscaledTime + 18f;
                while (!resumed.GetComponent<DeliveryEnding>().RobotBurned && Time.unscaledTime < deadline) yield return null;
                yield return new WaitForSeconds(0.15f);
                SoundLoop fireAudio = FindObjectsOfType<SoundLoop>().SingleOrDefault(s => s.Cue == Cue.Fire);
                if (!Check(fireAudio != null && fireAudio.Source.clip.name == "FireCrackle" && fireAudio.StartCount > 0 &&
                    Mathf.Approximately(fireAudio.TargetLevel, 1f) && AudioFeedback.PlayCount(Cue.FireFeed) == 1,
                    "Fire crackle loops and the supplied fire boost plays once as the flames grow")) yield break;
                CaptureScene("EndingFire.png");
                SpriteFlipbook fire = FindObjectOfType<SpriteFlipbook>();
                var animatedFrames = new HashSet<int>();
                for (int frame = 0; frame < 8; frame++)
                {
                    if (fire != null) animatedFrames.Add(fire.CurrentFrame);
                    yield return new WaitForSeconds(0.12f);
                }
                if (!Check(fire != null && fire.frames.Length == 4 && animatedFrames.Count == 4 &&
                    fire.GetComponent<SpriteRenderer>() != null && fire.GetComponentsInChildren<MeshRenderer>().Length == 0 &&
                    fire.GetComponentInChildren<Light>() != null,
                    "Ending uses all four painted fire frames and a warm light, with no sphere flames")) yield break;
                bool orderedFire = true;
                for (int frame = 0; frame < 4; frame++)
                    orderedFire &= fire.frames[frame].name == "fire_frame" + frame.ToString("0000");
                if (!Check(orderedFire, "Ending binds the new fire_frame0000 through fire_frame0003 in order")) yield break;
                while (!resumed.EndingComplete && Time.unscaledTime < deadline) yield return null;
                if (!Check(resumed.EndingComplete && resumed.GetComponent<DeliveryEnding>().RobotBurned &&
                    !GameProgress.HasActiveSave && resumed.Checkpoints.Logs[1].Body.position.x > 35f &&
                    Camera.main.orthographicSize < 2f,
                    "Ending burns the robot, features the delivered log, and completes the saved run")) yield break;
                CaptureScene("EndingLogCloseup.png");
                yield return CaptureInterface("ResultsUi.png");
                resumed.ReturnToMenu();
                yield return WaitForMenu();
                if (!Check(FindObjectOfType<MainMenuController>() != null, "Ending returns to the menu")) yield break;
                if (!Check(!AudioFeedback.GameplayAudioPlaying && FindObjectsOfType<SoundLoop>().Length == 0,
                    "Returning to the menu stops the fire, rolling, and remaining gameplay audio tails")) yield break;
                GameScenes.StartNewDelivery();
                yield return WaitForSession();
                HaulSession fresh = FindObjectOfType<HaulSession>();
                if (!Check(fresh != null && fresh.Ready && fresh.Checkpoints.CurrentIndex == 0 &&
                    fresh.Impacts == 0 && LogsUnderCargo(fresh), "New Delivery clears prior progress and starts with three logs")) yield break;
                GameProgress.ClearSmokeSave();
                Finish(true);
                yield break;
            }

            GameProgress.ClearSmokeSave();
            GameScenes.StartNewDelivery();
            yield return WaitForSession();
            HaulSession session = FindObjectOfType<HaulSession>();
            if (!Check(session != null && session.Ready && session.Checkpoints.OwnedLogCount == 3 &&
                session.Checkpoints.CurrentIndex == 0 && LogsUnderCargo(session), "New game has exactly three logs supporting the cargo")) yield break;
            session.JumpToCheckpoint(3);
            session.ToggleTuning();
            if (!Check(session.Checkpoints.CurrentIndex == 0 && !session.TuningOpen && Time.timeScale == 1f,
                "Locked mode rejects both checkpoint jumps and physics settings")) yield break;
            session.TogglePause();
            session.ToggleTuning();
            if (!Check(session.IsPaused && !session.TuningOpen && Time.timeScale == 0f,
                "Locked physics settings cannot bypass the pause menu")) yield break;
            session.TogglePause();
            session.ReturnToMenu();
            yield return WaitForMenu();
            if (!Check(FindObjectOfType<MainMenuController>().TryUnlockDeveloperMode("LudumDare60") && DeveloperMode.IsUnlocked,
                "Correct password in How to Play unlocks developer mode")) yield break;
            GameScenes.ContinueDelivery();
            yield return WaitForSession();
            session = FindObjectOfType<HaulSession>();
            if (!Check(DeveloperMode.IsUnlocked && session != null && session.Ready,
                "Developer unlock survives the menu-to-game scene transition")) yield break;
            session.cargo.externalControl = true;
            WorkerVisual worker = FindObjectOfType<WorkerVisual>();
            if (!Check(worker != null && worker.Body != null && worker.Body.isKinematic &&
                worker.GetComponent<CapsuleCollider>() != null && worker.transform.Find("WorkerHandsCollider") != null,
                "Worker has a solid kinematic body and arm collision")) yield break;
            for (int side = -1; side <= 1; side += 2)
            {
                session.RestoreCheckpoint();
                // Isolate the contact from the pallet and rollers, then restore the normal stack.
                CheckpointController.Teleport(session.cargo.Body, session.cargo.Body.position + Vector3.up * 8f);
                foreach (RollingLog log in session.Checkpoints.Logs)
                    CheckpointController.Teleport(log.Body, log.Body.position + Vector3.right * 20f);
                CheckpointController.Teleport(session.robot.Body, worker.Body.position + new Vector3(side * 1.5f, -0.25f, 0.1f));
                session.robot.Body.velocity = Vector3.right * (-side * 4f);
                Physics.SyncTransforms();
                yield return new WaitForSeconds(0.4f);
                bool separated = side * (session.robot.Body.position.x - worker.Body.position.x) > 0.15f;
                foreach (Collider own in worker.GetComponentsInChildren<Collider>())
                    foreach (Collider other in session.robot.GetComponentsInChildren<Collider>())
                        if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > 0.04f)
                            separated = false;
                CaptureWorkerContact(side < 0 ? "WorkerBlocksRobotLeft.png" : "WorkerBlocksRobotRight.png", worker);
                if (!Check(separated, "Fallen robot hits the worker from the " + (side < 0 ? "left" : "right") + " without crossing or deep overlap")) yield break;
            }
            session.RestoreCheckpoint();
            if (!Check(Mathf.Abs(worker.Body.position.x - (session.cargo.Body.position.x - 3.05f)) < 0.01f,
                "Checkpoint recovery teleports the physical worker directly beside the pallet")) yield break;
            DaylightCycle daylight = FindObjectOfType<DaylightCycle>();
            if (!Check(daylight != null && daylight.Progress < 0.01f && daylight.sunDisc.sprite != null &&
                daylight.ruins.Length == 13 && session.GetComponent<DeliveryEnding>().fireFrames.Length == 4,
                "New delivery begins at sunrise with ruin decorations and four ending fire sprites")) yield break;
            session.cargo.externalControl = true;
            session.grabber.externalControl = true;
            int footstepsBeforePush = AudioFeedback.PlayCount(Cue.Footstep);
            CaptureScene("ThreeLogStart.png");
            yield return CaptureInterface("HudUi.png");
            float beforePush = session.cargo.Body.position.x;
            session.cargo.externalPush = true;
            RollingLog rear = session.Checkpoints.Logs[0];
            float pushDeadline = Time.unscaledTime + 3.5f;
            while (rear.Body.position.x >= session.cargo.Body.position.x - 2.1f &&
                Time.unscaledTime < pushDeadline) yield return null;
            session.cargo.externalPush = false;
            yield return new WaitForSeconds(0.65f);
            if (!Check(session.cargo.Body.position.x > beforePush + 0.8f, "Space motor advances a three-log load")) yield break;
            HaulSoundscape soundscape = session.GetComponent<HaulSoundscape>();
            if (!Check(soundscape != null && soundscape.Rolling.StartCount > 0 && soundscape.Rolling.Source.clip.name == "LogRollingLoop" &&
                AudioFeedback.PlayCount(Cue.Footstep) > footstepsBeforePush,
                "Physical log movement drives rolling audio and worker travel produces footsteps")) yield break;

            session.grabber.BeginGrab(rear, rear.Body.position);
            session.grabber.SetTarget(rear.Body.position + Vector3.left * 0.65f);
            yield return new WaitForSeconds(0.6f);
            session.grabber.SetTarget(rear.Body.position + Vector3.up * 2.2f);
            yield return new WaitForSeconds(1.2f);
            if (!Check(rear.Body.position.y > 1.5f, "Mouse spring lifts a released rear log")) yield break;
            if (!Check(AudioFeedback.PlayCount(Cue.Pickup) > 0 && AudioFeedback.GetClip(Cue.Pickup).name == "LogPickup",
                "Grabbing a log plays the supplied quiet wooden hit")) yield break;
            CaptureScene("DraggingLog.png");
            int woodHitsBeforeDrop = AudioFeedback.PlayCount(Cue.Drop);
            session.grabber.Release();
            if (!Check(AudioFeedback.PlayCount(Cue.Drop) == woodHitsBeforeDrop,
                "Releasing a log does not play its landing sound prematurely")) yield break;
            yield return new WaitForSeconds(1.5f);
            if (!Check(rear.Body.position.y > 0.43f && rear.Body.position.y < 0.65f, "Dropped log lands without tunneling")) yield break;
            if (!Check(AudioFeedback.PlayCount(Cue.Drop) > woodHitsBeforeDrop && AudioFeedback.GetClip(Cue.Drop).name == "LogLanding" &&
                AudioFeedback.Level(Cue.Drop) > AudioFeedback.Level(Cue.Pickup),
                "Logs land with the trimmed, louder quiet-wood clip; the hard impact is reserved for stone")) yield break;

            session.RestoreCheckpoint();
            if (!Check(LogsUnderCargo(session) && Mathf.Abs(session.cargo.Body.position.x) < 0.1f,
                "Reset before any checkpoint returns to the start and recovers three logs")) yield break;
            yield return new WaitForSeconds(1.4f);
            session.RecordImpact();
            if (!Check(AudioFeedback.PlayCount(Cue.Impact) > 0 && AudioFeedback.GetClip(Cue.Impact).name == "RobotImpact",
                "Robot damage plays the supplied punch impact")) yield break;
            int checkpointTicks = AudioFeedback.PlayCount(Cue.Checkpoint);
            PlaceAt(session, 9.1f);
            yield return new WaitForSeconds(0.35f);
            if (!Check(session.Checkpoints.CurrentIndex == 1 && GameProgress.TryLoad(out DeliverySave first) &&
                first.checkpointIndex == 1 && first.impacts == session.Impacts,
                "Crossing checkpoint one saves progress and damage automatically")) yield break;
            if (!Check(AudioFeedback.PlayCount(Cue.Checkpoint) > checkpointTicks && AudioFeedback.GetClip(Cue.Checkpoint).name == "CheckpointTick",
                "A reached checkpoint plays the supplied pencil check sound")) yield break;
            int savedHits = session.Impacts;
            session.cargo.Body.position = new Vector3(15f, 2f, 0f);
            for (int i = 0; i < session.Checkpoints.Logs.Length; i++)
            {
                Rigidbody body = session.Checkpoints.Logs[i].Body;
                body.position = new Vector3(80f + i * 5f, -15f, 0f);
                body.velocity = new Vector3(5f, -6f, 0f);
                body.angularVelocity = Vector3.forward * 8f;
            }
            session.RestoreCheckpoint();
            if (!Check(Mathf.Abs(session.cargo.Body.position.x - 9f) < 0.01f && LogsUnderCargo(session) &&
                session.Impacts == savedHits && session.cargo.Body.velocity.sqrMagnitude < 0.001f,
                "Reset recovers lost falling logs and removes old momentum at the current checkpoint")) yield break;
            yield return null;
            if (!Check(Mathf.Abs(daylight.Progress - 9f / session.Settings.finishX) < 0.002f,
                "Checkpoint retry restores the sun to the checkpoint's route progress")) yield break;
            session.grabber.BeginGrab(session.Checkpoints.Logs[0], session.Checkpoints.Logs[0].Body.position);
            session.TogglePause();
            Vector3 pausedPosition = session.cargo.Body.position;
            yield return new WaitForSecondsRealtime(0.3f);
            if (!Check(session.IsPaused && session.grabber.Held == null && Time.timeScale == 0f &&
                session.cargo.Body.position == pausedPosition, "Pause releases the mouse grip and stops physics")) yield break;
            if (!Check(soundscape.Rolling.Paused && !soundscape.Rolling.Source.isPlaying,
                "Pausing also pauses the movement loop")) yield break;
            yield return CaptureInterface("PauseUi.png");
            session.TogglePause();
            if (!Check(Time.timeScale == 1f && !session.InputBlocked, "Resume restores gameplay input")) yield break;
            session.ToggleTuning();
            yield return CaptureInterface("SettingsUi.png");
            session.ToggleTuning();
            session.ReturnToMenu();
            yield return WaitForMenu();
            if (!Check(GameProgress.HasActiveSave, "Main menu retains the checkpoint save")) yield break;
            GameScenes.ContinueDelivery();
            yield return WaitForSession();
            session = FindObjectOfType<HaulSession>();
            if (!Check(session != null && session.Ready && session.Checkpoints.CurrentIndex == 1 &&
                Mathf.Abs(session.cargo.Body.position.x - 9f) < 0.12f && LogsUnderCargo(session),
                "Continue across scenes resumes at checkpoint one")) yield break;
            session.cargo.externalControl = true;
            PlaceAt(session, 23.6f);
            yield return new WaitForSeconds(0.35f);
            if (!Check(session.Checkpoints.CurrentIndex == 2, "Checkpoint two supersedes checkpoint one")) yield break;
            session.RestoreCheckpoint();
            if (!Check(Mathf.Abs(session.cargo.Body.position.x - 23.5f) < 0.01f && LogsUnderCargo(session),
                "Later reset never falls back to an earlier checkpoint")) yield break;
            for (int index = 0; index <= session.Settings.checkpointPositions.Length; index++)
            {
                session.JumpToCheckpoint(index);
                float destination = index == 0 ? 0f : session.Settings.checkpointPositions[index - 1];
                int count = index < 3 ? 3 : 4;
                if (!Check(session.Checkpoints.CurrentIndex == index && session.Checkpoints.OwnedLogCount == count &&
                    Mathf.Abs(session.cargo.Body.position.x - destination) < 0.01f && session.robot.Body.velocity.sqrMagnitude < 0.001f &&
                    session.cargo.Body.velocity.sqrMagnitude < 0.001f &&
                    GameProgress.TryLoad(out DeliverySave jumped) && jumped.checkpointIndex == index &&
                    Mathf.Abs(FindObjectOfType<DaylightCycle>().Progress - destination / session.Settings.finishX) < 0.001f &&
                    session.Puzzles.boulder.Filled == (destination >= 108f),
                    "Jump " + index + " restores its position, log budget, robot, puzzle, daylight and saved retry destination")) yield break;
            }
            session.grabber.BeginGrab(session.Checkpoints.Logs[0], session.Checkpoints.Logs[0].Body.position);
            session.TogglePause();
            session.JumpToCheckpoint(5);
            if (!Check(!session.IsPaused && !session.TuningOpen && Time.timeScale == 1f && session.grabber.Held == null &&
                !session.Puzzles.boulder.Filled, "Jumping backwards from pause resumes safely and resets the stone puzzle")) yield break;
            session.RestoreCheckpoint();
            if (!Check(session.Checkpoints.CurrentIndex == 5 && Mathf.Abs(session.cargo.Body.position.x - 84f) < 0.01f,
                "R retries the selected jump destination")) yield break;
            session.JumpToCheckpoint(2);
            session.JumpToCheckpoint(-1);
            session.JumpToCheckpoint(session.Settings.checkpointPositions.Length + 1);
            if (!Check(session.Checkpoints.CurrentIndex == 2 && LogsUnderCargo(session), "Invalid destinations leave the chosen checkpoint unchanged")) yield break;
            CaptureScene("CheckpointRecovery.png");
            Finish(true);
        }

        private static IEnumerator WaitForSession()
        {
            float deadline = Time.unscaledTime + 8f;
            while (Time.unscaledTime < deadline)
            {
                HaulSession session = FindObjectOfType<HaulSession>();
                if (session != null && session.Ready && !GameScenes.IsTransitioning) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.2f);
        }

        private static IEnumerator WaitForMenu()
        {
            float deadline = Time.unscaledTime + 8f;
            while ((FindObjectOfType<MainMenuController>() == null || GameScenes.IsTransitioning) &&
                Time.unscaledTime < deadline) yield return null;
            yield return new WaitForSecondsRealtime(0.2f);
        }

        private static bool LogsUnderCargo(HaulSession session)
        {
            if (session.Checkpoints.Logs.Length != 3) return false;
            foreach (RollingLog log in session.Checkpoints.Logs)
            {
                if (!log.gameObject.activeInHierarchy ||
                    Mathf.Abs(log.Body.position.x - session.cargo.Body.position.x) > 1.65f ||
                    Mathf.Abs(log.Body.position.y - RouteGeometry.HeightAt(log.Body.position.x) - 0.508f) > 0.1f ||
                    Mathf.Abs(log.Body.position.z) > 0.01f) return false;
            }
            return true;
        }

        private static void PlaceAt(HaulSession session, float x)
        {
            Rigidbody cargo = session.cargo.Body;
            cargo.position = new Vector3(x, RouteGeometry.HeightAt(x) + 1.235f, 0f);
            cargo.rotation = Quaternion.identity;
            cargo.velocity = Vector3.zero;
            cargo.angularVelocity = Vector3.zero;
            cargo.transform.SetPositionAndRotation(cargo.position, cargo.rotation);
            if (session.robot != null) CheckpointController.Teleport(session.robot.Body, cargo.position);
            for (int i = 0; i < session.Checkpoints.Logs.Length; i++)
            {
                float logX = x + (i - 1) * 1.5f;
                Rigidbody body = session.Checkpoints.Logs[i].Body;
                body.position = new Vector3(logX, RouteGeometry.HeightAt(logX) + 0.508f, 0f);
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.transform.SetPositionAndRotation(body.position, body.rotation);
            }
            Physics.SyncTransforms();
            session.cargo.ResetAfterRecovery();
            if (session.Puzzles != null) session.Puzzles.RestoreAt(x);
        }

        private bool Check(bool condition, string message)
        {
            if (condition) { results.Add("PASS: " + message); return true; }
            failure = message;
            Finish(false);
            return false;
        }

        private void Finish(bool success)
        {
            if (failure != null) success = false;
            if (!success) results.Add("FAIL: " + failure);
            string report = string.Join("\n", results);
            File.WriteAllText(Path.Combine(output, resumeOnly ? "ResumeAndEnding.txt" : "MenuAndCheckpoints.txt"), report);
            Debug.Log("OLDTECH: " + report);
            Application.Quit(success ? 0 : 1);
        }

        private IEnumerator CaptureInterface(string filename)
        {
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            // Hidden Windows players may not have a readable swap chain. The editor preview
            // separately captures actual UI; do not publish a misleading black preview here.
            Color32[] pixels = image.GetPixels32();
            bool visible = false;
            for (int i = 0; i < pixels.Length; i += 199)
                if (pixels[i].r + pixels[i].g + pixels[i].b > 50) { visible = true; break; }
            if (visible) File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
            Destroy(image);
        }

        private void CaptureWorkerContact(string filename, WorkerVisual worker)
        {
            Camera camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize;
            try
            {
                Vector3 focus = worker.Body.position + new Vector3(0f, 1.1f, 0f);
                camera.transform.position = focus + new Vector3(0f, 4.2f, -18f);
                camera.transform.LookAt(focus);
                camera.orthographicSize = 2.4f;
                CaptureScene(filename);
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
                camera.orthographicSize = size;
            }
        }

        private void CaptureScene(string filename)
        {
            Camera camera = Camera.main;
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
                File.WriteAllBytes(Path.Combine(output, filename), image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                Destroy(image);
                rt.Release();
                Destroy(rt);
            }
        }
    }
}
#endif
