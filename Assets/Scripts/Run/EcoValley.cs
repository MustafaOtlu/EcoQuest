using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>The movable 50 × 50 m region tiles of a valley (GDD v4 §21, run phase 2).</summary>
public enum EcoTile { Lake, DirtyLake, Industry, Wind, Grove }

/// <summary>
/// The valley is a 3 × 3 grid of 50 m tiles. The top row is always the glacier, the bottom-centre tile is always the
/// sunny plain with the base and the centre tile is always the town. The four side slots are rolled from the run seed:
/// the dirty lake and the factories are always there, one of the clean lake, the windy hills or the forest is left out.
/// A layout is a pure function of the seed, so a save only needs the seed (it also stores the layout code as a check).
/// </summary>
public static class EcoValley
{
    public const float TileSize = 50f;
    public static readonly string[] SlotNames = { "güneybatı", "güneydoğu", "batı", "doğu" };
    /// <summary>World origin (south-west corner) of each side slot: bottom-left, bottom-right, middle-left, middle-right.</summary>
    public static readonly Vector2[] SlotOrigins = { new Vector2(0f, 0f), new Vector2(100f, 0f), new Vector2(0f, 50f), new Vector2(100f, 50f) };
    /// <summary>The training valley keeps the old map's shape: clean lake west, dirty lake east, forest and factories above.</summary>
    public static readonly EcoTile[] TrainingLayout = { EcoTile.Lake, EcoTile.DirtyLake, EcoTile.Grove, EcoTile.Industry };

    /// <summary>Where each tile was authored; the assembler moves it by (slot − home).</summary>
    public static Vector2 HomeOrigin(EcoTile tile) => tile switch
    {
        EcoTile.Lake => new Vector2(0f, 0f),
        EcoTile.DirtyLake => new Vector2(100f, 0f),
        EcoTile.Industry => new Vector2(100f, 50f),
        _ => new Vector2(0f, 50f),
    };

    public static EcoRegionInfo.Kind KindOf(EcoTile tile) => tile switch
    {
        EcoTile.Lake => EcoRegionInfo.Kind.Lake,
        EcoTile.DirtyLake => EcoRegionInfo.Kind.Lake,
        EcoTile.Industry => EcoRegionInfo.Kind.Industry,
        EcoTile.Wind => EcoRegionInfo.Kind.Wind,
        _ => EcoRegionInfo.Kind.Grove,
    };

    public static string TileName(EcoTile tile) => tile switch
    {
        EcoTile.Lake => "Balık Gölü",
        EcoTile.DirtyLake => "Fabrika Gölü",
        EcoTile.Industry => "Fabrikalar",
        EcoTile.Wind => "Rüzgâr Tepeleri",
        _ => "Orman",
    };

    private static readonly EcoTile[] Optional = { EcoTile.Lake, EcoTile.Wind, EcoTile.Grove };

    public static EcoTile[] Layout(int seed, bool training) => Roll(seed, training, out _);

    /// <summary>Quarter turns (0–3, clockwise seen from above) of the four side tiles. Every side tile has flat edges
    /// and a road mouth in the middle of each edge, so any turn still meets its neighbours. The training valley is
    /// never turned.</summary>
    public static int[] Turns(int seed, bool training) { Roll(seed, training, out var turns); return turns; }

    private static EcoTile[] Roll(int seed, bool training, out int[] turns)
    {
        turns = new int[SlotOrigins.Length];
        if (training) return (EcoTile[])TrainingLayout.Clone();
        var rng = new System.Random(unchecked(seed * 6151 + 29));
        var left = Optional[rng.Next(Optional.Length)];
        var tiles = new List<EcoTile> { EcoTile.DirtyLake, EcoTile.Industry };
        tiles.AddRange(Optional.Where(t => t != left));
        for (int i = tiles.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (tiles[i], tiles[j]) = (tiles[j], tiles[i]);
        }
        for (int i = 0; i < turns.Length; i++) turns[i] = rng.Next(4);
        return tiles.ToArray();
    }

    public static EcoTile[] Layout(EcoScenario scenario) => scenario == null ? (EcoTile[])TrainingLayout.Clone() : Layout(scenario.seed, scenario.training);
    public static int[] Turns(EcoScenario scenario) => scenario == null ? new int[SlotOrigins.Length] : Turns(scenario.seed, scenario.training);
    /// <summary>The full valley code of a scenario: tiles and their turns ("Lake2-DirtyLake-Grove1-Industry3").</summary>
    public static string PlanCode(EcoScenario scenario) => Code(Layout(scenario), Turns(scenario));

    /// <summary>The region kinds a layout contains, for picking goals before the valley exists.</summary>
    public static HashSet<EcoRegionInfo.Kind> Kinds(EcoTile[] layout)
    {
        var kinds = new HashSet<EcoRegionInfo.Kind> { EcoRegionInfo.Kind.Glacier, EcoRegionInfo.Kind.Sun, EcoRegionInfo.Kind.City };
        foreach (var tile in layout) kinds.Add(KindOf(tile));
        return kinds;
    }

    public static string Code(EcoTile[] layout, int[] turns = null) => layout == null ? "" : string.Join("-", layout.Select((t, i) =>
        t.ToString() + (turns != null && i < turns.Length && (turns[i] & 3) != 0 ? (turns[i] & 3).ToString() : "")));

    public static EcoTile[] Parse(string code) => Parse(code, out _);

    /// <summary>Reads a valley code; a tile without a digit is not turned (codes saved before turns existed).</summary>
    public static EcoTile[] Parse(string code, out int[] turns)
    {
        turns = new int[SlotOrigins.Length];
        if (string.IsNullOrEmpty(code)) return null;
        var parts = code.Split('-');
        if (parts.Length != SlotOrigins.Length) return null;
        var result = new EcoTile[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string name = parts[i];
            if (name.Length > 1 && char.IsDigit(name[name.Length - 1]))
            {
                turns[i] = (name[name.Length - 1] - '0') & 3;
                name = name.Substring(0, name.Length - 1);
            }
            if (!Enum.TryParse(name, out result[i]) || !Enum.IsDefined(typeof(EcoTile), result[i])) return null;
        }
        return result;
    }

    /// <summary>A short description for the briefing ("batıda Orman, doğuda Fabrikalar…").</summary>
    public static string Describe(EcoTile[] layout)
    {
        if (layout == null) return "";
        var parts = new List<string>();
        for (int i = 0; i < layout.Length; i++) parts.Add(SlotNames[i] + ": " + TileName(layout[i]));
        var missing = Optional.Where(t => !layout.Contains(t)).Select(TileName).ToList();
        return string.Join(" · ", parts) + (missing.Count > 0 ? "  (bu vadide " + string.Join(", ", missing) + " yok)" : "");
    }

    /// <summary>Authored starting pollution (air, water, soil) of each tile, as built; the briefing shows a valley
    /// before it is assembled.</summary>
    private static Vector3 Authored(EcoTile tile) => tile switch
    {
        EcoTile.Lake => new Vector3(4, 8, 6),
        EcoTile.DirtyLake => new Vector3(22, 64, 35),
        EcoTile.Industry => new Vector3(66, 55, 58),
        EcoTile.Wind => new Vector3(6, 6, 6),
        _ => new Vector3(8, 8, 22),
    };

    public struct Row { public string name, place; public EcoRegionInfo.Kind kind; public Vector3 pollution; }

    /// <summary>Every region of a layout with where it lies, for the briefing.</summary>
    public static List<Row> Rows(EcoTile[] layout)
    {
        var rows = new List<Row>
        {
            new Row { name = "Güneş Ovası", place = "üs", kind = EcoRegionInfo.Kind.Sun, pollution = new Vector3(5, 8, 10) },
            new Row { name = "Şehir", place = "orta", kind = EcoRegionInfo.Kind.City, pollution = new Vector3(20, 22, 20) },
            new Row { name = "Buzul Dağı", place = "kuzey", kind = EcoRegionInfo.Kind.Glacier, pollution = new Vector3(2, 2, 0) },
        };
        for (int i = 0; i < layout.Length && i < SlotNames.Length; i++)
            rows.Add(new Row { name = TileName(layout[i]), place = SlotNames[i], kind = KindOf(layout[i]), pollution = Authored(layout[i]) });
        return rows;
    }

    // ----------------------------------------------------------- across scene reloads

    private static string requested;
    /// <summary>The valley code the next scene load assembles (a run start or a load from another valley sets it).</summary>
    public static void Request(string code) => requested = code;
    public static string ConsumeRequest() { var value = requested; requested = null; return value; }
    /// <summary>Set while a reload into another valley is in flight, so the title screen stays closed.</summary>
    public static bool SkipTitle { get; set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { requested = null; SkipTitle = false; }
}
