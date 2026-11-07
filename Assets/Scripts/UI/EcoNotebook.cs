using UnityEngine;
using UnityEngine.UI;

// Shared field-notebook palette for opened menus and planning screens.
internal static class EcoNotebook
{
    internal static readonly Color Paper = new Color32(224, 222, 207, 255);
    internal static readonly Color Sheet = new Color32(235, 232, 220, 255);
    internal static readonly Color Inset = new Color32(211, 216, 202, 255);
    internal static readonly Color Ink = new Color32(32, 49, 42, 255);
    internal static readonly Color Muted = new Color32(74, 90, 80, 255);
    internal static readonly Color Accent = new Color32(48, 87, 67, 255);
    internal static readonly Color Rule = new Color32(152, 166, 146, 255);
    internal static readonly Color Dim = new Color32(17, 29, 30, 166);
    internal static readonly Color Selected = new Color32(192, 211, 191, 255);

    internal static Image Surface(Image image, Color color)
    {
        image.sprite = EcoUI.Rounded;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1.85f;
        image.color = color;
        if (image.TryGetComponent<Shadow>(out var oldShadow)) oldShadow.enabled = false;
        return image;
    }
}
