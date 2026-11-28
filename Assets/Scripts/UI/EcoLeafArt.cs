using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural art for the title menu (GDD v5 §21, "Ana menü"): a tan card framed by a wreath of green leaves, and
/// the black-to-clear gradient behind the menu column. Drawn once into textures; no image assets needed.
/// </summary>
public static class EcoLeafArt
{
    private static readonly Dictionary<long, Sprite> frames = new Dictionary<long, Sprite>();
    private static Sprite gradient;

    public static readonly Color Tan = new Color32(236, 206, 166, 255);
    public static readonly Color TanDark = new Color32(196, 150, 104, 255);
    public static readonly Color Ink = new Color32(74, 46, 24, 255);
    private static readonly Color[] Greens =
    {
        new Color32(46, 120, 52, 255), new Color32(86, 170, 74, 255), new Color32(34, 96, 44, 255), new Color32(122, 190, 82, 255)
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { frames.Clear(); gradient = null; }

    /// <summary>Left 100 % black → right 30 % black, for an Image stretched over the left half of the screen.</summary>
    public static Sprite Gradient()
    {
        if (gradient != null) return gradient;
        const int w = 256;
        var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Title panel gradient", hideFlags = HideFlags.DontSave };
        var pixels = new Color32[w * 4];
        for (int x = 0; x < w; x++)
        {
            byte a = (byte)Mathf.RoundToInt(Mathf.Lerp(1f, 0.3f, x / (float)(w - 1)) * 255f);
            for (int y = 0; y < 4; y++) pixels[y * w + x] = new Color32(0, 0, 0, a);
        }
        tex.SetPixels32(pixels); tex.Apply(false, true);
        gradient = Sprite.Create(tex, new Rect(0, 0, w, 4), new Vector2(0.5f, 0.5f), 100f);
        gradient.hideFlags = HideFlags.DontSave;
        return gradient;
    }

    /// <summary>A tan card of <paramref name="width"/> × <paramref name="height"/> pixels with a leaf wreath round its edge.</summary>
    public static Sprite Frame(int width, int height, int seed = 7)
    {
        long key = ((long)width << 32) | ((long)height << 8) | (uint)(seed & 0xff);
        if (frames.TryGetValue(key, out var cached) && cached != null) return cached;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Leaf frame", hideFlags = HideFlags.DontSave };
        var px = new Color[width * height];
        float leaf = Mathf.Clamp(Mathf.Min(width, height) * 0.22f, 14f, 30f);
        float inset = leaf * 0.85f, radius = leaf * 0.9f;

        // The tan card: rounded, a little lighter at the top, with a thin darker rim.
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = RoundedBox(x + 0.5f, y + 0.5f, width, height, inset, radius);
                if (d > 1f) continue;
                float shade = Mathf.Lerp(0.9f, 1.04f, y / (float)height);
                var c = Tan * shade; c.a = 1f;
                if (d > -3f) c = Color.Lerp(c, TanDark, Mathf.Clamp01((d + 3f) / 3f));
                c.a = Mathf.Clamp01(1f - d);
                px[y * width + x] = c;
            }

        // Leaves round the edge, tilted alternately outwards; bigger clusters at the corners.
        var random = new System.Random(seed);
        float perimeter = 2f * (width + height - 4f * inset);
        int count = Mathf.Max(8, Mathf.RoundToInt(perimeter / (leaf * 0.5f)));
        for (int i = 0; i < count; i++)
        {
            float s = (i + (float)random.NextDouble() * 0.3f) / count * perimeter;
            PointOnEdge(s, width, height, inset, out var p, out var tangent);
            float side = (i % 2 == 0) ? 1f : -1f;
            float angle = Mathf.Atan2(tangent.y, tangent.x) + side * Mathf.Deg2Rad * (28f + (float)random.NextDouble() * 18f);
            float len = leaf * (0.85f + (float)random.NextDouble() * 0.35f);
            Leaf(px, width, height, p + new Vector2(-tangent.y, tangent.x) * (side * leaf * 0.12f), angle, len, len * 0.46f, Greens[random.Next(Greens.Length)]);
        }
        float c0 = inset + leaf * 0.15f;
        var corners = new[] { new Vector2(c0, c0), new Vector2(width - c0, c0), new Vector2(c0, height - c0), new Vector2(width - c0, height - c0) };
        foreach (var corner in corners)
        {
            Vector2 outward = (corner - new Vector2(width * 0.5f, height * 0.5f)).normalized;
            float baseAngle = Mathf.Atan2(outward.y, outward.x);
            for (int k = -1; k <= 1; k++)
            {
                float a = baseAngle + k * 0.75f;
                var at = corner + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * leaf * 0.2f;
                Leaf(px, width, height, at, a, leaf * 1.15f, leaf * 0.52f, Greens[(k + 2 + random.Next(2)) % Greens.Length]);
            }
        }
        tex.SetPixels(px); tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
        sprite.hideFlags = HideFlags.DontSave;
        frames[key] = sprite;
        return sprite;
    }

    private static float RoundedBox(float x, float y, float w, float h, float inset, float r)
    {
        float hx = w * 0.5f - inset, hy = h * 0.5f - inset;
        float qx = Mathf.Abs(x - w * 0.5f) - (hx - r), qy = Mathf.Abs(y - h * 0.5f) - (hy - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static void PointOnEdge(float s, int w, int h, float inset, out Vector2 p, out Vector2 tangent)
    {
        float top = w - 2f * inset, side = h - 2f * inset;
        if (s < top) { p = new Vector2(inset + s, inset); tangent = Vector2.right; return; }
        s -= top;
        if (s < side) { p = new Vector2(w - inset, inset + s); tangent = Vector2.up; return; }
        s -= side;
        if (s < top) { p = new Vector2(w - inset - s, h - inset); tangent = Vector2.left; return; }
        s -= top;
        p = new Vector2(inset, h - inset - Mathf.Min(s, side)); tangent = Vector2.down;
    }

    /// <summary>A pointed leaf with a lighter midrib and a darker edge, blended over what is already there.</summary>
    private static void Leaf(Color[] px, int w, int h, Vector2 centre, float angle, float length, float width, Color color)
    {
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), across = new Vector2(-dir.y, dir.x);
        float reach = length * 0.5f + 2f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(centre.x - reach)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(centre.x + reach));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(centre.y - reach)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(centre.y + reach));
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                Vector2 d = new Vector2(x + 0.5f, y + 0.5f) - centre;
                float u = Vector2.Dot(d, dir), v = Vector2.Dot(d, across);
                float t = u / length + 0.5f;
                if (t <= 0f || t >= 1f) continue;
                float half = width * 0.5f * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.8f);
                float edge = half - Mathf.Abs(v);
                if (edge <= -1f) continue;
                float alpha = Mathf.Clamp01(edge + 0.5f);
                var c = color;
                if (Mathf.Abs(v) < 0.7f && t > 0.08f && t < 0.9f) c = Color.Lerp(c, Color.white, 0.35f);
                else c = Color.Lerp(c * 0.75f, c, Mathf.Clamp01(edge / Mathf.Max(1f, half * 0.6f)));
                c.a = 1f;
                int i = y * w + x;
                var under = px[i];
                float outA = alpha + under.a * (1f - alpha);
                px[i] = outA <= 0f ? Color.clear : new Color(
                    (c.r * alpha + under.r * under.a * (1f - alpha)) / outA,
                    (c.g * alpha + under.g * under.a * (1f - alpha)) / outA,
                    (c.b * alpha + under.b * under.a * (1f - alpha)) / outA, outA);
            }
    }
}
