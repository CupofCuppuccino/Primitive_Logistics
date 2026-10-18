using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoulderPuzzle : MonoBehaviour
    {
        public Vector3 spawnPosition = new Vector3(90f, 1.45f, 0f);
        public Vector3 filledPosition = new Vector3(97.7f, -1.45f, 0f);
        public float solvedCheckpointX = 108f;
        public bool Unlocked { get; private set; }
        public bool Filled { get; private set; }
        public Rigidbody Body { get; private set; }
        private HaulSession session;
        private float muteImpactsUntil;
        private float lastLandingSound = float.NegativeInfinity;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            session = FindObjectOfType<HaulSession>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            TryUnlock(collision);
            if (!Unlocked || Filled || session == null || !session.Ready || session.InputBlocked || Time.time < muteImpactsUntil) return;
            if (collision.collider.gameObject.layer != 8) return;
            float impact = 0f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y > 0.4f)
                    impact = Mathf.Max(impact, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal)));
            }
            if (impact >= 0.85f) PlayLanding(Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(0.85f, 5f, impact)));
        }
        private void OnCollisionStay(Collision collision) { TryUnlock(collision); }
        private void TryUnlock(Collision collision)
        {
            if (Unlocked || Filled) return;
            RollingLog log = collision.collider.GetComponentInParent<RollingLog>();
            if (log == null || log.anchored) return;
            if (!log.IsGrabbed && collision.relativeVelocity.sqrMagnitude < 0.05f) return;
            Unlocked = true;
            Body.isKinematic = false;
            Body.WakeUp();
            if (session != null) session.ShowHint("STONE LOOSENED! Keep pushing with the log until it drops into the square pit.");
        }

        private void FixedUpdate()
        {
            if (!Unlocked || Filled) return;
            if (Mathf.Abs(Body.position.x - filledPosition.x) < 0.36f && Mathf.Abs(Body.position.y - filledPosition.y) < 0.12f &&
                Mathf.Abs(Body.velocity.y) < 1f)
            {
                Filled = true;
                Body.velocity = Body.angularVelocity = Vector3.zero;
                Body.isKinematic = true;
                if (Time.time - lastLandingSound > 0.35f) PlayLanding(1f);
                if (session != null) session.ShowHint("PIT FILLED. The stone is now your bridge.");
                AudioFeedback.Play(Cue.Checkpoint);
            }
        }

        public void Restore(bool solved)
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            CheckpointController.Teleport(Body, solved ? filledPosition : spawnPosition);
            Body.isKinematic = true;
            Unlocked = Filled = solved;
            muteImpactsUntil = Time.time + 0.4f;
            lastLandingSound = float.NegativeInfinity;
        }

        private void PlayLanding(float strength)
        {
            lastLandingSound = Time.time;
            AudioFeedback.Play(Cue.StoneImpact, strength);
        }
    }
}
