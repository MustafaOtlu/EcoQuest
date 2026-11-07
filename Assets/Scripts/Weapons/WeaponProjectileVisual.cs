using UnityEngine;
using UnityEngine.Rendering;

// Art follows the swept projectile. No renderer or effect adds a collider or changes the shot's trajectory.
public sealed class WeaponProjectileVisual : MonoBehaviour
{
    internal enum Shot { Seed, Iron, Rivet, Ember, Acid, Smoke }

    private static readonly Color Olive = new Color32(118, 143, 52, 255);
    private static readonly Color Husk = new Color32(230, 195, 108, 255);
    private static readonly Color Steel = new Color32(77, 104, 119, 255);
    private static readonly Color SteelEdge = new Color32(179, 208, 216, 255);
    private static readonly Color Copper = new Color32(166, 104, 62, 255);
    private static readonly Color Ember = new Color32(230, 90, 42, 255);
    private static readonly Color EmberHeart = new Color32(255, 198, 99, 255);
    private static readonly Color Acid = new Color32(124, 159, 45, 255);
    private static readonly Color AcidGlint = new Color32(210, 227, 111, 255);
    private static readonly Color Smoke = new Color32(114, 128, 136, 255);

    private static Material litMaterial;
    private static Mesh hull, tapered, shard;
    private Transform body;
    private ParticleSystem wake;
    private Vector3 previousPosition;
    private Quaternion heading;
    private Shot shot;
    private float roll, wakeDistance;

    internal static WeaponProjectileVisual Attach(GameObject projectile, Vector3 direction, Shot shot)
    {
        var visual = projectile.AddComponent<WeaponProjectileVisual>();
        visual.shot = shot;
        visual.previousPosition = projectile.transform.position;
        visual.heading = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : projectile.transform.rotation;
        visual.BuildBody();
        if (shot == Shot.Seed || shot == Shot.Iron)
            visual.wake = CreateFragments(shot == Shot.Seed ? "Seed flight motes" : "Iron flight glints", projectile.transform, 18);
        return visual;
    }

    private void BuildBody()
    {
        if (litMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            litMaterial = new Material(shader) { name = "Eco projectile surfaces" };
            litMaterial.EnableKeyword("_EMISSION");
            litMaterial.SetFloat("_Smoothness", 0.24f);
        }
        EnsureMeshes();
        foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
        body = new GameObject("Projectile silhouette").transform;
        body.SetParent(transform, false);
        Vector3 scale = transform.lossyScale;
        body.localScale = new Vector3(1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
        body.rotation = heading;

        switch (shot)
        {
            case Shot.Seed:
                Part("Faceted seed", tapered, Vector3.zero, new Vector3(0.13f, 0.10f, 0.22f), Olive);
                Part("Seed seam", hull, new Vector3(0f, 0.041f, 0.004f), new Vector3(0.047f, 0.034f, 0.17f), Husk);
                break;
            case Shot.Iron:
            case Shot.Rivet:
                bool rivet = shot == Shot.Rivet;
                float width = rivet ? 0.18f : 0.15f;
                float length = rivet ? 0.23f : 0.16f;
                Part("Iron core", hull, Vector3.zero, new Vector3(width, width, length), Steel);
                Part("Machined collar", hull, new Vector3(0f, 0f, 0.015f), new Vector3(width * 1.06f, width * 1.06f, length * 0.44f),
                    rivet ? Copper : SteelEdge);
                break;
            case Shot.Ember:
                Part("Ember shell", tapered, new Vector3(0f, 0f, -0.022f), new Vector3(0.23f, 0.20f, 0.35f), Ember, 0.18f);
                Part("Hot facet", hull, new Vector3(0.026f, 0.04f, 0.064f), new Vector3(0.145f, 0.144f, 0.14f), EmberHeart, 0.33f);
                Part("Ember tail", tapered, new Vector3(-0.05f, 0.015f, -0.13f), new Vector3(0.075f, 0.071f, 0.17f), Copper, 0.10f);
                break;
            case Shot.Acid:
                Part("Acid droplet", tapered, Vector3.zero, new Vector3(0.21f, 0.18f, 0.30f), Acid, 0.13f);
                Part("Droplet glint", hull, new Vector3(0.031f, 0.052f, 0.042f), new Vector3(0.089f, 0.074f, 0.11f), AcidGlint, 0.11f);
                Part("Acid bead", hull, new Vector3(-0.063f, 0.033f, -0.118f), Vector3.one * 0.062f, AcidGlint);
                break;
            case Shot.Smoke:
                Part("Smoke core", hull, Vector3.zero, new Vector3(0.24f, 0.21f, 0.25f), Smoke);
                Part("Smoke lobe", hull, new Vector3(0.074f, 0.012f, -0.072f), Vector3.one * 0.17f, Steel);
                break;
        }
    }

    private void Part(string label, Mesh mesh, Vector3 at, Vector3 dimensions, Color tint, float glow = 0.06f)
    {
        var part = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(body, false);
        part.transform.localPosition = at;
        part.transform.localScale = dimensions;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = litMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var colors = new MaterialPropertyBlock();
        colors.SetColor("_BaseColor", tint);
        colors.SetColor("_Color", tint);
        colors.SetColor("_EmissionColor", tint * glow);
        renderer.SetPropertyBlock(colors);
    }

    private void LateUpdate()
    {
        Vector3 current = transform.position;
        Vector3 step = current - previousPosition;
        previousPosition = current;
        if (step.sqrMagnitude < 0.000001f) return;
        heading = Quaternion.LookRotation(step);
        if (body != null)
        {
            roll += step.magnitude * (shot == Shot.Seed ? 145f : 110f);
            body.rotation = heading * Quaternion.AngleAxis(roll, Vector3.forward);
        }
        if (wake == null) return;

        float spacing = shot == Shot.Seed ? 0.23f : 0.34f;
        float distance = step.magnitude;
        float travelled = wakeDistance + distance;
        int passed = Mathf.FloorToInt(travelled / spacing);
        float carry = wakeDistance;
        wakeDistance = travelled - passed * spacing;
        Color color = shot == Shot.Seed ? Husk : SteelEdge;
        color.a = 0.68f;
        for (int i = Mathf.Max(1, passed - 3); i <= passed; i++)
        {
            float t = Mathf.Clamp01((i * spacing - carry) / distance);
            Emit(wake, current - step * (1f - t), Vector3.zero, color,
                shot == Shot.Seed ? 0.095f : 0.07f, shot == Shot.Seed ? 0.075f : 0.083f, heading.eulerAngles);
        }
    }

    internal void EndWake()
    {
        if (wake == null) return;
        wake.transform.SetParent(null, true);
        Destroy(wake.gameObject, 0.14f);
        wake = null;
    }

    internal static void Contact(RaycastHit hit, Vector3 from, Vector3 direction, Shot shot, bool accepted, Transform scope = null)
    {
        Vector3 normal = hit.normal.sqrMagnitude > 0.01f ? hit.normal.normalized : -direction.normalized;
        if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
        // An overlap at the start of a sweep may not report a world-space hit point.
        Vector3 point = hit.distance > 0f ? hit.point : hit.collider.ClosestPoint(from);
        var host = new GameObject("Projectile contact • " + shot);
        if (scope != null) host.transform.SetParent(scope, false);
        host.transform.SetPositionAndRotation(point + normal * 0.04f, Quaternion.identity);
        var particles = CreateFragments("Surface flecks", host.transform, 16);
        var physics = particles.main;
        physics.gravityModifier = shot == Shot.Smoke ? -0.12f : shot == Shot.Acid ? 0.8f : 0.53f;
        Vector3 side = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.96f ? Vector3.up : Vector3.forward).normalized;
        Vector3 across = Vector3.Cross(normal, side);
        Vector3 reflected = Vector3.Reflect(direction.normalized, normal);
        var (primary, secondary, count, force) = ContactPalette(shot, accepted);
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f;
            Vector3 radial = side * Mathf.Cos(angle) + across * Mathf.Sin(angle);
            float variation = Mathf.Repeat(i * 0.381966f, 1f);
            Vector3 velocity = (radial * Mathf.Lerp(0.38f, 0.8f, variation) + normal * 0.55f + reflected * 0.28f).normalized
                * force * Mathf.Lerp(0.48f, 1f, variation);
            Emit(particles, host.transform.position + radial * 0.018f, velocity,
                i % 3 == 0 ? secondary : primary, Mathf.Lerp(0.14f, 0.32f, variation),
                Mathf.Lerp(0.06f, 0.12f, variation), Quaternion.LookRotation(velocity).eulerAngles);
        }
        Destroy(host, 0.37f);
    }

    private static (Color, Color, int, float) ContactPalette(Shot shot, bool accepted)
    {
        var soil = new Color32(142, 112, 78, 255);
        return shot switch
        {
            Shot.Seed => accepted ? (Olive, Husk, 8, 1.25f) : (soil, Husk, 4, 1.1f),
            Shot.Iron => accepted ? (EmberHeart, SteelEdge, 10, 2.9f) : (SteelEdge, Copper, 6, 2f),
            Shot.Rivet => (Copper, SteelEdge, 6, 2.35f),
            Shot.Ember => (Ember, EmberHeart, 8, 1.95f),
            Shot.Acid => (Acid, AcidGlint, 8, 1.65f),
            _ => (Smoke, Steel, 5, 1.2f)
        };
    }

    private static ParticleSystem CreateFragments(string label, Transform parent, int limit)
    {
        EnsureMeshes();
        var particles = WeaponVisualEffects.CreateParticles(label, parent, limit);
        var main = particles.main;
        main.startRotation3D = true;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = shard;
        return particles;
    }

    private static void Emit(ParticleSystem particles, Vector3 at, Vector3 velocity, Color color, float life, float size, Vector3 rotation)
    {
        particles.Emit(new ParticleSystem.EmitParams
        {
            position = at, velocity = velocity, startColor = color, startLifetime = life, startSize = size, rotation3D = rotation
        }, 1);
    }

    private static void EnsureMeshes()
    {
        if (hull != null) return;
        hull = MakeFacets("Bevelled shot hull", 7, 0.30f, 0.34f, 0.33f);
        tapered = MakeFacets("Tapered shot hull", 7, 0.31f, 0.22f, 0.42f);
        shard = MakeFacets("Shot fragment", 4, 0.43f, 0.1f, 0.24f);
    }

    // Vertices are independent per triangle for crisp facet lighting at gameplay scale.
    private static Mesh MakeFacets(string label, int sides, float shoulder, float tailRadius, float noseRadius)
    {
        var positions = new Vector3[sides * 12];
        var uv = new Vector2[positions.Length];
        var indices = new int[positions.Length];
        int offset = 0;
        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            positions[offset++] = a;
            positions[offset++] = b;
            positions[offset++] = c;
        }
        for (int i = 0; i < sides; i++)
        {
            float a = i * 2f * Mathf.PI / sides;
            float b = (i + 1) * 2f * Mathf.PI / sides;
            Vector3 u = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 v = new Vector3(Mathf.Cos(b), Mathf.Sin(b), 0f);
            Vector3 far = Vector3.forward * shoulder, near = -far;
            Vector3 p = u * tailRadius + near, q = v * tailRadius + near;
            Vector3 r = u * noseRadius + far, s = v * noseRadius + far;
            Face(Vector3.back * 0.5f, q, p);
            Face(p, q, s);
            Face(p, s, r);
            Face(r, s, Vector3.forward * 0.5f);
        }
        for (int i = 0; i < positions.Length; i++)
        {
            uv[i] = Vector2.one * 0.5f;
            indices[i] = i;
        }
        var mesh = new Mesh { name = label, vertices = positions, uv = uv, triangles = indices };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
