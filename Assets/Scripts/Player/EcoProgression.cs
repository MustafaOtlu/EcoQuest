using System;
using UnityEngine;

public sealed class EcoProgression : MonoBehaviour
{
    public const int MaximumLevel = 24;
    [SerializeField] private int experience;
    public int Experience => experience;
    public int Level
    {
        get
        {
            int level = 1;
            while (level < MaximumLevel && experience >= Threshold(level + 1)) level++;
            return level;
        }
    }
    public int LevelProgress => experience - Threshold(Level);
    public int LevelRequirement => Level == MaximumLevel ? 0 : Threshold(Level + 1) - Threshold(Level);
    public event Action<int> LevelChanged;
    public static int Threshold(int level)
    {
        level = Mathf.Clamp(level, 1, MaximumLevel);
        return (level - 1) * (40 + 10 * level);
    }
    public bool HasLevel(int required) => required >= 1 && Level >= required;
    public int Award(int amount)
    {
        if (amount <= 0) return 0;
        int before = experience, previousLevel = Level;
        experience = (int)Math.Min(Threshold(MaximumLevel), (long)experience + amount);
        if (Level != previousLevel) LevelChanged?.Invoke(Level);
        return experience - before;
    }
    public void Restore(int value) => experience = Mathf.Clamp(value, 0, Threshold(MaximumLevel));
}
