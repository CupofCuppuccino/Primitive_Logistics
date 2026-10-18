using UnityEngine;

namespace OldTech
{
    [RequireComponent(typeof(HaulSession))]
    public sealed class HaulHud : MonoBehaviour
    {
        private HaulSession session;

        private void Awake() { session = GetComponent<HaulSession>(); }

        private void OnGUI()
        {
            if (session == null || !session.Ready) return;
            Matrix4x4 old = GameGui.Begin(out float w, out float h);
            if (!session.Finished) DrawHud(w, h);
            if (session.IsPaused) DrawPause(w, h);
            if (session.TuningOpen) DrawTuning(w, h);
            DeliveryEnding ending = session.GetComponent<DeliveryEnding>();
            if (session.Finished && !session.EndingComplete)
            {
                if (ending != null && ending.ResultVisible)
                {
                    GameGui.Panel(new Rect(w * 0.5f - 245f, 24f, 490f, 100f));
                    GameGui.Label(new Rect(w * 0.5f - 210f, 38f, 420f, 42f), "DELIVERY COMPLETE", GameGui.Heading);
                    GameGui.Label(new Rect(w * 0.5f - 210f, 82f, 420f, 35f), "Robot condition  " + session.Integrity + "%");
                }
                if (GameGui.Button(new Rect(w - 190f, h - 65f, 170f, 45f), "SKIP")) ending.Skip();
            }
            if (session.EndingComplete) DrawFinish(w, h);
            GUI.matrix = old;
        }

        private void DrawHud(float w, float h)
        {
            GameGui.Panel(new Rect(18f, 18f, 285f, 83f));
            GameGui.Label(new Rect(42f, 29f, 245f, 34f), "CARGO  " + session.Integrity + "%", GameGui.Heading);
            GameGui.Label(new Rect(43f, 66f, 240f, 30f), session.Impacts + " hits  /  " + session.Checkpoints.OwnedLogCount + " logs", GameGui.Small);
            GameGui.Panel(new Rect(w - 330f, 18f, 310f, 83f));
            GameGui.Label(new Rect(w - 303f, 29f, 260f, 34f), Mathf.Clamp(session.cargo.Body.position.x, 0, session.Settings.finishX).ToString("0.0") + " / " + session.Settings.finishX.ToString("0") + " m", GameGui.Heading);
            GameGui.Label(new Rect(w - 302f, 66f, 260f, 30f), "CHECKPOINT " + session.Checkpoints.CurrentIndex + " / " + session.Settings.checkpointPositions.Length, GameGui.Small);
            GameGui.Panel(new Rect(18f, h - 84f, w - 36f, 65f), image: GuiImageId.ControlsBar);
            GameGui.Label(new Rect(43f, h - 65f, w - 85f, 35f), DeveloperMode.IsUnlocked ?
                "MOUSE  drag     SPACE  push     R  restore     0-9  jump     F1  tune     ESC  pause" :
                "MOUSE  drag     SPACE  push     R  restore     ESC  pause", GameGui.Text);
            if (!string.IsNullOrEmpty(session.Hint))
            {
                GameGui.Panel(new Rect(w * 0.5f - 455f, h - 147f, 910f, 48f), image: GuiImageId.HintPanel);
                GameGui.Label(new Rect(w * 0.5f - 420f, h - 136f, 840f, 32f), session.Hint, GameGui.Small);
            }
        }

        private void DrawPause(float w, float h)
        {
            GameGui.Veil(w, h);
            Rect rect = new Rect(w * 0.5f - 250f, h * 0.5f - 245f, 500f, 490f);
            GameGui.Panel(rect, image: GuiImageId.PausePanel);
            GameGui.Label(new Rect(rect.x + 35f, rect.y + 28f, 430f, 45f), "TAKE A BREATHER", GameGui.Heading);
            if (GameGui.Button(new Rect(rect.x + 30f, rect.y + 100f, 440f, 58f), "RESUME")) session.TogglePause();
            if (GameGui.Button(new Rect(rect.x + 30f, rect.y + 177f, 440f, 58f), "RESTORE CHECKPOINT")) session.RestoreCheckpoint();
            if (GameGui.Button(new Rect(rect.x + 30f, rect.y + 254f, 440f, 58f),
                DeveloperMode.IsUnlocked ? "PHYSICS SETTINGS" : "PHYSICS SETTINGS (LOCKED)", DeveloperMode.IsUnlocked)) session.ToggleTuning();
            if (GameGui.Button(new Rect(rect.x + 30f, rect.y + 331f, 440f, 58f), "MAIN MENU")) session.ReturnToMenu();
            GameGui.Label(new Rect(rect.x + 38f, rect.y + 417f, 420f, 45f), "Your last checkpoint is saved automatically.", GameGui.Small);
        }

        private void DrawFinish(float w, float h)
        {
            Rect rect = new Rect(w - 465f, h * 0.5f - 210f, 440f, 420f);
            GameGui.Panel(rect, image: GuiImageId.ResultsPanel);
            GameGui.Label(new Rect(rect.x + 30f, rect.y + 28f, 380f, 45f), "JOB DONE.", GameGui.Heading);
            GameGui.Label(new Rect(rect.x + 30f, rect.y + 90f, 380f, 50f), "Robot delivered: " + session.Integrity + "%\nLogs carried: " + session.Checkpoints.OwnedLogCount, GameGui.Text);
            GameGui.Label(new Rect(rect.x + 30f, rect.y + 158f, 380f, 30f), session.Elapsed.ToString("0.0") + " seconds  /  " + session.Impacts + " impacts", GameGui.Small);
            if (GameGui.Button(new Rect(rect.x + 25f, rect.y + 215f, 390f, 50f), "NEW DELIVERY")) GameScenes.StartNewDelivery();
            if (GameGui.Button(new Rect(rect.x + 25f, rect.y + 279f, 390f, 50f), "RETRY LAST CHECKPOINT")) session.RetryLastCheckpoint();
            if (GameGui.Button(new Rect(rect.x + 25f, rect.y + 343f, 390f, 50f), "MAIN MENU")) session.ReturnToMenu();
        }

        private void DrawTuning(float w, float h)
        {
            GameGui.Veil(w, h);
            float x = w * 0.5f - 280f, y = h * 0.5f - 280f;
            GameGui.Panel(new Rect(x, y, 560f, 560f), image: GuiImageId.SettingsPanel);
            GameGui.Label(new Rect(x + 32f, y + 26f, 490f, 45f), "PHYSICS SETTINGS", GameGui.Heading);
            float row = y + 100f;
            bool changed = Slider(x, ref row, "Push acceleration", ref session.Settings.pushAcceleration, 0.5f, 7f);
            changed |= Slider(x, ref row, "Push speed", ref session.Settings.topSpeed, 0.5f, 5f);
            changed |= Slider(x, ref row, "Braking", ref session.Settings.braking, 0.1f, 6f);
            changed |= Slider(x, ref row, "Grip stiffness", ref session.Settings.gripSpring, 200f, 3000f);
            changed |= Slider(x, ref row, "Grip damping", ref session.Settings.gripDamping, 10f, 180f);
            changed |= Slider(x, ref row, "Drag speed", ref session.Settings.dragSpeed, 3f, 18f);
            changed |= Slider(x, ref row, "Friction", ref session.Settings.friction, 0.2f, 1.5f);
            changed |= Slider(x, ref row, "Rolling resistance", ref session.Settings.rollingDrag, 0.01f, 0.8f);
            if (changed) session.ApplyTuning();
            GameGui.Label(new Rect(x + 34f, y + 464f, 490f, 30f), "For this delivery only. New deliveries restore defaults.", GameGui.Small);
            if (GameGui.Button(new Rect(x + 28f, y + 500f, 504f, 42f), "F1 / RESUME")) session.ToggleTuning();
        }

        private static bool Slider(float x, ref float y, string label, ref float value, float min, float max)
        {
            GameGui.Label(new Rect(x + 34f, y, 235f, 28f), label, GameGui.Text);
            GameGui.Label(new Rect(x + 455f, y, 80f, 28f), value.ToString("0.00"), GameGui.Small);
            float next = GameGui.Slider(new Rect(x + 285f, y + 8f, 150f, 20f), value, min, max);
            bool changed = !Mathf.Approximately(next, value);
            value = next;
            y += 43f;
            return changed;
        }
    }
}
