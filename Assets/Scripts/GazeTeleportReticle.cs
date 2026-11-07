using UnityEngine;
using UnityEngine.UI;

// The UI stays outside the camera hierarchy so it cannot affect its editor handles.
public class GazeTeleportReticle : MonoBehaviour
{
    public GameObject Root { get; private set; }
    private Text countdown;
    private GameObject countdownPanel;
    private Transform viewTransform;
    private float viewDistance;
    private Image transitionFade;
    private GameObject ringRoot;
    private GameObject[] ringSegments;
    private const float StereoUnitsPerPixel = 0.002f;

    public static GazeTeleportReticle Create(Camera camera)
    {
        GameObject root = new GameObject("Gaze Reticle Canvas", typeof(RectTransform), typeof(Canvas));
        root.layer = camera.gameObject.layer;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.sortingOrder = 1000;
        float distance = Mathf.Max(1f, camera.nearClipPlane + 0.2f);
        // Mobile XR may start after this camera's Start callback.
        bool worldSpace = Application.isMobilePlatform || camera.stereoEnabled;
        if (worldSpace)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            root.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward * distance,
                camera.transform.rotation);
            root.transform.localScale = Vector3.one * StereoUnitsPerPixel;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 120f);
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        // Keep the drawing on a child: its centered rect is independent of canvas sizing.
        GameObject drawing = new GameObject("Center Dot and Progress", typeof(RectTransform),
            typeof(GazeTeleportReticle));
        drawing.layer = root.layer;
        drawing.transform.SetParent(root.transform, false);
        RectTransform drawingRect = drawing.GetComponent<RectTransform>();
        drawingRect.anchorMin = drawingRect.anchorMax = new Vector2(0.5f, 0.5f);
        drawingRect.pivot = new Vector2(0.5f, 0.5f);
        drawingRect.anchoredPosition = Vector2.zero;
        drawingRect.sizeDelta = new Vector2(160f, 120f);
        GazeTeleportReticle reticle = drawing.GetComponent<GazeTeleportReticle>();
        reticle.Root = root;
        if (worldSpace)
        {
            reticle.viewTransform = camera.transform;
            reticle.viewDistance = distance;
        }
        reticle.BuildIndicator(drawing.transform);
        reticle.countdownPanel = new GameObject("Countdown", typeof(RectTransform), typeof(Image));
        reticle.countdownPanel.layer = root.layer;
        reticle.countdownPanel.transform.SetParent(drawing.transform, false);
        RectTransform panelRect = reticle.countdownPanel.GetComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(84f, 26f);
        panelRect.anchoredPosition = new Vector2(0f, -42f);
        Image panel = reticle.countdownPanel.GetComponent<Image>();
        panel.color = Color.black;
        panel.raycastTarget = false;

        GameObject label = new GameObject("Seconds Remaining", typeof(RectTransform), typeof(Text));
        label.layer = root.layer;
        label.transform.SetParent(panelRect, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        reticle.countdown = label.GetComponent<Text>();
        reticle.countdown.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        reticle.countdown.fontSize = 18;
        reticle.countdown.alignment = TextAnchor.MiddleCenter;
        reticle.countdown.color = Color.white;
        reticle.countdown.raycastTarget = false;
        reticle.SetProgress(false, 0f, 0f);
        return reticle;
    }

    private void OnEnable() { Application.onBeforeRender += UpdatePose; }
    private void OnDisable() { Application.onBeforeRender -= UpdatePose; }
    private void LateUpdate() { UpdatePose(); }

    private void UpdatePose()
    {
        if (viewTransform != null && Root != null)
        {
            Root.transform.SetPositionAndRotation(
                viewTransform.position + viewTransform.forward * viewDistance, viewTransform.rotation);
        }
    }

    public void SetProgress(bool isGazing, float fraction, float secondsRemaining)
    {
        float progress = Mathf.Clamp01(fraction);
        ringRoot.SetActive(isGazing);
        for (int i = 0; i < ringSegments.Length; i++)
            ringSegments[i].SetActive((i + 0.5f) / ringSegments.Length <= progress);
        countdownPanel.SetActive(isGazing);
        if (isGazing)
        {
            // Round up so the countdown never reads zero before teleporting.
            float seconds = Mathf.Ceil(secondsRemaining * 10f) / 10f;
            countdown.text = seconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
        }
    }

    public void SetFade(float alpha)
    {
        if (transitionFade == null)
        {
            GameObject overlay = new GameObject("Portal Transition Fade", typeof(RectTransform), typeof(Image));
            overlay.layer = Root.layer;
            overlay.transform.SetParent(Root.transform, false);
            RectTransform rect = overlay.GetComponent<RectTransform>();
            if (viewTransform != null)
            {
                // Put the stereo fade just beyond the near plane, covering both eye views.
                float near = viewTransform.GetComponent<Camera>().nearClipPlane + 0.02f;
                rect.localPosition = new Vector3(0f, 0f, (near - viewDistance) / StereoUnitsPerPixel);
                rect.sizeDelta = new Vector2(10000f, 10000f);
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            transitionFade = overlay.GetComponent<Image>();
            transitionFade.raycastTarget = false;
        }
        transitionFade.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        transitionFade.gameObject.SetActive(alpha > 0f);
    }

    private void BuildIndicator(Transform parent)
    {
        ringRoot = new GameObject("Progress Ring - UI Quads", typeof(RectTransform));
        ringRoot.transform.SetParent(parent, false);
        ringSegments = new GameObject[48];
        for (int i = 0; i < ringSegments.Length; i++)
        {
            float angle = 90f - i * 360f / ringSegments.Length;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 20.5f;
            QuadImage("Ring Background", ringRoot.transform, position, new Vector2(3.2f, 7f), angle - 90f, Color.black);
        }
        for (int i = 0; i < ringSegments.Length; i++)
        {
            float angle = 90f - i * 360f / ringSegments.Length;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 position = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * 20.5f;
            ringSegments[i] = QuadImage("Progress Segment", ringRoot.transform, position,
                new Vector2(2.6f, 3f), angle - 90f, Color.white);
        }
        // Overlapping rectangular UI images form the small outlined center dot.
        for (int i = 0; i < 6; i++)
            QuadImage("Dot Outline", parent, Vector2.zero, new Vector2(8f, 6f), i * 30f, Color.black);
        for (int i = 0; i < 6; i++)
            QuadImage("White Center Dot", parent, Vector2.zero, new Vector2(4.5f, 3.5f), i * 30f, Color.white);
    }
    private static GameObject QuadImage(string name, Transform parent, Vector2 position, Vector2 size, float angle, Color color)
    {
        GameObject quad = new GameObject(name, typeof(RectTransform), typeof(Image));
        quad.layer = parent.gameObject.layer;
        quad.transform.SetParent(parent, false);
        RectTransform rect = quad.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = quad.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return quad;
    }
}
