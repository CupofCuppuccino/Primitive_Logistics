using UnityEngine;

namespace OldTech
{
    [DefaultExecutionOrder(50)]
    public sealed class WorkerVisual : MonoBehaviour
    {
        public CargoMotor cargo;
        public Transform torso;
        public Transform leftLeg;
        public Transform rightLeg;
        public PhysicMaterial contactMaterial;
        public Rigidbody Body { get; private set; }
        private float stride;
        private Transform handsCollider;

        private void Awake() { ConfigureCollision(contactMaterial); }

        public void ConfigureCollision(PhysicMaterial material)
        {
            contactMaterial = material;
            Body = GetComponent<Rigidbody>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody>();
            Body.isKinematic = true;
            Body.useGravity = false;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.constraints = RigidbodyConstraints.FreezeRotation;
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule == null) capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.radius = 0.24f;
            capsule.height = 1.9f;
            capsule.direction = 1;
            capsule.isTrigger = false;
            capsule.sharedMaterial = material;
            Transform hands = transform.Find("WorkerHandsCollider");
            if (hands == null)
            {
                hands = new GameObject("WorkerHandsCollider").transform;
                hands.SetParent(transform, false);
            }
            handsCollider = hands;
            hands.localPosition = torso != null ? torso.localPosition : new Vector3(0f, 0.68f, 0f);
            BoxCollider box = hands.GetComponent<BoxCollider>();
            if (box == null) box = hands.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0.39f, 0.38f, 0f);
            box.size = new Vector3(0.72f, 0.16f, 0.42f);
            box.isTrigger = false;
            box.sharedMaterial = material;
        }

        private void Start()
        {
            // Walking/footing already comes from the course. Keep the new solid worker
            // from changing log dragging, pallet movement, or the terrain support checks.
            Collider[] others = FindObjectsOfType<Collider>();
            foreach (Collider own in GetComponentsInChildren<Collider>())
                foreach (Collider other in others)
                    if (other.attachedRigidbody != Body && other.GetComponentInParent<RobotCargo>() == null)
                        Physics.IgnoreCollision(own, other);
            SnapToCargo();
        }

        private Vector3 WalkingPosition()
        {
            float x = cargo.WorkerX;
            RouteGeometry.TryWalkingSurface(x, out float height);
            return new Vector3(x, height, -0.1f);
        }

        public void SnapToCargo()
        {
            if (cargo == null) return;
            if (Body == null) ConfigureCollision(contactMaterial);
            RigidbodyInterpolation interpolation = Body.interpolation;
            Body.interpolation = RigidbodyInterpolation.None;
            Body.position = WalkingPosition();
            transform.position = Body.position;
            Body.interpolation = interpolation;
        }

        private void FixedUpdate()
        {
            if (cargo == null || cargo.Body == null) return;
            handsCollider.localRotation = Quaternion.Euler(0f, 0f, cargo.IsPushing ? -20f : -5f);
            Body.MovePosition(WalkingPosition());
        }

        public void PrepareForEnding()
        {
            enabled = false;
            if (Body != null) Body.detectCollisions = false;
        }

        private void LateUpdate()
        {
            float speed = cargo.Body == null ? 0f : Mathf.Abs(cargo.Body.velocity.x);
            bool pushing = cargo.IsPushing;
            stride += Time.deltaTime * (speed * 5f + (pushing ? 3f : 0f));
            float swing = Mathf.Sin(stride) * Mathf.Min(27f, speed * 18f + (pushing ? 8f : 0f));
            torso.localRotation = Quaternion.Euler(0f, 0f, pushing ? -20f : -5f);
            leftLeg.localRotation = Quaternion.Euler(0f, 0f, swing);
            rightLeg.localRotation = Quaternion.Euler(0f, 0f, -swing);
        }
    }

}
