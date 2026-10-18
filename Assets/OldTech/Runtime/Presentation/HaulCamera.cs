using UnityEngine;

namespace OldTech
{
    // The same camera follows the load and frames the delivery ending.
    [RequireComponent(typeof(Camera))]
    public sealed class HaulCamera : MonoBehaviour
    {
        public Transform cargo;
        public float lead = 1.8f;
        public float followTime = 0.32f;
        private Vector3 velocity;
        private bool cinematic;
        private Vector3 cinematicFocus;
        private float targetSize = 5.65f;

        public void Focus(Vector3 point, float size = 5.65f)
        {
            cinematic = true;
            cinematicFocus = point;
            targetSize = size;
        }

        public void Snap()
        {
            cinematic = false;
            GetComponent<Camera>().orthographicSize = 5.65f;
            Vector3 focus = new Vector3(cargo.position.x + lead, cargo.position.y + 0.7f, 0f);
            transform.position = focus + new Vector3(0f, 4.2f, -18f);
            transform.LookAt(focus);
            velocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (cargo == null) return;
            Vector3 focus = new Vector3(cargo.position.x + lead, Mathf.Max(1.2f, cargo.position.y) + 0.7f, 0f);
            if (cinematic) focus = cinematicFocus;
            Vector3 desired = focus + new Vector3(0f, 4.2f, -18f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, followTime);
            transform.rotation = Quaternion.LookRotation(new Vector3(0f, -4.2f, 18f), Vector3.up);
            Camera camera = GetComponent<Camera>();
            camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, cinematic ? targetSize : 5.65f, 1f - Mathf.Exp(-Time.deltaTime * 3f));
        }
    }
}
