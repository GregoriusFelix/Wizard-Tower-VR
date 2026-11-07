using UnityEngine;
using UnityEngine.Rendering;

public class GazeEyeBlood : MonoBehaviour
{
    private GameObject veins;
    private Material material;

    public void SetBloody(bool active, Renderer white = null, Renderer iris = null)
    {
        if (active && veins == null && white != null && iris != null) Build(white, iris);
        if (veins != null) veins.SetActive(active);
    }
    private void Build(Renderer white, Renderer iris)
    {
        MeshFilter sphere = white.GetComponent<MeshFilter>();
        if (sphere == null || sphere.sharedMesh == null || white.sharedMaterial == null) return;
        Bounds bounds = sphere.sharedMesh.bounds;
        Vector3 forward = white.transform.InverseTransformDirection(iris.bounds.center - white.bounds.center).normalized;
        Vector3 up = Vector3.ProjectOnPlane(white.transform.InverseTransformDirection(transform.up), forward).normalized;
        if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(Vector3.right, forward).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        material = new Material(white.sharedMaterial) { name = "Primitive Eye Blood" };
        material.SetColor("_BaseColor", new Color(0.48f, 0.006f, 0.012f));
        material.SetColor("_Color", new Color(0.48f, 0.006f, 0.012f));
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", new Color(0.3f, 0.001f, 0.002f));
        material.SetFloat("_Smoothness", 0.6f);
        veins = new GameObject("Tracking Eye Blood - Cubes");
        veins.layer = white.gameObject.layer;
        veins.transform.SetParent(white.transform, false);
        System.Random random = new System.Random(7919);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3[] path = Path(forward, up, right, 0.7f, 0.85f, angle, 0.2f, 4, random);
            AddSegments(path, 0.004f, bounds);
            if (i % 2 == 0)
            {
                Vector3 joint = path[2];
                float theta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(joint, forward), -1f, 1f));
                float phi = Mathf.Atan2(Vector3.Dot(joint, up), Vector3.Dot(joint, right));
                Vector3[] branch = Path(forward, up, right, theta, 0.3f, phi, 0.5f, 2, random);
                branch[0] = joint;
                AddSegments(branch, 0.003f, bounds);
            }
        }
        for (int i = 0; i < 3; i++)
            AddSegments(Path(forward, up, right, 0.7f, 0.8f, -Mathf.PI * 0.5f + (i - 1) * 0.25f,
                0f, 4, random), 0.011f, bounds);
    }
    private static Vector3[] Path(Vector3 forward, Vector3 up, Vector3 right, float start,
        float length, float angle, float bend, int segments, System.Random random)
    {
        Vector3[] path = new Vector3[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float theta = start + length * t;
            float phi = angle + bend * t + ((float)random.NextDouble() - 0.5f) * 0.05f;
            path[i] = (forward * Mathf.Cos(theta) + (right * Mathf.Cos(phi) + up * Mathf.Sin(phi)) * Mathf.Sin(theta)).normalized;
        }
        return path;
    }
    private void AddSegments(Vector3[] path, float width, Bounds bounds)
    {
        Vector3 radii = bounds.extents * 1.025f;
        for (int i = 1; i < path.Length; i++)
        {
            Vector3 a = bounds.center + Vector3.Scale(path[i - 1], radii);
            Vector3 b = bounds.center + Vector3.Scale(path[i], radii);
            Vector3 midpoint = bounds.center + Vector3.Scale((path[i - 1] + path[i]).normalized, radii);
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = "Blood Vessel Cube";
            segment.layer = veins.layer;
            segment.GetComponent<Collider>().enabled = false;
            Destroy(segment.GetComponent<Collider>());
            segment.transform.SetParent(veins.transform, false);
            segment.transform.localPosition = midpoint;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            segment.transform.localScale = new Vector3(width, Vector3.Distance(a, b) * 1.03f, width * 0.7f);
            MeshRenderer renderer = segment.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
    private void OnDisable() { SetBloody(false); }
    private void OnDestroy()
    {
        if (veins != null) Destroy(veins);
        if (material != null) Destroy(material);
    }
}
