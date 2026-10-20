using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Creates Assets/Scenes/EcoWorld.unity: a new, GDD-shaped world map.
//  * MainScene is never modified. EcoWorld starts as a copy of it so the working player rig,
//    weapons, enemies, HUD, clock and mechanic stations stay wired; only the old scenery
//    (the previous terrain, houses, roads, lamps, poles, solar rows) is dropped from the copy.
//  * Map layout follows the GDD "Harita" section: glacier (north), windy hills (west),
//    city (centre-north), sunny plains (centre-south), industry (north-east) and two lakes.
//  * Run from the menu: EcoQuest > Build EcoWorld map scene.
public static class BuildEcoWorldMap
{
    private const string SourceScene = "Assets/Scenes/MainScene.unity";
    private const string TargetScene = "Assets/Scenes/EcoWorld.unity";
    private const string Folder = "Assets/EcoWorld/";
    private const string ResFolder = "Assets/Resources/EcoWorld/";
    private const float Size = 640f;
    private const float Height = 170f;
    private const float WaterY = 17.5f;
    private const int HeightRes = 513;
    private const int AlphaRes = 512;
    private static readonly Vector2 Base = new Vector2(320f, 70f);
    private static readonly Vector2 LakeA = new Vector2(105f, 95f);
    private static readonly Vector2 LakeB = new Vector2(535f, 110f);
    private const float LakeAR = 56f, LakeBR = 50f;

    // ---------------------------------------------------------------- definitions

    private sealed class RegionDef
    {
        public string name, tagline;
        public EcoRegionInfo.Kind kind;
        public Vector2 center, size;
        public float air, water, soil, carbon, temperature, vegetation, habitat;
        public bool sensitive;
        public Color color;
    }

    private sealed class RoadDef
    {
        public Vector2[] points; public float width; public bool trail;
        public RoadDef(float width, bool trail, params Vector2[] points) { this.width = width; this.trail = trail; this.points = points; }
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
    private static TerrainData terrainData;
    private static Terrain terrain;
    private static Transform worldRoot;
    private static readonly Dictionary<string, Transform> Groups = new Dictionary<string, Transform>();
    private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();
    private static GameObject housePrefab, lampPrefab, polePrefab, solarPrefab;

    private static void DefineRegions()
    {
        Defs.Clear();
        Defs.Add(new RegionDef { name = "Buzul Bölgesi", kind = EcoRegionInfo.Kind.Glacier, center = new Vector2(320, 545), size = new Vector2(640, 190),
            tagline = "Erime riski altındaki buzullar. Karbon ayak izi arttıkça küçülür.", air = 2, water = 2, soil = 0, carbon = 6,
            temperature = -9f, vegetation = 0.05f, habitat = 0.4f, sensitive = true, color = new Color(0.72f, 0.90f, 1.00f) });
        Defs.Add(new RegionDef { name = "Rüzgârlı Alan", kind = EcoRegionInfo.Kind.Wind, center = new Vector2(105, 300), size = new Vector2(210, 300),
            tagline = "Açık tepeler. Rüzgâr türbinleri için en verimli bölge.", air = 8, water = 6, soil = 6, carbon = 8,
            temperature = 13f, vegetation = 0.65f, habitat = 0.8f, sensitive = false, color = new Color(0.45f, 0.82f, 0.74f) });
        Defs.Add(new RegionDef { name = "Şehir", kind = EcoRegionInfo.Kind.City, center = new Vector2(320, 360), size = new Vector2(220, 180),
            tagline = "Yoğun yerleşim. Orta düzey kirlilik, çocukların yaşadığı yer.", air = 38, water = 30, soil = 28, carbon = 38,
            temperature = 18f, vegetation = 0.22f, habitat = 0.3f, sensitive = false, color = new Color(0.86f, 0.80f, 0.58f) });
        Defs.Add(new RegionDef { name = "Sanayi Bölgesi", kind = EcoRegionInfo.Kind.Industry, center = new Vector2(535, 340), size = new Vector2(210, 220),
            tagline = "Filtresiz fabrikalar. Hava ve karbon kirliliğinin ana kaynağı.", air = 66, water = 60, soil = 58, carbon = 58,
            temperature = 19f, vegetation = 0.10f, habitat = 0f, sensitive = false, color = new Color(0.86f, 0.46f, 0.36f) });
        Defs.Add(new RegionDef { name = "Güneşli Bölge", kind = EcoRegionInfo.Kind.Sun, center = new Vector2(320, 135), size = new Vector2(220, 270),
            tagline = "Geniş açık ovalar. Güneş panelleri ve tarım için ideal.", air = 6, water = 8, soil = 10, carbon = 10,
            temperature = 24f, vegetation = 0.50f, habitat = 0.7f, sensitive = false, color = new Color(1.00f, 0.84f, 0.38f) });
        Defs.Add(new RegionDef { name = "Batı Gölü Havzası", kind = EcoRegionInfo.Kind.Lake, center = new Vector2(105, 75), size = new Vector2(210, 150),
            tagline = "Temiz tatlı su kaynağı. Su sınırlıdır, koru.", air = 5, water = 8, soil = 6, carbon = 6,
            temperature = 17f, vegetation = 0.70f, habitat = 0.85f, sensitive = true, color = new Color(0.45f, 0.74f, 1.00f) });
        Defs.Add(new RegionDef { name = "Doğu Gölü Havzası", kind = EcoRegionInfo.Kind.Lake, center = new Vector2(535, 115), size = new Vector2(210, 230),
            tagline = "Sanayiden etkilenen göl. Arıtma ve temizlik gerekiyor.", air = 30, water = 62, soil = 35, carbon = 25,
            temperature = 20f, vegetation = 0.30f, habitat = 0.1f, sensitive = false, color = new Color(0.55f, 0.62f, 0.42f) });
    }

    private static void DefineRoads()
    {
        Roads.Clear();
        Vector2 V(float x, float z) => new Vector2(x, z);
        Roads.Add(new RoadDef(9, false, V(320, 70), V(320, 140), V(322, 210), V(320, 272), V(320, 350), V(320, 442)));
        Roads.Add(new RoadDef(8, false, V(250, 272), V(250, 442)));
        Roads.Add(new RoadDef(8, false, V(390, 272), V(390, 442)));
        Roads.Add(new RoadDef(8, false, V(215, 300), V(425, 300)));
        Roads.Add(new RoadDef(9, false, V(215, 350), V(425, 350)));
        Roads.Add(new RoadDef(8, false, V(215, 400), V(425, 400)));
        Roads.Add(new RoadDef(7, false, V(215, 350), V(170, 346), V(120, 332), V(70, 305), V(30, 285)));
        Roads.Add(new RoadDef(7, false, V(425, 350), V(470, 347), V(540, 345), V(615, 336)));
        Roads.Add(new RoadDef(6, false, V(540, 345), V(540, 420)));
        Roads.Add(new RoadDef(6, false, V(480, 347), V(480, 300), V(560, 300)));
        Roads.Add(new RoadDef(6, false, V(320, 112), V(260, 108), V(200, 102), V(150, 108)));
        Roads.Add(new RoadDef(6, false, V(320, 112), V(400, 114), V(470, 114)));
        Roads.Add(new RoadDef(4, true, V(320, 442), V(332, 480), V(352, 520), V(376, 562)));
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
    {
        return 1f - Smooth(radius, radius + feather, Vector2.Distance(new Vector2(x, z), c));
    }

    private static float LakeShape(float h, float x, float z, Vector2 c, float r)
    {
        float d = Vector2.Distance(new Vector2(x, z), c);
        // Keep a dry rim around the water so a hill valley can never leak below the water plane.
        float rim = 1f - Smooth(r * 1.0f, r * 1.4f, d);
        h = Mathf.Lerp(h, Mathf.Max(h, 21f), rim);
        float bowl = 1f - Smooth(r * 0.5f, r * 1.05f, d);
        float bottom = 10.5f + (d / r) * 3.5f;
        return Mathf.Lerp(h, Mathf.Min(h, bottom), bowl);
    }

    public static float TerrainHeight(float x, float z)
    {
        float h = 22f + (Fbm(x * 0.004f, z * 0.004f, 4) - 0.5f) * 10f;
        // Glacier mountains in the north.
        float g = Smooth(420f, 540f, z);
        float ridge = 1f - Mathf.Abs(2f * Fbm(x * 0.007f + 11f, z * 0.007f + 5f, 4) - 1f);
        h += g * (16f + ridge * ridge * 58f + Fbm(x * 0.02f, z * 0.02f, 3) * 10f);
        // Rolling windy hills in the west.
        float w = RectMask(x, z, 105f, 300f, 105f, 150f, 70f);
        h += w * ((Fbm(x * 0.012f + 3f, z * 0.012f + 8f, 3) - 0.42f) * 34f);
        // Flat building grounds.
        h = Mathf.Lerp(h, 24.5f, RectMask(x, z, 320f, 360f, 100f, 85f, 35f));
        h = Mathf.Lerp(h, 21.5f, RectMask(x, z, 535f, 340f, 85f, 100f, 35f));
        h = Mathf.Lerp(h, 23f, RectMask(x, z, 320f, 140f, 95f, 110f, 60f) * 0.9f);
        h = Mathf.Lerp(h, 23f, CircleMask(x, z, Base, 34f, 22f));
        h = LakeShape(h, x, z, LakeA, LakeAR);
        h = LakeShape(h, x, z, LakeB, LakeBR);
        // Mountain ring so the playable area is enclosed.
        float edge = Mathf.Min(Mathf.Min(x, z), Mathf.Min(Size - x, Size - z));
        float e = 1f - Smooth(0f, 80f, edge);
        h += e * (14f + Fbm(x * 0.015f, z * 0.015f, 3) * 32f);
        return h;
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    private static float RoadEdgeDistance(float x, float z)
    {
        float best = float.MaxValue; var p = new Vector2(x, z);
        foreach (var road in Roads)
            for (int i = 0; i + 1 < road.points.Length; i++)
                best = Mathf.Min(best, DistToSegment(p, road.points[i], road.points[i + 1]) - road.width * 0.5f);
        return best;
    }

    private static float RoadPaint(float x, float z, bool trail)
    {
        float best = 0f; var p = new Vector2(x, z);
        foreach (var road in Roads)
        {
            if (road.trail != trail) continue;
            for (int i = 0; i + 1 < road.points.Length; i++)
            {
                float d = DistToSegment(p, road.points[i], road.points[i + 1]) - road.width * 0.5f;
                best = Mathf.Max(best, 1f - Smooth(-0.5f, 1.4f, d));
            }
        }
        return best;
    }

    private static int RegionIndexAt(float x, float z)
    {
        for (int i = 0; i < Defs.Count; i++)
        {
            var d = Defs[i];
            if (Mathf.Abs(x - d.center.x) <= d.size.x * 0.5f && Mathf.Abs(z - d.center.y) <= d.size.y * 0.5f) return i;
        }
        return -1;
    }

    private static float GroundY(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
    private static float F(float a, float b) => a + (float)rng.NextDouble() * (b - a);
    private static T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

    // ---------------------------------------------------------------- entry point

    [MenuItem("EcoQuest/Build EcoWorld map scene")]
    public static void Build()
    {
        if (!File.Exists(SourceScene))
        {
            EditorUtility.DisplayDialog("EcoWorld", "Kaynak sahne bulunamadı:\n" + SourceScene, "Tamam");
            return;
        }
        if (File.Exists(TargetScene) && !EditorUtility.DisplayDialog("EcoWorld",
            "Assets/Scenes/EcoWorld.unity zaten var. Yeniden oluşturulsun mu?\n(MainScene'e dokunulmaz; yalnız EcoWorld sahnesi yenilenir.)", "Yeniden oluştur", "İptal")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        report = new StringBuilder("EcoWorld oluşturma raporu\n");
        rng = new System.Random(20261018);
        KeepOut.Clear(); Groups.Clear(); MaterialCache.Clear();
        DefineRegions(); DefineRoads();
        try
        {
            Directory.CreateDirectory(Folder + "Materials"); Directory.CreateDirectory(Folder + "Textures"); Directory.CreateDirectory(ResFolder);
            AssetDatabase.Refresh();
            Progress("Sahne kopyalanıyor", 0.02f);
            if (File.Exists(TargetScene)) AssetDatabase.DeleteAsset(TargetScene);
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene)) throw new System.Exception("MainScene kopyalanamadı.");
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

            Progress("Eski dekor ayıklanıyor", 0.06f);
            var oldTerrainMaterial = HarvestAndClean(scene);

            worldRoot = new GameObject("EcoWorld").transform;
            foreach (var groupName in new[] { "Terrain", "Regions", "Lakes", "City", "Industry", "Energy", "Glacier", "Vegetation", "Props" })
            {
                var t = new GameObject(groupName).transform; t.SetParent(worldRoot, false); Groups[groupName] = t;
            }

            Progress("Arazi ve doku oluşturuluyor", 0.12f);
            BuildTerrain(oldTerrainMaterial);
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
            BuildHud();
            RegisterInBuildSettings();

            report.AppendLine("Sahne: " + TargetScene);
            File.WriteAllText(Folder + "BuildReport.txt", report.ToString());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("ECOWORLD_READY\n" + report);
            EditorUtility.DisplayDialog("EcoWorld hazır",
                "Assets/Scenes/EcoWorld.unity oluşturuldu.\n\nMainScene değiştirilmedi.\nRapor: Assets/EcoWorld/BuildReport.txt\n\nPlay'e basıp harita üzerinde test edebilirsin. [M] tuşu haritayı açar.", "Tamam");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("EcoWorld oluşturulamadı: " + ex);
            EditorUtility.DisplayDialog("EcoWorld", "Hata: " + ex.Message + "\n\nAyrıntı Console'da. MainScene etkilenmedi.", "Tamam");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    private static void Progress(string text, float value) => EditorUtility.DisplayProgressBar("EcoWorld", text, value);

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

    private static Material HarvestAndClean(Scene scene)
    {
        Material terrainMaterial = null;
        var roots = scene.GetRootGameObjects();
        GameObject Find(System.Func<string, bool> match) { foreach (var r in roots) if (r != null && match(r.name)) return r; return null; }
        housePrefab = HarvestPrefab(Find(n => n.Contains("Evler")));
        lampPrefab = HarvestPrefab(Find(n => n.Contains("Lamba")));
        polePrefab = HarvestPrefab(Find(n => n.Contains("Diregi")));
        solarPrefab = HarvestPrefab(Find(n => n.Contains("Dizisi")));
        report.AppendLine("Eski haritadan alınan prefablar (kopyalanmadı, yalnız yeniden kullanıldı): ev=" + Name(housePrefab)
            + ", lamba=" + Name(lampPrefab) + ", direk=" + Name(polePrefab) + ", güneş dizisi=" + Name(solarPrefab));

        var old = Object.FindFirstObjectByType<Terrain>();
        if (old != null) terrainMaterial = old.materialTemplate;

        var keepNames = new HashSet<string> { "alan", "Enemys", "Player", "Cam's", "Daylight" };
        foreach (var root in roots)
        {
            if (root == null) continue;
            string n = root.name;
            bool remove = n.Contains("Evler") || n.Contains("Yollar") || n.Contains("Lamba") || n.Contains("Diregi") || n.Contains("Dizisi")
                || n == "Gecici" || (n.Contains("lge1") && root.transform.childCount == 0);
            if (!remove && !keepNames.Contains(n)) remove = IsPureScenery(root);
            if (remove) { report.AppendLine("  silindi (kopyada): " + n); Object.DestroyImmediate(root); }
            else report.AppendLine("  korundu: " + n);
        }
        return terrainMaterial;
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
            main.startLifetime = 16f; main.startSpeed = 0.5f; main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.maxParticles = 2200; main.gravityModifier = 0.05f; main.startColor = new Color(1f, 1f, 1f, 0.85f);
            emission.rateOverTime = 130f;
            shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(520f, 2f, 150f);
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

    // ---------------------------------------------------------------- terrain

    private static void BuildTerrain(Material templateMaterial)
    {
        var data = new TerrainData { name = "EcoWorldTerrain" };
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(Size, Height, Size);
        var heights = new float[HeightRes, HeightRes];
        for (int iz = 0; iz < HeightRes; iz++)
            for (int ix = 0; ix < HeightRes; ix++)
            {
                float x = ix / (HeightRes - 1f) * Size, z = iz / (HeightRes - 1f) * Size;
                heights[iz, ix] = Mathf.Clamp01(TerrainHeight(x, z) / Height);
            }
        data.SetHeights(0, 0, heights);
        AssetDatabase.CreateAsset(data, Folder + "EcoWorldTerrain.asset");
        terrainData = data;

        var layers = MakeLayers();
        data.terrainLayers = layers;
        data.alphamapResolution = AlphaRes;
        PaintTerrain(data, layers.Length);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "EcoWorld Terrain"; go.transform.SetParent(Groups["Terrain"], false); go.transform.position = Vector3.zero;
        terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 4f; terrain.basemapDistance = 500f; terrain.drawInstanced = true;
        if (templateMaterial != null) terrain.materialTemplate = templateMaterial;
        report.AppendLine("Arazi: " + Size + "x" + Size + " m, yükseklik aralığı 0-" + Height + " m, su seviyesi y=" + WaterY);
    }

    private static TerrainLayer[] MakeLayers()
    {
        var defs = new (string name, Color color, float contrast, float tile)[]
        {
            ("Grass", new Color(0.30f, 0.52f, 0.22f), 0.20f, 9f),
            ("DryGrass", new Color(0.63f, 0.58f, 0.30f), 0.18f, 9f),
            ("Rock", new Color(0.46f, 0.45f, 0.47f), 0.38f, 12f),
            ("Snow", new Color(0.93f, 0.96f, 0.99f), 0.07f, 14f),
            ("Asphalt", new Color(0.17f, 0.18f, 0.20f), 0.10f, 6f),
            ("Dirt", new Color(0.37f, 0.31f, 0.25f), 0.25f, 8f),
        };
        var layers = new TerrainLayer[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            string texPath = Folder + "Textures/Terrain_" + defs[i].name + ".png";
            File.WriteAllBytes(texPath, MakeTileTexture(defs[i].color, defs[i].contrast, 1000 + i * 17).EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var layer = new TerrainLayer { diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath), tileSize = new Vector2(defs[i].tile, defs[i].tile) };
            layer.smoothness = 0f; layer.metallic = 0f;
            AssetDatabase.CreateAsset(layer, Folder + "Terrain_" + defs[i].name + ".terrainlayer");
            layers[i] = layer;
        }
        return layers;
    }

    // Seamless tile: sums of integer-frequency sines wrap exactly at the texture edges.
    private static Texture2D MakeTileTexture(Color baseColor, float contrast, int seed)
    {
        const int n = 256;
        var r = new System.Random(seed);
        var waves = new (int fx, int fy, float phase, float amp)[9];
        for (int i = 0; i < waves.Length; i++)
            waves[i] = (r.Next(1, 7), r.Next(1, 7), (float)r.NextDouble() * 6.283f, 1f / (1f + i * 0.45f));
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true);
        var pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n, sum = 0f, norm = 0f;
                foreach (var w in waves) { sum += Mathf.Sin(6.2831853f * (w.fx * u + w.fy * v) + w.phase) * w.amp; norm += w.amp; }
                float speckle = ((float)r.NextDouble() - 0.5f) * 0.08f;
                float k = 1f + (sum / norm) * contrast + speckle;
                pixels[y * n + x] = new Color(Mathf.Clamp01(baseColor.r * k), Mathf.Clamp01(baseColor.g * k), Mathf.Clamp01(baseColor.b * k), 1f);
            }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    private static void PaintTerrain(TerrainData data, int layerCount)
    {
        var map = new float[AlphaRes, AlphaRes, layerCount];
        var w = new float[layerCount];
        for (int iz = 0; iz < AlphaRes; iz++)
            for (int ix = 0; ix < AlphaRes; ix++)
            {
                float u = (ix + 0.5f) / AlphaRes, v = (iz + 0.5f) / AlphaRes;
                float x = u * Size, z = v * Size;
                float h = data.GetInterpolatedHeight(u, v);
                float steep = data.GetSteepness(u, v);
                float noise = Fbm(x * 0.03f, z * 0.03f, 3);
                for (int i = 0; i < layerCount; i++) w[i] = 0f;
                w[0] = 1f;
                float sun = RectMask(x, z, 320f, 140f, 110f, 135f, 25f);
                w[1] += sun * (0.55f + 0.45f * noise);
                float industry = RectMask(x, z, 535f, 340f, 105f, 110f, 20f);
                w[5] += industry * 0.95f; w[0] *= 1f - industry * 0.8f;
                float city = RectMask(x, z, 320f, 360f, 108f, 90f, 10f);
                w[4] += city * 0.30f; w[0] *= 1f - city * 0.55f;
                foreach (var lake in new[] { new Vector3(LakeA.x, LakeA.y, LakeAR), new Vector3(LakeB.x, LakeB.y, LakeBR) })
                {
                    float d = Vector2.Distance(new Vector2(x, z), new Vector2(lake.x, lake.y));
                    w[1] += (1f - Smooth(lake.z * 0.78f, lake.z * 1.3f, d)) * 1.2f;
                }
                float snowLine = 50f + noise * 12f;
                float snow = Mathf.Max(Smooth(snowLine - 6f, snowLine + 8f, h), Smooth(455f, 505f, z + noise * 25f) * 0.55f);
                w[3] += snow * 4f;
                w[2] += Smooth(27f, 43f, steep) * 3.5f + Smooth(38f, 70f, h) * 0.6f * (1f - snow);
                w[4] += RoadPaint(x, z, false) * 8f;
                w[5] += RoadPaint(x, z, true) * 5f;
                float sum = 0f;
                for (int i = 0; i < layerCount; i++) sum += w[i];
                for (int i = 0; i < layerCount; i++) map[iz, ix, i] = w[i] / sum;
            }
        data.SetAlphamaps(0, 0, map);
    }

    // ---------------------------------------------------------------- regions

    private static void BuildRegions()
    {
        foreach (var def in Defs)
        {
            var go = new GameObject(def.name); go.transform.SetParent(Groups["Regions"], false);
            go.transform.position = new Vector3(def.center.x, GroundY(def.center.x, def.center.y), def.center.y);
            var region = go.AddComponent<EcoRegion>();
            region.regionName = def.name; region.size = def.size; region.priority = 0;
            region.airPollution = def.air; region.waterPollution = def.water; region.soilPollution = def.soil; region.carbon = def.carbon;
            region.baseTemperature = def.temperature; region.vegetation = def.vegetation; region.sensitiveHabitat = def.sensitive;
            var habitat = go.AddComponent<EcoHabitat>(); habitat.recovery = def.habitat;
            var info = go.AddComponent<EcoRegionInfo>(); info.kind = def.kind; info.tagline = def.tagline; info.mapColor = def.color;
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
        AssetDatabase.CreateAsset(mesh, Folder + name + ".asset");
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
        BuildLake("Batı Gölü", LakeA, LakeAR, false, new Vector2(1f, 0.15f));
        BuildLake("Doğu Gölü", LakeB, LakeBR, true, new Vector2(-1f, 0.1f));
    }

    private static void BuildLake(string name, Vector2 center, float radius, bool polluted, Vector2 dockDirection)
    {
        var root = Group(name, Groups["Lakes"]);
        root.position = new Vector3(center.x, WaterY, center.y);

        var surface = new GameObject("Water surface"); surface.transform.SetParent(root, false);
        var filter = surface.AddComponent<MeshFilter>(); filter.sharedMesh = DiscMesh(name.Replace(" ", "") + "_Disc", radius * 0.97f, 12, 44);
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
        var box = source.AddComponent<BoxCollider>(); box.size = new Vector3(radius * 1.2f, 0.1f, radius * 1.2f);
        var water = source.AddComponent<WeaponWaterSource>(); water.useRegionalQuality = true; water.cleanWater = !polluted;
        var fishing = source.AddComponent<EcoFishingSpot>(); fishing.population = polluted ? 3f : 8f;

        // Wooden dock reaching into the lake from the road side.
        Vector2 dir = dockDirection.normalized;
        float shore = radius * 0.86f;
        Vector2 start = center + dir * shore;
        float yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg; // plank length axis (local z) points into the lake
        var dock = Group("Dock", root); dock.position = new Vector3(start.x, WaterY + 0.55f, start.y);
        dock.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wood = Mat("DockWood", new Color(0.42f, 0.29f, 0.17f), 0.1f);
        var plank = Prim(PrimitiveType.Cube, "Planks", dock, new Vector3(0f, 0f, 7f), new Vector3(2.6f, 0.22f, 15f), wood, true);
        for (int i = 0; i < 5; i++)
        {
            Prim(PrimitiveType.Cylinder, "Post", dock, new Vector3(-1.2f, -1.4f, 1.5f + i * 3f), new Vector3(0.28f, 1.6f, 0.28f), wood);
            Prim(PrimitiveType.Cylinder, "Post", dock, new Vector3(1.2f, -1.4f, 1.5f + i * 3f), new Vector3(0.28f, 1.6f, 0.28f), wood);
        }
        WorldLabel(dock, polluted ? "DOĞU GÖLÜ\nKirli • Arıtma gerekiyor" : "BATI GÖLÜ\nTemiz su • [5] Eller → [E] Balık tut", new Vector3(0f, 2.6f, 1f), 0.008f);
        KeepOut.Add(new Rect(center.x - radius * 1.12f, center.y - radius * 1.12f, radius * 2.24f, radius * 2.24f));
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
        const string root = "Assets/Zipler ve Modeller";
        if (!AssetDatabase.IsValidFolder(root)) return null;
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
        report.AppendLine("Model bulunamadı [" + string.Join("/", keys) + "] -> yedek (basit) yapı kullanıldı");
        return null;
    }

    // ---------------------------------------------------------------- city

    private static void BuildCity()
    {
        var root = Groups["City"];
        var apartment = FindZiplerModel("apartman");
        var modern = FindZiplerModel("modern");
        var library = FindZiplerModel("kitap");
        var clinic = FindZiplerModel("klinik");
        var market = FindZiplerModel("market");
        var traffic = FindZiplerModel("trafik");
        var bin = FindZiplerModel("cop");
        var lamp = lampPrefab != null ? lampPrefab : FindZiplerModel("lamba");

        float[] xe = { 215f, 250f, 320f, 390f, 425f };
        float[] ze = { 272f, 300f, 350f, 400f, 442f };
        var slots = new List<(Vector2 center, Vector2 size, Vector2 blockCenter)>();
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 4; j++)
            {
                float x0 = xe[i] + (i == 0 ? 3f : 6f), x1 = xe[i + 1] - (i == 3 ? 3f : 6f);
                float z0 = ze[j] + (j == 0 ? 3f : 6f), z1 = ze[j + 1] - (j == 3 ? 3f : 6f);
                float w = x1 - x0, d = z1 - z0;
                if (w < 14f || d < 12f) continue;
                int nx = Mathf.Max(1, Mathf.FloorToInt(w / 26f)), nz = Mathf.Max(1, Mathf.FloorToInt(d / 24f));
                for (int a = 0; a < nx; a++)
                    for (int b = 0; b < nz; b++)
                    {
                        var c = new Vector2(x0 + (a + 0.5f) * w / nx, z0 + (b + 0.5f) * d / nz);
                        if (Vector2.Distance(c, new Vector2(320f, 350f)) < 19f) continue; // central plaza
                        slots.Add((c, new Vector2(w / nx, d / nz), new Vector2((x0 + x1) * 0.5f, (z0 + z1) * 0.5f)));
                    }
            }
        slots = slots.OrderBy(s => Vector2.Distance(s.center, new Vector2(320f, 350f))).ToList();
        int placed = 0;
        for (int k = 0; k < slots.Count; k++)
        {
            var slot = slots[k];
            if (k > 4 && rng.NextDouble() < 0.12) continue;
            Vector2 toward = slot.center - slot.blockCenter;
            float yaw = Mathf.Abs(toward.x) > Mathf.Abs(toward.y) ? (toward.x > 0 ? 90f : 270f) : (toward.y > 0 ? 0f : 180f);
            float maxW = slot.size.x * 0.82f, maxD = slot.size.y * 0.82f;
            float dist = Vector2.Distance(slot.center, new Vector2(320f, 350f));
            var pos = new Vector3(slot.center.x, GroundY(slot.center.x, slot.center.y), slot.center.y);
            GameObject model; float height; bool shrinkOnly = false;
            if (k == 0) { model = market; height = 8f; }
            else if (k == 1) { model = clinic; height = 11f; }
            else if (k == 2) { model = library; height = 9f; }
            else if (dist < 85f) { model = apartment; height = F(15f, 24f); }
            else if (housePrefab != null && rng.NextDouble() < 0.5) { model = housePrefab; height = 0f; shrinkOnly = true; }
            else { model = modern != null ? modern : housePrefab; height = F(6.5f, 9f); shrinkOnly = model == housePrefab; }
            GameObject built = null;
            if (model != null) built = SpawnModel(model, root, pos, yaw, height, maxW, maxD, true, shrinkOnly);
            if (built == null)
            {
                float bw = Mathf.Min(maxW, 22f), bd = Mathf.Min(maxD, 18f), bh = dist < 85f ? F(14f, 26f) : F(6f, 9f);
                built = ProceduralBuilding(root, pos, yaw, bw, bd, bh);
            }
            built.name = "Building " + k;
            if (TryBounds(built, out var bounds)) KeepOut.Add(new Rect(bounds.min.x - 1f, bounds.min.z - 1f, bounds.size.x + 2f, bounds.size.z + 2f));
            placed++;
        }
        report.AppendLine("Şehir: " + placed + " bina yerleştirildi");

        // Central plaza: stone disc, ring of trees and benches-as-blocks.
        var plaza = Group("Plaza", root); plaza.position = new Vector3(320f, GroundY(320f, 350f), 350f);
        Prim(PrimitiveType.Cylinder, "Plaza floor", plaza, new Vector3(0f, 0.08f, 0f), new Vector3(30f, 0.08f, 30f), Mat("PlazaStone", new Color(0.62f, 0.6f, 0.55f), 0.1f));
        var fountain = Prim(PrimitiveType.Cylinder, "Fountain basin", plaza, new Vector3(0f, 0.45f, 0f), new Vector3(6f, 0.4f, 6f), Mat("FountainStone", new Color(0.7f, 0.7f, 0.68f), 0.2f), true);
        Prim(PrimitiveType.Cylinder, "Fountain water", plaza, new Vector3(0f, 0.8f, 0f), new Vector3(5.2f, 0.05f, 5.2f), Mat("FountainWater", new Color(0.35f, 0.75f, 0.85f, 0.8f), 0.9f, 0f, true));
        Prim(PrimitiveType.Cylinder, "Fountain column", plaza, new Vector3(0f, 1.6f, 0f), new Vector3(0.8f, 1.0f, 0.8f), Mat("FountainStone", new Color(0.7f, 0.7f, 0.68f), 0.2f));
        WorldLabel(plaza, "ŞEHİR MEYDANI\nTemiz çevre, yaşanabilir şehir", new Vector3(0f, 4.5f, 0f), 0.01f);
        KeepOut.Add(new Rect(320f - 17f, 350f - 17f, 34f, 34f));

        // Street furniture.
        int lamps = 0;
        foreach (var road in Roads)
        {
            if (road.width < 8f || road.trail) continue;
            for (int i = 0; i + 1 < road.points.Length; i++)
            {
                Vector2 a = road.points[i], b = road.points[i + 1], dir = (b - a).normalized, normal = new Vector2(-dir.y, dir.x);
                float length = Vector2.Distance(a, b);
                for (float t = 12f; t < length - 6f; t += 24f)
                {
                    Vector2 p = a + dir * t + normal * (road.width * 0.5f + 1.4f) * (((int)(t / 24f)) % 2 == 0 ? 1f : -1f);
                    if (Vector2.Distance(p, new Vector2(320f, 350f)) < 20f) continue;
                    if (p.x < 212f || p.x > 428f || p.y < 270f || p.y > 444f) continue;
                    PlaceLamp(root, lamp, p); lamps++;
                }
            }
        }
        report.AppendLine("Şehir: " + lamps + " sokak lambası");
        if (traffic != null)
            foreach (var p in new[] { new Vector2(256f, 356f), new Vector2(384f, 344f), new Vector2(326f, 306f), new Vector2(314f, 394f) })
                SpawnModel(traffic, root, new Vector3(p.x, GroundY(p.x, p.y), p.y), 0f, 5f, 0f, 0f, true);
        if (bin != null)
            foreach (var p in new[] { new Vector2(306f, 336f), new Vector2(334f, 364f), new Vector2(306f, 364f) })
                SpawnModel(bin, root, new Vector3(p.x, GroundY(p.x, p.y), p.y), F(0f, 360f), 1.1f, 0f, 0f, true);
    }

    private static void PlaceLamp(Transform parent, GameObject lampModel, Vector2 p)
    {
        var pos = new Vector3(p.x, GroundY(p.x, p.y), p.y);
        if (lampModel != null && SpawnModel(lampModel, parent, pos, 0f, 5.2f, 0f, 0f, false, false) != null) return;
        var lamp = Group("Street lamp", parent); lamp.position = pos;
        var metal = Mat("LampMetal", new Color(0.18f, 0.2f, 0.22f), 0.4f, 0.6f);
        Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(0f, 2.6f, 0f), new Vector3(0.14f, 2.6f, 0.14f), metal);
        Prim(PrimitiveType.Cube, "Arm", lamp, new Vector3(0.5f, 5.1f, 0f), new Vector3(1.2f, 0.1f, 0.12f), metal);
        Prim(PrimitiveType.Cube, "Lamp head", lamp, new Vector3(1.0f, 5.0f, 0f), new Vector3(0.7f, 0.14f, 0.35f),
            Mat("LampGlow", new Color(1f, 0.93f, 0.7f), 0.2f, 0f, false, new Color(1f, 0.85f, 0.5f) * 1.4f));
    }

    private static GameObject ProceduralBuilding(Transform parent, Vector3 pos, float yaw, float w, float d, float h)
    {
        var tint = new Color(F(0.62f, 0.82f), F(0.6f, 0.78f), F(0.55f, 0.74f));
        var root = Group("Building", parent); root.position = pos; root.rotation = Quaternion.Euler(0f, yaw, 0f);
        var body = Prim(PrimitiveType.Cube, "Body", root, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d), Mat("BuildingWall" + rng.Next(4), tint, 0.1f), true);
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, h + 0.2f, 0f), new Vector3(w + 0.6f, 0.4f, d + 0.6f), Mat("BuildingRoof", new Color(0.22f, 0.24f, 0.26f), 0.1f));
        var glass = Mat("BuildingGlass", new Color(0.35f, 0.5f, 0.6f), 0.85f, 0.2f);
        int floors = Mathf.Max(1, Mathf.FloorToInt(h / 3.4f));
        for (int f = 0; f < floors; f++)
            Prim(PrimitiveType.Cube, "Windows", root, new Vector3(0f, 1.8f + f * 3.4f, -d * 0.5f - 0.03f), new Vector3(w * 0.82f, 1.5f, 0.08f), glass);
        return root.gameObject;
    }

    // ---------------------------------------------------------------- industry

    private static void BuildIndustry()
    {
        var root = Groups["Industry"];
        BuildFactory(root, "Fabrika A", new Vector2(500f, 385f), 0f, new Vector3(34f, 12f, 20f), 2, true);
        BuildFactory(root, "Fabrika B", new Vector2(578f, 372f), 0f, new Vector3(30f, 11f, 22f), 2, true);
        BuildFactory(root, "Depo C", new Vector2(520f, 262f), 0f, new Vector3(38f, 9f, 24f), 1, false);
        BuildFactory(root, "Depo D", new Vector2(595f, 268f), 0f, new Vector3(28f, 10f, 20f), 1, false);

        var tankMat = Mat("TankSteel", new Color(0.62f, 0.66f, 0.68f), 0.5f, 0.5f);
        var tanks = Group("Tank farm", root);
        for (int i = 0; i < 4; i++)
        {
            float x = 452f + (i % 2) * 14f, z = 395f + (i / 2) * 14f;
            var t = Prim(PrimitiveType.Cylinder, "Tank", tanks, new Vector3(x, GroundY(x, z) + 5f, z), new Vector3(10f, 5f, 10f), tankMat, true);
            Prim(PrimitiveType.Cylinder, "Tank cap", t.transform, new Vector3(0f, 1.02f, 0f), new Vector3(0.9f, 0.04f, 0.9f), Mat("TankCap", new Color(0.3f, 0.33f, 0.35f), 0.3f, 0.5f));
        }
        KeepOut.Add(new Rect(444f, 388f, 32f, 32f));

        var yard = Group("Container yard", root);
        Color[] colors = { new Color(0.75f, 0.25f, 0.2f), new Color(0.2f, 0.4f, 0.6f), new Color(0.8f, 0.6f, 0.2f), new Color(0.25f, 0.5f, 0.35f) };
        for (int i = 0; i < 14; i++)
        {
            float x = 560f + (i % 4) * 7f, z = 405f + (i / 4) * 9f;
            int stack = 1 + rng.Next(3);
            for (int s = 0; s < stack; s++)
                Prim(PrimitiveType.Cube, "Container", yard, new Vector3(x, GroundY(x, z) + 1.3f + s * 2.6f, z), new Vector3(6f, 2.5f, 2.5f),
                    Mat("Container" + (i % 4), colors[i % 4], 0.3f, 0.3f), true);
        }
        KeepOut.Add(new Rect(554f, 400f, 36f, 40f));

        // Dumped waste by the road: gives the recycler something to do in the worst region.
        var junk = Group("Dump site", root);
        foreach (var name in new[] { "MetalScrap", "PlasticScrap", "OpenBox", "StorageBarrel", "SupplyBox" })
        {
            var prefab = Resources.Load<GameObject>("EcoArt/" + name);
            if (prefab == null) continue;
            for (int i = 0; i < 9; i++)
            {
                float x = F(488f, 530f), z = F(318f, 336f);
                if (RoadEdgeDistance(x, z) < 2f) continue;
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, junk);
                item.transform.position = new Vector3(x, GroundY(x, z) + 0.05f, z); item.transform.rotation = Quaternion.Euler(0f, F(0f, 360f), 0f);
            }
        }
        report.AppendLine("Sanayi: 2 işlevsel (filtre takılabilir) + 2 dekoratif fabrika, tank, konteyner, atık sahası");
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
        Prim(PrimitiveType.Cube, "Roof", root, new Vector3(0f, size.y + 0.15f, 0f), new Vector3(size.x + 0.6f, 0.3f, size.z + 0.6f), roofMat);
        Prim(PrimitiveType.Cube, "Gate", root, new Vector3(0f, 2.3f, -size.z * 0.5f - 0.03f), new Vector3(6f, 4.6f, 0.2f), roofMat);
        for (int i = 0; i < 5; i++)
            Prim(PrimitiveType.Cube, "Window", root, new Vector3(-size.x * 0.38f + i * size.x * 0.19f, size.y * 0.68f, -size.z * 0.5f - 0.03f), new Vector3(2.8f, 1.5f, 0.12f), glass);
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cube, "Roof vent", root, new Vector3(-size.x * 0.3f + i * size.x * 0.2f, size.y + 0.9f, -size.z * 0.15f), new Vector3(2.4f, 1.2f, 2.4f), roofMat);
        ParticleSystem firstSmoke = null;
        for (int c = 0; c < chimneys; c++)
        {
            float cx = size.x * 0.5f - 5f - c * 6f, cz = size.z * 0.5f - 3.5f, ch = size.y + 17f + c * 4f;
            Prim(PrimitiveType.Cylinder, "Chimney", root, new Vector3(cx, ch * 0.5f, cz), new Vector3(2.6f, ch * 0.5f, 2.6f), brick);
            Prim(PrimitiveType.Cylinder, "Chimney band", root, new Vector3(cx, ch - 1.2f, cz), new Vector3(2.75f, 0.6f, 2.75f), red);
            Prim(PrimitiveType.Cylinder, "Chimney band", root, new Vector3(cx, ch - 4f, cz), new Vector3(2.75f, 0.6f, 2.75f), red);
            var smoke = MakeSmoke(root, new Vector3(cx, ch + 0.4f, cz), 1f);
            if (firstSmoke == null) firstSmoke = smoke;
        }
        var box = root.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0f, size.y * 0.5f, 0f); box.size = size;
        if (functional)
        {
            var filter = Prim(PrimitiveType.Cylinder, "Installed air filter", root, new Vector3(-size.x * 0.5f - 2.2f, 3f, -size.z * 0.25f),
                new Vector3(3.2f, 3f, 3.2f), Mat("FactoryFilter", new Color(0.2f, 0.62f, 0.4f), 0.3f, 0.3f));
            var factory = root.gameObject.AddComponent<EcoFactory>();
            factory.filterVisual = filter; factory.smoke = firstSmoke;
            filter.SetActive(false);
            WorldLabel(root, "FABRİKA\n[E] Büyük filtre tak", new Vector3(0f, size.y * 0.55f + 2f, -size.z * 0.5f - 2f), 0.012f);
        }
        else WorldLabel(root, "ESKİ DEPO\nTerk edilmiş", new Vector3(0f, size.y * 0.5f + 2f, -size.z * 0.5f - 2f), 0.01f);
        KeepOut.Add(new Rect(xz.x - size.x * 0.5f - 6f, xz.y - size.z * 0.5f - 4f, size.x + 12f, size.z + 8f));
    }

    // ---------------------------------------------------------------- energy (wind + solar)

    private static void BuildEnergy()
    {
        var root = Groups["Energy"];
        // Wind farm: the highest ridges of the western hills, at least 46 m apart.
        var candidates = new List<(Vector2 p, float h)>();
        for (float x = 20f; x < 195f; x += 6f)
            for (float z = 175f; z < 435f; z += 6f)
            {
                if (RoadEdgeDistance(x, z) < 14f) continue;
                if (terrainData.GetSteepness(x / Size, z / Size) > 22f) continue;
                candidates.Add((new Vector2(x, z), GroundY(x, z)));
            }
        var chosen = new List<Vector2>();
        foreach (var c in candidates.OrderByDescending(cand => cand.h))
        {
            if (chosen.Count >= 11) break;
            if (chosen.All(o => Vector2.Distance(o, c.p) >= 46f)) chosen.Add(c.p);
        }
        var farm = Group("Wind farm", root);
        foreach (var p in chosen) BuildTurbine(farm, p);
        report.AppendLine("Rüzgâr çiftliği: " + chosen.Count + " dekoratif türbin (rüzgâra göre döner, enerji üretmez)");

        BuildSolarField(root, new Rect(232f, 160f, 74f, 90f));
        BuildSolarField(root, new Rect(338f, 160f, 74f, 90f));

        // Power line from the wind farm to the city along the west road.
        {
            var road = Roads[6];
            var poles = new List<Vector3>();
            for (int i = 0; i + 1 < road.points.Length; i++)
            {
                Vector2 a = road.points[i], b = road.points[i + 1], dir = (b - a).normalized, normal = new Vector2(-dir.y, dir.x);
                float length = Vector2.Distance(a, b);
                for (float t = 0f; t < length; t += 32f)
                {
                    Vector2 p = a + dir * t + normal * (road.width * 0.5f + 4f);
                    var pos = new Vector3(p.x, GroundY(p.x, p.y), p.y);
                    GameObject pole = polePrefab != null ? SpawnModel(polePrefab, root, pos, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f, 11f, 0f, 0f, true) : null;
                    if (pole == null) pole = ProceduralPole(root, pos, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg);
                    poles.Add(pos + Vector3.up * 10.5f);
                }
            }
            var lineMat = Mat("PowerLine", new Color(0.05f, 0.05f, 0.06f), 0.1f);
            var wires = Group("Power lines", root);
            for (int i = 0; i + 1 < poles.Count; i++)
            {
                if (Vector3.Distance(poles[i], poles[i + 1]) > 60f) continue;
                var go = new GameObject("Wire"); go.transform.SetParent(wires, false);
                var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.widthMultiplier = 0.06f;
                line.sharedMaterial = lineMat; line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.positionCount = 7;
                for (int k = 0; k < 7; k++)
                {
                    float t = k / 6f;
                    var p = Vector3.Lerp(poles[i], poles[i + 1], t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * 1.4f;
                    line.SetPosition(k, p);
                }
            }
        }
    }

    private static GameObject ProceduralPole(Transform parent, Vector3 pos, float yaw)
    {
        var pole = Group("Power pole", parent); pole.position = pos; pole.rotation = Quaternion.Euler(0f, yaw, 0f);
        var wood = Mat("PoleWood", new Color(0.36f, 0.26f, 0.17f), 0.1f);
        Prim(PrimitiveType.Cylinder, "Pole", pole, new Vector3(0f, 5.5f, 0f), new Vector3(0.3f, 5.5f, 0.3f), wood, true);
        Prim(PrimitiveType.Cube, "Crossarm", pole, new Vector3(0f, 10.2f, 0f), new Vector3(0.18f, 0.18f, 3.2f), wood);
        return pole.gameObject;
    }

    private static void BuildTurbine(Transform parent, Vector2 p)
    {
        var root = Group("Wind turbine", parent);
        root.position = new Vector3(p.x, GroundY(p.x, p.y) - 0.3f, p.y);
        root.rotation = Quaternion.Euler(0f, F(-15f, 15f) - 90f, 0f); // rotor faces the prevailing western wind
        var white = Mat("TurbineWhite", new Color(0.93f, 0.94f, 0.92f), 0.35f);
        var tower = Prim(PrimitiveType.Cylinder, "Tower", root, new Vector3(0f, 17f, 0f), new Vector3(1.5f, 17f, 1.5f), white);
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
        KeepOut.Add(new Rect(p.x - 6f, p.y - 6f, 12f, 12f));
    }

    private static void BuildSolarField(Transform parent, Rect area)
    {
        var field = Group("Solar field", parent);
        int placed = 0;
        if (solarPrefab != null)
        {
            var probe = SpawnModel(solarPrefab, field, new Vector3(area.center.x, GroundY(area.center.x, area.center.y), area.center.y), 0f, 0f, 0f, 0f, false, false);
            if (probe != null && TryBounds(probe, out var pb) && pb.size.x < 40f && pb.size.z < 40f && pb.size.x > 0.5f && pb.size.z > 0.5f)
            {
                float sx = pb.size.x + 1.5f, sz = pb.size.z + 3f;
                Object.DestroyImmediate(probe);
                for (float z = area.yMin + sz * 0.5f; z < area.yMax - sz * 0.4f && placed < 60; z += sz)
                    for (float x = area.xMin + sx * 0.5f; x < area.xMax - sx * 0.4f && placed < 60; x += sx)
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
            for (float z = area.yMin + 4f; z < area.yMax - 3f; z += 8.5f)
            {
                var row = Group("Solar row", field);
                float baseY = GroundY(area.center.x, z);
                row.position = new Vector3(area.xMin + 2f, baseY, z);
                int count = Mathf.FloorToInt((area.width - 4f) / 4.6f);
                for (int i = 0; i < count; i++)
                {
                    float x = 2.3f + i * 4.6f;
                    var panel = Prim(PrimitiveType.Cube, "Panel", row, new Vector3(x, 1.7f, 0f), new Vector3(4.3f, 0.1f, 2.8f), cell);
                    panel.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Prim(PrimitiveType.Cube, "Frame", row, new Vector3(x, 1.68f, 0f), new Vector3(4.4f, 0.06f, 2.9f), frame).transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Prim(PrimitiveType.Cylinder, "Post", row, new Vector3(x, 0.85f, 0.2f), new Vector3(0.14f, 0.85f, 0.14f), frame);
                }
                var col = row.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(count * 2.3f, 1.2f, 0f); col.size = new Vector3(count * 4.6f, 2.4f, 3f);
                placed++;
            }
        }
        KeepOut.Add(area);
        report.AppendLine("Güneş sahası: " + placed + " öğe (" + (solarPrefab != null ? "eski haritanın dizisi yeniden kullanıldı" : "basit panel sıraları") + ")");
    }

    // ---------------------------------------------------------------- glacier

    private static void BuildGlacier()
    {
        var root = Groups["Glacier"];
        var iceMat = Mat("GlacierIce", new Color(0.62f, 0.86f, 0.96f, 0.82f), 0.95f, 0.1f, true);
        var blocks = new List<Transform>();
        for (int i = 0; i < 60; i++)
        {
            float x = F(40f, 600f), z = F(468f, 585f);
            if (RoadEdgeDistance(x, z) < 8f) continue;
            float ground = GroundY(x, z);
            if (ground > 95f) continue;
            float w = F(4f, 11f), hgt = F(5f, 15f), d = F(4f, 11f);
            var b = Prim(PrimitiveType.Cube, "Ice block", root, new Vector3(x, ground + hgt * 0.45f, z), new Vector3(w, hgt, d), iceMat, true);
            b.transform.rotation = Quaternion.Euler(F(-9f, 9f), F(0f, 360f), F(-9f, 9f));
            blocks.Add(b.transform);
        }
        for (int i = 0; i < 28; i++)
        {
            float x = F(40f, 600f), z = F(466f, 560f);
            if (RoadEdgeDistance(x, z) < 8f) continue;
            float ground = GroundY(x, z);
            if (ground > 100f) continue;
            float hgt = F(7f, 20f);
            var spike = Prim(PrimitiveType.Cube, "Ice spire", root, new Vector3(x, ground + hgt * 0.4f, z), new Vector3(F(1.5f, 3.2f), hgt, F(1.5f, 3.2f)), iceMat, true);
            spike.transform.rotation = Quaternion.Euler(F(-14f, 14f), F(0f, 360f), F(-14f, 14f));
            blocks.Add(spike.transform);
        }
        var melt = root.gameObject.AddComponent<EcoGlacierMelt>(); melt.ice = blocks;

        // Research cabin at the end of the trail.
        float cx = 345f, cz = 505f, gy = GroundY(cx, cz);
        var cabin = Group("Glacier research station", root); cabin.position = new Vector3(cx, gy, cz);
        var wall = Mat("CabinWall", new Color(0.72f, 0.28f, 0.22f), 0.1f);
        Prim(PrimitiveType.Cube, "Foundation", cabin, new Vector3(0f, -0.8f, 0f), new Vector3(9f, 3f, 7f), Mat("CabinStone", new Color(0.4f, 0.4f, 0.42f), 0.1f));
        Prim(PrimitiveType.Cube, "Cabin", cabin, new Vector3(0f, 2.2f, 0f), new Vector3(8f, 3.4f, 6f), wall, true);
        Prim(PrimitiveType.Cube, "Roof", cabin, new Vector3(0f, 4.2f, 0f), new Vector3(8.8f, 0.5f, 6.8f), Mat("CabinSnowRoof", new Color(0.93f, 0.96f, 0.98f), 0.1f));
        Prim(PrimitiveType.Cube, "Window", cabin, new Vector3(-1.8f, 2.4f, -3.03f), new Vector3(1.8f, 1.2f, 0.1f), Mat("CabinGlow", new Color(1f, 0.9f, 0.6f), 0.5f, 0f, false, new Color(1f, 0.8f, 0.45f)));
        Prim(PrimitiveType.Cube, "Door", cabin, new Vector3(2f, 1.8f, -3.03f), new Vector3(1.3f, 2.4f, 0.1f), Mat("CabinDoor", new Color(0.25f, 0.18f, 0.12f), 0.1f));
        WorldLabel(cabin, "BUZUL İZLEME İSTASYONU\nKarbon ayak izi arttıkça buzullar erir", new Vector3(0f, 6.4f, -2f), 0.01f);
        KeepOut.Add(new Rect(cx - 8f, cz - 7f, 16f, 14f));

        MakeSmoke(root, new Vector3(320f, 118f, 555f), 1f, true);
        report.AppendLine("Buzul: " + blocks.Count + " buz bloğu (karbonla küçülür), izleme istasyonu, kar yağışı");
    }

    // ---------------------------------------------------------------- vegetation

    private static void BuildVegetation()
    {
        var root = Groups["Vegetation"];
        var leafy = BuildSet(new[] { "tree" }, new[] { "palm", "pine", "plateau", "stump", "log", "cone", "tent", "house", "fall", "dead" }, KenneyFolders());
        var pines = BuildSet(new[] { "pine", "cone" }, new[] { "log", "stump", "fall" }, KenneyFolders());
        var rocks = BuildSet(new[] { "rock", "stone" }, new[] { "stepping", "path", "crystal", "tile" }, KenneyFolders());
        var plants = BuildSet(new[] { "bush", "flower", "plant", "mushroom" }, new[] { "tile", "pot" }, KenneyFolders());
        report.AppendLine("Bitki modelleri: yapraklı=" + leafy.models.Count + " çam=" + pines.models.Count + " kaya=" + rocks.models.Count + " bitki=" + plants.models.Count);

        var trees = Group("Trees", root); var stones = Group("Rocks", root); var small = Group("Plants", root);
        var heritageCount = new Dictionary<int, int>();
        int treeCount = 0, rockCount = 0, plantCount = 0;

        for (float x = 8f; x < Size - 8f && treeCount < 1100; x += 7f)
            for (float z = 8f; z < Size - 8f && treeCount < 1100; z += 7f)
            {
                float px = x + F(-3f, 3f), pz = z + F(-3f, 3f);
                if (!CanPlant(px, pz, 3.5f)) continue;
                float h = GroundY(px, pz);
                if (h < WaterY + 1.2f || h > 75f) continue;
                if (terrainData.GetSteepness(px / Size, pz / Size) > 28f) continue;
                int ri = RegionIndexAt(px, pz);
                float density = ri < 0 ? 0f : Defs[ri].kind switch
                {
                    EcoRegionInfo.Kind.Wind => 0.05f, EcoRegionInfo.Kind.Sun => 0.02f, EcoRegionInfo.Kind.Industry => 0.012f,
                    EcoRegionInfo.Kind.Glacier => h < 50f ? 0.2f : 0f, EcoRegionInfo.Kind.Lake => ri == 5 ? 0.22f : 0.09f, _ => 0f,
                };
                float edge = Mathf.Min(Mathf.Min(px, pz), Mathf.Min(Size - px, Size - pz));
                density += (1f - Smooth(0f, 95f, edge)) * 0.45f;
                density *= 0.35f + 1.65f * Fbm(px * 0.02f, pz * 0.02f, 3);
                if (rng.NextDouble() > density) continue;
                bool pine = h > 36f || pz > 430f || edge < 110f || rng.NextDouble() < 0.12;
                bool heritage = ri >= 0 && Defs[ri].kind != EcoRegionInfo.Kind.Industry && Defs[ri].kind != EcoRegionInfo.Kind.Glacier
                    && (!heritageCount.ContainsKey(ri) || heritageCount[ri] < 3) && edge > 60f;
                var tree = PlaceTree(trees, new Vector3(px, h, pz), pine ? pines : leafy, pine ? leafy : pines, pine, heritage);
                if (tree == null) continue;
                treeCount++;
                if (heritage) heritageCount[ri] = heritageCount.TryGetValue(ri, out int c) ? c + 1 : 1;
            }

        // City: a tree on every sidewalk corner plus an avenue of trees along the main axis.
        for (float z = 285f; z < 440f && treeCount < 1250; z += 15f)
            foreach (float x in new[] { 311f, 329f })
            {
                if (Vector2.Distance(new Vector2(x, z), new Vector2(320f, 350f)) < 18f) continue;
                if (PlaceTree(trees, new Vector3(x, GroundY(x, z), z), leafy, pines, false, treeCount % 7 == 0) != null) treeCount++;
            }
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f, x = 320f + Mathf.Cos(a) * 14f, z = 350f + Mathf.Sin(a) * 14f;
            if (PlaceTree(trees, new Vector3(x, GroundY(x, z), z), leafy, pines, false, i < 3) != null) treeCount++;
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
                if (RegionIndexAt(tx, tz) != regionIndex || !CanPlant(tx, tz, 4f)) continue;
                float ty = GroundY(tx, tz);
                if (ty < WaterY + 2f || ty > 50f || terrainData.GetSteepness(tx / Size, tz / Size) > 20f) continue;
                if (PlaceTree(trees, new Vector3(tx, ty, tz), leafy, pines, false, true) == null) continue;
                have++; treeCount++;
            }
        }

        for (float x = 10f; x < Size - 10f && rockCount < 450; x += 12f)
            for (float z = 10f; z < Size - 10f && rockCount < 450; z += 12f)
            {
                float px = x + F(-5f, 5f), pz = z + F(-5f, 5f);
                if (!CanPlant(px, pz, 2.5f)) continue;
                float h = GroundY(px, pz);
                if (h < WaterY - 1f) continue;
                int ri = RegionIndexAt(px, pz);
                float edge = Mathf.Min(Mathf.Min(px, pz), Mathf.Min(Size - px, Size - pz));
                float prob = ri < 0 ? 0f : Defs[ri].kind switch
                {
                    EcoRegionInfo.Kind.Glacier => 0.16f, EcoRegionInfo.Kind.Wind => 0.07f, EcoRegionInfo.Kind.Lake => 0.1f,
                    EcoRegionInfo.Kind.Industry => 0.03f, EcoRegionInfo.Kind.Sun => 0.02f, _ => 0.005f,
                };
                prob += (1f - Smooth(0f, 90f, edge)) * 0.2f;
                if (rng.NextDouble() > prob) continue;
                float size = ri >= 0 && Defs[ri].kind == EcoRegionInfo.Kind.Glacier ? F(1.5f, 5f) : F(0.8f, 3f);
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
            for (float x = 10f; x < Size - 10f && plantCount < 320; x += 9f)
                for (float z = 10f; z < Size - 10f && plantCount < 320; z += 9f)
                {
                    float px = x + F(-4f, 4f), pz = z + F(-4f, 4f);
                    int ri = RegionIndexAt(px, pz);
                    if (ri < 0 || !CanPlant(px, pz, 2f)) continue;
                    var kind = Defs[ri].kind;
                    if (kind != EcoRegionInfo.Kind.Sun && kind != EcoRegionInfo.Kind.Wind && !(kind == EcoRegionInfo.Kind.Lake && ri == 5)) continue;
                    if (rng.NextDouble() > 0.1) continue;
                    float h = GroundY(px, pz);
                    if (h < WaterY + 0.8f || terrainData.GetSteepness(px / Size, pz / Size) > 20f) continue;
                    int idx = rng.Next(plants.models.Count);
                    var plant = SpawnModel(plants.models[idx], small, new Vector3(px, h, pz), F(0f, 360f), F(0.5f, 1.0f) * plants.heights[idx] / plants.average, 0f, 0f, false);
                    if (plant != null) plantCount++;
                }
        report.AppendLine("Bitki örtüsü: " + treeCount + " ağaç, " + rockCount + " kaya, " + plantCount + " küçük bitki. "
            + "EcoVegetation (kuş/kelebek için canlı ağaç) yalnız bölge başına en çok 3 ağaçta; diğerleri dekor.");
    }

    private static bool CanPlant(float x, float z, float roadMargin)
    {
        if (x < 6f || z < 6f || x > Size - 6f || z > Size - 6f) return false;
        if (RoadEdgeDistance(x, z) < roadMargin) return false;
        if (Vector2.Distance(new Vector2(x, z), Base) < 46f) return false;
        if (x > 212f && x < 428f && z > 270f && z < 444f) return false;
        var p = new Vector2(x, z);
        foreach (var r in KeepOut) if (r.Contains(p)) return false;
        return true;
    }

    private static GameObject PlaceTree(Transform parent, Vector3 pos, ModelSet primary, ModelSet fallback, bool pine, bool heritage)
    {
        var set = primary.Any ? primary : fallback;
        float baseHeight = pine ? F(7f, 12f) : F(5f, 9f);
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
                    string path = Folder + "Materials/URP_" + safe + "_" + (source.GetInstanceID() & 0xffff) + ".mat";
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
    }

    private static void BuildSigns()
    {
        var root = Groups["Props"];
        AddSignpost(root, new Vector2(Base.x + 8f, Base.y + 34f), "BAŞLANGIÇ KAVŞAĞI",
            new[]
            {
                "KUZEY  Şehir  " + Mathf.RoundToInt(350f - Base.y) + " m",
                "BATI  Rüzgârlı Alan  " + Mathf.RoundToInt(Vector2.Distance(Base, new Vector2(110f, 300f))) + " m",
                "DOĞU  Sanayi  " + Mathf.RoundToInt(Vector2.Distance(Base, new Vector2(520f, 340f))) + " m",
                "KUZEY  Buzul  " + Mathf.RoundToInt(500f - Base.y) + " m",
            });
        AddSignpost(root, new Vector2(327f, 286f), "ŞEHİR GİRİŞİ", new[] { "KUZEY  Meydan  " + 64 + " m", "BATI  Rüzgâr türbinleri", "DOĞU  Fabrikalar" });
        AddSignpost(root, new Vector2(332f, 104f), "GÖL YOLLARI", new[] { "BATI  Temiz Göl (balık)", "DOĞU  Kirli Göl (arıtma)" });
    }

    private static void AddSignpost(Transform parent, Vector2 xz, string title, string[] lines)
    {
        var sign = Group("Signpost", parent); sign.position = new Vector3(xz.x, GroundY(xz.x, xz.y), xz.y);
        var wood = Mat("SignWood", new Color(0.4f, 0.28f, 0.16f), 0.1f);
        Prim(PrimitiveType.Cylinder, "Post", sign, new Vector3(0f, 1.8f, 0f), new Vector3(0.22f, 1.8f, 0.22f), wood, true);
        Prim(PrimitiveType.Cube, "Board", sign, new Vector3(0f, 3.2f, 0f), new Vector3(3.6f, 1.5f + lines.Length * 0.15f, 0.12f), Mat("SignBoard", new Color(0.12f, 0.2f, 0.18f), 0.1f));
        WorldLabel(sign, title + "\n" + string.Join("\n", lines), new Vector3(0f, 3.25f, -0.2f), 0.0042f, 34);
    }

    // ---------------------------------------------------------------- base & HUD

    private static void PlaceBase()
    {
        GameObject alan = null;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) if (root.name == "alan") alan = root;
        if (alan == null) { report.AppendLine("UYARI: 'alan' grubu bulunamadı; oyuncu taşınamadı."); return; }
        alan.transform.position = new Vector3(Base.x, GroundY(Base.x, Base.y) + 0.1f, Base.y);
        // The tutorial pockets inside the mechanic station keep priority over the big regions around them.
        foreach (var region in alan.GetComponentsInChildren<EcoRegion>(true)) region.priority = 5;
        report.AppendLine("Başlangıç: 'alan' grubu (oyuncu, istasyon, deneme fabrikası/yangın/kirli su) " + Base + " konumuna taşındı");
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
        File.WriteAllBytes(mapPath, BakeMinimap().EncodeToPNG());
        AssetDatabase.ImportAsset(mapPath);
        var mapImporter = (TextureImporter)AssetImporter.GetAtPath(mapPath);
        mapImporter.textureType = TextureImporterType.Default; mapImporter.mipmapEnabled = false; mapImporter.wrapMode = TextureWrapMode.Clamp;
        mapImporter.filterMode = FilterMode.Bilinear; mapImporter.textureCompression = TextureImporterCompression.Uncompressed;
        mapImporter.maxTextureSize = 1024;
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
        report.AppendLine("Arayüz: EcoWorldHUD eklendi; EcoQuestHUD panel sprite'ı yuvarlatıldı: " + (existing != null));
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

    private static Texture2D BakeMinimap()
    {
        const int n = 512;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var pixels = new Color[n * n];
        var light = new Vector3(-0.55f, 0.75f, 0.45f).normalized;
        var clean = new Color(0.30f, 0.62f, 0.78f); var dirty = new Color(0.40f, 0.44f, 0.24f);
        for (int iz = 0; iz < n; iz++)
            for (int ix = 0; ix < n; ix++)
            {
                float u = (ix + 0.5f) / n, v = (iz + 0.5f) / n, x = u * Size, z = v * Size;
                float h = terrainData.GetInterpolatedHeight(u, v);
                var normal = terrainData.GetInterpolatedNormal(u, v);
                float shade = Mathf.Clamp01(Vector3.Dot(normal, light)) * 0.75f + 0.38f;
                int ri = RegionIndexAt(x, z);
                Color tint = ri >= 0 ? Defs[ri].color : Color.gray;
                Color ground = new Color(0.34f, 0.5f, 0.28f);
                if (ri >= 0)
                    ground = Defs[ri].kind switch
                    {
                        EcoRegionInfo.Kind.Sun => new Color(0.62f, 0.58f, 0.32f),
                        EcoRegionInfo.Kind.Industry => new Color(0.42f, 0.36f, 0.3f),
                        EcoRegionInfo.Kind.City => new Color(0.52f, 0.52f, 0.48f),
                        EcoRegionInfo.Kind.Glacier => new Color(0.82f, 0.9f, 0.95f),
                        _ => new Color(0.32f, 0.5f, 0.27f),
                    };
                Color c = Color.Lerp(ground, tint, 0.28f);
                float rock = Mathf.InverseLerp(30f, 52f, terrainData.GetSteepness(u, v));
                c = Color.Lerp(c, new Color(0.5f, 0.49f, 0.5f), rock * 0.7f);
                c = Color.Lerp(c, Color.white, Mathf.InverseLerp(50f, 72f, h) * 0.85f);
                c *= shade;
                if (h < WaterY)
                {
                    bool east = Vector2.Distance(new Vector2(x, z), LakeB) < Vector2.Distance(new Vector2(x, z), LakeA);
                    c = Color.Lerp(east ? dirty : clean, new Color(0.08f, 0.2f, 0.32f), Mathf.InverseLerp(WaterY, 10f, h) * 0.5f) * (0.85f + shade * 0.2f);
                }
                float road = Mathf.Max(RoadPaint(x, z, false), RoadPaint(x, z, true) * 0.8f);
                if (road > 0.05f) c = Color.Lerp(c, new Color(0.9f, 0.88f, 0.8f), road * 0.9f);
                // Region borders.
                const float e = 2.2f;
                if (RegionIndexAt(x + e, z) != ri || RegionIndexAt(x - e, z) != ri || RegionIndexAt(x, z + e) != ri || RegionIndexAt(x, z - e) != ri)
                    c = Color.Lerp(c, new Color(0.04f, 0.08f, 0.1f), 0.6f);
                c.a = 1f;
                pixels[iz * n + ix] = c;
            }
        tex.SetPixels(pixels); tex.Apply();
        return tex;
    }

    private static void RegisterInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == TargetScene)) return;
        scenes.Add(new EditorBuildSettingsScene(TargetScene, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        report.AppendLine("Build Settings: EcoWorld sahnesi listeye eklendi (diğer sahnelerin sırası değişmedi).");
    }
}
