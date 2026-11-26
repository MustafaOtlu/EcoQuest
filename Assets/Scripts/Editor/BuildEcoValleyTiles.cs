using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run phase 2 (GDD v4 §21): the map is cut into 50 × 50 m region tiles that the runtime EcoValleyAssembler arranges
// per run. Each tile is built with the same ground / road / content code as the old single map, at its "home" slot,
// with three rules that let any tile sit next to any other:
//  * every shared edge is pinned to the same height (Ground0 for the 3 × 3 grid, the glacier slope only inside it),
//  * every edge midpoint has a straight 8 m country-road stub ("Bağlantı …") that meets the neighbour's stub,
//  * ground colour fades to one neutral grass within 3 m of an edge.
// The border ring (hills, forest, walls), the glacier row, the base plain and the town are fixed tiles.
// Output: Assets/Resources/EcoTiles/Tile_*.prefab, their meshes in Assets/EcoTiles/Meshes/<tile>/ and a top-down map
// image per tile in Assets/Resources/EcoTiles/Maps/ (the runtime puts the HUD map together from them).
public static partial class BuildEcoWorldMap
{
    private const string TilePrefabFolder = "Assets/Resources/EcoTiles/";
    private const string TileMapFolder = "Assets/Resources/EcoTiles/Maps/";
    private const string TileMeshRoot = "Assets/EcoTiles/Meshes/";
    private const float TileCell = 50f / 34f;  // 34 cells per tile: ground chunks line up with tile edges
    private const float SeamBlend = 8f, SeamColour = 3f;

    private static bool tileMode, TileIsBorder, tilePinAll;
    private static Rect tileRect;
    private static System.Func<float, float, float> tileHeight;
    private static Vector2 IndustryCenter = new Vector2(128f, 80f), IndustryHalf = new Vector2(21f, 25f);
    private static float CivicAlong = 9f, GlacierFloeStart = 122f;
    private static Vector2 InletPoint = new Vector2(135.4f, 31.4f);
    private static readonly Vector2 Far = new Vector2(-5000f, -5000f);

    private sealed class TileSpec
    {
        public string id;
        public Rect rect;
        public bool border, pinAll = true;
        public System.Func<float, float, float> height;
        public System.Action setup;      // statics, Defs and Roads
        public System.Action content;    // tile-specific objects
        public bool island, facades;
    }

    [MenuItem("EcoQuest/Valley/Build region tiles")]
    public static void BuildValleyTiles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("Önce Play modundan çık."); return; }
        EditorSceneManager.SaveOpenScenes();
        var started = System.DateTime.Now;
        var log = new StringBuilder("EcoValley karo raporu\n");
        try
        {
            Progress("Eski haritadan modeller", 0.02f);
            HarvestModels();
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            // Old map content (if any) must not show up in the tile renders or collide with lamp placement checks.
            foreach (var go in scene.GetRootGameObjects()) if (go.name == "EcoWorld") go.SetActive(false);
            Directory.CreateDirectory(TilePrefabFolder); Directory.CreateDirectory(TileMapFolder); Directory.CreateDirectory(TileMeshRoot);
            Directory.CreateDirectory(Folder + "Materials");
            AssetDatabase.Refresh();

            var specs = TileSpecs();
            for (int i = 0; i < specs.Count; i++)
            {
                Progress("Karo: " + specs[i].id, 0.05f + 0.9f * i / specs.Count);
                log.Append(BuildTile(specs[i]));
            }
            log.AppendLine("Süre: " + (System.DateTime.Now - started).TotalSeconds.ToString("0") + " sn");
            File.WriteAllText("Assets/EcoTiles/TileReport.txt", log.ToString());
            Debug.Log("ECOVALLEY_TILES_READY\n" + log);
        }
        catch (System.Exception ex) { Debug.LogError("EcoValley karoları oluşturulamadı: " + ex); }
        finally
        {
            tileMode = false; TileIsBorder = false; Cell = 1.5f; Border = 25.5f; MeshDir = Folder + "Meshes/";
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            // Throw away the temporary build objects: reopen the scene as saved.
            EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        }
    }

    // House, lamp, pole and solar prefabs come from the original MainScene groups, as for the full map build.
    private static void HarvestModels()
    {
        var main = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        var roots = main.GetRootGameObjects();
        GameObject Find(string part) => roots.FirstOrDefault(r => r != null && r.name.Contains(part));
        housePrefab = HarvestPrefab(Find("Evler")); lampPrefab = HarvestPrefab(Find("Lamba"));
        polePrefab = HarvestPrefab(Find("Diregi")); solarPrefab = HarvestPrefab(Find("Dizisi"));
    }

    private static string BuildTile(TileSpec spec)
    {
        report = new StringBuilder("== " + spec.id + "\n");
        rng = new System.Random(20261020 + spec.id.Aggregate(17, (h, c) => h * 31 + c));
        KeepOut.Clear(); Groups.Clear(); MaterialCache.Clear(); Crossings.Clear();
        tileMode = true; TileIsBorder = spec.border; tilePinAll = spec.pinAll; tileRect = spec.rect; tileHeight = spec.height;
        Cell = TileCell; Border = 50f;
        // Statics that the content code reads; each tile sets the ones it owns.
        Base = Far; Roundabout = Far; LakeA = Far; LakeB = Far; StationPad = new Vector2(96f, 133f); HydroPad = Far; TankFarm = new Rect(Far, Vector2.one);
        IndustryCenter = Far; IndustryHalf = Vector2.one; CivicAlong = 4.5f; GlacierFloeStart = 127f; InletPoint = Far;
        Defs.Clear(); Roads.Clear();
        spec.setup?.Invoke();

        MeshDir = TileMeshRoot + spec.id + "/";
        if (AssetDatabase.IsValidFolder(MeshDir.TrimEnd('/'))) AssetDatabase.DeleteAsset(MeshDir.TrimEnd('/'));
        Directory.CreateDirectory(MeshDir); AssetDatabase.Refresh();

        worldRoot = new GameObject(spec.id).transform;
        foreach (var groupName in new[] { "Ground", "Roads", "Regions", "Lakes", "City", "Industry", "Energy", "Glacier", "Vegetation", "Props", "Bounds" })
        {
            var t = new GameObject(groupName).transform; t.SetParent(worldRoot, false); Groups[groupName] = t;
        }
        ComputeHeights();
        BuildGround();
        if (Roads.Count > 0) BuildRoads(spec.island);
        if (spec.border) BuildBounds();
        BuildRegions();
        spec.content?.Invoke();
        if (spec.facades) DetailBuildingFacades(worldRoot);
        BuildVegetation();
        FixBuiltinMaterials();
        // Empty groups only add noise to the hierarchy.
        foreach (var pair in Groups) if (pair.Value.childCount == 0 && pair.Value.GetComponents<Component>().Length == 1) Object.DestroyImmediate(pair.Value.gameObject);

        if (!spec.border) SaveTileMap(spec);
        string path = TilePrefabFolder + spec.id + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(worldRoot.gameObject, path);
        Object.DestroyImmediate(worldRoot.gameObject);
        worldRoot = null;
        return report.ToString();
    }

    // ---------------------------------------------------------------- heights and seams

    /// <summary>The height every tile edge agrees on: flat valley floor, rising to the glacier in the north.</summary>
    private static float ValleyBase(float x, float z) => Ground0 + GlacierRise(z) * (5.5f + (Fbm(x * 0.07f, z * 0.07f, 3) - 0.5f) * 2.5f);

    private static float EdgeDistance(float x, float z)
    {
        if (TileIsBorder) return Outside(x, z);
        if (!tilePinAll) return z - tileRect.yMin;
        return Mathf.Min(Mathf.Min(x - tileRect.xMin, tileRect.xMax - x), Mathf.Min(z - tileRect.yMin, tileRect.yMax - z));
    }

    private static float SeamWeight(float x, float z)
    {
        if (TileIsBorder) return Outside(x, z) > 0f ? 1f : 0f;
        if (!tileRect.Contains(new Vector2(x, z))) return 0f;
        return Smooth(0f, SeamBlend, EdgeDistance(x, z));
    }

    private static float TileRawHeight(float x, float z)
    {
        float b = ValleyBase(x, z);
        if (TileIsBorder)
        {
            float outside = Outside(x, z);
            if (outside <= 0f) return b;
            float h = b + Smooth(0f, 18f, outside) * (5f + Fbm(x * 0.05f + 5f, z * 0.05f, 3) * 16f);
            float north = Smooth(Size, Size + 22f, z);
            float ridge = 1f - Mathf.Abs(2f * Fbm(x * 0.03f + 11f, z * 0.03f + 5f, 4) - 1f);
            return h + north * (14f + ridge * ridge * 36f);
        }
        float w = SeamWeight(x, z);
        return w <= 0f ? b : Mathf.Lerp(b, tileHeight(x, z), w);
    }

    private static void PinRoadField()
    {
        for (int i = 0; i <= N; i++)
            for (int j = 0; j <= N; j++)
            {
                float x = VX(i), z = VX(j);
                roadField[i, j] = Mathf.Lerp(ValleyBase(x, z), roadField[i, j], SeamWeight(x, z));
            }
    }

    private static readonly Color SeamGrass = new Color(0.365f, 0.575f, 0.23f);

    private static Color TileSeamColor(float x, float z, Color c)
    {
        float d = EdgeDistance(x, z);
        if (TileIsBorder) return d <= 0f ? c : Color.Lerp(SeamGrass, c, Smooth(0f, SeamColour, d));
        if (!tileRect.Contains(new Vector2(x, z))) return c;
        if (!tilePinAll && z > tileRect.yMin + SeamColour) return c;
        return Color.Lerp(SeamGrass, c, Smooth(0f, SeamColour, d));
    }

    private static bool TileOwnsChunk(int cx, int cz)
    {
        float x0 = Origin + cx * ChunkCells * Cell, z0 = Origin + cz * ChunkCells * Cell;
        var centre = new Vector2(x0 + ChunkCells * Cell * 0.5f, z0 + ChunkCells * Cell * 0.5f);
        if (TileIsBorder) return Outside(centre.x, centre.y) > 0f;
        return tileRect.Contains(centre);
    }

    private static bool NearTileEdge(Vector2 p) => !TileIsBorder && Mathf.Min(Mathf.Min(p.x - tileRect.xMin, tileRect.xMax - p.x), Mathf.Min(p.y - tileRect.yMin, tileRect.yMax - p.y)) < 2.5f;

    private static bool InTile(float x, float z, float margin)
        => !TileIsBorder && x > tileRect.xMin + margin && x < tileRect.xMax - margin && z > tileRect.yMin + margin && z < tileRect.yMax - margin;

    private static float Noise(float x, float z, float amount) => Ground0 + (Fbm(x * 0.035f, z * 0.035f, 3) - 0.5f) * amount;

    // ---------------------------------------------------------------- roads helpers

    private static RoadDef Stub(string dir, float ex, float ez, float ix, float iz, float length = 8f)
    {
        var edge = new Vector2(ex, ez); var inward = new Vector2(ix, iz);
        return new RoadDef("Bağlantı " + dir, RoadKind.Country, edge, edge + inward * length);
    }

    private static void FourStubs(Rect r)
    {
        Vector2 c = r.center;
        Roads.Add(Stub("G", c.x, r.yMin, 0f, 1f)); Roads.Add(Stub("K", c.x, r.yMax, 0f, -1f));
        Roads.Add(Stub("B", r.xMin, c.y, 1f, 0f)); Roads.Add(Stub("D", r.xMax, c.y, -1f, 0f));
    }

    private static RoadDef Loop(string name, RoadKind kind, Vector2 centre, float radius, int points = 24)
    {
        var road = new RoadDef(name, kind) { closed = true };
        var circle = new Vector2[points];
        for (int i = 0; i < points; i++)
        {
            float a = i * Mathf.PI * 2f / points;
            circle[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
        }
        road.points = circle;
        return road;
    }

    private static RegionDef RegionOf(EcoRegionInfo.Kind kind, string name, string key, Rect area, int project, string tagline, string[] features, Color color,
        float air, float water, float soil, float carbon, float temperature, float vegetation, float habitat, float wind, float sun, bool sensitive = false)
        => new RegionDef { name = name, key = key, kind = kind, center = area.center, size = area.size, project = project, tagline = tagline, features = features,
            color = color, air = air, water = water, soil = soil, carbon = carbon, temperature = temperature, vegetation = vegetation, habitat = habitat,
            wind = wind, sun = sun, sensitive = sensitive };

    // ---------------------------------------------------------------- the tiles

    private static List<TileSpec> TileSpecs()
    {
        var list = new List<TileSpec>();

        list.Add(new TileSpec { id = "Tile_Border", border = true, rect = new Rect(-50f, -50f, 250f, 250f), height = ValleyBase });

        // ---- glacier row (fixed, 150 × 50 m)
        var glacier = new Rect(0f, 100f, 150f, 50f);
        list.Add(new TileSpec
        {
            id = "Tile_Glacier", rect = glacier, pinAll = false,
            height = (x, z) => Mathf.Lerp(ValleyBase(x, z), stationPadHeight, CircleMask(x, z, StationPad, 6.5f, 5f)),
            setup = () =>
            {
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Glacier, "Buzul Dağı", "Buzul Bölgesi", glacier, 5,
                    "Erime riski altındaki buzul. Dünya karbonu arttıkça buz çizgisi geri çekilir.",
                    new[] { "İzleme istasyonu: dünya karbonunu ölçer", "Patikalar vadiden kuzeye çıkar", "Soğuk: ekime uygun değil" },
                    new Color(0.72f, 0.90f, 1.00f), 2, 2, 0, 6, -3f, 0.05f, 0.4f, 1.4f, 0.9f, true));
                foreach (var (n, x) in new[] { ("G1", 25f), ("G2", 75f), ("G3", 125f) }) Roads.Add(Stub(n, x, 100f, 0f, 1f));
                Roads.Add(new RoadDef("Buzul Patikası", RoadKind.Trail, P(75, 108), P(80, 115), P(86, 123), P(91.6f, 128.6f)));
                Roads.Add(new RoadDef("Batı Buzul Patikası", RoadKind.Trail, P(25, 108), P(33, 115), P(46, 120), P(62, 123), P(76, 124.5f), P(86, 123)));
                Roads.Add(new RoadDef("Doğu Buzul Patikası", RoadKind.Trail, P(125, 108), P(119, 116), P(108, 123), P(99, 127), P(93.6f, 128.8f)));
            },
            content = () =>
            {
                BuildGlacier();
                AddSignpost(Groups["Props"], P(80.4f, 106.5f), "BUZUL PATİKASI", new[] { "İzleme istasyonu " + Mathf.RoundToInt(Vector2.Distance(P(80.4f, 106.5f), StationPad)) + " m" });
            },
        });

        // ---- base plain (fixed, bottom centre)
        var sun = new Rect(50f, 0f, 50f, 50f);
        list.Add(new TileSpec
        {
            id = "Tile_Sun", rect = sun,
            height = (x, z) =>
            {
                float h = Noise(x, z, 1.4f);
                h = Mathf.Lerp(h, Ground0, RectMask(x, z, 75f, 30f, 18f, 14f, 8f) * 0.85f);
                return Mathf.Lerp(h, Ground0, CircleMask(x, z, Base, 17f, 8f));
            },
            setup = () =>
            {
                Base = new Vector2(75f, 16f);
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Sun, "Güneş Ovası", "Güneşli Çayır", sun, 0,
                    "Geniş açık ova. İlk güneş üretimi ve küçük tarım için ideal.",
                    new[] { "Güneş paneli verimi yüksek", "Başlangıç üssü ve atölye", "Ilık: domates ve lahana" },
                    new Color(1.00f, 0.84f, 0.38f), 5, 8, 10, 10, 20f, 0.50f, 0.7f, 0.8f, 1.15f));
                Roads.Add(Stub("K", 75f, 50f, 0f, -1f)); Roads.Add(Stub("B", 50f, 25f, 1f, 0f)); Roads.Add(Stub("D", 100f, 25f, -1f, 0f));
                Roads.Add(new RoadDef("Şehir Yolu", RoadKind.Country, P(75, 34), P(75, 38), P(75, 42)));
                Roads.Add(new RoadDef("Batı Göl Yolu", RoadKind.Country, P(75, 34), P(68, 33), P(62, 28), P(60, 25.5f), P(58, 25)));
                Roads.Add(new RoadDef("Doğu Göl Yolu", RoadKind.Country, P(75, 34), P(82, 33), P(88, 28), P(90, 25.5f), P(92, 25)));
                Roads.Add(new RoadDef("Üs Patikası", RoadKind.Trail, P(75, 30.4f), P(77, 26.5f), P(78.6f, 22.8f), P(79.4f, 20.2f)));
            },
            content = () => AddSignpost(Groups["Props"], P(82.2f, 30f), "BAŞLANGIÇ KAVŞAĞI", new[] { "KUZEY  Şehir", "BATI  Göl", "DOĞU  Göl" }),
        });

        // ---- town (fixed, centre)
        var city = new Rect(50f, 50f, 50f, 50f);
        list.Add(new TileSpec
        {
            id = "Tile_City", rect = city, island = true, facades = true,
            height = (x, z) => Ground0 + 0.3f,
            setup = () =>
            {
                Roundabout = new Vector2(75f, 75f);
                Defs.Add(RegionOf(EcoRegionInfo.Kind.City, "Şehir", "Şehir Merkezi", city, 4,
                    "Ev, pazar ve meydan. Güvenli hazırlık alanı; çocuklar burada büyüyor.",
                    new[] { "Ev ve mahalle çocuğu", "Göbekli kavşak ve meydan", "Çevresel tehdit oluşmaz" },
                    new Color(0.95f, 0.84f, 0.55f), 20, 22, 20, 28, 18.5f, 0.25f, 0.3f, 0.6f, 0.85f));
                FourStubs(city);
                Roads.Add(new RoadDef("Ana Cadde (güney)", RoadKind.Avenue, P(75, 58), P(75, 62.5f), P(75, 67.4f)));
                Roads.Add(new RoadDef("Ana Cadde (kuzey)", RoadKind.Avenue, P(75, 82.6f), P(75, 87), P(75, 92)));
                Roads.Add(new RoadDef("Batı Bulvarı", RoadKind.Street, P(67.4f, 75), P(62, 75), P(58, 75)));
                Roads.Add(new RoadDef("Doğu Bulvarı", RoadKind.Street, P(82.6f, 75), P(88, 75), P(92, 75)));
                // A pinwheel of short side streets towards the corners, as in the old town.
                Roads.Add(new RoadDef("Çınar Sokak", RoadKind.Street, P(75, 90), P(69, 90.5f), P(62, 92.5f), P(56.5f, 95)));
                Roads.Add(new RoadDef("Lale Sokak", RoadKind.Street, P(75, 60), P(81, 59.5f), P(88, 57.5f), P(93.5f, 55)));
                Roads.Add(new RoadDef("Okul Sokağı", RoadKind.Street, P(60, 75), P(59.5f, 69), P(57.5f, 62), P(55, 56.5f)));
                Roads.Add(new RoadDef("Fırın Sokağı", RoadKind.Street, P(90, 75), P(90.5f, 81), P(92.5f, 88), P(95, 93.5f)));
                var ring = Loop("Göbekli Kavşak", RoadKind.Ring, Roundabout, RoundaboutR);
                Roads.Add(ring);
            },
            content = () =>
            {
                BuildCity();
                AddSignpost(Groups["Props"], P(80.6f, 56f), "ŞEHİR", new[] { "Pazar, ev, kütüphane", "Sağlık ocağı" });
            },
        });

        // ---- movable tiles
        var bl = new Rect(0f, 0f, 50f, 50f); var br = new Rect(100f, 0f, 50f, 50f);
        var ml = new Rect(0f, 50f, 50f, 50f); var mr = new Rect(100f, 50f, 50f, 50f);

        list.Add(new TileSpec
        {
            id = "Tile_Lake", rect = bl,
            height = (x, z) => Mathf.Lerp(LakeShape(Noise(x, z, 1.4f), x, z, LakeA, LakeAR), Ground0 - 0.4f, CircleMask(x, z, HydroPad, 3.2f, 2f)),
            setup = () =>
            {
                LakeA = new Vector2(25f, 25f); LakeAR = 9.5f; HydroPad = new Vector2(17.6f, 32.4f);
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Lake, "Balık Gölü", "Batı Gölü", bl, -1,
                    "Temiz tatlı su ve balık. Su sınırlıdır, aşırı avlama.",
                    new[] { "Temiz su ve balıkçılık", "Kaynak deresi: küçük hidro kurulabilir", "Hassas kıyı" },
                    new Color(0.45f, 0.74f, 1.00f), 4, 8, 6, 6, 18f, 0.70f, 0.85f, 1.1f, 1f, true));
                FourStubs(bl);
                Roads.Add(Loop("Göl Yolu", RoadKind.Country, LakeA, 17f));
                Roads.Add(new RoadDef("Kaynak Deresi", RoadKind.Stream, P(15.5f, 34.5f), P(17.2f, 32.8f), P(18.9f, 31.1f)));
            },
            content = () => BuildLake("Balık Gölü", LakeA, LakeAR, false, new Vector2(0.25f, -1f)),
        });

        list.Add(new TileSpec
        {
            id = "Tile_DirtyLake", rect = br,
            height = (x, z) => LakeShape(Noise(x, z, 1.2f), x, z, LakeB, LakeBR),
            setup = () =>
            {
                LakeB = new Vector2(125f, 25f); LakeBR = 9f; InletPoint = new Vector2(131.2f, 31.2f);
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Lake, "Fabrika Gölü", "Doğu Gölü", br, 1,
                    "Fabrikaların atık kanalının döküldüğü göl. Giriş kapanmalı, su arıtılmalı.",
                    new[] { "Kirletici giriş kanalı", "Salya görülebilir", "Pompa + arıtma + depo ile göle temiz dönüş" },
                    new Color(0.58f, 0.62f, 0.40f), 22, 64, 35, 22, 18f, 0.30f, 0.1f, 1f, 1f));
                FourStubs(br);
                Roads.Add(Loop("Göl Yolu", RoadKind.Country, LakeB, 17f));
                Roads.Add(new RoadDef("Kirli Kanal", RoadKind.DirtyStream, P(134.3f, 34.3f), P(132.5f, 32.5f), P(130.6f, 30.6f)));
            },
            content = () =>
            {
                BuildLake("Fabrika Gölü", LakeB, LakeBR, true, new Vector2(-0.25f, -1f));
                var pipe = Group("Atık su çıkışı", Groups["Industry"]);
                Vector2 o = P(134.8f, 34.8f);
                pipe.position = new Vector3(o.x, GroundY(o.x, o.y) + 0.6f, o.y);
                pipe.rotation = Quaternion.Euler(0f, 45f, 0f);
                var body = Prim(PrimitiveType.Cylinder, "Pipe", pipe, Vector3.zero, new Vector3(1.1f, 1.6f, 1.1f), Mat("PipeSteel", new Color(0.45f, 0.48f, 0.5f), 0.4f, 0.5f), true);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                EcoMapMarker.Add(pipe.gameObject, "poi_inlet", "Atık su çıkışı", "Fabrikaların kirli suyu bu kanalla Fabrika Gölü'ne iniyor.", EcoMapMarker.Category.Danger);
            },
        });

        list.Add(new TileSpec
        {
            id = "Tile_Industry", rect = mr, facades = true,
            height = (x, z) => Mathf.Lerp(Noise(x, z, 1f), Ground0 - 0.1f, RectMask(x, z, 125f, 75f, 20f, 20f, 6f)),
            setup = () =>
            {
                IndustryCenter = new Vector2(125f, 75f); IndustryHalf = new Vector2(21f, 21f);
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Industry, "Fabrikalar", "Sanayi Bölgesi", mr, 3,
                    "Filtresiz fabrikalar. Hava ve karbon kirliliğinin ana kaynağı.",
                    new[] { "Fabrikalara büyük filtre takılabilir", "Atık su kanalı Fabrika Gölü'ne iner", "Teneke ve Duman görülebilir" },
                    new Color(0.90f, 0.48f, 0.38f), 66, 55, 58, 58, 16f, 0.10f, 0f, 0.9f, 0.7f));
                FourStubs(mr);
                Roads.Add(new RoadDef("Fabrika Caddesi", RoadKind.Industrial, P(108, 75), P(125, 75), P(142, 75)));
                Roads.Add(new RoadDef("Depo Yolu", RoadKind.Industrial, P(125, 58), P(125, 66), P(125, 75)));
                Roads.Add(new RoadDef("Fabrika Yolu", RoadKind.Industrial, P(125, 75), P(125, 84), P(125, 92)));
            },
            content = BuildIndustryTile,
        });

        list.Add(new TileSpec
        {
            id = "Tile_Wind", rect = ml,
            height = (x, z) =>
            {
                float h = Noise(x, z, 1.4f);
                float wm = RectMask(x, z, 25f, 75f, 15f, 15f, 9f);
                return h + wm * Mathf.Max(0f, Fbm(x * 0.05f + 3f, z * 0.05f + 8f, 3) - 0.36f) * 10f;
            },
            setup = () =>
            {
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Wind, "Rüzgâr Tepeleri", "Rüzgârlı Tepeler", ml, -1,
                    "Açık tepeler. Rüzgâr türbinleri için en verimli yer.",
                    new[] { "Rüzgâr türbini verimi yüksek", "Tepeler arasında yangın çabuk yayılır" },
                    new Color(0.45f, 0.82f, 0.74f), 6, 6, 6, 8, 15f, 0.65f, 0.8f, 1.6f, 0.95f));
                FourStubs(ml);
                Roads.Add(new RoadDef("Tepe Yolu", RoadKind.Country, P(8, 75), P(15, 73), P(25, 72), P(35, 73), P(42, 75)));
                Roads.Add(new RoadDef("Sırt Yolu", RoadKind.Country, P(25, 58), P(23.5f, 64), P(25, 72), P(26.5f, 82), P(25, 92)));
            },
            content = BuildWindFarm,
        });

        list.Add(new TileSpec
        {
            id = "Tile_Grove", rect = ml,
            height = (x, z) => Noise(x, z, 2.2f),
            setup = () =>
            {
                Defs.Add(RegionOf(EcoRegionInfo.Kind.Grove, "Orman", "Koruluk", ml, 2,
                    "Zarar görmüş ağaçlar ve sessiz bir orman. Kuşlar geri dönmeyi bekliyor.",
                    new[] { "Ağaç tohumu ekmek için ideal", "Hassas habitat: büyük tesis kurma", "Kuş ve kelebekler habitatla döner" },
                    new Color(0.36f, 0.66f, 0.36f), 8, 8, 22, 14, 16f, 0.34f, 0.28f, 0.7f, 0.7f, true));
                FourStubs(ml);
                Roads.Add(Loop("Orman Patikası", RoadKind.Trail, new Vector2(25f, 75f), 17f));
            },
            content = () => AddSignpost(Groups["Props"], P(30.5f, 59.5f), "ORMAN", new[] { "Hassas habitat", "Ağaç dik, yangını söndür" }),
        });
        return list;
    }

    // Factories, depot, tanks, containers and the dump site, laid out around the crossroads of the industry tile.
    private static void BuildIndustryTile()
    {
        var root = Groups["Industry"];
        BuildFactory(root, "Fabrika A", P(112, 87), 0f, new Vector3(16f, 9f, 9f), 2, true);
        BuildFactory(root, "Fabrika B", P(138, 87), 0f, new Vector3(18f, 8f, 7f), 2, true);
        BuildFactory(root, "Depo C", P(111, 62), 90f, new Vector3(14f, 6f, 9f), 1, false);

        var tankMat = Mat("TankSteel", new Color(0.62f, 0.66f, 0.68f), 0.5f, 0.5f);
        var tanks = Group("Tank farm", root);
        for (int i = 0; i < 4; i++)
        {
            float x = 133f + (i % 2) * 6f, z = 59.5f + (i / 2) * 6.5f;
            var t = Prim(PrimitiveType.Cylinder, "Tank", tanks, new Vector3(x, GroundY(x, z) + 2.6f, z), new Vector3(4.6f, 2.6f, 4.6f), tankMat, true);
            Prim(PrimitiveType.Cylinder, "Tank cap", t.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), Mat("TankCap", new Color(0.3f, 0.33f, 0.35f), 0.3f, 0.5f));
        }
        KeepOut.Add(new Rect(130f, 56.5f, 12f, 12.5f));

        var yard = Group("Container yard", root);
        Color[] colors = { new Color(0.75f, 0.25f, 0.2f), new Color(0.2f, 0.4f, 0.6f), new Color(0.8f, 0.6f, 0.2f), new Color(0.25f, 0.5f, 0.35f) };
        for (int i = 0; i < 8; i++)
        {
            float x = 145.6f + (i % 2) * 2.2f - 1.1f, z = 56.5f + (i / 2) * 4.6f;
            if (RoadEdgeDistance(x, z) < 1.4f) continue;
            int stack = 1 + rng.Next(3);
            for (int s = 0; s < stack; s++)
                Prim(PrimitiveType.Cube, "Container", yard, new Vector3(x, GroundY(x, z) + 0.9f + s * 1.8f, z), new Vector3(1.8f, 1.75f, 4.2f),
                    Mat("Container" + (i % 4), colors[i % 4], 0.3f, 0.3f), true);
        }
        KeepOut.Add(new Rect(143f, 54f, 6f, 18f));

        var junk = Group("Dump site", root);
        int dumped = 0;
        foreach (var name in new[] { "MetalScrap", "PlasticScrap", "OpenBox", "StorageBarrel", "SupplyBox" })
        {
            var prefab = Resources.Load<GameObject>("EcoArt/" + name);
            if (prefab == null) continue;
            for (int i = 0; i < 5; i++)
            {
                float x = F(116.5f, 121.5f), z = F(55f, 70f);
                if (RoadEdgeDistance(x, z) < 1.5f) continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, junk);
                item.transform.position = new Vector3(x, GroundY(x, z) + 0.05f, z); item.transform.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
                dumped++;
            }
        }
        if (dumped > 0) EcoMapMarker.Add(junk.gameObject, "scrap", "Atık sahası", "Geri dönüşüm aracıyla metal ve plastik topla.", EcoMapMarker.Category.Industry);
        AddSignpost(Groups["Props"], P(118.5f, 77.8f), "FABRİKALAR", new[] { "Büyük filtre: fabrikada [E]", "Atık sahası güneyde" });
        report.AppendLine("Sanayi karosu: 2 fabrika + depo, tanklar, konteynerler, atık sahası");
    }

    // Decorative turbines on the highest crests of the windy hills, at least 15 m apart.
    private static void BuildWindFarm()
    {
        var root = Groups["Energy"];
        var wind = Defs[0];
        var candidates = new List<(Vector2 p, float h)>();
        for (float x = wind.center.x - wind.size.x * 0.5f + 7f; x < wind.center.x + wind.size.x * 0.5f - 7f; x += 3f)
            for (float z = wind.center.y - wind.size.y * 0.5f + 7f; z < wind.center.y + wind.size.y * 0.5f - 7f; z += 3f)
            {
                if (RoadEdgeDistance(x, z) < 6f || Steepness(x, z) > 18f) continue;
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
        AddSignpost(Groups["Props"], P(30.5f, 59.5f), "RÜZGÂR TEPELERİ", new[] { "Türbin için en iyi rüzgâr", "Kuru otlar: yangına dikkat" });
        report.AppendLine("Rüzgâr karosu: " + chosen.Count + " dekoratif türbin");
    }

    // ---------------------------------------------------------------- map image

    private static void SaveTileMap(TileSpec spec)
    {
        const float ppm = 1536f / 150f;
        int w = Mathf.RoundToInt(spec.rect.width * ppm), h = Mathf.RoundToInt(spec.rect.height * ppm);
        var tex = RenderArea(spec.rect, w, h, spec.id == "Tile_Sun");
        string path = TileMapFolder + spec.id + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default; importer.isReadable = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.npotScale = TextureImporterNPOTScale.None;
        importer.wrapMode = TextureWrapMode.Clamp; importer.sRGBTexture = true; importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
    }

    // Top-down orthographic render of one tile: only the tile itself (plus the base for the base plain) is drawn.
    private static Texture2D RenderArea(Rect area, int w, int h, bool withBase)
    {
        var hiddenRenderers = new List<Renderer>();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (!r.enabled) continue;
            bool own = r.transform.IsChildOf(worldRoot);
            bool baseStation = withBase && r.transform.root.name == "alan" && !(r is SkinnedMeshRenderer);
            if ((own || baseStation) && !(r is ParticleSystemRenderer)) continue;
            r.enabled = false; hiddenRenderers.Add(r);
        }
        var hidden = new List<Behaviour>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) if (canvas.enabled) { canvas.enabled = false; hidden.Add(canvas); }
        bool fog = RenderSettings.fog; RenderSettings.fog = false;
        Light sunLight = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        Quaternion sunRotation = sunLight != null ? sunLight.transform.rotation : Quaternion.identity;
        float sunIntensity = sunLight != null ? sunLight.intensity : 1f;
        if (sunLight != null) { sunLight.transform.rotation = Quaternion.Euler(58f, -35f, 0f); sunLight.intensity = Mathf.Max(1f, sunIntensity); }
        var camGo = new GameObject("Tile map camera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = area.height * 0.5f; cam.aspect = area.width / area.height;
        cam.transform.SetPositionAndRotation(new Vector3(area.center.x, 220f, area.center.y), Quaternion.Euler(90f, 0f, 0f));
        cam.nearClipPlane = 1f; cam.farClipPlane = 400f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.2f, 0.3f, 0.22f);
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        Texture2D tex;
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            cam.Render();
            var previous = RenderTexture.active; RenderTexture.active = rt;
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = previous;
        }
        finally
        {
            cam.targetTexture = null; Object.DestroyImmediate(camGo); rt.Release(); Object.DestroyImmediate(rt);
            foreach (var b in hidden) if (b != null) b.enabled = true;
            foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
            RenderSettings.fog = fog; ShaderUtil.allowAsyncCompilation = async;
            if (sunLight != null) { sunLight.transform.rotation = sunRotation; sunLight.intensity = sunIntensity; }
        }
        var pixels = tex.GetPixels();
        var paper = new Color(0.98f, 0.95f, 0.86f);
        for (int i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i]; float l = c.grayscale;
            c = Color.Lerp(new Color(l, l, l), c, 1.12f);
            c = new Color(Mathf.Pow(c.r, 0.92f), Mathf.Pow(c.g, 0.92f), Mathf.Pow(c.b, 0.92f));
            c = Color.Lerp(c, c * paper, 0.35f); c.a = 1f;
            pixels[i] = c;
        }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- scene

    /// <summary>Empties the scene's old single-map "EcoWorld" root (it stays as the placeholder the assembler fills)
    /// and adds the valley assembler. Run once after the tiles exist.</summary>
    [MenuItem("EcoQuest/Valley/Switch scene to tile valley")]
    public static void SwitchSceneToTiles()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != TargetScene) { Debug.LogError("Önce " + TargetScene + " sahnesini aç."); return; }
        var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "EcoWorld");
        if (root == null) { root = new GameObject("EcoWorld"); }
        root.SetActive(true);
        for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        foreach (var c in root.GetComponents<Component>()) if (!(c is Transform)) Object.DestroyImmediate(c);
        if (Object.FindFirstObjectByType<EcoValleyAssembler>() == null) new GameObject("Eco Valley").AddComponent<EcoValleyAssembler>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("ECOVALLEY_SCENE_READY");
    }
}
