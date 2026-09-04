using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    Rigidbody rb;
    Vector3 moveInput;
    bool isBuildMode;
    public float moveSpeed = 5f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Update()
    {
        if (isBuildMode)
        {
            moveInput = Vector3.zero;
            return;
        }

        moveInput = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0,
            Input.GetAxisRaw("Vertical")
        ).normalized;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = new Vector3(moveInput.x * moveSpeed, rb.linearVelocity.y, moveInput.z * moveSpeed);
        
        if (moveInput.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveInput);
            rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.fixedDeltaTime));
        }
    }

    public void SetBuildMode(bool active)
    {
        isBuildMode = active;
        if (active)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
    }

    public bool IsBuildMode => isBuildMode;
}
