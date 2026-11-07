using System.Collections.Generic;
using UnityEngine;

public class GazeGolemReset : MonoBehaviour
{
    public GazeElementSelection selection;
    private Transform viewer;
    private Vector3 anchor;
    private TextMesh label;
    private Transform frame, face;
    private BoxCollider gazeCollider;
    private bool fitOnNextUpdate;
    private bool leavesArena;
    private readonly List<Material> materials = new List<Material>();

    public void Initialize(GazeElementSelection source, Material reference, bool exitArena = false)
    {
        selection = source;
        leavesArena = exitArena;
        Material border = ColorMaterial(reference, exitArena ? new Color(0.015f, 0.075f, 0.09f)
            : new Color(0.32f, 0.008f, 0.015f), 0.1f);
        Material red = ColorMaterial(reference, exitArena ? new Color(0.025f, 0.20f, 0.24f)
            : new Color(0.65f, 0.025f, 0.035f), 0.15f);
        frame = Plate("Reset Frame", new Vector3(0f, 0f, 0.025f), new Vector3(1.6f, 0.4f, 0.06f), border);
        face = Plate("Reset Face", new Vector3(0f, 0f, -0.02f), new Vector3(1.52f, 0.32f, 0.06f), red);
        label = new GameObject("Reset Label").AddComponent<TextMesh>();
        label.transform.SetParent(transform, false);
        label.transform.localPosition = new Vector3(0f, 0f, -0.065f);
        label.text = exitArena ? "LEAVE ARENA" : "RESET GOLEM";
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.GetComponent<Renderer>().sharedMaterial = label.font.material;
        label.fontSize = 64;
        label.characterSize = 0.065f;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.color = Color.white;
        gazeCollider = gameObject.AddComponent<BoxCollider>();
        FitLabel();
        Hide();
    }

    private void FitLabel()
    {
        Bounds text = label.GetComponent<Renderer>().localBounds;
        if (text.size.x <= 0.001f || text.size.y <= 0.001f) return;
        float scale = Mathf.Min(0.17f / text.size.y, 1.65f / text.size.x);
        label.transform.localScale = Vector3.one * scale;
        label.transform.localPosition = new Vector3(-text.center.x * scale, -text.center.y * scale, -0.065f);
        float width = Mathf.Max(1.3f, text.size.x * scale + 0.32f);
        float height = Mathf.Max(0.36f, text.size.y * scale + 0.18f);
        frame.localScale = new Vector3(width, height, 0.06f);
        face.localScale = new Vector3(width - 0.06f, height - 0.06f, 0.06f);
        gazeCollider.size = new Vector3(width + 0.02f, height + 0.02f, 0.18f);
    }

    private Material ColorMaterial(Material reference, Color color, float emission)
    {
        Material template = Resources.Load<Material>("ElementChoicePanel");
        Material material = new Material(template != null ? template : reference) { name = "Color Only Arena Button" };
        foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * emission);
        materials.Add(material);
        return material;
    }

    private Transform Plate(string label, Vector3 position, Vector3 size, Material material)
    {
        GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = label;
        plate.transform.SetParent(transform, false);
        plate.transform.localPosition = position;
        plate.transform.localScale = size;
        Collider collider = plate.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        plate.GetComponent<Renderer>().sharedMaterial = material;
        return plate.transform;
    }

    public bool CanGaze(GazeTeleport gaze) => selection != null &&
        (leavesArena ? selection.CanLeaveArena(gaze) : selection.CanResetEncounter(gaze));
    public void Activate(GazeTeleport gaze)
    {
        if (!CanGaze(gaze)) return;
        if (leavesArena) selection.LeaveArena(gaze);
        else selection.ResetEncounter(gaze);
    }

    public void Show(Transform camera)
    {
        viewer = camera;
        PositionInFront();
        gameObject.SetActive(true);
        FitLabel();
        fitOnNextUpdate = true;
    }

    private void PositionInFront()
    {
        Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        // The arena floor is raised, so keep the lower button near eye level rather than below the platform.
        float verticalOffset = leavesArena ? -0.45f : 0.12f;
        anchor = viewer.position + forward * 3.6f + Vector3.up * verticalOffset;
        transform.position = anchor;
        transform.rotation = Quaternion.LookRotation(anchor - viewer.position, Vector3.up);
    }

    private void Update()
    {
        if (viewer == null) { Hide(); return; }
        if (fitOnNextUpdate) { FitLabel(); fitOnNextUpdate = false; }
        if (Vector3.Distance(anchor, viewer.position) > 8f) PositionInFront();
        transform.position = anchor + Vector3.up * Mathf.Sin(Time.time * 1.6f) * 0.04f;
        transform.rotation = Quaternion.LookRotation(transform.position - viewer.position, Vector3.up);
    }

    public void Hide() { gameObject.SetActive(false); }
    private void OnDestroy() { foreach (Material material in materials) if (material != null) Destroy(material); }
}
