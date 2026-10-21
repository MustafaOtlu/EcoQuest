using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class EcoOrders : MonoBehaviour
{
    public const int OrdersPerRefresh = 2, RefreshEveryDays = 3;
    public const float Bonus = 1.25f;
    public static EcoOrders Current { get; private set; }
    public sealed class Order { public string title; public int food, metal, plastic, cropKind, issuedDay; public bool delivered, fish; }
    private readonly List<Order> orders = new List<Order>();
    public IReadOnlyList<Order> Open => orders;
    public bool PanelOpen { get; private set; }
    public int LastRefreshDay => lastRefreshDay;
    private int lastRefreshDay;
    private PlayerWeaponSystem player;
    private EcoMarket nearMarket;
    private float nextScan;
    private string message = "";
    private static readonly string[] Clients = { "Mahalle fırını", "Okul mutfağı", "Yaşlılar evi", "Mahalle pazarı" };
    public void Restore(int refreshDay, List<Order> saved)
    { lastRefreshDay = refreshDay; orders.Clear(); if (saved != null) orders.AddRange(saved); }
    private void Awake() { Current = this; }
    private void OnDestroy() { EcoUIFlow.SetPanel(this, false); if (Current == this) Current = null; }
    public void Close() { PanelOpen = false; EcoUIFlow.SetPanel(this, false); }
    private void Update()
    {
        var clock = EcoWorldClock.Current;
        if (clock != null) RefreshIfDue(clock.day);
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + .3f;
            if (player == null) player = FindFirstObjectByType<PlayerWeaponSystem>();
            nearMarket = null;
            if (player != null) foreach (var shop in FindObjectsByType<EcoMarket>(FindObjectsSortMode.None))
                if (shop.CanReach(player)) { nearMarket = shop; break; }
        }
        if (PanelOpen && (nearMarket == null || !EcoUIFlow.IsPanel(this))) Close();
        var keys = Keyboard.current;
        if (keys == null) return;
        if (PanelOpen && (keys.escapeKey.wasPressedThisFrame || EcoKeys.Pressed(EcoKeys.Action.Orders))) { if (keys.escapeKey.wasPressedThisFrame) EcoUIFlow.ConsumeEscape(); Close(); return; }
        if (EcoKeys.Pressed(EcoKeys.Action.Orders) && nearMarket != null && !PlayerController.Paused)
        { EcoMarketUI.CloseFor(player); PanelOpen = true; EcoUIFlow.SetPanel(this, true); player.GetComponentInParent<PlayerController>()?.CancelWalkOrder(); }
    }
    public void RefreshIfDue(int day)
    {
        int period = day / RefreshEveryDays * RefreshEveryDays;
        if (period < RefreshEveryDays || period <= lastRefreshDay) return;
        lastRefreshDay = period; orders.Clear();
        var choices = new List<int> { 0 };
        if (EcoDiscoveries.CanPlant(EcoCropCatalog.Kind.Carrot)) choices.Add(1);
        if (EcoDiscoveries.CanPlant(EcoCropCatalog.Kind.Tomato)) choices.Add(2);
        if (EcoDiscoveries.Has("field.fished")) choices.Add(3);
        var random = new System.Random(period * 7919 + 13);
        for (int i = 0; i < OrdersPerRefresh; i++)
        {
            int kind = choices[random.Next(choices.Count)], amount = random.Next(2) == 0 ? 4 : 8;
            var value = kind == 3 ? new Vector2Int(4, 2) : EcoMarket.ProduceValue((EcoCropCatalog.Kind)kind);
            string name = kind == 3 ? "temiz balık" : EcoCropCatalog.Get((EcoCropCatalog.Kind)kind).Name;
            orders.Add(new Order { title = Clients[random.Next(Clients.Length)] + " • " + name, food = amount,
                cropKind = Mathf.Min(kind, 2), fish = kind == 3, issuedDay = period,
                metal = value.x * amount * 5 / 4, plastic = value.y * amount * 5 / 4 });
        }
        player?.ShowFeedback("Yeni avantajlı siparişler geldi • markette " + EcoKeys.Tag(EcoKeys.Action.Orders) + ".");
    }
    public bool Deliver(Order order)
    {
        if (order == null || order.delivered || player == null || nearMarket == null || !nearMarket.CanReach(player)) return false;
        if (!player.DeliverTypedOrder(order.cropKind, order.fish, order.food, order.metal, order.plastic))
        { message = "İstenen türde yeterli temiz ürün yok."; return false; }
        order.delivered = true; message = "Teslim edildi • +" + order.metal + "M +" + order.plastic + "P";
        EcoWorldSave.Current?.RequestAutosave(); return true;
    }
    private void OnGUI()
    {
        if (!PanelOpen) return;
        EcoTheme.Begin();
        var rect = EcoTheme.Window(760, 540, "SİPARİŞ PANOSU", "Temiz üretimin mahallede karşılık buluyor.", "ui_orders");
        GUILayout.BeginArea(new Rect(rect.x + 28, rect.y + 94, rect.width - 56, rect.height - 120));
        GUILayout.Label("Her üç günde iki isteğe bağlı talep • %25 ek karşılık • Süre cezası yok", EcoTheme.Body);
        int day = EcoWorldClock.Current != null ? EcoWorldClock.Current.day : 1;
        GUILayout.Label("Gün " + day + "   |   Sonraki yenileme: " + (lastRefreshDay > 0 ? lastRefreshDay + 3 : 3) + ". gün", EcoTheme.Muted);
        GUILayout.Space(18);
        if (orders.Count == 0) GUILayout.Label("İlk siparişler 3. gün gelir. Normal takas her zaman açık.", EcoTheme.Body);
        foreach (var order in orders)
        {
            GUILayout.BeginVertical(EcoTheme.Card);
            GUILayout.BeginHorizontal();
            var iconRect = GUILayoutUtility.GetRect(44, 44, GUILayout.Width(44), GUILayout.Height(44));
            EcoTheme.Icon(iconRect, order.fish ? "fish" : order.cropKind == 1 ? "crop_carrot" : order.cropKind == 2 ? "crop_tomato" : "crop_cabbage");
            GUILayout.Space(10);
            GUILayout.Label(order.title, EcoTheme.Subtitle);
            GUILayout.EndHorizontal();
            GUILayout.Label(order.food + " adet temiz ürün → " + order.metal + " metal + " + order.plastic + " plastik", EcoTheme.Body);
            bool has = player != null && (order.fish ? player.CleanFish : player.CountProduce((EcoCropCatalog.Kind)order.cropKind)) >= order.food;
            GUI.enabled = !order.delivered && has;
            var buttonRect = GUILayoutUtility.GetRect(100, 40, GUILayout.ExpandWidth(true), GUILayout.Height(40));
            if (EcoTheme.IconButton(buttonRect, order.delivered ? "TESLİM EDİLDİ" : has ? "Teslim et" : "Ürün yetersiz", order.delivered ? "ui_check" : has ? "metal" : "poi_warning")) Deliver(order);
            GUI.enabled = true; GUILayout.EndVertical(); GUILayout.Space(12);
        }
        GUILayout.Label(message, EcoTheme.Muted);
        if (GUILayout.Button("Kapat [O / Esc]", EcoTheme.Button, GUILayout.Height(40))) Close();
        GUILayout.EndArea(); EcoTheme.End();
    }
}
