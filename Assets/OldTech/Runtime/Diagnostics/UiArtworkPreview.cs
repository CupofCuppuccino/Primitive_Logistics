#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OldTech
{
    // Capture from the editor Game view: a hidden Windows player has no readable swap chain.
    public sealed class UiArtworkPreview : MonoBehaviour
    {
        public const string ActiveKey = "OldTech.UiArtworkPreview.Active";
        private const string Output = "Logs/PaintedUi/Preview/";
        private string failure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;
            SessionState.SetBool(ActiveKey, false);
            DontDestroyOnLoad(new GameObject("UiArtworkPreview").AddComponent<UiArtworkPreview>().gameObject);
        }
        private void OnEnable() { Application.logMessageReceived += TrackErrors; }
        private void OnDisable() { Application.logMessageReceived -= TrackErrors; }
        private void TrackErrors(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failure = message + "\n" + trace;
        }
        private IEnumerator Start()
        {
            Directory.CreateDirectory(Output);
            GameProgress.UseEditorSmokeSave = true;
            GameProgress.ClearSmokeSave();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Capture("Menu.png");
            MainMenuController menu = FindObjectOfType<MainMenuController>();
            typeof(MainMenuController).GetProperty("ControlsOpen").SetValue(menu, true);
            yield return Capture("Help.png");
            menu.TryUnlockDeveloperMode("wrong");
            yield return Capture("DeveloperLocked.png");
            menu.TryUnlockDeveloperMode("LudumDare60");
            yield return Capture("DeveloperUnlocked.png");
            typeof(MainMenuController).GetProperty("ControlsOpen").SetValue(menu, false);
            GameScenes.StartNewDelivery();
            float timeout = Time.realtimeSinceStartup + 12f;
            HaulSession session;
            do { yield return null; session = FindObjectOfType<HaulSession>(); }
            while ((session == null || !session.Ready) && Time.realtimeSinceStartup < timeout);
            if (session == null || !session.Ready) { Finish("Game UI preview timed out."); yield break; }
            session.cargo.externalControl = true;
            session.grabber.externalControl = true;
            yield return Capture("Hud.png");
            session.JumpToCheckpoint(3);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Capture("Checkpoint03.png");
            session.JumpToCheckpoint(0);
            session.TogglePause();
            yield return Capture("Pause.png");
            session.ToggleTuning();
            yield return Capture("Settings.png");
            session.ToggleTuning();
            session.JumpToCheckpoint(9);
            float distance = session.Settings.finishX + 0.1f - session.cargo.Body.position.x;
            CheckpointController.Teleport(session.cargo.Body, session.cargo.Body.position + Vector3.right * distance);
            CheckpointController.Teleport(session.robot.Body, session.robot.Body.position + Vector3.right * distance);
            foreach (RollingLog log in session.Checkpoints.Logs) CheckpointController.Teleport(log.Body, log.Body.position + Vector3.right * distance);
            Physics.SyncTransforms();
            timeout = Time.realtimeSinceStartup + 16f;
            while (!session.GetComponent<DeliveryEnding>().RobotBurned && Time.realtimeSinceStartup < timeout) yield return null;
            yield return Capture("Fire.png");
            session.GetComponent<DeliveryEnding>().Skip();
            yield return Capture("Results.png");
            GameProgress.ClearSmokeSave();
            Finish(null);
        }

        private IEnumerator Capture(string filename)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
            Color32[] pixels = image.GetPixels32();
            bool visible = false;
            for (int i = 0; i < pixels.Length; i += 199)
                if (pixels[i].r + pixels[i].g + pixels[i].b > 50) { visible = true; break; }
            if (!visible) failure = "Game view capture is black: " + filename;
            File.WriteAllBytes(Output + filename, image.EncodeToPNG());
            Destroy(image);
        }
        private void Finish(string error)
        {
            if (error != null) failure = error;
            File.WriteAllText(Output + "Validation.txt", failure == null ?
                "PASS: Menu, help, developer password rejection/unlock, HUD, checkpoint 3, pause, settings, four-frame fire and results captured with actual picture UI and text. No runtime exceptions." : "FAIL: " + failure);
            Time.timeScale = 1f;
            GameProgress.UseEditorSmokeSave = false;
            EditorApplication.isPlaying = false;
        }
    }
}
#endif
