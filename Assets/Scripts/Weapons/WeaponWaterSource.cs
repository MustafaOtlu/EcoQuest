using UnityEngine;

// Add to a water volume with a collider to make it refillable with Vacuum mode.
public sealed class WeaponWaterSource : MonoBehaviour
{
    public bool cleanWater = true;
    public bool useRegionalQuality;
    [Tooltip("Radius of the bank for bodies of water (0 = small source such as a barrel); the player stops at it.")]
    public float shoreRadius;
    [SerializeField, Range(0f, 100f)] private float localPollution;
    public float LocalPollution => localPollution;
    public UnityEngine.UI.Text statusLabel;
    public bool IsClean => useRegionalQuality
        ? EcoRegion.At(transform.position) is EcoRegion region ? region.CleanWater : cleanWater && localPollution < 35f
        : cleanWater;
    public void Pollute(float amount)
    {
        if (!useRegionalQuality || !EcoRegion.Valid(amount)) return;
        amount = Mathf.Min(amount, 100f);
        var region = EcoRegion.At(transform.position);
        if (region != null) region.AddPollution(0f, amount, 0f, 0f);
        else localPollution = Mathf.Clamp(localPollution + amount, 0f, 100f);
    }
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
