using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "EcoQuest/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public EnemyType enemyType;
    public string displayName;
    public Sprite sprite;

    [Header("Ozellikler")]
    public float maxHealth = 100f;
    public float moveSpeed = 2f;
    public float attackDamage = 10f;
    public float attackInterval = 2f;
    public float detectionRange = 8f;

    [Header("Hedefler")]
    public BuildingType[] targetBuildings;
    public bool attacksPlayer;
    public bool attacksEcosystem;

    [Header("Etkisizlestirme")]
    public EquipmentType[] weakAgainst;
    [TextArea] public string defeatMethod;

    [Header("Odul")]
    public ResourceType rewardResource;
    public int rewardAmount;
    public float yepReward;

    [Header("Spawn")]
    public BiomeType[] spawnBiomes;
    public float spawnChancePerDay = 0.3f;
    public int maxActiveCount = 3;
}
