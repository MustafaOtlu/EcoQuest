using System.Collections.Generic;
using UnityEngine;

// Local fixtures follow game time. Assets and the authored scene are never modified.
[DisallowMultipleComponent]
[DefaultExecutionOrder(-80)]
public sealed class EcoEveningLighting : MonoBehaviour
{
    public EcoWorldClock clock;
    [Range(0f, 24f)] public float switchOnHour = 17f, switchOffHour = 6f;
    [Range(1, 24)] public int maximumNearbyLights = 12;
    [Min(10f)] public float nearbyDistance = 45f;

    private sealed class Fixture
    {
        public Light light;
        public float intensity, distance;
    }
    private sealed class Surface
    {
        public Renderer renderer;
        public Material[] originals;
        public MaterialPropertyBlock originalBlock;
    }

    private readonly List<Fixture> fixtures = new List<Fixture>();
    private readonly List<Fixture> nearby = new List<Fixture>();
    private readonly List<Renderer> panes = new List<Renderer>();
    private readonly List<Surface> surfaces = new List<Surface>();
    private readonly List<GameObject> generated = new List<GameObject>();
    private readonly List<Material> materials = new List<Material>();
    private readonly Dictionary<Mesh, Mesh> houseMeshes = new Dictionary<Mesh, Mesh>();
    private readonly HashSet<int> seen = new HashSet<int>();
    private Material glow;
    private bool initialized, explicitScope;
    private float level = -1f, nextDiscovery, nextLightSelection;
    private Camera view;
    private MaterialPropertyBlock emission;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Warm = new Color(1f, 0.69f, 0.30f);

    private void Awake() => EnsureEmissionBlock();
    private void EnsureEmissionBlock()
    {
        if (emission == null) emission = new MaterialPropertyBlock();
    }

    public bool IsEvening(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        return switchOnHour < switchOffHour
            ? hour >= switchOnHour && hour < switchOffHour
            : hour >= switchOnHour || hour < switchOffHour;
    }

    private void Start()
    {
        if (clock == null) clock = GetComponent<EcoWorldClock>();
        if (!initialized) Discover(null);
        RefreshLighting(clock != null ? clock.hour : 9f, Camera.main, true);
    }

    private void Update()
    {
        if (clock == null) clock = EcoWorldClock.Current;
        if (clock == null || clock != EcoWorldClock.Current) { SetDark(); return; }
        if (!explicitScope && Time.unscaledTime >= nextDiscovery)
        {
            nextDiscovery = Time.unscaledTime + 10f;
            Discover(null);
        }
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        RefreshLighting(clock.hour, view, false);
    }

    // Used for isolated editor previews; it only discovers descendants of the supplied clone group.
    public void InitializeForScope(Transform scope)
    {
        if (initialized) return;
        explicitScope = true;
        Discover(scope);
    }

    public void RefreshLighting(float hour, Camera camera, bool immediate)
    {
        EnsureEmissionBlock();
        float target = IsEvening(hour) ? 1f : 0f;
        level = immediate || level < 0f ? target : Mathf.MoveTowards(level, target, Time.deltaTime * 2f);
        bool on = level > 0.001f;
        foreach (var pane in panes) if (pane != null) pane.enabled = on;
        foreach (var surface in surfaces)
        {
            if (surface.renderer == null) continue;
            surface.renderer.GetPropertyBlock(emission);
            emission.SetColor(EmissionColor, Warm * (2.7f * level));
            surface.renderer.SetPropertyBlock(emission);
        }
        if (glow != null) glow.SetColor(EmissionColor, Warm * (2.7f * level));

        if (immediate || Time.unscaledTime >= nextLightSelection)
        {
            nextLightSelection = Time.unscaledTime + 0.25f;
            nearby.Clear();
            Vector3 anchor = camera != null ? camera.transform.position : transform.position;
            float maxDistance = nearbyDistance * nearbyDistance;
            foreach (var fixture in fixtures)
            {
                if (fixture.light == null) continue;
                fixture.light.enabled = false;
                fixture.distance = (fixture.light.transform.position - anchor).sqrMagnitude;
                if (fixture.distance > maxDistance) continue;
                if (camera != null)
                {
                    Vector3 point = camera.WorldToViewportPoint(fixture.light.transform.position);
                    if (point.z <= 0f || point.x < -0.2f || point.x > 1.2f || point.y < -0.2f || point.y > 1.2f) continue;
                }
                nearby.Add(fixture);
            }
            nearby.Sort((a, b) => a.distance.CompareTo(b.distance));
        }
        for (int i = 0; i < nearby.Count; i++)
        {
            var fixture = nearby[i];
            if (fixture.light == null) continue;
            fixture.light.intensity = fixture.intensity * level;
            fixture.light.enabled = on && i < maximumNearbyLights;
        }
    }

    private void Discover(Transform scope)
    {
        initialized = true;
        Renderer[] renderers = scope != null ? scope.GetComponentsInChildren<Renderer>(true)
            : FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || seen.Contains(renderer.GetInstanceID())) continue;
            if (scope == null && renderer.gameObject.scene != gameObject.scene) continue;
            string name = renderer.name.ToLowerInvariant();
            if (name.StartsWith("eco evening")) continue;
            var meshFilter = renderer.GetComponent<MeshFilter>();
            var sourceMaterials = renderer.sharedMaterials;
            bool window = name == "window" || name == "window up" || name == "windows";
            bool lamp = false, modernHouse = false;
            bool proceduralLamp = false;
            for (Transform t = renderer.transform; t != null; t = t.parent)
            {
                string parentName = t.name.ToLowerInvariant();
                if (parentName == "street lamp") { lamp = true; proceduralLamp = true; }
                if (parentName.Contains("street+lamp") || parentName.Contains("street lamp 3d")) lamp = true;
            }
            if (proceduralLamp && name != "lamp head") lamp = false;
            foreach (var material in sourceMaterials)
            {
                if (material == null) continue;
                // This is the atlas-backed house already used by the City's Building/House instances.
                modernHouse |= material.name.StartsWith("tripo_36893c51_51bc_41a3_b8b7_eb9e78d2c5e4");
                lamp |= name == "lamp head" && material.name.StartsWith("LampGlow");
            }
            if (!window && !lamp && !modernHouse) continue;
            seen.Add(renderer.GetInstanceID());
            if (window) AddWindowSurface(renderer);
            else if (modernHouse && meshFilter != null && meshFilter.sharedMesh != null) AddHouseWindows(renderer, meshFilter);
            else if (lamp) AddStreetLamp(renderer, meshFilter);
        }
    }

    private Material GlowMaterial()
    {
        if (glow != null) return glow;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;
        glow = new Material(shader) { name = "Eco evening window glow", hideFlags = HideFlags.DontSave };
        glow.color = Warm;
        if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", Warm);
        glow.EnableKeyword("_EMISSION"); glow.SetColor(EmissionColor, Warm * 2.7f);
        if (glow.HasProperty("_Smoothness")) glow.SetFloat("_Smoothness", 0.15f);
        materials.Add(glow);
        return glow;
    }

    private void AddWindowSurface(Renderer renderer, bool addPool = true)
    {
        var originals = renderer.sharedMaterials;
        var copies = new Material[originals.Length];
        for (int i = 0; i < originals.Length; i++)
        {
            if (originals[i] == null) continue;
            var copy = new Material(originals[i]) { name = "Eco evening " + originals[i].name, hideFlags = HideFlags.DontSave };
            copy.EnableKeyword("_EMISSION"); copy.SetColor(EmissionColor, Color.black);
            materials.Add(copy); copies[i] = copy;
        }
        renderer.sharedMaterials = copies;
        var originalBlock = new MaterialPropertyBlock(); renderer.GetPropertyBlock(originalBlock);
        surfaces.Add(new Surface { renderer = renderer, originals = originals, originalBlock = originalBlock });
        // A real warm pool of light below the pane, in addition to the visible emissive glass.
        Vector3 normal = -renderer.transform.forward;
        if (addPool) AddLight(renderer.transform, renderer.bounds.center + normal * 0.4f, false, 1.8f, 4f);
    }

    private Renderer AddGlowMesh(Transform parent, Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount == 0 || GlowMaterial() == null) return null;
        var go = new GameObject("Eco evening glass", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = glow;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false; renderer.enabled = false;
        generated.Add(go); panes.Add(renderer); seen.Add(renderer.GetInstanceID());
        return renderer;
    }

    private void AddHouseWindows(Renderer renderer, MeshFilter filter)
    {
        var mesh = filter.sharedMesh;
        if (!mesh.isReadable) return;
        if (!houseMeshes.TryGetValue(mesh, out var windows))
        {
            windows = BuildHousePanes(mesh, renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture as Texture2D : null);
            houseMeshes.Add(mesh, windows);
        }
        var panesRenderer = AddGlowMesh(filter.transform, windows);
        if (panesRenderer == null) return;
        // One nearby fill per imported house keeps the local-light count modest.
        var b = renderer.bounds;
        AddLight(filter.transform, new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z) - renderer.transform.forward * b.extents.z,
            false, 2f, 5f);
    }

    private void AddStreetLamp(Renderer renderer, MeshFilter filter)
    {
        Vector3 head = renderer.bounds.center + Vector3.up * renderer.bounds.extents.y * 0.92f;
        if (renderer.name == "Lamp head") AddWindowSurface(renderer, false);
        else if (filter != null && filter.sharedMesh != null && filter.sharedMesh.isReadable)
        {
            var mesh = filter.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
            var selected = new List<int>(); Vector3 weighted = Vector3.zero; float area = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                Vector3 cross = Vector3.Cross(b - a, c - a); Vector3 center = (a + b + c) / 3f;
                if (center.y < mesh.bounds.max.y - mesh.bounds.size.y * 0.14f || cross.normalized.y > -0.55f) continue;
                selected.Add(triangles[i]); selected.Add(triangles[i + 1]); selected.Add(triangles[i + 2]);
                float weight = cross.magnitude; weighted += center * weight; area += weight;
            }
            if (area > 0.0001f)
            {
                var lit = new Mesh { name = "Eco evening lamp glass", hideFlags = HideFlags.DontSave };
                lit.vertices = vertices; lit.triangles = selected.ToArray(); lit.RecalculateNormals(); lit.RecalculateBounds();
                houseMeshes[lit] = lit;
                AddGlowMesh(filter.transform, lit); head = filter.transform.TransformPoint(weighted / area) - Vector3.up * 0.04f;
            }
        }
        AddLight(renderer.transform, head, true, 15f, 9f);
    }

    private void AddLight(Transform parent, Vector3 worldPosition, bool spot, float intensity, float range)
    {
        var go = new GameObject(spot ? "Eco evening street light" : "Eco evening window light", typeof(Light));
        go.transform.SetParent(parent, true); go.transform.position = worldPosition;
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        var light = go.GetComponent<Light>(); light.type = spot ? LightType.Spot : LightType.Point;
        light.color = Warm; light.range = range; light.intensity = intensity; light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel; light.spotAngle = 100f; light.innerSpotAngle = 55f;
        light.enabled = false;
        fixtures.Add(new Fixture { light = light, intensity = intensity }); generated.Add(go);
    }

    // Only the registered modern-house asset uses this profile. Copy its actual glass triangles,
    // rather than placing flat quads behind the recessed panes or illuminating its shared wall atlas.
    private static Mesh BuildHousePanes(Mesh mesh, Texture2D atlas)
    {
        Texture2D readable = ReadAtlas(atlas);
        if (readable == null) return null;
        try
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles; var uv = mesh.uv;
            if (uv.Length != vertices.Length) return null;
            var points = new List<Vector3>(); var indices = new List<int>(); Bounds bounds = mesh.bounds;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                Vector3 a = vertices[ia], b = vertices[ib], c = vertices[ic];
                Vector3 center = (a + b + c) / 3f;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                float y = (center.y - bounds.min.y) / bounds.size.y;
                if (!((y > 0.14f && y < 0.31f) || (y > 0.57f && y < 0.74f))) continue;
                float x = Mathf.Abs(center.x - bounds.center.x) / bounds.size.x;
                float z = Mathf.Abs(center.z - bounds.center.z) / bounds.size.z;
                bool front = z > 0.27f && z < 0.34f && x > 0.155f && x < 0.29f && Mathf.Abs(normal.z) > 0.6f;
                bool side = x > 0.31f && x < 0.37f && z > 0.08f && z < 0.21f && Mathf.Abs(normal.x) > 0.6f;
                if (!front && !side) continue;
                Vector2 sample = (uv[ia] + uv[ib] + uv[ic]) / 3f;
                Color glass = readable.GetPixelBilinear(sample.x, sample.y);
                if ((glass.r + glass.g + glass.b) / 3f < 0.58f
                    || Mathf.Max(glass.r, Mathf.Max(glass.g, glass.b)) - Mathf.Min(glass.r, Mathf.Min(glass.g, glass.b)) > 0.22f) continue;
                Vector3 outward = front ? Vector3.forward * Mathf.Sign(center.z) : Vector3.right * Mathf.Sign(center.x);
                Vector3 offset = outward * (bounds.size.y * 0.002f);
                int first = points.Count;
                points.Add(a + offset); points.Add(b + offset); points.Add(c + offset);
                indices.Add(first); indices.Add(first + 1); indices.Add(first + 2);
            }
            var result = new Mesh { name = "Eco evening modern house panes", hideFlags = HideFlags.DontSave };
            result.SetVertices(points); result.SetTriangles(indices, 0); result.RecalculateNormals(); result.RecalculateBounds();
            return result;
        }
        finally { Release(readable); }
    }

    private static Texture2D ReadAtlas(Texture2D source)
    {
        if (source == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return null;
        var previous = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1024, 1024, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            Graphics.Blit(source, target); RenderTexture.active = target;
            var readable = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0f, 0f, 1024f, 1024f), 0, 0); readable.Apply(); return readable;
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
    }

    private void SetDark()
    {
        EnsureEmissionBlock();
        foreach (var pane in panes) if (pane != null) pane.enabled = false;
        foreach (var fixture in fixtures) if (fixture.light != null) fixture.light.enabled = false;
        foreach (var surface in surfaces) if (surface.renderer != null)
        {
            surface.renderer.GetPropertyBlock(emission); emission.SetColor(EmissionColor, Color.black); surface.renderer.SetPropertyBlock(emission);
        }
        level = 0f;
    }
    private void OnDisable() => SetDark();

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }

    private void OnDestroy()
    {
        foreach (var surface in surfaces) if (surface.renderer != null)
        { surface.renderer.sharedMaterials = surface.originals; surface.renderer.SetPropertyBlock(surface.originalBlock); }
        foreach (var go in generated) Release(go);
        foreach (var material in materials) Release(material);
        var released = new HashSet<Mesh>();
        foreach (var mesh in houseMeshes.Values) if (mesh != null && released.Add(mesh)) Release(mesh);
    }
}
