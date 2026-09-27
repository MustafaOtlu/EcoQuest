using UnityEngine;

public static class EcoCropCatalog
{
    public enum Kind { Cabbage, Carrot, Tomato, Tree }
    public const int Count = 4;
    public readonly struct Crop
    {
        public readonly string Name, Model;
        public readonly int Level, Food;
        public readonly float MinimumTemperature, MaximumTemperature, GrowthSeconds, WaterUse;
        public Crop(string name, string model, int level, int food, float min, float max, float seconds, float water)
        { Name = name; Model = model; Level = level; Food = food; MinimumTemperature = min; MaximumTemperature = max; GrowthSeconds = seconds; WaterUse = water; }
        public bool Suitable(float temperature) => temperature >= MinimumTemperature && temperature <= MaximumTemperature;
        public string Climate => MinimumTemperature.ToString("0") + "–" + MaximumTemperature.ToString("0") + " °C";
    }
    public static readonly Crop[] Entries =
    {
        new Crop("Lahana", "Cabbage", 1, 1, 8, 28, 60, 0.008f),
        new Crop("Havuç", "Carrot", 2, 2, 5, 22, 75, 0.007f),
        new Crop("Domates", "Tomato", 4, 3, 18, 34, 90, 0.012f),
        new Crop("Ağaç", "Tree", 2, 0, 4, 36, 120, 0.006f)
    };
    public static bool Valid(Kind kind) => (int)kind >= 0 && (int)kind < Count;
    public static Crop Get(Kind kind) => Entries[Valid(kind) ? (int)kind : 0];
}
