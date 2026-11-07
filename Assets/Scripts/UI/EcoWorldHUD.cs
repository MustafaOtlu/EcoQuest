using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Tone = EcoHudStyle.Tone;

// World layer of the HUD (GDD §13):
//  - environment card: the region's air, water, carbon, biodiversity, ecosystem health and temperature, with icons,
//  - a zoomed mini map that follows the player (north up) with the main points of interest,
//  - the [M] world map: zoom (wheel), pan (drag), region outlines and weather/wind symbols, points of interest and an
//    information card for whatever the mouse is over (state with real readings, climate, live tasks). Click pins a region.
//  - the [G] quest board: every open job grouped by region, easy → hard; a click tracks one for the objective arrow,
//  - the quick-action column (map, quests, journal, build, skills, settings, help, HUD mode), the [F1] controls sheet,
//    the region banner and the intro title. [H] cycles the HUD between Tam, Sade and Gizli.
[DefaultExecutionOrder(45)]
public sealed class EcoWorldHUD : MonoBehaviour
{
    public Texture2D minimapTexture;
    public Sprite panelSprite;
    public Vector2 worldOrigin = Vector2.zero;
    public Vector2 worldSize = new Vector2(150f, 150f);
    public string introTitle = "ECOQUEST";
    public string introLine = "Bugünkü çevresel kararların, gelecek nesillerin yaşam koşullarını belirler.";
    public float introSeconds = 5f;

    /// <summary>Bottom edge (canvas units from the top) of the map card; EcoQuestHUD stacks the resources card below.</summary>
    public static float MapBottom { get; private set; } = 282f;
    internal const float CompactMapWidth = 170f;
    /// <summary>Bottom edge of the region banner while it is visible (0 when hidden); other top-centre notices sit below it.</summary>
    public static float BannerBottom { get; private set; }
    private float bannerBottom;
    public static bool MapOpen { get; private set; }
    /// <summary>The world map or the quest board covers the screen: world markers and floating cards stay hidden.</summary>
    public static bool OverlayOpen => MapOpen || BoardOpen;

    private const float MiniWindow = 56f;  // metres shown across the corner map
    private static readonly string[] RowNames = { "Hava kalitesi", "Su kalitesi", "Toprak kalitesi", "Karbon yükü", "Biyoçeşitlilik", "Ekosistem sağlığı" };
    private static readonly string[] RowIcons = { "air", "water_quality", "organic", "carbon", "biodiversity", "ecosystem" };
    private static readonly string[] RowTips =
    {
        "Hava kalitesi = 100 − hava kirliliği. AQI: hava kalite indeksi (0–50 iyi, 51–100 orta, 150 üstü sağlıksız); PM2.5 ince toz, µg/m³. Fabrika filtresi ve yeşil alan iyileştirir.",
        "Su kalitesi = 100 − su kirliliği. Çözünmüş oksijen: sağlıklı gölde 8–10 mg/L, 4 mg/L altında balıklar zorlanır. Kirletici girişi kapat, arıtıp göle döndür.",
        "Toprak kalitesi = 100 − toprak kirliliği. Organik madde: verimli toprakta %3–6, %2 altı yorgun toprak. Atık toplamak ve gübre iyileştirir.",
        "Karbon yükü (düşük iyi). Havadaki CO₂ bugün dünya ortalamasında yaklaşık 425 ppm; baca ve trafik yanında yükselir. Temiz enerji ve habitat azaltır; buzul bunu izler.",
        "Biyoçeşitlilik: bitki örtüsü, habitat ve su kalitesinden hesaplanır.",
        "Ekosistem sağlığı: hava, su, toprak ve biyoçeşitliliğin ortak özeti.",
    };

    private PlayerWeaponSystem weapons;
    private readonly EcoHudStyle hudStyle = new EcoHudStyle();
    private Transform player;
    private Canvas canvas, overlay;
    private CanvasScaler scaler, overlayScaler;
    private int lastWidth, lastHeight;
    private float laidOutVitalsBottom = -1f;
    private float laidOutDockWidth = -1f, laidOutDockTop = -1f;
    private bool dirty = true, bannerFits = true;
    private EcoUI.HudMode lastMode;
    private float refreshAt, regionScanAt, bannerUntil, startTime, mapRefreshAt;
    private readonly List<EcoRegion> regions = new List<EcoRegion>();
    private EcoRegion current;

    // Environment card
    private EcoUI.Card envCard;
    private EcoQuestSidebar questSidebar;
    private readonly Image[] rowIcons = new Image[8], rowFills = new Image[8];
    private readonly RectTransform[] rowTracks = new RectTransform[8];
    private readonly Text[] rowNames = new Text[8], rowValues = new Text[8];
    private readonly float[] shown = new float[7], target = new float[7];

    // Mini map card
    private EcoUI.Card mapCard;
    private RectTransform miniFrame, miniArrow, miniStatus, miniWeatherHit;
    private RawImage miniImage;
    private Text miniNorth;
    private Button bigMapButton;
    private Image clockIcon, weatherIcon;
    private Text clockText, weatherText;
    private int shownWind = -1;
    private string announcedWeather = "";
    private readonly List<Image> miniPins = new List<Image>();

    // Banner & intro
    private RectTransform banner, intro;
    private CanvasGroup bannerGroup, introGroup;
    private Image bannerIcon, bannerStrip;
    private Text bannerTitle, bannerSub, bannerState;

    // Quick actions & help
    private RectTransform actions;
    private readonly List<(Button button, Text key, string id)> actionButtons = new List<(Button, Text, string)>();
    private RectTransform helpRoot, help;
    private static readonly Vector2 HelpSize = new Vector2(780f, 550f);
    private Image skillsAlert;
    private bool helpOpen;
    private readonly EcoUI.HudPanel helpOwner = new EcoUI.HudPanel();
    private Text modeToast; private float modeToastUntil; private Image modeToastBg;

    // Big map
    private RectTransform bigRoot, bigCard, bigFrame, bigView, infoCard, infoViewport, infoContent, legend;
    private ScrollRect infoScroll;
    private EcoRegion infoRegion;
    private readonly List<(Image icon, Text label)> legendItems = new List<(Image, Text)>();
    private RawImage bigImage;
    private RectTransform bigArrow, hintPin;
    private Text bigTitle, bigHint, coordText;
    private float zoom = 1f;
    private Vector2 viewCenter = new Vector2(0.5f, 0.5f);
    private bool dragging; private Vector2 dragStart, dragCenter; private bool dragMoved;
    private EcoRegion hovered, pinned;
    private sealed class RegionOverlay { public EcoRegion region; public EcoRegionInfo info; public Image fill; public Image[] edges; public RectTransform chip; public Image chipIcon, weather; public Text chipText; }
    private readonly List<RegionOverlay> overlays = new List<RegionOverlay>();
    private sealed class Poi { public Vector3 position; public string icon, title, detail; public float size = 26f; public bool pulse; }
    private readonly List<Poi> pois = new List<Poi>();
    private readonly List<Image> poiImages = new List<Image>();
    private Poi hoveredPoi;
    // Info card pieces
    private Image infoIcon, infoBadge; private Text infoName, infoTag, infoBadgeText, infoEmpty;
    private readonly Image[] infoRowIcon = new Image[6], infoRowFill = new Image[6]; private readonly RectTransform[] infoRowTrack = new RectTransform[6];
    private readonly Text[] infoRowName = new Text[6], infoRowValue = new Text[6], infoRowReading = new Text[6];
    private Text climateHeader, tasksHeader;
    private readonly Image[] climateIcon = new Image[4]; private readonly Text[] climateText = new Text[4];
    private readonly Image[] taskIcon = new Image[7], taskFill = new Image[7]; private readonly RectTransform[] taskTrack = new RectTransform[7]; private readonly Text[] taskText = new Text[7];
    private readonly Image[] taskHit = new Image[7]; private readonly EcoQuestBoard.Quest[] taskQuest = new EcoQuestBoard.Quest[7];

    // Quest board
    public static bool BoardOpen { get; private set; }
    private readonly EcoUI.HudPanel boardOwner = new EcoUI.HudPanel();
    private RectTransform boardRoot, boardCard, boardViewport, boardContent;
    private Text boardHint, boardEmpty;
    private float boardRefreshAt;
    private sealed class BoardRow { public RectTransform root, track; public Image bg, icon, chip, fill; public Text chipText, title, detail, action; public EcoQuestBoard.Quest quest; }
    private readonly List<BoardRow> boardRows = new List<BoardRow>();
    private readonly List<Text> boardHeaders = new List<Text>();

    private void Start()
    {
        startTime = Time.unscaledTime;
        hudStyle.Refresh(EcoWorldClock.Current);
        canvas = EcoUI.CreateCanvas("EcoWorld HUD", 19, out scaler);
        // The world map and the controls sheet sit above every HUD card (their dim covers the HUD).
        overlay = EcoUI.CreateCanvas("EcoWorld Map", 30, out overlayScaler);
        BuildEnvironmentCard();
        questSidebar = new EcoQuestSidebar(canvas.transform, hudStyle);
        BuildMiniMap();
        BuildBanner();
        BuildActions();
        BuildHelp();
        BuildBigMap();
        BuildBoard();
        BuildIntro();
        foreach (var root in new[] { envCard.root, mapCard.root, actions, banner, help, intro })
            EcoUI.RegisterWorldLabelObstacle(root);
        EcoUI.LayoutChanged += MarkDirty;
        lastMode = EcoUI.Mode;
        ApplyLayout();
    }

    private void MarkDirty() => dirty = true;

    private void OnDestroy()
    {
        EcoUI.LayoutChanged -= MarkDirty;
        if (MapOpen) EcoUIFlow.SetPanel(this, false);
        if (helpOpen) EcoUIFlow.SetPanel(helpOwner, false);
        if (BoardOpen) EcoUIFlow.SetPanel(boardOwner, false);
        MapOpen = false; BoardOpen = false;
        questSidebar?.Dispose();
        if (canvas != null) Destroy(canvas.gameObject);
        if (overlay != null) Destroy(overlay.gameObject);
    }

    // ================================================================== update

    private void Update()
    {
        if (canvas == null) return;
        hudStyle.Refresh(EcoWorldClock.Current);
        if (minimapTexture != null) miniImage.color = Color.Lerp(Color.white, new Color(0.74f, 0.78f, 0.77f), hudStyle.NightAmount);
        bool foreign = EcoMarketUI.IsOpen || EcoUI.ForeignWindowOpen;
        canvas.enabled = !foreign;
        overlay.enabled = !foreign;
        if (lastWidth != DisplayWidth || lastHeight != DisplayHeight || lastMode != EcoUI.Mode) dirty = true;
        if (Mathf.Abs(laidOutVitalsBottom - EcoQuestHUD.VitalsBottom) > 0.5f) dirty = true;
        if (Mathf.Abs(laidOutDockWidth - EcoQuestHUD.DockWidth) > 0.5f || Mathf.Abs(laidOutDockTop - EcoQuestHUD.DockTop) > 0.5f) dirty = true;
        if (dirty) ApplyLayout();
        HandleKeys();
        questSidebar?.SetVisible(!foreign && !OverlayOpen && !helpOpen && EcoUI.Mode != EcoUI.HudMode.Hidden);
        AnimateIntro();
        UpdateModeToast();
        if (!FindPlayer()) return;

        if (Time.unscaledTime >= regionScanAt)
        {
            regionScanAt = Time.unscaledTime + 2f;
            regions.Clear();
            regions.AddRange(FindObjectsByType<EcoRegion>(FindObjectsSortMode.InstanceID));
            if (overlays.Count != regions.Count(r => r != null)) RebuildOverlays();
        }
        var region = EcoRegion.At(player.position);
        if (region != current) { current = region; if (current != null) ShowBanner(current); }
        questSidebar?.Refresh(current, player.position);
        if (Time.unscaledTime >= refreshAt)
        {
            refreshAt = Time.unscaledTime + 0.1f;
            Sample(current, target);
            RefreshMiniPins();
            var clock = EcoWorldClock.Current;
            if (clock != null)
            {
                clockText.text = "Gün " + clock.day + "  " + Mathf.FloorToInt(clock.hour).ToString("00") + ":" + Mathf.FloorToInt(clock.hour % 1f * 60f).ToString("00");
                EcoUI.SetIcon(weatherIcon, EcoIcons.Weather(clock));
                // GDD §10: at least 30 s warning before a harsh change (rain or storm).
                string soon = clock.ForecastName(45f);
                bool harsh = soon != clock.WeatherName && (soon == "Yağmurlu" || soon == "Fırtına");
                weatherText.text = harsh ? "→ " + soon : clock.WeatherName; // the icon still shows the current weather
                weatherText.color = hudStyle.ColorFor(harsh ? Tone.Amber : Tone.Muted);
                if (harsh && soon != announcedWeather && weapons != null) { announcedWeather = soon; weapons.ShowFeedback("Hava değişiyor: yaklaşık 45 sn içinde " + soon.ToLowerInvariant() + ". Açık ekinler sulanır, güneş üretimi düşer."); }
                if (!harsh) announcedWeather = "";
                int wind = Mathf.RoundToInt(RegionWind(current, clock) * 100f);
                if (shownWind != wind)
                {
                    shownWind = wind;
                    EcoUI.AddTooltip(miniWeatherHit.gameObject, "Rüzgâr %" + wind + " · Bulut güneş üretimini, sakin hava rüzgâr üretimini azaltır. Yağmur açık ekinleri sular.");
                }
            }
            var skills = EcoSpecialization.Current;
            bool alert = skills != null && skills.Points > 0;
            if (skillsAlert != null && skillsAlert.gameObject.activeSelf != alert) skillsAlert.gameObject.SetActive(alert);
        }
        AnimateValues();
        UpdateMiniMap();
        UpdateBanner();
        if (MapOpen) UpdateBigMap();
        if (BoardOpen && Time.unscaledTime >= boardRefreshAt) { boardRefreshAt = Time.unscaledTime + 0.5f; RefreshBoard(); }
    }

    private void HandleKeys()
    {
        if (EcoUIFlow.PanelFreezesWorld || EcoUIFlow.EscapeConsumed) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool typing = PlayerController.Paused;
        if (!typing && EcoKeys.Pressed(EcoKeys.Action.Map)) SetMap(!MapOpen);
        if (!typing && EcoKeys.Pressed(EcoKeys.Action.Quests)) SetBoard(!BoardOpen);
        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            if (MapOpen) { EcoUIFlow.ConsumeEscape(); SetMap(false); }
            else if (BoardOpen) { EcoUIFlow.ConsumeEscape(); SetBoard(false); }
            else if (helpOpen) { EcoUIFlow.ConsumeEscape(); SetHelp(false); }
        }
        if (!typing && keyboard.f1Key.wasPressedThisFrame) SetHelp(!helpOpen);
        if (!typing && keyboard.hKey.wasPressedThisFrame && !MapOpen) CycleHud();
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

    // ================================================================== simulation sampling

    private static void Sample(EcoRegion r, float[] into)
    {
        if (r == null) { for (int i = 0; i < 7; i++) into[i] = 0f; return; }
        float air = 100f - r.airPollution, water = 100f - r.waterPollution, soil = 100f - r.soilPollution;
        float recovery = r.TryGetComponent<EcoHabitat>(out var habitat) ? habitat.recovery : 0f;
        float bio = Mathf.Clamp(r.vegetation * 55f + recovery * 30f + water * 0.15f, 0f, 100f);
        float eco = Mathf.Clamp(air * 0.30f + water * 0.25f + soil * 0.20f + bio * 0.25f, 0f, 100f);
        into[0] = air; into[1] = water; into[2] = soil; into[3] = r.carbon; into[4] = bio; into[5] = eco; into[6] = r.Temperature;
    }

    private static float Health(EcoRegion r)
    {
        var v = new float[7]; Sample(r, v); return v[5];
    }

    private static string StateName(float health) => health >= 70f ? "SAĞLIKLI" : health >= 45f ? "DENGEDE" : "KİRLİ";

    private static string TempTag(float t) => t < 0f ? "Dondurucu" : t < 10f ? "Soğuk" : t < 25f ? "Ilıman" : t < 32f ? "Sıcak" : "Aşırı sıcak";

    private static Color TempColor(float t) => t < 0f ? new Color(0.6f, 0.85f, 1f) : t < 10f ? new Color(0.5f, 0.78f, 1f)
        : t < 25f ? EcoUI.Accent : t < 32f ? EcoUI.Amber : new Color(1f, 0.45f, 0.3f);

    private static float RegionWind(EcoRegion r, EcoWorldClock clock)
    {
        float exposure = r != null && r.TryGetComponent<EcoRegionInfo>(out var info) ? info.windExposure : 1f;
        return Mathf.Clamp01((clock != null ? clock.windStrength : 0.5f) * exposure);
    }

    /// <summary>The real-world reading that goes with an index row (empty when there is none).</summary>
    private static string Reading(EcoRegion r, int row, bool brief)
    {
        if (r == null) return "";
        switch (row)
        {
            case 0: return brief ? "AQI " + r.AQI : "PM2.5 " + r.PM25.ToString("0") + " µg/m³ · AQI " + r.AQI + " (" + EcoRegion.AqiName(r.AQI) + ")";
            case 1: return brief ? r.DissolvedOxygen.ToString("0.0") + " mg/L" : "Çözünmüş oksijen " + r.DissolvedOxygen.ToString("0.0") + " mg/L";
            case 2: return brief ? "%" + r.SoilOrganicMatter.ToString("0.0") : "Organik madde %" + r.SoilOrganicMatter.ToString("0.0");
            case 3: return "CO₂ " + r.CO2ppm.ToString("0") + " ppm";
            case 4: return !brief && r.TryGetComponent<EcoHabitat>(out var habitat) ? "Kuş " + habitat.Birds + " · Kelebek " + habitat.Butterflies : "";
            default: return "";
        }
    }

    private static string WindTag(float w) => w > 0.8f ? "Fırtınalı" : w > 0.55f ? "Rüzgârlı" : w > 0.25f ? "Esintili" : "Sakin";

    // ================================================================== environment card

    private void BuildEnvironmentCard()
    {
        envCard = new EcoUI.Card("environment", canvas.transform, new Vector2(0, 1), new Vector2(0, 1), "region_city", "Çevre",
            "Bulunduğun bölgenin göstergeleri. Kartı daralt / genişlet.");
        hudStyle.Card(envCard);
        // Row 7: karbon ayak izi (EcoCarbonFootprint). Clicking it opens the breakdown.
        for (int i = 0; i < 8; i++)
        {
            string icon = i < 6 ? RowIcons[i] : i == 6 ? "temperature" : "carbon";
            rowIcons[i] = EcoUI.Icon("Row icon " + i, envCard.body, icon, new Vector2(0, 1), new Vector2(0, 1), 20f);
            rowNames[i] = EcoUI.Label("Row name " + i, envCard.body, i < 6 ? RowNames[i] : i == 6 ? "Sıcaklık" : "Ayak izi", 14, TextAnchor.MiddleLeft, EcoUI.Muted);
            rowValues[i] = EcoUI.Label("Row value " + i, envCard.body, "—", 14, TextAnchor.MiddleRight, EcoUI.Ink, true);
            rowFills[i] = EcoUI.Meter("Row meter " + i, envCard.body, out rowTracks[i]);
            hudStyle.Tint(rowNames[i], Tone.Muted);
            hudStyle.Tint(rowValues[i], Tone.Text);
            hudStyle.Tint(rowTracks[i].GetComponent<Image>(), Tone.Rule);
            var hit = EcoUI.Panel("Hit " + i, envCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
            hit.rectTransform.anchoredPosition = new Vector2(0, -4 - i * 29); hit.rectTransform.sizeDelta = new Vector2(300, 28);
            EcoUI.AddTooltip(hit.gameObject, i < 6 ? RowTips[i] : i == 6 ? "Sıcaklık: bölgenin tabanı + karbon ve hava kirliliği etkisi. Ekinlerin uygun aralığı vardır."
                : "Karbon ayak izi: yerleşimindeki kararların dünya karbonuna günlük katkısı (kaynaklar − yutaklar). Artı: karbon artıyor, eksi: azalıyor. Ayrıntı için tıkla.");
            if (i == 7)
            {
                var footprintButton = hit.gameObject.AddComponent<Button>(); footprintButton.transition = Selectable.Transition.None;
                footprintButton.onClick.AddListener(() => { if (EcoCarbonFootprint.Current != null) EcoCarbonFootprint.Current.Toggle(); });
            }
        }
    }

    private void AnimateValues()
    {
        float k = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
        bool has = current != null;
        string muted = EcoUI.Hex(hudStyle.ColorFor(Tone.Muted));
        for (int i = 0; i < 7; i++)
        {
            shown[i] = Mathf.Lerp(shown[i], target[i], k);
            if (i == 6)
            {
                rowValues[i].text = has ? shown[i].ToString("0.0") + " °C · " + TempTag(shown[i]) : "—";
                EcoUI.SetMeter(rowFills[i], rowTracks[i], has ? Mathf.InverseLerp(-15f, 40f, shown[i]) : 0f, TempColor(shown[i]));
                continue;
            }
            float good = i == 3 ? 1f - shown[i] / 100f : shown[i] / 100f;
            EcoUI.SetMeter(rowFills[i], rowTracks[i], has ? shown[i] / 100f : 0f, EcoUI.GoodColor(good));
            string reading = has ? Reading(current, i, true) : "";
            rowValues[i].text = !has ? "—" : (reading.Length > 0 ? "<size=" + 11 * EcoUI.RasterScale + "><color=#" + muted + ">" + reading + "</color></size>  " : "")
                + Mathf.RoundToInt(shown[i]);
        }
        // Karbon ayak izi: world-wide, so it is shown even outside a region. Bar: world carbon (low is good).
        float footprint = EcoCarbonFootprint.NetPerDay, worldCarbon = EcoRegion.WorldCarbon();
        rowValues[7].text = "<size=" + 11 * EcoUI.RasterScale + "><color=#" + muted + ">dünya " + Mathf.RoundToInt(worldCarbon) + "</color></size>  "
            + "<color=#" + EcoUI.Hex(hudStyle.ColorFor(footprint > 0.05f ? Tone.Amber : Tone.Leaf)) + ">" + EcoCarbonFootprint.Signed(footprint) + "/gün</color>";
        EcoUI.SetMeter(rowFills[7], rowTracks[7], worldCarbon / 100f, EcoUI.GoodColor(1f - worldCarbon / 100f));
        if (has)
        {
            var info = current.GetComponent<EcoRegionInfo>();
            envCard.title.text = current.regionName;
            EcoUI.SetIcon(envCard.icon, info != null ? EcoIcons.Region(info.kind, current.waterPollution > 35f) : "region_city");
            float health = shown[5];
            envCard.summary.text = "%" + Mathf.RoundToInt(health);
            envCard.summary.color = hudStyle.ColorFor(health < 50f ? Tone.Amber : Tone.Leaf);
        }
        else { envCard.title.text = "Bölge dışı"; envCard.summary.text = ""; }
    }

    // ================================================================== mini map

    private void BuildMiniMap()
    {
        mapCard = new EcoUI.Card("minimap", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), "ui_map", "HARİTA", "Mini harita. Kartı daralt / genişlet.");
        hudStyle.Card(mapCard);
        var frame = EcoUI.Panel("Frame", mapCard.body, new Vector2(0, 1), new Vector2(0, 1), EcoUI.SurfaceRaised);
        miniFrame = frame.rectTransform;
        miniFrame.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Map image", typeof(RectTransform), typeof(RawImage)); go.layer = 5;
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(miniFrame, false); EcoUI.Stretch(rt, 2f);
        miniImage = go.GetComponent<RawImage>(); miniImage.texture = minimapTexture; miniImage.raycastTarget = false;
        miniImage.color = minimapTexture != null ? Color.white : new Color(0.2f, 0.3f, 0.3f);
        miniArrow = EcoUI.Icon("Player", miniFrame, "player", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 22f).rectTransform;
        miniNorth = EcoUI.Label("North", miniFrame, "K", 13, TextAnchor.MiddleCenter, EcoUI.PaperInk, true);
        miniNorth.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        bigMapButton = EcoUI.IconButton("Open map", mapCard.root, "ui_layers", 26f, () => SetMap(true), "Büyük harita " + EcoKeys.Tag(EcoKeys.Action.Map));
        hudStyle.Tint(bigMapButton.targetGraphic, Tone.Raised);
        miniStatus = EcoUI.Rect("Time and weather", mapCard.root, new Vector2(0, 1), new Vector2(0, 1));
        clockIcon = EcoUI.Icon("Clock", miniStatus, "clock", new Vector2(0, 1), new Vector2(0, 1), 18f);
        clockText = EcoUI.Label("Clock text", miniStatus, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        weatherIcon = EcoUI.Icon("Weather", miniStatus, "w_sun", new Vector2(0, 1), new Vector2(0, 1), 20f);
        weatherText = EcoUI.Label("Weather text", miniStatus, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink);
        hudStyle.Tint(clockText, Tone.Text);
        weatherText.color = hudStyle.ColorFor(Tone.Muted);
        var strip = EcoUI.Panel("Weather hit", miniStatus, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        miniWeatherHit = strip.rectTransform;
        EcoUI.AddTooltip(strip.gameObject, "Gün, saat ve hava. Bulut güneş üretimini, sakin hava rüzgâr türbinini düşürür; yağmur açık ekinleri sular.");
        mapCard.toggle.transform.SetAsLastSibling();
        var button = frame.gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SetMap(true));
        EcoUI.AddTooltip(frame.gameObject, "Büyük haritayı aç " + EcoKeys.Tag(EcoKeys.Action.Map));
    }

    private Vector2 ToUv(Vector3 world) => new Vector2(
        (world.x - worldOrigin.x) / Mathf.Max(1f, worldSize.x), (world.z - worldOrigin.y) / Mathf.Max(1f, worldSize.y));

    private Rect MiniView()
    {
        float s = MiniWindow / Mathf.Max(1f, worldSize.x);
        var uv = player != null ? ToUv(player.position) : new Vector2(0.5f, 0.5f);
        float x = Mathf.Clamp(uv.x - s * 0.5f, 0f, 1f - s), y = Mathf.Clamp(uv.y - s * 0.5f, 0f, 1f - s);
        return new Rect(x, y, s, s);
    }

    private void UpdateMiniMap()
    {
        if (mapCard.IsCollapsed || !mapCard.root.gameObject.activeInHierarchy) return;
        var view = MiniView();
        miniImage.uvRect = view;
        float side = miniFrame.sizeDelta.x - 4f;
        float yaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : player.eulerAngles.y;
        var uv = ToUv(player.position);
        miniArrow.anchoredPosition = new Vector2(2f + (uv.x - view.x) / view.width * side, -2f - (1f - (uv.y - view.y) / view.height) * side);
        float facing = player.eulerAngles.y;
        miniArrow.localRotation = Quaternion.Euler(0f, 0f, -facing);
        for (int i = 0; i < miniPins.Count; i++)
        {
            var pin = miniPins[i];
            if (!pin.gameObject.activeSelf) continue;
            var world = (Vector3)pinWorld[i];
            var p = ToUv(world);
            bool inside = view.Contains(p);
            pin.enabled = inside;
            if (inside) pin.rectTransform.anchoredPosition = new Vector2(2f + (p.x - view.x) / view.width * side, -2f - (1f - (p.y - view.y) / view.height) * side);
        }
        _ = yaw;
    }

    private readonly List<Vector3> pinWorld = new List<Vector3>();

    private void RefreshMiniPins()
    {
        pinWorld.Clear();
        var icons = new List<string>();
        foreach (var marker in EcoMapMarker.All)
            if (marker != null && marker.onMiniMap) { pinWorld.Add(marker.transform.position); icons.Add(marker.icon); }
        foreach (var fire in FindObjectsByType<EcoFire>(FindObjectsSortMode.None))
            if (fire.Burning) { pinWorld.Add(fire.transform.position); icons.Add("poi_fire"); }
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (enemy != null && enemy.Health > 0f && enemy.State != EnemyBrain.Behaviour.Dead) { pinWorld.Add(enemy.transform.position); icons.Add("poi_enemy"); }
        var projects = EcoProjects.Current;
        if (projects != null && Time.unscaledTime < projects.HintUntil) { pinWorld.Add(projects.HintPosition); icons.Add("poi_quest"); }
        while (miniPins.Count < pinWorld.Count)
        {
            var pin = EcoUI.Icon("Pin", miniFrame, "poi_quest", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 18f);
            pin.transform.SetSiblingIndex(miniArrow.GetSiblingIndex());
            miniPins.Add(pin);
        }
        for (int i = 0; i < miniPins.Count; i++)
        {
            bool used = i < pinWorld.Count;
            miniPins[i].gameObject.SetActive(used);
            if (used) { EcoUI.SetIcon(miniPins[i], icons[i]); miniPins[i].rectTransform.sizeDelta = Vector2.one * (icons[i] == "poi_enemy" ? 14f : 18f); }
        }
    }

    // ================================================================== banner & intro

    private void BuildBanner()
    {
        var bg = hudStyle.Surface(EcoUI.Panel("Region banner", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), EcoUI.Surface, false));
        banner = bg.rectTransform;
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.alpha = 0f; bannerGroup.interactable = false; bannerGroup.blocksRaycasts = false;
        bannerStrip = EcoUI.Fill("Strip", banner, EcoUI.Accent);
        bannerIcon = EcoUI.Icon("Icon", banner, "region_city", new Vector2(0, 1), new Vector2(0, 1), 44f);
        bannerTitle = EcoUI.Label("Title", banner, "", 21, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        bannerState = EcoUI.Label("State", banner, "", 13, TextAnchor.MiddleRight, EcoUI.Accent, true);
        bannerSub = EcoUI.Label("Tagline", banner, "", 13, TextAnchor.UpperLeft, EcoUI.Muted);
        hudStyle.Tint(bannerTitle, Tone.Text);
        hudStyle.Tint(bannerSub, Tone.Muted);
        hudStyle.Tint(bannerState, Tone.Leaf);
    }

    private void ShowBanner(EcoRegion region)
    {
        var info = region.GetComponent<EcoRegionInfo>();
        bannerTitle.text = region.regionName;
        bannerSub.text = info != null ? info.tagline : "";
        bannerStrip.color = info != null ? info.mapColor : EcoUI.Accent;
        EcoUI.SetIcon(bannerIcon, info != null ? EcoIcons.Region(info.kind, region.waterPollution > 35f) : "region_city");
        float health = Health(region);
        bannerState.text = StateName(health);
        bannerUntil = Time.unscaledTime + 3.5f;
    }

    private void UpdateBanner()
    {
        float left = bannerUntil - Time.unscaledTime;
        bannerGroup.alpha = !bannerFits || EcoUI.Mode != EcoUI.HudMode.Full || left <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(left, 3.5f - left) * 2.5f);
        BannerBottom = bannerGroup.alpha > 0.01f ? bannerBottom : 0f;
    }

    private void BuildIntro()
    {
        var bg = EcoUI.Panel("Intro", canvas.transform, Vector2.zero, Vector2.zero, EcoUI.Surface, false);
        bg.sprite = null; bg.type = Image.Type.Simple;
        intro = bg.rectTransform; EcoUI.Stretch(intro);
        introGroup = intro.gameObject.AddComponent<CanvasGroup>(); introGroup.interactable = false; introGroup.blocksRaycasts = false;
        var column = EcoUI.Rect("Column", intro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); column.sizeDelta = new Vector2(820, 320);
        var logo = EcoUI.Icon("Logo", column, "yep", new Vector2(0.5f, 1), new Vector2(0.5f, 1), 72f); logo.rectTransform.anchoredPosition = Vector2.zero;
        var title = EcoUI.Label("Title", column, introTitle, 54, TextAnchor.MiddleCenter, EcoUI.Accent, true);
        EcoUI.Layout(title, new Vector2(10f, -78f), new Vector2(800f, 66f));
        var line = EcoUI.Label("Line", column, introLine, 20, TextAnchor.MiddleCenter, EcoUI.Ink);
        EcoUI.Layout(line, new Vector2(10f, -150f), new Vector2(800f, 56f));
        var hints = new[] { ("ui_map", EcoKeys.Tag(EcoKeys.Action.Map) + " Harita"), ("tool_vacuum", "[1–6] Araçlar"), ("ui_build", EcoKeys.Tag(EcoKeys.Action.Build) + " İnşa"), ("tool_hand", EcoKeys.Tag(EcoKeys.Action.Interact) + " Etkileşim"), ("ui_help", "[F1] Yardım") };
        float x = (820f - hints.Length * 150f) * 0.5f;
        foreach (var (icon, text) in hints)
        {
            var img = EcoUI.Icon("Hint icon", column, icon, new Vector2(0, 1), new Vector2(0, 1), 34f); EcoUI.LayoutIcon(img, new Vector2(x + 58f, -224f), 34f);
            var label = EcoUI.Label("Hint", column, text, 14, TextAnchor.MiddleCenter, EcoUI.Muted); EcoUI.Layout(label, new Vector2(x, -262f), new Vector2(150f, 20f));
            x += 150f;
        }
    }

    private void AnimateIntro()
    {
        if (introGroup == null) return;
        float t = Time.unscaledTime - startTime;
        float alpha = t < introSeconds ? 1f : Mathf.Clamp01(1f - (t - introSeconds) / 1.6f);
        introGroup.alpha = alpha;
        if (alpha <= 0f && intro != null) { Destroy(intro.gameObject); intro = null; introGroup = null; }
    }

    // ================================================================== quick actions, help, HUD mode

    private void BuildActions()
    {
        actions = hudStyle.Surface(EcoUI.Panel("Quick actions", canvas.transform, Vector2.zero, Vector2.zero, EcoUI.Surface)).rectTransform;
        AddAction("map", "ui_map", EcoKeys.Label(EcoKeys.Action.Map), "Dünya haritası", () => SetMap(!MapOpen));
        AddAction("quests", "poi_quest", EcoKeys.Label(EcoKeys.Action.Quests), "Görev panosu", () => SetBoard(!BoardOpen));
        AddAction("journal", "ui_journal", EcoKeys.Label(EcoKeys.Action.Journal), "Proje günlüğü", () => { var p = EcoProjects.Current; if (p != null) p.SetOpen(!p.PanelOpen); });
        if (gameObject.scene.path == "Assets/Scenes/EcoWorld.unity")
        {
            var catalog = gameObject.AddComponent<EcoFieldCatalog>();
            catalog.Bind(this);
            AddAction("catalog", "ui_journal", "", "Saha kataloğu", catalog.Open);
        }
        AddAction("build", "ui_build", EcoKeys.Label(EcoKeys.Action.Build), "İnşa modu", () => { if (weapons != null && weapons.Builder != null) weapons.Builder.SetBuildMode(!weapons.Builder.IsBuilding); });
        AddAction("skills", "ui_skills", EcoKeys.Label(EcoKeys.Action.Skills), "Uzmanlık", () => { var s = FindFirstObjectByType<EcoSpecialization>(); if (s != null) s.SetOpen(!s.PanelOpen); });
        AddAction("settings", "ui_gear", EcoKeys.Label(EcoKeys.Action.Settings), "Ses ayarları", () => { var a = FindFirstObjectByType<EcoAudio>(); if (a != null) a.TogglePanel(); });
        AddAction("help", "ui_help", "F1", "Kontroller ve yardım", () => SetHelp(!helpOpen));
        AddAction("hud", "ui_hud", "H", "Arayüz: Tam / Sade / Gizli", CycleHud);
        var bg = hudStyle.Surface(EcoUI.Panel("Mode toast", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false));
        modeToastBg = bg; modeToast = EcoUI.Label("Text", bg.transform, "", 18, TextAnchor.MiddleCenter, EcoUI.Ink, true);
        hudStyle.Tint(modeToast, Tone.Text);
        bg.gameObject.SetActive(false);
    }

    private void AddAction(string id, string icon, string key, string tip, System.Action onClick)
    {
        var button = EcoUI.IconButton("Action " + id, actions, icon, 28f, onClick, key.Length == 0 ? tip : tip + "  [" + key + "]");
        hudStyle.Tint(button.targetGraphic, Tone.Raised);
        var badge = EcoUI.Panel("Key", button.transform, new Vector2(1, 0), new Vector2(1, 0), EcoUI.Selected, false, EcoUI.Rounded);
        EcoUI.Place(badge.rectTransform, new Vector2(6f, -4f), new Vector2(key.Length > 2 ? 30f : 20f, 16f));
        var text = EcoUI.Label("Key text", badge.transform, key, 10, TextAnchor.MiddleCenter, EcoUI.PaperInk, true);
        hudStyle.Tint(badge, Tone.Surface);
        hudStyle.Tint(text, Tone.Muted);
        EcoUI.Layout(text, Vector2.zero, badge.rectTransform.sizeDelta);
        badge.gameObject.SetActive(key.Length > 0);
        actionButtons.Add((button, text, id));
        if (id == "skills")
        {
            skillsAlert = EcoUI.Panel("Alert", button.transform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), EcoUI.Amber, false, EcoUI.Circle);
            EcoUI.Place(skillsAlert.rectTransform, new Vector2(-4f, -4f), new Vector2(16f, 16f));
            var mark = EcoUI.Label("!", skillsAlert.transform, "!", 11, TextAnchor.MiddleCenter, EcoUI.Ink, true);
            EcoUI.Layout(mark, Vector2.zero, new Vector2(16f, 16f));
            skillsAlert.gameObject.SetActive(false);
        }
    }

    private void CycleHud()
    {
        EcoUI.CycleMode();
        modeToast.text = "Arayüz: " + EcoUI.ModeName + "   ·   [H] değiştir";
        modeToastUntil = Time.unscaledTime + 1.8f;
    }

    private void UpdateModeToast()
    {
        bool on = Time.unscaledTime < modeToastUntil;
        if (modeToastBg.gameObject.activeSelf != on) modeToastBg.gameObject.SetActive(on);
    }

    private void BuildHelp()
    {
        helpRoot = NotebookBackdrop("Controls");
        var card = EcoNotebook.Surface(EcoUI.Panel("Help", helpRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoNotebook.Paper), EcoNotebook.Paper);
        help = card.rectTransform; help.sizeDelta = HelpSize;
        var icon = EcoUI.Icon("Icon", help, "ui_help", new Vector2(0, 1), new Vector2(0, 1), 28f); EcoUI.LayoutIcon(icon, new Vector2(22f, -18f), 28f);
        var title = EcoUI.Label("Title", help, "Kontroller", 23, TextAnchor.MiddleLeft, EcoNotebook.Ink, true); EcoUI.Layout(title, new Vector2(61f, -15f), new Vector2(400f, 30f));
        var sub = EcoUI.Label("Sub", help, "Tuşları Ayarlar menüsünden değiştirebilirsin. Paneller açıkken dünya %10 hızda akar.", 13, TextAnchor.MiddleLeft, EcoNotebook.Muted);
        EcoUI.Layout(sub, new Vector2(61f, -46f), new Vector2(640f, 20f));
        NotebookClose(help, () => SetHelp(false), "Kapat [F1] / [Esc]");
        NotebookRule(help, 74f, 20f);
        var rows = new (string icon, string key, string text)[]
        {
            ("player", "WASD", "Kameraya göre yürü (" + EcoKeys.Label(EcoKeys.Action.Sprint) + " koş)"), ("poi_quest", "Sağ tık", "Tıklanan yere yürü"),
            ("tool_vacuum", "Sol tık", "Seçili aracı kullan"), ("ui_layers", "Tekerlek", "Kamerayı yaklaştır / uzaklaştır"),
            ("tool_seed", "1 – 6", "Araç seç: vakum, tohum, geri dönüşüm, tarayıcı, eller, fener"), ("w_wind", EcoKeys.Label(EcoKeys.Action.Mode), "Araç modu (toplama · anti-vakum · su / tohum · demir)"),
            ("seed", EcoKeys.Label(EcoKeys.Action.Rotate), "Tohum türü · inşada döndür · gübrele"), ("tool_hand", EcoKeys.Label(EcoKeys.Action.Interact), "Etkileşim: konuş, hasat, filtre, balık"),
            ("ui_build", EcoKeys.Label(EcoKeys.Action.Build), "İnşa modu (kablo, boru, tesisler)"), ("ui_map", EcoKeys.Label(EcoKeys.Action.Map), "Dünya haritası ve bölge bilgisi"),
            ("ui_journal", EcoKeys.Label(EcoKeys.Action.Journal), "Proje günlüğü ve ipuçları"), ("ui_skills", EcoKeys.Label(EcoKeys.Action.Skills), "Uzmanlık hakları"),
            ("ui_orders", EcoKeys.Label(EcoKeys.Action.Orders), "Siparişler (pazarın yanında)"), ("poi_finale", EcoKeys.Label(EcoKeys.Action.Finale), "Final terminali (buzul istasyonu)"),
            ("ui_gear", EcoKeys.Label(EcoKeys.Action.Settings), "Ses ayarları"), ("calendar", "F5 / F9", "Hızlı kaydet / yükle"),
            ("ui_hud", "H", "Arayüz: Tam · Sade · Gizli"), ("ui_pause", "Esc", "Paneli kapat / duraklat"),
            ("poi_quest", EcoKeys.Label(EcoKeys.Action.Quests), "Görev panosu: görev seç, ok yol gösterir"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float x = i % 2 == 0 ? 24f : 404f, y = -89f - (i / 2) * 45f;
            var rowIcon = EcoUI.Icon("Row icon", help, rows[i].icon, new Vector2(0, 1), new Vector2(0, 1), 24f); EcoUI.LayoutIcon(rowIcon, new Vector2(x, y), 24f);
            var keyBg = EcoUI.Panel("Key", help, new Vector2(0, 1), new Vector2(0, 1), EcoNotebook.Inset, false);
            EcoUI.Place(keyBg.rectTransform, new Vector2(x + 32f, y), new Vector2(74f, 24f));
            var key = EcoUI.Label("Key", keyBg.transform, rows[i].key, 13, TextAnchor.MiddleCenter, EcoNotebook.Accent, true); EcoUI.Layout(key, Vector2.zero, new Vector2(74f, 24f));
            var text = EcoUI.Label("Text", help, rows[i].text, 13, TextAnchor.MiddleLeft, EcoNotebook.Ink); EcoUI.Layout(text, new Vector2(x + 117f, y + 7f), new Vector2(234f, 38f));
        }
        helpRoot.gameObject.SetActive(false);
    }

    /// <summary>Opens the F1 controls panel (pause menu "Kontroller").</summary>
    public void ShowHelp() => SetHelp(true);

    private void SetHelp(bool open)
    {
        if (open && MapOpen) SetMap(false);
        if (open && BoardOpen) SetBoard(false);
        helpOpen = open; helpRoot.gameObject.SetActive(open);
        EcoUIFlow.SetPanel(helpOwner, open);
    }

    private RectTransform NotebookBackdrop(string name)
    {
        var root = EcoUI.Panel(name, overlay.transform, Vector2.zero, Vector2.zero, EcoNotebook.Dim);
        root.sprite = null; root.type = Image.Type.Simple;
        EcoUI.Stretch(root.rectTransform);
        return root.rectTransform;
    }

    private static void NotebookClose(RectTransform parent, System.Action onClick, string tip)
    {
        var close = EcoUI.IconButton("Close", parent, "ui_close", 30f, onClick, tip);
        close.targetGraphic.color = EcoNotebook.Inset;
        var rect = (RectTransform)close.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-14f, -14f);
    }

    private static void NotebookRule(RectTransform parent, float top, float margin)
    {
        var line = EcoUI.Fill("Header rule", parent, EcoNotebook.Rule).rectTransform;
        line.anchorMax = Vector2.one;
        line.offsetMin = new Vector2(margin, -top - 1f);
        line.offsetMax = new Vector2(-margin, -top);
    }

    private static ScrollRect NotebookScroll(Image viewport, RectTransform content)
    {
        viewport.gameObject.AddComponent<RectMask2D>();
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 36f;
        scroll.inertia = false;
        return scroll;
    }

    // ================================================================== big map

    private void BuildBigMap()
    {
        bigRoot = NotebookBackdrop("Big map");
        var card = EcoNotebook.Surface(EcoUI.Panel("Card", bigRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoNotebook.Paper), EcoNotebook.Paper);
        bigCard = card.rectTransform;
        var titleIcon = EcoUI.Icon("Icon", bigCard, "ui_map", new Vector2(0, 1), new Vector2(0, 1), 28f); EcoUI.LayoutIcon(titleIcon, new Vector2(20f, -16f), 28f);
        bigTitle = EcoUI.Label("Title", bigCard, "Dünya haritası", 23, TextAnchor.MiddleLeft, EcoNotebook.Ink, true);
        bigHint = EcoUI.Label("Hint", bigCard, "", 12, TextAnchor.MiddleLeft, EcoNotebook.Muted);
        NotebookClose(bigCard, () => SetMap(false), "Kapat " + EcoKeys.Tag(EcoKeys.Action.Map) + " / [Esc]");
        NotebookRule(bigCard, 68f, 18f);

        var frame = EcoNotebook.Surface(EcoUI.Panel("Frame", bigCard, new Vector2(0, 1), new Vector2(0, 1), EcoNotebook.Inset), EcoNotebook.Inset);
        bigFrame = frame.rectTransform;
        bigFrame.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Map image", typeof(RectTransform), typeof(RawImage)); go.layer = 5;
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(bigFrame, false); EcoUI.Stretch(rt, 3f);
        bigImage = go.GetComponent<RawImage>(); bigImage.texture = minimapTexture; bigImage.raycastTarget = false;
        bigView = EcoUI.Rect("Overlay", bigFrame, new Vector2(0, 1), new Vector2(0, 1)); // region tints, outlines and icons live here
        hintPin = EcoUI.Icon("Hint", bigFrame, "poi_quest", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 34f).rectTransform;
        bigArrow = EcoUI.Icon("Player", bigFrame, "player", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 30f).rectTransform;
        coordText = EcoUI.Label("Coordinates", bigCard, "", 12, TextAnchor.MiddleLeft, EcoNotebook.Muted);

        legend = EcoUI.Rect("Legend", bigCard, new Vector2(0, 1), new Vector2(0, 1));
        var items = new[] { ("player", "Sen"), ("poi_quest", "Görev"), ("poi_market", "Pazar"), ("poi_home", "Ev"), ("poi_factory", "Fabrika"), ("poi_station", "İstasyon"),
            ("w_wind", "Rüzgâr"), ("poi_fire", "Yangın"), ("poi_enemy", "Tehdit") };
        foreach (var (icon, text) in items)
        {
            var img = EcoUI.Icon("Legend icon", legend, icon, new Vector2(0, 1), new Vector2(0, 1), 16f);
            var label = EcoUI.Label("Legend text", legend, text, 12, TextAnchor.MiddleLeft, EcoNotebook.Muted);
            legendItems.Add((img, label));
        }

        BuildInfoCard();
        bigRoot.gameObject.SetActive(false);
    }

    private void BuildInfoCard()
    {
        var bg = EcoNotebook.Surface(EcoUI.Panel("Info", bigCard, Vector2.one, Vector2.one, EcoNotebook.Sheet), EcoNotebook.Sheet);
        infoCard = bg.rectTransform;
        var viewport = EcoUI.Panel("Region notes viewport", infoCard, Vector2.up, Vector2.up, Color.clear);
        infoViewport = viewport.rectTransform;
        infoContent = EcoUI.Rect("Region notes", infoViewport, Vector2.up, Vector2.up);
        infoScroll = NotebookScroll(viewport, infoContent);
        infoIcon = EcoUI.Icon("Region icon", infoContent, "region_city", new Vector2(0, 1), new Vector2(0, 1), 50f);
        infoName = EcoUI.Label("Name", infoContent, "", 20, TextAnchor.MiddleLeft, EcoNotebook.Ink, true);
        infoBadge = EcoUI.Panel("Badge", infoContent, new Vector2(0, 1), new Vector2(0, 1), EcoNotebook.Inset, false);
        infoBadgeText = EcoUI.Label("Badge text", infoBadge.transform, "", 11, TextAnchor.MiddleCenter, EcoNotebook.Ink, true);
        infoTag = EcoUI.Label("Tagline", infoContent, "", 13, TextAnchor.UpperLeft, EcoNotebook.Muted);
        infoEmpty = EcoUI.Label("Empty", infoCard, "Bir bölgenin üzerine gel.\nTıklayınca bölge notları burada kalır.", 15, TextAnchor.MiddleCenter, EcoNotebook.Muted);
        for (int i = 0; i < 6; i++)
        {
            infoRowIcon[i] = EcoUI.Icon("Row icon", infoContent, RowIcons[i], new Vector2(0, 1), new Vector2(0, 1), 18f);
            infoRowName[i] = EcoUI.Label("Row name", infoContent, RowNames[i], 13, TextAnchor.MiddleLeft, EcoNotebook.Muted);
            infoRowValue[i] = EcoUI.Label("Row value", infoContent, "", 13, TextAnchor.MiddleRight, EcoNotebook.Ink, true);
            infoRowReading[i] = EcoUI.Label("Row reading", infoContent, "", 11, TextAnchor.MiddleLeft, EcoNotebook.Muted);
            infoRowFill[i] = EcoUI.Meter("Row meter", infoContent, out infoRowTrack[i]);
            infoRowTrack[i].GetComponent<Image>().color = EcoNotebook.Rule;
        }
        climateHeader = EcoUI.Label("Climate", infoContent, "İklim şu an", 13, TextAnchor.MiddleLeft, EcoNotebook.Accent, true);
        for (int i = 0; i < 4; i++)
        {
            climateIcon[i] = EcoUI.Icon("Climate icon", infoContent, "w_sun", new Vector2(0, 1), new Vector2(0, 1), 22f);
            climateText[i] = EcoUI.Label("Climate text", infoContent, "", 12, TextAnchor.MiddleLeft, EcoNotebook.Ink, true);
        }
        tasksHeader = EcoUI.Label("Tasks", infoContent, "Bölge görevleri · Tıkla ve takip et", 13, TextAnchor.MiddleLeft, EcoNotebook.Accent, true);
        for (int i = 0; i < taskIcon.Length; i++)
        {
            taskIcon[i] = EcoUI.Icon("Task icon", infoContent, "poi_project", new Vector2(0, 1), new Vector2(0, 1), 22f);
            taskText[i] = EcoUI.Label("Task text", infoContent, "", 13, TextAnchor.UpperLeft, EcoNotebook.Ink);
            taskFill[i] = EcoUI.Meter("Task meter", infoContent, out taskTrack[i]);
            taskTrack[i].GetComponent<Image>().color = EcoNotebook.Rule;
            taskHit[i] = EcoUI.Panel("Task hit", infoContent, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
            int row = i;
            var button = taskHit[i].gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { if (taskQuest[row] != null) { EcoQuestBoard.Toggle(taskQuest[row]); mapRefreshAt = 0f; } });
            EcoUI.AddTooltip(taskHit[i].gameObject, "Tıkla: bu görevi takip et (ok yol gösterir) · tekrar tıkla: bırak");
        }
    }

    public void SetMap(bool open)
    {
        if (open && helpOpen) SetHelp(false);
        if (open && BoardOpen) SetBoard(false);
        MapOpen = open;
        bigRoot.gameObject.SetActive(open);
        EcoUIFlow.SetPanel(this, open);
        if (open)
        {
            RebuildOverlays();
            zoom = 1f; viewCenter = new Vector2(0.5f, 0.5f); pinned = null;
            dragging = false;
            infoRegion = null;
            mapRefreshAt = 0f;
            dirty = true;
        }
        else EcoUI.HideTooltip(this);
    }

    private void RebuildOverlays()
    {
        foreach (var o in overlays) { if (o.fill != null) Destroy(o.fill.gameObject); if (o.chip != null) Destroy(o.chip.gameObject); foreach (var e in o.edges) if (e != null) Destroy(e.gameObject); }
        overlays.Clear();
        if (bigView == null) return;
        // Big regions first (drawn below), the small starter pockets last.
        foreach (var region in regions.Where(r => r != null).OrderBy(r => r.priority).ThenByDescending(r => r.size.x * r.size.y))
        {
            var info = region.GetComponent<EcoRegionInfo>();
            var o = new RegionOverlay { region = region, info = info, edges = new Image[4] };
            Color c = info != null ? info.mapColor : new Color(0.8f, 0.8f, 0.8f);
            o.fill = EcoUI.Fill("Region " + region.regionName, bigView, new Color(c.r, c.g, c.b, 0.05f));
            for (int e = 0; e < 4; e++) o.edges[e] = EcoUI.Fill("Edge", bigView, new Color(c.r, c.g, c.b, 0.55f));
            if (info != null)
            {
                var chip = EcoUI.Panel("Chip", bigView, new Vector2(0, 1), new Vector2(0.5f, 0.5f), EcoNotebook.Sheet, false);
                chip.pixelsPerUnitMultiplier = 2.5f;
                o.chip = chip.rectTransform;
                o.chipIcon = EcoUI.Icon("Icon", o.chip, EcoIcons.Region(info.kind, region.waterPollution > 35f), new Vector2(0, 1), new Vector2(0, 1), 24f);
                o.chipText = EcoUI.Label("Name", o.chip, region.regionName, 12, TextAnchor.MiddleLeft, EcoNotebook.Ink, true);
                o.weather = EcoUI.Icon("Weather", o.chip, "w_wind", new Vector2(0, 1), new Vector2(0, 1), 24f);
            }
            overlays.Add(o);
        }
        foreach (var image in poiImages) if (image != null) Destroy(image.gameObject);
        poiImages.Clear();
        hintPin.SetAsLastSibling(); bigArrow.SetAsLastSibling();
    }

    private Rect BigView()
    {
        float s = 1f / Mathf.Max(1f, zoom);
        viewCenter.x = Mathf.Clamp(viewCenter.x, s * 0.5f, 1f - s * 0.5f);
        viewCenter.y = Mathf.Clamp(viewCenter.y, s * 0.5f, 1f - s * 0.5f);
        return new Rect(viewCenter.x - s * 0.5f, viewCenter.y - s * 0.5f, s, s);
    }

    private Vector2 ToFrame(Vector2 uv, Rect view, float side) =>
        new Vector2(3f + (uv.x - view.x) / view.width * side, -3f - (1f - (uv.y - view.y) / view.height) * side);

    private void UpdateBigMap()
    {
        float side = bigFrame.sizeDelta.x - 6f;
        var mouse = Mouse.current;
        Vector2 local = Vector2.zero; bool overMap = false;
        if (mouse != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(bigFrame, mouse.position.ReadValue(), null, out local);
            // local is relative to the frame pivot (top-left).
            overMap = local.x >= 3f && local.x <= side + 3f && local.y <= -3f && local.y >= -side - 3f;
        }
        var view = BigView();
        Vector2 mouseUv = new Vector2(view.x + (local.x - 3f) / side * view.width, view.y + (1f - (-local.y - 3f) / side) * view.height);
        if (overMap && mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float before = zoom;
                zoom = Mathf.Clamp(zoom * (scroll > 0f ? 1.18f : 1f / 1.18f), 1f, 3.5f);
                // Keep the point under the cursor in place while zooming.
                float k = before / zoom;
                viewCenter = mouseUv + (viewCenter - mouseUv) * k;
                view = BigView();
            }
            if (mouse.leftButton.wasPressedThisFrame) { dragging = true; dragMoved = false; dragStart = local; dragCenter = viewCenter; }
        }
        if (dragging && mouse != null)
        {
            Vector2 delta = local - dragStart;
            if (delta.magnitude > 4f) dragMoved = true;
            if (dragMoved) { viewCenter = dragCenter - new Vector2(delta.x / side * view.width, -delta.y / side * view.height); view = BigView(); }
            if (!mouse.leftButton.isPressed)
            {
                dragging = false;
                if (!dragMoved && overMap) pinned = RegionAtUv(mouseUv);
            }
        }
        if (mouse != null && mouse.rightButton.wasPressedThisFrame) pinned = null;
        bigImage.uvRect = view;

        // Region overlays.
        var clock = EcoWorldClock.Current;
        hovered = overMap ? RegionAtUv(mouseUv) : null;
        var focus = pinned != null ? pinned : hovered;
        foreach (var o in overlays)
        {
            if (o.region == null) continue;
            var min = ToUv(o.region.transform.position - new Vector3(o.region.size.x * 0.5f, 0f, o.region.size.y * 0.5f));
            var max = ToUv(o.region.transform.position + new Vector3(o.region.size.x * 0.5f, 0f, o.region.size.y * 0.5f));
            Vector2 a = ToFrame(min, view, side), b = ToFrame(max, view, side); // a = bottom-left, b = top-right
            float x0 = a.x, x1 = b.x, y0 = b.y, y1 = a.y; // y0 top (less negative)
            bool focusOn = o.region == focus;
            Color c = o.info != null ? o.info.mapColor : Color.white;
            EcoUI.Place(o.fill.rectTransform, new Vector2(x0, y0), new Vector2(x1 - x0, y0 - y1));
            o.fill.color = new Color(c.r, c.g, c.b, focusOn ? 0.23f : 0.05f);
            float t = focusOn ? 2.5f : 1.2f;
            EcoUI.Place(o.edges[0].rectTransform, new Vector2(x0, y0), new Vector2(x1 - x0, t));
            EcoUI.Place(o.edges[1].rectTransform, new Vector2(x0, y1 + t), new Vector2(x1 - x0, t));
            EcoUI.Place(o.edges[2].rectTransform, new Vector2(x0, y0), new Vector2(t, y0 - y1));
            EcoUI.Place(o.edges[3].rectTransform, new Vector2(x1 - t, y0), new Vector2(t, y0 - y1));
            foreach (var e in o.edges) e.color = new Color(c.r, c.g, c.b, focusOn ? 0.95f : 0.45f);
            if (o.chip != null)
            {
                // Chip: region icon, name and its live weather symbol (wind on exposed ground, rain, snow, haze).
                var centre = ToFrame(ToUv(o.region.transform.position), view, side);
                float wind = RegionWind(o.region, clock);
                string weather = o.info.kind == EcoRegionInfo.Kind.Glacier ? "w_snow" : clock != null && clock.RainIntensity > 0.15f ? "w_rain"
                    : wind > 0.55f ? "w_wind" : o.region.airPollution > 45f ? "carbon" : o.info.sunExposure > 1.05f && clock != null && clock.SolarFactor > 0.3f ? "w_sun" : "";
                EcoUI.SetIcon(o.weather, weather);
                float sway = weather == "w_wind" ? Mathf.Sin(Time.unscaledTime * (2f + wind * 4f)) * 3f : 0f;
                float textWidth = Mathf.Min(150f, o.region.regionName.Length * 7.4f + 8f);
                float w = 8f + 24f + 4f + textWidth + (weather.Length > 0 ? 28f : 4f);
                bool small = o.region.size.x < 20f;
                o.chip.gameObject.SetActive(!small || zoom > 1.8f);
                EcoUI.Place(o.chip, centre, new Vector2(w, 30f));
                EcoUI.LayoutIcon(o.chipIcon, new Vector2(6f, -3f), 24f);
                EcoUI.Layout(o.chipText, new Vector2(34f, -6f), new Vector2(textWidth, 18f));
                EcoUI.LayoutIcon(o.weather, new Vector2(34f + textWidth + sway, -3f), 24f);
                o.chip.GetComponent<Image>().color = focusOn ? EcoNotebook.Selected : EcoNotebook.Sheet;
            }
        }

        // Points of interest.
        if (Time.unscaledTime >= mapRefreshAt) { mapRefreshAt = Time.unscaledTime + 0.25f; CollectPois(); RefreshInfoCard(focus); }
        if (poiImages.Count < pois.Count)
        {
            while (poiImages.Count < pois.Count) poiImages.Add(EcoUI.Icon("Poi", bigView, "poi_quest", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 26f));
            foreach (var o in overlays) if (o.chip != null) o.chip.SetAsLastSibling(); // region names stay readable above icons
        }
        hoveredPoi = null; float best = 18f;
        for (int i = 0; i < poiImages.Count; i++)
        {
            var image = poiImages[i];
            bool used = i < pois.Count;
            if (image.gameObject.activeSelf != used) image.gameObject.SetActive(used);
            if (!used) continue;
            var poi = pois[i];
            var uv = ToUv(poi.position);
            bool inside = view.Contains(uv);
            image.enabled = inside;
            if (!inside) continue;
            EcoUI.SetIcon(image, poi.icon);
            var p = ToFrame(uv, view, side);
            float size = poi.size * Mathf.Lerp(1f, 1.25f, (zoom - 1f) / 2.5f) * (poi.pulse ? 1f + Mathf.Sin(Time.unscaledTime * 5f) * 0.12f : 1f);
            image.rectTransform.anchoredPosition = p; image.rectTransform.sizeDelta = new Vector2(size, size);
            float d = Vector2.Distance(p, local);
            if (overMap && d < best) { best = d; hoveredPoi = poi; }
        }
        if (hoveredPoi != null) EcoUI.ShowTooltip(this, "<b>" + hoveredPoi.title + "</b>" + (string.IsNullOrEmpty(hoveredPoi.detail) ? "" : "\n" + hoveredPoi.detail));
        else EcoUI.HideTooltip(this);

        // Player and project hint.
        var puv = ToUv(player.position);
        bigArrow.gameObject.SetActive(view.Contains(puv));
        bigArrow.anchoredPosition = ToFrame(puv, view, side);
        bigArrow.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);
        var projects = EcoProjects.Current;
        bool hint = projects != null && Time.unscaledTime < projects.HintUntil && view.Contains(ToUv(projects.HintPosition));
        hintPin.gameObject.SetActive(hint);
        if (hint)
        {
            hintPin.anchoredPosition = ToFrame(ToUv(projects.HintPosition), view, side);
            hintPin.sizeDelta = Vector2.one * (34f + Mathf.Sin(Time.unscaledTime * 6f) * 4f);
        }
        if (overMap)
        {
            var world = new Vector2(worldOrigin.x + mouseUv.x * worldSize.x, worldOrigin.y + mouseUv.y * worldSize.y);
            coordText.text = "İmleç  X " + Mathf.RoundToInt(world.x) + " · Z " + Mathf.RoundToInt(world.y) + "   ·   Sen  X " + Mathf.RoundToInt(player.position.x) + " · Z "
                + Mathf.RoundToInt(player.position.z) + "   ·   Yakınlık ×" + zoom.ToString("0.0");
        }
        else coordText.text = "Sen  X " + Mathf.RoundToInt(player.position.x) + " · Z " + Mathf.RoundToInt(player.position.z) + "   ·   Yakınlık ×" + zoom.ToString("0.0");
    }

    private EcoRegion RegionAtUv(Vector2 uv)
    {
        var world = new Vector3(worldOrigin.x + uv.x * worldSize.x, player != null ? player.position.y : 0f, worldOrigin.y + uv.y * worldSize.y);
        return EcoRegion.At(world);
    }

    private void CollectPois()
    {
        pois.Clear();
        foreach (var m in EcoMapMarker.All)
            if (m != null && m.isActiveAndEnabled) pois.Add(new Poi { position = m.transform.position, icon = m.icon, title = m.title, detail = m.detail });
        foreach (var f in FindObjectsByType<EcoFactory>(FindObjectsSortMode.None))
            pois.Add(new Poi { position = f.transform.position, icon = "poi_factory", title = "Fabrika",
                detail = (f.Filtered ? "Büyük filtre takılı" : "Filtresiz: [E] büyük filtre tak") + " · " + (f.Converted ? "temiz enerjiye dönüştü" : "kirli yakıt") });
        var station = EcoGlacierStation.Current;
        if (station != null)
            pois.Add(new Poi { position = station.transform.position, icon = "poi_station", title = "Buzul izleme istasyonu", size = 30f,
                detail = station.Active ? "Etkin · Ortalama karbon " + station.AverageCarbon.ToString("0.0") + " / 20" : "Kapalı: yanına gidip [E] ile etkinleştir. Final terminali burada." });
        foreach (var inlet in FindObjectsByType<EcoPollutionInlet>(FindObjectsSortMode.None))
            pois.Add(new Poi { position = inlet.transform.position, icon = inlet.Remediated ? "ui_check" : "poi_inlet", title = "Kirletici giriş",
                detail = inlet.Remediated ? "Kapatıldı. Gölün suyunu arıtıp geri döndür." : "Açık: [E] ile sızıntıyı kapat (10M + 5P).", pulse = !inlet.Remediated });
        foreach (var site in FindObjectsByType<EcoHydroSite>(FindObjectsSortMode.None))
            pois.Add(new Poi { position = site.transform.position, icon = "poi_hydro", title = "Doğal akış alanı", detail = "Küçük hidroelektrik yalnız burada kurulabilir; gece de üretir." });
        foreach (var charger in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None))
            pois.Add(new Poi { position = charger.transform.position, icon = "poi_charge", title = "Şarj istasyonu", detail = "Araç elektriğini doldurur; ağdaki üretime bağlıdır." });
        foreach (var fire in FindObjectsByType<EcoFire>(FindObjectsSortMode.None))
            if (fire.Burning) pois.Add(new Poi { position = fire.transform.position, icon = "poi_fire", title = "Yangın", detail = "Vakumla nişan al, söndürür.", pulse = true });
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (enemy != null && enemy.Health > 0f && enemy.State != EnemyBrain.Behaviour.Dead)
                pois.Add(new Poi { position = enemy.transform.position, icon = "poi_enemy", title = "Çevresel tehdit", detail = "Türüne uygun araçla müdahale et.", size = 20f });
        var tracked = EcoQuestBoard.Tracked(player.position);
        if (tracked != null && tracked.target.HasValue)
            pois.Add(new Poi { position = tracked.target.Value, icon = "poi_quest", title = "Takip edilen görev · " + tracked.title, detail = tracked.detail, size = 34f, pulse = true });
    }

    // ------------------------------------------------------------------ info card

    private void RefreshInfoCard(EcoRegion r)
    {
        float w = infoViewport.sizeDelta.x;
        bool has = r != null;
        bool changed = infoRegion != r;
        infoRegion = r;
        infoEmpty.gameObject.SetActive(!has);
        EcoUI.Layout(infoEmpty, new Vector2(16f, -infoCard.sizeDelta.y * 0.5f + 30f), new Vector2(infoCard.sizeDelta.x - 32f, 60f));
        infoViewport.gameObject.SetActive(has);
        if (!has) return;
        if (changed)
        {
            infoScroll.StopMovement();
            infoContent.anchoredPosition = Vector2.zero;
        }
        var info = r.GetComponent<EcoRegionInfo>();
        var clock = EcoWorldClock.Current;
        var values = new float[7]; Sample(r, values);
        float health = values[5];

        float y = -16f;
        EcoUI.SetIcon(infoIcon, info != null ? EcoIcons.Region(info.kind, r.waterPollution > 35f) : "region_city");
        EcoUI.LayoutIcon(infoIcon, new Vector2(16f, y), 50f);
        infoName.text = r.regionName;
        float titleHeight = NoteHeight(infoName, new Vector2(76f, y - 2f), w - 92f, 26f);
        infoBadge.color = Color.Lerp(EcoNotebook.Inset, EcoUI.GoodColor(health / 100f), 0.3f);
        infoBadgeText.text = StateName(health) + "  ·  " + Mathf.RoundToInt(health);
        EcoUI.Place(infoBadge.rectTransform, new Vector2(76f, y - titleHeight - 4f), new Vector2(138f, 20f));
        EcoUI.Layout(infoBadgeText, Vector2.zero, new Vector2(138f, 20f));
        infoTag.text = info != null ? info.tagline : "";
        y -= titleHeight + 32f;
        y -= NoteHeight(infoTag, new Vector2(16f, y), w - 32f, 16f) + 10f;
        for (int i = 0; i < 6; i++)
        {
            float v = values[i];
            float good = i == 3 ? 1f - v / 100f : v / 100f;
            EcoUI.LayoutIcon(infoRowIcon[i], new Vector2(16f, y), 18f);
            EcoUI.Layout(infoRowName[i], new Vector2(40f, y), new Vector2(130f, 18f));
            infoRowValue[i].text = Mathf.RoundToInt(v).ToString();
            infoRowValue[i].color = Color.Lerp(EcoUI.GoodColor(good), EcoNotebook.Ink, 0.65f);
            EcoUI.Layout(infoRowValue[i], new Vector2(w - 56f, y), new Vector2(40f, 18f));
            EcoUI.Place(infoRowTrack[i], new Vector2(170f, y - 7f), new Vector2(w - 232f, 5f));
            EcoUI.SetMeter(infoRowFill[i], infoRowTrack[i], v / 100f, EcoUI.GoodColor(good));
            // The measured value a scientist would quote, under the 0–100 index.
            infoRowReading[i].text = Reading(r, i, false);
            bool reading = infoRowReading[i].text.Length > 0;
            infoRowReading[i].gameObject.SetActive(reading);
            if (reading) EcoUI.Layout(infoRowReading[i], new Vector2(40f, y - 16f), new Vector2(w - 56f, 14f));
            y -= reading ? 34f : 22f;
        }
        y -= 8f;
        EcoUI.Layout(climateHeader, new Vector2(16f, y), new Vector2(w - 32f, 16f));
        y -= 22f;
        float wind = RegionWind(r, clock);
        float sun = clock != null ? Mathf.Clamp01(clock.SolarFactor * (info != null ? info.sunExposure : 1f) * r.SolarTransmission) : 0.5f;
        var chips = new (string icon, string text)[]
        {
            ("temperature", values[6].ToString("0.0") + " °C\n" + TempTag(values[6])),
            ("w_wind", "%" + Mathf.RoundToInt(wind * 100f) + "\n" + WindTag(wind)),
            ("w_sun", "%" + Mathf.RoundToInt(sun * 100f) + "\nGüneş"),
            (EcoIcons.Weather(clock), clock != null ? clock.WeatherName + "\nHava" : "—"),
        };
        float cw = (w - 32f) * 0.5f;
        for (int i = 0; i < 4; i++)
        {
            float x = 16f + i % 2 * cw;
            float top = y - i / 2 * 36f;
            EcoUI.SetIcon(climateIcon[i], chips[i].icon);
            EcoUI.LayoutIcon(climateIcon[i], new Vector2(x, top - 3f), 22f);
            climateText[i].text = chips[i].text;
            EcoUI.Layout(climateText[i], new Vector2(x + 29f, top), new Vector2(cw - 34f, 32f));
        }
        y -= 80f;

        // This region's quests from the quest board, easy first and harder below; a click tracks one. Finished projects
        // and live readings (fish, station) follow as plain status lines.
        var tasks = new List<(string icon, string text, float progress, EcoQuestBoard.Quest quest)>();
        foreach (var q in EcoQuestBoard.For(r, player.position).OrderBy(q => q.level))
            tasks.Add((q.icon, LevelTag(q.level) + "  <b>" + q.title + "</b>" + (EcoQuestBoard.IsTracked(q) ? "  <color=#" + EcoUI.Hex(EcoNotebook.Accent) + ">· takipte</color>" : "") + "\n" + q.detail, q.progress, q));
        var projects = EcoProjects.Current;
        if (info != null && info.projectIndex >= 0 && projects != null && info.projectIndex < projects.All.Count && projects.All[info.projectIndex].done)
            tasks.Add(("ui_check", "<b>" + projects.All[info.projectIndex].name + "</b>  <color=#" + EcoUI.Hex(EcoNotebook.Accent) + ">tamamlandı</color>\nBaşarı korunuyor", 1f, null));
        foreach (var spot in FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None))
            if (r.Contains(spot.transform.position)) { tasks.Add((r.waterPollution < 35f ? "fish" : "fish_dirty", "Balık nüfusu " + Mathf.FloorToInt(spot.population) + " / 12" + (spot.population <= 4f ? " · av kapalı" : ""), spot.population / 12f, null)); break; }
        var station = EcoGlacierStation.Current;
        if (station != null && station.Active && r.Contains(station.transform.position))
            tasks.Add(("poi_station", "İstasyon etkin · karbon " + station.AverageCarbon.ToString("0.0") + " / 20", -1f, null));
        if (tasks.Count == 0) tasks.Add(("ui_check", "Açık görev yok. Bölgeyi korumaya devam et.", -1f, null));

        EcoUI.Layout(tasksHeader, new Vector2(16f, y), new Vector2(w - 32f, 16f));
        y -= 22f;
        for (int i = 0; i < taskIcon.Length; i++)
        {
            bool used = i < tasks.Count;
            taskIcon[i].gameObject.SetActive(used); taskText[i].gameObject.SetActive(used); taskTrack[i].gameObject.SetActive(used && tasks[i].progress >= 0f);
            taskQuest[i] = used ? tasks[i].quest : null;
            taskHit[i].gameObject.SetActive(used && tasks[i].quest != null);
            if (!used) continue;
            float rowTop = y;
            EcoUI.SetIcon(taskIcon[i], tasks[i].icon);
            EcoUI.LayoutIcon(taskIcon[i], new Vector2(16f, y), 22f);
            taskText[i].text = tasks[i].text;
            float h = NoteHeight(taskText[i], new Vector2(46f, y - 1f), w - 62f, 23f);
            y -= h + 4f;
            if (tasks[i].progress >= 0f)
            {
                EcoUI.Place(taskTrack[i], new Vector2(46f, y), new Vector2(w - 62f, 5f));
                EcoUI.SetMeter(taskFill[i], taskTrack[i], tasks[i].progress, tasks[i].progress >= 1f ? EcoNotebook.Accent : EcoUI.Amber);
                y -= 10f;
            }
            y -= 4f;
            if (tasks[i].quest != null)
            {
                EcoUI.Place(taskHit[i].rectTransform, new Vector2(10f, rowTop + 2f), new Vector2(w - 20f, rowTop - y));
                taskHit[i].color = EcoQuestBoard.IsTracked(tasks[i].quest) ? EcoNotebook.Selected : Color.clear;
                taskHit[i].transform.SetAsFirstSibling(); // behind the text, still catching the click
            }
        }
        infoContent.sizeDelta = new Vector2(w, Mathf.Max(infoViewport.rect.height, -y + 8f));
    }

    private static float NoteHeight(Text label, Vector2 position, float width, float minimum)
    {
        EcoUI.Layout(label, position, new Vector2(width, minimum));
        float height = Mathf.Max(minimum, Mathf.Ceil(label.preferredHeight / EcoUI.RasterScale) + 2f);
        EcoUI.Layout(label, position, new Vector2(width, height));
        return height;
    }

    private static string LevelTag(int level) =>
        "<color=#" + EcoUI.Hex(Color.Lerp(LevelColor(level), EcoNotebook.Ink, 0.45f)) + "><b>" + EcoQuestBoard.LevelNames[Mathf.Clamp(level, 1, 3)] + "</b></color>";
    private static Color LevelColor(int level) => level <= 1 ? EcoNotebook.Accent : level == 2 ? EcoUI.Amber : EcoUI.Coral;

    // ================================================================== quest board

    private void BuildBoard()
    {
        boardRoot = NotebookBackdrop("Quest board");
        boardCard = EcoNotebook.Surface(EcoUI.Panel("Card", boardRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoNotebook.Paper), EcoNotebook.Paper).rectTransform;
        var icon = EcoUI.Icon("Icon", boardCard, "poi_quest", new Vector2(0, 1), new Vector2(0, 1), 28f); EcoUI.LayoutIcon(icon, new Vector2(18f, -16f), 28f);
        var title = EcoUI.Label("Title", boardCard, "Görev panosu", 23, TextAnchor.MiddleLeft, EcoNotebook.Ink, true); EcoUI.Layout(title, new Vector2(56f, -15f), new Vector2(320f, 30f));
        boardHint = EcoUI.Label("Hint", boardCard, "", 13, TextAnchor.MiddleLeft, EcoNotebook.Muted);
        NotebookClose(boardCard, () => SetBoard(false), "Kapat " + EcoKeys.Tag(EcoKeys.Action.Quests) + " / [Esc]");
        NotebookRule(boardCard, 88f, 18f);
        var viewport = EcoUI.Panel("Viewport", boardCard, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        boardViewport = viewport.rectTransform;
        boardContent = EcoUI.Rect("Content", boardViewport, new Vector2(0, 1), new Vector2(0, 1));
        NotebookScroll(viewport, boardContent);
        boardEmpty = EcoUI.Label("Empty", boardCard, "Şu an açık görev yok. Bölgeler temiz, korumaya devam et.", 15, TextAnchor.MiddleCenter, EcoNotebook.Muted);
        boardRoot.gameObject.SetActive(false);
    }

    public void SetBoard(bool open)
    {
        if (open && MapOpen) SetMap(false);
        if (open && helpOpen) SetHelp(false);
        BoardOpen = open;
        boardRoot.gameObject.SetActive(open);
        EcoUIFlow.SetPanel(boardOwner, open);
        if (open) { boardRefreshAt = 0f; boardContent.anchoredPosition = Vector2.zero; }
        else EcoUI.HideTooltip(this);
    }

    private BoardRow Row(int index)
    {
        while (boardRows.Count <= index)
        {
            var row = new BoardRow();
            row.bg = EcoNotebook.Surface(EcoUI.Panel("Quest", boardContent, new Vector2(0, 1), new Vector2(0, 1), EcoNotebook.Sheet), EcoNotebook.Sheet);
            row.root = row.bg.rectTransform;
            var button = row.bg.gameObject.AddComponent<Button>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(0.9f, 0.96f, 0.86f); colors.pressedColor = new Color(0.8f, 0.86f, 0.78f);
            button.colors = colors; button.targetGraphic = row.bg;
            button.onClick.AddListener(() => { if (row.quest == null) return; EcoQuestBoard.Toggle(row.quest); boardRefreshAt = 0f; });
            row.icon = EcoUI.Icon("Icon", row.root, "poi_quest", new Vector2(0, 1), new Vector2(0, 1), 32f);
            row.chip = EcoUI.Panel("Level", row.root, new Vector2(0, 1), new Vector2(0, 1), EcoNotebook.Inset, false);
            row.chipText = EcoUI.Label("Level text", row.chip.transform, "", 11, TextAnchor.MiddleCenter, EcoNotebook.Ink, true);
            row.title = EcoUI.Label("Title", row.root, "", 15, TextAnchor.MiddleLeft, EcoNotebook.Ink, true);
            row.detail = EcoUI.Label("Detail", row.root, "", 12, TextAnchor.MiddleLeft, EcoNotebook.Muted);
            row.action = EcoUI.Label("Action", row.root, "", 12, TextAnchor.MiddleRight, EcoNotebook.Accent, true);
            row.fill = EcoUI.Meter("Progress", row.root, out row.track);
            row.track.GetComponent<Image>().color = EcoNotebook.Rule;
            boardRows.Add(row);
        }
        return boardRows[index];
    }

    private Text Header(int index)
    {
        while (boardHeaders.Count <= index) boardHeaders.Add(EcoUI.Label("Region", boardContent, "", 14, TextAnchor.MiddleLeft, EcoNotebook.Accent, true));
        return boardHeaders[index];
    }

    private void RefreshBoard()
    {
        if (!FindPlayer()) return;
        var size = ((RectTransform)overlay.transform).rect.size;
        float cw = Mathf.Min(800f, size.x - 32f), ch = Mathf.Min(660f, size.y - 48f);
        boardCard.sizeDelta = new Vector2(cw, ch);
        EcoUI.Place(boardViewport, new Vector2(18f, -98f), new Vector2(cw - 36f, ch - 114f));
        float w = cw - 36f;
        var tracked = EcoQuestBoard.Tracked(player.position);
        boardHint.text = tracked != null ? "Takipte: <b>" + tracked.title + "</b>\nTekrar tıklayarak takibi bırak." : "Bir görevi seç, hedef oku yolu göstersin.";
        EcoUI.Layout(boardHint, new Vector2(56f, -45f), new Vector2(cw - 94f, 38f));

        var quests = EcoQuestBoard.All(player.position);
        // The tutorial can precede the local region. Keep its region together in the displayed list.
        var groups = quests.GroupBy(q => q.region).ToList();
        var here = EcoRegion.At(player.position);
        float y = 0f; int rows = 0, headers = 0; EcoRegion last = null;
        foreach (var q in groups.SelectMany(group => group))
        {
            if (q.region != last)
            {
                last = q.region;
                if (headers > 0) y -= 10f;
                var header = Header(headers++); header.gameObject.SetActive(true);
                int count = quests.Count(o => o.region == q.region);
                header.text = q.region.regionName + "   <color=#" + EcoUI.Hex(EcoNotebook.Muted) + ">" + count + " görev" + (q.region == here ? " · buradasın" : "") + "</color>";
                EcoUI.Layout(header, new Vector2(4f, y), new Vector2(w - 8f, 22f));
                y -= 26f;
            }
            var row = Row(rows++); row.quest = q; row.root.gameObject.SetActive(true);
            bool on = EcoQuestBoard.IsTracked(q);
            row.bg.color = on ? EcoNotebook.Selected : EcoNotebook.Sheet;
            EcoUI.SetIcon(row.icon, q.icon); EcoUI.LayoutIcon(row.icon, new Vector2(12f, -12f), 28f);
            row.chip.color = Color.Lerp(EcoNotebook.Inset, LevelColor(q.level), 0.25f);
            EcoUI.Place(row.chip.rectTransform, new Vector2(52f, -8f), new Vector2(54f, 18f));
            row.chipText.text = EcoQuestBoard.LevelNames[Mathf.Clamp(q.level, 1, 3)];
            EcoUI.Layout(row.chipText, Vector2.zero, new Vector2(54f, 18f));
            row.title.text = q.title;
            float titleHeight = NoteHeight(row.title, new Vector2(114f, -6f), w - 250f, 22f);
            row.detail.text = q.detail;
            float detailTop = titleHeight + 10f;
            float detailHeight = NoteHeight(row.detail, new Vector2(52f, -detailTop), w - 188f, 20f);
            float rowHeight = Mathf.Max(56f, detailTop + detailHeight + 7f);
            EcoUI.Place(row.root, new Vector2(0f, y), new Vector2(w, rowHeight));
            row.action.text = on ? "Takipte · Bırak" : "Takip et ›";
            EcoUI.Layout(row.action, new Vector2(w - 130f, -10f), new Vector2(118f, 20f));
            bool meter = q.progress >= 0f;
            row.track.gameObject.SetActive(meter);
            if (meter)
            {
                EcoUI.Place(row.track, new Vector2(w - 130f, -38f), new Vector2(118f, 5f));
                EcoUI.SetMeter(row.fill, row.track, q.progress, q.progress >= 1f ? EcoNotebook.Accent : EcoUI.Amber);
            }
            y -= rowHeight + 6f;
        }
        for (int i = rows; i < boardRows.Count; i++) { boardRows[i].quest = null; boardRows[i].root.gameObject.SetActive(false); }
        for (int i = headers; i < boardHeaders.Count; i++) boardHeaders[i].gameObject.SetActive(false);
        boardContent.sizeDelta = new Vector2(w, -y + 8f);
        boardEmpty.gameObject.SetActive(quests.Count == 0);
        EcoUI.Layout(boardEmpty, new Vector2(16f, -ch * 0.5f + 20f), new Vector2(cw - 32f, 40f));
    }

    // ================================================================== layout

    private int DisplayWidth => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.width));
    private int DisplayHeight => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.height));

    private void ApplyLayout()
    {
        dirty = false;
        lastWidth = DisplayWidth; lastHeight = DisplayHeight; lastMode = EcoUI.Mode;
        float scale = EcoUI.Scale(canvas);
        scaler.scaleFactor = scale; canvas.scaleFactor = scale;
        overlayScaler.scaleFactor = scale; overlay.scaleFactor = scale;
        float width = lastWidth / scale, height = lastHeight / scale;
        const float margin = 16f;
        bool compact = width < 1250f;
        var mode = EcoUI.Mode;
        bool hidden = mode == EcoUI.HudMode.Hidden, sade = mode == EcoUI.HudMode.Compact;
        float leftWidth = EcoQuestHUD.LeftColumnWidth, rightWidth = compact ? 250f : 276f;
        bool narrow = width - leftWidth - rightWidth - 4f * margin < 280f;

        // The region badge shows a quick reading; the expanded card retains all eight measurements.
        laidOutVitalsBottom = EcoQuestHUD.VitalsBottom;
        laidOutDockWidth = EcoQuestHUD.DockWidth; laidOutDockTop = EcoQuestHUD.DockTop;
        envCard.SetVisible(!hidden);
        bool compactEnvironment = envCard.IsCollapsed || sade || narrow;
        float envBody = sade ? 0f : 8 * 29f + 8f;
        envCard.Layout(new Vector2(margin, -(EcoQuestHUD.VitalsBottom + 7f)), leftWidth, envBody, true);
        envCard.background.enabled = true;
        envCard.body.gameObject.SetActive(!compactEnvironment);
        if (compactEnvironment)
        {
            const float badgeWidth = EcoQuestHUD.CompactLeftWidth;
            EcoUI.Place(envCard.root, new Vector2(margin, -(EcoQuestHUD.VitalsBottom + 7f)), new Vector2(badgeWidth, 34f));
            EcoUI.LayoutIcon(envCard.icon, new Vector2(8f, -8f), 17f);
            EcoUI.Layout(envCard.title, new Vector2(31f, -7f), new Vector2(badgeWidth - 105f, 20f));
            EcoUI.Layout(envCard.summary, new Vector2(badgeWidth - 69f, -8f), new Vector2(39f, 18f));
            envCard.summary.alignment = TextAnchor.MiddleRight;
            envCard.title.resizeTextForBestFit = true;
            envCard.title.resizeTextMinSize = 11 * EcoUI.RasterScale;
            envCard.title.resizeTextMaxSize = 13 * EcoUI.RasterScale;
            envCard.summary.gameObject.SetActive(true);
            envCard.rule.gameObject.SetActive(false);
            ((RectTransform)envCard.toggle.transform).anchoredPosition = new Vector2(-6f, -6f);
        }
        for (int i = 0; i < 8; i++)
        {
            float y = -6f - i * 29f;
            EcoUI.LayoutIcon(rowIcons[i], new Vector2(12f, y), 20f);
            float valueWidth = i >= 6 ? 132f : i <= 3 ? 112f : 44f; // rows 0–3 carry a real reading (AQI, mg/L, %, ppm)
            EcoUI.Layout(rowNames[i], new Vector2(38f, y), new Vector2(leftWidth - valueWidth - 58f, 18f));
            EcoUI.Layout(rowValues[i], new Vector2(leftWidth - valueWidth - 12f, y), new Vector2(valueWidth, 18f));
            EcoUI.Place(rowTracks[i], new Vector2(38f, y - 20f), new Vector2(leftWidth - 50f, 4f));
        }

        // The slim map frame also contains time and weather, with room for the map controls.
        mapCard.SetVisible(!hidden);
        const float mapOuter = CompactMapWidth;
        const float mapSide = mapOuter - 10f;
        bool mapCollapsed = mapCard.IsCollapsed;
        float statusTop = mapCollapsed ? 26f : mapSide + 8f;
        float mh = statusTop + 26f;
        mapCard.Layout(new Vector2(-margin, -margin), mapOuter, mapOuter);
        EcoUI.Place(mapCard.root, new Vector2(-margin, -margin), new Vector2(mapOuter, mh));
        mapCard.background.enabled = true;
        mapCard.title.gameObject.SetActive(false); mapCard.icon.gameObject.SetActive(false);
        mapCard.summary.gameObject.SetActive(false); mapCard.rule.gameObject.SetActive(false);
        EcoUI.Place(mapCard.body, Vector2.zero, new Vector2(mapOuter, mapOuter));
        mapCard.body.gameObject.SetActive(!mapCollapsed);
        EcoUI.Place(miniFrame, new Vector2(5f, -5f), new Vector2(mapSide, mapSide));
        EcoUI.Place(miniStatus, new Vector2(4f, -statusTop), new Vector2(mapOuter - 8f, 23f));
        EcoUI.Place(miniWeatherHit, Vector2.zero, miniStatus.sizeDelta);
        EcoUI.LayoutIcon(clockIcon, new Vector2(3f, -4f), 13f);
        EcoUI.Layout(clockText, new Vector2(20f, -2f), new Vector2(67f, 18f));
        clockText.fontSize = 11 * EcoUI.RasterScale;
        EcoUI.LayoutIcon(weatherIcon, new Vector2(91f, -3f), 14f);
        EcoUI.Layout(weatherText, new Vector2(108f, -2f), new Vector2(51f, 18f));
        weatherText.resizeTextForBestFit = true; weatherText.resizeTextMinSize = 9 * EcoUI.RasterScale;
        weatherText.resizeTextMaxSize = 11 * EcoUI.RasterScale;
        EcoUI.Layout(miniNorth, new Vector2(mapSide * 0.5f - 10f, -4f), new Vector2(20f, 16f));
        var bb = (RectTransform)bigMapButton.transform; bb.anchorMin = bb.anchorMax = new Vector2(0, 1); bb.pivot = new Vector2(0, 1);
        EcoUI.Place(bb, mapCollapsed ? new Vector2(5f, -4f) : new Vector2(mapOuter - 28f, -(mapSide - 22f)), Vector2.one * 21f);
        ((RectTransform)mapCard.toggle.transform).anchoredPosition = new Vector2(-4f, -4f);
        mapCard.toggle.transform.SetAsLastSibling();
        MapBottom = hidden ? margin : margin + mh;

        // Banner under the objective card.
        float bannerWidth = narrow ? Mathf.Min(440f, width - margin * 2f)
            : Mathf.Min(470f, width - leftWidth - rightWidth - 4f * margin);
        float bannerTop = Mathf.Max(margin + 44f, EcoQuestHUD.ObjectiveBottomPixels / Mathf.Max(.01f, scale) + 7f);
        EcoUI.Place(banner, new Vector2(0f, -bannerTop), new Vector2(bannerWidth, 46f));
        bannerBottom = bannerTop + 46f;
        // The compact region badge already carries this information when a short viewport cannot fit a second notice.
        bannerFits = bannerBottom + 64f < height - EcoQuestHUD.DockTop;
        EcoUI.Place(bannerStrip.rectTransform, new Vector2(2f, -6f), new Vector2(2f, 34f));
        EcoUI.LayoutIcon(bannerIcon, new Vector2(9f, -9f), 27f);
        EcoUI.Layout(bannerTitle, new Vector2(43f, -3f), new Vector2(bannerWidth - 166f, 22f));
        EcoUI.Layout(bannerState, new Vector2(bannerWidth - 119f, -5f), new Vector2(110f, 18f));
        EcoUI.Layout(bannerSub, new Vector2(43f, -25f), new Vector2(bannerWidth - 52f, 18f));

        // Quick actions use the space beside the compact belt and wrap into a short grid on small screens.
        // In Gizli only the HUD button stays so the player can bring the interface back.
        int count = actionButtons.Count(a => !hidden || a.id == "hud");
        float dockHalf = hidden ? 0f : EcoQuestHUD.DockWidth * 0.5f;
        float availableLeft = width * 0.5f - dockHalf - margin - 12f;
        const float actionStep = 34f;
        bool aboveBelt = !hidden && availableLeft < actionStep;
        int columns = aboveBelt ? Mathf.Min(4, count) : Mathf.Clamp(Mathf.FloorToInt(availableLeft / actionStep), 1, count);
        int actionIndex = 0;
        foreach (var (button, _, id) in actionButtons)
        {
            bool show = !hidden || id == "hud";
            button.gameObject.SetActive(show);
            if (!show) continue;
            var rt = (RectTransform)button.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = new Vector2(4f + actionIndex % columns * actionStep, 4f + actionIndex / columns * actionStep);
            actionIndex++;
        }
        int actionRows = Mathf.CeilToInt(count / (float)columns);
        EcoUI.Place(actions, new Vector2(margin, aboveBelt ? EcoQuestHUD.DockTop + 8f : margin),
            new Vector2((columns - 1) * actionStep + 38f, (actionRows - 1) * actionStep + 36f));

        // Local quests follow the actual environment-card height, including its collapsed state.
        float questsTop = -envCard.root.anchoredPosition.y + envCard.root.rect.height + 10f;
        float actionsTop = actions.anchoredPosition.y + actions.rect.height;
        questSidebar?.SetVisible(!hidden && !OverlayOpen && !helpOpen && !EcoUI.ForeignWindowOpen && !EcoMarketUI.IsOpen);
        questSidebar?.Layout(questsTop, leftWidth, Mathf.Max(0f, height - questsTop - actionsTop - 12f),
            width, height, EcoQuestHUD.DockTop);

        // Mode toast.
        EcoUI.Place(modeToastBg.rectTransform, new Vector2(0f, EcoQuestHUD.DockTop + 12f), new Vector2(380f, 46f));
        EcoUI.Layout(modeToast, new Vector2(10f, -12f), new Vector2(360f, 24f));

        // Help sheet.
        help.localScale = Vector3.one * Mathf.Min(1f, (width - 40f) / HelpSize.x, (height - 40f) / HelpSize.y);

        // Big map: map square on the left, information card on the right.
        float availableMapWidth = Mathf.Min(1180f, width - 40f), cardH = Mathf.Min(760f, height - 40f);
        float infoW = Mathf.Clamp(availableMapWidth * 0.37f, 310f, 420f);
        const float mapTop = 78f;
        float side = Mathf.Min(cardH - 150f, availableMapWidth - infoW - 54f);
        float cardW = side + infoW + 54f;
        EcoUI.Place(bigCard, Vector2.zero, new Vector2(cardW, cardH));
        EcoUI.Layout(bigTitle, new Vector2(57f, -13f), new Vector2(cardW - 115f, 31f));
        bigHint.text = "Tekerlek: yakınlaştır  ·  Sürükle: kaydır  ·  Tıkla: bölgeyi sabitle  ·  Sağ tık: bırak";
        EcoUI.Layout(bigHint, new Vector2(57f, -44f), new Vector2(cardW - 115f, 20f));
        EcoUI.Place(bigFrame, new Vector2(18f, -mapTop), new Vector2(side, side));
        EcoUI.Place(bigView, Vector2.zero, new Vector2(side, side));
        float mapBottom = mapTop + side;
        EcoUI.Layout(coordText, new Vector2(18f, -mapBottom - 4f), new Vector2(side, 18f));
        EcoUI.Place(legend, new Vector2(18f, -mapBottom - 24f), new Vector2(side, 38f));
        const int legendColumns = 5;
        float column = side / legendColumns;
        for (int i = 0; i < legendItems.Count; i++)
        {
            float x = i % legendColumns * column, y = -(i / legendColumns) * 19f;
            var (icon, label) = legendItems[i];
            EcoUI.LayoutIcon(icon, new Vector2(x, y), 16f);
            EcoUI.Layout(label, new Vector2(x + 19f, y), new Vector2(column - 21f, 17f));
        }
        float infoHeight = cardH - mapTop - 17f;
        EcoUI.Place(infoCard, new Vector2(-18f, -mapTop), new Vector2(infoW, infoHeight));
        EcoUI.Place(infoViewport, new Vector2(1f, -1f), new Vector2(infoW - 2f, infoHeight - 2f));
        if (MapOpen) mapRefreshAt = 0f;
    }
}
