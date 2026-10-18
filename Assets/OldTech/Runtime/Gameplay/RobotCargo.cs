using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class RobotCargo : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        private HaulSession session;
        private float ignoreUntil;
        public bool IsOnPallet
        {
            get
            {
                if (session == null || session.cargo == null || Body == null) return false;
                // Use the mass centre: the model pivot sits below its feet and moves as it tips.
                Vector3 local = session.cargo.transform.InverseTransformPoint(Body.worldCenterOfMass);
                return Mathf.Abs(local.x) < 2.5f && local.y > 0.25f && local.y < 1.8f;
            }
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.centerOfMass = new Vector3(0f, 0.78f, 0f);
        }
        private void Start()
        {
            session = FindObjectOfType<HaulSession>();
            if (session != null) Body.mass = session.Settings.robotMass;
            IgnoreImpactsFor(1.5f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (session == null || session.InputBlocked || Time.time < ignoreUntil) return;
            // The worker and pallet remain solid, but their supporting/blocking contacts
            // must never count as damage to the delivery. This includes child PalletEdges.
            if (collision.collider.GetComponentInParent<WorkerVisual>() != null ||
                collision.collider.GetComponentInParent<CargoMotor>() != null) return;
            float impactSpeed = 0f;
            for (int i = 0; i < collision.contactCount; i++)
                impactSpeed = Mathf.Max(impactSpeed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal)));
            if (impactSpeed < session.Settings.impactThreshold) return;
            ignoreUntil = Time.time + 0.65f;
            session.RecordImpact();
        }

        public void IgnoreImpactsFor(float seconds) { ignoreUntil = Time.time + seconds; }
    }
}
