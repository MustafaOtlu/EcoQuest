using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 6"): the big moments get a moment. "+20 YEP" rises from the player when a
/// reward lands; crossing a 40 YEP step adds a gold ring around the player and a "YENİ UZMANLIK HAKKI" banner; a run
/// goal finishing throws confetti, shows a banner and makes its row on the run chip glow with a popping tick.
/// Rewards are read from <see cref="EcoProgression.RewardGranted"/> (never fired by loading a save), so nothing
/// celebrates on load. Cosmetic only.
/// </summary>
public sealed class EcoCelebrate : MonoBehaviour
{
    private static EcoCelebrate current;
    private static readonly Dictionary<string, float> goalTimes = new Dictionary<string, float>();
    private EcoProgression watched;
    private string banner, subtitle;
    private Color bannerColor;
    private float bannerAge = -1f;
    private float yepStackUntil; private int yepStack;
    private GUIStyle big, small;
    private const float BannerSeconds = 2.6f, GlowSeconds = 3f;

    public static readonly Color Gold = new Color32(255, 204, 72, 255);
    public static readonly Color Leaf = new Color32(110, 220, 120, 255);
    private static readonly Color[] Confetti =
    {
        new Color32(255, 204, 72, 255), new Color32(110, 220, 120, 255), new Color32(96, 190, 255, 255),
        new Color32(255, 128, 110, 255), new Color32(255, 255, 255, 255),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; cachedWeapons = null; goalTimes.Clear(); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() => Ensure();

    private static EcoCelebrate Ensure()
    {
        if (current != null) return current;
        var go = new GameObject("Celebrations");
        DontDestroyOnLoad(go);
        return current = go.AddComponent<EcoCelebrate>();
    }

    /// <summary>A run goal has just been met.</summary>
    public static void Goal(string id, string title, bool required)
    {
        var c = Ensure();
        if (!string.IsNullOrEmpty(id)) goalTimes[id] = Time.unscaledTime;
        c.Banner(required ? "HEDEF TAMAMLANDI" : "BONUS HEDEF", title, Leaf);
        var player = Player();
        if (player != null) Burst(player.position + Vector3.up * 1.2f, 40, true);
    }

    /// <summary>0..1 for a short while after a goal completed (row glow on the run chip).</summary>
    public static float GoalGlow(string id)
    {
        if (string.IsNullOrEmpty(id) || !goalTimes.TryGetValue(id, out float at)) return 0f;
        float t = (Time.unscaledTime - at) / GlowSeconds;
        return t >= 1f ? 0f : 1f - t;
    }

    private static PlayerWeaponSystem cachedWeapons;
    private static PlayerWeaponSystem Weapons()
    {
        if (cachedWeapons == null) cachedWeapons = FindFirstObjectByType<PlayerWeaponSystem>();
        return cachedWeapons;
    }

    private void Update()
    {
        var weapons = Weapons();
        var progression = weapons != null ? weapons.Progression : null;
        if (progression == watched) return;
        if (watched != null) watched.RewardGranted -= OnReward;
        watched = progression;
        if (watched != null) watched.RewardGranted += OnReward;
    }

    private void OnDestroy() { if (watched != null) watched.RewardGranted -= OnReward; if (current == this) current = null; }

    private void OnReward(string id)
    {
        int amount = EcoProgression.RewardValue(id);
        var player = Player();
        if (amount <= 0 || player == null || watched == null) return;
        // Rewards that land together stack instead of drawing on top of each other.
        yepStack = Time.unscaledTime < yepStackUntil ? yepStack + 1 : 0; yepStackUntil = Time.unscaledTime + 0.6f;
        EcoFloatingText.Show(player.position + Vector3.up * (2.3f + 0.7f * yepStack), "+" + amount + " YEP", Gold, 1.8f, 1.2f, 38f);
        Burst(player.position + Vector3.up * 1.4f, 14, false);
        int after = watched.Experience, before = after - amount;
        if (after / EcoProgression.PointsPerRight > before / EcoProgression.PointsPerRight && before < EcoProgression.MaximumExperience)
        {
            // A new specialisation right: a gold ring sweeps out around the player.
            for (int ring = 0; ring < 3; ring++)
                EcoLitterToss.Burst(player.position + Vector3.up * (0.1f + ring * 0.5f), Gold, 28, 3.2f - ring * 0.6f, 0.22f, 0.7f);
            Banner("YENİ UZMANLIK HAKKI", "Hak " + watched.EarnedRights + " / 9 · " + EcoKeys.Tag(EcoKeys.Action.Skills) + " uzmanlık", Gold);
        }
    }

    private static void Burst(Vector3 at, int count, bool confetti)
    {
        for (int i = 0; i < count; i++)
        {
            var v = Random.insideUnitSphere * 2.2f; v.y = Mathf.Abs(v.y) * 1.4f + 1.8f;
            var color = confetti ? Confetti[i % Confetti.Length] : Gold;
            EcoLitterToss.Puff(at + Random.insideUnitSphere * 0.2f, v, Random.Range(0.7f, 1.2f), Random.Range(0.14f, 0.26f), color);
        }
    }

    private static Transform Player()
    {
        var weapons = Weapons();
        return weapons != null ? weapons.transform : null;
    }

    private void Banner(string title, string sub, Color color)
    {
        banner = title; subtitle = sub; bannerColor = color; bannerAge = 0f;
    }

    private void OnGUI()
    {
        if (bannerAge < 0f || Event.current.type != EventType.Repaint) return;
        bannerAge += Time.unscaledDeltaTime;
        float t = bannerAge / BannerSeconds;
        if (t >= 1f) { bannerAge = -1f; return; }
        float ui = Screen.height / 1080f;
        if (big == null)
        {
            big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, clipping = TextClipping.Overflow, wordWrap = false };
            small = new GUIStyle(big) { fontStyle = FontStyle.Normal };
        }
        // Pop in with an overshoot, hold, then fade and drift up.
        float pop = t < 0.08f ? Mathf.Lerp(0.5f, 1.15f, t / 0.08f) : t < 0.16f ? Mathf.Lerp(1.15f, 1f, (t - 0.08f) / 0.08f) : 1f;
        float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
        float y = Screen.height * 0.22f - (t > 0.75f ? (t - 0.75f) * 60f * ui : 0f);
        float w = 640f * ui * pop, h = 110f * ui * pop;
        var strip = new Rect(Screen.width * 0.5f - w * 0.5f, y - h * 0.5f, w, h);
        var previous = GUI.color;
        GUI.color = new Color(0.06f, 0.09f, 0.07f, 0.72f * alpha);
        GUI.DrawTexture(strip, Texture2D.whiteTexture);
        GUI.color = new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha);
        GUI.DrawTexture(new Rect(strip.x, strip.y, strip.width, 4f * ui), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(strip.x, strip.yMax - 4f * ui, strip.width, 4f * ui), Texture2D.whiteTexture);
        GUI.color = previous;
        big.fontSize = Mathf.Max(12, Mathf.RoundToInt(44f * ui * pop));
        small.fontSize = Mathf.Max(10, Mathf.RoundToInt(22f * ui * pop));
        Draw(new Rect(strip.x, strip.y + 8f * ui, strip.width, h * 0.55f), banner, big, bannerColor, alpha, ui);
        Draw(new Rect(strip.x, strip.y + h * 0.55f, strip.width, h * 0.38f), subtitle, small, Color.white, alpha, ui);
    }

    private static void Draw(Rect rect, string text, GUIStyle style, Color color, float alpha, float ui)
    {
        if (string.IsNullOrEmpty(text)) return;
        var content = new GUIContent(text);
        float o = Mathf.Max(1f, 2f * ui);
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * alpha);
        style.Draw(new Rect(rect.x + o, rect.y + o, rect.width, rect.height), content, false, false, false, false);
        style.normal.textColor = new Color(color.r, color.g, color.b, alpha);
        style.Draw(rect, content, false, false, false, false);
    }
}
