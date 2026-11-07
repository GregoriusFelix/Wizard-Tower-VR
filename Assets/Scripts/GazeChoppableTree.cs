using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeChoppableTree : MonoBehaviour
{
    public bool IsFelled { get; private set; }
    private Renderer trunk;
    private Vector3 groundBase;

    public static GazeChoppableTree Find(Transform selected, Vector3 near)
    {
        Renderer nearest = null;
        float best = selected != null ? float.PositiveInfinity : 12f;
        Renderer[] candidates = selected != null ? selected.GetComponentsInChildren<Renderer>()
            : Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer renderer in candidates)
        {
            if (!renderer.name.StartsWith("Cylinder")) continue;
            GazeChoppableTree previous = renderer.GetComponentInParent<GazeChoppableTree>();
            if (previous != null && previous.IsFelled) continue;
            bool tree = selected != null;
            for (Transform parent = renderer.transform.parent; parent != null; parent = parent.parent)
                if (parent.name == "Tree") tree = true;
            if (!tree || renderer.bounds.size.y < 1f) continue;
            float distance = Vector3.Distance(renderer.bounds.center, near);
            if (distance < best) { best = distance; nearest = renderer; }
        }
        if (nearest == null) return null;
        GazeChoppableTree existing = nearest.GetComponentInParent<GazeChoppableTree>();
        if (existing != null) return existing;
        Bounds bounds = nearest.bounds;
        List<Transform> pieces = new List<Transform> { nearest.transform };
        Transform sourceParent = nearest.transform.parent;
        if (sourceParent != null)
            foreach (Transform sibling in sourceParent)
            {
                Renderer foliage = sibling.GetComponent<Renderer>();
                if (!sibling.name.StartsWith("Cone") || foliage == null) continue;
                Vector3 offset = foliage.bounds.center - bounds.center;
                if (new Vector2(offset.x, offset.z).magnitude < Mathf.Max(bounds.size.x, bounds.size.z) * 0.7f)
                    pieces.Add(sibling);
            }
        // Use the trunk's actual foot, not its group's imported origin, as the hinge.
        GameObject root = new GameObject("Choppable Standing Tree");
        root.transform.SetParent(sourceParent, true);
        root.transform.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        foreach (Transform piece in pieces) piece.SetParent(root.transform, true);
        GazeChoppableTree result = root.AddComponent<GazeChoppableTree>();
        result.trunk = nearest;
        result.groundBase = root.transform.position;
        return result;
    }

    public Vector3 MarkPosition(Vector3 playerPosition, int hit, out Vector3 outward)
    {
        Bounds bounds = trunk.bounds;
        outward = Vector3.ProjectOnPlane(playerPosition - bounds.center, Vector3.up).normalized;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        outward = Quaternion.AngleAxis(hit == 0 ? -12f : hit == 1 ? 12f : 0f, Vector3.up) * outward;
        float radiusX = Mathf.Max(0.05f, bounds.extents.x);
        float radiusZ = Mathf.Max(0.05f, bounds.extents.z);
        float radius = 1f / Mathf.Sqrt(outward.x * outward.x / (radiusX * radiusX)
            + outward.z * outward.z / (radiusZ * radiusZ));
        Vector3 point = bounds.center + outward * (radius + 0.035f);
        point.y = bounds.min.y + Mathf.Clamp(1f + (hit - 1) * 0.14f, 0.3f, bounds.size.y - 0.2f);
        return point;
    }

    public void BeginFall(Vector3 playerPosition, System.Action finished)
    {
        if (IsFelled) return;
        IsFelled = true;
        StartCoroutine(Fall(playerPosition, finished));
    }

    private IEnumerator Fall(Vector3 playerPosition, System.Action finished)
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        Vector3 away = Vector3.ProjectOnPlane(groundBase - playerPosition, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.01f) away = Vector3.forward;
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        Quaternion initialRotation = transform.rotation;
        float groundY = groundBase.y;
        float trunkWidth = Mathf.Min(trunk.bounds.size.x, trunk.bounds.size.z);
        Renderer grass = GameObject.Find("Grass")?.GetComponent<Renderer>();
        if (grass != null) groundY = grass.bounds.max.y;
        for (float t = 0f; t < 1.6f; t += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(t / 1.6f);
            transform.position = groundBase;
            transform.rotation = Quaternion.AngleAxis(90f * progress * progress, axis) * initialRotation;
            // Keep the rotating trunk's underside on or above the ground throughout its fall.
            float penetration = groundY - trunk.bounds.min.y;
            if (penetration > 0f) transform.position += Vector3.up * penetration;
            yield return null;
        }
        transform.position = groundBase;
        transform.rotation = Quaternion.AngleAxis(90f, axis) * initialRotation;
        transform.position += Vector3.up * Mathf.Max(0f, groundY - trunk.bounds.min.y);
        // Replace the fallen tree with the collectable wood, avoiding foliage intersecting the ground.
        yield return null;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        GameObject stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "Felled Tree Stump";
        stump.transform.position = new Vector3(groundBase.x, groundY + 0.12f, groundBase.z);
        stump.transform.localScale = new Vector3(trunkWidth, 0.12f, trunkWidth);
        stump.GetComponent<Renderer>().sharedMaterial = trunk.sharedMaterial;
        Collider roundedCollider = stump.GetComponent<Collider>();
        roundedCollider.enabled = false;
        Destroy(roundedCollider);
        Mesh stumpMesh = stump.GetComponent<MeshFilter>().sharedMesh;
        BoxCollider stumpCollider = stump.AddComponent<BoxCollider>();
        stumpCollider.center = stumpMesh.bounds.center;
        stumpCollider.size = stumpMesh.bounds.size;
        finished?.Invoke();
    }
}
