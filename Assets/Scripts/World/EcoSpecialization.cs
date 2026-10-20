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
    private float nextScan;
    private GUIStyle title, body;
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
            if (keyboard.escapeKey.wasPressedThisFrame && open) SetOpen(false);
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

    private void OnGUI()
    {
        if (!open)
        {
            if (Points > 0) GUI.Label(new Rect(Screen.width - 232f, 62f, 216f, 20f), "Uzmanlık hakkı: " + Points + "  " + EcoKeys.Tag(EcoKeys.Action.Skills) + "");
            return;
        }
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        }
        float w = 560f, h = 320f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.Box(panel, "");
        GUI.Label(new Rect(panel.x + 16f, panel.y + 10f, w - 32f, 24f), "UZMANLIK", title);
        GUI.Label(new Rect(panel.x + 16f, panel.y + 36f, w - 32f, 40f),
            "Her 40 kalıcı YEP bir uzmanlık hakkı verir. Kalan hak: " + Points + ". Bütün dokuz kademe açılabilir.", body);
        for (int i = 0; i < 3; i++)
        {
            float y = panel.y + 84f + i * 70f;
            GUI.Label(new Rect(panel.x + 16f, y, 200f, 22f), Names[i] + "  " + new string('●', ranks[i]) + new string('○', MaxRank - ranks[i]), title);
            GUI.Label(new Rect(panel.x + 16f, y + 22f, w - 160f, 40f), Info[i], body);
            GUI.enabled = Points > 0 && ranks[i] < MaxRank;
            if (GUI.Button(new Rect(panel.x + w - 130f, y + 6f, 110f, 28f), ranks[i] >= MaxRank ? "En yüksek" : "Geliştir")) TryBuy((Branch)i);
            GUI.enabled = true;
        }
        GUI.Label(new Rect(panel.x + 16f, panel.y + h - 28f, w - 32f, 22f), "Kapat: " + EcoKeys.Tag(EcoKeys.Action.Skills) + " veya [Esc]", body);
    }
}
