using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class EcoTutorialLesson
{
    private const float BatteryRadius = 1.5f;
    private const float CablePracticeReach = 6.5f;
    private const float ChargePracticeReach = 2.3f;
    private const float ChargeGoal = 5f;
    private static readonly int BatteryIndex = EcoBuildingCatalog.Find("battery");

    [SerializeField] private Transform batteryPad;
    [SerializeField] private EcoChargingStation trainingCharger;

    private EcoPowerNode trainingSolar, trainingBattery, trainingChargerNode;
    private float chargeAtStart;

    private void RecordTrainingSolar(EcoStructure structure)
    {
        trainingSolar = structure.GetComponent<EcoPowerNode>();
    }

    private void EnterPowerStep()
    {
        if (batteryPad == null || trainingCharger == null)
        {
            Debug.LogError("EcoTutorialLesson: batarya alanı ve şarj istasyonu sahneye bağlı olmalı.", this);
            EnterStep(Step.Finish);
            return;
        }

        if (trainingChargerNode == null) trainingChargerNode = trainingCharger.GetComponent<EcoPowerNode>();
        if (step != Step.Charge) return;

        tools.Builder.SetBuildMode(false);
        chargeAtStart = trainingCharger.TotalDeliveredEnergy;
        // The practice kit always has room to demonstrate actual power transfer.
        float excess = tools.Energy - Mathf.Max(0f, tools.maximumEnergy - ChargeGoal);
        if (excess > 0f) tools.TrySpendElectricity(excess);
    }

    private void UpdatePowerStep()
    {
        if (step < Step.Battery || step > Step.Charge) return;
        if (trainingSolar == null || !trainingSolar.Operational)
        {
            EnterStep(Step.Solar);
            return;
        }

        if (step == Step.Battery)
        {
            foreach (var node in EcoPowerNode.Active)
            {
                if (node == null || node.kind != EcoPowerNode.Kind.Battery || !node.Operational || node.gameObject.scene != gameObject.scene ||
                    FlatDistance(node.transform.position, batteryPad.position) > BatteryRadius ||
                    !node.TryGetComponent<EcoStructure>(out var structure) || !structure.playerBuilt) continue;
                trainingBattery = node;
                Advance();
                break;
            }
            return;
        }

        if (trainingBattery == null || !trainingBattery.Operational)
        {
            EnterStep(Step.Battery);
            return;
        }

        bool solarConnected = HasTrainingCable(trainingSolar, trainingBattery);
        if (step == Step.SolarCable)
        {
            if (solarConnected) Advance();
            return;
        }
        if (!solarConnected)
        {
            EnterStep(Step.SolarCable);
            return;
        }

        bool chargerConnected = HasTrainingCable(trainingBattery, trainingChargerNode);
        if (step == Step.ChargerCable)
        {
            if (chargerConnected) Advance();
            return;
        }
        if (!chargerConnected)
        {
            EnterStep(Step.ChargerCable);
            return;
        }

        if (DeliveredTrainingCharge >= ChargeGoal - 0.01f)
        {
            tools.StopCharging();
            Advance();
        }
    }

    private float DeliveredTrainingCharge => trainingCharger.TotalDeliveredEnergy - chargeAtStart;

    private bool NearTrainingCharger => trainingCharger != null &&
        Vector3.Distance(player.transform.position, trainingCharger.transform.position) <= Mathf.Min(ChargePracticeReach, trainingCharger.reach - 0.15f);

    internal bool TryUseTrainingCharger(PlayerWeaponSystem sender)
    {
        if (step != Step.Charge || sender != tools || !NearTrainingCharger || tools.Builder.IsBuilding) return false;
        trainingCharger.Toggle(sender);
        return true;
    }

    private static bool HasTrainingCable(EcoPowerNode a, EcoPowerNode b)
    {
        foreach (var cable in EcoPowerCable.Active)
            if (cable != null && cable.playerBuilt && cable.IsConnected &&
                ((cable.a == a && cable.b == b) || (cable.a == b && cable.b == a))) return true;
        return false;
    }

    private void RefreshPowerPrompt()
    {
        var build = tools.Builder;
        switch (step)
        {
            case Step.Battery:
                SetPowerTarget(batteryPad.position, 1.1f, "electricity");
                if (Distance(batteryPad.position) > 5f) PromptPowerApproach("Batarya alanına yaklaş");
                else if (SelectPowerBuildItem(BatteryIndex, "Bataryayı seç"))
                    Prompt("Bataryayı kur", "Yeşil önizlemeye tıkla", mouse: true);
                break;
            case Step.SolarCable:
                PromptTrainingCable(trainingSolar, trainingBattery);
                break;
            case Step.ChargerCable:
                PromptTrainingCable(trainingBattery, trainingChargerNode);
                break;
            case Step.Charge:
                SetPowerTarget(trainingCharger.transform.position, 0.75f, "poi_charge");
                if (build.IsBuilding) Prompt("İnşa modundan çık", "", EcoKeys.Get(EcoKeys.Action.Build));
                else if (!NearTrainingCharger)
                    Prompt("Şarj istasyonuna yaklaş", "");
                else if (tools.ChargingStation == trainingCharger)
                    Prompt("Araç şarj oluyor", Mathf.Clamp(DeliveredTrainingCharge, 0f, ChargeGoal).ToString("0.0") + " / " + ChargeGoal + " EB");
                else Prompt("Aracını şarj et", "5 EB aktar", EcoKeys.Get(EcoKeys.Action.Interact));
                break;
        }
    }

    private void PromptTrainingCable(EcoPowerNode first, EcoPowerNode second)
    {
        Vector3 start = first.transform.position;
        Vector3 end = second.transform.position;
        if (Distance(start) > CablePracticeReach || Distance(end) > CablePracticeReach)
        {
            SetPowerTarget((start + end) * 0.5f, 0.8f, "electricity");
            PromptPowerApproach("Bağlantı alanına yaklaş");
            return;
        }

        if (!SelectPowerBuildItem(tools.Builder.CableIndex, "Elektrik kablosunu seç"))
        {
            SetPowerTarget(start, 0.85f, PowerIcon(first));
            return;
        }

        var selected = tools.Builder.CableStart;
        if (selected != null && selected != first && selected != second)
        {
            SetPowerTarget(start, 0.85f, PowerIcon(first));
            Prompt("Kablo ucunu sıfırla", "Sağ tıkla, sonra işaretli ucu seç");
            return;
        }

        // Both connection orders are valid. Show the endpoint that is still missing.
        var next = selected == first ? second : first;
        SetPowerTarget(next.transform.position, 0.85f, PowerIcon(next));
        Prompt(selected == null ? "Kablonun ilk ucunu seç" : "İkinci kablo ucunu bağla", next.DisplayName, mouse: true);
    }

    private void PromptPowerApproach(string heading)
    {
        if (tools.Builder.IsBuilding) Prompt("İnşa modundan çık", heading, EcoKeys.Get(EcoKeys.Action.Build));
        else Prompt(heading, "");
    }

    private bool SelectPowerBuildItem(int selection, string title)
    {
        var build = tools.Builder;
        int page = selection / 5;
        if (!build.IsBuilding) Prompt("İnşa modunu aç", "", EcoKeys.Get(EcoKeys.Action.Build));
        else if (build.Page != page) Prompt("İnşa sayfasını değiştir", (page + 1) + ". sayfaya geç", Key.Tab);
        else if (build.Selection != selection) Prompt(title, "", (Key)((int)Key.Digit1 + selection % 5));
        else return true;
        return false;
    }

    private void SetPowerTarget(Vector3 position, float size, string icon)
    {
        if (target != position || !Mathf.Approximately(radius, size)) SetTarget(position, size);
        EcoUI.SetIcon(pinIcon, icon);
    }

    private static string PowerIcon(EcoPowerNode node) => node.kind == EcoPowerNode.Kind.Solar ? "poi_solar"
        : node.kind == EcoPowerNode.Kind.Charger ? "poi_charge" : "electricity";
}
