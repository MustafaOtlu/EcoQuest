#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class LowPolyWaterCheck : MonoBehaviour
{
    private const string Pack = "Assets/LowPolyWater_Pack/";
    public Mesh source;
    public int originalCount;
    public Vector3 originalFirst;
    private string report = "";
    public static void Begin()
    {
        Application.logMessageReceived += EarlyLog;
        AssetDatabase.Refresh();
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        if (pipeline == null) throw new Exception("PC URP pipeline is missing");
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
        EditorSceneManager.OpenScene(Pack + "_Demo/DemoScene.unity");
        foreach (var obj in FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            foreach (var component in obj.GetComponents<Component>())
                if (component == null) throw new Exception("Missing demo component on " + obj.name);
        var water = FindFirstObjectByType<LowPolyWater.LowPolyWater>();
        if (water == null) throw new Exception("Demo water is missing");
        var runner = new GameObject("Water compatibility check").AddComponent<LowPolyWaterCheck>();
        runner.source = water.GetComponent<MeshFilter>().sharedMesh;
        runner.originalCount = runner.source.vertexCount;
        runner.originalFirst = runner.source.vertices[0];
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/WaterValidation.unity");
        EditorApplication.isPlaying = true;
    }
    public static void UpgradeDemo()
    {
        Application.logMessageReceived += EarlyLog;
        EditorSceneManager.OpenScene(Pack + "_Demo/DemoScene.unity");
        var water = FindFirstObjectByType<LowPolyWater.LowPolyWater>();
        var islandMaterial = AssetDatabase.LoadAssetAtPath<Material>(Pack + "LowPoly Island/Materials/IslandMat.mat");
        if (water == null || islandMaterial == null || islandMaterial.GetTexture("_BaseMap") == null)
            throw new Exception("Demo water or island texture is missing");
        // The old Unity 2017 OBJ-prefab instance is lost when Unity 6 serializes the scene.
        // Recreate that demo instance from the actual imported model when necessary.
        bool hasIsland = false;
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (renderer.GetComponent<LowPolyWater.LowPolyWater>() == null) hasIsland = true;
        if (!hasIsland)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "LowPoly Island/island1_design2_c4d.obj");
            if (model == null) throw new Exception("Imported demo island model is missing");
            var island = (GameObject)PrefabUtility.InstantiatePrefab(model);
            island.name = "Demo Island";
            var renderers = island.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) throw new Exception("Imported island has no mesh renderer");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            island.transform.localScale *= 55 / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            island.transform.position -= new Vector3(bounds.center.x, bounds.min.y - 0.5f, bounds.center.z);
        }
        foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (renderer.GetComponent<LowPolyWater.LowPolyWater>() != null) continue;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = islandMaterial;
            renderer.sharedMaterials = materials;
        }
        if (!AssetDatabase.IsValidFolder(Pack + "Prefabs"))
            AssetDatabase.CreateFolder(Pack.TrimEnd('/'), "Prefabs");
        PrefabUtility.SaveAsPrefabAsset(water.gameObject, Pack + "Prefabs/LowPolyWater_URP.prefab");
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        File.WriteAllText("low-poly-water-upgrade.txt", "Demo island texture connected; reusable URP water prefab saved.");
        EditorApplication.Exit(0);
    }
    private static void EarlyLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("low-poly-water-failed.txt", message + "\n" + stack);
        EditorApplication.Exit(1);
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        Check(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset, "Demo uses the project URP pipeline");
        Check(source != null, "Imported ocean mesh loads");
        var water = FindFirstObjectByType<LowPolyWater.LowPolyWater>();
        MeshRenderer island = null;
        foreach (var candidate in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (candidate.GetComponent<LowPolyWater.LowPolyWater>() == null) { island = candidate; break; }
        Check(island != null && island.sharedMaterial.GetTexture("_BaseMap") != null, "Demo island survives scene reload with its color texture");
        var second = new GameObject("Independent second water");
        second.transform.position = new Vector3(2000, 0, 0);
        second.AddComponent<MeshFilter>().sharedMesh = source;
        second.AddComponent<MeshRenderer>().sharedMaterial = water.GetComponent<MeshRenderer>().sharedMaterial;
        var other = second.AddComponent<LowPolyWater.LowPolyWater>(); other.waveHeight = 0.1f; other.waveLength = 0;
        yield return null; yield return null;
        var mesh = water.GetComponent<MeshFilter>().sharedMesh;
        Check(mesh != source && other.GetComponent<MeshFilter>().sharedMesh != mesh, "Water objects animate independent mesh copies");
        Check(source.vertexCount == originalCount && source.vertices[0] == originalFirst, "Imported ocean mesh remains unchanged");
        var before = mesh.vertices[0].y;
        yield return new WaitForSeconds(0.2f);
        Check(Mathf.Abs(mesh.vertices[0].y - before) > 0.0001f, "Demo waves animate");
        foreach (var vertex in other.GetComponent<MeshFilter>().sharedMesh.vertices)
            if (!Finite(vertex)) throw new Exception("Zero wave length created an invalid vertex");
        Check(true, "Zero wave length is safe");
        foreach (var vertex in mesh.vertices)
            if (!mesh.bounds.Contains(vertex)) throw new Exception("Water vertex " + vertex + " lies outside its culling bounds " + mesh.bounds);
        Check(true, "Animated bounds contain all wave vertices");
        var camera = Camera.main;
        Check(camera != null, "Demo camera loads without obsolete or missing components");
        foreach (var animation in FindObjectsByType<Animator>(FindObjectsSortMode.None)) animation.enabled = false;
        camera.transform.position = new Vector3(0, 26.6f, -135);
        camera.transform.rotation = Quaternion.Euler(15, 0, 0);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = false;
        var target = new RenderTexture(1600, 900, 24); target.Create(); camera.targetTexture = target;
        yield return null;
        var image = Capture(camera, target);
        var pixels = image.GetPixels32();
        File.WriteAllBytes("low-poly-water-preview.png", image.EncodeToPNG());
        Destroy(image);
        island.enabled = false;
        image = Capture(camera, target); var withoutIsland = image.GetPixels32(); Destroy(image);
        island.enabled = true;
        int islandPixels = 0;
        for (int i = 0; i < pixels.Length; i++)
            if (Mathf.Abs(pixels[i].r-withoutIsland[i].r) + Mathf.Abs(pixels[i].g-withoutIsland[i].g) + Mathf.Abs(pixels[i].b-withoutIsland[i].b) > 25) islandPixels++;
        Check(islandPixels > 500, "Textured island is visible in the demo (" + islandPixels + " pixels)");
        var renderer = water.GetComponent<MeshRenderer>();
        renderer.enabled = false;
        image = Capture(camera, target); var background = image.GetPixels32(); Destroy(image);
        renderer.enabled = true;
        int changed = 0, blue = 0, pink = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            var p = pixels[i]; var b = background[i];
            if (p.r > 210 && p.b > 210 && p.g < 60) pink++;
            if (Mathf.Abs(p.r-b.r) + Mathf.Abs(p.g-b.g) + Mathf.Abs(p.b-b.b) < 25) continue;
            changed++;
            if (p.g > p.r * 1.08f && p.b > p.r * 1.08f) blue++;
        }
        Check(changed > 10000 && blue > 5000, "Water renders visibly without camera depth (" + changed + " changed, " + blue + " blue pixels)");
        Check(pink < 50, "Demo water and island do not render with an error shader");
        var shader = renderer.sharedMaterial.shader;
        Check(shader.isSupported && !ShaderUtil.ShaderHasError(shader), "URP water shader compiles and is supported");
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        var shore = new Material(renderer.sharedMaterial);
        shore.SetFloat("_UseShoreDepth", 1); shore.EnableKeyword("WATER_EDGEBLEND_ON");
        renderer.sharedMaterial = shore;
        yield return null;
        image = Capture(camera, target); Destroy(image);
        Check(!ShaderUtil.ShaderHasError(shader), "Optional depth shoreline variant renders without shader errors");
        RenderTexture.active = null; camera.targetTexture = null; target.Release(); Destroy(target); Destroy(shore);
        File.WriteAllText("low-poly-water-success.txt", report);
        EditorApplication.Exit(0);
    }
    private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
        && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    private static Texture2D Capture(Camera camera, RenderTexture target)
    {
        camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
        return image;
    }
    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        report += "PASS " + label + "\n";
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("low-poly-water-failed.txt", report + message + "\n" + stack);
        EditorApplication.Exit(1);
    }
}
#endif
