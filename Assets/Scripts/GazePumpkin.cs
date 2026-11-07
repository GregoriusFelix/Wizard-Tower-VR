using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazePumpkin : MonoBehaviour
{
    public Renderer body;
    [Min(0.1f)] public float floatHeight = 0.9f;
    [Min(3f)] public float hauntingDuration = 7f;
    [Range(0, 8)] public int littlePumpkinCount = 5;
    [Min(0.5f)] public float littlePumpkinRadius = 1.4f;

    public bool IsHaunting { get; private set; }
    private Vector3 restPosition;
    private Quaternion restRotation;
    private readonly List<Renderer> eyes = new List<Renderer>();
    private readonly List<MaterialPropertyBlock> eyeAppearances = new List<MaterialPropertyBlock>();
    private readonly List<Transform> smallPumpkins = new List<Transform>();
    private readonly List<Vector3> anchors = new List<Vector3>();
    private MaterialPropertyBlock glowAppearance;
    private GameObject summoned;
    private Light glow;
    private Material faceMaterial;
    private bool initialized;
    private AudioSource hauntingSound;

    private void Reset() { FindBody(); }
    private void FindBody()
    {
        Transform sphere = transform.Find("Sphere");
        if (body == null && sphere != null) body = sphere.GetComponent<Renderer>();
    }

    private void Awake()
    {
        FindBody();
        if (body == null || !body.transform.IsChildOf(transform) || body.GetComponent<MeshFilter>() == null)
        {
            Debug.LogError("GazePumpkin: add to the whole Pumpkin group and assign its orange Sphere to Body.", this);
            enabled = false;
            return;
        }
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        foreach (Renderer part in GetComponentsInChildren<Renderer>())
            if (part.transform.parent != null && part.transform.parent.name.StartsWith("Glow"))
            {
                eyes.Add(part);
                MaterialPropertyBlock original = new MaterialPropertyBlock();
                part.GetPropertyBlock(original);
                eyeAppearances.Add(original);
            }
        glowAppearance = new MaterialPropertyBlock();
        if (body.GetComponent<Collider>() == null)
        {
            Mesh mesh = body.GetComponent<MeshFilter>().sharedMesh;
            SphereCollider collider = body.gameObject.AddComponent<SphereCollider>();
            collider.center = mesh.bounds.center;
            collider.radius = mesh.bounds.extents.x;
        }
        glow = new GameObject("Jack O Lantern Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.22f, 0.025f);
        glow.range = 2.5f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
        initialized = true;
    }

    public void Haunt(Transform viewer)
    {
        if (initialized && isActiveAndEnabled && !IsHaunting && viewer != null)
            StartCoroutine(Animate(viewer));
    }

    private IEnumerator Animate(Transform viewer)
    {
        IsHaunting = true;
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref hauntingSound, body.transform, audio != null ? audio.pumpkin : null, 0.85f, true);
        Vector3 start = transform.position;
        Quaternion rotation = transform.rotation;
        Vector3 center = body.bounds.center;
        Vector3 centerOffset = center - start;
        Vector3 facing = -transform.forward;
        Transform eye = transform.Find("Eye");
        Transform otherEye = transform.Find("Eye (1)");
        if (eye != null && otherEye != null)
        {
            Bounds face = new Bounds(eye.position, Vector3.zero);
            foreach (Renderer part in eye.GetComponentsInChildren<Renderer>()) face.Encapsulate(part.bounds);
            foreach (Renderer part in otherEye.GetComponentsInChildren<Renderer>()) face.Encapsulate(part.bounds);
            Vector3 measured = Vector3.ProjectOnPlane(face.center - center, Vector3.up);
            if (measured.sqrMagnitude > 0.001f) facing = measured.normalized;
        }
        Quaternion facingOffset = Quaternion.Inverse(Quaternion.LookRotation(facing, Vector3.up)) * rotation;
        BuildLittlePumpkins(center, viewer);
        glow.enabled = true;
        float duration = Mathf.Max(3f, hauntingDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (viewer == null) break;
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Min(t, duration - t) / 0.9f);
            Vector3 direction = Vector3.ProjectOnPlane(viewer.position - body.bounds.center, Vector3.up);
            if (direction.sqrMagnitude < 0.001f) direction = facing;
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up) * facingOffset
                * Quaternion.Euler(Mathf.Sin(t * 2.7f) * 6f, 0f, Mathf.Sin(t * 1.8f) * 8f);
            Quaternion turned = Quaternion.RotateTowards(transform.rotation, desired, 150f * Time.deltaTime);
            Quaternion pose = Quaternion.Slerp(rotation, turned, reveal);
            Vector3 sway = Vector3.right * (Mathf.Sin(t * 1.6f) * 0.1f * reveal);
            Vector3 floatingCenter = center + Vector3.up * ((floatHeight + Mathf.Sin(t * 2.4f) * 0.09f) * reveal) + sway;
            transform.SetPositionAndRotation(floatingCenter - (pose * Quaternion.Inverse(rotation)) * centerOffset, pose);
            glow.transform.position = body.bounds.center + direction.normalized * body.bounds.extents.magnitude * 0.8f;
            float flicker = reveal * (1.6f + Mathf.Sin(t * 17f) * 0.25f + Mathf.Sin(t * 29f) * 0.12f);
            glow.intensity = flicker;
            for (int i = 0; i < eyes.Count; i++)
            {
                eyes[i].GetPropertyBlock(glowAppearance);
                glowAppearance.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.005f) * (2f + flicker));
                eyes[i].SetPropertyBlock(glowAppearance);
            }
            for (int i = 0; i < smallPumpkins.Count; i++)
            {
                Transform small = smallPumpkins[i];
                float size = Mathf.SmoothStep(0f, 1f, (t - 0.5f - i * 0.2f) / 0.45f)
                    * Mathf.SmoothStep(0f, 1f, (duration - t) / 0.6f);
                small.localScale = Vector3.one * ((0.28f + i % 3 * 0.055f) * size);
                small.position = anchors[i] + Vector3.up * Mathf.Sin(t * 2f + i * 1.7f) * 0.12f;
                Vector3 look = viewer.position - small.position;
                if (look.sqrMagnitude > 0.001f)
                    small.rotation = Quaternion.LookRotation(look, Vector3.up) * Quaternion.Euler(0f, 180f, Mathf.Sin(t * 3f + i) * 10f);
            }
            yield return null;
        }
        Restore();
    }

    private void BuildLittlePumpkins(Vector3 center, Transform viewer)
    {
        ClearSummoned();
        if (littlePumpkinCount <= 0) return;
        if (faceMaterial == null)
        {
            faceMaterial = new Material(body.sharedMaterial) { name = "Summoned Pumpkin Fire" };
            faceMaterial.SetColor("_BaseColor", new Color(1f, 0.3f, 0.015f));
            faceMaterial.SetColor("_Color", new Color(1f, 0.3f, 0.015f));
            faceMaterial.EnableKeyword("_EMISSION");
            faceMaterial.SetColor("_EmissionColor", new Color(3f, 0.35f, 0.01f));
        }
        summoned = new GameObject("Little Jack O Lanterns");
        Vector3 toward = Vector3.ProjectOnPlane(viewer.position - center, Vector3.up).normalized;
        if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, toward);
        Renderer stem = transform.Find("Stem")?.GetComponentInChildren<Renderer>();
        for (int i = 0; i < Mathf.Min(8, littlePumpkinCount); i++)
        {
            Transform small = new GameObject("Little Pumpkin " + (i + 1)).transform;
            small.SetParent(summoned.transform, false);
            GameObject shell = new GameObject("Orange Shell", typeof(MeshFilter), typeof(MeshRenderer));
            shell.transform.SetParent(small, false);
            shell.transform.localScale = new Vector3(1f, 0.8f, 1f);
            shell.GetComponent<MeshFilter>().sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            shell.GetComponent<MeshRenderer>().sharedMaterial = body.sharedMaterial;
            BuildPrimitiveFace(small);
            GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stalk.GetComponent<Collider>());
            stalk.transform.SetParent(small, false);
            stalk.transform.localPosition = new Vector3(0f, 0.44f, 0f);
            stalk.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            stalk.GetComponent<Renderer>().sharedMaterial = stem != null ? stem.sharedMaterial : body.sharedMaterial;
            float angle = Mathf.Lerp(-1.2f, 1.2f, (i + 0.5f) / Mathf.Min(8, littlePumpkinCount));
            anchors.Add(center + toward * (Mathf.Cos(angle) * littlePumpkinRadius)
                + right * (Mathf.Sin(angle) * littlePumpkinRadius)
                + Vector3.up * (0.3f + (i % 3) * 0.28f));
            small.position = anchors[i];
            small.localScale = Vector3.zero;
            smallPumpkins.Add(small);
        }
    }

    private void BuildPrimitiveFace(Transform pumpkin)
    {
        FaceCube(pumpkin, "Left Eye", new Vector2(-0.17f, 0.13f), new Vector3(0.1f, 0.1f, 0.02f), 45f);
        FaceCube(pumpkin, "Right Eye", new Vector2(0.17f, 0.13f), new Vector3(0.1f, 0.1f, 0.02f), 45f);
        FaceCube(pumpkin, "Nose", new Vector2(0f, 0.005f), new Vector3(0.04f, 0.04f, 0.02f), 45f);
        for (int i = 0; i < 9; i++)
        {
            float x = Mathf.Lerp(-0.27f, 0.27f, i / 8f);
            float y = -0.2f + Mathf.Abs(x) * 0.24f;
            FaceCube(pumpkin, "Grin Cube", new Vector2(x, y), new Vector3(0.065f, i % 2 == 0 ? 0.055f : 0.035f, 0.02f), 0f);
        }
    }
    private void FaceCube(Transform pumpkin, string name, Vector2 point, Vector3 size, float angle)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.GetComponent<Collider>().enabled = false;
        Destroy(piece.GetComponent<Collider>());
        piece.transform.SetParent(pumpkin, false);
        piece.transform.localPosition = new Vector3(point.x, point.y,
            -Mathf.Sqrt(Mathf.Max(0.01f, 0.25f - point.x * point.x - point.y * point.y / 0.64f)) - 0.035f);
        piece.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        piece.transform.localScale = size;
        MeshRenderer renderer = piece.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = faceMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
    }

    private void ClearSummoned()
    {
        if (summoned != null) { summoned.SetActive(false); Destroy(summoned); }
        summoned = null;
        smallPumpkins.Clear();
        anchors.Clear();
    }

    private void Restore()
    {
        GazeAudio.Stop(hauntingSound);
        if (initialized) transform.SetLocalPositionAndRotation(restPosition, restRotation);
        for (int i = 0; i < eyes.Count; i++)
            if (eyes[i] != null) eyes[i].SetPropertyBlock(eyeAppearances[i]);
        if (glow != null) glow.enabled = false;
        ClearSummoned();
        IsHaunting = false;
    }

    private void OnDisable() { StopAllCoroutines(); Restore(); }
    private void OnDestroy()
    {
        ClearSummoned();
        if (faceMaterial != null) Destroy(faceMaterial);
        if (glow != null) Destroy(glow.gameObject);
    }
}
