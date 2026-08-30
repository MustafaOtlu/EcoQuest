using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Oyuncunun topladığı çöpleri takip eden envanter sistemi.
/// Singleton pattern — sahnede tek bir instance bulunmalı.
/// 
/// Geri Dönüşüm Tesisi (Aşama 2) bu envanterden çöp alarak
/// Metal/Plastik'e dönüştürecek.
/// </summary>
public class WasteInventory : MonoBehaviour
{
    public static WasteInventory Instance { get; private set; }

    [Header("Kapasite")]
    [Tooltip("Envanterin maksimum çöp kapasitesi")]
    [SerializeField] int maxCapacity = 50;

    // Her çöp tipinden kaç adet toplandı
    readonly Dictionary<WasteType, int> collectedWaste = new();

    // Toplam potansiyel metal/plastik (hızlı erişim için cache)
    int cachedTotalMetal;
    int cachedTotalPlastic;

    // WasteData referansları (yield hesabı için)
    readonly Dictionary<WasteType, WasteData> wasteDataLookup = new();

    // ── Public Properties ──

    /// <summary>Envanterdeki toplam çöp sayısı</summary>
    public int TotalWaste { get; private set; }

    /// <summary>Envanter kapasitesi</summary>
    public int MaxCapacity => maxCapacity;

    /// <summary>Envanter dolu mu?</summary>
    public bool IsFull => TotalWaste >= maxCapacity;

    /// <summary>Envanterdeki toplam potansiyel Metal verimi</summary>
    public int PotentialMetal => cachedTotalMetal;

    /// <summary>Envanterdeki toplam potansiyel Plastik verimi</summary>
    public int PotentialPlastic => cachedTotalPlastic;

    // ── Lifecycle ──

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ── Public Methods ──

    /// <summary>
    /// Envantere çöp ekler. Kapasite doluysa ekleme yapılmaz.
    /// </summary>
    /// <returns>Başarıyla eklendi mi?</returns>
    public bool AddWaste(WasteData data)
    {
        if (data == null) return false;
        if (IsFull)
        {
            Debug.LogWarning("[WasteInventory] Envanter dolu! Çöp eklenemedi.");
            return false;
        }

        WasteType type = data.wasteType;

        if (!collectedWaste.ContainsKey(type))
        {
            collectedWaste[type] = 0;
        }

        collectedWaste[type]++;
        TotalWaste++;

        // WasteData referansını kaydet (yield hesabı için)
        if (!wasteDataLookup.ContainsKey(type))
        {
            wasteDataLookup[type] = data;
        }

        RecalculateYields();

        // Event tetikle
        WasteEvents.WasteInventoryChanged(TotalWaste);

        Debug.Log($"[WasteInventory] {data.displayName} eklendi. Toplam: {TotalWaste}/{maxCapacity}");

        return true;
    }

    /// <summary>
    /// Belirli tipteki çöpten belirtilen miktarı çıkarır.
    /// Geri Dönüşüm Tesisi tarafından kullanılır (Aşama 2).
    /// </summary>
    /// <returns>Başarıyla çıkarıldı mı?</returns>
    public bool RemoveWaste(WasteType type, int amount)
    {
        if (!collectedWaste.ContainsKey(type) || collectedWaste[type] < amount)
        {
            return false;
        }

        collectedWaste[type] -= amount;
        TotalWaste -= amount;

        if (collectedWaste[type] <= 0)
        {
            collectedWaste.Remove(type);
        }

        RecalculateYields();
        WasteEvents.WasteInventoryChanged(TotalWaste);

        return true;
    }

    /// <summary>Belirli bir çöp tipinin envanterdeki sayısı</summary>
    public int GetWasteCount(WasteType type)
    {
        return collectedWaste.TryGetValue(type, out int count) ? count : 0;
    }

    /// <summary>Envanterdeki tüm çöp tiplerini ve sayılarını döndürür</summary>
    public Dictionary<WasteType, int> GetAllWaste()
    {
        return new Dictionary<WasteType, int>(collectedWaste);
    }

    /// <summary>Envanteri tamamen temizler</summary>
    public void ClearInventory()
    {
        collectedWaste.Clear();
        TotalWaste = 0;
        cachedTotalMetal = 0;
        cachedTotalPlastic = 0;
        WasteEvents.WasteInventoryChanged(0);
    }

    /// <summary>
    /// Envanterdeki tüm çöpleri Metal ve Plastik'e dönüştürür.
    /// GameManager üzerinden kaynak ekler.
    /// Geri Dönüşüm Silahı veya Geri Dönüşüm Tesisi tarafından çağrılır.
    /// </summary>
    /// <returns>Dönüştürülen toplam çöp sayısı</returns>
    public int RecycleAll()
    {
        if (TotalWaste == 0) return 0;

        int totalRecycled = 0;
        int totalMetal = 0;
        int totalPlastic = 0;

        foreach (var kvp in collectedWaste)
        {
            if (wasteDataLookup.TryGetValue(kvp.Key, out WasteData data))
            {
                totalMetal += data.metalYield * kvp.Value;
                totalPlastic += data.plasticYield * kvp.Value;
                totalRecycled += kvp.Value;
            }
        }

        // Kaynakları GameManager'a ekle
        if (GameManager.Instance != null)
        {
            if (totalMetal > 0)
                GameManager.Instance.AddResource(ResourceType.Metal, totalMetal);
            if (totalPlastic > 0)
                GameManager.Instance.AddResource(ResourceType.Plastic, totalPlastic);
        }

        Debug.Log($"[WasteInventory] Geri dönüşüm: {totalRecycled} çöp → " +
                  $"{totalMetal} Metal, {totalPlastic} Plastik");

        ClearInventory();
        return totalRecycled;
    }

    /// <summary>
    /// Belirli tipteki çöpleri dönüştürür.
    /// </summary>
    public bool RecycleType(WasteType type, int amount)
    {
        int available = GetWasteCount(type);
        if (available < amount) return false;

        if (!wasteDataLookup.TryGetValue(type, out WasteData data)) return false;

        int metalGain = data.metalYield * amount;
        int plasticGain = data.plasticYield * amount;

        if (GameManager.Instance != null)
        {
            if (metalGain > 0)
                GameManager.Instance.AddResource(ResourceType.Metal, metalGain);
            if (plasticGain > 0)
                GameManager.Instance.AddResource(ResourceType.Plastic, plasticGain);
        }

        RemoveWaste(type, amount);
        return true;
    }

    // ── Internal ──

    void RecalculateYields()
    {
        cachedTotalMetal = 0;
        cachedTotalPlastic = 0;

        foreach (var kvp in collectedWaste)
        {
            if (wasteDataLookup.TryGetValue(kvp.Key, out WasteData data))
            {
                cachedTotalMetal += data.metalYield * kvp.Value;
                cachedTotalPlastic += data.plasticYield * kvp.Value;
            }
        }
    }
}
