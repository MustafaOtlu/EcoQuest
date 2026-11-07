using UnityEngine;

// Swept collision prevents fast projectiles tunnelling through the player or walls.
public sealed class EnemyProjectile : MonoBehaviour
{
    private Transform owner;
    private Vector3 velocity;
    private float damage, slow, remaining = 6f;
    private EnemyBrain.Species? species;
    private WeaponProjectileVisual.Shot shot;
    public void Launch(Transform shooter, Vector3 direction, float speed, float hitDamage, float slowSeconds, EnemyBrain.Species? sourceSpecies = null)
    {
        owner = shooter;
        var finaleActor = shooter != null ? shooter.GetComponentInParent<EcoFinaleActor>() : null;
        if (finaleActor != null) finaleActor.AttachTransient(gameObject);
        species = sourceSpecies ?? (shooter != null && shooter.TryGetComponent<EnemyBrain>(out var enemy) ? enemy.species : (EnemyBrain.Species?)null);
        if (!EcoRegion.Valid(speed) || !EcoRegion.Finite(hitDamage) || hitDamage < 0 || !EcoRegion.Finite(slowSeconds)
            || !EcoRegion.Finite(direction.x) || !EcoRegion.Finite(direction.y) || !EcoRegion.Finite(direction.z) || direction.sqrMagnitude < 0.001f)
        { enabled = false; Destroy(gameObject); return; }
        velocity = direction.normalized * speed; damage = hitDamage; slow = Mathf.Max(0, slowSeconds); remaining = 6f;
        shot = species switch
        {
            EnemyBrain.Species.Flame => WeaponProjectileVisual.Shot.Ember,
            EnemyBrain.Species.Acid => WeaponProjectileVisual.Shot.Acid,
            EnemyBrain.Species.Smoke => WeaponProjectileVisual.Shot.Smoke,
            _ => WeaponProjectileVisual.Shot.Rivet
        };
        WeaponProjectileVisual.Attach(gameObject, velocity, shot);
    }
    private void Update()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        float distance = velocity.magnitude * delta;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.07f, velocity.normalized, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var player = nearest.Value.collider.GetComponentInParent<PlayerVitals>();
            if (player != null && player.isActiveAndEnabled) player.ReceiveHit(species == EnemyBrain.Species.Smoke ? 0 : damage, slow, species == EnemyBrain.Species.Smoke ? 2.5f : 0);
            else if (species.HasValue) EnemyEcoTarget.From(nearest.Value.collider, species.Value)?.ApplyImpact(species.Value, damage);
            WeaponProjectileVisual.Contact(nearest.Value, transform.position, velocity, shot, player != null, transform.parent);
            Destroy(gameObject); return;
        }
        transform.position += velocity * delta;
        remaining -= delta;
        if (remaining <= 0f) Destroy(gameObject);
    }
}
