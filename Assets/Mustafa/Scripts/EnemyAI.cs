using UnityEngine;
using UnityEngine.AI;
using System.Collections;

/// <summary>
/// Temel Dusman Yapay Zekasi (Mustafa - Asama 3 - 3D Guncellemesi)
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
    private Rigidbody rb;
    private Renderer rend;

    private enum State { Idle, Chasing, Attacking }
    private State currentState = State.Idle;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();
        
        if (data != null)
        {
            currentHealth = data.maxHealth;
            moveSpeed = data.moveSpeed;
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
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist <= attackRange)
                {
                    currentState = State.Attacking;
                    if(rb != null) rb.linearVelocity = Vector3.zero;
                    
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
                if(rb != null) rb.linearVelocity = Vector3.zero;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    void FindTarget()
    {
        Collider[] playerCol = Physics.OverlapSphere(transform.position, detectRadius, playerLayer);
        if (playerCol.Length > 0)
        {
            target = playerCol[0].transform;
            targetBuilding = null;
            return;
        }

        Collider[] buildings = Physics.OverlapSphere(transform.position, detectRadius, buildingLayer);
        float closestDist = float.MaxValue;
        Transform bestTarget = null;
        PlacedBuilding bestBuilding = null;

        foreach (var col in buildings)
        {
            float d = Vector3.Distance(transform.position, col.transform.position);
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
        Vector3 dir = (destination - transform.position);
        dir.y = 0;
        dir.Normalize();
        
        if (rb != null)
            rb.linearVelocity = new Vector3(dir.x * moveSpeed, rb.linearVelocity.y, dir.z * moveSpeed);
    }

    void Attack()
    {
        lastAttackTime = Time.time;
        
        if (targetBuilding != null)
        {
            float damage = data != null ? data.attackDamage : 10f;
            targetBuilding.TakeDamage(damage);
        }

        StartCoroutine(AttackEffect());
    }

    IEnumerator AttackEffect()
    {
        if (rend != null)
        {
            rend.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            rend.material.color = Color.white;
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
        if (rend != null)
        {
            rend.material.color = Color.yellow;
            yield return new WaitForSeconds(0.1f);
            rend.material.color = Color.white;
        }
    }

    void Die()
    {
        GameEvents.EnemyDefeated(data != null ? data.enemyType : EnemyType.TinMonster);
        Destroy(gameObject);
    }
}
