using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class GazeOrrery : MonoBehaviour
{
    public Transform innerOrbit;
    public Transform outerOrbit;
    public Transform meridian;
    [Min(1f)] public float spinDuration = 8f;
    public bool IsSpinning { get; private set; }
    public bool CanGaze => initialized && isActiveAndEnabled;

    private Quaternion innerRest, outerRest, meridianRest;
    private bool initialized;

    private void Awake()
    {
        if (innerOrbit == null) innerOrbit = transform.Find("Inclined Planet Orbit 1");
        if (outerOrbit == null) outerOrbit = transform.Find("Inclined Planet Orbit 2");
        if (meridian == null) meridian = transform.Find("Vertical Meridian");
        if (innerOrbit == null || outerOrbit == null || meridian == null ||
            innerOrbit.parent != transform || outerOrbit.parent != transform || meridian.parent != transform)
        {
            Debug.LogError("GazeOrrery: assign the two orbit groups and vertical meridian on the orrery table.", this);
            enabled = false;
            return;
        }
        innerRest = innerOrbit.localRotation;
        outerRest = outerOrbit.localRotation;
        meridianRest = meridian.localRotation;
        initialized = true;
    }

    public void Activate()
    {
        if (CanGaze && !IsSpinning) StartCoroutine(Spin());
    }

    private IEnumerator Spin()
    {
        IsSpinning = true;
        float duration = Mathf.Max(1f, spinDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            float t = elapsed / duration;
            // Whole revolutions with a gradual start and stop return to the authored pose.
            float turn = (t - Mathf.Sin(t * Mathf.PI * 2f) / (Mathf.PI * 2f)) * 360f;
            innerOrbit.localRotation = innerRest * Quaternion.AngleAxis(turn * 2f, Vector3.up);
            outerOrbit.localRotation = outerRest * Quaternion.AngleAxis(-turn, Vector3.up);
            meridian.localRotation = Quaternion.AngleAxis(turn, Vector3.up) * meridianRest;
            yield return null;
        }
        RestorePose();
    }

    private void RestorePose()
    {
        if (initialized)
        {
            if (innerOrbit != null) innerOrbit.localRotation = innerRest;
            if (outerOrbit != null) outerOrbit.localRotation = outerRest;
            if (meridian != null) meridian.localRotation = meridianRest;
        }
        IsSpinning = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        RestorePose();
    }
}
