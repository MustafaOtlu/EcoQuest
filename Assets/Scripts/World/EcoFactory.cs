using UnityEngine;

public sealed class EcoFactory : MonoBehaviour
{
    [SerializeField] private bool filtered;
    public bool Filtered => filtered;
    [SerializeField] private bool converted;
    public bool Converted => converted;
    public ParticleSystem smoke;
    public GameObject filterVisual;
    // GDD §10 balance: an unfiltered factory adds +4 air pollution and +2 carbon per minute; the filter removes 80 % of
    // the air load (not carbon); the clean-energy conversion removes 90 % of the carbon while it is powered.
    [Min(0)] public float airPerSecond = 4f / 60f, carbonPerSecond = 2f / 60f;
    private void Start() => RefreshVisual();
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        var region = EcoRegion.At(transform.position);
        // Air filters catch particulates; they do not remove the factory's carbon emissions.
        if (converted && (!TryGetComponent<EcoPowerNode>(out var power) || power.DeliveredPower < 1.99f)) return;
        region?.AddPollution(airPerSecond * seconds * (filtered ? 0.2f : 1), 0, 0, carbonPerSecond * seconds * (converted ? 0.1f : 1));
    }
    public bool InstallFilter(PlayerWeaponSystem player)
    {
        if (!isActiveAndEnabled || player == null || !player.CanOperate) return false;
        var hit = GetComponentInChildren<Collider>();
        if (Vector3.Distance(player.transform.position, hit != null ? hit.bounds.ClosestPoint(player.transform.position) : transform.position) > 3) return false;
        if (filtered) { player.ShowFeedback("Bu fabrikada filtre zaten takılı."); return false; }
        if (!player.UseAirFilter(true)) { player.ShowFeedback("Marketten büyük hava filtresi alman gerekiyor."); return false; }
        filtered = true; RefreshVisual();
        player.ShowFeedback("Büyük filtre takıldı • Hava emisyonu %80 azaldı. Karbon üretimi sürüyor."); return true;
    }
    public bool ConvertToCleanPower(PlayerWeaponSystem player)
    {
        if (player == null || !player.CanOperate || converted) { player?.ShowFeedback("Fabrika temiz enerjiye dönüştürüldü."); return false; }
        if (!filtered) { player.ShowFeedback("Önce büyük filtreyi tak."); return false; }
        if (!player.TrySpendMaterials(20, 12)) { player.ShowFeedback("Temiz enerji dönüşümü: 20 metal + 12 plastik."); return false; }
        converted = true;
        var node = GetComponent<EcoPowerNode>(); if (node == null) node = gameObject.AddComponent<EcoPowerNode>();
        node.kind = EcoPowerNode.Kind.Consumer; node.requestedPower = 2;
        RefreshVisual(); player.ShowFeedback("Temiz enerji dönüşümü tamam. Kabloyu bağla; karbon yükü %90 azalır."); return true;
    }
    public void RestoreState(bool hasFilter, bool cleanPower)
    {
        filtered = hasFilter; converted = cleanPower;
        if (converted) { var node = GetComponent<EcoPowerNode>(); if (node == null) node = gameObject.AddComponent<EcoPowerNode>(); node.kind = EcoPowerNode.Kind.Consumer; node.requestedPower = 2; }
        RefreshVisual();
    }
    public void RefreshVisual()
    {
        if (filterVisual != null) filterVisual.SetActive(filtered);
        foreach (var caption in GetComponentsInChildren<UnityEngine.UI.Text>())
            caption.text = filtered ? "FABRİKA\nFiltre çalışıyor" : "FABRİKA\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Büyük filtre tak";
        if (smoke == null) return;
        var main = smoke.main; main.startColor = filtered ? new Color(0.92f, 0.95f, 0.95f, 0.6f) : new Color(0.12f, 0.1f, 0.08f, 0.85f);
        // Existing particles must also change when the filter is installed.
        smoke.Clear(); if (Application.isPlaying && smoke.gameObject.activeInHierarchy) smoke.Play();
    }
    public string Describe() => filtered ? "Fabrika • Büyük filtre takılı\nHava emisyonu %80 azaldı • Karbon üretimi sürüyor"
        : "Filtresiz fabrika • Hava kirliliği ve karbon üretiyor\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Büyük hava filtresi tak";
}
