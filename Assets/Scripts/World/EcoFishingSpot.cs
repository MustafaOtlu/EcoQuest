using UnityEngine;

[RequireComponent(typeof(WeaponWaterSource))]
public sealed class EcoFishingSpot : MonoBehaviour
{
    [Range(0, 12)] public float population = 12;
    public PlayerWeaponSystem Fisher { get; private set; }
    public float CatchProgress { get; private set; }
    private WeaponWaterSource source;
    private Collider waterBounds;
    private EcoPondBasin basin;
    private float nextBasinCheck;
    private readonly GameObject[] fish = new GameObject[5];
    private void Awake() { source = GetComponent<WeaponWaterSource>(); waterBounds = GetComponent<Collider>(); basin = GetComponent<EcoPondBasin>(); }
    private bool HasFishingWater => source != null && (!source.finiteVolume || source.FillFraction >= 0.05f);
    public bool CanFish(PlayerWeaponSystem player) => isActiveAndEnabled && player != null && player.isActiveAndEnabled && player.CanOperate
        && HasFishingWater
        && player.Selected == PlayerWeaponSystem.Tool.Hands && !EcoMarketUI.BlocksGameplay && (player.Builder == null || !player.Builder.IsBuilding)
        && Vector3.Distance(player.transform.position, waterBounds != null ? waterBounds.bounds.ClosestPoint(player.transform.position) : transform.position) <= 3;
    public bool Interact(PlayerWeaponSystem player)
    {
        if (!CanFish(player)) { player?.ShowFeedback(!HasFishingWater ? "Gölette balık tutmak için yeterli su yok. Su taşı veya yağmuru bekle." : "Balık tutmak için 5 ile ellerini seç ve kıyıya yaklaş."); return false; }
        if (Fisher == player) { Stop(); player.ShowFeedback("Olta toplandı."); return true; }
        if (Fisher != null || population <= 4 || player.CleanFish + player.DirtyFish >= 20) { player.ShowFeedback("Balık nüfusu korunuyor (en az 4) veya balık depon dolu."); return false; }
        Fisher = player; CatchProgress = 0; player.ShowFeedback("Olta atıldı • 3 saniye bekle. E: iptal"); return true;
    }
    private void Update() { Simulate(Time.deltaTime); Animate(); }
    public void Poison(float amount)
    {
        if (!EcoRegion.Valid(amount) || !EcoRegion.Finite(population)) return;
        population = Mathf.Max(0f, Mathf.Clamp(population, 0f, 12f) - Mathf.Min(amount, 12f));
    }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        if (!HasFishingWater) { Stop(); return; }
        float capacity = source.IsClean ? 12 : 3;
        population = Mathf.MoveTowards(population, capacity, seconds * (source.IsClean ? 1f / 120f : 1f / 240f));
        if (Fisher == null) return;
        if (!CanFish(Fisher)) { Stop(); return; }
        CatchProgress += seconds;
        if (CatchProgress < 3) return;
        var player = Fisher; Stop();
        if (population > 4 && player.ReceiveFish(source.IsClean)) population -= 1;
    }
    private void Stop() { Fisher = null; CatchProgress = 0; }
    private void Animate()
    {
        int visible = HasFishingWater ? Mathf.Clamp(Mathf.FloorToInt(population), 0, fish.Length) : 0;
        var bounds = waterBounds != null ? waterBounds.bounds : new Bounds(transform.position, new Vector3(4, 0.1f, 4));
        if (basin == null && source != null && source.finiteVolume && Time.unscaledTime >= nextBasinCheck)
        { basin = GetComponent<EcoPondBasin>(); nextBasinCheck = Time.unscaledTime + 5f; }
        Vector3 center = bounds.center;
        float radiusX = bounds.extents.x * 0.65f, radiusZ = bounds.extents.z * 0.65f;
        if (basin != null)
        {
            center.y = basin.SurfaceHeight + 0.025f;
            radiusX = radiusZ = basin.SurfaceRadius * 0.6f;
        }
        else center.y += bounds.extents.y + 0.025f;
        for (int i = 0; i < fish.Length; i++)
        {
            if (i < visible && fish[i] == null)
            {
                fish[i] = EcoWorldArt.Spawn("Fish", bounds.center);
                if (fish[i] != null) fish[i].transform.SetParent(transform, true);
            }
            if (fish[i] == null) continue;
            fish[i].SetActive(i < visible); if (i >= visible) continue;
            float phase = Time.time * 0.6f + i * 1.9f;
            fish[i].transform.position = center + new Vector3(Mathf.Cos(phase) * radiusX, 0f, Mathf.Sin(phase) * radiusZ);
            fish[i].transform.rotation = Quaternion.Euler(0, -phase * Mathf.Rad2Deg, 0);
        }
    }
    public string Describe()
    {
        if (Fisher != null) return "Balık bekleniyor… " + Mathf.Max(0, 3 - CatchProgress).ToString("0.0") + " sn • " + EcoKeys.Tag(EcoKeys.Action.Interact) + " İptal";
        string water = source != null && source.finiteVolume ? source.Describe()
            : source != null && source.IsClean ? "Temiz su • Sağlıklı balık" : "Kirli su • Balıklar kirlenmiş";
        return water + (!HasFishingWater ? "\nSu az • Balık tutmak için göleti doldur."
            : "\nBalık " + Mathf.FloorToInt(population) + " • [5] Eller → " + EcoKeys.Tag(EcoKeys.Action.Interact) + " Balık tut");
    }
    private void OnDisable() { Stop(); foreach (var visual in fish) if (visual != null) visual.SetActive(false); }
}
