using UnityEngine;

// Colours a lake surface from the water pollution of the region it lies in, so cleaning
// (or polluting) a lake is visible. Visual only: it never writes to the simulation.
public sealed class EcoLakeTint : MonoBehaviour
{
    public Renderer target;
    public Color clean = new Color(0.30f, 0.74f, 0.82f, 0.62f);
    public Color dirty = new Color(0.36f, 0.40f, 0.17f, 0.82f);
    private MaterialPropertyBlock block;
    private float nextUpdate;

    private void Update()
    {
        if (Time.time < nextUpdate) return;
        nextUpdate = Time.time + 0.4f;
        if (target == null) target = GetComponent<Renderer>();
        if (target == null) return;
        var region = EcoRegion.At(transform.position);
        float pollution = region != null ? region.waterPollution / 100f : 0f;
        float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, pollution));
        block ??= new MaterialPropertyBlock();
        target.GetPropertyBlock(block);
        block.SetColor("_BaseColor", Color.Lerp(clean, dirty, t));
        target.SetPropertyBlock(block);
    }
}
