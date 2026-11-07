using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazeBrewingTable : MonoBehaviour
{
    public Transform[] vials = new Transform[5];
    public Transform[] vialCaps = new Transform[5];
    public Color[] ingredientColors = { Color.magenta, Color.cyan, Color.red, Color.yellow, Color.white };
    public Transform receivingBottle;
    [Tooltip("The large grey bottle body which will display the mixed potion color.")]
    public Renderer potionSurface;
    [Min(0.1f)] public float travelDuration = 0.8f;
    [Min(0.1f)] public float pourDuration = 1.4f;

    public bool IsPouring { get; private set; }
    public int IngredientCount { get; private set; }
    private Vector3[] restPositions, capPositions, capScales;
    private Quaternion[] restRotations, capRotations;
    private Transform[] capParents;
    private bool[] used;
    private Color mixture;
    private MaterialPropertyBlock originalAppearance, appearance;
    private Color originalColor;
    private Transform receiverMouth;
    private LineRenderer stream;
    private Material streamMaterial;
    private Transform[] bubbles;
    private Text hint;
    private GameObject label;
    private Transform viewer;
    private bool initialized;
    private AudioSource pouringSound;

    private void Reset() { FindParts(); }
    private void FindParts()
    {
        if (vials == null || vials.Length == 0) vials = new Transform[5];
        if (vialCaps == null || vialCaps.Length != vials.Length) System.Array.Resize(ref vialCaps, vials.Length);
        for (int i = 0; i < vials.Length; i++)
        {
            if (vials[i] == null) vials[i] = transform.Find("weew (" + (54 + i * 2) + ")");
            if (vialCaps[i] == null) vialCaps[i] = transform.Find("weew (" + (55 + i * 2) + ")");
        }
        if (receivingBottle == null) receivingBottle = transform.Find("Cube Potion (1)");
        if (receivingBottle != null && potionSurface == null)
        {
            Transform main = receivingBottle.Find("Potion/Cube (11)");
            if (main != null) potionSurface = main.GetComponent<Renderer>();
        }
    }

    private void Awake()
    {
        FindParts();
        if (receivingBottle == null || potionSurface == null || potionSurface.sharedMaterial == null
            || !receivingBottle.IsChildOf(transform) || !potionSurface.transform.IsChildOf(receivingBottle))
        {
            Debug.LogError("GazeBrewingTable: add to Brewing Table 3 and assign the receiving bottle and its grey body.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < vials.Length; i++)
            if (vials[i] == null || !vials[i].IsChildOf(transform) || vials[i].GetComponentInChildren<Renderer>() == null)
            {
                Debug.LogError("GazeBrewingTable: assign each colored vial in Vials.", this);
                enabled = false;
                return;
            }
        restPositions = new Vector3[vials.Length];
        restRotations = new Quaternion[vials.Length];
        capPositions = new Vector3[vials.Length];
        capScales = new Vector3[vials.Length];
        capRotations = new Quaternion[vials.Length];
        capParents = new Transform[vials.Length];
        used = new bool[vials.Length];
        for (int i = 0; i < vials.Length; i++)
        {
            restPositions[i] = vials[i].localPosition;
            restRotations[i] = vials[i].localRotation;
            AddTarget(vials[i], i);
            if (vialCaps[i] == null) continue;
            capParents[i] = vialCaps[i].parent;
            capPositions[i] = vialCaps[i].localPosition;
            capRotations[i] = vialCaps[i].localRotation;
            capScales[i] = vialCaps[i].localScale;
            AddTarget(vialCaps[i], i);
        }
        AddTarget(receivingBottle, -1);
        originalAppearance = new MaterialPropertyBlock();
        appearance = new MaterialPropertyBlock();
        potionSurface.GetPropertyBlock(originalAppearance);
        originalColor = potionSurface.sharedMaterial.HasProperty("_BaseColor")
            ? potionSurface.sharedMaterial.GetColor("_BaseColor") : Color.gray;
        Transform bottleBody = receivingBottle.Find("Potion");
        if (bottleBody == null) bottleBody = receivingBottle;
        Bounds bottleBounds = VisualBounds(bottleBody);
        receiverMouth = new GameObject("Potion Bottle Opening").transform;
        receiverMouth.SetParent(receivingBottle, false);
        receiverMouth.position = new Vector3(bottleBounds.center.x, bottleBounds.max.y + 0.015f, bottleBounds.center.z);
        BuildEffects();
        BuildHint();
        initialized = true;
        UpdateHint();
    }

    private static Bounds VisualBounds(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = parts.Length > 0 ? parts[0].bounds : new Bounds(root.position, Vector3.zero);
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }
    private void AddTarget(Transform root, int index)
    {
        GazeBrewingTarget target = root.GetComponent<GazeBrewingTarget>();
        if (target == null) target = root.gameObject.AddComponent<GazeBrewingTarget>();
        target.table = this;
        target.ingredientIndex = index;
        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.GetComponent<Collider>() != null || mesh.sharedMesh == null) continue;
            BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
    }
    private void BuildEffects()
    {
        Material reference = vials[0].GetComponentInChildren<Renderer>().sharedMaterial;
        streamMaterial = new Material(reference) { name = "Brewing Liquid Stream" };
        streamMaterial.EnableKeyword("_EMISSION");
        GameObject pouring = new GameObject("Pouring Liquid", typeof(LineRenderer));
        pouring.transform.SetParent(transform, false);
        stream = pouring.GetComponent<LineRenderer>();
        stream.sharedMaterial = streamMaterial;
        stream.useWorldSpace = true;
        stream.positionCount = 4;
        stream.startWidth = 0.012f;
        stream.endWidth = 0.008f;
        stream.shadowCastingMode = ShadowCastingMode.Off;
        stream.enabled = false;
        bubbles = new Transform[7];
        for (int i = 0; i < bubbles.Length; i++)
        {
            GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bubble.name = "Brewing Bubble";
            bubble.GetComponent<Collider>().enabled = false;
            Destroy(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(transform, true);
            bubble.GetComponent<Renderer>().sharedMaterial = streamMaterial;
            bubble.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            bubble.SetActive(false);
            bubbles[i] = bubble.transform;
        }
    }
    private void BuildHint()
    {
        label = new GameObject("Potion Mix Status", typeof(RectTransform), typeof(Canvas));
        label.transform.SetParent(transform, true);
        label.transform.position = receiverMouth.position + Vector3.up * 0.3f;
        label.transform.localScale = Vector3.one * 0.0012f;
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 100f);
        GameObject text = new GameObject("Text", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(label.transform, false);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        hint = text.GetComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 24;
        hint.color = Color.white;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.raycastTarget = false;
        text.AddComponent<Outline>().effectColor = Color.black;
    }

    public bool CanGazeAt(int index)
    {
        if (!initialized || !isActiveAndEnabled || IsPouring) return false;
        return index < 0 ? IngredientCount > 0
            : index < vials.Length && IngredientCount < 2 && !used[index];
    }
    public void Activate(int index, Transform cameraTransform)
    {
        if (!CanGazeAt(index)) return;
        viewer = cameraTransform;
        if (index < 0) { EmptyBottle(); return; }
        StartCoroutine(Pour(index));
    }
    private IEnumerator Pour(int index)
    {
        IsPouring = true;
        UpdateHint();
        Transform vial = vials[index];
        Transform cap = vialCaps[index];
        Bounds vialBounds = VisualBounds(vial);
        Vector3 opening = vial.InverseTransformPoint(new Vector3(vialBounds.center.x, vialBounds.max.y, vialBounds.center.z));
        Quaternion upright = vial.rotation;
        if (cap != null)
        {
            Vector3 startCap = cap.position;
            cap.SetParent(transform, true);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                cap.position = Vector3.Lerp(startCap, startCap + Vector3.up * 0.12f + transform.right * 0.08f,
                    Mathf.SmoothStep(0f, 1f, t / 0.3f));
                yield return null;
            }
        }
        float travel = Mathf.Max(0.1f, travelDuration);
        yield return Move(vial, vial.position + Vector3.up * 0.18f, upright, travel * 0.5f);
        Vector3 towardBottle = Vector3.ProjectOnPlane(receiverMouth.position - vial.position, Vector3.up).normalized;
        if (towardBottle.sqrMagnitude < 0.01f) towardBottle = transform.right;
        Quaternion tilted = Quaternion.AngleAxis(105f, Vector3.Cross(Vector3.up, towardBottle)) * upright;
        Vector3 mouthTarget = receiverMouth.position + Vector3.up * 0.18f;
        Quaternion before = vial.rotation;
        vial.rotation = tilted;
        Vector3 pouringPosition = mouthTarget - vial.TransformVector(opening);
        vial.rotation = before;
        yield return Move(vial, pouringPosition, tilted, travel);
        Color color = ingredientColors != null && index < ingredientColors.Length ? ingredientColors[index] : Color.cyan;
        color.a = 1f;
        streamMaterial.SetColor("_BaseColor", color);
        streamMaterial.SetColor("_Color", color);
        streamMaterial.SetColor("_EmissionColor", color * 2f);
        Color previous = IngredientCount == 0 ? originalColor : mixture;
        Color result = IngredientCount == 0 ? color : Color.Lerp(mixture, color, 0.5f);
        stream.enabled = true;
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref pouringSound, transform, audio != null ? audio.pouring : null, 0.8f, true,
            startTime: audio != null ? audio.pouringStartTime : 0f);
        if (pouringSound != null) pouringSound.transform.position = receiverMouth.position;
        foreach (Transform bubble in bubbles) bubble.gameObject.SetActive(true);
        float duration = Mathf.Max(0.1f, pourDuration);
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            Vector3 from = vial.TransformPoint(opening);
            Vector3 to = receiverMouth.position;
            for (int point = 0; point < 4; point++)
                stream.SetPosition(point, Vector3.Lerp(from, to, point / 3f));
            SetPotionColor(Color.Lerp(previous, result, Mathf.SmoothStep(0f, 1f, t / duration)));
            for (int i = 0; i < bubbles.Length; i++)
            {
                float phase = Mathf.Repeat(t * 0.8f + i / (float)bubbles.Length, 1f);
                float angle = i * 2.4f + t;
                bubbles[i].position = to + new Vector3(Mathf.Cos(angle) * 0.04f, phase * 0.2f, Mathf.Sin(angle) * 0.04f);
                bubbles[i].localScale = Vector3.one * (Mathf.Sin(phase * Mathf.PI) * 0.025f);
            }
            yield return null;
        }
        mixture = result;
        used[index] = true;
        IngredientCount++;
        SetPotionColor(mixture);
        HidePourEffects();
        yield return Move(vial, vial.position + Vector3.up * 0.08f, upright, travel * 0.5f);
        yield return Move(vial, vial.parent.TransformPoint(restPositions[index]), vial.parent.rotation * restRotations[index], travel);
        if (cap != null)
            yield return Move(cap, capParents[index].TransformPoint(capPositions[index]), capParents[index].rotation * capRotations[index], 0.35f);
        RestoreVials();
        IsPouring = false;
        UpdateHint();
    }
    private IEnumerator Move(Transform part, Vector3 destination, Quaternion rotation, float duration)
    {
        Vector3 start = part.position;
        Quaternion startRotation = part.rotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / duration);
            part.SetPositionAndRotation(Vector3.Lerp(start, destination, weight), Quaternion.Slerp(startRotation, rotation, weight));
            yield return null;
        }
        part.SetPositionAndRotation(destination, rotation);
    }
    private void SetPotionColor(Color color)
    {
        potionSurface.GetPropertyBlock(appearance);
        appearance.SetColor("_BaseColor", color);
        appearance.SetColor("_Color", color);
        appearance.SetColor("_EmissionColor", color * 0.6f);
        potionSurface.SetPropertyBlock(appearance);
    }
    public void EmptyBottle()
    {
        if (!initialized || IsPouring) return;
        IngredientCount = 0;
        mixture = Color.clear;
        System.Array.Clear(used, 0, used.Length);
        potionSurface.SetPropertyBlock(originalAppearance);
        UpdateHint();
    }
    private void UpdateHint()
    {
        if (hint == null) return;
        hint.text = IsPouring ? "Pouring..." : IngredientCount == 0 ? "Choose up to 2 vials"
            : IngredientCount + "/2 ingredients\nGaze at bottle to empty";
    }
    private void LateUpdate()
    {
        if (label == null) return;
        Transform camera = viewer != null ? viewer : Camera.main != null ? Camera.main.transform : null;
        if (camera != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.position, Vector3.up);
    }
    private void HidePourEffects()
    {
        GazeAudio.Stop(pouringSound);
        if (stream != null) stream.enabled = false;
        if (bubbles != null) foreach (Transform bubble in bubbles) if (bubble != null) bubble.gameObject.SetActive(false);
    }
    private void RestoreVials()
    {
        if (!initialized) return;
        for (int i = 0; i < vials.Length; i++)
        {
            if (vials[i] != null) vials[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
            if (vialCaps[i] == null) continue;
            vialCaps[i].SetParent(capParents[i], false);
            vialCaps[i].SetLocalPositionAndRotation(capPositions[i], capRotations[i]);
            vialCaps[i].localScale = capScales[i];
        }
    }
    private void OnEnable() { if (label != null) label.SetActive(true); }
    private void OnDisable()
    {
        StopAllCoroutines();
        RestoreVials();
        HidePourEffects();
        IsPouring = false;
        if (initialized)
        {
            if (IngredientCount == 0) potionSurface.SetPropertyBlock(originalAppearance);
            else SetPotionColor(mixture);
        }
        UpdateHint();
        if (label != null) label.SetActive(false);
    }
    private void OnDestroy()
    {
        if (streamMaterial != null) Destroy(streamMaterial);
        if (stream != null) Destroy(stream.gameObject);
        if (label != null) Destroy(label);
        if (receiverMouth != null) Destroy(receiverMouth.gameObject);
        if (bubbles != null) foreach (Transform bubble in bubbles) if (bubble != null) Destroy(bubble.gameObject);
    }
}
