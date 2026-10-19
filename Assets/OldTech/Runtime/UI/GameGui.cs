using UnityEngine;

namespace OldTech
{
    public static class GameGui
    {
        public static readonly Color Ink = new Color(0.26f, 0.16f, 0.11f);
        public static readonly Color Paper = new Color(0.94f, 0.86f, 0.69f);
        public static readonly Color Red = new Color(0.62f, 0.22f, 0.12f);
        private static GuiArtwork artwork;
        public static GuiArtwork Artwork => artwork != null ? artwork : (artwork = Resources.Load<GuiArtwork>("GuiArtwork"));
        public static GUIStyle Title { get; private set; }
        public static GUIStyle Heading { get; private set; }
        public static GUIStyle Text { get; private set; }
        public static GUIStyle Small { get; private set; }
        public static GUIStyle MenuQuote { get; private set; }
        private static GUIStyle button, buttonHovered, passwordField;
        private static GUIStyle sliderTrack, sliderThumb;

        public static Matrix4x4 Begin(out float width, out float height)
        {
            EnsureStyles();
            float scale = Mathf.Max(0.1f, Mathf.Min(Screen.width / 1280f, Screen.height / 720f));
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            width = Screen.width / scale;
            height = Screen.height / scale;
            return old;
        }

        private static void EnsureStyles()
        {
            if (Title != null) return;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
            SetTextColor(Text, Ink);
            Title = new GUIStyle(Text) { fontSize = 48, fontStyle = FontStyle.Bold };
            Heading = new GUIStyle(Text) { fontSize = 26, fontStyle = FontStyle.Bold };
            SetTextColor(Heading, Red);
            Small = new GUIStyle(Text) { fontSize = 14 };
            MenuQuote = new GUIStyle(Text) { fontSize = 14, wordWrap = false };
            SetTextColor(MenuQuote, Red);
            button = new GUIStyle(Text) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            SetTextColor(button, Red);
            buttonHovered = new GUIStyle(button) { fontSize = 23 };
            passwordField = new GUIStyle(Text)
            {
                wordWrap = false, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(12, 12, 4, 4)
            };
            sliderTrack = new GUIStyle(GUI.skin.horizontalSlider);
            sliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb);
            if (Artwork != null)
            {
                SetBackground(sliderTrack, Artwork.Get(GuiImageId.SliderTrack)?.texture);
                SetBackground(sliderThumb, Artwork.Get(GuiImageId.SliderThumb)?.texture);
            }
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            foreach (GUIStyleState state in new[] { style.normal, style.hover, style.active, style.focused,
                style.onNormal, style.onHover, style.onActive, style.onFocused })
                state.textColor = color;
        }

        private static void SetBackground(GUIStyle style, Texture2D texture)
        {
            style.normal.background = style.hover.background = style.active.background = style.focused.background = texture;
        }

        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Panel(Rect rect, Color? tint = null, GuiImageId image = GuiImageId.HudPanel)
        {
            EnsureStyles();
            Color old = GUI.color;
            GUI.color = new Color(0.15f, 0.08f, 0.03f, 0.25f);
            DrawImage(new Rect(rect.x + 3f, rect.y + 5f, rect.width, rect.height), image, 16f);
            GUI.color = tint ?? Color.white;
            DrawImage(rect, image, 16f);
            GUI.color = old;
        }

        public static void Label(Rect rect, string text, GUIStyle style = null)
        {
            EnsureStyles();
            GUI.Label(rect, text, style ?? Text);
        }

        public static bool Button(Rect rect, string text, bool enabled = true)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && enabled && !GameScenes.IsTransitioning;
            EnsureStyles();
            bool hover = rect.Contains(Event.current.mousePosition);
            GuiImageId image = !GUI.enabled ? GuiImageId.ButtonDisabled :
                hover && GUIUtility.hotControl != 0 && Input.GetMouseButton(0) ? GuiImageId.ButtonPressed :
                hover ? GuiImageId.ButtonHover : GuiImageId.ButtonNormal;
            Color old = GUI.color;
            GUI.color *= !GUI.enabled ? new Color(0.65f, 0.65f, 0.65f) :
                image == GuiImageId.ButtonPressed ? new Color(0.82f, 0.77f, 0.66f) :
                hover ? new Color(1f, 0.96f, 0.85f) : Color.white;
            DrawImage(rect, image, 12f);
            GUI.color = old;
            bool pressed = GUI.Button(rect, text, GUI.enabled && hover ? buttonHovered : button);
            GUI.enabled = previous;
            if (pressed) AudioFeedback.Play(Cue.Select);
            return pressed;
        }

        public static void Veil(float width, float height)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.13f, 0.08f, 0.04f, 0.48f);
            DrawImage(new Rect(0f, 0f, width, height), GuiImageId.ModalShade, 0f);
            GUI.color = old;
        }

        public static string PasswordField(Rect rect, string value)
        {
            EnsureStyles();
            DrawImage(rect, GuiImageId.ButtonNormal, 10f);
            return GUI.PasswordField(rect, value, '*', 64, passwordField);
        }

        public static float Slider(Rect rect, float value, float min, float max)
        {
            EnsureStyles();
            return GUI.HorizontalSlider(rect, value, min, max, sliderTrack, sliderThumb);
        }

        // Independent source and screen borders keep painted corners intact on any panel size.
        private static void DrawImage(Rect rect, GuiImageId id, float edge)
        {
            GuiImage image = Artwork != null ? Artwork.Get(id) : null;
            if (image == null || image.texture == null) { GUI.DrawTexture(rect, Texture2D.whiteTexture); return; }
            Rect region = image.region;
            if (edge <= 0f) { GUI.DrawTextureWithTexCoords(rect, image.texture, region); return; }
            float bx = Mathf.Min(edge, rect.width * 0.5f), by = Mathf.Min(edge, rect.height * 0.5f);
            float[] x = { rect.x, rect.x + bx, rect.xMax - bx, rect.xMax };
            float[] y = { rect.y, rect.y + by, rect.yMax - by, rect.yMax };
            // IMGUI rounds each textured rectangle separately. At fractional UI scales,
            // adjacent slices can otherwise disagree by a pixel and expose the backdrop.
            Matrix4x4 matrix = GUI.matrix;
            for (int i = 0; i < 4; i++)
            {
                x[i] = (Mathf.Round(x[i] * matrix.m00 + matrix.m03) - matrix.m03) / matrix.m00;
                y[i] = (Mathf.Round(y[i] * matrix.m11 + matrix.m13) - matrix.m13) / matrix.m11;
            }
            float[] u = { region.x, region.x + region.width * 0.12f, region.xMax - region.width * 0.12f, region.xMax };
            float[] v = { region.yMax, region.yMax - region.height * 0.12f, region.y + region.height * 0.12f, region.y };
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    GUI.DrawTextureWithTexCoords(new Rect(x[column], y[row], x[column + 1] - x[column], y[row + 1] - y[row]),
                        image.texture, new Rect(u[column], v[row + 1], u[column + 1] - u[column], v[row] - v[row + 1]));
        }

        public static void Controls(Rect rect)
        {
            string text = "MOUSE drag wood\nSPACE hold to push cart\nR / Restore the current checkpoint and its logs.\nESC / Pause or return to the menu.";
            if (DeveloperMode.IsUnlocked)
                text += "\n0-9 / Jump to checkpoints (also numpad).\nF1 / Open physics settings.";
            Label(rect, text);
        }
    }
}
