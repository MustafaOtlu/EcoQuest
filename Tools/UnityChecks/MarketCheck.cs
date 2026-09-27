#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public sealed class MarketCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Market check").AddComponent<MarketCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/MarketValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var player = weapon.GetComponentInParent<PlayerController>(); player.enabled = false;
        var vitals = player.GetComponent<PlayerVitals>(); vitals.enabled = false;
        var shop = FindFirstObjectByType<EcoMarket>();
        Check(shop != null && shop.name == "Workbench" && shop.GetComponent<Collider>() != null, "MainScene existing workbench is a reachable market with its original collider");
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = shop.transform.position + Vector3.back * 1.8f; cc.enabled = true;
        camera.transform.position = player.transform.position + Vector3.up * 1.3f; camera.transform.LookAt(shop.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        Check(weapon.ContextHint().Contains("MARKET"), "Actual camera ray identifies the market interaction");
        weapon.Interact(); Check(EcoMarketUI.IsOpen && EcoMarketUI.BlocksGameplay && weapon.holder.EquippedWeapon == null, "E opens the shopping UI and stows the weapon");
        Check(Cursor.lockState == CursorLockMode.None, "Market releases the pointer for actual UI buttons");
        int ammo = weapon.MetalAmmo; float energy = weapon.Energy;
        weapon.Use(1); weapon.Builder.SetBuildMode(true);
        Check(weapon.Energy == energy && weapon.MetalAmmo == ammo && !weapon.Builder.IsBuilding, "Open market blocks firing and construction");
        Check(!shop.Trade(weapon, EcoMarket.Offer.IronAmmo) && weapon.MetalAmmo == ammo && weapon.Metal == 0, "Unfunded purchase changes neither ammo nor materials");
        Check(!shop.Trade(weapon, (EcoMarket.Offer)99), "Unknown shop offer is rejected without indexing outside the catalogue");
        weapon.ReturnMaterials(1000, 1000); int metal = weapon.Metal, plastic = weapon.Plastic, xp = weapon.Progression.Experience;
        var buy = GameObject.Find("Eco Market UI").GetComponentsInChildren<Button>().First(b => b.transform.parent.name == "Offer 0");
        // Refresh affordability before invoking the same listener used by the UI pointer.
        yield return new WaitForSeconds(0.15f); Check(buy.interactable, "Affordable purchase enables its UI button"); buy.onClick.Invoke();
        Check(weapon.MetalAmmo == ammo + 5 && weapon.Metal == metal - 1 && weapon.Plastic == plastic, "Market purchase button exchanges exactly one metal for five balls");
        Check(weapon.Progression.Experience == xp, "Trading does not farm YEP");
        int seeds = weapon.Seeds; shop.Trade(weapon, EcoMarket.Offer.Seeds);
        Check(weapon.Seeds == seeds + 5 && weapon.Plastic == plastic - 2, "Seed pack costs two plastic for five seeds");
        Check(shop.Trade(weapon, EcoMarket.Offer.SellSeeds) && weapon.Seeds == seeds && weapon.Plastic == plastic - 1, "Seed resale costs more to buy than it returns and cannot create instant profit");
        Check(!shop.Trade(weapon, EcoMarket.Offer.SellProduce), "Selling a missing harvested product is rejected");
        Check(!shop.Trade(weapon, EcoMarket.Offer.SmallFilter) && !shop.Trade(weapon, EcoMarket.Offer.LargeFilter), "Air filters respect their level gates");
        weapon.Progression.Restore(EcoProgression.Threshold(2)); metal = weapon.Metal; plastic = weapon.Plastic;
        Check(shop.Trade(weapon, EcoMarket.Offer.SmallFilter) && weapon.SmallFilters == 1 && weapon.Metal == metal - 3 && weapon.Plastic == plastic - 2, "Small filter unlocks at level two with exact cost");
        var up = weapon.Upgrades;
        for (int i = 0; i < 6; i++) Check(shop.Upgrade(weapon, (EcoEquipmentUpgrades.Upgrade)i), "First equipment rank can be purchased for track " + i);
        Check(up.Purchased == 6 && !shop.Upgrade(weapon, EcoEquipmentUpgrades.Upgrade.RecyclerBatch), "Level two allows six total upgrades and rejects a seventh");
        Check(weapon.maximumWater == 150 && weapon.Water == 100, "Larger water tank changes capacity without granting free water");
        Check(!shop.Upgrade(weapon, EcoEquipmentUpgrades.Upgrade.WaterTank), "Second rank stays locked below level six");
        var ui = FindFirstObjectByType<EcoMarketUI>(); ui.ShowPage(true);
        Check(GameObject.Find("Eco Market UI").GetComponentsInChildren<Button>().Count(b => b.transform.parent.name.StartsWith("Offer ")) == 9, "Upgrade page exposes all nine equipment tracks");
        EcoMarketUI.CloseFor(weapon); Check(!EcoMarketUI.IsOpen && weapon.holder.EquippedWeapon != null, "Closing the market restores the selected tool");
        weapon.Use(1); Check(weapon.Energy == energy, "Closing UI cannot leak a same-frame shot");
        yield return null; yield return null;
        camera.transform.rotation = Quaternion.LookRotation(Vector3.up); weapon.SelectTool(0); weapon.Use(1);
        Check(Near(weapon.Energy, energy - 6.5f), "Three vacuum upgrades increase real electricity use from five to six-and-a-half per second");
        var rangeProbe = GameObject.CreatePrimitive(PrimitiveType.Cube); rangeProbe.transform.position = new Vector3(40.18f, 1, 40); rangeProbe.transform.localScale = Vector3.one * 0.2f;
        var distantScrap = rangeProbe.AddComponent<RecyclableResource>(); distantScrap.metal = 1; distantScrap.plastic = 0;
        camera.transform.position = new Vector3(40, 1, 31); camera.transform.rotation = Quaternion.identity; Physics.SyncTransforms();
        var purchasedRanks = up.Capture(); up.Restore(null); weapon.Use(0.5f);
        Check(!distantScrap.Claimed, "Base vacuum cannot claim an off-axis resource nine metres away");
        up.Restore(purchasedRanks); weapon.Use(0.5f);
        Check(distantScrap.Claimed, "Vacuum reach upgrade expands both actual collection distance and pickup width");
        var smoke = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).First(e => e.species == EnemyBrain.Species.Smoke);
        smoke.transform.position = new Vector3(40, 0, 48); Physics.SyncTransforms();
        Aim(camera, smoke.GetComponent<Collider>()); weapon.CycleMode(); float smokeHealth = smoke.Health;
        weapon.Use(0.5f); Check(Near(smoke.Health, smokeHealth - 19.2f), "Vacuum pressure upgrade increases actual anti-vacuum damage against smoke: " + smokeHealth + " -> " + smoke.Health);
        weapon.CycleMode(); weapon.CycleMode();
        camera.transform.rotation = Quaternion.LookRotation(Vector3.up);
        weapon.ReceiveElectricity(100); weapon.SelectTool(1); int beforeSeeds = weapon.Seeds;
        weapon.Use(0.1f);
        var shots = FindObjectsByType<PlayerSeedProjectile>(FindObjectsSortMode.None);
        Check(shots.Length == 2 && weapon.Seeds == beforeSeeds - 2 && Near(weapon.Energy, 94.8f), "Multi-seed upgrade launches two physical seeds and pays both seed and electricity costs");
        var velocity = (Vector3)typeof(PlayerSeedProjectile).GetField("velocity", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(shots[0]);
        Check(Near(velocity.magnitude, 21.6f), "Seed range upgrade changes actual projectile launch velocity");
        foreach (var shot in shots) DestroyImmediate(shot.gameObject);
        weapon.Progression.Restore(EcoProgression.Threshold(6));
        for (int i = 6; i < 9; i++) Check(shop.Upgrade(weapon, (EcoEquipmentUpgrades.Upgrade)i), "Recycler upgrade track becomes purchasable with more level allowance " + i);
        weapon.ReturnRawResources(20, 0, 0); weapon.SelectTool(2); weapon.ReceiveElectricity(100);
        int raw = weapon.CollectedMetalScrap; energy = weapon.Energy; weapon.RecycleInventory();
        Check(weapon.CollectedMetalScrap == raw - 12 && Near(weapon.Energy, energy - 31.2f), "Recycler capacity upgrade processes twelve items at the increased per-item electricity cost");
        var heavy = FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None).Where(r => r.requiredRecyclerRank > 0).OrderBy(r => r.requiredRecyclerRank).ToArray();
        Check(heavy.Length == 3, "Field station includes three visibly larger salvage objects with distinct rank gates");
        Aim(camera, heavy[1].GetComponent<Collider>()); weapon.ReceiveElectricity(100); weapon.Use(0.1f);
        Check(!heavy[1].Claimed, "Rank one recycler cannot consume rank two salvage");
        Aim(camera, heavy[0].GetComponent<Collider>()); weapon.Use(1.5f);
        Check(heavy[0].Claimed, "Upgraded recycler speed completes the authored two-second salvage in one-and-a-half seconds");
        for (int i = 0; i < 9; i++) Check(shop.Upgrade(weapon, (EcoEquipmentUpgrades.Upgrade)i), "Second rank purchases retain per-track state " + i);
        Check(up.Purchased == 18 && !shop.Upgrade(weapon, EcoEquipmentUpgrades.Upgrade.WaterTank), "Second-rank equipment remains bounded by level and rank gates");
        weapon.Progression.Restore(EcoProgression.Threshold(12));
        Check(shop.Trade(weapon, EcoMarket.Offer.LargeFilter) && weapon.LargeFilters == 1, "Large factory filter can be bought after its level gate");
        for (int i = 0; i < 9; i++) Check(shop.Upgrade(weapon, (EcoEquipmentUpgrades.Upgrade)i), "Final rank purchases succeed once " + i);
        metal = weapon.Metal; plastic = weapon.Plastic;
        Check(!shop.Upgrade(weapon, EcoEquipmentUpgrades.Upgrade.WaterTank) && weapon.Metal == metal && weapon.Plastic == plastic, "Maximum rank rejects additional charges");
        Check(weapon.maximumWater == 250 && up.SeedCapacity == 100 && up.AmmoCapacity == 250 && up.RecyclingBatch == 20, "Maximum capacities match actual equipment state");
        while (weapon.MetalAmmo <= up.AmmoCapacity - 5)
            if (!shop.Trade(weapon, EcoMarket.Offer.IronAmmo)) throw new Exception("Ammo capacity fill stopped early");
        metal = weapon.Metal; ammo = weapon.MetalAmmo;
        Check(!shop.Trade(weapon, EcoMarket.Offer.IronAmmo) && weapon.MetalAmmo == ammo && weapon.Metal == metal, "Full ammo capacity rejects purchases without wasting metal");
        // Save under an isolated key: never overwrite the user's current save.
        var keyField = typeof(PlayerWeaponSystem).GetField("sessionKey", BindingFlags.NonPublic | BindingFlags.Instance); string previousKey = (string)keyField.GetValue(weapon); const string key = "EcoQuest.Validation.Market"; keyField.SetValue(weapon, key);
        try
        {
            weapon.ReceiveWater(150, true); weapon.SaveSession(); int savedSmall = weapon.SmallFilters, savedLarge = weapon.LargeFilters;
            up.Restore(null); weapon.UseAirFilter(true); weapon.UseAirFilter(false); weapon.LoadSession();
            Check(up.Purchased == 27 && weapon.maximumWater == 250 && weapon.Water == 250 && weapon.SmallFilters == savedSmall && weapon.LargeFilters == savedLarge,
                "Save round-trip restores upgrade ranks before capacity-clamped water and both filter inventories");
        }
        finally { PlayerPrefs.DeleteKey(key); keyField.SetValue(weapon, previousKey); }
        camera.transform.position = player.transform.position + Vector3.up * 1.3f; camera.transform.LookAt(shop.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        weapon.Interact(); Check(EcoMarketUI.IsOpen, "Market can reopen after upgraded combat and load");
        vitals.ReceiveHit(10000); yield return null;
        Check(!EcoMarketUI.IsOpen && !shop.Trade(weapon, EcoMarket.Offer.Seeds), "Recovery closes the market and blocks transactions"); vitals.SimulateRecovery(5); yield return null;
        cc.enabled = false; player.transform.position += Vector3.right * 10; cc.enabled = true;
        Check(!shop.Trade(weapon, EcoMarket.Offer.Seeds) && !shop.Upgrade(weapon, EcoEquipmentUpgrades.Upgrade.WaterTank), "Trading and upgrades cannot be executed remotely");
        File.WriteAllText("market-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static void Aim(Camera camera, Collider target)
    { camera.transform.position = target.bounds.center - Vector3.forward * 2; camera.transform.LookAt(target.bounds.center); Physics.SyncTransforms(); }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.002f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("market-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
