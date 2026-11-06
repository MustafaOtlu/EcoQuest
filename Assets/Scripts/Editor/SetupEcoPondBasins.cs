using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Touches only the two starter ponds and the exact ground triangles underneath them.
public static class SetupEcoPondBasins
{
    private const string Output = "Assets/EcoWorld/Meshes/Ponds/";
    private const int Sides = 24;
    private const float Radius = 2.3f, Depth = 1.2f, Epsilon = 0.00001f;

    [MenuItem("EcoQuest/World/Set up finite starter ponds")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Set up pond geometry outside Play Mode.");
        Directory.CreateDirectory(Output); AssetDatabase.Refresh();
        var sources = Object.FindObjectsByType<WeaponWaterSource>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(s => s.gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                && (s.name == "Clean fishing pond" || s.name == "Natural polluted water"))
            .ToArray();
        if (sources.Length != 2) throw new InvalidOperationException("Expected exactly the clean and polluted starter pond in the open scene.");
        var ground = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None)
            .Where(f => f.gameObject.scene == sources[0].gameObject.scene && f.sharedMesh != null && f.GetComponentInParent<EcoGround>() != null
                && f.TryGetComponent<Renderer>(out var r) && r.enabled).ToArray();
        int changedMeshes = 0;
        foreach (var source in sources)
        {
            if (source.name == "Natural polluted water") source.cleanWater = false;
            source.ConfigureFiniteVolume(400f, 1f);
            source.shoreRadius = 2.7f;
            var existing = source.GetComponent<EcoPondBasin>();
            if (existing != null && existing.waterSurface != null)
            {
                existing.RefreshVisual(); Record(source); Record(existing); continue;
            }
            var center = source.transform.position;
            center.y = GroundHeight(ground, center, center.y - 0.03f);
            var rim = new Vector3[Sides];
            for (int i = 0; i < Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                var p = center + new Vector3(Mathf.Cos(a) * Radius, 0f, Mathf.Sin(a) * Radius);
                p.y = GroundHeight(ground, p, center.y);
                rim[i] = p;
            }
            foreach (var filter in ground)
                if (Carve(filter, center, Radius)) changedMeshes++;

            var old = source.transform.Find("Pond visual");
            Material waterMaterial = null;
            if (old != null)
            {
                var previousWater = old.Find("Water");
                if (previousWater != null && previousWater.TryGetComponent<Renderer>(out var wr)) waterMaterial = wr.sharedMaterial;
                // Keep the old transform/map-marker hierarchy intact so existing save identities remain stable.
                foreach (var renderer in old.GetComponentsInChildren<Renderer>(true)) { renderer.enabled = false; Record(renderer); }
            }
            var visual = new GameObject("Recessed pond basin").transform;
            visual.SetParent(source.transform, false);
            visual.position = center; visual.rotation = Quaternion.identity;
            Vector3 scale = source.transform.lossyScale;
            visual.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
            var bowl = new GameObject("Pond bed", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(EcoGround));
            bowl.transform.SetParent(visual, false);
            var bowlMesh = StoreMesh(BowlMesh(center, rim), SafeName(source.name) + "_Bowl");
            bowl.GetComponent<MeshFilter>().sharedMesh = bowlMesh;
            bowl.GetComponent<MeshCollider>().sharedMesh = bowlMesh;
            bowl.GetComponent<EcoGround>().surface = EcoGround.Surface.Rock;
            bowl.GetComponent<MeshRenderer>().sharedMaterials = new[]
            {
                Solid("Pond bank soil", new Color(0.43f, 0.34f, 0.22f)),
                Solid("Pond deep mud", new Color(0.23f, 0.25f, 0.17f))
            };
            var surface = new GameObject("Variable water surface", typeof(MeshFilter), typeof(MeshRenderer));
            surface.transform.SetParent(visual, false);
            surface.GetComponent<MeshFilter>().sharedMesh = UnitDisc();
            surface.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial != null ? waterMaterial
                : Solid("Pond water fallback", new Color(0.18f, 0.52f, 0.66f));
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            var stoneMaterial = Solid("Pond rim stone", new Color(0.48f, 0.47f, 0.38f));
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var stone = GameObject.CreatePrimitive(PrimitiveType.Sphere); stone.name = "Bank stone";
                stone.transform.SetParent(visual, false);
                stone.transform.localPosition = new Vector3(Mathf.Cos(a) * (Radius + 0.10f), rim[i * 2].y - center.y + 0.07f, Mathf.Sin(a) * (Radius + 0.10f));
                stone.transform.localScale = new Vector3(0.36f + 0.05f * Mathf.Sin(i * 1.8f), 0.20f + 0.03f * Mathf.Cos(i), 0.31f);
                stone.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
                stone.GetComponent<Renderer>().sharedMaterial = stoneMaterial;
                Object.DestroyImmediate(stone.GetComponent<Collider>());
            }
            var basin = source.gameObject.AddComponent<EcoPondBasin>();
            basin.Configure(source, surface.transform, surface.GetComponent<MeshRenderer>(), Depth, Radius);
            Record(source); Record(basin);
        }
        EcoNavigation.MarkDirty(); AssetDatabase.SaveAssets();
        Debug.Log("ECO_POND_BASINS_READY: two 400-SB ponds recessed 1.2m; " + changedMeshes + " local ground meshes cut. Save the scene to persist authored geometry.");
    }

    private static float GroundHeight(MeshFilter[] ground, Vector3 p, float fallback)
    {
        float nearest = float.NegativeInfinity;
        var ray = new Ray(p + Vector3.up * 30f, Vector3.down);
        foreach (var f in ground)
            foreach (var collider in f.GetComponents<Collider>())
                if (collider.enabled && collider.Raycast(ray, out var hit, 80f)) nearest = Mathf.Max(nearest, hit.point.y);
        return float.IsNegativeInfinity(nearest) ? fallback : nearest;
    }

    private static Mesh BowlMesh(Vector3 center, Vector3[] rim)
    {
        var vertices = new List<Vector3>();
        var bank = new List<int>(); var floor = new List<int>();
        // Flat bottom and two sloping rings. The outer ring exactly meets the original ground edge.
        vertices.Add(new Vector3(0, -Depth, 0));
        for (int ring = 0; ring < 3; ring++) for (int i = 0; i < Sides; i++)
        {
            float a = i * Mathf.PI * 2f / Sides;
            float radius = ring == 0 ? Radius * 0.28f : ring == 1 ? Radius * 0.70f : Radius;
            float y = ring == 0 ? -Depth : ring == 1 ? -Depth * (1f - (0.70f - 0.28f) / 0.72f) : rim[i].y - center.y;
            vertices.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
        }
        for (int i = 0; i < Sides; i++)
        {
            int next = (i + 1) % Sides;
            floor.Add(0); floor.Add(1 + next); floor.Add(1 + i);
            for (int ring = 0; ring < 2; ring++)
            {
                int a = 1 + ring * Sides + i, b = 1 + ring * Sides + next;
                int c = a + Sides, d = b + Sides;
                bank.Add(a); bank.Add(b); bank.Add(c); bank.Add(b); bank.Add(d); bank.Add(c);
            }
        }
        var mesh = new Mesh { name = "Recessed pond bed" };
        mesh.SetVertices(vertices); mesh.subMeshCount = 2; mesh.SetTriangles(bank, 0); mesh.SetTriangles(floor, 1);
        mesh.SetUVs(0, vertices.Select(v => new Vector2(v.x, v.z) * 0.2f).ToList());
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
    }

    private static Mesh UnitDisc()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Output + "Pond_SurfaceDisc.asset");
        if (existing != null) return existing;
        var points = new Vector3[Sides + 1]; var indices = new int[Sides * 3];
        for (int i = 0; i < Sides; i++)
        {
            float a = i * Mathf.PI * 2f / Sides; points[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            indices[i * 3] = 0; indices[i * 3 + 1] = 1 + (i + 1) % Sides; indices[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "Pond surface disc", vertices = points, triangles = indices };
        mesh.uv = points.Select(v => new Vector2(v.x, v.z) * 0.5f + Vector2.one * 0.5f).ToArray();
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); return StoreMesh(mesh, "Pond_SurfaceDisc");
    }

    private struct Vertex
    {
        public Vector3 position, world, normal;
        public Vector2 uv, uv2;
        public Color color;
        public Vector4 tangent;
        public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex
        {
            position = Vector3.LerpUnclamped(a.position, b.position, t), world = Vector3.LerpUnclamped(a.world, b.world, t),
            normal = Vector3.LerpUnclamped(a.normal, b.normal, t), uv = Vector2.LerpUnclamped(a.uv, b.uv, t),
            uv2 = Vector2.LerpUnclamped(a.uv2, b.uv2, t), color = Color.LerpUnclamped(a.color, b.color, t),
            tangent = Vector4.LerpUnclamped(a.tangent, b.tangent, t)
        };
    }

    // Difference between a triangle and a convex polygon: each clipped outside strip is disjoint.
    private static bool Carve(MeshFilter filter, Vector3 center, float radius)
    {
        var renderer = filter.GetComponent<Renderer>();
        if (renderer == null || !renderer.bounds.Intersects(new Bounds(center, new Vector3(radius * 2f + 0.1f, 20f, radius * 2f + 0.1f)))) return false;
        var source = filter.sharedMesh;
        if (!source.isReadable) throw new InvalidOperationException("Pond ground mesh must be readable: " + filter.name);
        var positions = source.vertices; var normals = source.normals; var colors = source.colors;
        var uv = source.uv; var uv2 = source.uv2; var tangents = source.tangents;
        bool haveNormals = normals.Length == positions.Length, haveColors = colors.Length == positions.Length;
        bool haveUV = uv.Length == positions.Length, haveUV2 = uv2.Length == positions.Length, haveTangents = tangents.Length == positions.Length;
        var input = new Vertex[positions.Length];
        for (int i = 0; i < positions.Length; i++) input[i] = new Vertex
        {
            position = positions[i], world = filter.transform.TransformPoint(positions[i]), normal = haveNormals ? normals[i] : Vector3.up,
            color = haveColors ? colors[i] : Color.white, uv = haveUV ? uv[i] : Vector2.zero,
            uv2 = haveUV2 ? uv2[i] : Vector2.zero, tangent = haveTangents ? tangents[i] : Vector4.zero
        };
        var hole = new Vector2[Sides];
        for (int i = 0; i < Sides; i++)
        {
            float a = i * Mathf.PI * 2f / Sides;
            hole[i] = new Vector2(center.x + Mathf.Cos(a) * radius, center.z + Mathf.Sin(a) * radius);
        }
        var output = new List<Vertex>(); var submeshes = new List<int>[source.subMeshCount]; bool changed = false;
        for (int sub = 0; sub < source.subMeshCount; sub++)
        {
            var indices = source.GetTriangles(sub); var result = new List<int>(); submeshes[sub] = result;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var triangle = new List<Vertex> { input[indices[i]], input[indices[i + 1]], input[indices[i + 2]] };
                float minX = Mathf.Min(triangle[0].world.x, triangle[1].world.x, triangle[2].world.x);
                float maxX = Mathf.Max(triangle[0].world.x, triangle[1].world.x, triangle[2].world.x);
                float minZ = Mathf.Min(triangle[0].world.z, triangle[1].world.z, triangle[2].world.z);
                float maxZ = Mathf.Max(triangle[0].world.z, triangle[1].world.z, triangle[2].world.z);
                if (maxX < center.x - radius || minX > center.x + radius || maxZ < center.z - radius || minZ > center.z + radius)
                { AddPolygon(triangle, output, result); continue; }
                var remaining = triangle;
                for (int edge = 0; edge < Sides && remaining.Count >= 3; edge++)
                {
                    var a = hole[edge]; var b = hole[(edge + 1) % Sides];
                    AddPolygon(Clip(remaining, a, b, false), output, result);
                    remaining = Clip(remaining, a, b, true);
                }
                if (PolygonArea(remaining) > Epsilon) changed = true;
            }
        }
        if (!changed) return false;
        var edited = new Mesh { name = source.name + " pond cut", indexFormat = IndexFormat.UInt32 };
        edited.SetVertices(output.Select(v => v.position).ToList());
        if (haveNormals) edited.SetNormals(output.Select(v => v.normal).ToList());
        if (haveColors) edited.SetColors(output.Select(v => v.color).ToList());
        if (haveUV) edited.SetUVs(0, output.Select(v => v.uv).ToList());
        if (haveUV2) edited.SetUVs(1, output.Select(v => v.uv2).ToList());
        if (haveTangents) edited.SetTangents(output.Select(v => v.tangent).ToList());
        edited.subMeshCount = submeshes.Length;
        for (int i = 0; i < submeshes.Length; i++) edited.SetTriangles(submeshes[i], i);
        if (!haveNormals) edited.RecalculateNormals(); edited.RecalculateBounds();
        string key = SafeName(filter.name) + "_" + StableHash(HierarchyPath(filter.transform)).ToString("x8") + "_PondCut";
        var stored = StoreMesh(edited, key);
        filter.sharedMesh = stored; Record(filter);
        foreach (var collider in filter.GetComponents<MeshCollider>())
            if (collider.sharedMesh == source) { collider.sharedMesh = null; collider.sharedMesh = stored; Record(collider); }
        return true;
    }

    private static List<Vertex> Clip(List<Vertex> polygon, Vector2 a, Vector2 b, bool inside)
    {
        var result = new List<Vertex>(); if (polygon.Count == 0) return result;
        Vertex previous = polygon[polygon.Count - 1]; float previousSide = Side(previous.world, a, b);
        bool previousKept = inside ? previousSide >= 0f : previousSide <= 0f;
        foreach (var current in polygon)
        {
            float currentSide = Side(current.world, a, b); bool currentKept = inside ? currentSide >= 0f : currentSide <= 0f;
            if (currentKept != previousKept)
            {
                float denominator = previousSide - currentSide;
                if (Mathf.Abs(denominator) > 0.0000001f) result.Add(Vertex.Lerp(previous, current, previousSide / denominator));
            }
            if (currentKept) result.Add(current);
            previous = current; previousSide = currentSide; previousKept = currentKept;
        }
        return result;
    }

    private static float Side(Vector3 p, Vector2 a, Vector2 b) => (b.x - a.x) * (p.z - a.y) - (b.y - a.y) * (p.x - a.x);
    private static float PolygonArea(List<Vertex> polygon)
    {
        float area = 0; for (int i = 1; i + 1 < polygon.Count; i++)
            area += Vector3.Cross(polygon[i].world - polygon[0].world, polygon[i + 1].world - polygon[0].world).magnitude * 0.5f;
        return area;
    }
    private static void AddPolygon(List<Vertex> polygon, List<Vertex> output, List<int> triangles)
    {
        if (polygon.Count < 3 || PolygonArea(polygon) <= Epsilon) return;
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            if (Vector3.Cross(polygon[i].world - polygon[0].world, polygon[i + 1].world - polygon[0].world).sqrMagnitude < Epsilon * Epsilon) continue;
            int at = output.Count; output.Add(polygon[0]); output.Add(polygon[i]); output.Add(polygon[i + 1]);
            triangles.Add(at); triangles.Add(at + 1); triangles.Add(at + 2);
        }
    }

    private static Mesh StoreMesh(Mesh mesh, string key)
    {
        string path = Output + key + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
    }
    private static Material Solid(string name, Color color)
    {
        string path = Output + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.color = color; material.SetFloat("_Smoothness", 0.08f); EditorUtility.SetDirty(material); return material;
    }
    private static string SafeName(string name) => new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    private static string HierarchyPath(Transform t) => t.parent == null ? t.name : HierarchyPath(t.parent) + "/" + t.name;
    private static uint StableHash(string text) { uint hash = 2166136261; foreach (char c in text) hash = (hash ^ c) * 16777619; return hash; }
    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
