using System.Collections.Generic;
using System.Collections;
using UnityEngine;

// Scene-only setup: keeps the original placement markers and spawns reusable rigged prefabs.
[DefaultExecutionOrder(-100)]
public sealed class EnemySceneSetup : MonoBehaviour
{
    public Transform player;
    public GameObject[] enemyPrefabs;
    public Transform[] placementMarkers;
    public Vector3[] additionalPositions;
    public bool enableRegionalSpawning = true;
    private void Awake()
    {
        if (player == null) { Debug.LogError("Enemy setup needs a Player reference.", this); return; }
        if (player.GetComponent<PlayerVitals>() == null) player.gameObject.AddComponent<PlayerVitals>();
        EcoNavigation.Ignore(player.gameObject);
        if (FindFirstObjectByType<EcoNavigation>() == null)
        {
            var navigation = new GameObject("Eco navigation");
            navigation.transform.position = player.position;
            navigation.AddComponent<EcoNavigation>();
        }
        if (enemyPrefabs == null) return;
        int relocated = 0;
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null) continue;
            Transform marker = placementMarkers != null && i < placementMarkers.Length ? placementMarkers[i] : null;
            // An incomplete placement list must not create an enemy at an unintended origin.
            if (marker == null && (additionalPositions == null || i >= additionalPositions.Length)) continue;
            Vector3 position = marker != null ? marker.position : transform.TransformPoint(additionalPositions[i]);
            if (marker != null) marker.gameObject.SetActive(false);
            var moved = OutsideSafeArea(position, relocated);
            if (moved != position) { relocated++; position = moved; }
            // Snap feet onto the terrain/plane instead of keeping the static FBX pivot height.
            if (Physics.Raycast(position + Vector3.up * 60f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + 0.03f;
            var instance = Instantiate(enemyPrefabs[i], position, Quaternion.identity, transform);
            instance.name = enemyPrefabs[i].name;
        }
    }
    /// <summary>GDD §11: nothing starts in the safe centre or within 20 m of the player (detection is 12 m), and at most
    /// three normal enemies share a region. Placed enemies that would break this are dealt round-robin to the nearest
    /// regions that may hold enemies, each near its region centre.</summary>
    private Vector3 OutsideSafeArea(Vector3 position, int relocated)
    {
        const float MinimumDistance = 20f;
        var regions = FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID);
        Vector3 offset = position - player.position; offset.y = 0f;
        if (offset.magnitude >= MinimumDistance && !Safe(position, regions)) return position;
        var hosts = new List<EcoRegion>();
        foreach (var region in regions)
        {
            if (region == null) continue;
            Vector3 c = region.transform.position;
            if (Safe(c, regions) || Vector3.Distance(new Vector3(c.x, player.position.y, c.z), player.position) < MinimumDistance) continue;
            hosts.Add(region);
        }
        if (hosts.Count == 0) return position;
        hosts.Sort((a, b) => Vector3.Distance(a.transform.position, player.position).CompareTo(Vector3.Distance(b.transform.position, player.position)));
        var host = hosts[relocated % hosts.Count];
        var rng = new System.Random(relocated * 7919 + 17);
        Vector3 fallback = host.transform.position;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            var candidate = host.transform.position + new Vector3((float)(rng.NextDouble() - 0.5) * host.size.x * 0.8f, 0f, (float)(rng.NextDouble() - 0.5) * host.size.y * 0.8f);
            // Must stand on walkable ground: lake regions are mostly water, whose surface is not ground.
            if (!Physics.Raycast(candidate + Vector3.up * 60f, Vector3.down, out var ground, 120f, ~0, QueryTriggerInteraction.Ignore)
                || !EcoGround.IsGround(ground.collider) || ground.normal.y < 0.8f) continue;
            candidate = ground.point;
            if (!Safe(candidate, regions) && Vector3.Distance(candidate, player.position) >= MinimumDistance) return candidate;
        }
        return fallback;
    }
    private static bool Safe(Vector3 point, EcoRegion[] regions)
    {
        EcoRegion best = null;
        foreach (var region in regions)
            if (region != null && region.Contains(point) && (best == null || region.priority > best.priority)) best = region;
        return best != null && (best.priority >= 5 || (best.TryGetComponent<EcoRegionInfo>(out var info) && info.kind == EcoRegionInfo.Kind.City));
    }
    private IEnumerator Start()
    {
        if (!enableRegionalSpawning || player == null || enemyPrefabs == null) yield break;
        // Regions can be supplied by the starter prefab; wait for its initialization and navigation.
        yield return null;
        while (EcoNavigation.Current != null && !EcoNavigation.Current.Ready) yield return null;
        foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID))
        {
            if (region.GetComponent<EcoEnemySpawnZone>() != null) continue;
            // GDD §2/§11: the town is the safe preparation area and the starter pockets at the base never spawn.
            if (region.priority >= 5 || (region.TryGetComponent<EcoRegionInfo>(out var info) && info.kind == EcoRegionInfo.Kind.City)) continue;
            var zone = region.gameObject.AddComponent<EcoEnemySpawnZone>();
            zone.enemyPrefabs = enemyPrefabs;
            zone.player = player;
        }
    }
}
