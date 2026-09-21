using UnityEngine;
using UnityEditor;
using System.IO;

public class FixTreeMaterials
{
    [MenuItem("Tools/Fix Tree Materials (LowPolyTree1)")]
    public static void FixMaterials()
    {
        string modelFolder = "Assets/3D Assets/ENVIRONMENT/Trees_Rocks/LowPolyTree1/Models";
        string textureFolder = "Assets/3D Assets/ENVIRONMENT/Trees_Rocks/LowPolyTree1/Textures";
        
        if (!AssetDatabase.IsValidFolder(modelFolder))
        {
            Debug.LogError("Model klasörü bulunamadı: " + modelFolder);
            return;
        }

        string[] daeFiles = Directory.GetFiles(modelFolder, "*.dae");
        
        string materialsFolder = modelFolder + "/Materials";
        if (!AssetDatabase.IsValidFolder(materialsFolder))
        {
            AssetDatabase.CreateFolder(modelFolder, "Materials");
        }
        
        string materialPath = materialsFolder + "/_trees_normal.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (mat == null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) urpShader = Shader.Find("Standard");
            mat = new Material(urpShader);
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        else
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null) mat.shader = urpShader;
        }
        
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(textureFolder + "/Colorsheet Tree Normal.png");
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
        }
        
        // Terrain üzerinde daha iyi çalışması için GPU Instancing'i açıyoruz
        mat.enableInstancing = true;
        
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        
        foreach (var file in daeFiles)
        {
            string assetPath = file.Replace("\\", "/");
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer != null)
            {
                importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                importer.materialSearch = ModelImporterMaterialSearch.Local;
                importer.materialLocation = ModelImporterMaterialLocation.External;
                importer.SaveAndReimport();
            }
        }
        
        Debug.Log("Başarıyla " + daeFiles.Length + " ağaç modelinin materyali düzeltildi ve URP uyumlu hale getirildi!");
    }
}
