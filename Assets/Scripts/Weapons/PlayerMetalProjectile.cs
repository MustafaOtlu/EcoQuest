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
    /// <summary>Where the ray from <paramref name="from"/> enters an upright capsule within <paramref name="maxDistance"/>
    /// (sampled finely along the step: shots move under a metre per frame).</summary>
    internal static bool HitCapsule(Vector3 from, Vector3 dir, float maxDistance, Vector3 bottom, float radius, float height, out float distance)
    {
        distance = 0f;
        float top = bottom.y + height;
        int samples = Mathf.Clamp(Mathf.CeilToInt(maxDistance / 0.05f), 1, 64);
        for (int i = 0; i <= samples; i++)
        {
            float d = maxDistance * i / samples;
            var p = from + dir * d;
            float y = Mathf.Clamp(p.y, bottom.y + radius, Mathf.Max(bottom.y + radius, top - radius));
            var axis = new Vector3(bottom.x, y, bottom.z);
            if ((p - axis).sqrMagnitude <= radius * radius) { distance = d; return true; }
        }
        return false;
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
        // The enemies' visible bodies are wider than their walking capsules: test those first (GDD v5 §21 "Geri bildirim 18").
        var direction = velocity.normalized;
        float limit = nearest.HasValue ? nearest.Value.distance : step;
        EnemyBrain struck = null;
        foreach (var candidate in EnemyBrain.Active)
        {
            if (candidate == null || candidate.State == EnemyBrain.Behaviour.Dead) continue;
            if (owner != null && candidate.transform.IsChildOf(owner)) continue;
            candidate.ShotCapsule(out var bottom, out float radius, out float height);
            if (HitCapsule(transform.position, direction, limit, bottom, radius + 0.04f, height, out float along))
            { limit = along; struck = candidate; }
        }
        if (struck != null)
        {
            Vector3 point = transform.position + direction * limit;
            // Gone first: nothing below may leave the ball flying on through the body.
            if (visual != null) visual.EndWake();
            Destroy(gameObject);
            if (weapons != null) weapons.HitWithMetal(struck);
            var normal = Vector3.ProjectOnPlane(point - struck.transform.position, Vector3.up);
            var contact = new RaycastHit { point = point, distance = Mathf.Max(0.001f, limit), normal = normal.sqrMagnitude > 1e-4f ? normal.normalized : -direction };
            WeaponProjectileVisual.Contact(contact, point, velocity, WeaponProjectileVisual.Shot.Iron, weapons != null);
            return;
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
