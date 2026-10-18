using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(RollingLog))]
    public sealed class LogPickup : MonoBehaviour
    {
        public Transform marker;
        private HaulSession session;
        private bool hinted, claimed;
        private void Start() { session = FindObjectOfType<HaulSession>(); }
        public void SetClaimed(bool value)
        {
            claimed = value;
            if (marker != null) marker.gameObject.SetActive(!value);
            if (!value) hinted = false;
        }
        private void Update()
        {
            if (claimed || hinted || session == null || !session.Ready) return;
            if (Mathf.Abs(session.cargo.Body.position.x - transform.position.x) > 7f) return;
            hinted = true;
            session.ShowHint("SPARE LOG! Drag it to collect your fourth log. Next checkpoint restores four.");
        }
    }
}
