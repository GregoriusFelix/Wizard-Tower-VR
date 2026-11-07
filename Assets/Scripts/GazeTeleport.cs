using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class GazeTeleport : MonoBehaviour
{
    [Tooltip("The camera itself for a standalone camera, or the root of the rig containing this camera.")]
    public Transform player;

    [Min(0f)] public float gazeDuration = 3f;
    [Min(0.01f)] public float maxDistance = 30f;
    [Tooltip("Separate range for the eyeball above the tower.")]
    [Min(0.01f)] public float eyeballMaxDistance = 100f;
    [Min(0.01f)] public float statueMaxDistance = 60f;
    [Min(0.01f)] public float spellMaxDistance = 65f;

    private float gazeTimer;
    private Transform currentTarget;
    private GazeElementChoice focusedElementChoice;
    private bool waitForLookAway;
    private GazeTeleportReticle reticle;
    private bool isTransitioning;
    private bool surfaceCollidersPrepared;
    private GazeChair activeChair;
    private const int LogCapacity = 3;
    private readonly List<GazeFuelLog> carriedLogs = new List<GazeFuelLog>();
    public int CarriedLogCount => carriedLogs.Count;
    private GazeAxe equippedAxe;
    private GazeCarryTorch carriedTorch;
    private AudioSource teleportSound;
    public GazeElementSelection elementSelection;
    private GazeGolemAwakening sceneGolem, focusedGolem;
    public bool CanPresentElements => isActiveAndEnabled && !isTransitioning && (carriedTorch == null || !carriedTorch.IsLighting);

    private void Start()
    {
        if (player == null || (player != transform && !transform.IsChildOf(player)))
        {
            Debug.LogError("GazeTeleport: assign this camera or its player rig parent to Player.", this);
            enabled = false;
            return;
        }

        reticle = GazeTeleportReticle.Create(GetComponent<Camera>());
        if (elementSelection == null) elementSelection = FindAnyObjectByType<GazeElementSelection>();
        sceneGolem = elementSelection != null ? elementSelection.golem : FindAnyObjectByType<GazeGolemAwakening>();
        PrepareSurfaceColliders();
    }

    private void Update()
    {
        if (carriedLogs.RemoveAll(log => log == null || !log.IsCarried) > 0)
            for (int i = 0; i < carriedLogs.Count; i++) carriedLogs[i].SetCarrySlot(i);
        if (!surfaceCollidersPrepared)
            PrepareSurfaceColliders();

        GazeGolemAwakening introduction = elementSelection != null ? elementSelection.golem : sceneGolem;
        if (introduction != null && introduction.IsIntroducing)
        {
            ResetGaze();
            if (!isTransitioning && focusedGolem != introduction)
            {
                focusedGolem = introduction;
                Vector3 toward = Vector3.ProjectOnPlane(introduction.GroundPosition - transform.position, Vector3.up);
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
                float turn = toward.sqrMagnitude > 0.001f ? Vector3.SignedAngle(forward, toward, Vector3.up) : 0f;
                StartCoroutine(TurnForSequence(turn));
            }
            return;
        }

        if (activeChair != null)
        {
            if (activeChair.IsAnimating) return;
            if (!activeChair.IsSeated) activeChair = null;
        }

        if (isTransitioning)
            return;
        if (carriedTorch != null && carriedTorch.IsLighting)
        {
            ResetGaze();
            return;
        }

        Ray ray = new Ray(transform.position, transform.forward);
        if (elementSelection != null && elementSelection.BlocksInteractions(this))
        {
            UpdateElementChoice(ray);
            return;
        }
        float rayDistance = Mathf.Max(spellMaxDistance, Mathf.Max(maxDistance, Mathf.Max(eyeballMaxDistance, statueMaxDistance)));
        Debug.DrawRay(ray.origin, ray.direction * rayDistance, Color.red);

        Transform target = null;
        GazeDoor targetDoor = null;
        GazeEyeball targetEye = null;
        GazeStatue targetStatue = null;
        GazeCauldron targetCauldron = null;
        GazeStreetLamp targetLamp = null;
        GazePortal targetPortal = null;
        GazeLectern targetLectern = null;
        GazeChair targetChair = null;
        GazeWritingBook targetWriting = null;
        GazeMagicGlobe targetGlobe = null;
        GazePumpkin targetPumpkin = null;
        GazeAxe targetAxe = null;
        GazeChopTarget targetChop = null;
        GazeFuelLog targetLog = null;
        GazeCauldronFuel targetFuel = null;
        GazeBrewingTarget targetBrewing = null;
        GazePotionDispenser targetDispenser = null;
        GazeTorchTarget targetTorch = null;
        GazeTomb targetTomb = null;
        GazeGolemAwakening targetGolem = null;
        GazeGolemReset targetReset = null;
        GazeChest targetChest = null;
        GazeOrrery targetOrrery = null;
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            GazeGolemReset reset = hit.collider.GetComponentInParent<GazeGolemReset>();
            if (hit.distance <= maxDistance && reset != null && reset.CanGaze(this))
            {
                target = reset.transform;
                targetReset = reset;
            }
            if (hit.distance <= maxDistance)
            {
                // Skip an incomplete component on the mesh and keep checking its portal group.
                for (Transform candidate = hit.collider.transform; candidate != null; candidate = candidate.parent)
                {
                    if (target == null && candidate.TryGetComponent(out GazePortal portal) && portal.CanGazeAt(hit.collider.transform))
                    {
                        target = portal.transform;
                        targetPortal = portal;
                        break;
                    }
                }
            }

            GazeEyeball eye = hit.collider.GetComponentInParent<GazeEyeball>();
            if (target == null && eye != null && eye.isActiveAndEnabled && hit.distance <= eyeballMaxDistance)
            {
                target = eye.transform;
                targetEye = eye;
            }

            GazeDoor door = hit.collider.GetComponentInParent<GazeDoor>();
            GazeChest chest = hit.collider.GetComponentInParent<GazeChest>();
            if (target == null && hit.distance <= maxDistance && chest != null && chest.CanGaze)
            {
                target = chest.transform;
                targetChest = chest;
            }
            GazeGolemAwakening golem = hit.collider.GetComponentInParent<GazeGolemAwakening>();
            if (target == null && hit.distance <= spellMaxDistance && elementSelection != null &&
                (elementSelection.CanCast(this, golem) || elementSelection.CanResumeEncounter(this, golem)))
            {
                target = golem.transform;
                targetGolem = golem;
            }
            GazeTomb tomb = hit.collider.GetComponentInParent<GazeTomb>();
            if (target == null && hit.distance <= maxDistance && tomb != null && tomb.CanLight(carriedTorch))
            {
                target = tomb.transform;
                targetTomb = tomb;
            }
            GazeTorchTarget torch = hit.collider.GetComponentInParent<GazeTorchTarget>();
            if (target == null && hit.distance <= maxDistance && torch != null
                && (torch.portable != null ? torch.portable.CanPickUp
                    : carriedTorch != null && carriedTorch.CanLightFrom(torch.source, torch.sourceIndex)))
            {
                target = torch.GazeRoot;
                targetTorch = torch;
            }
            GazePotionDispenserTarget dispenser = hit.collider.GetComponentInParent<GazePotionDispenserTarget>();
            if (target == null && hit.distance <= maxDistance && dispenser != null && dispenser.dispenser != null
                && dispenser.dispenser.CanDispense)
            {
                target = dispenser.dispenser.transform;
                targetDispenser = dispenser.dispenser;
            }
            GazeBrewingTarget brewing = hit.collider.GetComponentInParent<GazeBrewingTarget>();
            if (target == null && hit.distance <= maxDistance && brewing != null && brewing.table != null
                && brewing.table.CanGazeAt(brewing.ingredientIndex))
            {
                target = brewing.GazeRoot;
                targetBrewing = brewing;
            }
            GazeChopTarget chop = hit.collider.GetComponentInParent<GazeChopTarget>();
            if (target == null && chop != null && chop.owner != null && chop.owner.CanHit(chop))
            {
                target = chop.transform;
                targetChop = chop;
            }
            GazeAxe axe = hit.collider.GetComponentInParent<GazeAxe>();
            if (target == null && hit.distance <= maxDistance && axe != null && axe.isActiveAndEnabled)
            {
                target = axe.transform;
                targetAxe = axe;
            }
            GazeFuelLog log = hit.collider.GetComponentInParent<GazeFuelLog>();
            if (target == null && hit.distance <= maxDistance && carriedLogs.Count < LogCapacity && log != null && log.CanPickUp)
            {
                target = log.transform;
                targetLog = log;
            }
            GazeCauldronFuel fuel = hit.collider.GetComponentInParent<GazeCauldronFuel>();
            if (target == null && hit.distance <= maxDistance && carriedLogs.Count > 0 && fuel != null
                && fuel.CanGazeAt(hit.collider.transform, hit.point))
            {
                target = fuel.transform;
                targetFuel = fuel;
            }
            GazePumpkin pumpkin = hit.collider.GetComponentInParent<GazePumpkin>();
            if (target == null && hit.distance <= maxDistance && pumpkin != null && pumpkin.isActiveAndEnabled)
            {
                target = pumpkin.transform;
                targetPumpkin = pumpkin;
            }
            GazeWritingBook writing = hit.collider.GetComponentInParent<GazeWritingBook>();
            if (target == null && hit.distance <= maxDistance && writing != null && writing.isActiveAndEnabled)
            {
                target = writing.transform;
                targetWriting = writing;
            }
            GazeMagicGlobe globe = hit.collider.GetComponentInParent<GazeMagicGlobe>();
            GazeOrrery orrery = hit.collider.GetComponentInParent<GazeOrrery>();
            if (target == null && hit.distance <= maxDistance && orrery != null && orrery.CanGaze)
            {
                target = orrery.transform;
                targetOrrery = orrery;
            }
            if (target == null && hit.distance <= maxDistance && globe != null && globe.isActiveAndEnabled)
            {
                target = globe.transform;
                targetGlobe = globe;
            }
            GazeChair chair = hit.collider.GetComponentInParent<GazeChair>();
            if (target == null && hit.distance <= maxDistance && chair != null && chair.CanGazeAt(hit.collider.transform))
            {
                target = chair.transform;
                targetChair = chair;
            }
            GazeLectern lectern = hit.collider.GetComponentInParent<GazeLectern>();
            if (target == null && hit.distance <= maxDistance && lectern != null && lectern.isActiveAndEnabled)
            {
                target = lectern.transform;
                targetLectern = lectern;
            }
            GazeStreetLamp lamp = hit.collider.GetComponentInParent<GazeStreetLamp>();
            if (target == null && hit.distance <= maxDistance && lamp != null && lamp.isActiveAndEnabled)
            {
                target = lamp.transform;
                targetLamp = lamp;
            }

            GazeCauldron cauldron = hit.collider.GetComponentInParent<GazeCauldron>();
            if (target == null && hit.distance <= maxDistance && cauldron != null && cauldron.CanGazeAt(hit.collider.transform))
            {
                target = cauldron.spoon;
                targetCauldron = cauldron;
            }

            GazeStatue statue = hit.collider.GetComponentInParent<GazeStatue>();
            if (target == null && statue != null && statue.isActiveAndEnabled && hit.distance <= statueMaxDistance)
            {
                target = statue.transform;
                targetStatue = statue;
            }

            if (target == null && hit.distance <= maxDistance && door != null && door.CanGazeAt(hit.collider.transform))
            {
                target = door.transform;
                targetDoor = door;
            }

            // Support a collider on the pad itself or one of its children.
            for (Transform candidate = hit.collider.transform; candidate != null; candidate = candidate.parent)
            {
                if (target == null && hit.distance <= maxDistance && candidate.CompareTag("Teleport"))
                {
                    target = candidate;
                    break;
                }
            }
        }

        if (target == null)
        {
            ResetGaze();
            return;
        }

        if (waitForLookAway)
        {
            if (currentTarget == target)
                return;

            // A different target counts as looking away from the completed interaction.
            waitForLookAway = false;
            gazeTimer = 0f;
        }

        if ((targetChest != null && targetChest.IsAnimating) ||
            (targetOrrery != null && targetOrrery.IsSpinning) ||
            (targetStatue != null && targetStatue.IsGreeting) ||
            (targetAxe != null && targetAxe.IsChopping) ||
            (targetPumpkin != null && targetPumpkin.IsHaunting) ||
            (targetWriting != null && targetWriting.IsWriting) ||
            (targetGlobe != null && targetGlobe.IsAnimating) ||
            (targetLectern != null && targetLectern.IsSummoning) ||
            (targetDoor != null && targetDoor.IsAnimating) ||
            (targetCauldron != null && targetCauldron.IsStirring) ||
            (targetLamp != null && targetLamp.IsSwinging))
        {
            gazeTimer = 0f;
            currentTarget = target;
            reticle.SetProgress(false, 0f, 0f);
            return;
        }

        if (currentTarget != target)
        {
            currentTarget = target;
            gazeTimer = 0f;
        }

        float holdDuration = targetChop != null ? targetChop.owner.chopGazeDuration : gazeDuration;
        gazeTimer += Time.deltaTime;
        reticle.SetProgress(true, holdDuration <= 0f ? 1f : gazeTimer / holdDuration,
            Mathf.Max(0f, holdDuration - gazeTimer));
        if (gazeTimer < holdDuration)
            return;

        if (targetChest != null)
        {
            targetChest.Toggle();
        }
        else if (targetOrrery != null)
        {
            targetOrrery.Activate();
        }
        else if (targetReset != null)
        {
            targetReset.Activate(this);
        }
        else if (targetGolem != null)
        {
            if (elementSelection.CanResumeEncounter(this, targetGolem)) elementSelection.ResumeEncounter(this);
            else elementSelection.CastAt(this, targetGolem);
        }
        else if (targetTomb != null)
        {
            carriedTorch.LightCore(targetTomb);
        }
        else if (targetTorch != null)
        {
            if (targetTorch.portable != null)
            {
                if (elementSelection != null) elementSelection.Stow(this);
                if (carriedTorch != null) carriedTorch.PutBack();
                if (equippedAxe != null) equippedAxe.Unequip();
                equippedAxe = null;
                DropCarriedLogs();
                carriedTorch = targetTorch.portable.PickUp(transform) ? targetTorch.portable : null;
            }
            else if (carriedTorch != null) carriedTorch.LightFrom(targetTorch.source, targetTorch.sourceIndex);
        }
        else if (targetDispenser != null)
        {
            targetDispenser.Dispense(transform);
        }
        else if (targetBrewing != null)
        {
            targetBrewing.table.Activate(targetBrewing.ingredientIndex, transform);
        }
        else if (targetChop != null)
        {
            targetChop.owner.Hit(targetChop);
        }
        else if (targetAxe != null)
        {
            if (elementSelection != null) elementSelection.Stow(this);
            if (carriedTorch != null) carriedTorch.PutBack();
            carriedTorch = null;
            DropCarriedLogs();
            if (equippedAxe != null) equippedAxe.Unequip();
            equippedAxe = targetAxe;
            targetAxe.Chop(transform);
        }
        else if (targetLog != null)
        {
            if (elementSelection != null) elementSelection.Stow(this);
            if (carriedTorch != null) carriedTorch.PutBack();
            carriedTorch = null;
            if (equippedAxe != null) equippedAxe.Unequip();
            equippedAxe = null;
            if (carriedLogs.Count < LogCapacity && targetLog.PickUp(transform, carriedLogs.Count))
                carriedLogs.Add(targetLog);
        }
        else if (targetFuel != null)
        {
            for (int i = carriedLogs.Count - 1; i >= 0; i--)
                if (targetFuel.AddFuel(carriedLogs[i])) carriedLogs.RemoveAt(i);
            for (int i = 0; i < carriedLogs.Count; i++) carriedLogs[i].SetCarrySlot(i);
        }
        else if (targetPumpkin != null)
        {
            targetPumpkin.Haunt(transform);
        }
        else if (targetWriting != null)
        {
            targetWriting.Write();
        }
        else if (targetGlobe != null)
        {
            targetGlobe.Activate();
        }
        else if (targetChair != null)
        {
            if (activeChair != null && activeChair != targetChair) activeChair.ReleaseForTravel();
            activeChair = targetChair;
            targetChair.Interact(this, GetComponent<Camera>(), player);
        }
        else if (targetLectern != null)
        {
            targetLectern.Summon(transform);
        }
        else if (targetPortal != null)
        {
            StartCoroutine(TransitionPortal(targetPortal));
        }
        else if (targetLamp != null)
        {
            targetLamp.Swing();
        }
        else if (targetCauldron != null)
        {
            targetCauldron.StartStirring();
        }
        else if (targetStatue != null)
        {
            targetStatue.Greet(transform);
        }
        else if (targetEye != null)
        {
            targetEye.ToggleTracking(transform);
        }
        else if (targetDoor != null)
        {
            targetDoor.Toggle();
        }
        else
        {
            TeleportTo(target.position);
        }

        gazeTimer = 0f;
        waitForLookAway = true;
        reticle.SetProgress(false, 0f, 0f);
    }

    private void UpdateElementChoice(Ray ray)
    {
        GazeElementChoice choice = null;
        GazeGolemReset arenaAction = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            choice = hit.collider.GetComponentInParent<GazeElementChoice>();
            arenaAction = hit.collider.GetComponentInParent<GazeGolemReset>();
        }
        if (arenaAction != null && arenaAction.CanGaze(this))
        {
            if (focusedElementChoice != null) focusedElementChoice.SetGazeFeedback(false, 0f);
            focusedElementChoice = null;
            if (currentTarget != arenaAction.transform) { currentTarget = arenaAction.transform; gazeTimer = 0f; }
            gazeTimer += Time.deltaTime;
            reticle.SetProgress(true, gazeDuration <= 0f ? 1f : gazeTimer / gazeDuration,
                Mathf.Max(0f, gazeDuration - gazeTimer));
            if (gazeTimer >= gazeDuration) { arenaAction.Activate(this); ResetGaze(); }
            return;
        }
        if (!elementSelection.CanChoose(choice, this)) { ResetGaze(); return; }
        if (currentTarget != choice.transform)
        {
            if (focusedElementChoice != null) focusedElementChoice.SetGazeFeedback(false, 0f);
            focusedElementChoice = choice;
            currentTarget = choice.transform;
            gazeTimer = 0f;
        }
        gazeTimer += Time.deltaTime;
        choice.SetGazeFeedback(true, gazeDuration <= 0f ? 1f : gazeTimer / gazeDuration);
        reticle.SetProgress(true, gazeDuration <= 0f ? 1f : gazeTimer / gazeDuration, Mathf.Max(0f, gazeDuration - gazeTimer));
        if (gazeTimer < gazeDuration) return;
        elementSelection.Choose(choice, this);
        ResetGaze();
    }

    public void ClearHandTools()
    {
        if (carriedTorch != null) carriedTorch.PutBack();
        carriedTorch = null;
        if (equippedAxe != null) equippedAxe.Unequip();
        equippedAxe = null;
        DropCarriedLogs();
    }

    public bool FinishTombLighting(GazeCarryTorch torch, GazeTomb tomb)
    {
        if (torch == null || carriedTorch != torch) return false;
        GazeGolemAwakening golem = elementSelection != null ? elementSelection.golem : FindAnyObjectByType<GazeGolemAwakening>();
        if (golem == null || !golem.BothCoresLit || System.Array.IndexOf(golem.tombs, tomb) < 0) return false;
        carriedTorch = null;
        torch.Disappear();
        return true;
    }

    public IEnumerator FaceRightForElements()
    {
        return TurnForSequence(90f);
    }

    private IEnumerator TurnForSequence(float angle)
    {
        isTransitioning = true;
        ReleaseChairForTravel();
        try
        {
            yield return FadePortal(0f, 1f, 0.22f);
            EnsurePlayerOrigin();
            MovePlayerBy(Vector3.zero, angle);
            Physics.SyncTransforms();
            yield return FadePortal(1f, 0f, 0.22f);
        }
        finally
        {
            isTransitioning = false;
            ResetGaze();
            if (reticle != null) reticle.SetFade(0f);
        }
    }

    public IEnumerator ReturnToStaffSelection(Vector3 eyePosition, Vector3 facing, GazeGolemAwakening golem)
    {
        return RepositionWithFade(eyePosition, facing, golem);
    }

    public IEnumerator LeaveArena(Vector3 eyePosition, Vector3 facing)
    {
        return RepositionWithFade(eyePosition, facing, null);
    }

    private IEnumerator RepositionWithFade(Vector3 eyePosition, Vector3 facing, GazeGolemAwakening golem)
    {
        isTransitioning = true;
        ReleaseChairForTravel();
        try
        {
            yield return FadePortal(0f, 1f, 0.25f);
            ClearHandTools();
            EnsurePlayerOrigin();
            if (golem != null) golem.ResetToStanding();
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            float angle = Vector3.SignedAngle(forward, Vector3.ProjectOnPlane(facing, Vector3.up), Vector3.up);
            MovePlayerBy(eyePosition - transform.position, angle);
            Physics.SyncTransforms();
            yield return FadePortal(1f, 0f, 0.25f);
        }
        finally
        {
            isTransitioning = false;
            ResetGaze();
            if (reticle != null) reticle.SetFade(0f);
        }
    }

    private void EnsurePlayerOrigin()
    {
        if (player != transform) return;
        // Insert an origin without changing the headset's local tracking pose.
        Transform origin = new GameObject("Player VR Yaw Origin").transform;
        origin.SetParent(transform.parent, false);
        transform.SetParent(origin, false);
        player = origin;
    }

    private void TeleportTo(Vector3 destination)
    {
        ReleaseChairForTravel();
        Vector3 offset = destination - transform.position;
        // These pads move the viewer horizontally, preserving their current eye height.
        offset.y = 0f;
        MovePlayerBy(offset);
        InteractionAudio audio = InteractionAudio.Current;
        GazeAudio.Play(ref teleportSound, transform, audio != null ? audio.teleport : null, 0.85f, false, 0f);
    }

    private void PrepareSurfaceColliders()
    {
        GazeSurfaceColliders.Repair();
        surfaceCollidersPrepared = true;
    }

    public void SetViewFade(float alpha)
    {
        if (reticle != null) reticle.SetFade(alpha);
    }

    private void ReleaseChairForTravel()
    {
        if (activeChair != null) activeChair.ReleaseForTravel();
        activeChair = null;
    }

    private void MovePlayerBy(Vector3 offset, float turnAngle = 0f)
    {

        CharacterController controller = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        try
        {
            player.position += offset;
            if (!Mathf.Approximately(turnAngle, 0f))
                player.RotateAround(transform.position, Vector3.up, turnAngle);
            if (equippedAxe != null) equippedAxe.RefreshAfterTravel();
            if (carriedTorch != null) carriedTorch.RefreshAfterTravel();
        }
        finally
        {
            if (controllerWasEnabled)
                controller.enabled = true;
        }
    }

    private IEnumerator TransitionPortal(GazePortal portal)
    {
        if (!portal.TryGetArrivalFeet(out Vector3 feet))
            yield break;

        isTransitioning = true;
        Transform lockedPortal = portal.transform;
        float eyeHeight = player != transform ? transform.position.y - player.position.y : 0f;
        if (eyeHeight < 0.1f) eyeHeight = portal.standaloneEyeHeight;
        float duration = Mathf.Max(0.05f, portal.fadeDuration);
        portal.DeparturePulse();
        reticle.SetProgress(false, 0f, 0f);
        try
        {
            yield return FadePortal(0f, 1f, duration);
            if (portal == null || !portal.TryGetArrivalFeet(out feet))
                yield break;

            // Move in all three axes: portals can connect different floors.
            ReleaseChairForTravel();
            Vector3 destination = feet + Vector3.up * eyeHeight;
            MovePlayerBy(destination - transform.position, 180f);
            lockedPortal = portal.destinationPortal;
            Physics.SyncTransforms();
            portal.ArrivalPulse();
            InteractionAudio audio = InteractionAudio.Current;
            GazeAudio.Play(ref teleportSound, transform, audio != null ? audio.portal : null, 0.85f, false, 0f);
            yield return null;
            yield return FadePortal(1f, 0f, duration);
        }
        finally
        {
            if (reticle != null) reticle.SetFade(0f);
            isTransitioning = false;
            gazeTimer = 0f;
            currentTarget = lockedPortal;
            waitForLookAway = true;
        }
    }

    private IEnumerator FadePortal(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
            reticle.SetFade(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
            yield return null;
        }
    }

    private void OnEnable()
    {
        surfaceCollidersPrepared = false;
        if (reticle != null)
            reticle.Root.SetActive(true);
    }

    private void OnDisable()
    {
        GazeAudio.Stop(teleportSound);
        focusedGolem = null;
        if (elementSelection != null) elementSelection.CancelForViewer(this);
        StopAllCoroutines();
        if (carriedTorch != null) carriedTorch.PutBack();
        carriedTorch = null;
        if (equippedAxe != null) equippedAxe.Unequip();
        equippedAxe = null;
        DropCarriedLogs();
        if (activeChair != null) activeChair.CancelInteraction();
        activeChair = null;
        isTransitioning = false;
        ResetGaze();
        if (reticle != null)
        {
            reticle.SetFade(0f);
            reticle.Root.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (reticle != null)
            Destroy(reticle.Root);
    }

    private void DropCarriedLogs()
    {
        foreach (GazeFuelLog log in carriedLogs) if (log != null) log.Drop();
        carriedLogs.Clear();
    }

    private void ResetGaze()
    {
        if (focusedElementChoice != null) focusedElementChoice.SetGazeFeedback(false, 0f);
        focusedElementChoice = null;
        gazeTimer = 0f;
        currentTarget = null;
        waitForLookAway = false;
        if (reticle != null)
            reticle.SetProgress(false, 0f, 0f);
    }
}
