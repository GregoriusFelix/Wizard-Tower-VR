using System.Collections;
using Google.XR.Cardboard;
using UnityEngine;
using UnityEngine.InputSystem.XR;

[DisallowMultipleComponent]
public class CardboardPlayerRuntime : MonoBehaviour
{
    public TrackedPoseDriver headTracking;
    private bool ready;

    private void OnEnable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        StartCoroutine(InitializeCardboard());
#endif
    }

    private IEnumerator InitializeCardboard()
    {
        float warningTime = Time.realtimeSinceStartup + 10f;
        bool warned = false;
        while (!UnityEngine.XR.XRSettings.isDeviceActive)
        {
            if (!warned && Time.realtimeSinceStartup > warningTime)
            {
                Debug.LogWarning("Cardboard: waiting for XR startup. Check the Android Cardboard provider and device logs.", this);
                warned = true;
            }
            yield return null;
        }
        if (headTracking == null)
        {
            Debug.LogError("Cardboard: assign the camera's Tracked Pose Driver to Head Tracking.", this);
            yield break;
        }
        // Bind tracking after the XR provider starts so its device is available.
        headTracking.enabled = true;
        ready = true;
        if (!Api.HasDeviceParams()) Api.ScanDeviceParams();
        Debug.Log("Cardboard: XR active and headset rotation enabled. Movement uses gaze teleport pads.", this);
    }

    private void Update()
    {
        if (!ready) return;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Api.IsGearButtonPressed) Api.ScanDeviceParams();
        if (Api.IsCloseButtonPressed) Application.Quit();
        if (Api.IsTriggerHeldPressed) Api.Recenter();
        if (Api.HasNewDeviceParams()) Api.ReloadDeviceParams();
        Api.UpdateScreenParams();
#endif
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ready = false;
        if (headTracking != null) headTracking.enabled = false;
    }
}
