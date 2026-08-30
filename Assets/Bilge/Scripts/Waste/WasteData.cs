using UnityEngine;

/// <summary>
/// Bir çöp/atık tipinin tüm özelliklerini tanımlayan ScriptableObject.
/// Unity Editor'da Assets/Bilge/ScriptableObjects/Waste/ altında
/// her çöp tipi için bir asset oluşturulur.
/// 
/// Kullanım: Create > EcoQuest > Bilge > Waste Data
/// </summary>
[CreateAssetMenu(fileName = "NewWasteData", menuName = "EcoQuest/Bilge/Waste Data")]
public class WasteData : ScriptableObject
{
    [Header("Temel Bilgiler")]
    public WasteType wasteType;
    public string displayName;
    [TextArea] public string description;
    public Sprite sprite;

    [Header("Geri Donusum Verimi")]
    [Tooltip("Bu çöp geri dönüştürüldüğünde elde edilecek Metal miktarı")]
    [Min(0)] public int metalYield = 1;
    [Tooltip("Bu çöp geri dönüştürüldüğünde elde edilecek Plastik miktarı")]
    [Min(0)] public int plasticYield = 1;

    [Header("Toplama")]
    [Tooltip("Vakum silahı ile toplama süresi (saniye)")]
    [Range(0.1f, 5f)] public float collectionTime = 1f;
    [Tooltip("Çöp toplandığında hava kalitesine etkisi (pozitif = iyileşme)")]
    public float airQualityImpact = 0.5f;

    [Header("Spawn Ayarlari")]
    [Tooltip("Spawn olasılık ağırlığı (yüksek = daha sık spawn)")]
    [Range(0.1f, 10f)] public float spawnWeight = 1f;
    [Tooltip("Bu çöpün tercih ettiği bölgeler (buralarda daha sık spawn olur)")]
    public BiomeType[] preferredBiomes;
    [Tooltip("Tercih edilen bölgelerde spawn ağırlık çarpanı")]
    [Range(1f, 5f)] public float biomeWeightMultiplier = 2f;

    [Header("Gorseller")]
    [Tooltip("Haritada dururken sprite'ın ölçeği")]
    public Vector2 spriteScale = Vector2.one;
    [Tooltip("Toplama sırasında oynatılacak renk tonu (tint)")]
    public Color collectionTint = new Color(1f, 1f, 1f, 0.6f);
}
