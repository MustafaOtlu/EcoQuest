using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the valley from region tiles before anything else in the scene wakes up: the glacier, the base plain, the
/// town and the border ring are fixed; the four side slots come from <see cref="EcoValley"/>. Every system that looks
/// for regions, ground or lakes (navigation, enemy zones, saves, projects, the HUD map) finds a finished world in its
/// own Awake/Start, so none of them needs to wait for a signal.
/// </summary>
[DefaultExecutionOrder(-32000)]
public sealed class EcoValleyAssembler : MonoBehaviour
{
    public const string TileFolder = "EcoTiles/";
    public static EcoValleyAssembler Current { get; private set; }
    public EcoTile[] Layout { get; private set; }
    public int[] Turns { get; private set; } = new int[4];
    public string Code => EcoValley.Code(Layout, Turns);
    public Transform Root { get; private set; }
    public Texture2D Minimap { get; private set; }

    private static readonly string[] FixedTiles = { "Tile_Border", "Tile_Glacier", "Tile_Sun", "Tile_City" };

    private void Awake()
    {
        Current = this;
        var layout = EcoValley.Parse(EcoValley.ConsumeRequest(), out var turns);
        Layout = layout ?? (EcoTile[])EcoValley.TrainingLayout.Clone();
        Turns = layout != null ? turns : new int[EcoValley.SlotOrigins.Length];
        Assemble();
    }
    private void OnDestroy() { if (Current == this) Current = null; if (Minimap != null) Destroy(Minimap); }

    private void Assemble()
    {
        var scene = gameObject.scene;
        // The scene keeps an empty "EcoWorld" root as a placeholder so the sibling order (and the save identities of
        // everything after it) stays stable. Anything left inside it from an older map build is dropped unseen.
        GameObject placeholder = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "EcoWorld");
        int index = placeholder != null ? placeholder.transform.GetSiblingIndex() : 0;
        if (placeholder != null)
        {
            for (int i = placeholder.transform.childCount - 1; i >= 0; i--)
            {
                var child = placeholder.transform.GetChild(i).gameObject;
                child.SetActive(false); Destroy(child);
            }
            Root = placeholder.transform;
        }
        else
        {
            Root = new GameObject("EcoWorld").transform;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(Root.gameObject, scene);
            Root.SetSiblingIndex(index);
        }
        Root.position = Vector3.zero;

        var placed = new List<(Transform tile, Rect rect)>();
        foreach (var id in FixedTiles)
        {
            var tile = Spawn(id, Vector2.zero);
            if (tile != null) placed.Add((tile, FixedRect(id)));
        }
        for (int slot = 0; slot < Layout.Length && slot < EcoValley.SlotOrigins.Length; slot++)
        {
            var kind = Layout[slot];
            Vector2 origin = EcoValley.SlotOrigins[slot];
            var tile = Spawn("Tile_" + kind, origin - EcoValley.HomeOrigin(kind), origin + Vector2.one * (EcoValley.TileSize * 0.5f), Turns[slot]);
            if (tile == null) continue;
            tile.name = EcoValley.TileName(kind) + " (" + EcoValley.SlotNames[slot] + ")";
            placed.Add((tile, new Rect(origin, Vector2.one * EcoValley.TileSize)));
        }
        foreach (var (tile, rect) in placed) CloseOuterSockets(tile, rect);

        var field = Root.gameObject.GetComponent<EcoPollutionField>();
        if (field == null) field = Root.gameObject.AddComponent<EcoPollutionField>();
        field.area = new Rect(0f, 0f, 150f, 150f);
        Minimap = ComposeMinimap();
        var hud = FindFirstObjectByType<EcoWorldHUD>(FindObjectsInactive.Include);
        if (hud != null && Minimap != null) hud.minimapTexture = Minimap;
        LabelStartSign();
        RetireDemoSet();
        PlacePlayer();
        ClearRoads();
        SettleBuildings();
        PlantTurningCircles();
    }

    // The first prototype's demo corner (a factory, two mini regions, a pond, a brush fire and three trees inside
    // PowerStarter) sits at fixed coordinates, so it showed up in the same spot of every valley and its factory stood
    // on a road. The tiles carry the real regions now. It is switched off before anything in it wakes up; the brush
    // fire stays in the scene (inactive) because runs and saves clone it as the fire template.
    private static readonly string[] DemoSet = { "Regional ecology", "Clean fishing pond" };
    private void RetireDemoSet()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(DemoSet, t.name) >= 0 && t.parent != null && t.parent.name.StartsWith("PowerStarter"))
                    t.gameObject.SetActive(false);
    }

    // Scene props were placed for the old fixed map, and a few tile props sit where a turned neighbour's road now runs.
    // Anything small that stands on a road surface moves to the nearest free, dry spot (GDD v5 §3).
    private static readonly string[] KeepInPlace = { "Player", "Cam's", "Enemys", "PowerStarter" };
    private void ClearRoads()
    {
        Physics.SyncTransforms();
        var roads = new HashSet<Collider>();
        foreach (var c in Root.GetComponentsInChildren<Collider>())
            if (c.transform.parent != null && IsRoad(c.transform)) roads.Add(c);
        if (roads.Count == 0) return;
        var movable = new List<Transform>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (root.transform == Root || !root.activeInHierarchy) continue;
            foreach (Transform child in root.transform)
            {
                if (!child.gameObject.activeInHierarchy || KeepInPlace.Any(k => child.name.StartsWith(k))) continue;
                if (child.name.StartsWith("Eco Field Station")) { foreach (Transform part in child) movable.Add(part); }
                else movable.Add(child);
            }
        }
        foreach (Transform tile in Root)
        {
            var props = tile.Find("Props");
            if (props != null) foreach (Transform prop in props) movable.Add(prop);
        }
        int moved = 0;
        foreach (var t in movable)
        {
            if (!Footprint(t, out var bounds) || bounds.size.x > 12f || bounds.size.z > 12f) continue;
            if (!OnRoad(bounds.center, bounds.extents, roads)) continue;
            if (!FreeSpot(t, bounds, roads, out var target)) continue;
            float lift = t.position.y - bounds.min.y;
            t.position = new Vector3(target.x + (t.position.x - bounds.center.x), target.y + lift, target.z + (t.position.z - bounds.center.z));
            moved++;
        }
        if (moved > 0) Physics.SyncTransforms();
    }

    /// <summary>
    /// The tiles' own buildings (the town's market, home, library and clinic, a few houses, the glacier station) were
    /// placed off one point of a curving street, so a corner could stand on the asphalt or the pavement. Each one that
    /// touches a road is slid by the smallest step (up to 6 m, in 16 directions) that clears every road surface and
    /// every other building. Doors, labels and markers are children and move with it.
    /// </summary>
    private void SettleBuildings()
    {
        Physics.SyncTransforms();
        var owner = new Dictionary<Collider, Transform>();
        var buildings = new List<Transform>();
        foreach (Transform tile in Root)
            foreach (Transform group in tile)
            {
                if (group.name == "Roads" || group.name == "Ground" || group.name == "Props") continue;
                foreach (Transform b in group)
                {
                    if (!b.gameObject.activeInHierarchy || !Footprint(b, out var bounds)) continue;
                    if ((bounds.size.x < 2.5f && bounds.size.z < 2.5f) || bounds.size.x > 30f || bounds.size.z > 30f) continue;
                    var colliders = b.GetComponentsInChildren<Collider>();
                    if (colliders.Length == 0) continue;
                    buildings.Add(b);
                    foreach (var c in colliders) owner[c] = b;
                }
            }
        int moved = 0;
        foreach (var b in buildings)
        {
            Footprint(b, out var bounds);
            // Where the building stands: points under its own colliders, seen from above.
            var samples = new List<Vector2>();
            for (float x = bounds.min.x + 0.3f; x <= bounds.max.x; x += 0.75f)
                for (float z = bounds.min.z + 0.3f; z <= bounds.max.z; z += 0.75f)
                    foreach (var hit in Physics.RaycastAll(new Vector3(x, bounds.max.y + 2f, z), Vector3.down, bounds.size.y + 4f, ~0, QueryTriggerInteraction.Ignore))
                        if (owner.TryGetValue(hit.collider, out var who) && who == b) { samples.Add(new Vector2(x, z)); break; }
            var evicted = new List<Transform>();
            if (samples.Count == 0 || !Blocked(b, samples, Vector2.zero, owner, false, false, evicted)) continue;
            // Clear road and pavement; failing that, a town landmark may take the lot of a small back-row house
            // ("House N"), which is removed; failing that too, at least the carriageway must be clear.
            bool landmark = !b.name.StartsWith("House ") && !b.name.StartsWith("Building ");
            bool done = false;
            foreach (var (pavementOk, evict) in new[] { (false, false), (false, landmark), (true, false), (true, landmark) })
            {
                if (done) break;
                for (float step = 0.5f; step <= 8f && !done; step += 0.5f)
                    for (int a = 0; a < 16 && !done; a++)
                    {
                        float angle = a * Mathf.PI / 8f;
                        var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * step;
                        var c = new Vector2(bounds.center.x, bounds.center.z) + offset;
                        evicted.Clear();
                        if (c.x < 2f || c.y < 2f || c.x > 148f || c.y > 148f || Blocked(b, samples, offset, owner, pavementOk, evict, evicted)) continue;
                        foreach (var house in evicted) { house.gameObject.SetActive(false); foreach (var col in house.GetComponentsInChildren<Collider>(true)) owner.Remove(col); }
                        b.position += new Vector3(offset.x, 0f, offset.y);
                        Physics.SyncTransforms();
                        moved++; done = true;
                    }
            }
            if (!done) Debug.LogWarning("EcoValleyAssembler: " + b.name + " yoldan çekilemedi");
        }
        if (moved > 0) Debug.Log("EcoValleyAssembler: " + moved + " yapı yoldan çekildi");
    }

    /// <summary>
    /// The town's dead ends finish in round turning circles that read as empty asphalt discs on the map. Each one gets a
    /// kerbed green island with a tree in the middle, as real cul-de-sacs have; the ring around it stays drivable. The
    /// circles are found in the junction fill mesh, where every circle is a fan of 28 triangles around its centre.
    /// </summary>
    private void PlantTurningCircles()
    {
        var turf = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Island turf", color = new Color(0.36f, 0.55f, 0.25f) };
        var kerb = new Material(turf) { name = "Island kerb", color = new Color(0.72f, 0.71f, 0.68f) };
        var treePrefab = Resources.Load<GameObject>("EcoArt/Tree");
        var grassPrefab = Resources.Load<GameObject>("EcoArt/GrassPatch");
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var cylinderMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
        Destroy(cylinder);
        int planted = 0;
        foreach (Transform tile in Root)
        {
            var group = new GameObject("Turning circle islands").transform;
            group.SetParent(tile, false);
            // The collider keeps the original mesh; the renderer's may already be a static-batched combination.
            foreach (var asphalt in tile.GetComponentsInChildren<MeshCollider>())
            {
                if (asphalt.name != "Asphalt" || !IsRoad(asphalt.transform) || asphalt.sharedMesh == null || !asphalt.sharedMesh.isReadable) continue;
                foreach (var (centre, radius) in TurningCircles(asphalt.sharedMesh))
                {
                    // Town circles are 3.8 m: a 1.4 m island leaves a 2.4 m ring, enough to turn a car.
                    if (radius < 3.3f) continue;
                    float island = Mathf.Clamp(radius - 2.4f, 0.9f, 2.2f);
                    var world = asphalt.transform.TransformPoint(centre);
                    float r = island * asphalt.transform.lossyScale.x;
                    var root = new GameObject("Turning circle island").transform;
                    root.SetParent(group, false); root.position = world;
                    Disc(root, "Kerb", cylinderMesh, kerb, r, 0.16f);
                    Disc(root, "Turf", cylinderMesh, turf, r - 0.15f, 0.2f);
                    if (treePrefab != null) Fit(Instantiate(treePrefab, world + Vector3.up * 0.2f, Quaternion.Euler(0f, planted * 67f, 0f), root), 3f);
                    if (grassPrefab != null)
                        for (int g = 0; g < 3; g++)
                        {
                            float a = (planted * 41f + g * 120f) * Mathf.Deg2Rad;
                            var spot = world + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (r * 0.55f) + Vector3.up * 0.2f;
                            Fit(Instantiate(grassPrefab, spot, Quaternion.Euler(0f, g * 90f, 0f), root), 0.9f);
                        }
                    planted++;
                }
            }
        }
        if (planted > 0) { Physics.SyncTransforms(); Debug.Log("EcoValleyAssembler: " + planted + " dönüş cebine ada"); }
    }

    private static void Disc(Transform parent, string name, Mesh mesh, Material material, float radius, float height)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * (height * 0.5f - 0.02f);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f); // the primitive is 2 m tall
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        go.AddComponent<MeshCollider>().sharedMesh = mesh; // solid: cars drive around it and litter lands on top
    }

    /// <summary>Scales a prop so its largest side is <paramref name="size"/>: a flat grass decal has no height to fit by.</summary>
    private static void Fit(GameObject go, float size)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
        if (!Footprint(go.transform, out var bounds)) return;
        float largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (largest > 0.01f) go.transform.localScale *= size / largest;
    }

    /// <summary>Fans of 28 triangles that share one centre and reach the same radius: the turning circles.</summary>
    private static IEnumerable<(Vector3 centre, float radius)> TurningCircles(Mesh mesh)
    {
        var v = mesh.vertices; var t = mesh.triangles;
        var fans = new Dictionary<Vector3Int, List<int>>();
        for (int i = 0; i + 2 < t.Length; i += 3)
        {
            var key = Vector3Int.RoundToInt(v[t[i]] * 50f);
            if (!fans.TryGetValue(key, out var list)) fans[key] = list = new List<int>();
            list.Add(i);
        }
        foreach (var fan in fans.Values)
        {
            if (fan.Count != 28) continue;
            var c = v[t[fan[0]]];
            float min = float.MaxValue, max = 0f;
            foreach (int i in fan)
                for (int k = 1; k <= 2; k++)
                {
                    var d = v[t[i + k]] - c; d.y = 0f;
                    min = Mathf.Min(min, d.magnitude); max = Mathf.Max(max, d.magnitude);
                }
            if (max - min < 0.05f) yield return (c, max);
        }
    }

    private static bool IsPavement(Collider c) => c.name.Contains("kaldırım") || c.name == "Corners" || c.transform.parent != null && c.transform.parent.name == "Kaldırımlar";

    private static bool Blocked(Transform self, List<Vector2> samples, Vector2 offset, Dictionary<Collider, Transform> owner, bool pavementOk,
        bool evict, List<Transform> evicted)
    {
        foreach (var s in samples)
            foreach (var hit in Physics.RaycastAll(new Vector3(s.x + offset.x, 120f, s.y + offset.y), Vector3.down, 240f, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = hit.collider.transform;
                if (t.IsChildOf(self)) continue;
                if (IsRoad(t) && !(pavementOk && IsPavement(hit.collider)) || hit.collider.name.Contains("Water") || hit.collider.GetComponentInParent<EcoPondBasin>() != null) return true;
                if (!owner.TryGetValue(hit.collider, out var other) || other == self) continue;
                if (evict && other.name.StartsWith("House ")) { if (!evicted.Contains(other)) evicted.Add(other); continue; }
                return true;
            }
        return false;
    }

    private static bool IsRoad(Transform t) => Under(t, "Roads");
    private static bool Under(Transform t, string group)
    {
        for (var p = t.parent; p != null; p = p.parent) if (p.name == group) return true;
        return false;
    }

    private static bool Footprint(Transform t, out Bounds bounds)
    {
        bounds = default; bool any = false;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled) continue;
            if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
        }
        return any;
    }

    // A 3×3 sample of the footprint (shrunk a little so a prop that only touches a kerb counts as clear).
    private static bool OnRoad(Vector3 center, Vector3 extents, HashSet<Collider> roads)
    {
        for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                var p = new Vector3(center.x + i * extents.x * 0.8f, 120f, center.z + j * extents.z * 0.8f);
                foreach (var hit in Physics.RaycastAll(p, Vector3.down, 240f, ~0, QueryTriggerInteraction.Ignore))
                    if (roads.Contains(hit.collider)) return true;
            }
        return false;
    }

    private static bool FreeSpot(Transform self, Bounds bounds, HashSet<Collider> roads, out Vector3 target)
    {
        target = default;
        var half = new Vector3(bounds.extents.x + 0.4f, Mathf.Max(0.5f, bounds.extents.y), bounds.extents.z + 0.4f);
        for (float radius = 1.5f; radius <= 18f; radius += 1.5f)
            for (int a = 0; a < 16; a++)
            {
                float angle = a * Mathf.PI / 8f;
                var c = bounds.center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (c.x < 2f || c.z < 2f || c.x > 148f || c.z > 98f) continue; // stay in the valley, below the glacier
                if (OnRoad(c, half, roads)) continue;
                // The first thing below the spot must be the tile's own ground, not a roof, a rock or water.
                if (!Physics.Raycast(new Vector3(c.x, 120f, c.z), Vector3.down, out var ground, 240f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (ground.transform.IsChildOf(self)) continue;
                if (!Under(ground.transform, "Ground") || ground.collider.GetComponentInParent<EcoPondBasin>() != null || ground.collider.name.Contains("Water")) continue;
                var box = new Vector3(c.x, ground.point.y + half.y + 0.15f, c.z);
                bool blocked = false;
                foreach (var other in Physics.OverlapBox(box, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                    if (!other.transform.IsChildOf(self) && other.GetComponent<EcoGround>() == null && !roads.Contains(other)) { blocked = true; break; }
                if (blocked) continue;
                target = new Vector3(c.x, ground.point.y, c.z);
                return true;
            }
        return false;
    }

    /// <summary>Where a fresh valley puts the player: on the base path, just north of the solar field.</summary>
    public static readonly Vector3 StartPoint = new Vector3(75f, 0f, 26f);

    // The scene keeps the player where the old fixed map had its base (in what is now the town); a fresh valley starts on
    // the base plain instead. A loaded save moves the player again afterwards.
    private void PlacePlayer()
    {
        var player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null) return;
        Physics.SyncTransforms();
        var p = StartPoint;
        p.y = Physics.Raycast(new Vector3(p.x, 80f, p.z), Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point.y + 0.1f : player.transform.position.y;
        var controller = player.GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f, 0f));
        if (controller != null) controller.enabled = was;
        Physics.SyncTransforms();
    }

    // A turned tile spins about the centre of its slot: world = centre + q · (authored + offset − centre).
    private Transform Spawn(string id, Vector2 offset, Vector2 pivot = default, int turn = 0)
    {
        var prefab = Resources.Load<GameObject>(TileFolder + id);
        if (prefab == null) { Debug.LogError("EcoValley: karo bulunamadı: Resources/" + TileFolder + id); return null; }
        var instance = Instantiate(prefab, Root);
        instance.name = id;
        var q = Quaternion.Euler(0f, 90f * (turn & 3), 0f);
        var c = new Vector3(pivot.x, 0f, pivot.y);
        instance.transform.SetPositionAndRotation(c + q * (new Vector3(offset.x, 0f, offset.y) - c), q);
        return instance.transform;
    }

    private static Rect FixedRect(string id) => id switch
    {
        "Tile_Glacier" => new Rect(0f, 100f, 150f, 50f),
        "Tile_Sun" => new Rect(50f, 0f, 50f, 50f),
        "Tile_City" => new Rect(50f, 50f, 50f, 50f),
        _ => new Rect(-1000f, -1000f, 1f, 1f),
    };

    // A road stub that leads to the map wall instead of a neighbouring tile is hidden (its flattened strip of ground stays).
    private static void CloseOuterSockets(Transform tile, Rect rect)
    {
        var roads = tile.Find("Roads");
        if (roads == null || rect.width < 2f) return;
        foreach (Transform stub in roads)
        {
            if (!stub.name.StartsWith("Bağlantı")) continue;
            var renderers = stub.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var c = new Vector2(bounds.center.x, bounds.center.z);
            float west = c.x - rect.xMin, east = rect.xMax - c.x, south = c.y - rect.yMin, north = rect.yMax - c.y;
            float nearest = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));
            Vector2 outward = nearest == west ? Vector2.left : nearest == east ? Vector2.right : nearest == south ? Vector2.down : Vector2.up;
            Vector2 edge = c + outward * (nearest + 1.5f);
            bool inside = edge.x > 0f && edge.x < 150f && edge.y > 0f && edge.y < 150f;
            if (!inside) stub.gameObject.SetActive(false);
        }
    }

    // The map image is put together from each tile's own top-down render (10.24 px per metre, 1536 px for 150 m).
    private Texture2D ComposeMinimap()
    {
        const int size = 1536; const float ppm = size / 150f;
        var map = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Eco valley map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var fill = new Color32[size * size];
        var ground = new Color32(74, 104, 62, 255);
        for (int i = 0; i < fill.Length; i++) fill[i] = ground;
        map.SetPixels32(fill);
        bool any = false;
        void Blit(string id, Vector2 origin, int turn)
        {
            var tex = Resources.Load<Texture2D>(TileFolder + "Maps/" + id);
            if (tex == null || !tex.isReadable) return;
            int x0 = Mathf.RoundToInt(origin.x * ppm), y0 = Mathf.RoundToInt(origin.y * ppm);
            int w = Mathf.Min(tex.width, size - x0), h = Mathf.Min(tex.height, size - y0);
            if (w <= 0 || h <= 0) return;
            var pixels = tex.GetPixels(0, 0, w, h);
            if ((turn & 3) != 0 && w == h) pixels = Turn(pixels, w, turn & 3);
            map.SetPixels(x0, y0, w, h, pixels);
            any = true;
        }
        Blit("Tile_Glacier", new Vector2(0f, 100f), 0);
        Blit("Tile_Sun", new Vector2(50f, 0f), 0);
        Blit("Tile_City", new Vector2(50f, 50f), 0);
        for (int slot = 0; slot < Layout.Length && slot < EcoValley.SlotOrigins.Length; slot++)
            Blit("Tile_" + Layout[slot], EcoValley.SlotOrigins[slot], Turns[slot]);
        if (!any) { Destroy(map); return null; }
        CoverOuterMouths(map, size, ppm);
        map.Apply(true, true);
        return map;
    }

    // The road mouths on the valley's outer wall are hidden in the world; on the map they are painted over with the
    // ground beside them (the strip 13 m along the wall), so no road seems to run off the edge.
    private static void CoverOuterMouths(Texture2D map, int size, float ppm)
    {
        var mouths = new[]
        {
            (new Vector2(0f, 25f), Vector2.right), (new Vector2(0f, 75f), Vector2.right),
            (new Vector2(150f, 25f), Vector2.left), (new Vector2(150f, 75f), Vector2.left),
            (new Vector2(25f, 0f), Vector2.up), (new Vector2(125f, 0f), Vector2.up),
        };
        var pixels = map.GetPixels32();
        foreach (var (mid, inward) in mouths)
        {
            var along = new Vector2(inward.y, -inward.x);
            for (float d = 0f; d < 5.2f; d += 0.5f / ppm)
                for (float a = -6.5f; a <= 6.5f; a += 0.5f / ppm)
                {
                    Vector2 p = mid + inward * d + along * a;
                    Vector2 q = mid + inward * d + along * (a + (a < 0f ? -13f : 13f));
                    int px = Mathf.Clamp(Mathf.FloorToInt(p.x * ppm), 0, size - 1), py = Mathf.Clamp(Mathf.FloorToInt(p.y * ppm), 0, size - 1);
                    int qx = Mathf.Clamp(Mathf.FloorToInt(q.x * ppm), 0, size - 1), qy = Mathf.Clamp(Mathf.FloorToInt(q.y * ppm), 0, size - 1);
                    pixels[py * size + px] = pixels[qy * size + qx];
                }
        }
        map.SetPixels32(pixels);
    }

    // Pixel x is world x and pixel y is world z. A clockwise quarter turn (Unity +90° about Y) sends local (x, z)
    // to (z, −x), so the destination pixel (u, v) reads the source at (n−1−v, u) for each turn.
    private static Color[] Turn(Color[] source, int n, int turns)
    {
        var result = source;
        for (int t = 0; t < turns; t++)
        {
            var next = new Color[n * n];
            for (int v = 0; v < n; v++)
                for (int u = 0; u < n; u++)
                    next[v * n + u] = result[u * n + (n - 1 - v)];
            result = next;
        }
        return result;
    }

    private void LabelStartSign()
    {
        var sun = Root.Find("Tile_Sun");
        if (sun == null) return;
        foreach (var text in sun.GetComponentsInChildren<Text>(true))
        {
            if (!text.text.StartsWith("BAŞLANGIÇ")) continue;
            text.text = "BAŞLANGIÇ KAVŞAĞI\nKUZEY  Şehir, sonra Buzul\nBATI  " + EcoValley.TileName(Layout[0]) + "\nDOĞU  " + EcoValley.TileName(Layout[1]);
        }
    }
}
