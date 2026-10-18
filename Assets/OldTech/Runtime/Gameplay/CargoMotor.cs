using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CargoMotor : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public bool IsPushing { get; private set; }
        public bool HasSupport { get; private set; }
        public bool WorkerHasFooting { get; private set; }
        public float WorkerX { get; private set; }
        public bool externalControl;
        public bool externalPush;
        private HaulSession session;
        private bool waitForSpaceRelease;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.centerOfMass = Vector3.zero;
            WorkerX = Body.position.x - 3.05f;
        }

        private void Start()
        {
            session = FindObjectOfType<HaulSession>();
            if (session == null || session.Settings == null)
            {
                Debug.LogError("CargoMotor requires an active HaulSession with HaulSettings. Check the Systems object for missing scripts.", this);
                enabled = false;
                return;
            }
            Body.mass = session.Settings.cargoMass;
        }

        private void FixedUpdate()
        {
            if (session == null || session.Settings == null) return;
            bool pushing = externalControl ? externalPush : Input.GetKey(KeyCode.Space);
            if (!externalControl && waitForSpaceRelease)
            {
                if (!pushing) waitForSpaceRelease = false;
                pushing = false;
            }
            TickMotor(pushing, Time.fixedDeltaTime);
        }

        // Also used by the editor's deterministic physics smoke checks.
        public void TickMotor(bool pushRequested, float step)
        {
            if (session == null) session = FindObjectOfType<HaulSession>();
            if (Body == null) Awake();
            if (session == null || session.Settings == null) return;
            HaulSettings settings = session.Settings;
            Body.mass = settings.cargoMass;
            // Check the whole underside: a few rays can all fall between adjacent round supports.
            HasSupport = Physics.CheckBox(Body.position + Body.rotation * new Vector3(0f, -0.32f, 0f),
                new Vector3(2.25f, 0.16f, 0.65f), Body.rotation,
                (1 << 8) | (1 << 9), QueryTriggerInteraction.Ignore);

            UpdateWorkerFooting();
            if (!WorkerHasFooting)
            {
                float extension = Mathf.Max(0f, Body.position.x - WorkerX - 3.05f);
                float restraint = extension * 1100f + Mathf.Max(0f, Body.velocity.x) * 130f;
                Body.AddForce(Vector3.left * Mathf.Min(950f, restraint), ForceMode.Force);
            }
            IsPushing = pushRequested && HasSupport && WorkerHasFooting && !session.InputBlocked;
            float angle = Mathf.Clamp(Mathf.DeltaAngle(0f, Body.rotation.eulerAngles.z), -55f, 55f) * Mathf.Deg2Rad;
            // The worker pushes horizontally: balancing a slope requires g*tan(angle).
            // Include the payload and the share of roller weight carried through their upper contacts.
            float carriedMass = session.robot != null && session.robot.IsOnPallet ? settings.robotMass : 0f;
            if (session.Checkpoints != null && session.Checkpoints.Logs != null)
                foreach (RollingLog log in session.Checkpoints.Logs)
                {
                    Vector3 local = transform.InverseTransformPoint(log.transform.position);
                    if (!log.IsGrabbed && Mathf.Abs(local.x) < 2.8f && local.y < 0f && local.y > -1.2f)
                        carriedMass += settings.logMass * 0.5f;
                }
            float hillHold = -Physics.gravity.y * Mathf.Tan(angle) * (1f + carriedMass / Mathf.Max(1f, settings.cargoMass));
            if (IsPushing)
            {
                float taper = Mathf.Clamp01(1f - Mathf.Max(0f, Body.velocity.x) / settings.topSpeed);
                Body.AddForce(Vector3.right * (settings.pushAcceleration * taper + hillHold), ForceMode.Acceleration);
            }
            else if (HasSupport)
            {
                // The worker holds back gently. Preserve a useful coast rather than stopping instantly.
                float deceleration = Mathf.Clamp(-Body.velocity.x * settings.braking, -5f, 5f);
                Body.AddForce(Vector3.right * (deceleration + hillHold), ForceMode.Acceleration);
                if (pushRequested && !WorkerHasFooting) session.ShowHint("NO FOOTING! Fill the pit with logs so the worker can cross too.");
            }
        }

        public void ResetAfterRecovery()
        {
            if (Body == null) Body = GetComponent<Rigidbody>();
            WorkerX = Body.position.x - 3.05f;
            WorkerHasFooting = true;
            IsPushing = false;
            externalPush = false;
            waitForSpaceRelease = true;
        }

        private void UpdateWorkerFooting()
        {
            float desired = Body.position.x - 3.05f;
            float distance = desired - WorkerX;
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(distance) / 0.12f), 1, 2048);
            float start = WorkerX;
            WorkerHasFooting = true;
            for (int i = 1; i <= steps; i++)
            {
                float candidate = Mathf.Lerp(start, desired, (float)i / steps);
                if (!RouteGeometry.TryWalkingSurface(candidate, out _)) { WorkerHasFooting = false; break; }
                WorkerX = candidate;
            }
        }
    }
}
