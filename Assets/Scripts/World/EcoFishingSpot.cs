using UnityEngine;

[RequireComponent(typeof(WeaponWaterSource))]
public sealed class EcoFishingSpot : MonoBehaviour
{
    [Range(0, 12)] public float population = 6;
    public PlayerWeaponSystem Fisher { get; private set; }
    public float CatchProgress { get; private set; }
    private WeaponWaterSource source;
    private Collider waterBounds;
    private readonly GameObject[] fish = new GameObject[5];
    private void Awake() { source = GetComponent<WeaponWaterSource>(); waterBounds = GetComponent<Collider>(); }
    private bool CanFish(PlayerWeaponSystem player) => isActiveAndEnabled && player != null && player.isActiveAndEnabled && player.CanOperate
        && player.Selected == PlayerWeaponSystem.Tool.Hands && !EcoMarketUI.BlocksGameplay && (player.Builder == null || !player.Builder.IsBuilding)
        && Vector3.Distance(player.transform.position, waterBounds != null ? waterBounds.ClosestPoint(player.transform.position) : transform.position) <= 3;
    public bool Interact(PlayerWeaponSystem player)
    {
        if (!CanFish(player)) { player?.ShowFeedback("Balık tutmak için 5 ile ellerini seç ve kıyıya yaklaş."); return false; }
        if (Fisher == player) { Stop(); player.ShowFeedback("Olta toplandı."); return true; }
        if (Fisher != null || population < 1 || player.CleanFish + player.DirtyFish >= 20) { player.ShowFeedback("Balık az veya balık depon dolu."); return false; }
        Fisher = player; CatchProgress = 0; player.ShowFeedback("Olta atıldı • 3 saniye bekle. E: iptal"); return true;
    }
    private void Update() { Simulate(Time.deltaTime); Animate(); }
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        float capacity = source.IsClean ? 12 : 3;
        population = Mathf.MoveTowards(population, capacity, seconds * (source.IsClean ? 0.06f : 0.03f));
        if (Fisher == null) return;
        if (!CanFish(Fisher)) { Stop(); return; }
        CatchProgress += seconds;
        if (CatchProgress < 3) return;
        var player = Fisher; Stop();
        if (population >= 1 && player.ReceiveFish(source.IsClean)) population -= 1;
    }
    private void Stop() { Fisher = null; CatchProgress = 0; }
    private void Animate()
    {
        int visible = Mathf.Clamp(Mathf.FloorToInt(population), 0, fish.Length);
        var bounds = waterBounds != null ? waterBounds.bounds : new Bounds(transform.position, new Vector3(4, 0.1f, 4));
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
            fish[i].transform.position = bounds.center + new Vector3(Mathf.Cos(phase) * bounds.extents.x * 0.65f, bounds.extents.y + 0.025f, Mathf.Sin(phase) * bounds.extents.z * 0.65f);
            fish[i].transform.rotation = Quaternion.Euler(0, -phase * Mathf.Rad2Deg, 0);
        }
    }
    public string Describe() => Fisher != null ? "Balık bekleniyor… " + Mathf.Max(0, 3 - CatchProgress).ToString("0.0") + " sn • [E] İptal"
        : (source.IsClean ? "Temiz su • Sağlıklı balık" : "Kirli su • Balıklar kirlenmiş") + "\nBalık " + Mathf.FloorToInt(population) + " • [5] Eller → [E] Balık tut";
    private void OnDisable() { Stop(); foreach (var visual in fish) if (visual != null) visual.SetActive(false); }
}
