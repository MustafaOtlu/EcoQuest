using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

// TTA Döngü 1 (uzman değerlendirmesi): the main story takes 3–5 hours, an expert session about 30–45 minutes. The title
// menu offers ready starting points that put a fresh world into a state where each theme of the ethics form can be seen
// within minutes, and a button that plays the six transformation moments one after another. Scenarios only change the
// fresh world opened from the title screen; they write no save file. The player's normal saves stay untouched.
public sealed class EcoExpertDemo : MonoBehaviour
{
    public static EcoExpertDemo Current { get; private set; }
    public static bool PanelOpen { get; private set; }

    private struct Scenario { public string title, themes, detail, icon; }
    private static readonly Scenario[] Scenarios =
    {
        new Scenario { title = "1 · Başlangıç (07:00)", icon = "player", themes = "Yenilenebilir enerji · çevre temizliği",
            detail = "Yeni oyunun ilk dakikaları: mahalle çocuğu, ekim ve sulama, üsteki şarj istasyonu ile ilk enerji projesi (P1). ~10 dk" },
        new Scenario { title = "2 · Akşam: evde tasarruf", icon = "electricity", themes = "Enerji tasarrufu · su tasarrufu · karbon ayak izi",
            detail = "İlk enerji projesi bitmiş, saat 17:30. Ev sayacının yanında başlarsın: ışık, bekleme modu, LED, damla sulama. Gece batarya boşalırsa jeneratör karbon salar; sol üstteki \"Ayak izi\" satırına tıkla. ~8 dk" },
        new Scenario { title = "3 · Göl ve sanayi", icon = "water", themes = "Su yönetimi · çevre temizliği · karbon · atık",
            detail = "2. gün sabahı, Fabrika Gölü'nün kirletici girişinin yanında; bol malzeme ve iki büyük filtre. Girişi kapat, fabrikalara filtre tak ve dönüştür; ayak izi penceresinde filtre ile karbon farkını gör. ~10 dk" },
        new Scenario { title = "4 · Orman ve final hazırlığı", icon = "crop_tree", themes = "Doğa koruma · iklim · final",
            detail = "Üç proje bitmiş (enerji, göl, orman), buzul istasyonu etkin, terminal rezervi dolu. Buzul terminalinin yanında başlarsın; [F] ile finali başlatabilirsin. ~10 dk" },
    };

    private Vector2 scroll;
    private bool showingMoments;
    private int momentIndex;
    private float nextMomentAt;

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) { Current = null; PanelOpen = false; } }

    public void Open() { PanelOpen = true; }

    private void Update()
    {
        if (PanelOpen)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { EcoUIFlow.ConsumeEscape(); PanelOpen = false; }
            if (!PlayerController.Paused) PanelOpen = false;
        }
        if (showingMoments && Time.unscaledTime >= nextMomentAt)
        {
            var moment = EcoTransformationMoment.Current;
            if (moment == null || momentIndex >= 6) { showingMoments = false; return; }
            if (moment.Playing) { nextMomentAt = Time.unscaledTime + 0.25f; return; }
            moment.Preview(momentIndex++);
            nextMomentAt = Time.unscaledTime + 6.2f;
        }
    }

    // ------------------------------------------------------------------ scenarios

    private void Launch(int index)
    {
        PanelOpen = false;
        PlayerController.SetPaused(false);
        StartCoroutine(Apply(index));
    }

    private IEnumerator Apply(int index)
    {
        // Let the runtime services (child, home meter, footprint, projects) create themselves first.
        float until = Time.unscaledTime + 1.3f;
        while (Time.unscaledTime < until) yield return null;
        var weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        var projects = EcoProjects.Current;
        var clock = EcoWorldClock.Current;
        if (weapons == null || projects == null || clock == null) yield break;
        if (EcoStory.Current != null) EcoStory.Current.intro = true;

        switch (index)
        {
            case 0:
                if (EcoStory.Current != null) EcoStory.Current.intro = false;
                weapons.ShowFeedback("Uzman demo · Başlangıç: hedef kartını izle, mahalle çocuğuyla konuş.");
                break;
            case 1:
                Complete(projects, 0);
                clock.hour = 17.5f;
                weapons.ReturnMaterials(30, 20);
                if (EcoHomeSaving.Current != null && EcoHomeSaving.Current.Meter != null) MovePlayer(EcoHomeSaving.Current.Meter.position + EcoHomeSaving.Current.Meter.forward * 1.6f);
                weapons.ShowFeedback("Uzman demo · Akşam: ev sayacında [E] ile tasarruf önlemlerini dene. Sol üstte \"Ayak izi\" satırına tıkla.");
                break;
            case 2:
                Complete(projects, 0);
                clock.day = Mathf.Max(clock.day, 2); clock.hour = 9f;
                weapons.ReturnMaterials(150, 150);
                SetProperty(weapons, "LargeFilters", weapons.LargeFilters + 2);
                DiscoverAll();
                var inlet = FindFirstObjectByType<EcoPollutionInlet>();
                if (inlet != null) MovePlayer(inlet.transform.position + Vector3.back * 2f);
                weapons.ShowFeedback("Uzman demo · Göl ve sanayi: girişi [E] ile kapat, fabrikalara filtre tak, [B] ile pompa–arıtma–depo kur.");
                break;
            case 3:
                Complete(projects, 0); Complete(projects, 1); Complete(projects, 2);
                clock.day = Mathf.Max(clock.day, 3); clock.hour = 10f;
                DiscoverAll();
                foreach (var info in FindObjectsByType<EcoRegionInfo>(FindObjectsSortMode.None))
                {
                    var region = info.GetComponent<EcoRegion>(); if (region == null) continue;
                    if (info.projectIndex == 1) { region.waterPollution = Mathf.Min(region.waterPollution, 22f); EcoDiscoveries.RemediateInlet(region); }
                    if (info.projectIndex == 2)
                    {
                        region.vegetation = Mathf.Max(region.vegetation, 0.66f); region.soilPollution = Mathf.Min(region.soilPollution, 12f);
                        if (region.TryGetComponent<EcoHabitat>(out var habitat)) habitat.recovery = Mathf.Max(habitat.recovery, 0.55f);
                    }
                }
                foreach (var inletMarker in FindObjectsByType<EcoPollutionInlet>(FindObjectsSortMode.None)) EcoDiscoveries.RemediateInlet(inletMarker.region);
                var station = EcoGlacierStation.Current;
                if (station != null) { station.RestoreActive(true); MovePlayer(station.transform.position + Vector3.back * 3f); }
                if (EcoFinale.Current != null) EcoFinale.Current.RestoreState(new EcoFinale.ReserveState { energy = 70f, water = 40f });
                weapons.ReceiveElectricity(weapons.maximumEnergy);
                weapons.ReceiveWater(weapons.maximumWater, true);
                SetProperty(weapons, "MetalAmmo", Mathf.Max(weapons.MetalAmmo, 15));
                weapons.ReturnMaterials(60, 60);
                weapons.ShowFeedback("Uzman demo · Final hazırlığı: buzul terminalinin yanında " + EcoKeys.Tag(EcoKeys.Action.Finale) + " ile finali başlatabilirsin.");
                break;
        }
        EcoPlaytestLog.Objective("Uzman demo senaryosu: " + Scenarios[index].title);
    }

    private static void Complete(EcoProjects projects, int index)
    {
        if (index < 0 || index >= projects.All.Count) return;
        var project = projects.All[index];
        project.done = true; project.held = EcoProjects.VerifySeconds;
        if (index == 0) projects.Charged = true;
        // EcoProjects grants the project's YEP once on its next scan.
    }

    private static void DiscoverAll()
    {
        foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None)) EcoDiscoveries.DiscoverRegion(region);
    }

    private static void SetProperty(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite) property.SetValue(target, value);
    }

    private static void MovePlayer(Vector3 position)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player == null) return;
        if (Physics.Raycast(position + Vector3.up * 40f, Vector3.down, out var hit, 120f, ~0, QueryTriggerInteraction.Ignore)) position = hit.point + Vector3.up * 0.1f;
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;
    }

    private void ShowMoments()
    {
        PanelOpen = false;
        PlayerController.SetPaused(false);
        showingMoments = true; momentIndex = 0; nextMomentAt = Time.unscaledTime + 1.5f;
    }

    // ------------------------------------------------------------------ panel (drawn instead of the title menu)

    private void OnGUI()
    {
        if (!PanelOpen) return;
        EcoTheme.Begin();
        var r = EcoTheme.Window(760f, 720f, "UZMAN DEMO", "Değerlendirme için hazır başlangıç noktaları. Her biri yeni dünyada açılır; kayıt dosyası yazmaz.", "ui_journal");
        if (EcoTheme.CloseButton(r)) { PanelOpen = false; EcoTheme.End(); return; }
        float x = r.x + 40f, w = r.width - 80f, y = r.y + 112f;
        var viewport = new Rect(x, y, w, r.yMax - 140f - y);
        float inner = w - 22f, total = 0f;
        var heights = new float[Scenarios.Length];
        for (int i = 0; i < Scenarios.Length; i++) { heights[i] = 92f + EcoTheme.Muted.CalcHeight(new GUIContent(Scenarios[i].detail), inner - 250f); total += heights[i] + 10f; }
        scroll.x = 0f;
        scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, inner, total), false, true);
        float cy = 0f;
        for (int i = 0; i < Scenarios.Length; i++)
        {
            var s = Scenarios[i];
            GUI.Box(new Rect(0, cy, inner, heights[i]), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(20, cy + 20, 32, 32), s.icon);
            GUI.Label(new Rect(66, cy + 16, inner - 250, 28), s.title, EcoTheme.Subtitle);
            GUI.Label(new Rect(66, cy + 44, inner - 250, 22), s.themes, EcoTheme.Body);
            GUI.Label(new Rect(66, cy + 70, inner - 250, heights[i] - 76), s.detail, EcoTheme.Muted);
            if (EcoTheme.IconButton(new Rect(inner - 170, cy + 18, 150, 40), "Başlat", "player")) Launch(i);
            cy += heights[i] + 10f;
        }
        GUI.EndScrollView();
        if (EcoTheme.IconButton(new Rect(x, r.yMax - 122f, w, 44f), "Dönüşüm anlarını izle (6 proje, ~40 sn)", "yep")) ShowMoments();
        if (EcoTheme.IconButton(new Rect(x, r.yMax - 70f, w, 40f), "Geri  [Esc]", "ui_close")) PanelOpen = false;
        EcoTheme.End();
    }
}
