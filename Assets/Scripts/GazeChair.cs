using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GazeChair : MonoBehaviour
{
    public Transform table;
    public Renderer seatSurface;
    public Renderer tableSurface;
    [Tooltip("Optional eye-level marker parented to the chair.")]
    public Transform seatedEyePoint;
    [Min(0.1f)] public float eyeHeightAboveSeat = 0.75f;
    [Min(0.05f)] public float pullDistance = 0.55f;
    [Min(0.1f)] public float chairMoveDuration = 0.8f;
    [Min(0.1f)] public float sitDuration = 0.65f;

    public bool IsSeated { get; private set; }
    public bool IsAnimating { get; private set; }
    private bool initialized;
    private bool occupantCaptured;
    private GazeTeleport gaze;
    private Camera viewCamera;
    private Transform player;
    private CharacterController controller;
    private bool controllerWasEnabled;
    private Vector3 chairRest;
    private Vector3 standingPosition;
    private Quaternion standingRotation;
    private float standingEyeY;
    private Vector3 pullDirection;
    private GameObject standButton;
    private AudioSource chairSound;

    private void Reset() { FindParts(); }

    private void FindParts()
    {
        if (table == null)
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                if (parent.name == "Table") { table = parent; break; }
        if (seatSurface == null) seatSurface = FindHorizontalSurface(transform, false);
        if (tableSurface == null && table != null) tableSurface = FindHorizontalSurface(table, true);
    }

    private Renderer FindHorizontalSurface(Transform root, bool excludeChair)
    {
        Renderer best = null;
        float bestArea = 0f;
        foreach (Renderer part in root.GetComponentsInChildren<Renderer>())
        {
            if (excludeChair && part.transform.IsChildOf(transform)) continue;
            Vector3 size = part.bounds.size;
            float area = size.x * size.z;
            if (size.y < Mathf.Min(size.x, size.z) * 0.4f && area > bestArea)
            { bestArea = area; best = part; }
        }
        return best;
    }

    private void Awake()
    {
        FindParts();
        if (seatSurface == null || !seatSurface.transform.IsChildOf(transform) || tableSurface == null)
        {
            Debug.LogError("GazeChair: add this component to Chair and assign its seat surface and the tabletop.", this);
            enabled = false;
            return;
        }
        chairRest = transform.localPosition;
        pullDirection = Vector3.ProjectOnPlane(seatSurface.bounds.center - tableSurface.bounds.center, Vector3.up).normalized;
        if (pullDirection.sqrMagnitude < 0.01f) pullDirection = -transform.forward;
        initialized = true;
    }

    public bool CanGazeAt(Transform hit)
    {
        return isActiveAndEnabled && initialized && hit != null && hit.IsChildOf(transform);
    }

    public void Interact(GazeTeleport owner, Camera camera, Transform rig)
    {
        if (!isActiveAndEnabled || !initialized || IsAnimating || owner == null || camera == null || rig == null) return;
        gaze = owner;
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref chairSound, transform, audio != null ? audio.chair : null, 0.85f);
        if (chairSound != null) chairSound.transform.position = seatSurface.bounds.center;
        if (IsSeated) { StartCoroutine(Stand()); return; }
        viewCamera = camera;
        player = rig;
        standingPosition = player.position;
        standingRotation = player.rotation;
        standingEyeY = viewCamera.transform.position.y;
        controller = player.GetComponent<CharacterController>();
        controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled) controller.enabled = false;
        occupantCaptured = true;
        StartCoroutine(Sit());
    }

    private Vector3 SeatEyes()
    {
        return seatedEyePoint != null ? seatedEyePoint.position :
            new Vector3(seatSurface.bounds.center.x, seatSurface.bounds.max.y + eyeHeightAboveSeat, seatSurface.bounds.center.z);
    }

    private Vector3 PulledPosition()
    {
        Vector3 localPull = transform.parent != null ? transform.parent.InverseTransformVector(pullDirection * pullDistance)
            : pullDirection * pullDistance;
        return chairRest + localPull;
    }

    private IEnumerator Sit()
    {
        IsAnimating = true;
        yield return MoveChair(PulledPosition(), false);
        yield return Fade(0f, 1f);
        MoveEyesTo(SeatEyes() + Vector3.up * 0.25f);
        Vector3 direction = tableSurface.bounds.center + Vector3.up * 0.12f - viewCamera.transform.position;
        Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up);
        Vector3 facing = Vector3.ProjectOnPlane(viewCamera.transform.forward, Vector3.up);
        if (horizontal.sqrMagnitude > 0.001f && facing.sqrMagnitude > 0.001f)
            player.RotateAround(viewCamera.transform.position, Vector3.up, Vector3.SignedAngle(facing, horizontal, Vector3.up));
        // A desktop preview can look down at the tabletop; tracked headset pitch remains untouched.
        if (!viewCamera.stereoEnabled && player == viewCamera.transform)
            viewCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        yield return Fade(1f, 0f);
        yield return MoveEyes(SeatEyes(), Mathf.Max(0.1f, sitDuration));
        yield return MoveChair(chairRest, true);
        IsSeated = true;
        IsAnimating = false;
        ShowStandButton();
    }

    private IEnumerator Stand()
    {
        IsAnimating = true;
        if (standButton != null) standButton.SetActive(false);
        yield return MoveChair(PulledPosition(), true);
        yield return MoveEyes(viewCamera.transform.position + Vector3.up * 0.25f, Mathf.Max(0.1f, sitDuration));
        yield return Fade(0f, 1f);
        player.SetPositionAndRotation(standingPosition, standingRotation);
        RestoreController();
        IsSeated = false;
        yield return Fade(1f, 0f);
        yield return MoveChair(chairRest, false);
        IsAnimating = false;
    }

    private IEnumerator MoveChair(Vector3 destination, bool carryPlayer)
    {
        Vector3 start = transform.localPosition;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, chairMoveDuration);
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            Vector3 before = transform.position;
            transform.localPosition = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            if (carryPlayer && player != null) player.position += transform.position - before;
            yield return null;
        }
    }

    private IEnumerator MoveEyes(Vector3 destination, float duration)
    {
        Vector3 start = player.position;
        Vector3 rigDestination = start + destination - viewCamera.transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
            player.position = Vector3.Lerp(start, rigDestination, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }
    }

    private void MoveEyesTo(Vector3 destination)
    {
        if (player != null && viewCamera != null) player.position += destination - viewCamera.transform.position;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed = Mathf.Min(0.25f, elapsed + Time.unscaledDeltaTime);
            if (gaze != null) gaze.SetViewFade(Mathf.Lerp(from, to, elapsed / 0.25f));
            yield return null;
        }
    }

    private void ShowStandButton()
    {
        if (standButton == null)
        {
            standButton = new GameObject("Stand Up Gaze Target", typeof(RectTransform), typeof(Canvas), typeof(BoxCollider));
            standButton.transform.SetParent(transform, false);
            standButton.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 60f);
            Vector3 scale = transform.lossyScale;
            standButton.transform.localScale = new Vector3(0.0015f / Mathf.Abs(scale.x),
                0.0015f / Mathf.Abs(scale.y), 0.0015f / Mathf.Abs(scale.z));
            Canvas canvas = standButton.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = viewCamera;
            GameObject panel = new GameObject("Stand Up Background", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(standButton.transform, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0.025f, 0.035f, 0.05f);
            background.raycastTarget = false;
            BoxCollider collider = standButton.GetComponent<BoxCollider>();
            collider.size = new Vector3(200f, 60f, 5f);
            collider.isTrigger = true;
            GameObject label = new GameObject("Stand Up Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(standButton.transform, false);
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 26; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
            text.text = "Stand up"; text.raycastTarget = false;
        }
        Vector3 forward = Vector3.ProjectOnPlane(tableSurface.bounds.center - SeatEyes(), Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector3 position = SeatEyes() + forward * 0.65f + right * 0.4f - Vector3.up * 0.08f;
        standButton.transform.SetPositionAndRotation(position, Quaternion.LookRotation(position - SeatEyes(), Vector3.up));
        standButton.SetActive(true);
    }

    public void ReleaseForTravel()
    {
        StopAllCoroutines();
        if (occupantCaptured && player != null && viewCamera != null)
            player.position += Vector3.up * (standingEyeY - viewCamera.transform.position.y);
        FinishReset(false);
    }

    public void CancelInteraction()
    {
        StopAllCoroutines();
        if (occupantCaptured && player != null) player.SetPositionAndRotation(standingPosition, standingRotation);
        FinishReset();
    }

    private void RestoreController()
    {
        if (controller != null && controllerWasEnabled) controller.enabled = true;
        occupantCaptured = false;
    }

    private void FinishReset(bool clearFade = true)
    {
        GazeAudio.Stop(chairSound);
        if (initialized) transform.localPosition = chairRest;
        if (standButton != null) standButton.SetActive(false);
        RestoreController();
        if (clearFade && gaze != null) gaze.SetViewFade(0f);
        IsSeated = IsAnimating = false;
    }

    private void OnDisable() { CancelInteraction(); }
    private void OnDestroy() { if (standButton != null) Destroy(standButton); }
}
