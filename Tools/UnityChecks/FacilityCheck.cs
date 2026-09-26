#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class FacilityCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Facility check").AddComponent<FacilityCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/FacilityValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var power = FindFirstObjectByType<EcoPowerGrid>(); power.enabled = false; power.clock.enabled = false;
        var waterGrid = FindFirstObjectByType<EcoWaterGrid>(); waterGrid.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var builder = weapon.Builder;
        var player = weapon.GetComponentInParent<PlayerController>(); player.enabled = false;
        MovePlayer(player, new Vector3(20, 0.03f, 0));
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.transform.position = new Vector3(23, -0.25f, 3); ground.transform.localScale = new Vector3(14, 0.5f, 12); ground.AddComponent<PlantingSurface>();
        weapon.ReturnMaterials(1000, 1000); Physics.SyncTransforms(); builder.SetBuildMode(true);
        int recyclingIndex = EcoBuildingCatalog.Find("recycling"), farmIndex = EcoBuildingCatalog.Find("farm");
        Check(!builder.TryPlace(recyclingIndex, new Vector3(23, 0.03f, 4), Quaternion.identity), "Recycling facility remains locked at YEP one");
        Check(builder.TryPlace(farmIndex, new Vector3(20, 0.03f, 4), Quaternion.identity), "Starter-level farm can be built on a valid foundation");
        weapon.Progression.Restore(EcoProgression.Threshold(2));
        Check(builder.TryPlace(recyclingIndex, new Vector3(23, 0.03f, 4), Quaternion.identity), "GDD recycling facility unlocks at YEP two");
        var factory = FindFirstObjectByType<EcoRecyclingFacility>(); var structure = factory.GetComponent<EcoStructure>();
        var farm = FindFirstObjectByType<EcoFarm>(); farm.enabled = false; var reservoir = farm.GetComponent<EcoWaterNode>();
        Check(!builder.TryPlace(farmIndex, farm.transform.position + Vector3.up * 0.14f, Quaternion.identity), "A second building cannot be stacked on plantable farmland");
        Check(!reservoir.CanReceive(EcoWaterNode.Fluid.Dirty) && reservoir.CanReceive(EcoWaterNode.Fluid.Clean), "Farm pipe inlet accepts clean irrigation water only");
        builder.SetBuildMode(false);
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        MovePlayer(player, factory.transform.position + Vector3.back * 2);
        camera.transform.position = player.transform.position + Vector3.up * 1.2f; camera.transform.LookAt(factory.GetComponent<Collider>().bounds.center); Physics.SyncTransforms();
        weapon.ReturnRawResources(2, 3, 6); int xp = weapon.Progression.Experience; float weaponEnergy = weapon.Energy;
        weapon.Interact();
        Check(factory.rawMetal == 2 && factory.rawPlastic == 3 && factory.rawOrganic == 6 && weapon.CollectedOrganicWaste == 0 && weapon.CollectedMetalScrap == 0,
            "Actual E interaction transfers each raw material type into the plant");
        Check(weapon.Progression.Experience == xp && weapon.Energy == weaponEnergy, "Depositing raw material grants no YEP and does not consume weapon electricity");
        power.Simulate(1); Check(factory.ReadyCount == 0, "An unconnected recycling plant cannot process waste");
        var battery = new GameObject("Facility battery").AddComponent<EcoPowerNode>(); battery.kind = EcoPowerNode.Kind.Battery; battery.storedEnergy = 0.5f;
        var cable = new GameObject("Facility cable").AddComponent<EcoPowerCable>(); cable.a = battery; cable.b = factory.GetComponent<EcoPowerNode>();
        power.Simulate(1);
        Check(factory.ReadyCount == 0 && Near(factory.workProgress, 0.5f) && Near(battery.storedEnergy, 0), "Partial supply preserves fractional work without producing an extra item");
        battery.storedEnergy = 20; power.Simulate(10);
        Check(factory.rawMetal == 0 && factory.rawPlastic == 0 && factory.rawOrganic == 0 && factory.readyMetal == 2 && factory.readyPlastic == 3 && factory.readyOrganic == 6,
            "Powered recycling preserves the exact mix of material outputs");
        Check(Near(battery.storedEnergy, 9.5f) && factory.workProgress < 0.001f, "One grid electricity unit is spent per processed item including prior fractional work");
        power.Simulate(1); Check(Near(battery.storedEnergy, 9.5f), "An idle recycling plant does not drain its battery");
        int metal = weapon.Metal, plastic = weapon.Plastic, compost = weapon.Compost;
        weapon.Interact(); Check(factory.ReadyCount == 0 && weapon.Metal == metal + 2 && weapon.Plastic == plastic + 3 && weapon.Compost == compost + 2 && weapon.Progression.Experience == xp + 22,
            "Collecting output awards exact materials, organic compost and YEP once");
        weapon.Interact(); Check(weapon.Progression.Experience == xp + 22 && weapon.Metal == metal + 2, "Repeated empty interaction does not duplicate material or YEP");
        factory.storageCapacity = 3; weapon.ReturnRawResources(2, 2, 2); weapon.Interact();
        Check(factory.RawCount == 3 && weapon.CollectedPlasticScrap == 1 && weapon.CollectedOrganicWaste == 2, "Full plant storage leaves overflow in the player inventory");
        metal = weapon.Metal; Check(!structure.Upgrade(weapon) && structure.tier == 1 && weapon.Metal == metal, "Recycling tier two is gated until YEP six");
        weapon.Progression.Restore(EcoProgression.Threshold(6)); factory.storageCapacity = 200;
        plastic = weapon.Plastic;
        Check(structure.Upgrade(weapon) && structure.tier == 2 && factory.unitsPerSecond == 4 && factory.storageCapacity == 400 && weapon.Metal == metal - 32 && weapon.Plastic == plastic - 16,
            "YEP six upgrade doubles capacity and throughput at the displayed cost");
        Check(!structure.Upgrade(weapon), "Recycling tier three requires YEP twelve");
        weapon.Progression.Restore(EcoProgression.Threshold(12)); structure.ReceiveDamage(20);
        Check(!structure.Upgrade(weapon) && structure.tier == 2, "Damaged buildings must be repaired before upgrading");
        Check(structure.Repair(weapon) && structure.Upgrade(weapon) && structure.tier == 3 && factory.unitsPerSecond == 8 && !structure.Upgrade(weapon), "Repaired plant reaches its final tier without an unlimited upgrade loop");
        var tank = Instantiate(Resources.Load<GameObject>("EcoInfrastructure/WaterTank"), new Vector3(25, 0.03f, 4), Quaternion.identity).GetComponent<EcoWaterNode>();
        var tankStructure = tank.GetComponent<EcoStructure>(); tank.cleanWater = 12;
        Check(tankStructure.Upgrade(weapon) && tank.capacity == 600 && tank.cleanWater == 12, "Water tier two unlocks at YEP twelve and preserves stored water");
        Check(!tankStructure.Upgrade(weapon), "Water tier three stays gated below YEP twenty-four");
        weapon.Progression.Restore(EcoProgression.Threshold(24));
        Check(tankStructure.Upgrade(weapon) && tank.capacity == 1200 && !tankStructure.Upgrade(weapon), "Water tier three unlocks at YEP twenty-four and has a finite cap");

        Physics.SyncTransforms();
        Check(Physics.Raycast(farm.transform.position + Vector3.up * 3, Vector3.down, out var soil, 4) && soil.collider.GetComponent<EcoFarm>() == farm && weapon.TryPlant(soil), "Real seed planting accepts the built farm soil collider");
        var crop = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).First(p => farm.Contains(p)); crop.enabled = false; crop.moisture = 0.1f;
        var second = PlantedSeed.Create(farm.transform.position + new Vector3(0.75f, 0.15f, 0)); second.enabled = false; second.moisture = 0.1f;
        var outside = PlantedSeed.Create(farm.transform.position + new Vector3(3, 0.15f, 0)); outside.enabled = false; outside.moisture = 0.1f;
        reservoir.cleanWater = 4; farm.Simulate(1);
        Check(Near(crop.moisture, 0.18f) && Near(second.moisture, 0.18f) && Near(reservoir.cleanWater, 0), "Farm fairly irrigates its own crops with conserved clean-water volume");
        Check(Near(outside.moisture, 0.1f), "Farm does not water crops outside its footprint");
        reservoir.dirtyWater = 10; farm.Simulate(1); Check(Near(reservoir.dirtyWater, 10) && !crop.contaminated && Near(crop.moisture, 0.18f), "Dirty water cannot silently replace clean automatic irrigation");
        reservoir.cleanWater = 30; farm.Simulate(100); float remaining = reservoir.cleanWater;
        Check(Near(crop.moisture, 0.78f) && Near(second.moisture, 0.78f), "Irrigation cannot use more water than remains in the reservoir");
        reservoir.cleanWater = 10; farm.Simulate(10); remaining = reservoir.cleanWater; farm.Simulate(10);
        Check(Near(crop.moisture, 0.8f) && Near(second.moisture, 0.8f) && Near(reservoir.cleanWater, remaining), "Wet crops stop drawing water at their target moisture");
        var farmStructure = farm.GetComponent<EcoStructure>();
        Check(!farmStructure.Dismantle(weapon), "A planted farm cannot be dismantled leaving floating crops");
        crop.growth = 1; second.growth = 1; crop.TryHarvest(out _, out _); second.TryHarvest(out _, out _);
        yield return null;
        Check(farmStructure.Dismantle(weapon), "An empty harvested farm can be dismantled normally");
        factory.rawMetal = 1; factory.rawPlastic = 2; factory.rawOrganic = 3; factory.readyMetal = 4; factory.readyPlastic = 0; factory.readyOrganic = 0;
        int rawMetal = weapon.CollectedMetalScrap, rawPlastic = weapon.CollectedPlasticScrap, rawOrganic = weapon.CollectedOrganicWaste; metal = weapon.Metal;
        Check(structure.Dismantle(weapon) && weapon.CollectedMetalScrap == rawMetal + 1 && weapon.CollectedPlasticScrap == rawPlastic + 2 && weapon.CollectedOrganicWaste == rawOrganic + 3 && weapon.Metal == metal + 4 + 48,
            "Dismantling returns unfinished inputs, finished outputs and half the full upgraded investment");
        File.WriteAllText("facility-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static void MovePlayer(PlayerController player, Vector3 position)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position; cc.enabled = true; }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("facility-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
