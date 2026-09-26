using UnityEngine;
using UnityEngine.UI;

/// <summary>Minimal world-space health bar for combat readability.</summary>
public sealed class EnemyHealthBar : MonoBehaviour
{
    private EnemyBrain enemy;
    private Image fill;
    private Canvas canvas;
    private Camera view;

    private void Awake()
    {
        enemy = GetComponent<EnemyBrain>();
        view = Camera.main;
        var root = new GameObject("Enemy Health Bar", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        var controller = GetComponent<CharacterController>();
        root.transform.localPosition = controller != null
            ? controller.center + Vector3.up * (controller.height * 0.5f + 0.2f)
            : Vector3.up * 1.75f;
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 10;
        canvas.enabled = false;
        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(100f, 10f);
        rect.localScale = Vector3.one * 0.01f;
        var background = MakeImage(root.transform, new Color(0.02f, 0.03f, 0.025f, 0.85f), Vector2.zero, new Vector2(100f, 10f));
        fill = MakeImage(root.transform, new Color(0.85f, 0.18f, 0.12f, 1f), new Vector2(-49f, 0f), new Vector2(98f, 8f));
        fill.rectTransform.pivot = new Vector2(0f, 0.5f);
        background.transform.SetAsFirstSibling();
    }

    private void LateUpdate()
    {
        if (canvas == null || enemy == null) return;
        if (view == null) view = Camera.main;
        if (view != null)
        {
            canvas.worldCamera = view;
            canvas.transform.rotation = view.transform.rotation;
        }
        // Sprite-less Images ignore fillAmount, so resize the left-anchored rectangle.
        float fraction = Mathf.Clamp01(enemy.Health / Mathf.Max(1f, enemy.maximumHealth));
        fill.rectTransform.sizeDelta = new Vector2(98f * fraction, 8f);
        canvas.enabled = enemy.State != EnemyBrain.Behaviour.Dead && enemy.Health < enemy.maximumHealth;
    }

    private static Image MakeImage(Transform parent, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
