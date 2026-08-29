using UnityEngine;

[CreateAssetMenu(fileName = "NewBuildingData", menuName = "EcoQuest/Building Data")]
public class BuildingData : ScriptableObject
{
    public BuildingType buildingType;
    public string displayName;
    public Sprite icon;
    public Sprite buildingSprite;

    [Header("Maliyet")]
    public int metalCost;
    public int plasticCost;
    public int requiredYEPLevel;
    public int maxBuildCount;

    [Header("Uretim")]
    public float energyProductionPerDay;
    public float energyConsumptionPerDay;
    public float waterCapacity;
    public float waterProcessingRate;

    [Header("Yerlestirme")]
    public Vector2Int tileSize = Vector2Int.one;
    public BiomeType[] preferredBiomes;
    public float biomeEfficiencyBonus = 0.5f;

    [Header("Dayaniklilik")]
    public float maxHealth = 100f;
    public EnemyType[] vulnerableTo;
}
