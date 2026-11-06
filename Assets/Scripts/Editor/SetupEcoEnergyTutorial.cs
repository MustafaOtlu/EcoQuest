using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Retain the starter simulation/ecology root, but let the player construct its electrical equipment.
public static class SetupEcoEnergyTutorial
{
    [MenuItem("EcoQuest/World/Set up energy tutorial site")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Set up the energy tutorial outside Play Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var groups = Object.FindObjectsByType<EcoPowerNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(n => n.gameObject.scene == scene && n.transform.parent != null
                && (!n.TryGetComponent<EcoStructure>(out var structure) || !structure.playerBuilt))
            .GroupBy(n => n.transform.parent)
            .Where(g => g.Any(n => n.kind == EcoPowerNode.Kind.Solar) && g.Any(n => n.kind == EcoPowerNode.Kind.Battery)
                && g.Any(n => n.kind == EcoPowerNode.Kind.Charger)
                && (g.Key.GetComponent<EcoPowerGrid>() != null || g.Key.GetComponent<EcoWorldClock>() != null))
            .OrderByDescending(g => g.Key.name.StartsWith("PowerStarter") || g.Key.name.StartsWith("Power Starter")).ToArray();
        if (groups.Length == 0) throw new InvalidOperationException("The original solar/battery/charger starter group was not found.");
        var group = groups[0];
        var original = group.Where(n => n.kind == EcoPowerNode.Kind.Solar || n.kind == EcoPowerNode.Kind.Wind
            || n.kind == EcoPowerNode.Kind.Battery || n.kind == EcoPowerNode.Kind.Charger).ToArray();
        var nodes = new HashSet<EcoPowerNode>(original);
        var objects = new List<Transform>(original.Select(n => n.transform));
        foreach (var wire in group.Key.GetComponentsInChildren<EcoPowerCable>(true))
            if (!wire.playerBuilt && nodes.Contains(wire.a) && nodes.Contains(wire.b)) objects.Add(wire.transform);
        foreach (var label in group.Key.GetComponentsInChildren<WorldSpaceLabel>(true))
        {
            var text = label.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (text != null && text.text.StartsWith("ELEKTRİK İSTASYONU")) objects.Add(label.transform);
        }
        var site = group.Key.GetComponent<EcoEnergyTutorialSite>();
        if (site == null) site = group.Key.gameObject.AddComponent<EcoEnergyTutorialSite>();
        site.legacyObjects = objects.Distinct().ToArray();
        foreach (var item in site.legacyObjects) { item.gameObject.SetActive(false); Record(item.gameObject); }
        Physics.SyncTransforms();
        if (!site.configured)
        {
            var solar = original.First(n => n.kind == EcoPowerNode.Kind.Solar);
            if (!TryLayout(solar.transform.position, out site.solarPosition, out site.batteryPosition, out site.chargerPosition))
                throw new InvalidOperationException("No clear compact tutorial layout near the original starter was found.");
            site.configured = true;
        }
        Record(site);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        EcoNavigation.MarkDirty();
        Debug.Log("ECO_ENERGY_TUTORIAL_SITE_READY: original equipment retained inactive; build targets "
            + site.SolarPosition + " → " + site.BatteryPosition + " → " + site.ChargerPosition + ". Save the scene to persist.");
    }

    private static bool TryLayout(Vector3 origin, out Vector3 solar, out Vector3 battery, out Vector3 charger)
    {
        origin.x = Mathf.Floor(origin.x / 2f) * 2f;
        origin.z = Mathf.Floor(origin.z / 2f) * 2f;
        Vector3[] turns = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
        for (int radius = 0; radius <= 6; radius++)
            for (int x = -radius; x <= radius; x++) for (int z = -radius; z <= radius; z++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) != radius) continue;
                foreach (var along in turns)
                {
                    var across = Vector3.Cross(along, Vector3.up);
                    var a = origin + new Vector3(x * 2f, 0, z * 2f);
                    var b = a + along * 4f;
                    var c = b + across * 4f;
                    if (BuildPoint(a, out solar) && BuildPoint(b, out battery) && BuildPoint(c, out charger)) return true;
                }
            }
        solar = battery = charger = Vector3.zero; return false;
    }

    private static bool BuildPoint(Vector3 point, out Vector3 grounded)
    {
        grounded = point;
        // The closest solid surface must be soil: a road or roof above soil cannot be used as a build plot.
        var hits = Physics.RaycastAll(point + Vector3.up * 20f, Vector3.down, 50f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        if (hits.Length == 0 || !EcoGround.IsSoil(hits[0].collider) || hits[0].normal.y < 0.9f) return false;
        grounded = hits[0].point + Vector3.up * 0.03f;
        foreach (var collider in Physics.OverlapBox(grounded + Vector3.up * 1.5f, new Vector3(1.05f, 1.45f, 1.05f), Quaternion.identity, ~(1 << 2), QueryTriggerInteraction.Ignore))
            if (!EcoGround.IsSoil(collider)) return false;
        foreach (float x in new[] { -0.9f, 0.9f }) foreach (float z in new[] { -0.9f, 0.9f })
            if (!Physics.Raycast(grounded + new Vector3(x, 0.4f, z), Vector3.down, out var hit, 0.8f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                || !EcoGround.IsSoil(hit.collider) || hit.normal.y < 0.9f || Mathf.Abs(hit.point.y - grounded.y + 0.03f) > 0.12f) return false;
        return true;
    }

    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
}
