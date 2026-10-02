#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public sealed class NavigationCheck : MonoBehaviour
{
    private string report = "";
    public static void Begin()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Navigation check").AddComponent<NavigationCheck>();
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/NavigationValidation.unity"); EditorApplication.isPlaying = true;
    }
    private IEnumerator Start()
    {
        Application.logMessageReceived += OnLog;
        var floor = Box("Floor", new Vector3(80, -0.1f, 80), new Vector3(30, 0.2f, 30)); floor.AddComponent<PlantingSurface>();
        Box("U bottom", new Vector3(80, 1.5f, 80), new Vector3(6, 3, 0.4f));
        Box("U left", new Vector3(77.2f, 1.5f, 83), new Vector3(0.4f, 3, 6));
        Box("U right", new Vector3(82.8f, 1.5f, 83), new Vector3(0.4f, 3, 6));
        var target = new GameObject("Player target"); target.transform.position = new Vector3(80, 0.03f, 77); target.AddComponent<PlayerVitals>().showLegacyHUD = false;
        var hit = target.AddComponent<CapsuleCollider>(); hit.height = 1.8f; hit.center = Vector3.up * 0.9f;
        var navObject = new GameObject("Runtime navigation"); navObject.transform.position = new Vector3(80, 0, 80); var navigation = navObject.AddComponent<EcoNavigation>(); navigation.worldSize = new Vector3(30, 10, 30);
        var enemyObject = new GameObject("Maze enemy"); enemyObject.transform.position = new Vector3(80, 0.03f, 82);
        var motor = enemyObject.AddComponent<CharacterController>(); motor.radius = 0.35f; motor.height = 1.6f; motor.center = Vector3.up * 0.8f;
        float initialMinMoveDistance = motor.minMoveDistance;
        var enemy = enemyObject.AddComponent<EnemyBrain>(); enemy.species = EnemyBrain.Species.Slime; enemy.detectionRange = 30; enemy.leashRange = 40; enemy.chaseSpeed = 4; enemy.attackRange = 1.2f; enemy.patrolRadius = 0;
        report += "SETUP minMoveDistance before brain=" + initialMinMoveDistance + ", after=" + motor.minMoveDistance + "\n";
        float deadline = Time.time + 15;
        while (!navigation.Ready && Time.time < deadline) yield return null;
        Check(navigation.Ready, "Runtime navigation is built from scene colliders");
        Check(NavMesh.SamplePosition(enemy.transform.position, out _, 1, NavMesh.AllAreas), "Enemy stands on a valid navigable surface");
        yield return null;
        enemy.TakeDamage(1); float maximumZ = enemy.transform.position.z; deadline = Time.time + 10; float nextDebug = 0;
        int frames = 0, subthresholdFrames = 0;
        float totalDelta = 0, minimumDelta = float.PositiveInfinity, maximumDelta = 0, distanceMoved = 0;
        Vector3 previousPosition = enemy.transform.position;
        while (Vector3.Distance(enemy.transform.position, target.transform.position) > 1.5f && Time.time < deadline)
        {
            frames++; totalDelta += Time.deltaTime;
            minimumDelta = Mathf.Min(minimumDelta, Time.deltaTime); maximumDelta = Mathf.Max(maximumDelta, Time.deltaTime);
            if (enemy.chaseSpeed * Time.deltaTime < initialMinMoveDistance) subthresholdFrames++;
            Vector3 movement = enemy.transform.position - previousPosition; movement.y = 0;
            distanceMoved += movement.magnitude; previousPosition = enemy.transform.position;
            maximumZ = Mathf.Max(maximumZ, enemy.transform.position.z);
            if (Time.time >= nextDebug) { nextDebug = Time.time + 1; report += "TRACE " + enemy.transform.position + " " + enemy.State + " route=" + enemy.GetComponent<EcoEnemyPath>().HasRoute + " velocity=" + motor.velocity + " delta=" + Time.deltaTime + " collisions=" + motor.collisionFlags + "\n"; }
            yield return null;
        }
        report += "TIMING frames=" + frames + ", delta min/mean/max=" + minimumDelta + "/" + (totalDelta / Mathf.Max(1, frames)) + "/" + maximumDelta + ", steps below original movement threshold=" + subthresholdFrames + ", horizontal distance=" + distanceMoved + "\n";
        Check(maximumZ > 86, "Enemy leaves the open end of the U instead of pushing into the blocking wall (max Z " + maximumZ + ", final " + enemy.transform.position + ")");
        Check(Vector3.Distance(enemy.transform.position, target.transform.position) < 1.5f, "Enemy completes the route around a concave obstacle and reaches the player");
        enemy.enabled = false;
        var barrier = Box("New player structure", new Vector3(89, 1, 83), new Vector3(1, 2, 5)); barrier.AddComponent<EcoStructure>().buildingId = "battery";
        yield return new WaitForSeconds(2);
        Check(!NavMesh.SamplePosition(new Vector3(89, 0, 83), out _, 0.2f, NavMesh.AllAreas), "New structure updates navigation and blocks its occupied ground");
        barrier.SetActive(false); yield return new WaitForSeconds(2);
        Check(NavMesh.SamplePosition(new Vector3(89, 0, 83), out _, 0.2f, NavMesh.AllAreas), "Removing the structure reopens the navigable ground");
        var pond = Box("Natural water", new Vector3(72, 0.05f, 80), new Vector3(4, 0.1f, 4)); pond.AddComponent<WeaponWaterSource>().useRegionalQuality = true; EcoNavigation.MarkDirty();
        yield return new WaitForSeconds(2);
        Check(!NavMesh.SamplePosition(new Vector3(72, 0.05f, 80), out _, 0.2f, NavMesh.AllAreas), "Water sources are excluded from land routes");
        File.WriteAllText("navigation-check-success.txt", report); EditorApplication.Exit(0);
    }
    private static GameObject Box(string name, Vector3 position, Vector3 scale)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; return go; }
    private void Check(bool condition, string label) { if (!condition) throw new Exception(label); report += "PASS " + label + "\n"; }
    private void OnLog(string message, string stack, LogType type)
    { if (type != LogType.Error && type != LogType.Exception) return; File.WriteAllText("navigation-check-failed.txt", report + message + "\n" + stack); EditorApplication.Exit(1); }
}
#endif
