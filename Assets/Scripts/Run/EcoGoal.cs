using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>What a goal measures. Project goals reuse an EcoProjects check; the rest read the living world.</summary>
public enum EcoGoalMetric { Project, LakeOxygen, IndustryAqi, GroveCover, WorldCarbon, CleanGenerators, FishStock, WasteCleared, ValleyHealth }

/// <summary>Catalogue entry. Thresholds are indexed by difficulty (Sakin, Normal, Kriz).</summary>
public sealed class EcoGoalSpec
{
    public string id, group, title, icon;
    public EcoGoalMetric metric;
    public EcoRegionInfo.Kind kind;
    public int project = -1;
    public float[] thresholds = new float[3];
    public float[] problemWeight = { 1, 1, 1, 1, 1 };
    public bool trainingOnly;

    public string Detail(float threshold)
    {
        switch (metric)
        {
            case EcoGoalMetric.LakeOxygen: return "Kirli gölde çözünmüş oksijen en az " + threshold.ToString("0.0") + " mg/L";
            case EcoGoalMetric.IndustryAqi: return "Sanayi bölgesinde hava kalitesi endeksi (AQI) en çok " + threshold.ToString("0");
            case EcoGoalMetric.GroveCover: return "Ormanda bitki örtüsü en az %" + (threshold * 100f).ToString("0") + ", habitat en az %" + (GroveHabitat(threshold) * 100f).ToString("0") + ", yangın yok";
            case EcoGoalMetric.WorldCarbon: return "Vadinin ortalama CO₂'si en çok " + (425f + threshold * 2.5f).ToString("0") + " ppm";
            case EcoGoalMetric.CleanGenerators: return "Senin kurduğun en az " + threshold.ToString("0") + " temiz enerji üreticisi çalışıyor";
            case EcoGoalMetric.FishStock: return "Göllerdeki ortalama balık stoku en az " + threshold.ToString("0") + "/12";
            case EcoGoalMetric.WasteCleared: return threshold <= 0f ? "Vadide toplanmamış atık yığını kalmasın" : "Vadide en çok " + threshold.ToString("0") + " atık yığını kalsın";
            case EcoGoalMetric.ValleyHealth: return "Ekosistem sağlığı en az %" + threshold.ToString("0");
            default: return EcoGoalCatalog.ProjectGoal(project);
        }
    }

    /// <summary>The grove goal pairs cover with habitat; habitat follows the same difficulty step.</summary>
    public static float GroveHabitat(float cover) => Mathf.Clamp01(cover - 0.1f);
}

/// <summary>A goal inside one run (saved).</summary>
[Serializable]
public sealed class EcoGoalState
{
    public string id;
    public float threshold, held;
    public bool required, done;
    [NonSerialized] public bool met;
    [NonSerialized] public float progress;
    [NonSerialized] public string status = "";
    public EcoGoalSpec Spec => EcoGoalCatalog.Find(id);
}

public static class EcoGoalCatalog
{
    public const int RequiredCount = 2, BonusCount = 1;
    private static List<EcoGoalSpec> all;

    // Weights per problem: DirtyLake, Smog, Wildfire, Waste, Warming.
    public static IReadOnlyList<EcoGoalSpec> All
    {
        get
        {
            if (all != null) return all;
            all = new List<EcoGoalSpec>
            {
                new EcoGoalSpec { id = "energy_project", group = "power", title = "Mahalleye ilk enerji", icon = "electricity", metric = EcoGoalMetric.Project, project = 0, kind = EcoRegionInfo.Kind.Sun, trainingOnly = true },
                new EcoGoalSpec { id = "lake_project", group = "lake", title = "Gölü canlandır", icon = "region_lake_dirty", metric = EcoGoalMetric.Project, project = 1, kind = EcoRegionInfo.Kind.Lake, problemWeight = new float[] { 3, 1, 0.8f, 1, 0.8f } },
                new EcoGoalSpec { id = "lake_oxygen", group = "lake", title = "Gölün oksijeni", icon = "region_lake_dirty", metric = EcoGoalMetric.LakeOxygen, project = 1, kind = EcoRegionInfo.Kind.Lake, thresholds = new[] { 6.5f, 7f, 7.5f }, problemWeight = new float[] { 4, 1, 1, 1, 1 } },
                new EcoGoalSpec { id = "fish_stock", group = "lake", title = "Balıklar geri dönsün", icon = "fish", metric = EcoGoalMetric.FishStock, project = 1, kind = EcoRegionInfo.Kind.Lake, thresholds = new[] { 6f, 8f, 9f }, problemWeight = new float[] { 2, 0.6f, 0.6f, 0.8f, 0.8f } },
                new EcoGoalSpec { id = "industry_aqi", group = "industry", title = "Sanayinin havası", icon = "poi_factory", metric = EcoGoalMetric.IndustryAqi, project = 3, kind = EcoRegionInfo.Kind.Industry, thresholds = new[] { 90f, 70f, 55f }, problemWeight = new float[] { 1.2f, 4, 1.2f, 1.2f, 1.5f } },
                new EcoGoalSpec { id = "industry_project", group = "industry", title = "Sanayiyi dönüştür", icon = "poi_factory", metric = EcoGoalMetric.Project, project = 3, kind = EcoRegionInfo.Kind.Industry, problemWeight = new float[] { 0.8f, 2, 0.8f, 0.8f, 1.2f } },
                new EcoGoalSpec { id = "grove_cover", group = "grove", title = "Orman toparlansın", icon = "poi_tree", metric = EcoGoalMetric.GroveCover, project = 2, kind = EcoRegionInfo.Kind.Grove, thresholds = new[] { 0.5f, 0.6f, 0.65f }, problemWeight = new float[] { 1, 1, 4, 1, 1.2f } },
                new EcoGoalSpec { id = "world_carbon", group = "climate", title = "Karbonu düşür", icon = "carbon", metric = EcoGoalMetric.WorldCarbon, project = 5, kind = EcoRegionInfo.Kind.Glacier, thresholds = new[] { 24f, 20f, 17f }, problemWeight = new float[] { 1, 1.5f, 1, 1, 4 } },
                new EcoGoalSpec { id = "glacier_project", group = "climate", title = "Buzulu koru", icon = "region_glacier", metric = EcoGoalMetric.Project, project = 5, kind = EcoRegionInfo.Kind.Glacier, problemWeight = new float[] { 0.5f, 0.8f, 0.5f, 0.5f, 2 } },
                new EcoGoalSpec { id = "clean_power", group = "power", title = "Temiz enerji ağı", icon = "electricity", metric = EcoGoalMetric.CleanGenerators, project = 0, kind = EcoRegionInfo.Kind.Sun, thresholds = new[] { 3f, 4f, 5f }, problemWeight = new float[] { 1, 1.5f, 1, 1, 2 } },
                new EcoGoalSpec { id = "resilience_project", group = "power", title = "Yerleşimi dayanıklı yap", icon = "ui_layers", metric = EcoGoalMetric.Project, project = 4, kind = EcoRegionInfo.Kind.City, problemWeight = new float[] { 0.5f, 0.6f, 0.6f, 0.5f, 0.8f } },
                new EcoGoalSpec { id = "waste_cleared", group = "waste", title = "Atıkları temizle", icon = "scrap", metric = EcoGoalMetric.WasteCleared, kind = EcoRegionInfo.Kind.City, thresholds = new[] { 3f, 1f, 0f }, problemWeight = new float[] { 0.8f, 0.8f, 0.8f, 4, 0.8f } },
                new EcoGoalSpec { id = "valley_health", group = "health", title = "Sağlıklı vadi", icon = "ecosystem", metric = EcoGoalMetric.ValleyHealth, kind = EcoRegionInfo.Kind.City, thresholds = new[] { 55f, 62f, 70f } },
            };
            return all;
        }
    }

    public static EcoGoalSpec Find(string id) => All.FirstOrDefault(g => g.id == id);

    public static string ProjectGoal(int project)
    {
        switch (project)
        {
            case 0: return "Güneş paneli, batarya ve şarj istasyonunu kur, bağla; 10 EB şarj al";
            case 1: return "Kirletici girişi kapat; pompa → arıtma → depo kur, göle dönüşü aç; su kalitesi en az 70";
            case 2: return "Ormanda yangın yok, örtü %60, habitat %50 ve sağlıklı ağaç ekimi";
            case 3: return "Bütün fabrikalara filtre ve temiz enerji dönüşümü; hava kalitesi en az 70";
            case 4: return "Bir üretici kapalıyken pompa, arıtma ve şarj çalışsın; 120 sn rezerv";
            case 5: return "Buzul istasyonu etkin; vadi karbonu güvenli ve erime artmıyor";
            default: return "";
        }
    }

    /// <summary>Deterministic goal draw: two required goals and one bonus, never two from the same group, only from
    /// region kinds present in the valley. Weights follow the valley's main problem.</summary>
    public static List<EcoGoalState> Pick(EcoScenario scenario, ICollection<EcoRegionInfo.Kind> kinds = null)
    {
        int level = (int)scenario.difficulty;
        var result = new List<EcoGoalState>();
        if (scenario.training)
        {
            foreach (var (id, required) in new[] { ("energy_project", true), ("lake_project", true), ("waste_cleared", false) })
                result.Add(new EcoGoalState { id = id, required = required, threshold = Find(id).thresholds[level] });
            return result;
        }
        // Difficulty only moves thresholds; the same valley keeps the same goals when the player changes it on the briefing.
        var rng = new System.Random(scenario.seed * 7919 + 3);
        var pool = All.Where(g => !g.trainingOnly && (kinds == null || kinds.Contains(g.kind))).ToList();
        var usedGroups = new HashSet<string>();
        for (int n = 0; n < RequiredCount + BonusCount; n++)
        {
            var open = pool.Where(g => !usedGroups.Contains(g.group)).ToList();
            if (open.Count == 0) break;
            // The first goal leans hard on the valley's main problem (squared weights); the rest add variety.
            float Weight(EcoGoalSpec g) { float w = g.problemWeight[(int)scenario.problem]; return n == 0 ? w * w : w; }
            float total = open.Sum(Weight);
            double roll = rng.NextDouble() * total;
            var chosen = open[open.Count - 1];
            foreach (var g in open)
            {
                roll -= Weight(g);
                if (roll <= 0) { chosen = g; break; }
            }
            usedGroups.Add(chosen.group);
            result.Add(new EcoGoalState { id = chosen.id, required = n < RequiredCount, threshold = chosen.thresholds[level] });
        }
        return result;
    }
}
