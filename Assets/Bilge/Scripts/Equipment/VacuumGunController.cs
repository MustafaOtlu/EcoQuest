using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Oyuncunun vakum silahı kontrolcüsü.
/// Çöpleri menzil içinde tespit eder ve vakum modunda çeker.
/// 
/// New Input System kullanır. Oyuncu GameObject'ine eklenmeli.
/// 
/// Modlar (GDD'den):
/// - Vacuum: Çöpleri çeker (bu aşamada aktif)
/// - AntiVacuum: Nesneleri iter (Aşama 2+)
/// - WaterShoot: Su fırlatır (Aşama 2+)
/// 
/// Gereklilikler:
/// - Oyuncu objesinde PlayerInput bileşeni veya doğrudan Input System kullanımı
/// - WasteItem bileşenli çöp objeleri sahnede mevcut
/// - WasteInventory sahnede mevcut
/// </summary>
public class VacuumGunController : MonoBehaviour
{
    [Header("Genel")]
    [Tooltip("Şu anki vakum modu")]
    [SerializeField] VacuumMode currentMode = VacuumMode.Vacuum;

    [Header("Vakum Ayarlari")]
    [Tooltip("Vakum menzili (Unity unit)")]
    [SerializeField] float vacuumRange = 4f;
    [Tooltip("Vakum etki alanı açısı (derece, 0 = sadece bakış yönü)")]
    [SerializeField, Range(0f, 180f)] float vacuumAngle = 50f;
    [Tooltip("Menzildeki çöp objeleri için fizik katmanı")]
    [SerializeField] LayerMask wasteLayer;

    [Header("Gorsel")]
    [Tooltip("Vakum efektinin çıkış noktası (silah ucu)")]
    [SerializeField] Transform vacuumOrigin;
    [Tooltip("Vakum aktifken gösterilecek parçacık sistemi (opsiyonel)")]
    [SerializeField] ParticleSystem vacuumParticles;

    [Header("Ses (Opsiyonel)")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip vacuumLoopSound;
    [SerializeField] AudioClip collectSound;

    // Durum
    bool isVacuumActive;
    Vector2 aimDirection = Vector2.right;
    WasteItem currentTarget;
    readonly List<WasteItem> wasteInRange = new();

    // Input
    InputAction fireAction;
    InputAction switchModeAction;
    InputAction lookAction;

    // ── Public Properties ──

    /// <summary>Vakum aktif mi?</summary>
    public bool IsVacuumActive => isVacuumActive;

    /// <summary>Şu anki mod</summary>
    public VacuumMode CurrentMode => currentMode;

    /// <summary>Nişan yönü</summary>
    public Vector2 AimDirection => aimDirection;

    /// <summary>Şu an çekilen çöp (varsa)</summary>
    public WasteItem CurrentTarget => currentTarget;

    // ── Lifecycle ──

    void Awake()
    {
        SetupInput();

        // Vakum çıkış noktası yoksa bu objenin pozisyonunu kullan
        if (vacuumOrigin == null)
        {
            vacuumOrigin = transform;
        }
    }

    void OnEnable()
    {
        EnableInput();
        WasteEvents.OnWasteCollected += OnWasteCollected;
    }

    void OnDisable()
    {
        DisableInput();
        WasteEvents.OnWasteCollected -= OnWasteCollected;
        StopVacuum();
    }

    void Update()
    {
        UpdateAimDirection();

        if (isVacuumActive && currentMode == VacuumMode.Vacuum)
        {
            UpdateVacuumTargeting();
        }
    }

    // ── Input Setup (New Input System) ──

    void SetupInput()
    {
        // Fire: Sol tık / gamepad sağ tetik
        fireAction = new InputAction("Fire", InputActionType.Button);
        fireAction.AddBinding("<Mouse>/leftButton");
        fireAction.AddBinding("<Gamepad>/rightTrigger");

        // Mod değiştirme: Q tuşu
        switchModeAction = new InputAction("SwitchMode", InputActionType.Button);
        switchModeAction.AddBinding("<Keyboard>/q");

        // Fare pozisyonu (nişan yönü)
        lookAction = new InputAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Mouse>/position");
    }

    void EnableInput()
    {
        fireAction.Enable();
        fireAction.started += OnFireStarted;
        fireAction.canceled += OnFireCanceled;

        switchModeAction.Enable();
        switchModeAction.performed += OnSwitchMode;

        lookAction.Enable();
    }

    void DisableInput()
    {
        fireAction.started -= OnFireStarted;
        fireAction.canceled -= OnFireCanceled;
        fireAction.Disable();

        switchModeAction.performed -= OnSwitchMode;
        switchModeAction.Disable();

        lookAction.Disable();
    }

    // ── Input Handlers ──

    void OnFireStarted(InputAction.CallbackContext ctx)
    {
        if (currentMode == VacuumMode.Vacuum)
        {
            StartVacuum();
        }
        // AntiVacuum ve WaterShoot modları Aşama 2+'de implemente edilecek
    }

    void OnFireCanceled(InputAction.CallbackContext ctx)
    {
        StopVacuum();
    }

    void OnSwitchMode(InputAction.CallbackContext ctx)
    {
        // Modlar arasında geçiş yap
        int modeCount = System.Enum.GetValues(typeof(VacuumMode)).Length;
        int nextMode = ((int)currentMode + 1) % modeCount;
        currentMode = (VacuumMode)nextMode;

        Debug.Log($"[VacuumGun] Mod değiştirildi: {currentMode}");

        // Mevcut vakumu durdur (mod değişince)
        if (isVacuumActive)
        {
            StopVacuum();
        }
    }

    // ── Nişan Yönü ──

    void UpdateAimDirection()
    {
        Vector2 mouseScreenPos = lookAction.ReadValue<Vector2>();
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mouseScreenPos.x, mouseScreenPos.y, 0f));
        Vector2 direction = (Vector2)(mouseWorldPos - vacuumOrigin.position);

        if (direction.sqrMagnitude > 0.01f)
        {
            aimDirection = direction.normalized;
        }
    }

    // ── Vakum Mekaniği ──

    void StartVacuum()
    {
        isVacuumActive = true;
        WasteEvents.VacuumStateChanged(true);

        // Parçacık efekti
        if (vacuumParticles != null && !vacuumParticles.isPlaying)
        {
            vacuumParticles.Play();
        }

        // Ses
        if (audioSource != null && vacuumLoopSound != null)
        {
            audioSource.clip = vacuumLoopSound;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void StopVacuum()
    {
        if (!isVacuumActive) return;

        isVacuumActive = false;
        WasteEvents.VacuumStateChanged(false);

        // Mevcut hedefi bırak
        if (currentTarget != null)
        {
            currentTarget.StopVacuum();
            currentTarget = null;
        }

        // Parçacık efekti
        if (vacuumParticles != null && vacuumParticles.isPlaying)
        {
            vacuumParticles.Stop();
        }

        // Ses
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    void UpdateVacuumTargeting()
    {
        // Mevcut hedef hâlâ geçerli mi?
        if (currentTarget != null)
        {
            if (currentTarget.IsCollected || !IsInVacuumCone(currentTarget.transform.position))
            {
                currentTarget.StopVacuum();
                currentTarget = null;
            }
            else
            {
                // Hedef hâlâ geçerli, çekmeye devam et
                return;
            }
        }

        // Yeni hedef bul
        FindBestTarget();

        if (currentTarget != null)
        {
            currentTarget.StartVacuum(vacuumOrigin);
        }
    }

    void FindBestTarget()
    {
        // Menzildeki tüm çöpleri bul
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            vacuumOrigin.position,
            vacuumRange,
            wasteLayer
        );

        if (hits.Length == 0) return;

        float closestDist = float.MaxValue;
        WasteItem bestTarget = null;

        foreach (var hit in hits)
        {
            // WasteItem bileşeni kontrol et
            WasteItem waste = hit.GetComponent<WasteItem>();
            if (waste == null || waste.IsCollected) continue;

            // Envanter dolu mu?
            if (WasteInventory.Instance != null && WasteInventory.Instance.IsFull) continue;

            // Vakum koni/açı kontrolü
            if (!IsInVacuumCone(waste.transform.position)) continue;

            // En yakın olanı seç
            float dist = Vector2.Distance(vacuumOrigin.position, waste.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                bestTarget = waste;
            }
        }

        currentTarget = bestTarget;
    }

    /// <summary>
    /// Verilen pozisyonun vakum konisi/açısı içinde olup olmadığını kontrol eder.
    /// </summary>
    bool IsInVacuumCone(Vector3 targetPos)
    {
        Vector2 toTarget = (Vector2)(targetPos - vacuumOrigin.position);
        float distance = toTarget.magnitude;

        // Menzil kontrolü
        if (distance > vacuumRange) return false;

        // Açı kontrolü (vacuumAngle = 180 ise tam daire)
        if (vacuumAngle >= 180f) return true;

        float angle = Vector2.Angle(aimDirection, toTarget);
        return angle <= vacuumAngle * 0.5f;
    }

    // ── Event Handlers ──

    void OnWasteCollected(WasteData data)
    {
        // Toplama sesi
        if (audioSource != null && collectSound != null)
        {
            audioSource.PlayOneShot(collectSound);
        }

        // Mevcut hedef toplandıysa temizle
        if (currentTarget != null && currentTarget.IsCollected)
        {
            currentTarget = null;
        }
    }

    // ── Editor Gizmos ──

    void OnDrawGizmosSelected()
    {
        Transform origin = vacuumOrigin != null ? vacuumOrigin : transform;

        // Menzil dairesi
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawWireSphere(origin.position, vacuumRange);

        // Vakum konisi
        if (vacuumAngle < 180f)
        {
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.4f);
            float halfAngle = vacuumAngle * 0.5f;
            Vector3 dir = Application.isPlaying ? (Vector3)aimDirection : origin.right;

            Vector3 leftRay = Quaternion.Euler(0, 0, halfAngle) * dir * vacuumRange;
            Vector3 rightRay = Quaternion.Euler(0, 0, -halfAngle) * dir * vacuumRange;

            Gizmos.DrawLine(origin.position, origin.position + leftRay);
            Gizmos.DrawLine(origin.position, origin.position + rightRay);
        }

        // Aktif hedef
        if (Application.isPlaying && currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(origin.position, currentTarget.transform.position);
        }
    }
}
