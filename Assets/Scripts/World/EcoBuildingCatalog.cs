using UnityEngine;

public static class EcoBuildingCatalog
{
    public readonly struct Entry
    {
        public readonly string Id, Name, Prefab;
        public readonly int Metal, Plastic, Level;
        public Vector2 Footprint => Id == "wind" ? new Vector2(3, 3) : Id == "solar" ? new Vector2(2.1f, 1.5f)
            : Id == "purifier" ? new Vector2(2, 1.2f) : Id == "tank" ? new Vector2(1.6f, 1.6f)
            : Id == "recycling" ? new Vector2(2.4f, 1.7f) : Id == "farm" ? new Vector2(3, 3) : new Vector2(0.9f, 0.75f);
        public Entry(string id, string name, string prefab, int metal, int plastic, int level)
        { Id = id; Name = name; Prefab = prefab; Metal = metal; Plastic = plastic; Level = level; }
    }
    // Costs are prototype balance values; GDD unlock levels are kept.
    public static readonly Entry[] Entries =
    {
        new Entry("solar", "Güneş paneli", "SolarPanel", 12, 6, 1),
        new Entry("wind", "Rüzgâr türbini", "WindTurbine", 80, 40, 4),
        new Entry("battery", "Batarya", "Battery", 8, 6, 1),
        new Entry("charger", "Şarj istasyonu", "ChargingStation", 6, 4, 1),
        new Entry("intake", "Su pompası", "WaterIntake", 8, 8, 4),
        new Entry("purifier", "Su arıtma", "WaterPurifier", 20, 16, 4),
        new Entry("tank", "Su deposu", "WaterTank", 10, 14, 4),
        new Entry("recycling", "Geri dönüşüm", "RecyclingFacility", 16, 8, 2),
        new Entry("farm", "Tarla", "Farm", 2, 4, 1)
    };
    public static int Limit(int index, int level) => index == 0 ? level * 2 : index == 1 ? level < 4 ? 0 : ((level - 4) / 3 + 1) * 2 : 8;
    public static int Find(string id)
    {
        for (int i = 0; i < Entries.Length; i++) if (Entries[i].Id == id) return i;
        return -1;
    }
}
