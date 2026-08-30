using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class BuildingSystem : MonoBehaviour
{
    [SerializeField] Tilemap groundTilemap;
    [SerializeField] Tilemap buildingTilemap;
    [SerializeField] GameObject ghostPrefab;

    BuildingData selectedBuilding;
    GameObject ghostObject;
    SpriteRenderer ghostRenderer;
    Vector3Int lastGridPos;
    bool isPlacing;

    readonly Dictionary<Vector2Int, PlacedBuilding> placedBuildings = new();
    readonly Color validColor = new(0f, 1f, 0f, 0.5f);
    readonly Color invalidColor = new(1f, 0f, 0f, 0.5f);

    public static BuildingSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Update()
    {
        if (!isPlacing || selectedBuilding == null) return;

        UpdateGhostPosition();

        if (Input.GetMouseButtonDown(0))
            TryPlaceBuilding();

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void StartPlacement(BuildingData building)
    {
        if (building == null) return;

        if (!CanBuildMore(building))
            return;

        if (GameManager.Instance.yepLevel < building.requiredYEPLevel)
            return;

        selectedBuilding = building;
        isPlacing = true;
        CreateGhost();
    }

    void CreateGhost()
    {
        if (ghostObject != null)
            Destroy(ghostObject);

        ghostObject = new GameObject("BuildingGhost");
        ghostRenderer = ghostObject.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = selectedBuilding.buildingSprite != null
            ? selectedBuilding.buildingSprite
            : selectedBuilding.icon;
        ghostRenderer.sortingOrder = 100;
        ghostRenderer.color = validColor;
    }

    void UpdateGhostPosition()
    {
        if (ghostObject == null) return;

        Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector3Int gridPos = groundTilemap != null
            ? groundTilemap.WorldToCell(mouseWorld)
            : new Vector3Int(Mathf.FloorToInt(mouseWorld.x), Mathf.FloorToInt(mouseWorld.y), 0);

        if (gridPos != lastGridPos)
        {
            lastGridPos = gridPos;
            Vector3 cellCenter = groundTilemap != null
                ? groundTilemap.GetCellCenterWorld(gridPos)
                : new Vector3(gridPos.x + 0.5f, gridPos.y + 0.5f, 0);

            ghostObject.transform.position = cellCenter;

            bool canPlace = CanPlaceAt(new Vector2Int(gridPos.x, gridPos.y));
            ghostRenderer.color = canPlace ? validColor : invalidColor;
        }
    }

    bool CanPlaceAt(Vector2Int pos)
    {
        for (int x = 0; x < selectedBuilding.tileSize.x; x++)
        {
            for (int y = 0; y < selectedBuilding.tileSize.y; y++)
            {
                Vector2Int checkPos = new(pos.x + x, pos.y + y);
                if (placedBuildings.ContainsKey(checkPos))
                    return false;
            }
        }
        return true;
    }

    bool CanBuildMore(BuildingData building)
    {
        if (building.maxBuildCount <= 0) return true;

        int count = 0;
        foreach (var placed in placedBuildings.Values)
        {
            if (placed.Data.buildingType == building.buildingType)
                count++;
        }
        return count < building.maxBuildCount;
    }

    void TryPlaceBuilding()
    {
        Vector2Int pos = new(lastGridPos.x, lastGridPos.y);

        if (!CanPlaceAt(pos)) return;

        if (!GameManager.Instance.HasResource(ResourceType.Metal, selectedBuilding.metalCost) ||
            !GameManager.Instance.HasResource(ResourceType.Plastic, selectedBuilding.plasticCost))
            return;

        GameManager.Instance.SpendResource(ResourceType.Metal, selectedBuilding.metalCost);
        GameManager.Instance.SpendResource(ResourceType.Plastic, selectedBuilding.plasticCost);

        PlaceBuilding(pos);
    }

    void PlaceBuilding(Vector2Int pos)
    {
        var buildingGo = new GameObject($"Building_{selectedBuilding.buildingType}_{pos}");
        buildingGo.transform.position = groundTilemap != null
            ? groundTilemap.GetCellCenterWorld(new Vector3Int(pos.x, pos.y, 0))
            : new Vector3(pos.x + 0.5f, pos.y + 0.5f, 0);

        var sr = buildingGo.AddComponent<SpriteRenderer>();
        sr.sprite = selectedBuilding.buildingSprite != null
            ? selectedBuilding.buildingSprite
            : selectedBuilding.icon;
        sr.sortingOrder = 5;

        var placed = buildingGo.AddComponent<PlacedBuilding>();
        placed.Initialize(selectedBuilding, pos);

        for (int x = 0; x < selectedBuilding.tileSize.x; x++)
        {
            for (int y = 0; y < selectedBuilding.tileSize.y; y++)
            {
                placedBuildings[new Vector2Int(pos.x + x, pos.y + y)] = placed;
            }
        }

        GameEvents.BuildingPlaced(selectedBuilding.buildingType, pos);

        float yep = CalculatePlacementYEP(selectedBuilding, pos);
        if (yep > 0)
            GameManager.Instance.AddYEP(yep);
    }

    float CalculatePlacementYEP(BuildingData building, Vector2Int pos)
    {
        float yep = 0f;
        var biome = BiomeManager.Instance?.GetBiomeAt(new Vector2(pos.x, pos.y));

        if (biome != null && building.preferredBiomes != null)
        {
            foreach (var preferred in building.preferredBiomes)
            {
                if (biome.BiomeType == preferred)
                {
                    yep += 5f;
                    break;
                }
            }
        }
        return yep;
    }

    void CancelPlacement()
    {
        isPlacing = false;
        selectedBuilding = null;
        if (ghostObject != null)
            Destroy(ghostObject);
    }

    public void RemoveBuilding(Vector2Int pos)
    {
        if (!placedBuildings.TryGetValue(pos, out var building)) return;

        var data = building.Data;
        var origin = building.GridPosition;

        for (int x = 0; x < data.tileSize.x; x++)
        {
            for (int y = 0; y < data.tileSize.y; y++)
            {
                placedBuildings.Remove(new Vector2Int(origin.x + x, origin.y + y));
            }
        }

        GameEvents.BuildingDestroyed(data.buildingType, origin);
        Destroy(building.gameObject);
    }

    public PlacedBuilding GetBuildingAt(Vector2Int pos)
    {
        return placedBuildings.TryGetValue(pos, out var b) ? b : null;
    }

    public int GetBuildingCount(BuildingType type)
    {
        int count = 0;
        HashSet<PlacedBuilding> counted = new();
        foreach (var b in placedBuildings.Values)
        {
            if (b.Data.buildingType == type && counted.Add(b))
                count++;
        }
        return count;
    }
}
