using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float gravity = -9.81f;
    [Header("First Person Look")]
    public float mouseSensitivity = 0.1f;
    public float gamepadLookSpeed = 150f;
    public float pitchLimit = 85f;
    public GameObject thirdPersonCamera;
    public GameObject firstPersonCamera;
    public Animator characterAnimator;

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private CharacterController controller;
    private PlayerVitals vitals;
    private InputAction moveAction, lookAction, sprintAction;
    private float verticalSpeed, pitch;
    private Renderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        vitals = GetComponent<PlayerVitals>();
        var input = GetComponent<PlayerInput>();
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;
        input.SwitchCurrentActionMap("Player");
        moveAction = input.actions.FindAction("Player/Move", true);
        lookAction = input.actions.FindAction("Player/Look", true);
        sprintAction = input.actions.FindAction("Player/Sprint", true);
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        if (characterAnimator != null) characterAnimator.applyRootMotion = false;
        ConfigureFirstPersonBody();
        if (thirdPersonCamera != null) thirdPersonCamera.SetActive(false);
        if (firstPersonCamera != null) firstPersonCamera.SetActive(true);
        SetCursorLocked(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
        else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        bool hasControl = Application.isFocused && Cursor.lockState == CursorLockMode.Locked
            && (vitals == null || !vitals.IsRecovering);
        if (hasControl && firstPersonCamera != null)
        {
            Vector2 look = lookAction.ReadValue<Vector2>();
            // Mouse delta is already a per-frame displacement; only stick input needs deltaTime.
            float sensitivity = lookAction.activeControl?.device is Gamepad
                ? gamepadLookSpeed * Time.deltaTime : mouseSensitivity;
            transform.Rotate(0f, look.x * sensitivity, 0f, Space.World);
            pitch = Mathf.Clamp(pitch - look.y * sensitivity, -pitchLimit, pitchLimit);
            firstPersonCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        Vector2 input = hasControl ? Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f) : Vector2.zero;
        float speed = sprintAction.IsPressed() ? runSpeed : walkSpeed;
        if (vitals != null) speed *= vitals.MovementMultiplier;
        // Backwards and sideways movement preserve the view direction.
        Vector3 movement = (transform.forward * input.y + transform.right * input.x) * speed;
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        controller.Move((movement + Vector3.up * verticalSpeed) * Time.deltaTime);

        if (characterAnimator != null)
        {
            Vector3 planarVelocity = controller.velocity;
            planarVelocity.y = 0f;
            float actualSpeed = planarVelocity.magnitude;
            float blend = actualSpeed <= walkSpeed
                ? Mathf.InverseLerp(0f, walkSpeed, actualSpeed) * 0.5f
                : 0.5f + Mathf.InverseLerp(walkSpeed, runSpeed, actualSpeed) * 0.5f;
            characterAnimator.SetFloat(SpeedParameter, blend, 0.1f, Time.deltaTime);
        }
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void ConfigureFirstPersonBody()
    {
        if (characterAnimator == null || firstPersonCamera == null) return;
        // The camera sits inside the full-body mesh. Keep its animated shadow,
        // but hide the mesh surfaces that would otherwise cover the FPS view.
        // Weapons live under the camera, outside this character hierarchy.
        bodyRenderers = characterAnimator.GetComponentsInChildren<Renderer>(true);
        originalShadowModes = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            originalShadowModes[i] = bodyRenderers[i].shadowCastingMode;
            bodyRenderers[i].shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private void OnEnable()
    {
        if (bodyRenderers == null) return; // Start initializes the renderer list.
        foreach (var bodyRenderer in bodyRenderers)
            if (bodyRenderer != null) bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
        if (bodyRenderers == null) return;
        for (int i = 0; i < bodyRenderers.Length; i++)
            if (bodyRenderers[i] != null) bodyRenderers[i].shadowCastingMode = originalShadowModes[i];
    }
}
