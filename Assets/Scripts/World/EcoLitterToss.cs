using UnityEngine;

/// <summary>
/// GDD v5 §3 (valley traffic, feedback): litter no longer appears silently at someone's feet. A walker swings an arm
/// forward and the can or bottle flies out of the hand; a car flicks it out of the window onto the verge. It tumbles
/// along an arc, hops once where it lands with a puff of dust and a bright ring, and only then becomes the collectable
/// litter (so the vacuum picks it up exactly where the player saw it fall). Cosmetic only: the flying copy has no
/// collider, no resource and is never saved; the real litter is made by <see cref="EcoTraffic.Litter"/> on landing.
/// </summary>
public sealed class EcoLitterToss : MonoBehaviour
{
    private Vector3 from, to;
    private float duration, arc, elapsed;
    private bool metal, landed;
    private Vector3 spinAxis;
    private float spinSpeed;
    private System.Action<GameObject> onLanded;

    private static ParticleSystem effects;
    private static readonly Color Dust = new Color32(205, 184, 146, 220);
    private static readonly Color Ping = new Color32(255, 236, 120, 235);

    public static void Throw(Vector3 from, Vector3 to, bool metal, float duration, float arc, Transform parent,
        System.Action<GameObject> onLanded = null)
    {
        var copy = EcoWorldArt.Spawn(metal ? "MetalScrap" : "PlasticScrap", from, parent);
        if (copy == null) return;
        copy.name = "Litter in flight";
        // Strip everything that would make the copy collectable, solid or saved while it is in the air.
        foreach (var r in copy.GetComponentsInChildren<RecyclableResource>()) DestroyImmediate(r);
        foreach (var b in copy.GetComponentsInChildren<Rigidbody>()) DestroyImmediate(b);
        foreach (var c in copy.GetComponentsInChildren<Collider>()) DestroyImmediate(c);
        // A touch bigger in the air, so the throw reads from the isometric camera.
        copy.transform.localScale *= 1.25f;
        var toss = copy.AddComponent<EcoLitterToss>();
        toss.from = from; toss.to = to; toss.metal = metal; toss.duration = Mathf.Max(0.2f, duration); toss.arc = arc;
        toss.onLanded = onLanded;
        toss.spinAxis = Random.onUnitSphere; toss.spinSpeed = Random.Range(540f, 900f);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (!landed)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            // Straight line plus a parabola: rises out of the hand, drops onto the ground.
            transform.position = Vector3.Lerp(from, to, t) + Vector3.up * (arc * 4f * t * (1f - t));
            transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
            if (Random.value < 0.5f) Emit(transform.position, Random.insideUnitSphere * 0.15f, 0.25f, 0.07f, new Color(1f, 1f, 1f, 0.35f));
            if (t >= 1f) Land();
            return;
        }
        // One small hop after touching down, then hand over to the real litter.
        float h = (elapsed - duration) / 0.28f;
        transform.position = to + Vector3.up * (0.18f * 4f * h * (1f - h));
        transform.Rotate(spinAxis, spinSpeed * 0.3f * Time.deltaTime, Space.World);
        if (h >= 1f) Finish();
    }

    private void Land()
    {
        landed = true;
        Burst(to, Dust, 10, 0.9f, 0.12f, 0.45f);
    }

    private void Finish()
    {
        var litter = EcoTraffic.Litter(to, metal);
        // A bright ring and a few sparks rising: "here it is, take it".
        Burst(to + Vector3.up * 0.05f, Ping, 14, 1.6f, 0.09f, 0.35f);
        for (int i = 0; i < 6; i++)
            Emit(to + Random.insideUnitSphere * 0.15f, new Vector3(0f, Random.Range(0.8f, 1.6f), 0f), Random.Range(0.4f, 0.7f), 0.07f, Ping);
        if (litter != null) litter.AddComponent<EcoLitterGlint>();
        onLanded?.Invoke(litter);
        Destroy(gameObject);
    }

    /// <summary>A flat ring of particles on the ground.</summary>
    public static void Burst(Vector3 centre, Color color, int count, float speed, float size, float life)
    {
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * Mathf.PI * 2f / count;
            var dir = new Vector3(Mathf.Cos(a), 0.15f, Mathf.Sin(a));
            Emit(centre + dir * 0.05f, dir * speed * Random.Range(0.75f, 1.1f), life, size, color);
        }
    }

    private static void Emit(Vector3 position, Vector3 velocity, float life, float size, Color color)
    {
        if (effects == null)
        {
            effects = WeaponVisualEffects.CreateParticles("Litter toss effects", null, 400);
            effects.GetComponent<ParticleSystemRenderer>().maxParticleSize = 0.06f;
        }
        effects.Emit(new ParticleSystem.EmitParams { position = position, velocity = velocity, startLifetime = life, startSize = size, startColor = color }, 1);
    }
}

/// <summary>
/// The vacuum's catch: a cosmetic copy of the collected waste shrinks and spins as it is pulled into the nozzle, so the
/// pickup reads as "sucked in" rather than "vanished". Every behaviour, collider and body is stripped from the copy.
/// </summary>
public sealed class EcoSuckedIn : MonoBehaviour
{
    private System.Func<Vector3> target;
    private Vector3 start, startScale;
    private float age;
    private const float Seconds = 0.28f;
    private static GameObject holder;

    public static void Pull(GameObject source, System.Func<Vector3> nozzle)
    {
        if (source == null || nozzle == null) return;
        // Made under an inactive holder, so none of the copied behaviours wakes up (no save identity, no growth).
        if (holder == null) { holder = new GameObject("Sucked in holder"); holder.SetActive(false); }
        var copy = Instantiate(source, source.transform.position, source.transform.rotation, holder.transform);
        copy.name = "Sucked in";
        // Behaviours first: some require a collider or body and would block their removal.
        foreach (var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) DestroyImmediate(c);
        foreach (var c in copy.GetComponentsInChildren<Component>(true))
            if (c != null && !(c is Transform) && !(c is MeshFilter) && !(c is Renderer)) DestroyImmediate(c);
        copy.transform.SetParent(null, true);
        copy.SetActive(true);
        var s = copy.AddComponent<EcoSuckedIn>();
        s.target = nozzle; s.start = copy.transform.position; s.startScale = copy.transform.localScale;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Seconds);
        Vector3 end;
        try { end = target(); } catch { end = start; }
        // Ease in: it lingers a moment, then whips into the nozzle.
        float k = t * t;
        transform.position = Vector3.Lerp(start, end, k) + Vector3.up * (0.25f * Mathf.Sin(t * Mathf.PI));
        transform.localScale = startScale * Mathf.Lerp(1f, 0.05f, k);
        transform.Rotate(Vector3.up, 900f * Time.deltaTime, Space.World);
        if (t >= 1f) Destroy(gameObject);
    }
}

/// <summary>Freshly thrown litter pulses a little for a while so it is easy to spot among the grass.</summary>
public sealed class EcoLitterGlint : MonoBehaviour
{
    private float age, next;
    private Vector3 baseScale;

    private void Awake() { baseScale = transform.localScale; next = 1.2f; }

    private void Update()
    {
        age += Time.deltaTime;
        if (age > 20f) { transform.localScale = baseScale; Destroy(this); return; }
        // A soft heartbeat on the model plus a spark ring every few seconds.
        float beat = Mathf.Max(0f, Mathf.Sin(age * 6f)) * Mathf.Clamp01(1f - age / 20f);
        transform.localScale = baseScale * (1f + 0.12f * beat);
        if (age >= next)
        {
            next = age + 2.5f;
            EcoLitterToss.Burst(transform.position + Vector3.up * 0.05f, new Color32(255, 236, 120, 200), 8, 0.9f, 0.06f, 0.3f);
        }
    }
}
