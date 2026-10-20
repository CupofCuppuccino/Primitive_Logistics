using UnityEngine;

namespace OldTech
{
    // Uses a force-limited spring so the mouse keeps the log's physical weight.
    public sealed class LogGrabber : MonoBehaviour
    {
        public Camera view;
        public CargoMotor cargo;
        [HideInInspector] public bool externalControl;
        public RollingLog Held { get; private set; }
        public RollingLog Hovered { get; private set; }
        public Vector3 Target => target;
        private Vector3 handlePosition;
        private HaulSession session;
        private Vector3 target;
        private Vector3 grabOffset;
        private Vector3 handleVelocity;
        private readonly Plane movementPlane = new Plane(Vector3.forward, Vector3.zero);

        private void Start()
        {
            session = FindObjectOfType<HaulSession>();
        }

        private void Update()
        {
            if (session == null || session.InputBlocked)
            {
                Release();
                SetHover(null);
                return;
            }
            if (externalControl) return;

            if (Held != null)
            {
                if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0)) Release();
                else UpdateTarget();
            }
            else
            {
                Ray ray = view.ScreenPointToRay(Input.mousePosition);
                RollingLog candidate = null;
                // Only logs are picked. Their exposed ends remain selectable under the deck.
                if (Physics.Raycast(ray, out RaycastHit hit, 100f, 1 << 9, QueryTriggerInteraction.Ignore))
                    candidate = hit.collider.GetComponentInParent<RollingLog>();
                if (candidate != null && candidate.anchored) candidate = null;
                SetHover(candidate);
                if (candidate != null && Input.GetMouseButtonDown(0))
                    BeginGrab(candidate, PointerOnPlane());
            }
        }

        private Vector3 PointerOnPlane()
        {
            Ray ray = view.ScreenPointToRay(Input.mousePosition);
            return movementPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : target;
        }

        private void UpdateTarget()
        {
            SetTarget(PointerOnPlane() + grabOffset);
        }

        public void SetTarget(Vector3 worldTarget)
        {
            Vector3 cargoPosition = cargo.Body != null ? cargo.Body.position : cargo.transform.position;
            float x = Mathf.Clamp(worldTarget.x, cargoPosition.x - 10f, cargoPosition.x + 10f);
            // Limit reach around the pallet; real colliders stop the log at edited terrain.
            // The generated route profile can otherwise leave an invisible floor above a lowered hill.
            float y = Mathf.Clamp(worldTarget.y, cargoPosition.y - 6.5f, cargoPosition.y + 6.5f);
            target = new Vector3(x, y, 0f);
        }

        public void BeginGrab(RollingLog log, Vector3 pointer)
        {
            if (Held != null || log == null || log.anchored) return;
            if (session == null) session = FindObjectOfType<HaulSession>();
            if (session != null && session.Checkpoints != null && !session.Checkpoints.TryClaimLog(log)) return;
            Held = log;
            log.SetGrabbed(true);
            AudioFeedback.Play(Cue.Pickup);
            grabOffset = log.Body.position - pointer;
            grabOffset.z = 0f;
            target = log.Body.position;
            handlePosition = target;
            handleVelocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            TickGrip(Time.fixedDeltaTime);
        }

        public void TickGrip(float step)
        {
            if (Held == null) return;
            Vector3 delta = target - handlePosition;
            Vector3 desiredVelocity = Vector3.ClampMagnitude(delta / 0.12f, session.Settings.dragSpeed);
            handleVelocity = Vector3.MoveTowards(handleVelocity, desiredVelocity, 28f * step);
            Vector3 motion = handleVelocity * step;
            if (motion.sqrMagnitude > delta.sqrMagnitude) motion = delta;
            handlePosition += motion;
            // A force-limited damped spring: collision response remains entirely with PhysX.
            // A trapped log resists the cursor instead of teleporting or building unbounded joint force.
            HaulSettings settings = session.Settings;
            Vector3 force = (handlePosition - Held.Body.position) * settings.gripSpring
                + (handleVelocity - Held.Body.velocity) * settings.gripDamping;
            force.z = 0f;
            Held.Body.AddForce(Vector3.ClampMagnitude(force, settings.maxGripForce), ForceMode.Force);
        }

        public void Release()
        {
            if (Held != null)
            {
                Held.SetGrabbed(false);
            }
            Held = null;
        }

        private void SetHover(RollingLog log)
        {
            if (Hovered == log) return;
            if (Hovered != null) Hovered.SetHovered(false);
            Hovered = log;
            if (Hovered != null) Hovered.SetHovered(true);
        }

        private void OnDisable()
        {
            Release();
            SetHover(null);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) Release();
        }
    }
}
