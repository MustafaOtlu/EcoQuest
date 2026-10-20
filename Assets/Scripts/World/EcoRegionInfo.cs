using UnityEngine;

// Presentation data for an EcoRegion: used by the world HUD, region banner and the map screen.
// It does not change any simulation value.
[RequireComponent(typeof(EcoRegion))]
public sealed class EcoRegionInfo : MonoBehaviour
{
    public enum Kind { Glacier, Wind, City, Sun, Industry, Lake, Grove }
    public Kind kind = Kind.City;
    [TextArea] public string tagline = "";
    public Color mapColor = Color.white;
    [Tooltip("EcoProjects index whose goal lives in this region (-1: none).")]
    public int projectIndex = -1;
    [Tooltip("How much stronger the wind blows here than the world average (map wind icon, turbine siting).")]
    [Range(0f, 2f)] public float windExposure = 1f;
    [Tooltip("Share of daylight that reaches the ground here (shade, haze).")]
    [Range(0f, 1.2f)] public float sunExposure = 1f;
    [Tooltip("Short facts for the map card: resources and opportunities in this area.")]
    public string[] features = new string[0];
}
