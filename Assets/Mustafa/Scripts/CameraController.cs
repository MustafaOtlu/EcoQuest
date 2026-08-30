using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] Transform player;

    Vector3 offset = new(0f, 0f, -10f);
    Vector3 buildModeOrigin;
    bool isBuildMode;

    void LateUpdate()
    {
        if (player == null) return;

        if (isBuildMode)
        {
            HandleBuildModeCamera();
        }
        else
        {
            FollowPlayer();
        }
    }

    void FollowPlayer()
    {
        Vector3 target = player.position + offset;
        transform.position = Vector3.Lerp(
            transform.position,
            target,
            GameConstants.CAMERA_FOLLOW_SPEED * Time.deltaTime
        );
    }

    void HandleBuildModeCamera()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector3 move = new Vector3(h, v, 0f) * GameConstants.BUILD_MODE_CAMERA_SPEED * Time.deltaTime;

        Vector3 newPos = transform.position + move;

        Vector3 playerPos = player.position + offset;
        Vector3 diff = newPos - playerPos;
        if (diff.magnitude > GameConstants.BUILD_MODE_CAMERA_MAX_DISTANCE)
        {
            diff = diff.normalized * GameConstants.BUILD_MODE_CAMERA_MAX_DISTANCE;
            newPos = playerPos + diff;
        }

        transform.position = newPos;
    }

    public void EnterBuildMode()
    {
        isBuildMode = true;
        buildModeOrigin = transform.position;
    }

    public void ExitBuildMode()
    {
        isBuildMode = false;
    }

    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }

    public bool IsBuildMode => isBuildMode;
}
