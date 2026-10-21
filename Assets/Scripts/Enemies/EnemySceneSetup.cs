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
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null) continue;
            Transform marker = placementMarkers != null && i < placementMarkers.Length ? placementMarkers[i] : null;
            // An incomplete placement list must not create an enemy at an unintended origin.
            if (marker == null && (additionalPositions == null || i >= additionalPositions.Length)) continue;
            Vector3 position = marker != null ? marker.position : transform.TransformPoint(additionalPositions[i]);
            if (marker != null) marker.gameObject.SetActive(false);
            position = OutsideSafeArea(position);
            // Snap feet onto the terrain/plane instead of keeping the static FBX pivot height.
            if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + 0.03f;
            var instance = Instantiate(enemyPrefabs[i], position, Quaternion.identity, transform);
            instance.name = enemyPrefabs[i].name;
        }
    }
    /// <summary>GDD §11: nothing starts in the safe centre or within 12 m of the player. Placed enemies that would are
    /// moved out along their own bearing (beyond detection range) into the nearest region that may hold enemies.</summary>
    private Vector3 OutsideSafeArea(Vector3 position)
    {
        const float MinimumDistance = 20f;
        var regions = FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID);
        Vector3 offset = position - player.position; offset.y = 0f;
        if (offset.magnitude >= MinimumDistance && !Safe(position, regions)) return position;
        float bearing = offset.sqrMagnitude > 0.01f ? Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg : 0f;
        for (int step = 0; step < 12; step++)
        {
            float angle = (bearing + (step % 2 == 0 ? 1 : -1) * ((step + 1) / 2) * 30f) * Mathf.Deg2Rad;
            for (float distance = MinimumDistance; distance <= 34f; distance += 7f)
            {
                var candidate = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                candidate.y = position.y;
                if (EcoGround.HasMap && !EcoGround.MapRect.Contains(new Vector2(candidate.x, candidate.z))) continue;
                if (!Safe(candidate, regions)) return candidate;
            }
        }
        return position;
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
