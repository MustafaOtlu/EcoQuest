using System.Collections.Generic;
using UnityEngine;

public sealed class EcoWaterNode : MonoBehaviour
{
    public enum Kind { Intake, Purifier, Tank, Farm }
    public enum Fluid { Dirty, Clean }
    internal static readonly HashSet<EcoWaterNode> Active = new HashSet<EcoWaterNode>();
    public Kind kind;
    [Min(0)] public float capacity = 100, dirtyWater, cleanWater;
    [Min(0)] public float litresPerSecond = 4, electricityPerLitre = 2;
    public WeaponWaterSource source;
    public bool returnToLake;
    public EcoRegion SourceRegion => source != null ? EcoRegion.At(source.transform.position) : EcoRegion.At(transform.position);
    public float LastProcessedClean { get; private set; }
    public float LastOutputRate { get; private set; }
    public float SlimeRemaining { get; private set; }
    public float ThroughputMultiplier => kind == Kind.Purifier && SlimeRemaining > 0f ? slimeRateMultiplier : 1f;
    private float slimeRateMultiplier = 1f;
    public Vector3 port = new Vector3(0, 0.6f, 0);
    public Vector3 SocketPosition => transform.TransformPoint(port);
    public float Space => Mathf.Max(0, capacity - dirtyWater - cleanWater);
    public bool Operational => isActiveAndEnabled && (GetComponent<EcoStructure>() == null || GetComponent<EcoStructure>().integrity > 0);
    public string DisplayName => kind == Kind.Intake ? "Su alım pompası" : kind == Kind.Purifier ? "Su arıtma tesisi" : kind == Kind.Farm ? "Tarla" : "Su deposu";
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
    private void Update() => Simulate(Time.deltaTime);
    public void Simulate(float seconds)
    {
        if (!Valid(seconds)) return;
        LastOutputRate = 0;
        if (returnToLake && kind == Kind.Purifier && cleanWater > 0)
        {
            float returned = Take(Fluid.Clean, Mathf.Min(cleanWater, litresPerSecond * seconds));
            SourceRegion?.AddPollution(0, -returned / 20f, 0, 0);
        }
        SlimeRemaining = Mathf.Max(0f, SlimeRemaining - seconds);
        if (SlimeRemaining <= 0f) slimeRateMultiplier = 1f;
    }
    public void ApplySlime(float seconds = 8f, float rateMultiplier = 0.5f)
    {
        if (kind != Kind.Purifier || !Valid(seconds) || !EcoRegion.Finite(rateMultiplier) || rateMultiplier < 0f || rateMultiplier >= 1f) return;
        slimeRateMultiplier = SlimeRemaining > 0f ? Mathf.Min(slimeRateMultiplier, rateMultiplier) : rateMultiplier;
        SlimeRemaining = Mathf.Max(SlimeRemaining, Mathf.Min(seconds, 30f));
    }
    public bool CanSend(Fluid fluid) => Operational && kind != Kind.Farm && (kind != Kind.Purifier || fluid == Fluid.Clean);
    public bool CanReceive(Fluid fluid) => Operational && kind != Kind.Intake && (kind != Kind.Purifier || fluid == Fluid.Dirty) && (kind != Kind.Farm || fluid == Fluid.Clean);
    public float Stored(Fluid fluid) => fluid == Fluid.Clean ? cleanWater : dirtyWater;
    public float Add(Fluid fluid, float amount)
    {
        if (!Operational || !Valid(amount)) return 0;
        float accepted = Mathf.Min(amount, Space);
        if (fluid == Fluid.Clean) cleanWater += accepted; else dirtyWater += accepted;
        return accepted;
    }
    public float Take(Fluid fluid, float amount)
    {
        if (!Operational || !Valid(amount)) return 0;
        float taken = Mathf.Min(amount, Mathf.Max(0, Stored(fluid)));
        if (fluid == Fluid.Clean) cleanWater -= taken; else dirtyWater -= taken;
        return taken;
    }
    private static bool Valid(float amount) => amount > 0 && !float.IsNaN(amount) && !float.IsInfinity(amount);
    public void FindSource()
    {
        source = null; float nearest = 3f;
        foreach (var candidate in FindObjectsByType<WeaponWaterSource>(FindObjectsSortMode.None))
        {
            var collider = candidate.GetComponentInChildren<Collider>();
            float distance = Vector3.Distance(transform.position, collider != null ? collider.bounds.ClosestPoint(transform.position) : candidate.transform.position);
            if (distance <= nearest) { nearest = distance; source = candidate; }
        }
    }
    public float RequestElectricity(float seconds)
    {
        if (!Operational || !Valid(seconds) || !Valid(electricityPerLitre) || !Valid(litresPerSecond)
            || !EcoRegion.Finite(dirtyWater) || !EcoRegion.Finite(cleanWater) || !EcoRegion.Finite(capacity)
            || dirtyWater < 0f || cleanWater < 0f || capacity < 0f || (double)dirtyWater + cleanWater > float.MaxValue) return 0;
        // Integrate the slowed part of the requested interval without changing the base rate.
        double processingSeconds = seconds - (double)Mathf.Min(seconds, SlimeRemaining) * (1f - ThroughputMultiplier);
        double rateLimit = litresPerSecond * processingSeconds;
        if (kind == Kind.Purifier) return (float)System.Math.Min(float.MaxValue, System.Math.Min(dirtyWater, rateLimit) * electricityPerLitre);
        if (kind != Kind.Intake) return 0;
        if (source == null) FindSource();
        if (source == null || !source.isActiveAndEnabled) return 0;
        var sourceCollider = source.GetComponentInChildren<Collider>();
        if (Vector3.Distance(transform.position, sourceCollider != null ? sourceCollider.bounds.ClosestPoint(transform.position) : source.transform.position) > 3f) return 0;
        return (float)System.Math.Min(float.MaxValue, System.Math.Min(Space, rateLimit) * electricityPerLitre);
    }
    public float DeliverElectricity(float amount, float seconds)
    {
        if (!Valid(amount)) return 0;
        float accepted = Mathf.Min(amount, RequestElectricity(seconds));
        if (accepted <= 0) return 0;
        float litres = Mathf.Min(kind == Kind.Purifier ? dirtyWater : Space, accepted / electricityPerLitre);
        if (kind == Kind.Intake) return Add(source.IsClean ? Fluid.Clean : Fluid.Dirty, litres) * electricityPerLitre;
        dirtyWater = Mathf.Max(0, dirtyWater - litres); cleanWater += litres;
        LastProcessedClean += litres; LastOutputRate = seconds > 0 ? litres / seconds : 0;
        return Mathf.Min(accepted, litres * electricityPerLitre);
    }
    public float FillWeapon(PlayerWeaponSystem user, bool allowDirty = false, float limit = float.MaxValue)
    {
        if (user == null || !user.CanOperate || !Operational || Vector3.Distance(user.transform.position, transform.position) > 8f) return 0;
        var fluid = cleanWater > 0 ? Fluid.Clean : Fluid.Dirty;
        if (!allowDirty && fluid == Fluid.Dirty) { user.ShowFeedback("Temiz su yok. Arıtma tesisini elektrik ve boruyla bağla."); return 0; }
        float amount = user.ReceiveWater(Mathf.Min(Stored(fluid), limit), fluid == Fluid.Clean);
        Take(fluid, amount);
        return amount;
    }
    public string Describe()
    {
        string state = !Operational ? "Devre dışı" : kind == Kind.Tank ? "E: temiz su doldur" : kind == Kind.Intake && source == null ? "Yakında su kaynağı yok" : "Elektrik ve boru bağlantısı gerekli";
        var power = GetComponent<EcoPowerNode>();
        if (Operational && power != null && power.DeliveredPower > 0) state = kind == Kind.Purifier ? "Arıtılıyor" : "Su çekiliyor";
        return DisplayName + " • " + state + "\nTemiz " + cleanWater.ToString("0.0") + " / Kirli " + dirtyWater.ToString("0.0") + " • Kapasite " + capacity.ToString("0")
            + (SlimeRemaining > 0f ? "\nSalgı: debi %" + (ThroughputMultiplier * 100f).ToString("0") + " • " + SlimeRemaining.ToString("0.0") + " sn" : "");
    }
}
