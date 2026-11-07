using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GazeEyeball : MonoBehaviour
{
    public Renderer eyeWhite;
    public Renderer pupil;
    public Renderer slit;
    [Min(1f)] public float turnSpeed = 360f;
    [Range(0, 20)] public int additionalEyeCount = 10;
    [Min(1f)] public float spawnRadius = 14f;
    [Range(0.1f, 1f)] public float spawnedEyeScale = 0.5f;

    public bool IsTracking { get; private set; }
    private Transform viewer;
    private Transform pivot;
    private Quaternion facingOffset;
    private MaterialPropertyBlock originalAppearance;
    private MaterialPropertyBlock trackingAppearance;
    private MaterialPropertyBlock originalWhiteAppearance;
    private MaterialPropertyBlock originalSlitAppearance;
    private GazeEyeAtmosphere atmosphere;
    private GazeEyeBlood blood;
    private GazeEyeSwarm swarm;

    private void Reset()
    {
        FindEyeParts();
    }

    private void FindEyeParts()
    {
        Transform white = transform.Find("Sphere");
        Transform colored = transform.Find("Sphere (1)");
        Transform centerSlit = transform.Find("Sphere (2)");
        if (eyeWhite == null && white != null) eyeWhite = white.GetComponent<Renderer>();
        if (pupil == null && colored != null) pupil = colored.GetComponent<Renderer>();
        if (slit == null && centerSlit != null) slit = centerSlit.GetComponent<Renderer>();
    }

    private void Awake()
    {
        FindEyeParts();
        if (eyeWhite == null || pupil == null || eyeWhite == pupil ||
            !eyeWhite.transform.IsChildOf(transform) || !pupil.transform.IsChildOf(transform))
        {
            Debug.LogError("GazeEyeball: assign the white sphere and colored pupil inside the Eyeball group.", this);
            enabled = false;
            return;
        }

        Vector3 center = eyeWhite.bounds.center;
        Vector3 facing = pupil.bounds.center - center;
        if (facing.sqrMagnitude < 0.0001f)
        {
            Debug.LogError("GazeEyeball: the pupil must be offset from the white sphere's center.", this);
            enabled = false;
            return;
        }

        List<Transform> parts = new List<Transform>();
        foreach (Transform child in transform)
            parts.Add(child);

        pivot = new GameObject("Eyeball Look Pivot").transform;
        pivot.SetParent(transform, false);
        pivot.position = center;
        foreach (Transform child in parts)
            child.SetParent(pivot, true);

        // Calibrate to this model's pupil direction rather than assuming it faces +Z.
        facingOffset = Quaternion.Inverse(Quaternion.LookRotation(facing, transform.up)) * pivot.rotation;
        originalAppearance = new MaterialPropertyBlock();
        trackingAppearance = new MaterialPropertyBlock();
        pupil.GetPropertyBlock(originalAppearance);
        originalWhiteAppearance = new MaterialPropertyBlock();
        eyeWhite.GetPropertyBlock(originalWhiteAppearance);
        if (slit != null)
        {
            originalSlitAppearance = new MaterialPropertyBlock();
            slit.GetPropertyBlock(originalSlitAppearance);
        }
        atmosphere = GetComponent<GazeEyeAtmosphere>();
        if (atmosphere == null) atmosphere = gameObject.AddComponent<GazeEyeAtmosphere>();
        blood = GetComponent<GazeEyeBlood>();
        if (blood == null) blood = gameObject.AddComponent<GazeEyeBlood>();
        swarm = GetComponent<GazeEyeSwarm>();
        if (swarm == null) swarm = gameObject.AddComponent<GazeEyeSwarm>();
    }

    public void ToggleTracking(Transform cameraTransform)
    {
        if (!isActiveAndEnabled || pivot == null || cameraTransform == null)
            return;

        IsTracking = !IsTracking;
        viewer = IsTracking ? cameraTransform : null;
        if (IsTracking)
        {
            SetEyeColor(pupil, Color.black, 0f);
            SetEyeColor(eyeWhite, new Color(0.95f, 0.82f, 0.8f), 0.7f);
            if (slit != null) SetEyeColor(slit, Color.white, 3f);
            if (blood != null) blood.SetBloody(true, eyeWhite, pupil);
            if (swarm != null) swarm.Spawn(pivot, pupil, eyeWhite, cameraTransform,
                additionalEyeCount, spawnRadius, spawnedEyeScale, turnSpeed);
        }
        else
        {
            RestoreAppearance();
        }
        if (atmosphere != null) atmosphere.SetTracking(IsTracking, cameraTransform.GetComponent<Camera>());
    }

    private void SetEyeColor(Renderer part, Color color, float glow)
    {
        part.GetPropertyBlock(trackingAppearance);
        trackingAppearance.SetColor("_BaseColor", color);
        trackingAppearance.SetColor("_Color", color);
        trackingAppearance.SetColor("_EmissionColor", color * glow);
        part.SetPropertyBlock(trackingAppearance);
    }

    private void RestoreAppearance()
    {
        if (swarm != null) swarm.Clear();
        if (blood != null) blood.SetBloody(false);
        if (pupil != null && originalAppearance != null) pupil.SetPropertyBlock(originalAppearance);
        if (eyeWhite != null && originalWhiteAppearance != null) eyeWhite.SetPropertyBlock(originalWhiteAppearance);
        if (slit != null && originalSlitAppearance != null) slit.SetPropertyBlock(originalSlitAppearance);
    }

    private void LateUpdate()
    {
        if (!IsTracking || pivot == null || viewer == null)
            return;

        Vector3 direction = viewer.position - pivot.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f
            ? Vector3.forward : Vector3.up;
        Quaternion desired = Quaternion.LookRotation(direction, up) * facingOffset;
        pivot.rotation = Quaternion.RotateTowards(pivot.rotation, desired, turnSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        IsTracking = false;
        viewer = null;
        RestoreAppearance();
        if (atmosphere != null) atmosphere.SetTracking(false);
    }
}
