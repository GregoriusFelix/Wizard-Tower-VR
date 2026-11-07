using UnityEngine;

[DisallowMultipleComponent]
public class GazeCauldronFuel : MonoBehaviour
{
    [Tooltip("Optional marker under the pot, at the middle of the firewood.")]
    public Transform fuelPoint;
    [Min(1f)] public float secondsPerLog = 30f;
    [Min(1f)] public float fullFuelFlameSize = 2.2f;
    [Min(1f)] public float fullFuelEmission = 4f;
    public float FuelSecondsRemaining { get; private set; }
    private ParticleSystem[] fires;
    private float[] originalRates;
    private float[] originalSizes;
    private float[] originalSpeeds;
    private Transform zone;
    private Light fireLight;
    private int deliveredCount;

    private void Awake()
    {
        fires = GetComponentsInChildren<ParticleSystem>();
        originalRates = new float[fires.Length];
        originalSizes = new float[fires.Length];
        originalSpeeds = new float[fires.Length];
        for (int i = 0; i < fires.Length; i++)
        {
            originalRates[i] = fires[i].emission.rateOverTimeMultiplier;
            originalSizes[i] = fires[i].main.startSizeMultiplier;
            originalSpeeds[i] = fires[i].main.startSpeedMultiplier;
        }
        Vector3 center = transform.position - Vector3.up * 0.6f;
        if (fires.Length > 0)
        {
            center = Vector3.zero;
            foreach (ParticleSystem fire in fires) center += fire.transform.position;
            center /= fires.Length;
        }
        zone = new GameObject("Gaze Firewood Target").transform;
        zone.SetParent(transform, false);
        zone.position = fuelPoint != null ? fuelPoint.position : center;
        BoxCollider collider = zone.gameObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        Vector3 scale = zone.lossyScale;
        collider.size = new Vector3(1.1f / Mathf.Max(0.01f, Mathf.Abs(scale.x)), 0.5f / Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            1.1f / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
        fireLight = new GameObject("Fuel Fire Light").AddComponent<Light>();
        fireLight.transform.SetParent(zone, false);
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.3f, 0.035f);
        fireLight.range = 3.5f;
        fireLight.shadows = LightShadows.None;
        fireLight.enabled = false;
    }
    public bool CanGazeAt(Transform hit, Vector3 hitPoint)
    {
        if (!isActiveAndEnabled || zone == null) return false;
        if (hit == zone) return true;
        Vector3 offset = hitPoint - zone.position;
        return Mathf.Abs(offset.y) < 0.35f && new Vector2(offset.x, offset.z).magnitude < 0.8f;
    }
    public bool AddFuel(GazeFuelLog log)
    {
        if (!isActiveAndEnabled || log == null) return false;
        Vector3 center = fuelPoint != null ? fuelPoint.position : zone.position;
        int slot = deliveredCount % 6;
        Vector3 placement = center + new Vector3((slot % 3 - 1) * 0.15f, -0.07f + (slot / 3) * 0.09f, 0f);
        float burnTime = FuelSecondsRemaining + Mathf.Max(1f, secondsPerLog);
        if (!log.Deliver(placement, Quaternion.Euler(0f, slot % 2 == 0 ? 35f : -35f, 0f), burnTime)) return false;
        deliveredCount++;
        FuelSecondsRemaining = burnTime;
        foreach (ParticleSystem fire in fires) if (fire != null && !fire.isPlaying) fire.Play();
        return true;
    }
    private void Update()
    {
        FuelSecondsRemaining = Mathf.Max(0f, FuelSecondsRemaining - Time.deltaTime);
        float fadeOut = Mathf.Clamp01(FuelSecondsRemaining / 5f);
        float fuelLoad = Mathf.Clamp01(FuelSecondsRemaining / (Mathf.Max(1f, secondsPerLog) * 3f));
        float strength = fadeOut * (0.45f + 0.55f * fuelLoad);
        for (int i = 0; i < fires.Length; i++)
        {
            if (fires[i] == null) continue;
            var emission = fires[i].emission;
            emission.rateOverTimeMultiplier = originalRates[i] * Mathf.Lerp(1f, fullFuelEmission, strength);
            var main = fires[i].main;
            main.startSizeMultiplier = originalSizes[i] * Mathf.Lerp(1f, fullFuelFlameSize, strength);
            main.startSpeedMultiplier = originalSpeeds[i] * Mathf.Lerp(1f, 1.3f, strength);
        }
        if (fireLight != null)
        {
            fireLight.enabled = strength > 0f;
            fireLight.intensity = strength * (5f + Mathf.Sin(Time.time * 13f) * 0.4f);
        }
    }
    private void OnDisable()
    {
        if (fires != null)
            for (int i = 0; i < fires.Length; i++)
            {
                if (fires[i] == null) continue;
                var emission = fires[i].emission;
                emission.rateOverTimeMultiplier = originalRates[i];
                var main = fires[i].main;
                main.startSizeMultiplier = originalSizes[i];
                main.startSpeedMultiplier = originalSpeeds[i];
            }
        if (fireLight != null) fireLight.enabled = false;
    }
    private void OnDestroy() { if (zone != null) Destroy(zone.gameObject); }
}
