using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EcoStructure : MonoBehaviour
{
    internal static readonly HashSet<EcoStructure> Active = new HashSet<EcoStructure>();
    public string buildingId;
    public bool playerBuilt;
    [Range(0f, 100f)] public float integrity = 100f;
    [Range(1, 3)] public int tier = 1;
    // GDD §9: production/storage facilities have three tiers. Tier 2 needs two completed projects and the base cost
    // again; tier 3 needs four projects and twice the base cost. Capacity/throughput is 1× / 1.5× / 2×; the cost per
    // unit processed stays the same, so a faster pump also draws more power.
    public bool SupportsUpgrade => buildingId == "recycling" || buildingId == "intake" || buildingId == "purifier" || buildingId == "tank" || buildingId == "battery";
    public int NextUpgradeProjects => !SupportsUpgrade || tier >= 3 ? 0 : tier == 1 ? 2 : 4;
    public static float TierScale(int t) => 1f + 0.5f * (Mathf.Clamp(t, 1, 3) - 1);
    public string UpgradeHint
    {
        get
        {
            if (!SupportsUpgrade) return "";
            if (tier >= 3) return "En yüksek kademe";
            int index = EcoBuildingCatalog.Find(buildingId); var entry = EcoBuildingCatalog.Entries[index];
            return EcoKeys.Tag(EcoKeys.Action.Upgrade) + " Kademe " + (tier + 1) + " (×" + TierScale(tier + 1).ToString("0.#") + ") • " + NextUpgradeProjects + " proje • "
                + entry.Metal * tier + "M / " + entry.Plastic * tier + "P";
        }
    }
    private EcoPowerNode power;
    private void Awake() => power = GetComponent<EcoPowerNode>();
    private void OnEnable() { Active.Add(this); EcoNavigation.MarkDirty(); }
    private void OnDisable() { Active.Remove(this); EcoNavigation.MarkDirty(); }
    private void Update() => SimulateEcology(Time.deltaTime);
    public void SimulateEcology(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds) || !playerBuilt || integrity <= 0 || (buildingId != "wind" && buildingId != "recycling")) return;
        var region = EcoRegion.At(transform.position);
        if (region == null || !region.sensitiveHabitat) return;
        region.vegetation = Mathf.Max(0, region.vegetation - seconds * 0.0005f);
        region.AddPollution(0, 0, seconds * 0.015f, 0);
    }
    public void ReceiveDamage(float amount)
    {
        if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        integrity = Mathf.Max(0, integrity - amount);
        if (power != null) power.condition = integrity / 100f;
    }
    public bool Repair(PlayerWeaponSystem owner)
    {
        int index = EcoBuildingCatalog.Find(buildingId);
        if (owner == null || index < 0 || integrity >= 100f) return false;
        var entry = EcoBuildingCatalog.Entries[index];
        // GDD §9: repair = missing integrity × 25 % of the base materials, rounded up.
        float fraction = (100f - integrity) / 100f;
        if (!owner.TrySpendMaterials(Mathf.CeilToInt(entry.Metal * fraction * 0.25f), Mathf.CeilToInt(entry.Plastic * fraction * 0.25f))) return false;
        integrity = 100;
        if (power != null) power.condition = 1;
        owner.ShowFeedback(entry.Name + " onarıldı."); return true;
    }
    public void RestoreTier()
    {
        if (tier >= 3) return;
        tier++;
        ApplyTierScale(tier - 1, tier);
    }
    private void ApplyTierScale(int from, int to)
    {
        float k = TierScale(to) / TierScale(from);
        if (TryGetComponent<EcoWaterNode>(out var water)) { water.capacity *= k; water.litresPerSecond *= k; }
        if (TryGetComponent<EcoRecyclingFacility>(out var recycler)) { recycler.unitsPerSecond *= k; recycler.storageCapacity = Mathf.RoundToInt(recycler.storageCapacity * k); }
        if (buildingId == "battery" && power != null) { power.capacity *= k; power.batteryRate *= k; }
    }
    public bool Upgrade(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate || NextUpgradeProjects == 0) return false;
        int done = EcoProjects.Current != null ? EcoProjects.Current.CompletedCount : 0;
        if (done < NextUpgradeProjects) { owner.ShowFeedback("Kademe " + (tier + 1) + " için " + NextUpgradeProjects + " tamamlanmış proje gerekiyor (" + done + "/" + NextUpgradeProjects + ")."); return false; }
        if (integrity < 100) { owner.ShowFeedback("Geliştirmeden önce yapıyı tamir et."); return false; }
        var entry = EcoBuildingCatalog.Entries[EcoBuildingCatalog.Find(buildingId)];
        if (!owner.TrySpendMaterials(entry.Metal * tier, entry.Plastic * tier)) { owner.ShowFeedback("Geliştirme malzemesi yetersiz."); return false; }
        tier++;
        ApplyTierScale(tier - 1, tier);
        owner.ShowFeedback(entry.Name + " kademe " + tier + " oldu • kapasite ×" + TierScale(tier).ToString("0.#") + "."); return true;
    }
    public bool Dismantle(PlayerWeaponSystem owner)
    {
        int index = EcoBuildingCatalog.Find(buildingId);
        if (owner == null || !owner.CanOperate || !playerBuilt || index < 0) return false;
        if (TryGetComponent<EcoFarm>(out var farm) && farm.HasCrops) { owner.ShowFeedback("Tarlayı sökmeden önce bitkileri hasat et veya kuruyanları gübreye dönüştür."); return false; }
        playerBuilt = false; // A second request in the same frame cannot refund twice.
        var entry = EcoBuildingCatalog.Entries[index];
        if (TryGetComponent<EcoRecyclingFacility>(out var recycler)) recycler.RecoverContents(owner);
        owner.ReturnMaterials(entry.Metal / 2, entry.Plastic / 2); // GDD §9: dismantling returns 50 % of the base cost
        gameObject.SetActive(false); Destroy(gameObject);
        owner.ShowFeedback("Yapı söküldü • Malzemenin yarısı geri alındı."); return true;
    }
    private void OnDestroy()
    {
        if (power != null)
            foreach (var cable in EcoPowerCable.Active.ToArray())
                if (cable != null && (cable.a == power || cable.b == power)) Destroy(cable.gameObject);
        var water = GetComponent<EcoWaterNode>();
        if (water != null)
            foreach (var pipe in EcoWaterPipe.Active.ToArray())
                if (pipe != null && (pipe.from == water || pipe.to == water)) Destroy(pipe.gameObject);
    }
}
