using UnityEngine;
using System.Collections;

/// <summary>
/// Haritada duran tek bir çöp/atık objesini temsil eder.
/// WasteSpawner tarafından oluşturulur, VacuumGunController tarafından toplanır.
/// 
/// Gerekli bileşenler: SpriteRenderer, CircleCollider2D (trigger olarak ayarlanmalı).
/// Prefab üzerinde "Waste" tag'i ve uygun layer ayarlanmalıdır.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(CircleCollider2D))]
public class WasteItem : MonoBehaviour
{
    [Header("Veri")]
    [SerializeField] WasteData wasteData;

    [Header("Gorsel Ayarlar")]
    [SerializeField] float spawnAnimDuration = 0.3f;
    [SerializeField] float hoverAmplitude = 0.05f;
    [SerializeField] float hoverSpeed = 2f;
    [SerializeField] float highlightScale = 1.15f;

    // Bileşen referansları
    SpriteRenderer spriteRenderer;
    CircleCollider2D interactionCollider;

    // Vakum durumu
    bool isBeingVacuumed;
    float vacuumProgress;
    Transform vacuumTarget; // Oyuncunun pozisyonu (vakum çekerken)
    Vector3 vacuumStartPos;

    // Durum
    bool isCollected;
    bool isInitialized;
    bool isPlayerNearby;
    Vector3 basePosition;
    Color originalColor;

    // ── Public Properties ──

    /// <summary>Bu çöpün veri tanımı</summary>
    public WasteData Data => wasteData;

    /// <summary>Çöp şu an vakumla çekilmekte mi?</summary>
    public bool IsBeingVacuumed => isBeingVacuumed;

    /// <summary>Vakum toplama ilerlemesi (0 = başlamadı, 1 = tamamlandı)</summary>
    public float VacuumProgress => vacuumProgress;

    /// <summary>Çöp zaten toplandı mı?</summary>
    public bool IsCollected => isCollected;

    // ── Initialization ──

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        interactionCollider = GetComponent<CircleCollider2D>();
        interactionCollider.isTrigger = true;
    }

    /// <summary>
    /// WasteSpawner tarafından çağrılır. Çöp objesini veriye göre yapılandırır.
    /// </summary>
    public void Initialize(WasteData data)
    {
        wasteData = data;

        if (data.sprite != null)
        {
            spriteRenderer.sprite = data.sprite;
        }

        transform.localScale = new Vector3(data.spriteScale.x, data.spriteScale.y, 1f);
        originalColor = spriteRenderer.color;
        basePosition = transform.position;
        isInitialized = true;

        // Spawn animasyonu başlat
        StartCoroutine(SpawnAnimation());
    }

    // ── Spawn Animasyonu ──

    IEnumerator SpawnAnimation()
    {
        Vector3 targetScale = transform.localScale;
        transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < spawnAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / spawnAnimDuration;
            // Ease out back: hafif zıplama efekti
            float eased = 1f + 2.7f * Mathf.Pow(t - 1f, 3f) + 1.7f * Mathf.Pow(t - 1f, 2f);
            transform.localScale = targetScale * eased;
            yield return null;
        }

        transform.localScale = targetScale;
    }

    // ── Update ──

    void Update()
    {
        if (!isInitialized || isCollected) return;

        if (isBeingVacuumed)
        {
            UpdateVacuum();
        }
        else
        {
            UpdateHover();
        }

        UpdateHighlight();
    }

    /// <summary>Idle durumda hafif yukarı-aşağı hareket</summary>
    void UpdateHover()
    {
        float yOffset = Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        transform.position = basePosition + new Vector3(0f, yOffset, 0f);
    }

    /// <summary>Oyuncu yakınken hafif büyüme efekti</summary>
    void UpdateHighlight()
    {
        if (isBeingVacuumed) return;

        float currentScale = wasteData != null ? wasteData.spriteScale.x : 1f;
        float targetScaleVal = isPlayerNearby ? currentScale * highlightScale : currentScale;
        float currentScaleVal = transform.localScale.x;
        float newScale = Mathf.Lerp(currentScaleVal, targetScaleVal, Time.deltaTime * 8f);
        transform.localScale = new Vector3(newScale, newScale, 1f);
    }

    // ── Vakum Mekaniği ──

    /// <summary>
    /// Vakum silahı tarafından çağrılır. Çöpü çekmeye başlar.
    /// </summary>
    /// <param name="target">Oyuncunun Transform'u (çekilme hedefi)</param>
    public void StartVacuum(Transform target)
    {
        if (isCollected || isBeingVacuumed) return;

        isBeingVacuumed = true;
        vacuumProgress = 0f;
        vacuumTarget = target;
        vacuumStartPos = transform.position;
    }

    /// <summary>
    /// Vakum silahı bırakıldığında çağrılır. Çekmeyi durdurur.
    /// </summary>
    public void StopVacuum()
    {
        if (!isBeingVacuumed) return;

        isBeingVacuumed = false;
        vacuumProgress = 0f;
        vacuumTarget = null;

        // Rengi orijinale döndür
        spriteRenderer.color = originalColor;
    }

    void UpdateVacuum()
    {
        if (vacuumTarget == null || wasteData == null)
        {
            StopVacuum();
            return;
        }

        // İlerlemeyi artır
        vacuumProgress += Time.deltaTime / wasteData.collectionTime;

        // Çöpü oyuncuya doğru çek (progress'e göre hızlanan lerp)
        float pullStrength = vacuumProgress * vacuumProgress; // Üstel hızlanma
        transform.position = Vector3.Lerp(vacuumStartPos, vacuumTarget.position, pullStrength);

        // Toplama tint efekti
        spriteRenderer.color = Color.Lerp(originalColor, wasteData.collectionTint, vacuumProgress);

        // Küçülme efekti (son %30'da)
        if (vacuumProgress > 0.7f)
        {
            float shrinkT = (vacuumProgress - 0.7f) / 0.3f;
            float scale = Mathf.Lerp(wasteData.spriteScale.x, 0f, shrinkT);
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        // Toplama tamamlandı
        if (vacuumProgress >= 1f)
        {
            Collect();
        }
    }

    // ── Toplama ──

    /// <summary>
    /// Çöpü toplar: envantere ekler, event tetikler, objeyi yok eder.
    /// </summary>
    void Collect()
    {
        if (isCollected) return;
        isCollected = true;

        // Envantere ekle
        if (WasteInventory.Instance != null)
        {
            WasteInventory.Instance.AddWaste(wasteData);
        }

        // Çöp toplandığında hava kalitesini hafif iyileştir
        if (wasteData.airQualityImpact > 0f && GameManager.Instance != null)
        {
            GameManager.Instance.UpdateIndicator(IndicatorType.AirQuality, wasteData.airQualityImpact);
        }

        // Event tetikle
        WasteEvents.WasteCollected(wasteData);

        // Objeyi yok et
        Destroy(gameObject);
    }

    // ── Tetikleyici Alanı (Highlight için) ──

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
        }
    }

    // ── Editor Yardımcıları ──

    void OnDrawGizmosSelected()
    {
        if (interactionCollider != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, interactionCollider.radius);
        }
    }
}
