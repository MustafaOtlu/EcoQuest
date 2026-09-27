using System;
using UnityEngine;

public sealed class EcoEquipmentUpgrades : MonoBehaviour
{
    public enum Upgrade { WaterTank, VacuumReach, VacuumPressure, SeedStorage, MultiSeed, SeedRange, RecyclerBatch, RecyclerSpeed, HeavyRecycling }
    public const int Count = 9;
    [SerializeField] private int[] levels = new int[Count];
    public PlayerWeaponSystem weapons;
    public int Rank(Upgrade item) => (int)item >= 0 && (int)item < Count ? levels[(int)item] : 0;
    public int Purchased { get { int total = 0; foreach (int rank in levels) total += rank; return total; } }
    public int Allowance => weapons != null && weapons.Progression != null ? weapons.Progression.Level * 3 : 0;
    public static readonly string[] Names = { "Su deposu", "Vakum menzili", "Vakum basıncı", "Tohum / top deposu", "Çoklu ekim", "Tohum menzili", "İşlem kapasitesi", "Geri dönüşüm hızı", "Büyük atık işleme" };
    private static readonly int[] BaseMetal = { 4, 6, 6, 3, 5, 5, 4, 6, 8 };
    private static readonly int[] BasePlastic = { 2, 3, 4, 4, 6, 4, 4, 4, 6 };
    public int RequiredLevel(Upgrade item) => Rank(item) switch { 0 => 2, 1 => 6, _ => 12 };
    public int MetalCost(Upgrade item) => BaseMetal[(int)item] * (1 << Rank(item));
    public int PlasticCost(Upgrade item) => BasePlastic[(int)item] * (1 << Rank(item));
    public float VacuumElectricity => 1 + 0.1f * (levels[0] + levels[1] + levels[2]);
    public float SeedElectricity => 1 + 0.1f * (levels[3] + levels[4] + levels[5]);
    public float RecyclerElectricity => 1 + 0.1f * (levels[6] + levels[7] + levels[8]);
    public int SeedCapacity => 40 + 20 * levels[3];
    public int AmmoCapacity => 100 + 50 * levels[3];
    public int SeedVolley => 1 + levels[4];
    public float SeedVelocity => 18 * (1 + 0.2f * levels[5]);
    public float CollectionRadius => levels[1] * 0.15f;
    public float VacuumRangeBonus => levels[1] * 2;
    public float VacuumStrength => 1 + levels[2] * 0.2f;
    public int RecyclingBatch => 8 + 4 * levels[6];
    public float RecyclingSpeed => 1 + 0.35f * levels[7];
    public bool CanBuy(Upgrade item, out string reason)
    {
        reason = "Geliştirme kullanılamıyor.";
        if ((int)item < 0 || (int)item >= Count || weapons == null || !weapons.CanOperate) return false;
        if (Rank(item) >= 3) { reason = "En yüksek kademe"; return false; }
        if (!weapons.Progression.HasLevel(RequiredLevel(item))) { reason = "YEP " + RequiredLevel(item) + ". seviye gerekli"; return false; }
        if (Purchased >= Allowance) { reason = "Yeni hak için YEP seviyeni artır"; return false; }
        if (weapons.Metal < MetalCost(item) || weapons.Plastic < PlasticCost(item)) { reason = "Malzeme yetersiz"; return false; }
        reason = "Geliştir"; return true;
    }
    public bool TryBuy(Upgrade item)
    {
        if (!CanBuy(item, out string reason)) { weapons?.ShowFeedback(reason); return false; }
        if (!weapons.TrySpendMaterials(MetalCost(item), PlasticCost(item))) return false;
        levels[(int)item]++; weapons.ApplyEquipmentCapacity();
        weapons.ShowFeedback(Names[(int)item] + " • Kademe " + Rank(item)); return true;
    }
    public string Description(Upgrade item) => item switch
    {
        Upgrade.WaterTank => "+50 su kapasitesi; dolum sağlamaz",
        Upgrade.VacuumReach => "+2 m erişim, daha geniş toplama",
        Upgrade.VacuumPressure => "+%20 vakum ve su etkisi",
        Upgrade.SeedStorage => "+20 tohum, +50 demir top kapasitesi",
        Upgrade.MultiSeed => "Atışta +1 tohum; her tohum harcanır",
        Upgrade.SeedRange => "+%20 tohum çıkış hızı ve erişim",
        Upgrade.RecyclerBatch => "R ile tek işlemde +4 atık",
        Upgrade.RecyclerSpeed => "Yerdeki atığı +%35 hızlı işler",
        _ => "Daha büyük atıkların kilidini açar"
    };
    public int[] Capture() => (int[])levels.Clone();
    public void Restore(int[] saved)
    {
        Array.Clear(levels, 0, levels.Length);
        if (saved != null)
            for (int i = 0; i < Mathf.Min(Count, saved.Length); i++) levels[i] = Mathf.Clamp(saved[i], 0, 3);
        weapons?.ApplyEquipmentCapacity();
    }
}
