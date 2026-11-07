using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum GolemElement { Water, Fire, Earth, Wind }

[DisallowMultipleComponent]
public class GazeElementSpell : MonoBehaviour
{
    public bool IsCasting { get; private set; }
    public float CastPoseWeight { get; private set; }
    private GazeElementSelection equipment;
    private Transform effects;
    private Transform soundAnchor;
    private AudioSource skillSound;
    private readonly List<Material> materials = new List<Material>();

    public void Initialize(GazeElementSelection selection) { equipment = selection; }

    public void Cast(GolemElement element, GazeGolemAwakening target, Vector3 caster)
    {
        if (!isActiveAndEnabled || IsCasting || target == null || !target.CanBeAttacked) return;
        StartCoroutine(Animate(element, target, caster));
    }

    private IEnumerator Animate(GolemElement element, GazeGolemAwakening target, Vector3 caster)
    {
        IsCasting = true;
        try
        {
            effects = new GameObject(element + " Spell - Primitives Only").transform;
            soundAnchor = new GameObject("Element Spell Audio Position").transform;
            soundAnchor.SetParent(effects, false);
            soundAnchor.position = target.GroundPosition + Vector3.up * 2f;
            Material reference = equipment.elements[equipment.SelectedIndex].staff.GetComponentInChildren<Renderer>(true).sharedMaterial;
            Color color = element == GolemElement.Fire ? new Color(1f, 0.2f, 0.015f)
                : element == GolemElement.Water ? new Color(0.02f, 0.46f, 0.88f)
                : element == GolemElement.Earth ? new Color(0.34f, 0.23f, 0.1f) : new Color(0.66f, 0.88f, 0.78f);
            Material main = MakeMaterial(reference, color, element == GolemElement.Fire ? 2f : 0.3f);
            Material bright = MakeMaterial(reference, element == GolemElement.Fire ? new Color(1f, 0.67f, 0.08f) : new Color(0.85f, 0.98f, 1f), 1f);
            Transform charge = Primitive(PrimitiveType.Sphere, "Staff Casting Charge", effects, bright);
            for (float time = 0f; time < 0.85f; time += Time.deltaTime)
            {
                if (target == null || !target.CanBeAttacked) yield break;
                CastPoseWeight = Mathf.SmoothStep(0f, 1f, time / 0.85f);
                charge.position = equipment.SpellOrigin;
                charge.localScale = Vector3.one * Mathf.Lerp(0.02f, 0.23f, CastPoseWeight);
                yield return null;
            }
            CastPoseWeight = 1f;
            charge.gameObject.SetActive(false);
            InteractionAudio audio = InteractionAudio.Current;
            AudioClip clip = audio == null ? null : element == GolemElement.Water ? audio.bigWaves
                : element == GolemElement.Fire ? audio.asteroid : element == GolemElement.Earth ? audio.earthquake : audio.tornado;
            GazeAudio.Play(ref skillSound, soundAnchor, clip, 0.9f, element != GolemElement.Fire);
            if (skillSound != null) { skillSound.minDistance = 6f; skillSound.maxDistance = 110f; }
            if (element == GolemElement.Fire) yield return Meteor(target, caster, main, bright);
            else if (element == GolemElement.Earth) yield return Earthquake(target, caster, main, bright);
            else if (element == GolemElement.Water) yield return TidalWave(target, caster, main, bright);
            else yield return Tornado(target, caster, main, bright);
            GazeAudio.Stop(skillSound);
            for (float time = 0f; time < 0.6f; time += Time.deltaTime)
            {
                CastPoseWeight = 1f - Mathf.SmoothStep(0f, 1f, time / 0.6f);
                yield return null;
            }
        }
        finally { Cleanup(); }
    }

    private Material MakeMaterial(Material reference, Color color, float emission)
    {
        Material material = new Material(reference) { name = "Color Only Element Spell" };
        foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.2f);
        material.EnableKeyword("_EMISSION");
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * emission);
        materials.Add(material);
        return material;
    }

    private Transform Primitive(PrimitiveType type, string label, Transform parent, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.name = label;
        part.transform.SetParent(parent, false);
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return part.transform;
    }

    private IEnumerator Meteor(GazeGolemAwakening target, Vector3 caster, Material fire, Material glow)
    {
        Bounds bounds = target.CombatBounds;
        float height = bounds.size.y;
        Vector3 toward = Vector3.ProjectOnPlane(bounds.center - caster, Vector3.up).normalized;
        Vector3 end = bounds.center;
        Vector3 start = end + Vector3.up * height * 0.85f - toward * height * 0.45f;
        Transform meteor = Primitive(PrimitiveType.Sphere, "Fire Meteor", effects, fire);
        Transform[] tail = new Transform[10];
        for (int i = 0; i < tail.Length; i++) tail[i] = Primitive(PrimitiveType.Sphere, "Meteor Flame Trail", effects, i % 2 == 0 ? glow : fire);
        Transform burst = Primitive(PrimitiveType.Sphere, "Meteor Impact", effects, glow);
        burst.gameObject.SetActive(false);
        bool hit = false;
        for (float time = 0f; time < 3.6f; time += Time.deltaTime)
        {
            if (target == null || !target.isActiveAndEnabled) yield break;
            float t = Mathf.Clamp01(time / 2.3f);
            Vector3 position = Vector3.Lerp(start, end, t * t);
            if (soundAnchor != null) soundAnchor.position = position;
            meteor.position = position;
            meteor.localScale = Vector3.one * height * 0.16f;
            meteor.Rotate(new Vector3(35f, 50f, 15f) * Time.deltaTime);
            for (int i = 0; i < tail.Length; i++)
            {
                float length = (i + 1f) / tail.Length;
                tail[i].position = position + (start - end).normalized * (length * height * 0.38f)
                    + Vector3.right * Mathf.Sin(time * 8f + i) * 0.3f;
                tail[i].localScale = Vector3.one * height * (0.12f * (1f - length) + 0.015f);
            }
            if (time >= 2.3f)
            {
                if (!hit)
                {
                    hit = true;
                    target.Defeat(GolemElement.Fire, caster);
                    meteor.gameObject.SetActive(false);
                    foreach (Transform piece in tail) piece.gameObject.SetActive(false);
                    burst.gameObject.SetActive(true);
                }
                float impact = Mathf.Clamp01((time - 2.3f) / 1.3f);
                burst.position = end;
                burst.localScale = Vector3.one * height * Mathf.Sin(impact * Mathf.PI) * 0.3f;
            }
            yield return null;
        }
    }

    private IEnumerator Earthquake(GazeGolemAwakening target, Vector3 caster, Material earth, Material glow)
    {
        Bounds bounds = target.CombatBounds;
        float height = bounds.size.y;
        Vector3 ground = target.GroundPosition;
        Transform[] spires = new Transform[7];
        Vector3[] origins = new Vector3[spires.Length];
        float[] sizes = new float[spires.Length];
        for (int i = 0; i < spires.Length; i++)
        {
            spires[i] = new GameObject("Rising Earth Spire").transform;
            spires[i].SetParent(effects, false);
            float angle = i * Mathf.PI * 2f / spires.Length;
            spires[i].position = ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * height * 0.15f;
            origins[i] = spires[i].position;
            spires[i].rotation = Quaternion.Euler(0f, i * 43f, 0f);
            sizes[i] = height * (0.32f + (i % 3) * 0.07f);
            for (int tier = 0; tier < 3; tier++)
            {
                Transform rock = Primitive(PrimitiveType.Cube, "Mountain Stone", spires[i], earth);
                float width = height * (0.095f - tier * 0.023f);
                rock.localPosition = Vector3.up * (sizes[i] * (tier + 0.5f) / 3f);
                rock.localScale = new Vector3(width, sizes[i] / 3f + 0.1f, width);
                rock.localRotation = Quaternion.Euler(0f, tier * 17f, tier * 3f);
            }
            spires[i].localScale = new Vector3(1f, 0.001f, 1f);
        }
        Transform ring = Primitive(PrimitiveType.Cylinder, "Earth Ground Shockwave", effects, glow);
        bool hit = false;
        for (float time = 0f; time < 4f; time += Time.deltaTime)
        {
            if (target == null || !target.isActiveAndEnabled) yield break;
            for (int i = 0; i < spires.Length; i++)
            {
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((time - i * 0.1f) / 1.4f));
                float sink = time > 2.8f ? 1f - Mathf.SmoothStep(0f, 1f, (time - 2.8f) / 1.2f) : 1f;
                spires[i].localScale = new Vector3(1f, Mathf.Max(0.001f, grow * sink), 1f);
                spires[i].position = origins[i] + Vector3.right * Mathf.Sin(time * 39f + i) * 0.025f * grow * sink;
            }
            float radius = height * 0.7f * Mathf.Clamp01(time / 1.8f);
            ring.position = ground + Vector3.up * 0.04f;
            ring.localScale = new Vector3(radius, 0.012f, radius);
            ring.gameObject.SetActive(time < 1.8f);
            if (!hit && time >= 1.8f) { hit = true; target.Defeat(GolemElement.Earth, caster); }
            yield return null;
        }
    }

    private IEnumerator TidalWave(GazeGolemAwakening target, Vector3 caster, Material water, Material foam)
    {
        Bounds bounds = target.CombatBounds;
        float height = bounds.size.y;
        Vector3 ground = target.GroundPosition;
        Vector3 forward = Vector3.ProjectOnPlane(ground - caster, Vector3.up).normalized;
        float travel = Mathf.Min(Vector3.ProjectOnPlane(ground - caster, Vector3.up).magnitude * 0.55f, height * 0.65f);
        Vector3 start = ground - forward * travel;
        Transform wave = new GameObject("Tsunami Wave").transform;
        wave.SetParent(effects, false);
        wave.rotation = Quaternion.LookRotation(forward, Vector3.up);
        Transform[] crests = new Transform[16];
        for (int i = 0; i < crests.Length; i++)
        {
            float x = (i - 7.5f) * height * 0.065f;
            float columnHeight = height * (0.37f + Mathf.Sin(i * 0.9f) * 0.025f);
            Transform column = Primitive(PrimitiveType.Cube, "Wave Water", wave, water);
            column.localPosition = new Vector3(x, columnHeight * 0.5f, 0f);
            column.localScale = new Vector3(height * 0.069f, columnHeight, height * 0.065f);
            Transform curl = Primitive(PrimitiveType.Sphere, "Curling Water Crest", wave, water);
            curl.localPosition = new Vector3(x, columnHeight, height * 0.025f);
            curl.localScale = new Vector3(height * 0.085f, height * 0.1f, height * 0.12f);
            crests[i] = Primitive(PrimitiveType.Sphere, "Wave Foam", wave, foam);
            crests[i].localPosition = curl.localPosition + Vector3.up * height * 0.04f;
            crests[i].localScale = new Vector3(height * 0.075f, height * 0.025f, height * 0.055f);
        }
        bool hit = false;
        for (float time = 0f; time < 4f; time += Time.deltaTime)
        {
            if (target == null || !target.isActiveAndEnabled) yield break;
            float t = Mathf.Clamp01(time / 2.5f);
            wave.position = Vector3.Lerp(start, ground + forward * height * 0.08f, Mathf.SmoothStep(0f, 1f, t));
            if (soundAnchor != null) soundAnchor.position = wave.position + Vector3.up * height * 0.1f;
            float grow = Mathf.SmoothStep(0f, 1f, time / 0.7f);
            float retreat = time > 2.7f ? 1f - Mathf.SmoothStep(0f, 1f, (time - 2.7f) / 1.3f) : 1f;
            wave.localScale = new Vector3(1f, Mathf.Max(0.001f, grow * retreat), 1f);
            for (int i = 0; i < crests.Length; i++)
            {
                Vector3 p = crests[i].localPosition;
                p.z = height * (0.025f + Mathf.Sin(time * 5f + i * 0.6f) * 0.012f);
                crests[i].localPosition = p;
            }
            if (!hit && time >= 2.2f) { hit = true; target.Defeat(GolemElement.Water, caster); }
            yield return null;
        }
    }

    private IEnumerator Tornado(GazeGolemAwakening target, Vector3 caster, Material wind, Material bright)
    {
        float height = target.CombatBounds.size.y;
        Vector3 ground = target.GroundPosition;
        Transform[] spiral = new Transform[84];
        for (int i = 0; i < spiral.Length; i++) spiral[i] = Primitive(PrimitiveType.Cylinder, "Tornado Wind Segment", effects, i % 4 == 0 ? bright : wind);
        Transform[] fragments = new Transform[10];
        for (int i = 0; i < fragments.Length; i++) fragments[i] = Primitive(PrimitiveType.Cube, "Tornado Debris", effects, wind);
        bool hit = false;
        for (float time = 0f; time < 4f; time += Time.deltaTime)
        {
            if (target == null || !target.isActiveAndEnabled) yield break;
            float grow = Mathf.SmoothStep(0f, 1f, time / 1.3f);
            float fade = time > 2.7f ? 1f - Mathf.SmoothStep(0f, 1f, (time - 2.7f) / 1.3f) : 1f;
            float weight = grow * fade;
            for (int i = 0; i < spiral.Length; i++)
            {
                Vector3 from = TornadoPoint(i / (float)spiral.Length, time, height, weight, ground);
                Vector3 to = TornadoPoint((i + 1f) / spiral.Length, time, height, weight, ground);
                Vector3 direction = to - from;
                spiral[i].SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction));
                spiral[i].localScale = new Vector3(height * 0.006f * weight, direction.magnitude * 0.51f, height * 0.006f * weight);
            }
            for (int i = 0; i < fragments.Length; i++)
            {
                float y = Mathf.Repeat(time * 0.25f + i * 0.1f, 1f);
                fragments[i].position = TornadoPoint(y, time + i, height, weight, ground);
                fragments[i].rotation = Quaternion.Euler(time * 170f, i * 37f + time * 120f, i * 61f);
                fragments[i].localScale = Vector3.one * height * 0.018f * weight;
            }
            if (!hit && time >= 1.65f) { hit = true; target.Defeat(GolemElement.Wind, caster); }
            yield return null;
        }
    }

    private Vector3 TornadoPoint(float t, float time, float height, float weight, Vector3 ground)
    {
        float angle = t * Mathf.PI * 10f + time * 8f;
        float radius = height * (0.035f + t * 0.24f) * weight;
        return ground + new Vector3(Mathf.Cos(angle) * radius, t * height * weight, Mathf.Sin(angle) * radius);
    }

    public void Cancel() { StopAllCoroutines(); Cleanup(); }

    private void Cleanup()
    {
        GazeAudio.Stop(skillSound);
        if (effects != null) { effects.gameObject.SetActive(false); Destroy(effects.gameObject); effects = null; }
        soundAnchor = null;
        skillSound = null;
        foreach (Material material in materials) if (material != null) Destroy(material);
        materials.Clear();
        IsCasting = false;
        CastPoseWeight = 0f;
    }

    private void OnDisable() { Cancel(); }
}
