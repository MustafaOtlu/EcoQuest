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
    public bool SupportsUpgrade => buildingId == "recycling" || buildingId == "intake" || buildingId == "purifier" || buildingId == "tank";
    public int NextUpgradeLevel => !SupportsUpgrade || tier >= 3 ? 0 : buildingId == "recycling" ? (tier == 1 ? 6 : 12) : (tier == 1 ? 12 : 24);
    public string UpgradeHint
    {
        get
        {
            if (!SupportsUpgrade) return "";
            if (tier >= 3) return "En yüksek kademe";
            int index = EcoBuildingCatalog.Find(buildingId); var entry = EcoBuildingCatalog.Entries[index];
            return "[U] Kademe " + (tier + 1) + " • YEP " + NextUpgradeLevel + " • " + entry.Metal * (tier + 1) + "M / " + entry.Plastic * (tier + 1) + "P";
        }
    }
    private int InvestmentMultiplier => tier * (tier + 1) / 2;
    private EcoPowerNode power;
    private void Awake() => power = GetComponent<EcoPowerNode>();
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
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
        float fraction = (100f - integrity) / 100f;
        if (!owner.TrySpendMaterials(Mathf.CeilToInt(entry.Metal * fraction * InvestmentMultiplier), Mathf.CeilToInt(entry.Plastic * fraction * InvestmentMultiplier))) return false;
        integrity = 100;
        if (power != null) power.condition = 1;
        owner.ShowFeedback(entry.Name + " onarıldı."); return true;
    }
    public bool Upgrade(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate || NextUpgradeLevel == 0) return false;
        if (!owner.Progression.HasLevel(NextUpgradeLevel)) { owner.ShowFeedback("Geliştirme için YEP " + NextUpgradeLevel + ". seviye gerekiyor."); return false; }
        if (integrity < 100) { owner.ShowFeedback("Geliştirmeden önce yapıyı tamir et."); return false; }
        var entry = EcoBuildingCatalog.Entries[EcoBuildingCatalog.Find(buildingId)];
        if (!owner.TrySpendMaterials(entry.Metal * (tier + 1), entry.Plastic * (tier + 1))) { owner.ShowFeedback("Geliştirme malzemesi yetersiz."); return false; }
        tier++;
        if (TryGetComponent<EcoWaterNode>(out var water)) { water.capacity *= 2; water.litresPerSecond *= 2; }
        if (TryGetComponent<EcoRecyclingFacility>(out var recycler)) { recycler.unitsPerSecond *= 2; recycler.storageCapacity *= 2; }
        owner.ShowFeedback(entry.Name + " kademe " + tier + " oldu. Kapasite ve işlem hızı arttı."); return true;
    }
    public bool Dismantle(PlayerWeaponSystem owner)
    {
        int index = EcoBuildingCatalog.Find(buildingId);
        if (owner == null || !owner.CanOperate || !playerBuilt || index < 0) return false;
        if (TryGetComponent<EcoFarm>(out var farm) && farm.HasCrops) { owner.ShowFeedback("Tarlayı sökmeden önce bitkileri hasat et veya kuruyanları gübreye dönüştür."); return false; }
        playerBuilt = false; // A second request in the same frame cannot refund twice.
        var entry = EcoBuildingCatalog.Entries[index];
        if (TryGetComponent<EcoRecyclingFacility>(out var recycler)) recycler.RecoverContents(owner);
        owner.ReturnMaterials(entry.Metal * InvestmentMultiplier / 2, entry.Plastic * InvestmentMultiplier / 2);
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
