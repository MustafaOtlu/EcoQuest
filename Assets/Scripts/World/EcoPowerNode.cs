using System.Collections.Generic;
using UnityEngine;

public sealed class EcoPowerNode : MonoBehaviour
{
    public enum Kind { Solar, Wind, Battery, Charger, Consumer }
    internal static readonly HashSet<EcoPowerNode> Active = new HashSet<EcoPowerNode>();
    public Kind kind;
    [Min(0f)] public float ratedPower = 4f, capacity = 150f, storedEnergy;
    [Range(0f, 1f)] public float condition = 1f, solarExposure = 1f, windExposure = 1f;
    public bool checkShade = true;
    public Vector3 port = new Vector3(0, 0.4f, 0);
    [Min(0f)] public float requestedPower;
    public float DeliveredPower { get; internal set; }
    public float NetworkGeneration { get; internal set; }
    public float NetworkDemand { get; internal set; }
    public float NetworkStored { get; internal set; }
    public float NetworkCapacity { get; internal set; }
    public bool Operational => isActiveAndEnabled && condition > 0f;
    public Vector3 SocketPosition => transform.TransformPoint(port);
    public string DisplayName => kind switch
    {
        Kind.Solar => "Güneş paneli", Kind.Wind => "Rüzgâr türbini", Kind.Battery => "Batarya",
        Kind.Charger => "Şarj istasyonu", _ => "Elektrikli tesis"
    };
    private void OnEnable() => Active.Add(this);
    private void OnDisable() { Active.Remove(this); DeliveredPower = NetworkGeneration = NetworkDemand = NetworkStored = NetworkCapacity = 0f; }
    public float Generation(EcoWorldClock clock)
    {
        if (!Operational || clock == null) return 0f;
        if (kind == Kind.Solar)
        {
            if (clock.SolarFactor <= 0f) return 0f;
            if (checkShade)
                foreach (var hit in Physics.RaycastAll(SocketPosition + Vector3.up * 0.1f, clock.SunDirection, 100f, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.transform.IsChildOf(transform)) return 0f;
            return ratedPower * clock.SolarFactor * solarExposure * condition;
        }
        if (kind == Kind.Wind)
        {
            float wind = Mathf.Clamp01(clock.windStrength * windExposure);
            return ratedPower * Mathf.Pow(Mathf.InverseLerp(0.15f, 1f, wind), 3f) * condition;
        }
        return 0f;
    }
    internal float Demand(float seconds)
    {
        if (!Operational) return 0f;
        if (kind == Kind.Charger)
        {
            var charger = GetComponent<EcoChargingStation>();
            return charger != null ? charger.RequestEnergy(seconds) : 0f;
        }
        var water = GetComponent<EcoWaterNode>();
        if (kind == Kind.Consumer && TryGetComponent<EcoRecyclingFacility>(out var recycler)) return recycler.RequestElectricity(seconds);
        return kind == Kind.Consumer ? water != null ? water.RequestElectricity(seconds) : Mathf.Max(0f, requestedPower) * seconds : 0f;
    }
    internal float Supply(float amount, float seconds)
    {
        if (kind == Kind.Charger)
        {
            var charger = GetComponent<EcoChargingStation>();
            amount = charger != null ? charger.DeliverEnergy(amount) : 0f;
        }
        if (kind == Kind.Consumer && TryGetComponent<EcoWaterNode>(out var water)) amount = water.DeliverElectricity(amount, seconds);
        if (kind == Kind.Consumer && TryGetComponent<EcoRecyclingFacility>(out var recycler)) amount = recycler.DeliverElectricity(amount, seconds);
        DeliveredPower = seconds > 0f ? amount / seconds : 0f;
        return amount;
    }
    public string Describe() => DisplayName + "\nÜretim " + NetworkGeneration.ToString("0.0")
        + " • Tüketim " + NetworkDemand.ToString("0.0") + " /sn • Depo "
        + NetworkStored.ToString("0") + "/" + NetworkCapacity.ToString("0")
        + (Operational ? "" : " • Devre dışı");
}
