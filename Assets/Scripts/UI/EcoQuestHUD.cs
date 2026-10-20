using UnityEngine;
using UnityEngine.UI;

// Player layer of the HUD: vitals (top-left), resources (right column, under the map), the next objective (top centre),
// the tool belt with its mode line (bottom centre) and the interaction / feedback / recovery messages.
// Every card can be collapsed with its chevron; [H] cycles Tam → Sade → Gizli (EcoUI.Mode). EcoWorldHUD stacks its
// cards around these using the heights published below.
[DefaultExecutionOrder(40)]
public sealed class EcoQuestHUD : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    [Header("Optional imported UI artwork")]
    public Sprite panelSprite;
    public Sprite slotSprite;
    public Sprite[] toolIcons = new Sprite[5];

    /// <summary>Bottom edge (canvas units from the top) of the vitals card; EcoWorldHUD places the environment card below.</summary>
    public static float VitalsBottom { get; private set; } = 220f;
    /// <summary>Height of the tool belt + mode line block at the bottom of the screen.</summary>
    public static float DockTop { get; private set; } = 170f;

    private static readonly string[] ToolNames = { "VAKUM", "TOHUM", "GERİ DÖNÜŞÜM", "TARAYICI", "ELLER" };
    private static readonly string[] ToolIcons = { "tool_vacuum", "tool_seed", "tool_recycle", "tool_scanner", "tool_hand" };
    private static readonly string[] ToolTips =
    {
        "Vakum [1]: atık toplar, anti-vakumla duman/asit dağıtır, su moduyla söndürür ve sular. [Q] mod.",
        "Tohum aracı [2]: toprağa tohum eker; demir top modunda Teneke'yi sersemletir. [Q] mod, [R] tohum türü.",
        "Geri dönüşüm [3]: atığı yerinde işler, [R] ile gübre uygular.",
        "Tarayıcı [4]: neden, kalite ve bağlantı bilgisini gösterir; hasar vermez.",
        "Eller [5]: etkileşim, hasat ve balıkçılık.",
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
    private readonly Text[] meterNames = new Text[3], meterValues = new Text[3];
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
    private Image yepIcon, yepFill; private RectTransform yepTrack; private Text yepText;
    // Objective
    private Text objective;
    // Dock
    private RectTransform dock, modePanel, contextPanel, feedbackPanel, recoveryPanel, aim;
    private readonly Image[] slotBg = new Image[5], slotIcon = new Image[5], slotMarker = new Image[5];
    private readonly RectTransform[] slotRects = new RectTransform[5];
    private readonly Text[] slotTitles = new Text[5], slotCounts = new Text[5], slotKeys = new Text[5];
    private Image modeIcon;
    private Text mode, instructions, context, feedback, recovery;
    private Image contextBg, feedbackBg, recoveryBg;
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
            meterFills[i] = EcoUI.Meter("Meter " + i, vitalsCard.body, out meterTracks[i]);
            var hit = EcoUI.Panel("Hit " + i, vitalsCard.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
            hit.rectTransform.anchoredPosition = new Vector2(0, -6 - i * 36); hit.rectTransform.sizeDelta = new Vector2(300, 34);
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
        for (int i = 0; i < Chips.Length; i++)
        {
            var hit = EcoUI.Panel("Chip " + i, resourcesCard.body, new Vector2(0, 1), new Vector2(0, 1), new Color(1f, 1f, 1f, 0.035f));
            chipHits[i] = hit.gameObject; EcoUI.AddTooltip(hit.gameObject, Chips[i].tip);
            chipIcons[i] = EcoUI.Icon("Icon", hit.transform, Chips[i].icon, new Vector2(0, 1), new Vector2(0, 1), 22f);
            chipValues[i] = EcoUI.Label("Value", hit.transform, "0", 16, TextAnchor.MiddleLeft, EcoUI.Ink, true);
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
        objective = EcoUI.Label("Current objective", objectiveCard.body, "", 17, TextAnchor.UpperLeft, EcoUI.Ink, true);
        objective.transform.SetAsFirstSibling();
    }

    private void BuildDock()
    {
        dock = EcoUI.Rect("Toolbelt", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            slotBg[i] = EcoUI.Panel(ToolNames[i], dock, new Vector2(0, 1), new Vector2(0, 1), EcoUI.Surface);
            slotRects[i] = slotBg[i].rectTransform;
            var button = slotBg[i].gameObject.AddComponent<Button>(); button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SelectSlot(index));
            EcoUI.AddTooltip(slotBg[i].gameObject, ToolTips[i]);
            slotMarker[i] = EcoUI.Panel("Selection", slotRects[i], new Vector2(0, 1), new Vector2(0, 1), EcoUI.Accent, false, EcoUI.Circle);
            var key = EcoUI.Panel("Key", slotRects[i], new Vector2(0, 1), new Vector2(0, 1), new Color(0f, 0f, 0f, 0.35f), false, EcoUI.Circle);
            EcoUI.Place(key.rectTransform, new Vector2(6, -6), new Vector2(20, 20));
            slotKeys[i] = EcoUI.Label("Key text", key.transform, (i + 1).ToString(), 12, TextAnchor.MiddleCenter, EcoUI.Muted, true);
            EcoUI.Layout(slotKeys[i], Vector2.zero, new Vector2(20, 20));
            slotIcon[i] = EcoUI.Icon("Icon", slotRects[i], ToolIcons[i], new Vector2(0.5f, 1), new Vector2(0.5f, 1), 40f);
            if (toolIcons != null && i < toolIcons.Length && toolIcons[i] != null) { slotIcon[i].sprite = toolIcons[i]; slotIcon[i].enabled = true; }
            slotTitles[i] = EcoUI.Label("Title", slotRects[i], ToolNames[i], 12, TextAnchor.MiddleCenter, EcoUI.Ink, true);
            slotCounts[i] = EcoUI.Label("Count", slotRects[i], "", 12, TextAnchor.MiddleCenter, EcoUI.Muted);
        }
        modePanel = EcoUI.Panel("Mode", canvas.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), EcoUI.Surface).rectTransform;
        modeIcon = EcoUI.Icon("Mode icon", modePanel, "tool_vacuum", new Vector2(0, 1), new Vector2(0, 1), 24f);
        mode = EcoUI.Label("Active tool", modePanel, "", 16, TextAnchor.MiddleLeft, EcoUI.Ink, true);
        instructions = EcoUI.Label("Controls", modePanel, "", 13, TextAnchor.MiddleRight, EcoUI.Muted);
        contextBg = EcoUI.Panel("Context", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false);
        contextPanel = contextBg.rectTransform;
        context = EcoUI.Label("Interaction", contextPanel, "", 17, TextAnchor.MiddleCenter, EcoUI.Ink);
        feedbackBg = EcoUI.Panel("Feedback", canvas.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), EcoUI.Surface, false);
        feedbackPanel = feedbackBg.rectTransform;
        feedback = EcoUI.Label("Notice", feedbackPanel, "", 16, TextAnchor.MiddleCenter, EcoUI.Accent, true);
        recoveryBg = EcoUI.Panel("Recovery", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), EcoUI.Surface, false);
        recoveryPanel = recoveryBg.rectTransform;
        recovery = EcoUI.Label("Recovery status", recoveryPanel, "", 21, TextAnchor.MiddleCenter, EcoUI.Ink, true);
        aim = EcoUI.Rect("Aim", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        Color aimColor = new Color(0.96f, 1f, 0.98f, 0.9f);
        foreach (var (pos, size) in new[] { (new Vector2(0, 8), new Vector2(2, 5)), (new Vector2(0, -8), new Vector2(2, 5)), (new Vector2(-8, 0), new Vector2(5, 2)), (new Vector2(8, 0), new Vector2(5, 2)), (Vector2.zero, new Vector2(2, 2)) })
        {
            var strip = EcoUI.Fill("Aim part", aim, aimColor);
            var rt = strip.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); EcoUI.Place(rt, pos, size);
        }
    }

    private void SelectSlot(int index)
    {
        if (weapons == null) return;
        if (weapons.Builder != null && weapons.Builder.IsBuilding) weapons.Builder.Select(weapons.Builder.Page * 5 + index);
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
        int selected = building ? weapons.Builder.Selection % 5 : (int)weapons.Selected;
        for (int i = 0; i < 5; i++)
        {
            bool active = selected == i;
            slotBg[i].color = active ? EcoUI.Selected : EcoUI.Surface;
            slotMarker[i].enabled = active; slotTitles[i].color = active ? Color.white : EcoUI.Ink;
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
        if (weapons.Progression != null)
        {
            bool max = weapons.Progression.Level == EcoProgression.MaximumLevel;
            // GDD §7: YEP is a finite score (360); every 40 YEP is one specialisation right (9 in total).
            var progression = weapons.Progression;
            yepText.text = "YEP " + progression.Experience + " / " + EcoProgression.MaximumExperience + "   ·   Hak " + progression.EarnedRights + " / 9";
            EcoUI.SetMeter(yepFill, yepTrack, max ? 1f : (progression.Experience % EcoProgression.PointsPerRight) / (float)EcoProgression.PointsPerRight, EcoUI.Amber);
        }
        // Tool belt
        slotCounts[0].text = Mathf.CeilToInt(weapons.Water) + " su";
        slotCounts[1].text = weapons.MetalMode ? weapons.MetalAmmo + " demir" : weapons.SeedCount(weapons.SelectedSeed) + " tohum";
        slotCounts[2].text = (weapons.CollectedMetalScrap + weapons.CollectedPlasticScrap + weapons.CollectedOrganicWaste) + " atık";
        slotCounts[3].text = "Analiz"; slotCounts[4].text = "Etkileşim";
        string[] modes = { "Toplama", "Anti-vakum", "Su" };
        string[] modeIcons = { "tool_vacuum", "w_wind", "water" };
        var tool = weapons.Selected;
        mode.text = ToolNames[(int)tool] + (tool == PlayerWeaponSystem.Tool.Vacuum ? "  ·  " + modes[(int)weapons.Mode]
            : tool == PlayerWeaponSystem.Tool.SeedGun ? "  ·  " + (weapons.MetalMode ? "Demir top" : EcoCropCatalog.Get(weapons.SelectedSeed).Name + " · " + EcoCropCatalog.Get(weapons.SelectedSeed).Climate) : "");
        EcoUI.SetIcon(modeIcon, tool == PlayerWeaponSystem.Tool.Vacuum ? modeIcons[(int)weapons.Mode] : tool == PlayerWeaponSystem.Tool.SeedGun && weapons.MetalMode ? "ammo" : ToolIcons[(int)tool]);
        instructions.text = tool == PlayerWeaponSystem.Tool.Hands ? EcoKeys.Tag(EcoKeys.Action.Interact) + " Etkileşim"
            : tool == PlayerWeaponSystem.Tool.Scanner ? "[Sol tık] Tara"
            : tool == PlayerWeaponSystem.Tool.Recycler ? "[Sol tık] Geri dönüştür  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Gübrele"
            : "[Sol tık] Kullan  ·  " + EcoKeys.Tag(EcoKeys.Action.Mode) + " Mod" + (tool == PlayerWeaponSystem.Tool.SeedGun && !weapons.MetalMode ? "  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Tohum" : "");
        context.text = tool == PlayerWeaponSystem.Tool.Scanner && !string.IsNullOrEmpty(weapons.LastScan) ? weapons.LastScan : weapons.ContextHint();
        bool building = weapons.Builder != null && weapons.Builder.IsBuilding;
        for (int i = 0; i < 5; i++)
        {
            slotTitles[i].text = building ? weapons.Builder.SlotName(i) : ToolNames[i];
            if (building) { slotCounts[i].text = weapons.Builder.SlotCost(i); EcoUI.SetIcon(slotIcon[i], BuildIcon(weapons.Builder.Page * 5 + i)); }
            else if (toolIcons == null || i >= toolIcons.Length || toolIcons[i] == null) EcoUI.SetIcon(slotIcon[i], ToolIcons[i]);
        }
        if (building)
        {
            mode.text = "İNŞA  ·  " + weapons.Builder.SelectedName + "  ·  sayfa " + (weapons.Builder.Page + 1) + "/" + weapons.Builder.PageCount;
            EcoUI.SetIcon(modeIcon, "ui_build");
            instructions.text = "[Tab] Sayfa  ·  [Sol tık] Kur  ·  " + EcoKeys.Tag(EcoKeys.Action.Rotate) + " Döndür  ·  " + EcoKeys.Tag(EcoKeys.Action.Build) + " Çık";
            context.text = weapons.Builder.Status;
        }
        SetVisible(contextBg, context, !string.IsNullOrEmpty(context.text) && EcoUI.Mode != EcoUI.HudMode.Hidden);
        var chargerPower = weapons.ChargingStation != null ? weapons.ChargingStation.GetComponent<EcoPowerNode>() : null;
        feedback.text = weapons.ChargingStation != null
            ? (chargerPower != null && chargerPower.DeliveredPower > 0f ? "Araç bataryası şarj oluyor…" : "Şarj bağlı · Elektrik bekleniyor…")
            : Time.time < weapons.FeedbackUntil ? weapons.Feedback : "";
        SetVisible(feedbackBg, feedback, !string.IsNullOrEmpty(feedback.text));
        bool recovering = vitals != null && vitals.IsRecovering;
        recovery.text = recovering ? "ENERJİ TOPLANIYOR\n" + vitals.RecoveryRemaining.ToString("0.0") + " saniye" : "";
        SetVisible(recoveryBg, recovery, recovering);
        if (Time.unscaledTime >= objectiveAt) { objectiveAt = Time.unscaledTime + 0.5f; RefreshObjective(); }
    }

    private static string BuildIcon(int index)
    {
        if (index == EcoBuildingCatalog.Entries.Length) return "electricity";
        if (index == EcoBuildingCatalog.Entries.Length + 1) return "water";
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
        for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null && enemies[i].State != EnemyBrain.Behaviour.Dead && enemies[i].Health > 0f) alive++;
        string text;
        var finale = EcoFinale.Current; var readiness = EcoProjects.Current;
        // GDD §12: the endgame state outranks the early-game guidance.
        if (finale != null && finale.Won) text = "Final kazanıldı · serbest oyun: siparişler ve çevre";
        else if (finale != null && finale.Fighting) text = "Finali tamamla · Faz " + (finale.Phase + 1) + "/3";
        else if (readiness != null && readiness.Readiness >= EcoFinale.MinimumReadiness) text = "Final hazır · buzul terminalinde " + EcoKeys.Tag(EcoKeys.Action.Finale);
        else if (alive > 0) text = "Alanı temizle · Kalan düşman: " + alive;
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
        objectiveCard.summary.text = text.Length > 34 ? text.Substring(0, 33) + "…" : text;
    }

    // ------------------------------------------------------------------ layout

    private void ApplyLayout()
    {
        dirty = false;
        lastWidth = DisplayWidth; lastHeight = DisplayHeight; lastMode = EcoUI.Mode;
        float scale = EcoUI.Scale(canvas);
        scaler.scaleFactor = scale; canvas.scaleFactor = scale;
        float width = lastWidth / scale, height = lastHeight / scale;
        const float margin = 16f;
        bool compact = width < 1250f;
        var hudMode = EcoUI.Mode;
        bool hidden = hudMode == EcoUI.HudMode.Hidden, sade = hudMode == EcoUI.HudMode.Compact;
        float leftWidth = compact ? 270f : 300f, rightWidth = compact ? 250f : 276f;

        // Vitals card.
        vitalsCard.SetVisible(!hidden);
        float vh = vitalsCard.Layout(new Vector2(margin, -margin), leftWidth, 114f);
        float inner = leftWidth - 24f;
        for (int i = 0; i < 3; i++)
        {
            float y = -8f - i * 36f;
            EcoUI.LayoutIcon(meterIcons[i], new Vector2(12f, y), 22f);
            EcoUI.Layout(meterNames[i], new Vector2(40f, y - 1f), new Vector2(inner - 120f, 20f));
            EcoUI.Layout(meterValues[i], new Vector2(leftWidth - 104f, y - 1f), new Vector2(92f, 20f));
            EcoUI.Place(meterTracks[i], new Vector2(40f, y - 23f), new Vector2(leftWidth - 52f, 6f));
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
            EcoUI.Place(rt, new Vector2(10f + (i % 2) * (chipW + 10f), -6f - (i / 2) * 32f), new Vector2(chipW, 28f));
            EcoUI.LayoutIcon(chipIcons[i], new Vector2(4f, -3f), 22f);
            EcoUI.Layout(chipValues[i], new Vector2(32f, -4f), new Vector2(40f, 20f));
            EcoUI.Layout(chipNames[i], new Vector2(70f, -6f), new Vector2(chipW - 72f, 18f));
        }
        float yy = -6f - 5 * 32f - 4f;
        EcoUI.LayoutIcon(yepIcon, new Vector2(10f, yy), 24f);
        EcoUI.Layout(yepText, new Vector2(40f, yy), new Vector2(rightWidth - 52f, 18f));
        EcoUI.Place(yepTrack, new Vector2(40f, yy - 21f), new Vector2(rightWidth - 52f, 6f));
        // Sade keeps the card but shows only its header summary.
        resourcesCard.Layout(new Vector2(-margin, -ry), rightWidth, sade ? 0f : 206f, sade);
        if (sade) resourcesCard.body.gameObject.SetActive(false);

        // Objective card (top centre, between the columns).
        objectiveCard.SetVisible(!hidden);
        float objectiveWidth = Mathf.Clamp(width - leftWidth - rightWidth - 4f * margin, 260f, 440f);
        float objectiveCenter = (leftWidth - rightWidth) * 0.5f;
        float oh = objectiveCard.Layout(new Vector2(objectiveCenter, -margin), objectiveWidth, sade ? 0f : 50f, sade);
        if (sade) objectiveCard.body.gameObject.SetActive(false);
        EcoUI.Layout(objective, new Vector2(14f, -2f), new Vector2(objectiveWidth - 28f, 44f));

        // Tool belt + mode line.
        float dockWidth = Mathf.Min(600f, width - 2f * margin);
        dock.gameObject.SetActive(!hidden);
        EcoUI.Place(dock, new Vector2(0f, margin), new Vector2(dockWidth, 86f)); // anchored and pivoted at the bottom centre
        float slotWidth = (dockWidth - 32f) / 5f;
        for (int i = 0; i < 5; i++)
        {
            EcoUI.Place(slotRects[i], new Vector2(i * (slotWidth + 8f), 0f), new Vector2(slotWidth, 86f));
            EcoUI.Place(slotMarker[i].rectTransform, new Vector2(10f, -2f), new Vector2(slotWidth - 20f, 4f));
            slotIcon[i].rectTransform.anchoredPosition = new Vector2(0f, -7f);
            EcoUI.Layout(slotTitles[i], new Vector2(4f, -48f), new Vector2(slotWidth - 8f, 18f));
            EcoUI.Layout(slotCounts[i], new Vector2(4f, -65f), new Vector2(slotWidth - 8f, 18f));
        }
        buildingLayout = weapons != null && weapons.Builder != null && weapons.Builder.IsBuilding;
        modePanel.gameObject.SetActive(!hidden);
        float modeHeight = 36f;
        EcoUI.Place(modePanel, new Vector2(0f, margin + 86f + 8f), new Vector2(dockWidth, modeHeight));
        EcoUI.LayoutIcon(modeIcon, new Vector2(10f, -6f), 24f);
        EcoUI.Layout(mode, new Vector2(42f, -8f), new Vector2(dockWidth * 0.5f, 20f));
        instructions.gameObject.SetActive(!sade);
        EcoUI.Layout(instructions, new Vector2(dockWidth * 0.42f, -9f), new Vector2(dockWidth * 0.58f - 12f, 20f));
        DockTop = hidden ? margin : margin + 86f + 8f + modeHeight;

        float contextWidth = Mathf.Min(560f, width - 2f * margin);
        EcoUI.Place(contextPanel, new Vector2(0f, -height * 0.5f + DockTop + 70f), new Vector2(contextWidth, 74f));
        EcoUI.Layout(context, new Vector2(14f, -8f), new Vector2(contextWidth - 28f, 58f));
        float feedbackWidth = Mathf.Min(520f, objectiveWidth + 80f);
        EcoUI.Place(feedbackPanel, new Vector2(objectiveCenter, -(margin + oh + 10f)), new Vector2(feedbackWidth, 56f));
        EcoUI.Layout(feedback, new Vector2(12f, -6f), new Vector2(feedbackWidth - 24f, 44f));
        EcoUI.Place(recoveryPanel, new Vector2(0f, 80f), new Vector2(Mathf.Min(460f, width - 2f * margin), 72f));
        EcoUI.Layout(recovery, new Vector2(14f, -8f), new Vector2(recoveryPanel.sizeDelta.x - 28f, 56f));
        aim.gameObject.SetActive(!hidden);
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
