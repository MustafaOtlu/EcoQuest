using UnityEngine;

// GDD §13: persistent accessibility/UI settings shared by every panel (UI scale, colour-blind palette).
public static class EcoSettings
{
    private const string ScaleKey = "EcoQuest.uiScale", PaletteKey = "EcoQuest.colorBlind";
    private static bool loaded;
    private static float scale = 1f;
    private static bool colorBlind;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { loaded = false; }
    private static void Load()
    {
        if (loaded) return; loaded = true;
        scale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 1f), 1f, 1.5f);
        colorBlind = PlayerPrefs.GetInt(PaletteKey, 0) == 1;
    }
    public static float UIScale
    {
        get { Load(); return scale; }
        set { Load(); scale = Mathf.Clamp(value, 1f, 1.5f); PlayerPrefs.SetFloat(ScaleKey, scale); PlayerPrefs.Save(); }
    }
    public static bool ColorBlind
    {
        get { Load(); return colorBlind; }
        set { Load(); colorBlind = value; PlayerPrefs.SetInt(PaletteKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool CameraShake
    {
        get => PlayerPrefs.GetInt("EcoQuest.shake", 1) == 1;
        set { PlayerPrefs.SetInt("EcoQuest.shake", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    /// <summary>GDD §13: zoom direction is adjustable; on, wheel-up moves the camera away instead of closer.</summary>
    public static bool InvertZoom
    {
        get => PlayerPrefs.GetInt("EcoQuest.invertZoom", 0) == 1;
        set { PlayerPrefs.SetInt("EcoQuest.invertZoom", value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool ReduceBlur
    {
        get => PlayerPrefs.GetInt("EcoQuest.reduceBlur", 0) == 1;
        set { PlayerPrefs.SetInt("EcoQuest.reduceBlur", value ? 1 : 0); PlayerPrefs.Save(); EcoCameraFx.ApplyBlurSetting(); }
    }
    /// <summary>Good/bad status colours; the colour-blind palette uses blue/orange instead of green/red.</summary>
    public static Color Good => ColorBlind ? new Color(0.0f, 0.45f, 0.7f) : new Color(0.3f, 0.83f, 0.53f);
    public static Color Bad => ColorBlind ? new Color(0.9f, 0.6f, 0.0f) : new Color(0.9f, 0.25f, 0.2f);
}
