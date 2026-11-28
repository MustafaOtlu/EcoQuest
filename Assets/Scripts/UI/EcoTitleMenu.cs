using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The title menu (GDD v5 §21, "Ana menü"). The left half of the screen is a black-to-clear panel with the game's
/// name and leaf-framed buttons; the right half shows the 3D set (<see cref="EcoTitleStage"/>) where the character
/// points its vacuum at the cursor. Holding the mouse button sucks dust towards the cursor. Clicking a button: the
/// vacuum pulls the button in, then shoots the panel it opens out to the middle of the screen, and the panel opens
/// when it lands. Closing a panel runs the same backwards: the panel is pulled in and the button shot back.
/// The panels themselves are the existing ones (EcoGameMenu's IMGUI screens, the expert demo, the survey).
/// </summary>
[DefaultExecutionOrder(-50)]
public sealed class EcoTitleMenu : MonoBehaviour
{
    public static bool Active => current != null;
    private static EcoTitleMenu current;
    internal static float Speed = 1f; // tests slow the animations down to capture them

    private enum Kind { NewGame, Continue, Tutorial, EnemyTutorial, Slots, Settings, Expert, Survey, Quit }
    private enum State { Idle, Pulling, Firing, Open, Closing, Returning, Leaving }

    private sealed class Entry
    {
        public Kind kind; public RectTransform rect; public CanvasGroup group; public Text label; public bool enabled = true, over; public float hover;
    }

    private EcoGameMenu menu;
    private EcoTitleStage stage;
    private Canvas canvas;
    private RectTransform fxLayer;
    private CanvasGroup rootGroup;
    private readonly List<Entry> entries = new List<Entry>();
    private readonly List<Behaviour> hiddenCanvases = new List<Behaviour>();
    private State state = State.Idle;
    private Entry active;
    private RectTransform flyer;
    private float t;
    private Vector2 from;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; }

    public static void Open(EcoGameMenu owner)
    {
        if (current != null) return;
        var go = new GameObject("Title menu");
        current = go.AddComponent<EcoTitleMenu>();
        current.menu = owner;
        current.Build();
    }

    /// <summary>Closes the menu and its 3D set and gives the screen back to the game.</summary>
    public static void Close()
    {
        if (current == null) return;
        var menu = current; current = null;
        foreach (var c in menu.hiddenCanvases) if (c != null) c.enabled = true;
        if (menu.stage != null) menu.stage.Close();
        Destroy(menu.gameObject);
    }

    // ------------------------------------------------------------------ build

    private void Build()
    {
        // The valley's HUD hides behind the menu (its overlay canvases come back on Close).
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.isRootCanvas && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; hiddenCanvases.Add(c); }
        stage = EcoTitleStage.Build();
        EcoUI.EnsureEventSystem();

        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 1f;
        rootGroup = gameObject.AddComponent<CanvasGroup>();
        var rootRect = (RectTransform)transform;

        var panel = Child("Panel", rootRect);
        panel.anchorMin = Vector2.zero; panel.anchorMax = new Vector2(0.5f, 1f); panel.offsetMin = panel.offsetMax = Vector2.zero;
        var panelImage = panel.gameObject.AddComponent<Image>(); panelImage.sprite = EcoLeafArt.Gradient(); panelImage.raycastTarget = false;

        var title = Label(panel, "EcoQuest", 104, FontStyle.Bold, new Color32(214, 244, 196, 255), TextAnchor.UpperLeft);
        title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0f, 1f); title.rectTransform.pivot = new Vector2(0f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(120f, -70f); title.rectTransform.sizeDelta = new Vector2(760f, 130f);
        var shadow = title.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(0f, 0.2f, 0.05f, 0.8f); shadow.effectDistance = new Vector2(4f, -5f);
        var sub = Label(panel, "Kirlenen vadiyi birlikte toparla.", 30, FontStyle.Italic, new Color32(226, 236, 214, 230), TextAnchor.UpperLeft);
        sub.rectTransform.anchorMin = sub.rectTransform.anchorMax = new Vector2(0f, 1f); sub.rectTransform.pivot = new Vector2(0f, 1f);
        sub.rectTransform.anchoredPosition = new Vector2(126f, -196f); sub.rectTransform.sizeDelta = new Vector2(760f, 44f);

        var save = EcoWorldSave.Current;
        float y = -262f;
        AddButton(panel, Kind.NewGame, "Yeni oyun", ref y, true);
        AddButton(panel, Kind.Continue, "Devam et", ref y, false).enabled = save != null && save.HasSave;
        AddButton(panel, Kind.Tutorial, "Öğretici", ref y, false);
        AddButton(panel, Kind.Slots, "Kayıtlar", ref y, false);
        AddButton(panel, Kind.Settings, "Ayarlar", ref y, false);
        AddButton(panel, Kind.Survey, "Sürdürülebilirlik anketi", ref y, false);
        AddButton(panel, Kind.Quit, "Oyundan çık", ref y, false);
        foreach (var e in entries) if (!e.enabled) e.group.alpha = 0.45f;

        fxLayer = Child("Flying", rootRect);
        fxLayer.anchorMin = Vector2.zero; fxLayer.anchorMax = Vector2.one; fxLayer.offsetMin = fxLayer.offsetMax = Vector2.zero;
    }

    private static RectTransform Child(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Text Label(RectTransform parent, string text, int size, FontStyle style, Color color, TextAnchor anchor)
    {
        var rect = Child("Text", parent);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = EcoUI.Font; label.fontSize = size; label.fontStyle = style; label.color = color; label.alignment = anchor;
        label.text = text; label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Overflow;
        return label;
    }

    private Entry AddButton(RectTransform panel, Kind kind, string text, ref float y, bool primary)
    {
        float w = 480f, h = primary ? 88f : 72f;
        var rect = Child(text, panel);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(120f + w * 0.5f, y - h * 0.5f);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = EcoLeafArt.Frame((int)w, (int)h, (int)kind * 13 + 5); image.raycastTarget = false;
        var group = rect.gameObject.AddComponent<CanvasGroup>(); group.interactable = false; group.blocksRaycasts = false;
        var label = Label(rect, text, primary ? 36 : 29, FontStyle.Bold, EcoLeafArt.Ink, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        y -= h + 8f;
        var entry = new Entry { kind = kind, rect = rect, group = group, label = label };
        entries.Add(entry);
        return entry;
    }

    // ------------------------------------------------------------------ frame

    private void Update()
    {
        // Anything that starts the game (or a scene load) without us closes the menu with it.
        if (!PlayerController.Paused && state != State.Leaving) { Close(); return; }
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * Speed;
        var mouse = Mouse.current;
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame, held = mouse != null && mouse.leftButton.isPressed;

        switch (state)
        {
            case State.Idle: Idle(pointer, pressed, held, dt); break;
            case State.Pulling: Pulling(dt); break;
            case State.Firing: Firing(dt); break;
            case State.Open: WhileOpen(pointer); break;
            case State.Closing: ClosingPanel(dt); break;
            case State.Returning: Returning(dt); break;
            case State.Leaving: Leaving(dt); break;
        }
        foreach (var e in entries)
        {
            float target = state == State.Idle && e.enabled && e.over ? 1f : 0f;
            e.hover = Mathf.MoveTowards(e.hover, target, dt * 6f);
            float s = 1f + 0.06f * Smooth(e.hover);
            e.rect.localScale = Vector3.one * s;
            e.rect.localRotation = Quaternion.Euler(0f, 0f, e.hover > 0f ? Mathf.Sin(Time.unscaledTime * 9f) * 1.2f * e.hover : 0f);
        }
    }

    private void Idle(Vector2 pointer, bool pressed, bool held, float dt)
    {
        Entry hovered = null;
        foreach (var e in entries)
        {
            e.over = RectTransformUtility.RectangleContainsScreenPoint(e.rect, pointer, null);
            if (e.over) hovered = e;
        }
        stage.AimAt(pointer);
        if (pressed && hovered != null && hovered.enabled) { StartPull(hovered); return; }
        // Holding the button anywhere else only runs the vacuum.
        if (held) stage.Suck(stage.WorldAt(pointer), dt);
    }

    // A button is pulled into the vacuum along a curve, shrinking and spinning.
    private void StartPull(Entry entry)
    {
        active = entry; state = State.Pulling; t = 0f;
        flyer = Copy(entry.rect);
        entry.group.alpha = 0f;
        from = ScreenOf(entry.rect);
    }

    private void Pulling(float dt)
    {
        t += dt / 0.5f;
        float k = Mathf.Clamp01(t), ease = k * k;
        Vector2 mouth = stage.MuzzleScreen;
        Vector2 at = Bezier(from, (from + mouth) * 0.5f + new Vector2(0f, 140f), mouth, ease);
        Place(flyer, at, Mathf.Lerp(1f, 0.06f, ease), 400f * ease);
        stage.AimAt(at);
        stage.Suck(stage.WorldAt(at), dt, 220f);
        if (k < 1f) return;
        Destroy(flyer.gameObject); flyer = null;
        stage.Pop();
        switch (active.kind)
        {
            case Kind.Slots: case Kind.Settings: case Kind.Expert: case Kind.Survey:
                StartFire(); break;
            case Kind.NewGame when EcoRun.Current != null && !EcoRun.Current.Active:
                StartFire(); break; // the valley briefing opens over the title
            case Kind.Quit:
                Application.Quit();
                StartReturn(); break; // in the editor (or if quitting is refused) the button comes back
            default:
                state = State.Leaving; t = 0f; break;
        }
    }

    // The opened panel is shot out of the vacuum to the middle of the screen; it opens when it lands.
    private void StartFire()
    {
        state = State.Firing; t = 0f;
        flyer = Card();
        from = stage.MuzzleScreen;
    }

    private void Firing(float dt)
    {
        t += dt / 0.45f;
        float k = Mathf.Clamp01(t), ease = 1f - (1f - k) * (1f - k);
        Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 at = Bezier(from, (from + centre) * 0.5f + new Vector2(0f, 160f), centre, ease);
        float scale = k < 0.8f ? Mathf.Lerp(0.06f, 1.06f, ease / 0.96f) : Mathf.Lerp(1.06f, 1f, (k - 0.8f) / 0.2f);
        Place(flyer, at, scale, -30f * (1f - ease));
        stage.AimAt(at);
        if (k < 0.7f) stage.Blow(stage.WorldAt(at), dt, 160f);
        if (k < 1f) return;
        stage.Kick();
        state = State.Open; t = 0f;
        switch (active.kind)
        {
            case Kind.Slots: menu.TitleOpenScreen(false); break;
            case Kind.Settings: menu.TitleOpenScreen(true); break;
            case Kind.Expert: EcoExpertDemo.Current?.Open(); break;
            case Kind.Survey: EcoSurvey.Current?.Open(false); break;
            case Kind.NewGame: EcoRun.Current?.RequestNewRun(); break;
        }
    }

    private void WhileOpen(Vector2 pointer)
    {
        // The card fades out under the real panel.
        if (flyer != null)
        {
            t += Time.unscaledDeltaTime / 0.18f;
            var g = flyer.GetComponent<CanvasGroup>();
            g.alpha = 1f - Mathf.Clamp01(t);
            if (t >= 1f) { Destroy(flyer.gameObject); flyer = null; }
        }
        stage.AimAt(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        var run = EcoRun.Current;
        bool open = active.kind == Kind.Expert ? EcoExpertDemo.PanelOpen
            : active.kind == Kind.Survey ? EcoSurvey.Current != null && EcoSurvey.Current.IsOpen
            : active.kind == Kind.NewGame ? run != null && run.BriefingOpen
            : !menu.TitleOnMain;
        if (open) return;
        // "Vadiye in" took the pending valley: the run has started (or the scene is reloading into it).
        if (active.kind == Kind.NewGame && (run == null || run.Pending == null))
        {
            if (flyer != null) { Destroy(flyer.gameObject); flyer = null; }
            var owner = menu;
            Close();
            owner.TitleStart(4);
            return;
        }
        // Closed: the panel is pulled back into the vacuum.
        if (flyer != null) Destroy(flyer.gameObject);
        flyer = Card();
        state = State.Closing; t = 0f;
        from = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private void ClosingPanel(float dt)
    {
        t += dt / 0.45f;
        float k = Mathf.Clamp01(t), ease = k * k;
        Vector2 mouth = stage.MuzzleScreen;
        Vector2 at = Bezier(from, (from + mouth) * 0.5f + new Vector2(0f, 160f), mouth, ease);
        Place(flyer, at, Mathf.Lerp(1f, 0.06f, ease), -300f * ease);
        stage.AimAt(at);
        stage.Suck(stage.WorldAt(at), dt, 220f);
        if (k < 1f) return;
        Destroy(flyer.gameObject); flyer = null;
        stage.Pop();
        StartReturn();
    }

    // The button is shot back out to its place in the column.
    private void StartReturn()
    {
        state = State.Returning; t = 0f;
        flyer = Copy(active.rect);
        from = stage.MuzzleScreen;
    }

    private void Returning(float dt)
    {
        t += dt / 0.42f;
        float k = Mathf.Clamp01(t), ease = 1f - (1f - k) * (1f - k);
        Vector2 home = ScreenOf(active.rect);
        Vector2 at = Bezier(from, (from + home) * 0.5f + new Vector2(0f, 140f), home, ease);
        float scale = k < 0.8f ? Mathf.Lerp(0.06f, 1.08f, ease / 0.96f) : Mathf.Lerp(1.08f, 1f, (k - 0.8f) / 0.2f);
        Place(flyer, at, scale, 360f * (1f - ease));
        stage.AimAt(at);
        if (k < 0.7f) stage.Blow(stage.WorldAt(at), dt, 140f);
        if (k < 1f) return;
        stage.Kick();
        Destroy(flyer.gameObject); flyer = null;
        active.group.alpha = active.enabled ? 1f : 0.45f;
        active = null; state = State.Idle;
    }

    // Starting the game: the whole menu fades, then the choice runs.
    private void Leaving(float dt)
    {
        t += dt / 0.3f;
        rootGroup.alpha = 1f - Mathf.Clamp01(t);
        if (t < 1f) return;
        var kind = active.kind;
        var owner = menu;
        Close();
        owner.TitleStart(kind == Kind.NewGame ? 0 : kind == Kind.Continue ? 1 : kind == Kind.Tutorial ? 2 : 3);
    }

    // ------------------------------------------------------------------ helpers

    private RectTransform Copy(RectTransform source)
    {
        var copy = Instantiate(source.gameObject, fxLayer);
        var rect = (RectTransform)copy.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = source.sizeDelta; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        var group = copy.GetComponent<CanvasGroup>(); if (group != null) group.alpha = 1f;
        return rect;
    }

    /// <summary>A stand-in for the panel while it flies: a large leaf-framed card the size of the window.</summary>
    private RectTransform Card()
    {
        var rect = Child("Panel card", fxLayer);
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0.5f, 0.5f);
        float units = EcoTheme.Height > 0f ? Screen.height / EcoTheme.Height / canvas.scaleFactor : 1f;
        float h = active.kind == Kind.Settings ? 720f : active.kind == Kind.Slots ? 680f : active.kind == Kind.NewGame ? 720f : 640f;
        float w = active.kind == Kind.NewGame ? 980f : 620f;
        rect.sizeDelta = new Vector2(Mathf.Min(w * units, 1700f), Mathf.Min(h * units, 1000f));
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = EcoLeafArt.Frame(372, 420, 91); image.raycastTarget = false;
        rect.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        var label = Label(rect, active.label.text, 40, FontStyle.Bold, EcoLeafArt.Ink, TextAnchor.MiddleCenter);
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return rect;
    }

    private void Place(RectTransform rect, Vector2 screen, float scale, float spin)
    {
        rect.anchoredPosition = screen / canvas.scaleFactor;
        rect.localScale = Vector3.one * scale;
        rect.localRotation = Quaternion.Euler(0f, 0f, spin);
    }

    private static Vector2 ScreenOf(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);
    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float k) => Vector2.Lerp(Vector2.Lerp(a, b, k), Vector2.Lerp(b, c, k), k);
    private static float Smooth(float x) => x * x * (3f - 2f * x);

    private void OnDestroy() { if (current == this) current = null; }
}
