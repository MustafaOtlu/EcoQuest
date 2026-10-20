using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Shared building blocks for the uGUI HUD (EcoQuestHUD, EcoWorldHUD): one palette, rounded panels, icon rows,
// meters, clickable icon buttons, a mouse tooltip and the persisted layout state (HUD mode + collapsed cards).
// Text is rasterised at 2× and scaled down so small labels stay crisp with the built-in font (Turkish glyphs).
public static class EcoUI
{
    public const int RasterScale = 2;
    public static readonly Color Surface = new Color(0.035f, 0.075f, 0.09f, 0.92f);
    public static readonly Color SurfaceHeader = new Color(0.06f, 0.13f, 0.15f, 0.96f);
    public static readonly Color SurfaceRaised = new Color(0.09f, 0.17f, 0.19f, 0.96f);
    public static readonly Color Ink = new Color(0.94f, 0.97f, 0.95f);
    public static readonly Color Muted = new Color(0.67f, 0.77f, 0.77f);
    public static readonly Color Accent = new Color(0.43f, 0.91f, 0.66f);
    public static readonly Color Amber = new Color(1f, 0.79f, 0.31f);
    public static readonly Color Sky = new Color(0.36f, 0.77f, 1f);
    public static readonly Color Coral = new Color(0.94f, 0.42f, 0.36f);
    public static readonly Color Track = new Color(0.15f, 0.22f, 0.24f, 1f);
    public static readonly Color Selected = new Color(0.10f, 0.42f, 0.30f, 0.98f);

    private static Font font;
    private static Sprite rounded, circle;
    public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { tooltip = null; tooltipText = null; tooltipOwner = null; hudMode = -1; }

    // ------------------------------------------------------------------ layout state

    public enum HudMode { Full, Compact, Hidden }
    private static int hudMode = -1;
    public static event Action LayoutChanged;
    public static HudMode Mode
    {
        get { if (hudMode < 0) hudMode = Mathf.Clamp(PlayerPrefs.GetInt("EcoQuest.hudMode", 0), 0, 2); return (HudMode)hudMode; }
        set { hudMode = (int)value; PlayerPrefs.SetInt("EcoQuest.hudMode", hudMode); PlayerPrefs.Save(); LayoutChanged?.Invoke(); }
    }
    public static string ModeName => Mode == HudMode.Full ? "Tam" : Mode == HudMode.Compact ? "Sade" : "Gizli";
    public static void CycleMode() => Mode = (HudMode)(((int)Mode + 1) % 3);
    public static bool Collapsed(string card) => PlayerPrefs.GetInt("EcoQuest.card." + card, 0) == 1;
    public static void SetCollapsed(string card, bool value) { PlayerPrefs.SetInt("EcoQuest.card." + card, value ? 1 : 0); PlayerPrefs.Save(); LayoutChanged?.Invoke(); }

    /// <summary>Canvas scale shared by every HUD layer (resolution fit × the player's UI scale setting).</summary>
    public static float Scale(Canvas canvas)
    {
        float w = Mathf.Max(1f, canvas.pixelRect.width), h = Mathf.Max(1f, canvas.pixelRect.height);
        return Mathf.Clamp(Mathf.Min(w / 1600f, h / 900f), 0.75f, 1.5f) * EcoSettings.UIScale;
    }

    // ------------------------------------------------------------------ canvas

    public static Canvas CreateCanvas(string name, int order, out CanvasScaler scaler)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)); root.layer = 5;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order; canvas.pixelPerfect = false;
        scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("Eco UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ------------------------------------------------------------------ sprites

    public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(48, 12);
    public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32);

    private static Sprite MakeRounded(int s, int r)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "EcoUI rounded " + r };
        var pixels = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                pixels[y * s + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d + 0.5f) * 255f));
            }
        tex.SetPixels32(pixels); tex.Apply();
        float border = Mathf.Min(r, s / 2 - 1);
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    // ------------------------------------------------------------------ elements

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        return rect;
    }

    public static void Place(RectTransform rect, Vector2 position, Vector2 size) { rect.anchoredPosition = position; rect.sizeDelta = size; }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
    }

    /// <summary>Rounded panel. Panels block clicks so tool use never fires through the HUD.</summary>
    public static Image Panel(string name, Transform parent, Vector2 anchor, Vector2 pivot, Color color, bool blocks = true, Sprite sprite = null)
    {
        var rect = Rect(name, parent, anchor, pivot);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite != null ? sprite : Rounded; image.type = Image.Type.Sliced; image.color = color; image.raycastTarget = blocks;
        return image;
    }

    public static Image Fill(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1));
        var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
        return image;
    }

    public static Image Icon(string name, Transform parent, string icon, Vector2 anchor, Vector2 pivot, float size)
    {
        var rect = Rect(name, parent, anchor, pivot); rect.sizeDelta = new Vector2(size, size);
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = EcoIcons.Get(icon); image.preserveAspect = true; image.raycastTarget = false;
        image.enabled = image.sprite != null;
        return image;
    }

    public static void SetIcon(Image image, string icon)
    {
        if (image == null) return;
        var sprite = EcoIcons.Get(icon);
        if (image.sprite != sprite) image.sprite = sprite;
        image.enabled = sprite != null;
    }

    public static Text Label(string name, Transform parent, string content, int fontSize, TextAnchor align, Color color, bool bold = false)
    {
        var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1));
        var text = rect.gameObject.AddComponent<Text>();
        text.font = Font; text.fontSize = fontSize * RasterScale; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color; text.alignment = align; text.text = content; text.raycastTarget = false; text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        text.lineSpacing = 0.95f;
        return text;
    }

    /// <summary>Positions a label in panel space (top-left anchored) at the 2× raster.</summary>
    public static void Layout(Text text, Vector2 position, Vector2 size)
    {
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = size * RasterScale; rect.localScale = Vector3.one / RasterScale;
    }

    public static void LayoutIcon(Image icon, Vector2 position, float size)
    {
        var rect = icon.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(size, size);
    }

    /// <summary>A thin meter: track + fill. Returns the fill (its width is set by SetMeter).</summary>
    public static Image Meter(string name, Transform parent, out RectTransform track)
    {
        var t = Panel(name, parent, new Vector2(0, 1), new Vector2(0, 1), Track, false, Circle); t.type = Image.Type.Sliced;
        track = t.rectTransform;
        var fill = Panel("Fill", track, new Vector2(0, 1), new Vector2(0, 1), Accent, false, Circle); fill.type = Image.Type.Sliced;
        return fill;
    }

    public static void SetMeter(Image fill, RectTransform track, float value01, Color color)
    {
        float w = track.sizeDelta.x * Mathf.Clamp01(value01);
        fill.rectTransform.sizeDelta = new Vector2(w < 0.5f ? 0f : Mathf.Max(track.sizeDelta.y, w), track.sizeDelta.y);
        fill.color = color; fill.enabled = w >= 0.5f;
    }

    /// <summary>Round icon button with an optional key caption and hover tooltip.</summary>
    public static Button IconButton(string name, Transform parent, string icon, float size, Action onClick, string tip = null)
    {
        var bg = Panel(name, parent, new Vector2(0, 1), new Vector2(0, 1), SurfaceRaised, true, Circle);
        bg.rectTransform.sizeDelta = new Vector2(size, size);
        var button = bg.gameObject.AddComponent<Button>();
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f); colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.colorMultiplier = 1.2f; button.colors = colors; button.targetGraphic = bg;
        button.onClick.AddListener(() => onClick?.Invoke());
        var image = Icon("Icon", bg.transform, icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size * 0.68f);
        image.rectTransform.anchoredPosition = Vector2.zero;
        if (!string.IsNullOrEmpty(tip)) AddTooltip(bg.gameObject, tip);
        return button;
    }

    // ------------------------------------------------------------------ tooltip

    private static RectTransform tooltip;
    private static Text tooltipText;
    private static object tooltipOwner;

    private sealed class TooltipSource : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string text;
        public void OnPointerEnter(PointerEventData eventData) => ShowTooltip(this, text);
        public void OnPointerExit(PointerEventData eventData) => HideTooltip(this);
        private void OnDisable() => HideTooltip(this);
    }

    public static void AddTooltip(GameObject target, string text)
    {
        var source = target.GetComponent<TooltipSource>();
        if (source == null) source = target.AddComponent<TooltipSource>();
        source.text = text;
    }

    // ------------------------------------------------------------------ collapsible card

    /// <summary>A HUD card with a header (icon, title, summary shown while collapsed, chevron button) and a body.
    /// Collapsing is remembered per card in PlayerPrefs.</summary>
    public sealed class Card
    {
        public const float HeaderHeight = 34f;
        public readonly string key;
        public readonly RectTransform root, body;
        public readonly Image background, icon, chevron;
        public readonly Text title, summary;
        public readonly Button toggle;
        public bool IsCollapsed => Collapsed(key);
        public float Width { get; private set; }

        public Card(string key, Transform parent, Vector2 anchor, Vector2 pivot, string iconName, string heading, string tip)
        {
            this.key = key;
            background = Panel("Card " + key, parent, anchor, pivot, Surface);
            root = background.rectTransform;
            icon = Icon("Icon", root, iconName, new Vector2(0, 1), new Vector2(0, 1), 22f);
            title = Label("Title", root, heading, 13, TextAnchor.MiddleLeft, Accent, true);
            summary = Label("Summary", root, "", 13, TextAnchor.MiddleRight, Ink, true);
            toggle = IconButton("Collapse", root, "ui_collapse", 22f, () => SetCollapsed(this.key, !Collapsed(this.key)), tip);
            chevron = toggle.transform.Find("Icon").GetComponent<Image>();
            body = Rect("Body", root, new Vector2(0, 1), new Vector2(0, 1));
        }

        /// <summary>Lays the card out; returns its total height.</summary>
        public float Layout(Vector2 position, float width, float bodyHeight, bool showSummaryWhenOpen = false)
        {
            Width = width;
            bool collapsed = IsCollapsed;
            float height = HeaderHeight + (collapsed ? 0f : bodyHeight);
            Place(root, position, new Vector2(width, height));
            LayoutIcon(icon, new Vector2(10f, -6f), 22f);
            EcoUI.Layout(title, new Vector2(38f, -8f), new Vector2(width * 0.55f, 18f));
            EcoUI.Layout(summary, new Vector2(width * 0.4f - 32f, -8f), new Vector2(width * 0.6f - 8f, 18f));
            summary.gameObject.SetActive(collapsed || showSummaryWhenOpen);
            var tr = (RectTransform)toggle.transform; tr.anchorMin = tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(1, 1);
            tr.anchoredPosition = new Vector2(-6f, -6f);
            SetIcon(chevron, collapsed ? "ui_expand" : "ui_collapse");
            Place(body, new Vector2(0f, -HeaderHeight), new Vector2(width, bodyHeight));
            body.gameObject.SetActive(!collapsed);
            return height;
        }

        public void SetVisible(bool visible) { if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible); }
    }

    public static void ShowTooltip(object owner, string text)
    {
        if (string.IsNullOrEmpty(text)) { HideTooltip(owner); return; }
        if (tooltip == null)
        {
            var canvas = CreateCanvas("EcoUI Tooltip", 60, out var scaler);
            canvas.gameObject.AddComponent<TooltipFollower>();
            var panel = Panel("Tooltip", canvas.transform, Vector2.zero, new Vector2(0, 1), new Color(0.02f, 0.05f, 0.06f, 0.97f), false);
            tooltip = panel.rectTransform;
            tooltipText = Label("Text", tooltip, "", 14, TextAnchor.UpperLeft, Ink);
            var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.43f, 0.91f, 0.66f, 0.35f); outline.effectDistance = new Vector2(1, -1);
        }
        tooltipOwner = owner;
        tooltipText.text = text;
        float width = Mathf.Clamp(text.Length * 7.2f + 28f, 120f, 300f);
        int lines = Mathf.CeilToInt(text.Length * 7.2f / (width - 28f)) + text.Split('\n').Length - 1;
        float height = 16f + Mathf.Max(1, lines) * 18f;
        tooltip.sizeDelta = new Vector2(width, height);
        Layout(tooltipText, new Vector2(12, -8), new Vector2(width - 24f, height - 12f));
        tooltip.gameObject.SetActive(true);
    }

    public static void HideTooltip(object owner)
    {
        if (tooltip == null || (owner != null && !ReferenceEquals(owner, tooltipOwner))) return;
        tooltip.gameObject.SetActive(false); tooltipOwner = null;
    }

    private sealed class TooltipFollower : MonoBehaviour
    {
        private Canvas canvas;
        private void Awake() => canvas = GetComponent<Canvas>();
        private void LateUpdate()
        {
            if (tooltip == null || !tooltip.gameObject.activeSelf || Mouse.current == null) return;
            float scale = Scale(canvas); canvas.scaleFactor = scale;
            Vector2 mouse = Mouse.current.position.ReadValue() / scale;
            float w = canvas.pixelRect.width / scale, h = canvas.pixelRect.height / scale;
            var size = tooltip.sizeDelta;
            float x = Mathf.Min(mouse.x + 18f, w - size.x - 6f), y = Mathf.Max(mouse.y - 14f, size.y + 6f);
            if (x < mouse.x) x = mouse.x - size.x - 14f;
            tooltip.anchoredPosition = new Vector2(x, y);
        }
    }

    // ------------------------------------------------------------------ colours

    public static Color GoodColor(float g)
    {
        g = Mathf.Clamp01(g);
        bool cb = EcoSettings.ColorBlind;
        var bad = cb ? new Color(0.84f, 0.37f, 0f) : new Color(0.94f, 0.36f, 0.31f);
        var mid = cb ? new Color(0.94f, 0.89f, 0.26f) : new Color(1f, 0.79f, 0.31f);
        var fine = cb ? new Color(0f, 0.45f, 0.70f) : new Color(0.34f, 0.86f, 0.59f);
        return g < 0.5f ? Color.Lerp(bad, mid, g * 2f) : Color.Lerp(mid, fine, (g - 0.5f) * 2f);
    }

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
}
