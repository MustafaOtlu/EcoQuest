using UnityEngine;

public sealed class EcoMarket : MonoBehaviour
{
    public enum Offer { IronAmmo, Seeds, SmallFilter, LargeFilter, SellProduce, SellSeeds, CarrotSeeds, TomatoSeeds, TreeSeeds }
    public static readonly string[] Titles = { "5 demir top", "5 lahana tohumu", "Küçük hava filtresi", "Büyük hava filtresi", "1 ürün sat", "5 seçili tohum sat", "5 havuç tohumu", "5 domates tohumu", "5 ağaç tohumu" };
    public static readonly string[] Details = { "Demir sapan için mühimmat", "8–28 °C • Temel sebze", "Sersemleyen Teneke için", "Filtresiz fabrikalar için", "+1 metal ve +1 plastik", "+1 plastik • Tohum silahında R ile tür seç", "5–22 °C • Hasat: 2 ürün", "18–34 °C • Hasat: 3 ürün", "4–36 °C • Açık toprağa dik; ekosistemi iyileştirir" };
    public static bool IsSeedOffer(Offer offer) => offer == Offer.Seeds || offer == Offer.CarrotSeeds || offer == Offer.TomatoSeeds || offer == Offer.TreeSeeds;
    public static bool IsPurchase(Offer offer) => offer != Offer.SellProduce && offer != Offer.SellSeeds;
    public static int MetalPrice(Offer offer) => offer == Offer.IronAmmo ? 1 : offer == Offer.SmallFilter ? 3 : offer == Offer.LargeFilter ? 12 : 0;
    public static int PlasticPrice(Offer offer) => offer == Offer.Seeds || offer == Offer.SmallFilter ? 2 : offer == Offer.LargeFilter ? 8 : offer == Offer.CarrotSeeds ? 3 : offer == Offer.TomatoSeeds ? 4 : offer == Offer.TreeSeeds ? 5 : 0;
    public static int Level(Offer offer) => offer == Offer.SmallFilter || offer == Offer.CarrotSeeds || offer == Offer.TreeSeeds ? 2 : offer == Offer.LargeFilter || offer == Offer.TomatoSeeds ? 4 : 1;
    public bool CanReach(PlayerWeaponSystem player) => isActiveAndEnabled && player != null && player.CanOperate
        && Vector3.Distance(player.transform.position, GetComponent<Collider>() != null ? GetComponent<Collider>().ClosestPoint(player.transform.position) : transform.position) <= 3f;
    public void Interact(PlayerWeaponSystem player) { if (CanReach(player)) EcoMarketUI.Open(this, player); }
    public bool Trade(PlayerWeaponSystem player, Offer offer) => CanReach(player) && player.ExecuteMarketTrade(offer);
    public bool Upgrade(PlayerWeaponSystem player, EcoEquipmentUpgrades.Upgrade item) => CanReach(player) && player.Upgrades.TryBuy(item);
    public string Describe() => "MARKET • [E] Alışveriş ve ekipman geliştirmeleri";
}
