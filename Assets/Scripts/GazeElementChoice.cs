using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GazeElementChoice : MonoBehaviour
{
    public GazeElementSelection selection;
    public int index;

    private const float Width = 0.56f;
    private const float Height = 2.18f;
    private static readonly Color Brass = new Color(0.65f, 0.55f, 0.36f);
    private static readonly Color Ivory = new Color(0.97f, 0.94f, 0.84f);
    private readonly List<Material> materials = new List<Material>();
    private readonly List<TextMesh> labels = new List<TextMesh>();
    private readonly List<Vector3> labelPositions = new List<Vector3>();
    private readonly List<Vector2> labelSizes = new List<Vector2>();
    private Material borderMaterial;
    private Transform progressFill;
    private Material panelTemplate;
    private bool focused, fitLabels;

    public void Initialize(Material reference, string elementName, Color elementColor)
    {
        panelTemplate = Resources.Load<Material>("ElementChoicePanel");
        borderMaterial = ColorMaterial(reference, Brass);
        Material background = ColorMaterial(reference, new Color(0.09f, 0.13f, 0.17f));
        Material footer = ColorMaterial(reference, new Color(0.055f, 0.08f, 0.11f));
        Material accent = ColorMaterial(reference, Color.Lerp(Ivory, elementColor, 0.55f));

        Vector3 center = new Vector3(0f, 0.08f, 0.22f);
        Vector3 size = new Vector3(Width, Height, 0.045f);
        Plate("Selection Area Frame", center, size, borderMaterial);
        Plate("Slate Selection Background", new Vector3(0f, center.y, 0.19f),
            new Vector3(Width - 0.024f, Height - 0.024f, 0.018f), background);
        Plate("Label Backing", new Vector3(0f, -0.835f, 0.175f),
            new Vector3(Width - 0.024f, 0.325f, 0.012f), footer);
        Plate("Element Color Inlay", new Vector3(0f, 1.115f, 0.17f),
            new Vector3(0.30f, 0.014f, 0.01f), accent);

        // The visible outer frame and the only gaze collider share their dimensions.
        BoxCollider target = gameObject.AddComponent<BoxCollider>();
        target.center = center;
        target.size = size;

        string displayName = elementName == "Wind" ? "Air" : elementName;
        Label("Element Name", displayName, -0.835f, 0.13f, Ivory, FontStyle.Bold);
        Plate("Gaze Progress Track", new Vector3(0f, -0.985f, 0.157f),
            new Vector3(0.44f, 0.012f, 0.008f), background);
        progressFill = Plate("Gaze Progress Fill", new Vector3(-0.22f, -0.985f, 0.15f),
            new Vector3(0f, 0.012f, 0.008f), accent);
        progressFill.gameObject.SetActive(false);
        fitLabels = true;
    }

    private Material ColorMaterial(Material reference, Color color)
    {
        // A Resources material keeps the unlit shader available in mobile builds.
        Material material = new Material(panelTemplate != null ? panelTemplate : reference);
        material.name = "Color Only Element Choice";
        foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", Color.black);
        materials.Add(material);
        return material;
    }

    private Transform Plate(string label, Vector3 position, Vector3 size, Material material)
    {
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = label;
        plate.layer = 2;
        plate.transform.SetParent(transform, false);
        plate.transform.localPosition = position;
        plate.transform.localScale = size;
        Collider collider = plate.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        Renderer renderer = plate.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return plate.transform;
    }

    private TextMesh Label(string objectName, string text, float y, float height, Color color,
        FontStyle style = FontStyle.Normal)
    {
        TextMesh label = new GameObject(objectName).AddComponent<TextMesh>();
        label.gameObject.layer = 2;
        label.transform.SetParent(transform, false);
        // Keep lettering close to the backing so perspective does not shift the outer labels sideways.
        Vector3 position = new Vector3(0f, y, 0.158f);
        label.transform.localPosition = position;
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.characterSize = 0.05f;
        label.fontSize = 80;
        label.fontStyle = style;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Renderer renderer = label.GetComponent<Renderer>();
        renderer.sharedMaterial = label.font.material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        label.color = color;
        labels.Add(label);
        labelPositions.Add(position);
        labelSizes.Add(new Vector2(Width - 0.10f, height));
        return label;
    }

    private void LateUpdate()
    {
        if (!fitLabels) return;
        for (int i = 0; i < labels.Count; i++)
        {
            Bounds bounds = labels[i].GetComponent<Renderer>().localBounds;
            if (bounds.size.x <= 0.001f || bounds.size.y <= 0.001f) return;
            float scale = Mathf.Min(labelSizes[i].x / bounds.size.x, labelSizes[i].y / bounds.size.y);
            labels[i].transform.localScale = Vector3.one * scale;
            labels[i].transform.localPosition = labelPositions[i] -
                new Vector3(bounds.center.x * scale, bounds.center.y * scale, 0f);
        }
        fitLabels = false;
    }

    public void SetGazeFeedback(bool active, float progress)
    {
        if (borderMaterial == null) return;
        if (focused != active)
        {
            focused = active;
            Color color = active ? Ivory : Brass;
            if (borderMaterial.HasProperty("_BaseColor")) borderMaterial.SetColor("_BaseColor", color);
            if (borderMaterial.HasProperty("_Color")) borderMaterial.SetColor("_Color", color);
        }
        float width = 0.44f * Mathf.Clamp01(progress);
        progressFill.gameObject.SetActive(active && width > 0f);
        progressFill.localScale = new Vector3(width, 0.012f, 0.008f);
        progressFill.localPosition = new Vector3(-0.22f + width * 0.5f, -0.985f, 0.15f);
    }

    private void OnEnable() { fitLabels = true; }
    private void OnDisable() { SetGazeFeedback(false, 0f); }
    private void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
