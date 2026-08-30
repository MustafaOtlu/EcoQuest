using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

public class SceneSetupWizard : EditorWindow
{
    [MenuItem("EcoQuest/Sahne Kurulumu")]
    static void ShowWindow()
    {
        GetWindow<SceneSetupWizard>("EcoQuest Kurulum");
    }

    void OnGUI()
    {
        GUILayout.Label("EcoQuest Sahne Kurulumu", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Tum Sahneyi Kur", GUILayout.Height(40)))
            SetupFullScene();

        GUILayout.Space(10);
        GUILayout.Label("Tek Tek Kurulum", EditorStyles.boldLabel);

        if (GUILayout.Button("GameManager Olustur"))
            CreateGameManager();
        if (GUILayout.Button("Oyuncu Olustur"))
            CreatePlayer();
        if (GUILayout.Button("Kamera Ayarla"))
            SetupCamera();
        if (GUILayout.Button("Gun/Gece Dongusu Olustur"))
            CreateDayNightCycle();
        if (GUILayout.Button("Tilemap Grid Olustur"))
            CreateTilemapGrid();
        if (GUILayout.Button("Bolgeleri Olustur"))
            CreateBiomeZones();
        if (GUILayout.Button("Tilemap Boya (Assetler Gerekli)"))
            PaintBiomes();
        if (GUILayout.Button("BuildMode Manager Olustur"))
            CreateBuildModeManager();
    }

    void SetupFullScene()
    {
        CreateGameManager();
        CreatePlayer();
        SetupCamera();
        CreateDayNightCycle();
        CreateTilemapGrid();
        CreateBiomeZones();
        CreateBuildModeManager();
        PaintBiomes();
        Debug.Log("EcoQuest sahne kurulumu tamamlandi!");
    }

    void CreateGameManager()
    {
        if (Object.FindFirstObjectByType<GameManager>() != null)
        {
            Debug.LogWarning("GameManager zaten mevcut.");
            return;
        }
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
        Undo.RegisterCreatedObjectUndo(go, "GameManager Olustur");
    }

    void CreatePlayer()
    {
        if (Object.FindFirstObjectByType<PlayerController>() != null)
        {
            Debug.LogWarning("Player zaten mevcut.");
            return;
        }
        var go = new GameObject("Player");
        go.tag = "Player";
        go.layer = LayerMask.NameToLayer("Default");

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateDefaultSprite();
        sr.sortingOrder = 10;

        var pc = go.AddComponent<PlayerController>();

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 0.8f);

        go.transform.position = Vector3.zero;
        Undo.RegisterCreatedObjectUndo(go, "Player Olustur");
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0, 0, -10);
        
        // 2D icin Skybox yerine Duz Renk (Solid Color) kullan
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f); // Koyu gri/siyah arka plan

        if (cam.GetComponent<CameraController>() == null)
        {
            var cc = cam.gameObject.AddComponent<CameraController>();
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (player != null)
                cc.SetPlayer(player.transform);
        }

        if (cam.GetComponent<UniversalAdditionalCameraData>() == null)
            cam.gameObject.AddComponent<UniversalAdditionalCameraData>();

        Undo.RegisterCompleteObjectUndo(cam.gameObject, "Kamera Ayarla");
    }

    void CreateDayNightCycle()
    {
        if (Object.FindFirstObjectByType<DayNightCycle>() != null)
        {
            Debug.LogWarning("DayNightCycle zaten mevcut.");
            return;
        }

        var go = new GameObject("DayNightCycle");
        var dnc = go.AddComponent<DayNightCycle>();

        var lightGo = new GameObject("GlobalLight2D");
        lightGo.transform.SetParent(go.transform);
        var light = lightGo.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        light.color = Color.white;

        Undo.RegisterCreatedObjectUndo(go, "DayNightCycle Olustur");
    }

    void CreateTilemapGrid()
    {
        if (Object.FindFirstObjectByType<Grid>() != null)
        {
            Debug.LogWarning("Grid zaten mevcut.");
            return;
        }

        var gridGo = new GameObject("Grid");
        gridGo.AddComponent<Grid>();

        string[] layers = { "Ground", "Terrain", "Objects", "Overlay" };
        for (int i = 0; i < layers.Length; i++)
        {
            var tmGo = new GameObject(layers[i]);
            tmGo.transform.SetParent(gridGo.transform);
            var tm = tmGo.AddComponent<Tilemap>();
            var tr = tmGo.AddComponent<TilemapRenderer>();
            tr.sortingOrder = i;
        }

        Undo.RegisterCreatedObjectUndo(gridGo, "Tilemap Grid Olustur");
    }

    void CreateBiomeZones()
    {
        if (Object.FindFirstObjectByType<BiomeManager>() != null)
        {
            Debug.LogWarning("BiomeManager zaten mevcut.");
            return;
        }

        var managerGo = new GameObject("BiomeManager");
        managerGo.AddComponent<BiomeManager>();

        var biomes = new (string name, BiomeType type, Vector2 pos, Vector2 size, Color color)[]
        {
            ("Buzul",       BiomeType.Glacier,    new Vector2(0, 40),    new Vector2(60, 20), new Color(0.7f, 0.9f, 1f)),
            ("Ruzgarli",    BiomeType.Windy,      new Vector2(-30, 10), new Vector2(20, 30), new Color(0.6f, 0.8f, 0.6f)),
            ("Sehir",       BiomeType.City,       new Vector2(0, 15),    new Vector2(30, 20), new Color(0.7f, 0.7f, 0.7f)),
            ("Gunesli",     BiomeType.Sunny,      new Vector2(0, -10),   new Vector2(40, 20), new Color(1f, 0.9f, 0.5f)),
            ("Sanayi",      BiomeType.Industrial, new Vector2(30, 25),  new Vector2(20, 20), new Color(0.5f, 0.4f, 0.4f)),
            ("TemizSu",     BiomeType.FreshWater, new Vector2(-20, -15), new Vector2(15, 15), new Color(0.3f, 0.6f, 1f)),
        };

        foreach (var (bName, bType, bPos, bSize, bColor) in biomes)
        {
            var go = new GameObject($"Biome_{bName}");
            go.transform.SetParent(managerGo.transform);
            go.transform.position = new Vector3(bPos.x, bPos.y, 0);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = bSize;

            var zone = go.AddComponent<BiomeZone>();
            var serialized = new SerializedObject(zone);
            serialized.FindProperty("biomeType").enumValueIndex = (int)bType;
            serialized.FindProperty("gizmoColor").colorValue = bColor;
            serialized.ApplyModifiedProperties();
        }

        Undo.RegisterCreatedObjectUndo(managerGo, "Bolgeler Olustur");
    }

    void PaintBiomes()
    {
        var ground = GameObject.Find("Grid/Ground")?.GetComponent<Tilemap>();
        if (ground == null)
        {
            Debug.LogError("Grid/Ground Tilemap bulunamadi.");
            return;
        }

        var bm = Object.FindFirstObjectByType<BiomeManager>();
        if (bm == null)
        {
            Debug.LogError("BiomeManager bulunamadi.");
            return;
        }

        // Yuklenmesi beklenen assetler (AssetIntegrationWizard uzerinden uretilmeli)
        Tile floorTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Floor_Tile_1.asset");
        Tile waterTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Water_Tile_1.asset");
        Tile snowTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Floor_Tile_64.asset"); // Varsayimsal snow tile
        Tile sandTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Floor_Tile_32.asset"); // Varsayimsal sand tile
        Tile grassTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Floor_Tile_5.asset"); // Varsayimsal grass tile
        Tile concreteTile = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Shared/Tiles/Floor_Tile_128.asset"); // Varsayimsal

        if (floorTile == null || waterTile == null)
        {
            Debug.LogWarning("Zemin tile assetleri tam bulunamadi! Sadece varsayilan renkler kullanilacak.");
        }

        Undo.RecordObject(ground, "Bolgeleri Boya");
        ground.ClearAllTiles();

        var zones = Object.FindObjectsByType<BiomeZone>(FindObjectsSortMode.None);
        foreach (var zone in zones)
        {
            var col = zone.GetComponent<BoxCollider2D>();
            if (col == null) continue;

            int minX = Mathf.FloorToInt(col.bounds.min.x);
            int maxX = Mathf.CeilToInt(col.bounds.max.x);
            int minY = Mathf.FloorToInt(col.bounds.min.y);
            int maxY = Mathf.CeilToInt(col.bounds.max.y);

            Tile selectedTile = floorTile; // Varsayilan

            switch (zone.BiomeType)
            {
                case BiomeType.Glacier: selectedTile = snowTile ?? floorTile; break;
                case BiomeType.Sunny: selectedTile = sandTile ?? floorTile; break;
                case BiomeType.Windy: selectedTile = grassTile ?? floorTile; break;
                case BiomeType.City:
                case BiomeType.Industrial: selectedTile = concreteTile ?? floorTile; break;
                case BiomeType.FreshWater: selectedTile = waterTile ?? floorTile; break;
            }

            if (selectedTile == null) continue;

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    ground.SetTile(new Vector3Int(x, y, 0), selectedTile);
                }
            }
        }
        Debug.Log("Harita boyandi!");
    }

    void CreateBuildModeManager()
    {
        if (Object.FindFirstObjectByType<BuildModeManager>() != null)
        {
            Debug.LogWarning("BuildModeManager zaten mevcut.");
            return;
        }

        var go = new GameObject("BuildModeManager");
        var bmm = go.AddComponent<BuildModeManager>();

        var serialized = new SerializedObject(bmm);

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
            serialized.FindProperty("playerController").objectReferenceValue = player;

        var cam = Object.FindFirstObjectByType<CameraController>();
        if (cam != null)
            serialized.FindProperty("cameraController").objectReferenceValue = cam;

        serialized.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(go, "BuildModeManager Olustur");
    }

    Sprite CreateDefaultSprite()
    {
        var tex = new Texture2D(16, 16);
        var colors = new Color[16 * 16];

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (y >= 10)
                    colors[y * 16 + x] = new Color(0.2f, 0.6f, 0.2f);
                else if (y >= 4)
                    colors[y * 16 + x] = new Color(0.3f, 0.5f, 0.8f);
                else
                    colors[y * 16 + x] = new Color(0.6f, 0.4f, 0.3f);

                bool isEdge = x == 0 || x == 15 || y == 0 || y == 15;
                if (isEdge)
                    colors[y * 16 + x] = new Color(0.1f, 0.1f, 0.1f);
            }
        }

        tex.SetPixels(colors);
        tex.filterMode = FilterMode.Point;
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
    }
}
