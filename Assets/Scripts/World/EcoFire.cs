using System.Collections.Generic;
using UnityEngine;

public sealed class EcoFire : MonoBehaviour
{
    [Min(0)] public float fuel = 90;
    [Range(0, 1)] public float intensity = 1;
    [Min(0.1f)] public float radius = 2;
    public ParticleSystem flames;
    public bool Burning => fuel > 0 && intensity > 0;
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !Burning || !EcoRegion.Valid(seconds)) return;
        float burned = Mathf.Min(fuel, seconds * intensity); fuel -= burned;
        EcoRegion.At(transform.position)?.AddPollution(burned * 0.25f, 0, burned * 0.08f, burned * 0.16f);
        var structures = new HashSet<EcoStructure>(); var crops = new HashSet<PlantedSeed>();
        var trees = new HashSet<EcoVegetation>();
        var players = new HashSet<PlayerVitals>();
        foreach (var hit in Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var structure = hit.GetComponentInParent<EcoStructure>(); if (structure != null) structures.Add(structure);
            var crop = hit.GetComponentInParent<PlantedSeed>(); if (crop != null) crops.Add(crop);
            var tree = hit.GetComponentInParent<EcoVegetation>(); if (tree != null) trees.Add(tree);
            var player = hit.GetComponentInParent<PlayerVitals>(); if (player != null) players.Add(player);
        }
        foreach (var structure in structures) structure.ReceiveDamage(burned * 3);
        foreach (var crop in crops) crop.ReceiveEnvironmentalDamage(burned * 0.06f);
        foreach (var tree in trees) tree.Burn(burned * 0.025f);
        foreach (var player in players) player.ReceiveHit(burned * 6);
        RefreshVisual();
    }
    public float Extinguish(float litres)
    {
        if (!Burning || !EcoRegion.Valid(litres)) return 0;
        float used = Mathf.Min(litres, intensity * 20);
        intensity = Mathf.Max(0, intensity - used / 20); RefreshVisual(); return used;
    }
    private void RefreshVisual()
    {
        var hit = GetComponent<Collider>(); if (hit != null) hit.enabled = Burning;
        foreach (var label in GetComponentsInChildren<Canvas>()) label.enabled = Burning;
        if (flames == null) return;
        if (!Burning) flames.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else { var emission = flames.emission; emission.rateOverTime = 18 * intensity; }
    }
    public string Describe() => Burning ? "Yangın • Vakumun su moduyla söndür\nYakındaki bitkilere ve yapılara zarar veriyor" : "Yangın söndü";
}
