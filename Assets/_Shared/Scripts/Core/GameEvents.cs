using System;
using UnityEngine;

public static class GameEvents
{
    public static event Action<ResourceType, int> OnResourceChanged;
    public static event Action<IndicatorType, float> OnIndicatorChanged;
    public static event Action<float> OnTimeOfDayChanged;
    public static event Action OnDayStarted;
    public static event Action OnNightStarted;
    public static event Action<BuildingType, Vector2Int> OnBuildingPlaced;
    public static event Action<BuildingType, Vector2Int> OnBuildingDestroyed;
    public static event Action<EnemyType, Vector3> OnEnemySpawned;
    public static event Action<EnemyType> OnEnemyDefeated;
    public static event Action<float> OnYEPChanged;
    public static event Action<int> OnYEPLevelUp;

    public static void ResourceChanged(ResourceType type, int amount) =>
        OnResourceChanged?.Invoke(type, amount);

    public static void IndicatorChanged(IndicatorType type, float value) =>
        OnIndicatorChanged?.Invoke(type, value);

    public static void TimeOfDayChanged(float normalizedTime) =>
        OnTimeOfDayChanged?.Invoke(normalizedTime);

    public static void DayStarted() => OnDayStarted?.Invoke();
    public static void NightStarted() => OnNightStarted?.Invoke();

    public static void BuildingPlaced(BuildingType type, Vector2Int pos) =>
        OnBuildingPlaced?.Invoke(type, pos);

    public static void BuildingDestroyed(BuildingType type, Vector2Int pos) =>
        OnBuildingDestroyed?.Invoke(type, pos);

    public static void EnemySpawned(EnemyType type, Vector3 pos) =>
        OnEnemySpawned?.Invoke(type, pos);

    public static void EnemyDefeated(EnemyType type) =>
        OnEnemyDefeated?.Invoke(type);

    public static void YEPChanged(float value) =>
        OnYEPChanged?.Invoke(value);

    public static void YEPLevelUp(int level) =>
        OnYEPLevelUp?.Invoke(level);
}
