using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Demand side of sustainability (etik kurul formu: "enerji tasarrufu", "su tasarrufu"). The home in town draws a real
// load from the base battery (the neighbourhood grid of P1): fridge, devices on standby and evening lights. Four one-time
// measures at the home meter cut that load or the water used for irrigation. Savings are tallied so the player sees
// what they kept. Player-built water tanks can spring a leak that wastes clean water until it is fixed with [E].
// Values are stylised game units (EB, SB), not physical measurements. No YEP is granted: the 360 YEP economy is unchanged.
public sealed class EcoHomeSaving : MonoBehaviour
{
    public static EcoHomeSaving Current { get; private set; }

    // ---- saved by EcoSaveIdentity (public fields)
    public bool lightsOff, standbyOff, ledInstalled, dripIrrigation, completionShown;
    public float savedEnergy, savedWater;
    public int lastLeakDay;

    // ---- load model (EB per real second)
    public const float Fridge = 0.04f, Standby = 0.04f, Lighting = 0.24f;
    public const float BaselineNightLoad = Fridge + Standby + Lighting;
    public const int LedMetal = 4, LedPlastic = 2, DripMetal = 6, DripPlastic = 6, LeakRepairMetal = 1;
    private const float Reach = 2.6f;

    public static float WaterPerFullWatering => Current != null && Current.dripIrrigation ? 3f : 5f;
    public static float MoistureUse => Current != null && Current.dripIrrigation ? 0.75f : 1f;

    public bool LightsOn => clock != null && (clock.hour >= 19f || clock.hour < 6f);
    public float Load => LoadWith(lightsOff, standbyOff, ledInstalled);
    public float BaselineLoad => LoadWith(false, false, false);
    public float NightLoad => Fridge + (standbyOff ? 0f : Standby) + Lighting * (lightsOff ? 0.5f : 1f) * (ledInstalled ? 0.25f : 1f);
    /// <summary>The battery could not cover the home: the backup diesel generator runs and emits carbon in town.</summary>
    public bool GeneratorRunning { get; private set; }
    public float GeneratorEnergy { get; private set; }
    public const float GeneratorCarbonPerEB = 0.03f, GeneratorAirPerEB = 0.02f;
    public bool FreeMeasuresDone => lightsOff && standbyOff;
    public bool AllDone => lightsOff && standbyOff && ledInstalled && dripIrrigation;
    public int DoneCount => (lightsOff ? 1 : 0) + (standbyOff ? 1 : 0) + (ledInstalled ? 1 : 0) + (dripIrrigation ? 1 : 0);
    public Transform Meter => meter;
    public EcoPowerNode Battery => battery;
    public bool PanelOpen => open;

    private float LoadWith(bool lights, bool standby, bool led)
    {
        float load = Fridge + (standby ? 0f : Standby);
        if (LightsOn) load += Lighting * (lights ? 0.5f : 1f) * (led ? 0.25f : 1f);
        return load;
    }

    private EcoWorldClock clock;
    private EcoPowerNode battery;
    private PlayerWeaponSystem weapons;
    private Transform meter, house;
    private readonly List<Renderer> upperWindows = new List<Renderer>(), lowerWindows = new List<Renderer>();
    private MaterialPropertyBlock dark;
    private float nextScan;
    private bool open;
    private Vector2 scroll;
    private float cardUntil;
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }
    private void OnDisable() { if (open) SetOpen(false); }

    private void Update()
    {
        if (clock == null) clock = EcoWorldClock.Current;
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
            if (battery == null) battery = FindBaseBattery();
            if (meter == null) CreateMeter();
            CheckLeaks();
        }
        if (open)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { EcoUIFlow.ConsumeEscape(); SetOpen(false); }
            if (!EcoUIFlow.IsPanel(this)) open = false;
        }
        Simulate(Time.deltaTime);
    }

    /// <summary>Draws the home load from the base battery and tallies what the measures saved.</summary>
    public void Simulate(float seconds)
    {
        if (!EcoRegion.Valid(seconds) || clock == null) return;
        float load = Load;
        savedEnergy += Mathf.Max(0f, BaselineLoad - load) * seconds;
        if (battery == null || !battery.Operational) { GeneratorRunning = false; return; }
        float wanted = load * seconds, taken = Mathf.Min(wanted, Mathf.Max(0f, battery.storedEnergy));
        battery.storedEnergy -= taken;
        // What the battery cannot cover comes from a backup diesel generator: wasted energy turns into carbon (ayak izi).
        float missing = wanted - taken;
        GeneratorRunning = missing > wanted * 0.05f;
        if (missing > 0f)
        {
            GeneratorEnergy += missing;
            var town = EcoRegion.At(meter != null ? meter.position : transform.position);
            town?.AddPollution(missing * GeneratorAirPerEB, 0f, 0f, missing * GeneratorCarbonPerEB, EcoCarbonFootprint.Source.Generator);
        }
    }

    /// <summary>Clean water kept in the tank thanks to drip irrigation (called by the watering code).</summary>
    public static void CountWaterSaved(float fullWaterings)
    {
        if (Current != null && Current.dripIrrigation && fullWaterings > 0f) Current.savedWater += fullWaterings * 2f;
    }

    private EcoPowerNode FindBaseBattery()
    {
        EcoChargingStation charger = null; float best = float.MaxValue;
        foreach (var c in FindObjectsByType<EcoChargingStation>(FindObjectsSortMode.None))
        {
            var region = EcoRegion.At(c.transform.position);
            float score = region != null && region.Key == "Üs" ? 0f : 1f;
            if (score < best) { best = score; charger = c; }
        }
        EcoPowerNode result = null; best = float.MaxValue;
        Vector3 at = charger != null ? charger.transform.position : transform.position;
        foreach (var node in FindObjectsByType<EcoPowerNode>(FindObjectsSortMode.None))
        {
            if (node == null || node.kind != EcoPowerNode.Kind.Battery) continue;
            float d = (node.transform.position - at).sqrMagnitude;
            if (d < best) { best = d; result = node; }
        }
        return result;
    }

    public bool Feeds(EcoPowerNode node) => node != null && node == battery;

    // ------------------------------------------------------------------ world object

    private void CreateMeter()
    {
        var door = GameObject.Find("Eco Home door");
        if (door == null) return;
        house = door.transform.parent;
        if (house != null)
            foreach (Transform child in house)
            {
                var r = child.GetComponent<Renderer>();
                if (r == null) continue;
                if (child.name == "Window up") upperWindows.Add(r);
                else if (child.name == "Window") lowerWindows.Add(r);
            }
        Vector3 front = house != null ? Vector3.ProjectOnPlane(door.transform.position - house.position, Vector3.up).normalized : Vector3.forward;
        Vector3 side = house != null ? Vector3.ProjectOnPlane(house.right, Vector3.up).normalized : Vector3.right;
        Vector3 position = door.transform.position + side * 7.2f + front * 0.8f;
        if (Physics.Raycast(position + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point;

        var go = new GameObject("Ev sayacı");
        go.transform.position = position;
        go.transform.rotation = Quaternion.LookRotation(front + Vector3.forward * 1e-4f);
        var trigger = go.AddComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(0.7f, 1.6f, 0.5f); trigger.center = Vector3.up * 0.8f;
        EcoNavigation.Ignore(go);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader != null)
        {
            Material Paint(Color c) { var m = new Material(shader); m.color = c; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); return m; }
            void Part(PrimitiveType type, Vector3 at, Vector3 size, Material m)
            {
                var part = GameObject.CreatePrimitive(type); Destroy(part.GetComponent<Collider>());
                part.transform.SetParent(go.transform, false); part.transform.localPosition = at; part.transform.localScale = size;
                part.GetComponent<Renderer>().sharedMaterial = m;
            }
            var post = Paint(new Color(0.45f, 0.36f, 0.26f));
            var box = Paint(new Color(0.86f, 0.88f, 0.84f));
            var screen = Paint(new Color(0.25f, 0.72f, 0.42f));
            var drop = Paint(new Color(0.30f, 0.62f, 0.95f));
            Part(PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.12f, 1.1f, 0.12f), post);
            Part(PrimitiveType.Cube, new Vector3(0f, 1.25f, 0.02f), new Vector3(0.62f, 0.5f, 0.22f), box);
            Part(PrimitiveType.Cube, new Vector3(-0.13f, 1.3f, 0.14f), new Vector3(0.22f, 0.16f, 0.02f), screen);
            Part(PrimitiveType.Sphere, new Vector3(0.15f, 1.3f, 0.14f), new Vector3(0.14f, 0.14f, 0.04f), drop);
        }
        EcoMapMarker.Add(go, "electricity", "Ev sayacı", "Evin elektrik ve su tüketimi. Tasarruf önlemleri burada. Yanında " + EcoKeys.Tag(EcoKeys.Action.Interact) + ".", EcoMapMarker.Category.Quest, true);
        var label = new GameObject("Label", typeof(RectTransform), typeof(Canvas), typeof(WorldSpaceLabel));
        label.transform.SetParent(go.transform, false); label.transform.localPosition = new Vector3(0f, 2.1f, 0f);
        label.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var caption = new GameObject("Caption", typeof(RectTransform), typeof(UnityEngine.UI.Text));
        caption.transform.SetParent(label.transform, false);
        var text = caption.GetComponent<UnityEngine.UI.Text>();
        text.font = EcoUI.Font; text.color = Color.white; text.raycastTarget = false;
        text.text = "EV SAYACI\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Tasarruf";
        meter = go.transform;
    }

    // Windows follow the measures: with "lights off" only the downstairs room stays lit at night.
    private void LateUpdate()
    {
        if (!LightsOn || !lightsOff || upperWindows.Count == 0) return;
        dark ??= new MaterialPropertyBlock();
        Darken(upperWindows);
    }
    private void Darken(List<Renderer> list)
    {
        foreach (var r in list)
        {
            if (r == null) continue;
            r.GetPropertyBlock(dark); dark.SetColor(EmissionColor, Color.black); r.SetPropertyBlock(dark);
        }
    }

    // ------------------------------------------------------------------ interaction

    public bool InReach(PlayerWeaponSystem w) => w != null && meter != null && (w.transform.position - meter.position).sqrMagnitude <= Reach * Reach;
    public string Prompt(PlayerWeaponSystem w) => InReach(w) ? "Ev sayacı • " + EcoKeys.Tag(EcoKeys.Action.Interact) + " Tasarruf önlemleri (" + DoneCount + "/4)" : "";
    public bool TryInteract(PlayerWeaponSystem w)
    {
        if (!InReach(w)) return false;
        weapons = w; SetOpen(true); return true;
    }

    /// <summary>Objective card step: after the first energy project, from late afternoon on, cut the home's waste.</summary>
    public bool WantsObjective(out string text, out Vector3? goal)
    {
        text = ""; goal = null;
        var projects = EcoProjects.Current;
        if (FreeMeasuresDone || meter == null || clock == null || projects == null || projects.All.Count == 0 || !projects.All[0].done) return false;
        bool evening = clock.hour >= 16f || clock.hour < 6f || clock.day > 1;
        if (!evening) return false;
        text = "Gece yaklaşıyor · evde boşa yanan ışığı ve bekleme modunu kapat, batarya sabaha yetsin · Ev sayacında " + EcoKeys.Tag(EcoKeys.Action.Interact);
        goal = meter.position;
        return true;
    }

    public void SetOpen(bool value) { open = value; EcoUIFlow.SetPanel(this, value); }

    public bool Apply(int index)
    {
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        if (weapons == null) return false;
        switch (index)
        {
            case 0: if (lightsOff) return false; lightsOff = true; break;
            case 1: if (standbyOff) return false; standbyOff = true; break;
            case 2:
                if (ledInstalled) return false;
                if (!weapons.TrySpendMaterials(LedMetal, LedPlastic)) { weapons.ShowFeedback("LED için " + LedMetal + " metal + " + LedPlastic + " plastik gerekiyor."); return false; }
                ledInstalled = true; break;
            case 3:
                if (dripIrrigation) return false;
                if (!weapons.TrySpendMaterials(DripMetal, DripPlastic)) { weapons.ShowFeedback("Damla sulama için " + DripMetal + " metal + " + DripPlastic + " plastik gerekiyor."); return false; }
                dripIrrigation = true; break;
            default: return false;
        }
        EcoPlaytestLog.Objective("Tasarruf önlemi: " + Titles[index]);
        weapons.ShowFeedback("Tasarruf • " + Titles[index] + " • ev gece yükü " + BaselineNightLoad.ToString("0.00") + " → " + NightLoad.ToString("0.00") + " EB/sn");
        if (AllDone && !completionShown) { completionShown = true; cardUntil = Time.unscaledTime + 16f; }
        return true;
    }

    // ------------------------------------------------------------------ leaks on player tanks

    private void CheckLeaks()
    {
        if (clock == null || clock.day < 2 || clock.day == lastLeakDay) return;
        lastLeakDay = clock.day;
        foreach (var node in FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None))
        {
            if (node == null || node.kind != EcoWaterNode.Kind.Tank || node.leaking || node.cleanWater < 10f || !node.Operational) continue;
            if (Random.value > 0.35f) continue;
            node.leaking = true;
            if (weapons != null) weapons.ShowFeedback("Su deposu sızdırıyor! Temiz su boşa akıyor · yanında " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile tamir et (" + LeakRepairMetal + " metal)");
            break; // at most one new leak a day
        }
    }

    public static bool TryRepair(EcoWaterNode tank, PlayerWeaponSystem w)
    {
        if (tank == null || !tank.leaking || w == null) return false;
        if (!w.TrySpendMaterials(LeakRepairMetal, 0)) { w.ShowFeedback("Sızıntıyı tamir etmek için " + LeakRepairMetal + " metal gerekiyor."); return true; }
        tank.leaking = false;
        w.ShowFeedback("Sızıntı tamir edildi • " + tank.leakedWater.ToString("0") + " SB su boşa gitmişti. Küçük bir damla bile gün boyunca birikir.");
        tank.leakedWater = 0f;
        return true;
    }

    // ------------------------------------------------------------------ panel

    private static readonly string[] Titles = { "Boş odaların ışığını kapat", "Bekleme modunu kapat", "LED ampule geç", "Damla sulama kur" };
    private static readonly string[] Icons = { "electricity", "electricity", "electricity", "water" };
    private static readonly string[] Facts =
    {
        "Kimsenin olmadığı odada yanan lamba hiçbir işe yaramadan enerji harcar. Bu oyunda gece aydınlatma yükü yarıya iner.",
        "Cihazları fişten çek: televizyon, modem ve şarj aleti kapalı görünse de prizde güç çekmeye devam eder (bekleme tüketimi). Bu oyunda sürekli 0,04 EB/sn kesilir.",
        "LED ampul, eski akkor ampule göre aynı ışığı çok daha az elektrikle verir (yaklaşık %75 daha az). Bu oyunda aydınlatma yükü dörtte birine iner.",
        "Su toprağın üstüne değil, damla damla doğrudan köke verilir; buharlaşma ve boşa akış azalır. Bu oyunda tam sulama 5 yerine 3 SB, toprak %25 daha yavaş kurur."
    };

    private void OnGUI()
    {
        if (open) DrawPanel();
        else if (Time.unscaledTime < cardUntil && !EcoUI.ForeignWindowOpen) DrawCard();
    }

    private static string Cost(int i) => i == 2 ? LedMetal + " metal + " + LedPlastic + " plastik" : i == 3 ? DripMetal + " metal + " + DripPlastic + " plastik" : "Ücretsiz";
    private bool Done(int i) => i == 0 ? lightsOff : i == 1 ? standbyOff : i == 2 ? ledInstalled : dripIrrigation;

    private void DrawPanel()
    {
        EcoTheme.Begin();
        var r = EcoTheme.Window(760f, 700f, "EV SAYACI · TASARRUF", "Ev, üsteki mahalle bataryasından beslenir; batarya bitince dizel jeneratöre geçer ve karbon salar.", "electricity");
        if (EcoTheme.CloseButton(r)) { SetOpen(false); EcoTheme.End(); return; }
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 112f;
        string now = (LightsOn ? "Gece" : "Gündüz") + " · Ev şu an " + Load.ToString("0.00") + " EB/sn çekiyor (önlemsiz " + BaselineLoad.ToString("0.00") + ")";
        GUI.Label(new Rect(x, y, w, 26f), now, EcoTheme.Subtitle); y += 32f;
        float nightTotal = NightLoad * 300f, baseNight = BaselineNightLoad * 300f;
        string stock = battery != null ? "Batarya " + battery.storedEnergy.ToString("0") + "/" + battery.capacity.ToString("0") + " EB" : "Batarya bulunamadı";
        GUI.Label(new Rect(x, y, w, 44f), "Bir gece (5 dk) ev tüketimi: " + nightTotal.ToString("0") + " EB (önlemsiz " + baseNight.ToString("0") + " EB) · " + stock
            + (GeneratorRunning ? "  ·  BATARYA BOŞ: yedek dizel jeneratör çalışıyor ve karbon salıyor!" : ""), EcoTheme.Muted); y += 44f;
        GUI.Label(new Rect(x, y, w, 26f), "Bugüne kadar kazandığın: " + savedEnergy.ToString("0") + " EB elektrik · " + savedWater.ToString("0") + " SB su · Önlemler " + DoneCount + "/4", EcoTheme.Body); y += 36f;

        var viewport = new Rect(x, y, w, r.yMax - 80f - y);
        float inner = w - 22f, total = 0f;
        var heights = new float[4];
        for (int i = 0; i < 4; i++) { heights[i] = 92f + Mathf.Max(36f, EcoTheme.Muted.CalcHeight(new GUIContent(Facts[i]), inner - 90f)); total += heights[i] + 10f; }
        scroll.x = 0f;
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, inner, total), false, true);
        float cy = 0f;
        for (int i = 0; i < 4; i++)
        {
            GUI.Box(new Rect(0, cy, inner, heights[i]), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(20, cy + 20, 34, 34), Done(i) ? "ui_check" : Icons[i]);
            GUI.Label(new Rect(68, cy + 18, inner - 260, 28), Titles[i], EcoTheme.Subtitle);
            GUI.Label(new Rect(68, cy + 48, inner - 260, 22), Done(i) ? "Uygulandı" : Cost(i), EcoTheme.Muted);
            GUI.Label(new Rect(68, cy + 80, inner - 90, heights[i] - 88), Facts[i], EcoTheme.Muted);
            GUI.enabled = !Done(i);
            if (EcoTheme.IconButton(new Rect(inner - 176, cy + 20, 156, 40), Done(i) ? "Yapıldı" : "Uygula", Done(i) ? "ui_check" : Icons[i])) Apply(i);
            GUI.enabled = true;
            cy += heights[i] + 10f;
        }
        GUI.EndScrollView();
        if (EcoTheme.IconButton(new Rect(r.x + 40f, r.yMax - 66f, r.width - 80f, 40f), "Kapat  [Esc]", "ui_close")) SetOpen(false);
        EcoTheme.End();
    }

    private void DrawCard()
    {
        EcoTheme.Begin();
        var box = new Rect(EcoTheme.Width * 0.5f - 300f, EcoTheme.Height - 330f, 600f, 170f);
        EcoTheme.Panel(box);
        EcoTheme.Icon(new Rect(box.x + 26, box.y + 24, 34, 34), "ui_check");
        GUI.Label(new Rect(box.x + 72, box.y + 24, box.width - 100, 30), "Tasarruflu ev", EcoTheme.Subtitle);
        GUI.Label(new Rect(box.x + 30, box.y + 64, box.width - 60, 90),
            "En temiz ve en ucuz enerji, hiç harcanmayan enerjidir. Evin gece yükü " + BaselineNightLoad.ToString("0.00") + " → " + NightLoad.ToString("0.00")
            + " EB/sn indi; damla sulama aynı hasadı daha az suyla veriyor. Çocuk: \"Artık gece ışığımız sabaha kadar yetiyor!\"", EcoTheme.Body);
        EcoTheme.End();
    }
}
