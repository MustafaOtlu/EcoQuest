using UnityEngine;

/// <summary>Keeps a physical market sign and its lettering parallel to the game camera.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class EcoMarketSign : MonoBehaviour
{
    [SerializeField, Min(1f)] private float minimumTextPixels = 22f;
    [SerializeField, Min(0.01f)] private float authoredCapitalHeight = 0.28f;
    [SerializeField, Range(1f, 1.45f)] private float maximumScale = 1.45f;
    [SerializeField, HideInInspector] private Vector3 authoredLocalScale;
    [SerializeField, HideInInspector] private bool hasAuthoredScale;

    private Camera mainCamera;

    private void OnEnable()
    {
        CaptureAuthoredScale();
        mainCamera = Camera.main;
        RefreshForCamera(mainCamera);
    }

    private void LateUpdate()
    {
        if (mainCamera == null || !mainCamera.isActiveAndEnabled) mainCamera = Camera.main;
        RefreshForCamera(mainCamera);
    }

    private void CaptureAuthoredScale()
    {
        if (hasAuthoredScale) return;
        authoredLocalScale = transform.localScale;
        hasAuthoredScale = true;
    }

    public void RefreshForCamera(Camera camera)
    {
        if (camera == null || camera.pixelHeight <= 0) return;
        CaptureAuthoredScale();

        transform.rotation = camera.transform.rotation;
        float worldPerPixel;
        if (camera.orthographic)
            worldPerPixel = 2f * camera.orthographicSize / camera.pixelHeight;
        else
        {
            float depth = Mathf.Max(camera.nearClipPlane,
                Vector3.Dot(transform.position - camera.transform.position, camera.transform.forward));
            worldPerPixel = 2f * depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / camera.pixelHeight;
        }

        float capitalHeight = Mathf.Max(0.001f, authoredCapitalHeight * Mathf.Abs(authoredLocalScale.y));
        float scale = Mathf.Clamp(minimumTextPixels * worldPerPixel / capitalHeight, 1f,
            Mathf.Clamp(maximumScale, 1f, 1.45f));
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            authoredLocalScale.x * scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            authoredLocalScale.y * scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            authoredLocalScale.z * scale / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
    }
}
