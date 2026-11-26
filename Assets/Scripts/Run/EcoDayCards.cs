using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Morning choice: three cards (four with the meta unlock), one is taken. Policy, technology and grant cards;
/// some carry a cost. The draw is deterministic per seed and day; effects act on the living world.</summary>
public static class EcoDayCards
{
    public sealed class Card
    {
        public string id, title, text, icon, cost = "";
        public bool repeatable = true;
        public EcoProblem? favoured;
    }

    public static readonly Card[] All =
    {
        new Card { id = "grant_metal", title = "Belediye hibesi", text = "+30 metal, +10 plastik", icon = "metal" },
        new Card { id = "grant_plastic", title = "Geri dönüşüm fonu", text = "+30 plastik, +10 metal", icon = "metal" },
        new Card { id = "bike_lanes", title = "Bisiklet yolları", text = "Şehirde karbon −12, hava kirliliği −8", icon = "region_city", repeatable = false, favoured = EcoProblem.Smog },
        new Card { id = "wind_survey", title = "Rüzgâr ölçümü", text = "Run boyunca rüzgâr türbinleri %25 fazla üretir", icon = "electricity", repeatable = false, favoured = EcoProblem.Warming },
        new Card { id = "panel_cleaning", title = "Panel temizliği", text = "Run boyunca güneş panelleri %20 fazla üretir", icon = "electricity", repeatable = false, favoured = EcoProblem.Smog },
        new Card { id = "volunteers", title = "Gönüllü temizlik", text = "8 atık yığını kendiliğinden toplanır, malzemesi sana gelir", icon = "scrap", favoured = EcoProblem.Waste },
        new Card { id = "saplings", title = "Fidan kampanyası", text = "Ormanda örtü +%12, çantana 6 ağaç tohumu", icon = "poi_tree", favoured = EcoProblem.Wildfire },
        new Card { id = "reed_beds", title = "Kıyı sazlıkları", text = "Kirli gölde su kirliliği −12", icon = "water_quality", favoured = EcoProblem.DirtyLake },
        new Card { id = "fire_crew", title = "Yangın ekibi", text = "Bütün yangınlar söner; bu akşamki yangın krizi zayıflar", icon = "poi_fire", favoured = EcoProblem.Wildfire },
        new Card { id = "science_team", title = "Bilim ekibi", text = "Run boyunca bölgeler %25 daha hızlı toparlanır", icon = "biodiversity", repeatable = false, favoured = EcoProblem.Warming },
        new Card { id = "compost_coop", title = "Kompost kooperatifi", text = "+6 kompost, +10 plastik", icon = "compost" },
        new Card { id = "factory_overtime", title = "Fabrika fazla mesaisi", text = "+45 metal, +15 plastik", cost = "Bedel: sanayide hava +15, karbon +10", icon = "poi_factory" },
        new Card { id = "tree_nursery", title = "Fidanlık", text = "Çantana 12 ağaç tohumu", icon = "seed", favoured = EcoProblem.Wildfire },
        new Card { id = "stream_cleanup", title = "Dere temizliği", text = "Bütün göllerde su kirliliği −8, toprak −4", icon = "water_quality", favoured = EcoProblem.DirtyLake },
        new Card { id = "green_roofs", title = "Yeşil çatılar", text = "Şehirde hava −6, toprak −6, bitki örtüsü +%8", icon = "region_city", repeatable = false, favoured = EcoProblem.Warming },
        new Card { id = "carbon_tax", title = "Karbon vergisi", text = "Sanayide karbon −15, hava −10", cost = "Bedel: 15 metal", icon = "carbon", favoured = EcoProblem.Smog },
        new Card { id = "night_patrol", title = "Gece devriyesi", text = "Run boyunca her bölgede günde 2 tehdit daha az doğar", icon = "poi_enemy", repeatable = false, favoured = EcoProblem.Waste },
        new Card { id = "scrap_auction", title = "Hurda müzayedesi", text = "+25 metal, +25 plastik", cost = "Bedel: vadiye 3 yeni atık yığını dökülür", icon = "scrap" },
        new Card { id = "lake_dredging", title = "Göl tabanı taraması", text = "Kirli gölde su −20; balık stoku +2", cost = "Bedel: 20 metal", icon = "region_lake_dirty", favoured = EcoProblem.DirtyLake },
    };

    public static Card Find(string id) => All.FirstOrDefault(c => c.id == id);

    public static string[] Draw(int seed, int day, EcoProblem problem, ICollection<string> taken, int count)
    {
        var rng = new System.Random(seed * 31 + day * 977 + 11);
        var pool = All.Where(c => c.repeatable || taken == null || !taken.Contains(c.id)).ToList();
        var result = new List<string>();
        while (result.Count < count && pool.Count > 0)
        {
            float total = pool.Sum(c => c.favoured == problem ? 2.2f : 1f);
            double roll = rng.NextDouble() * total;
            var chosen = pool[pool.Count - 1];
            foreach (var c in pool)
            {
                roll -= c.favoured == problem ? 2.2f : 1f;
                if (roll <= 0) { chosen = c; break; }
            }
            result.Add(chosen.id); pool.Remove(chosen);
        }
        return result.ToArray();
    }

    /// <summary>Applies one card. Returns the feedback line, or null when it cannot be taken (e.g. not enough metal).</summary>
    public static string Apply(string id, EcoRun run)
    {
        var player = Object.FindFirstObjectByType<PlayerWeaponSystem>();
        switch (id)
        {
            case "grant_metal": player?.ReturnMaterials(30, 10); return "Hibe geldi: +30 metal, +10 plastik";
            case "grant_plastic": player?.ReturnMaterials(10, 30); return "Fon geldi: +30 plastik, +10 metal";
            case "bike_lanes": EcoRun.RegionOf(EcoRegionInfo.Kind.City)?.AddPollution(-8f, 0f, 0f, -12f); return "Bisiklet yolları açıldı: şehirde karbon ve duman azaldı";
            case "wind_survey": run.State.windBoost *= 1.25f; return "Rüzgâr ölçüldü: türbinler daha verimli";
            case "panel_cleaning": run.State.solarBoost *= 1.2f; return "Paneller temizlendi: güneş üretimi arttı";
            case "volunteers":
            {
                int metal = 0, plastic = 0, piles = 0;
                foreach (var waste in Object.FindObjectsByType<RecyclableResource>(FindObjectsSortMode.InstanceID))
                {
                    if (piles >= 8 || waste == null || waste.Claimed || waste.GetComponent<EnemyLootDrop>() != null) continue;
                    metal += waste.metal; plastic += waste.plastic;
                    if (waste.TryClaim()) piles++;
                }
                player?.ReturnMaterials(metal, plastic);
                return piles > 0 ? "Gönüllüler " + piles + " yığın topladı: +" + metal + " metal, +" + plastic + " plastik" : "Toplanacak atık yığını kalmamış; gönüllüler teşekkür etti";
            }
            case "saplings":
            {
                var grove = EcoRun.RegionOf(EcoRegionInfo.Kind.Grove) ?? EcoRun.RegionOf(EcoRegionInfo.Kind.Wind);
                if (grove != null) grove.vegetation = Mathf.Clamp01(grove.vegetation + 0.12f);
                player?.AddSeeds(EcoCropCatalog.Kind.Tree, 6);
                return "Fidanlar dikildi: orman örtüsü arttı, +6 ağaç tohumu";
            }
            case "reed_beds": EcoRun.RegionOf(EcoRegionInfo.Kind.Lake)?.AddPollution(0f, -12f, 0f, 0f); return "Sazlıklar suyu süzüyor: göl biraz temizlendi";
            case "fire_crew":
                foreach (var fire in Object.FindObjectsByType<EcoFire>(FindObjectsSortMode.None)) fire.Extinguish(1000f);
                run.State.fireGuardDay = run.RunDay;
                return "Yangın ekibi görevde: yangınlar söndü";
            case "science_team": run.State.recoveryBoost *= 1.25f; return "Bilim ekibi geldi: doğa daha hızlı toparlanacak";
            case "compost_coop": player?.ReturnMaterials(0, 10, 6); return "Kooperatiften +6 kompost, +10 plastik";
            case "factory_overtime":
                player?.ReturnMaterials(45, 15);
                EcoRun.RegionOf(EcoRegionInfo.Kind.Industry)?.AddPollution(15f, 0f, 0f, 10f, EcoCarbonFootprint.Source.Industry);
                return "Fabrikalar fazla çalıştı: +45 metal, +15 plastik; sanayinin havası kötüleşti";
            case "tree_nursery": player?.AddSeeds(EcoCropCatalog.Kind.Tree, 12); return "Fidanlıktan +12 ağaç tohumu";
            case "stream_cleanup":
            {
                int lakes = 0;
                foreach (var info in Object.FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.None))
                    if (info.kind == EcoRegionInfo.Kind.Lake && info.TryGetComponent<EcoRegion>(out var lake)) { lake.AddPollution(0f, -8f, -4f, 0f); lakes++; }
                return lakes > 0 ? "Dereler temizlendi: " + lakes + " gölün suyu iyileşti" : "Bu vadide temizlenecek göl yok";
            }
            case "green_roofs":
            {
                var city = EcoRun.RegionOf(EcoRegionInfo.Kind.City);
                if (city != null) { city.AddPollution(-6f, 0f, -6f, 0f); city.vegetation = Mathf.Clamp01(city.vegetation + 0.08f); }
                return "Çatılar yeşillendi: şehrin havası ve toprağı iyileşti";
            }
            case "carbon_tax":
                if (player == null || !player.TrySpendMaterials(15, 0)) return null;
                EcoRun.RegionOf(EcoRegionInfo.Kind.Industry)?.AddPollution(-10f, 0f, 0f, -15f);
                return "Karbon vergisi yürürlükte: sanayi daha az kirletiyor";
            case "night_patrol":
                foreach (var zone in Object.FindObjectsByType<EcoEnemySpawnZone>(FindObjectsSortMode.None)) zone.dailyBudget = Mathf.Max(1, zone.dailyBudget - 2);
                run.State.patrol = true;
                return "Devriye başladı: tehditler daha seyrek doğacak";
            case "scrap_auction":
            {
                player?.ReturnMaterials(25, 25);
                int piles = run.SpawnWaste(3, EcoRegionInfo.Kind.City, EcoRegionInfo.Kind.Sun, EcoRegionInfo.Kind.Industry);
                return "Müzayede: +25 metal, +25 plastik; " + piles + " yeni atık yığını";
            }
            case "lake_dredging":
                if (player == null || !player.TrySpendMaterials(20, 0)) return null;
                EcoRun.RegionOf(EcoRegionInfo.Kind.Lake)?.AddPollution(0f, -20f, 0f, 0f);
                foreach (var spot in Object.FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None)) spot.population = Mathf.Min(12f, spot.population + 2f);
                return "Göl tabanı tarandı: su temizlendi, balık arttı";
        }
        return "";
    }
}
