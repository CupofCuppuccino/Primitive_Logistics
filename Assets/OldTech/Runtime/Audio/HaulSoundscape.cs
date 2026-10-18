using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(HaulSession))]
    public sealed class HaulSoundscape : MonoBehaviour
    {
        public SoundLoop Rolling { get; private set; }
        private HaulSession session;
        private WorkerVisual worker;
        private Vector3 lastWorkerPosition;
        private float stepDistance;

        private void Awake()
        {
            session = GetComponent<HaulSession>();
            worker = FindObjectOfType<WorkerVisual>();
            GameObject rolling = new GameObject("LogRollingAudio");
            rolling.transform.SetParent(transform, false);
            Rolling = rolling.AddComponent<SoundLoop>();
            Rolling.Configure(Cue.Rolling);
            ResetAfterRecovery();
        }

        public void ResetAfterRecovery()
        {
            if (worker != null) lastWorkerPosition = worker.transform.position;
            stepDistance = 0f;
            if (Rolling != null) Rolling.SetLevel(0f);
        }

        private void Update()
        {
            if (!session.Ready || session.IsPaused || session.TuningOpen)
            {
                ResetAfterRecovery();
                return;
            }
            float speed = 0f;
            if (!session.Finished && session.Checkpoints.Logs != null)
                foreach (RollingLog log in session.Checkpoints.Logs)
                {
                    if (!log.gameObject.activeInHierarchy || log.IsGrabbed || log.Body.isKinematic || !log.HasRollingContact) continue;
                    if (Mathf.Abs(log.Body.position.x - session.cargo.Body.position.x) > 12f) continue;
                    speed = Mathf.Max(speed, Mathf.Max(Mathf.Abs(log.Body.velocity.x), Mathf.Abs(log.Body.angularVelocity.z) * 0.5f));
                }
            Rolling.SetLevel(Mathf.InverseLerp(0.12f, 2.4f, speed));
            if (worker == null || !worker.gameObject.activeInHierarchy) return;
            Vector3 position = worker.transform.position;
            float travel = Mathf.Abs(position.x - lastWorkerPosition.x);
            lastWorkerPosition = position;
            if (travel > 2f) { stepDistance = 0f; return; }
            stepDistance += travel;
            if (stepDistance >= 0.52f)
            {
                stepDistance %= 0.52f;
                AudioFeedback.Play(Cue.Footstep);
            }
        }
    }
}
