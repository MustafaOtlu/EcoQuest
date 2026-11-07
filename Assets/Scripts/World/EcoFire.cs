using System.Collections.Generic;
using UnityEngine;

public sealed class EcoFire : MonoBehaviour
{
    [Min(0)] public float fuel = 90;
    [Range(0, 1)] public float intensity = 1;
    [Min(0.1f)] public float radius = 2;
    public ParticleSystem flames;
    public bool Burning => fuel > 0 && intensity > 0;
    private ParticleSystem embers;

    private void Start()
    {
        if (flames == null) return;
        flames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = flames.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.32f, 0.66f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.45f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color32(240, 83, 27, 232), new Color32(255, 200, 91, 249));
        var color = flames.colorOverLifetime;
        color.enabled = true;
        var heat = new Gradient();
        heat.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color32(247, 142, 80, 255), 0.58f),
            new GradientColorKey(new Color32(124, 66, 60, 255), 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.8f, 0.34f), new GradientAlphaKey(0f, 1f) });
        color.color = heat;
        var size = flames.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.28f));
        var renderer = flames.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 1.5f;
        renderer.velocityScale = 0.04f;

        embers = WeaponVisualEffects.CreateParticles("Warm embers", transform, 24);
        embers.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var embersMain = embers.main;
        embersMain.startLifetime = new ParticleSystem.MinMaxCurve(0.52f, 1.02f);
        embersMain.startSpeed = new ParticleSystem.MinMaxCurve(0.85f, 1.3f);
        embersMain.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.082f);
        embersMain.startColor = (Color)new Color32(255, 164, 73, 200);
        var shape = embers.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 19f;
        shape.radius = Mathf.Min(0.42f, radius * 0.24f);
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var emission = embers.emission;
        emission.enabled = true;
        RefreshVisual();
        if (Burning) { flames.Play(); embers.Play(); }
    }
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !Burning || !EcoRegion.Valid(seconds)) return;
        float burned = Mathf.Min(fuel, seconds * intensity); fuel -= burned;
        EcoRegion.At(transform.position)?.AddPollution(burned * 0.25f, 0, burned * 0.08f, burned * 0.16f, EcoCarbonFootprint.Source.Fire);
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
        else { var emission = flames.emission; emission.rateOverTime = 25 * intensity; }
        if (embers == null) return;
        if (!Burning) embers.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        else { var emission = embers.emission; emission.rateOverTime = 8 * intensity; }
    }
    public string Describe() => Burning ? "Yangın • Vakumla nişan al, söndürür\nYakındaki bitkilere ve yapılara zarar veriyor" : "Yangın söndü";
}
