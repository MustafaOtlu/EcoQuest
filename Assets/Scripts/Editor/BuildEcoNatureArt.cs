using UnityEditor;
using UnityEngine;
using System.IO;

public static class BuildEcoNatureArt
{
    [MenuItem("EcoQuest/Build crop and wildlife art")]
    public static void Build()
    {
        var food = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/EcoArt/Food Palette.mat");
        BuildEcoArt.Model("Carrot", "Food", "carrot", 0.4f, food, false);
        BuildEcoArt.Model("Tomato", "Food", "tomato", 0.38f, food, false);
        BuildEcoArt.Model("Fish", "Survival", "fish", 0.1f, AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/EcoArt/Survival Palette.mat"), false);
        Creature("Bird", new Color(0.23f, 0.32f, 0.43f), false);
        Creature("Butterfly", new Color(1, 0.57f, 0.15f), true);
        const string path = "Assets/Resources/EcoArt/FieldStation.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var bed in root.GetComponentsInChildren<PlantingSurface>()) bed.allowTrees = false;
            foreach (var node in root.GetComponentsInChildren<Transform>())
                if (node.name == "Tree" && node.GetComponent<EcoVegetation>() == null)
                { var tree = node.gameObject.AddComponent<EcoVegetation>(); tree.foliage = node.GetComponentsInChildren<Renderer>(); }
            foreach (var label in root.GetComponentsInChildren<UnityEngine.UI.Text>())
                if (label.text.StartsWith("EKİM ALANI")) label.text = "EKİM ALANI\n2 · Tohum / R · Tür / 1 · Su";
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        const string powerPath = "Assets/Resources/EcoInfrastructure/PowerStarter.prefab";
        var powerRoot = PrefabUtility.LoadPrefabContents(powerPath);
        try { BindEnvironment(powerRoot); PrefabUtility.SaveAsPrefabAsset(powerRoot, powerPath); }
        finally { PrefabUtility.UnloadPrefabContents(powerRoot); }
        AssetDatabase.SaveAssets(); Debug.Log("ECO_NATURE_ART_READY");
    }
    public static void BindEnvironment(GameObject root)
    {
        foreach (var region in root.GetComponentsInChildren<EcoRegion>())
            if (region.GetComponent<EcoHabitat>() == null) region.gameObject.AddComponent<EcoHabitat>().recovery = region.airPollution < 40 ? 0.5f : 0;
        foreach (var source in root.GetComponentsInChildren<WeaponWaterSource>())
            if (source.useRegionalQuality && source.GetComponent<EcoFishingSpot>() == null) source.gameObject.AddComponent<EcoFishingSpot>().population = 3;
        if (root.transform.Find("Clean fishing pond") != null) return;
        var pond = GameObject.CreatePrimitive(PrimitiveType.Cube); pond.name = "Clean fishing pond"; pond.transform.SetParent(root.transform, false);
        pond.transform.localPosition = new Vector3(-5, 0.03f, 9); pond.transform.localScale = new Vector3(4, 0.05f, 4);
        pond.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/EcoEnvironment/Water.mat");
        pond.AddComponent<WeaponWaterSource>().useRegionalQuality = true; pond.AddComponent<EcoFishingSpot>().population = 6;
    }
    private static void Creature(string name, Color color, bool butterfly)
    {
        string path = "Assets/Resources/EcoArt/";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path + name + ".mat");
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path + name + ".mat"); }
        material.SetColor("_BaseColor", color);
        var root = new GameObject(name);
        Part(root.transform, "Body", Vector3.zero, new Vector3(0.08f, 0.08f, butterfly ? 0.13f : 0.3f), material);
        for (int i = 0; i < 2; i++)
        {
            var pivot = new GameObject(i == 0 ? "Wing L" : "Wing R").transform; pivot.SetParent(root.transform, false);
            var wing = Part(pivot, "Feathers", new Vector3((i == 0 ? -1 : 1) * 0.12f, 0, 0), new Vector3(0.23f, 0.015f, butterfly ? 0.18f : 0.12f), material);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path + name + ".prefab"); Object.DestroyImmediate(root);
    }
    private static GameObject Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
}
