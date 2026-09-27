using UnityEngine;

public sealed class EcoFactory : MonoBehaviour
{
    [SerializeField] private bool filtered;
    public bool Filtered => filtered;
    public ParticleSystem smoke;
    public GameObject filterVisual;
    [Min(0)] public float airPerSecond = 0.12f, carbonPerSecond = 0.04f;
    private void Start() => RefreshVisual();
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(seconds)) return;
        var region = EcoRegion.At(transform.position);
        // Air filters catch particulates; they do not remove the factory's carbon emissions.
        region?.AddPollution(airPerSecond * seconds * (filtered ? 0.08f : 1), 0, 0, carbonPerSecond * seconds);
    }
    public bool InstallFilter(PlayerWeaponSystem player)
    {
        if (!isActiveAndEnabled || player == null || !player.CanOperate) return false;
        var hit = GetComponentInChildren<Collider>();
        if (Vector3.Distance(player.transform.position, hit != null ? hit.ClosestPoint(player.transform.position) : transform.position) > 3) return false;
        if (filtered) { player.ShowFeedback("Bu fabrikada filtre zaten takılı."); return false; }
        if (!player.UseAirFilter(true)) { player.ShowFeedback("Marketten büyük hava filtresi alman gerekiyor."); return false; }
        filtered = true; RefreshVisual(); player.Progression.Award(30);
        player.ShowFeedback("Büyük filtre takıldı • Hava emisyonu %92 azaldı. Karbon üretimi sürüyor."); return true;
    }
    public void RefreshVisual()
    {
        if (filterVisual != null) filterVisual.SetActive(filtered);
        foreach (var caption in GetComponentsInChildren<UnityEngine.UI.Text>())
            caption.text = filtered ? "FABRİKA\nFiltre çalışıyor" : "FABRİKA\n[E] Büyük filtre tak";
        if (smoke == null) return;
        var main = smoke.main; main.startColor = filtered ? new Color(0.92f, 0.95f, 0.95f, 0.6f) : new Color(0.12f, 0.1f, 0.08f, 0.85f);
        // Existing particles must also change when the filter is installed.
        smoke.Clear(); if (Application.isPlaying && smoke.gameObject.activeInHierarchy) smoke.Play();
    }
    public string Describe() => filtered ? "Fabrika • Büyük filtre takılı\nHava emisyonu %92 azaldı • Karbon üretimi sürüyor"
        : "Filtresiz fabrika • Hava kirliliği ve karbon üretiyor\n[E] Büyük hava filtresi tak";
}
