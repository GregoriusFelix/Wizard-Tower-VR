using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeCauldron : MonoBehaviour
{
    public Transform spoon;
    public Renderer rim;
    public Transform contents;
    [Min(0.1f)] public float stirDuration = 6f;
    [Range(1, 8)] public int stirTurns = 3;
    [Min(0.1f)] public float floatDuration = 0.7f;
    [Range(0.05f, 0.6f)] public float stirRadiusFraction = 0.35f;
    [Range(0f, 35f)] public float spoonTilt = 15f;
    [Tooltip("Center of the spoon's broad end, in the spoon group's local coordinates.")]
    public Vector3 spoonBowlPoint = new Vector3(0f, 0.606f, 0f);

    public bool IsStirring { get; private set; }
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Transform contentsPivot;
    private Vector3 potCenter;
    private float radius;
    private float clearanceHeight;
    private bool initialized;
    private AudioSource stirringSound;

    private void Reset()
    {
        FindParts();
    }

    private void FindParts()
    {
        foreach (Transform child in transform)
        {
            if (spoon == null && (child.name.Trim() == "Couldron Spoon" || child.name.Trim() == "Cauldron Spoon"))
                spoon = child;
        }
        Transform lip = transform.Find("Cylinder (1)");
        if (rim == null && lip != null) rim = lip.GetComponent<Renderer>();
        if (contents == null) contents = transform.Find("Content");
    }

    private void Awake()
    {
        FindParts();
        if (spoon == null || spoon.parent != transform || rim == null || !rim.transform.IsChildOf(transform))
        {
            Debug.LogError("GazeCauldron: add this component to Couldron and assign its spoon and rim.", this);
            enabled = false;
            return;
        }

        restPosition = spoon.localPosition;
        restRotation = spoon.localRotation;
        Bounds lipBounds = BoundsInCauldron(rim);
        potCenter = new Vector3(lipBounds.center.x, lipBounds.max.y, lipBounds.center.z);
        radius = Mathf.Min(lipBounds.extents.x, lipBounds.extents.z);
        float spoonReach = 0f;
        foreach (Renderer part in spoon.GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = BoundsInCauldron(part);
            spoonReach = Mathf.Max(spoonReach, Vector3.Distance(restPosition, bounds.center) + bounds.extents.magnitude);
        }
        clearanceHeight = potCenter.y + Mathf.Max(spoonReach, radius) + radius * 0.25f;

        if (contents != null && contents != transform && contents != spoon && contents.IsChildOf(transform))
        {
            contentsPivot = new GameObject("Cauldron Mixing Pivot").transform;
            contentsPivot.SetParent(transform, false);
            contentsPivot.localPosition = new Vector3(potCenter.x,
                transform.InverseTransformPoint(contents.position).y, potCenter.z);
            contents.SetParent(contentsPivot, true);
        }
        initialized = true;
    }

    private Bounds BoundsInCauldron(Renderer part)
    {
        Bounds mesh = part.localBounds;
        Bounds result = new Bounds();
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = mesh.center + Vector3.Scale(mesh.extents,
                new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            point = transform.InverseTransformPoint(part.transform.TransformPoint(point));
            if (corner == 0) result = new Bounds(point, Vector3.zero);
            else result.Encapsulate(point);
        }
        return result;
    }

    public bool CanGazeAt(Transform hit)
    {
        return isActiveAndEnabled && initialized && hit != null && hit.IsChildOf(spoon);
    }

    public void StartStirring()
    {
        if (!isActiveAndEnabled || !initialized || IsStirring)
            return;

        IsStirring = true;
        StartCoroutine(Stir());
    }

    private void StirPose(float angle, out Vector3 position, out Quaternion rotation)
    {
        // This model has its broad end at +Y; invert it to put that end into the pot.
        rotation = Quaternion.AngleAxis(angle, Vector3.up) * Quaternion.Euler(0f, 0f, 180f - spoonTilt);
        Vector3 bowl = potCenter + Quaternion.AngleAxis(angle, Vector3.up) *
            new Vector3(radius * stirRadiusFraction, -radius * 0.1f, 0f);
        position = bowl - rotation * Vector3.Scale(spoonBowlPoint, spoon.localScale);
    }

    private IEnumerator Stir()
    {
        StirPose(0f, out Vector3 entry, out Quaternion stirringRotation);
        Vector3 outside = new Vector3(restPosition.x, clearanceHeight, restPosition.z);
        Vector3 above = new Vector3(entry.x, clearanceHeight, entry.z);
        float moveTime = Mathf.Max(0.1f, floatDuration);
        yield return MoveTo(outside, restRotation, moveTime);
        yield return MoveTo(above, stirringRotation, moveTime);
        yield return MoveTo(entry, stirringRotation, moveTime);

        float duration = Mathf.Max(0.1f, stirDuration);
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref stirringSound, transform, audio != null ? audio.stir : null, 0.85f, true);
        if (stirringSound != null) stirringSound.transform.localPosition = potCenter;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            // Ease the orbit in and out, completing whole turns before returning home.
            float fraction = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float angle = fraction * 360f * Mathf.Max(1, stirTurns);
            StirPose(angle, out Vector3 position, out Quaternion rotation);
            spoon.SetLocalPositionAndRotation(position, rotation);
            if (contentsPivot != null)
                contentsPivot.localRotation = Quaternion.AngleAxis(angle, Vector3.up);
            yield return null;
        }

        GazeAudio.Stop(stirringSound);
        if (contentsPivot != null) contentsPivot.localRotation = Quaternion.identity;
        yield return MoveTo(above, stirringRotation, moveTime);
        yield return MoveTo(outside, restRotation, moveTime);
        yield return MoveTo(restPosition, restRotation, moveTime);
        RestorePose();
    }

    private IEnumerator MoveTo(Vector3 destination, Quaternion rotation, float duration)
    {
        Vector3 startPosition = spoon.localPosition;
        Quaternion startRotation = spoon.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            spoon.SetLocalPositionAndRotation(Vector3.Lerp(startPosition, destination, t),
                Quaternion.Slerp(startRotation, rotation, t));
            yield return null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestorePose();
    }

    private void RestorePose()
    {
        GazeAudio.Stop(stirringSound);
        if (initialized && spoon != null)
            spoon.SetLocalPositionAndRotation(restPosition, restRotation);
        if (contentsPivot != null) contentsPivot.localRotation = Quaternion.identity;
        IsStirring = false;
    }
}
