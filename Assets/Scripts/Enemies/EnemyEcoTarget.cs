using UnityEngine;
using UnityEngine.AI;

// A snapshot of one ecological target; an attack retains this descriptor through its windup.
public sealed class EnemyEcoTarget
{
    public MonoBehaviour Component { get; }
    public Vector3 ApproachPosition { get; private set; }
    private readonly Collider shape;
    private readonly int priority;
    private EnemyEcoTarget(MonoBehaviour component, Collider collider, int rank)
    { Component = component; shape = collider; priority = rank; }

    public bool IsAlive => Component != null && Component.isActiveAndEnabled && shape != null && shape.enabled && shape.gameObject.activeInHierarchy
        && (Component is not EcoStructure structure || structure.integrity > 0)
        && (Component is not EcoPowerNode power || power.Operational)
        && (Component is not EcoWaterNode water || water.Operational)
        && (Component is not EcoVegetation tree || tree.health > 0)
        && (Component is not PlantedSeed crop || !crop.IsWithered);

    public Vector3 AimPoint(Vector3 origin) => shape != null ? shape.ClosestPoint(origin) : Component != null ? Component.transform.position : origin;
    public bool VisibleFrom(Vector3 origin, Transform observer)
    {
        if (!IsAlive) return false;
        Vector3 delta = AimPoint(origin) - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if ((observer == null || !hit.transform.IsChildOf(observer)) && !hit.transform.IsChildOf(Component.transform)) return false;
        return true;
    }

    public static EnemyEcoTarget From(Collider hit, EnemyBrain.Species species)
    {
        if (hit == null || !hit.enabled || hit.isTrigger) return null;
        MonoBehaviour component = null; int rank = 60;
        if (species == EnemyBrain.Species.Smoke)
        {
            var power = hit.GetComponentInParent<EcoPowerNode>();
            if (power != null && power.kind == EcoPowerNode.Kind.Solar) component = power;
        }
        else if (species == EnemyBrain.Species.Slime)
        {
            var source = hit.GetComponentInParent<WeaponWaterSource>();
            var water = hit.GetComponentInParent<EcoWaterNode>();
            var crop = hit.GetComponentInParent<PlantedSeed>();
            if (source != null && source.useRegionalQuality) { component = source; rank = 100; }
            else if (water != null && water.kind == EcoWaterNode.Kind.Purifier) { component = water; rank = 80; }
            else if (crop != null) component = crop;
        }
        else
        {
            if (species == EnemyBrain.Species.Flame) component = hit.GetComponentInParent<EcoVegetation>();
            if (species == EnemyBrain.Species.Acid)
            {
                component = hit.GetComponentInParent<PlantedSeed>();
                if (component != null) rank = 80;
            }
            if (component == null) component = hit.GetComponentInParent<EcoStructure>();
        }
        if (component == null) return null;
        var result = new EnemyEcoTarget(component, hit, rank);
        return result.IsAlive ? result : null;
    }

    public static EnemyEcoTarget Select(EnemyBrain.Species species, Vector3 home, float leash, Vector3 origin, Vector3 eye, float range, Transform observer)
    {
        EnemyEcoTarget best = null; float bestScore = float.NegativeInfinity;
        // The caller throttles this broad-phase query; this also excludes targets without a usable collider.
        foreach (var hit in Physics.OverlapSphere(home, Mathf.Max(0, leash), ~0, QueryTriggerInteraction.Ignore))
        {
            var candidate = From(hit, species);
            if (candidate == null) continue;
            float distance = Vector3.Distance(origin, candidate.AimPoint(origin));
            float score = candidate.priority * 1000 - distance;
            if (score <= bestScore || !candidate.FindApproach(origin, eye, range, observer)
                || (candidate.ApproachPosition - home).sqrMagnitude > leash * leash) continue;
            best = candidate; bestScore = score;
        }
        return best;
    }

    private bool FindApproach(Vector3 origin, Vector3 eye, float range, Transform observer)
    {
        if (Vector3.Distance(eye, AimPoint(eye)) <= range && VisibleFrom(eye, observer))
        { ApproachPosition = origin; return true; }
        var bounds = shape.bounds;
        Vector3 outward = origin - bounds.center; outward.y = 0;
        if (outward.sqrMagnitude < 0.01f) outward = Vector3.forward;
        outward.Normalize();
        bool navigationReady = EcoNavigation.Current != null && EcoNavigation.Current.Ready;
        if (EcoNavigation.Current != null && !navigationReady) return false;
        var path = navigationReady ? new NavMeshPath() : null;
        if (navigationReady && !NavMesh.SamplePosition(origin, out _, 1.5f, NavMesh.AllAreas)) return false;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0, i * 45, 0) * outward;
            // A point beyond the collider edge keeps ponds and solid buildings out of the route itself.
            float edge = Mathf.Min(Mathf.Abs(direction.x) > 0.001f ? bounds.extents.x / Mathf.Abs(direction.x) : float.MaxValue,
                Mathf.Abs(direction.z) > 0.001f ? bounds.extents.z / Mathf.Abs(direction.z) : float.MaxValue);
            Vector3 point = bounds.center + direction * (edge + 0.7f); point.y = origin.y;
            if (navigationReady)
            {
                if (!NavMesh.SamplePosition(point, out var end, 0.6f, NavMesh.AllAreas)
                    || !NavMesh.SamplePosition(origin, out var start, 1.5f, NavMesh.AllAreas)
                    || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
                    || path.status != NavMeshPathStatus.PathComplete) continue;
                point = end.position;
            }
            else if (!VisibleFrom(eye, observer)) continue;
            Vector3 futureEye = point + Vector3.up * (eye.y - origin.y);
            if (!VisibleFrom(futureEye, observer) || Vector3.Distance(futureEye, AimPoint(futureEye)) > range + 0.3f) continue;
            ApproachPosition = point; return true;
        }
        return false;
    }

    public bool ApplyImpact(EnemyBrain.Species species, float damage)
    {
        if (!IsAlive || !EcoRegion.Finite(damage) || damage < 0) return false;
        damage = Mathf.Min(damage, 25);
        if (species == EnemyBrain.Species.Smoke && Component is EcoPowerNode solar && solar.kind == EcoPowerNode.Kind.Solar)
        { solar.ApplySmoke(); return true; }
        if (species == EnemyBrain.Species.Slime)
        {
            if (Component is WeaponWaterSource source && source.useRegionalQuality)
            {
                source.Pollute(40);
                if (source.TryGetComponent<EcoFishingSpot>(out var fish)) fish.Poison(1);
                return true;
            }
            if (Component is EcoWaterNode water && water.kind == EcoWaterNode.Kind.Purifier) { water.ApplySlime(); return true; }
            if (Component is PlantedSeed crop) { crop.ApplySlime(); return true; }
            return false;
        }
        if (damage <= 0) return false;
        if (species == EnemyBrain.Species.Acid && Component is PlantedSeed plant) { plant.ApplyAcid(damage / 100); return true; }
        if (species == EnemyBrain.Species.Flame && Component is EcoVegetation tree)
        { tree.Burn(damage / 100); Ignite(tree.transform.position); return true; }
        if ((species == EnemyBrain.Species.Tin || species == EnemyBrain.Species.Flame || species == EnemyBrain.Species.Acid)
            && Component is EcoStructure structure)
        {
            structure.ReceiveDamage(damage);
            if (species == EnemyBrain.Species.Flame) Ignite(shape.bounds.center + Vector3.up * shape.bounds.extents.y);
            return true;
        }
        return false;
    }

    private static void Ignite(Vector3 position)
    {
        foreach (var existing in Object.FindObjectsByType<EcoFire>(FindObjectsSortMode.None))
            if (Vector3.Distance(existing.transform.position, position) < 2.5f) return;
        var go = new GameObject("Enemy fire"); go.transform.position = position + Vector3.up * 0.15f;
        var collider = go.AddComponent<SphereCollider>(); collider.radius = 0.22f;
        EcoNavigation.Ignore(go);
        var fire = go.AddComponent<EcoFire>(); fire.fuel = 6; fire.intensity = 0.65f; fire.radius = 1.2f;
        var particles = go.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = Resources.Load<Material>("EcoEnvironment/Particle");
        var main = particles.main; main.startColor = new Color(1, 0.35f, 0.05f); main.startLifetime = 0.7f; main.startSpeed = 0.7f; main.startSize = 0.2f;
        var emission = particles.emission; emission.rateOverTime = 12;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = 0.18f;
        particles.transform.rotation = Quaternion.Euler(-90, 0, 0); fire.flames = particles; particles.Play();
        Object.Destroy(go, 18); // Neither repeated hits nor extinguished residue can accumulate indefinitely.
    }
}
