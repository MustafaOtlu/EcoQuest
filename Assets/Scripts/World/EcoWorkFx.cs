using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 8"): the valley's work places answer in motion, not only in text. A fishing
/// bobber is cast, splashes, bites with a "!" and the fish leaps to the player; a factory filter snaps into place while
/// the smoke turns white; the workshop hands over its pack with a sparkle; a market card punches green on a trade.
/// Cosmetic only.
/// </summary>
public static class EcoWorkFx
{
    public static readonly Color Splash = new Color32(190, 230, 255, 235);
    public static readonly Color Gold = new Color32(255, 214, 90, 240);
    public static readonly Color Clean = new Color32(120, 226, 130, 240);

    public static void Sparkle(Vector3 at, Color color, int count = 14)
    {
        for (int i = 0; i < count; i++)
        {
            var v = UnityEngine.Random.insideUnitSphere * 1.8f; v.y = Mathf.Abs(v.y) + 1f;
            EcoLitterToss.Puff(at, v, UnityEngine.Random.Range(0.35f, 0.6f), UnityEngine.Random.Range(0.12f, 0.2f), color);
        }
    }

    public static void SplashAt(Vector3 at, int count = 14)
    {
        EcoLitterToss.Burst(at, Splash, count, 1.4f, 0.16f, 0.4f);
        for (int i = 0; i < count / 2; i++)
            EcoLitterToss.Puff(at, new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), UnityEngine.Random.Range(1.4f, 2.4f), UnityEngine.Random.Range(-0.6f, 0.6f)),
                UnityEngine.Random.Range(0.35f, 0.55f), UnityEngine.Random.Range(0.1f, 0.18f), Splash);
    }

    /// <summary>A cosmetic copy of a world-art model (fish, filter …) with every collider, body and behaviour removed.</summary>
    public static GameObject Prop(string art, Vector3 at)
    {
        var go = EcoWorldArt.Spawn(art, at);
        if (go == null) return null;
        foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(b);
        foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(r);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        go.name = art + " (effect)";
        return go;
    }
}

/// <summary>Flies an object along an arc to a (possibly moving) point, spinning, then calls back and optionally destroys it.</summary>
public sealed class EcoArcFly : MonoBehaviour
{
    private Vector3 from; private Func<Vector3> to; private float duration, arc, age; private Action done; private bool destroy;
    private Vector3 spin;

    public static EcoArcFly Launch(GameObject go, Vector3 from, Func<Vector3> to, float duration, float arc, Action done = null, bool destroyAtEnd = true, float spinSpeed = 540f)
    {
        if (go == null) return null;
        var fly = go.AddComponent<EcoArcFly>();
        fly.from = from; fly.to = to; fly.duration = Mathf.Max(0.05f, duration); fly.arc = arc; fly.done = done; fly.destroy = destroyAtEnd;
        fly.spin = UnityEngine.Random.onUnitSphere * spinSpeed;
        go.transform.position = from;
        return fly;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        Vector3 end;
        try { end = to(); } catch { end = from; }
        transform.position = Vector3.Lerp(from, end, t) + Vector3.up * (arc * 4f * t * (1f - t));
        transform.Rotate(spin * Time.deltaTime, Space.World);
        if (t < 1f) return;
        var callback = done; done = null;
        callback?.Invoke();
        if (destroy) Destroy(gameObject); else Destroy(this);
    }
}

/// <summary>Pops a transform in from small with an overshoot (a filter snapping into its frame).</summary>
public sealed class EcoPopIn : MonoBehaviour
{
    private Vector3 baseScale; private float age;
    public static void On(GameObject go) { if (go != null && go.GetComponent<EcoPopIn>() == null) go.AddComponent<EcoPopIn>(); }
    private void Awake() { baseScale = transform.localScale; transform.localScale = baseScale * 0.05f; }
    private void Update()
    {
        age += Time.deltaTime;
        float t = age / 0.45f;
        if (t >= 1f) { transform.localScale = baseScale; Destroy(this); return; }
        float s = t < 0.45f ? Mathf.Lerp(0.05f, 1.3f, t / 0.45f) : 1f + 0.3f * Mathf.Exp(-6f * (t - 0.45f)) * Mathf.Cos(14f * (t - 0.45f));
        transform.localScale = baseScale * s;
    }
    private void OnDestroy() { if (transform != null) transform.localScale = baseScale; }
}

/// <summary>The fishing float: cast on an arc, splashes, bobs, dips on a bite and is reeled back in.</summary>
public sealed class EcoBobber : MonoBehaviour
{
    private Vector3 water; private float age, biteAt = -1f; private bool landed, reeling;
    private static Material white, red;

    public static EcoBobber Cast(Vector3 from, Vector3 water)
    {
        var root = new GameObject("Fishing bobber");
        if (white == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            white = new Material(shader) { color = new Color(0.96f, 0.96f, 0.94f), hideFlags = HideFlags.DontSave };
            red = new Material(shader) { color = new Color(0.9f, 0.18f, 0.14f), hideFlags = HideFlags.DontSave };
        }
        Part(root.transform, white, new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0.16f, 0.2f));
        Part(root.transform, red, new Vector3(0f, 0.09f, 0f), new Vector3(0.16f, 0.12f, 0.16f));
        var bob = root.AddComponent<EcoBobber>();
        bob.water = water;
        EcoArcFly.Launch(root, from, () => water, 0.45f, 1.6f, bob.Land, false, 0f);
        return bob;
    }

    private static void Part(Transform parent, Material material, Vector3 at, Vector3 size)
    {
        var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());
        p.transform.SetParent(parent, false); p.transform.localPosition = at; p.transform.localScale = size;
        p.GetComponent<Renderer>().sharedMaterial = material;
    }

    private void Land() { landed = true; age = 0f; EcoWorkFx.SplashAt(water, 12); }

    /// <summary>The fish takes the bait: the float dips and a "!" pops over it.</summary>
    public void Bite()
    {
        if (biteAt >= 0f || !landed) return;
        biteAt = age;
        EcoFloatingText.Follow(transform, Vector3.up * 0.5f, "!", EcoFloatingText.Alert, 0.9f, 52f);
        EcoLitterToss.Burst(water, EcoWorkFx.Splash, 8, 0.9f, 0.1f, 0.3f);
    }

    /// <summary>Reels the float back to the rod and removes it.</summary>
    public void Reel(Func<Vector3> rod)
    {
        if (reeling) return;
        reeling = true;
        var fly = GetComponent<EcoArcFly>(); if (fly != null) Destroy(fly);
        EcoArcFly.Launch(gameObject, transform.position, rod, 0.3f, 0.6f, null, true, 0f);
    }

    private void Update()
    {
        if (!landed || reeling) return;
        age += Time.deltaTime;
        float dip = biteAt >= 0f ? Mathf.Abs(Mathf.Sin((age - biteAt) * 18f)) * 0.12f : 0f;
        transform.position = water + Vector3.up * (0.03f * Mathf.Sin(age * 3.2f) - dip);
        if (UnityEngine.Random.value < 0.02f) EcoLitterToss.Burst(water, EcoWorkFx.Splash, 6, 0.5f, 0.06f, 0.4f);
    }
}

/// <summary>A uGUI card that punches and flashes when its action succeeds (green) or shakes red when it fails.</summary>
public sealed class EcoUIPop : MonoBehaviour
{
    private RectTransform rect; private Image flash; private Text ghost;
    private Vector2 basePosition; private float age = -1f; private bool ok;

    public static void Play(RectTransform target, bool success, string text)
    {
        if (target == null) return;
        var pop = target.GetComponent<EcoUIPop>();
        if (pop == null) pop = target.gameObject.AddComponent<EcoUIPop>();
        pop.Begin(success, text);
    }

    private void Begin(bool success, string text)
    {
        if (rect == null)
        {
            rect = (RectTransform)transform;
            var f = new GameObject("Pop flash", typeof(RectTransform), typeof(Image));
            f.transform.SetParent(transform, false);
            var fr = (RectTransform)f.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            flash = f.GetComponent<Image>(); flash.raycastTarget = false;
            var g = new GameObject("Pop ghost", typeof(RectTransform), typeof(Text));
            g.transform.SetParent(transform, false);
            var gr = (RectTransform)g.transform; gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 1f); gr.sizeDelta = new Vector2(220f, 30f);
            ghost = g.GetComponent<Text>(); ghost.font = EcoUI.Font; ghost.fontSize = 18; ghost.fontStyle = FontStyle.Bold;
            ghost.alignment = TextAnchor.MiddleCenter; ghost.raycastTarget = false; ghost.horizontalOverflow = HorizontalWrapMode.Overflow;
            g.AddComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.85f); // market and panels are light paper
        }
        if (age < 0f) basePosition = rect.anchoredPosition;
        ok = success; age = 0f;
        transform.SetAsLastSibling(); // draw over the neighbouring cards so the rising word is never hidden
        ghost.text = text ?? "";
        ghost.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void Update()
    {
        if (age < 0f) return;
        age += Time.unscaledDeltaTime;
        float t = age / 0.9f;
        if (t >= 1f) { age = -1f; rect.localScale = Vector3.one; rect.anchoredPosition = basePosition; flash.color = Color.clear; ghost.gameObject.SetActive(false); return; }
        var color = ok ? (Color)EcoWorkFx.Clean : new Color(0.95f, 0.35f, 0.28f);
        float early = Mathf.Clamp01(1f - t * 2.5f);
        rect.localScale = Vector3.one * (ok ? 1f + 0.07f * Mathf.Sin(Mathf.Clamp01(t * 3f) * Mathf.PI) : 1f);
        rect.anchoredPosition = basePosition + (ok ? Vector2.zero : new Vector2(Mathf.Sin(age * 70f) * 6f * early, 0f));
        flash.color = new Color(color.r, color.g, color.b, 0.35f * early);
        var gc = ok ? new Color(0.12f, 0.5f, 0.2f) : new Color(0.75f, 0.18f, 0.12f); gc.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        ghost.color = gc;
        ((RectTransform)ghost.transform).anchoredPosition = new Vector2(0f, 6f + 34f * (1f - (1f - t) * (1f - t)));
    }
}
