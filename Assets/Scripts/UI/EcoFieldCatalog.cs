using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Catalog = EcoFieldCatalogData;

[DefaultExecutionOrder(-70)]
public sealed partial class EcoFieldCatalog : MonoBehaviour
{
    private static readonly Vector2 TopLeft = new Vector2(0, 1);
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Color Ink = new Color32(54, 39, 26, 255);
    private static readonly Color Muted = new Color32(104, 77, 49, 255);
    private static readonly Color Paper = new Color32(255, 236, 194, 255);
    private static readonly Color Control = new Color32(130, 98, 56, 43);
    private static readonly string[] Names = { "Düşmanlar", "Araçlar", "Yapılar", "Bölgeler" };

    private EcoWorldHUD worldHud;
    private EcoFieldCatalogDemo demo;
    private Canvas canvas;
    private CanvasScaler scaler;
    private Font bookFont;
    private Texture2D idleTab, activeTab;
    private RectTransform book, right, page, index, demonstration, navigation;
    private CanvasGroup pageMotion;
    private RawImage drawing;
    private Image drawingFallback;
    private Text title, heading, description, source, pageCount, indexCaption;
    private Button forward, back;
    private readonly RawImage[] tabs = new RawImage[Names.Length];
    private readonly Text[] tabLabels = new Text[Names.Length];
    private readonly Button[] rows = new Button[12];
    private readonly Image[] rowIcons = new Image[12];
    private readonly Text[] rowLabels = new Text[12];
    private Catalog.Category category;
    private Catalog.Entry entry;
    private int pageIndex, turnDirection;
    private float turnStarted;

    internal void Bind(EcoWorldHUD hud) => worldHud = hud;

    internal void Open()
    {
        if (worldHud == null || EcoUIFlow.InputBlocked || EcoMarketUI.BlocksGameplay) return;
        if (canvas == null) Build();
        EventSystem.current?.SetSelectedGameObject(null);
        EcoKeys.CancelFire();
        EcoUIFlow.SetPanel(this, true, freezeWorld: true);
        canvas.gameObject.SetActive(true);
        SelectCategory(Catalog.Category.Enemies);
        Fit();
    }

    private void Close()
    {
        if (!EcoUIFlow.IsPanel(this)) return;
        demo.Clear();
        EcoKeys.CancelFire();
        EcoUIFlow.SetPanel(this, false);
        canvas.gameObject.SetActive(false);
    }

    private void OnDisable() => Close();

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (bookFont != null) Destroy(bookFont);
    }

    private void Update()
    {
        if (!EcoUIFlow.IsPanel(this)) return;
        if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            EcoUIFlow.ConsumeEscape();
            if (index.gameObject.activeSelf) ShowIndex(false);
            else Close();
            return;
        }
        Fit();
        if (turnDirection == 0) return;
        float t = Mathf.Clamp01((Time.unscaledTime - turnStarted) / 0.22f);
        float ease = 1f - (1f - t) * (1f - t) * (1f - t);
        pageMotion.alpha = Mathf.Lerp(0.28f, 1f, ease);
        page.anchoredPosition = new Vector2(Mathf.Lerp(18f * turnDirection, 0f, ease), 0f);
        if (t >= 1f) turnDirection = 0;
    }

    private void Fit()
    {
        var pixels = canvas.pixelRect.size;
        scaler.scaleFactor = Mathf.Max(0.1f, Mathf.Min(pixels.x / 1125f, pixels.y / 665f));
    }

    private void Build()
    {
        bookFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Liberation Serif", "Times New Roman" }, 24);
        canvas = EcoUI.CreateCanvas("Saha kataloğu", 90, out scaler);
        canvas.transform.SetParent(transform, false);
        var shade = EcoUI.Fill("Catalogue backdrop", canvas.transform, new Color(0.035f, 0.052f, 0.045f, 0.78f));
        shade.raycastTarget = true;
        EcoUI.Stretch(shade.rectTransform);
        book = EcoUI.Rect("Open book", shade.transform, Center, Center);
        var spread = new Vector2(1000, 633);
        EcoUI.Place(book, new Vector2(-44, 0), spread);
        BuildTabs();
        var cover = Picture("Parchment spread", book, Vector2.zero, spread, TextureFor("Book"));
        cover.color = cover.texture != null ? Color.white : Paper;
        var illustration = Box("Graphite illustration", book, new Vector2(58, -64), new Vector2(406, 505));
        drawing = Picture("Graphite", illustration, Vector2.zero, illustration.sizeDelta, null);
        drawingFallback = EcoUI.Icon("Specimen symbol", illustration, "", Center, Center, 165);
        drawingFallback.color = Ink;
        right = Box("Right leaf", book, new Vector2(538, -52), new Vector2(406, 529));
        page = Box("Right page content", right, Vector2.zero, right.sizeDelta);
        pageMotion = page.gameObject.AddComponent<CanvasGroup>();
        title = Print("Record title", page, "", new Vector2(0, -43), new Vector2(406, 55), 40, Ink, true, TextAnchor.MiddleCenter);
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 24 * EcoUI.RasterScale;
        title.resizeTextMaxSize = 40 * EcoUI.RasterScale;
        Stroke(page, new Vector2(16, -108), new Vector2(374, 1));
        heading = Print("Section", page, "", new Vector2(0, -123), new Vector2(406, 34), 26, Ink, true);
        description = Print("Explanation", page, "", new Vector2(0, -163), new Vector2(406, 93), 20, Ink, false, TextAnchor.UpperLeft);
        source = Print("Source", page, "", new Vector2(0, -424), new Vector2(406, 32), 14, Muted);
        demonstration = Box("Page simulation", page, new Vector2(3, -228), new Vector2(400, 218));
        demo = gameObject.AddComponent<EcoFieldCatalogDemo>();
        BuildMapAndCues();
        BuildNavigation();
        BuildIndex();
        Action("Kayıtlar", right, Vector2.zero, new Vector2(118, 31), () => ShowIndex(!index.gameObject.activeSelf), out indexCaption);
        Action("×", right, new Vector2(377, 0), new Vector2(29, 29), Close, out _).gameObject.name = "Kitabı kapat";
    }

    private void BuildTabs()
    {
        idleTab = TextureFor("Tab");
        activeTab = TextureFor("TabSelected");
        for (int i = 0; i < tabs.Length; i++)
        {
            var value = (Catalog.Category)i;
            tabs[i] = Picture("Ayraç " + Names[i], book, new Vector2(966, -75 - i * 57), new Vector2(119, 46), idleTab);
            tabLabels[i] = Print("Category", tabs[i].transform, Names[i], new Vector2(29, 0), new Vector2(89, 44), 13, Ink, true, TextAnchor.MiddleCenter);
            Click(tabs[i], () => SelectCategory(value));
        }
    }

    private void BuildNavigation()
    {
        navigation = Box("Page controls", right, new Vector2(215, -464), new Vector2(226, 83));
        back = Action("↶", navigation, new Vector2(0, -29), new Vector2(39, 31), () => Turn(-1), out _);
        back.gameObject.name = "Önceki sayfa";
        pageCount = Print("Page count", navigation, "", new Vector2(49, -28), new Vector2(64, 33), 17, Ink, true, TextAnchor.MiddleCenter);
        var curl = Picture("Sayfayı çevir", navigation, new Vector2(123, -3), new Vector2(89, 83), TextureFor("PageCurl"));
        forward = Click(curl, () => Turn(1));
        if (curl.texture == null) Print("Forward arrow", curl.transform, "↗", new Vector2(11, -20), new Vector2(55, 40), 28, Ink, true, TextAnchor.MiddleCenter);
    }

    private void BuildIndex()
    {
        index = Box("Record list", right, new Vector2(0, -49), new Vector2(406, 474));
        Print("Index title", index, "Bu bölümdeki kayıtlar", Vector2.zero, new Vector2(406, 38), 26, Ink, true);
        Stroke(index, new Vector2(0, -43), new Vector2(406, 1));
        for (int i = 0; i < rows.Length; i++)
        {
            int slot = i;
            rows[i] = Action("Kayıt " + (i + 1), index, new Vector2((i % 2) * 207, -58 - (i / 2) * 59), new Vector2(198, 50),
                () => SelectEntry(Catalog.GetEntries(category)[slot]), out rowLabels[i]);
            rowIcons[i] = EcoUI.Icon("Record icon", rows[i].transform, "", TopLeft, TopLeft, 24);
            EcoUI.LayoutIcon(rowIcons[i], new Vector2(9, -13), 24);
            rowLabels[i].alignment = TextAnchor.MiddleLeft;
            EcoUI.Layout(rowLabels[i], new Vector2(39, 0), new Vector2(151, 50));
        }
    }

    private void SelectCategory(Catalog.Category value)
    {
        category = value;
        for (int i = 0; i < tabs.Length; i++)
        {
            bool selected = i == (int)value;
            tabs[i].texture = selected ? activeTab : idleTab;
            tabs[i].color = tabs[i].texture != null ? Color.white : selected ? EcoUI.Selected : Paper;
            tabLabels[i].color = selected ? Paper : Ink;
        }
        SelectEntry(Catalog.GetEntries(category)[0]);
    }

    private void SelectEntry(Catalog.Entry next)
    {
        entry = next;
        pageIndex = 0;
        var texture = TextureFor("Illustrations/" + entry.Id);
        drawing.texture = texture;
        drawing.enabled = texture != null;
        EcoUI.SetIcon(drawingFallback, entry.Icon);
        drawingFallback.enabled &= texture == null;
        if (texture != null)
        {
            var bounds = ((RectTransform)drawing.transform.parent).sizeDelta;
            float scale = Mathf.Min(bounds.x / texture.width, bounds.y / texture.height);
            var size = new Vector2(texture.width, texture.height) * scale;
            EcoUI.Place(drawing.rectTransform, Vector2.Scale(bounds - size, new Vector2(0.5f, -0.5f)), size);
        }
        ShowIndex(false);
    }

    private void ShowIndex(bool open)
    {
        index.gameObject.SetActive(open);
        page.gameObject.SetActive(!open);
        indexCaption.text = open ? "‹  Kayda dön" : "Kayıtlar  ▾";
        if (!open) { DrawPage(0); return; }
        demo.Clear();
        navigation.gameObject.SetActive(false);
        var entries = Catalog.GetEntries(category);
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i].gameObject.SetActive(i < entries.Count);
            if (i >= entries.Count) continue;
            rowLabels[i].text = entries[i].Title;
            rows[i].targetGraphic.color = entries[i] == entry ? new Color32(88, 105, 61, 65) : Control;
            EcoUI.SetIcon(rowIcons[i], entries[i].Icon);
        }
    }

    private void Turn(int direction)
    {
        int next = pageIndex + direction;
        if (next < 0 || next >= entry.Pages.Length) return;
        pageIndex = next;
        DrawPage(direction);
    }

    private void DrawPage(int direction)
    {
        demo.Clear();
        Catalog.Page current = entry.Pages[pageIndex];
        title.text = EcoUI.Upper(entry.Title);
        heading.text = current.Heading;
        source.text = current.Source;
        description.text = current.Body;
        bool hasHeading = current.Heading.Length > 0;
        bool map = current.Demo == Catalog.Demo.Region;
        bool interactive = current.Demo != Catalog.Demo.None && !map;
        EcoUI.Layout(description, new Vector2(0, hasHeading ? -161 : -126), new Vector2(406, hasHeading && interactive ? 92 : 100));
        description.resizeTextForBestFit = true;
        description.resizeTextMinSize = 15 * EcoUI.RasterScale;
        description.resizeTextMaxSize = 20 * EcoUI.RasterScale;
        mapFrame.gameObject.SetActive(map);
        demonstration.gameObject.SetActive(interactive);
        if (map) UpdateRegionMap();
        if (interactive)
        {
            EcoUI.Place(demonstration, new Vector2(3, hasHeading ? -253 : -228), new Vector2(400, 218));
            demo.Show(demonstration, current.Demo, entry.Id);
        }
        DrawCues(current, map, interactive);
        navigation.gameObject.SetActive(entry.Pages.Length > 1);
        forward.gameObject.SetActive(pageIndex + 1 < entry.Pages.Length);
        back.gameObject.SetActive(pageIndex > 0);
        pageCount.text = (pageIndex + 1) + " / " + entry.Pages.Length;
        turnStarted = Time.unscaledTime;
        turnDirection = direction;
        pageMotion.alpha = direction == 0 ? 1f : 0.28f;
        page.anchoredPosition = new Vector2(18f * direction, 0f);
    }
}
