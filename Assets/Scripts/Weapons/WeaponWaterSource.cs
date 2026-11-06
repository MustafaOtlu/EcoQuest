using UnityEngine;

// Add to a water volume with a collider to make it refillable with Vacuum mode.
public sealed class WeaponWaterSource : MonoBehaviour
{
    public bool cleanWater = true;
    public bool useRegionalQuality;
    [Tooltip("Radius of the bank for bodies of water (0 = small source such as a barrel); the player stops at it.")]
    public float shoreRadius;
    [Tooltip("Small ponds have a finite supply; large map lakes and existing barrels stay unlimited unless enabled.")]
    public bool finiteVolume;
    [Min(1f)] public float capacity = 400f;
    [Min(0f)] public float rainFillPerSecond = 0.5f;
    [SerializeField] private float storedWater = -1f;
    [SerializeField] private bool containsTransferredDirtyWater;
    [SerializeField, Range(0f, 100f)] private float localPollution;
    public float LocalPollution => localPollution;
    public Color WaterColor => IsClean ? new Color(0.15f, 0.52f, 0.66f, 0.68f) : new Color(0.42f, 0.24f, 0.10f, 0.86f);
    public UnityEngine.UI.Text statusLabel;
    public float Volume
    {
        get
        {
            if (!finiteVolume) return float.PositiveInfinity;
            EnsureVolume(); return storedWater;
        }
    }
    public float Space => finiteVolume ? Mathf.Max(0f, SafeCapacity - Volume) : float.PositiveInfinity;
    public float FillFraction => !finiteVolume ? 1f : SafeCapacity > 0f ? Mathf.Clamp01(Volume / SafeCapacity) : 0f;
    private float SafeCapacity => EcoRegion.Finite(capacity) ? Mathf.Max(0f, capacity) : 0f;
    public bool IsClean
    {
        get
        {
            if (finiteVolume && Volume > 0.001f && containsTransferredDirtyWater) return false;
            return useRegionalQuality
                ? EcoRegion.At(transform.position) is EcoRegion region ? region.CleanWater : cleanWater && localPollution < 35f
                : cleanWater;
        }
    }
    public void ConfigureFiniteVolume(float maxVolume, float initialFraction = 1f)
    {
        if (!EcoRegion.Valid(maxVolume)) return;
        finiteVolume = true; capacity = maxVolume;
        if (!EcoRegion.Finite(storedWater) || storedWater < 0f)
            storedWater = capacity * (EcoRegion.Finite(initialFraction) ? Mathf.Clamp01(initialFraction) : 1f);
        EnsureVolume();
    }
    private void EnsureVolume()
    {
        if (!EcoRegion.Finite(storedWater) || storedWater < 0f) storedWater = SafeCapacity;
        storedWater = Mathf.Clamp(storedWater, 0f, SafeCapacity);
        if (storedWater <= 0.001f) containsTransferredDirtyWater = false;
    }
    public float Withdraw(float amount)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(amount)) return 0f;
        if (!finiteVolume) return amount;
        float taken = Mathf.Min(amount, Volume);
        storedWater = Mathf.Max(0f, storedWater - taken);
        if (storedWater <= 0.001f) containsTransferredDirtyWater = false;
        return taken;
    }
    public float Deposit(float amount, bool clean)
    {
        if (!isActiveAndEnabled || !EcoRegion.Valid(amount)) return 0f;
        if (!finiteVolume)
        {
            if (!clean) Pollute(amount / 20f);
            return amount;
        }
        float accepted = Mathf.Min(amount, Space);
        if (accepted <= 0f) return 0f;
        storedWater += accepted;
        // Match the carried tank: mixing dirty water taints the whole remaining batch.
        // Clean transfer/rain adds volume but never silently purifies existing pollution.
        if (!clean) containsTransferredDirtyWater = true;
        return accepted;
    }
    public float FillWeapon(PlayerWeaponSystem user, float limit)
    {
        if (!isActiveAndEnabled || user == null || !user.CanOperate || !EcoRegion.Valid(limit)) return 0f;
        float received = user.ReceiveWater(Mathf.Min(limit, Volume), IsClean);
        // Only the amount actually accepted by the carried tank leaves the source.
        return Withdraw(received);
    }
    public void SimulateRain(float seconds)
    {
        if (!finiteVolume || !isActiveAndEnabled || !EcoRegion.Valid(seconds) || !EcoRegion.Valid(rainFillPerSecond)) return;
        if (EcoWorldSave.Current != null && EcoWorldSave.Current.IsLoading) return;
        var clock = EcoWorldClock.Current;
        if (clock != null && clock.RainIntensity > 0f)
            Deposit(rainFillPerSecond * Mathf.Clamp01(clock.RainIntensity) * seconds, true);
    }
    public string Describe()
    {
        string quality = IsClean ? "TEMİZ SU" : "KİRLİ SU • Arıtmadan kullanma";
        if (!finiteVolume) return quality + "\nVakumla su al";
        return quality + " • " + Volume.ToString("0") + "/" + SafeCapacity.ToString("0") + " SB"
            + "\nSeviye %" + (FillFraction * 100f).ToString("0") + " • Yağmurda yavaşça dolar"
            + "\nToplama: su al • " + EcoKeys.Tag(EcoKeys.Action.Mode) + " Su modu: gölete aktar";
    }
    public void Pollute(float amount)
    {
        if (!useRegionalQuality || !EcoRegion.Valid(amount)) return;
        amount = Mathf.Min(amount, 100f);
        var region = EcoRegion.At(transform.position);
        if (region != null) region.AddPollution(0f, amount, 0f, 0f);
        else localPollution = Mathf.Clamp(localPollution + amount, 0f, 100f);
    }
    private MaterialPropertyBlock tint;
    private void Update()
    {
        SimulateRain(Time.deltaTime);
        if (statusLabel != null) statusLabel.text = Describe();
        if (!useRegionalQuality || !TryGetComponent<Renderer>(out var surface)) return;
        tint ??= new MaterialPropertyBlock();
        tint.SetColor("_BaseColor", WaterColor); surface.SetPropertyBlock(tint);
    }
}
