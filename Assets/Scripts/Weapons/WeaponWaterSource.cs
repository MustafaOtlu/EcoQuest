using UnityEngine;

// Add to a water volume with a collider to make it refillable with Vacuum mode.
public sealed class WeaponWaterSource : MonoBehaviour
{
    public bool cleanWater = true;
    public bool useRegionalQuality;
    public UnityEngine.UI.Text statusLabel;
    public bool IsClean => useRegionalQuality && EcoRegion.At(transform.position) is EcoRegion region ? region.CleanWater : cleanWater;
    private MaterialPropertyBlock tint;
    private void Update()
    {
        if (!useRegionalQuality || !TryGetComponent<Renderer>(out var surface)) return;
        if (statusLabel != null) statusLabel.text = IsClean ? "TEMİZ SU\nVakumla doldurabilirsin" : "KİRLİ SU\nArıtmadan kullanma";
        tint ??= new MaterialPropertyBlock();
        var region = EcoRegion.At(transform.position);
        var color = Color.Lerp(new Color(0.16f, 0.5f, 0.66f), new Color(0.31f, 0.35f, 0.17f), region != null ? region.waterPollution / 100 : cleanWater ? 0 : 1);
        tint.SetColor("_BaseColor", color); surface.SetPropertyBlock(tint);
    }
}
