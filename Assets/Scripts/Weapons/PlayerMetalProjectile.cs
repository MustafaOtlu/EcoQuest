using UnityEngine;

public sealed class PlayerMetalProjectile : MonoBehaviour
{
    private Transform owner;
    private PlayerWeaponSystem weapons;
    private Vector3 velocity;
    // GDD §11: iron balls reach 12 m (25 m/s for 0.48 s), then drop out of play.
    public const float Range = 12f, Speed = 25f;
    private float lifetime = Range / Speed;
    public void Launch(Transform shooter, PlayerWeaponSystem source, Vector3 direction)
    {
        owner = shooter; weapons = source; velocity = direction.normalized * Speed;
        WeaponProjectileVisual.Attach(gameObject, source != null ? source.effectMaterial : null, true);
    }
    private void Update()
    {
        float step = velocity.magnitude * Time.deltaTime;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.04f, velocity.normalized, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var focus = nearest.Value.collider.GetComponentInParent<EcoFinaleFocus>();
            if (focus != null) { focus.TryHitMetal(); Destroy(gameObject); return; }
            var enemy = nearest.Value.collider.GetComponentInParent<EnemyBrain>();
            if (enemy != null && weapons != null) weapons.HitWithMetal(enemy);
            Destroy(gameObject); return;
        }
        transform.position += velocity * Time.deltaTime;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }
}
