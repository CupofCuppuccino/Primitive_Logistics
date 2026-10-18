using UnityEngine;
using UnityEngine.Rendering;

namespace OldTech
{
    // Route progress represents one day. Retrying a checkpoint restores its time of day.
    [ExecuteAlways, DefaultExecutionOrder(100)]
    public sealed class DaylightCycle : MonoBehaviour
    {
        public Camera view;
        public Transform cargo;
        public Light sunlight;
        public Light skyFill;
        public SpriteRenderer sunDisc;
        public SpriteRenderer[] ruins;
        [Range(0f, 1f)] public float previewProgress;
        public float finishX = 190f;
        public float Progress { get; private set; }

        private void LateUpdate()
        {
            float progress = Application.isPlaying && cargo != null ? cargo.position.x / Mathf.Max(1f, finishX) : previewProgress;
            ApplyProgress(progress);
        }

        public void ApplyProgress(float progress)
        {
            if (view == null || sunlight == null || sunDisc == null) return;
            Progress = Mathf.Clamp01(progress);
            float arc = Mathf.Max(0f, Mathf.Sin(Progress * Mathf.PI));
            float dusk = Mathf.InverseLerp(0.86f, 1f, Progress);
            float x = Mathf.Lerp(0.08f, 0.92f, Progress);
            // Apex at 88% of the picture height, matching the marked reference.
            float y = 0.44f + arc * 0.44f - dusk * 0.16f;
            sunDisc.transform.SetPositionAndRotation(view.ViewportToWorldPoint(new Vector3(x, y, 55f)), view.transform.rotation);
            sunDisc.color = Color.Lerp(Color.white, new Color(1f, 0.52f, 0.32f), dusk);
            // The visible sun and the physical directional light use the same east-to-west arc.
            Vector3 towardSun = view.transform.right * ((x - 0.5f) * 27f) + Vector3.up * (1.4f + arc * 23f) + Vector3.back * 12f;
            sunlight.transform.rotation = Quaternion.LookRotation(-towardSun, Vector3.up);
            sunlight.color = Color.Lerp(Color.Lerp(new Color(1f, 0.64f, 0.37f), new Color(1f, 0.96f, 0.84f), arc),
                new Color(1f, 0.43f, 0.24f), dusk);
            sunlight.intensity = Mathf.Lerp(Mathf.Lerp(0.55f, 1.12f, Mathf.Sqrt(arc)), 0.16f, dusk);
            if (skyFill != null)
            {
                skyFill.color = Color.Lerp(new Color(0.88f, 0.9f, 1f), new Color(0.76f, 0.7f, 0.9f), dusk);
                skyFill.intensity = Mathf.Lerp(Mathf.Lerp(0.4f, 0.22f, arc), 0.48f, dusk);
            }
            RenderSettings.sun = sunlight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Color.Lerp(new Color(0.65f, 0.59f, 0.5f), new Color(0.65f, 0.71f, 0.75f), arc),
                new Color(0.43f, 0.4f, 0.48f), dusk);
            view.backgroundColor = Color.Lerp(Color.Lerp(new Color(0.72f, 0.62f, 0.5f), new Color(0.64f, 0.73f, 0.74f), arc),
                new Color(0.34f, 0.3f, 0.39f), dusk);
            if (ruins != null)
                foreach (SpriteRenderer ruin in ruins)
                    if (ruin != null) ruin.color = Color.Lerp(new Color(0.78f, 0.78f, 0.73f), new Color(0.48f, 0.45f, 0.51f), dusk);
        }
    }
}
