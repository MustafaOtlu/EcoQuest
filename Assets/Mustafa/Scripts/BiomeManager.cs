using System.Linq;
using UnityEngine;

public class BiomeManager : MonoBehaviour
{
    BiomeZone[] zones;

    public static BiomeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        zones = FindObjectsByType<BiomeZone>(FindObjectsSortMode.None);
    }

    public BiomeZone GetBiomeAt(Vector2 position)
    {
        foreach (var zone in zones)
        {
            if (zone.ContainsPoint(position))
                return zone;
        }
        return null;
    }

    public BiomeType? GetBiomeTypeAt(Vector2 position)
    {
        var zone = GetBiomeAt(position);
        return zone != null ? zone.BiomeType : null;
    }

    public float GetWindPotentialAt(Vector2 position)
    {
        var zone = GetBiomeAt(position);
        return zone != null ? zone.GetWindPotential() : 0.2f;
    }

    public float GetSunPotentialAt(Vector2 position)
    {
        var zone = GetBiomeAt(position);
        return zone != null ? zone.GetSunPotential() : 0.5f;
    }

    public BiomeZone[] GetZonesOfType(BiomeType type)
    {
        return zones.Where(z => z.BiomeType == type).ToArray();
    }
}
