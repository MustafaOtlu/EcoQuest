using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(40)]
public sealed class EcoQuestHUD : MonoBehaviour
{
    public PlayerWeaponSystem weapons;
    [Header("Optional imported UI artwork")]
    public Sprite panelSprite;
    public Sprite slotSprite;
    public Sprite[] toolIcons = new Sprite[5];
    private const int RasterScale = 2;
    private readonly Color surface = new Color(0.035f, 0.075f, 0.09f, 0.94f);
    private readonly Color ink = new Color(0.94f, 0.97f, 0.95f);
    private readonly Color muted = new Color(0.67f, 0.77f, 0.77f);
    private readonly Color accent = new Color(0.43f, 0.91f, 0.66f);
    private readonly Color[] meterColors = { new Color(0.34f, 0.86f, 0.59f), new Color(1f, 0.79f, 0.31f), new Color(0.36f, 0.77f, 1f) };
    private readonly string[] names = { "VAKUM", "TOHUM", "GERİ DÖNÜŞÜM", "TARAYICI", "ELLER" };
    private PlayerVitals vitals;
    private EcoWorldClock worldClock;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Font font;
    private RectTransform vitalsPanel, resourcesPanel, objectivePanel, dock, modePanel, contextPanel, feedbackPanel, recoveryPanel;
    private RectTransform[] tracks, slotRects;
    private Image[] fills, slots, markers;
    private Text[] barLabels, barValues, slotTitles, slotCounts;
    private Text mode, inventory, context, feedback, instructions, recovery, objective, weather;
    private float refreshAt, objectiveAt;
    private int lastWidth, lastHeight;
    private bool buildingLayout, previousLegacyHud;

    private void Start()
    {
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        vitals = weapons != null ? weapons.GetComponentInParent<PlayerVitals>() : null;
        if (vitals != null) { previousLegacyHud = vitals.showLegacyHUD; vitals.showLegacyHUD = false; }
        worldClock = EcoWorldClock.Current;
        // The dynamic built-in font includes Turkish glyphs. A 2x raster provides crisp
        // small labels without depending on a font installed only on the developer's PC.
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("EcoQuest HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)); root.layer = 5;
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20; canvas.pixelPerfect = true;
        scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        vitalsPanel = Panel("Vitals", root.transform, new Vector2(0, 1), Vector2.zero, new Vector2(296, 202), new Vector2(0, 1));
        Strip(vitalsPanel, "Accent", new Vector2(0, 1), Vector2.zero, new Vector2(3, 202), new Vector2(0, 1), accent);
        Label(vitalsPanel, "Brand", "ECOQUEST", new Vector2(16, -10), new Vector2(200, 23), 16, TextAnchor.MiddleLeft, accent, true);
        tracks = new RectTransform[3]; fills = new Image[3]; barLabels = new Text[3]; barValues = new Text[3];
        string[] labels = { "Karakter enerjisi", "Silah elektriği", "Temiz su" };
        for (int i = 0; i < 3; i++)
        {
            float y = -38 - i * 36;
            barLabels[i] = Label(vitalsPanel, "Meter name " + i, labels[i], new Vector2(16, y), new Vector2(176, 22), 17, TextAnchor.MiddleLeft);
            barValues[i] = Label(vitalsPanel, "Meter value " + i, "", new Vector2(192, y), new Vector2(88, 22), 17, TextAnchor.MiddleRight, ink, true);
            tracks[i] = Panel("Meter " + i, vitalsPanel, new Vector2(0, 1), new Vector2(16, y - 25), new Vector2(264, 5), new Vector2(0, 1), new Color(0.15f, 0.22f, 0.24f));
            fills[i] = Panel("Fill", tracks[i], new Vector2(0, 1), Vector2.zero, new Vector2(264, 5), new Vector2(0, 1), meterColors[i]).GetComponent<Image>();
        }
        weather = Label(vitalsPanel, "Weather", "", new Vector2(16, -150), new Vector2(264, 44), 15, TextAnchor.UpperLeft, muted);
        resourcesPanel = Panel("Materials", root.transform, new Vector2(1, 1), Vector2.zero, new Vector2(274, 188), new Vector2(1, 1));
        Label(resourcesPanel, "Heading", "KAYNAKLAR", new Vector2(14, -10), new Vector2(220, 22), 14, TextAnchor.MiddleLeft, accent, true);
        inventory = Label(resourcesPanel, "Inventory", "", new Vector2(14, -39), new Vector2(246, 143), 16, TextAnchor.UpperLeft);
        objectivePanel = Panel("Objective", root.transform, new Vector2(0.5f, 1), Vector2.zero, new Vector2(416, 92), new Vector2(0.5f, 1));
        // Keep the objective first for existing scene consumers which query the first Text.
        objective = Label(objectivePanel, "Current objective", "", new Vector2(16, -32), new Vector2(384, 54), 19, TextAnchor.UpperLeft, ink, true);
        Label(objectivePanel, "Heading", "SIRADAKİ HEDEF", new Vector2(16, -10), new Vector2(300, 18), 12, TextAnchor.MiddleLeft, accent, true);

        dock = Panel("Toolbelt", root.transform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(700, 96), new Vector2(0.5f, 0), Color.clear);
        slots = new Image[5]; slotRects = new RectTransform[5]; markers = new Image[5];
        slotTitles = new Text[5]; slotCounts = new Text[5];
        for (int i = 0; i < 5; i++)
        {
            var slot = Panel(names[i], dock, new Vector2(0, 1), new Vector2(i * 142, 0), new Vector2(132, 96), new Vector2(0, 1));
            slotRects[i] = slot; slots[i] = slot.GetComponent<Image>(); slots[i].sprite = slotSprite;
            markers[i] = Strip(slot, "Selection", new Vector2(0, 1), Vector2.zero, new Vector2(132, 3), new Vector2(0, 1), accent);
            Label(slot, "Key", (i + 1).ToString(), new Vector2(10, -7), new Vector2(24, 19), 15, TextAnchor.MiddleLeft, muted, true);
            slotTitles[i] = Label(slot, "Title", names[i], new Vector2(8, -25), new Vector2(116, 40), 15, TextAnchor.MiddleCenter, ink, true);
            slotCounts[i] = Label(slot, "Count", "", new Vector2(8, -69), new Vector2(116, 21), 15, TextAnchor.MiddleCenter, muted);
            if (toolIcons != null && i < toolIcons.Length && toolIcons[i] != null)
            {
                var icon = Panel("Icon", slot, new Vector2(1, 1), new Vector2(-9, -7), new Vector2(21, 21), new Vector2(1, 1), Color.white).GetComponent<Image>();
                icon.sprite = toolIcons[i]; icon.preserveAspect = true;
            }
        }
        modePanel = Panel("Mode", root.transform, new Vector2(0.5f, 0), Vector2.zero, new Vector2(700, 62), new Vector2(0.5f, 0));
        mode = Label(modePanel, "Active tool", "", new Vector2(14, -7), new Vector2(672, 25), 19, TextAnchor.MiddleCenter, ink, true);
        instructions = Label(modePanel, "Controls", "", new Vector2(14, -34), new Vector2(672, 22), 15, TextAnchor.MiddleCenter, muted);
        contextPanel = Panel("Context", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -88), new Vector2(600, 112), new Vector2(0.5f, 0.5f), Color.clear);
        context = Label(contextPanel, "Interaction", "", new Vector2(16, -10), new Vector2(568, 92), 19, TextAnchor.MiddleCenter);
        feedbackPanel = Panel("Feedback", root.transform, new Vector2(0.5f, 1), new Vector2(0, -122), new Vector2(540, 74), new Vector2(0.5f, 1), Color.clear);
        feedback = Label(feedbackPanel, "Notice", "", new Vector2(14, -7), new Vector2(512, 60), 18, TextAnchor.MiddleCenter, accent);
        recoveryPanel = Panel("Recovery", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(470, 76), new Vector2(0.5f, 0.5f), Color.clear);
        recovery = Label(recoveryPanel, "Recovery status", "", new Vector2(16, -10), new Vector2(438, 56), 22, TextAnchor.MiddleCenter, ink, true);
        var aim = Panel("Aim", root.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24, 24), new Vector2(0.5f, 0.5f), Color.clear);
        Color aimColor = new Color(0.96f, 1f, 0.98f, 0.9f);
        Strip(aim, "Top", Vector2.one * 0.5f, new Vector2(0, 8), new Vector2(2, 5), Vector2.one * 0.5f, aimColor);
        Strip(aim, "Bottom", Vector2.one * 0.5f, new Vector2(0, -8), new Vector2(2, 5), Vector2.one * 0.5f, aimColor);
        Strip(aim, "Left", Vector2.one * 0.5f, new Vector2(-8, 0), new Vector2(5, 2), Vector2.one * 0.5f, aimColor);
        Strip(aim, "Right", Vector2.one * 0.5f, new Vector2(8, 0), new Vector2(5, 2), Vector2.one * 0.5f, aimColor);
        Strip(aim, "Dot", Vector2.one * 0.5f, Vector2.zero, new Vector2(2, 2), Vector2.one * 0.5f, aimColor);
        ApplyLayout();
        if (weapons != null) weapons.Changed += OnInventoryChanged;
        Refresh();
    }

    private void Update()
    {
        if (canvas == null) return;
        canvas.enabled = !EcoMarketUI.IsOpen;
        if (lastWidth != DisplayWidth || lastHeight != DisplayHeight) ApplyLayout();
        if (weapons == null) return;
        bool building = weapons.Builder != null && weapons.Builder.IsBuilding;
        if (buildingLayout != building) ApplyLayout();
        int selected = building ? weapons.Builder.Selection % 5 : (int)weapons.Selected;
        for (int i = 0; i < slots.Length; i++)
        {
            bool active = selected == i;
            slots[i].color = active ? new Color(0.1f, 0.44f, 0.31f, 0.98f) : surface;
            markers[i].enabled = active; slotTitles[i].color = active ? Color.white : ink;
        }
        if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + 0.1f; Refresh(); }
    }

    private void OnInventoryChanged() { objectiveAt = 0f; Refresh(); }

    private void Refresh()
    {
        if (weapons == null || canvas == null) return;
        worldClock = EcoWorldClock.Current;
        if (worldClock != null) weather.text = "Gün " + worldClock.day + "  ·  " + Mathf.FloorToInt(worldClock.hour).ToString("00") + ":"
            + Mathf.FloorToInt(worldClock.hour % 1f * 60f).ToString("00") + "  ·  " + worldClock.WeatherName + "\nRüzgâr %" + Mathf.RoundToInt(worldClock.windStrength * 100);
        float[] values = { vitals != null ? vitals.Energy : 100, weapons.Energy, weapons.Water };
        float[] maxima = { vitals != null ? vitals.maximumEnergy : 100, weapons.maximumEnergy, weapons.maximumWater };
        barLabels[2].text = weapons.HasDirtyWater ? "Kirli su" : "Temiz su";
        for (int i = 0; i < fills.Length; i++)
        {
            fills[i].rectTransform.sizeDelta = new Vector2(tracks[i].sizeDelta.x * Mathf.Clamp01(values[i] / Mathf.Max(1, maxima[i])), 5);
            barValues[i].text = Mathf.CeilToInt(values[i]) + " / " + Mathf.CeilToInt(maxima[i]);
        }
        fills[0].color = vitals != null && vitals.IsRecovering ? new Color(1f, 0.62f, 0.32f) : meterColors[0];
        fills[2].color = weapons.HasDirtyWater ? new Color(0.8f, 0.66f, 0.33f) : meterColors[2];
        inventory.text = "Metal " + weapons.Metal + "  ·  Plastik " + weapons.Plastic + "\nÜrün " + weapons.Food + "  ·  Gübre " + weapons.Compost
            + "\nBalık " + weapons.CleanFish + " temiz / " + weapons.DirtyFish + " kirli"
            + "\nFiltre " + weapons.SmallFilters + " küçük / " + weapons.LargeFilters + " büyük"
            + "\nHam atık " + (weapons.CollectedMetalScrap + weapons.CollectedPlasticScrap) + "  ·  Organik " + weapons.CollectedOrganicWaste;
        if (weapons.Progression != null) inventory.text += "\nSeviye " + weapons.Progression.Level + "  ·  "
            + (weapons.Progression.Level == EcoProgression.MaximumLevel ? "En yüksek seviye" : "YEP " + weapons.Progression.LevelProgress + " / " + weapons.Progression.LevelRequirement);
        slotCounts[0].text = Mathf.CeilToInt(weapons.Water) + " su";
        slotCounts[1].text = weapons.MetalMode ? weapons.MetalAmmo + " demir top" : weapons.SeedCount(weapons.SelectedSeed) + " tohum";
        slotCounts[2].text = (weapons.CollectedMetalScrap + weapons.CollectedPlasticScrap + weapons.CollectedOrganicWaste) + " atık";
        slotCounts[3].text = "Analiz"; slotCounts[4].text = "Etkileşim";
        string[] modes = { "Toplama", "Anti-vakum", "Su" };
        mode.text = names[(int)weapons.Selected] + (weapons.Selected == PlayerWeaponSystem.Tool.Vacuum ? "  ·  " + modes[(int)weapons.Mode]
            : weapons.Selected == PlayerWeaponSystem.Tool.SeedGun ? "  ·  " + (weapons.MetalMode ? "Demir top" : EcoCropCatalog.Get(weapons.SelectedSeed).Name + " · " + EcoCropCatalog.Get(weapons.SelectedSeed).Climate) : "");
        instructions.text = weapons.Selected == PlayerWeaponSystem.Tool.Hands ? "[E] Etkileşim   ·   [1–5 / Tekerlek] Seç"
            : weapons.Selected == PlayerWeaponSystem.Tool.Scanner ? "[Sol tık] Tara   ·   [1–5 / Tekerlek] Seç"
            : weapons.Selected == PlayerWeaponSystem.Tool.Recycler ? "[Sol tık] Geri dönüştür   ·   [R] İşle / gübrele"
            : "[Sol tık] Kullan   ·   [Q] Mod değiştir" + (weapons.Selected == PlayerWeaponSystem.Tool.SeedGun ? (weapons.MetalMode ? "   ·   Mühimmat: Market" : "   ·   [R] Tohum türü") : "");
        context.text = weapons.Selected == PlayerWeaponSystem.Tool.Scanner && !string.IsNullOrEmpty(weapons.LastScan) ? weapons.LastScan : weapons.ContextHint();
        bool building = weapons.Builder != null && weapons.Builder.IsBuilding;
        for (int i = 0; i < slots.Length; i++)
        {
            slotTitles[i].text = building ? weapons.Builder.SlotName(i) : names[i];
            if (building) slotCounts[i].text = weapons.Builder.SlotCost(i);
        }
        if (building)
        {
            mode.text = "İNŞA  ·  " + weapons.Builder.SelectedName + "  ·  " + (weapons.Builder.Page + 1) + "/" + weapons.Builder.PageCount;
            instructions.text = "[Tab] Sayfa   ·   [1–5] Seç   ·   [Sol tık] Kur   ·   [R] Döndür   ·   [B] Çık";
            context.text = weapons.Builder.Status;
        }
        else instructions.text += "   ·   [B] İnşa";
        SetVisible(contextPanel, !string.IsNullOrEmpty(context.text));
        var chargerPower = weapons.ChargingStation != null ? weapons.ChargingStation.GetComponent<EcoPowerNode>() : null;
        feedback.text = weapons.ChargingStation != null
            ? (chargerPower != null && chargerPower.DeliveredPower > 0f ? "Silah bataryası şarj oluyor…" : "Şarj bağlı · Elektrik bekleniyor…")
            : Time.time < weapons.FeedbackUntil ? weapons.Feedback : "";
        SetVisible(feedbackPanel, !string.IsNullOrEmpty(feedback.text));
        bool recovering = vitals != null && vitals.IsRecovering;
        recovery.text = recovering ? "ENERJİ TOPLANIYOR\n" + vitals.RecoveryRemaining.ToString("0.0") + " saniye" : "";
        SetVisible(recoveryPanel, recovering);
        // Poll external world changes twice a second; harvesting/planting refresh immediately.
        if (Time.unscaledTime >= objectiveAt) { objectiveAt = Time.unscaledTime + 0.5f; RefreshObjective(); }
    }

    private void RefreshObjective()
    {
        var enemies = FindObjectsByType<EnemyBrain>(FindObjectsSortMode.None); int alive = 0;
        for (int i = 0; i < enemies.Length; i++) if (enemies[i] != null && enemies[i].State != EnemyBrain.Behaviour.Dead && enemies[i].Health > 0f) alive++;
        if (alive > 0) objective.text = "Alanı temizle · Kalan düşman: " + alive;
        else
        {
            var crops = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None); bool mature = false, living = false;
            for (int i = 0; i < crops.Length; i++)
            {
                if (crops[i] == null || crops[i].IsWithered) continue;
                living = true; if (crops[i].IsMature) mature = true;
            }
            objective.text = weapons.Food > 0 ? "Görev tamamlandı · Ekosistemi koru" : !living ? "Tohum ekerek çevreyi canlandır"
                : mature ? "Olgun bitkiyi hasat et" : "Bitkini vakumun su moduyla sula";
        }
    }

    private void ApplyLayout()
    {
        lastWidth = DisplayWidth; lastHeight = DisplayHeight;
        // Keep labels readable at 800x600 while allowing high-resolution displays to grow
        // gently. The whole HUD is at scene root, independent of camera and weapon sway.
        float scale = Mathf.Clamp(Mathf.Min(lastWidth / 1600f, lastHeight / 900f), 0.8f, 1.5f);
        scaler.scaleFactor = scale; canvas.scaleFactor = scale;
        float width = lastWidth / scale, height = lastHeight / scale;
        const float margin = 18f;
        bool compact = width < 1250f;
        float vitalsWidth = compact ? 266 : 296, resourcesWidth = compact ? 248 : 274;
        Place(vitalsPanel, new Vector2(margin, -margin), new Vector2(vitalsWidth, 202));
        Place(resourcesPanel, new Vector2(-margin, -margin), new Vector2(resourcesWidth, 188));
        LayoutText(inventory, new Vector2(14, -39), new Vector2(resourcesWidth - 28, 143));
        for (int i = 0; i < tracks.Length; i++)
        {
            float meterWidth = vitalsWidth - 32, y = -38 - i * 36;
            tracks[i].sizeDelta = new Vector2(meterWidth, 5);
            LayoutText(barLabels[i], new Vector2(16, y), new Vector2(meterWidth - 88, 22));
            LayoutText(barValues[i], new Vector2(16 + meterWidth - 88, y), new Vector2(88, 22));
        }
        LayoutText(weather, new Vector2(16, -150), new Vector2(vitalsWidth - 32, 44));
        float objectiveWidth = Mathf.Min(416, width - vitalsWidth - resourcesWidth - 4 * margin);
        float objectiveCenter = (vitalsWidth - resourcesWidth) * 0.5f;
        Place(objectivePanel, new Vector2(objectiveCenter, -margin), new Vector2(objectiveWidth, 92));
        LayoutText(objective, new Vector2(16, -32), new Vector2(objectiveWidth - 32, 54));
        float dockWidth = Mathf.Min(700, width - 2 * margin);
        Place(dock, new Vector2(0, margin), new Vector2(dockWidth, 96));
        float slotWidth = (dockWidth - 40) / 5;
        for (int i = 0; i < slots.Length; i++)
        {
            Place(slotRects[i], new Vector2(i * (slotWidth + 10), 0), new Vector2(slotWidth, 96));
            markers[i].rectTransform.sizeDelta = new Vector2(slotWidth, 3);
            LayoutText(slotTitles[i], new Vector2(8, -25), new Vector2(slotWidth - 16, 40));
            LayoutText(slotCounts[i], new Vector2(8, -69), new Vector2(slotWidth - 16, 21));
        }
        buildingLayout = weapons != null && weapons.Builder != null && weapons.Builder.IsBuilding;
        float modeHeight = compact && buildingLayout ? 80 : 62;
        Place(modePanel, new Vector2(0, margin + 108), new Vector2(dockWidth, modeHeight));
        LayoutText(mode, new Vector2(14, -7), new Vector2(dockWidth - 28, 25));
        LayoutText(instructions, new Vector2(14, -34), new Vector2(dockWidth - 28, modeHeight - 40));
        float contextWidth = Mathf.Min(600, width - 2 * margin);
        float contextY = Mathf.Max(-88, margin + 108 + modeHeight + 18 + 56 - height * 0.5f);
        Place(contextPanel, new Vector2(0, contextY), new Vector2(contextWidth, 112));
        LayoutText(context, new Vector2(16, -10), new Vector2(contextWidth - 32, 92));
        float feedbackWidth = Mathf.Min(540, objectiveWidth + 80);
        Place(feedbackPanel, new Vector2(objectiveCenter, -122), new Vector2(feedbackWidth, 74));
        LayoutText(feedback, new Vector2(14, -7), new Vector2(feedbackWidth - 28, 60));
        Place(recoveryPanel, new Vector2(0, 80), new Vector2(Mathf.Min(470, width - 2 * margin), 76));
        LayoutText(recovery, new Vector2(16, -10), new Vector2(recoveryPanel.sizeDelta.x - 32, 56));
    }

    // Camera-space previews and split displays may have a different output size from
    // Screen. Overlay canvases report the ordinary game-view size through pixelRect.
    private int DisplayWidth => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.width));
    private int DisplayHeight => Mathf.Max(1, Mathf.RoundToInt(canvas.pixelRect.height));

    private void SetVisible(RectTransform panel, bool visible) => panel.GetComponent<Image>().color = visible ? surface : Color.clear;
    private static void Place(RectTransform rect, Vector2 position, Vector2 size) { rect.anchoredPosition = position; rect.sizeDelta = size; }

    private RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color? tint = null)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; Place(rect, position, size);
        var image = go.GetComponent<Image>(); image.sprite = panelSprite; image.type = Image.Type.Sliced;
        image.color = tint ?? surface; image.raycastTarget = false; return rect;
    }

    private Image Strip(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color)
    {
        var image = Panel(name, parent, anchor, position, size, pivot, color).GetComponent<Image>();
        image.sprite = null; image.type = Image.Type.Simple; return image;
    }

    private Text Label(Transform parent, string name, string content, Vector2 position, Vector2 size, int fontSize, TextAnchor align, Color? color = null, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.layer = 5;
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        var text = go.GetComponent<Text>(); LayoutText(text, position, size);
        text.font = font; text.fontSize = fontSize * RasterScale; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color ?? ink; text.alignment = align; text.text = content; text.raycastTarget = false; text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
    }

    private static void LayoutText(Text text, Vector2 position, Vector2 size)
    {
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = size * RasterScale;
        text.rectTransform.localScale = Vector3.one / RasterScale;
    }

    private void OnDestroy()
    {
        if (weapons != null) weapons.Changed -= OnInventoryChanged;
        if (canvas != null) Destroy(canvas.gameObject);
        if (vitals != null) vitals.showLegacyHUD = previousLegacyHud;
    }
}
