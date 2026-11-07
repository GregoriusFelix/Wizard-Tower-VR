using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GazeDoor : MonoBehaviour
{
    public Transform doorWood;
    public Transform frontKnob;
    public Transform backKnob;
    [Tooltip("Negative 90 opens this scene's left-hinged door toward the tower interior.")]
    public float openAngle = -90f;
    [Min(0.01f)] public float openingDuration = 1f;
    [Min(0.01f)] public float knockDuration = 0.8f;
    [Tooltip("Seconds to raise the ring before the three-knock recording starts.")]
    [Min(0f)] public float knockLeadIn = 0.16f;
    [Tooltip("Times of the three impacts in Door Knock.wav, in seconds.")]
    public Vector3 knockImpactTimes = new Vector3(0.05f, 0.25f, 0.475f);
    [Range(0f, 80f)] public float ringLiftAngle = 45f;

    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }
    private Transform hinge;
    private float openingTime;
    private Transform frontRing;
    private Transform backRing;
    private Transform gazedRing;
    private Transform activeRing;
    private bool targetOpen;
    private float startAngle;
    private float endAngle;
    private AudioSource knockSound, openingSound;
    private bool openingSoundPlayed;
    private bool knockSoundPlayed;
    private float knockPlaybackStartedAt, motionDelay;
    private AudioClip knockClip;

    private static bool BelongsTo(Transform hit, Transform part)
    {
        return part != null && hit.IsChildOf(part);
    }

    private void Reset()
    {
        FindDoorParts();
    }

    private void FindDoorParts()
    {
        if (doorWood == null) doorWood = transform.Find("Door Wood");
        if (frontKnob == null) frontKnob = transform.Find("Door Knob");
        if (backKnob == null) backKnob = transform.Find("Door Knob (1)");
    }

    private void Awake()
    {
        FindDoorParts();
        if (doorWood == null || doorWood == transform || !doorWood.IsChildOf(transform))
        {
            Debug.LogError("GazeDoor: add this component to the Door group and assign its Door Wood child.", this);
            enabled = false;
            return;
        }

        Renderer[] renderers = doorWood.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            Debug.LogError("GazeDoor: Door Wood needs a visible mesh to locate its hinge.", this);
            enabled = false;
            return;
        }

        Bounds bounds = new Bounds();
        bool hasBounds = false;
        foreach (Renderer part in renderers)
        {
            Bounds meshBounds = part.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = meshBounds.center + Vector3.Scale(meshBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                point = transform.InverseTransformPoint(part.transform.TransformPoint(point));
                if (!hasBounds)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
                else bounds.Encapsulate(point);
            }
        }

        hinge = new GameObject("Door Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(bounds.min.x, bounds.center.y, bounds.center.z);
        doorWood.SetParent(hinge, true);
        if (frontKnob != null && frontKnob != transform && frontKnob.IsChildOf(transform))
            frontKnob.SetParent(hinge, true);
        if (backKnob != null && backKnob != transform && backKnob.IsChildOf(transform))
            backKnob.SetParent(hinge, true);

        frontRing = CreateRingPivot(frontKnob);
        backRing = CreateRingPivot(backKnob);
    }

    private Transform CreateRingPivot(Transform knob)
    {
        if (knob == null || !knob.IsChildOf(hinge))
            return null;

        List<Transform> segments = new List<Transform>();
        Bounds bounds = new Bounds();
        bool hasBounds = false;
        Transform mount = knob.Find("Door Handle");
        foreach (Transform child in knob)
        {
            if (child == mount)
                continue;

            Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                continue;

            segments.Add(child);
            foreach (Renderer part in renderers)
            {
                Bounds mesh = part.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = mesh.center + Vector3.Scale(mesh.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                    point = knob.InverseTransformPoint(part.transform.TransformPoint(point));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                    else bounds.Encapsulate(point);
                }
            }
        }

        if (!hasBounds)
            return null;

        Vector3 attachment = mount != null
            ? new Vector3(mount.localPosition.x, mount.localPosition.y, bounds.center.z)
            : new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        Transform pivot = new GameObject(knob.name + " Ring Pivot").transform;
        // Parent to the unscaled door hinge: the knob groups have compressed Z scales.
        pivot.SetParent(hinge, false);
        pivot.SetPositionAndRotation(knob.TransformPoint(attachment), knob.rotation);
        foreach (Transform segment in segments)
            segment.SetParent(pivot, true);
        return pivot;
    }

    public bool CanGazeAt(Transform hit)
    {
        if (!isActiveAndEnabled || hinge == null)
            return false;

        if (BelongsTo(hit, frontKnob) || BelongsTo(hit, frontRing))
        {
            gazedRing = frontRing;
            return true;
        }
        if (BelongsTo(hit, backKnob) || BelongsTo(hit, backRing))
        {
            gazedRing = backRing;
            return true;
        }
        return false;
    }

    public void Open()
    {
        StartMotion(true);
    }

    public void Toggle()
    {
        StartMotion(!IsOpen);
    }

    private void StartMotion(bool opening)
    {
        if (!isActiveAndEnabled || IsAnimating || IsOpen == opening || hinge == null)
            return;

        targetOpen = opening;
        startAngle = IsOpen ? openAngle : 0f;
        endAngle = opening ? openAngle : 0f;
        IsAnimating = true;
        openingTime = 0f;
        activeRing = gazedRing != null ? gazedRing : frontRing != null ? frontRing : backRing;
        openingSoundPlayed = false;
        knockSoundPlayed = false;
        knockPlaybackStartedAt = 0f;
        GazeAudio.Stop(knockSound);
        GazeAudio.Stop(openingSound);
        InteractionAudio audio = InteractionAudio.Current;
        knockClip = audio != null ? audio.doorKnock : null;
        float recordingDuration = knockClip != null ? knockClip.length : 0.706f;
        motionDelay = activeRing != null ? Mathf.Max(knockDuration,
            Mathf.Max(0f, knockLeadIn) + Mathf.Max(recordingDuration, KnockTimes().z)) : 0f;
        if (activeRing != null)
            restingRingRotation = activeRing.localRotation;
    }

    private Quaternion restingRingRotation;

    private void Update()
    {
        if (!IsAnimating || hinge == null)
            return;

        openingTime += Time.deltaTime;
        float leadIn = Mathf.Max(0f, knockLeadIn);
        if (activeRing != null && !knockSoundPlayed && openingTime >= leadIn)
        {
            knockSoundPlayed = true;
            knockPlaybackStartedAt = openingTime;
            // The recording contains three impacts, so play it once for all three strikes.
            GazeAudio.Play(ref knockSound, activeRing, knockClip);
            float recordingDuration = knockClip != null ? knockClip.length : 0.706f;
            motionDelay = Mathf.Max(motionDelay, openingTime + Mathf.Max(recordingDuration, KnockTimes().z));
        }
        float delay = motionDelay;
        if (!openingSoundPlayed && openingTime >= delay)
        {
            openingSoundPlayed = true;
            GazeAudio.Stop(knockSound);
            InteractionAudio audio = InteractionAudio.Current;
            GazeAudio.Play(ref openingSound, hinge, audio != null ? audio.doorOpen : null);
        }
        if (activeRing != null)
        {
            float knockTime = openingTime;
            if (knockSoundPlayed)
            {
                float clipTime = knockSound != null && knockSound.isPlaying ? knockSound.time
                    : openingTime - knockPlaybackStartedAt;
                knockTime = leadIn + clipTime;
            }
            float lift = RingLift(knockTime);
            activeRing.localRotation = restingRingRotation *
                Quaternion.AngleAxis(ringLiftAngle * lift, Vector3.right);
        }

        float fraction = Mathf.Clamp01((openingTime - delay) / Mathf.Max(0.01f, openingDuration));
        hinge.localRotation = Quaternion.AngleAxis(
            Mathf.SmoothStep(startAngle, endAngle, fraction), Vector3.up);
        if (fraction >= 1f)
        {
            if (activeRing != null) activeRing.localRotation = restingRingRotation;
            IsOpen = targetOpen;
            IsAnimating = false;
        }
    }

    private Vector3 KnockTimes()
    {
        float first = Mathf.Max(0.01f, knockImpactTimes.x);
        float second = Mathf.Max(first + 0.05f, knockImpactTimes.y);
        return new Vector3(first, second, Mathf.Max(second + 0.05f, knockImpactTimes.z));
    }

    private float RingLift(float time)
    {
        Vector3 impacts = KnockTimes();
        float start = 0f;
        for (int i = 0; i < 3; i++)
        {
            float impact = Mathf.Max(0f, knockLeadIn) + impacts[i];
            if (time < impact)
            {
                float phase = Mathf.Clamp01((time - start) / (impact - start));
                return phase < 0.6f ? Mathf.SmoothStep(0f, 1f, phase / 0.6f)
                    : 1f - Mathf.SmoothStep(0f, 1f, (phase - 0.6f) / 0.4f);
            }
            start = impact;
        }
        return 0f;
    }

    private void OnDisable()
    {
        GazeAudio.Stop(knockSound);
        GazeAudio.Stop(openingSound);
        if (!IsAnimating) return;
        if (activeRing != null) activeRing.localRotation = restingRingRotation;
        if (hinge != null)
            hinge.localRotation = Quaternion.AngleAxis(IsOpen ? openAngle : 0f, Vector3.up);
        IsAnimating = false;
    }
}
