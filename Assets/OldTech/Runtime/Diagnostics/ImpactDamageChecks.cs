#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace OldTech
{
    // Play-mode regression check against real PhysX contacts; never included in player builds.
    public sealed class ImpactDamageChecks : MonoBehaviour
    {
        private const string ActiveKey = "OldTech.ImpactDamageChecks";
        private const string Output = "Logs/RobotDamageCheck.txt";
        private readonly List<string> results = new List<string>();
        private bool probeOnly;
        private int workerContacts, palletContacts, logContacts;
        private string failure;

        [MenuItem("Old Tech/Validate robot-only impact damage")]
        public static void RunFromEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            EditorSceneManager.SaveOpenScenes();
            EditorSceneManager.OpenScene("Assets/OldTech/Scenes/MainMenu.unity");
            SessionState.SetBool(ActiveKey, true);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            SessionState.SetBool(ActiveKey, false);
            GameProgress.UseEditorSmokeSave = true;
            DontDestroyOnLoad(new GameObject("ImpactDamageChecks").AddComponent<ImpactDamageChecks>().gameObject);
        }

        private void OnEnable() { Application.logMessageReceived += TrackErrors; }
        private void OnDisable() { Application.logMessageReceived -= TrackErrors; }
        private void TrackErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!probeOnly) return;
            if (collision.collider.GetComponentInParent<WorkerVisual>() != null) workerContacts++;
            if (collision.collider.GetComponentInParent<CargoMotor>() != null) palletContacts++;
            if (collision.collider.GetComponentInParent<RollingLog>() != null) logContacts++;
        }

        private IEnumerator Start()
        {
            if (probeOnly) yield break;
            GameProgress.ClearSmokeSave();
            GameScenes.StartNewDelivery();
            float timeout = Time.realtimeSinceStartup + 15f;
            HaulSession session;
            do { yield return null; session = FindObjectOfType<HaulSession>(); }
            while ((session == null || !session.Ready) && Time.realtimeSinceStartup < timeout);
            if (!Require(session != null && session.Ready, "Delivery ready")) yield break;
            session.cargo.externalControl = true;
            session.grabber.externalControl = true;
            yield return new WaitForSeconds(1.8f);
            session.cargo.enabled = false;
            session.cargo.Body.isKinematic = true;
            WorkerVisual worker = FindObjectOfType<WorkerVisual>();
            worker.enabled = false;
            Rigidbody robot = session.robot.Body;
            robot.useGravity = false;
            robot.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            ImpactDamageChecks probe = robot.gameObject.AddComponent<ImpactDamageChecks>();
            probe.probeOnly = true;
            int hits = session.Impacts, sounds = AudioFeedback.PlayCount(Cue.Impact);

            // A fast fallen robot is physically stopped by the worker, without losing integrity.
            CheckpointController.Teleport(robot, worker.Body.position + new Vector3(-2f, 0.2f, 0f));
            session.robot.IgnoreImpactsFor(0f);
            robot.velocity = Vector3.right * 8f;
            yield return new WaitForSeconds(0.5f);
            if (!Require(probe.workerContacts > 0 && session.Impacts == hits && AudioFeedback.PlayCount(Cue.Impact) == sounds,
                "Physical robot/worker contact does not damage cargo or play robot-hit audio")) yield break;
            if (!Require(robot.position.x < worker.Body.position.x, "Worker still physically blocks the robot")) yield break;

            CheckpointController.Teleport(robot, new Vector3(session.cargo.Body.position.x, 4.5f, 0f));
            session.robot.IgnoreImpactsFor(0f);
            robot.velocity = Vector3.down * 8f;
            yield return new WaitForSeconds(0.65f);
            if (!Require(probe.palletContacts > 0 && session.Impacts == hits && AudioFeedback.PlayCount(Cue.Impact) == sounds,
                "Landing hard on the own pallet does not count as cargo damage")) yield break;

            int palletContacts = probe.palletContacts;
            CheckpointController.Teleport(robot, session.cargo.Body.position + new Vector3(3.5f, 0.2f, 0f));
            session.robot.IgnoreImpactsFor(0f);
            robot.velocity = Vector3.left * 8f;
            yield return new WaitForSeconds(0.5f);
            if (!Require(probe.palletContacts > palletContacts && session.Impacts == hits,
                "PalletEdge child colliders also block without damage")) yield break;

            robot.isKinematic = true;
            CheckpointController.Teleport(robot, new Vector3(0f, 3f, 0f));
            session.robot.IgnoreImpactsFor(0f);
            RollingLog log = session.Checkpoints.Logs[0];
            log.Body.useGravity = false;
            CheckpointController.Teleport(log.Body, new Vector3(3f, 4.2f, 0f));
            log.Body.velocity = Vector3.left * 9f;
            yield return new WaitForSeconds(0.55f);
            if (!Require(probe.logContacts > 0 && session.Impacts == hits + 1 && AudioFeedback.PlayCount(Cue.Impact) == sounds + 1,
                "A log directly hitting the robot still counts one hit and plays robot-hit audio")) yield break;
            Finish();
        }

        private bool Require(bool condition, string description)
        {
            results.Add((condition ? "PASS: " : "FAIL: ") + description);
            if (!condition) { failure = description; Finish(); }
            return condition;
        }

        private void Finish()
        {
            if (failure != null) results.Add("FAIL: " + failure);
            Directory.CreateDirectory("Logs");
            File.WriteAllLines(Output, results);
            GameProgress.ClearSmokeSave();
            GameProgress.UseEditorSmokeSave = false;
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
