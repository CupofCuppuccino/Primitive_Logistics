using UnityEngine;

namespace OldTech
{
    // TextMesh's built-in font material draws through opaque actors. Keep its font atlas,
    // but let the board lettering participate in the normal scene depth test.
    [ExecuteAlways, RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class DepthTestedLabel : MonoBehaviour
    {
        public Material textMaterial;
        private TextMesh label;
        private MeshRenderer target;
        private MaterialPropertyBlock properties;

        private void OnEnable()
        {
            label = GetComponent<TextMesh>();
            target = GetComponent<MeshRenderer>();
            properties = new MaterialPropertyBlock();
            RefreshAtlas();
            Font.textureRebuilt += OnFontTextureRebuilt;
        }

        private void OnDisable() { Font.textureRebuilt -= OnFontTextureRebuilt; }
        private void OnFontTextureRebuilt(Font font)
        {
            if (label != null && label.font == font) RefreshAtlas();
        }

        public void RefreshAtlas()
        {
            if (label == null) label = GetComponent<TextMesh>();
            if (target == null) target = GetComponent<MeshRenderer>();
            if (label.font == null || textMaterial == null) return;
            target.sharedMaterial = textMaterial;
            if (properties == null) properties = new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", label.font.material.mainTexture);
            target.SetPropertyBlock(properties);
        }
    }
}
