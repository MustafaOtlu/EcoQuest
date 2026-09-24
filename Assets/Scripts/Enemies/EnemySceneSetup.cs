using UnityEngine;

// Scene-only setup: keeps the original placement markers and spawns reusable rigged prefabs.
[DefaultExecutionOrder(-100)]
public sealed class EnemySceneSetup : MonoBehaviour
{
    public Transform player;
    public GameObject[] enemyPrefabs;
    public Transform[] placementMarkers;
    public Vector3[] additionalPositions;
    private void Awake()
    {
        if (player == null) { Debug.LogError("Enemy setup needs a Player reference.", this); return; }
        if (player.GetComponent<PlayerVitals>() == null) player.gameObject.AddComponent<PlayerVitals>();
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            if (enemyPrefabs[i] == null) continue;
            Transform marker = i < placementMarkers.Length ? placementMarkers[i] : null;
            Vector3 position = marker != null ? marker.position : additionalPositions[i];
            if (marker != null) marker.gameObject.SetActive(false);
            // Snap feet onto the terrain/plane instead of keeping the static FBX pivot height.
            if (Physics.Raycast(position + Vector3.up * 8f, Vector3.down, out var hit, 30f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + 0.03f;
            var instance = Instantiate(enemyPrefabs[i], position, Quaternion.identity, transform);
            instance.name = enemyPrefabs[i].name;
        }
    }
}
