using UnityEngine;
using UnityEngine.UI;

// Player HUD: portrait and meters, a small objective tracker and an icon toolbelt.
// Detailed resource counts remain in a collapsible foldout; [H] cycles Tam → Sade → Gizli.
[DefaultExecutionOrder(40)]
public sealed class EcoQuestHUD : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    [Header("Optional imported UI artwork")]
    public Sprite panelSprite;
    public Sprite slotSprite;
    public Sprite[] toolIcons = new Sprite[PlayerWeaponSystem.ToolCount];

    /// <summary>Bottom edge (canvas units from the top) of the vitals card; EcoWorldHUD places the environment card below.</summary>
    public static float VitalsBottom { get; private set; } = 110f;
    /// <summary>Height of the tool belt + mode line block at the bottom of the screen.</summary>
    public static float DockTop { get; private set; } = 146f;
    public static float DockWidth { get; private set; } = 414f;
    /// <summary>Bottom of the objective card in screen pixels from the top (IMGUI overlays stack below it).</summary>
    public static float ObjectiveBottomPixels { get; private set; } = 90f;
    public const float LeftColumnWidth = 284f;
    private float uiScale = 1f;

    private const int BuilderSlotCount = 5;
    private static readonly string[] ToolNames = { "VAKUM", "TOHUM", "GERİ DÖNÜŞÜM", "TARAYICI", "ELLER", "FENER" };
    private static readonly string[] ToolIcons = { "tool_vacuum", "tool_seed", "tool_recycle", "tool_scanner", "tool_hand", "tool_flashlight" };
    private static readonly string[] ToolTips =
    {
        "Vakum [1]: atık toplar, anti-vakumla duman/asit dağıtır, su moduyla söndürür ve sular. [Q] mod.",
        "Tohum aracı [2]: toprağa tohum eker; demir top modunda Teneke'yi sersemletir. [Q] mod, [R] tohum türü.",
        "Geri dönüşüm [3]: atığı yerinde işler, [R] ile gübre uygular.",
        "Tarayıcı [4]: neden, kalite ve bağlantı bilgisini gösterir; hasar vermez.",
        "Eller [5]: etkileşim, hasat ve balıkçılık.",
        "Fener [6]: seçiliyken sol tıkla ışığı aç veya kapat.",
    };
    private static readonly Color EnergyColor = new Color(0.34f, 0.86f, 0.59f), ElectricColor = new Color(1f, 0.79f, 0.31f), WaterColor = new Color(0.36f, 0.77f, 1f);

    private PlayerVitals vitals;
    private EcoWorldClock worldClock;
    private Canvas canvas;
    private CanvasScaler scaler;
    private EcoUI.Card vitalsCard, resourcesCard, objectiveCard;
    // Vitals
    private readonly Image[] meterIcons = new Image[3], meterFills = new Image[3];
    private readonly RectTransform[] meterTracks = new RectTransform[3];
    private readonly RectTransform[] meterHits = new RectTransform[3];
    private readonly Text[] meterNames = new Text[3], meterValues = new Text[3];
    private RectTransform portraitRoot;
    private Image portrait, portraitFrame;
    private Image weatherIcon, windIcon, clockIcon;
    private Text weatherText, windText;
    // Resources
    private static readonly (string icon, string name, string tip)[] Chips =
    {
        ("metal", "Metal", "Metal: inşa, tamir ve ekipman."), ("plastic", "Plastik", "Plastik: inşa, tohum ve filtre."),
        ("food", "Ürün", "Temiz ürün: pazarda takas ve siparişler."), ("compost", "Gübre", "Gübre: bitki bakımı, zararlı baskısını azaltır."),
        ("fish", "Temiz balık", "Temiz balık: en değerli takas ürünü."), ("fish_dirty", "Kirli balık", "Kirli balık gıda olmaz; organiğe işlenir."),
        ("filter", "Küçük filtre", "Küçük filtre: sersemlemiş Teneke'ye takılır."), ("ui_build", "Büyük filtre", "Büyük filtre: fabrikaya takılır."),
        ("scrap", "Ham atık", "Toplanmış metal/plastik atık; geri dönüştür."), ("organic", "Organik", "Organik kalıntı: üç birim bir gübre."),
    };
    private readonly Image[] chipIcons = new Image[10];
    private readonly Text[] chipValues = new Text[10], chipNames = new Text[10];
    private readonly GameObject[] chipHits = new GameObject[10];
    private RectTransform resourceQuick;
    private readonly Image[] resourceQuickIcons = new Image[3];
    private readonly Text[] resourceQuickCounts = new Text[3];
    private Image yepIcon, yepFill; private RectTransform yepTrack; private Text yepText;
    // Objective
    private Text objective;
    // Dock
    private RectTransform dock, modePanel, contextPanel, feedbackPanel, recoveryPanel;
    private readonly Image[] slotBg = new Image[PlayerWeaponSystem.ToolCount], slotIcon = new Image[PlayerWeaponSystem.ToolCount], slotMarker = new Image[PlayerWeaponSystem.ToolCount], slotKeyBg = new Image[PlayerWeaponSystem.ToolCount], slotCountBg = new Image[PlayerWeaponSystem.ToolCount];
    private readonly RectTransform[] slotRects = new RectTransform[PlayerWeaponSystem.ToolCount];
    private readonly Text[] slotTitles = new Text[PlayerWeaponSystem.ToolCount], slotCounts = new Text[PlayerWeaponSystem.ToolCount], slotKeys = new Text[PlayerWeaponSystem.ToolCount];
    private Image modeIcon;
    private Text mode, instructions, context, feedback, recovery;
    private Image contextBg, feedbackBg, recoveryBg;
    private float feedbackTop;
    private float refreshAt, objectiveAt;
    private int lastWidth, lastHeight;
    private float laidOutMapBottom = -1f;
    private bool buildingLayout, previousLegacyHud, dirty = true;
    private EcoUI.HudMode lastMode;

    private void Start()
    {
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        vitals = weapons != null ? weapons.GetComponentInParent<PlayerVitals>() : null;
        if (vitals != null) { previousLegacyHud = vitals.showLegacyHUD; vitals.showLegacyHUD = false; }
        worldClock = EcoWorldClock.Current;
        canvas = EcoUI.CreateCanvas("EcoQuest HUD", 20, out scaler);
        BuildCards();
        BuildDock();
        foreach (var root in new[] { vitalsCard.root, resourcesCard.root, objectiveCard.root, dock, modePanel, contextPanel, feedbackPanel, recoveryPanel })
            EcoUI.RegisterWorldLabelObstacle(root);
        EcoUI.LayoutChanged += MarkDirty;
        lastMode = EcoUI.Mode;
        ApplyLayout();
        if (weapons != null) weapons.Changed += OnInventoryChanged;
        Refresh();
    }

    private void MarkDirty() => dirty = true;

    private void BuildCards()
    {
        // Vitals: character energy, tool electricity, water tank and the day/weather strip.
        vitalsCard = new EcoUI.Card("vitals", canvas.transform, new Vector2(0, 1), new Vector2(0, 1), "energy", "DURUM",
            "Karakter enerjisi, araç elektriği ve su. Kartı daralt / genişlet.");
        var portraitMask = EcoUI.Panel("Portrait mask", vitalsCard.root, new Vector2(0, 1), new Vector2(0, 1), Color.white, false, EcoUI.Circle);
        portraitRoot = portraitMask.rectTransform;
        portraitMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        portrait = EcoUI.Panel("Character portrait", portraitRoot, Vector2.zero, Vector2.zero, Color.white, false, EcoUI.PortraitSprite);
        EcoUI.Stretch(portrait.rectTransform); portrait.type = Image.Type.Simple; portrait.preserveAspect = true;
        portrait.enabled = EcoUI.PortraitSprite != null;
        portraitFrame = EcoUI.Panel("Portrait frame", vitalsCard.root, new Vector2(0, 1), new Vector2(0, 1), Color.white, false, EcoUI.PortraitFrameSprite);
        portraitFrame.type = Image.Type.Simple; portraitFrame.preserveAspect = true;
        portraitFrame.enabled = EcoUI.PortraitFrameSprite != null;
        FloatingText(vitalsCard.summary);
        string[] icons = { "energy", "electricity", "water" };
        string[] names = { "Karakter enerjisi", "Araç elektriği", "Temiz su" };
        string[] tips =
        {
            "Karakter enerjisi: hasar alınca azalır. Sıfırda 5 sn toparlanırsın (ölüm yok).",
            "Araç elektriği (EB): araçlar harcar; şarj istasyonundan dolar.",
            "Araç su tankı (SB): sulama ve söndürme. Kirli su tıkanma yapar.",
        };
        for (int i = 0; i < 3; i++)
        {
            meterIcons[i] = EcoUI.Icon("Meter icon " + i, vitalsCard.body, icons[i], new Vector2(0, 1), new Vector2(0, 1), 22f);
            meterNames[i] = EcoUI.Label("Meter name " + i, vitalsCard.body, names[i], 15, TextAnchor.MiddleLeft, EcoUI.Muted);
            meterValues[i] = EcoUI.Label("Meter value " + i, vitalsCard.body, "", 15, TextAnchor.MiddleRight, EcoUI.Ink, true);
            FloatingText(meterValues[i]);
            meterFills[i] = EcoUI.Meter("Meter " + i, vitalsCard.body, out meterTracks[i]);
            meterTracks[i].GetComponent<Image>().color = new Color(0.045f, 0.065f, 0.035f, 0.84f);
            var hit = EcoUI.Panel("Hit " + i, vitalsCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
            meterHits[i] = hit.rectTransform;
            EcoUI.AddTooltip(hit.gameObject, tips[i]);
        }
        clockIcon = EcoUI.Icon("Clock", vitalsCard.body, "clock", new Vector2(0, 1), new Vector2(0, 1), 18f);
        weatherIcon = EcoUI.Icon("Weather", vitalsCard.body, "w_sun", new Vector2(0, 1), new Vector2(0, 1), 22f);
        weatherText = EcoUI.Label("Weather text", vitalsCard.body, "", 14, TextAnchor.MiddleLeft, EcoUI.Ink);
        windIcon = EcoUI.Icon("Wind", vitalsCard.body, "w_wind", new Vector2(0, 1), new Vector2(0, 1), 22f);
        windText = EcoUI.Label("Wind text", vitalsCard.body, "", 14, TextAnchor.MiddleRight, EcoUI.Ink, true);

        // Resources: icon chips in two columns plus the YEP progress bar.
        resourcesCard = new EcoUI.Card("resources", canvas.transform, new Vector2(1, 1), new Vector2(1, 1), "metal", "KAYNAKLAR",
            "Ortak stok: malzeme, ürün, filtre ve YEP. Kartı daralt / genişlet.");
        resourceQuick = EcoUI.Rect("Resource totals", resourcesCard.root, new Vector2(0, 1), new Vector2(0, 1));
        for (int i = 0; i < 3; i++)
        {
            resourceQuickIcons[i] = EcoUI.Icon("Resource icon " + i, resourceQuick, Chips[i].icon, new Vector2(0, 1), new Vector2(0, 1), 22f);
            resourceQuickCounts[i] = EcoUI.Label("Resource count " + i, resourceQuick, "0", 14, TextAnchor.MiddleLeft, EcoUI.PaperInk, true);
            FloatingText(resourceQuickCounts[i]);
            EcoUI.AddTooltip(resourceQuickIcons[i].gameObject, Chips[i].tip);
            resourceQuickIcons[i].raycastTarget = true;
        }
        for (int i = 0; i < Chips.Length; i++)
        {
            var hit = EcoUI.Panel("Chip " + i, resourcesCard.body, new Vector2(0, 1), new Vector2(0, 1), EcoUI.SurfaceRaised);
            chipHits[i] = hit.gameObject; EcoUI.AddTooltip(hit.gameObject, Chips[i].tip);
            chipIcons[i] = EcoUI.Icon("Icon", hit.transform, Chips[i].icon, new Vector2(0, 1), new Vector2(0, 1), 22f);
            chipValues[i] = EcoUI.Label("Value", hit.transform, "0", 16, TextAnchor.MiddleLeft, EcoUI.Ink, true);
            chipValues[i].verticalOverflow = VerticalWrapMode.Overflow; // a 16 px line is taller than the 17 px slot; Truncate drew nothing
            chipNames[i] = EcoUI.Label("Name", hit.transform, Chips[i].name, 11, TextAnchor.MiddleLeft, EcoUI.Muted);
        }
        yepIcon = EcoUI.Icon("YEP", resourcesCard.body, "yep", new Vector2(0, 1), new Vector2(0, 1), 24f);
        yepText = EcoUI.Label("YEP text", resourcesCard.body, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        yepFill = EcoUI.Meter("YEP meter", resourcesCard.body, out yepTrack);
        var yepHit = EcoUI.Panel("YEP hit", resourcesCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        yepHit.rectTransform.anchoredPosition = new Vector2(0, -170); yepHit.rectTransform.sizeDelta = new Vector2(280, 36);
        EcoUI.AddTooltip(yepHit.gameObject, "YEP: Yenilenebilir Enerji Puanı. Projeler ve bakım anları kalıcı YEP verir; her 40 YEP bir uzmanlık hakkı.");

        // Objective.
        objectiveCard = new EcoUI.Card("objective", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), "poi_quest", "SIRADAKİ HEDEF",
            "Önerilen sonraki adım. Kartı daralt / genişlet.");
        // Keep the objective first among the card's labels for existing scene consumers which query the first Text.
        objective = EcoUI.Label("Current objective", objectiveCard.body, "", 17, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        objective.transform.SetAsFirstSibling();
        objectiveCard.summary.color = EcoUI.Ink; // the goal sits on its paper card like the other panels
    }

    private void BuildDock()
    {
        dock = EcoUI.Rect("Toolbelt", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        for (int i = 0; i < PlayerWeaponSystem.ToolCount; i++)
        {
            int index = i;
            slotBg[i] = EcoUI.Panel(ToolNames[i], dock, new Vector2(0, 1), new Vector2(0, 1), Color.white, true, EcoUI.ToolSlotSprite);
            slotRects[i] = slotBg[i].rectTransform;
            var button = slotBg[i].gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectSlot(index));
            EcoUI.AddTooltip(slotBg[i].gameObject, ToolTips[i]);
            slotMarker[i] = EcoUI.Panel("Selection", slotRects[i], new Vector2(0, 1), new Vector2(0, 1), EcoUI.Accent, false, EcoUI.Circle);
            slotMarker[i].enabled = false;
            var key = EcoUI.Panel("Key", slotRects[i], new Vector2(0, 1), new Vector2(0, 1), new Color(0.12f, 0.14f, 0.075f, 0.94f), false, EcoUI.Circle);
            slotKeyBg[i] = key;
            EcoUI.Place(key.rectTransform, new Vector2(6, -6), new Vector2(20, 20));
            slotKeys[i] = EcoUI.Label("Key text", key.transform, (i + 1).ToString(), 12, TextAnchor.MiddleCenter, EcoUI.PaperInk, true);
            EcoUI.Layout(slotKeys[i], Vector2.zero, new Vector2(20, 20));
            slotIcon[i] = EcoUI.Icon("Icon", slotRects[i], ToolIcons[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), 40f);
            if (toolIcons != null && i < toolIcons.Length && toolIcons[i] != null) { slotIcon[i].sprite = toolIcons[i]; slotIcon[i].enabled = true; }
            slotCountBg[i] = EcoUI.Panel("Quantity badge", slotRects[i], new Vector2(0, 1), new Vector2(0, 1), new Color(0.12f, 0.14f, 0.075f, 0.94f), false);
            slotTitles[i] = EcoUI.Label("Title", slotRects[i], ToolNames[i], 12, TextAnchor.MiddleCenter, EcoUI.Ink, true);
            slotCounts[i] = EcoUI.Label("Count", slotRects[i], "", 12, TextAnchor.MiddleCenter, EcoUI.PaperInk);
        }
        // Mode line and interaction hint sit on paper like the world captions; outlined text over grass and roads was hard to read.
        var modeBackground = EcoUI.Dress(EcoUI.Panel("Mode", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), EcoUI.Surface, false), false);
        modePanel = modeBackground.rectTransform;
        modeIcon = EcoUI.Icon("Mode icon", modePanel, "tool_vacuum", new Vector2(0, 1), new Vector2(0, 1), 24f);
        mode = EcoUI.Label("Active tool", modePanel, "", 16, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        FitOneLine(mode, 16);
        instructions = EcoUI.Label("Controls", modePanel, "", 13, TextAnchor.MiddleRight, EcoUI.Muted);
        FitOneLine(instructions, 13);
        contextBg = EcoUI.Dress(EcoUI.Panel("Context", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false));
        contextPanel = contextBg.rectTransform;
        context = EcoUI.Label("Interaction", contextPanel, "", 16, TextAnchor.MiddleCenter, EcoUI.Ink);
        feedbackBg = EcoUI.Dress(EcoUI.Panel("Feedback", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), EcoUI.Surface, false));
        feedbackPanel = feedbackBg.rectTransform;
        feedback = EcoUI.Label("Notice", feedbackPanel, "", 16, TextAnchor.MiddleCenter, EcoUI.Accent, true);
        recoveryBg = EcoUI.Dress(EcoUI.Panel("Recovery", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false));
        recoveryPanel = recoveryBg.rectTransform;
        recovery = EcoUI.Label("Recovery status", recoveryPanel, "", 21, TextAnchor.MiddleCenter, EcoUI.Ink, true);
    }

    private void SelectSlot(int index)
    {
        if (weapons == null || index < 0 || index >= PlayerWeaponSystem.ToolCount) return;
        if (weapons.Builder != null && weapons.Builder.IsBuilding)
        {
            if (index >= BuilderSlotCount) return;
            weapons.Builder.Select(weapons.Builder.Page * BuilderSlotCount + index);
        }
        else weapons.SelectTool(index);
        Refresh();
    }

    private void Update()
    {
        if (canvas == null) return;
        canvas.enabled = !EcoMarketUI.IsOpen && !EcoUI.ForeignWindowOpen;
        if (lastWidth != DisplayWidth || lastHeight != DisplayHeight || lastMode != EcoUI.Mode) dirty = true;
        if (Mathf.Abs(laidOutMapBottom - EcoWorldHUD.MapBottom) > 0.5f) dirty = true;
        if (weapons == null) return;
        bool building = weapons.Builder != null && weapons.Builder.IsBuilding;
        if (buildingLayout != building) dirty = true;
        if (dirty) ApplyLayout();
        int selected = building ? weapons.Builder.Selection % BuilderSlotCount : (int)weapons.Selected;
        for (int i = 0; i < PlayerWeaponSystem.ToolCount; i++)
        {
            bool showSlot = !building || i < BuilderSlotCount;
            if (slotRects[i].gameObject.activeSelf != showSlot) slotRects[i].gameObject.SetActive(showSlot);
            if (!showSlot) continue;
            bool active = selected == i;
            slotBg[i].sprite = active ? EcoUI.ActiveToolSlotSprite : EcoUI.ToolSlotSprite;
            slotBg[i].color = Color.white;
            var position = slotRects[i].anchoredPosition; position.y = active ? 8f : 0f; slotRects[i].anchoredPosition = position;
            slotMarker[i].enabled = false;
            slotTitles[i].color = active ? EcoUI.PaperInk : EcoUI.Ink;
            slotCounts[i].color = EcoUI.PaperInk;
            slotKeys[i].color = EcoUI.PaperInk;
            slotCountBg[i].gameObject.SetActive(!string.IsNullOrEmpty(slotCounts[i].text));
        }
        if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + 0.1f; Refresh(); }
    }

    private void OnInventoryChanged() { objectiveAt = 0f; Refresh(); }

    private void Refresh()
    {
        if (weapons == null || canvas == null) return;
        worldClock = EcoWorldClock.Current;
        // Vitals
        float[] values = { vitals != null ? vitals.Energy : 100, weapons.Energy, weapons.Water };
        float[] maxima = { vitals != null ? vitals.maximumEnergy : 100, weapons.maximumEnergy, weapons.maximumWater };
        Color[] colors = { vitals != null && vitals.IsRecovering ? new Color(1f, 0.62f, 0.32f) : EnergyColor, ElectricColor, weapons.HasDirtyWater ? new Color(0.8f, 0.66f, 0.33f) : WaterColor };
        meterNames[2].text = weapons.HasDirtyWater ? "Kirli su" : "Temiz su";
        EcoUI.SetIcon(meterIcons[2], weapons.HasDirtyWater ? "water_dirty" : "water");
        for (int i = 0; i < 3; i++)
        {
            EcoUI.SetMeter(meterFills[i], meterTracks[i], values[i] / Mathf.Max(1f, maxima[i]), colors[i]);
            meterValues[i].text = Mathf.CeilToInt(values[i]) + " / " + Mathf.CeilToInt(maxima[i]);
        }
        vitalsCard.summary.text = "<color=#" + EcoUI.Hex(colors[0]) + ">" + Mathf.CeilToInt(values[0]) + "</color>  ·  <color=#" + EcoUI.Hex(colors[1]) + ">"
            + Mathf.CeilToInt(values[1]) + " EB</color>  ·  <color=#" + EcoUI.Hex(colors[2]) + ">" + Mathf.CeilToInt(values[2]) + " SB</color>";
        if (worldClock != null)
        {
            EcoUI.SetIcon(weatherIcon, EcoIcons.Weather(worldClock));
            weatherText.text = "Gün " + worldClock.day + "  " + Mathf.FloorToInt(worldClock.hour).ToString("00") + ":" + Mathf.FloorToInt(worldClock.hour % 1f * 60f).ToString("00")
                + "  ·  " + worldClock.WeatherName;
            int wind = Mathf.RoundToInt(worldClock.windStrength * 100);
            windText.text = "%" + wind + (wind > 70 ? " güçlü" : wind < 25 ? " sakin" : "");
        }
        // Resources
        int[] counts =
        {
            weapons.Metal, weapons.Plastic, weapons.Food, weapons.Compost, weapons.CleanFish, weapons.DirtyFish, weapons.SmallFilters, weapons.LargeFilters,
            weapons.CollectedMetalScrap + weapons.CollectedPlasticScrap, weapons.CollectedOrganicWaste,
        };
        for (int i = 0; i < counts.Length; i++) chipValues[i].text = counts[i].ToString();
        resourcesCard.summary.text = weapons.Metal + " M  ·  " + weapons.Plastic + " P  ·  " + weapons.Food + " ürün";
        resourceQuickCounts[0].text = weapons.Metal.ToString();
        resourceQuickCounts[1].text = weapons.Plastic.ToString();
        resourceQuickCounts[2].text = weapons.Food.ToString();
        if (weapons.Progression != null)
        {
            bool max = weapons.Progression.Level == EcoProgression.MaximumLevel;
            // GDD §7: YEP is a finite score (360); every 40 YEP is one specialisation right (9 in total).
            var progression = weapons.Progression;
            yepText.text = "YEP " + progression.Experience + " / " + EcoProgression.MaximumExperience + "   ·   Hak " + progression.EarnedRights + " / 9";
            EcoUI.SetMeter(yepFill, yepTrack, max ? 1f : (progression.Experience % EcoProgression.PointsPerRight) / (float)EcoProgression.PointsPerRight, EcoUI.Amber);
        }
        // Tool belt
        slotCounts[0].text = Mathf.CeilToInt(weapons.Water).ToString();
        slotCounts[1].text = (weapons.MetalMode ? weapons.MetalAmmo : weapons.SeedCount(weapons.SelectedSeed)).ToString();
        slotCounts[2].text = (weapons.CollectedMetalScrap + weapons.CollectedPlasticScrap + weapons.CollectedOrganicWaste).ToString();
        slotCounts[3].text = ""; slotCounts[4].text = "";
        slotCounts[(int)PlayerWeaponSystem.Tool.Flashlight].text = weapons.FlashlightOn ? "Açık" : "Kapalı";
        string[] modes = { "Toplama", "Anti-vakum", "Su" };
        string[] modeIcons = { "tool_vacuum", "w_wind", "water" };
        var tool = weapons.Selected;
        mode.text = ToolNames[(int)tool] + (tool == PlayerWeaponSystem.Tool.Vacuum ? "  ·  " + modes[(int)weapons.Mode]
            : tool == PlayerWeaponSystem.Tool.SeedGun ? "  ·  " + (weapons.MetalMode ? "Demir top" : EcoCropCatalog.Get(weapons.SelectedSeed).Name + " · " + EcoCropCatalog.Get(weapons.SelectedSeed).Climate)
            : tool == PlayerWeaponSystem.Tool.Flashlight ? "  ·  " + (weapons.FlashlightOn ? "Açık" : "Kapalı") : "");
        EcoUI.SetIcon(modeIcon, tool == PlayerWeaponSystem.Tool.Vacuum ? modeIcons[(int)weapons.Mode] : tool == PlayerWeaponSystem.Tool.SeedGun && weapons.MetalMode ? "ammo" : ToolIcons[(int)tool]);
        instructions.text = tool == PlayerWeaponSystem.Tool.Hands ? EcoKeys.Tag(EcoKeys.Action.Interact) + " Etkileşim"
            : tool == PlayerWeaponSystem.Tool.Flashlight ? "[Sol tık] Aç / kapat"
            : tool == PlayerWeaponSystem.Tool.Scanner ? "[Sol tık] Tara"
            : tool == PlayerWeaponSystem.Tool.Recycler ? "[Sol tık] Geri dönüştür  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Gübrele"
            : "[Sol tık] Kullan  ·  " + EcoKeys.Tag(EcoKeys.Action.Mode) + " Mod" + (tool == PlayerWeaponSystem.Tool.SeedGun && !weapons.MetalMode ? "  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Tohum" : "");
        context.text = tool == PlayerWeaponSystem.Tool.Scanner && !string.IsNullOrEmpty(weapons.LastScan) ? weapons.LastScan : weapons.ContextHint();
        bool building = weapons.Builder != null && weapons.Builder.IsBuilding;
        for (int i = 0; i < PlayerWeaponSystem.ToolCount; i++)
        {
            if (building && i >= BuilderSlotCount) continue;
            slotTitles[i].text = building ? weapons.Builder.SlotName(i) : ToolNames[i];
            EcoUI.AddTooltip(slotBg[i].gameObject, building ? weapons.Builder.SlotTip(i) : ToolTips[i]);
            if (building) { slotCounts[i].text = weapons.Builder.SlotCost(i); EcoUI.SetIcon(slotIcon[i], BuildIcon(weapons.Builder.Page * BuilderSlotCount + i)); }
            else if (toolIcons == null || i >= toolIcons.Length || toolIcons[i] == null) EcoUI.SetIcon(slotIcon[i], ToolIcons[i]);
        }
        if (building)
        {
            mode.text = "İNŞA  ·  " + weapons.Builder.SelectedName + "  ·  " + (weapons.Builder.Page + 1) + "/" + weapons.Builder.PageCount;
            EcoUI.SetIcon(modeIcon, "ui_build");
            instructions.text = "[Tab] Sayfa  ·  [Sol tık] Kur  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Döndür  ·  " + EcoKeys.Tag(EcoKeys.Action.Build) + " Çık";
            context.text = weapons.Builder.Status;
        }
        SetVisible(contextBg, context, !string.IsNullOrEmpty(context.text) && EcoUI.Mode != EcoUI.HudMode.Hidden);
        FitContext();
        var chargerPower = weapons.ChargingStation != null ? weapons.ChargingStation.GetComponent<EcoPowerNode>() : null;
        feedback.text = weapons.ChargingStation != null
            ? (chargerPower != null && chargerPower.DeliveredPower > 0f ? "Araç bataryası şarj oluyor…" : "Şarj bağlı · Elektrik bekleniyor…")
            : Time.time < weapons.FeedbackUntil ? weapons.Feedback : "";
        SetVisible(feedbackBg, feedback, !string.IsNullOrEmpty(feedback.text));
        // Never under the region banner: both live at the top centre.
        feedbackPanel.anchoredPosition = new Vector2(feedbackPanel.anchoredPosition.x,
            -Mathf.Max(feedbackTop, EcoWorldHUD.BannerBottom + 8f, EcoFinale.CardBottomPixels / Mathf.Max(0.01f, uiScale) + 8f));
        bool recovering = vitals != null && vitals.IsRecovering;
        recovery.text = recovering ? "ENERJİ TOPLANIYOR\n" + vitals.RecoveryRemaining.ToString("0.0") + " saniye" : "";
        SetVisible(recoveryBg, recovery, recovering);
        if (Time.unscaledTime >= objectiveAt) { objectiveAt = Time.unscaledTime + 0.5f; RefreshObjective(); }
    }

    /// <summary>One line that shrinks (down to 10 pt) instead of wrapping or running into its neighbour.</summary>
    private static void FitOneLine(Text text, int size)
    {
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = size * EcoUI.RasterScale; text.resizeTextMinSize = 10 * EcoUI.RasterScale;
    }

    private float contextMaxWidth = 460f, contextBottom;
    private string contextFitFor;

    // The hint card hugs its text: a one-line "[E] Hasat et" no longer sits in a 460×68 box.
    private void FitContext()
    {
        if (context == null || context.text == contextFitFor) return;
        contextFitFor = context.text;
        float width = Mathf.Min(contextMaxWidth, context.preferredWidth / EcoUI.RasterScale + 36f);
        EcoUI.Layout(context, new Vector2(14f, -6f), new Vector2(width - 28f, 60f));
        float textHeight = Mathf.Clamp(context.preferredHeight / EcoUI.RasterScale, 20f, 60f);
        EcoUI.Layout(context, new Vector2(14f, -6f), new Vector2(width - 28f, textHeight));
        float panelHeight = textHeight + 12f;
        EcoUI.Place(contextPanel, new Vector2(0f, contextBottom + panelHeight * 0.5f), new Vector2(width, panelHeight));
    }

    private static void FloatingText(Text text)
    {
        text.color = EcoUI.PaperInk;
        var outline = text.GetComponent<Outline>();
        if (outline == null) outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.035f, 0.055f, 0.025f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private static void HudFrame(EcoUI.Card card, bool visible)
    {
        card.background.enabled = visible;
        card.background.raycastTarget = visible;
        card.rule.gameObject.SetActive(visible && !card.IsCollapsed);
    }

    private static void PlaceToggle(EcoUI.Card card, Vector2 position)
    {
        var toggleRect = (RectTransform)card.toggle.transform;
        toggleRect.anchorMin = toggleRect.anchorMax = new Vector2(1, 1);
        toggleRect.pivot = Vector2.one;
        EcoUI.Place(toggleRect, position, new Vector2(18f, 18f));
    }
    private static string BuildIcon(int index)
    {
        if (index == EcoBuildingCatalog.Entries.Length) return "electricity";
        if (index == EcoBuildingCatalog.Entries.Length + 1) return "water";
        if (index > EcoBuildingCatalog.Entries.Length + 1) return ""; // empty slot on the last page
        if (index < 0 || index >= EcoBuildingCatalog.Entries.Length) return "ui_build";
        return EcoBuildingCatalog.Entries[index].Id switch
        {
            "solar" => "poi_solar", "wind" => "poi_turbine", "battery" => "electricity", "charger" => "poi_charge", "intake" => "water",
            "purifier" => "filter", "tank" => "water_quality", "recycling" => "tool_recycle", "farm" => "seed", "hydro" => "poi_hydro", _ => "ui_build",
        };
    }

    private void RefreshObjective()
    {
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None); int alive = 0;
        // Only nearby threats are an objective; enemies elsewhere on the map show on the map instead (GDD §4 first minutes).
        Vector3 here = weapons != null ? weapons.transform.position : Vector3.zero;
        for (int i = 0; i < enemies.Length; i++)
            if (enemies[i] != null && enemies[i].State != EnemyBrain.Behaviour.Dead && enemies[i].Health > 0f
                && (enemies[i].transform.position - here).sqrMagnitude < 30f * 30f) alive++;
        string text;
        var finale = EcoFinale.Current; var readiness = EcoProjects.Current;
        // GDD §12: the endgame state outranks the early-game guidance.
        if (finale != null && finale.Won) text = "Final kazanıldı · serbest oyun: siparişler ve çevre";
        else if (finale != null && finale.Fighting) text = "Finali tamamla · Faz " + (finale.Phase + 1) + "/3";
        else if (readiness != null && readiness.Readiness >= EcoFinale.MinimumReadiness) text = "Final hazır · buzul terminalinde " + EcoKeys.Tag(EcoKeys.Action.Finale);
        else if (alive > 0) text = "Yakında tehdit · " + alive + " çevresel tehdit (aracın uygun modunu kullan)";
        else if (EcoStory.Current != null && !EcoStory.Current.intro) text = "Mahalle çocuğuyla konuş · şehir merkezi, " + EcoKeys.Tag(EcoKeys.Action.Interact);
        else
        {
            var crops = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None); bool mature = false, living = false;
            for (int i = 0; i < crops.Length; i++)
            {
                if (crops[i] == null || crops[i].IsWithered) continue;
                living = true; if (crops[i].IsMature) mature = true;
            }
            var projects = EcoProjects.Current;
            int open = -1;
            if (projects != null) for (int i = 0; i < projects.All.Count; i++) if (!projects.All[i].done) { open = i; break; }
            text = !living ? "Tohum ekerek çevreyi canlandır" : mature ? "Olgun bitkiyi hasat et"
                : weapons.Food > 0 && open >= 0 ? "Proje: " + projects.All[open].name + " · " + EcoKeys.Tag(EcoKeys.Action.Journal) + " günlük"
                : weapons.Food > 0 ? "Ekosistemi koru" : "Bitkini vakumun su moduyla sula";
        }
        objective.text = text;
        objectiveCard.summary.text = text; // best fit shrinks long goals instead of cutting them at 34 characters
    }

    // ------------------------------------------------------------------ layout

    private void ApplyLayout()
    {
        dirty = false;
        lastWidth = DisplayWidth; lastHeight = DisplayHeight; lastMode = EcoUI.Mode;
        float scale = EcoUI.Scale(canvas);
        scaler.scaleFactor = scale; canvas.scaleFactor = scale; uiScale = scale;
        float width = lastWidth / scale, height = lastHeight / scale;
        const float margin = 16f;
        bool compact = width < 1250f;
        var hudMode = EcoUI.Mode;
        bool hidden = hudMode == EcoUI.HudMode.Hidden, sade = hudMode == EcoUI.HudMode.Compact;
        float leftWidth = LeftColumnWidth, rightWidth = compact ? 250f : 276f;
        bool narrow = width - leftWidth - rightWidth - 4f * margin < 280f;

        // Character portrait and meters sit directly over the world, without a card header.
        vitalsCard.SetVisible(!hidden);
        vitalsCard.Layout(new Vector2(margin, -margin), leftWidth, 94f);
        bool vitalsCollapsed = vitalsCard.IsCollapsed;
        float vh = vitalsCollapsed ? 32f : 94f;
        vitalsCard.root.sizeDelta = new Vector2(leftWidth, vh);
        HudFrame(vitalsCard, false);
        vitalsCard.title.gameObject.SetActive(false); vitalsCard.icon.gameObject.SetActive(false);
        vitalsCard.summary.gameObject.SetActive(vitalsCollapsed);
        EcoUI.Layout(vitalsCard.summary, new Vector2(38f, -6f), new Vector2(leftWidth - 66f, 20f));
        EcoUI.Place(vitalsCard.body, Vector2.zero, new Vector2(leftWidth, 94f));
        PlaceToggle(vitalsCard, new Vector2(-1f, -5f));
        bool hasPortrait = portrait.enabled && portrait.sprite != null;
        portraitRoot.gameObject.SetActive(hasPortrait);
        portraitFrame.gameObject.SetActive(hasPortrait && portraitFrame.enabled);
        EcoUI.Place(portraitRoot, vitalsCollapsed ? new Vector2(5f, -3f) : new Vector2(8f, -9f),
            Vector2.one * (vitalsCollapsed ? 26f : 66f));
        EcoUI.Place(portraitFrame.rectTransform, vitalsCollapsed ? new Vector2(1f, 1f) : new Vector2(0f, -1f),
            Vector2.one * (vitalsCollapsed ? 34f : 82f));
        float meterX = hasPortrait ? 86f : 4f;
        for (int i = 0; i < 3; i++)
        {
            float y = -9f - i * 27f;
            meterNames[i].gameObject.SetActive(false);
            meterValues[i].fontSize = 12 * EcoUI.RasterScale;
            EcoUI.LayoutIcon(meterIcons[i], new Vector2(meterX, y), 20f);
            EcoUI.Layout(meterValues[i], new Vector2(leftWidth - 80f, y), new Vector2(56f, 20f));
            EcoUI.Place(meterTracks[i], new Vector2(meterX + 24f, y - 6f), new Vector2(leftWidth - meterX - 108f, 10f));
            EcoUI.Place(meterHits[i], new Vector2(meterX - 2f, y + 2f), new Vector2(leftWidth - meterX - 20f, 25f));
        }
        // Day, time and weather moved to the map card (GDD §13, top right).
        weatherIcon.gameObject.SetActive(false); weatherText.gameObject.SetActive(false);
        windIcon.gameObject.SetActive(false); windText.gameObject.SetActive(false); clockIcon.gameObject.SetActive(false);
        VitalsBottom = hidden ? margin : margin + vh;

        // Resources card, under EcoWorldHUD's map card in the right column.
        bool showResources = !hidden;
        resourcesCard.SetVisible(showResources);
        laidOutMapBottom = EcoWorldHUD.MapBottom;
        float ry = EcoWorldHUD.MapBottom + 10f;
        float chipW = (rightWidth - 30f) * 0.5f;
        for (int i = 0; i < Chips.Length; i++)
        {
            var rt = (RectTransform)chipHits[i].transform;
            EcoUI.Place(rt, new Vector2(10f + (i % 2) * (chipW + 10f), -6f - (i / 2) * 32f), new Vector2(chipW, 30f));
            EcoUI.LayoutIcon(chipIcons[i], new Vector2(4f, -4f), 22f);
            EcoUI.Layout(chipValues[i], new Vector2(32f, -1f), new Vector2(chipW - 38f, 17f));
            EcoUI.Layout(chipNames[i], new Vector2(32f, -18f), new Vector2(chipW - 38f, 12f));
            FitOneLine(chipNames[i], 11);
        }
        float yy = -6f - 5 * 32f - 4f;
        EcoUI.LayoutIcon(yepIcon, new Vector2(10f, yy), 24f);
        EcoUI.Layout(yepText, new Vector2(40f, yy), new Vector2(rightWidth - 52f, 18f));
        EcoUI.Place(yepTrack, new Vector2(40f, yy - 21f), new Vector2(rightWidth - 52f, 6f));
        // Inventory totals remain small; opening the foldout reveals every resource and YEP.
        resourcesCard.Layout(new Vector2(-margin, -ry), rightWidth, sade ? 0f : 206f, sade);
        bool resourceCompact = sade || resourcesCard.IsCollapsed || ry + 240f > height - margin;
        resourcesCard.title.gameObject.SetActive(!resourceCompact);
        resourcesCard.icon.gameObject.SetActive(!resourceCompact);
        resourcesCard.summary.gameObject.SetActive(false);
        resourceQuick.gameObject.SetActive(resourceCompact);
        HudFrame(resourcesCard, !resourceCompact);
        PlaceToggle(resourcesCard, new Vector2(-3f, -6f));
        if (resourceCompact)
        {
            resourcesCard.body.gameObject.SetActive(false);
            resourcesCard.root.sizeDelta = new Vector2(rightWidth, 30f);
        }
        resourceQuick.anchorMin = resourceQuick.anchorMax = Vector2.one; resourceQuick.pivot = Vector2.one;
        EcoUI.Place(resourceQuick, new Vector2(-28f, -2f), new Vector2(204f, 28f));
        for (int i = 0; i < 3; i++)
        {
            EcoUI.LayoutIcon(resourceQuickIcons[i], new Vector2(i * 68f, -3f), 22f);
            EcoUI.Layout(resourceQuickCounts[i], new Vector2(i * 68f + 26f, -3f), new Vector2(39f, 22f));
        }

        // A quest symbol and a short line, rather than a second window across the scene.
        objectiveCard.SetVisible(!hidden);
        float objectiveWidth = narrow ? Mathf.Min(440f, width - margin * 2f)
            : Mathf.Min(440f, width - leftWidth - rightWidth - 4f * margin);
        float objectiveCenter = narrow ? 0f : (leftWidth - rightWidth) * 0.5f;
        float objectiveTop = narrow ? Mathf.Max(VitalsBottom + 56f, EcoWorldHUD.MapBottom) + 12f : margin;
        objectiveCard.Layout(new Vector2(objectiveCenter, -objectiveTop), objectiveWidth, 50f, sade);
        bool objectiveCompact = sade || objectiveCard.IsCollapsed || narrow;
        float oh = objectiveCompact ? 28f : 52f;
        objectiveCard.root.sizeDelta = new Vector2(objectiveWidth, oh);
        HudFrame(objectiveCard, true); objectiveCard.rule.gameObject.SetActive(false); objectiveCard.title.gameObject.SetActive(false);
        EcoUI.LayoutIcon(objectiveCard.icon, new Vector2(0f, -3f), 26f);
        PlaceToggle(objectiveCard, new Vector2(-1f, -5f));
        objectiveCard.summary.gameObject.SetActive(objectiveCompact);
        objectiveCard.summary.alignment = TextAnchor.MiddleLeft; // next to the quest symbol, not pushed to the far edge
        EcoUI.Layout(objectiveCard.summary, new Vector2(34f, -3f), new Vector2(objectiveWidth - 60f, 22f));
        objectiveCard.body.gameObject.SetActive(!objectiveCompact);
        EcoUI.Place(objectiveCard.body, new Vector2(34f, -2f), new Vector2(objectiveWidth - 60f, 48f));
        EcoUI.Layout(objective, Vector2.zero, new Vector2(objectiveWidth - 60f, 48f));

        // Tool belt + mode line.
        buildingLayout = weapons != null && weapons.Builder != null && weapons.Builder.IsBuilding;
        int slotCount = buildingLayout ? BuilderSlotCount : PlayerWeaponSystem.ToolCount;
        float normalDockWidth = PlayerWeaponSystem.ToolCount * 64f + (PlayerWeaponSystem.ToolCount - 1) * 6f;
        float dockWidth = Mathf.Min(buildingLayout ? 580f : normalDockWidth, width - 2f * margin);
        float slotHeight = buildingLayout ? 94f : 72f;
        DockWidth = hidden ? 0f : dockWidth;
        dock.gameObject.SetActive(!hidden);
        EcoUI.Place(dock, new Vector2(0f, margin), new Vector2(dockWidth, slotHeight));
        float slotWidth = (dockWidth - (slotCount - 1) * 6f) / slotCount;
        for (int i = 0; i < PlayerWeaponSystem.ToolCount; i++)
        {
            bool showSlot = i < slotCount;
            slotRects[i].gameObject.SetActive(showSlot);
            if (!showSlot) continue;
            EcoUI.Place(slotRects[i], new Vector2(i * (slotWidth + 6f), 0f), new Vector2(slotWidth, slotHeight));
            EcoUI.LayoutIcon(slotIcon[i], new Vector2(0f, -8f), buildingLayout ? 40f : 44f);
            slotKeyBg[i].rectTransform.sizeDelta = new Vector2(18f, 18f);
            EcoUI.Place(slotKeyBg[i].rectTransform, new Vector2(2f, -2f), new Vector2(18f, 18f));
            EcoUI.Layout(slotKeys[i], Vector2.zero, new Vector2(18f, 18f));
            slotKeyBg[i].rectTransform.SetAsLastSibling();
            slotTitles[i].gameObject.SetActive(buildingLayout);
            EcoUI.Layout(slotTitles[i], new Vector2(4f, -49f), new Vector2(slotWidth - 8f, 18f));
            float countY = buildingLayout ? -71f : -53f;
            EcoUI.Place(slotCountBg[i].rectTransform, new Vector2(5f, countY), new Vector2(slotWidth - 10f, 16f));
            EcoUI.Layout(slotCounts[i], new Vector2(5f, countY), new Vector2(slotWidth - 10f, 16f));
        }
        modePanel.gameObject.SetActive(!hidden);
        float modeHeight = sade ? 30f : 46f;
        float modeWidth = Mathf.Min(buildingLayout ? 600f : 440f, width - 2f * margin);
        EcoUI.Place(modePanel, new Vector2(0f, margin + slotHeight + 12f), new Vector2(modeWidth, modeHeight));
        // Text keeps a few pixels of paper above and below; it used to touch the frame, worst in the 24 px Sade bar.
        EcoUI.LayoutIcon(modeIcon, new Vector2(8f, -4f), 22f);
        mode.alignment = TextAnchor.MiddleCenter;
        EcoUI.Layout(mode, new Vector2(32f, -4f), new Vector2(modeWidth - 64f, 22f));
        instructions.gameObject.SetActive(!sade);
        instructions.alignment = TextAnchor.MiddleCenter;
        EcoUI.Layout(instructions, new Vector2(12f, -25f), new Vector2(modeWidth - 24f, 17f));
        DockTop = hidden ? margin : margin + slotHeight + 12f + modeHeight;

        float contextWidth = Mathf.Min(460f, width - 2f * margin);
        contextMaxWidth = contextWidth; contextBottom = -height * 0.5f + DockTop + 10f; contextFitFor = null;
        FitContext();
        float feedbackWidth = Mathf.Min(520f, objectiveWidth + 80f);
        feedbackTop = objectiveTop + oh + 10f;
        ObjectiveBottomPixels = hidden ? 0f : (objectiveTop + oh) * uiScale;
        EcoUI.Place(feedbackPanel, new Vector2(objectiveCenter, -feedbackTop), new Vector2(feedbackWidth, 56f));
        EcoUI.Layout(feedback, new Vector2(12f, -6f), new Vector2(feedbackWidth - 24f, 44f));
        EcoUI.Place(recoveryPanel, new Vector2(0f, 80f), new Vector2(Mathf.Min(460f, width - 2f * margin), 72f));
        EcoUI.Layout(recovery, new Vector2(14f, -8f), new Vector2(recoveryPanel.sizeDelta.x - 28f, 56f));
    }

    private int DisplayWidth => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.width));
    private int DisplayHeight => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.height));

    private static void SetVisible(Image background, Text text, bool visible)
    {
        if (background.gameObject.activeSelf != visible) background.gameObject.SetActive(visible);
    }

    private void OnDestroy()
    {
        EcoUI.LayoutChanged -= MarkDirty;
        if (weapons != null) weapons.Changed -= OnInventoryChanged;
        if (canvas != null) Destroy(canvas.gameObject);
        if (vitals != null) vitals.showLegacyHUD = previousLegacyHud;
    }
}
