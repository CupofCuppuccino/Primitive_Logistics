using System;
using UnityEngine;

namespace OldTech
{
    public static class DeveloperMode
    {
        public static bool IsUnlocked { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() { IsUnlocked = false; }

        public static bool TryUnlock(string password)
        {
            if (!string.Equals(password, "LudumDare60", StringComparison.Ordinal)) return false;
            IsUnlocked = true;
            return true;
        }
    }
}
