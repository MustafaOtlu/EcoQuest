using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent observations and recipes. Discovery never requires spending YEP.</summary>
public sealed class EcoDiscoveries : MonoBehaviour
{
    public static EcoDiscoveries Current { get; private set; }
    private static readonly HashSet<string> facts = new HashSet<string>(StringComparer.Ordinal);
    private float nextScan;
    private PlayerWeaponSystem player;
    public static event Action<string> Discovered;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() { facts.Clear(); Current = null; Discovered = null; }
    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }
    public static bool Has(string id) => !string.IsNullOrEmpty(id) && facts.Contains(id);
    public static bool Remember(string id)
    {
        if (string.IsNullOrEmpty(id) || !facts.Add(id)) return false;
        Discovered?.Invoke(id); return true;
    }
    public static string[] Capture() { var values = new List<string>(facts); values.Sort(StringComparer.Ordinal); return values.ToArray(); }
    public static void Restore(string[] saved)
    {
        facts.Clear();
        if (saved != null) foreach (string value in saved) if (!string.IsNullOrEmpty(value)) facts.Add(value);
    }
    public static bool CanBuild(string id)
    {
        if (id == "solar" || id == "battery" || id == "charger" || id == "farm" || id == "recycling") return true;
        if (id == "wind") return Has("recipe.wind") && Completed(0); // GDD §7: wind opens after the first energy project
        if (id == "intake" || id == "purifier" || id == "tank") return Has("recipe.water");
        if (id == "hydro") return Has("recipe.hydro") || Completed(1);
        if (id == "station" || id == "glacier") return Has("recipe.station");
        return false;
    }
    public static string UnlockReason(string id) => CanBuild(id) ? ""
        : id == "wind" ? (Has("recipe.wind") ? "Önce \"Mahalleye ilk enerji\" projesini tamamla." : "Rüzgârlı tepelerin üretim koşullarını keşfet.")
        : id == "hydro" ? "Gölü canlandırma projesi sonrası doğal akış santrali açılır."
        : id == "station" || id == "glacier" ? "Buzul bölgesine giderek izleme tarifini keşfet."
        : "Su sorunu olan bir gölü incele; temel çözüm tarifi YEP istemez.";
    public static bool CanPlant(EcoCropCatalog.Kind kind) => kind == EcoCropCatalog.Kind.Cabbage
        || kind == EcoCropCatalog.Kind.Tree && (Has("recipe.grove") || Completed(0))
        || kind == EcoCropCatalog.Kind.Carrot && (Has("recipe.water") || Has("field.harvested"))
        || kind == EcoCropCatalog.Kind.Tomato && (Has("recipe.farm") || Has("field.harvested"));
    private static bool Completed(int index) => EcoProjects.Current != null && index >= 0
        && index < EcoProjects.Current.All.Count && EcoProjects.Current.All[index].done;
    public static void DiscoverRegion(EcoRegion region)
    {
        if (region == null) return;
        bool first = Remember("region." + region.regionName);
        string name = region.regionName;
        if (name.Contains("Göl")) Remember("recipe.water");
        if (name.Contains("Koruluk") || name.Contains("Rüzg") || name.Contains("Wind"))
        { Remember("recipe.grove"); Remember("recipe.wind"); }
        if (name.Contains("Buzul")) Remember("recipe.station");
        if (name.Contains("Güneş") || name.Contains("Şehir")) Remember("recipe.farm");
        if (first && Current != null && Current.player != null)
            Current.player.ShowFeedback(region.regionName + " keşfedildi • Belirti ve ipuçlarına bak " + EcoKeys.Tag(EcoKeys.Action.Journal) + ".");
    }
    public static void NotifyCleanHarvest(PlantedSeed plant)
    {
        if (plant == null || plant.contaminated || plant.health <= 0 || plant.kind == EcoCropCatalog.Kind.Tree) return;
        Remember("field.harvested"); Remember("recipe.farm"); TryAwardField();
    }
    public static void NotifyPlanting(PlantedSeed plant)
    {
        if (plant == null || plant.contaminated || plant.IsWithered || !plant.ClimateSuitable) return;
        var region = EcoRegion.At(plant.transform.position);
        if (region != null && region.soilPollution >= 40f) return;
        if (Has("field.harvested")) Remember("field.replanted");
        if (plant.kind == EcoCropCatalog.Kind.Tree) Remember("field.treePlanted");
        TryAwardField();
    }
    public static void NotifyCleanFish(EcoRegion source)
    {
        if (source == null || source.waterPollution > 30f) return;
        Remember("field.fished"); TryAwardField();
    }
    public static void NotifyPlacementWarning(string warning)
    { if (!string.IsNullOrEmpty(warning)) Remember("field.placementWarning"); }
    public static void NotifySafePlacement(string buildingId, Vector3 position)
    {
        if (!Has("field.placementWarning") || !string.IsNullOrEmpty(EcoRegion.PlacementWarning(buildingId, position))) return;
        Remember("field.safePlacement"); TryAwardField();
    }
    public static bool InletRemediated(EcoRegion region) => region != null && Has("inlet." + region.regionName);
    public static void RemediateInlet(EcoRegion region) { if (region != null) Remember("inlet." + region.regionName); }
    public static void TryAwardField()
    {
        var owner = Current != null ? Current.player : FindFirstObjectByType<PlayerWeaponSystem>();
        if (owner == null || owner.Progression == null) return;
        if (Has("field.harvested") && Has("field.replanted")) Grant(owner, "field.harvest", "Temiz hasat ve yeniden ekim");
        if (Has("field.fished")) Grant(owner, "field.fish", "Temiz kaynaktan sürdürülebilir ilk balık");
        if (Has("field.safePlacement")) Grant(owner, "field.placement", "Çevresel uyarıya uygun yerleşim");
    }
    private static void Grant(PlayerWeaponSystem owner, string id, string label)
    { if (owner.Progression.AwardOnce(id, 20) > 0) owner.ShowFeedback("SAHA BAŞARISI • " + label + " • +20 YEP"); }
    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
        if (player != null) DiscoverRegion(EcoRegion.At(player.transform.position));
        if (Completed(1)) Remember("recipe.hydro");
        TryAwardField(); EnsureInlet();
    }
    private static void EnsureInlet()
    {
        if (FindFirstObjectByType<EcoPollutionInlet>() != null) return;
        foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
        {
            if (!region.regionName.Contains("Doğu Göl")) continue;
            Vector3 position = region.transform.TransformPoint(new Vector3(-region.size.x * 0.5f - 1.5f, 0f, 0f));
            if (Physics.Raycast(position + Vector3.up * 150f, Vector3.down, out var ground, 300f, ~0, QueryTriggerInteraction.Ignore)) position.y = ground.point.y;
            var marker = new GameObject("Doğu Gölü - kirletici giriş"); marker.transform.position = position;
            var inlet = marker.AddComponent<EcoPollutionInlet>(); inlet.region = region; inlet.CreateVisual(); break;
        }
    }
}
