using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeCarryTorch : MonoBehaviour
{
    [Min(0.2f)] public float heldLength = 0.75f;
    public Vector3 gripOffset = new Vector3(0.28f, -0.4f, 0.8f);
    [Min(0.5f)] public float lightingDistance = 4.5f;
    public bool IsHeld { get; private set; }
    public bool IsLighting { get; private set; }
    public bool IsLit => fire != null && fire.IsLit();
    public bool CanPickUp => initialized && isActiveAndEnabled && !IsHeld;
    private TorchFire fire;
    private Vector3 restPosition, restScale, localGrip, localTip;
    private Quaternion restRotation, heldRotation;
    private float modelLength;
    private Transform viewer;
    private Collider[] colliders;
    private bool[] colliderStates;
    private bool initialized;
    private GazeTomb activeTomb;

    public bool CanLightCore(GazeTomb tomb)
    {
        return isActiveAndEnabled && IsHeld && IsLit && !IsLighting && viewer != null && tomb != null && tomb.core != null
            && Vector3.Distance(viewer.position, tomb.core.bounds.center) <= lightingDistance;
    }

    public void LightCore(GazeTomb tomb)
    {
        if (tomb != null && tomb.CanLight(this)) StartCoroutine(LightTomb(tomb));
    }

    private IEnumerator LightTomb(GazeTomb tomb)
    {
        IsLighting = true;
        activeTomb = tomb;
        Vector3 startTip = transform.TransformPoint(localTip);
        Quaternion startRotation = transform.rotation;
        Vector3 contact = tomb.ContactPoint(viewer.position);
        Vector3 grip = viewer.position + viewer.rotation * gripOffset;
        Vector3 shaft = (transform.TransformPoint(localTip) - transform.TransformPoint(localGrip)).normalized;
        Quaternion touching = Quaternion.FromToRotation(shaft, (contact - grip).normalized) * startRotation;
        for (float time = 0f; time < 1.5f; time += Time.deltaTime)
        {
            if (viewer == null || tomb == null || !tomb.isActiveAndEnabled || tomb.core == null || !IsLit)
            {
                RefreshAfterTravel();
                yield break;
            }
            float reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / 0.8f));
            contact = tomb.ContactPoint(viewer.position);
            transform.rotation = Quaternion.Slerp(startRotation, touching, reach);
            Vector3 tip = Vector3.Lerp(startTip, contact, reach) + Vector3.up * (Mathf.Sin(reach * Mathf.PI) * 0.08f);
            transform.position = tip - transform.TransformVector(localTip);
            tomb.SetIgnition(Mathf.Clamp01((time - 0.8f) / 0.7f));
            yield return null;
        }
        tomb.Ignite();
        activeTomb = null;
        GazeTeleport gaze = viewer != null ? viewer.GetComponent<GazeTeleport>() : null;
        if (gaze != null && gaze.FinishTombLighting(this, tomb)) yield break;
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        for (float time = 0f; time < 0.65f; time += Time.deltaTime)
        {
            if (viewer == null) { PutBack(); yield break; }
            float weight = Mathf.SmoothStep(0f, 1f, time / 0.65f);
            HeldPose();
            transform.SetPositionAndRotation(Vector3.Lerp(position, transform.position, weight),
                Quaternion.Slerp(rotation, transform.rotation, weight));
            yield return null;
        }
        IsLighting = false;
        HeldPose();
    }

    private void CancelCoreLighting()
    {
        if (activeTomb != null) activeTomb.SetIgnition(0f);
        activeTomb = null;
    }

    public void Initialize(Renderer head, Transform shaft, TorchFire flame)
    {
        if (initialized) return;
        fire = flame;
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;
        Renderer wood = shaft.GetComponent<Renderer>();
        if (wood == null) wood = shaft.GetComponentInChildren<Renderer>();
        if (wood == null) return;
        Vector3 axis = shaft.up;
        if (Vector3.Dot(axis, head.bounds.center - wood.bounds.center) < 0f) axis = -axis;
        float halfLength = Vector3.Dot(wood.bounds.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
        Vector3 grip = wood.bounds.center - axis * halfLength * 0.4f;
        Vector3 tip = TorchFire.HeadTip(head);
        modelLength = Vector3.Distance(tip, wood.bounds.center - axis * halfLength);
        localGrip = transform.InverseTransformPoint(grip);
        localTip = transform.InverseTransformPoint(tip);
        Vector3 modelUp = Quaternion.Inverse(transform.rotation) * axis;
        heldRotation = Quaternion.FromToRotation(modelUp, new Vector3(-0.12f, 0.98f, 0.13f).normalized);
        colliders = GetComponentsInChildren<Collider>();
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++) colliderStates[i] = colliders[i].enabled;
        initialized = true;
    }
    public bool PickUp(Transform cameraTransform)
    {
        if (!CanPickUp || cameraTransform == null) return false;
        viewer = cameraTransform;
        IsHeld = true;
        transform.localScale = restScale * (heldLength / Mathf.Max(0.1f, modelLength));
        foreach (Collider collider in colliders) if (collider != null) collider.enabled = false;
        HeldPose();
        return true;
    }
    public bool CanLightFrom(TorchFire source, int index)
    {
        return IsHeld && !IsLighting && !IsLit && viewer != null && source != null && source.IsLit(index)
            && Vector3.Distance(viewer.position, source.FlamePosition(index)) <= lightingDistance;
    }
    public void LightFrom(TorchFire source, int index)
    {
        if (CanLightFrom(source, index)) StartCoroutine(TransferFlame(source, index));
    }
    private IEnumerator TransferFlame(TorchFire source, int index)
    {
        IsLighting = true;
        Vector3 startTip = transform.TransformPoint(localTip);
        Quaternion startRotation = transform.rotation;
        Vector3 target = source.FlamePosition(index);
        Vector3 grip = viewer.position + viewer.rotation * gripOffset;
        Vector3 toward = (target - grip).normalized;
        Vector3 shaft = (transform.TransformPoint(localTip) - transform.TransformPoint(localGrip)).normalized;
        Quaternion touchingRotation = Quaternion.FromToRotation(shaft, toward) * transform.rotation;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            if (viewer == null || source == null || !source.IsLit(index)) { RefreshAfterTravel(); yield break; }
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.8f);
            transform.rotation = Quaternion.Slerp(startRotation, touchingRotation, weight);
            Vector3 tip = Vector3.Lerp(startTip, source.FlamePosition(index), weight)
                + Vector3.up * Mathf.Sin(weight * Mathf.PI) * 0.08f;
            transform.position = tip - transform.TransformVector(localTip);
            yield return null;
        }
        transform.rotation = touchingRotation;
        transform.position = source.FlamePosition(index) - transform.TransformVector(localTip);
        float contact = 0f;
        while (contact < 0.4f)
        {
            contact += Time.deltaTime;
            if (source == null || !source.IsLit(index)) { RefreshAfterTravel(); yield break; }
            if (contact >= 0.2f) fire.SetLit(true);
            yield return null;
        }
        Vector3 position = transform.position;
        Quaternion rotation = transform.rotation;
        for (float t = 0f; t < 0.65f; t += Time.deltaTime)
        {
            if (viewer == null) { PutBack(); yield break; }
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.65f);
            HeldPose();
            Vector3 destination = transform.position;
            Quaternion desired = transform.rotation;
            transform.SetPositionAndRotation(Vector3.Lerp(position, destination, weight), Quaternion.Slerp(rotation, desired, weight));
            yield return null;
        }
        IsLighting = false;
        HeldPose();
    }
    private void LateUpdate()
    {
        if (!IsHeld) return;
        if (viewer == null) { PutBack(); return; }
        if (!IsLighting) HeldPose();
    }
    private void HeldPose()
    {
        if (viewer == null) return;
        transform.rotation = viewer.rotation * heldRotation;
        transform.position = viewer.position + viewer.rotation * gripOffset - transform.TransformVector(localGrip);
    }
    public void RefreshAfterTravel()
    {
        if (!IsHeld) return;
        StopAllCoroutines();
        CancelCoreLighting();
        IsLighting = false;
        HeldPose();
    }
    public void PutBack()
    {
        StopAllCoroutines();
        CancelCoreLighting();
        viewer = null;
        IsHeld = IsLighting = false;
        if (!initialized) return;
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
        transform.localScale = restScale;
        for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
    }
    public void Disappear()
    {
        PutBack();
        if (fire != null) fire.SetLit(false);
        gameObject.SetActive(false);
    }
    private void OnDisable() { PutBack(); }
    private void OnDestroy()
    {
        PutBack();
    }
}
