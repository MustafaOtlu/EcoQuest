using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// GDD §3 isometric controls: WASD moves relative to the camera, right click walks along a NavMesh route,
// the mouse aims the selected tool, Esc pauses. The old first-person camera object is kept only as the
// weapon mount, so the existing tool models and effects keep working.
[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float runSpeed = 7f;
    public float gravity = -9.81f;
    public float turnSpeed = 720f;
    [Header("Legacy camera objects (weapon mount)")]
    public GameObject thirdPersonCamera;
    public GameObject firstPersonCamera;
    public Animator characterAnimator;

    public static bool Paused => EcoUIFlow.Paused;
    public bool HasWalkOrder => hasPath || direct;

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private CharacterController controller;
    private Vector3 lastGrounded;
    private bool hasGrounded;
    private PlayerVitals vitals;
    private PlayerWeaponSystem weapons;
    private EcoBuildController builder;
    private InputAction moveAction, sprintAction;
    private float verticalSpeed;
    private EcoIsoCamera isoCamera;

    private NavMeshPath path;
    private bool hasPath, direct;
    private int corner;
    private Vector3 directTarget;
    private GameObject marker;
    private float markerUntil;

    private void Awake()
    {
        // The scene stores older, slower values; the compact 200 m map needs these floors.
        path = new NavMeshPath();
        walkSpeed = Mathf.Max(walkSpeed, 4.5f);
        runSpeed = Mathf.Max(runSpeed, 7f);
        BuildIsometricCamera();
    }

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        vitals = GetComponent<PlayerVitals>();
        var input = GetComponent<PlayerInput>();
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        input.SwitchCurrentActionMap("Player");
        moveAction = input.actions.FindAction("Player/Move", true);
        sprintAction = input.actions.FindAction("Player/Sprint", true);
        EcoKeys.AttachInput(moveAction, sprintAction);
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null) characterAnimator.applyRootMotion = false;
        weapons = GetComponentInChildren<PlayerWeaponSystem>(true);
        builder = GetComponent<EcoBuildController>();
        if (thirdPersonCamera != null) thirdPersonCamera.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void BuildIsometricCamera()
    {
        Camera legacy = firstPersonCamera != null ? firstPersonCamera.GetComponent<Camera>() : null;
        // Any other on-screen camera tagged MainCamera is a leftover (e.g. the scene's first-person "MainCam"):
        // it would render the whole frame a second time and make Camera.main ambiguous.
        foreach (var other in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (other == legacy || other.targetTexture != null || !other.CompareTag("MainCamera")) continue;
            if (legacy == null) legacy = other;
            else { other.enabled = false; other.gameObject.tag = "Untagged"; }
        }
        var go = new GameObject("EcoIso Camera", typeof(Camera), typeof(AudioListener), typeof(EcoIsoCamera));
        go.tag = "MainCamera";
        foreach (var other in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (other.gameObject != go) other.enabled = false; // exactly one listener
        var cam = go.GetComponent<Camera>();
        if (legacy != null) cam.CopyFrom(legacy);
        cam.orthographic = true; cam.orthographicSize = 10f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 450f;
        cam.enabled = true;
        var data = cam.GetUniversalAdditionalCameraData();
        if (legacy != null)
        {
            var old = legacy.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = old.renderPostProcessing;
            data.antialiasing = old.antialiasing;
            data.renderShadows = old.renderShadows;
            legacy.enabled = false;
            var listener = legacy.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            legacy.gameObject.tag = "Untagged";
        }
        isoCamera = go.GetComponent<EcoIsoCamera>();
        isoCamera.target = transform;
        // The mount keeps the weapon holder in front of the chest instead of in front of a first-person eye.
        if (firstPersonCamera != null) firstPersonCamera.transform.localPosition = new Vector3(0.28f, 1.15f, 0.4f);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (builder == null) builder = GetComponent<EcoBuildController>();
        bool buildBusy = builder != null && builder.IsBuilding;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !EcoUIFlow.EscapeConsumed && !EcoMarketUI.IsOpen && !EcoUIFlow.IsOpen && !buildBusy)
            SetPaused(!Paused);

        bool hasControl = !EcoUIFlow.InputBlocked && !EcoMarketUI.BlocksGameplay && !buildBusy && Application.isFocused
            && (vitals == null || !vitals.IsRecovering);

        Vector2 stick = hasControl && moveAction != null ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        Vector3 desired = Vector3.zero;
        if (isoCamera != null && stick.sqrMagnitude > 0.01f)
        {
            desired = isoCamera.FlatForward * stick.y + isoCamera.FlatRight * stick.x;
            CancelWalkOrder(); // WASD overrides any walk order (GDD §3)
        }
        else if (hasControl)
        {
            if (mouse != null && mouse.rightButton.wasPressedThisFrame && !buildBusy && !EcoIsoCamera.PointerOverUI())
                OrderWalk();
            desired = FollowWalkOrder();
        }

        float speed = sprintAction != null && sprintAction.IsPressed() ? runSpeed : walkSpeed;
        if (vitals != null) speed *= vitals.MovementMultiplier;
        Vector3 movement = KeepOutOfWater(desired * speed);

        // Facing: toward the cursor while a tool is held, otherwise toward the walking direction.
        Vector3 aimPoint = default;
        bool aiming = hasControl && !buildBusy && mouse != null && EcoKeys.FireHeld(mouse) && !EcoIsoCamera.PointerOverUI()
            && EcoIsoCamera.TryCursorPoint(transform, out aimPoint);
        Vector3 face = aiming ? Vector3.ProjectOnPlane(aimPoint - transform.position, Vector3.up) : desired;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face.normalized), turnSpeed * Time.deltaTime);

        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        controller.Move((movement + Vector3.up * verticalSpeed) * Time.deltaTime);
        // Safety net: never fall out of the world through a seam; return to the last grounded spot.
        if (controller.isGrounded) { lastGrounded = transform.position; hasGrounded = true; }
        else if (hasGrounded && transform.position.y < lastGrounded.y - 40f) Teleport(lastGrounded + Vector3.up * 0.5f);

        UpdateWeaponMount();
        UpdateMarker();

        if (characterAnimator != null)
        {
            Vector3 planar = controller.velocity; planar.y = 0f;
            float actual = planar.magnitude;
            float blend = actual <= walkSpeed
                ? Mathf.InverseLerp(0f, walkSpeed, actual) * 0.5f
                : 0.5f + Mathf.InverseLerp(walkSpeed, runSpeed, actual) * 0.5f;
            characterAnimator.SetFloat(SpeedParameter, blend, 0.1f, Time.deltaTime);
        }
    }

    // ------------------------------------------------------------------ walk orders

    private void OrderWalk()
    {
        if (!EcoIsoCamera.TryCursorHit(transform, out var hit))
        { Notify("Oraya yürünemez."); return; }
        // Clicking an object (enemy, building, tree) walks next to it instead of rejecting its steep side.
        Vector3 target = hit.point; float snap = 2.5f;
        if (!EcoGround.IsGround(hit.collider)) { var b = hit.collider.bounds; target = new Vector3(b.center.x, b.min.y, b.center.z); snap = 4f; }
        else if (hit.normal.y < 0.5f) { Notify("Oraya ulaşılamıyor."); return; }
        var navigation = EcoNavigation.Current;
        if (navigation != null && navigation.Ready)
        {
            if (!NavMesh.SamplePosition(target, out var goal, snap, NavMesh.AllAreas)
                || !NavMesh.SamplePosition(transform.position, out var start, 3f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(start.position, goal.position, NavMesh.AllAreas, path)
                || path.status != NavMeshPathStatus.PathComplete || path.corners.Length < 1)
            { Notify("Oraya ulaşılamıyor."); return; }
            hasPath = true; direct = false; corner = path.corners.Length > 1 ? 1 : 0;
            ShowMarker(goal.position);
        }
        else
        {
            CancelWalkOrder(); Notify("Yürüme rotası hazırlanıyor. Şimdilik WASD ile hareket et.");
        }
    }

    private Vector3 FollowWalkOrder()
    {
        if (hasPath)
        {
            var corners = path.corners;
            while (corner < corners.Length)
            {
                Vector3 to = corners[corner] - transform.position; to.y = 0f;
                if (to.magnitude > 0.45f) return to.normalized;
                corner++;
            }
            hasPath = false;
        }
        else if (direct)
        {
            Vector3 to = directTarget - transform.position; to.y = 0f;
            if (to.magnitude > 0.5f) return to.normalized;
            direct = false;
        }
        return Vector3.zero;
    }

    // Lakes and ponds have a solid surface collider (aiming, filling and fishing need it); the player stops at the shore
    // and slides along it instead of walking onto the water.
    private WeaponWaterSource[] waters;
    private float nextWaterScan;
    private Vector3 KeepOutOfWater(Vector3 movement)
    {
        if (movement.sqrMagnitude < 0.0001f) return movement;
        if (waters == null || Time.unscaledTime >= nextWaterScan) { waters = FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None); nextWaterScan = Time.unscaledTime + 5f; }
        Vector3 next = transform.position + movement * Time.deltaTime * 4f;
        foreach (var water in waters)
        {
            if (water == null || water.shoreRadius < 2f) continue; // barrels and small taps are not bodies of water
            var b = new Bounds(water.transform.position, Vector3.zero); float radius = water.shoreRadius - 0.4f;
            Vector3 toNext = next - b.center; toNext.y = 0f;
            if (toNext.magnitude >= radius) continue;
            Vector3 outward = toNext.sqrMagnitude > 0.001f ? toNext.normalized : -movement.normalized;
            float inward = Vector3.Dot(movement, -outward);
            if (inward > 0f) movement += outward * inward; // drop the part heading into the water, keep sliding along the shore
        }
        return movement;
    }
    public void CancelWalkOrder() { hasPath = false; direct = false; }
    public void Teleport(Vector3 position)
    {
        CancelWalkOrder(); verticalSpeed = 0f;
        if (controller == null) controller = GetComponent<CharacterController>();
        controller.enabled = false; transform.position = position + Vector3.up * 0.1f; controller.enabled = true;
    }

    private void ShowMarker(Vector3 point)
    {
        if (marker == null)
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Walk Marker";
            Destroy(marker.GetComponent<Collider>());
            marker.transform.localScale = new Vector3(0.7f, 0.015f, 0.7f);
            var renderer = marker.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.35f, 0.95f, 0.55f));
            renderer.SetPropertyBlock(block);
        }
        marker.transform.position = point + Vector3.up * 0.05f;
        marker.SetActive(true);
        markerUntil = Time.time + 1.2f;
    }

    private void UpdateMarker()
    {
        if (marker != null && marker.activeSelf && (Time.time > markerUntil || !HasWalkOrder)) marker.SetActive(false);
    }

    private void UpdateWeaponMount()
    {
        if (firstPersonCamera == null) return;
        Vector3 direction = transform.forward;
        if (!Paused && EcoIsoCamera.TryCursorPoint(transform, out var point))
        {
            Vector3 d = point - firstPersonCamera.transform.position;
            if (d.sqrMagnitude > 0.04f) direction = d.normalized;
        }
        firstPersonCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Notify(string message)
    {
        if (weapons == null) weapons = GetComponentInChildren<PlayerWeaponSystem>(true);
        if (weapons != null) weapons.ShowFeedback(message);
    }

    // ------------------------------------------------------------------ pause

    public static void SetPaused(bool paused)
    {
        EcoUIFlow.SetPaused(paused);
    }

    private void OnGUI()
    {
        if (!Paused || FindFirstObjectByType<EcoGameMenu>() != null) return;
        float width = 360f, height = 110f;
        var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(rect, "DURAKLATILDI");
        GUI.Label(new Rect(rect.x + 20f, rect.y + 36f, width - 40f, 24f), "Dünya durdu. Devam etmek için Esc.");
        if (GUI.Button(new Rect(rect.x + 20f, rect.y + 66f, width - 40f, 30f), "Devam et")) SetPaused(false);
    }

    private void OnDisable()
    {
        if (Paused) SetPaused(false);
        if (marker != null) Destroy(marker);
    }
}
