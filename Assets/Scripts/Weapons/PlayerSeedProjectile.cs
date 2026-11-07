using UnityEngine;

public sealed class PlayerSeedProjectile : MonoBehaviour
{
    private Transform owner;
    private PlayerWeaponSystem weapons;
    private WeaponProjectileVisual visual;
    private Vector3 velocity;
    private EcoCropCatalog.Kind seedKind;
    private float life = 4f;
    public void Launch(Transform shooter, PlayerWeaponSystem source, Vector3 direction)
    {
        owner = shooter; weapons = source; seedKind = source != null ? source.SelectedSeed : EcoCropCatalog.Kind.Cabbage;
        velocity = direction.normalized * (source != null && source.Upgrades != null ? source.Upgrades.SeedVelocity : 18f);
        visual = WeaponProjectileVisual.Attach(gameObject, velocity, WeaponProjectileVisual.Shot.Seed);
    }
    private void Update()
    {
        float delta = Time.deltaTime;
        if (delta <= 0f) return;
        Vector3 step = velocity * delta;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.025f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            bool planted = weapons != null && weapons.TryPlant(nearest.Value, seedKind);
            WeaponProjectileVisual.Contact(nearest.Value, transform.position, velocity, WeaponProjectileVisual.Shot.Seed, planted);
            if (visual != null) visual.EndWake();
            Destroy(gameObject); return;
        }
        transform.position += step;
        velocity += Vector3.down * (3f * delta);
        life -= delta;
        if (life <= 0f)
        {
            if (visual != null) visual.EndWake();
            Destroy(gameObject);
        }
    }
}
