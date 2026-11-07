using UnityEngine;

public sealed class PlayerMetalProjectile : MonoBehaviour
{
    private Transform owner;
    private PlayerWeaponSystem weapons;
    private WeaponProjectileVisual visual;
    private Vector3 velocity;
    // GDD §11: iron balls reach 12 m (25 m/s for 0.48 s), then drop out of play.
    public const float Range = 12f, Speed = 25f;
    private float lifetime = Range / Speed;
    public void Launch(Transform shooter, PlayerWeaponSystem source, Vector3 direction)
    {
        owner = shooter; weapons = source; velocity = direction.normalized * Speed;
        visual = WeaponProjectileVisual.Attach(gameObject, velocity, WeaponProjectileVisual.Shot.Iron);
    }
    private void Update()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        float step = velocity.magnitude * delta;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.04f, velocity.normalized, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var focus = nearest.Value.collider.GetComponentInParent<EcoFinaleFocus>();
            bool accepted;
            if (focus != null) accepted = focus.TryHitMetal();
            else
            {
                var enemy = nearest.Value.collider.GetComponentInParent<EnemyBrain>();
                accepted = enemy != null && weapons != null;
                if (accepted) weapons.HitWithMetal(enemy);
            }
            WeaponProjectileVisual.Contact(nearest.Value, transform.position, velocity, WeaponProjectileVisual.Shot.Iron, accepted);
            if (visual != null) visual.EndWake();
            Destroy(gameObject); return;
        }
        transform.position += velocity * delta;
        lifetime -= delta;
        if (lifetime <= 0f)
        {
            if (visual != null) visual.EndWake();
            Destroy(gameObject);
        }
    }
}
