using UnityEngine;

public sealed class PlayerSeedProjectile : MonoBehaviour
{
    private Transform owner;
    private PlayerWeaponSystem weapons;
    private Vector3 velocity;
    private float life = 4f;
    public void Launch(Transform shooter, PlayerWeaponSystem source, Vector3 direction)
    { owner = shooter; weapons = source; velocity = direction.normalized * 18f; }
    private void Update()
    {
        Vector3 step = velocity * Time.deltaTime;
        RaycastHit? nearest = null;
        foreach (var hit in Physics.SphereCastAll(transform.position, 0.025f, step.normalized, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || (owner != null && hit.transform.IsChildOf(owner))) continue;
            if (!nearest.HasValue || hit.distance < nearest.Value.distance) nearest = hit;
        }
        if (nearest.HasValue)
        {
            if (weapons != null) weapons.TryPlant(nearest.Value);
            Destroy(gameObject); return;
        }
        transform.position += step;
        velocity += Vector3.down * (3f * Time.deltaTime);
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}
