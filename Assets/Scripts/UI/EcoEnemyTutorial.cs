using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-65)]
public sealed class EcoEnemyTutorial : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/EcoEnemyTutorial.unity";
    private const string TitleScene = "Assets/Scenes/EcoWorld.unity";
    private static readonly Color Ink = new Color32(26, 42, 37, 255);
    private static readonly Color Raised = new Color32(49, 69, 58, 255);
    private static readonly Color Paper = new Color32(245, 243, 228, 255);
    private static readonly Color Muted = new Color32(188, 204, 192, 255);
    private static readonly Color Gold = new Color32(255, 205, 103, 255);
    private static readonly Color Leaf = new Color32(163, 223, 154, 255);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    [SerializeField] private PlayerController player;
    [SerializeField] private Transform arenaSpawn;
    [SerializeField] private GameObject[] enemyPrefabs;

    private enum Phase { Briefing, Practice, Result, Complete }
    private Phase phase;
    private PlayerWeaponSystem tools;
    private EnemyBrain enemy;
    private Vector3 playerStart;
    private Quaternion startRotation;
    private string[] discoveries;
    private string practiceInventory, practiceHeading;
    private int encounter, page;
    private bool retry, leaving;
    private float animationStarted, resultUntil;

    private Canvas canvas;
    private CanvasScaler scaler;
    private GameObject modal, cueRow;
    private RectTransform card, status, marker;
    private CanvasGroup animationGroup;
    private Text section, title, topic, details, explanation, source, backLabel, nextLabel, statusTitle, statusHint;
    private Image topicIcon, statusIcon;
    private Button back;
    private readonly Image[] cueIcons = new Image[3];
    private readonly Text[] cueLabels = new Text[3];

    internal readonly struct Cue
    {
        public readonly string Icon, Label;
        public Cue(string icon, string label) { Icon = icon; Label = label; }
    }

    internal sealed class Lesson
    {
        public readonly string Topic, Explanation, Source, Threat, Technique;
        public readonly Cue[] Real, Game;

        public Lesson(string topic, string explanation, string source, string threat, string technique, Cue[] real, Cue[] game)
        {
            Topic = topic; Explanation = explanation; Source = source; Threat = threat; Technique = technique;
            Real = real; Game = game;
        }
    }

    internal static Lesson LessonFor(EnemyBrain.Species species) => Lessons[(int)species];

    private static readonly Lesson[] Lessons =
    {
        // Indexed by EnemyBrain.Species. Real cards describe the environmental themes, game cards describe the fictional enemies.
        new Lesson("Metal atık ve geri kazanım",
            "Alüminyum ve çelik kutular geri dönüştürülebilir. Geri kazanılan metal, yeni ham madde çıkarmak ve işlemek için gereken enerjiyi azaltır.",
            "ABD EPA · Geri dönüşüm ve enerji",
            "Yapılara zarar verir ve uzaktan ateş eder.",
            "Teneke’ye bir kez sol tıkla. Sersemlediğinde yaklaş ve dört saniye içinde küçük hava filtresini tak.",
            new[] { new Cue("scrap", "Metal atık"), new Cue("tool_recycle", "Ayırma"), new Cue("metal", "Geri kazanım") },
            new[] { new Cue("tool_seed", "[2] Tohum aracı"), new Cue("ammo", "Demir top"), new Cue("filter", "Filtre tak") }),
        new Lesson("Orman yangınının yayılması",
            "Kuru bitki örtüsü daha kolay tutuşur. Rüzgâr alevleri başka bitkilere taşıyarak yangının yayılmasını hızlandırabilir.",
            "ABD Ulusal Park Servisi · Orman yangını davranışı",
            "Uzaktan ateş eder ve bitkilerde yangın başlatabilir.",
            "Vakumla Alev’e bir kez sol tıkla. Araç Su moduna geçer. Depoda su olduğundan emin ol.",
            new[] { new Cue("poi_tree", "Kuru bitki"), new Cue("w_wind", "Rüzgâr"), new Cue("poi_fire", "Yangının yayılması") },
            new[] { new Cue("tool_vacuum", "[1] Vakum"), new Cue("water", "Su modu"), new Cue("poi_enemy", "Sol tık") }),
        new Lesson("Asit yağışları",
            "Kükürt dioksit ve azot oksitleri havada tepkimeye girerek asitli bileşikler oluşturur. Yağış bunları toprağa ve suya taşır.",
            "ABD EPA · Asit yağmuru",
            "Mermisi seni yavaşlatır ve ekinlere zarar verebilir.",
            "Vakumla Asit bulutuna bir kez sol tıkla. Araç Anti-vakum moduna geçer. Gelen mermilerden yana kaç.",
            new[] { new Cue("poi_factory", "Hava salımı"), new Cue("w_cloud", "Tepkime"), new Cue("w_rain", "Asitli yağış") },
            new[] { new Cue("tool_vacuum", "[1] Vakum"), new Cue("w_wind", "Anti-vakum"), new Cue("poi_enemy", "Sol tık") }),
        new Lesson("Gölde alg patlaması",
            "Fazla gübre ve atık su, göllere azot ve fosfor taşır. Algler hızla çoğalabilir ve sudaki oksijen azalabilir.",
            "ABD EPA · Besin kirliliği",
            "Yakından saldırıp seni yavaşlatır. Su ve bitkilere zarar verebilir.",
            "Geri dönüşüm aracıyla Salya’ya bir kez sol tıkla. Vakumun Toplama modu da etkili, fakat daha yavaş.",
            new[] { new Cue("compost", "Gübre ve atık su"), new Cue("water_dirty", "Besin birikimi"), new Cue("biodiversity", "Alg artışı") },
            new[] { new Cue("tool_recycle", "[3] Geri dönüşüm"), new Cue("poi_enemy", "Sol tık"), new Cue("player", "Mesafeni koru") }),
        new Lesson("Dumandaki ince parçacıklar",
            "Yanma sonucu havaya ince parçacıklar karışır. Orman yangınlarının dumanı bu parçacıkları taşıyabilir ve solunumla akciğerlere ulaşabilir.",
            "ABD EPA · Yangın dumanı",
            "Yakında görüşü kapatır ve güneş üretimini düşürebilir.",
            "Vakumla Duman’a bir kez sol tıkla. Araç Anti-vakum moduna geçer. Yaklaşmasına fırsat vermeden kullan.",
            new[] { new Cue("poi_fire", "Yanma"), new Cue("air", "İnce parçacıklar"), new Cue("player", "Solunum") },
            new[] { new Cue("tool_vacuum", "[1] Vakum"), new Cue("w_wind", "Anti-vakum"), new Cue("poi_enemy", "Sol tık") })
    };

    private EnemyBrain.Species Species => enemyPrefabs[encounter].GetComponent<EnemyBrain>().species;
    private Lesson Current => LessonFor(Species);

    public static void Open() => Load(ScenePath);

    private static void Load(string path)
    {
        EcoKeys.CancelFire();
        PlayerController.SetPaused(false);
        SceneManager.LoadScene(path);
    }

    private void Awake() => discoveries = EcoDiscoveries.Capture();

    private IEnumerator Start()
    {
        if (player == null || arenaSpawn == null || !ValidRoster())
        {
            Debug.LogError("Düşman eğitiminde oyuncu, arena veya düşman prefabları eksik.", this);
            enabled = false;
            yield break;
        }
        tools = player.GetComponentInChildren<PlayerWeaponSystem>(true);
        if (tools == null)
        {
            Debug.LogError("Düşman eğitimi oyuncusunun araç sistemi eksik.", this);
            enabled = false;
            yield break;
        }
        playerStart = player.transform.position;
        startRotation = player.transform.rotation;
        BuildHud();
        ShowBriefing(0);
        // Capture practice supplies after the real player loadout has initialized.
        yield return null;
        practiceInventory = tools.CaptureSessionJson();
    }

    private bool ValidRoster()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return false;
        var seen = new HashSet<EnemyBrain.Species>();
        foreach (var prefab in enemyPrefabs)
            if (prefab == null || !prefab.TryGetComponent<EnemyBrain>(out var brain) ||
                (int)brain.species < 0 || (int)brain.species >= Lessons.Length || !seen.Add(brain.species)) return false;
        return true;
    }

    private void Update()
    {
        if (leaving || canvas == null) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            EcoUIFlow.ConsumeEscape();
            Leave(TitleScene);
            return;
        }
        if (phase == Phase.Practice)
        {
            if (enemy != null && enemy.State != EnemyBrain.Behaviour.Dead) UpdatePracticeCue();
            else
            {
                retry = Species == EnemyBrain.Species.Tin && (enemy == null || !enemy.FilterInstalled);
                SetPracticeCue(retry ? "Filtreyi takmayı dene" : "Etkisiz hale getirildi",
                    retry ? "Teneke sersemleyince küçük filtreyi tak." : EnemyBrain.NameOf(Species), retry ? "filter" : "ui_check");
                resultUntil = Time.unscaledTime + 0.9f;
                phase = Phase.Result;
            }
        }
        else if (phase == Phase.Result && Time.unscaledTime >= resultUntil)
        {
            ClearShots();
            if (retry) ShowBriefing(1);
            else if (++encounter < enemyPrefabs.Length) ShowBriefing(0);
            else ShowCompletion();
        }
    }

    private void ShowBriefing(int initialPage)
    {
        phase = Phase.Briefing;
        page = initialPage;
        status.gameObject.SetActive(false);
        marker.gameObject.SetActive(false);
        cueRow.SetActive(true);
        ShowModal();
        DrawPage();
    }

    private void ShowCompletion()
    {
        phase = Phase.Complete;
        status.gameObject.SetActive(false);
        marker.gameObject.SetActive(false);
        cueRow.SetActive(false);
        ShowModal();
        EcoUI.SetIcon(topicIcon, "ui_check");
        section.text = "DÜŞMAN EĞİTİMİ";
        title.text = "Eğitim tamamlandı";
        topic.text = enemyPrefabs.Length + " / " + enemyPrefabs.Length + " karşılaşma";
        details.text = "";
        explanation.text = "Her düşmana karşı işe yarayan aracı sahada denedin.";
        source.text = "";
        back.gameObject.SetActive(true);
        backLabel.text = "Baştan başla";
        nextLabel.text = "Ana menü";
    }

    private void ShowModal()
    {
        tools.Builder?.SetBuildMode(false);
        EcoKeys.CancelFire();
        EcoUIFlow.SetPanel(this, true);
        modal.SetActive(true);
        animationStarted = Time.unscaledTime;
        AnimateCard();
    }

    private void DrawPage()
    {
        var lesson = Current;
        bool real = page == 0;
        EcoUI.SetIcon(topicIcon, real ? lesson.Real[2].Icon : lesson.Game[0].Icon);
        section.text = "DÜŞMAN EĞİTİMİ   ·   " + (encounter + 1) + " / " + enemyPrefabs.Length;
        title.text = EnemyBrain.NameOf(Species);
        topic.text = real ? "1 / 2    Gerçek dünyadaki karşılığı" : "2 / 2    Oyundaki davranışı";
        explanation.text = real ? lesson.Explanation : lesson.Technique;
        source.text = real ? lesson.Source : "";
        details.text = real ? lesson.Topic : lesson.Threat;
        var cues = real ? lesson.Real : lesson.Game;
        for (int i = 0; i < cues.Length; i++)
        {
            EcoUI.SetIcon(cueIcons[i], cues[i].Icon);
            cueLabels[i].text = cues[i].Label;
        }
        if (!real && Species == EnemyBrain.Species.Tin)
        {
            cueLabels[1].text = EcoKeys.Tag(EcoKeys.Action.Mode) + " Demir top";
            cueLabels[2].text = EcoKeys.Tag(EcoKeys.Action.Interact) + " Filtre tak";
        }
        back.gameObject.SetActive(!real);
        backLabel.text = "‹  Geri";
        nextLabel.text = real ? "Oyundaki taktik  ›" : "Sahada dene  ›";
    }

    private void Back()
    {
        if (phase == Phase.Complete) Leave(ScenePath);
        else { page = 0; DrawPage(); }
    }

    private void Next()
    {
        if (phase == Phase.Complete) Leave(TitleScene);
        else if (page == 0) { page = 1; DrawPage(); }
        else if (!string.IsNullOrEmpty(practiceInventory)) BeginPractice();
    }

    private void BeginPractice()
    {
        if (enemy != null)
        {
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
        ClearShots();
        tools.Builder?.SetBuildMode(false);
        tools.RestoreSessionJson(practiceInventory);
        player.CancelWalkOrder();
        var motor = player.GetComponent<CharacterController>();
        if (motor != null) motor.enabled = false;
        player.transform.SetPositionAndRotation(playerStart, startRotation);
        if (motor != null) motor.enabled = true;
        var vitals = player.GetComponent<PlayerVitals>();
        if (vitals != null && vitals.IsRecovering) vitals.SimulateRecovery(PlayerVitals.RecoveryDuration);

        Vector3 direction = Vector3.ProjectOnPlane(playerStart - arenaSpawn.position, Vector3.up);
        var rotation = direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        enemy = Instantiate(enemyPrefabs[encounter], arenaSpawn.position, rotation, transform).GetComponent<EnemyBrain>();
        // Keep ranged targets inside the starter vacuum's reach and soften damage for practice.
        enemy.preferredRange = Mathf.Min(enemy.preferredRange, 4.1f);
        enemy.damage = Mathf.Min(enemy.damage, 6f);
        practiceHeading = (encounter + 1) + " / " + enemyPrefabs.Length + "    " + enemy.DisplayName;
        phase = Phase.Practice;
        modal.SetActive(false);
        EcoUIFlow.SetPanel(this, false);
        EcoKeys.CancelFire();
        status.gameObject.SetActive(true);
        UpdatePracticeCue();
    }

    private static void ClearShots()
    {
        foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
        foreach (var projectile in FindObjectsByType<PlayerMetalProjectile>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
    }

    private void UpdatePracticeCue()
    {
        switch (enemy.species)
        {
            case EnemyBrain.Species.Tin:
                if (enemy.IsStunned)
                {
                    string remaining = Mathf.CeilToInt(enemy.StunRemaining).ToString() + " sn";
                    SetPracticeCue(practiceHeading, "Yaklaş  ›  " + EcoKeys.Tag(EcoKeys.Action.Interact) + " Filtre tak  ·  " + remaining, "filter");
                }
                else if (tools.Selected != PlayerWeaponSystem.Tool.SeedGun) SetPracticeCue(practiceHeading, "[2] Tohum aracını seç", "tool_seed");
                else if (!tools.MetalMode) SetPracticeCue(practiceHeading, EcoKeys.Tag(EcoKeys.Action.Mode) + " Demir top moduna geç", "ammo");
                else SetPracticeCue(practiceHeading, "Teneke’ye bir kez sol tıkla", "ammo");
                break;
            case EnemyBrain.Species.Slime:
                SetPracticeCue(practiceHeading, "[3] Geri dönüşüm  ›  Düşmana sol tıkla", "tool_recycle");
                break;
            case EnemyBrain.Species.Flame:
                SetPracticeCue(practiceHeading, "[1] Vakum  ›  Düşmana sol tıkla", "water");
                break;
            default:
                SetPracticeCue(practiceHeading, "[1] Vakum  ›  Düşmana sol tıkla", "w_wind");
                break;
        }
    }

    private void SetPracticeCue(string heading, string hint, string icon)
    {
        if (statusTitle.text != heading) statusTitle.text = heading;
        if (statusHint.text != hint) statusHint.text = hint;
        EcoUI.SetIcon(statusIcon, icon);
    }

    private void Retry()
    {
        if (phase == Phase.Practice) BeginPractice();
    }

    private void Leave(string path)
    {
        if (leaving) return;
        leaving = true;
        tools?.Builder?.SetBuildMode(false);
        EcoUIFlow.SetPanel(this, false);
        Load(path);
    }

    private void OnDestroy()
    {
        if (EcoUIFlow.IsPanel(this)) EcoUIFlow.SetPanel(this, false);
        if (discoveries != null) EcoDiscoveries.Restore(discoveries);
    }

    private void BuildHud()
    {
        canvas = EcoUI.CreateCanvas("Enemy tutorial", 110, out scaler);
        canvas.transform.SetParent(transform, false);
        var root = (RectTransform)canvas.transform;
        status = Plate("Combat cue", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -18), new Vector2(370, 76), Ink, false);
        statusIcon = Icon("Combat action", status, "poi_enemy", new Vector2(15, -18), 40);
        statusTitle = Write(status, "", new Vector2(68, -9), new Vector2(281, 28), 18, Paper, true);
        statusHint = Write(status, "", new Vector2(68, -37), new Vector2(285, 28), 15, Muted);
        marker = Plate("Enemy marker", root, Vector2.zero, Center, Vector2.zero, new Vector2(36, 36), Ink, false);
        Icon("Target", marker, "poi_enemy", new Vector2(5, -5), 26);
        Action("Ana menü  ·  Esc", root, Vector2.zero, Vector2.zero, new Vector2(18, 18), new Vector2(148, 38), Raised, Paper,
            () => Leave(TitleScene), out _);
        var retryButton = Action("Tekrar dene", status, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(110, 31),
            Raised, Paper, Retry, out _);
        retryButton.gameObject.name = "Retry encounter";

        var shade = EcoUI.Fill("Briefing overlay", root, new Color(0.034f, 0.056f, 0.048f, 0.6f));
        shade.raycastTarget = true;
        EcoUI.Stretch(shade.rectTransform);
        modal = shade.gameObject;
        card = Plate("Enemy briefing", shade.transform, Center, Center, Vector2.zero, new Vector2(510, 382), Ink, true);
        animationGroup = card.gameObject.AddComponent<CanvasGroup>();
        topicIcon = Icon("Lesson symbol", card, "poi_enemy", new Vector2(22, -26), 48);
        section = Write(card, "", new Vector2(86, -20), new Vector2(400, 22), 13, Gold, true);
        title = Write(card, "", new Vector2(86, -44), new Vector2(400, 40), 29, Paper, true);
        topic = Write(card, "", new Vector2(23, -96), new Vector2(465, 26), 16, Gold, true);
        details = Write(card, "", new Vector2(23, -121), new Vector2(465, 22), 15, Muted);
        var cueLayout = EcoUI.Rect("Lesson sequence", card, TopLeft, TopLeft);
        EcoUI.Place(cueLayout, new Vector2(22, -151), new Vector2(466, 76));
        cueRow = cueLayout.gameObject;
        for (int i = 0; i < cueIcons.Length; i++)
        {
            var tile = Plate("Sequence tile", cueLayout, TopLeft, TopLeft, new Vector2(i * 166, 0), new Vector2(134, 76), Raised, false);
            cueIcons[i] = Icon("Step symbol", tile, "poi_enemy", new Vector2(49, -8), 36);
            cueLabels[i] = Write(tile, "", new Vector2(4, -46), new Vector2(126, 25), 13, Paper, true, TextAnchor.MiddleCenter);
            if (i < 2) Write(cueLayout, "›", new Vector2(i * 166 + 137, -20), new Vector2(26, 35), 27, Gold, true, TextAnchor.MiddleCenter);
        }
        explanation = Write(card, "", new Vector2(23, -241), new Vector2(465, 61), 17, Paper, false, TextAnchor.UpperLeft);
        source = Write(card, "", new Vector2(23, -302), new Vector2(465, 20), 12, Muted);
        back = Action("‹  Geri", card, TopLeft, TopLeft, new Vector2(23, -332), new Vector2(132, 36), Raised, Paper, Back, out backLabel);
        Action("Oyundaki taktik  ›", card, TopLeft, TopLeft, new Vector2(304, -332), new Vector2(182, 36), Leaf, Ink, Next, out nextLabel);
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        float scale = EcoUI.Scale(canvas);
        scaler.scaleFactor = scale;
        if (modal.activeSelf) AnimateCard();
        bool showMarker = phase == Phase.Practice && enemy != null && EcoIsoCamera.Cam != null;
        marker.gameObject.SetActive(showMarker);
        if (!showMarker) return;
        var at = EcoIsoCamera.Cam.WorldToScreenPoint(enemy.transform.position + Vector3.up * 1.6f);
        var bounds = canvas.pixelRect.size / scale;
        marker.anchoredPosition = new Vector2(Mathf.Clamp(at.x / scale, 23, bounds.x - 23), Mathf.Clamp(at.y / scale, 68, bounds.y - 110));
    }

    private void AnimateCard()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - animationStarted) / 0.22f);
        float progress = 1f - Mathf.Pow(1f - t, 3f);
        animationGroup.alpha = progress;
        card.anchoredPosition = Vector2.down * (12f * (1f - progress));
        card.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, progress);
    }

    private static RectTransform Plate(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color tint, bool blocks)
    {
        var image = EcoUI.Panel(name, parent, anchor, pivot, tint, blocks);
        image.pixelsPerUnitMultiplier = 1.8f;
        EcoUI.Place(image.rectTransform, position, size);
        return image.rectTransform;
    }

    private static Image Icon(string name, Transform parent, string icon, Vector2 position, float size)
    {
        var image = EcoUI.Icon(name, parent, icon, TopLeft, TopLeft, size);
        EcoUI.LayoutIcon(image, position, size);
        return image;
    }

    private static Text Write(Transform parent, string value, Vector2 position, Vector2 size, int font, Color tint,
        bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var text = EcoUI.Label("Text", parent, value, font, align, tint, bold);
        EcoUI.Layout(text, position, size);
        return text;
    }

    private static Button Action(string caption, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position,
        Vector2 size, Color tint, Color ink, UnityEngine.Events.UnityAction action, out Text label)
    {
        var panel = Plate(caption, parent, anchor, pivot, position, size, tint, true);
        var button = panel.gameObject.AddComponent<Button>();
        button.targetGraphic = panel.GetComponent<Image>();
        button.onClick.AddListener(action);
        label = Write(panel, caption, Vector2.zero, size, 15, ink, true, TextAnchor.MiddleCenter);
        return button;
    }
}
