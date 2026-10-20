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
    // Sized for the largest page (10 offers today); a 4-column grid holds up to 12 cards.
    private const int MaxCards = 12, Columns = 4;
    private readonly Button[] buttons = new Button[MaxCards];
    private readonly Text[] descriptions = new Text[MaxCards];
    private readonly Text[] prices = new Text[MaxCards];
    private Font font;
    private enum Page { Shop, Upgrades, Support }
    private Page page;
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
        if (!PlayerController.Paused) Time.timeScale = 0.1f; // GDD §14: planning/shopping runs the world at 10%
    }
    public static void CloseFor(PlayerWeaponSystem owner) { if (current != null && current.player == owner) current.Close(); }
    public void Close()
    {
        if (vendor == null && canvasRoot == null) return;
        vendor = null;
        if (canvasRoot != null) { canvasRoot.SetActive(false); Destroy(canvasRoot); }
        closedFrame = Time.frameCount; waitForRelease = true;
        if (!PlayerController.Paused) Time.timeScale = 1f;
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
    public void ShowPage(bool upgrades) => ShowPage(upgrades ? Page.Upgrades : Page.Shop);
    private int CardCount => page == Page.Upgrades ? 9 : page == Page.Support ? SupportTitles.Length : EcoMarket.Titles.Length;
    private void ShowPage(Page target)
    {
        page = target;
        if (content != null) { content.SetActive(false); Destroy(content); }
        content = Panel("Market entries", canvasRoot.transform.Find("Market window"), new Vector2(20, -148), new Vector2(920, 470), Color.clear).gameObject;
        int count = Mathf.Min(CardCount, MaxCards);
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var card = Panel("Offer " + i, content.transform, new Vector2(i % Columns * 230, -(i / Columns) * 155), new Vector2(220, 147), new Color(0.12f, 0.2f, 0.17f));
            IconAt(card, page == Page.Upgrades ? UpgradeIcons[i] : page == Page.Support ? SupportIcons[i] : OfferIcons[i], new Vector2(172, -6), 40f);
            TextAt(card, page == Page.Upgrades ? EcoEquipmentUpgrades.Names[i] : page == Page.Support ? SupportTitles[i] : EcoMarket.Titles[i], new Vector2(10, -7), new Vector2(160, 27), 17);
            descriptions[i] = TextAt(card, "", new Vector2(10, -34), new Vector2(202, 46), 13);
            prices[i] = TextAt(card, "", new Vector2(10, -81), new Vector2(202, 20), 13);
            buttons[i] = ButtonAt(card, "İşlem", new Vector2(10, -107), new Vector2(200, 30), () =>
            {
                if (vendor == null || player == null) return;
                if (page == Page.Upgrades) vendor.Upgrade(player, (EcoEquipmentUpgrades.Upgrade)index);
                else if (page == Page.Support) Support(index);
                else vendor.Trade(player, (EcoMarket.Offer)index);
                Refresh();
            });
        }
        Refresh();
    }
    // GDD §8 "Çıkmaza girmeme": emergency charge, basic rescue, hand sorting and the slow workshop path.
    private static readonly string[] SupportTitles = { "Acil şarj", "Temel destek", "Elle ayırma", "Atölye: demir top", "Atölye: küçük filtre", "Atölye: büyük filtre" };
    private static readonly string[] SupportIcons = { "electricity", "seed", "tool_recycle", "ammo", "filter", "poi_factory" };
    private static readonly EcoMarket.Offer[] Crafts = { EcoMarket.Offer.IronAmmo, EcoMarket.Offer.SmallFilter, EcoMarket.Offer.LargeFilter };
    private void Support(int index)
    {
        var shop = EcoWorkshop.For(player);
        if (index == 0) shop.StartCharge(vendor);
        else if (index == 1) shop.Rescue();
        else if (index == 2) player.HandSort();
        else shop.Enqueue(Crafts[index - 3]);
    }
    private bool DescribeSupport(int index, out string description, out string price, out string action)
    {
        var shop = EcoWorkshop.For(player); string reason;
        int raw = player.CollectedMetalScrap + player.CollectedPlasticScrap + player.CollectedOrganicWaste;
        switch (index)
        {
            case 0:
                description = "Araç elektriği bitince 30 sn markete yakın kal: +15 EB. Paneli kapatabilirsin.";
                price = shop.Charging ? "Şarj %" + Mathf.RoundToInt(shop.ChargeProgress / EcoWorkshop.ChargeSeconds * 100f) : "Araç " + player.Energy.ToString("0") + " EB";
                bool charge = shop.CanCharge(out reason); action = charge ? "Başlat" : reason; return charge;
            case 1:
                description = "Tohum ve ürün yokken ya da su 2 SB altındayken: 1 lahana tohumu + 2 SB.";
                price = "Günde bir kez • ücretsiz";
                bool rescue = shop.CanRescue(out reason); action = rescue ? "Al" : reason; return rescue;
            case 2:
                description = "Elektriksiz: 1 ham atığı elle ayırıp metal/plastik/organik yap.";
                price = "Ham atık " + raw;
                action = raw > 0 ? "Ayır" : "Ham atık yok"; return raw > 0;
            default:
                var offer = Crafts[index - 3];
                description = "Günlük stok bitince eldeki malzemeyle paket başına 45 sn'de yapılır.";
                int queued = 0; foreach (var q in shop.Queue) if (q == offer) queued++;
                price = EcoMarket.MetalPrice(offer) + "M / " + EcoMarket.PlasticPrice(offer) + "P • sırada " + queued
                    + (shop.Queue.Count > 0 && shop.Queue[0] == offer ? " • %" + Mathf.RoundToInt(shop.PackProgress / EcoWorkshop.PackSeconds * 100f) : "");
                bool craft = shop.CanQueue(offer, out reason); action = craft ? "Sıraya ekle" : reason; return craft;
        }
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
        IconAt(window, "poi_market", new Vector2(22, -10), 44f);
        TextAt(window, "EKO MARKET", new Vector2(76, -14), new Vector2(600, 36), 28);
        wallet = TextAt(window, "", new Vector2(24, -55), new Vector2(906, 31), 17);
        ButtonAt(window, "Alışveriş", new Vector2(24, -99), new Vector2(180, 35), () => ShowPage(false));
        ButtonAt(window, "Ekipman geliştirmeleri", new Vector2(216, -99), new Vector2(240, 35), () => ShowPage(true));
        ButtonAt(window, "Destek ve atölye", new Vector2(468, -99), new Vector2(200, 35), () => ShowPage(Page.Support));
        ButtonAt(window, "Kapat [Esc]", new Vector2(790, -15), new Vector2(145, 35), Close);
        notice = TextAt(window, "", new Vector2(24, -618), new Vector2(908, 38), 17);
        TextAt(window, "Geliştirmeler YEP harcamaz; her kademe ilgili kapasite/erişim/hızı tabana göre %15 artırır.\n2. kademe 2 tamamlanmış proje, 3. kademe 4 proje ister. Güçlü araç daha çok elektrik kullanır.", new Vector2(24, -667), new Vector2(908, 43), 14);
    }
    private void Refresh()
    {
        if (player == null || wallet == null) return;
        wallet.text = "Metal " + player.Metal + " • Plastik " + player.Plastic + " • Ürün " + player.Food + " • Temiz balık " + player.CleanFish
            + " • YEP " + player.Progression.Experience + "/" + EcoProgression.MaximumExperience
            + " • Geliştirme kademesi " + player.Upgrades.Purchased + "/" + player.Upgrades.Allowance;
        int count = Mathf.Min(CardCount, MaxCards);
        for (int i = 0; i < count; i++)
        {
            bool valid; string reason;
            if (page == Page.Support)
            {
                valid = DescribeSupport(i, out string description, out string price, out string action);
                descriptions[i].text = description; prices[i].text = price;
                buttons[i].interactable = valid; buttons[i].GetComponentInChildren<Text>().text = action;
                continue;
            }
            if (page == Page.Upgrades)
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
                valid = player.CanMarketTrade(item, out reason);
                if (valid && !EcoMarket.WithinDailyLimit(item, out reason)) valid = false;
                descriptions[i].text = EcoMarket.Details[i] + (EcoMarket.DailyLimits[i] > 0 && EcoMarket.IsPurchase(item) ? "\nBugün kalan: " + EcoMarket.RemainingToday(item) + "/" + EcoMarket.DailyLimits[i] : "");
                prices[i].text = EcoMarket.IsPurchase(item) ? EcoMarket.MetalPrice(item) + " metal / " + EcoMarket.PlasticPrice(item) + " plastik"
                    : i == 4 ? "Bedel: 1 ürün" : item == EcoMarket.Offer.SellFish ? "Bedel: 1 temiz balık" : "Bedel: 5 tohum";
            }
            buttons[i].interactable = valid;
            buttons[i].GetComponentInChildren<Text>().text = valid ? page == Page.Upgrades ? "Geliştir" : EcoMarket.IsPurchase((EcoMarket.Offer)i) ? "Satın al" : "Sat" : reason;
        }
        notice.text = Time.time < player.FeedbackUntil ? player.Feedback : "Tohum " + player.TotalSeeds + "/" + player.Upgrades.SeedCapacity + " • Seçili " + EcoCropCatalog.Get(player.SelectedSeed).Name + " " + player.SeedCount(player.SelectedSeed) + " • Filtre " + player.SmallFilters + " küçük / " + player.LargeFilters + " büyük";
    }
    private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        go.GetComponent<Image>().color = color; return rect;
    }
    // Hand-drawn icons for each offer / upgrade card (Resources/EcoIcons).
    private static readonly string[] OfferIcons = { "ammo", "crop_cabbage", "filter", "poi_factory", "food", "seed", "crop_carrot", "crop_tomato", "crop_tree", "fish" };
    private static readonly string[] UpgradeIcons = { "water", "tool_vacuum", "w_wind", "seed", "crop_cabbage", "tool_seed", "scrap", "tool_recycle", "metal" };
    private static void IconAt(Transform parent, string icon, Vector2 position, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image)); var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = new Vector2(size, size);
        var image = go.GetComponent<Image>(); image.sprite = EcoIcons.Get(icon); image.preserveAspect = true; image.raycastTarget = false; image.enabled = image.sprite != null;
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
