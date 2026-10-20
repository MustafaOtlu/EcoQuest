using UnityEngine;
using UnityEngine.UI;

// GDD §15.5 decision: the glacier is protected by a monitoring station. After the player switches it on ([E] next to
// it) it pushes the melt line back (EcoGlacierMelt.stationBonus) while the average carbon index stays under the
// threshold. If an EcoPowerNode sits on the same object the station also draws electricity and only protects while
// it is supplied. It doubles as the final terminal (EcoFinale) once active.
public sealed class EcoGlacierStation : MonoBehaviour
{
    public static EcoGlacierStation Current { get; private set; }
    [Range(0f, 100f)] public float safeCarbon = 20f;
    [Min(1f)] public float reach = 5.5f;
    [Min(0f)] public float meltBonus = 8f;
    [Min(0f)] public float requestedPower = 0.5f;

    public bool Active { get; private set; }
    public float AverageCarbon { get; private set; }
    public bool Safe => Active && Powered && AverageCarbon <= safeCarbon;
    /// <summary>True when no power node is attached or the network recently delivered at least half of the demand.</summary>
    public bool Powered => !hasNode || (Time.time - lastDelivery < 3f && powerRatio >= 0.5f);
    public void RestoreActive(bool on)
    {
        Active = on;
        ApplyBonus();
        nextScan = 0f;
    }

    private PlayerVitals player;
    private PlayerWeaponSystem weapons;
    private EcoGlacierMelt melt;
    private Text[] captions;
    private float nextScan, lastDelivery = -100f, powerRatio;
    private bool hasNode;

    private void Awake() { Current = this; hasNode = GetComponent<EcoPowerNode>() != null; }
    private void OnDestroy() { if (Current == this) Current = null; }

    public float RequestElectricity(float seconds) => Active ? requestedPower * Mathf.Max(0f, seconds) : 0f;
    public float DeliverElectricity(float amount, float seconds)
    {
        float wanted = RequestElectricity(seconds);
        float accepted = Mathf.Min(Mathf.Max(0f, amount), wanted);
        if (wanted > 0f) { powerRatio = accepted / wanted; lastDelivery = Time.time; }
        return accepted;
    }

    private void ApplyBonus()
    {
        if (melt == null) melt = FindFirstObjectByType<EcoGlacierMelt>();
        if (melt != null) melt.stationBonus = Active && Powered ? meltBonus : 0f;
    }

    public bool TryInteract(PlayerWeaponSystem w)
    {
        if (w == null || (w.transform.position - transform.position).sqrMagnitude > reach * reach) return false;
        if (Active) { w.ShowFeedback(Describe()); return true; }
        Active = true; ApplyBonus(); nextScan = 0f;
        w.ShowFeedback("İzleme istasyonu çalışıyor • Buzul erime çizgisi geriye çekildi • Final terminali açıldı.");
        return true;
    }

    private void Update()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + 1f;
        float sum = 0f; int count = 0;
        foreach (var region in FindObjectsByType<EcoRegion>(FindObjectsSortMode.None)) if (region != null) { sum += region.carbon; count++; }
        AverageCarbon = count > 0 ? sum / count : 0f;
        ApplyBonus();
        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (captions == null || captions.Length == 0) captions = GetComponentsInChildren<Text>(true);
        string text = !Active
            ? "BUZUL İZLEME İSTASYONU\n" + EcoKeys.Tag(EcoKeys.Action.Interact) + " Çalıştır • Karbon arttıkça buzullar erir"
            : "BUZUL İZLEME İSTASYONU\nOrtalama karbon " + AverageCarbon.ToString("0") + " / hedef ≤ " + safeCarbon.ToString("0") + (Safe ? " ✓" : " ✗") + (Powered ? "" : " • Elektrik yok");
        foreach (var caption in captions) if (caption != null) caption.text = text;
    }

    public string Describe() => Active
        ? "Buzul izleme istasyonu • Çalışıyor" + (Powered ? "" : " (elektrik bekliyor)") + "\nOrtalama karbon " + AverageCarbon.ToString("0") + " (hedef ≤ " + safeCarbon.ToString("0") + ") • Final terminali: " + EcoKeys.Tag(EcoKeys.Action.Interact) + " rezerv yükle"
        : "Buzul izleme istasyonu • " + EcoKeys.Tag(EcoKeys.Action.Interact) + " ile çalıştır";
}
