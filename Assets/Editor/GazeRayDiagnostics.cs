using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GazeRayDiagnostics
{
    private static double nextCapture;

    static GazeRayDiagnostics()
    {
        EditorApplication.update += Capture;
    }

    private static void Capture()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextCapture ||
            !File.Exists("Temp/GazeTrace.request")) return;
        nextCapture = EditorApplication.timeSinceStartup + 1;
        Camera camera = Camera.main;
        if (camera == null) return;
        StringBuilder result = new StringBuilder();
        result.AppendLine(DateTime.UtcNow.ToString("O"));
        result.AppendLine("Camera: " + camera.name + " position=" + camera.transform.position.ToString("F4") +
            " forward=" + camera.transform.forward.ToString("F4") + " paused=" + EditorApplication.isPaused);
        GazeTeleport gaze = camera.GetComponent<GazeTeleport>();
        if (gaze != null)
        {
            result.AppendLine("Gaze enabled=" + gaze.enabled + " duration=" + gaze.gazeDuration + " range=" + gaze.maxDistance);
            foreach (string field in new[] { "currentTarget", "waitForLookAway", "gazeTimer", "isTransitioning" })
                result.AppendLine(field + "=" + typeof(GazeTeleport).GetField(field,
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(gaze));
        }
        RaycastHit[] hits = Physics.RaycastAll(camera.transform.position, camera.transform.forward,
            100f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            string path = hit.collider.name;
            for (Transform parent = hit.collider.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            result.AppendLine("HIT " + hit.distance.ToString("F3") + " " + path + " tag=" + hit.collider.tag +
                " type=" + hit.collider.GetType().Name + " trigger=" + hit.collider.isTrigger +
                " bounds=" + hit.collider.bounds);
            foreach (GazePortal portal in hit.collider.GetComponentsInParent<GazePortal>())
                result.AppendLine("PORTAL " + portal.name + " acceptsGaze=" + portal.CanGazeAt(hit.collider.transform) +
                    " destination=" + portal.destinationPortal);
        }
        File.WriteAllText("Temp/GazeTrace.txt", result.ToString());
    }
}
