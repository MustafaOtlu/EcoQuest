using UnityEngine;

// Shared, resolution-independent UI. Text renders at the screen scale, never into a small texture.
public static class EcoTheme
{
    private static Texture2D backdrop, card, green, cardSkin, buttonSkin, buttonHover;
    private static GUIStyle title, body, muted, button, subtitle, cardStyle, thumb, windowSkin, panelSkin, fittedHeading;
    private static GUIStyle primary, noticePanel, noticeTitle, noticeBody;
    private static readonly GUIContent measuredText = new GUIContent();
    private static Matrix4x4 previous;
    public static float Width { get; private set; }
    public static float Height { get; private set; }
    public static GUIStyle Body { get { Init(); return body; } }
    public static GUIStyle Muted { get { Init(); return muted; } }
    public static GUIStyle Button { get { Init(); return button; } }
    public static GUIStyle PrimaryButton { get { Init(); return primary; } }
    public static GUIStyle Subtitle { get { Init(); return subtitle; } }
    public static GUIStyle Card { get { Init(); return cardStyle; } }
    public static void Begin()
    {
        Init(); previous=GUI.matrix;
        float scale=Mathf.Min(Screen.width / 1280f, Screen.height / 800f) * EcoSettings.UIScale;
        scale=Mathf.Max(.5f,scale); Width=Screen.width/scale; Height=Screen.height/scale;
        GUI.matrix=Matrix4x4.TRS(Vector3.zero, Quaternion.identity,Vector3.one*scale);
    }
    public static void End() { GUI.matrix=previous; GUI.color=Color.white; GUI.enabled=true; }
    public static Rect Window(float width, float height, string heading, string description)
    {
        Init();
        Line(new Rect(0, 0, Width, Height), EcoNotebook.Dim);
        width=Mathf.Min(width,Width-30); height=Mathf.Min(height,Height-30);
        var rect=new Rect((Width-width)*.5f,(Height-height)*.5f,width,height);
        GUI.Box(rect,GUIContent.none,windowSkin);
        Line(new Rect(rect.x + 19f, rect.y + 24f, 2f, 62f), EcoNotebook.Rule);
        Line(new Rect(rect.x + 32f, rect.y + 97f, rect.width - 64f, 1f), EcoNotebook.Rule);
        Heading(new Rect(rect.x+40,rect.y+29,rect.width-80,34),heading);
        Description(new Rect(rect.x+40,rect.y+64,rect.width-80,30),description);
        return rect;
    }
    /// <summary>Window with an icon beside the heading (icon names from Resources/EcoIcons).</summary>
    public static Rect Window(float width, float height, string heading, string description, string icon, float trailingHeaderSpace = 0f)
    {
        var rect = Window(width, height, "", "");
        Icon(new Rect(rect.x + 36, rect.y + 30, 34, 34), icon);
        Heading(new Rect(rect.x + 82, rect.y + 29, Mathf.Max(40f, rect.width - 122 - trailingHeaderSpace), 34), heading);
        Description(new Rect(rect.x + 82, rect.y + 64, rect.width - 122, 30), description);
        return rect;
    }

    public static bool CloseButton(Rect window)
    {
        var rect = new Rect(window.xMax - 66f, window.y + 30f, 30f, 30f);
        bool close = GUI.Button(rect, GUIContent.none, Button);
        Icon(new Rect(rect.x + 4f, rect.y + 4f, 22f, 22f), "ui_close");
        return close;
    }

    private static void Heading(Rect rect, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (fittedHeading == null) fittedHeading = new GUIStyle(title);
        measuredText.text = text;
        fittedHeading.fontSize = title.fontSize;
        while (fittedHeading.fontSize > 17 && fittedHeading.CalcSize(measuredText).x > rect.width)
            fittedHeading.fontSize--;
        GUI.Label(rect, measuredText, fittedHeading);
    }

    private static void Description(Rect rect, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        measuredText.text = text;
        int originalSize = muted.fontSize;
        while (muted.fontSize > 12 && muted.CalcHeight(measuredText, rect.width) > rect.height) muted.fontSize--;
        GUI.Label(rect, measuredText, muted);
        muted.fontSize = originalSize;
    }
    /// <summary>A paper card for finale panels and notices, with an optional continuous margin rule.</summary>
    public static void Panel(Rect rect, bool stitch = true)
    {
        Init();
        GUI.color=new Color(0,0,0,.16f); GUI.Box(new Rect(rect.x+1,rect.y+3,rect.width,rect.height),GUIContent.none,panelSkin); GUI.color=Color.white;
        GUI.Box(rect,GUIContent.none,panelSkin);
        if (stitch) StitchedEdge(new Rect(rect.x+5,rect.y+5,rect.width-10,rect.height-10));
    }
    /// <summary>Legacy entry point: draws a quiet, continuous margin line instead of a stitched border.</summary>
    public static void StitchedEdge(Rect r)
    {
        if (r.width <= 16f || r.height <= 0f) return;
        var rule = EcoNotebook.Rule; rule.a = .65f;
        Line(new Rect(r.x+8f,r.yMax-1f,r.width-16f,1f),rule);
    }
    private static void Line(Rect r, Color c) { var prev=GUI.color; GUI.color=c; GUI.DrawTexture(r,Texture2D.whiteTexture); GUI.color=prev; }
    /// <summary>Draws a hand-drawn icon; does nothing when the icon is missing.</summary>
    public static void Icon(Rect rect, string icon)
    {
        var sprite = EcoIcons.Get(icon);
        if (sprite != null) GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
    }
    /// <summary>A themed button with an icon on its left.</summary>
    private static GUIStyle iconButtonLabel, iconButtonSource;
    public static bool IconButton(Rect rect, string text, string icon, GUIStyle style = null)
    {
        Init();
        style = style ?? button;
        // Padding the label with spaces let centred text slide under the icon on narrow buttons; keep them in separate lanes.
        bool pressed = GUI.Button(rect, GUIContent.none, style);
        float s = Mathf.Min(23f, rect.height - 10f);
        Icon(new Rect(rect.x + 12f, rect.y + (rect.height - s) * .5f, s, s), icon);
        if (iconButtonLabel == null || iconButtonSource != style)
        {
            iconButtonSource = style;
            // Built from a plain label: copying the button style and clearing its backgrounds also stripped the button skin.
            iconButtonLabel = new GUIStyle(GUI.skin.label)
            {
                font = style.font, fontSize = style.fontSize, fontStyle = style.fontStyle, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Clip,
            };
            TextColors(iconButtonLabel, style.normal.textColor); // a hovered label otherwise takes the skin's pale hover colour
        }
        float textX = rect.x + 12 + s + 6;
        var labelRect = new Rect(textX, rect.y, rect.xMax - textX - 8, rect.height);
        measuredText.text = text;
        iconButtonLabel.fontSize = style.fontSize;
        while (iconButtonLabel.fontSize > 13 && iconButtonLabel.CalcSize(measuredText).x > labelRect.width)
            iconButtonLabel.fontSize--;
        var color = style.normal.textColor;
        if (!GUI.enabled) color.a *= .62f;
        TextColors(iconButtonLabel, color);
        GUI.Label(labelRect, measuredText, iconButtonLabel);
        return pressed;
    }
    /// <summary>Themed 0..1 slider: pale sage track, forest fill and a clear handle.</summary>
    public static float Slider(Rect rect, float value)
    {
        Init();
        var track = new Rect(rect.x, rect.y + rect.height * .5f - 3f, rect.width, 6f);
        GUI.DrawTexture(track, card);
        GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(value), track.height), green);
        return GUI.HorizontalSlider(rect, value, 0f, 1f, GUIStyle.none, thumb);
    }
    private static Texture2D Swatch(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static Texture2D Skin(Color fill, Color edge)
    {
        const int size = 24;
        const float radius = 6f;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius), 0f);
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius), 0f);
                float boundary = radius - Mathf.Sqrt(dx * dx + dy * dy);
                Color pixel = Color.Lerp(edge, fill, Mathf.Clamp01(boundary - .55f));
                pixel.a *= Mathf.Clamp01(boundary + .5f);
                pixels[y * size + x] = pixel;
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static RectOffset Slice() => new RectOffset(7, 7, 7, 7);

    internal static void DrawLearningNote(Rect rect, string heading, string message)
    {
        Init();
        float night = EcoHudStyle.NightTarget(EcoWorldClock.Current);
        var surface = EcoHudStyle.ColorFor(EcoHudStyle.Tone.Surface, night);
        var text = EcoHudStyle.ColorFor(EcoHudStyle.Tone.Text, night);
        var secondary = EcoHudStyle.ColorFor(EcoHudStyle.Tone.Muted, night);
        var previousColor = GUI.color;
        GUI.color = surface;
        GUI.Box(rect, GUIContent.none, noticePanel);
        GUI.color = previousColor;
        TextColors(noticeTitle, text);
        TextColors(noticeBody, secondary);
        var content = new Rect(rect.x + 48f, rect.y + 13f, rect.width - 91f, rect.height - 26f);
        float headingHeight = noticeTitle.CalcHeight(new GUIContent(heading), content.width);
        Icon(new Rect(rect.x + 14f, rect.y + 14f, 24f, 24f), "yep");
        GUI.Label(new Rect(content.x, content.y, content.width, headingHeight), heading, noticeTitle);
        GUI.Label(new Rect(content.x, content.y + headingHeight + 5f, content.width, content.height - headingHeight - 5f), message, noticeBody);
    }

    internal static float LearningNoteHeight(float width, string heading, string message)
    {
        Init();
        width -= 91f;
        return 31f + noticeTitle.CalcHeight(new GUIContent(heading), width) + noticeBody.CalcHeight(new GUIContent(message), width);
    }
    private static void TextColors(GUIStyle style, Color color)
    {
        style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = color;
        style.onNormal.textColor = style.onHover.textColor = style.onFocused.textColor = style.onActive.textColor = color;
    }
    private static void Init()
    {
        if(backdrop!=null)return;
        backdrop = Skin(EcoNotebook.Paper, EcoNotebook.Rule);
        cardSkin = Skin(EcoNotebook.Sheet, EcoNotebook.Rule);
        buttonSkin = Skin(EcoNotebook.Inset, EcoNotebook.Rule);
        buttonHover = Skin(EcoNotebook.Selected, EcoNotebook.Accent);
        card = Swatch(EcoNotebook.Rule); green = Swatch(EcoNotebook.Accent);
        windowSkin = new GUIStyle { normal = { background = backdrop }, border = Slice() };
        panelSkin = new GUIStyle { normal = { background = cardSkin, textColor = EcoNotebook.Ink }, border = Slice() };
        var font = EcoUI.Font;
        title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = false,
            clipping = TextClipping.Clip, padding = new RectOffset(), margin = new RectOffset() };
        body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true, clipping = TextClipping.Clip };
        muted = new GUIStyle(body) { fontSize = 16 };
        subtitle = new GUIStyle(body) { fontSize = 20, fontStyle = FontStyle.Bold };
        TextColors(title, EcoNotebook.Ink); TextColors(body, EcoNotebook.Ink);
        TextColors(muted, EcoNotebook.Muted); TextColors(subtitle, EcoNotebook.Accent);
        button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17, padding = new RectOffset(12, 12, 4, 4), border = Slice() };
        ButtonStates(button, buttonSkin, buttonHover, EcoNotebook.Ink);
        primary = new GUIStyle(button) { fontStyle = FontStyle.Bold };
        ButtonStates(primary, Skin(EcoNotebook.Accent, EcoNotebook.Accent), Skin(new Color32(63, 105, 83, 255), EcoNotebook.Accent), EcoNotebook.Sheet);
        cardStyle = new GUIStyle(panelSkin) { padding = new RectOffset(16, 16, 14, 14) };
        noticePanel = new GUIStyle { normal = { background = Skin(Color.white, new Color(.8f, .9f, .85f)) }, border = Slice() };
        noticeTitle = new GUIStyle(subtitle) { fontSize = 16, padding = new RectOffset() };
        noticeBody = new GUIStyle(muted) { fontSize = 14, padding = new RectOffset() };
        thumb=new GUIStyle{fixedWidth=14,fixedHeight=18,normal={background=green},hover={background=green},active={background=green}};
    }

    private static void ButtonStates(GUIStyle style, Texture2D normal, Texture2D highlighted, Color color)
    {
        style.normal.background = style.onNormal.background = normal;
        style.hover.background = style.focused.background = style.active.background = highlighted;
        style.onHover.background = style.onFocused.background = style.onActive.background = highlighted;
        TextColors(style, color);
    }
}
