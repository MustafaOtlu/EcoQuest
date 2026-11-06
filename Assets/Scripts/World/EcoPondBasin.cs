using UnityEngine;

// Water is a horizontal cross-section of a recessed bowl, not a decal on top of the ground.
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class EcoPondBasin : MonoBehaviour
{
    public WeaponWaterSource source;
    public Transform waterSurface;
    public MeshRenderer waterRenderer;
    [Min(0.1f)] public float depth = 1.2f;
    [Min(0.1f)] public float radius = 2.3f;
    [Range(0.05f, 0.8f)] public float floorRadiusFraction = 0.28f;
    private float previousFill = -1f;
    private bool previousClean;
    private MaterialPropertyBlock waterTint;

    public float SurfaceHeight => waterSurface != null ? waterSurface.position.y : transform.position.y;
    public float SurfaceRadius => waterSurface != null ? Mathf.Abs(waterSurface.lossyScale.x) : 0f;

    public void Configure(WeaponWaterSource water, Transform surface, MeshRenderer renderer, float bowlDepth, float bowlRadius)
    {
        source = water; waterSurface = surface; waterRenderer = renderer;
        depth = Mathf.Max(0.1f, bowlDepth); radius = Mathf.Max(0.1f, bowlRadius);
        previousFill = -1f;
        RefreshVisual();
    }

    private void OnEnable() { previousFill = -1f; RefreshVisual(); }
    private void LateUpdate()
    {
        if (source != null && (Mathf.Abs(source.FillFraction - previousFill) >= 0.0001f || source.IsClean != previousClean)) RefreshVisual();
    }

    public void RefreshVisual()
    {
        if (source == null || waterSurface == null || waterRenderer == null) return;
        float fraction = Mathf.Clamp01(source.FillFraction);
        previousFill = fraction;
        float maximumHeight = Mathf.Max(0.01f, depth - 0.06f);
        float targetVolume = FrustumVolume(maximumHeight) * fraction;
        float low = 0f, high = maximumHeight;
        for (int i = 0; i < 14; i++)
        {
            float midpoint = (low + high) * 0.5f;
            if (FrustumVolume(midpoint) < targetVolume) low = midpoint; else high = midpoint;
        }
        float height = (low + high) * 0.5f;
        float sectionRadius = RadiusAt(height);
        waterSurface.localPosition = new Vector3(0f, -depth + height + 0.008f, 0f);
        waterSurface.localScale = new Vector3(sectionRadius, 1f, sectionRadius);
        waterRenderer.enabled = fraction > 0.00001f;
        previousClean = source.IsClean;
        if (waterTint == null) waterTint = new MaterialPropertyBlock();
        waterRenderer.GetPropertyBlock(waterTint);
        waterTint.SetColor("_BaseColor", source.WaterColor);
        waterRenderer.SetPropertyBlock(waterTint);
    }

    private float RadiusAt(float height)
        => Mathf.Lerp(radius * floorRadiusFraction, radius, Mathf.Clamp01(height / depth));

    private float FrustumVolume(float height)
    {
        float bottom = radius * floorRadiusFraction, top = RadiusAt(height);
        return Mathf.PI * height * (bottom * bottom + bottom * top + top * top) / 3f;
    }
}
