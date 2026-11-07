using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public class TorchFire : MonoBehaviour
{
    public Renderer[] torchHeads;
    [Tooltip("Use a particle-shader material. Empty or incompatible materials fall back to the cauldron's particle material.")]
    public Material fireMaterial;
    [Min(0.05f)] public float flameHeight = 1.15f;
    [Min(0.02f)] public float flameWidth = 0.48f;
    [Min(1f)] public float flameBrightness = 2f;
    [Min(0f)] public float lightIntensity = 4.5f;
    [Min(0.1f)] public float lightRange = 5.2f;
    public Color flameColor = new Color(1f, 0.3f, 0.025f);
    public bool lightOnStart = true;

    private readonly List<Transform> fireRoots = new List<Transform>();
    private readonly List<Vector3> localAnchors = new List<Vector3>();
    private readonly List<Light> lights = new List<Light>();
    private readonly List<bool> litHeads = new List<bool>();
    private readonly List<AudioSource> fireSounds = new List<AudioSource>();
    private Material runtimeMaterial;
    private bool managesTorchGroups;

    private void Reset() { FindHeads(); }
    private void FindHeads()
    {
        List<Renderer> standing = new List<Renderer>();
        managesTorchGroups = false;
        if (torchHeads != null)
            foreach (Renderer head in torchHeads)
                if (head != null && head.transform.IsChildOf(transform) && !standing.Contains(head)) standing.Add(head);
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Cone") && child.TryGetComponent(out MeshRenderer head) && !standing.Contains(head)) standing.Add(head);
            if (!child.name.StartsWith("Torch") || !FindTorchParts(child, out Renderer groupHead, out _)) continue;
            managesTorchGroups = true;
            Vector3 scale = child.localScale;
            bool fullHeight = Mathf.Abs(scale.y) >= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.8f;
            if ((child.name == "Torch Main" || fullHeight) && !standing.Contains(groupHead)) standing.Add(groupHead);
        }
        torchHeads = standing.ToArray();
    }

    private static bool FindTorchParts(Transform group, out Renderer head, out Transform shaft)
    {
        head = null;
        shaft = null;
        foreach (Transform part in group)
        {
            if (part.name.StartsWith("Cone") && part.TryGetComponent(out Renderer renderer)) head = renderer;
            if (part.name.StartsWith("Cylinder") && part.GetComponent<Renderer>() != null) shaft = part;
        }
        return head != null && shaft != null;
    }
    private void Start()
    {
        FindHeads();
        Material reference = fireMaterial;
        if (reference != null && (reference.shader == null || !reference.shader.name.Contains("Particles"))) reference = null;
        if (reference == null)
        {
            GameObject cauldron = GameObject.Find("Couldron");
            if (cauldron == null) cauldron = GameObject.Find("Cauldron");
            if (cauldron != null)
                foreach (ParticleSystemRenderer renderer in cauldron.GetComponentsInChildren<ParticleSystemRenderer>())
                    if (renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null
                        && renderer.sharedMaterial.shader.name.Contains("Particles")) { reference = renderer.sharedMaterial; break; }
        }
        if (torchHeads == null || torchHeads.Length == 0 || reference == null)
        {
            Debug.LogError("TorchFire: assign the torch head renderers and an existing fire particle material.", this);
            enabled = false;
            return;
        }
        runtimeMaterial = new Material(reference) { name = "Torch Flame Particles" };
        foreach (string texture in runtimeMaterial.GetTexturePropertyNames()) runtimeMaterial.SetTexture(texture, null);
        Color brightness = new Color(flameBrightness, flameBrightness, flameBrightness, 1f);
        if (runtimeMaterial.HasProperty("_BaseColor")) runtimeMaterial.SetColor("_BaseColor", brightness);
        if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", brightness);
        runtimeMaterial.EnableKeyword("_EMISSION");
        if (runtimeMaterial.HasProperty("_EmissionColor"))
            runtimeMaterial.SetColor("_EmissionColor", new Color(1f, 0.48f, 0.045f) * flameBrightness);
        for (int i = 0; i < torchHeads.Length; i++)
        {
            Renderer head = torchHeads[i];
            if (head == null) continue;
            Vector3 anchor = HeadTip(head);
            Transform root = new GameObject("Torch Flame " + (i + 1)).transform;
            root.SetPositionAndRotation(anchor, Quaternion.identity);
            root.SetParent(head.transform, true);
            localAnchors.Add(head.transform.InverseTransformPoint(anchor));
            fireRoots.Add(root);
            litHeads.Add(lightOnStart);
            CreateParticles(root, false, i);
            CreateParticles(root, true, i);
            Light glow = new GameObject("Flickering Torch Light").AddComponent<Light>();
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = Vector3.up * flameHeight * 0.3f;
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.43f, 0.12f);
            glow.intensity = lightIntensity;
            glow.range = lightRange;
            glow.shadows = LightShadows.None;
            lights.Add(glow);
            InteractionAudio audio = InteractionAudio.Current;
            AudioSource sound = null;
            if (audio != null && audio.fire != null)
            {
                sound = GazeAudio.Create(root, 1f, 14f);
                sound.clip = audio.fire;
                sound.volume = audio.fireVolume;
                sound.loop = true;
            }
            fireSounds.Add(sound);
            root.gameObject.SetActive(lightOnStart);
            if (lightOnStart && sound != null) sound.Play();
        }
        if (managesTorchGroups || fireRoots.Count >= 2) SetUpInteractions(reference);
    }

    public static Vector3 HeadTip(Renderer head)
    {
        MeshFilter filter = head.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return head.bounds.center + Vector3.up * head.bounds.extents.y;
        Bounds bounds = filter.sharedMesh.bounds;
        return head.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)) + head.transform.up * 0.01f;
    }
    public bool IsLit(int index = 0)
    {
        return isActiveAndEnabled && (index >= 0 && index < litHeads.Count ? litHeads[index] : lightOnStart);
    }
    public Vector3 FlamePosition(int index = 0)
    {
        if (index >= 0 && index < fireRoots.Count && fireRoots[index] != null)
            return fireRoots[index].parent.TransformPoint(localAnchors[index]) + Vector3.up * flameHeight * 0.12f;
        return transform.position;
    }
    public void SetLit(bool active)
    {
        lightOnStart = active;
        for (int i = 0; i < fireRoots.Count; i++)
        {
            litHeads[i] = active;
            if (fireRoots[i] != null) fireRoots[i].gameObject.SetActive(active && isActiveAndEnabled);
            if (i < fireSounds.Count && fireSounds[i] != null)
            {
                if (active && isActiveAndEnabled) { if (!fireSounds[i].isPlaying) fireSounds[i].Play(); }
                else fireSounds[i].Stop();
            }
        }
    }
    private void SetUpInteractions(Material reference)
    {
        for (int i = 0; i < torchHeads.Length; i++)
        {
            if (torchHeads[i] == null) continue;
            GazeTorchTarget.Bind(torchHeads[i].transform, this, i, null);
            Transform nearest = null;
            float distance = float.PositiveInfinity;
            foreach (Transform child in torchHeads[i].transform.parent)
            {
                if (!child.name.StartsWith("Cylinder") || !child.TryGetComponent(out Renderer shaft)) continue;
                float candidate = Vector3.Distance(shaft.bounds.center, torchHeads[i].bounds.center);
                if (candidate < distance) { distance = candidate; nearest = child; }
            }
            if (nearest != null) GazeTorchTarget.Bind(nearest, this, i, null);
            if (i < fireRoots.Count)
            {
                Transform flame = fireRoots[i];
                GazeTorchTarget.Bind(flame, this, i, null);
                SphereCollider collider = flame.gameObject.AddComponent<SphereCollider>();
                collider.isTrigger = true;
                collider.center = Vector3.up * flameHeight * 0.25f;
                collider.radius = flameHeight * 0.45f;
            }
        }
        List<Transform> candidates = new List<Transform>();
        foreach (Transform child in transform) if (child.name.StartsWith("Torch") && child.GetComponentInChildren<MeshRenderer>() != null) candidates.Add(child);
        foreach (Transform candidate in candidates)
        {
            if (!FindTorchParts(candidate, out Renderer head, out Transform shaft)) continue;
            int duplicate = -1;
            for (int i = 0; i < torchHeads.Length; i++)
                if (torchHeads[i] != null && Vector3.Distance(head.bounds.center, torchHeads[i].bounds.center) < 0.08f) { duplicate = i; break; }
            if (duplicate >= 0)
            {
                // An overlapping copy of a standing torch uses that same fire and gaze target.
                GazeTorchTarget.Bind(candidate, this, duplicate, null);
                continue;
            }
            TorchFire fire = candidate.GetComponent<TorchFire>();
            if (fire == null) fire = candidate.gameObject.AddComponent<TorchFire>();
            fire.torchHeads = new[] { head };
            fire.fireMaterial = reference;
            fire.flameHeight = 0.5f;
            fire.flameWidth = 0.25f;
            fire.flameBrightness = flameBrightness;
            fire.lightIntensity = 2.5f;
            fire.lightRange = 3.5f;
            fire.SetLit(false);
            GazeCarryTorch carried = candidate.GetComponent<GazeCarryTorch>();
            if (carried == null) carried = candidate.gameObject.AddComponent<GazeCarryTorch>();
            GazeTorchTarget.Bind(candidate, null, 0, carried);
            carried.Initialize(head, shaft, fire);
        }
    }

    private void CreateParticles(Transform parent, bool embers, int index)
    {
        ParticleSystem particles = new GameObject(embers ? "Rising Embers" : "Looping Fire").AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.transform.SetParent(parent, false);
        // Cone emitters fire along local +Z; turn that axis upward.
        particles.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        particles.useAutoRandomSeed = false;
        particles.randomSeed = (uint)(5317 + index * 7919 + (embers ? 71 : 0));
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.prewarm = true;
        main.duration = 2f;
        main.maxParticles = embers ? 16 : 64;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = embers ? new ParticleSystem.MinMaxCurve(0.45f, 0.8f) : new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
        float speed = flameHeight / 0.65f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * (embers ? 0.5f : 0.65f), speed * (embers ? 0.85f : 1.1f));
        main.startColor = Color.white;
        main.startSize3D = !embers;
        if (embers) main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        else
        {
            main.startSizeX = new ParticleSystem.MinMaxCurve(flameWidth * 0.5f, flameWidth * 0.9f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(flameHeight * 0.3f, flameHeight * 0.5f);
            main.startSizeZ = 1f;
        }
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
        var emission = particles.emission;
        emission.rateOverTime = embers ? 5f : 40f;
        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = embers ? 12f : 6f;
        shape.radius = flameWidth * 0.12f;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] {
            new GradientColorKey(new Color(1f, 0.88f, 0.25f), 0f),
            new GradientColorKey(flameColor, 0.45f),
            new GradientColorKey(new Color(0.55f, 0.025f, 0.002f), 1f)
        }, new[] {
            new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.95f, 0.12f),
            new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f)
        });
        var color = particles.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.5f), new Keyframe(0.2f, 1f), new Keyframe(0.7f, 0.35f), new Keyframe(1f, 0f)));
        var noise = particles.noise;
        noise.enabled = true;
        noise.strength = embers ? 0.025f : 0.035f;
        noise.frequency = 1.4f;
        noise.scrollSpeed = 0.5f;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = runtimeMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        particles.Play();
    }

    private void LateUpdate()
    {
        for (int i = 0; i < fireRoots.Count; i++)
        {
            Transform root = fireRoots[i];
            if (root == null || root.parent == null) continue;
            root.position = root.parent.TransformPoint(localAnchors[i]);
            root.rotation = Quaternion.identity;
            Vector3 scale = root.parent.lossyScale;
            root.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            if (lights[i] != null)
                lights[i].intensity = lightIntensity * (0.8f + Mathf.PerlinNoise(Time.time * 9f, i * 11.3f) * 0.4f);
        }
    }
    private void OnEnable()
    {
        for (int i = 0; i < fireRoots.Count; i++) if (fireRoots[i] != null) fireRoots[i].gameObject.SetActive(litHeads[i]);
        for (int i = 0; i < fireSounds.Count; i++) if (fireSounds[i] != null && litHeads[i]) fireSounds[i].Play();
    }
    private void OnDisable()
    {
        foreach (AudioSource sound in fireSounds) GazeAudio.Stop(sound);
        foreach (Transform root in fireRoots) if (root != null) root.gameObject.SetActive(false);
    }
    private void OnDestroy()
    {
        foreach (Transform root in fireRoots) if (root != null) Destroy(root.gameObject);
        if (runtimeMaterial != null) Destroy(runtimeMaterial);
    }
}
