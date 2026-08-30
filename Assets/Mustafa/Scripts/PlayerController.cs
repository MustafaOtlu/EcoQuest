using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    Animator animator;
    Vector2 moveInput;
    bool isBuildMode;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        if (isBuildMode)
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        if (moveInput.x != 0)
            spriteRenderer.flipX = moveInput.x < 0;

        if (animator != null)
        {
            animator.SetFloat("Speed", moveInput.magnitude);
            animator.SetFloat("Horizontal", moveInput.x);
            animator.SetFloat("Vertical", moveInput.y);
        }
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveInput * GameConstants.PLAYER_MOVE_SPEED;
    }

    public void SetBuildMode(bool active)
    {
        isBuildMode = active;
        if (active)
            rb.linearVelocity = Vector2.zero;
    }

    public bool IsBuildMode => isBuildMode;
}
