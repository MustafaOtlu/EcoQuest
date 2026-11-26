using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// GDD v5 §3 (valley traffic): builds the townsfolk and car prefabs that EcoTraffic loads from Resources/EcoTraffic.
/// The walkers reuse the project's own character models with a looping walk; the car is a small low-poly model made of
/// primitives so it matches the stylised art without a third-party pack.
/// </summary>
public static class BuildEcoTraffic
{
    private const string Folder = "Assets/Resources/EcoTraffic";
    private const string Male = "Assets/3D Assets/CHARACTER/MaleCharacter/Animasyon/Walking Male.fbx";
    private const string Female = "Assets/3D Assets/CHARACTER/FemaleCharacter/Animals/Walking Female.fbx";

    [MenuItem("EcoQuest/Build Traffic Assets")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "EcoTraffic");
        Walker("Townsfolk Male", Male);
        Walker("Townsfolk Female", Female);
        Car();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EcoTraffic assets built in " + Folder);
    }

    private static void Walker(string name, string modelPath)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        AnimationClip source = null;
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
            if (asset is AnimationClip c && !c.name.StartsWith("__preview__")) source = c;
        if (model == null || source == null) { Debug.LogError("Traffic walker source missing: " + modelPath); return; }

        // A looping copy: the female walk is imported without loop time, and the import settings stay untouched.
        string clipPath = Folder + "/" + name + " Walk.anim";
        var clip = Object.Instantiate(source);
        clip.name = name + " Walk";
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        AssetDatabase.DeleteAsset(clipPath);
        AssetDatabase.CreateAsset(clip, clipPath);

        string controllerPath = Folder + "/" + name + ".controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var state = controller.layers[0].stateMachine.AddState("Walk");
        state.motion = clip;
        controller.layers[0].stateMachine.defaultState = state;

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = name;
        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        PrefabUtility.SaveAsPrefabAsset(go, Folder + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }

    private static void Car()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        Material Mat(string n, Color c, float smooth)
        {
            string path = Folder + "/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }
        var paint = Mat("Car Paint", Color.white, 0.55f);
        var glass = Mat("Car Glass", new Color(0.18f, 0.24f, 0.3f), 0.85f);
        var tyre = Mat("Car Tyre", new Color(0.08f, 0.08f, 0.09f), 0.1f);
        var light = Mat("Car Light", new Color(1f, 0.95f, 0.75f), 0.6f);
        var trim = Mat("Car Trim", new Color(0.25f, 0.25f, 0.27f), 0.3f);

        var root = new GameObject("Car");
        GameObject Part(string n, PrimitiveType type, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var p = GameObject.CreatePrimitive(type);
            p.name = n; p.transform.SetParent(root.transform, false);
            p.transform.localPosition = pos; p.transform.localScale = scale; p.transform.localEulerAngles = euler;
            Object.DestroyImmediate(p.GetComponent<Collider>());
            p.GetComponent<MeshRenderer>().sharedMaterial = m;
            return p;
        }
        // About 2.3 m long: the player model is ~1.15 m tall, so the car keeps the same scale.
        Part("Body", PrimitiveType.Cube, new Vector3(0, 0.42f, 0), new Vector3(1.15f, 0.42f, 2.3f), paint);
        Part("Cabin", PrimitiveType.Cube, new Vector3(0, 0.8f, -0.12f), new Vector3(1.0f, 0.38f, 1.25f), paint);
        Part("Windscreen", PrimitiveType.Cube, new Vector3(0, 0.8f, 0.5f), new Vector3(0.92f, 0.3f, 0.04f), glass, new Vector3(-18f, 0, 0));
        Part("Rear window", PrimitiveType.Cube, new Vector3(0, 0.8f, -0.74f), new Vector3(0.92f, 0.28f, 0.04f), glass, new Vector3(15f, 0, 0));
        Part("Side windows", PrimitiveType.Cube, new Vector3(0, 0.82f, -0.12f), new Vector3(1.02f, 0.24f, 1.05f), glass);
        Part("Bumper front", PrimitiveType.Cube, new Vector3(0, 0.28f, 1.16f), new Vector3(1.18f, 0.14f, 0.08f), trim);
        Part("Bumper rear", PrimitiveType.Cube, new Vector3(0, 0.28f, -1.16f), new Vector3(1.18f, 0.14f, 0.08f), trim);
        Part("Headlight L", PrimitiveType.Cube, new Vector3(-0.38f, 0.46f, 1.155f), new Vector3(0.22f, 0.1f, 0.03f), light);
        Part("Headlight R", PrimitiveType.Cube, new Vector3(0.38f, 0.46f, 1.155f), new Vector3(0.22f, 0.1f, 0.03f), light);
        foreach (var x in new[] { -0.56f, 0.56f })
            foreach (var z in new[] { -0.72f, 0.72f })
                Part("Wheel", PrimitiveType.Cylinder, new Vector3(x, 0.22f, z), new Vector3(0.44f, 0.08f, 0.44f), tyre, new Vector3(0, 0, 90f));
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Car.prefab");
        Object.DestroyImmediate(root);
    }
}
