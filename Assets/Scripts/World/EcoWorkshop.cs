using System.Collections.Generic;
using UnityEngine;

// GDD §8 "Çıkmaza girmeme": the centre never leaves the player stuck.
//  • Emergency charge: with an empty vehicle battery, 30 simulated seconds next to the market give 15 EB.
//  • Basic rescue: with no seeds and no food, or under 2 SB water, 1 cabbage seed + 2 SB once per world day.
//  • Workshop: once the daily ammo/filter quota is used, the same recipe is processed at 45 s per pack from the
//    player's own materials. The panel can be closed; production continues. It never creates materials.
public sealed class EcoWorkshop : MonoBehaviour
{
    public const float ChargeSeconds = 30f, ChargeAmount = 15f, PackSeconds = 45f;
    public const int MaxQueue = 3;
    private PlayerWeaponSystem player;
    private readonly List<EcoMarket.Offer> queue = new List<EcoMarket.Offer>();
    private EcoMarket chargeAt;
    private int rescueDay = -1;
    public float ChargeProgress { get; private set; }
    public float PackProgress { get; private set; }
    public bool Charging => chargeAt != null;
    public IReadOnlyList<EcoMarket.Offer> Queue => queue;

    public static EcoWorkshop For(PlayerWeaponSystem player)
    {
        if (player == null) return null;
        if (!player.TryGetComponent<EcoWorkshop>(out var shop)) { shop = player.gameObject.AddComponent<EcoWorkshop>(); shop.player = player; }
        return shop;
    }
    private void Awake() { player = GetComponent<PlayerWeaponSystem>(); }

    public static bool Craftable(EcoMarket.Offer offer) => offer == EcoMarket.Offer.IronAmmo || offer == EcoMarket.Offer.SmallFilter || offer == EcoMarket.Offer.LargeFilter;

    public bool CanCharge(out string reason)
    {
        reason = "";
        if (Charging) { reason = "Şarj sürüyor"; return false; }
        if (player.Energy >= 1f) { reason = "Yalnız araç elektriği bitince"; return false; }
        return true;
    }
    public bool StartCharge(EcoMarket market)
    {
        if (market == null || !market.CanReach(player) || !CanCharge(out string reason)) return false;
        chargeAt = market; ChargeProgress = 0f;
        player.ShowFeedback("Acil şarj başladı • 30 sn markete yakın kal (paneli kapatabilirsin).");
        return true;
    }

    public bool CanRescue(out string reason)
    {
        reason = "";
        var clock = EcoWorldClock.Current; int day = clock != null ? clock.day : 1;
        bool needSeed = player.TotalSeeds == 0 && player.Food == 0, needWater = player.Water < 2f;
        if (!needSeed && !needWater) { reason = "Tohum ve su yeterli"; return false; }
        if (rescueDay == day) { reason = "Bugün alındı • yarın"; return false; }
        return true;
    }
    public bool Rescue()
    {
        if (!CanRescue(out _)) return false;
        var clock = EcoWorldClock.Current; rescueDay = clock != null ? clock.day : 1;
        player.EmergencySupplies();
        player.ShowFeedback("Temel destek: 1 lahana tohumu ve ilk sulama için 2 SB temiz su.");
        return true;
    }

    public bool CanQueue(EcoMarket.Offer offer, out string reason)
    {
        reason = "";
        if (!Craftable(offer)) { reason = "Atölyede yapılmaz"; return false; }
        if (EcoMarket.RemainingToday(offer) > 0) { reason = "Önce günlük stoktan al"; return false; }
        if (queue.Count >= MaxQueue) { reason = "Sıra dolu (" + MaxQueue + ")"; return false; }
        if (!player.CanMarketTrade(offer, out reason)) return false;
        return true;
    }
    public bool Enqueue(EcoMarket.Offer offer)
    {
        if (!CanQueue(offer, out string reason)) { player.ShowFeedback(reason); return false; }
        queue.Add(offer); if (queue.Count == 1) PackProgress = 0f;
        player.ShowFeedback(EcoMarket.Titles[(int)offer] + " atölye sırasına eklendi • paket başına 45 sn.");
        return true;
    }

    private void Update()
    {
        if (player == null) return;
        float dt = Time.deltaTime;
        if (Charging)
        {
            if (!chargeAt.CanReach(player)) { chargeAt = null; ChargeProgress = 0f; player.ShowFeedback("Acil şarj durdu • marketten uzaklaştın."); }
            else if ((ChargeProgress += dt) >= ChargeSeconds)
            {
                chargeAt = null; ChargeProgress = 0f;
                player.ReceiveElectricity(ChargeAmount);
                player.ShowFeedback("Acil şarj tamam • +15 EB. Kalıcı çözüm için güneş + batarya + şarj kur.");
            }
        }
        if (queue.Count > 0 && (PackProgress += dt) >= PackSeconds)
        {
            var offer = queue[0]; queue.RemoveAt(0); PackProgress = 0f;
            // Same recipe as the market; the daily counter is not touched because the workshop is the slow path.
            if (player.ExecuteMarketTrade(offer)) player.ShowFeedback("Atölye hazır: " + EcoMarket.Titles[(int)offer] + ".");
            else player.ShowFeedback("Atölye " + EcoMarket.Titles[(int)offer] + " yapamadı • malzeme yetmedi, sıra iptal.");
        }
    }
}
