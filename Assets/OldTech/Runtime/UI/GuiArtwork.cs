using System;
using UnityEngine;

namespace OldTech
{
    public enum GuiImageId
    {
        MenuPanel, PausePanel, HelpPanel, SettingsPanel, ResultsPanel,
        HudPanel, HintPanel, ControlsBar,
        ButtonNormal, ButtonHover, ButtonPressed, ButtonDisabled,
        SliderTrack, SliderThumb, ModalShade
    }

    [Serializable]
    public sealed class GuiImage
    {
        public string name;
        public Texture2D texture;
        public Rect region = new Rect(0f, 0f, 1f, 1f);
    }

    public sealed class GuiArtwork : ScriptableObject
    {
        public GuiImage[] images;
        public GuiImage Get(GuiImageId id) => images != null && (int)id < images.Length ? images[(int)id] : null;
        public bool Complete
        {
            get
            {
                foreach (GuiImageId id in Enum.GetValues(typeof(GuiImageId)))
                    if (Get(id) == null || Get(id).texture == null) return false;
                return true;
            }
        }
    }
}
