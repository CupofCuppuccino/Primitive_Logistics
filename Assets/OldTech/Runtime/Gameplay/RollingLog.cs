using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RollingLog : MonoBehaviour
    {
        public bool anchored;
        public Rigidbody Body { get; private set; }
        public bool IsGrabbed { get; private set; }
        public bool IsHovered { get; private set; }
        public Renderer rim;
        public Material normalRim;
        public Material hoverRim;
        public Material heldRim;
        private HaulSession session;
        private float lastContact = float.NegativeInfinity;
        private float muteImpactsUntil;
        public bool HasRollingContact => Time.fixedTime - lastContact <= Time.fixedDeltaTime * 2.5f;

        public void MuteImpactsFor(float seconds)
        {
            muteImpactsUntil = Time.time + seconds;
            lastContact = float.NegativeInfinity;
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            if (anchored) Body.isKinematic = true;
            MuteImpactsFor(0.5f);
        }

        private void OnCollisionStay(Collision collision) { lastContact = Time.fixedTime; }

        private void OnCollisionEnter(Collision collision)
        {
            lastContact = Time.fixedTime;
            if (anchored || session == null || !session.Ready || session.InputBlocked || Time.time < muteImpactsUntil) return;
            if (collision.collider.GetComponentInParent<RobotCargo>() != null) return;
            float impact = 0f;
            for (int i = 0; i < collision.contactCount; i++)
                impact = Mathf.Max(impact, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal)));
            if (impact < 0.85f) return;
            muteImpactsUntil = Time.time + 0.16f;
            AudioFeedback.Play(Cue.Drop, Mathf.Lerp(0.55f, 1f, Mathf.InverseLerp(0.85f, 5f, impact)));
        }

        private void Start()
        {
            session = FindObjectOfType<HaulSession>();
            ApplyTuning();
        }

        public void ApplyTuning()
        {
            if (session == null) session = FindObjectOfType<HaulSession>();
            if (Body == null) Body = GetComponent<Rigidbody>();
            if (anchored) { Body.isKinematic = true; return; }
            // Scene previews and temporarily reimporting scripts may have no live session yet.
            if (session == null || session.Settings == null || Body == null) return;
            Body.mass = session.Settings.logMass;
            Body.angularDrag = IsGrabbed ? 1.8f : session.Settings.rollingDrag;
            Body.drag = IsGrabbed ? 1.5f : 0.035f;
            Body.maxAngularVelocity = 25f;
        }

        public void SetGrabbed(bool value)
        {
            if (anchored) value = false;
            IsGrabbed = value;
            ApplyTuning();
            UpdateRim();
        }

        public void SetHovered(bool value)
        {
            IsHovered = value;
            UpdateRim();
        }

        private void UpdateRim()
        {
            if (rim != null)
            {
                rim.sharedMaterial = IsGrabbed ? heldRim : IsHovered ? hoverRim : normalRim;
                rim.enabled = IsGrabbed || IsHovered;
            }
        }
    }
}
