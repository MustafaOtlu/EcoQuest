using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Haritaya çöp/atık spawn eden yönetici.
/// Coroutine ile belirli aralıklarla çöp oluşturur, maksimum sınırı takip eder.
/// Çöpler tile grid'ine hizalanır ve çakışma kontrolü yapılır.
/// 
/// Kullanım: Boş bir GameObject'e ekle, Inspector'dan wasteTypes ve spawnZones'u ata.
/// </summary>
public class WasteSpawner : MonoBehaviour
{
    [Header("Spawn Edilebilir Cop Tipleri")]
    [Tooltip("Spawn olabilecek çöp tipleri (WasteData ScriptableObject'leri)")]
    [SerializeField] WasteData[] wasteTypes;

    [Header("Spawn Ayarlari")]
    [Tooltip("Çöp spawn aralığı (saniye)")]
    [SerializeField] float spawnInterval = 10f;
    [Tooltip("Haritadaki maksimum çöp sayısı")]
    [SerializeField] int maxWasteOnMap = 30;
    [Tooltip("Her spawn turunda oluşturulacak çöp sayısı")]
    [SerializeField, Range(1, 5)] int wastePerSpawn = 1;
    [Tooltip("Oyun başlangıcında ilk spawn'dan önceki bekleme süresi")]
    [SerializeField] float initialDelay = 3f;

    [Header("Spawn Alani")]
    [Tooltip("Spawn bölgeleri (boş bırakılırsa spawner pozisyonu etrafında spawn olur)")]
    [SerializeField] Transform[] spawnZones;
    [Tooltip("Her spawn bölgesinin yarıçapı")]
    [SerializeField] float spawnZoneRadius = 15f;
    [Tooltip("Spawner pozisyonu etrafında varsayılan spawn yarıçapı")]
    [SerializeField] float defaultSpawnRadius = 20f;

    [Header("Grid Hizalama")]
    [Tooltip("Çöpleri tile grid'ine hizala")]
    [SerializeField] bool snapToGrid = true;
    [Tooltip("Grid hücre boyutu (Unity unit)")]
    [SerializeField] float gridCellSize = 1f;

    [Header("Cakisma Kontrolu")]
    [Tooltip("Spawn noktası etrafında çakışma kontrol yarıçapı")]
    [SerializeField] float overlapCheckRadius = 0.4f;
    [Tooltip("Spawn engelleyen katmanlar (binalar, su, vs.)")]
    [SerializeField] LayerMask obstacleLayer;
    [Tooltip("Geçerli pozisyon bulmak için maksimum deneme sayısı")]
    [SerializeField] int maxSpawnAttempts = 10;

    [Header("Prefab")]
    [Tooltip("Çöp prefab'ı (WasteItem bileşeni olmalı)")]
    [SerializeField] GameObject wasteItemPrefab;

    // Aktif çöpler
    readonly List<WasteItem> activeWaste = new();

    // Spawn coroutine referansı
    Coroutine spawnCoroutine;

    // Toplam ağırlık (weighted random için cache)
    float totalSpawnWeight;

    // ── Public Properties ──

    /// <summary>Haritadaki aktif çöp sayısı</summary>
    public int ActiveWasteCount => activeWaste.Count;

    /// <summary>Maksimum çöp kapasitesi</summary>
    public int MaxWasteOnMap => maxWasteOnMap;

    // ── Lifecycle ──

    void Start()
    {
        CalculateTotalWeight();
        spawnCoroutine = StartCoroutine(SpawnLoop());

        // Çöp toplandığında listeden çıkar
        WasteEvents.OnWasteCollected += OnWasteCollected;
    }

    void OnDestroy()
    {
        WasteEvents.OnWasteCollected -= OnWasteCollected;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }
    }

    // ── Spawn Döngüsü ──

    IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            if (activeWaste.Count < maxWasteOnMap)
            {
                int toSpawn = Mathf.Min(wastePerSpawn, maxWasteOnMap - activeWaste.Count);
                for (int i = 0; i < toSpawn; i++)
                {
                    TrySpawnWaste();
                }
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    /// <summary>Tek bir çöp spawn etmeyi dener</summary>
    void TrySpawnWaste()
    {
        // Rastgele çöp tipi seç (weighted random)
        WasteData selectedWaste = SelectRandomWaste();
        if (selectedWaste == null) return;

        // Geçerli pozisyon bul
        Vector3? spawnPos = FindValidSpawnPosition();
        if (!spawnPos.HasValue)
        {
            Debug.LogWarning("[WasteSpawner] Geçerli spawn pozisyonu bulunamadı.");
            return;
        }

        SpawnWasteAt(selectedWaste, spawnPos.Value);
    }

    /// <summary>Belirli pozisyona belirli çöp tipi spawn eder</summary>
    void SpawnWasteAt(WasteData data, Vector3 position)
    {
        if (wasteItemPrefab == null)
        {
            Debug.LogError("[WasteSpawner] wasteItemPrefab atanmamış!");
            return;
        }

        GameObject wasteObj = Instantiate(wasteItemPrefab, position, Quaternion.identity);
        wasteObj.name = $"Waste_{data.displayName}_{activeWaste.Count}";

        WasteItem wasteItem = wasteObj.GetComponent<WasteItem>();
        if (wasteItem != null)
        {
            wasteItem.Initialize(data);
            activeWaste.Add(wasteItem);

            // Event tetikle
            WasteEvents.WasteSpawned(data, position);
        }
        else
        {
            Debug.LogError("[WasteSpawner] Prefab'da WasteItem bileşeni bulunamadı!");
            Destroy(wasteObj);
        }
    }

    // ── Weighted Random Seçim ──

    void CalculateTotalWeight()
    {
        totalSpawnWeight = 0f;
        if (wasteTypes == null) return;

        foreach (var waste in wasteTypes)
        {
            if (waste != null)
            {
                totalSpawnWeight += waste.spawnWeight;
            }
        }
    }

    WasteData SelectRandomWaste()
    {
        if (wasteTypes == null || wasteTypes.Length == 0) return null;
        if (totalSpawnWeight <= 0f)
        {
            CalculateTotalWeight();
            if (totalSpawnWeight <= 0f) return null;
        }

        float random = Random.Range(0f, totalSpawnWeight);
        float accumulated = 0f;

        foreach (var waste in wasteTypes)
        {
            if (waste == null) continue;
            accumulated += waste.spawnWeight;
            if (random <= accumulated)
            {
                return waste;
            }
        }

        // Fallback (float hassasiyet sorunu olursa)
        return wasteTypes[wasteTypes.Length - 1];
    }

    // ── Pozisyon Bulma ──

    Vector3? FindValidSpawnPosition()
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector3 candidatePos = GenerateRandomPosition();

            if (snapToGrid)
            {
                candidatePos = SnapToGrid(candidatePos);
            }

            // Çakışma kontrolü
            if (!IsPositionBlocked(candidatePos))
            {
                return candidatePos;
            }
        }

        return null;
    }

    Vector3 GenerateRandomPosition()
    {
        if (spawnZones != null && spawnZones.Length > 0)
        {
            // Rastgele bir spawn bölgesi seç
            Transform zone = spawnZones[Random.Range(0, spawnZones.Length)];
            if (zone != null)
            {
                Vector2 randomOffset = Random.insideUnitCircle * spawnZoneRadius;
                return zone.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
            }
        }

        // Varsayılan: spawner pozisyonu etrafında
        Vector2 offset = Random.insideUnitCircle * defaultSpawnRadius;
        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        float x = Mathf.Round(position.x / gridCellSize) * gridCellSize;
        float y = Mathf.Round(position.y / gridCellSize) * gridCellSize;
        return new Vector3(x, y, 0f);
    }

    bool IsPositionBlocked(Vector3 position)
    {
        // Fizik çakışma kontrolü
        Collider2D hit = Physics2D.OverlapCircle(position, overlapCheckRadius, obstacleLayer);
        if (hit != null) return true;

        // Mevcut çöplerle çakışma kontrolü
        foreach (var waste in activeWaste)
        {
            if (waste != null && Vector3.Distance(waste.transform.position, position) < gridCellSize * 0.9f)
            {
                return true;
            }
        }

        return false;
    }

    // ── Event Handlers ──

    void OnWasteCollected(WasteData data)
    {
        // Null referansları temizle (toplanan/yok edilen çöpler)
        activeWaste.RemoveAll(w => w == null);
    }

    // ── Public Methods ──

    /// <summary>Spawn döngüsünü durdurur</summary>
    public void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }
    }

    /// <summary>Spawn döngüsünü yeniden başlatır</summary>
    public void StartSpawning()
    {
        StopSpawning();
        spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    /// <summary>Belirli bir pozisyona zorla çöp spawn eder (debug/test amaçlı)</summary>
    public void ForceSpawn(WasteData data, Vector3 position)
    {
        if (activeWaste.Count >= maxWasteOnMap) return;
        SpawnWasteAt(data, position);
    }

    // ── Editor Gizmos ──

    void OnDrawGizmosSelected()
    {
        // Varsayılan spawn alanını göster
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, defaultSpawnRadius);

        // Spawn bölgelerini göster
        if (spawnZones != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            foreach (var zone in spawnZones)
            {
                if (zone != null)
                {
                    Gizmos.DrawWireSphere(zone.position, spawnZoneRadius);
                }
            }
        }
    }
}
