using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Camera Settings")]
    public Camera playerCamera;
    public Transform firstPersonTarget;
    public Transform thirdPersonTarget;
    public float mouseSensitivity = 2f;
    public float cameraSmoothTime = 0.1f;

    private CharacterController controller;
    private Animator animator;
    private PlayerInput playerInput;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isRunning;
    private bool isFirstPerson = false;

    private Vector3 velocity;
    private float cameraPitch = 0f;
    private Vector3 cameraVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        
        // Hide and lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Default to Third Person if camera is assigned
        if (playerCamera != null && thirdPersonTarget != null)
        {
            playerCamera.transform.position = thirdPersonTarget.position;
            playerCamera.transform.rotation = thirdPersonTarget.rotation;
        }
    }

    void Update()
    {
        HandleCameraToggle();
        HandleMovement();
        HandleCameraLook();
    }

    private void HandleCameraToggle()
    {
        // V tuşuna basıldığında kamera açısını değiştir
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;
        }

        if (playerCamera == null) return;

        Transform targetPos = isFirstPerson && firstPersonTarget != null ? firstPersonTarget : thirdPersonTarget;
        
        if (targetPos != null)
        {
            // Smoothly move camera to target position
            playerCamera.transform.position = Vector3.SmoothDamp(playerCamera.transform.position, targetPos.position, ref cameraVelocity, cameraSmoothTime);
            
            // In first person, match rotation exactly. In 3rd person, look at the player slightly ahead
            if (isFirstPerson)
            {
                playerCamera.transform.rotation = targetPos.rotation;
            }
            else
            {
                playerCamera.transform.LookAt(transform.position + Vector3.up * 1.5f);
            }
        }
    }

    private void HandleMovement()
    {
        // Calculate speed
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        if (moveInput == Vector2.zero) targetSpeed = 0f;

        // Calculate direction relative to camera
        Vector3 moveDirection = Vector3.zero;
        
        if (playerCamera != null)
        {
            Vector3 forward = playerCamera.transform.forward;
            Vector3 right = playerCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            moveDirection = forward * moveInput.y + right * moveInput.x;
        }
        else
        {
            moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);
        }

        // Apply gravity
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;

        // Move Character
        controller.Move((moveDirection * targetSpeed + velocity) * Time.deltaTime);

        // Rotate Character
        if (moveDirection != Vector3.zero && !isFirstPerson)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
        else if (isFirstPerson)
        {
            // 1. Şahısta karakter her zaman kameranın baktığı yöne döner
            Vector3 camForward = playerCamera.transform.forward;
            camForward.y = 0;
            if (camForward != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(camForward);
            }
        }

        // Update Animator Blend Tree Parameter (0 = Idle, 0.5 = Walk, 1.0 = Run)
        float currentSpeedPercent = 0f;
        if (moveInput != Vector2.zero)
        {
            currentSpeedPercent = isRunning ? 1.0f : 0.5f;
        }
        
        // Smoothly blend animation
        float currentAnimSpeed = animator.GetFloat("Speed");
        animator.SetFloat("Speed", Mathf.Lerp(currentAnimSpeed, currentSpeedPercent, Time.deltaTime * 10f));
    }

    private void HandleCameraLook()
    {
        if (playerCamera == null) return;

        // Rotate Character and Camera based on mouse input
        float mouseX = lookInput.x * mouseSensitivity;
        float mouseY = lookInput.y * mouseSensitivity;

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -80f, 60f); // Sınırlandır

        if (isFirstPerson)
        {
            // 1. Şahıs görünümünde kamerayı yukarı/aşağı oynat
            if (firstPersonTarget != null)
            {
                firstPersonTarget.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
            // Sağa/Sola dönüşü HandleMovement içinde karakteri döndürerek hallediyoruz
        }
        else
        {
            // 3. Şahıs kamerası için hedefin etrafında dönme (Basit bir Orbit)
            if (thirdPersonTarget != null)
            {
                transform.Rotate(Vector3.up * mouseX);
                thirdPersonTarget.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }
    }

    // --- Input System Events (SendMessages) ---
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        // Note: Make sure there's an action named "Sprint" (Button type) in your Input Actions
        isRunning = value.isPressed;
    }
}
