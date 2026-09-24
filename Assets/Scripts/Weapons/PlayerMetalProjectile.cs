using UnityEngine;

public sealed class PlayerMetalProjectile : MonoBehaviour
{
    private Transform owner;
    private PlayerWeaponSystem weapons;
    private Vector3 velocity;
    private float lifetime = 4f;
    public void Launch(Transform shooter, PlayerWeaponSystem source, Vector3 direction)
    { owner = shooter; weapons = source; velocity = direction.normalized * 25f; }
    private void Update()
    {
        float step = velocity.magnitude * Time.deltaTime;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.04f, velocity.normalized, step, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(owner)) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            var enemy = nearest.Value.collider.GetComponentInParent<EnemyBrain>();
            if (enemy != null && weapons != null) weapons.HitWithMetal(enemy);
            Destroy(gameObject); return;
        }
        transform.position += velocity * Time.deltaTime;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) Destroy(gameObject);
    }
}
