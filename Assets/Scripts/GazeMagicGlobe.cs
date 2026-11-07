using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazeMagicGlobe : MonoBehaviour
{
    private AudioSource magicSound;
    [Tooltip("The shiny cube only; the pedestal stays still.")]
    public MeshRenderer crystal;
    [Min(1f)] public float effectDuration = 6f;
    [Min(0f)] public float floatHeight = 0.12f;
    [Min(1f)] public float spinSpeed = 150f;
    public bool IsAnimating { get; private set; }
    private Vector3 restPosition;
    private Quaternion restRotation;
    private ParticleSystem sparks;
    private Light glow;
    private GameObject effects;
    private bool initialized;

    private void Reset() { FindCrystal(); }
    private void FindCrystal()
    {
        if (crystal != null) return;
        float brightest = -1f;
        foreach (MeshRenderer part in GetComponentsInChildren<MeshRenderer>())
        {
            Material material = part.sharedMaterial;
            float brightness = material != null && material.HasProperty("_EmissionColor")
                ? material.GetColor("_EmissionColor").maxColorComponent : 0f;
            if (brightness > brightest) { brightest = brightness; crystal = part; }
        }
    }

    private void Awake()
    {
        FindCrystal();
        MeshFilter mesh = crystal != null ? crystal.GetComponent<MeshFilter>() : null;
        if (mesh == null || mesh.sharedMesh == null || !crystal.transform.IsChildOf(transform))
        {
            Debug.LogError("GazeMagicGlobe: add to the globe group and assign its glowing cube to Crystal.", this);
            enabled = false;
            return;
        }
        restPosition = crystal.transform.localPosition;
        restRotation = crystal.transform.localRotation;
        if (crystal.GetComponent<Collider>() == null)
        {
            BoxCollider collider = crystal.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
        effects = new GameObject("Crystal Sparks");
        effects.transform.SetParent(transform, false);
        sparks = effects.AddComponent<ParticleSystem>();
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = sparks.main;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
        main.startColor = new Color(0.25f, 1f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = sparks.emission;
        emission.rateOverTime = 22f;
        var shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = Mathf.Max(0.03f, crystal.bounds.extents.magnitude * 0.5f);
        var size = sparks.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = mesh.sharedMesh;
        particleRenderer.sharedMaterial = crystal.sharedMaterial;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        glow = new GameObject("Crystal Glow").AddComponent<Light>();
        glow.transform.SetParent(effects.transform, false);
        glow.type = LightType.Point;
        glow.color = Color.cyan;
        glow.range = 1.5f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
        initialized = true;
    }

    public void Activate()
    {
        if (initialized && isActiveAndEnabled && !IsAnimating) StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        IsAnimating = true;
        Vector3 position = crystal.transform.position;
        Quaternion rotation = crystal.transform.rotation;
        Vector3 center = crystal.bounds.center;
        effects.transform.position = center;
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref magicSound, effects.transform, audio != null ? audio.magicGlow : null, 0.75f, true);
        sparks.Play();
        glow.enabled = true;
        float duration = Mathf.Max(1f, effectDuration);
        // Whole turns allow the final easing to return to the exact starting pose.
        float turns = Mathf.Max(1f, Mathf.Round(spinSpeed * duration / 360f));
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float progress = elapsed / duration;
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.Min(progress, 1f - progress) * 5f);
            Quaternion spin = Quaternion.AngleAxis(Mathf.SmoothStep(0f, 1f, progress) * turns * 360f, Vector3.up);
            Vector3 lift = Vector3.up * (floatHeight + Mathf.Sin(elapsed * 3f) * 0.02f) * strength;
            crystal.transform.SetPositionAndRotation(center + spin * (position - center) + lift, spin * rotation);
            effects.transform.position = center + lift;
            glow.intensity = strength * (1.3f + Mathf.Sin(elapsed * 5f) * 0.4f);
            yield return null;
        }
        Restore();
    }

    private void Restore()
    {
        GazeAudio.Stop(magicSound);
        if (initialized && crystal != null) crystal.transform.SetLocalPositionAndRotation(restPosition, restRotation);
        if (sparks != null) sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (glow != null) glow.enabled = false;
        IsAnimating = false;
    }
    private void OnDisable() { StopAllCoroutines(); Restore(); }
    private void OnDestroy() { if (effects != null) Destroy(effects); }
}
