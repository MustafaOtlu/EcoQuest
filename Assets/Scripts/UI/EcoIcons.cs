using System.Collections.Generic;
using UnityEngine;

// Shared access to the hand-drawn icon set in Resources/EcoIcons (source art: Tools/IconArt/icons.js).
// Missing names return null so callers can fall back to text.
public static class EcoIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => Cache.Clear();

    public static Sprite Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (Cache.TryGetValue(name, out var sprite) && sprite != null) return sprite;
        sprite = Resources.Load<Sprite>("EcoIcons/" + name);
        Cache[name] = sprite;
        return sprite;
    }

    public static string Region(EcoRegionInfo.Kind kind, bool polluted = false) => kind switch
    {
        EcoRegionInfo.Kind.Glacier => "region_glacier",
        EcoRegionInfo.Kind.Wind => "region_wind",
        EcoRegionInfo.Kind.City => "region_city",
        EcoRegionInfo.Kind.Industry => "region_industry",
        EcoRegionInfo.Kind.Sun => "region_sun",
        _ => polluted ? "region_lake_dirty" : "region_lake",
    };

    public static string Weather(EcoWorldClock clock)
    {
        if (clock == null) return "w_sun";
        bool night = clock.hour < 6f || clock.hour > 20f;
        if (clock.RainIntensity > 0.65f && clock.windStrength > 0.7f) return "w_storm";
        if (clock.RainIntensity > 0.15f) return "w_rain";
        if (clock.cloudCover > 0.55f) return "w_overcast";
        if (clock.cloudCover > 0.3f) return night ? "w_overcast" : "w_cloud";
        return night ? "w_moon" : "w_sun";
    }
}
