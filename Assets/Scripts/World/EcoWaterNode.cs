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
    public Vector3 port = new Vector3(0, 0.6f, 0);
    public Vector3 SocketPosition => transform.TransformPoint(port);
    public float Space => Mathf.Max(0, capacity - dirtyWater - cleanWater);
    public bool Operational => isActiveAndEnabled && (GetComponent<EcoStructure>() == null || GetComponent<EcoStructure>().integrity > 0);
    public string DisplayName => kind == Kind.Intake ? "Su alım pompası" : kind == Kind.Purifier ? "Su arıtma tesisi" : kind == Kind.Farm ? "Tarla" : "Su deposu";
    private void OnEnable() => Active.Add(this);
    private void OnDisable() => Active.Remove(this);
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
            float distance = Vector3.Distance(transform.position, collider != null ? collider.ClosestPoint(transform.position) : candidate.transform.position);
            if (distance <= nearest) { nearest = distance; source = candidate; }
        }
    }
    public float RequestElectricity(float seconds)
    {
        if (!Operational || seconds <= 0 || electricityPerLitre <= 0) return 0;
        if (kind == Kind.Purifier) return Mathf.Min(dirtyWater, litresPerSecond * seconds) * electricityPerLitre;
        if (kind != Kind.Intake) return 0;
        if (source == null) FindSource();
        if (source == null || !source.isActiveAndEnabled) return 0;
        var sourceCollider = source.GetComponentInChildren<Collider>();
        if (Vector3.Distance(transform.position, sourceCollider != null ? sourceCollider.ClosestPoint(transform.position) : source.transform.position) > 3f) return 0;
        return Mathf.Min(Space, litresPerSecond * seconds) * electricityPerLitre;
    }
    public float DeliverElectricity(float amount, float seconds)
    {
        float accepted = Mathf.Min(Mathf.Max(0, amount), RequestElectricity(seconds));
        if (accepted <= 0) return 0;
        float litres = accepted / electricityPerLitre;
        if (kind == Kind.Intake) return Add(source.cleanWater ? Fluid.Clean : Fluid.Dirty, litres) * electricityPerLitre;
        dirtyWater = Mathf.Max(0, dirtyWater - litres); cleanWater += litres;
        return accepted;
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
        return DisplayName + " • " + state + "\nTemiz " + cleanWater.ToString("0.0") + " / Kirli " + dirtyWater.ToString("0.0") + " • Kapasite " + capacity.ToString("0");
    }
}
