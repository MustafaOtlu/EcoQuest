using UnityEngine;

// Keep station directions readable from either side of the work area.
[RequireComponent(typeof(Canvas))]
public sealed class WorldSpaceLabel : MonoBehaviour
{
    private Camera view;
    private Canvas canvas;

    private void Awake() => canvas = GetComponent<Canvas>();

    private void LateUpdate()
    {
        if (view == null || !view.isActiveAndEnabled)
        {
            view = Camera.main;
            if (view == null || !view.isActiveAndEnabled)
            {
                if (Camera.allCamerasCount > 0) view = Camera.allCameras[0];
            }
        }
        if (view == null) return;
        canvas.worldCamera = view;
        transform.rotation = view.transform.rotation;
    }
}
