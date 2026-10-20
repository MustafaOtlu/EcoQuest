using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneHitboxInitializer
{
    private static readonly string[] BuildingAndTreeKeywords =
    {
        "modern+house",
        "house",
        "evler",
        "apartman",
        "apartment",
        "klinik",
        "clinic",
        "market",
        "su arıtma",
        "su aritma",
        "su deposu",
        "geri dönüşüm",
        "geri donusum",
        "batarya",
        "hidroelektrik",
        "kitap evi",
        "bookstore",
        "ruzgar",
        "rüzgar",
        "turbine",
        "power+pole",
        "street+lamp",
        "traffic+light",
        "tree",
        "ağaç",
        "agac",
        "oak",
        "spruce",
        "pine",
        "palm"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeActiveScene()
    {
        EnsureHitboxesInScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureHitboxesInScene(scene);
    }

    public static int EnsureHitboxesInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return 0;

        int addedCount = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root == null) continue;
            addedCount += EnsureHierarchyHitboxes(root.transform, IsBuildingOrTreeContainer(root.name));
        }

        foreach (var terrain in UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
        {
            if (terrain == null || terrain.gameObject.scene != scene) continue;
            addedCount += EnsureTerrainTreeHitboxes(terrain);
        }

        if (addedCount > 0)
        {
            Physics.SyncTransforms();
            EcoNavigation.MarkDirty();
        }

        return addedCount;
    }

    private static int EnsureHierarchyHitboxes(Transform current, bool parentMatched)
    {
        if (current == null) return 0;

        // Skip characters, players, enemies, cameras, UI, and weapons
        if (ShouldSkipSubtree(current)) return 0;

        bool isMatch = parentMatched || MatchesBuildingOrTree(current.name) || current.GetComponent<EcoVegetation>() != null || current.GetComponent<EcoStructure>() != null;
        bool isTree = IsTreeName(current.name) || current.GetComponent<EcoVegetation>() != null;

        int added = 0;

        if (isMatch && current.childCount > 0 && HasAnyMeshOrRenderer(current) && !HasActiveColliderInHierarchy(current))
        {
            if (isTree)
            {
                if (AddFittedTreeCapsuleCollider(current.gameObject))
                    added++;
            }
            else
            {
                added += AddCollidersToBuildingOrProp(current.gameObject);
            }
        }
        else if (isMatch && current.childCount == 0 && HasAnyMeshOrRenderer(current) && current.GetComponent<Collider>() == null && current.GetComponentInParent<Collider>() == null)
        {
            if (isTree)
            {
                if (AddFittedTreeCapsuleCollider(current.gameObject))
                    added++;
            }
            else
            {
                added += AddCollidersToBuildingOrProp(current.gameObject);
            }
        }

        for (int i = 0; i < current.childCount; i++)
        {
            Transform child = current.GetChild(i);
            if (child.name == "TerrainTreeHitboxes") continue;
            added += EnsureHierarchyHitboxes(child, isMatch);
        }

        return added;
    }

    private static bool ShouldSkipSubtree(Transform t)
    {
        string n = t.name;
        if (n.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Cam", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Vakum", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Enemy", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Canvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Sign", StringComparison.OrdinalIgnoreCase) >= 0 ||
            n.IndexOf("Label", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (t.GetComponent<CharacterController>() != null ||
            t.GetComponent<PlayerController>() != null ||
            t.GetComponent<EnemyBrain>() != null ||
            t.GetComponent<Camera>() != null ||
            t.GetComponent<Canvas>() != null ||
            t.GetComponent<ParticleSystem>() != null)
        {
            return true;
        }

        return false;
    }

    private static bool IsBuildingOrTreeContainer(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("Evler", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Binalar", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Ağaç", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Agac", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Trees", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Elektrik Diregi", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Lamba", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static bool MatchesBuildingOrTree(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        for (int i = 0; i < BuildingAndTreeKeywords.Length; i++)
        {
            if (name.IndexOf(BuildingAndTreeKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    public static bool IsTreeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("tree", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("ağaç", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("agac", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("oak", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("spruce", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("pine", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("palm", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasAnyMeshOrRenderer(Transform t)
    {
        return t.GetComponentInChildren<MeshFilter>(true) != null ||
               t.GetComponentInChildren<MeshRenderer>(true) != null;
    }

    private static bool HasActiveColliderInHierarchy(Transform t)
    {
        var colliders = t.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
                return true;
        }
        return false;
    }

    public static int AddCollidersToBuildingOrProp(GameObject target)
    {
        int added = 0;
        var meshFilters = target.GetComponentsInChildren<MeshFilter>(false);

        for (int i = 0; i < meshFilters.Length; i++)
        {
            MeshFilter mf = meshFilters[i];
            if (mf == null || mf.sharedMesh == null) continue;
            if (ShouldSkipSubtree(mf.transform)) continue;

            Collider existing = mf.GetComponent<Collider>();
            if (existing != null && existing.enabled) continue;

            if (mf.sharedMesh.isReadable)
            {
                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                added++;
            }
        }

        if (!HasActiveColliderInHierarchy(target.transform))
        {
            if (AddFittedBoxCollider(target))
                added++;
        }

        return added;
    }

    public static bool AddFittedBoxCollider(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(false);
        if (renderers == null || renderers.Length == 0) return false;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer) continue;
            worldBounds.Encapsulate(renderers[i].bounds);
        }

        if (worldBounds.size.sqrMagnitude < 0.0001f) return false;

        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null) box = target.AddComponent<BoxCollider>();

        Vector3 localCenter = target.transform.InverseTransformPoint(worldBounds.center);
        Vector3 lossy = target.transform.lossyScale;
        Vector3 localSize = new Vector3(
            lossy.x != 0f ? Mathf.Abs(worldBounds.size.x / lossy.x) : worldBounds.size.x,
            lossy.y != 0f ? Mathf.Abs(worldBounds.size.y / lossy.y) : worldBounds.size.y,
            lossy.z != 0f ? Mathf.Abs(worldBounds.size.z / lossy.z) : worldBounds.size.z
        );

        box.center = localCenter;
        box.size = localSize;
        return true;
    }

    public static bool AddFittedTreeCapsuleCollider(GameObject target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>(false);
        Bounds worldBounds;
        if (renderers != null && renderers.Length > 0)
        {
            worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] is ParticleSystemRenderer) continue;
                worldBounds.Encapsulate(renderers[i].bounds);
            }
        }
        else
        {
            worldBounds = new Bounds(target.transform.position + Vector3.up * 1.75f, new Vector3(1.5f, 3.5f, 1.5f));
        }

        CapsuleCollider capsule = target.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = target.AddComponent<CapsuleCollider>();

        Vector3 lossy = target.transform.lossyScale;
        float scaleY = Mathf.Max(0.01f, Mathf.Abs(lossy.y));
        float scaleXZ = Mathf.Max(0.01f, (Mathf.Abs(lossy.x) + Mathf.Abs(lossy.z)) * 0.5f);

        float worldHeight = Mathf.Max(2f, worldBounds.size.y);
        float worldTrunkRadius = Mathf.Clamp(Mathf.Min(worldBounds.size.x, worldBounds.size.z) * 0.1f, 0.22f, 0.55f);

        capsule.direction = 1;
        capsule.height = worldHeight / scaleY;
        capsule.radius = worldTrunkRadius / scaleXZ;
        capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
        return true;
    }

    public static int EnsureTerrainTreeHitboxes(Terrain terrain)
    {
        if (terrain == null || terrain.terrainData == null) return 0;

        TerrainData data = terrain.terrainData;
        TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
        if (terrainCollider != null && !terrainCollider.enabled)
        {
            terrainCollider.enabled = true;
        }

        TreeInstance[] instances = data.treeInstances;
        if (instances == null || instances.Length == 0) return 0;

        Transform existingContainer = terrain.transform.Find("TerrainTreeHitboxes");
        if (existingContainer != null && existingContainer.childCount >= instances.Length)
        {
            return 0;
        }

        if (existingContainer != null)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(existingContainer.gameObject);
            else
                UnityEngine.Object.DestroyImmediate(existingContainer.gameObject);
        }

        GameObject container = new GameObject("TerrainTreeHitboxes");
        container.transform.SetParent(terrain.transform, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        container.transform.localScale = Vector3.one;
        container.isStatic = true;

        Vector3 terrainSize = data.size;
        TreePrototype[] prototypes = data.treePrototypes;
        float[] protoHeights = new float[prototypes != null ? prototypes.Length : 0];
        float[] protoRadii = new float[prototypes != null ? prototypes.Length : 0];

        if (prototypes != null)
        {
            for (int p = 0; p < prototypes.Length; p++)
            {
                float h = 3.8f;
                float r = 0.32f;
                GameObject prefab = prototypes[p].prefab;
                if (prefab != null)
                {
                    CapsuleCollider cap = prefab.GetComponent<CapsuleCollider>();
                    if (cap != null)
                    {
                        h = Mathf.Max(2f, cap.height);
                        r = Mathf.Clamp(cap.radius, 0.18f, 0.6f);
                    }
                    else
                    {
                        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                        if (renderers != null && renderers.Length > 0)
                        {
                            Bounds b = renderers[0].bounds;
                            for (int k = 1; k < renderers.Length; k++)
                                b.Encapsulate(renderers[k].bounds);
                            if (b.size.y > 0.5f)
                            {
                                h = Mathf.Max(2f, b.size.y * 0.85f);
                                r = Mathf.Clamp(Mathf.Min(b.size.x, b.size.z) * 0.09f, 0.22f, 0.5f);
                            }
                        }
                    }
                }
                protoHeights[p] = h;
                protoRadii[p] = r;
            }
        }

        int created = 0;
        for (int i = 0; i < instances.Length; i++)
        {
            TreeInstance ti = instances[i];
            Vector3 localPos = Vector3.Scale(ti.position, terrainSize);

            int pIdx = ti.prototypeIndex;
            float baseHeight = (pIdx >= 0 && pIdx < protoHeights.Length) ? protoHeights[pIdx] : 3.8f;
            float baseRadius = (pIdx >= 0 && pIdx < protoRadii.Length) ? protoRadii[pIdx] : 0.32f;

            float height = Mathf.Max(1.5f, baseHeight * ti.heightScale);
            float radius = Mathf.Max(0.15f, baseRadius * ti.widthScale);

            GameObject treeColObj = new GameObject("TreeHitbox_" + i);
            treeColObj.transform.SetParent(container.transform, false);
            treeColObj.transform.localPosition = localPos;
            treeColObj.isStatic = true;

            CapsuleCollider capsule = treeColObj.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.height = height;
            capsule.radius = radius;
            capsule.center = new Vector3(0f, height * 0.5f, 0f);
            created++;
        }

        return created;
    }
}
