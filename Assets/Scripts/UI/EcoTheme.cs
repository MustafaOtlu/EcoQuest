using UnityEngine;

// Shared, resolution-independent UI. Text renders at the screen scale, never into a small texture.
public static class EcoTheme
{
    private static Texture2D backdrop, card, green;
    private static GUIStyle title, body, muted, button, subtitle, cardStyle, thumb;
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
        GUI.color=new Color(0,0,0,.62f); GUI.DrawTexture(new Rect(0,0,Width,Height),Texture2D.whiteTexture); GUI.color=Color.white;
        width=Mathf.Min(width,Width-30); height=Mathf.Min(height,Height-30);
        var rect=new Rect((Width-width)*.5f,(Height-height)*.5f,width,height);
        GUI.DrawTexture(rect,backdrop);
        GUI.DrawTexture(new Rect(rect.x,rect.y,5,rect.height),green);
        GUI.Label(new Rect(rect.x+28,rect.y+18,rect.width-56,34),heading,title);
        GUI.Label(new Rect(rect.x+28,rect.y+54,rect.width-56,30),description,muted);
        return rect;
    }
    /// <summary>Window with an icon beside the heading (icon names from Resources/EcoIcons).</summary>
    public static Rect Window(float width, float height, string heading, string description, string icon)
    {
        var rect = Window(width, height, "", "");
        Icon(new Rect(rect.x + 24, rect.y + 18, 40, 40), icon);
        GUI.Label(new Rect(rect.x + 74, rect.y + 18, rect.width - 102, 34), heading, title);
        GUI.Label(new Rect(rect.x + 74, rect.y + 54, rect.width - 102, 30), description, muted);
        return rect;
    }
    /// <summary>Draws a hand-drawn icon; does nothing when the icon is missing.</summary>
    public static void Icon(Rect rect, string icon)
    {
        var sprite = EcoIcons.Get(icon);
        if (sprite != null) GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
    }
    /// <summary>A themed button with an icon on its left.</summary>
    public static bool IconButton(Rect rect, string text, string icon, GUIStyle style = null)
    {
        Init();
        bool pressed = GUI.Button(rect, "        " + text, style ?? button);
        float s = Mathf.Min(26f, rect.height - 10f);
        Icon(new Rect(rect.x + 12, rect.y + (rect.height - s) * .5f, s, s), icon);
        return pressed;
    }
    /// <summary>Themed 0..1 slider: dark track, green fill and a light thumb (replaces the default grey skin).</summary>
    public static float Slider(Rect rect, float value)
    {
        Init();
        var track = new Rect(rect.x, rect.y + rect.height * .5f - 3f, rect.width, 6f);
        GUI.DrawTexture(track, card);
        GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(value), track.height), green);
        return GUI.HorizontalSlider(rect, value, 0f, 1f, GUIStyle.none, thumb);
    }
    private static Texture2D Swatch(Color color) { var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t; }
    private static void Init()
    {
        if(backdrop!=null)return;
        backdrop=Swatch(new Color(.035f,.08f,.075f,.99f));card=Swatch(new Color(.08f,.15f,.14f));green=Swatch(new Color(.4f,.81f,.58f));
        var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        title=new GUIStyle(GUI.skin.label){font=font,fontSize=28,fontStyle=FontStyle.Bold}; title.normal.textColor=new Color(.88f,.97f,.9f);
        body=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true};body.normal.textColor=new Color(.91f,.95f,.92f);
        muted=new GUIStyle(body){fontSize=16};muted.normal.textColor=new Color(.57f,.73f,.67f);
        subtitle=new GUIStyle(body){fontSize=21,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){font=font,fontSize=18,padding=new RectOffset(14,14,8,8)};
        button.normal.background=card; button.normal.textColor=new Color(.84f,.96f,.88f);button.hover.background=green;button.hover.textColor=new Color(.02f,.09f,.05f);
        cardStyle=new GUIStyle{normal={background=card},padding=new RectOffset(18,18,12,12)};
        var knob=Swatch(new Color(.88f,.97f,.9f)); thumb=new GUIStyle{fixedWidth=14,fixedHeight=18,normal={background=knob},hover={background=knob},active={background=green}};
    }
}
