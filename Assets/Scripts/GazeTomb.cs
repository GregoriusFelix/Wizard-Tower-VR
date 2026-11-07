using UnityEngine;

[DisallowMultipleComponent]
public class GazeTomb : MonoBehaviour
{
    public Renderer core;
    public Color litColor = new Color(1f, 0.015f, 0.005f);
    [Min(0f)] public float glowIntensity = 3f;
    public bool IsLit { get; private set; }

    private Material originalMaterial, glowingMaterial;
    private Color originalColor;
    private Light coreLight;

    private void Awake()
    {
        if (core == null) core = transform.Find("Center Core")?.GetComponent<Renderer>();
        if (core == null || !core.transform.IsChildOf(transform) || core.sharedMaterial == null)
        {
            Debug.LogError("GazeTomb: assign this tomb's Center Core renderer.", this);
            enabled = false;
            return;
        }
        originalMaterial = core.sharedMaterial;
        originalColor = originalMaterial.HasProperty("_BaseColor") ? originalMaterial.GetColor("_BaseColor") : originalMaterial.color;
        glowingMaterial = new Material(originalMaterial) { name = name + " Red Core" };
        foreach (string texture in glowingMaterial.GetTexturePropertyNames()) glowingMaterial.SetTexture(texture, null);
        glowingMaterial.EnableKeyword("_EMISSION");
        core.sharedMaterial = glowingMaterial;
        coreLight = new GameObject("Red Core Light").AddComponent<Light>();
        coreLight.transform.SetParent(transform, false);
        coreLight.transform.position = core.bounds.center;
        coreLight.type = LightType.Point;
        coreLight.color = litColor;
        coreLight.range = 1.6f;
        coreLight.shadows = LightShadows.None;
        SetIgnition(0f);
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
    }

    public bool CanLight(GazeCarryTorch torch)
    {
        return isActiveAndEnabled && glowingMaterial != null && core != null && core.enabled
            && core.gameObject.activeInHierarchy && !IsLit && torch != null && torch.CanLightCore(this);
    }

    public Vector3 ContactPoint(Vector3 from)
    {
        Bounds bounds = core.bounds;
        Vector3 direction = (from - bounds.center).normalized;
        return bounds.center + direction * (Mathf.Min(bounds.extents.x, bounds.extents.y, bounds.extents.z) + 0.035f);
    }

    public void SetIgnition(float progress)
    {
        if (glowingMaterial == null || coreLight == null) return;
        float t = IsLit ? 1f : Mathf.Clamp01(progress);
        Color color = Color.Lerp(originalColor, litColor, t);
        if (glowingMaterial.HasProperty("_BaseColor")) glowingMaterial.SetColor("_BaseColor", color);
        if (glowingMaterial.HasProperty("_Color")) glowingMaterial.SetColor("_Color", color);
        if (glowingMaterial.HasProperty("_EmissionColor")) glowingMaterial.SetColor("_EmissionColor", litColor * (glowIntensity * t));
        coreLight.enabled = t > 0f;
        coreLight.intensity = 2f * t;
    }

    public void Ignite()
    {
        IsLit = true;
        SetIgnition(1f);
    }

    private void OnDisable() { if (!IsLit) SetIgnition(0f); }

    private void OnDestroy()
    {
        if (core != null && core.sharedMaterial == glowingMaterial) core.sharedMaterial = originalMaterial;
        if (glowingMaterial != null) Destroy(glowingMaterial);
        if (coreLight != null) Destroy(coreLight.gameObject);
    }
}
