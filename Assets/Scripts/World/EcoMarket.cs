using UnityEngine;

public sealed class EcoMarket : MonoBehaviour
{
    public enum Offer { IronAmmo, Seeds, SmallFilter, LargeFilter, SellProduce, SellSeeds, CarrotSeeds, TomatoSeeds, TreeSeeds, SellFish }
    public static readonly string[] Titles = { "5 demir top", "5 lahana tohumu", "Küçük hava filtresi", "Büyük hava filtresi", "1 seçili ürün sat", "5 seçili tohum sat", "5 havuç tohumu", "5 domates tohumu", "5 ağaç tohumu", "1 temiz balık sat" };
    public static readonly string[] Details = { "Demir sapan için 5 adet mühimmat", "8–28 °C • Hasat: 2 ürün", "Sersemleyen Teneke için", "Filtresiz fabrikalar için", "Tohum türüne göre temiz ürün takası", "+1 plastik • R ile tohum türü seç", "5–22 °C • Hasat: 3 ürün", "18–34 °C • Hasat: 4 ürün", "Açık toprağa dik; kalıcı habitat", "+4 metal ve +2 plastik" };
    // Purchases (not sales) may be limited per world day (GDD §15.5); enemy loot itself is unlimited.
    // Order follows the Offer enum: IronAmmo, Seeds, SmallFilter, LargeFilter, SellProduce, SellSeeds, CarrotSeeds, TomatoSeeds, TreeSeeds. 0 = no limit.
    public static readonly int[] DailyLimits = { 8, 4, 4, 2, 0, 0, 3, 3, 3, 0 };
    private static readonly int[] boughtToday = new int[10];
    public static int BoughtDay { get { SyncDay(); return boughtDay; } }
    public static int[] CaptureDaily() { SyncDay(); return (int[])boughtToday.Clone(); }
    public static void RestoreDaily(int day, int[] data)
    { boughtDay = day; System.Array.Clear(boughtToday, 0, boughtToday.Length); if (data != null) for (int i = 0; i < Mathf.Min(data.Length, boughtToday.Length); i++) boughtToday[i] = Mathf.Clamp(data[i], 0, DailyLimits[i]); }
    private static int boughtDay = -1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDaily() { boughtDay = -1; System.Array.Clear(boughtToday, 0, boughtToday.Length); }
    private static void SyncDay()
    {
        var clock = EcoWorldClock.Current; int day = clock != null ? clock.day : 1;
        if (day == boughtDay) return;
        boughtDay = day; System.Array.Clear(boughtToday, 0, boughtToday.Length);
    }
    public static int RemainingToday(Offer offer)
    {
        int cap = DailyLimits[(int)offer];
        if (cap <= 0) return int.MaxValue;
        SyncDay();
        return Mathf.Max(0, cap - boughtToday[(int)offer]);
    }
    public static bool WithinDailyLimit(Offer offer, out string reason)
    {
        reason = ""; if (!IsPurchase(offer) || RemainingToday(offer) > 0) return true;
        reason = "Günlük sınır doldu"; return false;
    }
    public static bool IsSeedOffer(Offer offer) => offer == Offer.Seeds || offer == Offer.CarrotSeeds || offer == Offer.TomatoSeeds || offer == Offer.TreeSeeds;
    public static bool IsPurchase(Offer offer) => offer != Offer.SellProduce && offer != Offer.SellSeeds && offer != Offer.SellFish;
    public static int MetalPrice(Offer offer) => offer == Offer.IronAmmo ? 3 : offer == Offer.SmallFilter ? 2 : offer == Offer.LargeFilter ? 6 : 0;
    public static int PlasticPrice(Offer offer) => offer == Offer.Seeds ? 2 : offer == Offer.SmallFilter ? 1 : offer == Offer.LargeFilter ? 4 : IsSeedOffer(offer) ? 3 : 0;
    public static int Level(Offer offer) => 1;
    public static Vector2Int ProduceValue(EcoCropCatalog.Kind kind) => kind == EcoCropCatalog.Kind.Cabbage ? new Vector2Int(2, 1) : kind == EcoCropCatalog.Kind.Carrot ? new Vector2Int(2, 2) : kind == EcoCropCatalog.Kind.Tomato ? new Vector2Int(3, 2) : Vector2Int.zero;
    public bool CanReach(PlayerWeaponSystem player) => isActiveAndEnabled && player != null && player.CanOperate
        && Vector3.Distance(player.transform.position, GetComponent<Collider>() != null ? GetComponent<Collider>().bounds.ClosestPoint(player.transform.position) : transform.position) <= 3f;
    public void Interact(PlayerWeaponSystem player) { if (CanReach(player)) EcoMarketUI.Open(this, player); }
    public bool Trade(PlayerWeaponSystem player, Offer offer)
    {
        if (!CanReach(player)) return false;
        if (!WithinDailyLimit(offer, out _)) { player.ShowFeedback("Bugünkü satın alma sınırı doldu • yarın yeniden dene."); return false; }
        bool ok = player.ExecuteMarketTrade(offer);
        if (ok && IsPurchase(offer) && DailyLimits[(int)offer] > 0) { SyncDay(); boughtToday[(int)offer]++; }
        return ok;
    }
    public bool Upgrade(PlayerWeaponSystem player, EcoEquipmentUpgrades.Upgrade item) => CanReach(player) && player.Upgrades.TryBuy(item);
    public string Describe() => "MARKET • " + EcoKeys.Tag(EcoKeys.Action.Interact) + " Alışveriş ve ekipman geliştirmeleri";
}
