using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.IO;

public class AssetIntegrationWizard : EditorWindow
{
    [MenuItem("EcoQuest/Asset Entegrasyonu")]
    public static void ShowWindow()
    {
        GetWindow<AssetIntegrationWizard>("Asset Entegrasyonu");
    }

    void OnGUI()
    {
        GUILayout.Label("Otomatik Asset Islemleri", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Oyuncu (Boy) Animasyonlarini Kur"))
        {
            SetupPlayerAnimations();
        }

        if (GUILayout.Button("Bina Verilerini (ScriptableObject) Olustur"))
        {
            CreateBuildingData();
        }
    }

    void CreateBuildingData()
    {
        string saveDir = "Assets/_Shared/Data/Buildings";
        if (!AssetDatabase.IsValidFolder("Assets/_Shared/Data"))
            AssetDatabase.CreateFolder("Assets/_Shared", "Data");
        if (!AssetDatabase.IsValidFolder("Assets/_Shared/Data/Buildings"))
            AssetDatabase.CreateFolder("Assets/_Shared/Data", "Buildings");

        CreateBuilding(saveDir, BuildingType.SolarPanel, "Gunes Paneli", 20, 10, 1, 2f, 0f, 10f);
        CreateBuilding(saveDir, BuildingType.WindTurbine, "Ruzgar Turbini", 40, 15, 3, 5f, 0f, 25f);
        CreateBuilding(saveDir, BuildingType.WaterTreatment, "Su Aritma Tesisi", 60, 30, 5, 0f, -2f, 50f);
        CreateBuilding(saveDir, BuildingType.RecyclingFacility, "Geri Donusum Tesisi", 100, 50, 10, 0f, -5f, 100f);

        AssetDatabase.SaveAssets();
        Debug.Log("BuildingData ScriptableObject'ler olusturuldu.");
    }

    void CreateBuilding(string path, BuildingType type, string name, int metal, int plastic, int yep, float energyProd, float energyCons, float health)
    {
        string fullPath = $"{path}/{type}.asset";
        if (AssetDatabase.LoadAssetAtPath<BuildingData>(fullPath) != null) return;

        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.buildingType = type;
        data.displayName = name;
        data.metalCost = metal;
        data.plasticCost = plastic;
        data.requiredYEPLevel = yep;
        data.energyProductionPerDay = energyProd;
        data.energyConsumptionPerDay = energyCons;
        data.maxHealth = health;
        
        AssetDatabase.CreateAsset(data, fullPath);
    }

    void SetupPlayerAnimations()
    {
        string basePath = "Assets/Kaynaklar/Ninja Adventure - Asset Pack/Ninja Adventure - Asset Pack/Actor/Character/Boy/SeparateAnim";
        string walkPath = $"{basePath}/Walk.png";
        string idlePath = $"{basePath}/Idle.png";
        
        ConfigureTexture(walkPath, 16);
        ConfigureTexture(idlePath, 16);

        var walkObjects = AssetDatabase.LoadAllAssetsAtPath(walkPath);
        var idleObjects = AssetDatabase.LoadAllAssetsAtPath(idlePath);

        System.Collections.Generic.List<Sprite> walkSprites = new();
        System.Collections.Generic.List<Sprite> idleSprites = new();
        
        foreach (var obj in walkObjects) if (obj is Sprite s) walkSprites.Add(s);
        foreach (var obj in idleObjects) if (obj is Sprite s) idleSprites.Add(s);

        if (walkSprites.Count < 16 || idleSprites.Count < 4)
        {
            Debug.LogError("Sprite dilimleme başarisiz veya eksik kare var.");
            return;
        }

        string saveDir = "Assets/Mustafa/Animations";
        if (!AssetDatabase.IsValidFolder(saveDir))
            AssetDatabase.CreateFolder("Assets/Mustafa", "Animations");

        string controllerPath = $"{saveDir}/PlayerAnimator.controller";
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        
        controller.AddParameter("Horizontal", AnimatorControllerParameterType.Float);
        controller.AddParameter("Vertical", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

        // TODO: Full blend tree requires creating AnimationClips.
        // For simplicity, we'll just set the first Idle sprite on the Player prefab.
        
        var player = GameObject.Find("Player");
        if (player != null)
        {
            var sr = player.GetComponent<SpriteRenderer>();
            sr.sprite = idleSprites[0]; // Down idle
            
            var anim = player.GetComponent<Animator>();
            if (anim == null) anim = player.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            
            EditorUtility.SetDirty(player);
        }
        
        Debug.Log("Oyuncu sprite'i ve Animator hazirlandi.");
    }

    void ConfigureTexture(string path, int cellSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.filterMode = FilterMode.Point;
        importer.spritePixelsPerUnit = 16;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null) return;
        
        int cols = tex.width / cellSize;
        int rows = tex.height / cellSize;

        var factory = new SpriteMetaData[cols * rows];
        int index = 0;
        
        for (int r = rows - 1; r >= 0; r--) // Unity uses bottom-left origin
        {
            for (int c = 0; c < cols; c++)
            {
                factory[index] = new SpriteMetaData
                {
                    name = $"{Path.GetFileNameWithoutExtension(path)}_{index}",
                    rect = new Rect(c * cellSize, r * cellSize, cellSize, cellSize),
                    alignment = 0,
                    pivot = new Vector2(0.5f, 0.5f)
                };
                index++;
            }
        }

        importer.spritesheet = factory;
        importer.SaveAndReimport();
    }
}
