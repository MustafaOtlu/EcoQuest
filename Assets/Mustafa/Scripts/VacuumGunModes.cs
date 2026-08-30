using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Vakum Silahi Ek Modlari (AntiVacuum ve WaterShoot) (Mustafa - Asama 3)
/// Bilge'nin VacuumGunController scriptine dokunmadan, modlari okuyarak calisir.
/// </summary>
[RequireComponent(typeof(VacuumGunController))]
public class VacuumGunModes : MonoBehaviour
{
    private VacuumGunController baseController;

    [Header("Su Atisi (WaterShoot)")]
    public float waterShootRange = 6f;
    public float waterDamage = 20f;
    public float waterShootCooldown = 0.5f;
    public ParticleSystem waterEffect;
    
    private float lastWaterShootTime;

    void Start()
    {
        baseController = GetComponent<VacuumGunController>();
    }

    void Update()
    {
        // Fire input control via Unity's new Input System
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            if (baseController.CurrentMode == VacuumMode.WaterShoot)
            {
                if (Time.time >= lastWaterShootTime + waterShootCooldown)
                {
                    ShootWater();
                }
            }
            else if (baseController.CurrentMode == VacuumMode.AntiVacuum)
            {
                PushObjects();
            }
        }
    }

    void ShootWater()
    {
        lastWaterShootTime = Time.time;

        if (waterEffect != null)
        {
            waterEffect.Play();
        }

        // Temiz su kaynagi harcama (Opsiyonel GDD detayi)
        if (GameManager.Instance.GetResource(ResourceType.CleanWater) > 0)
        {
            GameManager.Instance.SpendResource(ResourceType.CleanWater, 1);
        }

        // Raycast ile dusman vurma
        RaycastHit2D hit = Physics2D.Raycast(transform.position, baseController.AimDirection, waterShootRange);
        if (hit.collider != null)
        {
            BossAI boss = hit.collider.GetComponent<BossAI>();
            if (boss != null)
            {
                boss.TakeDamage(waterDamage, true);
            }
            else
            {
                EnemyAI enemy = hit.collider.GetComponent<EnemyAI>();
                if (enemy != null)
                {
                    enemy.TakeDamage(waterDamage);
                }
            }
        }
    }

    void PushObjects()
    {
        // Anti-Vacuum: Coplari veya dusmanlari itme
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 4f);
        foreach (var hit in hits)
        {
            Rigidbody2D rb = hit.GetComponent<Rigidbody2D>();
            if (rb != null && hit.gameObject != this.gameObject)
            {
                Vector2 pushDir = (hit.transform.position - transform.position).normalized;
                rb.AddForce(pushDir * 5f, ForceMode2D.Force);
            }
        }
    }
}
