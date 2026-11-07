using UnityEngine;
using UnityEngine.UI;
using Catalog = EcoFieldCatalogData;

public sealed partial class EcoFieldCatalog
{
    private static readonly Color Rule = new Color32(117, 86, 51, 100);
    private RectTransform mapFrame, cueGroup;
    private RawImage mapImage;
    private Image mapHighlight;
    private readonly Image[] cueIcons = new Image[3];
    private readonly Text[] cueLabels = new Text[3];

    private void BuildMapAndCues()
    {
        mapFrame = Box("Region map", page, new Vector2(0, -236), new Vector2(207, 207));
        var plate = EcoUI.Panel("Map border", mapFrame, TopLeft, TopLeft, Ink, false);
        EcoUI.Stretch(plate.rectTransform);
        mapImage = Picture("Map", mapFrame, new Vector2(3, -3), new Vector2(201, 201), worldHud.minimapTexture);
        mapHighlight = EcoUI.Fill("Selected region", mapImage.transform, new Color32(244, 193, 84, 102));
        var edge = mapHighlight.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color32(78, 55, 21, 255);
        edge.effectDistance = new Vector2(1.5f, 1.5f);
        cueGroup = Box("Page cues", page, Vector2.zero, new Vector2(406, 201));
        for (int i = 0; i < cueIcons.Length; i++)
        {
            cueIcons[i] = EcoUI.Icon("Cue icon", cueGroup, "", TopLeft, TopLeft, 36);
            cueLabels[i] = Print("Cue label", cueGroup, "", Vector2.zero, new Vector2(124, 41), 15, Ink, true, TextAnchor.MiddleCenter);
        }
    }

    private void DrawCues(Catalog.Page content, bool map, bool interactive)
    {
        cueGroup.gameObject.SetActive(!interactive && content.Icons.Length > 0);
        EcoUI.Place(cueGroup, new Vector2(map ? 222 : 0, map ? -237 : -298), new Vector2(map ? 184 : 406, 201));
        for (int i = 0; i < cueIcons.Length; i++)
        {
            bool show = i < content.Icons.Length;
            cueIcons[i].gameObject.SetActive(show);
            cueLabels[i].gameObject.SetActive(show);
            if (!show) continue;
            float column = i * 139;
            EcoUI.SetIcon(cueIcons[i], content.Icons[i]);
            EcoUI.LayoutIcon(cueIcons[i], map ? new Vector2(2, -14 - i * 63) : new Vector2(column + 45, 0), map ? 34 : 38);
            cueLabels[i].text = content.Labels[i];
            cueLabels[i].alignment = map ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            EcoUI.Layout(cueLabels[i], map ? new Vector2(44, -9 - i * 63) : new Vector2(column, -43), new Vector2(map ? 133 : 125, 42));
        }
    }

    private void UpdateRegionMap()
    {
        mapImage.texture = worldHud.minimapTexture;
        var region = System.Array.Find(FindObjectsByType<EcoRegion>(FindObjectsSortMode.None),
            candidate => candidate.gameObject.scene == worldHud.gameObject.scene && candidate.Key == entry.RegionKey);
        mapHighlight.gameObject.SetActive(region != null);
        if (region == null) return;
        Vector2 min = Vector2.one;
        Vector2 max = Vector2.zero;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = region.transform.TransformPoint(new Vector3(
                region.size.x * (i % 2 == 0 ? -0.5f : 0.5f), 0, region.size.y * (i < 2 ? -0.5f : 0.5f)));
            Vector2 uv = new Vector2(
                Mathf.Clamp01((corner.x - worldHud.worldOrigin.x) / Mathf.Max(1f, worldHud.worldSize.x)),
                Mathf.Clamp01((corner.z - worldHud.worldOrigin.y) / Mathf.Max(1f, worldHud.worldSize.y)));
            min = Vector2.Min(min, uv);
            max = Vector2.Max(max, uv);
        }
        var map = mapImage.rectTransform.sizeDelta;
        EcoUI.Place(mapHighlight.rectTransform, new Vector2(min.x * map.x, (max.y - 1f) * map.y), Vector2.Scale(max - min, map));
    }

    private static Texture2D TextureFor(string name) => Resources.Load<Texture2D>("EcoCatalog/" + name);

    private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = EcoUI.Rect(name, parent, TopLeft, TopLeft);
        EcoUI.Place(rect, position, size);
        return rect;
    }

    private static RawImage Picture(string name, Transform parent, Vector2 position, Vector2 size, Texture texture)
    {
        var picture = Box(name, parent, position, size).gameObject.AddComponent<RawImage>();
        picture.texture = texture;
        picture.raycastTarget = false;
        return picture;
    }

    private Text Print(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize,
        Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var label = EcoUI.Label(name, parent, text, fontSize, alignment, color, bold);
        if (bookFont != null) label.font = bookFont;
        label.supportRichText = false;
        EcoUI.Layout(label, position, size);
        return label;
    }

    private static void Stroke(Transform parent, Vector2 position, Vector2 size)
    {
        var line = EcoUI.Fill("Rule", parent, Rule);
        EcoUI.Place(line.rectTransform, position, size);
    }

    private Button Action(string label, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action, out Text text)
    {
        var face = EcoUI.Panel(label, parent, TopLeft, TopLeft, Control);
        EcoUI.Place(face.rectTransform, position, size);
        face.pixelsPerUnitMultiplier = 2f;
        text = Print("Label", face.transform, label, Vector2.zero, size, 16, Ink, true, TextAnchor.MiddleCenter);
        return Click(face, action);
    }

    private static Button Click(Graphic face, UnityEngine.Events.UnityAction action)
    {
        face.raycastTarget = true;
        var button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        var colors = button.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.04f);
        colors.pressedColor = new Color(0.83f, 0.79f, 0.69f);
        button.colors = colors;
        button.onClick.AddListener(action);
        return button;
    }
}
