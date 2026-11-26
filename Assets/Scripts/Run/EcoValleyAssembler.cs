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
    public string Code => EcoValley.Code(Layout);
    public Transform Root { get; private set; }
    public Texture2D Minimap { get; private set; }

    private static readonly string[] FixedTiles = { "Tile_Border", "Tile_Glacier", "Tile_Sun", "Tile_City" };

    private void Awake()
    {
        Current = this;
        Layout = EcoValley.ConsumeRequest() ?? (EcoTile[])EcoValley.TrainingLayout.Clone();
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
            var tile = Spawn("Tile_" + kind, origin - EcoValley.HomeOrigin(kind));
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
        PlacePlayer();
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

    private Transform Spawn(string id, Vector2 offset)
    {
        var prefab = Resources.Load<GameObject>(TileFolder + id);
        if (prefab == null) { Debug.LogError("EcoValley: karo bulunamadı: Resources/" + TileFolder + id); return null; }
        var instance = Instantiate(prefab, Root);
        instance.name = id;
        instance.transform.SetPositionAndRotation(new Vector3(offset.x, 0f, offset.y), Quaternion.identity);
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
        void Blit(string id, Vector2 origin)
        {
            var tex = Resources.Load<Texture2D>(TileFolder + "Maps/" + id);
            if (tex == null || !tex.isReadable) return;
            int x0 = Mathf.RoundToInt(origin.x * ppm), y0 = Mathf.RoundToInt(origin.y * ppm);
            int w = Mathf.Min(tex.width, size - x0), h = Mathf.Min(tex.height, size - y0);
            if (w <= 0 || h <= 0) return;
            map.SetPixels(x0, y0, w, h, tex.GetPixels(0, 0, w, h));
            any = true;
        }
        Blit("Tile_Glacier", new Vector2(0f, 100f));
        Blit("Tile_Sun", new Vector2(50f, 0f));
        Blit("Tile_City", new Vector2(50f, 50f));
        for (int slot = 0; slot < Layout.Length && slot < EcoValley.SlotOrigins.Length; slot++)
            Blit("Tile_" + Layout[slot], EcoValley.SlotOrigins[slot]);
        if (!any) { Destroy(map); return null; }
        map.Apply(true, true);
        return map;
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
