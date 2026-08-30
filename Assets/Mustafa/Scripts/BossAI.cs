using UnityEngine;
using System.Collections;

/// <summary>
/// Iklim Canavari (Boss) Yapay Zekasi (Mustafa - Asama 5)
/// Karbon %90 uzerindeyken cikar, binalari hizla yikar.
/// Sadece Su Saldirisi (WaterShoot) ve Yuksek Teknoloji Filtreler ile zayiflatilir.
/// </summary>
public class BossAI : EnemyAI
{
    [Header("Boss Ayarlari")]
    public float bossScale = 3f;
    public float specialAttackCooldown = 10f;
    
    private float lastSpecialAttack;

    void Awake()
    {
        transform.localScale = Vector3.one * bossScale;
    }

    void Update()
    {
        // Temel AI Loop Update'den ziyade Coroutine'de, biz ekstra davranis ekliyoruz
        if (Time.time > lastSpecialAttack + specialAttackCooldown)
        {
            PerformSpecialAttack();
        }
    }

    void PerformSpecialAttack()
    {
        lastSpecialAttack = Time.time;
        Debug.Log("IKLIM CANAVARI OZEL SALDIRI: AoE Hasar!");
        
        // Etraftaki tum binalara hasar ver
        Collider2D[] cols = Physics2D.OverlapCircleAll(transform.position, detectRadius * 1.5f, buildingLayer);
        foreach (var col in cols)
        {
            var b = col.GetComponent<PlacedBuilding>();
            if (b != null)
            {
                b.TakeDamage(25f);
            }
        }
        
        // Ekrani salla, parcacik cikar
        // TODO: CameraShake
    }

    // Normal hasar almasini kisitla
    public void TakeDamage(float amount, bool isWaterAttack = false)
    {
        if (!isWaterAttack)
        {
            // Normal saldirilar boss'a %10 hasar verir
            amount *= 0.1f;
        }

        base.TakeDamage(amount);
    }
    
    public override void TakeDamage(float amount)
    {
        TakeDamage(amount, false);
    }
}
