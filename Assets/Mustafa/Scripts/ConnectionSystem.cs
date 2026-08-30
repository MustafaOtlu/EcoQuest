using UnityEngine;
using System.Collections.Generic;

public class ConnectionSystem : MonoBehaviour
{
    public static ConnectionSystem Instance { get; private set; }

    [SerializeField] Sprite cableSprite;
    [SerializeField] Sprite pipeSprite;

    readonly Dictionary<Vector2Int, ConnectionType> connections = new();
    readonly List<GameObject> connectionObjects = new();

    public enum ConnectionType { Cable, Pipe }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool PlaceConnection(Vector2Int pos, ConnectionType type)
    {
        if (connections.ContainsKey(pos)) return false;

        int metalCost = type == ConnectionType.Cable ? GameConstants.CABLE_METAL_PER_TILE : 0;
        int plasticCost = type == ConnectionType.Cable
            ? GameConstants.CABLE_PLASTIC_PER_TILE
            : GameConstants.PIPE_PLASTIC_PER_TILE;

        if (!GameManager.Instance.HasResource(ResourceType.Metal, metalCost) ||
            !GameManager.Instance.HasResource(ResourceType.Plastic, plasticCost))
            return false;

        GameManager.Instance.SpendResource(ResourceType.Metal, metalCost);
        GameManager.Instance.SpendResource(ResourceType.Plastic, plasticCost);

        connections[pos] = type;

        var go = new GameObject($"{type}_{pos}");
        go.transform.position = new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = type == ConnectionType.Cable ? cableSprite : pipeSprite;
        sr.sortingOrder = 3;
        connectionObjects.Add(go);

        return true;
    }

    public void RemoveConnection(Vector2Int pos)
    {
        if (!connections.ContainsKey(pos)) return;
        connections.Remove(pos);

        connectionObjects.RemoveAll(go =>
        {
            if (go == null) return true;
            Vector2Int goPos = new(
                Mathf.FloorToInt(go.transform.position.x),
                Mathf.FloorToInt(go.transform.position.y)
            );
            if (goPos == pos)
            {
                Destroy(go);
                return true;
            }
            return false;
        });
    }

    public bool HasConnectionAt(Vector2Int pos) => connections.ContainsKey(pos);

    public ConnectionType? GetConnectionType(Vector2Int pos)
    {
        return connections.TryGetValue(pos, out var type) ? type : null;
    }

    public bool IsConnectedToGrid(Vector2Int buildingPos, ConnectionType type)
    {
        HashSet<Vector2Int> visited = new();
        Queue<Vector2Int> queue = new();
        queue.Enqueue(buildingPos);

        Vector2Int[] directions = {
            Vector2Int.up, Vector2Int.down,
            Vector2Int.left, Vector2Int.right
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            if (connections.TryGetValue(current, out var connType) && connType == type)
            {
                var building = BuildingSystem.Instance?.GetBuildingAt(current);
                if (building != null && building.GridPosition != buildingPos)
                    return true;
            }

            foreach (var dir in directions)
            {
                var neighbor = current + dir;
                if (!visited.Contains(neighbor) && connections.ContainsKey(neighbor))
                {
                    if (connections[neighbor] == type)
                        queue.Enqueue(neighbor);
                }
            }
        }
        return false;
    }
}
