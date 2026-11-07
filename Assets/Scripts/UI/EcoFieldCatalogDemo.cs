using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Demo = EcoFieldCatalogData.Demo;

public sealed class EcoFieldCatalogDemo : MonoBehaviour
{
    private const float Width = 384f;
    private const float BoardWidth = 380f;
    private const float BoardHeight = 132f;
    private const float HoldSeconds = .8f;
    private static readonly Vector2 Size = new Vector2(Width, 202f);
    private static readonly Vector2 TopLeft = new Vector2(0, 1);
    private static readonly Vector2 Center = new Vector2(.5f, .5f);
    private static readonly Vector2 Origin = new Vector2(63f, -67f);
    private static readonly Color Ink = new Color32(59, 46, 32, 255);
    private static readonly Color Muted = new Color32(103, 86, 58, 255);
    private static readonly Color Green = new Color32(63, 87, 61, 255);
    private static readonly Color Paper = new Color32(252, 237, 202, 255);
    private static readonly Color KeyColor = new Color32(122, 100, 68, 255);
    private static readonly Color Gold = new Color32(173, 107, 43, 255);
    private static readonly Color Blue = new Color32(37, 119, 143, 255);
    private static readonly Color Soot = new Color32(80, 99, 78, 255);
    private static readonly Color Dark = new Color32(43, 49, 43, 255);
    private static readonly Color Tan = new Color32(149, 112, 66, 40);
    private static readonly string[] VacuumModes = { "Toplama", "Anti-vakum", "Su" };

    private sealed class Target
    {
        public Vector2 Point;
        public Image Icon;
        public Text Caption;
        public bool Reached;
    }

    private readonly List<Target> targets = new List<Target>(4);
    private RectTransform root, board;
    private Image surface;
    private InkLayer drawing;
    private Text state, feedback;
    private Demo kind;
    private string entryId;
    private int mode, shotMode, firstLink, completedLinks, holdingIndex, raw;
    private bool flashlight, energized;
    private float hold, shownAt, shotAt, stunUntil;
    private Vector2 endpoint, lightAim;
    private Color shotColor;
    private Image harvestSeeds;

    internal void Show(RectTransform host, Demo demo, string id)
    {
        Clear();
        if (host == null || demo == Demo.None || demo == Demo.Region) return;

        kind = demo;
        entryId = id;
        root = EcoUI.Rect("Katalog denemesi", host, Center, Center);
        EcoUI.Place(root, Vector2.zero, Size);
        TextAt("UYGULAMA", root, new Vector2(3f, 0f), new Vector2(108f, 26f), 13, Green, true);
        state = TextAt("", root, new Vector2(99f, 0f), new Vector2(135f, 26f), 13, Ink, true);

        surface = EcoUI.Panel("Katalog deneme alanı", root, TopLeft, TopLeft, Tan);
        board = surface.rectTransform;
        EcoUI.Place(board, new Vector2(2f, -32f), new Vector2(BoardWidth, BoardHeight));
        board.gameObject.AddComponent<RectMask2D>();
        var graphics = EcoUI.Rect("Örnek efektleri", board, TopLeft, TopLeft);
        EcoUI.Stretch(graphics);
        graphics.gameObject.AddComponent<CanvasRenderer>();
        drawing = graphics.gameObject.AddComponent<InkLayer>();
        drawing.raycastTarget = false;
        drawing.Draw = Draw;

        var trigger = board.gameObject.AddComponent<EventTrigger>();
        Pointer(trigger, EventTriggerType.PointerDown, Press);
        Pointer(trigger, EventTriggerType.PointerUp, Release);
        Pointer(trigger, EventTriggerType.PointerExit, Release);
        Pointer(trigger, EventTriggerType.Drag, Drag);
        feedback = TextAt("", root, new Vector2(3f, -169f), new Vector2(378f, 30f), 13, Ink);
        ButtonAt("Sıfırla", new Vector2(316f, -2f), 66f, () => Show(host, demo, id), false);

        mode = completedLinks = 0;
        firstLink = holdingIndex = -1;
        raw = 2;
        hold = 0f;
        energized = flashlight = false;
        shownAt = Time.unscaledTime;
        shotAt = stunUntil = -10f;
        lightAim = new Vector2(256f, -66f);
        SetUp();
        UpdateScale();
    }

    internal void Clear()
    {
        if (root != null)
        {
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }
        root = null;
        board = null;
        harvestSeeds = null;
        targets.Clear();
    }

    private void OnDestroy() => Clear();

    private void SetUp()
    {
        switch (kind)
        {
            case Demo.Vacuum:
                Emitter("tool_vacuum");
                AddTarget("scrap", "Atık", 139f, 66f);
                AddTarget("air", "Duman", 223f, 55f);
                AddTarget("seed", "Fidan", 310f, 79f);
                Binding(EcoKeys.Action.Mode, "Mod", ChangeMode);
                DescribeMode();
                break;
            case Demo.Seed:
                Emitter("tool_seed");
                AddTarget("seed", "Toprak", 153f, 72f);
                AddTarget("poi_enemy", "Teneke", 287f, 68f);
                Binding(EcoKeys.Action.Mode, "Mod", ChangeMode);
                DescribeMode();
                break;
            case Demo.Recycler:
                Emitter("tool_recycle");
                AddTarget("scrap", "Yerdeki atık", 151f, 65f);
                AddTarget("ui_layers", "Hazne · 2", 289f, 70f);
                Binding(EcoKeys.Action.Rotate, "İşle", ProcessRaw);
                SetStatus("Geri dönüşüm", "Yerdeki atığa sol tuşla basılı tut veya hazneyi işle.");
                break;
            case Demo.Scanner:
                Emitter("tool_scanner");
                AddTarget("water", "Su", 145f, 67f);
                AddTarget("seed", "Fidan", 224f, 67f);
                AddTarget("poi_charge", "Tesis", 309f, 67f);
                SetStatus("Hedef tarama", "Bilgisini görmek için örnek hedefe tıkla.");
                break;
            case Demo.Hands:
                Emitter("tool_hand");
                AddTarget("crop_cabbage", "Olgun bitki", 216f, 64f);
                Binding(EcoKeys.Action.Interact, "Hasat", Harvest);
                SetStatus("Yakın etkileşim", "Yanındaki olgun bitkiyi etkileşim tuşuyla hasat et.");
                break;
            case Demo.Flashlight:
                surface.color = Dark;
                Emitter("tool_flashlight");
                AddTarget("scrap", "Gizli nesne", 275f, 80f);
                ButtonAt("Sol tık", new Vector2(232f, -2f), 74f, ToggleFlashlight);
                UpdateFlashlight();
                break;
            case Demo.EnemyTin:
                Emitter("tool_seed");
                AddTarget("poi_enemy", "Teneke maketi", 225f, 68f);
                Binding(EcoKeys.Action.Interact, "Filtre", ApplyFilter);
                SetStatus("Demir top", "Teneke’ye tıkla, sersemleyince filtreyi tak.");
                break;
            case Demo.Power:
                AddTarget("poi_solar", "Panel", 65f, 62f);
                AddTarget("electricity", "Batarya", 190f, 62f);
                AddTarget("poi_charge", "Şarj istasyonu", 315f, 62f);
                SetStatus("Elektrik hattı", "Panel ile bataryayı, sonra batarya ile istasyonu bağla.");
                break;
            case Demo.Water:
                AddTarget("water_dirty", "Pompa", 53f, 62f);
                AddTarget("water_quality", "Arıtma", 144f, 62f);
                AddTarget("water", "Depo", 237f, 62f);
                AddTarget("crop_cabbage", "Tarla", 329f, 62f);
                SetStatus("Su hattı", "Her bağlantıda önce çıkışı, ardından giriş noktasını seç.");
                break;
            case Demo.Solar:
                SetGeneration("w_sun", "poi_solar", "Güneş / gölge", "Güneşli", "Gölgede");
                break;
            case Demo.Wind:
                SetGeneration("w_wind", "poi_turbine", "Rüzgâr", "Kuvvetli", "Sakin");
                break;
            case Demo.Hydro:
                SetGeneration("water", "poi_hydro", "Doğal akış", "Akış var", "Akış yok");
                break;
            case Demo.Farm:
                AddTarget("water", "Temiz su girişi", 96f, 67f);
                AddTarget("crop_cabbage", "Ekim yatağı", 286f, 67f);
                SetStatus("Sulama hattı", "Temiz su girişine tıklayarak tarlayı besle.");
                break;
            case Demo.Facility:
                AddTarget("scrap", "Ham atık", 67f, 65f);
                AddTarget("tool_recycle", "Tesis", 190f, 65f);
                AddTarget("metal", "Malzeme", 314f, 65f);
                SetStatus("Atık işleme", "Önce ham atığa, ardından tesise tıkla.");
                break;
        }
    }

    private void Update()
    {
        if (root == null || !root.gameObject.activeInHierarchy) return;
        UpdateScale();
        if ((kind == Demo.Vacuum || kind == Demo.Seed) && Pressed(EcoKeys.Action.Mode)) ChangeMode();
        if (kind == Demo.Recycler && Pressed(EcoKeys.Action.Rotate)) ProcessRaw();
        if (kind == Demo.Hands && Pressed(EcoKeys.Action.Interact)) Harvest();
        if (kind == Demo.EnemyTin && Pressed(EcoKeys.Action.Interact)) ApplyFilter();
        if (kind == Demo.Flashlight && flashlight) UpdateFlashlight();
        if ((kind == Demo.EnemyTin || kind == Demo.Seed) && stunUntil > 0f && Time.unscaledTime > stunUntil)
        {
            stunUntil = -10f;
            targets[kind == Demo.EnemyTin ? 0 : 1].Caption.text = "Teneke maketi";
            if (kind == Demo.EnemyTin || mode == 1)
                SetStatus("Demir top", "Sersemleme bitti. Makete tekrar tıkla.");
        }
        if (harvestSeeds != null && Time.unscaledTime - shotAt > 1.65f) harvestSeeds.enabled = false;
        if (holdingIndex >= 0)
        {
            hold += Time.unscaledDeltaTime;
            if (hold >= HoldSeconds) FinishRecycling();
        }
        drawing.SetVerticesDirty();
    }

    private static bool Pressed(EcoKeys.Action action)
        => Keyboard.current?[EcoKeys.Get(action)].wasPressedThisFrame == true;

    private void UpdateScale()
    {
        var bounds = ((RectTransform)root.parent).rect;
        if (bounds.width <= 0f || bounds.height <= 0f) return;
        float scale = Mathf.Min(1f, bounds.width / Size.x, bounds.height / Size.y);
        root.localScale = Vector3.one * scale;
    }

    private void Press(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out var point) ||
            !board.rect.Contains(point)) return;
        int target = TargetAt(point);
        switch (kind)
        {
            case Demo.Vacuum: UseVacuum(point, target); break;
            case Demo.Seed: UseSeed(point, target); break;
            case Demo.Recycler:
                if (target == 0 && !targets[0].Reached)
                {
                    holdingIndex = 0;
                    hold = 0f;
                    SetStatus("İşleniyor", "Atığın üzerinde basılı tut.");
                }
                break;
            case Demo.Scanner: Scan(point, target); break;
            case Demo.Hands:
                if (target == 0 && !targets[0].Reached) SetStatus("Hasat", "Hasat etmek için etkileşim tuşunu kullan.");
                break;
            case Demo.Flashlight:
                lightAim = point;
                ToggleFlashlight();
                break;
            case Demo.EnemyTin:
                Shot(point, Gold);
                if (target == 0 && !targets[0].Reached)
                {
                    stunUntil = Time.unscaledTime + 4f;
                    targets[0].Caption.text = "Sersemledi";
                    SetStatus("Sersemledi", "Şimdi etkileşim tuşuyla küçük filtreyi tak.");
                }
                break;
            case Demo.Power:
            case Demo.Water:
                if (target >= 0) Connect(target);
                break;
            case Demo.Solar:
            case Demo.Wind:
            case Demo.Hydro:
                if (target == 0) ToggleGeneration();
                break;
            case Demo.Farm:
                if (target == 0 && completedLinks == 0)
                {
                    completedLinks = 1;
                    Mark(1, "crop_cabbage", "Sulandı");
                    SetStatus("Temiz su bağlı", "Hat, ekim yatağındaki bitkiyi suluyor.");
                }
                break;
            case Demo.Facility:
                if (target == completedLinks && target < 2)
                {
                    completedLinks++;
                    if (completedLinks == 1) SetStatus("Atık yüklendi", "İşlemek için tesise tıkla.");
                    else
                    {
                        Mark(2, "metal", "Malzeme hazır");
                        SetStatus("İşlendi", "Geri kazanılan malzeme çıkışa ulaştı.");
                    }
                }
                break;
        }
    }

    private void Drag(PointerEventData e)
    {
        if (holdingIndex < 0) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(board, e.position, e.pressEventCamera, out var point) ||
            TargetAt(point) != holdingIndex) Release(e);
    }

    private void Release(PointerEventData e)
    {
        if (holdingIndex < 0) return;
        holdingIndex = -1;
        hold = 0f;
        SetStatus("Geri dönüşüm", "İşlemi bitirmek için atığın üzerinde basılı tut.");
    }

    private int TargetAt(Vector2 point)
    {
        for (int i = 0; i < targets.Count; i++)
            if ((targets[i].Point - point).sqrMagnitude <= 31f * 31f) return i;
        return -1;
    }

    private void ChangeMode()
    {
        mode = (mode + 1) % (kind == Demo.Vacuum ? 3 : 2);
        DescribeMode();
    }

    private void DescribeMode()
    {
        if (kind == Demo.Vacuum)
        {
            string instruction = mode == 0 ? "Parçacıkları kendine çekmek için alana tıkla."
                : mode == 1 ? "Hava dalgasıyla dumanı dağıt." : "Fidana su püskürt.";
            SetStatus(VacuumModes[mode], instruction);
        }
        else SetStatus(mode == 0 ? "Tohum" : "Demir top", mode == 0
            ? "Örnek toprağa nişan alıp tohum gönder." : "Teneke maketine nişan alıp demir top gönder.");
    }

    private void UseVacuum(Vector2 point, int target)
    {
        Shot(point, mode == 0 ? Gold : mode == 1 ? Soot : Blue);
        if (target != mode || targets[target].Reached) return;
        if (mode == 0) Mark(target, "ui_check", "Toplandı");
        else if (mode == 1) Mark(target, "ui_check", "Dağıldı");
        else Mark(target, "crop_cabbage", "Sulandı");
        SetStatus(VacuumModes[mode], targets[target].Caption.text);
    }

    private void UseSeed(Vector2 point, int target)
    {
        Shot(point, mode == 0 ? Green : Gold);
        if (target != mode || targets[target].Reached) return;
        if (mode == 0)
        {
            Mark(0, "crop_cabbage", "Filiz");
            SetStatus("Tohum", "Tohum toprağa ulaştı ve filiz göründü.");
        }
        else
        {
            targets[1].Caption.text = "Sersemledi";
            stunUntil = Time.unscaledTime + 2f;
            SetStatus("Demir top", "Teneke kısa süre sersemledi.");
        }
    }

    private void FinishRecycling()
    {
        holdingIndex = -1;
        Mark(0, "metal", "Malzeme hazır");
        Shot(targets[0].Point, Green);
        SetStatus("İşlendi", "Atık, kullanılabilir malzemeye dönüştü.");
    }

    private void ProcessRaw()
    {
        if (raw == 0) return;
        raw = 0;
        Mark(1, "plastic", "Hazne · 0");
        Shot(targets[1].Point, Green);
        SetStatus("Ham atık işlendi", "Haznedeki örnek atık malzemeye dönüştü.");
    }

    private void Scan(Vector2 point, int target)
    {
        Shot(point, Blue);
        string result = target == 0 ? "Su kalitesi · Temiz" : target == 1 ? "Nem · Sulama gerekli"
            : target == 2 ? "Elektrik · Şebekeye bağlı" : "Bir örnek hedefin üzerine tıkla.";
        SetStatus("Tarama", result);
    }

    private void Harvest()
    {
        if (targets[0].Reached) return;
        Mark(0, "food", "Hasat edildi");
        harvestSeeds = EcoUI.Icon("Hasat tohumu", board, "seed", TopLeft, Center, 24f);
        harvestSeeds.rectTransform.anchoredPosition = targets[0].Point + new Vector2(34f, 14f);
        Shot(targets[0].Point, Green);
        SetStatus("Hasat tamam", "Ürün ve tohum alındı.");
    }

    private void ApplyFilter()
    {
        if (targets[0].Reached) return;
        if (Time.unscaledTime > stunUntil)
        {
            SetStatus("Küçük filtre", "Filtreyi takmadan önce Teneke’yi sersemlet.");
            return;
        }
        stunUntil = -10f;
        Mark(0, "filter", "Filtre takıldı");
        SetStatus("Etkisiz", "Küçük hava filtresi takıldı.");
    }

    private void ToggleFlashlight()
    {
        flashlight = !flashlight;
        UpdateFlashlight();
    }

    private void UpdateFlashlight()
    {
        if (Mouse.current != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(board,
            Mouse.current.position.ReadValue(), null, out var point) && board.rect.Contains(point)) lightAim = point;
        var item = targets[0];
        bool lit = flashlight && Vector2.Angle(lightAim - Origin, item.Point - Origin) < 18f;
        item.Icon.color = lit ? Paper : new Color(.29f, .32f, .29f, 1f);
        item.Caption.color = lit ? Paper : new Color(.47f, .49f, .43f, 1f);
        SetStatus(flashlight ? "Fener açık" : "Fener kapalı", flashlight
            ? "Işığı karanlık alanda fareyle yönlendir." : "Sol tıkla feneri aç.");
    }

    private void Connect(int target)
    {
        int max = targets.Count - 1;
        if (completedLinks == max) return;
        int a = completedLinks;
        int b = a + 1;
        if (firstLink < 0)
        {
            if (target != a && (kind != Demo.Power || target != b))
            {
                SetStatus("İlk uç", targets[a].Caption.text + " düğümünden başla.");
                return;
            }
            firstLink = target;
            SetStatus("İkinci uç", targets[target == a ? b : a].Caption.text + " düğümünü seç.");
            return;
        }
        if (target == firstLink)
        {
            firstLink = -1;
            SetStatus("Bağlantı iptal", "İlk ucu yeniden seçebilirsin.");
            return;
        }
        if (target != (firstLink == a ? b : a))
        {
            SetStatus("İkinci uç", "Gösterilen komşu düğümü seç.");
            return;
        }
        firstLink = -1;
        completedLinks++;
        if (completedLinks == max)
            SetStatus("Hat hazır", kind == Demo.Power ? "İstasyona elektrik ulaştı." : "Temiz su tarlaya ulaştı.");
        else SetStatus("Bağlandı", targets[completedLinks].Caption.text + " üzerinden sonraki hattı kur.");
    }

    private void SetGeneration(string source, string machine, string title, string active, string inactive)
    {
        AddTarget(source, active, 77f, 65f);
        AddTarget(machine, "Üretici", 286f, 65f);
        ButtonAt(inactive, new Vector2(232f, -2f), 74f, ToggleGeneration);
        energized = true;
        SetStatus(title, "Kaynak ve üretim durumunu karşılaştırmak için koşulu değiştir.");
        ToggleGeneration();
    }

    private void ToggleGeneration()
    {
        energized = !energized;
        var input = targets[0];
        if (kind == Demo.Solar)
        {
            EcoUI.SetIcon(input.Icon, energized ? "w_sun" : "w_cloud");
            input.Caption.text = energized ? "Güneşli" : "Gölgede";
        }
        else if (kind == Demo.Wind) input.Caption.text = energized ? "Kuvvetli" : "Sakin";
        else input.Caption.text = energized ? "Akış var" : "Akış yok";
        string lowOutput = kind == Demo.Wind ? "Üretim düşük" : "Üretim bekliyor";
        targets[1].Caption.text = energized ? "Elektrik üretiyor" : lowOutput;
        SetStatus(energized ? "Üretim etkin" : lowOutput, "Koşulu tekrar değiştirmek için kaynak simgesine tıkla.");
    }

    private void Mark(int index, string icon, string caption)
    {
        var t = targets[index];
        t.Reached = true;
        EcoUI.SetIcon(t.Icon, icon);
        t.Caption.text = caption;
    }

    private void Shot(Vector2 point, Color color)
    {
        endpoint = point;
        shotMode = mode;
        shotColor = color;
        shotAt = Time.unscaledTime;
    }

    private void SetStatus(string title, string instruction)
    {
        if (state.text != title) state.text = title;
        if (feedback.text != instruction) feedback.text = instruction;
    }

    private void Draw(VertexHelper mesh)
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < targets.Count; i++)
        {
            if (kind == Demo.Flashlight) break;
            var target = targets[i];
            bool selected = firstLink == i || holdingIndex == i;
            Color color = selected ? Gold : target.Reached ? Green : new Color(.53f, .4f, .27f, .33f);
            Ring(mesh, target.Point, selected ? 22f : 23f, color, selected ? 2.2f : 1f);
        }
        if (kind == Demo.Flashlight && flashlight)
        {
            Vector2 direction = lightAim - Origin;
            if (direction.sqrMagnitude < 1f) direction = Vector2.right;
            direction.Normalize();
            Vector2 side = Vector2.Perpendicular(direction);
            Vector2 far = Origin + direction * 380f;
            Triangle(mesh, Origin, far + side * 108f, far - side * 108f, new Color(.98f, .86f, .51f, .34f));
        }
        if (holdingIndex >= 0)
            Ring(mesh, targets[holdingIndex].Point, 25f, Green, 3f, Mathf.Clamp01(hold / HoldSeconds));
        if (kind == Demo.Power || kind == Demo.Water || kind == Demo.Farm)
        {
            for (int i = 0; i < completedLinks; i++)
            {
                Color color = kind == Demo.Power ? Gold : kind == Demo.Water && i == 0 ? new Color32(121, 78, 49, 255) : Blue;
                Flow(mesh, i, color, now);
            }
        }
        if (kind == Demo.Facility)
            for (int i = 0; i < completedLinks; i++) Flow(mesh, i, Green, now);
        if ((kind == Demo.Solar || kind == Demo.Wind || kind == Demo.Hydro) && energized)
            Flow(mesh, 0, kind == Demo.Hydro ? Blue : Gold, now);
        if (now < stunUntil) Ring(mesh, targets[kind == Demo.EnemyTin ? 0 : 1].Point, 26f, Gold, 2f);

        float age = now - shotAt;
        if (age < 0f || age > .72f) return;
        float t = age / .72f;
        Color ink = shotColor;
        ink.a = 1f - t;
        if (kind == Demo.Scanner)
        {
            Ring(mesh, endpoint, Mathf.Lerp(3f, 39f, t), ink, 2f);
            Ring(mesh, endpoint, Mathf.Lerp(2f, 26f, t), ink, 1f);
            return;
        }
        if (kind == Demo.Vacuum && shotMode == 1)
        {
            Vector2 side = Vector2.Perpendicular((endpoint - Origin).normalized);
            for (int i = -1; i <= 1; i++)
            {
                float head = Mathf.Clamp01(t * 2f - Mathf.Abs(i) * .11f);
                Vector2 center = Vector2.Lerp(Origin, endpoint, head) + side * i * head * 17f;
                Stroke(mesh, Vector2.Lerp(Origin, center, .66f), center, ink, 1.8f);
            }
            Ring(mesh, endpoint, 4f + 19f * t, ink, 1.4f);
            return;
        }
        if (kind == Demo.Vacuum)
        {
            Vector2 side = Vector2.Perpendicular((endpoint - Origin).normalized);
            for (int i = 0; i < 12; i++)
            {
                float distance = Mathf.Clamp01(t * 2f - i * .07f);
                float spread = Mathf.Sin(i * 2.7f) * Mathf.Sin(distance * Mathf.PI) * (shotMode == 2 ? 21f : 14f);
                Vector2 position = Vector2.Lerp(shotMode == 0 ? endpoint : Origin, shotMode == 0 ? Origin : endpoint, distance) + side * spread;
                Dot(mesh, position, shotMode == 2 ? 2.9f : 2.3f, ink);
            }
        }
        else
        {
            bool projectile = kind == Demo.Seed || kind == Demo.EnemyTin;
            Vector2 point = projectile ? Vector2.Lerp(Origin, endpoint, Mathf.Clamp01(t * 2.8f)) : endpoint;
            Dot(mesh, point, 4.5f, ink);
            for (int i = 0; i < 6; i++)
                Dot(mesh, point + new Vector2(Mathf.Cos(i), Mathf.Sin(i)) * (5f + t * 28f), 2.2f, ink);
        }
    }

    private void Flow(VertexHelper mesh, int index, Color color, float now)
    {
        Vector2 a = targets[index].Point;
        Vector2 b = targets[index + 1].Point;
        Vector2 direction = (b - a).normalized;
        a += direction * 26f;
        b -= direction * 26f;
        Stroke(mesh, a, b, color, 2.2f);
        Vector2 side = Vector2.Perpendicular(direction);
        for (int i = 0; i < 3; i++)
        {
            Vector2 point = Vector2.Lerp(a, b, Mathf.Repeat((now - shownAt) * .62f + i / 3f, 1f));
            Stroke(mesh, point - direction * 5f + side * 4f, point, color, 2f);
            Stroke(mesh, point - direction * 5f - side * 4f, point, color, 2f);
        }
    }

    private static void Ring(VertexHelper mesh, Vector2 center, float radius, Color color, float width, float amount = 1f)
    {
        const int segments = 28;
        var previous = center + Vector2.up * radius;
        for (int i = 1; i <= Mathf.CeilToInt(segments * amount); i++)
        {
            float angle = Mathf.PI * 2f * Mathf.Min(i / (float)segments, amount);
            var next = center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
            Stroke(mesh, previous, next, color, width);
            previous = next;
        }
    }

    private static void Dot(VertexHelper mesh, Vector2 point, float radius, Color color)
    {
        const int sides = 8;
        int start = mesh.currentVertCount;
        mesh.AddVert(point, color, Vector2.zero);
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            mesh.AddVert(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.zero);
        }
        for (int i = 0; i < sides; i++) mesh.AddTriangle(start, start + 1 + i, start + 1 + ((i + 1) % sides));
    }

    private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, Color color, float width)
    {
        Vector2 side = Vector2.Perpendicular((b - a).normalized) * (width * .5f);
        int start = mesh.currentVertCount;
        mesh.AddVert(a + side, color, Vector2.zero);
        mesh.AddVert(a - side, color, Vector2.zero);
        mesh.AddVert(b - side, color, Vector2.zero);
        mesh.AddVert(b + side, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
        mesh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(start, start + 1, start + 2);
    }

    private void Emitter(string icon)
    {
        var image = EcoUI.Icon("Örnek araç", board, icon, TopLeft, Center, 48f);
        image.rectTransform.anchoredPosition = Origin + Vector2.left * 22f;
        image.color = Paper;
    }

    private void AddTarget(string icon, string caption, float x, float y)
    {
        var point = new Vector2(x, -y);
        var image = EcoUI.Icon(caption, board, icon, TopLeft, Center, 38f);
        image.rectTransform.anchoredPosition = point;
        var label = TextAt(caption, board, point + new Vector2(-47f, -24f), new Vector2(94f, 23f), 12, Ink, true, TextAnchor.MiddleCenter);
        targets.Add(new Target { Point = point, Icon = image, Caption = label });
    }

    private void Binding(EcoKeys.Action action, string description, Action run)
    {
        ButtonAt(EcoKeys.Label(action) + " · " + description, new Vector2(232f, -2f), 74f, run);
    }

    private void ButtonAt(string caption, Vector2 point, float width, Action run, bool primary = true)
    {
        var bg = EcoUI.Panel(caption, root, TopLeft, TopLeft, primary ? Green : KeyColor);
        EcoUI.Place(bg.rectTransform, point, new Vector2(width, 27f));
        var button = bg.gameObject.AddComponent<Button>();
        button.targetGraphic = bg;
        // A rebound demonstration key can also be the UI Submit key.
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => run());
        TextAt(caption, bg.rectTransform, new Vector2(2f, 0f), new Vector2(width - 4f, 27f), 12,
            Paper, true, TextAnchor.MiddleCenter);
    }

    private static Text TextAt(string value, RectTransform parent, Vector2 position, Vector2 size, int fontSize,
        Color color, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var label = EcoUI.Label(value, parent, value, fontSize, align, color, bold);
        label.supportRichText = false;
        EcoUI.Layout(label, position, size);
        return label;
    }

    private static void Pointer(EventTrigger trigger, EventTriggerType eventType, Action<PointerEventData> callback)
    {
        var entry = new EventTrigger.Entry { eventID = eventType };
        entry.callback.AddListener(e => callback((PointerEventData)e));
        trigger.triggers.Add(entry);
    }

    private sealed class InkLayer : MaskableGraphic
    {
        internal Action<VertexHelper> Draw;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Draw?.Invoke(mesh);
        }
    }
}
