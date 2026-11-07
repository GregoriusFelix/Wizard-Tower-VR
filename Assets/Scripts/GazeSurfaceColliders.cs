using UnityEngine;

public static class GazeSurfaceColliders
{
    // A flattened cylinder's default capsule keeps its full width as a sphere radius.
    // Match collision to the rendered mesh for flat walkable surfaces instead.
    public static int Repair()
    {
        int repaired = 0;
        foreach (CapsuleCollider capsule in Object.FindObjectsByType<CapsuleCollider>(FindObjectsSortMode.None))
        {
            if (!capsule.enabled || capsule.direction != 1 || capsule.isTrigger)
                continue;

            bool isPad = capsule.CompareTag("Teleport");
            bool isTowerFloor = capsule.name == "Floor" || capsule.name == "2nd Floor" || capsule.name == "3rd Floor";
            if (isTowerFloor)
            {
                bool insideTower = false;
                for (Transform parent = capsule.transform.parent; parent != null; parent = parent.parent)
                    if (parent.name == "Tower") insideTower = true;
                isTowerFloor = insideTower;
            }
            bool isPathOrArena = false;
            for (Transform parent = capsule.transform.parent; parent != null; parent = parent.parent)
                if (parent.name == "Rock Path" || parent.name == "Arena") isPathOrArena = true;
            if (!isPad && !isTowerFloor && !isPathOrArena)
                continue;

            MeshFilter filter = capsule.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            Vector3 scale = capsule.transform.lossyScale;
            Vector3 size = filter.sharedMesh.bounds.size;
            float width = Mathf.Min(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.z * scale.z));
            if (Mathf.Abs(size.y * scale.y) >= width * 0.25f)
                continue;

            Rigidbody body = capsule.attachedRigidbody;
            if (body != null && !body.isKinematic)
                continue;

            MeshCollider mesh = capsule.GetComponent<MeshCollider>();
            if (mesh == null) mesh = capsule.gameObject.AddComponent<MeshCollider>();
            mesh.sharedMesh = filter.sharedMesh;
            mesh.convex = false;
            mesh.isTrigger = false;
            mesh.sharedMaterial = capsule.sharedMaterial;
            mesh.includeLayers = capsule.includeLayers;
            mesh.excludeLayers = capsule.excludeLayers;
            mesh.layerOverridePriority = capsule.layerOverridePriority;
            mesh.enabled = true;
            capsule.enabled = false;
            repaired++;
        }
        if (repaired > 0) Physics.SyncTransforms();
        return repaired;
    }
}
