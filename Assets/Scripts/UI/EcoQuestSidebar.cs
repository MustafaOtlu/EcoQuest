using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Tone = EcoHudStyle.Tone;

// Local jobs remain part of the HUD; reading an instruction never opens a modal or pauses the world.
public sealed class EcoQuestSidebar
{
    private const float Margin = 16f, RowHeight = 48f;
    private readonly EcoHudStyle hudStyle;
    private readonly EcoUI.Card card;
    private readonly RectTransform viewport, content, details, detailViewport, detailContent;
    private readonly ScrollRect listScroll, detailScroll;
    private readonly Text empty, detailTitle, detailState, detailBody, trackText;
    private readonly Image detailIcon;
    private readonly Button track;
    private readonly List<Row> rows = new List<Row>();
    private List<EcoQuestBoard.Quest> quests = new List<EcoQuestBoard.Quest>();
    private EcoRegion region;
    private string selectedId = "";
    private float nextRefresh, top, width, available, screenWidth, screenHeight, dockTop;
    private bool visible = true, hasLayout;

    private sealed class Row
    {
        public RectTransform root, meter;
        public Image background, icon, fill;
        public Text title, level;
        public EcoQuestBoard.Quest quest;
    }

    public RectTransform Root => card.root;
    public RectTransform DetailsRoot => details;

    internal EcoQuestSidebar(Transform parent, EcoHudStyle hudStyle)
    {
        this.hudStyle = hudStyle;
        card = new EcoUI.Card("local_quests", parent, new Vector2(0, 1), new Vector2(0, 1), "poi_quest", "Görevler",
            "Bu bölgedeki görevler. Bir göreve tıkla, nasıl yapılacağını oku.");
        hudStyle.Card(card);
        card.toggle.onClick.AddListener(() => { if (card.IsCollapsed) selectedId = ""; Relayout(); });
        var list = EcoUI.Panel("Task viewport", card.body, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        viewport = list.rectTransform;
        list.gameObject.AddComponent<RectMask2D>();
        content = EcoUI.Rect("Task rows", viewport, new Vector2(0, 1), new Vector2(0, 1));
        listScroll = Scroller(viewport, content);
        empty = EcoUI.Label("No local tasks", card.body, "Bu bölgede açık görev yok.", 13, TextAnchor.MiddleCenter, EcoUI.Muted);

        hudStyle.Tint(empty, Tone.Muted);
        details = hudStyle.Surface(EcoUI.Panel("Task instructions", parent, new Vector2(0, 1), new Vector2(0, 1), EcoUI.Surface)).rectTransform;
        var layer = details.gameObject.AddComponent<Canvas>(); layer.overrideSorting = true; layer.sortingOrder = 26;
        details.gameObject.AddComponent<GraphicRaycaster>();
        detailIcon = EcoUI.Icon("Task icon", details, "poi_quest", new Vector2(0, 1), new Vector2(0, 1), 26);
        detailTitle = EcoUI.Label("Task title", details, "", 17, TextAnchor.UpperLeft, EcoUI.Accent, true);
        detailState = EcoUI.Label("Current state", details, "", 12, TextAnchor.UpperLeft, EcoUI.Muted);
        hudStyle.Tint(detailTitle, Tone.Text);
        hudStyle.Tint(detailState, Tone.Muted);
        var close = EcoUI.IconButton("Close instructions", details, "ui_close", 26, () => { selectedId = ""; Relayout(); }, "Görev açıklamasını kapat");
        hudStyle.Tint(close.targetGraphic, Tone.Raised);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
        closeRect.anchoredPosition = new Vector2(-14, -16);
        var instructionArea = EcoUI.Panel("Instructions viewport", details, new Vector2(0, 1), new Vector2(0, 1), Color.clear);
        detailViewport = instructionArea.rectTransform; instructionArea.gameObject.AddComponent<RectMask2D>();
        detailContent = EcoUI.Rect("Instructions content", detailViewport, new Vector2(0, 1), new Vector2(0, 1));
        detailScroll = Scroller(detailViewport, detailContent);
        detailBody = EcoUI.Label("How to", detailContent, "", 14, TextAnchor.UpperLeft, EcoUI.Ink);
        hudStyle.Tint(detailBody, Tone.Text);
        var trackImage = EcoUI.Panel("Track task", details, new Vector2(0, 1), new Vector2(0, 1), EcoUI.Accent);
        track = trackImage.gameObject.AddComponent<Button>(); track.targetGraphic = trackImage;
        hudStyle.Tint(trackImage, Tone.Raised);
        ButtonColors(track);
        track.onClick.AddListener(() => { var quest = Selected(); if (quest == null) return; EcoQuestBoard.Toggle(quest); Relayout(); });
        trackText = EcoUI.Label("Track label", track.transform, "Takip et · Ok yol göstersin", 13, TextAnchor.MiddleCenter, EcoUI.PaperInk, true);
        hudStyle.Tint(trackText, Tone.Leaf);
        EcoUI.RegisterWorldLabelObstacle(card.root); EcoUI.RegisterWorldLabelObstacle(details);
        details.gameObject.SetActive(false);
    }

    private static ScrollRect Scroller(RectTransform viewport, RectTransform content)
    {
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = false; scroll.scrollSensitivity = 34;
        return scroll;
    }

    private static void ButtonColors(Button button)
    {
        var colors = button.colors; colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f); colors.pressedColor = new Color(0.84f, 0.87f, 0.82f);
        button.colors = colors;
    }

    public void Refresh(EcoRegion current, Vector3 from)
    {
        bool changedRegion = current != region;
        if (!changedRegion && Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f; region = current;
        quests = EcoQuestBoard.For(current, from);
        if (changedRegion) { selectedId = ""; listScroll.verticalNormalizedPosition = 1; }
        if (Selected() == null) selectedId = "";
        card.summary.text = quests.Count.ToString();
        empty.text = current == null ? "Bir bölgeye girince görevler burada görünür." : "Bu bölgede açık görev yok. Çevreyi korumaya devam et.";
        Relayout();
    }

    public void Layout(float top, float width, float availableHeight, float viewWidth, float viewHeight, float dockTop)
    {
        this.top = top; this.width = width; available = availableHeight;
        screenWidth = viewWidth; screenHeight = viewHeight; this.dockTop = dockTop; hasLayout = true;
        Relayout();
    }

    public void SetVisible(bool shown)
    {
        if (visible == shown) return;
        visible = shown;
        card.SetVisible(shown && (!hasLayout || available >= EcoUI.Card.HeaderHeight));
        details.gameObject.SetActive(shown && !card.IsCollapsed && Selected() != null);
    }

    private EcoQuestBoard.Quest Selected()
    {
        if (string.IsNullOrEmpty(selectedId)) return null;
        foreach (var quest in quests) if (quest.id == selectedId) return quest;
        return null;
    }

    private Row GetRow(int index)
    {
        while (rows.Count <= index)
        {
            var row = new Row();
            row.background = EcoUI.Panel("Local task", content, new Vector2(0, 1), new Vector2(0, 1), EcoUI.SurfaceRaised);
            row.root = row.background.rectTransform;
            var button = row.root.gameObject.AddComponent<Button>(); button.targetGraphic = row.background; ButtonColors(button);
            button.onClick.AddListener(() =>
            {
                if (row.quest == null) return;
                bool newSelection = selectedId != row.quest.id;
                selectedId = newSelection ? row.quest.id : "";
                if (newSelection) detailScroll.verticalNormalizedPosition = 1;
                Relayout();
            });
            row.icon = EcoUI.Icon("Icon", row.root, "poi_quest", new Vector2(0, 1), new Vector2(0, 1), 23);
            row.title = EcoUI.Label("Title", row.root, "", 13, TextAnchor.MiddleLeft, EcoUI.Ink, true);
            row.level = EcoUI.Label("Difficulty", row.root, "", 10, TextAnchor.MiddleLeft, EcoUI.Muted);
            row.fill = EcoUI.Meter("Progress", row.root, out row.meter);
            hudStyle.Tint(row.title, Tone.Text);
            hudStyle.Tint(row.level, Tone.Muted);
            hudStyle.Tint(row.meter.GetComponent<Image>(), Tone.Rule);
            rows.Add(row);
        }
        return rows[index];
    }

    private void Relayout()
    {
        if (!hasLayout) return;
        bool show = visible && available >= EcoUI.Card.HeaderHeight;
        card.SetVisible(show);
        float wantedBody = quests.Count == 0 ? 66 : Mathf.Min(5 * RowHeight + 20, quests.Count * RowHeight + 20);
        float bodyHeight = Mathf.Min(wantedBody, Mathf.Max(0, available - EcoUI.Card.HeaderHeight));
        bool overflow = !card.IsCollapsed && quests.Count * RowHeight > Mathf.Max(0, bodyHeight - 20) + 1;
        card.summary.text = quests.Count + (overflow ? " · Kaydır" : "");
        card.Layout(new Vector2(Margin, -top), card.IsCollapsed ? EcoQuestHUD.CompactLeftWidth : width, bodyHeight, true);
        EcoUI.Place(viewport, new Vector2(12, -8), new Vector2(width - 24, Mathf.Max(0, bodyHeight - 20)));
        EcoUI.Place(content, content.anchoredPosition, new Vector2(width - 24, quests.Count * RowHeight));
        for (int i = 0; i < quests.Count; i++)
        {
            var row = GetRow(i); var quest = quests[i]; row.quest = quest; row.root.gameObject.SetActive(true);
            bool selected = quest.id == selectedId, tracked = EcoQuestBoard.IsTracked(quest);
            EcoUI.Place(row.root, new Vector2(0, -i * RowHeight), new Vector2(width - 24, RowHeight - 3));
            row.background.color = selected ? hudStyle.ColorFor(Tone.Raised) : Color.clear;
            EcoUI.SetIcon(row.icon, quest.icon); EcoUI.LayoutIcon(row.icon, new Vector2(7, -8), 23);
            row.title.text = quest.title;
            EcoUI.Layout(row.title, new Vector2(38, -3), new Vector2(width - 70, 28));
            row.level.text = EcoQuestBoard.LevelNames[Mathf.Clamp(quest.level, 1, 3)] + (tracked ? " · Takipte" : " · Nasıl yapılır? ›");
            EcoUI.Layout(row.level, new Vector2(38, -29), new Vector2(width - 70, 13));
            row.meter.gameObject.SetActive(quest.progress >= 0);
            if (quest.progress >= 0)
            {
                EcoUI.Place(row.meter, new Vector2(38, -42), new Vector2(width - 70, 3));
                EcoUI.SetMeter(row.fill, row.meter, quest.progress, hudStyle.ColorFor(selected ? Tone.Amber : Tone.Leaf));
            }
        }
        for (int i = quests.Count; i < rows.Count; i++) { rows[i].quest = null; rows[i].root.gameObject.SetActive(false); }
        empty.gameObject.SetActive(quests.Count == 0);
        EcoUI.Layout(empty, new Vector2(18, -8), new Vector2(width - 36, Mathf.Max(22, bodyHeight - 20)));
        ShowInstructions(show && !card.IsCollapsed ? Selected() : null);
    }

    private void ShowInstructions(EcoQuestBoard.Quest quest)
    {
        details.gameObject.SetActive(quest != null);
        if (quest == null) return;
        float popupWidth = Mathf.Min(360, screenWidth - width - Margin * 3);
        popupWidth = Mathf.Max(260, popupWidth);
        detailTitle.text = quest.title; detailState.text = quest.detail; detailBody.text = EcoQuestBoard.HowTo(quest);
        EcoUI.SetIcon(detailIcon, quest.icon);
        float titleHeight = Height(detailTitle, popupWidth - 104, 24);
        float stateHeight = Mathf.Min(80, Height(detailState, popupWidth - 36, 18));
        float bodyHeight = Height(detailBody, popupWidth - 44, 40);
        float header = 20 + titleHeight + 10 + stateHeight + 16;
        float maxBottom = screenHeight - dockTop - 12;
        float popupHeight = Mathf.Min(430, maxBottom - 96, header + Mathf.Min(bodyHeight, 240) + 76);
        popupHeight = Mathf.Max(header + 92, popupHeight);
        float popupTop = Mathf.Clamp(top, 96, Mathf.Max(96, maxBottom - popupHeight));
        float x = Mathf.Min(Margin + width + 12, screenWidth - popupWidth - Margin);
        EcoUI.Place(details, new Vector2(x, -popupTop), new Vector2(popupWidth, popupHeight));
        EcoUI.LayoutIcon(detailIcon, new Vector2(18, -20), 26);
        EcoUI.Layout(detailTitle, new Vector2(54, -20), new Vector2(popupWidth - 104, titleHeight));
        EcoUI.Layout(detailState, new Vector2(18, -(30 + titleHeight)), new Vector2(popupWidth - 36, stateHeight));
        EcoUI.Place(detailViewport, new Vector2(18, -header), new Vector2(popupWidth - 36, popupHeight - header - 70));
        EcoUI.Place(detailContent, detailContent.anchoredPosition, new Vector2(popupWidth - 36, bodyHeight + 4));
        EcoUI.Layout(detailBody, new Vector2(0, 0), new Vector2(popupWidth - 44, bodyHeight + 4));
        EcoUI.Place((RectTransform)track.transform, new Vector2(18, -(popupHeight - 56)), new Vector2(popupWidth - 36, 36));
        trackText.text = EcoQuestBoard.IsTracked(quest) ? "Takipte · Takibi bırak" : "Takip et · Ok yol göstersin";
        EcoUI.Layout(trackText, new Vector2(8, -5), new Vector2(popupWidth - 52, 26));
    }

    private static float Height(Text text, float width, float minimum)
    {
        text.rectTransform.sizeDelta = new Vector2(width * EcoUI.RasterScale, 0);
        return Mathf.Max(minimum, text.preferredHeight / EcoUI.RasterScale + 2);
    }

    public void Dispose()
    {
        if (card.root != null) Destroy(card.root.gameObject);
        if (details != null) Destroy(details.gameObject);
    }

    private static void Destroy(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
    }
}
