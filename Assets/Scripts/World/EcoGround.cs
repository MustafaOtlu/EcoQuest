using UnityEngine;

// Marks the generated low-poly map ground (it replaces the old Unity Terrain). Soil accepts planting and
// building; roads, plazas and rock/snow are walkable but not farmland or a building site.
[DisallowMultipleComponent]
public sealed class EcoGround : MonoBehaviour
{
    public enum Surface { Soil, Road, Plaza, Rock }
    public Surface surface = Surface.Soil;

    /// <summary>Playable extents of the map (x/z), set by the map builder; used to keep the camera inside.</summary>
    public static Rect MapRect { get; private set; } = new Rect(0f, 0f, 0f, 0f);
    public static bool HasMap => MapRect.width > 0f;
    public Rect mapRect;

    private void OnEnable() { if (mapRect.width > 0f) MapRect = mapRect; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => MapRect = new Rect(0f, 0f, 0f, 0f);

    /// <summary>True when a collider is natural ground (old terrain or generated soil) that can carry crops and buildings.</summary>
    public static bool IsSoil(Collider collider)
    {
        if (collider == null) return false;
        if (collider is TerrainCollider) return true;
        var ground = collider.GetComponentInParent<EcoGround>();
        return ground != null && ground.surface == Surface.Soil;
    }

    /// <summary>True for any generated ground piece (soil, road, plaza, rock) or the old terrain.</summary>
    public static bool IsGround(Collider collider)
        => collider != null && (collider is TerrainCollider || collider.GetComponentInParent<EcoGround>() != null);

    public static string SurfaceName(Collider collider)
    {
        var ground = collider != null ? collider.GetComponentInParent<EcoGround>() : null;
        if (ground == null) return "";
        return ground.surface switch
        {
            Surface.Road => "Yol: buraya ekim veya inşa yapılmaz.",
            Surface.Plaza => "Meydan: buraya ekim veya inşa yapılmaz.",
            Surface.Rock => "Kaya/buz zemin: ekim için uygun değil.",
            _ => "",
        };
    }
}
