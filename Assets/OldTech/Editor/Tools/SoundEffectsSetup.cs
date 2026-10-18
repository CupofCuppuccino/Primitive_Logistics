using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OldTech.Editor
{
    public static class SoundEffectsSetup
    {
        private const string Folder = "Assets/OldTech/Audio/Sfx/";
        private static readonly string[] Files =
        {
            "AchievementSatisfied", "StoneImpact", "UiWoodButton", "RobotImpact", "FireCrackle",
            "FireFeed", "CheckpointTick", "LogPickup", "LogLanding", "LogRollingLoop", "Footstep"
        };

        [MenuItem("Old Tech/Import provided sound effects")]
        public static void Apply()
        {
            var report = new StringBuilder("Edited PCM WAVs remove leading silence; original supplied MP3 files are retained unchanged.\n");
            foreach (string name in Files)
            {
                string path = SoundPath(name);
                if (!File.Exists(path)) throw new FileNotFoundException("Sound effect missing", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AudioImporter importer = (AudioImporter)AssetImporter.GetAtPath(path);
                importer.forceToMono = !path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase);
                importer.loadInBackground = false;
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.preloadAudioData = true;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? AudioCompressionFormat.PCM : AudioCompressionFormat.Vorbis;
                settings.quality = 0.85f;
                settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                clip.LoadAudioData();
                if (clip.length <= 0f || clip.samples <= 0) throw new InvalidOperationException("Empty sound: " + path);
                float[] samples = new float[clip.samples * clip.channels];
                if (!clip.GetData(samples, 0) || !samples.Any(s => Mathf.Abs(s) > 0.0001f))
                    throw new InvalidOperationException("Sound has no readable signal: " + path);
                report.AppendLine(name + " | " + clip.length.ToString("0.000") + "s | " + clip.frequency + " Hz | peak " + samples.Max(s => Mathf.Abs(s)).ToString("0.000"));
                if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    int onset = Array.FindIndex(samples, s => Mathf.Abs(s) > 0.007943283f);
                    float leading = (float)onset / clip.frequency / clip.channels;
                    if (onset < 0 || leading > 0.006f) throw new InvalidOperationException("Edited sound still has a delayed onset: " + path);
                    report.AppendLine("  Verified onset: " + (leading * 1000f).ToString("0.00") + "ms; PCM playback.");
                }
            }
            const string libraryPath = "Assets/OldTech/Resources/SoundLibrary.asset";
            SoundLibrary library = AssetDatabase.LoadAssetAtPath<SoundLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            library.sounds = new[]
            {
                Entry(Cue.Select, "UiWoodButton", 0.65f, 0.06f),
                Entry(Cue.Pickup, "LogPickup", 0.65f, 0.08f),
                Entry(Cue.Drop, "LogLanding", 0.9f, 0.09f),
                Entry(Cue.StoneImpact, "StoneImpact", 0.8f, 0.2f),
                Entry(Cue.Impact, "RobotImpact", 0.85f, 0.15f),
                Entry(Cue.Checkpoint, "CheckpointTick", 0.85f, 0.12f),
                Entry(Cue.Recover, "CheckpointTick", 0.65f, 0.1f),
                Entry(Cue.Complete, "AchievementSatisfied", 0.8f, 0.5f),
                Entry(Cue.FireFeed, "FireFeed", 0.85f, 0.3f),
                Entry(Cue.Footstep, "Footstep", 0.4f, 0.16f),
                Entry(Cue.Rolling, "LogRollingLoop", 0.45f, 0f),
                Entry(Cue.Fire, "FireCrackle", 0.55f, 0f)
            };
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/SoundEffects");
            File.WriteAllText("Logs/SoundEffects/Import.txt", "PASS: All eleven active clips contain readable audio; all twelve cues bind to real clips.\n" + report);
        }

        private static SoundEntry Entry(Cue cue, string file, float level, float minimumInterval)
        {
            return new SoundEntry { cue = cue, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath(file)), level = level, minimumInterval = minimumInterval };
        }

        private static string SoundPath(string file)
        {
            string edited = Folder + "Edited/" + file + ".wav";
            return File.Exists(edited) ? edited : Folder + file + ".mp3";
        }
    }
}
