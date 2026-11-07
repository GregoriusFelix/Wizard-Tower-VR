using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class GolemStaffLightning : MonoBehaviour
{
    private const int SegmentCount = 12;
    private class Bolt
    {
        public Renderer source;
        public Transform aura;
        public Light light;
        public Material material;
        public readonly List<Transform> segments = new List<Transform>();
    }
    private readonly List<Bolt> bolts = new List<Bolt>();

    public void Initialize(Renderer[] spheres)
    {
        if (spheres == null) spheres = new Renderer[0];
        for (int index = 0; index < spheres.Length; index++)
        {
            Renderer sphere = spheres[index];
            if (sphere == null || sphere.sharedMaterial == null) continue;
            Color color = index == 0 ? new Color(0.55f, 0.82f, 1f) : new Color(0.65f, 0.35f, 1f);
            Material material = new Material(sphere.sharedMaterial) { name = sphere.transform.parent.name + " Lightning" };
            foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 4f);
            Bolt bolt = new Bolt { source = sphere, material = material };
            bolt.aura = Primitive(PrimitiveType.Sphere, "Staff Charge Glow", material);
            bolt.light = new GameObject("Staff Charge Light").AddComponent<Light>();
            bolt.light.transform.SetParent(transform, false);
            bolt.light.color = color;
            bolt.light.type = LightType.Point;
            bolt.light.range = 5f;
            bolt.light.shadows = LightShadows.None;
            for (int segment = 0; segment < SegmentCount + 6; segment++)
                bolt.segments.Add(Primitive(PrimitiveType.Cylinder, "Lightning Cylinder", material));
            bolts.Add(bolt);
        }
        if (bolts.Count != 2)
            Debug.LogWarning("GolemStaffLightning: expected both arena staff spheres; found " + bolts.Count + ". Assign Staff Spheres on the golem.", this);
        gameObject.SetActive(false);
    }

    private Transform Primitive(PrimitiveType type, string label, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = label;
        part.transform.SetParent(transform, false);
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part.transform;
    }

    public void Animate(Vector3 destination, float charge, float strength, float time)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        for (int index = 0; index < bolts.Count; index++)
        {
            Bolt bolt = bolts[index];
            bool available = bolt.source != null && bolt.source.enabled && bolt.source.gameObject.activeInHierarchy;
            bolt.aura.gameObject.SetActive(available && charge > 0.01f);
            bolt.light.enabled = available && charge > 0.01f;
            if (!available)
            {
                foreach (Transform segment in bolt.segments) segment.gameObject.SetActive(false);
                continue;
            }
            Vector3 start = bolt.source.bounds.center;
            float pulse = 1f + Mathf.Sin(time * 4f + index) * 0.035f;
            bolt.aura.position = start;
            Vector3 size = bolt.source.bounds.size;
            bolt.aura.localScale = Vector3.one * (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * charge * pulse * 1.1f);
            bolt.light.transform.position = start;
            bolt.light.intensity = charge * 2.5f;
            Vector3 end = destination + Vector3.right * (index == 0 ? -0.6f : 0.6f);
            Vector3 direction = (end - start).normalized;
            Vector3 side = Vector3.Cross(direction, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            Vector3 normal = Vector3.Cross(direction, side);
            Vector3 previous = start;
            Vector3 fork = start;
            for (int segment = 0; segment < SegmentCount; segment++)
            {
                float t = (segment + 1f) / SegmentCount;
                float envelope = Mathf.Sin(t * Mathf.PI) * 0.65f;
                Vector3 point = Vector3.Lerp(start, end, t) + envelope * (
                    side * Mathf.Sin(segment * 2.7f + time * 13f + index * 3f)
                    + normal * Mathf.Cos(segment * 3.9f - time * 11f));
                SetSegment(bolt.segments[segment], previous, point, 0.09f * strength);
                previous = point;
                if (segment == 5) fork = point;
            }
            for (int branch = 0; branch < 2; branch++)
            {
                previous = fork;
                Vector3 branchEnd = Vector3.Lerp(fork, end, 0.45f) + side * (branch == 0 ? 1.3f : -1.3f);
                for (int segment = 0; segment < 3; segment++)
                {
                    float t = (segment + 1f) / 3;
                    Vector3 point = Vector3.Lerp(fork, branchEnd, t)
                        + normal * (Mathf.Sin(time * 9f + segment * 4f + branch) * Mathf.Sin(t * Mathf.PI) * 0.3f);
                    SetSegment(bolt.segments[SegmentCount + branch * 3 + segment], previous, point, 0.035f * strength);
                    previous = point;
                }
            }
        }
    }

    private void SetSegment(Transform segment, Vector3 from, Vector3 to, float width)
    {
        segment.gameObject.SetActive(width > 0.001f);
        Vector3 direction = to - from;
        segment.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction));
        segment.localScale = new Vector3(width, direction.magnitude * 0.5f, width);
    }

    public void Hide() { gameObject.SetActive(false); }

    private void OnDestroy()
    {
        foreach (Bolt bolt in bolts) if (bolt.material != null) Destroy(bolt.material);
    }
}
