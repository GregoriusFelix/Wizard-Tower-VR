using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class GazeElementSelection : MonoBehaviour
{
    [System.Serializable]
    public class Element
    {
        public string name;
        public Transform staff;
        public Transform book;
        public Color color;
    }
    public GazeGolemAwakening golem;
    public GameObject unlockedTeleportPad;
    [Tooltip("Outside-arena destination for Leave Arena. Defaults to Teleport Pad (16).")]
    public Transform arenaExitPoint;
    public Element[] elements;
    [Min(1.5f)] public float presentationDistance = 2.6f;
    public int SelectedIndex { get; private set; } = -1;
    public bool IsChoosing => state == State.Choosing;

    private enum State { Waiting, Revealing, Choosing, Equipping, Equipped, Exploring }
    private State state;
    private GazeTeleport owner;
    private Transform viewer, gallery;
    private readonly Transform[] options = new Transform[4];
    private readonly Transform[] staffs = new Transform[4];
    private readonly GazeElementBook[] books = new GazeElementBook[4];
    private readonly Vector3[] grips = new Vector3[4];
    private readonly bool[] sourceStates = new bool[8];
    private bool initialized, heldVisible;
    private GazeElementSpell spell;
    private GazeGolemReset resetButton;
    private GazeGolemReset exitButton;
    private Vector3 selectionPosition, selectionForward;
    private bool hasSelectionPosition, resettingEncounter;
    public bool IsCasting => spell != null && spell.IsCasting;
    public Vector3 SpellOrigin => SelectedIndex >= 0 && staffs[SelectedIndex] != null
        ? staffs[SelectedIndex].TransformPoint(Vector3.up * 0.55f) : transform.position;

    private void Awake()
    {
        if (golem == null || elements == null || elements.Length != 4)
        {
            Debug.LogError("GazeElementSelection: assign the golem and four element staff/book pairs.", this);
            enabled = false;
            return;
        }
        for (int i = 0; i < 4; i++)
            if (elements[i].staff == null || elements[i].staff.Find("Cylinder")?.GetComponent<Renderer>() == null)
            {
                Debug.LogError("GazeElementSelection: each staff needs its existing Cylinder shaft.", this);
                enabled = false;
                return;
            }
        if (unlockedTeleportPad == null)
            unlockedTeleportPad = GameObject.Find("Teleport")?.transform.Find("Teleport Pad (19)")?.gameObject;
        if (unlockedTeleportPad != null) unlockedTeleportPad.SetActive(false);
        if (arenaExitPoint == null) arenaExitPoint = GameObject.Find("Teleport Pad (16)")?.transform;
        gallery = new GameObject("Choose an Element").transform;
        for (int i = 0; i < 4; i++)
        {
            Element element = elements[i];
            options[i] = new GameObject(element.name + " Choice").transform;
            options[i].SetParent(gallery, false);
            options[i].localPosition = new Vector3((i - 1.5f) * 0.62f, -0.12f, 0f);
            staffs[i] = CopyStaff(element.staff, options[i], out grips[i]);
            staffs[i].localPosition = Vector3.down * 0.12f;
            books[i] = new GameObject(element.name + " Spellbook").AddComponent<GazeElementBook>();
            books[i].transform.SetParent(options[i], false);
            books[i].transform.localPosition = new Vector3(0f, 0.74f, 0f);
            Material reference = element.staff.GetComponentInChildren<Renderer>(true).sharedMaterial;
            Material paper = null;
            if (element.book != null)
                foreach (Renderer renderer in element.book.GetComponentsInChildren<Renderer>(true))
                    if (renderer.name.StartsWith("Paper") && renderer.sharedMaterial != null) { paper = renderer.sharedMaterial; break; }
            books[i].Initialize(reference, element.color, paper);
            GazeElementChoice choice = options[i].gameObject.AddComponent<GazeElementChoice>();
            choice.selection = this;
            choice.index = i;
            choice.Initialize(reference, element.name, element.color);
            sourceStates[i * 2] = element.staff.gameObject.activeSelf;
            element.staff.gameObject.SetActive(false);
            if (element.book != null)
            {
                sourceStates[i * 2 + 1] = element.book.gameObject.activeSelf;
                element.book.gameObject.SetActive(false);
            }
        }
        gallery.gameObject.SetActive(false);
        spell = GetComponent<GazeElementSpell>();
        if (spell == null) spell = gameObject.AddComponent<GazeElementSpell>();
        spell.Initialize(this);
        resetButton = new GameObject("Floating Golem Reset").AddComponent<GazeGolemReset>();
        resetButton.Initialize(this, elements[0].staff.GetComponentInChildren<Renderer>(true).sharedMaterial);
        exitButton = new GameObject("Floating Leave Arena").AddComponent<GazeGolemReset>();
        exitButton.Initialize(this, elements[0].staff.GetComponentInChildren<Renderer>(true).sharedMaterial, true);
        initialized = true;
    }

    private Transform CopyStaff(Transform source, Transform parent, out Vector3 grip)
    {
        Renderer shaft = source.Find("Cylinder").GetComponent<Renderer>();
        Quaternion frame = Quaternion.LookRotation(source.right, source.up);
        Quaternion inverse = Quaternion.Inverse(frame);
        Bounds bounds = new Bounds();
        bool found = false;
        foreach (MeshFilter mesh in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mesh.sharedMesh == null) continue;
            Bounds local = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                Vector3 point = inverse * mesh.transform.TransformPoint(local.center + offset);
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        float factor = 1.1f / Mathf.Max(0.01f, bounds.size.y);
        Transform model = new GameObject(source.name + " Carried Model").transform;
        model.SetParent(parent, false);
        foreach (MeshFilter mesh in source.GetComponentsInChildren<MeshFilter>(true))
        {
            Renderer renderer = mesh.GetComponent<Renderer>();
            if (mesh.sharedMesh == null || renderer == null) continue;
            GameObject copy = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            copy.transform.SetParent(model, false);
            copy.transform.localPosition = (inverse * mesh.transform.position - bounds.center) * factor;
            copy.transform.localRotation = inverse * mesh.transform.rotation;
            copy.transform.localScale = mesh.transform.lossyScale * factor;
            copy.GetComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            copy.GetComponent<Renderer>().sharedMaterials = renderer.sharedMaterials;
        }
        grip = (inverse * shaft.bounds.center - bounds.center) * factor;
        return model;
    }

    private void Update()
    {
        if (!initialized) return;
        bool resetAvailable = CanResetEncounter(owner);
        if (resetAvailable && !resetButton.gameObject.activeSelf)
        {
            resetButton.Show(viewer);
            if (exitButton.gameObject.activeSelf) exitButton.Show(viewer);
        }
        else if (!resetAvailable && resetButton.gameObject.activeSelf) resetButton.Hide();
        bool exitAvailable = CanLeaveArena(owner);
        if (exitAvailable && !exitButton.gameObject.activeSelf) exitButton.Show(viewer);
        else if (!exitAvailable && exitButton.gameObject.activeSelf) exitButton.Hide();
        if (!initialized || state != State.Waiting || !golem.HasAwakened) return;
        Camera camera = Camera.main;
        GazeTeleport gaze = camera != null ? camera.GetComponent<GazeTeleport>() : null;
        if (gaze != null && gaze.CanPresentElements) StartCoroutine(Present(gaze));
    }

    private IEnumerator Present(GazeTeleport gaze, bool returning = false)
    {
        owner = gaze;
        viewer = gaze.transform;
        state = State.Revealing;
        Vector3 forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up).normalized;
        Vector3 direction = returning ? selectionForward : Quaternion.AngleAxis(90f, Vector3.up) * forward;
        if (direction.sqrMagnitude < 0.1f) direction = Vector3.right;
        Vector3 destination = viewer.position + direction * presentationDistance;
        gallery.SetPositionAndRotation(destination, Quaternion.LookRotation(direction, Vector3.up));
        gallery.localScale = Vector3.one * 0.03f;
        if (!returning) yield return gaze.FaceRightForElements();
        if (!hasSelectionPosition)
        {
            selectionPosition = viewer.position;
            selectionForward = direction;
            hasSelectionPosition = true;
        }
        gallery.gameObject.SetActive(true);
        for (float time = 0f; time < 0.9f; time += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, time / 0.9f);
            gallery.localScale = Vector3.one * Mathf.Lerp(0.03f, 1f, t);
            gallery.position = destination + Vector3.down * (0.25f * (1f - t));
            yield return null;
        }
        gallery.localScale = Vector3.one;
        gallery.position = destination;
        Physics.SyncTransforms();
        state = State.Choosing;
    }

    public bool BlocksInteractions(GazeTeleport gaze)
    {
        return isActiveAndEnabled && owner == gaze && (state == State.Revealing || state == State.Choosing || state == State.Equipping || IsCasting);
    }

    public bool CanCast(GazeTeleport gaze, GazeGolemAwakening target)
    {
        return initialized && isActiveAndEnabled && state == State.Equipped && heldVisible && owner == gaze
            && viewer != null && SelectedIndex >= 0 && !IsCasting && target != null && target == golem && target.CanBeAttacked;
    }

    public void CastAt(GazeTeleport gaze, GazeGolemAwakening target)
    {
        if (!CanCast(gaze, target)) return;
        if (System.Enum.TryParse(elements[SelectedIndex].name, true, out GolemElement element))
            spell.Cast(element, target, viewer.position);
    }

    public bool CanChoose(GazeElementChoice choice, GazeTeleport gaze)
    {
        return IsChoosing && owner == gaze && choice != null && choice.selection == this && choice.index >= 0 && choice.index < 4;
    }

    public void Choose(GazeElementChoice choice, GazeTeleport gaze)
    {
        if (CanChoose(choice, gaze)) StartCoroutine(Equip(choice.index));
    }

    private IEnumerator Equip(int index)
    {
        state = State.Equipping;
        SelectedIndex = index;
        owner.ClearHandTools();
        foreach (Transform option in options) option.GetComponent<Collider>().enabled = false;
        Transform staff = staffs[index], book = books[index].transform;
        staff.SetParent(gallery, true);
        book.SetParent(gallery, true);
        Vector3 staffStart = staff.position, bookStart = book.position;
        Quaternion staffRotation = staff.rotation, bookRotation = book.rotation;
        for (float time = 0f; time < 1.4f; time += Time.deltaTime)
        {
            if (viewer == null) { CancelPresentation(); yield break; }
            float t = Mathf.SmoothStep(0f, 1f, time / 1.4f);
            HeldPose(index);
            staff.SetPositionAndRotation(Vector3.Lerp(staffStart, staff.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.13f,
                Quaternion.Slerp(staffRotation, staff.rotation, t));
            book.SetPositionAndRotation(Vector3.Lerp(bookStart, book.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.08f,
                Quaternion.Slerp(bookRotation, book.rotation, t));
            books[index].SetOpen(Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.15f) / 0.85f)));
            foreach (Transform option in options) option.localScale = Vector3.one * (1f - t);
            yield return null;
        }
        foreach (Transform option in options) option.gameObject.SetActive(false);
        books[index].SetOpen(1f);
        heldVisible = true;
        state = State.Equipped;
        HeldPose(index);
        if (unlockedTeleportPad != null)
        {
            unlockedTeleportPad.SetActive(true);
            GazeSurfaceColliders.Repair();
            Physics.SyncTransforms();
        }
    }

    private void LateUpdate()
    {
        if (state == State.Equipped && gallery != null)
        {
            bool visible = heldVisible && viewer != null && owner != null && owner.isActiveAndEnabled;
            if (gallery.gameObject.activeSelf != visible) gallery.gameObject.SetActive(visible);
            if (visible) HeldPose(SelectedIndex);
        }
        if (state == State.Choosing)
            for (int i = 0; i < options.Length; i++)
                options[i].localPosition = new Vector3((i - 1.5f) * 0.62f, -0.12f + Mathf.Sin(Time.time * 1.4f + i) * 0.015f, 0f);
    }

    private void HeldPose(int index)
    {
        float cast = spell != null ? spell.CastPoseWeight : 0f;
        Quaternion staffRotation = viewer.rotation * Quaternion.Euler(-5f + cast * 55f, 0f, -8f);
        staffs[index].SetPositionAndRotation(viewer.position + viewer.rotation * new Vector3(0.32f, -0.36f, 0.85f)
            - staffRotation * grips[index], staffRotation);
        books[index].transform.SetPositionAndRotation(viewer.position + viewer.rotation * new Vector3(-0.32f, -0.28f + cast * 0.04f, 0.76f),
            viewer.rotation * Quaternion.Euler(22f + cast * 10f, 0f, 0f));
    }

    public void Stow(GazeTeleport gaze)
    {
        if (state != State.Equipped || owner != gaze || !heldVisible) return;
        heldVisible = false;
        staffs[SelectedIndex].gameObject.SetActive(false);
        books[SelectedIndex].gameObject.SetActive(false);
    }

    public bool CanResetEncounter(GazeTeleport gaze)
    {
        return initialized && isActiveAndEnabled && state == State.Equipped && !resettingEncounter
            && gaze != null && gaze == owner && gaze.isActiveAndEnabled && viewer != null && hasSelectionPosition
            && golem != null && golem.IsDefeated && !golem.IsDefeating && !IsCasting;
    }

    public void ResetEncounter(GazeTeleport gaze)
    {
        if (CanResetEncounter(gaze)) StartCoroutine(ReturnToChoices(gaze));
    }

    public bool CanLeaveArena(GazeTeleport gaze)
    {
        return initialized && isActiveAndEnabled && state == State.Equipped && !resettingEncounter
            && gaze != null && gaze == owner && gaze.CanPresentElements && viewer != null
            && arenaExitPoint != null && arenaExitPoint.gameObject.activeInHierarchy;
    }

    public void LeaveArena(GazeTeleport gaze)
    {
        if (CanLeaveArena(gaze)) StartCoroutine(LeaveEncounter(gaze));
    }

    private IEnumerator LeaveEncounter(GazeTeleport gaze)
    {
        resettingEncounter = true;
        state = State.Revealing;
        try
        {
            resetButton.Hide();
            exitButton.Hide();
            spell.Cancel();
            gallery.gameObject.SetActive(false);
            ResetChoices();
            Vector3 destination = arenaExitPoint.position;
            float eyeHeight = gaze.player != null && gaze.player != viewer
                ? Mathf.Max(0.5f, viewer.position.y - gaze.player.position.y) : 1.62f;
            Renderer exitSurface = arenaExitPoint.GetComponent<Renderer>();
            destination.y = (exitSurface != null ? exitSurface.bounds.max.y : destination.y) + eyeHeight;
            Vector3 facing = golem != null ? destination - golem.GroundPosition : selectionForward;
            yield return gaze.LeaveArena(destination, facing);
            state = State.Exploring;
        }
        finally { resettingEncounter = false; }
    }

    public bool CanResumeEncounter(GazeTeleport gaze, GazeGolemAwakening target)
    {
        return initialized && isActiveAndEnabled && state == State.Exploring && !resettingEncounter
            && gaze != null && gaze.CanPresentElements && target != null && target == golem
            && target.isActiveAndEnabled && target.HasAwakened && !target.IsDefeating;
    }

    public void ResumeEncounter(GazeTeleport gaze)
    {
        if (CanResumeEncounter(gaze, golem)) StartCoroutine(ReturnToChoices(gaze));
    }

    private IEnumerator ReturnToChoices(GazeTeleport gaze)
    {
        resettingEncounter = true;
        state = State.Revealing;
        try
        {
            resetButton.Hide();
            exitButton.Hide();
            spell.Cancel();
            gallery.gameObject.SetActive(false);
            ResetChoices();
            if (unlockedTeleportPad != null) unlockedTeleportPad.SetActive(false);
            yield return gaze.ReturnToStaffSelection(selectionPosition, selectionForward, golem);
            yield return Present(gaze, true);
        }
        finally { resettingEncounter = false; }
    }

    private void CancelPresentation()
    {
        if (gallery != null) gallery.gameObject.SetActive(false);
        if (state == State.Equipped || state == State.Exploring) return;
        if (owner != null) owner.SetViewFade(0f);
        ResetChoices();
        state = State.Waiting;
        owner = null;
        viewer = null;
    }

    private void ResetChoices()
    {
        heldVisible = false;
        SelectedIndex = -1;
        for (int i = 0; i < 4; i++)
        {
            staffs[i].SetParent(options[i], false);
            staffs[i].localPosition = Vector3.down * 0.12f;
            staffs[i].localRotation = Quaternion.identity;
            staffs[i].localScale = Vector3.one;
            staffs[i].gameObject.SetActive(true);
            books[i].transform.SetParent(options[i], false);
            books[i].transform.localPosition = new Vector3(0f, 0.74f, 0f);
            books[i].transform.localRotation = Quaternion.identity;
            books[i].transform.localScale = Vector3.one;
            books[i].gameObject.SetActive(true);
            books[i].SetOpen(0f);
            options[i].localScale = Vector3.one;
            options[i].gameObject.SetActive(true);
            options[i].GetComponent<Collider>().enabled = true;
        }
    }

    public void CancelForViewer(GazeTeleport gaze)
    {
        if (owner != gaze) return;
        if (resettingEncounter && golem != null) golem.ResetToStanding();
        if (spell != null) spell.Cancel();
        StopAllCoroutines();
        CancelPresentation();
    }

    private void OnDisable()
    {
        if (resetButton != null) resetButton.Hide();
        if (exitButton != null) exitButton.Hide();
        if (resettingEncounter && golem != null) golem.ResetToStanding();
        if (spell != null) spell.Cancel();
        StopAllCoroutines();
        if (initialized) CancelPresentation();
    }

    private void OnDestroy()
    {
        if (resetButton != null) Destroy(resetButton.gameObject);
        if (exitButton != null) Destroy(exitButton.gameObject);
        if (gallery != null) Destroy(gallery.gameObject);
        if (!initialized) return;
        for (int i = 0; i < 4; i++)
        {
            if (elements[i].staff != null) elements[i].staff.gameObject.SetActive(sourceStates[i * 2]);
            if (elements[i].book != null) elements[i].book.gameObject.SetActive(sourceStates[i * 2 + 1]);
        }
    }
}
