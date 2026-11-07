using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class GazeGolemAwakening : MonoBehaviour
{
    public GazeTomb[] tombs;
    public Material dustMaterial;
    public Renderer[] staffSpheres;
    [Min(0.2f)] public float chargeDuration = 2.4f;
    [Min(0.5f)] public float assemblyDuration = 6f;
    [Min(0.1f)] public float windupDuration = 1.8f;
    [Min(0f)] public float windupHoldDuration = 0.45f;
    [Min(0.1f)] public float smashDuration = 0.6f;
    [Min(0.2f)] public float recoveryDuration = 2.4f;
    [Min(0.2f)] public float earthquakeDuration = 2f;
    [Min(1f)] public float destructionSpread = 3.5f;
    [Min(0.5f)] public float destructionDuration = 4.5f;
    [Header("Standing idle and player tracking")]
    [Min(0.5f)] public float idleBreathDuration = 3.8f;
    [Min(0f)] public float idleBreathHeight = 0.18f;
    [Range(0f, 8f)] public float idleArmSway = 3.5f;
    [Min(1f)] public float turnSpeed = 65f;
    public bool HasAwakened { get; private set; }
    public bool IsAnimating { get; private set; }
    public bool IsDefeated { get; private set; }
    public bool IsDefeating { get; private set; }
    public bool CanBeAttacked => initialized && isActiveAndEnabled && HasAwakened && !IsAnimating && !IsDefeated;
    public bool BothCoresLit => tombs != null && tombs.Length == 2 && tombs[0] != null && tombs[1] != null && tombs[0].IsLit && tombs[1].IsLit;
    public bool IsIntroducing => initialized && isActiveAndEnabled && BothCoresLit && !HasAwakened && !IsDefeated;
    public Vector3 GroundPosition => transform.TransformPoint(new Vector3(bodyBounds.center.x, bodyBounds.min.y, bodyBounds.center.z));
    public Bounds CombatBounds
    {
        get
        {
            Bounds result = new Bounds(transform.TransformPoint(bodyBounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                result.Encapsulate(transform.TransformPoint(bodyBounds.center + Vector3.Scale(bodyBounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f))));
            return result;
        }
    }

    private class Stone
    {
        public Transform part;
        public Renderer renderer;
        public Vector3 position, pilePosition;
        public Quaternion rotation, pileRotation;
        public Bounds meshBounds;
        public int arm;
        public bool decoration, visible;
        public bool head;
        public Vector3 defeatedPosition;
        public Quaternion defeatedRotation;
    }

    private readonly List<Stone> stones = new List<Stone>();
    private readonly List<Collider> colliders = new List<Collider>();
    private readonly List<bool> colliderStates = new List<bool>();
    private readonly Vector3[] shoulders = new Vector3[2];
    private readonly List<Transform> debris = new List<Transform>();
    private readonly List<LineRenderer> rings = new List<LineRenderer>();
    private Bounds bodyBounds;
    private Transform effects;
    private ParticleSystem dust;
    private Material dustRuntime, debrisMaterial, ringMaterial;
    private Vector3 impact;
    private float crouchDepth;
    private bool initialized;
    private GolemStaffLightning lightning;
    private Vector3 pileCenter;
    private Vector3 neck, headCenter;
    private Camera trackedCamera;
    private AudioSource stompSound, rubbleSound;
    private float idleTime, idleBlend, headYaw, headPitch;

    private void Awake()
    {
        if (tombs == null || tombs.Length != 2 || tombs[0] == null || tombs[1] == null || tombs[0] == tombs[1])
        {
            Debug.LogError("GazeGolemAwakening: assign the two different tombs.", this);
            enabled = false;
            return;
        }
        foreach (Transform child in transform)
        {
            MeshFilter mesh = child.GetComponent<MeshFilter>();
            Renderer renderer = child.GetComponent<Renderer>();
            if (mesh == null || mesh.sharedMesh == null || renderer == null) continue;
            int number = -1;
            if (child.name.StartsWith("Cube (") && child.name.EndsWith(")"))
                int.TryParse(child.name.Substring(6, child.name.Length - 7), out number);
            Stone stone = new Stone {
                part = child, renderer = renderer, position = child.localPosition,
                rotation = child.localRotation, meshBounds = mesh.sharedMesh.bounds,
                decoration = number == 49 || number == 50 || child.name.StartsWith("Sphere"),
                head = number >= 48 && number <= 54 || child.name.StartsWith("Sphere"),
                arm = number >= 19 && number <= 31 ? 1 : number >= 32 && number <= 45 ? 2 : 0,
                visible = renderer.enabled
            };
            stones.Add(stone);
            Bounds bounds = PartBounds(stone);
            if (stones.Count == 1) bodyBounds = bounds;
            else bodyBounds.Encapsulate(bounds);
        }
        if (stones.Count == 0)
        {
            Debug.LogError("GazeGolemAwakening: add this component to the existing Golem mesh group.", this);
            enabled = false;
            return;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            colliders.Add(collider);
            colliderStates.Add(collider.enabled);
        }
        shoulders[0] = FindShoulder(1);
        shoulders[1] = FindShoulder(2);
        Stone headStone = stones.Find(stone => stone.part.name == "Cube (48)");
        Bounds headBounds = headStone != null ? PartBounds(headStone) : bodyBounds;
        headCenter = headBounds.center;
        neck = new Vector3(headBounds.center.x, headBounds.min.y, headBounds.center.z);
        PreparePile();
        PrepareEffects();
        GameObject lightningRoot = new GameObject("Staff Awakening Lightning");
        lightningRoot.transform.SetParent(transform, false);
        lightning = lightningRoot.AddComponent<GolemStaffLightning>();
        FindStaffSpheres();
        lightning.Initialize(staffSpheres);
        initialized = true;
        ShowPile();
    }

    private void FindStaffSpheres()
    {
        Renderer[] assigned = staffSpheres;
        if (staffSpheres == null || staffSpheres.Length != 2) staffSpheres = new Renderer[2];
        Transform arena = GameObject.Find("Arena")?.transform;
        for (int i = 0; i < 2; i++)
        {
            if (staffSpheres[i] == null && assigned != null && i < assigned.Length) staffSpheres[i] = assigned[i];
            if (staffSpheres[i] == null && arena != null)
                staffSpheres[i] = arena.Find(i == 0 ? "Staff White/Sphere" : "Staff Black/Sphere")?.GetComponent<Renderer>();
        }
    }

    private Bounds PartBounds(Stone stone)
    {
        Bounds result = new Bounds();
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 offset = Vector3.Scale(stone.meshBounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
            Vector3 point = transform.InverseTransformPoint(stone.part.TransformPoint(stone.meshBounds.center + offset));
            if (corner == 0) result = new Bounds(point, Vector3.zero);
            else result.Encapsulate(point);
        }
        return result;
    }

    private Vector3 FindShoulder(int arm)
    {
        Stone highest = null;
        foreach (Stone stone in stones)
            if (stone.arm == arm && (highest == null || stone.position.y > highest.position.y)) highest = stone;
        if (highest == null) return bodyBounds.center;
        Bounds bounds = PartBounds(highest);
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    private void PreparePile()
    {
        System.Random random = new System.Random(7319);
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            float angle = i * 2.399963f;
            float radius = Mathf.Sqrt((i + 0.5f) / stones.Count);
            stone.pileRotation = Quaternion.Euler((float)random.NextDouble() * 180f,
                (float)random.NextDouble() * 360f, (float)random.NextDouble() * 180f);
            stone.part.localRotation = stone.pileRotation;
            stone.part.localPosition = Vector3.zero;
            Bounds bounds = PartBounds(stone);
            float mound = (1f - radius) * bodyBounds.size.y * 0.055f;
            stone.pilePosition = new Vector3(
                bodyBounds.center.x + Mathf.Cos(angle) * radius * bodyBounds.size.z * 0.3f - bounds.center.x,
                bodyBounds.min.y - bounds.min.y + mound,
                bodyBounds.center.z + Mathf.Sin(angle) * radius * bodyBounds.size.z * 0.43f - bounds.center.z);
            stone.part.SetLocalPositionAndRotation(stone.position, stone.rotation);
        }
        Quaternion strike = Quaternion.AngleAxis(18f, Vector3.forward);
        float bottom = float.PositiveInfinity;
        Vector3[] contacts = new Vector3[2];
        float[] lowest = { float.PositiveInfinity, float.PositiveInfinity };
        foreach (Stone stone in stones)
        {
            if (stone.arm == 0) continue;
            Vector3 shoulder = shoulders[stone.arm - 1];
            stone.part.SetLocalPositionAndRotation(shoulder + strike * (stone.position - shoulder), strike * stone.rotation);
            Bounds bounds = PartBounds(stone);
            bottom = Mathf.Min(bottom, bounds.min.y);
            if (bounds.min.y < lowest[stone.arm - 1])
            {
                lowest[stone.arm - 1] = bounds.min.y;
                contacts[stone.arm - 1] = bounds.center;
            }
            stone.part.SetLocalPositionAndRotation(stone.position, stone.rotation);
        }
        crouchDepth = float.IsInfinity(bottom) ? bodyBounds.size.y * 0.2f : Mathf.Max(0f, bottom - bodyBounds.min.y);
        Vector3 fists = (contacts[0] + contacts[1]) * 0.5f;
        impact = new Vector3(fists.x, bodyBounds.min.y + 0.06f, fists.z);
        pileCenter = new Vector3(bodyBounds.center.x, bodyBounds.min.y + bodyBounds.size.y * 0.07f, bodyBounds.center.z);
    }

    private void Update()
    {
        if (!initialized || HasAwakened || IsAnimating) return;
        if (BothCoresLit)
            StartCoroutine(Awaken());
    }

    private void LateUpdate()
    {
        if (!CanBeAttacked) return;
        if (trackedCamera == null || !trackedCamera.isActiveAndEnabled) trackedCamera = Camera.main;
        if (trackedCamera != null)
        {
            Vector3 toward = Vector3.ProjectOnPlane(trackedCamera.transform.position - GroundPosition, Vector3.up);
            if (toward.sqrMagnitude > 0.01f)
            {
                float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(transform.right, Vector3.up), toward, Vector3.up);
                float turn = Mathf.Clamp(angle, -turnSpeed * Time.deltaTime, turnSpeed * Time.deltaTime);
                transform.RotateAround(GroundPosition, Vector3.up, turn);
            }
            Vector3 localLook = transform.InverseTransformDirection(trackedCamera.transform.position - transform.TransformPoint(headCenter));
            float yaw = Mathf.Clamp(-Mathf.Atan2(localLook.z, localLook.x) * Mathf.Rad2Deg, -45f, 45f);
            float pitch = Mathf.Clamp(Mathf.Atan2(localLook.y, new Vector2(localLook.x, localLook.z).magnitude) * Mathf.Rad2Deg, -35f, 15f);
            float tracking = 1f - Mathf.Exp(-Time.deltaTime * 5f);
            headYaw = Mathf.LerpAngle(headYaw, yaw, tracking);
            headPitch = Mathf.LerpAngle(headPitch, pitch, tracking);
        }
        idleTime += Time.deltaTime;
        idleBlend = Mathf.MoveTowards(idleBlend, 1f, Time.deltaTime / 1.2f);
        float phase = idleTime * Mathf.PI * 2f / Mathf.Max(0.5f, idleBreathDuration);
        float breath = Mathf.Sin(phase) * idleBreathHeight * idleBlend;
        Vector3 hip = new Vector3(bodyBounds.center.x, bodyBounds.min.y + bodyBounds.size.y * 0.28f, bodyBounds.center.z);
        Quaternion bodyLean = Quaternion.Euler(Mathf.Sin(phase * 0.52f) * 0.75f * idleBlend,
            0f, Mathf.Sin(phase) * 0.45f * idleBlend);
        Quaternion headLook = Quaternion.Slerp(Quaternion.identity,
            Quaternion.AngleAxis(headYaw, Vector3.up)
                * Quaternion.AngleAxis(headPitch + Mathf.Sin(phase * 0.7f) * 1.2f, Vector3.forward), idleBlend);
        foreach (Stone stone in stones)
        {
            if (stone.part == null) continue;
            Vector3 position = stone.position;
            Quaternion rotation = stone.rotation;
            if (stone.arm != 0)
            {
                float offset = stone.arm == 1 ? 0f : Mathf.PI;
                Quaternion swing = Quaternion.AngleAxis(Mathf.Sin(phase * 0.65f + offset) * idleArmSway * idleBlend, Vector3.forward);
                Vector3 shoulder = shoulders[stone.arm - 1];
                position = shoulder + swing * (position - shoulder);
                rotation = swing * rotation;
            }
            if (stone.head)
            {
                position = neck + headLook * (position - neck);
                rotation = headLook * rotation;
            }
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(
                (stone.position.y - bodyBounds.min.y - bodyBounds.size.y * 0.18f) / (bodyBounds.size.y * 0.24f)));
            Quaternion lean = Quaternion.Slerp(Quaternion.identity, bodyLean, weight);
            position = hip + lean * (position - hip) + Vector3.up * (breath * weight);
            stone.part.SetLocalPositionAndRotation(position, lean * rotation);
        }
    }

    private IEnumerator Awaken()
    {
        IsAnimating = true;
        SetColliders(false);
        for (float time = 0f; time < chargeDuration; time += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(time / chargeDuration);
            float charge = Ease(progress / 0.45f);
            float power = Ease((progress - 0.3f) / 0.25f);
            lightning.Animate(transform.TransformPoint(pileCenter), charge, power, time);
            float tremble = Mathf.Sin(progress * Mathf.PI) * bodyBounds.size.y * 0.002f * power;
            for (int i = 0; i < stones.Count; i++)
            {
                Stone stone = stones[i];
                stone.part.localPosition = stone.pilePosition + new Vector3(
                    Mathf.Sin(time * 13f + i * 2.1f), Mathf.Sin(time * 17f + i), Mathf.Cos(time * 11f + i)) * tremble;
            }
            yield return null;
        }
        ShowPile();
        for (float time = 0f; time < assemblyDuration; time += Time.deltaTime)
        {
            float progress = Mathf.Clamp01(time / assemblyDuration);
            float power = 1f - Ease((progress - 0.12f) / 0.48f);
            if (power > 0f) lightning.Animate(transform.TransformPoint(pileCenter), power, power, chargeDuration + time);
            else lightning.Hide();
            foreach (Stone stone in stones)
            {
                float height = Mathf.Clamp01((stone.position.y - bodyBounds.min.y) / bodyBounds.size.y);
                float delay = height * 0.32f + (stone.arm != 0 ? 0.06f : 0f);
                float t = Ease((progress - delay) / 0.62f);
                Vector3 radial = stone.pilePosition - pileCenter;
                radial.y = 0f;
                Vector3 curl = Vector3.Cross(Vector3.up, radial).normalized * bodyBounds.size.y * 0.035f;
                Vector3 first = stone.pilePosition + Vector3.up * bodyBounds.size.y * 0.09f + curl;
                Vector3 second = stone.position + Vector3.up * bodyBounds.size.y * 0.035f + curl * 0.35f;
                float remaining = 1f - t;
                Vector3 position = remaining * remaining * remaining * stone.pilePosition
                    + 3f * remaining * remaining * t * first + 3f * remaining * t * t * second + t * t * t * stone.position;
                stone.part.SetLocalPositionAndRotation(position,
                    Quaternion.Slerp(stone.pileRotation, stone.rotation, t));
                stone.renderer.enabled = stone.visible && (!stone.decoration || t >= 0.9f);
            }
            yield return null;
        }
        lightning.Hide();
        RestorePose();
        // Let the completed silhouette read before the first deliberate movement.
        yield return new WaitForSeconds(0.5f);
        for (float time = 0f; time < windupDuration; time += Time.deltaTime)
        {
            PoseSmash(Ease(time / windupDuration) * 125f, 0f);
            yield return null;
        }
        PoseSmash(125f, 0f);
        yield return new WaitForSeconds(windupHoldDuration);
        for (float time = 0f; time < smashDuration; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / smashDuration);
            t = t * t * t;
            PoseSmash(Mathf.Lerp(125f, 18f, t), t);
            yield return null;
        }
        PoseSmash(18f, 1f);
        InteractionAudio audio = InteractionAudio.Current;
        PlayGolemSound(ref stompSound, audio != null ? audio.golemStomp : null, transform.TransformPoint(impact), 1f,
            audio != null ? audio.golemStompSpatialBlend : 0.2f, audio != null ? audio.golemStompStartTime : 0.75f);
        effects.gameObject.SetActive(true);
        if (dust != null) dust.Play();
        for (float time = 0f; time < earthquakeDuration; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / earthquakeDuration);
            PoseSmash(18f, 1f);
            float tremor = Mathf.Sin(time * 43f) * (1f - t) * (1f - t) * bodyBounds.size.y * 0.0025f;
            foreach (Stone stone in stones)
                if (stone.position.y > bodyBounds.min.y + 2f) stone.part.localPosition += Vector3.up * tremor;
            AnimateImpact(t);
            yield return null;
        }
        effects.gameObject.SetActive(false);
        for (float time = 0f; time < recoveryDuration; time += Time.deltaTime)
        {
            float t = Ease(time / recoveryDuration);
            PoseSmash(Mathf.Lerp(18f, 0f, t), 1f - t);
            yield return null;
        }
        RestorePose();
        SetColliders(true);
        HasAwakened = true;
        IsAnimating = false;
    }

    private static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    public void Defeat(GolemElement element, Vector3 casterPosition)
    {
        if (CanBeAttacked) StartCoroutine(Scatter(element, casterPosition));
    }

    public void ResetToStanding()
    {
        if (!initialized) return;
        GazeAudio.Stop(stompSound);
        GazeAudio.Stop(rubbleSound);
        StopAllCoroutines();
        IsDefeated = IsDefeating = IsAnimating = false;
        HasAwakened = true;
        idleTime = idleBlend = headYaw = headPitch = 0f;
        if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effects.gameObject.SetActive(false);
        if (lightning != null) lightning.Hide();
        RestorePose();
        SetColliders(true);
        Physics.SyncTransforms();
    }

    private IEnumerator Scatter(GolemElement element, Vector3 casterPosition)
    {
        IsDefeated = IsDefeating = IsAnimating = true;
        GazeAudio.Stop(stompSound);
        InteractionAudio audio = InteractionAudio.Current;
        PlayGolemSound(ref rubbleSound, audio != null ? audio.stoneFalling : null, GroundPosition, 0.85f);
        SetColliders(false);
        Vector3 caster = transform.InverseTransformPoint(casterPosition);
        Vector3 center = new Vector3(bodyBounds.center.x, bodyBounds.min.y, bodyBounds.center.z);
        Vector3 push = Vector3.ProjectOnPlane(center - caster, Vector3.up).normalized;
        if (push.sqrMagnitude < 0.01f) push = Vector3.right;
        Vector3 sideways = Vector3.Cross(Vector3.up, push);
        System.Random random = new System.Random(8217 + (int)element * 173);
        Vector3[] starts = new Vector3[stones.Count];
        Quaternion[] rotations = new Quaternion[stones.Count];
        Vector3[] landings = new Vector3[stones.Count];
        float[] groundHeights = new float[stones.Count];
        float[] launchSpeeds = new float[stones.Count];
        float[] flightTimes = new float[stones.Count];
        float[] bounceHeights = new float[stones.Count];
        float[] bounceTimes = new float[stones.Count];
        float gravity = Mathf.Max(1f, Mathf.Abs(Physics.gravity.y));
        float duration = Mathf.Max(0.1f, destructionDuration);
        float height = CombatBounds.size.y;
        // Use the visible flat floor mesh instead of the flattened cylinder's capsule.
        GazeSurfaceColliders.Repair();
        Collider terrain = GameObject.Find("Grass")?.GetComponent<Collider>();
        List<Collider> groundSurfaces = new List<Collider>();
        if (terrain != null && terrain.enabled && !terrain.isTrigger) groundSurfaces.Add(terrain);
        Transform arena = GameObject.Find("Arena")?.transform;
        if (arena != null)
            foreach (Transform child in arena)
            {
                // Staffs and other tall props are not landing surfaces.
                Renderer floor = child.GetComponent<Renderer>();
                if (floor == null || floor.bounds.size.y >= Mathf.Min(floor.bounds.size.x, floor.bounds.size.z) * 0.15f)
                    continue;
                foreach (Collider surface in child.GetComponents<Collider>())
                    if (surface.enabled && !surface.isTrigger) groundSurfaces.Add(surface);
            }
        float fallbackGround = terrain != null ? terrain.bounds.max.y : GroundPosition.y;
        for (int i = 0; i < stones.Count; i++)
        {
            Stone stone = stones[i];
            starts[i] = stone.part.position;
            rotations[i] = stone.part.rotation;
            float angle = i * 2.399963f;
            float distance = bodyBounds.size.y * (0.13f + (float)random.NextDouble() * 0.24f) * destructionSpread;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 landing = center + radial * distance;
            if (element == GolemElement.Water)
                landing = center + push * distance + sideways * (Mathf.Sin(angle) * distance * 0.7f);
            // Keep a clear landing area around the caster.
            Vector3 fromCaster = Vector3.ProjectOnPlane(landing - caster, Vector3.up);
            if (fromCaster.magnitude < 4f) landing = center + push * (distance + 2f);
            // A cuboid comes to rest on a face rather than balancing on a corner.
            Quaternion restingRotation = Quaternion.Euler(random.Next(4) * 90f,
                (float)random.NextDouble() * 360f, random.Next(4) * 90f);
            stone.defeatedRotation = Quaternion.Inverse(transform.rotation) * restingRotation;
            stone.part.SetPositionAndRotation(Vector3.zero, restingRotation);
            Bounds rockBounds = stone.renderer.bounds;
            Vector3 worldLanding = transform.TransformPoint(landing);
            if (terrain != null)
            {
                Bounds ground = terrain.bounds;
                float padding = Mathf.Max(rockBounds.extents.x, rockBounds.extents.z) + 0.2f;
                worldLanding.x = Mathf.Clamp(worldLanding.x, ground.min.x + padding, ground.max.x - padding);
                worldLanding.z = Mathf.Clamp(worldLanding.z, ground.min.z + padding, ground.max.z - padding);
            }
            groundHeights[i] = ScatterGroundHeight(worldLanding, groundSurfaces, fallbackGround);
            worldLanding.y = groundHeights[i] - rockBounds.min.y + 0.005f;
            landings[i] = worldLanding;
            stone.defeatedPosition = transform.InverseTransformPoint(worldLanding);
            stone.part.SetPositionAndRotation(starts[i], rotations[i]);
            float launchHeight = height * ((element == GolemElement.Fire ? 0.10f : 0.04f)
                + (float)random.NextDouble() * 0.05f);
            launchSpeeds[i] = Mathf.Sqrt(2f * gravity * (launchHeight + Mathf.Max(0f, worldLanding.y - starts[i].y)));
            flightTimes[i] = (launchSpeeds[i] + Mathf.Sqrt(launchSpeeds[i] * launchSpeeds[i]
                + 2f * gravity * (starts[i].y - worldLanding.y))) / gravity;
            bounceHeights[i] = Mathf.Min(0.16f, rockBounds.size.y * 0.035f);
            bounceTimes[i] = 2f * Mathf.Sqrt(2f * bounceHeights[i] / gravity);
            duration = Mathf.Max(duration, flightTimes[i] + bounceTimes[i]);
            if (stone.decoration) stone.renderer.enabled = false;
        }
        effects.localPosition = center + Vector3.up * 0.06f;
        effects.gameObject.SetActive(true);
        if (dust != null) dust.Play();
        for (float time = 0f; time < duration; time += Time.deltaTime)
        {
            float t = Mathf.Clamp01(time / duration);
            for (int i = 0; i < stones.Count; i++)
            {
                Stone stone = stones[i];
                if (stone.part == null) continue;
                float u = Mathf.Clamp01(time / flightTimes[i]);
                Vector3 position = Vector3.Lerp(starts[i], landings[i], u);
                if (time < flightTimes[i])
                    position.y = starts[i].y + launchSpeeds[i] * time - 0.5f * gravity * time * time;
                else
                {
                    float bounce = Mathf.Clamp01((time - flightTimes[i]) / bounceTimes[i]);
                    position.y += 4f * bounce * (1f - bounce) * bounceHeights[i];
                }
                if (element == GolemElement.Wind && u < 1f)
                {
                    float spiral = Mathf.Sin(u * Mathf.PI) * bodyBounds.size.y * 0.18f;
                    position += new Vector3(Mathf.Sin(u * 9f + i), 0f, Mathf.Cos(u * 9f + i)) * spiral;
                }
                stone.part.SetPositionAndRotation(position,
                    Quaternion.Slerp(rotations[i], transform.rotation * stone.defeatedRotation, Ease(u))
                    * Quaternion.Euler(Mathf.Sin(u * Mathf.PI) * 280f, Mathf.Sin(u * Mathf.PI) * i * 19f,
                        Mathf.Sin(u * Mathf.PI) * (i % 3 - 1) * 170f));
                // Changing rotation must not put a tumbling corner below the floor.
                if (stone.renderer.bounds.min.y < groundHeights[i])
                    stone.part.position += Vector3.up * (groundHeights[i] - stone.renderer.bounds.min.y + 0.005f);
            }
            AnimateImpact(t);
            yield return null;
        }
        SetDefeatedPose();
        GazeAudio.Stop(rubbleSound);
        effects.gameObject.SetActive(false);
        IsDefeating = IsAnimating = false;
    }

    private static float ScatterGroundHeight(Vector3 position, List<Collider> surfaces, float fallback)
    {
        Ray ray = new Ray(position + Vector3.up * 100f, Vector3.down);
        float highest = float.NegativeInfinity;
        foreach (Collider surface in surfaces)
            if (surface != null && surface.Raycast(ray, out RaycastHit hit, 200f) && hit.normal.y > 0.8f)
                highest = Mathf.Max(highest, hit.point.y);
        return float.IsNegativeInfinity(highest) ? fallback : highest;
    }

    private void PlayGolemSound(ref AudioSource source, AudioClip clip, Vector3 position, float volume,
        float spatialBlend = 1f, float startTime = 0f)
    {
        GazeAudio.Play(ref source, transform, clip, volume, spatialBlend: spatialBlend, startTime: startTime);
        if (source == null) return;
        source.transform.position = position;
        source.minDistance = 6f;
        source.maxDistance = 110f;
    }

    private void SetDefeatedPose()
    {
        foreach (Stone stone in stones)
        {
            if (stone.part == null) continue;
            stone.part.SetLocalPositionAndRotation(stone.defeatedPosition, stone.defeatedRotation);
            stone.renderer.enabled = stone.visible && !stone.decoration;
        }
    }

    private void PoseSmash(float armAngle, float crouch)
    {
        Quaternion turn = Quaternion.AngleAxis(armAngle, Vector3.forward);
        foreach (Stone stone in stones)
        {
            Vector3 position = stone.position;
            Quaternion rotation = stone.rotation;
            float bend = Mathf.Clamp01((position.y - bodyBounds.min.y - 2f) / (bodyBounds.size.y * 0.3f));
            position += Vector3.down * (crouchDepth * crouch * bend);
            position += Vector3.right * (crouchDepth * crouch * bend * (1f - bend) * 0.55f);
            if (stone.arm != 0)
            {
                Vector3 shoulder = shoulders[stone.arm - 1];
                position = shoulder + turn * (stone.position - shoulder) + Vector3.down * (crouchDepth * crouch);
                rotation = turn * rotation;
            }
            stone.part.SetLocalPositionAndRotation(position, rotation);
        }
    }

    private void PrepareEffects()
    {
        effects = new GameObject("Golem Ground Impact").transform;
        effects.SetParent(transform, false);
        effects.localPosition = impact;
        Material reference = stones[0].renderer.sharedMaterial;
        debrisMaterial = new Material(reference) { name = "Golem Impact Stone" };
        ringMaterial = new Material(reference) { name = "Golem Ground Ripples" };
        foreach (Material material in new[] { debrisMaterial, ringMaterial })
        {
            foreach (string texture in material.GetTexturePropertyNames()) material.SetTexture(texture, null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", new Color(0.37f, 0.29f, 0.23f));
        }
        for (int i = 0; i < 18; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.name = "Impact Debris";
            rock.transform.SetParent(effects, false);
            rock.GetComponent<Collider>().enabled = false;
            Destroy(rock.GetComponent<Collider>());
            rock.GetComponent<Renderer>().sharedMaterial = debrisMaterial;
            rock.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            debris.Add(rock.transform);
        }
        for (int i = 0; i < 3; i++)
        {
            LineRenderer ring = new GameObject("Ground Shockwave").AddComponent<LineRenderer>();
            ring.transform.SetParent(effects, false);
            ring.sharedMaterial = ringMaterial;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = 48;
            ring.widthMultiplier = 0.12f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            rings.Add(ring);
        }
        if (dustMaterial != null)
        {
            dustRuntime = new Material(dustMaterial) { name = "Golem Dust Particles" };
            foreach (string texture in dustRuntime.GetTexturePropertyNames()) dustRuntime.SetTexture(texture, null);
            if (dustRuntime.HasProperty("_BaseColor")) dustRuntime.SetColor("_BaseColor", Color.white);
            if (dustRuntime.HasProperty("_Color")) dustRuntime.SetColor("_Color", Color.white);
            if (dustRuntime.HasProperty("_EmissionColor")) dustRuntime.SetColor("_EmissionColor", Color.black);
            dust = new GameObject("Ground Smash Dust").AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            dust.transform.SetParent(effects, false);
            dust.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = dust.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.8f);
            main.startColor = new Color(0.5f, 0.39f, 0.28f, 0.3f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 32) });
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 80f;
            shape.radius = 1f;
            var size = dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f));
            var color = dust.colorOverLifetime;
            color.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            ParticleSystemRenderer renderer = dust.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = dustRuntime;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        effects.gameObject.SetActive(false);
    }

    private void AnimateImpact(float t)
    {
        float radius = bodyBounds.size.y * 0.23f;
        for (int i = 0; i < debris.Count; i++)
        {
            float angle = i * 2.399963f;
            float distance = radius * (0.35f + (i % 5) * 0.13f) * t;
            debris[i].localPosition = new Vector3(Mathf.Cos(angle) * distance,
                Mathf.Sin(t * Mathf.PI) * (0.6f + i % 4 * 0.3f), Mathf.Sin(angle) * distance);
            debris[i].localRotation = Quaternion.Euler(i * 37f + t * 270f, i * 61f, t * 170f);
            debris[i].localScale = Vector3.one * ((0.12f + i % 3 * 0.06f) * (1f - t));
        }
        for (int i = 0; i < rings.Count; i++)
        {
            float wave = Mathf.Clamp01((t - i * 0.12f) / (1f - i * 0.12f));
            rings[i].enabled = wave > 0f && wave < 1f;
            rings[i].widthMultiplier = 0.16f * (1f - wave);
            for (int point = 0; point < 48; point++)
            {
                float angle = point * Mathf.PI * 2f / 48;
                rings[i].SetPosition(point, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (radius * wave));
            }
        }
    }

    private void SetColliders(bool restore)
    {
        for (int i = 0; i < colliders.Count; i++)
            if (colliders[i] != null) colliders[i].enabled = restore && colliderStates[i];
    }

    private void ShowPile()
    {
        foreach (Stone stone in stones)
        {
            stone.part.SetLocalPositionAndRotation(stone.pilePosition, stone.pileRotation);
            stone.renderer.enabled = stone.visible && !stone.decoration;
        }
    }

    private void RestorePose()
    {
        foreach (Stone stone in stones)
        {
            if (stone.part == null) continue;
            stone.part.SetLocalPositionAndRotation(stone.position, stone.rotation);
            stone.renderer.enabled = stone.visible;
        }
    }

    private void OnDisable()
    {
        GazeAudio.Stop(stompSound);
        GazeAudio.Stop(rubbleSound);
        StopAllCoroutines();
        idleTime = idleBlend = headYaw = headPitch = 0f;
        IsAnimating = false;
        if (!initialized) return;
        if (IsDefeated) SetDefeatedPose();
        else if (HasAwakened) RestorePose(); else ShowPile();
        SetColliders(!IsDefeated);
        IsDefeating = false;
        effects.gameObject.SetActive(false);
        if (lightning != null) lightning.Hide();
    }

    private void OnDestroy()
    {
        if (lightning != null) Destroy(lightning.gameObject);
        if (effects != null) Destroy(effects.gameObject);
        if (dustRuntime != null) Destroy(dustRuntime);
        if (debrisMaterial != null) Destroy(debrisMaterial);
        if (ringMaterial != null) Destroy(ringMaterial);
    }
}
