using System;
using UnityEngine;

namespace OldTech
{
    public sealed class CheckpointController : MonoBehaviour
    {
        public int CurrentIndex { get; private set; }
        public float CurrentX => CurrentIndex == 0 ? startX : session.Settings.checkpointPositions[CurrentIndex - 1];
        public RollingLog[] Logs { get; private set; }
        public RollingLog[] AllLogs { get; private set; }
        public int RecoveryLogCount => RequiredLogCount(CurrentIndex);
        public int OwnedLogCount => Logs == null ? 0 : Logs.Length;
        private HaulSession session;
        private CargoMotor cargo;
        private float startX, cargoClearance;
        private Vector3 robotOffset;
        private CheckpointMarker[] markers;

        private int RequiredLogCount(int index)
        {
            if (index > 0 && markers != null)
                foreach (CheckpointMarker marker in markers)
                    if (marker.checkpointIndex == index) return Mathf.Clamp(marker.recoveryLogCount, 3, 4);
            return session.Settings.LogCountAt(index);
        }

        public void Initialize(HaulSession owner)
        {
            session = owner;
            cargo = owner.cargo;
            markers = FindObjectsOfType<CheckpointMarker>();
            startX = cargo.transform.position.x;
            cargoClearance = cargo.transform.position.y - RouteGeometry.HeightAt(startX);
            if (owner.robot != null) robotOffset = owner.robot.transform.position - cargo.transform.position;
            AllLogs = Array.FindAll(FindObjectsOfType<RollingLog>(true), log => !log.anchored);
            Array.Sort(AllLogs, (a, b) => string.CompareOrdinal(a.name, b.name));
            if (AllLogs.Length < 3) throw new InvalidOperationException("The delivery scene requires three rolling logs.");
            SetOwnedCount(3);
        }

        private void SetOwnedCount(int count)
        {
            count = Mathf.Clamp(count, 3, Mathf.Min(4, AllLogs.Length));
            Logs = new RollingLog[count];
            Array.Copy(AllLogs, Logs, count);
            for (int i = 0; i < AllLogs.Length; i++)
            {
                LogPickup pickup = AllLogs[i].GetComponent<LogPickup>();
                if (pickup != null) pickup.SetClaimed(i < count);
            }
        }

        public bool TryClaimLog(RollingLog log)
        {
            int index = Array.IndexOf(AllLogs, log);
            if (index < 0) return false;
            if (index < Logs.Length) return true;
            if (Mathf.Abs(cargo.Body.position.x - log.transform.position.x) > 9f) return false;
            SetOwnedCount(index + 1);
            log.GetComponent<Rigidbody>().isKinematic = false;
            session.ShowHint("FOURTH LOG COLLECTED. Checkpoint 3 onward restores all four logs.");
            AudioFeedback.Play(Cue.Checkpoint);
            return true;
        }

        public void Restore(int index)
        {
            CurrentIndex = Mathf.Clamp(index, 0, session.Settings.checkpointPositions.Length);
            session.grabber.Release();
            SetOwnedCount(RecoveryLogCount);
            float x = CurrentX;
            Vector3 palletPosition = new Vector3(x, RouteGeometry.HeightAt(x) + cargoClearance, 0f);
            Teleport(cargo.GetComponent<Rigidbody>(), palletPosition);
            cargo.ResetAfterRecovery();
            if (session.robot != null)
            {
                session.robot.gameObject.SetActive(true);
                Teleport(session.robot.GetComponent<Rigidbody>(), palletPosition + robotOffset);
                session.robot.IgnoreImpactsFor(1.5f);
            }
            for (int i = 0; i < AllLogs.Length; i++)
            {
                RollingLog log = AllLogs[i];
                log.gameObject.SetActive(true);
                log.SetGrabbed(false);
                log.SetHovered(false);
                bool owned = i < Logs.Length;
                float logX = owned ? x + (i - (Logs.Length - 1) * 0.5f) * (Logs.Length == 3 ? 1.5f : 1.25f) : session.Settings.fourthLogX;
                Rigidbody body = log.GetComponent<Rigidbody>();
                Teleport(body, new Vector3(logX, RouteGeometry.HeightAt(logX) + 0.508f, 0f));
                log.MuteImpactsFor(0.4f);
                body.isKinematic = !owned;
            }
            WorkerVisual worker = FindObjectOfType<WorkerVisual>();
            if (worker != null) worker.SnapToCargo();
            HaulSoundscape sounds = session.GetComponent<HaulSoundscape>();
            if (sounds != null) sounds.ResetAfterRecovery();
            Physics.SyncTransforms();
            if (session.Puzzles != null) session.Puzzles.RestoreAt(x);
            HaulCamera camera = FindObjectOfType<HaulCamera>();
            if (camera != null) camera.Snap();
        }

        public bool TryAdvance()
        {
            if (session.InputBlocked || !session.Ready || CurrentIndex >= session.Settings.checkpointPositions.Length) return false;
            Rigidbody body = cargo.Body;
            if (!cargo.HasSupport || (session.robot != null && !session.robot.IsOnPallet) ||
                Mathf.Abs(Mathf.DeltaAngle(0f, body.rotation.eulerAngles.z)) > 40f) return false;
            bool advanced = false;
            while (CurrentIndex < session.Settings.checkpointPositions.Length && body.position.x >= session.Settings.checkpointPositions[CurrentIndex])
            {
                if (session.Puzzles != null && !session.Puzzles.CanAdvanceTo(session.Settings.checkpointPositions[CurrentIndex])) break;
                CurrentIndex++; advanced = true;
            }
            if (advanced)
            {
                SetOwnedCount(Mathf.Max(OwnedLogCount, RecoveryLogCount));
                for (int i = 0; i < Logs.Length; i++) Logs[i].GetComponent<Rigidbody>().isKinematic = false;
                session.CheckpointReached();
            }
            return advanced;
        }

        public static void Teleport(Rigidbody body, Vector3 position)
        {
            RigidbodyInterpolation interpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = Quaternion.identity;
            body.transform.SetPositionAndRotation(position, Quaternion.identity);
            body.interpolation = interpolation;
            body.WakeUp();
        }
    }
}
