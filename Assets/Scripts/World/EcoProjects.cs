using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Six permanent achievements; conditions must continuously hold for 120 simulation seconds.</summary>
public sealed class EcoProjects : MonoBehaviour
{
    public const float VerifySeconds = 120f;
    public const int RewardYep = 40;
    public static EcoProjects Current { get; private set; }
    public delegate bool Check(out string status);
    public sealed class Project
    {
        public string name, concept, goal, fact, symptom, cause, action, status = "";
        public float held;
        public bool done, met;
        public Check check;
    }
    private readonly List<Project> projects = new List<Project>();
    public IReadOnlyList<Project> All => projects;
    public int CompletedCount => projects.Count(p => p.done);
    public float Readiness => projects.Count == 0 ? 0f : (float)CompletedCount / projects.Count;
    public bool PanelOpen { get; private set; }
    public float ChargedEnergy { get; set; }
    public bool Charged { get => ChargedEnergy >= 10f; set { if (value) ChargedEnergy = Mathf.Max(10f, ChargedEnergy); else ChargedEnergy = 0; } }
    public bool WasBurning { get; set; }
    public int SelectedIndex { get; set; }
    public int[] HintLevels { get; private set; } = new int[6];
    public Vector3 HintPosition { get; private set; }
    public float HintUntil { get; private set; }
    public event Action<int> Completed;
    private readonly Dictionary<int, float> chargeObserved = new Dictionary<int, float>();
    private float accumulated, nextScan, scanSeconds;
    private PlayerWeaponSystem player;
    private EcoRegion[] regions = Array.Empty<EcoRegion>();
    private EcoPowerNode[] power = Array.Empty<EcoPowerNode>();
    private EcoWaterNode[] water = Array.Empty<EcoWaterNode>();
    private Vector2 scroll;
    private string cardTitle = "", cardText = "";
    private float cardUntil;
    private bool showLearningCard;
    private GUIStyle journalHeading, journalFootnote;
    private readonly List<JournalRow> journalRows = new List<JournalRow>(6);
    private struct JournalRow
    {
        public float heading, goal, status, hint, height;
        public bool stackedActions;
    }

    private void Awake()
    {
        Current = this;
        if (GetComponent<EcoDiscoveries>() == null) gameObject.AddComponent<EcoDiscoveries>();
        if (GetComponent<EcoStory>() == null) gameObject.AddComponent<EcoStory>();
        if (FindFirstObjectByType<EcoGameMenu>() == null) gameObject.AddComponent<EcoGameMenu>();
        Add("Mahalleye ilk enerji", "Üretim, kablo ve şarj",
            "Üretici–batarya–şarj aynı ağda; toplam 10 EB gerçek şarj ve güncel besleme.",
            "Araç elektriği kısıtlı; hazırlık için yerel bir şebeke gerekiyor.",
            "Üretim, depolama ve tüketim ayrı parçalardır.",
            "Güneş/rüzgâr üreticisini batarya ve şarj istasyonuna kabloyla bağla.",
            "Panel üretir, batarya fazlayı saklar, kablo taşır. Güneşsiz zamanda üretim ve depo aynı şey değildir.", EnergyCheck);
        Add("Gölü canlandır", "Kaynak müdahalesi ve arıtma",
            "Doğu gölünün girişi kapalı, kendi pompa–arıtma–deposu çalışıyor; su kalitesi en az 70.",
            "Doğu kıyısında tortu ve azalan balık görülüyor.",
            "Sürekli kirli giriş varken yalnız su toplamak sorunu çözmez.",
            "Giriş sızıntısını kapat; gölden pompa→kirli boru→arıtıcı→temiz boru→depo kur ve göle dönüşü aç.",
            "Kaynağı azaltma ile arıtma birbirini tamamlar. Depolanan suyu aynı anda göle dönmüş sayamayız.", LakeCheck);
        Add("Koruluğu toparla", "Bitki örtüsü ve habitat",
            "Yerel bitki örtüsü en az %60, habitat %50; yangın yok ve sağlıklı ekim var.",
            "Korulukta sessizlik, zarar görmüş ağaçlar ve seyrek örtü var.",
            "Yangın ve kirli toprak yeni canlıların yerleşmesini zorlaştırır.",
            "Koruluk/rüzgârlı yükseltide mevcut yangını söndür, uygun sağlıklı ağaç dik ve bakımını sürdür.",
            "Habitat yalnız ağaç adedi değildir. Uygun örtü, su ve temiz toprak kuşları ve kelebekleri geri getirir.", GroveCheck);
        Add("Sanayiyi dönüştür", "Filtre ve temiz enerji",
            "Yerel fabrikaların filtreleri ve enerji dönüşümü etkin; hava kalitesi en az 70.",
            "Baca dumanı görüşü ve yakın bitkileri etkiliyor.",
            "Parçacık filtresi karbonu ortadan kaldırmaz; yakıt dönüşümü ayrıca gerekir.",
            "Sanayide büyük filtre ve dönüşüm paketi uygula; çalışan temiz şebekeye bağla.",
            "Hava filtresi ve karbon azaltımı farklıdır. Temiz enerjiye dönüşen fabrika enerjisizken kirli yakıta dönmez.", IndustryCheck);
        Add("Yerleşimi dayanıklı yap", "Kesintide hizmet ve rezerv",
            "Bir üretici kapalıyken aynı ağdaki pompa, arıtma ve şarj çalışabilir; 120 sn su/elektrik rezervi.",
            "Hava değişince tek üretim kaynağına bağlı hizmetler durabilir.",
            "Kapasite etiketi dolu depo anlamına gelmez; bağlı rezerv ve debi gerekir.",
            "Bir üreticiyi kontrol panelinden kapat; yedek üretim/batarya ve temiz depo ile hizmeti sürdür.",
            "Yenilenebilir üretim hava koşullarına bağlıdır. Güvenilir başka kaynak ve rezerv birlikte dayanıklılık sağlar.", ResilienceCheck);
        Add("Buzulu koru", "Dünya karbonu ve ölçüm",
            "İzleme istasyonu etkin; alan ağırlıklı karbon en çok 20 ve erime artmıyor.",
            "Buzuldaki ölçüm çizgisi geri çekiliyor.",
            "İstasyon ölçer; karbonu kendiliğinden düşürmez.",
            "Buzul istasyonunu etkinleştir; emisyon dönüşümü ve habitatla dünya karbonunu azalt.",
            "Yerel çevre kararları birlikte iklim baskısını değiştirir. Bu endeksler oyunun stilize modelidir.", GlacierCheck);
    }
    private void Add(string name, string concept, string goal, string symptom, string cause, string action, string fact, Check check)
    { projects.Add(new Project { name = name, concept = concept, goal = goal, symptom = symptom, cause = cause, action = action, fact = fact, check = check }); }
    private void OnDestroy() { if (Current == this) Current = null; EcoUIFlow.SetPanel(this, false); }
    private void OnDisable() { PanelOpen = false; EcoUIFlow.SetPanel(this, false); }
    public void SetOpen(bool open) { PanelOpen = open; EcoUIFlow.SetPanel(this, open); }
    public void RestoreHints(int selected, int[] hints)
    {
        SelectedIndex = Mathf.Clamp(selected, 0, 5); HintLevels = new int[6];
        if (hints != null) for (int i = 0; i < Mathf.Min(6, hints.Length); i++) HintLevels[i] = Mathf.Clamp(hints[i], 0, 3);
        HintUntil = 0;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && EcoKeys.Pressed(EcoKeys.Action.Journal) && !PlayerController.Paused) SetOpen(!PanelOpen);
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && PanelOpen) { EcoUIFlow.ConsumeEscape(); SetOpen(false); }
        if (PanelOpen && !EcoUIFlow.IsPanel(this)) PanelOpen = false;
        accumulated += Time.deltaTime;
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.5f; scanSeconds = accumulated; accumulated = 0;
        regions = FindObjectsByType<EcoRegion>(FindObjectsSortMode.None);
        power = FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None);
        water = FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None);
        if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
        ObserveCharge();
        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i]; project.met = project.check(out project.status);
            if (project.done) { player?.Progression?.AwardOnce("project." + i, RewardYep); continue; }
            project.held = project.met ? Mathf.Min(VerifySeconds, project.held + scanSeconds) : 0f;
            if (project.held < VerifySeconds) continue;
            project.done = true; showLearningCard = true; cardTitle = project.name; cardText = project.fact; cardUntil = Time.unscaledTime + 14f;
            player?.Progression?.AwardOnce("project." + i, RewardYep);
            player?.ShowFeedback("PROJE TAMAMLANDI • " + project.name + " • +40 YEP");
            if (i == 1) EcoDiscoveries.Remember("recipe.hydro");
            Completed?.Invoke(i);
        }
    }
    private void ObserveCharge()
    {
        foreach (var station in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None))
        {
            int id = station.GetInstanceID(); chargeObserved.TryGetValue(id, out float observed);
            ChargedEnergy = Mathf.Clamp(ChargedEnergy + Mathf.Max(0, station.TotalDeliveredEnergy - observed), 0f, 10f);
            chargeObserved[id] = station.TotalDeliveredEnergy;
        }
    }
    public void ResetChargeObservation() { chargeObserved.Clear(); foreach (var station in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None)) chargeObserved[station.GetInstanceID()] = station.TotalDeliveredEnergy; }
    private EcoRegion Region(string name) => regions.FirstOrDefault(r => r != null && r.regionName.Contains(name));
    public EcoRegion GroveRegion => Region("Koruluk") ?? regions.FirstOrDefault(r => r != null && r.TryGetComponent<EcoRegionInfo>(out var info) && info.kind == EcoRegionInfo.Kind.Wind)
        ?? Region("Rüzg");
    private static bool Generator(EcoPowerNode node) => node.kind == EcoPowerNode.Kind.Solar || node.kind == EcoPowerNode.Kind.Wind || node.kind == EcoPowerNode.Kind.Hydro;
    private static bool Supported(EcoPowerNode node, float demand) => node != null && node.Operational
        && (node.NetworkGeneration >= demand || node.NetworkStored >= Mathf.Max(1f, demand));

    private bool EnergyCheck(out string status)
    {
        foreach (var charger in power)
        {
            if (!charger.Operational || charger.kind != EcoPowerNode.Kind.Charger) continue;
            var circuit = EcoPowerGrid.ComponentOf(charger).ToArray();
            bool producer = circuit.Any(Generator), battery = circuit.Any(n => n.kind == EcoPowerNode.Kind.Battery);
            float rate = charger.TryGetComponent<EcoChargingStation>(out var station) ? station.chargeRate : 4f;
            if (!producer || !battery || !Supported(charger, rate)) continue;
            status = "Gerçek şarj " + ChargedEnergy.ToString("0.0") + "/10 EB • Ağ beslemesi hazır";
            return Charged;
        }
        status = "Üretici + batarya + şarj aynı çalışan ağda olmalı; boş rezerv yeterli değildir";
        return false;
    }
    private bool LakeCheck(out string status)
    {
        var lake = Region("Doğu Göl");
        if (lake == null) { status = "Doğu Gölü bulunamadı"; return false; }
        if (!EcoDiscoveries.InletRemediated(lake)) { status = "Doğu kıyısındaki kirletici girişi kapat " + EcoKeys.Tag(EcoKeys.Action.Interact) + ""; return false; }
        foreach (var intake in water)
        {
            if (!intake.Operational || intake.kind != EcoWaterNode.Kind.Intake || intake.SourceRegion != lake) continue;
            var inletPower = intake.GetComponent<EcoPowerNode>();
            foreach (var purifier in water)
            {
                if (!purifier.Operational || purifier.kind != EcoWaterNode.Kind.Purifier || !purifier.returnToLake || purifier.SourceRegion != lake) continue;
                if (!EcoWaterGrid.AreConnected(intake, purifier, EcoWaterNode.Fluid.Dirty)) continue;
                var purifierPower = purifier.GetComponent<EcoPowerNode>();
                if (!EcoPowerGrid.AreConnected(inletPower, purifierPower)
                    || !Supported(inletPower, intake.litresPerSecond * intake.electricityPerLitre)
                    || !Supported(purifierPower, purifier.litresPerSecond * purifier.electricityPerLitre)) continue;
                bool tank = water.Any(t => t.Operational && t.kind == EcoWaterNode.Kind.Tank && EcoWaterGrid.AreConnected(purifier, t, EcoWaterNode.Fluid.Clean));
                if (!tank || purifier.LastProcessedClean <= 0f) continue;
                status = "Doğu gölü su kalitesi " + (100f - lake.waterPollution).ToString("0") + "/70 • Yerel arıtma ve göle dönüş bağlı";
                return lake.waterPollution <= 30f;
            }
        }
        status = "Doğu gölü pompa→kirli hat→enerjili arıtma→temiz depo; aynı göle dönüş açık olmalı";
        return false;
    }
    private bool GroveCheck(out string status)
    {
        var grove = GroveRegion;
        if (grove == null) { status = "Koruluk/rüzgârlı yükselti bulunamadı"; return false; }
        bool burning = FindObjectsByType<EcoFire>(FindObjectsSortMode.None).Any(f => f.Burning && grove.Contains(f.transform.position));
        if (burning) WasBurning = true;
        float habitat = grove.TryGetComponent<EcoHabitat>(out var wildlife) ? wildlife.recovery : 0f;
        bool planting = FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None).Any(p => grove.Contains(p.transform.position)
            && p.kind == EcoCropCatalog.Kind.Tree && !p.IsWithered && !p.contaminated && p.health >= 0.5f && p.moisture >= 0.2f);
        status = "Örtü %" + (grove.vegetation * 100f).ToString("0") + "/60 • Habitat %" + (habitat * 100f).ToString("0") + "/50"
            + (burning ? " • Yerel yangın var" : " • Yangın yok") + (planting ? " • Sağlıklı ekim var" : " • Sağlıklı ağaç ekimi gerekli");
        return !burning && grove.vegetation >= 0.6f && habitat >= 0.5f && planting;
    }
    private bool IndustryCheck(out string status)
    {
        var region = Region("Sanayi");
        if (region == null) { status = "Sanayi bölgesi bulunamadı"; return false; }
        var factories = FindObjectsByType<EcoFactory>(FindObjectsSortMode.None).Where(f => region.Contains(f.transform.position)).ToArray();
        int ready = factories.Count(f => f.Filtered && f.Converted && Supported(f.GetComponent<EcoPowerNode>(), 2f));
        status = "Filtre ve temiz ağ " + ready + "/" + factories.Length + " • Hava kalitesi " + (100f - region.airPollution).ToString("0") + "/70";
        return factories.Length > 0 && ready == factories.Length && region.airPollution <= 30f;
    }
    private bool ResilienceCheck(out string status)
    {
        float remaining = Mathf.Max(0.5f, VerifySeconds - projects[4].held);
        foreach (var charger in power)
        {
            if (!charger.Operational || charger.kind != EcoPowerNode.Kind.Charger) continue;
            var circuit = EcoPowerGrid.ComponentOf(charger).ToArray();
            if (!circuit.Any(n => Generator(n) && n.GenerationIsDisabled)) continue;
            var pumps = circuit.Select(n => n.GetComponent<EcoWaterNode>()).Where(n => n != null && n.Operational).ToArray();
            var intake = pumps.FirstOrDefault(n => n.kind == EcoWaterNode.Kind.Intake);
            var purifier = pumps.FirstOrDefault(n => n.kind == EcoWaterNode.Kind.Purifier && intake != null && EcoWaterGrid.AreConnected(intake, n, EcoWaterNode.Fluid.Dirty));
            if (intake == null || purifier == null) continue;
            var tanks = water.Where(t => t.Operational && t.kind == EcoWaterNode.Kind.Tank && EcoWaterGrid.AreConnected(purifier, t, EcoWaterNode.Fluid.Clean)).ToArray();
            if (tanks.Length == 0) continue;
            float demand = (charger.TryGetComponent<EcoChargingStation>(out var station) ? station.chargeRate : 4f)
                + intake.litresPerSecond * intake.electricityPerLitre + purifier.litresPerSecond * purifier.electricityPerLitre;
            float generated = circuit.Sum(n => n.Generation(EcoWorldClock.Current));
            float deficit = Mathf.Max(0, demand - generated);
            float stored = circuit.Where(n => n.kind == EcoPowerNode.Kind.Battery).Sum(n => n.storedEnergy);
            float dischargeRate = circuit.Where(n => n.kind == EcoPowerNode.Kind.Battery && n.storedEnergy > 0).Sum(n => n.batteryRate);
            // A modest essential water load is checked against remaining verified time; real farm use can be larger.
            float waterDemand = 0.1f;
            foreach (var farm in FindObjectsByType<EcoFarm>(FindObjectsSortMode.None))
                if (farm.HasCrops && tanks.Any(t => EcoWaterGrid.AreConnected(t, farm.GetComponent<EcoWaterNode>(), EcoWaterNode.Fluid.Clean)))
                    waterDemand += 0.1f;
            float clean = tanks.Sum(t => t.cleanWater);
            bool enough = stored + 0.01f >= deficit * remaining && generated + dischargeRate + 0.01f >= demand && clean + 0.01f >= waterDemand * remaining;
            bool service = Supported(intake.GetComponent<EcoPowerNode>(), intake.litresPerSecond * intake.electricityPerLitre)
                && Supported(purifier.GetComponent<EcoPowerNode>(), purifier.litresPerSecond * purifier.electricityPerLitre);
            status = "Kesintide ağ: " + generated.ToString("0.0") + "/" + demand.ToString("0.0") + " EB/sn • Rezerv "
                + stored.ToString("0") + "/" + (deficit * remaining).ToString("0") + " EB, "
                + clean.ToString("0") + "/" + (waterDemand * remaining).ToString("0") + " SB";
            if (enough && service) return true;
        }
        status = "Bir üreticiyi kapat; aynı ağda pompa/arıtma/şarj ile 120 sn elektrik ve temiz su rezervini koru";
        return false;
    }
    private bool GlacierCheck(out string status)
    {
        var station = EcoGlacierStation.Current;
        var melt = FindFirstObjectByType<EcoGlacierMelt>();
        if (station == null || !station.Active) { status = "Buzul istasyonunu keşfet, kur ve etkinleştir " + EcoKeys.Tag(EcoKeys.Action.Interact) + ""; return false; }
        status = "Alan ağırlıklı karbon " + station.AverageCarbon.ToString("0.0") + "/20 • Erime "
            + (melt == null ? "ölçülmüyor" : melt.MeltRate > 0.0001f ? "artıyor" : "artmıyor");
        return station.Safe && melt != null && melt.MeltRate <= 0.0001f;
    }
    public string RequestHint(int index)
    {
        index = Mathf.Clamp(index, 0, projects.Count - 1); SelectedIndex = index;
        HintLevels[index] = Mathf.Clamp(HintLevels[index] + 1, 1, 3);
        var project = projects[index];
        string text = HintLevels[index] == 1 ? project.symptom : HintLevels[index] == 2 ? project.cause : project.action;
        if (HintLevels[index] == 3)
        {
            var target = index == 1 ? Region("Doğu Göl") : index == 2 ? GroveRegion : index == 3 ? Region("Sanayi")
                : index == 5 ? Region("Buzul") : Region("Güneş") ?? Region("Şehir");
            if (target != null) { HintPosition = target.transform.position; HintUntil = Time.unscaledTime + 18f; }
        }
        player?.ShowFeedback(text); return text;
    }
    private static readonly string[] ProjectIcons = { "electricity", "region_lake_dirty", "poi_tree", "poi_factory", "ui_layers", "region_glacier" };

    private void OnGUI()
    {
        if (!EcoUI.ForeignWindowOpen && !EcoWorldHUD.MapOpen && Time.unscaledTime < HintUntil && Camera.main != null)
        {
            Vector3 point = Camera.main.WorldToScreenPoint(HintPosition + Vector3.up * 3f);
            if (point.z > 0)
            {
                EcoTheme.Begin();
                float s = Screen.width / EcoTheme.Width;
                var box = new Rect(Mathf.Clamp(point.x / s - 140, 12, EcoTheme.Width - 292), Mathf.Clamp((Screen.height - point.y) / s, 12, EcoTheme.Height - 76), 280, 64);
                GUI.Box(box, "", EcoTheme.Card);
                EcoTheme.Icon(new Rect(box.x + 20, box.y + 18, 28, 28), "poi_quest");
                GUI.Label(new Rect(box.x + 60, box.y + 20, box.width - 82, 26), "İpucu: çalışma alanı", EcoTheme.Muted);
                EcoTheme.End();
            }
        }
        if (showLearningCard && Time.unscaledTime < cardUntil && !EcoUI.ForeignWindowOpen && !EcoWorldHUD.MapOpen)
        {
            EcoTheme.Begin();
            PrepareJournalStyles();
            float width = Mathf.Min(520, EcoTheme.Width - 36);
            string heading = cardTitle + " · Biliyor muydun?";
            float titleHeight = TextHeight(journalHeading, heading, width - 150, 28);
            float bodyHeight = TextHeight(EcoTheme.Muted, cardText, width - 100, 36);
            float height = 22 + titleHeight + 8 + bodyHeight + 22;
            var card = new Rect(18, Mathf.Max(112, EcoTheme.Height - 156 - height), width, height);
            GUI.Box(card, "", EcoTheme.Card);
            EcoTheme.Icon(new Rect(card.x + 22, card.y + 24, 36, 36), "yep");
            GUI.Label(new Rect(card.x + 76, card.y + 22, card.width - 150, titleHeight), heading, journalHeading);
            var close = new Rect(card.xMax - 54, card.y + 22, 28, 28);
            if (GUI.Button(close, GUIContent.none, EcoTheme.Button)) showLearningCard = false;
            EcoTheme.Icon(new Rect(close.x + 3, close.y + 3, 22, 22), "ui_close");
            GUI.Label(new Rect(card.x + 76, card.y + 22 + titleHeight + 8, card.width - 100, bodyHeight), cardText, EcoTheme.Muted);
            EcoTheme.End();
        }
        if (!PanelOpen) return;
        EcoTheme.Begin();
        var panel = EcoTheme.Window(940f, 720f, "PROJE GÜNLÜĞÜ",
            CompletedCount + "/6 proje tamamlandı  •  Final hazırlığı %" + Mathf.RoundToInt(Readiness * 100), "ui_journal", 48f);
        if (EcoTheme.CloseButton(panel)) SetOpen(false);
        DrawJournalContents(panel);
        EcoTheme.End();
    }

    private void PrepareJournalStyles()
    {
        if (journalHeading != null) return;
        journalHeading = new GUIStyle(EcoTheme.Subtitle)
        {
            fontSize = 20, wordWrap = true, clipping = TextClipping.Clip,
            padding = new RectOffset(), margin = new RectOffset(), alignment = TextAnchor.UpperLeft
        };
        journalFootnote = new GUIStyle(EcoTheme.Muted) { fontSize = 14 };
    }

    private static float TextHeight(GUIStyle style, string text, float width, float minimum)
        => Mathf.Max(minimum, style.CalcHeight(new GUIContent(text ?? ""), Mathf.Max(40, width)));

    private JournalRow MeasureRow(Project project, int index, float width)
    {
        float textWidth = Mathf.Max(80, width - 100);
        var row = new JournalRow
        {
            heading = TextHeight(journalHeading, project.name + " · " + project.concept, textWidth, 26),
            goal = TextHeight(EcoTheme.Body, project.goal, textWidth, 26),
            status = TextHeight(EcoTheme.Muted, ProjectStatus(project), textWidth, 22),
            hint = HintLevels[index] > 0 ? TextHeight(EcoTheme.Muted, ProjectHint(project, index), textWidth, 22) : 0,
            stackedActions = textWidth < 340
        };
        row.height = 22 + row.heading + 8 + row.goal + 10 + row.status + 14 + 42
            + (row.stackedActions ? 44 : 0) + (row.hint > 0 ? row.hint + 12 : 0) + 22;
        return row;
    }

    private static string ProjectStatus(Project project)
        => (project.done ? "Başarı korundu · Güncel: " : "") + project.status;

    private string ProjectHint(Project project, int index)
        => HintLevels[index] == 1 ? project.symptom : HintLevels[index] == 2 ? project.cause : project.action;

    private void DrawJournalContents(Rect panel)
    {
        PrepareJournalStyles();
        const string footnote = "Koşulu 120 sn koru • Proje sırası serbest • Başarı kalıcı • Günlük açıkken dünya %10 hızda";
        float footerHeight = TextHeight(journalFootnote, footnote, panel.width - 72, 24);
        var viewport = new Rect(panel.x + 32, panel.y + 114, panel.width - 64, panel.height - 158 - footerHeight);
        float width = viewport.width - 22, total = 0;
        journalRows.Clear();
        for (int i = 0; i < projects.Count; i++)
        {
            var row = MeasureRow(projects[i], i, width); journalRows.Add(row); total += row.height + 12;
        }
        scroll.x = 0;
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, width, Mathf.Max(1, total - 12)), false, true);
        float top = 0;
        for (int i = 0; i < projects.Count; i++)
        {
            var project = projects[i]; var row = journalRows[i];
            GUI.Box(new Rect(0, top, width, row.height), GUIContent.none, EcoTheme.Card);
            if (SelectedIndex == i)
            {
                var previous = GUI.color; GUI.color = EcoSettings.Good;
                GUI.DrawTexture(new Rect(18, top + 20, 3, row.height - 40), Texture2D.whiteTexture); GUI.color = previous;
            }
            EcoTheme.Icon(new Rect(26, top + 24, 36, 36), ProjectIcons[Mathf.Clamp(i, 0, ProjectIcons.Length - 1)]);
            if (project.done) EcoTheme.Icon(new Rect(48, top + 48, 20, 20), "ui_check");
            float x = 76, textWidth = width - 100, y = top + 22;
            if (GUI.Button(new Rect(x, y, textWidth, row.heading), project.name + " · " + project.concept, journalHeading)) SelectedIndex = i;
            y += row.heading + 8;
            GUI.Label(new Rect(x, y, textWidth, row.goal), project.goal, EcoTheme.Body); y += row.goal + 10;
            GUI.Label(new Rect(x, y, textWidth, row.status), ProjectStatus(project), EcoTheme.Muted); y += row.status + 14;
            float barWidth = row.stackedActions ? textWidth : textWidth - 166;
            DrawBar(new Rect(x, y + 3, barWidth, 8), project.done ? 1 : project.held / VerifySeconds);
            GUI.Label(new Rect(x, y + 16, barWidth, 24), project.done ? "40 YEP kazanıldı" : project.held.ToString("0") + " / 120 sn", EcoTheme.Muted);
            float buttonY = row.stackedActions ? y + 44 : y - 4;
            if (EcoTheme.IconButton(new Rect(width - 174, buttonY, 150, 36), "İpucu " + HintLevels[i] + "/3", "ui_help")) RequestHint(i);
            y += 42 + (row.stackedActions ? 44 : 0);
            if (row.hint > 0) GUI.Label(new Rect(x, y + 12, textWidth, row.hint), ProjectHint(project, i), EcoTheme.Muted);
            top += row.height + 12;
        }
        GUI.EndScrollView();
        GUI.Label(new Rect(panel.x + 36, panel.yMax - 30 - footerHeight, panel.width - 72, footerHeight), footnote, journalFootnote);
    }
    private static void DrawBar(Rect rect, float fill)
    {
        var previous = GUI.color; GUI.color = new Color(0.1f, 0.16f, 0.17f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = EcoSettings.Good; GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture); GUI.color = previous;
    }
}
