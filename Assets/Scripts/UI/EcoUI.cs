using System;
using System.Collections.Generic;
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
    public static readonly Color Surface = new Color(0.957f, 0.937f, 0.875f);
    public static readonly Color SurfaceHeader = new Color(0.91f, 0.92f, 0.84f);
    public static readonly Color SurfaceRaised = new Color(0.91f, 0.91f, 0.843f);
    public static readonly Color Ink = new Color(0.149f, 0.231f, 0.184f);
    public static readonly Color PaperInk = new Color(0.98f, 0.97f, 0.91f);
    public static readonly Color Muted = new Color(0.36f, 0.42f, 0.35f);
    public static readonly Color Accent = new Color(0.18f, 0.396f, 0.267f);
    public static readonly Color Amber = new Color(0.64f, 0.43f, 0.10f);
    public static readonly Color Sky = new Color(0.20f, 0.44f, 0.53f);
    public static readonly Color Coral = new Color(0.66f, 0.28f, 0.20f);
    public static readonly Color Track = new Color(0.76f, 0.80f, 0.69f);
    public static readonly Color Selected = new Color(0.184f, 0.345f, 0.259f);

    private static Font font;
    private static Sprite rounded, circle;
    private static readonly List<RectTransform> worldLabelObstacles = new List<RectTransform>();
    private static readonly Vector3[] obstacleCorners = new Vector3[4];
    public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { tooltip = null; tooltipText = null; tooltipOwner = null; hudMode = -1; worldLabelObstacles.Clear(); }

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
    public static bool Collapsed(string card) => PlayerPrefs.GetInt("EcoQuest.card." + card,
        card == "environment" || card == "resources" ? 1 : 0) == 1;
    public static void SetCollapsed(string card, bool value) { PlayerPrefs.SetInt("EcoQuest.card." + card, value ? 1 : 0); PlayerPrefs.Save(); LayoutChanged?.Invoke(); }

    /// <summary>Canvas scale shared by every HUD layer (resolution fit × the player's UI scale setting).</summary>
    public static float Scale(Canvas canvas)
    {
        float w = Mathf.Max(1f, canvas.pixelRect.width), h = Mathf.Max(1f, canvas.pixelRect.height);
        float requested = Mathf.Clamp(Mathf.Min(w / 1600f, h / 900f), 0.75f, 1.5f) * EcoSettings.UIScale;
        float maximumFit = Mathf.Min(w / 1000f, h / 620f);
        return Mathf.Max(0.5f, Mathf.Min(requested, maximumFit));
    }

    /// <summary>Reserve an individual HUD panel so map captions can avoid its visible screen area.</summary>
    public static void RegisterWorldLabelObstacle(RectTransform rect)
    {
        if (rect != null && !worldLabelObstacles.Contains(rect)) worldLabelObstacles.Add(rect);
    }

    /// <summary>Append visible panel bounds in screen pixels, without reserving their entire canvas.</summary>
    public static void AppendWorldLabelObstacles(List<Rect> output)
    {
        if (output == null) return;
        for (int i = worldLabelObstacles.Count - 1; i >= 0; i--)
        {
            var panel = worldLabelObstacles[i];
            if (panel == null) { worldLabelObstacles.RemoveAt(i); continue; }
            if (!panel.gameObject.activeInHierarchy) continue;
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas == null || !canvas.isActiveAndEnabled) continue;
            var root = canvas.rootCanvas;
            if (root == null || !root.isActiveAndEnabled || root.transform == panel) continue;
            bool visible = true;
            float alpha = 1f;
            for (Transform ancestor = panel; ancestor != null; ancestor = ancestor.parent)
            {
                var group = ancestor.GetComponent<CanvasGroup>();
                if (group != null && group.isActiveAndEnabled) alpha *= group.alpha;
                if (alpha < 0.01f) { visible = false; break; }
            }
            if (!visible) continue;
            var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            if (camera == null && root.renderMode != RenderMode.ScreenSpaceOverlay) camera = Camera.main;
            panel.GetWorldCorners(obstacleCorners);
            float left = float.PositiveInfinity, bottom = float.PositiveInfinity;
            float right = float.NegativeInfinity, top = float.NegativeInfinity;
            bool inFront = camera == null;
            for (int corner = 0; corner < obstacleCorners.Length; corner++)
            {
                Vector3 screen = camera != null ? camera.WorldToScreenPoint(obstacleCorners[corner])
                    : (Vector3)RectTransformUtility.WorldToScreenPoint(null, obstacleCorners[corner]);
                if (camera != null && screen.z > camera.nearClipPlane) inFront = true;
                left = Mathf.Min(left, screen.x); right = Mathf.Max(right, screen.x);
                bottom = Mathf.Min(bottom, screen.y); top = Mathf.Max(top, screen.y);
            }
            if (!inFront) continue;
            Rect viewport = root.pixelRect;
            left = Mathf.Max(left, viewport.xMin); right = Mathf.Min(right, viewport.xMax);
            bottom = Mathf.Max(bottom, viewport.yMin); top = Mathf.Min(top, viewport.yMax);
            if (right > left && top > bottom) output.Add(UnityEngine.Rect.MinMaxRect(left, bottom, right, top));
        }
    }

    /// <summary>Owner token for HUD-made panels (controls sheet) so they do not count as foreign modal windows.</summary>
    public sealed class HudPanel { }

    /// <summary>True while a full-screen IMGUI window (menu, journal, skills, orders, final, sound) is open: the uGUI
    /// HUD hides so it never draws over those windows.</summary>
    public static bool ForeignWindowOpen
    {
        get
        {
            if (PlayerController.Paused || EcoAudio.AnyPanelOpen) return true;
            var panel = EcoUIFlow.ActivePanel;
            return panel != null && !(panel is EcoWorldHUD) && !(panel is HudPanel);
        }
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

    // ------------------------------------------------------------------ field notes skin
    // Kenney UI Pack Adventure (CC0), with editable SVG sources in Assets/Art/UI/Adventure.
    // Both uGUI and IMGUI share the illustrated wood, metal and paper frames.
    private static Texture2D paperTexture, cardTexture, buttonTexture, buttonHoverTexture, selectedTexture, borderTexture;
    private static Sprite paperSprite, cardSprite, selectedSprite, borderSprite;
    private static Texture2D portraitTexture, portraitFrameTexture, mapFrameTexture;
    private static Sprite portraitSprite, portraitFrameSprite, mapFrameSprite;
    public static Texture2D PanelTexture => SkinTexture(ref paperTexture, "panel_brown_corners_a");
    public static Texture2D CardTexture => SkinTexture(ref cardTexture, "panel_brown");
    public static Texture2D ButtonTexture => SkinTexture(ref buttonTexture, "button_brown");
    public static Texture2D ButtonHoverTexture => SkinTexture(ref buttonHoverTexture, "button_grey");
    public static Texture2D SelectedTexture => SkinTexture(ref selectedTexture, "panel_brown_dark");
    public static Sprite PaperSprite => SkinSprite(ref paperSprite, PanelTexture);
    public static Sprite CardSprite => SkinSprite(ref cardSprite, CardTexture);
    public static Sprite SelectedSprite => SkinSprite(ref selectedSprite, SelectedTexture);
    public static Sprite ToolSlotSprite => CardSprite;
    public static Sprite ActiveToolSlotSprite => SelectedSprite;
    public static Sprite PortraitSprite => NaturalSprite(ref portraitSprite, ref portraitTexture, "hero_portrait");
    public static Sprite PortraitFrameSprite => NaturalSprite(ref portraitFrameSprite, ref portraitFrameTexture, "minimap_ring_brown");
    public static Sprite MapFrameSprite => SkinSprite(ref mapFrameSprite, SkinTexture(ref mapFrameTexture, "panel_border_brown_detail"));
    // Keep the old accessors for existing scene consumers; they now return paper and a fine keyline.
    public static Sprite Grain => PaperSprite;
    public static Sprite Trim => SkinSprite(ref borderSprite, SkinTexture(ref borderTexture, "panel_border_brown_detail"));
    public static readonly Color StitchColor = new Color(0.40f, 0.49f, 0.35f, 0.32f);

    private static Texture2D SkinTexture(ref Texture2D cached, string name)
    {
        if (cached == null) cached = Resources.Load<Texture2D>("EcoUI/Adventure/" + name);
        return cached != null ? cached : Texture2D.whiteTexture;
    }

    private static Sprite SkinSprite(ref Sprite cached, Texture2D texture)
    {
        if (texture.width < 32 || texture.height < 32) return Rounded;
        if (cached == null) cached = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 200f, 0, SpriteMeshType.FullRect, new Vector4(24, 24, 24, 24));
        return cached;
    }

    private static Sprite NaturalSprite(ref Sprite sprite, ref Texture2D texture, string name)
    {
        if (sprite != null) return sprite;
        if (texture == null) texture = Resources.Load<Texture2D>("EcoUI/Adventure/" + name);
        if (texture == null) return null;
        return sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>Applies the paper export and a quiet shadow without adding decorative UI over the content.</summary>
    public static Image Dress(Image image, bool stitch = true)
    {
        if (image == null) return image;
        Color tint = image.color;
        bool inset = Mathf.Abs(tint.r - SurfaceRaised.r) + Mathf.Abs(tint.g - SurfaceRaised.g) + Mathf.Abs(tint.b - SurfaceRaised.b) < 0.03f;
        image.sprite = inset ? CardSprite : PaperSprite; image.type = Image.Type.Sliced;
        image.color = Color.white;
        if (!image.TryGetComponent<Shadow>(out var shadow)) shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.13f, 0.08f, 0.18f); shadow.effectDistance = new Vector2(0f, -2f);
        var oldTrim = image.transform.Find("Trim");
        if (oldTrim != null) oldTrim.gameObject.SetActive(false);
        return image;
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
        colors.colorMultiplier = 1f; button.colors = colors; button.targetGraphic = bg;
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
        public readonly Image background, icon, chevron, rule;
        public readonly Text title, summary;
        public readonly Button toggle;
        public bool IsCollapsed => Collapsed(key);
        public float Width { get; private set; }

        public Card(string key, Transform parent, Vector2 anchor, Vector2 pivot, string iconName, string heading, string tip)
        {
            this.key = key;
            background = Dress(Panel("Card " + key, parent, anchor, pivot, Surface));
            root = background.rectTransform;
            rule = Panel("Header rule", root, new Vector2(0, 1), new Vector2(0, 1), new Color(StitchColor.r, StitchColor.g, StitchColor.b, 0.22f), false);
            rule.sprite = null; rule.type = Image.Type.Simple;
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
            bool showSummary = collapsed || showSummaryWhenOpen;
            // With a summary the title takes only its own width and the summary starts right after it (no overlap).
            float titleWidth = showSummary ? Mathf.Min(width * 0.55f, title.preferredWidth / RasterScale + 6f) : width - 70f;
            EcoUI.Layout(title, new Vector2(38f, -8f), new Vector2(titleWidth, 18f));
            float summaryX = 38f + titleWidth + 8f;
            EcoUI.Layout(summary, new Vector2(summaryX, -8f), new Vector2(Mathf.Max(20f, width - summaryX - 36f), 18f));
            summary.resizeTextForBestFit = true; summary.resizeTextMaxSize = 13 * RasterScale; summary.resizeTextMinSize = 9 * RasterScale;
            summary.gameObject.SetActive(showSummary);
            var tr = (RectTransform)toggle.transform; tr.anchorMin = tr.anchorMax = new Vector2(1, 1); tr.pivot = new Vector2(1, 1);
            tr.anchoredPosition = new Vector2(-6f, -6f);
            SetIcon(chevron, collapsed ? "ui_expand" : "ui_collapse");
            Place(body, new Vector2(0f, -HeaderHeight), new Vector2(width, bodyHeight));
            Place(rule.rectTransform, new Vector2(12f, -HeaderHeight + 1f), new Vector2(width - 24f, 1f));
            rule.gameObject.SetActive(!collapsed);
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
            var panel = Dress(Panel("Tooltip", canvas.transform, Vector2.zero, new Vector2(0, 1), Surface, false), false);
            tooltip = panel.rectTransform;
            tooltipText = Label("Text", tooltip, "", 14, TextAnchor.UpperLeft, Ink);
            var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(0.24f, 0.37f, 0.22f, 0.12f); outline.effectDistance = new Vector2(1, -1);
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
    public static Color OnPaper(Color semantic) => Color.Lerp(semantic, Ink, 0.6f);
}
