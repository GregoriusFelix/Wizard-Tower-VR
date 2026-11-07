using UnityEngine;

[CreateAssetMenu(menuName = "VR Assignment/Interaction Audio")]
public class InteractionAudio : ScriptableObject
{
    public AudioClip chain, chest, doorKnock, doorOpen, fire, pencil, pumpkin, teleport, portal, magicGlow;
    public AudioClip stir, bigWaves, asteroid, earthquake, tornado, stoneFalling, golemStomp;
    public AudioClip axeChop, magicEnhance, pouring, chair;
    [Header("Overall Interaction Volume")]
    [Tooltip("Multiplies all individual effect volumes below. Torch fire has its own independent control.")]
    [Range(0f, 1f)] public float effectsVolume = 0.7f;
    [Header("Individual Effect Volumes")]
    [Range(0f, 1f)] public float chainVolume = 1f;
    [Range(0f, 1f)] public float chestVolume = 1f;
    [Range(0f, 1f)] public float doorKnockVolume = 1f;
    [Range(0f, 1f)] public float doorOpenVolume = 1f;
    [Range(0f, 1f)] public float pencilVolume = 1f;
    [Range(0f, 1f)] public float pumpkinVolume = 1f;
    [Range(0f, 1f)] public float teleportVolume = 1f;
    [Range(0f, 1f)] public float portalVolume = 1f;
    [Range(0f, 1f)] public float magicGlowVolume = 1f;
    [Range(0f, 1f)] public float stirVolume = 1f;
    [Range(0f, 1f)] public float bigWavesVolume = 1f;
    [Range(0f, 1f)] public float asteroidVolume = 1f;
    [Range(0f, 1f)] public float earthquakeVolume = 1f;
    [Range(0f, 1f)] public float tornadoVolume = 1f;
    [Range(0f, 1f)] public float stoneFallingVolume = 1f;
    [Range(0f, 2f)] public float golemStompVolume = 1.4f;
    [Range(0f, 1f)] public float axeChopVolume = 1f;
    [Range(0f, 1f)] public float magicEnhanceVolume = 1f;
    [Range(0f, 1f)] public float pouringVolume = 1f;
    [Range(0f, 1f)] public float chairVolume = 1f;
    [Header("Pouring Timing")]
    [Tooltip("Skips the quiet lead-in of the supplied pouring recording.")]
    [Min(0f)] public float pouringStartTime = 4.5f;
    [Header("Golem Impact Playback")]
    [Tooltip("Keeps the arena impact audible at a distance while retaining some directional sound.")]
    [Range(0f, 1f)] public float golemStompSpatialBlend = 0.2f;
    [Tooltip("Skips the silence before the impact in the supplied stomp recording.")]
    [Min(0f)] public float golemStompStartTime = 0.75f;
    [Header("Independent Torch Ambience")]
    [Range(0f, 1f)] public float fireVolume = 0.18f;

    public float ClipVolume(AudioClip clip)
    {
        if (clip == null) return 0f;
        if (clip == chain) return chainVolume;
        if (clip == chest) return chestVolume;
        if (clip == doorKnock) return doorKnockVolume;
        if (clip == doorOpen) return doorOpenVolume;
        if (clip == pencil) return pencilVolume;
        if (clip == pumpkin) return pumpkinVolume;
        if (clip == teleport) return teleportVolume;
        if (clip == portal) return portalVolume;
        if (clip == magicGlow) return magicGlowVolume;
        if (clip == stir) return stirVolume;
        if (clip == bigWaves) return bigWavesVolume;
        if (clip == asteroid) return asteroidVolume;
        if (clip == earthquake) return earthquakeVolume;
        if (clip == tornado) return tornadoVolume;
        if (clip == stoneFalling) return stoneFallingVolume;
        if (clip == golemStomp) return golemStompVolume;
        if (clip == axeChop) return axeChopVolume;
        if (clip == magicEnhance) return magicEnhanceVolume;
        if (clip == pouring) return pouringVolume;
        if (clip == chair) return chairVolume;
        return 1f;
    }
    private static InteractionAudio current;
    public static InteractionAudio Current
    {
        get
        {
            if (current == null) current = Resources.Load<InteractionAudio>("InteractionAudio");
            return current;
        }
    }
}

public static class GazeAudio
{
    public static AudioSource Create(Transform anchor, float spatialBlend = 1f, float maxDistance = 35f)
    {
        AudioSource source = new GameObject("Interaction Audio").AddComponent<AudioSource>();
        source.transform.SetParent(anchor, false);
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        return source;
    }

    public static void Play(ref AudioSource source, Transform anchor, AudioClip clip,
        float volume = 1f, bool loop = false, float spatialBlend = 1f, float startTime = 0f)
    {
        if (clip == null || anchor == null || !anchor.gameObject.activeInHierarchy) return;
        if (source == null) source = Create(anchor, spatialBlend);
        source.Stop();
        source.transform.SetParent(anchor, false);
        source.transform.localPosition = Vector3.zero;
        source.clip = clip;
        source.loop = loop;
        source.mute = false;
        source.spatialBlend = spatialBlend;
        InteractionAudio settings = InteractionAudio.Current;
        source.volume = Mathf.Clamp01(volume * (settings != null ? settings.effectsVolume * settings.ClipVolume(clip) : 0.7f));
        source.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
        source.Play();
    }

    public static void Stop(AudioSource source)
    {
        if (source != null) source.Stop();
    }
}
