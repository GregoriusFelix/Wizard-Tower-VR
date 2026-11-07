using UnityEngine;

public class GazeTorchTarget : MonoBehaviour
{
    public TorchFire source;
    public int sourceIndex;
    public GazeCarryTorch portable;
    public Transform GazeRoot => portable != null ? portable.transform : source.torchHeads[sourceIndex].transform;

    public static void Bind(Transform root, TorchFire source, int index, GazeCarryTorch portable)
    {
        GazeTorchTarget target = root.GetComponent<GazeTorchTarget>();
        if (target == null) target = root.gameObject.AddComponent<GazeTorchTarget>();
        target.source = source;
        target.sourceIndex = index;
        target.portable = portable;
        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
    }
}
