using UnityEngine;

[RequireComponent(typeof(EcoPowerNode))]
public sealed class EcoRecyclingFacility : MonoBehaviour
{
    public int rawMetal, rawPlastic, rawOrganic, readyMetal, readyPlastic, readyOrganic;
    public float workProgress;
    public float unitsPerSecond = 2;
    public int storageCapacity = 200;
    public int RawCount => rawMetal + rawPlastic + rawOrganic;
    public int ReadyCount => readyMetal + readyPlastic + readyOrganic;
    public bool Operational => isActiveAndEnabled && (GetComponent<EcoStructure>() == null || GetComponent<EcoStructure>().integrity > 0);
    public float RequestElectricity(float seconds) => Operational && seconds > 0 ? Mathf.Min(Mathf.Max(0, RawCount - workProgress), unitsPerSecond * seconds) : 0;
    public float DeliverElectricity(float amount, float seconds)
    {
        float accepted = Mathf.Min(Mathf.Max(0, amount), RequestElectricity(seconds));
        workProgress += accepted;
        int count = Mathf.Min(RawCount, Mathf.FloorToInt(workProgress + 0.00001f));
        workProgress = Mathf.Max(0, workProgress - count);
        int metal = Mathf.Min(rawMetal, count); rawMetal -= metal; readyMetal += metal; count -= metal;
        int plastic = Mathf.Min(rawPlastic, count); rawPlastic -= plastic; readyPlastic += plastic; count -= plastic;
        int organic = Mathf.Min(rawOrganic, count); rawOrganic -= organic; readyOrganic += organic;
        return accepted;
    }
    public void Interact(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate || Vector3.Distance(owner.transform.position, transform.position) > 3f) return;
        int collected = Collect(owner);
        if (!Operational) { owner.ShowFeedback("Tesis onarılmalı. Hazır ürünler teslim alındı."); return; }
        owner.TakeRawResources(Mathf.Max(0, storageCapacity - RawCount - ReadyCount), out int metal, out int plastic, out int organic);
        rawMetal += metal; rawPlastic += plastic; rawOrganic += organic;
        owner.ShowFeedback(collected + " işlenmiş kaynak alındı • " + (metal + plastic + organic) + " atık tesise bırakıldı.");
    }
    public int Collect(PlayerWeaponSystem owner)
    {
        if (owner == null || !owner.CanOperate) return 0;
        int count = ReadyCount;
        int metal = readyMetal, plastic = readyPlastic, organic = readyOrganic;
        readyMetal = readyPlastic = readyOrganic = 0;
        owner.ReceiveProcessedResources(metal, plastic, organic); return count;
    }
    public void RecoverContents(PlayerWeaponSystem owner)
    {
        Collect(owner); owner.ReturnRawResources(rawMetal, rawPlastic, rawOrganic);
        rawMetal = rawPlastic = rawOrganic = 0; workProgress = 0;
    }
    public string Describe() => "Geri dönüşüm tesisi • " + (Operational ? "[E] Atık bırak / ürün al" : "Tamir gerekiyor")
        + "\nBekleyen " + RawCount + " • Hazır " + ReadyCount + " • İşlem " + GetComponent<EcoPowerNode>().DeliveredPower.ToString("0.0") + "/sn";
}
