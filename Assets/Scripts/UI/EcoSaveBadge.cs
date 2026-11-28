using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feedback (GDD v5 §21, "Geri bildirim 14"): the autosave was silent, so players did not know it existed. When the
/// game saves itself a small pill fades in at the bottom right ("Otomatik kaydedildi" with a green dot that pulses)
/// and fades out again after two seconds. It never moves or replaces the notices at the top.
/// </summary>
public sealed class EcoSaveBadge : MonoBehaviour
{
    private static EcoSaveBadge current;
    private CanvasGroup group;
    private RectTransform dot;
    private Text label;
    private float age = -1f;
    private const float FadeIn = 0.2f, Hold = 2f, FadeOut = 0.6f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { current = null; }

    public static void Show(string text)
    {
        if (current == null) current = Create();
        current.label.text = text;
        // A save during the fade-out brings it back up without a second pop.
        current.age = current.age >= 0f && current.age < FadeIn + Hold + FadeOut ? Mathf.Min(current.age, FadeIn) : 0f;
    }

    private static EcoSaveBadge Create()
    {
        var go = new GameObject("Save badge");
        DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 1f;
        var badge = go.AddComponent<EcoSaveBadge>();
        badge.group = go.AddComponent<CanvasGroup>();
        badge.group.alpha = 0f; badge.group.interactable = false; badge.group.blocksRaycasts = false;

        var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(go.transform, false);
        var rect = (RectTransform)pill.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f); rect.sizeDelta = new Vector2(250f, 42f);
        var image = pill.GetComponent<Image>();
        image.sprite = EcoUI.Rounded; image.type = Image.Type.Sliced; image.color = new Color(0.06f, 0.13f, 0.1f, 0.85f); image.raycastTarget = false;

        var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
        dotGo.transform.SetParent(pill.transform, false);
        badge.dot = (RectTransform)dotGo.transform;
        badge.dot.anchorMin = badge.dot.anchorMax = badge.dot.pivot = new Vector2(0f, 0.5f);
        badge.dot.anchoredPosition = new Vector2(16f, 0f); badge.dot.sizeDelta = new Vector2(14f, 14f);
        var dotImage = dotGo.GetComponent<Image>();
        dotImage.sprite = EcoUI.Circle; dotImage.color = new Color32(110, 225, 130, 255); dotImage.raycastTarget = false;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(pill.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(40f, 0f); textRect.offsetMax = new Vector2(-12f, 0f);
        badge.label = textGo.GetComponent<Text>();
        badge.label.font = EcoUI.Font; badge.label.fontSize = 20; badge.label.alignment = TextAnchor.MiddleLeft;
        badge.label.color = new Color32(226, 236, 228, 255); badge.label.raycastTarget = false;
        return badge;
    }

    private void Update()
    {
        if (age < 0f) return;
        age += Time.unscaledDeltaTime;
        float alpha = age < FadeIn ? age / FadeIn : age < FadeIn + Hold ? 1f : 1f - (age - FadeIn - Hold) / FadeOut;
        if (alpha <= 0f) { group.alpha = 0f; age = -1f; return; }
        group.alpha = alpha;
        float pulse = 1f + 0.25f * Mathf.Max(0f, Mathf.Sin(age * 6f));
        dot.localScale = Vector3.one * pulse;
    }
}
