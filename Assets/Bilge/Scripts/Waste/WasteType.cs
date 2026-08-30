/// <summary>
/// Haritada spawn olan çöp/atık türlerini tanımlar.
/// _Shared/GameEnums.cs'e eklenmedi çünkü Bilge'nin modülüne özel.
/// İleride gerekirse Mustafa'ya _Shared'a taşınması önerilebilir.
/// </summary>
public enum WasteType
{
    /// <summary>Metal hurda — geri dönüşümde Metal verir</summary>
    MetalScrap,

    /// <summary>Plastik şişe — geri dönüşümde Plastik verir</summary>
    PlasticBottle,

    /// <summary>Karışık atık — hem Metal hem Plastik verir (düşük miktar)</summary>
    MixedWaste,

    /// <summary>Elektronik atık — yüksek Metal verimi</summary>
    ElectronicWaste,

    /// <summary>Organik atık — gübre potansiyeli (ileride)</summary>
    OrganicWaste
}
