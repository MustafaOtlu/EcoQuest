using System.Collections.Generic;
using UnityEngine;

// Procedural low-poly run-of-river plant (GDD §9): turbine house, a wheel turning with real output, the flume that
// feeds it and the fish-pass steps that make the site legal. Materials are shared per colour.
public static class EcoHydroArt
{
    private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
    private static readonly Color Concrete = new Color(.62f, .62f, .57f), Wall = new Color(.86f, .8f, .66f), Roof = new Color(.55f, .22f, .18f),
        Wood = new Color(.45f, .3f, .18f), Metal = new Color(.25f, .29f, .31f), Water = new Color(.24f, .6f, .78f), Dark = new Color(.16f, .14f, .13f);

    public static GameObject Create(Vector3 position, Quaternion rotation)
    {
        var root = new GameObject("Küçük akarsu santrali"); root.transform.SetPositionAndRotation(position, rotation);
        var t = root.transform;
        Part(t, PrimitiveType.Cube, new Vector3(0, .15f, 0), new Vector3(4.6f, .3f, 3.6f), Concrete);
        // Turbine house with a pitched roof.
        Part(t, PrimitiveType.Cube, new Vector3(-.8f, 1.1f, 0), new Vector3(2.2f, 1.6f, 2.2f), Wall);
        Part(t, PrimitiveType.Cube, new Vector3(-1.32f, 2.12f, 0), new Vector3(1.35f, .14f, 2.5f), Roof).localRotation = Quaternion.Euler(0, 0, 35f);
        Part(t, PrimitiveType.Cube, new Vector3(-.28f, 2.12f, 0), new Vector3(1.35f, .14f, 2.5f), Roof).localRotation = Quaternion.Euler(0, 0, -35f);
        Part(t, PrimitiveType.Cube, new Vector3(-.8f, 1.95f, 0), new Vector3(1.4f, .5f, 2.2f), Wall); // gable fill
        Part(t, PrimitiveType.Cube, new Vector3(-.8f, .85f, -1.11f), new Vector3(.6f, 1.1f, .05f), Dark); // door
        Part(t, PrimitiveType.Cube, new Vector3(-1.5f, 1.3f, -1.11f), new Vector3(.4f, .4f, .05f), Water); // window
        var lamp = Part(t, PrimitiveType.Sphere, new Vector3(-.8f, 2.55f, 0), Vector3.one * .22f, new Color(.3f, .3f, .3f));
        // Flume and wheel.
        Part(t, PrimitiveType.Cube, new Vector3(1.25f, .32f, 0), new Vector3(.9f, .08f, 3.6f), Water);
        Part(t, PrimitiveType.Cube, new Vector3(.75f, .55f, 0), new Vector3(.12f, .5f, 3.6f), Concrete);
        Part(t, PrimitiveType.Cube, new Vector3(1.75f, .55f, 0), new Vector3(.12f, .5f, 3.6f), Concrete);
        Part(t, PrimitiveType.Cylinder, new Vector3(.45f, 1.25f, 0), new Vector3(.12f, .55f, .12f), Metal).localRotation = Quaternion.Euler(0, 0, 90f); // axle
        var wheel = new GameObject("Wheel").transform; wheel.SetParent(t, false); wheel.localPosition = new Vector3(1.25f, 1.25f, 0);
        Part(wheel, PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.9f, .1f, 1.9f), Wood).localRotation = Quaternion.Euler(0, 0, 90f);
        Part(wheel, PrimitiveType.Cylinder, Vector3.zero, new Vector3(.35f, .2f, .35f), Metal).localRotation = Quaternion.Euler(0, 0, 90f);
        for (int i = 0; i < 8; i++)
        {
            var paddle = Part(wheel, PrimitiveType.Cube, Vector3.zero, new Vector3(.5f, .08f, .5f), Wood);
            paddle.localRotation = Quaternion.Euler(i * 45f, 0, 0); paddle.localPosition = paddle.localRotation * new Vector3(0, .9f, 0);
        }
        // Fish pass: small pools stepping down beside the flume.
        for (int i = 0; i < 3; i++)
        {
            float z = -1.2f + i * 1.2f, h = .55f - i * .14f;
            Part(t, PrimitiveType.Cube, new Vector3(2.2f, h * .5f, z), new Vector3(.7f, h, 1.05f), Concrete);
            Part(t, PrimitiveType.Cube, new Vector3(2.2f, h + .01f, z), new Vector3(.55f, .04f, .9f), Water);
        }
        var collider = root.AddComponent<BoxCollider>(); collider.center = Vector3.up * 1.1f; collider.size = new Vector3(4.6f, 2.2f, 3.6f);
        var power = root.AddComponent<EcoPowerNode>(); power.kind = EcoPowerNode.Kind.Hydro; power.ratedPower = 6;
        root.AddComponent<EcoHydroPlant>();
        var spin = root.AddComponent<EcoHydroWheel>(); spin.wheel = wheel; spin.lamp = lamp.GetComponent<Renderer>();
        return root;
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 p, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type); go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
        Object.Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = MaterialFor(color);
        return go.transform;
    }

    public static Material MaterialFor(Color color)
    {
        if (materials.TryGetValue(color, out var m) && m != null) return m;
        var shader = Shader.Find("EcoQuest/LowPoly Vertex Lit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        m = new Material(shader); m.color = color; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        materials[color] = m; return m;
    }
}
