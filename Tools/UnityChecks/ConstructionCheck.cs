#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class ConstructionCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Construction check").AddComponent<ConstructionCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/ConstructionValidation.unity");
        EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        FindFirstObjectByType<EcoPowerGrid>().enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var builder = weapon.Builder;
        var player = weapon.GetComponentInParent<PlayerController>(); player.enabled = false;
        var controller = player.GetComponent<CharacterController>(); controller.enabled = false;
        player.transform.position = new Vector3(0, 0.03f, -4); controller.enabled = true; Physics.SyncTransforms();
        weapon.ReturnMaterials(200, 200);
        int actualNodes = EcoPowerNode.Active.Count, actualStructures = EcoStructure.Active.Count;
        Check(actualStructures == 4, "All four starter power structures expose integrity and building identity");
        builder.SetBuildMode(true);
        Check(builder.IsBuilding && weapon.holder.EquippedWeapon == null, "Build mode safely stows the selected weapon");
        Check(EcoPowerNode.Active.Count == actualNodes && EcoStructure.Active.Count == actualStructures, "Preview is never registered as a producing or countable structure");
        var preview = GameObject.Find("Build preview");
        Check(preview != null && preview.GetComponentsInChildren<Collider>().All(c => !c.enabled), "Preview cannot obstruct raycasts or collide with the player");
        float energy = weapon.Energy; weapon.Use(1); Check(weapon.Energy == energy, "Build mode blocks firing the selected weapon");
        Check(!builder.TryPlace(1, new Vector3(0, 0.03f, -7), Quaternion.identity) && weapon.Metal == 200 && weapon.Plastic == 200,
            "YEP level lock rejects a turbine without spending materials");
        Check(!builder.TryPlace(0, new Vector3(0, 1, -7), Quaternion.identity), "Floating foundations are rejected");
        Check(!builder.TryPlace(0, new Vector3(0, 0.03f, -30), Quaternion.identity), "Placement outside construction reach is rejected");
        var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.transform.position = new Vector3(0, 0.5f, -7); Physics.SyncTransforms();
        Check(!builder.TryPlace(0, new Vector3(0, 0.03f, -7), Quaternion.identity), "Occupied footprint rejects placement");
        DestroyImmediate(obstacle); Physics.SyncTransforms();
        Check(builder.TryPlace(0, new Vector3(0, 0.03f, -7), Quaternion.Euler(0, 90, 0)), "Valid rotated solar panel can be constructed");
        var solar = EcoStructure.Active.First(s => s.playerBuilt && s.buildingId == "solar");
        Check(weapon.Metal == 188 && weapon.Plastic == 194 && weapon.Progression.Experience == 12, "Placement consumes exact material cost and awards construction YEP once");
        Check(Quaternion.Angle(solar.transform.rotation, Quaternion.Euler(0, 90, 0)) < 0.01f, "Placed structure preserves the selected rotation");
        Check(!builder.TryPlace(0, new Vector3(0, 0.03f, -10), Quaternion.identity) && weapon.Metal == 188, "Per-level solar limit prevents extra construction without charging materials");
        Check(builder.TryPlace(2, new Vector3(0, 0.03f, -9), Quaternion.identity), "A battery can be placed separately from the panel");
        var battery = EcoStructure.Active.First(s => s.playerBuilt && s.buildingId == "battery");
        Check(battery.GetComponent<EcoPowerNode>().storedEnergy == 0, "Player-built batteries begin empty");
        int beforeCable = weapon.Metal; int plasticBeforeCable = weapon.Plastic;
        Check(builder.TryConnect(solar.GetComponent<EcoPowerNode>(), battery.GetComponent<EcoPowerNode>()), "Player can connect two constructed power structures");
        var cable = EcoPowerCable.Active.First(c => c.playerBuilt);
        int lengthCost = Mathf.CeilToInt(Vector3.Distance(cable.a.SocketPosition, cable.b.SocketPosition));
        Check(weapon.Metal == beforeCable - lengthCost && weapon.Plastic == plasticBeforeCable - lengthCost, "Cable consumes one metal and plastic per rounded-up metre");
        int afterCable = weapon.Metal;
        Check(!builder.TryConnect(cable.b, cable.a) && weapon.Metal == afterCable, "Duplicate reversed cable is rejected without a second charge");
        solar.ReceiveDamage(50); int beforeRepair = weapon.Metal, plasticBeforeRepair = weapon.Plastic;
        Check(Near(solar.GetComponent<EcoPowerNode>().condition, 0.5f), "Structure damage lowers the connected power node condition");
        Check(solar.Repair(weapon) && solar.integrity == 100 && weapon.Metal == beforeRepair - 6 && weapon.Plastic == plasticBeforeRepair - 3,
            "Repair spends materials in proportion to missing integrity");
        int repairedMetal = weapon.Metal; Check(!solar.Repair(weapon) && weapon.Metal == repairedMetal, "A full-health structure cannot charge for unnecessary repair");
        var starter = EcoStructure.Active.First(s => !s.playerBuilt);
        Check(!starter.Dismantle(weapon), "Starter equipment cannot be sold repeatedly for free materials");
        int beforeRefund = weapon.Metal;
        Check(solar.Dismantle(weapon) && weapon.Metal == beforeRefund + 6, "Dismantling returns half the original solar material cost");
        Check(!solar.Dismantle(weapon) && weapon.Metal == beforeRefund + 6, "Repeated dismantle in the same frame cannot duplicate refunds");
        yield return null; yield return null;
        Check(cable == null, "Dismantling a structure removes attached electrical cables");
        builder.SetBuildMode(false);
        Check(!builder.IsBuilding && weapon.holder.EquippedWeapon != null, "Leaving build mode restores the selected weapon");
        File.WriteAllText("construction-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;
        File.WriteAllText("construction-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1);
    }
}
#endif
