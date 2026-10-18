using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SoundLoop : MonoBehaviour
    {
        public Cue Cue { get; private set; }
        public AudioSource Source { get; private set; }
        public float TargetLevel { get; private set; }
        public int StartCount { get; private set; }
        public bool Paused { get; private set; }
        private float level;

        public void Configure(Cue cue)
        {
            Cue = cue;
            Source = GetComponent<AudioSource>();
            Source.playOnAwake = false;
            Source.loop = true;
            Source.spatialBlend = 0f;
            Source.priority = 160;
            Source.volume = 0f;
            Source.clip = AudioFeedback.GetClip(cue);
        }

        public void SetLevel(float value) { TargetLevel = Mathf.Clamp01(value); }

        private void Update()
        {
            if (Source == null || Source.clip == null) return;
            bool pause = Time.timeScale == 0f;
            if (pause != Paused)
            {
                Paused = pause;
                if (pause) Source.Pause();
                else Source.UnPause();
            }
            if (pause) return;
            level = Mathf.MoveTowards(level, TargetLevel, Time.unscaledDeltaTime * (TargetLevel > level ? 5f : 3f));
            Source.volume = AudioFeedback.Volume * AudioFeedback.Level(Cue) * level;
            if (level > 0.001f && !Source.isPlaying)
            {
                Source.Play();
                StartCount++;
            }
            else if (level <= 0.001f && Source.isPlaying) Source.Stop();
        }

        private void OnDisable() { if (Source != null) Source.Stop(); }
    }
}
