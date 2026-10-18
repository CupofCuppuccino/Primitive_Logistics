using System.Collections.Generic;
using UnityEngine;

namespace OldTech
{
    public enum Cue { Select, Pickup, Drop, Impact, Checkpoint, Recover, Complete, FireFeed, Footstep, Rolling, Fire, StoneImpact }

    public static class AudioFeedback
    {
        private static AudioSource source;
        private static AudioSource uiSource;
        private static SoundLibrary library;
        private static readonly Dictionary<Cue, AudioClip> clips = new Dictionary<Cue, AudioClip>();
        private static readonly Dictionary<Cue, float> nextPlay = new Dictionary<Cue, float>();
        private static readonly Dictionary<Cue, int> playCounts = new Dictionary<Cue, int>();
        public static SoundLibrary Library => library != null ? library : (library = Resources.Load<SoundLibrary>("SoundLibrary"));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            source = null;
            uiSource = null;
            library = null;
            clips.Clear();
            nextPlay.Clear();
            playCounts.Clear();
        }

        public static AudioClip GetClip(Cue cue) => Library != null ? Library.Get(cue)?.clip : null;
        public static float Level(Cue cue) => Library != null && Library.Get(cue) != null ? Library.Get(cue).level : 1f;
        public static int PlayCount(Cue cue) => playCounts.TryGetValue(cue, out int count) ? count : 0;
        public static bool GameplayAudioPlaying => source != null && source.isPlaying;
        public static void StopGameplaySounds() { if (source != null) source.Stop(); }
        public static float Volume
        {
            get => PlayerPrefs.GetFloat("OldTech.AudioVolume", 0.55f);
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat("OldTech.AudioVolume", volume);
                if (source != null) source.volume = volume;
                if (uiSource != null) uiSource.volume = volume;
            }
        }

        public static void Play(Cue cue, float strength = 1f)
        {
            if (!Application.isPlaying) return;
            float now = Time.unscaledTime;
            if (nextPlay.TryGetValue(cue, out float readyAt) && now < readyAt) return;
            SoundEntry entry = Library != null ? Library.Get(cue) : null;
            nextPlay[cue] = now + (entry != null ? entry.minimumInterval : 0.05f);
            AudioSource channel = cue == Cue.Select ? uiSource : source;
            if (channel == null)
            {
                GameObject root = new GameObject(cue == Cue.Select ? "UiAudioFeedback" : "GameplayAudioFeedback");
                Object.DontDestroyOnLoad(root);
                channel = root.AddComponent<AudioSource>();
                channel.playOnAwake = false;
                channel.spatialBlend = 0f;
                channel.volume = Volume;
                if (cue == Cue.Select) uiSource = channel;
                else source = channel;
            }
            AudioClip clip = entry != null ? entry.clip : null;
            if (clip == null && (!clips.TryGetValue(cue, out clip) || clip == null))
            {
                clip = MakeClip(cue);
                clips[cue] = clip;
            }
            channel.PlayOneShot(clip, Level(cue) * Mathf.Clamp01(strength));
            playCounts[cue] = PlayCount(cue) + 1;
        }

        private static AudioClip MakeClip(Cue cue)
        {
            const int sampleRate = 22050;
            float duration = cue == Cue.Checkpoint || cue == Cue.Complete ? 0.4f : 0.13f;
            float frequency = cue == Cue.Impact ? 85f : cue == Cue.Drop ? 120f :
                cue == Cue.Pickup ? 190f : cue == Cue.Checkpoint ? 440f : cue == Cue.Complete ? 523f : 280f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[count];
            System.Random random = new System.Random(31 + (int)cue);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float pitch = (cue == Cue.Checkpoint || cue == Cue.Complete) ? (t < 0.16f ? 1f : 1.5f) : 1f - t * 2f;
                phase += Mathf.PI * 2f * frequency * pitch / sampleRate;
                float envelope = Mathf.Min(1f, t / 0.008f) * Mathf.Pow(1f - t / duration, 2f);
                float noise = cue == Cue.Drop || cue == Cue.Impact ? ((float)random.NextDouble() * 2f - 1f) * 0.32f : 0f;
                samples[i] = (Mathf.Sin(phase) * 0.4f + noise) * envelope;
            }
            AudioClip clip = AudioClip.Create("Cue" + cue, count, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
