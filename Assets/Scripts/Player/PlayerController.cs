using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float rotationSpeed = 10f;
    public float gravity = -9.81f;

    [Header("Cinemachine Cameras")]
    public GameObject thirdPersonCamera; // FreeLook Kamerası
    public GameObject firstPersonCamera; // 1. Şahıs Virtual Kamerası

    private CharacterController controller;
    public Animator characterAnimator;

    private Vector2 moveInput;
    private bool isRunning;
    private bool isFirstPerson = false;

    private Vector3 velocity;
    private Transform mainCameraTransform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>();
        
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }

        // Fareyi gizle ve kilitle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Başlangıçta 3. şahıs kamerası aktif olsun
        if (thirdPersonCamera != null) thirdPersonCamera.SetActive(true);
        if (firstPersonCamera != null) firstPersonCamera.SetActive(false);
    }

    void Update()
    {
        HandleCameraToggle();
        HandleMovement();
    }

    private void HandleCameraToggle()
    {
        // V tuşuna basıldığında kameraları Aç/Kapat
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            isFirstPerson = !isFirstPerson;

            if (thirdPersonCamera != null) thirdPersonCamera.SetActive(!isFirstPerson);
            if (firstPersonCamera != null) firstPersonCamera.SetActive(isFirstPerson);
        }
    }

    private void HandleMovement()
    {
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        if (moveInput == Vector2.zero) targetSpeed = 0f;

        Vector3 moveDirection = Vector3.zero;

        // Hareket yönünü aktif kameranın baktığı yöne göre hesapla
        if (mainCameraTransform != null)
        {
            Vector3 forward = mainCameraTransform.forward;
            Vector3 right = mainCameraTransform.right;
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

        // Yerçekimi
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        velocity.y += gravity * Time.deltaTime;

        // Karakteri Hareket Ettir
        controller.Move((moveDirection * targetSpeed + velocity) * Time.deltaTime);

        // --- ROTASYON ---
        if (isFirstPerson)
        {
            // 1. Şahıs: Karakter her zaman farenin (kameranın) baktığı yöne doğru döner
            if (mainCameraTransform != null)
            {
                transform.rotation = Quaternion.Euler(0f, mainCameraTransform.eulerAngles.y, 0f);
            }
        }
        else
        {
            // 3. Şahıs: Karakter sadece WASD ile hareket ettiği yöne doğru döner (FreeLook kamerasından bağımsız)
            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }
        }

        // --- ANİMASYON ---
        if (characterAnimator != null)
        {
            float currentSpeedPercent = moveInput != Vector2.zero ? (isRunning ? 1.0f : 0.5f) : 0f;
            float currentAnimSpeed = characterAnimator.GetFloat("Speed");
            characterAnimator.SetFloat("Speed", Mathf.Lerp(currentAnimSpeed, currentSpeedPercent, Time.deltaTime * 10f));
        }
    }

    // --- Input Action Events ---
    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnSprint(InputValue value)
    {
        isRunning = value.isPressed;
    }
}
