using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The gameplay palette is separate from journal and menu artwork.
internal sealed class EcoHudStyle
{
    internal enum Tone { Surface, Raised, Text, Muted, Rule, Leaf, Amber }

    private static readonly Color32[] Day =
    {
        new Color32(25, 41, 34, 245), new Color32(35, 55, 46, 255), new Color32(237, 240, 229, 255),
        new Color32(188, 202, 192, 255), new Color32(163, 200, 180, 66), new Color32(155, 195, 152, 255), new Color32(226, 189, 124, 255),
    };
    private static readonly Color32[] Night =
    {
        new Color32(20, 31, 36, 245), new Color32(32, 44, 49, 255), new Color32(224, 231, 226, 255),
        new Color32(172, 189, 190, 255), new Color32(157, 187, 177, 63), new Color32(165, 190, 156, 255), new Color32(201, 173, 124, 255),
    };
    private readonly List<(Graphic graphic, Tone tone)> graphics = new();
    private float night = -1f;
    internal float NightAmount => Mathf.Max(0f, night);

    internal Color ColorFor(Tone tone) => ColorFor(tone, NightAmount);
    internal static Color ColorFor(Tone tone, float nightAmount) => Color.Lerp(Day[(int)tone], Night[(int)tone], nightAmount);

    internal static float NightTarget(EcoWorldClock clock)
        => clock != null ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.03f, 0.52f, clock.SolarFactor)) : 0f;

    internal void Refresh(EcoWorldClock clock)
    {
        float target = NightTarget(clock);
        float next = night < 0f ? target : Mathf.MoveTowards(night, target, Time.unscaledDeltaTime * 0.75f);
        if (next == night) return;
        night = next;
        foreach (var (graphic, tone) in graphics) if (graphic != null) graphic.color = ColorFor(tone);
    }

    internal void Tint(Graphic graphic, Tone tone)
    {
        graphic.color = ColorFor(tone);
        graphics.Add((graphic, tone));
    }

    internal Image Surface(Image image, Tone tone = Tone.Surface)
    {
        image.sprite = EcoUI.Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.85f;
        if (image.TryGetComponent<Shadow>(out var existing)) existing.enabled = false;
        var edge = image.gameObject.AddComponent<Outline>();
        edge.effectDistance = new Vector2(1f, -1f);
        edge.effectColor = new Color(0.60f, 0.76f, 0.67f, 0.21f);
        Tint(image, tone);
        return image;
    }

    internal void Card(EcoUI.Card card)
    {
        Surface(card.background);
        Tint(card.title, Tone.Text);
        Tint(card.summary, Tone.Muted);
        Tint(card.rule, Tone.Rule);
        Tint(card.toggle.targetGraphic, Tone.Raised);
    }
}
