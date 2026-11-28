using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 12"): what the player looks at is labelled in the world, above the thing
/// itself, instead of in a panel at the bottom of the screen. The title is large, the values sit under it and the
/// action ("[E] Silahı şarj et") is yellow. Pointing at something else lets the old label sink away while the new one
/// pops in; a value that changes (Depo 134 → 135) gives its own line a small green punch, at most twice a second, so
/// a ticking store breathes instead of flickering. Cosmetic only; the hint text itself comes from the tools.
/// </summary>
public sealed class EcoContextLabel : MonoBehaviour
{
    private enum Kind { Title, Body, Action }
    private sealed class Line
    {
        public Text text; public Kind kind; public float punch = -1f, lastPunch = -9f;
    }

    private static EcoContextLabel current;
    private static Material overlay;
    private readonly List<Line> lines = new List<Line>();
    private CanvasGroup group;
    private RectTransform root;
    private Vector3 anchor, shown;
    private string title = "", text = "";
    private float age = -1f, fade, leaving = -1f;
    private bool visible, ghost;

    private static readonly Color TitleColor = Color.white;
    private static readonly Color BodyColor = new Color32(226, 236, 228, 255);
    private static readonly Color ActionColor = new Color32(255, 214, 64, 255);
    private static readonly Color PunchColor = new Color32(140, 240, 150, 255);
    private const float LineGap = 4f, LineWidth = 640f, ScreenScale = 0.0016f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; }

    /// <summary>Shows <paramref name="hint"/> above <paramref name="top"/> (the top of the thing it is about).</summary>
    public static void Show(string hint, Vector3 top)
    {
        if (string.IsNullOrEmpty(hint)) { Hide(); return; }
        if (current == null) current = Create(false);
        current.Set(hint, top);
    }

    public static void Hide() { if (current != null) current.visible = false; }

    private static EcoContextLabel Create(bool asGhost)
    {
        var go = new GameObject(asGhost ? "Context label (leaving)" : "Context label");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 50;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
        var label = go.AddComponent<EcoContextLabel>();
        label.root = (RectTransform)go.transform;
        label.root.sizeDelta = new Vector2(900f, 300f);
        label.root.pivot = new Vector2(0.5f, 0f);
        label.group = go.AddComponent<CanvasGroup>();
        label.group.interactable = false; label.group.blocksRaycasts = false; label.group.alpha = 0f;
        label.ghost = asGhost;
        return label;
    }

    private static Material Overlay()
    {
        if (overlay != null) return overlay;
        // Drawn over the world (the thing it names may stand in front of its own label).
        overlay = new Material(Canvas.GetDefaultCanvasMaterial()) { hideFlags = HideFlags.DontSave };
        overlay.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        return overlay;
    }

    private void Set(string hint, Vector3 top)
    {
        var parts = Split(hint);
        string newTitle = parts.Count > 0 ? parts[0].Key : "";
        bool other = !visible || fade < 0.05f || newTitle != title || (top - anchor).sqrMagnitude > 4f;
        if (other && visible && fade > 0.3f && !string.IsNullOrEmpty(text)) Leave();
        anchor = top;
        if (other) { shown = top; age = 0f; }
        visible = true;
        if (hint == text) return;
        text = hint; title = newTitle;
        for (int i = 0; i < parts.Count; i++)
        {
            var line = i < lines.Count ? lines[i] : AddLine();
            line.text.gameObject.SetActive(true);
            bool changed = line.text.text != parts[i].Key || line.kind != parts[i].Value;
            if (line.kind != parts[i].Value) Style(line, parts[i].Value);
            line.text.text = parts[i].Key;
            // Same thing, new value: punch the line, but never more often than twice a second.
            if (!other && changed && Time.unscaledTime - line.lastPunch > 0.5f) { line.punch = 0f; line.lastPunch = Time.unscaledTime; }
            if (other) line.punch = -1f;
        }
        for (int i = parts.Count; i < lines.Count; i++) lines[i].text.gameObject.SetActive(false);
        Layout();
    }

    /// <summary>The old label is copied and sinks away while this one pops in with the new text.</summary>
    private void Leave()
    {
        var copy = Create(true);
        copy.anchor = copy.shown = shown;
        copy.fade = fade; copy.leaving = 0f; copy.visible = false;
        foreach (var line in lines)
        {
            if (!line.text.gameObject.activeSelf) continue;
            var l = copy.AddLine(); Style(l, line.kind); l.text.text = line.text.text;
        }
        copy.Layout();
        copy.Place(0f);
    }

    private Line AddLine()
    {
        var go = new GameObject("Line", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(root, false);
        var t = go.GetComponent<Text>();
        t.font = EcoUI.Font; t.alignment = TextAnchor.LowerCenter; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow; // long lines wrap
        t.material = Overlay();
        var outline = go.AddComponent<Outline>(); outline.effectColor = new Color(0.03f, 0.07f, 0.05f, 0.9f); outline.effectDistance = new Vector2(2.2f, -2.2f);
        var shadow = go.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0f, 0f, 0.45f); shadow.effectDistance = new Vector2(3f, -5f);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(900f, 50f);
        var line = new Line { text = t, kind = (Kind)(-1) };
        lines.Add(line);
        return line;
    }

    private static void Style(Line line, Kind kind)
    {
        line.kind = kind;
        var t = line.text;
        t.fontSize = kind == Kind.Title ? 46 : 32;
        t.fontStyle = kind == Kind.Body ? FontStyle.Normal : FontStyle.Bold;
        t.color = kind == Kind.Title ? TitleColor : kind == Kind.Action ? ActionColor : BodyColor;
    }

    /// <summary>Stacks the lines bottom-up: actions at the bottom, the title on top.</summary>
    private void Layout()
    {
        float y = 0f;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (!line.text.gameObject.activeSelf) continue;
            var rect = line.text.rectTransform;
            rect.sizeDelta = new Vector2(LineWidth, 50f);
            float h = Mathf.Max(line.text.fontSize * 1.2f, line.text.preferredHeight);
            rect.sizeDelta = new Vector2(LineWidth, h);
            rect.anchoredPosition = new Vector2(0f, y);
            y += h + LineGap;
        }
    }

    /// <summary>"Şarj istasyonu\nÜretim … • Depo …\n[E] Silahı şarj et" → title, body lines, action lines. A first line
    /// "Ağaç • Sağlık %80" becomes the title "Ağaç" over the body "Sağlık %80".</summary>
    private static List<KeyValuePair<string, Kind>> Split(string hint)
    {
        var parts = new List<KeyValuePair<string, Kind>>();
        var raw = hint.Split('\n');
        for (int i = 0; i < raw.Length; i++)
        {
            string s = raw[i].Trim();
            if (s.Length == 0) continue;
            bool action = s.StartsWith("[");
            if (parts.Count == 0 && !action)
            {
                int dot = s.IndexOf(" • ");
                if (dot > 0 && dot <= 28)
                {
                    parts.Add(new KeyValuePair<string, Kind>(s.Substring(0, dot), Kind.Title));
                    parts.Add(new KeyValuePair<string, Kind>(s.Substring(dot + 3), Kind.Body));
                    continue;
                }
                parts.Add(new KeyValuePair<string, Kind>(s, Kind.Title));
                continue;
            }
            parts.Add(new KeyValuePair<string, Kind>(s, action ? Kind.Action : Kind.Body));
        }
        return parts;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (ghost)
        {
            leaving += dt;
            float k = leaving / 0.22f;
            if (k >= 1f) { Destroy(gameObject); return; }
            group.alpha = fade * (1f - k);
            Place(k);
            return;
        }
        fade = Mathf.MoveTowards(fade, visible ? 1f : 0f, dt / (visible ? 0.12f : 0.15f));
        group.alpha = fade;
        if (fade <= 0f) { text = ""; title = ""; return; }
        if (age >= 0f) age += dt;
        shown = Vector3.Lerp(shown, anchor, 1f - Mathf.Exp(-12f * dt));
        Place(-1f);
        foreach (var line in lines)
        {
            if (line.punch < 0f) { line.text.rectTransform.localScale = Vector3.one; continue; }
            line.punch += dt;
            float t = line.punch / 0.3f;
            if (t >= 1f) { line.punch = -1f; line.text.rectTransform.localScale = Vector3.one; Style(line, line.kind); continue; }
            line.text.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
            var baseColor = line.kind == Kind.Title ? TitleColor : line.kind == Kind.Action ? ActionColor : BodyColor;
            line.text.color = Color.Lerp(PunchColor, baseColor, t);
        }
    }

    /// <summary>Faces the camera at a steady on-screen size; <paramref name="leave"/> ≥ 0 sinks and shrinks a leaving copy.</summary>
    private void Place(float leave)
    {
        var cam = Camera.main;
        if (cam == null) return;
        float size = cam.orthographic ? cam.orthographicSize * ScreenScale
            : Vector3.Distance(cam.transform.position, shown) * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * ScreenScale;
        float pop = 1f, lift = 0f;
        if (leave >= 0f) { pop = 1f - 0.25f * leave; lift = -0.6f * leave; }
        else if (age >= 0f && age < 0.3f)
        {
            float t = age / 0.3f;
            // Pops in from below with an overshoot.
            pop = t < 0.55f ? Mathf.Lerp(0.55f, 1.1f, t / 0.55f) : Mathf.Lerp(1.1f, 1f, (t - 0.55f) / 0.45f);
            lift = -0.5f * (1f - t) * (1f - t);
        }
        float bob = leave >= 0f ? 0f : 0.04f * Mathf.Sin(Time.unscaledTime * 2.2f);
        root.position = shown + cam.transform.up * (lift + bob) * size * 300f;
        root.rotation = cam.transform.rotation;
        root.localScale = Vector3.one * size * pop;
    }
}
