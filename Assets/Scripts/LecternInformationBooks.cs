using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class LecternInformationBooks : MonoBehaviour
{
    private Transform root;
    private Transform[] books;
    private Vector3[] positions;
    private readonly Material[] materials = new Material[6];
    private Transform viewer;
    private Light glow;

    public void Initialize(Material reference, int count)
    {
        if (root != null) return;
        root = new GameObject("Floating Information Books - Cubes Only").transform;
        // Keep the books outside the camera hierarchy and the lectern's scale.
        Color[] colors = { new Color(0.16f, 0.23f, 0.34f), new Color(0.26f, 0.16f, 0.32f),
            new Color(0.12f, 0.28f, 0.27f), new Color(0.95f, 0.9f, 0.76f),
            new Color(0.2f, 0.22f, 0.26f), new Color(0.45f, 0.75f, 0.8f) };
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = new Material(reference) { name = "Color Only Information Book " + i };
            foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
            material.SetColor("_BaseColor", colors[i]);
            material.SetColor("_Color", colors[i]);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.15f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", colors[i] * (i == 5 ? 0.7f : i == 3 ? 0.2f : 0.025f));
            materials[i] = material;
        }
        books = new Transform[Mathf.Clamp(count, 3, 12)];
        positions = new Vector3[books.Length];
        for (int i = 0; i < books.Length; i++)
        {
            Transform book = new GameObject("Floating Open Book " + (i + 1)).transform;
            book.SetParent(root, false);
            books[i] = book;
            Cube("Spine", book, Vector3.zero, new Vector3(0.018f, 0.25f, 0.035f), materials[i % 3]);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? -1f : 1f;
                Transform page = new GameObject("Open Page").transform;
                page.SetParent(book, false);
                page.localPosition = Vector3.right * sign * 0.085f;
                page.localRotation = Quaternion.Euler(0f, sign * 12f, 0f);
                Cube("Cover", page, new Vector3(0f, 0f, 0.012f), new Vector3(0.17f, 0.25f, 0.016f), materials[i % 3]);
                Cube("Paper", page, Vector3.zero, new Vector3(0.155f, 0.232f, 0.012f), materials[3]);
                Cube("Heading", page, new Vector3(-0.015f, 0.075f, -0.008f), new Vector3(0.095f, 0.012f, 0.004f), materials[5]);
                for (int line = 0; line < 3; line++)
                {
                    float width = 0.11f - ((line + i + side) % 3) * 0.018f;
                    Cube("Information Line", page, new Vector3(-0.06f + width * 0.5f, 0.035f - line * 0.035f, -0.008f),
                        new Vector3(width, 0.005f, 0.004f), materials[4]);
                }
            }
        }
        glow = new GameObject("Soft Information Book Light").AddComponent<Light>();
        glow.transform.SetParent(root, false);
        glow.type = LightType.Point;
        glow.color = new Color(0.8f, 0.9f, 1f);
        glow.intensity = 0.4f;
        glow.range = 2f;
        glow.shadows = LightShadows.None;
        Hide();
    }
    private static void Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.GetComponent<Collider>().enabled = false;
        Destroy(cube.GetComponent<Collider>());
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }
    public void Arrange(Transform cameraTransform, float radius)
    {
        viewer = cameraTransform;
        Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        Vector3 center = viewer.position;
        GazeTeleport gaze = viewer.GetComponent<GazeTeleport>();
        for (int i = 0; i < books.Length; i++)
        {
            float angle = Mathf.Lerp(-140f, 140f, i / (float)(books.Length - 1));
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            direction.y = i % 2 == 0 ? -0.14f : 0.1f;
            direction.Normalize();
            float distance = Mathf.Max(0.5f, radius);
            foreach (RaycastHit hit in Physics.SphereCastAll(center, 0.16f, direction, distance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(viewer)
                    || (gaze != null && gaze.player != null && hit.collider.transform.IsChildOf(gaze.player))) continue;
                distance = Mathf.Min(distance, Mathf.Max(0.4f, hit.distance - 0.08f));
            }
            positions[i] = center + direction * distance;
            books[i].position = positions[i];
            books[i].localScale = Vector3.zero;
        }
        glow.transform.position = center + forward * 0.4f;
        root.gameObject.SetActive(true);
    }
    public void Animate(float time, float weight)
    {
        if (root == null || viewer == null) return;
        root.gameObject.SetActive(weight > 0.001f);
        for (int i = 0; i < books.Length; i++)
        {
            float appear = Mathf.SmoothStep(0f, 1f, (time - 0.15f - i * 0.09f) / 0.4f);
            float size = appear * weight;
            books[i].localScale = Vector3.one * size;
            books[i].position = positions[i] + Vector3.up * (Mathf.Sin(time * 1.2f + i) * 0.012f * weight);
            Vector3 facing = books[i].position - viewer.position;
            if (facing.sqrMagnitude > 0.001f)
                books[i].rotation = Quaternion.LookRotation(facing, Vector3.up);
        }
        glow.intensity = 0.4f * weight;
    }
    public void Hide() { if (root != null) root.gameObject.SetActive(false); }
    private void OnDisable() { Hide(); }
    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
