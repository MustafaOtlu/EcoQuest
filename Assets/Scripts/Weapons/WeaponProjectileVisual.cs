using UnityEngine;
using UnityEngine.Rendering;

// A short trail of separate beads follows the actual projectile, never the aim ray.
public sealed class WeaponProjectileVisual : MonoBehaviour
{
    private ParticleSystem beads;
    private bool metal;
    private float emission;
    private Vector3 previousPosition;

    public static void Attach(GameObject projectile, Material material, bool isMetal)
    {
        var visual = projectile.AddComponent<WeaponProjectileVisual>(); visual.metal = isMetal;
        var renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            if (material != null) renderer.sharedMaterial = material;
            var tint = new MaterialPropertyBlock();
            var color = isMetal ? new Color(0.65f, 0.74f, 0.82f) : new Color(0.6f, 0.9f, 0.16f);
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); renderer.SetPropertyBlock(tint);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        visual.beads = WeaponVisualEffects.CreateParticles(isMetal ? "Metal flight sparks" : "Seed flight motes", projectile.transform, 24);
        visual.previousPosition = projectile.transform.position;
    }

    private void LateUpdate()
    {
        if (beads == null) return;
        Vector3 current = transform.position, step = current - previousPosition;
        emission += Time.deltaTime * 65;
        int count = Mathf.Min(6, Mathf.FloorToInt(emission)); emission -= count;
        for (int i = 0; i < count; i++)
        {
            var particle = new ParticleSystem.EmitParams
            {
                position = Vector3.Lerp(previousPosition, current, (i + 1f) / Mathf.Max(1, count)),
                velocity = Vector3.zero, startLifetime = metal ? 0.065f : 0.11f,
                startSize = metal ? 0.035f : 0.045f,
                startColor = metal ? new Color(1, 0.8f, 0.45f, 0.8f) : new Color(0.6f, 1, 0.25f, 0.75f)
            };
            beads.Emit(particle, 1);
        }
        if (step.sqrMagnitude > 0.00001f) transform.rotation = Quaternion.LookRotation(step);
        previousPosition = current;
    }
}
