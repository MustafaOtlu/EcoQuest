using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Environment layer of the HUD (GDD "Kullanıcı Arayüzü" / düzeltme #12):
//  - five separate indicators instead of one "pollution" number: air quality, water quality,
//    carbon footprint, biodiversity, ecosystem health (+ temperature),
//  - a mini map with the player arrow, a full map screen on [M],
//  - a banner shown when the player enters a new region and a short intro title.
// It sits beside EcoQuestHUD (same scale rules, same palette) and reads only public simulation data.
[DefaultExecutionOrder(45)]
public sealed class EcoWorldHUD : MonoBehaviour
{
    public Texture2D minimapTexture;
    public Sprite panelSprite;
    public Vector2 worldOrigin = Vector2.zero;
    public Vector2 worldSize = new Vector2(640f, 640f);
    public string introTitle = "ECOQUEST";
    public string introLine = "Bugünkü çevresel kararların, gelecek nesillerin yaşam koşullarını belirler.";
    public float introSeconds = 5f;

    private static readonly Color Surface = new Color(0.035f, 0.075f, 0.09f, 0.90f);
    private static readonly Color Ink = new Color(0.94f, 0.97f, 0.95f);
    private static readonly Color Muted = new Color(0.67f, 0.77f, 0.77f);
    private static readonly Color Accent = new Color(0.43f, 0.91f, 0.66f);
    private static readonly Color Track = new Color(0.15f, 0.22f, 0.24f, 1f);
    private static readonly string[] RowNames = { "Hava kalitesi", "Su kalitesi", "Karbon ayak izi", "Biyoçeşitlilik", "Ekosistem sağlığı" };
    private static readonly string[] Compass = { "K", "KD", "D", "GD", "G", "GB", "B", "KB" };
    private const int RasterScale = 2;

    private PlayerWeaponSystem weapons;
    private Transform player;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Font font;
    private Sprite fallbackPanel, arrowSprite;
    private RectTransform envPanel, mapPanel, banner, intro, bigMap, bigFrame;
    private RectTransform miniFrame, miniArrow, miniShadow, bigArrow, bigShadow;
    private RectTransform[] rowTracks = new RectTransform[5];
    private Image[] rowFills = new Image[5];
    private Text[] rowNames = new Text[5], rowValues = new Text[5];
    private Text header, regionName, tempName, tempValue, mapHeader, mapHint, mapFooter, bannerTitle, bannerSub, bannerState;
    private Text introTitleText, introLineText, introHint, bigTitle, bigHint;
    private Image bannerStrip, introDim, bigDim, tempDot;
    private CanvasGroup bannerGroup, introGroup;
    private RawImage miniImage, bigImage;
    private readonly float[] shown = new float[5];
    private readonly float[] target = new float[5];
    private readonly float[] good = new float[5];
    private readonly List<Text> bigLabels = new List<Text>();
    private readonly List<EcoRegion> regions = new List<EcoRegion>();
    private EcoRegion current;
    private float refreshAt, regionScanAt, bannerUntil, startTime;
    private float shownTemp;
    private int lastWidth, lastHeight, labelRegionCount = -1;
    private float miniSide = 250f, bigSide = 600f;
    private bool mapOpen;

    private void Start()
    {
        startTime = Time.unscaledTime;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        fallbackPanel = panelSprite != null ? panelSprite : MakeRounded();
        arrowSprite = MakeArrow();
        BuildUi();
        ApplyLayout();
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // ---------------------------------------------------------------- update

    private void Update()
    {
        if (canvas == null) return;
        canvas.enabled = !EcoMarketUI.IsOpen;
        if (lastWidth != DisplayWidth || lastHeight != DisplayHeight) ApplyLayout();
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) SetMap(!mapOpen);
        if (mapOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) SetMap(false);

        AnimateIntro();
        if (!FindPlayer()) return;

        if (Time.unscaledTime >= regionScanAt)
        {
            regionScanAt = Time.unscaledTime + 2f;
            regions.Clear();
            regions.AddRange(FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID));
            if (bigMap != null && regions.Count != labelRegionCount) RebuildBigLabels();
        }

        var region = EcoRegion.At(player.position);
        if (region != current)
        {
            current = region;
            if (current != null) ShowBanner(current);
        }

        if (Time.unscaledTime >= refreshAt)
        {
            refreshAt = Time.unscaledTime + 0.1f;
            Sample(current);
        }
        AnimateValues();
        UpdateMapMarkers();
        UpdateBanner();
    }

    private bool FindPlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy) return true;
        if (weapons == null || !weapons.isActiveAndEnabled) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (weapons == null) return false;
        var vitals = weapons.GetComponentInParent<PlayerVitals>();
        player = vitals != null ? vitals.transform : weapons.transform;
        return true;
    }

    // ------------------------------------------------------------ simulation

    private void Sample(EcoRegion r)
    {
        if (r == null)
        {
            for (int i = 0; i < 5; i++) { target[i] = 0f; good[i] = 0f; }
            return;
        }
        float air = 100f - r.airPollution, water = 100f - r.waterPollution, soil = 100f - r.soilPollution;
        float recovery = r.TryGetComponent<EcoHabitat>(out var habitat) ? habitat.recovery : 0f;
        float bio = Mathf.Clamp(r.vegetation * 55f + recovery * 30f + water * 0.15f, 0f, 100f);
        float eco = Mathf.Clamp(air * 0.30f + water * 0.25f + soil * 0.20f + bio * 0.25f, 0f, 100f);
        target[0] = air; target[1] = water; target[2] = r.carbon; target[3] = bio; target[4] = eco;
    }

    private void AnimateValues()
    {
        float k = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
        for (int i = 0; i < 5; i++)
        {
            shown[i] = Mathf.Lerp(shown[i], target[i], k);
            good[i] = i == 2 ? 1f - shown[i] / 100f : shown[i] / 100f;
            float width = rowTracks[i].sizeDelta.x * (current != null ? Mathf.Clamp01(shown[i] / 100f) : 0f);
            rowFills[i].rectTransform.sizeDelta = new Vector2(width, rowTracks[i].sizeDelta.y);
            rowFills[i].color = GoodColor(good[i]);
            rowValues[i].text = current != null ? Mathf.RoundToInt(shown[i]) + " / 100" : "—";
        }
        float temp = current != null ? current.Temperature : 0f;
        shownTemp = current != null ? Mathf.Lerp(shownTemp, temp, k) : shownTemp;
        regionName.text = current != null ? current.regionName : "Bölge dışı";
        if (current == null)
        {
            tempValue.text = "—"; tempDot.color = Muted; return;
        }
        string tag = shownTemp < 0f ? "Dondurucu" : shownTemp < 10f ? "Soğuk" : shownTemp < 25f ? "Ilıman" : shownTemp < 32f ? "Sıcak" : "Aşırı sıcak";
        tempValue.text = shownTemp.ToString("0.0") + " °C  ·  " + tag;
        tempDot.color = shownTemp < 0f ? new Color(0.6f, 0.85f, 1f) : shownTemp < 10f ? new Color(0.5f, 0.78f, 1f)
            : shownTemp < 25f ? Accent : shownTemp < 32f ? new Color(1f, 0.79f, 0.31f) : new Color(1f, 0.45f, 0.3f);
    }

    private static Color GoodColor(float g)
    {
        g = Mathf.Clamp01(g);
        var bad = new Color(0.94f, 0.36f, 0.31f);
        var mid = new Color(1f, 0.79f, 0.31f);
        var fine = new Color(0.34f, 0.86f, 0.59f);
        return g < 0.5f ? Color.Lerp(bad, mid, g * 2f) : Color.Lerp(mid, fine, (g - 0.5f) * 2f);
    }

    private static string StateName(float health) => health >= 70f ? "SAĞLIKLI" : health >= 45f ? "DENGEDE" : "KİRLİ";

    // ------------------------------------------------------------ map / banner

    private void SetMap(bool open)
    {
        mapOpen = open;
        if (bigMap != null) bigMap.gameObject.SetActive(open);
        if (bigDim != null) bigDim.gameObject.SetActive(open);
        if (open && bigMap != null && regions.Count != labelRegionCount) RebuildBigLabels();
    }

    private Vector2 ToUv(Vector3 world) => new Vector2(
        Mathf.Clamp01((world.x - worldOrigin.x) / Mathf.Max(1f, worldSize.x)),
        Mathf.Clamp01((world.z - worldOrigin.y) / Mathf.Max(1f, worldSize.y)));

    private void UpdateMapMarkers()
    {
        var uv = ToUv(player.position);
        float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : player.eulerAngles.y;
        PlaceMarker(miniArrow, miniShadow, miniFrame, uv, yaw, miniSide);
        if (mapOpen) PlaceMarker(bigArrow, bigShadow, bigFrame, uv, yaw, bigSide);
        int index = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 45f) % 8;
        mapFooter.text = "X " + Mathf.RoundToInt(player.position.x) + "  ·  Z " + Mathf.RoundToInt(player.position.z)
            + "  ·  Yön " + Compass[index] + " " + Mathf.RoundToInt(Mathf.Repeat(yaw, 360f)) + "°";
    }

    private static void PlaceMarker(RectTransform arrow, RectTransform shadow, RectTransform frame, Vector2 uv, float yaw, float side)
    {
        if (arrow == null || frame == null) return;
        var position = new Vector2(uv.x * side, -(1f - uv.y) * side);
        arrow.anchoredPosition = position; shadow.anchoredPosition = position;
        arrow.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        shadow.localRotation = arrow.localRotation;
    }

    private void RebuildBigLabels()
    {
        foreach (var label in bigLabels) if (label != null) Destroy(label.gameObject);
        bigLabels.Clear();
        labelRegionCount = regions.Count;
        foreach (var region in regions)
        {
            if (region == null) continue;
            var info = region.GetComponent<EcoRegionInfo>();
            var uv = ToUv(region.transform.position);
            var text = Label(bigFrame, "Region label", region.regionName.ToUpperInvariant(),
                new Vector2(uv.x * bigSide - 90f, -(1f - uv.y) * bigSide + 9f), new Vector2(180f, 18f), 12, TextAnchor.MiddleCenter, Ink, true);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f); outline.effectDistance = new Vector2(1.5f, -1.5f);
            if (info != null && info.kind == EcoRegionInfo.Kind.Lake) text.color = new Color(0.62f, 0.86f, 1f);
            bigLabels.Add(text);
        }
    }

    private void ShowBanner(EcoRegion region)
    {
        var info = region.GetComponent<EcoRegionInfo>();
        bannerTitle.text = region.regionName;
        bannerSub.text = info != null ? info.tagline : "";
        bannerStrip.color = info != null ? info.mapColor : Accent;
        float health = region.vegetation * 20f + (100f - region.airPollution) * 0.3f + (100f - region.waterPollution) * 0.25f
            + (100f - region.soilPollution) * 0.25f;
        bannerState.text = StateName(health);
        bannerState.color = GoodColor(health / 100f);
        bannerUntil = Time.unscaledTime + 5f;
    }

    private void UpdateBanner()
    {
        float left = bannerUntil - Time.unscaledTime;
        float alpha = left <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(left, 5f - left) * 2.5f);
        bannerGroup.alpha = alpha;
    }

    private void AnimateIntro()
    {
        if (introGroup == null) return;
        float t = Time.unscaledTime - startTime;
        float alpha = t < introSeconds ? 1f : Mathf.Clamp01(1f - (t - introSeconds) / 1.6f);
        introGroup.alpha = alpha;
        if (alpha <= 0f && intro != null) { Destroy(intro.gameObject); intro = null; introGroup = null; }
    }

    // ------------------------------------------------------------------- UI

    private int DisplayWidth => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.width));
    private int DisplayHeight => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.height));

    private void BuildUi()
    {
        var root = new GameObject("EcoWorld HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); root.layer = 5;
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 19; canvas.pixelPerfect = true;
        scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        // Environment indicators (below the vitals panel).
        envPanel = Panel("Environment", root.transform, new Vector2(0, 1), Vector2.zero, new Vector2(296, 252), new Vector2(0, 1), Surface);
        Strip(envPanel, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(3, 252), new Vector2(0, 1), Accent);
        header = Label(envPanel, "Heading", "ÇEVRE DURUMU", new Vector2(16, -10), new Vector2(200, 18), 12, TextAnchor.MiddleLeft, Accent, true);
        regionName = Label(envPanel, "Region", "Bölge dışı", new Vector2(16, -28), new Vector2(264, 24), 18, TextAnchor.MiddleLeft, Ink, true);
        for (int i = 0; i < 5; i++)
        {
            rowNames[i] = Label(envPanel, "Name " + i, RowNames[i], new Vector2(16, 0), new Vector2(160, 20), 15, TextAnchor.MiddleLeft, Muted);
            rowValues[i] = Label(envPanel, "Value " + i, "—", new Vector2(16, 0), new Vector2(90, 20), 15, TextAnchor.MiddleRight, Ink, true);
            rowTracks[i] = Panel("Track " + i, envPanel, new Vector2(0, 1), Vector2.zero, new Vector2(264, 5), new Vector2(0, 1), Track);
            rowFills[i] = Panel("Fill", rowTracks[i], new Vector2(0, 1), Vector2.zero, new Vector2(0, 5), new Vector2(0, 1), Accent).GetComponent<Image>();
        }
        tempDot = Panel("Temperature dot", envPanel, new Vector2(0, 1), Vector2.zero, new Vector2(10, 10), new Vector2(0, 1), Accent).GetComponent<Image>();
        tempName = Label(envPanel, "Temperature name", "Sıcaklık", new Vector2(32, 0), new Vector2(110, 20), 15, TextAnchor.MiddleLeft, Muted);
        tempValue = Label(envPanel, "Temperature value", "—", new Vector2(120, 0), new Vector2(160, 20), 15, TextAnchor.MiddleRight, Ink, true);

        // Mini map (below the resources panel).
        mapPanel = Panel("Map", root.transform, new Vector2(1, 1), Vector2.zero, new Vector2(274, 330), new Vector2(1, 1), Surface);
        mapHeader = Label(mapPanel, "Heading", "HARİTA", new Vector2(14, -10), new Vector2(120, 18), 12, TextAnchor.MiddleLeft, Accent, true);
        mapHint = Label(mapPanel, "Hint", "[M] Büyüt", new Vector2(130, -10), new Vector2(130, 18), 12, TextAnchor.MiddleRight, Muted);
        miniFrame = Panel("Frame", mapPanel, new Vector2(0, 1), new Vector2(12, -32), new Vector2(250, 250), new Vector2(0, 1), new Color(0.02f, 0.04f, 0.05f, 1f));
        miniImage = MapImage(miniFrame, "Map image");
        MakeMarker(miniFrame, out miniArrow, out miniShadow, 16f);
        var north = Label(miniFrame, "North", "K", new Vector2(0, -3), new Vector2(250, 16), 12, TextAnchor.MiddleCenter, Ink, true);
        north.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        mapFooter = Label(mapPanel, "Coordinates", "", new Vector2(12, -290), new Vector2(250, 30), 13, TextAnchor.MiddleLeft, Muted);

        // Region banner.
        banner = Panel("Region banner", root.transform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(460, 70), new Vector2(0.5f, 1), Surface);
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.alpha = 0f; bannerGroup.interactable = false; bannerGroup.blocksRaycasts = false;
        bannerStrip = Strip(banner, "Strip", new Vector2(0, 1), Vector2.zero, new Vector2(5, 70), new Vector2(0, 1), Accent);
        bannerTitle = Label(banner, "Title", "", new Vector2(20, -9), new Vector2(300, 28), 22, TextAnchor.MiddleLeft, Ink, true);
        bannerState = Label(banner, "State", "", new Vector2(320, -12), new Vector2(125, 20), 13, TextAnchor.MiddleRight, Accent, true);
        bannerSub = Label(banner, "Tagline", "", new Vector2(20, -39), new Vector2(425, 24), 14, TextAnchor.MiddleLeft, Muted);

        // Full map screen.
        bigDim = Panel("Map dim", root.transform, new Vector2(0, 0), Vector2.zero, new Vector2(10, 10), new Vector2(0, 0), new Color(0f, 0f, 0f, 0.62f)).GetComponent<Image>();
        bigDim.rectTransform.anchorMin = Vector2.zero; bigDim.rectTransform.anchorMax = Vector2.one;
        bigDim.rectTransform.offsetMin = bigDim.rectTransform.offsetMax = Vector2.zero;
        bigDim.sprite = null; bigDim.type = Image.Type.Simple;
        bigMap = Panel("Big map", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 690), new Vector2(0.5f, 0.5f), Surface);
        bigTitle = Label(bigMap, "Title", "DÜNYA HARİTASI", new Vector2(18, -12), new Vector2(300, 22), 16, TextAnchor.MiddleLeft, Accent, true);
        bigHint = Label(bigMap, "Hint", "[M] / [Esc] Kapat", new Vector2(300, -14), new Vector2(322, 20), 13, TextAnchor.MiddleRight, Muted);
        bigFrame = Panel("Frame", bigMap, new Vector2(0, 1), new Vector2(20, -44), new Vector2(600, 600), new Vector2(0, 1), new Color(0.02f, 0.04f, 0.05f, 1f));
        bigImage = MapImage(bigFrame, "Map image");
        MakeMarker(bigFrame, out bigArrow, out bigShadow, 26f);
        bigMap.gameObject.SetActive(false); bigDim.gameObject.SetActive(false);

        // Intro title.
        intro = Panel("Intro", root.transform, new Vector2(0, 0), Vector2.zero, new Vector2(10, 10), new Vector2(0, 0), new Color(0.02f, 0.05f, 0.06f, 0.88f));
        intro.GetComponent<Image>().sprite = null; intro.GetComponent<Image>().type = Image.Type.Simple;
        intro.anchorMin = Vector2.zero; intro.anchorMax = Vector2.one; intro.offsetMin = intro.offsetMax = Vector2.zero;
        introGroup = intro.gameObject.AddComponent<CanvasGroup>(); introGroup.interactable = false; introGroup.blocksRaycasts = false;
        introTitleText = Label(intro, "Title", introTitle, Vector2.zero, new Vector2(800, 70), 56, TextAnchor.MiddleCenter, Accent, true);
        introLineText = Label(intro, "Line", introLine, Vector2.zero, new Vector2(800, 60), 21, TextAnchor.MiddleCenter, Ink);
        introHint = Label(intro, "Hint", "[M] Harita   ·   [1–5] Araçlar   ·   [B] İnşa   ·   [E] Etkileşim", Vector2.zero, new Vector2(800, 24), 15, TextAnchor.MiddleCenter, Muted);
    }

    private RawImage MapImage(RectTransform frame, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(RawImage)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(frame, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(2f, 2f); rect.offsetMax = new Vector2(-2f, -2f);
        var image = go.GetComponent<RawImage>(); image.texture = minimapTexture; image.raycastTarget = false;
        image.color = minimapTexture != null ? Color.white : new Color(0.2f, 0.3f, 0.3f);
        return image;
    }

    private void MakeMarker(RectTransform frame, out RectTransform arrow, out RectTransform shadow, float size)
    {
        shadow = Marker(frame, "Arrow shadow", size * 1.45f, new Color(0f, 0f, 0f, 0.75f));
        arrow = Marker(frame, "Player arrow", size, Accent);
    }

    private RectTransform Marker(RectTransform frame, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(frame, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>(); image.sprite = arrowSprite; image.color = color; image.raycastTarget = false;
        return rect;
    }

    private void ApplyLayout()
    {
        lastWidth = DisplayWidth; lastHeight = DisplayHeight;
        // Same scale rule as EcoQuestHUD so both layers line up at every resolution.
        float scale = Mathf.Clamp(Mathf.Min(lastWidth / 1600f, lastHeight / 900f), 0.8f, 1.5f);
        scaler.scaleFactor = scale; canvas.scaleFactor = scale;
        float width = lastWidth / scale, height = lastHeight / scale;
        const float margin = 18f;
        bool compact = width < 1250f;
        float vitalsWidth = compact ? 266f : 296f, resourcesWidth = compact ? 248f : 274f;

        // Environment panel.
        Place(envPanel, new Vector2(margin, -(margin + 202f + 10f)), new Vector2(vitalsWidth, 252f));
        float inner = vitalsWidth - 32f;
        for (int i = 0; i < 5; i++)
        {
            float y = -58f - i * 31f;
            LayoutText(rowNames[i], new Vector2(16f, y), new Vector2(inner - 92f, 20f));
            LayoutText(rowValues[i], new Vector2(16f + inner - 90f, y), new Vector2(90f, 20f));
            rowTracks[i].anchoredPosition = new Vector2(16f, y - 21f); rowTracks[i].sizeDelta = new Vector2(inner, 5f);
        }
        float ty = -58f - 5 * 31f;
        tempDot.rectTransform.anchoredPosition = new Vector2(16f, ty - 5f);
        LayoutText(tempName, new Vector2(32f, ty), new Vector2(80f, 20f));
        LayoutText(tempValue, new Vector2(112f, ty), new Vector2(inner - 96f, 20f));
        LayoutText(regionName, new Vector2(16f, -28f), new Vector2(inner, 24f));

        // Mini map.
        miniSide = resourcesWidth - 24f;
        Place(mapPanel, new Vector2(-margin, -(margin + 188f + 10f)), new Vector2(resourcesWidth, miniSide + 80f));
        LayoutText(mapHint, new Vector2(resourcesWidth - 144f, -10f), new Vector2(130f, 18f));
        Place(miniFrame, new Vector2(12f, -32f), new Vector2(miniSide, miniSide));
        miniArrow.sizeDelta = Vector2.one * 16f; miniShadow.sizeDelta = Vector2.one * 23f;
        LayoutText(mapFooter, new Vector2(12f, -(miniSide + 40f)), new Vector2(miniSide, 30f));

        // Banner and intro.
        float bannerWidth = Mathf.Min(460f, width - vitalsWidth - resourcesWidth - 4f * margin);
        float center = (vitalsWidth - resourcesWidth) * 0.5f;
        Place(banner, new Vector2(center, -(margin + 92f + 8f)), new Vector2(Mathf.Max(300f, bannerWidth), 70f));
        float bw = banner.sizeDelta.x;
        LayoutText(bannerTitle, new Vector2(20f, -9f), new Vector2(bw - 150f, 28f));
        LayoutText(bannerState, new Vector2(bw - 135f, -12f), new Vector2(125f, 20f));
        LayoutText(bannerSub, new Vector2(20f, -39f), new Vector2(bw - 32f, 24f));
        if (intro != null)
        {
            LayoutCentered(introTitleText, 40f, 800f, 70f, width);
            LayoutCentered(introLineText, -30f, 800f, 60f, width);
            LayoutCentered(introHint, -130f, 800f, 24f, width);
        }

        // Full map.
        bigSide = Mathf.Clamp(Mathf.Min(width - 80f, height - 150f), 280f, 640f);
        Place(bigMap, Vector2.zero, new Vector2(bigSide + 40f, bigSide + 70f));
        Place(bigFrame, new Vector2(20f, -44f), new Vector2(bigSide, bigSide));
        bigArrow.sizeDelta = Vector2.one * 26f; bigShadow.sizeDelta = Vector2.one * 38f;
        LayoutText(bigHint, new Vector2(bigSide - 280f, -14f), new Vector2(300f, 20f));
        labelRegionCount = -1; // positions depend on the map size
        if (mapOpen) RebuildBigLabels();
    }

    private static void LayoutCentered(Text text, float y, float w, float h, float screenWidth)
    {
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f); rect.pivot = new Vector2(0.5f, 0.5f);
        float use = Mathf.Min(w, screenWidth - 40f);
        rect.anchoredPosition = new Vector2(0f, y); rect.sizeDelta = new Vector2(use, h) * RasterScale;
        rect.localScale = Vector3.one / RasterScale;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size) { rect.anchoredPosition = position; rect.sizeDelta = size; }

    private static void LayoutText(Text text, Vector2 position, Vector2 size)
    {
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size * RasterScale;
        text.rectTransform.localScale = Vector3.one / RasterScale;
    }

    private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color tint)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; Place(rect, position, size);
        var image = go.GetComponent<Image>(); image.sprite = fallbackPanel; image.type = Image.Type.Sliced;
        image.color = tint; image.raycastTarget = false;
        return rect;
    }

    private Image Strip(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color)
    {
        var image = Panel(name, parent, anchor, position, size, pivot, color).GetComponent<Image>();
        image.sprite = null; image.type = Image.Type.Simple; return image;
    }

    private Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor align, Color color, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        var text = go.GetComponent<Text>(); LayoutText(text, position, size);
        text.font = font; text.fontSize = fontSize * RasterScale; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color; text.alignment = align; text.text = content; text.raycastTarget = false; text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    // ------------------------------------------------------ procedural sprites

    private static Sprite MakeRounded()
    {
        const int s = 40, r = 11;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "EcoWorld panel" };
        var pixels = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, s - r), cy = Mathf.Clamp(y + 0.5f, r, s - r);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(r - d + 0.5f) * 255f);
                pixels[y * s + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }

    private static Sprite MakeArrow()
    {
        const int s = 48;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "EcoWorld arrow" };
        var pixels = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                // Arrow head: apex at the top, notch at the bottom.
                float v = y / (s - 1f), u = (x + 0.5f) / s - 0.5f;
                float half = Mathf.Lerp(0.0f, 0.42f, Mathf.Clamp01((1f - v) / 0.92f));
                float notch = Mathf.Lerp(0f, 0.2f, Mathf.Clamp01((0.3f - v) / 0.3f));
                bool inside = v > 0.04f && Mathf.Abs(u) <= half && !(v < 0.3f && Mathf.Abs(u) < notch * (1f - v / 0.3f) * 1.6f);
                pixels[y * s + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }
        tex.SetPixels32(pixels); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
    }
}
