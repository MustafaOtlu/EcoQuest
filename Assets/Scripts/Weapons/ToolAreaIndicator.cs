using UnityEngine;
using UnityEngine.Rendering;

// A quiet ground preview of the tool's actual reachable sector; never intercepts input or casts shadows.
[DefaultExecutionOrder(300)]
public sealed class ToolAreaIndicator : MonoBehaviour
{
    private const int Segments = 40, RingSegments = 32;
    private const float Lift = 0.045f, EdgeWidth = 0.035f;
    private Mesh fill, edge, ring;
    private MeshRenderer fillRenderer, edgeRenderer, ringRenderer;
    private Material fillMaterial, edgeMaterial;
    private readonly Vector3[] fillPoints = new Vector3[Segments + 2];
    private readonly Vector3[] edgePoints = new Vector3[(Segments + 1) * 2 + 8];
    private readonly Vector3[] ringPoints = new Vector3[RingSegments * 2];
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private Vector3 origin, direction, previousOrigin, previousDirection, previousTarget;
    private float previousRange = -1f, previousAngle, nextGroundSample;
    private bool shown, hadTarget;

    public static ToolAreaIndicator Create(Transform owner)
    {
        var root = new GameObject("Tool working area");
        root.transform.SetParent(owner, false);
        var indicator = root.AddComponent<ToolAreaIndicator>();
        indicator.Build(); indicator.Hide(); return indicator;
    }

    private void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        fillMaterial = Transparent(shader); edgeMaterial = Transparent(shader);
        var fan = new int[Segments * 3];
        for (int i = 0; i < Segments; i++) { fan[i * 3] = 0; fan[i * 3 + 1] = i + 1; fan[i * 3 + 2] = i + 2; }
        var outline = new int[Segments * 6 + 12];
        for (int i = 0; i < Segments; i++) Quad(outline, i * 6, i * 2, i * 2 + 1, i * 2 + 3, i * 2 + 2);
        int side = (Segments + 1) * 2;
        Quad(outline, Segments * 6, side, side + 1, side + 2, side + 3);
        Quad(outline, Segments * 6 + 6, side + 4, side + 5, side + 6, side + 7);
        var circle = new int[RingSegments * 6];
        for (int i = 0; i < RingSegments; i++) Quad(circle, i * 6, i * 2, i * 2 + 1, ((i + 1) % RingSegments) * 2 + 1, ((i + 1) % RingSegments) * 2);
        fill = Part("Area fill", fillPoints, fan, fillMaterial, out fillRenderer);
        edge = Part("Area edge", edgePoints, outline, edgeMaterial, out edgeRenderer);
        ring = Part("Selected target", ringPoints, circle, edgeMaterial, out ringRenderer);
    }

    public void Show(Vector3 at, Vector3 forward, float range, float halfAngle, Color tint, Vector3? targetPoint, bool active)
    {
        if (fill == null) Build();
        origin = at; direction = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        range = Mathf.Max(0.1f, range); halfAngle = Mathf.Clamp(halfAngle, 1f, 85f);
        shown = true; ApplyPose(); fillRenderer.enabled = edgeRenderer.enabled = true;
        ringRenderer.enabled = targetPoint.HasValue;
        Paint(fillMaterial, tint, active ? 0.10f : 0.07f); Paint(edgeMaterial, tint, active ? 0.55f : 0.32f);
        Vector3 target = targetPoint.GetValueOrDefault();
        if ((at - previousOrigin).sqrMagnitude < 0.0004f && (direction - previousDirection).sqrMagnitude < 0.0004f
            && Mathf.Abs(range - previousRange) < 0.01f && Mathf.Abs(halfAngle - previousAngle) < 0.1f
            && hadTarget == targetPoint.HasValue && (!hadTarget || (target - previousTarget).sqrMagnitude < 0.0004f)
            && Time.unscaledTime < nextGroundSample) return;
        previousOrigin = at; previousDirection = direction; previousRange = range; previousAngle = halfAngle;
        previousTarget = target; hadTarget = targetPoint.HasValue; nextGroundSample = Time.unscaledTime + 0.1f;
        fillPoints[0] = Ground(Vector3.zero);
        for (int i = 0; i <= Segments; i++)
        {
            Vector3 radial = Quaternion.AngleAxis(-halfAngle + 2f * halfAngle * i / Segments, Vector3.up) * direction;
            fillPoints[i + 1] = Ground(radial * range);
            edgePoints[i * 2] = fillPoints[i + 1] - radial * EdgeWidth;
            edgePoints[i * 2 + 1] = fillPoints[i + 1] + radial * EdgeWidth;
        }
        int first = (Segments + 1) * 2;
        for (int i = 0; i < 2; i++)
        {
            Vector3 end = fillPoints[i == 0 ? 1 : Segments + 1], from = fillPoints[0];
            Vector3 across = Vector3.Cross(Vector3.up, end - from).normalized * EdgeWidth * 0.5f;
            int k = first + i * 4; edgePoints[k] = from - across; edgePoints[k + 1] = from + across;
            edgePoints[k + 2] = end + across; edgePoints[k + 3] = end - across;
        }
        fill.vertices = fillPoints; fill.RecalculateBounds(); edge.vertices = edgePoints; edge.RecalculateBounds();
        if (!targetPoint.HasValue) return;
        Vector3 center = Ground(target - origin);
        for (int i = 0; i < RingSegments; i++)
        {
            Vector3 radial = Quaternion.AngleAxis(i * 360f / RingSegments, Vector3.up) * Vector3.forward;
            ringPoints[i * 2] = center + radial * 0.42f; ringPoints[i * 2 + 1] = center + radial * 0.46f;
        }
        ring.vertices = ringPoints; ring.RecalculateBounds();
    }

    private Vector3 Ground(Vector3 offset)
    {
        Vector3 world = origin + offset; float height = origin.y, nearest = float.PositiveInfinity;
        int count = Physics.RaycastNonAlloc(world + Vector3.up * 4f, Vector3.down, groundHits, 10f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (EcoGround.IsGround(groundHits[i].collider) && groundHits[i].distance < nearest)
            { nearest = groundHits[i].distance; height = groundHits[i].point.y; }
        world.y = height + Lift; return transform.InverseTransformPoint(world);
    }

    private void ApplyPose()
    {
        transform.SetPositionAndRotation(origin, Quaternion.identity);
        Vector3 scale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(1f / Mathf.Max(.001f, Mathf.Abs(scale.x)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
    }
    private void LateUpdate() { if (shown) ApplyPose(); }
    public void Hide() { shown = false; if (fillRenderer != null) fillRenderer.enabled = edgeRenderer.enabled = ringRenderer.enabled = false; }
    private void OnDisable() => Hide();
    private static void Quad(int[] data, int index, int a, int b, int c, int d)
    { data[index] = a; data[index + 1] = b; data[index + 2] = c; data[index + 3] = a; data[index + 4] = c; data[index + 5] = d; }
    private Mesh Part(string label, Vector3[] points, int[] triangles, Material material, out MeshRenderer renderer)
    {
        var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); go.layer = 2; go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label }; mesh.MarkDynamic(); mesh.vertices = points; mesh.triangles = triangles;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return mesh;
    }
    private static Material Transparent(Shader shader)
    {
        var material = new Material(shader) { name = "Tool area preview", renderQueue = 3000 };
        material.SetOverrideTag("RenderType", "Transparent"); material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); return material;
    }
    private static void Paint(Material material, Color tint, float alpha)
    { tint.a = alpha; material.color = tint; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint); }
    private static void Release(Object value) { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    private void OnDestroy() { Release(fill); Release(edge); Release(ring); Release(fillMaterial); Release(edgeMaterial); }
}
