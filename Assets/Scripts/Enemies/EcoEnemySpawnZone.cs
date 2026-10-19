using UnityEngine;
using UnityEngine.AI;

// A limited daily population, driven by the local environment rather than an endless wave.
[RequireComponent(typeof(EcoRegion))]
public sealed class EcoEnemySpawnZone : MonoBehaviour
{
    public GameObject[] enemyPrefabs;
    public Transform player;
    public EcoWorldClock clock;
    public bool autoSimulate = true;
    [Min(0)] public float initialDelay = 20;
    [Min(1)] public float spawnInterval = 20;
    [Range(0, 100)] public float pollutionThreshold = 35;
    [Min(1)] public int zoneLimit = 3, globalLimit = 12, dailyBudget = 6;
    [Min(2)] public float minimumPlayerDistance = 12;
    public int SpawnedToday { get; private set; }
    public int BudgetDay { get; private set; }
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
                if (enemy.Health > 0 && EcoRegion.At(enemy.transform.position) == region) count++;
            return count;
        }
    }
    public float CooldownRemaining => Mathf.Max(0, nextSpawn - elapsed);
    private EcoRegion region;
    private float elapsed, nextSpawn;
    private bool initialized;
    private readonly NavMeshPath connection = new NavMeshPath();

    private void Awake() => region = GetComponent<EcoRegion>();
    private void Update() { if (autoSimulate) Simulate(Time.deltaTime); }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        if (clock == null) clock = FindFirstObjectByType<EcoWorldClock>();
        int today = clock != null ? clock.day : 1;
        if (!initialized)
        {
            initialized = true; BudgetDay = today;
            nextSpawn = elapsed + Mathf.Max(0, initialDelay);
        }
        // Toggling the component or loading an earlier day cannot replenish its allowance.
        if (today > BudgetDay) { BudgetDay = today; SpawnedToday = 0; }
        elapsed += seconds;
        if (elapsed < nextSpawn) return;
        nextSpawn = elapsed + Mathf.Max(1, spawnInterval);
        if (!CanSpawn() || !TryChoosePrefab(out var prefab) || !TryFindPosition(prefab, out var position)) return;
        var instance = Instantiate(prefab, position, Quaternion.identity);
        instance.name = prefab.name + " (" + region.regionName + ")";
        var brain = instance.GetComponent<EnemyBrain>();
        if (brain == null) { Destroy(instance); return; }
        SpawnedToday++;
    }
    private bool CanSpawn()
    {
        if (region == null || !region.isActiveAndEnabled || player == null || !player.gameObject.activeInHierarchy
            || EcoNavigation.Current == null || !EcoNavigation.Current.Ready || SpawnedToday >= Mathf.Max(0, dailyBudget)
            || AliveCount >= Mathf.Max(0, zoneLimit)) return false;
        int population = 0;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (enemy.Health > 0 && ++population >= Mathf.Max(0, globalLimit)) return false;
        return globalLimit > 0;
    }
    public float SpeciesWeight(EnemyBrain.Species species)
    {
        if (region == null || !region.isActiveAndEnabled) return 0;
        float air = Mathf.Max(0, region.airPollution - pollutionThreshold);
        float soil = Mathf.Max(0, region.soilPollution - pollutionThreshold);
        float water = Mathf.Max(0, region.waterPollution - pollutionThreshold);
        float carbon = Mathf.Max(0, region.carbon - pollutionThreshold);
        // Low pollution stays safe even when an otherwise healthy biome is naturally warm.
        return species switch
        {
            EnemyBrain.Species.Flame => carbon * (1 + Mathf.Max(0, region.Temperature - 24) * 0.1f) * (1 - (clock != null ? clock.RainIntensity * 0.6f : 0)),
            EnemyBrain.Species.Smoke => air,
            EnemyBrain.Species.Acid => Mathf.Max(air, soil) * (1 + (clock != null ? clock.RainIntensity : 0)),
            EnemyBrain.Species.Slime => HasWater() ? water : 0,
            EnemyBrain.Species.Tin => Mathf.Max(air, soil) * 0.6f,
            _ => 0
        };
    }
    private bool HasWater()
    {
        foreach (var source in FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None))
            if (source.isActiveAndEnabled && source.useRegionalQuality && EcoRegion.At(source.transform.position) == region) return true;
        return false;
    }
    private bool TryChoosePrefab(out GameObject selected)
    {
        selected = null;
        if (enemyPrefabs == null) return false;
        float total = 0;
        foreach (var prefab in enemyPrefabs)
            if (prefab != null && prefab.TryGetComponent<EnemyBrain>(out var brain)) total += SpeciesWeight(brain.species);
        if (!EcoRegion.Valid(total)) return false;
        float choice = Random.value * total;
        foreach (var prefab in enemyPrefabs)
        {
            if (prefab == null || !prefab.TryGetComponent<EnemyBrain>(out var brain)) continue;
            float weight = SpeciesWeight(brain.species);
            if (weight <= 0) continue;
            selected = prefab; choice -= weight;
            if (choice < 0) return true;
        }
        return selected != null;
    }
    public bool TryFindPosition(GameObject prefab, out Vector3 position)
    {
        position = default;
        if (prefab == null || region == null || !region.isActiveAndEnabled || player == null
            || EcoNavigation.Current == null || !EcoNavigation.Current.Ready) return false;
        if (!NavMesh.SamplePosition(player.position, out var playerGround, 3, NavMesh.AllAreas)) return false;
        var motor = prefab.GetComponent<CharacterController>();
        float radius = motor != null ? Mathf.Max(0.35f, motor.radius * Mathf.Abs(prefab.transform.localScale.x)) : 0.4f;
        float height = motor != null ? Mathf.Max(radius * 2, motor.height * Mathf.Abs(prefab.transform.localScale.y)) : 1.8f;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 local = new Vector3(Random.Range(-region.size.x * 0.45f, region.size.x * 0.45f), 0,
                Random.Range(-region.size.y * 0.45f, region.size.y * 0.45f));
            Vector3 candidate = region.transform.TransformPoint(local);
            if (!Physics.Raycast(candidate + Vector3.up * 12, Vector3.down, out var ground, 40, ~(1 << 2 | 1 << 5), QueryTriggerInteraction.Ignore)) continue;
            if (ground.collider.GetComponentInParent<WeaponWaterSource>() != null || ground.collider.GetComponentInParent<EcoStructure>() != null
                || ground.collider.GetComponentInParent<EcoVegetation>() != null || ground.collider.GetComponentInParent<EnemyBrain>() != null
                || ground.collider.GetComponentInParent<PlayerVitals>() != null || ground.normal.y < 0.7f) continue;
            if (!NavMesh.SamplePosition(ground.point, out var nav, 0.75f, NavMesh.AllAreas)) continue;
            candidate = nav.position + Vector3.up * 0.04f;
            if (EcoRegion.At(candidate) != region || Vector3.Distance(candidate, player.position) < minimumPlayerDistance) continue;
            Vector3 bottom = candidate + Vector3.up * (radius + 0.05f), top = candidate + Vector3.up * (height - radius);
            if (Physics.CheckCapsule(bottom, top, radius, ~(1 << 2 | 1 << 5), QueryTriggerInteraction.Ignore)) continue;
            if (!NavMesh.CalculatePath(nav.position, playerGround.position, NavMesh.AllAreas, connection) || connection.status != NavMeshPathStatus.PathComplete) continue;
            position = candidate; return true;
        }
        return false;
    }
    public string Describe() => "Düşmanlar " + AliveCount + "/" + zoneLimit + " • Bugün " + SpawnedToday + "/" + dailyBudget
        + "\n" + (Mathf.Max(region.airPollution, region.waterPollution, region.soilPollution, region.carbon) <= pollutionThreshold
            ? "Temiz bölge: yeni düşman oluşmuyor." : "Kirli hava: Duman/Asit • kirli toprak: Teneke/Asit • karbon: Alev • kirli göl: Salya");
}
