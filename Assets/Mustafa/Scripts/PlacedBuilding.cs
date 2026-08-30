using UnityEngine;

public class PlacedBuilding : MonoBehaviour
{
    BuildingData data;
    Vector2Int gridPosition;
    float currentHealth;
    float efficiency = 1f;
    bool isOperational = true;

    public BuildingData Data => data;
    public Vector2Int GridPosition => gridPosition;
    public float CurrentHealth => currentHealth;
    public float Efficiency => efficiency;
    public bool IsOperational => isOperational;

    public void Initialize(BuildingData buildingData, Vector2Int pos)
    {
        data = buildingData;
        gridPosition = pos;
        currentHealth = data.maxHealth;

        var biome = BiomeManager.Instance?.GetBiomeAt(new Vector2(pos.x, pos.y));
        if (biome != null && data.preferredBiomes != null)
        {
            bool inPreferred = false;
            foreach (var pref in data.preferredBiomes)
            {
                if (biome.BiomeType == pref)
                {
                    inPreferred = true;
                    break;
                }
            }
            efficiency = inPreferred ? 1f + data.biomeEfficiencyBonus : 0.5f;
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            isOperational = false;
        }
    }

    public void Repair(float amount)
    {
        currentHealth = Mathf.Min(data.maxHealth, currentHealth + amount);
        if (currentHealth > 0f)
            isOperational = true;
    }

    public int GetRepairMetalCost()
    {
        float damageRatio = 1f - (currentHealth / data.maxHealth);
        return Mathf.CeilToInt(data.metalCost * damageRatio);
    }

    public int GetRepairPlasticCost()
    {
        float damageRatio = 1f - (currentHealth / data.maxHealth);
        return Mathf.CeilToInt(data.plasticCost * damageRatio);
    }

    public float GetEnergyProduction()
    {
        if (!isOperational) return 0f;
        return data.energyProductionPerDay * efficiency;
    }

    public float GetEnergyConsumption()
    {
        if (!isOperational) return 0f;
        return data.energyConsumptionPerDay;
    }

    public void SetEfficiencyModifier(float modifier)
    {
        efficiency = Mathf.Clamp(modifier, 0f, 2f);
    }
}
