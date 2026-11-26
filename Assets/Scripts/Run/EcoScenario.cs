using System;
using UnityEngine;

public enum EcoDifficulty { Calm, Normal, Crisis }

/// <summary>The valley's main problem for one run. It weights the region dice, the goal pool and the evening crises.</summary>
public enum EcoProblem { DirtyLake, Smog, Wildfire, Waste, Warming }

/// <summary>Everything a run rolls from its seed before the world is touched. Pure data: the same seed and difficulty
/// always give the same scenario (EditMode tests rely on this), and nothing here reads the scene.</summary>
[Serializable]
public sealed class EcoScenario
{
    public const int TrainingSeed = 20261020;
    public const int RegionKinds = 7; // EcoRegionInfo.Kind count

    public int seed;
    public EcoDifficulty difficulty;
    public bool training;
    public string valleyName;
    public EcoProblem problem;
    /// <summary>Pollution multiplier per EcoRegionInfo.Kind (1 = authored map values).</summary>
    public float[] severity = new float[RegionKinds];
    /// <summary>Vegetation multiplier per kind.</summary>
    public float[] greenery = new float[RegionKinds];
    /// <summary>Kind that gets an extra, unexpected pollution spike (-1: none).</summary>
    public int hotspot = -1;
    public float hotspotAmount;
    public int fires, wastePiles, startMetal, startPlastic;
    public float solarYield = 1f, windYield = 1f;

    private static readonly string[] Names =
    {
        "Kızılçam", "Akdere", "Yeşilyurt", "Karaburun", "Söğütlü", "Çamlıca", "Taşlıca", "Gökova", "Sarıkaya", "Dumanlı",
        "Kavaklı", "Ilıcak", "Bademli", "Kuşluk", "Sazlıdere", "Meşeli"
    };
    public static readonly string[] ProblemNames = { "Kirli göl", "Duman ve is", "Kuru orman, yangın", "Atık yığınları", "Isınan iklim" };
    public static readonly string[] ProblemTexts =
    {
        "Fabrika atığı gölün oksijenini düşürüyor; balık azalıyor.",
        "Bacalar ve trafik havayı kirletiyor; güneş panelleri az üretiyor.",
        "Kuraklık ormanı kurutmuş; yangınlar çıkmış.",
        "Vadiye yasa dışı atık dökülmüş; toprak kirleniyor.",
        "Karbon yüksek; buzul hızla eriyor."
    };
    public static readonly string[] DifficultyNames = { "Sakin", "Normal", "Kriz" };

    public string ProblemName => ProblemNames[(int)problem];
    public string ProblemText => ProblemTexts[(int)problem];

    public static float DifficultyScale(EcoDifficulty difficulty) => difficulty == EcoDifficulty.Calm ? 0.85f : difficulty == EcoDifficulty.Crisis ? 1.2f : 1f;

    /// <summary>The first run is a fixed, gentle valley: authored map values, a dirty lake, the familiar tutorial.</summary>
    public static EcoScenario Training()
    {
        var s = new EcoScenario { seed = TrainingSeed, difficulty = EcoDifficulty.Calm, training = true, valleyName = "Eğitim Vadisi",
            problem = EcoProblem.DirtyLake, fires = 0, wastePiles = 4 };
        for (int i = 0; i < RegionKinds; i++) { s.severity[i] = 1f; s.greenery[i] = 1f; }
        return s;
    }

    public static EcoScenario Roll(int seed, EcoDifficulty difficulty)
    {
        var rng = new System.Random(seed);
        var s = new EcoScenario { seed = seed, difficulty = difficulty };
        s.valleyName = Names[rng.Next(Names.Length)] + " Vadisi";
        s.problem = (EcoProblem)rng.Next(5);
        float scale = DifficultyScale(difficulty);
        for (int i = 0; i < RegionKinds; i++)
        {
            s.severity[i] = Lerp(0.75f, 1.3f, rng.NextDouble()) * scale;
            s.greenery[i] = Lerp(0.8f, 1.15f, rng.NextDouble());
        }
        int Kind(EcoRegionInfo.Kind kind) => (int)kind;
        switch (s.problem)
        {
            case EcoProblem.DirtyLake: s.severity[Kind(EcoRegionInfo.Kind.Lake)] *= 1.35f; break;
            case EcoProblem.Smog:
                s.severity[Kind(EcoRegionInfo.Kind.Industry)] *= 1.3f; s.severity[Kind(EcoRegionInfo.Kind.City)] *= 1.3f; break;
            case EcoProblem.Wildfire: s.greenery[Kind(EcoRegionInfo.Kind.Grove)] *= 0.7f; s.greenery[Kind(EcoRegionInfo.Kind.Wind)] *= 0.8f; break;
            case EcoProblem.Warming: for (int i = 0; i < RegionKinds; i++) s.severity[i] *= 1.12f; break;
        }
        // One surprise: a region that is clean on the authored map can arrive polluted this time.
        var candidates = new[] { EcoRegionInfo.Kind.Wind, EcoRegionInfo.Kind.Sun, EcoRegionInfo.Kind.Grove, EcoRegionInfo.Kind.City };
        s.hotspot = (int)candidates[rng.Next(candidates.Length)];
        s.hotspotAmount = Lerp(10f, 24f, rng.NextDouble()) * scale;
        s.fires = (s.problem == EcoProblem.Wildfire ? 3 : 0) + rng.Next(2) + (difficulty == EcoDifficulty.Crisis ? 1 : 0);
        s.wastePiles = (s.problem == EcoProblem.Waste ? 12 : 4) + rng.Next(4);
        s.solarYield = Lerp(0.85f, 1.15f, rng.NextDouble()) * (s.problem == EcoProblem.Smog ? 0.9f : 1f);
        s.windYield = Lerp(0.85f, 1.15f, rng.NextDouble());
        s.startMetal = rng.Next(0, 4) * 5;
        s.startPlastic = rng.Next(0, 4) * 5;
        return s;
    }

    /// <summary>Scenario value for one authored region reading (index 0..100).</summary>
    public float Pollution(EcoRegionInfo.Kind kind, float authored)
    {
        int k = (int)kind;
        float value = authored * severity[k] + (k == hotspot ? hotspotAmount : 0f);
        return Mathf.Clamp(value, 0f, 95f);
    }

    public float Vegetation(EcoRegionInfo.Kind kind, float authored) => Mathf.Clamp(authored * greenery[(int)kind], 0.03f, 0.95f);

    private static float Lerp(float a, float b, double t) => a + (b - a) * (float)t;
}
