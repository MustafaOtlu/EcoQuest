using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(-65)]
public sealed partial class EcoTutorialLesson : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/EcoTutorial.unity";
    internal static EcoTutorialLesson Current { get; private set; }
    private const string TitleScene = "Assets/Scenes/EcoWorld.unity";
    private const float PlantRadius = 2.4f;
    private const float SolarRadius = 1.45f;
    private const float WaterReach = 2.7f;
    private static readonly Color Ink = new Color32(27, 43, 38, 247);
    private static readonly Color Raised = new Color32(49, 69, 58, 255);
    private static readonly Color Paper = new Color32(245, 243, 228, 255);
    private static readonly Color Muted = new Color32(195, 207, 195, 255);
    private static readonly Color Amber = new Color32(255, 205, 103, 255);
    private static readonly Color Leaf = new Color32(163, 223, 154, 255);
    private static readonly Vector2 Top = new Vector2(0, 1);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);

    private enum Step { Walk, Collect, Recycle, Plant, Water, Solar, Battery, SolarCable, ChargerCable, Charge, Finish, Done }
    private static readonly string[] StepIcons =
        { "player", "tool_vacuum", "tool_recycle", "tool_seed", "water", "poi_solar",
          "electricity", "electricity", "electricity", "poi_charge", "poi_quest", "ui_check" };

    [SerializeField] private PlayerController player;
    [SerializeField] private Transform movement, plantBed, solarPad, finish;
    [SerializeField] private RecyclableResource scrap;

    private PlayerWeaponSystem tools;
    private PlantedSeed plant;
    private string[] discoveries;
    private Step step;
    private bool ready, leaving;
    private float nextPlantCheck, checkUntil;
    private Vector3 scrapPosition;
    private Vector3 target;
    private float radius;
    private LineRenderer ring;
    private Material markerMaterial;
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform layout, pin, pinArrow, mouseGlyph;
    private Image pinIcon, mainIcon, mouseButton, checkIcon;
    private readonly Image[] progress = new Image[(int)Step.Done];
    private readonly KeyCap[] keyCaps = new KeyCap[4];
    private Text heading, note;
    private Button exitButton;

    private sealed class KeyCap
    {
        private static readonly Color BaseColor = new Color32(156, 140, 122, 255);
        private static readonly Color BevelColor = new Color32(213, 201, 183, 255);
        private static readonly Color FaceColor = new Color32(251, 246, 233, 255);
        private static readonly Color PressedBase = new Color32(87, 131, 89, 255);
        private static readonly Color PressedBevel = new Color32(133, 183, 115, 255);

        private readonly Image shell, bevel, face;
        private readonly Text legend;
        private bool pressed;
        public Key Binding { get; private set; }
        public float FlashUntil { get; private set; }

        public KeyCap(Transform parent, Vector2 position)
        {
            shell = Tile("Keycap", parent, position, new Vector2(45, 37), BaseColor);
            bevel = Tile("Bevel", shell.transform, new Vector2(1, -1), new Vector2(43, 32), BevelColor);
            face = Tile("Face", bevel.transform, new Vector2(4, -1), new Vector2(35, 27), FaceColor);
            shell.pixelsPerUnitMultiplier = bevel.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier = 2f;
            legend = Label(face.transform, "Binding", "", 16, Ink, new Vector2(1, 0), new Vector2(33, 27), true, TextAnchor.MiddleCenter);
            legend.resizeTextForBestFit = true;
            legend.resizeTextMinSize = 8 * EcoUI.RasterScale;
            legend.resizeTextMaxSize = 16 * EcoUI.RasterScale;
            shell.gameObject.SetActive(false);
        }

        public void SetBinding(Key key)
        {
            if (Binding == key) return;
            Binding = key;
            FlashUntil = 0f;
            SetPressed(false);
            shell.gameObject.SetActive(key != Key.None);
            if (key == Key.None) return;
            string name = key.ToString();
            var control = Keyboard.current?[key];
            legend.text = name.StartsWith("Digit", System.StringComparison.Ordinal) ? name.Substring(5) :
                control?.shortDisplayName ?? control?.displayName ?? name;
        }

        public void UpdateInput(Keyboard keyboard, float now)
        {
            if (Binding == Key.None) return;
            var key = keyboard?[Binding];
            if (key == null || !key.wasPressedThisFrame) return;
            FlashUntil = now + 0.2f;
            SetPressed(true);
        }

        private void SetPressed(bool active)
        {
            if (pressed == active) return;
            pressed = active;
            shell.color = active ? PressedBase : BaseColor;
            bevel.color = active ? PressedBevel : BevelColor;
            face.color = active ? Leaf : FaceColor;
            bevel.rectTransform.anchoredPosition = new Vector2(1, active ? -4 : -1);
        }
    }

    public static void Open() => Load(ScenePath);

    private static void Load(string path)
    {
        EcoKeys.CancelFire();
        PlayerController.SetPaused(false);
        SceneManager.LoadScene(path);
    }

    private void Awake()
    {
        Current = this;
        discoveries = EcoDiscoveries.Capture();
    }

    private void Start()
    {
        if (player == null || movement == null || plantBed == null || solarPad == null || finish == null || scrap == null)
        {
            Debug.LogError("EcoTutorialLesson: oyuncu veya öğretici hedefi sahneye bağlı değil.", this);
            enabled = false;
            return;
        }
        tools = player.GetComponentInChildren<PlayerWeaponSystem>(true);
        scrapPosition = scrap.transform.position;
        BuildHud();
        ring = new GameObject("Tutorial target ring", typeof(LineRenderer)).GetComponent<LineRenderer>();
        ring.transform.SetParent(transform, false);
        ring.loop = true;
        ring.positionCount = 48;
        ring.widthMultiplier = 0.055f;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        var shader = Resources.Load<Shader>("EcoUI/ToolAreaOverlay");
        if (shader != null)
        {
            markerMaterial = new Material(shader) { name = "Tutorial highlight", renderQueue = 4002 };
            ring.sharedMaterial = markerMaterial;
        }
        EnterStep(Step.Walk);
    }

    private void Update()
    {
        if (leaving || tools == null) return;
        if (!ready)
        {
            tools.SelectTool((int)PlayerWeaponSystem.Tool.Hands);
            ready = true;
        }
        var keyboard = Keyboard.current;
        float now = Time.unscaledTime;
        foreach (var cap in keyCaps) cap.UpdateInput(keyboard, now);
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && (tools.Builder == null || !tools.Builder.IsBuilding))
        {
            EcoUIFlow.ConsumeEscape();
            Leave(TitleScene);
            return;
        }

        switch (step)
        {
            case Step.Walk:
                if (Distance(movement.position) <= 1.1f) Advance();
                break;
            case Step.Collect:
                if (scrap == null || scrap.Claimed) Advance();
                break;
            case Step.Recycle:
                if (tools.CollectedMetalScrap + tools.CollectedPlasticScrap + tools.CollectedOrganicWaste == 0) Advance();
                break;
            case Step.Plant:
                if (Time.unscaledTime >= nextPlantCheck)
                {
                    nextPlantCheck = Time.unscaledTime + 0.2f;
                    foreach (var seed in FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None))
                    {
                        if (seed.IsWithered || FlatDistance(seed.transform.position, plantBed.position) > PlantRadius) continue;
                        plant = seed;
                        Advance();
                        break;
                    }
                }
                break;
            case Step.Water:
                if (plant == null || plant.IsWithered) EnterStep(Step.Plant);
                else if (plant.moisture >= 0.25f || plant.IsMature) Advance();
                break;
            case Step.Solar:
                foreach (var structure in EcoStructure.Active)
                {
                    if (!structure.playerBuilt || structure.buildingId != "solar" ||
                        FlatDistance(structure.transform.position, solarPad.position) > SolarRadius) continue;
                    RecordTrainingSolar(structure);
                    tools.Builder.SetBuildMode(false);
                    Advance();
                    break;
                }
                break;
            case Step.Finish:
                if (Distance(finish.position) <= 1.15f) Advance();
                break;
            case Step.Done:
                break;
            default:
                UpdatePowerStep();
                break;
        }
        RefreshPrompt();
    }

    private void Leave(string path)
    {
        if (leaving) return;
        leaving = true;
        if (tools != null && tools.Builder != null) tools.Builder.SetBuildMode(false);
        Load(path);
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
        EcoDiscoveries.Restore(discoveries);
        if (markerMaterial != null) Destroy(markerMaterial);
    }

    private void Advance()
    {
        EcoKeys.CancelFire();
        checkUntil = Time.unscaledTime + 0.9f;
        EnterStep(step + 1);
    }

    private void EnterStep(Step next)
    {
        step = next;
        for (int i = 0; i < progress.Length; i++)
            progress[i].color = i < (int)step ? Leaf : i == (int)step ? Amber : Raised;
        EcoUI.SetIcon(mainIcon, StepIcons[(int)step]);
        EcoUI.SetIcon(pinIcon, StepIcons[(int)step]);
        switch (step)
        {
            case Step.Walk: SetTarget(movement.position, 1.1f); break;
            case Step.Collect: SetTarget(scrapPosition, 0.82f); break;
            case Step.Recycle: SetTarget(Vector3.zero, 0f); break;
            case Step.Plant: SetTarget(plantBed.position, 1.25f); break;
            case Step.Water: SetTarget(plant.transform.position, 0.7f); break;
            case Step.Solar: SetTarget(solarPad.position, 2.05f); break;
            case Step.Finish: SetTarget(finish.position, 1.15f); break;
            case Step.Done:
                SetTarget(Vector3.zero, 0f);
                var button = (RectTransform)exitButton.transform;
                button.anchorMin = button.anchorMax = new Vector2(0.5f, 1f);
                button.pivot = new Vector2(0.5f, 1f);
                EcoUI.Place(button, new Vector2(0, -153), new Vector2(175, 43));
                exitButton.targetGraphic.color = Raised;
                break;
            default:
                EnterPowerStep();
                break;
        }
    }

    private void SetTarget(Vector3 position, float size)
    {
        target = position;
        radius = size;
        pin.gameObject.SetActive(size > 0f);
        ring.enabled = markerMaterial != null && size > 0f;
        if (!ring.enabled) return;
        // Use the top of the training surface so the outline remains visible on the soil and deck.
        float height = position.y;
        if (Physics.Raycast(position + Vector3.up * 0.6f, Vector3.down, out var hit, 1.5f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            height = Mathf.Min(height, hit.point.y);
        var center = new Vector3(position.x, height + 0.065f, position.z);
        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(i, center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * size);
        }
    }

    private float Distance(Vector3 position) => FlatDistance(player.transform.position, position);

    private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

    private void RefreshPrompt()
    {
        bool building = tools.Builder != null && tools.Builder.IsBuilding;
        if (building && (step < Step.Solar || step >= Step.Finish))
        {
            Prompt("İnşa modunu kapat", "", EcoKeys.Get(EcoKeys.Action.Build));
            return;
        }

        switch (step)
        {
            case Step.Walk:
                Prompt("Halkaya yürü", "", EcoKeys.Get(EcoKeys.Action.MoveUp), EcoKeys.Get(EcoKeys.Action.MoveLeft),
                    EcoKeys.Get(EcoKeys.Action.MoveDown), EcoKeys.Get(EcoKeys.Action.MoveRight));
                break;
            case Step.Collect:
                if (tools.Selected != PlayerWeaponSystem.Tool.Vacuum) Prompt("Vakumu seç", "", Key.Digit1);
                else if (tools.Mode != PlayerWeaponSystem.VacuumMode.Collect) Prompt("Toplama modunu seç", "", EcoKeys.Get(EcoKeys.Action.Mode));
                else Prompt("Atığı vakumla çek", EcoKeys.HoldToggle ? "Sol tıkla" : "Sol tuşu basılı tut", mouse: true);
                break;
            case Step.Recycle:
                if (tools.Selected != PlayerWeaponSystem.Tool.Recycler) Prompt("Geri dönüşüm aracını seç", "", Key.Digit3);
                else Prompt("Atığı malzemeye dönüştür", "", EcoKeys.Get(EcoKeys.Action.Rotate));
                break;
            case Step.Plant:
                if (Distance(plantBed.position) > 5.5f) Prompt("Toprak yatağına yaklaş", "");
                else if (tools.Selected != PlayerWeaponSystem.Tool.SeedGun) Prompt("Tohum aracını seç", "", Key.Digit2);
                else if (tools.MetalMode) Prompt("Tohum modunu seç", "", EcoKeys.Get(EcoKeys.Action.Mode));
                else Prompt("Toprak yatağına tohum at", "Halkanın içine tıkla", mouse: true);
                break;
            case Step.Water:
                if (Distance(plant.transform.position) > WaterReach) Prompt("Fidana yaklaş", "");
                else Prompt("Fidanı sula", "", EcoKeys.Get(EcoKeys.Action.Interact));
                break;
            case Step.Solar:
                if (Distance(solarPad.position) > 7f)
                {
                    if (building) Prompt("İnşa modunu kapat", "Sonra işaretli alana yaklaş", EcoKeys.Get(EcoKeys.Action.Build));
                    else Prompt("İnşa alanına yaklaş", "");
                }
                else if (!building) Prompt("İnşa modunu aç", "", EcoKeys.Get(EcoKeys.Action.Build));
                else if (tools.Builder.Page != 0) Prompt("İlk inşa sayfasına dön", "", Key.Tab);
                else if (tools.Builder.Selection != 0) Prompt("Güneş panelini seç", "", Key.Digit1);
                else Prompt("Güneş panelini yerleştir", "Yeşil önizlemeye tıkla", mouse: true);
                break;
            case Step.Finish: Prompt("Bitiş halkasına yürü", ""); break;
            case Step.Done: Prompt("Öğretici tamamlandı", ""); break;
            default: RefreshPowerPrompt(); break;
        }
    }

    private void Prompt(string title, string instruction, Key first = Key.None, Key second = Key.None,
        Key third = Key.None, Key fourth = Key.None, bool mouse = false)
    {
        if (heading.text == title && note.text == instruction && mouseGlyph.gameObject.activeSelf == mouse &&
            keyCaps[0].Binding == first && keyCaps[1].Binding == second && keyCaps[2].Binding == third && keyCaps[3].Binding == fourth) return;
        // A tool switch can replace the prompt on the next frame. Let the key confirm the tap first.
        float now = Time.unscaledTime;
        foreach (var cap in keyCaps)
            if (now < cap.FlashUntil) return;

        heading.text = title;
        note.text = instruction;
        keyCaps[0].SetBinding(first);
        keyCaps[1].SetBinding(second);
        keyCaps[2].SetBinding(third);
        keyCaps[3].SetBinding(fourth);
        mouseGlyph.gameObject.SetActive(mouse);
        float noteX = mouse ? 124f : first != Key.None ? 136f : 80f;
        EcoUI.Layout(note, new Vector2(noteX, -61f), new Vector2(355f - noteX, 33f));
    }

    private void BuildHud()
    {
        canvas = EcoUI.CreateCanvas("Tutorial prompts", 100, out scaler);
        canvas.transform.SetParent(transform, false);
        layout = (RectTransform)canvas.transform;
        var banner = EcoUI.Panel("Current lesson", layout, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Ink, false);
        EcoUI.Place(banner.rectTransform, new Vector2(0, -18), new Vector2(370, 117));
        var iconTile = Tile("Lesson icon", banner.transform, new Vector2(16, -26), new Vector2(48, 48), Raised);
        mainIcon = EcoUI.Icon("Lesson", iconTile.transform, StepIcons[0], Center, Center, 33);
        heading = Label(banner.transform, "Title", "", 21, Paper, new Vector2(80, -24), new Vector2(275, 34), true);
        note = Label(banner.transform, "Hint", "", 15, Muted, new Vector2(80, -61), new Vector2(247, 33));
        for (int i = 0; i < keyCaps.Length; i++)
            keyCaps[i] = new KeyCap(banner.transform, new Vector2(80 + i * 53, -58));
        mouseGlyph = EcoUI.Rect("Left mouse button", banner.transform, Top, Top);
        EcoUI.Place(mouseGlyph, new Vector2(80, -58), new Vector2(38, 33));
        var mouseBody = Tile("Mouse body", mouseGlyph, new Vector2(6, -1), new Vector2(25, 32), Muted);
        Tile("Mouse face", mouseBody.transform, new Vector2(2, -2), new Vector2(21, 28), Raised);
        mouseButton = Tile("Highlighted left button", mouseBody.transform, new Vector2(3, -3), new Vector2(9, 12), Amber);
        Tile("Right button", mouseBody.transform, new Vector2(13, -3), new Vector2(9, 12), Ink);
        float progressSpacing = 264f / progress.Length;
        for (int i = 0; i < progress.Length; i++)
            progress[i] = Tile("Step " + (i + 1), banner.transform, new Vector2(80 + i * progressSpacing, -101), new Vector2(progressSpacing - 4f, 4), Raised);
        checkIcon = EcoUI.Icon("Step complete", banner.transform, "ui_check", Top, Top, 24);
        EcoUI.LayoutIcon(checkIcon, new Vector2(330, -65), 24);
        checkIcon.enabled = false;

        var badge = EcoUI.Panel("Target pin", layout, Vector2.zero, Center, Ink, false, EcoUI.Circle);
        pin = badge.rectTransform;
        pin.sizeDelta = new Vector2(52, 52);
        var edge = badge.gameObject.AddComponent<Outline>();
        edge.effectColor = Amber;
        edge.effectDistance = new Vector2(1.5f, 1.5f);
        pinIcon = EcoUI.Icon("Target", pin, StepIcons[0], Center, Center, 30);
        pinArrow = EcoUI.Rect("Target direction", pin, Center, Center);
        pinArrow.sizeDelta = new Vector2(18, 15);
        var left = Tile("Arrow left", pinArrow, new Vector2(2, -3), new Vector2(4, 11), Amber);
        left.rectTransform.localRotation = Quaternion.Euler(0, 0, 43f);
        var right = Tile("Arrow right", pinArrow, new Vector2(13, -1), new Vector2(4, 11), Amber);
        right.rectTransform.localRotation = Quaternion.Euler(0, 0, -43f);

        exitButton = ActionButton("Ana menü  ·  Esc", new Vector2(18, 18), new Vector2(151, 38), () => Leave(TitleScene));
        ActionButton("Baştan başla", new Vector2(18, 62), new Vector2(151, 38), () => Leave(ScenePath));
    }

    private Button ActionButton(string text, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var surface = EcoUI.Panel(text, layout, Vector2.zero, Vector2.zero, Ink);
        EcoUI.Place(surface.rectTransform, position, size);
        var button = surface.gameObject.AddComponent<Button>();
        button.targetGraphic = surface;
        button.onClick.AddListener(action);
        var label = Label(surface.transform, "Label", text, 15, Paper, Vector2.zero, size, true, TextAnchor.MiddleCenter);
        EcoUI.Stretch(label.rectTransform);
        label.rectTransform.localScale = Vector3.one;
        label.fontSize = 15;
        return button;
    }

    private static Image Tile(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var tile = EcoUI.Panel(name, parent, Top, Top, color, false);
        EcoUI.Place(tile.rectTransform, position, size);
        return tile;
    }

    private static Text Label(Transform parent, string name, string value, int size, Color color, Vector2 position,
        Vector2 dimensions, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var label = EcoUI.Label(name, parent, value, size, align, color, bold);
        EcoUI.Layout(label, position, dimensions);
        return label;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        float scale = EcoUI.Scale(canvas);
        scaler.scaleFactor = scale;
        float now = Time.unscaledTime;
        checkIcon.enabled = now < checkUntil;
        mouseButton.color = Color.Lerp(Amber, Leaf, 0.5f + 0.5f * Mathf.Sin(now * 5f));
        if (radius <= 0f || EcoIsoCamera.Cam == null) return;

        float pulse = Mathf.Sin(now * 4.4f);
        if (ring.enabled)
        {
            markerMaterial.color = Color.Lerp(Amber, Paper, 0.24f + pulse * 0.15f);
            ring.widthMultiplier = 0.07f + pulse * 0.015f;
        }
        var projection = EcoIsoCamera.Cam.WorldToScreenPoint(target + Vector3.up * 1.28f);
        var desired = (Vector2)projection / scale + Vector2.up * (20f + pulse * 5f);
        var size = canvas.pixelRect.size / scale;
        var clamped = new Vector2(Mathf.Clamp(desired.x, 37, size.x - 37), Mathf.Clamp(desired.y, 75, size.y - 168));
        pin.anchoredPosition = clamped;
        var direction = (desired - clamped + Vector2.down * 0.5f).normalized;
        pinArrow.anchoredPosition = direction * 29f;
        pinArrow.localRotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.down, direction));
    }
}
