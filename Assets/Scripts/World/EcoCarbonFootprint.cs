using UnityEngine;
using UnityEngine.InputSystem;

// Karbon ayak izi (etik kurul formu: "karbon ayak izi"). A ledger of what moves the world carbon index: every source
// (factories, fires, the home's backup generator, the finale) and every sink (trees, vegetation and natural recovery)
// reports the change it actually made to its region. Each change is weighted by the region's share of the map, so the
// ledger speaks in world-carbon points like the glacier station and P6 (≤ 20). The HUD row shows the current trend per
// game day; clicking it opens the breakdown with the biggest source and what reduces it. Values are the game's stylised
// index, not tonnes of CO₂. The ledger is not saved: it is a live reading that refills within a minute after loading.
public sealed class EcoCarbonFootprint : MonoBehaviour
{
    public enum Source { None, Industry, Fire, Generator, Finale, Trees, Nature }
    private const int Count = 7;
    public static readonly string[] Names = { "", "Sanayi (fabrikalar)", "Yangınlar", "Evin yedek jeneratörü", "İklim canavarı (final)", "Ağaçlar", "Bitki örtüsü ve doğal toparlanma" };
    private static readonly string[] Icons = { "", "poi_factory", "poi_fire", "electricity", "poi_enemy", "crop_tree", "biodiversity" };
    private static readonly string[] Tips =
    {
        "",
        "Fabrikalara büyük filtre tak ve sonra temiz enerjiye dönüştür: dönüşüm karbonu %90 azaltır. Filtre tek başına yalnız havayı temizler, karbonu değil.",
        "Yangınları vakumun su moduyla söndür. Yanan orman hem karbon salar hem de karbon tutan ağaçları yok eder.",
        "Batarya gece boşalınca ev yedek dizel jeneratöre geçiyor. Ev sayacında tasarruf yap ve bataryayı gündüz doldur.",
        "Final sırasında iklim odaklarını dengelemek baskıyı bitirir.",
        "", ""
    };

    public static EcoCarbonFootprint Current { get; private set; }
    private static readonly float[] today = new float[Count], yesterday = new float[Count], rate = new float[Count], frame = new float[Count];
    private static int day = -1;
    private static bool hasYesterday;
    private static float totalArea, areaAt = -10f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        System.Array.Clear(today, 0, Count); System.Array.Clear(yesterday, 0, Count);
        System.Array.Clear(rate, 0, Count); System.Array.Clear(frame, 0, Count);
        day = -1; hasYesterday = false; totalArea = 0f; areaAt = -10f;
    }

    /// <summary>Called by EcoRegion.AddPollution with the carbon change it actually applied (after clamping).</summary>
    public static void Record(EcoRegion region, float carbonDelta, Source source)
    {
        if (source == Source.None || region == null || carbonDelta == 0f || !EcoRegion.Finite(carbonDelta)) return;
        float weighted = carbonDelta * Share(region);
        today[(int)source] += weighted; frame[(int)source] += weighted;
    }

    private static float Share(EcoRegion region)
    {
        if (areaAt < 0f || Time.unscaledTime - areaAt > 2f)
        {
            totalArea = 0f;
            foreach (var r in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None)) if (r != null && r.isActiveAndEnabled) totalArea += Mathf.Abs(r.size.x * r.size.y);
            areaAt = Time.unscaledTime;
        }
        return totalArea > 0f ? Mathf.Abs(region.size.x * region.size.y) / totalArea : 0f;
    }

    // ---- readings (world-carbon points)
    public static float DayLength => EcoWorldClock.Current != null ? EcoWorldClock.Current.dayLengthSeconds : 900f;
    /// <summary>Current pace of a source, extrapolated to one game day.</summary>
    public static float PerDay(Source s) => rate[(int)s] * DayLength;
    public static float NetPerDay { get { float sum = 0f; for (int i = 1; i < Count; i++) sum += rate[i]; return sum * DayLength; } }
    public static float EmissionsPerDay { get { float sum = 0f; for (int i = 1; i < Count; i++) if (rate[i] > 0f) sum += rate[i]; return sum * DayLength; } }
    public static float SinksPerDay { get { float sum = 0f; for (int i = 1; i < Count; i++) if (rate[i] < 0f) sum += rate[i]; return sum * DayLength; } }
    public static float NetToday { get { float sum = 0f; for (int i = 1; i < Count; i++) sum += today[i]; return sum; } }
    public static float NetYesterday { get { float sum = 0f; for (int i = 1; i < Count; i++) sum += yesterday[i]; return sum; } }
    public static bool HasYesterday => hasYesterday;
    public static Source Biggest
    {
        get
        {
            Source best = Source.None; float most = 0f;
            for (int i = 1; i < Count; i++) if (rate[i] > most) { most = rate[i]; best = (Source)i; }
            return best;
        }
    }
    public static string Signed(float v, string format = "0.0") => (v > 0.005f ? "+" : v < -0.005f ? "−" : "") + Mathf.Abs(v).ToString(format);

    private bool open;
    private Vector2 scroll;

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }
    private void OnDisable() { if (open) SetOpen(false); }

    private void Update()
    {
        var clock = EcoWorldClock.Current;
        if (clock != null && clock.day != day)
        {
            if (day >= 0) { System.Array.Copy(today, yesterday, Count); hasYesterday = true; }
            System.Array.Clear(today, 0, Count);
            day = clock.day;
        }
        float dt = Time.deltaTime;
        if (dt > 0f)
        {
            float k = 1f - Mathf.Exp(-dt / 10f); // ~10 s smoothing
            for (int i = 1; i < Count; i++) { rate[i] = Mathf.Lerp(rate[i], frame[i] / dt, k); frame[i] = 0f; }
        }
        if (open)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { EcoUIFlow.ConsumeEscape(); SetOpen(false); }
            if (!EcoUIFlow.IsPanel(this)) open = false;
        }
    }

    public bool PanelOpen => open;
    public void SetOpen(bool value) { open = value; EcoUIFlow.SetPanel(this, value); }
    public void Toggle() { if (!PlayerController.Paused) SetOpen(!open); }

    private void OnGUI()
    {
        if (!open) return;
        EcoTheme.Begin();
        var r = EcoTheme.Window(760f, 700f, "KARBON AYAK İZİ", "Yerleşimindeki kararların dünya karbonuna günlük katkısı. Değerler oyunun stilize endeksidir, ton CO₂ değildir.", "carbon");
        if (EcoTheme.CloseButton(r)) { SetOpen(false); EcoTheme.End(); return; }
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 112f;
        float world = EcoRegion.WorldCarbon(), net = NetPerDay;
        GUI.Label(new Rect(x, y, w, 28f), "Dünya karbonu " + world.ToString("0") + "/100 · CO₂ ≈ " + (425f + world * 2.5f).ToString("0") + " ppm", EcoTheme.Subtitle); y += 32f;
        GUI.Label(new Rect(x, y, w, 24f), "Buzulu koruma hedefi (P6): dünya karbonu ≤ 20 · " + (world <= 20f ? "şu an hedefin altında" : "hedefin " + (world - 20f).ToString("0") + " puan üstünde"), EcoTheme.Muted); y += 28f;
        string trend = net > 0.05f ? "artıyor: buzul erimeye devam eder" : net < -0.05f ? "azalıyor: doğru yoldasın" : "dengede";
        GUI.Label(new Rect(x, y, w, 26f), "Şu anki gidiş: " + Signed(net) + " puan/gün (" + trend + ")", EcoTheme.Body); y += 30f;
        GUI.Label(new Rect(x, y, w, 24f), "Bugün şimdiye kadar: " + Signed(NetToday, "0.00") + (hasYesterday ? " · Dün: " + Signed(NetYesterday, "0.00") : "")
            + " · Kaynaklar " + Signed(EmissionsPerDay) + " · Yutaklar " + Signed(SinksPerDay) + " /gün", EcoTheme.Muted); y += 34f;

        // Tip for the biggest source.
        var big = Biggest;
        string tip = big != Source.None && Tips[(int)big].Length > 0 ? "En büyük kaynağın: " + Names[(int)big] + ". " + Tips[(int)big]
            : SinksPerDay > -0.05f ? "Kaynak yok denecek kadar az. Ağaç dikerek ve bitki örtüsünü büyüterek karbonu daha da düşürebilirsin." : "Şu an hiçbir kaynak karbon eklemiyor; ağaçlar ve doğa karbonu düşürüyor.";
        float tipHeight = Mathf.Max(48f, EcoTheme.Body.CalcHeight(new GUIContent(tip), w - 70f) + 20f);
        GUI.Box(new Rect(x, y, w, tipHeight), GUIContent.none, EcoTheme.Card);
        EcoTheme.Icon(new Rect(x + 14f, y + 12f, 26f, 26f), big != Source.None ? Icons[(int)big] : "crop_tree");
        GUI.Label(new Rect(x + 52f, y + 10f, w - 66f, tipHeight - 16f), tip, EcoTheme.Body); y += tipHeight + 12f;

        var viewport = new Rect(x, y, w, r.yMax - 80f - y);
        float inner = w - 22f, rowH = 82f;
        float max = 0.01f; for (int i = 1; i < Count; i++) max = Mathf.Max(max, Mathf.Abs(PerDay((Source)i)));
        scroll.x = 0f;
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, inner, (Count - 1) * (rowH + 8f)), false, true);
        float cy = 0f;
        for (int i = 1; i < Count; i++)
        {
            float v = PerDay((Source)i);
            GUI.Box(new Rect(0, cy, inner, rowH), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(20, cy + 22, 30, 30), Icons[i]);
            GUI.Label(new Rect(62, cy + 18, inner - 220, 26), Names[i], EcoTheme.Body);
            GUI.Label(new Rect(inner - 166, cy + 18, 146, 26), Signed(v, "0.00") + " /gün", EcoTheme.Body);
            var track = new Rect(62, cy + 54, inner - 86, 9);
            var prev = GUI.color;
            GUI.color = EcoUI.Track; GUI.DrawTexture(track, Texture2D.whiteTexture);
            GUI.color = v > 0f ? new Color(0.86f, 0.42f, 0.28f) : EcoSettings.Good;
            GUI.DrawTexture(new Rect(track.x, track.y, track.width * Mathf.Clamp01(Mathf.Abs(v) / max), track.height), Texture2D.whiteTexture);
            GUI.color = prev;
            cy += rowH + 8f;
        }
        GUI.EndScrollView();
        if (EcoTheme.IconButton(new Rect(r.x + 40f, r.yMax - 66f, r.width - 80f, 40f), "Kapat  [Esc]", "ui_close")) SetOpen(false);
        EcoTheme.End();
    }
}
