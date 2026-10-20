using UnityEngine;

// GDD §5/§15: pollution is visible on the land. A small texture over the play area stores how polluted each spot is
// (soil, air, water and missing vegetation of the region that owns it); the ground shader (EcoQuest/LowPoly Vertex Lit,
// _PollutionStrength) fades polluted ground to a dusty brown-grey and lets the green return as regions recover.
public sealed class EcoPollutionField : MonoBehaviour
{
    public Rect area = new Rect(0f, 0f, 150f, 150f);
    [Range(16, 128)] public int resolution = 56;
    [Min(0.2f)] public float refreshSeconds = 1.2f;

    private static readonly int MapId = Shader.PropertyToID("_EcoPollutionMap");
    private static readonly int RectId = Shader.PropertyToID("_EcoPollutionRect");
    private static readonly int ReadyId = Shader.PropertyToID("_EcoPollutionReady");
    private Texture2D map;
    private float[] raw, blurred;
    private Color32[] pixels;
    private float next;

    private void OnEnable()
    {
        map = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Eco pollution field" };
        raw = new float[resolution * resolution]; blurred = new float[raw.Length]; pixels = new Color32[raw.Length];
        Shader.SetGlobalTexture(MapId, map);
        Shader.SetGlobalVector(RectId, new Vector4(area.x, area.y, 1f / Mathf.Max(1f, area.width), 1f / Mathf.Max(1f, area.height)));
        Shader.SetGlobalFloat(ReadyId, 0f);
        next = 0f;
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(ReadyId, 0f);
        if (map != null) Destroy(map);
    }

    private void Update()
    {
        if (Time.unscaledTime < next) return;
        next = Time.unscaledTime + refreshSeconds;
        Rebuild();
    }

    /// <summary>Pollution shown at a point (0 clean … 1), the same value the ground colour uses.</summary>
    public static float Sample(EcoRegion region)
    {
        if (region == null) return 0f;
        float load = (region.soilPollution * 0.5f + region.airPollution * 0.3f + region.waterPollution * 0.1f) / 100f + (0.5f - region.vegetation) * 0.18f;
        return Mathf.SmoothStep(0f, 0.8f, Mathf.InverseLerp(0.14f, 0.62f, load));
    }

    private void Rebuild()
    {
        int n = resolution;
        float y = transform.position.y;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                var p = new Vector3(area.x + (i + 0.5f) / n * area.width, y, area.y + (j + 0.5f) / n * area.height);
                raw[j * n + i] = Sample(EcoRegion.At(p));
            }
        // Soft borders between regions: two box-blur passes.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float s = 0f; int c = 0;
                    for (int dj = -1; dj <= 1; dj++)
                        for (int di = -1; di <= 1; di++)
                        {
                            int x = Mathf.Clamp(i + di, 0, n - 1), z = Mathf.Clamp(j + dj, 0, n - 1);
                            s += raw[z * n + x]; c++;
                        }
                    blurred[j * n + i] = s / c;
                }
            System.Array.Copy(blurred, raw, raw.Length);
        }
        for (int k = 0; k < raw.Length; k++) { byte v = (byte)Mathf.RoundToInt(Mathf.Clamp01(raw[k]) * 255f); pixels[k] = new Color32(v, v, v, 255); }
        map.SetPixels32(pixels); map.Apply(false);
        Shader.SetGlobalFloat(ReadyId, 1f);
    }
}
