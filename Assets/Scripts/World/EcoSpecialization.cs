using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// GDD §7: YEP specialisations. One point is earned at YEP levels 2, 4, 6, ... (level / 2 rounded down, minus points already spent).
// Three branches, three ranks each. Early choices change the order the player solves problems in; no recovery path
// depends on a single branch (every effect is a modest efficiency bonus). [K] opens the panel.
public sealed class EcoSpecialization : MonoBehaviour
{
    public enum Branch { Energy, Water, Ecology }
    public const int MaxRank = 3;

    public static EcoSpecialization Current { get; private set; }
    private static readonly int[] ranks = new int[3];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { System.Array.Clear(ranks, 0, ranks.Length); }

    // Effects other systems read (all are 1 when nothing is chosen).
    public static float EnergyUse => 1f - 0.08f * ranks[0];      // tool electricity cost
    public static float PowerOutput => 1f + 0.06f * ranks[0];    // solar / wind output
    public static float WaterEfficiency => 1f + 0.12f * ranks[1];// farm irrigation, pump and purifier flow
    public static float WaterUse => 1f - 0.05f * ranks[1];
    public bool PanelOpen => open;
    public static float Recovery => 1f + 0.10f * ranks[2];       // regional pollution recovery
    public static float RecyclingSpeed => 1f + 0.15f * ranks[2]; // recycling facilities
    public static float GrowthSpeed => 1f + 0.08f * ranks[2];    // crops and trees

    public static int Rank(Branch branch) => ranks[(int)branch];
    public static int[] Capture() => (int[])ranks.Clone();
    public static void Restore(int[] saved)
    {
        System.Array.Clear(ranks, 0, ranks.Length);
        if (saved != null) for (int i = 0; i < Mathf.Min(ranks.Length, saved.Length); i++) ranks[i] = Mathf.Clamp(saved[i], 0, MaxRank);
    }

    private static readonly string[] Names = { "Enerji", "Su", "Ekoloji" };
    private static readonly string[] Info =
    {
        "Araç elektriği %8 daha az harcanır • güneş ve rüzgâr üretimi %6 artar (kademe başına)",
        "Tarla sulama, pompa ve arıtma akışı %12 daha verimli (kademe başına)",
        "Bölge toparlanması %10, geri dönüşüm %15, bitki büyümesi %8 daha hızlı (kademe başına)"
    };

    private PlayerWeaponSystem weapons;
    private bool open;
    private Vector2 panelScroll;
    private float nextScan;
    private int appliedWater, appliedRecycle, appliedGrowth;
    private readonly HashSet<int> seenWater = new HashSet<int>(), seenRecycle = new HashSet<int>(), seenGrowth = new HashSet<int>();

    private void Awake() { Current = this; }
    private void OnDestroy() { if (Current == this) Current = null; }

    public int Points
    {
        get
        {
            if (weapons == null || weapons.Progression == null) return 0;
            int earned = weapons.Progression.EarnedRights;
            return Mathf.Max(0, earned - Spent);
        }
    }
    public static int Spent { get { int s = 0; foreach (int r in ranks) s += r; return s; } }

    private void Update()
    {
        if (weapons == null) weapons = FindFirstObjectByType<PlayerWeaponSystem>();
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (EcoKeys.Pressed(EcoKeys.Action.Skills) && !PlayerController.Paused) SetOpen(!open);
            if (keyboard.escapeKey.wasPressedThisFrame && open) { EcoUIFlow.ConsumeEscape(); SetOpen(false); }
        }
        if (open && !EcoUIFlow.IsPanel(this)) open = false;
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 1f;
        ApplyFlows();
    }

    // Rank-dependent stats of existing objects are scaled by the change in multiplier, so tier upgrades that
    // double the same fields stay correct. New objects are scaled once, when first seen.
    private void ApplyFlows()
    {
        float water = WaterEfficiency;
        foreach (var node in FindObjectsByType<EcoWaterNode>(FindObjectsSortMode.None))
        {
            int id = node.GetInstanceID();
            if (seenWater.Add(id)) node.litresPerSecond *= water;
            else if (appliedWater != ranks[1]) node.litresPerSecond *= water / (1f + 0.12f * appliedWater);
        }
        appliedWater = ranks[1];
        float recycle = RecyclingSpeed;
        foreach (var facility in FindObjectsByType<EcoRecyclingFacility>(FindObjectsSortMode.None))
        {
            int id = facility.GetInstanceID();
            if (seenRecycle.Add(id)) facility.unitsPerSecond *= recycle;
            else if (appliedRecycle != ranks[2]) facility.unitsPerSecond *= recycle / (1f + 0.15f * appliedRecycle);
        }
        appliedRecycle = ranks[2];
        float growth = GrowthSpeed;
        foreach (var plant in FindObjectsByType<PlantedSeed>(FindObjectsSortMode.None))
        {
            int id = plant.GetInstanceID();
            if (seenGrowth.Add(id)) plant.growthSeconds = Mathf.Max(1f, plant.growthSeconds / growth);
            else if (appliedGrowth != ranks[2]) plant.growthSeconds = Mathf.Max(1f, plant.growthSeconds * (1f + 0.08f * appliedGrowth) / growth);
        }
        appliedGrowth = ranks[2];
    }

    public bool TryBuy(Branch branch)
    {
        int index = (int)branch;
        if (ranks[index] >= MaxRank || Points <= 0) return false;
        ranks[index]++;
        if (weapons != null) weapons.ShowFeedback("Uzmanlık: " + Names[index] + " kademe " + ranks[index]);
        nextScan = 0f;
        return true;
    }

    public void SetOpen(bool value) { open = value; EcoUIFlow.SetPanel(this, value); }
    private void OnDisable() { open = false; EcoUIFlow.SetPanel(this, false); }

    private static readonly string[] Icons = { "electricity", "water", "biodiversity" };

    private void OnGUI()
    {
        // While closed, the HUD's skills button carries a badge when rights are available.
        if (!open) return;
        EcoTheme.Begin();
        var r = EcoTheme.Window(680f, 650f, "UZMANLIK", "Her 40 kalıcı YEP bir hak verir. Kalan hak: " + Points + " · Dokuz kademenin hepsi açılabilir.", "ui_skills", 48);
        if (EcoTheme.CloseButton(r)) SetOpen(false);
        var viewport = new Rect(r.x + 32, r.y + 114, r.width - 64, r.height - 204);
        float width = viewport.width - 22, total = 0;
        for (int i = 0; i < 3; i++)
            total += 106 + Mathf.Max(38, EcoTheme.Muted.CalcHeight(new GUIContent(Info[i]), width - 100)) + 12;
        panelScroll.x = 0;
        panelScroll = GUI.BeginScrollView(viewport, panelScroll, new Rect(0, 0, width, total - 12), false, true);
        float y = 0;
        for (int i = 0; i < 3; i++)
        {
            float descriptionHeight = Mathf.Max(38, EcoTheme.Muted.CalcHeight(new GUIContent(Info[i]), width - 100));
            float height = 106 + descriptionHeight;
            GUI.Box(new Rect(0, y, width, height), GUIContent.none, EcoTheme.Card);
            EcoTheme.Icon(new Rect(24, y + 24, 36, 36), Icons[i]);
            GUI.Label(new Rect(76, y + 22, width - 268, 28), Names[i], EcoTheme.Subtitle);
            for (int k = 0; k < MaxRank; k++)
            {
                var prev = GUI.color; GUI.color = k < ranks[i] ? EcoSettings.Good : EcoUI.Track;
                GUI.DrawTexture(new Rect(76 + k * 26f, y + 56, 18, 18), Texture2D.whiteTexture);
                GUI.color = prev;
            }
            GUI.Label(new Rect(76, y + 84, width - 100, descriptionHeight), Info[i], EcoTheme.Muted);
            GUI.enabled = Points > 0 && ranks[i] < MaxRank;
            if (EcoTheme.IconButton(new Rect(width - 180, y + 22, 156, 40), ranks[i] >= MaxRank ? "En yüksek" : "Geliştir", ranks[i] >= MaxRank ? "ui_check" : "yep")) TryBuy((Branch)i);
            GUI.enabled = true;
            y += height + 12;
        }
        GUI.EndScrollView();
        if (EcoTheme.IconButton(new Rect(r.x + 32f, r.yMax - 70f, r.width - 64f, 40f), "Kapat  " + EcoKeys.Tag(EcoKeys.Action.Skills) + " / [Esc]", "ui_close")) SetOpen(false);
        EcoTheme.End();
    }
}
