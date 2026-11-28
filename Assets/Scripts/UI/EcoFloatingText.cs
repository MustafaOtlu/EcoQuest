using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small world-anchored labels that pop in, rise and fade: "+2 metal" when the vacuum takes litter, a "!" over a
/// walker caught littering. Drawn with IMGUI over the game view, so they need no canvas and cost nothing when idle.
/// </summary>
public sealed class EcoFloatingText : MonoBehaviour
{
    private sealed class Label
    {
        public string text; public Color color; public Vector3 at; public Transform follow; public Vector3 offset;
        public float age, life, rise, size;
    }

    private static EcoFloatingText current;
    private readonly List<Label> labels = new List<Label>();
    private GUIStyle style;

    public static readonly Color Metal = new Color32(206, 218, 232, 255);
    public static readonly Color Plastic = new Color32(120, 214, 255, 255);
    public static readonly Color Organic = new Color32(150, 226, 110, 255);
    public static readonly Color Alert = new Color32(255, 214, 64, 255);

    public static void Show(Vector3 world, string text, Color color, float life = 1.3f, float rise = 1f, float size = 30f)
        => Add(new Label { text = text, color = color, at = world, life = life, rise = rise, size = size });

    /// <summary>A label that rides along with a moving transform (a walker's head).</summary>
    public static void Follow(Transform follow, Vector3 offset, string text, Color color, float life = 1.2f, float size = 46f)
        => Add(new Label { text = text, color = color, follow = follow, offset = offset, at = follow.position + offset, life = life, rise = 0.25f, size = size });

    /// <summary>"+2 metal", "+1 plastik" …, one line each, stacked so they never overlap.</summary>
    public static void Gain(Vector3 world, int metal, int plastic, int organic)
    {
        float lift = 0f;
        if (metal > 0) { Show(world + Vector3.up * lift, "+" + metal + " metal", Metal); lift += 0.6f; }
        if (plastic > 0) { Show(world + Vector3.up * lift, "+" + plastic + " plastik", Plastic); lift += 0.6f; }
        if (organic > 0) Show(world + Vector3.up * lift, "+" + organic + " organik", Organic);
    }

    private static void Add(Label label)
    {
        if (current == null) current = new GameObject("Floating text").AddComponent<EcoFloatingText>();
        current.labels.Add(label);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => current = null;

    private void OnDestroy() { if (current == this) current = null; }

    private void Update()
    {
        for (int i = labels.Count - 1; i >= 0; i--)
        {
            var l = labels[i];
            l.age += Time.deltaTime;
            if (l.follow != null) l.at = l.follow.position + l.offset;
            if (l.age >= l.life) labels.RemoveAt(i);
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || labels.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
        float ui = Screen.height / 1080f;
        foreach (var l in labels)
        {
            float t = l.age / l.life;
            var world = l.at + Vector3.up * (l.rise * (1f - (1f - t) * (1f - t)));
            var screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) continue;
            // Pop: overshoot to 125 % in the first tenth, settle, fade over the last 40 %.
            float pop = t < 0.1f ? Mathf.Lerp(0.4f, 1.25f, t / 0.1f) : t < 0.2f ? Mathf.Lerp(1.25f, 1f, (t - 0.1f) / 0.1f) : 1f;
            float alpha = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
            style.fontSize = Mathf.Max(6, Mathf.RoundToInt(Mathf.Max(l.size * ui, l.size * 0.6f) * pop));
            var rect = new Rect(screen.x - 150f, Screen.height - screen.y - 30f, 300f, 60f);
            var content = new GUIContent(l.text);
            // A dark outline keeps the label readable on snow, asphalt and grass alike.
            style.normal.textColor = new Color(0.08f, 0.1f, 0.08f, 0.85f * alpha);
            float o = Mathf.Max(1f, 2f * ui);
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (dx != 0 || dy != 0) style.Draw(new Rect(rect.x + dx * o, rect.y + dy * o, rect.width, rect.height), content, false, false, false, false);
            style.normal.textColor = new Color(l.color.r, l.color.g, l.color.b, alpha);
            style.Draw(rect, content, false, false, false, false);
        }
    }
}
