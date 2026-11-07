using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(TextMesh))]
public class WorldSignLabel : MonoBehaviour
{
    [Min(0.1f)] public float width = 1.3f;
    [Min(0.05f)] public float height = 0.26f;
    public FontStyle fontStyle = FontStyle.Bold;
    public bool leftAligned;
    private bool needsFit = true;
    private Font font;
    private Material textMaterial;

    private void OnEnable() { needsFit = true; Font.textureRebuilt += RefreshAtlas; }
    private void OnDisable() { Font.textureRebuilt -= RefreshAtlas; }
    private void OnValidate() { needsFit = true; }

    private void RefreshAtlas(Font changedFont)
    {
        if (changedFont == font && textMaterial != null) textMaterial.mainTexture = font.material.mainTexture;
    }

    private void LateUpdate()
    {
        if (!needsFit) return;
        TextMesh text = GetComponent<TextMesh>();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.font = font;
        text.fontSize = 80;
        text.characterSize = 0.1f;
        text.fontStyle = fontStyle;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = leftAligned ? TextAlignment.Left : TextAlignment.Center;
        Renderer renderer = text.GetComponent<Renderer>();
        if (textMaterial == null)
        {
            Material template = Resources.Load<Material>("WorldSignText");
            textMaterial = new Material(template != null ? template : font.material)
                { name = "Built-in Font Sign Lettering", hideFlags = HideFlags.DontSave };
        }
        textMaterial.mainTexture = font.material.mainTexture;
        renderer.sharedMaterial = textMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Bounds bounds = renderer.localBounds;
        if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f) return;
        float scale = Mathf.Min(width / bounds.size.x, height / bounds.size.y);
        transform.localScale = Vector3.one * scale;
        Vector3 center = bounds.center;
        center.z = 0f;
        Vector3 offset = leftAligned ? Vector3.right * (-width * 0.5f + bounds.extents.x * scale) : Vector3.zero;
        transform.localPosition = transform.localRotation * (offset - center * scale);
        needsFit = false;
    }

    private void OnDestroy()
    {
        if (textMaterial == null) return;
        if (Application.isPlaying) Destroy(textMaterial);
        else DestroyImmediate(textMaterial);
    }
}
