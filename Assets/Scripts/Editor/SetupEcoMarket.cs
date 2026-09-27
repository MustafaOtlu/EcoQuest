using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupEcoMarket
{
    [MenuItem("EcoQuest/Bind market to field station")]
    public static void Build()
    {
        const string path = "Assets/Resources/EcoArt/FieldStation.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try { Bind(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Debug.Log("ECO_MARKET_READY: existing field station counter bound to trading and upgrades.");
    }
    public static void Bind(GameObject root)
    {
        var counter = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Workbench");
        if (counter == null) throw new InvalidOperationException("Field station workbench is missing.");
        if (counter.GetComponent<EcoMarket>() == null) counter.gameObject.AddComponent<EcoMarket>();
        var box = root.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "OpenBox");
        if (box != null && box.parent != counter) box.SetParent(counter, true);
        foreach (var label in root.GetComponentsInChildren<Text>())
            if (label.text.StartsWith("GERİ DÖNÜŞÜM")) label.text = "MARKET\n[E] Alışveriş / geliştirme";
        for (int tier = 1; tier <= 3; tier++)
        {
            if (root.transform.Find("Heavy salvage " + tier) != null) continue;
            var pile = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/EcoArt/MetalScrap.prefab"));
            pile.name = "Heavy salvage " + tier; pile.transform.SetParent(root.transform, false);
            pile.transform.localPosition = new Vector3(7.5f + tier * 0.9f, 0.02f, 2.5f); pile.transform.localScale = Vector3.one * (1.6f + tier * 0.6f);
            var waste = pile.AddComponent<RecyclableResource>(); waste.metal = tier * 4; waste.plastic = tier * 2; waste.requiredRecyclerRank = tier; waste.processingSeconds = tier * 2;
        }
    }
}
