using System.Collections.Generic;
using UnityEngine;

// Pollution and carbon are gameplay indices (0..100), not physical measurements.
public sealed class EcoRegion : MonoBehaviour
{
    private static readonly HashSet<EcoRegion> Active = new HashSet<EcoRegion>();
    public string regionName = "Bölge";
    public Vector2 size = new Vector2(24, 32);
    public int priority;
    [Range(0, 100)] public float airPollution, waterPollution, soilPollution, carbon;
    public float baseTemperature = 20;
    [Range(0, 1)] public float vegetation = 0.5f;
    public bool sensitiveHabitat;
    public Renderer groundVisual;
    private MaterialPropertyBlock tint;
    public float Temperature => baseTemperature + carbon * 0.08f + airPollution * 0.025f;
    public float SolarTransmission => 1 - airPollution * 0.006f;
    public bool CleanWater => waterPollution < 35;
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
    public bool Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.z) <= size.y * 0.5f;
    }
    public static EcoRegion At(Vector3 point)
    {
        EcoRegion best = null;
        foreach (var region in Active)
            if (region != null && region.Contains(point) && (best == null || region.priority > best.priority
                || (region.priority == best.priority && region.GetInstanceID() < best.GetInstanceID()))) best = region;
        return best;
    }
    private void Update() { Simulate(Time.deltaTime); UpdateVisual(); }
    public void Simulate(float seconds)
    {
        if (!Valid(seconds)) return;
        // Recovery remains gradual even after an emission source has been filtered.
        float recovery = Mathf.Lerp(0.015f, 0.06f, vegetation);
        AddPollution(-recovery * seconds, -recovery * 0.22f * seconds, -recovery * 0.12f * seconds, -recovery * 0.4f * seconds);
    }
    public void AddPollution(float air, float water, float soil, float carbonAdded)
    {
        if (!Finite(air) || !Finite(water) || !Finite(soil) || !Finite(carbonAdded)) return;
        airPollution = Mathf.Clamp(airPollution + air, 0, 100);
        waterPollution = Mathf.Clamp(waterPollution + water, 0, 100);
        soilPollution = Mathf.Clamp(soilPollution + soil, 0, 100);
        carbon = Mathf.Clamp(carbon + carbonAdded, 0, 100);
    }
    public string Describe() => regionName + " • " + Temperature.ToString("0.0") + " °C"
        + "\nKirlilik: hava %" + airPollution.ToString("0") + " • su %" + waterPollution.ToString("0") + " • toprak %" + soilPollution.ToString("0")
        + "\nKarbon " + carbon.ToString("0") + "/100"
        + (TryGetComponent<EcoHabitat>(out var habitat) ? " • Kuş " + habitat.Birds + " • Kelebek " + habitat.Butterflies : "");
    public static string PlacementWarning(string buildingId, Vector3 position)
    {
        var region = At(position);
        if (region == null) return "";
        if (buildingId == "farm" && region.soilPollution >= 40) return "Kirli toprak: ürünler kirlenir ve daha yavaş büyür.";
        if (region.sensitiveHabitat && (buildingId == "wind" || buildingId == "recycling"))
            return "Hassas yaşam alanı: bu tesis çevredeki bitki örtüsünü azaltır.";
        if (buildingId == "solar" && region.airPollution >= 40) return "Kirli hava güneş panelinin üretimini azaltır.";
        return "";
    }
    private void UpdateVisual()
    {
        if (groundVisual == null) return;
        tint ??= new MaterialPropertyBlock();
        var color = Color.Lerp(new Color(0.32f, 0.52f, 0.24f), new Color(0.31f, 0.26f, 0.18f), soilPollution / 100);
        tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); groundVisual.SetPropertyBlock(tint);
    }
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    internal static bool Valid(float value) => value > 0 && Finite(value);
}
