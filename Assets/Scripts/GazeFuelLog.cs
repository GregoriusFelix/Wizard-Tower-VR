using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeFuelLog : MonoBehaviour
{
    public bool CanPickUp => isActiveAndEnabled && !carried && !fuelled && !settling;
    public bool IsLoose => !carried && !fuelled;
    public bool IsCarried => carried;
    private int carrySlot;
    private bool settling;
    private Rigidbody physicsBody;
    private Transform viewer;
    private bool carried;
    private bool fuelled;
    private float born;
    private Vector3 restScale;
    private Material barkMaterial;
    private Material endMaterial;

    public static GazeFuelLog Create(Vector3 position, Material bark, Material ends)
    {
        GameObject root = new GameObject("Collectable Firewood");
        Material barkCopy = new Material(bark);
        Material endsCopy = new Material(ends);
        root.transform.SetPositionAndRotation(position, Random.rotation);
        GameObject wood = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wood.transform.SetParent(root.transform, false);
        wood.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        wood.transform.localScale = new Vector3(0.19f, 0.28f, 0.19f);
        wood.GetComponent<Renderer>().sharedMaterial = barkCopy;
        wood.GetComponent<Collider>().enabled = false;
        Destroy(wood.GetComponent<Collider>());
        for (int i = 0; i < 2; i++)
        {
            GameObject end = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            end.transform.SetParent(root.transform, false);
            end.transform.localPosition = new Vector3(0f, 0f, i == 0 ? -0.282f : 0.282f);
            end.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            end.transform.localScale = new Vector3(0.17f, 0.004f, 0.17f);
            end.GetComponent<Renderer>().sharedMaterial = endsCopy;
            end.GetComponent<Collider>().enabled = false;
            Destroy(end.GetComponent<Collider>());
        }
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.direction = 2;
        collider.radius = 0.1f;
        collider.height = 0.58f;
        Rigidbody rigidbody = root.AddComponent<Rigidbody>();
        rigidbody.mass = 0.5f;
        rigidbody.isKinematic = true;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        GazeFuelLog log = root.AddComponent<GazeFuelLog>();
        log.barkMaterial = barkCopy;
        log.endMaterial = endsCopy;
        return log;
    }
    private void Awake()
    {
        physicsBody = GetComponent<Rigidbody>();
        born = Time.time;
        restScale = transform.localScale;
    }
    public void Launch(Vector3 velocity)
    {
        physicsBody.isKinematic = true;
        StartCoroutine(Settle(velocity));
    }
    private IEnumerator Settle(Vector3 offset)
    {
        settling = true;
        Vector3 start = transform.position;
        Vector3 destination = start + Vector3.ProjectOnPlane(offset, Vector3.up);
        float ground = 0f;
        float highest = float.NegativeInfinity;
        GameObject grass = GameObject.Find("Grass");
        Renderer grassSurface = grass != null ? grass.GetComponent<Renderer>() : null;
        if (grassSurface != null) ground = grassSurface.bounds.max.y;
        foreach (RaycastHit hit in Physics.RaycastAll(destination + Vector3.up * 4f, Vector3.down, 30f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.normal.y < 0.7f || hit.collider.GetComponentInParent<GazeFuelLog>() != null
                || hit.collider.GetComponentInParent<GazeAxe>() != null
                || hit.collider.GetComponentInParent<GazeCauldronFuel>() != null) continue;
            bool obstacle = false;
            for (Transform parent = hit.collider.transform; parent != null; parent = parent.parent)
                if (parent.name == "Tree" || parent.name == "Logs") { obstacle = true; break; }
            if (obstacle) continue;
            if (hit.point.y > highest) { highest = hit.point.y; ground = hit.point.y; }
        }
        destination.y = ground + 0.105f;
        Quaternion rotation = transform.rotation;
        Quaternion landed = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(t / 0.8f);
            transform.SetPositionAndRotation(Vector3.Lerp(start, destination, progress)
                + Vector3.up * Mathf.Sin(progress * Mathf.PI) * 0.25f, Quaternion.Slerp(rotation, landed, progress));
            yield return null;
        }
        transform.SetPositionAndRotation(destination, landed);
        settling = false;
    }
    public bool PickUp(Transform cameraTransform, int slot = 0)
    {
        if (!CanPickUp || cameraTransform == null) return false;
        carried = true;
        viewer = cameraTransform;
        SetCarrySlot(slot);
        physicsBody.isKinematic = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        return true;
    }
    public void SetCarrySlot(int slot) { carrySlot = Mathf.Clamp(slot, 0, 2); }
    private void LateUpdate()
    {
        if (carried && viewer != null)
        {
            // Keep it below the reticle; do not add camera children that affect editor gizmos.
            Vector3 across = Quaternion.Euler(0f, 65f, 0f) * Vector3.right;
            Vector3 offset = new Vector3(0.28f, -0.36f, 0.85f)
                + (carrySlot == 0 ? -across * 0.105f - Vector3.up * 0.08f
                    : carrySlot == 1 ? across * 0.105f - Vector3.up * 0.08f : Vector3.up * 0.105f);
            transform.position = viewer.position + viewer.rotation * offset;
            transform.rotation = viewer.rotation * Quaternion.Euler(0f, 65f, carrySlot == 1 ? -6f : 6f);
        }
        else if (carried) Drop();
        if (!carried && !fuelled && (Time.time - born > 240f || transform.position.y < -30f)) Destroy(gameObject);
    }
    public void Drop()
    {
        if (!carried) return;
        carried = false;
        viewer = null;
        physicsBody.isKinematic = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = true;
        if (isActiveAndEnabled) Launch(Vector3.zero);
    }
    public bool Deliver(Vector3 position, Quaternion rotation, float burnAfter)
    {
        if (!carried || fuelled) return false;
        carried = false;
        viewer = null;
        fuelled = true;
        StartCoroutine(FeedFire(position, rotation, burnAfter));
        return true;
    }
    private IEnumerator FeedFire(Vector3 destination, Quaternion rotation, float burnAfter)
    {
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        for (float t = 0f; t < 0.6f; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.6f);
            transform.SetPositionAndRotation(Vector3.Lerp(start, destination, weight) + Vector3.up * Mathf.Sin(weight * Mathf.PI) * 0.2f,
                Quaternion.Slerp(startRotation, rotation, weight));
            yield return null;
        }
        transform.SetPositionAndRotation(destination, rotation);
        float duration = Mathf.Max(1f, burnAfter);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            transform.localScale = restScale * Mathf.Clamp01((duration - t) / Mathf.Min(5f, duration));
            yield return null;
        }
        Destroy(gameObject);
    }
    private void OnDisable() { if (carried) Drop(); }
    private void OnDestroy()
    {
        if (barkMaterial != null) Destroy(barkMaterial);
        if (endMaterial != null) Destroy(endMaterial);
    }
}
