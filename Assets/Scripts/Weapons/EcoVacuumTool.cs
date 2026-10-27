using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The vacuum as a friendly household-style tool rather than a gun: a rounded mint body, a clear canister with a few
// bits of collected litter, a D-shaped carry handle at the back, a ribbed neck and a wide orange-rimmed nozzle.
// A small lamp on top shows the mode (amber collect, blue water, white blow) and the rear fan spins while in use.
// Built from code like the flashlight, so it needs no imported model; the holder scales and grips it like any tool.
public sealed class EcoVacuumTool : MonoBehaviour
{
    private static readonly Color Body = new Color(0.56f, 0.80f, 0.62f), Cream = new Color(0.96f, 0.93f, 0.84f);
    private static readonly Color Rim = new Color(0.95f, 0.55f, 0.22f), Dark = new Color(0.20f, 0.25f, 0.24f);
    private static readonly Color CollectLight = new Color(1f, 0.72f, 0.25f), WaterLight = new Color(0.35f, 0.78f, 1f), BlowLight = new Color(0.92f, 0.97f, 1f);

    private PlayerWeaponSystem owner;
    private WeaponVisualEffects effects;
    private Transform fan;
    private Material lamp;
    private float fanSpeed;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");

    /// <summary>Creates the tool under the holder. The visual sits in a child turned so that, after the holder's usual
    /// tool rotation, the nozzle points forward (+z) and the canister up.</summary>
    public static EcoVacuumTool Create(PlayerWeaponSystem owner, Transform parent)
    {
        var root = new GameObject("Eco vacuum").transform;
        root.SetParent(parent, false);
        var tool = root.gameObject.AddComponent<EcoVacuumTool>();
        tool.owner = owner;
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root, false);
        visual.localRotation = Quaternion.Inverse(Quaternion.Euler(-90f, -90f, 0f));
        tool.Build(visual);
        return tool;
    }

    private void Build(Transform v)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material Paint(string name, Color color, float smooth = 0.45f)
        {
            var m = new Material(shader) { name = name, color = color };
            m.SetFloat("_Smoothness", smooth);
            return m;
        }
        var body = Paint("Vacuum body", Body, 0.55f);
        var cream = Paint("Vacuum cream", Cream, 0.35f);
        var rim = Paint("Vacuum nozzle rim", Rim, 0.5f);
        var dark = Paint("Vacuum dark", Dark, 0.2f);
        var glass = Glass(shader);
        lamp = Paint("Vacuum lamp", CollectLight, 0.8f);
        lamp.EnableKeyword("_EMISSION");
        lamp.SetColor(EmissionColor, CollectLight * 1.6f);

        // Main body: a rounded capsule along +z.
        Lathe(v, "Body", body, new[] { V(0.000f, -0.115f), V(0.040f, -0.110f), V(0.062f, -0.090f), V(0.068f, -0.050f), V(0.068f, 0.090f),
            V(0.062f, 0.130f), V(0.046f, 0.150f) }, 18);
        // Cream rear cap with a dark vent ring and a fan behind it.
        Lathe(v, "Rear cap", cream, new[] { V(0.000f, -0.135f), V(0.034f, -0.132f), V(0.055f, -0.118f), V(0.060f, -0.100f) }, 18);
        Lathe(v, "Vent ring", dark, new[] { V(0.036f, -0.139f), V(0.046f, -0.137f), V(0.046f, -0.131f), V(0.036f, -0.133f) }, 18);
        fan = new GameObject("Fan").transform; fan.SetParent(v, false); fan.localPosition = new Vector3(0f, 0f, -0.140f);
        for (int i = 0; i < 3; i++)
        {
            var blade = Part(PrimitiveType.Cube, "Fan blade", fan, Vector3.zero, new Vector3(0.012f, 0.060f, 0.004f), dark);
            blade.localRotation = Quaternion.Euler(0f, 0f, i * 60f);
        }
        // Clear canister on top, with a few bits of collected litter inside.
        var canister = Lathe(v, "Canister", glass, new[] { V(0.000f, -0.045f), V(0.040f, -0.045f), V(0.044f, -0.030f), V(0.044f, 0.040f), V(0.040f, 0.055f), V(0.000f, 0.055f) }, 16);
        canister.localPosition = new Vector3(0f, 0.088f, 0.010f); canister.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        Part(PrimitiveType.Cylinder, "Canister lid", v, new Vector3(0f, 0.140f, 0.010f), new Vector3(0.092f, 0.006f, 0.092f), cream);
        Part(PrimitiveType.Sphere, "Litter leaf", v, new Vector3(-0.014f, 0.084f, 0.022f), new Vector3(0.030f, 0.010f, 0.020f), Paint("Leaf", new Color(0.35f, 0.62f, 0.25f)));
        Part(PrimitiveType.Sphere, "Litter bit", v, new Vector3(0.016f, 0.080f, -0.004f), new Vector3(0.020f, 0.018f, 0.020f), Paint("Litter", new Color(0.62f, 0.45f, 0.30f)));
        // Mode lamp at the front of the lid.
        Part(PrimitiveType.Sphere, "Mode lamp", v, new Vector3(0f, 0.146f, 0.048f), new Vector3(0.020f, 0.016f, 0.020f), lamp);
        // D-shaped carry handle at the back: two posts and a rounded bar; the palm goes on its lower end.
        Part(PrimitiveType.Cylinder, "Handle post", v, new Vector3(0f, 0.090f, -0.080f), new Vector3(0.018f, 0.030f, 0.018f), cream);
        Part(PrimitiveType.Cylinder, "Handle post", v, new Vector3(0f, -0.085f, -0.080f), new Vector3(0.018f, 0.020f, 0.018f), cream);
        var bar = Part(PrimitiveType.Capsule, "Handle bar", v, new Vector3(0f, 0.004f, -0.122f), new Vector3(0.030f, 0.090f, 0.030f), body);
        bar.localRotation = Quaternion.Euler(8f, 0f, 0f);
        Part(PrimitiveType.Cube, "Handle top", v, new Vector3(0f, 0.110f, -0.100f), new Vector3(0.022f, 0.018f, 0.050f), cream);
        Part(PrimitiveType.Cube, "Handle foot", v, new Vector3(0f, -0.098f, -0.100f), new Vector3(0.022f, 0.018f, 0.050f), cream);
        // Leaf badge on each side.
        foreach (float side in new[] { -1f, 1f })
        {
            var badge = Part(PrimitiveType.Sphere, "Leaf badge", v, new Vector3(side * 0.068f, 0.012f, 0.030f), new Vector3(0.006f, 0.026f, 0.040f), Paint("Badge", new Color(0.24f, 0.55f, 0.30f)));
            badge.localRotation = Quaternion.Euler(-35f, 0f, 0f);
        }
        // Ribbed neck and the flared nozzle with an orange rim.
        for (int i = 0; i < 3; i++)
            Lathe(v, "Neck rib", dark, new[] { V(0.034f, 0.148f + i * 0.016f), V(0.040f, 0.153f + i * 0.016f), V(0.040f, 0.159f + i * 0.016f), V(0.034f, 0.164f + i * 0.016f) }, 16);
        // A short, wide, flattened cleaning head with a soft orange bumper: a vacuum attachment, not a flared barrel.
        var head = new GameObject("Cleaning head").transform; head.SetParent(v, false);
        head.localPosition = new Vector3(0f, -0.010f, 0f); head.localScale = new Vector3(1.25f, 0.55f, 1f);
        Lathe(head, "Nozzle", cream, new[] { V(0.040f, 0.196f), V(0.052f, 0.206f), V(0.070f, 0.222f), V(0.078f, 0.236f) }, 22);
        Lathe(head, "Nozzle bumper", rim, new[] { V(0.074f, 0.232f), V(0.084f, 0.238f), V(0.084f, 0.250f), V(0.074f, 0.256f), V(0.068f, 0.250f) }, 22);
        Lathe(head, "Nozzle mouth", dark, new[] { V(0.070f, 0.249f), V(0.050f, 0.241f), V(0.000f, 0.240f) }, 22); // outer→inner so the disc faces forward
    }

    private void Update()
    {
        if (owner == null) return;
        if (effects == null) effects = owner.GetComponentInChildren<WeaponVisualEffects>();
        bool working = effects != null && effects.Flowing && owner.Selected == PlayerWeaponSystem.Tool.Vacuum;
        fanSpeed = Mathf.MoveTowards(fanSpeed, working ? 1440f : 0f, Time.deltaTime * 2400f);
        if (fan != null && fanSpeed > 0.01f) fan.Rotate(0f, 0f, fanSpeed * Time.deltaTime, Space.Self);
        if (lamp == null) return;
        Color color = owner.Mode == PlayerWeaponSystem.VacuumMode.Water ? WaterLight : owner.Mode == PlayerWeaponSystem.VacuumMode.Disperse ? BlowLight : CollectLight;
        float glow = working ? 2.2f + Mathf.Sin(Time.time * 18f) * 0.4f : 1.2f;
        lamp.SetColor(BaseColor, color);
        lamp.SetColor(EmissionColor, color * glow);
    }

    private static Vector2 V(float radius, float z) => new Vector2(radius, z);

    private static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    // A surface of revolution around +z from a (radius, z) profile; smooth normals so the body reads as rounded plastic.
    private static Transform Lathe(Transform parent, string name, Material material, Vector2[] profile, int segments)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int s = 0; s <= segments; s++)
        {
            float a = s * Mathf.PI * 2f / segments;
            float c = Mathf.Cos(a), sn = Mathf.Sin(a);
            foreach (var p in profile) vertices.Add(new Vector3(c * p.x, sn * p.x, p.y));
        }
        int rows = profile.Length;
        for (int s = 0; s < segments; s++)
            for (int r = 0; r + 1 < rows; r++)
            {
                int a = s * rows + r, b = (s + 1) * rows + r;
                triangles.Add(a); triangles.Add(b); triangles.Add(a + 1); // wound so the faces look outwards
                triangles.Add(b); triangles.Add(b + 1); triangles.Add(a + 1);
            }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
        return go.transform;
    }

    private static Material Glass(Shader shader)
    {
        var m = new Material(shader) { name = "Vacuum canister" };
        m.color = new Color(0.75f, 0.92f, 0.95f, 0.38f);
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_Smoothness", 0.9f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }
}
