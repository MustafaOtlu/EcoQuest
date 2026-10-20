using UnityEngine;

// Presentation data for an EcoRegion: used by the world HUD, region banner and the map screen.
// It does not change any simulation value.
[RequireComponent(typeof(EcoRegion))]
public sealed class EcoRegionInfo : MonoBehaviour
{
    public enum Kind { Glacier, Wind, City, Sun, Industry, Lake }
    public Kind kind = Kind.City;
    [TextArea] public string tagline = "";
    public Color mapColor = Color.white;
}
