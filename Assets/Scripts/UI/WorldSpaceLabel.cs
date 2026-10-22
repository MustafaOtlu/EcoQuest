using UnityEngine;
using UnityEngine.UI;

// A map caption stays attached to its location, but its type stays readable at any camera zoom.
[DefaultExecutionOrder(500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class WorldSpaceLabel : MonoBehaviour
{
    [SerializeField, Range(14f, 32f)] private float fontPixels = 22f;
    [SerializeField, Range(180f, 440f)] private float maximumWidthPixels = 340f;
    [SerializeField] private Transform signboard;

    private const int LayoutFontSize = 36;
    private const float Padding = 16f;
    private Camera view;
    private Canvas canvas;
    private RectTransform rect;
    private Text[] captions;
    private string[] previousText;
    private Vector3 anchorLocalPosition;
    private float previousWidth = -1f;
    private Transform alignedBoard;
    private Renderer signpost;
    private Bounds boardMeshBounds;
    private Vector3 boardPosition, boardScale;
    private Quaternion boardRotation;
    private float boardDepth;
    private CanvasGroup visibility;
    private Image connector;
    private Vector3 sourceScreen;
    private float captionPixels;

    internal bool IsPlantHint => name == "Planted Seed Sign";
    internal bool IsPhysicalSign => alignedBoard != null;
    internal bool WasPlaced { get; set; }
    internal float AnchorDepth => sourceScreen.z;
    internal Vector3 Anchor => transform.parent != null ? transform.parent.TransformPoint(anchorLocalPosition) : anchorLocalPosition;
    internal Rect ScreenBounds => new Rect((Vector2)sourceScreen - PixelSize * 0.5f, PixelSize);
    private Vector2 PixelSize => rect != null ? rect.rect.size * (captionPixels / LayoutFontSize) : Vector2.zero;

    private void Awake() => canvas = GetComponent<Canvas>();
    private void OnEnable()
    {
        WorldLabelLayout.Register(this);
        if (captions != null) PrepareSignboard();
    }

    private void LateUpdate()
    {
        if (view == null || !view.isActiveAndEnabled)
        {
            view = Camera.main;
            if (view == null || !view.isActiveAndEnabled)
                view = Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }
        WorldLabelLayout.Refresh(view);
    }

    // Also usable by editor-only previews without starting the game or changing the scene asset.
    public void RefreshForCamera(Camera camera)
    {
        if (camera == null || camera.pixelHeight < 1 || camera.pixelWidth < 1) return;
        if (captions == null && !PrepareCaption()) return;

        canvas.worldCamera = camera;
        Vector3 anchor = transform.parent != null
            ? transform.parent.TransformPoint(anchorLocalPosition) : anchorLocalPosition;
        // Keep the pole behind the readable face even when the camera looks down steeply.
        if (alignedBoard != null && signpost != null)
        {
            Vector3 forward = camera.transform.forward;
            Bounds post = signpost.bounds;
            float front = Vector3.Dot(post.center - anchor, forward)
                - Vector3.Dot(post.extents, new Vector3(Mathf.Abs(forward.x), Mathf.Abs(forward.y), Mathf.Abs(forward.z)));
            if (front < 0.03f) anchor += forward * (front - 0.03f);
        }
        transform.position = anchor;
        transform.rotation = camera.transform.rotation;

        Vector3 screen = camera.WorldToScreenPoint(anchor);
        sourceScreen = screen;
        if (screen.z <= camera.nearClipPlane) { SetVisible(false); return; }
        SetVisible(true);
        float pixels = (IsPlantHint ? 18f : IsPhysicalSign ? fontPixels : Mathf.Min(fontPixels, 20f))
            * Mathf.Clamp(camera.pixelHeight / 900f, 0.85f, 1.25f);
        captionPixels = pixels;
        float unitsPerPixel = camera.orthographic
            ? camera.orthographicSize * 2f / camera.pixelHeight
            : 2f * screen.z * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / camera.pixelHeight;
        float worldScale = unitsPerPixel * pixels / LayoutFontSize;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldScale / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));

        float widthPixels = Mathf.Min(IsPlantHint ? 280f : maximumWidthPixels, camera.pixelWidth * 0.38f);
        float contentWidth = Mathf.Max(LayoutFontSize * 3f, widthPixels * LayoutFontSize / pixels - Padding * 2f);
        bool changed = Mathf.Abs(contentWidth - previousWidth) > 0.5f;
        for (int i = 0; i < captions.Length; i++)
            if (captions[i] != null && captions[i].text != previousText[i]) changed = true;
        if (changed) Layout(contentWidth);

        // Only keep an already visible sign's edges readable; off-screen signs stay off screen.
        Rect viewport = camera.pixelRect;
        if (viewport.Contains(new Vector2(screen.x, screen.y)))
        {
            Vector2 half = rect.rect.size * (pixels / LayoutFontSize * 0.5f);
            float insetX = Mathf.Min(half.x + 8f, viewport.width * 0.5f);
            float insetY = Mathf.Min(half.y + 8f, viewport.height * 0.5f);
            screen.x = Mathf.Round(Mathf.Clamp(screen.x, viewport.xMin + insetX, viewport.xMax - insetX));
            screen.y = Mathf.Round(Mathf.Clamp(screen.y, viewport.yMin + insetY, viewport.yMax - insetY));
            transform.position = camera.ScreenToWorldPoint(screen);
        }
        AlignSignboard(camera);
        UpdateConnector(camera);
    }

    internal void PlaceAt(Camera camera, Vector2 center)
    {
        transform.position = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, sourceScreen.z));
        AlignSignboard(camera);
        UpdateConnector(camera);
    }

    internal void SetVisible(bool shown)
    {
        if (visibility != null) visibility.alpha = shown ? 1f : 0f;
        if (!shown && alignedBoard != null) RestoreBoardPose();
    }

    private void UpdateConnector(Camera camera)
    {
        if (connector == null || rect == null) return;
        Vector3 local = rect.InverseTransformPoint(camera.ScreenToWorldPoint(sourceScreen));
        Vector2 end = new Vector2(local.x, local.y), half = rect.rect.size * 0.5f;
        bool outside = Mathf.Abs(end.x) > half.x + 12 || Mathf.Abs(end.y) > half.y + 12;
        connector.enabled = outside;
        if (!outside) return;
        float edge = 1f / Mathf.Max(Mathf.Abs(end.x) / Mathf.Max(1, half.x), Mathf.Abs(end.y) / Mathf.Max(1, half.y));
        Vector2 start = end * edge, delta = end - start;
        var line = connector.rectTransform;
        line.anchoredPosition = (start + end) * 0.5f;
        line.sizeDelta = new Vector2(delta.magnitude, 1.5f * LayoutFontSize / Mathf.Max(1, captionPixels));
        line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
    }

    // Builders can opt in explicitly. Existing authored signs use the same Board/Post sibling layout.
    public void BindSignboard(Transform board)
    {
        RestoreSignboard();
        signboard = board;
        PrepareSignboard();
    }

    private void PrepareSignboard()
    {
        if (alignedBoard != null) return;
        var parent = transform.parent;
        if (signboard == null && parent != null && parent.name == "Signpost" && parent.Find("Post") != null)
            signboard = parent.Find("Board");
        if (signboard == null || signboard.parent != parent) return;
        var filter = signboard.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return;
        alignedBoard = signboard;
        boardPosition = alignedBoard.localPosition;
        boardRotation = alignedBoard.localRotation;
        boardScale = alignedBoard.localScale;
        boardMeshBounds = filter.sharedMesh.bounds;
        boardDepth = Mathf.Max(0.01f, Mathf.Abs(alignedBoard.lossyScale.z) * boardMeshBounds.size.z);
        var post = parent != null ? parent.Find("Post") : null;
        signpost = post != null ? post.GetComponent<Renderer>() : null;
    }

    private void AlignSignboard(Camera camera)
    {
        if (alignedBoard == null) return;
        // Fit the backing inside the paper frame, including after zoom/layout or viewport edge clamping.
        Vector3 size = new Vector3(rect.rect.width * Mathf.Abs(transform.lossyScale.x) * 0.96f,
            rect.rect.height * Mathf.Abs(transform.lossyScale.y) * 0.96f, boardDepth);
        Vector3 parentScale = alignedBoard.parent != null ? alignedBoard.parent.lossyScale : Vector3.one;
        alignedBoard.rotation = camera.transform.rotation;
        alignedBoard.localScale = new Vector3(
            size.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x * boardMeshBounds.size.x)),
            size.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y * boardMeshBounds.size.y)),
            size.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z * boardMeshBounds.size.z)));
        alignedBoard.position = transform.TransformPoint(rect.rect.center)
            + camera.transform.forward * (boardDepth * 0.5f + 0.02f)
            - alignedBoard.TransformVector(boardMeshBounds.center);
    }

    private void RestoreSignboard()
    {
        RestoreBoardPose();
        alignedBoard = null;
        signpost = null;
    }

    private void RestoreBoardPose()
    {
        if (alignedBoard == null) return;
        alignedBoard.localPosition = boardPosition;
        alignedBoard.localRotation = boardRotation;
        alignedBoard.localScale = boardScale;
    }

    private void OnDisable() { SetVisible(false); WorldLabelLayout.Unregister(this); RestoreSignboard(); }

    private bool PrepareCaption()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas.renderMode != RenderMode.WorldSpace) return false;
        var found = GetComponentsInChildren<Text>(true);
        if (found.Length == 0) return false; // Runtime builders add children after this component.
        rect = (RectTransform)transform;
        anchorLocalPosition = transform.localPosition;
        PrepareSignboard();
        captions = found;
        previousText = new string[found.Length];
        previousWidth = -1f;

        var scaler = GetComponent<CanvasScaler>();
        if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3f;
        canvas.scaleFactor = scaler.dynamicPixelsPerUnit;
        canvas.pixelPerfect = false;
        canvas.overrideSorting = true;
        canvas.sortingOrder = -5; // World callouts stay below the HUD and modal windows.
        visibility = GetComponent<CanvasGroup>();
        if (visibility == null) visibility = gameObject.AddComponent<CanvasGroup>();
        visibility.interactable = visibility.blocksRaycasts = false;
        var readableMaterial = Resources.Load<Material>("EcoUI/WorldCaption");

        var plateTransform = transform.Find("Plate");
        var plate = plateTransform != null ? plateTransform.GetComponent<Image>() : null;
        if (plate == null)
        {
            var background = new GameObject("Plate", typeof(RectTransform), typeof(Image));
            background.layer = gameObject.layer;
            background.transform.SetParent(transform, false);
            plate = background.GetComponent<Image>();
        }
        var plateRect = plate.rectTransform;
        plateRect.SetAsFirstSibling();
        plateRect.anchorMin = Vector2.zero;
        plateRect.anchorMax = Vector2.one;
        plateRect.offsetMin = plateRect.offsetMax = Vector2.zero;
        plate.sprite = EcoUI.PaperSprite;
        plate.type = Image.Type.Sliced;
        plate.color = Color.white;
        plate.raycastTarget = false;
        if (readableMaterial != null) plate.material = readableMaterial;

        var lineObject = new GameObject("Anchor line", typeof(RectTransform), typeof(Image));
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(transform, false);
        lineObject.transform.SetAsFirstSibling();
        connector = lineObject.GetComponent<Image>();
        connector.color = new Color(EcoUI.Ink.r, EcoUI.Ink.g, EcoUI.Ink.b, 0.65f);
        connector.raycastTarget = false;
        connector.enabled = false;
        connector.rectTransform.anchorMin = connector.rectTransform.anchorMax = connector.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        if (readableMaterial != null) connector.material = readableMaterial;

        foreach (var caption in captions)
        {
            if (caption.font == null) caption.font = EcoUI.Font;
            caption.fontSize = LayoutFontSize;
            caption.color = EcoUI.Ink;
            caption.resizeTextForBestFit = false;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Overflow;
            caption.raycastTarget = false;
            if (readableMaterial != null) caption.material = readableMaterial;
            var textRect = caption.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.localScale = Vector3.one;
            textRect.localRotation = Quaternion.identity;
            var outline = caption.GetComponent<Outline>();
            if (outline == null) outline = caption.gameObject.AddComponent<Outline>();
            outline.enabled = false;
        }
        return true;
    }

    private void Layout(float maximumWidth)
    {
        float width = LayoutFontSize * 3f;
        foreach (var caption in captions)
            if (caption != null) width = Mathf.Max(width, Mathf.Min(maximumWidth, caption.preferredWidth + 6f));

        float height = 0f;
        for (int i = 0; i < captions.Length; i++)
        {
            var caption = captions[i];
            if (caption == null) continue;
            caption.rectTransform.sizeDelta = new Vector2(width, LayoutFontSize);
            // Wrapping depends on the width just assigned.
            caption.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(LayoutFontSize, caption.preferredHeight) + 4f);
            height += caption.rectTransform.rect.height;
            previousText[i] = caption.text;
        }
        rect.sizeDelta = new Vector2(width + Padding * 2f, height + Padding);
        float y = height * 0.5f;
        foreach (var caption in captions)
        {
            if (caption == null) continue;
            float h = caption.rectTransform.rect.height;
            caption.rectTransform.anchoredPosition = new Vector2(0f, y - h * 0.5f);
            y -= h;
        }
        previousWidth = maximumWidth;
    }
}
