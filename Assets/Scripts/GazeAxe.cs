using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazeAxe : MonoBehaviour
{
    public Transform handle;
    public Transform blade;
    [Tooltip("Optional marker on the standing tree trunk.")]
    public Transform chopPoint;
    [Tooltip("A standing tree group. Empty selects the nearest standing tree to the resting axe.")]
    public Transform treeToChop;
    [Range(1, 6)] public int logsPerChop = 3;
    [Min(1)] public int maxLooseLogs = 12;
    [Min(0.1f)] public float chopGazeDuration = 0.8f;
    [Min(0.5f)] public float maxChopDistance = 3f;
    [Range(0.2f, 1f)] public float heldScale = 0.65f;
    public Vector3 heldGripOffset = new Vector3(0.38f, -0.4f, 0.65f);
    public bool IsChopping { get; private set; }
    public bool IsEquipped { get; private set; }
    private Vector3 restPosition, restScale, localGrip, localHead, contact;
    private Quaternion restRotation, heldRotation;
    private Transform viewer;
    private Collider[] axeColliders;
    private bool[] colliderStates;
    private Material bark, cutWood, targetMaterial, handMaterial;
    private GameObject hand;
    private GazeChopTarget mark;
    private Text hint;
    private int hits;
    private bool initialized;
    private Vector3 restingHead;
    private GazeChoppableTree choppingTree;
    private AudioSource chopSound;
    private readonly List<GazeFuelLog> logs = new List<GazeFuelLog>();

    private void Reset() { FindParts(); }
    private void FindParts()
    {
        if (handle == null) handle = transform.Find("Handle");
        if (blade == null) blade = transform.Find("Blade");
        if (blade == null) blade = transform.Find("bLDE");
    }
    private void Awake()
    {
        FindParts();
        if (handle == null || blade == null || !handle.IsChildOf(transform) || !blade.IsChildOf(transform))
        {
            Debug.LogError("GazeAxe: add to Axe and assign Handle and Blade.", this);
            enabled = false;
            return;
        }
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;
        Renderer reference = handle.GetComponentInChildren<Renderer>();
        if (reference == null || reference.sharedMaterial == null) { enabled = false; return; }
        bark = MakeMaterial(reference.sharedMaterial, "Firewood Bark", new Color(0.22f, 0.09f, 0.035f));
        cutWood = MakeMaterial(reference.sharedMaterial, "Cut Wood", new Color(0.65f, 0.4f, 0.18f));
        targetMaterial = MakeMaterial(reference.sharedMaterial, "Chopping Mark", new Color(1f, 0.45f, 0.03f));
        targetMaterial.EnableKeyword("_EMISSION");
        targetMaterial.SetColor("_EmissionColor", new Color(2f, 0.65f, 0.03f));
        handMaterial = MakeMaterial(reference.sharedMaterial, "Axe Glove", new Color(0.28f, 0.16f, 0.08f));
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
            if (mesh.sharedMesh != null && mesh.GetComponent<Collider>() == null)
            {
                BoxCollider collider = mesh.gameObject.AddComponent<BoxCollider>();
                collider.center = mesh.sharedMesh.bounds.center;
                collider.size = mesh.sharedMesh.bounds.size;
            }
        axeColliders = GetComponentsInChildren<Collider>();
        colliderStates = new bool[axeColliders.Length];
        for (int i = 0; i < axeColliders.Length; i++) colliderStates[i] = axeColliders[i].enabled;
        Bounds handleBounds = VisualBounds(handle);
        Vector3 head = VisualBounds(blade).center;
        MeshFilter shaft = handle.GetComponentInChildren<MeshFilter>();
        Vector3 direction = (head - handleBounds.center).normalized;
        float reach = handleBounds.extents.magnitude;
        if (shaft != null && shaft.sharedMesh != null)
        {
            Bounds shaftBounds = shaft.sharedMesh.bounds;
            Vector3 scaledSize = Vector3.Scale(shaftBounds.size, shaft.transform.lossyScale);
            Vector3 shaftAxis = Mathf.Abs(scaledSize.y) >= Mathf.Abs(scaledSize.x)
                && Mathf.Abs(scaledSize.y) >= Mathf.Abs(scaledSize.z) ? Vector3.up
                : Mathf.Abs(scaledSize.x) >= Mathf.Abs(scaledSize.z) ? Vector3.right : Vector3.forward;
            Vector3 worldAxis = shaft.transform.TransformVector(shaftAxis);
            direction = worldAxis.normalized;
            if (Vector3.Dot(direction, head - handleBounds.center) < 0f) direction = -direction;
            reach = Mathf.Abs(Vector3.Dot(shaftBounds.extents, shaftAxis)) * worldAxis.magnitude;
        }
        Vector3 grip = handleBounds.center - direction * reach * 0.72f;
        localGrip = transform.InverseTransformPoint(grip);
        localHead = transform.InverseTransformPoint(head);
        Vector3 modelUp = Quaternion.Inverse(transform.rotation) * direction;
        // The shaft points up; the blade extends forward from it toward the tree.
        Vector3 cuttingDirection = Vector3.ProjectOnPlane(head - handleBounds.center, direction).normalized;
        if (cuttingDirection.sqrMagnitude < 0.01f)
            cuttingDirection = Vector3.ProjectOnPlane(blade.right, direction).normalized;
        Vector3 modelCuttingDirection = Quaternion.Inverse(transform.rotation) * cuttingDirection;
        Vector3 heldUp = new Vector3(-0.1f, 0.95f, 0.25f).normalized;
        Vector3 heldCuttingDirection = Vector3.ProjectOnPlane(Vector3.forward, heldUp).normalized;
        heldRotation = Quaternion.LookRotation(heldCuttingDirection, heldUp)
            * Quaternion.Inverse(Quaternion.LookRotation(modelCuttingDirection, modelUp));
        restingHead = head;
        GameObject pot = GameObject.Find("Couldron");
        if (pot == null) pot = GameObject.Find("Cauldron");
        if (pot != null && pot.GetComponent<GazeCauldronFuel>() == null) pot.AddComponent<GazeCauldronFuel>();
        initialized = true;
    }
    private static Material MakeMaterial(Material reference, string name, Color color)
    {
        Material material = new Material(reference) { name = name };
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        material.SetColor("_EmissionColor", Color.black);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.15f);
        return material;
    }
    private static Bounds VisualBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.position, Vector3.zero);
        foreach (Renderer part in renderers) bounds.Encapsulate(part.bounds);
        return bounds;
    }
    public void Chop(Transform cameraTransform)
    {
        if (!initialized || !isActiveAndEnabled || IsEquipped || cameraTransform == null) return;
        logs.RemoveAll(log => log == null || !log.IsLoose);
        if (logs.Count >= maxLooseLogs) return;
        choppingTree = GazeChoppableTree.Find(treeToChop, restingHead);
        if (choppingTree == null) return;
        viewer = cameraTransform;
        contact = choppingTree.MarkPosition(viewer.position, 0, out _);
        IsEquipped = true;
        hits = 0;
        foreach (Collider collider in axeColliders) if (collider != null) collider.enabled = false;
        transform.localScale = restScale * heldScale;
        hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hand.name = "Axe Holding Glove";
        hand.GetComponent<Collider>().enabled = false;
        Destroy(hand.GetComponent<Collider>());
        hand.transform.localScale = new Vector3(0.12f, 0.15f, 0.11f);
        hand.GetComponent<Renderer>().sharedMaterial = handMaterial;
        CreateMark();
        SetHeldPose();
    }
    private void CreateMark()
    {
        ClearMark();
        GameObject target = new GameObject("Chop Here " + (hits + 1));
        mark = target.AddComponent<GazeChopTarget>();
        mark.owner = this;
        Vector3 point = choppingTree.MarkPosition(viewer.position, hits, out Vector3 outward);
        if (chopPoint != null) point.y = chopPoint.position.y + (hits - 1) * 0.1f;
        target.transform.SetPositionAndRotation(point, Quaternion.LookRotation(outward, Vector3.up));
        SphereCollider collider = target.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.12f;
        LineRenderer ring = target.AddComponent<LineRenderer>();
        ring.sharedMaterial = targetMaterial;
        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = 40;
        ring.startWidth = ring.endWidth = 0.014f;
        ring.shadowCastingMode = ShadowCastingMode.Off;
        for (int i = 0; i < 40; i++)
        {
            float angle = i * Mathf.PI * 2f / 40f;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.095f, Mathf.Sin(angle) * 0.095f, 0f));
        }
        GameObject label = new GameObject("Chop Hint", typeof(RectTransform), typeof(Canvas));
        label.transform.SetParent(target.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        label.transform.localScale = Vector3.one * 0.0015f;
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        label.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 65f);
        GameObject text = new GameObject("Text", typeof(RectTransform), typeof(Text));
        text.transform.SetParent(label.transform, false);
        RectTransform rect = text.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        hint = text.GetComponent<Text>();
        hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hint.fontSize = 26;
        hint.alignment = TextAnchor.MiddleCenter;
        hint.raycastTarget = false;
        hint.color = Color.white;
        text.AddComponent<Outline>().effectColor = Color.black;
    }
    public bool CanHit(GazeChopTarget target)
    {
        return isActiveAndEnabled && IsEquipped && !IsChopping && target != null && target == mark && viewer != null
            && Vector3.Distance(viewer.position, target.transform.position) <= maxChopDistance;
    }
    public void Hit(GazeChopTarget target)
    {
        if (!CanHit(target)) return;
        Vector3 hitPoint = target.transform.position;
        ClearMark();
        StartCoroutine(Swing(hitPoint));
    }
    private IEnumerator Swing(Vector3 hitPoint)
    {
        IsChopping = true;
        float side = hits % 2 == 0 ? -1f : 1f;
        Vector3 right = Vector3.ProjectOnPlane(viewer.right, Vector3.up).normalized;
        Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        Vector3 windupHead = viewer.position + forward * 0.75f + right * (side * 0.65f);
        windupHead.y = hitPoint.y + 0.03f;
        Vector3 initialPosition = transform.position;
        Quaternion initialRotation = transform.rotation;
        Quaternion windupRotation = viewer.rotation * Quaternion.Euler(0f, side * 55f, side * 12f) * heldRotation;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.3f);
            transform.rotation = Quaternion.Slerp(initialRotation, windupRotation, weight);
            transform.position = Vector3.Lerp(initialPosition, windupHead - transform.TransformVector(localHead), weight);
            MoveHand();
            yield return null;
        }
        Quaternion startRotation = transform.rotation;
        Quaternion strikeRotation = viewer.rotation * Quaternion.Euler(0f, -side * 25f, -side * 15f) * heldRotation;
        Vector3 startingHead = transform.TransformPoint(localHead);
        for (float t = 0f; t < 0.18f; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.18f);
            transform.rotation = Quaternion.Slerp(startRotation, strikeRotation, weight);
            Vector3 headPath = Vector3.Lerp(startingHead, hitPoint, weight)
                + right * (side * Mathf.Sin(weight * Mathf.PI) * 0.12f);
            transform.position = headPath - transform.TransformVector(localHead);
            MoveHand();
            yield return null;
        }
        transform.rotation = strikeRotation;
        transform.position = hitPoint - transform.TransformVector(localHead);
        MoveHand();
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref chopSound, choppingTree.transform, audio != null ? audio.axeChop : null);
        if (chopSound != null) chopSound.transform.position = hitPoint;
        hits++;
        Vector3 recovery = transform.position;
        Quaternion recoveryRotation = transform.rotation;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            float weight = Mathf.SmoothStep(0f, 1f, t / 0.35f);
            SetHeldPose();
            Vector3 destination = transform.position;
            Quaternion desiredRotation = transform.rotation;
            transform.SetPositionAndRotation(Vector3.Lerp(recovery, destination, weight), Quaternion.Slerp(recoveryRotation, desiredRotation, weight));
            MoveHand();
            yield return null;
        }
        IsChopping = false;
        if (hits >= 3)
        {
            Vector3 playerPosition = viewer.position;
            Vector3 cutPosition = contact;
            GazeChoppableTree finishedTree = choppingTree;
            finishedTree.BeginFall(playerPosition, () =>
            {
                if (this != null) SpawnLogs(playerPosition, cutPosition);
            });
            Unequip();
        }
        else CreateMark();
    }
    private void SpawnLogs(Vector3 playerPosition, Vector3 cutPosition)
    {
        logs.RemoveAll(log => log == null || !log.IsLoose);
        Vector3 outward = Vector3.ProjectOnPlane(playerPosition - cutPosition, Vector3.up).normalized;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, outward);
        for (int i = 0; i < logsPerChop && logs.Count < maxLooseLogs; i++)
        {
            Vector3 offset = outward * 0.7f + right * ((i - (logsPerChop - 1) * 0.5f) * 0.4f);
            GazeFuelLog log = GazeFuelLog.Create(cutPosition + Vector3.up * 0.2f + offset * 0.4f, bark, cutWood);
            log.Launch(offset);
            logs.Add(log);
        }
    }
    private void LateUpdate()
    {
        if (!IsEquipped) return;
        if (viewer == null) { Unequip(); return; }
        if (!IsChopping) SetHeldPose();
        if (hint != null && mark != null)
        {
            hint.text = Vector3.Distance(viewer.position, mark.transform.position) > maxChopDistance
                ? "Move closer to chop" : "Aim here  " + (hits + 1) + "/3";
            hint.canvas.transform.rotation = Quaternion.LookRotation(hint.canvas.transform.position - viewer.position, Vector3.up);
        }
    }
    private void SetHeldPose()
    {
        if (viewer == null) return;
        transform.rotation = viewer.rotation * heldRotation;
        transform.position = viewer.position + viewer.rotation * heldGripOffset - transform.TransformVector(localGrip);
        MoveHand();
    }
    private void MoveHand()
    {
        if (hand != null) hand.transform.SetPositionAndRotation(transform.TransformPoint(localGrip), transform.rotation);
    }
    public void RefreshAfterTravel()
    {
        if (!IsEquipped || viewer == null) return;
        GazeAudio.Stop(chopSound);
        StopAllCoroutines();
        IsChopping = false;
        CreateMark();
        SetHeldPose();
    }
    private void ClearMark()
    {
        hint = null;
        if (mark != null) { mark.gameObject.SetActive(false); Destroy(mark.gameObject); }
        mark = null;
    }
    public void Unequip()
    {
        StopAllCoroutines();
        ClearMark();
        if (hand != null) { hand.SetActive(false); Destroy(hand); }
        hand = null;
        viewer = null;
        IsEquipped = IsChopping = false;
        if (!initialized) return;
        transform.SetLocalPositionAndRotation(restPosition, restRotation);
        transform.localScale = restScale;
        for (int i = 0; i < axeColliders.Length; i++) if (axeColliders[i] != null) axeColliders[i].enabled = colliderStates[i];
    }
    private void OnDisable() { GazeAudio.Stop(chopSound); Unequip(); }
    private void OnDestroy()
    {
        Unequip();
        if (chopSound != null) Destroy(chopSound.gameObject);
        if (bark != null) Destroy(bark);
        if (cutWood != null) Destroy(cutWood);
        if (targetMaterial != null) Destroy(targetMaterial);
        if (handMaterial != null) Destroy(handMaterial);
    }
}
