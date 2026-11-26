using System.Linq;
using UnityEngine;

/// <summary>Evening events (18:00 each run day). The pick is deterministic per seed and day, weighted by the valley's
/// problem; the strength grows with the day and the difficulty. Prepared players soften them (filters, tanks, cover).</summary>
public static class EcoCrisis
{
    public sealed class Crisis
    {
        public string id, title, text, icon;
        public EcoProblem favoured;
    }

    public static readonly Crisis[] All =
    {
        new Crisis { id = "wildfire", title = "Kuru yaz yangını", text = "Kuru otlar tutuştu. Yangınlar bitki örtüsünü ve havayı bozuyor: vakumla söndür.", icon = "poi_fire", favoured = EcoProblem.Wildfire },
        new Crisis { id = "oil_spill", title = "Petrol sızıntısı", text = "Bir tanker gölü kirletti. Oksijen düştü, balıklar zehirlendi.", icon = "region_lake_dirty", favoured = EcoProblem.DirtyLake },
        new Crisis { id = "heatwave", title = "Sıcak hava dalgası", text = "Depolar ve tarlalar su kaybetti. Kapalı depo ve sulama hazırlığı işe yarar.", icon = "w_sun", favoured = EcoProblem.Warming },
        new Crisis { id = "factory_leak", title = "Fabrika kaçağı", text = "Filtresiz bacalardan yoğun duman çıktı. Filtreli fabrikalar etkiyi azaltır.", icon = "poi_factory", favoured = EcoProblem.Smog },
        new Crisis { id = "slime_migration", title = "Balçık göçü", text = "Kirlilik yeni tehditleri çekiyor; bu gece daha çok düşman doğabilir.", icon = "poi_enemy", favoured = EcoProblem.Waste },
        new Crisis { id = "acid_rain", title = "Asit yağmuru", text = "Kirli bulutlar toprağı ve suyu asitlendirdi. Bitki örtüsü etkiyi azaltır.", icon = "w_rain", favoured = EcoProblem.Smog },
        new Crisis { id = "flash_flood", title = "Sel baskını", text = "Dereler taştı; göllere çamur ve atık indi. Bitki örtüsü güçlü kıyılar etkiyi azaltır.", icon = "w_rain", favoured = EcoProblem.DirtyLake },
        new Crisis { id = "inversion", title = "Sıcaklık terslenmesi", text = "Soğuk hava dumanı vadiye hapsetti. Filtreli fabrikalar ve şehir yeşili etkiyi azaltır.", icon = "carbon", favoured = EcoProblem.Smog },
        new Crisis { id = "invasive_species", title = "İstilacı tür", text = "Göllere yabancı bir tür yayıldı; balık stoku düşüyor. Oksijeni yüksek göller dayanır.", icon = "fish", favoured = EcoProblem.Warming },
        new Crisis { id = "illegal_dump", title = "Kaçak döküm", text = "Gece birileri vadiye atık döktü. Yığınları topla ve işle.", icon = "scrap", favoured = EcoProblem.Waste },
    };

    public static Crisis Find(string id) => All.FirstOrDefault(c => c.id == id);

    public static float Strength(int day, EcoDifficulty difficulty) => (0.6f + 0.25f * Mathf.Clamp(day, 1, 5)) * EcoScenario.DifficultyScale(difficulty);

    public static string Draw(int seed, int day, EcoProblem problem, string yesterday)
    {
        var rng = new System.Random(seed * 17 + day * 131 + 5);
        var pool = All.Where(c => c.id != yesterday).ToArray();
        float total = pool.Sum(c => c.favoured == problem ? 2.5f : 1f);
        double roll = rng.NextDouble() * total;
        foreach (var c in pool)
        {
            roll -= c.favoured == problem ? 2.5f : 1f;
            if (roll <= 0) return c.id;
        }
        return pool[pool.Length - 1].id;
    }

    /// <summary>Applies the crisis and returns a short line saying what actually happened.</summary>
    public static string Apply(string id, EcoRun run, int day)
    {
        float s = Strength(day, run.Scenario.difficulty);
        switch (id)
        {
            case "wildfire":
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(s * (run.State.fireGuardDay == day ? 0.6f : 1.5f)));
                int lit = run.SpawnFires(count, EcoRegionInfo.Kind.Grove, EcoRegionInfo.Kind.Wind, EcoRegionInfo.Kind.Sun);
                return lit + " yeni yangın çıktı";
            }
            case "oil_spill":
            {
                var lake = EcoRun.RegionOf(EcoRegionInfo.Kind.Lake);
                lake?.AddPollution(0f, 10f * s, 2f * s, 0f);
                foreach (var spot in Object.FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None))
                    if (lake != null && lake.Contains(spot.transform.position)) spot.Poison(1.5f * s);
                return "Gölde su kirliliği +" + (10f * s).ToString("0");
            }
            case "heatwave":
            {
                float loss = Mathf.Clamp01(0.12f * s);
                foreach (var node in Object.FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None))
                    if (node.kind == EcoWaterNode.Kind.Tank) node.cleanWater *= 1f - loss * 0.5f;
                    else if (node.kind == EcoWaterNode.Kind.Purifier) node.cleanWater *= 1f - loss;
                foreach (var plant in Object.FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None)) plant.moisture = Mathf.Max(0f, plant.moisture - 0.25f * s);
                foreach (var region in Object.FindObjectsByType<EcoRegion>(FindObjectsSortMode.None)) region.vegetation = Mathf.Max(0.03f, region.vegetation - 0.02f * s);
                return "Depolar %" + (loss * 50f).ToString("0") + "–" + (loss * 100f).ToString("0") + " su kaybetti; bitkiler kurudu";
            }
            case "factory_leak":
            {
                var industry = EcoRun.RegionOf(EcoRegionInfo.Kind.Industry);
                if (industry == null) return "";
                var factories = Object.FindObjectsByType<EcoFactory>(FindObjectsSortMode.None).Where(f => industry.Contains(f.transform.position)).ToArray();
                float open = factories.Length == 0 ? 1f : factories.Count(f => !f.Filtered) / (float)factories.Length;
                float guard = 0.3f + 0.7f * open;
                industry.AddPollution(14f * s * guard, 4f * s * guard, 6f * s * guard, 6f * s * guard, EcoCarbonFootprint.Source.Industry);
                return open <= 0f ? "Filtreler dumanın çoğunu tuttu" : "Sanayide hava kirliliği +" + (14f * s * guard).ToString("0");
            }
            case "slime_migration":
            {
                int extra = Mathf.Max(1, Mathf.RoundToInt(2f * s));
                foreach (var zone in Object.FindObjectsByType<EcoEnemySpawnZone>(FindObjectsSortMode.None))
                { zone.dailyBudget += extra; zone.pollutionThreshold = Mathf.Max(15f, zone.pollutionThreshold - 5f); }
                return "Düşman bütçesi bölge başına +" + extra;
            }
            case "acid_rain":
                foreach (var region in Object.FindObjectsByType<EcoRegion>(FindObjectsSortMode.None))
                {
                    float guard = 1f - 0.6f * region.vegetation;
                    region.AddPollution(0f, 5f * s * guard, 6f * s * guard, 0f);
                }
                return "Toprak ve su asitlendi; ormanlık bölgeler daha az etkilendi";
            case "flash_flood":
            {
                int lakes = 0;
                foreach (var info in Object.FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.None))
                {
                    if (info.kind != EcoRegionInfo.Kind.Lake || !info.TryGetComponent<EcoRegion>(out var lake)) continue;
                    float guard = 1f - 0.5f * lake.vegetation;
                    lake.AddPollution(0f, 6f * s * guard, 4f * s * guard, 0f); lakes++;
                }
                int piles = run.SpawnWaste(Mathf.RoundToInt(2f * s), EcoRegionInfo.Kind.Lake);
                return lakes == 0 ? "Sel vadiye zarar vermeden geçti" : lakes + " gölde su bulandı; " + piles + " atık yığını kıyıya vurdu";
            }
            case "inversion":
            {
                var industry = EcoRun.RegionOf(EcoRegionInfo.Kind.Industry);
                var factories = Object.FindObjectsByType<EcoFactory>(FindObjectsSortMode.None).Where(f => industry != null && industry.Contains(f.transform.position)).ToArray();
                float open = factories.Length == 0 ? 1f : factories.Count(f => !f.Filtered) / (float)factories.Length;
                var city = EcoRun.RegionOf(EcoRegionInfo.Kind.City);
                float green = city != null ? 1f - 0.6f * city.vegetation : 1f;
                float air = 9f * s * (0.35f + 0.65f * open) * green;
                city?.AddPollution(air, 0f, 0f, 0f);
                industry?.AddPollution(air * 0.6f, 0f, 0f, 0f);
                return "Şehirde hava kirliliği +" + air.ToString("0");
            }
            case "invasive_species":
            {
                float lost = 0f; int spots = 0;
                foreach (var spot in Object.FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None))
                {
                    var lake = EcoRegion.At(spot.transform.position);
                    float guard = lake != null && lake.DissolvedOxygen >= 7f ? 0.4f : 1f;
                    float loss = Mathf.Min(spot.population, 1.6f * s * guard);
                    spot.population -= loss; lost += loss; spots++;
                }
                return spots == 0 ? "Göllerde balık olmadığı için tür tutunamadı" : "Balık stoku toplam " + lost.ToString("0.0") + " düştü";
            }
            case "illegal_dump":
            {
                int piles = run.SpawnWaste(Mathf.RoundToInt(3f * s), EcoRegionInfo.Kind.City, EcoRegionInfo.Kind.Sun, EcoRegionInfo.Kind.Industry);
                return piles + " yeni atık yığını";
            }
        }
        return "";
    }
}
