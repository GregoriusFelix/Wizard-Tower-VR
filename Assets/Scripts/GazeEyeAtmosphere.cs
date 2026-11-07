using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GazeEyeAtmosphere : MonoBehaviour
{
    [Min(0.05f)] public float transitionDuration = 1.2f;
    public Color nightSky = new Color(0.055f, 0.07f, 0.11f);
    public Color nightAmbient = new Color(0.22f, 0.25f, 0.32f);
    [Range(0f, 1f)] public float directionalLightMultiplier = 0.45f;
    [Min(0f)] public float nightFogDensity = 0.008f;

    private bool captured;
    private float darkness;
    private Camera viewCamera;
    private CameraClearFlags originalClearFlags;
    private Color originalBackground;
    private Material originalSkybox;
    private AmbientMode originalAmbientMode;
    private Color originalAmbient;
    private Color originalSkyAmbient;
    private Color originalEquatorAmbient;
    private Color originalGroundAmbient;
    private SphericalHarmonicsL2 originalAmbientProbe;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private bool originalFog;
    private FogMode originalFogMode;
    private Color originalFogColor;
    private float originalFogDensity;
    private Light[] lights;
    private float[] lightIntensities;
    private Volume volume;
    private VolumeProfile profile;
    private Coroutine transition;

    public void SetTracking(bool active, Camera camera = null)
    {
        if (!isActiveAndEnabled)
        {
            if (!active) Restore();
            return;
        }
        if (active && !captured)
        {
            viewCamera = camera;
            originalSkybox = RenderSettings.skybox;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbient = RenderSettings.ambientLight;
            originalSkyAmbient = RenderSettings.ambientSkyColor;
            originalEquatorAmbient = RenderSettings.ambientEquatorColor;
            originalGroundAmbient = RenderSettings.ambientGroundColor;
            originalAmbientProbe = RenderSettings.ambientProbe;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;
            originalFog = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            if (viewCamera != null)
            {
                originalClearFlags = viewCamera.clearFlags;
                originalBackground = viewCamera.backgroundColor;
            }
            lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            lightIntensities = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) lightIntensities[i] = lights[i].intensity;
            captured = true;
            CreateVolume();
        }
        if (!captured) return;
        if (transition != null) StopCoroutine(transition);
        transition = StartCoroutine(Fade(active ? 1f : 0f));
    }

    private void CreateVolume()
    {
        if (volume != null) return;
        GameObject effect = new GameObject("Eye Tracking Atmosphere");
        effect.transform.SetParent(transform, false);
        volume = effect.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1000f;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.hideFlags = HideFlags.DontSave;
        ColorAdjustments grading = profile.Add<ColorAdjustments>(true);
        grading.postExposure.Override(-0.15f);
        grading.saturation.Override(-40f);
        grading.contrast.Override(8f);
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.18f);
        vignette.smoothness.Override(0.65f);
        vignette.color.Override(Color.black);
        volume.sharedProfile = profile;
    }

    private IEnumerator Fade(float target)
    {
        float start = darkness;
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, transitionDuration);
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            darkness = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            Apply();
            yield return null;
        }
        if (target == 0f) Restore();
        transition = null;
    }

    private void Apply()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(originalAmbient, nightAmbient, darkness);
        RenderSettings.ambientIntensity = Mathf.Lerp(originalAmbientIntensity, 0.65f, darkness);
        RenderSettings.reflectionIntensity = Mathf.Lerp(originalReflectionIntensity, originalReflectionIntensity * 0.5f, darkness);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = Color.Lerp(originalFogColor, nightSky, darkness);
        float initialDensity = originalFog && originalFogMode == FogMode.ExponentialSquared ? originalFogDensity : 0f;
        RenderSettings.fogDensity = Mathf.Lerp(initialDensity, nightFogDensity, darkness);
        if (viewCamera != null)
        {
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.Lerp(originalBackground, nightSky, darkness);
        }
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null && lights[i].type == LightType.Directional)
                lights[i].intensity = Mathf.Lerp(lightIntensities[i], lightIntensities[i] * directionalLightMultiplier, darkness);
        if (volume != null) volume.weight = darkness;
    }

    private void Restore()
    {
        if (!captured) return;
        RenderSettings.skybox = originalSkybox;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbient;
        RenderSettings.ambientSkyColor = originalSkyAmbient;
        RenderSettings.ambientEquatorColor = originalEquatorAmbient;
        RenderSettings.ambientGroundColor = originalGroundAmbient;
        RenderSettings.ambientProbe = originalAmbientProbe;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        RenderSettings.fog = originalFog;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogDensity = originalFogDensity;
        if (viewCamera != null)
        {
            viewCamera.clearFlags = originalClearFlags;
            viewCamera.backgroundColor = originalBackground;
        }
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null && lights[i].type == LightType.Directional)
                lights[i].intensity = lightIntensities[i];
        if (volume != null) volume.weight = 0f;
        darkness = 0f;
        captured = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        transition = null;
        Restore();
    }

    private void OnDestroy()
    {
        Restore();
        if (volume != null) Destroy(volume.gameObject);
        if (profile != null)
        {
            foreach (VolumeComponent component in profile.components) Destroy(component);
            Destroy(profile);
        }
    }
}
