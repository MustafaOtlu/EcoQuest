using UnityEngine;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 11"): the scanner is seen working. A cyan ring sweeps out over the ground
/// around the player, a short beam reaches the scanned thing and a ring marks it; a scan that finds something new
/// (a region, a recipe) sends out a wide double ring and a "YENİ KEŞİF" label. Cosmetic only.
/// </summary>
public sealed class EcoScanFx : MonoBehaviour
{
    public static readonly Color Scan = new Color32(90, 225, 255, 255);
    private static Material material;
    private static float nextSweep;

    private enum Kind { Sweep, Beam, Mark }
    private LineRenderer line;
    private Kind kind;
    private Vector3 centre, from, to;
    private float age, life, radius, width;
    private Collider ground;
    private const int Points = 48;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { nextSweep = 0f; }

    /// <summary>One scan: sweep ring at the feet (at most about once a second while the button is held), a beam to the
    /// target and a mark around it. <paramref name="discovery"/> is the name of something found for the first time.</summary>
    public static void Pulse(Vector3 feet, Vector3 aimFrom, bool hasTarget, Vector3 target, Collider targetCollider, string discovery)
    {
        bool found = !string.IsNullOrEmpty(discovery);
        if (found || Time.time >= nextSweep)
        {
            nextSweep = Time.time + 0.9f;
            Make(Kind.Sweep, feet, 7f, 0.7f, 0.12f);
        }
        if (hasTarget)
        {
            var beam = Make(Kind.Beam, aimFrom, 0f, 0.22f, 0.05f);
            beam.from = aimFrom; beam.to = target;
            float size = 0.6f;
            Vector3 at = target;
            if (targetCollider != null && !EcoGround.IsGround(targetCollider))
            {
                var b = targetCollider.bounds;
                size = Mathf.Clamp(Mathf.Max(b.extents.x, b.extents.z) * 1.15f, 0.4f, 4f);
                at = new Vector3(b.center.x, b.min.y, b.center.z);
            }
            Make(Kind.Mark, at, size, 0.5f, 0.07f);
            EcoLitterToss.Burst(target, Scan, 8, 0.9f, 0.06f, 0.3f);
        }
        if (!found) return;
        Make(Kind.Sweep, feet, 13f, 1.1f, 0.2f);
        var second = Make(Kind.Sweep, feet, 9f, 1.1f, 0.14f); second.age = -0.18f;
        EcoFloatingText.Show(feet + Vector3.up * 2.4f, "YENİ KEŞİF · " + discovery, Scan, 2.2f, 1.1f, 34f);
        for (int i = 0; i < 14; i++)
        {
            var v = Random.insideUnitSphere * 1.6f; v.y = Mathf.Abs(v.y) + 1.2f;
            EcoLitterToss.Puff(feet + Vector3.up * 1.2f, v, Random.Range(0.4f, 0.7f), Random.Range(0.1f, 0.18f), Scan);
        }
    }

    private static EcoScanFx Make(Kind kind, Vector3 centre, float radius, float life, float width)
    {
        if (material == null)
        {
            var source = Resources.Load<Material>("EcoEnvironment/Particle");
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = source != null ? new Material(source) : new Material(shader != null ? shader : Shader.Find("Sprites/Default"));
            material.mainTexture = null; material.hideFlags = HideFlags.DontSave;
        }
        var go = new GameObject("Scan " + kind);
        var fx = go.AddComponent<EcoScanFx>();
        fx.kind = kind; fx.centre = centre; fx.radius = radius; fx.life = life; fx.width = width;
        var line = fx.line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = true; line.loop = kind != Kind.Beam;
        line.positionCount = kind == Kind.Beam ? 2 : Points;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
        line.numCapVertices = 2; line.alignment = LineAlignment.View;
        fx.Apply(0f);
        return fx;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = age / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        Apply(Mathf.Max(0f, t));
    }

    private void Apply(float t)
    {
        float alpha = age < 0f ? 0f : 1f - t * t;
        var c = new Color(Scan.r, Scan.g, Scan.b, 0.9f * alpha);
        line.startColor = line.endColor = c;
        if (kind == Kind.Beam)
        {
            line.widthMultiplier = width * (1f - 0.5f * t);
            line.SetPosition(0, from); line.SetPosition(1, to);
            return;
        }
        float eased = 1f - (1f - t) * (1f - t);
        float r = kind == Kind.Sweep ? Mathf.Lerp(0.3f, radius, eased) : radius * (1f + 0.25f * Mathf.Sin(t * Mathf.PI));
        line.widthMultiplier = width * (kind == Kind.Sweep ? 1f - 0.6f * t : 1f);
        for (int i = 0; i < Points; i++)
        {
            float a = i * Mathf.PI * 2f / Points;
            var p = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            // Sweeps hug the ground so the ring never cuts into a slope or floats over a dip.
            if (kind == Kind.Sweep && Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && EcoGround.IsGround(hit.collider))
                p.y = hit.point.y;
            line.SetPosition(i, p + Vector3.up * 0.08f);
        }
    }
}
