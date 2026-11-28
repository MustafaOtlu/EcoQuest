using System.Linq;
using UnityEngine;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 4"): building is the core verb, so it should feel physical. A new structure
/// springs up out of the ground with a squash and stretch and a ring of dust, and the materials it cost float up in red.
/// A cable or pipe is drawn out from the first node to the second, sparks at both ends and then sends a pulse along
/// its length (power yellow, water blue). Cosmetic only: nothing here is saved or changes the simulation.
/// </summary>
public static class EcoBuildFx
{
    public static readonly Color Spend = new Color32(255, 112, 96, 255);
    private static readonly Color Dust = new Color32(196, 178, 140, 230);
    private static readonly Color Spark = new Color32(255, 226, 120, 240);

    /// <summary>The structure grows from the ground; the cost floats above it.</summary>
    public static void Placed(GameObject structure, Vector2 footprint, int metal, int plastic)
    {
        if (structure == null) return;
        var rise = structure.AddComponent<EcoBuildRise>();
        rise.radius = Mathf.Max(0.6f, Mathf.Max(footprint.x, footprint.y) * 0.55f);
        Cost(structure.transform.position + Vector3.up * 2.2f, metal, plastic);
    }

    /// <summary>"−10 metal" and "−5 plastik", stacked and red.</summary>
    public static void Cost(Vector3 world, int metal, int plastic)
    {
        float lift = 0f;
        if (metal > 0) { EcoFloatingText.Show(world + Vector3.up * lift, "−" + metal + " metal", Spend, 1.4f, 0.8f); lift += 0.6f; }
        if (plastic > 0) EcoFloatingText.Show(world + Vector3.up * lift, "−" + plastic + " plastik", Spend, 1.4f, 0.8f);
    }

    /// <summary>A cable or pipe that has just been laid: drawn out from its first node, then a pulse runs along it.</summary>
    public static void Laid(LineRenderer line, Color pulse, int metal, int plastic, Vector3 from, Vector3 to)
    {
        if (line == null) return;
        var grow = line.gameObject.AddComponent<EcoLineGrow>();
        grow.pulse = pulse;
        Sparks(from, Spark, 10);
        Cost(Vector3.Lerp(from, to, 0.5f) + Vector3.up * 1.2f, metal, plastic);
    }

    public static void DustRing(Vector3 centre, float radius, int count)
    {
        float offset = Random.value * Mathf.PI * 2f;
        for (int i = 0; i < count; i++)
        {
            float a = offset + i * Mathf.PI * 2f / count;
            var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            EcoLitterToss.Puff(centre + dir * radius + Vector3.up * 0.08f, (dir * 1.6f + Vector3.up * 0.35f) * Random.Range(0.7f, 1.1f),
                Random.Range(0.45f, 0.7f), Random.Range(0.3f, 0.5f), Dust);
        }
    }

    public static void Sparks(Vector3 at, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var v = Random.insideUnitSphere * 1.6f; v.y = Mathf.Abs(v.y) + 0.8f;
            EcoLitterToss.Puff(at, v, Random.Range(0.25f, 0.45f), Random.Range(0.12f, 0.2f), color);
        }
    }

    public static Color SparkColor => Spark;
    public static readonly Color Gain = new Color32(120, 235, 140, 255);

    /// <summary>Feedback (GDD v5 §21, "Geri bildirim 13"): a dismantled structure squashes and sinks into the ground in a
    /// ring of dust and the materials it gives back float up in green. Call before the structure is hidden.</summary>
    public static void Dismantled(GameObject structure, Vector2 footprint, int metal, int plastic)
    {
        if (structure == null) return;
        float radius = Mathf.Max(0.6f, Mathf.Max(footprint.x, footprint.y) * 0.55f);
        var copy = MeshCopy(structure, null, "Dismantled structure");
        copy.AddComponent<EcoBuildSink>();
        var at = structure.transform.position;
        DustRing(at, radius * 0.8f, 18);
        for (int i = 0; i < 5; i++) Sparks(at + Random.insideUnitSphere * radius * 0.5f + Vector3.up * 0.5f, Spark, 1);
        float lift = 0f; var label = at + Vector3.up * 2.2f;
        if (metal > 0) { EcoFloatingText.Show(label, "+" + metal + " metal", Gain, 1.5f, 0.9f); lift += 0.6f; }
        if (plastic > 0) EcoFloatingText.Show(label + Vector3.up * lift, "+" + plastic + " plastik", Gain, 1.5f, 0.9f);
    }

    /// <summary>Only the visible meshes of <paramref name="source"/> (no scripts or colliders), at the same place.
    /// <paramref name="material"/> replaces every material when given.</summary>
    public static GameObject MeshCopy(GameObject source, Material material, string name)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        foreach (var r in source.GetComponentsInChildren<MeshRenderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
            var filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var go = new GameObject(r.name);
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            var parentScale = root.transform.lossyScale; var scale = r.transform.lossyScale;
            go.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = material != null ? Enumerable.Repeat(material, r.sharedMaterials.Length).ToArray() : r.sharedMaterials;
            mr.shadowCastingMode = material != null ? UnityEngine.Rendering.ShadowCastingMode.Off : r.shadowCastingMode;
        }
        return root;
    }
}

/// <summary>A dismantled structure's copy: a short squash up, then it shrinks and sinks into the ground.</summary>
public sealed class EcoBuildSink : MonoBehaviour
{
    private Vector3 start;
    private float age;
    private const float Pop = 0.08f, Sink = 0.38f;

    private void Awake() => start = transform.position;

    private void Update()
    {
        age += Time.deltaTime;
        float sy, sxz, down = 0f;
        if (age < Pop) { float t = age / Pop; sy = 1f + 0.12f * t; sxz = 1f - 0.05f * t; }
        else
        {
            float t = (age - Pop) / Sink;
            if (t >= 1f) { Destroy(gameObject); return; }
            float e = t * t;
            sy = Mathf.Lerp(1.12f, 0.02f, e); sxz = Mathf.Lerp(0.95f, 0.6f, e); down = 0.4f * e;
        }
        transform.localScale = new Vector3(sxz, sy, sxz);
        transform.position = start + Vector3.down * down;
    }
}

/// <summary>
/// The player's own building under the cursor in build mode glows red (a slightly larger see-through copy that
/// pulses), so it is clear what [X] will take down. Shown each frame it is pointed at; gone the frame after.
/// </summary>
public sealed class EcoRemoveHighlight : MonoBehaviour
{
    private static EcoRemoveHighlight current;
    private static Material material;
    private GameObject target;
    private int frame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; }

    public static void Show(GameObject structure)
    {
        if (structure == null) return;
        if (current != null && current.target != structure) { Destroy(current.gameObject); current = null; }
        if (current == null)
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.renderQueue = 3001;
            }
            var copy = EcoBuildFx.MeshCopy(structure, material, "Remove highlight");
            copy.transform.localScale = Vector3.one * 1.04f;
            current = copy.AddComponent<EcoRemoveHighlight>();
            current.target = structure;
        }
        current.frame = Time.frameCount;
    }

    private void LateUpdate()
    {
        if (target == null || !target.activeInHierarchy || frame != Time.frameCount) { Destroy(gameObject); if (current == this) current = null; return; }
        transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
        material.SetColor("_BaseColor", new Color(1f, 0.2f, 0.12f, 0.22f + 0.2f * pulse));
    }
}

/// <summary>Squash and stretch out of the ground, then a dust ring where it lands.</summary>
public sealed class EcoBuildRise : MonoBehaviour
{
    public float radius = 1f;
    private Vector3 baseScale;
    private float age;
    private bool landed;
    private const float Grow = 0.2f, Settle = 0.65f;

    private void Awake()
    {
        baseScale = transform.localScale;
        transform.localScale = new Vector3(baseScale.x * 1.2f, baseScale.y * 0.05f, baseScale.z * 1.2f);
    }

    private void Start() => EcoBuildFx.DustRing(transform.position, radius * 0.6f, 10);

    private void Update()
    {
        age += Time.deltaTime;
        float sy, sxz;
        if (age < Grow)
        {
            float t = age / Grow; t = 1f - (1f - t) * (1f - t);
            sy = Mathf.Lerp(0.05f, 1.18f, t); sxz = Mathf.Lerp(1.2f, 0.9f, t);
        }
        else
        {
            if (!landed)
            {
                landed = true;
                EcoBuildFx.DustRing(transform.position, radius, 16);
                for (int i = 0; i < 6; i++) EcoBuildFx.Sparks(transform.position + Random.insideUnitSphere * radius * 0.5f + Vector3.up * 0.3f, EcoBuildFx.SparkColor, 1);
            }
            float u = age - Grow;
            // A damped wobble back to the real size.
            float wobble = Mathf.Exp(-7f * u) * Mathf.Cos(14f * u);
            sy = 1f + 0.18f * wobble; sxz = 1f - 0.1f * wobble;
            if (u >= Settle) { transform.localScale = baseScale; Destroy(this); return; }
        }
        transform.localScale = new Vector3(baseScale.x * sxz, baseScale.y * sy, baseScale.z * sxz);
    }

    private void OnDestroy() { if (transform != null) transform.localScale = baseScale; }
}

/// <summary>
/// Runs after the cable/pipe has written its full path and cuts it at the growing length, with sparks at the head;
/// once complete it sparks at the far end and sends a glowing pulse along the line.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class EcoLineGrow : MonoBehaviour
{
    public Color pulse = Color.yellow;
    private LineRenderer line;
    private float age;
    private bool reached;
    private Vector3[] full = new Vector3[8];
    private const float DrawSeconds = 0.45f, PulseSeconds = 0.6f;

    private void Awake() => line = GetComponent<LineRenderer>();

    private void LateUpdate()
    {
        if (line == null || !line.enabled || line.positionCount < 2) return;
        age += Time.deltaTime;
        int n = line.positionCount;
        if (full.Length < n) full = new Vector3[n];
        line.GetPositions(full);
        float total = 0f;
        for (int i = 1; i < n; i++) total += Vector3.Distance(full[i - 1], full[i]);
        if (total < 0.01f) return;

        if (age < DrawSeconds)
        {
            float t = age / DrawSeconds; t = 1f - (1f - t) * (1f - t);
            float want = total * t, run = 0f;
            int count = 1; Vector3 head = full[0];
            for (int i = 1; i < n; i++)
            {
                float seg = Vector3.Distance(full[i - 1], full[i]);
                if (run + seg >= want) { head = Vector3.Lerp(full[i - 1], full[i], seg > 0f ? (want - run) / seg : 1f); count = i; break; }
                run += seg; head = full[i]; count = i;
            }
            line.positionCount = count + 1;
            line.SetPosition(count, head);
            if (Random.value < 0.8f) EcoBuildFx.Sparks(head, EcoBuildFx.SparkColor, 1);
            return;
        }
        if (!reached)
        {
            reached = true;
            EcoBuildFx.Sparks(full[n - 1], EcoBuildFx.SparkColor, 14);
            EcoLitterToss.Burst(full[n - 1], pulse, 14, 1.6f, 0.15f, 0.4f);
        }
        float p = (age - DrawSeconds) / PulseSeconds;
        if (p >= 1f) { EcoLitterToss.Burst(full[n - 1], pulse, 12, 1.3f, 0.13f, 0.35f); Destroy(this); return; }
        // A bright head travelling along the line, leaving a short trail.
        Vector3 at = PointAt(full, n, total * p);
        for (int k = 0; k < 3; k++)
            EcoLitterToss.Puff(at + Random.insideUnitSphere * 0.03f, Random.insideUnitSphere * 0.1f, 0.3f, 0.22f, pulse);
    }

    private static Vector3 PointAt(Vector3[] pts, int n, float distance)
    {
        for (int i = 1; i < n; i++)
        {
            float seg = Vector3.Distance(pts[i - 1], pts[i]);
            if (distance <= seg) return Vector3.Lerp(pts[i - 1], pts[i], seg > 0f ? distance / seg : 1f);
            distance -= seg;
        }
        return pts[n - 1];
    }
}
