using UnityEngine;

public static class EcoBuildingCatalog
{
    public readonly struct Entry
    {
        public readonly string Id, Name, Prefab;
        public readonly int Metal, Plastic, Level;
        public Vector2 Footprint => Id == "wind" || Id == "hydro" || Id == "farm" ? new Vector2(6, 6)
            : Id == "solar" || Id == "purifier" || Id == "tank" || Id == "recycling" ? new Vector2(4, 4) : new Vector2(2, 2);
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
        new Entry("farm", "Tarla", "Farm", 2, 4, 1),
        new Entry("hydro", "Küçük hidroelektrik", "Hydro", 60, 30, 1)
    };
    /// <summary>One-line purpose shown on the build slot tooltip.</summary>
    public static string Purpose(string id) => id switch
    {
        "solar" => "Gündüz elektrik üretir; bulutta ve gece düşer.",
        "wind" => "Rüzgâr güçlüyken çok elektrik üretir; geniş alan ister.",
        "battery" => "Fazla elektriği saklar, kesintide ağı besler.",
        "charger" => "Ağdan aldığı elektrikle araç bataryanı doldurur.",
        "intake" => "Göl ya da sudan su çeker; elektrik ister.",
        "purifier" => "Kirli suyu temizler; boru ile pompa ve depoya bağlanır.",
        "tank" => "Arıtılmış suyu saklar; yanında E ile vakuma temiz su doldurursun.",
        "recycling" => "Atığı metal, plastik ve gübreye çevirir.",
        "farm" => "Tohum ekilen tarla; sulanınca ürün verir.",
        "hydro" => "Doğal akan suda sürekli elektrik üretir; balık geçidi var.",
        _ => "",
    };
    public static int Limit(int index, int level) => int.MaxValue;
    public static GameObject CreateInstance(int index, Vector3 position, Quaternion rotation)
    {
        if (index < 0 || index >= Entries.Length) return null;
        var entry = Entries[index];
        var prefab = Resources.Load<GameObject>("EcoInfrastructure/" + entry.Prefab);
        if (prefab == null && entry.Id != "hydro") return null;
        var go = prefab != null ? Object.Instantiate(prefab, position, rotation) : EcoHydroArt.Create(position, rotation);
        var structure = go.GetComponent<EcoStructure>(); if (structure == null) structure = go.AddComponent<EcoStructure>();
        structure.buildingId = entry.Id;
        if (go.TryGetComponent<EcoPowerNode>(out var power))
        {
            if (power.kind == EcoPowerNode.Kind.Solar) power.ratedPower = 4;
            if (power.kind == EcoPowerNode.Kind.Wind || power.kind == EcoPowerNode.Kind.Hydro) power.ratedPower = 6;
            if (power.kind == EcoPowerNode.Kind.Battery) power.capacity = 240;
        }
        if (go.TryGetComponent<EcoChargingStation>(out var charging)) charging.chargeRate = 4;
        if (go.TryGetComponent<EcoWaterNode>(out var water)) { water.capacity = 120; water.litresPerSecond = 1; water.electricityPerLitre = water.kind == EcoWaterNode.Kind.Intake ? 1 : 2; }
        if (go.TryGetComponent<EcoFarm>(out var farm)) { farm.growingArea = new Vector2(5,5); farm.irrigationPerSecond = 1; }
        if (go.TryGetComponent<EcoRecyclingFacility>(out var recycler)) recycler.unitsPerSecond = 1f / 3f;
        return go;
    }
    public static int Find(string id)
    {
        for (int i = 0; i < Entries.Length; i++) if (Entries[i].Id == id) return i;
        return -1;
    }
}
