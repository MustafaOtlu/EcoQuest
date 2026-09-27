using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

[DefaultExecutionOrder(-100)]
public sealed class EcoMarketUI : MonoBehaviour
{
    private static EcoMarketUI current;
    private static int closedFrame = -10;
    private static bool waitForRelease;
    public static bool IsOpen => current != null && current.vendor != null;
    public static bool BlocksGameplay => IsOpen || waitForRelease || Time.frameCount <= closedFrame;
    public EcoMarket vendor { get; private set; }
    private PlayerWeaponSystem player;
    private GameObject canvasRoot, content;
    private Text wallet, notice;
    private readonly Button[] buttons = new Button[9];
    private readonly Text[] descriptions = new Text[9];
    private readonly Text[] prices = new Text[9];
    private Font font;
    private bool upgradePage;
    private float refreshAt;
    private CursorLockMode previousLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { current = null; closedFrame = -10; waitForRelease = false; }
    public static void Open(EcoMarket shop, PlayerWeaponSystem owner)
    {
        if (shop == null || owner == null || !shop.CanReach(owner)) return;
        if (current == null) current = owner.gameObject.AddComponent<EcoMarketUI>();
        if (IsOpen) return;
        current.player = owner; current.vendor = shop; current.previousLock = Cursor.lockState;
        owner.StopCharging(); owner.Builder.SetBuildMode(false); owner.holder.Unequip();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        current.Build(); current.ShowPage(false);
    }
    public static void CloseFor(PlayerWeaponSystem owner) { if (current != null && current.player == owner) current.Close(); }
    public void Close()
    {
        if (vendor == null && canvasRoot == null) return;
        vendor = null;
        if (canvasRoot != null) { canvasRoot.SetActive(false); Destroy(canvasRoot); }
        closedFrame = Time.frameCount; waitForRelease = true;
        Cursor.lockState = previousLock; Cursor.visible = previousLock != CursorLockMode.Locked;
        if (player != null && player.isActiveAndEnabled) player.SelectTool((int)player.Selected);
    }
    private void Update()
    {
        if (canvasRoot != null && vendor == null) { Close(); return; }
        if (!IsOpen)
        {
            if (Mouse.current == null || !Mouse.current.leftButton.isPressed) waitForRelease = false;
            return;
        }
        if (!vendor.CanReach(player) || !player.isActiveAndEnabled || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)) { Close(); return; }
        if (Time.unscaledTime >= refreshAt) { refreshAt = Time.unscaledTime + 0.1f; Refresh(); }
    }
    public void ShowPage(bool upgrades)
    {
        upgradePage = upgrades;
        if (content != null) { content.SetActive(false); Destroy(content); }
        content = Panel("Market entries", canvasRoot.transform.Find("Market window"), new Vector2(20, -148), new Vector2(920, 470), Color.clear).gameObject;
        int count = upgrades ? 9 : EcoMarket.Titles.Length;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var card = Panel("Offer " + i, content.transform, new Vector2(i % 3 * 306, -(i / 3) * 153), new Vector2(292, 143), new Color(0.12f, 0.2f, 0.17f));
            TextAt(card, upgrades ? EcoEquipmentUpgrades.Names[i] : EcoMarket.Titles[i], new Vector2(12, -7), new Vector2(268, 27), 19);
            descriptions[i] = TextAt(card, "", new Vector2(12, -36), new Vector2(268, 42), 14);
            prices[i] = TextAt(card, "", new Vector2(12, -79), new Vector2(268, 20), 14);
            buttons[i] = ButtonAt(card, "İşlem", new Vector2(12, -104), new Vector2(268, 30), () =>
            {
                if (vendor == null || player == null) return;
                if (upgradePage) vendor.Upgrade(player, (EcoEquipmentUpgrades.Upgrade)index);
                else vendor.Trade(player, (EcoMarket.Offer)index);
                Refresh();
            });
        }
        Refresh();
    }
    private void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (EventSystem.current == null)
        {
            var events = new GameObject("Eco UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        canvasRoot = new GameObject("Eco Market UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvasRoot.GetComponent<Canvas>().sortingOrder = 80;
        var scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var shade = Panel("Shade", canvasRoot.transform, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.65f));
        shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
        var window = Panel("Market window", canvasRoot.transform, Vector2.zero, new Vector2(960, 720), new Color(0.045f, 0.085f, 0.07f, 1));
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        TextAt(window, "EKO MARKET", new Vector2(24, -14), new Vector2(600, 36), 28);
        wallet = TextAt(window, "", new Vector2(24, -55), new Vector2(906, 31), 17);
        ButtonAt(window, "Alışveriş", new Vector2(24, -99), new Vector2(180, 35), () => ShowPage(false));
        ButtonAt(window, "Ekipman geliştirmeleri", new Vector2(216, -99), new Vector2(240, 35), () => ShowPage(true));
        ButtonAt(window, "Kapat [Esc]", new Vector2(790, -15), new Vector2(145, 35), Close);
        notice = TextAt(window, "", new Vector2(24, -618), new Vector2(908, 38), 17);
        TextAt(window, "Geliştirmeler daha fazla elektrik kullanır. YEP harcanmaz; seviye kilitleri açar.\nHer YEP seviyesi toplam geliştirme hakkına 3 ekler.", new Vector2(24, -667), new Vector2(908, 43), 14);
    }
    private void Refresh()
    {
        if (player == null || wallet == null) return;
        wallet.text = "Metal " + player.Metal + " • Plastik " + player.Plastic + " • Ürün " + player.Food + " • YEP " + player.Progression.Level
            + " • Geliştirme hakkı " + Mathf.Max(0, player.Upgrades.Allowance - player.Upgrades.Purchased);
        int count = upgradePage ? 9 : EcoMarket.Titles.Length;
        for (int i = 0; i < count; i++)
        {
            bool valid; string reason;
            if (upgradePage)
            {
                var item = (EcoEquipmentUpgrades.Upgrade)i;
                valid = player.Upgrades.CanBuy(item, out reason);
                descriptions[i].text = player.Upgrades.Description(item);
                prices[i].text = player.Upgrades.Rank(item) >= 3 ? "Kademe 3/3 • Tamamlandı"
                    : "Kademe " + player.Upgrades.Rank(item) + "/3 • " + player.Upgrades.MetalCost(item) + "M / " + player.Upgrades.PlasticCost(item) + "P";
            }
            else
            {
                var item = (EcoMarket.Offer)i;
                valid = player.CanMarketTrade(item, out reason); descriptions[i].text = EcoMarket.Details[i];
                prices[i].text = EcoMarket.IsPurchase(item) ? EcoMarket.MetalPrice(item) + " metal / " + EcoMarket.PlasticPrice(item) + " plastik • YEP " + EcoMarket.Level(item)
                    : i == 4 ? "Bedel: 1 ürün" : "Bedel: 5 tohum";
            }
            buttons[i].interactable = valid;
            buttons[i].GetComponentInChildren<Text>().text = valid ? upgradePage ? "Geliştir" : EcoMarket.IsPurchase((EcoMarket.Offer)i) ? "Satın al" : "Sat" : reason;
        }
        notice.text = Time.time < player.FeedbackUntil ? player.Feedback : "Tohum " + player.TotalSeeds + "/" + player.Upgrades.SeedCapacity + " • Seçili " + EcoCropCatalog.Get(player.SelectedSeed).Name + " " + player.SeedCount(player.SelectedSeed) + " • Filtre " + player.SmallFilters + " küçük / " + player.LargeFilters + " büyük";
    }
    private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        go.GetComponent<Image>().color = color; return rect;
    }
    private Text TextAt(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size * 4; rect.localScale = Vector3.one * 0.25f;
        var label = go.GetComponent<Text>(); label.font = font; label.fontSize = fontSize * 4; label.color = new Color(0.94f, 0.95f, 0.86f); label.text = value; label.raycastTarget = false;
        label.alignment = TextAnchor.MiddleLeft; return label;
    }
    private Button ButtonAt(Transform parent, string value, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Panel(value, parent, position, size, new Color(0.2f, 0.43f, 0.29f));
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
        var label = TextAt(rect, value, Vector2.zero, size, 15); label.alignment = TextAnchor.MiddleCenter; return button;
    }
    private void OnDisable() { Close(); if (current == this) { current = null; waitForRelease = false; } }
}
