using UnityEngine;

[DisallowMultipleComponent]
public class GazeStatue : MonoBehaviour
{
    public Transform head;
    public Transform torso;
    public Transform staffArm;
    public Transform staff;
    [Min(0.01f)] public float raiseDuration = 0.5f;
    [Min(0.01f)] public float waveDuration = 2f;
    [Min(0.01f)] public float returnDuration = 0.6f;
    [Range(0f, 75f)] public float armRaiseAngle = 35f;
    [Range(0f, 30f)] public float waveAngle = 12f;
    [Range(1, 5)] public int waveCount = 3;
    [Header("Dark mode gesture")]
    [Min(0.1f)] public float threateningRaiseDuration = 1.5f;
    [Min(0.1f)] public float threateningHoldDuration = 1.5f;
    [Range(0f, 85f)] public float threateningArmAngle = 65f;
    [Range(-25f, 25f)] public float threateningHeadTilt = 12f;

    public bool IsGreeting { get; private set; }
    private Transform headPivot;
    private Transform armPivot;
    private Transform viewer;
    private float elapsed;
    private GazeEyeball trackingEye;
    private bool threatening;
    private bool returningFromDark;
    private float darkReturnStart;
    private Quaternion darkReturnHead;
    private Quaternion darkReturnArm;

    private void Reset()
    {
        FindParts();
    }

    private void FindParts()
    {
        if (head == null) head = transform.Find("Cube (7)");
        if (torso == null) torso = transform.Find("Cube (6)");
        if (staffArm == null) staffArm = transform.Find("Cube (8)");
        if (staff == null) staff = transform.Find("Staff");
    }

    private void Awake()
    {
        FindParts();
        if (!IsPart(head) || !IsPart(torso) || !IsPart(staffArm) || !IsPart(staff) ||
            head == torso || staffArm == torso || staffArm == head || staff == staffArm)
        {
            Debug.LogError("GazeStatue: assign the head, torso, staff-side arm and Staff inside this statue.", this);
            enabled = false;
            return;
        }

        Renderer headMesh = head.GetComponent<Renderer>();
        Renderer armMesh = staffArm.GetComponent<Renderer>();
        Renderer torsoMesh = torso.GetComponent<Renderer>();
        if (headMesh == null || armMesh == null || torsoMesh == null)
        {
            Debug.LogError("GazeStatue: the head, arm and torso must have mesh renderers.", this);
            enabled = false;
            return;
        }

        Bounds headBounds = headMesh.localBounds;
        Vector3 neck = head.TransformPoint(new Vector3(headBounds.center.x,
            headBounds.min.y, headBounds.center.z));
        headPivot = CreatePivot("Statue Neck Pivot", neck);
        head.SetParent(headPivot, true);

        Vector3 shoulder = transform.InverseTransformPoint(armMesh.bounds.center);
        shoulder.z = transform.InverseTransformPoint(torsoMesh.bounds.center).z;
        armPivot = CreatePivot("Statue Staff Shoulder Pivot", transform.TransformPoint(shoulder));
        staffArm.SetParent(armPivot, true);
        staff.SetParent(armPivot, true);
    }

    private bool IsPart(Transform part)
    {
        return part != null && part != transform && part.IsChildOf(transform);
    }

    private Transform CreatePivot(string pivotName, Vector3 position)
    {
        Transform pivot = new GameObject(pivotName).transform;
        pivot.SetParent(transform, false);
        pivot.position = position;
        return pivot;
    }

    public void Greet(Transform cameraTransform)
    {
        if (!isActiveAndEnabled || IsGreeting || headPivot == null || armPivot == null || cameraTransform == null)
            return;

        viewer = cameraTransform;
        elapsed = 0f;
        trackingEye = null;
        foreach (GazeEyeball eye in Object.FindObjectsByType<GazeEyeball>(FindObjectsSortMode.None))
        {
            if (eye.isActiveAndEnabled && eye.IsTracking)
            {
                trackingEye = eye;
                break;
            }
        }
        threatening = trackingEye != null;
        returningFromDark = false;
        IsGreeting = true;
    }

    private void LateUpdate()
    {
        if (!IsGreeting)
            return;

        if (viewer == null)
        {
            RestorePose();
            return;
        }

        elapsed += Time.deltaTime;
        if (threatening)
        {
            AnimateThreat();
            return;
        }
        float raise = Mathf.Max(0.01f, raiseDuration);
        float wave = Mathf.Max(0.01f, waveDuration);
        float returning = Mathf.Max(0.01f, returnDuration);
        float lookWeight;
        float armAngle;
        if (elapsed < raise)
        {
            lookWeight = Mathf.SmoothStep(0f, 1f, elapsed / raise);
            armAngle = armRaiseAngle * lookWeight;
        }
        else if (elapsed < raise + wave)
        {
            float t = (elapsed - raise) / wave;
            lookWeight = 1f;
            armAngle = armRaiseAngle + waveAngle * Mathf.Sin(t * Mathf.PI * 2f * waveCount) * Mathf.Sin(t * Mathf.PI);
        }
        else if (elapsed < raise + wave + returning)
        {
            lookWeight = 1f - Mathf.SmoothStep(0f, 1f, (elapsed - raise - wave) / returning);
            armAngle = armRaiseAngle * lookWeight;
        }
        else
        {
            RestorePose();
            return;
        }

        AimHead(lookWeight, 0f);
        armPivot.localRotation = Quaternion.AngleAxis(armAngle, Vector3.right);
    }

    private void AimHead(float lookWeight, float tilt)
    {
        // Both models face local -Z. Turn only the head, with natural neck limits.
        Vector3 direction = transform.InverseTransformDirection(viewer.position - headPivot.position);
        float horizontal = new Vector2(direction.x, direction.z).magnitude;
        float yaw = Mathf.Clamp(Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg, -80f, 80f);
        float pitch = Mathf.Clamp(Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg, -45f, 45f);
        headPivot.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(pitch, yaw, tilt), lookWeight);
    }

    private void AnimateThreat()
    {
        float returning = Mathf.Max(0.01f, returnDuration);
        if (trackingEye == null || !trackingEye.IsTracking)
        {
            // If daylight returns mid-gesture, ease out from the current pose.
            if (!returningFromDark)
            {
                returningFromDark = true;
                darkReturnStart = elapsed;
                darkReturnHead = headPivot.localRotation;
                darkReturnArm = armPivot.localRotation;
            }
            float t = Mathf.Clamp01((elapsed - darkReturnStart) / returning);
            float blend = Mathf.SmoothStep(0f, 1f, t);
            headPivot.localRotation = Quaternion.Slerp(darkReturnHead, Quaternion.identity, blend);
            armPivot.localRotation = Quaternion.Slerp(darkReturnArm, Quaternion.identity, blend);
            if (t >= 1f) RestorePose();
            return;
        }

        float raise = Mathf.Max(0.1f, threateningRaiseDuration);
        float hold = Mathf.Max(0.1f, threateningHoldDuration);
        const float headStart = 0.25f;
        float holdStart = headStart + raise;
        float returnStart = holdStart + hold;
        float lookWeight = Mathf.SmoothStep(0f, 1f, elapsed / 0.18f);
        float armWeight = Mathf.SmoothStep(0f, 1f, (elapsed - headStart) / raise);
        float tremor = 0f;
        if (elapsed >= returnStart)
        {
            float t = Mathf.Clamp01((elapsed - returnStart) / returning);
            lookWeight = armWeight = 1f - Mathf.SmoothStep(0f, 1f, t);
            if (t >= 1f)
            {
                RestorePose();
                return;
            }
        }
        else if (elapsed >= holdStart)
        {
            float envelope = Mathf.Sin((elapsed - holdStart) / hold * Mathf.PI);
            tremor = (Mathf.Sin(elapsed * 31f) * 0.6f + Mathf.Sin(elapsed * 19f) * 0.4f) * envelope;
        }

        AimHead(lookWeight, threateningHeadTilt + tremor * 0.7f);
        armPivot.localRotation = Quaternion.Euler(threateningArmAngle * armWeight + tremor, 0f, tremor * 0.5f);
    }

    private void OnDisable()
    {
        RestorePose();
    }

    private void RestorePose()
    {
        if (headPivot != null) headPivot.localRotation = Quaternion.identity;
        if (armPivot != null) armPivot.localRotation = Quaternion.identity;
        IsGreeting = false;
        viewer = null;
        elapsed = 0f;
        threatening = false;
        returningFromDark = false;
        trackingEye = null;
    }
}
