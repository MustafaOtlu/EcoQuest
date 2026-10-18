#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public sealed class EnemySpawnCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Enemy spawn check").AddComponent<EnemySpawnCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/EnemySpawnValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        UnityEngine.Random.InitState(2134);
        var floor = Box("Floor", new Vector3(80, -0.1f, 80), new Vector3(75, 0.2f, 60));
        var player = new GameObject("Player"); player.transform.position = new Vector3(52, 0.04f, 80);
        player.AddComponent<PlayerVitals>().showLegacyHUD = false;
        var collider = player.AddComponent<CapsuleCollider>(); collider.height = 1.8f; collider.center = Vector3.up * 0.9f;
        var clock = new GameObject("Clock").AddComponent<EcoWorldClock>(); clock.advanceTime = clock.varyingWeather = clock.driveLighting = false;
        var healthy = Region("Healthy", new Vector3(64, 0, 80));
        var dirty = Region("Dirty", new Vector3(92, 0, 80)); dirty.soilPollution = 80;
        var navigation = new GameObject("Navigation").AddComponent<EcoNavigation>(); navigation.transform.position = new Vector3(80, 0, 80); navigation.worldSize = new Vector3(75, 14, 60);
        var wall = Box("Blocked ground", new Vector3(94, 1.5f, 83), new Vector3(5, 3, 6));
        var pond = Box("Excluded pond", new Vector3(89, 0.07f, 75), new Vector3(4, 0.14f, 4)); var pondSource = pond.AddComponent<WeaponWaterSource>(); pondSource.useRegionalQuality = true;
        var tin = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TinMonster.prefab");
        Check(tin != null && tin.GetComponent<EnemyBrain>() != null, "Uses the actual rigged Tin prefab");
        float deadline = Time.time + 15;
        while (!navigation.Ready && Time.time < deadline) yield return null;
        Check(navigation.Ready, "Runtime navigation is ready before spawning");
        var goodZone = Zone(healthy, player.transform, clock, tin);
        goodZone.initialDelay = 0; goodZone.Simulate(100);
        Check(goodZone.SpawnedToday == 0 && goodZone.AliveCount == 0, "A healthy region does not spawn enemies");
        healthy.baseTemperature = 50;
        Check(goodZone.SpeciesWeight(EnemyBrain.Species.Flame) == 0, "Natural warmth without carbon pollution does not create Flame");
        var zone = Zone(dirty, player.transform, clock, tin); zone.initialDelay = 20; zone.spawnInterval = 10; zone.zoneLimit = 1; zone.globalLimit = 2; zone.dailyBudget = 2;
        zone.Simulate(10); Check(zone.AliveCount == 0, "Initial delay preserves the opening encounter");
        zone.Simulate(10);
        Check(zone.AliveCount == 1 && zone.SpawnedToday == 1, "Polluted ground creates a real enemy and spends one daily allowance");
        var first = FindFirstObjectByType<EnemyBrain>(); first.enabled = false;
        Check(EcoRegion.At(first.transform.position) == dirty && NavMesh.SamplePosition(first.transform.position, out _, 0.2f, NavMesh.AllAreas), "Spawn is on navigable ground in its own region");
        Check(Vector3.Distance(first.transform.position, player.transform.position) >= zone.minimumPlayerDistance, "Spawn respects the minimum distance from the player");
        Check(!pond.GetComponent<Collider>().bounds.Contains(first.transform.position) && !wall.GetComponent<Collider>().bounds.Contains(first.transform.position), "Spawn avoids water and occupied obstacles");
        zone.zoneLimit = 3; zone.Simulate(0.1f);
        Check(zone.SpawnedToday == 1, "Another frame cannot bypass the cooldown");
        zone.zoneLimit = 1; zone.Simulate(11);
        Check(zone.AliveCount == 1 && zone.SpawnedToday == 1, "The per-region live population cap stops spawning");
        zone.zoneLimit = 3; zone.globalLimit = 1; zone.Simulate(11);
        Check(zone.SpawnedToday == 1, "Global population includes live enemies from the scene");
        zone.globalLimit = 2; Destroy(first.gameObject); yield return null;
        zone.Simulate(11); Check(zone.AliveCount == 1 && zone.SpawnedToday == 2, "A defeated or removed enemy frees live capacity but not its daily allowance");
        var second = FindFirstObjectByType<EnemyBrain>(); second.enabled = false; Destroy(second.gameObject); yield return null;
        zone.Simulate(100); Check(zone.AliveCount == 0 && zone.SpawnedToday == 2, "Daily budget prevents unlimited resource farming");
        zone.enabled = false; zone.enabled = true; zone.Simulate(100);
        Check(zone.SpawnedToday == 2 && zone.AliveCount == 0, "Disable and re-enable cannot reset the daily budget");
        clock.day++; zone.Simulate(11);
        Check(zone.SpawnedToday == 1 && zone.AliveCount == 1 && zone.BudgetDay == clock.day, "A new in-game day renews the allowance once");
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) { enemy.enabled = false; Destroy(enemy.gameObject); }
        yield return null;
        dirty.soilPollution = 0; zone.Simulate(100);
        Check(zone.AliveCount == 0, "Environmental recovery stops further spawning");
        dirty.carbon = 60; float lowHeatWeight = zone.SpeciesWeight(EnemyBrain.Species.Flame); dirty.carbon = 95;
        Check(zone.SpeciesWeight(EnemyBrain.Species.Flame) > lowHeatWeight && zone.SpeciesWeight(EnemyBrain.Species.Smoke) == 0, "Carbon and heat favor Flame without creating air-pollution species");
        dirty.carbon = 0; dirty.waterPollution = 80;
        Check(zone.SpeciesWeight(EnemyBrain.Species.Slime) > 0, "Polluted natural water enables Slime");
        pond.SetActive(false); Check(zone.SpeciesWeight(EnemyBrain.Species.Slime) == 0, "A region without an active water source cannot spawn Slime");
        pondSource.useRegionalQuality = false; pond.SetActive(true);
        Check(zone.SpeciesWeight(EnemyBrain.Species.Slime) == 0, "Stored supply barrels cannot enable Slime spawns");
        dirty.carbon = 80; clock.cloudCover = 0;
        float dryFlame = zone.SpeciesWeight(EnemyBrain.Species.Flame);
        dirty.airPollution = 80; float dryAcid = zone.SpeciesWeight(EnemyBrain.Species.Acid);
        clock.cloudCover = 0.75f;
        Check(zone.SpeciesWeight(EnemyBrain.Species.Flame) < dryFlame && zone.SpeciesWeight(EnemyBrain.Species.Acid) > dryAcid, "Rain suppresses Flame and favors Acid only in polluted regions");
        clock.cloudCover = 0; dirty.carbon = 0;
        dirty.airPollution = 80;
        Check(zone.SpeciesWeight(EnemyBrain.Species.Smoke) > 0 && zone.SpeciesWeight(EnemyBrain.Species.Acid) > 0, "Air pollution enables Smoke and Acid");
        int budget = zone.SpawnedToday; zone.Simulate(float.NaN); zone.Simulate(float.PositiveInfinity); zone.Simulate(-1);
        Check(zone.SpawnedToday == budget && zone.AliveCount == 0, "Invalid time steps cannot create enemies or alter the allowance");
        var authored = Instantiate(tin, new Vector3(92, 0.04f, 85), Quaternion.identity).GetComponent<EnemyBrain>(); authored.enabled = false;
        zone.zoneLimit = 1; clock.day++; zone.Simulate(30);
        Check(zone.AliveCount == 1 && zone.SpawnedToday == 0, "Opening enemies already present in a region count against its population cap");
        authored.gameObject.SetActive(false); Check(zone.AliveCount == 0, "An inactive actor does not occupy live regional capacity");
        authored.gameObject.SetActive(true); Check(zone.AliveCount == 1, "Reactivating an actor restores its regional capacity usage");
        Destroy(authored.gameObject); yield return null;
        zone.enabled = goodZone.enabled = false;
        var setupObject = new GameObject("Incomplete marker setup"); setupObject.SetActive(false);
        var setup = setupObject.AddComponent<EnemySceneSetup>(); setup.player = player.transform; setup.enemyPrefabs = new[] { tin, tin }; setup.placementMarkers = null;
        setup.additionalPositions = new[] { new Vector3(82, 0.04f, 85) }; setup.enableRegionalSpawning = false; setupObject.SetActive(true);
        Check(setupObject.GetComponentsInChildren<EnemyBrain>().Length == 1, "Missing placement data skips only that enemy without an index error or origin spawn");
        Destroy(setupObject); yield return null;
        var introObject = new GameObject("Five intro enemies"); introObject.SetActive(false);
        var intro = introObject.AddComponent<EnemySceneSetup>(); intro.player = player.transform; intro.enemyPrefabs = new[] { tin, tin, tin, tin, tin };
        intro.placementMarkers = Array.Empty<Transform>(); intro.additionalPositions = new[] { new Vector3(82, 0, 88), new Vector3(84, 0, 88), new Vector3(86, 0, 88), new Vector3(88, 0, 88), new Vector3(90, 0, 88) };
        intro.enableRegionalSpawning = false; introObject.SetActive(true);
        Check(introObject.GetComponentsInChildren<EnemyBrain>().Length == 5, "Scene setup keeps all five configured opening enemies");
        File.WriteAllText("enemy-spawn-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static EcoRegion Region(string name, Vector3 position)
    { var go = new GameObject(name); go.transform.position = position; var r = go.AddComponent<EcoRegion>(); r.size = new Vector2(24, 36); return r; }
    private static EcoEnemySpawnZone Zone(EcoRegion region, Transform player, EcoWorldClock clock, GameObject prefab)
    { var z = region.gameObject.AddComponent<EcoEnemySpawnZone>(); z.autoSimulate = false; z.player = player; z.clock = clock; z.enemyPrefabs = new[] { prefab }; return z; }
    private static GameObject Box(string name, Vector3 position, Vector3 scale)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; return go; }
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("enemy-spawn-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
