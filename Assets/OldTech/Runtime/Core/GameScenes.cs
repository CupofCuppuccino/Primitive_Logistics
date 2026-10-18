using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech
{
    public static class GameScenes
    {
        public const string MainMenu = "MainMenu";
        public const string Delivery = "RollingDelivery";
        public static bool IsTransitioning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            IsTransitioning = false;
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private static void SceneLoaded(Scene scene, LoadSceneMode mode) { IsTransitioning = false; }

        public static void StartNewDelivery()
        {
            GameProgress.Save(new DeliverySave());
            OpenDelivery();
        }

        public static void ContinueDelivery()
        {
            if (!GameProgress.HasActiveSave) return;
            OpenDelivery();
        }

        public static void OpenDelivery()
        {
            Time.timeScale = 1f;
            if (IsTransitioning) return;
            AudioFeedback.StopGameplaySounds();
            IsTransitioning = true;
            SceneManager.LoadSceneAsync(Delivery);
        }

        public static void OpenMenu()
        {
            Time.timeScale = 1f;
            if (IsTransitioning) return;
            AudioFeedback.StopGameplaySounds();
            IsTransitioning = true;
            SceneManager.LoadSceneAsync(MainMenu);
        }
    }
}
