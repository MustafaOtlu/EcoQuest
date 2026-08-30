using UnityEngine;
using System.Collections;

/// <summary>
/// Temel Dusman Yapay Zekasi (Mustafa - Asama 3)
/// </summary>
public class EnemyAI : MonoBehaviour
{
    [Header("Ayarlar")]
    public EnemyData data;
    public float currentHealth;
    
    [Header("Hareket & AI")]
    public float moveSpeed = 2f;
    public float attackRange = 1.5f;
    public float attackCooldown = 2f;
    public float detectRadius = 8f;

    [Header("Katmanlar")]
    public LayerMask playerLayer;
    public LayerMask buildingLayer;

    private Transform target;
    private PlacedBuilding targetBuilding;
    private float lastAttackTime;
    private Rigidbody2D rb;
    private SpriteRenderer sr;

    private enum State { Idle, Chasing, Attacking }
    private State currentState = State.Idle;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        
        if (data != null)
        {
            currentHealth = data.maxHealth;
            moveSpeed = data.moveSpeed;
            attackRange = 1.5f; // GDD veya Data'da yoksa sabit
            attackCooldown = data.attackInterval;
            detectRadius = data.detectionRange;
        }
        else
        {
            currentHealth = 50f;
        }

        StartCoroutine(AILoop());
    }

    IEnumerator AILoop()
    {
        while (currentHealth > 0)
        {
            FindTarget();
            
            if (target != null)
            {
                float dist = Vector2.Distance(transform.position, target.position);
                if (dist <= attackRange)
                {
                    currentState = State.Attacking;
                    rb.linearVelocity = Vector2.zero;
                    
                    if (Time.time >= lastAttackTime + attackCooldown)
                    {
                        Attack();
                    }
                }
                else
                {
                    currentState = State.Chasing;
                    MoveTowards(target.position);
                }
            }
            else
            {
                currentState = State.Idle;
                rb.linearVelocity = Vector2.zero;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    void FindTarget()
    {
        // 1. Once Player'a bak
        Collider2D playerCol = Physics2D.OverlapCircle(transform.position, detectRadius, playerLayer);
        if (playerCol != null)
        {
            target = playerCol.transform;
            targetBuilding = null;
            return;
        }

        // 2. Player yoksa binalara bak
        Collider2D[] buildings = Physics2D.OverlapCircleAll(transform.position, detectRadius, buildingLayer);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;
        PlacedBuilding bestBuilding = null;

        foreach (var col in buildings)
        {
            float d = Vector2.Distance(transform.position, col.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                bestTarget = col.transform;
                bestBuilding = col.GetComponent<PlacedBuilding>();
            }
        }

        target = bestTarget;
        targetBuilding = bestBuilding;
    }

    void MoveTowards(Vector3 destination)
    {
        Vector2 dir = (destination - transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
        
        if (sr != null)
        {
            sr.flipX = dir.x < 0;
        }
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        
        // Basit saldiri mantigi
        if (targetBuilding != null)
        {
            float damage = data != null ? data.attackDamage : 10f;
            targetBuilding.TakeDamage(damage);
            Debug.Log($"{name} binaya saldirdi: {damage} hasar.");
        }
        else if (target != null && target.CompareTag("Player"))
        {
            // Player take damage
            Debug.Log($"{name} oyuncuya saldirdi!");
        }

        // Vurus animasyonu/rengi
        StartCoroutine(AttackEffect());
    }

    IEnumerator AttackEffect()
    {
        if (sr != null)
        {
            Color orig = sr.color;
            sr.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            sr.color = orig;
        }
    }

    public virtual void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(DamageEffect());
        }
    }

    IEnumerator DamageEffect()
    {
        if (sr != null)
        {
            Color orig = sr.color;
            sr.color = Color.white;
            yield return new WaitForSeconds(0.1f);
            sr.color = orig;
        }
    }

    void Die()
    {
        // GDD: Dusman olunce cop birakir
        // Bilge'nin yazdigi WasteSpawner veya event tetiklenebilir
        GameEvents.EnemyDefeated(data != null ? data.enemyType : EnemyType.TinMonster);
        Destroy(gameObject);
    }
}
