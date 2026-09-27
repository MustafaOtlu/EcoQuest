using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshSurface))]
public sealed class EcoNavigation : MonoBehaviour
{
    public static EcoNavigation Current { get; private set; }
    public Vector3 worldSize = new Vector3(128, 18, 128);
    public bool Ready => surface != null && surface.navMeshData != null && !initializing;
    private NavMeshSurface surface;
    private AsyncOperation update;
    private bool dirty = true, initializing = true;
    private float nextBuild;
    private void Awake() { Current = this; surface = GetComponent<NavMeshSurface>(); }
    private IEnumerator Start()
    {
        yield return null;
        surface.collectObjects = CollectObjects.Volume; surface.center = Vector3.up * 3; surface.size = worldSize;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.layerMask = ~(1 << 2 | 1 << 5);
        surface.overrideVoxelSize = true; surface.voxelSize = 0.12f;
        Prepare(); surface.BuildNavMesh(); initializing = false; dirty = false; nextBuild = Time.time + 1;
    }
    public static void MarkDirty() { if (Current != null) Current.dirty = true; }
    public static void Ignore(GameObject actor)
    {
        var modifier = actor.GetComponent<NavMeshModifier>(); if (modifier == null) modifier = actor.AddComponent<NavMeshModifier>();
        modifier.ignoreFromBuild = true; modifier.applyToChildren = true;
    }
    private void Prepare()
    {
        foreach (var player in FindObjectsByType<PlayerVitals>(FindObjectsSortMode.None)) Ignore(player.gameObject);
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) Ignore(enemy.gameObject);
        foreach (var crop in FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None)) Ignore(crop.gameObject);
        foreach (var source in FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None))
        {
            var modifier = source.GetComponent<NavMeshModifier>(); if (modifier == null) modifier = source.gameObject.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true; modifier.area = 1;
        }
        Physics.SyncTransforms();
    }
    private void Update()
    {
        if (initializing || !dirty || Time.time < nextBuild || (update != null && !update.isDone)) return;
        Prepare(); update = surface.UpdateNavMesh(surface.navMeshData); dirty = false; nextBuild = Time.time + 1;
    }
    private void OnDestroy() { if (Current == this) Current = null; }
}
