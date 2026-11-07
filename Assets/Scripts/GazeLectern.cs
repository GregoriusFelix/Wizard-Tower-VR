using UnityEngine;

[DisallowMultipleComponent]
public class GazeLectern : MonoBehaviour
{
    public Transform book;
    public Renderer pageReference;
    [Min(0.05f)] public float floatHeight = 0.45f;
    [Min(0.1f)] public float floatDuration = 0.8f;
    [Min(3f)] public float summoningDuration = 8f;
    [Range(3, 12)] public int surroundingBookCount = 8;
    [Min(0.5f)] public float surroundingBookRadius = 1.1f;

    public bool IsSummoning { get; private set; }
    private Vector3 restPosition;
    private Quaternion restRotation;
    private float elapsed;
    private bool initialized;
    private Transform viewer;
    private Vector3 viewerStart;
    private LecternInformationBooks information;
    private AudioSource summoningSound;

    private void Reset() { FindParts(); }
    private void FindParts()
    {
        if (book == null) book = transform.Find("Book");
        if (book == null) book = transform.Find("Book Open");
        if (book != null && pageReference == null)
            foreach (Renderer part in book.GetComponentsInChildren<Renderer>())
                if (part.name.StartsWith("Paper", System.StringComparison.Ordinal)) { pageReference = part; break; }
        if (book != null && pageReference == null) pageReference = book.GetComponentInChildren<Renderer>();
    }
    private void Awake()
    {
        FindParts();
        if (book == null || book == transform || !book.IsChildOf(transform) || pageReference == null || pageReference.sharedMaterial == null)
        {
            Debug.LogError("GazeLectern: assign the Book child and a page renderer.", this);
            enabled = false;
            return;
        }
        restPosition = book.localPosition;
        restRotation = book.localRotation;
        information = GetComponent<LecternInformationBooks>();
        if (information == null) information = gameObject.AddComponent<LecternInformationBooks>();
        information.Initialize(pageReference.sharedMaterial, surroundingBookCount);
        initialized = true;
    }
    public void Summon(Transform cameraTransform = null)
    {
        if (!initialized || !isActiveAndEnabled || IsSummoning) return;
        viewer = cameraTransform != null ? cameraTransform : Camera.main != null ? Camera.main.transform : null;
        if (viewer == null) return;
        viewerStart = viewer.position;
        elapsed = 0f;
        IsSummoning = true;
        information.Arrange(viewer, surroundingBookRadius);
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref summoningSound, transform, audio != null ? audio.magicEnhance : null, 0.8f);
        if (summoningSound != null) summoningSound.transform.position = book.position;
    }
    private void Update()
    {
        if (!IsSummoning) return;
        if (viewer == null || Vector3.Distance(viewer.position, viewerStart) > 3f) { RestorePose(); return; }
        elapsed += Time.deltaTime;
        float liftTime = Mathf.Max(0.1f, floatDuration);
        float duration = Mathf.Max(3f, summoningDuration);
        if (elapsed >= liftTime * 2f + duration) { RestorePose(); return; }
        float weight = elapsed < liftTime ? Mathf.SmoothStep(0f, 1f, elapsed / liftTime)
            : 1f - Mathf.SmoothStep(0f, 1f, (elapsed - liftTime - duration) / liftTime);
        float lift = (floatHeight + Mathf.Sin(elapsed * 1.7f) * 0.015f) * weight;
        book.localPosition = restPosition + book.parent.InverseTransformVector(Vector3.up * lift);
        book.localRotation = restRotation;
        information.Animate(elapsed, weight);
    }
    private void RestorePose()
    {
        GazeAudio.Stop(summoningSound);
        if (initialized && book != null) book.SetLocalPositionAndRotation(restPosition, restRotation);
        if (information != null) information.Hide();
        IsSummoning = false;
        elapsed = 0f;
        viewer = null;
    }
    private void OnDisable() { RestorePose(); }
    private void OnDestroy()
    {
        RestorePose();
        if (information != null) Destroy(information);
    }
}
