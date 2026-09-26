#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ResourceProgressionCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Resource progression check").AddComponent<ResourceProgressionCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/ResourceValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var vitals = weapon.GetComponentInParent<PlayerVitals>();
        vitals.GetComponent<PlayerController>().enabled = false; vitals.enabled = false; weapon.enabled = false;
        var camera = Camera.main;
        foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var progression = weapon.Progression;
        Check(progression != null && progression.Level == 1 && progression.Experience == 0, "New player starts with level-one YEP progression");
        var scraps = FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None);
        Check(scraps.Count(s => s.organic > 0) == 3, "MainScene supplies three modeled organic waste piles");
        int totalMetal = scraps.Sum(s => s.metal), totalPlastic = scraps.Sum(s => s.plastic), totalOrganic = scraps.Sum(s => s.organic);
        weapon.SelectTool(0); while (weapon.Mode != PlayerWeaponSystem.VacuumMode.Collect) weapon.CycleMode();
        foreach (var scrap in scraps)
        {
            Aim(camera, scrap.GetComponent<Collider>()); weapon.Use(0.5f);
            Check(scrap.Claimed, "Vacuum claims authored resource " + scrap.name);
            Check(!scrap.TryClaim(), "The same resource cannot be claimed twice");
        }
        Check(weapon.CollectedMetalScrap == totalMetal && weapon.CollectedPlasticScrap == totalPlastic && weapon.CollectedOrganicWaste == totalOrganic,
            "Vacuum preserves distinct raw metal, plastic and organic inventories");
        Check(progression.Experience == 0, "Collection alone does not grant recycling YEP");
        weapon.SelectTool(2); float electricBefore = weapon.Energy; int total = totalMetal + totalPlastic + totalOrganic;
        weapon.RecycleInventory();
        Check(weapon.CollectedMetalScrap + weapon.CollectedPlasticScrap + weapon.CollectedOrganicWaste == total - 8 && Near(weapon.Energy, electricBefore - 16),
            "Hand recycler processes at most eight units at two electricity each");
        Check(progression.Experience == 16, "Processed materials award YEP exactly once");
        while (weapon.CollectedMetalScrap + weapon.CollectedPlasticScrap + weapon.CollectedOrganicWaste > 0) weapon.RecycleInventory();
        Check(weapon.Metal == totalMetal && weapon.Plastic == totalPlastic && weapon.Compost == totalOrganic / 3 && weapon.ProcessedOrganicRemainder == totalOrganic % 3,
            "Full chain produces metal, plastic and one compost for every three organics");
        Check(progression.Experience == total * 2 && Near(weapon.Energy, electricBefore - total * 2), "Batch processing conserves resources and electricity across the complete inventory");
        float after = weapon.Energy; int xp = progression.Experience;
        weapon.RecycleInventory(); Check(weapon.Energy == after && progression.Experience == xp, "Empty recycling cannot consume electricity or farm YEP");
        Check(!weapon.TrySpendMaterials(totalMetal + 1, 1) && weapon.Metal == totalMetal && weapon.Plastic == totalPlastic,
            "Insufficient construction materials do not partially debit the inventory");
        Check(!weapon.TrySpendMaterials(-1, 0) && weapon.Metal == totalMetal, "Negative material costs are rejected");
        Check(weapon.TrySpendMaterials(2, 1, 1) && weapon.Metal == totalMetal - 2 && weapon.Plastic == totalPlastic - 1 && weapon.Compost == totalOrganic / 3 - 1,
            "Affordable material transaction debits all requested resources");
        weapon.ReturnMaterials(2, 1, 1);
        Check(weapon.Metal == totalMetal && weapon.Plastic == totalPlastic && weapon.Compost == totalOrganic / 3, "Cancelled transactions can return precisely their material cost");
        vitals.ReceiveHit(10000); Check(!weapon.TrySpendMaterials(1, 0), "Recovery blocks material transactions"); vitals.SimulateRecovery(5);
        // Test the visible organic remainder using the actual continuous recycler beam.
        var extra = EcoWorldArt.Spawn("GrassPatch", new Vector3(20, 1, 20));
        var collider = extra.AddComponent<BoxCollider>(); collider.size = Vector3.one * 0.4f;
        var residue = extra.AddComponent<RecyclableResource>(); residue.metal = residue.plastic = 0; residue.organic = 2; residue.processingSeconds = 0.2f;
        Aim(camera, collider); weapon.Use(0.25f);
        Check(residue.Claimed && weapon.ProcessedOrganicRemainder == 2, "Direct recycling retains incomplete organic compost batches");
        Check(progression.Experience == xp + 4, "Direct recycling grants the same YEP per unit as inventory processing");
        // Exact level boundaries and level cap.
        progression.Restore(EcoProgression.Threshold(2) - 1); int changes = 0; progression.LevelChanged += _ => changes++;
        progression.Award(1);
        Check(progression.Level == 2 && progression.LevelProgress == 0 && changes == 1, "Exact YEP threshold unlocks level two and emits one level change");
        progression.Restore(EcoProgression.Threshold(4) - 1);
        Check(!progression.HasLevel(4), "Level-four equipment stays locked below its threshold");
        progression.Award(1); Check(progression.HasLevel(4), "Level-four equipment unlocks at its threshold");
        int beforeInvalid = progression.Experience; progression.Award(-100);
        Check(progression.Experience == beforeInvalid, "Negative rewards cannot alter progression");
        progression.Award(int.MaxValue); Check(progression.Level == 24 && progression.Experience == EcoProgression.Threshold(24), "Large rewards safely clamp to level twenty-four");
        // Isolate save data from the user's actual slot.
        var keyField = typeof(PlayerWeaponSystem).GetField("sessionKey", BindingFlags.NonPublic | BindingFlags.Instance);
        string oldKey = (string)keyField.GetValue(weapon), testKey = "EcoQuest.ResourceCheck." + Guid.NewGuid().ToString("N");
        try
        {
            keyField.SetValue(weapon, testKey); progression.Restore(173); weapon.SaveSession(); progression.Restore(0); weapon.LoadSession();
            Check(progression.Experience == 173 && weapon.ProcessedOrganicRemainder == 2 && weapon.Compost == totalOrganic / 3,
                "Inventory save restores YEP and partial compost progress");
        }
        finally { PlayerPrefs.DeleteKey(testKey); keyField.SetValue(weapon, oldKey); PlayerPrefs.Save(); }
        File.WriteAllText("resource-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.005f;
    private static void Aim(Camera camera, Collider collider)
    {
        camera.transform.position = collider.bounds.center + new Vector3(0, 0.5f, -0.8f); camera.transform.LookAt(collider.bounds.center); Physics.SyncTransforms();
    }
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        File.WriteAllText("resource-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
