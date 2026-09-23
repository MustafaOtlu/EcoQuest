using UnityEngine;
using UnityEditor;
using System.IO;

public class RevertToGenericRig : EditorWindow
{
    [MenuItem("EcoQuest/Revert Characters To Generic Rig")]
    public static void RevertRig()
    {
        string[] fbxFiles = Directory.GetFiles("Assets/3D Assets/CHARACTER", "*.fbx", SearchOption.AllDirectories);
        
        foreach (string file in fbxFiles)
        {
            ModelImporter importer = AssetImporter.GetAtPath(file) as ModelImporter;
            if (importer != null)
            {
                // Revert to Generic
                importer.animationType = ModelImporterAnimationType.Generic;
                
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
        
        Debug.Log("Tüm karakter ve animasyon dosyaları tekrar Generic (Genel) iskelete döndürüldü!");
    }
}
