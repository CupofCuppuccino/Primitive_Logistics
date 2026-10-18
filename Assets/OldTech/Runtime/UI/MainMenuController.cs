using UnityEngine;

namespace OldTech
{
    public sealed class MainMenuController : MonoBehaviour
    {
        public bool ControlsOpen { get; private set; }
        private string developerPassword = string.Empty;
        private string developerFeedback = string.Empty;

        public bool TryUnlockDeveloperMode(string password)
        {
            bool unlocked = DeveloperMode.TryUnlock(password);
            developerFeedback = unlocked ? "Developer mode unlocked. Enabled until you close the game." : "Incorrect password.";
            developerPassword = string.Empty;
            return unlocked;
        }

        private void Awake()
        {
            Time.timeScale = 1f;
        }

        private void Update()
        {
            if (ControlsOpen && Input.GetKeyDown(KeyCode.Escape)) ControlsOpen = false;
        }

        private void OnGUI()
        {
            Matrix4x4 old = GameGui.Begin(out float w, out float h);
            float x = 60f, top = Mathf.Max(35f, h * 0.5f - 305f);
            GameGui.Panel(new Rect(x, top, 510f, 605f), image: GuiImageId.MenuPanel);
            GameGui.Label(new Rect(x + 36f, top + 32f, 440f, 130f), "PRIMITIVE\nLOGISTICS", GameGui.Title);
            GameGui.Label(new Rect(x + 39f, top + 165f, 425f, 60f), "Log: An easy solution for transport or recording");
            bool canContinue = GameProgress.HasActiveSave;
            GUI.enabled = !ControlsOpen;
            string continueText = "CONTINUE DELIVERY";
            if (canContinue && GameProgress.TryLoad(out DeliverySave save))
                continueText += "  /  CP " + save.checkpointIndex;
            if (GameGui.Button(new Rect(x + 32f, top + 242f, 446f, 60f), continueText, canContinue)) GameScenes.ContinueDelivery();
            if (GameGui.Button(new Rect(x + 32f, top + 315f, 446f, 60f), "NEW DELIVERY")) GameScenes.StartNewDelivery();
            if (GameGui.Button(new Rect(x + 32f, top + 388f, 446f, 60f), "HOW TO PLAY")) ControlsOpen = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            if (GameGui.Button(new Rect(x + 32f, top + 461f, 446f, 60f), "FULLSCREEN")) Screen.fullScreen = !Screen.fullScreen;
#else
            if (GameGui.Button(new Rect(x + 32f, top + 461f, 446f, 60f), "QUIT")) Application.Quit();
#endif
            GameGui.Label(new Rect(x + 39f, top + 548f, 425f, 34f), "OLD TECH  /  LD60", GameGui.Small);
            GUI.enabled = true;

            if (ControlsOpen)
            {
                GameGui.Veil(w, h);
                Rect panel = new Rect(w * 0.5f - 310f, h * 0.5f - 325f, 620f, 650f);
                GameGui.Panel(panel, image: GuiImageId.HelpPanel);
                GameGui.Label(new Rect(panel.x + 35f, panel.y + 30f, 550f, 45f), "A VERY OLD DELIVERY METHOD", GameGui.Heading);
                GameGui.Controls(new Rect(panel.x + 35f, panel.y + 100f, 550f, 135f));
                GameGui.Label(new Rect(panel.x + 35f, panel.y + 250f, 550f, 60f), "Collect the fourth log. Fill pits for the worker and pallet. Take steep hills slowly: the robot can fall off!");
                GameGui.Label(new Rect(panel.x + 35f, panel.y + 325f, 160f, 30f), "SOUND", GameGui.Small);
                AudioFeedback.Volume = GameGui.Slider(new Rect(panel.x + 200f, panel.y + 335f, 355f, 20f), AudioFeedback.Volume, 0f, 1f);
                GameGui.Label(new Rect(panel.x + 35f, panel.y + 375f, 550f, 30f), "DEVELOPER MODE", GameGui.Small);
                if (!DeveloperMode.IsUnlocked)
                {
                    bool enter = Event.current.type == EventType.KeyDown &&
                        (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) &&
                        GUI.GetNameOfFocusedControl() == "DeveloperPassword";
                    GUI.SetNextControlName("DeveloperPassword");
                    developerPassword = GameGui.PasswordField(new Rect(panel.x + 35f, panel.y + 415f, 320f, 44f), developerPassword);
                    if (GameGui.Button(new Rect(panel.x + 375f, panel.y + 415f, 210f, 44f), "UNLOCK") || enter)
                    {
                        TryUnlockDeveloperMode(developerPassword);
                        GUI.FocusControl(null);
                        if (enter) Event.current.Use();
                    }
                }
                else GameGui.Label(new Rect(panel.x + 35f, panel.y + 415f, 550f, 44f), "DEVELOPER MODE UNLOCKED", GameGui.Heading);
                GameGui.Label(new Rect(panel.x + 35f, panel.y + 475f, 550f, 60f),
                    !string.IsNullOrEmpty(developerFeedback) ? developerFeedback : DeveloperMode.IsUnlocked ?
                    "0-9: checkpoint jumps. F1: physics settings. Enabled until you close the game." :
                    "Enter a password to enable checkpoint jumps and physics settings.", GameGui.Small);
                if (GameGui.Button(new Rect(panel.x + 30f, panel.y + 565f, 560f, 55f), "BACK")) ControlsOpen = false;
            }
            GUI.matrix = old;
        }
    }
}
