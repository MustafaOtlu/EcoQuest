using UnityEngine;

// Target intent is measured on the ground, independently of a muzzle ray hitting the floor first.
// Every assisted action still needs player reach and a clear physical path to the object.
public sealed class ToolAreaTargeting
{
    public struct ResourcePick
    {
        public RecyclableResource resource;
        public Collider collider;
        public Vector3 point;
    }

    private Collider[] nearby = new Collider[128];
    private RaycastHit[] sight = new RaycastHit[64];
    private readonly Vector3[] corners = new Vector3[8];

    private int Query(Vector3 position, float range)
    {
        int count = Physics.OverlapSphereNonAlloc(position, range, nearby, ~(1 << 2), QueryTriggerInteraction.Ignore);
        while (count == nearby.Length && nearby.Length < 2048)
        {
            System.Array.Resize(ref nearby, nearby.Length * 2);
            count = Physics.OverlapSphereNonAlloc(position, range, nearby, ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        return count;
    }

    public bool TryResource(Transform owner, Vector3 origin, Vector3 forward, float range, float halfAngle,
        RecyclableResource current, int allowedRecyclerRank, out ResourcePick result)
    {
        result = default;
        if (owner == null || range <= 0) return false;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.1f) forward = owner.forward;
        int count = Query(owner.position + Vector3.up, range + 1f);
        float best = float.PositiveInfinity, spread = Mathf.Tan(halfAngle * Mathf.Deg2Rad);
        for (int i = 0; i < count; i++)
        {
            Collider collider = nearby[i];
            if (collider == null || !collider.enabled || collider.transform.IsChildOf(owner)) continue;
            var resource = collider.GetComponentInParent<RecyclableResource>();
            if (resource == null || !resource.isActiveAndEnabled || resource.Claimed
                || resource.requiredRecyclerRank > allowedRecyclerRank) continue;
            Bounds bounds = collider.bounds;
            if (Mathf.Abs(bounds.center.y - owner.position.y) > 3f) continue;
            Vector3 nearest = bounds.ClosestPoint(new Vector3(owner.position.x, bounds.center.y, owner.position.z));
            Vector3 reach = Vector3.ProjectOnPlane(nearest - owner.position, Vector3.up);
            if (reach.sqrMagnitude > range * range) continue;
            Vector3 to = Vector3.ProjectOnPlane(bounds.center - owner.position, Vector3.up);
            float along = Vector3.Dot(to, forward);
            float lateral = (to - forward * along).magnitude;
            float footprint = Mathf.Min(1.5f, new Vector2(bounds.extents.x, bounds.extents.z).magnitude);
            if (along < -footprint || lateral > Mathf.Max(0.25f, along * spread) + footprint) continue;
            float score = lateral / Mathf.Max(0.5f, along * spread + footprint) * 2f + reach.magnitude / range * 0.2f;
            if (resource == current) score -= 0.4f; // Keep processing stable until it is claimed or leaves the area.
            if (score >= best || !HasClearPath(owner, origin, resource.transform, bounds.center, true)) continue;
            best = score;
            result = new ResourcePick { resource = resource, collider = collider, point = bounds.center };
        }
        return result.resource != null;
    }

    public EnemyBrain EnemyAtCursor(Transform owner, Camera camera, Vector2 pointer, float searchRange)
    {
        if (owner == null || camera == null) return null;
        // A real body hit wins. Nearby screen-space tolerance also accepts clicks just beside a silhouette.
        Ray ray = camera.ScreenPointToRay(pointer);
        var hits = Physics.RaycastAll(ray, 400f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        float firstDistance = float.PositiveInfinity; Collider first = null;
        foreach (var hit in hits)
            if (!hit.transform.IsChildOf(owner) && hit.distance < firstDistance) { first = hit.collider; firstDistance = hit.distance; }
        if (first != null)
        {
            var direct = first.GetComponentInParent<EnemyBrain>();
            if (Alive(direct)) return direct;
            if (first.GetComponentInParent<RecyclableResource>() != null || first.GetComponentInParent<PlantedSeed>() != null
                || first.GetComponentInParent<EcoFire>() != null || first.GetComponentInParent<EcoWaterNode>() != null
                || first.GetComponentInParent<WeaponWaterSource>() != null || first.GetComponentInParent<EcoFinaleFocus>() != null) return null;
        }
        int count = Query(owner.position + Vector3.up, searchRange + 1f);
        EnemyBrain best = null;
        float tolerance = Mathf.Clamp(camera.pixelHeight * 0.035f, 24f, 54f);
        float bestScore = tolerance * tolerance;
        for (int i = 0; i < count; i++)
        {
            Collider collider = nearby[i];
            if (collider == null) continue;
            var enemy = collider.GetComponentInParent<EnemyBrain>();
            if (!Alive(enemy)) continue;
            Rect rect;
            if (!ScreenBounds(camera, collider.bounds, out rect)) continue;
            Vector2 nearest = new Vector2(Mathf.Clamp(pointer.x, rect.xMin, rect.xMax), Mathf.Clamp(pointer.y, rect.yMin, rect.yMax));
            float score = (pointer - nearest).sqrMagnitude;
            if (score >= bestScore) continue;
            best = enemy; bestScore = score;
        }
        return best;
    }

    private bool ScreenBounds(Camera camera, Bounds bounds, out Rect rect)
    {
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = -min;
        for (int i = 0; i < corners.Length; i++)
        {
            corners[i] = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 screen = camera.WorldToScreenPoint(corners[i]);
            if (screen.z <= camera.nearClipPlane) { rect = default; return false; }
            min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
        }
        rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y); return true;
    }

    public bool HasClearPath(Transform owner, Vector3 origin, Transform target, Vector3 point, bool looseResources = false)
    {
        Vector3 delta = point - origin;
        float distance = delta.magnitude;
        if (distance < 0.05f) return true;
        int count = Physics.RaycastNonAlloc(origin, delta / distance, sight, Mathf.Max(0, distance - 0.03f),
            ~(1 << 2), QueryTriggerInteraction.Ignore);
        while (count == sight.Length && sight.Length < 1024)
        {
            System.Array.Resize(ref sight, sight.Length * 2);
            count = Physics.RaycastNonAlloc(origin, delta / distance, sight, Mathf.Max(0, distance - 0.03f),
                ~(1 << 2), QueryTriggerInteraction.Ignore);
        }
        for (int i = 0; i < count; i++)
        {
            Transform hit = sight[i].transform;
            if (hit == null || (owner != null && hit.IsChildOf(owner)) || (target != null && hit.IsChildOf(target))) continue;
            if (looseResources && hit.GetComponentInParent<RecyclableResource>() != null) continue;
            return false;
        }
        return true;
    }

    public static bool Alive(EnemyBrain enemy) => enemy != null && enemy.isActiveAndEnabled
        && enemy.State != EnemyBrain.Behaviour.Dead && enemy.Health > 0 && !enemy.FilterInstalled;

    public static Vector3 EnemyPoint(EnemyBrain enemy)
    {
        if (enemy == null) return Vector3.zero;
        var body = enemy.GetComponent<CharacterController>();
        return body != null && body.enabled ? body.bounds.center : enemy.transform.position + Vector3.up * 0.7f;
    }
}
