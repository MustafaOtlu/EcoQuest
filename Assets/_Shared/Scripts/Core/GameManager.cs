using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Gostergeler")]
    [Range(0f, 100f)] public float airQuality = 50f;
    [Range(0f, 100f)] public float waterQuality = 50f;
    [Range(0f, 100f)] public float carbonFootprint = 50f;
    [Range(0f, 100f)] public float biodiversity = 30f;
    [Range(0f, 100f)] public float ecosystemHealth = 40f;
    [Range(-20f, 60f)] public float temperature = 22f;

    [Header("Kaynaklar")]
    public int metal = 0;
    public int plastic = 0;
    public int seeds = 0;
    public int ironBalls = 0;
    public int cleanWater = 0;

    [Header("YEP")]
    public float yepPoints = 0f;
    public int yepLevel = 1;

    [Header("Enerji")]
    public float totalEnergyProduction = 0f;
    public float totalEnergyConsumption = 0f;

    readonly Dictionary<ResourceType, int> resources = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeResources();
    }

    void InitializeResources()
    {
        resources[ResourceType.Metal] = metal;
        resources[ResourceType.Plastic] = plastic;
        resources[ResourceType.Seed] = seeds;
        resources[ResourceType.IronBall] = ironBalls;
        resources[ResourceType.CleanWater] = cleanWater;
    }

    public int GetResource(ResourceType type) =>
        resources.TryGetValue(type, out int val) ? val : 0;

    public bool HasResource(ResourceType type, int amount) =>
        GetResource(type) >= amount;

    public void AddResource(ResourceType type, int amount)
    {
        if (!resources.ContainsKey(type))
            resources[type] = 0;
        resources[type] += amount;
        GameEvents.ResourceChanged(type, resources[type]);
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (!HasResource(type, amount)) return false;
        resources[type] -= amount;
        GameEvents.ResourceChanged(type, resources[type]);
        return true;
    }

    public void UpdateIndicator(IndicatorType type, float delta)
    {
        switch (type)
        {
            case IndicatorType.AirQuality:
                airQuality = Mathf.Clamp(airQuality + delta, 0f, 100f);
                GameEvents.IndicatorChanged(type, airQuality);
                break;
            case IndicatorType.WaterQuality:
                waterQuality = Mathf.Clamp(waterQuality + delta, 0f, 100f);
                GameEvents.IndicatorChanged(type, waterQuality);
                break;
            case IndicatorType.CarbonFootprint:
                carbonFootprint = Mathf.Clamp(carbonFootprint + delta, 0f, 100f);
                GameEvents.IndicatorChanged(type, carbonFootprint);
                break;
            case IndicatorType.Biodiversity:
                biodiversity = Mathf.Clamp(biodiversity + delta, 0f, 100f);
                GameEvents.IndicatorChanged(type, biodiversity);
                break;
            case IndicatorType.EcosystemHealth:
                ecosystemHealth = Mathf.Clamp(ecosystemHealth + delta, 0f, 100f);
                GameEvents.IndicatorChanged(type, ecosystemHealth);
                break;
            case IndicatorType.Temperature:
                temperature = Mathf.Clamp(temperature + delta, -20f, 60f);
                GameEvents.IndicatorChanged(type, temperature);
                break;
        }
    }

    public float GetIndicator(IndicatorType type) => type switch
    {
        IndicatorType.AirQuality => airQuality,
        IndicatorType.WaterQuality => waterQuality,
        IndicatorType.CarbonFootprint => carbonFootprint,
        IndicatorType.Biodiversity => biodiversity,
        IndicatorType.EcosystemHealth => ecosystemHealth,
        IndicatorType.Temperature => temperature,
        _ => 0f
    };

    public void AddYEP(float amount)
    {
        yepPoints += amount;
        GameEvents.YEPChanged(yepPoints);
        CheckYEPLevelUp();
    }

    void CheckYEPLevelUp()
    {
        const int MAX_LEVEL = 48;
        float threshold = yepLevel * 100f;
        while (yepPoints >= threshold && yepLevel < MAX_LEVEL)
        {
            yepLevel++;
            GameEvents.YEPLevelUp(yepLevel);
            threshold = yepLevel * 100f;
        }
    }

    public float EnergyBalance => totalEnergyProduction - totalEnergyConsumption;
}
