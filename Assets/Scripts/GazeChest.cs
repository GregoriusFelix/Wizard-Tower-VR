using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeChest : MonoBehaviour
{
    public Transform lid;
    public Material latchMaterial;
    public Vector3 hingeAxis = Vector3.forward;
    public float openAngle = 105f;
    [Min(0.1f)] public float openingDuration = 1.15f;
    [Tooltip("Time of the closing thud within the chest clip, used to align it with the shut lid.")]
    [Min(0f)] public float closingSoundImpactTime = 0.45f;
    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool CanGaze => initialized && isActiveAndEnabled;

    private Transform hinge, originalParent;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation;
    private bool initialized, generatedLid;
    private AudioSource openingSound;

    private void Awake()
    {
        if (lid == null)
            foreach (Transform part in GetComponentsInChildren<Transform>(true))
                if (part != transform && part.name == "Top") { lid = part; break; }
        Bounds body = new Bounds();
        bool found = false;
        Renderer wood = null;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (lid != null && renderer.transform.IsChildOf(lid)) continue;
            bool contents = false;
            for (Transform parent = renderer.transform; parent != null && parent != transform; parent = parent.parent)
                if (parent.name.StartsWith("Potion")) contents = true;
            if (contents) continue;
            Bounds bounds = BoundsInChest(renderer);
            if (!found) { body = bounds; found = true; wood = renderer; }
            else body.Encapsulate(bounds);
        }
        if (!found || wood == null || wood.sharedMaterial == null)
        {
            Debug.LogError("GazeChest: assign this component to the chest and its Top lid group.", this);
            enabled = false;
            return;
        }
        if (lid == null) CreateLid(body, wood.sharedMaterial);
        if (lid == transform || !lid.IsChildOf(transform))
        {
            Debug.LogError("GazeChest: the lid must belong to this chest.", this);
            enabled = false;
            return;
        }
        originalParent = lid.parent;
        originalPosition = lid.localPosition;
        originalRotation = lid.localRotation;
        originalScale = lid.localScale;
        // The authored Top may be saved open; start gameplay with it fitted to the box.
        lid.rotation = transform.rotation;
        Bounds closedBounds = new Bounds();
        bool hasLidBounds = false;
        foreach (Renderer renderer in lid.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == "ew (10)" || renderer.name.Contains("Clasp") || renderer.name.Contains("Latch")) continue;
            Bounds bounds = BoundsInChest(renderer);
            if (!hasLidBounds) { closedBounds = bounds; hasLidBounds = true; } else closedBounds.Encapsulate(bounds);
        }
        if (hasLidBounds)
        {
            Vector3 fit = new Vector3(body.center.x - closedBounds.center.x,
                body.max.y + 0.006f - closedBounds.min.y, body.center.z - closedBounds.center.z);
            lid.position += transform.TransformVector(fit);
        }
        hinge = new GameObject("Chest Lid Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = Mathf.Abs(hingeAxis.z) >= Mathf.Abs(hingeAxis.x)
            ? new Vector3(body.min.x + 0.025f, body.max.y + 0.035f, body.center.z)
            : new Vector3(body.center.x, body.max.y + 0.035f, body.min.z + 0.025f);
        lid.SetParent(hinge, true);
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
        initialized = true;
    }

    private Bounds BoundsInChest(Renderer renderer)
    {
        Bounds local = renderer.localBounds;
        Bounds bounds = new Bounds();
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 p = local.center + Vector3.Scale(local.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            p = transform.InverseTransformPoint(renderer.transform.TransformPoint(p));
            if (corner == 0) bounds = new Bounds(p, Vector3.zero); else bounds.Encapsulate(p);
        }
        return bounds;
    }

    private void CreateLid(Bounds body, Material wood)
    {
        generatedLid = true;
        lid = new GameObject("Top").transform;
        lid.SetParent(transform, false);
        lid.localPosition = new Vector3(body.center.x, body.max.y + 0.045f, body.center.z);
        Cube("Wood Lid", Vector3.zero, new Vector3(body.size.x, 0.09f, body.size.z), wood);
        Cube("Front Lid Rail", new Vector3(0f, 0.055f, body.size.z * 0.5f - 0.035f), new Vector3(body.size.x, 0.06f, 0.07f), wood);
        Cube("Back Lid Rail", new Vector3(0f, 0.055f, -body.size.z * 0.5f + 0.035f), new Vector3(body.size.x, 0.06f, 0.07f), wood);
        Cube("Gold Clasp", new Vector3(body.size.x * 0.5f + 0.025f, -0.04f, 0f), new Vector3(0.065f, 0.21f, 0.14f), latchMaterial != null ? latchMaterial : wood);
    }

    private void Cube(string label, Vector3 position, Vector3 size, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = label;
        cube.transform.SetParent(lid, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }

    public void Toggle()
    {
        if (CanGaze && !IsAnimating) StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        IsAnimating = true;
        InteractionAudio audio = InteractionAudio.Current;
        bool soundPlayed = !IsOpen;
        if (soundPlayed) GazeAudio.Play(ref openingSound, hinge, audio != null ? audio.chest : null);
        float closingSoundStart = Mathf.Max(0f, openingDuration - closingSoundImpactTime);
        Quaternion from = hinge.localRotation;
        Vector3 axis = hingeAxis.sqrMagnitude > 0.001f ? hingeAxis.normalized : Vector3.forward;
        Quaternion to = Quaternion.AngleAxis(IsOpen ? 0f : openAngle, axis);
        for (float time = 0f; time < openingDuration; time += Time.deltaTime)
        {
            if (!soundPlayed && time >= closingSoundStart)
            {
                GazeAudio.Play(ref openingSound, hinge, audio != null ? audio.chest : null);
                soundPlayed = true;
            }
            float t = Mathf.SmoothStep(0f, 1f, time / openingDuration);
            hinge.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }
        hinge.localRotation = to;
        if (!soundPlayed) GazeAudio.Play(ref openingSound, hinge, audio != null ? audio.chest : null);
        IsOpen = !IsOpen;
        IsAnimating = false;
    }

    private void OnDisable()
    {
        GazeAudio.Stop(openingSound);
        StopAllCoroutines();
        if (hinge != null) hinge.localRotation = Quaternion.identity;
        IsOpen = IsAnimating = false;
    }

    private void OnDestroy()
    {
        if (lid != null && !generatedLid && originalParent != null)
        {
            lid.SetParent(originalParent, false);
            lid.SetLocalPositionAndRotation(originalPosition, originalRotation);
            lid.localScale = originalScale;
        }
        else if (generatedLid && lid != null) Destroy(lid.gameObject);
        if (hinge != null) Destroy(hinge.gameObject);
    }
}
