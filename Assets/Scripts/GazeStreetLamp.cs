using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeStreetLamp : MonoBehaviour
{
    public Transform[] lanterns;
    public Transform[] chains;
    [Min(0.1f)] public float swingDuration = 5f;
    [Range(1, 8)] public int swingCount = 3;
    [Range(0f, 30f)] public float swingAngle = 14f;

    public bool IsSwinging { get; private set; }
    private Transform[] pivots;
    private float elapsed;
    private AudioSource chainSound;

    private void Reset()
    {
        FindParts();
    }

    private void FindParts()
    {
        List<Transform> foundLanterns = new List<Transform>();
        List<Transform> foundChains = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Lantern", System.StringComparison.Ordinal)) foundLanterns.Add(child);
            if (child.name.StartsWith("Chain", System.StringComparison.Ordinal)) foundChains.Add(child);
        }
        if (lanterns == null || lanterns.Length == 0) lanterns = foundLanterns.ToArray();
        if (chains == null || chains.Length == 0) chains = foundChains.ToArray();
    }

    private void Awake()
    {
        FindParts();
        if (lanterns.Length == 0 || chains.Length == 0)
        {
            FailSetup();
            return;
        }

        Bounds[] lanternBounds = new Bounds[lanterns.Length];
        List<Transform>[] groups = new List<Transform>[lanterns.Length];
        for (int i = 0; i < lanterns.Length; i++)
        {
            if (!TryGetBounds(lanterns[i], out lanternBounds[i]))
            {
                FailSetup();
                return;
            }
            groups[i] = new List<Transform>();
        }

        Bounds[] chainBounds = new Bounds[chains.Length];
        int[] owner = new int[chains.Length];
        for (int i = 0; i < chains.Length; i++)
        {
            if (!TryGetBounds(chains[i], out chainBounds[i]))
            {
                FailSetup();
                return;
            }
            float closest = float.PositiveInfinity;
            for (int j = 0; j < lanterns.Length; j++)
            {
                Vector3 offset = chainBounds[i].center - lanternBounds[j].center;
                offset.y = 0f;
                if (offset.sqrMagnitude < closest)
                {
                    closest = offset.sqrMagnitude;
                    owner[i] = j;
                }
            }
            groups[owner[i]].Add(chains[i]);
        }

        for (int i = 0; i < groups.Length; i++)
        {
            if (groups[i].Count == 0)
            {
                FailSetup();
                return;
            }
        }

        pivots = new Transform[lanterns.Length];
        for (int i = 0; i < lanterns.Length; i++)
        {
            Bounds topLink = new Bounds();
            float top = float.NegativeInfinity;
            for (int j = 0; j < chains.Length; j++)
            {
                if (owner[j] == i && chainBounds[j].max.y > top)
                {
                    topLink = chainBounds[j];
                    top = topLink.max.y;
                }
            }
            Transform pivot = new GameObject(lanterns[i].name + " Swing Pivot").transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = new Vector3(topLink.center.x, topLink.max.y, topLink.center.z);
            foreach (Transform chain in groups[i]) chain.SetParent(pivot, true);
            lanterns[i].SetParent(pivot, true);
            pivots[i] = pivot;
        }
    }

    private bool TryGetBounds(Transform part, out Bounds result)
    {
        result = new Bounds();
        if (part == null || part == transform || !part.IsChildOf(transform)) return false;
        bool found = false;
        foreach (Renderer mesh in part.GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = mesh.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                point = transform.InverseTransformPoint(mesh.transform.TransformPoint(point));
                if (!found)
                {
                    result = new Bounds(point, Vector3.zero);
                    found = true;
                }
                else result.Encapsulate(point);
            }
        }
        return found;
    }

    private void FailSetup()
    {
        Debug.LogError("GazeStreetLamp: add this component to StreetLamp and assign its lantern and chain groups.", this);
        enabled = false;
    }

    public void Swing()
    {
        if (!isActiveAndEnabled || IsSwinging || pivots == null) return;
        elapsed = 0f;
        IsSwinging = true;
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref chainSound, pivots[0], audio != null ? audio.chain : null, 0.85f, true);
    }

    private void Update()
    {
        if (!IsSwinging) return;
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, swingDuration));
        if (t >= 1f)
        {
            RestorePose();
            return;
        }

        float envelope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 8f)) * (1f - t) * (1f - t);
        for (int i = 0; i < pivots.Length; i++)
        {
            float phase = t * Mathf.PI * 2f * Mathf.Max(1, swingCount) + i * 0.35f;
            float forward = Mathf.Sin(phase) * swingAngle * envelope;
            float sideways = Mathf.Sin(phase * 0.8f) * swingAngle * 0.35f * envelope;
            pivots[i].localRotation = Quaternion.Euler(forward, 0f, sideways);
        }
    }

    private void OnDisable()
    {
        RestorePose();
    }

    private void RestorePose()
    {
        GazeAudio.Stop(chainSound);
        if (pivots != null)
            foreach (Transform pivot in pivots)
                if (pivot != null) pivot.localRotation = Quaternion.identity;
        IsSwinging = false;
        elapsed = 0f;
    }
}
