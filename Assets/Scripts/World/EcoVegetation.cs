using UnityEngine;
using System.Collections.Generic;

public sealed class EcoVegetation : MonoBehaviour
{
    private static readonly HashSet<EcoVegetation> Active = new HashSet<EcoVegetation>();
    [System.NonSerialized] public bool planted;
    private void OnEnable() { Active.Add(this); EcoNavigation.MarkDirty(); planted |= GetComponent<PlantedSeed>() != null; }
    private void OnDisable() { Active.Remove(this); EcoNavigation.MarkDirty(); }
    public static int HealthyTreesIn(EcoRegion region)
    {
        if (region == null) return 0;
        int count = 0;
        foreach (var tree in Active) if (tree != null && tree.health >= 0.5f && EcoRegion.At(tree.transform.position) == region) count++;
        return count;
    }
    /// <summary>Grown trees the player planted that are still healthy (GDD v5 tree_planting goal).</summary>
    public static int PlantedHealthy()
    {
        int count = 0;
        foreach (var tree in Active) if (tree != null && tree.planted && tree.health >= 0.5f && tree.GetComponent<PlantedSeed>() == null) count++;
        return count;
    }
    public static Transform FirstHealthyTree(EcoRegion region)
    {
        foreach (var tree in Active) if (tree != null && tree.health >= 0.5f && EcoRegion.At(tree.transform.position) == region) return tree.transform;
        return null;
    }
    [Range(0, 1)] public float health = 1;
    public Renderer[] foliage;
    private MaterialPropertyBlock tint;
    // Trees stand still: the owning region is looked up every couple of seconds, not every frame for every tree.
    private EcoRegion region;
    private float regionAt = -10f;
    private void Update()
    {
        if (Time.time - regionAt > 2f || Time.time < regionAt) { regionAt = Time.time; region = EcoRegion.At(transform.position); }
        if (health > 0 && region != null && region.isActiveAndEnabled)
        {
            // GDD v5 §6: in a rolled run the valley's own (wild) trees clean far less and only hold cover at the starting
            // level (EcoRegion.wildCover), so an idle player cannot pass the carbon or cover goals. Trees the player
            // planted work at full strength. In the Training Valley the factor is 1 and the cap stays at 1.
            float rate = health * Time.deltaTime, factor = planted ? 1f : EcoRun.RecoveryFactor, sink = rate * factor * factor;
            region.AddPollution(-0.003f * sink, -0.001f * sink, -0.002f * sink, -0.004f * sink, EcoCarbonFootprint.Source.Trees);
            float cap = planted ? 1f : region.wildCover;
            if (region.vegetation < cap) region.vegetation = Mathf.Min(cap, region.vegetation + rate * factor * 0.0001f);
        }
    }
    public void Burn(float damage)
    {
        if (!EcoRegion.Valid(damage)) return;
        health = Mathf.Max(0, health - damage); tint ??= new MaterialPropertyBlock();
        var color = Color.Lerp(new Color(0.12f, 0.09f, 0.065f), Color.white, health);
        tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color);
        if (foliage != null) foreach (var renderer in foliage) if (renderer != null) renderer.SetPropertyBlock(tint);
        if (health <= 0 && GetComponent<RecyclableResource>() == null)
        {
            var waste = gameObject.AddComponent<RecyclableResource>(); waste.metal = waste.plastic = 0; waste.organic = 6; waste.processingSeconds = 3;
        }
    }
}
