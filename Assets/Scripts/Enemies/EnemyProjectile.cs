using UnityEngine;

// Swept collision prevents fast projectiles tunnelling through the player or walls.
public sealed class EnemyProjectile : MonoBehaviour
{
    private Transform owner;
    private Vector3 velocity;
    private float damage, slow, remaining = 6f;
    public void Launch(Transform shooter, Vector3 direction, float speed, float hitDamage, float slowSeconds)
    { owner = shooter; velocity = direction.normalized * speed; damage = hitDamage; slow = slowSeconds; }
    private void Update()
    {
        float distance = velocity.magnitude * Time.deltaTime;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.07f, velocity.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var player = nearest.Value.collider.GetComponentInParent<PlayerVitals>();
            if (player != null) player.ReceiveHit(damage, slow);
            Destroy(gameObject); return;
        }
        transform.position += velocity * Time.deltaTime;
        remaining -= Time.deltaTime;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
