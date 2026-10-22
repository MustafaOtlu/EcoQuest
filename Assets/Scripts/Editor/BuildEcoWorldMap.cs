using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates Assets/Scenes/EcoWorld.unity: a compact, GDD-shaped world map.
//  * MainScene is never modified. EcoWorld starts as a copy of it so the working player rig, weapons, enemies, HUD,
//    clock and mechanic stations stay wired; only the old scenery (terrain, houses, roads, lamps…) is dropped.
//  * No Unity Terrain: the ground is a chunked low-poly mesh with vertex colours (EcoQuest/LowPoly Vertex Lit) and
//    mesh colliders tagged EcoGround. Roads are spline ribbons (asphalt, kerbs, pavements, markings, crossings,
//    dirt trails, streams) that follow a smoothed height field, so the town reads as streets rather than a grid.
//  * Layout (150 × 150 m, authored on a 150 m frame and scaled by L): glacier north, windy hills + grove west,
//    town centre with a roundabout, industry east, sunny meadows and the starting base south, two lakes.
//  * Run from the menu: EcoQuest > Build EcoWorld map scene.
public static class BuildEcoWorldMap
{
    private const string SourceScene = "Assets/Scenes/MainScene.unity";
    private const string TargetScene = "Assets/Scenes/EcoWorld.unity";
    private const string Folder = "Assets/EcoWorld/";
    private const string ResFolder = "Assets/Resources/EcoWorld/";
    // Playable edge length in metres. Every position below is authored for 150 m and multiplied by L.
    private const float Size = 150f;
    private const float L = Size / 150f;
    private const float Border = 25.5f;   // hills/forest outside the walls, visual only
    private const float Cell = 1.5f;      // ground mesh resolution
    private const float Pave = 1.6f;      // pavement width beside urban streets
    private const int ChunkCells = 34;
    private const float Ground0 = 22f;
    private const float WaterY = 20.6f;
    private static readonly Vector2 Base = new Vector2(75f * L, 16f * L);
    private static readonly Vector2 Roundabout = new Vector2(75f * L, 80f * L);
    private const float RoundaboutR = 8f;
    private static readonly Vector2 LakeA = new Vector2(24f * L, 30f * L);
    private static readonly Vector2 LakeB = new Vector2(127f * L, 30f * L);
    private const float LakeAR = 11f, LakeBR = 10f;
    private static readonly Vector2 StationPad = new Vector2(96f * L, 133f * L);
    // GDD §9: level ground beside the Dağ Deresi mouth so the small hydro plant (6×6 m) can actually be placed.
    private static readonly Vector2 HydroPad = new Vector2(6.5f * L, 24.5f * L);
    private static Vector2 P(float x, float z) => new Vector2(x * L, z * L);

    // ---------------------------------------------------------------- definitions

    private sealed class RegionDef
    {
        public string name, tagline;
        public EcoRegionInfo.Kind kind;
        public Vector2 center, size;
        public float air, water, soil, carbon, temperature, vegetation, habitat, wind = 1f, sun = 1f;
        public bool sensitive;
        public int priority, project = -1;
        public string[] features = new string[0];
        public Color color;
    }

    private enum RoadKind { Avenue, Street, Industrial, Country, Trail, Stream, DirtyStream, Ring }

    private sealed class RoadDef
    {
        public RoadKind kind; public Vector2[] points; public bool closed; public string name;
        public readonly List<Vector2> pos = new List<Vector2>();
        public readonly List<Vector2> tan = new List<Vector2>();
        public readonly List<float> dist = new List<float>();
        public bool[] junction, nearJunction;
        public Rect bounds;
        public float Width => kind switch
        {
            RoadKind.Avenue => 6f, RoadKind.Ring => 6f, RoadKind.Street => 4.4f, RoadKind.Industrial => 5.6f,
            RoadKind.Country => 4.4f, RoadKind.Trail => 2.4f, RoadKind.Stream => 2.6f, _ => 2.2f,
        };
        public bool Paved => kind == RoadKind.Avenue || kind == RoadKind.Street || kind == RoadKind.Industrial || kind == RoadKind.Country || kind == RoadKind.Ring;
        public bool Urban => kind == RoadKind.Avenue || kind == RoadKind.Street || kind == RoadKind.Ring;
        public bool Water => kind == RoadKind.Stream || kind == RoadKind.DirtyStream;
        public RoadDef(string name, RoadKind kind, params Vector2[] points) { this.name = name; this.kind = kind; this.points = points; }
    }

    private sealed class ModelSet
    {
        public readonly List<GameObject> models = new List<GameObject>();
        public readonly List<float> heights = new List<float>();
        public float average = 1f;
        public bool Any => models.Count > 0;
    }

    private static readonly List<RegionDef> Defs = new List<RegionDef>();
    private static readonly List<RoadDef> Roads = new List<RoadDef>();
    private static readonly List<Rect> KeepOut = new List<Rect>();
    private static System.Random rng;
    private static StringBuilder report;
    private static Transform worldRoot;
    private static readonly Dictionary<string, Transform> Groups = new Dictionary<string, Transform>();
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
    private static GameObject housePrefab, lampPrefab, polePrefab, solarPrefab;

    // Height grid (vertex heights of the ground mesh) and the smoothed field roads follow.
    private static int N;                 // cells per side
    private static float Origin;          // world x/z of vertex (0,0)
    private static float[,] heights, roadField, roadDistance;
    private static float stationPadHeight;

    private static RegionDef Def(EcoRegionInfo.Kind kind) => Defs.First(d => d.kind == kind);

    private static void DefineRegions()
    {
        Defs.Clear();
        Defs.Add(new RegionDef { name = "Buzul Bölgesi", kind = EcoRegionInfo.Kind.Glacier, center = P(75, 130), size = P(150, 40), project = 5,
            tagline = "Erime riski altındaki buzul. Dünya karbonu arttıkça buz çizgisi geri çekilir.", air = 2, water = 2, soil = 0, carbon = 6,
            temperature = -9f, vegetation = 0.05f, habitat = 0.4f, sensitive = true, wind = 1.4f, sun = 0.9f, color = new Color(0.72f, 0.90f, 1.00f),
            features = new[] { "İzleme istasyonu: dünya karbonunu ölçer", "Patika şehirden kuzeye çıkar", "Soğuk: ekime uygun değil" } });
        Defs.Add(new RegionDef { name = "Rüzgârlı Tepeler", kind = EcoRegionInfo.Kind.Wind, center = P(22, 80), size = P(44, 60), project = -1,
            tagline = "Açık tepeler. Rüzgâr türbinleri için en verimli yer.", air = 8, water = 6, soil = 6, carbon = 8,
            temperature = 13f, vegetation = 0.65f, habitat = 0.8f, wind = 1.6f, sun = 0.95f, color = new Color(0.45f, 0.82f, 0.74f),
            features = new[] { "Rüzgâr türbini verimi yüksek", "Koruluğa komşu: türbini ağaçlardan uzak kur" } });
        Defs.Add(new RegionDef { name = "Koruluk", kind = EcoRegionInfo.Kind.Grove, center = P(22, 99), size = P(40, 20), priority = 1, project = 2,
            tagline = "Zarar görmüş ağaçlar ve sessiz bir orman. Kuşlar geri dönmeyi bekliyor.", air = 10, water = 8, soil = 22, carbon = 14,
            temperature = 14f, vegetation = 0.34f, habitat = 0.28f, sensitive = true, wind = 0.7f, sun = 0.7f, color = new Color(0.36f, 0.66f, 0.36f),
            features = new[] { "Ağaç tohumu ekmek için ideal", "Hassas habitat: büyük tesis kurma", "Kuş ve kelebekler habitatla döner" } });
        Defs.Add(new RegionDef { name = "Şehir Merkezi", kind = EcoRegionInfo.Kind.City, center = P(75, 80), size = P(62, 60), project = 4,
            tagline = "Ev, pazar ve meydan. Güvenli hazırlık alanı; çocuklar burada büyüyor.", air = 32, water = 26, soil = 24, carbon = 32,
            temperature = 18f, vegetation = 0.25f, habitat = 0.3f, wind = 0.6f, sun = 0.85f, color = new Color(0.95f, 0.84f, 0.55f),
            features = new[] { "Ev ve mahalle çocuğu", "Göbekli kavşak ve meydan", "Çevresel tehdit oluşmaz" } });
        Defs.Add(new RegionDef { name = "Sanayi Bölgesi", kind = EcoRegionInfo.Kind.Industry, center = P(128, 80), size = P(44, 60), project = 3,
            tagline = "Filtresiz fabrikalar. Hava ve karbon kirliliğinin ana kaynağı.", air = 66, water = 55, soil = 58, carbon = 58,
            temperature = 19f, vegetation = 0.10f, habitat = 0f, wind = 0.9f, sun = 0.7f, color = new Color(0.90f, 0.48f, 0.38f),
            features = new[] { "Fabrikalara büyük filtre takılabilir", "Kirli kanal Doğu Gölü'ne akıyor", "Teneke ve Duman görülebilir" } });
        Defs.Add(new RegionDef { name = "Güneşli Çayır", kind = EcoRegionInfo.Kind.Sun, center = P(75, 25), size = P(62, 50), project = 0,
            tagline = "Geniş açık çayır. İlk güneş üretimi ve küçük tarım için ideal.", air = 6, water = 8, soil = 10, carbon = 10,
            temperature = 24f, vegetation = 0.50f, habitat = 0.7f, wind = 0.8f, sun = 1.15f, color = new Color(1.00f, 0.84f, 0.38f),
            features = new[] { "Güneş paneli verimi yüksek", "Başlangıç üssü ve atölye", "Sıcak: domates ve lahana" } });
        Defs.Add(new RegionDef { name = "Batı Gölü", kind = EcoRegionInfo.Kind.Lake, center = P(22, 25), size = P(44, 50), project = -1,
            tagline = "Temiz tatlı su ve balık. Su sınırlıdır, aşırı avlama.", air = 5, water = 8, soil = 6, carbon = 6,
            temperature = 17f, vegetation = 0.70f, habitat = 0.85f, sensitive = true, wind = 1.1f, sun = 1f, color = new Color(0.45f, 0.74f, 1.00f),
            features = new[] { "Temiz su ve balıkçılık", "Doğal akış: küçük hidro kurulabilir", "Hassas kıyı" } });
        Defs.Add(new RegionDef { name = "Doğu Gölü", kind = EcoRegionInfo.Kind.Lake, center = P(128, 25), size = P(44, 50), project = 1,
            tagline = "Sanayi kanalının döküldüğü göl. Giriş kapanmalı, su arıtılmalı.", air = 28, water = 64, soil = 35, carbon = 22,
            temperature = 20f, vegetation = 0.30f, habitat = 0.1f, wind = 1f, sun = 1f, color = new Color(0.58f, 0.62f, 0.40f),
            features = new[] { "Kirletici giriş kanalı", "Salya görülebilir", "Pompa + arıtma + depo ile göle temiz dönüş" } });
    }

    private static void DefineRoads()
    {
        Roads.Clear();
        Vector2 V(float x, float z) => new Vector2(x * L, z * L);
        Roads.Add(new RoadDef("Ana Cadde (güney)", RoadKind.Avenue, V(75, 29), V(75, 40), V(74.6f, 52), V(75, 63), V(75, 72.4f)));
        Roads.Add(new RoadDef("Ana Cadde (kuzey)", RoadKind.Avenue, V(75, 87.6f), V(75.4f, 98), V(76.4f, 106), V(77.6f, 111)));
        Roads.Add(new RoadDef("Batı Bulvarı", RoadKind.Street, V(67.4f, 80), V(60, 80.4f), V(52, 81.5f), V(46, 83.2f), V(38, 86), V(29, 89.5f), V(20, 93.2f), V(12, 96.5f)));
        Roads.Add(new RoadDef("Doğu Bulvarı", RoadKind.Street, V(82.6f, 80), V(90, 79.6f), V(98, 79.4f), V(106, 79.6f), V(114, 80.2f), V(122, 81.2f), V(132, 81.6f), V(142, 80.6f), V(147, 80)));
        // Four side streets leave the main cross near the town edge in a pinwheel, each bending away at its own angle.
        // The quarters between them stay deep enough (25–30 m) for a front row and back lots, like a real small town.
        Roads.Add(new RoadDef("Çınar Sokak", RoadKind.Street, V(75.6f, 101.6f), V(68, 102), V(60, 103.2f), V(52, 105.2f), V(45, 107.6f)));
        Roads.Add(new RoadDef("Lale Sokak", RoadKind.Street, V(74.8f, 57.6f), V(82, 57.2f), V(90, 56), V(98, 53.6f), V(105, 50.6f)));
        Roads.Add(new RoadDef("Okul Sokağı", RoadKind.Street, V(52, 81.5f), V(51.4f, 74), V(50, 66), V(48, 58), V(46, 51.6f)));
        Roads.Add(new RoadDef("Fırın Sokağı", RoadKind.Street, V(98, 79.4f), V(98.6f, 87), V(100, 95), V(102, 102), V(104, 108)));
        Roads.Add(new RoadDef("Batı Göl Yolu", RoadKind.Country, V(75, 34), V(66, 33.6f), V(56, 33), V(46, 33.4f), V(37.5f, 32.6f)));
        Roads.Add(new RoadDef("Doğu Göl Yolu", RoadKind.Country, V(75, 34), V(84, 33.6f), V(94, 33), V(104, 33.2f), V(114.5f, 32.4f)));
        Roads.Add(new RoadDef("Fabrika Halkası", RoadKind.Industrial, V(118, 80.8f), V(117.4f, 90), V(122, 96), V(132, 97.2f), V(140, 93.4f), V(141.2f, 86.4f), V(140.4f, 81.2f)));
        Roads.Add(new RoadDef("Depo Yolu", RoadKind.Industrial, V(124, 81.4f), V(124.4f, 72), V(127.4f, 64.4f), V(132.6f, 60.4f)));
        Roads.Add(new RoadDef("Buzul Patikası", RoadKind.Trail, V(77.6f, 110), V(81, 117), V(87, 124), V(92.4f, 129.4f), V(95.4f, 131.6f)));
        Roads.Add(new RoadDef("Türbin Patikası", RoadKind.Trail, V(29, 89.5f), V(26, 80), V(20.5f, 72), V(17, 64), V(21, 57.4f), V(30, 55.6f), V(40, 57.4f), V(45, 61)));
        Roads.Add(new RoadDef("Koruluk Patikası", RoadKind.Trail, V(20, 93.2f), V(17, 100), V(23.6f, 104.4f), V(32.4f, 101.6f), V(36.4f, 95.4f)));
        Roads.Add(new RoadDef("Batı Kıyı Patikası", RoadKind.Trail, V(37.5f, 32.6f), V(36, 22), V(27, 16.4f), V(16, 18.6f), V(11.4f, 26)));
        Roads.Add(new RoadDef("Doğu Kıyı Patikası", RoadKind.Trail, V(114.5f, 32.4f), V(118.4f, 40.6f), V(126, 43.4f), V(132, 41.6f)));
        Roads.Add(new RoadDef("Batı Güneş Tarlası", RoadKind.Trail, V(66, 33.6f), V(65, 37.4f), V(63.4f, 41)));
        Roads.Add(new RoadDef("Doğu Güneş Tarlası", RoadKind.Trail, V(84, 33.6f), V(85, 37.4f), V(86.6f, 41)));
        Roads.Add(new RoadDef("Dağ Deresi", RoadKind.Stream, V(-16, 47), V(-5, 42.6f), V(3, 37.4f), V(9, 33.4f), V(14.4f, 30.6f)));
        Roads.Add(new RoadDef("Kirli Kanal", RoadKind.DirtyStream, V(135.6f, 61.4f), V(137.6f, 52), V(137.2f, 44), V(136.2f, 37), V(135.4f, 31.4f)));
        var ring = new RoadDef("Göbekli Kavşak", RoadKind.Ring) { closed = true };
        var circle = new Vector2[24];
        for (int i = 0; i < circle.Length; i++)
        {
            float a = i * Mathf.PI * 2f / circle.Length;
            circle[i] = Roundabout + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RoundaboutR;
        }
        ring.points = circle;
        Roads.Add(ring);
    }

    // ---------------------------------------------------------------- math helpers

    private static float Smooth(float a, float b, float v)
    {
        float t = Mathf.Clamp01((v - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Fbm(float x, float z, int octaves)
    {
        float sum = 0f, amp = 1f, norm = 0f, freq = 1f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Mathf.PerlinNoise(x * freq + 37.2f * i, z * freq + 91.7f * i) * amp;
            norm += amp; amp *= 0.5f; freq *= 2f;
        }
        return sum / norm;
    }

    private static float RectMask(float x, float z, float cx, float cz, float hx, float hz, float feather)
    {
        float dx = Mathf.Max(Mathf.Abs(x - cx) - hx, 0f), dz = Mathf.Max(Mathf.Abs(z - cz) - hz, 0f);
        return 1f - Smooth(0f, feather, Mathf.Sqrt(dx * dx + dz * dz));
    }

    private static float CircleMask(float x, float z, Vector2 c, float radius, float feather)
        => 1f - Smooth(radius, radius + feather, Vector2.Distance(new Vector2(x, z), c));

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Outside(float x, float z) => Mathf.Max(Mathf.Max(-x, x - Size), Mathf.Max(-z, z - Size));

    private static float LakeShape(float h, float x, float z, Vector2 c, float r)
    {
        float d = Vector2.Distance(new Vector2(x, z), c);
        float rim = 1f - Smooth(r * 1.0f, r * 1.45f, d);
        h = Mathf.Lerp(h, Mathf.Max(h, WaterY + 0.8f), rim); // a dry rim keeps valleys from leaking under the water plane
        float bowl = 1f - Smooth(r * 0.55f, r * 1.04f, d);
        float bottom = WaterY - 3.6f + (d / r) * 3f;
        return Mathf.Lerp(h, Mathf.Min(h, bottom), bowl);
    }

    private static float GlacierRise(float z) => Smooth(110f * L, 150f * L, z);

    /// <summary>Height before roads are cut in.</summary>
    private static float RawHeight(float x, float z)
    {
        float h = Ground0 + (Fbm(x * 0.035f, z * 0.035f, 3) - 0.5f) * 1.4f;
        // Rolling windy hills in the west (crests for turbines).
        float wm = RectMask(x, z, 22f * L, 74f * L, 16f * L, 18f * L, 12f);
        h += wm * Mathf.Max(0f, Fbm(x * 0.05f + 3f, z * 0.05f + 8f, 3) - 0.36f) * 10f;
        // The glacier plateau rises gently to the north.
        float g = GlacierRise(z);
        h += g * (5.5f + (Fbm(x * 0.07f, z * 0.07f, 3) - 0.5f) * 2.5f);
        // Hills and forest beyond the walls; high mountains behind the glacier.
        float outside = Outside(x, z);
        h += Smooth(-3f, 18f, outside) * (5f + Fbm(x * 0.05f + 5f, z * 0.05f, 3) * 16f);
        float north = Smooth(Size - 4f, Size + 22f, z);
        float ridge = 1f - Mathf.Abs(2f * Fbm(x * 0.03f + 11f, z * 0.03f + 5f, 4) - 1f);
        h += north * (14f + ridge * ridge * 36f);
        // Level building grounds.
        h = Mathf.Lerp(h, Ground0 + 0.3f, RectMask(x, z, 75f * L, 80f * L, 33f * L, 30f * L, 8f));
        h = Mathf.Lerp(h, Ground0 - 0.1f, RectMask(x, z, 128f * L, 80f * L, 21f * L, 25f * L, 8f));
        h = Mathf.Lerp(h, Ground0, RectMask(x, z, 75f * L, 30f * L, 26f * L, 22f * L, 10f) * 0.85f);
        h = Mathf.Lerp(h, Ground0, CircleMask(x, z, Base, 17f, 8f));
        h = Mathf.Lerp(h, stationPadHeight, CircleMask(x, z, StationPad, 6.5f, 5f));
        h = Mathf.Lerp(h, Ground0 - 0.4f, CircleMask(x, z, HydroPad, 4.6f, 2.5f));
        h = LakeShape(h, x, z, LakeA, LakeAR);
        h = LakeShape(h, x, z, LakeB, LakeBR);
        return h;
    }

    // ------------------------------------------------------------ height grid queries

    private static float VX(int i) => Origin + i * Cell;

    /// <summary>Exact height of the generated ground mesh (same triangle split as the mesh).</summary>
    private static float GroundY(float x, float z) => SampleGrid(heights, x, z);

    private static float SampleGrid(float[,] grid, float x, float z)
    {
        float gx = Mathf.Clamp((x - Origin) / Cell, 0f, N - 0.0001f), gz = Mathf.Clamp((z - Origin) / Cell, 0f, N - 0.0001f);
        int i = Mathf.FloorToInt(gx), j = Mathf.FloorToInt(gz);
        float u = gx - i, v = gz - j;
        float h00 = grid[i, j], h10 = grid[i + 1, j], h01 = grid[i, j + 1], h11 = grid[i + 1, j + 1];
        return v >= u ? h00 + (h11 - h01) * u + (h01 - h00) * v : h00 + (h10 - h00) * u + (h11 - h10) * v;
    }

    private static float Steepness(float x, float z)
    {
        const float e = 0.75f;
        float dx = (GroundY(x + e, z) - GroundY(x - e, z)) / (2f * e), dz = (GroundY(x, z + e) - GroundY(x, z - e)) / (2f * e);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
    }

    private static float RoadEdgeDistance(float x, float z, bool includeWater = true, bool includeTrails = true)
    {
        float best = float.MaxValue; var p = new Vector2(x, z);
        foreach (var road in Roads)
        {
            if (!includeWater && road.Water) continue;
            if (!includeTrails && road.kind == RoadKind.Trail) continue;
            float margin = road.Width * 0.5f + (road.Urban ? Pave + 0.4f : 0f);
            if (!road.bounds.Contains(p) && Distance(road.bounds, p) > best + margin) continue;
            for (int i = 0; i + 1 < road.pos.Count; i++)
                best = Mathf.Min(best, DistToSegment(p, road.pos[i], road.pos[i + 1]) - margin);
        }
        return best;
    }

    private static float Distance(Rect r, Vector2 p)
    {
        float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax), dz = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    private static int RegionIndexAt(float x, float z)
    {
        int best = -1, bestPriority = int.MinValue;
        for (int i = 0; i < Defs.Count; i++)
        {
            var d = Defs[i];
            if (Mathf.Abs(x - d.center.x) > d.size.x * 0.5f || Mathf.Abs(z - d.center.y) > d.size.y * 0.5f) continue;
            if (d.priority > bestPriority) { best = i; bestPriority = d.priority; }
        }
        return best;
    }

    private static float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    // ---------------------------------------------------------------- entry point

    private static bool silent;

    [MenuItem("EcoQuest/Build EcoWorld map scene")]
    public static void Build() { silent = false; BuildInternal(); }

    // Same build without any dialog (used by automation; overwrites EcoWorld.unity and saves open scenes).
    [MenuItem("EcoQuest/Build EcoWorld map scene (no prompts)")]
    public static void BuildSilent() { silent = true; BuildInternal(); }

    private static void BuildInternal()
    {
        if (!File.Exists(SourceScene))
        {
            EditorUtility.DisplayDialog("EcoWorld", "Kaynak sahne bulunamadı:\n" + SourceScene, "Tamam");
            return;
        }
        if (!silent && File.Exists(TargetScene) && !EditorUtility.DisplayDialog("EcoWorld",
            "Assets/Scenes/EcoWorld.unity zaten var. Yeniden oluşturulsun mu?\n(MainScene'e dokunulmaz; yalnız EcoWorld sahnesi yenilenir.)", "Yeniden oluştur", "İptal")) return;
        if (silent) EditorSceneManager.SaveOpenScenes();
        else if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        report = new StringBuilder("EcoWorld oluşturma raporu\n");
        rng = new System.Random(20261020);
        KeepOut.Clear(); Groups.Clear(); MaterialCache.Clear();
        DefineRegions(); DefineRoads();
        try
        {
            Directory.CreateDirectory(Folder + "Materials"); Directory.CreateDirectory(Folder + "Meshes"); Directory.CreateDirectory(ResFolder);
            AssetDatabase.Refresh();
            Progress("Sahne kopyalanıyor", 0.02f);
            // Overwrite in place so the scene keeps its GUID (Build Settings and other references stay valid).
            if (File.Exists(TargetScene)) { File.Copy(SourceScene, TargetScene, true); AssetDatabase.ImportAsset(TargetScene, ImportAssetOptions.ForceUpdate); }
            else if (!AssetDatabase.CopyAsset(SourceScene, TargetScene)) throw new System.Exception("MainScene kopyalanamadı.");
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

            Progress("Eski dekor ayıklanıyor", 0.06f);
            HarvestAndClean(scene);
            CleanGeneratedAssets();

            worldRoot = new GameObject("EcoWorld").transform;
            foreach (var groupName in new[] { "Ground", "Roads", "Regions", "Lakes", "City", "Industry", "Energy", "Glacier", "Vegetation", "Props", "Bounds" })
            {
                var t = new GameObject(groupName).transform; t.SetParent(worldRoot, false); Groups[groupName] = t;
            }

            Progress("Yükseklik ve yollar", 0.10f);
            ComputeHeights();
            Progress("Zemin", 0.22f);
            BuildGround();
            Progress("Yollar", 0.32f);
            BuildRoads();
            BuildBounds();
            Progress("Bölgeler", 0.40f);
            BuildRegions();
            Progress("Göller", 0.44f);
            BuildLakes();
            Progress("Şehir", 0.50f);
            BuildCity();
            Progress("Sanayi", 0.60f);
            BuildIndustry();
            Progress("Enerji sahaları", 0.68f);
            BuildEnergy();
            Progress("Buzul", 0.74f);
            BuildGlacier();
            Progress("Bitki örtüsü", 0.80f);
            BuildVegetation();
            BuildSigns();
            FixBuiltinMaterials();
            Progress("Başlangıç üssü ve arayüz", 0.90f);
            PlaceBase();
            Progress("Harita görüntüsü", 0.94f);
            BuildHud();
            new GameObject("Eco Orders").AddComponent<EcoOrders>();                 // GDD §8 order board ([O] at a market)
            new GameObject("Eco Projects").AddComponent<EcoProjects>();             // GDD §7 project log ([J]) and final readiness
            new GameObject("Eco Finale").AddComponent<EcoFinale>();                 // GDD §12 final encounter
            new GameObject("Eco World Save").AddComponent<EcoWorldSave>();          // GDD §14 world save (F5/F9 + autosave)
            new GameObject("Eco Audio").AddComponent<EcoAudio>();                   // GDD §15 procedural ambience + settings ([F2])
            new GameObject("Eco Specialization").AddComponent<EcoSpecialization>(); // GDD §7 specialisations ([K])
            RegisterInBuildSettings();

            report.AppendLine("Sahne: " + TargetScene);
            File.WriteAllText(Folder + "BuildReport.txt", report.ToString());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("ECOWORLD_READY\n" + report);
            if (!silent) EditorUtility.DisplayDialog("EcoWorld hazır",
                "Assets/Scenes/EcoWorld.unity oluşturuldu.\n\nMainScene değiştirilmedi.\nRapor: Assets/EcoWorld/BuildReport.txt\n\nPlay'e basıp harita üzerinde test edebilirsin. [M] tuşu haritayı açar.", "Tamam");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("EcoWorld oluşturulamadı: " + ex);
            if (!silent) EditorUtility.DisplayDialog("EcoWorld", "Hata: " + ex.Message + "\n\nAyrıntı Console'da. MainScene etkilenmedi.", "Tamam");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void Progress(string text, float value) => EditorUtility.DisplayProgressBar("EcoWorld", text, value);

    // The previous terrain build left large generated assets behind; they are no longer referenced.
    private static void CleanGeneratedAssets()
    {
        foreach (var path in new[] { Folder + "EcoWorldTerrain.asset", Folder + "Textures" })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
        foreach (var guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { Folder.TrimEnd('/') }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        foreach (var guid in AssetDatabase.FindAssets("t:Mesh", new[] { Folder + "Meshes" }))
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
    }

    // ---------------------------------------------------------------- scene cleanup

    private static GameObject HarvestPrefab(GameObject groupObject)
    {
        if (groupObject == null) return null;
        var counts = new Dictionary<GameObject, int>();
        foreach (Transform child in groupObject.transform)
        {
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(child.gameObject);
            if (root == null) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
            if (source == null) continue;
            counts[source] = counts.TryGetValue(source, out int n) ? n + 1 : 1;
        }
        return counts.Count == 0 ? null : counts.OrderByDescending(pair => pair.Value).First().Key;
    }

    private static void HarvestAndClean(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        GameObject Find(System.Func<string, bool> match) { foreach (var r in roots) if (r != null && match(r.name)) return r; return null; }
        housePrefab = HarvestPrefab(Find(n => n.Contains("Evler")));
        lampPrefab = HarvestPrefab(Find(n => n.Contains("Lamba")));
        polePrefab = HarvestPrefab(Find(n => n.Contains("Diregi")));
        solarPrefab = HarvestPrefab(Find(n => n.Contains("Dizisi")));
        report.AppendLine("Eski haritadan alınan prefablar (kopyalanmadı, yalnız yeniden kullanıldı): ev=" + Name(housePrefab)
            + ", lamba=" + Name(lampPrefab) + ", direk=" + Name(polePrefab) + ", güneş dizisi=" + Name(solarPrefab));

        bool alanExists = roots.Any(r => r != null && r.name == "alan");
        var keepNames = new HashSet<string> { "alan", "Enemys", "Player", "Cam's", "Daylight" };
        foreach (var root in roots)
        {
            if (root == null) continue;
            string n = root.name;
            // Root-level starter copies (the live ones sit inside "alan") and old-map scenery would otherwise stay as buried or far-away strays.
            bool stray = alanExists && (n == "Eco Field Station" || n == "PowerStarter" || n == "Rock" || n == "WaterIntake" || n.StartsWith("WindTurbine"));
            bool remove = stray || n.Contains("Evler") || n.Contains("Yollar") || n.Contains("Lamba") || n.Contains("Diregi") || n.Contains("Dizisi")
                || n == "Gecici" || (n.Contains("lge1") && root.transform.childCount == 0);
            if (!remove && !keepNames.Contains(n)) remove = IsPureScenery(root);
            if (remove) { report.AppendLine("  silindi (kopyada): " + n); Object.DestroyImmediate(root); }
            else report.AppendLine("  korundu: " + n);
        }
        // Any terrain left inside kept groups would cover the new ground mesh.
        foreach (var terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            report.AppendLine("  arazi kaldırıldı: " + terrain.name);
            Object.DestroyImmediate(terrain.gameObject);
        }
    }

    private static string Name(Object o) => o != null ? o.name : "yok";

    private static bool IsPureScenery(GameObject root)
    {
        if (root.GetComponentInChildren<global::LowPolyWater.LowPolyWater>(true) != null) return true;
        if (root.GetComponentsInChildren<MonoBehaviour>(true).Length > 0) return false;
        if (root.GetComponentInChildren<Camera>(true) != null || root.GetComponentInChildren<Light>(true) != null) return false;
        if (root.GetComponentInChildren<Terrain>(true) != null) return true;
        var source = PrefabUtility.GetCorrespondingObjectFromSource(root);
        if (source != null && AssetDatabase.GetAssetPath(source).StartsWith("Assets/Resources/")) return false;
        return root.GetComponentInChildren<Renderer>(true) != null;
    }

    // ---------------------------------------------------------------- materials & primitives

    private static Material Mat(string name, Color color, float smooth = 0.2f, float metal = 0f, bool transparent = false, Color? emission = null)
    {
        if (MaterialCache.TryGetValue(name, out var cached) && cached != null) return cached;
        string path = Folder + "Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color); m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
        if (transparent)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; m.SetOverrideTag("RenderType", "Transparent");
        }
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        EditorUtility.SetDirty(m);
        MaterialCache[name] = m;
        return m;
    }

    /// <summary>Vertex-coloured material for ground and road meshes.</summary>
    private static Material VertexMat(string name, bool flat, float smoothness)
    {
        if (MaterialCache.TryGetValue(name, out var cached) && cached != null) return cached;
        string path = Folder + "Materials/" + name + ".mat";
        var shader = Shader.Find("EcoQuest/LowPoly Vertex Lit");
        if (shader == null) throw new System.Exception("EcoQuest/LowPoly Vertex Lit shader bulunamadı (Assets/Shaders/EcoLowPoly.shader).");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetFloat("_FlatShading", flat ? 1f : 0f); m.SetFloat("_Smoothness", smoothness); m.SetFloat("_ShadeContrast", 0.32f);
        m.SetFloat("_PollutionStrength", 0f);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        MaterialCache[name] = m;
        return m;
    }

    private static Material SmokeMaterial()
    {
        string path = Folder + "Materials/EcoWorldSmoke.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/EcoEnvironment/SoftParticle.asset");
        if (tex != null) m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f); m.SetFloat("_ZWrite", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material material, bool collider = false)
    {
        var go = GameObject.CreatePrimitive(type); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = localPos; go.transform.localScale = scale;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static Transform Group(string name, Transform parent)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); return t;
    }

    private static Text WorldLabel(Transform parent, string content, Vector3 localPosition, float scale = 0.006f, int fontSize = 36)
    {
        var go = new GameObject("Eco sign", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; go.transform.localScale = Vector3.one * scale;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text)); caption.transform.SetParent(go.transform, false);
        caption.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 160);
        var text = caption.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1f, 0.92f, 0.65f); text.text = content; text.raycastTarget = false;
        return text;
    }

    private static ParticleSystem MakeSmoke(Transform parent, Vector3 localPos, float scale, bool snow = false)
    {
        var go = new GameObject(snow ? "Snowfall" : "Smoke"); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission; var shape = ps.shape;
        if (snow)
        {
            main.startLifetime = 14f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
            main.maxParticles = 700; main.gravityModifier = 0.05f; main.startColor = new Color(1f, 1f, 1f, 0.85f);
            emission.rateOverTime = 50f;
            shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(Size * 0.95f, 2f, 40f * L);
        }
        else
        {
            main.startLifetime = 6f; main.startSpeed = 2.2f; main.startSize = new ParticleSystem.MinMaxCurve(2.2f * scale, 3.4f * scale);
            main.maxParticles = 140; main.startColor = new Color(0.12f, 0.1f, 0.08f, 0.8f);
            emission.rateOverTime = 9f;
            shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.8f; shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.8f));
        }
        var colors = ps.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        colors.color = gradient;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMaterial();
        return ps;
    }

    // ---------------------------------------------------------------- heights & road field

    private static void ComputeHeights()
    {
        N = Mathf.RoundToInt((Size + 2f * Border) / Cell);
        Origin = -Border;
        stationPadHeight = Ground0 + GlacierRise(StationPad.y) * 5.5f;
        heights = new float[N + 1, N + 1];
        for (int i = 0; i <= N; i++)
            for (int j = 0; j <= N; j++)
                heights[i, j] = RawHeight(VX(i), VX(j));

        // Roads follow a blurred copy of the land so crossing roads share one height and slopes stay gentle.
        roadField = (float[,])heights.Clone();
        for (int pass = 0; pass < 4; pass++) roadField = Blur(roadField, 3);

        foreach (var road in Roads) SampleRoad(road);
        MarkJunctions();

        // Cut roads and stream beds into the ground.
        roadDistance = new float[N + 1, N + 1];
        for (int i = 0; i <= N; i++)
            for (int j = 0; j <= N; j++)
            {
                float x = VX(i), z = VX(j);
                var p = new Vector2(x, z);
                float bestPaved = float.MaxValue, bestTrail = float.MaxValue, bestWater = float.MaxValue, waterWidth = 1f;
                foreach (var road in Roads)
                {
                    if (Distance(road.bounds, p) > 8f) continue;
                    float d = float.MaxValue;
                    for (int k = 0; k + 1 < road.pos.Count; k++) d = Mathf.Min(d, DistToSegment(p, road.pos[k], road.pos[k + 1]));
                    float edge = d - road.Width * 0.5f;
                    if (road.Water) { if (edge < bestWater) { bestWater = edge; waterWidth = road.Width; } }
                    else if (road.kind == RoadKind.Trail) bestTrail = Mathf.Min(bestTrail, edge);
                    else bestPaved = Mathf.Min(bestPaved, edge - (road.Urban ? Pave + 0.1f : 0.4f));
                }
                float field = roadField[i, j];
                float h = heights[i, j];
                if (bestPaved < 5f) h = Mathf.Lerp(h, field - 0.02f, 1f - Smooth(0.2f, 4.5f, bestPaved));
                if (bestTrail < 3f) h = Mathf.Lerp(h, field - 0.02f, 1f - Smooth(0.1f, 3f, bestTrail));
                if (bestWater < 3.5f)
                {
                    float bed = field - 1.0f;
                    h = Mathf.Lerp(h, Mathf.Min(h, bed), 1f - Smooth(-waterWidth * 0.2f, 3.5f, bestWater));
                }
                heights[i, j] = h;
                roadDistance[i, j] = Mathf.Min(bestPaved, bestTrail + 0.4f);
            }
        report.AppendLine("Zemin ızgarası: " + (N + 1) + "×" + (N + 1) + " köşe, hücre " + Cell + " m, oynanabilir alan " + Size + "×" + Size + " m");
    }

    private static float[,] Blur(float[,] src, int radius)
    {
        int n = src.GetLength(0);
        var tmp = new float[n, n]; var dst = new float[n, n];
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                float s = 0f; int c = 0;
                for (int k = -radius; k <= radius; k++) { int ii = Mathf.Clamp(i + k, 0, n - 1); s += src[ii, j]; c++; }
                tmp[i, j] = s / c;
            }
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float s = 0f; int c = 0;
                for (int k = -radius; k <= radius; k++) { int jj = Mathf.Clamp(j + k, 0, n - 1); s += tmp[i, jj]; c++; }
                dst[i, j] = s / c;
            }
        return dst;
    }

    private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static void SampleRoad(RoadDef road)
    {
        road.pos.Clear(); road.tan.Clear(); road.dist.Clear();
        var pts = road.points; int count = pts.Length;
        int segments = road.closed ? count : count - 1;
        Vector2 Pt(int k) => road.closed ? pts[(k % count + count) % count] : pts[Mathf.Clamp(k, 0, count - 1)];
        const float step = 0.7f;
        for (int s = 0; s < segments; s++)
        {
            Vector2 a = Pt(s - 1), b = Pt(s), c = Pt(s + 1), d = Pt(s + 2);
            if (!road.closed && s == 0) a = b - (c - b);
            if (!road.closed && s == segments - 1) d = c + (c - b);
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(b, c) / step));
            for (int k = 0; k < steps; k++) road.pos.Add(CatmullRom(a, b, c, d, k / (float)steps));
        }
        if (!road.closed) road.pos.Add(pts[count - 1]);
        else road.pos.Add(road.pos[0]);
        float total = 0f;
        for (int i = 0; i < road.pos.Count; i++)
        {
            if (i > 0) total += Vector2.Distance(road.pos[i - 1], road.pos[i]);
            road.dist.Add(total);
            Vector2 prev = road.pos[Mathf.Max(0, i - 1)], next = road.pos[Mathf.Min(road.pos.Count - 1, i + 1)];
            if (road.closed && i == 0) prev = road.pos[road.pos.Count - 2];
            if (road.closed && i == road.pos.Count - 1) next = road.pos[1];
            road.tan.Add((next - prev).normalized);
        }
        float minX = road.pos.Min(p => p.x), maxX = road.pos.Max(p => p.x), minZ = road.pos.Min(p => p.y), maxZ = road.pos.Max(p => p.y);
        float pad = road.Width * 0.5f + 3f;
        road.bounds = Rect.MinMaxRect(minX - pad, minZ - pad, maxX + pad, maxZ + pad);
    }

    // A sample is in a junction when it lies on another paved road; markings, kerbs and pavements stop there.
    private static void MarkJunctions()
    {
        foreach (var road in Roads)
        {
            road.junction = new bool[road.pos.Count];
            road.nearJunction = new bool[road.pos.Count];
            if (road.Water) continue;
            for (int i = 0; i < road.pos.Count; i++)
            {
                var p = road.pos[i];
                foreach (var other in Roads)
                {
                    if (other == road || other.Water || !other.Paved && !road.Paved) continue;
                    if (Distance(other.bounds, p) > 6f) continue;
                    float d = float.MaxValue;
                    for (int k = 0; k + 1 < other.pos.Count; k++) d = Mathf.Min(d, DistToSegment(p, other.pos[k], other.pos[k + 1]));
                    float half = other.Width * 0.5f;
                    if (d < half + 0.6f) road.junction[i] = true;
                    // Pavements stop where the crossing street's pavement begins (a little beyond its kerb).
                    if (d < half + (other.Urban ? Pave : 0.7f) + (road.Urban ? Pave : 0.4f)) road.nearJunction[i] = true;
                }
            }
        }
    }

    // ---------------------------------------------------------------- ground mesh

    private static readonly Color GrassA = new Color(0.36f, 0.58f, 0.25f), GrassB = new Color(0.46f, 0.66f, 0.29f);
    private static readonly Color GrassDark = new Color(0.25f, 0.44f, 0.21f), DryGrass = new Color(0.72f, 0.66f, 0.37f);
    private static readonly Color Dirt = new Color(0.52f, 0.40f, 0.27f), Sand = new Color(0.86f, 0.78f, 0.56f);
    private static readonly Color Rock = new Color(0.52f, 0.52f, 0.54f), Snow = new Color(0.93f, 0.96f, 1.0f), Ice = new Color(0.76f, 0.89f, 0.97f);
    private static readonly Color IndustrialGround = new Color(0.48f, 0.44f, 0.37f), Lawn = new Color(0.42f, 0.65f, 0.30f), Mud = new Color(0.40f, 0.36f, 0.24f);

    private static Color GroundColor(int i, int j)
    {
        float x = VX(i), z = VX(j), h = heights[i, j];
        float n1 = Fbm(x * 0.08f, z * 0.08f, 3), n2 = Fbm(x * 0.21f + 4f, z * 0.21f + 9f, 2);
        Color c = Color.Lerp(GrassA, GrassB, Smooth(0.35f, 0.7f, n1));
        c = Color.Lerp(c, GrassDark, Smooth(0.55f, 0.8f, n2) * 0.4f);

        int ri = RegionIndexAt(x, z);
        var kind = ri >= 0 ? Defs[ri].kind : (EcoRegionInfo.Kind)(-1);
        if (kind == EcoRegionInfo.Kind.Sun) c = Color.Lerp(c, DryGrass, 0.35f + 0.4f * Smooth(0.4f, 0.75f, n1));
        if (kind == EcoRegionInfo.Kind.City) c = Color.Lerp(c, Lawn, 0.6f);
        if (kind == EcoRegionInfo.Kind.Grove) c = Color.Lerp(c, Color.Lerp(GrassDark, Dirt, 0.35f), 0.55f);
        if (kind == EcoRegionInfo.Kind.Industry)
        {
            float industry = RectMask(x, z, 128f * L, 80f * L, 21f * L, 25f * L, 6f);
            c = Color.Lerp(c, Color.Lerp(IndustrialGround, Dirt, n2), industry * 0.85f);
        }
        // The east lake shore is muddy, the west one sandy.
        float da = Vector2.Distance(new Vector2(x, z), LakeA), db = Vector2.Distance(new Vector2(x, z), LakeB);
        c = Color.Lerp(c, Sand, (1f - Smooth(LakeAR * 1.0f, LakeAR * 1.35f, da)) * 0.9f);
        c = Color.Lerp(c, Mud, (1f - Smooth(LakeBR * 1.0f, LakeBR * 1.45f, db)) * 0.9f);
        if (h < WaterY - 0.2f) c = Color.Lerp(c, new Color(0.30f, 0.33f, 0.26f), 0.7f);

        // Glacier: snow fields, rocky outcrops on slopes, blue ice in hollows.
        float glacier = GlacierRise(z + (n1 - 0.5f) * 8f);
        float steep = SlopeAt(i, j);
        c = Color.Lerp(c, Color.Lerp(Snow, Ice, Smooth(0.5f, 0.8f, n2) * 0.6f), Smooth(0.35f, 0.75f, glacier));
        float outside = Outside(x, z);
        c = Color.Lerp(c, GrassDark, Smooth(0f, 6f, outside) * 0.7f);
        c = Color.Lerp(c, Rock, Smooth(24f, 40f, steep) * 0.85f);
        c = Color.Lerp(c, Snow, Smooth(38f, 52f, h) * 0.9f);

        // Verges: worn grass and gravel along roads.
        float rd = roadDistance[i, j];
        c = Color.Lerp(c, Color.Lerp(c, Dirt, 0.45f), 1f - Smooth(-0.5f, 2.5f, rd));
        // Hand-painted variation: a tiny per-vertex jitter breaks up flat colour without looking noisy.
        float jitter = (Mathf.PerlinNoise(x * 0.9f, z * 0.9f) - 0.5f) * 0.06f;
        return new Color(Mathf.Clamp01(c.r + jitter), Mathf.Clamp01(c.g + jitter), Mathf.Clamp01(c.b + jitter * 0.6f), 1f);
    }

    private static float SlopeAt(int i, int j)
    {
        int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(N, i + 1), j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(N, j + 1);
        float dx = (heights[i1, j] - heights[i0, j]) / ((i1 - i0) * Cell), dz = (heights[i, j1] - heights[i, j0]) / ((j1 - j0) * Cell);
        return Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
    }

    private static Vector3 NormalAt(int i, int j)
    {
        int i0 = Mathf.Max(0, i - 1), i1 = Mathf.Min(N, i + 1), j0 = Mathf.Max(0, j - 1), j1 = Mathf.Min(N, j + 1);
        float dx = (heights[i1, j] - heights[i0, j]) / ((i1 - i0) * Cell), dz = (heights[i, j1] - heights[i, j0]) / ((j1 - j0) * Cell);
        return new Vector3(-dx, 1f, -dz).normalized;
    }

    private static void BuildGround()
    {
        var material = VertexMat("EcoGround", true, 0.04f);
        material.SetFloat("_PollutionStrength", 1f); // polluted regions fade the ground (EcoPollutionField)
        var field = Groups["Ground"].gameObject.AddComponent<EcoPollutionField>();
        field.area = new Rect(0f, 0f, Size, Size);
        var colors = new Color[N + 1, N + 1];
        for (int i = 0; i <= N; i++) for (int j = 0; j <= N; j++) colors[i, j] = GroundColor(i, j);
        int chunks = Mathf.CeilToInt(N / (float)ChunkCells);
        for (int cx = 0; cx < chunks; cx++)
            for (int cz = 0; cz < chunks; cz++)
            {
                int i0 = cx * ChunkCells, j0 = cz * ChunkCells, i1 = Mathf.Min(N, i0 + ChunkCells), j1 = Mathf.Min(N, j0 + ChunkCells);
                int w = i1 - i0 + 1, d = j1 - j0 + 1;
                var vertices = new Vector3[w * d]; var normals = new Vector3[w * d]; var vcolors = new Color[w * d];
                for (int a = 0; a < w; a++)
                    for (int b = 0; b < d; b++)
                    {
                        int i = i0 + a, j = j0 + b, k = a * d + b;
                        vertices[k] = new Vector3(VX(i), heights[i, j], VX(j));
                        normals[k] = NormalAt(i, j); vcolors[k] = colors[i, j];
                    }
                var triangles = new int[(w - 1) * (d - 1) * 6]; int t = 0;
                for (int a = 0; a < w - 1; a++)
                    for (int b = 0; b < d - 1; b++)
                    {
                        int v00 = a * d + b, v10 = (a + 1) * d + b, v01 = a * d + b + 1, v11 = (a + 1) * d + b + 1;
                        triangles[t++] = v00; triangles[t++] = v01; triangles[t++] = v11;
                        triangles[t++] = v00; triangles[t++] = v11; triangles[t++] = v10;
                    }
                var mesh = new Mesh { name = "EcoGround_" + cx + "_" + cz };
                mesh.vertices = vertices; mesh.normals = normals; mesh.colors = vcolors; mesh.triangles = triangles;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, Folder + "Meshes/" + mesh.name + ".asset");
                var go = new GameObject("Ground " + cx + "," + cz, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                go.transform.SetParent(Groups["Ground"], false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                go.GetComponent<MeshCollider>().sharedMesh = mesh;
                go.isStatic = true;
                var ground = go.AddComponent<EcoGround>(); ground.surface = EcoGround.Surface.Soil; ground.mapRect = new Rect(0f, 0f, Size, Size);
            }
        report.AppendLine("Zemin: Unity Terrain yerine " + (chunks * chunks) + " parça alçak poligon mesh (köşe renkli, MeshCollider)");
    }

    // ---------------------------------------------------------------- roads

    private sealed class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Color> c = new List<Color>();
        public readonly List<int> t = new List<int>();
        // a-b is one long edge, c-d the opposite one (b and d one step further along). The winding is chosen so the
        // face looks towards `facing` (up for road surfaces, sideways for kerb faces).
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color ca, Color cb, Color ccol, Color cd, Vector3 facing)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(cc); v.Add(d); c.Add(ca); c.Add(cb); c.Add(ccol); c.Add(cd);
            Vector3 normal = Vector3.Cross(b - a, d - a);
            if (normal.sqrMagnitude < 1e-10f) normal = Vector3.Cross(d - a, cc - a);
            if (Vector3.Dot(normal, facing) >= 0f) { t.Add(i); t.Add(i + 1); t.Add(i + 3); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 3); t.Add(i + 1); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color ca, Color cb, Color ccol, Color cd) => Quad(a, b, cc, d, ca, cb, ccol, cd, Vector3.up);
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col) => Quad(a, b, cc, d, col, col, col, col, Vector3.up);
        public void Quad(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, Color col, Vector3 facing) => Quad(a, b, cc, d, col, col, col, col, facing);
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }

    private static Vector3 RoadPoint(Vector2 p, float lift) => new Vector3(p.x, SampleGrid(roadField, p.x, p.y) - 0.02f + lift, p.y);

    private static readonly Color Asphalt = new Color(0.25f, 0.26f, 0.28f), AsphaltLight = new Color(0.31f, 0.32f, 0.33f);
    private static readonly Color Paint = new Color(0.93f, 0.92f, 0.86f), PaintYellow = new Color(0.95f, 0.78f, 0.28f);
    private static readonly Color Pavement = new Color(0.70f, 0.69f, 0.65f), Kerb = new Color(0.56f, 0.56f, 0.55f);
    private static readonly Color Gravel = new Color(0.55f, 0.52f, 0.46f), TrailDirt = new Color(0.58f, 0.46f, 0.31f), TrailDark = new Color(0.47f, 0.37f, 0.25f);

    private static void BuildRoads()
    {
        var roadMat = VertexMat("EcoRoad", false, 0.18f);
        var waterClean = Mat("StreamClean", new Color(0.30f, 0.66f, 0.80f, 0.78f), 0.9f, 0f, true);
        var waterDirty = Mat("StreamDirty", new Color(0.38f, 0.40f, 0.20f, 0.88f), 0.6f, 0f, true);
        int paved = 0, trails = 0;
        foreach (var road in Roads)
        {
            var group = Group(road.name, Groups["Roads"]);
            if (road.Water) { BuildStream(road, group, road.kind == RoadKind.Stream ? waterClean : waterDirty); continue; }
            var surface = new MeshBuilder(); var details = new MeshBuilder();
            float hw = road.Width * 0.5f;
            int count = road.pos.Count;
            for (int i = 0; i + 1 < count; i++)
            {
                Vector2 p0 = road.pos[i], p1 = road.pos[i + 1];
                Vector2 n0 = new Vector2(-road.tan[i].y, road.tan[i].x), n1 = new Vector2(-road.tan[i + 1].y, road.tan[i + 1].x);
                if (road.kind == RoadKind.Trail)
                {
                    // Dirt track: irregular edges, two worn wheel ruts, colour noise.
                    float e0 = (Mathf.PerlinNoise(road.dist[i] * 0.35f, 3.1f) - 0.5f) * 0.7f, e1 = (Mathf.PerlinNoise(road.dist[i + 1] * 0.35f, 3.1f) - 0.5f) * 0.7f;
                    float f0 = (Mathf.PerlinNoise(road.dist[i] * 0.35f, 8.7f) - 0.5f) * 0.7f, f1 = (Mathf.PerlinNoise(road.dist[i + 1] * 0.35f, 8.7f) - 0.5f) * 0.7f;
                    Color a = Color.Lerp(TrailDirt, TrailDark, Mathf.PerlinNoise(road.dist[i] * 0.2f, 1f)), b = Color.Lerp(TrailDirt, TrailDark, Mathf.PerlinNoise(road.dist[i + 1] * 0.2f, 1f));
                    surface.Quad(RoadPoint(p0 + n0 * (hw + e0), 0.03f), RoadPoint(p1 + n1 * (hw + e1), 0.03f),
                        RoadPoint(p0 - n0 * (hw + f0), 0.03f), RoadPoint(p1 - n1 * (hw + f1), 0.03f), a, b, a, b);
                    foreach (float s in new[] { -0.45f, 0.45f })
                        details.Quad(RoadPoint(p0 + n0 * (s * hw + 0.16f), 0.045f), RoadPoint(p1 + n1 * (s * hw + 0.16f), 0.045f),
                            RoadPoint(p0 + n0 * (s * hw - 0.16f), 0.045f), RoadPoint(p1 + n1 * (s * hw - 0.16f), 0.045f), TrailDark);
                    continue;
                }
                // Gravel shoulder under the asphalt, then the asphalt itself.
                float shoulder = road.Urban ? 0f : 0.7f;
                if (shoulder > 0f)
                    surface.Quad(RoadPoint(p0 + n0 * (hw + shoulder), 0.025f), RoadPoint(p1 + n1 * (hw + shoulder), 0.025f),
                        RoadPoint(p0 - n0 * (hw + shoulder), 0.025f), RoadPoint(p1 - n1 * (hw + shoulder), 0.025f), Gravel);
                float tone0 = Mathf.PerlinNoise(p0.x * 0.15f, p0.y * 0.15f), tone1 = Mathf.PerlinNoise(p1.x * 0.15f, p1.y * 0.15f);
                Color c0 = Color.Lerp(Asphalt, AsphaltLight, tone0), c1 = Color.Lerp(Asphalt, AsphaltLight, tone1);
                if (road.kind == RoadKind.Industrial) { c0 = Color.Lerp(c0, Dirt, 0.18f); c1 = Color.Lerp(c1, Dirt, 0.18f); }
                surface.Quad(RoadPoint(p0 + n0 * hw, 0.05f), RoadPoint(p1 + n1 * hw, 0.05f), RoadPoint(p0 - n0 * hw, 0.05f), RoadPoint(p1 - n1 * hw, 0.05f), c0, c1, c0, c1);

                bool skip = road.nearJunction[i] || road.nearJunction[i + 1];
                bool inside = road.junction[i] || road.junction[i + 1];
                // Centre line: dashed on streets, double yellow on the avenue, none on the ring (one-way).
                if (!skip && road.kind != RoadKind.Ring)
                {
                    float mid = (road.dist[i] + road.dist[i + 1]) * 0.5f;
                    if (road.kind == RoadKind.Avenue)
                        foreach (float s in new[] { -0.14f, 0.14f })
                            details.Quad(RoadPoint(p0 + n0 * (s + 0.06f), 0.065f), RoadPoint(p1 + n1 * (s + 0.06f), 0.065f),
                                RoadPoint(p0 + n0 * (s - 0.06f), 0.065f), RoadPoint(p1 + n1 * (s - 0.06f), 0.065f), PaintYellow);
                    else if (Mathf.Repeat(mid, 5f) < 2.6f)
                        details.Quad(RoadPoint(p0 + n0 * 0.07f, 0.065f), RoadPoint(p1 + n1 * 0.07f, 0.065f),
                            RoadPoint(p0 - n0 * 0.07f, 0.065f), RoadPoint(p1 - n1 * 0.07f, 0.065f), Paint);
                }
                // Edge lines on country roads.
                if (!skip && road.kind == RoadKind.Country)
                    foreach (float s in new[] { -1f, 1f })
                    {
                        float o = s * (hw - 0.25f);
                        details.Quad(RoadPoint(p0 + n0 * (o + 0.06f), 0.065f), RoadPoint(p1 + n1 * (o + 0.06f), 0.065f),
                            RoadPoint(p0 + n0 * (o - 0.06f), 0.065f), RoadPoint(p1 + n1 * (o - 0.06f), 0.065f), Paint);
                    }
                // Urban streets: kerb faces and pavements, broken at junctions.
                if (road.Urban && !inside)
                {
                    bool outerOnly = road.kind == RoadKind.Ring; // the island side of the ring gets its own kerb
                    foreach (float s in new[] { -1f, 1f })
                    {
                        if (road.nearJunction[i] && road.nearJunction[i + 1]) continue;
                        if (outerOnly && s > 0f) continue;
                        Vector2 e0 = p0 + n0 * s * hw, e1 = p1 + n1 * s * hw;
                        Vector2 o0 = p0 + n0 * s * (hw + Pave), o1 = p1 + n1 * s * (hw + Pave);
                        // Vertical kerb face from the asphalt up to the pavement.
                        Vector3 k0 = RoadPoint(e0, 0.05f), k1 = RoadPoint(e1, 0.05f);
                        Vector3 t0 = k0 + Vector3.up * 0.12f, t1 = k1 + Vector3.up * 0.12f;
                        Vector3 inward = new Vector3(-n0.x * s, 0f, -n0.y * s); // the kerb face looks at the carriageway
                        details.Quad(k0, k1, t0, t1, Kerb, inward);
                        details.Quad(t0, t1, RoadPoint(o0, 0.17f), RoadPoint(o1, 0.17f), Pavement);
                    }
                }
            }
            // Zebra crossings where an urban street meets a junction.
            if (road.Urban && road.kind != RoadKind.Ring)
                for (int i = 1; i < count; i++)
                {
                    if (road.nearJunction[i] == road.nearJunction[i - 1]) continue;
                    int k = road.nearJunction[i] ? i - 1 : i; // first sample outside the junction
                    int dir = road.nearJunction[i] ? -1 : 1;
                    AddCrossing(details, road, k, dir, hw);
                }

            var mesh = surface.ToMesh(road.name.Replace(" ", "_") + "_surface");
            AssetDatabase.CreateAsset(mesh, Folder + "Meshes/Road_" + Roads.IndexOf(road) + "_surface.asset");
            var go = new GameObject("Surface", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(group, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = roadMat;
            go.GetComponent<MeshCollider>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.isStatic = true;
            go.AddComponent<EcoGround>().surface = EcoGround.Surface.Road;
            if (details.v.Count > 0)
            {
                var dm = details.ToMesh(road.name.Replace(" ", "_") + "_details");
                AssetDatabase.CreateAsset(dm, Folder + "Meshes/Road_" + Roads.IndexOf(road) + "_details.asset");
                var dgo = new GameObject("Markings", typeof(MeshFilter), typeof(MeshRenderer));
                dgo.transform.SetParent(group, false);
                dgo.GetComponent<MeshFilter>().sharedMesh = dm; dgo.GetComponent<MeshRenderer>().sharedMaterial = roadMat;
                dgo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                dgo.isStatic = true;
            }
            if (road.kind == RoadKind.Trail) trails++; else paved++;
        }
        BuildRoundaboutIsland();
        report.AppendLine("Yollar: " + paved + " asfalt yol (bordür, kaldırım, şerit, yaya geçidi), " + trails + " toprak patika, 2 dere; ızgara yok");
    }

    private static void AddCrossing(MeshBuilder mb, RoadDef road, int k, int dir, float hw)
    {
        Vector2 t = road.tan[k], n = new Vector2(-t.y, t.x);
        Vector2 center = road.pos[k] - t * dir * 0.2f;
        for (float s = -hw + 0.45f; s < hw - 0.3f; s += 0.9f)
        {
            Vector2 a = center + n * s, b = center + n * (s + 0.45f);
            Vector2 f = t * 1.4f * -dir;
            mb.Quad(RoadPoint(a, 0.066f), RoadPoint(a + f, 0.066f), RoadPoint(b, 0.066f), RoadPoint(b + f, 0.066f), Paint);
        }
    }

    private static void BuildStream(RoadDef road, Transform group, Material water)
    {
        var mb = new MeshBuilder(); float hw = road.Width * 0.5f;
        for (int i = 0; i + 1 < road.pos.Count; i++)
        {
            Vector2 p0 = road.pos[i], p1 = road.pos[i + 1];
            Vector2 n0 = new Vector2(-road.tan[i].y, road.tan[i].x), n1 = new Vector2(-road.tan[i + 1].y, road.tan[i + 1].x);
            float y0 = Mathf.Max(WaterY, SampleGrid(roadField, p0.x, p0.y) - 0.55f), y1 = Mathf.Max(WaterY, SampleGrid(roadField, p1.x, p1.y) - 0.55f);
            mb.Quad(new Vector3(p0.x + n0.x * hw, y0, p0.y + n0.y * hw), new Vector3(p1.x + n1.x * hw, y1, p1.y + n1.y * hw),
                new Vector3(p0.x - n0.x * hw, y0, p0.y - n0.y * hw), new Vector3(p1.x - n1.x * hw, y1, p1.y - n1.y * hw), Color.white);
        }
        var mesh = mb.ToMesh(road.name.Replace(" ", "_"));
        AssetDatabase.CreateAsset(mesh, Folder + "Meshes/Stream_" + Roads.IndexOf(road) + ".asset");
        var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(group, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = water;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Pebbles along the banks.
        for (int i = 4; i < road.pos.Count; i += 6)
        {
            Vector2 n = new Vector2(-road.tan[i].y, road.tan[i].x) * (hw + F(0.2f, 0.7f)) * (rng.NextDouble() < 0.5 ? -1f : 1f);
            Vector2 p = road.pos[i] + n;
            if (Outside(p.x, p.y) > 2f) continue;
            var stone = Prim(PrimitiveType.Sphere, "Pebble", group, new Vector3(p.x, GroundY(p.x, p.y) + 0.05f, p.y),
                new Vector3(F(0.3f, 0.6f), F(0.15f, 0.25f), F(0.3f, 0.6f)), Mat("StreamPebble", new Color(0.55f, 0.55f, 0.52f), 0.2f));
            stone.transform.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
        }
    }

    private static void BuildRoundaboutIsland()
    {
        var plaza = Group("Meydan", Groups["City"]);
        float y = SampleGrid(roadField, Roundabout.x, Roundabout.y) - 0.02f;
        plaza.position = new Vector3(Roundabout.x, y, Roundabout.y);
        float inner = RoundaboutR - 3.2f;
        var mb = new MeshBuilder();
        const int seg = 40;
        for (int s = 0; s < seg; s++)
        {
            float a0 = s * Mathf.PI * 2f / seg, a1 = (s + 1) * Mathf.PI * 2f / seg;
            Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
            Vector3 up = Vector3.up * 0.2f;
            // Kerb face, stone rim, lawn.
            mb.Quad(d1 * inner + Vector3.up * 0.05f, d0 * inner + Vector3.up * 0.05f, d1 * inner + up, d0 * inner + up, Kerb, (d0 + d1) * 0.5f);
            mb.Quad(d1 * inner + up, d0 * inner + up, d1 * (inner - 0.9f) + up, d0 * (inner - 0.9f) + up, Pavement);
            mb.Quad(d1 * (inner - 0.9f) + up, d0 * (inner - 0.9f) + up, Vector3.up * 0.22f, Vector3.up * 0.22f, Lawn, Lawn, GrassB, GrassB);
        }
        var mesh = mb.ToMesh("Roundabout_island");
        AssetDatabase.CreateAsset(mesh, Folder + "Meshes/Roundabout_island.asset");
        var island = new GameObject("Island", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        island.transform.SetParent(plaza, false);
        island.GetComponent<MeshFilter>().sharedMesh = mesh; island.GetComponent<MeshRenderer>().sharedMaterial = VertexMat("EcoRoad", false, 0.18f);
        island.GetComponent<MeshCollider>().sharedMesh = mesh;
        island.AddComponent<EcoGround>().surface = EcoGround.Surface.Plaza;
        Prim(PrimitiveType.Cylinder, "Fountain basin", plaza, new Vector3(0f, 0.45f, 0f), new Vector3(3.4f, 0.3f, 3.4f), Mat("FountainStone", new Color(0.72f, 0.71f, 0.68f), 0.2f), true);
        Prim(PrimitiveType.Cylinder, "Fountain water", plaza, new Vector3(0f, 0.74f, 0f), new Vector3(3.0f, 0.04f, 3.0f), Mat("FountainWater", new Color(0.35f, 0.75f, 0.85f, 0.8f), 0.9f, 0f, true));
        Prim(PrimitiveType.Cylinder, "Fountain column", plaza, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.8f, 0.5f), Mat("FountainStone", new Color(0.72f, 0.71f, 0.68f), 0.2f));
        WorldLabel(plaza, "ŞEHİR MEYDANI", new Vector3(0f, 3.4f, 0f), 0.008f);
        EcoMapMarker.Add(plaza.gameObject, "region_city", "Şehir Meydanı", "Göbekli kavşak ve çeşme. Bütün bulvarlar buradan dağılır.", EcoMapMarker.Category.Service);
        KeepOut.Add(new Rect(Roundabout.x - RoundaboutR - 5f, Roundabout.y - RoundaboutR - 5f, (RoundaboutR + 5f) * 2f, (RoundaboutR + 5f) * 2f));
    }

    private static void BuildBounds()
    {
        var root = Groups["Bounds"];
        const float h = 30f, t = 2f;
        var walls = new[]
        {
            (new Vector3(Size * 0.5f, Ground0, -t * 0.5f), new Vector3(Size + 2f * t, h, t)),
            (new Vector3(Size * 0.5f, Ground0, Size + t * 0.5f), new Vector3(Size + 2f * t, h, t)),
            (new Vector3(-t * 0.5f, Ground0, Size * 0.5f), new Vector3(t, h, Size)),
            (new Vector3(Size + t * 0.5f, Ground0, Size * 0.5f), new Vector3(t, h, Size)),
        };
        int k = 0;
        foreach (var (center, size) in walls)
        {
            var wall = new GameObject("Map edge " + k++, typeof(BoxCollider)); wall.transform.SetParent(root, false);
            wall.transform.position = center; wall.GetComponent<BoxCollider>().size = size;
        }
    }

    // ---------------------------------------------------------------- regions

    private static void BuildRegions()
    {
        foreach (var def in Defs)
        {
            var go = new GameObject(def.name); go.transform.SetParent(Groups["Regions"], false);
            go.transform.position = new Vector3(def.center.x, GroundY(def.center.x, def.center.y), def.center.y);
            var region = go.AddComponent<EcoRegion>();
            region.regionName = def.name; region.size = def.size; region.priority = def.priority;
            region.airPollution = def.air; region.waterPollution = def.water; region.soilPollution = def.soil; region.carbon = def.carbon;
            region.baseTemperature = def.temperature; region.vegetation = def.vegetation; region.sensitiveHabitat = def.sensitive;
            var habitat = go.AddComponent<EcoHabitat>(); habitat.recovery = def.habitat;
            var info = go.AddComponent<EcoRegionInfo>(); info.kind = def.kind; info.tagline = def.tagline; info.mapColor = def.color;
            info.projectIndex = def.project; info.windExposure = def.wind; info.sunExposure = def.sun; info.features = def.features;
            report.AppendLine("Bölge: " + def.name + " merkez=" + def.center + " boyut=" + def.size
                + " hava=" + def.air + " su=" + def.water + " toprak=" + def.soil + " karbon=" + def.carbon);
        }
    }

    // ---------------------------------------------------------------- lakes

    private static Mesh DiscMesh(string name, float radius, int rings, int segments)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        for (int ring = 1; ring <= rings; ring++)
        {
            float rr = radius * ring / rings;
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                vertices.Add(new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * rr / (2f * radius), 0.5f + Mathf.Sin(a) * rr / (2f * radius)));
            }
        }
        var triangles = new List<int>();
        for (int s = 0; s < segments; s++)
        {
            int cur = 1 + s, next = 1 + (s + 1) % segments;
            triangles.Add(0); triangles.Add(next); triangles.Add(cur);
        }
        for (int ring = 1; ring < rings; ring++)
            for (int s = 0; s < segments; s++)
            {
                int inner = 1 + (ring - 1) * segments, outer = 1 + ring * segments;
                int i0 = inner + s, i1 = inner + (s + 1) % segments, o0 = outer + s, o1 = outer + (s + 1) % segments;
                triangles.Add(i0); triangles.Add(i1); triangles.Add(o1);
                triangles.Add(i0); triangles.Add(o1); triangles.Add(o0);
            }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, Folder + "Meshes/" + name + ".asset");
        return mesh;
    }

    private static Material WaterMaterial(string name, Color color)
    {
        string path = Folder + "Materials/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/LowPolyWater_Pack/Materials/LowPolyWaterMaterial.mat");
        Material m;
        if (source != null) m = new Material(source);
        else m = Mat(name + "_fallback", color, 0.9f, 0f, true);
        if (source != null)
        {
            m.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    private static void BuildLakes()
    {
        BuildLake("Batı Gölü", LakeA, LakeAR, false, new Vector2(1f, 0.25f));
        BuildLake("Doğu Gölü", LakeB, LakeBR, true, new Vector2(-1f, 0.25f));
    }

    private static void BuildLake(string name, Vector2 center, float radius, bool polluted, Vector2 dockDirection)
    {
        var root = Group(name, Groups["Lakes"]);
        root.position = new Vector3(center.x, WaterY, center.y);

        var surface = new GameObject("Water surface"); surface.transform.SetParent(root, false);
        var filter = surface.AddComponent<MeshFilter>(); filter.sharedMesh = DiscMesh(name.Replace(" ", "") + "_Disc", radius * 1.18f, 12, 44);
        var renderer = surface.AddComponent<MeshRenderer>();
        var clean = new Color(0.30f, 0.74f, 0.82f, 0.62f); var dirty = new Color(0.36f, 0.40f, 0.17f, 0.82f);
        renderer.sharedMaterial = WaterMaterial(polluted ? "EcoLakeDirty" : "EcoLakeClean", polluted ? dirty : clean);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var wave = surface.AddComponent<global::LowPolyWater.LowPolyWater>();
        wave.waveHeight = 0.07f; wave.waveFrequency = 0.25f; wave.waveLength = 4f;
        var tint = surface.AddComponent<EcoLakeTint>(); tint.target = renderer; tint.clean = clean; tint.dirty = dirty;

        // Interaction volume: sits 0.7 m under the surface so the player can wade in and the vacuum / fishing rod can target it.
        var source = new GameObject("Water source"); source.transform.SetParent(root, false);
        source.transform.localPosition = new Vector3(0f, -0.75f, 0f);
        // Covers the open water up to near the shore so filling and fishing work from the bank (E reaches 3 m).
        var box = source.AddComponent<BoxCollider>(); box.size = new Vector3(radius * 1.9f, 0.1f, radius * 1.9f);
        var water = source.AddComponent<WeaponWaterSource>(); water.useRegionalQuality = true; water.cleanWater = !polluted; water.shoreRadius = radius;
        var fishing = source.AddComponent<EcoFishingSpot>(); fishing.population = polluted ? 3f : 8f;

        // Wooden dock reaching into the lake from the road side.
        Vector2 dir = dockDirection.normalized;
        float shore = radius * 0.9f;
        Vector2 start = center + dir * shore;
        float yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg; // plank length axis (local z) points into the lake
        var dock = Group("Dock", root); dock.position = new Vector3(start.x, WaterY + 0.55f, start.y);
        dock.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wood = Mat("DockWood", new Color(0.42f, 0.29f, 0.17f), 0.1f);
        Prim(PrimitiveType.Cube, "Planks", dock, new Vector3(0f, 0f, 3.4f), new Vector3(2f, 0.2f, 7f), wood, true);
        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cylinder, "Post", dock, new Vector3(-0.9f, -1.4f, 1.0f + i * 2.6f), new Vector3(0.28f, 1.6f, 0.28f), wood);
            Prim(PrimitiveType.Cylinder, "Post", dock, new Vector3(0.9f, -1.4f, 1.0f + i * 2.6f), new Vector3(0.28f, 1.6f, 0.28f), wood);
        }
        WorldLabel(dock, polluted ? "DOĞU GÖLÜ\nKirli • Arıtma gerekiyor" : "BATI GÖLÜ\nTemiz su • [5] Eller → [E] Balık tut", new Vector3(0f, 2.6f, 1f), 0.008f);
        EcoMapMarker.Add(dock.gameObject, "poi_fishing", polluted ? "Doğu iskelesi" : "Batı iskelesi",
            polluted ? "Balıklar kirli; temiz balık için önce gölü canlandır." : "Temiz balık tutulabilir. Nüfus 4'e inince av durur.", EcoMapMarker.Category.Water);
        if (polluted)
        {
            // GDD §7/§18: the lake project needs a real polluted inlet on the east shore that the player must close.
            Vector2 ip = new Vector2(135.4f * L, 31.4f * L);
            var inlet = new GameObject("Kirletici giriş"); inlet.transform.SetParent(root, true);
            inlet.transform.position = new Vector3(ip.x, Mathf.Max(GroundY(ip.x, ip.y), WaterY) + 0.05f, ip.y);
            var inletComponent = inlet.AddComponent<EcoPollutionInlet>(); inletComponent.CreateVisual();
            WorldLabel(inlet.transform, "KİRLETİCİ GİRİŞ\n[E] Sızıntıyı kapat • 10M + 5P", new Vector3(0f, 2.6f, 0f), 0.008f);
        }
        else
        {
            // GDD §9: small hydro may only stand on a natural-flow site with a fish pass (the mountain stream mouth).
            Vector2 hp = HydroPad;
            KeepOut.Add(new Rect(hp.x - 5f, hp.y - 5f, 10f, 10f));
            var site = new GameObject("Doğal akış alanı (hidro)"); site.transform.SetParent(root, true);
            site.transform.position = new Vector3(hp.x, Mathf.Max(GroundY(hp.x, hp.y), WaterY) + 0.05f, hp.y);
            var hydro = site.AddComponent<EcoHydroSite>(); hydro.radius = 8f;
            WorldLabel(site.transform, "DOĞAL AKIŞ ALANI\nKüçük hidro santrali burada kurulabilir", new Vector3(0f, 2.6f, 0f), 0.008f);
        }
        KeepOut.Add(new Rect(center.x - radius * 1.2f, center.y - radius * 1.2f, radius * 2.4f, radius * 2.4f));
        report.AppendLine("Göl: " + name + " " + (polluted ? "(kirli)" : "(temiz)") + " merkez=" + center + " yarıçap=" + radius);
    }

    // ---------------------------------------------------------------- models

    private static ModelSet BuildSet(string[] include, string[] exclude, params string[] folders)
    {
        var set = new ModelSet();
        var valid = folders.Where(AssetDatabase.IsValidFolder).ToArray();
        if (valid.Length == 0) return set;
        foreach (var guid in AssetDatabase.FindAssets("t:GameObject", valid))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (!include.Any(k => n.Contains(k)) || exclude.Any(k => n.Contains(k))) continue;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null || go.GetComponentInChildren<MeshRenderer>(true) == null) continue;
            float natural = NaturalHeight(go);
            if (natural < 0.05f) continue;
            set.models.Add(go); set.heights.Add(natural);
        }
        if (set.Any) set.average = set.heights.Average();
        return set;
    }

    private static float NaturalHeight(GameObject model)
    {
        var temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
        if (temp == null) return 0f;
        temp.transform.position = Vector3.zero; temp.transform.rotation = Quaternion.identity; temp.transform.localScale = Vector3.one;
        float h = TryBounds(temp, out var b) ? b.size.y : 0f;
        Object.DestroyImmediate(temp);
        return h;
    }

    private static bool TryBounds(GameObject go, out Bounds bounds)
    {
        bounds = new Bounds(go.transform.position, Vector3.zero); bool found = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
        }
        return found;
    }

    private static string[] KenneyFolders() => new[]
    {
        "Assets/3d_nature_nature_kit_nature_stylized_lowpoly_modular_cc0_kenney_0055",
        "Assets/3d_nature_mini_forest_kit_nature_stylized_lowpoly_modular_cc0_kenney_0031",
    };

    // Instantiates a model/prefab standing on `pos`, scaled uniformly to fit the given limits, then rotated by yaw about its own centre.
    private static GameObject SpawnModel(GameObject model, Transform parent, Vector3 pos, float yaw, float targetHeight,
        float maxWidth = 0f, float maxDepth = 0f, bool addCollider = false, bool onlyShrink = false)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        if (go == null) return null;
        go.transform.position = pos; go.transform.rotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        if (!TryBounds(go, out var b)) { Object.DestroyImmediate(go); return null; }
        float s = float.MaxValue;
        if (targetHeight > 0f && b.size.y > 0.001f) s = Mathf.Min(s, targetHeight / b.size.y);
        if (maxWidth > 0f && b.size.x > 0.001f) s = Mathf.Min(s, maxWidth / b.size.x);
        if (maxDepth > 0f && b.size.z > 0.001f) s = Mathf.Min(s, maxDepth / b.size.z);
        if (s == float.MaxValue) s = 1f;
        if (onlyShrink) s = Mathf.Min(1f, s);
        go.transform.localScale = Vector3.one * s;
        TryBounds(go, out b);
        go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        TryBounds(go, out b);
        if (addCollider && go.GetComponentInChildren<Collider>() == null)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size = new Vector3(b.size.x / s, b.size.y / s, b.size.z / s);
        }
        if (Mathf.Abs(yaw) > 0.01f) go.transform.RotateAround(b.center, Vector3.up, yaw);
        return go;
    }

    private static GameObject FindZiplerModel(params string[] keys)
    {
        foreach (string root in new[] { "Assets/Zipler ve Modeller", "Assets/Modeller" })
        {
            if (!AssetDatabase.IsValidFolder(root)) continue;
            foreach (var sub in AssetDatabase.GetSubFolders(root))
            {
                string leaf = Path.GetFileName(sub).ToLowerInvariant();
                if (!keys.Any(k => leaf.Contains(k))) continue;
                GameObject best = null; int bestScore = -1;
                foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { sub }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    int renderers = go.GetComponentsInChildren<MeshRenderer>(true).Length;
                    if (renderers == 0) continue;
                    int score = renderers + (path.EndsWith(".prefab") ? 1000 : 0);
                    if (score > bestScore) { best = go; bestScore = score; }
                }
                if (best != null) { report.AppendLine("Model [" + string.Join("/", keys) + "] = " + AssetDatabase.GetAssetPath(best)); return best; }
            }
        }
        report.AppendLine("Model bulunamadı [" + string.Join("/", keys) + "] -> yedek (basit) yapı kullanıldı");
        return null;
    }

    // ---------------------------------------------------------------- city

    private static bool Overlaps(Rect r) => KeepOut.Any(k => k.Overlaps(r));

    private static void BuildCity()
    {
        var root = Groups["City"];
        var apartment = FindZiplerModel("apartman");
        var modern = FindZiplerModel("modern");
        var traffic = FindZiplerModel("trafik");
        var bin = FindZiplerModel("cop");
        // The old map's "Lamba" group is mostly traffic lights; only a real lamp model replaces the procedural one.
        var lamp = lampPrefab != null && lampPrefab.name.ToLowerInvariant().Contains("lamp") ? lampPrefab : FindZiplerModel("lamba");
        var city = Def(EcoRegionInfo.Kind.City);
        var cityRect = new Rect(city.center - city.size * 0.5f, city.size);
        // Houses may spill a little past the district edge (village outskirts) but never onto the glacier slope.
        var houseRect = Rect.MinMaxRect(cityRect.xMin - 5f, cityRect.yMin - 4f, cityRect.xMax + 4f, cityRect.yMax - 1f);

        // Civic buildings stand on the first plot of each main road out of the roundabout, facing their street.
        var civic = new (GameObject model, string name, string road, Vector2 side, bool fromEnd, float height, string icon, string detail)[]
        {
            // The Meshy market/clinic exports read as flat slabs from the iso camera; procedural buildings keep clear silhouettes.
            (null, "Mahalle Pazarı", "Batı Bulvarı", Vector2.up, false, 6f, "poi_market", "Ürün satışı ve günlük paketler. Siparişler [O] pazarın yanında."),
            (null, "Ev", "Ana Cadde (kuzey)", Vector2.right, false, 6.5f, "poi_home", "Mahalle çocuğunun evi. Bakım anları burada başlar."),
            // The bookstore asset bakes a white ground plate into its texture; a procedural civic hall reads cleaner.
            (null, "Kütüphane", "Doğu Bulvarı", Vector2.down, false, 7f, "poi_project", "Öğrenme kartları ve proje günlüğü [J]."),
            (null, "Sağlık Ocağı", "Ana Cadde (güney)", Vector2.left, true, 8f, "poi_station", "Kirli hava çocukların sağlığını etkiler."),
        };
        foreach (var plot in civic)
        {
            var road = Roads.First(r => r.name == plot.road);
            float along = plot.fromEnd ? road.dist[road.dist.Count - 1] - 9f : 9f;
            int k = road.dist.FindIndex(d => d >= along);
            Vector2 n = new Vector2(-road.tan[k].y, road.tan[k].x);
            float side = Vector2.Dot(n, plot.side) >= 0f ? 1f : -1f;
            const float depth = 7.5f;
            Vector2 c = road.pos[k] + n * side * (road.Width * 0.5f + Pave + 1.2f + depth * 0.5f);
            Vector2 face = -n * side;
            float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;
            var pos = new Vector3(c.x, GroundY(c.x, c.y), c.y);
            GameObject built = plot.model != null ? SpawnModel(plot.model, root, pos, yaw, plot.height, 9f, depth, true) : null;
            if (built == null) built = plot.name == "Ev" ? HomeBuilding(root, pos, yaw) : plot.name == "Kütüphane" ? CivicHall(root, pos, yaw)
                : plot.name == "Mahalle Pazarı" ? MarketHall(root, pos, yaw) : plot.name == "Sağlık Ocağı" ? Clinic(root, pos, yaw) : ProceduralBuilding(root, pos, yaw, 8.5f, depth, plot.height);
            built.name = plot.name;
            if (TryBounds(built, out var top))
                WorldLabel(built.transform, plot.name.ToUpperInvariant(), built.transform.InverseTransformPoint(new Vector3(top.center.x, top.max.y + 1.2f, top.center.z)), 0.009f / Mathf.Max(0.01f, built.transform.lossyScale.x));
            EcoMapMarker.Add(built, plot.icon, plot.name, plot.detail, EcoMapMarker.Category.Service, plot.name == "Ev");
            if (plot.name == "Ev")
            {
                var door = new GameObject("Eco Home door").transform; door.SetParent(built.transform, true);
                Vector2 front = c + face * (depth * 0.5f + 1.1f); door.position = new Vector3(front.x, GroundY(front.x, front.y), front.y);
            }
            if (TryBounds(built, out var bounds)) KeepOut.Add(new Rect(bounds.min.x - 0.8f, bounds.min.z - 0.8f, bounds.size.x + 1.6f, bounds.size.z + 1.6f));
        }

        // Houses and apartments line every urban street, fronts towards the road.
        int placed = 0, rejectArea = 0, rejectRoad = 0, rejectOverlap = 0;
        foreach (var road in Roads.Where(r => r.kind == RoadKind.Avenue || r.kind == RoadKind.Street))
        {
            for (float along = 3.5f; along < road.dist[road.dist.Count - 1] - 2f; along += F(7f, 8.5f))
            {
                int k = road.dist.FindIndex(d => d >= along);
                if (k < 0 || road.junction[k]) continue;
                Vector2 p = road.pos[k], t = road.tan[k], n = new Vector2(-t.y, t.x);
                foreach (float side in new[] { -1f, 1f })
                {
                    float centreDist = Vector2.Distance(p, Roundabout);
                    bool tall = centreDist < 26f;
                    float width = tall ? F(8f, 9.5f) : F(5.5f, 7f), depth = tall ? F(7.5f, 9f) : F(5.5f, 6.5f);
                    Vector2 c = p + n * side * (road.Width * 0.5f + Pave + 1.2f + depth * 0.5f);
                    if (!houseRect.Contains(c) || Vector2.Distance(c, Roundabout) < RoundaboutR + 9f) { rejectArea++; continue; }
                    // On curves the nearest kerb is a little closer than the setback measured along the normal.
                    if (RoadEdgeDistance(c.x, c.y, true, true) < depth * 0.5f - 0.6f) { rejectRoad++; continue; }
                    float reach = Mathf.Max(width, depth) * 0.5f;
                    var footprint = new Rect(c.x - reach, c.y - reach, reach * 2f, reach * 2f);
                    if (Overlaps(footprint)) { rejectOverlap++; continue; }
                    Vector2 face = -n * side; // from the house towards the road
                    float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;
                    var pos = new Vector3(c.x, GroundY(c.x, c.y), c.y);
                    GameObject model; float height; bool shrinkOnly = false;
                    if (tall) { model = apartment; height = F(10f, 15f); }
                    else if (housePrefab != null && rng.NextDouble() < 0.5) { model = housePrefab; height = 0f; shrinkOnly = true; }
                    else { model = modern != null ? modern : housePrefab; height = F(5f, 7f); shrinkOnly = model == housePrefab; }
                    GameObject built = model != null ? SpawnModel(model, root, pos, yaw, height, width, depth, true, shrinkOnly) : null;
                    if (built == null) built = ProceduralBuilding(root, pos, yaw, width, depth, tall ? F(8f, 13f) : F(4.5f, 6.5f));
                    built.name = "Building " + placed;
                    if (TryBounds(built, out var bounds)) KeepOut.Add(new Rect(bounds.min.x - 0.8f, bounds.min.z - 0.8f, bounds.size.x + 1.6f, bounds.size.z + 1.6f));
                    else KeepOut.Add(footprint);
                    placed++;
                }
            }
        }
        report.AppendLine("Şehir: göbekli kavşak, 4 kamu yapısı, sokak kenarlarına " + placed + " bina (ızgara blok yok; elenen aday: alan "
            + rejectArea + ", yol " + rejectRoad + ", çakışma " + rejectOverlap + ")");

        // Infill: the second row of smaller houses inside each block, each facing its nearest street (as real lots do).
        int infill = 0;
        var urban = Roads.Where(r => r.Urban).ToList();
        for (float x = houseRect.xMin + 3f; x < houseRect.xMax - 3f; x += 3f)
            for (float z = houseRect.yMin + 3f; z < houseRect.yMax - 3f; z += 3f)
            {
                var c = new Vector2(x + F(-1f, 1f), z + F(-1f, 1f));
                if (Vector2.Distance(c, Roundabout) < RoundaboutR + 9f) continue;
                float w = F(5f, 6.5f), d = F(5f, 6f), reach = Mathf.Max(w, d) * 0.5f + 0.4f;
                float edge = RoadEdgeDistance(c.x, c.y, true, true);
                if (edge < reach || edge > 13f) continue;
                if (Overlaps(new Rect(c.x - reach, c.y - reach, reach * 2f, reach * 2f))) continue;
                Vector2 nearest = c; float bestD = float.MaxValue;
                foreach (var r in urban)
                    foreach (var q in r.pos) { float dd = (q - c).sqrMagnitude; if (dd < bestD) { bestD = dd; nearest = q; } }
                Vector2 face = (nearest - c).normalized;
                float yaw = Mathf.Atan2(-face.x, -face.y) * Mathf.Rad2Deg;
                yaw = Mathf.Round(yaw / 15f) * 15f; // lots are surveyed in neat angles
                var pos = new Vector3(c.x, GroundY(c.x, c.y), c.y);
                var model = housePrefab != null && rng.NextDouble() < 0.5 ? housePrefab : modern;
                GameObject built = model != null ? SpawnModel(model, root, pos, yaw, F(4.5f, 6f), w, d, true, model == housePrefab) : null;
                if (built == null) built = ProceduralBuilding(root, pos, yaw, w, d, F(4f, 6f));
                built.name = "House " + infill;
                if (TryBounds(built, out var bounds)) KeepOut.Add(new Rect(bounds.min.x - 1f, bounds.min.z - 1f, bounds.size.x + 2f, bounds.size.z + 2f));
                else KeepOut.Add(new Rect(c.x - reach, c.y - reach, reach * 2f, reach * 2f));
                infill++;
            }
        report.AppendLine("Şehir: " + infill + " iç parsel evi (en yakın sokağa bakar)");

        // Street lamps on the pavement, staggered, never inside a junction.
        int lamps = 0;
        foreach (var road in Roads.Where(r => r.Urban))
        {
            int side = 1;
            for (float along = 5f; along < road.dist[road.dist.Count - 1] - 2f; along += 13f)
            {
                int k = road.dist.FindIndex(d => d >= along);
                if (k < 0 || road.nearJunction[k]) continue;
                Vector2 n = new Vector2(-road.tan[k].y, road.tan[k].x);
                if (road.kind == RoadKind.Ring) side = -1;
                Vector2 p = road.pos[k] + n * side * (road.Width * 0.5f + 0.7f);
                if (!cityRect.Contains(p)) continue;
                PlaceLamp(root, lamp, p, Mathf.Atan2(-n.x * side, -n.y * side) * Mathf.Rad2Deg); lamps++;
                if (road.kind != RoadKind.Ring) side = -side;
            }
        }
        report.AppendLine("Şehir: " + lamps + " sokak lambası");

        // Street trees on the outer edge of the avenue and boulevard pavements, between the lamps.
        var streetTrees = BuildSet(new[] { "tree" }, new[] { "palm", "pine", "plateau", "stump", "log", "cone", "tent", "house", "fall", "dead", "tall" }, KenneyFolders());
        int planted = 0;
        if (streetTrees.Any)
            foreach (var road in Roads.Where(r => r.kind == RoadKind.Avenue || r.name.Contains("Bulvar")))
                for (float along = 11.5f; along < road.dist[road.dist.Count - 1] - 3f; along += 13f)
                {
                    int k = road.dist.FindIndex(d => d >= along);
                    if (k < 0 || road.nearJunction[k]) continue;
                    Vector2 n = new Vector2(-road.tan[k].y, road.tan[k].x);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector2 p = road.pos[k] + n * side * (road.Width * 0.5f + 1.45f);
                        if (!houseRect.Contains(p) || Overlaps(new Rect(p.x - 0.6f, p.y - 0.6f, 1.2f, 1.2f))) continue;
                        int idx = rng.Next(streetTrees.models.Count);
                        var holder = new GameObject("Street tree"); holder.transform.SetParent(root, false);
                        holder.transform.position = new Vector3(p.x, SampleGrid(roadField, p.x, p.y) + 0.15f, p.y);
                        if (SpawnModel(streetTrees.models[idx], holder.transform, holder.transform.position, F(0f, 360f), F(3.6f, 4.6f), 2.6f, 2.6f) == null)
                        { Object.DestroyImmediate(holder); continue; }
                        var trunk = holder.AddComponent<CapsuleCollider>(); trunk.radius = 0.25f; trunk.height = 2.5f; trunk.center = Vector3.up * 1.25f;
                        planted++;
                    }
                }
        report.AppendLine("Şehir: " + planted + " cadde ağacı");
        if (traffic != null)
            foreach (var o in new[] { new Vector2(-9f, -12.5f), new Vector2(12.5f, -9f), new Vector2(9f, 12.5f), new Vector2(-12.5f, 9f) })
            {
                var p = Roundabout + o;
                SpawnModel(traffic, root, new Vector3(p.x, GroundY(p.x, p.y) + 0.17f, p.y), 0f, 3.6f, 0f, 0f, true);
            }
        if (bin != null)
            foreach (var o in new[] { new Vector2(-14f, -5f), new Vector2(14f, 5f), new Vector2(5f, 14f) })
            {
                var p = Roundabout + o;
                if (RoadEdgeDistance(p.x, p.y) < -1.8f) continue;
                SpawnModel(bin, root, new Vector3(p.x, GroundY(p.x, p.y) + 0.17f, p.y), F(0f, 360f), 0.9f, 0f, 0f, true);
            }
        // Small pocket parks fill the gaps between the crescents and the boulevards.
        var benchMat = Mat("BenchWood", new Color(0.55f, 0.36f, 0.2f), 0.1f);
        int benches = 0;
        for (int i = 0; i < 160 && benches < 10; i++)
        {
            Vector2 c = new Vector2(F(cityRect.xMin + 4f, cityRect.xMax - 4f), F(cityRect.yMin + 4f, cityRect.yMax - 4f));
            if (RoadEdgeDistance(c.x, c.y) < 0.6f || Overlaps(new Rect(c.x - 1.5f, c.y - 1.5f, 3f, 3f))) continue;
            var bench = Group("Bench", root); bench.position = new Vector3(c.x, GroundY(c.x, c.y), c.y);
            bench.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
            Prim(PrimitiveType.Cube, "Seat", bench, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.45f), benchMat, true);
            Prim(PrimitiveType.Cube, "Back", bench, new Vector3(0f, 0.75f, 0.2f), new Vector3(1.6f, 0.4f, 0.06f), benchMat);
            Prim(PrimitiveType.Cube, "Leg", bench, new Vector3(-0.65f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.4f), Mat("LampMetal", new Color(0.18f, 0.2f, 0.22f), 0.4f, 0.6f));
            Prim(PrimitiveType.Cube, "Leg", bench, new Vector3(0.65f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.4f), Mat("LampMetal", new Color(0.18f, 0.2f, 0.22f), 0.4f, 0.6f));
            KeepOut.Add(new Rect(c.x - 1.2f, c.y - 1.2f, 2.4f, 2.4f));
            benches++;
        }
    }

    // The child's home: a warm-coloured two-storey house with a garden fence (kept procedural so it is recognisable).
    private static GameObject HomeBuilding(Transform parent, Vector3 pos, float yaw)
    {
        var root = Group("Ev", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wall = Mat("HomeWall", new Color(0.95f, 0.86f, 0.68f), 0.1f);
        var roof = Mat("HomeRoof", new Color(0.78f, 0.33f, 0.25f), 0.15f);
        var trim = Mat("HomeTrim", new Color(0.98f, 0.97f, 0.94f), 0.2f);
        Prim(PrimitiveType.Cube, "Body", root, new Vector3(0f, 2.6f, 0f), new Vector3(8f, 5.2f, 7f), wall, true);
        var r1 = Prim(PrimitiveType.Cube, "Roof L", root, new Vector3(0f, 6.1f, -1.9f), new Vector3(8.8f, 0.3f, 4.6f), roof);
        r1.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f);
        var r2 = Prim(PrimitiveType.Cube, "Roof R", root, new Vector3(0f, 6.1f, 1.9f), new Vector3(8.8f, 0.3f, 4.6f), roof);
        r2.transform.localRotation = Quaternion.Euler(32f, 0f, 0f);
        Prim(PrimitiveType.Cube, "Door", root, new Vector3(0f, 1.15f, -3.53f), new Vector3(1.3f, 2.3f, 0.1f), Mat("HomeDoor", new Color(0.30f, 0.45f, 0.36f), 0.2f));
        var glow = Mat("HomeWindow", new Color(1f, 0.92f, 0.7f), 0.6f, 0f, false, new Color(1f, 0.8f, 0.5f) * 0.6f);
        foreach (float x in new[] { -2.6f, 2.6f })
        {
            Prim(PrimitiveType.Cube, "Window", root, new Vector3(x, 1.6f, -3.53f), new Vector3(1.4f, 1.2f, 0.1f), glow);
            Prim(PrimitiveType.Cube, "Window up", root, new Vector3(x, 4.0f, -3.53f), new Vector3(1.4f, 1.1f, 0.1f), glow);
            Prim(PrimitiveType.Cube, "Sill", root, new Vector3(x, 0.95f, -3.6f), new Vector3(1.6f, 0.12f, 0.2f), trim);
            Prim(PrimitiveType.Cube, "Flower box", root, new Vector3(x, 3.35f, -3.68f), new Vector3(1.5f, 0.3f, 0.3f), Mat("FlowerBox", new Color(0.86f, 0.38f, 0.48f), 0.2f));
        }
        Prim(PrimitiveType.Cube, "Chimney", root, new Vector3(2.4f, 7f, 1.2f), new Vector3(0.8f, 1.6f, 0.8f), Mat("HomeChimney", new Color(0.55f, 0.32f, 0.26f), 0.1f));
        return root.gameObject;
    }

    // A small civic hall (library): pale stone, a portico of columns and a green roof, front facing local -z.
    private static GameObject CivicHall(Transform parent, Vector3 pos, float yaw)
    {
        var root = Group("Kütüphane", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var stone = Mat("HallStone", new Color(0.88f, 0.85f, 0.78f), 0.15f);
        var roof = Mat("HallRoof", new Color(0.30f, 0.52f, 0.42f), 0.2f);
        var glass = Mat("BuildingGlass", new Color(0.35f, 0.5f, 0.6f), 0.85f, 0.2f);
        Prim(PrimitiveType.Cube, "Plinth", root, new Vector3(0f, 0.2f, 0.3f), new Vector3(9f, 0.4f, 7.6f), stone, true);
        Prim(PrimitiveType.Cube, "Hall", root, new Vector3(0f, 2.6f, 1f), new Vector3(8f, 4.6f, 5.6f), stone, true);
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, 5.1f, 0.4f), new Vector3(8.8f, 0.5f, 7.2f), roof);
        Prim(PrimitiveType.Cube, "Pediment", root, new Vector3(0f, 5.65f, -2.7f), new Vector3(6.2f, 0.7f, 0.6f), stone);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cylinder, "Column", root, new Vector3(-2.7f + i * 1.8f, 2.6f, -2.6f), new Vector3(0.5f, 2.3f, 0.5f), stone);
        Prim(PrimitiveType.Cube, "Door", root, new Vector3(0f, 1.5f, -1.83f), new Vector3(1.5f, 2.4f, 0.1f), Mat("HallDoor", new Color(0.36f, 0.24f, 0.16f), 0.2f));
        foreach (float x in new[] { -2.6f, 2.6f })
            Prim(PrimitiveType.Cube, "Window", root, new Vector3(x, 2.8f, -1.83f), new Vector3(1.4f, 1.8f, 0.1f), glass);
        return root.gameObject;
    }

    // Neighbourhood market: open-fronted hall with a striped awning and produce crates.
    private static GameObject MarketHall(Transform parent, Vector3 pos, float yaw)
    {
        var root = Group("Mahalle Pazarı", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wall = Mat("MarketWall", new Color(0.93f, 0.88f, 0.76f), 0.1f);
        var roof = Mat("MarketRoof", new Color(0.36f, 0.30f, 0.26f), 0.15f);
        Prim(PrimitiveType.Cube, "Hall", root, new Vector3(0f, 2.2f, 1.2f), new Vector3(8f, 4.4f, 5f), wall, true);
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, 4.6f, 1.2f), new Vector3(8.6f, 0.4f, 5.6f), roof);
        for (int i = 0; i < 8; i++)
        {
            var stripe = Prim(PrimitiveType.Cube, "Awning", root, new Vector3(-3.5f + i * 1f, 3.3f, -2.1f), new Vector3(1f, 0.12f, 2.2f),
                Mat(i % 2 == 0 ? "AwningRed" : "AwningWhite", i % 2 == 0 ? new Color(0.86f, 0.30f, 0.27f) : new Color(0.97f, 0.95f, 0.9f), 0.2f));
            stripe.transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
        }
        var crate = Mat("CrateWood", new Color(0.62f, 0.44f, 0.26f), 0.1f);
        Color[] produce = { new Color(0.47f, 0.72f, 0.3f), new Color(0.95f, 0.55f, 0.2f), new Color(0.86f, 0.25f, 0.22f) };
        for (int i = 0; i < 3; i++)
        {
            Prim(PrimitiveType.Cube, "Crate", root, new Vector3(-2.4f + i * 2.4f, 0.45f, -2.2f), new Vector3(1.6f, 0.9f, 1f), crate, true);
            Prim(PrimitiveType.Cube, "Produce", root, new Vector3(-2.4f + i * 2.4f, 0.95f, -2.2f), new Vector3(1.4f, 0.18f, 0.85f), Mat("Produce" + i, produce[i], 0.3f));
        }
        Prim(PrimitiveType.Cube, "Counter", root, new Vector3(0f, 0.6f, -1.25f), new Vector3(7.6f, 1.2f, 0.4f), Mat("MarketCounter", new Color(0.44f, 0.30f, 0.18f), 0.1f), true);
        return root.gameObject;
    }

    // Health centre: white walls, a teal band and a green cross sign above the entrance.
    private static GameObject Clinic(Transform parent, Vector3 pos, float yaw)
    {
        var root = Group("Sağlık Ocağı", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var white = Mat("ClinicWall", new Color(0.95f, 0.96f, 0.95f), 0.2f);
        var teal = Mat("ClinicBand", new Color(0.24f, 0.62f, 0.62f), 0.2f);
        var green = Mat("ClinicCross", new Color(0.28f, 0.78f, 0.47f), 0.3f, 0f, false, new Color(0.2f, 0.7f, 0.4f) * 0.6f);
        var glass = Mat("BuildingGlass", new Color(0.35f, 0.5f, 0.6f), 0.85f, 0.2f);
        Prim(PrimitiveType.Cube, "Body", root, new Vector3(0f, 2.4f, 0.6f), new Vector3(8f, 4.8f, 6f), white, true);
        Prim(PrimitiveType.Cube, "Band", root, new Vector3(0f, 4.55f, 0.6f), new Vector3(8.1f, 0.5f, 6.1f), teal);
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, 4.95f, 0.6f), new Vector3(8.4f, 0.3f, 6.4f), Mat("BuildingRoof", new Color(0.22f, 0.24f, 0.26f), 0.1f));
        Prim(PrimitiveType.Cube, "Canopy", root, new Vector3(0f, 2.7f, -3.0f), new Vector3(3.6f, 0.2f, 1.6f), teal);
        Prim(PrimitiveType.Cube, "Door", root, new Vector3(0f, 1.2f, -2.43f), new Vector3(1.8f, 2.4f, 0.1f), glass);
        foreach (float x in new[] { -2.8f, 2.8f }) Prim(PrimitiveType.Cube, "Window", root, new Vector3(x, 2.6f, -2.43f), new Vector3(1.6f, 1.4f, 0.1f), glass);
        Prim(PrimitiveType.Cube, "Cross V", root, new Vector3(0f, 3.75f, -2.5f), new Vector3(0.45f, 1.3f, 0.12f), green);
        Prim(PrimitiveType.Cube, "Cross H", root, new Vector3(0f, 3.75f, -2.5f), new Vector3(1.3f, 0.45f, 0.12f), green);
        return root.gameObject;
    }

    private static void PlaceLamp(Transform parent, GameObject lampModel, Vector2 p, float yaw)
    {
        var pos = new Vector3(p.x, SampleGrid(roadField, p.x, p.y) + 0.15f, p.y);
        if (lampModel != null && SpawnModel(lampModel, parent, pos, yaw, 3.8f, 0f, 0f, false, false) != null) return;
        var lamp = Group("Street lamp", parent); lamp.position = pos; lamp.rotation = Quaternion.Euler(0f, yaw, 0f); lamp.localScale = Vector3.one * 0.72f;
        var metal = Mat("LampMetal", new Color(0.18f, 0.2f, 0.22f), 0.4f, 0.6f);
        Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(0f, 2.6f, 0f), new Vector3(0.14f, 2.6f, 0.14f), metal);
        Prim(PrimitiveType.Cube, "Arm", lamp, new Vector3(0f, 5.1f, -0.5f), new Vector3(0.12f, 0.1f, 1.2f), metal);
        Prim(PrimitiveType.Cube, "Lamp head", lamp, new Vector3(0f, 5.0f, -1.0f), new Vector3(0.35f, 0.14f, 0.7f),
            Mat("LampGlow", new Color(1f, 0.93f, 0.7f), 0.2f, 0f, false, new Color(1f, 0.85f, 0.5f) * 1.4f));
    }

    private static GameObject ProceduralBuilding(Transform parent, Vector3 pos, float yaw, float w, float d, float h)
    {
        var tint = new Color(F(0.62f, 0.86f), F(0.58f, 0.78f), F(0.52f, 0.72f));
        var root = Group("Building", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        Prim(PrimitiveType.Cube, "Body", root, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Mat("BuildingWall" + rng.Next(4), tint, 0.1f), true);
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, h + 0.2f, 0f), new Vector3(w + 0.6f, 0.4f, d + 0.6f), Mat("BuildingRoof", new Color(0.22f, 0.24f, 0.26f), 0.1f));
        var glass = Mat("BuildingGlass", new Color(0.35f, 0.5f, 0.6f), 0.85f, 0.2f);
        int floors = Mathf.Max(1, Mathf.FloorToInt(h / 3.4f));
        for (int f = 0; f < floors; f++)
            Prim(PrimitiveType.Cube, "Windows", root, new Vector3(0f, 1.8f + f * 3.4f, -d * 0.5f - 0.03f), new Vector3(w * 0.82f, 1.5f, 0.08f), glass);
        Prim(PrimitiveType.Cube, "Entrance", root, new Vector3(0f, 1.1f, -d * 0.5f - 0.05f), new Vector3(1.4f, 2.2f, 0.1f), Mat("BuildingDoor", new Color(0.3f, 0.24f, 0.2f), 0.2f));
        return root.gameObject;
    }

    // ---------------------------------------------------------------- industry

    private static void BuildIndustry()
    {
        var root = Groups["Industry"];
        BuildFactory(root, "Fabrika A", P(129, 88.6f), 0f, new Vector3(16f, 9f, 9f), 2, true);
        BuildFactory(root, "Fabrika B", P(130, 104.4f), 0f, new Vector3(18f, 8f, 7f), 2, true);
        BuildFactory(root, "Depo C", P(114, 70), 90f, new Vector3(14f, 6f, 9f), 1, false);
        BuildFactory(root, "Depo D", P(140, 70.6f), 0f, new Vector3(12f, 6f, 9f), 1, false);

        var tankMat = Mat("TankSteel", new Color(0.62f, 0.66f, 0.68f), 0.5f, 0.5f);
        var tanks = Group("Tank farm", root);
        for (int i = 0; i < 4; i++)
        {
            float x = 108f * L + (i % 2) * 6f, z = 90f * L + (i / 2) * 6.5f;
            var t = Prim(PrimitiveType.Cylinder, "Tank", tanks, new Vector3(x, GroundY(x, z) + 2.6f, z), new Vector3(4.6f, 2.6f, 4.6f), tankMat, true);
            Prim(PrimitiveType.Cylinder, "Tank cap", t.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), Mat("TankCap", new Color(0.3f, 0.33f, 0.35f), 0.3f, 0.5f));
        }
        KeepOut.Add(new Rect(104f * L, 86f * L, 14f, 15f));

        var yard = Group("Container yard", root);
        Color[] colors = { new Color(0.75f, 0.25f, 0.2f), new Color(0.2f, 0.4f, 0.6f), new Color(0.8f, 0.6f, 0.2f), new Color(0.25f, 0.5f, 0.35f) };
        for (int i = 0; i < 10; i++)
        {
            float x = 127f * L + (i % 2) * 2.2f - 1.1f, z = 66f * L + (i / 2) * 5f - 10f;
            if (RoadEdgeDistance(x, z) < 1.4f) continue;
            int stack = 1 + rng.Next(3);
            for (int s = 0; s < stack; s++)
                Prim(PrimitiveType.Cube, "Container", yard, new Vector3(x, GroundY(x, z) + 0.9f + s * 1.8f, z), new Vector3(1.8f, 1.75f, 4.2f),
                    Mat("Container" + (i % 4), colors[i % 4], 0.3f, 0.3f), true);
        }

        // Dumped waste by the depot road: gives the recycler something to do in the worst region.
        var junk = Group("Dump site", root);
        int dumped = 0;
        foreach (var name in new[] { "MetalScrap", "PlasticScrap", "OpenBox", "StorageBarrel", "SupplyBox" })
        {
            var prefab = Resources.Load<GameObject>("EcoArt/" + name);
            if (prefab == null) continue;
            for (int i = 0; i < 5; i++)
            {
                float x = F(108f * L, 120f * L), z = F(56f * L, 64f * L);
                if (RoadEdgeDistance(x, z) < 1.5f) continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, junk);
                item.transform.position = new Vector3(x, GroundY(x, z) + 0.05f, z); item.transform.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
                dumped++;
            }
        }
        if (dumped > 0) EcoMapMarker.Add(junk.gameObject, "scrap", "Atık sahası", "Geri dönüşüm aracıyla metal ve plastik topla.", EcoMapMarker.Category.Industry);

        // Outfall pipe where the polluted canal starts.
        var pipe = Group("Atık su çıkışı", root);
        Vector2 o = P(135.6f, 62.6f);
        pipe.position = new Vector3(o.x, GroundY(o.x, o.y) + 0.6f, o.y);
        var steel = Mat("PipeSteel", new Color(0.45f, 0.48f, 0.5f), 0.4f, 0.5f);
        var pipeBody = Prim(PrimitiveType.Cylinder, "Pipe", pipe, Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), steel, true);
        pipeBody.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        EcoMapMarker.Add(pipe.gameObject, "poi_inlet", "Atık su çıkışı", "Fabrikaların kirli suyu bu kanalla Doğu Gölü'ne iniyor.", EcoMapMarker.Category.Danger);
        report.AppendLine("Sanayi: 2 işlevsel (filtre takılabilir) + 2 depo, tanklar, konteynerler, atık sahası, kirli kanal");
    }

    private static void BuildFactory(Transform parent, string name, Vector2 xz, float yaw, Vector3 size, int chimneys, bool functional)
    {
        var root = Group(name, parent);
        root.position = new Vector3(xz.x, GroundY(xz.x, xz.y), xz.y); root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var concrete = Mat("FactoryConcrete", new Color(0.55f, 0.57f, 0.57f), 0.1f);
        var roofMat = Mat("FactoryRoof", new Color(0.17f, 0.2f, 0.22f), 0.15f);
        var brick = Mat("FactoryBrick", new Color(0.55f, 0.27f, 0.2f), 0.05f);
        var red = Mat("FactoryRed", new Color(0.78f, 0.2f, 0.17f), 0.1f);
        var glass = Mat("FactoryGlass", new Color(0.45f, 0.6f, 0.68f), 0.85f, 0.2f);
        Prim(PrimitiveType.Cube, "Hall", root, new Vector3(0f, size.y * 0.5f, 0f), size, concrete);
        // Saw-tooth roof reads as industry from the isometric view.
        int teeth = Mathf.Max(2, Mathf.RoundToInt(size.x / 4f));
        for (int i = 0; i < teeth; i++)
        {
            float x = -size.x * 0.5f + (i + 0.5f) * size.x / teeth;
            var tooth = Prim(PrimitiveType.Cube, "Roof tooth", root, new Vector3(x, size.y + 0.9f, 0f), new Vector3(size.x / teeth * 0.98f, 0.25f, size.z * 1.02f), roofMat);
            tooth.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
        }
        Prim(PrimitiveType.Cube, "Gate", root, new Vector3(0f, 1.7f, -size.z * 0.5f - 0.03f), new Vector3(4f, 3.4f, 0.2f), roofMat);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, "Window", root, new Vector3(-size.x * 0.36f + i * size.x * 0.24f, size.y * 0.68f, -size.z * 0.5f - 0.03f), new Vector3(2.2f, 1.2f, 0.12f), glass);
        ParticleSystem firstSmoke = null;
        for (int c = 0; c < chimneys; c++)
        {
            float cx = size.x * 0.5f - 2.5f - c * 3.6f, cz = size.z * 0.5f - 1.8f, ch = size.y + 8f + c * 2.5f;
            Prim(PrimitiveType.Cylinder, "Chimney", root, new Vector3(cx, ch * 0.5f, cz), new Vector3(1.6f, ch * 0.5f, 1.6f), brick);
            Prim(PrimitiveType.Cylinder, "Chimney band", root, new Vector3(cx, ch - 0.9f, cz), new Vector3(1.75f, 0.45f, 1.75f), red);
            Prim(PrimitiveType.Cylinder, "Chimney band", root, new Vector3(cx, ch - 3f, cz), new Vector3(1.75f, 0.45f, 1.75f), red);
            var smoke = MakeSmoke(root, new Vector3(cx, ch + 0.4f, cz), 0.6f);
            if (firstSmoke == null) firstSmoke = smoke;
        }
        var box = root.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0f, size.y * 0.5f, 0f); box.size = size;
        if (functional)
        {
            var filter = Prim(PrimitiveType.Cylinder, "Installed air filter", root, new Vector3(-size.x * 0.5f - 1.6f, 2f, -size.z * 0.2f),
                new Vector3(2.2f, 2f, 2.2f), Mat("FactoryFilter", new Color(0.2f, 0.62f, 0.4f), 0.3f, 0.3f));
            var factory = root.gameObject.AddComponent<EcoFactory>();
            factory.filterVisual = filter; factory.smoke = firstSmoke;
            filter.SetActive(false);
            WorldLabel(root, "FABRİKA\n[E] Büyük filtre tak", new Vector3(0f, size.y * 0.55f + 1.5f, -size.z * 0.5f - 1.5f), 0.009f);
        }
        else
        {
            WorldLabel(root, "ESKİ DEPO\nTerk edilmiş", new Vector3(0f, size.y * 0.5f + 1.5f, -size.z * 0.5f - 1.5f), 0.008f);
            EcoMapMarker.Add(root.gameObject, "region_industry", name, "Terk edilmiş depo.", EcoMapMarker.Category.Industry);
        }
        KeepOut.Add(new Rect(xz.x - size.x * 0.5f - 4f, xz.y - size.z * 0.5f - 3f, size.x + 8f, size.z + 6f));
    }

    // ---------------------------------------------------------------- energy (wind + solar)

    private static void BuildEnergy()
    {
        var root = Groups["Energy"];
        // Wind farm: the highest crests of the windy hills (outside the grove), at least 15 m apart.
        var wind = Def(EcoRegionInfo.Kind.Wind);
        var grove = Def(EcoRegionInfo.Kind.Grove);
        var groveRect = new Rect(grove.center - grove.size * 0.5f, grove.size);
        var candidates = new List<(Vector2 p, float h)>();
        for (float x = wind.center.x - wind.size.x * 0.5f + 5f; x < wind.center.x + wind.size.x * 0.5f - 5f; x += 3f)
            for (float z = wind.center.y - wind.size.y * 0.5f + 5f; z < wind.center.y + wind.size.y * 0.5f - 4f; z += 3f)
            {
                if (groveRect.Contains(new Vector2(x, z)) || RoadEdgeDistance(x, z) < 6f || Steepness(x, z) > 18f) continue;
                candidates.Add((new Vector2(x, z), GroundY(x, z)));
            }
        var chosen = new List<Vector2>();
        foreach (var c in candidates.OrderByDescending(cand => cand.h))
        {
            if (chosen.Count >= 4) break;
            if (chosen.All(o => Vector2.Distance(o, c.p) >= 15f)) chosen.Add(c.p);
        }
        var farm = Group("Wind farm", root);
        foreach (var p in chosen) BuildTurbine(farm, p);
        if (chosen.Count > 0)
        {
            var centre = new Vector2(chosen.Average(p => p.x), chosen.Average(p => p.y));
            var anchor = new GameObject("Rüzgâr çiftliği"); anchor.transform.SetParent(farm, false);
            anchor.transform.position = new Vector3(centre.x, GroundY(centre.x, centre.y), centre.y);
            EcoMapMarker.Add(anchor, "poi_turbine", "Rüzgâr çiftliği", "Eski türbinler. Bölgenin rüzgârı türbin kurmak için güçlü.", EcoMapMarker.Category.Energy, true);
        }
        report.AppendLine("Rüzgâr çiftliği: " + chosen.Count + " dekoratif türbin (rüzgâra göre döner, enerji üretmez)");

        BuildSolarField(root, new Rect(49f * L, 38.5f * L, 13f, 10f), "Batı güneş tarlası");
        BuildSolarField(root, new Rect(88f * L, 38.5f * L, 13f, 10f), "Doğu güneş tarlası");

        // Power line from the wind farm along the west boulevard into town.
        {
            var road = Roads.First(r => r.name == "Batı Bulvarı");
            var poles = new List<Vector3>();
            for (float along = 2f; along < road.dist[road.dist.Count - 1]; along += 12f)
            {
                int k = road.dist.FindIndex(d => d >= along);
                if (k < 0) break;
                Vector2 n = new Vector2(-road.tan[k].y, road.tan[k].x);
                Vector2 p = road.pos[k] - n * (road.Width * 0.5f + 3.4f);
                if (Def(EcoRegionInfo.Kind.City).center.x - Def(EcoRegionInfo.Kind.City).size.x * 0.5f < p.x) continue; // not inside town
                var pos = new Vector3(p.x, GroundY(p.x, p.y), p.y);
                float yaw = Mathf.Atan2(road.tan[k].x, road.tan[k].y) * Mathf.Rad2Deg + 90f;
                GameObject pole = polePrefab != null ? SpawnModel(polePrefab, root, pos, yaw, 6.5f, 0f, 0f, true) : null;
                if (pole == null) pole = ProceduralPole(root, pos, yaw - 90f);
                poles.Add(pos + Vector3.up * 6.1f);
            }
            var lineMat = Mat("PowerLine", new Color(0.05f, 0.05f, 0.06f), 0.1f);
            var wires = Group("Power lines", root);
            for (int i = 0; i + 1 < poles.Count; i++)
            {
                if (Vector3.Distance(poles[i], poles[i + 1]) > 26f) continue;
                var go = new GameObject("Wire"); go.transform.SetParent(wires, false);
                var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.widthMultiplier = 0.04f;
                line.sharedMaterial = lineMat; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.positionCount = 7;
                for (int k = 0; k < 7; k++)
                {
                    float t = k / 6f;
                    var p = Vector3.Lerp(poles[i], poles[i + 1], t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * 0.6f;
                    line.SetPosition(k, p);
                }
            }
        }
    }

    private static GameObject ProceduralPole(Transform parent, Vector3 pos, float yaw)
    {
        var pole = Group("Power pole", parent); pole.position = pos; pole.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wood = Mat("PoleWood", new Color(0.36f, 0.26f, 0.17f), 0.1f);
        Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0f, 3.3f, 0f), new Vector3(0.2f, 3.3f, 0.2f), wood, true);
        Prim(PrimitiveType.Cube, "Crossarm", pole, new Vector3(0f, 6.0f, 0f), new Vector3(0.14f, 0.14f, 2.0f), wood);
        return pole.gameObject;
    }

    private static void BuildTurbine(Transform parent, Vector2 p)
    {
        var root = Group("Wind turbine", parent);
        root.position = new Vector3(p.x, GroundY(p.x, p.y) - 0.2f, p.y);
        root.rotation = Quaternion.Euler(0f, F(-15f, 15f) - 90f, 0f); // rotor faces the prevailing western wind
        root.localScale = Vector3.one * 0.45f; // compact map: ~15 m towers
        var white = Mat("TurbineWhite", new Color(0.93f, 0.94f, 0.92f), 0.35f);
        Prim(PrimitiveType.Cylinder, "Tower", root, new Vector3(0f, 17f, 0f), new Vector3(1.5f, 17f, 1.5f), white);
        var capsule = root.gameObject.AddComponent<CapsuleCollider>(); capsule.center = new Vector3(0f, 17f, 0f); capsule.radius = 1.2f; capsule.height = 34f;
        Prim(PrimitiveType.Cube, "Nacelle", root, new Vector3(0f, 34.6f, -0.4f), new Vector3(2.2f, 2.4f, 5.6f), white);
        var rotor = new GameObject("Rotor").transform; rotor.SetParent(root, false); rotor.localPosition = new Vector3(0f, 34.6f, 2.9f);
        Prim(PrimitiveType.Sphere, "Hub", rotor, Vector3.zero, new Vector3(1.8f, 1.8f, 1.8f), white);
        var bladeMat = Mat("TurbineBlade", new Color(0.96f, 0.96f, 0.95f), 0.3f);
        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("Blade " + i).transform; arm.SetParent(rotor, false); arm.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
            Prim(PrimitiveType.Cube, "Blade", arm, new Vector3(0f, 7.6f, 0f), new Vector3(0.7f, 15f, 0.22f), bladeMat);
            Prim(PrimitiveType.Cube, "Tip", arm, new Vector3(0f, 14.2f, 0.02f), new Vector3(0.72f, 1.6f, 0.24f), Mat("TurbineTip", new Color(0.85f, 0.2f, 0.18f), 0.3f));
        }
        var spin = rotor.gameObject.AddComponent<EcoDecorRotor>(); spin.axis = Vector3.forward; spin.degreesPerSecond = 150f;
        KeepOut.Add(new Rect(p.x - 4f, p.y - 4f, 8f, 8f));
    }

    private static void BuildSolarField(Transform parent, Rect area, string title)
    {
        var field = Group(title, parent);
        int placed = 0;
        if (solarPrefab != null)
        {
            var probe = SpawnModel(solarPrefab, field, new Vector3(area.center.x, GroundY(area.center.x, area.center.y), area.center.y), 0f, 0f, 0f, 0f, false, false);
            if (probe != null && TryBounds(probe, out var pb) && pb.size.x < 20f && pb.size.z < 20f && pb.size.x > 0.5f && pb.size.z > 0.5f)
            {
                float sx = pb.size.x + 1.0f, sz = pb.size.z + 2f;
                Object.DestroyImmediate(probe);
                for (float z = area.yMin + sz * 0.5f; z < area.yMax - sz * 0.4f && placed < 16; z += sz)
                    for (float x = area.xMin + sx * 0.5f; x < area.xMax - sx * 0.4f && placed < 16; x += sx)
                    {
                        var g = SpawnModel(solarPrefab, field, new Vector3(x, GroundY(x, z), z), 0f, 0f, 0f, 0f, true, false);
                        if (g != null) placed++;
                    }
            }
            else if (probe != null) Object.DestroyImmediate(probe);
        }
        if (placed == 0)
        {
            var frame = Mat("SolarFrame", new Color(0.7f, 0.74f, 0.78f), 0.5f, 0.6f);
            var cell = Mat("SolarCell", new Color(0.05f, 0.13f, 0.32f), 0.8f, 0.35f);
            for (float z = area.yMin + 2f; z < area.yMax - 1f; z += 4.5f)
            {
                var row = Group("Solar row", field);
                float baseY = GroundY(area.center.x, z);
                row.position = new Vector3(area.xMin + 1f, baseY, z);
                int count = Mathf.FloorToInt((area.width - 2f) / 3.4f);
                for (int i = 0; i < count; i++)
                {
                    float x = 1.7f + i * 3.4f;
                    var panel = Prim(PrimitiveType.Cube, "Panel", row, new Vector3(x, 1.2f, 0f), new Vector3(3.1f, 0.08f, 2.0f), cell);
                    panel.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Frame", row, new Vector3(x, 1.18f, 0f), new Vector3(3.2f, 0.05f, 2.1f), frame).transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, "Post", row, new Vector3(x, 0.6f, 0.15f), new Vector3(0.12f, 0.6f, 0.12f), frame);
                }
                var col = row.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(count * 1.7f, 0.9f, 0f); col.size = new Vector3(count * 3.4f, 1.8f, 2.2f);
                placed++;
            }
        }
        var anchor = new GameObject("Marker"); anchor.transform.SetParent(field, false);
        anchor.transform.position = new Vector3(area.center.x, GroundY(area.center.x, area.center.y), area.center.y);
        EcoMapMarker.Add(anchor, "poi_solar", title, "Eski panel dizisi. Kendi güneş panelini [B] ile çayıra kurabilirsin.", EcoMapMarker.Category.Energy);
        KeepOut.Add(area);
        report.AppendLine(title + ": " + placed + " öğe");
    }

    // ---------------------------------------------------------------- glacier

    private static void BuildGlacier()
    {
        var root = Groups["Glacier"];
        // The glacier tongue: broad, low ice floes half-buried in snow, with crevassed seracs on top. Each piece is an
        // irregular faceted prism (unit size, centred pivot) so EcoGlacierMelt can lower it in place as carbon rises.
        var iceMat = VertexMat("EcoIce", true, 0.55f);
        var iceMeshes = Enumerable.Range(0, 5).Select(IceMesh).ToArray();
        var blocks = new List<Transform>();
        var tongue = new List<Vector2>();
        GameObject IcePiece(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            go.transform.SetParent(root, false); go.transform.position = pos; go.transform.localScale = size;
            var mesh = iceMeshes[rng.Next(iceMeshes.Length)];
            go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = iceMat;
            var col = go.GetComponent<MeshCollider>(); col.sharedMesh = mesh; col.convex = true;
            go.transform.rotation = Quaternion.Euler(F(-3f, 3f), F(0f, 360f), F(-3f, 3f));
            return go;
        }
        for (int i = 0; i < 140 && tongue.Count < 15; i++)
        {
            float x = F(10f, Size - 10f), z = F(122f * L, Size - 6f);
            var p = new Vector2(x, z);
            if (RoadEdgeDistance(x, z) < 6f || Vector2.Distance(p, StationPad) < 13f || tongue.Any(o => Vector2.Distance(o, p) < 12f)) continue;
            tongue.Add(p);
            float ground = GroundY(x, z), w = F(7f, 12f), d = F(6f, 10f), hgt = F(1.4f, 2.8f);
            var slab = IcePiece("Ice floe", new Vector3(x, ground + hgt * 0.2f, z), new Vector3(w, hgt, d));
            blocks.Add(slab.transform);
            int seracs = 1 + rng.Next(3);
            for (int s = 0; s < seracs; s++)
            {
                float hh = F(2.2f, 5f);
                var serac = IcePiece("Serac", slab.transform.position + new Vector3(F(-w * 0.28f, w * 0.28f), hgt * 0.35f + hh * 0.35f, F(-d * 0.28f, d * 0.28f)),
                    new Vector3(F(1.6f, 3f), hh, F(1.6f, 3f)));
                serac.transform.rotation = Quaternion.Euler(F(-7f, 7f), F(0f, 360f), F(-7f, 7f));
                blocks.Add(serac.transform);
            }
        }
        var melt = root.gameObject.AddComponent<EcoGlacierMelt>(); melt.ice = blocks;
        var meltAnchor = new GameObject("Erime çizgisi"); meltAnchor.transform.SetParent(root, false);
        meltAnchor.transform.position = new Vector3(Size * 0.35f, GroundY(Size * 0.35f, 128f * L), 128f * L);
        EcoMapMarker.Add(meltAnchor, "poi_glacier", "Buzul dili", "Buz kütleleri dünya karbonuna göre erir veya toparlanır.", EcoMapMarker.Category.Research);

        // Research station at the end of the trail (the ground is levelled under it in RawHeight).
        float gy = GroundY(StationPad.x, StationPad.y);
        var cabin = Group("Glacier research station", root); cabin.position = new Vector3(StationPad.x, gy, StationPad.y); cabin.localScale = Vector3.one * 0.8f;
        cabin.rotation = Quaternion.Euler(0f, -30f, 0f);
        var wall = Mat("CabinWall", new Color(0.72f, 0.28f, 0.22f), 0.1f);
        Prim(PrimitiveType.Cube, "Foundation", cabin, new Vector3(0f, -0.8f, 0f), new Vector3(9f, 3f, 7f), Mat("CabinStone", new Color(0.4f, 0.4f, 0.42f), 0.1f));
        Prim(PrimitiveType.Cube, "Cabin", cabin, new Vector3(0f, 2.2f, 0f), new Vector3(8f, 3.4f, 6f), wall, true);
        Prim(PrimitiveType.Cube, "Roof", cabin, new Vector3(0f, 4.2f, 0f), new Vector3(8.8f, 0.5f, 6.8f), Mat("CabinSnowRoof", new Color(0.93f, 0.96f, 0.98f), 0.1f));
        Prim(PrimitiveType.Cube, "Window", cabin, new Vector3(-1.8f, 2.4f, -3.03f), new Vector3(1.8f, 1.2f, 0.1f), Mat("CabinGlow", new Color(1f, 0.9f, 0.6f), 0.5f, 0f, false, new Color(1f, 0.8f, 0.45f)));
        Prim(PrimitiveType.Cube, "Door", cabin, new Vector3(2f, 1.8f, -3.03f), new Vector3(1.3f, 2.4f, 0.1f), Mat("CabinDoor", new Color(0.25f, 0.18f, 0.12f), 0.1f));
        Prim(PrimitiveType.Cylinder, "Antenna", cabin, new Vector3(3f, 6.5f, 2f), new Vector3(0.12f, 2.2f, 0.12f), Mat("LampMetal", new Color(0.18f, 0.2f, 0.22f), 0.4f, 0.6f));
        WorldLabel(cabin, "BUZUL İZLEME İSTASYONU\nKarbon ayak izi arttıkça buzullar erir", new Vector3(0f, 6.4f, -2f), 0.01f);
        cabin.gameObject.AddComponent<EcoGlacierStation>(); // [E] switches it on; carbon below its threshold keeps the melt line back
        KeepOut.Add(new Rect(StationPad.x - 6f, StationPad.y - 5f, 12f, 10f));

        MakeSmoke(root, new Vector3(Size * 0.5f, 46f, 132f * L), 1f, true);
        report.AppendLine("Buzul: " + blocks.Count + " buz bloğu (karbonla küçülür), izleme istasyonu, kar yağışı");
    }

    // Irregular faceted ice prism in a unit box centred on the origin: snowy top, blue flanks, deep-blue base.
    private static Mesh IceMesh(int seed)
    {
        var r = new System.Random(9100 + seed * 31);
        float Rf(float a, float b) => a + (float)r.NextDouble() * (b - a);
        int sides = 6 + r.Next(3);
        var bottom = new Vector3[sides]; var top = new Vector3[sides];
        float phase = Rf(0f, 1f);
        for (int s = 0; s < sides; s++)
        {
            float a = (s + phase + Rf(-0.2f, 0.2f)) / sides * Mathf.PI * 2f, rad = Rf(0.38f, 0.5f);
            bottom[s] = new Vector3(Mathf.Cos(a) * rad, -0.5f, Mathf.Sin(a) * rad);
            float shrink = Rf(0.62f, 0.86f);
            top[s] = new Vector3(bottom[s].x * shrink + Rf(-0.05f, 0.05f), Rf(0.32f, 0.5f), bottom[s].z * shrink + Rf(-0.05f, 0.05f));
        }
        var mb = new MeshBuilder();
        Color snowTop = new Color(0.93f, 0.97f, 1f), iceSide = new Color(0.58f, 0.80f, 0.93f), iceDeep = new Color(0.36f, 0.60f, 0.82f);
        var crown = new Vector3(Rf(-0.06f, 0.06f), 0.5f, Rf(-0.06f, 0.06f));
        for (int s = 0; s < sides; s++)
        {
            int n = (s + 1) % sides;
            Vector3 outward = ((bottom[s] + bottom[n]) * 0.5f); outward.y = 0f;
            mb.Quad(bottom[s], bottom[n], top[s], top[n], iceDeep, iceDeep, iceSide, iceSide, outward);
            mb.Quad(top[s], top[n], crown, crown, snowTop, snowTop, snowTop, snowTop, Vector3.up);
        }
        var mesh = mb.ToMesh("IceFloe_" + seed);
        AssetDatabase.CreateAsset(mesh, Folder + "Meshes/IceFloe_" + seed + ".asset");
        return mesh;
    }

    // ---------------------------------------------------------------- vegetation

    private static void BuildVegetation()
    {
        var root = Groups["Vegetation"];
        var leafy = BuildSet(new[] { "tree" }, new[] { "palm", "pine", "plateau", "stump", "log", "cone", "tent", "house", "fall", "dead" }, KenneyFolders());
        var pines = BuildSet(new[] { "pine", "cone" }, new[] { "log", "stump", "fall" }, KenneyFolders());
        var dead = BuildSet(new[] { "dead", "stump" }, new[] { "tile", "log" }, KenneyFolders());
        var rocks = BuildSet(new[] { "rock", "stone" }, new[] { "stepping", "path", "crystal", "tile" }, KenneyFolders());
        var plants = BuildSet(new[] { "bush", "flower", "plant", "mushroom" }, new[] { "tile", "pot" }, KenneyFolders());
        report.AppendLine("Bitki modelleri: yapraklı=" + leafy.models.Count + " çam=" + pines.models.Count + " kuru=" + dead.models.Count + " kaya=" + rocks.models.Count + " bitki=" + plants.models.Count);

        var trees = Group("Trees", root); var stones = Group("Rocks", root); var small = Group("Plants", root); var forest = Group("Border forest", root);
        var heritageCount = new Dictionary<int, int>();
        int treeCount = 0, rockCount = 0, plantCount = 0, borderCount = 0, treeRejectPlant = 0, treeRejectSlope = 0;
        float glacierStart = Def(EcoRegionInfo.Kind.Glacier).center.y - Def(EcoRegionInfo.Kind.Glacier).size.y * 0.5f;

        for (float x = 3f; x < Size - 3f && treeCount < 360; x += 3.5f)
            for (float z = 3f; z < Size - 3f && treeCount < 360; z += 3.5f)
            {
                float px = x + F(-1.8f, 1.8f), pz = z + F(-1.8f, 1.8f);
                if (!CanPlant(px, pz, 2.2f)) { treeRejectPlant++; continue; }
                float h = GroundY(px, pz);
                if (h < WaterY + 1f || Steepness(px, pz) > 26f) { treeRejectSlope++; continue; }
                int ri = RegionIndexAt(px, pz);
                float density = ri < 0 ? 0f : Defs[ri].kind switch
                {
                    EcoRegionInfo.Kind.Grove => 0.6f, EcoRegionInfo.Kind.Wind => 0.16f, EcoRegionInfo.Kind.Sun => 0.09f,
                    EcoRegionInfo.Kind.Industry => 0.03f, EcoRegionInfo.Kind.City => 0.12f,
                    EcoRegionInfo.Kind.Glacier => pz < glacierStart + 10f ? 0.24f : 0f,
                    EcoRegionInfo.Kind.Lake => Defs[ri].water < 30f ? 0.38f : 0.14f, _ => 0f,
                };
                float edge = Mathf.Min(Mathf.Min(px, pz), Mathf.Min(Size - px, Size - pz));
                density += (1f - Smooth(0f, 10f, edge)) * 0.3f;
                density *= 0.4f + 1.6f * Fbm(px * 0.07f, pz * 0.07f, 3);
                if (rng.NextDouble() > density) continue;
                bool isGrove = ri >= 0 && Defs[ri].kind == EcoRegionInfo.Kind.Grove;
                // The damaged grove: a third of the trees are dead or burnt, so the project has a visible "before".
                if (isGrove && dead.Any && rng.NextDouble() < 0.33)
                {
                    int idx = rng.Next(dead.models.Count);
                    var snag = SpawnModel(dead.models[idx], trees, new Vector3(px, h, pz), F(0f, 360f), F(2f, 4f), 3.5f, 3.5f, false);
                    if (snag != null) { snag.name = "Damaged tree"; treeCount++; }
                    continue;
                }
                bool pine = pz > glacierStart - 6f || edge < 8f || rng.NextDouble() < (isGrove ? 0.35 : 0.12);
                bool heritage = ri >= 0 && Defs[ri].kind != EcoRegionInfo.Kind.Industry && Defs[ri].kind != EcoRegionInfo.Kind.Glacier
                    && (!heritageCount.ContainsKey(ri) || heritageCount[ri] < 3) && edge > 10f;
                var tree = PlaceTree(trees, new Vector3(px, h, pz), pine ? pines : leafy, pine ? leafy : pines, pine, heritage);
                if (tree == null) continue;
                treeCount++;
                if (heritage) heritageCount[ri] = heritageCount.TryGetValue(ri, out int c) ? c + 1 : 1;
            }

        // Every healthy region needs at least three living trees so birds and butterflies can return.
        for (int regionIndex = 0; regionIndex < Defs.Count; regionIndex++)
        {
            var def = Defs[regionIndex];
            if (def.kind == EcoRegionInfo.Kind.Industry || def.kind == EcoRegionInfo.Kind.Glacier || def.kind == EcoRegionInfo.Kind.City) continue;
            int have = heritageCount.TryGetValue(regionIndex, out int existing) ? existing : 0;
            for (int attempt = 0; attempt < 400 && have < 3; attempt++)
            {
                float tx = def.center.x + F(-def.size.x * 0.45f, def.size.x * 0.45f), tz = def.center.y + F(-def.size.y * 0.45f, def.size.y * 0.45f);
                if (RegionIndexAt(tx, tz) != regionIndex || !CanPlant(tx, tz, 3f)) continue;
                float ty = GroundY(tx, tz);
                if (ty < WaterY + 1.5f || Steepness(tx, tz) > 20f) continue;
                if (PlaceTree(trees, new Vector3(tx, ty, tz), leafy, pines, false, true) == null) continue;
                have++; treeCount++;
            }
        }

        // A dense forest on the hills beyond the walls hides the map edge.
        for (float x = -Border + 2f; x < Size + Border - 2f; x += 3.2f)
            for (float z = -Border + 2f; z < Size + Border - 2f; z += 3.2f)
            {
                float px = x + F(-1.4f, 1.4f), pz = z + F(-1.4f, 1.4f);
                float outside = Outside(px, pz);
                if (outside < 1.5f || pz > Size + 6f) continue;
                if (rng.NextDouble() > 0.55f) continue;
                float h = GroundY(px, pz);
                var set = pines.Any && rng.NextDouble() < 0.7 ? pines : leafy;
                if (!set.Any) continue;
                int idx = rng.Next(set.models.Count);
                var t = SpawnModel(set.models[idx], forest, new Vector3(px, h - 0.1f, pz), F(0f, 360f), F(6f, 10f), 0f, 0f, false);
                if (t != null) { t.isStatic = true; borderCount++; }
            }

        for (float x = 4f; x < Size - 4f && rockCount < 110; x += 5f)
            for (float z = 4f; z < Size - 4f && rockCount < 110; z += 5f)
            {
                float px = x + F(-2f, 2f), pz = z + F(-2f, 2f);
                if (!CanPlant(px, pz, 1.6f)) continue;
                float h = GroundY(px, pz);
                if (h < WaterY - 1f) continue;
                int ri = RegionIndexAt(px, pz);
                float edge = Mathf.Min(Mathf.Min(px, pz), Mathf.Min(Size - px, Size - pz));
                float prob = ri < 0 ? 0f : Defs[ri].kind switch
                {
                    EcoRegionInfo.Kind.Glacier => 0.14f, EcoRegionInfo.Kind.Wind => 0.08f, EcoRegionInfo.Kind.Lake => 0.08f, EcoRegionInfo.Kind.Grove => 0.06f,
                    EcoRegionInfo.Kind.Industry => 0.03f, EcoRegionInfo.Kind.Sun => 0.02f, _ => 0.004f,
                };
                prob += (1f - Smooth(0f, 10f, edge)) * 0.15f;
                if (rng.NextDouble() > prob) continue;
                float size = ri >= 0 && Defs[ri].kind == EcoRegionInfo.Kind.Glacier ? F(0.9f, 2.4f) : F(0.4f, 1.4f);
                GameObject rock;
                if (rocks.Any)
                {
                    int idx = rng.Next(rocks.models.Count);
                    float target = Mathf.Clamp(size * rocks.heights[idx] / rocks.average, size * 0.6f, size * 1.7f);
                    rock = SpawnModel(rocks.models[idx], stones, new Vector3(px, h - 0.1f, pz), F(0f, 360f), target, 0f, 0f, true);
                }
                else
                {
                    rock = Prim(PrimitiveType.Sphere, "Rock", stones, new Vector3(px, h + size * 0.25f, pz), new Vector3(size * 1.3f, size * 0.8f, size), Mat("ProcRock", new Color(0.46f, 0.45f, 0.46f), 0.1f), true);
                    rock.transform.rotation = Quaternion.Euler(F(-10f, 10f), F(0f, 360f), F(-10f, 10f));
                }
                if (rock != null) rockCount++;
            }

        if (plants.Any)
            for (float x = 4f; x < Size - 4f && plantCount < 160; x += 3.5f)
                for (float z = 4f; z < Size - 4f && plantCount < 160; z += 3.5f)
                {
                    float px = x + F(-1.5f, 1.5f), pz = z + F(-1.5f, 1.5f);
                    int ri = RegionIndexAt(px, pz);
                    if (ri < 0 || !CanPlant(px, pz, 0.8f, true)) continue;
                    var kind = Defs[ri].kind;
                    float chance = kind == EcoRegionInfo.Kind.Sun ? 0.1f : kind == EcoRegionInfo.Kind.Wind ? 0.08f : kind == EcoRegionInfo.Kind.City ? 0.07f
                        : kind == EcoRegionInfo.Kind.Lake && Defs[ri].water < 30f ? 0.12f : kind == EcoRegionInfo.Kind.Grove ? 0.06f : 0f;
                    if (rng.NextDouble() > chance) continue;
                    float h = GroundY(px, pz);
                    if (h < WaterY + 0.6f || Steepness(px, pz) > 20f) continue;
                    int idx = rng.Next(plants.models.Count);
                    var plant = SpawnModel(plants.models[idx], small, new Vector3(px, h, pz), F(0f, 360f), F(0.4f, 0.9f) * plants.heights[idx] / plants.average, 0f, 0f, false);
                    if (plant != null) plantCount++;
                }
        report.AppendLine("Ağaç adayları: yer uygun değil " + treeRejectPlant + ", eğim/su " + treeRejectSlope);
        report.AppendLine("Bitki örtüsü: " + treeCount + " ağaç, " + borderCount + " sınır ormanı ağacı, " + rockCount + " kaya, " + plantCount + " küçük bitki. "
            + "EcoVegetation (kuş/kelebek için canlı ağaç) yalnız bölge başına en çok 3 ağaçta; diğerleri dekor.");
    }

    private static bool CanPlant(float x, float z, float roadMargin, bool allowCity = false)
    {
        if (x < 2f || z < 2f || x > Size - 2f || z > Size - 2f) return false;
        if (RoadEdgeDistance(x, z) < roadMargin) return false;
        if (Vector2.Distance(new Vector2(x, z), Base) < 20f) return false;
        var p = new Vector2(x, z);
        if (!allowCity && Vector2.Distance(p, Roundabout) < RoundaboutR + 6f) return false;
        foreach (var r in KeepOut) if (r.Contains(p)) return false;
        return true;
    }

    private static GameObject PlaceTree(Transform parent, Vector3 pos, ModelSet primary, ModelSet fallback, bool pine, bool heritage)
    {
        var set = primary.Any ? primary : fallback;
        float baseHeight = pine ? F(5f, 8f) : F(4f, 6.5f);
        var holder = new GameObject(pine ? "Pine" : "Tree"); holder.transform.SetParent(parent, false); holder.transform.position = pos;
        GameObject inst = null; float target = baseHeight;
        if (set.Any)
        {
            int idx = rng.Next(set.models.Count);
            target = Mathf.Clamp(baseHeight * set.heights[idx] / set.average, baseHeight * 0.6f, baseHeight * 1.6f);
            inst = SpawnModel(set.models[idx], holder.transform, pos, F(0f, 360f), target, 0f, 0f, false);
        }
        if (inst == null)
        {
            var trunk = Mat("ProcTrunk", new Color(0.36f, 0.25f, 0.15f), 0.05f);
            var leaf = Mat(pine ? "ProcPine" : "ProcLeaf", pine ? new Color(0.13f, 0.34f, 0.2f) : new Color(0.28f, 0.52f, 0.2f), 0.05f);
            Prim(PrimitiveType.Cylinder, "Trunk", holder.transform, new Vector3(0f, baseHeight * 0.2f, 0f), new Vector3(0.45f, baseHeight * 0.2f, 0.45f), trunk);
            if (pine)
                for (int i = 0; i < 3; i++)
                {
                    float k = 1f - i * 0.28f;
                    var tier = Prim(PrimitiveType.Sphere, "Tier", holder.transform, Vector3.zero, new Vector3(baseHeight * 0.42f * k, baseHeight * 0.34f, baseHeight * 0.42f * k), leaf);
                    tier.transform.position = pos + new Vector3(0f, baseHeight * (0.38f + i * 0.2f), 0f);
                }
            else
            {
                var crown = Prim(PrimitiveType.Sphere, "Crown", holder.transform, Vector3.zero, new Vector3(baseHeight * 0.7f, baseHeight * 0.6f, baseHeight * 0.7f), leaf);
                crown.transform.position = pos + new Vector3(0f, baseHeight * 0.68f, 0f);
            }
        }
        var capsule = holder.AddComponent<CapsuleCollider>();
        capsule.radius = 0.35f * Mathf.Clamp(target / 7f, 0.6f, 1.4f); capsule.height = Mathf.Min(target, 3.5f); capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        if (heritage)
        {
            var vegetation = holder.AddComponent<EcoVegetation>();
            vegetation.foliage = holder.GetComponentsInChildren<Renderer>();
            holder.name += " (canlı ağaç)";
        }
        KeepOut.Add(new Rect(pos.x - 1f, pos.z - 1f, 2f, 2f));
        return holder;
    }

    // ---------------------------------------------------------------- signs

    // Kenney/asset-pack prefabs ship with the built-in "Standard" shader, which renders magenta in URP.
    // Make URP Lit copies (colour + main texture carried over) and assign them to the scene instances only;
    // the original prefab materials are never touched.
    private static void FixBuiltinMaterials()
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null || worldRoot == null) return;
        var map = new Dictionary<Material, Material>();
        int fixedCount = 0;
        foreach (var renderer in worldRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
            var shared = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < shared.Length; i++)
            {
                var source = shared[i];
                if (source == null || source.shader == null) continue;
                string shaderName = source.shader.name;
                if (shaderName != "Standard" && shaderName != "Hidden/InternalErrorShader" && shaderName != "Standard (Specular setup)") continue;
                if (!map.TryGetValue(source, out var replacement))
                {
                    string safe = string.Concat(source.name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
                    string path = Folder + "Materials/URP_" + safe + ".mat";
                    replacement = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (replacement == null)
                    {
                        replacement = new Material(urp) { name = "URP_" + safe };
                        AssetDatabase.CreateAsset(replacement, path);
                    }
                    Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    replacement.SetColor("_BaseColor", color);
                    if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
                        replacement.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                    replacement.SetFloat("_Smoothness", 0.15f); replacement.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(replacement);
                    map[source] = replacement;
                }
                shared[i] = replacement; changed = true;
            }
            if (changed) { renderer.sharedMaterials = shared; fixedCount++; }
        }
        report.AppendLine("Malzeme düzeltme: " + fixedCount + " renderer URP Lit'e çevrildi (" + map.Count + " malzeme).");
        RestyleNatureMaterials();
        ApplyFolderTextures();
    }

    // Meshy/Tripo exports (market, clinic…) import white: their baked texture sits next to the FBX as *_texture.png.
    private static void ApplyFolderTextures()
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        var byFolder = new Dictionary<string, Material>();
        int count = 0;
        foreach (var renderer in worldRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(renderer.gameObject);
            string path = source != null ? AssetDatabase.GetAssetPath(source) : "";
            if (!path.Contains("Zipler ve Modeller") && !path.Contains("/Modeller/")) continue;
            var shared = renderer.sharedMaterials;
            if (shared.Any(m => m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)) continue;
            string folder = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!byFolder.TryGetValue(folder, out var material))
            {
                material = null;
                string texPath = Directory.GetFiles(folder, "*_texture.png").Select(f => f.Replace('\\', '/')).FirstOrDefault();
                if (texPath != null)
                {
                    string matPath = Folder + "Materials/Tex_" + string.Concat(Path.GetFileName(folder).Where(char.IsLetterOrDigit)) + ".mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (material == null) { material = new Material(urp); AssetDatabase.CreateAsset(material, matPath); }
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                    material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", 0.2f); material.SetFloat("_Metallic", 0f);
                    EditorUtility.SetDirty(material);
                }
                byFolder[folder] = material;
            }
            if (material == null) continue;
            for (int i = 0; i < shared.Length; i++) shared[i] = material;
            renderer.sharedMaterials = shared; count++;
        }
        report.AppendLine("Model dokuları: " + count + " renderer klasöründeki *_texture.png ile boyandı");
    }

    // The Kenney nature kits import with washed-out teal leaves and salmon bark. Scene instances get a curated
    // palette that matches the ground colours (prefab assets stay untouched).
    private static readonly (string key, Color color, float smooth)[] NaturePalette =
    {
        ("leafsdark", new Color(0.20f, 0.42f, 0.22f), 0.1f), ("leafsgreen", new Color(0.35f, 0.58f, 0.24f), 0.1f),
        ("leafsfall", new Color(0.86f, 0.52f, 0.20f), 0.1f), ("leafslight", new Color(0.50f, 0.70f, 0.30f), 0.1f),
        ("leafs", new Color(0.40f, 0.62f, 0.27f), 0.1f), ("woodbarkdark", new Color(0.30f, 0.21f, 0.14f), 0.05f),
        ("woodbark", new Color(0.45f, 0.31f, 0.19f), 0.05f), ("woodinner", new Color(0.78f, 0.62f, 0.42f), 0.05f),
        ("wooddark", new Color(0.30f, 0.22f, 0.15f), 0.05f), ("wood", new Color(0.50f, 0.36f, 0.22f), 0.05f),
        ("stonedark", new Color(0.38f, 0.39f, 0.41f), 0.1f), ("stone", new Color(0.58f, 0.58f, 0.60f), 0.1f),
        ("grass", new Color(0.42f, 0.64f, 0.28f), 0.05f), ("dirtdark", new Color(0.36f, 0.27f, 0.19f), 0.05f),
        ("dirt", new Color(0.50f, 0.38f, 0.26f), 0.05f), ("water", new Color(0.40f, 0.70f, 0.85f), 0.8f),
        ("colorred", new Color(0.86f, 0.33f, 0.33f), 0.15f), ("coloryellow", new Color(0.96f, 0.80f, 0.35f), 0.15f),
        ("colorpurple", new Color(0.62f, 0.47f, 0.86f), 0.15f), ("colorwhite", new Color(0.95f, 0.95f, 0.94f), 0.15f),
        ("colorblue", new Color(0.35f, 0.55f, 0.86f), 0.15f), ("colororange", new Color(0.92f, 0.55f, 0.25f), 0.15f),
    };

    private static void RestyleNatureMaterials()
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        var cache = new Dictionary<string, Material>();
        int count = 0;
        foreach (var renderer in worldRoot.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer) continue;
            var shared = renderer.sharedMaterials; bool changed = false;
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                string n = shared[i].name.ToLowerInvariant().Replace("urp_", "").Replace(" (instance)", "").Trim();
                if (n.StartsWith("nature_")) continue;
                foreach (var entry in NaturePalette)
                {
                    if (!n.StartsWith(entry.key)) continue;
                    if (!cache.TryGetValue(entry.key, out var m))
                    {
                        string path = Folder + "Materials/Nature_" + entry.key + ".mat";
                        m = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (m == null) { m = new Material(urp); AssetDatabase.CreateAsset(m, path); }
                        m.SetColor("_BaseColor", entry.color); m.SetFloat("_Smoothness", entry.smooth); m.SetFloat("_Metallic", 0f);
                        m.enableInstancing = true; EditorUtility.SetDirty(m);
                        cache[entry.key] = m;
                    }
                    shared[i] = m; changed = true; break;
                }
            }
            if (changed) { renderer.sharedMaterials = shared; count++; }
        }
        report.AppendLine("Doğa paleti: " + count + " renderer (yaprak, kabuk, taş, çimen) oyunun renklerine uyarlandı");
    }

    private static void BuildSigns()
    {
        var root = Groups["Props"];
        AddSignpost(root, P(82.2f, 30f), "BAŞLANGIÇ KAVŞAĞI",
            new[]
            {
                "KUZEY  Şehir  " + Mathf.RoundToInt(Roundabout.y - Base.y) + " m",
                "BATI  Temiz göl  " + Mathf.RoundToInt(Vector2.Distance(Base, LakeA)) + " m",
                "DOĞU  Kirli göl  " + Mathf.RoundToInt(Vector2.Distance(Base, LakeB)) + " m",
                "KUZEY  Buzul  " + Mathf.RoundToInt(StationPad.y - Base.y) + " m",
            });
        AddSignpost(root, P(82.6f, 64f), "ŞEHİR GİRİŞİ", new[] { "BATI  Rüzgârlı tepeler", "DOĞU  Sanayi", "KUZEY  Buzul patikası" });
        AddSignpost(root, P(40f, 89.6f), "KORULUK", new[] { "Hassas habitat", "Ağaç dik, yangını söndür" });
        AddSignpost(root, P(83.4f, 111.6f), "BUZUL PATİKASI", new[] { "İzleme istasyonu " + Mathf.RoundToInt(Vector2.Distance(P(83.4f, 111.6f), StationPad)) + " m" });
    }

    private static void AddSignpost(Transform parent, Vector2 xz, string title, string[] lines)
    {
        var sign = Group("Signpost", parent); sign.position = new Vector3(xz.x, GroundY(xz.x, xz.y), xz.y);
        var wood = Mat("SignWood", new Color(0.4f, 0.28f, 0.16f), 0.1f);
        Prim(PrimitiveType.Cylinder, "Post", sign, new Vector3(0f, 1.8f, 0f), new Vector3(0.22f, 1.8f, 0.22f), wood, true);
        var board = Prim(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 3.2f, 0f), new Vector3(3.6f, 1.5f + lines.Length * 0.15f, 0.12f), Mat("SignBoard", new Color(0.12f, 0.2f, 0.18f), 0.1f));
        var caption = WorldLabel(sign, title + "\n" + string.Join("\n", lines), new Vector3(0f, 3.25f, -0.2f), 0.0042f, 34);
        caption.GetComponentInParent<WorldSpaceLabel>().BindSignboard(board.transform);
    }

    // ---------------------------------------------------------------- base & HUD

    private static void PlaceBase()
    {
        GameObject alan = null, enemies = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == "alan") alan = root;
            else if (root.name == "Enemys") enemies = root;
        }
        if (alan == null) { report.AppendLine("UYARI: 'alan' grubu bulunamadı; oyuncu taşınamadı."); return; }
        Vector3 before = alan.transform.position;
        alan.transform.position = new Vector3(Base.x, GroundY(Base.x, Base.y) + 0.1f, Base.y);
        // The tutorial pockets inside the mechanic station keep priority over the big regions around them, but they are
        // small and named for what they are: the grove project must find the real grove, not a starter patch.
        foreach (var region in alan.GetComponentsInChildren<EcoRegion>(true))
        {
            region.priority = 5;
            region.size = new Vector2(Mathf.Min(region.size.x, 14f), Mathf.Min(region.size.y, 14f));
            if (region.regionName.Contains("koruluk") || region.regionName.Contains("Koruluk")) region.regionName = "Üs bahçesi";
            else if (region.regionName.Contains("sanayi") || region.regionName.Contains("Sanayi")) region.regionName = "Eski atölye sahası";
            // The flat 10 m "condition sample" mats read as stickers on the new ground; the HUD shows the state instead.
            if (region.groundVisual != null) { region.groundVisual.enabled = false; RecordOverride(region.groundVisual); region.groundVisual = null; }
            RecordOverride(region);
        }
        // The station's flat turf plate is a dark square on the new ground; the mesh ground already covers it.
        foreach (var renderer in alan.GetComponentsInChildren<MeshRenderer>(true))
            if (renderer.name == "Ground cover") { renderer.enabled = false; RecordOverride(renderer); }
        // The square starter pond becomes a round pond with a stone rim.
        foreach (var source in alan.GetComponentsInChildren<WeaponWaterSource>(true))
        {
            if (!source.name.Contains("pond")) continue;
            var flat = source.GetComponent<MeshRenderer>();
            if (flat != null) { flat.enabled = false; RecordOverride(flat); }
            var pond = Group("Pond visual", source.transform);
            pond.localPosition = Vector3.zero; pond.localRotation = Quaternion.identity;
            pond.localScale = new Vector3(1f / Mathf.Max(0.01f, source.transform.lossyScale.x), 1f, 1f / Mathf.Max(0.01f, source.transform.lossyScale.z));
            var water = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer)); water.transform.SetParent(pond, false);
            water.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            water.GetComponent<MeshFilter>().sharedMesh = DiscMesh("StarterPond_Disc", 2.3f, 4, 20);
            water.GetComponent<MeshRenderer>().sharedMaterial = WaterMaterial("EcoLakeClean", new Color(0.30f, 0.74f, 0.82f, 0.62f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var stone = Prim(PrimitiveType.Sphere, "Rim stone", pond, new Vector3(Mathf.Cos(a) * 2.45f, 0.05f, Mathf.Sin(a) * 2.45f),
                    new Vector3(F(0.45f, 0.7f), F(0.2f, 0.32f), F(0.45f, 0.7f)), Mat("StreamPebble", new Color(0.55f, 0.55f, 0.52f), 0.2f));
                stone.transform.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
            }
            EcoMapMarker.Add(pond.gameObject, "poi_fishing", "Üs göleti", "Temiz su doldurma ve deneme balıkçılığı.", EcoMapMarker.Category.Water);
        }
        report.AppendLine("Başlangıç: 'alan' grubu (oyuncu, istasyon, deneme fabrikası/yangın/kirli su) " + Base + " konumuna taşındı; deneme bölgeleri 14×14 m'ye küçültüldü");
        // Enemy placement markers were authored around the old start point; keep their offsets from the base.
        if (enemies != null)
        {
            enemies.transform.position += alan.transform.position - before;
            report.AppendLine("Düşman başlangıç noktaları 'alan' ile aynı miktarda kaydırıldı: " + (alan.transform.position - before));
        }
        foreach (var market in alan.GetComponentsInChildren<EcoMarket>(true))
            EcoMapMarker.Add(market.gameObject, "poi_market", "Atölye tezgâhı", "Alışveriş, ürün satışı ve araç geliştirmeleri [E].", EcoMapMarker.Category.Service, true);

        // Click-to-move and enemy pathing need a NavMesh volume that covers the whole map.
        var nav = Object.FindFirstObjectByType<EcoNavigation>();
        if (nav == null) nav = new GameObject("Eco navigation").AddComponent<EcoNavigation>();
        nav.transform.SetPositionAndRotation(new Vector3(Size * 0.5f, Ground0, Size * 0.5f), Quaternion.identity);
        nav.worldSize = new Vector3(Size, 40f, Size);
        EditorUtility.SetDirty(nav);
        report.AppendLine("Gezinme (NavMesh) hacmi: " + nav.worldSize + " @ " + nav.transform.position);
    }

    // Changes to objects inside a prefab instance ("alan") only survive the scene save when recorded as overrides.
    private static void RecordOverride(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void BuildHud()
    {
        var spritePath = ResFolder + "UIPanel.png";
        File.WriteAllBytes(spritePath, MakeRoundedPng(40, 11));
        AssetDatabase.ImportAsset(spritePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = new Vector4(11, 11, 11, 11); importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var panel = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);

        var mapPath = ResFolder + "Minimap.png";
        File.WriteAllBytes(mapPath, RenderMinimap().EncodeToPNG());
        AssetDatabase.ImportAsset(mapPath);
        var mapImporter = (TextureImporter)AssetImporter.GetAtPath(mapPath);
        mapImporter.textureType = TextureImporterType.Default; mapImporter.mipmapEnabled = true; mapImporter.wrapMode = TextureWrapMode.Clamp;
        mapImporter.filterMode = FilterMode.Trilinear; mapImporter.textureCompression = TextureImporterCompression.Uncompressed;
        mapImporter.maxTextureSize = 2048; mapImporter.sRGBTexture = true;
        mapImporter.SaveAndReimport();
        var minimap = AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);

        var go = new GameObject("EcoWorld HUD");
        var hud = go.AddComponent<EcoWorldHUD>();
        hud.minimapTexture = minimap; hud.panelSprite = panel; hud.worldOrigin = Vector2.zero; hud.worldSize = new Vector2(Size, Size);

        // Give the existing HUD the same rounded panels (these are its optional artwork slots).
        var existing = Object.FindFirstObjectByType<EcoQuestHUD>();
        if (existing != null)
        {
            existing.panelSprite = panel; existing.slotSprite = panel;
            EditorUtility.SetDirty(existing);
            if (PrefabUtility.IsPartOfPrefabInstance(existing)) PrefabUtility.RecordPrefabInstancePropertyModifications(existing);
        }
        report.AppendLine("Arayüz: EcoWorldHUD eklendi (harita: üstten gerçek render " + minimap.width + " px)");
    }

    private static byte[] MakeRoundedPng(int s, int r)
    {
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 0.5f)));
            }
        tex.Apply();
        var bytes = tex.EncodeToPNG(); Object.DestroyImmediate(tex);
        return bytes;
    }

    // Top-down orthographic render of the finished map (labels and particles hidden), lightly graded so it reads as a
    // printed map rather than a screenshot: warmer paper tone, crisper contrast.
    private static Texture2D RenderMinimap()
    {
        const int n = 1536;
        var hidden = new List<Behaviour>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (canvas.enabled) { canvas.enabled = false; hidden.Add(canvas); }
        var hiddenRenderers = new List<Renderer>();
        foreach (var ps in Object.FindObjectsByType<ParticleSystemRenderer>(FindObjectsSortMode.None)) if (ps.enabled) { ps.enabled = false; hiddenRenderers.Add(ps); }
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        Quaternion sunRotation = sun != null ? sun.transform.rotation : Quaternion.identity;
        float sunIntensity = sun != null ? sun.intensity : 1f;
        if (sun != null) { sun.transform.rotation = Quaternion.Euler(58f, -35f, 0f); sun.intensity = Mathf.Max(1f, sunIntensity); }

        var camGo = new GameObject("Minimap camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = Size * 0.5f;
        cam.transform.SetPositionAndRotation(new Vector3(Size * 0.5f, 220f, Size * 0.5f), Quaternion.Euler(90f, 0f, 0f));
        cam.nearClipPlane = 1f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.3f, 0.22f); cam.aspect = 1f;
        var rt = new RenderTexture(n, n, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        Texture2D tex;
        try
        {
            cam.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, n, n), 0, 0); tex.Apply();
            RenderTexture.active = previous;
        }
        finally
        {
            cam.targetTexture = null; Object.DestroyImmediate(camGo); rt.Release(); Object.DestroyImmediate(rt);
            foreach (var b in hidden) if (b != null) b.enabled = true;
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            RenderSettings.fog = fog;
            if (sun != null) { sun.transform.rotation = sunRotation; sun.intensity = sunIntensity; }
        }
        var pixels = tex.GetPixels();
        float brightness = 0f;
        for (int i = 0; i < pixels.Length; i += 97) brightness += pixels[i].grayscale;
        brightness /= pixels.Length / 97f;
        if (brightness < 0.03f) { report.AppendLine("UYARI: harita render'ı boş geldi; boyalı harita kullanıldı"); return PaintedMinimap(); }
        var paper = new Color(0.98f, 0.95f, 0.86f);
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            float l = c.grayscale;
            c = Color.Lerp(new Color(l, l, l), c, 1.12f);                // a touch more saturation
            c = new Color(Mathf.Pow(c.r, 0.92f), Mathf.Pow(c.g, 0.92f), Mathf.Pow(c.b, 0.92f)); // lift shadows
            c = Color.Lerp(c, c * paper, 0.35f); c.a = 1f;
            pixels[i] = c;
        }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    // Fallback when the camera cannot render in this editor session: paint the ground colours directly.
    private static Texture2D PaintedMinimap()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color[n * n];
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float x = (ix + 0.5f) / n * Size, z = (iz + 0.5f) / n * Size;
                int i = Mathf.Clamp(Mathf.RoundToInt((x - Origin) / Cell), 0, N), j = Mathf.Clamp(Mathf.RoundToInt((z - Origin) / Cell), 0, N);
                var c = GroundColor(i, j);
                if (GroundY(x, z) < WaterY) c = Vector2.Distance(new Vector2(x, z), LakeB) < 20f ? new Color(0.40f, 0.44f, 0.24f) : new Color(0.30f, 0.62f, 0.78f);
                if (RoadEdgeDistance(x, z, false) < 0f) c = new Color(0.3f, 0.3f, 0.32f);
                c.a = 1f; pixels[iz * n + ix] = c;
            }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    private static void RegisterInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        int index = scenes.FindIndex(s => s.path == TargetScene);
        // Re-create the entry so its GUID matches the scene asset. Unity keeps a cached GUID when a path is replaced
        // in one assignment, so the entry is removed first and then inserted again at the same position.
        if (index >= 0)
        {
            bool enabled = scenes[index].enabled; scenes.RemoveAt(index);
            EditorBuildSettings.scenes = scenes.ToArray();
            scenes.Insert(index, new EditorBuildSettingsScene(TargetScene, enabled));
            EditorBuildSettings.scenes = scenes.ToArray(); return;
        }
        scenes.Add(new EditorBuildSettingsScene(TargetScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        report.AppendLine("Build Settings: EcoWorld sahnesi listeye eklendi (diğer sahnelerin sırası değişmedi).");
    }
}
