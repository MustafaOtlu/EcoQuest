#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SeedVisibilityCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Seed visibility check").AddComponent<SeedVisibilityCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/SeedVisibilityValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Raised test soil"; ground.transform.position = new Vector3(0, 1.9f, 0);
        ground.transform.localScale = new Vector3(18, 0.2f, 12);
        ground.AddComponent<PlantingSurface>();
        var camera = new GameObject("Visibility camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 3.7f, -5);
        camera.fieldOfView = 65; camera.aspect = 16f / 9f;
        var planted = new PlantedSeed[EcoCropCatalog.Count];
        for (int i = 0; i < planted.Length; i++)
        {
            var kind = (EcoCropCatalog.Kind)i;
            Vector3 soilPoint = new Vector3(-3 + i * 2, 2.01f, 0);
            var crop = planted[i] = PlantedSeed.Create(soilPoint, kind);
            Check(crop != null, kind + " creates a plant from the actual Resources prefab");
            crop.enabled = false;
            var renderers = crop.transform.Find("Crop Visual").GetComponentsInChildren<Renderer>();
            Check(renderers.Length > 0 && renderers.All(r => r.enabled && r.gameObject.activeInHierarchy
                && r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m != null && m.shader != null)),
                kind + " has active mesh renderers and resolved materials before watering");
            var bounds = BoundsOf(crop);
            Check(bounds.size.y >= (kind == EcoCropCatalog.Kind.Tree ? 0.99f : 0.279f),
                kind + " starts at a readable physical height (" + bounds.size.y + " m)");
            Grounded(crop, soilPoint, kind + " initial pivot");
            camera.transform.LookAt(bounds.center);
            Vector3 bottom = camera.WorldToViewportPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            Vector3 top = camera.WorldToViewportPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z));
            Check(bottom.z > 0 && top.y - bottom.y > 0.025f,
                kind + " occupies a visible screen height from ordinary first-person distance");
            crop.Simulate(12);
            Check(crop.gameObject.activeInHierarchy && crop.health > 0 && Near(crop.growth, 0)
                && Near(BoundsOf(crop).size.y, bounds.size.y), kind + " remains visible while waiting for its first watering");
            crop.growth = 0.5f; crop.WaterPlant(0.01f, true);
            Check(BoundsOf(crop).size.y > bounds.size.y, kind + " visibly increases in size during growth");
            Grounded(crop, soilPoint, kind + " half-grown pivot");
            crop.growth = 1; crop.WaterPlant(0.01f, true);
            float fullHeight = kind == EcoCropCatalog.Kind.Tree ? 3.4f : kind == EcoCropCatalog.Kind.Carrot ? 0.4f : 0.38f;
            Check(Mathf.Abs(BoundsOf(crop).size.y - fullHeight) < 0.015f, kind + " preserves its authored full-grown dimensions");
            Grounded(crop, soilPoint, kind + " mature pivot");
            crop.growth = float.NaN; crop.WaterPlant(0.01f, true);
            Check(Finite(crop.transform.Find("Crop Visual").localScale) && Finite(BoundsOf(crop).size),
                kind + " rejects nonfinite growth when computing render scale");
            crop.growth = 0; crop.WaterPlant(float.NaN, true);
            Check(float.IsFinite(crop.moisture), kind + " rejects invalid watering without poisoning moisture");
            crop.WaterPlant(0.01f, true);
        }
        Check(PlantedSeed.Create(new Vector3(float.NaN, 0, 0)) == null
            && PlantedSeed.Create(Vector3.zero, (EcoCropCatalog.Kind)99) == null,
            "Invalid planting coordinates or species cannot create invisible orphan plants");

        // An off-centre imported wrapper with a non-unit scale must grow about the soil, not its mesh pivot.
        var root = new GameObject("Offset prefab regression"); root.transform.position = new Vector3(0, 2.01f, 3);
        var visual = new GameObject("Crop Visual"); visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0.6f, -0.9f, -0.4f);
        visual.transform.localScale = new Vector3(1.3f, 0.8f, 1.1f);
        var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.transform.SetParent(visual.transform, false);
        mesh.transform.localPosition = new Vector3(0.3f, 0.2f, -0.1f);
        Destroy(mesh.GetComponent<Collider>());
        var offsetPlant = root.AddComponent<PlantedSeed>(); offsetPlant.enabled = false;
        Grounded(offsetPlant, root.transform.position, "Offset model initial pivot");
        offsetPlant.growth = 0.5f; offsetPlant.WaterPlant(0.01f, true);
        Grounded(offsetPlant, root.transform.position, "Offset model growing pivot");
        offsetPlant.growth = 1; offsetPlant.WaterPlant(0.01f, true);
        Grounded(offsetPlant, root.transform.position, "Offset model mature pivot");
        Check((visual.transform.localScale - new Vector3(1.3f, 0.8f, 1.1f)).sqrMagnitude < 0.00001f,
            "Mature rendering preserves the prefab wrapper's authored scale");

        var treeSeed = planted[(int)EcoCropCatalog.Kind.Tree];
        GameObject treeObject = treeSeed.gameObject;
        treeSeed.growth = 0.999f; treeSeed.moisture = 1; treeSeed.Simulate(1);
        yield return null;
        var vegetation = treeObject.GetComponent<EcoVegetation>();
        var trunk = treeObject.GetComponent<CapsuleCollider>();
        Check(treeObject.GetComponent<PlantedSeed>() == null && vegetation != null && vegetation.health > 0,
            "A mature sapling still becomes a permanent ecosystem tree");
        Bounds mature = RendererBounds(treeObject.GetComponentsInChildren<Renderer>());
        Check(Near(mature.min.y, treeObject.transform.position.y) && Mathf.Abs(mature.size.y - trunk.height) < 0.015f,
            "Permanent tree retains a grounded full-height model matching its trunk collider");
        File.WriteAllText("seed-visibility-check-success.txt", report); EditorApplication.Exit(0);
    }
    private void Grounded(PlantedSeed crop, Vector3 ground, string label)
    {
        Bounds bounds = BoundsOf(crop);
        Check(Near(bounds.min.y, ground.y) && Near(bounds.center.x, ground.x) && Near(bounds.center.z, ground.z)
            && (crop.transform.position - ground).sqrMagnitude < 0.00001f, label + " stays centred on its original soil point");
    }
    private static Bounds BoundsOf(PlantedSeed crop) => RendererBounds(crop.transform.Find("Crop Visual").GetComponentsInChildren<Renderer>());
    private static Bounds RendererBounds(Renderer[] renderers)
    {
        Bounds result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.004f;
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    private void Check(bool condition, string label)
    { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception) return;
        File.WriteAllText("seed-visibility-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
