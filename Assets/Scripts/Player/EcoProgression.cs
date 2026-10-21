using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Permanent, finite achievement points. Resource trading is a separate economy.</summary>
public sealed class EcoProgression : MonoBehaviour
{
    public const int PointsPerRight = 40;
    public const int MaximumExperience = 360;
    // Compatibility for old HUD/build APIs: level one means zero earned rights.
    public const int MaximumLevel = 10;
    [SerializeField] private int experience;
    private readonly HashSet<string> rewards = new HashSet<string>(StringComparer.Ordinal);
    public int Experience => experience;
    public int EarnedRights => Mathf.Clamp(experience / PointsPerRight, 0, 9);
    public int Level => EarnedRights + 1;
    public int LevelProgress => experience - Threshold(Level);
    public int LevelRequirement => Level == MaximumLevel ? 0 : PointsPerRight;
    public event Action<int> LevelChanged;
    public event Action<string> RewardGranted;

    public static int Threshold(int level) => (Mathf.Clamp(level, 1, MaximumLevel) - 1) * PointsPerRight;
    public bool HasLevel(int required) => required >= 1 && Level >= required;
    public bool HasReward(string rewardId) => !string.IsNullOrEmpty(rewardId) && rewards.Contains(rewardId);

    public static int RewardValue(string rewardId)
    {
        if (string.IsNullOrEmpty(rewardId)) return 0;
        for (int i = 0; i < 6; i++) if (rewardId == "project." + i) return 40;
        // Care moments (EcoStory): water, meal, birds. "care.0..2" kept so older saves still match.
        if (rewardId == "care.water" || rewardId == "care.meal" || rewardId == "care.birds") return 20;
        for (int i = 0; i < 3; i++) if (rewardId == "care." + i) return 20;
        return rewardId == "field.harvest" || rewardId == "field.fish" || rewardId == "field.placement" ? 20 : 0;
    }

    public int AwardOnce(string rewardId, int amount)
    {
        int expected = RewardValue(rewardId);
        if (expected <= 0 || amount != expected || !rewards.Add(rewardId)) return 0;
        int granted = Award(expected);
        RewardGranted?.Invoke(rewardId);
        return granted;
    }

    // Kept for save/third party API compatibility. Gameplay rewards use AwardOnce with a stable ID.
    public int Award(int amount)
    {
        if (amount <= 0) return 0;
        int before = experience, previous = Level;
        experience = (int)Math.Min(MaximumExperience, (long)experience + amount);
        if (Level != previous) LevelChanged?.Invoke(Level);
        return experience - before;
    }

    public string[] CaptureRewards()
    {
        var result = new List<string>(rewards);
        result.Sort(StringComparer.Ordinal);
        return result.ToArray();
    }

    public void RestoreRewards(string[] saved)
    {
        int previous = Level;
        rewards.Clear();
        if (saved != null) foreach (string id in saved) if (RewardValue(id) > 0) rewards.Add(id);
        int total = 0;
        foreach (string id in rewards) total += RewardValue(id);
        experience = Mathf.Clamp(total, 0, MaximumExperience);
        if (Level != previous) LevelChanged?.Invoke(Level);
    }

    public void Restore(int value)
    {
        int previous = Level;
        experience = Mathf.Clamp(value, 0, MaximumExperience);
        if (Level != previous) LevelChanged?.Invoke(Level);
    }
}
