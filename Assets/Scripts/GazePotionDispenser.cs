using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazePotionDispenser : MonoBehaviour
{
    [System.Serializable]
    public class BottleSlot
    {
        public Transform bottle;
        public Transform cap;
        public Renderer potionSurface;
        [Tooltip("Optional point on the tabletop where the completed bottle should stand.")]
        public Transform finishedPoint;
        [System.NonSerialized] public Vector3 restPosition, restScale, capPosition, capScale, mouth, capBottom, finishFeet;
        [System.NonSerialized] public Quaternion restRotation, capRotation, upright, closedCapRotation;
        [System.NonSerialized] public Transform capParent;
        [System.NonSerialized] public MaterialPropertyBlock originalAppearance;
        [System.NonSerialized] public Color originalColor;
        [System.NonSerialized] public float plugInsertion;
        [System.NonSerialized] public Material originalMaterial, filledMaterial;
    }
    public Transform nozzle;
    public Transform tap;
    public Renderer tableSurface;
    public BottleSlot[] bottles = { new BottleSlot(), new BottleSlot() };
    public Color potionColor = new Color(1f, 0.88f, 0.04f);
    [Min(0f)] public float potionGlow = 2.5f;
    [Min(0.1f)] public float fillDuration = 2f;
    [Min(0.1f)] public float moveDuration = 0.8f;
    public int FilledCount { get; private set; }
    public bool IsDispensing { get; private set; }
    public bool CanDispense => initialized && isActiveAndEnabled && !IsDispensing && FilledCount < 2;

    private bool initialized;
    private int activeBottle = -1;
    private GameObject stream;
    private readonly Transform[] flowDrops = new Transform[3];
    private Material streamMaterial;
    private GameObject label;
    private Text hint;
    private Transform viewer;
    private readonly List<Transform> createdCaps = new List<Transform>();
    private readonly List<PartPose> capPieces = new List<PartPose>();
    private MaterialPropertyBlock appearance;
    private string progressMessage;
    private AudioSource pouringSound;

    private class PartPose
    {
        public Transform part, parent;
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    private void Reset() { FindParts(); }
    private void FindParts()
    {
        if (nozzle == null) nozzle = transform.Find("weew (26)");
        if (tap == null) tap = transform.Find("weew (19)");
        if (tableSurface == null) tableSurface = transform.Find("weew (5)")?.GetComponent<Renderer>();
        if (bottles == null || bottles.Length != 2) bottles = new[] { new BottleSlot(), new BottleSlot() };
        Transform supply = transform.Find("Cube Potion (4)");
        for (int i = 0; i < 2; i++)
        {
            if (bottles[i] == null) bottles[i] = new BottleSlot();
            if (bottles[i].bottle == null && supply != null)
                bottles[i].bottle = supply.Find(i == 0 ? "Potion" : "Potion (1)");
            if (bottles[i].potionSurface == null && bottles[i].bottle != null)
                bottles[i].potionSurface = bottles[i].bottle.Find("Cube (11)")?.GetComponent<Renderer>();
        }
    }
    private void Awake()
    {
        appearance = new MaterialPropertyBlock();
        FindParts();
        if (nozzle == null || tableSurface == null || !nozzle.IsChildOf(transform))
        {
            Debug.LogError("GazePotionDispenser: add to Brewing Table and assign the nozzle and tabletop.", this);
            enabled = false;
            return;
        }
        foreach (BottleSlot slot in bottles)
            if (slot.bottle == null || slot.potionSurface == null || slot.potionSurface.sharedMaterial == null
                || !slot.bottle.IsChildOf(transform) || !slot.potionSurface.transform.IsChildOf(slot.bottle))
            {
                Debug.LogError("GazePotionDispenser: assign two empty bottle groups and their grey body renderers.", this);
                enabled = false;
                return;
            }
        Transform capParts = transform.Find("Cube Potion (4)/Cap");
        for (int i = 0; i < 2; i++)
        {
            BottleSlot slot = bottles[i];
            if (slot.cap == null && capParts != null)
                slot.cap = GroupCap(capParts, i == 0 ? "Cube (15)" : "Cube (16)", i == 0 ? "Cube (20)" : "Cube (17)", i);
            if (slot.cap == null)
            {
                Debug.LogError("GazePotionDispenser: assign one complete cap per bottle.", this);
                enabled = false;
                return;
            }
            SaveSlot(slot);
        }
        BindTarget(nozzle);
        if (tap != null) BindTarget(tap);
        Material reference = transform.Find("weew (17)")?.GetComponent<Renderer>()?.sharedMaterial;
        if (reference == null) reference = bottles[0].potionSurface.sharedMaterial;
        streamMaterial = new Material(reference) { name = "Yellow Dispenser Liquid" };
        foreach (string texture in streamMaterial.GetTexturePropertyNames()) streamMaterial.SetTexture(texture, null);
        streamMaterial.SetColor("_BaseColor", potionColor);
        streamMaterial.SetColor("_Color", potionColor);
        streamMaterial.EnableKeyword("_EMISSION");
        streamMaterial.SetColor("_EmissionColor", potionColor * 2f);
        stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stream.name = "Yellow Liquid Stream - Cylinder";
        stream.GetComponent<Collider>().enabled = false;
        Destroy(stream.GetComponent<Collider>());
        stream.GetComponent<Renderer>().sharedMaterial = streamMaterial;
        stream.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        for (int i = 0; i < flowDrops.Length; i++)
        {
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "Flowing Yellow Drop";
            drop.GetComponent<Collider>().enabled = false;
            Destroy(drop.GetComponent<Collider>());
            drop.transform.SetParent(stream.transform, true);
            drop.GetComponent<Renderer>().sharedMaterial = streamMaterial;
            drop.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            flowDrops[i] = drop.transform;
        }
        stream.SetActive(false);
        BuildHint();
        initialized = true;
        UpdateHint();
    }

    private Transform GroupCap(Transform parent, string first, string second, int index)
    {
        Transform bottom = parent.Find(first), top = parent.Find(second);
        if (bottom == null || top == null) return null;
        Vector3 bottomCenter = VisualBounds(bottom).center;
        Vector3 topCenter = VisualBounds(top).center;
        Transform group = new GameObject("Dispenser Bottle Cap " + (index + 1)).transform;
        group.SetParent(transform, true);
        group.position = bottomCenter;
        // The first cube is the wide shoulder; the smaller second cube is the downward plug.
        Vector3 up = (bottomCenter - topCenter).normalized;
        if (up.sqrMagnitude < 0.01f) up = Vector3.up;
        group.rotation = Quaternion.FromToRotation(Vector3.up, up);
        foreach (Transform part in new[] { bottom, top })
        {
            capPieces.Add(new PartPose { part = part, parent = part.parent, position = part.localPosition,
                rotation = part.localRotation, scale = part.localScale });
            part.SetParent(group, true);
        }
        createdCaps.Add(group);
        return group;
    }
    private static Bounds VisualBounds(Transform root)
    {
        Renderer[] parts = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = parts.Length > 0 ? parts[0].bounds : new Bounds(root.position, Vector3.zero);
        foreach (Renderer part in parts) bounds.Encapsulate(part.bounds);
        return bounds;
    }
    private static Bounds GeometryBounds(Transform root, Transform frame)
    {
        bool first = true;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            Bounds local = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = frame.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }
    private static float HalfLengthAlong(Renderer renderer, Vector3 axis)
    {
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return Vector3.Dot(renderer.bounds.extents,
            new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)));
        Vector3 extents = filter.sharedMesh.bounds.extents;
        return Mathf.Abs(Vector3.Dot(filter.transform.TransformVector(Vector3.right * extents.x), axis))
            + Mathf.Abs(Vector3.Dot(filter.transform.TransformVector(Vector3.up * extents.y), axis))
            + Mathf.Abs(Vector3.Dot(filter.transform.TransformVector(Vector3.forward * extents.z), axis));
    }
    private void SaveSlot(BottleSlot slot)
    {
        slot.restPosition = slot.bottle.localPosition;
        slot.restRotation = slot.bottle.localRotation;
        slot.restScale = slot.bottle.localScale;
        slot.capParent = slot.cap.parent;
        slot.capPosition = slot.cap.localPosition;
        slot.capRotation = slot.cap.localRotation;
        slot.capScale = slot.cap.localScale;
        slot.originalAppearance = new MaterialPropertyBlock();
        slot.potionSurface.GetPropertyBlock(slot.originalAppearance);
        slot.originalColor = slot.potionSurface.sharedMaterial.HasProperty("_BaseColor")
            ? slot.potionSurface.sharedMaterial.GetColor("_BaseColor") : Color.gray;
        slot.originalMaterial = slot.potionSurface.sharedMaterial;
        Transform rim = slot.bottle.Find("Cube (21)");
        Bounds body = slot.potionSurface.bounds;
        Bounds neck = rim != null ? VisualBounds(rim) : VisualBounds(slot.bottle);
        Vector3 axis = (neck.center - body.center).normalized;
        if (axis.sqrMagnitude < 0.01f) axis = slot.bottle.up;
        Bounds bottleGeometry = GeometryBounds(slot.bottle, slot.bottle);
        Vector3 neckCenter = slot.bottle.InverseTransformPoint(neck.center);
        slot.mouth = new Vector3(neckCenter.x, bottleGeometry.max.y, neckCenter.z);
        slot.upright = Quaternion.FromToRotation(axis, Vector3.up) * slot.bottle.rotation;
        Vector3 originalCenter = VisualBounds(slot.bottle).center;
        slot.finishFeet = new Vector3(originalCenter.x, tableSurface.bounds.max.y + 0.008f, originalCenter.z);
        Renderer narrow = null, wide = null;
        float smallest = float.PositiveInfinity, largest = 0f;
        foreach (Renderer piece in slot.cap.GetComponentsInChildren<Renderer>())
        {
            Vector3 size = piece.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume < smallest) { smallest = volume; narrow = piece; }
            if (volume > largest) { largest = volume; wide = piece; }
        }
        Vector3 capUp = narrow != null && wide != null && narrow != wide
            ? (wide.bounds.center - narrow.bounds.center).normalized : slot.cap.up;
        if (capUp.sqrMagnitude < 0.01f) capUp = slot.cap.up;
        float half = narrow != null ? HalfLengthAlong(narrow, capUp) : 0.025f;
        Vector3 plugCenter = narrow != null ? narrow.bounds.center : slot.cap.position;
        slot.capBottom = slot.cap.InverseTransformPoint(plugCenter - capUp * half);
        slot.plugInsertion = half;
        slot.closedCapRotation = Quaternion.FromToRotation(capUp, Vector3.up) * slot.cap.rotation;
    }
    private void BindTarget(Transform root)
    {
        GazePotionDispenserTarget target = root.GetComponent<GazePotionDispenserTarget>();
        if (target == null) target = root.gameObject.AddComponent<GazePotionDispenserTarget>();
        target.dispenser = this;
        foreach (MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
    }
    private Vector3 SpoutTip()
    {
        Bounds bounds = VisualBounds(nozzle);
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }
    public void Dispense(Transform cameraTransform)
    {
        if (!CanDispense) return;
        viewer = cameraTransform;
        StartCoroutine(FillBottle(FilledCount));
    }
    private IEnumerator FillBottle(int index)
    {
        IsDispensing = true;
        activeBottle = index;
        bool completed = false;
        try
        {
            progressMessage = "Moving bottle...";
            UpdateHint();
            BottleSlot slot = bottles[index];
            Transform bottle = slot.bottle;
            float travel = Mathf.Max(0.1f, moveDuration);
            Vector3 center = VisualBounds(bottle).center;
            Vector3 localCenter = bottle.InverseTransformPoint(center);
            Quaternion originalRotation = bottle.rotation;
            bottle.rotation = slot.upright;
            Vector3 liftedPosition = center + Vector3.up * 0.18f - bottle.TransformVector(localCenter);
            bottle.rotation = originalRotation;
            yield return Move(bottle, liftedPosition, slot.upright, travel * 0.5f);
            Vector3 opening = SpoutTip() - Vector3.up * 0.065f;
            Vector3 fillPosition = opening - bottle.TransformVector(slot.mouth);
            yield return Move(bottle, fillPosition, slot.upright, travel);
            progressMessage = "Filling bottle...";
            UpdateHint();
            stream.SetActive(true);
            InteractionAudio audio = InteractionAudio.Current;
            GazeAudio.Play(ref pouringSound, transform, audio != null ? audio.pouring : null, 0.8f, true,
                startTime: audio != null ? audio.pouringStartTime : 0f);
            if (pouringSound != null) pouringSound.transform.position = SpoutTip();
            float duration = Mathf.Max(0.1f, fillDuration);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                UpdateStream(SpoutTip(), bottle.TransformPoint(slot.mouth) - Vector3.up * 0.04f);
                float amount = Mathf.SmoothStep(0f, 1f, t / duration);
                SetColor(slot, Color.Lerp(slot.originalColor, potionColor, amount), amount);
                yield return null;
            }
            SetColor(slot, potionColor);
            GazeAudio.Stop(pouringSound);
            stream.SetActive(false);
            Vector3 clearDirection = Vector3.ProjectOnPlane(viewer != null ? viewer.position - bottle.TransformPoint(slot.mouth)
                : SpoutTip() - VisualBounds(tap != null ? tap : nozzle).center, Vector3.up).normalized;
            if (clearDirection.sqrMagnitude < 0.01f) clearDirection = transform.forward;
            yield return Move(bottle, bottle.position + clearDirection * 0.22f, slot.upright, 0.35f);
            progressMessage = "Sealing bottle...";
            UpdateHint();
            Quaternion oldCapRotation = slot.cap.rotation;
            slot.cap.rotation = slot.closedCapRotation;
            Vector3 sealedPosition = bottle.TransformPoint(slot.mouth) - Vector3.up * slot.plugInsertion - slot.cap.TransformVector(slot.capBottom);
            slot.cap.rotation = oldCapRotation;
            yield return Move(slot.cap, sealedPosition + Vector3.up * 0.16f, slot.closedCapRotation, travel * 0.7f);
            yield return Move(slot.cap, sealedPosition, slot.closedCapRotation, 0.35f);
            slot.cap.SetParent(bottle, true);
            progressMessage = "Setting bottle aside...";
            UpdateHint();
            Vector3 feet = slot.finishedPoint != null ? slot.finishedPoint.position : slot.finishFeet;
            Bounds bounds = VisualBounds(bottle);
            Vector3 destination = bottle.position + new Vector3(feet.x - bounds.center.x, feet.y - bounds.min.y, feet.z - bounds.center.z);
            yield return Move(bottle, destination + Vector3.up * 0.1f, slot.upright, travel);
            yield return Move(bottle, destination, slot.upright, 0.3f);
            FilledCount++;
            completed = true;
        }
        finally
        {
            GazeAudio.Stop(pouringSound);
            if (!completed && index >= 0 && index < bottles.Length) RestoreSlot(bottles[index]);
            if (stream != null) stream.SetActive(false);
            activeBottle = -1;
            IsDispensing = false;
            progressMessage = null;
            UpdateHint();
        }
    }
    private void UpdateStream(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        stream.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, direction));
        stream.transform.localScale = new Vector3(0.014f, direction.magnitude * 0.5f, 0.014f);
        Vector3 scale = stream.transform.lossyScale;
        for (int i = 0; i < flowDrops.Length; i++)
        {
            if (flowDrops[i] == null) continue;
            float progress = Mathf.Repeat(Time.time * 2.5f + i / (float)flowDrops.Length, 1f);
            flowDrops[i].position = Vector3.Lerp(from, to, progress);
            flowDrops[i].localScale = new Vector3(0.018f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                0.018f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 0.018f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        }
    }
    private void SetColor(BottleSlot slot, Color color, float amount = 1f)
    {
        if (appearance == null) appearance = new MaterialPropertyBlock();
        if (slot.filledMaterial == null)
        {
            slot.filledMaterial = new Material(slot.originalMaterial) { name = "Bright Yellow Filled Potion" };
            foreach (string texture in slot.filledMaterial.GetTexturePropertyNames()) slot.filledMaterial.SetTexture(texture, null);
            slot.filledMaterial.EnableKeyword("_EMISSION");
        }
        slot.potionSurface.sharedMaterial = slot.filledMaterial;
        slot.potionSurface.GetPropertyBlock(appearance);
        appearance.SetColor("_BaseColor", color);
        appearance.SetColor("_Color", color);
        appearance.SetColor("_EmissionColor", color * potionGlow * amount);
        slot.potionSurface.SetPropertyBlock(appearance);
    }
    private IEnumerator Move(Transform part, Vector3 destination, Quaternion rotation, float duration)
    {
        Vector3 center = VisualBounds(part).center;
        Vector3 localCenter = part.InverseTransformPoint(center);
        Quaternion originalRotation = part.rotation;
        part.rotation = rotation;
        Vector3 endCenter = destination + part.TransformVector(localCenter);
        part.rotation = originalRotation;
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / duration);
            part.rotation = Quaternion.Slerp(originalRotation, rotation, weight);
            part.position = Vector3.Lerp(center, endCenter, weight) - part.TransformVector(localCenter);
            yield return null;
        }
        part.SetPositionAndRotation(destination, rotation);
    }
    private void BuildHint()
    {
        label = new GameObject("Dispenser Status", typeof(RectTransform), typeof(Canvas));
        label.transform.SetParent(transform, true);
        label.transform.position = SpoutTip() + Vector3.up * 0.28f;
        label.transform.localScale = Vector3.one * 0.0012f;
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(330f, 85f);
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
    private void UpdateHint()
    {
        if (hint != null) hint.text = IsDispensing ? progressMessage ?? "Preparing bottle..." : FilledCount >= 2 ? "2/2 bottles filled"
            : "Gaze at tap to fill\n" + FilledCount + "/2 bottles filled";
    }
    private void LateUpdate()
    {
        Transform camera = viewer != null ? viewer : Camera.main != null ? Camera.main.transform : null;
        if (label != null && camera != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - camera.position, Vector3.up);
    }
    private void RestoreSlot(BottleSlot slot)
    {
        if (slot == null || slot.originalAppearance == null) return;
        if (slot.bottle != null)
        {
            slot.bottle.SetLocalPositionAndRotation(slot.restPosition, slot.restRotation);
            slot.bottle.localScale = slot.restScale;
        }
        if (slot.cap != null)
        {
            slot.cap.SetParent(slot.capParent, false);
            slot.cap.SetLocalPositionAndRotation(slot.capPosition, slot.capRotation);
            slot.cap.localScale = slot.capScale;
        }
        if (slot.potionSurface != null)
        {
            slot.potionSurface.sharedMaterial = slot.originalMaterial;
            slot.potionSurface.SetPropertyBlock(slot.originalAppearance);
        }
    }
    private void OnEnable() { if (label != null) label.SetActive(true); }
    private void OnDisable()
    {
        GazeAudio.Stop(pouringSound);
        StopAllCoroutines();
        if (activeBottle >= 0) RestoreSlot(bottles[activeBottle]);
        activeBottle = -1;
        IsDispensing = false;
        if (stream != null) stream.SetActive(false);
        UpdateHint();
        if (label != null) label.SetActive(false);
    }
    private void OnDestroy()
    {
        if (bottles != null) foreach (BottleSlot slot in bottles)
        {
            RestoreSlot(slot);
            if (slot != null && slot.filledMaterial != null) Destroy(slot.filledMaterial);
        }
        foreach (PartPose pose in capPieces)
            if (pose.part != null)
            {
                pose.part.SetParent(pose.parent, false);
                pose.part.SetLocalPositionAndRotation(pose.position, pose.rotation);
                pose.part.localScale = pose.scale;
            }
        foreach (Transform cap in createdCaps) if (cap != null) Destroy(cap.gameObject);
        if (streamMaterial != null) Destroy(streamMaterial);
        if (stream != null) Destroy(stream);
        if (label != null) Destroy(label);
    }
}
