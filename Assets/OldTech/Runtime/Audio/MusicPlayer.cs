using UnityEngine;

namespace OldTech
{
    public sealed class MusicPlayer : MonoBehaviour
    {
        public AudioClip[] playlist;
        [Range(0f, 1f)] public float musicLevel = 0.6f;
        public int CurrentTrackIndex { get; private set; }
        public int SwitchCount { get; private set; }
        public AudioClip CurrentClip => playlist == null || playlist.Length == 0 ? null : playlist[CurrentTrackIndex];
        private readonly AudioSource[] channels = new AudioSource[2];
        private int playingChannel, queuedTrack;
        private double switchAt, queuedEnd;
        private bool scheduled;

        private void Start() { RestartPlaylist(); }

        public void RestartPlaylist()
        {
            scheduled = false;
            for (int i = 0; i < channels.Length; i++)
            {
                if (channels[i] == null) channels[i] = gameObject.AddComponent<AudioSource>();
                channels[i].Stop();
                channels[i].playOnAwake = false;
                channels[i].spatialBlend = 0f;
                channels[i].priority = 64;
                channels[i].volume = AudioFeedback.Volume * musicLevel;
            }
            if (playlist == null || playlist.Length == 0) return;
            foreach (AudioClip clip in playlist) if (clip == null) return;
            CurrentTrackIndex = SwitchCount = playingChannel = 0;
            double start = AudioSettings.dspTime + 0.1;
            channels[0].clip = playlist[0];
            channels[0].loop = playlist.Length == 1;
            channels[0].PlayScheduled(start);
            if (playlist.Length == 1) return;
            channels[1].loop = channels[0].loop = false;
            queuedTrack = 1;
            switchAt = start + Duration(playlist[0]);
            channels[1].clip = playlist[queuedTrack];
            channels[1].PlayScheduled(switchAt);
            queuedEnd = switchAt + Duration(playlist[queuedTrack]);
            scheduled = true;
        }

        private static double Duration(AudioClip clip) => (double)clip.samples / clip.frequency;

        private void Update()
        {
            foreach (AudioSource channel in channels)
                if (channel != null) channel.volume = AudioFeedback.Volume * musicLevel;
            if (!scheduled || AudioSettings.dspTime < switchAt) return;
            int freeChannel = playingChannel;
            playingChannel = 1 - playingChannel;
            CurrentTrackIndex = queuedTrack;
            queuedTrack = (queuedTrack + 1) % playlist.Length;
            SwitchCount++;
            // Queue the following clip on the source that just finished. DSP timing avoids frame-sized gaps.
            channels[freeChannel].clip = playlist[queuedTrack];
            channels[freeChannel].PlayScheduled(queuedEnd);
            switchAt = queuedEnd;
            queuedEnd += Duration(playlist[queuedTrack]);
        }

        private void OnDisable()
        {
            foreach (AudioSource channel in channels) if (channel != null) channel.Stop();
        }
    }
}
