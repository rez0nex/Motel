using UnityEngine;

namespace Motel
{
    // World text only. It does not draw UI, handle input or interact with doors.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh), typeof(MeshRenderer))]
    public sealed class WorldSign : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float maximumWidth = 0.6f;
        [SerializeField, Min(0.01f)] private float maximumHeight = 0.14f;
        private TextMesh text;
        private MeshRenderer textRenderer;
        private MaterialPropertyBlock properties;
        private bool needsRefresh;

        private void OnEnable()
        {
            Font.textureRebuilt += OnFontRebuilt;
            needsRefresh = true;
        }

        private void OnDisable() => Font.textureRebuilt -= OnFontRebuilt;
        private void OnValidate() => needsRefresh = true;

        private void LateUpdate()
        {
            if (needsRefresh) RefreshNow();
        }

        private void OnFontRebuilt(Font font)
        {
            if (text != null && text.font == font)
            {
                BindAtlas();
                needsRefresh = true;
            }
        }

        public void Configure(float width, float height)
        {
            maximumWidth = width;
            maximumHeight = height;
            RefreshNow();
        }

        public bool RefreshNow()
        {
            if (text == null) text = GetComponent<TextMesh>();
            if (textRenderer == null) textRenderer = GetComponent<MeshRenderer>();
            if (text.font == null || string.IsNullOrEmpty(text.text)) return false;
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            BindAtlas();
            Bounds bounds = textRenderer.localBounds;
            if (bounds.size.x <= 0 || bounds.size.y <= 0) { needsRefresh = true; return false; }
            float scale = Mathf.Min(maximumWidth / bounds.size.x, maximumHeight / bounds.size.y);
            transform.localScale = Vector3.one * scale;
            transform.localPosition = -bounds.center * scale;
            needsRefresh = false;
            return true;
        }

        private void BindAtlas()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            textRenderer.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex", text.font.material.mainTexture);
            textRenderer.SetPropertyBlock(properties);
        }
    }
}
