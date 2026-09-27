using UnityEngine;
using System.Collections.Generic;

public sealed class EcoVegetation : MonoBehaviour
{
    private static readonly HashSet<EcoVegetation> Active = new HashSet<EcoVegetation>();
    private void OnEnable() { Active.Add(this); EcoNavigation.MarkDirty(); }
    private void OnDisable() { Active.Remove(this); EcoNavigation.MarkDirty(); }
    public static int HealthyTreesIn(EcoRegion region)
    {
        if (region == null) return 0;
        int count = 0;
        foreach (var tree in Active) if (tree != null && tree.health >= 0.5f && EcoRegion.At(tree.transform.position) == region) count++;
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
    private void Update()
    {
        if (health > 0 && EcoRegion.At(transform.position) is EcoRegion region)
        {
            region.AddPollution(-0.003f * health * Time.deltaTime, -0.001f * health * Time.deltaTime, -0.002f * health * Time.deltaTime, -0.004f * health * Time.deltaTime);
            region.vegetation = Mathf.Min(1, region.vegetation + health * Time.deltaTime * 0.0001f);
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
