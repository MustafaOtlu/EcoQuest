using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BuildEcoPowerArt
{
    private const string Output = "Assets/Resources/EcoInfrastructure/";
    private static Material metal, panel, cell, white, yellow, green, cable, blue, brown;

    [MenuItem("EcoQuest/Build power equipment")]
    public static void Build()
    {
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        metal = Mat("Frame", new Color(0.25f, 0.3f, 0.34f));
        panel = Mat("PanelFrame", new Color(0.8f, 0.84f, 0.87f));
        cell = Mat("SolarCells", new Color(0.04f, 0.14f, 0.35f));
        white = Mat("Turbine", new Color(0.9f, 0.9f, 0.84f));
        yellow = Mat("Charger", new Color(0.9f, 0.6f, 0.12f));
        green = Mat("Indicator", new Color(0.2f, 0.85f, 0.48f));
        cable = Mat("Cable", new Color(0.08f, 0.09f, 0.1f));
        blue = Mat("WaterTank", new Color(0.12f, 0.5f, 0.67f));
        brown = Mat("DirtyWaterTank", new Color(0.46f, 0.32f, 0.16f));
        var pipeMaterial = Mat("WaterPipe", Color.white);
        pipeMaterial.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Solar(); Wind(); Battery(); Charger(); Intake(); Purifier(); Tank(); Recycling(); Farm();
        var root = new GameObject("Power Starter");
        try
        {
            var clock = root.AddComponent<EcoWorldClock>();
            root.AddComponent<EcoPowerGrid>().clock = clock;
            root.AddComponent<EcoWaterGrid>();
            var solar = Place(root, "SolarPanel", new Vector3(-4, 0.02f, -4));
            var wind = Place(root, "WindTurbine", new Vector3(-6.5f, 0.02f, -6));
            var battery = Place(root, "Battery", new Vector3(-2.6f, 0.02f, -3));
            battery.storedEnergy = 80f; // Starter grant only; new constructed batteries are empty.
            var charger = Place(root, "ChargingStation", new Vector3(-1.5f, 0.02f, -2));
            Connect(root, solar, battery); Connect(root, wind, battery); Connect(root, battery, charger);
            Label(root.transform, "ELEKTRİK İSTASYONU\n[E] Silah bataryasını şarj et", new Vector3(-1.5f, 1.8f, -2));
            for (int i = 0; i < 3; i++)
            {
                var leaves = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EcoArt/GrassPatch.prefab"));
                leaves.name = "Organic leaves " + (i + 1); leaves.transform.SetParent(root.transform, false);
                leaves.transform.localPosition = new Vector3(-0.4f, 0.02f, -3.2f - i * 0.8f); leaves.transform.localScale = Vector3.one * 1.6f;
                var hit = leaves.AddComponent<BoxCollider>(); hit.center = Vector3.up * 0.08f; hit.size = new Vector3(0.4f, 0.16f, 0.4f);
                var resource = leaves.AddComponent<RecyclableResource>(); resource.metal = resource.plastic = 0; resource.organic = 3; resource.processingSeconds = 2;
            }
            PrefabUtility.SaveAsPrefabAsset(root, Output + "PowerStarter.prefab");
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets(); Debug.Log("ECO_POWER_READY: electricity equipment, water intake, purifier, tank and starter grids.");
    }

    private static void Solar()
    {
        var root = new GameObject("SolarPanel"); var node = root.AddComponent<EcoPowerNode>();
        node.kind = EcoPowerNode.Kind.Solar; node.ratedPower = 5; node.port = new Vector3(0, 1.25f, 0);
        for (int i = 0; i < 2; i++)
            Box(root.transform, "Support", new Vector3(i == 0 ? -0.65f : 0.65f, 0.5f, 0), new Vector3(0.08f, 1f, 0.7f), metal);
        var face = new GameObject("Tilted panel").transform; face.SetParent(root.transform, false);
        face.localPosition = new Vector3(0, 1.1f, 0); face.localRotation = Quaternion.Euler(25, 0, 0);
        Box(face, "Frame", Vector3.zero, new Vector3(1.9f, 0.07f, 1.3f), panel);
        for (int x = 0; x < 4; x++) for (int z = 0; z < 3; z++)
            Box(face, "Photovoltaic cell", new Vector3(-0.69f + x * 0.46f, 0.046f, -0.4f + z * 0.4f), new Vector3(0.435f, 0.025f, 0.37f), cell);
        ColliderAndSave(root, "SolarPanel");
    }
    private static void Wind()
    {
        var root = new GameObject("WindTurbine"); var node = root.AddComponent<EcoPowerNode>();
        node.kind = EcoPowerNode.Kind.Wind; node.ratedPower = 8f;
        Box(root.transform, "Foundation", new Vector3(0, 0.12f, 0), new Vector3(0.9f, 0.24f, 0.9f), metal);
        Box(root.transform, "Tower", new Vector3(0, 1.9f, 0), new Vector3(0.16f, 3.6f, 0.16f), white);
        Box(root.transform, "Generator", new Vector3(0, 3.7f, 0), new Vector3(0.4f, 0.3f, 0.65f), white);
        var rotor = new GameObject("Rotor").transform; rotor.SetParent(root.transform, false); rotor.localPosition = new Vector3(0, 3.7f, -0.38f);
        rotor.gameObject.AddComponent<EcoWindRotor>().node = node;
        for (int i = 0; i < 3; i++)
        {
            var blade = new GameObject("Blade").transform; blade.SetParent(rotor, false); blade.localRotation = Quaternion.Euler(0, 0, i * 120f);
            Box(blade, "Airfoil", new Vector3(0, 0.62f, 0), new Vector3(0.13f, 1.15f, 0.05f), white);
            Box(blade, "Tip", new Vector3(0, 1.16f, 0), new Vector3(0.14f, 0.14f, 0.06f), cell);
        }
        var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(0.9f, 3.8f, 0.9f); collider.center = Vector3.up * 1.9f;
        Save(root, "WindTurbine");
    }
    private static void Battery()
    {
        var root = new GameObject("Battery"); var node = root.AddComponent<EcoPowerNode>();
        node.kind = EcoPowerNode.Kind.Battery; node.capacity = 250f; node.storedEnergy = 0f;
        Box(root.transform, "Cabinet", new Vector3(0, 0.55f, 0), new Vector3(0.8f, 1.1f, 0.55f), metal);
        Box(root.transform, "Front panel", new Vector3(0, 0.65f, -0.29f), new Vector3(0.6f, 0.6f, 0.035f), white);
        for (int i = 0; i < 4; i++) Box(root.transform, "Charge indicator", new Vector3(0, 0.46f + i * 0.12f, -0.32f), new Vector3(0.36f, 0.07f, 0.025f), green);
        ColliderAndSave(root, "Battery");
    }
    private static void Charger()
    {
        var root = new GameObject("ChargingStation"); var node = root.AddComponent<EcoPowerNode>(); node.kind = EcoPowerNode.Kind.Charger;
        root.AddComponent<EcoChargingStation>();
        Box(root.transform, "Base", new Vector3(0, 0.07f, 0), new Vector3(0.7f, 0.14f, 0.6f), metal);
        Box(root.transform, "Terminal", new Vector3(0, 0.75f, 0), new Vector3(0.5f, 1.4f, 0.38f), yellow);
        Box(root.transform, "Screen", new Vector3(0, 1.13f, 0.2f), new Vector3(0.35f, 0.26f, 0.035f), green);
        Box(root.transform, "Socket", new Vector3(0, 0.65f, 0.22f), new Vector3(0.17f, 0.17f, 0.08f), metal);
        ColliderAndSave(root, "ChargingStation");
    }
    private static EcoPowerNode Place(GameObject parent, string name, Vector3 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output + name + ".prefab"));
        go.transform.SetParent(parent.transform, false); go.transform.localPosition = position; return go.GetComponent<EcoPowerNode>();
    }
    private static GameObject WaterRoot(string name, EcoWaterNode.Kind kind, float capacity)
    {
        var root = new GameObject(name);
        var water = root.AddComponent<EcoWaterNode>(); water.kind = kind; water.capacity = capacity;
        if (kind != EcoWaterNode.Kind.Tank) root.AddComponent<EcoPowerNode>().kind = EcoPowerNode.Kind.Consumer;
        return root;
    }
    private static void Intake()
    {
        var root = WaterRoot("WaterIntake", EcoWaterNode.Kind.Intake, 30);
        root.GetComponent<EcoWaterNode>().electricityPerLitre = 0.5f;
        Box(root.transform, "Pump base", new Vector3(0, 0.09f, 0), new Vector3(0.85f, 0.18f, 0.7f), metal);
        Cylinder(root.transform, "Motor", new Vector3(0, 0.4f, 0), new Vector3(0.5f, 0.25f, 0.5f), blue);
        Box(root.transform, "Control box", new Vector3(0.25f, 0.6f, 0), new Vector3(0.28f, 0.35f, 0.3f), yellow);
        Box(root.transform, "Suction inlet", new Vector3(-0.3f, 0.23f, 0), new Vector3(0.3f, 0.18f, 0.2f), metal);
        Box(root.transform, "Water outlet", new Vector3(0, 0.5f, -0.3f), new Vector3(0.18f, 0.18f, 0.2f), blue);
        ColliderAndSave(root, "WaterIntake");
    }
    private static void Purifier()
    {
        var root = WaterRoot("WaterPurifier", EcoWaterNode.Kind.Purifier, 80);
        root.GetComponent<EcoWaterNode>().litresPerSecond = 3;
        Box(root.transform, "Foundation", new Vector3(0, 0.08f, 0), new Vector3(1.9f, 0.16f, 1.1f), metal);
        Cylinder(root.transform, "Raw water chamber", new Vector3(-0.58f, 0.73f, 0), new Vector3(0.62f, 0.65f, 0.62f), brown);
        Cylinder(root.transform, "Filter column", new Vector3(0, 0.85f, 0), new Vector3(0.35f, 0.77f, 0.35f), white);
        Cylinder(root.transform, "Clean chamber", new Vector3(0.58f, 0.73f, 0), new Vector3(0.62f, 0.65f, 0.62f), blue);
        Box(root.transform, "Connecting manifold", new Vector3(0, 1.32f, 0), new Vector3(1.25f, 0.09f, 0.09f), metal);
        Box(root.transform, "Control cabinet", new Vector3(0, 0.45f, -0.38f), new Vector3(0.45f, 0.6f, 0.28f), yellow);
        Box(root.transform, "Status screen", new Vector3(0, 0.56f, -0.53f), new Vector3(0.3f, 0.18f, 0.025f), green);
        ColliderAndSave(root, "WaterPurifier");
    }
    private static void Tank()
    {
        var root = WaterRoot("WaterTank", EcoWaterNode.Kind.Tank, 300);
        Box(root.transform, "Foundation", new Vector3(0, 0.09f, 0), new Vector3(1.5f, 0.18f, 1.5f), metal);
        Cylinder(root.transform, "Tank", new Vector3(0, 1.05f, 0), new Vector3(1.35f, 0.9f, 1.35f), blue);
        Cylinder(root.transform, "Top rim", new Vector3(0, 1.98f, 0), new Vector3(1.4f, 0.05f, 1.4f), white);
        Box(root.transform, "Gauge", new Vector3(0, 1.1f, -0.675f), new Vector3(0.14f, 1.2f, 0.025f), white);
        Box(root.transform, "Tap", new Vector3(0.32f, 0.4f, -0.67f), new Vector3(0.18f, 0.16f, 0.2f), yellow);
        ColliderAndSave(root, "WaterTank");
    }
    private static void Cylinder(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material;
    }
    private static void Recycling()
    {
        var root = new GameObject("RecyclingFacility"); root.AddComponent<EcoPowerNode>().kind = EcoPowerNode.Kind.Consumer;
        root.AddComponent<EcoRecyclingFacility>();
        Box(root.transform, "Foundation", new Vector3(0, 0.1f, 0), new Vector3(2.3f, 0.2f, 1.6f), metal);
        Box(root.transform, "Processor", new Vector3(0, 0.75f, 0.1f), new Vector3(1.3f, 1.3f, 1.1f), green);
        Box(root.transform, "Intake", new Vector3(-0.88f, 0.6f, 0), new Vector3(0.5f, 0.9f, 0.9f), white);
        Box(root.transform, "Dark intake opening", new Vector3(-0.88f, 1.06f, 0), new Vector3(0.4f, 0.025f, 0.7f), metal);
        Box(root.transform, "Sorted output", new Vector3(0.88f, 0.35f, 0), new Vector3(0.5f, 0.5f, 0.8f), yellow);
        Box(root.transform, "Control screen", new Vector3(0, 1.05f, -0.46f), new Vector3(0.7f, 0.28f, 0.04f), cell);
        for (int i = 0; i < 3; i++) Box(root.transform, "Vent", new Vector3(0, 0.7f - i * 0.14f, -0.465f), new Vector3(0.7f, 0.045f, 0.045f), metal);
        ColliderAndSave(root, "RecyclingFacility");
    }
    private static void Farm()
    {
        var root = WaterRoot("Farm", EcoWaterNode.Kind.Farm, 60);
        // Irrigation is a gravity-fed clean-water sink, not an electrical consumer.
        Object.DestroyImmediate(root.GetComponent<EcoPowerNode>());
        root.AddComponent<EcoFarm>();
        var soil = Mat("Soil", new Color(0.24f, 0.15f, 0.075f));
        Box(root.transform, "Soil", new Vector3(0, 0.07f, 0), new Vector3(2.8f, 0.14f, 2.8f), soil);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1.44f : 1.44f;
            Box(root.transform, "Bed edge", new Vector3(side, 0.1f, 0), new Vector3(0.12f, 0.2f, 3), brown);
            Box(root.transform, "Bed edge", new Vector3(0, 0.1f, side), new Vector3(2.8f, 0.2f, 0.12f), brown);
        }
        for (int i = 0; i < 3; i++) Box(root.transform, "Drip line", new Vector3(-0.8f + i * 0.8f, 0.145f, 0), new Vector3(0.035f, 0.02f, 2.6f), blue);
        var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * 0.07f; collider.size = new Vector3(3, 0.14f, 3);
        Save(root, "Farm");
    }
    private static void Connect(GameObject parent, EcoPowerNode a, EcoPowerNode b)
    {
        var go = new GameObject(a.name + " to " + b.name); go.transform.SetParent(parent.transform, false);
        var cableComponent = go.AddComponent<EcoPowerCable>(); cableComponent.a = a; cableComponent.b = b;
        var line = go.GetComponent<LineRenderer>(); line.sharedMaterial = cable; line.startWidth = line.endWidth = 0.035f;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.numCapVertices = 3;
    }
    private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = size;
        Object.DestroyImmediate(go.GetComponent<Collider>()); go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    private static void ColliderAndSave(GameObject root, string name)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        var collider = root.AddComponent<BoxCollider>(); collider.center = bounds.center; collider.size = bounds.size;
        Save(root, name);
    }
    private static void Save(GameObject root, string name)
    {
        var structure = root.AddComponent<EcoStructure>();
        structure.buildingId = name switch { "SolarPanel" => "solar", "WindTurbine" => "wind", "Battery" => "battery", "ChargingStation" => "charger", "WaterIntake" => "intake", "WaterPurifier" => "purifier", "WaterTank" => "tank", "RecyclingFacility" => "recycling", "Farm" => "farm", _ => "" };
        PrefabUtility.SaveAsPrefabAsset(root, Output + name + ".prefab"); Object.DestroyImmediate(root);
    }
    private static Material Mat(string name, Color color)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(Output + name + ".mat");
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, Output + name + ".mat"); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", 0.25f); return material;
    }
    private static void Label(Transform parent, string content, Vector3 position)
    {
        var root = new GameObject("Charging sign", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        root.transform.SetParent(parent, false); root.transform.localPosition = position; root.transform.localScale = Vector3.one * 0.0025f;
        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var text = new GameObject("Caption", typeof(RectTransform), typeof(Text)); text.transform.SetParent(root.transform, false);
        text.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 100);
        var caption = text.GetComponent<Text>(); caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        caption.fontSize = 36; caption.alignment = TextAnchor.MiddleCenter; caption.color = new Color(1f, 0.9f, 0.6f); caption.text = content; caption.raycastTarget = false;
    }
}
