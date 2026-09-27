#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class EnvironmentCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Environment check").AddComponent<EnvironmentCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnvironmentValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>(); var player = weapons.GetComponentInParent<PlayerController>(); player.enabled = false;
        var vitals = player.GetComponent<PlayerVitals>(); vitals.enabled = false;
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var healthy = EcoRegion.At(Vector3.zero); var dirty = EcoRegion.At(new Vector3(26, 0, 8));
        Check(healthy != null && dirty != null && healthy != dirty && healthy.size == dirty.size, "MainScene contains equal-sized healthy and polluted regions");
        Check(healthy.airPollution < 1 && dirty.airPollution > 60 && dirty.Temperature > healthy.Temperature, "Regional pollution and carbon produce distinct starting temperatures");
        Check(EcoRegion.At(new Vector3(1000, 0, 1000)) == null, "Outside a region does not borrow unrelated pollution");
        float cleanAir = healthy.airPollution; var factory = FindFirstObjectByType<EcoFactory>();
        float air = dirty.airPollution, carbon = dirty.carbon;
        factory.Simulate(10); Check(Near(dirty.airPollution - air, 1.2f) && Near(dirty.carbon - carbon, 0.4f) && healthy.airPollution == cleanAir, "Unfiltered factory affects only its own region with exact air and carbon rates");
        var hit = factory.GetComponent<Collider>(); Move(player, hit.bounds.center + Vector3.back * 3.8f); Aim(camera, hit);
        Check(weapons.ContextHint().Contains("Filtresiz fabrika"), "Camera ray exposes factory interaction");
        Check(!factory.InstallFilter(weapons) && !factory.Filtered, "Missing large filter cannot clean a factory");
        weapons.ReturnMaterials(100, 100); weapons.Progression.Restore(EcoProgression.Threshold(4));
        var shop = FindFirstObjectByType<EcoMarket>(); Move(player, shop.transform.position + Vector3.back);
        Check(shop.Trade(weapons, EcoMarket.Offer.LargeFilter) && weapons.LargeFilters == 1, "Real market supplies the factory filter");
        Check(!factory.InstallFilter(weapons) && weapons.LargeFilters == 1, "Remote factory installation cannot consume an item");
        Move(player, hit.bounds.center + Vector3.back * 3.8f); Aim(camera, hit); int xp = weapons.Progression.Experience;
        air = dirty.airPollution; weapons.Interact();
        Check(factory.Filtered && weapons.LargeFilters == 0 && weapons.Progression.Experience == xp + 30, "Actual E interaction consumes exactly one filter and awards YEP once");
        Check(Near(dirty.airPollution, air), "Installing a filter does not instantly erase pollution");
        Check(factory.filterVisual.activeSelf && factory.smoke.main.startColor.color.r > 0.9f, "Installed filter appears and chimney changes to white smoke");
        Check(!factory.InstallFilter(weapons) && weapons.Progression.Experience == xp + 30, "Repeated interaction cannot farm XP or filters");
        air = dirty.airPollution; carbon = dirty.carbon; factory.Simulate(10);
        Check(Near(dirty.airPollution - air, 0.096f) && Near(dirty.carbon - carbon, 0.4f), "Air filtration reduces particulate emissions 92 percent without pretending to capture carbon");
        dirty.Simulate(10); Check(dirty.airPollution < air + 0.096f && dirty.airPollution > 60, "Pollution recovers gradually");
        air = dirty.airPollution; dirty.Simulate(float.NaN); dirty.AddPollution(float.PositiveInfinity, 0, 0, 0); factory.Simulate(-1);
        Check(Near(air, dirty.airPollution), "Invalid simulation input cannot corrupt ecology state");
        var source = FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None).First(w => w.useRegionalQuality && !w.IsClean);
        var barrel = FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None).First(w => !w.useRegionalQuality && w.cleanWater);
        Check(!source.IsClean && barrel.IsClean, "Polluted natural water stays separate from the clean supply barrel");
        var intake = new GameObject("Test intake").AddComponent<EcoWaterNode>(); intake.kind = EcoWaterNode.Kind.Intake; intake.source = source;
        intake.transform.position = source.transform.position; intake.capacity = 100; intake.electricityPerLitre = 0.5f;
        float delivered = intake.DeliverElectricity(2, 1);
        Check(Near(delivered, 2) && Near(intake.dirtyWater, 4) && intake.cleanWater == 0, "Intake uses regional quality when drawing natural water");
        dirty.waterPollution = 10; intake.DeliverElectricity(2, 1);
        Check(source.IsClean && Near(intake.cleanWater, 4) && Near(intake.dirtyWater, 4), "Recovered lake supplies clean water without retroactively purifying stored dirty water");
        dirty.waterPollution = 70;
        var clock = FindFirstObjectByType<EcoWorldClock>(); clock.hour = 12; clock.cloudCover = 0;
        var solar = new GameObject("Test solar").AddComponent<EcoPowerNode>(); solar.kind = EcoPowerNode.Kind.Solar; solar.checkShade = false; solar.ratedPower = 10;
        solar.transform.position = Vector3.zero; float clearPower = solar.Generation(clock); solar.transform.position = dirty.transform.position;
        Check(Near(solar.Generation(clock), clearPower * dirty.SolarTransmission) && solar.Generation(clock) < clearPower, "Air pollution measurably reduces solar generation");
        var cleanCrop = PlantedSeed.Create(new Vector3(2, 0, 15)); var dirtyCrop = PlantedSeed.Create(new Vector3(34, 0, 15));
        cleanCrop.moisture = dirtyCrop.moisture = 1; cleanCrop.Simulate(5); dirtyCrop.Simulate(5);
        Check(!cleanCrop.contaminated && dirtyCrop.contaminated && Near(dirtyCrop.growth * 2, cleanCrop.growth), "Crops grown on polluted soil are contaminated and grow at half speed");
        dirty.carbon = 100; dirty.baseTemperature = 30; cleanCrop.moisture = dirtyCrop.moisture = 1; cleanCrop.Simulate(1); dirtyCrop.Simulate(1);
        Check(dirtyCrop.moisture < cleanCrop.moisture, "Regional heat increases irrigation need");
        Check(EcoRegion.PlacementWarning("farm", dirtyCrop.transform.position).Contains("Kirli toprak") && EcoRegion.PlacementWarning("solar", dirty.transform.position).Length > 0, "Construction explains dirty-soil and solar placement consequences");
        var misplaced = new GameObject("Test misplaced turbine").AddComponent<EcoStructure>(); misplaced.buildingId = "wind"; misplaced.playerBuilt = true;
        misplaced.transform.position = Vector3.zero; float vegetation = healthy.vegetation, soil = healthy.soilPollution; misplaced.SimulateEcology(10);
        Check(healthy.vegetation < vegetation && healthy.soilPollution > soil && EcoRegion.PlacementWarning("wind", Vector3.zero).Length > 0, "Player turbine in sensitive habitat degrades vegetation with an explicit placement warning");
        misplaced.gameObject.SetActive(false); vegetation = healthy.vegetation; misplaced.SimulateEcology(10);
        Check(healthy.vegetation == vegetation, "Removing a misplaced structure stops its ecological damage");
        var fire = FindFirstObjectByType<EcoFire>();
        var structure = new GameObject("Fire damage target").AddComponent<EcoStructure>(); structure.transform.position = fire.transform.position + Vector3.right * 0.5f;
        structure.gameObject.AddComponent<BoxCollider>(); structure.gameObject.AddComponent<SphereCollider>();
        var crop = PlantedSeed.Create(fire.transform.position + Vector3.left * 0.5f);
        var tree = FindObjectsByType<EcoVegetation>(FindObjectsSortMode.None).OrderBy(t => Vector3.Distance(t.transform.position, fire.transform.position)).First();
        float fuel = fire.fuel; air = dirty.airPollution; float cropHealth = crop.health, treeHealth = tree.health; Physics.SyncTransforms(); fire.Simulate(1);
        Check(Near(structure.integrity, 97), "Fire damages a structure once even with multiple colliders");
        Check(crop.health < cropHealth && tree.health < treeHealth && Near(fire.fuel, fuel - 1) && dirty.airPollution > air, "Fire consumes finite fuel, burns crops and trees, and pollutes its region");
        structure.gameObject.SetActive(false); crop.gameObject.SetActive(false);
        Move(player, fire.transform.position + Vector3.back * 3); camera.transform.position = fire.transform.position + Vector3.back * 3 + Vector3.up * 0.4f; camera.transform.LookAt(fire.transform.position + Vector3.up * 0.4f); Physics.SyncTransforms();
        weapons.SelectTool(0); while (weapons.Mode != PlayerWeaponSystem.VacuumMode.Water) weapons.CycleMode();
        float water = weapons.Water, electricity = weapons.Energy; weapons.Use(1);
        Check(Near(fire.intensity, 0.5f) && Near(weapons.Water, water - 10) && Near(weapons.Energy, electricity - 5), "Actual water weapon weakens fire and pays both water and electricity");
        weapons.Use(1); Check(!fire.Burning && !fire.GetComponent<Collider>().enabled, "Second water burst extinguishes fire and removes its invisible shot blocker");
        air = dirty.airPollution; fire.Simulate(10); Check(Near(air, dirty.airPollution), "Extinguished fire no longer pollutes or damages nearby objects");
        weapons.SelectTool(3); camera.transform.position = dirty.transform.position + Vector3.up * 2; camera.transform.rotation = Quaternion.LookRotation(Vector3.up); weapons.Use(0.1f);
        Check(weapons.LastScan.Contains(dirty.regionName) && weapons.LastScan.Contains("Karbon"), "Scanner reports current region even while pointing into the sky");
        var wasteObject = GameObject.CreatePrimitive(PrimitiveType.Cube); wasteObject.transform.position = new Vector3(35, 1, 21);
        var waste = wasteObject.AddComponent<RecyclableResource>(); waste.metal = 2; waste.plastic = 3;
        Move(player, wasteObject.transform.position + Vector3.back * 2); Aim(camera, wasteObject.GetComponent<Collider>());
        weapons.SelectTool(0); while (weapons.Mode != PlayerWeaponSystem.VacuumMode.Collect) weapons.CycleMode();
        soil = dirty.soilPollution; weapons.Use(0.5f);
        Check(waste.Claimed && Near(soil - dirty.soilPollution, 1.5f), "Physically collecting waste improves soil in that region only");
        weapons.Use(0.5f); Check(Near(soil - dirty.soilPollution, 1.5f), "The same waste cannot clean the soil twice");
        var hazard = new GameObject("Energy hazard check").AddComponent<EcoFire>(); hazard.transform.position = new Vector3(42, 0, 20); hazard.fuel = 1;
        var victim = new GameObject("Fire victim").AddComponent<PlayerVitals>(); victim.showLegacyHUD = false; victim.transform.position = hazard.transform.position;
        victim.gameObject.AddComponent<BoxCollider>(); Physics.SyncTransforms(); hazard.Simulate(10);
        Check(Near(victim.Energy, 94) && !hazard.Burning, "Fire damages character energy only for its remaining fuel duration");
        victim.ReceiveHit(100); hazard.fuel = 1; hazard.Simulate(1);
        Check(victim.IsRecovering && victim.Energy == 0, "Fire respects the five-second recovery protection");
        File.WriteAllText("environment-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static void Move(PlayerController player, Vector3 position)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position; cc.enabled = true; Physics.SyncTransforms(); }
    private static void Aim(Camera camera, Collider collider)
    { camera.transform.position = collider.bounds.center + Vector3.back * 4; camera.transform.LookAt(collider.bounds.center); Physics.SyncTransforms(); }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.003f;
    private void Check(bool condition, string message) { if (!condition) throw new Exception(message); report += "PASS " + message + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("environment-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
