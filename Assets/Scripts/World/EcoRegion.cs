using System.Collections.Generic;
using UnityEngine;

// Pollution and carbon are gameplay indices (0..100), not physical measurements.
public sealed class EcoRegion : MonoBehaviour
{
    private static readonly HashSet<EcoRegion> Active = new HashSet<EcoRegion>();
    public string regionName = "Bölge";
    [Tooltip("Stable id used by saves, discoveries and projects (the region's original name). The shown name can change freely.")]
    public string key = "";
    public string Key => string.IsNullOrEmpty(key) ? regionName : key;
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
    private static float worldCarbon, worldCarbonAt = -10f;
    /// <summary>GDD §10: area-weighted world carbon. The map is sampled on a grid and each point counts the region that
    /// owns it (highest priority), so overlapping pockets or many small clean regions cannot pull the average down.</summary>
    public static float WorldCarbon()
    {
        if (Time.time - worldCarbonAt < 1f && worldCarbonAt >= 0f) return worldCarbon;
        worldCarbonAt = Time.time;
        bool any = false; float minX = 0f, maxX = 0f, minZ = 0f, maxZ = 0f;
        foreach (var r in Active)
        {
            if (r == null) continue;
            Vector3 c = r.transform.position; float hx = r.size.x * 0.5f, hz = r.size.y * 0.5f;
            if (!any) { minX = c.x - hx; maxX = c.x + hx; minZ = c.z - hz; maxZ = c.z + hz; any = true; }
            else { minX = Mathf.Min(minX, c.x - hx); maxX = Mathf.Max(maxX, c.x + hx); minZ = Mathf.Min(minZ, c.z - hz); maxZ = Mathf.Max(maxZ, c.z + hz); }
        }
        if (!any) return worldCarbon = 0f;
        float step = Mathf.Max(2f, Mathf.Max(maxX - minX, maxZ - minZ) / 40f), sum = 0f; int count = 0;
        float y = 0f; foreach (var r in Active) if (r != null) { y = r.transform.position.y; break; }
        for (float x = minX + step * 0.5f; x < maxX; x += step)
            for (float z = minZ + step * 0.5f; z < maxZ; z += step)
            {
                var region = At(new Vector3(x, y, z));
                if (region == null) continue;
                sum += region.carbon; count++;
            }
        return worldCarbon = count > 0 ? sum / count : 0f;
    }

    private void Update() { Simulate(Time.deltaTime); UpdateVisual(); }
    public void Simulate(float seconds)
    {
        if (!Valid(seconds)) return;
        // Recovery remains gradual even after an emission source has been filtered.
        float recovery = Mathf.Lerp(0.015f, 0.06f, vegetation) * EcoSpecialization.Recovery;
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
    // ---------------------------------------------------------------- real-world readings
    // The simulation keeps 0..100 indices; these map them onto familiar measurements so the map can show both. The
    // ranges follow common reference values: PM2.5 a few µg/m³ in clean countryside, 15–35 in towns, 50–150 beside
    // unfiltered industry; outdoor CO₂ about 425 ppm, higher near traffic and chimneys; dissolved oxygen 8–10 mg/L
    // in a healthy lake, under 4 mg/L where fish struggle; topsoil organic matter 3–6 % in good soil, under 2 % when degraded.
    public float PM25 => 3f + airPollution * 1.2f;
    public int AQI => AirQualityIndex(PM25);
    public float CO2ppm => 425f + carbon * 2.5f;
    public float DissolvedOxygen => Mathf.Max(0.5f, 9.6f - waterPollution * 0.09f);
    public float SoilOrganicMatter => 0.5f + Mathf.Pow(1f - soilPollution / 100f, 1.5f) * 4.5f;

    /// <summary>US EPA AQI from a 24 h PM2.5 concentration (2024 breakpoints).</summary>
    public static int AirQualityIndex(float pm)
    {
        float[] c = { 0f, 9.0f, 35.4f, 55.4f, 125.4f, 225.4f, 325.4f };
        int[] a = { 0, 50, 100, 150, 200, 300, 500 };
        for (int i = 1; i < c.Length; i++)
            if (pm <= c[i]) return Mathf.RoundToInt(Mathf.Lerp(a[i - 1], a[i], (pm - c[i - 1]) / (c[i] - c[i - 1])));
        return 500;
    }
    public static string AqiName(int aqi) => aqi <= 50 ? "İyi" : aqi <= 100 ? "Orta" : aqi <= 150 ? "Hassas gruplar için sağlıksız"
        : aqi <= 200 ? "Sağlıksız" : aqi <= 300 ? "Çok sağlıksız" : "Tehlikeli";

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
