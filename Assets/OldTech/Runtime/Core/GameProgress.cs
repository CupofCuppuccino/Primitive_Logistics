using System;
using UnityEngine;

namespace OldTech
{
    [Serializable]
    public sealed class DeliverySave
    {
        public int version = 1;
        public int checkpointIndex;
        public int impacts;
        public float elapsed;
        public bool completed;
    }

    public static class GameProgress
    {
        // Automated player checks never read or overwrite a player's actual progress.
#if UNITY_EDITOR
        public static bool UseEditorSmokeSave { get; set; }
        public static bool IsSmokeTest => UseEditorSmokeSave || IsPlayerSmoke;
#else
        public static bool IsSmokeTest => IsPlayerSmoke;
#endif
#if UNITY_WEBGL && !UNITY_EDITOR
        private static bool ExtendedSmoke => false;
        private static bool IsPlayerSmoke => false;
#else
        private static bool ExtendedSmoke => Array.IndexOf(Environment.GetCommandLineArgs(), "-oldTechExtended") >= 0;
        private static bool IsPlayerSmoke => ExtendedSmoke || Array.IndexOf(Environment.GetCommandLineArgs(), "-oldTechSmoke") >= 0;
#endif
        private static string SaveKey => ExtendedSmoke ? "OldTech.Delivery.ExtendedSmoke.v1" : IsSmokeTest ? "OldTech.Delivery.Smoke.v1" : "OldTech.Delivery.v1";
        public static bool HasActiveSave => TryLoad(out DeliverySave data) && !data.completed;

        public static bool TryLoad(out DeliverySave data)
        {
            data = null;
            if (!PlayerPrefs.HasKey(SaveKey)) return false;
            try
            {
                data = JsonUtility.FromJson<DeliverySave>(PlayerPrefs.GetString(SaveKey));
                if (data == null || data.version != 1 || data.checkpointIndex < 0 ||
                    float.IsNaN(data.elapsed) || float.IsInfinity(data.elapsed)) return false;
                data.impacts = Mathf.Max(0, data.impacts);
                data.elapsed = Mathf.Max(0f, data.elapsed);
                return true;
            }
            catch (ArgumentException) { data = null; return false; }
        }

        public static void Save(DeliverySave data)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static void CompleteDelivery()
        {
            if (!TryLoad(out DeliverySave data)) data = new DeliverySave();
            data.completed = true;
            Save(data);
        }

        public static void ReopenLastCheckpoint()
        {
            if (!TryLoad(out DeliverySave data)) data = new DeliverySave();
            data.completed = false;
            Save(data);
        }

        public static void ClearSmokeSave()
        {
            if (!IsSmokeTest) return;
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
