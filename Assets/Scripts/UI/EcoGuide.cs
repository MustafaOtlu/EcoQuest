using UnityEngine;
using UnityEngine.UI;
using Tone = EcoHudStyle.Tone;

// Both navigation chips occupy the same space budget as world captions.
[DefaultExecutionOrder(510)]
public sealed class EcoGuide : MonoBehaviour
{
    public static Vector3? Target { get; set; }
    public static string Label { get; set; } = "";

    private Canvas canvas;
    private Chip objective, project;

    internal void Bind(Canvas targetCanvas, EcoHudStyle style)
    {
        canvas = targetCanvas;
        objective = new Chip(canvas.transform, style, "Objective waypoint");
        project = new Chip(canvas.transform, style, "Project waypoint");
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        WorldLabelLayout.GetMarkers(EcoIsoCamera.Cam, out var goal, out var hint);
        objective.Show(goal, canvas);
        project.Show(hint, canvas);
    }

    private sealed class Chip
    {
        private readonly RectTransform root, arrow;
        private readonly Image icon;
        private readonly Text label;

        public Chip(Transform parent, EcoHudStyle style, string name)
        {
            root = style.Surface(EcoUI.Panel(name, parent, Vector2.zero, new Vector2(.5f, .5f), Color.white, false)).rectTransform;
            root.SetAsFirstSibling();
            icon = EcoUI.Icon("Pin", root, "poi_quest", Vector2.up, Vector2.up, 16f);
            var chevron = EcoUI.Label("Direction", root, "▲", 13, TextAnchor.MiddleCenter, Color.white, true);
            style.Tint(chevron, Tone.Amber);
            EcoUI.Layout(chevron, new Vector2(5f, -4f), new Vector2(18f, 18f));
            arrow = chevron.rectTransform;
            // Rotate around the glyph's centre while keeping the same position inside the chip.
            arrow.pivot = new Vector2(0.5f, 0.5f);
            arrow.anchoredPosition = new Vector2(14f, -13f);
            label = EcoUI.Label("Distance", root, "", 13, TextAnchor.MiddleLeft, Color.white, true);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11 * EcoUI.RasterScale;
            label.resizeTextMaxSize = 13 * EcoUI.RasterScale;
            style.Tint(label, Tone.Text);
            root.gameObject.SetActive(false);
        }

        public void Show(WorldLabelLayout.Marker marker, Canvas canvas)
        {
            bool show = marker.Text != null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            var scale = Mathf.Max(0.01f, canvas.scaleFactor);
            var size = marker.Bounds.size / scale;
            EcoUI.Place(root, (marker.Bounds.center - canvas.pixelRect.min) / scale, size);
            bool directional = marker.Direction.sqrMagnitude > 0f;
            icon.enabled = !directional;
            arrow.gameObject.SetActive(directional);
            if (directional)
                arrow.localRotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, marker.Direction));
            EcoUI.LayoutIcon(icon, new Vector2(6f, -(size.y - 16f) * 0.5f), 16f);
            EcoUI.Layout(label, new Vector2(27f, -3f), new Vector2(size.x - 33f, size.y - 6f));
            label.text = marker.Text;
        }
    }
}
