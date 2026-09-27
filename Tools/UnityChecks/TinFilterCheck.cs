#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TinFilterCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        new GameObject("Tin filter check").AddComponent<TinFilterCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/TinFilterValidation.unity"); EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog; yield return null; yield return null;
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) enemy.enabled = false;
        var weapon = FindFirstObjectByType<PlayerWeaponSystem>(); var player = weapon.GetComponentInParent<PlayerController>(); player.enabled = false;
        var camera = Camera.main; foreach (var component in camera.GetComponents<Behaviour>()) if (component is not Camera) component.enabled = false;
        var shop = FindFirstObjectByType<EcoMarket>(); Move(player, shop.transform.position + Vector3.back);
        weapon.ReturnMaterials(100, 100); weapon.Progression.Restore(EcoProgression.Threshold(2));
        Check(shop.Trade(weapon, EcoMarket.Offer.SmallFilter) && weapon.SmallFilters == 1, "Market supplies a small filter");
        var tin = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).First(e => e.species == EnemyBrain.Species.Tin);
        var motor = tin.GetComponent<CharacterController>(); motor.enabled = false; tin.transform.position = new Vector3(0, 0.1f, 22); motor.enabled = true;
        Move(player, new Vector3(0, 0.1f, 20)); camera.transform.position = player.transform.position + Vector3.up; camera.transform.LookAt(motor.bounds.center); Physics.SyncTransforms();
        tin.enabled = true;
        Check(!tin.InstallFilter(weapon) && weapon.SmallFilters == 1, "An active Tin cannot consume a filter before being stunned");
        float health = tin.Health; weapon.HitWithMetal(tin);
        Check(tin.IsStunned && tin.StunRemaining > 4.9f && Near(tin.Health, health - 45), "Metal hit damages Tin and opens a five-second filter opportunity");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 1, 21); wall.transform.localScale = new Vector3(3, 2, 0.2f); Physics.SyncTransforms();
        weapon.Interact(); Check(!tin.FilterInstalled && weapon.SmallFilters == 1, "Actual E interaction cannot install through a wall"); Destroy(wall); yield return null;
        Move(player, new Vector3(0, 0.1f, 15)); Check(!tin.InstallFilter(weapon) && weapon.SmallFilters == 1, "Remote installation does not consume the filter");
        Move(player, new Vector3(0, 0.1f, 20)); camera.transform.position = player.transform.position + Vector3.up; camera.transform.LookAt(motor.bounds.center); Physics.SyncTransforms();
        Check(weapon.ContextHint().Contains("Sersemleme"), "HUD exposes the remaining stun and filter action");
        int projectiles = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length; float playerEnergy = player.GetComponent<PlayerVitals>().Energy;
        yield return new WaitForSeconds(0.3f);
        Check(FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length == projectiles && Near(playerEnergy, player.GetComponent<PlayerVitals>().Energy), "Stunned Tin cannot continue its attack windup");
        int xp = weapon.Progression.Experience;
        camera.transform.LookAt(motor.bounds.center); weapon.Interact();
        Check(tin.FilterInstalled && tin.Health == 0 && tin.State == EnemyBrain.Behaviour.Dead && weapon.SmallFilters == 0 && weapon.Progression.Experience == xp + 12,
            "E consumes one small filter, neutralizes Tin, and awards YEP once");
        var drops = FindObjectsByType<EnemyLootDrop>(FindObjectsSortMode.None);
        Check(drops.Length == 2 && drops.Sum(d => d.GetComponent<RecyclableResource>().metal) == 6, "Filtered Tin leaves the same recoverable six-metal loot");
        Check(!tin.InstallFilter(weapon) && weapon.Progression.Experience == xp + 12, "Repeated filtering cannot duplicate rewards");
        tin.TakeDamage(1000); Check(FindObjectsByType<EnemyLootDrop>(FindObjectsSortMode.None).Length == 2, "Further damage after filtering cannot duplicate loot");
        Check(tin.GetComponent<EnemyDefeatMotion>() != null && !motor.enabled, "Defeat starts visible motion with collision disabled");
        Vector3 scale = tin.transform.localScale; yield return new WaitForSeconds(0.25f);
        Check(tin != null && tin.transform.localScale.magnitude < scale.magnitude, "Defeated model visibly shrinks instead of instantly disappearing");
        yield return new WaitForSeconds(0.5f); Check(tin == null, "Defeat cleanup removes the spent actor");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TinMonster.prefab");
        var next = Instantiate(prefab, new Vector3(5, 0.1f, 22), Quaternion.identity).GetComponent<EnemyBrain>();
        next.StunFromMetal(0.1f); yield return new WaitForSeconds(0.12f); next.TakeDamage(1); yield return null;
        Check(!next.IsStunned && next.animator.speed == 1 && next.State != EnemyBrain.Behaviour.Stunned, "Stun expiry restores animation even if a hit arrives during the transition");
        Move(player, new Vector3(5, 0.1f, 20)); next.StunFromMetal();
        Check(!next.InstallFilter(weapon) && !next.FilterInstalled, "No filter means the stunned enemy remains active");
        var flame = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None).First(e => e.species == EnemyBrain.Species.Flame); flame.StunFromMetal();
        Check(!flame.IsStunned, "Metal filter stun is specific to Tin");
        File.WriteAllText("tin-filter-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static void Move(PlayerController player, Vector3 position)
    { var cc = player.GetComponent<CharacterController>(); cc.enabled = false; player.transform.position = position; cc.enabled = true; Physics.SyncTransforms(); }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.003f;
    private void Check(bool condition, string message) { if (!condition) throw new Exception(message); report += "PASS " + message + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("tin-filter-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
