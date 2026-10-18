using System;
using UnityEngine;

namespace OldTech
{
    [Serializable]
    public sealed class SoundEntry
    {
        public Cue cue;
        public AudioClip clip;
        [Range(0f, 1f)] public float level = 1f;
        public float minimumInterval;
    }

    [CreateAssetMenu(menuName = "Old Tech/Sound Library")]
    public sealed class SoundLibrary : ScriptableObject
    {
        public SoundEntry[] sounds;
        public SoundEntry Get(Cue cue)
        {
            if (sounds != null)
                foreach (SoundEntry sound in sounds)
                    if (sound != null && sound.cue == cue) return sound;
            return null;
        }
    }
}
