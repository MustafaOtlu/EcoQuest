using UnityEngine;

[RequireComponent(typeof(EcoPowerNode))]
public sealed class EcoChargingStation : MonoBehaviour
{
    [Min(0.1f)] public float chargeRate = 15f, reach = 3f;
    public PlayerWeaponSystem User { get; private set; }
    public void Toggle(PlayerWeaponSystem weapons)
    {
        if (weapons.ChargingStation == this) { weapons.StopCharging(); return; }
        if (User != null && User != weapons) { weapons.ShowFeedback("Bu şarj istasyonu kullanımda."); return; }
        if (!weapons.CanOperate || weapons.Energy >= weapons.maximumEnergy) { weapons.ShowFeedback("Silah bataryası dolu."); return; }
        weapons.StopCharging(); User = weapons; weapons.BeginCharging(this);
    }
    public float RequestEnergy(float seconds)
    {
        if (User == null) return 0f;
        var player = User.GetComponentInParent<PlayerVitals>();
        if (!User.isActiveAndEnabled || !User.CanOperate || User.ChargingStation != this || player == null
            || Vector3.Distance(player.transform.position, transform.position) > reach)
        { Stop(); return 0f; }
        if (User.Energy >= User.maximumEnergy - 0.001f) { User.ShowFeedback("Silah bataryası doldu."); Stop(); return 0f; }
        return Mathf.Min(Mathf.Max(0f, chargeRate) * seconds, User.maximumEnergy - User.Energy);
    }
    public float DeliverEnergy(float amount) => User != null ? User.ReceiveElectricity(amount) : 0f;
    public void Stop()
    {
        var previous = User; User = null;
        if (previous != null && previous.ChargingStation == this) previous.StopCharging();
    }
    private void OnDisable() => Stop();
}
