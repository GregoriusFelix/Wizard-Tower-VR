using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazeEyeSwarm : MonoBehaviour
{
    private readonly List<Transform> eyes = new List<Transform>();
    private readonly List<float> scales = new List<float>();
    private Transform viewer;
    private GameObject group;
    private Quaternion facingOffset;
    private float turnSpeed;
    private float spawnTime;

    public void Spawn(Transform sourcePivot, Renderer iris, Renderer white, Transform target,
        int count, float radius, float scale, float speed)
    {
        Clear();
        if (count <= 0 || target == null) return;
        viewer = target;
        turnSpeed = speed;
        spawnTime = Time.time;
        Vector3 forward = iris.bounds.center - white.bounds.center;
        facingOffset = Quaternion.Inverse(Quaternion.LookRotation(forward, sourcePivot.up)) * sourcePivot.rotation;
        group = new GameObject("Summoned Watching Eyes");
        // Keep the original eye as their gaze owner without inheriting its look rotation.
        group.transform.SetParent(transform, true);
        MeshRenderer[] parts = sourcePivot.GetComponentsInChildren<MeshRenderer>();
        MaterialPropertyBlock appearance = new MaterialPropertyBlock();
        for (int i = 0; i < Mathf.Min(count, 20); i++)
        {
            Transform eye = new GameObject("Watching Eye " + (i + 1)).transform;
            eye.SetPositionAndRotation(sourcePivot.position, sourcePivot.rotation);
            eye.SetParent(group.transform, true);
            foreach (MeshRenderer part in parts)
            {
                MeshFilter sourceMesh = part.GetComponent<MeshFilter>();
                if (sourceMesh == null || sourceMesh.sharedMesh == null) continue;
                GameObject copy = new GameObject(part.name, typeof(MeshFilter), typeof(MeshRenderer));
                copy.layer = part.gameObject.layer;
                copy.transform.SetPositionAndRotation(part.transform.position, part.transform.rotation);
                copy.transform.localScale = part.transform.lossyScale;
                copy.transform.SetParent(eye, true);
                copy.GetComponent<MeshFilter>().sharedMesh = sourceMesh.sharedMesh;
                MeshRenderer renderer = copy.GetComponent<MeshRenderer>();
                renderer.sharedMaterials = part.sharedMaterials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = part.receiveShadows;
                part.GetPropertyBlock(appearance);
                renderer.SetPropertyBlock(appearance);
                if (part == white)
                {
                    BoxCollider collider = copy.AddComponent<BoxCollider>();
                    collider.center = sourceMesh.sharedMesh.bounds.center;
                    collider.size = sourceMesh.sharedMesh.bounds.size;
                }
            }
            float angle = i * Mathf.PI * 2f / Mathf.Min(count, 20) + 0.3f;
            float distance = radius * (i % 2 == 0 ? 1f : 0.78f);
            eye.position = sourcePivot.position + new Vector3(Mathf.Cos(angle) * distance,
                2f + (i % 3) * 3f, Mathf.Sin(angle) * distance);
            Vector3 direction = viewer.position - eye.position;
            eye.rotation = LookAt(direction) * facingOffset;
            eyes.Add(eye);
            scales.Add(scale * (0.8f + (i % 3) * 0.15f));
            eye.localScale = Vector3.zero;
        }
    }

    private static Quaternion LookAt(Vector3 direction)
    {
        Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f
            ? Vector3.forward : Vector3.up;
        return Quaternion.LookRotation(direction, up);
    }

    private void LateUpdate()
    {
        if (viewer == null) return;
        float growth = Mathf.SmoothStep(0f, 1f, (Time.time - spawnTime) / 0.8f);
        for (int i = 0; i < eyes.Count; i++)
        {
            Transform eye = eyes[i];
            if (eye == null) continue;
            eye.localScale = Vector3.one * (scales[i] * growth);
            Vector3 direction = viewer.position - eye.position;
            if (direction.sqrMagnitude > 0.0001f)
                eye.rotation = Quaternion.RotateTowards(eye.rotation, LookAt(direction) * facingOffset,
                    turnSpeed * Time.deltaTime);
        }
    }

    public void Clear()
    {
        if (group != null)
        {
            group.SetActive(false);
            Destroy(group);
        }
        group = null;
        viewer = null;
        eyes.Clear();
        scales.Clear();
    }

    private void OnDisable() { Clear(); }
    private void OnDestroy() { Clear(); }
}
