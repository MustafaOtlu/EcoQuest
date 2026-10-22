using UnityEngine;

// Shared, resolution-independent UI. Text renders at the screen scale, never into a small texture.
public static class EcoTheme
{
    private static Texture2D backdrop, card, green, cardSkin, buttonSkin, buttonHover;
    private static GUIStyle title, body, muted, button, subtitle, cardStyle, thumb, windowSkin, panelSkin, fittedHeading;
    private static readonly GUIContent measuredText = new GUIContent();
    private static Matrix4x4 previous;
    public static float Width { get; private set; }
    public static float Height { get; private set; }
    public static GUIStyle Body { get { Init(); return body; } }
    public static GUIStyle Muted { get { Init(); return muted; } }
    public static GUIStyle Button { get { Init(); return button; } }
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
        GUI.color=new Color(.075f,.12f,.09f,.48f); GUI.DrawTexture(new Rect(0,0,Width,Height),Texture2D.whiteTexture); GUI.color=Color.white;
        width=Mathf.Min(width,Width-30); height=Mathf.Min(height,Height-30);
        var rect=new Rect((Width-width)*.5f,(Height-height)*.5f,width,height);
        // Paper, a small chapter mark and a fine rule keep the notebook layout quiet around the content.
        GUI.color=new Color(0,0,0,.18f); GUI.Box(new Rect(rect.x+2,rect.y+5,rect.width,rect.height),GUIContent.none,windowSkin); GUI.color=Color.white;
        GUI.Box(rect,GUIContent.none,windowSkin);
        var rule = EcoUI.Muted; rule.a = .22f;
        Line(new Rect(rect.x+40,rect.y+97,rect.width-80,1f),rule);
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
        var rule = EcoUI.Muted; rule.a = .16f;
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
        float s = Mathf.Min(26f, rect.height - 10f);
        Icon(new Rect(rect.x + 12, rect.y + (rect.height - s) * .5f, s, s), icon);
        if (iconButtonLabel == null || iconButtonSource != style)
        {
            iconButtonSource = style;
            // Built from a plain label: copying the button style and clearing its backgrounds also stripped the button skin.
            iconButtonLabel = new GUIStyle(GUI.skin.label)
            {
                font = style.font, fontSize = style.fontSize, fontStyle = style.fontStyle, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Clip,
            };
            iconButtonLabel.normal.textColor = style.normal.textColor;
        }
        float textX = rect.x + 12 + s + 6;
        GUI.Label(new Rect(textX, rect.y, rect.xMax - textX - 8, rect.height), text, iconButtonLabel);
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
    private static Texture2D Swatch(Color color) { var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t; }
    private static void TextColors(GUIStyle style, Color color)
    {
        style.normal.textColor = style.hover.textColor = style.focused.textColor = style.active.textColor = color;
        style.onNormal.textColor = style.onHover.textColor = style.onFocused.textColor = style.onActive.textColor = color;
    }
    private static void Init()
    {
        if(backdrop!=null)return;
        backdrop=EcoUI.PanelTexture;
        card=Swatch(EcoUI.Track); green=Swatch(EcoUI.Accent);
        cardSkin=EcoUI.CardTexture;
        buttonSkin=EcoUI.ButtonTexture;
        buttonHover=EcoUI.ButtonHoverTexture;
        windowSkin=new GUIStyle{normal={background=backdrop},border=new RectOffset(24,24,24,24)};
        // The cream centre begins16px inside the128px source. Cutting the slice at8px stretches
        // the remaining brown trim across wide cards and puts it underneath otherwise inset text.
        panelSkin=new GUIStyle{normal={background=cardSkin,textColor=EcoUI.Ink},border=new RectOffset(16,16,16,16)};
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title=new GUIStyle(GUI.skin.label){font=font,fontSize=28,fontStyle=FontStyle.Bold,wordWrap=false,clipping=TextClipping.Clip,padding=new RectOffset(),margin=new RectOffset()}; title.normal.textColor=EcoUI.Ink;
        body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true,clipping=TextClipping.Clip};body.normal.textColor=EcoUI.Ink;
        muted=new GUIStyle(body){fontSize=16};muted.normal.textColor=EcoUI.Muted;
        subtitle=new GUIStyle(body){fontSize=21,fontStyle=FontStyle.Bold};
        TextColors(title, EcoUI.Ink); TextColors(body, EcoUI.Ink);
        TextColors(muted, EcoUI.Muted); TextColors(subtitle, EcoUI.Ink);
        // The 96×48 wooden button has a ~7 px frame and a heavier bottom lip; slicing at 12 px and centring 18 px text
        // ran the text into that lip on 34 px rows. A thinner vertical slice, 17 px text and a 2 px lift keep it on the cream.
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,padding=new RectOffset(14,14,6,8)};
        button.normal.background=buttonSkin; button.normal.textColor=EcoUI.Ink; button.border=new RectOffset(16,16,9,10);
        button.hover.background=buttonHover; button.hover.textColor=EcoUI.Ink;
        button.focused.background=buttonHover; button.focused.textColor=EcoUI.Ink;
        button.active.background=buttonHover; button.active.textColor=EcoUI.Ink;
        button.onNormal.background=buttonSkin; button.onNormal.textColor=EcoUI.Ink;
        button.onHover.background=buttonHover; button.onHover.textColor=EcoUI.Ink;
        button.onActive.background=buttonHover; button.onActive.textColor=EcoUI.Ink;
        button.onFocused.background=buttonHover; button.onFocused.textColor=EcoUI.Ink;
        cardStyle=new GUIStyle{normal={background=cardSkin,textColor=EcoUI.Ink},border=new RectOffset(16,16,16,16),padding=new RectOffset(22,22,20,20)};
        thumb=new GUIStyle{fixedWidth=14,fixedHeight=18,normal={background=green},hover={background=green},active={background=green}};
    }
}
