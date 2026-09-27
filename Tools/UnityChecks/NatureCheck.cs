#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class NatureCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Nature check").AddComponent<NatureCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/NatureValidation.unity"); EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog; yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>(); var player = weapons.GetComponentInParent<PlayerController>(); player.enabled = false;
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var shop = FindFirstObjectByType<EcoMarket>(); Move(player, shop.transform.position + Vector3.back);
        weapons.ReturnMaterials(100, 100);
        Check(!shop.Trade(weapons, EcoMarket.Offer.CarrotSeeds) && !shop.Trade(weapons, EcoMarket.Offer.TreeSeeds), "Havuç and tree seeds are gated at level two");
        weapons.Progression.Restore(EcoProgression.Threshold(2));
        Check(shop.Trade(weapons, EcoMarket.Offer.CarrotSeeds) && shop.Trade(weapons, EcoMarket.Offer.TreeSeeds) && !shop.Trade(weapons, EcoMarket.Offer.TomatoSeeds), "Level two unlocks carrot and tree while tomato stays locked");
        weapons.Progression.Restore(EcoProgression.Threshold(4)); Check(shop.Trade(weapons, EcoMarket.Offer.TomatoSeeds) && weapons.TotalSeeds == 35, "Four species occupy one shared seed capacity");
        int plastic = weapons.Plastic; Check(shop.Trade(weapons, EcoMarket.Offer.Seeds) && weapons.TotalSeeds == 40, "Seed stock fills the shared capacity exactly");
        Check(!shop.Trade(weapons, EcoMarket.Offer.CarrotSeeds) && weapons.Plastic == plastic - 2, "Full shared seed storage rejects other species without charging");
        Check(shop.Trade(weapons, EcoMarket.Offer.SellSeeds) && weapons.Seeds == 20, "Selling selected cabbage frees shared space");
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Nature test soil"; ground.transform.position = new Vector3(60, 0, 60); ground.transform.localScale = new Vector3(16, 0.1f, 16); ground.AddComponent<PlantingSurface>();
        Move(player, new Vector3(60, 0.1f, 54)); camera.transform.position = new Vector3(60, 1.3f, 55); camera.transform.LookAt(new Vector3(60, 0.05f, 60)); Physics.SyncTransforms();
        weapons.SelectTool(1); weapons.CycleSeed(); Check(weapons.SelectedSeed == EcoCropCatalog.Kind.Carrot, "R cycles to an unlocked seed species");
        int carrotStock = weapons.SeedCount(EcoCropCatalog.Kind.Carrot), cabbageStock = weapons.Seeds;
        weapons.Use(0.1f); weapons.CycleSeed(); yield return new WaitForSeconds(0.5f);
        var shotCrop = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).FirstOrDefault(p => p.kind == EcoCropCatalog.Kind.Carrot);
        Check(shotCrop != null && weapons.SelectedSeed == EcoCropCatalog.Kind.Tomato && weapons.SeedCount(EcoCropCatalog.Kind.Carrot) == carrotStock - 1 && weapons.Seeds == cabbageStock,
            "Flying seed remembers its launch species when selection changes before impact");
        Check(shotCrop.transform.Find("Crop Visual").GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null)), "Carrot uses its imported model with resolved materials");
        shotCrop.growth = 1; shotCrop.contaminated = false; Aim(camera, shotCrop.GetComponent<Collider>()); Move(player, camera.transform.position - Vector3.up);
        int food = weapons.Food; weapons.Interact();
        Check(weapons.SeedCount(EcoCropCatalog.Kind.Carrot) == carrotStock + 2 && weapons.Food == food + 2, "Harvest returns seeds of the actual crop and its species-specific food yield");
        Check(!shotCrop.TryHarvest(out _, out _), "Species harvest remains single-use");
        var region = new GameObject("Climate test region").AddComponent<EcoRegion>(); region.transform.position = new Vector3(60, 0, 60); region.size = new Vector2(20, 20); region.baseTemperature = 30; region.priority = 20;
        var carrot = PlantedSeed.Create(new Vector3(57, 0.06f, 61), EcoCropCatalog.Kind.Carrot);
        var tomato = PlantedSeed.Create(new Vector3(58, 0.06f, 61), EcoCropCatalog.Kind.Tomato);
        carrot.moisture = tomato.moisture = 1; carrot.Simulate(1); tomato.Simulate(1);
        Check(!carrot.ClimateSuitable && carrot.growth == 0 && tomato.ClimateSuitable && tomato.growth > 0, "Warm climate supports tomato and stops carrot growth");
        Check(carrot.health < 1 && tomato.moisture < carrot.moisture, "Wrong climate damages crops and crop types have distinct water demand");
        Check(carrot.Fertilize() && carrot.growth == 0 && carrot.pestProtectionRemaining == 120, "Compost cannot bypass climate but still protects from pests");
        region.baseTemperature = 10; float tomatoGrowth = tomato.growth; carrot.Simulate(1); tomato.Simulate(1);
        Check(carrot.ClimateSuitable && carrot.growth > 0 && !tomato.ClimateSuitable && Near(tomato.growth, tomatoGrowth), "Cool climate reverses which crop can grow");
        region.baseTemperature = 20; carrot.age = 60; carrot.pestProtectionRemaining = 0; carrot.moisture = 1; carrot.Simulate(5);
        Check(carrot.pests > 0, "Unprotected crops acquire pests after the establishment period");
        carrot.Fertilize(); carrot.Simulate(10);
        Check(carrot.pests == 0 && Near(carrot.pestProtectionRemaining, 110), "Organic compost clears pests and its protection counts down");
        carrot.pestProtectionRemaining = 0.5f; carrot.Simulate(1); Check(Near(carrot.pests, 0.006f), "Pests return only for the portion of a step after protection expires");
        var habitat = region.gameObject.AddComponent<EcoHabitat>(); habitat.recovery = 0;
        var treeA = new GameObject("Habitat tree A").AddComponent<EcoVegetation>(); treeA.transform.position = new Vector3(55, 0, 65);
        var treeB = new GameObject("Habitat tree B").AddComponent<EcoVegetation>(); treeB.transform.position = new Vector3(56, 0, 65);
        habitat.Simulate(30); Check(habitat.Birds > 0 && habitat.Butterflies == 2 && EcoHabitat.HasBirds(region), "Clean restored woodland attracts birds and butterflies gradually");
        carrot.pests = 0.5f; carrot.Simulate(1); Check(carrot.pests < 0.5f, "Returned birds reduce actual crop pests");
        region.airPollution = 80; habitat.Simulate(20); Check(habitat.Birds == 0 && habitat.Butterflies == 0, "Wildlife retreats when its region becomes polluted"); region.airPollution = 0;
        treeB.Burn(1); Check(treeB.GetComponent<RecyclableResource>().organic == 6 && EcoVegetation.HealthyTreesIn(region) == 1, "Burnt tree stops supporting wildlife and becomes organic recycling material");
        Physics.SyncTransforms(); Check(Physics.Raycast(new Vector3(65, 2, 64), Vector3.down, out var soil, 3), "Tree test has real soil collision");
        Check(weapons.TryPlant(soil, EcoCropCatalog.Kind.Tree), "Tree seed can be planted on open soil with enough spacing");
        var sapling = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).First(p => p.kind == EcoCropCatalog.Kind.Tree);
        var treeObject = sapling.gameObject; sapling.moisture = 1; sapling.growth = 0.999f; sapling.Simulate(1); yield return null;
        Check(treeObject.GetComponent<PlantedSeed>() == null && treeObject.GetComponent<EcoVegetation>() != null && treeObject.GetComponent<CapsuleCollider>().height > 3,
            "Mature sapling becomes a permanent full-height ecosystem tree");
        var bed = FindObjectsByType<PlantingSurface>(FindObjectsSortMode.None).First(s => s.name.StartsWith("Planting Bed"));
        Physics.Raycast(bed.transform.position + Vector3.up * 2, Vector3.down, out var bedHit, 3);
        Check(!weapons.TryPlant(bedHit, EcoCropCatalog.Kind.Tree), "Vegetable beds reject tree planting");
        var spots = FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None);
        var cleanSpot = spots.First(s => s.GetComponent<WeaponWaterSource>().IsClean); var dirtySpot = spots.First(s => !s.GetComponent<WeaponWaterSource>().IsClean);
        Move(player, cleanSpot.transform.position + Vector3.back * 3); Aim(camera, cleanSpot.GetComponent<Collider>()); weapons.SelectTool(0);
        Check(!cleanSpot.Interact(weapons), "Fishing requires free hands"); weapons.SelectTool(4);
        weapons.Interact(); Check(cleanSpot.Fisher == weapons, "Actual E camera interaction starts fishing at a clean pond");
        float population = cleanSpot.population; cleanSpot.Simulate(2); Check(weapons.CleanFish == 0 && cleanSpot.Fisher == weapons, "Fishing waits three seconds before giving a catch");
        cleanSpot.Simulate(1); Check(weapons.CleanFish == 1 && cleanSpot.Fisher == null && Near(cleanSpot.population, population + 0.18f - 1), "A clean catch consumes one fish from the regenerating population");
        cleanSpot.Interact(weapons); weapons.SelectTool(1); cleanSpot.Simulate(3);
        Check(weapons.CleanFish == 1 && cleanSpot.Fisher == null, "Switching to a weapon cancels fishing without granting fish");
        weapons.SelectTool(4); Move(player, dirtySpot.transform.position + Vector3.back * 3); dirtySpot.Interact(weapons); dirtySpot.Simulate(3);
        Check(weapons.DirtyFish == 1 && weapons.CleanFish == 1, "Polluted pond catch is kept separate from clean food fish");
        dirtySpot.Interact(weapons); Move(player, Vector3.zero); dirtySpot.Simulate(3); Check(dirtySpot.Fisher == null && weapons.DirtyFish == 1, "Leaving the bank cancels the pending catch");
        dirtySpot.population = 1; var waterSource = dirtySpot.GetComponent<WeaponWaterSource>(); var dirtyRegion = EcoRegion.At(dirtySpot.transform.position); dirtyRegion.waterPollution = 0; dirtySpot.Simulate(100);
        Check(dirtySpot.population > 3 && waterSource.IsClean, "Cleaning a lake allows its fish population to recover above the polluted limit");
        while (weapons.CleanFish + weapons.DirtyFish < 20) weapons.ReceiveFish(true);
        Check(!weapons.ReceiveFish(true) && weapons.CleanFish + weapons.DirtyFish == 20, "Fish inventory has a bounded shared capacity");
        var key = typeof(PlayerWeaponSystem).GetField("sessionKey", BindingFlags.NonPublic | BindingFlags.Instance); string oldKey = (string)key.GetValue(weapons); key.SetValue(weapons, "EcoQuest.Validation.Nature");
        try
        {
            var selected = weapons.SelectedSeed; int seedTotal = weapons.TotalSeeds, cleanFish = weapons.CleanFish, dirtyFish = weapons.DirtyFish;
            weapons.SaveSession(); weapons.CycleSeed(); weapons.LoadSession();
            Check(weapons.TotalSeeds == seedTotal && weapons.SelectedSeed == selected && weapons.CleanFish == cleanFish && weapons.DirtyFish == dirtyFish, "Inventory save restores all seed species, selection, and both fish qualities");
        }
        finally { PlayerPrefs.DeleteKey("EcoQuest.Validation.Nature"); key.SetValue(weapons, oldKey); }
        File.WriteAllText("nature-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static void Move(PlayerController player, Vector3 position)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position; cc.enabled = true; Physics.SyncTransforms(); }
    private static void Aim(Camera camera, Collider target)
    { camera.transform.position = target.bounds.center + Vector3.back * 2.5f + Vector3.up; camera.transform.LookAt(target.bounds.center); Physics.SyncTransforms(); }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.004f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("nature-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
