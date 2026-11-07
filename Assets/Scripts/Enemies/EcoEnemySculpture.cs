using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Species = EnemyBrain.Species;

// Authored from the catalogue silhouettes in a common one-unit, feet-to-head space.
// A cached mesh per moving group keeps the articulated creatures cheap to spawn.
internal sealed partial class EcoEnemySculpture
{
    internal enum Joint { Body, Eyes, LeftArm, RightArm, LeftFoot, RightFoot, Crest, Trail, Filter }

    internal readonly struct Node
    {
        public readonly Transform Transform;
        public readonly Vector3 Rest;
        public Node(Transform transform, Vector3 rest) { Transform = transform; Rest = rest; }
    }

    private sealed class Group
    {
        public readonly Joint Parent;
        public readonly Vector3 Rest;
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<List<int>> Triangles = new List<List<int>>();
        public readonly List<Material> Materials = new List<Material>();
        public Mesh Mesh;

        public Group(Joint parent, Vector3 rest) { Parent = parent; Rest = rest; }

        public void Add(Mesh shape, Material material, Vector3 point, Vector3 size, Vector3 rotation)
        {
            var model = Matrix4x4.TRS(point - Rest, Quaternion.Euler(rotation), size);
            var normals = model.inverse.transpose;
            int start = Vertices.Count;
            var shapeVertices = shape.vertices;
            var shapeNormals = shape.normals;
            for (int i = 0; i < shapeVertices.Length; i++)
            {
                Vertices.Add(model.MultiplyPoint3x4(shapeVertices[i]));
                Normals.Add(normals.MultiplyVector(shapeNormals[i]).normalized);
            }
            int index = Materials.IndexOf(material);
            if (index < 0)
            {
                index = Materials.Count;
                Materials.Add(material);
                Triangles.Add(new List<int>());
            }
            foreach (int vertex in shape.triangles) Triangles[index].Add(start + vertex);
        }

        public void Bake(string label)
        {
            if (Vertices.Count == 0) return;
            Mesh = new Mesh { name = label, hideFlags = HideFlags.DontSave };
            Mesh.SetVertices(Vertices);
            Mesh.SetNormals(Normals);
            Mesh.subMeshCount = Materials.Count;
            for (int i = 0; i < Triangles.Count; i++) Mesh.SetTriangles(Triangles[i], i, false);
            Mesh.RecalculateBounds();
            Mesh.UploadMeshData(true);
            Vertices.Clear();
            Normals.Clear();
            Triangles.Clear();
        }
    }

    private static readonly Dictionary<Species, EcoEnemySculpture> Library = new Dictionary<Species, EcoEnemySculpture>();
    private static readonly Dictionary<uint, Material> Palette = new Dictionary<uint, Material>();
    private static readonly int Count = Enum.GetValues(typeof(Joint)).Length;
    private static Shader shader;
    private static Mesh soft, cloud, metal, box, ring;
    private readonly Group[] groups = new Group[Count];
    private readonly string name;

    private EcoEnemySculpture(Species kind)
    {
        name = EnemyBrain.NameOf(kind);
        Open(Joint.Body, P(0, 0.45f, 0));
        switch (kind)
        {
            case Species.Slime: Slime(); break;
            case Species.Flame: Flame(); break;
            case Species.Smoke: Smoke(); break;
            case Species.Acid: Acid(); break;
            case Species.Tin: Tin(); break;
        }
        for (int i = 0; i < groups.Length; i++)
            groups[i]?.Bake(name + " · " + (Joint)i);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        foreach (var art in Library.Values)
            foreach (var group in art.groups)
                Release(group?.Mesh);
        foreach (var material in Palette.Values) Release(material);
        Release(soft);
        Release(cloud);
        Release(metal);
        Release(box);
        Release(ring);
        Library.Clear();
        Palette.Clear();
        soft = cloud = metal = box = ring = null;
        shader = null;
    }

    internal static EcoEnemySculpture For(Species species)
    {
        if (Library.TryGetValue(species, out var art)) return art;
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        if (soft == null)
        {
            soft = Round("Soft sculpt", 16, 10, 0.03f);
            cloud = Round("Cloud lobe", 12, 8, 0.13f);
            metal = Revolve("Beveled cylinder", 20,
                new Vector2(0f, -0.5f), new Vector2(0.43f, -0.5f), new Vector2(0.5f, -0.38f),
                new Vector2(0.5f, 0.38f), new Vector2(0.43f, 0.5f), new Vector2(0f, 0.5f));
            box = RoundedBox();
            ring = Torus();
        }
        art = new EcoEnemySculpture(species);
        Library.Add(species, art);
        return art;
    }

    internal Node[] Mount(Transform rig)
    {
        var nodes = new Node[Count];
        for (int i = 0; i < groups.Length; i++)
        {
            var group = groups[i];
            if (group == null) continue;
            var joint = (Joint)i;
            var parent = joint == Joint.Body || group.Parent == joint ? rig : nodes[(int)group.Parent].Transform;
            var go = new GameObject(name + " " + joint) { layer = rig.parent.gameObject.layer };
            var bone = go.transform;
            bone.SetParent(parent, false);
            bone.localPosition = group.Rest - (parent == rig ? Vector3.zero : groups[(int)group.Parent].Rest);
            if (group.Mesh != null)
            {
                go.AddComponent<MeshFilter>().sharedMesh = group.Mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = group.Materials.ToArray();
                renderer.shadowCastingMode = joint == Joint.Eyes ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
            if (joint == Joint.Filter) go.SetActive(false);
            nodes[i] = new Node(bone, bone.localPosition);
        }
        return nodes;
    }

    private static Vector3 P(float x, float y, float z) => new Vector3(x, y, z);

    private void Open(Joint joint, Vector3 center, Joint parent = Joint.Body) => groups[(int)joint] = new Group(parent, center);

    private static Material Paint(uint rgb, bool metal = false, bool lit = false)
    {
        uint key = rgb | (metal ? 0x1000000u : 0) | (lit ? 0x2000000u : 0);
        if (Palette.TryGetValue(key, out var existing)) return existing;
        Color color = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        var material = new Material(shader) { name = "Creature paint " + rgb.ToString("X6"), color = color, hideFlags = HideFlags.DontSave };
        material.SetFloat("_Metallic", metal ? 0.36f : 0f);
        material.SetFloat("_Smoothness", metal ? 0.48f : 0.22f);
        if (lit)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.72f);
        }
        Palette.Add(key, material);
        return material;
    }

    private void Put(Mesh shape, Material paint, Vector3 point, Vector3 size, Joint joint = Joint.Body, Vector3 rotation = default)
        => groups[(int)joint].Add(shape, paint, point, size, rotation);

    private void Ball(Material paint, Vector3 point, Vector3 size, Joint joint = Joint.Body, Vector3 rotation = default)
        => Put(soft, paint, point, size, joint, rotation);

    private void Puff(Material paint, Vector3 point, Vector3 size, Joint joint = Joint.Body, Vector3 rotation = default)
        => Put(cloud, paint, point, size, joint, rotation);

    private void Link(Material paint, Vector3 a, Vector3 b, float width, Joint joint)
    {
        Vector3 delta = b - a;
        Put(metal, paint, (a + b) * 0.5f, P(width, delta.magnitude, width), joint,
            Quaternion.FromToRotation(Vector3.up, delta).eulerAngles);
    }

    private void Lick(Material paint, Joint joint, float width, params Vector3[] points)
    {
        Mesh mesh = Strand(width, points);
        Put(mesh, paint, Vector3.zero, Vector3.one, joint);
        Release(mesh);
    }

    private static void Release(UnityEngine.Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }

    private void EyePair(Vector3 center, float spacing, Vector3 size, Material iris, Material glint, float tilt = 0)
    {
        Open(Joint.Eyes, center);
        foreach (int side in new[] { -1, 1 })
        {
            Vector3 spot = center + Vector3.right * (side * spacing);
            Ball(iris, spot, size, Joint.Eyes, P(0, side * 9, side * tilt));
            if (glint != null) Ball(glint, spot + P(-size.x * 0.12f, size.y * 0.19f, size.z * 0.48f),
                P(size.x * 0.24f, size.y * 0.24f, size.z * 0.31f), Joint.Eyes);
        }
    }

    private static Mesh Round(string name, int sides, int steps, float ripple)
    {
        var vertices = new Vector3[sides * (steps + 1)];
        for (int y = 0; y <= steps; y++)
        {
            float angle = (y / (float)steps - 0.5f) * Mathf.PI;
            float radius = 0.5f * Mathf.Cos(angle);
            for (int i = 0; i < sides; i++)
            {
                float a = i * (Mathf.PI * 2 / sides);
                float wave = 1f + ripple * (0.58f * Mathf.Cos(a * 3f + angle) + 0.42f * Mathf.Sin(a * 5f - angle * 1.4f));
                vertices[y * sides + i] = P(radius * Mathf.Cos(a) * wave, Mathf.Sin(angle) * 0.5f, radius * Mathf.Sin(a) * wave);
            }
        }
        return Surface(name, vertices, sides);
    }

    private static Mesh Revolve(string name, int sides, params Vector2[] profile)
    {
        var vertices = new Vector3[sides * profile.Length];
        for (int y = 0; y < profile.Length; y++)
            for (int i = 0; i < sides; i++)
            {
                float angle = i * (Mathf.PI * 2 / sides);
                vertices[y * sides + i] = P(profile[y].x * Mathf.Cos(angle), profile[y].y, profile[y].x * Mathf.Sin(angle));
            }
        return Surface(name, vertices, sides);
    }

    private static Mesh RoundedBox()
    {
        Vector2[] corners =
        {
            new Vector2(.50f, .38f), new Vector2(.38f, .50f), new Vector2(-.38f, .50f), new Vector2(-.50f, .38f),
            new Vector2(-.50f, -.38f), new Vector2(-.38f, -.50f), new Vector2(.38f, -.50f), new Vector2(.50f, -.38f)
        };
        float[] sizes = { 0, .84f, 1, 1, .84f, 0 };
        float[] levels = { -.5f, -.5f, -.38f, .38f, .5f, .5f };
        var vertices = new Vector3[corners.Length * sizes.Length];
        for (int y = 0; y < sizes.Length; y++)
            for (int x = 0; x < corners.Length; x++)
                vertices[y * corners.Length + x] = P(corners[x].x * sizes[y], levels[y], corners[x].y * sizes[y]);
        return Surface("Beveled panel", vertices, corners.Length);
    }

    private static Mesh Torus()
    {
        const int sides = 24;
        const int steps = 8;
        var vertices = new Vector3[sides * (steps + 1)];
        for (int y = 0; y <= steps; y++)
        {
            float v = y * Mathf.PI * 2f / steps;
            float r = 0.42f + Mathf.Cos(v) * 0.08f;
            for (int i = 0; i < sides; i++)
            {
                float u = i * Mathf.PI * 2f / sides;
                vertices[y * sides + i] = P(r * Mathf.Cos(u), Mathf.Sin(v) * 0.08f, r * Mathf.Sin(u));
            }
        }
        return Surface("Rim", vertices, sides);
    }

    private static Mesh Strand(float width, Vector3[] points)
    {
        const int sides = 10;
        int sections = (points.Length - 1) * 4;
        var vertices = new Vector3[(sections + 1) * sides];
        for (int i = 0; i <= sections; i++)
        {
            float t = i / (float)sections;
            var p = Curve(points, t);
            var direction = (Curve(points, Mathf.Min(1f, t + 0.012f)) - Curve(points, Mathf.Max(0f, t - 0.012f))).normalized;
            var across = Vector3.Cross(Vector3.forward, direction).normalized;
            if (across.sqrMagnitude < 0.1f) across = Vector3.Cross(Vector3.right, direction).normalized;
            var depth = Vector3.Cross(across, direction);
            float radius = width * Mathf.Pow(1f - t, 0.78f) * Mathf.Lerp(0.76f, 1f, Mathf.Sin(t * Mathf.PI));
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                vertices[i * sides + j] = p + radius * (across * Mathf.Cos(a) + depth * Mathf.Sin(a));
            }
        }
        return Surface("Sculpted taper", vertices, sides);
    }

    private static Vector3 Curve(Vector3[] points, float t)
    {
        float p = t * (points.Length - 1);
        int i = Mathf.Min((int)p, points.Length - 2);
        float a = p - i;
        var start = points[Mathf.Max(i - 1, 0)];
        var inner = points[i];
        var next = points[i + 1];
        var end = points[Mathf.Min(i + 2, points.Length - 1)];
        return 0.5f * (2f * inner + (-start + next) * a + (2f * start - 5f * inner + 4f * next - end) * a * a
            + (-start + 3f * inner - 3f * next + end) * a * a * a);
    }

    private static Mesh Surface(string name, Vector3[] vertices, int sides)
    {
        var triangles = new int[(vertices.Length / sides - 1) * sides * 6];
        int index = 0;
        for (int row = 0; row < vertices.Length / sides - 1; row++)
            for (int i = 0; i < sides; i++)
            {
                int a = row * sides + i;
                int b = row * sides + (i + 1) % sides;
                triangles[index++] = a;
                triangles[index++] = a + sides;
                triangles[index++] = b;
                triangles[index++] = b;
                triangles[index++] = a + sides;
                triangles[index++] = b + sides;
            }
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        return mesh;
    }
}
