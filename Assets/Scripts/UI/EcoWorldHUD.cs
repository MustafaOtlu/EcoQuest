using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// World layer of the HUD (GDD §13):
//  - environment card: the region's air, water, carbon, biodiversity, ecosystem health and temperature, with icons,
//  - a zoomed mini map that follows the player (north up) with the main points of interest,
//  - the [M] world map: zoom (wheel), pan (drag), region outlines and weather/wind symbols, points of interest and an
//    information card for whatever the mouse is over (state, climate, live tasks, opportunities). Click pins a region.
//  - the quick-action column (map, journal, build, skills, settings, help, HUD mode), the [F1] controls sheet,
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
    public static float MapBottom { get; private set; } = 330f;
    public static bool MapOpen { get; private set; }

    private const float MiniWindow = 56f;  // metres shown across the corner map
    private static readonly string[] RowNames = { "Hava kalitesi", "Su kalitesi", "Toprak kalitesi", "Karbon yükü", "Biyoçeşitlilik", "Ekosistem sağlığı" };
    private static readonly string[] RowIcons = { "air", "water_quality", "organic", "carbon", "biodiversity", "ecosystem" };
    private static readonly string[] RowTips =
    {
        "Hava kalitesi = 100 − hava kirliliği. Fabrika filtresi ve yeşil alan iyileştirir.",
        "Su kalitesi = 100 − su kirliliği. Kirletici girişi kapat, arıtıp göle döndür.",
        "Toprak kalitesi = 100 − toprak kirliliği. Atık toplamak ve gübre iyileştirir.",
        "Karbon yükü (düşük iyi). Temiz enerji dönüşümü ve habitat azaltır; buzul bunu izler.",
        "Biyoçeşitlilik: bitki örtüsü, habitat ve su kalitesinden hesaplanır.",
        "Ekosistem sağlığı: hava, su, toprak ve biyoçeşitliliğin ortak özeti.",
    };

    private PlayerWeaponSystem weapons;
    private Transform player;
    private Canvas canvas, overlay;
    private CanvasScaler scaler, overlayScaler;
    private int lastWidth, lastHeight;
    private float laidOutVitalsBottom = -1f;
    private bool dirty = true;
    private EcoUI.HudMode lastMode;
    private float refreshAt, regionScanAt, bannerUntil, startTime, mapRefreshAt;
    private readonly List<EcoRegion> regions = new List<EcoRegion>();
    private EcoRegion current;

    // Environment card
    private EcoUI.Card envCard;
    private readonly Image[] rowIcons = new Image[7], rowFills = new Image[7];
    private readonly RectTransform[] rowTracks = new RectTransform[7];
    private readonly Text[] rowNames = new Text[7], rowValues = new Text[7];
    private readonly float[] shown = new float[7], target = new float[7];

    // Mini map card
    private EcoUI.Card mapCard;
    private RectTransform miniFrame, miniArrow;
    private RawImage miniImage;
    private Text miniNorth;
    private Button bigMapButton;
    private Image clockIcon, weatherIcon, windIcon;
    private Text clockText, weatherText, windText;
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
    private RectTransform help;
    private Image skillsAlert;
    private bool helpOpen;
    private readonly EcoUI.HudPanel helpOwner = new EcoUI.HudPanel();
    private Text modeToast; private float modeToastUntil; private Image modeToastBg;

    // Big map
    private RectTransform bigRoot, bigCard, bigFrame, bigView, infoCard, legend;
    private Image bigDim;
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
    private readonly Text[] infoRowName = new Text[6], infoRowValue = new Text[6];
    private Text climateHeader, tasksHeader, featuresHeader;
    private readonly Image[] climateIcon = new Image[4]; private readonly Text[] climateText = new Text[4];
    private readonly Image[] taskIcon = new Image[7], taskFill = new Image[7]; private readonly RectTransform[] taskTrack = new RectTransform[7]; private readonly Text[] taskText = new Text[7];
    private readonly Image[] featureIcon = new Image[4]; private readonly Text[] featureText = new Text[4];

    private void Start()
    {
        startTime = Time.unscaledTime;
        canvas = EcoUI.CreateCanvas("EcoWorld HUD", 19, out scaler);
        // The world map and the controls sheet sit above every HUD card (their dim covers the HUD).
        overlay = EcoUI.CreateCanvas("EcoWorld Map", 30, out overlayScaler);
        BuildEnvironmentCard();
        BuildMiniMap();
        BuildBanner();
        BuildActions();
        BuildHelp();
        BuildBigMap();
        BuildIntro();
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
        MapOpen = false;
        if (canvas != null) Destroy(canvas.gameObject);
        if (overlay != null) Destroy(overlay.gameObject);
    }

    // ================================================================== update

    private void Update()
    {
        if (canvas == null) return;
        bool foreign = EcoMarketUI.IsOpen || EcoUI.ForeignWindowOpen;
        canvas.enabled = !foreign;
        overlay.enabled = !foreign;
        if (lastWidth != DisplayWidth || lastHeight != DisplayHeight || lastMode != EcoUI.Mode) dirty = true;
        if (Mathf.Abs(laidOutVitalsBottom - EcoQuestHUD.VitalsBottom) > 0.5f) dirty = true;
        if (dirty) ApplyLayout();
        HandleKeys();
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
                weatherText.color = harsh ? EcoUI.Amber : EcoUI.Ink;
                if (harsh && soon != announcedWeather && weapons != null) { announcedWeather = soon; weapons.ShowFeedback("Hava değişiyor: yaklaşık 45 sn içinde " + soon.ToLowerInvariant() + ". Açık ekinler sulanır, güneş üretimi düşer."); }
                if (!harsh) announcedWeather = "";
                float wind = RegionWind(current, clock);
                windText.text = "%" + Mathf.RoundToInt(wind * 100f);
                mapCard.summary.text = clockText.text + "  ·  " + clock.WeatherName;
            }
            var skills = EcoSpecialization.Current;
            bool alert = skills != null && skills.Points > 0;
            if (skillsAlert != null && skillsAlert.gameObject.activeSelf != alert) skillsAlert.gameObject.SetActive(alert);
        }
        AnimateValues();
        UpdateMiniMap();
        UpdateBanner();
        if (MapOpen) UpdateBigMap();
    }

    private void HandleKeys()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        bool typing = PlayerController.Paused;
        if (!typing && EcoKeys.Pressed(EcoKeys.Action.Map)) SetMap(!MapOpen);
        if (keyboard.escapeKey.wasPressedThisFrame) { if (MapOpen) SetMap(false); else if (helpOpen) SetHelp(false); }
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

    private static string WindTag(float w) => w > 0.8f ? "Fırtınalı" : w > 0.55f ? "Rüzgârlı" : w > 0.25f ? "Esintili" : "Sakin";

    // ================================================================== environment card

    private void BuildEnvironmentCard()
    {
        envCard = new EcoUI.Card("environment", canvas.transform, new Vector2(0, 1), new Vector2(0, 1), "region_city", "ÇEVRE",
            "Bulunduğun bölgenin göstergeleri. Kartı daralt / genişlet.");
        for (int i = 0; i < 7; i++)
        {
            string icon = i < 6 ? RowIcons[i] : "temperature";
            rowIcons[i] = EcoUI.Icon("Row icon " + i, envCard.body, icon, new Vector2(0, 1), new Vector2(0, 1), 20f);
            rowNames[i] = EcoUI.Label("Row name " + i, envCard.body, i < 6 ? RowNames[i] : "Sıcaklık", 14, TextAnchor.MiddleLeft, EcoUI.Muted);
            rowValues[i] = EcoUI.Label("Row value " + i, envCard.body, "—", 14, TextAnchor.MiddleRight, EcoUI.Ink, true);
            rowFills[i] = EcoUI.Meter("Row meter " + i, envCard.body, out rowTracks[i]);
            var hit = EcoUI.Panel("Hit " + i, envCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
            hit.rectTransform.anchoredPosition = new Vector2(0, -4 - i * 29); hit.rectTransform.sizeDelta = new Vector2(300, 28);
            EcoUI.AddTooltip(hit.gameObject, i < 6 ? RowTips[i] : "Sıcaklık: bölgenin tabanı + karbon ve hava kirliliği etkisi. Ekinlerin uygun aralığı vardır.");
        }
    }

    private void AnimateValues()
    {
        float k = 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime);
        bool has = current != null;
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
            rowValues[i].text = has ? Mathf.RoundToInt(shown[i]).ToString() : "—";
        }
        if (has)
        {
            var info = current.GetComponent<EcoRegionInfo>();
            envCard.title.text = current.regionName.ToUpperInvariant();
            EcoUI.SetIcon(envCard.icon, info != null ? EcoIcons.Region(info.kind, current.waterPollution > 35f) : "region_city");
            float health = shown[5];
            envCard.summary.text = "<color=#" + EcoUI.Hex(EcoUI.GoodColor(health / 100f)) + ">" + StateName(health) + "</color>";
        }
        else { envCard.title.text = "BÖLGE DIŞI"; envCard.summary.text = ""; }
    }

    // ================================================================== mini map

    private void BuildMiniMap()
    {
        mapCard = new EcoUI.Card("minimap", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), "ui_map", "HARİTA", "Mini harita. Kartı daralt / genişlet.");
        var frame = EcoUI.Panel("Frame", mapCard.body, new Vector2(0, 1), new Vector2(0, 1), new Color(0.02f, 0.04f, 0.05f, 1f));
        miniFrame = frame.rectTransform;
        miniFrame.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Map image", typeof(RectTransform), typeof(RawImage)); go.layer = 5;
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(miniFrame, false); EcoUI.Stretch(rt, 2f);
        miniImage = go.GetComponent<RawImage>(); miniImage.texture = minimapTexture; miniImage.raycastTarget = false;
        miniImage.color = minimapTexture != null ? Color.white : new Color(0.2f, 0.3f, 0.3f);
        miniArrow = EcoUI.Icon("Player", miniFrame, "player", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 22f).rectTransform;
        miniNorth = EcoUI.Label("North", miniFrame, "K", 13, TextAnchor.MiddleCenter, EcoUI.Ink, true);
        miniNorth.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        bigMapButton = EcoUI.IconButton("Open map", mapCard.body, "ui_layers", 28f, () => SetMap(true), "Büyük harita " + EcoKeys.Tag(EcoKeys.Action.Map));
        // Day, time and weather strip under the map (GDD §13).
        clockIcon = EcoUI.Icon("Clock", mapCard.body, "clock", new Vector2(0, 1), new Vector2(0, 1), 20f);
        clockText = EcoUI.Label("Clock text", mapCard.body, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        weatherIcon = EcoUI.Icon("Weather", mapCard.body, "w_sun", new Vector2(0, 1), new Vector2(0, 1), 22f);
        weatherText = EcoUI.Label("Weather text", mapCard.body, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink);
        windIcon = EcoUI.Icon("Wind", mapCard.body, "w_wind", new Vector2(0, 1), new Vector2(0, 1), 22f);
        windText = EcoUI.Label("Wind text", mapCard.body, "", 13, TextAnchor.MiddleRight, EcoUI.Ink, true);
        var strip = EcoUI.Panel("Weather hit", mapCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        strip.rectTransform.anchoredPosition = new Vector2(0, -260); strip.rectTransform.sizeDelta = new Vector2(276, 26);
        EcoUI.AddTooltip(strip.gameObject, "Gün, saat ve hava. Bulut güneş üretimini, sakin hava rüzgâr türbinini düşürür; yağmur açık ekinleri sular.");
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
        var bg = EcoUI.Panel("Region banner", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), EcoUI.Surface, false);
        banner = bg.rectTransform;
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>(); bannerGroup.alpha = 0f; bannerGroup.interactable = false; bannerGroup.blocksRaycasts = false;
        bannerStrip = EcoUI.Fill("Strip", banner, EcoUI.Accent);
        bannerIcon = EcoUI.Icon("Icon", banner, "region_city", new Vector2(0, 1), new Vector2(0, 1), 44f);
        bannerTitle = EcoUI.Label("Title", banner, "", 21, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        bannerState = EcoUI.Label("State", banner, "", 13, TextAnchor.MiddleRight, EcoUI.Accent, true);
        bannerSub = EcoUI.Label("Tagline", banner, "", 13, TextAnchor.UpperLeft, EcoUI.Muted);
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
        bannerState.color = EcoUI.GoodColor(health / 100f);
        bannerUntil = Time.unscaledTime + 5f;
    }

    private void UpdateBanner()
    {
        float left = bannerUntil - Time.unscaledTime;
        bannerGroup.alpha = EcoUI.Mode == EcoUI.HudMode.Hidden || left <= 0f ? 0f : Mathf.Clamp01(Mathf.Min(left, 5f - left) * 2.5f);
    }

    private void BuildIntro()
    {
        var bg = EcoUI.Panel("Intro", canvas.transform, Vector2.zero, Vector2.zero, new Color(0.02f, 0.05f, 0.06f, 0.9f), false);
        bg.sprite = null; bg.type = Image.Type.Simple;
        intro = bg.rectTransform; EcoUI.Stretch(intro);
        introGroup = intro.gameObject.AddComponent<CanvasGroup>(); introGroup.interactable = false; introGroup.blocksRaycasts = false;
        var column = EcoUI.Rect("Column", intro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f)); column.sizeDelta = new Vector2(820, 320);
        var logo = EcoUI.Icon("Logo", column, "yep", new Vector2(0.5f, 1), new Vector2(0.5f, 1), 72f); logo.rectTransform.anchoredPosition = Vector2.zero;
        var title = EcoUI.Label("Title", column, introTitle, 54, TextAnchor.MiddleCenter, EcoUI.Accent, true);
        EcoUI.Layout(title, new Vector2(10f, -78f), new Vector2(800f, 66f));
        var line = EcoUI.Label("Line", column, introLine, 20, TextAnchor.MiddleCenter, EcoUI.Ink);
        EcoUI.Layout(line, new Vector2(10f, -150f), new Vector2(800f, 56f));
        var hints = new[] { ("ui_map", EcoKeys.Tag(EcoKeys.Action.Map) + " Harita"), ("tool_vacuum", "[1–5] Araçlar"), ("ui_build", EcoKeys.Tag(EcoKeys.Action.Build) + " İnşa"), ("tool_hand", EcoKeys.Tag(EcoKeys.Action.Interact) + " Etkileşim"), ("ui_help", "[F1] Yardım") };
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
        actions = EcoUI.Rect("Quick actions", canvas.transform, Vector2.zero, Vector2.zero);
        AddAction("map", "ui_map", EcoKeys.Label(EcoKeys.Action.Map), "Dünya haritası", () => SetMap(!MapOpen));
        AddAction("journal", "ui_journal", EcoKeys.Label(EcoKeys.Action.Journal), "Proje günlüğü", () => { var p = EcoProjects.Current; if (p != null) p.SetOpen(!p.PanelOpen); });
        AddAction("build", "ui_build", EcoKeys.Label(EcoKeys.Action.Build), "İnşa modu", () => { if (weapons != null && weapons.Builder != null) weapons.Builder.SetBuildMode(!weapons.Builder.IsBuilding); });
        AddAction("skills", "ui_skills", EcoKeys.Label(EcoKeys.Action.Skills), "Uzmanlık", () => { var s = FindFirstObjectByType<EcoSpecialization>(); if (s != null) s.SetOpen(!s.PanelOpen); });
        AddAction("settings", "ui_gear", EcoKeys.Label(EcoKeys.Action.Settings), "Ses ayarları", () => { var a = FindFirstObjectByType<EcoAudio>(); if (a != null) a.TogglePanel(); });
        AddAction("help", "ui_help", "F1", "Kontroller ve yardım", () => SetHelp(!helpOpen));
        AddAction("hud", "ui_hud", "H", "Arayüz: Tam / Sade / Gizli", CycleHud);
        var bg = EcoUI.Panel("Mode toast", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false);
        modeToastBg = bg; modeToast = EcoUI.Label("Text", bg.transform, "", 18, TextAnchor.MiddleCenter, EcoUI.Ink, true);
        bg.gameObject.SetActive(false);
    }

    private void AddAction(string id, string icon, string key, string tip, System.Action onClick)
    {
        var button = EcoUI.IconButton("Action " + id, actions, icon, 42f, onClick, tip + "  [" + key + "]");
        var badge = EcoUI.Panel("Key", button.transform, new Vector2(1, 0), new Vector2(1, 0), new Color(0f, 0f, 0f, 0.75f), false, EcoUI.Rounded);
        EcoUI.Place(badge.rectTransform, new Vector2(6f, -4f), new Vector2(key.Length > 2 ? 30f : 20f, 16f));
        var text = EcoUI.Label("Key text", badge.transform, key, 10, TextAnchor.MiddleCenter, EcoUI.Ink, true);
        EcoUI.Layout(text, Vector2.zero, badge.rectTransform.sizeDelta);
        actionButtons.Add((button, text, id));
        if (id == "skills")
        {
            skillsAlert = EcoUI.Panel("Alert", button.transform, new Vector2(1, 1), new Vector2(0.5f, 0.5f), EcoUI.Amber, false, EcoUI.Circle);
            EcoUI.Place(skillsAlert.rectTransform, new Vector2(-4f, -4f), new Vector2(16f, 16f));
            var mark = EcoUI.Label("!", skillsAlert.transform, "!", 11, TextAnchor.MiddleCenter, new Color(0.03f, 0.07f, 0.08f), true);
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
        var card = EcoUI.Panel("Help", overlay.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.03f, 0.07f, 0.08f, 0.97f));
        help = card.rectTransform; help.sizeDelta = new Vector2(780f, 540f);
        var icon = EcoUI.Icon("Icon", help, "ui_help", new Vector2(0, 1), new Vector2(0, 1), 34f); EcoUI.LayoutIcon(icon, new Vector2(22f, -18f), 34f);
        var title = EcoUI.Label("Title", help, "KONTROLLER", 22, TextAnchor.MiddleLeft, EcoUI.Accent, true); EcoUI.Layout(title, new Vector2(66f, -22f), new Vector2(400f, 28f));
        var sub = EcoUI.Label("Sub", help, "Tuşlar Ayarlar > Tuş atamaları'ndan değiştirilebilir. Paneller açıkken dünya %10 hızda akar.", 13, TextAnchor.MiddleLeft, EcoUI.Muted);
        EcoUI.Layout(sub, new Vector2(66f, -48f), new Vector2(640f, 18f));
        var close = EcoUI.IconButton("Close", help, "ui_close", 30f, () => SetHelp(false), "Kapat [F1] / [Esc]");
        var cr = (RectTransform)close.transform; cr.anchorMin = cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(1, 1); cr.anchoredPosition = new Vector2(-14f, -14f);
        var rows = new (string icon, string key, string text)[]
        {
            ("player", "WASD", "Kameraya göre yürü (" + EcoKeys.Label(EcoKeys.Action.Sprint) + " koş)"), ("poi_quest", "Sağ tık", "Tıklanan yere yürü"),
            ("tool_vacuum", "Sol tık", "Seçili aracı kullan"), ("ui_layers", "Tekerlek", "Kamerayı yaklaştır / uzaklaştır"),
            ("tool_seed", "1 – 5", "Araç seç: vakum, tohum, geri dönüşüm, tarayıcı, eller"), ("w_wind", EcoKeys.Label(EcoKeys.Action.Mode), "Araç modu (toplama · anti-vakum · su / tohum · demir)"),
            ("seed", EcoKeys.Label(EcoKeys.Action.Rotate), "Tohum türü · inşada döndür · gübrele"), ("tool_hand", EcoKeys.Label(EcoKeys.Action.Interact), "Etkileşim: konuş, hasat, filtre, balık"),
            ("ui_build", EcoKeys.Label(EcoKeys.Action.Build), "İnşa modu (kablo, boru, tesisler)"), ("ui_map", EcoKeys.Label(EcoKeys.Action.Map), "Dünya haritası ve bölge bilgisi"),
            ("ui_journal", EcoKeys.Label(EcoKeys.Action.Journal), "Proje günlüğü ve ipuçları"), ("ui_skills", EcoKeys.Label(EcoKeys.Action.Skills), "Uzmanlık hakları"),
            ("ui_orders", EcoKeys.Label(EcoKeys.Action.Orders), "Siparişler (pazarın yanında)"), ("poi_finale", EcoKeys.Label(EcoKeys.Action.Finale), "Final terminali (buzul istasyonu)"),
            ("ui_gear", EcoKeys.Label(EcoKeys.Action.Settings), "Ses ayarları"), ("calendar", "F5 / F9", "Hızlı kaydet / yükle"),
            ("ui_hud", "H", "Arayüz: Tam · Sade · Gizli"), ("ui_pause", "Esc", "Paneli kapat / duraklat"),
        };
        for (int i = 0; i < rows.Length; i++)
        {
            float x = i % 2 == 0 ? 28f : 400f, y = -84f - (i / 2) * 48f;
            var rowIcon = EcoUI.Icon("Row icon", help, rows[i].icon, new Vector2(0, 1), new Vector2(0, 1), 30f); EcoUI.LayoutIcon(rowIcon, new Vector2(x, y), 30f);
            var keyBg = EcoUI.Panel("Key", help, new Vector2(0, 1), new Vector2(0, 1), EcoUI.SurfaceRaised, false);
            EcoUI.Place(keyBg.rectTransform, new Vector2(x + 38f, y - 3f), new Vector2(76f, 24f));
            var key = EcoUI.Label("Key", keyBg.transform, rows[i].key, 13, TextAnchor.MiddleCenter, EcoUI.Amber, true); EcoUI.Layout(key, Vector2.zero, new Vector2(76f, 24f));
            var text = EcoUI.Label("Text", help, rows[i].text, 13, TextAnchor.MiddleLeft, EcoUI.Ink); EcoUI.Layout(text, new Vector2(x + 122f, y + 5f), new Vector2(240f, 40f));
        }
        help.gameObject.SetActive(false);
    }

    private void SetHelp(bool open)
    {
        if (open && MapOpen) SetMap(false);
        helpOpen = open; help.gameObject.SetActive(open);
        EcoUIFlow.SetPanel(helpOwner, open);
    }

    // ================================================================== big map

    private void BuildBigMap()
    {
        bigRoot = EcoUI.Rect("Big map", overlay.transform, Vector2.zero, Vector2.zero); EcoUI.Stretch(bigRoot);
        bigDim = EcoUI.Panel("Dim", bigRoot, Vector2.zero, Vector2.zero, new Color(0f, 0.02f, 0.03f, 0.78f));
        bigDim.sprite = null; bigDim.type = Image.Type.Simple; EcoUI.Stretch(bigDim.rectTransform);
        var card = EcoUI.Panel("Card", bigRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(0.03f, 0.07f, 0.08f, 0.98f));
        bigCard = card.rectTransform;
        var titleIcon = EcoUI.Icon("Icon", bigCard, "ui_map", new Vector2(0, 1), new Vector2(0, 1), 30f); EcoUI.LayoutIcon(titleIcon, new Vector2(18f, -14f), 30f);
        bigTitle = EcoUI.Label("Title", bigCard, "DÜNYA HARİTASI", 19, TextAnchor.MiddleLeft, EcoUI.Accent, true);
        bigHint = EcoUI.Label("Hint", bigCard, "", 12, TextAnchor.MiddleRight, EcoUI.Muted);
        var close = EcoUI.IconButton("Close", bigCard, "ui_close", 30f, () => SetMap(false), "Kapat " + EcoKeys.Tag(EcoKeys.Action.Map) + " / [Esc]");
        var cr = (RectTransform)close.transform; cr.anchorMin = cr.anchorMax = new Vector2(1, 1); cr.pivot = new Vector2(1, 1); cr.anchoredPosition = new Vector2(-12f, -12f);

        var frame = EcoUI.Panel("Frame", bigCard, new Vector2(0, 1), new Vector2(0, 1), new Color(0.02f, 0.04f, 0.05f, 1f));
        bigFrame = frame.rectTransform;
        bigFrame.gameObject.AddComponent<RectMask2D>();
        var go = new GameObject("Map image", typeof(RectTransform), typeof(RawImage)); go.layer = 5;
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(bigFrame, false); EcoUI.Stretch(rt, 3f);
        bigImage = go.GetComponent<RawImage>(); bigImage.texture = minimapTexture; bigImage.raycastTarget = false;
        bigView = EcoUI.Rect("Overlay", bigFrame, new Vector2(0, 1), new Vector2(0, 1)); // region tints, outlines and icons live here
        hintPin = EcoUI.Icon("Hint", bigFrame, "poi_quest", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 34f).rectTransform;
        bigArrow = EcoUI.Icon("Player", bigFrame, "player", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 30f).rectTransform;
        coordText = EcoUI.Label("Coordinates", bigCard, "", 12, TextAnchor.MiddleLeft, EcoUI.Muted);

        legend = EcoUI.Rect("Legend", bigCard, new Vector2(0, 1), new Vector2(0, 1));
        var items = new[] { ("player", "Sen"), ("poi_quest", "Görev"), ("poi_market", "Pazar"), ("poi_home", "Ev"), ("poi_factory", "Fabrika"), ("poi_station", "İstasyon"),
            ("w_wind", "Rüzgâr"), ("poi_fire", "Yangın"), ("poi_enemy", "Düşman") };
        float lx = 0f;
        foreach (var (icon, text) in items)
        {
            var img = EcoUI.Icon("Legend icon", legend, icon, new Vector2(0, 1), new Vector2(0, 1), 18f); EcoUI.LayoutIcon(img, new Vector2(lx, -2f), 18f);
            var label = EcoUI.Label("Legend text", legend, text, 12, TextAnchor.MiddleLeft, EcoUI.Muted); EcoUI.Layout(label, new Vector2(lx + 21f, -2f), new Vector2(70f, 18f));
            lx += 26f + text.Length * 7.5f + 10f;
        }

        BuildInfoCard();
        bigRoot.gameObject.SetActive(false);
    }

    private void BuildInfoCard()
    {
        var bg = EcoUI.Panel("Info", bigCard, new Vector2(1, 1), new Vector2(1, 1), new Color(0.05f, 0.1f, 0.115f, 1f));
        infoCard = bg.rectTransform;
        infoIcon = EcoUI.Icon("Region icon", infoCard, "region_city", new Vector2(0, 1), new Vector2(0, 1), 50f);
        infoName = EcoUI.Label("Name", infoCard, "", 20, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        infoBadge = EcoUI.Panel("Badge", infoCard, new Vector2(0, 1), new Vector2(0, 1), EcoUI.Accent, false, EcoUI.Rounded);
        infoBadgeText = EcoUI.Label("Badge text", infoBadge.transform, "", 11, TextAnchor.MiddleCenter, new Color(0.03f, 0.07f, 0.08f), true);
        infoTag = EcoUI.Label("Tagline", infoCard, "", 13, TextAnchor.UpperLeft, EcoUI.Muted);
        infoEmpty = EcoUI.Label("Empty", infoCard, "Bir bölgenin üzerine gel.\nTıklayınca bilgi kartı sabitlenir.", 15, TextAnchor.MiddleCenter, EcoUI.Muted);
        for (int i = 0; i < 6; i++)
        {
            infoRowIcon[i] = EcoUI.Icon("Row icon", infoCard, RowIcons[i], new Vector2(0, 1), new Vector2(0, 1), 18f);
            infoRowName[i] = EcoUI.Label("Row name", infoCard, RowNames[i], 13, TextAnchor.MiddleLeft, EcoUI.Muted);
            infoRowValue[i] = EcoUI.Label("Row value", infoCard, "", 13, TextAnchor.MiddleRight, EcoUI.Ink, true);
            infoRowFill[i] = EcoUI.Meter("Row meter", infoCard, out infoRowTrack[i]);
        }
        climateHeader = EcoUI.Label("Climate", infoCard, "İKLİM ŞU AN", 11, TextAnchor.MiddleLeft, EcoUI.Accent, true);
        for (int i = 0; i < 4; i++)
        {
            climateIcon[i] = EcoUI.Icon("Climate icon", infoCard, "w_sun", new Vector2(0, 1), new Vector2(0, 1), 26f);
            climateText[i] = EcoUI.Label("Climate text", infoCard, "", 12, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        }
        tasksHeader = EcoUI.Label("Tasks", infoCard, "GÖREVLER VE DURUM", 11, TextAnchor.MiddleLeft, EcoUI.Accent, true);
        for (int i = 0; i < taskIcon.Length; i++)
        {
            taskIcon[i] = EcoUI.Icon("Task icon", infoCard, "poi_project", new Vector2(0, 1), new Vector2(0, 1), 22f);
            taskText[i] = EcoUI.Label("Task text", infoCard, "", 13, TextAnchor.UpperLeft, EcoUI.Ink);
            taskFill[i] = EcoUI.Meter("Task meter", infoCard, out taskTrack[i]);
        }
        featuresHeader = EcoUI.Label("Features", infoCard, "FIRSATLAR", 11, TextAnchor.MiddleLeft, EcoUI.Accent, true);
        for (int i = 0; i < featureIcon.Length; i++)
        {
            featureIcon[i] = EcoUI.Icon("Feature icon", infoCard, "ui_check", new Vector2(0, 1), new Vector2(0, 1), 16f);
            featureText[i] = EcoUI.Label("Feature text", infoCard, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink);
        }
    }

    public void SetMap(bool open)
    {
        if (open && helpOpen) SetHelp(false);
        MapOpen = open;
        bigRoot.gameObject.SetActive(open);
        EcoUIFlow.SetPanel(this, open);
        if (open)
        {
            RebuildOverlays();
            zoom = 1f; viewCenter = new Vector2(0.5f, 0.5f); pinned = null;
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
                var chip = EcoUI.Panel("Chip", bigView, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Color(0.03f, 0.07f, 0.08f, 0.82f), false);
                o.chip = chip.rectTransform;
                o.chipIcon = EcoUI.Icon("Icon", o.chip, EcoIcons.Region(info.kind, region.waterPollution > 35f), new Vector2(0, 1), new Vector2(0, 1), 24f);
                o.chipText = EcoUI.Label("Name", o.chip, region.regionName, 12, TextAnchor.MiddleLeft, EcoUI.Ink, true);
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
            if (mouse.leftButton.wasReleasedThisFrame)
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
            o.fill.color = new Color(c.r, c.g, c.b, focusOn ? 0.26f : 0.05f);
            float t = focusOn ? 3f : 1.5f;
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
                o.chip.GetComponent<Image>().color = focusOn ? new Color(0.08f, 0.2f, 0.18f, 0.95f) : new Color(0.03f, 0.07f, 0.08f, 0.82f);
            }
        }

        // Points of interest.
        if (Time.unscaledTime >= mapRefreshAt) { mapRefreshAt = Time.unscaledTime + 0.25f; CollectPois(); RefreshInfoCard(focus); }
        while (poiImages.Count < pois.Count) poiImages.Add(EcoUI.Icon("Poi", bigView, "poi_quest", new Vector2(0, 1), new Vector2(0.5f, 0.5f), 26f));
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
            if (fire.Burning) pois.Add(new Poi { position = fire.transform.position, icon = "poi_fire", title = "Yangın", detail = "Vakumun su moduyla söndür.", pulse = true });
        foreach (var enemy in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None))
            if (enemy != null && enemy.Health > 0f && enemy.State != EnemyBrain.Behaviour.Dead)
                pois.Add(new Poi { position = enemy.transform.position, icon = "poi_enemy", title = "Kirlilik yaratığı", detail = "Türüne uygun araçla müdahale et.", size = 20f });
    }

    // ------------------------------------------------------------------ info card

    private void RefreshInfoCard(EcoRegion r)
    {
        float w = infoCard.sizeDelta.x;
        bool has = r != null;
        infoEmpty.gameObject.SetActive(!has);
        EcoUI.Layout(infoEmpty, new Vector2(16f, -infoCard.sizeDelta.y * 0.5f + 30f), new Vector2(w - 32f, 60f));
        foreach (Transform child in infoCard) if (child != infoEmpty.transform) child.gameObject.SetActive(has);
        if (!has) return;
        var info = r.GetComponent<EcoRegionInfo>();
        var clock = EcoWorldClock.Current;
        var values = new float[7]; Sample(r, values);
        float health = values[5];

        float y = -16f;
        EcoUI.SetIcon(infoIcon, info != null ? EcoIcons.Region(info.kind, r.waterPollution > 35f) : "region_city");
        EcoUI.LayoutIcon(infoIcon, new Vector2(16f, y), 50f);
        infoName.text = r.regionName;
        EcoUI.Layout(infoName, new Vector2(76f, y - 2f), new Vector2(w - 92f, 26f));
        infoBadge.color = EcoUI.GoodColor(health / 100f);
        infoBadgeText.text = StateName(health) + "  ·  " + Mathf.RoundToInt(health);
        EcoUI.Place(infoBadge.rectTransform, new Vector2(76f, y - 30f), new Vector2(118f, 20f));
        EcoUI.Layout(infoBadgeText, Vector2.zero, new Vector2(118f, 20f));
        infoTag.text = info != null ? info.tagline : "";
        y -= 58f;
        EcoUI.Layout(infoTag, new Vector2(16f, y), new Vector2(w - 32f, 34f));
        y -= 40f;
        for (int i = 0; i < 6; i++)
        {
            float v = values[i];
            float good = i == 3 ? 1f - v / 100f : v / 100f;
            EcoUI.LayoutIcon(infoRowIcon[i], new Vector2(16f, y), 18f);
            EcoUI.Layout(infoRowName[i], new Vector2(40f, y), new Vector2(130f, 18f));
            infoRowValue[i].text = Mathf.RoundToInt(v).ToString();
            infoRowValue[i].color = EcoUI.GoodColor(good);
            EcoUI.Layout(infoRowValue[i], new Vector2(w - 56f, y), new Vector2(40f, 18f));
            EcoUI.Place(infoRowTrack[i], new Vector2(170f, y - 7f), new Vector2(w - 232f, 5f));
            EcoUI.SetMeter(infoRowFill[i], infoRowTrack[i], v / 100f, EcoUI.GoodColor(good));
            y -= 22f;
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
        float cw = (w - 32f) / 4f;
        for (int i = 0; i < 4; i++)
        {
            EcoUI.SetIcon(climateIcon[i], chips[i].icon);
            EcoUI.LayoutIcon(climateIcon[i], new Vector2(16f + i * cw, y), 26f);
            climateText[i].text = chips[i].text;
            EcoUI.Layout(climateText[i], new Vector2(16f + i * cw + 30f, y + 2f), new Vector2(cw - 32f, 32f));
        }
        y -= 40f;

        // Live tasks for this region.
        var tasks = new List<(string icon, string text, float progress)>();
        var projects = EcoProjects.Current;
        if (info != null && info.projectIndex >= 0 && projects != null && info.projectIndex < projects.All.Count)
        {
            var p = projects.All[info.projectIndex];
            tasks.Add((p.done ? "ui_check" : "poi_project", "<b>" + p.name + "</b>  " + (p.done ? "<color=#6EE8A8>tamamlandı</color>" : "") + "\n" + (p.done ? "Başarı korunuyor" : p.status),
                p.done ? 1f : p.held / EcoProjects.VerifySeconds));
        }
        int trash = 0, fires = 0, enemies = 0, thirsty = 0, factories = 0, filtered = 0;
        foreach (var t in FindObjectsByType<RecyclableResource>(FindObjectsSortMode.None)) if (r.Contains(t.transform.position)) trash++;
        foreach (var f in FindObjectsByType<EcoFire>(FindObjectsSortMode.None)) if (f.Burning && r.Contains(f.transform.position)) fires++;
        foreach (var e in FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None)) if (e != null && e.Health > 0f && e.State != EnemyBrain.Behaviour.Dead && r.Contains(e.transform.position)) enemies++;
        foreach (var c in FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None)) if (c.NeedsWater && r.Contains(c.transform.position)) thirsty++;
        foreach (var f in FindObjectsByType<EcoFactory>(FindObjectsSortMode.None)) if (r.Contains(f.transform.position)) { factories++; if (f.Filtered) filtered++; }
        if (fires > 0) tasks.Add(("poi_fire", fires + " aktif yangın · vakumun su moduyla söndür", -1f));
        if (enemies > 0) tasks.Add(("poi_enemy", enemies + " kirlilik yaratığı bölgede", -1f));
        if (factories > 0) tasks.Add(("poi_factory", "Fabrika filtresi " + filtered + " / " + factories, filtered / (float)factories));
        foreach (var inlet in FindObjectsByType<EcoPollutionInlet>(FindObjectsSortMode.None))
            if (r.Contains(inlet.transform.position)) tasks.Add((inlet.Remediated ? "ui_check" : "poi_inlet", inlet.Remediated ? "Kirletici giriş kapatıldı" : "Kirletici giriş açık · [E] ile kapat", -1f));
        if (thirsty > 0) tasks.Add(("seed", thirsty + " fidan sulanmayı bekliyor", -1f));
        if (trash > 0) tasks.Add(("scrap", trash + " atık toplanabilir", -1f));
        foreach (var spot in FindObjectsByType<EcoFishingSpot>(FindObjectsSortMode.None))
            if (r.Contains(spot.transform.position)) { tasks.Add((r.waterPollution < 35f ? "fish" : "fish_dirty", "Balık nüfusu " + Mathf.FloorToInt(spot.population) + " / 12" + (spot.population <= 4f ? " · av kapalı" : ""), spot.population / 12f)); break; }
        var station = EcoGlacierStation.Current;
        if (station != null && r.Contains(station.transform.position))
            tasks.Add(("poi_station", station.Active ? "İstasyon etkin · karbon " + station.AverageCarbon.ToString("0.0") + " / 20" : "İzleme istasyonu kapalı · [E] ile etkinleştir", -1f));
        if (tasks.Count == 0) tasks.Add(("ui_check", "Acil iş yok. Bölgeyi korumaya devam et.", -1f));

        EcoUI.Layout(tasksHeader, new Vector2(16f, y), new Vector2(w - 32f, 16f));
        y -= 22f;
        for (int i = 0; i < taskIcon.Length; i++)
        {
            bool used = i < tasks.Count;
            taskIcon[i].gameObject.SetActive(used); taskText[i].gameObject.SetActive(used); taskTrack[i].gameObject.SetActive(used && tasks[i].progress >= 0f);
            if (!used) continue;
            EcoUI.SetIcon(taskIcon[i], tasks[i].icon);
            EcoUI.LayoutIcon(taskIcon[i], new Vector2(16f, y), 22f);
            taskText[i].text = tasks[i].text;
            bool twoLines = tasks[i].text.Contains("\n") || tasks[i].text.Length > 44;
            float h = twoLines ? 34f : 18f;
            EcoUI.Layout(taskText[i], new Vector2(46f, y - 1f), new Vector2(w - 62f, h));
            y -= h + 4f;
            if (tasks[i].progress >= 0f)
            {
                EcoUI.Place(taskTrack[i], new Vector2(46f, y), new Vector2(w - 62f, 5f));
                EcoUI.SetMeter(taskFill[i], taskTrack[i], tasks[i].progress, tasks[i].progress >= 1f ? EcoUI.Accent : EcoUI.Amber);
                y -= 10f;
            }
            y -= 4f;
        }
        var features = info != null ? info.features : new string[0];
        featuresHeader.gameObject.SetActive(features.Length > 0);
        EcoUI.Layout(featuresHeader, new Vector2(16f, y - 2f), new Vector2(w - 32f, 16f));
        y -= 22f;
        for (int i = 0; i < featureIcon.Length; i++)
        {
            bool used = i < features.Length;
            featureIcon[i].gameObject.SetActive(used); featureText[i].gameObject.SetActive(used);
            if (!used) continue;
            featureText[i].text = features[i];
            EcoUI.LayoutIcon(featureIcon[i], new Vector2(18f, y - 1f), 16f);
            EcoUI.Layout(featureText[i], new Vector2(42f, y), new Vector2(w - 58f, 18f));
            y -= 22f;
        }
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
        float leftWidth = compact ? 270f : 300f, rightWidth = compact ? 250f : 276f;

        // Environment card under the vitals card.
        laidOutVitalsBottom = EcoQuestHUD.VitalsBottom;
        envCard.SetVisible(!hidden);
        float envBody = sade ? 0f : 7 * 29f + 8f;
        envCard.Layout(new Vector2(margin, -(EcoQuestHUD.VitalsBottom + 10f)), leftWidth, envBody, sade);
        if (sade) envCard.body.gameObject.SetActive(false);
        for (int i = 0; i < 7; i++)
        {
            float y = -6f - i * 29f;
            EcoUI.LayoutIcon(rowIcons[i], new Vector2(12f, y), 20f);
            EcoUI.Layout(rowNames[i], new Vector2(38f, y), new Vector2(150f, 18f));
            EcoUI.Layout(rowValues[i], new Vector2(leftWidth - 150f, y), new Vector2(138f, 18f));
            EcoUI.Place(rowTracks[i], new Vector2(38f, y - 20f), new Vector2(leftWidth - 50f, 4f));
        }

        // Map card, top right.
        mapCard.SetVisible(!hidden);
        float mapSide = rightWidth - 20f;
        float mh = mapCard.Layout(new Vector2(-margin, -margin), rightWidth, mapSide + 40f, mapCard.IsCollapsed);
        EcoUI.Place(miniFrame, new Vector2(10f, -4f), new Vector2(mapSide, mapSide));
        float sy = -(mapSide + 10f);
        EcoUI.LayoutIcon(clockIcon, new Vector2(10f, sy), 20f);
        EcoUI.Layout(clockText, new Vector2(34f, sy), new Vector2(90f, 20f));
        EcoUI.LayoutIcon(weatherIcon, new Vector2(122f, sy - 1f), 22f);
        EcoUI.Layout(weatherText, new Vector2(146f, sy), new Vector2(70f, 20f));
        EcoUI.LayoutIcon(windIcon, new Vector2(rightWidth - 74f, sy - 1f), 22f);
        EcoUI.Layout(windText, new Vector2(rightWidth - 52f, sy), new Vector2(40f, 20f));
        EcoUI.Layout(miniNorth, new Vector2(mapSide * 0.5f - 10f, -5f), new Vector2(20f, 16f));
        var bb = (RectTransform)bigMapButton.transform; bb.anchorMin = bb.anchorMax = new Vector2(0, 1); bb.pivot = new Vector2(1, 1);
        bb.anchoredPosition = new Vector2(10f + mapSide - 6f, -10f);
        MapBottom = hidden ? margin : margin + mh;

        // Banner under the objective card.
        float bannerWidth = Mathf.Clamp(width - leftWidth - rightWidth - 4f * margin, 300f, 470f);
        float center = (leftWidth - rightWidth) * 0.5f;
        EcoUI.Place(banner, new Vector2(center, -(margin + 96f)), new Vector2(bannerWidth, 70f));
        EcoUI.Place(bannerStrip.rectTransform, Vector2.zero, new Vector2(5f, 70f));
        EcoUI.LayoutIcon(bannerIcon, new Vector2(14f, -13f), 44f);
        EcoUI.Layout(bannerTitle, new Vector2(68f, -9f), new Vector2(bannerWidth - 190f, 26f));
        EcoUI.Layout(bannerState, new Vector2(bannerWidth - 124f, -12f), new Vector2(110f, 18f));
        EcoUI.Layout(bannerSub, new Vector2(68f, -38f), new Vector2(bannerWidth - 82f, 30f));

        // Quick actions: a row at the bottom-left beside the tool belt (a column when the screen is too narrow);
        // in Gizli only the HUD button stays so the player can bring the interface back.
        int count = actionButtons.Count(a => !hidden || a.id == "hud");
        float dockHalf = Mathf.Min(600f, width - 2f * margin) * 0.5f;
        bool row = width * 0.5f - dockHalf - margin - 12f >= count * 48f;
        float bx = 0f;
        foreach (var (button, _, id) in actionButtons)
        {
            bool show = !hidden || id == "hud";
            button.gameObject.SetActive(show);
            if (!show) continue;
            var rt = (RectTransform)button.transform; rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0);
            rt.anchoredPosition = row ? new Vector2(bx, 0f) : new Vector2(0f, bx); bx += 48f;
        }
        EcoUI.Place(actions, new Vector2(margin, margin + 4f), row ? new Vector2(bx, 42f) : new Vector2(42f, bx));

        // Mode toast.
        EcoUI.Place(modeToastBg.rectTransform, new Vector2(0f, 140f), new Vector2(380f, 46f));
        EcoUI.Layout(modeToast, new Vector2(10f, -12f), new Vector2(360f, 24f));

        // Help sheet.
        help.localScale = Vector3.one * Mathf.Min(1f, (width - 40f) / 780f, (height - 40f) / 540f);

        // Big map: map square on the left, information card on the right.
        float cardW = Mathf.Min(1180f, width - 40f), cardH = Mathf.Min(760f, height - 40f);
        EcoUI.Place(bigCard, Vector2.zero, new Vector2(cardW, cardH));
        float infoW = Mathf.Clamp(cardW * 0.32f, 300f, 380f);
        float side = Mathf.Min(cardH - 96f, cardW - infoW - 52f);
        EcoUI.Layout(bigTitle, new Vector2(56f, -18f), new Vector2(300f, 24f));
        bigHint.text = "Tekerlek: yakınlaştır  ·  Sürükle: kaydır  ·  Tıkla: bölgeyi sabitle  ·  Sağ tık: bırak  ·  " + EcoKeys.Tag(EcoKeys.Action.Map) + " / [Esc] kapat";
        EcoUI.Layout(bigHint, new Vector2(cardW - 620f - 56f, -22f), new Vector2(620f, 18f));
        EcoUI.Place(bigFrame, new Vector2(18f, -56f), new Vector2(side, side));
        EcoUI.Place(bigView, Vector2.zero, new Vector2(side, side));
        EcoUI.Layout(coordText, new Vector2(18f, -(56f + side + 6f)), new Vector2(side, 16f));
        EcoUI.Place(legend, new Vector2(18f, -(56f + side + 22f)), new Vector2(side, 20f));
        EcoUI.Place(infoCard, new Vector2(-16f, -56f), new Vector2(cardW - side - 52f, side + 38f));
    }
}
