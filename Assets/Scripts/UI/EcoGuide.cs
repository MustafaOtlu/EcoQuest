using UnityEngine;

// Player guidance for the current objective (the card at the top): a marker floats over the goal with its distance,
// and when the goal is off screen an arrow on the screen edge points the way. The target comes from EcoQuestHUD,
// which picks it together with the objective text, so the card and the marker never disagree.
public sealed class EcoGuide : MonoBehaviour
{
    /// <summary>World position of the current objective (null: nothing to walk to, e.g. a goal right here).</summary>
    public static Vector3? Target { get; set; }
    /// <summary>Short name of the goal shown under the marker ("Mahalle çocuğu", "Tarla"…).</summary>
    public static string Label { get; set; } = "";

    private const float NearEnough = 5f;     // closer than this the marker steps aside; the context hint takes over
    private static Texture2D arrow;
    private Transform player;

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || Target == null) return;
        if (PlayerController.Paused || EcoUI.ForeignWindowOpen || EcoWorldHUD.MapOpen || EcoUI.Mode == EcoUI.HudMode.Hidden) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (player == null) { var p = FindFirstObjectByType<PlayerController>(); if (p == null) return; player = p.transform; }
        Vector3 goal = Target.Value;
        float distance = Vector2.Distance(new Vector2(goal.x, goal.z), new Vector2(player.position.x, player.position.z));
        if (distance < NearEnough) return;

        EcoTheme.Begin();
        float s = Screen.width / EcoTheme.Width;
        Vector3 screen = cam.WorldToScreenPoint(goal + Vector3.up * 2.6f);
        var point = new Vector2(screen.x / s, (Screen.height - screen.y) / s);
        const float inset = 70f;
        bool onScreen = screen.z > 0f && point.x > inset && point.x < EcoTheme.Width - inset && point.y > inset + 40f && point.y < EcoTheme.Height - 170f;
        string text = Label + "  " + Mathf.RoundToInt(distance) + " m";
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);

        if (onScreen)
        {
            // Bobbing pin above the goal.
            float bob = pulse * 6f;
            EcoTheme.Icon(new Rect(point.x - 17f, point.y - 40f - bob, 34f, 34f), "poi_quest");
            Chip(new Vector2(point.x, point.y + 2f - bob), text);
        }
        else
        {
            // Arrow on an inset ellipse around the screen centre, turned towards the goal.
            var centre = new Vector2(EcoTheme.Width * 0.5f, EcoTheme.Height * 0.5f);
            Vector2 dir = point - centre;
            if (screen.z < 0f) dir = -dir;                     // behind the camera: flip so the arrow points the right way
            if (dir.sqrMagnitude < 1f) dir = Vector2.up;
            dir.Normalize();
            float rx = EcoTheme.Width * 0.5f - inset - 60f, ry = EcoTheme.Height * 0.5f - 190f; // clear of the objective card, toasts and the tool dock
            float k = 1f / Mathf.Sqrt(dir.x * dir.x / (rx * rx) + dir.y * dir.y / (ry * ry));
            Vector2 at = centre + dir * k;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, Vector2.Scale(at, new Vector2(s, s)));
            var prev = GUI.color; GUI.color = new Color(1f, 0.82f, 0.36f, 0.75f + 0.25f * pulse);
            GUI.DrawTexture(new Rect(at.x - 18f, at.y - 24f, 36f, 36f), Arrow());
            GUI.color = prev;
            GUI.matrix = matrix;
            Vector2 label = at - dir * 46f;
            EcoTheme.Icon(new Rect(label.x - 13f, label.y - 30f, 26f, 26f), "poi_quest");
            Chip(new Vector2(label.x, label.y - 2f), text);
        }
        EcoTheme.End();
    }

    private static GUIStyle chipText;

    // Paper label in the same wooden frame as the other world labels, dark ink so it reads on any ground.
    private static void Chip(Vector2 topCentre, string text)
    {
        if (chipText == null)
        {
            chipText = new GUIStyle(EcoTheme.Body) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            chipText.normal.textColor = EcoUI.Ink; chipText.hover.textColor = EcoUI.Ink;
        }
        // The wooden frame is 16 px on every side; the box must be tall enough to leave paper behind the text.
        float width = Mathf.Min(300f, chipText.CalcSize(new GUIContent(text)).x + 48f);
        var box = new Rect(Mathf.Clamp(topCentre.x - width * 0.5f, 8f, EcoTheme.Width - width - 8f), topCentre.y, width, 54f);
        EcoTheme.Panel(box, false);
        GUI.Label(new Rect(box.x + 18f, box.y + 15f, box.width - 36f, 22f), text, chipText);
    }

    // A soft white chevron, made once (tinted when drawn).
    private static Texture2D Arrow()
    {
        if (arrow != null) return arrow;
        const int n = 64;
        arrow = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                // Point at the top (y = n), base at the bottom with a notch: a chevron that reads at small sizes.
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n;
                float half = (1f - v) * 0.48f;                    // triangle half-width shrinks towards the tip
                float notch = 0.28f - Mathf.Abs(u) * 0.55f;       // cut-in at the base
                float edge = Mathf.Min(half - Mathf.Abs(u), v - notch);
                float a = Mathf.Clamp01(edge * n * 0.6f + 0.5f);
                arrow.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        arrow.Apply();
        return arrow;
    }
}
