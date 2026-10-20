using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SetupBuildingAndTreeHitboxes : AssetPostprocessor
{
    private const string SessionKey = "EcoQuest_SceneHitboxesApplied_v1";

    private void OnPreprocessModel()
    {
        if (string.IsNullOrEmpty(assetPath)) return;

        if (IsBuildingOrTreeAssetPath(assetPath))
        {
            ModelImporter importer = assetImporter as ModelImporter;
            if (importer != null)
            {
                importer.addCollider = true;
                importer.isReadable = true;
            }
        }
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (root == null || string.IsNullOrEmpty(assetPath)) return;

        if (IsTreeAssetPath(assetPath))
        {
            if (root.GetComponent<CapsuleCollider>() == null)
            {
                SceneHitboxInitializer.AddFittedTreeCapsuleCollider(root);
            }
        }
        else if (IsBuildingOrTreeAssetPath(assetPath))
        {
            SceneHitboxInitializer.AddCollidersToBuildingOrProp(root);
        }
    }

    private static bool IsBuildingOrTreeAssetPath(string path)
    {
        return path.StartsWith("Assets/Zipler ve Modeller/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("Assets/3D Assets/ENVIRONMENT/Trees_Rocks/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("Assets/ThirdParty/Kenney/Survival/Models/tree.fbx", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTreeAssetPath(string path)
    {
        return path.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("spruce", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
               path.IndexOf("palm", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    [InitializeOnLoadMethod]
    private static void AutoApplyOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SessionKey, false)) return;

            SessionState.SetBool(SessionKey, true);
            ApplyHitboxesToOpenScene();
        };
    }

    [MenuItem("Tools/Add Hitboxes to Buildings and Trees")]
    public static void ApplyHitboxesToOpenScene()
    {
        // Ensure Tree.prefab has a CapsuleCollider
        const string treePrefabPath = "Assets/Resources/EcoArt/Tree.prefab";
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(treePrefabPath);
        if (treePrefab != null && treePrefab.GetComponent<Collider>() == null)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(treePrefabPath);
            try
            {
                SceneHitboxInitializer.AddFittedTreeCapsuleCollider(root);
                PrefabUtility.SaveAsPrefabAsset(root, treePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Reimport Terrain tree prototype models if they lack a root CapsuleCollider
        foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain == null || terrain.terrainData == null) continue;
            var prototypes = terrain.terrainData.treePrototypes;
            if (prototypes == null) continue;

            bool anyReimported = false;
            for (int i = 0; i < prototypes.Length; i++)
            {
                GameObject prefab = prototypes[i].prefab;
                if (prefab != null && prefab.GetComponent<CapsuleCollider>() == null)
                {
                    string pPath = AssetDatabase.GetAssetPath(prefab);
                    if (!string.IsNullOrEmpty(pPath))
                    {
                        AssetDatabase.ImportAsset(pPath, ImportAssetOptions.ForceUpdate);
                        anyReimported = true;
                    }
                }
            }

            if (anyReimported)
            {
                terrain.terrainData.RefreshPrototypes();
                EditorUtility.SetDirty(terrain.terrainData);
            }
        }

        Scene activeScene = SceneManager.GetActiveScene();
        int added = SceneHitboxInitializer.EnsureHitboxesInScene(activeScene);
        if (added > 0 && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveOpenScenes();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EcoQuest] Bina ve ağaç hitbox kurulumu tamamlandı. Sahneye eklenen/güncellenen hitbox sayısı: {added}");
    }
}
