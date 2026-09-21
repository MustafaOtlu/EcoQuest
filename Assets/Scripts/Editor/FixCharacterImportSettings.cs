using UnityEngine;
using UnityEditor;
using System.IO;

public class FixCharacterImportSettings : EditorWindow
{
    [MenuItem("EcoQuest/Fix Character & Animation Settings")]
    public static void FixSettings()
    {
        string[] fbxFiles = Directory.GetFiles("Assets/3D Assets/CHARACTER", "*.fbx", SearchOption.AllDirectories);
        
        foreach (string file in fbxFiles)
        {
            ModelImporter importer = AssetImporter.GetAtPath(file) as ModelImporter;
            if (importer != null)
            {
                // Set to Humanoid
                importer.animationType = ModelImporterAnimationType.Human;
                
                // Fix animations if it has them
                ModelImporterClipAnimation[] clipAnimations = importer.defaultClipAnimations;
                if (clipAnimations != null && clipAnimations.Length > 0)
                {
                    for (int i = 0; i < clipAnimations.Length; i++)
                    {
                        clipAnimations[i].lockRootRotation = true; // Bake Into Pose for Rotation
                        clipAnimations[i].lockRootPositionXZ = true; // Bake Into Pose for XZ
                        clipAnimations[i].lockRootHeightY = true; // Bake Into Pose for Y
                        clipAnimations[i].keepOriginalOrientation = true;
                        clipAnimations[i].keepOriginalPositionXZ = true;
                        clipAnimations[i].keepOriginalPositionY = true;
                    }
                    importer.clipAnimations = clipAnimations;
                }
                
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
            }
        }
        
        Debug.Log("Tüm karakter ve animasyon dosyaları Humanoid olarak ayarlandı ve rotasyonları düzeltildi!");
    }
}
