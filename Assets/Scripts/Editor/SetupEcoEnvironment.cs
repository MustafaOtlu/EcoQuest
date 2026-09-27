using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupEcoEnvironment
{
    private const string Folder = "Assets/Resources/EcoEnvironment/";
    [MenuItem("EcoQuest/Set up regional ecology")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        const string path = "Assets/Resources/EcoInfrastructure/PowerStarter.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { Bind(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("ECO_ENVIRONMENT_READY");
    }
    public static void Bind(GameObject root)
    {
        if (root.transform.Find("Regional ecology") != null) return;
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var ecology = new GameObject("Regional ecology"); ecology.transform.SetParent(root.transform, false);
        var soil = Mat("Ground", new Color(0.32f, 0.52f, 0.24f));
        var metal = Mat("Factory", new Color(0.38f, 0.42f, 0.42f));
        var dark = Mat("Roof", new Color(0.16f, 0.19f, 0.2f));
        var green = Mat("Filter", new Color(0.2f, 0.62f, 0.4f));
        Region(ecology.transform, "Sağlıklı koruluk", new Vector3(0, 0, 8), false, soil);
        Region(ecology.transform, "Kirli sanayi alanı", new Vector3(32, 0, 8), true, soil);
        var factory = new GameObject("Unfiltered factory"); factory.transform.SetParent(ecology.transform, false);
        factory.transform.localPosition = new Vector3(26, 0, 8);
        Shape(factory.transform, "Workshop", new Vector3(0, 1.6f, 0), new Vector3(5, 3.2f, 4), metal);
        Shape(factory.transform, "Roof", new Vector3(0, 3.3f, 0), new Vector3(5.4f, 0.2f, 4.4f), dark);
        Shape(factory.transform, "Door", new Vector3(0, 1.15f, -2.03f), new Vector3(1.3f, 2.3f, 0.08f), dark);
        Shape(factory.transform, "Chimney", new Vector3(1.5f, 4, 1), new Vector3(0.7f, 4, 0.7f), dark);
        var collider = factory.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.6f, 0); collider.size = new Vector3(5, 3.2f, 4);
        var filter = Shape(factory.transform, "Installed air filter", new Vector3(-1.6f, 1.2f, -2.25f), new Vector3(0.9f, 1.6f, 0.4f), green);
        var component = factory.AddComponent<EcoFactory>(); component.filterVisual = filter;
        component.smoke = Particles(factory.transform, new Vector3(1.5f, 6, 1), false);
        filter.SetActive(false);
        Label(factory.transform, "FABRİKA\n[E] Büyük filtre tak", new Vector3(0, 3.8f, -2.1f));
        var water = Shape(ecology.transform, "Natural polluted water", new Vector3(32, 0.03f, 2), new Vector3(4, 0.05f, 4), Mat("Water", new Color(0.24f, 0.38f, 0.28f)));
        water.AddComponent<BoxCollider>(); var source = water.AddComponent<WeaponWaterSource>(); source.useRegionalQuality = true;
        source.statusLabel = Label(ecology.transform, "KİRLİ SU\nArıtmadan kullanma", new Vector3(32, 1.2f, 2));
        var burning = new GameObject("Brush fire"); burning.transform.SetParent(ecology.transform, false); burning.transform.localPosition = new Vector3(22, 0.3f, -3);
        var fire = burning.AddComponent<EcoFire>(); fire.fuel = 300; fire.flames = Particles(burning.transform, Vector3.zero, true);
        var fireCollider = burning.AddComponent<BoxCollider>(); fireCollider.center = Vector3.up * 0.4f; fireCollider.size = new Vector3(1.8f, 1.5f, 1.8f);
        Label(burning.transform, "YANGIN\nVakum → Su", new Vector3(0, 2, 0));
        foreach (var position in new[] { new Vector3(22.8f, 0, -2.5f), new Vector3(-10, 0, 8), new Vector3(-12, 0, 11) })
        {
            var tree = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EcoArt/Tree.prefab"));
            tree.transform.SetParent(ecology.transform, false); tree.transform.localPosition = position;
            var vegetation = tree.AddComponent<EcoVegetation>(); vegetation.foliage = tree.GetComponentsInChildren<Renderer>();
            if (tree.GetComponentInChildren<Collider>() == null) { var trunk = tree.AddComponent<CapsuleCollider>(); trunk.height = 3; trunk.radius = 0.4f; trunk.center = Vector3.up * 1.5f; }
        }
        BuildEcoNatureArt.BindEnvironment(root);
    }
    private static void Region(Transform parent, string name, Vector3 position, bool dirty, Material ground)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var region = go.AddComponent<EcoRegion>(); region.regionName = name; region.size = new Vector2(32, 40);
        region.airPollution = dirty ? 65 : 0; region.waterPollution = dirty ? 70 : 0; region.soilPollution = dirty ? 65 : 0;
        region.carbon = dirty ? 45 : 0; region.vegetation = dirty ? 0.15f : 0.8f; region.sensitiveHabitat = !dirty;
        var patch = Shape(go.transform, "Condition sample ground", new Vector3(dirty ? -6 : -10, 0.012f, 0), new Vector3(10, 0.02f, 10), ground);
        region.groundVisual = patch.GetComponent<Renderer>();
        Label(go.transform, name + "\n[4] Tarayıcıyla zemini tara", new Vector3(dirty ? -6 : -10, 1.5f, -5));
    }
    private static Material Mat(string name, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Folder + name + ".mat"); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.2f); return material;
    }
    private static GameObject Shape(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size; Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    private static ParticleSystem Particles(Transform parent, Vector3 position, bool fire)
    {
        var go = new GameObject(fire ? "Flames" : "Chimney smoke"); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var particles = go.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.startLifetime = fire ? 0.8f : 3.5f; main.startSpeed = fire ? 1.1f : 1.3f;
        main.startSize = fire ? 0.4f : 0.8f; main.maxParticles = 100; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = fire ? new Color(1, 0.45f, 0.04f, 0.85f) : new Color(0.12f, 0.1f, 0.08f, 0.8f);
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 15; shape.radius = fire ? 0.65f : 0.25f;
        shape.rotation = new Vector3(-90, 0, 0);
        var emission = particles.emission; emission.rateOverTime = fire ? 18 : 7;
        var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 0.4f, 1, fire ? 0 : 2));
        var colors = particles.colorOverLifetime; colors.enabled = true;
        var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); colors.color = gradient;
        var material = Mat("Particle", Color.white); material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "SoftParticle.asset");
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Soft particle", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float distance = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - distance), 1.5f)));
            }
            texture.Apply(); AssetDatabase.CreateAsset(texture, Folder + "SoftParticle.asset");
        }
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0); material.SetFloat("_SrcBlend", 5); material.SetFloat("_DstBlend", 10); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; return particles;
    }
    private static Text Label(Transform parent, string content, Vector3 position)
    {
        var go = new GameObject("Ecology sign", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = Vector3.one * 0.006f;
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(Text)); caption.transform.SetParent(go.transform, false);
        caption.GetComponent<RectTransform>().sizeDelta = new Vector2(1000, 120);
        var text = caption.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 36;
        text.alignment = TextAnchor.MiddleCenter; text.color = new Color(1, 0.92f, 0.65f); text.text = content; text.raycastTarget = false;
        return text;
    }
}
