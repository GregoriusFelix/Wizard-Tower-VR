using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeWritingBook : MonoBehaviour
{
    private AudioSource writingSound;
    public Transform pencil;
    [Tooltip("One flat page on which the pencil should write.")]
    public Renderer writingSurface;
    [Tooltip("Optional marker placed exactly at the pencil's sharp tip, parented to Pencil.")]
    public Transform pencilTip;
    [Min(0.01f)] public float liftHeight = 0.15f;
    [Min(1f)] public float writingDuration = 5f;
    public bool IsWriting { get; private set; }

    private Vector3 restPosition;
    private Quaternion restRotation;
    private Vector3 localTip;
    private Transform pageFrame;
    private Bounds pageBounds;
    private bool initialized;

    private void Reset() { FindParts(); }

    private void FindParts()
    {
        if (pencil == null) pencil = transform.Find("Pencil");
        if (writingSurface != null) return;
        Transform page = transform.Find("ee (1)");
        if (page != null) writingSurface = page.GetComponent<Renderer>();
        if (writingSurface == null)
            foreach (Renderer part in GetComponentsInChildren<Renderer>())
                if (part.name.StartsWith("Paper")) { writingSurface = part; break; }
    }

    private void Awake()
    {
        FindParts();
        MeshFilter pageMesh = writingSurface != null ? writingSurface.GetComponent<MeshFilter>() : null;
        if (pencil == null || !pencil.IsChildOf(transform) || pageMesh == null || pageMesh.sharedMesh == null)
        {
            Debug.LogError("GazeWritingBook: add to Book Open and assign Pencil and a flat page to Writing Surface.", this);
            enabled = false;
            return;
        }
        restPosition = pencil.localPosition;
        restRotation = pencil.localRotation;
        if (pencilTip != null && pencilTip.IsChildOf(pencil)) localTip = pencil.InverseTransformPoint(pencilTip.position);
        else
        {
            Transform tip = pencil.Find("Cone (2)");
            if (tip == null) tip = pencil.Find("Cone (1)");
            MeshFilter tipMesh = tip != null ? tip.GetComponent<MeshFilter>() : null;
            if (tipMesh == null || tipMesh.sharedMesh == null)
            {
                Debug.LogError("GazeWritingBook: assign a Pencil Tip marker at the sharp end of the pencil.", this);
                enabled = false;
                return;
            }
            Vector3 direction = (tip.GetComponent<Renderer>().bounds.center - pencil.position).normalized;
            // Bounds also work with imported meshes that do not allow CPU vertex reads.
            Bounds tipBounds = tipMesh.sharedMesh.bounds;
            Vector3 axis = tip.InverseTransformDirection(direction);
            Vector3 end = tipBounds.center;
            if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y) && Mathf.Abs(axis.x) > Mathf.Abs(axis.z))
                end.x += Mathf.Sign(axis.x) * tipBounds.extents.x;
            else if (Mathf.Abs(axis.y) > Mathf.Abs(axis.z))
                end.y += Mathf.Sign(axis.y) * tipBounds.extents.y;
            else end.z += Mathf.Sign(axis.z) * tipBounds.extents.z;
            localTip = pencil.InverseTransformPoint(tip.TransformPoint(end));
        }
        Bounds bounds = pageMesh.sharedMesh.bounds;
        Vector3 size = Vector3.Scale(bounds.size, writingSurface.transform.lossyScale);
        Vector3 normalAxis = size.x < size.y && size.x < size.z ? Vector3.right
            : size.z < size.y ? Vector3.forward : Vector3.up;
        Vector3 normal = writingSurface.transform.TransformDirection(normalAxis);
        if (Vector3.Dot(normal, Vector3.up) < 0f) normal = -normal;
        // This book's printed lines run along page -Z; page +X points down the page.
        Vector3 along = writingSurface.transform.TransformDirection(normalAxis == Vector3.up ? Vector3.right : Vector3.up);
        pageFrame = new GameObject("Pencil Writing Frame").transform;
        pageFrame.SetParent(transform, false);
        pageFrame.SetPositionAndRotation(writingSurface.bounds.center, Quaternion.LookRotation(along, normal));
        pageBounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            pageBounds.Encapsulate(pageFrame.InverseTransformPoint(writingSurface.transform.TransformPoint(corner)));
        }
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
            if (mesh.sharedMesh != null && mesh.GetComponent<Collider>() == null)
            {
                BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.sharedMesh.bounds.center;
                collider.size = mesh.sharedMesh.bounds.size;
            }
        initialized = true;
    }

    public void Write()
    {
        if (initialized && isActiveAndEnabled && !IsWriting) StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        IsWriting = true;
        Vector3 restWorld = pencil.position;
        Quaternion restWorldRotation = pencil.rotation;
        yield return Move(restWorld + pageFrame.up * liftHeight, restWorldRotation, 0.5f);
        Vector3 direction = pencil.TransformVector(localTip).normalized;
        Vector3 writingDirection = (-pageFrame.up + pageFrame.right * 0.3f + pageFrame.forward * 0.2f).normalized;
        Quaternion writingRotation = Quaternion.FromToRotation(direction, writingDirection) * pencil.rotation;
        Vector3 first = WritingPoint(0f);
        yield return Move(TipPosition(first, writingRotation), writingRotation, 0.6f);
        float duration = Mathf.Max(1f, writingDuration);
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref writingSound, pageFrame, audio != null ? audio.pencil : null, 0.65f, true);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (writingSound != null) writingSound.mute = Mathf.Repeat(elapsed / duration * 5f, 1f) >= 0.82f;
            pencil.rotation = writingRotation * Quaternion.AngleAxis(Mathf.Sin(elapsed * 12f) * 3f, Vector3.forward);
            pencil.position = WritingPoint(elapsed / duration) - pencil.TransformVector(localTip);
            yield return null;
        }
        GazeAudio.Stop(writingSound);
        yield return Move(pencil.position + pageFrame.up * liftHeight, pencil.rotation, 0.4f);
        yield return Move(pencil.parent.TransformPoint(restPosition), pencil.parent.rotation * restRotation, 0.7f);
        Restore();
    }

    private Vector3 WritingPoint(float progress)
    {
        float rowProgress = Mathf.Min(progress, 0.9999f) * 5f;
        int row = Mathf.FloorToInt(rowProgress);
        float stroke = rowProgress - row;
        // Lift the tip during each short return to the start of the next line.
        float x = stroke < 0.82f ? stroke / 0.82f : 1f - (stroke - 0.82f) / 0.18f;
        float lift = stroke < 0.82f ? 0f : Mathf.Sin((stroke - 0.82f) / 0.18f * Mathf.PI) * 0.04f;
        float z = (row + (stroke < 0.82f ? 0f : (stroke - 0.82f) / 0.18f)) / 5f;
        return pageFrame.TransformPoint(new Vector3(
            Mathf.Lerp(pageBounds.min.x, pageBounds.max.x, Mathf.Lerp(0.18f, 0.82f, x)),
            pageBounds.max.y + 0.004f + lift,
            Mathf.Lerp(pageBounds.min.z, pageBounds.max.z, Mathf.Lerp(0.22f, 0.78f, z))
                + Mathf.Sin(stroke * 90f) * pageBounds.size.z * 0.012f));
    }

    private Vector3 TipPosition(Vector3 target, Quaternion rotation)
    {
        Quaternion previous = pencil.rotation;
        pencil.rotation = rotation;
        Vector3 position = target - pencil.TransformVector(localTip);
        pencil.rotation = previous;
        return position;
    }

    private IEnumerator Move(Vector3 destination, Quaternion rotation, float duration)
    {
        Vector3 start = pencil.position;
        Quaternion startRotation = pencil.rotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / duration);
            pencil.SetPositionAndRotation(Vector3.Lerp(start, destination, weight), Quaternion.Slerp(startRotation, rotation, weight));
            yield return null;
        }
        pencil.SetPositionAndRotation(destination, rotation);
    }

    private void Restore()
    {
        GazeAudio.Stop(writingSound);
        if (initialized && pencil != null) pencil.SetLocalPositionAndRotation(restPosition, restRotation);
        IsWriting = false;
    }

    private void OnDisable() { StopAllCoroutines(); Restore(); }
    private void OnDestroy() { if (pageFrame != null) Destroy(pageFrame.gameObject); }
}
