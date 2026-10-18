using UnityEngine;

namespace OldTech
{
    public sealed class CheckpointMarker : MonoBehaviour
    {
        public int checkpointIndex;
        [Range(3, 4)] public int recoveryLogCount = 3;
        public Renderer flag;
        public Renderer symbol;
        private HaulSession session;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            session = FindObjectOfType<HaulSession>();
            properties = new MaterialPropertyBlock();
        }

        private void Update()
        {
            Renderer target = symbol != null ? symbol : flag;
            if (session == null || session.Checkpoints == null || target == null) return;
            Color color = session.Checkpoints.CurrentIndex >= checkpointIndex ? new Color(0.7f, 0.25f, 0.12f) : new Color(0.76f, 0.69f, 0.5f);
            properties.SetColor("_Color", color);
            target.SetPropertyBlock(properties);
        }
    }
}
