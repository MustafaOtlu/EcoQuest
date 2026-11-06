using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Rebuilds only the market artwork. The existing Workbench prefab GUID, interaction and save IDs survive.
public static class BuildEcoMarketStall
{
    private const string Art = "Assets/Resources/EcoArt/";
    private const string Output = Art + "Market/";

    [MenuItem("EcoQuest/Art/Build market stall")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build the market artwork outside Play Mode.");
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var root = new GameObject("Market stall");
        try
        {
            var wood = Material("Honey wood", new Color(0.53f, 0.31f, 0.15f));
            var edge = Material("Dark timber", new Color(0.27f, 0.17f, 0.10f));
            var plank = Material("Light wood", new Color(0.72f, 0.49f, 0.25f));
            var green = Material("Forest canvas", new Color(0.16f, 0.39f, 0.26f));
            var cream = Material("Cream canvas", new Color(0.94f, 0.86f, 0.64f));
            var metal = Material("Iron fittings", new Color(0.18f, 0.22f, 0.21f));
            var body = new GameObject("Stall structure").transform;
            body.SetParent(root.transform, false);

            // A compact wooden cart silhouette, rather than another full building beside the house.
            Cube(body, "Counter body", new Vector3(0, 0.55f, 0), new Vector3(2.24f, 0.88f, 0.98f), wood);
            Cube(body, "Lower trim", new Vector3(0, 0.18f, -0.02f), new Vector3(2.34f, 0.13f, 1.09f), edge);
            Cube(body, "Counter top", new Vector3(0, 1.04f, 0), new Vector3(2.44f, 0.12f, 1.16f), plank);
            for (int i = 0; i < 9; i++)
                Cube(body, "Front slat", new Vector3(-1.01f + i * 0.252f, 0.60f, -0.502f), new Vector3(0.226f, 0.73f, 0.035f), i % 3 == 0 ? plank : wood);
            foreach (float x in new[] { -1.08f, 1.08f })
            {
                Cube(body, "Canopy upright", new Vector3(x, 1.29f, 0.31f), new Vector3(0.105f, 2.58f, 0.105f), edge);
                Cube(body, "Iron foot", new Vector3(x, 0.13f, 0.31f), new Vector3(0.14f, 0.23f, 0.14f), metal);
                Cube(body, "Canopy brace", new Vector3(x * 0.88f, 2.30f, 0.31f), new Vector3(0.08f, 0.50f, 0.08f), wood, new Vector3(0, 0, x < 0 ? -35 : 35));
            }
            Cube(body, "Canopy crossbar", new Vector3(0, 2.55f, 0.31f), new Vector3(2.48f, 0.12f, 0.12f), edge);

            // Each stripe is a solid, gently folded fabric panel with a scalloped hanging edge.
            for (int i = 0; i < 10; i++)
            {
                var panel = new GameObject("Canvas stripe " + i, typeof(MeshFilter), typeof(MeshRenderer));
                panel.transform.SetParent(body, false);
                panel.GetComponent<MeshFilter>().sharedMesh = CanopyStripe(-1.38f + i * 0.276f, 0.276f);
                panel.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? green : cream;
            }
            Cube(body, "Front canopy rail", new Vector3(0, 2.29f, -0.75f), new Vector3(2.76f, 0.065f, 0.065f), edge);

            // Real low-poly produce models and open crates from the project's existing CC0 art.
            for (int i = 0; i < 3; i++)
            {
                float x = -0.77f + i * 0.77f;
                Prop(body, "OpenBox", "Produce crate", new Vector3(x, 1.10f, -0.06f), 0.48f, 0);
                string crop = i == 0 ? "Cabbage" : i == 1 ? "Carrot" : "Tomato";
                for (int p = 0; p < 4; p++)
                    Prop(body, crop, crop, new Vector3(x + (p % 2 == 0 ? -0.12f : 0.12f), 1.23f,
                        -0.16f + (p / 2) * 0.19f), i == 1 ? 0.25f : 0.20f, 23f + p * 63f);
            }
            // Small green seed packets on a side shelf communicate the shop's purpose even from above.
            Cube(body, "Side shelf", new Vector3(1.24f, 0.67f, 0.02f), new Vector3(0.34f, 0.07f, 0.76f), plank);
            for (int i = 0; i < 3; i++)
                Cube(body, "Seed packet", new Vector3(1.25f, 0.83f, -0.22f + i * 0.21f), new Vector3(0.16f, 0.25f, 0.12f), i % 2 == 0 ? green : cream, new Vector3(0, 0, -8));

            Consolidate(body);
            var sign = new GameObject("MARKET sign").transform;
            sign.SetParent(root.transform, false);
            sign.localPosition = new Vector3(0, 2.98f, 0.18f);
            sign.rotation = Quaternion.Euler(50, 45, 0);
            Cube(sign, "Timber sign frame", Vector3.zero, new Vector3(2.46f, 0.58f, 0.09f), edge);
            Cube(sign, "Cream sign face", new Vector3(0, 0, -0.055f), new Vector3(2.30f, 0.44f, 0.025f), cream);
            foreach (float x in new[] { -1.13f, 1.13f })
                Cube(sign, "Sign rivet", new Vector3(x, 0, -0.075f), new Vector3(0.055f, 0.055f, 0.025f), metal);
            var letters = new GameObject("3D MARKET lettering", typeof(TextMesh));
            letters.transform.SetParent(sign, false);
            letters.transform.localPosition = new Vector3(0, 0.01f, -0.078f);
            var text = letters.GetComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 128;
            text.characterSize = 0.085f;
            text.fontStyle = FontStyle.Bold;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.08f, 0.24f, 0.14f);
            text.text = "MARKET";
            text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            int advance = 0, minimumY = int.MaxValue, maximumY = int.MinValue;
            foreach (char letter in text.text)
            {
                CharacterInfo glyph;
                if (!text.font.GetCharacterInfo(letter, out glyph, text.fontSize, text.fontStyle)) continue;
                advance += glyph.advance;
                minimumY = Mathf.Min(minimumY, glyph.minY);
                maximumY = Mathf.Max(maximumY, glyph.maxY);
            }
            if (advance > 0 && maximumY > minimumY)
                text.characterSize = Mathf.Min(1.94f / (advance * 0.1f), 0.28f / ((maximumY - minimumY) * 0.1f));
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // TextMesh is actual world-space lettering; the whole framed board follows the camera.
            sign.gameObject.AddComponent<EcoMarketSign>();
            PrefabUtility.SaveAsPrefabAsset(root, Output + "MarketStall.prefab");
            ApplyToWorkbench();
            AssetDatabase.SaveAssets();
            Debug.Log("ECO_MARKET_STALL_READY: striped market stall and camera-facing 3D MARKET sign built; existing scene/save identity preserved.");
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static void ApplyToWorkbench()
    {
        var root = PrefabUtility.LoadPrefabContents(Art + "Workbench.prefab");
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output + "MarketStall.prefab"), root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            var box = root.GetComponent<BoxCollider>();
            if (box == null) box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.55f, 0);
            box.size = new Vector3(2.30f, 1.10f, 1.08f);
            PrefabUtility.SaveAsPrefabAsset(root, Art + "Workbench.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static Material Material(string name, Color tint)
    {
        string path = Output + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.color = tint;
        material.SetFloat("_Smoothness", 0.12f);
        return material;
    }

    private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material, Vector3 angles = default(Vector3))
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = size;
        go.transform.localRotation = Quaternion.Euler(angles);
        go.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    private static void Prop(Transform parent, string asset, string name, Vector3 position, float height, float yaw)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + asset + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing market prop: " + asset);
        var prop = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
        prop.name = name;
        var renderers = prop.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        prop.transform.localScale = Vector3.one * height / Mathf.Max(0.001f, bounds.size.y);
        prop.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        prop.transform.localPosition = position;
        foreach (var collider in prop.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
    }

    private static Mesh CanopyStripe(float x, float width)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        // Front-to-back profile: hanging hem, low front edge, raised ridge and rear edge.
        for (int i = 0; i <= 6; i++)
        {
            float across = i / 6f;
            float px = x + across * width;
            vertices.Add(new Vector3(px, 2.12f - Mathf.Sin(across * Mathf.PI) * 0.065f, -0.80f));
            vertices.Add(new Vector3(px, 2.35f, -0.80f));
            vertices.Add(new Vector3(px, 2.68f, 0.27f));
            vertices.Add(new Vector3(px, 2.45f, 0.76f));
        }
        for (int i = 0; i < 6; i++) for (int p = 0; p < 3; p++)
        {
            int a = i * 4 + p, b = a + 4;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
            triangles.Add(b); triangles.Add(a + 1); triangles.Add(b + 1);
        }
        // Separate back-face vertices retain valid lighting normals on both sides of the canvas.
        int frontVertices = vertices.Count, frontTriangles = triangles.Count;
        for (int i = 0; i < frontVertices; i++) vertices.Add(vertices[i]);
        for (int i = 0; i < frontTriangles; i += 3)
        {
            triangles.Add(triangles[i + 2] + frontVertices);
            triangles.Add(triangles[i + 1] + frontVertices);
            triangles.Add(triangles[i] + frontVertices);
        }
        var mesh = new Mesh { name = "Temporary canopy stripe" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static void Consolidate(Transform structure)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var temporary = new HashSet<Mesh>();
        foreach (var filter in structure.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null) continue;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
            {
                var material = materials[Mathf.Min(i, materials.Length - 1)];
                if (!groups.ContainsKey(material)) groups.Add(material, new List<CombineInstance>());
                groups[material].Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = i,
                    transform = structure.worldToLocalMatrix * filter.transform.localToWorldMatrix });
            }
            if (!AssetDatabase.Contains(filter.sharedMesh)) temporary.Add(filter.sharedMesh);
        }
        foreach (var group in groups)
        {
            string path = Output + group.Key.name.Replace("/", "_") + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.name = "Market " + group.Key.name;
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.CombineMeshes(group.Value.ToArray(), true, true);
            EditorUtility.SetDirty(mesh);
        }
        for (int i = structure.childCount - 1; i >= 0; i--) Object.DestroyImmediate(structure.GetChild(i).gameObject);
        foreach (var mesh in temporary) Object.DestroyImmediate(mesh);
        foreach (var group in groups)
        {
            var go = new GameObject(group.Key.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(structure, false);
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Output + group.Key.name.Replace("/", "_") + ".asset");
            go.GetComponent<MeshRenderer>().sharedMaterial = group.Key;
        }
    }
}
