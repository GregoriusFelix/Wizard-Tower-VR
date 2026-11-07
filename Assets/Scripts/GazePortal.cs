using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazePortal : MonoBehaviour
{
    public Transform destinationPortal;
    [Tooltip("Optional feet-level marker at the destination. Overrides the automatic platform landing point.")]
    public Transform arrivalPoint;
    [Min(0f)] public float arrivalDistance = 0.45f;
    [Min(0.1f)] public float standaloneEyeHeight = 1.62f;
    [Min(0.05f)] public float fadeDuration = 0.35f;
    public Color departureColor = Color.cyan;
    public Color arrivalColor = Color.yellow;

    private Transform surface;

    private void Reset()
    {
        FindParts();
    }

    private void Awake()
    {
        FindParts();
    }

    private void FindParts()
    {
        surface = transform.Find("Portal");
        if (destinationPortal == null && gameObject.name == "Portal (Cyan)")
        {
            GameObject yellow = GameObject.Find("Portal (Yellow)");
            if (yellow != null) destinationPortal = yellow.transform;
        }
    }

    public bool CanGazeAt(Transform hit)
    {
        if (surface == null) FindParts();
        return isActiveAndEnabled && surface != null && hit.IsChildOf(surface) &&
            destinationPortal != null && destinationPortal != transform && destinationPortal.gameObject.activeInHierarchy;
    }

    public bool TryGetArrivalFeet(out Vector3 position)
    {
        position = Vector3.zero;
        if (destinationPortal == null || !destinationPortal.gameObject.activeInHierarchy)
            return false;

        if (arrivalPoint != null)
        {
            position = arrivalPoint.position;
            return true;
        }

        Transform platform = destinationPortal.Find("Floor Layer 2");
        Transform face = destinationPortal.Find("Portal");
        Renderer floorMesh = platform != null ? platform.GetComponent<Renderer>() : null;
        Renderer portalMesh = face != null ? face.GetComponent<Renderer>() : null;
        if (floorMesh == null || portalMesh == null)
        {
            Debug.LogError("GazePortal: assign an Arrival Point at floor level, or use a destination with Portal and Floor Layer 2 children.", this);
            return false;
        }

        position = portalMesh.bounds.center - destinationPortal.forward * arrivalDistance;
        position.y = floorMesh.bounds.max.y + 0.03f;
        return true;
    }

    public void DeparturePulse()
    {
        StartCoroutine(Pulse(transform, departureColor));
    }

    public void ArrivalPulse()
    {
        if (destinationPortal != null) StartCoroutine(Pulse(destinationPortal, arrivalColor));
    }

    private IEnumerator Pulse(Transform portal, Color tint)
    {
        Transform face = portal.Find("Portal");
        Renderer mesh = face != null ? face.GetComponent<Renderer>() : null;
        GameObject effect = new GameObject("Portal Transition Light");
        effect.transform.SetParent(portal, false);
        effect.transform.position = (mesh != null ? mesh.bounds.center : portal.position)
            - portal.forward * 0.4f;
        Light glow = effect.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = tint;
        glow.range = 5f;
        glow.shadows = LightShadows.None;
        glow.intensity = 0f;
        // Also schedule cleanup if the portal is disabled during its pulse.
        Destroy(effect, 1.5f);
        float elapsed = 0f;
        while (elapsed < 1f && glow != null)
        {
            elapsed += Time.unscaledDeltaTime;
            glow.intensity = Mathf.Sin(Mathf.Clamp01(elapsed) * Mathf.PI) * 5f;
            yield return null;
        }
        if (effect != null) Destroy(effect);
    }
}
