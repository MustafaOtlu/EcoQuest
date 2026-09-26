using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Rebuildable, curated CC0 models. Does not change the user's currently open scene.
public static class BuildEcoArt
{
    private const string Output = "Assets/Resources/EcoArt/";
    [MenuItem("EcoQuest/Build field station art")]
    public static void Build()
    {
        Directory.CreateDirectory(Output);
        AssetDatabase.Refresh();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
        var survival = Palette("Survival", shader);
        var food = Palette("Food", shader);
        Model("WaterBarrel", "Survival", "barrel-open", 0.8f, survival, true);
        Model("StorageBarrel", "Survival", "barrel", 0.75f, survival, true);
        Model("SupplyBox", "Survival", "box", 0.45f, survival, true);
        Model("OpenBox", "Survival", "box-open", 0.35f, survival, true);
        Model("Workbench", "Survival", "workbench", 0.85f, survival, true);
        Model("Fence", "Survival", "fence", 0.7f, survival, true);
        Model("Signpost", "Survival", "signpost-single", 1.15f, survival, true);
        Model("Tree", "Survival", "tree", 3.4f, survival, false);
        Model("Rock", "Survival", "rock-a", 0.45f, survival, true);
        Model("Grass", "Survival", "grass", 0.23f, survival, false);
        Model("GrassPatch", "Survival", "patch-grass", 0.12f, survival, false);
        Model("MetalScrap", "Food", "soda-can-crushed", 0.23f, food, true);
        Model("PlasticScrap", "Food", "soda-bottle", 0.32f, food, true);
        Model("Cabbage", "Food", "cabbage", 0.38f, food, false);
        var water = Solid("Water", shader, new Color(0.08f, 0.55f, 0.75f));
        var soil = Solid("Soil", shader, new Color(0.22f, 0.12f, 0.07f));
        var edging = Solid("GardenEdge", shader, new Color(0.36f, 0.25f, 0.14f));
        var turf = Solid("Turf", shader, new Color(0.18f, 0.29f, 0.15f));
        var station = new GameObject("Eco Field Station");
        try
        {
            // Working area at the player's right/rear; leave enemy approaches and spawn unobstructed.
            Primitive(station.transform, "Ground cover", new Vector3(6, 0.008f, -3.5f), new Vector3(9.5f, 0.012f, 9), turf, false);
            var barrel = Place(station, "WaterBarrel", new Vector3(2.2f, 0.02f, 2.5f), 0);
            barrel.AddComponent<WeaponWaterSource>().cleanWater = true;
            var b = WorldBounds(barrel);
            var surface = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            surface.name = "Clean water surface"; surface.transform.SetParent(barrel.transform, true);
            surface.transform.position = new Vector3(b.center.x, b.max.y - 0.09f, b.center.z);
            surface.transform.localScale = new Vector3(b.size.x * 0.73f, 0.012f, b.size.z * 0.73f);
            UnityEngine.Object.DestroyImmediate(surface.GetComponent<Collider>());
            surface.GetComponent<Renderer>().sharedMaterial = water;
            Label(station, "TEMİZ SU\n1 · TOPLAMA", new Vector3(2.2f, 1.25f, 2.5f), 180);
            Place(station, "Workbench", new Vector3(3.2f, 0.02f, -3.6f), 90);
            Place(station, "StorageBarrel", new Vector3(2f, 0.02f, -4.8f), 0);
            Place(station, "SupplyBox", new Vector3(2.2f, 0.02f, -2.5f), 10);
            Place(station, "OpenBox", new Vector3(3.3f, 0.88f, -3.6f), 0);
            Label(station, "GERİ DÖNÜŞÜM\n3 · R ile ham atığı işle", new Vector3(3.2f, 1.65f, -3.6f), 0);
            for (int i = 0; i < 6; i++)
            {
                bool metal = i % 2 == 0;
                var scrap = Place(station, metal ? "MetalScrap" : "PlasticScrap", new Vector3(3.5f + i * 0.55f, 0.02f, 2.5f), i * 47);
                var resource = scrap.AddComponent<RecyclableResource>();
                resource.metal = metal ? 2 : 0; resource.plastic = metal ? 0 : 1;
            }
            for (int i = 0; i < 4; i++)
            {
                var center = new Vector3(6.1f + (i % 2) * 2.3f, 0.05f, -1.7f - (i / 2) * 2.3f);
                var bed = Primitive(station.transform, "Planting Bed " + (i + 1), center, new Vector3(1.9f, 0.1f, 1.9f), soil, true);
                bed.AddComponent<PlantingSurface>();
                for(int side=0;side<2;side++)
                {
                    Primitive(station.transform, "Bed edging", center + new Vector3(side==0?-1:1, 0, 0), new Vector3(0.1f,0.15f,2.1f), edging, false);
                    Primitive(station.transform, "Bed edging", center + new Vector3(0, 0, side==0?-1:1), new Vector3(1.9f,0.15f,0.1f), edging, false);
                }
            }
            Place(station, "Signpost", new Vector3(5f, 0.02f, 0.5f), 180);
            Label(station, "EKİM ALANI\n2 · Tohum   /   1 · Su", new Vector3(6.7f,1.2f,0.1f), 180);
            for(int i=0;i<6;i++) Place(station,"Fence",new Vector3(2.2f+i*1.45f,0.02f,-6.5f),0);
            foreach(var pos in new[]{new Vector3(11,0,-8),new Vector3(12,0,2),new Vector3(-11,0,-8),new Vector3(11,0,11),new Vector3(-12,0,11)})
            {
                var tree=Place(station,"Tree",pos,20+pos.x*7);
                var trunk=tree.AddComponent<CapsuleCollider>();trunk.radius=0.18f;trunk.height=2;trunk.center=Vector3.up;
                Place(station,"GrassPatch",pos+new Vector3(0.6f,0.01f,0.3f),0);
                Place(station,"Grass",pos+new Vector3(-0.7f,0.01f,0.5f),33);
                Place(station,"Rock",pos+new Vector3(0.8f,0.01f,-1),70);
            }
            PrefabUtility.SaveAsPrefabAsset(station, Output + "FieldStation.prefab");
        }
        finally { UnityEngine.Object.DestroyImmediate(station); }
        AssetDatabase.SaveAssets();
        Debug.Log("ECO_ART_READY: 14 models and field station built.");
    }

    private static Material Palette(string pack, Shader shader)
    {
        string path="Assets/ThirdParty/Kenney/"+pack+"/Textures/colormap.png";
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var mat=Solid(pack+" Palette",shader,Color.white);
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));return mat;
    }
    private static Material Solid(string name,Shader shader,Color color)
    {
        string path=Output+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
        mat.shader=shader;mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",0.15f);return mat;
    }
    private static void Model(string name,string pack,string file,float height,Material mat,bool collide)
    {
        string path="Assets/ThirdParty/Kenney/"+pack+"/Models/"+file+".fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.importAnimation=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
        var root=new GameObject(name);
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        model.transform.SetParent(root.transform,false);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        { var mats=new Material[renderer.sharedMaterials.Length]; for(int i=0;i<mats.Length;i++)mats[i]=mat; renderer.sharedMaterials=mats; }
        var bounds=WorldBounds(root);float scale=height/Mathf.Max(0.001f,bounds.size.y);
        model.transform.localScale*=scale;
        bounds=WorldBounds(root);model.transform.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        bounds=WorldBounds(root);
        if(collide){var collider=root.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;}
        PrefabUtility.SaveAsPrefabAsset(root,Output+name+".prefab");UnityEngine.Object.DestroyImmediate(root);
    }
    private static Bounds WorldBounds(GameObject go)
    {
        var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);return bounds;
    }
    private static GameObject Place(GameObject parent,string prefab,Vector3 position,float yaw)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Output+prefab+".prefab"));
        go.transform.SetParent(parent.transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);return go;
    }
    private static GameObject Primitive(Transform parent,string name,Vector3 position,Vector3 size,Material mat,bool collide)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    private static void Label(GameObject parent,string text,Vector3 position,float yaw)
    {
        var go=new GameObject("Station sign",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent.transform,false);
        go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);go.transform.localScale=Vector3.one*0.0025f;
        go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<RectTransform>().sizeDelta=new Vector2(720,110);
        go.AddComponent<WorldSpaceLabel>();
        var plate=new GameObject("Plate",typeof(RectTransform),typeof(Image));plate.transform.SetParent(go.transform,false);
        plate.GetComponent<RectTransform>().sizeDelta=new Vector2(720,110);plate.GetComponent<Image>().color=new Color(0.04f,0.09f,0.065f,0.96f);plate.GetComponent<Image>().raycastTarget=false;
        var caption=new GameObject("Caption",typeof(RectTransform),typeof(Text));caption.transform.SetParent(go.transform,false);
        caption.GetComponent<RectTransform>().sizeDelta=new Vector2(700,100);var label=caption.GetComponent<Text>();label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=32;label.alignment=TextAnchor.MiddleCenter;label.text=text;label.color=new Color(0.95f,0.96f,0.85f);label.raycastTarget=false;
    }
}
