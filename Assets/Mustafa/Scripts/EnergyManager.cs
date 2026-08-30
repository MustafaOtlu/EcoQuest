using UnityEngine;
using System.Collections.Generic;

public class EnergyManager : MonoBehaviour
{
    public static EnergyManager Instance { get; private set; }

    float totalProduction;
    float totalConsumption;
    readonly List<PlacedBuilding> producers = new();
    readonly List<PlacedBuilding> consumers = new();

    public float TotalProduction => totalProduction;
    public float TotalConsumption => totalConsumption;
    public float Balance => totalProduction - totalConsumption;
    public bool HasSurplus => Balance >= 0;

    DayNightCycle dayNightCycle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        GameEvents.OnBuildingPlaced += OnBuildingPlaced;
        GameEvents.OnBuildingDestroyed += OnBuildingDestroyed;
    }

    void OnDestroy()
    {
        GameEvents.OnBuildingPlaced -= OnBuildingPlaced;
        GameEvents.OnBuildingDestroyed -= OnBuildingDestroyed;
    }

    void Update()
    {
        RecalculateEnergy();
        GameManager.Instance.totalEnergyProduction = totalProduction;
        GameManager.Instance.totalEnergyConsumption = totalConsumption;
    }

    void RecalculateEnergy()
    {
        totalProduction = 0f;
        totalConsumption = 0f;

        producers.RemoveAll(b => b == null);
        consumers.RemoveAll(b => b == null);

        foreach (var building in producers)
        {
            if (!building.IsOperational) continue;

            float production = building.GetEnergyProduction();

            if (building.Data.buildingType == BuildingType.SolarPanel)
            {
                float solarEff = dayNightCycle != null ? dayNightCycle.GetSolarEfficiency() : 1f;
                float sunPotential = BiomeManager.Instance != null
                    ? BiomeManager.Instance.GetSunPotentialAt(building.transform.position)
                    : 0.5f;
                production *= solarEff * sunPotential;
            }
            else if (building.Data.buildingType == BuildingType.WindTurbine)
            {
                float windPotential = BiomeManager.Instance != null
                    ? BiomeManager.Instance.GetWindPotentialAt(building.transform.position)
                    : 0.2f;
                production *= windPotential;
            }

            totalProduction += production;
        }

        foreach (var building in consumers)
        {
            if (!building.IsOperational) continue;
            totalConsumption += building.GetEnergyConsumption();
        }
    }

    void OnBuildingPlaced(BuildingType type, Vector2Int pos)
    {
        var building = BuildingSystem.Instance?.GetBuildingAt(pos);
        if (building == null) return;

        if (building.Data.energyProductionPerDay > 0)
            producers.Add(building);
        if (building.Data.energyConsumptionPerDay > 0)
            consumers.Add(building);
    }

    void OnBuildingDestroyed(BuildingType type, Vector2Int pos)
    {
        producers.RemoveAll(b => b == null || b.GridPosition == pos);
        consumers.RemoveAll(b => b == null || b.GridPosition == pos);
    }

    public bool CanAffordEnergy(float amount)
    {
        return Balance >= amount;
    }
}
